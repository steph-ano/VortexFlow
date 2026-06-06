"""Test the Redis SCAN-based reader used by the /trends/current endpoint.

The reader used to use redis.keys() which is O(N) and blocks the server.
We use scan_iter instead and cap the number of records we collect.
"""
from __future__ import annotations

from unittest.mock import MagicMock

import pytest

from app.main import get_current_trends


def test_get_current_trends_uses_scan_not_keys(monkeypatch):
    """The endpoint MUST call scan_iter, never keys()."""
    fake_redis = MagicMock()
    fake_redis.scan_iter.return_value = iter([])
    monkeypatch.setattr("app.main.redis_client", fake_redis)

    get_current_trends(limit=10)

    assert fake_redis.scan_iter.called
    assert not fake_redis.keys.called


def test_get_current_trends_caps_at_limit(monkeypatch):
    fake_redis = MagicMock()
    # Yield 100 keys; limit=10 must short-circuit.
    fake_redis.scan_iter.return_value = (f"trend:current:twitter:#t{i}" for i in range(100))
    fake_redis.get.return_value = b'{"eventId":"e","platform":"twitter","hashtags":["#t0"],"source":"s","timestamp":"2026-01-01T00:00:00","metrics":{"volume":1,"sentiment":0.0,"engagement":0.0}}'
    monkeypatch.setattr("app.main.redis_client", fake_redis)

    result = get_current_trends(limit=10)
    assert result["count"] == 10
