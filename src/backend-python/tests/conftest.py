"""Shared pytest fixtures and environment setup for the Python backend.

The application's Settings (app/config.py) require several env vars to
construct without raising. They are real config values for the worker but
test-only stand-ins here. We use setdefault so an outer shell (e.g. docker
compose run --env-file=.env) still wins when present.
"""
from __future__ import annotations

import os

# Settings() in app/config.py raises ValueError on empty required URL fields;
# provide placeholders that satisfy Pydantic (RABBITMQ_URL/REDIS_URL need
# non-empty; API_KEY_INTERNAL needs min_length=16).
os.environ.setdefault("RABBITMQ_URL", "amqp://test:test@localhost:5672/")
os.environ.setdefault("REDIS_URL", "redis://localhost:6379/0")
os.environ.setdefault("DOTNET_INGEST_URL", "http://localhost/api/trends/ingest")
os.environ.setdefault("API_KEY_INTERNAL", "test-internal-key-1234567890")
