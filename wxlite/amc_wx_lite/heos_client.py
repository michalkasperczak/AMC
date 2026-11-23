"""HEOS CLI transport with separate read and explicit control clients.

No account credentials are sent by this module. A request owns the connection
until its final response so late replies cannot be mistaken for a later
request after a timeout.
"""

from __future__ import annotations

import hashlib
import ipaddress
import json
import re
import socket
import threading
import time
from collections.abc import Callable
from dataclasses import dataclass
from typing import Any
from urllib.parse import urlencode


_COMMAND = re.compile(r"^[a-z]+/[a-z_]+$")
_READ_COMMANDS = frozenset({
    "system/check_account", "player/get_players", "player/get_player_info",
    "player/get_play_state", "player/get_now_playing_media",
    "player/get_volume", "player/get_mute", "player/get_queue",
    "group/get_groups", "browse/get_music_sources", "browse/browse",
})
_CONTROL_COMMANDS = frozenset({
    "player/set_play_state", "player/play_next", "player/play_previous",
    "player/set_volume", "player/set_mute", "player/play_queue",
    "browse/add_to_queue", "browse/play_stream",
})
_MAX_LINE = 4 * 1024 * 1024
_SSDP_TARGET = "urn:schemas-denon-com:device:ACT-Denon:1"


class HeosError(RuntimeError):
    """A connection, protocol, or command failure; never show raw text in UI."""


class HeosTimeout(HeosError):
    pass


class HeosUnavailable(HeosError):
    pass


class HeosProtocolError(HeosError):
    pass


class HeosListUnavailable(HeosError):
    """A source succeeded but supplied no list; this is not an empty list."""

    def __init__(self, command: str) -> None:
        super().__init__("To źródło nie udostępnia listy przez HEOS.")
        self.command = command


class HeosCommandError(HeosError):
    def __init__(self, command: str, message: str) -> None:
        super().__init__(f"HEOS command {command} failed")
        self.command = command
        self.message = message  # Diagnostic only; may contain account data.


@dataclass(frozen=True, slots=True)
class HeosPage:
    items: tuple[dict[str, Any], ...]
    next_start: int
    has_more: bool
    total: int | None


def _local_address(host: str) -> str:
    try:
        address = ipaddress.ip_address(host)
    except ValueError as error:
        raise ValueError("Podaj adres IP urządzenia HEOS.") from error
    if address.is_unspecified or address.is_multicast or address.is_loopback or not (
        address.is_private or address.is_link_local
    ):
        raise ValueError("Urządzenie HEOS musi być w sieci lokalnej.")
    return str(address)


class HeosReadClient:
    """One blocking read-only CLI connection, intended for a worker thread."""

    _allowed_commands = _READ_COMMANDS

    def __init__(
        self,
        host: str,
        *,
        port: int = 1255,
        timeout: float = 4.0,
        connector: Callable[..., socket.socket] = socket.create_connection,
    ) -> None:
        self.host = _local_address(host)
        self.port = port
        self.timeout = max(0.2, float(timeout))
        self._connector = connector
        self._socket: socket.socket | None = None
        self._buffer = bytearray()
        self._lock = threading.RLock()
        self.events: list[dict[str, Any]] = []

    def close(self) -> None:
        with self._lock:
            sock, self._socket = self._socket, None
            self._buffer.clear()
            if sock is not None:
                try:
                    sock.close()
                except OSError:
                    pass

    def __enter__(self) -> HeosReadClient:
        return self

    def __exit__(self, *_: object) -> None:
        self.close()

    def _connect(self, deadline: float) -> socket.socket:
        if self._socket is None:
            try:
                self._socket = self._connector(
                    (self.host, self.port), timeout=max(0.01, deadline - time.monotonic())
                )
            except (OSError, TimeoutError) as error:
                raise HeosUnavailable("Urządzenie HEOS nie odpowiada.") from error
        return self._socket

    def _line(self, sock: socket.socket, deadline: float) -> bytes:
        while True:
            marker = self._buffer.find(b"\r\n")
            if marker >= 0:
                line = bytes(self._buffer[:marker])
                del self._buffer[:marker + 2]
                return line
            if len(self._buffer) > _MAX_LINE:
                raise HeosProtocolError("Odpowiedź HEOS przekracza limit.")
            remaining = deadline - time.monotonic()
            if remaining <= 0:
                raise HeosTimeout("Przekroczono czas oczekiwania na HEOS.")
            try:
                sock.settimeout(remaining)
                chunk = sock.recv(65536)
            except socket.timeout as error:
                raise HeosTimeout("Przekroczono czas oczekiwania na HEOS.") from error
            except OSError as error:
                raise HeosUnavailable("Połączenie z HEOS zostało przerwane.") from error
            if not chunk:
                raise HeosUnavailable("Urządzenie HEOS zamknęło połączenie.")
            self._buffer.extend(chunk)

    def request(self, command: str, **params: object) -> dict[str, Any]:
        if command not in self._allowed_commands or not _COMMAND.fullmatch(command):
            raise ValueError("Nieobsługiwane polecenie HEOS.")
        query = urlencode({key: str(value) for key, value in params.items()}, safe=",")
        line = f"heos://{command}" + (f"?{query}" if query else "") + "\r\n"
        deadline = time.monotonic() + self.timeout
        with self._lock:
            try:
                sock = self._connect(deadline)
                sock.settimeout(max(0.01, deadline - time.monotonic()))
                sock.sendall(line.encode("utf-8"))
                while True:
                    raw = self._line(sock, deadline)
                    try:
                        response = json.loads(raw)
                    except (UnicodeError, json.JSONDecodeError) as error:
                        raise HeosProtocolError("HEOS zwrócił nieprawidłowy JSON.") from error
                    if not isinstance(response, dict) or not isinstance(response.get("heos"), dict):
                        raise HeosProtocolError("HEOS zwrócił nieprawidłową odpowiedź.")
                    header = response["heos"]
                    received = str(header.get("command", "")).strip()
                    if received.startswith("event/"):
                        self.events.append(response)
                        if len(self.events) > 100:
                            del self.events[:-100]
                        continue
                    if received != command:
                        raise HeosProtocolError("HEOS zwrócił odpowiedź innego polecenia.")
                    result = header.get("result")
                    message = str(header.get("message", ""))
                    if result == "fail":
                        raise HeosCommandError(command, message)
                    if result != "success":
                        raise HeosProtocolError("HEOS zwrócił nieznany wynik.")
                    if message.strip().lower() == "command under process":
                        continue
                    return response
            except (HeosTimeout, HeosUnavailable, HeosProtocolError, HeosCommandError):
                self.close()
                raise
            except OSError as error:
                self.close()
                raise HeosUnavailable("Połączenie z HEOS zostało przerwane.") from error

    def players(self) -> list[dict[str, Any]]:
        return self._list("player/get_players")

    def groups(self) -> list[dict[str, Any]]:
        return self._list("group/get_groups")

    def sources(self) -> list[dict[str, Any]]:
        return self._list("browse/get_music_sources")

    def _list(self, command: str, **params: object) -> list[dict[str, Any]]:
        payload = self.request(command, **params).get("payload")
        if not isinstance(payload, list):
            raise HeosProtocolError("HEOS nie zwrócił listy.")
        return [item for item in payload if isinstance(item, dict)]

    def player_state(self, pid: str) -> dict[str, dict[str, Any]]:
        return {
            "info": self.request("player/get_player_info", pid=pid),
            "play": self.request("player/get_play_state", pid=pid),
            "media": self.request("player/get_now_playing_media", pid=pid),
            "volume": self.request("player/get_volume", pid=pid),
            "mute": self.request("player/get_mute", pid=pid),
        }

    def queue(self, pid: str, *, page_size: int = 100, max_items: int = 10000) -> list[dict[str, Any]]:
        return self._pages("player/get_queue", {"pid": pid}, page_size, max_items)

    def browse(self, sid: str, cid: str | None = None, *, page_size: int = 100,
               max_items: int = 10000) -> list[dict[str, Any]]:
        params = {"sid": sid}
        if cid is not None:
            params["cid"] = cid
        return self._pages("browse/browse", params, page_size, max_items)

    def browse_page(self, sid: str, cid: str | None = None, *, start: int = 0,
                    limit: int = 100) -> HeosPage:
        params = {"sid": sid}
        if cid is not None:
            params["cid"] = cid
        return self._page("browse/browse", params, start, limit)

    def queue_page(self, pid: str, *, start: int = 0, limit: int = 100) -> HeosPage:
        return self._page("player/get_queue", {"pid": pid}, start, limit)

    def _page(self, command: str, params: dict[str, str], start: int,
              limit: int) -> HeosPage:
        if isinstance(start, bool) or not isinstance(start, int) or start < 0:
            raise ValueError("Nieprawidłowy początek strony HEOS.")
        if isinstance(limit, bool) or not isinstance(limit, int) or not 1 <= limit <= 100:
            raise ValueError("Nieprawidłowy rozmiar strony HEOS.")
        response = self.request(command, **params, range=f"{start},{start + limit - 1}")
        payload = response.get("payload")
        if payload is None:
            raise HeosListUnavailable(command)
        if not isinstance(payload, list) or len(payload) > limit or any(
            not isinstance(item, dict) for item in payload
        ):
            raise HeosProtocolError("HEOS zwrócił uszkodzoną stronę listy.")
        message = str(response["heos"].get("message", ""))
        count_match = re.search(r"(?:^|&)count=['\"]?(\d+)", message)
        reported = int(count_match.group(1)) if count_match else 0
        total = reported or None  # HEOS uses zero for an unknown total.
        next_start = start + len(payload)
        return HeosPage(
            items=tuple(payload),
            next_start=next_start,
            has_more=(next_start < total if total is not None else bool(payload)),
            total=total,
        )

    def _pages(self, command: str, params: dict[str, str], page_size: int,
               max_items: int) -> list[dict[str, Any]]:
        if not 1 <= page_size <= 100 or max_items < 1:
            raise ValueError("Nieprawidłowy zakres strony HEOS.")
        result: list[dict[str, Any]] = []
        while len(result) < max_items:
            start = len(result)
            end = min(start + page_size, max_items) - 1
            response = self.request(command, **params, range=f"{start},{end}")
            payload = response.get("payload")
            if payload is None:
                raise HeosListUnavailable(command)
            if not isinstance(payload, list):
                raise HeosProtocolError("HEOS nie zwrócił listy.")
            page = [item for item in payload if isinstance(item, dict)]
            if len(page) != len(payload):
                raise HeosProtocolError("HEOS zwrócił uszkodzoną stronę listy.")
            if len(page) > end - start + 1:
                raise HeosProtocolError("HEOS zwrócił stronę poza żądanym zakresem.")
            result.extend(page)
            if not page:
                break
            message = str(response["heos"].get("message", ""))
            count_match = re.search(r"(?:^|&)count=['\"]?(\d+)", message)
            total = int(count_match.group(1)) if count_match else 0
            if total and len(result) >= total:
                break
        return result


class HeosControlClient(HeosReadClient):
    """Explicit mutations. Timeouts never repeat a command automatically."""

    _allowed_commands = _READ_COMMANDS | _CONTROL_COMMANDS

    def set_play_state(self, pid: str, state: str) -> None:
        if state not in {"play", "pause", "stop"}:
            raise ValueError("Nieznany stan odtwarzania HEOS.")
        self.request("player/set_play_state", pid=pid, state=state)

    def next(self, pid: str) -> None:
        self.request("player/play_next", pid=pid)

    def previous(self, pid: str) -> None:
        self.request("player/play_previous", pid=pid)

    def set_volume(self, pid: str, level: int) -> None:
        if isinstance(level, bool) or not isinstance(level, int) or not 0 <= level <= 100:
            raise ValueError("Głośność HEOS musi być od 0 do 100.")
        self.request("player/set_volume", pid=pid, level=level)

    def set_mute(self, pid: str, muted: bool) -> None:
        if not isinstance(muted, bool):
            raise ValueError("Nieprawidłowy stan wyciszenia HEOS.")
        self.request("player/set_mute", pid=pid, state="on" if muted else "off")

    def add_to_queue(
        self, pid: str, sid: str, *, cid: str = "", mid: str = "",
        placement: str = "now", replace_confirmed: bool = False,
    ) -> None:
        """Use distinct HEOS placements; a replace requires explicit confirmation."""

        criteria = {"now": 1, "next": 2, "end": 3, "replace": 4}
        if placement not in criteria or (placement == "replace" and not replace_confirmed):
            raise ValueError("Nie potwierdzono sposobu dodania do kolejki HEOS.")
        if not pid or not sid or (not cid and not mid):
            raise ValueError("Brakuje celu lub pozycji do kolejki HEOS.")
        params = {"pid": pid, "sid": sid}
        if cid:
            params["cid"] = cid
        if mid:
            params["mid"] = mid
        params["aid"] = criteria[placement]
        self.request("browse/add_to_queue", **params)

    def play_station(self, pid: str, sid: str, mid: str, name: str,
                     *, parent_cid: str = "") -> None:
        if not pid or not sid or not mid or not isinstance(name, str) or not name.strip():
            raise ValueError("Brakuje danych stacji HEOS.")
        params = {"pid": pid, "sid": sid}
        if parent_cid:
            params["cid"] = parent_cid
        params.update(mid=mid, name=name)
        self.request("browse/play_stream", **params)

    def play_queue_if_current(
        self, pid: str, qid: str, expected_fingerprint: str,
    ) -> None:
        """Check the current queue before using HEOS's shifting queue index."""

        if not qid or not expected_fingerprint:
            raise ValueError("Nie wybrano aktualnej pozycji kolejki HEOS.")
        current = self.queue(pid)
        if queue_fingerprint(current) != expected_fingerprint or not any(
            str(item.get("qid", "")) == qid for item in current
        ):
            raise HeosError("Kolejka HEOS zmieniła się. Odśwież listę przed odtworzeniem.")
        self.request("player/play_queue", pid=pid, qid=qid)


def queue_fingerprint(items: list[dict[str, Any]]) -> str:
    """A private comparison token, never a spoken queue label."""

    fields = tuple(
        (item.get("qid"), item.get("mid"), item.get("song"),
         item.get("album"), item.get("artist"))
        for item in items
    )
    encoded = json.dumps(fields, ensure_ascii=False, sort_keys=True, default=str).encode("utf-8")
    return hashlib.sha256(encoded).hexdigest()


def parse_ssdp_response(data: bytes, source_ip: str) -> str | None:
    """Return a local HEOS host only for the documented SSDP device type."""

    try:
        lines = data.decode("utf-8", "replace").splitlines()
        headers = {}
        for line in lines[1:]:
            if ":" in line:
                key, value = line.split(":", 1)
                headers[key.strip().lower()] = value.strip()
        if headers.get("st", "").lower() != _SSDP_TARGET.lower():
            return None
        return _local_address(source_ip)
    except ValueError:
        return None


def discover_heos(
    *, timeout: float = 4.0, interface_ips: tuple[str, ...] | None = None,
    socket_factory: Callable[..., socket.socket] = socket.socket,
) -> tuple[str, ...]:
    """Search local IPv4 interfaces without contacting HEOS accounts or media."""

    if interface_ips is None:
        try:
            interface_ips = tuple(socket.gethostbyname_ex(socket.gethostname())[2])
        except OSError as error:
            raise HeosUnavailable("Nie można odczytać kart sieciowych.") from error
    candidates = []
    for value in interface_ips:
        try:
            address = ipaddress.IPv4Address(value)
            if _local_address(value) == str(address):
                candidates.append(str(address))
        except ValueError:
            continue
    if not candidates:
        raise HeosUnavailable("Brak dostępnego połączenia z siecią lokalną.")
    request = (
        "M-SEARCH * HTTP/1.1\r\nHOST: 239.255.255.250:1900\r\n"
        'MAN: "ssdp:discover"\r\nMX: 2\r\nST: '
        + _SSDP_TARGET + "\r\n\r\n"
    ).encode("ascii")
    found: set[str] = set()
    accessible = False
    for address in candidates:
        sock = socket_factory(socket.AF_INET, socket.SOCK_DGRAM, socket.IPPROTO_UDP)
        try:
            sock.setsockopt(socket.IPPROTO_IP, socket.IP_MULTICAST_TTL, 2)
            sock.setsockopt(socket.IPPROTO_IP, socket.IP_MULTICAST_IF, socket.inet_aton(address))
            sock.bind((address, 0))
            sock.settimeout(0.25)
            for _ in range(2):
                sock.sendto(request, ("239.255.255.250", 1900))
            accessible = True
            deadline = time.monotonic() + max(0.2, float(timeout))
            while time.monotonic() < deadline:
                try:
                    packet, source = sock.recvfrom(16384)
                except socket.timeout:
                    continue
                host = parse_ssdp_response(packet, source[0])
                if host is not None:
                    found.add(host)
        except OSError:
            continue
        finally:
            sock.close()
    if not accessible:
        raise HeosUnavailable("Nie można wyszukać HEOS w sieci lokalnej.")
    return tuple(sorted(found))
