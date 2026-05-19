from pydantic_settings import BaseSettings

class Settings(BaseSettings):
    RABBITMQ_URL: str = "amqp://guest:guest@localhost:5672//"
    REDIS_URL: str = "redis://localhost:6379/0"
    DOTNET_INGEST_URL: str = "http://localhost:5032/api/trends/ingest"
    API_KEY_INTERNAL: str = "VORTEX_INTERNAL_DEV_KEY_999"
    SCRAPING_INTERVAL_MINUTES: int = 5
    OTLP_ENDPOINT: str = "http://localhost:4317"
    HOSTNAME: str = "unknown"

    class Config:
        env_file = [".env", ".env.secrets"]

settings = Settings()
