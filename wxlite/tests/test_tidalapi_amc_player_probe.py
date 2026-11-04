"""Offline one-shot AMC player contract tests; no WASAPI, login or live host."""

from __future__ import annotations

import base64
import datetime as dt
import json
import sys
import types
import unittest
from pathlib import Path
from types import SimpleNamespace
from unittest.mock import patch


TOOLS = Path(__file__).resolve().parents[1] / "tools"
sys.path.insert(0, str(TOOLS))
import tidalapi_amc_player_probe as pilot  # noqa: E402
import tidalapi_session_store as session_store  # noqa: E402


SECRET = "never-print-signed-audio-url-or-token"
SIGNED_URL = "https://sp-pr-fa.audio.tidal.com/audio.flac?signature=" + SECRET


def full_bts(*, encryption: str = "NONE", mime: str = "audio/flac",
             codec: str = "flac") -> dict:
    manifest = {
        "mimeType": mime,
        "codecs": codec,
        "encryptionType": encryption,
        "urls": [SIGNED_URL],
    }
    return {
        "assetPresentation": "FULL",
        "manifestMimeType": "application/vnd.tidal.bts",
        "manifest": base64.b64encode(json.dumps(manifest).encode()).decode(),
    }


class FakeSession:
    def __init__(self, manifest=None, *, duration=180,
                 complete_link="link.tidal.com/activate?code=ABCD-1234"):
        self.manifest = manifest if manifest is not None else full_bts()
        self.duration = duration
        self.complete_link = complete_link
        self.request = SimpleNamespace(basic_request=self.basic_request)
        self.requested_quality = None
        self.token_type = None
        self.access_token = None
        self.refresh_token = None
        self.expiry_time = None
        self.login_calls = 0
        self.load_calls = 0
        self.load_ok = True
        self.refresh_on_manifest = False
        self.login_ok = True
        self.load_error = None

    def get_link_login(self):
        self.login_calls += 1
        return SimpleNamespace(
            verification_uri="link.tidal.com",
            verification_uri_complete=self.complete_link,
            user_code="ABCD-1234",
        )

    def process_link_login(self, challenge):
        self.token_type = "Bearer"
        self.access_token = "access-" + SECRET
        self.refresh_token = "refresh-" + SECRET
        self.expiry_time = dt.datetime(2030, 1, 1)
        return self.login_ok

    def load_oauth_session(self, token_type, access_token, refresh_token,
                           expiry_time, *, is_pkce):
        self.load_calls += 1
        if self.load_error is not None:
            raise self.load_error
        self.token_type = token_type
        self.access_token = access_token
        self.refresh_token = refresh_token
        self.expiry_time = expiry_time
        return self.load_ok

    def track(self, track_id):
        return SimpleNamespace(title="  Utwór próby  ", duration=self.duration)

    def basic_request(self, method, path, params):
        self.requested_quality = params["audioquality"]
        if self.refresh_on_manifest:
            self.access_token = "refreshed-" + SECRET
        return SimpleNamespace(status_code=200, json=lambda: self.manifest)


class FakeProxy:
    instances: list[FakeProxy] = []

    def __init__(self, plan):
        self.plan = plan
        self.closed = False
        self.entered = False
        self.instances.append(self)

    def __enter__(self):
        self.entered = True
        return self

    def __exit__(self, exc_type, exc, traceback):
        self.closed = True

    @property
    def local_url(self):
        return "http://127.0.0.1:55555/audio/test-capability-1234567890123456"


class FakeCredentialStore:
    def __init__(self, data=None):
        self.data = data
        self.writes = []

    def read(self):
        return self.data

    def write(self, data):
        self.data = data
        self.writes.append(data)


def saved_credential():
    source = FakeSession()
    source.process_link_login(None)
    store = FakeCredentialStore()
    session_store.save_if_changed(source, store, None)
    return store


class FakeHost:
    instances: list[FakeHost] = []
    event_name = "playback.started"
    raise_on_play = False
    positions = [5, 10, 15, 20, 25, 31, 36, 41, 46]
    fail_after_first_status = False

    def __init__(self, executable, **kwargs):
        self.executable = executable
        self.kwargs = kwargs
        self.started = False
        self.closed = False
        self.calls = []
        self.status_calls = 0
        self.instances.append(self)

    def start(self):
        self.started = True

    def call(self, op, args, *, timeout):
        self.calls.append((op, args, timeout))
        if op == "tidal.nativePlay":
            if self.raise_on_play:
                raise RuntimeError(SECRET + SIGNED_URL)
            self.kwargs["on_event"](
                self.event_name,
                {"sessionId": "tidal", "id": "tidal:123", "message": SECRET},
            )
            return {
                "ok": True,
                "engine": "files",
                "sessionId": "tidal",
                "id": "tidal:123",
                "preparing": True,
            }
        if op == "transport.status":
            position = self.positions[min(self.status_calls, len(self.positions) - 1)]
            self.status_calls += 1
            if self.fail_after_first_status and self.status_calls == 1:
                self.kwargs["on_event"](
                    "playback.failed",
                    {"sessionId": "tidal", "id": "tidal:123", "message": SECRET},
                )
            return {
                "sessionId": "tidal", "id": "tidal:123", "playing": True,
                "positionSeconds": position,
            }
        return {"ok": True}

    def close(self):
        self.closed = True


class PlayerPilotTests(unittest.TestCase):
    def setUp(self):
        FakeProxy.instances.clear()
        FakeHost.instances.clear()
        FakeHost.event_name = "playback.started"
        FakeHost.raise_on_play = False
        FakeHost.positions = [5, 10, 15, 20, 25, 31, 36, 41, 46]
        FakeHost.fail_after_first_status = False

    def invoke(self, session=None, sleeper=lambda _: None, store=None):
        lines: list[str] = []
        session = session or FakeSession()
        store = store or FakeCredentialStore()
        result = pilot.run(
            "123", "fake-host.exe", "fake-state.json",
            session_factory=lambda: session,
            credential_store_factory=lambda: store,
            host_factory=FakeHost,
            proxy_factory=FakeProxy,
            emit=lines.append,
            sleeper=sleeper,
        )
        return result, lines, session

    def test_full_flac_starts_host_and_keeps_proxy_for_45_seconds(self):
        held: list[float] = []

        def sleep(seconds):
            self.assertTrue(FakeProxy.instances[0].entered)
            self.assertFalse(FakeProxy.instances[0].closed)
            self.assertFalse(FakeHost.instances[0].closed)
            held.append(seconds)

        result, lines, session = self.invoke(sleeper=sleep)
        self.assertEqual(result, 0)
        self.assertEqual(held, [5.0] * 9)
        self.assertEqual(session.requested_quality, "LOSSLESS")
        self.assertEqual(lines[:2], [
            "Adres logowania: https://link.tidal.com/activate?code=ABCD-1234",
            "Kod jednorazowy: ABCD-1234",
        ])
        self.assertIn('"status":"session_saved"', lines[2])
        self.assertIn('"status":"playback_started"', lines[3])
        self.assertIn('"status":"played_beyond_30s"', lines[4])
        self.assertIn('"seconds":46', lines[4])
        host = FakeHost.instances[0]
        self.assertEqual(host.kwargs["release_profile"], "test")
        self.assertEqual(host.calls[0][0], "tidal.nativePlay")
        self.assertEqual(host.calls[0][1], {
            "id": "123",
            "title": "Utwór próby",
            "proxyUrl": FakeProxy.instances[0].local_url,
            "durationSeconds": 180,
        })
        self.assertEqual(host.calls[-1][0], "transport.stop")
        self.assertTrue(host.closed)
        self.assertTrue(FakeProxy.instances[0].closed)
        self.assertNotIn(SECRET, "\n".join(lines))
        self.assertNotIn("test-capability", "\n".join(lines))

    def test_failed_event_stops_host_and_proxy_without_waiting(self):
        FakeHost.event_name = "playback.failed"
        held: list[float] = []
        result, lines, _ = self.invoke(sleeper=held.append)
        self.assertEqual(result, 1)
        self.assertIn('"status":"playback_failed"', lines[-1])
        self.assertEqual(held, [])
        self.assertTrue(FakeHost.instances[0].closed)
        self.assertTrue(FakeProxy.instances[0].closed)
        self.assertNotIn(SECRET, "\n".join(lines))

    def test_rejects_encrypted_manifest_before_host(self):
        result, lines, _ = self.invoke(FakeSession(full_bts(encryption="CENC")))
        self.assertEqual(result, 1)
        self.assertIn('"status":"encrypted_audio"', lines[-1])
        self.assertEqual(FakeHost.instances, [])
        self.assertEqual(FakeProxy.instances, [])

    def test_host_exception_is_redacted_and_resources_close(self):
        FakeHost.raise_on_play = True
        result, lines, _ = self.invoke()
        self.assertEqual(result, 1)
        self.assertIn('"status":"player_probe_failed"', lines[-1])
        self.assertNotIn(SECRET, "\n".join(lines))
        self.assertTrue(FakeHost.instances[0].closed)
        self.assertTrue(FakeProxy.instances[0].closed)

    def test_failure_after_start_is_not_reported_as_success(self):
        FakeHost.fail_after_first_status = True
        held: list[float] = []
        result, lines, _ = self.invoke(sleeper=held.append)
        self.assertEqual(result, 1)
        self.assertEqual(held, [5.0] * 9)
        self.assertIn('"status":"playback_failed"', lines[-1])
        self.assertNotIn("played_beyond_30s", "\n".join(lines))
        self.assertTrue(FakeHost.instances[0].closed)
        self.assertTrue(FakeProxy.instances[0].closed)

    def test_aac_bts_is_accepted_with_known_duration(self):
        manifest = full_bts(mime="audio/mp4", codec="mp4a.40.2")
        result, lines, _ = self.invoke(FakeSession(manifest))
        self.assertEqual(result, 0)
        self.assertEqual(FakeProxy.instances[0].plan.codec, "aac")
        self.assertEqual(FakeHost.instances[0].calls[0][0], "tidal.nativePlay")
        self.assertIn('"status":"played_beyond_30s"', lines[-1])

    def test_missing_duration_reports_safe_format_without_host(self):
        result, lines, _ = self.invoke(FakeSession(duration=None))
        self.assertEqual(result, 1)
        self.assertIn('"status":"duration_unavailable"', lines[-1])
        self.assertIn('"format":"flac"', lines[-1])
        self.assertIn('"durationSeconds":null', lines[-1])
        self.assertEqual(FakeHost.instances, [])
        self.assertEqual(FakeProxy.instances, [])
        self.assertNotIn(SECRET, "\n".join(lines))

    def test_direct_login_link_must_be_tidal_domain(self):
        result, lines, _ = self.invoke(FakeSession(complete_link="https://evil.example/activate?code=ABCD-1234"))
        self.assertEqual(result, 1)
        self.assertEqual(lines, ['Status: {"status":"invalid_login_challenge"}'])
        self.assertEqual(FakeHost.instances, [])

    def test_direct_login_link_requires_exact_host_and_matching_code(self):
        invalid_links = (
            "https://tidal.com/activate?code=ABCD-1234",
            "https://other.link.tidal.com/activate?code=ABCD-1234",
            "https://link.tidal.com/activate?code=WRONG",
            "https://link.tidal.com/activate?other=ABCD-1234",
            "https://user@link.tidal.com/activate?code=ABCD-1234",
            "https://link.tidal.com:444/activate?code=ABCD-1234",
            "https://link.tidal.com/activate?code=ABCD-1234#fragment",
        )
        for link in invalid_links:
            with self.subTest(link=link):
                result, lines, _ = self.invoke(FakeSession(complete_link=link))
                self.assertEqual(result, 1)
                self.assertEqual(lines, ['Status: {"status":"invalid_login_challenge"}'])
                self.assertEqual(FakeHost.instances, [])

    def test_direct_login_link_accepts_code_in_path(self):
        link = "https://link.tidal.com/activate/ABCD-1234"
        result, lines, _ = self.invoke(FakeSession(complete_link=link))
        self.assertEqual(result, 0)
        self.assertEqual(lines[0], "Adres logowania: " + link)

    def test_stored_session_reuses_login_without_device_code(self):
        store = saved_credential()
        session = FakeSession()
        result, lines, _ = self.invoke(session, store=store)
        self.assertEqual(result, 0)
        self.assertEqual(session.load_calls, 1)
        self.assertEqual(session.login_calls, 0)
        self.assertEqual(store.writes, [store.data])
        self.assertEqual(lines[0], 'Status: {"status":"session_reused"}')
        self.assertNotIn("Adres logowania:", "\n".join(lines))
        self.assertNotIn(SECRET, "\n".join(lines))

    def test_refreshed_access_token_is_saved(self):
        store = saved_credential()
        session = FakeSession()
        session.refresh_on_manifest = True
        result, lines, _ = self.invoke(session, store=store)
        self.assertEqual(result, 0)
        self.assertEqual(len(store.writes), 2)
        self.assertIn(b"refreshed-", store.data)
        self.assertNotIn(SECRET, "\n".join(lines))

    def test_invalid_saved_session_uses_device_login_and_replaces_secret(self):
        store = saved_credential()
        session = FakeSession()
        session.load_ok = False
        result, lines, _ = self.invoke(session, store=store)
        self.assertEqual(result, 0)
        self.assertEqual(session.load_calls, 1)
        self.assertEqual(session.login_calls, 1)
        self.assertEqual(len(store.writes), 2)
        self.assertIn('"status":"session_saved"', "\n".join(lines))

    def test_expired_refresh_token_uses_device_login(self):
        errors = types.ModuleType("tidalapi.exceptions")
        errors.AuthenticationError = type("AuthenticationError", (Exception,), {})
        package = types.ModuleType("tidalapi")
        package.exceptions = errors
        store = saved_credential()
        session = FakeSession()
        session.load_error = errors.AuthenticationError(SECRET)
        with patch.dict(sys.modules, {"tidalapi": package, "tidalapi.exceptions": errors}):
            result, lines, _ = self.invoke(session, store=store)
        self.assertEqual(result, 0)
        self.assertEqual(session.login_calls, 1)
        self.assertIn('"status":"session_saved"', "\n".join(lines))
        self.assertNotIn(SECRET, "\n".join(lines))

    def test_restore_network_error_does_not_ask_for_new_code(self):
        store = saved_credential()
        session = FakeSession()
        session.load_error = OSError(SECRET)
        result, lines, _ = self.invoke(session, store=store)
        self.assertEqual(result, 1)
        self.assertEqual(session.login_calls, 0)
        self.assertEqual(lines, ['Status: {"status":"session_restore_failed"}'])

    def test_malformed_saved_credential_uses_device_login(self):
        store = FakeCredentialStore(b"not valid JSON " + SECRET.encode())
        session = FakeSession()
        result, lines, _ = self.invoke(session, store=store)
        self.assertEqual(result, 0)
        self.assertEqual(session.login_calls, 1)
        self.assertEqual(len(store.writes), 1)
        self.assertNotIn(SECRET, "\n".join(lines))

    def test_failed_login_does_not_save_credentials(self):
        store = FakeCredentialStore()
        session = FakeSession()
        session.login_ok = False
        result, lines, _ = self.invoke(session, store=store)
        self.assertEqual(result, 1)
        self.assertEqual(store.writes, [])
        self.assertIn('"status":"login_failed"', lines[-1])

    def test_store_failure_does_not_start_device_login(self):
        class BrokenStore(FakeCredentialStore):
            def read(self):
                raise session_store.CredentialStoreError()

        session = FakeSession()
        result, lines, _ = self.invoke(session, store=BrokenStore())
        self.assertEqual(result, 1)
        self.assertEqual(lines, ['Status: {"status":"credential_store_unavailable"}'])
        self.assertEqual(session.login_calls, 0)

    def test_no_position_progress_is_unverified(self):
        FakeHost.positions = [0] * 9
        held: list[float] = []
        result, lines, _ = self.invoke(sleeper=held.append)
        self.assertEqual(result, 1)
        self.assertEqual(held, [5.0] * 9)
        self.assertIn('"status":"unverified_playback"', lines[-1])
        self.assertNotIn("played_beyond_30s", "\n".join(lines))

    def test_bad_track_id_does_not_begin_login(self):
        lines: list[str] = []
        result = pilot.run(
            "12/../secret", "host", "state",
            session_factory=lambda: (_ for _ in ()).throw(AssertionError("login")),
            credential_store_factory=lambda: (_ for _ in ()).throw(AssertionError("store")),
            emit=lines.append,
        )
        self.assertEqual(result, 2)
        self.assertEqual(lines, ['Status: {"status":"invalid_track"}'])


if __name__ == "__main__":
    unittest.main()
