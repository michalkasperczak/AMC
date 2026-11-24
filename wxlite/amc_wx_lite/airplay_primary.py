"""A single audible RAOP output backed by the muted native AMC timeline.

The native engine retains queue, position and media ownership. Its output is
muted while RAOP sends the source independently; loss of RAOP restores local
audio. This is separate from the explicit two-output Mirror route.
"""
from __future__ import annotations

from dataclasses import dataclass
from pathlib import Path
import threading
from typing import Callable

from . import i18n
from .airplay_adapter import AirPlayDevice, AirPlayUnavailable
from .cast_session import UNHANDLED
from .mirror_airplay import MirrorAirPlayRouter
from .network_audio import (NETWORK_AUDIO_SESSIONS, SHARED_DECODER_SESSIONS,
                            play_session, source_for_session)


@dataclass
class PrimaryAirPlayState:
    device: AirPlayDevice
    volume: int
    muted: bool
    owns_local_mute: bool = False
    title: str = ""
    playing: bool = False
    source: str = ""


class PrimaryAirPlayRouter:
    def __init__(self, native: Callable, ffmpeg: str, event: Callable | None = None,
                 *, discover=None, stream: MirrorAirPlayRouter | None = None) -> None:
        self.native = native
        self.event = event
        if stream is None:
            options = {"discover": discover} if discover is not None else {}
            stream = MirrorAirPlayRouter(native, ffmpeg, self._on_stream_event, **options)
        self.stream = stream
        self.routes: dict[str, PrimaryAirPlayState] = {}
        self.ffmpeg = ffmpeg
        self._lock = threading.RLock()

    def devices(self, *, refresh: bool = False) -> tuple[AirPlayDevice, ...]:
        if not Path(self.ffmpeg).is_file():
            return ()
        return self.stream.devices(refresh=refresh)

    def selected_device_id(self, session: str) -> str | None:
        state = self.routes.get(session)
        return state.device.device_id if state else None

    def playback_context(self, session: str) -> tuple[str, str] | None:
        state = self.routes.get(session)
        if not state or not state.playing or not state.title:
            return None
        return state.title, state.device.name

    def _restore_local(self, session: str, state: PrimaryAirPlayState) -> None:
        try:
            status = self.native("transport.status", {"sessionId": session})
            if bool(status.get("muted")) != state.muted:
                self.native("transport.toggleMute", {"sessionId": session})
        except Exception:
            # The host might already be closing. It must not mask a RAOP error.
            pass

    def _on_stream_event(self, name: str, data: dict) -> None:
        if name == "mirror.failed":
            with self._lock:
                session = data.get("sessionId")
                state = self.routes.pop(session, None)
                if state is not None:
                    self.stream.disable(session)
                    self._restore_local(session, state)
                    if self.event is not None:
                        self.event("playback.failed", {
                            "sessionId": session,
                            "engine": "radio" if session == "radio" else "files",
                            "message": i18n.tr("airplay.primary.lostMuted" if state.muted
                                               else "airplay.primary.lost"),
                        })

    def select(self, session: str, device_id: str | None,
               *, volume_override: int | None = None) -> dict:
        with self._lock:
            return self._select_locked(session, device_id, volume_override=volume_override)

    def _select_locked(self, session: str, device_id: str | None,
                       *, volume_override: int | None = None) -> dict:
        if session not in NETWORK_AUDIO_SESSIONS:
            raise AirPlayUnavailable(i18n.tr("airplay.primary.session"))
        if device_id is None:
            state = self.routes.pop(session, None)
            if state is not None:
                self.stream.disable(session)
                self._restore_local(session, state)
            return {"name": i18n.tr("cast.local")}
        previous = self.routes.get(session)
        if previous is not None and previous.device.device_id == device_id:
            return {"name": "AirPlay: " + previous.device.name}
        try:
            devices = self.devices(refresh=True)
        except Exception:
            raise AirPlayUnavailable(i18n.tr("mirror.airplayDiscoveryFailed")) from None
        device = next((item for item in devices if item.device_id == device_id), None)
        if device is None:
            raise AirPlayUnavailable(i18n.tr("airplay.primary.deviceMissing"))
        if any(owner != session and state.device.device_id == device_id
               for owner, state in self.routes.items()):
            raise AirPlayUnavailable(i18n.tr("airplay.primary.inUse"))
        status = self.native("transport.status", {"sessionId": session})
        playing = bool(status.get("playing") and not status.get("paused"))
        has_source = bool(status.get("source") and (playing or status.get("paused")))
        if has_source:
            source_for_session(session, status)
        if session == "tidal" and not has_source and previous is None:
            raise AirPlayUnavailable(i18n.tr("airplay.primary.tidalFirst"))
        value = (previous.volume if previous is not None else
                 int(volume_override if volume_override is not None else status.get("volume", 20)))
        volume = max(0, min(30, value))
        muted = previous.muted if previous is not None else bool(status.get("muted"))
        state = PrimaryAirPlayState(device, volume, muted,
                                     previous.owns_local_mute if previous is not None else False,
                                     str(status.get("title") or ""), playing,
                                     str(status.get("source") or ""))
        muted_for_selection = False
        if has_source and previous is None and not status.get("muted"):
            self.native("transport.toggleMute", {"sessionId": session})
            state.owns_local_mute = True
            muted_for_selection = True
        try:
            if has_source:
                self.stream.enable(session, device_id,
                                   volume=0 if muted else volume)
            self.routes[session] = state
            return {"name": "AirPlay: " + device.name}
        except Exception:
            if muted_for_selection:
                self._restore_local(session, state)
            raise AirPlayUnavailable(i18n.tr(
                "airplay.primary.startFailed", name=device.name)) from None

    def after_native(self, op: str, args: dict, result: object) -> None:
        session = play_session(op, args) or args.get("sessionId")
        state = self.routes.get(session)
        if state is None:
            return
        if op == "transport.stop":
            self.stream.disable(session)
            state.playing = False
            return
        if op not in ("files.play", "radio.play", "media.play", "tidal.nativePlay",
                      "transport.pauseResume", "transport.seek"):
            return
        status = self.native("transport.status", {"sessionId": session})
        state.title = str(status.get("title") or "")
        state.playing = bool(status.get("playing") and not status.get("paused"))
        state.source = str(status.get("source") or "")
        try:
            if self.stream.selected_device_id(session) is None:
                if status.get("source") and (status.get("playing") or status.get("paused")
                                             or session == "tidal" and status.get("preparing")):
                    self.stream.enable(session, state.device.device_id,
                                       volume=0 if state.muted else state.volume)
            else:
                self.stream.after_native(op, args, result if isinstance(result, dict) else None)
            if session not in self.routes:
                raise AirPlayUnavailable(i18n.tr("airplay.primary.lostMuted" if state.muted
                                                 else "airplay.primary.lost"))
        except Exception:
            if session in self.routes:
                self.routes.pop(session, None)
                self.stream.disable(session)
                self._restore_local(session, state)
            raise AirPlayUnavailable(i18n.tr("airplay.primary.lostMuted" if state.muted
                                             else "airplay.primary.lost")) from None

    def call(self, op: str, args: dict) -> object:
        session = play_session(op, args) or args.get("sessionId")
        state = self.routes.get(session)
        if state is None:
            return UNHANDLED
        if op == "transport.status":
            status = self.native(op, args)
            source = str(status.get("source") or "")
            stream_route = getattr(self.stream, "routes", {}).get(session)
            worker = getattr(stream_route, "worker", None)
            ended_early = bool(worker is not None and not worker.is_alive()
                               and not stream_route.stopped)
            if (source and status.get("playing") and not status.get("paused")
                    and (source != state.source or ended_early)):
                self.after_native("transport.seek", {"sessionId": session}, status)
            elif not status.get("playing") and not status.get("paused"):
                self.stream.disable(session)
                state.playing = False
            else:
                state.playing = bool(status.get("playing") and not status.get("paused"))
                state.title = str(status.get("title") or state.title)
            return {**status, "volume": state.volume, "muted": state.muted,
                    "airplay": True, "airplayDeviceName": state.device.name}
        if op == "transport.setVolume":
            volume = max(0, min(100, int(args.get("volume", state.volume))))
            self.stream.set_volume(session, 0 if state.muted else volume)
            current = self.native("transport.status", {"sessionId": session})
            if current.get("source") or current.get("playing") or current.get("paused"):
                self.native(op, {**args, "volume": volume})
            state.volume = volume
            return {"volume": volume, "muted": state.muted}
        if op == "transport.toggleMute":
            muted = not state.muted
            self.stream.set_volume(session, 0 if muted else state.volume)
            state.muted = muted
            return {"volume": state.volume, "muted": muted}
        if play_session(op, args) == session and not state.owns_local_mute:
            before = self.native("transport.status", {"sessionId": session})
            needs_mute = not bool(before.get("muted"))
            native_args = {**args, "volume": 0} if needs_mute else args
            result = self.native(op, native_args)
            if needs_mute:
                self.native("transport.toggleMute", {"sessionId": session})
                state.owns_local_mute = True
                self.native("transport.setVolume", {"sessionId": session,
                                                    "volume": state.volume})
            # The host client must first remember the newly accepted TIDAL
            # proxy request so its status can expose the new local source.
            if op != "tidal.nativePlay":
                self.after_native(op, args, result)
            return ({**result, "volume": state.volume, "muted": state.muted}
                    if isinstance(result, dict) else result)
        return UNHANDLED

    def retire_displaced(self, op: str, args: dict) -> None:
        """A new owner of the shared decoder must never inherit local silence."""
        incoming = play_session(op, args)
        if incoming not in SHARED_DECODER_SESSIONS:
            return
        for owner in tuple(self.routes):
            if owner != incoming and owner in SHARED_DECODER_SESSIONS:
                self.select(owner, None)

    def close(self) -> None:
        for session in tuple(self.routes):
            self.select(session, None)
        self.stream.close()
