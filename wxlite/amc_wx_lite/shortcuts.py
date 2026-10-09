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
  uzywany PO AKORDZIE PREFIKSU; te same KIERUNKI przenosimy na okno wx):
    57-60  Left/Right = przewijanie 10 s, Up/Down = glosnosc +-5
    63-66  Shift+Left/Right = 60 s, Shift+Up/Down = glosnosc +-1
    70-72  Ctrl+E czas miniony, Ctrl+R pozostaly, Ctrl+T calkowity

UWAGA o strzalkach: w pelnym AMC Up/Down zmieniaja glosnosc dopiero PO akordzie
prefiksu, bo zwykle strzalki musza chodzic po liscie. Tutaj tak samo - na liscie
strzalki naleza do natywnego ListCtrl, a glosnosc i przewijanie dzialaja w widoku
ODTWARZACZA, gdzie nie ma po czym chodzic. To swiadoma decyzja, nie rozjazd.

POPRAWKA PARYTETU TRANSPORTU (zgloszenie z realnego uruchomienia)
-----------------------------------------------------------------
Michal uruchomil ten wariant na prawdziwym profilu i transport NIE byl tym
transportem, ktory zna z AMC: nie dzialalo ``Ctrl+Shift+E/R/T`` (dzialalo
``Ctrl+E/R/T``), nie dzialalo ``Ctrl+Left/Right``, a predkosci nie dalo sie
znalezc pod klawiszami oryginalu.

Zrodlo bledu: powyzsze wiersze ``KeyboardProfile.cs:70-72`` opisuja profil
czytany PO AKORDZIE PREFIKSU -- to INNA WARSTWA niz klawisze okna. Wziecie ich
za skroty okna dalo gesty, ktorych oryginal w oknie nie ma, i zabralo gesty,
ktore ma. Warstwa okna to:

  MainWindow.xaml.cs  (dwie drogi tego samego okna: 215xx sciezka klawiszy
  i 22xxx tablica skrotow -- OBIE musza sie zgadzac)
    21583-21590 / 22387-22394  przewijanie, CZTERY pary:
        (None)        Left/Right -> SeekBackward10  / SeekForward10
        (Shift)       Left/Right -> SeekBackward30  / SeekForward30
        (Control)     Left/Right -> SeekBackward60  / SeekForward60
        (Control|Alt) Left/Right -> SeekBackwardCustom / SeekForwardCustom
      => ``Shift`` to 30 s, NIE 60 s. 60 s siedzi pod ``Ctrl``.
    21595-21597 / 22395-22397  predkosc odtwarzania:
        Shift+OemComma  -> PlaybackRateDown
        Shift+OemPeriod -> PlaybackRateUp
        Ctrl+OemPeriod  -> PlaybackRateReset
      => NIE ``Ctrl+Up/Down`` ani ``Ctrl+0``.
    21670 / 22188  Ctrl+Shift+G -> SettingsToggleSeekMessages
    21674-21676 / 22190-22192  Ctrl+Shift+E/R/T -> TimeElapsed/Remaining/Total
    21555-21557 / 22348-22350  cyfra BEZ modyfikatora -> SeekPercent(digit*10)
    21454-21456  Ctrl+cyfra -> SessionSlot, a Ctrl+0 -> SessionList
      => ``Ctrl+0`` NIE jest resetem tempa; to cudzy gest.
    21662  Ctrl+E -> ExportRadioFavorites
      => w oknie ``Ctrl+E`` nalezy do eksportu, wiec alias czasu byl bledem.
    20778-20785  F6 i Shift+F6: na LISCIE oba ida do ShowPlayerView, w
      ODTWARZACZU oba wracaja na liste (warunek to ``_playerViewActive``).

Zadne z powyzszych nie jest przechwytywaniem gestow czytnika ekranu: NVDA+Up,
NVDA+End i reszta gestow czytnika pozostaja nietkniete, a strzalki PIONOWE na
LISCIE nadal naleza do natywnej kontrolki.

LEWA STRZALKA NA LISCIE (zgloszenie z realnego uruchomienia)
------------------------------------------------------------
Michal zglosil, ze na listach stacji i plikow nie dziala lewa strzalka, ktora
w AMC czyta parametry zaznaczonego wiersza. Potwierdzone WYKONANIEM resolvera:
``resolve(Chord("Left"), player_view=False, radio_session=True)`` oddawalo
``None`` (to samo dla ``radio_session=False``), czyli klawisz ginal w kontrolce.
Brak byl rzeczywisty, nie domniemany.

Zrodlo wzorca: ``MainWindow.xaml.cs:23206-23216`` -- ``MediaList_PreviewKeyDown``
lapie ``Key.Left`` przy ``Keyboard.Modifiers == ModifierKeys.None`` i oddaje go
albo sciezce folderu (``$"{item.Title}: {folderPath}"``), albo
``AnnounceQuickMediaInformation(item)``. Dlatego ``Left`` jest w ``LIST_VIEW``,
a ``Shift/Ctrl/Alt+Left`` NIE sa -- modyfikator wyklucza te galaz w oryginale.
W ODTWARZACZU ``Left`` zostaje przewijaniem 10 s, bez zmian.
"""

from __future__ import annotations

from dataclasses import dataclass
from enum import Enum


class Action(Enum):
    SESSION_FILES = "session.files"
    SESSION_RADIO = "session.radio"
    SESSION_PODCASTS = "session.podcasts"
    ACTIVATE = "activate"
    PARENT_FOLDER = "parent"
    SHOW_PLAYER = "view.player"
    SHOW_LIST = "view.list"
    PLAY_PAUSE = "transport.playPause"
    # Zmiana utworu W KOLEJCE. Nastepstwo liczy zywa kolejka hosta, wiec te
    # akcje maja sens tylko wtedy, gdy kolejka naprawde prowadzi odtwarzanie.
    QUEUE_NEXT = "queue.next"
    QUEUE_PREVIOUS = "queue.previous"
    SEEK_BACK_10 = "seek.back10"
    SEEK_FORWARD_10 = "seek.forward10"
    # Shift to 30 s (MainWindow.xaml.cs:21585-21586). Dostarczona wersja
    # wiazala Shift z 60 s -- przez to dwa gesty robily to samo, a para
    # Ctrl+Left/Right nie robila nic.
    SEEK_BACK_30 = "seek.back30"
    SEEK_FORWARD_30 = "seek.forward30"
    SEEK_BACK_60 = "seek.back60"
    SEEK_FORWARD_60 = "seek.forward60"
    # Ctrl+Alt: krok z USTAWIEN uzytkownika (PlaybackSettings.CustomSeekSeconds,
    # domyslnie 300 s, zakres 5..1800 -- AppSettings.cs:280-295).
    SEEK_BACK_CUSTOM = "seek.backCustom"
    SEEK_FORWARD_CUSTOM = "seek.forwardCustom"
    # Skok procentowy: cyfra BEZ modyfikatora w odtwarzaczu
    # (MainWindow.xaml.cs:21555-21557 -> CommandIds.SeekPercent(digit * 10)).
    # Osobne wartosci, bo oryginal ma osobne identyfikatory komend -- jedna
    # akcja z parametrem nie dalaby sie sprawdzic w tablicy skrotow.
    SEEK_PERCENT_0 = "seek.percent.0"
    SEEK_PERCENT_10 = "seek.percent.10"
    SEEK_PERCENT_20 = "seek.percent.20"
    SEEK_PERCENT_30 = "seek.percent.30"
    SEEK_PERCENT_40 = "seek.percent.40"
    SEEK_PERCENT_50 = "seek.percent.50"
    SEEK_PERCENT_60 = "seek.percent.60"
    SEEK_PERCENT_70 = "seek.percent.70"
    SEEK_PERCENT_80 = "seek.percent.80"
    SEEK_PERCENT_90 = "seek.percent.90"
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
    ADD_BOOKMARK = "action.bookmark.add"
    CLIP_MARK_START = "audio.clip.markStart"
    CLIP_MARK_END = "audio.clip.markEnd"
    CLIP_JUMP_START = "audio.clip.jumpStart"
    CLIP_JUMP_END = "audio.clip.jumpEnd"
    CLIP_PREVIOUS_BOUNDARY = "audio.clip.previousBoundary"
    CLIP_NEXT_BOUNDARY = "audio.clip.nextBoundary"
    CLIP_EXPORT = "audio.clip.export"
    CLIP_APPEND = "audio.clip.appendToFile"
    CLIP_REMOVE = "editing.clip.removeFromOriginal"
    CLIP_CLEAR = "audio.clip.clear"
    #: Home/End w odtwarzaczu. ID WPROST z oryginalu (CommandIds.cs:40-41),
    #: zeby przyszly port komend sieciowych i palety nie wymyslal wlasnych.
    TRACK_START = "transport.trackStart"
    TRACK_END = "transport.trackEnd"
    # CommandIds.SettingsToggleSeekMessages, Ctrl+Shift+G
    # (MainWindow.xaml.cs:21670 i 22188).
    TOGGLE_SEEK_MESSAGES = "settings.toggleSeekMessages"
    GENERAL_SETTINGS = "settings.general"
    PRESET_1 = "preset.1"
    PRESET_2 = "preset.2"
    PRESET_3 = "preset.3"
    PRESET_4 = "preset.4"
    PRESET_5 = "preset.5"
    PRESET_6 = "preset.6"
    PRESET_7 = "preset.7"
    PRESET_8 = "preset.8"
    PRESET_9 = "preset.9"
    PRESET_10 = "preset.10"
    PRESET_11 = "preset.11"
    PRESET_12 = "preset.12"
    VIEW_PRESETS = "presets.view"
    ASSIGN_PRESET = "presets.assign"
    OPEN_FOLDER_DIALOG = "files.openFolder"
    OPEN_FILE_DIALOG = "files.openFile"
    COPY_NAME = "clipboard.copyName"
    COPY_ADDRESS = "clipboard.copyAddress"
    #: Ctrl+X: plik GOTOWY DO PRZENIESIENIA poza AMC. Osobna akcja od
    #: kopiowania, bo rozni sie formatem schowka (``Preferred DropEffect``
    #: MOVE), warunkiem (tylko istniejacy PLIK, nie folder) i komunikatem --
    #: ``CutLocalFilesForExternalMove`` (MainWindow.xaml.cs:25277).
    CUT_FILE = "clipboard.cutFile"
    RENAME_LIBRARY_ITEM = "library.renameItem"
    RENAME_LOCAL_FILE = "library.renameFile"
    REMOVE_SELECTED = "library.removeSelected"
    RECYCLE_SELECTED = "library.recycleSelected"
    STATION_ADD = "radio.add"
    STATION_EDIT = "radio.edit"
    STATION_DELETE = "radio.delete"
    STATION_IMPORT = "radio.import"
    RECORD_TOGGLE = "radio.recording.toggle"
    RECORD_PAUSE = "radio.recording.pause"
    RECORD_SPLIT = "radio.recording.split"
    RECORD_STOP_ALL = "radio.recording.stopAll"
    VIEW_ACTIVE_RECORDINGS = "radio.recording.activeView"
    VIEW_RECORDED_RADIO_FILES = "radio.recording.completedView"
    MANAGE_RADIO_SCHEDULES = "radio.recording.schedules"
    VIEW_PODCAST_INBOX = "podcasts.inbox"
    VIEW_PODCAST_IN_PROGRESS = "podcasts.inProgress"
    VIEW_PODCAST_DOWNLOADS = "podcasts.downloads"
    SORT_PODCAST_INBOX_ADDED = "podcasts.inbox.sort.added"
    SORT_PODCAST_INBOX_ALPHABETICAL = "podcasts.inbox.sort.alphabetical"
    SORT_PODCAST_INBOX_BY_PODCAST = "podcasts.inbox.sort.byPodcast"
    ADD_PODCAST_SOURCE = "podcasts.source.add"
    IMPORT_PODCAST_OPML = "podcasts.opml.import"
    EXPORT_PODCAST_OPML = "podcasts.opml.export"
    EXPORT_YOUTUBE_SUBSCRIPTIONS = "podcasts.youtube.export"
    REFRESH_PODCAST = "podcasts.refresh.current"
    REFRESH_PODCAST_LIBRARY = "podcasts.refresh.all"
    DOWNLOAD_PODCAST_EPISODES = "podcasts.download.selected"
    VIEW_ALL_FILES = "library.allFiles"
    VIEW_FAVORITES = "library.favorites"
    VIEW_PLAYLISTS = "library.playlists"
    # Nazwy odpowiadaja komendom C#, ktore te widoki otwieraja:
    #   CommandIds.ViewFolders  (MainWindow.xaml:446, Alt+1)
    #   CommandIds.ViewHistory  (CommandIds.cs:182, MainWindow.xaml:488, Ctrl+H)
    #   CommandIds.ViewQueue    (CommandIds.cs:149, MainWindow.xaml:487, Ctrl+Q)
    VIEW_FOLDERS = "library.folders"
    # ``CommandIds.ViewLibrary`` (CommandIds.cs:81, Ctrl+L z
    # MainWindow.xaml:427-428 i cs:22173). NIE jest tozsame z VIEW_FOLDERS:
    # CommandRouter.cs:390 woła ShowView("Biblioteka"), a cs:701-705 podmienia
    # te nazwe na ZAPAMIETANY widok profilu -- Foldery albo Wszystkie pliki.
    VIEW_LIBRARY = "view.library"
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
    # Port ``Filter_Click`` -> ``ShowFilter``/``FocusFilter``
    # (MainWindow.xaml:491 "_Filtruj listę" Ctrl+K; MainWindow.xaml.cs:25455
    # i :22210). NIE jest to ``CommandIds.SearchCurrent``/``SearchAll``
    # (Ctrl+F / Ctrl+Shift+F, MainWindow.xaml:492-493): tamte PYTAJA USLUGE o
    # nowe dane i oddaja je w osobnym oknie wynikow, a tu zwezamy liste, ktora
    # juz jest na ekranie. Port nie ma zdalnych uslug, wiec Ctrl+F zostaje
    # NIEOBSADZONY -- lepiej nie mieć gestu niż dać mu ciche, inne znaczenie.
    FOCUS_FILTER = "list.filter"
    # LEWA STRZALKA NA LISCIE: krotka informacja uzupelniajaca o ZAZNACZONYM
    # wierszu (bitrate, czestotliwosc, rozmiar, czas...). Gest ODCZYTANY, nie
    # wymyslony: ``MainWindow.xaml.cs:23206-23216`` w
    # ``MediaList_PreviewKeyDown`` lapie ``Key.Left`` bez modyfikatora i
    # oddaje go albo sciezce folderu, albo
    # ``AnnounceQuickMediaInformation(item)``.
    #
    # To NIE koliduje z natywna nawigacja listy: ``wx.ListCtrl`` w trybie
    # raportu uzywa strzalek PIONOWYCH do zmiany wiersza, a pozioma strzalka w
    # lewo nie ma w nim wlasnego znaczenia (tak samo jak w ``ListView`` WPF,
    # ktory oryginal przechwytuje). Gesty czytnika ekranu zostaja nietkniete --
    # NVDA czyta kolumny swoim modyfikatorem, nie sama strzalka.
    QUICK_INFORMATION = "list.quickInformation"
    #: Opcje sesji (Ctrl+Alt+Enter). Port ``SessionPlaybackOptionsEditor``.
    #:
    #: Jedno WSPOLNE wejscie dla obu sesji -- dialog sam pokazuje tylko te
    #: opcje, ktore dana sesja umie wykonac (``session_options``). Oryginal nie
    #: ma tego gestu w oknie (otwiera opcje z menu sesji), wiec klawisz jest
    #: WLASNY dla portu: Ctrl+Alt+Enter nie koliduje ani z aktywacja (gole
    #: Enter), ani z para Ctrl+Alt+Left/Right (przewijanie wlasnym krokiem).
    SESSION_OPTIONS = "session.options"
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
    "Ctrl+3": Action.SESSION_PODCASTS,
    "Return": Action.ACTIVATE,
    "Back": Action.PARENT_FOLDER,
    # Kontekstowe polecenia edycji z głównego AMC. F2 zmienia nazwę
    # widoczną w Bibliotece (albo nazwę/adres stacji, albo nazwę źródła
    # podcastów), Shift+F2 zmienia prawdziwą nazwę pliku. Delete nie dotyka
    # pliku; Shift+Delete po potwierdzeniu wysyła plik do systemowego Kosza.
    "F2": Action.RENAME_LIBRARY_ITEM,
    "Shift+F2": Action.RENAME_LOCAL_FILE,
    "Delete": Action.REMOVE_SELECTED,
    "Shift+Delete": Action.RECYCLE_SELECTED,
    # Escape NA LISCIE. ``MainWindow.xaml.cs:20797-20828`` kieruje go do
    # ``ReturnToMediaListFromEscape``, a ta (cs:22551-22563) przy PUSTYM
    # filtrze wola ``NavigateToParentLevel()`` -- czyli to samo wyjscie o
    # poziom wyzej, co Backspace. Port nie mial tu nic, wiec Escape na liscie
    # nie robil nic. Niepusty filtr obsluguje osobno ``_on_filter_key``
    # (czyszczenie + powrot), bo tam fokus jest w polu edycji.
    "Escape": Action.PARENT_FOLDER,
    "F6": Action.SHOW_PLAYER,
    # Shift+F6 na LISCIE tez idzie do odtwarzacza: warunek oryginalu to
    # ``_playerViewActive && Shift`` (MainWindow.xaml.cs:20780), a na liscie
    # pierwszy czlon jest falszem, wiec wykonuje sie galaz ShowPlayerView.
    "Shift+F6": Action.SHOW_PLAYER,
    "Space": Action.PLAY_PAUSE,
    # Czas: Ctrl+SHIFT+E/R/T (MainWindow.xaml.cs:21674-21676, 22190-22192).
    # Samo Ctrl+E/R/T nalezy do profilu PO PREFIKSIE, a w oknie Ctrl+E to
    # eksport ulubionych stacji (cs:21662) -- stad brak aliasow.
    "Ctrl+Shift+E": Action.TIME_ELAPSED,
    "Ctrl+Shift+R": Action.TIME_REMAINING,
    "Ctrl+Shift+T": Action.TIME_TOTAL,
    # Przelacznik automatycznych komunikatow odtwarzacza (cs:21670, 22188).
    "Ctrl+Shift+G": Action.TOGGLE_SEEK_MESSAGES,
    # MainWindow.xaml:42-45  OpenLocalFilesMenuItem   Ctrl+O        -> PLIKI
    # MainWindow.xaml:46-49  OpenLocalFolderMenuItem  Ctrl+Shift+O  -> FOLDER
    # Port mial te dwie pozycje odwrotnie, przez co Ctrl+O otwieralo dialog
    # folderu. Kolejnosc jest ustalona przez oryginal, nie przez wygode.
    "Ctrl+O": Action.OPEN_FILE_DIALOG,
    "Ctrl+Shift+O": Action.OPEN_FOLDER_DIALOG,
    # Adres NA ZADANIE, tak jak w pelnym AMC (MainWindow.xaml.cs:20717-20732).
    # Dzieki temu lista moze czytac samo nazwe, a pelny adres nadal jest
    # dostepny jednym skrotem -- nie zabieramy funkcji, przenosimy ja.
    "Ctrl+C": Action.COPY_NAME,
    "Ctrl+Shift+C": Action.COPY_ADDRESS,
    # Ctrl+X na liscie: wyciecie PLIKU do przeniesienia poza AMC
    # (MainWindow.xaml.cs:21022-21026). W oryginale ten sam klawisz obsluguje
    # tez WEWNETRZNE przestawianie elementow listy
    # (``TryStartInternalListMove``) -- tego portu jeszcze nie ma, wiec
    # mapujemy wylacznie galaz zewnetrzna, ktora Michal uzywa.
    "Ctrl+X": Action.CUT_FILE,
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
    # MainWindow.xaml:427-428 i MainWindow.xaml.cs:22173 -- powrot do
    # Biblioteki z KAZDEGO nazwanego widoku (Ulubione, Historia, ...).
    "Ctrl+L": Action.VIEW_LIBRARY,
    "Ctrl+Q": Action.VIEW_SAVED_QUEUE,
    "Ctrl+H": Action.VIEW_HISTORY,
    # Ctrl+B to ViewBookmarks oryginalu (MainWindow.xaml:489): WSZYSTKIE
    # zakladki. Teraz mamy ten zbior naprawde (GetForDisplay), wiec skrot
    # dostaje swoje wlasne znaczenie, a wezszy widok zakladek ZAZNACZONEGO
    # pliku zostaje pod Ctrl+Shift+B. Dwa zakresy, dwa gesty -- zaden nie
    # podmienia drugiemu zbioru pod reka.
    "Ctrl+B": Action.VIEW_ALL_BOOKMARKS,
    "Ctrl+Shift+B": Action.VIEW_ITEM_BOOKMARKS,
    # Filtr listy. Gest ODCZYTANY, nie zgadniety: MainWindow.xaml:491
    # ``<MenuItem Header="_Filtruj listę" InputGestureText="Ctrl+K" ...>``.
    # Ctrl+F i Ctrl+Shift+F NALEZA w oryginale do zdalnego SZUKANIA w usludze
    # (xaml:492-493) i dlatego ich tu NIE MA -- wziecie Ctrl+F na filtr
    # nauczyloby uzytkownika gestu, ktory w pelnym AMC robi co innego.
    "Ctrl+K": Action.FOCUS_FILTER,
    # LEWA STRZALKA bez modyfikatora. Jedyny wpis ze strzalka w tablicy listy:
    # oryginal wymaga ``Keyboard.Modifiers == ModifierKeys.None``
    # (cs:23206), wiec Shift/Ctrl/Alt+Left NIE sa tu wpisane i zostaja
    # kontrolce. Strzalki PIONOWE nadal naleza wylacznie do listy.
    "Left": Action.QUICK_INFORMATION,
    # Opcje sesji: TEN SAM gest co w odtwarzaczu, bo zakres (sesja) jest ten
    # sam niezaleznie od widoku.
    "Ctrl+Alt+Return": Action.SESSION_OPTIONS,
    # Zarzadzanie presetami z pelnego AMC (MainWindow.xaml:428-435).
    "Ctrl+Alt+P": Action.VIEW_PRESETS,
    "Ctrl+Alt+Shift+P": Action.ASSIGN_PRESET,
    "F1": Action.HELP,
}

PRESET_ACTIONS: dict[int, Action] = {
    slot: Action[f"PRESET_{slot}"] for slot in range(1, 13)
}
PRESET_SHORTCUTS: dict[str, int] = {
    **{f"Ctrl+Shift+{slot}": slot for slot in range(1, 10)},
    "Ctrl+Shift+0": 10,
    "Ctrl+Shift+-": 11,
    "Ctrl+Shift+=": 12,
    "Ctrl+Shift++": 12,
}


def preset_slot(action: Action) -> int | None:
    return next((slot for slot, candidate in PRESET_ACTIONS.items() if candidate is action), None)


for _chord, _slot in PRESET_SHORTCUTS.items():
    LIST_VIEW[_chord] = PRESET_ACTIONS[_slot]

# Dodatkowo w sesji radiowej: zarzadzanie wlasna lista stacji.
RADIO_LIST_VIEW: dict[str, Action] = {
    "Ctrl+N": Action.STATION_ADD,
    "F2": Action.STATION_EDIT,
    "Delete": Action.STATION_DELETE,
    # W pelnym AMC import jest kontekstowym Ctrl+O w sesji Radia. Ctrl+I
    # nalezy globalnie do widoku nowych odcinkow i materialow.
    "Ctrl+O": Action.STATION_IMPORT,
    # Skroty nagrywania z pelnego AMC (MainWindow.xaml.cs:21396-21446).
    # Ctrl+R dziala na liscie i w odtwarzaczu; Shift+Spacja steruje pauza.
    "Ctrl+R": Action.RECORD_TOGGLE,
    "Shift+Space": Action.RECORD_PAUSE,
}

# Litery bez modyfikatora sa bezpieczne tylko w odtwarzaczu Radia. Na liscie
# T i R musza pozostac natywnej kontroli (wyszukiwanie przyrostowe).
RADIO_PLAYER_VIEW: dict[str, Action] = {
    "Ctrl+O": Action.STATION_IMPORT,
    "R": Action.RECORD_TOGGLE,
    "Ctrl+R": Action.RECORD_TOGGLE,
    "Shift+Space": Action.RECORD_PAUSE,
    "T": Action.RECORD_SPLIT,
}

# Skroty W WIDOKU ODTWARZACZA. Tu strzalki sa wolne, wiec przejmuja role
# z profilu globalnego AMC (przewijanie i glosnosc).
PLAYER_VIEW: dict[str, Action] = {
    "Ctrl+1": Action.SESSION_FILES,
    "Ctrl+2": Action.SESSION_RADIO,
    "Ctrl+3": Action.SESSION_PODCASTS,
    "Escape": Action.SHOW_LIST,
    "Shift+F6": Action.SHOW_LIST,
    "F6": Action.SHOW_LIST,
    "Space": Action.PLAY_PAUSE,
    # Pelne AMC: gola litera B w otwartym odtwarzaczu dodaje szybka zakladke.
    # Ctrl+B pozostaje zbiorcza lista zakladek i nie jest aliasem tej akcji.
    "B": Action.ADD_BOOKMARK,
    "Ctrl+B": Action.VIEW_ALL_BOOKMARKS,
    # Zaznaczenie fragmentu i jego plikowe operacje. Gesty wprost z obu
    # sciezek klawiatury pelnego AMC (MainWindow.xaml.cs:21619-21627 oraz
    # 22430-22438). Ctrl+X jest tutaj inna akcja niz wyciecie pliku na liscie:
    # dziala tylko w odtwarzaczu i prowadzi przez potwierdzenie, wspolny edytor
    # C#, kopie bezpieczenstwa i weryfikacje zapisanego wyniku.
    "I": Action.CLIP_MARK_START,
    "O": Action.CLIP_MARK_END,
    "Shift+I": Action.CLIP_JUMP_START,
    "Shift+O": Action.CLIP_JUMP_END,
    "Alt+Prior": Action.CLIP_PREVIOUS_BOUNDARY,
    "Alt+Next": Action.CLIP_NEXT_BOUNDARY,
    "Ctrl+S": Action.CLIP_EXPORT,
    "Ctrl+D": Action.CLIP_APPEND,
    "Ctrl+X": Action.CLIP_REMOVE,
    "Shift+X": Action.CLIP_CLEAR,
    # Poprzedni/nastepny utwor kolejki. Gesty z oryginalu (MainWindow.xaml:714
    # i :717 -- menu odtwarzacza, te same akceleratory na przyciskach :995-1000).
    "Prior": Action.QUEUE_PREVIOUS,
    "Next": Action.QUEUE_NEXT,
    "Left": Action.SEEK_BACK_10,
    "Right": Action.SEEK_FORWARD_10,
    # Cztery pary krokow z oryginalu (MainWindow.xaml.cs:21583-21590,
    # 22387-22394). Shift = 30 s, Ctrl = 60 s, Ctrl+Alt = czas z ustawien.
    "Shift+Left": Action.SEEK_BACK_30,
    "Shift+Right": Action.SEEK_FORWARD_30,
    "Ctrl+Left": Action.SEEK_BACK_60,
    "Ctrl+Right": Action.SEEK_FORWARD_60,
    "Ctrl+Alt+Left": Action.SEEK_BACK_CUSTOM,
    "Ctrl+Alt+Right": Action.SEEK_FORWARD_CUSTOM,
    "Up": Action.VOLUME_UP_5,
    "Down": Action.VOLUME_DOWN_5,
    "Shift+Up": Action.VOLUME_UP_1,
    "Shift+Down": Action.VOLUME_DOWN_1,
    # Predkosc pod klawiszami ORYGINALU (cs:21595-21597, 22395-22397).
    # Ctrl+Up/Down i Ctrl+0 zostaly usuniete: pierwszych dwoch oryginal nie ma
    # wcale, a Ctrl+0 to u niego lista sesji (cs:21454-21456).
    "Shift+,": Action.RATE_DOWN,
    "Shift+.": Action.RATE_UP,
    "Ctrl+.": Action.RATE_RESET,
    # Skok procentowy: cyfra bez modyfikatora (cs:21555-21557, 22348-22350).
    # W odtwarzaczu cyfry sa wolne; na liscie naleza do kontrolki, wiec tam ich
    # nie ma.
    "0": Action.SEEK_PERCENT_0,
    "1": Action.SEEK_PERCENT_10,
    "2": Action.SEEK_PERCENT_20,
    "3": Action.SEEK_PERCENT_30,
    "4": Action.SEEK_PERCENT_40,
    "5": Action.SEEK_PERCENT_50,
    "6": Action.SEEK_PERCENT_60,
    "7": Action.SEEK_PERCENT_70,
    "8": Action.SEEK_PERCENT_80,
    "9": Action.SEEK_PERCENT_90,
    "Ctrl+Shift+E": Action.TIME_ELAPSED,
    "Ctrl+Shift+R": Action.TIME_REMAINING,
    "Ctrl+Shift+T": Action.TIME_TOTAL,
    "Ctrl+Shift+G": Action.TOGGLE_SEEK_MESSAGES,
    # Home/End TYLKO w odtwarzaczu (MainWindow.xaml.cs:21598-21599 i
    # 22398-22399, oba warunkiem ``ModifierKeys.None``). Na liscie te klawisze
    # naleza do kontrolki. Ctrl/Shift/Alt nie odpowiadaja modyfikatorowi NVDA:
    # brak kolizji Insert/CapsLock+End wymaga osobnego odbioru z czytnikiem.
    "Home": Action.TRACK_START,
    "End": Action.TRACK_END,
    # Opcje sesji dzialaja takze z odtwarzacza: zakres to SESJA, nie widok.
    "Ctrl+Alt+Return": Action.SESSION_OPTIONS,
    "Ctrl+Alt+P": Action.VIEW_PRESETS,
    "Ctrl+Alt+Shift+P": Action.ASSIGN_PRESET,
    "F1": Action.HELP,
    # Globalne zatrzymanie dziala niezaleznie od aktualnej sesji, jak w WPF.
    "Ctrl+Alt+Shift+R": Action.RECORD_STOP_ALL,
}

for _chord, _slot in PRESET_SHORTCUTS.items():
    PLAYER_VIEW[_chord] = PRESET_ACTIONS[_slot]

# To samo globalne polecenie z widoku listy.
LIST_VIEW["Ctrl+Alt+Shift+R"] = Action.RECORD_STOP_ALL
# Wspolne podglady pelnego AMC: dzialaja z listy i odtwarzacza niezaleznie od
# sesji, a Escape wraca do zapamietanego miejsca.
for _table in (LIST_VIEW, PLAYER_VIEW):
    _table["Alt+R"] = Action.VIEW_ACTIVE_RECORDINGS
    _table["Alt+Shift+R"] = Action.VIEW_RECORDED_RADIO_FILES
    _table["Ctrl+Shift+H"] = Action.MANAGE_RADIO_SCHEDULES
    _table["Ctrl+I"] = Action.VIEW_PODCAST_INBOX
    _table["Ctrl+Shift+I"] = Action.VIEW_PODCAST_IN_PROGRESS


PODCAST_INBOX_LIST_VIEW: dict[str, Action] = {
    "Alt+1": Action.SORT_PODCAST_INBOX_ADDED,
    "Alt+2": Action.SORT_PODCAST_INBOX_ALPHABETICAL,
    "Alt+3": Action.SORT_PODCAST_INBOX_BY_PODCAST,
    "F5": Action.REFRESH_PODCAST_LIBRARY,
}

# Kontekst sesji, niezaleznie od listy/odtwarzacza. F5 w skrzynce jest
# przesloniete powyzej, dokladnie jak w glownym AMC.
PODCAST_SESSION_VIEW: dict[str, Action] = {
    "Ctrl+N": Action.ADD_PODCAST_SOURCE,
    "Ctrl+O": Action.IMPORT_PODCAST_OPML,
    "F5": Action.REFRESH_PODCAST,
    "Ctrl+F5": Action.REFRESH_PODCAST_LIBRARY,
    # Jak w głównym AMC: na liście pobiera całe zaznaczenie, a w
    # odtwarzaczu bieżący odcinek. Ma pierwszeństwo przed plikowym dopisywaniem
    # fragmentu, które pod Ctrl+D pozostaje w sesji Plików lokalnych.
    "Ctrl+D": Action.DOWNLOAD_PODCAST_EPISODES,
}


def resolve(
    chord: Chord,
    *,
    player_view: bool,
    radio_session: bool,
    podcast_inbox: bool = False,
    podcast_session: bool = False,
) -> Action | None:
    """Znajdz akcje dla klawisza w DANYM widoku. Brak wpisu = klawisz zostaje
    dla kontrolki (natywna nawigacja ma pierwszenstwo)."""
    table = PLAYER_VIEW if player_view else LIST_VIEW
    canonical = chord.canonical
    # W „Nowych odcinkach” te same cyfry co w glownym AMC zmieniaja
    # kolejnosc. Poza tym jednym widokiem Alt+1/Alt+2 zachowuja lokalne
    # znaczenie Folderow/Wszystkich plikow, a Alt+3 pozostaje wolne.
    if not player_view and podcast_inbox and canonical in PODCAST_INBOX_LIST_VIEW:
        return PODCAST_INBOX_LIST_VIEW[canonical]
    # Parity z głównym AMC: Ctrl+F5 zawsze odświeża wszystkie źródła sesji,
    # F5 w skrzynce robi to samo, a F5 w pozostałych widokach odświeża źródło
    # bieżącego podcastu lub odcinka.
    if podcast_session and canonical in PODCAST_SESSION_VIEW:
        return PODCAST_SESSION_VIEW[canonical]
    # Widoki Radia sa dostepne takze z odtwarzacza, bez wychodzenia Escape.
    # Ta sama akcja co na liscie/menu; nie przenosimy edycji stacji do PLAYER.
    if radio_session and canonical in ("Ctrl+L", "Ctrl+U", "Ctrl+H"):
        return LIST_VIEW[canonical]
    if player_view and radio_session and canonical in RADIO_PLAYER_VIEW:
        return RADIO_PLAYER_VIEW[canonical]
    if not player_view and radio_session and canonical in RADIO_LIST_VIEW:
        return RADIO_LIST_VIEW[canonical]
    return table.get(canonical)


def describe() -> list[tuple[str, str]]:
    """Tekst pomocy (F1). Po polsku, krotko, bez zaleznosci od wx."""
    labels = {
        Action.SESSION_FILES: "Pliki lokalne",
        Action.SESSION_RADIO: "Radio internetowe",
        Action.SESSION_PODCASTS: "Podcasty i YouTube",
        Action.ACTIVATE: "Otworz folder albo odtworz",
        Action.PARENT_FOLDER: "Folder nadrzedny",
        Action.SHOW_PLAYER: "Widok odtwarzacza",
        Action.SHOW_LIST: "Powrot na liste",
        Action.PLAY_PAUSE: "Pauza albo wznowienie",
        Action.QUEUE_NEXT: "Nastepny utwor kolejki",
        Action.QUEUE_PREVIOUS: "Poprzedni utwor kolejki",
        Action.SEEK_BACK_10: "Przewin 10 sekund wstecz",
        Action.SEEK_FORWARD_10: "Przewin 10 sekund w przod",
        Action.SEEK_BACK_30: "Przewin 30 sekund wstecz",
        Action.SEEK_FORWARD_30: "Przewin 30 sekund w przod",
        Action.SEEK_BACK_60: "Przewin minute wstecz",
        Action.SEEK_FORWARD_60: "Przewin minute w przod",
        # Krok tej pary ustawia uzytkownik w AMC (domyslnie 5 minut), wiec
        # pomoc nie moze podawac stalej liczby sekund.
        Action.SEEK_BACK_CUSTOM: "Przewin wstecz o czas z ustawien AMC",
        Action.SEEK_FORWARD_CUSTOM: "Przewin w przod o czas z ustawien AMC",
        Action.VOLUME_UP_5: "Glosniej o 5",
        Action.VOLUME_DOWN_5: "Ciszej o 5",
        Action.VOLUME_UP_1: "Glosniej o 1",
        Action.VOLUME_DOWN_1: "Ciszej o 1",
        Action.RATE_UP: "Szybciej (prędkość odtwarzania)",
        Action.RATE_DOWN: "Wolniej (prędkość odtwarzania)",
        Action.RATE_RESET: "Prędkość normalna",
        Action.TIME_ELAPSED: "Czas miniony",
        Action.TIME_REMAINING: "Czas pozostaly",
        Action.TIME_TOTAL: "Czas calkowity",
        Action.ADD_BOOKMARK: "Dodaj zakladke w biezacym miejscu",
        Action.CLIP_MARK_START: "Ustaw poczatek fragmentu",
        Action.CLIP_MARK_END: "Ustaw koniec fragmentu",
        Action.CLIP_JUMP_START: "Skocz do poczatku fragmentu",
        Action.CLIP_JUMP_END: "Skocz do konca fragmentu",
        Action.CLIP_PREVIOUS_BOUNDARY: "Poprzednia granica fragmentu",
        Action.CLIP_NEXT_BOUNDARY: "Nastepna granica fragmentu",
        Action.CLIP_EXPORT: "Zapisz zaznaczony fragment jako nowy plik",
        Action.CLIP_APPEND: "Dopisz zaznaczony fragment na koncu pliku",
        Action.CLIP_CLEAR: "Wyczysc zaznaczenie fragmentu",
        Action.TOGGLE_SEEK_MESSAGES: "Automatyczne komunikaty odtwarzacza",
        Action.ADD_PODCAST_SOURCE: "Dodaj podcast, kanał YouTube lub medium internetowe",
        Action.IMPORT_PODCAST_OPML: "Importuj podcasty z OPML",
        Action.EXPORT_PODCAST_OPML: "Eksportuj bibliotekę podcastów do OPML",
        Action.EXPORT_YOUTUBE_SUBSCRIPTIONS: "Eksportuj kanały i playlisty YouTube",
        Action.REFRESH_PODCAST: "Odśwież wybrany podcast, kanał lub playlistę",
        Action.REFRESH_PODCAST_LIBRARY: "Odśwież wszystkie źródła podcastów",
        Action.DOWNLOAD_PODCAST_EPISODES: "Pobierz zaznaczone odcinki podcastów",
        # Skok procentowy. Dziesiec wierszy, bo oryginal ma dziesiec komend i
        # uzytkownik szuka w pomocy konkretnej cyfry, nie opisu rodziny.
        Action.SEEK_PERCENT_0: "Skok na poczatek utworu (0%)",
        Action.SEEK_PERCENT_10: "Skok do 10% utworu",
        Action.SEEK_PERCENT_20: "Skok do 20% utworu",
        Action.SEEK_PERCENT_30: "Skok do 30% utworu",
        Action.SEEK_PERCENT_40: "Skok do 40% utworu",
        Action.SEEK_PERCENT_50: "Skok do polowy utworu (50%)",
        Action.SEEK_PERCENT_60: "Skok do 60% utworu",
        Action.SEEK_PERCENT_70: "Skok do 70% utworu",
        Action.SEEK_PERCENT_80: "Skok do 80% utworu",
        Action.SEEK_PERCENT_90: "Skok do 90% utworu",
        Action.OPEN_FOLDER_DIALOG: "Wybierz folder",
        Action.OPEN_FILE_DIALOG: "Wybierz plik",
        Action.TRACK_START: "Poczatek utworu",
        # Pomoc mowi, ILE oryginal zostawia (CommandRouter.cs:321), zeby
        # uzytkownik nie uznal braku ciszy na koncu za blad.
        Action.TRACK_END: "Koniec utworu, dziesiec sekund przed koncem",
        Action.COPY_NAME: "Skopiuj nazwe",
        Action.COPY_ADDRESS: "Skopiuj adres",
        Action.CUT_FILE: "Wytnij plik",
        Action.RENAME_LIBRARY_ITEM: "Zmień nazwę w Bibliotece lub nazwę źródła",
        Action.RENAME_LOCAL_FILE: "Zmień nazwę pliku na dysku",
        Action.REMOVE_SELECTED: "Usuń z bieżącego widoku bez kasowania pliku",
        Action.RECYCLE_SELECTED: "Przenieś zaznaczone pliki do Kosza",
        Action.STATION_ADD: "Dodaj stacje",
        Action.STATION_EDIT: "Zmien stacje",
        Action.STATION_DELETE: "Usun stacje",
        Action.STATION_IMPORT: "Importuj liste stacji",
        Action.RECORD_TOGGLE: "Rozpocznij albo zatrzymaj nagrywanie wybranej stacji",
        Action.RECORD_PAUSE: "Wstrzymaj albo wznow nagrywanie wybranej stacji",
        Action.RECORD_SPLIT: "Zapisz biezaca czesc i rozpocznij nowa",
        Action.RECORD_STOP_ALL: "Zatrzymaj wszystkie nagrania radia",
        Action.VIEW_ACTIVE_RECORDINGS: "Pokaz trwajace nagrania radia",
        Action.VIEW_RECORDED_RADIO_FILES: "Pokaz historie nagrywania radia",
        Action.MANAGE_RADIO_SCHEDULES: "Pokaz harmonogram nagrywania radia",
        Action.VIEW_PODCAST_INBOX: "Nowe odcinki i materiały",
        Action.VIEW_PODCAST_IN_PROGRESS: "W trakcie słuchania",
        Action.VIEW_PODCAST_DOWNLOADS: "Pobrane odcinki podcastów",
        Action.SORT_PODCAST_INBOX_ADDED: (
            "Nowe odcinki: według dodania, najnowsze na początku"
        ),
        Action.SORT_PODCAST_INBOX_ALPHABETICAL: "Nowe odcinki: alfabetycznie",
        Action.SORT_PODCAST_INBOX_BY_PODCAST: "Nowe odcinki: według podcastu",
        Action.VIEW_PRESETS: "Pokaż presety aktywnej sesji",
        Action.ASSIGN_PRESET: "Utwórz preset lub przypisz bieżący element",
        Action.VIEW_ALL_FILES: "Wszystkie pliki alfabetycznie",
        Action.VIEW_FAVORITES: "Ulubione",
        Action.VIEW_PLAYLISTS: "Playlisty",
        Action.VIEW_FOLDERS: "Foldery Biblioteki",
        Action.VIEW_LIBRARY: "Biblioteka: powrot do zapamietanego widoku",
        Action.VIEW_HISTORY: "Historia odtwarzania",
        # Wiersze pochodza z ZAPISANEGO profilu (host nie jest ich autorem), ale
        # Enter uruchamia z nich ZYWA kolejke hosta -- stad "Enter odtwarza".
        Action.VIEW_SAVED_QUEUE: "Kolejka: kolejka silnika, Enter odtwarza",
        # Jawnie wezszy zakres niz "Zakladki" w pelnym AMC.
        Action.VIEW_ITEM_BOOKMARKS: "Zakladki zaznaczonego pliku",
        # Pelny zbior. Nazwa mowi, ze to wszystkie sesje, bo widok pokazuje
        # tez wpisy, ktorych ten program nie odtworzy.
        Action.VIEW_ALL_BOOKMARKS: "Wszystkie zakladki",
        # Pomoc MUSI powiedziec cala droge wyjscia, bo pole filtra zabiera
        # klawisze liter i uzytkownik czytnika nie widzi, ze jest w edycji.
        Action.FOCUS_FILTER: (
            "Filtruj liste: Enter albo strzalka w dol przechodzi do wynikow, "
            "Escape czysci filtr"
        ),
        # Nazwa mowi, CO klawisz daje, a nie jak dziala. "Uzupelniajace", bo
        # sama nazwa wiersza jest juz przeczytana przy nawigacji -- oryginal
        # celowo jej nie powtarza (QuickMediaInformationFormatter:8-10).
        Action.QUICK_INFORMATION: (
            "Informacje uzupelniajace o zaznaczonym wierszu "
            "(folder: sciezka)"
        ),
        Action.HELP: "Ta pomoc",
        # Pomoc mowi WPROST, ze zakres to sesja, a nie zaznaczony plik -- bez
        # tego uzytkownik nie wie, czego dotkna zmiany.
        Action.SESSION_OPTIONS: (
            "Opcje odtwarzania tej sesji: dialog pokazuje tylko opcje, "
            "ktore sesja umie wykonac"
        ),
    }
    labels.update({
        action: f"Uruchom preset {slot} bieżącej sesji"
        for slot, action in PRESET_ACTIONS.items()
    })
    # Nazwy klawiszy w pomocy musza byc TAKIE, jak na klawiaturze. Wewnetrzne
    # "Prior"/"Next" (z wx) czytnik przeczytalby jako obce slowa, a uzytkownik
    # nie znalazlby tych klawiszy pod palcami.
    readable_keys = {"Prior": "Page Up", "Next": "Page Down"}

    seen: set[Action] = set()
    out: list[tuple[str, str]] = []
    for table in (
        PODCAST_INBOX_LIST_VIEW,
        PODCAST_SESSION_VIEW,
        LIST_VIEW,
        RADIO_LIST_VIEW,
        RADIO_PLAYER_VIEW,
        PLAYER_VIEW,
    ):
        for chord, action in table.items():
            if action in seen:
                continue
            seen.add(action)
            label = chord
            for internal, spoken in readable_keys.items():
                label = label.replace(internal, spoken)
            # Ctrl+O jest celowo kontekstowe. Dwa identyczne naglowki w
            # pomocy wygladalyby jak sprzecznosc i ``dict(describe())``
            # zgubilby pierwszy. Dopowiadamy sesje tylko przy imporcie Radia;
            # zwykle Ctrl+O nadal brzmi po prostu jak wybor pliku.
            if action is Action.STATION_IMPORT:
                label = f"{label} (Radio internetowe)"
            elif action is Action.IMPORT_PODCAST_OPML:
                label = f"{label} (Podcasty i YouTube)"
            out.append((label, labels.get(action, action.value)))
    return out
