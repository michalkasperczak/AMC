"""The web bridge carries only validated catalog IDs and fixed commands."""

from __future__ import annotations

import io
import json
import queue
from pathlib import Path

import pytest

from amc_wx_lite.apple_music_source import records_from_resources
from amc_wx_lite.apple_music_web import (
    AppleMusicWebClient,
    AppleMusicWebUnavailable,
    catalog_song_id,
    helper_for_host,
)


def _record(catalog_id: str | None):
    play_params = {"catalogId": catalog_id} if catalog_id is not None else {}
    return records_from_resources([{
        "id": "private.user-song",
        "type": "library-songs",
        "attributes": {"name": "Utwór", "playParams": play_params},
    }])[0]


def test_only_numeric_playable_catalog_id_crosses_bridge() -> None:
    assert catalog_song_id(_record("1804690076")) == "1804690076"
    assert catalog_song_id(_record(None)) is None
    assert catalog_song_id(_record("private.user-song")) is None
    assert catalog_song_id(None) is None


def test_helper_lives_beside_test_host_not_in_profile(tmp_path: Path) -> None:
    helper = helper_for_host(tmp_path / "host" / "amc_lite_host.exe")
    assert helper == (tmp_path / "host" / "apple-music-web"
                      / "amc_apple_music_web_host.exe")


class _FakeProcess:
    def __init__(self) -> None:
        self.stdin = io.StringIO()
        self.stdout = io.StringIO(
            '{"id":1,"result":{"handled":true,"message":"Odtwarzanie"}}\n'
        )

    def poll(self):
        return None

    def wait(self, **_kwargs):
        return 0

    def terminate(self):
        pass


def test_play_uses_private_pipe_and_never_sends_credentials(
    tmp_path: Path,
) -> None:
    executable = tmp_path / "amc_apple_music_web_host.exe"
    executable.touch()
    process = _FakeProcess()
    spawned = []

    def spawn(*args, **kwargs):
        spawned.append((args, kwargs))
        return process

    client = AppleMusicWebClient(executable, spawn=spawn)
    assert client.play("1804690076")["handled"] is True
    sent = json.loads(process.stdin.getvalue())
    assert sent == {"id": 1, "command": "play", "songId": "1804690076"}
    assert spawned[0][0] == ([str(executable)],)
    assert "cookie" not in process.stdin.getvalue().casefold()
    assert "music.apple.com" not in process.stdin.getvalue()
    client.close()


def test_invalid_song_and_idle_state_do_not_spawn(tmp_path: Path) -> None:
    client = AppleMusicWebClient(
        tmp_path / "missing.exe",
        spawn=lambda *_args, **_kwargs: pytest.fail("helper launched"),
    )
    assert client.transport("state") == {"hasSession": False}
    with pytest.raises(AppleMusicWebUnavailable):
        client.play("private.user-song")
    with pytest.raises(AppleMusicWebUnavailable):
        client.transport("executeJavaScript")


def test_broken_reply_closes_helper_before_retry(tmp_path: Path) -> None:
    executable = tmp_path / "amc_apple_music_web_host.exe"
    executable.touch()
    process = _FakeProcess()
    process.stdout = io.StringIO('{"id":99,"result":{"handled":true}}\n')
    client = AppleMusicWebClient(executable, spawn=lambda *_a, **_k: process)

    with pytest.raises(AppleMusicWebUnavailable):
        client.play("1804690076")
    assert not client.is_running
    assert process.stdin.closed


def test_timeout_closes_helper_without_exposing_private_data(
    tmp_path: Path, monkeypatch,
) -> None:
    executable = tmp_path / "amc_apple_music_web_host.exe"
    executable.touch()
    process = _FakeProcess()
    client = AppleMusicWebClient(executable, spawn=lambda *_a, **_k: process)
    monkeypatch.setattr(
        queue.Queue, "get",
        lambda self, **_kwargs: (_ for _ in ()).throw(queue.Empty()),
    )

    with pytest.raises(AppleMusicWebUnavailable) as error:
        client.play("1804690076")
    assert str(error.value) == "Osadzony odtwarzacz Apple Music nie odpowiada."
    assert not client.is_running
    assert process.stdin.closed
