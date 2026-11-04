"""One-shot, opt-in TIDAL audio-byte gate; never plays or saves a track.

Run manually from an isolated Python environment with tidalapi installed.
The only live output is a TIDAL login address, one-time code and a fixed status.
The loopback proxy exists only for one 64-byte Range request after login.
"""

from __future__ import annotations

import json
import sys
import urllib.request
from collections.abc import Callable

import tidal_bts_audio as audio
import tidalapi_device_probe as login_probe


def _signature(data: bytes, codec: str) -> str:
    if codec == "flac" and data.startswith(b"fLaC"):
        return "flac"
    if codec == "aac" and len(data) >= 8 and data[4:8] == b"ftyp":
        return "mp4"
    if codec == "mp3" and (
        data.startswith(b"ID3") or (len(data) >= 2 and data[0] == 0xFF and data[1] & 0xE0 == 0xE0)
    ):
        return "mp3"
    return "unknown"


def run(
    track_id: str,
    *,
    session_factory: Callable[[], object] | None = None,
    upstream_opener=None,
    emit: Callable[[str], None] = print,
) -> int:
    if not login_probe._track_id(track_id):
        emit('Status: {"status":"invalid_track"}')
        return 2
    try:
        with login_probe._quiet_library():
            if session_factory is None:
                import tidalapi

                session_factory = tidalapi.Session
            session = session_factory()
            challenge = session.get_link_login()
        address = login_probe._login_address(challenge.verification_uri)
        code = login_probe._login_code(challenge.user_code)
        if address is None or code is None:
            emit('Status: {"status":"invalid_login_challenge"}')
            return 1
        emit("Adres logowania: " + address)
        emit("Kod jednorazowy: " + code)

        with login_probe._quiet_library():
            authenticated = session.process_link_login(challenge)
        if not authenticated:
            emit('Status: {"status":"login_failed"}')
            return 1
        with login_probe._quiet_library():
            track = session.track(track_id)
            duration = getattr(track, "duration", None)
            response = session.request.basic_request(
                "GET",
                "tracks/" + track_id + "/playbackinfopostpaywall",
                {
                    "playbackmode": "STREAM",
                    "audioquality": "LOSSLESS",
                    "assetpresentation": "FULL",
                },
            )
            http_status = response.status_code
            payload = response.json() if 200 <= http_status < 300 else None
        if http_status in (401, 403):
            emit('Status: {"status":"access_denied"}')
            return 1
        if not 200 <= http_status < 300:
            emit('Status: {"status":"manifest_unavailable"}')
            return 1

        try:
            plan = audio.resolve_bts(payload, duration_seconds=duration)
        except audio.BtsResolutionError as error:
            # BtsResolutionError contains only fixed codes from our resolver.
            emit("Status: " + json.dumps({"status": str(error)}, separators=(",", ":")))
            return 1

        with login_probe._quiet_library():
            with audio.RangeProxy(plan, opener=upstream_opener) as proxy:
                local_opener = urllib.request.build_opener(urllib.request.ProxyHandler({}))
                request = urllib.request.Request(
                    proxy.local_url, headers={"Range": "bytes=0-63"}
                )
                with local_opener.open(request, timeout=20) as stream:
                    status = stream.status
                    first_bytes = stream.read(64)
        signature = _signature(first_bytes, plan.codec)
        result = {
            "status": "audio_header" if status == 206 and signature != "unknown"
            else "unexpected_audio_response",
            "format": plan.codec,
            "durationSeconds": plan.duration_seconds,
            "bytesReceived": len(first_bytes),
            "signature": signature,
        }
        emit("Status: " + json.dumps(result, separators=(",", ":")))
        return 0 if result["status"] == "audio_header" else 1
    except ModuleNotFoundError:
        emit('Status: {"status":"tidalapi_missing"}')
        return 2
    except TimeoutError:
        emit('Status: {"status":"login_expired"}')
        return 1
    except Exception:
        # Third-party errors can contain signed addresses and tokens.
        emit('Status: {"status":"audio_probe_failed"}')
        return 1


if __name__ == "__main__":
    raise SystemExit(run(sys.argv[1] if len(sys.argv) == 2 else ""))
