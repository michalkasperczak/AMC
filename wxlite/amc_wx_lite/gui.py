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

Czego tu nie ma swiadomie: WebView2, Sonos, harmonogramow, uslug startowych.
Lekki wariant ma byc lekki; pelny AMC zostaje nietkniety.
"""

from __future__ import annotations

import os
from pathlib import Path

import wx

from .async_gate import BackgroundRunner, StaleResultGate
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
from .navigation import (
    Announce,
    LIBRARY_VIEW_FOLDERS,
    LibraryView,
    Navigator,
    OpenFolder,
    OpenLibraryView,
    OpenQueueView,
    PlayFromQueue,
    PlayQueueAt,
    PlayStation,
    PlayTrack,
    SessionId,
    View,
    view_context,
)
from .shortcuts import Action, Chord, describe, resolve
from .profile_layout import resolve_layout
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
from .radio_source import RadioSource
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


class MediaListAccessible(wx.Accessible):
    """Udostepnia przez MSAA nazwe i role samej kontrolki listy.

    Sonda MSAA wykazala brak accName klienta SysListView32 mimo SetName;
    z ta nakladka accName jest dostepne. Nie jest to jeszcze dowod usuniecia
    komunikatu "nieznane" przy fizycznym wejsciu na pusta liste z NVDA.
    Dla dzieci pozostaje dotychczasowa implementacja natywna. Kwity pomiaru
    interfejsu: list-speech-after422/zdarzenia15.jsonl.
    """

    def __init__(self, window: wx.Window) -> None:
        super().__init__(window)
        self._window = window

    def GetName(self, childId):  # noqa: N802 - API wx
        if childId == 0:
            name = self._window.GetName()
            if name:
                return (wx.ACC_OK, name)
        return (wx.ACC_NOT_IMPLEMENTED, "")

    def GetRole(self, childId):  # noqa: N802 - API wx
        # Rola 0 na pustej liscie byla drugim polem objawu F03.
        if childId == 0:
            return (wx.ACC_OK, wx.ROLE_SYSTEM_LIST)
        return (wx.ACC_NOT_IMPLEMENTED, wx.ROLE_NONE)


#: Stale MSAA (winuser.h). Uzywamy ich do JEDNEGO zdarzenia: zejscia listy do
#: zera wierszy. Nazwy wlasne, zeby nie wiazac sie z wersja ``comtypes``.
EVENT_OBJECT_FOCUS = 0x8005
OBJID_CLIENT = -4
CHILDID_SELF = 0


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
    ``ListModel``, ta sama nakladka nazwy/roli, ta sama trwala kontrolka (bez
    ``Destroy``/rekreacji HWND -- ta droga zostala zmierzona i wycofana).
    Strzalki, Home/End i pisanie-po-pierwszej-literze naleza do kontrolki;
    na zwyklej liscie dziala to natywnie, bez naszego udzialu.

    Czego ten kod NIE robi: nie dotyka czytnika ekranu. Mniej operacji na
    kontrolce to mniej zdarzen a11y, ale dowodem mowy jest zywy NVDA.
    """

    def __init__(self, parent: wx.Window, model: ListModel, label: str,
                 state: object | None = None) -> None:
        super().__init__(
            parent,
            style=wx.LC_REPORT | wx.LC_SINGLE_SEL | wx.BORDER_SUNKEN,
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
        #: Czy OSTATNI przebieg skonczyl sie pusta lista. ``None`` = jeszcze nie
        #: synchronizowano. Zapowiedz pustki nalezy do PRZEJSCIA do zera
        #: wierszy; bez tego tick statusu powtarzalby ja przy kazdym przebiegu.
        self._was_empty: bool | None = None
        #: TEKST FILTRA obowiazujacy dla tej listy. Ustawia go okno przed
        #: ``sync_rows`` (jedno pole dla wszystkich list, jak ``FilterBox``).
        #: Pusty = zachowanie dokladnie takie jak przed dodaniem filtra.
        self.filter_query: str = ""
        # Nazwa dla czytnika ekranu i Narratora.
        self.SetName(label)
        # Bez tego nazwa wyzej NIE dociera do MSAA (zmierzone -- patrz
        # ``MediaListAccessible``).
        self.SetAccessible(MediaListAccessible(self))

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
        # Przejscie do pustki liczymy PRZED zmiana stanu, zeby zapowiedz dotyczyla
        # PRZEJSCIA (pusto->pusto na ticku statusu to ZERO zdarzen).
        became_empty = not desired and self._was_empty is not True
        self._was_empty = not desired
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
                if became_empty:
                    # Wyjatek: pierwsze wejscie od razu w pusty widok. Planu nie
                    # ma (nie bylo czego usuwac), ale obiekt dostepny sie zmienil.
                    self._announce_empty_list()
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
        # Dopiero po odmrozeniu: zdarzenie ma opisywac stan KONCOWY.
        if became_empty:
            self._announce_empty_list()

    def _announce_empty_list(self) -> None:
        """Po zejsciu do ZERA wierszy oglos SAMA LISTE jako dostepny obiekt.

        PO CO TO JEST (zmierzone, nie wywnioskowane). Na zywym NVDA wejscie w
        widok bez wierszy dawalo ``name=''`` i ``role=0``, choc sama kontrolka
        oddaje ``accName`` i ``accRole=33``. A/B z kontrolka standardowa na tej
        samej ``MediaListCtrl`` pokazalo roznice: nasza droga do zera to petla
        ``DeleteItem``, a SysListView32 przy kazdym usunieciu wiersza PRZED
        kursorem przesuwa fokus na nizszy indeks -- wiec na liscie konczacej z
        zerem wierszy poszly ``EVENT_OBJECT_FOCUS`` z ``idChild=2``, potem
        ``idChild=1``, czyli na dzieci, ktorych po oproznieniu NIE MA
        (``get_accChild(1)`` -> ``0x80070057``). ``DeleteAllItems`` nie wysyla
        wtedy nic i ostatnim zdarzeniem zostaje fokus z czasow niepustej listy.
        W obu drogach czytnik trzyma USUNIETE dziecko: lista ma fokus
        klawiatury, a ``accFocus`` jest VT_EMPTY, bo dziecka z fokusem nie ma.

        CO TU ROBIMY: wysylamy JEDNO zdarzenie na ``CHILDID_SELF`` -- zgloszenie
        realnej zmiany dostepnego obiektu, ktorym jest teraz sama lista. Fokus
        klawiatury JUZ na niej jest (``HasFocus``), wiec nie przestawiamy
        niczego i nie udajemy danych. Czego tu nie ma: sztucznych wierszy,
        uciszania czytnika, globalnych hookow i odtwarzania HWND.

        Zdarzenie leci TYLKO gdy lista ma fokus klawiatury -- zapowiedz dotyczy
        tego, co uzytkownik ma pod reka; lista w ukrytym panelu tez przechodzi
        przez ``sync_rows``.
        """
        if not self.HasFocus():
            return
        try:
            self._notify(EVENT_OBJECT_FOCUS, int(self.GetHandle()),
                         OBJID_CLIENT, CHILDID_SELF)
        except Exception:
            # Zapowiedz jest DODATKIEM. Lista musi sie opruznic nawet gdy MSAA
            # odmowi -- blad powiadomienia nie moze wywrocic aktualizacji GUI.
            pass

    @staticmethod
    def _notify(event: int, hwnd: int, obj_id: int, child_id: int) -> None:
        """Cienka osloda na ``NotifyWinEvent``. Osobno, zeby test ja podmienil."""
        import ctypes

        ctypes.windll.user32.NotifyWinEvent(  # type: ignore[attr-defined]
            ctypes.c_uint(event), ctypes.c_void_p(hwnd),
            ctypes.c_long(obj_id), ctypes.c_long(child_id))

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
        return list_sync.plan_cursor(
            wanted=self._wanted_visible_index(),
            selected=self.GetFirstSelected(),
            focused=self.GetFocusedItem(),
        )

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
        if self.GetFirstSelected() != index:
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
        # Przelaczniki komunikatow i czas wlasny z PRAWDZIWEGO profilu AMC.
        # Tylko odczyt: wlascicielem state.json zostaje host C#.
        self.messages: MessagePolicy = load_message_policy(self.layout)
        self.radio = RadioSource(self.layout)
        self._radio_snapshot = self.radio.load()
        self.stations = self._radio_snapshot.list
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
        self.radio_list.Hide()
        list_sizer = wx.BoxSizer(wx.VERTICAL)
        list_sizer.Add(self.filter_label, 0, wx.BOTTOM, 3)
        list_sizer.Add(self.filter_box, 0, wx.EXPAND | wx.BOTTOM, 8)
        list_sizer.Add(self.files_list, 1, wx.EXPAND)
        list_sizer.Add(self.radio_list, 1, wx.EXPAND)
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
        # "Cos gra" rozpoznajemy po TYM SAMYM statusie z hosta, z ktorego
        # korzysta ``_announce_time`` -- nie po wlasnym liczniku.
        playing = bool(self._last_status)
        session = self.navigator.sessions[self.navigator.active]
        has_row = session.model.selected_row is not None
        for item, entry in self._menu_items:
            enabled = True
            if entry.needs_radio_session and not radio:
                enabled = False
            if entry.needs_playback and not playing:
                enabled = False
            if entry.needs_selection and not has_row:
                enabled = False
            if item.IsEnabled() != enabled:
                item.Enable(enabled)

    def _bind_list(self, control: MediaListCtrl) -> None:
        """Wspolne powiazania klawiatury i wyboru dla list plikow i radia."""
        control.Bind(wx.EVT_KEY_DOWN, self._on_key)
        control.Bind(wx.EVT_LIST_ITEM_ACTIVATED, lambda _e: self._activate())
        control.Bind(wx.EVT_LIST_ITEM_SELECTED, self._on_item_selected)

    def _bind_keys(self) -> None:
        self.Bind(wx.EVT_CHAR_HOOK, self._on_player_shortcut_hook)
        for control in (self.files_list, self.radio_list):
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
        for control in (self.files_list, self.radio_list):
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
        client = LiteHostClient(
            default_host_path(),
            timeshift_minutes=self.options.timeshift_minutes,
            on_event=self._on_engine_event,
            on_stderr=lambda line: None,
            **persistence,  # type: ignore[arg-type]
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
            # Kolejnosc listy liczy host C# (CompareInfo pl-PL), nie wlasny
            # collator w Pythonie. Podpinamy ja DOPIERO tu, bo wymaga zywego
            # silnika. Bez niej LibrarySource swiadomie oddaje kolejnosc z
            # SQL-a zamiast udawac zgodnosc.
            self.library.use_collation(HostCollation(client.call))
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
        if name == "playback.started":
            # POTWIERDZENIE startu. Dopiero teraz material jest "biezacy" dla
            # Ctrl+B: samo wyslanie ``files.play`` jeszcze niczego nie dowodzi.
            # Pole nazywa sie ``id`` (LiteEngineHandlers.cs:54), nie
            # ``itemId``, i czesto niesie ``file:<path>``, bo tak host sklada
            # Id przy przegladaniu folderow -- a to NIE jest profilowe Id.
            # Dlatego tozsamosc bierzemy z kandydata zapisanego przy zlecaniu,
            # a z hosta tylko FAKT, ze start sie udal.
            if data.get("engine") == "files":
                self.navigator.note_playback_started()
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
        elif name == "queue.advanced":
            # NATURALNE przejscie policzone przez sesje Core po stronie hosta.
            # GUI tylko odwzorowuje to, co host NAPRAWDE zaczal grac.
            self._run(self.navigator.note_queue_advanced(
                str(data.get("id") or ""), str(data.get("title") or "")
            ))
        elif name == "playback.failed":
            self._run(self.navigator.note_playback_failed(
                f"Nie udalo sie odtworzyc: {data.get('message', 'blad')}"
            ))
        elif name == "radio.nowPlaying":
            title = str(data.get("streamTitle") or "").strip()
            if title:
                self.now_playing.SetLabel(title)
                self.announcer.say(title)
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
        # Blad ODCZYTU stacji nie jest tym samym co profil bez stacji. Dopoki
        # RadioSource oddawalo pusta liste w obu przypadkach, uzytkownik slyszal
        # cisze takze wtedy, gdy profil byl uszkodzony albo zajety.
        if self._radio_snapshot.load_error:
            self.announcer.say(self._radio_snapshot.load_error)
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

        def work():
            from .radio_views import load_view
            return load_view(source, scope)

        def done(result) -> None:
            if result.unavailable_reason:
                if self.navigator.active is SessionId.RADIO:
                    self.announcer.say(result.unavailable_reason)
                return
            events = self.navigator.apply_radio_view(
                intent.view, result.heading, result.rows, preferred_id=intent.preferred_id)
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

        # Native dialog processing on a button consumes player keys before
        # KEY_DOWN. The existing resolver passes unknown keys (e.g. Tab) on.
        if self.navigator.view is not View.PLAYER:
            event.Skip()
            return
        focus = wx.Window.FindFocus()
        if focus is None or wx.GetTopLevelParent(focus) is not self:
            event.Skip()
            return
        self._on_key(event)

    def _on_key(self, event: wx.KeyEvent) -> None:
        chord = chord_from_event(event)
        player = self.navigator.view is View.PLAYER
        radio = self.navigator.active is SessionId.RADIO
        # WPF czyści aktywny filtr także wtedy, gdy fokus jest już na wynikach.
        if not player and chord.canonical == "Escape" and self.filter_box.GetValue():
            self._clear_filter_and_return()
            return
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
        elif action in (Action.QUEUE_NEXT, Action.QUEUE_PREVIOUS):
            self._queue_step(action is Action.QUEUE_NEXT)
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
        elif action is Action.COPY_NAME:
            self._copy_name()
        elif action is Action.COPY_ADDRESS:
            self._copy_address()
        elif action is Action.CUT_FILE:
            self._cut_file()
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
        elif action is Action.VIEW_ALL_FILES:
            self._run(self.navigator.open_library_view(LibraryView.ALL_FILES))
        elif action is Action.VIEW_FAVORITES:
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
            self._run(self.navigator.open_library_view(LibraryView.HISTORY))
        elif action is Action.VIEW_SAVED_QUEUE:
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
            elif isinstance(intent, PlayTrack):
                self._play_track(intent)
            elif isinstance(intent, PlayFromQueue):
                self._play_from_queue(intent)
            elif isinstance(intent, PlayQueueAt):
                self._play_queue_at(intent)
            elif isinstance(intent, PlayStation):
                self._play_station(intent)
        self._sync_views()

    def _sync_views(self) -> None:
        """Odwzoruj stan nawigatora. Fokus ruszamy TYLKO przy zmianie widoku."""
        session = self.navigator.session
        # Menu musi zgadzac sie z kontekstem, ktory wlasnie sie zmienil.
        self._refresh_menu_state()
        self.session_label.SetLabel(
            "Pliki lokalne" if self.navigator.active is SessionId.FILES else "Radio internetowe"
        )

        active_list = self._active_list()
        other_list = self.radio_list if active_list is self.files_list else self.files_list
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

    def _copy_name(self) -> None:
        """Ctrl+C: nazwa zaznaczonego elementu do schowka.

        Odpowiednik ``CopyActionItemName`` (MainWindow.xaml.cs:20717-20724).
        Komunikat "Skopiowano nazwę" z MainWindow.xaml.cs:24372.
        """
        row = self.navigator.session.model.selected_row
        if row is None or not row.title:
            self.announcer.say("Nie ma czego skopiować")
            return
        if self._to_clipboard(row.title):
            self.announcer.say(COPIED_NAME_MESSAGE)

    def _copy_address(self) -> None:
        """Ctrl+Shift+C: adres albo PRAWDZIWY PLIK zaznaczonego elementu.

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
        row = self.navigator.session.model.selected_row
        if row is None:
            self.announcer.say("Nie ma czego skopiować")
            return
        address = row.address
        if not address:
            self.announcer.say("Ten element nie ma zapisanego adresu")
            return
        drop = self._file_drop_path(address)
        if drop is not None:
            # Tekst sciezki idzie w postaci sprowadzonej do zwyklej sciezki --
            # tak jak w oryginale, gdzie do tekstu trafia ``localPaths``
            # (wynik ``TryGetLocalPath``), a nie surowe ``item.Source``.
            if self._to_clipboard(drop, file_path=drop):
                self.announcer.say(copied_address_message(row, file_copied=True))
            return
        if self._to_clipboard(address):
            self.announcer.say(copied_address_message(row))

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
        """Ctrl+X: plik GOTOWY DO PRZENIESIENIA poza AMC.

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
        row = self.navigator.session.model.selected_row
        if row is None:
            self.announcer.say("Brak pliku do wycięcia")
            return
        # Widok zakladek: wiersz wskazuje ``bookmark:<id>``, czyli ani plik,
        # ani sciezke (:25280-25284).
        if self.navigator.session.library_view is LibraryView.ALL_BOOKMARKS:
            self.announcer.say("Wycinanie plików nie działa na liście zakładek")
            return
        path = self._cut_file_path(row.address)
        if path is None:
            self.announcer.say(
                "Wycinanie jest dostępne tylko dla istniejących plików lokalnych")
            return
        payload = file_cut_clipboard_payload(path)
        if not self._to_clipboard(
            payload["text"],
            file_path=payload["file_path"],
            preferred_drop_effect=payload["preferred_drop_effect"],
        ):
            return
        # Zapamietujemy OCZEKUJACE przeniesienie, tak jak oryginal. Samo
        # zapamietanie nic nie usuwa -- sluzy pozniejszemu rozpoznaniu, ze
        # plik juz nie lezy pod stara sciezka.
        self.pending_external_moves[row.item_id] = path
        self.announcer.say("Plik gotowy do przeniesienia. Wklej go w folderze docelowym")

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
        preferred_drop_effect: int | None = None,
    ) -> bool:
        """Zapis do schowka Windows. Porazke MOWIMY, nie udajemy sukcesu.

        Schowek bywa chwilowo zajety przez inny proces -- odpowiednik
        ``ClipboardRetry`` z AMC, ktory tez zwraca komunikat bledu zamiast
        komunikatu sukcesu.

        ``file_path`` ustawia DODATKOWO format plikowy (``wx.FileDataObject``,
        czyli ``CF_HDROP``) obok tekstu -- jak ``SetFileDropList`` w oryginale.
        Bez niego zostaje JEDEN format tekstowy, dokladnie jak dotad; dlatego
        ``Ctrl+C`` (nazwa) i stacje nie zmieniaja zachowania ani o jotę.
        """
        try:
            if not wx.TheClipboard.Open():
                self.announcer.say("Schowek jest zajęty, spróbuj ponownie")
                return False
            try:
                if file_path is None:
                    data = wx.TextDataObject(text)
                else:
                    # Kolejnosc jak w oryginale: najpierw tekst (``UnicodeText``),
                    # potem file drop. Preferowany jest format PLIKOWY -- o niego
                    # chodzi w zgloszeniu, a odbiorcy tekstowi (edytor, pole
                    # wyszukiwania) i tak wezma galaz tekstowa.
                    data = wx.DataObjectComposite()
                    data.Add(wx.TextDataObject(text))
                    files = wx.FileDataObject()
                    files.AddFile(file_path)
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
            self._refresh_status()

        def failed(error: Exception) -> None:
            self._run(self.navigator.note_playback_failed(f"Nie udalo sie odtworzyc: {error}"))

        self.runner.submit("playback", work, done, failed)

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
        """Page Down / Page Up: nastepny albo poprzedni utwor ZYWEJ kolejki.

        Skok liczy kolejka hosta. Gdy kolejka nie prowadzi odtwarzania (zwykle
        ``files.play``, radio, zakladka) albo nie ma gdzie isc, host odmawia, a
        my mowimy to wprost -- zamiast milczec albo udawac zmiane utworu.
        """
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
            self._refresh_status()

        def failed(error: Exception) -> None:
            self._run(self.navigator.note_playback_failed(f"Nie moge polaczyc ze stacja: {error}"))

        self.runner.submit("playback", work, done, failed)

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

        self.runner.submit("status", client.status, done, lambda _error: None)

    # ------------------------------------------------------------- stacje

    def _selected_station(self) -> Station | None:
        row = self.navigator.sessions[SessionId.RADIO].model.selected_row
        return self.stations.find(row.item_id) if row is not None else None

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
        if self._refuse_station_edit():
            return
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
        if self._refuse_station_edit():
            return
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

    def _save_state(self) -> None:
        self.state.options = self.options.clamp()
        # Stacji NIE dopisujemy do prywatnego stanu: we wspolnym profilu naleza
        # do AMC, a w piaskownicy zapisuje je RadioSource.
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
