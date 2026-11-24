"""Optional isolated host proof: a stopped TIDAL session can restore mute."""
from __future__ import annotations

import os
from pathlib import Path

import pytest

from amc_wx_lite.host_client import LiteHostClient


@pytest.mark.skipif(not os.environ.get("AMC_AIRPLAY_TEST_HOST"),
                    reason="Requires an explicitly selected isolated host build")
def test_stopped_tidal_can_restore_local_mute_without_starting_audio(tmp_path: Path):
    for name in ("queue", "soloist", "buffer"):
        (tmp_path / name).mkdir()
    stderr = []
    client = LiteHostClient(
        os.environ["AMC_AIRPLAY_TEST_HOST"],
        queue_data_dir=tmp_path / "queue",
        library_db=tmp_path / "library.db",
        podcasts_db=tmp_path / "podcasts.db",
        state_json=tmp_path / "state.json",
        soloist_settings=tmp_path / "soloist.json",
        soloist_data_dir=tmp_path / "soloist",
        timeshift_buffer_folder=tmp_path / "buffer",
        release_profile="test",
        on_stderr=stderr.append,
    )
    try:
        client.start()
        try:
            client.hello()
        except Exception as error:
            pytest.fail(f"Isolated host startup failed: {error}; stderr: {stderr[-3:]}")
        first = client.call("transport.toggleMute", {"sessionId": "tidal"})
        status_while_idle = client.call("transport.status", {"sessionId": "tidal"})
        second = client.call("transport.toggleMute", {"sessionId": "tidal"})
        assert first["muted"] is True
        assert status_while_idle["muted"] is True
        assert second["muted"] is False
    finally:
        client.close()
