"""Optional, one-shot TIDAL device-login experiment for AMC Test.

This tool is deliberately separate from the running AMC process and its
credential store. It never opens a browser, saves a session or plays audio.
Install tidalapi only in an isolated environment before running it manually.
"""

from __future__ import annotations

import base64
import binascii
import contextlib
import io
import json
import logging
import sys
from collections.abc import Callable, Iterator
from urllib.parse import urlsplit, urlunsplit


class _Discard(io.TextIOBase):
    def write(self, value: str) -> int:
        return len(value)

    def flush(self) -> None:
        pass


@contextlib.contextmanager
def _quiet_library() -> Iterator[None]:
    """Do not let third-party logging or exceptions print account data."""
    previous_level = logging.root.manager.disable
    logging.disable(logging.CRITICAL)
    try:
        with contextlib.redirect_stdout(_Discard()), contextlib.redirect_stderr(
            _Discard()
        ):
            yield
    finally:
        logging.disable(previous_level)


def _track_id(value: str) -> bool:
    return 1 <= len(value) <= 18 and value.isascii() and value.isdecimal()


def _login_address(value: object) -> str | None:
    if not isinstance(value, str):
        return None
    candidate = value.strip()
    if not candidate:
        return None
    if "://" not in candidate:
        candidate = "https://" + candidate
    parsed = urlsplit(candidate)
    if parsed.scheme != "https" or parsed.username or parsed.password:
        return None
    host = parsed.hostname or ""
    if host != "tidal.com" and not host.endswith(".tidal.com"):
        return None
    if parsed.port not in (None, 443):
        return None
    return urlunsplit(("https", host, parsed.path, "", ""))


def _login_code(value: object) -> str | None:
    if not isinstance(value, str) or not 3 <= len(value) <= 24:
        return None
    if not all(char.isascii() and (char.isalnum() or char == "-") for char in value):
        return None
    return value


def _manifest_status(payload: object, http_status: int) -> dict[str, object]:
    status = "invalid_response"
    full = False
    if http_status in (401, 403):
        status = "access_denied"
    elif http_status == 429:
        status = "rate_limited"
    elif not 200 <= http_status < 300:
        status = "server_rejected"
    elif isinstance(payload, dict):
        presentation = payload.get("assetPresentation")
        if presentation == "PREVIEW":
            status = "preview_only"
        elif presentation == "FULL" and payload.get("manifestMimeType") in (
            "application/vnd.tidal.bts",
            "application/dash+xml",
        ):
            manifest = payload.get("manifest")
            if isinstance(manifest, str) and 0 < len(manifest) <= 64 * 1024:
                try:
                    full = bool(base64.b64decode(manifest, validate=True))
                except (ValueError, binascii.Error):
                    pass
                if full:
                    status = "full_manifest"
    return {
        "status": status,
        "httpStatus": http_status if 100 <= http_status <= 599 else 0,
        "fullManifestAvailable": full,
    }


def run(
    track_id: str,
    *,
    session_factory: Callable[[], object] | None = None,
    emit: Callable[[str], None] = print,
) -> int:
    if not _track_id(track_id):
        emit('Status: {"status":"invalid_track"}')
        return 2
    try:
        with _quiet_library():
            if session_factory is None:
                import tidalapi

                session_factory = tidalapi.Session
            session = session_factory()
            login = session.get_link_login()
        address = _login_address(login.verification_uri)
        code = _login_code(login.user_code)
        if address is None or code is None:
            emit('Status: {"status":"invalid_login_challenge"}')
            return 1

        emit("Adres logowania: " + address)
        emit("Kod jednorazowy: " + code)

        with _quiet_library():
            authenticated = session.process_link_login(login)
        if not authenticated:
            emit('Status: {"status":"login_failed"}')
            return 1

        with _quiet_library():
            # basic_request preserves the HTTP status on 401/403. The higher
            # level request() raises an exception with server details.
            response = session.request.basic_request(
                "GET",
                "tracks/" + track_id + "/playbackinfopostpaywall",
                {
                    "playbackmode": "STREAM",
                    "audioquality": "HIGH",
                    "assetpresentation": "FULL",
                },
            )
            http_status = response.status_code
            if 200 <= http_status < 300:
                try:
                    payload = response.json()
                except ValueError:
                    payload = None
            else:
                # Error bodies are not needed and may contain private data.
                payload = None
        result = _manifest_status(payload, http_status)
        emit("Status: " + json.dumps(result, separators=(",", ":")))
        return 0 if result["fullManifestAvailable"] else 1
    except ModuleNotFoundError:
        emit('Status: {"status":"tidalapi_missing"}')
        return 2
    except TimeoutError:
        emit('Status: {"status":"login_expired"}')
        return 1
    except Exception:
        # Exception text may contain a token, URL or raw server response.
        emit('Status: {"status":"probe_failed"}')
        return 1


if __name__ == "__main__":
    raise SystemExit(run(sys.argv[1] if len(sys.argv) == 2 else ""))
