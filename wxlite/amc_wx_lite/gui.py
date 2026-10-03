"""Okno AMC-wx-Lite: NATYWNE kontrolki wxPython.

Zasady dostepnosci przyjete tutaj (i dlaczego):

* ``wx.ListCtrl`` w trybie ``LC_VIRTUAL`` z tekstem NA ZADANIE
  (``OnGetItemText``). Lista 20 000 plikow nie tworzy 20 000 obiektow, a
  czytnik ekranu dostaje zwykla, natywna liste z rolami i nazwami - dziala
  bez dodatku NVDA i z Narratorem.
* Strzalki, Home/End, Tab i pisanie-po-pierwszej-literze naleza do KONTROLKI.
  Nie przejmujemy ich. Przejmujemy tylko to, co ma wlasne znaczenie w AMC
  (Enter, Backspace, F6, Escape, Spacja, Ctrl+cyfra...).
* Fokusu NIE ustawiamy co tick. ``SetFocus`` wola sie wylacznie przy ZMIANIE
  widoku - inaczej czytnik przerywalby sobie mowe w kolko.
* Odswiezamy TYLKO zmienione wiersze/etykiety, nigdy calej listy bez potrzeby.
* Jedna brama komunikatow: ``Announcer``. Zadnego sleepMode, zadnego zapisu
  do appModules NVDA.

Czego tu nie ma swiadomie: WebView2, Sonos, harmonogramow, uslug startowych.
Lekki wariant ma byc lekki; pelny AMC zostaje nietkniety.
"""

from __future__ import annotations

import os
from pathlib import Path

import wx

from .async_gate import BackgroundRunner, StaleResultGate
from .host_client import HostError, HostUnavailable, LiteHostClient, default_host_path
from .library_source import LibrarySnapshot, LibrarySource
from .list_model import ListModel, Row, rows_from_folder_payload, rows_from_stations
from .navigation import (
    Announce,
    Navigator,
    OpenFolder,
    PlayStation,
    PlayTrack,
    SessionId,
    View,
)
from .shortcuts import Action, Chord, describe, resolve
from .state_store import LiteState, Station, StationList, StateStore

APP_NAME = "AMC-wx-Lite"
TEMPO_LABELS = {1: "Mowa – Speedy", 2: "Muzyka – Signalsmith", 0: "Dotychczasowy – SoundTouch"}

# Mapowanie klawiszy wx -> wlasne nazwy z shortcuts.py. Trzymamy to w JEDNYM
# miejscu, zeby tablica skrotow nie zalezala od wx (da sie ja testowac w WSL).
_SPECIAL_KEYS = {
    wx.WXK_RETURN: "Return",
    wx.WXK_NUMPAD_ENTER: "Return",
    wx.WXK_BACK: "Back",
    wx.WXK_ESCAPE: "Escape",
    wx.WXK_SPACE: "Space",
    wx.WXK_DELETE: "Delete",
    wx.WXK_F1: "F1",
    wx.WXK_F2: "F2",
    wx.WXK_F6: "F6",
    wx.WXK_LEFT: "Left",
    wx.WXK_RIGHT: "Right",
    wx.WXK_UP: "Up",
    wx.WXK_DOWN: "Down",
    wx.WXK_TAB: "Tab",
    wx.WXK_HOME: "Home",
    wx.WXK_END: "End",
    wx.WXK_PAGEUP: "Prior",
    wx.WXK_PAGEDOWN: "Next",
}


def chord_from_event(event: wx.KeyEvent) -> Chord:
    code = event.GetKeyCode()
    name = _SPECIAL_KEYS.get(code)
    if name is None:
        if 32 < code < 127:
            name = chr(code).upper()
        else:
            name = f"#{code}"
    return Chord(key=name, ctrl=event.ControlDown(), shift=event.ShiftDown(), alt=event.AltDown())


def format_time(seconds: float | None) -> str:
    """Czas po polsku, krotko - tak go czyta czytnik ekranu."""
    if seconds is None or seconds < 0:
        return "nieznany"
    total = int(seconds)
    minutes, secs = divmod(total, 60)
    hours, minutes = divmod(minutes, 60)
    if hours:
        return f"{hours} godz {minutes} min {secs} s"
    if minutes:
        return f"{minutes} min {secs} s"
    return f"{secs} s"


class Announcer:
    """Visible status plus a standard MSAA alert, without changing focus.

    An explicit repeated query is repeated; unchanged control text is not
    rewritten. A notification call is not itself proof of spoken output.
    """

    def __init__(self, status_field: wx.StaticText) -> None:
        self._status = status_field

    def say(self, text: str) -> None:
        text = (text or "").strip()
        if not text:
            return
        if self._status.GetLabel() != text:
            self._status.SetLabel(text)
            self._status.SetName(text)
        owner = wx.GetTopLevelParent(self._status)
        if owner is not None and not owner.IsActive():
            return
        wx.Accessible.NotifyEvent(wx.ACC_EVENT_SYSTEM_ALERT, self._status, wx.OBJID_CLIENT, 0)


class MediaListCtrl(wx.ListCtrl):
    """Natywna lista wirtualna. Tekst dostarcza model NA ZADANIE."""

    def __init__(self, parent: wx.Window, model: ListModel, label: str) -> None:
        super().__init__(
            parent,
            style=wx.LC_REPORT | wx.LC_VIRTUAL | wx.LC_SINGLE_SEL | wx.BORDER_SUNKEN,
        )
        self.model = model
        self.InsertColumn(0, "Nazwa", width=420)
        self.InsertColumn(1, "Rodzaj", width=120)
        self.InsertColumn(2, "Szczegoly", width=260)
        # Nazwa dla czytnika ekranu i Narratora.
        self.SetName(label)

    # wx wola to tylko dla WIDOCZNYCH wierszy - stad niski koszt duzych list.
    def OnGetItemText(self, item: int, column: int) -> str:  # noqa: N802 - API wx
        return self.model.text_for(item, column)

    def sync_length(self) -> None:
        self.SetItemCount(len(self.model))

    def sync_selection(self) -> None:
        """Ustaw zaznaczenie wg modelu. Bez SetFocus - fokus zmieniamy tylko
        przy przejsciu miedzy widokami."""
        index = self.model.selected_index
        if index < 0 or index >= len(self.model):
            return
        if self.GetFirstSelected() == index:
            return
        self.Select(index)
        self.Focus(index)

    def refresh_row(self, index: int) -> None:
        """Odswiez JEDEN wiersz, nie cala liste."""
        if 0 <= index < len(self.model):
            self.RefreshItem(index)


class StationDialog(wx.Dialog):
    """Dodanie/zmiana stacji. Zwykle pola z etykietami - czytnik je nazwie."""

    def __init__(self, parent: wx.Window, title: str, name: str = "", url: str = "") -> None:
        super().__init__(parent, title=title)
        panel = wx.Panel(self)
        grid = wx.FlexGridSizer(2, 2, 8, 8)

        name_label = wx.StaticText(panel, label="&Nazwa stacji:")
        self.name_field = wx.TextCtrl(panel, value=name, size=(320, -1))
        self.name_field.SetName("Nazwa stacji")
        url_label = wx.StaticText(panel, label="&Adres (http/https):")
        self.url_field = wx.TextCtrl(panel, value=url, size=(320, -1))
        self.url_field.SetName("Adres stacji")

        grid.AddMany(
            [
                (name_label, 0, wx.ALIGN_CENTER_VERTICAL),
                (self.name_field, 1, wx.EXPAND),
                (url_label, 0, wx.ALIGN_CENTER_VERTICAL),
                (self.url_field, 1, wx.EXPAND),
            ]
        )
        grid.AddGrowableCol(1, 1)

        buttons = self.CreateStdDialogButtonSizer(wx.OK | wx.CANCEL)
        # WAZNE: przyciski OK/Anuluj sa dziecmi DIALOGU, nie panelu. Gdy ich
        # sizer wlozymy w sizer PANELU, wxWidgets przerywa asercja
        # CheckExpectedParentIs i modalna petla nigdy nie wraca - okno zostaje
        # niewidoczne, ale wciaz trzyma fokus. Dlatego panel trzyma TYLKO pola,
        # a sizer zewnetrzny nalezy do dialogu.
        inner = wx.BoxSizer(wx.VERTICAL)
        inner.Add(grid, 1, wx.ALL | wx.EXPAND, 12)
        panel.SetSizer(inner)

        outer = wx.BoxSizer(wx.VERTICAL)
        outer.Add(panel, 1, wx.EXPAND)
        outer.Add(buttons, 0, wx.ALL | wx.ALIGN_RIGHT, 12)
        self.SetSizer(outer)
        self.Fit()
        self.name_field.SetFocus()

    @property
    def values(self) -> tuple[str, str]:
        return self.name_field.GetValue().strip(), self.url_field.GetValue().strip()


class LiteFrame(wx.Frame):
    """Okno glowne: panel listy i panel odtwarzacza, przelaczane jak w AMC."""

    def __init__(self, store: StateStore, state: LiteState) -> None:
        super().__init__(None, title=APP_NAME, size=(900, 600))

        self.store = store
        self.state = state
        self.options = state.options
        self.stations = StationList(state.stations)
        self.navigator = Navigator()
        # Biblioteka AMC (SQLite, tylko odczyt). Wlascicielem zapisu profilu
        # pozostaje host C#; ten wariant nigdy nie prowadzi harmonogramow.
        self.library = LibrarySource()
        self.navigator.restore(state.navigation)

        # Brama zna zycie okna, wiec spozniony wynik nie dotknie zniszczonego
        # okna. Nazwy strumieni ("folder", "status"...) uniewazniaja poprzednie
        # zadania tego samego rodzaju.
        self.gate = StaleResultGate(alive=self._window_alive)
        self.runner = BackgroundRunner(self.gate, to_gui=wx.CallAfter)

        self.client: LiteHostClient | None = None
        self._last_status: dict = {}

        self._build_ui()
        self._bind_keys()
        self._start_engine()

    # ------------------------------------------------------------------ UI

    def _build_ui(self) -> None:
        self.panel = wx.Panel(self)
        self.session_label = wx.StaticText(self.panel, label="Pliki lokalne")
        font = self.session_label.GetFont()
        font.SetWeight(wx.FONTWEIGHT_BOLD)
        self.session_label.SetFont(font)

        # --- widok listy
        self.list_panel = wx.Panel(self.panel)
        self.files_list = MediaListCtrl(
            self.list_panel, self.navigator.sessions[SessionId.FILES].model, "Pliki lokalne"
        )
        self.radio_list = MediaListCtrl(
            self.list_panel, self.navigator.sessions[SessionId.RADIO].model, "Stacje radiowe"
        )
        self.radio_list.Hide()
        list_sizer = wx.BoxSizer(wx.VERTICAL)
        list_sizer.Add(self.files_list, 1, wx.EXPAND)
        list_sizer.Add(self.radio_list, 1, wx.EXPAND)
        self.list_panel.SetSizer(list_sizer)

        # --- widok odtwarzacza (zwykle kontrolki, nie wlasne rysowanie)
        self.player_panel = wx.Panel(self.panel)
        self.now_playing = wx.StaticText(self.player_panel, label="Nic nie jest odtwarzane")
        self.now_playing.SetName("Teraz odtwarzane")
        self.time_label = wx.StaticText(self.player_panel, label="0 s / nieznany")
        self.time_label.SetName("Czas")

        self.play_button = wx.Button(self.player_panel, label="&Pauza/wznow")
        self.volume_slider = wx.Slider(
            self.player_panel, value=self.options.volume, minValue=0, maxValue=100,
            style=wx.SL_HORIZONTAL | wx.SL_LABELS,
        )
        self.volume_slider.SetName("Glosnosc")
        self.rate_slider = wx.Slider(
            self.player_panel, value=int(self.options.rate * 100), minValue=50, maxValue=200,
            style=wx.SL_HORIZONTAL | wx.SL_LABELS,
        )
        self.rate_slider.SetName("Tempo w procentach")

        player_sizer = wx.BoxSizer(wx.VERTICAL)
        for control, flag in (
            (self.now_playing, 0),
            (self.time_label, 0),
            (self.play_button, 0),
            (wx.StaticText(self.player_panel, label="&Glosnosc:"), 0),
            (self.volume_slider, 0),
            (wx.StaticText(self.player_panel, label="&Tempo:"), 0),
            (self.rate_slider, 0),
        ):
            player_sizer.Add(control, flag, wx.ALL | wx.EXPAND, 6)
        self.player_panel.SetSizer(player_sizer)
        self.player_panel.Hide()

        self.status_field = wx.StaticText(self.panel, label="Gotowe")
        self.status_field.SetName("Komunikaty")
        self.announcer = Announcer(self.status_field)

        outer = wx.BoxSizer(wx.VERTICAL)
        outer.Add(self.session_label, 0, wx.ALL, 8)
        outer.Add(self.list_panel, 1, wx.EXPAND | wx.LEFT | wx.RIGHT, 8)
        outer.Add(self.player_panel, 1, wx.EXPAND | wx.LEFT | wx.RIGHT, 8)
        outer.Add(self.status_field, 0, wx.ALL | wx.EXPAND, 8)
        self.panel.SetSizer(outer)

        self._build_menu()

        self.timer = wx.Timer(self)
        self.Bind(wx.EVT_TIMER, self._on_timer, self.timer)
        self.Bind(wx.EVT_CLOSE, self._on_close)

    def _build_menu(self) -> None:
        """Menu daje te same polecenia co skroty - wazne dla odkrywalnosci."""
        bar = wx.MenuBar()

        files_menu = wx.Menu()
        self.id_open_folder = files_menu.Append(wx.ID_ANY, "Otworz &folder\tCtrl+O").GetId()
        self.id_open_file = files_menu.Append(wx.ID_ANY, "Otworz &plik\tCtrl+Shift+O").GetId()
        files_menu.AppendSeparator()
        self.id_quit = files_menu.Append(wx.ID_EXIT, "&Zakoncz\tAlt+F4").GetId()
        bar.Append(files_menu, "&Pliki")

        radio_menu = wx.Menu()
        self.id_station_add = radio_menu.Append(wx.ID_ANY, "&Dodaj stacje\tCtrl+N").GetId()
        self.id_station_edit = radio_menu.Append(wx.ID_ANY, "&Zmien stacje\tF2").GetId()
        self.id_station_delete = radio_menu.Append(wx.ID_ANY, "&Usun stacje\tDelete").GetId()
        radio_menu.AppendSeparator()
        self.id_station_import = radio_menu.Append(wx.ID_ANY, "&Importuj M3U/PLS\tCtrl+I").GetId()
        bar.Append(radio_menu, "&Radio")

        view_menu = wx.Menu()
        self.id_session_files = view_menu.Append(wx.ID_ANY, "Sesja: &Pliki lokalne\tCtrl+1").GetId()
        self.id_session_radio = view_menu.Append(wx.ID_ANY, "Sesja: &Radio\tCtrl+2").GetId()
        view_menu.AppendSeparator()
        self.id_show_player = view_menu.Append(wx.ID_ANY, "Widok &odtwarzacza\tF6").GetId()
        bar.Append(view_menu, "&Widok")

        audio_menu = wx.Menu()
        algorithms = wx.Menu()
        self.tempo_items = {}
        for value, label in TEMPO_LABELS.items():
            item = algorithms.AppendRadioItem(wx.ID_ANY, label)
            item.Check(value == self.options.tempo_algorithm)
            self.tempo_items[value] = item
            self.Bind(wx.EVT_MENU, lambda _event, choice=value: self._set_tempo_algorithm(choice), id=item.GetId())
        audio_menu.AppendSubMenu(algorithms, "&Algorytm przyspieszania")
        bar.Append(audio_menu, "&Dźwięk")

        help_menu = wx.Menu()
        self.id_help = help_menu.Append(wx.ID_HELP, "&Skroty klawiszowe\tF1").GetId()
        bar.Append(help_menu, "Pomo&c")

        self.SetMenuBar(bar)

        for identifier, handler in (
            (self.id_open_folder, lambda _e: self._choose_folder()),
            (self.id_open_file, lambda _e: self._choose_file()),
            (self.id_quit, lambda _e: self.Close()),
            (self.id_station_add, lambda _e: self._station_add()),
            (self.id_station_edit, lambda _e: self._station_edit()),
            (self.id_station_delete, lambda _e: self._station_delete()),
            (self.id_station_import, lambda _e: self._station_import()),
            (self.id_session_files, lambda _e: self._switch_session(SessionId.FILES)),
            (self.id_session_radio, lambda _e: self._switch_session(SessionId.RADIO)),
            (self.id_show_player, lambda _e: self._run(self.navigator.show_player())),
            (self.id_help, lambda _e: self._show_help()),
        ):
            self.Bind(wx.EVT_MENU, handler, id=identifier)

    def _bind_keys(self) -> None:
        for control in (self.files_list, self.radio_list):
            control.Bind(wx.EVT_KEY_DOWN, self._on_key)
            control.Bind(wx.EVT_LIST_ITEM_ACTIVATED, lambda _e: self._activate())
            control.Bind(wx.EVT_LIST_ITEM_SELECTED, self._on_item_selected)
        for control in (self.player_panel, self.play_button, self.volume_slider, self.rate_slider):
            control.Bind(wx.EVT_KEY_DOWN, self._on_key)
        self.play_button.Bind(wx.EVT_BUTTON, lambda _e: self._play_pause())
        self.volume_slider.Bind(wx.EVT_SLIDER, self._on_volume_slider)
        self.rate_slider.Bind(wx.EVT_SLIDER, self._on_rate_slider)

    def _set_tempo_algorithm(self, value: int) -> None:
        """Persist only a selection acknowledged by the real host."""
        if value not in TEMPO_LABELS:
            return
        previous = self.options.tempo_algorithm
        if self.client is None:
            for algorithm, item in self.tempo_items.items():
                item.Check(algorithm == previous)
            self.announcer.say("Silnik nie jest gotowy.")
            return
        client = self.client
        payload = self.options.audio_payload()
        payload["tempoAlgorithm"] = value
        for item in self.tempo_items.values():
            item.Enable(False)

        def restore_selection(selected: int) -> None:
            for algorithm, item in self.tempo_items.items():
                item.Check(algorithm == selected)
                item.Enable(True)

        def failed(error: Exception) -> None:
            restore_selection(previous)
            self.announcer.say(f"Nie zmieniono algorytmu: {error}")

        def done(result: dict) -> None:
            if not isinstance(result, dict) or result.get("tempoAlgorithm") != value:
                failed(RuntimeError("Silnik nie potwierdził wybranego algorytmu."))
                return
            self.options.tempo_algorithm = value
            restore_selection(value)
            if self._save_state() is False:
                return
            suffix = ". Zmiana po ponownym otwarciu materiału." if result.get("appliesOnNextPlayback") else "."
            self.announcer.say(TEMPO_LABELS[value] + suffix)

        self.runner.submit("audio-settings", lambda: client.configure_audio(**payload), done, failed)

    # --------------------------------------------------------------- silnik

    def _window_alive(self) -> bool:
        """Brama zycia okna: wynik z tla nie moze dotknac zamknietego okna."""
        return bool(self) and not self.IsBeingDeleted()

    def _start_engine(self) -> None:
        client = LiteHostClient(
            default_host_path(),
            timeshift_minutes=self.options.timeshift_minutes,
            on_event=self._on_engine_event,
            on_stderr=lambda line: None,
        )
        self.client = client
        settings = self.options.audio_payload()

        def work() -> dict:
            try:
                client.start()
                client.hello()
                return client.configure_audio(**settings)
            except Exception:
                client.close()
                raise

        def done(_result: dict) -> None:
            self.timer.Start(1000)
            self._load_initial_content()

        def failed(error: Exception) -> None:
            self.client = None
            self.announcer.say(f"Silnik nie wystartował: {error}. Odtwarzanie niedostępne.")

        self.runner.submit("startup", work, done, failed)

    def _on_engine_event(self, name: str, data: dict) -> None:
        """Zdarzenie z WATKU silnika - przerzucamy do GUI przez CallAfter."""
        wx.CallAfter(self._handle_engine_event, name, data)

    def _handle_engine_event(self, name: str, data: dict) -> None:
        if not self._window_alive():
            return
        if name == "playback.started" and data.get("tempoFallbackReason"):
            self.announcer.say("Wybrany algorytm tempa jest niedostępny. Używany SoundTouch.")
        elif name == "playback.ended":
            self.announcer.say("Koniec utworu")
        elif name == "playback.failed":
            self._run(self.navigator.note_playback_failed(
                f"Nie udalo sie odtworzyc: {data.get('message', 'blad')}"
            ))
        elif name == "radio.nowPlaying":
            title = str(data.get("streamTitle") or "").strip()
            if title:
                self.now_playing.SetLabel(title)
                self.announcer.say(title)

    # ------------------------------------------------------- tresc poczatkowa

    def _load_initial_content(self) -> None:
        self._run(self.navigator.apply_stations(rows_from_stations(self.stations.as_payload())))
        # Biblioteka AMC ma PIERWSZENSTWO nad przegladaniem dysku. Dawniej
        # bylo odwrotnie: pytalismy ``Path(folder).exists()``, a skoro sciezki
        # profilu (D:\, C:\Users\micha) na tej maszynie nie istnieja, lista pod
        # Ctrl+1 byla pusta -- to jest zglaszany blad.
        if self.library.is_available:
            self._open_library(self.library.saved_folder())
            return
        folder = self.navigator.sessions[SessionId.FILES].folder_path or self.options.last_folder
        if folder and Path(folder).exists():
            self._open_folder(folder)
        else:
            self.announcer.say(self.library.describe() or "Wybierz folder: Ctrl+O")

    def _open_library(self, folder: str | None, preferred_id: str | None = None) -> None:
        """Wczytanie poziomu Biblioteki z SQLite -- POZA watkiem GUI.

        Baza ma 11 tysiecy rekordow, wiec odczyt nie moze blokowac okna, nawet
        jesli jest szybki.
        """
        self.announcer.say("Wczytywanie Biblioteki...")

        def work() -> LibrarySnapshot:
            return self.library.load(folder)

        def done(snapshot: LibrarySnapshot) -> None:
            if snapshot.is_empty:
                self.announcer.say("Biblioteka jest pusta")
                return
            self._run(self.navigator.apply_folder(
                snapshot.folder_path or "", snapshot.rows, preferred_id=preferred_id))

        def failed(error: Exception) -> None:
            self.announcer.say(f"Nie moge wczytac Biblioteki: {error}")

        self.runner.submit("folder", work, done, failed)

    # ------------------------------------------------------------- klawisze

    def _on_key(self, event: wx.KeyEvent) -> None:
        chord = chord_from_event(event)
        player = self.navigator.view is View.PLAYER
        radio = self.navigator.active is SessionId.RADIO
        action = resolve(chord, player_view=player, radio_session=radio)
        if action is None:
            # Klawisz NIE jest nasz: oddajemy go kontrolce, zeby natywna
            # nawigacja i czytnik ekranu dzialaly bez zmian.
            event.Skip()
            return
        self._dispatch(action)

    def _dispatch(self, action: Action) -> None:
        if action is Action.SESSION_FILES:
            self._switch_session(SessionId.FILES)
        elif action is Action.SESSION_RADIO:
            self._switch_session(SessionId.RADIO)
        elif action is Action.ACTIVATE:
            self._activate()
        elif action is Action.PARENT_FOLDER:
            self._run(self.navigator.go_to_parent())
        elif action is Action.SHOW_PLAYER:
            self._run(self.navigator.show_player())
        elif action is Action.SHOW_LIST:
            self._run(self.navigator.back_to_list())
        elif action is Action.PLAY_PAUSE:
            self._play_pause()
        elif action in (
            Action.SEEK_BACK_10, Action.SEEK_FORWARD_10,
            Action.SEEK_BACK_60, Action.SEEK_FORWARD_60,
        ):
            self._seek({
                Action.SEEK_BACK_10: -10.0,
                Action.SEEK_FORWARD_10: 10.0,
                Action.SEEK_BACK_60: -60.0,
                Action.SEEK_FORWARD_60: 60.0,
            }[action])
        elif action in (
            Action.VOLUME_UP_5, Action.VOLUME_DOWN_5,
            Action.VOLUME_UP_1, Action.VOLUME_DOWN_1,
        ):
            self._adjust_volume({
                Action.VOLUME_UP_5: 5, Action.VOLUME_DOWN_5: -5,
                Action.VOLUME_UP_1: 1, Action.VOLUME_DOWN_1: -1,
            }[action])
        elif action is Action.RATE_UP:
            self._set_rate(self.options.rate + 0.1)
        elif action is Action.RATE_DOWN:
            self._set_rate(self.options.rate - 0.1)
        elif action is Action.RATE_RESET:
            self._set_rate(1.0)
        elif action in (Action.TIME_ELAPSED, Action.TIME_REMAINING, Action.TIME_TOTAL):
            self._announce_time(action)
        elif action is Action.OPEN_FOLDER_DIALOG:
            self._choose_folder()
        elif action is Action.OPEN_FILE_DIALOG:
            self._choose_file()
        elif action is Action.STATION_ADD:
            self._station_add()
        elif action is Action.STATION_EDIT:
            self._station_edit()
        elif action is Action.STATION_DELETE:
            self._station_delete()
        elif action is Action.STATION_IMPORT:
            self._station_import()
        elif action is Action.HELP:
            self._show_help()

    # ---------------------------------------------------- wykonanie zamiarow

    def _run(self, intents: list[object]) -> None:
        """Wykonaj zamiary zwrocone przez Navigator. To JEDYNE miejsce, w
        ktorym logika nawigacji spotyka wx."""
        for intent in intents:
            if isinstance(intent, Announce):
                self.announcer.say(intent.text)
            elif isinstance(intent, OpenFolder):
                # Gdy Biblioteka jest dostepna, wchodzenie w foldery czyta
                # SQLite, a nie dysk -- sciezki profilu moga tu nie istniec.
                if self.library.is_available:
                    self._open_library(intent.path or None, preferred_id=intent.preferred_id)
                else:
                    self._open_folder(intent.path, preferred_id=intent.preferred_id)
            elif isinstance(intent, PlayTrack):
                self._play_track(intent)
            elif isinstance(intent, PlayStation):
                self._play_station(intent)
        self._sync_views()

    def _sync_views(self) -> None:
        """Odwzoruj stan nawigatora. Fokus ruszamy TYLKO przy zmianie widoku."""
        session = self.navigator.session
        self.session_label.SetLabel(
            "Pliki lokalne" if self.navigator.active is SessionId.FILES else "Radio internetowe"
        )

        active_list = self._active_list()
        other_list = self.radio_list if active_list is self.files_list else self.files_list
        active_list.sync_length()
        active_list.sync_selection()

        want_player = session.view is View.PLAYER
        changed = self.player_panel.IsShown() != want_player or other_list.IsShown()

        self.list_panel.Show(not want_player)
        self.player_panel.Show(want_player)
        active_list.Show(not want_player)
        other_list.Hide()
        self.panel.Layout()

        if want_player:
            self.now_playing.SetLabel(session.now_playing_title or "Nic nie jest odtwarzane")
            if changed:
                self.play_button.SetFocus()
        elif changed:
            active_list.SetFocus()

    def _active_list(self) -> MediaListCtrl:
        return (
            self.files_list
            if self.navigator.active is SessionId.FILES
            else self.radio_list
        )

    def _on_item_selected(self, event: wx.ListEvent) -> None:
        """Zaznaczenie z klawiatury/myszy wraca do modelu, zeby ID pozostal
        stabilny po odswiezeniu listy."""
        self.navigator.session.model.select_index(event.GetIndex())
        event.Skip()

    def _activate(self) -> None:
        self._run(self.navigator.activate_selected())

    def _switch_session(self, session_id: SessionId) -> None:
        self._run(self.navigator.switch_session(session_id))

    # ---------------------------------------------------------------- pliki

    def _open_folder(self, path: str, preferred_id: str | None = None) -> None:
        """Skan folderu idzie POZA GUI. Wynik przeterminowany jest odrzucany."""
        client = self.client
        if client is None:
            self.announcer.say("Silnik nie dziala, nie moge wczytac folderu")
            return
        self.announcer.say("Wczytywanie...")

        def work() -> dict:
            return client.list_folder(path)

        def done(payload: dict) -> None:
            # Brama juz odrzucila wynik, jesli uzytkownik poszedl dalej albo
            # okno zniknelo - tutaj jestesmy tylko dla AKTUALNEGO skanu.
            rows = rows_from_folder_payload(payload)
            self.options.last_folder = path
            self._run(self.navigator.apply_folder(path, rows, preferred_id=preferred_id))

        def failed(error: Exception) -> None:
            self.announcer.say(f"Nie moge wczytac folderu: {error}")

        self.runner.submit("folder", work, done, failed)

    def _choose_folder(self) -> None:
        with wx.DirDialog(self, "Wybierz folder z muzyka", style=wx.DD_DIR_MUST_EXIST) as dialog:
            if dialog.ShowModal() == wx.ID_OK:
                self._switch_session(SessionId.FILES)
                self._open_folder(dialog.GetPath())

    def _choose_file(self) -> None:
        wildcard = "Audio|*.mp3;*.flac;*.wav;*.m4a;*.ogg;*.opus;*.wma;*.aac|Wszystkie|*.*"
        with wx.FileDialog(
            self, "Wybierz plik", wildcard=wildcard, style=wx.FD_OPEN | wx.FD_FILE_MUST_EXIST
        ) as dialog:
            if dialog.ShowModal() != wx.ID_OK:
                return
            path = dialog.GetPath()
            self._switch_session(SessionId.FILES)
            self._play_track(PlayTrack(path, f"file:{path}", Path(path).name))
            folder = str(Path(path).parent)
            self._open_folder(folder, preferred_id=f"file:{path}")

    # ----------------------------------------------------------- odtwarzanie

    def _play_track(self, intent: PlayTrack) -> None:
        client = self.client
        if client is None:
            self.announcer.say("Silnik nie dziala, nie moge odtworzyc")
            return

        def work() -> dict:
            return client.play_file(
                intent.path, volume=self.options.volume, rate=self.options.rate, title=intent.title
            )

        def done(_payload: dict) -> None:
            self._refresh_status()

        def failed(error: Exception) -> None:
            self._run(self.navigator.note_playback_failed(f"Nie udalo sie odtworzyc: {error}"))

        self.runner.submit("playback", work, done, failed)

    def _play_station(self, intent: PlayStation) -> None:
        client = self.client
        if client is None:
            self.announcer.say("Silnik nie dziala, nie moge odtworzyc")
            return

        def work() -> dict:
            return client.play_station(
                intent.url, volume=self.options.volume, item_id=intent.item_id, title=intent.title
            )

        def done(_payload: dict) -> None:
            self._refresh_status()

        def failed(error: Exception) -> None:
            self._run(self.navigator.note_playback_failed(f"Nie moge polaczyc ze stacja: {error}"))

        self.runner.submit("playback", work, done, failed)

    def _play_pause(self) -> None:
        client = self.client
        if client is None:
            return
        self.runner.submit(
            "transport",
            client.pause_resume,
            lambda payload: self.announcer.say(
                "Pauza" if (payload or {}).get("paused") else "Odtwarzanie"
            ),
            lambda error: self.announcer.say(f"Blad: {error}"),
        )

    def _seek(self, delta: float) -> None:
        client = self.client
        if client is None:
            return
        self.runner.submit(
            "transport",
            lambda: client.seek_by(delta),
            lambda payload: self.announcer.say(
                format_time((payload or {}).get("positionSeconds"))
            ),
            lambda error: self.announcer.say(f"Nie moge przewinac: {error}"),
        )

    def _adjust_volume(self, delta: int) -> None:
        self._set_volume(self.options.volume + delta)

    def _set_volume(self, value: int) -> None:
        value = max(0, min(100, int(value)))
        self.options.volume = value
        self.volume_slider.SetValue(value)
        self.announcer.say(f"Glosnosc {value}")
        client = self.client
        if client is not None:
            self.runner.submit("volume", lambda: client.set_volume(value), lambda _p: None, lambda _e: None)

    def _set_rate(self, value: float) -> None:
        value = max(0.5, min(2.0, round(float(value), 2)))
        self.options.rate = value
        self.rate_slider.SetValue(int(value * 100))
        self.announcer.say(f"Tempo {int(value * 100)} procent")
        client = self.client
        if client is not None:
            self.runner.submit("rate", lambda: client.set_rate(value), lambda _p: None, lambda _e: None)

    def _on_volume_slider(self, event: wx.CommandEvent) -> None:
        self._set_volume(self.volume_slider.GetValue())
        event.Skip()

    def _on_rate_slider(self, event: wx.CommandEvent) -> None:
        self._set_rate(self.rate_slider.GetValue() / 100.0)
        event.Skip()

    def _announce_time(self, action: Action) -> None:
        status = self._last_status
        position = status.get("positionSeconds")
        duration = status.get("durationSeconds")
        if action is Action.TIME_ELAPSED:
            self.announcer.say(f"Minelo {format_time(position)}")
        elif action is Action.TIME_TOTAL:
            self.announcer.say(f"Calosc {format_time(duration)}")
        else:
            if isinstance(position, (int, float)) and isinstance(duration, (int, float)) and duration > 0:
                self.announcer.say(f"Pozostalo {format_time(duration - position)}")
            else:
                self.announcer.say("Czas pozostaly nieznany")

    # ------------------------------------------------------------- status

    def _on_timer(self, _event: wx.TimerEvent) -> None:
        # Czas odswiezamy TYLKO w widoku odtwarzacza i TYLKO etykiete czasu.
        if self.navigator.view is not View.PLAYER:
            return
        self._refresh_status()

    def _refresh_status(self) -> None:
        client = self.client
        if client is None:
            return
        def done(payload: dict) -> None:
            if not payload:
                return
            self._last_status = payload
            position = payload.get("positionSeconds")
            duration = payload.get("durationSeconds")
            label = f"{format_time(position)} / {format_time(duration)}"
            # Odswiezamy etykiete tylko gdy TEKST sie zmienil - inaczej
            # czytnik ekranu dostawalby zmiane co sekunde bez potrzeby.
            if self.time_label.GetLabel() != label:
                self.time_label.SetLabel(label)

        self.runner.submit("status", client.status, done, lambda _error: None)

    # ------------------------------------------------------------- stacje

    def _selected_station(self) -> Station | None:
        row = self.navigator.sessions[SessionId.RADIO].model.selected_row
        return self.stations.find(row.item_id) if row is not None else None

    def _reload_stations(self, preferred_id: str | None = None) -> None:
        rows = rows_from_stations(self.stations.as_payload())
        self.state.stations = self.stations.stations
        self._save_state()
        self._run(self.navigator.apply_stations(rows, preferred_id=preferred_id))

    def _station_add(self) -> None:
        self._switch_session(SessionId.RADIO)
        with StationDialog(self, "Dodaj stacje") as dialog:
            if dialog.ShowModal() != wx.ID_OK:
                return
            name, url = dialog.values
        try:
            station = self.stations.add(name, url)
        except ValueError as error:
            self.announcer.say(str(error))
            return
        self._reload_stations(preferred_id=station.id)
        self.announcer.say(f"Dodano {station.name}")

    def _station_edit(self) -> None:
        station = self._selected_station()
        if station is None:
            self.announcer.say("Nie wybrano stacji")
            return
        with StationDialog(self, "Zmien stacje", station.name, station.url) as dialog:
            if dialog.ShowModal() != wx.ID_OK:
                return
            name, url = dialog.values
        try:
            self.stations.edit(station.id, name, url)
        except ValueError as error:
            self.announcer.say(str(error))
            return
        self._reload_stations(preferred_id=station.id)
        self.announcer.say(f"Zapisano {name or url}")

    def _station_delete(self) -> None:
        station = self._selected_station()
        if station is None:
            self.announcer.say("Nie wybrano stacji")
            return
        if wx.MessageBox(
            f"Usunac stacje {station.name}?", "Potwierdzenie",
            wx.YES_NO | wx.NO_DEFAULT | wx.ICON_QUESTION, self,
        ) != wx.YES:
            return
        self.stations.remove(station.id)
        self._reload_stations()
        self.announcer.say(f"Usunieto {station.name}")

    def _station_import(self) -> None:
        """Import M3U/PLS ISTNIEJACYM importerem AMC (w hoscie).

        Plik uzytkownika czytamy TYLKO po jego wskazaniu - nic nie jest
        przenoszone ani kopiowane automatycznie.
        """
        client = self.client
        if client is None:
            self.announcer.say("Silnik nie dziala, import niedostepny")
            return
        wildcard = "Listy stacji|*.m3u;*.m3u8;*.pls;*.xspf;*.asx|Wszystkie|*.*"
        with wx.FileDialog(
            self, "Wskaz liste stacji", wildcard=wildcard, style=wx.FD_OPEN | wx.FD_FILE_MUST_EXIST
        ) as dialog:
            if dialog.ShowModal() != wx.ID_OK:
                return
            path = dialog.GetPath()

        self._switch_session(SessionId.RADIO)

        def work() -> dict:
            return client.import_playlist(path)

        def done(payload: dict) -> None:
            entries = (payload or {}).get("stations") or []
            added, skipped = self.stations.merge_imported(entries)
            self._reload_stations()
            self.announcer.say(f"Zaimportowano {added}, pominieto {skipped}")

        self.runner.submit(
            "import", work, done, lambda error: self.announcer.say(f"Import nieudany: {error}")
        )

    # --------------------------------------------------------------- pomoc

    def _show_help(self) -> None:
        lines = [f"{chord}\t{label}" for chord, label in describe()]
        wx.MessageBox("\n".join(lines), "Skroty klawiszowe", wx.OK | wx.ICON_INFORMATION, self)

    # -------------------------------------------------------- zapis i koniec

    def _save_state(self) -> None:
        self.state.options = self.options.clamp()
        self.state.stations = self.stations.stations
        self.state.navigation = self.navigator.snapshot()
        try:
            self.store.save(self.state)
        except OSError as error:
            self.announcer.say(f"Nie moge zapisac ustawien: {error}")

    def _on_close(self, event: wx.CloseEvent) -> None:
        self.timer.Stop()
        self.gate.cancel_all()
        self._save_state()
        if self.client is not None:
            self.client.close()
        event.Skip()


class LiteApp(wx.App):
    def OnInit(self) -> bool:  # noqa: N802 - API wx
        self.SetAppName(APP_NAME)
        store = StateStore()
        frame = LiteFrame(store, store.load())
        frame.Show()
        frame.Centre()
        return True


def main() -> int:
    # Na Windows wlasciwe DPI daje czytelny tekst i poprawne wspolrzedne.
    if os.name == "nt":
        try:
            import ctypes

            ctypes.windll.shcore.SetProcessDpiAwareness(1)  # type: ignore[attr-defined]
        except Exception:
            pass
    app = LiteApp(False)
    app.MainLoop()
    return 0
