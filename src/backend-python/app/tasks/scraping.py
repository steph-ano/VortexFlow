import structlog
import asyncio
from app.worker import celery_app
from app.services.scraper import ScraperService
from app.tasks.processing import process_and_publish
from redis import Redis
from app.config import settings

logger = structlog.get_logger()
redis_client = Redis.from_url(settings.REDIS_URL)

PLATFORMS = ["twitter", "instagram", "tiktok"]

@celery_app.task(name="app.tasks.scraping.scrape_all_platforms")
def scrape_all_platforms():
    logger.info("Iniciando scraping periódico para todas las plataformas.")
    for platform in PLATFORMS:
        scrape_platform.delay(platform)

@celery_app.task(name="app.tasks.scraping.scrape_platform")
def scrape_platform(platform: str):
    lock_key = f"lock:scrape:{platform}"
    lock = redis_client.lock(lock_key, timeout=60, blocking_timeout=1)
    
    if not lock.acquire(blocking=False):
        logger.warning(f"Scraping para {platform} ya está en curso. Omitiendo.")
        return
        
    try:
        logger.info(f"Lock adquirido para {platform}. Iniciando scraping...")
        scraper = ScraperService(platform)
        
        loop = asyncio.get_event_loop()
        raw_data = loop.run_until_complete(scraper.fetch_data())
        
        process_and_publish.delay(raw_data)
        logger.info(f"Datos de {platform} enviados a procesamiento.")
    except Exception as e:
        logger.error(f"Error al hacer scraping para {platform}: {str(e)}")
    finally:
        try:
            lock.release()
        except Exception:
            pass
