"""Read-only HEOS CLI transport for the first hardware compatibility probe.

No account credentials or playback commands are sent by this module.  A
request owns the connection until its final response so late replies cannot
be mistaken for a later request after a timeout.
"""

from __future__ import annotations

import ipaddress
import json
import re
import socket
import threading
import time
from collections.abc import Callable
from typing import Any
from urllib.parse import urlencode


_COMMAND = re.compile(r"^[a-z]+/[a-z_]+$")
_READ_COMMANDS = frozenset({
    "system/check_account", "player/get_players", "player/get_player_info",
    "player/get_play_state", "player/get_now_playing_media",
    "player/get_volume", "player/get_mute", "player/get_queue",
    "group/get_groups", "browse/get_music_sources", "browse/browse",
})
_MAX_LINE = 4 * 1024 * 1024


class HeosError(RuntimeError):
    """A connection, protocol, or command failure; never show raw text in UI."""


class HeosTimeout(HeosError):
    pass


class HeosUnavailable(HeosError):
    pass


class HeosProtocolError(HeosError):
    pass


class HeosCommandError(HeosError):
    def __init__(self, command: str, message: str) -> None:
        super().__init__(f"HEOS command {command} failed")
        self.command = command
        self.message = message  # Diagnostic only; may contain account data.


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
        if command not in _READ_COMMANDS or not _COMMAND.fullmatch(command):
            raise ValueError("Ta próba HEOS dopuszcza tylko polecenia odczytowe.")
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
