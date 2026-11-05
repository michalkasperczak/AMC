"""Offline BTS resolver and loopback Range tests; no TIDAL login or audio files."""

from __future__ import annotations

import base64
import io
import json
import re
import shutil
import subprocess
import sys
import unittest
import urllib.error
import urllib.request
from contextlib import redirect_stderr
from pathlib import Path
from types import SimpleNamespace


TOOLS = Path(__file__).resolve().parents[1] / "tools"
sys.path.insert(0, str(TOOLS))
import tidal_bts_audio as audio  # noqa: E402
import tidalapi_audio_probe as pilot  # noqa: E402


SECRET = "signed-secret-must-not-leak"
SIGNED_URL = "https://sp-pr-fa.audio.tidal.com/track.flac?signature=" + SECRET


def payload(*, url: str = SIGNED_URL, encryption: str = "NONE", codec: str = "flac"):
    manifest = {
        "mimeType": "audio/flac",
        "codecs": codec,
        "encryptionType": encryption,
        "urls": [url],
    }
    return {
        "assetPresentation": "FULL",
        "manifestMimeType": "application/vnd.tidal.bts",
        "manifest": base64.b64encode(json.dumps(manifest).encode()).decode(),
    }


class FakeUpstream(io.BytesIO):
    status = 206

    def __init__(self, data: bytes):
        super().__init__(data)
        self.headers = {
            "Content-Length": str(len(data)),
            "Content-Range": "bytes 0-3/4096",
        }


class FakeOpener:
    def __init__(self, data: bytes = b"fLaC"):
        self.data = data
        self.calls: list[tuple[str, str | None]] = []

    def open(self, request, timeout: int):
        self.calls.append((request.full_url, request.headers.get("Range")))
        return FakeUpstream(self.data)


class BtsAudioTests(unittest.TestCase):
    def test_resolves_only_one_clear_tidal_https_url(self) -> None:
        plan = audio.resolve_bts(payload(), duration_seconds=187)
        self.assertEqual(plan.codec, "flac")
        self.assertEqual(plan.duration_seconds, 187)
        self.assertNotIn(SECRET, repr(plan))
        self.assertNotIn(SECRET, json.dumps(plan.public_status()))

    def test_rejects_preview_encryption_key_and_foreign_url(self) -> None:
        cases = []
        preview = payload()
        preview["assetPresentation"] = "PREVIEW"
        cases.append(preview)
        cases.append(payload(encryption="CENC"))
        cases.append(payload(url="https://127.0.0.1/track.flac"))
        cases.append(payload(url="http://sp-pr-fa.audio.tidal.com/track.flac"))
        cases.append(payload(url="https://audio.tidal.com.evil.example/track.flac"))
        keyed = payload()
        manifest = json.loads(base64.b64decode(keyed["manifest"]))
        manifest["keyId"] = SECRET
        keyed["manifest"] = base64.b64encode(json.dumps(manifest).encode()).decode()
        cases.append(keyed)
        for candidate in cases:
            with self.subTest(candidate=candidate["assetPresentation"]):
                with self.assertRaises(audio.BtsResolutionError):
                    audio.resolve_bts(candidate)

    def test_rejects_dash_and_multiple_urls(self) -> None:
        dash = payload()
        dash["manifestMimeType"] = "application/dash+xml"
        with self.assertRaises(audio.BtsResolutionError):
            audio.resolve_bts(dash)
        multiple = payload()
        manifest = json.loads(base64.b64decode(multiple["manifest"]))
        manifest["urls"].append(SIGNED_URL)
        multiple["manifest"] = base64.b64encode(json.dumps(manifest).encode()).decode()
        with self.assertRaises(audio.BtsResolutionError):
            audio.resolve_bts(multiple)

    def test_proxy_forwards_range_only_on_loopback_and_stops(self) -> None:
        upstream = FakeOpener()
        plan = audio.resolve_bts(payload())
        local_opener = urllib.request.build_opener(urllib.request.ProxyHandler({}))
        with audio.RangeProxy(plan, opener=upstream) as proxy:
            self.assertEqual(proxy._server.server_address[0], "127.0.0.1")
            local = proxy.local_url
            request = urllib.request.Request(local, headers={"Range": "bytes=0-3"})
            with local_opener.open(request, timeout=5) as response:
                self.assertEqual(response.status, 206)
                self.assertEqual(response.headers.get("Content-Range"), "bytes 0-3/4096")
                self.assertEqual(response.read(), b"fLaC")
            self.assertEqual(upstream.calls, [(SIGNED_URL, "bytes=0-3")])
            with self.assertRaises(urllib.error.HTTPError) as wrong:
                local_opener.open(local + "/wrong", timeout=5)
            self.assertEqual(wrong.exception.code, 404)
            wrong.exception.close()
        with self.assertRaises(RuntimeError):
            _ = proxy.local_url

    def test_proxy_reports_compressed_bitrate_once_from_normal_range_response(self) -> None:
        upstream = FakeOpener()
        values: list[int] = []
        plan = audio.resolve_bts(payload(), duration_seconds=1)
        local_opener = urllib.request.build_opener(urllib.request.ProxyHandler({}))
        with audio.RangeProxy(plan, opener=upstream) as proxy:
            proxy.set_bitrate_callback(values.append)
            for _ in range(2):
                request = urllib.request.Request(
                    proxy.local_url, headers={"Range": "bytes=0-3"}
                )
                with local_opener.open(request, timeout=5) as response:
                    self.assertEqual(response.read(), b"fLaC")
        self.assertEqual(values, [33])  # 4096 * 8 / 1 s / 1000
        self.assertEqual(len(upstream.calls), 2)  # no metadata-only request

    def test_proxy_rejects_invalid_range_without_upstream_request(self) -> None:
        upstream = FakeOpener()
        with audio.RangeProxy(audio.resolve_bts(payload()), opener=upstream) as proxy:
            request = urllib.request.Request(
                proxy.local_url, headers={"Range": "bytes=10-2"}
            )
            with self.assertRaises(urllib.error.HTTPError) as outcome:
                urllib.request.build_opener(urllib.request.ProxyHandler({})).open(
                    request, timeout=5
                )
            self.assertEqual(outcome.exception.code, 416)
            outcome.exception.close()
            self.assertEqual(upstream.calls, [])

    def test_proxy_redirect_failure_does_not_log_signed_url(self) -> None:
        class Redirecting:
            def open(self, request, timeout):
                raise urllib.error.HTTPError(SIGNED_URL, 302, SECRET, {}, None)

        stderr = io.StringIO()
        with redirect_stderr(stderr):
            with audio.RangeProxy(audio.resolve_bts(payload()), opener=Redirecting()) as proxy:
                request = urllib.request.Request(
                    proxy.local_url, headers={"Range": "bytes=0-3"}
                )
                with self.assertRaises(urllib.error.HTTPError) as outcome:
                    urllib.request.build_opener(urllib.request.ProxyHandler({})).open(
                        request, timeout=5
                    )
                self.assertEqual(outcome.exception.code, 502)
                outcome.exception.close()
        self.assertNotIn(SECRET, stderr.getvalue())

    def test_manual_pilot_reports_only_audio_header_status(self) -> None:
        class Session:
            def __init__(self):
                self.request = SimpleNamespace(basic_request=self.basic_request)

            def get_link_login(self):
                return SimpleNamespace(
                    verification_uri="link.tidal.com", user_code="ABCD-1234"
                )

            def process_link_login(self, challenge):
                return True

            def track(self, track_id):
                return SimpleNamespace(duration=187)

            def basic_request(self, method, path, params):
                assert params["audioquality"] == "LOSSLESS"
                return SimpleNamespace(status_code=200, json=lambda: payload())

        lines: list[str] = []
        result = pilot.run(
            "123", session_factory=Session, upstream_opener=FakeOpener(b"fLaC"),
            emit=lines.append,
        )
        self.assertEqual(result, 0)
        self.assertIn('"status":"audio_header"', lines[-1])
        self.assertNotIn(SECRET, "\n".join(lines))

    def test_manual_pilot_prints_login_failure_after_quiet_library_call(self) -> None:
        class Session:
            def get_link_login(self):
                return SimpleNamespace(
                    verification_uri="link.tidal.com", user_code="ABCD-1234"
                )

            def process_link_login(self, challenge):
                return False

        from contextlib import redirect_stdout

        output = io.StringIO()
        with redirect_stdout(output):
            result = pilot.run("123", session_factory=Session)
        self.assertEqual(result, 1)
        self.assertIn('Status: {"status":"login_failed"}', output.getvalue())

    def test_ffmpeg_reads_flac_through_local_range_proxy(self) -> None:
        executable = shutil.which("ffmpeg")
        if executable is None:
            self.skipTest("FFmpeg nie jest dostępny w PATH")
        generated = subprocess.run(
            [executable, "-v", "error", "-f", "lavfi", "-i",
             "anullsrc=r=44100:cl=stereo", "-t", "0.15", "-c:a", "flac",
             "-f", "flac", "pipe:1"],
            capture_output=True, timeout=15, check=True,
        ).stdout
        self.assertTrue(generated.startswith(b"fLaC"))

        class ByteRangeOpener:
            def __init__(self, data: bytes):
                self.data = data
                self.ranges: list[str | None] = []

            def open(self, request, timeout):
                range_value = request.headers.get("Range")
                self.ranges.append(range_value)
                if range_value is None:
                    result = FakeUpstream(self.data)
                    result.status = 200
                    result.headers = {"Content-Length": str(len(self.data))}
                    return result
                match = re.fullmatch(r"bytes=(\d+)-(\d*)", range_value)
                if match is None:
                    raise AssertionError("nieoczekiwany Range")
                start = int(match.group(1))
                end = min(int(match.group(2)), len(self.data) - 1) if match.group(2) else len(self.data) - 1
                part = self.data[start:end + 1]
                result = FakeUpstream(part)
                result.headers = {
                    "Content-Length": str(len(part)),
                    "Content-Range": f"bytes {start}-{end}/{len(self.data)}",
                }
                return result

        upstream = ByteRangeOpener(generated)
        with audio.RangeProxy(audio.resolve_bts(payload()), opener=upstream) as proxy:
            decoded = subprocess.run(
                [executable, "-v", "error", "-i", proxy.local_url,
                 "-f", "null", "-"],
                capture_output=True, timeout=15,
            )
        self.assertEqual(decoded.returncode, 0, decoded.stderr.decode(errors="replace"))
        self.assertTrue(upstream.ranges, "FFmpeg nie odczytał proxy")


if __name__ == "__main__":
    unittest.main()
