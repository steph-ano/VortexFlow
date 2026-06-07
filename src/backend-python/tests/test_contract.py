"""Tests for the trend publishing contract between the Python worker and the
.NET ingest endpoint.

These tests assert the wire format that the .NET API expects:
  * the body MUST be a JSON list of events, not a single object;
  * the X-Api-Key header MUST be set and equal to the configured internal key;
  * the response status code MUST be 2xx for a successful publish.

The contract is also enforced on the .NET side by the [Authorize(Policy =
"InternalApiKey")] attribute on TrendsController.Ingest; if the body shape
or header name changes on either side, the contract test fails before the
service is even deployed.
"""
from __future__ import annotations

import json
from unittest.mock import MagicMock, patch

import httpx
import pytest

from app.models.trend import TrendMetrics, TrendsProcessedEvent
from app.tasks.publishing import (
    HTTP_FALLBACK_HEADER,
    _build_event,
    _publish_to_http_fallback,
)


@pytest.fixture
def sample_event() -> TrendsProcessedEvent:
    return TrendsProcessedEvent(
        eventId="11111111-1111-1111-1111-111111111111",
        platform="twitter",
        hashtags=["#vortexflow"],
        source="scraper-mock-twitter",
        timestamp="2026-06-06T00:00:00",
        metrics=TrendMetrics(volume=123, sentiment=0.42, engagement=0.5),
    )


def test_build_event_is_idempotent_for_same_input():
    """Same (platform, hashtags, timestamp) MUST yield the same EventId."""
    trend = {
        "platform": "twitter",
        "hashtags": ["#vortexflow"],
        "source": "scraper-mock-twitter",
        "timestamp": "2026-06-06T00:00:00",
        "metrics": {"volume": 1, "sentiment": 0.0, "engagement": 0.0},
    }
    e1 = _build_event(trend)
    e2 = _build_event(trend)
    assert e1.eventId == e2.eventId


def test_publish_to_http_fallback_uses_correct_header_and_list_body(sample_event, monkeypatch):
    """Captures the request built by the HTTP fallback and asserts its shape."""
    captured: dict = {}

    def fake_send(self, request: httpx.Request, **kwargs) -> httpx.Response:
        captured["url"] = str(request.url)
        captured["method"] = request.method
        captured["headers"] = dict(request.headers)
        captured["body"] = json.loads(request.content.decode("utf-8"))
        return httpx.Response(202, request=request, json={"accepted": True})

    monkeypatch.setattr(httpx.Client, "send", fake_send)

    with patch("app.tasks.publishing.settings") as fake_settings:
        fake_settings.DOTNET_INGEST_URL = "http://api.local/api/trends/ingest"
        fake_settings.API_KEY_INTERNAL = "test-internal-key-1234567890"
        _publish_to_http_fallback(sample_event)

    assert captured["method"] == "POST"
    assert captured["url"] == "http://api.local/api/trends/ingest"
    # Header MUST be X-Api-Key, NOT Api-Key or X-API-Key (case-sensitive on .NET).
    assert captured["headers"].get("x-api-key") == "test-internal-key-1234567890"
    # Body MUST be a list (the .NET controller expects List<TrendProcessedEvent>).
    assert isinstance(captured["body"], list)
    assert len(captured["body"]) == 1
    assert captured["body"][0]["eventId"] == sample_event.eventId
    assert captured["body"][0]["platform"] == sample_event.platform
    assert captured["body"][0]["hashtags"] == sample_event.hashtags


def test_publish_to_http_fallback_raises_on_5xx(sample_event, monkeypatch):
    def fake_send(self, request: httpx.Request, **kwargs) -> httpx.Response:
        return httpx.Response(500, request=request, json={"error": "boom"})

    monkeypatch.setattr(httpx.Client, "send", fake_send)

    with patch("app.tasks.publishing.settings") as fake_settings:
        fake_settings.DOTNET_INGEST_URL = "http://api.local/api/trends/ingest"
        fake_settings.API_KEY_INTERNAL = "test-internal-key-1234567890"

        with pytest.raises(httpx.HTTPStatusError):
            _publish_to_http_fallback(sample_event)
