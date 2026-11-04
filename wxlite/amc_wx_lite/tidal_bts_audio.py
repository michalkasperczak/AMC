"""Volatile BTS audio plan and loopback Range proxy for AMC Test playback.

No token or signed TIDAL URL is written to disk or included in a repr, log or
public status. The proxy starts only inside a context manager and stops on exit.
"""

from __future__ import annotations

import base64
import binascii
import json
import math
import re
import secrets
import threading
import urllib.error
import urllib.request
from dataclasses import dataclass, field
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from typing import Callable
from urllib.parse import urlsplit


class BtsResolutionError(ValueError):
    """A fixed, safe reason for declining a stream plan."""


@dataclass(frozen=True)
class BtsAudioPlan:
    codec: str
    mime_type: str
    duration_seconds: float | None
    _signed_url: str = field(repr=False, compare=False)

    def public_status(self) -> dict[str, object]:
        return {
            "format": self.codec,
            "durationSeconds": self.duration_seconds,
        }


def resolve_bts(
    playback_info: object, *, duration_seconds: float | None = None
) -> BtsAudioPlan:
    """Accept one clear BTS HTTPS audio URL; decline DRM and DASH.

    Duration comes from separate track metadata. BTS alone cannot establish it.
    """
    if duration_seconds is not None and (
        not isinstance(duration_seconds, (int, float))
        or isinstance(duration_seconds, bool)
        or not math.isfinite(duration_seconds)
        or not 0 < duration_seconds <= 86_400
    ):
        raise BtsResolutionError("invalid_duration")
    if not isinstance(playback_info, dict):
        raise BtsResolutionError("invalid_response")
    if playback_info.get("assetPresentation") != "FULL":
        raise BtsResolutionError("not_full")
    if playback_info.get("manifestMimeType") != "application/vnd.tidal.bts":
        raise BtsResolutionError("unsupported_manifest")
    encoded = playback_info.get("manifest")
    if not isinstance(encoded, str) or not 0 < len(encoded) <= 64 * 1024:
        raise BtsResolutionError("invalid_manifest")
    try:
        data = base64.b64decode(encoded, validate=True)
        manifest = json.loads(data)
    except (ValueError, UnicodeError, binascii.Error):
        raise BtsResolutionError("invalid_manifest") from None
    if not isinstance(manifest, dict):
        raise BtsResolutionError("invalid_manifest")
    if manifest.get("encryptionType") != "NONE":
        raise BtsResolutionError("encrypted_audio")
    if manifest.get("keyId") not in (None, ""):
        raise BtsResolutionError("encrypted_audio")

    mime = manifest.get("mimeType")
    codec = manifest.get("codecs")
    formats = {
        ("audio/flac", "flac"): "flac",
        ("audio/x-flac", "flac"): "flac",
        ("audio/mp4", "mp4a.40.2"): "aac",
        ("audio/mp4", "mp4a.40.5"): "aac",
        ("audio/mpeg", "mp3"): "mp3",
    }
    normalized = formats.get((mime, str(codec).lower()))
    if normalized is None:
        raise BtsResolutionError("unsupported_format")
    urls = manifest.get("urls")
    if not isinstance(urls, list) or len(urls) != 1 or not isinstance(urls[0], str):
        raise BtsResolutionError("invalid_url_count")
    signed_url = urls[0]
    try:
        parsed = urlsplit(signed_url)
        host = parsed.hostname or ""
        port = parsed.port
    except ValueError:
        raise BtsResolutionError("invalid_audio_url") from None
    if (
        parsed.scheme != "https"
        or (host != "audio.tidal.com" and not host.endswith(".audio.tidal.com"))
        or port not in (None, 443)
        or parsed.username is not None
        or parsed.password is not None
        or not parsed.path.startswith("/")
        or parsed.fragment
    ):
        raise BtsResolutionError("invalid_audio_url")
    return BtsAudioPlan(normalized, mime, duration_seconds, signed_url)


class _NoRedirect(urllib.request.HTTPRedirectHandler):
    def redirect_request(self, request, fp, code, msg, headers, newurl):
        return None


def _default_opener():
    # Ignore HTTP_PROXY/HTTPS_PROXY from the process environment. Do not allow
    # a redirect to escape the validated TIDAL audio host.
    return urllib.request.build_opener(urllib.request.ProxyHandler({}), _NoRedirect())


class RangeProxy:
    """One in-memory signed URL served only via a random loopback capability."""

    def __init__(self, plan: BtsAudioPlan, *, opener=None):
        self._plan = plan
        self._opener = opener or _default_opener()
        self._capability = secrets.token_urlsafe(24)
        self._server: ThreadingHTTPServer | None = None
        self._thread: threading.Thread | None = None

    def __enter__(self) -> RangeProxy:
        plan = self._plan
        opener = self._opener
        capability_path = "/audio/" + self._capability

        class Handler(BaseHTTPRequestHandler):
            def log_message(self, format, *args):
                pass

            def do_GET(self):
                self._serve(head_only=False)

            def do_HEAD(self):
                self._serve(head_only=True)

            def _serve(self, *, head_only: bool):
                headers_sent = False
                if self.path != capability_path:
                    self._empty(404)
                    return
                range_header = self.headers.get("Range")
                if range_header is not None:
                    match = re.fullmatch(r"bytes=(\d{1,18})-(\d{0,18})", range_header)
                    if match is None or (
                        match.group(2) and int(match.group(1)) > int(match.group(2))
                    ):
                        self._empty(416)
                        return
                headers = {"Accept-Encoding": "identity"}
                if range_header is not None:
                    headers["Range"] = range_header
                request = urllib.request.Request(
                    plan._signed_url,
                    headers=headers,
                    method="HEAD" if head_only else "GET",
                )
                try:
                    with opener.open(request, timeout=15) as upstream:
                        status = getattr(upstream, "status", 0)
                        if status not in (200, 206) or (range_header and status != 206):
                            self._empty(502)
                            return
                        length = upstream.headers.get("Content-Length")
                        content_range = upstream.headers.get("Content-Range")
                        if length is not None and not re.fullmatch(r"\d{1,18}", length):
                            self._empty(502)
                            return
                        if status == 206 and (
                            content_range is None
                            or not re.fullmatch(r"bytes \d+-\d+/\d+", content_range)
                        ):
                            self._empty(502)
                            return
                        self.send_response(status)
                        self.send_header("Content-Type", plan.mime_type)
                        self.send_header("Accept-Ranges", "bytes")
                        self.send_header("Cache-Control", "no-store")
                        self.send_header("X-Content-Type-Options", "nosniff")
                        if length is not None:
                            self.send_header("Content-Length", length)
                        if content_range is not None:
                            self.send_header("Content-Range", content_range)
                        self.end_headers()
                        headers_sent = True
                        if not head_only:
                            while chunk := upstream.read(64 * 1024):
                                self.wfile.write(chunk)
                except Exception as error:
                    # urllib exceptions often contain the signed URL.
                    close = getattr(error, "close", None)
                    if callable(close):
                        close()
                    try:
                        if not headers_sent and not self.wfile.closed:
                            self._empty(502)
                        else:
                            self.close_connection = True
                    except Exception:
                        pass

            def _empty(self, status: int):
                self.send_response(status)
                self.send_header("Content-Length", "0")
                self.send_header("Cache-Control", "no-store")
                self.end_headers()

        class Server(ThreadingHTTPServer):
            daemon_threads = True

            def handle_error(self, request, client_address):
                # Never print tracebacks containing a signed URL.
                pass

        self._server = Server(("127.0.0.1", 0), Handler)
        self._thread = threading.Thread(target=self._server.serve_forever, daemon=True)
        self._thread.start()
        return self

    @property
    def local_url(self) -> str:
        if self._server is None:
            raise RuntimeError("proxy_not_started")
        return f"http://127.0.0.1:{self._server.server_port}/audio/{self._capability}"

    def __exit__(self, exc_type, exc, traceback) -> None:
        if self._server is not None:
            self._server.shutdown()
            self._server.server_close()
            self._server = None
        if self._thread is not None:
            self._thread.join(timeout=2)
            self._thread = None

