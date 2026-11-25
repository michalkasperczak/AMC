"""Receiver start confirmation before AMC yields local playback."""
from __future__ import annotations

import threading

import pytest

from amc_wx_lite.cast_protocol import CastConnection, CastError


def connection_with(*responses: dict) -> CastConnection:
    connection = object.__new__(CastConnection)
    connection._transport_id = "receiver"
    connection._session_id = None
    connection._request_id = 0
    connection._changed = threading.Condition(threading.RLock())
    connection._messages = []
    connection._send = lambda *_args, **_kwargs: None
    answers = iter(responses)
    connection._wait = lambda *_args, **_kwargs: next(answers)
    return connection


def media_status(state: str, reason: str | None = None) -> dict:
    item = {"media": {"contentId": "http://example.test/audio.mp3"},
            "playerState": state, "mediaSessionId": 7}
    if reason is not None:
        item["idleReason"] = reason
    return {"type": "MEDIA_STATUS", "status": [item]}


@pytest.mark.parametrize("reason", ["FINISHED", "CANCELLED", None])
def test_idle_load_does_not_claim_playback(reason: str | None) -> None:
    connection = connection_with(media_status("IDLE", reason))
    with pytest.raises(CastError, match="rozpoczę|start"):
        connection.load("http://example.test/audio.mp3", "audio/mpeg", "Test")


def test_playing_load_is_accepted() -> None:
    connection = connection_with(media_status("PLAYING"))
    connection.load("http://example.test/audio.mp3", "audio/mpeg", "Test")
    assert connection._session_id == 7
