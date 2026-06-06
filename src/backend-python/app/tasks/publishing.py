"""Publishing pipeline for processed trend events.

Both the broker and the HTTP fallback paths must use the same headers, payload
shape and authentication so the .NET ingest endpoint accepts either delivery
mode without contract drift.
"""
from __future__ import annotations

import uuid
from typing import Any

import httpx
import structlog
from kombu import Connection, Exchange, Producer
from redis import Redis
from tenacity import (
    RetryError,
    Retrying,
    retry_if_exception_type,
    stop_after_attempt,
    wait_exponential,
)

from app.config import settings
from app.models.trend import TrendsProcessedEvent
from app.worker import celery_app

logger = structlog.get_logger()
redis_client = Redis.from_url(settings.REDIS_URL)

# Names MUST match the .NET MassTransit topology on the consumer side.
EXCHANGE_NAME = "VortexFlow.Application.Events:TrendProcessedEvent"
MESSAGE_TYPE_URN = "urn:message:VortexFlow.Application.Events:TrendProcessedEvent"

HTTP_FALLBACK_HEADER = "X-Api-Key"
HTTP_FALLBACK_TIMEOUT_SECONDS = 10
MAX_PUBLISH_RETRIES = 3


def _build_event(trend_dict: dict) -> TrendsProcessedEvent:
    """Builds a TrendsProcessedEvent from the dict produced by processing.

    EventId is stable per (platform, hashtag) so retries from Celery do not
    produce duplicate snapshots on the consumer side.
    """
    event_id = trend_dict.get("eventId") or str(
        uuid.uuid5(
            uuid.NAMESPACE_DNS,
            f"{trend_dict['platform']}|{','.join(trend_dict['hashtags'])}|{trend_dict['timestamp']}",
        )
    )
    return TrendsProcessedEvent(
        eventId=event_id,
        platform=trend_dict["platform"],
        hashtags=trend_dict["hashtags"],
        source=trend_dict["source"],
        timestamp=trend_dict["timestamp"],
        metrics=trend_dict["metrics"],
    )


@celery_app.task(name="app.tasks.publishing.publish_trend_event", bind=True, max_retries=MAX_PUBLISH_RETRIES)
def publish_trend_event(self, trend_dict: dict) -> dict:
    """Publish a single trend event to the broker, with HTTP fallback.

    Raises if both paths fail so Celery records the failure and retries.
    """
    event = _build_event(trend_dict)

    # Best-effort cache write. A Redis failure must not poison the publish.
    cache_key = f"trend:current:{event.platform}:{event.hashtags[0]}"
    try:
        _cache_in_redis_with_retry(cache_key, event.model_dump_json())
    except RetryError as redis_exc:
        logger.warning("redis_cache_failed", error=str(redis_exc), event_id=event.eventId)

    try:
        _publish_to_rabbitmq(event)
        logger.info("trend_published", event_id=event.eventId, transport="rabbitmq")
        return {"eventId": event.eventId, "transport": "rabbitmq"}
    except Exception as exc:  # noqa: BLE001
        logger.warning("rabbitmq_publish_failed", error=str(exc), event_id=event.eventId)
        try:
            _publish_to_http_fallback(event)
            logger.info("trend_published", event_id=event.eventId, transport="http_fallback")
            return {"eventId": event.eventId, "transport": "http_fallback"}
        except Exception as http_exc:  # noqa: BLE001
            # Re-raise so Celery applies its own retry policy with the bound task.
            try:
                raise self.retry(exc=http_exc, countdown=2 ** self.request.retries)
            except MaxRetriesExceededError:
                logger.error("trend_publish_exhausted", event_id=event.eventId, error=str(http_exc))
                raise


def _cache_in_redis_with_retry(key: str, value: str) -> None:
    for attempt in Retrying(
        stop=stop_after_attempt(3),
        wait=wait_exponential(multiplier=1, min=2, max=10),
        retry=retry_if_exception_type(Exception),
        reraise=True,
    ):
        with attempt:
            redis_client.setex(key, 3600, value)


def _publish_to_rabbitmq(event: TrendsProcessedEvent) -> None:
    for attempt in Retrying(
        stop=stop_after_attempt(3),
        wait=wait_exponential(multiplier=1, min=2, max=10),
        retry=retry_if_exception_type(Exception),
        reraise=True,
    ):
        with attempt:
            with Connection(settings.RABBITMQ_URL) as conn:
                exchange = Exchange(EXCHANGE_NAME, type="fanout", durable=True)
                producer = Producer(conn)
                masstransit_message = {
                    "messageId": event.eventId,
                    "conversationId": str(uuid.uuid4()),
                    "messageType": [MESSAGE_TYPE_URN],
                    "message": event.model_dump(),
                }
                producer.publish(
                    masstransit_message,
                    exchange=exchange,
                    routing_key="",
                    serializer="json",
                    declare=[exchange],
                )


def _publish_to_http_fallback(event: TrendsProcessedEvent) -> None:
    """POST a single-element list of events to the .NET ingest endpoint.

    .NET expects a List<TrendProcessedEvent>, the X-Api-Key header, and the
    body to be the list itself (not a wrapper). This contract is tested in
    tests/test_contract.py.
    """
    for attempt in Retrying(
        stop=stop_after_attempt(3),
        wait=wait_exponential(multiplier=1, min=2, max=10),
        retry=retry_if_exception_type((httpx.RequestError, httpx.HTTPStatusError)),
        reraise=True,
    ):
        with attempt:
            with httpx.Client(timeout=HTTP_FALLBACK_TIMEOUT_SECONDS) as client:
                response = client.post(
                    settings.DOTNET_INGEST_URL,
                    json=[event.model_dump()],  # MUST be a list to match List<TrendProcessedEvent>
                    headers={
                        "Content-Type": "application/json",
                        HTTP_FALLBACK_HEADER: settings.API_KEY_INTERNAL,
                    },
                )
                response.raise_for_status()


class MaxRetriesExceededError(Exception):
    """Raised when Celery's self.retry has been exhausted."""
