"""Offline adapter tests: no real TIDAL, Credential Manager, GUI or host."""

from __future__ import annotations

import base64
import datetime as dt
import json
import sys
import threading
import time
import unittest
from pathlib import Path
from types import SimpleNamespace


sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from amc_wx_lite.tidal_native_playback import TidalNativePlaybackAdapter  # noqa: E402
from tools import tidalapi_session_store as session_store  # noqa: E402


SECRET = "secret-signed-audio-or-token"


def max_payload(*, media_host="https://sp-ad-fa.audio.tidal.com"):
    manifest = f'''<MPD xmlns="urn:mpeg:dash:schema:mpd:2011" type="static">
      <Period><AdaptationSet mimeType="audio/mp4">
      <Representation id="1" codecs="flac" audioSamplingRate="192000" bandwidth="5000000">
      <SegmentTemplate timescale="192000" startNumber="1"
        initialization="https://sp-ad-fa.audio.tidal.com/init.mp4?sig=private"
        media="{media_host}/seg-$Number$.mp4?sig=private">
      <SegmentTimeline><S d="768000" r="2"/></SegmentTimeline>
      </SegmentTemplate></Representation></AdaptationSet></Period></MPD>'''
    return {"assetPresentation": "FULL", "audioQuality": "HI_RES_LOSSLESS",
            "bitDepth": 24, "sampleRate": 192000,
            "manifestMimeType": "application/dash+xml",
            "manifest": base64.b64encode(manifest.encode()).decode()}


class FakeStore:
    def __init__(self, data=None):
        self.data = data
        self.writes = []

    def read(self):
        return self.data

    def write(self, data):
        self.data = data
        self.writes.append(data)


class FakeSession:
    def __init__(self):
        self.token_type = "Bearer"
        self.access_token = "access-" + SECRET
        self.refresh_token = "refresh-" + SECRET
        self.expiry_time = dt.datetime(2030, 1, 1)
        self.login_calls = 0
        self.last_quality = None
        self.load_calls = 0
        self.worker_thread = None
        self.login_gate = None
        self.request = SimpleNamespace(basic_request=self.basic_request)

    def load_oauth_session(self, *args, **kwargs):
        self.load_calls += 1
        self.worker_thread = threading.current_thread()
        return True

    def get_link_login(self):
        self.login_calls += 1
        return SimpleNamespace(
            user_code="ABCD-1234",
            verification_uri_complete="https://link.tidal.com/activate?code=ABCD-1234",
            expires_in=120,
        )

    def process_link_login(self, challenge, *, until_expiry):
        if self.login_gate is not None:
            self.login_gate.wait(timeout=2)
        return True

    def track(self, track_id):
        return SimpleNamespace(title=" Utwór testowy ", duration=180)

    def basic_request(self, method, path, params):
        self.last_quality = params["audioquality"]
        manifest = {
            "mimeType": "audio/flac", "codecs": "flac",
            "encryptionType": "NONE",
            "urls": ["https://sp-pr-fa.audio.tidal.com/a.flac?sig=" + SECRET],
        }
        payload = {
            "assetPresentation": "FULL",
            "manifestMimeType": "application/vnd.tidal.bts",
            "manifest": base64.b64encode(json.dumps(manifest).encode()).decode(),
        }
        return SimpleNamespace(status_code=200, json=lambda: payload)


class FakeProxy:
    instances = []

    def __init__(self, plan):
        self.plan = plan
        self.entered = False
        self.closed = threading.Event()
        self.bitrate_callback = None
        self.instances.append(self)

    def __enter__(self):
        self.entered = True
        return self

    def set_bitrate_callback(self, callback):
        self.bitrate_callback = callback

    def __exit__(self, *_):
        self.closed.set()

    @property
    def local_url(self):
        return "http://127.0.0.1:55555/audio/private-capability"


class FakeHost:
    def __init__(self):
        self.calls = []
        self.called = threading.Event()
        self.accept = True

    def call(self, op, args, *, timeout):
        self.calls.append((op, args, timeout))
        self.called.set()
        return {"ok": self.accept, "id": "tidal:" + args["id"]}


def saved_store():
    store = FakeStore()
    session_store.save_if_changed(FakeSession(), store, None)
    return store


class AdapterTests(unittest.TestCase):
    def setUp(self):
        FakeProxy.instances.clear()
        self.host = FakeHost()
        self.session = FakeSession()
        self.challenges = []
        self.statuses = []
        self.dispatched = []

    def adapter(self, store):
        def dispatch(callback):
            self.dispatched.append(threading.current_thread())
            callback()

        return TidalNativePlaybackAdapter(
            self.host, lambda url, code: self.challenges.append((url, code)),
            lambda kind, track_id, message: self.statuses.append((kind, track_id, message)),
            dispatch, session_factory=lambda: self.session,
            credential_store_factory=lambda: store, proxy_factory=FakeProxy,
        )

    def wait_for(self, predicate):
        deadline = time.monotonic() + 3
        while time.monotonic() < deadline:
            if predicate():
                return
            time.sleep(0.01)
        self.fail("worker did not complete")

    def test_saved_session_plays_via_existing_host_and_proxy_lasts_until_end(self):
        store = saved_store()
        adapter = self.adapter(store)
        self.assertTrue(adapter.play("123", ""))
        self.assertTrue(self.host.called.wait(3))
        self.assertEqual(self.session.load_calls, 1)
        self.assertEqual(self.session.last_quality, "HI_RES_LOSSLESS")
        self.assertEqual(self.session.login_calls, 0)
        self.assertEqual(self.challenges, [])
        self.assertTrue(adapter.is_active)
        proxy = FakeProxy.instances[0]
        self.assertFalse(proxy.closed.is_set())
        self.assertEqual(self.host.calls[0][0], "tidal.nativePlay")
        self.assertEqual(self.host.calls[0][1]["title"], "Utwór testowy")
        self.assertEqual(self.host.calls[0][1]["proxyUrl"], proxy.local_url)
        self.assertNotIn("preparing", [item[0] for item in self.statuses])
        adapter.handle_host_event("playback.started", {"sessionId": "tidal", "id": "tidal:123"})
        self.assertIn("started", [item[0] for item in self.statuses])
        adapter.handle_host_event("playback.ended", {"sessionId": "tidal", "id": "tidal:123"})
        self.assertTrue(proxy.closed.wait(3))
        self.wait_for(lambda: not adapter.is_active)
        self.assertEqual([item[0] for item in self.statuses][-1], "ended")
        self.assertNotIn(SECRET, str(self.statuses) + str(self.challenges))
        adapter.close()

    def test_compressed_bitrate_reaches_host_without_restarting_audio(self):
        adapter = self.adapter(saved_store())
        self.assertTrue(adapter.play("123"))
        self.assertTrue(self.host.called.wait(3))
        proxy = FakeProxy.instances[0]
        self.assertIsNotNone(proxy.bitrate_callback)
        proxy.bitrate_callback(928)
        self.wait_for(lambda: len(self.host.calls) == 2)
        self.assertEqual(self.host.calls[1][0], "tidal.nativeBitrate")
        self.assertEqual(self.host.calls[1][1], {"id": "123", "bitrateKbps": 928})
        self.assertFalse(proxy.closed.is_set())
        adapter.stop()
        adapter.close()

    def test_max_manifest_is_selected_and_announced_after_host_starts(self):
        self.session.request.basic_request = lambda method, path, params: SimpleNamespace(
            status_code=200, json=lambda: max_payload(),
        )
        adapter = self.adapter(saved_store())
        self.assertTrue(adapter.play("123"))
        self.assertTrue(self.host.called.wait(3))
        self.assertEqual(FakeProxy.instances[0].plan.bit_depth, 24)
        self.assertEqual(FakeProxy.instances[0].plan.sample_rate, 192000)
        self.assertEqual(self.host.calls[0][1].get("bitrateKbps"), 5000)
        adapter.handle_host_event("playback.started", {"sessionId": "tidal", "id": "tidal:123"})
        self.assertTrue(any("Jakość Max, FLAC 24 bit, 192 kHz" in message
                            for _, _, message in self.statuses))
        self.assertNotIn(SECRET, str(self.statuses))
        adapter.stop()
        self.assertTrue(FakeProxy.instances[0].closed.wait(3))
        adapter.close()

    def test_unsupported_max_manifest_falls_back_to_high_and_says_so(self):
        requests = []

        def request(method, path, params):
            quality = params["audioquality"]
            requests.append(quality)
            if quality == "HI_RES_LOSSLESS":
                return SimpleNamespace(
                    status_code=200,
                    json=lambda: max_payload(media_host="https://evil.example"),
                )
            original = self.session.basic_request(method, path, params)
            data = original.json()
            data["audioQuality"] = "LOSSLESS"
            data["bitDepth"] = 16
            data["sampleRate"] = 44100
            return SimpleNamespace(status_code=200, json=lambda: data)

        self.session.request.basic_request = request
        adapter = self.adapter(saved_store())
        self.assertTrue(adapter.play("123"))
        self.assertTrue(self.host.called.wait(3))
        self.assertEqual(requests, ["HI_RES_LOSSLESS", "LOSSLESS"])
        adapter.handle_host_event("playback.started", {"sessionId": "tidal", "id": "tidal:123"})
        self.assertTrue(any("Jakość High, FLAC 16 bit, 44,1 kHz" in message
                            for _, _, message in self.statuses))
        adapter.stop()
        self.assertTrue(FakeProxy.instances[0].closed.wait(3))
        adapter.close()

    def test_missing_session_displays_direct_challenge_and_saves_once(self):
        store = FakeStore()
        adapter = self.adapter(store)
        self.assertTrue(adapter.play("123"))
        self.assertTrue(self.host.called.wait(3))
        self.assertEqual(self.session.login_calls, 1)
        self.assertEqual(self.challenges, [
            ("https://link.tidal.com/activate?code=ABCD-1234", "ABCD-1234")
        ])
        self.assertEqual(len(store.writes), 1)
        self.assertTrue(adapter.has_saved_session)
        adapter.stop()
        self.assertTrue(FakeProxy.instances[0].closed.wait(3))
        self.assertFalse(adapter.is_active)
        self.assertNotIn(SECRET, str(self.statuses) + str(self.challenges))
        adapter.close()

    def test_stop_cancels_pending_login_without_host_play(self):
        gate = threading.Event()
        self.session.login_gate = gate
        adapter = self.adapter(FakeStore())
        self.assertTrue(adapter.play("123"))
        self.wait_for(lambda: bool(self.challenges))
        adapter.stop()
        gate.set()
        self.wait_for(lambda: not adapter.is_active)
        time.sleep(0.05)
        self.assertEqual([item[0] for item in self.statuses][-1], "stopped")
        self.assertEqual(self.host.calls, [])
        self.assertEqual(FakeProxy.instances, [])
        adapter.close()

    def test_host_rejection_closes_new_proxy_and_reports_safe_failure(self):
        self.host.accept = False
        adapter = self.adapter(saved_store())
        self.assertTrue(adapter.play("123"))
        self.assertTrue(self.host.called.wait(3))
        self.wait_for(lambda: any(item[0] == "failed" for item in self.statuses))
        self.assertTrue(FakeProxy.instances[0].closed.wait(3))
        self.assertFalse(adapter.is_active)
        self.assertNotIn(SECRET, str(self.statuses) + str(self.challenges))
        adapter.close()

    def test_wrong_host_event_cannot_close_proxy(self):
        adapter = self.adapter(saved_store())
        self.assertTrue(adapter.play("123"))
        self.assertTrue(self.host.called.wait(3))
        adapter.handle_host_event("playback.ended", {"sessionId": "tidal", "id": "tidal:456"})
        self.assertFalse(FakeProxy.instances[0].closed.is_set())
        adapter.stop()
        self.assertTrue(FakeProxy.instances[0].closed.wait(3))
        adapter.close()

    def test_previous_proxy_stays_live_until_next_track_started(self):
        adapter = self.adapter(saved_store())
        self.assertTrue(adapter.play("123"))
        self.assertTrue(self.host.called.wait(3))
        self.wait_for(lambda: not adapter._pending)
        first = FakeProxy.instances[0]
        adapter.handle_host_event("playback.started", {"sessionId": "tidal", "id": "tidal:123"})
        self.host.called.clear()
        self.assertTrue(adapter.play("456"))
        self.assertTrue(self.host.called.wait(3))
        self.wait_for(lambda: not adapter._pending)
        self.assertFalse(first.closed.is_set())
        self.assertFalse(adapter.play("789"))
        adapter.handle_host_event("playback.started", {"sessionId": "tidal", "id": "tidal:456"})
        self.assertTrue(first.closed.wait(3))
        self.assertFalse(FakeProxy.instances[1].closed.is_set())
        adapter.stop()
        self.assertTrue(FakeProxy.instances[1].closed.wait(3))
        adapter.close()

    def test_new_track_failure_preserves_previous_proxy(self):
        adapter = self.adapter(saved_store())
        self.assertTrue(adapter.play("123"))
        self.assertTrue(self.host.called.wait(3))
        self.wait_for(lambda: not adapter._pending)
        first = FakeProxy.instances[0]
        adapter.handle_host_event("playback.started", {"sessionId": "tidal", "id": "tidal:123"})
        self.host.called.clear()
        self.assertTrue(adapter.play("456"))
        self.assertTrue(self.host.called.wait(3))
        self.wait_for(lambda: not adapter._pending)
        adapter.handle_host_event("playback.failed", {"sessionId": "tidal", "id": "tidal:456"})
        self.assertTrue(FakeProxy.instances[1].closed.wait(3))
        self.assertFalse(first.closed.is_set())
        self.assertTrue(adapter.is_active)
        adapter.stop()
        self.assertTrue(first.closed.wait(3))
        adapter.close()


if __name__ == "__main__":
    unittest.main()
