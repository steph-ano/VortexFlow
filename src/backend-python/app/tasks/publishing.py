import uuid

import httpx
import structlog
from kombu import Connection, Exchange, Producer
from redis import Redis
from tenacity import retry, retry_if_exception_type, stop_after_attempt, wait_exponential

from app.config import settings
from app.models.trend import TrendsProcessedEvent
from app.worker import celery_app

logger = structlog.get_logger()
redis_client = Redis.from_url(settings.REDIS_URL)

@celery_app.task(name="app.tasks.publishing.publish_trend_event", bind=True, max_retries=3)
def publish_trend_event(self, trend_dict: dict):
    try:
        event_id = str(uuid.uuid4())
        event = TrendsProcessedEvent(
            eventId=event_id,
            platform=trend_dict["platform"],
            hashtags=trend_dict["hashtags"],
            source=trend_dict["source"],
            timestamp=trend_dict["timestamp"],
            metrics=trend_dict["metrics"]
        )

        cache_key = f"trend:current:{event.platform}:{event.hashtags[0]}"
        try:
            _cache_in_redis_with_retry(cache_key, event.model_dump_json())
        except Exception as redis_exc:
            logger.error(f"Failed to cache in Redis: {str(redis_exc)}")

        _publish_to_rabbitmq(event)
        logger.info(f"Evento {event_id} publicado exitosamente en RabbitMQ.")

    except Exception as exc:
        logger.warning(f"Fallo publicando en RabbitMQ, usando fallback HTTP: {str(exc)}")
        try:
            _publish_to_http_fallback(event)
            logger.info(f"Evento {event_id} enviado al fallback HTTP exitosamente.")
        except Exception as http_exc:
            logger.error(f"Fallback HTTP falló para {event_id}: {str(http_exc)}")
            raise self.retry(exc=http_exc, countdown=2 ** self.request.retries)

@retry(stop=stop_after_attempt(3), wait=wait_exponential(multiplier=1, min=2, max=10))
def _cache_in_redis_with_retry(key: str, value: str):
    redis_client.setex(key, 3600, value)

@retry(stop=stop_after_attempt(3), wait=wait_exponential(multiplier=1, min=2, max=10), reraise=True)
def _publish_to_rabbitmq(event: TrendsProcessedEvent):
    exchange_name = "VortexFlow.Application.Events:TrendProcessedEvent"

    with Connection(settings.RABBITMQ_URL) as conn:
        exchange = Exchange(exchange_name, type='fanout')
        producer = Producer(conn)

        masstransit_message = {
            "messageId": event.eventId,
            "conversationId": str(uuid.uuid4()),
            "messageType": [
                "urn:message:VortexFlow.Application.Events:TrendProcessedEvent"
            ],
            "message": event.model_dump()
        }

        producer.publish(
            masstransit_message,
            exchange=exchange,
            routing_key='',
            serializer='json',
            declare=[exchange]
        )

@retry(
    stop=stop_after_attempt(3),
    wait=wait_exponential(multiplier=1, min=2, max=10),
    retry=retry_if_exception_type((httpx.RequestError, httpx.HTTPStatusError)),
    reraise=True
)
def _publish_to_http_fallback(event: TrendsProcessedEvent):
    headers = {
        "Content-Type": "application/json",
        "Api-Key": settings.API_KEY_INTERNAL
    }

    with httpx.Client() as client:
        response = client.post(
            settings.DOTNET_INGEST_URL,
            json=event.model_dump(),
            headers=headers,
            timeout=10
        )
        response.raise_for_status()
