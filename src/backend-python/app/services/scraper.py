import random
import uuid
from datetime import datetime

import httpx
import structlog
from tenacity import retry, stop_after_attempt, wait_exponential

logger = structlog.get_logger()

MOCK_HASHTAGS = [
    "#tecnologia", "#ai", "#python", "#dotnet", "#arquitectura",
    "#cloud", "#devops", "#frontend", "#backend", "#vortexflow"
]

class ScraperService:
    def __init__(self, platform: str):
        self.platform = platform

    @retry(stop=stop_after_attempt(3), wait=wait_exponential(multiplier=1, min=2, max=10))
    async def fetch_data(self) -> dict:
        """
        Simula una llamada a la API de una red social.
        Puede fallar aleatoriamente para probar los reintentos.
        """
        logger.info(f"Iniciando scraping para {self.platform}...")

        async with httpx.AsyncClient():
            if random.random() < 0.1:
                logger.warning(f"Error simulado (429 Too Many Requests) en {self.platform}. Reintentando...")
                raise httpx.HTTPStatusError(
                    "Too Many Requests",
                    request=httpx.Request("GET", "http://mock-api.local"),
                    response=httpx.Response(429)
                )

            num_trends = random.randint(3, 8)
            hashtags = random.sample(MOCK_HASHTAGS, k=num_trends)

            raw_data = {
                "request_id": str(uuid.uuid4()),
                "platform": self.platform,
                "timestamp": datetime.utcnow().isoformat(),
                "trends": [
                    {
                        "hashtag": tag,
                        "mentions": random.randint(100, 50000),
                        "sentiment_score": random.uniform(-1.0, 1.0),
                        "likes": random.randint(50, 10000),
                        "shares": random.randint(10, 5000)
                    }
                    for tag in hashtags
                ]
            }

            logger.info(f"Scraping completado para {self.platform}. Se obtuvieron {len(hashtags)} tendencias.")
            return raw_data
