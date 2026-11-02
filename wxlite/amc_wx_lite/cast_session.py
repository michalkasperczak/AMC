"""Session-owned Cast playback; native host remains the owner of local playback."""
from __future__ import annotations

import threading
from dataclasses import dataclass, field, replace
from typing import Callable
from . import i18n

from .cast_media import CastFileShare, cast_source
from .cast_protocol import CastConnection, CastDevice, CastError, discover_cast_devices


CAST_SESSIONS = ("files", "radio", "podcasts")
UNHANDLED = object()


def cast_session_available(session: str, status: dict | None = None, *, active: bool = False) -> bool:
    if session not in CAST_SESSIONS:
        return False
    if active:
        return True
    status = status or {}
    if status.get("source") and (status.get("playing") or status.get("paused")):
        try:
            cast_source(status["source"])
        except (CastError, OSError, ValueError):
            return False
    return True


@dataclass
class CastRoute:
    device: CastDevice
    connection: CastConnection
    share: CastFileShare | None = None
    item: dict = field(default_factory=dict)
    volume: int = 20
    muted: bool = False
    stopped: bool = True
    failed: bool = False
    ended: bool = False
    native_volume: int = 20
    native_rate: float = 1.0

    def close(self) -> None:
        self.connection.close()
        if self.share is not None:
            self.share.close()


def cast_action_available(action: object, active: bool, session: str | None = None,
                          queue_view: bool = False) -> bool:
    """One capability predicate shared by dispatch, menus, palette and help."""
    if not active:
        return True
    name = getattr(action, "name", "")
    if queue_view and name == "ACTIVATE":
        return False
    if session == "radio" and name.startswith(("SEEK_", "TRACK_")):
        return False
    return not (name.startswith(("RATE_", "CLIP_", "PREVIOUS_BOOKMARK", "NEXT_BOOKMARK",
                                "PREVIOUS_NAMED_BOOKMARK", "NEXT_NAMED_BOOKMARK"))
                or name in {"ADD_BOOKMARK", "ADD_NAMED_BOOKMARK", "ADD_NAMED_CHAPTER", "VIEW_CHAPTERS",
                            "PREVIOUS_CHAPTER", "NEXT_CHAPTER", "RADIO_JUMP_LIVE", "TOGGLE_SHUFFLE",
                            "TOGGLE_PLAYBACK_SHUFFLE", "QUEUE_NEXT", "QUEUE_PREVIOUS"}
                or "AUDIO_CLIP" in name)


class CastSessionRouter:
    def __init__(self, native: Callable, event: Callable | None = None,
                 *, discover=discover_cast_devices, connect=CastConnection,
                 share=CastFileShare) -> None:
        self.native = native
        self.event = event
        self._discover = discover
        self._connect = connect
        self._share = share
        self._devices: tuple[CastDevice, ...] | None = None
        self.routes: dict[str, CastRoute] = {}
        self._lock = threading.RLock()

    def devices(self, refresh: bool = False) -> tuple[CastDevice, ...]:
        with self._lock:
            if self._devices is None or refresh:
                found = tuple(self._discover())
                ids = {device.device_id for device in found}
                self._devices = found + tuple(replace(route.device, available=False)
                    for route in self.routes.values() if route.device.device_id not in ids)
            return self._devices

    def active(self, session: str) -> bool:
        return session in self.routes

    def select(self, session: str, device_id: str | None) -> dict:
        if session not in CAST_SESSIONS:
            raise CastError("cast.error.session")
        with self._lock:
            if device_id is None:
                return self._return_local(session)
            device = next((d for d in self.devices(refresh=True) if d.device_id == device_id), None)
            if device is None or not device.available:
                raise CastError("cast.error.deviceMissing")
            if any(key != session and route.device.device_id == device_id
                   for key, route in self.routes.items()):
                raise CastError("cast.error.inUse")
            previous = self.routes.get(session)
            if previous and not previous.failed and previous.device.device_id == device_id:
                return {"name": device.name}
            status = ({**previous.item, "paused": True} if previous and previous.failed else
                      self.status(session) if previous else self.native(
                          "transport.status", {"sessionId": session}))
            if status.get("source") and (status.get("playing") or status.get("paused")):
                cast_source(status["source"])
            connection = self._connect(device)
            connection.connect()
            route = CastRoute(device, connection)
            route.native_volume = previous.native_volume if previous else int(status.get("volume", 20))
            route.native_rate = previous.native_rate if previous else float(status.get("rate", 1.0))
            was_playing = bool(status.get("playing"))
            paused_native = False
            try:
                volume = connection.receiver_status().get("volume", {})
                route.volume = round(float(volume.get("level", 0.2)) * 100)
                route.muted = bool(volume.get("muted", False))
                if status.get("source") and (was_playing or status.get("paused")):
                    if not previous and was_playing and not status.get("paused"):
                        self.native("transport.pauseResume", {"sessionId": session})
                        paused_native = True
                    self._load(route, session, status, paused=bool(status.get("paused")))
                    if previous and not previous.failed:
                        previous.connection.transport("STOP")
                    elif not previous:
                        self.native("transport.stop", {"sessionId": session})
                self.routes[session] = route
                if previous:
                    previous.close()
                return {"name": device.name}
            except Exception:
                self._devices = None
                try:
                    if not route.stopped:
                        route.connection.transport("STOP")
                finally:
                    route.close()
                    if paused_native:
                        self.native("transport.pauseResume", {"sessionId": session})
                raise

    def _load(self, route: CastRoute, session: str, item: dict, *, paused: bool = False) -> dict:
        source = str(item.get("source") or item.get("path") or item.get("url") or "")
        url, mime, path = cast_source(source)
        share = self._share(path, route.device.host, mime) if path else None
        try:
            route.connection.load(share.url if share else url, mime, str(item.get("title") or ""),
                                  current_time=float(item.get("positionSeconds") or 0),
                                  live=session == "radio", autoplay=not paused)
        except Exception:
            if share:
                share.close()
            raise
        old_share = route.share
        route.share = share
        route.item = {**item, "source": source}
        route.stopped = False
        route.failed = False
        route.ended = False
        if old_share:
            old_share.close()
        return self._status(route, session)

    def _status(self, route: CastRoute, session: str) -> dict:
        if route.failed:
            raise CastError("cast.error.reconnect")
        try:
            return self._status_live(route, session)
        except CastError:
            if not route.failed:
                route.failed = True
                if route.share:
                    route.share.close()
                    route.share = None
                if self.event:
                    self.event("playback.failed", {"engine": "radio" if session == "radio" else "files",
                        "sessionId": session, "cast": True,
                        "message": i18n.tr("cast.error.lostControl")})
            raise

    def _status_live(self, route: CastRoute, session: str) -> dict:
        state = {} if route.stopped else route.connection.status()
        receiver = route.connection.receiver_status()
        volume = receiver.get("volume", {})
        route.volume = round(float(volume.get("level", route.volume / 100)) * 100)
        route.muted = bool(volume.get("muted", route.muted))
        player_state = state.get("playerState", "IDLE")
        route.item["positionSeconds"] = float(state.get("currentTime", route.item.get("positionSeconds", 0)) or 0)
        if not route.ended and state.get("idleReason") == "FINISHED":
            route.ended = True
            route.stopped = True
            route.item["positionSeconds"] = 0
            if route.share:
                route.share.close()
                route.share = None
            if self.event:
                self.event("playback.ended", {"engine": "radio" if session == "radio" else "files",
                                              "sessionId": session,
                                              "id": route.item.get("id"), "cast": True})
        return {"engine": "radio" if session == "radio" else "files", "sessionId": session,
                "id": route.item.get("id"), "loadedId": route.item.get("id"),
                "title": route.item.get("title"), "source": route.item.get("source"),
                "playing": player_state in ("PLAYING", "BUFFERING"),
                "paused": player_state == "PAUSED", "muted": route.muted,
                "positionSeconds": float(state.get("currentTime") or 0),
                "durationSeconds": float(state.get("media", {}).get("duration") or 0),
                "volume": route.volume, "rate": 1.0, "bufferedSeconds": 0,
                "behindLiveSeconds": 0, "cast": True, "castDeviceName": route.device.name}

    def status(self, session: str) -> dict:
        with self._lock:
            return self._status(self.routes[session], session)

    def _return_local(self, session: str) -> dict:
        route = self.routes.get(session)
        if route is None:
            return {"name": "Ten komputer"}
        status = ({**route.item, "playing": False, "paused": True,
                   "positionSeconds": route.item.get("positionSeconds", 0)}
                  if route.failed else self._status(route, session))
        if route.item.get("source") and (status["playing"] or status["paused"]):
            if not route.failed:
                route.connection.transport("PAUSE")
            op = {"files": "files.play", "radio": "radio.play", "podcasts": "media.play"}[session]
            args = {**route.item, "positionSeconds": status["positionSeconds"],
                    "volume": 0 if status["paused"] else route.native_volume, "rate": route.native_rate}
            args["path" if session == "files" else "url" if session == "radio" else "source"] = route.item["source"]
            try:
                self.native(op, args)
                if status["paused"]:
                    self.native("transport.pauseResume", {"sessionId": session})
                    self.native("transport.setVolume", {"sessionId": session, "volume": route.native_volume})
            except Exception:
                if status["playing"] and not route.failed:
                    route.connection.transport("PLAY")
                raise
        warning = (i18n.tr("cast.error.stopUnconfirmed") if route.failed else None)
        try:
            if not route.stopped and not route.failed:
                route.connection.transport("STOP")
        except CastError:
            warning = i18n.tr("cast.error.stopUnconfirmed")
        finally:
            route.close()
            del self.routes[session]
        return {"name": "Ten komputer", "warning": warning}

    def _note_started(self, route: CastRoute, session: str, result: dict) -> None:
        try:
            history = self.native("cast.notePlayback", {"sessionId": session, "id": route.item.get("id")})
            history_error = (i18n.tr("cast.error.history") if (history or {}).get("historyError") else None)
        except Exception:
            history_error = i18n.tr("cast.error.history")
        if self.event:
            self.event("playback.started", {"engine": result["engine"], "id": route.item.get("id"),
                                           "sessionId": session,
                                           "title": route.item.get("title"), "cast": True,
                                           "historyError": history_error})

    def call(self, op: str, args: dict) -> object:
        session = args.get("sessionId")
        play_session = {"files.play": "files", "radio.play": "radio", "media.play": "podcasts"}.get(op)
        if play_session:
            session = play_session
        if op in ("podcast.checkpoint", "local.checkpoint"):
            owner = "podcasts" if op.startswith("podcast") else "files"
            if owner in self.routes:
                return {"saved": False, "cast": True}
        if session not in self.routes:
            if op.startswith("queue.") and op in ("queue.playAt", "queue.next", "queue.previous") and any(
                    owner in self.routes for owner in ("files", "podcasts")):
                raise CastError("cast.error.queue")
            if play_session in ("files", "podcasts"):
                with self._lock:
                    other_session = "podcasts" if play_session == "files" else "files"
                    other = self.routes.get(other_session)
                    if other and not other.stopped:
                        was_paused = self._status(other, other_session)["paused"]
                        other.connection.transport("PAUSE")
                        try:
                            result = self.native(op, args)
                        except Exception:
                            if not was_paused:
                                other.connection.transport("PLAY")
                            raise
                        other.connection.transport("STOP")
                        other.stopped = True
                        if other.share:
                            other.share.close()
                            other.share = None
                        return result
            return UNHANDLED
        with self._lock:
            route = self.routes[session]
            if play_session:
                result = self._load(route, session, args)
                if session in ("files", "podcasts"):
                    other = self.routes.get("podcasts" if session == "files" else "files")
                    if other is not None and not other.stopped:
                        other.connection.transport("STOP")
                        other.stopped = True
                        if other.share:
                            other.share.close()
                            other.share = None
                self._note_started(route, session, result)
                return result
            if op == "transport.status":
                return self._status(route, session)
            if op == "transport.pauseResume":
                if route.failed:
                    raise CastError("cast.error.reconnect")
                if route.stopped:
                    if not route.item.get("source"):
                        raise CastError("cast.error.noMaterial")
                    route.item["positionSeconds"] = 0
                    result = self._load(route, session, route.item)
                    self._note_started(route, session, result)
                    return result
                paused = self._status(route, session)["paused"]
                route.connection.transport("PLAY" if paused else "PAUSE")
                return {"paused": not paused}
            if op == "transport.stop":
                if not route.stopped:
                    route.connection.transport("STOP")
                route.stopped = True
                route.item["positionSeconds"] = 0
                if route.share:
                    route.share.close()
                    route.share = None
                return self._status(route, session)
            if op in ("transport.setVolume", "transport.toggleMute"):
                volume = max(0, min(100, int(args.get("volume", route.volume))))
                muted = not route.muted if op.endswith("toggleMute") else route.muted
                route.connection.set_volume(volume / 100, muted=muted)
                route.volume, route.muted = volume, muted
                return {"volume": volume, "muted": muted}
            if op == "transport.seek":
                if session == "radio":
                    raise CastError("cast.error.live")
                position = args.get("positionSeconds")
                if position is None:
                    position = self._status(route, session)["positionSeconds"] + float(args.get("deltaSeconds", 0))
                route.connection.transport("SEEK", currentTime=max(0.0, float(position)))
                return {"positionSeconds": max(0.0, float(position))}
            if op == "transport.setRate":
                raise CastError("cast.error.rate")
            return UNHANDLED

    def close(self) -> None:
        with self._lock:
            for route in self.routes.values():
                try:
                    if not route.stopped:
                        route.connection.transport("STOP")
                except CastError:
                    pass
                route.close()
            self.routes.clear()
