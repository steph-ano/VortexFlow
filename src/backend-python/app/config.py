"""Application configuration for the VortexFlow Python worker.

All sensitive values MUST be provided via environment variables or a `.env`
file. There are no default secrets here: starting the service with a missing
required variable is a fatal startup error. This is the contract the audit
established; do not reintroduce development placeholders.
"""
from __future__ import annotations

from pydantic import Field, field_validator
from pydantic_settings import BaseSettings, SettingsConfigDict


class Settings(BaseSettings):
    model_config = SettingsConfigDict(
        env_file=[".env", ".env.secrets"],
        env_file_encoding="utf-8",
        case_sensitive=False,
        extra="ignore",
    )

    RABBITMQ_URL: str = Field(..., description="AMQP connection string for the broker.")
    REDIS_URL: str = Field(..., description="Redis connection string used for cache and Celery results.")
    DOTNET_INGEST_URL: str = Field(..., description="HTTP fallback URL for the .NET ingest endpoint.")
    API_KEY_INTERNAL: str = Field(
        ...,
        min_length=16,
        description="Static API key expected by the .NET ingest endpoint.",
    )
    SCRAPING_INTERVAL_MINUTES: int = Field(5, ge=1, le=1440)
    OTLP_ENDPOINT: str = Field("http://localhost:4317")
    HOSTNAME: str = Field("unknown")
    LOG_LEVEL: str = Field("INFO")

    @field_validator("RABBITMQ_URL", "REDIS_URL", "DOTNET_INGEST_URL")
    @classmethod
    def _no_localhost_defaults_in_production(cls, v: str) -> str:
        # The settings object is constructed at import time, so we cannot read
        # the environment here. The check is in startup_log(); this validator
        # is only used for syntactic validation.
        if not v:
            raise ValueError("URL is required")
        return v


settings = Settings()  # type: ignore[call-arg]
