"""Skroty klawiszowe AMC-wx-Lite.

KAZDY skrot ponizej zostal ODCZYTANY ZE ZRODEL pelnego AMC, nie wymyslony.
Zrodla (gałąź hermes/wx-lite-after416, baza 4.1.6):

  src/AccessibleMediaController.Windows/MainWindow.xaml.cs
    21449  modifiers == ModifierKeys.Control && TryGetDigitKey(...) -> SessionSlot
           => Ctrl+cyfra wybiera sesje.
    20773  e.Key == Key.F6 && Modifiers is None or Shift
    20775      _playerViewActive && Shift -> ReturnFromPlayerToList()
    20778      else ShowPlayerView()
           => F6 idzie do odtwarzacza, Shift+F6 wraca na liste.
    20792  Modifiers == None && e.Key == Key.Escape -> ReturnFromPlayerToList()
           => Escape z odtwarzacza wraca na liste.
    20832  Modifiers == None && e.Key == Key.Back (poza polem tekstowym)
           => Backspace wychodzi do folderu nadrzednego.
    21687  (ModifierKeys.None, Key.Space) => CommandIds.PlayPause
           => Spacja to pauza/wznowienie.

  src/AccessibleMediaController.Core/Input/KeyboardProfile.cs  (profil globalny,
  uzywany po akordzie prefiksu; te same KIERUNKI przenosimy na okno wx):
    57-60  Left/Right = przewijanie 10 s, Up/Down = glosnosc +-5
    63-66  Shift+Left/Right = 60 s, Shift+Up/Down = glosnosc +-1
    70-72  Ctrl+E czas miniony, Ctrl+R pozostaly, Ctrl+T calkowity

UWAGA o strzalkach: w pelnym AMC Up/Down zmieniaja glosnosc dopiero PO akordzie
prefiksu, bo zwykle strzalki musza chodzic po liscie. Tutaj tak samo - na liscie
strzalki naleza do natywnego ListCtrl, a glosnosc i przewijanie dzialaja w widoku
ODTWARZACZA, gdzie nie ma po czym chodzic. To swiadoma decyzja, nie rozjazd.
"""

from __future__ import annotations

from dataclasses import dataclass
from enum import Enum


class Action(Enum):
    SESSION_FILES = "session.files"
    SESSION_RADIO = "session.radio"
    ACTIVATE = "activate"
    PARENT_FOLDER = "parent"
    SHOW_PLAYER = "view.player"
    SHOW_LIST = "view.list"
    PLAY_PAUSE = "transport.playPause"
    SEEK_BACK_10 = "seek.back10"
    SEEK_FORWARD_10 = "seek.forward10"
    SEEK_BACK_60 = "seek.back60"
    SEEK_FORWARD_60 = "seek.forward60"
    VOLUME_UP_5 = "volume.up5"
    VOLUME_DOWN_5 = "volume.down5"
    VOLUME_UP_1 = "volume.up1"
    VOLUME_DOWN_1 = "volume.down1"
    RATE_UP = "rate.up"
    RATE_DOWN = "rate.down"
    RATE_RESET = "rate.reset"
    TIME_ELAPSED = "time.elapsed"
    TIME_REMAINING = "time.remaining"
    TIME_TOTAL = "time.total"
    OPEN_FOLDER_DIALOG = "files.openFolder"
    OPEN_FILE_DIALOG = "files.openFile"
    COPY_NAME = "clipboard.copyName"
    COPY_ADDRESS = "clipboard.copyAddress"
    STATION_ADD = "radio.add"
    STATION_EDIT = "radio.edit"
    STATION_DELETE = "radio.delete"
    STATION_IMPORT = "radio.import"
    VIEW_ALL_FILES = "library.allFiles"
    VIEW_FAVORITES = "library.favorites"
    VIEW_PLAYLISTS = "library.playlists"
    # Nazwy odpowiadaja komendom C#, ktore te widoki otwieraja:
    #   CommandIds.ViewFolders  (MainWindow.xaml:446, Alt+1)
    #   CommandIds.ViewHistory  (CommandIds.cs:182, MainWindow.xaml:488, Ctrl+H)
    #   CommandIds.ViewQueue    (CommandIds.cs:149, MainWindow.xaml:487, Ctrl+Q)
    VIEW_FOLDERS = "library.folders"
    VIEW_HISTORY = "view.history"
    VIEW_SAVED_QUEUE = "view.queue"
    # UWAGA: to NIE jest CommandIds.ViewBookmarks. Tamta komenda (Ctrl+B)
    # pokazuje WSZYSTKIE zakladki (BookmarkIndex.GetForDisplay,
    # BookmarkIndex.cs:19-28). Tutaj mamy port GetForItem (cs:30-36), czyli
    # zakladki JEDNEGO zaznaczonego elementu -- zakres wezszy, wiec wlasny
    # identyfikator i wlasny gest, zeby nie obiecywac cudzego zbioru.
    VIEW_ITEM_BOOKMARKS = "library.itemBookmarks"
    # To JUZ jest port CommandIds.ViewBookmarks (BookmarkIndex.GetForDisplay,
    # BookmarkIndex.cs:19-28): WSZYSTKIE zakladki profilu, wszystkich sesji.
    # Osobna akcja od VIEW_ITEM_BOOKMARKS, bo zakres jest inny -- jedna akcja
    # z przelacznikiem kazalaby zgadywac, ktory zbior widzi uzytkownik.
    VIEW_ALL_BOOKMARKS = "library.allBookmarks"
    HELP = "help"


@dataclass(frozen=True, slots=True)
class Chord:
    """Klawisz + modyfikatory. Nazwy klawiszy wlasne, zeby nie wiazac sie z wx."""

    key: str
    ctrl: bool = False
    shift: bool = False
    alt: bool = False

    @property
    def canonical(self) -> str:
        parts = []
        if self.ctrl:
            parts.append("Ctrl")
        if self.alt:
            parts.append("Alt")
        if self.shift:
            parts.append("Shift")
        parts.append(self.key)
        return "+".join(parts)


# Skroty dzialajace W WIDOKU LISTY. Strzalki NIE sa tu wpisane - naleza do
# natywnego ListCtrl, zeby czytnik ekranu i Narrator dzialaly bez dodatku.
LIST_VIEW: dict[str, Action] = {
    "Ctrl+1": Action.SESSION_FILES,
    "Ctrl+2": Action.SESSION_RADIO,
    "Return": Action.ACTIVATE,
    "Back": Action.PARENT_FOLDER,
    "F6": Action.SHOW_PLAYER,
    "Space": Action.PLAY_PAUSE,
    "Ctrl+E": Action.TIME_ELAPSED,
    "Ctrl+R": Action.TIME_REMAINING,
    "Ctrl+T": Action.TIME_TOTAL,
    "Ctrl+O": Action.OPEN_FOLDER_DIALOG,
    "Ctrl+Shift+O": Action.OPEN_FILE_DIALOG,
    # Adres NA ZADANIE, tak jak w pelnym AMC (MainWindow.xaml.cs:20717-20732).
    # Dzieki temu lista moze czytac samo nazwe, a pelny adres nadal jest
    # dostepny jednym skrotem -- nie zabieramy funkcji, przenosimy ja.
    "Ctrl+C": Action.COPY_NAME,
    "Ctrl+Shift+C": Action.COPY_ADDRESS,
    # Nazwane widoki Biblioteki. Skroty WPROST ze wzorca, nie wymyslone:
    #   MainWindow.xaml:458  "_Wszystkie pliki alfabetycznie"  Alt+2
    #   MainWindow.xaml:426  "_Ulubione"                       Ctrl+U
    #   MainWindow.xaml:427  "_Playlisty"                      Ctrl+P
    # Zadny z nich nie koliduje z Ctrl+1/Ctrl+2 (sesje) ani z Ctrl+O.
    "Alt+2": Action.VIEW_ALL_FILES,
    "Ctrl+U": Action.VIEW_FAVORITES,
    "Ctrl+P": Action.VIEW_PLAYLISTS,
    # Widoki aktywnosci. Skroty ODCZYTANE z kodu, nie zgadniete:
    #   MainWindow.xaml:446  "Foldery _Biblioteki"     Alt+1
    #   MainWindow.xaml:487  "_Kolejka"                Ctrl+Q
    #   MainWindow.xaml:488  "_Historia odtwarzania"   Ctrl+H
    # (te same pary w KeyboardProfile.cs:84 i :90 oraz w sciezce klawiszy
    # MainWindow.xaml.cs:21674-21675.)
    "Alt+1": Action.VIEW_FOLDERS,
    "Ctrl+Q": Action.VIEW_SAVED_QUEUE,
    "Ctrl+H": Action.VIEW_HISTORY,
    # Ctrl+B to ViewBookmarks oryginalu (MainWindow.xaml:489): WSZYSTKIE
    # zakladki. Teraz mamy ten zbior naprawde (GetForDisplay), wiec skrot
    # dostaje swoje wlasne znaczenie, a wezszy widok zakladek ZAZNACZONEGO
    # pliku zostaje pod Ctrl+Shift+B. Dwa zakresy, dwa gesty -- zaden nie
    # podmienia drugiemu zbioru pod reka.
    "Ctrl+B": Action.VIEW_ALL_BOOKMARKS,
    "Ctrl+Shift+B": Action.VIEW_ITEM_BOOKMARKS,
    "F1": Action.HELP,
}

# Dodatkowo w sesji radiowej: zarzadzanie wlasna lista stacji.
RADIO_LIST_VIEW: dict[str, Action] = {
    "Ctrl+N": Action.STATION_ADD,
    "F2": Action.STATION_EDIT,
    "Delete": Action.STATION_DELETE,
    "Ctrl+I": Action.STATION_IMPORT,
}

# Skroty W WIDOKU ODTWARZACZA. Tu strzalki sa wolne, wiec przejmuja role
# z profilu globalnego AMC (przewijanie i glosnosc).
PLAYER_VIEW: dict[str, Action] = {
    "Ctrl+1": Action.SESSION_FILES,
    "Ctrl+2": Action.SESSION_RADIO,
    "Escape": Action.SHOW_LIST,
    "Shift+F6": Action.SHOW_LIST,
    "F6": Action.SHOW_LIST,
    "Space": Action.PLAY_PAUSE,
    "Left": Action.SEEK_BACK_10,
    "Right": Action.SEEK_FORWARD_10,
    "Shift+Left": Action.SEEK_BACK_60,
    "Shift+Right": Action.SEEK_FORWARD_60,
    "Up": Action.VOLUME_UP_5,
    "Down": Action.VOLUME_DOWN_5,
    "Shift+Up": Action.VOLUME_UP_1,
    "Shift+Down": Action.VOLUME_DOWN_1,
    "Ctrl+Up": Action.RATE_UP,
    "Ctrl+Down": Action.RATE_DOWN,
    "Ctrl+0": Action.RATE_RESET,
    "Ctrl+E": Action.TIME_ELAPSED,
    "Ctrl+R": Action.TIME_REMAINING,
    "Ctrl+T": Action.TIME_TOTAL,
    "F1": Action.HELP,
}


def resolve(chord: Chord, *, player_view: bool, radio_session: bool) -> Action | None:
    """Znajdz akcje dla klawisza w DANYM widoku. Brak wpisu = klawisz zostaje
    dla kontrolki (natywna nawigacja ma pierwszenstwo)."""
    table = PLAYER_VIEW if player_view else LIST_VIEW
    canonical = chord.canonical
    if not player_view and radio_session and canonical in RADIO_LIST_VIEW:
        return RADIO_LIST_VIEW[canonical]
    return table.get(canonical)


def describe() -> list[tuple[str, str]]:
    """Tekst pomocy (F1). Po polsku, krotko, bez zaleznosci od wx."""
    labels = {
        Action.SESSION_FILES: "Pliki lokalne",
        Action.SESSION_RADIO: "Radio internetowe",
        Action.ACTIVATE: "Otworz folder albo odtworz",
        Action.PARENT_FOLDER: "Folder nadrzedny",
        Action.SHOW_PLAYER: "Widok odtwarzacza",
        Action.SHOW_LIST: "Powrot na liste",
        Action.PLAY_PAUSE: "Pauza albo wznowienie",
        Action.SEEK_BACK_10: "Przewin 10 sekund wstecz",
        Action.SEEK_FORWARD_10: "Przewin 10 sekund w przod",
        Action.SEEK_BACK_60: "Przewin minute wstecz",
        Action.SEEK_FORWARD_60: "Przewin minute w przod",
        Action.VOLUME_UP_5: "Glosniej o 5",
        Action.VOLUME_DOWN_5: "Ciszej o 5",
        Action.VOLUME_UP_1: "Glosniej o 1",
        Action.VOLUME_DOWN_1: "Ciszej o 1",
        Action.RATE_UP: "Szybciej",
        Action.RATE_DOWN: "Wolniej",
        Action.RATE_RESET: "Normalne tempo",
        Action.TIME_ELAPSED: "Czas miniony",
        Action.TIME_REMAINING: "Czas pozostaly",
        Action.TIME_TOTAL: "Czas calkowity",
        Action.OPEN_FOLDER_DIALOG: "Wybierz folder",
        Action.OPEN_FILE_DIALOG: "Wybierz plik",
        Action.COPY_NAME: "Skopiuj nazwe",
        Action.COPY_ADDRESS: "Skopiuj adres",
        Action.STATION_ADD: "Dodaj stacje",
        Action.STATION_EDIT: "Zmien stacje",
        Action.STATION_DELETE: "Usun stacje",
        Action.STATION_IMPORT: "Importuj liste stacji",
        Action.VIEW_ALL_FILES: "Wszystkie pliki alfabetycznie",
        Action.VIEW_FAVORITES: "Ulubione",
        Action.VIEW_PLAYLISTS: "Playlisty",
        Action.VIEW_FOLDERS: "Foldery Biblioteki",
        Action.VIEW_HISTORY: "Historia odtwarzania",
        # Nazwa mowi, ze to ZAPISANY stan profilu, a nie kolejka grajacego
        # silnika -- tej w tej aplikacji nie ma i nie udajemy jej.
        Action.VIEW_SAVED_QUEUE: "Zapisana kolejka z profilu",
        # Jawnie wezszy zakres niz "Zakladki" w pelnym AMC.
        Action.VIEW_ITEM_BOOKMARKS: "Zakladki zaznaczonego pliku",
        # Pelny zbior. Nazwa mowi, ze to wszystkie sesje, bo widok pokazuje
        # tez wpisy, ktorych ten program nie odtworzy.
        Action.VIEW_ALL_BOOKMARKS: "Wszystkie zakladki",
        Action.HELP: "Ta pomoc",
    }
    seen: set[Action] = set()
    out: list[tuple[str, str]] = []
    for table in (LIST_VIEW, RADIO_LIST_VIEW, PLAYER_VIEW):
        for chord, action in table.items():
            if action in seen:
                continue
            seen.add(action)
            out.append((chord, labels.get(action, action.value)))
    return out
