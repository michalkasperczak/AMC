"""Private loopback DASH proxy for clear TIDAL HiRes FLAC.

The remote segment addresses stay in memory. FFmpeg receives only the local
manifest capability, including when it seeks to another segment.
"""

from __future__ import annotations

import base64
import binascii
import math
import re
import secrets
import threading
import urllib.request
import xml.etree.ElementTree as ET
from dataclasses import dataclass, field
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from urllib.parse import urlsplit

from .tidal_bts_audio import _default_opener


class DashResolutionError(ValueError):
    """A safe, fixed failure reason without any remote address."""


def _name(element: ET.Element) -> str:
    return element.tag.rsplit("}", 1)[-1]


def _valid_url(address: str) -> bool:
    try:
        parsed = urlsplit(address)
        host = parsed.hostname or ""
        port = parsed.port
    except ValueError:
        return False
    return (parsed.scheme == "https"
            and (host == "audio.tidal.com" or host.endswith(".audio.tidal.com"))
            and port in (None, 443) and parsed.username is None
            and parsed.password is None and parsed.fragment == ""
            and parsed.path.startswith("/") and not any(ord(c) < 32 for c in address))


@dataclass(frozen=True)
class DashAudioPlan:
    duration_seconds: float
    bit_depth: int
    sample_rate: int
    _manifest: bytes = field(repr=False, compare=False)
    _initialization: str = field(repr=False, compare=False)
    _media_template: str = field(repr=False, compare=False)
    _first_number: int = field(repr=False)
    _segment_count: int = field(repr=False)

    def segment_url(self, number: int) -> str | None:
        if not self._first_number <= number < self._first_number + self._segment_count:
            return None
        return self._media_template.replace("$Number$", str(number))


def resolve_dash(playback_info: object, *, duration_seconds: float | None) -> DashAudioPlan:
    if (not isinstance(duration_seconds, (int, float)) or isinstance(duration_seconds, bool)
            or not math.isfinite(duration_seconds) or not 0 < duration_seconds <= 86_400):
        raise DashResolutionError("invalid_duration")
    if not isinstance(playback_info, dict) or playback_info.get("assetPresentation") != "FULL":
        raise DashResolutionError("invalid_response")
    if (playback_info.get("audioQuality") != "HI_RES_LOSSLESS"
            or playback_info.get("manifestMimeType") != "application/dash+xml"):
        raise DashResolutionError("unsupported_manifest")
    bit_depth = playback_info.get("bitDepth")
    sample_rate = playback_info.get("sampleRate")
    if (type(bit_depth) is not int or not 16 <= bit_depth <= 32
            or type(sample_rate) is not int or not 44_100 <= sample_rate <= 384_000):
        raise DashResolutionError("invalid_audio_metadata")
    encoded = playback_info.get("manifest")
    if not isinstance(encoded, str) or not 0 < len(encoded) <= 128 * 1024:
        raise DashResolutionError("invalid_manifest")
    try:
        raw = base64.b64decode(encoded, validate=True)
        if len(raw) > 96 * 1024 or b"<!" in raw:
            raise DashResolutionError("invalid_manifest")
        root = ET.fromstring(raw)
    except (ValueError, binascii.Error, ET.ParseError):
        raise DashResolutionError("invalid_manifest") from None
    if _name(root) != "MPD" or root.get("type") not in (None, "static"):
        raise DashResolutionError("invalid_manifest")
    if any(_name(node) in ("ContentProtection", "BaseURL", "SegmentList", "SegmentBase")
           for node in root.iter()):
        raise DashResolutionError("unsupported_manifest")
    representations = [node for node in root.iter() if _name(node) == "Representation"]
    adaptations = [node for node in root.iter() if _name(node) == "AdaptationSet"]
    templates = [node for node in root.iter() if _name(node) == "SegmentTemplate"]
    timelines = [node for node in root.iter() if _name(node) == "SegmentTimeline"]
    if (len(representations) != 1 or len(adaptations) != 1
            or len(templates) != 1 or len(timelines) != 1
            or adaptations[0].get("mimeType") != "audio/mp4"
            or representations[0].get("codecs") != "flac"
            or representations[0].get("audioSamplingRate") != str(sample_rate)):
        raise DashResolutionError("unsupported_manifest")
    template = templates[0]
    initialization = template.get("initialization", "")
    media = template.get("media", "")
    if ("$" in initialization or media.count("$Number$") != 1
            or "$" in media.replace("$Number$", "")
            or not _valid_url(initialization)
            or not _valid_url(media.replace("$Number$", "1"))):
        raise DashResolutionError("invalid_audio_url")
    try:
        first = int(template.get("startNumber", "1"))
        timescale = int(template.get("timescale", "1"))
    except ValueError:
        raise DashResolutionError("invalid_manifest") from None
    if not 0 <= first <= 100_000 or not 1 <= timescale <= 1_000_000:
        raise DashResolutionError("invalid_manifest")
    count = 0
    for segment in timelines[0]:
        if _name(segment) != "S":
            raise DashResolutionError("invalid_manifest")
        try:
            repeat = int(segment.get("r", "0"))
            length = int(segment.get("d", "0"))
        except ValueError:
            raise DashResolutionError("invalid_manifest") from None
        if not 0 <= repeat <= 5000 or length <= 0:
            raise DashResolutionError("invalid_manifest")
        count += repeat + 1
    if not 0 < count <= 5000:
        raise DashResolutionError("invalid_manifest")
    return DashAudioPlan(float(duration_seconds), bit_depth, sample_rate,
                         raw, initialization, media, first, count)


class DashProxy:
    def __init__(self, plan: DashAudioPlan, *, opener=None):
        self._plan = plan
        self._opener = opener or _default_opener()
        self._capability = secrets.token_urlsafe(24)
        self._server: ThreadingHTTPServer | None = None
        self._thread: threading.Thread | None = None

    def __enter__(self):
        plan, opener = self._plan, self._opener
        prefix = "/audio/" + self._capability
        local_base = "http://127.0.0.1"

        class Handler(BaseHTTPRequestHandler):
            def log_message(self, format, *args):
                pass

            def _empty(self, status):
                self.send_response(status)
                self.send_header("Content-Length", "0")
                self.send_header("Cache-Control", "no-store")
                self.end_headers()

            def do_GET(self):
                self._serve(False)

            def do_HEAD(self):
                self._serve(True)

            def _serve(self, head_only):
                if self.path == prefix + ".mpd":
                    root = ET.fromstring(plan._manifest)
                    template = next(node for node in root.iter() if _name(node) == "SegmentTemplate")
                    base = f"{local_base}:{self.server.server_port}{prefix}"
                    template.set("initialization", base + "/init")
                    template.set("media", base + "/seg-$Number$")
                    body = ET.tostring(root, encoding="utf-8", xml_declaration=True)
                    self.send_response(200)
                    self.send_header("Content-Type", "application/dash+xml")
                    self.send_header("Content-Length", str(len(body)))
                    self.send_header("Cache-Control", "no-store")
                    self.end_headers()
                    if not head_only:
                        self.wfile.write(body)
                    return
                if self.path == prefix + "/init":
                    address = plan._initialization
                else:
                    match = re.fullmatch(re.escape(prefix) + r"/seg-(\d{1,6})", self.path)
                    address = plan.segment_url(int(match.group(1))) if match else None
                if address is None:
                    self._empty(404)
                    return
                range_header = self.headers.get("Range")
                if range_header is not None and not re.fullmatch(r"bytes=\d{1,18}-\d{0,18}", range_header):
                    self._empty(416)
                    return
                headers = {"Accept-Encoding": "identity"}
                if range_header:
                    headers["Range"] = range_header
                request = urllib.request.Request(address, headers=headers,
                                                 method="HEAD" if head_only else "GET")
                sent = False
                try:
                    with opener.open(request, timeout=15) as upstream:
                        status = getattr(upstream, "status", 0)
                        if status not in (200, 206) or (range_header and status != 206):
                            self._empty(502)
                            return
                        length = upstream.headers.get("Content-Length")
                        if length is not None and not re.fullmatch(r"\d{1,18}", length):
                            self._empty(502)
                            return
                        content_range = upstream.headers.get("Content-Range")
                        if status == 206 and (
                            content_range is None
                            or not re.fullmatch(r"bytes \d+-\d+/\d+", content_range)
                        ):
                            self._empty(502)
                            return
                        self.send_response(status)
                        self.send_header("Content-Type", "audio/mp4")
                        self.send_header("Cache-Control", "no-store")
                        self.send_header("Accept-Ranges", "bytes")
                        if length is not None:
                            self.send_header("Content-Length", length)
                        if content_range is not None:
                            self.send_header("Content-Range", content_range)
                        self.end_headers()
                        sent = True
                        if not head_only:
                            while chunk := upstream.read(64 * 1024):
                                self.wfile.write(chunk)
                except Exception:
                    # Exception text may contain the signed address.
                    if not sent:
                        try:
                            self._empty(502)
                        except Exception:
                            pass
                    else:
                        self.close_connection = True

        class Server(ThreadingHTTPServer):
            daemon_threads = True

            def handle_error(self, request, client_address):
                pass

        self._server = Server(("127.0.0.1", 0), Handler)
        self._thread = threading.Thread(target=self._server.serve_forever, daemon=True)
        self._thread.start()
        return self

    @property
    def local_url(self):
        if self._server is None:
            raise RuntimeError("proxy_not_started")
        return f"http://127.0.0.1:{self._server.server_port}/audio/{self._capability}.mpd"

    def __exit__(self, exc_type, exc, traceback):
        if self._server is not None:
            self._server.shutdown()
            self._server.server_close()
            self._server = None
        if self._thread is not None:
            self._thread.join(timeout=2)
            self._thread = None
