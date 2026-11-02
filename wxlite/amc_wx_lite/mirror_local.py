"""Second local AMC output in an isolated host process.

The second host decodes the same AMC material independently. Its clock may drift
from the primary output; no sample-accurate synchronization is implied.
"""
from __future__ import annotations

from dataclasses import dataclass
from typing import Callable, Any

from .mirror_core import MIRROR_SESSIONS, PLAY_OPERATIONS


@dataclass(frozen=True, slots=True)
class MirrorChoice:
    device_id: str
    label: str

    def __str__(self) -> str:
        return self.label


@dataclass
class LocalRoute:
    device_id: str
    name: str
    client: Any

    def close(self) -> None:
        self.client.close()


class MirrorLocalRouter:
    def __init__(self, native: Callable, create_client: Callable,
                 event: Callable | None = None) -> None:
        self.native = native
        self.create_client = create_client
        self.event = event
        self.routes: dict[str, LocalRoute] = {}

    def selected_device_id(self, session: str) -> str | None:
        route = self.routes.get(session)
        return route.device_id if route else None

    @staticmethod
    def _play(route: LocalRoute, session: str, status: dict) -> None:
        source = str(status.get("source") or "")
        if not source:
            raise ValueError("Brak materiału do dublowania")
        common = {
            "id": status.get("id") or source,
            "title": status.get("title") or "Materiał",
            "volume": status.get("volume", 35),
            "positionSeconds": max(0.0, float(status.get("positionSeconds") or 0)),
        }
        if session == "files":
            op, args = "files.play", {**common, "path": source,
                                      "rate": status.get("rate", 1.0)}
        elif session == "radio":
            op, args = "radio.play", {**common, "url": source}
        else:
            op, args = "media.play", {**common, "source": source,
                                      "sessionId": session,
                                      "rate": status.get("rate", 1.0)}
        route.client.call(op, args, timeout=120)
        if status.get("paused"):
            route.client.call("transport.pauseResume", {"sessionId": session})

    def enable(self, session: str, device_id: str, name: str) -> dict:
        if session not in MIRROR_SESSIONS or not device_id or not name.strip():
            raise ValueError("Nie można wybrać drugiego wyjścia audio")
        previous = self.routes.get(session)
        if previous and previous.device_id == device_id:
            return {"name": previous.name, "synchronized": False}
        status = self.native("transport.status", {"sessionId": session})
        if not status.get("source") or not (status.get("playing") or status.get("paused")):
            raise ValueError("Najpierw uruchom materiał w AMC")
        client = self.create_client()
        route = LocalRoute(device_id, name.strip(), client)
        try:
            client.start()
            selected = client.call("audio.selectOutput", {"sessionId": session,
                                                          "deviceId": device_id,
                                                          "restart": False})
            if selected.get("usingDefault"):
                raise ValueError("Wybrane drugie wyjście audio jest niedostępne")
            self._play(route, session, status)
        except Exception:
            route.close()
            raise
        self.routes[session] = route
        if previous is not None:
            previous.close()
        return {"name": route.name, "synchronized": False}

    def disable(self, session: str) -> dict:
        route = self.routes.pop(session, None)
        if route:
            route.close()
        return {"active": False}

    def after_native(self, op: str, args: dict, result: dict | None) -> None:
        session = PLAY_OPERATIONS.get(op, args.get("sessionId"))
        route = self.routes.get(session)
        if route is None:
            return
        try:
            if op in PLAY_OPERATIONS:
                status = self.native("transport.status", {"sessionId": session})
                self._play(route, session, status)
            elif op == "transport.stop":
                self.disable(session)
            elif op in ("transport.pauseResume", "transport.seek",
                        "transport.setVolume", "transport.setRate",
                        "transport.toggleMute"):
                route.client.call(op, args)
        except Exception:
            self.routes.pop(session, None)
            route.close()
            if self.event is not None:
                self.event("mirror.failed", {"sessionId": session,
                                             "message": "Utracono drugie wyjście audio; odtwarzanie lokalne trwa dalej"})

    def close(self) -> None:
        for session in tuple(self.routes):
            self.disable(session)
