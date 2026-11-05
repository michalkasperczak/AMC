"""Optional AMC Test native TIDAL playback using the existing LiteHostClient.

All remote work runs in a daemon worker. The GUI owns the host and routes its
playback events here; this adapter owns only a private, in-memory Range proxy.
Callbacks are dispatched by the caller's UI dispatcher and contain no secrets.
"""

from __future__ import annotations

import contextlib
import logging
import threading
import time
from collections.abc import Callable
from urllib.parse import parse_qs, unquote, urlsplit

from . import tidal_bts_audio as audio
from . import tidalapi_session_store as session_store


_MESSAGES = {
    "login_required": "Wymagane jest logowanie do odtwarzania TIDAL.",
    "started": "Odtwarzanie TIDAL rozpoczęte.",
    "failed": "Nie udało się odtworzyć utworu TIDAL.",
    "ended": "Odtwarzanie TIDAL zakończone.",
    "stopped": "Odtwarzanie TIDAL zatrzymane.",
}


def _track_id(value: object) -> bool:
    return (isinstance(value, str) and 1 <= len(value) <= 18
            and value.isascii() and value.isdecimal())


def _login_code(value: object) -> str | None:
    if not isinstance(value, str) or not 3 <= len(value) <= 24:
        return None
    if not all(char.isascii() and (char.isalnum() or char == "-") for char in value):
        return None
    return value


@contextlib.contextmanager
def _quiet_library():
    """Suppress third-party request details without redirecting GUI stdout."""
    loggers = [logging.getLogger(name) for name in ("tidalapi", "requests", "urllib3")]
    levels = [logger.level for logger in loggers]
    try:
        for logger in loggers:
            logger.setLevel(logging.CRITICAL)
        yield
    finally:
        for logger, level in zip(loggers, levels):
            logger.setLevel(level)


def _direct_login_address(value: object, code: str) -> str | None:
    if not isinstance(value, str) or not code:
        return None
    candidate = value.strip()
    if not candidate or any(ord(char) < 32 for char in candidate):
        return None
    if "://" not in candidate:
        candidate = "https://" + candidate
    try:
        parsed = urlsplit(candidate)
        port = parsed.port
    except ValueError:
        return None
    if (parsed.scheme != "https" or parsed.hostname != "link.tidal.com"
            or parsed.username is not None or parsed.password is not None
            or port not in (None, 443) or parsed.fragment):
        return None
    if code not in unquote(parsed.path) and code not in parse_qs(parsed.query).get("code", []):
        return None
    return candidate


def _title(value: object) -> str:
    if not isinstance(value, str):
        return "Utwór TIDAL"
    cleaned = " ".join("".join(c for c in value if c.isprintable()).split())
    return cleaned[:200] or "Utwór TIDAL"


def _bound_network_timeouts(session: object) -> None:
    """Keep cancellation bounded even if tidalapi's requests omit a timeout."""
    request_session = getattr(session, "request_session", None)
    original = getattr(request_session, "request", None)
    if not callable(original):
        return

    def bounded_request(method, url, **kwargs):
        kwargs.setdefault("timeout", 12)
        return original(method, url, **kwargs)

    request_session.request = bounded_request


class TidalNativePlaybackAdapter:
    """One optional TIDAL playback owner; never starts or closes LiteHost."""

    def __init__(
        self,
        host_client: object,
        on_challenge: Callable[[str, str], None],
        on_status: Callable[[str, str, str], None],
        dispatch_ui: Callable[[Callable[[], None]], None],
        *,
        session_factory: Callable[[], object] | None = None,
        credential_store_factory: Callable[[], object] = session_store.WindowsCredentialStore,
        proxy_factory: Callable[..., object] = audio.RangeProxy,
    ) -> None:
        self._host = host_client
        self._on_challenge = on_challenge
        self._on_status = on_status
        self._dispatch_ui = dispatch_ui
        self._session_factory = session_factory
        self._store_factory = credential_store_factory
        self._proxy_factory = proxy_factory
        self._lock = threading.Lock()
        self._closed = False
        self._pending = False
        self._pending_track_id: str | None = None
        self._generation = 0
        self._cancel = threading.Event()
        self._proxy: object | None = None
        self._track_id: str | None = None
        self._retired_proxy: object | None = None
        self._retired_track_id: str | None = None
        self._has_saved_session: bool | None = None

    @property
    def is_active(self) -> bool:
        with self._lock:
            return self._pending or self._proxy is not None or self._retired_proxy is not None

    @property
    def has_saved_session(self) -> bool | None:
        with self._lock:
            return self._has_saved_session

    def _dispatch(self, callback: Callable[[], None]) -> None:
        def safe_callback() -> None:
            try:
                callback()
            except Exception:
                pass

        try:
            self._dispatch_ui(safe_callback)
        except Exception:
            pass

    def _status(self, kind: str, track_id: str) -> None:
        message = _MESSAGES[kind]
        self._dispatch(lambda: self._on_status(kind, track_id, message))

    def _current(self, generation: int) -> bool:
        with self._lock:
            return not self._closed and generation == self._generation and not self._cancel.is_set()

    def play(self, track_id: str, title: str = "") -> bool:
        """Start preparation immediately; result arrives through status/event callbacks."""
        if not _track_id(track_id):
            self._status("failed", str(track_id))
            return False
        with self._lock:
            if (self._closed or self._pending or self._retired_proxy is not None
                    or (self._proxy is not None and self._track_id == track_id)):
                return False
            self._generation += 1
            generation = self._generation
            self._cancel = threading.Event()
            self._pending = True
            self._pending_track_id = track_id
        threading.Thread(
            target=self._prepare, args=(generation, track_id, title),
            name="AMC TIDAL playback", daemon=True,
        ).start()
        return True

    def _login(self, session: object, generation: int, track_id: str) -> bool:
        with _quiet_library():
            challenge = session.get_link_login()
        code = _login_code(getattr(challenge, "user_code", None))
        address = _direct_login_address(
            getattr(challenge, "verification_uri_complete", None), code or ""
        )
        if code is None or address is None or not self._current(generation):
            return False
        self._status("login_required", track_id)
        self._dispatch(
            lambda: self._on_challenge(address, code) if self._current(generation) else None
        )
        expiry = getattr(challenge, "expires_in", None)
        if not isinstance(expiry, (int, float)) or not 0 < expiry <= 3600:
            return False
        deadline = time.monotonic() + expiry
        while self._current(generation) and time.monotonic() < deadline:
            try:
                with _quiet_library():
                    if session.process_link_login(challenge, until_expiry=False):
                        return True
            except TimeoutError:
                continue
        return False

    def _prepare(self, generation: int, track_id: str, title: str) -> None:
        proxy = None
        previous_proxy = None
        failed = False
        try:
            with _quiet_library():
                if self._session_factory is None:
                    import tidalapi

                    session_factory = tidalapi.Session
                else:
                    session_factory = self._session_factory
                session = session_factory()
                _bound_network_timeouts(session)
                store = self._store_factory()
                restored, previous = session_store.restore_session(session, store)
            with self._lock:
                if generation == self._generation:
                    self._has_saved_session = restored
            if not self._current(generation):
                return
            if not restored:
                if not self._login(session, generation, track_id):
                    raise RuntimeError("login_failed")
                if not self._current(generation):
                    return
                with _quiet_library():
                    previous = session_store.save_if_changed(session, store, previous)
                with self._lock:
                    if generation == self._generation:
                        self._has_saved_session = True
            with _quiet_library():
                track = session.track(track_id)
                safe_title = _title(title or getattr(track, "title", None))
                duration = getattr(track, "duration", None)
                response = session.request.basic_request(
                    "GET", "tracks/" + track_id + "/playbackinfopostpaywall",
                    {"playbackmode": "STREAM", "audioquality": "LOSSLESS",
                     "assetpresentation": "FULL"},
                )
                # basic_request may have refreshed the access token.
                session_store.save_if_changed(session, store, previous)
                if not 200 <= response.status_code < 300:
                    raise RuntimeError("manifest_unavailable")
                payload = response.json()
                plan = audio.resolve_bts(payload, duration_seconds=duration)
            if plan.duration_seconds is None or not self._current(generation):
                raise RuntimeError("duration_unavailable")
            proxy = self._proxy_factory(plan)
            with _quiet_library():
                proxy.__enter__()
            if not self._current(generation):
                return
            with self._lock:
                if generation != self._generation or self._closed:
                    return
                previous_proxy = self._proxy
                previous_track = self._track_id
                self._retired_proxy = previous_proxy
                self._retired_track_id = previous_track
                self._proxy = proxy
                self._track_id = track_id
            with _quiet_library():
                accepted = self._host.call(
                    "tidal.nativePlay",
                    {"id": track_id, "title": safe_title,
                     "proxyUrl": proxy.local_url,
                     "durationSeconds": plan.duration_seconds},
                    timeout=30,
                )
            if not isinstance(accepted, dict) or accepted.get("ok") is not True \
                    or accepted.get("id") != "tidal:" + track_id:
                raise RuntimeError("host_rejected")
            proxy = None  # Active owner releases it on stop/end/failure.
        except Exception:
            # Exceptions from the library, host or proxy may contain signed URLs.
            failed = self._current(generation)
            with self._lock:
                if self._proxy is proxy and self._track_id == track_id:
                    self._proxy = previous_proxy
                    self._track_id = previous_track if previous_proxy is not None else None
                    if self._retired_proxy is previous_proxy:
                        self._retired_proxy = None
                        self._retired_track_id = None
        finally:
            if proxy is not None:
                self._dispose_proxy(proxy)
            with self._lock:
                if generation == self._generation:
                    self._pending = False
                    self._pending_track_id = None
            if failed and self._current(generation):
                self._status("failed", track_id)

    @staticmethod
    def _dispose_proxy(proxy: object) -> None:
        def close() -> None:
            try:
                proxy.__exit__(None, None, None)
            except Exception:
                pass

        threading.Thread(target=close, name="AMC TIDAL proxy close", daemon=True).start()

    def handle_host_event(self, name: str, data: object) -> None:
        if not isinstance(data, dict) or data.get("sessionId") != "tidal":
            return
        with self._lock:
            track_id = self._track_id
            event_id = data.get("id")
            extra_proxy = None
            if track_id is None or event_id != "tidal:" + track_id:
                if (self._retired_track_id is not None
                        and event_id == "tidal:" + self._retired_track_id
                        and name in ("playback.failed", "playback.ended")):
                    proxy = self._retired_proxy
                    self._retired_proxy = None
                    self._retired_track_id = None
                else:
                    return
                kind = None
            elif name == "playback.started":
                proxy = self._retired_proxy
                self._retired_proxy = None
                self._retired_track_id = None
                kind = "started"
            elif name == "playback.failed":
                proxy = self._proxy
                self._proxy = self._retired_proxy
                self._track_id = self._retired_track_id
                self._retired_proxy = None
                self._retired_track_id = None
                kind = "failed"
            elif name == "playback.ended":
                proxy = self._proxy
                self._proxy = None
                self._track_id = None
                extra_proxy = self._retired_proxy
                self._retired_proxy = None
                self._retired_track_id = None
                kind = "ended"
            else:
                return
        if kind is not None:
            self._status(kind, track_id)
        if proxy is not None:
            self._dispose_proxy(proxy)
        if extra_proxy is not None:
            self._dispose_proxy(extra_proxy)

    def stop(self) -> None:
        with self._lock:
            self._generation += 1
            self._cancel.set()
            track_id = self._track_id
            pending_track_id = self._pending_track_id
            self._pending = False
            self._pending_track_id = None
            proxy = self._proxy
            self._proxy = None
            self._track_id = None
            retired_proxy = self._retired_proxy
            self._retired_proxy = None
            self._retired_track_id = None
        if proxy is not None:
            self._dispose_proxy(proxy)
        if retired_proxy is not None:
            self._dispose_proxy(retired_proxy)
        if track_id is not None:
            self._status("stopped", track_id)
        if pending_track_id is not None and pending_track_id != track_id:
            self._status("stopped", pending_track_id)

    def close(self) -> None:
        self.stop()
        with self._lock:
            self._closed = True
