"""Real LiteHost transport on a silent, synthetic FLAC; no TIDAL account."""

from __future__ import annotations

import io
import os
import re
import shutil
import subprocess
import sys
import time
import unittest
import pytest
from pathlib import Path

from amc_wx_lite.host_client import LiteHostClient


TOOLS = Path(__file__).resolve().parents[1] / "tools"
sys.path.insert(0, str(TOOLS))
import tidal_bts_audio as audio  # noqa: E402


class _UpstreamResponse(io.BytesIO):
    def __init__(self, data: bytes, *, status: int, headers: dict[str, str]):
        super().__init__(data)
        self.status = status
        self.headers = headers


class _SyntheticFlacUpstream:
    def __init__(self, data: bytes):
        self.data = data
        self.requests: list[str | None] = []

    def open(self, request, timeout: int):
        assert request.full_url == "https://audio.tidal.com/synthetic-test.flac"
        requested_range = request.headers.get("Range")
        self.requests.append(requested_range)
        if requested_range is None:
            return _UpstreamResponse(
                self.data,
                status=200,
                headers={"Content-Length": str(len(self.data))},
            )
        match = re.fullmatch(r"bytes=(\d+)-(\d*)", requested_range)
        assert match is not None, "FFmpeg podał nieoczekiwany zakres HTTP"
        start = int(match.group(1))
        end = min(int(match.group(2)), len(self.data) - 1) if match.group(2) else len(self.data) - 1
        assert 0 <= start <= end, "FFmpeg zażądał zakresu poza syntetycznym FLAC"
        return _UpstreamResponse(
            self.data[start : end + 1],
            status=206,
            headers={
                "Content-Length": str(end - start + 1),
                "Content-Range": f"bytes {start}-{end}/{len(self.data)}",
            },
        )


def _silent_flac(sample_rate: int) -> bytes:
    ffmpeg = shutil.which("ffmpeg")
    if ffmpeg is None:
        raise unittest.SkipTest("FFmpeg nie jest dostępny w PATH")
    result = subprocess.run(
        [
            ffmpeg, "-nostdin", "-hide_banner", "-loglevel", "error",
            "-f", "lavfi", "-i", f"anullsrc=r={sample_rate}:cl=stereo",
            "-t", "72", "-c:a", "flac", "-f", "flac", "pipe:1",
        ],
        capture_output=True,
        timeout=30,
        check=True,
    )
    assert result.stdout.startswith(b"fLaC")
    return result.stdout


def _wait_for_event(events: list[tuple[str, dict]], name: str, *, timeout: float = 25) -> None:
    deadline = time.monotonic() + timeout
    while time.monotonic() < deadline:
        if any(event_name == name for event_name, _ in events):
            return
        failed = [data for event_name, data in events if event_name == "playback.failed"]
        if failed:
            raise AssertionError(f"LiteHost zgłosił błąd odtwarzania: {failed[-1].get('message')}")
        time.sleep(0.1)
    raise AssertionError(f"Brak zdarzenia {name} po {timeout} s")


def _wait_for_position(client: LiteHostClient, minimum: float, *, timeout: float = 15) -> dict:
    deadline = time.monotonic() + timeout
    last: dict = {}
    while time.monotonic() < deadline:
        last = client.call("transport.status", {"sessionId": "tidal"})
        if last.get("positionSeconds", 0) >= minimum:
            return last
        time.sleep(0.15)
    raise AssertionError(f"TIDAL nie osiągnął pozycji {minimum} s; ostatni stan: {last}")


@pytest.mark.parametrize("source_rate,provide_metadata", [(192000, True), (44100, False)])
def test_real_host_tidal_transport_with_silent_loopback_flac(
    tmp_path, monkeypatch, source_rate, provide_metadata,
) -> None:
    if sys.platform != "win32":
        raise unittest.SkipTest("Rzeczywisty LiteHost z wyjściem audio wymaga Windows")
    root = Path(__file__).resolve().parents[2]
    host_executable = Path(os.environ["AMC_LITE_HOST_EXE"]) if os.environ.get("AMC_LITE_HOST_EXE") else (
        root / "src" / "AccessibleMediaController.LiteHost" / "bin" / "Release"
        / "net8.0-windows10.0.19041.0" / "amc_lite_host.exe"
    )
    if not host_executable.is_file():
        raise unittest.SkipTest("Najpierw zbuduj LiteHost Release z bieżących źródeł")

    state_path = tmp_path / "state.json"
    state_path.write_text("{}", encoding="utf-8")
    for name in ("APPDATA", "LOCALAPPDATA"):
        directory = tmp_path / name
        directory.mkdir()
        monkeypatch.setenv(name, str(directory))

    events: list[tuple[str, dict]] = []
    stderr: list[str] = []
    upstream = _SyntheticFlacUpstream(_silent_flac(source_rate))
    spoken_rate = format(source_rate / 1000, "g").replace(".", ",")
    expected_pcm = f"PCM w silniku {spoken_rate} kHz"
    source_label = f"FLAC, {spoken_rate} kHz"
    plan = audio.BtsAudioPlan(
        codec="flac",
        mime_type="audio/flac",
        duration_seconds=72,
        _signed_url="https://audio.tidal.com/synthetic-test.flac",
    )
    with audio.RangeProxy(plan, opener=upstream) as proxy:
        client = LiteHostClient(
            host_executable,
            state_json=state_path,
            release_profile="test",
            on_event=lambda name, data: events.append((name, data)),
            on_stderr=stderr.append,
        )
        try:
            client.start()
            started = client.call(
                "tidal.nativePlay",
                {
                    "id": "42",
                    "title": "Cichy test transportu",
                    "proxyUrl": proxy.local_url,
                    "durationSeconds": 72,
                    "sourceQualityLabel": source_label,
                    **({"sampleRateHz": source_rate} if provide_metadata else {}),
                },
                timeout=30,
            )
            assert started["ok"] and started["sessionId"] == "tidal"
            assert started["id"] == "tidal:42"
            _wait_for_event(events, "playback.started")
            status = _wait_for_position(client, 0.2)
            assert status["playing"] and status["sessionId"] == "tidal"
            assert status["durationSeconds"] >= 70
            assert f"źródło {source_label}" in status["currentPlaybackText"]
            assert expected_pcm in status["currentPlaybackText"]

            first_seek = client.call(
                "transport.seek", {"sessionId": "tidal", "positionSeconds": 30},
                timeout=30,
            )
            assert first_seek["sessionId"] == "tidal"
            status = _wait_for_position(client, 29.5)
            assert expected_pcm in status["currentPlaybackText"]

            paused = client.call("transport.pauseResume", {"sessionId": "tidal"})
            assert paused["paused"] is True
            status = client.call("transport.status", {"sessionId": "tidal"})
            assert status["paused"] is True
            paused_position = status["positionSeconds"]
            time.sleep(0.6)
            still_paused = client.call("transport.status", {"sessionId": "tidal"})
            assert still_paused["paused"] is True
            assert abs(still_paused["positionSeconds"] - paused_position) < 0.5

            selected = client.call(
                "audio.selectOutput",
                {"sessionId": "tidal", "deviceId": None, "restart": True},
            )
            assert selected["sessionId"] == "tidal" and selected["rebuilt"] is True
            assert selected["usingDefault"] is True
            status = client.call("transport.status", {"sessionId": "tidal"})
            assert status["paused"] is True

            # TIDAL is paused, then another local item replaces the shared
            # decoder. Space in TIDAL must restore the saved stream/position.
            other = tmp_path / "other-silent.flac"
            other.write_bytes(_silent_flac(44100))
            client.play_file(str(other), volume=0, rate=1.0)
            remembered = client.call("transport.status", {"sessionId": "tidal"})
            assert remembered["paused"] is True
            assert abs(remembered["positionSeconds"] - paused_position) < 1.0

            resumed = client.call("transport.pauseResume", {"sessionId": "tidal"})
            assert resumed["paused"] is False
            _wait_for_position(client, 29.5)

            sought = client.call(
                "transport.seek", {"sessionId": "tidal", "positionSeconds": 61},
                timeout=30,
            )
            assert sought["sessionId"] == "tidal"
            status = _wait_for_position(client, 60.5)
            assert status["durationSeconds"] >= 70
            assert len(upstream.requests) >= 2

            started_before_output_change = sum(name == "playback.started" for name, _ in events)
            selected_active = client.call(
                "audio.selectOutput",
                {"sessionId": "tidal", "deviceId": None, "restart": True},
            )
            assert selected_active["rebuilt"] is True
            deadline = time.monotonic() + 20
            while time.monotonic() < deadline:
                if sum(name == "playback.started" for name, _ in events) > started_before_output_change:
                    break
                time.sleep(0.1)
            else:
                raise AssertionError("Zmiana wyjścia nie wznowiła cichego strumienia TIDAL")
            status = _wait_for_position(client, 60.5)
            assert expected_pcm in status["currentPlaybackText"]

            stopped = client.call("transport.stop", {"sessionId": "tidal"})
            assert stopped["sessionId"] == "tidal" and stopped["playing"] is False
            status = client.call("transport.status", {"sessionId": "tidal"})
            assert status["playing"] is False

            exposed = repr(started) + repr(status) + repr(events) + repr(stderr)
            assert proxy.local_url not in exposed
            assert plan._signed_url not in exposed
        finally:
            client.close()
