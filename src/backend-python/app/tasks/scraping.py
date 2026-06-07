"""Scrape task: runs the scraper for each platform under a Redis lock so that
two workers don't double-scrape the same source.
"""
from __future__ import annotations

import asyncio
import logging

import structlog
from redis import Redis

from app.config import settings
from app.services.scraper import ScraperService
from app.tasks.processing import process_and_publish
from app.worker import celery_app

logger = structlog.get_logger()
redis_client = Redis.from_url(settings.REDIS_URL)

PLATFORMS = ["twitter", "instagram", "tiktok"]


@celery_app.task(name="app.tasks.scraping.scrape_all_platforms")
def scrape_all_platforms():
    """Schedule a per-platform scrape. Each platform runs as its own task so a
    failure on one does not block the others.
    """
    for platform in PLATFORMS:
        scrape_platform.delay(platform)


@celery_app.task(name="app.tasks.scraping.scrape_platform", bind=True, max_retries=3)
def scrape_platform(self, platform: str):
    lock_key = f"lock:scrape:{platform}"
    lock = redis_client.lock(lock_key, timeout=60, blocking_timeout=1)
    if not lock.acquire(blocking=False):
        logger.info("scrape_skipped_locked", platform=platform)
        return
    try:
        logger.info("scrape_started", platform=platform)
        scraper = ScraperService(platform)
        loop = asyncio.new_event_loop()
        try:
            raw_data = loop.run_until_complete(scraper.fetch_data())
        finally:
            loop.close()
        process_and_publish.delay(raw_data)
        logger.info("scrape_dispatched", platform=platform)
    except Exception as exc:  # noqa: BLE001
        logger.error("scrape_failed", platform=platform, error=str(exc))
        # Re-raise so Celery records and retries per the bound task policy.
        # When max_retries is exceeded, the resulting MaxRetriesExceededError
        # MUST propagate (do not swallow it with `return`/`pass`), otherwise
        # Celery marks the task as SUCCESS even though every attempt failed.
        try:
            raise self.retry(exc=exc, countdown=2 ** self.request.retries)
        except MaxRetriesExceededError as exhausted:
            logger.error("scrape_exhausted", platform=platform, error=str(exhausted))
            raise
    finally:
        try:
            lock.release()
        except Exception:  # noqa: BLE001
            pass


class MaxRetriesExceededError(Exception):
    """Raised when Celery's self.retry has been exhausted."""
