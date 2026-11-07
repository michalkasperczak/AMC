"""Dostępny wybór natywnych playlist Spotify dla Ctrl+Shift+P.

Identyfikatory i ``snapshot_id`` pozostają danymi modelu. Lista wx otrzymuje
wyłącznie celowe etykiety użytkowe; żaden rekord ani identyfikator techniczny
nie może stać się nazwą dostępną kontrolki.
"""

from __future__ import annotations

from dataclasses import dataclass

import wx

from . import i18n
from .live_region import announce_status


class SpotifyPlaylistPayloadError(ValueError):
    """Krótki błąd odpowiedzi hosta bez ujawniania danych technicznych."""


@dataclass(slots=True)
class SpotifyPlaylistChoice:
    playlist_id: str
    name: str
    description: str
    selected: bool = False
    new_public: bool | None = None

    @property
    def label(self) -> str:
        state = "zaznaczona" if self.selected else "niezaznaczona"
        state = i18n.ui_text(state)
        return f"{self.description}, {state}"

    def __str__(self) -> str:
        return self.label


def choices_from_payload(payload: object) -> list[SpotifyPlaylistChoice]:
    if not isinstance(payload, dict) or not isinstance(payload.get("playlists"), list):
        raise SpotifyPlaylistPayloadError("Nie udało się odczytać playlist Spotify")
    result: list[SpotifyPlaylistChoice] = []
    seen: set[str] = set()
    for raw in payload["playlists"]:
        if not isinstance(raw, dict):
            raise SpotifyPlaylistPayloadError("Lista playlist Spotify zawiera uszkodzony wpis")
        playlist_id = str(raw.get("id") or "").strip()
        name = str(raw.get("name") or "").strip()
        label = str(raw.get("accessibleLabel") or "").strip()
        if not playlist_id or playlist_id in seen or not name or not label:
            raise SpotifyPlaylistPayloadError("Lista playlist Spotify zawiera niepełny wpis")
        seen.add(playlist_id)
        result.append(SpotifyPlaylistChoice(playlist_id, name, label))
    return result


class SpotifyPlaylistNameDialog(wx.Dialog):
    """Jednoznaczne pola nazwy i widoczności nowej playlisty Spotify."""

    def __init__(self, parent: wx.Window, *, provider: str = "Spotify") -> None:
        self.provider = provider
        super().__init__(
            parent,
            title=f"Nowa playlista {provider}",
            style=wx.DEFAULT_DIALOG_STYLE | wx.RESIZE_BORDER,
        )
        self.result_name = ""
        self.result_public = False

        panel = wx.Panel(self)
        grid = wx.FlexGridSizer(0, 2, 8, 10)
        grid.AddGrowableCol(1, 1)
        grid.Add(wx.StaticText(panel, label="&Nazwa playlisty:"), 0, wx.ALIGN_CENTER_VERTICAL)
        self.name_control = wx.TextCtrl(panel)
        self.name_control.SetName(f"Nazwa playlisty {provider}")
        grid.Add(self.name_control, 1, wx.EXPAND)
        visibility_label = wx.StaticText(panel, label="&Widoczność:")
        grid.Add(visibility_label, 0, wx.ALIGN_CENTER_VERTICAL)
        self.visibility = wx.Choice(
            panel,
            choices=[
                i18n.ui_text("Prywatna (domyślna)"),
                i18n.ui_text("Publiczna"),
            ],
        )
        self.visibility.SetName(f"Widoczność playlisty {provider}")
        self.visibility.SetSelection(0)
        grid.Add(self.visibility, 1, wx.EXPAND)
        if provider == "TIDAL":
            visibility_label.Hide()
            self.visibility.Hide()
        self.validation = wx.StaticText(panel, label="Wpisz nazwę playlisty")
        self.validation.SetName(f"Wynik sprawdzenia nazwy playlisty {provider}")
        grid.Add(wx.StaticText(panel, label=""), 0)
        grid.Add(self.validation, 1, wx.EXPAND)
        panel.SetSizer(grid)

        buttons = wx.StdDialogButtonSizer()
        save = wx.Button(self, wx.ID_OK, label="&Dalej")
        cancel = wx.Button(self, wx.ID_CANCEL, label="&Anuluj")
        save.SetName(f"Zatwierdź nazwę playlisty {provider}")
        cancel.SetName(f"Anuluj tworzenie playlisty {provider}")
        save.SetDefault()
        buttons.AddButton(save)
        buttons.AddButton(cancel)
        buttons.Realize()

        shell = wx.BoxSizer(wx.VERTICAL)
        shell.Add(panel, 1, wx.ALL | wx.EXPAND, 12)
        shell.Add(buttons, 0, wx.LEFT | wx.RIGHT | wx.BOTTOM | wx.ALIGN_RIGHT, 12)
        self.SetSizer(shell)
        self.SetMinSize((520, 230))
        save.Bind(wx.EVT_BUTTON, self._save)
        self.Bind(wx.EVT_SHOW, self._on_show)

    def _save(self, _event: object | None = None) -> None:
        name = self.name_control.GetValue().strip()
        if not 1 <= len(name) <= 100:
            announce_status(
                self.validation,
                "Nazwa playlisty musi mieć od 1 do 100 znaków",
            )
            self.name_control.SetFocus()
            return
        self.result_name = name
        self.result_public = (
            self.provider == "Spotify" and self.visibility.GetSelection() == 1
        )
        self.EndModal(wx.ID_OK)

    def _on_show(self, event: wx.ShowEvent) -> None:
        event.Skip()
        if event.IsShown():
            wx.CallAfter(self.name_control.SetFocus)


class SpotifyPlaylistDialog(wx.Dialog):
    """Jedna lista: Spacja zaznacza wiele celów, Enter zapisuje."""

    def __init__(
        self,
        parent: wx.Window,
        *,
        choices: list[SpotifyPlaylistChoice],
        target_label: str,
        provider: str = "Spotify",
    ) -> None:
        self.provider = provider
        super().__init__(
            parent,
            title=f"Dodaj do playlist {provider}",
            size=(690, 470),
            style=wx.DEFAULT_DIALOG_STYLE | wx.RESIZE_BORDER,
        )
        self._choices = list(choices)
        self._temporary_counter = 0
        self._selection_touched = False

        panel = wx.Panel(self)
        layout = wx.BoxSizer(wx.VERTICAL)
        instruction = i18n.tr("playlists.add.hint", provider=provider, target=target_label)
        heading = wx.StaticText(panel, label=instruction)
        heading.SetName(instruction)
        heading.Wrap(630)
        layout.Add(heading, 0, wx.ALL | wx.EXPAND, 12)

        list_label = i18n.tr("playlists.add.list", provider=provider)
        layout.Add(wx.StaticText(panel, label=list_label), 0, wx.LEFT | wx.RIGHT, 12)
        self.playlist_list = wx.ListBox(panel, choices=[], style=wx.LB_SINGLE)
        self.playlist_list.SetName(list_label)
        self.playlist_list.SetToolTip(i18n.tr("playlists.add.navigation"))
        layout.Add(self.playlist_list, 1, wx.LEFT | wx.RIGHT | wx.EXPAND, 12)

        self.status = wx.StaticText(panel, label="Wybierz co najmniej jedną playlistę")
        self.status.SetName(f"Stan wyboru playlist {provider}")
        layout.Add(self.status, 0, wx.ALL | wx.EXPAND, 12)
        panel.SetSizer(layout)

        buttons = wx.BoxSizer(wx.HORIZONTAL)
        self.new_button = wx.Button(self, label="&Nowa playlista…")
        self.save_button = wx.Button(self, wx.ID_OK, label="&Dodaj")
        self.cancel_button = wx.Button(self, wx.ID_CANCEL, label="&Anuluj")
        self.new_button.SetName(f"Utwórz nową playlistę {provider}")
        self.save_button.SetName(f"Dodaj do zaznaczonych playlist {provider}")
        self.cancel_button.SetName(f"Anuluj dodawanie do playlist {provider}")
        for button in (self.new_button, self.save_button, self.cancel_button):
            buttons.Add(button, 0, wx.RIGHT, 8)
        self.save_button.SetDefault()

        shell = wx.BoxSizer(wx.VERTICAL)
        shell.Add(panel, 1, wx.EXPAND)
        shell.Add(buttons, 0, wx.LEFT | wx.RIGHT | wx.BOTTOM | wx.ALIGN_RIGHT, 12)
        self.SetSizer(shell)
        self.SetMinSize((540, 350))

        self.new_button.Bind(wx.EVT_BUTTON, self._create)
        self.save_button.Bind(wx.EVT_BUTTON, self._save)
        self.Bind(wx.EVT_CHAR_HOOK, self._on_key)
        self.Bind(wx.EVT_SHOW, self._on_show)
        preferred = next(
            (
                choice.playlist_id
                for choice in self._choices
                if choice.selected
            ),
            None,
        )
        self._rebuild(preferred)

    @property
    def selected_playlist_ids(self) -> list[str]:
        return [
            choice.playlist_id
            for choice in self._choices
            if choice.selected and choice.new_public is None
        ]

    @property
    def new_playlists(self) -> list[tuple[str, bool]]:
        return [
            (choice.name, bool(choice.new_public))
            for choice in self._choices
            if choice.selected and choice.new_public is not None
        ]

    def _selected_choice(self) -> SpotifyPlaylistChoice | None:
        index = self.playlist_list.GetSelection()
        return self._choices[index] if 0 <= index < len(self._choices) else None

    def _rebuild(self, preferred_id: str | None = None) -> None:
        previous = preferred_id
        if previous is None and (choice := self._selected_choice()) is not None:
            previous = choice.playlist_id
        self.playlist_list.Set([choice.label for choice in self._choices])
        selected = next((
            index for index, choice in enumerate(self._choices)
            if choice.playlist_id == previous
        ), 0 if self._choices else wx.NOT_FOUND)
        self.playlist_list.SetSelection(selected)
        count = sum(choice.selected for choice in self._choices)
        message = (
            "Wybierz co najmniej jedną playlistę"
            if count == 0
            else f"Wybrane playlisty: {count}"
        )
        message = i18n.ui_text(message)
        self.status.SetLabel(message)
        self.status.SetName(message)

    def _toggle(self) -> None:
        choice = self._selected_choice()
        if choice is None:
            self.status.SetLabel(i18n.ui_text("Brak playlisty do zaznaczenia"))
            return
        choice.selected = not choice.selected
        self._selection_touched = True
        self._rebuild(choice.playlist_id)

    def _create(self, _event: object | None = None) -> None:
        provider = getattr(self, "provider", "Spotify")
        if provider not in ("Spotify", "TIDAL"):
            provider = "Spotify"
        name_dialog = (
            SpotifyPlaylistNameDialog(self)
            if provider == "Spotify"
            else SpotifyPlaylistNameDialog(self, provider=provider)
        )
        with name_dialog as name_dialog:
            if name_dialog.ShowModal() != wx.ID_OK:
                return
            name = name_dialog.result_name
            is_public = name_dialog.result_public
        self._temporary_counter += 1
        kind = "publiczna" if is_public else "prywatna"
        description = (
            f"Nowa playlista {name}, {kind}"
            if provider == "Spotify" else f"Nowa playlista {name}"
        )
        choice = SpotifyPlaylistChoice(
            f"new-{self._temporary_counter}",
            name,
            description,
            selected=True,
            new_public=is_public,
        )
        self._choices.append(choice)
        self._selection_touched = True
        self._rebuild(choice.playlist_id)
        self.playlist_list.SetFocus()
        announce_status(
            self.status,
            (f"Przygotowano nową playlistę {name}, {kind}, zaznaczoną. "
             if provider == "Spotify" else
             f"Przygotowano nową playlistę {name}, zaznaczoną. ") +
            "Naciśnij Enter, aby ją utworzyć i dodać wybrane elementy",
        )

    def _save(self, _event: object | None = None) -> None:
        if not any(choice.selected for choice in self._choices):
            self.status.SetLabel(
                i18n.ui_text("Wybierz co najmniej jedną playlistę")
            )
            self.status.SetName(self.status.GetLabel())
            self.playlist_list.SetFocus() if self._choices else self.new_button.SetFocus()
            return
        self.EndModal(wx.ID_OK)

    def _on_key(self, event: wx.KeyEvent) -> None:
        if (
            event.GetModifiers() == wx.MOD_NONE
            and event.GetKeyCode() in (wx.WXK_RETURN, wx.WXK_NUMPAD_ENTER)
            and self.playlist_list.HasFocus()
        ):
            if not self._selection_touched and (choice := self._selected_choice()) is not None:
                for candidate in self._choices:
                    candidate.selected = candidate is choice
                self._rebuild(choice.playlist_id)
            self._save()
            return
        if (
            event.GetModifiers() == wx.MOD_NONE
            and event.GetKeyCode() == wx.WXK_SPACE
            and self.playlist_list.HasFocus()
        ):
            self._toggle()
            return
        event.Skip()

    def _on_show(self, event: wx.ShowEvent) -> None:
        event.Skip()
        if event.IsShown():
            wx.CallAfter(
                self.playlist_list.SetFocus if self._choices else self.new_button.SetFocus
            )
