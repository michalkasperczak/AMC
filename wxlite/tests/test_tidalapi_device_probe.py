"""Offline contract tests for the optional TIDAL device-login probe."""

from __future__ import annotations

import importlib.util
import io
import unittest
from contextlib import redirect_stderr, redirect_stdout
from pathlib import Path
from types import SimpleNamespace


TOOL = Path(__file__).resolve().parents[1] / "tools" / "tidalapi_device_probe.py"
spec = importlib.util.spec_from_file_location("tidalapi_device_probe", TOOL)
assert spec is not None and spec.loader is not None
probe = importlib.util.module_from_spec(spec)
spec.loader.exec_module(probe)

SECRET = "private-token-or-audio-url-must-not-leak"


class FakeResponse:
    status_code = 200

    def __init__(self, payload: object, status_code: int = 200):
        self.payload = payload
        self.status_code = status_code

    def json(self) -> object:
        return self.payload


class NonJsonResponse(FakeResponse):
    def json(self) -> object:
        raise ValueError(SECRET)


class FakeSession:
    def __init__(self, response: FakeResponse, *, challenge_host: str = "link.tidal.com"):
        self.response = response
        self.challenge_host = challenge_host
        self.request = SimpleNamespace(basic_request=self._request)
        self.saved = False

    def get_link_login(self) -> object:
        print(SECRET)
        return SimpleNamespace(
            verification_uri=self.challenge_host,
            user_code="ABCD-1234",
        )

    def process_link_login(self, login: object) -> bool:
        print(SECRET)
        return True

    def save_session_to_file(self, path: object) -> None:
        self.saved = True
        raise AssertionError("sesja nie może być zapisana")

    def _request(self, method: str, path: str, params: dict) -> FakeResponse:
        assert method == "GET" and path == "tracks/123/playbackinfopostpaywall"
        assert params["assetpresentation"] == "FULL"
        return self.response


class DeviceProbeTests(unittest.TestCase):
    def call(self, session: FakeSession) -> tuple[int, list[str], str]:
        lines: list[str] = []
        capture = io.StringIO()
        with redirect_stdout(capture), redirect_stderr(capture):
            code = probe.run("123", session_factory=lambda: session, emit=lines.append)
        return code, lines, capture.getvalue()

    def test_full_manifest_only_reports_safe_status(self) -> None:
        import base64

        session = FakeSession(FakeResponse({
            "assetPresentation": "FULL",
            "manifestMimeType": "application/vnd.tidal.bts",
            "manifest": base64.b64encode(SECRET.encode()).decode(),
            "privateUrl": SECRET,
        }))
        code, lines, captured = self.call(session)
        self.assertEqual(code, 0)
        self.assertEqual(lines[:2], [
            "Adres logowania: https://link.tidal.com",
            "Kod jednorazowy: ABCD-1234",
        ])
        self.assertIn('"fullManifestAvailable":true', lines[2])
        self.assertNotIn(SECRET, "\n".join(lines) + captured)
        self.assertFalse(session.saved)

    def test_preview_is_not_full(self) -> None:
        session = FakeSession(FakeResponse({"assetPresentation": "PREVIEW"}))
        code, lines, _ = self.call(session)
        self.assertEqual(code, 1)
        self.assertIn('"status":"preview_only"', lines[-1])

    def test_denial_does_not_print_response(self) -> None:
        session = FakeSession(FakeResponse({"error": SECRET}, 403))
        code, lines, captured = self.call(session)
        self.assertEqual(code, 1)
        self.assertIn('"status":"access_denied"', lines[-1])
        self.assertNotIn(SECRET, "\n".join(lines) + captured)

    def test_denial_without_json_remains_access_denied(self) -> None:
        session = FakeSession(NonJsonResponse(None, 403))
        code, lines, captured = self.call(session)
        self.assertEqual(code, 1)
        self.assertIn('"status":"access_denied"', lines[-1])
        self.assertNotIn(SECRET, "\n".join(lines) + captured)

    def test_success_without_json_is_invalid_response(self) -> None:
        session = FakeSession(NonJsonResponse(None, 200))
        code, lines, captured = self.call(session)
        self.assertEqual(code, 1)
        self.assertIn('"status":"invalid_response"', lines[-1])
        self.assertNotIn(SECRET, "\n".join(lines) + captured)

    def test_bad_host_is_rejected_before_printing_code(self) -> None:
        session = FakeSession(FakeResponse({}), challenge_host="evil.example")
        code, lines, _ = self.call(session)
        self.assertEqual(code, 1)
        self.assertEqual(lines, ['Status: {"status":"invalid_login_challenge"}'])

    def test_bad_track_id_does_not_start_login(self) -> None:
        lines: list[str] = []
        code = probe.run("123/../secret", session_factory=lambda: None, emit=lines.append)
        self.assertEqual(code, 2)
        self.assertEqual(lines, ['Status: {"status":"invalid_track"}'])

    def test_login_address_discards_query_and_fragment(self) -> None:
        self.assertEqual(
            probe._login_address("https://link.tidal.com/login?token=" + SECRET + "#x"),
            "https://link.tidal.com/login",
        )


if __name__ == "__main__":
    unittest.main()
