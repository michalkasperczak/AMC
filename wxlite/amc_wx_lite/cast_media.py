"""Bounded HTTP sharing of exactly one Cast media file, without a file listing."""
from __future__ import annotations

import os
import re
import secrets
import socket
import threading
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path
from urllib.parse import urlsplit

from .cast_protocol import CastError


_FORMATS = {".mp3": "audio/mpeg", ".m4a": "audio/mp4", ".aac": "audio/aac",
            ".wav": "audio/wav", ".flac": "audio/flac", ".ogg": "audio/ogg",
            ".opus": "audio/ogg", ".mp4": "video/mp4"}


def cast_source(source: str) -> tuple[str, str, Path | None]:
    parsed = urlsplit(source)
    if parsed.scheme in ("http", "https"):
        host = (parsed.hostname or "").casefold()
        if (not host or parsed.username or parsed.password
                or host in ("localhost", "::1") or host.startswith("127.")
                or any(host == domain or host.endswith("." + domain) for domain in
                       ("youtube.com", "youtu.be", "googlevideo.com", "spotify.com", "tidal.com"))):
            raise CastError("cast.error.source")
        # Radio URLs often omit an extension; the receiver validates playback.
        return source, _FORMATS.get(Path(parsed.path).suffix.casefold(), "audio/mpeg"), None
    path = Path(source).resolve()
    mime = _FORMATS.get(path.suffix.casefold())
    if not mime:
        raise CastError("cast.error.format")
    if not path.is_file():
        raise CastError("cast.error.fileMissing")
    return "", mime, path


class CastFileShare:
    def __init__(self, path: Path, receiver_host: str, mime: str) -> None:
        self.path = path
        self._receiver = receiver_host
        self._token = "/" + secrets.token_urlsafe(32)
        self._clients: set[socket.socket] = set()
        self._clients_lock = threading.Lock()
        with socket.socket(socket.AF_INET, socket.SOCK_DGRAM) as probe:
            probe.connect((receiver_host, 8009))
            host = probe.getsockname()[0]
        owner = self

        class Handler(BaseHTTPRequestHandler):
            def setup(self) -> None:
                self.request.settimeout(5)
                super().setup()
                with owner._clients_lock:
                    owner._clients.add(self.connection)

            def finish(self) -> None:
                try:
                    super().finish()
                finally:
                    with owner._clients_lock:
                        owner._clients.discard(self.connection)

            def log_message(self, _format: str, *args: object) -> None:
                pass

            def do_HEAD(self) -> None:
                self._serve(False)

            def do_GET(self) -> None:
                self._serve(True)

            def _serve(self, body: bool) -> None:
                if self.path != owner._token or self.client_address[0] != owner._receiver:
                    self.send_error(404)
                    return
                try:
                    stream = owner.path.open("rb")
                except OSError:
                    self.send_error(404)
                    return
                with stream:
                    size = os.fstat(stream.fileno()).st_size
                    start, end = 0, size - 1
                    byte_range = self.headers.get("Range")
                    if byte_range:
                        match = re.fullmatch(r"bytes=(\d*)-(\d*)", byte_range.strip())
                        if not match or not any(match.groups()) or any(len(value) > 20 for value in match.groups()):
                            self.send_error(416)
                            return
                        first, last = match.groups()
                        if first:
                            start = int(first)
                            end = min(int(last), size - 1) if last else size - 1
                        else:
                            start = max(0, size - int(last))
                        if start > end or start >= size:
                            self.send_response(416)
                            self.send_header("Content-Range", f"bytes */{size}")
                            self.end_headers()
                            return
                    self.send_response(206 if byte_range else 200)
                    self.send_header("Content-Type", mime)
                    self.send_header("Accept-Ranges", "bytes")
                    self.send_header("Access-Control-Allow-Origin", "*")
                    self.send_header("Content-Length", str(max(0, end - start + 1)))
                    if byte_range:
                        self.send_header("Content-Range", f"bytes {start}-{end}/{size}")
                    self.end_headers()
                    if body:
                        stream.seek(start)
                        remaining = end - start + 1
                        try:
                            while remaining > 0:
                                data = stream.read(min(65536, remaining))
                                if not data:
                                    break
                                self.wfile.write(data)
                                remaining -= len(data)
                        except OSError:
                            pass

        self._server = ThreadingHTTPServer((host, 0), Handler)
        self._server.daemon_threads = False
        self._thread = threading.Thread(target=self._server.serve_forever, daemon=True)
        self._thread.start()
        self.url = f"http://{host}:{self._server.server_port}{self._token}"

    def close(self) -> None:
        self._server.shutdown()
        with self._clients_lock:
            for client in tuple(self._clients):
                try:
                    client.shutdown(socket.SHUT_RDWR)
                except OSError:
                    pass
        self._server.server_close()
        self._thread.join(timeout=2)
