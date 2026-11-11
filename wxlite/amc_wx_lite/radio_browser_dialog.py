"""Keyboard-first Radio Browser navigation in the Radio session."""

from __future__ import annotations

import re
from typing import Callable

import wx

from .live_region import announce_status
from .radio_browser_catalog import (
    Country,
    STATION_ORDERS,
    countries_by_continent,
    countries_from_payload,
    station_page_from_payload,
)
from .search_catalog import SearchEntry, SearchResultAction


class _DialogAnnouncer:
    def __init__(self, status: wx.StaticText) -> None:
        self._status = status

    def say(self, message: str) -> None:
        owner = wx.GetTopLevelParent(self._status)
        if owner is None or not owner.IsActive():
            self._status.SetLabel(message)
            self._status.SetName(message)
            return
        announce_status(self._status, message)


class RadioBrowserDialog(wx.Dialog):
    """One native list for every directory level; no hidden focus handoffs."""

    def __init__(
        self,
        parent: wx.Window,
        *,
        load_countries: Callable[["RadioBrowserDialog"], None],
        load_stations: Callable[[str, str, int, "RadioBrowserDialog"], None],
        station_action: Callable[[list[SearchEntry], SearchResultAction, Callable[[str], None]], None],
        copy_text: Callable[[str], bool],
    ) -> None:
        super().__init__(parent, title="Radio Browser — AMC", size=(760, 560))
        self.SetName("Radio Browser — AMC")
        self._load_countries = load_countries
        self._load_stations = load_stations
        self._station_action = station_action
        self._copy_text = copy_text
        self._countries: tuple[Country, ...] = ()
        self._groups: dict[str, tuple[Country, ...]] = {}
        self._continent: str | None = None
        self._country: Country | None = None
        self._order = "name"
        self._entries: list[SearchEntry] = []
        self._page_addresses: dict[str, str] = {}
        self._next_offset: int | None = None
        self._level = "continents"
        self._loading = True
        self.selected_station: SearchEntry | None = None

        self.heading = wx.StaticText(self, label="Regiony Radio Browser")
        self.heading.SetName("Regiony Radio Browser")
        self.items = wx.ListBox(self, choices=["Pobieranie krajów…"], style=wx.LB_SINGLE)
        self.items.SetName("Regiony Radio Browser")
        self.items.SetHelpText(
            "Enter otwiera. Backspace lub Escape wraca. Na stacji strzałka "
            "w lewo lub prawo podaje bitrate; Ctrl+C kopiuje adres strumienia, "
            "Ctrl+Shift+C link do strony stacji, Ctrl+Shift+L zmienia Bibliotekę, "
            "Ctrl+Shift+U zmienia Ulubione."
        )
        self.status = wx.StaticText(self, label="Pobieranie krajów Radio Browser")
        self.status.SetName("Pobieranie krajów Radio Browser")
        self.announcer = _DialogAnnouncer(self.status)
        self.close_button = wx.Button(self, wx.ID_CANCEL, label="&Zamknij")
        self.close_button.SetName("Zamknij Radio Browser")

        layout = wx.BoxSizer(wx.VERTICAL)
        layout.Add(self.heading, 0, wx.ALL, 12)
        layout.Add(self.items, 1, wx.LEFT | wx.RIGHT | wx.EXPAND, 12)
        layout.Add(self.status, 0, wx.ALL | wx.EXPAND, 12)
        layout.Add(self.close_button, 0, wx.RIGHT | wx.BOTTOM | wx.ALIGN_RIGHT, 12)
        self.SetSizer(layout)
        self.SetEscapeId(wx.ID_CANCEL)
        self.Bind(wx.EVT_CHAR_HOOK, self._on_key)
        self.items.Bind(wx.EVT_LISTBOX_DCLICK, self._activate)
        self.close_button.Bind(wx.EVT_BUTTON, lambda _event: self.EndModal(wx.ID_CANCEL))
        self.items.SetFocus()

    def start(self) -> None:
        self._load_countries(self)

    def _set_items(
        self, title: str, labels: list[str], *, selected_index: int = 0,
        focus: bool = True,
    ) -> None:
        self.heading.SetLabel(title)
        self.heading.SetName(title)
        self.items.SetName(title)
        self.items.Set(labels)
        if labels:
            self.items.SetSelection(min(selected_index, len(labels) - 1))
        if focus:
            self.items.SetFocus()

    def show_countries(self, payload: object) -> None:
        try:
            self._countries = countries_from_payload(payload)
        except ValueError:
            self.failed("Nie można odczytać krajów Radio Browser")
            return
        self._groups = countries_by_continent(self._countries)
        self._loading = False
        if not self._groups:
            self.failed("Katalog Radio Browser nie zwrócił krajów ze stacjami")
            return
        self._show_continents()

    def _show_continents(self) -> None:
        self._level = "continents"
        labels = list(self._groups)
        selected = labels.index(self._continent) if self._continent in labels else 0
        self._set_items("Kontynenty", labels, selected_index=selected)
        self.announcer.say("Wybierz kontynent i naciśnij Enter")

    def _show_countries(self) -> None:
        assert self._continent is not None
        self._level = "countries"
        countries = self._groups[self._continent]
        selected = next((
            index for index, item in enumerate(countries)
            if self._country is not None and item.code == self._country.code
        ), 0)
        self._set_items(
            f"Kraje: {self._continent}",
            [item.label for item in countries],
            selected_index=selected,
        )
        self.announcer.say(f"{len(countries)} krajów. Wybierz kraj")

    def _show_orders(self) -> None:
        assert self._country is not None
        self._level = "orders"
        selected = next((
            index for index, (_label, order) in enumerate(STATION_ORDERS)
            if order == self._order
        ), 0)
        self._set_items(
            f"Stacje: {self._country.name}, wybierz kolejność",
            [label for label, _order in STATION_ORDERS],
            selected_index=selected,
        )
        self.announcer.say("Wybierz kolejność stacji")

    def _request_stations(self, offset: int = 0) -> None:
        assert self._country is not None
        self._loading = True
        if offset == 0:
            self._entries.clear()
            self._page_addresses.clear()
            self._next_offset = None
            self._set_items("Stacje Radio Browser", ["Pobieranie stacji…"])
        self.announcer.say("Pobieranie stacji")
        self._load_stations(self._country.code, self._order, offset, self)

    def show_stations(self, payload: object, offset: int) -> None:
        try:
            page = station_page_from_payload(payload)
        except ValueError:
            self.failed("Nie można odczytać stacji Radio Browser")
            return
        if offset == 0:
            self._entries.clear()
            self._page_addresses.clear()
        known = {entry.row.item_id for entry in self._entries}
        self._entries.extend(entry for entry in page.entries if entry.row.item_id not in known)
        self._page_addresses.update(page.page_addresses)
        self._next_offset = page.next_offset
        self._level = "stations"
        self._loading = False
        labels = [
            ", ".join(part for part in (entry.row.title, entry.row.detail) if part)
            for entry in self._entries
        ]
        if self._next_offset is not None:
            labels.append("Wczytaj kolejne stacje")
        self._set_items(
            f"Stacje: {self._country.name}", labels,
            selected_index=offset if offset > 0 else 0,
        )
        if not self._entries:
            self.announcer.say("Brak stacji w tej części katalogu")
        else:
            self.announcer.say(f"Wczytano {len(self._entries)} stacji")

    def failed(self, message: str) -> None:
        self._loading = False
        self.announcer.say(message)
        if self._level == "stations" and self._entries:
            self._set_items(
                f"Stacje: {self._country.name}",
                [entry.row.title for entry in self._entries],
            )

    def _selected_entry(self) -> SearchEntry | None:
        index = self.items.GetSelection()
        return self._entries[index] if 0 <= index < len(self._entries) else None

    def _activate(self, _event=None) -> None:
        if self._loading:
            return
        index = self.items.GetSelection()
        if index < 0:
            return
        if self._level == "continents":
            self._continent = list(self._groups)[index]
            self._show_countries()
        elif self._level == "countries":
            self._country = self._groups[self._continent][index]
            self._show_orders()
        elif self._level == "orders":
            self._order = STATION_ORDERS[index][1]
            self._request_stations()
        elif self._level == "stations":
            if index == len(self._entries) and self._next_offset is not None:
                self._request_stations(self._next_offset)
                return
            self.selected_station = self._selected_entry()
            if self.selected_station is not None:
                self.EndModal(wx.ID_OK)

    def _back(self) -> None:
        if self._loading:
            self.EndModal(wx.ID_CANCEL)
            return
        if self._level == "stations":
            self._show_orders()
        elif self._level == "orders":
            self._show_countries()
        elif self._level == "countries":
            self._show_continents()
        else:
            self.EndModal(wx.ID_CANCEL)

    def _copy(self, homepage: bool) -> None:
        entry = self._selected_entry()
        if entry is None:
            self.announcer.say("Wybierz stację")
            return
        value = (
            self._page_addresses.get(entry.row.item_id)
            if homepage else entry.row.address
        )
        if not value:
            self.announcer.say("Ta stacja nie ma linku do strony" if homepage else "Brak adresu strumienia")
            return
        if self._copy_text(value):
            self.announcer.say("Skopiowano link do strony stacji" if homepage else "Skopiowano adres strumienia")

    def _bitrate(self) -> None:
        entry = self._selected_entry()
        if entry is None:
            return
        found = re.search(r"\b\d+\s*kb/s\b", entry.row.detail, re.IGNORECASE)
        self.announcer.say(f"Bitrate {found.group(0)}" if found else "Bitrate nieznany")

    def _on_key(self, event: wx.KeyEvent) -> None:
        code = event.GetKeyCode()
        ctrl, shift, alt = event.ControlDown(), event.ShiftDown(), event.AltDown()
        if code == wx.WXK_ESCAPE and not (ctrl or shift or alt):
            self._back()
            return
        if wx.Window.FindFocus() is not self.items:
            event.Skip()
            return
        if code == wx.WXK_BACK and not (ctrl or shift or alt):
            self._back()
        elif code in (wx.WXK_RETURN, wx.WXK_NUMPAD_ENTER) and not (ctrl or shift or alt):
            self._activate()
        elif self._level == "stations" and not self._loading:
            if code in (wx.WXK_LEFT, wx.WXK_RIGHT) and not (ctrl or shift or alt):
                self._bitrate()
            elif ctrl and not alt and code in (ord("C"), ord("c")):
                self._copy(shift)
            elif ctrl and shift and not alt and code in (ord("L"), ord("l"), ord("U"), ord("u")):
                entry = self._selected_entry()
                if entry is not None:
                    action = (
                        SearchResultAction.LIBRARY
                        if code in (ord("L"), ord("l"))
                        else SearchResultAction.FAVORITE
                    )
                    self._station_action([entry], action, self.announcer.say)
            else:
                event.Skip()
        else:
            event.Skip()
