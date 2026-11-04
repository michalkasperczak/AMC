"""One-shot private TIDAL-to-AMC Test player pilot.

This script must be started manually with a separate test LiteHost executable.
It does not open a browser or save login credentials. Only a TIDAL login link,
one-time code and fixed status messages reach stdout. A volatile loopback
capability is passed to LiteHost over its private stdin protocol, never printed.
"""

from __future__ import annotations

import json
import math
import queue
import sys
import time
from collections.abc import Callable
from pathlib import Path
from urllib.parse import parse_qs, unquote, urlsplit

import tidal_bts_audio as audio
import tidalapi_device_probe as login_probe
import tidalapi_session_store as session_store


def _host_factory():
    wxlite = str(Path(__file__).resolve().parents[1])
    if wxlite not in sys.path:
        sys.path.insert(0, wxlite)
    from amc_wx_lite.host_client import LiteHostClient

    return LiteHostClient


def _title(value: object) -> str:
    if not isinstance(value, str):
        return "Utwór TIDAL"
    cleaned = " ".join("".join(char for char in value if char.isprintable()).split())
    return cleaned[:200] or "Utwór TIDAL"


def _direct_login_address(value: object, user_code: str) -> str | None:
    """Preserve the exact challenge link, including its code-bearing query."""
    if not isinstance(value, str):
        return None
    candidate = value.strip()
    if not candidate or any(ord(char) < 32 for char in candidate):
        return None
    if "://" not in candidate:
        candidate = "https://" + candidate
    try:
        parsed = urlsplit(candidate)
        host = parsed.hostname or ""
        port = parsed.port
    except ValueError:
        return None
    if (parsed.scheme != "https" or host != "link.tidal.com"
            or parsed.username is not None or parsed.password is not None
            or port not in (None, 443) or parsed.fragment):
        return None
    path_has_code = user_code in unquote(parsed.path)
    query_has_code = user_code in parse_qs(parsed.query).get("code", [])
    if not (path_has_code or query_has_code):
        return None
    return candidate


def _event_for_track(name: str, data: object, track_id: str) -> str | None:
    if not isinstance(data, dict) or data.get("sessionId") != "tidal":
        return None
    expected_id = "tidal:" + track_id
    event_id = data.get("id")
    if event_id is not None and event_id != expected_id:
        return None
    if name == "playback.started" and event_id == expected_id:
        return "started"
    if name == "playback.failed":
        return "failed"
    return None


def _wait_for_start(
    events: queue.Queue[tuple[str, object]], track_id: str, timeout: float
) -> str:
    deadline = time.monotonic() + timeout
    while True:
        remaining = deadline - time.monotonic()
        if remaining <= 0:
            return "start_timeout"
        try:
            name, data = events.get(timeout=remaining)
        except queue.Empty:
            return "start_timeout"
        event = _event_for_track(name, data, track_id)
        if event == "started":
            return "started"
        if event == "failed":
            return "playback_failed"


def _status(code: str, *, seconds: int | None = None) -> str:
    body: dict[str, object] = {"status": code}
    if seconds is not None:
        body["seconds"] = seconds
    return "Status: " + json.dumps(body, separators=(",", ":"))


def _observe_45_seconds(
    host: object,
    events: queue.Queue[tuple[str, object]],
    track_id: str,
    sleeper: Callable[[float], None],
) -> tuple[str, int | None]:
    """Hold the proxy for 45 s and require measured, playing position >=31 s."""
    maximum_position = 0.0
    terminal_event: str | None = None
    for _ in range(9):
        sleeper(5.0)
        while True:
            try:
                name, data = events.get_nowait()
            except queue.Empty:
                break
            if name == "playback.ended" and isinstance(data, dict) and (
                data.get("sessionId") == "tidal"
                and data.get("id") == "tidal:" + track_id
            ):
                terminal_event = "playback_ended"
            elif _event_for_track(name, data, track_id) == "failed":
                terminal_event = "playback_failed"
        if terminal_event is not None:
            continue
        try:
            with login_probe._quiet_library():
                status = host.call("transport.status", {"sessionId": "tidal"}, timeout=5)
        except Exception:
            continue
        if not isinstance(status, dict) or status.get("sessionId") != "tidal":
            continue
        if status.get("id") != "tidal:" + track_id or status.get("playing") is not True:
            continue
        position = status.get("positionSeconds")
        if isinstance(position, (int, float)) and not isinstance(position, bool) \
                and math.isfinite(position) and position >= 0:
            maximum_position = max(maximum_position, float(position))
    if terminal_event == "playback_failed":
        return "playback_failed", None
    if maximum_position >= 31:
        return "played_beyond_30s", int(maximum_position)
    if terminal_event == "playback_ended":
        return "playback_ended_early", None
    return "unverified_playback", None


def run(
    track_id: str,
    host_executable: str,
    state_json: str,
    *,
    session_factory: Callable[[], object] | None = None,
    credential_store_factory: Callable[[], object] = session_store.WindowsCredentialStore,
    host_factory: Callable[..., object] | None = None,
    proxy_factory: Callable[..., object] = audio.RangeProxy,
    emit: Callable[[str], None] = print,
    sleeper: Callable[[float], None] = time.sleep,
) -> int:
    if not login_probe._track_id(track_id):
        emit(_status("invalid_track"))
        return 2
    if not host_executable or not state_json:
        emit(_status("host_not_specified"))
        return 2
    try:
        with login_probe._quiet_library():
            if session_factory is None:
                import tidalapi

                session_factory = tidalapi.Session
            session = session_factory()
            store = credential_store_factory()
        try:
            with login_probe._quiet_library():
                restored, previous_credential = session_store.restore_session(session, store)
        except session_store.CredentialStoreError:
            raise
        except Exception:
            emit(_status("session_restore_failed"))
            return 1
        if restored:
            emit(_status("session_reused"))
            with login_probe._quiet_library():
                previous_credential = session_store.save_if_changed(
                    session, store, previous_credential
                )
        else:
            with login_probe._quiet_library():
                challenge = session.get_link_login()
            code = login_probe._login_code(challenge.user_code)
            address = (_direct_login_address(
                getattr(challenge, "verification_uri_complete", None), code
            ) if code is not None else None)
            if address is None or code is None:
                emit(_status("invalid_login_challenge"))
                return 1
            emit("Adres logowania: " + address)
            emit("Kod jednorazowy: " + code)

            with login_probe._quiet_library():
                authenticated = session.process_link_login(challenge)
            if not authenticated:
                emit(_status("login_failed"))
                return 1
            with login_probe._quiet_library():
                previous_credential = session_store.save_if_changed(
                    session, store, previous_credential
                )
            emit(_status("session_saved"))

        with login_probe._quiet_library():
            track = session.track(track_id)
            duration = getattr(track, "duration", None)
            title = _title(getattr(track, "title", None))
            response = session.request.basic_request(
                "GET",
                "tracks/" + track_id + "/playbackinfopostpaywall",
                {
                    "playbackmode": "STREAM",
                    "audioquality": "LOSSLESS",
                    "assetpresentation": "FULL",
                },
            )
            # basic_request can refresh access_token; persist it before parsing/host work.
            previous_credential = session_store.save_if_changed(
                session, store, previous_credential
            )
            http_status = response.status_code
            payload = response.json() if 200 <= http_status < 300 else None
        if http_status in (401, 403):
            emit(_status("access_denied"))
            return 1
        if not 200 <= http_status < 300:
            emit(_status("manifest_unavailable"))
            return 1
        try:
            plan = audio.resolve_bts(payload, duration_seconds=duration)
        except audio.BtsResolutionError as error:
            emit(_status(str(error)))
            return 1
        if plan.duration_seconds is None:
            emit("Status: " + json.dumps({
                "status": "duration_unavailable",
                "format": plan.codec,
                "durationSeconds": None,
            }, separators=(",", ":")))
            return 1

        event_queue: queue.Queue[tuple[str, object]] = queue.Queue()
        with login_probe._quiet_library():
            host_type = host_factory or _host_factory()
        with proxy_factory(plan) as proxy:
            host = host_type(
                host_executable,
                state_json=state_json,
                release_profile="test",
                on_event=lambda name, data: event_queue.put((name, data)),
                on_stderr=lambda _: None,
            )
            started_host = False
            try:
                with login_probe._quiet_library():
                    host.start()
                    started_host = True
                    accepted = host.call(
                        "tidal.nativePlay",
                        {
                            "id": track_id,
                            "title": title,
                            "proxyUrl": proxy.local_url,
                            "durationSeconds": plan.duration_seconds,
                        },
                        timeout=30,
                    )
                if not isinstance(accepted, dict) or not accepted.get("ok"):
                    emit(_status("host_rejected"))
                    return 1
                if accepted.get("id") != "tidal:" + track_id:
                    emit(_status("host_rejected"))
                    return 1

                event = _wait_for_start(event_queue, track_id, timeout=25)
                if event != "started":
                    emit(_status(event))
                    return 1
                emit(_status("playback_started"))
                observation, measured_seconds = _observe_45_seconds(
                    host, event_queue, track_id, sleeper
                )
                emit(_status(observation, seconds=measured_seconds))
                return 0 if observation == "played_beyond_30s" else 1
            finally:
                if started_host:
                    with login_probe._quiet_library():
                        try:
                            host.call("transport.stop", {"sessionId": "tidal"}, timeout=10)
                        except Exception:
                            pass
                with login_probe._quiet_library():
                    try:
                        host.close()
                    except Exception:
                        pass
    except ModuleNotFoundError:
        emit(_status("tidalapi_missing"))
        return 2
    except session_store.CredentialStoreError:
        emit(_status("credential_store_unavailable"))
        return 1
    except TimeoutError:
        emit(_status("login_expired"))
        return 1
    except Exception:
        # Never expose exception text: it may contain signed URLs or credentials.
        emit(_status("player_probe_failed"))
        return 1


if __name__ == "__main__":
    args = sys.argv[1:]
    raise SystemExit(run(*args) if len(args) == 3 else run("", "", ""))
