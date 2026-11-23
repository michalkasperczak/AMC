"""Muted picture in AMC's existing player, driven by the audio host clock.

The C# host remains the only audio source and owns transport, queue, bookmarks,
speed, and resume state. wx.media renders the same local file without sound.
"""

from __future__ import annotations

from collections.abc import Callable

import wx
import wx.media

from . import i18n


class EmbeddedVideo:
    def __init__(
        self, parent: wx.Window, on_error: Callable[[], None],
        on_ready: Callable[[], None], focus_control: wx.Window,
    ) -> None:
        self.control = wx.media.MediaCtrl(parent, style=wx.SIMPLE_BORDER)
        self.control.SetName(i18n.ui_text("Obraz odtwarzanego wideo"))
        self.control.SetMinSize((480, 270))
        self.control.SetVolume(0.0)
        self.control.Hide()
        self.control.Bind(wx.media.EVT_MEDIA_LOADED, self._loaded)
        self.control.Bind(wx.EVT_SET_FOCUS, lambda _event: focus_control.SetFocus())
        self._on_error = on_error
        self._on_ready = on_ready
        self.path: str | None = None
        self.ready = False
        self._status: dict = {}

    def present(self, path: str, status: dict) -> bool:
        self._status = status
        self.control.Show()
        if path != self.path:
            self.control.Stop()
            self.path = path
            self.ready = False
            self.control.SetVolume(0.0)
            if not self.control.Load(path):
                self.hide()
                self._on_error()
                return False
            wx.CallLater(300, self._check_loaded, path)
            wx.CallLater(5000, self._check_timeout, path)
        elif self.ready:
            self._sync()
        return True

    def _check_loaded(self, path: str) -> None:
        # Some DirectShow backends report a duration without sending LOADED.
        if self.path == path and not self.ready and self.control.Length() > 0:
            self._loaded(None)

    def _check_timeout(self, path: str) -> None:
        if self.path == path and not self.ready:
            self.hide()
            self._on_error()

    def _loaded(self, _event: wx.Event | None) -> None:
        if not self.path or self.ready:
            return
        self.ready = True
        self.control.SetVolume(0.0)
        if self._sync():
            self._on_ready()

    def _sync(self) -> bool:
        if not self.ready:
            return False
        status = self._status
        position = status.get("positionSeconds")
        if isinstance(position, (int, float)):
            target = max(0, int(position * 1000))
            length = self.control.Length()
            if length > 0:
                target = min(target, length)
            if abs(self.control.Tell() - target) > 1200:
                self.control.Seek(target)
        rate = status.get("rate")
        if isinstance(rate, (int, float)) and 0.5 <= rate <= 2.0:
            self.control.SetPlaybackRate(float(rate))
        self.control.SetVolume(0.0)
        state = self.control.GetState()
        if status.get("playing") and not status.get("paused"):
            length = self.control.Length()
            position = status.get("positionSeconds")
            near_end = (
                isinstance(position, (int, float)) and length > 0
                and position * 1000 >= length - 500
            )
            if state != wx.media.MEDIASTATE_PLAYING and not near_end:
                if not self.control.Play():
                    self.hide()
                    self._on_error()
                    return False
        elif state == wx.media.MEDIASTATE_PLAYING:
            self.control.Pause()
        return True

    def hide(self) -> None:
        self.control.Stop()
        self.control.Hide()
        self.path = None
        self.ready = False

    def stop(self) -> None:
        self.hide()
