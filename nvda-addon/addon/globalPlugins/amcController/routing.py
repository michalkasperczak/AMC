"""Select one AMC release profile before a global gesture enters the queue."""

import ctypes
from ctypes import wintypes
import os
from pathlib import Path

from .transport import BridgeError, pipe_available


PROFILES = ("test", "public")
TITLES = {"test": "AMC Test", "public": "AMC Public"}


def _profile_from_title(title):
    for profile, label in TITLES.items():
        if title == label or title.startswith(label + " — "):
            return profile
    return None


def foreground_windows():
    """Return foreground PID and top-level titles, including a dialog's owner."""
    user = ctypes.WinDLL("user32", use_last_error=True)
    user.GetForegroundWindow.restype = wintypes.HWND
    user.GetWindowThreadProcessId.argtypes = [wintypes.HWND, ctypes.POINTER(wintypes.DWORD)]
    user.GetWindowTextLengthW.argtypes = [wintypes.HWND]
    user.GetWindowTextW.argtypes = [wintypes.HWND, wintypes.LPWSTR, ctypes.c_int]
    foreground = user.GetForegroundWindow()
    if not foreground:
        raise BridgeError("Nie można ustalić aktywnego okna. Wybierz okno AMC i ponów skrót.")
    foreground_pid = wintypes.DWORD()
    user.GetWindowThreadProcessId(foreground, ctypes.byref(foreground_pid))
    if not foreground_pid.value:
        raise BridgeError("Nie można ustalić aktywnego okna. Wybierz okno AMC i ponów skrót.")
    windows = []
    callback_type = ctypes.WINFUNCTYPE(wintypes.BOOL, wintypes.HWND, wintypes.LPARAM)

    @callback_type
    def collect(handle, _data):
        pid = wintypes.DWORD()
        user.GetWindowThreadProcessId(handle, ctypes.byref(pid))
        if pid.value == foreground_pid.value:
            length = user.GetWindowTextLengthW(handle)
            if length:
                buffer = ctypes.create_unicode_buffer(min(length + 1, 512))
                user.GetWindowTextW(handle, buffer, len(buffer))
                windows.append((pid.value, buffer.value))
        return True

    user.EnumWindows.argtypes = [callback_type, wintypes.LPARAM]
    user.EnumWindows(collect, 0)
    return foreground_pid.value, windows


class ProfileRouter:
    def __init__(self, state_path, window_snapshot=foreground_windows,
                 available=pipe_available):
        self._path = Path(state_path) if state_path else None
        self._window_snapshot = window_snapshot
        self._available = available
        self.last_profile = self._read_last()

    def _read_last(self):
        try:
            value = self._path.read_text(encoding="ascii").strip() if self._path else ""
        except OSError:
            return None
        return value if value in PROFILES else None

    def _remember(self, profile):
        if profile == self.last_profile:
            return
        self.last_profile = profile
        if self._path:
            try:
                temporary = self._path.with_suffix(".tmp")
                temporary.write_text(profile + "\n", encoding="ascii")
                os.replace(temporary, self._path)
            except OSError:
                # Speech and routing still work in NVDA's read-only mode.
                pass

    def select(self):
        try:
            pid, windows = self._window_snapshot()
        except (OSError, AttributeError) as error:
            raise BridgeError("Nie można ustalić aktywnego okna. Wybierz okno AMC i ponów skrót.") from error
        focused_titles = [title for window_pid, title in windows if window_pid == pid]
        focused_profiles = {_profile_from_title(title) for title in focused_titles}
        focused_profiles.discard(None)
        if len(focused_profiles) > 1:
            raise BridgeError("Nie można rozróżnić okien AMC. Wybierz okno AMC Test lub AMC Public.")
        if focused_profiles:
            profile = focused_profiles.pop()
            self._remember(profile)
            return profile
        if any(title.startswith("AMC") for title in focused_titles):
            raise BridgeError("Nie można rozpoznać profilu aktywnego AMC. Wybierz jego okno główne.")
        try:
            live = [profile for profile in PROFILES if self._available(profile)]
        except (OSError, AttributeError) as error:
            raise BridgeError("Nie można sprawdzić połączenia z AMC. Spróbuj za chwilę.") from error
        if self.last_profile:
            if self.last_profile in live:
                return self.last_profile
            raise BridgeError("Ostatnio używany %s nie działa. Wybierz okno AMC Test lub AMC Public." % TITLES[self.last_profile])
        if len(live) == 1:
            self._remember(live[0])
            return live[0]
        if not live:
            raise BridgeError("Uruchom AMC Test lub AMC Public, a potem ponów skrót.")
        raise BridgeError("Działają AMC Test i AMC Public. Wybierz okno właściwego programu, a potem ponów skrót.")
