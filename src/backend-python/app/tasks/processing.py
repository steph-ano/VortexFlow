"""Processing pipeline.

Each raw scrape is converted into a list of trend events and pushed to the
publishing task. Errors are logged with structlog and re-raised so Celery
records the failure and applies its retry policy.
"""
from __future__ import annotations

import structlog

from app.services.analyzer import AnalyzerService
from app.tasks.publishing import publish_trend_event
from app.worker import celery_app

logger = structlog.get_logger()


@celery_app.task(name="app.tasks.processing.process_and_publish", bind=True, max_retries=3)
def process_and_publish(self, raw_data: dict) -> None:
    try:
        trends = AnalyzerService.process(raw_data)
    except Exception as exc:  # noqa: BLE001
        logger.error("analyzer_failed", error=str(exc))
        try:
            raise self.retry(exc=exc, countdown=2 ** self.request.retries)
        except MaxRetriesExceededError:
            logger.error("analyzer_exhausted")
            return

    for trend in trends:
        trend_dict = trend.model_dump()
        trend_dict["timestamp"] = trend.timestamp.isoformat()
        publish_trend_event.delay(trend_dict)

    logger.info("processing_complete", count=len(trends))


class MaxRetriesExceededError(Exception):
    """Raised when Celery's self.retry has been exhausted."""
