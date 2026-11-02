"""A second Google Cast playback target alongside AMC's local audio output.

This core is intentionally independent of wx and of the existing Shift+C
switching route. The local host remains authoritative for playback.
"""

from __future__ import annotations

from dataclasses import dataclass
import time
from typing import Callable

from . import i18n
from .cast_media import CastFileShare, cast_source
from .cast_protocol import CastConnection, CastDevice, CastError, discover_cast_devices


MIRROR_SESSIONS = ("files", "radio", "podcasts")
PLAY_OPERATIONS = {"files.play": "files", "radio.play": "radio", "media.play": "podcasts"}


@dataclass
class MirrorRoute:
    device: CastDevice
    connection: CastConnection
    share: CastFileShare | None = None
    source: str = ""
    paused: bool = False

    def close(self) -> None:
        try:
            self.connection.close()
        finally:
            if self.share is not None:
                self.share.close()
                self.share = None


class MirrorCastRouter:
    """Mirror one AMC session to one Cast receiver without stopping local audio.

    The first stage is suitable for separate rooms: Cast and WASAPI have
    independent clocks. No synchronisation is claimed by this class.
    """

    def __init__(self, native: Callable, event: Callable | None = None,
                 *, discover=discover_cast_devices, connect=CastConnection,
                 share=CastFileShare) -> None:
        self.native = native
        self.event = event
        self._discover = discover
        self._connect = connect
        self._share = share
        self.routes: dict[str, MirrorRoute] = {}

    def selected_device_id(self, session: str) -> str | None:
        route = self.routes.get(session)
        return route.device.device_id if route else None

    def enable(self, session: str, device_id: str) -> dict:
        if session not in MIRROR_SESSIONS:
            raise CastError("cast.error.session")
        previous = self.routes.get(session)
        if previous is not None and previous.device.device_id == device_id:
            return {"name": previous.device.name, "synchronized": False}
        device = next((item for item in self._discover()
                       if item.device_id == device_id and item.available), None)
        if device is None:
            raise CastError("cast.error.deviceMissing")
        if any(key != session and route.device.device_id == device_id
               for key, route in self.routes.items()):
            raise CastError("cast.error.inUse")
        status = self.native("transport.status", {"sessionId": session})
        if not status.get("source") or not (status.get("playing") or status.get("paused")):
            raise CastError("cast.error.noMaterial")
        source = str(status["source"])
        cast_source(source)
        connection = self._connect(device)
        route = MirrorRoute(device, connection)
        try:
            connection.connect()
            self._load(route, session, status)
            self._align_file_route(route, session)
        except Exception:
            route.close()
            raise
        self.routes[session] = route
        if previous is not None:
            self._stop_and_close(previous)
        return {"name": device.name, "synchronized": False}

    def disable(self, session: str) -> dict:
        route = self.routes.pop(session, None)
        if route is None:
            return {"active": False}
        self._stop_and_close(route)
        return {"active": False}

    def _stop_and_close(self, route: MirrorRoute) -> None:
        try:
            route.connection.transport("STOP")
        except CastError:
            pass
        finally:
            route.close()

    def _load(self, route: MirrorRoute, session: str, item: dict) -> None:
        source = str(item.get("source") or item.get("path") or item.get("url") or "")
        url, mime, path = cast_source(source)
        share = self._share(path, route.device.host, mime) if path else None
        try:
            route.connection.load(
                share.url if share else url, mime, str(item.get("title") or ""),
                current_time=float(item.get("positionSeconds") or 0),
                live=session == "radio", autoplay=not bool(item.get("paused")),
            )
        except Exception:
            if share is not None:
                share.close()
            raise
        old_share = route.share
        route.share = share
        route.source = source
        route.paused = bool(item.get("paused"))
        if old_share is not None:
            old_share.close()

    def _align_file_route(self, route: MirrorRoute, session: str) -> None:
        """Best-effort catch-up after Cast's buffered LOAD has started.

        The local clock keeps moving while the receiver connects and buffers.
        A fresh position, read after LOAD, avoids reusing the old position
        captured before connection. This does not promise sample-accurate sync.
        """
        if session == "radio" or route.paused:
            return
        try:
            remote = route.connection.status()
            deadline = time.monotonic() + 5.0
            while remote.get("playerState") not in ("PLAYING", "PAUSED"):
                if time.monotonic() >= deadline:
                    return
                time.sleep(0.25)
                remote = route.connection.status()
            local = self.native("transport.status", {"sessionId": session})
            if (not local.get("playing") or local.get("paused")
                    or str(local.get("source") or "") != route.source
                    or remote.get("playerState") != "PLAYING"):
                return
            local_position = local.get("positionSeconds")
            remote_position = remote.get("currentTime")
            if local_position is None or remote_position is None:
                return
            local_seconds = float(local_position)
            remote_seconds = float(remote_position)
            if local_seconds - remote_seconds > 0.5:
                route.connection.transport("SEEK", currentTime=max(0.0, local_seconds))
        except Exception:
            # A timing adjustment must never cancel a working second target.
            return

    def after_native(self, op: str, args: dict, result: dict | None) -> None:
        """Mirror only a successfully completed local transport command.

        A receiver error removes the second target and never undoes the local
        command. The caller remains responsible for announcing the failure.
        """
        session = PLAY_OPERATIONS.get(op, args.get("sessionId"))
        route = self.routes.get(session)
        if route is None:
            return
        try:
            if op in PLAY_OPERATIONS:
                self._load(route, session, {**args, **(result or {})})
                self._align_file_route(route, session)
            elif op == "transport.pauseResume":
                paused = bool((result or {}).get("paused"))
                route.connection.transport("PAUSE" if paused else "PLAY")
                route.paused = paused
            elif op == "transport.stop":
                self.disable(session)
            elif op == "transport.seek" and session != "radio":
                position = (result or {}).get("positionSeconds")
                if position is not None:
                    route.connection.transport("SEEK", currentTime=max(0.0, float(position)))
        except Exception:
            self.routes.pop(session, None)
            route.close()
            if self.event is not None:
                self.event("mirror.failed", {"sessionId": session,
                                             "message": i18n.tr("mirror.lost")})

    def close(self) -> None:
        for session in tuple(self.routes):
            self.disable(session)
