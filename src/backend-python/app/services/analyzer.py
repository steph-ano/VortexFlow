import structlog
from datetime import datetime
from app.models.trend import TrendData, TrendMetrics

logger = structlog.get_logger()

class AnalyzerService:
    @staticmethod
    def process(raw_data: dict) -> list[TrendData]:
        """
        Procesa los datos crudos obtenidos del scraper y los convierte a TrendData.
        """
        logger.info(f"Analizando datos crudos de {raw_data.get('platform', 'unknown')}...")
        
        processed_trends = []
        platform = raw_data.get("platform", "unknown")
        trends = raw_data.get("trends", [])
        
        for item in trends:
            hashtag = item.get("hashtag")
            if not hashtag:
                continue
                
            volume = item.get("mentions", 0)
            sentiment = item.get("sentiment_score", 0.0)
            
            likes = item.get("likes", 0)
            shares = item.get("shares", 0)
            engagement = (likes + (shares * 2)) / max(volume, 1)
            
            metrics = TrendMetrics(
                volume=volume,
                sentiment=sentiment,
                engagement=engagement
            )
            
            trend = TrendData(
                platform=platform,
                hashtags=[hashtag],
                source=f"scraper-mock-{platform}",
                metrics=metrics,
                timestamp=datetime.utcnow(),
                raw_data=item
            )
            processed_trends.append(trend)
            
        logger.info(f"Análisis completado. {len(processed_trends)} tendencias procesadas.")
        return processed_trends
