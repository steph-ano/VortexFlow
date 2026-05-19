import structlog
from app.worker import celery_app
from app.services.analyzer import AnalyzerService
from app.tasks.publishing import publish_trend_event

logger = structlog.get_logger()

@celery_app.task(name="app.tasks.processing.process_and_publish")
def process_and_publish(raw_data: dict):
    logger.info("Iniciando procesamiento de datos crudos.")
    try:
        trends = AnalyzerService.process(raw_data)
        
        for trend in trends:
            trend_dict = trend.model_dump()
            trend_dict['timestamp'] = trend.timestamp.isoformat()
            
            publish_trend_event.delay(trend_dict)
            
        logger.info(f"Procesamiento completado. {len(trends)} eventos enviados a publicar.")
    except Exception as e:
        logger.error(f"Error durante el procesamiento: {str(e)}")
