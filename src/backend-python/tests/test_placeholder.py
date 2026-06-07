"""Smoke test kept for backwards compatibility with the original test runner."""


def test_placeholder():
    # The repo's CI runs `pytest` and used to have only this stub. The real
    # tests live next to this file. Keep this so the suite is not empty.
    assert True
