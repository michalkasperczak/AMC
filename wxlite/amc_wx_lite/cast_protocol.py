"""Minimal Google Cast V2 sender for the Default Media Receiver.

Only public, directly retrievable media is passed to the receiver.  This
module has no wx dependency and never treats Cast as a Windows audio device.
"""

from __future__ import annotations

import json
import select
import socket
import ssl
import struct
import threading
import time
from dataclasses import dataclass
from typing import Callable
from . import i18n


_RECEIVER = "urn:x-cast:com.google.cast.receiver"
_MEDIA = "urn:x-cast:com.google.cast.media"
_CONNECTION = "urn:x-cast:com.google.cast.tp.connection"
_HEARTBEAT = "urn:x-cast:com.google.cast.tp.heartbeat"
_DEFAULT_MEDIA_APP = "CC1AD845"
_MULTICAST = ("224.0.0.251", 5353)


class CastError(RuntimeError):
    """A user-facing Cast operation failed."""

    def __init__(self, message: str, **values: object) -> None:
        super().__init__(i18n.tr(message, **values) if message.startswith("cast.") else message)


@dataclass(frozen=True, slots=True)
class CastDevice:
    device_id: str
    name: str
    host: str
    port: int = 8009
    available: bool = True

    @property
    def label(self) -> str:
        return self.name if self.available else i18n.tr("cast.unavailableDevice", name=self.name)

    def __str__(self) -> str:
        return self.label


def _dns_name(packet: bytes, offset: int) -> tuple[str, int]:
    labels: list[str] = []
    end: int | None = None
    visited: set[int] = set()
    while True:
        if offset >= len(packet) or offset in visited:
            raise ValueError("invalid DNS name")
        visited.add(offset)
        length = packet[offset]
        if length & 0xC0 == 0xC0:
            if offset + 1 >= len(packet):
                raise ValueError("truncated DNS pointer")
            target = ((length & 0x3F) << 8) | packet[offset + 1]
            end = offset + 2 if end is None else end
            offset = target
            continue
        if length & 0xC0:
            raise ValueError("invalid DNS label")
        offset += 1
        if length == 0:
            return ".".join(labels).casefold(), end or offset
        if offset + length > len(packet):
            raise ValueError("truncated DNS label")
        labels.append(packet[offset:offset + length].decode("utf-8", "replace"))
        offset += length


def _dns_records(packet: bytes) -> list[tuple[str, int, bytes, int]]:
    if len(packet) < 12:
        return []
    questions, answers, authorities, additional = struct.unpack_from(">4H", packet, 4)
    offset = 12
    try:
        for _ in range(questions):
            _, offset = _dns_name(packet, offset)
            offset += 4
        records = []
        for _ in range(answers + authorities + additional):
            name, offset = _dns_name(packet, offset)
            kind, _class, _ttl, length = struct.unpack_from(">HHIH", packet, offset)
            offset += 10
            if offset + length > len(packet):
                return records
            records.append((name, kind, packet[offset:offset + length], offset))
            offset += length
        return records
    except (ValueError, struct.error):
        return []


def _discovered_devices(packets: list[bytes]) -> tuple[CastDevice, ...]:
    services: dict[str, dict[str, object]] = {}
    addresses: dict[str, str] = {}
    for packet in packets:
        for name, kind, value, start in _dns_records(packet):
            if kind == 1 and len(value) == 4:
                addresses[name] = socket.inet_ntoa(value)
            if not name.endswith("._googlecast._tcp.local"):
                continue
            entry = services.setdefault(name, {})
            if kind == 33 and len(value) >= 7:
                entry["port"] = struct.unpack_from(">H", value, 4)[0]
                try:
                    entry["target"] = _dns_name(packet, start + 6)[0]
                except ValueError:
                    pass
            elif kind == 16:
                pos = 0
                while pos < len(value):
                    length = value[pos]
                    pos += 1
                    field = value[pos:pos + length]
                    pos += length
                    key, _, raw = field.partition(b"=")
                    if key in (b"id", b"fn"):
                        entry[key.decode("ascii")] = raw.decode("utf-8", "replace").strip()
    result: dict[str, CastDevice] = {}
    for service, fields in services.items():
        target = fields.get("target")
        host = addresses.get(target) if isinstance(target, str) else None
        name = fields.get("fn") or "Google Cast"
        name = " ".join("".join(ch for ch in str(name) if ch.isprintable()).split())[:180]
        name = name or "Google Cast"
        device_id = fields.get("id")
        port = fields.get("port")
        if (isinstance(host, str) and isinstance(name, str)
                and isinstance(device_id, str) and device_id
                and isinstance(port, int) and 0 < port < 65536):
            result[device_id] = CastDevice(device_id, name, host, port)
    return tuple(sorted(result.values(), key=lambda item: item.name.casefold()))


def _multicast_ipv4_interfaces() -> tuple[str, ...]:
    """List local IPv4 addresses; the default route may point at a VPN."""
    try:
        records = socket.getaddrinfo(socket.gethostname(), None,
                                     socket.AF_INET, socket.SOCK_DGRAM)
    except OSError:
        return ()
    addresses = dict.fromkeys(record[4][0] for record in records)
    return tuple(address for address in addresses
                 if address != "0.0.0.0" and not address.startswith(("127.", "169.254.")))


def discover_cast_devices(timeout: float = 2.5) -> tuple[CastDevice, ...]:
    """Discover receivers across local interfaces within one bounded deadline."""
    query = (b"\x00" * 4 + struct.pack(">HHHH", 1, 0, 0, 0)
             + bytes((len("_googlecast"),)) + b"_googlecast"
             + bytes((len("_tcp"),)) + b"_tcp"
             + bytes((len("local"),)) + b"local\x00"
             + struct.pack(">HH", 12, 0x8001))
    packets: list[bytes] = []
    sockets: list[socket.socket] = []
    try:
        # Keep the default route as a fallback when hostname resolution omits
        # the active adapter. Each socket receives unicast mDNS replies.
        for address in (None, *_multicast_ipv4_interfaces()):
            sock = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
            try:
                if address is not None:
                    sock.setsockopt(socket.IPPROTO_IP, socket.IP_MULTICAST_IF,
                                    socket.inet_aton(address))
                sock.sendto(query, _MULTICAST)
                sockets.append(sock)
            except OSError:
                sock.close()
        if not sockets:
            return ()
        deadline = time.monotonic() + max(0.1, timeout)
        while (remaining := deadline - time.monotonic()) > 0:
            readable, _, _ = select.select(sockets, [], [], min(0.25, remaining))
            for sock in readable:
                try:
                    packet, _address = sock.recvfrom(65535)
                except OSError:
                    continue
                packets.append(packet)
    finally:
        for sock in sockets:
            sock.close()
    return _discovered_devices(packets)


def _varint(value: int) -> bytes:
    output = bytearray()
    while value > 127:
        output.append((value & 127) | 128)
        value >>= 7
    output.append(value)
    return bytes(output)


def _field(number: int, value: int | str) -> bytes:
    if isinstance(value, int):
        return _varint(number << 3) + _varint(value)
    data = value.encode("utf-8")
    return _varint(number << 3 | 2) + _varint(len(data)) + data


def _cast_message(source: str, destination: str, namespace: str, data: dict) -> bytes:
    message = b"".join((_field(1, 0), _field(2, source),
                        _field(3, destination), _field(4, namespace),
                        _field(5, 0), _field(6, json.dumps(data, separators=(",", ":")))))
    return struct.pack(">I", len(message)) + message


def _read_varint(data: bytes, offset: int) -> tuple[int, int]:
    value = 0
    for shift in range(0, 70, 7):
        if offset >= len(data):
            raise ValueError("truncated Cast message")
        byte = data[offset]
        offset += 1
        value |= (byte & 127) << shift
        if not byte & 128:
            return value, offset
    raise ValueError("invalid Cast varint")


def _parse_cast_message(data: bytes) -> tuple[str, str, dict]:
    offset = 0
    fields: dict[int, bytes] = {}
    while offset < len(data):
        tag, offset = _read_varint(data, offset)
        wire = tag & 7
        if wire == 0:
            _, offset = _read_varint(data, offset)
        elif wire == 2:
            size, offset = _read_varint(data, offset)
            if offset + size > len(data):
                raise ValueError("truncated Cast message")
            fields[tag >> 3] = data[offset:offset + size]
            offset += size
        else:
            raise ValueError("unsupported Cast wire type")
    namespace = fields.get(4, b"").decode("utf-8", "replace")
    source = fields.get(2, b"").decode("utf-8", "replace")
    try:
        payload = json.loads(fields.get(6, b"{}"))
    except ValueError:
        payload = {}
    return namespace, source, payload if isinstance(payload, dict) else {}


class CastConnection:
    """Single receiver connection, with a reader keeping heartbeats alive."""

    def __init__(self, device: CastDevice) -> None:
        self.device = device
        self._socket: ssl.SSLSocket | None = None
        self._lock = threading.RLock()
        self._changed = threading.Condition(self._lock)
        self._messages: list[tuple[str, str, dict]] = []
        self._closed = False
        self._reader: threading.Thread | None = None
        self._request_id = 0
        self._transport_id: str | None = None
        self._session_id: int | None = None
        self._media_status: dict = {}

    def connect(self, timeout: float = 8.0) -> None:
        # Cast receivers use device certificates rather than Web PKI names.
        context = ssl.SSLContext(ssl.PROTOCOL_TLS_CLIENT)
        context.check_hostname = False
        context.verify_mode = ssl.CERT_NONE
        try:
            raw = socket.create_connection((self.device.host, self.device.port), timeout)
            self._socket = context.wrap_socket(raw, server_hostname=self.device.host)
            self._socket.settimeout(1.0)
        except OSError as error:
            if 'raw' in locals():
                raw.close()
            raise CastError("cast.error.connect", name=self.device.name) from error
        self._reader = threading.Thread(target=self._read_loop, daemon=True)
        self._reader.start()
        self._send("receiver-0", _CONNECTION, {"type": "CONNECT"})

    def _send(self, destination: str, namespace: str, data: dict) -> None:
        with self._lock:
            if self._closed or self._socket is None:
                raise CastError("cast.error.lost")
            try:
                self._socket.sendall(_cast_message("sender-0", destination, namespace, data))
            except OSError as error:
                self._closed = True
                self._changed.notify_all()
                raise CastError("cast.error.lost") from error

    def _read_exact(self, count: int) -> bytes:
        sock = self._socket
        if sock is None:
            raise OSError("connection closed")
        result = bytearray()
        while len(result) < count and not self._closed:
            try:
                piece = sock.recv(count - len(result))
            except socket.timeout:
                continue
            if not piece:
                raise OSError("connection closed")
            result.extend(piece)
        return bytes(result)

    def _read_loop(self) -> None:
        next_ping = time.monotonic() + 5
        try:
            while not self._closed:
                if time.monotonic() >= next_ping:
                    self._send("receiver-0", _HEARTBEAT, {"type": "PING"})
                    next_ping = time.monotonic() + 5
                sock = self._socket
                if sock is None:
                    break
                ready = [sock] if sock.pending() else select.select(
                    [sock], [], [], max(0.0, next_ping - time.monotonic()))[0]
                if not ready:
                    continue
                try:
                    header = self._read_exact(4)
                except OSError:
                    break
                if len(header) != 4:
                    break
                length = struct.unpack(">I", header)[0]
                if length > 1_000_000:
                    break
                data = self._read_exact(length)
                namespace, source, payload = _parse_cast_message(data)
                if namespace == _HEARTBEAT and payload.get("type") == "PING":
                    self._send("receiver-0", _HEARTBEAT, {"type": "PONG"})
                with self._changed:
                    if namespace == _MEDIA and payload.get("status"):
                        self._media_status = payload["status"][0]
                    self._messages.append((namespace, source, payload))
                    self._messages = self._messages[-50:]
                    self._changed.notify_all()
        except (OSError, ValueError, CastError, TypeError):
            pass
        finally:
            with self._changed:
                self._closed = True
                self._changed.notify_all()

    def _wait(self, predicate: Callable[[str, dict], bool], timeout: float = 10) -> dict:
        deadline = time.monotonic() + timeout
        with self._changed:
            while True:
                for index, (namespace, _source, payload) in enumerate(self._messages):
                    if predicate(namespace, payload):
                        self._messages.pop(index)
                        return payload
                if self._closed:
                    raise CastError("cast.error.lost")
                remaining = deadline - time.monotonic()
                if remaining <= 0:
                    raise CastError("cast.error.timeout")
                self._changed.wait(remaining)

    def _next_request(self) -> int:
        self._request_id += 1
        return self._request_id

    def launch(self) -> None:
        self._send("receiver-0", _RECEIVER, {"type": "GET_STATUS", "requestId": self._next_request()})
        status = self._wait(lambda ns, data: ns == _RECEIVER and data.get("type") == "RECEIVER_STATUS")
        apps = status.get("status", {}).get("applications", [])
        active = next((app for app in apps if app.get("appId") == _DEFAULT_MEDIA_APP), None)
        if active is None:
            self._send("receiver-0", _RECEIVER, {"type": "LAUNCH", "appId": _DEFAULT_MEDIA_APP,
                                               "requestId": self._next_request()})
            status = self._wait(lambda ns, data: ns == _RECEIVER and data.get("type") == "RECEIVER_STATUS"
                                and any(app.get("appId") == _DEFAULT_MEDIA_APP for app in
                                        data.get("status", {}).get("applications", [])), timeout=15)
            active = next(app for app in status["status"]["applications"]
                          if app.get("appId") == _DEFAULT_MEDIA_APP)
        self._transport_id = active.get("transportId")
        if not isinstance(self._transport_id, str):
            raise CastError("cast.error.transport")
        self._send(self._transport_id, _CONNECTION, {"type": "CONNECT"})

    def load(self, url: str, mime: str, title: str, *, current_time: float = 0,
             live: bool = False, autoplay: bool = True) -> None:
        if not self._transport_id:
            self.launch()
        request_id = self._next_request()
        with self._changed:
            self._messages = [item for item in self._messages if item[0] != _MEDIA]
        data = {"type": "LOAD", "requestId": request_id,
                "media": {"contentId": url, "contentType": mime, "streamType": "LIVE" if live else "BUFFERED",
                          "metadata": {"metadataType": 0, "title": title}},
                "autoplay": autoplay, "currentTime": max(0.0, current_time)}
        self._send(self._transport_id or "", _MEDIA, data)
        status = self._wait(lambda ns, value: ns == _MEDIA and
                            (value.get("type") in ("LOAD_FAILED", "LOAD_CANCELLED", "INVALID_REQUEST")
                             or (value.get("type") == "MEDIA_STATUS" and any(
                                 item.get("media", {}).get("contentId") == url
                                 for item in value.get("status", [])))), timeout=20)
        if status.get("type") != "MEDIA_STATUS":
            raise CastError("cast.error.unplayable")
        media_status = status["status"][0]
        if media_status.get("playerState") == "IDLE" and media_status.get("idleReason") == "ERROR":
            raise CastError("cast.error.unplayable")
        self._session_id = media_status.get("mediaSessionId")
        if media_status.get("playerState") == "BUFFERING":
            confirmed = self._wait(lambda ns, value: ns == _MEDIA and any(
                item.get("mediaSessionId") == self._session_id and item.get("playerState") in
                ("PLAYING", "PAUSED", "IDLE") for item in value.get("status", [])), timeout=20)
            current = next(item for item in confirmed["status"] if item.get("mediaSessionId") == self._session_id)
            if current.get("playerState") == "IDLE":
                raise CastError("cast.error.notStarted")

    def receiver_status(self) -> dict:
        request_id = self._next_request()
        self._send("receiver-0", _RECEIVER, {"type": "GET_STATUS", "requestId": request_id})
        result = self._wait(lambda ns, data: ns == _RECEIVER and data.get("requestId") == request_id
                            and data.get("type") == "RECEIVER_STATUS", 5)
        return result.get("status", {})

    def status(self) -> dict:
        if not self._transport_id:
            return {}
        request_id = self._next_request()
        with self._changed:
            self._messages = [item for item in self._messages if item[0] != _MEDIA]
        self._send(self._transport_id, _MEDIA, {"type": "GET_STATUS", "requestId": request_id,
                                              "mediaSessionId": self._session_id})
        result = self._wait(lambda ns, data: ns == _MEDIA and
                            data.get("type") == "MEDIA_STATUS" and
                            (data.get("requestId") == request_id or
                             any(item.get("mediaSessionId") == self._session_id
                                 for item in data.get("status", []))), 8)
        return result.get("status", [{}])[0] if result.get("status") else {}

    def set_volume(self, level: float, *, muted: bool | None = None) -> None:
        volume: dict = {"level": max(0.0, min(1.0, level))}
        if muted is not None:
            volume["muted"] = muted
        self._send("receiver-0", _RECEIVER, {"type": "SET_VOLUME", "volume": volume,
                                           "requestId": self._next_request()})

    def transport(self, command: str, **fields: object) -> None:
        if not self._transport_id or not self._session_id:
            raise CastError("cast.error.noPlayback")
        self._send(self._transport_id, _MEDIA, {"type": command,
                   "requestId": self._next_request(), "mediaSessionId": self._session_id, **fields})

    def close(self) -> None:
        with self._changed:
            self._closed = True
            sock = self._socket
            self._socket = None
            self._changed.notify_all()
        if sock is not None:
            try:
                sock.shutdown(socket.SHUT_RDWR)
            except OSError:
                pass
            try:
                sock.close()
            except OSError:
                pass
        if self._reader is not None and self._reader is not threading.current_thread():
            self._reader.join(timeout=2)
