"""Receiver-scoped bridge to AMC's existing unencrypted audio loopback proxy.

The receiver gets a new capability, never the upstream URL, session token or
the loopback capability. DASH and arbitrary HTTP proxies are not accepted.
"""
from __future__ import annotations

import http.client
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
import re
import secrets
import socket
import threading
from urllib.parse import urlsplit

from .cast_protocol import CastError
from .network_audio import valid_tidal_request


class TidalCastShare:
    def __init__(self, request: dict, receiver_host: str) -> None:
        if not valid_tidal_request(request, cast=True):
            raise CastError("cast.error.source")
        upstream = urlsplit(request["proxyUrl"])
        probe = http.client.HTTPConnection("127.0.0.1", upstream.port, timeout=12)
        try:
            probe.request("HEAD", upstream.path)
            response = probe.getresponse()
            mime = (response.getheader("Content-Type") or "").split(";", 1)[0].strip().lower()
            if response.status not in (200, 206) or mime not in {
                    "audio/flac", "audio/x-flac", "audio/mp4", "audio/aac", "audio/mpeg"}:
                raise CastError("cast.error.source")
            self.mime = "audio/flac" if mime == "audio/x-flac" else mime
        except (OSError, http.client.HTTPException) as error:
            raise CastError("cast.error.source") from error
        finally:
            probe.close()
        receiver_host = socket.gethostbyname(receiver_host)
        with socket.socket(socket.AF_INET, socket.SOCK_DGRAM) as probe:
            probe.connect((receiver_host, 8009))
            bind_host = probe.getsockname()[0]
        token = "/" + secrets.token_urlsafe(32)
        self._lock = threading.Lock()
        self._closed = False
        self._clients: set[socket.socket] = set()
        self._upstreams: set[http.client.HTTPConnection] = set()
        owner = self

        class Handler(BaseHTTPRequestHandler):
            def log_message(self, *_args):
                pass

            def do_HEAD(self):
                self._serve("HEAD")

            def do_GET(self):
                self._serve("GET")

            def _serve(self, method):
                if (self.client_address[0] != receiver_host or not self.path.isascii()
                        or not secrets.compare_digest(self.path, token)):
                    self.send_error(404)
                    return
                byte_range = self.headers.get("Range")
                if byte_range and not re.fullmatch(r"bytes=(?:[0-9]+-[0-9]*|-[0-9]+)", byte_range):
                    self.send_error(416)
                    return
                headers = {"Range": byte_range} if byte_range else {}
                connection = http.client.HTTPConnection("127.0.0.1", upstream.port, timeout=12)
                with owner._lock:
                    if owner._closed:
                        return
                    owner._clients.add(self.connection)
                    owner._upstreams.add(connection)
                self.connection.settimeout(12)
                try:
                    connection.request(method, upstream.path, headers=headers)
                    response = connection.getresponse()
                    if response.status not in (200, 206, 416):
                        self.send_error(502)
                        return
                    self.send_response(response.status)
                    self.send_header("Content-Type", owner.mime)
                    for name in ("Content-Length", "Content-Range", "Accept-Ranges"):
                        value = response.getheader(name)
                        if value is not None:
                            self.send_header(name, value)
                    self.send_header("Cache-Control", "no-store")
                    self.end_headers()
                    if method == "GET":
                        while not owner._closed:
                            block = response.read(65536)
                            if not block:
                                break
                            self.wfile.write(block)
                except (OSError, http.client.HTTPException):
                    # Never include a private capability in an HTTP error/log.
                    pass
                finally:
                    connection.close()
                    with owner._lock:
                        owner._clients.discard(self.connection)
                        owner._upstreams.discard(connection)

        self._server = ThreadingHTTPServer((bind_host, 0), Handler)
        self._server.daemon_threads = True
        self.url = f"http://{bind_host}:{self._server.server_port}{token}"
        self._worker = threading.Thread(target=self._server.serve_forever, daemon=True)
        self._worker.start()

    def close(self) -> None:
        with self._lock:
            if self._closed:
                return
            self._closed = True
            clients = tuple(self._clients)
            upstreams = tuple(self._upstreams)
        for connection in upstreams:
            connection.close()
        for client in clients:
            try:
                client.shutdown(socket.SHUT_RDWR)
            except OSError:
                pass
        self._server.shutdown()
        self._server.server_close()
        self._worker.join(timeout=2)
