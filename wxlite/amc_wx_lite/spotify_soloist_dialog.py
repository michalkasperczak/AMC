"""Accessible, keyboard-first local setup for Spotify Soloist."""

from __future__ import annotations

import threading
from typing import Callable

import wx

from .accessible_controls import FocusableReadOnlyTextCtrl
from . import i18n
from .spotify_soloist import SoloistManager, SoloistSetupError


class SpotifySoloistDialog(wx.Dialog):
    def __init__(
        self, parent: wx.Window, manager: SoloistManager,
        announce: Callable[[str], None],
        on_closed: Callable[[], None] | None = None,
    ) -> None:
        super().__init__(parent, title="Odtwarzacz Spotify Soloist",
                         style=wx.DEFAULT_DIALOG_STYLE | wx.RESIZE_BORDER)
        self.SetName("Konfiguracja odtwarzacza Spotify Soloist")
        self._manager = manager
        self._announce = announce
        self._on_closed = on_closed
        self._busy = False
        self._closed = False
        try:
            saved = manager.setup()
            initial_status = ("Zapisano ustawienia Soloist. Sprawdź połączenie."
                              if saved and saved.key_saved else
                              "Wskaż soloist.exe i wpisz własny klucz Soloist.")
        except SoloistSetupError as error:
            saved = None
            initial_status = str(error)

        layout = wx.BoxSizer(wx.VERTICAL)
        instruction = wx.StaticText(self, label=(
            "Katalog Spotify używa osobnego Client ID. Do Soloist potrzebujesz "
            "własnego klucza z konta Spotify Premium. Wybierz oficjalny plik "
            "soloist.exe, zapisz klucz, uruchom urządzenie AMC i wybierz je "
            "w aplikacji Spotify. AMC żąda jakości Lossless dla muzyki Premium; "
            "rzeczywistej jakości Soloist nie podaje. Hasła nie wpisuj w AMC."
        ))
        instruction.Wrap(620)
        layout.Add(instruction, 0, wx.ALL | wx.EXPAND, 12)

        self.status = wx.StaticText(self, label=initial_status)
        self.status.SetName(f"Stan Soloist: {initial_status}")
        layout.Add(self.status, 0, wx.LEFT | wx.RIGHT | wx.BOTTOM | wx.EXPAND, 12)

        label = wx.StaticText(self, label="&Plik soloist.exe:")
        layout.Add(label, 0, wx.LEFT | wx.RIGHT, 12)
        row = wx.BoxSizer(wx.HORIZONTAL)
        self.executable = wx.TextCtrl(self, value=str(saved.executable) if saved else "")
        self.executable.SetName("Ścieżka do oficjalnego pliku soloist.exe")
        row.Add(self.executable, 1, wx.RIGHT | wx.EXPAND, 8)
        self.browse_button = wx.Button(self, label="Wybierz plik")
        self.browse_button.SetName("Wybierz oficjalny plik soloist.exe")
        self.browse_button.Bind(wx.EVT_BUTTON, self._browse)
        row.Add(self.browse_button, 0)
        layout.Add(row, 0, wx.LEFT | wx.RIGHT | wx.BOTTOM | wx.EXPAND, 12)

        key_label = wx.StaticText(self, label="&Klucz Soloist:")
        layout.Add(key_label, 0, wx.LEFT | wx.RIGHT, 12)
        self.api_key = wx.TextCtrl(self, style=wx.TE_PASSWORD)
        self.api_key.SetName("Własny klucz Spotify Soloist, pole ukryte")
        layout.Add(self.api_key, 0, wx.LEFT | wx.RIGHT | wx.BOTTOM | wx.EXPAND, 12)

        for caption, url in (
            ("Panel klucza Soloist do skopiowania:",
             "https://developer.spotify.com/dashboard/soloist"),
            ("Oficjalne pobieranie Soloist do skopiowania:",
             "https://developer.spotify.com/documentation/soloist/reference/downloads-and-updates"),
        ):
            url_label = wx.StaticText(self, label=caption)
            layout.Add(url_label, 0, wx.LEFT | wx.RIGHT, 12)
            field = FocusableReadOnlyTextCtrl(self, value=url)
            field.SetName(caption)
            layout.Add(field, 0, wx.LEFT | wx.RIGHT | wx.BOTTOM | wx.EXPAND, 12)

        actions = wx.BoxSizer(wx.HORIZONTAL)
        self.save_button = wx.Button(self, label="Zapisz ustawienia")
        self.save_button.SetName("Zapisz ścieżkę i klucz Soloist")
        self.save_button.Bind(wx.EVT_BUTTON, self._save)
        actions.Add(self.save_button, 0, wx.RIGHT, 8)
        self.start_button = wx.Button(self, label="Uruchom urządzenie")
        self.start_button.SetName("Uruchom odtwarzacz Spotify Soloist")
        self.start_button.Bind(wx.EVT_BUTTON, self._start)
        actions.Add(self.start_button, 0, wx.RIGHT, 8)
        self.check_button = wx.Button(self, label="Sprawdź połączenie")
        self.check_button.SetName("Sprawdź stan odtwarzacza Spotify Soloist")
        self.check_button.Bind(wx.EVT_BUTTON, self._check)
        actions.Add(self.check_button, 0, wx.RIGHT, 8)
        self.close_button = wx.Button(self, wx.ID_CANCEL, "Zamknij")
        self.close_button.SetName("Zamknij konfigurację Soloist")
        actions.Add(self.close_button, 0)
        layout.Add(actions, 0, wx.LEFT | wx.RIGHT | wx.BOTTOM, 12)
        self.SetSizerAndFit(layout)
        self.SetMinSize((700, 360))
        self.SetEscapeId(wx.ID_CANCEL)
        self.close_button.Bind(wx.EVT_BUTTON, self._on_close_button)
        self.Bind(wx.EVT_CLOSE, self._on_close)
        wx.CallAfter(self.executable.SetFocus if not saved else self.start_button.SetFocus)

    def focus_primary_action(self) -> None:
        self.start_button.SetFocus() if self.executable.GetValue().strip() else self.executable.SetFocus()

    def _set_status(self, value: str) -> None:
        message = i18n.ui_text(value)
        self.status.SetLabel(message)
        self.status.SetName(i18n.ui_text(f"Stan Soloist: {message}"))
        self._announce(message)

    def _set_busy(self, busy: bool) -> None:
        self._busy = busy
        for button in (self.browse_button, self.save_button,
                       self.start_button, self.check_button):
            button.Enable(not busy)

    def _browse(self, _event: wx.CommandEvent) -> None:
        with wx.FileDialog(self, "Wybierz oficjalny plik soloist.exe",
                           wildcard="Spotify Soloist (soloist.exe)|soloist.exe",
                           style=wx.FD_OPEN | wx.FD_FILE_MUST_EXIST) as picker:
            if picker.ShowModal() == wx.ID_OK:
                self.executable.SetValue(picker.GetPath())
        self.executable.SetFocus()

    def _save(self, _event: wx.CommandEvent) -> None:
        try:
            self._manager.save(self.executable.GetValue().strip(),
                               self.api_key.GetValue())
        except SoloistSetupError as error:
            self._set_status(str(error))
            self.api_key.SetFocus()
            return
        self.api_key.SetValue("")
        self._set_status("Zapisano ścieżkę i klucz Soloist. Możesz uruchomić urządzenie.")
        self.start_button.SetFocus()

    def _run_background(self, operation: Callable[[], str], focus: wx.Window) -> None:
        if self._busy:
            return
        self._set_busy(True)

        def worker() -> None:
            try:
                message = operation()
            except SoloistSetupError as error:
                message = str(error)
            except Exception:
                message = "Operacja Soloist nie powiodła się."

            def done() -> None:
                try:
                    if self._closed or self.IsBeingDeleted():
                        return
                except RuntimeError:
                    return
                self._set_busy(False)
                self._set_status(message)
                focus.SetFocus()

            wx.CallAfter(done)

        threading.Thread(target=worker, daemon=True, name="amc-soloist-setup").start()

    def _start(self, _event: wx.CommandEvent) -> None:
        def operation() -> str:
            self._manager.start()
            return self._manager.status()
        self._run_background(operation, self.start_button)

    def _check(self, _event: wx.CommandEvent) -> None:
        self._run_background(self._manager.status, self.check_button)

    def _on_close_button(self, _event: wx.CommandEvent) -> None:
        self.Close()

    def _on_close(self, _event: wx.CloseEvent) -> None:
        self._closed = True
        self.Hide()
        if self._on_closed is not None:
            self._on_closed()
        self.Destroy()
