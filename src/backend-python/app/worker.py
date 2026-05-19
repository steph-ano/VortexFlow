from celery import Celery
from celery.schedules import crontab
from app.config import settings

celery_app = Celery(
    "vortexflow_tasks",
    broker=settings.RABBITMQ_URL,
    backend=settings.REDIS_URL,
    include=["app.tasks.scraping", "app.tasks.processing", "app.tasks.publishing"]
)

celery_app.conf.update(
    task_serializer='json',
    accept_content=['json'],
    result_serializer='json',
    timezone='UTC',
    enable_utc=True,
)

celery_app.conf.beat_schedule = {
    'scrape-all-platforms-every-x-minutes': {
        'task': 'app.tasks.scraping.scrape_all_platforms',
        'schedule': crontab(minute=f'*/{settings.SCRAPING_INTERVAL_MINUTES}'),
    },
}
