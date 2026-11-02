"""AirPlay/RAOP second target for AMC audio, backed by pyatv and FFmpeg.

The primary host remains authoritative. Audio is transcoded from the current
source in a worker thread; AirPlay buffering can leave an audible delay.
"""
from __future__ import annotations

import asyncio
from dataclasses import dataclass, field
from pathlib import Path
import subprocess
import threading
from typing import Callable

from .airplay_adapter import AirPlayDevice, discover_airplay_devices
from .mirror_core import MIRROR_SESSIONS, PLAY_OPERATIONS


def ffmpeg_command(executable: str, source: str, position: float) -> list[str]:
    return [executable, "-nostdin", "-hide_banner", "-loglevel", "error",
            "-ss", str(max(0.0, position)), "-i", source, "-vn",
            "-f", "mp3", "-b:a", "192k", "pipe:1"]


@dataclass
class AirPlayRoute:
    device: AirPlayDevice
    ffmpeg: str
    source: str = ""
    worker: threading.Thread | None = None
    loop: asyncio.AbstractEventLoop | None = None
    task: asyncio.Task | None = None
    ready: threading.Event = field(default_factory=threading.Event)
    stopped: bool = False
    error: Exception | None = None

    def stop(self) -> None:
        self.stopped = True
        if self.loop is not None and self.task is not None and self.loop.is_running():
            self.loop.call_soon_threadsafe(self.task.cancel)
        if self.worker is not None and self.worker is not threading.current_thread():
            self.worker.join(timeout=2)
        self.worker = None


class MirrorAirPlayRouter:
    def __init__(self, native: Callable, ffmpeg: str, event: Callable | None = None,
                 *, discover=discover_airplay_devices) -> None:
        self.native = native
        self.ffmpeg = ffmpeg
        self.event = event
        self.discover = discover
        self.routes: dict[str, AirPlayRoute] = {}

    def devices(self) -> tuple[AirPlayDevice, ...]:
        if not Path(self.ffmpeg).is_file():
            return ()
        return asyncio.run(self.discover())

    def selected_device_id(self, session: str) -> str | None:
        route = self.routes.get(session)
        return route.device.device_id if route else None

    async def _stream_once(self, route: AirPlayRoute, position: float) -> None:
        import pyatv
        loop = asyncio.get_running_loop()
        route.loop = loop
        route.task = asyncio.current_task()
        protocol = pyatv.const.Protocol.RAOP
        configs = await pyatv.scan(loop, timeout=4, protocol=protocol,
                                   identifier=route.device.device_id)
        config = next((item for item in configs
                       if item.identifier == route.device.device_id
                       and item.get_service(protocol) is not None), None)
        if config is None:
            raise RuntimeError("Wybrany odbiornik AirPlay jest niedostępny")
        receiver = await pyatv.connect(config, loop, protocol=protocol)
        process = None
        stream_task = None
        try:
            if route.stopped:
                return
            flags = getattr(subprocess, "CREATE_NO_WINDOW", 0)
            process = subprocess.Popen(
                ffmpeg_command(route.ffmpeg, route.source, position),
                stdin=subprocess.DEVNULL,
                stdout=subprocess.PIPE,
                stderr=subprocess.DEVNULL,
                creationflags=flags,
            )
            stream_task = asyncio.create_task(receiver.stream.stream_file(process.stdout))
            while not stream_task.done():
                await asyncio.sleep(0.1)
                playback = await receiver.metadata.playing()
                if playback.device_state == pyatv.const.DeviceState.Playing:
                    route.ready.set()
                    break
            await stream_task
            route.ready.set()
            return_code = await asyncio.to_thread(process.wait)
            if return_code != 0 and not route.stopped:
                raise RuntimeError("Nie udało się przygotować dźwięku dla AirPlay")
        finally:
            if stream_task is not None and not stream_task.done():
                stream_task.cancel()
                await asyncio.gather(stream_task, return_exceptions=True)
            receiver.close()
            if process is not None and process.returncode is None:
                process.kill()
                await asyncio.to_thread(process.wait)
            if process is not None and process.stdout is not None:
                process.stdout.close()

    async def _stream(self, route: AirPlayRoute, position: float) -> None:
        for attempt in range(2):
            try:
                await self._stream_once(route, position)
                return
            except TimeoutError as error:
                if route.stopped or route.ready.is_set() or attempt:
                    raise
                await asyncio.sleep(1)

    def _start(self, session: str, route: AirPlayRoute, status: dict) -> None:
        route.source = str(status.get("source") or "")
        route.stopped = False
        route.error = None
        route.ready.clear()
        position = float(status.get("positionSeconds") or 0) if session != "radio" else 0.0

        def run() -> None:
            try:
                asyncio.run(self._stream(route, position))
            except asyncio.CancelledError:
                pass
            except Exception as error:
                route.error = error
                if (not route.stopped and route.ready.is_set()
                        and self.routes.get(session) is route):
                    self.routes.pop(session, None)
                    if self.event is not None:
                        self.event("mirror.failed", {"sessionId": session,
                                                     "message": "Utracono AirPlay; odtwarzanie lokalne trwa dalej"})
            finally:
                route.ready.set()
        route.worker = threading.Thread(target=run, name="AMC AirPlay", daemon=True)
        route.worker.start()
        if not route.ready.wait(timeout=35):
            route.stop()
            raise RuntimeError("Odbiornik AirPlay nie odpowiedział")
        if route.error is not None:
            route.stop()
            if isinstance(route.error, RuntimeError):
                raise route.error
            raise RuntimeError(
                f"{route.device.name} nie odpowiada przez AirPlay. "
                "Sprawdź odbiornik i spróbuj ponownie."
            ) from route.error

    def enable(self, session: str, device_id: str) -> dict:
        if session not in MIRROR_SESSIONS or not Path(self.ffmpeg).is_file():
            raise RuntimeError("AirPlay nie jest dostępny w tej sesji")
        previous = self.routes.get(session)
        if previous and previous.device.device_id == device_id:
            return {"name": previous.device.name, "synchronized": False}
        device = next((item for item in self.devices() if item.device_id == device_id), None)
        if device is None:
            raise RuntimeError("Nie znaleziono wybranego odbiornika AirPlay")
        if any(key != session and route.device.device_id == device_id
               for key, route in self.routes.items()):
            raise RuntimeError("Odbiornik AirPlay jest już używany przez AMC")
        status = self.native("transport.status", {"sessionId": session})
        if not status.get("source") or not (status.get("playing") or status.get("paused")):
            raise RuntimeError("Najpierw uruchom materiał w AMC")
        route = AirPlayRoute(device, self.ffmpeg)
        self.routes[session] = route
        try:
            if status.get("playing") and not status.get("paused"):
                self._start(session, route, status)
        except Exception:
            if previous is None:
                self.routes.pop(session, None)
            else:
                self.routes[session] = previous
            raise
        if previous is not None:
            previous.stop()
        return {"name": device.name, "synchronized": False}

    def disable(self, session: str) -> dict:
        route = self.routes.pop(session, None)
        if route is not None:
            route.stop()
        return {"active": False}

    def after_native(self, op: str, args: dict, result: dict | None) -> None:
        session = PLAY_OPERATIONS.get(op, args.get("sessionId"))
        route = self.routes.get(session)
        if route is None:
            return
        if op == "transport.stop":
            self.disable(session)
            return
        if op not in PLAY_OPERATIONS and op not in ("transport.pauseResume", "transport.seek"):
            return
        try:
            route.stop()
            status = self.native("transport.status", {"sessionId": session})
            if status.get("playing") and not status.get("paused") and status.get("source"):
                replacement = AirPlayRoute(route.device, route.ffmpeg)
                self.routes[session] = replacement
                self._start(session, replacement, status)
        except Exception:
            self.disable(session)
            if self.event is not None:
                self.event("mirror.failed", {"sessionId": session,
                                             "message": "Utracono AirPlay; odtwarzanie lokalne trwa dalej"})

    def close(self) -> None:
        for session in tuple(self.routes):
            self.disable(session)
