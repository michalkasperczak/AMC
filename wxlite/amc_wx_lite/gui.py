"""Okno AMC-wx-Lite: NATYWNE kontrolki wxPython.

Zasady dostepnosci przyjete tutaj (i dlaczego):

* ``wx.ListCtrl`` w trybie ``LC_REPORT`` -- ZWYKLA natywna lista Windows, tak
  jak w SARA i WinZappie. Teksty siedza w kontrolce, a przy zmianie danych
  aktualizujemy TYLKO faktycznie zmienione wiersze i pola (``list_sync``).
  Czytnik ekranu dostaje zwykla, natywna liste z rolami i nazwami -- dziala
  bez dodatku NVDA i z Narratorem. Wirtualizacja (``LC_VIRTUAL`` +
  ``OnGetItemText``) ZOSTALA USUNIETA: jej cache tekstu dawal czytnikowi
  stara nazwe z nowym licznikiem, a ``SetItemCount`` przestawial cala liste
  nawet wtedy, gdy dane sie nie zmienily.
* Strzalki, Home/End, Tab i pisanie-po-pierwszej-literze naleza do KONTROLKI.
  Nie przejmujemy ich. Przejmujemy tylko to, co ma wlasne znaczenie w AMC
  (Enter, Backspace, F6, Escape, Spacja, Ctrl+cyfra...).
* Fokusu NIE ustawiamy co tick. ``SetFocus`` wola sie wylacznie przy ZMIANIE
  widoku - inaczej czytnik przerywalby sobie mowe w kolko.
* Odswiezamy TYLKO zmienione wiersze/etykiety, nigdy calej listy bez potrzeby.
* Jedna brama komunikatow: ``Announcer``. Zadnego sleepMode, zadnego zapisu
  do appModules NVDA.

Czego tu nie ma swiadomie: WebView2, Sonos ani uslug startowych. Plany sa
edytowane w natywnym oknie wxPython i wykonywane przez host C#. Pelny AMC
zostaje nietkniety: wxPython zapisuje wlasne, kompletne nadpisanie planow.
"""

from __future__ import annotations

import os
import sqlite3
import time
import uuid
from pathlib import Path
from typing import Callable

import wx

from .async_gate import BackgroundRunner, StaleResultGate
from .audio_clip import (
    AudioClipContext,
    AudioClipFormatChoice,
    AudioClipSelection,
    append_clip_confirmation_text,
    clip_context_from_status,
    describe_backup_outcome,
    format_choices_from_payload,
    format_clip_time,
    load_keep_audio_edit_backups,
    remove_clip_confirmation_text,
    restore_clip_selection,
    suggested_clip_file_name,
    update_clip_selections,
)
from .audio_output import (
    AudioOutputChoice,
    choices_from_payload,
    effective_output_device_id,
    read_profile_outputs,
)
from .collation import HostCollation
from .host_client import HostError, HostUnavailable, LiteHostClient, default_host_path
from .library_source import LibrarySnapshot, LibrarySource, degradation_notice
from . import list_sync
from . import list_filter
from .list_model import (
    ListModel,
    Row,
    rows_from_folder_payload,
    rows_from_queue_status,
    rows_from_stations,
)
from . import menu_model
from . import profile_presets
from .navigation import (
    Announce,
    LIBRARY_VIEW_FOLDERS,
    LibraryView,
    Navigator,
    OpenFolder,
    OpenLibraryView,
    OpenPodcastAggregateView,
    OpenPodcastView,
    OpenQueueView,
    PlayFromQueue,
    PlayQueueAt,
    PlayStation,
    PlayMedia,
    PlayTrack,
    SessionId,
    TransientNavigationSnapshot,
    View,
    view_context,
)
from .shortcuts import Action, Chord, describe, preset_slot, resolve
from .profile_layout import resolve_layout
from .podcast_source import (
    PAGE_SIZE as PODCAST_PAGE_SIZE,
    PodcastDescription,
    PodcastProfileError,
    PodcastSource,
    SORT_ADDED_NEWEST,
    SORT_ALPHABETICAL,
    SORT_CUSTOM,
    subscription_rows,
)
from .quick_info import (
    HOST_ERROR_MESSAGE,
    QUICK_INFO_OP,
    QUICK_INFO_STREAM,
    quick_info_failure,
    quick_info_plan,
    quick_info_reply,
    read_cached_information,
)
from .radio_activity import (
    STATE_POSITION_LABELS,
    activity_cue_wav,
    state_position_index,
    state_position_value,
)
from .transport_parity import (
    PLAYBACK_RATE_MAX,
    PLAYBACK_RATE_MIN,
    TRACK_END_MARGIN_SECONDS,
    MessagePolicy,
    clamp_playback_rate,
    format_clock,
    format_playback_rate,
    load_message_policy,
    next_playback_rate,
    seek_percent_value,
    seek_step_seconds,
    time_announcement,
)
from . import session_options
from .radio_source import RadioSource
from .radio_schedule_dialogs import RadioSchedulesDialog
from . import radio_schedule_settings
from .radio_recording import (
    active_recording_rows,
    format_duration,
    recording_history_from_amc_state,
    recording_history_payload_from_event,
    recording_history_rows,
    station_activity_rows,
)
from .state_store import LiteState, Station, StationList, StateStore

APP_NAME = "AMC-wx-Lite"
TEMPO_LABELS = {1: "Mowa – Speedy", 2: "Muzyka – Signalsmith", 0: "Dotychczasowy – SoundTouch"}

YOUTUBE_EXPORT_CSV_FILTER = 0
YOUTUBE_EXPORT_OPML_FILTER = 1
YOUTUBE_EXPORT_WILDCARD = (
    "CSV kanałów, zgodny z Google Takeout, NewPipe i FreeTube (*.csv)|*.csv|"
    "OPML dla kanałów i playlist, do czytników RSS (*.opml)|*.opml"
)


def youtube_export_path_for_filter(path: str, filter_index: int) -> str:
    """Nadaj rozszerzenie odpowiadające formatowi wybranemu w dialogu.

    Natywny dialog Windows potrafi zachować poprzednie ``.csv`` w nazwie,
    mimo że użytkownik przeszedł na filtr OPML. Format wybiera jawnie pole
    „Typ pliku”, więc to jego wybór ma pierwszeństwo przed starym rozszerzeniem.
    """
    suffix = ".opml" if filter_index == YOUTUBE_EXPORT_OPML_FILTER else ".csv"
    return str(Path(path).with_suffix(suffix))

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

# Pelne wx udostepnia te stale, ale lekki zastepnik testowy nie musi. Budujemy
# rozszerzenie warunkowo, zeby test dostepnosci GUI nie musial udawac calej
# klawiatury numerycznej tylko dlatego, ze presety obsluguja ja produkcyjnie.
for _wx_name, _key_name in (
    *((f"WXK_NUMPAD{digit}", str(digit)) for digit in range(10)),
    ("WXK_NUMPAD_SUBTRACT", "-"),
    ("WXK_NUMPAD_ADD", "+"),
):
    if hasattr(wx, _wx_name):
        _SPECIAL_KEYS[getattr(wx, _wx_name)] = _key_name


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


def player_time_label(position: float | None, duration: float | None) -> str:
    """Etykieta czasu w oknie odtwarzacza, dokladnie jak w oryginale.

    ``MainWindow.xaml.cs:2643-2647``: gdy czas trwania jest znany, tekst to
    ``{pozycja} z {calosc}`` w formacie ``FormatTime`` (czyli ``m:ss``); gdy
    nie jest -- sama pozycja; a bez odtwarzania zdanie "Stan czasu nieznany".
    Wariant wx mial tu wlasny format ("30 s / 3 min 51 s"), wiec ten sam ekran
    wygladal inaczej niz w AMC.
    """
    if position is None:
        return "Stan czasu nieznany"
    if duration:
        return f"{format_clock(position)} z {format_clock(duration)}"
    return format_clock(position)


#: Komunikat Ctrl+C. ``MainWindow.xaml.cs:24372``. Byl zmierzony jako
#: poprawny (dokladnie jeden na gest, takze przy powtorzeniu) -- nie ruszamy go.
COPIED_NAME_MESSAGE = "Skopiowano nazwę"


def format_item_count(count: int) -> str:
    """Polska odmiana licznika elementow zgodna z pelnym AMC."""
    if count == 1:
        return "1 element"
    last_two_digits = count % 100
    last_digit = count % 10
    suffix = (
        "elementy"
        if 2 <= last_digit <= 4 and not 12 <= last_two_digits <= 14
        else "elementów"
    )
    return f"{count} {suffix}"


def format_file_count(count: int) -> str:
    """Polska odmiana licznika plikow zgodna z pelnym AMC."""
    if count == 1:
        return "1 plik"
    last_two_digits = count % 100
    last_digit = count % 10
    suffix = (
        "pliki"
        if 2 <= last_digit <= 4 and not 12 <= last_two_digits <= 14
        else "plików"
    )
    return f"{count} {suffix}"


def is_local_path(source: str | None) -> bool:
    """``TryGetLocalPath`` -- MainWindow.xaml.cs:5378-5390, regula za regula.

    Oryginal:

        if (string.IsNullOrWhiteSpace(source)
            || Uri.TryCreate(source, UriKind.Absolute, out var uri) && !uri.IsFile)
            return false;
        if (!Path.IsPathFullyQualified(source)) return false;

    czyli trzy odrzucenia: pusty tekst; absolutny URI, ktory NIE jest ``file:``
    (np. ``http://``); sciezka niepelna (wzgledna). Co wazne, oryginal TUTAJ
    NIE sprawdza istnienia pliku -- to osobna regula, patrz ``_file_drop_path``.

    Nie dodajemy nic poza tym -- z JEDNYM jawnym wyjatkiem: ``file:`` URI.
    Oryginal przepuszcza go przez pierwszy warunek (``uri.IsFile`` jest prawda),
    ale zaraz potem odrzuca na ``Path.IsPathFullyQualified("file:///D:/a.mp3")``
    == ``false``, czyli de facto NIE uznaje go za lokalny. My uznajemy i
    sprowadzamy do sciezki (``local_path_from_source``). To nasze rozszerzenie,
    nie przepisana linia oryginalu.
    """
    if not source or not source.strip():
        return False
    source = source.strip()
    scheme = source.split(":", 1)[0].lower() if ":" in source else ""
    # Jednoliterowy "schemat" to litera dysku Windows (``D:\...``), nie URI.
    if len(scheme) > 1 and scheme.isalpha():
        if scheme != "file":
            return False
        return True
    # ``IsPathFullyQualified``: UNC, dysk z separatorem albo korzen POSIX
    # (testy chodza w WSL, wiec sciezka POSIX tez jest w pelni kwalifikowana).
    if source.startswith("\\\\") or source.startswith("//"):
        return True
    if len(source) >= 3 and source[1] == ":" and source[2] in "\\/":
        return True
    return source.startswith("/")


#: ``Preferred DropEffect`` -- liczby shella Windows, nie nasze wymysly.
#: ``DROPEFFECT_COPY = 1``, ``DROPEFFECT_MOVE = 2``. Oryginal wola
#: ``SetData("Preferred DropEffect", BitConverter.GetBytes(2))`` TYLKO przy
#: wycinaniu (``MainWindow.xaml.cs:25303``); strona kopiowania (:24781-24783)
#: nie ustawia tego formatu wcale, a jego BRAK znaczy dla shella kopiowanie.
DROPEFFECT_COPY = 1
DROPEFFECT_MOVE = 2


def file_copy_clipboard_payload(path: str) -> dict:
    """Co ma trafic do schowka przy KOPIOWANIU pliku.

    Odpowiada ``CopyItemLocations`` (``MainWindow.xaml.cs:24778-24787``):
    tekst sciezki ORAZ ``CF_HDROP``, bez ``Preferred DropEffect``.
    """
    return {"text": path, "file_path": path, "preferred_drop_effect": None}


def file_cut_clipboard_payload(path: str) -> dict:
    """Co ma trafic do schowka przy WYCINANIU pliku.

    Odpowiada ``CutLocalFilesForExternalMove``
    (``MainWindow.xaml.cs:25300-25303``): to samo co przy kopiowaniu PLUS
    ``Preferred DropEffect`` = MOVE. Bez tego trzeciego formatu Explorer
    zrobilby KOPIE, a uzytkownik uslyszalby "gotowy do przeniesienia".
    """
    return {"text": path, "file_path": path, "preferred_drop_effect": DROPEFFECT_MOVE}


def local_path_from_source(source: str) -> str:
    """Sprowadza ``file:`` URI do zwyklej sciezki; reszte oddaje bez zmian.

    UWAGA na granice tej funkcji wobec oryginalu. ``TryGetLocalPath``
    (``MainWindow.xaml.cs:5378``) NIE konwertuje ``file:`` URI -- oddaje
    ``localPath = source`` i poleca go dalej ``Path.IsPathFullyQualified``,
    ktory dla ``file:///D:/a.mp3`` zwraca ``false``. Czyli oryginal takiego
    zrodla w ogole nie uznaje za lokalne. Ta konwersja to NASZE rozszerzenie,
    nie doslowne 1:1; opisujemy ja tak wprost, zeby nikt nie przypisal jej
    oryginalowi.

    Istotne jest, zeby nie zgubic nazwy serwera. ``urlparse`` wklada ja do
    ``netloc``, nie do ``path``, wiec ``file://serwer/udzial/a.mp3`` bez tego
    dawalo ``/udzial/a.mp3`` -- sciezke na BIEZACYM dysku, nie na udziale.
    """
    source = (source or "").strip()
    if source.lower().startswith("file:"):
        from urllib.parse import unquote, urlparse

        parsed = urlparse(source)
        path = unquote(parsed.path)
        host = unquote(parsed.netloc)
        # ``file://serwer/udzial/a.mp3`` -> ``\\serwer\udzial\a.mp3``.
        # ``localhost`` to umowna nazwa maszyny biezacej, nie udzial sieciowy.
        if host and host.lower() != "localhost":
            return "\\\\" + host + path.replace("/", "\\")
        # ``file:///D:/a.mp3`` -> ``D:/a.mp3``
        if len(path) >= 3 and path[0] == "/" and path[2] == ":":
            path = path[1:]
        return path
    return source


def copied_address_message(row: Row, *, file_copied: bool = False) -> str:
    """Komunikat Ctrl+Shift+C opisujacy to, co NAPRAWDE trafia do schowka.

    Dla stacji zostaje "Skopiowano bezpośredni adres"
    (``MainWindow.xaml.cs:24400``): adres strumienia to tekst i komunikat
    niczego wiecej nie obiecuje.

    Dla pliku slowa zaleza od DANYCH, nie od zamiaru:

    ``file_copied=True``
        schowek dostal ``wx.FileDataObject`` (``CF_HDROP``) obok tekstu, czyli
        wklejenie w menedzerze plikow utworzy kopie. Dopiero wtedy wolno
        powiedziec "Skopiowano plik i pełną ścieżkę"
        (``MainWindow.xaml.cs:24785``).

    ``file_copied=False``
        sciezki nie ma na dysku (albo nie jest lokalna), wiec file drop
        wskazywalby w pustke -- idzie SAM tekst i komunikat mowi tylko o nim.
        Tak samo jak przed ta zmiana: komunikat nigdy nie obiecuje czynnosci,
        ktorej program nie wykonal.
    """
    if row.kind == "station":
        return "Skopiowano bezpośredni adres"
    return "Skopiowano plik i pełną ścieżkę" if file_copied else "Skopiowano pełną ścieżkę"


#: ``winUser.EVENT_SYSTEM_ALERT`` (``source/winUser.py:355``). NIE uzywamy go
#: na polu statusu -- patrz komentarz w ``Announcer``.
EVENT_SYSTEM_ALERT = 0x0002

#: ``winUser.EVENT_OBJECT_LIVEREGIONCHANGED`` (``source/winUser.py:396``).
EVENT_OBJECT_LIVEREGIONCHANGED = 0x8019

#: ``winUser.OBJID_CLIENT`` i ``CHILDID_SELF`` (``source/winUser.py:416, 410``).
#: Trzymamy je TUTAJ, a nie bierzemy z ``wx``: to stale MSAA, a dzieki temu
#: brak atrybutu w ``wx`` nie moze sie przebrac za "czytnik milczy".
OBJID_CLIENT = -4
CHILDID_SELF = 0


def _notify_win_event(event: int, hwnd: int, object_id: int, child_id: int) -> None:
    """``user32.NotifyWinEvent`` -- zwykle API MSAA, bez dodatku do czytnika.

    Na nie-Windows (testy w WSL) nie ma czego wolac; ``Announcer`` dostaje
    wtedy wlasna funkcje powiadamiania i tu nie zaglada.
    """
    import ctypes

    ctypes.windll.user32.NotifyWinEvent(event, hwnd, object_id, child_id)


def transport_button_label(*, playing: bool, preparing: bool = False) -> str:
    """Nazwa przycisku transportu: CZYNNOSC, ktora wykona nacisniecie.

    Dawne "Pauza/wznow" wymienialo oba warianty naraz, wiec czytnik mowil
    zawsze to samo i nie bylo wiadomo, co sie stanie. Slownictwo i warunek
    bierzemy ze zwyklego AMC, bez nowej rodziny nazw:

        ``MainWindow.xaml.cs:2560``
        ``PlayerPlayPauseButton.Content =``
        ``    preparing ? "Anuluj" : session.IsPlaying ? "Wstrzymaj" : "Odtwórz";``

    ``&`` to klawisz dostepu wx (odpowiednik ``_`` w XAML).
    """
    if preparing:
        return "&Anuluj"
    return "&Wstrzymaj" if playing else "&Odtwórz"


def transport_confirmation(*, paused: bool) -> str:
    """Krotkie potwierdzenie po Spacji: stan, ktory JUZ nastapil.

    Etykieta mowi o przyszlosci ("Wstrzymaj"), potwierdzenie o terazniejszosci
    ("Wstrzymano") -- inaczej czytnik dwa razy powtarza to samo slowo i nie
    wiadomo, czy czynnosc sie udala. Jedno slowo, bo to potwierdzenie gestu,
    nie opis stanu odtwarzacza.
    """
    return "Wstrzymano" if paused else "Odtwarzanie"


class Announcer:
    """JEDNA brama krotkich komunikatow. Widoczny status + mowa czytnika.

    DLACZEGO NIE ``EVENT_SYSTEM_ALERT`` (tak bylo i bylo CICHO):

    NVDA tlumaczy ``EVENT_SYSTEM_ALERT`` na zdarzenie ``"alert"``
    (``source/IAccessibleHandler/internalWinEventHandler.py:37``), a jego
    obsluga ODRZUCA obiekt, ktory nie ma roli alertu:

        ``source/NVDAObjects/IAccessible/__init__.py:2025-2028``
        ``def event_alert(self):``
        ``    if self.role != controlTypes.Role.ALERT:``
        ``        # Ignore alert events on objects that aren't alerts.``
        ``        return``

    Pole statusu to ``wx.StaticText``, czyli okno klasy ``Static`` z rola
    ``ROLE_SYSTEM_STATICTEXT`` -> nakladka ``StaticText``
    (tamze:2842), a nie ``Role.ALERT`` (``IAccessibleHandler/__init__.py:119``
    daje ALERT tylko dla ``ROLE_SYSTEM_ALERT``). Wywolanie bylo poprawne,
    obiekt byl zly -- NVDA wracal w pierwszej linii i milczal. Stad
    "schowek poprawny, a mowy nie bylo".

    DLACZEGO LIVE REGION DZIALA:

        ``source/NVDAObjects/__init__.py:1238-1254``
        ``def event_liveRegionChange(self):``
        ``    name = self.name``
        ``    if name: ... ui.message(name, ...)``

    Zdarzenie ``EVENT_OBJECT_LIVEREGIONCHANGED`` nie sprawdza ROLI -- wymaga
    tylko NIEPUSTEJ nazwy, a nazwa ``wx.StaticText`` to jego tekst, ktory
    ustawiamy ponizej. ``ui.message`` wymawia DOKLADNIE ten tekst: bez slowa
    "alert" i bez nazwy regionu, czyli krotko.

    To ta sama intencja, co w zwyklym AMC: ``AccessibleStatusTextBlock``
    wysyla notyfikacje z TRESCIA, a peer bierze nazwe z tekstu
    (``Controls/AccessibleStatusTextBlock.cs:26-37, 44-48``).

    Pozostale zasady bez zmian: jawnie powtorzone pytanie (Ctrl+E dwa razy)
    dostaje odpowiedz dwa razy, ale tekstu nie przepisujemy bez potrzeby;
    JEDEN komunikat to JEDNO zdarzenie (dwa to podwojna zapowiedz); okno w
    tle aktualizuje status, lecz nie wchodzi w slowo obcej aplikacji.
    """

    def __init__(
        self,
        status_field: wx.StaticText,
        *,
        notify=_notify_win_event,
        status_bar: "NativeStatusBar | None" = None,
    ) -> None:
        self._status = status_field
        self._notify = notify
        # Pasek stanu jest OPCJONALNY: stare wywolania i atrapy w testach nadal
        # maja dzialac, a sama zapowiedz nie moze zalezec od dodatku.
        self._status_bar = status_bar

    def say(self, text: str) -> None:
        text = (text or "").strip()
        if not text:
            return
        if self._status.GetLabel() != text:
            self._status.SetLabel(text)
        # Ta sama tresc ma tez byc DO ODCZYTANIA na zadanie (``NVDA+End``).
        # Pasek stanu tylko ja przechowuje -- nie mowi, wiec nie dubluje
        # zapowiedzi i nie przestawia nawigatora czytnika.
        if self._status_bar is not None:
            self._status_bar.show(text)
        # Nazwe ustawiamy ZAWSZE, nawet gdy tekst sie nie zmienil: wlasnie ja
        # czyta ``event_liveRegionChange``, a powtorzone pytanie ma odpowiedziec.
        self._status.SetName(text)
        owner = wx.GetTopLevelParent(self._status)
        if owner is not None and not owner.IsActive():
            return
        try:
            self._notify(
                EVENT_OBJECT_LIVEREGIONCHANGED,
                int(self._status.GetHandle()),
                OBJID_CLIENT,
                CHILDID_SELF,
            )
        except OSError:
            # Zapowiedz jest DODATKIEM do czynnosci: gdy samo MSAA padnie,
            # kopiowanie albo pauza i tak sie wykonaly, a tekst statusu zostaje
            # do odczytania na zadanie. Zadnego ``sleep`` ani ponowien.
            #
            # Lapiemy WYLACZNIE ``OSError`` (tyle potrafi zglosic
            # ``NotifyWinEvent``). Szerokie ``except Exception`` ukrywalo tu
            # wlasna literowke w nazwie stalej -- czyli dokladnie te ciche
            # milczenie czytnika, ktore naprawiamy.
            pass


class NativeStatusBar:
    """Trzyma aktualny status w NATYWNYM pasku stanu okna -- dla ``NVDA+End``.

    PO CO, skoro ``Announcer`` juz mowi
    -----------------------------------
    ``Announcer`` to kanal ZDARZENIA: mowi w chwili zmiany. ``NVDA+End`` to
    kanal NA ZADANIE: czyta PASEK STANU. NVDA szuka go natywnie i gdy nie
    znajdzie kontrolki paska stanu, sieka tytul okna -- stad zgloszone
    "wieczorne" z "Uspokojenie wieczorne". ``wx.StaticText`` z nazwa
    "Komunikaty" nie jest paskiem stanu (rola ``STATICTEXT``), wiec nie da
    sie go odczytac na zadanie. Zostaje wiec oba kanaly obok siebie.

    Zwykle AMC trzyma je dokladnie tak samo rozdzielnie:
    ``Controls/AccessibleStatusTextBlock.cs`` to kanal mowy, a
    ``Controls/AccessiblePlaybackStatusStrip.cs`` + ``MainWindow.xaml:20-22``
    to natywny ``StatusStrip`` do odczytu na zadanie, jawnie poza fokusem
    (``Focusable="False"``, ``IsTabStop="False"``, ``Selectable, false``):
    "It must remain available to NVDA+End, but it must never become the
    keyboard target".

    ZADNEGO zdarzenia czytnika -- to regula oryginalu
    ------------------------------------------------
    ``AccessiblePlaybackStatusStrip.SpokenText`` celowo nie wysyla
    notyfikacji o zmianie nazwy: "Do not emit a NameChange event every
    second: ... that event can move NVDA's navigator away from the player".
    Dlatego ta klasa tylko PRZECHOWUJE tekst -- nie wola
    ``_notify_win_event`` i nie udaje live regionu. Mowa zostaje tam, gdzie
    byla zmierzona jako dzialajaca: w ``Announcer``.

    Dodatkowo nie piszemy tego samego tekstu dwa razy. Status aktualizuje sie
    takze z zegara transportu, a ``SetStatusText`` przemalowuje pasek --
    powtorzony tekst to czysty koszt bez zmiany tresci.
    """

    def __init__(self, bar) -> None:
        self._bar = bar
        self._text = ""

    def show(self, text: str) -> None:
        text = (text or "").strip()
        if not text or text == self._text:
            return
        self._text = text
        self._bar.SetStatusText(text, 0)

    @property
    def text(self) -> str:
        return self._text


#: Sentinel "kontekst jeszcze nieznany". ``None`` nie nadaje sie na te role:
#: widok Folderow ma ``library_view=None`` i to POPRAWNY kontrakt, wiec
#: ``None`` jest zwyklym, legalnym kontekstem widoku.
_BRAK_KONTEKSTU = object()


class MediaListCtrl(wx.ListCtrl):
    """ZWYKLA natywna lista Windows z aktualizacja tylko tego, co sie zmienilo.

    DLACZEGO NIE JEST JUZ WIRTUALNA. Kontrolka byla ``LC_VIRTUAL`` i oddawala
    tekst na zadanie przez ``OnGetItemText``. Kazde odswiezenie widoku szlo
    wtedy jedna droga: ``SetItemCount`` + ``RefreshItems(0, n-1)`` -- rowniez
    gdy dane sie nie zmienily, bo ``_sync_views`` konczy KAZDY przebieg
    ``_run`` (takze po samym komunikacie, Ctrl+C czy ticku statusu). Na 2476
    wierszach bylo to odswiezenie calej listy na kazde nacisniecie klawisza.

    Zatwierdzony kierunek: zwykle natywne listy (jak w SARA i WinZapp, ktore
    uzytkownik wskazal jako dzialajace dobrze) plus aktualizacje przyrostowe.
    Kontrolka trzyma teksty u siebie, a ``sync_rows`` dopisuje, usuwa, przenosi
    i nadpisuje WYLACZNIE to, co sie rozni.

    Co ZOSTAJE bez zmian: te same trzy kolumny, te same ``item_id`` z
    ``ListModel`` i ta sama trwala kontrolka (bez ``Destroy``/rekreacji HWND --
    ta droga zostala zmierzona i wycofana). Drzewo dostepnosci wierszy nalezy
    do natywnego SysListView32; nie instalujemy na nim ``wx.Accessible``.
    Strzalki, Home/End i pisanie-po-pierwszej-literze naleza do kontrolki;
    na zwyklej liscie dziala to natywnie, bez naszego udzialu.

    Czego ten kod NIE robi: nie dotyka czytnika ekranu. Mniej operacji na
    kontrolce to mniej zdarzen a11y, ale dowodem mowy jest zywy NVDA.
    """

    def __init__(self, parent: wx.Window, model: ListModel, label: str,
                 state: object | None = None) -> None:
        super().__init__(
            parent,
            # Bez ``LC_SINGLE_SEL``: pelne AMC pozwala zaznaczyc Shift/Ctrl
            # kilka wierszy, a polecenia schowka dzialaja na calym wyborze.
            # SysListView32 zachowuje przy tym zwykla obsluge klawiatury i
            # role dostepnosci -- nie budujemy wlasnego mechanizmu wyboru.
            style=wx.LC_REPORT | wx.BORDER_SUNKEN,
        )
        self.model = model
        #: STAN SESJI, ktorej liste pokazujemy (``navigation.SessionState``).
        #: Sluzy WYLACZNIE do odczytu tozsamosci widoku w ``_view_context``;
        #: kontrolka nic w nim nie zmienia. ``None`` = brak wiedzy o widoku, co
        #: wylacza pelna podmiane (bezpieczny domysl: przyrostowo).
        self.state = state
        self.InsertColumn(0, "Nazwa", width=420)
        self.InsertColumn(1, "Rodzaj", width=120)
        self.InsertColumn(2, "Szczegoly", width=260)
        #: Stan POKAZANY w kontrolce. Jedyne zrodlo prawdy do porownania --
        #: odczytywanie tekstow z ``GetItemText`` przy kazdym odswiezeniu
        #: oznaczaloby 7428 wywolan API na liste 2476 wierszy.
        self._shown: list[list_sync.RowText] = []
        #: KONTEKST WIDOKU, ktory ``_shown`` naprawde przedstawia (sentinel, bo
        #: ``None`` jest LEGALNYM kontekstem -- widok Folderow ma
        #: ``library_view=None``). Pelna podmiana listy bramkuje sie o roznice
        #: miedzy tym polem a kontekstem liczonym w ``sync_rows``.
        self._shown_context: object = _BRAK_KONTEKSTU
        # ListModel.replace zawsze tworzy nowa liste, takze dla identycznych danych.
        self._shown_rows_source = self.model.rows
        #: Czy trwa NASZA aktualizacja. Wstawianie wierszy i ``SetItemState``
        #: powoduja, ze kontrolka wysyla ``EVT_LIST_ITEM_SELECTED`` tak samo jak
        #: przy ruchu uzytkownika. Bez tej bramki przejsciowy indeks (np. 0 po
        #: wstawieniu pierwszego wiersza) wszedlby do modelu JAKO NOWY WYBOR i
        #: skasowal wybor uzytkownika. Czytane przez ``MediaListFrame``.
        self.updating = False
        #: TEKST FILTRA obowiazujacy dla tej listy. Ustawia go okno przed
        #: ``sync_rows`` (jedno pole dla wszystkich list, jak ``FilterBox``).
        #: Pusty = zachowanie dokladnie takie jak przed dodaniem filtra.
        self.filter_query: str = ""
        # Natywne SysListView32 MUSI pozostac wlascicielem calego drzewa
        # dostepnosci. Wczesniejsza nakladka ``wx.Accessible`` podawala nazwe
        # samej listy, ale na rzeczywistym NVDA zaslaniala jej dzieci: strzalki
        # przesuwaly fokus i Enter dzialal, a czytnik nie dostawal nazw wierszy.
        # ``SetLabel`` ustawia tekst natywnego okna, ``SetName`` zachowuje
        # jawna nazwe po stronie wx; nie podmieniamy providera MSAA/UIA.
        self.SetLabel(label)
        self.SetName(label)

    # ------------------------------------------------------------ aktualizacja

    def sync_rows(self) -> None:
        """Dociagnij kontrolke do modelu. Brak zmian = ZERO operacji.

        To jest cala zatwierdzona zasada w jednym miejscu: plan wylicza
        ``list_sync``, my go tylko nakladamy. Gdy plan jest pusty i kursor jest
        na miejscu, nie wolamy nawet ``Freeze`` -- bo zamrozenie kontrolki tez
        jest operacja na kontrolce.
        """
        desired = list_sync.model_row_texts(self.model, self.filter_query)
        if self.filter_query and desired and not any(
            row.item_id == self.model.selected_id for row in desired
        ):
            # Enter must activate a visible result, not the now-hidden old row.
            # Match the original list policy's fallback to the first result.
            self.model.select_id(desired[0].item_id)
        ops = list_sync.plan_row_updates(self._shown, desired)
        # TOZSAMOSC WIDOKU, ktory wlasnie mamy pokazac. Potrzebna, bo pelna
        # podmiana listy nalezy do ZMIANY WIDOKU, a nie do zmiany danych w nim.
        context = self._view_context()
        cursor = self._cursor_target()
        if not ops:
            # Nowa lista oznacza zakonczone zastosowanie danych przez model.
            # Sam zamiar zmiany widoku zostawia dotychczasowy obiekt rows.
            if self.model.rows is not getattr(self, "_shown_rows_source", None):
                self._shown_context = context
                self._shown_rows_source = self.model.rows
            if cursor is None:
                # Nic sie nie zmienilo i kursor jest na miejscu: ZERO operacji.
                # To jest cel calej zmiany -- sam komunikat, Ctrl+C czy tick
                # statusu nie dotykaja listy.
                # Brak operacji kontrolki nie wyklucza nowego, identycznego
                # wyniku loadera. Kontekst takiego wyniku zapisano wyzej.
                return
            # Sam kursor: jedno przejscie stanu, bez przemalowania listy.
            self.updating = True
            try:
                self._move_cursor(cursor)
            finally:
                self.updating = False
            # Samo przesuniecie kursora nie zmienia obiektu model.rows.
            return
        # Zmiana struktury/tekstu to JEDNO przemalowanie, nie seria krokow.
        # ``Freeze``/``Thaw`` to standardowy mechanizm wx, nie usypianie i nie
        # wyciszanie czytnika. ``Thaw`` w ``finally``, bo zamrozona kontrolka
        # po wyjatku bylaby niewidoczna. ``Freeze`` NIE wstrzymuje zdarzen
        # a11y ani ``EVT_LIST_ITEM_SELECTED`` -- od tego jest ``updating``.
        self.updating = True
        self.Freeze()
        try:
            self._apply_ops(ops, desired, context)
            self._shown = desired
            # Kontekst ZAAPLIKOWANY, razem z danymi. Nie wczesniej: gdyby lecial
            # przed nalozeniem planu, przerwana aktualizacja zostawilaby liste z
            # danymi starego widoku, a bramke z kontekstem nowego.
            self._shown_context = context
            self._shown_rows_source = self.model.rows
            # Kursor liczymy PONOWNIE: po usunieciu wiersza kontrolka sama
            # przesuwa fokus, wiec stan sprzed podmiany nie jest wiarygodny.
            target = self._cursor_target()
            if target is not None:
                self._move_cursor(target)
        finally:
            self.Thaw()
            self.updating = False
    def _view_context(self) -> tuple | None:
        """Tozsamosc widoku z sesji, albo ``None`` gdy sesji nie znamy.

        Jedno zrodlo prawdy: ``navigation.view_context`` na ``SessionState``,
        ktory kontrolka dostala przy budowie. Zadnej wlasnej kopii stanu.
        """
        state = getattr(self, "state", None)
        if state is None:
            return None
        return view_context(state)

    def _plan_usuwa_fokus_i_wstawia(self, ops: list) -> bool:
        """Czy plan usuwa wiersz Z FOKUSEM i jednoczesnie cos wstawia.

        Tylko taki uklad daje przejsciowy fokus na usuwane dziecko (zmierzone:
        ``DeleteItem`` na sfokusowanym wierszu emituje ``EVENT_OBJECT_FOCUS`` na
        dziecko, ktorego juz nie bedzie). To jest warunek KONIECZNY pelnej
        podmiany, ale NIE wystarczajacy -- patrz ``_wymienia_caly_widok``.
        """
        focused = self.GetFocusedItem()
        if focused < 0:
            return False
        usuwa_fokus = any(
            isinstance(op, list_sync.DeleteRow) and op.index == focused
            for op in ops
        )
        if not usuwa_fokus:
            return False
        return any(isinstance(op, list_sync.InsertRow) for op in ops)

    def _wymienia_caly_widok(self, ops: list, context: tuple | None) -> bool:
        """Czy wolno siegnac po pelna podmiane ``DeleteAllItems`` + wstawienie.

        DWA warunki, OBA konieczne:

        1. ``context`` MUSI sie roznic od kontekstu, ktory lista NAPRAWDE
           pokazuje (``_shown_context``). To jest bramka ZAKRESU: pelna podmiana
           nalezy do RZECZYWISTEJ zmiany widoku (Foldery -> Wszystkie pliki,
           wejscie w folder, zawartosc playlisty). Zwykle usuniecie jednego
           elementu i wstawienie w TYM SAMYM widoku zostaje przyrostowe -- diff,
           punktowe zmiany, zero operacji bez zmian, wybor po ID.
           ``context is None`` (np. atrapa testowa bez sesji) NIGDY nie wlacza
           podmiany: brak wiedzy o widoku to nie dowod jego zmiany.

           Kontekst porownujemy z ZAAPLIKOWANYM, nie z zamiarem: ``_shown_context``
           zapisujemy razem z ``_shown`` dopiero po nalozeniu planu, wiec
           asynchroniczny loader, ktory jeszcze nie oddal danych, nie przestawia
           bramki przed czasem.

        2. Plan MUSI usuwac wiersz z fokusem i wstawiac (``_plan_usuwa_fokus_i_wstawia``).
           Bez tego nie ma przejsciowego zdarzenia fokusu, wiec pelna podmiana
           nie naprawialaby niczego, a kosztowalaby przebudowe 2476 wierszy.
        """
        if context is None or context == self._shown_context:
            return False
        return self._plan_usuwa_fokus_i_wstawia(ops)

    def _apply_ops(self, ops: list, desired: list | None = None,
                   context: tuple | None = None) -> None:
        """Wykonaj plan w podanej kolejnosci. Indeksy sa juz uzgodnione.

        Przy RZECZYWISTEJ zmianie calego widoku (``_wymienia_caly_widok``)
        czyscimy liste jednym ``DeleteAllItems`` i wstawiamy caly ``desired``.
        Powod jest zmierzony: ``DeleteItem`` na sfokusowanym wierszu emituje
        przejsciowy ``EVENT_OBJECT_FOCUS`` na dziecko, ktorego juz nie bedzie
        (NVDA czytal wtedy wiersz dwa razy), a ``DeleteAllItems`` nie emituje
        nic. Wstawiamy WSZYSTKIE wiersze i wszystkie kolumny -- bez paginacji,
        obcinania i bez LC_VIRTUAL.

        W obrebie TEGO SAMEGO widoku droga zostaje nietknieta: usuniecie
        wiersza pod kursorem BEZ zmiany widoku to nadal jedno ``DeleteItem``,
        a zmiana tekstu to jedno ``SetItem``.
        """
        if desired is not None and self._wymienia_caly_widok(ops, context):
            self.DeleteAllItems()
            for index, row in enumerate(desired):
                self.InsertItem(index, row.texts[0])
                for column in range(1, len(row.texts)):
                    if row.texts[column]:
                        self.SetItem(index, column, row.texts[column])
            return
        for op in ops:
            if isinstance(op, list_sync.DeleteRow):
                self.DeleteItem(op.index)
            elif isinstance(op, list_sync.InsertRow):
                self.InsertItem(op.index, op.texts[0])
                for column in range(1, len(op.texts)):
                    if op.texts[column]:
                        self.SetItem(op.index, column, op.texts[column])
            else:
                self.SetItem(op.index, op.column, op.text)

    def fill_initial(self) -> None:
        """Pierwsze wypelnienie. Ta sama droga co kazda pozniejsza zmiana."""
        self.sync_rows()

    # ------------------------------------------------------- kursor (2 rzeczy)

    def _cursor_target(self) -> int | None:
        """Gdzie ma stac kursor, albo ``None`` gdy nie ma co ruszac.

        ZAZNACZENIE i SKUPIONY WIERSZ to osobne wlasciwosci: pytamy o oba.
        Dawna ``sync_selection`` patrzyla tylko na ``GetFirstSelected``, wiec
        rozjazd fokusu po usunieciu wiersza zostawal nienaprawiony.

        PRZY AKTYWNYM FILTRZE indeks z modelu NIE jest indeksem w kontrolce:
        lista pokazuje tylko wiersze dopasowane. Pozycje liczymy wiec z
        ``_shown`` (czyli ze stanu, ktory kontrolka NAPRAWDE ma) po
        ``item_id`` wybranego wiersza -- ta sama zasada "wybor po ID, nie po
        pozycji", ktora rzadzi calym mechanizmem list i ktorej uzywa oryginal
        (``ResolveListSelectionIndex``, ``MainWindowNavigationPolicy.cs:101``).
        Przy niepustym wyniku ``sync_rows`` wybiera pierwszy widoczny wiersz,
        jeżeli poprzedni wybór wypadł z filtra. Tutaj mapujemy już jego ID.
        """
        wanted = self._wanted_visible_index()
        first_selected = self.GetFirstSelected()
        focused = self.GetFocusedItem()
        # W wielokrotnym wyborze Ctrl+strzalka legalnie przesuwa SAM fokus na
        # niezaznaczony wiersz, aby Ctrl+Spacja mogla go potem dolaczyc albo
        # odlaczyc. Jezeli model juz wskazuje ten fokus i jakies zaznaczenie
        # istnieje, stan natywnej listy jest spojny -- brak bitu SELECTED na
        # fokusie jest zamierzony, a nie usterka do naprawienia. Dawna droga
        # ponownie zaznaczala wiersz przy najblizszym ticku i psula wybor
        # nieprzylegajacy.
        if focused == wanted and first_selected != -1:
            return None
        # W liscie wielokrotnego wyboru ``GetFirstSelected`` zwraca PIERWSZY
        # z zaznaczonych wierszy, a niekoniecznie wiersz z fokusem. Jezeli
        # chciany wiersz juz nalezy do zaznaczenia, podajemy go planerowi jako
        # zaznaczony. Inaczej kazdy tick uznawalby poprawny zakres Shift za
        # rozjazd i ponownie dotykal kontrolki (oraz NVDA).
        selected = wanted if self._is_index_selected(wanted) else first_selected
        return list_sync.plan_cursor(
            wanted=wanted,
            selected=selected,
            focused=focused,
        )

    def _is_index_selected(self, index: int) -> bool:
        """Czy konkretny widoczny wiersz nalezy do natywnego zaznaczenia."""
        # Model moze juz wskazywac pierwszy wiersz, gdy natywna kontrolka jest
        # jeszcze pusta (pierwsze ladowanie albo zmiana widoku). Na prawdziwym
        # wxMSW ``GetItemState(0)`` przy ``GetItemCount()==0`` nie zwraca po
        # prostu zera: podnosi ``wxAssertionError: invalid list control item
        # index``. Wyjatek przerywal ``sync_rows`` PRZED ``InsertItem``. Model
        # mial wtedy stacje/plik (wiec Enter go uruchamial), ale SysListView32
        # nie mial zadnych dzieci, dlatego strzalki nie dawaly NVDA tekstu.
        if index < 0 or index >= self.GetItemCount():
            return False
        try:
            return bool(
                self.GetItemState(index, wx.LIST_STATE_SELECTED)
                & wx.LIST_STATE_SELECTED
            )
        except (AttributeError, TypeError):
            # Minimalne atrapy testowe sprzed obslugi wielokrotnego wyboru
            # znaja tylko pierwszy zaznaczony wiersz.
            return self.GetFirstSelected() == index

    def _wanted_visible_index(self) -> int:
        """Indeks wybranego wiersza W WIDOCZNEJ liscie albo -1.

        Bez filtra jest to po prostu ``model.selected_index`` -- te same
        liczby co przed ta zmiana, wiec zadna istniejaca droga sie nie rusza.
        """
        wanted = self.model.selected_index
        if not self.filter_query:
            return wanted
        if wanted < 0:
            return -1
        selected_id = self.model.selected_id
        for index, shown in enumerate(self._shown):
            if shown.item_id == selected_id:
                return index
        return -1

    def _move_cursor(self, index: int) -> None:
        """Przestaw zaznaczenie i fokus, ustawiajac TYLKO brakujace bity.

        ``Select`` + ``Focus`` to dwa wywolania API, dwa zdarzenia MSAA i dwa
        odczyty tego samego wiersza (zmierzone: "Emu ... 1 z 9" dwukrotnie).
        ``SetItemState`` z maska ``SELECTED|FOCUSED`` przestawia oba bity razem
        -- tak samo jak natywne chodzenie strzalkami, ktore nie dubluje odczytu.

        ALE maska musi pokrywac wylacznie to, czego NAPRAWDE brakuje. Po
        wstawieniu wierszy SysListView32 SAM ustawia fokus na wiersz 0; gdy
        potem ustawialismy bit FOCUSED na JUZ skupionym wierszu, kontrolka
        dostawala kolejne przejscie stanu bez potrzeby. Ustawiamy wiec tylko
        bity brakujace -- mniej zdarzen a11y na kazda synchronizacje.

        UCZCIWIE O SKUTKU: ta zmiana NIE usunela podwojnego odczytu pierwszego
        wiersza przy wejsciu do "Wszystkich plikow" (gest W02). Po niej objaw
        wystepuje nadal, powtarzalnie 3/3 (kwit ``odbior-powt-w02-*.json``),
        wiec hipoteza "to powtorny bit FOCUSED" jest FALSYFIKOWANA. Zostaje to
        jako redukcja zbednych operacji, a nie jako naprawa W02; przyczyna
        podwojnego odczytu pozostaje otwarta i jest opisana w raporcie.

        Czego tu NIE MA: opozniania mowy, ``cancelSpeech`` ani usypiania
        czytnika. Nie dublujemy zdarzenia, zamiast tlumic jego skutek.
        """
        wanted = wx.LIST_STATE_SELECTED | wx.LIST_STATE_FOCUSED
        # Pytamy kontrolke, co JUZ ma -- bity, ktore sa na miejscu, pomijamy.
        mask = 0
        if not self._is_index_selected(index):
            mask |= wx.LIST_STATE_SELECTED
        if self.GetFocusedItem() != index:
            mask |= wx.LIST_STATE_FOCUSED
        if mask:
            self.SetItemState(index, wanted & mask, mask)
        # Przewijanie MUSI zostac: na 2476 wierszach kursor poza widokiem byl by
        # regresja. ``EnsureVisible`` samo nie oglasza wiersza.
        self.EnsureVisible(index)

    def sync_cursor(self) -> None:
        """Sam kursor, bez dotykania zawartosci (np. po powrocie z odtwarzacza)."""
        target = self._cursor_target()
        if target is not None:
            self._move_cursor(target)

    def shown_item_id(self, index: int) -> str | None:
        """``item_id`` wiersza, ktory kontrolka POKAZUJE pod tym indeksem.

        Jedyne poprawne tlumaczenie pozycji z kontrolki na tozsamosc danej,
        gdy filtr zweza liste. ``None`` dla indeksu poza zakresem -- wx potrafi
        zapytac o wiersz w trakcie zmiany dlugosci listy.
        """
        if 0 <= index < len(self._shown):
            return self._shown[index].item_id
        return None

    def selected_rows(self) -> list[Row]:
        """Wszystkie zaznaczone wiersze w kolejnosci widocznej na liscie.

        Model zachowuje jedno ID jako kursor/zakotwiczenie nawigacji. Pelny
        zakres Shift/Ctrl jest natomiast stanem natywnej kontrolki Windows i
        trzeba go odczytac przez ``GetFirstSelected``/``GetNextSelected``.
        Mapowanie idzie przez ``_shown``, zeby filtr nie pomylil indeksu
        widocznego z indeksem pelnego modelu.
        """
        item_ids: list[str] = []
        seen: set[str] = set()
        index = self.GetFirstSelected()
        while index != -1:
            item_id = self.shown_item_id(index)
            if item_id is not None and item_id not in seen:
                seen.add(item_id)
                item_ids.append(item_id)
            next_index = self.GetNextSelected(index)
            # Prawdziwy ListCtrl zwraca indeks wiekszy albo -1. Wadliwa
            # atrapa nie moze zawiesic aplikacji ani testu w petli bez konca.
            if next_index != -1 and next_index <= index:
                break
            index = next_index

        rows_by_id = {row.item_id: row for row in self.model.rows}
        return [rows_by_id[item_id] for item_id in item_ids if item_id in rows_by_id]

    def visible_count(self) -> int:
        """Liczba wierszy POKAZANYCH teraz. Zrodlo liczby do statusu filtra."""
        return len(self._shown)


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


class RenameItemDialog(wx.Dialog):
    """Jednoznacznie nazwane pole zmiany nazwy, bez technicznych wartości."""

    def __init__(
        self,
        parent: wx.Window,
        *,
        title: str,
        prompt: str,
        field_name: str,
        value: str,
        help_text: str = "",
    ) -> None:
        super().__init__(parent, title=title)
        panel = wx.Panel(self)
        inner = wx.BoxSizer(wx.VERTICAL)
        if help_text:
            help_label = wx.StaticText(panel, label=help_text)
            help_label.Wrap(460)
            inner.Add(help_label, 0, wx.ALL | wx.EXPAND, 10)
        label = wx.StaticText(panel, label=prompt)
        self.field = wx.TextCtrl(panel, value=value, size=(420, -1))
        self.field.SetName(field_name)
        inner.Add(label, 0, wx.LEFT | wx.RIGHT | wx.TOP, 10)
        inner.Add(self.field, 0, wx.ALL | wx.EXPAND, 10)
        panel.SetSizer(inner)

        buttons = self.CreateStdDialogButtonSizer(wx.OK | wx.CANCEL)
        outer = wx.BoxSizer(wx.VERTICAL)
        outer.Add(panel, 1, wx.EXPAND)
        outer.Add(buttons, 0, wx.ALL | wx.ALIGN_RIGHT, 10)
        self.SetSizer(outer)
        self.Fit()
        self.field.SetFocus()
        self.field.SelectAll()

    @property
    def value(self) -> str:
        return self.field.GetValue().strip()


class PodcastSourceDialog(wx.Dialog):
    """Natywny formularz dodawania źródła Podcastów i YouTube."""

    def __init__(self, parent: wx.Window) -> None:
        super().__init__(
            parent,
            title="Nowy podcast, kanał YouTube lub medium internetowe",
        )
        panel = wx.Panel(self)
        layout = wx.BoxSizer(wx.VERTICAL)

        help_text = (
            "Wpisz adres kanału RSS lub Atom, kanału albo playlisty YouTube, "
            "lub pojedynczego publicznego materiału YouTube. Po zatwierdzeniu "
            "AMC sprawdzi adres i doda źródło do Biblioteki."
        )
        help_label = wx.StaticText(panel, label=help_text)
        help_label.SetName(help_text)
        help_label.Wrap(520)
        layout.Add(help_label, 0, wx.ALL | wx.EXPAND, 10)

        address_label = wx.StaticText(panel, label="&Adres źródła:")
        self.address_field = wx.TextCtrl(panel, value="", size=(500, -1))
        self.address_field.SetName("Adres źródła")
        layout.Add(address_label, 0, wx.LEFT | wx.RIGHT | wx.TOP, 10)
        layout.Add(self.address_field, 0, wx.ALL | wx.EXPAND, 10)

        title_label = wx.StaticText(panel, label="Własna &nazwa, opcjonalnie:")
        self.title_field = wx.TextCtrl(panel, value="", size=(500, -1))
        self.title_field.SetName("Własna nazwa, opcjonalnie")
        layout.Add(title_label, 0, wx.LEFT | wx.RIGHT | wx.TOP, 10)
        layout.Add(self.title_field, 0, wx.ALL | wx.EXPAND, 10)
        panel.SetSizer(layout)

        buttons = wx.StdDialogButtonSizer()
        add_button = wx.Button(self, wx.ID_OK, label="&Sprawdź i dodaj")
        add_button.SetName("Sprawdź i dodaj źródło")
        add_button.SetDefault()
        cancel = wx.Button(self, wx.ID_CANCEL, label="&Anuluj")
        cancel.SetName("Anuluj dodawanie źródła")
        buttons.AddButton(add_button)
        buttons.AddButton(cancel)
        buttons.Realize()

        outer = wx.BoxSizer(wx.VERTICAL)
        outer.Add(panel, 1, wx.EXPAND)
        outer.Add(buttons, 0, wx.ALL | wx.ALIGN_RIGHT, 10)
        self.SetSizer(outer)
        self.SetMinSize((620, 360))
        self.Fit()
        self.address_field.SetFocus()

    @property
    def values(self) -> tuple[str, str]:
        return self.address_field.GetValue().strip(), self.title_field.GetValue().strip()


class PodcastOpmlImportDialog(wx.Dialog):
    """Wybór źródeł bez ujawniania obiektów ani identyfikatorów NVDA."""

    def __init__(self, parent: wx.Window, entries: list[dict]) -> None:
        super().__init__(parent, title="Importuj podcasty z OPML")
        self._entries = [
            {
                "label": str(entry.get("label") or "Podcast").strip() or "Podcast",
                "feedUrl": str(entry.get("feedUrl") or "").strip(),
            }
            for entry in entries
            if str(entry.get("feedUrl") or "").strip()
        ]
        self._included = [True] * len(self._entries)

        panel = wx.Panel(self)
        layout = wx.BoxSizer(wx.VERTICAL)
        help_text = (
            "Wszystkie podcasty są początkowo zaznaczone. "
            "Spacja zmienia stan bieżącej pozycji, Ctrl+A zaznacza wszystkie."
        )
        help_label = wx.StaticText(panel, label=help_text)
        help_label.SetName(help_text)
        help_label.Wrap(620)
        layout.Add(help_label, 0, wx.ALL | wx.EXPAND, 10)

        self.sources = wx.ListBox(panel, style=wx.LB_SINGLE)
        self.sources.SetName("Podcasty do importu")
        self.sources.SetToolTip(help_text)
        layout.Add(self.sources, 1, wx.LEFT | wx.RIGHT | wx.BOTTOM | wx.EXPAND, 10)
        panel.SetSizer(layout)

        buttons = wx.StdDialogButtonSizer()
        import_button = wx.Button(self, wx.ID_OK, label="&Importuj zaznaczone")
        import_button.SetName("Importuj zaznaczone podcasty")
        import_button.SetDefault()
        cancel = wx.Button(self, wx.ID_CANCEL, label="&Anuluj")
        cancel.SetName("Anuluj import podcastów")
        buttons.AddButton(import_button)
        buttons.AddButton(cancel)
        buttons.Realize()

        outer = wx.BoxSizer(wx.VERTICAL)
        outer.Add(panel, 1, wx.EXPAND)
        outer.Add(buttons, 0, wx.ALL | wx.ALIGN_RIGHT, 10)
        self.SetSizer(outer)
        self.SetMinSize((700, 480))
        self._refresh_labels(selected=0)
        self.sources.Bind(wx.EVT_KEY_DOWN, self._on_key)
        self.Bind(wx.EVT_BUTTON, self._on_accept, id=wx.ID_OK)
        self.sources.SetFocus()

    def _label(self, index: int) -> str:
        state = "zaznaczony" if self._included[index] else "niezaznaczony"
        return f"{self._entries[index]['label']}, {state}"

    def _refresh_labels(self, *, selected: int) -> None:
        labels = [self._label(index) for index in range(len(self._entries))]
        if self.sources.GetCount() != len(labels):
            self.sources.Set(labels)
        else:
            for index, label in enumerate(labels):
                if self.sources.GetString(index) != label:
                    self.sources.SetString(index, label)
        if self._entries:
            self.sources.SetSelection(max(0, min(selected, len(self._entries) - 1)))

    def _on_key(self, event: wx.KeyEvent) -> None:
        chord = chord_from_event(event)
        selected = self.sources.GetSelection()
        if chord.canonical == "Space" and 0 <= selected < len(self._included):
            self._included[selected] = not self._included[selected]
            self._refresh_labels(selected=selected)
            return
        if chord.canonical == "Ctrl+A":
            self._included = [True] * len(self._included)
            self._refresh_labels(selected=max(0, selected))
            return
        event.Skip()

    def _on_accept(self, _event: wx.CommandEvent) -> None:
        if not self.selected_feed_urls:
            wx.MessageBox(
                "Zaznacz co najmniej jeden podcast do importu.",
                "Import OPML",
                wx.OK | wx.ICON_INFORMATION,
                self,
            )
            self.sources.SetFocus()
            return
        self.EndModal(wx.ID_OK)

    @property
    def selected_feed_urls(self) -> list[str]:
        return [
            entry["feedUrl"]
            for entry, included in zip(self._entries, self._included)
            if included
        ]


class PodcastDescriptionDialog(wx.Dialog):
    """Native read-only long-form text, with no embedded browser surface."""

    def __init__(
        self,
        parent: wx.Window,
        information: PodcastDescription,
        copy_text: Callable[[str], bool],
    ) -> None:
        super().__init__(parent, title=information.window_title)
        self._information = information.text
        self._copy_text = copy_text

        panel = wx.Panel(self)
        layout = wx.BoxSizer(wx.VERTICAL)
        content_label = wx.StaticText(panel, label="&Treść opisu i informacje:")
        self.content = wx.TextCtrl(
            panel,
            value=information.text,
            style=wx.TE_MULTILINE | wx.TE_READONLY | wx.TE_RICH2,
        )
        # To jest świadomie przygotowany tekst użytkowy, nigdy repr obiektu,
        # identyfikator ani nazwa klasy. Tak samo pełny AMC rozpoczyna fokus od
        # początku właściwego opisu, zamiast od technicznej roli kontrolki.
        self.content.SetName(
            information.initial_focus_name or information.window_title
        )
        self.content.SetInsertionPoint(0)
        layout.Add(content_label, 0, wx.BOTTOM, 4)
        layout.Add(self.content, 1, wx.EXPAND)

        self.copy_status = wx.StaticText(panel, label="")
        self.copy_status.SetName("Stan kopiowania")
        layout.Add(self.copy_status, 0, wx.TOP | wx.EXPAND, 8)
        panel.SetSizer(layout)

        buttons = wx.StdDialogButtonSizer()
        copy_button = wx.Button(self, wx.ID_COPY, label="&Kopiuj wszystko")
        copy_button.SetName("Kopiuj całą treść")
        close_button = wx.Button(self, wx.ID_CANCEL, label="&Zamknij")
        close_button.SetName("Zamknij opis")
        buttons.AddButton(copy_button)
        buttons.AddButton(close_button)
        buttons.Realize()

        outer = wx.BoxSizer(wx.VERTICAL)
        outer.Add(panel, 1, wx.ALL | wx.EXPAND, 12)
        outer.Add(buttons, 0, wx.LEFT | wx.RIGHT | wx.BOTTOM | wx.ALIGN_RIGHT, 12)
        self.SetSizer(outer)
        self.SetMinSize((700, 520))
        self.Bind(wx.EVT_BUTTON, self._on_copy, id=wx.ID_COPY)
        self.Bind(wx.EVT_CHAR_HOOK, self._on_key)
        self.content.SetFocus()

    def _on_copy(self, _event: wx.CommandEvent) -> None:
        if self._copy_text(self._information):
            self.copy_status.SetLabel("Skopiowano całą treść")

    def _on_key(self, event: wx.KeyEvent) -> None:
        if chord_from_event(event).canonical == "Escape":
            self.EndModal(wx.ID_CANCEL)
            return
        event.Skip()


def _preset_slot_from_event(event: wx.KeyEvent) -> int | None:
    """Cyfra/minus/rownosc w dialogach presetow, bez nazw technicznych wx."""
    if event.ControlDown() or event.AltDown() or event.ShiftDown():
        return None
    key = chord_from_event(event).key
    if key in {str(value) for value in range(1, 10)}:
        return int(key)
    return {"0": 10, "-": 11, "=": 12, "+": 12}.get(key)


class PresetListDialog(wx.Dialog):
    """Dostepna lista 12 miejsc. Do kontrolek trafiaja tylko jawne etykiety."""

    def __init__(
        self,
        parent: wx.Window,
        *,
        choices: tuple[profile_presets.PresetChoice, ...],
        session_name: str,
        current_target_id: str | None,
    ) -> None:
        super().__init__(parent, title=f"Presety — {session_name}")
        self._choices = choices
        panel = wx.Panel(self)
        layout = wx.BoxSizer(wx.VERTICAL)

        description_text = (
            f"Presety sesji {session_name}. Cyfry wybierają miejsce. "
            "Enter lub Spacja uruchamia zajętą pozycję. "
            "Ctrl+Alt+Shift+P przypisuje bieżący element."
        )
        description = wx.StaticText(panel, label=description_text)
        description.SetName(description_text)
        layout.Add(description, 0, wx.ALL | wx.EXPAND, 12)

        self.preset_list = wx.ListBox(
            panel,
            choices=[choice.label for choice in choices],
            style=wx.LB_SINGLE,
        )
        self.preset_list.SetName(f"Presety, {session_name}")
        self.preset_list.SetToolTip(
            "Cyfra, minus lub znak równości wybiera miejsce. Enter albo Spacja uruchamia."
        )
        selected = next(
            (index for index, choice in enumerate(choices)
             if choice.target_id is not None and choice.target_id == current_target_id),
            -1,
        )
        if selected < 0:
            selected = next(
                (index for index, choice in enumerate(choices) if choice.target_id is not None),
                0,
            )
        self.preset_list.SetSelection(selected)
        layout.Add(self.preset_list, 1, wx.LEFT | wx.RIGHT | wx.EXPAND, 12)

        self.status = wx.StaticText(panel, label="Gotowe")
        self.status.SetName("Gotowe")
        self._announcer = Announcer(self.status)
        layout.Add(self.status, 0, wx.ALL | wx.EXPAND, 12)
        panel.SetSizer(layout)

        buttons = wx.StdDialogButtonSizer()
        self.activate_button = wx.Button(self, wx.ID_OK, label="&Uruchom")
        self.activate_button.SetName("Uruchom preset")
        self.activate_button.SetDefault()
        close = wx.Button(self, wx.ID_CANCEL, label="&Zamknij")
        close.SetName("Zamknij listę presetów")
        buttons.AddButton(self.activate_button)
        buttons.AddButton(close)
        buttons.Realize()

        shell = wx.BoxSizer(wx.VERTICAL)
        shell.Add(panel, 1, wx.EXPAND)
        shell.Add(buttons, 0, wx.ALL | wx.ALIGN_RIGHT, 12)
        self.SetSizer(shell)
        self.SetMinSize((620, 430))
        self.Fit()
        self.Bind(wx.EVT_CHAR_HOOK, self._on_key)
        self.activate_button.Bind(wx.EVT_BUTTON, self._activate)
        self.preset_list.Bind(wx.EVT_LISTBOX_DCLICK, self._activate)
        self.preset_list.SetFocus()

    @property
    def selected_choice(self) -> profile_presets.PresetChoice | None:
        index = self.preset_list.GetSelection()
        return self._choices[index] if 0 <= index < len(self._choices) else None

    def _on_key(self, event: wx.KeyEvent) -> None:
        slot = _preset_slot_from_event(event)
        if slot is not None:
            self.preset_list.SetSelection(slot - 1)
            return
        code = event.GetKeyCode()
        if not event.ControlDown() and not event.AltDown() and not event.ShiftDown():
            if code in (wx.WXK_RETURN, wx.WXK_NUMPAD_ENTER, wx.WXK_SPACE):
                self._activate(event)
                return
        event.Skip()

    def _activate(self, _event) -> None:
        choice = self.selected_choice
        if choice is None:
            return
        if choice.target_id is None:
            self._announcer.say(
                f"Preset {choice.spoken_shortcut_label} pusty. "
                "Ctrl+Alt+Shift+P przypisuje bieżący element"
            )
            return
        self.EndModal(wx.ID_OK)


class PresetAssignmentDialog(wx.Dialog):
    """Przypisanie z ochrona zajetego miejsca i osobnym trybem usuwania."""

    def __init__(
        self,
        parent: wx.Window,
        *,
        target: profile_presets.PresetTarget,
        choices: tuple[profile_presets.PresetChoice, ...],
        first_free_slot: int | None,
        initial_slot: int,
        session_name: str,
    ) -> None:
        super().__init__(parent, title=f"Przypisz preset — {session_name}")
        self._target = target
        self._choices = choices
        self.selected_slot = 0
        self.selected_action = ""
        self._remove_pending = False
        self._last_requested_slot: int | None = None
        self._replacement_armed_slot: int | None = None

        panel = wx.Panel(self)
        layout = wx.BoxSizer(wx.VERTICAL)
        if first_free_slot is not None:
            free = profile_presets.shortcut_label(first_free_slot, spoken=True)
            description_text = (
                f"Dodaj preset: {target.title}. Pierwsze wolne miejsce: "
                f"preset {profile_presets.shortcut_label(first_free_slot)}, "
                f"skrót Ctrl+Shift+{free}. Naciśnij cyfrę, minus albo znak "
                "równości, a następnie Enter. Escape anuluje."
            )
        else:
            description_text = (
                f"Dodaj preset: {target.title}. Nie ma wolnego miejsca. "
                "Aby zastąpić zajęty preset, wskaż dwukrotnie to samo miejsce "
                "i naciśnij Enter. Escape anuluje."
            )
        description = wx.StaticText(panel, label=description_text)
        description.SetName(description_text)
        layout.Add(description, 0, wx.ALL | wx.EXPAND, 12)

        self.preset_list = wx.ListBox(
            panel,
            choices=[choice.label for choice in choices],
            style=wx.LB_SINGLE,
        )
        self.preset_list.SetName("Miejsca presetów")
        self.preset_list.SetToolTip(
            "Cyfra, minus lub znak równości wybiera miejsce. "
            "Enter zapisuje lub zastępuje. Delete przygotowuje usunięcie."
        )
        self.preset_list.SetSelection(max(0, min(len(choices) - 1, initial_slot - 1)))
        layout.Add(self.preset_list, 1, wx.LEFT | wx.RIGHT | wx.EXPAND, 12)

        self.status = wx.StaticText(panel, label="Gotowe")
        self.status.SetName("Gotowe")
        self._announcer = Announcer(self.status)
        layout.Add(self.status, 0, wx.ALL | wx.EXPAND, 12)
        panel.SetSizer(layout)

        buttons = wx.StdDialogButtonSizer()
        self.save_button = wx.Button(self, wx.ID_OK, label="&Zapisz")
        self.save_button.SetName("Zapisz preset")
        self.save_button.SetDefault()
        cancel = wx.Button(self, wx.ID_CANCEL, label="&Anuluj")
        cancel.SetName("Anuluj przypisanie presetu")
        buttons.AddButton(self.save_button)
        buttons.AddButton(cancel)
        buttons.Realize()

        shell = wx.BoxSizer(wx.VERTICAL)
        shell.Add(panel, 1, wx.EXPAND)
        shell.Add(buttons, 0, wx.ALL | wx.ALIGN_RIGHT, 12)
        self.SetSizer(shell)
        self.SetMinSize((650, 460))
        self.Fit()
        self.Bind(wx.EVT_CHAR_HOOK, self._on_key)
        self.preset_list.Bind(wx.EVT_LISTBOX, self._on_list_selection)
        self.save_button.Bind(wx.EVT_BUTTON, self._confirm)
        self.preset_list.SetFocus()

    @property
    def selected_choice(self) -> profile_presets.PresetChoice | None:
        index = self.preset_list.GetSelection()
        return self._choices[index] if 0 <= index < len(self._choices) else None

    def _on_key(self, event: wx.KeyEvent) -> None:
        slot = _preset_slot_from_event(event)
        if slot is not None:
            self._select_slot(slot, announce=True)
            return
        if not event.ControlDown() and not event.AltDown() and not event.ShiftDown():
            code = event.GetKeyCode()
            if code == wx.WXK_DELETE:
                self._prepare_removal()
                return
            if code in (wx.WXK_RETURN, wx.WXK_NUMPAD_ENTER):
                self._confirm(event)
                return
        event.Skip()

    def _on_list_selection(self, event) -> None:
        self._remove_pending = False
        self._last_requested_slot = None
        self._replacement_armed_slot = None
        event.Skip()

    def _select_slot(self, slot: int, *, announce: bool) -> None:
        self._remove_pending = False
        previous = self.selected_choice
        repeated = (
            self._last_requested_slot == slot
            and previous is not None
            and previous.slot == slot
        )
        self._last_requested_slot = slot
        self.preset_list.SetSelection(slot - 1)
        choice = self.selected_choice
        if choice is None:
            return
        replaces_other = (
            choice.target_id is not None and choice.target_id != self._target.target_id
        )
        self._replacement_armed_slot = slot if replaces_other and repeated else None
        if not announce:
            return
        slot_name = f"Preset {choice.slot_label}"
        if choice.target_id is None:
            text = f"{slot_name} pusty. {self._target.title}. Enter zapisuje, Escape anuluje"
        elif not replaces_other:
            text = f"{slot_name} już zawiera ten element. Enter zatwierdza, Escape anuluje"
        elif self._replacement_armed_slot == slot:
            text = (
                f"Potwierdzono {slot_name}. Enter zastępuje element "
                f"{choice.target_title} elementem {self._target.title}, Escape anuluje"
            )
        else:
            text = (
                f"{slot_name} zajęty: {choice.target_title}. Naciśnij ponownie "
                f"{choice.spoken_shortcut_label}, a następnie Enter, aby zastąpić; "
                "inny klawisz wybiera inne miejsce; Escape anuluje"
            )
        self._announcer.say(text)

    def _prepare_removal(self) -> None:
        choice = self.selected_choice
        if choice is None:
            return
        if choice.target_id is None:
            self._announcer.say(f"Preset {choice.slot_label} jest już pusty")
            return
        self._remove_pending = True
        self._announcer.say(
            f"Usunąć preset {choice.slot_label}: {choice.target_title}? "
            "Enter potwierdza, Escape anuluje"
        )

    def _confirm(self, _event) -> None:
        choice = self.selected_choice
        if choice is None:
            return
        replaces_other = (
            choice.target_id is not None and choice.target_id != self._target.target_id
        )
        if (
            not self._remove_pending
            and replaces_other
            and self._replacement_armed_slot != choice.slot
        ):
            self._announcer.say(
                f"Preset {choice.slot_label} jest zajęty przez {choice.target_title}. "
                f"Naciśnij dwa razy {choice.spoken_shortcut_label}, a następnie "
                "Enter, aby zastąpić, albo Escape, aby anulować"
            )
            return
        self.selected_slot = choice.slot
        self.selected_action = "remove" if self._remove_pending else "save"
        self.EndModal(wx.ID_OK)


class SessionOptionsDialog(wx.Dialog):
    """Opcje sesji. Pokazuje WYLACZNIE opcje, ktore ta sesja umie wykonac.

    Port ``SessionPlaybackOptionsEditor.CreateDialog`` (cs:33-70). Dwie
    rzeczy przeniesione z oryginalu swiadomie:

    * O tym, ktore kontrolki powstaja, decyduja ZDOLNOSCI sesji
      (``session_options.capabilities_for``), a nie to, czy pole istnieje w
      C#. Oryginal ukrywa niewykonalne pola (cs:59-66); my ich nie tworzymy
      wcale -- ukryta kontrolka nadal siedzi w kolejnosci Tab u czytnika.
    * Kazde pole ma wariant "Jak ustawienie ogólne", bo brak wyboru to w
      modelu ``None``, a nie falsz. Bez tego nie dalo by sie WROCIC do
      dziedziczenia po jednorazowym wyborze.

    Wszystkie kontrolki sa ZWYKLE natywne wx (``wx.Choice``) -- czytnik
    nazywa je sam, bez naszych komunikatow.
    """

    #: Kolejnosc wariantow trojstanowych. Dziedziczenie jest PIERWSZE, bo to
    #: stan domyslny; uzytkownik slyszy je przy otwarciu bez wlasnego wyboru.
    _TRISTATE: tuple[tuple[str, bool | None], ...] = (
        ("Jak ustawienie ogólne", None),
        ("Włączone", True),
        ("Wyłączone", False),
    )

    def __init__(self, parent: wx.Window, session, options, overrides, *, drafts=None) -> None:
        super().__init__(parent, title=session_options.dialog_title(session))
        self._options = options
        # Drafty WSZYSTKICH sesji: przelaczenie konfigurowanej sesji nie moze
        # zgubic tego, co uzytkownik juz wybral dla poprzedniej. Kontrakt:
        # Zapisz utrwala kazdy zmieniony draft, Anuluj zaden.
        self._drafts: dict = {
            SessionId.FILES: session_options.SessionPlaybackOverrides(),
            SessionId.RADIO: session_options.SessionPlaybackOverrides(),
            SessionId.PODCASTS: session_options.SessionPlaybackOverrides(),
        }
        if drafts:
            self._drafts.update(drafts)
        self._drafts[session] = overrides
        self._session = session
        self._caps = session_options.capabilities_for(session)
        panel = wx.Panel(self)
        self._panel = panel
        outer = wx.BoxSizer(wx.VERTICAL)
        self._outer = outer

        # WYBOR KONFIGUROWANEJ SESJI. Sam tytul okna nie jest wyborem: bez tej
        # kontrolki dialog konfigurowal WYLACZNIE sesje, ktora wlasnie gra, a
        # wymaganie jest odwrotne -- skonfigurowac druga sesje BEZ przelaczenia
        # odsluchu. Dialog nie dotyka ani ``_switch_session``, ani transportu.
        sessions = [SessionId.FILES, SessionId.RADIO, SessionId.PODCASTS]
        self._sessions = sessions
        row = wx.BoxSizer(wx.HORIZONTAL)
        label = wx.StaticText(panel, label="Konfigurowana &sesja:")
        self.session_choice = wx.Choice(
            panel,
            choices=[session_options.session_display_name(item) for item in sessions],
        )
        self.session_choice.SetName("Konfigurowana sesja")
        self.session_choice.SetSelection(sessions.index(session))
        self.session_choice.Bind(wx.EVT_CHOICE, self._on_session_changed)
        row.Add(label, 0, wx.ALIGN_CENTER_VERTICAL | wx.RIGHT, 8)
        row.Add(self.session_choice, 1, wx.EXPAND)
        outer.Add(row, 0, wx.ALL | wx.EXPAND, 12)

        # Pola opcji siedza w WYMIENIANYM kontenerze: zdolnosci sesji decyduja
        # o tym, ktore kontrolki istnieja, wiec przelaczenie sesji buduje je od
        # nowa (ukryta kontrolka nadal siedzi w kolejnosci Tab u czytnika).
        self._fields_panel: wx.Panel | None = None
        self._fields_slot = wx.BoxSizer(wx.VERTICAL)
        outer.Add(self._fields_slot, 1, wx.EXPAND)

        # Przyciski sa dziecmi DIALOGU, nie panelu -- patrz uwaga w
        # ``StationDialog``: inaczej wxWidgets przerywa asercja i modalna
        # petla nie wraca, a okno trzyma fokus bez obrazu.
        buttons = self.CreateStdDialogButtonSizer(wx.OK | wx.CANCEL)
        shell = wx.BoxSizer(wx.VERTICAL)
        panel.SetSizer(outer)
        shell.Add(panel, 1, wx.EXPAND)
        shell.Add(buttons, 0, wx.ALL | wx.ALIGN_RIGHT, 12)
        self.SetSizer(shell)
        self._build_fields()

    @property
    def session(self):
        """Sesja AKTUALNIE konfigurowana w dialogu."""
        return self._session

    def _on_session_changed(self, _event) -> None:
        """Przelaczenie konfigurowanej sesji. NIE zmienia odsluchu.

        Wybory biezacej sesji ida do draftu, zeby wrocily po powrocie.
        """
        index = self.session_choice.GetSelection()
        if index < 0:
            return
        wanted = self._sessions[index]
        if wanted is self._session:
            return
        self._drafts[self._session] = self._collect()
        self._session = wanted
        self._caps = session_options.capabilities_for(wanted)
        self._build_fields()

    def _build_fields(self) -> None:
        """Zbuduj pola opcji dla BIEZACEJ sesji z jej draftu."""
        overrides = self._drafts[self._session]
        options = self._options
        if self._fields_panel is not None:
            self._fields_slot.Clear(True)
        panel = wx.Panel(self._panel)
        self._fields_panel = panel
        self._fields_slot.Add(panel, 1, wx.EXPAND)
        grid = wx.FlexGridSizer(0, 2, 8, 8)
        self._first = None

        self.loudness = None
        self.transitions = None
        self.silence = None
        self.pause = None

        if self._caps.supports_audio_processing:
            self.loudness = self._add_choice(
                panel, grid, "&Normalizacja głośności:", "Normalizacja głośności",
                self._TRISTATE, overrides.loudness_normalization,
            )
            self.transitions = self._add_choice(
                panel, grid, "Łagodne &przejścia między utworami:",
                "Łagodne przejścia między utworami",
                self._TRISTATE, overrides.smooth_track_transitions,
            )
            # Tylko dlugosci, ktore silnik przyjmuje (LiteAudioSettings.cs:15).
            silence_choices: tuple[tuple[str, int | None], ...] = (
                ("Jak ustawienie ogólne", None),
            ) + tuple(
                (session_options.inter_track_silence_label(value), value)
                for value in session_options.INTER_TRACK_SILENCE_CHOICES
            )
            self.silence = self._add_choice(
                panel, grid, "&Cisza między utworami:", "Cisza między utworami",
                silence_choices, overrides.inter_track_silence_ms,
            )

        if self._caps.supports_player_exit_pause:
            # Etykieta wariantu dziedziczonego mowi WPROST, co z niego wynika
            # (jak ``PlayerExitPausePolicy.DescribeSessionMode``) -- inaczej
            # uzytkownik musialby sprawdzac ustawienia ogolne osobnym gestem.
            inherited = (
                "Jak ustawienie ogólne: wstrzymuj"
                if options.pause_on_player_exit
                else "Jak ustawienie ogólne: odtwarzaj dalej"
            )
            self.pause = self._add_choice(
                panel, grid, "Po &wyjściu z odtwarzacza:", "Po wyjściu z odtwarzacza",
                (
                    (inherited, None),
                    ("Wstrzymuj odtwarzanie", True),
                    ("Odtwarzaj dalej", False),
                ),
                overrides.pause_on_player_exit,
            )

        grid.AddGrowableCol(1, 1)
        inner = wx.BoxSizer(wx.VERTICAL)
        inner.Add(grid, 1, wx.ALL | wx.EXPAND, 12)
        panel.SetSizer(inner)
        self.Fit()
        self.Layout()
        if self._first is not None:
            self._first.SetFocus()

    def _add_choice(self, panel, grid, label, name, choices, current) -> wx.Choice:
        """Jedna zwykla lista wyboru z etykieta. Etykieta ma skrot literowy."""
        static = wx.StaticText(panel, label=label)
        control = wx.Choice(panel, choices=[text for text, _ in choices])
        # Nazwa dostepnosciowa BEZ znaku ``&`` i bez dwukropka: czytnik czyta
        # nazwe kontrolki, a nie tekst etykiety graficznej.
        control.SetName(name)
        control._amc_values = [value for _, value in choices]  # type: ignore[attr-defined]
        index = next(
            (i for i, (_, value) in enumerate(choices) if value == current and type(value) is type(current)),
            0,
        )
        control.SetSelection(index)
        grid.AddMany(
            [(static, 0, wx.ALIGN_CENTER_VERTICAL), (control, 1, wx.EXPAND)]
        )
        if self._first is None:
            self._first = control
        return control

    @staticmethod
    def _value(control: wx.Choice | None):
        if control is None:
            return None
        index = control.GetSelection()
        if index < 0:
            return None
        return control._amc_values[index]  # type: ignore[attr-defined]

    def _collect(self):
        """Wybory UZYTKOWNIKA dla BIEZACEJ sesji. Bez skutku ubocznego."""
        return session_options.SessionPlaybackOverrides(
            loudness_normalization=self._value(self.loudness),
            smooth_track_transitions=self._value(self.transitions),
            inter_track_silence_ms=self._value(self.silence),
            pause_on_player_exit=self._value(self.pause),
        )

    @property
    def overrides(self):
        """Wybory dla sesji aktualnie pokazanej w dialogu."""
        return self._collect()

    @property
    def drafts(self) -> dict:
        """Wybory KAZDEJ edytowanej sesji. Zapisz utrwala wszystkie.

        Bez tego skonfigurowanie drugiej sesji i zatwierdzenie przepadlo by:
        handler widzialby tylko sesje pokazana na koniec.
        """
        result = dict(self._drafts)
        result[self._session] = self._collect()
        return result


class AudioClipExportDialog(wx.Dialog):
    """Dostepny wybor SPOSOBU zapisu, oddzielony od wartosci protokolu.

    ``wx.Choice`` dostaje wylacznie jawne polskie etykiety. Wartosci
    ``original/flac/wav`` sa przechowywane osobno, wiec nazwa enumu ani repr
    obiektu nie moze trafic do UI Automation i mowy NVDA.
    """

    def __init__(
        self,
        parent: wx.Window,
        *,
        context: AudioClipContext,
        selection: AudioClipSelection,
        choices: list[AudioClipFormatChoice],
        notice: str,
    ) -> None:
        super().__init__(parent, title="Zapisz fragment audio")
        panel = wx.Panel(self)
        outer = wx.BoxSizer(wx.VERTICAL)

        start = format_clip_time(selection.start_seconds or 0.0)
        end = format_clip_time(selection.end_seconds or 0.0)
        duration = format_clip_time(
            (selection.end_seconds or 0.0) - (selection.start_seconds or 0.0)
        )
        summary = wx.StaticText(
            panel,
            label=(
                f"{context.title}. Od {start} do {end}. "
                f"Długość fragmentu: {duration}."
            ),
        )
        summary.SetName(summary.GetLabel())
        outer.Add(summary, 0, wx.ALL | wx.EXPAND, 12)

        label = wx.StaticText(panel, label="&Sposób zapisu:")
        self.format_choice = wx.Choice(panel, choices=[choice.label for choice in choices])
        self.format_choice.SetName("Sposób zapisu fragmentu")
        self._choices = choices
        self.format_choice.SetSelection(0)
        row = wx.BoxSizer(wx.HORIZONTAL)
        row.Add(label, 0, wx.ALIGN_CENTER_VERTICAL | wx.RIGHT, 8)
        row.Add(self.format_choice, 1, wx.EXPAND)
        outer.Add(row, 0, wx.LEFT | wx.RIGHT | wx.BOTTOM | wx.EXPAND, 12)

        notice_text = wx.StaticText(panel, label=notice)
        notice_text.SetName(notice)
        outer.Add(notice_text, 0, wx.LEFT | wx.RIGHT | wx.BOTTOM | wx.EXPAND, 12)
        safety = wx.StaticText(
            panel,
            label=(
                "Plik źródłowy nigdy nie jest zmieniany. "
                "Wynik zostanie zapisany jako nowy plik."
            ),
        )
        safety.SetName(safety.GetLabel())
        outer.Add(safety, 0, wx.LEFT | wx.RIGHT | wx.BOTTOM | wx.EXPAND, 12)

        buttons = self.CreateStdDialogButtonSizer(wx.OK | wx.CANCEL)
        ok = self.FindWindowById(wx.ID_OK)
        if ok is not None:
            ok.SetLabel("&Wybierz plik…")
            ok.SetName("Wybierz plik")
        shell = wx.BoxSizer(wx.VERTICAL)
        panel.SetSizer(outer)
        shell.Add(panel, 1, wx.EXPAND)
        shell.Add(buttons, 0, wx.ALL | wx.ALIGN_RIGHT, 12)
        self.SetSizerAndFit(shell)

    @property
    def selected_format(self) -> AudioClipFormatChoice | None:
        index = self.format_choice.GetSelection()
        return self._choices[index] if 0 <= index < len(self._choices) else None


class AudioOutputDeviceDialog(wx.Dialog):
    """Natywny wybor wyjscia; do wx trafiaja tylko jawne etykiety."""

    def __init__(
        self,
        parent: wx.Window,
        session_name: str,
        choices: tuple[AudioOutputChoice, ...],
        selected_device_id: str | None,
    ) -> None:
        super().__init__(parent, title=f"Urządzenie audio — {session_name}")
        self._choices = choices
        panel = wx.Panel(self)
        label = wx.StaticText(panel, label="&Urządzenie audio:")
        self.device_choice = wx.Choice(
            panel,
            choices=[choice.label for choice in choices],
        )
        self.device_choice.SetName("Urządzenie audio")
        selected = next(
            (
                index
                for index, choice in enumerate(choices)
                if choice.device_id == selected_device_id
            ),
            0,
        )
        if choices:
            self.device_choice.SetSelection(selected)

        row = wx.BoxSizer(wx.HORIZONTAL)
        row.Add(label, 0, wx.ALIGN_CENTER_VERTICAL | wx.RIGHT, 8)
        row.Add(self.device_choice, 1, wx.EXPAND)
        panel.SetSizer(row)
        buttons = self.CreateStdDialogButtonSizer(wx.OK | wx.CANCEL)
        outer = wx.BoxSizer(wx.VERTICAL)
        outer.Add(panel, 1, wx.ALL | wx.EXPAND, 12)
        outer.Add(buttons, 0, wx.ALL | wx.ALIGN_RIGHT, 12)
        self.SetSizerAndFit(outer)
        self.device_choice.SetFocus()

    @property
    def selected_choice(self) -> AudioOutputChoice | None:
        index = self.device_choice.GetSelection()
        return self._choices[index] if 0 <= index < len(self._choices) else None


class GeneralPlaybackOptionsDialog(wx.Dialog):
    """Najwazniejsze opcje interfejsu przeniesione z Ustawien pelnego AMC."""

    def __init__(self, parent: wx.Window, options) -> None:
        super().__init__(parent, title="Ustawienia — interfejs i odtwarzanie")
        panel = wx.Panel(self)
        layout = wx.BoxSizer(wx.VERTICAL)

        self.pause = wx.CheckBox(
            panel, label="&Wstrzymuj odtwarzanie po wyjściu z odtwarzacza"
        )
        self.pause.SetName("Wstrzymuj odtwarzanie po wyjściu z odtwarzacza")
        self.pause.SetValue(options.pause_on_player_exit)

        self.follow = wx.CheckBox(
            panel,
            label="Po wyjściu ustaw &fokus na aktualnie odtwarzanym elemencie",
        )
        self.follow.SetName(
            "Po wyjściu ustaw fokus na aktualnie odtwarzanym elemencie"
        )
        self.follow.SetValue(options.follow_playback_on_player_exit)

        self.open_preset = wx.CheckBox(
            panel, label="Po uruchomieniu presetu &otwieraj odtwarzacz"
        )
        self.open_preset.SetName("Po uruchomieniu presetu otwieraj odtwarzacz")
        self.open_preset.SetValue(options.open_player_when_activating_preset)

        self.radio_enter = wx.CheckBox(
            panel, label="Pozostawaj na li&ście po uruchomieniu stacji Enterem"
        )
        self.radio_enter.SetName(
            "Pozostawaj na liście po uruchomieniu stacji Enterem"
        )
        self.radio_enter.SetValue(options.stay_on_list_after_radio_enter)

        for control in (self.pause, self.follow, self.open_preset, self.radio_enter):
            layout.Add(control, 0, wx.BOTTOM | wx.EXPAND, 10)

        activity_heading = wx.StaticText(
            panel, label="Komunikaty stanu stacji radiowej"
        )
        activity_heading.SetName("Komunikaty stanu stacji radiowej")
        layout.Add(activity_heading, 0, wx.TOP | wx.BOTTOM | wx.EXPAND, 8)

        playback_label = wx.StaticText(
            panel, label="Stan &odtwarzania przy nazwie stacji:"
        )
        self.playback_position = wx.Choice(
            panel, choices=list(STATE_POSITION_LABELS)
        )
        self.playback_position.SetName("Stan odtwarzania przy nazwie stacji")
        self.playback_position.SetSelection(
            state_position_index(options.radio_playback_state_position)
        )
        layout.Add(playback_label, 0, wx.BOTTOM, 4)
        layout.Add(self.playback_position, 0, wx.BOTTOM | wx.EXPAND, 10)

        recording_label = wx.StaticText(
            panel, label="Stan &nagrywania przy nazwie stacji:"
        )
        self.recording_position = wx.Choice(
            panel, choices=list(STATE_POSITION_LABELS)
        )
        self.recording_position.SetName("Stan nagrywania przy nazwie stacji")
        self.recording_position.SetSelection(
            state_position_index(options.radio_recording_state_position)
        )
        layout.Add(recording_label, 0, wx.BOTTOM, 4)
        layout.Add(self.recording_position, 0, wx.BOTTOM | wx.EXPAND, 10)

        self.playback_sound = wx.CheckBox(
            panel, label="Krótki &dźwięk dla odtwarzanej stacji"
        )
        self.playback_sound.SetName("Krótki dźwięk dla odtwarzanej stacji")
        self.playback_sound.SetValue(options.radio_playback_state_sound)
        self.recording_sound = wx.CheckBox(
            panel, label="Krótki dźwięk dla nagrywanej stacji"
        )
        self.recording_sound.SetName("Krótki dźwięk dla nagrywanej stacji")
        self.recording_sound.SetValue(options.radio_recording_state_sound)
        layout.Add(self.playback_sound, 0, wx.BOTTOM | wx.EXPAND, 10)
        layout.Add(self.recording_sound, 0, wx.BOTTOM | wx.EXPAND, 10)
        panel.SetSizer(layout)

        buttons = self.CreateStdDialogButtonSizer(wx.OK | wx.CANCEL)
        outer = wx.BoxSizer(wx.VERTICAL)
        outer.Add(panel, 1, wx.ALL | wx.EXPAND, 12)
        outer.Add(buttons, 0, wx.ALL | wx.ALIGN_RIGHT, 12)
        self.SetSizer(outer)
        self.Fit()
        self.pause.SetFocus()

    @property
    def values(self) -> dict[str, object]:
        return {
            "pause_on_player_exit": self.pause.GetValue(),
            "follow_playback_on_player_exit": self.follow.GetValue(),
            "open_player_when_activating_preset": self.open_preset.GetValue(),
            "stay_on_list_after_radio_enter": self.radio_enter.GetValue(),
            "radio_playback_state_position": state_position_value(
                self.playback_position.GetSelection()
            ),
            "radio_recording_state_position": state_position_value(
                self.recording_position.GetSelection()
            ),
            "radio_playback_state_sound": self.playback_sound.GetValue(),
            "radio_recording_state_sound": self.recording_sound.GetValue(),
        }


class LiteFrame(wx.Frame):
    """Okno glowne: panel listy i panel odtwarzacza, przelaczane jak w AMC."""

    def __init__(self, store: StateStore, state: LiteState) -> None:
        super().__init__(None, title=APP_NAME, size=(900, 600))

        self.store = store
        self.state = state
        self.options = state.options
        # Stacje: w trybie wspolnego profilu zrodlem jest state.json AMC
        # (radio.stations), a nie prywatna kopia -- inaczej Radio bylo puste.
        # Uklad rozstrzygamy RAZ: ten sam obiekt decyduje tez o tym, czy host
        # dostanie zgode na zapis kolejki (patrz _queue_persistence_arguments).
        self.layout = resolve_layout()
        self._profile_audio_outputs = read_profile_outputs(self.layout.state_json)
        # Przelaczniki komunikatow i czas wlasny z PRAWDZIWEGO profilu AMC.
        # Tylko odczyt: wlascicielem state.json zostaje host C#.
        self.messages: MessagePolicy = load_message_policy(self.layout)
        self.radio = RadioSource(self.layout)
        self._radio_snapshot = self.radio.load()
        self.stations = self._radio_snapshot.list
        self.navigator = Navigator()
        self._transient_preview_return: TransientNavigationSnapshot | None = None
        # Biblioteka AMC (SQLite, tylko odczyt). Wlascicielem zapisu profilu
        # Wykonawca nagran i zegara pozostaje w hoscie C#. Interfejs wxPython
        # moze teraz edytowac prywatna kopie planow, ale sam nie uruchamia
        # drugiego, konkurencyjnego zegara.
        self.library = LibrarySource()
        self.podcasts = PodcastSource(self.layout)
        self._podcast_loaded_counts: dict[str, int] = {}
        # ``None`` w stanie prywatnym dziedziczy wybor glownego AMC. Do czasu
        # pierwszego odczytu widoku trzymamy bezpieczny domysl; wynik strony
        # niesie potem faktycznie uzyty tryb i aktualizuje zaznaczenie menu.
        self._podcast_inbox_sort_mode = (
            state.podcast_inbox_sort_mode or SORT_ADDED_NEWEST
        )
        self.navigator.restore(state.navigation)

        # Brama zna zycie okna, wiec spozniony wynik nie dotknie zniszczonego
        # okna. Nazwy strumieni ("folder", "status"...) uniewazniaja poprzednie
        # zadania tego samego rodzaju.
        self.gate = StaleResultGate(alive=self._window_alive)
        self.runner = BackgroundRunner(self.gate, to_gui=wx.CallAfter)

        self.client: LiteHostClient | None = None
        self._last_status: dict = {}
        self._last_recording_status: dict = {}
        self._status_poll_pending = False
        self._recording_status_poll_pending = False
        self._podcast_checkpoint_pending = False
        self._podcast_checkpoint_due = 0.0
        self._podcast_checkpoint_active = False
        self._podcast_refresh_pending = False
        self._podcast_download_pending = False
        self._podcast_favorite_pending = False
        self._podcast_add_pending = False
        self._podcast_opml_pending = False
        self._last_podcast_progress_error: str | None = None
        self._recording_history_persist_error = False
        self._audio_clip_export_in_progress = False
        self._audio_clip_export_percent = -1
        self._audio_clip_export_operation_id: str | None = None
        self._audio_clip_edit_in_progress = False
        self._audio_clip_edit_percent = -1
        self._audio_clip_edit_operation_id: str | None = None
        self._audio_clip_edit_kind: str | None = None
        #: Ktora sesja NAPRAWDE gra na hoscie. Host ma jedno wyjscie, wiec
        #: wyjscie z odtwarzacza PLIKOW nie moze wstrzymac grajacego radia --
        #: ``transport.pauseResume`` nie zna zakresu sesji.
        self._playing_session: SessionId | None = None
        self._pending_playback_session: SessionId | None = None
        self._radio_activity_cues: dict[tuple[bool, bool], object] = {}

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
        # POLE FILTRA. JEDNO, wspolne dla kazdej listy -- dokladnie jak
        # ``FilterBox`` w oryginale (MainWindow.xaml:521-524), ktory stoi nad
        # ``MediaList`` i obsluguje wszystkie widoki. Nie ma osobnej kontrolki
        # na sesje ani na widok: filtrowana jest ta lista, ktora jest WIDOCZNA.
        #
        # Etykieta ``wx.StaticText`` z ``&F``: na Windows klawisz dostepu z
        # etykiety przenosi fokus do NASTEPNEJ kontrolki w kolejnosci tabulacji,
        # czyli do pola. Nazwa dostepna pola ustawiona osobno, bo czytnik czyta
        # ``accName`` kontrolki, nie sasiedniego napisu.
        self.filter_label = wx.StaticText(self.list_panel, label="&Filtruj liste:")
        self.filter_box = wx.TextCtrl(self.list_panel)
        self.filter_box.SetName("Filtruj liste")
        #: Tekst filtra PER WIDOK (odpowiednik ``navigation.Filters``).
        self.filter_state = list_filter.FilterState()
        #: Kontekst widoku, dla ktorego pole JUZ pokazuje swoj tekst. Chroni od
        #: zapisania cudzego filtra pod kluczem widoku, do ktorego wlasnie
        #: wchodzimy: ``SetValue`` wysyla ``EVT_TEXT`` tak samo jak pisanie
        #: uzytkownika, wiec bez tej bramki przywracanie filtra samo by go
        #: nadpisywalo. ``_BRAK_KONTEKSTU``, bo ``None`` jest LEGALNYM
        #: kontekstem (widok Folderow ma ``library_view=None``).
        self._filter_context: object = _BRAK_KONTEKSTU
        #: Czy TERAZ przywracamy tekst pola z zapisanego stanu. Wtedy zmiana
        #: tekstu nie jest gestem uzytkownika i nie moze nic oglaszac.
        self._restoring_filter = False
        self.files_list = MediaListCtrl(
            self.list_panel, self.navigator.sessions[SessionId.FILES].model, "Pliki lokalne",
            self.navigator.sessions[SessionId.FILES],
        )
        self.radio_list = MediaListCtrl(
            self.list_panel, self.navigator.sessions[SessionId.RADIO].model, "Stacje radiowe",
            self.navigator.sessions[SessionId.RADIO],
        )
        self.podcasts_list = MediaListCtrl(
            self.list_panel,
            self.navigator.sessions[SessionId.PODCASTS].model,
            "Podcasty i YouTube",
            self.navigator.sessions[SessionId.PODCASTS],
        )
        self.radio_list.Hide()
        self.podcasts_list.Hide()
        list_sizer = wx.BoxSizer(wx.VERTICAL)
        list_sizer.Add(self.filter_label, 0, wx.BOTTOM, 3)
        list_sizer.Add(self.filter_box, 0, wx.EXPAND | wx.BOTTOM, 8)
        list_sizer.Add(self.files_list, 1, wx.EXPAND)
        list_sizer.Add(self.radio_list, 1, wx.EXPAND)
        list_sizer.Add(self.podcasts_list, 1, wx.EXPAND)
        self.list_panel.SetSizer(list_sizer)

        # --- widok odtwarzacza (zwykle kontrolki, nie wlasne rysowanie)
        self.player_panel = wx.Panel(self.panel)
        self.now_playing = wx.StaticText(self.player_panel, label="Nic nie jest odtwarzane")
        self.now_playing.SetName("Teraz odtwarzane")
        self.time_label = wx.StaticText(self.player_panel, label="0 s / nieznany")
        self.time_label.SetName("Czas")

        self.play_button = wx.Button(
            self.player_panel, label=transport_button_label(playing=False)
        )
        self.volume_slider = wx.Slider(
            self.player_panel, value=self.options.volume, minValue=0, maxValue=100,
            style=wx.SL_HORIZONTAL | wx.SL_LABELS,
        )
        self.volume_slider.SetName("Glosnosc")
        # Zakres suwaka = konce drabiny silnika: DemoMediaSession.cs:11
        # daje 0,50 .. 2,00. Suwak nie moze obiecywac wiecej niz silnik robi.
        self.rate_slider = wx.Slider(
            self.player_panel, value=int(round(self.options.rate * 100)),
            minValue=int(PLAYBACK_RATE_MIN * 100), maxValue=int(PLAYBACK_RATE_MAX * 100),
            style=wx.SL_HORIZONTAL | wx.SL_LABELS,
        )
        self.rate_slider.SetName("Predkosc odtwarzania w procentach")

        player_sizer = wx.BoxSizer(wx.VERTICAL)
        for control, flag in (
            (self.now_playing, 0),
            (self.time_label, 0),
            (self.play_button, 0),
            (wx.StaticText(self.player_panel, label="&Glosnosc:"), 0),
            (self.volume_slider, 0),
            (wx.StaticText(self.player_panel, label="&Predkosc:"), 0),
            (self.rate_slider, 0),
        ):
            player_sizer.Add(control, flag, wx.ALL | wx.EXPAND, 6)
        self.player_panel.SetSizer(player_sizer)
        self.player_panel.Hide()

        self.status_field = wx.StaticText(self.panel, label="Gotowe")
        self.status_field.SetName("Komunikaty")

        # NATYWNY pasek stanu ramki (``msctls_statusbar32``) -- to jego szuka
        # ``NVDA+End``. Bez niego NVDA nie znajdowal kontrolki paska stanu i
        # siekal TYTUL okna, czytajac jego ostatnie slowo ("wieczorne" z
        # "Uspokojenie wieczorne"); tak samo w widoku odtwarzacza po ``F6``.
        # Kolejna ``wx.StaticText`` tego nie naprawila, bo jej rola to
        # ``STATICTEXT``, a nie pasek stanu.
        #
        # Pasek ramki wx NIE jest w kolejnosci tabulacji i nie przyjmuje
        # fokusu, czyli spelnia warunek oryginalu z
        # ``AccessiblePlaybackStatusStrip.cs``: dostepny dla ``NVDA+End``,
        # nigdy nie bedacy celem klawiatury. Nie przejmujemy zadnego gestu
        # czytnika -- NVDA znajduje go sam.
        self.CreateStatusBar(1, style=wx.STB_DEFAULT_STYLE)
        self.status_bar = NativeStatusBar(self.GetStatusBar())
        self.status_bar.show("Gotowe")

        # Kanal mowy zostaje bez zmian; pasek stanu jest DODATKIEM obok niego,
        # a nie zamiast -- ``Announcer`` sam nic nie traci.
        self.announcer = Announcer(self.status_field, status_bar=self.status_bar)

        #: Sciezki WYCIETE przez Ctrl+X i jeszcze nie wklejone, po ``item_id``.
        #: Odpowiednik ``_pendingExternalMoves`` (``MainWindow.xaml.cs:25306``).
        #: Trzymamy to w pamieci okna, a NIE w profilu -- profil wspoldzielony
        #: otwieramy tylko do czytania i oczekujace przeniesienie nie jest
        #: stanem, ktory ma przetrwac zamkniecie programu.
        self.pending_external_moves: dict[str, str] = {}

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

        self._focus_active_list_initially()

    def _focus_active_list_initially(self) -> None:
        """Fokus startowy NA LISTE, nie w pole filtra.

        Pole filtra stoi w kolejnosci tabulacji przed lista, wiec bez tego wx
        daje fokus startowy edycji -- zmierzone na zywym przebiegu:
        ``focus_class='TextCtrl'``, ``filter_has_focus=True``. Niewidomy
        uzytkownik startowalby w pustym polu, bez strzalek po pozycjach.

        To jedyne miejsce, ktore ten fokus ustawia; ``_sync_views`` dalej rusza
        fokusem TYLKO przy zmianie widoku, wiec nie ma dwoch sciezek.
        """
        self._active_list().SetFocus()

    def _build_menu(self) -> None:
        """Pasek menu z OPISU w ``menu_model`` -- bez wlasnej logiki polecen.

        Kazda pozycja konczy sie wywolaniem ``_dispatch`` z ta sama ``Action``,
        ktorej uzywa skrot klawiszowy. Nie ma tu drugiej sciezki wykonania,
        wiec menu nie moze sie rozjechac ze skrotem.

        Skrot dopisujemy do etykiety po tabulatorze: wx pokazuje go po prawej,
        a czytnik ekranu czyta razem z nazwa. To TYLKO podpis -- klawisze
        obsluguje ``_on_key``, dlatego nie rejestrujemy tu akceleratorow
        (odebralyby strzalki i literowa nawigacje natywnej liscie).
        """
        bar = wx.MenuBar()
        #: Pozycje zalezne od kontekstu; stan ustawia ``_refresh_menu_state``.
        self._menu_items: list[tuple[wx.MenuItem, menu_model.MenuItem]] = []

        for described in menu_model.build_menus():
            native = wx.Menu()
            for entry in described.items:
                if entry.is_separator:
                    native.AppendSeparator()
                    continue
                if entry.builtin == "tempo-submenu":
                    native.AppendSubMenu(self._build_tempo_menu(), entry.label)
                    continue
                label = entry.label
                if entry.shortcut:
                    text = self._menu_shortcut_text(entry.shortcut)
                    if entry.accelerator:
                        label = f"{label}\t{text}"
                    else:
                        # BEZ tabulatora: wx zrobilby z tego akcelerator na
                        # poziomie okna, ktory polyka klawisz w polu filtra
                        # (zmierzone: Backspace nie kasowal znaku, tylko
                        # wychodzil o poziom wyzej). Skrot ZOSTAJE widoczny i
                        # czytany przez czytnik -- jako czesc nazwy pozycji.
                        label = f"{label} ({text})"
                identifier = wx.ID_EXIT if entry.builtin == "quit" else wx.ID_ANY
                if entry.checkable:
                    # ``AppendCheckItem``, nie ``Append``: tylko pozycja
                    # zaznaczalna niesie STAN, ktory czytnik ekranu powie przy
                    # samym przejsciu po menu. Stan poczatkowy stawia
                    # ``_refresh_menu_state``, zeby byla JEDNA droga.
                    item = native.AppendCheckItem(identifier, label)
                else:
                    item = native.Append(identifier, label)
                self._menu_items.append((item, entry))
                if entry.builtin == "quit":
                    self.Bind(wx.EVT_MENU, lambda _e: self.Close(), id=item.GetId())
                else:
                    action = entry.action
                    assert action is not None  # pilnuje tego test modelu menu
                    self.Bind(
                        wx.EVT_MENU,
                        lambda _e, chosen=action: self._dispatch(chosen),
                        id=item.GetId(),
                    )
            bar.Append(native, described.title)

        self.SetMenuBar(bar)
        self._refresh_menu_state()

    def _build_tempo_menu(self) -> wx.Menu:
        """Podmenu algorytmow -- zachowane bez zmian, ze stanem Check."""
        algorithms = wx.Menu()
        self.tempo_items = {}
        for value, label in TEMPO_LABELS.items():
            item = algorithms.AppendRadioItem(wx.ID_ANY, label)
            item.Check(value == self.options.tempo_algorithm)
            self.tempo_items[value] = item
            self.Bind(
                wx.EVT_MENU,
                lambda _event, choice=value: self._set_tempo_algorithm(choice),
                id=item.GetId(),
            )
        return algorithms

    @staticmethod
    def _menu_shortcut_text(chord: str) -> str:
        """Zapis skrotu zrozumialy dla uzytkownika i dla wx.

        Nasze tablice uzywaja nazw klawiszy wx (``Back``, ``Return``), ktore po
        polsku nic nie mowia. Tlumaczymy tylko PODPIS, nie dzialanie.
        """
        names = {"Back": "Backspace", "Return": "Enter", "Space": "Spacja"}
        head, _, key = chord.rpartition("+")
        return f"{head}+{names.get(key, key)}" if head else names.get(key, key)

    def _refresh_menu_state(self) -> None:
        """Wylaczaj pozycje, ktore teraz nie zadzialaja -- zamiast udawac.

        Uczciwie wylaczona pozycja jest dla czytnika ekranu informacja
        ("niedostepne"), a nie cisza po probie uzycia. Wspolny profil AMC
        czytamy tylko do odczytu, wiec zarzadzanie stacjami zyje wylacznie
        w sesji radiowej.
        """
        if not hasattr(self, "_menu_items"):
            return
        radio = self.navigator.active is SessionId.RADIO
        podcasts = self.navigator.active is SessionId.PODCASTS
        # "Cos gra" rozpoznajemy po TYM SAMYM statusie z hosta, z ktorego
        # korzysta ``_announce_time`` -- nie po wlasnym liczniku.
        playing = bool(self._last_status)
        session = self.navigator.sessions[self.navigator.active]
        podcast_inbox = (
            podcasts
            and session.view is View.LIST
            and session.library_view is LibraryView.PODCAST_INBOX
        )
        has_row = session.model.selected_row is not None
        selected_kind = (
            session.model.selected_row.kind
            if session.model.selected_row is not None
            else ""
        )
        podcast_sort_actions = {
            Action.SORT_PODCAST_INBOX_ADDED: SORT_ADDED_NEWEST,
            Action.SORT_PODCAST_INBOX_ALPHABETICAL: SORT_ALPHABETICAL,
            Action.SORT_PODCAST_INBOX_BY_PODCAST: SORT_CUSTOM,
        }
        for item, entry in self._menu_items:
            enabled = True
            if entry.needs_radio_session and not radio:
                enabled = False
            if entry.needs_podcast_session and not podcasts:
                enabled = False
            if entry.needs_podcast_inbox and not podcast_inbox:
                enabled = False
            if entry.needs_playback and not playing:
                enabled = False
            if entry.needs_selection and not has_row:
                enabled = False
            if entry.needs_podcast_item and (
                not podcasts or selected_kind not in ("podcast", "episode")
            ):
                enabled = False
            if entry.needs_podcast_episode and (
                not podcasts or selected_kind != "episode"
            ):
                enabled = False
            if item.IsEnabled() != enabled:
                item.Enable(enabled)
            if entry.checkable and entry.action in podcast_sort_actions:
                item.Check(
                    podcast_sort_actions[entry.action]
                    == self._podcast_inbox_sort_mode
                )
    def _bind_list(self, control: MediaListCtrl) -> None:
        """Wspolne powiazania klawiatury i wyboru dla list plikow i radia."""
        control.Bind(wx.EVT_KEY_DOWN, self._on_key)
        control.Bind(wx.EVT_LIST_ITEM_ACTIVATED, lambda _e: self._activate())
        control.Bind(wx.EVT_LIST_ITEM_FOCUSED, self._on_item_focused)
        control.Bind(wx.EVT_LIST_ITEM_SELECTED, self._on_item_selected)
        control.Bind(wx.EVT_LIST_ITEM_DESELECTED, self._on_item_deselected)

    def _bind_keys(self) -> None:
        self.Bind(wx.EVT_CHAR_HOOK, self._on_player_shortcut_hook)
        for control in (self.files_list, self.radio_list, self.podcasts_list):
            self._bind_list(control)
        for control in (self.player_panel, self.play_button, self.volume_slider, self.rate_slider):
            control.Bind(wx.EVT_KEY_DOWN, self._on_key)
        # POLE FILTRA ma WLASNA obsluge klawiszy, nie ``_on_key``: w edycji
        # litery naleza do pola, nie do skrotow. Odpowiednik bramki oryginalu
        # ``if (Keyboard.FocusedElement is TextBox)`` z
        # ``MainWindow.xaml.cs:21000`` -- tam rowniez tylko Enter/Down (i
        # osobno Escape) sa przechwytywane, a reszta idzie do pola.
        self.filter_box.Bind(wx.EVT_TEXT, self._on_filter_text)
        self.filter_box.Bind(wx.EVT_KEY_DOWN, self._on_filter_key)
        self.play_button.Bind(wx.EVT_BUTTON, lambda _e: self._play_pause())
        self.volume_slider.Bind(wx.EVT_SLIDER, self._on_volume_slider)
        self.rate_slider.Bind(wx.EVT_SLIDER, self._on_rate_slider)

    # -------------------------------------------------------------- filtr

    def _focus_filter(self) -> None:
        """Ctrl+K: fokus do pola filtra. Port ``FocusFilter`` (cs:22210-22220).

        ``SelectAll`` jest z oryginalu i ma konkretny sens dla uzytkownika
        czytnika: ponowne Ctrl+K zaznacza caly stary tekst, wiec pisanie go
        zastepuje, a nie dokleja sie do niego po omacku.

        Komunikat idzie przez ``announcer``, czyli te sama droga, ktora caly
        port ma zmierzona na zywym NVDA. Mowimy KROTKO (\"Filtr listy\") -- tak
        jak AMC z ``DetailedHints`` wylaczonymi; dluga podpowiedz oryginalu
        wymaga przelacznika ustawien, ktorego port jeszcze nie ma.
        """
        if self.navigator.view is View.PLAYER:
            # W odtwarzaczu nie ma listy do filtrowania. Mowimy o tym, zamiast
            # po cichu przenosic fokus do pola schowanego pod panelem.
            self.announcer.say("Filtr dziala na liscie, nie w odtwarzaczu")
            return
        self.filter_box.SetFocus()
        self.filter_box.SelectAll()
        self.announcer.say(list_filter.FILTER_ENTERED_MESSAGE)

    def _on_filter_text(self, event) -> None:
        """Zmiana tekstu filtra. Port ``FilterBox_TextChanged`` (cs:23650).

        Wybor celujemy po ``item_id``, tak jak oryginal przez
        ``preferredItemId`` -- zaznaczony utwor, ktory nadal przechodzi filtr,
        ZOSTAJE zaznaczony. Nic tu nie dotyka kolejki ani zrodla danych: filtr
        jest wlasciwoscia widoku, model zostaje pelny.
        """
        event.Skip()
        if self._restoring_filter:
            # Przywracanie tekstu przy wejsciu w widok nie jest gestem
            # uzytkownika: nie oglaszamy i nie nadpisujemy stanu.
            return
        context = self._current_view_context()
        self.filter_state.set_for_view(context, self.filter_box.GetValue())
        self._filter_context = context
        # Pisanie nie jest zadaniem odczytu liczby wynikow. Zachowaj zwykle
        # echo klawiszy NVDA; brak wynikow wyjasni proba wejscia na liste.
        self._apply_filter_to_list(announce_status=False)

    def _apply_filter_to_list(self, *, announce_status: bool = True) -> None:
        """Dociagnij WIDOCZNA liste do tekstu filtra. Jedna wspolna droga.

        Nie ma tu wlasnego wstawiania wierszy: ustawiamy ``filter_query`` i
        wolamy ``sync_rows``, czyli DOKLADNIE ten mechanizm, ktorym idzie kazda
        inna zmiana listy (diff, punktowe operacje, wybor po ID, zero operacji
        gdy nic sie nie zmienilo). Dlatego filtr nie ma wlasnej sciezki, ktora
        mogla by sie rozjechac z odebranym zachowaniem list.
        """
        query = self.filter_box.GetValue()
        active = self._active_list()
        active.filter_query = query
        active.sync_rows()
        # Bramki ``needs_selection`` zaleza od tego, CO sync_rows wybral --
        # ``MediaListCtrl.sync_rows`` ustawia wybor przez ``model.select_id``
        # (gui.py:605), czyli NIE przez ``EVT_LIST_ITEM_SELECTED``. Zmierzone
        # na zywym GUI (statusclip-4): po filtrze byl zaznaczony wiersz, a
        # "Skopiuj adres" zostawalo wylaczone, wiec jego akcelerator POLYKAL
        # Ctrl+Shift+C.
        self._refresh_menu_state()
        if announce_status:
            self.announcer.say(
                list_filter.results_status_text(query, active.visible_count())
            )

    def _on_filter_key(self, event: wx.KeyEvent) -> None:
        """Klawisze W POLU filtra. Tylko te, ktore oryginal przechwytuje.

        * Enter albo strzalka w dol -> wyniki (``FocusFilterResults``, cs:22200).
          Przy PUSTYM wyniku fokus ZOSTAJE w polu, a uzytkownik slyszy, co
          zrobic -- przeniesienie fokusu na liste bez wierszy zostawiloby go
          w miejscu, z ktorego nie slychac nic.
        * Escape -> czysci filtr i wraca na liste (``ReturnToMediaListFromEscape``,
          cs:22551-22561). Drugiego stopnia oryginalu (pusty filtr => poziom
          wyzej) tu NIE MA: w tym porcie wyjscie w gore nalezy do Backspace, a
          dorzucanie Escape do nawigacji byloby zmiana poza tym przyrostem.

        KAZDY inny klawisz (litery, w tym polskie, strzalki w poziomie, Home,
        Backspace) idzie do pola przez ``event.Skip()``. Nie mapujemy niczego
        na gesty czytnika ekranu.
        """
        code = event.GetKeyCode()
        if chord_from_event(event).canonical == "Ctrl+Alt+Return":
            self._dispatch(Action.SESSION_OPTIONS)
            return
        if code in (wx.WXK_RETURN, wx.WXK_NUMPAD_ENTER, wx.WXK_DOWN):
            self._focus_filter_results()
            return
        if code == wx.WXK_ESCAPE:
            self._clear_filter_and_return()
            return
        event.Skip()

    def _focus_filter_results(self) -> None:
        """Przejscie Z POLA na liste wynikow. Port ``FocusFilterResults``."""
        active = self._active_list()
        if active.visible_count() == 0:
            self.announcer.say(list_filter.NO_RESULTS_MESSAGE)
            return
        active.SetFocus()
        # Kursor dociagamy PO przejsciu fokusu: czytnik ma przeczytac wiersz,
        # na ktorym naprawde stoi lista, a nie poprzedni.
        active.sync_cursor()

    def _clear_filter_and_return(self) -> None:
        """Escape w polu: wyczysc filtr, wroc na liste. Port cs:22551-22561."""
        active = self._active_list()
        had_text = bool(self.filter_box.GetValue())
        if had_text:
            context = self._current_view_context()
            self.filter_state.clear_for_view(context)
            self._set_filter_text("")
            active.filter_query = ""
            active.sync_rows()
        active.SetFocus()
        active.sync_cursor()
        # Komunikat po przywroceniu listy, zeby opisywal stan KONCOWY.
        if had_text:
            self.announcer.say(list_filter.FILTER_CLEARED_MESSAGE)

    def _current_view_context(self) -> tuple:
        """Klucz filtra = TOZSAMOSC WIDOKU z ``navigation.view_context``.

        Ten sam klucz, po ktorym lista rozpoznaje zmiane widoku. Drugi,
        wlasny system identyfikacji rozjechalby filtr z lista po cichu.
        """
        return view_context(self.navigator.session)

    def _set_filter_text(self, text: str) -> None:
        """Wstaw tekst do pola BEZ traktowania tego jako gestu uzytkownika.

        ``SetValue`` wysyla ``EVT_TEXT`` tak samo jak pisanie, wiec bez tej
        bramki przywracanie zapisanego filtra nadpisywaloby sam siebie i
        oglaszalo status bez powodu uzytkownika.
        """
        self._restoring_filter = True
        try:
            self.filter_box.SetValue(text)
        finally:
            self._restoring_filter = False

    def _restore_filter_for_current_view(self) -> None:
        """Wejscie w widok przywraca JEGO filtr. Port ``RestoreFilterForCurrentView``.

        Wolane z ``_sync_views``, czyli z jedynej drogi odwzorowania stanu --
        bez wlasnego haka na kazda komende nawigacji.
        """
        context = self._current_view_context()
        if context == self._filter_context:
            # Ten sam widok: pole juz pokazuje swoj tekst. Nie ruszamy go, bo
            # ``SetValue`` w trakcie pisania przestawialby karetke.
            return
        self._filter_context = context
        wanted = self.filter_state.text_for_view(context)
        if self.filter_box.GetValue() != wanted:
            self._set_filter_text(wanted)
        for control in tuple(
            candidate for candidate in (
                getattr(self, "files_list", None),
                getattr(self, "radio_list", None),
                getattr(self, "podcasts_list", None),
            )
            if candidate is not None
        ):
            control.filter_query = wanted if control is self._active_list() else ""

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
        # JEDEN efektywny payload: ustawienia ogolne PO nalozeniu wyborow sesji.
        # Sam ``options.audio_payload()`` ucinalby zapisane Opcje sesji -- menu
        # Dzwiek zdmuchiwaloby je przy kazdej zmianie algorytmu.
        payload = session_options.engine_audio_payload(self.state)
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

    def _queue_persistence_arguments(self) -> dict[str, object]:
        """Czy TEN start okna ma byc pisarzem zapisanej kolejki.

        Decyduje UKLAD PROFILU, nie zyczenie wywolujacego:

        * ``PRIVATE_SANDBOX`` (wlasna pelna kopia) -- host dostaje sciezke i
          jawna zgode na zapis. To jedyny tryb, w ktorym kolejka przezywa
          zamkniecie okna.
        * ``READ_ONLY_MIRROR`` (wspolny profil uzytkownika) -- NIC nie
          wysylamy. Wlascicielem tych plikow jest pelne AMC (WPF), ktore nie
          zna naszej blokady; nasz zamek nie chronilby go przed niczym.

        Brak bazy w piaskownicy to zwykle przegladanie dysku: host zostaje w
        pamieci, a stara sciezka plikow dziala bez zmian.
        """
        layout = getattr(self, "layout", None) or resolve_layout()
        if not layout.may_write_profile:
            return {}
        if not layout.library_db.exists():
            return {}
        return {"profile_dir": str(layout.library_db.parent), "queue_write": True}

    def _start_engine(self) -> None:
        persistence = self._queue_persistence_arguments()
        # Co obiecalismy uzytkownikowi: bez tej zgody ``persistent=false`` jest
        # stanem NORMALNYM, a nie awaria warta ogloszenia.
        self._queue_write_requested = bool(persistence.get("queue_write"))
        self._last_persist_error: str | None = None
        layout = getattr(self, "layout", None)
        library_db = getattr(layout, "library_db", None)
        podcasts_db = getattr(layout, "podcasts_db", None)
        state_json = getattr(layout, "state_json", None)
        client = LiteHostClient(
            default_host_path(),
            timeshift_minutes=self.options.timeshift_minutes,
            # Odczyt list nadal nalezy do Pythona w trybie ro. Sciezka daje
            # hostowi C# wyłącznie możliwość wykonania wąskiej, transakcyjnej
            # operacji ``bookmark.add`` z własną bramką konfliktu WPF.
            library_db=(str(library_db) if library_db is not None and library_db.exists() else None),
            # Python nadal otwiera baze w mode=ro. Ta sciezka daje hostowi C#
            # wylacznie waska transakcje postepu jednego odcinka.
            podcasts_db=(
                str(podcasts_db)
                if podcasts_db is not None and podcasts_db.exists()
                else None
            ),
            state_json=(
                str(state_json)
                if state_json is not None and state_json.exists()
                else None
            ),
            on_event=self._on_engine_event,
            on_stderr=lambda line: None,
            **persistence,  # type: ignore[arg-type]
        )
        self.client = client
        # TEN SAM efektywny payload co w menu Dzwiek: nowy proces musi dostac
        # ustawienia ogolne PO nalozeniu zapisanych Opcji sesji. Sam
        # ``options.audio_payload()`` dawal hostowi ustawienie ogolne, wiec
        # zapis wracal z dysku BEZ TRWALEGO SKUTKU w silniku.
        settings = session_options.engine_audio_payload(self.state)
        profile_audio_outputs = getattr(self, "_profile_audio_outputs", None)
        if profile_audio_outputs is None:
            profile_audio_outputs = (
                read_profile_outputs(state_json)
                if state_json is not None
                else {}
            )
        private_audio_outputs = getattr(
            self.state,
            "audio_output_device_ids_by_session",
            {},
        )
        # Część testów uruchamia samą tę metodę, bez importów modułu GUI.
        # Produkcja zawsze używa wspólnego, walidującego resolvera; poniższa
        # mała rezerwa zachowuje zgodność takiej izolowanej próby metody.
        output_resolver = globals().get("effective_output_device_id")
        if output_resolver is None:
            def output_resolver(session, private, profile):
                key = session.value
                if isinstance(private, dict) and key in private:
                    return private[key] or None
                return profile.get(key) or None if isinstance(profile, dict) else None
        output_devices = {
            session.value: output_resolver(
                session,
                private_audio_outputs,
                profile_audio_outputs,
            )
            for session in (SessionId.FILES, SessionId.RADIO, SessionId.PODCASTS)
        }
        # ``_start_engine`` bywa tez wywolywane przez lekkie tryby testowe i
        # awaryjne, ktore nie otwieraja profilu radia. Brak migawki oznacza
        # wtedy po prostu brak planow do zsynchronizowania, a nie blad startu
        # calego odtwarzacza.
        radio_snapshot = getattr(self, "_radio_snapshot", None)
        schedule_payload = (
            LiteFrame._radio_schedule_sync_payload(radio_snapshot, self.state)
            if radio_snapshot is not None
            else None
        )

        def work() -> dict:
            try:
                client.start()
                client.hello()
                result = client.configure_audio(**settings)
                for session_id, device_id in output_devices.items():
                    client.call(
                        "audio.selectOutput",
                        {
                            "sessionId": session_id,
                            "deviceId": device_id,
                            "restart": False,
                        },
                    )
                if schedule_payload is not None:
                    client.sync_radio_schedules(schedule_payload)
                return result
            except Exception:
                client.close()
                raise

        def done(_result: dict) -> None:
            self.timer.Start(1000)
            # Kolejnosc listy liczy host C# (CompareInfo pl-PL), nie wlasny
            # collator w Pythonie. Podpinamy ja DOPIERO tu, bo wymaga zywego
            # silnika. Bez niej LibrarySource swiadomie oddaje kolejnosc z
            # SQL-a zamiast udawac zgodnosc.
            collation = HostCollation(client.call)
            self.library.use_collation(collation)
            # Radio nie ma wlasnego ``use_collation`` -- jego widoki sa
            # bezstanowe, wiec kolacje trzymamy TUTAJ i podajemy na wywolanie.
            # Jeden obiekt dla obu sesji: to ten sam host i ten sam cache.
            self._collation = collation
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
        if name == "audio.outputFallback":
            self.announcer.say(
                "Wybrane urządzenie audio jest niedostępne. "
                "Radio gra na urządzeniu domyślnym"
            )
        elif name == "audio.clipExportProgress":
            # Postep ma byc dostepny przez pole i NVDA+End, ale NIE moze
            # przerywac mowy co 5%. Zdarzenie koncowe jest oglaszane raz przez
            # odpowiedz operacji w ``_start_audio_clip_export``.
            operation_id = str(data.get("operationId") or "")
            if (
                self._audio_clip_export_in_progress
                and operation_id == (self._audio_clip_export_operation_id or "")
            ):
                percent = max(0, min(100, int(data.get("percent") or 0)))
                if percent != self._audio_clip_export_percent:
                    self._audio_clip_export_percent = percent
                    file_name = str(data.get("name") or "").strip()
                    text = f"Zapisywanie fragmentu: {percent}%"
                    if file_name:
                        text += f". {file_name}"
                    self.status_field.SetLabel(text)
                    self.status_bar.show(text)
        elif name == "podcast.downloadProgress":
            if self._podcast_download_pending:
                current = max(1, int(data.get("current") or 1))
                total = max(current, int(data.get("total") or current))
                title = str(data.get("title") or "odcinek").strip() or "odcinek"
                percent = int(data.get("percent") or 0)
                text = (
                    f"Pobieranie {current} z {total}: {title}"
                    if percent < 0
                    else f"Pobieranie {current} z {total}: {percent}%. {title}"
                )
                # Postęp jest dostępny przez pasek stanu i NVDA+End, lecz nie
                # przerywa mowy co kilka procent.
                self.status_field.SetLabel(text)
                self.status_bar.show(text)
        elif name == "audio.clipRemoveStarted":
            operation_id = str(data.get("operationId") or "")
            if (
                self._audio_clip_edit_in_progress
                and operation_id == (self._audio_clip_edit_operation_id or "")
            ):
                self.announcer.say("Usuwanie fragmentu. Odtwarzanie zatrzymano")
        elif name == "audio.clipRemoveProgress":
            # Jak przy eksporcie: postep jest dostepny w pasku, ale nie
            # przerywa NVDA co kilka procent. Id operacji odrzuca spoznione
            # zdarzenie poprzedniej edycji.
            operation_id = str(data.get("operationId") or "")
            if (
                self._audio_clip_edit_in_progress
                and operation_id == (self._audio_clip_edit_operation_id or "")
            ):
                percent = max(0, min(100, int(data.get("percent") or 0)))
                if percent != self._audio_clip_edit_percent:
                    self._audio_clip_edit_percent = percent
                    file_name = str(data.get("name") or "").strip()
                    text = f"Usuwanie fragmentu: {percent}%"
                    if file_name:
                        text += f". {file_name}"
                    self.status_field.SetLabel(text)
                    self.status_bar.show(text)
        elif name == "audio.clipAppendStarted":
            operation_id = str(data.get("operationId") or "")
            if (
                self._audio_clip_edit_in_progress
                and self._audio_clip_edit_kind == "append"
                and operation_id == (self._audio_clip_edit_operation_id or "")
            ):
                file_name = str(data.get("name") or "").strip()
                text = "Dopisywanie fragmentu rozpoczęte"
                if file_name:
                    text += f": {file_name}"
                self.announcer.say(text)
        elif name == "audio.clipAppendProgress":
            operation_id = str(data.get("operationId") or "")
            if (
                self._audio_clip_edit_in_progress
                and self._audio_clip_edit_kind == "append"
                and operation_id == (self._audio_clip_edit_operation_id or "")
            ):
                percent = max(0, min(100, int(data.get("percent") or 0)))
                if percent != self._audio_clip_edit_percent:
                    self._audio_clip_edit_percent = percent
                    file_name = str(data.get("name") or "").strip()
                    text = f"Dopisywanie fragmentu: {percent}%"
                    if file_name:
                        text += f". {file_name}"
                    self.status_field.SetLabel(text)
                    self.status_bar.show(text)
        elif name == "playback.started":
            # POTWIERDZENIE startu. Dopiero teraz material jest "biezacy" dla
            # Ctrl+B: samo wyslanie ``files.play`` jeszcze niczego nie dowodzi.
            # Pole nazywa sie ``id`` (LiteEngineHandlers.cs:54), nie
            # ``itemId``, i czesto niesie ``file:<path>``, bo tak host sklada
            # Id przy przegladaniu folderow -- a to NIE jest profilowe Id.
            # Dlatego tozsamosc bierzemy z kandydata zapisanego przy zlecaniu,
            # a z hosta tylko FAKT, ze start sie udal.
            if data.get("engine") == "files":
                target = self._pending_playback_session
                if target not in (SessionId.FILES, SessionId.PODCASTS):
                    target = SessionId.FILES
                self.navigator.note_playback_started(target)
                self._playing_session = target
                self._podcast_checkpoint_active = target is SessionId.PODCASTS
                if self._podcast_checkpoint_active:
                    self._podcast_checkpoint_due = 0.0
                self._pending_playback_session = None
            if data.get("tempoFallbackReason"):
                self.announcer.say(
                    "Wybrany algorytm tempa jest niedostępny. Używany SoundTouch."
                )
        elif name == "playback.ended":
            # Przy ZYWEJ kolejce koniec utworu nie jest koncem sluchania: host
            # zaraz przysle ``queue.advanced``. Mowienie "Koniec utworu" przed
            # nazwa nastepnego byloby tylko halasem, dlatego milczymy i czekamy.
            if not data.get("queueContinues"):
                self.announcer.say("Koniec utworu")
            if getattr(self, "_playing_session", None) is SessionId.PODCASTS:
                self._podcast_checkpoint_active = False
        elif name == "queue.advanced":
            # NATURALNE przejscie policzone przez sesje Core po stronie hosta.
            # GUI tylko odwzorowuje to, co host NAPRAWDE zaczal grac.
            self._run(self.navigator.note_queue_advanced(
                str(data.get("id") or ""), str(data.get("title") or "")
            ))
        elif name == "playback.failed":
            failed_session = self._pending_playback_session or self._playing_session
            self._pending_playback_session = None
            if failed_session is SessionId.PODCASTS:
                self._podcast_checkpoint_active = False
            self._run(self.navigator.note_playback_failed(
                f"Nie udalo sie odtworzyc: {data.get('message', 'blad')}",
                failed_session,
            ))
        elif name == "podcast.progressSaveFailed":
            self._note_podcast_progress_error(
                str(data.get("message") or "Nie udało się zapisać postępu odcinka")
            )
        elif name == "radio.nowPlaying":
            title = str(data.get("streamTitle") or "").strip()
            if title:
                self.now_playing.SetLabel(title)
                self.announcer.say(title)
        elif name == "radio.recordingScheduled":
            # To zdarzenie przychodzi DOKLADNIE wtedy, gdy termin staje sie
            # wymagalny i host zaklada aktywne nagranie. Bez tej galezi plan
            # wygladal na martwy az do otwarcia dekodera albo bledu.
            station = str(data.get("stationName") or "stacja")
            self.announcer.say(
                f"Rozpoczynam zaplanowane nagrywanie: {station}"
            )
        elif name == "radio.recordingStarted":
            # Reczne Ctrl+R potwierdza odpowiedz ``recordingToggle``, a plan
            # potwierdza ``recordingScheduled``. Zdarzenie otwarcia pliku ma
            # tylko odswiezyc stan listy; drugie zdanie z nazwa pliku
            # zagluszaloby nawigacje NVDA.
            pass
        elif name == "radio.recordingFinished":
            station = str(data.get("stationName") or "stacja")
            scheduled = bool(str(data.get("scheduleName") or "").strip())
            count = int(data.get("savedFileCount") or 0)
            path = str(data.get("path") or "").strip()
            if count > 1:
                self.announcer.say(
                    f"Zakończono {'zaplanowane ' if scheduled else ''}nagrywanie {station}. "
                    f"Zapisano plików: {count}"
                )
            elif path:
                self.announcer.say(
                    f"Zakończono {'zaplanowane ' if scheduled else ''}nagrywanie "
                    f"{station}: {Path(path).name}"
                )
            else:
                self.announcer.say(
                    f"Zakończono {'zaplanowane ' if scheduled else ''}nagrywanie: {station}"
                )
        elif name == "radio.recordingStopped":
            station = str(data.get("stationName") or "stacja")
            scheduled = bool(str(data.get("scheduleName") or "").strip())
            count = int(data.get("savedFileCount") or 0)
            path = str(data.get("path") or "").strip()
            prefix = "Zatrzymano zaplanowane nagrywanie" if scheduled else "Zatrzymano nagrywanie"
            if count > 1:
                self.announcer.say(f"{prefix} {station}. Zapisano plików: {count}")
            elif path:
                self.announcer.say(f"{prefix} {station}: {Path(path).name}")
            else:
                self.announcer.say(f"{prefix}: {station}")
        elif name == "radio.recordingFailed":
            station = str(data.get("stationName") or "stacja")
            reason = str(data.get("error") or "nie utworzono pliku")
            scheduled = bool(str(data.get("scheduleName") or "").strip())
            subject = "wykonać zaplanowanego nagrania" if scheduled else "nagrać"
            self.announcer.say(f"Nie udało się {subject} {station}: {reason}")
        terminal_recording_event = name in (
            "radio.recordingFinished",
            "radio.recordingStopped",
            "radio.recordingFailed",
        )
        if terminal_recording_event:
            self._remember_recording_result(data)
        if name.startswith("radio.recording"):
            # Zdarzenie opisuje pojedyncza zmiane, natomiast lista potrzebuje
            # pelnej migawki wszystkich nagran. Odswiezamy ja bez dodatkowego
            # komunikatu; samo zdarzenie powyzej jest jedyna mowa.
            self._refresh_recording_status()
            radio_state = self.navigator.sessions[SessionId.RADIO]
            if radio_state.library_view is LibraryView.ACTIVE_RADIO_RECORDINGS:
                self._show_active_radio_recordings(announce=False)
        if terminal_recording_event:
            files_state = self.navigator.sessions[SessionId.FILES]
            if files_state.library_view is LibraryView.RECORDED_RADIO_FILES:
                self._show_radio_recording_history(announce=False)
        if name == "queue.advanced" or (
            name in ("playback.started", "playback.ended")
            and data.get("engine") == "files"
        ):
            self._refresh_live_queue()
        if name == "queue.advanced" or (
            name == "playback.ended" and data.get("engine") == "files"
        ):
            # Także ostatni utwór zapisuje pustkę, ale nie emituje queue.advanced.
            # Kolejka WLASNIE sie zmienila, czyli host wlasnie probowal ja
            # zapisac. Pytamy tu, a nie w _refresh_live_queue, bo tamta droga
            # istnieje tylko w otwartym Ctrl+Q.
            self._check_queue_persistence()

    # --------------------------------------------- slyszalna trwalosc kolejki

    def _note_queue_persistence(self, payload: dict) -> None:
        """Powiedz PRAWDE o zapisie kolejki -- raz, a nie co zdarzenie.

        Zasady, w tej kolejnosci:

        * Nie prosilismy o zapis (zwykly, tylko-do-odczytu start) -->
          ``persistent=false`` jest stanem normalnym. Milczymy; obiecywanie
          albo oplakiwanie trwalosci, ktorej nie zamawialismy, to halas.
        * Zapis sie udal --> tez milczymy. Powodzenie nie jest komunikatem.
        * Swiadomy pisarz dostal odmowe --> mowimy, co sie stalo i dlaczego,
          krotko. Powtorzenia TEJ SAMEJ przyczyny tlumimy: host przysyla ja
          przy kazdym przejsciu kolejki, a czytnik ekranu nie jest logiem.
        """
        if not getattr(self, "_queue_write_requested", False):
            return
        error = str((payload or {}).get("persistError") or "").strip()
        if not error:
            # Zapis wrocil do zdrowia: nastepna awaria znow jest nowiną.
            self._last_persist_error = None
            return
        if error == getattr(self, "_last_persist_error", None):
            return
        self._last_persist_error = error
        self.announcer.say(f"Nie zapisałem kolejki: {error}")

    def _check_queue_persistence(self) -> None:
        """Zapytaj o stan zapisu po NATURALNYM przejsciu kolejki.

        Osobno od ``_refresh_live_queue``, bo ta milczy poza widokiem Ctrl+Q --
        a blad zapisu trzeba uslyszec takze patrzac na odtwarzacz. Pytanie
        idzie przez ``runner`` (czyli POZA brama hosta i bez trzymania zamkow
        koordynatora), zadnego nowego timera ani drugiego silnika.
        """
        client = self.client
        if client is None or not getattr(self, "_queue_write_requested", False):
            return

        def done(payload: dict) -> None:
            if self._window_alive():
                self._note_queue_persistence(payload or {})

        def failed(error: Exception) -> None:
            # Samo pytanie padlo; to NIE jest dowod odmowy zapisu, wiec nie
            # zmyslamy przyczyny.
            return None

        self.runner.submit("queue-persist", client.queue_status, done, failed)

    # ------------------------------------------------------- tresc poczatkowa

    def _load_initial_content(self) -> None:
        # Zaznaczenie idzie za radio.currentItemId profilu AMC, a nie wraca
        # odruchowo na wiersz 0 (w zmierzonym profilu biezaca stacja ma indeks 80).
        self._run(self.navigator.apply_stations(
            rows_from_stations(self.stations.as_payload()),
            preferred_id=self._radio_snapshot.current_id,
        ))
        # Porzadek profilu obowiazuje juz po starcie, nie dopiero po Ctrl+L.
        # Wstepne dane zapewniaja liste takze przy odmowie drugiego odczytu;
        # finalny odczyt/kolacja sa poza watkiem GUI. Brak preferred_id chroni
        # wybor wykonany przez uzytkownika w czasie oczekiwania.
        self._open_radio_view(OpenLibraryView(
            view=None, target_session_id=SessionId.RADIO,
        ))
        # Blad ODCZYTU stacji nie jest tym samym co profil bez stacji. Dopoki
        # RadioSource oddawalo pusta liste w obu przypadkach, uzytkownik slyszal
        # cisze takze wtedy, gdy profil byl uszkodzony albo zajety.
        if self._radio_snapshot.load_error:
            self.announcer.say(self._radio_snapshot.load_error)
        if getattr(self.navigator, "active", None) is SessionId.PODCASTS:
            self._open_podcast_library(
                preferred_id=self.navigator.sessions[SessionId.PODCASTS].list_anchor_id
            )
        # Biblioteka AMC ma PIERWSZENSTWO nad przegladaniem dysku. Dawniej
        # bylo odwrotnie: pytalismy ``Path(folder).exists()``, a skoro sciezki
        # profilu (D:\, C:\Users\micha) na tej maszynie nie istnieja, lista pod
        # Ctrl+1 byla pusta -- to jest zglaszany blad.
        if self.library.is_available:
            # ``saved_folder`` otwiera SQLite, a jestesmy w ``done()`` zadania
            # startowego, czyli pod ``wx.CallAfter``. Nieobsluzony wyjatek nie
            # wyszedlby poza log i okno zostaloby puste ORAZ ciche. Zamiast tego
            # mowimy co sie stalo i wczytujemy Biblioteke od korzenia -- blad
            # ZAPAMIETANEGO folderu nie moze odciac calej Biblioteki.
            try:
                folder = self.library.saved_folder()
            except Exception as error:
                self.announcer.say(
                    f"Nie mogę odczytać zapamiętanego folderu: {error}. "
                    "Pokazuję Bibliotekę od początku."
                )
                folder = None
            self._open_library(folder)
            return
        folder = self.navigator.sessions[SessionId.FILES].folder_path or self.options.last_folder
        if folder and Path(folder).exists():
            self._open_folder(folder)
        else:
            self.announcer.say(self.library.describe() or "Wybierz folder: Ctrl+O")

    def _return_to_library(self) -> None:
        """Ctrl+L: powrot do ZAPAMIETANEGO widoku Biblioteki (cs:701-705).

        Nazwe widoku czytamy z profilu teraz, w watku GUI -- to jeden wiersz
        ``local_state``, nie lista. Blad odczytu nie moze odciac Biblioteki
        (jak przy ``saved_folder``, gui.py:1661-1668), wiec wtedy wracamy do
        Folderow i mowimy, co sie stalo.
        """
        if self.navigator.active is SessionId.RADIO:
            self._run(self.navigator.return_to_library(LIBRARY_VIEW_FOLDERS))
            return
        if self.navigator.active is SessionId.PODCASTS:
            self._open_podcast_library(
                preferred_id=self.navigator.sessions[SessionId.PODCASTS].library_return_id
            )
            return
        if not self.library.is_available:
            self.announcer.say(self.library.describe() or "Biblioteka niedostepna")
            return
        try:
            saved = self.library.saved_library_view()
        except Exception as error:
            self.announcer.say(
                f"Nie mogę odczytać zapamiętanego widoku Biblioteki: {error}. "
                "Pokazuję Foldery."
            )
            saved = LIBRARY_VIEW_FOLDERS
        self._run(self.navigator.return_to_library(saved))

    def _open_library(self, folder: str | None, preferred_id: str | None = None) -> None:
        """Wczytanie poziomu Biblioteki z SQLite -- POZA watkiem GUI.

        Baza ma 11 tysiecy rekordow, wiec odczyt nie moze blokowac okna, nawet
        jesli jest szybki.

        BEZ zapowiedzi \"Wczytywanie...\": oryginal przy wejsciu w widok nic
        takiego nie mowi, a dla uzytkownika czytnika to szum przed KAZDA lista.
        Komunikaty o PUSTEJ bibliotece, degradacji odczytu i bledzie zostaja --
        usuwamy rutyne, nie diagnostyke.
        """

        def work() -> LibrarySnapshot:
            return self.library.load(folder)

        def done(snapshot: LibrarySnapshot) -> None:
            if snapshot.is_empty:
                self.announcer.say("Biblioteka jest pusta")
                return
            # Degradacja odczytu (stara migawka, zastepcza kolejnosc) NIE MOZE
            # przejsc w ciszy: do tej pory oba pola snapshotu byly martwe.
            notice = degradation_notice(snapshot)
            if notice:
                self.announcer.say(notice)
            self._run(self.navigator.apply_folder(
                snapshot.folder_path or "", snapshot.rows, preferred_id=preferred_id))

        def failed(error: Exception) -> None:
            self.announcer.say(f"Nie moge wczytac Biblioteki: {error}")

        self.runner.submit("folder", work, done, failed)

    _VIEW_KEYS = {
        LibraryView.ALL_FILES: "all_files",
        LibraryView.FAVORITES: "favorites",
        LibraryView.PLAYLISTS: "playlists",
        LibraryView.PLAYLIST_CONTENTS: "playlist_contents",
        LibraryView.HISTORY: "history",
        LibraryView.SAVED_QUEUE: "saved_queue",
        LibraryView.ITEM_BOOKMARKS: "item_bookmarks",
        LibraryView.ALL_BOOKMARKS: "all_bookmarks",
    }

    def _open_radio_view(self, intent: OpenLibraryView) -> None:
        """Odczyt Radia ma osobny bilet zadania, bez lokalnego katalogu plikow."""
        if intent.view not in (None, LibraryView.FAVORITES, LibraryView.HISTORY):
            self.announcer.say("Ten widok nie jest jeszcze dostępny w Radiu")
            return
        source = self.radio
        scope = intent.view.value if intent.view else "library"
        # Kolacje czytamy TERAZ, w watku GUI, i wysylamy jako wartosc --
        # zagladanie do pola okna z watku roboczego scigaloby sie ze startem
        # silnika. ``None`` znaczy ,,hosta jeszcze nie ma'' i widok uczciwie
        # nazwie kolejnosc zastepcza.
        collation = getattr(self, "_collation", None)

        def work():
            from .radio_views import load_view
            return load_view(source, scope, collation=collation)

        def done(result) -> None:
            if result.unavailable_reason:
                if self.navigator.active is SessionId.RADIO:
                    self.announcer.say(result.unavailable_reason)
                return
            events = self.navigator.apply_radio_view(
                intent.view, result.heading, result.rows,
                preferred_id=intent.preferred_id,
                order_matches_amc=result.order_matches_amc)
            # Odczyt zaczal sie w Radiu, ale uzytkownik mogl juz przejsc do Plikow.
            # Zachowujemy stan wlasciwej sesji, nie przestawiamy obcego fokusu.
            if self.navigator.active is SessionId.RADIO:
                self._run(events)
                if result.missing_item_count:
                    self.announcer.say(
                        f"Pozycje historii bez stacji w profilu: {result.missing_item_count}")

        def failed(error: Exception) -> None:
            if self.navigator.active is SessionId.RADIO:
                self.announcer.say(f"Nie mogę wczytać listy radia: {error}")

        self.runner.submit("radio-view", work, done, failed)

    def _open_library_view(self, intent: OpenLibraryView) -> None:
        """Wczytanie nazwanego widoku Biblioteki -- tak samo POZA watkiem GUI.

        "Wszystkie pliki" to kilka tysiecy wierszy plus klucze kolacji z hosta,
        wiec odczyt w watku GUI zamrozilby okno w trakcie czytania listy.
        """
        if intent.target_session_id is SessionId.RADIO:
            self._open_radio_view(intent)
            return
        if not self.library.is_available:
            # Niedostepne D: w kopii profilu NIE znaczy pustej Biblioteki --
            # to brak samej bazy, i tak to nazywamy.
            self.announcer.say(self.library.describe() or "Biblioteka niedostepna")
            return

        view = intent.view
        key = self._VIEW_KEYS[view]
        playlist_id = intent.playlist_id
        item_id = intent.item_id
        # Kontekst biezacego materialu CZYTAMY TERAZ, w watku GUI, i wysylamy
        # do watku roboczego jako wartosci. Zajrzenie do nawigatora z tamtej
        # strony scigaloby sie ze zmiana sesji w trakcie odczytu.
        current_session_id = intent.current_session_id
        current_item_id = intent.current_item_id

        def work():
            return self.library.load_view(
                key,
                playlist_id=playlist_id,
                item_id=item_id,
                current_session_id=current_session_id,
                current_item_id=current_item_id,
            )

        def done(result) -> None:
            if result.fallback_view is not None:
                self._run(self.navigator.apply_library_view(
                    view, result.heading, [], fallback_to_playlists=True))
                return
            # ``sees_live_writes`` dotyczy calego odczytu, nie jednego widoku,
            # wiec mowimy o nim ta sama droga co w Folderach.
            if not result.sees_live_writes:
                self.announcer.say("Uwaga: zamrozona migawka profilu.")
            self._run(self.navigator.apply_library_view(
                view,
                result.heading,
                result.rows,
                preferred_id=intent.preferred_id,
                playlist_id=playlist_id,
                order_matches_amc=result.order_matches_amc,
                item_id=item_id,
                bookmark_targets=result.bookmark_targets,
                bookmark_contexts=result.bookmark_contexts,
                queue_flags=result.queue_flags,
            ))

        def failed(error: Exception) -> None:
            self.announcer.say(f"Nie moge wczytac widoku: {error}")

        self.runner.submit("folder", work, done, failed)

    # ------------------------------------------------------------- klawisze

    def _on_player_shortcut_hook(self, event: wx.KeyEvent) -> None:
        # POLE FILTRA JEST SZCZELNE. ``EVT_CHAR_HOOK`` widzi klawisze przed
        # kontrolka, wiec bez tej bramki Enter i Backspace z pola wpadaly do
        # globalnej nawigacji: zmierzone na zywym GUI (Enter otwieral folder,
        # Backspace wynosil o poziom wyzej) zamiast dojsc do
        # ``_on_filter_key``. Odpowiednik ``if (Keyboard.FocusedElement is
        # TextBox)`` z oryginalu (MainWindow.xaml.cs:21000): gdy fokus jest w
        # polu, klawisze naleza WYLACZNIE do pola i jego wlasnej obslugi.
        if wx.Window.FindFocus() is self.filter_box:
            self._on_filter_key(event)
            return

        focus = wx.Window.FindFocus()
        if focus is None or wx.GetTopLevelParent(focus) is not self:
            event.Skip()
            return

        # Windowsowa kontrolka listy potrafi przejac F2 (wlasna edycja
        # etykiety) oraz Delete, zanim dotra do EVT_KEY_DOWN kontrolki. Te
        # cztery gesty sa poleceniami AMC, wiec przechwytujemy je juz na
        # poziomie okna. Reszte klawiszy listy nadal dostaje natywna kontrolka
        # -- szczegolnie strzalki, Home/End i wyszukiwanie przyrostowe NVDA.
        if self.navigator.view is not View.PLAYER:
            if chord_from_event(event).canonical in {
                "F2",
                "Shift+F2",
                "Delete",
                "Shift+Delete",
            }:
                self._on_key(event)
                return
            event.Skip()
            return

        # Native dialog processing on a button consumes player keys before
        # KEY_DOWN. The existing resolver passes unknown keys (e.g. Tab) on.
        self._on_key(event)

    def _on_key(self, event: wx.KeyEvent) -> None:
        chord = chord_from_event(event)
        player = self.navigator.view is View.PLAYER
        radio = self.navigator.active is SessionId.RADIO
        podcast_inbox = (
            not player
            and self.navigator.active is SessionId.PODCASTS
            and self.navigator.session.library_view is LibraryView.PODCAST_INBOX
        )
        # WPF czyści aktywny filtr także wtedy, gdy fokus jest już na wynikach.
        if not player and chord.canonical == "Escape" and self.filter_box.GetValue():
            self._clear_filter_and_return()
            return
        action = resolve(
            chord,
            player_view=player,
            radio_session=radio,
            podcast_inbox=podcast_inbox,
            podcast_session=self.navigator.active is SessionId.PODCASTS,
        )
        if action is None:
            # Klawisz NIE jest nasz: oddajemy go kontrolce, zeby natywna
            # nawigacja i czytnik ekranu dzialaly bez zmian.
            event.Skip()
            return
        self._dispatch(action)

    def _dispatch(self, action: Action) -> None:
        if (
            self.navigator.active is SessionId.PODCASTS
            and action in {
                Action.VIEW_ALL_FILES,
                Action.VIEW_PLAYLISTS,
                Action.VIEW_FOLDERS,
                Action.VIEW_ITEM_BOOKMARKS,
                Action.VIEW_ALL_BOOKMARKS,
                Action.OPEN_FOLDER_DIALOG,
                Action.OPEN_FILE_DIALOG,
            }
        ):
            self.announcer.say(
                "To polecenie dotyczy Biblioteki plików. "
                "Do podcastów wrócisz skrótem Ctrl+L"
            )
            return
        if action is Action.SESSION_FILES:
            self._transient_preview_return = None
            self._switch_session(SessionId.FILES)
        elif action is Action.SESSION_RADIO:
            self._transient_preview_return = None
            self._switch_session(SessionId.RADIO)
        elif action is Action.SESSION_PODCASTS:
            self._transient_preview_return = None
            self._switch_session(SessionId.PODCASTS)
        elif action is Action.ACTIVATE:
            self._activate()
        elif action is Action.PARENT_FOLDER:
            # Po Escape/Backspace z podgladu nie dopowiadamy technicznego
            # "Powrot, Radio internetowe, lista". Natywna lista odzyskuje
            # poprzedni wiersz i to wlasnie jego nazwe ma od razu przeczytac
            # NVDA.
            if not LiteFrame._restore_transient_preview(self, announce=False):
                self._run(self.navigator.go_to_parent())
        elif action is Action.SHOW_PLAYER:
            # F6 w odtwarzaczu TEZ wraca na liste (navigator.show_player), wiec
            # musi przejsc przez polityke pauzy tak samo jak Escape.
            if self.navigator.session.view is View.PLAYER:
                self._leave_player_to_list()
            else:
                self._run(self.navigator.show_player())
        elif action is Action.SHOW_LIST:
            self._leave_player_to_list()
        elif action is Action.SESSION_OPTIONS:
            self._show_session_options()
        elif action is Action.SELECT_AUDIO_OUTPUT:
            self._choose_audio_output()
        elif action is Action.GENERAL_SETTINGS:
            self._show_general_playback_options()
        elif action is Action.VIEW_PRESETS:
            self._show_presets()
        elif action is Action.ASSIGN_PRESET:
            self._assign_preset()
        elif (slot := preset_slot(action)) is not None:
            self._activate_preset(slot)
        elif action is Action.PLAY_PAUSE:
            self._play_pause()
        elif action is Action.ADD_BOOKMARK:
            self._add_bookmark()
        elif action is Action.CLIP_MARK_START:
            self._mark_audio_clip(start=True)
        elif action is Action.CLIP_MARK_END:
            self._mark_audio_clip(start=False)
        elif action is Action.CLIP_JUMP_START:
            self._jump_to_audio_clip_boundary(end=False)
        elif action is Action.CLIP_JUMP_END:
            self._jump_to_audio_clip_boundary(end=True)
        elif action is Action.CLIP_PREVIOUS_BOUNDARY:
            self._jump_to_relative_audio_clip_boundary(direction=-1)
        elif action is Action.CLIP_NEXT_BOUNDARY:
            self._jump_to_relative_audio_clip_boundary(direction=1)
        elif action is Action.CLIP_EXPORT:
            self._export_audio_clip()
        elif action is Action.CLIP_APPEND:
            self._append_audio_clip()
        elif action is Action.CLIP_REMOVE:
            self._remove_audio_clip()
        elif action is Action.CLIP_CLEAR:
            self._clear_audio_clip_selection()
        elif action in (Action.QUEUE_NEXT, Action.QUEUE_PREVIOUS):
            self._queue_step(action is Action.QUEUE_NEXT)
        elif action in (Action.ADD_TO_QUEUE, Action.TOGGLE_PLAY_NEXT):
            self._toggle_queue_membership(action is Action.TOGGLE_PLAY_NEXT)
        elif (step := seek_step_seconds(action, custom_seconds=self.messages.custom_seek_seconds)) is not None:
            # Krok czyta parytet transportu: 10 / 30 / 60 s i czas z ustawien
            # AMC (MainWindow.xaml.cs:21583-21590, 22387-22394). Wartosc
            # wlasna bierzemy z profilu, nie z twardej liczby.
            self._seek(float(step))
        elif (percent := seek_percent_value(action)) is not None:
            self._seek_percent(percent)
        elif action in (
            Action.VOLUME_UP_5, Action.VOLUME_DOWN_5,
            Action.VOLUME_UP_1, Action.VOLUME_DOWN_1,
        ):
            self._adjust_volume({
                Action.VOLUME_UP_5: 5, Action.VOLUME_DOWN_5: -5,
                Action.VOLUME_UP_1: 1, Action.VOLUME_DOWN_1: -1,
            }[action])
        elif action is Action.RATE_UP:
            # Drabina predkosci z oryginalu, nie plaskie +/- 0,1
            # (MainWindow.xaml.cs:16090-16115).
            self._set_rate(next_playback_rate(self.options.rate, +1))
        elif action is Action.RATE_DOWN:
            self._set_rate(next_playback_rate(self.options.rate, -1))
        elif action is Action.RATE_RESET:
            self._set_rate(1.0)
        elif action in (Action.TIME_ELAPSED, Action.TIME_REMAINING, Action.TIME_TOTAL):
            self._announce_time(action)
        elif action in (Action.TRACK_START, Action.TRACK_END):
            self._seek_to_track_edge(action is Action.TRACK_END)
        elif action is Action.TOGGLE_SEEK_MESSAGES:
            self._toggle_seek_messages()
        elif action is Action.OPEN_FOLDER_DIALOG:
            self._choose_folder()
        elif action is Action.OPEN_FILE_DIALOG:
            self._choose_file()
        elif action is Action.QUICK_INFORMATION:
            self._announce_quick_information()
        elif action is Action.COPY_NAME:
            self._copy_name()
        elif action is Action.COPY_ADDRESS:
            self._copy_address()
        elif action is Action.CUT_FILE:
            self._cut_file()
        elif action is Action.RENAME_LIBRARY_ITEM:
            self._rename_library_item()
        elif action is Action.RENAME_LOCAL_FILE:
            self._rename_local_file()
        elif action is Action.REMOVE_SELECTED:
            self._remove_selected_items()
        elif action is Action.RECYCLE_SELECTED:
            self._recycle_selected_files()
        elif action is Action.FOCUS_FILTER:
            self._focus_filter()
        elif action is Action.STATION_ADD:
            self._station_add()
        elif action is Action.STATION_EDIT:
            self._station_edit()
        elif action is Action.STATION_DELETE:
            self._station_delete()
        elif action is Action.STATION_IMPORT:
            self._station_import()
        elif action is Action.RECORD_TOGGLE:
            self._toggle_radio_recording()
        elif action is Action.RECORD_PAUSE:
            self._toggle_radio_recording_pause()
        elif action is Action.RECORD_SPLIT:
            self._split_radio_recording()
        elif action is Action.RECORD_STOP_ALL:
            self._stop_all_radio_recordings()
        elif action is Action.VIEW_ACTIVE_RECORDINGS:
            self._show_active_radio_recordings()
        elif action is Action.VIEW_RECORDED_RADIO_FILES:
            self._show_radio_recording_history()
        elif action is Action.MANAGE_RADIO_SCHEDULES:
            self._show_radio_schedules()
        elif action is Action.ADD_PODCAST_SOURCE:
            self._add_podcast_source()
        elif action is Action.IMPORT_PODCAST_OPML:
            self._import_podcast_opml()
        elif action is Action.EXPORT_PODCAST_OPML:
            self._export_podcast_opml()
        elif action is Action.EXPORT_YOUTUBE_SUBSCRIPTIONS:
            self._export_youtube_subscriptions()
        elif action is Action.VIEW_PODCAST_INBOX:
            self._show_podcast_inbox()
        elif action is Action.VIEW_PODCAST_DOWNLOADS:
            self._show_podcast_downloads()
        elif action in (
            Action.SORT_PODCAST_INBOX_ADDED,
            Action.SORT_PODCAST_INBOX_ALPHABETICAL,
            Action.SORT_PODCAST_INBOX_BY_PODCAST,
        ):
            self._set_podcast_inbox_sort(action)
        elif action is Action.REFRESH_PODCAST:
            self._refresh_podcasts(refresh_all=False)
        elif action is Action.REFRESH_PODCAST_LIBRARY:
            self._refresh_podcasts(refresh_all=True)
        elif action is Action.DOWNLOAD_PODCAST_EPISODES:
            self._download_podcast_episodes()
        elif action is Action.SAVE_PODCAST_EPISODE_AS:
            self._save_podcast_episode_as()
        elif action is Action.SHOW_PODCAST_DESCRIPTION:
            self._show_podcast_description()
        elif action is Action.GO_TO_RELATED_PODCAST:
            self._go_to_related_podcast()
        elif action is Action.TOGGLE_PODCAST_FAVORITE:
            self._toggle_podcast_favorite()
        elif action is Action.VIEW_ALL_FILES:
            self._run(self.navigator.open_library_view(LibraryView.ALL_FILES))
        elif action is Action.VIEW_FAVORITES:
            if self.navigator.active is SessionId.PODCASTS:
                self._transient_preview_return = None
                self._open_podcast_aggregate(
                    OpenPodcastAggregateView(LibraryView.PODCAST_FAVORITES)
                )
            else:
                self._run(self.navigator.open_library_view(LibraryView.FAVORITES))
        elif action is Action.VIEW_PLAYLISTS:
            self._run(self.navigator.open_library_view(LibraryView.PLAYLISTS))
        elif action is Action.VIEW_FOLDERS:
            # Foldery Biblioteki dzialaly juz z menu kontekstu startu; tutaj
            # dostaja jawne wejscie (Alt+1, jak MainWindow.xaml:446).
            self._open_library(self.library.saved_folder())
        elif action is Action.VIEW_LIBRARY:
            self._return_to_library()
        elif action is Action.VIEW_HISTORY:
            if self.navigator.active is SessionId.PODCASTS:
                self._transient_preview_return = None
                self._open_podcast_aggregate(
                    OpenPodcastAggregateView(LibraryView.PODCAST_HISTORY)
                )
            else:
                self._run(self.navigator.open_library_view(LibraryView.HISTORY))
        elif action is Action.VIEW_SAVED_QUEUE:
            if self.navigator.active is SessionId.PODCASTS:
                self._transient_preview_return = None
                self._open_podcast_aggregate(
                    OpenPodcastAggregateView(LibraryView.PODCAST_QUEUE)
                )
            else:
                self._run(self.navigator.open_queue_view())
        elif action is Action.VIEW_ITEM_BOOKMARKS:
            self._run(self.navigator.open_item_bookmarks())
        elif action is Action.VIEW_ALL_BOOKMARKS:
            # Ctrl+B i pozycja menu wchodza TA SAMA droga: jedna akcja, jeden
            # dispatcher. Inaczej gest i menu mogly by sie rozjechac.
            self._run(self.navigator.open_all_bookmarks())
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
            elif isinstance(intent, OpenLibraryView):
                self._open_library_view(intent)
            elif isinstance(intent, OpenQueueView):
                self._open_queue_view()
            elif isinstance(intent, OpenPodcastView):
                self._open_podcast_view(intent)
            elif isinstance(intent, OpenPodcastAggregateView):
                self._open_podcast_aggregate(intent)
            elif isinstance(intent, PlayTrack):
                self._play_track(intent)
            elif isinstance(intent, PlayFromQueue):
                self._play_from_queue(intent)
            elif isinstance(intent, PlayQueueAt):
                self._play_queue_at(intent)
            elif isinstance(intent, PlayStation):
                self._play_station(intent)
            elif isinstance(intent, PlayMedia):
                self._play_media(intent)
        self._sync_views()

    def _sync_views(self) -> None:
        """Odwzoruj stan nawigatora. Fokus ruszamy TYLKO przy zmianie widoku."""
        session = self.navigator.session
        # Menu musi zgadzac sie z kontekstem, ktory wlasnie sie zmienil.
        self._refresh_menu_state()
        self.session_label.SetLabel({
            SessionId.FILES: "Pliki lokalne",
            SessionId.RADIO: "Radio internetowe",
            SessionId.PODCASTS: "Podcasty i YouTube",
        }[self.navigator.active])

        active_list = self._active_list()
        all_lists = tuple(
            candidate for candidate in (
                getattr(self, "files_list", None),
                getattr(self, "radio_list", None),
                getattr(self, "podcasts_list", None),
            )
            if candidate is not None
        )
        other_lists = tuple(control for control in all_lists if control is not active_list)
        # FILTR TEGO WIDOKU przywracamy PRZED ``sync_rows``, bo on wlasnie
        # liczy stan zadany listy. Port ``RestoreFilterForCurrentView``
        # (MainWindow.xaml.cs:10196-10204): wejscie w widok oddaje jego wlasny
        # tekst filtra, a nie tekst widoku, z ktorego przyszlismy.
        self._restore_filter_for_current_view()
        # JEDNA droga odswiezenia dla WSZYSTKICH widokow, bez wymiany kontrolki.
        # ``sync_rows`` sam decyduje, czy jest co robic: gdy dane, kolejnosc,
        # teksty i kursor sa te same, nie wykonuje ZADNEJ operacji na liscie.
        # Dlatego wolanie go na koncu kazdego ``_run`` (takze po samym
        # komunikacie czy Ctrl+C) nie odswieza juz listy bez potrzeby.
        #
        # Rekreacja kontrolki (nowy HWND dla czystego cache czytnika) byla tu
        # przez chwile i ZOSTALA WYCOFANA PO POMIARZE. Usuwala wprawdzie stara
        # nazwe z nowym licznikiem, ale na glownym widoku (2476 wierszy)
        # czytnik przestawal mowic wybrany wiersz w ogole -- slychac bylo tylko
        # naglowek widoku. Kwity: ``przed-po-przed.json`` (stary kod, wiersz
        # czytany) vs ``przed-po-po.json`` (z rekreacja, cisza) oraz para
        # ``odbior-listy-Z-REKREACJA.json`` / ``odbior-listy-BEZ-REKREACJI.json``.
        # Siedem prob ratowania rekreacji (kolejnosc fokus/wypelnienie, obrot
        # petli, podwojny ``SetFocus``, wymuszony ``SetItemState``, kolejnosc
        # ``Destroy``, zdjecie nakladki) nie przywrocilo tego odczytu.
        # Problem "stara nazwa z nowym licznikiem" rozwiazuje teraz sama zmiana
        # na zwykla liste: tekst siedzi w kontrolce, a nie w cache wirtualnym,
        # wiec stara nazwa nie ma skad wrocic.
        active_list.sync_rows()

        want_player = session.view is View.PLAYER
        changed = self.player_panel.IsShown() != want_player or any(
            control.IsShown() for control in other_lists
        )

        self.list_panel.Show(not want_player)
        self.player_panel.Show(want_player)
        active_list.Show(not want_player)
        for control in other_lists:
            control.Hide()
        self.panel.Layout()

        if want_player:
            self.now_playing.SetLabel(session.now_playing_title or "Nic nie jest odtwarzane")
            if changed:
                self.play_button.SetFocus()
        elif changed:
            active_list.SetFocus()

    def _active_list(self) -> MediaListCtrl:
        # Galazie zamiast slownika: testy logiki buduja lekkie okno tylko z
        # kontrolkami sesji, ktora sprawdzaja. Slownik obliczalby wszystkie
        # wartosci z gory i pytal o ``podcasts_list`` nawet przy sesji Plikow.
        if self.navigator.active is SessionId.FILES:
            return self.files_list
        if self.navigator.active is SessionId.RADIO:
            return self.radio_list
        return self.podcasts_list

    def _on_item_focused(self, event: wx.ListEvent) -> None:
        """Fokus jest kotwica akcji takze wtedy, gdy wiersz nie jest wybrany.

        To natywny etap windowsowego wyboru nieprzylegajacego:
        Ctrl+strzalka przesuwa fokus bez kasowania dotychczasowych zaznaczen,
        a Ctrl+Spacja przelacza dopiero ten wiersz. Model przechowuje jedno ID
        wlasnie jako kotwice nawigacji; pelny zbior zaznaczen nadal pozostaje
        w SysListView32 i nie jest tu kopiowany ani emulowany.
        """
        control = event.GetEventObject()
        if not isinstance(control, MediaListCtrl) or control.updating:
            event.Skip()
            return
        item_id = control.shown_item_id(event.GetIndex())
        if item_id is not None:
            self.navigator.session.model.select_id(item_id)
            self._refresh_menu_state()
            self._play_radio_activity_cue(self.navigator.session.model.selected_row)
        event.Skip()

    def _play_radio_activity_cue(self, row: Row | None) -> None:
        """Opcjonalny, krotki sygnal stanu. Nie dotyka hosta ani audio AMC."""
        if (
            row is None
            or row.kind != "station"
            or getattr(self.navigator, "active", None) is not SessionId.RADIO
        ):
            return
        playback = bool(
            row.playback_activity and self.options.radio_playback_state_sound
        )
        recording = bool(
            row.recording_activity and self.options.radio_recording_state_sound
        )
        if not playback and not recording:
            return
        try:
            import wx.adv as wxadv

            key = (playback, recording)
            sound = self._radio_activity_cues.get(key)
            if sound is None:
                sound = wxadv.Sound()
                if not sound.CreateFromData(
                    activity_cue_wav(playback=playback, recording=recording)
                ):
                    return
                self._radio_activity_cues[key] = sound
            sound.Play(wxadv.SOUND_ASYNC)
        except (ImportError, AttributeError, RuntimeError):
            # Sygnal jest dodatkiem. Jego brak nie moze zepsuc strzalek ani
            # wypowiadania nazwy stacji przez natywna liste.
            return

    def _on_item_selected(self, event: wx.ListEvent) -> None:
        """Zaznaczenie z klawiatury/myszy wraca do modelu, zeby ID pozostal
        stabilny po odswiezeniu listy.

        BRAMKA ``updating``: nasza wlasna aktualizacja listy (wstawienie wiersza,
        ``SetItemState``) wysyla DOKLADNIE TO SAMO zdarzenie co ruch
        uzytkownika. Bez bramki przejsciowy indeks z trwajacej podmiany
        nadpisalby swiadomy wybor uzytkownika -- np. po wstawieniu pierwszego
        wiersza model wskazywalby 0 zamiast zapamietanego utworu.
        """
        control = event.GetEventObject()
        if isinstance(control, MediaListCtrl) and control.updating:
            event.Skip()
            return
        # INDEKS Z KONTROLKI, NIE Z MODELU. Przy aktywnym filtrze lista
        # pokazuje tylko wiersze dopasowane, wiec ``GetIndex()`` numeruje
        # WIDOCZNE wiersze. Przelozenie go na ``item_id`` tego, co kontrolka
        # faktycznie ma w ``_shown``, jest jedyna poprawna droga -- bez tego
        # strzalka w dol na przefiltrowanej liscie wybieralaby w modelu
        # zupelnie inny utwor (ten pod tym samym numerem w PELNYM zbiorze), a
        # Enter odtwarzalby nie to, co czytnik przeczytal.
        if isinstance(control, MediaListCtrl):
            item_id = control.shown_item_id(event.GetIndex())
            if item_id is not None:
                self.navigator.session.model.select_id(item_id)
                self._refresh_menu_state()
                event.Skip()
                return
        self.navigator.session.model.select_index(event.GetIndex())
        # Bramki ``needs_selection`` zmieniaja sie WRAZ Z ZAZNACZENIEM, a nie
        # tylko przy zmianie widoku. Zmierzone na zywym GUI (statusclip-2):
        # bez tego wywolania pozycja "Skopiuj adres" zostawala WYLACZONA po
        # strzalce na liscie, a wylaczony akcelerator POLYKAL Ctrl+Shift+C --
        # klawisz docieral do okna i nie robil nic.
        self._refresh_menu_state()
        event.Skip()

    def _on_item_deselected(self, event: wx.ListEvent) -> None:
        """Nie pozwol, by model wskazywal wiersz usuniety z zaznaczenia.

        Przy Ctrl+klik lub skracaniu zakresu Shift natywna lista moze zostawic
        fokus na wierszu juz niezaznaczonym. To POPRAWNY stan dla Ctrl+Spacji:
        kotwica modelu zostaje wtedy na fokusie, a ``_cursor_target`` nie
        zamienia go z powrotem w zaznaczenie. Gdy fokus jest gdzie indziej,
        kotwice przenosimy na rzeczywiscie zaznaczony wiersz.
        """
        control = event.GetEventObject()
        if not isinstance(control, MediaListCtrl) or control.updating:
            event.Skip()
            return
        item_id = control.shown_item_id(event.GetIndex())
        model = self.navigator.session.model
        if item_id is not None and model.selected_id == item_id:
            focused = control.GetFocusedItem()
            if focused != event.GetIndex():
                replacement = (
                    focused
                    if control._is_index_selected(focused)
                    else control.GetFirstSelected()
                )
                replacement_id = control.shown_item_id(replacement)
                model.select_id(replacement_id)
            self._refresh_menu_state()
        event.Skip()

    def _activate(self) -> None:
        self._run(self.navigator.activate_selected(
            stay_on_list_after_radio_enter=(
                self.navigator.active is SessionId.RADIO
                and self.options.stay_on_list_after_radio_enter
            )
        ))

    def _current_preset_target(self) -> profile_presets.PresetTarget | None:
        """Element listy albo faktycznie odtwarzany element widoku gracza."""
        session = self.navigator.active
        state = self.navigator.session
        row = state.model.selected_row
        if state.view is View.PLAYER and state.now_playing_id:
            row = next(
                (
                    candidate for candidate in (
                        *state.playback_source_rows,
                        *state.model.rows,
                    )
                    if candidate.item_id == state.now_playing_id
                ),
                None,
            )
            # Plik otwarty bezposrednio przez Ctrl+O moze nie miec jeszcze
            # wiersza w Bibliotece, ale host zwraca jego prawdziwa sciezke.
            if row is None and session is SessionId.FILES:
                source = self._last_status.get("source")
                if isinstance(source, str) and source.strip():
                    row = Row(
                        item_id=state.now_playing_id,
                        title=state.now_playing_title or Path(source).name,
                        kind="track",
                        path=source.strip(),
                    )
        return profile_presets.target_from_row(session, row)

    def _preset_inputs(self, session: SessionId) -> tuple[dict, list[dict]]:
        """Niezmienne migawki dla watku odczytujacego duzy profil AMC."""
        overrides = {
            key: [dict(item) for item in value]
            for key, value in self.state.preset_overrides.items()
        }
        stations = self.stations.as_payload() if session is SessionId.RADIO else []
        return overrides, stations

    def _show_presets(self) -> None:
        """Ctrl+Alt+P: jawna lista 12 miejsc, bez zapisu i bez technicznych ID."""
        session = self.navigator.active
        target = self._current_preset_target()
        current_target_id = target.target_id if target is not None else None
        overrides, _stations = self._preset_inputs(session)

        def work() -> tuple[profile_presets.PresetEntry, ...]:
            return profile_presets.effective_entries(
                self.layout.state_json, session, overrides
            )

        def done(entries: tuple[profile_presets.PresetEntry, ...]) -> None:
            if self.navigator.active is not session:
                return
            session_name = session_options.session_display_name(session)
            with PresetListDialog(
                self,
                choices=profile_presets.choices(entries),
                session_name=session_name,
                current_target_id=current_target_id,
            ) as dialog:
                if dialog.ShowModal() != wx.ID_OK:
                    self._active_list().SetFocus() if self.navigator.view is View.LIST else self.play_button.SetFocus()
                    return
                choice = dialog.selected_choice
            if choice is not None:
                self._activate_preset(choice.slot)

        def failed(error: Exception) -> None:
            if isinstance(error, profile_presets.PresetProfileError):
                self.announcer.say(str(error))
            else:
                self.announcer.say("Nie udało się odczytać presetów")

        self.runner.submit("preset", work, done, failed)

    def _assign_preset(self) -> None:
        """Ctrl+Alt+Shift+P: trwale zapisz tylko prywatne nadpisanie wxPython."""
        session = self.navigator.active
        target = self._current_preset_target()
        if target is None:
            self.announcer.say(
                f"Wybierz element sesji {session_options.session_display_name(session)}, "
                "który chcesz przypisać do presetu"
            )
            return
        overrides, _stations = self._preset_inputs(session)

        def work() -> tuple[profile_presets.PresetEntry, ...]:
            return profile_presets.effective_entries(
                self.layout.state_json, session, overrides
            )

        def done(entries: tuple[profile_presets.PresetEntry, ...]) -> None:
            if self.navigator.active is not session:
                return
            free = profile_presets.first_free_slot(entries)
            initial = profile_presets.existing_target_slot(entries, target.target_id) or free or 1
            with PresetAssignmentDialog(
                self,
                target=target,
                choices=profile_presets.choices(entries),
                first_free_slot=free,
                initial_slot=initial,
                session_name=session_options.session_display_name(session),
            ) as dialog:
                if dialog.ShowModal() != wx.ID_OK:
                    self._active_list().SetFocus() if self.navigator.view is View.LIST else self.play_button.SetFocus()
                    return
                slot = dialog.selected_slot
                action = dialog.selected_action

            updated = (
                profile_presets.remove_entry(entries, slot)
                if action == "remove"
                else profile_presets.replace_entry(entries, target, slot)
            )
            previous = {
                key: [dict(item) for item in value]
                for key, value in self.state.preset_overrides.items()
            }
            next_overrides = {
                key: [dict(item) for item in value]
                for key, value in previous.items()
            }
            next_overrides[session.value] = profile_presets.entries_payload(updated)
            self.state.preset_overrides = next_overrides
            try:
                self.store.save(self.state)
            except Exception:
                self.state.preset_overrides = previous
                self.announcer.say("Nie udało się zapisać presetów. Niczego nie zmieniono")
                return

            spoken = profile_presets.shortcut_label(slot, spoken=True)
            if action == "remove":
                self.announcer.say(f"Usunięto preset {spoken}")
            else:
                self.announcer.say(f"Zapisano preset {spoken}: {target.title}")

        def failed(error: Exception) -> None:
            if isinstance(error, profile_presets.PresetProfileError):
                self.announcer.say(str(error))
            else:
                self.announcer.say("Nie udało się przygotować przypisania presetu")

        self.runner.submit("preset", work, done, failed)

    def _activate_preset(self, slot: int) -> None:
        """Uruchom preset profilu AMC albo jego prywatne nadpisanie wxPython."""
        session = self.navigator.active
        spoken = profile_presets.shortcut_label(slot, spoken=True)
        overrides, stations = self._preset_inputs(session)

        def work():
            return profile_presets.resolve_preset(
                self.layout.state_json,
                session,
                slot,
                overrides=overrides,
                current_stations=stations,
            )

        def done(resolved: profile_presets.ResolvedPreset) -> None:
            # Preset nalezy do sesji z chwili nacisniecia. Odpowiedz po zmianie
            # sesji nie moze uruchomic materialu w obcym kontekscie.
            if self.navigator.active is not session:
                return
            entry = resolved.entry
            if entry is None:
                self.announcer.say(
                    f"Preset {spoken} pusty. Ctrl+Alt+Shift+P przypisuje bieżący element."
                )
                return

            if session is SessionId.RADIO:
                target = resolved.radio_target
                if target is None:
                    self.announcer.say(
                        f"Preset {spoken} jest niedostępny. Stacji nie ma już w profilu."
                    )
                    return
                sequence = tuple(
                    Row(
                        item_id=item.item_id,
                        title=item.title,
                        kind="station",
                        url=item.url,
                        show_kind=False,
                    )
                    for item in resolved.radio_sequence
                )
                target_row = next(
                    item for item in sequence if item.item_id == target.item_id
                )
                self._run(self.navigator.activate_radio_preset(
                    target_row,
                    sequence,
                    open_player=self.options.open_player_when_activating_preset,
                ))
                return

            kind = entry.target_kind.casefold()
            if kind == "folder" and entry.location:
                self._open_library(entry.location)
                return
            if kind == "amcplaylist":
                playlist_id = entry.location
                if not playlist_id and ":" in entry.target_id:
                    playlist_id = entry.target_id.split(":", 1)[1]
                if playlist_id:
                    self._open_library_view(OpenLibraryView(
                        view=LibraryView.PLAYLIST_CONTENTS,
                        playlist_id=playlist_id,
                        target_session_id=SessionId.FILES,
                    ))
                    return
            if kind in ("track", "file") and entry.location:
                target_row = Row(
                    item_id=entry.target_id,
                    title=entry.title or Path(entry.location).name,
                    kind="track",
                    path=entry.location,
                )
                self._run(self.navigator.activate_local_preset(
                    target_row,
                    open_player=self.options.open_player_when_activating_preset,
                ))
                return
            self.announcer.say(
                f"Preset {spoken} istnieje, ale ten rodzaj materiału nie jest jeszcze przeniesiony."
            )

        def failed(error: Exception) -> None:
            if isinstance(error, profile_presets.PresetProfileError):
                self.announcer.say(str(error))
            else:
                self.announcer.say("Nie udało się odczytać presetu")

        self.runner.submit("preset", work, done, failed)

    def _switch_session(self, session_id: SessionId) -> None:
        self._run(self.navigator.switch_session(session_id))
        if session_id is SessionId.PODCASTS:
            state = self.navigator.sessions[SessionId.PODCASTS]
            if not state.model.rows:
                self._open_podcast_library(preferred_id=state.list_anchor_id)

    # ------------------------------------------------ Podcasty i YouTube

    def _apply_podcast_result(self, events: list[object]) -> None:
        """Apply data silently when its session is no longer on screen."""
        if self.navigator.active is SessionId.PODCASTS:
            self._run(events)
        else:
            self._sync_views()

    def _open_podcast_library(
        self,
        preferred_id: str | None = None,
        *,
        completion_message: str = "",
    ) -> None:
        """Load the shared AMC podcast/channel library outside the GUI thread."""
        if not self.podcasts.is_available:
            if self.navigator.active is SessionId.PODCASTS:
                self.announcer.say("Nie znaleziono biblioteki podcastów AMC")
            return

        def work():
            return self.podcasts.subscriptions()

        def done(subscriptions) -> None:
            if completion_message:
                state = self.navigator.sessions[SessionId.PODCASTS]
                if (
                    self.navigator.active is not SessionId.PODCASTS
                    or state.view is not View.LIST
                    or state.library_view is not LibraryView.PODCAST_LIBRARY
                ):
                    return
            events = self.navigator.apply_podcast_library(
                subscription_rows(subscriptions), preferred_id=preferred_id
            )
            if completion_message:
                self._sync_views()
                self.announcer.say(completion_message)
            else:
                self._apply_podcast_result(events)

        def failed(error: Exception) -> None:
            if self.navigator.active is SessionId.PODCASTS:
                self.announcer.say(str(error) if isinstance(error, PodcastProfileError)
                                   else "Nie udało się odczytać biblioteki podcastów")

        self.runner.submit("podcast-view", work, done, failed)

    def _open_podcast_view(
        self,
        intent: OpenPodcastView,
        *,
        completion_message: str = "",
    ) -> None:
        if not intent.subscription_id:
            self._open_podcast_library(
                preferred_id=intent.preferred_id,
                completion_message=completion_message,
            )
            return

        subscription_id = intent.subscription_id
        previous_count = self._podcast_loaded_counts.get(subscription_id, 0)
        requested = (
            max(PODCAST_PAGE_SIZE, previous_count + PODCAST_PAGE_SIZE)
            if intent.load_more
            else max(PODCAST_PAGE_SIZE, previous_count)
        )

        def work():
            subscription = self.podcasts.subscription(subscription_id)
            if subscription is None:
                raise PodcastProfileError("Tego podcastu nie ma już w Bibliotece.")
            page = self.podcasts.episodes(subscription_id, loaded_count=requested)
            return subscription, page

        def done(result) -> None:
            if completion_message:
                state = self.navigator.sessions[SessionId.PODCASTS]
                if (
                    self.navigator.active is not SessionId.PODCASTS
                    or state.view is not View.LIST
                    or state.library_view is not LibraryView.PODCAST_EPISODES
                    or state.library_playlist_id != subscription_id
                ):
                    return
            subscription, page = result
            self._podcast_loaded_counts[subscription_id] = page.loaded_count
            preferred = intent.preferred_id
            if intent.load_more and previous_count < len(page.rows):
                candidate = page.rows[previous_count]
                if candidate.kind == "episode":
                    preferred = candidate.item_id
            events = self.navigator.apply_podcast_episodes(
                subscription_id,
                subscription.title,
                page.rows,
                preferred_id=preferred,
            )
            if completion_message:
                self._sync_views()
                self.announcer.say(completion_message)
            else:
                self._apply_podcast_result(events)

        def failed(error: Exception) -> None:
            if self.navigator.active is SessionId.PODCASTS:
                self.announcer.say(str(error) if isinstance(error, PodcastProfileError)
                                   else "Nie udało się odczytać odcinków")

        self.runner.submit("podcast-view", work, done, failed)

    def _show_podcast_inbox(self) -> None:
        """Ctrl+I: global preview that Escape restores exactly."""
        self._begin_transient_preview()
        self.navigator.active = SessionId.PODCASTS
        # Oznaczamy widok od razu, zanim watek SQLite odda dane. Escape ma
        # dzialac takze podczas wczytywania, a spozniony wynik po Escape nie
        # moze ponownie otworzyc podgladu.
        podcast_state = self.navigator.sessions[SessionId.PODCASTS]
        podcast_state.library_view = LibraryView.PODCAST_INBOX
        podcast_state.view = View.LIST
        self._open_podcast_aggregate(
            OpenPodcastAggregateView(LibraryView.PODCAST_INBOX)
        )

    def _show_podcast_downloads(self) -> None:
        if self.navigator.active is not SessionId.PODCASTS:
            self.announcer.say(
                "Pobrane odcinki są dostępne w sesji Podcasty i YouTube"
            )
            return
        self._transient_preview_return = None
        self._open_podcast_aggregate(
            OpenPodcastAggregateView(LibraryView.PODCAST_DOWNLOADS)
        )

    def _set_podcast_inbox_sort(self, action: Action) -> None:
        """Alt+1/2/3 in the podcast inbox, with a private durable choice."""
        state = self.navigator.sessions[SessionId.PODCASTS]
        if (
            self.navigator.active is not SessionId.PODCASTS
            or state.view is not View.LIST
            or state.library_view is not LibraryView.PODCAST_INBOX
        ):
            self.announcer.say(
                "Sortowanie Alt+1, Alt+2 i Alt+3 działa w Nowych odcinkach"
            )
            return

        choices = {
            Action.SORT_PODCAST_INBOX_ADDED: (
                SORT_ADDED_NEWEST,
                "Według dodania, najnowsze na początku",
            ),
            Action.SORT_PODCAST_INBOX_ALPHABETICAL: (
                SORT_ALPHABETICAL,
                "Alfabetycznie",
            ),
            Action.SORT_PODCAST_INBOX_BY_PODCAST: (
                SORT_CUSTOM,
                "Według podcastu",
            ),
        }
        choice = choices.get(action)
        if choice is None:
            return
        mode, label = choice
        previous_override = self.state.podcast_inbox_sort_mode
        previous_effective = self._podcast_inbox_sort_mode
        self.state.podcast_inbox_sort_mode = mode
        self._podcast_inbox_sort_mode = mode
        if not self._save_state():
            self.state.podcast_inbox_sort_mode = previous_override
            self._podcast_inbox_sort_mode = previous_effective
            self._refresh_menu_state()
            return

        self._refresh_menu_state()
        self._open_podcast_aggregate(OpenPodcastAggregateView(
            LibraryView.PODCAST_INBOX,
            preferred_id=state.model.selected_id,
            announcement=label,
        ))

    def _open_podcast_aggregate(
        self,
        intent: OpenPodcastAggregateView,
        *,
        completion_message: str = "",
    ) -> None:
        if intent.view not in (
            LibraryView.PODCAST_FAVORITES,
            LibraryView.PODCAST_HISTORY,
            LibraryView.PODCAST_QUEUE,
            LibraryView.PODCAST_INBOX,
            LibraryView.PODCAST_DOWNLOADS,
        ):
            return
        if not self.podcasts.is_available:
            self._restore_transient_preview(announce=False, force=True)
            self.announcer.say("Nie znaleziono biblioteki podcastów AMC")
            return

        key = intent.view.value
        previous_count = self._podcast_loaded_counts.get(key, 0)
        requested = (
            max(PODCAST_PAGE_SIZE, previous_count + PODCAST_PAGE_SIZE)
            if intent.load_more
            else max(PODCAST_PAGE_SIZE, previous_count)
        )
        sort_mode_override = self.state.podcast_inbox_sort_mode

        def work():
            if intent.view is LibraryView.PODCAST_FAVORITES:
                return self.podcasts.favorites(
                    loaded_count=requested,
                    collation=getattr(self, "_collation", None),
                )
            if intent.view is LibraryView.PODCAST_HISTORY:
                return self.podcasts.history(loaded_count=requested)
            if intent.view is LibraryView.PODCAST_QUEUE:
                return self.podcasts.queue(loaded_count=requested)
            if intent.view is LibraryView.PODCAST_INBOX:
                return self.podcasts.inbox(
                    loaded_count=requested,
                    sort_mode=sort_mode_override,
                    collation=getattr(self, "_collation", None),
                )
            return self.podcasts.downloads(loaded_count=requested)

        def done(page) -> None:
            if (
                intent.view is LibraryView.PODCAST_INBOX
                and self._transient_preview_return is None
            ):
                return
            if completion_message:
                state = self.navigator.sessions[SessionId.PODCASTS]
                if (
                    self.navigator.active is not SessionId.PODCASTS
                    or state.view is not View.LIST
                    or state.library_view is not intent.view
                ):
                    return
            self._podcast_loaded_counts[key] = page.loaded_count
            if (
                intent.view is LibraryView.PODCAST_INBOX
                and page.sort_mode in (
                    SORT_ADDED_NEWEST,
                    SORT_ALPHABETICAL,
                    SORT_CUSTOM,
                )
            ):
                self._podcast_inbox_sort_mode = page.sort_mode
            preferred = intent.preferred_id
            if intent.load_more and previous_count < len(page.rows):
                candidate = page.rows[previous_count]
                if candidate.kind in ("podcast", "episode"):
                    preferred = candidate.item_id
            heading = {
                LibraryView.PODCAST_FAVORITES: "Ulubione",
                LibraryView.PODCAST_HISTORY: "Historia odtwarzania",
                LibraryView.PODCAST_QUEUE: "Kolejka",
                LibraryView.PODCAST_INBOX: "Nowe odcinki i materiały",
                LibraryView.PODCAST_DOWNLOADS: "Pobrane",
            }[intent.view]
            events = self.navigator.apply_podcast_aggregate(
                intent.view,
                heading,
                page.rows,
                preferred_id=preferred,
                order_matches_amc=page.order_matches_amc,
            )
            count = sum(
                1 for row in page.rows if row.kind in ("podcast", "episode")
            )
            if completion_message:
                # Wynik odswiezenia ma zastapic zwykly naglowek widoku. Lista
                # zostaje najpierw podmieniona, a czytnik dostaje jeden,
                # konkretny komunikat z licznikami -- rowniez gdy jest pusta.
                self._sync_views()
                self.announcer.say(completion_message)
            elif self.navigator.active is SessionId.PODCASTS and intent.announcement:
                # Przy zmianie porzadku najwazniejsza jest nowa wlasciwosc,
                # nie powtorny naglowek ani techniczna wartosc modelu.
                self._sync_views()
                if count:
                    self.announcer.say(intent.announcement)
                else:
                    message = next(
                        (event.text for event in events if isinstance(event, Announce)),
                        heading,
                    )
                    self.announcer.say(f"{intent.announcement}, {message}")
            elif self.navigator.active is SessionId.PODCASTS and count == 0:
                self._sync_views()
                message = next(
                    (event.text for event in events if isinstance(event, Announce)),
                    heading,
                )
                self._announce_after_native_list_update(message)
            else:
                self._apply_podcast_result(events)

        def failed(error: Exception) -> None:
            if (
                intent.view is LibraryView.PODCAST_INBOX
                and self._transient_preview_return is None
            ):
                return
            message = (
                str(error) if isinstance(error, PodcastProfileError)
                else "Nie udało się odczytać odcinków"
            )
            if intent.view is LibraryView.PODCAST_INBOX:
                self._restore_transient_preview(announce=False, force=True)
                self.announcer.say(message)
            elif self.navigator.active is SessionId.PODCASTS:
                self.announcer.say(message)

        self.runner.submit("podcast-view", work, done, failed)

    def _current_podcast_subscription_id(self) -> str | None:
        """Zrodlo dla F5 bez ujawniania technicznego identyfikatora w UI."""
        state = self.navigator.sessions[SessionId.PODCASTS]
        if state.library_view is LibraryView.PODCAST_EPISODES:
            return state.library_playlist_id
        row = state.model.selected_row
        if state.library_view is LibraryView.PODCAST_LIBRARY:
            return row.item_id if row is not None and row.kind == "podcast" else None
        if row is not None and row.kind == "episode":
            return row.parent_id
        return None

    def _add_podcast_source(self) -> None:
        """Ctrl+N: dodaj RSS, kolekcję YouTube albo publiczne medium."""
        if self.navigator.active is not SessionId.PODCASTS:
            self.announcer.say(
                "Dodawanie źródła jest dostępne w sesji Podcasty i YouTube"
            )
            return
        if self._podcast_add_pending:
            self.announcer.say("Dodawanie źródła już trwa")
            return
        client = self.client
        if client is None:
            self.announcer.say("Silnik nie działa, nie mogę dodać źródła")
            return

        with PodcastSourceDialog(self) as dialog:
            if dialog.ShowModal() != wx.ID_OK:
                return
            address, title = dialog.values
        if not address:
            self.announcer.say("Wpisz adres źródła")
            return

        self._podcast_add_pending = True
        started = "Sprawdzanie i dodawanie źródła"
        self.status_field.SetLabel(started)
        self.status_bar.show(started)
        self.announcer.say(started)

        def work() -> dict:
            result = client.add_podcast_source(address, title)
            return result if isinstance(result, dict) else {}

        def done(payload: dict) -> None:
            self._podcast_add_pending = False
            source_title = str(payload.get("title") or title or "źródło").strip()
            source_label = str(payload.get("sourceLabel") or "źródło").strip()
            count = max(0, int(payload.get("itemCount") or 0))
            if bool(payload.get("added")):
                change = "Dodano"
            elif bool(payload.get("restored")):
                change = "Ponownie dodano"
            else:
                change = "Zaktualizowano"
            message = (
                f"{change}: {source_label}: {source_title}. "
                f"Pozycji: {count}"
            )
            self.status_field.SetLabel(message)
            self.status_bar.show(message)

            subscription_id = str(payload.get("subscriptionId") or "").strip()
            state = self.navigator.sessions[SessionId.PODCASTS]
            if (
                subscription_id
                and self.navigator.active is SessionId.PODCASTS
                and state.view is View.LIST
                and state.library_view is LibraryView.PODCAST_LIBRARY
            ):
                self._open_podcast_library(
                    preferred_id=subscription_id,
                    completion_message=message,
                )
                return
            self.announcer.say(message)

        def failed(error: Exception) -> None:
            self._podcast_add_pending = False
            message = (
                str(error)
                if isinstance(error, (HostError, HostUnavailable))
                else "Nie udało się dodać źródła"
            )
            self.status_field.SetLabel(message)
            self.status_bar.show(message)
            self.announcer.say(message)

        self.runner.submit("podcast-add", work, done, failed)

    def _import_podcast_opml(self) -> None:
        """Ctrl+O w sesji podcastów: sprawdź OPML, wybierz i zaimportuj RSS."""
        if self.navigator.active is not SessionId.PODCASTS:
            self.announcer.say("Import OPML jest dostępny w sesji Podcasty i YouTube")
            return
        if self._podcast_opml_pending:
            self.announcer.say("Import lub eksport OPML już trwa")
            return
        client = self.client
        if client is None:
            self.announcer.say("Silnik nie działa, nie mogę zaimportować podcastów")
            return

        with wx.FileDialog(
            self,
            message="Importuj podcasty z OPML",
            wildcard="Pliki OPML i XML (*.opml;*.xml)|*.opml;*.xml|Wszystkie pliki (*.*)|*.*",
            style=wx.FD_OPEN | wx.FD_FILE_MUST_EXIST,
        ) as picker:
            if picker.ShowModal() != wx.ID_OK:
                return
            path = picker.GetPath()

        self._podcast_opml_pending = True
        started = "Odczytywanie listy podcastów z OPML"
        self.status_field.SetLabel(started)
        self.status_bar.show(started)
        self.announcer.say(started)

        def inspect() -> dict:
            result = client.inspect_podcast_opml(path)
            return result if isinstance(result, dict) else {}

        def inspected(payload: dict) -> None:
            entries = payload.get("entries")
            if not isinstance(entries, list) or not entries:
                self._podcast_opml_pending = False
                self.announcer.say("Plik OPML nie zawiera adresów podcastów")
                return
            with PodcastOpmlImportDialog(self, entries) as dialog:
                if dialog.ShowModal() != wx.ID_OK:
                    self._podcast_opml_pending = False
                    message = "Import OPML anulowany"
                    self.status_field.SetLabel(message)
                    self.status_bar.show(message)
                    return
                selected = dialog.selected_feed_urls

            message = f"Importowanie podcastów: {len(selected)}"
            self.status_field.SetLabel(message)
            self.status_bar.show(message)
            self.announcer.say(message)

            def import_selected() -> dict:
                result = client.import_podcast_opml(path, selected)
                return result if isinstance(result, dict) else {}

            def imported(result: dict) -> None:
                self._podcast_opml_pending = False
                imported_count = max(0, int(result.get("imported") or 0))
                failed_count = max(0, int(result.get("failed") or 0))
                completed = (
                    f"Zaimportowano podcasty: {imported_count}. "
                    f"Niepowodzenia: {failed_count}"
                )
                self.status_field.SetLabel(completed)
                self.status_bar.show(completed)
                state = self.navigator.sessions[SessionId.PODCASTS]
                if (
                    self.navigator.active is SessionId.PODCASTS
                    and state.view is View.LIST
                    and state.library_view is LibraryView.PODCAST_LIBRARY
                ):
                    self._open_podcast_library(completion_message=completed)
                else:
                    self.announcer.say(completed)

            def import_failed(error: Exception) -> None:
                self._podcast_opml_pending = False
                message = (
                    str(error)
                    if isinstance(error, (HostError, HostUnavailable))
                    else "Nie udało się zaimportować podcastów"
                )
                self.status_field.SetLabel(message)
                self.status_bar.show(message)
                self.announcer.say(message)

            self.runner.submit(
                "podcast-opml-import",
                import_selected,
                imported,
                import_failed,
            )

        def inspect_failed(error: Exception) -> None:
            self._podcast_opml_pending = False
            message = (
                str(error)
                if isinstance(error, (HostError, HostUnavailable))
                else "Nie udało się odczytać pliku OPML"
            )
            self.status_field.SetLabel(message)
            self.status_bar.show(message)
            self.announcer.say(message)

        self.runner.submit("podcast-opml-inspect", inspect, inspected, inspect_failed)

    def _export_podcast_opml(self) -> None:
        """Eksportuj zapisane podcasty RSS przez wspólny eksporter Core."""
        if self.navigator.active is not SessionId.PODCASTS:
            self.announcer.say("Eksport OPML jest dostępny w sesji Podcasty i YouTube")
            return
        if self._podcast_opml_pending:
            self.announcer.say("Import lub eksport OPML już trwa")
            return
        client = self.client
        if client is None:
            self.announcer.say("Silnik nie działa, nie mogę wyeksportować podcastów")
            return

        with wx.FileDialog(
            self,
            message="Eksportuj bibliotekę podcastów do OPML",
            defaultFile="Podcasty AMC.opml",
            wildcard="Pliki OPML (*.opml)|*.opml|Pliki XML (*.xml)|*.xml",
            style=wx.FD_SAVE | wx.FD_OVERWRITE_PROMPT,
        ) as picker:
            if picker.ShowModal() != wx.ID_OK:
                return
            path = picker.GetPath()

        self._podcast_opml_pending = True
        started = "Eksportowanie biblioteki podcastów do OPML"
        self.status_field.SetLabel(started)
        self.status_bar.show(started)

        def work() -> dict:
            result = client.export_podcast_opml(path)
            return result if isinstance(result, dict) else {}

        def done(payload: dict) -> None:
            self._podcast_opml_pending = False
            count = max(0, int(payload.get("count") or 0))
            message = f"Wyeksportowano podcasty: {count}"
            self.status_field.SetLabel(message)
            self.status_bar.show(message)
            self.announcer.say(message)

        def failed(error: Exception) -> None:
            self._podcast_opml_pending = False
            message = (
                str(error)
                if isinstance(error, (HostError, HostUnavailable))
                else "Nie udało się wyeksportować biblioteki podcastów"
            )
            self.status_field.SetLabel(message)
            self.status_bar.show(message)
            self.announcer.say(message)

        self.runner.submit("podcast-opml-export", work, done, failed)

    def _export_youtube_subscriptions(self) -> None:
        """Eksportuj kanały do innych klientów albo wszystkie źródła do OPML."""
        if self.navigator.active is not SessionId.PODCASTS:
            self.announcer.say(
                "Eksport kanałów YouTube jest dostępny w sesji Podcasty i YouTube"
            )
            return
        if self._podcast_opml_pending:
            self.announcer.say("Import lub eksport źródeł już trwa")
            return
        client = self.client
        if client is None:
            self.announcer.say("Silnik nie działa, nie mogę wyeksportować kanałów YouTube")
            return

        with wx.FileDialog(
            self,
            message="Eksportuj subskrypcje YouTube",
            defaultFile="Kanaly YouTube AMC.csv",
            wildcard=YOUTUBE_EXPORT_WILDCARD,
            style=wx.FD_SAVE | wx.FD_OVERWRITE_PROMPT,
        ) as picker:
            if picker.ShowModal() != wx.ID_OK:
                return
            path = youtube_export_path_for_filter(
                picker.GetPath(),
                picker.GetFilterIndex(),
            )

        self._podcast_opml_pending = True
        started = "Eksportowanie kanałów YouTube"
        self.status_field.SetLabel(started)
        self.status_bar.show(started)

        def work() -> dict:
            result = client.export_youtube_subscriptions(path)
            return result if isinstance(result, dict) else {}

        def done(payload: dict) -> None:
            self._podcast_opml_pending = False
            exported = max(0, int(payload.get("exported") or 0))
            skipped = max(0, int(payload.get("skippedPlaylists") or 0))
            if str(payload.get("format") or "").casefold() == "opml":
                message = (
                    f"Wyeksportowano kanały i playlisty YouTube: {exported}. "
                    "Plik OPML jest przeznaczony do czytników RSS"
                )
            elif skipped:
                message = (
                    f"Wyeksportowano kanały YouTube: {exported}; "
                    f"pominięto playlisty: {skipped}. "
                    "Plik CSV można importować w NewPipe i FreeTube"
                )
            else:
                message = (
                    f"Wyeksportowano kanały YouTube: {exported}. "
                    "Plik CSV można importować w NewPipe i FreeTube"
                )
            self.status_field.SetLabel(message)
            self.status_bar.show(message)
            self.announcer.say(message)

        def failed(error: Exception) -> None:
            self._podcast_opml_pending = False
            message = (
                str(error)
                if isinstance(error, (HostError, HostUnavailable))
                else "Nie udało się wyeksportować kanałów YouTube"
            )
            self.status_field.SetLabel(message)
            self.status_bar.show(message)
            self.announcer.say(message)

        self.runner.submit("podcast-youtube-export", work, done, failed)

    def _refresh_podcasts(self, *, refresh_all: bool) -> None:
        """F5/Ctrl+F5: wspolny mechanizm odswiezania glownego AMC.

        Host C# pozostaje jedynym pisarzem ``podcasts.db``. Praca sieciowa
        biegnie w tle i nie zatrzymuje odtwarzania. Po odpowiedzi odczytujemy
        na nowo tylko widok, ktory uzytkownik nadal ma przed soba.
        """
        if self.navigator.active is not SessionId.PODCASTS:
            self.announcer.say(
                "Odświeżanie źródeł jest dostępne w sesji Podcasty i YouTube"
            )
            return
        if self._podcast_refresh_pending:
            self.announcer.say("Odświeżanie źródeł już trwa")
            return
        client = self.client
        if client is None:
            self.announcer.say("Silnik nie działa, nie mogę odświeżyć źródeł")
            return

        subscription_id = None if refresh_all else self._current_podcast_subscription_id()
        if not refresh_all and not subscription_id:
            self.announcer.say("Zaznacz podcast albo otwórz jego listę odcinków")
            return

        self._podcast_refresh_pending = True
        self.status_bar.show(
            "Odświeżanie wszystkich źródeł podcastów"
            if refresh_all
            else "Odświeżanie wybranego źródła podcastów"
        )

        def work() -> dict:
            result = client.refresh_podcasts(subscription_id)
            return result if isinstance(result, dict) else {}

        def done(payload: dict) -> None:
            self._podcast_refresh_pending = False
            requested = max(0, int(payload.get("requested") or 0))
            succeeded = max(0, int(payload.get("succeeded") or 0))
            added = max(0, int(payload.get("addedEpisodes") or 0))
            inbox = max(0, int(payload.get("inboxCount") or 0))
            message = (
                f"Odświeżono źródła: {succeeded} z {requested}. "
                f"Nowe teraz: {added}. W skrzynce: {inbox}."
            )
            self.status_bar.show(message)

            # Tak jak w glownym AMC wynik nie wyrywa uzytkownika z odtwarzacza
            # ani z innej sesji. Zmiany sa w bazie i pojawia sie przy kolejnym
            # otwarciu listy.
            if self.navigator.active is not SessionId.PODCASTS:
                return
            state = self.navigator.sessions[SessionId.PODCASTS]
            if state.view is not View.LIST:
                return
            preferred_id = state.model.selected_id
            if state.library_view is LibraryView.PODCAST_LIBRARY:
                self._open_podcast_library(
                    preferred_id=preferred_id,
                    completion_message=message,
                )
            elif state.library_view is LibraryView.PODCAST_EPISODES:
                current_id = state.library_playlist_id
                if current_id:
                    self._open_podcast_view(
                        OpenPodcastView(
                            subscription_id=current_id,
                            preferred_id=preferred_id,
                        ),
                        completion_message=message,
                    )
            elif state.library_view in (
                LibraryView.PODCAST_FAVORITES,
                LibraryView.PODCAST_HISTORY,
                LibraryView.PODCAST_QUEUE,
                LibraryView.PODCAST_INBOX,
                LibraryView.PODCAST_DOWNLOADS,
            ):
                self._open_podcast_aggregate(
                    OpenPodcastAggregateView(
                        state.library_view,
                        preferred_id=preferred_id,
                    ),
                    completion_message=message,
                )

        def failed(error: Exception) -> None:
            self._podcast_refresh_pending = False
            message = (
                str(error)
                if isinstance(error, (HostError, HostUnavailable))
                else "Nie udało się odświeżyć źródeł podcastów"
            )
            self.status_bar.show(message)
            if self.navigator.active is SessionId.PODCASTS:
                self.announcer.say(message)

        self.runner.submit("podcast-refresh", work, done, failed)

    def _download_podcast_episodes(self) -> None:
        """Ctrl+D: pobierz jeden lub wiele zaznaczonych odcinków."""
        if self.navigator.active is not SessionId.PODCASTS:
            self.announcer.say("Pobieranie odcinków jest dostępne w sesji Podcasty i YouTube")
            return
        if self._podcast_download_pending:
            self.announcer.say("Pobieranie odcinków już trwa")
            return
        client = self.client
        if client is None:
            self.announcer.say("Silnik nie działa, nie mogę pobrać odcinków")
            return

        rows: list[Row] = []
        seen: set[str] = set()
        for row in self._selected_action_rows():
            if row.kind != "episode" or row.item_id in seen:
                continue
            seen.add(row.item_id)
            rows.append(row)
        if not rows:
            self.announcer.say("Zaznacz co najmniej jeden odcinek podcastu")
            return

        episode_ids = [row.item_id for row in rows]
        preferred_id = rows[0].item_id
        self._podcast_download_pending = True
        started = (
            "Pobieranie odcinka"
            if len(rows) == 1
            else f"Pobieranie odcinków: {len(rows)}"
        )
        self.announcer.say(started)

        def work() -> dict:
            result = client.download_podcast_episodes(episode_ids)
            return result if isinstance(result, dict) else {}

        def done(payload: dict) -> None:
            self._podcast_download_pending = False
            requested = max(0, int(payload.get("requested") or len(rows)))
            downloaded = max(0, int(payload.get("downloaded") or 0))
            already = max(0, int(payload.get("alreadyDownloaded") or 0))
            failed_count = max(0, int(payload.get("failed") or 0))
            if requested > 1:
                parts = [f"Pobrano: {downloaded}"]
                if already:
                    parts.append(f"już pobrane: {already}")
                if failed_count:
                    parts.append(f"niepowodzenia: {failed_count}")
                message = ". ".join(parts)
            elif downloaded:
                message = f"Pobrano odcinek: {rows[0].title}"
            elif already:
                message = f"Odcinek jest już pobrany: {rows[0].title}"
            else:
                detail = str(payload.get("firstFailure") or "").strip()
                message = (
                    f"Nie można pobrać odcinka: {detail}"
                    if detail
                    else "Nie udało się pobrać odcinka"
                )
            self.status_field.SetLabel(message)
            self.status_bar.show(message)
            if not self._reload_podcast_list_after_download(preferred_id, message):
                self.announcer.say(message)

        def failed(error: Exception) -> None:
            self._podcast_download_pending = False
            message = (
                str(error)
                if isinstance(error, (HostError, HostUnavailable))
                else "Nie udało się pobrać odcinków"
            )
            self.status_field.SetLabel(message)
            self.status_bar.show(message)
            self.announcer.say(message)

        self.runner.submit("podcast-download", work, done, failed)

    def _save_podcast_episode_as(self) -> None:
        """Ctrl+S: jedna jawnie nazwana kopia, bez zmiany pola Pobrane."""
        if self.navigator.active is not SessionId.PODCASTS:
            self.announcer.say("Zapisywanie odcinka jest dostępne w sesji Podcasty i YouTube")
            return
        if self._podcast_download_pending:
            self.announcer.say("Pobieranie odcinków już trwa")
            return
        client = self.client
        if client is None:
            self.announcer.say("Silnik nie działa, nie mogę zapisać odcinka")
            return

        rows = [row for row in self._selected_action_rows() if row.kind == "episode"]
        if len(rows) != 1:
            self.announcer.say(
                "Zapisz jako działa dla jednego odcinka. Wybierz jeden odcinek i spróbuj ponownie"
            )
            return
        row = rows[0]
        self._podcast_download_pending = True
        self.status_bar.show("Przygotowywanie zapisu odcinka")

        def prepare() -> dict:
            result = client.podcast_save_as_info(row.item_id)
            return result if isinstance(result, dict) else {}

        def prepared(info: dict) -> None:
            suggested = str(
                info.get("suggestedFileName") or "Odcinek podcastu.mp3"
            ).strip()
            initial_folder = str(info.get("initialFolder") or "").strip()
            extension = Path(suggested).suffix or ".mp3"
            with wx.FileDialog(
                self,
                message="Zapisz odcinek podcastu jako",
                defaultDir=initial_folder,
                defaultFile=suggested,
                wildcard=(
                    f"Plik audio (*{extension})|*{extension}|"
                    "Wszystkie pliki (*.*)|*.*"
                ),
                style=wx.FD_SAVE | wx.FD_OVERWRITE_PROMPT,
            ) as picker:
                if picker.ShowModal() != wx.ID_OK:
                    self._podcast_download_pending = False
                    return
                destination = picker.GetPath()

            started = f"Pobieranie odcinka: {row.title}"
            self.status_field.SetLabel(started)
            self.status_bar.show(started)
            self.announcer.say("Pobieranie odcinka")

            def work() -> dict:
                result = client.save_podcast_episode_as(row.item_id, destination)
                return result if isinstance(result, dict) else {}

            def done(payload: dict) -> None:
                self._podcast_download_pending = False
                file_name = str(
                    payload.get("fileName") or Path(destination).name
                ).strip()
                message = f"Zapisano odcinek jako: {file_name}"
                self.status_field.SetLabel(message)
                self.status_bar.show(message)
                self.announcer.say(message)

            self.runner.submit("podcast-save-as", work, done, failed)

        def failed(error: Exception) -> None:
            self._podcast_download_pending = False
            message = (
                str(error)
                if isinstance(error, (HostError, HostUnavailable))
                else "Nie udało się zapisać odcinka"
            )
            self.status_field.SetLabel(message)
            self.status_bar.show(message)
            self.announcer.say(message)

        self.runner.submit("podcast-save-as-info", prepare, prepared, failed)

    def _show_podcast_description(self) -> None:
        """Alt+D: description first, then metadata, in a native text field."""
        if self.navigator.active is not SessionId.PODCASTS:
            self.announcer.say(
                "Pełny opis jest dostępny dla podcastu albo odcinka"
            )
            return
        row = self.navigator.sessions[SessionId.PODCASTS].model.selected_row
        if row is None or row.kind not in ("podcast", "episode"):
            self.announcer.say(
                "Pełny opis jest dostępny dla podcastu albo odcinka"
            )
            return
        requested_id = row.item_id
        requested_kind = row.kind

        def work() -> PodcastDescription | None:
            return self.podcasts.description(requested_id, requested_kind)

        def done(information: PodcastDescription | None) -> None:
            state = self.navigator.sessions[SessionId.PODCASTS]
            current = state.model.selected_row
            if (
                self.navigator.active is not SessionId.PODCASTS
                or current is None
                or current.item_id != requested_id
            ):
                return
            if information is None:
                self.announcer.say(
                    "Ten podcast nie zawiera opisu"
                    if requested_kind == "podcast"
                    else "Ten odcinek nie zawiera opisu"
                )
                return

            def copy_text(text: str) -> bool:
                copied = self._to_clipboard(text)
                if copied:
                    self.announcer.say("Skopiowano całą treść")
                return copied

            with PodcastDescriptionDialog(self, information, copy_text) as dialog:
                dialog.ShowModal()

        def failed(error: Exception) -> None:
            message = (
                str(error)
                if isinstance(error, PodcastProfileError)
                else "Nie udało się odczytać opisu"
            )
            self.announcer.say(message)

        self.runner.submit("podcast-description", work, done, failed)

    def _go_to_related_podcast(self) -> None:
        """Open the selected episode's parent and keep focus on the episode."""
        if self.navigator.active is not SessionId.PODCASTS:
            self.announcer.say(
                "Dla tego elementu nie znaleziono podcastu w Bibliotece"
            )
            return
        row = self.navigator.sessions[SessionId.PODCASTS].model.selected_row
        if row is None or row.kind != "episode":
            self.announcer.say(
                "Dla tego elementu nie znaleziono podcastu w Bibliotece"
            )
            return
        episode_id = row.item_id

        def work():
            return self.podcasts.related_podcast(episode_id)

        def done(subscription) -> None:
            state = self.navigator.sessions[SessionId.PODCASTS]
            current = state.model.selected_row
            if (
                self.navigator.active is not SessionId.PODCASTS
                or current is None
                or current.item_id != episode_id
            ):
                return
            if subscription is None:
                self.announcer.say(
                    "Dla tego elementu nie znaleziono podcastu w Bibliotece"
                )
                return
            state.library_return_id = subscription.subscription_id
            self._open_podcast_view(OpenPodcastView(
                subscription_id=subscription.subscription_id,
                subscription_title=subscription.title,
                preferred_id=episode_id,
            ))

        def failed(error: Exception) -> None:
            self.announcer.say(
                str(error)
                if isinstance(error, PodcastProfileError)
                else "Nie udało się odnaleźć podcastu tego odcinka"
            )

        self.runner.submit("podcast-go-to-related", work, done, failed)

    def _toggle_podcast_favorite(self) -> None:
        """Ctrl+Shift+U: jeden wspólny stan dla całego zaznaczenia."""
        if self.navigator.active is not SessionId.PODCASTS:
            self.announcer.say(
                "Ulubione podcasty są dostępne w sesji Podcasty i YouTube"
            )
            return
        if getattr(self, "_podcast_favorite_pending", False):
            self.announcer.say("Zmiana stanu ulubionych już trwa")
            return
        client = self.client
        if client is None:
            self.announcer.say("Silnik nie działa, nie mogę zmienić ulubionych")
            return

        rows: list[Row] = []
        seen: set[tuple[str, str]] = set()
        for row in self._selected_action_rows():
            if row.kind not in ("podcast", "episode") or not row.item_id:
                continue
            key = (row.kind, row.item_id)
            if key in seen:
                continue
            seen.add(key)
            rows.append(row)
        if not rows:
            self.announcer.say("Zaznacz podcast albo odcinek")
            return

        subscription_ids = [row.item_id for row in rows if row.kind == "podcast"]
        episode_ids = [row.item_id for row in rows if row.kind == "episode"]
        preferred_id = rows[0].item_id
        self._podcast_favorite_pending = True

        def work() -> dict:
            result = client.toggle_podcast_favorites(
                subscription_ids,
                episode_ids,
            )
            return result if isinstance(result, dict) else {}

        def done(payload: dict) -> None:
            self._podcast_favorite_pending = False
            favorite = bool(payload.get("favorite"))
            label = rows[0].title if len(rows) == 1 else format_item_count(len(rows))
            message = (
                f"Dodano do ulubionych: {label}"
                if favorite
                else f"Usunięto z ulubionych: {label}"
            )
            self.status_field.SetLabel(message)
            self.status_bar.show(message)

            if self.navigator.active is not SessionId.PODCASTS:
                self.announcer.say(message)
                return
            state = self.navigator.sessions[SessionId.PODCASTS]
            if state.view is not View.LIST:
                self.announcer.say(message)
                return
            if state.library_view is LibraryView.PODCAST_LIBRARY:
                self._open_podcast_library(
                    preferred_id=preferred_id,
                    completion_message=message,
                )
                return
            if not self._reload_podcast_list_after_download(preferred_id, message):
                self.announcer.say(message)

        def failed(error: Exception) -> None:
            self._podcast_favorite_pending = False
            message = (
                str(error)
                if isinstance(error, (HostError, HostUnavailable))
                else "Nie udało się zmienić stanu ulubionych"
            )
            self.status_field.SetLabel(message)
            self.status_bar.show(message)
            self.announcer.say(message)

        self.runner.submit("podcast-favorite", work, done, failed)

    def _reload_podcast_list_after_download(
        self, preferred_id: str, completion_message: str
    ) -> bool:
        """Odśwież tylko nadal otwartą listę, bez wyrywania z odtwarzacza."""
        if self.navigator.active is not SessionId.PODCASTS:
            return False
        state = self.navigator.sessions[SessionId.PODCASTS]
        if state.view is not View.LIST:
            return False
        if state.library_view is LibraryView.PODCAST_EPISODES:
            if state.library_playlist_id:
                self._open_podcast_view(
                    OpenPodcastView(
                        subscription_id=state.library_playlist_id,
                        preferred_id=preferred_id,
                    ),
                    completion_message=completion_message,
                )
                return True
        elif state.library_view in (
            LibraryView.PODCAST_FAVORITES,
            LibraryView.PODCAST_HISTORY,
            LibraryView.PODCAST_QUEUE,
            LibraryView.PODCAST_INBOX,
            LibraryView.PODCAST_DOWNLOADS,
        ):
            self._open_podcast_aggregate(
                OpenPodcastAggregateView(
                    state.library_view,
                    preferred_id=preferred_id,
                ),
                completion_message=completion_message,
            )
            return True
        return False

    def _begin_transient_preview(self) -> None:
        if self._transient_preview_return is None:
            self._transient_preview_return = self.navigator.capture_transient_navigation()

    def _restore_transient_preview(
        self, *, announce: bool = True, force: bool = False
    ) -> bool:
        snapshot = getattr(self, "_transient_preview_return", None)
        if snapshot is None:
            return False
        current = self.navigator.session.library_view
        if not force and current not in (
            LibraryView.ACTIVE_RADIO_RECORDINGS,
            LibraryView.RECORDED_RADIO_FILES,
            LibraryView.RADIO_RECORDING_SCHEDULES,
            LibraryView.PODCAST_INBOX,
        ):
            return False
        self._transient_preview_return = None
        events = self.navigator.restore_transient_navigation(snapshot)
        if announce:
            self._run(events)
        else:
            self._sync_views()
        return True

    def _announce_after_native_list_update(self, text: str) -> None:
        """Daj SysListView32 zakonczyc zdarzenia pustki, potem podaj wynik.

        Usuniecie ostatniego wiersza generuje w Windows osobne zdarzenie
        dostepnosciowe. Samo ``CallAfter`` bywa za wczesne: NVDA potrafi wtedy
        dopisac PO naszym komunikacie systemowe ``pusto, nieznane``. Krotki
        jednorazowy timer nie blokuje GUI i sprawia, ze koncowa, pozostajaca
        informacja jest jednoznaczna: ``Nagrywane, zero elementow``.
        """
        call_later = getattr(wx, "CallLater", None)
        if callable(call_later):
            call_later(90, self.announcer.say, text)
        else:
            # Atrapy wx w testach bez CallLater zachowuja dotychczasowa droge.
            wx.CallAfter(self.announcer.say, text)

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

    def _copy_name(self) -> None:
        """Ctrl+C: nazwy zaznaczonych elementow, po jednej w wierszu.

        Odpowiednik ``CopyActionItemName`` (MainWindow.xaml.cs:24755): pelne
        AMC czyta ``SelectedItems``, zachowuje kolejnosc listy i laczy nazwy
        przez ``Environment.NewLine``.
        """
        rows = [row for row in self._selected_action_rows() if row.title]
        if not rows:
            self.announcer.say("Nie ma czego skopiować")
            return
        self._clear_pending_external_moves()
        if self._to_clipboard(os.linesep.join(row.title for row in rows)):
            self.announcer.say(
                COPIED_NAME_MESSAGE
                if len(rows) == 1
                else f"Skopiowano nazwy: {format_item_count(len(rows))}"
            )

    def _selected_action_rows(self) -> list[Row]:
        """Wiersze dla polecenia: natywny wielokrotny wybor albo kursor.

        ``ListModel`` celowo nadal przechowuje jedno ID, bo sluzy ono do
        przywracania fokusu po zmianie widoku. Zakres zaznaczony Shiftem/Ctrl
        zyje w kontrolce i tylko stamtad moze zostac odczytany bez utraty.
        """
        try:
            rows = self._active_list().selected_rows()
        except (AttributeError, TypeError):
            # Testy czystej logiki oraz widok odtwarzacza nie musza miec
            # zbudowanej natywnej kontrolki.
            rows = []
        if rows:
            return rows
        row = self.navigator.session.model.selected_row
        return [] if row is None else [row]

    def _clear_pending_external_moves(self) -> None:
        pending = getattr(self, "pending_external_moves", None)
        if pending is not None:
            pending.clear()

    def _announce_quick_information(self) -> None:
        """Parametry zaznaczonego elementu, nie aktualnie odtwarzanego.

        Cache i uzupełnianie przez silnik działają poza wątkiem GUI;
        wspólny formatter Core składa napis, bramka odrzuca spóźniony wynik.
        """
        client = self.client
        row = self.navigator.session.model.selected_row
        plan = quick_info_plan(
            row,
            session=self.navigator.active.value,
            view=self.navigator.view.value,
            alive=self._window_alive,
        )
        if plan.message is not None:
            self.announcer.say(plan.message)
            return
        if plan.request is None or plan.guard is None:
            return
        if client is None:
            # Bez hosta nie ma POMIARU. Jedno uczciwe zdanie zamiast ciszy,
            # ktora wygladalaby jak ten sam martwy klawisz.
            self.announcer.say(HOST_ERROR_MESSAGE)
            return

        guard = plan.guard
        request = plan.request
        layout = self.layout
        assert row is not None

        def work() -> object:
            enriched = dict(request)
            enriched.update(read_cached_information(layout, row, guard.session))
            return client.call(QUICK_INFO_OP, enriched, timeout=12.0)

        def answer(payload: object) -> None:
            quick_info_reply(
                payload if isinstance(payload, dict) else {},
                guard=guard,
                item_id=self._selected_item_id(),
                session=self.navigator.active.value,
                view=self.navigator.view.value,
                say=self.announcer.say,
            )

        def failed(_error: Exception) -> None:
            quick_info_failure(
                error=_error,
                guard=guard,
                item_id=self._selected_item_id(),
                session=self.navigator.active.value,
                view=self.navigator.view.value,
                say=self.announcer.say,
            )

        # Budzet hosta to 5 s na plik i 6 s na strumien (cs:5493, cs:5522),
        # wiec czekamy odrobine dluzej, zeby to silnik oddal wynik, a nie my
        # zglosili falszywa awarie tuz przed nim.
        self.runner.submit(
            QUICK_INFO_STREAM,
            work,
            answer,
            failed,
        )

    def _selected_item_id(self) -> str | None:
        row = self.navigator.session.model.selected_row
        return None if row is None else row.item_id

    def _copy_address(self) -> None:
        """Ctrl+Shift+C: adresy albo PRAWDZIWE PLIKI calego zaznaczenia.

        Odpowiednik ``CopyItemLocations`` (MainWindow.xaml.cs:24744-24814).
        Tu wlasnie trafil adres, ktory wczesniej czytnik wymawial przy KAZDYM
        wierszu -- funkcja nie znika, zmienia sie moment jej uzycia.

        ZGLOSZENIE, ktore to naprawia: komunikat mowil o kopiowaniu pliku,
        a wklejenie w Total Commanderze nic nie robilo, bo schowek dostawal
        TYLKO tekst sciezki. Oryginal ustawia DWA formaty naraz
        (``MainWindow.xaml.cs:24781``):

            data.SetData(DataFormats.UnicodeText, string.Join(NewLine, localPaths));
            data.SetFileDropList(fileDropList);

        czyli ``CF_HDROP`` ORAZ tekst. Wiec dla lokalnego, ISTNIEJACEGO pliku
        lub folderu robimy to samo: ``wx.FileDataObject`` + ``wx.TextDataObject``
        w ``wx.DataObjectComposite``.

        Slowa komunikatu nadal opisuja DANE, nie zamiar -- patrz
        ``copied_address_message``.
        """
        rows = self._selected_action_rows()
        if not rows:
            self.announcer.say("Nie ma czego skopiować")
            return
        if any(not row.address for row in rows):
            self.announcer.say(
                "Ten element nie ma zapisanego adresu"
                if len(rows) == 1
                else "Co najmniej jeden zaznaczony element nie ma zapisanego adresu"
            )
            return

        entries: list[tuple[Row, str, str | None]] = []
        for row in rows:
            drop = self._file_drop_path(row.address)
            # Tekst istniejacego pliku dostaje sprowadzona zwykla sciezke;
            # pozostale elementy zachowuja swoj bezposredni adres.
            entries.append((row, drop or row.address, drop))

        file_paths: list[str] = []
        seen_paths: set[str] = set()
        for _row, _text, path in entries:
            if path is None:
                continue
            key = os.path.normcase(path).casefold()
            if key not in seen_paths:
                seen_paths.add(key)
                file_paths.append(path)

        self._clear_pending_external_moves()
        text = os.linesep.join(entry[1] for entry in entries)
        if not self._to_clipboard(text, file_paths=file_paths or None):
            return

        if len(rows) == 1:
            self.announcer.say(copied_address_message(
                rows[0], file_copied=bool(file_paths)))
        elif all(entry[2] is not None for entry in entries):
            self.announcer.say(
                f"Skopiowano pliki i pełne ścieżki: {format_file_count(len(file_paths))}"
            )
        elif file_paths:
            address_count = sum(entry[2] is None for entry in entries)
            self.announcer.say(
                f"Skopiowano pliki: {len(file_paths)}; adresy: {address_count}"
            )
        elif all(row.kind == "station" for row in rows):
            self.announcer.say(
                f"Skopiowano bezpośrednie adresy: {format_item_count(len(rows))}"
            )
        else:
            self.announcer.say(f"Skopiowano adresy: {format_item_count(len(rows))}")

    @staticmethod
    def _file_drop_path(address: str) -> str | None:
        """Sciezka do file dropu albo ``None``, gdy go nie wolno ustawic.

        DWA warunki, oba z oryginalu i oba konieczne:

        1. ``TryGetLocalPath`` (``MainWindow.xaml.cs:5378``) -- czy to w ogole
           sciezka lokalna. Adres strumienia radiowego nie jest, wiec stacja
           dalej dostaje sam tekst i swoj dotychczasowy komunikat.
        2. ISTNIENIE na dysku. Oryginal sprawdza je w dwoch miejscach i w dwoch
           wariantach: ``CopySearchResultLocations`` (:24835) dokłada
           ``File.Exists``, a ``RadioPresetsWindow.xaml.cs:156-158`` pyta
           ``File.Exists(path) || Directory.Exists(path)``, bo wpis moze
           wskazywac FOLDER. Nasza lista pokazuje i pliki, i foldery, wiec
           bierzemy wariant szerszy -- jedyny, ktory nie klamie o folderze.

        Nieistniejace zrodlo celowo NIE dostaje file dropu: Windows i tak
        odmowilby wklejenia, a my nie moglibysmy ogłosic skopiowanego pliku
        bez powtorzenia tego samego zgloszenia.
        """
        if not is_local_path(address):
            return None
        path = local_path_from_source(address)
        return path if os.path.exists(path) else None

    def _cut_file(self) -> None:
        """Ctrl+X: zaznaczone pliki GOTOWE DO PRZENIESIENIA poza AMC.

        Port ``CutLocalFilesForExternalMove`` (``MainWindow.xaml.cs:25277``).
        Michal uzywa tego w dzialajacym AMC i prosil o zgodne zachowanie.

        CZEGO TA FUNKCJA NIE ROBI -- i to jest regula oryginalu, nie nasze
        uproszczenie: SAMA NIE USUWA PLIKU. Oryginal tylko zapisuje schowek i
        zapamietuje sciezki w ``_pendingExternalMoves`` (:25306-25309);
        skasowanie zrodla wykonuje SHELL przy wklejeniu, a
        ``ReconcileCompletedExternalMoves`` (:25315) jedynie ZAUWAZA, ze plik
        zniknal. Gdybysmy usuwali sami, nieudane wklejenie skasowaloby nagranie.

        Warunki sa WEZSZE niz przy kopiowaniu i tak samo jest w C#:
        tylko ISTNIEJACY PLIK (``File.Exists``, :25289) -- folder sie nie
        kwalifikuje, choc do KOPIOWANIA jak najbardziej.
        """
        rows = self._selected_action_rows()
        if not rows:
            self.announcer.say("Brak pliku do wycięcia")
            return
        # Widok zakladek: wiersz wskazuje ``bookmark:<id>``, czyli ani plik,
        # ani sciezke (:25280-25284).
        if self.navigator.session.library_view is LibraryView.ALL_BOOKMARKS:
            self.announcer.say("Wycinanie plików nie działa na liście zakładek")
            return
        entries = [(row, self._cut_file_path(row.address)) for row in rows]
        if any(path is None for _row, path in entries):
            self.announcer.say(
                "Wycinanie jest dostępne tylko dla istniejących plików lokalnych")
            return
        paths: list[str] = []
        seen_paths: set[str] = set()
        for _row, path in entries:
            assert path is not None
            key = os.path.normcase(path).casefold()
            if key not in seen_paths:
                seen_paths.add(key)
                paths.append(path)
        file_arguments = (
            {"file_path": paths[0]}
            if len(paths) == 1
            else {"file_paths": paths}
        )
        if not self._to_clipboard(
            os.linesep.join(paths),
            preferred_drop_effect=DROPEFFECT_MOVE,
            **file_arguments,
        ):
            return
        # Zapamietujemy OCZEKUJACE przeniesienie, tak jak oryginal. Samo
        # zapamietanie nic nie usuwa -- sluzy pozniejszemu rozpoznaniu, ze
        # plik juz nie lezy pod stara sciezka.
        self.pending_external_moves.clear()
        for row, path in entries:
            assert path is not None
            self.pending_external_moves[row.item_id] = path
        self.announcer.say(
            "Plik gotowy do przeniesienia. Wklej go w folderze docelowym"
            if len(paths) == 1
            else f"Pliki gotowe do przeniesienia: {format_file_count(len(paths))}. "
                 "Wklej je w folderze docelowym"
        )

    @staticmethod
    def _cut_file_path(address: str) -> str | None:
        """Sciezka do wyciecia albo ``None``.

        Rozni sie od ``_file_drop_path`` JEDNYM warunkiem i jest to roznica z
        oryginalu: wycinanie wymaga ``File.Exists`` (:25289), wiec FOLDER sie
        nie kwalifikuje. Kopiowanie bierze wariant szerszy (plik lub folder),
        bo ``RadioPresetsWindow.xaml.cs:156-158`` pyta o oba.
        """
        if not address or not is_local_path(address):
            return None
        path = local_path_from_source(address)
        return path if os.path.isfile(path) else None

    def _to_clipboard(
        self,
        text: str,
        *,
        file_path: str | None = None,
        file_paths: list[str] | None = None,
        preferred_drop_effect: int | None = None,
    ) -> bool:
        """Zapis do schowka Windows. Porazke MOWIMY, nie udajemy sukcesu.

        Schowek bywa chwilowo zajety przez inny proces -- odpowiednik
        ``ClipboardRetry`` z AMC, ktory tez zwraca komunikat bledu zamiast
        komunikatu sukcesu.

        ``file_path``/``file_paths`` ustawiaja DODATKOWO format plikowy (``wx.FileDataObject``,
        czyli ``CF_HDROP``) obok tekstu -- jak ``SetFileDropList`` w oryginale.
        Bez niego zostaje JEDEN format tekstowy, dokladnie jak dotad; dlatego
        ``Ctrl+C`` (nazwa) i stacje nie zmieniaja zachowania ani o jotę.
        """
        try:
            if not wx.TheClipboard.Open():
                self.announcer.say("Schowek jest zajęty, spróbuj ponownie")
                return False
            try:
                paths = file_paths if file_paths is not None else (
                    [file_path] if file_path is not None else [])
                if not paths:
                    data = wx.TextDataObject(text)
                else:
                    # Kolejnosc jak w oryginale: najpierw tekst (``UnicodeText``),
                    # potem file drop. Preferowany jest format PLIKOWY -- o niego
                    # chodzi w zgloszeniu, a odbiorcy tekstowi (edytor, pole
                    # wyszukiwania) i tak wezma galaz tekstowa.
                    data = wx.DataObjectComposite()
                    data.Add(wx.TextDataObject(text))
                    files = wx.FileDataObject()
                    for path in paths:
                        files.AddFile(path)
                    data.Add(files, True)
                    if preferred_drop_effect is not None:
                        # ``Preferred DropEffect``: TRZECI format, ktorym shell
                        # rozpoznaje WYCIECIE. ``SetData("Preferred DropEffect",
                        # BitConverter.GetBytes(2))`` w C# to 4 bajty little
                        # endian -- ``wx.CustomDataObject`` przyjmuje je wprost.
                        effect = wx.CustomDataObject(wx.DataFormat("Preferred DropEffect"))
                        effect.SetData(
                            preferred_drop_effect.to_bytes(4, "little", signed=False))
                        data.Add(effect)
                if not wx.TheClipboard.SetData(data):
                    # ``wxClipboard::SetData`` zwraca BOOL i nie rzuca wyjatku,
                    # gdy schowek odmowi przyjecia obiektu. Bez tej bramki
                    # porazka konczyla sie komunikatem sukcesu przy pustym
                    # schowku -- tym samym zgloszeniem, tylko cichszym.
                    self.announcer.say("Nie udało się skopiować do schowka")
                    return False
                # ``Flush`` to TRWALOSC po zamknieciu naszego procesu, a nie
                # sam zapis: dane juz LEZA w schowku i wklejenie zadziala.
                # Dlatego jego ``False`` NIE jest bledem kopiowania -- inaczej
                # program klamalby w druga strone.
                wx.TheClipboard.Flush()
            finally:
                wx.TheClipboard.Close()
        except Exception as error:
            self.announcer.say(f"Nie udało się skopiować: {error}")
            return False
        return True

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

        self._pending_playback_session = SessionId.FILES

        def work() -> dict:
            return client.play_file(
                intent.path,
                volume=self.options.volume,
                rate=self.options.rate,
                title=intent.title,
                # Skok zakladki jedzie Z TYM SAMYM zadaniem, nie osobnym seek.
                position_seconds=intent.position_seconds,
            )

        def done(_payload: dict) -> None:
            # Host ma JEDNO wyjscie: zapamietujemy, czyje granie wlasnie
            # zaczal, zeby pauza po wyjsciu nie ruszyla cudzej sesji.
            self._playing_session = SessionId.FILES
            self._refresh_status()

        def failed(error: Exception) -> None:
            if self._pending_playback_session is SessionId.FILES:
                self._pending_playback_session = None
            self._run(self.navigator.note_playback_failed(
                f"Nie udalo sie odtworzyc: {error}", SessionId.FILES
            ))

        self.runner.submit("playback", work, done, failed)

    def _play_media(self, intent: PlayMedia) -> None:
        """Play an episode/YouTube item through the existing C# audio engine."""
        client = self.client
        if client is None:
            self.announcer.say("Silnik nie działa, nie mogę odtworzyć")
            return

        self._pending_playback_session = SessionId.PODCASTS

        def work() -> dict:
            return client.play_media(
                intent.source,
                item_id=intent.item_id,
                title=intent.title,
                volume=self.options.volume,
                rate=self.options.rate,
                position_seconds=intent.position_seconds,
            )

        def done(_payload: dict) -> None:
            self._playing_session = SessionId.PODCASTS
            self._refresh_status()

        def failed(error: Exception) -> None:
            if self._pending_playback_session is SessionId.PODCASTS:
                self._pending_playback_session = None
            self._run(self.navigator.note_playback_failed(
                f"Nie udało się odtworzyć: {error}", SessionId.PODCASTS
            ))

        self.runner.submit("playback", work, done, failed)

    def _add_bookmark(self) -> None:
        """B w odtwarzaczu: szybka zakladka przez waski zapis hosta C#."""
        if self.navigator.active is not SessionId.FILES:
            self.announcer.say(
                "Zakładki w tej sesji nie są jeszcze dostępne w tej wersji"
            )
            return
        state = self.navigator.session
        item_id = state.current_material_id or state.pending_material_id
        if not item_id:
            self.announcer.say("Bieżący plik nie ma identyfikatora Biblioteki")
            return
        client = self.client
        if client is None:
            self.announcer.say("Silnik nie działa, nie mogę dodać zakładki")
            return

        def done(payload: dict) -> None:
            seconds = float((payload or {}).get("positionSeconds") or 0.0)
            position = format_duration(seconds)
            if (payload or {}).get("added"):
                self.announcer.say(f"Dodano zakładkę: {position}")
            else:
                self.announcer.say(f"Zakładka już istnieje: {position}")

        self.runner.submit(
            "bookmark-write",
            lambda: client.add_bookmark(
                item_id=item_id,
                item_title=state.now_playing_title or item_id,
            ),
            done,
            lambda error: self.announcer.say(f"Nie można dodać zakładki: {error}"),
        )

    # ------------------------------------------------------ fragmenty audio

    def _request_audio_clip_context(
        self,
        on_ready: Callable[[AudioClipContext, AudioClipSelection], None],
    ) -> None:
        """Pobierz pozycje i tozsamosc z hosta, nie ze starego ticka GUI."""
        client = self.client
        if client is None:
            self.announcer.say("Silnik nie działa, wycinanie fragmentu jest niedostępne")
            return

        def work() -> tuple[dict, bool]:
            payload = client.status() or {}
            source = payload.get("source")
            # File.Exists dla dysku sieciowego/chmury moze czekac. Nie robimy
            # tego na watku GUI, bo podczas czekania NVDA stracilby cale okno.
            exists = (
                Path(source).is_file()
                if isinstance(source, str) and source.strip()
                else False
            )
            return payload, exists

        def done(result: tuple[dict, bool]) -> None:
            payload, source_exists = result
            self._last_status = payload or {}
            context, error = clip_context_from_status(
                payload,
                files_session_active=self.navigator.active is SessionId.FILES,
                player_view_active=self.navigator.view is View.PLAYER,
                path_exists=lambda _path: source_exists,
            )
            if context is None:
                self.announcer.say(error or "Bieżący element nie jest lokalnym plikiem multimedialnym")
                return
            selection = restore_clip_selection(
                self.state.clip_selections,
                context.item_id,
                context.source_path,
                context.duration_seconds,
            )
            on_ready(context, selection)

        self.runner.submit(
            "audio-clip-context",
            work,
            done,
            lambda error: self.announcer.say(
                f"Nie można odczytać położenia fragmentu: {error}"
            ),
        )

    def _persist_audio_clip_selection(self, selection: AudioClipSelection) -> bool:
        previous = list(self.state.clip_selections)
        self.state.clip_selections = update_clip_selections(previous, selection)
        self.state.options = self.options.clamp()
        self.state.navigation = self.navigator.snapshot()
        try:
            self.store.save(self.state)
        except OSError as error:
            self.state.clip_selections = previous
            self.announcer.say(f"Nie mogę zapisać zaznaczenia fragmentu: {error}")
            return False
        return True

    def _mark_audio_clip(self, *, start: bool) -> None:
        def ready(context: AudioClipContext, selection: AudioClipSelection) -> None:
            if start:
                accepted = selection.set_start(
                    context.item_id,
                    context.source_path,
                    context.position_seconds,
                    context.duration_seconds,
                )
                if not accepted:
                    self.announcer.say(
                        "Początek fragmentu musi znajdować się przed jego końcem"
                    )
                    return
            else:
                accepted = selection.set_end(
                    context.item_id,
                    context.source_path,
                    context.position_seconds,
                    context.duration_seconds,
                )
                if not accepted:
                    self.announcer.say(
                        "Koniec fragmentu musi znajdować się po jego początku"
                    )
                    return
            if not self._persist_audio_clip_selection(selection):
                return

            if start:
                time = format_clip_time(selection.start_seconds or 0.0)
                text = f"Początek fragmentu: {time}"
                if selection.end_seconds is not None:
                    text += ". Długość: " + format_clip_time(
                        selection.end_seconds - (selection.start_seconds or 0.0)
                    )
            else:
                time = format_clip_time(selection.end_seconds or 0.0)
                text = f"Koniec fragmentu: {time}"
                if selection.start_seconds is not None:
                    text += ". Długość: " + format_clip_time(
                        (selection.end_seconds or 0.0) - selection.start_seconds
                    )
                else:
                    text += ". Ustaw początek klawiszem I"
            self.announcer.say(text)

        self._request_audio_clip_context(ready)

    def _clear_audio_clip_selection(self) -> None:
        def ready(context: AudioClipContext, selection: AudioClipSelection) -> None:
            if selection.start_seconds is None and selection.end_seconds is None:
                self.announcer.say("Ten plik nie ma zaznaczonego fragmentu")
                return
            # Zachowujemy tozsamosc do usuniecia rekordu. ``Clear`` modelu C#
            # zeruje ja, a WPF przekazuje potem osobno biezacy MediaItem.
            empty = AudioClipSelection(
                item_id=context.item_id,
                source_path=context.source_path,
            )
            if self._persist_audio_clip_selection(empty):
                self.announcer.say("Zaznaczenie fragmentu wyczyszczone")

        self._request_audio_clip_context(ready)

    def _seek_to_audio_clip_position(self, target: float, label: str) -> None:
        client = self.client
        if client is None:
            return

        def done(payload: dict) -> None:
            raw_position = (payload or {}).get("positionSeconds")
            position = (
                float(raw_position)
                if isinstance(raw_position, (int, float)) and not isinstance(raw_position, bool)
                else target
            )
            self._last_status = {**self._last_status, "positionSeconds": position}
            if self.messages.seek_messages and self.messages.speaks_routine:
                self.announcer.say(f"{label} fragmentu: {format_clip_time(position)}")

        self.runner.submit(
            "audio-clip-seek",
            lambda: client.seek_to_position(target),
            done,
            lambda error: self.announcer.say(f"Nie mogę przejść do granicy fragmentu: {error}"),
        )

    def _jump_to_audio_clip_boundary(self, *, end: bool) -> None:
        def ready(context: AudioClipContext, selection: AudioClipSelection) -> None:
            if not selection.matches(context.item_id, context.source_path):
                self.announcer.say("Ten plik nie ma zaznaczonego fragmentu")
                return
            position = selection.end_seconds if end else selection.start_seconds
            if position is None:
                self.announcer.say(
                    "Nie ustawiono końca fragmentu"
                    if end
                    else "Nie ustawiono początku fragmentu"
                )
                return
            self._seek_to_audio_clip_position(position, "Koniec" if end else "Początek")

        self._request_audio_clip_context(ready)

    def _jump_to_relative_audio_clip_boundary(self, *, direction: int) -> None:
        def ready(context: AudioClipContext, selection: AudioClipSelection) -> None:
            if not selection.matches(context.item_id, context.source_path):
                self.announcer.say("Ten plik nie ma zaznaczonego fragmentu")
                return
            position = selection.find_relative_boundary(
                context.position_seconds, direction
            )
            if position is None:
                self.announcer.say(
                    "Brak poprzedniej granicy fragmentu"
                    if direction < 0
                    else "Brak następnej granicy fragmentu"
                )
                return
            label = "Początek" if position == selection.start_seconds else "Koniec"
            self._seek_to_audio_clip_position(position, label)

        self._request_audio_clip_context(ready)

    def _export_audio_clip(self) -> None:
        if self._audio_clip_export_in_progress:
            self.announcer.say("Zapisywanie fragmentu już trwa")
            return
        if self._audio_clip_edit_in_progress:
            self.announcer.say(
                "Dopisywanie fragmentu już trwa"
                if self._audio_clip_edit_kind == "append"
                else "Usuwanie fragmentu już trwa"
            )
            return

        def ready(context: AudioClipContext, selection: AudioClipSelection) -> None:
            if not selection.is_complete:
                self.announcer.say(
                    "Zaznacz początek klawiszem I i koniec klawiszem O"
                )
                return
            client = self.client
            if client is None:
                return

            def capabilities_done(payload: dict) -> None:
                choices = format_choices_from_payload(payload)
                if not choices:
                    self.announcer.say("Brak dostępnego sposobu zapisu fragmentu")
                    return
                notice = str((payload or {}).get("notice") or "").strip()
                self._choose_audio_clip_destination(
                    context, selection, choices, notice
                )

            self.runner.submit(
                "audio-clip-capabilities",
                lambda: client.audio_clip_capabilities(context.source_path),
                capabilities_done,
                lambda error: self.announcer.say(
                    f"Nie można przygotować zapisu fragmentu: {error}"
                ),
            )

        self._request_audio_clip_context(ready)

    def _choose_audio_clip_destination(
        self,
        context: AudioClipContext,
        selection: AudioClipSelection,
        choices: list[AudioClipFormatChoice],
        notice: str,
    ) -> None:
        dialog = AudioClipExportDialog(
            self,
            context=context,
            selection=selection,
            choices=choices,
            notice=notice,
        )
        chosen: AudioClipFormatChoice | None = None
        try:
            dialog.format_choice.SetFocus()
            if dialog.ShowModal() == wx.ID_OK:
                chosen = dialog.selected_format
        finally:
            dialog.Destroy()
        if chosen is None:
            self._restore_focus_after_dialog()
            return

        wildcard = (
            f"Plik FLAC (*{chosen.extension})|*{chosen.extension}"
            if chosen.value == "flac"
            else f"Plik WAV (*{chosen.extension})|*{chosen.extension}"
            if chosen.value == "wav"
            else f"Oryginalny format (*{chosen.extension})|*{chosen.extension}"
        )
        default_name = suggested_clip_file_name(context.title, chosen.extension)
        with wx.FileDialog(
            self,
            "Zapisz zaznaczony fragment jako nowy plik",
            defaultDir=str(Path(context.source_path).parent),
            defaultFile=default_name,
            wildcard=wildcard,
            style=wx.FD_SAVE | wx.FD_OVERWRITE_PROMPT,
        ) as file_dialog:
            if file_dialog.ShowModal() != wx.ID_OK:
                self._restore_focus_after_dialog()
                return
            destination = file_dialog.GetPath()
        if not Path(destination).suffix:
            destination += chosen.extension
        self._start_audio_clip_export(context, selection, chosen, destination)

    def _start_audio_clip_export(
        self,
        context: AudioClipContext,
        selection: AudioClipSelection,
        chosen: AudioClipFormatChoice,
        destination: str,
    ) -> None:
        client = self.client
        if client is None or not selection.is_complete:
            return
        operation_id = uuid.uuid4().hex
        self._audio_clip_export_in_progress = True
        self._audio_clip_export_percent = 0
        self._audio_clip_export_operation_id = operation_id
        progress_text = f"Zapisywanie fragmentu: 0%. {Path(destination).name}"
        self.status_field.SetLabel(progress_text)
        self.status_bar.show(progress_text)
        self.announcer.say(f"Rozpoczęto zapisywanie fragmentu: {Path(destination).name}")

        def done(payload: dict) -> None:
            self._audio_clip_export_in_progress = False
            self._audio_clip_export_percent = 100
            self._audio_clip_export_operation_id = None
            name = str((payload or {}).get("name") or Path(destination).name)
            self.announcer.say(f"Fragment zapisany: {name}")
            self._restore_focus_after_dialog()

        def failed(error: Exception) -> None:
            self._audio_clip_export_in_progress = False
            self._audio_clip_export_percent = -1
            self._audio_clip_export_operation_id = None
            self.announcer.say(f"Nie udało się zapisać fragmentu: {error}")
            self._restore_focus_after_dialog()

        self.runner.submit(
            "audio-clip-export",
            lambda: client.export_audio_clip(
                source_path=context.source_path,
                destination_path=destination,
                start_seconds=selection.start_seconds or 0.0,
                end_seconds=selection.end_seconds or 0.0,
                format_value=chosen.value,
                operation_id=operation_id,
            ),
            done,
            failed,
        )

    def _append_audio_clip(self) -> None:
        if self._audio_clip_edit_in_progress:
            self.announcer.say(
                "Dopisywanie fragmentu już trwa"
                if self._audio_clip_edit_kind == "append"
                else "Usuwanie fragmentu już trwa"
            )
            return
        if self._audio_clip_export_in_progress:
            self.announcer.say("Zapisywanie fragmentu już trwa")
            return

        def ready(context: AudioClipContext, selection: AudioClipSelection) -> None:
            if not selection.is_complete:
                self.announcer.say(
                    "Zaznacz początek klawiszem I i koniec klawiszem O"
                )
                return
            client = self.client
            if client is None:
                return

            with wx.FileDialog(
                self,
                "Wybierz istniejący plik, na końcu którego dopisać fragment",
                defaultDir=str(Path(context.source_path).parent),
                wildcard=(
                    "Obsługiwane audio (*.wav;*.flac;*.mp3;*.m4a;*.aac;*.ogg;*.oga;*.opus)|"
                    "*.wav;*.flac;*.mp3;*.m4a;*.aac;*.ogg;*.oga;*.opus"
                ),
                style=wx.FD_OPEN | wx.FD_FILE_MUST_EXIST,
            ) as file_dialog:
                if file_dialog.ShowModal() != wx.ID_OK:
                    self._restore_focus_after_dialog()
                    return
                target_path = file_dialog.GetPath()
            self._restore_focus_after_dialog()

            def prepare() -> tuple[object, bool]:
                capabilities = client.audio_clip_append_capabilities(
                    context.source_path,
                    target_path,
                )
                keep_backup = load_keep_audio_edit_backups(
                    self.layout.state_json
                )
                return capabilities, keep_backup

            def prepared(result: tuple[object, bool]) -> None:
                capabilities, keep_backup = result
                if not isinstance(capabilities, dict):
                    self.announcer.say(
                        "Nie można sprawdzić możliwości dopisania fragmentu. Pliki nie zostały zmienione"
                    )
                    return
                if capabilities.get("available") is not True:
                    message = str(capabilities.get("message") or "").strip()
                    self.announcer.say(
                        message
                        or "Dopisanie fragmentu do tego pliku jest niedostępne"
                    )
                    return
                if capabilities.get("requiresLossyReencode") is True:
                    if not self._confirm_audio_clip_append(keep_backup=keep_backup):
                        return
                self._start_audio_clip_append(
                    context,
                    selection,
                    target_path=target_path,
                    keep_backup=keep_backup,
                )

            self.runner.submit(
                "audio-clip-append-preflight",
                prepare,
                prepared,
                lambda error: self.announcer.say(str(error)),
            )

        self._request_audio_clip_context(ready)

    def _confirm_audio_clip_append(self, *, keep_backup: bool) -> bool:
        dialog = wx.MessageDialog(
            self,
            append_clip_confirmation_text(keep_backup=keep_backup),
            "Ponowna kompresja pliku docelowego",
            wx.YES_NO | wx.NO_DEFAULT | wx.ICON_WARNING,
        )
        try:
            if hasattr(dialog, "SetName"):
                dialog.SetName("Potwierdzenie dopisania fragmentu")
            if hasattr(dialog, "SetYesNoLabels"):
                dialog.SetYesNoLabels(
                    "Tak, dopisz fragment",
                    "Nie, pozostaw plik",
                )
            confirmed = dialog.ShowModal() == wx.ID_YES
        finally:
            dialog.Destroy()
        self._restore_focus_after_dialog()
        if not confirmed:
            self.announcer.say("Dopisywanie fragmentu anulowane")
        return confirmed

    def _start_audio_clip_append(
        self,
        context: AudioClipContext,
        selection: AudioClipSelection,
        *,
        target_path: str,
        keep_backup: bool,
    ) -> None:
        client = self.client
        if client is None or not selection.is_complete:
            return
        operation_id = uuid.uuid4().hex
        self._audio_clip_edit_in_progress = True
        self._audio_clip_edit_kind = "append"
        self._audio_clip_edit_percent = 0
        self._audio_clip_edit_operation_id = operation_id
        target_name = Path(target_path).name
        progress_text = f"Dopisywanie fragmentu: 0%. {target_name}"
        self.status_field.SetLabel(progress_text)
        self.status_bar.show(progress_text)

        def done(payload: dict) -> None:
            self._audio_clip_edit_in_progress = False
            self._audio_clip_edit_kind = None
            self._audio_clip_edit_percent = 100
            self._audio_clip_edit_operation_id = None
            result = payload if isinstance(payload, dict) else {}
            backup_path = result.get("backupPath")
            backup_text = describe_backup_outcome(
                keep_backup,
                backup_path if isinstance(backup_path, str) else None,
            )
            if isinstance(backup_path, str) and backup_path.strip():
                backup_text += " " + Path(backup_path).name
            warning = result.get("reencodeWarning")
            warning_text = ""
            if isinstance(warning, str) and warning.strip():
                # Backend może dopisać do ostrzeżenia nazwę kopii. Los kopii
                # opisujemy niżej na podstawie osobnego, rzeczywistego pola.
                warning_text = warning.strip().split(" Poprzednia wersja", 1)[0]
            message = f"Fragment dopisany na końcu: {target_name}."
            if warning_text:
                message += f" {warning_text}"
            message += f" {backup_text}"
            self.status_field.SetLabel(message)
            self.status_bar.show(message)
            self.announcer.say(message)
            self._restore_focus_after_dialog()

        def failed(error: Exception) -> None:
            self._audio_clip_edit_in_progress = False
            self._audio_clip_edit_kind = None
            self._audio_clip_edit_percent = -1
            self._audio_clip_edit_operation_id = None
            message = f"Nie udało się dopisać fragmentu: {error}"
            self.status_field.SetLabel(message)
            self.status_bar.show(message)
            self.announcer.say(message)
            self._restore_focus_after_dialog()

        self.runner.submit(
            "audio-clip-append",
            lambda: client.append_audio_clip(
                source_path=context.source_path,
                target_path=target_path,
                start_seconds=selection.start_seconds or 0.0,
                end_seconds=selection.end_seconds or 0.0,
                keep_backup=keep_backup,
                operation_id=operation_id,
            ),
            done,
            failed,
        )

    def _remove_audio_clip(self) -> None:
        if self._audio_clip_edit_in_progress:
            self.announcer.say(
                "Dopisywanie fragmentu już trwa"
                if self._audio_clip_edit_kind == "append"
                else "Usuwanie fragmentu już trwa"
            )
            return
        if self._audio_clip_export_in_progress:
            self.announcer.say("Zapisywanie fragmentu już trwa")
            return

        def ready(context: AudioClipContext, selection: AudioClipSelection) -> None:
            if not selection.is_complete:
                self.announcer.say(
                    "Zaznacz początek klawiszem I i koniec klawiszem O"
                )
                return
            client = self.client
            if client is None:
                return

            def prepare() -> tuple[object, bool]:
                capabilities = client.audio_clip_removal_capabilities(
                    context.source_path
                )
                keep_backup = load_keep_audio_edit_backups(
                    self.layout.state_json
                )
                return capabilities, keep_backup

            def prepared(result: tuple[object, bool]) -> None:
                capabilities, keep_backup = result
                if not isinstance(capabilities, dict):
                    self.announcer.say(
                        "Nie można sprawdzić możliwości edycji pliku. Oryginalny plik nie został zmieniony"
                    )
                    return
                if capabilities.get("available") is not True:
                    message = str(capabilities.get("message") or "").strip()
                    self.announcer.say(
                        message
                        or "Usuwanie fragmentu z tego pliku jest niedostępne"
                    )
                    return
                self._confirm_audio_clip_removal(
                    context,
                    selection,
                    keep_backup=keep_backup,
                )

            self.runner.submit(
                "audio-clip-remove-preflight",
                prepare,
                prepared,
                lambda error: self.announcer.say(str(error)),
            )

        self._request_audio_clip_context(ready)

    def _confirm_audio_clip_removal(
        self,
        context: AudioClipContext,
        selection: AudioClipSelection,
        *,
        keep_backup: bool,
    ) -> None:
        if not selection.is_complete:
            return
        dialog = wx.MessageDialog(
            self,
            remove_clip_confirmation_text(
                selection.start_seconds or 0.0,
                selection.end_seconds or 0.0,
                keep_backup=keep_backup,
            ),
            "Usuń fragment z oryginalnego pliku",
            wx.YES_NO | wx.NO_DEFAULT | wx.ICON_WARNING,
        )
        try:
            # Jawne etykiety sa wazne dla NVDA: uzytkownik slyszy skutek
            # przycisku, a nie samo ogolne "Tak" / "Nie".
            if hasattr(dialog, "SetName"):
                dialog.SetName("Potwierdzenie usunięcia fragmentu")
            if hasattr(dialog, "SetYesNoLabels"):
                dialog.SetYesNoLabels(
                    "Tak, usuń fragment",
                    "Nie, pozostaw plik",
                )
            confirmed = dialog.ShowModal() == wx.ID_YES
        finally:
            dialog.Destroy()
        self._restore_focus_after_dialog()
        if not confirmed:
            return
        self._start_audio_clip_removal(
            context,
            selection,
            keep_backup=keep_backup,
        )

    def _start_audio_clip_removal(
        self,
        context: AudioClipContext,
        selection: AudioClipSelection,
        *,
        keep_backup: bool,
    ) -> None:
        client = self.client
        if client is None or not selection.is_complete:
            return
        operation_id = uuid.uuid4().hex
        self._audio_clip_edit_in_progress = True
        self._audio_clip_edit_kind = "remove"
        self._audio_clip_edit_percent = 0
        self._audio_clip_edit_operation_id = operation_id
        progress_text = f"Usuwanie fragmentu: 0%. {Path(context.source_path).name}"
        self.status_field.SetLabel(progress_text)
        self.status_bar.show(progress_text)

        def done(payload: dict) -> None:
            self._audio_clip_edit_in_progress = False
            self._audio_clip_edit_kind = None
            self._audio_clip_edit_percent = 100
            self._audio_clip_edit_operation_id = None
            result = payload if isinstance(payload, dict) else {}
            raw_duration = result.get("durationSeconds")
            duration = (
                float(raw_duration)
                if isinstance(raw_duration, (int, float))
                and not isinstance(raw_duration, bool)
                else max(
                    0.0,
                    context.duration_seconds
                    - ((selection.end_seconds or 0.0) - (selection.start_seconds or 0.0)),
                )
            )
            next_position = min(selection.start_seconds or 0.0, duration)
            self._last_status = {
                **self._last_status,
                "positionSeconds": next_position,
                "durationSeconds": duration,
                "paused": False,
            }
            empty = AudioClipSelection(
                item_id=context.item_id,
                source_path=context.source_path,
            )
            self._persist_audio_clip_selection(empty)

            backup_path = result.get("backupPath")
            backup_text = describe_backup_outcome(
                keep_backup,
                backup_path if isinstance(backup_path, str) else None,
            )
            if isinstance(backup_path, str) and backup_path.strip():
                backup_text += " " + Path(backup_path).name
            message = f"Fragment usunięty. {backup_text}"
            self.status_field.SetLabel(message)
            self.status_bar.show(message)
            self.announcer.say(message)
            self._restore_focus_after_dialog()

        def failed(error: Exception) -> None:
            self._audio_clip_edit_in_progress = False
            self._audio_clip_edit_kind = None
            self._audio_clip_edit_percent = -1
            self._audio_clip_edit_operation_id = None
            message = f"Nie udało się usunąć fragmentu: {error}"
            self.status_field.SetLabel(message)
            self.status_bar.show(message)
            self.announcer.say(message)
            self._restore_focus_after_dialog()

        self.runner.submit(
            "audio-clip-remove",
            lambda: client.remove_audio_clip(
                source_path=context.source_path,
                start_seconds=selection.start_seconds or 0.0,
                end_seconds=selection.end_seconds or 0.0,
                source_duration_seconds=context.duration_seconds,
                keep_backup=keep_backup,
                operation_id=operation_id,
            ),
            done,
            failed,
        )

    def _play_from_queue(self, intent: PlayFromQueue) -> None:
        """Start ZYWEJ kolejki hosta od wybranego wiersza.

        Dwa zadania pod rzad w JEDNYM watku roboczym: ``queue.set`` wczytuje
        kolejnosc (nic nie gra), ``queue.playAt`` zaczyna od wskazanej pozycji.
        Rozdzielone, bo samo wejscie w widok nie moze niczego odtworzyc.
        """
        client = self.client
        if client is None:
            self.announcer.say("Silnik nie dziala, nie moge odtworzyc")
            return

        items = [
            # ``isInQueue``/``isPlayNext`` sa WYMAGANE: pozycja bez zadnej z
            # nich nie jest dla hosta czescia kolejki
            # (``LiteQueueCoordinator.Set`` -> ``SynchronizeQueueOrder``) i
            # utwor zagra, ale naturalny koniec NIE poprowadzi dalej.
            # Wartosci bierzemy z ODCZYTU profilu, a nie wymuszamy ``True``:
            # inaczej blok "odtworz nastepne" zlalby sie ze zwykla kolejka.
            {
                "id": row.item_id,
                "path": row.path,
                "title": row.title,
                "isInQueue": intent.flags.get(row.item_id, (True, False))[0],
                "isPlayNext": intent.flags.get(row.item_id, (True, False))[1],
            }
            for row in intent.rows
        ]
        # Kolejnosc WIDOKU podajemy jawnie. Bez ``order`` host odtworzylby ja z
        # kolejnosci ``items``, co dzis wychodzi na to samo -- ale jawny
        # kontrakt nie zalezy od tej zbieznosci.
        order = [row.item_id for row in intent.rows]

        def work() -> dict:
            client.queue_set(items, order=order)
            return client.queue_play_at(
                intent.item_id, volume=self.options.volume, rate=self.options.rate
            )

        def done(payload: dict) -> None:
            self._note_queue_persistence(payload or {})
            self._refresh_status()

        def failed(error: Exception) -> None:
            self._run(self.navigator.note_playback_failed(f"Nie udalo sie odtworzyc: {error}"))

        self.runner.submit("playback", work, done, failed)

    def _open_queue_view(self) -> None:
        """Ctrl+Q: stan hosta, także po zużyciu ostatniego wiersza.

        Zapis profilu służy tylko jako początek, zanim host przyjął kolejkę.
        Pusta zainicjalizowana kolejka nie odtwarza zużytych wpisów z dysku.
        """
        client = self.client
        if client is None:
            self._run(self.navigator.open_library_view(LibraryView.SAVED_QUEUE))
            return

        def work() -> dict:
            return client.queue_status()

        def done(payload: dict) -> None:
            rows = rows_from_queue_status(payload or {})
            if not rows and not payload.get("initialized", False):
                # Jeszcze nie wczytano żadnej kolejki do tego hosta.
                self._run(self.navigator.open_library_view(LibraryView.SAVED_QUEUE))
                return
            current = payload.get("currentId")
            self._run(self.navigator.apply_live_queue(
                rows, current_id=str(current) if current else None
            ))

        def failed(error: Exception) -> None:
            # Brak odpowiedzi silnika nie moze skonczyc sie cisza ani pusta
            # lista: mowimy, co sie stalo, i oddajemy widok zapisany.
            self.announcer.say(f"Nie moge odczytac kolejki silnika: {error}")
            self._run(self.navigator.open_library_view(LibraryView.SAVED_QUEUE))

        self.runner.submit("folder", work, done, failed)

    def _toggle_queue_membership(self, play_next: bool) -> None:
        """Zmień kolejkę dla natywnego wielokrotnego zaznaczenia.

        Operacja dotyczy na razie Biblioteki plików lokalnych — dokładnie tego
        etapu portu. Nie składamy aktywnej kolejki ponownie, więc grający plik,
        pozycja, pauza i wyjście audio pozostają nietknięte.
        """
        if self.navigator.active is not SessionId.FILES:
            self.announcer.say("To polecenie dotyczy Biblioteki plików")
            return
        client = self.client
        if client is None:
            self.announcer.say("Silnik nie działa, nie mogę zmienić kolejki")
            return

        rows = self._selected_action_rows()
        if not rows:
            self.announcer.say("Nie wybrano żadnego pliku")
            return
        if any(row.kind != "track" for row in rows):
            self.announcer.say("Do kolejki można dodać tylko pliki")
            return

        items = [
            {"id": row.item_id, "title": row.title, "path": row.path}
            for row in rows
        ]
        call = (
            client.queue_toggle_play_next
            if play_next
            else client.queue_toggle_membership
        )

        def done(payload: dict) -> None:
            payload = payload or {}
            self._note_queue_persistence(payload)
            changed = int(payload.get("changed") or 0)
            if changed <= 0:
                self.announcer.say("Kolejka bez zmian")
                return
            label = rows[0].title if changed == 1 else format_item_count(changed)
            added = bool(payload.get("added"))
            if play_next:
                message = (
                    f"Ustawiono jako następne: {label}"
                    if added
                    else f"Usunięto z odtwarzania jako następne: {label}"
                )
            else:
                message = (
                    f"Dodano do kolejki: {label}"
                    if added
                    else f"Usunięto z kolejki: {label}"
                )
            self.announcer.say(message)
            self._refresh_live_queue()

        def failed(error: Exception) -> None:
            self.announcer.say(f"Nie mogę zmienić kolejki: {error}")

        self.runner.submit("queue-membership", lambda: call(items), done, failed)

    def _refresh_live_queue(self) -> None:
        """Odśwież już otwartą kolejkę bez nawigacji i przejmowania fokusu."""
        state = self.navigator.session
        if state.library_view is not LibraryView.LIVE_QUEUE or self.client is None:
            return
        client = self.client

        def current() -> bool:
            return (
                self.navigator.session is state
                and state.library_view is LibraryView.LIVE_QUEUE
            )

        def done(payload: dict) -> None:
            if not current():
                return
            rows = rows_from_queue_status(payload or {})
            if rows == state.model.rows:
                return
            state.model.replace(rows, preferred_id=state.model.selected_id)
            self._sync_views()

        def failed(error: Exception) -> None:
            if current():
                self.announcer.say(f"Nie mogę odświeżyć kolejki: {error}")

        self.runner.submit("queue-refresh", client.queue_status, done, failed)

    def _play_queue_at(self, intent: PlayQueueAt) -> None:
        """Enter w ZYWYM widoku: start od wiersza kolejki, ktora host juz ma.

        Bez ``queue.set``: kolejka po stronie hosta JEST stanem, a nie kopia
        ekranu. Ponowne wyslanie jej z wierszy widoku skasowalo by to, co sesja
        Core wie o pozycjach juz odegranych.
        """
        client = self.client
        if client is None:
            self.announcer.say("Silnik nie dziala, nie moge odtworzyc")
            return

        def work() -> dict:
            return client.queue_play_at(
                intent.item_id, volume=self.options.volume, rate=self.options.rate
            )

        def done(payload: dict) -> None:
            self._note_queue_persistence(payload or {})
            self._refresh_status()

        def failed(error: Exception) -> None:
            self._run(self.navigator.note_playback_failed(f"Nie udalo sie odtworzyc: {error}"))

        self.runner.submit("playback", work, done, failed)

    def _queue_step(self, forward: bool) -> None:
        """Page Down / Page Up: lista zrodlowa ALBO prawdziwa kolejka.

        Pełne AMC zmienia element źródła, z którego otwarto odtwarzacz. Tylko
        wejście z Zapisanej/Zywej kolejki deleguje krok do koordynatora hosta.
        Dawniej każde naciśnięcie szło do ``queue.*`` i zwykły plik albo stacja
        kończyły komunikatem „początek/koniec kolejki”.
        """
        source_intents = self.navigator.step_playback_source(forward)
        if source_intents is not None:
            self._run(source_intents)
            return

        client = self.client
        if client is None:
            self.announcer.say("Silnik nie dziala")
            return

        call = client.queue_next if forward else client.queue_previous

        def work() -> dict:
            return call(volume=self.options.volume, rate=self.options.rate)

        def done(payload: dict) -> None:
            # Mowimy to, co host NAPRAWDE zaczal grac, a nie to, o co prosilismy.
            if payload and payload.get("moved"):
                title = str(payload.get("currentTitle") or "").strip()
                self._run(self.navigator.note_queue_advanced(
                    str(payload.get("currentId") or ""), title
                ))
            else:
                self.announcer.say(
                    "Koniec kolejki" if forward else "Poczatek kolejki"
                )
            self._note_queue_persistence(payload or {})
            self._refresh_status()

        def failed(error: Exception) -> None:
            self.announcer.say(f"Nie moge zmienic utworu: {error}")

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
            # Jak w ``_play_track``: host gra teraz RADIO.
            self._playing_session = SessionId.RADIO
            self._refresh_status()

        def failed(error: Exception) -> None:
            self._run(self.navigator.note_playback_failed(f"Nie moge polaczyc ze stacja: {error}"))

        self.runner.submit("playback", work, done, failed)

    def _pause_on_player_exit_if_needed(self, session: SessionId) -> None:
        """Wstrzymaj po wyjsciu z odtwarzacza, jesli TA sesja tak ma ustawione.

        Port ``MainWindow.ApplyPlaybackPolicyWhenLeavingPlayer``
        (MainWindow.xaml.cs:2396-2420) zwezony do tego, co port ma dzisiaj.
        Oryginal stosuje polityke WYLACZNIE przy powrocie na liste
        (``ShouldApplyPlaybackExitPolicy`` -> ``ReturnToList``), a w porcie
        Escape, F6 i menu "Powrót na listę" wszystkie prowadza do
        ``navigator.back_to_list()`` -- dlatego guard stoi dokladnie tam.

        Trzy warunki odmowy, kazdy z powodem:

        * ``_playing_session`` inne niz wychodzaca sesja -- host ma JEDNO
          wyjscie, a ``transport.pauseResume`` nie zna zakresu; wstrzymanie
          ruszylo by CUDZE granie (oryginal rozwiazuje to samo przez wyjatki
          dla ``wiim``/Sonos, ktorych port nie ma),
        * nic nie gra w tej sesji (odpowiednik ``session.HasCurrentItem``),
        * host juz jest wstrzymany -- ``pauseResume`` to przelacznik, wiec
          drugie wywolanie WZNOWILO by odtwarzanie, czyli dokladnie odwrotnie
          niz zada opcja (oryginal ma na to ``session.IsPlaying``).

        Zapisana opcja zastepuje sama REGULE pauzy. Zaznaczenie, filtr i widok
        listy zostaja nietkniete -- to osobna droga (``back_to_list``).
        """
        client = self.client
        if client is None:
            return
        if self._playing_session is not session:
            return
        state = self.navigator.sessions.get(session)
        if state is None or state.now_playing_id is None:
            return
        if not session_options.resolve_pause_on_player_exit(
            self.options, session_options.effective_overrides(self.state, session)
        ):
            return
        if bool(self._last_status.get("paused")):
            return

        def done(payload: dict) -> None:
            paused = bool((payload or {}).get("paused"))
            self._last_status = {**self._last_status, "paused": paused}
            self._set_transport_label(playing=not paused)

        # Przez istniejacy TaskRunner z bramka zywego kontekstu: synchroniczne
        # wywolanie hosta na watku GUI zawiesilo by okno na czas call timeout.
        self.runner.submit("playback", client.pause_resume, done, lambda _error: None)

    def _leave_player_to_list(self) -> None:
        """Wyjscie z odtwarzacza na liste: najpierw polityka, potem nawigacja.

        Kolejnosc jak w oryginale (MainWindow.xaml.cs:2335-2338): polityka
        pauzy czyta stan sesji SPRZED przebudowy widoku.
        """
        session = self.navigator.active
        if self.navigator.session.view is View.PLAYER:
            self._pause_on_player_exit_if_needed(session)
        self._run(self.navigator.back_to_list(
            follow_playback=self.options.follow_playback_on_player_exit
        ))

    def _choose_audio_output(self) -> None:
        """Shift+A: wybierz i zapamietaj wyjscie osobno dla jednej sesji."""
        client = self.client
        if client is None:
            self.announcer.say("Silnik nie jest gotowy")
            return
        session = self.navigator.active
        session_name = session_options.session_display_name(session)
        previous_device_id = effective_output_device_id(
            session,
            self.state.audio_output_device_ids_by_session,
            self._profile_audio_outputs,
        )

        def prepared(payload: dict) -> None:
            choices = choices_from_payload(payload)
            if not choices:
                self.announcer.say("Nie udało się odczytać urządzeń audio")
                return
            dialog = AudioOutputDeviceDialog(
                self,
                session_name,
                choices,
                previous_device_id,
            )
            selected: AudioOutputChoice | None = None
            try:
                if dialog.ShowModal() == wx.ID_OK:
                    selected = dialog.selected_choice
            finally:
                dialog.Destroy()
            self._restore_focus_after_dialog()
            if selected is None:
                return

            def applied(result: dict) -> None:
                previous_private = dict(
                    self.state.audio_output_device_ids_by_session
                )
                next_private = dict(previous_private)
                next_private[session.value] = selected.device_id or ""
                self.state.audio_output_device_ids_by_session = next_private
                if not self._save_state():
                    self.state.audio_output_device_ids_by_session = previous_private
                    # Silnik zdazyl zastosowac wybor. Przywracamy poprzedni w
                    # tle, zeby RAM, dzwiek i stan na dysku znow byly zgodne.
                    self.runner.submit(
                        "audio-output-rollback",
                        lambda: client.select_audio_output(
                            session.value,
                            previous_device_id,
                            restart=True,
                        ),
                        lambda _payload: None,
                        lambda _error: None,
                    )
                    return
                if selected.device_id is not None and bool(
                    (result or {}).get("usingDefault")
                ):
                    self.announcer.say(
                        f"Zapamiętano dla sesji {session_name}: {selected.label}. "
                        "Urządzenie jest teraz niedostępne, dlatego używane jest "
                        "urządzenie domyślne"
                    )
                else:
                    self.announcer.say(
                        f"Dla sesji {session_name} wybrano: {selected.label}"
                    )

            self.runner.submit(
                "audio-output-select",
                lambda: client.select_audio_output(
                    session.value,
                    selected.device_id,
                    restart=True,
                ),
                applied,
                lambda error: self.announcer.say(
                    f"Nie zmieniono urządzenia audio: {error}"
                ),
            )

        self.runner.submit(
            "audio-output-list",
            lambda: client.audio_outputs(previous_device_id),
            prepared,
            lambda error: self.announcer.say(
                f"Nie udało się odczytać urządzeń audio: {error}"
            ),
        )

    def _show_general_playback_options(self) -> None:
        """Ustawienia ogolne wxPython z natychmiastowym, atomowym zapisem."""
        dialog = GeneralPlaybackOptionsDialog(self, self.options)
        chosen: dict[str, object] | None = None
        try:
            if dialog.ShowModal() == wx.ID_OK:
                chosen = dialog.values
        finally:
            dialog.Destroy()
        if chosen is None:
            return

        previous = {name: getattr(self.options, name) for name in chosen}
        for name, value in chosen.items():
            setattr(self.options, name, value)
        if not self._save_state():
            for name, value in previous.items():
                setattr(self.options, name, value)
            self.state.options = self.options
            return
        # Zmiana dotyczy tylko tekstu/sygnalu wierszy. Odtwarzanie,
        # nagrywanie, harmonogramy i timeshift pozostaja nietkniete.
        self._apply_radio_activity_status()
        self.announcer.say("Zapisano ustawienia interfejsu i odtwarzania")

    def _show_session_options(self) -> None:
        """Opcje sesji: dialog, wybor sesji, Zapisz/Anuluj, zapis i SKUTEK.

        Sesje otwarcia bierzemy z nawigatora, ale KONFIGUROWANA sesja pochodzi
        z dialogu -- wymaganie jest takie, by dalo sie ustawic druga sesje BEZ
        przelaczenia odsluchu. Dialog oddaje ``drafts``: wybory kazdej sesji,
        ktorej uzytkownik dotknal.

        Anuluj, Escape i krzyzyk konczy sie TUTAJ: poza ``wx.ID_OK`` nie
        wolamy ani zapisu, ani silnika. Fokus wraca PO ``Destroy``, bo przed
        nim wx oddal by go z powrotem oknu modalnemu.
        """
        opened = self.navigator.active
        drafts = {
            session: session_options.effective_overrides(self.state, session)
            for session in (SessionId.FILES, SessionId.RADIO, SessionId.PODCASTS)
        }
        dialog = SessionOptionsDialog(
            self, opened, self.options, drafts[opened], drafts=drafts
        )
        chosen: dict | None = None
        try:
            if dialog.ShowModal() == wx.ID_OK:
                chosen = dialog.drafts
        finally:
            dialog.Destroy()

        if chosen is None:
            self._restore_focus_after_dialog()
            return

        self._apply_session_option_drafts(chosen)
        self._restore_focus_after_dialog()

    def _apply_session_option_drafts(self, drafts: dict) -> None:
        """Zatwierdz wybory ze wszystkich edytowanych sesji.

        SILNIK IDZIE PRZEZ TASKRUNNER. ``audio.configure`` to wywolanie
        procesu hosta z wlasnym timeoutem; na watku GUI zawieszalo by okno i
        czytnik ekranu na caly ten czas. ``runner.submit`` ma bramke zywego
        kontekstu, wiec spozniony wynik nie dotknie zamknietego okna.

        SPOJNOSC RAM / SILNIK / DYSK. Zmieniamy tylko sesje, ktorych wybor
        NAPRAWDE rozni sie od stanu trwalego. Gdy dysk ODMOWI zapisu, wracamy
        do stanu sprzed zmiany -- i w pamieci, i w silniku. Bez tego
        uzytkownik slyszal "Nie moge zapisac" i zaraz po nim "Zapisano", a
        RAM/silnik zostawaly z wyborem, ktorego na dysku nie ma.
        """
        poprzednie = {
            session: session_options.effective_overrides(self.state, session)
            for session in (SessionId.FILES, SessionId.RADIO, SessionId.PODCASTS)
        }
        zmienione = [
            session
            for session, overrides in drafts.items()
            if overrides.restricted_to(session_options.capabilities_for(session))
            != poprzednie.get(session)
        ]
        if not zmienione:
            self.announcer.say("Opcje sesji bez zmian.")
            return

        def work() -> list:
            # Watek roboczy: wylacznie rozmowa z hostem i zmiana stanu w RAM.
            # Zapis na dysk i mowa czytnika zostaja w ``done`` (watek GUI).
            return [
                session_options.apply_session_options(
                    self.state, session, drafts[session], client=self.client
                )
                for session in zmienione
            ]

        def done(results: list) -> None:
            odmowa = next((item for item in results if not item.saved), None)
            if odmowa is not None:
                # Odmowa silnika: ``apply_session_options`` nie dotknelo stanu.
                self.announcer.say(odmowa.message)
                return
            if self._save_state() is False:
                # Dysk odmowil. ``_save_state`` juz powiedzial dlaczego --
                # drugi komunikat ze slowem "Zapisano" byl by klamstwem.
                for session in zmienione:
                    session_options.restore_session_options(
                        self.state, session, poprzednie[session], client=self.client
                    )
                return
            tresc = " ".join(item.message for item in results)
            if any(item.applies_on_next_playback for item in results):
                # Prawda komunikatu: host zwrocil ``appliesOnNextPlayback``,
                # wiec biezace granie zostaje po staremu.
                tresc += " Przetwarzanie dźwięku zmieni się przy następnym uruchomieniu materiału."
            self.announcer.say(tresc)

        def failed(error: Exception) -> None:
            self.announcer.say(f"Nie zapisano opcji sesji: {error}")

        self.runner.submit("session-options", work, done, failed)

    def _restore_focus_after_dialog(self) -> None:
        """Fokus po zamknieciu okna modalnego: lista TEJ sesji albo odtwarzacz.

        Bez tego fokus zostaje na ramce i czytnik ekranu nie ma czego czytac, a
        klawisze listy nie dochodza do kontrolki.

        Lista pochodzi z ``_active_list()``. ``LiteFrame`` NIE ma pola
        ``list_ctrl``: sa dwie listy (``files_list``, ``radio_list``) i o
        wlasciwej decyduje aktywna sesja. Wolanie nieistniejacego pola
        konczylo sie ``AttributeError`` i fokus ginal po KAZDYM zamknieciu
        dialogu -- tak samo po Anuluj jak po Zapisz.
        """
        target = (
            self.play_button
            if self.navigator.session.view is View.PLAYER
            else self._active_list()
        )
        if target is not None:
            target.SetFocus()

    def _play_pause(self) -> None:
        client = self.client
        if client is None:
            return

        def done(payload: dict) -> None:
            paused = bool((payload or {}).get("paused"))
            # Etykieta zawsze zgodna ze stanem: po wstrzymaniu przycisk ma juz
            # proponowac odtwarzanie (MainWindow.xaml.cs:2560).
            self._set_transport_label(playing=not paused)
            self.announcer.say(transport_confirmation(paused=paused))

        self.runner.submit(
            "transport",
            client.pause_resume,
            done,
            lambda error: self.announcer.say(f"Blad: {error}"),
        )

    def _set_transport_label(self, *, playing: bool, preparing: bool = False) -> None:
        """Jedno miejsce, ktore nazywa przycisk transportu.

        Czytnik czyta nazwe przycisku przy fokusie, wiec etykieta i nazwa
        dostepnosciowa musza byc tym samym slowem -- inaczej widac jedno, a
        slychac drugie. Nie ogłaszamy tu nic sami: zmiana etykiety to nie
        potwierdzenie gestu, a dwa zrodla mowy daja podwojna zapowiedz.
        """
        label = transport_button_label(playing=playing, preparing=preparing)
        if self.play_button.GetLabel() == label:
            return
        self.play_button.SetLabel(label)
        self.play_button.SetName(label.replace("&", ""))

    def _seek(self, delta: float) -> None:
        client = self.client
        if client is None:
            return
        self.runner.submit(
            "transport",
            lambda: client.seek_by(delta),
            # Oryginal pyta najpierw o przelaczniki: CommandRouter.cs:534-539
            # sprawdza Messages.SeekMessages, a dla strzalek dodatkowo
            # ArrowSeekMessages -- i DOPIERO wtedy formatuje czas. Dostarczona
            # wersja mowila bezwarunkowo, dlatego strzalki gadaly non stop.
            lambda payload: self._announce_seek(payload),
            lambda error: self.announcer.say(f"Nie moge przewinac: {error}"),
        )

    def _announce_seek(self, payload: dict | None) -> None:
        """Komunikat po przewinieciu -- tylko jesli ustawienia na to pozwalaja."""
        text = self.messages.arrow_seek_text((payload or {}).get("positionSeconds"))
        if text is not None and self.messages.speaks_routine:
            self.announcer.say(text)

    def _seek_to_track_edge(self, to_end: bool) -> None:
        """Home/End w odtwarzaczu. Port ``CommandRouter.cs:316-324``.

        End NIE jest skokiem na 100%: oryginal celuje w
        ``Max(Zero, Duration - 10s)``, zeby na koncu bylo jeszcze czego
        posluchac, a utwor nie konczyl sie w tej samej chwili. Home to czysta
        pozycja zero i oryginal mowi przy niej doslownie \"0:00\".

        Uzywamy TEGO SAMEGO ``seek_to_position`` hosta, co skok procentowy --
        nie dokladamy nowej komendy protokolu.
        """
        client = self.client
        if client is None:
            return
        if not to_end:
            self.runner.submit(
                "transport",
                lambda: client.seek_to_position(0.0),
                # cs:318 -- staly napis, nie odczyt pozycji z odpowiedzi.
                lambda _payload: self._announce_track_edge("0:00"),
                lambda error: self.announcer.say(f"Nie moge przewinac: {error}"),
            )
            return

        duration = self._last_status.get("durationSeconds")
        if not isinstance(duration, (int, float)) or duration <= 0:
            # Bez czasu trwania konca nie da sie policzyc. Milczenie
            # wygladaloby na zepsuty klawisz, wiec mowimy -- to blad
            # wykonania, nie rutynowy komunikat (jak cs:546-551 przy %).
            self.announcer.say("Nie znam czasu trwania, nie moge skoczyc na koniec")
            return
        target = max(0.0, float(duration) - TRACK_END_MARGIN_SECONDS)
        self.runner.submit(
            "transport",
            lambda: client.seek_to_position(target),
            lambda _payload: self._announce_track_edge(format_clock(target)),
            lambda error: self.announcer.say(f"Nie moge przewinac: {error}"),
        )

    def _announce_track_edge(self, text: str) -> None:
        """cs:318 i cs:323 pytaja o TE SAME dwie bramki, co strzalki."""
        if self.messages.announces_arrow_seek and self.messages.speaks_routine:
            self.announcer.say(text)

    def _seek_percent(self, percent: int) -> None:
        """Skok procentowy (gole cyfry 0..9) -- wlasna rodzina przelacznikow.

        CommandRouter.cs:545-560 trzyma dla procentow OSOBNE przelaczniki
        (PercentageSeekMessages / PercentageSeekAnnouncement), wiec NIE
        dziedzicza one po polityce strzalek.
        """
        client = self.client
        if client is None:
            return
        duration = self._last_status.get("durationSeconds")
        if not isinstance(duration, (int, float)) or duration <= 0:
            # cs:546-551 -- brak czasu trwania to blad wykonania, mowiony
            # ZAWSZE, bo inaczej gest wygladalby na niedzialajacy.
            text = self.messages.percent_seek_text(
                percent, position_seconds=None, duration_seconds=None
            )
            if text is not None:
                self.announcer.say(text)
            return
        target = float(duration) * percent / 100.0
        self.runner.submit(
            "transport",
            lambda: client.seek_to_position(target),
            lambda payload: self._announce_seek_percent(percent, payload, float(duration)),
            lambda error: self.announcer.say(f"Nie moge przewinac: {error}"),
        )

    def _announce_seek_percent(
        self, percent: int, payload: dict | None, duration: float
    ) -> None:
        text = self.messages.percent_seek_text(
            percent,
            position_seconds=(payload or {}).get("positionSeconds"),
            duration_seconds=duration,
        )
        if text is not None and self.messages.speaks_routine:
            self.announcer.say(text)

    def _adjust_volume(self, delta: int) -> None:
        self._set_volume(self.options.volume + delta)

    def _set_volume(self, value: int) -> None:
        value = max(0, min(100, int(value)))
        self.options.volume = value
        self.volume_slider.SetValue(value)
        # Glosnosc tez ma swoj przelacznik (CommandRouter.cs:566-575) i swoj
        # szablon "{value}%" -- nie wlasne zdanie "Glosnosc 70".
        text = self.messages.volume_text(value)
        if text is not None and self.messages.speaks_routine:
            self.announcer.say(text)
        client = self.client
        if client is not None:
            self.runner.submit("volume", lambda: client.set_volume(value), lambda _p: None, lambda _e: None)

    def _set_rate(self, value: float) -> None:
        # Przyciecie jak SetPlaybackRate (DemoMediaSession.cs:381): do
        # NAJBLIZSZEGO szczebla drabiny, nie do dowolnej wartosci z suwaka.
        value = clamp_playback_rate(value)
        self.options.rate = value
        self.rate_slider.SetValue(int(round(value * 100)))
        # Format jak w oryginale: "1,25x" (MainWindow.xaml.cs:16122), a nie
        # "Tempo 125 procent".
        self.announcer.say(format_playback_rate(value))
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
        """Czas na zadanie: Ctrl+Shift+E / R / T.

        To komenda WPROST od uzytkownika, wiec oryginal NIE filtruje jej
        przelacznikami przewijania -- CommandRouter.cs:325-335 idzie prosto do
        AnnounceTemplate. Domyslny szablon to sam czas, bez slowa "Minelo":
        release .383 pokazuje "3:51". Slowo pojawia sie tylko wtedy, gdy
        uzytkownik sam wpisal je do szablonu w ustawieniach AMC.
        """
        status = self._last_status
        text = time_announcement(
            action,
            self.messages,
            position=status.get("positionSeconds"),
            duration=status.get("durationSeconds"),
        )
        if text is not None:
            self.announcer.say(text)

    def _toggle_seek_messages(self) -> None:
        """Ctrl+Shift+G -- przelacznik komunikatow przewijania.

        We wspolnym profilu wlascicielem state.json jest host C#, wiec gest
        nie zapisuje nic po cichu: mowi, gdzie te opcje zmienic. Martwe pole
        byloby gorsze od braku pola.
        """
        result = self.messages.toggle_seek_messages()
        self.announcer.say(result.message)

    # ------------------------------------------------------------- status

    def _on_timer(self, _event: wx.TimerEvent) -> None:
        # Stan transportu i nagran odswiezamy takze na liscie: dzieki temu
        # wiersz stacji uczciwie mowi, czy jest odtwarzany albo nagrywany.
        # Etykiete czasu nadal zmieniamy tylko w widoku odtwarzacza.
        self._refresh_status()
        self._refresh_recording_status()
        self._checkpoint_podcast_if_due()

    def _checkpoint_podcast_if_due(self) -> None:
        """Co 15 sekund zlec zapis jak w pelnym AMC, bez dodatkowej mowy."""
        client = self.client
        if (
            client is None
            or not self._podcast_checkpoint_active
            or self._podcast_checkpoint_pending
            or time.monotonic() < self._podcast_checkpoint_due
        ):
            return
        self._podcast_checkpoint_pending = True
        # Termin przesuwamy PRZED zadaniem: szybkie ticki nie moga ustawic
        # kilku rownoleglych zapisow tego samego czasu.
        self._podcast_checkpoint_due = time.monotonic() + 15.0

        def done(_payload: dict) -> None:
            self._podcast_checkpoint_pending = False
            self._last_podcast_progress_error = None

        def failed(error: Exception) -> None:
            self._podcast_checkpoint_pending = False
            self._note_podcast_progress_error(str(error))

        self.runner.submit(
            "podcast-checkpoint",
            client.checkpoint_podcast,
            done,
            failed,
        )

    def _note_podcast_progress_error(self, message: str) -> None:
        """Jedna slyszalna informacja na przyczyne; timer nie jest logiem."""
        cleaned = message.strip() or "Nie udało się zapisać postępu odcinka"
        if cleaned == self._last_podcast_progress_error:
            return
        self._last_podcast_progress_error = cleaned
        self.announcer.say(f"Nie zapisano postępu odcinka: {cleaned}")

    def _refresh_status(self) -> None:
        client = self.client
        if client is None or getattr(self, "_status_poll_pending", False):
            return
        self._status_poll_pending = True
        def done(payload: dict) -> None:
            self._status_poll_pending = False
            if not payload:
                return
            self._last_status = payload
            if self.navigator.view is View.PLAYER:
                position = payload.get("positionSeconds")
                duration = payload.get("durationSeconds")
                label = player_time_label(position, duration)
                # Odswiezamy etykiete tylko gdy TEKST sie zmienil - inaczej
                # czytnik ekranu dostawalby zmiane co sekunde bez potrzeby.
                if self.time_label.GetLabel() != label:
                    self.time_label.SetLabel(label)
            # Etykieta transportu podaza za PRAWDZIWYM stanem hosta, nie tylko
            # za nasza Spacja: odtwarzanie zaczete Enterem na liscie albo
            # zakonczony plik tez musza ja poprawic. Bez ogloszenia -- samo
            # odswiezenie statusu nie jest gestem uzytkownika.
            if "paused" in payload:
                self._set_transport_label(playing=not bool(payload.get("paused")))
            self._apply_radio_activity_status()

        def failed(_error: Exception) -> None:
            self._status_poll_pending = False

        self.runner.submit("status", client.status, done, failed)

    def _refresh_recording_status(self) -> None:
        """Pobierz jedna migawke nagran bez mowy i bez nakladania zapytan."""
        client = self.client
        if client is None or getattr(self, "_recording_status_poll_pending", False):
            return
        self._recording_status_poll_pending = True

        def done(payload: dict) -> None:
            self._recording_status_poll_pending = False
            self._last_recording_status = payload or {}
            state = self.navigator.sessions[SessionId.RADIO]
            if state.library_view is LibraryView.ACTIVE_RADIO_RECORDINGS:
                preferred_id = state.model.selected_id
                state.model.replace(
                    active_recording_rows(self._last_recording_status),
                    preferred_id=preferred_id,
                )
                if self.navigator.active is SessionId.RADIO:
                    self._sync_views()
                return
            self._apply_radio_activity_status()

        def failed(_error: Exception) -> None:
            self._recording_status_poll_pending = False

        self.runner.submit(
            "radio-recording-status",
            client.radio_recording_status,
            done,
            failed,
        )

    def _apply_radio_activity_status(self) -> None:
        """Zmien tylko dynamiczny opis zwyklych list Radia."""
        state = self.navigator.sessions[SessionId.RADIO]
        if state.library_view not in (None, LibraryView.FAVORITES, LibraryView.HISTORY):
            return
        rows = station_activity_rows(
            state.model.rows,
            transport_status=self._last_status,
            recording_status=self._last_recording_status,
            playback_position=self.options.radio_playback_state_position,
            recording_position=self.options.radio_recording_state_position,
        )
        if rows == state.model.rows:
            return
        preferred_id = state.model.selected_id
        state.model.replace(rows, preferred_id=preferred_id)
        if self.navigator.active is SessionId.RADIO:
            self._sync_views()

    # ------------------------------------------------------- edycja Biblioteki

    def _single_edit_row(self, what: str) -> Row | None:
        rows = self._selected_action_rows()
        if len(rows) != 1:
            self.announcer.say(f"Do {what} wybierz jeden element")
            return None
        return rows[0]

    def _profile_edit_client(self) -> LiteHostClient | None:
        if self.client is None:
            self.announcer.say("Silnik nie działa. Zmiana nie została zapisana")
            return None
        return self.client

    def _current_profile_view_name(self) -> str:
        view = self.navigator.session.library_view
        return "library" if view is None else view.value

    def _refresh_profile_view(self, preferred_id: str | None = None) -> None:
        """Wczytaj ponownie TEN SAM widok po wąskiej zmianie hosta."""
        state = self.navigator.session
        if self.navigator.active is SessionId.RADIO:
            self._open_radio_view(OpenLibraryView(
                view=state.library_view,
                preferred_id=preferred_id,
                target_session_id=SessionId.RADIO,
            ))
            return
        if self.navigator.active is SessionId.PODCASTS:
            if state.library_view is LibraryView.PODCAST_LIBRARY:
                self._open_podcast_library(preferred_id=preferred_id)
            elif state.library_view is LibraryView.PODCAST_EPISODES:
                if state.library_playlist_id:
                    self._open_podcast_view(OpenPodcastView(
                        subscription_id=state.library_playlist_id,
                        preferred_id=preferred_id,
                    ))
            elif state.library_view in (
                LibraryView.PODCAST_FAVORITES,
                LibraryView.PODCAST_HISTORY,
                LibraryView.PODCAST_QUEUE,
                LibraryView.PODCAST_INBOX,
                LibraryView.PODCAST_DOWNLOADS,
            ):
                self._open_podcast_aggregate(OpenPodcastAggregateView(
                    state.library_view,
                    preferred_id=preferred_id,
                ))
            return
        if state.library_view is None:
            self._open_library(state.folder_path or None, preferred_id=preferred_id)
            return
        if state.library_view is LibraryView.LIVE_QUEUE:
            self._open_queue_view()
            return
        if state.library_view is LibraryView.RECORDED_RADIO_FILES:
            self._show_radio_recording_history()
            return
        if state.library_view not in self._VIEW_KEYS:
            self.announcer.say("Zmiana została zapisana. Odśwież bieżący widok")
            return
        self._open_library_view(OpenLibraryView(
            view=state.library_view,
            playlist_id=state.library_playlist_id,
            preferred_id=preferred_id,
            item_id=state.library_item_id,
            target_session_id=SessionId.FILES,
        ))

    def _rename_library_item(self) -> None:
        if self.navigator.active is SessionId.RADIO:
            self._station_edit()
            return
        row = self._single_edit_row("zmiany nazwy")
        if row is None:
            return
        if self.navigator.active is SessionId.PODCASTS:
            if row.kind != "podcast":
                self.announcer.say(
                    "Wybierz podcast lub kanał na głównej liście. "
                    "Nazwy odcinków pochodzą ze źródła"
                )
                return
            client = self._profile_edit_client()
            if client is None:
                return
            with RenameItemDialog(
                self,
                title="Zmień nazwę podcastu lub kanału",
                prompt="&Nowa nazwa podcastu lub kanału:",
                field_name="Nowa nazwa podcastu lub kanału",
                value=row.title,
                help_text=(
                    "Zmiana dotyczy nazwy wyświetlanej przez AMC. "
                    "Nazwa w źródle RSS lub YouTube pozostanie bez zmian."
                ),
            ) as dialog:
                if dialog.ShowModal() != wx.ID_OK:
                    return
                title = dialog.value
            if not title:
                self.announcer.say("Nowa nazwa podcastu lub kanału nie może być pusta")
                return

            def done(_payload: object) -> None:
                self._refresh_profile_view(preferred_id=row.item_id)
                self.announcer.say(f"Zmieniono nazwę podcastu lub kanału: {title}")

            self.runner.submit(
                "profile-edit",
                lambda: client.rename_podcast_subscription(row.item_id, title),
                done,
                lambda error: self.announcer.say(f"Nie zmieniono nazwy: {error}"),
            )
            return

        if row.kind != "track":
            self.announcer.say("Wybrany element nie jest plikiem lokalnym")
            return
        client = self._profile_edit_client()
        if client is None:
            return
        with RenameItemDialog(
            self,
            title="Zmień nazwę w Bibliotece",
            prompt="&Nowa nazwa w Bibliotece:",
            field_name="Nowa nazwa w Bibliotece",
            value=row.title,
            help_text="Plik na dysku nie zmieni nazwy.",
        ) as dialog:
            if dialog.ShowModal() != wx.ID_OK:
                return
            title = dialog.value
        if not title:
            self.announcer.say("Nowa nazwa w Bibliotece nie może być pusta")
            return
        if title == row.title:
            self.announcer.say("Nazwa w Bibliotece nie została zmieniona")
            return

        def done(_payload: object) -> None:
            self._refresh_profile_view(preferred_id=row.item_id)
            self.announcer.say(f"Zmieniono nazwę w Bibliotece: {title}")

        self.runner.submit(
            "profile-edit",
            lambda: client.rename_library_item(row.item_id, title),
            done,
            lambda error: self.announcer.say(f"Nie zmieniono nazwy: {error}"),
        )

    def _rename_local_file(self) -> None:
        if self.navigator.active is not SessionId.FILES:
            self.announcer.say("Zmiana nazwy pliku na dysku jest dostępna w Plikach lokalnych")
            return
        row = self._single_edit_row("zmiany nazwy pliku")
        if row is None:
            return
        if row.kind != "track" or not row.path:
            self.announcer.say("Wybrany element nie jest plikiem lokalnym")
            return
        client = self._profile_edit_client()
        if client is None:
            return
        current_name = Path(row.path).stem
        with RenameItemDialog(
            self,
            title="Zmień nazwę pliku na dysku",
            prompt="&Nowa nazwa pliku bez rozszerzenia:",
            field_name="Nowa nazwa pliku bez rozszerzenia",
            value=current_name,
            help_text="Rozszerzenie pliku pozostanie bez zmian.",
        ) as dialog:
            if dialog.ShowModal() != wx.ID_OK:
                return
            new_name = dialog.value
        if not new_name:
            self.announcer.say("Nowa nazwa pliku nie może być pusta")
            return

        def done(payload: object) -> None:
            data = payload if isinstance(payload, dict) else {}
            file_name = str(data.get("fileName") or new_name)
            self._refresh_profile_view(preferred_id=row.item_id)
            self.announcer.say(f"Zmieniono nazwę pliku na dysku: {file_name}")

        self.runner.submit(
            "profile-edit",
            lambda: client.rename_local_file(row.item_id, new_name),
            done,
            lambda error: self.announcer.say(f"Nie zmieniono nazwy pliku: {error}"),
        )

    def _remove_selected_items(self) -> None:
        if (
            self.navigator.active is SessionId.PODCASTS
            and self.navigator.session.library_view
            is LibraryView.PODCAST_FAVORITES
        ):
            # Wszystkie wiersze tego widoku są ulubione, więc wspólna
            # transakcja ToggleFavorites usuwa dokładnie całe zaznaczenie.
            # Dotyczy zarówno źródeł, jak i odcinków i zachowuje wielokrotny
            # wybór kontrolki Windows.
            self._toggle_podcast_favorite()
            return
        rows = [
            row for row in self._selected_action_rows()
            if row.kind not in ("parent", "folder", "playlist", "loadMore")
        ]
        if not rows:
            self.announcer.say("Brak elementu do usunięcia")
            return
        if (
            self.navigator.active is SessionId.PODCASTS
            and self.navigator.session.library_view
            is not LibraryView.PODCAST_HISTORY
            and self.navigator.session.library_view
            is not LibraryView.PODCAST_QUEUE
            and any(row.kind != "podcast" for row in rows)
        ):
            self.announcer.say("Usuń źródło z głównej listy Podcastów i YouTube")
            return
        client = self._profile_edit_client()
        if client is None:
            return
        session_id = self.navigator.active.value
        view = self._current_profile_view_name()
        item_ids = [row.item_id for row in rows]
        titles = [row.title for row in rows]

        def done(_payload: object) -> None:
            self._refresh_profile_view()
            label = titles[0] if len(titles) == 1 else format_item_count(len(titles))
            if view in (
                LibraryView.HISTORY.value,
                LibraryView.PODCAST_HISTORY.value,
            ):
                message = f"Usunięto z Historii odtwarzania: {label}. Pliki i Biblioteka pozostały bez zmian"
            elif view == LibraryView.FAVORITES.value:
                message = f"Usunięto z Ulubionych: {label}"
            elif view in (LibraryView.SAVED_QUEUE.value, LibraryView.LIVE_QUEUE.value):
                message = f"Usunięto z Kolejki: {label}"
            elif view == LibraryView.PODCAST_QUEUE.value:
                message = f"Usunięto z Kolejki podcastów: {label}"
            elif session_id == SessionId.FILES.value:
                message = f"Usunięto z Biblioteki: {label}. Plik pozostał na dysku"
            elif session_id == SessionId.PODCASTS.value:
                message = f"Usunięto z Biblioteki podcastów: {label}"
            else:
                message = f"Usunięto stację: {label}"
            self.announcer.say(message)

        self.runner.submit(
            "profile-edit",
            lambda: client.remove_profile_items(session_id, view, item_ids),
            done,
            lambda error: self.announcer.say(f"Nie usunięto: {error}"),
        )

    def _recycle_selected_files(self) -> None:
        if self.navigator.active is not SessionId.FILES:
            self.announcer.say("Przenoszenie do Kosza jest dostępne tylko dla plików lokalnych")
            return
        rows = [row for row in self._selected_action_rows() if row.kind == "track" and row.path]
        if not rows:
            self.announcer.say("Brak pliku do przeniesienia do Kosza")
            return
        label = rows[0].title if len(rows) == 1 else format_item_count(len(rows))
        if wx.MessageBox(
            f"Przenieść do Kosza: {label}?\n\n"
            "Pliki zostaną też usunięte z AMC. Można je odzyskać z systemowego Kosza.",
            "Przenieś pliki do Kosza",
            wx.YES_NO | wx.NO_DEFAULT | wx.ICON_WARNING,
            self,
        ) != wx.YES:
            return
        client = self._profile_edit_client()
        if client is None:
            return
        item_ids = [row.item_id for row in rows]

        def done(payload: object) -> None:
            data = payload if isinstance(payload, dict) else {}
            removed = data.get("removed") if isinstance(data.get("removed"), list) else []
            failures = data.get("failures") if isinstance(data.get("failures"), list) else []
            self._refresh_profile_view()
            if removed:
                removed_label = rows[0].title if len(removed) == 1 else format_item_count(len(removed))
                self.announcer.say(f"Przeniesiono do Kosza i usunięto z AMC: {removed_label}")
            if failures:
                self.announcer.say("Nie przeniesiono do Kosza: " + "; ".join(map(str, failures)))

        self.runner.submit(
            "profile-edit",
            lambda: client.recycle_local_files(item_ids),
            done,
            lambda error: self.announcer.say(f"Nie przeniesiono do Kosza: {error}"),
        )

    # ------------------------------------------------------------- stacje

    def _selected_station(self) -> Station | None:
        row = self.navigator.sessions[SessionId.RADIO].model.selected_row
        if row is None:
            return None
        saved = self.stations.find(row.item_id)
        if saved is not None:
            return saved
        # Historia i Ulubione moga pokazywac stacje spoza biezacego zakresu
        # Biblioteki. Wiersz nadal niesie zamierzona nazwe i adres; nigdy nie
        # podstawiamy technicznej reprezentacji obiektu pod etykiete NVDA.
        if row.kind == "station" and row.url:
            return Station(id=row.item_id, name=row.title, url=row.url)
        return None

    def _recording_station(self) -> Station | None:
        if self.navigator.active is not SessionId.RADIO:
            self.announcer.say(
                "Nagrywanie ręczne jest dostępne dla wybranej stacji radia internetowego"
            )
            return None
        station = self._selected_station()
        if station is None:
            self.announcer.say("Wybierz stację radiową")
        return station

    def _toggle_radio_recording(self) -> None:
        client = self.client
        station = self._recording_station()
        if client is None or station is None:
            return

        # Ustawienia mogly zostac zmienione w glownym AMC juz po starcie wx.
        # Czytamy je ponownie bez zapisu i nie podmieniamy widoku listy.
        snapshot = self.radio.load(previous=self._radio_snapshot)
        payload = snapshot.recording.payload_for(station)

        def done(result: dict) -> None:
            # Parzystosc ze starym AMC: gest Ctrl+R od razu daje jedno
            # krotkie potwierdzenie. Zdarzenie recordingStarted uaktualnia
            # liste, ale dla nagrania recznego nie powtarza komunikatu ani
            # nazwy pliku. Blad uruchomienia nadal przyjdzie osobnym
            # recordingFailed, wiec nie udajemy, ze plik juz istnieje.
            action = str((result or {}).get("action") or "")
            if action == "starting":
                self.announcer.say(
                    f"Rozpoczynam nagrywanie w tle: {station.name}"
                )
            elif action == "stopping":
                self.announcer.say(f"Zatrzymuję nagrywanie: {station.name}")
            self._refresh_recording_status()

        self.runner.submit(
            "radio-recording-command",
            lambda: client.toggle_radio_recording(payload),
            done,
            lambda error: self.announcer.say(f"Nie można zmienić nagrywania: {error}"),
        )

    def _toggle_radio_recording_pause(self) -> None:
        client = self.client
        station = self._recording_station()
        if client is None or station is None:
            return

        def done(result: dict) -> None:
            state = str((result or {}).get("state") or "")
            if state == "paused":
                position = format_duration((result or {}).get("positionSeconds"))
                suffix = (
                    ". Zapis oryginalnego strumienia pozostał aktywny"
                    if int((result or {}).get("unpausableCount") or 0) else ""
                )
                self.announcer.say(
                    f"Wstrzymano nagrywanie: {station.name}, {position}{suffix}"
                )
            elif state == "recording":
                self.announcer.say(f"Wznowiono nagrywanie: {station.name}")
            elif state == "notRecording":
                self.announcer.say(f"Stacja nie jest nagrywana: {station.name}")
            elif state == "starting":
                self.announcer.say("Nagranie jeszcze się uruchamia")
            elif state == "unsupported":
                self.announcer.say(
                    str((result or {}).get("reason") or "Pauza jest niedostępna")
                )
            else:
                self.announcer.say(
                    "Trwa finalizowanie albo rozpoczynanie części nagrania"
                )

        self.runner.submit(
            "radio-recording-command",
            lambda: client.toggle_radio_recording_pause(station.id, station.url),
            done,
            lambda error: self.announcer.say(
                f"Nie można zmienić pauzy nagrania: {error}"
            ),
        )

    def _split_radio_recording(self) -> None:
        client = self.client
        station = self._recording_station()
        if client is None or station is None:
            return

        def done(result: dict) -> None:
            state = str((result or {}).get("state") or "")
            if state == "split":
                path = str((result or {}).get("currentPath") or "").strip()
                suffix = f": {Path(path).name}" if path else ""
                self.announcer.say(
                    f"Rozpoczęto nową część nagrania {station.name}{suffix}"
                )
            elif state == "tooSoon":
                self.announcer.say(
                    "Nowa część nagrania już trwa. Ponowne T pominięte"
                )
            elif state == "notRecording":
                self.announcer.say(f"Stacja nie jest nagrywana: {station.name}")
            elif state == "starting":
                self.announcer.say("Nagranie jeszcze się uruchamia")
            elif state == "stopping":
                self.announcer.say("Nagranie jest już zatrzymywane")
            else:
                self.announcer.say(
                    "Trwa finalizowanie albo rozpoczynanie części nagrania"
                )

        self.announcer.say(f"Zapisuję bieżącą część nagrania: {station.name}")
        self.runner.submit(
            "radio-recording-command",
            lambda: client.split_radio_recording(station.id, station.url),
            done,
            lambda error: self.announcer.say(f"Nie można podzielić nagrania: {error}"),
        )

    def _stop_all_radio_recordings(self) -> None:
        client = self.client
        if client is None:
            return

        def done(result: dict) -> None:
            count = int((result or {}).get("stopping") or 0)
            if count == 0:
                self.announcer.say("Brak trwających nagrań")
            elif count == 1:
                self.announcer.say("Zatrzymuję nagrywanie")
            else:
                self.announcer.say(f"Zatrzymuję wszystkie nagrania: {count}")

        self.runner.submit(
            "radio-recording-command",
            client.stop_all_radio_recordings,
            done,
            lambda error: self.announcer.say(f"Nie można zatrzymać nagrań: {error}"),
        )

    def _show_active_radio_recordings(self, *, announce: bool = True) -> None:
        """Pokaz stan hosta, bez ujawniania technicznych identyfikatorow."""
        client = self.client
        if client is None:
            if announce:
                self.announcer.say("Silnik odtwarzania jest niedostępny")
            return
        if announce:
            self._begin_transient_preview()
            # Sam wynik wypowie nazwe podgladu. Osobne "Radio internetowe"
            # przed nim byloby podwojnym komunikatem po jednym Alt+R.
            self.navigator.active = SessionId.RADIO
        preferred_id = self.navigator.sessions[SessionId.RADIO].model.selected_id

        def done(payload: dict) -> None:
            if announce and self._transient_preview_return is None:
                return
            rows = active_recording_rows(payload)
            events = self.navigator.apply_radio_view(
                LibraryView.ACTIVE_RADIO_RECORDINGS,
                "Nagrywane",
                rows,
                preferred_id=preferred_id,
            )
            if self.navigator.active is not SessionId.RADIO:
                return
            if announce:
                if rows:
                    self._run(events)
                else:
                    # Najpierw opróżniamy natywna liste, a dopiero potem
                    # wypowiadamy wynik. Gdy komunikat szedl przed zmiana
                    # kontrolki, zdarzenie usuniecia ostatniego wiersza
                    # dopisywalo na koncu mylace "nieznane".
                    self._sync_views()
                    self._announce_after_native_list_update(events[0].text)
            else:
                # Odswiezenie po zdarzeniu nie moze zagluszac komunikatu o
                # starcie, zatrzymaniu lub bledzie nagrania.
                self._sync_views()

        def failed(error: Exception) -> None:
            if announce and self.navigator.active is SessionId.RADIO:
                self._restore_transient_preview(announce=False, force=True)
                self.announcer.say(f"Nie można wczytać trwających nagrań: {error}")

        self.runner.submit(
            "radio-recordings-view",
            client.radio_recording_status,
            done,
            failed,
        )

    def _remember_recording_result(self, payload: object) -> bool:
        """Utrwal wynik w prywatnym stanie bez zmiany profilu pelnego AMC."""
        entry = recording_history_payload_from_event(payload)
        if entry is None:
            return False
        history = [
            item for item in getattr(self.state, "recording_history", [])
            if isinstance(item, dict) and item.get("id") != entry["id"]
        ]
        self.state.recording_history = [entry, *history][:1_000]
        try:
            self.store.save(self.state)
        except (OSError, TypeError, ValueError):
            # Komunikat o zakonczeniu nagrania pozostaje pojedynczy. Problem
            # zapisu wyjasnimy po otwarciu Historii, gdzie jest istotny.
            self._recording_history_persist_error = True
            return False
        self._recording_history_persist_error = False
        return True

    def _show_radio_recording_history(self, *, announce: bool = True) -> None:
        """Alt+Shift+R: utrwalone wyniki prob, razem z nieudanymi."""
        if announce:
            self._begin_transient_preview()
            self.navigator.active = SessionId.FILES
        previous = self._radio_snapshot
        client = self.client
        private_history = list(getattr(self.state, "recording_history", []))
        persistence_error = bool(
            getattr(self, "_recording_history_persist_error", False)
        )

        def work():
            snapshot = self.radio.load(previous=previous)
            current_entries = ()
            current_error = ""
            recorded_files = ()
            library_error = ""
            if client is not None:
                try:
                    payload = client.radio_recording_history()
                    current_entries = recording_history_from_amc_state({
                        "radio": {
                            "recordingHistory": payload.get("recordings", [])
                            if isinstance(payload, dict) else []
                        }
                    })
                except (HostError, HostUnavailable, OSError) as error:
                    current_error = str(error)
            private_entries = recording_history_from_amc_state({
                "radio": {"recordingHistory": private_history}
            })
            library = getattr(self, "library", None)
            if library is not None and library.is_available:
                try:
                    recorded_files = tuple(library.recorded_radio_items())
                except (OSError, sqlite3.Error) as error:
                    library_error = str(error)
            combined = list(current_entries)
            known_ids = {entry.id for entry in combined}
            for entry in (*private_entries, *snapshot.recording_history):
                if entry.id in known_ids:
                    continue
                known_ids.add(entry.id)
                combined.append(entry)
            return (
                snapshot,
                recording_history_rows(combined, recorded_files=recorded_files),
                current_error,
                library_error,
                persistence_error,
            )

        def done(result) -> None:
            if announce and self._transient_preview_return is None:
                return
            if not announce and (
                self.navigator.active is not SessionId.FILES
                or self.navigator.sessions[SessionId.FILES].library_view
                is not LibraryView.RECORDED_RADIO_FILES
            ):
                return
            snapshot, rows, current_error, library_error, history_save_error = result
            self._radio_snapshot = snapshot
            self.stations = snapshot.list
            if announce and snapshot.load_error and not rows:
                self._restore_transient_preview(announce=False, force=True)
                self.announcer.say(snapshot.load_error)
                return
            events = self.navigator.apply_library_view(
                LibraryView.RECORDED_RADIO_FILES,
                "Historia nagrywania",
                rows,
            )
            if self.navigator.active is SessionId.FILES:
                if announce:
                    self._run(events)
                else:
                    self._sync_views()
                if announce and snapshot.load_error:
                    self.announcer.say(snapshot.load_error)
                if announce and current_error:
                    self.announcer.say(
                        "Historia bieżącej sesji nagrywania jest chwilowo niedostępna"
                    )
                if announce and library_error:
                    self.announcer.say(
                        "Lista zapisanych plików nagrań jest chwilowo niedostępna"
                    )
                if announce and history_save_error:
                    self.announcer.say(
                        "Nie udało się zapisać prywatnej historii nagrywania"
                    )

        def failed(error: Exception) -> None:
            if announce:
                self._restore_transient_preview(announce=False, force=True)
                self.announcer.say(f"Nie można wczytać historii nagrywania: {error}")

        self.runner.submit("radio-recording-history", work, done, failed)

    def _show_radio_schedules(self) -> None:
        """Ctrl+Shift+H: natywne zarzadzanie planami wykonywanymi przez host."""
        client = self.client
        if client is None:
            self.announcer.say("Silnik odtwarzania jest niedostępny")
            return
        previous = self._radio_snapshot

        def work():
            snapshot = self.radio.load(previous=previous)
            schedules = radio_schedule_settings.effective_schedules(
                snapshot.recording_schedules,
                self.state.radio_schedule_overrides,
            )
            wake = radio_schedule_settings.effective_wake(
                snapshot.wake_scheduled_recordings,
                self.state.radio_schedule_wake_override,
            )
            payload = client.sync_radio_schedules(
                self._radio_schedule_sync_payload(snapshot, self.state)
            )
            return snapshot, schedules, wake, payload

        def done(result) -> None:
            snapshot, schedules, wake, payload = result
            self._radio_snapshot = snapshot
            self.stations = snapshot.list
            schedules = radio_schedule_settings.schedules_from_host_status(
                payload, schedules
            )
            if self.state.radio_schedule_overrides is not None:
                # Host jest zrodlem prawdy o wykonanym terminie: po nagraniu
                # jednorazowym wylacza plan, a cykliczny przesuwa. Utrwalamy
                # te zmiany w prywatnym pliku wxPython, nie w profilu WPF.
                previous_effective = self.state.radio_schedule_overrides
                self.state.radio_schedule_overrides = schedules
                try:
                    self.store.save(self.state)
                except Exception:
                    self.state.radio_schedule_overrides = previous_effective
                    self.announcer.say(
                        "Nie udało się zapisać aktualnego terminu harmonogramu"
                    )
            if snapshot.load_error and not schedules:
                self.announcer.say(snapshot.load_error)
                return

            def commit(updated: list[dict], updated_wake: bool):
                old_schedules = self.state.radio_schedule_overrides
                old_wake = self.state.radio_schedule_wake_override
                self.state.radio_schedule_overrides = (
                    radio_schedule_settings.clone_schedules(updated)
                )
                self.state.radio_schedule_wake_override = bool(updated_wake)
                try:
                    self.store.save(self.state)
                    synced = client.sync_radio_schedules(
                        self._radio_schedule_sync_payload(snapshot, self.state)
                    )
                    self.state.radio_schedule_overrides = (
                        radio_schedule_settings.schedules_from_host_status(
                            synced, self.state.radio_schedule_overrides
                        )
                    )
                    self.store.save(self.state)
                    return synced
                except Exception:
                    # Zapis i wykonawca sa jedna zmiana z punktu widzenia
                    # uzytkownika. Gdy ktorykolwiek etap zawiedzie, wracamy do
                    # poprzednich danych i probujemy przywrocic je hostowi.
                    self.state.radio_schedule_overrides = old_schedules
                    self.state.radio_schedule_wake_override = old_wake
                    try:
                        self.store.save(self.state)
                    except Exception:
                        pass
                    try:
                        client.sync_radio_schedules(
                            self._radio_schedule_sync_payload(snapshot, self.state)
                        )
                    except Exception:
                        pass
                    raise

            with RadioSchedulesDialog(
                self,
                stations=snapshot.stations,
                recording=snapshot.recording,
                schedules=schedules,
                wake_scheduled_recordings=wake,
                labels_payload=payload,
                commit=commit,
            ) as dialog:
                dialog.ShowModal()
            if self.navigator.view is View.LIST:
                self._active_list().SetFocus()
            else:
                self.play_button.SetFocus()
            if snapshot.load_error:
                self.announcer.say(snapshot.load_error)

        def failed(error: Exception) -> None:
            self.announcer.say(f"Nie można wczytać harmonogramu nagrywania: {error}")

        self.runner.submit("radio-schedules", work, done, failed)

    @staticmethod
    def _radio_schedule_sync_payload(snapshot, state: LiteState | None = None) -> dict:
        """Efektywne plany ida do hosta; profil pelnego AMC pozostaje read-only."""
        recording = snapshot.recording
        schedules = radio_schedule_settings.effective_schedules(
            snapshot.recording_schedules,
            state.radio_schedule_overrides if state is not None else None,
        )
        wake = radio_schedule_settings.effective_wake(
            snapshot.wake_scheduled_recordings,
            state.radio_schedule_wake_override if state is not None else None,
        )
        return {
            "schedules": schedules,
            "defaultFolder": recording.default_folder or "",
            "folderPreset": recording.folder_preset,
            "stationFolders": dict(recording.station_folders),
            "recordingFormat": recording.format,
            "recordingBitrateKbps": recording.bitrate_kbps,
            "wakeScheduledRecordings": wake,
        }

    def _refuse_station_edit(self) -> bool:
        """Odmowa edycji stacji we wspolnym profilu. ``True`` = nie kontynuuj.

        Wlascicielem ``state.json`` jest host C#. Zamiast cichego "zapisalem"
        mowimy wprost, gdzie zmieniac stacje.
        """
        if self.radio.may_edit:
            return False
        self._switch_session(SessionId.RADIO)
        self.announcer.say(self.radio.edit_refusal_reason())
        return True

    def _reload_stations(self, preferred_id: str | None = None) -> None:
        rows = rows_from_stations(self.stations.as_payload())
        if self.radio.may_edit:
            self._radio_snapshot.current_id = preferred_id or self._radio_snapshot.current_id
            self.radio.save(self._radio_snapshot)
        self._run(self.navigator.apply_stations(rows, preferred_id=preferred_id))

    def _station_add(self) -> None:
        if self._refuse_station_edit():
            return
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
        if not self.radio.may_edit:
            client = self._profile_edit_client()
            if client is None:
                return

            def done(_payload: object) -> None:
                self._refresh_profile_view(preferred_id=station.id)
                self.announcer.say(f"Zapisano nazwę i adres stacji: {name}")

            self.runner.submit(
                "profile-edit",
                lambda: client.edit_radio_station(station.id, name, url),
                done,
                lambda error: self.announcer.say(f"Nie zapisano stacji: {error}"),
            )
            return
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
        if not self.radio.may_edit:
            client = self._profile_edit_client()
            if client is None:
                return
            view = self._current_profile_view_name()

            def done(_payload: object) -> None:
                self._refresh_profile_view()
                self.announcer.say(f"Usunięto {station.name}")

            self.runner.submit(
                "profile-edit",
                lambda: client.remove_profile_items("radio", view, [station.id]),
                done,
                lambda error: self.announcer.say(f"Nie usunięto stacji: {error}"),
            )
            return
        self.stations.remove(station.id)
        self._reload_stations()
        self.announcer.say(f"Usunieto {station.name}")

    def _station_import(self) -> None:
        """Import M3U/PLS ISTNIEJACYM importerem AMC (w hoscie).

        Plik uzytkownika czytamy TYLKO po jego wskazaniu - nic nie jest
        przenoszone ani kopiowane automatycznie.
        """
        if self._refuse_station_edit():
            return
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

    def _save_state(self) -> bool:
        """Utrwal prywatny stan. ``False`` = dysk ODMOWIL zapisu.

        Wynik jest istotny: wolacz, ktory wlasnie zmienil stan w pamieci i w
        silniku, musi umiec rozpoznac odmowe i wrocic do stanu trwalego.
        Bez tego uzytkownik slyszal "Nie moge zapisac" i zaraz po nim
        "Zapisano", a profil rozjezdzal sie z tym, co naprawde gra.
        """
        self.state.options = self.options.clamp()
        # Stacji NIE dopisujemy do prywatnego stanu: we wspolnym profilu naleza
        # do AMC, a w piaskownicy zapisuje je RadioSource.
        self.state.navigation = self.navigator.snapshot()
        try:
            self.store.save(self.state)
        except OSError as error:
            self.announcer.say(f"Nie moge zapisac ustawien: {error}")
            return False
        return True

    def _on_close(self, event: wx.CloseEvent) -> None:
        self.timer.Stop()
        # Dlugie operacje fragmentu sa jawnie anulowane przed EOF hosta.
        # Edytory C# usuwaja swoje pliki techniczne i nie dotykaja oryginalu
        # przed zweryfikowanym, atomowym etapem podmiany.
        if self.client is not None:
            operation_ids = {
                value
                for value in (
                    self._audio_clip_export_operation_id,
                    self._audio_clip_edit_operation_id,
                )
                if value
            }
            for operation_id in operation_ids:
                try:
                    self.client.cancel_audio_clip(operation_id)
                except Exception:
                    # ``client.close`` ma jeszcze awaryjne zamkniecie procesu;
                    # okna nie blokujemy osobnym komunikatem podczas wyjscia.
                    pass
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
