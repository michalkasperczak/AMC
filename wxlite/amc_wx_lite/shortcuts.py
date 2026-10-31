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
      => NIE ``Ctrl+Up/Down``. ``Ctrl+0`` nie jest już używane; listę sesji
         otwiera wyłącznie ``Ctrl+Shift+S``.
    21670 / 22188  Ctrl+Shift+G -> SettingsToggleSeekMessages
    21674-21676 / 22190-22192  Ctrl+Shift+E/R/T -> TimeElapsed/Remaining/Total
    21555-21557 / 22348-22350  cyfra BEZ modyfikatora -> SeekPercent(digit*10)
    21454-21456  Ctrl+cyfra -> SessionSlot, a dawniej Ctrl+0 -> SessionList
      => w interfejsie Python pozostają Ctrl+1–9, a listę sesji otwiera
         jednoznaczne ``Ctrl+Shift+S``.
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
from typing import Mapping

from . import i18n
from .navigation import SessionId


class Action(Enum):
    SESSION_FILES = "session.files"
    SESSION_RADIO = "session.radio"
    SESSION_PODCASTS = "session.podcasts"
    SESSION_TIDAL = "session.tidal"
    SESSION_WIIM = "session.wiim"
    SESSION_SONOS = "session.sonos"
    SESSION_SPOTIFY = "session.spotify"
    SESSION_FRONTIER_SMART = "session.frontierSmart"
    SESSION_RADIOARCHIWISTA = "session.radioarchiwista"
    SESSION_LIST = "session.list"
    SESSION_ENABLE = "session.enable"
    SESSION_DISABLE_CURRENT = "session.disableCurrent"
    SESSION_CONFIGURE_CURRENT = "session.configureCurrent"
    MANAGE_LIBRARY_FOLDERS = "library.folderSources.manage"
    REFRESH_LOCAL_LIBRARY = "library.folderSources.refresh"
    REFRESH_RADIO_STATIONS = "radio.stations.refresh"
    PREVIOUS_SESSION = "session.previous"
    NEXT_SESSION = "session.next"
    COMMAND_PALETTE = "view.commandPalette"
    SELECT_SONOS_TARGET = "sonos.target.select"
    FRONTIER_SMART_SELECT_MODE = "frontierSmart.mode.select"
    ACTIVATE = "activate"
    PARENT_FOLDER = "parent"
    # Escape na liscie nie jest aliasem Backspace. Zamyka podglad lub
    # zagniezdzony widok, ale w drzewie folderow Biblioteki nie przechodzi do
    # katalogu nadrzednego. Ta roznica jest istotna przy obsludze bez wzroku:
    # przypadkowy Escape nie moze przeniesc fokusu do innego poziomu listy.
    CLOSE_LIST_PREVIEW = "view.closePreview"
    SHOW_PLAYER = "view.player"
    SHOW_LIST = "view.list"
    PLAY_PAUSE = "transport.playPause"
    TOGGLE_SHUFFLE = "transport.shuffle.toggle"
    TOGGLE_MUTE = "transport.toggleMute"
    # Zmiana utworu W KOLEJCE. Nastepstwo liczy zywa kolejka hosta, wiec te
    # akcje maja sens tylko wtedy, gdy kolejka naprawde prowadzi odtwarzanie.
    QUEUE_NEXT = "queue.next"
    QUEUE_PREVIOUS = "queue.previous"
    ADD_TO_QUEUE = "action.queue.add"
    TOGGLE_PLAY_NEXT = "action.queue.playNextToggle"
    TOGGLE_FAVORITE = "action.favorite.toggle"
    TOGGLE_RADIO_LIBRARY = "radio.library.toggle"
    MANAGE_PLAYLIST_MEMBERSHIP = "action.playlists.membership"
    TIDAL_TOGGLE_LIBRARY = "tidal.library.toggle"
    TIDAL_TOGGLE_FAVORITE = "tidal.favorite.toggle"
    SPOTIFY_TOGGLE_LIBRARY = "spotify.library.toggle"
    SPOTIFY_TOGGLE_FAVORITE = "spotify.favorite.toggle"
    # Prawa strzalka na liscie TIDAL. To polecenie jest kontekstowe: zależnie
    # od wiersza pokazuje przejście do albumu, wykonawcy albo oba kierunki.
    # Osobna akcja jest potrzebna przede wszystkim Pomocy klawiatury -- bez
    # niej działający gest był opisywany jako brak polecenia.
    TIDAL_RELATIONS = "tidal.relations"
    SPOTIFY_RELATIONS = "spotify.relations"
    SPOTIFY_VIEW_LIKED = "spotify.view.liked"
    SPOTIFY_VIEW_ALBUMS = "spotify.view.albums"
    SPOTIFY_VIEW_ARTISTS = "spotify.view.artists"
    SPOTIFY_VIEW_PLAYLISTS = "spotify.view.playlists"
    SPOTIFY_VIEW_PODCASTS = "spotify.view.podcasts"
    SPOTIFY_VIEW_EPISODES = "spotify.view.episodes"
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
    ADD_NAMED_BOOKMARK = "action.bookmark.addNamed"
    PREVIOUS_BOOKMARK = "transport.bookmark.previous"
    NEXT_BOOKMARK = "transport.bookmark.next"
    PREVIOUS_NAMED_BOOKMARK = "transport.bookmark.named.previous"
    NEXT_NAMED_BOOKMARK = "transport.bookmark.named.next"
    VIEW_CHAPTERS = "view.chapters"
    ADD_NAMED_CHAPTER = "action.chapter.addNamed"
    PREVIOUS_CHAPTER = "transport.chapter.previous"
    NEXT_CHAPTER = "transport.chapter.next"
    VIEW_BACK = "view.history.back"
    VIEW_FORWARD = "view.history.forward"
    PLAYBACK_HISTORY_OLDER = "transport.history.older"
    PLAYBACK_HISTORY_NEWER = "transport.history.newer"
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
    RADIO_JUMP_LIVE = "radio.jumpLive"
    # CommandIds.SettingsToggleSeekMessages, Ctrl+Shift+G
    # (MainWindow.xaml.cs:21670 i 22188).
    TOGGLE_SEEK_MESSAGES = "settings.toggleSeekMessages"
    GENERAL_SETTINGS = "settings.general"
    REFRESH_COMMON_DATA = "profile.commonData.refresh"
    COMMON_DATA_STATUS = "profile.commonData.status"
    EXPORT_BACKUP = "profile.backup.export"
    IMPORT_BACKUP = "profile.backup.import"
    SELECT_AUDIO_OUTPUT = "audio.selectOutput"
    PREVIOUS_AUDIO_OUTPUT = "audio.previousOutput"
    NEXT_AUDIO_OUTPUT = "audio.nextOutput"
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
    VIEW_WIIM_NETWORK_STREAMS = "wiim.networkStreams.view"
    WIIM_SELECT_INPUT = "wiim.input.select"
    WIIM_SELECT_OUTPUT = "wiim.output.select"
    WIIM_SELECT_EQUALIZER = "wiim.equalizer.select"
    WIIM_SELECT_REPEAT = "wiim.repeat.select"
    WIIM_TOGGLE_SHUFFLE = "wiim.shuffle.toggle"
    WIIM_SET_SLEEP_TIMER = "wiim.sleepTimer.set"
    WIIM_PREVIOUS_SOURCE = "wiim.source.previous"
    WIIM_NEXT_SOURCE = "wiim.source.next"
    ADD_WIIM_NETWORK_STREAM = "wiim.networkStreams.add"
    EDIT_WIIM_NETWORK_STREAM = "wiim.networkStreams.edit"
    REMOVE_WIIM_NETWORK_STREAMS = "wiim.networkStreams.remove"
    IMPORT_WIIM_NETWORK_STREAMS = "wiim.networkStreams.import"
    EXPORT_WIIM_NETWORK_STREAMS = "wiim.networkStreams.export"
    SORT_WIIM_NETWORK_STREAMS_ADDED = "wiim.networkStreams.sort.added"
    SORT_WIIM_NETWORK_STREAMS_ALPHABETICAL = "wiim.networkStreams.sort.alphabetical"
    SORT_WIIM_NETWORK_STREAMS_CUSTOM = "wiim.networkStreams.sort.custom"
    MOVE_WIIM_NETWORK_STREAM_UP = "wiim.networkStreams.moveUp"
    MOVE_WIIM_NETWORK_STREAM_DOWN = "wiim.networkStreams.moveDown"
    CUT_WIIM_NETWORK_STREAMS = "wiim.networkStreams.cut"
    PASTE_WIIM_NETWORK_STREAMS = "wiim.networkStreams.paste"
    ADD_SONOS_OWN_STREAM = "sonos.ownStreams.add"
    EDIT_SONOS_OWN_STREAM = "sonos.ownStreams.edit"
    REMOVE_SONOS_OWN_STREAMS = "sonos.ownStreams.remove"
    IMPORT_SONOS_OWN_STREAMS = "sonos.ownStreams.import"
    SORT_SONOS_OWN_STREAMS_ADDED = "sonos.ownStreams.sort.added"
    SORT_SONOS_OWN_STREAMS_ALPHABETICAL = "sonos.ownStreams.sort.alphabetical"
    SORT_SONOS_OWN_STREAMS_CUSTOM = "sonos.ownStreams.sort.custom"
    MOVE_SONOS_OWN_STREAM_UP = "sonos.ownStreams.moveUp"
    MOVE_SONOS_OWN_STREAM_DOWN = "sonos.ownStreams.moveDown"
    CUT_SONOS_OWN_STREAMS = "sonos.ownStreams.cut"
    PASTE_SONOS_OWN_STREAMS = "sonos.ownStreams.paste"
    EXPORT_SONOS_OWN_STREAMS = "sonos.ownStreams.export"
    OPEN_FOLDER_DIALOG = "files.openFolder"
    OPEN_FILE_DIALOG = "files.openFile"
    OPEN_CONTAINING_FOLDER = "files.openContainingFolder"
    COPY_NAME = "clipboard.copyName"
    COPY_ADDRESS = "clipboard.copyAddress"
    #: Ctrl+X jest kontekstowe jak w głównym AMC: w otwartej playliście oraz
    #: Bibliotece/Ulubionych Radia rozpoczyna wewnętrzne przenoszenie, a w
    #: pozostałych lokalnych widokach przygotowuje prawdziwe pliki do ruchu.
    CUT_FILE = "clipboard.cutFile"
    PASTE_OR_MOVE = "clipboard.pasteOrMove"
    MOVE_LIST_ITEM_UP = "library.moveItemUp"
    MOVE_LIST_ITEM_DOWN = "library.moveItemDown"
    RENAME_LIBRARY_ITEM = "library.renameItem"
    RENAME_LOCAL_FILE = "library.renameFile"
    REMOVE_SELECTED = "library.removeSelected"
    RECYCLE_SELECTED = "library.recycleSelected"
    STATION_ADD = "radio.add"
    STATION_EDIT = "radio.edit"
    STATION_DELETE = "radio.delete"
    STATION_IMPORT = "radio.import"
    STATION_EXPORT_FAVORITES = "radio.exportFavorites"
    RECORD_TOGGLE = "radio.recording.toggle"
    RECORD_PAUSE = "radio.recording.pause"
    RECORD_SPLIT = "radio.recording.split"
    RECORD_STOP_ALL = "radio.recording.stopAll"
    VIEW_ACTIVE_RECORDINGS = "radio.recording.activeView"
    RECORDING_OUTPUT_INFORMATION = "radio.recording.outputInformation"
    VIEW_RECORDED_RADIO_FILES = "radio.recording.completedView"
    VIEW_DOWNLOADED_FILES = "downloads.history"
    ADD_RADIO_SCHEDULE = "radio.recording.schedule.add"
    MANAGE_RADIO_SCHEDULES = "radio.recording.schedules"
    SORT_RADIO_COLLECTION_ADDED = "radio.collection.sort.added"
    SORT_RADIO_COLLECTION_ALPHABETICAL = "radio.collection.sort.alphabetical"
    SORT_RADIO_COLLECTION_CUSTOM = "radio.collection.sort.custom"
    VIEW_PODCAST_INBOX = "podcasts.inbox"
    VIEW_PODCAST_DOWNLOADS = "podcasts.downloads"
    SORT_PODCAST_INBOX_ADDED = "podcasts.inbox.sort.added"
    SORT_PODCAST_INBOX_ALPHABETICAL = "podcasts.inbox.sort.alphabetical"
    SORT_PODCAST_INBOX_BY_PODCAST = "podcasts.inbox.sort.byPodcast"
    SORT_PODCAST_LIBRARY_ADDED = "podcasts.library.sort.added"
    SORT_PODCAST_LIBRARY_ALPHABETICAL = "podcasts.library.sort.alphabetical"
    SORT_PODCAST_LIBRARY_CUSTOM = "podcasts.library.sort.custom"
    SORT_TIDAL_COLLECTION_ADDED = "tidal.collection.sort.added"
    SORT_TIDAL_COLLECTION_ALPHABETICAL = "tidal.collection.sort.alphabetical"
    SORT_TIDAL_COLLECTION_CUSTOM = "tidal.collection.sort.custom"
    SORT_SPOTIFY_COLLECTION_ADDED = "spotify.collection.sort.added"
    SORT_SPOTIFY_COLLECTION_ALPHABETICAL = "spotify.collection.sort.alphabetical"
    SORT_SPOTIFY_COLLECTION_CATALOG = "spotify.collection.sort.catalog"
    ADD_PODCAST_SOURCE = "podcasts.source.add"
    IMPORT_PODCAST_OPML = "podcasts.opml.import"
    EXPORT_PODCAST_OPML = "podcasts.opml.export"
    EXPORT_YOUTUBE_SUBSCRIPTIONS = "podcasts.youtube.export"
    YOUTUBE_ACCOUNT = "youtube.account.manage"
    YOUTUBE_BROADCASTS = "youtube.broadcasts.open"
    REFRESH_YOUTUBE_BROADCASTS = "youtube.broadcasts.refresh"
    ADD_YOUTUBE_LIVE_STATION = "youtube.liveStation.add"
    VIEW_YOUTUBE_LIVE_AGGREGATE = "youtube.broadcasts.aggregate.open"
    CONFIGURE_YOUTUBE_LIVE_CHANNELS = "youtube.broadcasts.aggregate.configure"
    TOGGLE_YOUTUBE_LIVE_AGGREGATE_CHANNEL = "youtube.broadcasts.aggregate.channel.toggle"
    REFRESH_YOUTUBE_LIVE_AGGREGATE = "youtube.broadcasts.aggregate.refresh"
    RECORD_YOUTUBE_LIVE_NOW = "youtube.broadcasts.aggregate.recordNow"
    SCHEDULE_YOUTUBE_LIVE = "youtube.broadcasts.aggregate.schedule"
    REFRESH_PODCAST = "podcasts.refresh.current"
    REFRESH_PODCAST_LIBRARY = "podcasts.refresh.all"
    DOWNLOAD_PODCAST_EPISODES = "podcasts.download.selected"
    RADIOARCHIWISTA_DOWNLOAD = "radioarchiwista.download.selected"
    RADIOARCHIWISTA_REFRESH = "radioarchiwista.catalog.refresh"
    SAVE_PODCAST_EPISODE_AS = "podcasts.download.saveAs"
    DOWNLOAD_YOUTUBE_VIDEOS = "podcasts.youtube.video.download"
    SAVE_YOUTUBE_VIDEO_AS = "podcasts.youtube.video.saveAs"
    SHOW_PODCAST_DESCRIPTION = "podcasts.description"
    CURRENT_RADIO_BROADCAST_INFORMATION = "radio.currentBroadcastInformation"
    RECOGNIZE_RADIO_TRACK = "radio.recognition.now"
    TOGGLE_RADIO_RECOGNITION_MONITORING = "radio.recognition.monitor.toggle"
    TOGGLE_RADIO_RECOGNITION_ANNOUNCEMENTS = "radio.recognition.announcements.toggle"
    VIEW_RADIO_RECOGNITION_HISTORY = "radio.recognition.history"
    GO_TO_RELATED_PODCAST = "podcasts.goToRelated"
    TOGGLE_PODCAST_FAVORITE = "podcasts.favorite.toggle"
    TOGGLE_RADIOARCHIWISTA_FAVORITE = "radioarchiwista.favorite.toggle"
    TOGGLE_PODCAST_LIBRARY = "podcasts.library.toggle"
    PODCAST_PLAYBACK_OPTIONS = "podcasts.playbackOptions"
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
    # (Ctrl+F / Ctrl+Shift+F, MainWindow.xaml:501-502): tamte otwieraja osobne
    # okno wynikow, a tu zwezamy liste, ktora juz jest na ekranie.
    FOCUS_FILTER = "list.filter"
    SEARCH_CURRENT = "view.search.current"
    SEARCH_ALL = "view.search.all"
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
    ITEM_PROPERTIES = "list.itemProperties"
    #: Ustawienia sesji (Ctrl+Alt+Enter). Port ``SessionPlaybackOptionsEditor``.
    #:
    #: Jedno WSPOLNE polecenie dla wszystkich sesji -- dialog sam pokazuje
    #: ustawienia, ktore dana sesja umie wykonać (``session_options``).
    #: Oryginal nie
    #: ma tego gestu w oknie (otwiera opcje z menu sesji), wiec klawisz jest
    #: WLASNY dla portu: Ctrl+Alt+Enter nie koliduje ani z aktywacja (gole
    #: Enter), ani z para Ctrl+Alt+Left/Right (przewijanie wlasnym krokiem).
    SESSION_OPTIONS = "session.options"
    HELP = "help"
    KEYBOARD_HELP = "keyboard.help"
    CHECK_APPLICATION_UPDATE = "help.checkApplicationUpdate"
    UPDATE_MEDIA_COMPONENTS = "help.updateMediaComponents"
    REPORT_PROBLEM = "help.reportProblem"
    VERSION_INFO = "help.version"


@dataclass(frozen=True, slots=True)
class CommandPresentation:
    """Jedna użytkowa prezentacja polecenia na wszystkich powierzchniach.

    ``name`` jest krótką nazwą menu i palety, a ``description`` objaśnia
    wynik w pełnej Pomocy oraz w Pomocy klawiatury. Model nie zawiera
    mnemoniców menu ani identyfikatorów technicznych.
    """

    action: Action
    name: str
    description: str
    shortcut: str
    menu_label: str | None = None


_STATIC_COMMAND_PRESENTATIONS: dict[Action, CommandPresentation] = {
    Action.VIEW_RADIO_RECOGNITION_HISTORY: CommandPresentation(
        Action.VIEW_RADIO_RECOGNITION_HISTORY,
        "Rozpoznane utwory…",
        (
            "Pokaż rozpoznane utwory; Enter otwiera utwór, Ctrl+Enter album, "
            "a prawa strzałka najważniejsze działania"
        ),
        "Ctrl+Alt+S",
        "Rozpoznan&e utwory…",
    ),
    Action.VIEW_PODCAST_INBOX: CommandPresentation(
        Action.VIEW_PODCAST_INBOX,
        "Nowe odcinki i materiały",
        (
            "Otwórz nowe odcinki i materiały; w otwartej skrzynce "
            "wczytaj kolejne 150 pozycji"
        ),
        "Ctrl+I",
        "&Nowe odcinki i materiały",
    ),
    Action.CHECK_APPLICATION_UPDATE: CommandPresentation(
        Action.CHECK_APPLICATION_UPDATE,
        "Sprawdź aktualizację AMC",
        "Sprawdź i zainstaluj najnowsze wydanie z kanału przypisanego do tej wersji AMC",
        "",
        "Sprawdź &aktualizację AMC…",
    ),
}


# Historia pochodzi z rozpoznawania Radia, lecz jest trwałym zbiorem i może
# zostać otwarta także podczas pracy z lokalną Biblioteką. Pozostałe sesje nie
# dostają skrótu, pozycji menu ani wpisu pomocy/palety.
RECOGNITION_HISTORY_SESSIONS = (SessionId.FILES, SessionId.RADIO)


def command_presentation(action: Action) -> CommandPresentation | None:
    """Jedna nazwa, opis i etykieta menu dla polecenia aplikacji."""

    presentation = _STATIC_COMMAND_PRESENTATIONS.get(action)
    if presentation is None or i18n.active_language() == i18n.POLISH:
        return presentation
    return CommandPresentation(
        action=action,
        name=i18n.command_name(action.value, presentation.name),
        description=i18n.command_description(
            action.value, presentation.description
        ),
        shortcut=presentation.shortcut,
        menu_label=(
            i18n.menu_action_label(action.value, presentation.menu_label)
            if presentation.menu_label is not None
            else None
        ),
    )


_CTRL_F5_PRESENTATIONS: dict[str, CommandPresentation] = {
    "pliki lokalne": CommandPresentation(
        Action.MANAGE_LIBRARY_FOLDERS,
        "Foldery Biblioteki",
        "Foldery Biblioteki: dodaj, odśwież albo bezpiecznie odłącz folder",
        "Ctrl+F5",
    ),
    "podcasty i youtube": CommandPresentation(
        Action.REFRESH_PODCAST_LIBRARY,
        "Odśwież wszystkie podcasty, kanały YouTube i playlisty",
        "Odśwież wszystkie podcasty, kanały YouTube i playlisty",
        "Ctrl+F5",
    ),
    "radio": CommandPresentation(
        Action.REFRESH_RADIO_STATIONS,
        "Odśwież stacje Radia",
        "Odśwież stacje i bieżący widok Radia",
        "Ctrl+F5",
    ),
    "spotify": CommandPresentation(
        Action.SESSION_CONFIGURE_CURRENT,
        "Konto Spotify",
        "Otwórz konto Spotify i możliwość ponownego logowania",
        "Ctrl+F5",
    ),
    "tidal": CommandPresentation(
        Action.SESSION_CONFIGURE_CURRENT,
        "Konto i synchronizacja TIDAL",
        "Otwórz konto, logowanie i synchronizację TIDAL",
        "Ctrl+F5",
    ),
    "wiim": CommandPresentation(
        Action.SESSION_CONFIGURE_CURRENT,
        "Urządzenia WiiM",
        "Odśwież listę zapisanych urządzeń WiiM",
        "Ctrl+F5",
    ),
    "sonos": CommandPresentation(
        Action.SELECT_SONOS_TARGET,
        "Głośniki i grupy Sonos",
        "Wybierz dom i grupę sterowania Sonos; przy braku połączenia otwórz konto Sonos",
        "Ctrl+F5",
    ),
    "frontier smart": CommandPresentation(
        Action.SESSION_CONFIGURE_CURRENT,
        "Urządzenia Frontier Smart",
        "Pokaż zapisane urządzenia Frontier Smart i opcjonalnie wyszukaj nowe",
        "Ctrl+F5",
    ),
    "radio archiwista": CommandPresentation(
        Action.SESSION_CONFIGURE_CURRENT,
        "Adres katalogu Radio Archiwisty",
        "Ustaw adres katalogu nagrań Radio Archiwisty",
        "Ctrl+F5",
    ),
}


def ctrl_f5_presentation(session_label: str | None) -> CommandPresentation | None:
    """Precyzyjna funkcja ``Ctrl+F5`` dla bieżącej sesji."""

    normalized_label = (session_label or "").strip().casefold()
    normalized_label = {
        "local files": "pliki lokalne",
        "podcasts and youtube": "podcasty i youtube",
    }.get(normalized_label, normalized_label)
    presentation = _CTRL_F5_PRESENTATIONS.get(
        normalized_label
    )
    if presentation is None or i18n.active_language() == i18n.POLISH:
        return presentation
    return CommandPresentation(
        action=presentation.action,
        name=i18n.command_name(presentation.action.value, presentation.name),
        description=i18n.command_description(
            presentation.action.value, presentation.description
        ),
        shortcut=presentation.shortcut,
        menu_label=presentation.menu_label,
    )


# Polecenia ratunkowe i porzadkowe, ktore dotycza calej aplikacji, a nie
# aktualnie wybranego dostawcy. Kazda restrykcyjna lista sesji dziedziczy ten
# jeden zbior, zeby skrot, menu, paleta, pomoc i dispatcher nie rozjechaly sie.
SESSION_INDEPENDENT_ACTIONS = frozenset({
    Action.RECORD_STOP_ALL,
    Action.CHECK_APPLICATION_UPDATE,
})


# TIDAL ma osobny tor: katalog czyta oficjalne API, a dzwiek odtwarza oryginalna
# aplikacja TIDAL. Jawna lista obslugiwanych polecen jest celowo restrykcyjna:
# nowy skrot nie moze przypadkiem trafic do lokalnego silnika AMC.
TIDAL_SUPPORTED_ACTIONS = frozenset({
    *SESSION_INDEPENDENT_ACTIONS,
    Action.SESSION_FILES,
    Action.SESSION_RADIO,
    Action.SESSION_PODCASTS,
    Action.SESSION_TIDAL,
    Action.SESSION_WIIM,
    Action.SESSION_SONOS,
    Action.SESSION_SPOTIFY,
    Action.SESSION_FRONTIER_SMART,
    Action.SESSION_RADIOARCHIWISTA,
    Action.SESSION_LIST,
    Action.SESSION_ENABLE,
    Action.SESSION_DISABLE_CURRENT,
    Action.SESSION_CONFIGURE_CURRENT,
    Action.PREVIOUS_SESSION,
    Action.NEXT_SESSION,
    Action.COMMAND_PALETTE,
    Action.ACTIVATE,
    Action.PARENT_FOLDER,
    Action.CLOSE_LIST_PREVIEW,
    Action.SHOW_LIST,
    Action.PLAY_PAUSE,
    Action.QUEUE_NEXT,
    Action.QUEUE_PREVIOUS,
    Action.ADD_TO_QUEUE,
    Action.TOGGLE_FAVORITE,
    Action.TIDAL_TOGGLE_LIBRARY,
    Action.TIDAL_TOGGLE_FAVORITE,
    Action.TIDAL_RELATIONS,
    Action.SORT_TIDAL_COLLECTION_ADDED,
    Action.SORT_TIDAL_COLLECTION_ALPHABETICAL,
    Action.SORT_TIDAL_COLLECTION_CUSTOM,
    Action.TIME_ELAPSED,
    Action.TIME_REMAINING,
    Action.TIME_TOTAL,
    Action.SESSION_OPTIONS,
    Action.VIEW_PRESETS,
    Action.GENERAL_SETTINGS,
    Action.REFRESH_COMMON_DATA,
    Action.COMMON_DATA_STATUS,
    Action.EXPORT_BACKUP,
    Action.IMPORT_BACKUP,
    Action.TOGGLE_SEEK_MESSAGES,
    Action.ASSIGN_PRESET,
    Action.PRESET_1,
    Action.PRESET_2,
    Action.PRESET_3,
    Action.PRESET_4,
    Action.PRESET_5,
    Action.PRESET_6,
    Action.PRESET_7,
    Action.PRESET_8,
    Action.PRESET_9,
    Action.PRESET_10,
    Action.PRESET_11,
    Action.PRESET_12,
    Action.QUICK_INFORMATION,
    Action.ITEM_PROPERTIES,
    Action.COPY_NAME,
    Action.COPY_ADDRESS,
    Action.FOCUS_FILTER,
    Action.SEARCH_CURRENT,
    Action.SEARCH_ALL,
    Action.VIEW_LIBRARY,
    Action.VIEW_HISTORY,
    Action.REMOVE_SELECTED,
    Action.VIEW_BACK,
    Action.VIEW_FORWARD,
    Action.VIEW_FAVORITES,
    Action.VIEW_PLAYLISTS,
    Action.VIEW_SAVED_QUEUE,
    Action.VIEW_ACTIVE_RECORDINGS,
    Action.VIEW_RECORDED_RADIO_FILES,
    Action.VIEW_DOWNLOADED_FILES,
    Action.MANAGE_RADIO_SCHEDULES,
    Action.VIEW_PODCAST_INBOX,
    Action.VIEW_YOUTUBE_LIVE_AGGREGATE,
    Action.HELP,
    Action.KEYBOARD_HELP,
    Action.UPDATE_MEDIA_COMPONENTS,
    Action.REPORT_PROBLEM,
    Action.VERSION_INFO,
})


# Czysty kontrakt jednej sesji Spotify. Jawna lista blokuje przypadkowe
# skierowanie operacji lokalnych plików albo innej usługi do jej kontrolera.
SPOTIFY_SUPPORTED_ACTIONS = frozenset({
    *SESSION_INDEPENDENT_ACTIONS,
    Action.SESSION_FILES,
    Action.SESSION_RADIO,
    Action.SESSION_PODCASTS,
    Action.SESSION_TIDAL,
    Action.SESSION_WIIM,
    Action.SESSION_SONOS,
    Action.SESSION_SPOTIFY,
    Action.SESSION_FRONTIER_SMART,
    Action.SESSION_RADIOARCHIWISTA,
    Action.SESSION_LIST,
    Action.SESSION_ENABLE,
    Action.SESSION_DISABLE_CURRENT,
    Action.SESSION_CONFIGURE_CURRENT,
    Action.PREVIOUS_SESSION,
    Action.NEXT_SESSION,
    Action.COMMAND_PALETTE,
    Action.ACTIVATE,
    Action.PARENT_FOLDER,
    Action.CLOSE_LIST_PREVIEW,
    Action.SHOW_PLAYER,
    Action.SHOW_LIST,
    Action.PLAY_PAUSE,
    Action.TOGGLE_SHUFFLE,
    Action.TOGGLE_MUTE,
    Action.QUEUE_NEXT,
    Action.QUEUE_PREVIOUS,
    Action.SEEK_BACK_10,
    Action.SEEK_FORWARD_10,
    Action.SEEK_BACK_30,
    Action.SEEK_FORWARD_30,
    Action.SEEK_BACK_60,
    Action.SEEK_FORWARD_60,
    Action.SEEK_BACK_CUSTOM,
    Action.SEEK_FORWARD_CUSTOM,
    Action.SEEK_PERCENT_0,
    Action.SEEK_PERCENT_10,
    Action.SEEK_PERCENT_20,
    Action.SEEK_PERCENT_30,
    Action.SEEK_PERCENT_40,
    Action.SEEK_PERCENT_50,
    Action.SEEK_PERCENT_60,
    Action.SEEK_PERCENT_70,
    Action.SEEK_PERCENT_80,
    Action.SEEK_PERCENT_90,
    Action.TRACK_START,
    Action.TRACK_END,
    Action.VOLUME_UP_5,
    Action.VOLUME_DOWN_5,
    Action.VOLUME_UP_1,
    Action.VOLUME_DOWN_1,
    Action.RATE_DOWN,
    Action.RATE_UP,
    Action.RATE_RESET,
    Action.TIME_ELAPSED,
    Action.TIME_REMAINING,
    Action.TIME_TOTAL,
    Action.ADD_BOOKMARK,
    Action.ADD_NAMED_BOOKMARK,
    Action.PREVIOUS_BOOKMARK,
    Action.NEXT_BOOKMARK,
    Action.PREVIOUS_NAMED_BOOKMARK,
    Action.NEXT_NAMED_BOOKMARK,
    Action.VIEW_CHAPTERS,
    Action.ADD_NAMED_CHAPTER,
    Action.PREVIOUS_CHAPTER,
    Action.NEXT_CHAPTER,
    Action.SELECT_AUDIO_OUTPUT,
    Action.PREVIOUS_AUDIO_OUTPUT,
    Action.NEXT_AUDIO_OUTPUT,
    Action.TOGGLE_SEEK_MESSAGES,
    Action.QUICK_INFORMATION,
    Action.ITEM_PROPERTIES,
    Action.COPY_NAME,
    Action.COPY_ADDRESS,
    Action.FOCUS_FILTER,
    Action.SEARCH_CURRENT,
    Action.SEARCH_ALL,
    Action.VIEW_LIBRARY,
    Action.VIEW_HISTORY,
    Action.VIEW_ACTIVE_RECORDINGS,
    Action.VIEW_RECORDED_RADIO_FILES,
    Action.VIEW_DOWNLOADED_FILES,
    Action.VIEW_BACK,
    Action.VIEW_FORWARD,
    Action.PLAYBACK_HISTORY_OLDER,
    Action.PLAYBACK_HISTORY_NEWER,
    Action.VIEW_PRESETS,
    Action.GENERAL_SETTINGS,
    Action.REFRESH_COMMON_DATA,
    Action.COMMON_DATA_STATUS,
    Action.EXPORT_BACKUP,
    Action.IMPORT_BACKUP,
    Action.ASSIGN_PRESET,
    Action.PRESET_1,
    Action.PRESET_2,
    Action.PRESET_3,
    Action.PRESET_4,
    Action.PRESET_5,
    Action.PRESET_6,
    Action.PRESET_7,
    Action.PRESET_8,
    Action.PRESET_9,
    Action.PRESET_10,
    Action.PRESET_11,
    Action.PRESET_12,
    Action.REMOVE_SELECTED,
    Action.SESSION_OPTIONS,
    Action.SPOTIFY_RELATIONS,
    Action.SPOTIFY_TOGGLE_LIBRARY,
    Action.SPOTIFY_TOGGLE_FAVORITE,
    Action.MANAGE_PLAYLIST_MEMBERSHIP,
    Action.SPOTIFY_VIEW_LIKED,
    Action.SPOTIFY_VIEW_ALBUMS,
    Action.SPOTIFY_VIEW_ARTISTS,
    Action.SPOTIFY_VIEW_PLAYLISTS,
    Action.SPOTIFY_VIEW_PODCASTS,
    Action.SPOTIFY_VIEW_EPISODES,
    Action.SORT_SPOTIFY_COLLECTION_ADDED,
    Action.SORT_SPOTIFY_COLLECTION_ALPHABETICAL,
    Action.SORT_SPOTIFY_COLLECTION_CATALOG,
    Action.SHOW_PODCAST_DESCRIPTION,
    Action.VIEW_YOUTUBE_LIVE_AGGREGATE,
    Action.HELP,
    Action.KEYBOARD_HELP,
    Action.UPDATE_MEDIA_COMPONENTS,
    Action.REPORT_PROBLEM,
    Action.VERSION_INFO,
})


# WiiM jest autonomicznym odtwarzaczem sieciowym. Dopoki polecenie nie ma
# jawnej drogi ``wiim.*`` w hoscie, nie moze trafic do lokalnego silnika audio.
WIIM_SUPPORTED_ACTIONS = frozenset({
    *SESSION_INDEPENDENT_ACTIONS,
    Action.SESSION_FILES,
    Action.SESSION_RADIO,
    Action.SESSION_PODCASTS,
    Action.SESSION_TIDAL,
    Action.SESSION_WIIM,
    Action.SESSION_SONOS,
    Action.SESSION_SPOTIFY,
    Action.SESSION_FRONTIER_SMART,
    Action.SESSION_RADIOARCHIWISTA,
    Action.SESSION_LIST,
    Action.SESSION_ENABLE,
    Action.SESSION_DISABLE_CURRENT,
    Action.SESSION_CONFIGURE_CURRENT,
    Action.PREVIOUS_SESSION,
    Action.NEXT_SESSION,
    Action.COMMAND_PALETTE,
    Action.ACTIVATE,
    Action.PARENT_FOLDER,
    Action.CLOSE_LIST_PREVIEW,
    Action.SHOW_PLAYER,
    Action.SHOW_LIST,
    Action.PLAY_PAUSE,
    Action.QUEUE_NEXT,
    Action.QUEUE_PREVIOUS,
    Action.VOLUME_UP_5,
    Action.VOLUME_DOWN_5,
    Action.VOLUME_UP_1,
    Action.VOLUME_DOWN_1,
    Action.TIME_ELAPSED,
    Action.TIME_REMAINING,
    Action.TIME_TOTAL,
    Action.SESSION_OPTIONS,
    Action.VIEW_PRESETS,
    Action.ASSIGN_PRESET,
    *(Action[f"PRESET_{slot}"] for slot in range(1, 13)),
    Action.VIEW_WIIM_NETWORK_STREAMS,
    Action.WIIM_SELECT_INPUT,
    Action.WIIM_SELECT_OUTPUT,
    Action.WIIM_SELECT_EQUALIZER,
    Action.WIIM_SELECT_REPEAT,
    Action.WIIM_TOGGLE_SHUFFLE,
    Action.WIIM_SET_SLEEP_TIMER,
    Action.WIIM_PREVIOUS_SOURCE,
    Action.WIIM_NEXT_SOURCE,
    Action.ADD_WIIM_NETWORK_STREAM,
    Action.EDIT_WIIM_NETWORK_STREAM,
    Action.REMOVE_WIIM_NETWORK_STREAMS,
    Action.IMPORT_WIIM_NETWORK_STREAMS,
    Action.EXPORT_WIIM_NETWORK_STREAMS,
    Action.SORT_WIIM_NETWORK_STREAMS_ADDED,
    Action.SORT_WIIM_NETWORK_STREAMS_ALPHABETICAL,
    Action.SORT_WIIM_NETWORK_STREAMS_CUSTOM,
    Action.MOVE_WIIM_NETWORK_STREAM_UP,
    Action.MOVE_WIIM_NETWORK_STREAM_DOWN,
    Action.CUT_WIIM_NETWORK_STREAMS,
    Action.PASTE_WIIM_NETWORK_STREAMS,
    Action.GENERAL_SETTINGS,
    Action.REFRESH_COMMON_DATA,
    Action.COMMON_DATA_STATUS,
    Action.EXPORT_BACKUP,
    Action.IMPORT_BACKUP,
    Action.TOGGLE_SEEK_MESSAGES,
    Action.QUICK_INFORMATION,
    Action.ITEM_PROPERTIES,
    Action.COPY_NAME,
    Action.COPY_ADDRESS,
    Action.FOCUS_FILTER,
    Action.VIEW_LIBRARY,
    Action.VIEW_HISTORY,
    Action.VIEW_BACK,
    Action.VIEW_FORWARD,
    Action.PLAYBACK_HISTORY_OLDER,
    Action.PLAYBACK_HISTORY_NEWER,
    Action.REMOVE_SELECTED,
    Action.VIEW_ACTIVE_RECORDINGS,
    Action.VIEW_RECORDED_RADIO_FILES,
    Action.VIEW_DOWNLOADED_FILES,
    Action.MANAGE_RADIO_SCHEDULES,
    Action.VIEW_PODCAST_INBOX,
    Action.VIEW_YOUTUBE_LIVE_AGGREGATE,
    Action.HELP,
    Action.KEYBOARD_HELP,
    Action.UPDATE_MEDIA_COMPONENTS,
    Action.REPORT_PROBLEM,
    Action.VERSION_INFO,
})


# Sonos jest autonomicznym odtwarzaczem sieciowym. Polecenia spoza tej listy
# nie mogą przypadkiem trafić do lokalnego silnika plików.
SONOS_SUPPORTED_ACTIONS = frozenset({
    *SESSION_INDEPENDENT_ACTIONS,
    Action.SESSION_FILES,
    Action.SESSION_RADIO,
    Action.SESSION_PODCASTS,
    Action.SESSION_TIDAL,
    Action.SESSION_WIIM,
    Action.SESSION_SONOS,
    Action.SESSION_SPOTIFY,
    Action.SESSION_FRONTIER_SMART,
    Action.SESSION_RADIOARCHIWISTA,
    Action.SESSION_LIST,
    Action.SESSION_ENABLE,
    Action.SESSION_DISABLE_CURRENT,
    Action.SESSION_CONFIGURE_CURRENT,
    Action.PREVIOUS_SESSION,
    Action.NEXT_SESSION,
    Action.COMMAND_PALETTE,
    Action.SESSION_OPTIONS,
    Action.SELECT_SONOS_TARGET,
    Action.ACTIVATE,
    Action.PARENT_FOLDER,
    Action.CLOSE_LIST_PREVIEW,
    Action.SHOW_PLAYER,
    Action.SHOW_LIST,
    Action.PLAY_PAUSE,
    Action.QUEUE_NEXT,
    Action.QUEUE_PREVIOUS,
    Action.SEEK_BACK_10,
    Action.SEEK_FORWARD_10,
    Action.SEEK_BACK_30,
    Action.SEEK_FORWARD_30,
    Action.SEEK_BACK_60,
    Action.SEEK_FORWARD_60,
    Action.SEEK_BACK_CUSTOM,
    Action.SEEK_FORWARD_CUSTOM,
    Action.SEEK_PERCENT_0,
    Action.SEEK_PERCENT_10,
    Action.SEEK_PERCENT_20,
    Action.SEEK_PERCENT_30,
    Action.SEEK_PERCENT_40,
    Action.SEEK_PERCENT_50,
    Action.SEEK_PERCENT_60,
    Action.SEEK_PERCENT_70,
    Action.SEEK_PERCENT_80,
    Action.SEEK_PERCENT_90,
    Action.VOLUME_UP_5,
    Action.VOLUME_DOWN_5,
    Action.VOLUME_UP_1,
    Action.VOLUME_DOWN_1,
    Action.TIME_ELAPSED,
    Action.TIME_REMAINING,
    Action.TIME_TOTAL,
    Action.VIEW_PRESETS,
    Action.ASSIGN_PRESET,
    Action.PRESET_1,
    Action.PRESET_2,
    Action.PRESET_3,
    Action.PRESET_4,
    Action.PRESET_5,
    Action.PRESET_6,
    Action.PRESET_7,
    Action.PRESET_8,
    Action.PRESET_9,
    Action.PRESET_10,
    Action.PRESET_11,
    Action.PRESET_12,
    Action.GENERAL_SETTINGS,
    Action.REFRESH_COMMON_DATA,
    Action.COMMON_DATA_STATUS,
    Action.EXPORT_BACKUP,
    Action.IMPORT_BACKUP,
    Action.TOGGLE_SEEK_MESSAGES,
    Action.QUICK_INFORMATION,
    Action.ITEM_PROPERTIES,
    Action.COPY_NAME,
    Action.COPY_ADDRESS,
    Action.VIEW_LIBRARY,
    Action.VIEW_HISTORY,
    Action.VIEW_BACK,
    Action.VIEW_FORWARD,
    Action.PLAYBACK_HISTORY_OLDER,
    Action.PLAYBACK_HISTORY_NEWER,
    Action.REMOVE_SELECTED,
    Action.VIEW_FAVORITES,
    Action.VIEW_PLAYLISTS,
    Action.ADD_SONOS_OWN_STREAM,
    Action.EDIT_SONOS_OWN_STREAM,
    Action.REMOVE_SONOS_OWN_STREAMS,
    Action.IMPORT_SONOS_OWN_STREAMS,
    Action.SORT_SONOS_OWN_STREAMS_ADDED,
    Action.SORT_SONOS_OWN_STREAMS_ALPHABETICAL,
    Action.SORT_SONOS_OWN_STREAMS_CUSTOM,
    Action.MOVE_SONOS_OWN_STREAM_UP,
    Action.MOVE_SONOS_OWN_STREAM_DOWN,
    Action.CUT_SONOS_OWN_STREAMS,
    Action.PASTE_SONOS_OWN_STREAMS,
    Action.EXPORT_SONOS_OWN_STREAMS,
    Action.FOCUS_FILTER,
    Action.VIEW_ACTIVE_RECORDINGS,
    Action.VIEW_RECORDED_RADIO_FILES,
    Action.VIEW_DOWNLOADED_FILES,
    Action.MANAGE_RADIO_SCHEDULES,
    Action.VIEW_PODCAST_INBOX,
    Action.VIEW_YOUTUBE_LIVE_AGGREGATE,
    Action.HELP,
    Action.KEYBOARD_HELP,
    Action.UPDATE_MEDIA_COMPONENTS,
    Action.REPORT_PROBLEM,
    Action.VERSION_INFO,
})


# Frontier Smart jest autonomicznym radiem. Jawna lista chroni przed
# skierowaniem polecenia lokalnego silnika albo innej usługi do FSAPI.
FRONTIER_SMART_SUPPORTED_ACTIONS = frozenset({
    *SESSION_INDEPENDENT_ACTIONS,
    Action.SESSION_FILES,
    Action.SESSION_RADIO,
    Action.SESSION_PODCASTS,
    Action.SESSION_TIDAL,
    Action.SESSION_WIIM,
    Action.SESSION_SONOS,
    Action.SESSION_SPOTIFY,
    Action.SESSION_FRONTIER_SMART,
    Action.SESSION_RADIOARCHIWISTA,
    Action.SESSION_LIST,
    Action.SESSION_ENABLE,
    Action.SESSION_DISABLE_CURRENT,
    Action.SESSION_CONFIGURE_CURRENT,
    Action.PREVIOUS_SESSION,
    Action.NEXT_SESSION,
    Action.COMMAND_PALETTE,
    Action.SESSION_OPTIONS,
    Action.ACTIVATE,
    Action.PARENT_FOLDER,
    Action.CLOSE_LIST_PREVIEW,
    Action.SHOW_PLAYER,
    Action.SHOW_LIST,
    Action.PLAY_PAUSE,
    Action.TOGGLE_MUTE,
    Action.QUEUE_NEXT,
    Action.QUEUE_PREVIOUS,
    Action.VOLUME_UP_5,
    Action.VOLUME_DOWN_5,
    Action.VOLUME_UP_1,
    Action.VOLUME_DOWN_1,
    Action.TIME_ELAPSED,
    Action.TIME_REMAINING,
    Action.TIME_TOTAL,
    Action.VIEW_PRESETS,
    *(Action[f"PRESET_{slot}"] for slot in range(1, 13)),
    Action.FRONTIER_SMART_SELECT_MODE,
    Action.GENERAL_SETTINGS,
    Action.EXPORT_BACKUP,
    Action.IMPORT_BACKUP,
    Action.TOGGLE_SEEK_MESSAGES,
    Action.QUICK_INFORMATION,
    Action.ITEM_PROPERTIES,
    Action.COPY_NAME,
    Action.FOCUS_FILTER,
    Action.VIEW_HISTORY,
    Action.VIEW_ACTIVE_RECORDINGS,
    Action.VIEW_RECORDED_RADIO_FILES,
    Action.VIEW_DOWNLOADED_FILES,
    Action.REMOVE_SELECTED,
    Action.VIEW_YOUTUBE_LIVE_AGGREGATE,
    Action.HELP,
    Action.KEYBOARD_HELP,
    Action.UPDATE_MEDIA_COMPONENTS,
    Action.REPORT_PROBLEM,
    Action.VERSION_INFO,
})


# Radio Archiwista korzysta z lokalnego odtwarzacza, więc przewijanie i
# prędkość są dostępne; tryb urządzenia i presety nie mają tu znaczenia.
RADIOARCHIWISTA_SUPPORTED_ACTIONS = frozenset(
    (FRONTIER_SMART_SUPPORTED_ACTIONS - {
        Action.VIEW_PRESETS,
        Action.FRONTIER_SMART_SELECT_MODE,
        *(Action[f"PRESET_{slot}"] for slot in range(1, 13)),
    })
    | {
        Action.VIEW_FAVORITES,
        Action.VIEW_LIBRARY,
        Action.VIEW_SAVED_QUEUE,
        Action.ADD_TO_QUEUE,
        Action.TOGGLE_PLAY_NEXT,
        Action.COPY_ADDRESS,
        Action.TOGGLE_RADIOARCHIWISTA_FAVORITE,
        Action.RADIOARCHIWISTA_DOWNLOAD,
        Action.RADIOARCHIWISTA_REFRESH,
        Action.SEEK_BACK_10, Action.SEEK_FORWARD_10,
        Action.SEEK_BACK_30, Action.SEEK_FORWARD_30,
        Action.SEEK_BACK_60, Action.SEEK_FORWARD_60,
        Action.SEEK_BACK_CUSTOM, Action.SEEK_FORWARD_CUSTOM,
        Action.RATE_UP, Action.RATE_DOWN, Action.RATE_RESET,
        Action.TRACK_START, Action.TRACK_END,
        *(Action[f"SEEK_PERCENT_{percent}"] for percent in range(0, 100, 10)),
        Action.ADD_BOOKMARK, Action.ADD_NAMED_BOOKMARK,
        Action.PREVIOUS_BOOKMARK, Action.NEXT_BOOKMARK,
        Action.PREVIOUS_NAMED_BOOKMARK, Action.NEXT_NAMED_BOOKMARK,
        Action.ADD_NAMED_CHAPTER, Action.PREVIOUS_CHAPTER,
        Action.NEXT_CHAPTER, Action.VIEW_CHAPTERS,
    }
)


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


@dataclass(frozen=True, slots=True)
class ShortcutDefinition:
    """Jedna edytowalna pozycja karty Klawiatura.

    Model zachowuje ``Action`` jako identyfikator wewnętrzny, ale kontrolki
    otrzymują wyłącznie ``name`` i jawnie sformatowany skrót.
    """

    action: Action
    name: str
    default_shortcut: str


# Skroty dzialajace W WIDOKU LISTY. Strzalki NIE sa tu wpisane - naleza do
# natywnego ListCtrl, zeby czytnik ekranu i Narrator dzialaly bez dodatku.
LIST_VIEW: dict[str, Action] = {
    "Ctrl+Shift+K": Action.COMMAND_PALETTE,
    "Ctrl+Shift+S": Action.SESSION_LIST,
    "Ctrl+Prior": Action.PREVIOUS_SESSION,
    "Ctrl+Next": Action.NEXT_SESSION,
    "Ctrl+1": Action.SESSION_FILES,
    "Ctrl+2": Action.SESSION_RADIO,
    "Ctrl+3": Action.SESSION_PODCASTS,
    "Ctrl+4": Action.SESSION_TIDAL,
    "Ctrl+5": Action.SESSION_WIIM,
    "Ctrl+6": Action.SESSION_SONOS,
    "Ctrl+7": Action.SESSION_SPOTIFY,
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
    # Escape i Backspace maja rozne role. Escape czysci filtr albo zamyka
    # rzeczywisty podglad/podwidok; nie przechodzi po drzewie folderow.
    # Backspace powyzej pozostaje jedynym gestem "folder nadrzedny".
    "Escape": Action.CLOSE_LIST_PREVIEW,
    "F6": Action.SHOW_PLAYER,
    # Shift+F6 na LISCIE tez idzie do odtwarzacza: warunek oryginalu to
    # ``_playerViewActive && Shift`` (MainWindow.xaml.cs:20780), a na liscie
    # pierwszy czlon jest falszem, wiec wykonuje sie galaz ShowPlayerView.
    "Shift+F6": Action.SHOW_PLAYER,
    "Space": Action.PLAY_PAUSE,
    "Ctrl+M": Action.TOGGLE_MUTE,
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
    # Ctrl+X/Ctrl+V najpierw próbują wewnętrznego przenoszenia w otwartej
    # playliście; poza nią Ctrl+X zachowuje dotychczasowe wycięcie pliku.
    "Ctrl+X": Action.CUT_FILE,
    "Ctrl+V": Action.PASTE_OR_MOVE,
    "Alt+Up": Action.MOVE_LIST_ITEM_UP,
    "Alt+Down": Action.MOVE_LIST_ITEM_DOWN,
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
    "Ctrl+Q": Action.VIEW_SAVED_QUEUE,
    # Jednoznaczny zestaw bez dawnego duplikatu Shift+Enter:
    # Ctrl+Shift+Q zmienia zwykla kolejke, Ctrl+Shift+Enter ustawia blok
    # „odtworz nastepne”. Sam Shift+Enter zostaje wolny na przyszla funkcje.
    "Ctrl+Shift+Q": Action.ADD_TO_QUEUE,
    "Ctrl+Shift+Return": Action.TOGGLE_PLAY_NEXT,
    "Ctrl+Shift+U": Action.TOGGLE_FAVORITE,
    "Ctrl+Shift+P": Action.MANAGE_PLAYLIST_MEMBERSHIP,
    # Ctrl+B to ViewBookmarks oryginalu (MainWindow.xaml:489): WSZYSTKIE
    # zakladki. Teraz mamy ten zbior naprawde (GetForDisplay), wiec skrot
    # dostaje swoje wlasne znaczenie, a wezszy widok zakladek ZAZNACZONEGO
    # pliku zostaje pod Ctrl+Shift+B. Dwa zakresy, dwa gesty -- zaden nie
    # podmienia drugiemu zbioru pod reka.
    "Ctrl+B": Action.VIEW_ALL_BOOKMARKS,
    # Filtr listy. Gest ODCZYTANY, nie zgadniety: MainWindow.xaml:491
    # ``<MenuItem Header="_Filtruj listę" InputGestureText="Ctrl+K" ...>``.
    # Osobne okno wyszukiwania, nie filtr bieżącej listy. Gesty wprost z
    # MainWindow.xaml:501-502 i obu tablic skrótów MainWindow.xaml.cs.
    "Ctrl+K": Action.FOCUS_FILTER,
    "Ctrl+F": Action.SEARCH_CURRENT,
    "Ctrl+Shift+F": Action.SEARCH_ALL,
    # LEWA STRZALKA bez modyfikatora. Jedyny wpis ze strzalka w tablicy listy:
    # oryginal wymaga ``Keyboard.Modifiers == ModifierKeys.None``
    # (cs:23206), wiec Shift/Ctrl/Alt+Left NIE sa tu wpisane i zostaja
    # kontrolce. Strzalki PIONOWE nadal naleza wylacznie do listy.
    # Alt+T jest wspólnym, łatwym do odnalezienia gestem informacji o
    # elemencie. Lewa strzałka pozostaje zgodnościowym skrótem z pełnego AMC.
    "Alt+T": Action.QUICK_INFORMATION,
    "Left": Action.QUICK_INFORMATION,
    "Alt+Return": Action.ITEM_PROPERTIES,
    # Ustawienia sesji: TEN SAM gest co w odtwarzaczu, bo zakres jest ten
    # sam niezaleznie od widoku.
    "Ctrl+Alt+Return": Action.SESSION_OPTIONS,
    # Ustawienia ogolne: gest z pelnego AMC (MainWindow.xaml:162 oraz
    # MainWindow.xaml.cs:21126). Przecinek jest zwyklym kodem ASCII, wiec
    # ``chord_from_event`` sklada z niego dokladnie "Ctrl+,".
    "Ctrl+,": Action.GENERAL_SETTINGS,
    # Pelne AMC: Shift+A wybiera wyjscie audio osobno dla biezacej sesji.
    "Shift+A": Action.SELECT_AUDIO_OUTPUT,
    # Szybka zmiana podczas odtwarzania, bez otwierania dialogu. Nawiasy sa
    # para kierunkowa i nie koliduja z istniejacymi skrótami transportu.
    "Ctrl+[": Action.PREVIOUS_AUDIO_OUTPUT,
    "Ctrl+]": Action.NEXT_AUDIO_OUTPUT,
    # Zarzadzanie presetami z pelnego AMC (MainWindow.xaml:428-435).
    "Ctrl+Alt+P": Action.VIEW_PRESETS,
    "Ctrl+Alt+Shift+P": Action.ASSIGN_PRESET,
    "Alt+Left": Action.VIEW_BACK,
    "Alt+Right": Action.VIEW_FORWARD,
    "F1": Action.HELP,
    "Ctrl+F1": Action.KEYBOARD_HELP,
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
    "Ctrl+F5": Action.REFRESH_RADIO_STATIONS,
    "Ctrl+N": Action.STATION_ADD,
    "F2": Action.STATION_EDIT,
    "Delete": Action.STATION_DELETE,
    # W pelnym AMC import jest kontekstowym Ctrl+O w sesji Radia. Ctrl+I
    # nalezy globalnie do widoku nowych odcinkow i materialow.
    "Ctrl+O": Action.STATION_IMPORT,
    "Ctrl+E": Action.STATION_EXPORT_FAVORITES,
    # Skroty nagrywania z pelnego AMC (MainWindow.xaml.cs:21396-21446).
    # Ctrl+R dziala na liscie i w odtwarzaczu; Shift+Spacja steruje pauza.
    "Ctrl+R": Action.RECORD_TOGGLE,
    "Shift+Space": Action.RECORD_PAUSE,
    "Shift+R": Action.ADD_RADIO_SCHEDULE,
    "Alt+D": Action.CURRENT_RADIO_BROADCAST_INFORMATION,
    "Ctrl+Alt+S": Action.VIEW_RADIO_RECOGNITION_HISTORY,
    "Ctrl+Alt+Shift+S": Action.TOGGLE_RADIO_RECOGNITION_ANNOUNCEMENTS,
    "Ctrl+Shift+L": Action.TOGGLE_RADIO_LIBRARY,
}

# Alt+1/2/3 dotyczy tylko dwóch równorzędnych kolekcji Radia. W Historii,
# Playlistach i innych podwidokach nie wolno dopuścić do odziedziczenia
# lokalnego znaczenia „Foldery/Wszystkie pliki”.
RADIO_COLLECTION_LIST_VIEW: dict[str, Action] = {
    "Alt+1": Action.SORT_RADIO_COLLECTION_ADDED,
    "Alt+2": Action.SORT_RADIO_COLLECTION_ALPHABETICAL,
    "Alt+3": Action.SORT_RADIO_COLLECTION_CUSTOM,
    "Alt+Up": Action.MOVE_LIST_ITEM_UP,
    "Alt+Down": Action.MOVE_LIST_ITEM_DOWN,
    "Ctrl+X": Action.CUT_FILE,
    "Ctrl+V": Action.PASTE_OR_MOVE,
}

# Litery bez modyfikatora sa bezpieczne tylko w odtwarzaczu Radia. Na liscie
# T i R musza pozostac natywnej kontroli (wyszukiwanie przyrostowe).
RADIO_PLAYER_VIEW: dict[str, Action] = {
    "Ctrl+F5": Action.REFRESH_RADIO_STATIONS,
    "Ctrl+O": Action.STATION_IMPORT,
    "Ctrl+E": Action.STATION_EXPORT_FAVORITES,
    "R": Action.RECORD_TOGGLE,
    "Ctrl+R": Action.RECORD_TOGGLE,
    "Shift+Space": Action.RECORD_PAUSE,
    "Shift+R": Action.ADD_RADIO_SCHEDULE,
    "T": Action.RECORD_SPLIT,
    "B": Action.ADD_BOOKMARK,
    "Ctrl+Shift+L": Action.TOGGLE_RADIO_LIBRARY,
    "Ctrl+Shift+B": Action.ADD_NAMED_BOOKMARK,
    "Alt+D": Action.CURRENT_RADIO_BROADCAST_INFORMATION,
    "S": Action.RECOGNIZE_RADIO_TRACK,
    "Shift+S": Action.TOGGLE_RADIO_RECOGNITION_MONITORING,
    "Ctrl+Alt+S": Action.VIEW_RADIO_RECOGNITION_HISTORY,
    "Ctrl+Alt+Shift+S": Action.TOGGLE_RADIO_RECOGNITION_ANNOUNCEMENTS,
    # W Time Shifcie End nie oznacza końca pliku: wraca do transmisji na żywo.
    "End": Action.RADIO_JUMP_LIVE,
}

# Skroty W WIDOKU ODTWARZACZA. Tu strzalki sa wolne, wiec przejmuja role
# z profilu globalnego AMC (przewijanie i glosnosc).
PLAYER_VIEW: dict[str, Action] = {
    "Ctrl+Shift+K": Action.COMMAND_PALETTE,
    "Ctrl+Shift+S": Action.SESSION_LIST,
    "Ctrl+Prior": Action.PREVIOUS_SESSION,
    "Ctrl+Next": Action.NEXT_SESSION,
    "Ctrl+1": Action.SESSION_FILES,
    "Ctrl+2": Action.SESSION_RADIO,
    "Ctrl+3": Action.SESSION_PODCASTS,
    "Ctrl+4": Action.SESSION_TIDAL,
    "Ctrl+5": Action.SESSION_WIIM,
    "Ctrl+6": Action.SESSION_SONOS,
    "Ctrl+7": Action.SESSION_SPOTIFY,
    "Escape": Action.SHOW_LIST,
    "Shift+F6": Action.SHOW_LIST,
    "F6": Action.SHOW_LIST,
    "Space": Action.PLAY_PAUSE,
    # Losowanie jest kontekstowe: Radio rozstrzyga R wcześniej jako
    # nagrywanie, a obce odtwarzacze sieciowe jawnie go nie przejmuja.
    "R": Action.TOGGLE_SHUFFLE,
    "Ctrl+M": Action.TOGGLE_MUTE,
    # Skróty istnieją także w tablicy odtwarzacza pełnego AMC, lecz sama
    # komenda odpowiada wtedy jasno: wyszukiwanie jest dostępne na listach.
    "Ctrl+F": Action.SEARCH_CURRENT,
    "Ctrl+Shift+F": Action.SEARCH_ALL,
    # Pelne AMC: gola litera B w otwartym odtwarzaczu dodaje szybka zakladke.
    # Ctrl+B pozostaje zbiorcza lista zakladek i nie jest aliasem tej akcji.
    "B": Action.ADD_BOOKMARK,
    # Na LISCIE Ctrl+Shift+B pozostaje widokiem zakladek zaznaczonego pliku.
    # W ODTWARZACZU ten sam gest ma znaczenie z glownego AMC: dialog nazwy.
    "Ctrl+Shift+B": Action.ADD_NAMED_BOOKMARK,
    # Nawigacja po zakładkach bieżącego materiału. Gesty wprost z głównego
    # AMC (MainWindow.xaml.cs:21646-21647 i 22457-22458). Nie mylić z
    # Alt+Page Up/Down, które poruszają granicami zaznaczonego fragmentu.
    "Shift+Prior": Action.PREVIOUS_BOOKMARK,
    "Shift+Next": Action.NEXT_BOOKMARK,
    # Zakładki nazwane przez Ctrl+Shift+B pozostają zakładkami, a nie
    # rozdziałami dostawcy. Mają własną nawigację niezależną od spisu treści.
    "Ctrl+Shift+Prior": Action.PREVIOUS_NAMED_BOOKMARK,
    "Ctrl+Shift+Next": Action.NEXT_NAMED_BOOKMARK,
    # Rozdziały pochodzą od dostawcy albo użytkownika i są osobnym zbiorem.
    "Ctrl+Shift+Left": Action.PREVIOUS_CHAPTER,
    "Ctrl+Shift+Right": Action.NEXT_CHAPTER,
    "Ctrl+Alt+Prior": Action.PREVIOUS_CHAPTER,
    "Ctrl+Alt+Next": Action.NEXT_CHAPTER,
    "Ctrl+Alt+B": Action.VIEW_CHAPTERS,
    "Ctrl+Alt+Shift+B": Action.ADD_NAMED_CHAPTER,
    # Historia odtwarzanych elementów działa tylko w odtwarzaczu. Na listach
    # Alt+góra/dół zachowuje ręczne przenoszenie elementu.
    "Alt+Down": Action.PLAYBACK_HISTORY_OLDER,
    "Alt+Up": Action.PLAYBACK_HISTORY_NEWER,
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
    "Ctrl+Shift+Q": Action.ADD_TO_QUEUE,
    "Ctrl+Shift+Return": Action.TOGGLE_PLAY_NEXT,
    "Ctrl+Shift+U": Action.TOGGLE_FAVORITE,
    "Ctrl+Shift+P": Action.MANAGE_PLAYLIST_MEMBERSHIP,
    "Alt+T": Action.QUICK_INFORMATION,
    "Alt+Return": Action.ITEM_PROPERTIES,
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
    # Ctrl+Up/Down zostaly usuniete, bo oryginal ich nie ma. Ctrl+0 także nie
    # jest używane; listę sesji otwiera Ctrl+Shift+S.
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
    # Ustawienia sesji dzialaja takze z odtwarzacza: zakres to SESJA, nie widok.
    "Ctrl+Alt+Return": Action.SESSION_OPTIONS,
    "Ctrl+,": Action.GENERAL_SETTINGS,
    "Shift+A": Action.SELECT_AUDIO_OUTPUT,
    "Ctrl+[": Action.PREVIOUS_AUDIO_OUTPUT,
    "Ctrl+]": Action.NEXT_AUDIO_OUTPUT,
    "Ctrl+Alt+P": Action.VIEW_PRESETS,
    "Ctrl+Alt+Shift+P": Action.ASSIGN_PRESET,
    "Alt+Left": Action.VIEW_BACK,
    "Alt+Right": Action.VIEW_FORWARD,
    "F1": Action.HELP,
    "Ctrl+F1": Action.KEYBOARD_HELP,
    # Globalne zatrzymanie dziala niezaleznie od aktualnej sesji, jak w WPF.
    "Ctrl+Alt+Shift+R": Action.RECORD_STOP_ALL,
}

for _chord, _slot in PRESET_SHORTCUTS.items():
    PLAYER_VIEW[_chord] = PRESET_ACTIONS[_slot]

# To samo globalne polecenie z widoku listy.
LIST_VIEW["Ctrl+Alt+Shift+R"] = Action.RECORD_STOP_ALL
# Widoki rozstrzygane PRZED tabelami adapterow. Natywna lista i odtwarzacz
# korzystaja z tego samego kontraktu, wiec WiiM, Sonos ani przyszly adapter nie
# moga polknac gestu bez dzialania.
GLOBAL_VIEW_SHORTCUTS: dict[str, Action] = {
    "Alt+R": Action.VIEW_ACTIVE_RECORDINGS,
    "Alt+Shift+R": Action.VIEW_RECORDED_RADIO_FILES,
    "Ctrl+J": Action.VIEW_DOWNLOADED_FILES,
    "Ctrl+I": Action.VIEW_PODCAST_INBOX,
    "Ctrl+Shift+I": Action.VIEW_YOUTUBE_LIVE_AGGREGATE,
    "Ctrl+H": Action.VIEW_HISTORY,
    "Ctrl+L": Action.VIEW_LIBRARY,
}
for _table in (LIST_VIEW, PLAYER_VIEW):
    _table.update(GLOBAL_VIEW_SHORTCUTS)
    _table["Ctrl+Shift+H"] = Action.MANAGE_RADIO_SCHEDULES


PODCAST_INBOX_LIST_VIEW: dict[str, Action] = {
    "Alt+1": Action.SORT_PODCAST_INBOX_ADDED,
    "Alt+2": Action.SORT_PODCAST_INBOX_ALPHABETICAL,
    "Alt+3": Action.SORT_PODCAST_INBOX_BY_PODCAST,
    "F5": Action.REFRESH_PODCAST_LIBRARY,
}


FILES_SESSION_VIEW: dict[str, Action] = {
    "F5": Action.REFRESH_LOCAL_LIBRARY,
    "Ctrl+F5": Action.MANAGE_LIBRARY_FOLDERS,
    "Ctrl+Alt+S": Action.VIEW_RADIO_RECOGNITION_HISTORY,
}

# Główna Biblioteka źródeł ma ten sam układ cyfr, ale osobny model i osobne
# akcje. Dzięki temu polecenia skrzynki odcinków nie mogą przez pomyłkę zapisać
# trybu podcastów, kanałów YouTube i kolekcji Media internetowe.
PODCAST_LIBRARY_LIST_VIEW: dict[str, Action] = {
    "Alt+1": Action.SORT_PODCAST_LIBRARY_ADDED,
    "Alt+2": Action.SORT_PODCAST_LIBRARY_ALPHABETICAL,
    "Alt+3": Action.SORT_PODCAST_LIBRARY_CUSTOM,
}

YOUTUBE_BROADCASTS_LIST_VIEW: dict[str, Action] = {
    "F5": Action.REFRESH_YOUTUBE_BROADCASTS,
}

YOUTUBE_BROADCASTS_AGGREGATE_LIST_VIEW: dict[str, Action] = {
    "F5": Action.REFRESH_YOUTUBE_LIVE_AGGREGATE,
    "Ctrl+R": Action.RECORD_YOUTUBE_LIVE_NOW,
    "Shift+R": Action.SCHEDULE_YOUTUBE_LIVE,
}

# Polecenia dotyczą zaznaczonego kanału YouTube, nie całej sesji Podcastów.
# Osobna tabela zapobiega przejęciu Ctrl+T/Ctrl+Y na odcinku, playliście,
# kanale RSS albo w odtwarzaczu. Ctrl+T było wcześniej słusznie usunięte z
# globalnej tabeli transportu; tutaj otrzymuje nowe, jawnie kontekstowe
# znaczenie zaakceptowane przez użytkownika 24.10.2026.
YOUTUBE_CHANNEL_LIST_VIEW: dict[str, Action] = {
    "Ctrl+T": Action.YOUTUBE_BROADCASTS,
    "Ctrl+Y": Action.ADD_YOUTUBE_LIVE_STATION,
}

# Kontekst sesji, niezaleznie od listy/odtwarzacza. F5 w skrzynce jest
# przesloniete powyzej, dokladnie jak w glownym AMC.
PODCAST_SESSION_VIEW: dict[str, Action] = {
    # Ctrl+U jest widokiem bieżącej sesji także z odtwarzacza. W Podcastach
    # obejmuje ulubione kanały i odcinki, a nie Bibliotekę plików lokalnych.
    "Ctrl+U": Action.VIEW_FAVORITES,
    "Ctrl+N": Action.ADD_PODCAST_SOURCE,
    "Ctrl+O": Action.IMPORT_PODCAST_OPML,
    "F5": Action.REFRESH_PODCAST,
    "Ctrl+F5": Action.REFRESH_PODCAST_LIBRARY,
    # Jak w głównym AMC: na liście pobiera całe zaznaczenie, a w
    # odtwarzaczu bieżący odcinek. Ma pierwszeństwo przed plikowym dopisywaniem
    # fragmentu, które pod Ctrl+D pozostaje w sesji Plików lokalnych.
    "Ctrl+D": Action.DOWNLOAD_PODCAST_EPISODES,
    "Ctrl+Shift+D": Action.DOWNLOAD_YOUTUBE_VIDEOS,
    "Ctrl+S": Action.SAVE_PODCAST_EPISODE_AS,
    # Pełny AMC udostępnia pełny opis podcastu lub odcinka pod Alt+D zarówno
    # z listy, jak i z odtwarzacza. Sam opis pozostaje zwykłym tekstem tylko
    # do odczytu; skrót nie otwiera strony internetowej.
    "Alt+D": Action.SHOW_PODCAST_DESCRIPTION,
    # Opcje pojedynczego podcastu lub odcinka. Gest wprost z głównego AMC
    # (CommandIds.ItemPlaybackOptions): działa z listy i odtwarzacza.
    "Alt+Shift+Return": Action.PODCAST_PLAYBACK_OPTIONS,
    # Pelny AMC zmienia wspolnie cale zaznaczenie: gdy wszystkie elementy sa
    # ulubione, usuwa stan; w przeciwnym razie dodaje go wszystkim.
    "Ctrl+Shift+U": Action.TOGGLE_PODCAST_FAVORITE,
    # Odcinek nie ma osobnego członkostwa w Bibliotece: skrót zmienia jego
    # podcast nadrzędny, zgodnie z pełnym AMC.
    "Ctrl+Shift+L": Action.TOGGLE_PODCAST_LIBRARY,
}


TIDAL_SESSION_VIEW: dict[str, Action] = {
    "Ctrl+F5": Action.SESSION_CONFIGURE_CURRENT,
    "Ctrl+Shift+L": Action.TIDAL_TOGGLE_LIBRARY,
    "Ctrl+Shift+U": Action.TIDAL_TOGGLE_FAVORITE,
}


TIDAL_LIST_VIEW: dict[str, Action] = {
    # MainWindow.xaml.cs:21808-21810: kontekstowe menu relacji zaznaczonego
    # elementu. Pionowe strzalki nadal pozostaja natywnej liscie.
    "Right": Action.TIDAL_RELATIONS,
}


SPOTIFY_LIST_VIEW: dict[str, Action] = {
    "Right": Action.SPOTIFY_RELATIONS,
}


# Bezpośrednie wejścia do sześciu kategorii Spotify. Cztery gesty zachowują
# nazewnictwo starego AMC (Ulubione, Albumy, Playlisty i Podcasty), a dwie
# wolne pary uzupełniają nowy interfejs o Wykonawców i zapisane odcinki.
# Są sesyjne, więc działają również z odtwarzacza Spotify i nie odbierają
# tych samych klawiszy innym usługom.
SPOTIFY_SESSION_VIEW: dict[str, Action] = {
    "Ctrl+F5": Action.SESSION_CONFIGURE_CURRENT,
    "Ctrl+U": Action.SPOTIFY_VIEW_LIKED,
    "Ctrl+Shift+A": Action.SPOTIFY_VIEW_ALBUMS,
    "Ctrl+Shift+W": Action.SPOTIFY_VIEW_ARTISTS,
    "Ctrl+P": Action.SPOTIFY_VIEW_PLAYLISTS,
    "Ctrl+Alt+O": Action.SPOTIFY_VIEW_PODCASTS,
    "Ctrl+Alt+E": Action.SPOTIFY_VIEW_EPISODES,
    # Tak jak w starym AMC: opis podcastu lub odcinka pochodzi z katalogu
    # Spotify, a nie z lokalnej bazy RSS/YouTube.
    "Alt+D": Action.SHOW_PODCAST_DESCRIPTION,
    "Ctrl+Shift+L": Action.SPOTIFY_TOGGLE_LIBRARY,
    "Ctrl+Shift+U": Action.SPOTIFY_TOGGLE_FAVORITE,
}


SPOTIFY_COLLECTION_LIST_VIEW: dict[str, Action] = {
    "Alt+1": Action.SORT_SPOTIFY_COLLECTION_ADDED,
    "Alt+2": Action.SORT_SPOTIFY_COLLECTION_ALPHABETICAL,
    "Alt+3": Action.SORT_SPOTIFY_COLLECTION_CATALOG,
}


TIDAL_COLLECTION_LIST_VIEW: dict[str, Action] = {
    "Alt+1": Action.SORT_TIDAL_COLLECTION_ADDED,
    "Alt+2": Action.SORT_TIDAL_COLLECTION_ALPHABETICAL,
    "Alt+3": Action.SORT_TIDAL_COLLECTION_CUSTOM,
}


# W sesji WiiM Ctrl+L prowadzi do uczciwej Biblioteki źródeł, a Ctrl+O otwiera
# plik playlisty i zapisuje znalezione strumienie w AMC. Zapisane strumienie
# nie potrzebują już osobnego skrótu: są drugim elementem Biblioteki źródeł.
WIIM_SESSION_VIEW: dict[str, Action] = {
    # Jak w historycznym AMC: Ctrl+F5 prowadzi do urządzeń, a zwykłe F5
    # odświeża bieżący widok bez otwierania konfiguracji.
    "Ctrl+F5": Action.SESSION_CONFIGURE_CURRENT,
    "Ctrl+O": Action.IMPORT_WIIM_NETWORK_STREAMS,
    # Zachowany jako zgodny alias starszych kompilacji; menu pokazuje prostsze
    # Ctrl+O i tylko ten skrót opisujemy użytkownikowi.
    "Ctrl+Shift+O": Action.IMPORT_WIIM_NETWORK_STREAMS,
    "Ctrl+N": Action.ADD_WIIM_NETWORK_STREAM,
    "Ctrl+E": Action.EXPORT_WIIM_NETWORK_STREAMS,
    "F2": Action.EDIT_WIIM_NETWORK_STREAM,
    "Delete": Action.REMOVE_WIIM_NETWORK_STREAMS,
    "Alt+1": Action.SORT_WIIM_NETWORK_STREAMS_ADDED,
    "Alt+2": Action.SORT_WIIM_NETWORK_STREAMS_ALPHABETICAL,
    "Alt+3": Action.SORT_WIIM_NETWORK_STREAMS_CUSTOM,
    "Alt+Up": Action.MOVE_WIIM_NETWORK_STREAM_UP,
    "Alt+Down": Action.MOVE_WIIM_NETWORK_STREAM_DOWN,
    "Ctrl+X": Action.CUT_WIIM_NETWORK_STREAMS,
    "Ctrl+V": Action.PASTE_WIIM_NETWORK_STREAMS,
}


FRONTIER_SMART_SESSION_VIEW: dict[str, Action] = {
    "Ctrl+F5": Action.SESSION_CONFIGURE_CURRENT,
    "Ctrl+Alt+M": Action.FRONTIER_SMART_SELECT_MODE,
}


# Sesja Radio Archiwista: F5 odświeża katalog, Ctrl+F5 ustawia jego adres.
# Skróty materiału są zgodne z Podcastami.
RADIOARCHIWISTA_SESSION_VIEW: dict[str, Action] = {
    "F5": Action.RADIOARCHIWISTA_REFRESH,
    "Ctrl+F5": Action.SESSION_CONFIGURE_CURRENT,
    "Ctrl+U": Action.VIEW_FAVORITES,
    "Ctrl+Shift+U": Action.TOGGLE_RADIOARCHIWISTA_FAVORITE,
    "Ctrl+Shift+C": Action.COPY_ADDRESS,
    "Ctrl+Q": Action.VIEW_SAVED_QUEUE,
    "Ctrl+Shift+Q": Action.ADD_TO_QUEUE,
    "Ctrl+Shift+Return": Action.TOGGLE_PLAY_NEXT,
    "Ctrl+D": Action.RADIOARCHIWISTA_DOWNLOAD,
}


# Klawisze odtwarzacza WiiM z pełnego AMC. Muszą mieć pierwszeństwo przed
# lokalnym zaznaczaniem fragmentu (I/O) i zwykłymi poleceniami plików.
WIIM_PLAYER_VIEW: dict[str, Action] = {
    # MainWindow.xaml.cs:21617-21618 i 22326-22327. To nie sa zwykle
    # poprzedni/nastepny utwor: pelne AMC przechodzi po zapisanych
    # strumieniach AMC albo zajetych, grywalnych presetach urzadzenia.
    "Alt+Prior": Action.WIIM_PREVIOUS_SOURCE,
    "Alt+Next": Action.WIIM_NEXT_SOURCE,
    "I": Action.WIIM_SELECT_INPUT,
    "O": Action.WIIM_SELECT_OUTPUT,
    "E": Action.WIIM_SELECT_EQUALIZER,
    "R": Action.WIIM_SELECT_REPEAT,
    "S": Action.WIIM_TOGGLE_SHUFFLE,
    "T": Action.WIIM_SET_SLEEP_TIMER,
}


SONOS_SESSION_VIEW: dict[str, Action] = {
    # Jak w głównym AMC: grupy i głośniki są celem sterowania, nie Biblioteką.
    "Ctrl+F5": Action.SELECT_SONOS_TARGET,
    "Ctrl+U": Action.VIEW_FAVORITES,
    "Ctrl+P": Action.VIEW_PLAYLISTS,
}


SONOS_OWN_STREAMS_VIEW: dict[str, Action] = {
    "Ctrl+N": Action.ADD_SONOS_OWN_STREAM,
    "F2": Action.EDIT_SONOS_OWN_STREAM,
    "Delete": Action.REMOVE_SONOS_OWN_STREAMS,
    # Tak samo jak w oryginalnym oknie Moje stacje Sonosa.
    "Ctrl+O": Action.IMPORT_SONOS_OWN_STREAMS,
    "Alt+1": Action.SORT_SONOS_OWN_STREAMS_ADDED,
    "Alt+2": Action.SORT_SONOS_OWN_STREAMS_ALPHABETICAL,
    "Alt+3": Action.SORT_SONOS_OWN_STREAMS_CUSTOM,
    "Alt+Up": Action.MOVE_SONOS_OWN_STREAM_UP,
    "Alt+Down": Action.MOVE_SONOS_OWN_STREAM_DOWN,
    "Ctrl+X": Action.CUT_SONOS_OWN_STREAMS,
    "Ctrl+V": Action.PASTE_SONOS_OWN_STREAMS,
    "Ctrl+E": Action.EXPORT_SONOS_OWN_STREAMS,
}


_SHORTCUT_TABLES: tuple[dict[str, Action], ...] = (
    FILES_SESSION_VIEW,
    PODCAST_LIBRARY_LIST_VIEW,
    PODCAST_INBOX_LIST_VIEW,
    YOUTUBE_CHANNEL_LIST_VIEW,
    YOUTUBE_BROADCASTS_LIST_VIEW,
    YOUTUBE_BROADCASTS_AGGREGATE_LIST_VIEW,
    PODCAST_SESSION_VIEW,
    TIDAL_SESSION_VIEW,
    TIDAL_COLLECTION_LIST_VIEW,
    TIDAL_LIST_VIEW,
    SPOTIFY_SESSION_VIEW,
    SPOTIFY_COLLECTION_LIST_VIEW,
    SPOTIFY_LIST_VIEW,
    WIIM_SESSION_VIEW,
    WIIM_PLAYER_VIEW,
    FRONTIER_SMART_SESSION_VIEW,
    RADIOARCHIWISTA_SESSION_VIEW,
    SONOS_SESSION_VIEW,
    SONOS_OWN_STREAMS_VIEW,
    LIST_VIEW,
    RADIO_COLLECTION_LIST_VIEW,
    RADIO_LIST_VIEW,
    RADIO_PLAYER_VIEW,
    PLAYER_VIEW,
)

# Te gesty są częścią drogi odzyskania sterowania albo zależą od specjalnej
# obsługi natywnej. Pozostają stałe, żeby błędna konfiguracja nie odcięła
# użytkownika od Pomocy klawiatury, listy, sesji lub nagrywania.
_PROTECTED_SHORTCUT_ACTIONS = frozenset({
    Action.ACTIVATE,
    Action.PARENT_FOLDER,
    Action.CLOSE_LIST_PREVIEW,
    Action.SHOW_PLAYER,
    Action.SHOW_LIST,
    Action.QUEUE_NEXT,
    Action.QUEUE_PREVIOUS,
    Action.TIDAL_RELATIONS,
    Action.HELP,
    Action.KEYBOARD_HELP,
    Action.RECORD_STOP_ALL,
    Action.SESSION_FILES,
    Action.SESSION_RADIO,
    Action.SESSION_PODCASTS,
    Action.SESSION_TIDAL,
    Action.SESSION_WIIM,
    Action.SESSION_SONOS,
    Action.SESSION_SPOTIFY,
    Action.SESSION_FRONTIER_SMART,
    Action.SESSION_RADIOARCHIWISTA,
    Action.SESSION_LIST,
    Action.PREVIOUS_SESSION,
    Action.NEXT_SESSION,
    Action.PRESET_1,
    Action.PRESET_2,
    Action.PRESET_3,
    Action.PRESET_4,
    Action.PRESET_5,
    Action.PRESET_6,
    Action.PRESET_7,
    Action.PRESET_8,
    Action.PRESET_9,
    Action.PRESET_10,
    Action.PRESET_11,
    Action.PRESET_12,
})

_DIRECT_RESERVED_SHORTCUTS: dict[str, str] = {
    "Alt+F4": "Zamknij program",
    "Alt+Escape": "Przełączanie okien Windows",
    "Alt+Tab": "Przełączanie okien Windows",
    "Ctrl+Escape": "Menu Start Windows",
    "Ctrl+Alt+Delete": "Ekran zabezpieczeń Windows",
    "Ctrl+Shift+Escape": "Menedżer zadań Windows",
    "Ctrl+Return": "Odtwórz kontener TIDAL",
    **{f"Ctrl+{slot}": "Przełączanie sesji" for slot in range(1, 10)},
}


def chord_from_canonical(value: object) -> Chord | None:
    """Odczytaj wyłącznie kanoniczny, wspierany zapis skrótu."""
    if not isinstance(value, str):
        return None
    text = value.strip()
    if not text:
        return None
    remaining = text
    ctrl = remaining.startswith("Ctrl+")
    if ctrl:
        remaining = remaining[5:]
    alt = remaining.startswith("Alt+")
    if alt:
        remaining = remaining[4:]
    shift = remaining.startswith("Shift+")
    if shift:
        remaining = remaining[6:]
    if not remaining or "+" in remaining and remaining != "+":
        return None
    chord = Chord(remaining, ctrl=ctrl, alt=alt, shift=shift)
    return chord if chord.canonical == text else None


def readable_shortcut(value: str | None) -> str:
    """Użytkowa etykieta skrótu do list, menu i pól edycyjnych."""
    if not value:
        return "Bez skrótu"
    chord = chord_from_canonical(value)
    if chord is None:
        return "Bez skrótu"
    text = chord.canonical
    for internal, spoken in {
        "Prior": "Page Up",
        "Next": "Page Down",
        "Return": "Enter",
        "Back": "Backspace",
        "Space": "Spacja",
        "Left": "strzałka w lewo",
        "Right": "strzałka w prawo",
        "Up": "strzałka w górę",
        "Down": "strzałka w dół",
    }.items():
        text = text.replace(internal, spoken)
    return text


def _default_chords_by_action() -> dict[Action, tuple[str, ...]]:
    collected: dict[Action, list[str]] = {}
    for table in _SHORTCUT_TABLES:
        for shortcut, action in table.items():
            values = collected.setdefault(action, [])
            if shortcut not in values:
                values.append(shortcut)
    return {action: tuple(values) for action, values in collected.items()}


def is_configurable_shortcut(action: Action) -> bool:
    return (
        action not in _PROTECTED_SHORTCUT_ACTIONS
        and action in _default_chords_by_action()
    )


def validate_custom_shortcut(value: str) -> str | None:
    """Zwróć komunikat użytkowy albo ``None`` dla bezpiecznej kombinacji."""
    chord = chord_from_canonical(value)
    if chord is None or chord.key.startswith("#"):
        return "Ten klawisz nie może być użyty jako skrót AMC."
    function_key = (
        chord.key.startswith("F")
        and chord.key[1:].isdigit()
        and 1 <= int(chord.key[1:]) <= 24
    )
    printable_key = len(chord.key) == 1 and 32 < ord(chord.key) < 127
    special_key = chord.key in {
        "Return", "Back", "Escape", "Space", "Delete", "Left", "Right",
        "Up", "Down", "Tab", "Home", "End", "Prior", "Next",
    }
    if not (printable_key or special_key or function_key):
        return "Ten klawisz nie może być użyty jako skrót AMC."
    if not (chord.ctrl or chord.alt or function_key):
        return (
            "Nowy skrót musi zawierać Ctrl lub Alt albo być klawiszem "
            "funkcyjnym od F1 do F24."
        )
    return None


def shortcut_conflict(
    action: Action,
    candidate: str,
    overrides: Mapping[Action, str] | None = None,
) -> tuple[Action | None, str] | None:
    """Wykryj konflikt z efektywnym skrótem innego polecenia."""
    if not candidate:
        return None
    pending = dict(overrides or {})
    defaults = _default_chords_by_action()
    for other, shortcuts in defaults.items():
        if other is action:
            continue
        effective = (
            (pending[other],)
            if other in pending and pending[other]
            else () if other in pending else shortcuts
        )
        if candidate in effective:
            return other, describe_action(other)
    reserved = _DIRECT_RESERVED_SHORTCUTS.get(candidate)
    if reserved is not None:
        return None, reserved
    return None


def normalize_shortcut_overrides(raw: object) -> dict[Action, str]:
    """Wczytaj prywatny stan bez przepuszczania obcych identyfikatorów."""
    if not isinstance(raw, Mapping):
        return {}
    candidates: dict[Action, str] = {}
    for raw_action, raw_shortcut in raw.items():
        if not isinstance(raw_shortcut, str):
            continue
        if isinstance(raw_action, Action):
            action = raw_action
        elif isinstance(raw_action, str):
            try:
                action = Action(raw_action)
            except ValueError:
                continue
        else:
            continue
        shortcut = raw_shortcut.strip()
        if not is_configurable_shortcut(action):
            continue
        if shortcut and validate_custom_shortcut(shortcut) is not None:
            continue
        candidates[action] = shortcut
    return {
        action: shortcut
        for action, shortcut in candidates.items()
        if shortcut_conflict(action, shortcut, candidates) is None
    }


def serialize_shortcut_overrides(
    overrides: Mapping[Action, str] | None,
) -> dict[str, str]:
    return {
        action.value: shortcut
        for action, shortcut in normalize_shortcut_overrides(overrides).items()
    }


def _resolve_default(
    chord: Chord,
    *,
    player_view: bool,
    radio_session: bool,
    radio_collection: bool = False,
    radio_playlist_contents: bool = False,
    radio_recording_context: bool = False,
    podcast_inbox: bool = False,
    podcast_library: bool = False,
    youtube_broadcasts: bool = False,
    youtube_broadcasts_aggregate: bool = False,
    youtube_channel: bool = False,
    youtube_account_available: bool = False,
    podcast_session: bool = False,
    youtube_video_selection: bool = False,
    files_session: bool = False,
    tidal_session: bool = False,
    tidal_collection: bool = False,
    spotify_session: bool = False,
    spotify_collection: bool = False,
    wiim_session: bool = False,
    wiim_streams: bool = False,
    frontier_smart_session: bool = False,
    radioarchiwista_session: bool = False,
    sonos_session: bool = False,
    sonos_own_streams: bool = False,
    service_history: bool = False,
) -> Action | None:
    """Znajdz akcje dla klawisza w DANYM widoku. Brak wpisu = klawisz zostaje
    dla kontrolki (natywna nawigacja ma pierwszenstwo)."""
    table = PLAYER_VIEW if player_view else LIST_VIEW
    canonical = chord.canonical
    if canonical in GLOBAL_VIEW_SHORTCUTS:
        action = GLOBAL_VIEW_SHORTCUTS[canonical]
        if (
            action is Action.VIEW_YOUTUBE_LIVE_AGGREGATE
            and not youtube_account_available
        ):
            return None
        return action
    if not player_view and service_history and canonical == "Delete":
        return Action.REMOVE_SELECTED
    if radio_session and canonical in RADIO_COLLECTION_LIST_VIEW:
        if not player_view and radio_collection:
            return RADIO_COLLECTION_LIST_VIEW[canonical]
        # Alt+1/2/3 belong only to the two radio collections.  The four
        # manual-order gestures are also the shared playlist gestures, so an
        # open radio playlist must be allowed to reach LIST_VIEW below.
        if not player_view and radio_playlist_contents and canonical in {
            "Alt+Up",
            "Alt+Down",
            "Ctrl+X",
            "Ctrl+V",
        }:
            pass
        else:
            return None
    # Alt+1/2/3 ma dwa jawne znaczenia w sesji Podcasty i YouTube: sortuje
    # główną Bibliotekę albo skrzynkę Nowych odcinków. W pozostałych widokach
    # tej sesji nie może wpaść w obce znaczenie lokalnych Folderów/Plików.
    if (
        not player_view
        and (podcast_session or podcast_library or podcast_inbox)
        and canonical in PODCAST_LIBRARY_LIST_VIEW
    ):
        if podcast_library:
            return PODCAST_LIBRARY_LIST_VIEW[canonical]
        if podcast_inbox:
            return PODCAST_INBOX_LIST_VIEW[canonical]
        return None
    if not player_view and podcast_inbox and canonical in PODCAST_INBOX_LIST_VIEW:
        # F5 w globalnej skrzynce odświeża wszystkie źródła. Alt+1/2/3 są już
        # rozstrzygnięte wyżej, lecz pozostają w tej samej tabeli poleceń.
        return PODCAST_INBOX_LIST_VIEW[canonical]
    if (
        not player_view
        and youtube_broadcasts_aggregate
        and canonical in YOUTUBE_BROADCASTS_AGGREGATE_LIST_VIEW
    ):
        return YOUTUBE_BROADCASTS_AGGREGATE_LIST_VIEW[canonical]
    if (
        not player_view
        and youtube_broadcasts
        and canonical in YOUTUBE_BROADCASTS_LIST_VIEW
    ):
        return YOUTUBE_BROADCASTS_LIST_VIEW[canonical]
    if (
        not player_view
        and youtube_channel
        and canonical in YOUTUBE_CHANNEL_LIST_VIEW
    ):
        action = YOUTUBE_CHANNEL_LIST_VIEW[canonical]
        if action is Action.YOUTUBE_BROADCASTS and not youtube_account_available:
            return None
        return action
    if files_session and canonical in FILES_SESSION_VIEW:
        return FILES_SESSION_VIEW[canonical]
    # Parity z głównym AMC: Ctrl+F5 zawsze odświeża wszystkie źródła sesji,
    # F5 w skrzynce robi to samo, a F5 w pozostałych widokach odświeża źródło
    # bieżącego podcastu lub odcinka.
    if (
        podcast_session
        and canonical == "Ctrl+S"
        and youtube_video_selection
    ):
        return Action.SAVE_YOUTUBE_VIDEO_AS
    if podcast_session and canonical in PODCAST_SESSION_VIEW:
        return PODCAST_SESSION_VIEW[canonical]
    # Alt+1/2/3 w TIDAL sortuja tylko trzy rownorzedne korzenie kolekcji.
    # W albumie, wykonawcy ani kolejce klawisze nie moga wpasc w lokalne
    # znaczenie „Foldery/Wszystkie pliki”.
    if tidal_session and canonical in TIDAL_COLLECTION_LIST_VIEW:
        return (
            TIDAL_COLLECTION_LIST_VIEW[canonical]
            if not player_view and tidal_collection
            else None
        )
    if tidal_session and canonical in TIDAL_SESSION_VIEW:
        return TIDAL_SESSION_VIEW[canonical]
    if tidal_session and not player_view and canonical in TIDAL_LIST_VIEW:
        return TIDAL_LIST_VIEW[canonical]
    # Spotify ma sześć równorzędnych korzeni kolekcji. Wewnątrz albumu,
    # playlisty, podcastu lub wykonawcy cyfry nie mogą uruchomić ani
    # sortowania, ani lokalnego znaczenia Folderów/Wszystkich plików.
    if spotify_session and canonical in SPOTIFY_COLLECTION_LIST_VIEW:
        return (
            SPOTIFY_COLLECTION_LIST_VIEW[canonical]
            if not player_view and spotify_collection
            else None
        )
    if spotify_session and canonical in SPOTIFY_SESSION_VIEW:
        return SPOTIFY_SESSION_VIEW[canonical]
    if spotify_session and not player_view and canonical in SPOTIFY_LIST_VIEW:
        return SPOTIFY_LIST_VIEW[canonical]
    if player_view and wiim_session and canonical in WIIM_PLAYER_VIEW:
        return WIIM_PLAYER_VIEW[canonical]
    if wiim_session and canonical in WIIM_SESSION_VIEW:
        action = WIIM_SESSION_VIEW[canonical]
        if action in {
            Action.EDIT_WIIM_NETWORK_STREAM,
            Action.REMOVE_WIIM_NETWORK_STREAMS,
            Action.SORT_WIIM_NETWORK_STREAMS_ADDED,
            Action.SORT_WIIM_NETWORK_STREAMS_ALPHABETICAL,
            Action.SORT_WIIM_NETWORK_STREAMS_CUSTOM,
            Action.MOVE_WIIM_NETWORK_STREAM_UP,
            Action.MOVE_WIIM_NETWORK_STREAM_DOWN,
            Action.CUT_WIIM_NETWORK_STREAMS,
            Action.PASTE_WIIM_NETWORK_STREAMS,
        } and not wiim_streams:
            return None
        return action
    if (
        frontier_smart_session
        and canonical in FRONTIER_SMART_SESSION_VIEW
    ):
        return FRONTIER_SMART_SESSION_VIEW[canonical]
    if radioarchiwista_session and canonical in RADIOARCHIWISTA_SESSION_VIEW:
        return RADIOARCHIWISTA_SESSION_VIEW[canonical]
    if sonos_session and canonical in SONOS_OWN_STREAMS_VIEW:
        return SONOS_OWN_STREAMS_VIEW[canonical] if sonos_own_streams else None
    if sonos_session and canonical in SONOS_SESSION_VIEW:
        return SONOS_SESSION_VIEW[canonical]
    if sonos_session and canonical in {"Shift+A", "Ctrl+[", "Ctrl+]"}:
        # Sonos nie używa lokalnych wyjść Windows. Jego celem jest grupa pod
        # Ctrl+F5; nie wysyłamy tych gestów do dialogu urządzeń komputera.
        return None
    # Widoki Radia sa dostepne takze z odtwarzacza, bez wychodzenia Escape.
    # Ta sama akcja co na liscie/menu; nie przenosimy edycji stacji do PLAYER.
    if radio_session and canonical == "Ctrl+U":
        return LIST_VIEW[canonical]
    if player_view and radio_session and canonical in RADIO_PLAYER_VIEW:
        return RADIO_PLAYER_VIEW[canonical]
    # Na zwyklej liscie gola litera B pozostaje wyszukiwaniem przyrostowym.
    # Zakladke nagrania przejmujemy tylko w rownorzednym widoku Nagrywane.
    if (
        not player_view
        and radio_session
        and radio_recording_context
        and canonical == "Right"
    ):
        return Action.RECORDING_OUTPUT_INFORMATION
    if (
        not player_view
        and radio_session
        and radio_recording_context
        and canonical in ("B", "Ctrl+Shift+B")
    ):
        return (
            Action.ADD_BOOKMARK
            if canonical == "B"
            else Action.ADD_NAMED_BOOKMARK
        )
    if not player_view and radio_session and canonical in RADIO_LIST_VIEW:
        return RADIO_LIST_VIEW[canonical]
    if (
        player_view
        and canonical == "R"
        and (tidal_session or wiim_session or sonos_session or frontier_smart_session)
    ):
        return None
    return table.get(canonical)


def resolve(
    chord: Chord,
    *,
    player_view: bool,
    radio_session: bool,
    shortcut_overrides: Mapping[Action, str] | None = None,
    **context: bool,
) -> Action | None:
    """Rozstrzygnij skrót, nakładając zapis użytkownika na wspólny model.

    Nadpisanie akcji wyłącza wszystkie jej dawne aliasy. Nowa kombinacja
    zachowuje dostępność polecenia: działa tylko w kontekście, w którym co
    najmniej jeden skrót domyślny prowadziłby do tej samej akcji.
    """
    effective = normalize_shortcut_overrides(shortcut_overrides)
    arguments = {
        "player_view": player_view,
        "radio_session": radio_session,
        **context,
    }
    if effective:
        for action, custom in effective.items():
            if not custom or custom != chord.canonical:
                continue
            for default_shortcut in _default_chords_by_action().get(action, ()):
                default_chord = chord_from_canonical(default_shortcut)
                if default_chord is not None and _resolve_default(
                    default_chord, **arguments
                ) is action:
                    return action
        default_action = _resolve_default(chord, **arguments)
        if default_action in effective:
            return None
        return default_action
    return _resolve_default(chord, **arguments)


def _help_entries() -> list[tuple[str, str, Action]]:
    """Tekst pomocy (F1). Po polsku, krotko, bez zaleznosci od wx."""
    labels = {
        Action.SESSION_FILES: "Pliki lokalne",
        Action.SESSION_RADIO: "Radio",
        Action.SESSION_PODCASTS: "Podcasty i YouTube",
        Action.SESSION_TIDAL: "TIDAL",
        Action.SESSION_WIIM: "WiiM",
        Action.SESSION_SONOS: "Sonos",
        Action.SESSION_SPOTIFY: "Spotify",
        Action.SESSION_FRONTIER_SMART: "Frontier Smart",
        Action.SESSION_RADIOARCHIWISTA: "Radio Archiwista",
        Action.SESSION_LIST: (
            "Menedżer sesji: Enter przełącza, Spacja włącza lub wyłącza, "
            "Alt+strzałka zmienia kolejność"
        ),
        Action.SESSION_ENABLE: "Włącz sesję w Menedżerze sesji",
        Action.SESSION_DISABLE_CURRENT: "Wyłącz bieżącą sesję",
        Action.SESSION_CONFIGURE_CURRENT: "Konfiguruj konto lub urządzenie bieżącej sesji",
        Action.MANAGE_LIBRARY_FOLDERS: (
            "Foldery Biblioteki: dodaj, odśwież albo bezpiecznie odłącz folder"
        ),
        Action.REFRESH_LOCAL_LIBRARY: "Odśwież wszystkie Foldery Biblioteki",
        Action.REFRESH_RADIO_STATIONS: "Odśwież stacje i bieżący widok Radia",
        Action.PREVIOUS_SESSION: "Poprzednia sesja",
        Action.NEXT_SESSION: "Następna sesja",
        Action.COMMAND_PALETTE: "Paleta poleceń",
        Action.SELECT_SONOS_TARGET: "Wybierz grupę Sonos",
        Action.FRONTIER_SMART_SELECT_MODE: (
            "Wybierz tryb urządzenia Frontier Smart"
        ),
        Action.ACTIVATE: "Otworz folder albo odtworz",
        Action.PARENT_FOLDER: "Folder nadrzedny",
        Action.CLOSE_LIST_PREVIEW: "Zamknij podglad lub podwidok",
        Action.SHOW_PLAYER: "Widok odtwarzacza",
        Action.SHOW_LIST: "Powrot na liste",
        Action.PLAY_PAUSE: "Pauza albo wznowienie",
        Action.TOGGLE_SHUFFLE: "Włącz lub wyłącz odtwarzanie losowe",
        Action.TOGGLE_MUTE: "Wycisz albo włącz dźwięk bieżącej sesji",
        Action.QUEUE_NEXT: "Nastepny utwor kolejki",
        Action.QUEUE_PREVIOUS: "Poprzedni utwor kolejki",
        Action.ADD_TO_QUEUE: "Dodaj zaznaczenie do kolejki albo je usun",
        Action.TOGGLE_PLAY_NEXT: "Odtwarzaj jako następne",
        Action.TOGGLE_FAVORITE: "Dodaj lub usuń z Ulubionych",
        Action.TOGGLE_RADIO_LIBRARY: "Dodaj lub usuń stację z Biblioteki Radia",
        Action.MANAGE_PLAYLIST_MEMBERSHIP: "Zmień przynależność do playlist",
        Action.TIDAL_TOGGLE_LIBRARY: "Dodaj lub usuń z Biblioteki TIDAL",
        Action.TIDAL_TOGGLE_FAVORITE: "Dodaj lub usuń z Ulubionych TIDAL",
        Action.SPOTIFY_TOGGLE_LIBRARY: "Dodaj lub usuń z Biblioteki Spotify",
        Action.SPOTIFY_TOGGLE_FAVORITE: "Dodaj lub usuń z Ulubionych Spotify",
        Action.TIDAL_RELATIONS: (
            "Wybierz przejście do albumu lub wykonawcy TIDAL"
        ),
        Action.SPOTIFY_RELATIONS: (
            "Wybierz przejście do albumu, wykonawcy lub podcastu Spotify"
        ),
        Action.SPOTIFY_VIEW_LIKED: "Spotify: Polubione utwory",
        Action.SPOTIFY_VIEW_ALBUMS: "Spotify: Albumy",
        Action.SPOTIFY_VIEW_ARTISTS: "Spotify: Wykonawcy",
        Action.SPOTIFY_VIEW_PLAYLISTS: "Spotify: Playlisty",
        Action.SPOTIFY_VIEW_PODCASTS: "Spotify: Podcasty",
        Action.SPOTIFY_VIEW_EPISODES: "Spotify: Zapisane odcinki",
        Action.RADIOARCHIWISTA_REFRESH: "Odśwież katalog Radio Archiwisty",
        Action.TOGGLE_RADIOARCHIWISTA_FAVORITE: "Dodaj lub usuń nagranie z ulubionych",
        Action.RADIOARCHIWISTA_DOWNLOAD: "Pobierz wybrane nagranie",
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
        Action.ADD_NAMED_BOOKMARK: "Dodaj nazwana zakladke w biezacym miejscu",
        Action.PREVIOUS_BOOKMARK: "Poprzednia zakładka",
        Action.NEXT_BOOKMARK: "Następna zakładka",
        Action.PREVIOUS_NAMED_BOOKMARK: "Poprzednia nazwana zakładka",
        Action.NEXT_NAMED_BOOKMARK: "Następna nazwana zakładka",
        Action.VIEW_CHAPTERS: "Pokaż rozdziały bieżącego materiału",
        Action.ADD_NAMED_CHAPTER: "Dodaj nazwany rozdział w bieżącym miejscu",
        Action.PREVIOUS_CHAPTER: "Poprzedni rozdział",
        Action.NEXT_CHAPTER: "Następny rozdział",
        Action.VIEW_BACK: "Wstecz w historii widoków",
        Action.VIEW_FORWARD: "Dalej w historii widoków",
        Action.PLAYBACK_HISTORY_OLDER: "Starszy odtwarzany element z historii",
        Action.PLAYBACK_HISTORY_NEWER: "Nowszy odtwarzany element z historii",
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
        Action.ADD_PODCAST_SOURCE: (
            "Dodaj podcast, SoundCloud, kanał YouTube, stronę Facebooka lub medium internetowe"
        ),
        Action.IMPORT_PODCAST_OPML: "Importuj podcasty z OPML",
        Action.EXPORT_PODCAST_OPML: "Eksportuj bibliotekę podcastów do OPML",
        Action.EXPORT_YOUTUBE_SUBSCRIPTIONS: "Eksportuj kanały i playlisty YouTube",
        Action.YOUTUBE_ACCOUNT: "Konto YouTube, subskrypcje i playlisty",
        Action.YOUTUBE_BROADCASTS: "Pokaż transmisje na żywo i zaplanowane kanału YouTube",
        Action.REFRESH_YOUTUBE_BROADCASTS: "Odśwież transmisje kanału YouTube",
        Action.ADD_YOUTUBE_LIVE_STATION: "Dodaj kanał jako dynamiczną stację YouTube na żywo",
        Action.VIEW_YOUTUBE_LIVE_AGGREGATE: "Pokaż transmisje wybranych kanałów YouTube",
        Action.CONFIGURE_YOUTUBE_LIVE_CHANNELS: "Wybierz kanały zbiorczej listy transmisji YouTube",
        Action.TOGGLE_YOUTUBE_LIVE_AGGREGATE_CHANNEL: "Uwzględniaj kanał na zbiorczej liście transmisji",
        Action.REFRESH_YOUTUBE_LIVE_AGGREGATE: "Odśwież transmisje wybranych kanałów YouTube",
        Action.RECORD_YOUTUBE_LIVE_NOW: "Rozpocznij albo zatrzymaj nagrywanie transmisji YouTube",
        Action.SCHEDULE_YOUTUBE_LIVE: "Zaplanuj nagranie transmisji YouTube",
        Action.REFRESH_PODCAST: "Odśwież wybrany podcast, kanał, playlistę lub stronę Facebooka",
        Action.REFRESH_PODCAST_LIBRARY: "Odśwież wszystkie źródła podcastów i Facebooka",
        Action.DOWNLOAD_PODCAST_EPISODES: "Pobierz zaznaczone odcinki lub dźwięk filmów jako MP3",
        Action.SAVE_PODCAST_EPISODE_AS: "Zapisz jeden odcinek podcastu jako plik",
        Action.DOWNLOAD_YOUTUBE_VIDEOS: (
            "Pobierz zaznaczone filmy YouTube, Facebooka lub Instagrama jako wideo"
        ),
        Action.SAVE_YOUTUBE_VIDEO_AS: "Zapisz jeden film internetowy jako plik wideo",
        Action.SHOW_PODCAST_DESCRIPTION: "Pokaż pełny opis podcastu lub odcinka",
        Action.CURRENT_RADIO_BROADCAST_INFORMATION: (
            "Informacje o bieżącym odtwarzaniu"
        ),
        Action.RECOGNIZE_RADIO_TRACK: "Rozpoznaj utwór z odtwarzanej stacji",
        Action.TOGGLE_RADIO_RECOGNITION_MONITORING: (
            "Włącz lub wyłącz obserwowanie rozpoznawania utworów"
        ),
        Action.TOGGLE_RADIO_RECOGNITION_ANNOUNCEMENTS: (
            "Włącz lub wyłącz oznajmianie rozpoznanych utworów"
        ),
        Action.VIEW_RADIO_RECOGNITION_HISTORY: (
            "Pokaż rozpoznane utwory; Enter otwiera utwór, Ctrl+Enter album, "
            "a prawa strzałka najważniejsze działania"
        ),
        Action.GO_TO_RELATED_PODCAST: "Przejdź do podcastu wybranego odcinka",
        Action.TOGGLE_PODCAST_FAVORITE: (
            "Dodaj zaznaczone podcasty lub odcinki do ulubionych albo je usuń"
        ),
        Action.TOGGLE_PODCAST_LIBRARY: (
            "Dodaj lub usuń podcast nadrzędny z Biblioteki"
        ),
        Action.PODCAST_PLAYBACK_OPTIONS: (
            "Opcje odtwarzania podcastu albo odcinka"
        ),
        # Skok procentowy. Dziesiec wierszy, bo oryginal ma dziesiec komend i
        # uzytkownik szuka w pomocy konkretnej cyfry, nie opisu rodziny.
        Action.SEEK_PERCENT_0: "Skok na początek materiału lub bufora Time Shift (0%)",
        Action.SEEK_PERCENT_10: "Skok do 10% materiału lub bufora Time Shift",
        Action.SEEK_PERCENT_20: "Skok do 20% materiału lub bufora Time Shift",
        Action.SEEK_PERCENT_30: "Skok do 30% materiału lub bufora Time Shift",
        Action.SEEK_PERCENT_40: "Skok do 40% materiału lub bufora Time Shift",
        Action.SEEK_PERCENT_50: "Skok do połowy materiału lub bufora Time Shift (50%)",
        Action.SEEK_PERCENT_60: "Skok do 60% materiału lub bufora Time Shift",
        Action.SEEK_PERCENT_70: "Skok do 70% materiału lub bufora Time Shift",
        Action.SEEK_PERCENT_80: "Skok do 80% materiału lub bufora Time Shift",
        Action.SEEK_PERCENT_90: "Skok do 90% materiału lub bufora Time Shift",
        Action.OPEN_FOLDER_DIALOG: "Wybierz folder",
        Action.OPEN_FILE_DIALOG: "Wybierz plik",
        Action.OPEN_CONTAINING_FOLDER: "Otwórz folder zawierający",
        Action.TRACK_START: "Poczatek utworu",
        # Pomoc mowi, ILE oryginal zostawia (CommandRouter.cs:321), zeby
        # uzytkownik nie uznal braku ciszy na koncu za blad.
        Action.TRACK_END: "Koniec utworu, dziesiec sekund przed koncem",
        Action.RADIO_JUMP_LIVE: "Wróć do transmisji na żywo",
        Action.COPY_NAME: "Skopiuj nazwe",
        Action.COPY_ADDRESS: "Skopiuj adres lub ścieżkę",
        Action.CUT_FILE: (
            "Wytnij plik albo zaznacz element ręcznej kolejności do przeniesienia"
        ),
        Action.PASTE_OR_MOVE: "Wstaw przenoszone pozycje przed bieżącym elementem",
        Action.MOVE_LIST_ITEM_UP: "Przenieś zaznaczenie wyżej na liście",
        Action.MOVE_LIST_ITEM_DOWN: "Przenieś zaznaczenie niżej na liście",
        Action.RENAME_LIBRARY_ITEM: (
            "Zmień nazwę pliku w Bibliotece, playlisty, podcastu lub kanału"
        ),
        Action.RENAME_LOCAL_FILE: "Zmień nazwę pliku na dysku",
        Action.REMOVE_SELECTED: "Usuń z bieżącego widoku bez kasowania pliku",
        Action.RECYCLE_SELECTED: "Przenieś zaznaczone pliki do Kosza",
        Action.STATION_ADD: "Dodaj stacje",
        Action.STATION_EDIT: "Zmień nazwę lub adres stacji",
        Action.STATION_DELETE: "Usun stacje",
        Action.STATION_IMPORT: "Importuj liste stacji",
        Action.STATION_EXPORT_FAVORITES: "Eksportuj ulubione stacje do playlisty",
        Action.RECORD_TOGGLE: "Rozpocznij albo zatrzymaj nagrywanie wybranej stacji",
        Action.RECORD_PAUSE: "Wstrzymaj albo wznow nagrywanie wybranej stacji",
        Action.RECORD_SPLIT: "Zapisz biezaca czesc i rozpocznij nowa",
        Action.RECORD_STOP_ALL: "Zatrzymaj wszystkie nagrania radia",
        Action.VIEW_ACTIVE_RECORDINGS: "Pokaż aktualnie nagrywane pliki",
        Action.RECORDING_OUTPUT_INFORMATION: (
            "Podaj format, nazwę pliku i folder bieżącego nagrania"
        ),
        Action.VIEW_RECORDED_RADIO_FILES: "Historia nagrywania",
        Action.VIEW_DOWNLOADED_FILES: "Pobrane pliki Radio Archiwisty, podcastów i wideo",
        Action.ADD_RADIO_SCHEDULE: "Zaplanuj nagranie wybranej stacji",
        Action.MANAGE_RADIO_SCHEDULES: "Harmonogram nagrywania",
        Action.SORT_RADIO_COLLECTION_ADDED: (
            "Radio: według dodania, najnowsze na początku"
        ),
        Action.SORT_RADIO_COLLECTION_ALPHABETICAL: "Radio: alfabetycznie",
        Action.SORT_RADIO_COLLECTION_CUSTOM: "Radio: kolejność własna",
        Action.VIEW_PODCAST_INBOX: "Nowe odcinki i materiały",
        Action.VIEW_PODCAST_DOWNLOADS: "Pobrane odcinki podcastów",
        Action.SORT_PODCAST_INBOX_ADDED: (
            "Nowe odcinki: według dodania, najnowsze na początku"
        ),
        Action.SORT_PODCAST_INBOX_ALPHABETICAL: "Nowe odcinki: alfabetycznie",
        Action.SORT_PODCAST_INBOX_BY_PODCAST: "Nowe odcinki: według podcastu",
        Action.SORT_PODCAST_LIBRARY_ADDED: (
            "Biblioteka podcastów i kanałów: ostatnio dodane na początku"
        ),
        Action.SORT_PODCAST_LIBRARY_ALPHABETICAL: (
            "Biblioteka podcastów i kanałów: alfabetycznie"
        ),
        Action.SORT_PODCAST_LIBRARY_CUSTOM: (
            "Biblioteka podcastów i kanałów: kolejność własna"
        ),
        Action.SORT_TIDAL_COLLECTION_ADDED: (
            "TIDAL: według dodania, najnowsze na początku"
        ),
        Action.SORT_TIDAL_COLLECTION_ALPHABETICAL: "TIDAL: alfabetycznie",
        Action.SORT_TIDAL_COLLECTION_CUSTOM: "TIDAL: kolejność własna",
        Action.SORT_SPOTIFY_COLLECTION_ADDED: (
            "Spotify: według dodania, najnowsze na początku"
        ),
        Action.SORT_SPOTIFY_COLLECTION_ALPHABETICAL: "Spotify: alfabetycznie",
        Action.SORT_SPOTIFY_COLLECTION_CATALOG: "Spotify: kolejność katalogu",
        Action.VIEW_PRESETS: "Pokaż presety aktywnej sesji",
        Action.VIEW_WIIM_NETWORK_STREAMS: "Otwórz zapisane strumienie WiiM",
        Action.WIIM_SELECT_INPUT: "Wybierz wejście urządzenia WiiM",
        Action.WIIM_SELECT_OUTPUT: "Wybierz wyjście urządzenia WiiM",
        Action.WIIM_SELECT_EQUALIZER: "Wybierz ustawienie korektora WiiM",
        Action.WIIM_SELECT_REPEAT: "Wybierz tryb powtarzania WiiM",
        Action.WIIM_TOGGLE_SHUFFLE: "Włącz lub wyłącz losowanie WiiM",
        Action.WIIM_SET_SLEEP_TIMER: "Ustaw timer uśpienia WiiM",
        Action.WIIM_PREVIOUS_SOURCE: (
            "Poprzedni strumień lub zajęty preset urządzenia WiiM"
        ),
        Action.WIIM_NEXT_SOURCE: (
            "Następny strumień lub zajęty preset urządzenia WiiM"
        ),
        Action.ADD_WIIM_NETWORK_STREAM: "Dodaj strumień sieciowy WiiM",
        Action.EDIT_WIIM_NETWORK_STREAM: "Edytuj wybrany strumień WiiM",
        Action.REMOVE_WIIM_NETWORK_STREAMS: "Usuń wybrane strumienie WiiM z AMC",
        Action.IMPORT_WIIM_NETWORK_STREAMS: "Importuj playlistę strumieni WiiM",
        Action.EXPORT_WIIM_NETWORK_STREAMS: "Eksportuj strumienie do WiiM Home",
        Action.SORT_WIIM_NETWORK_STREAMS_ADDED: (
            "Strumienie WiiM: według dodania, najnowsze najpierw"
        ),
        Action.SORT_WIIM_NETWORK_STREAMS_ALPHABETICAL: (
            "Strumienie WiiM: alfabetycznie"
        ),
        Action.SORT_WIIM_NETWORK_STREAMS_CUSTOM: (
            "Strumienie WiiM: kolejność własna"
        ),
        Action.MOVE_WIIM_NETWORK_STREAM_UP: "Strumienie WiiM: przenieś wyżej",
        Action.MOVE_WIIM_NETWORK_STREAM_DOWN: "Strumienie WiiM: przenieś niżej",
        Action.CUT_WIIM_NETWORK_STREAMS: (
            "Strumienie WiiM: zaznacz do przeniesienia"
        ),
        Action.PASTE_WIIM_NETWORK_STREAMS: (
            "Strumienie WiiM: wstaw przed bieżącym elementem"
        ),
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
        Action.SEARCH_CURRENT: "Szukaj w bieżącej usłudze",
        Action.SEARCH_ALL: "Szukaj we wszystkich usługach",
        # Nazwa mowi, CO klawisz daje, a nie jak dziala. "Uzupelniajace", bo
        # sama nazwa wiersza jest juz przeczytana przy nawigacji -- oryginal
        # celowo jej nie powtarza (QuickMediaInformationFormatter:8-10).
        Action.QUICK_INFORMATION: (
            "Informacje uzupelniajace o zaznaczonym wierszu "
            "(folder: sciezka)"
        ),
        Action.ITEM_PROPERTIES: (
            "Właściwości i pełne informacje o zaznaczonym lub odtwarzanym elemencie"
        ),
        Action.HELP: "Ta pomoc",
        Action.KEYBOARD_HELP: "Włącz lub wyłącz Pomoc klawiatury",
        Action.CHECK_APPLICATION_UPDATE: _STATIC_COMMAND_PRESENTATIONS[
            Action.CHECK_APPLICATION_UPDATE
        ].description,
        Action.UPDATE_MEDIA_COMPONENTS: (
            "Sprawdź, pobierz i zweryfikuj składniki FFmpeg i yt-dlp"
        ),
        Action.REPORT_PROBLEM: "Zgłoś błąd lub sugestię",
        Action.VERSION_INFO: "Wersja programu",
        # Pomoc mowi WPROST, ze zakres to sesja, a nie zaznaczony plik -- bez
        # tego uzytkownik nie wie, czego dotkna zmiany.
        Action.SESSION_OPTIONS: (
            "Ustawienia sesji: uruchamianie materiału, pozycja, domyślna "
            "prędkość, dźwięk i zachowanie po wyjściu, zależnie od możliwości sesji"
        ),
        Action.SELECT_AUDIO_OUTPUT: (
            "Wybierz urządzenie audio dla bieżącej sesji"
        ),
        Action.PREVIOUS_AUDIO_OUTPUT: (
            "Poprzednie dostępne urządzenie audio bieżącej sesji"
        ),
        Action.NEXT_AUDIO_OUTPUT: (
            "Następne dostępne urządzenie audio bieżącej sesji"
        ),
        Action.GENERAL_SETTINGS: "Ustawienia programu",
        Action.REFRESH_COMMON_DATA: "Odśwież dane z AMC Test",
        Action.COMMON_DATA_STATUS: "Stan synchronizacji danych z AMC Test",
        Action.EXPORT_BACKUP: "Eksportuj dane AMC",
        Action.IMPORT_BACKUP: "Importuj dane AMC",
    }
    labels.update({
        action: f"Uruchom preset {slot} bieżącej sesji"
        for slot, action in PRESET_ACTIONS.items()
    })
    # Nazwy klawiszy w pomocy musza byc TAKIE, jak na klawiaturze. Wewnetrzne
    # "Prior"/"Next" (z wx) czytnik przeczytalby jako obce slowa, a uzytkownik
    # nie znalazlby tych klawiszy pod palcami.
    readable_keys = {
        "Prior": "Page Up",
        "Next": "Page Down",
        "Return": "Enter",
        "Back": "Backspace",
    }

    seen: set[Action] = set()
    out: list[tuple[str, str, Action]] = []
    for table in (
        FILES_SESSION_VIEW,
        PODCAST_LIBRARY_LIST_VIEW,
        PODCAST_INBOX_LIST_VIEW,
        YOUTUBE_CHANNEL_LIST_VIEW,
        YOUTUBE_BROADCASTS_LIST_VIEW,
        YOUTUBE_BROADCASTS_AGGREGATE_LIST_VIEW,
        PODCAST_SESSION_VIEW,
        TIDAL_SESSION_VIEW,
        TIDAL_LIST_VIEW,
        SPOTIFY_SESSION_VIEW,
        SPOTIFY_COLLECTION_LIST_VIEW,
        SPOTIFY_LIST_VIEW,
        WIIM_SESSION_VIEW,
        WIIM_PLAYER_VIEW,
        FRONTIER_SMART_SESSION_VIEW,
        RADIOARCHIWISTA_SESSION_VIEW,
        SONOS_SESSION_VIEW,
        LIST_VIEW,
        RADIO_COLLECTION_LIST_VIEW,
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
                label = f"{label} (Radio)"
            elif action is Action.RADIO_JUMP_LIVE:
                label = f"{label} (Radio)"
            elif action is Action.IMPORT_PODCAST_OPML:
                label = f"{label} (Podcasty i YouTube)"
            elif action is Action.VIEW_WIIM_NETWORK_STREAMS:
                label = f"{label} (WiiM)"
            elif action is Action.SELECT_SONOS_TARGET:
                label = f"{label} (Sonos)"
            elif action in {
                Action.SPOTIFY_VIEW_LIKED,
                Action.SPOTIFY_VIEW_ALBUMS,
                Action.SPOTIFY_VIEW_ARTISTS,
                Action.SPOTIFY_VIEW_PLAYLISTS,
                Action.SPOTIFY_VIEW_PODCASTS,
                Action.SPOTIFY_VIEW_EPISODES,
            }:
                label = f"{label} (Spotify)"
            elif action in {
                Action.ADD_WIIM_NETWORK_STREAM,
                Action.EDIT_WIIM_NETWORK_STREAM,
                Action.REMOVE_WIIM_NETWORK_STREAMS,
                Action.IMPORT_WIIM_NETWORK_STREAMS,
                Action.EXPORT_WIIM_NETWORK_STREAMS,
                Action.SORT_WIIM_NETWORK_STREAMS_ADDED,
                Action.SORT_WIIM_NETWORK_STREAMS_ALPHABETICAL,
                Action.SORT_WIIM_NETWORK_STREAMS_CUSTOM,
                Action.MOVE_WIIM_NETWORK_STREAM_UP,
                Action.MOVE_WIIM_NETWORK_STREAM_DOWN,
                Action.CUT_WIIM_NETWORK_STREAMS,
                Action.PASTE_WIIM_NETWORK_STREAMS,
            }:
                label = f"{label} (Strumienie WiiM)"
            # Nigdy nie wystawiamy technicznego identyfikatora Enum do mowy
            # czytnika. Brak etykiety jest defektem katalogu, ale bezpieczny
            # tekst użytkowy nadal nie ujawnia ``action.value``.
            out.append((label, labels.get(action, "Polecenie AMC"), action))
    for chord, action in (
        ("Ctrl+S (film internetowy)", Action.SAVE_YOUTUBE_VIDEO_AS),
        ("Strzałka w prawo (Nagrywane)", Action.RECORDING_OUTPUT_INFORMATION),
    ):
        if action not in seen:
            seen.add(action)
            out.append((chord, labels[action], action))
    # Polecenia bez własnego skrótu nadal należą do pełnej pomocy i palety.
    # Tekst „Bez skrótu” jest celową etykietą użytkową, nie identyfikatorem.
    for action in (
        Action.SESSION_ENABLE,
        Action.SESSION_DISABLE_CURRENT,
        Action.SESSION_CONFIGURE_CURRENT,
        Action.YOUTUBE_ACCOUNT,
        Action.YOUTUBE_BROADCASTS,
        Action.ADD_YOUTUBE_LIVE_STATION,
        Action.CONFIGURE_YOUTUBE_LIVE_CHANNELS,
        Action.TOGGLE_YOUTUBE_LIVE_AGGREGATE_CHANNEL,
        Action.FRONTIER_SMART_SELECT_MODE,
        Action.REFRESH_COMMON_DATA,
        Action.COMMON_DATA_STATUS,
        Action.EXPORT_BACKUP,
        Action.IMPORT_BACKUP,
        Action.OPEN_CONTAINING_FOLDER,
        Action.CHECK_APPLICATION_UPDATE,
        Action.UPDATE_MEDIA_COMPONENTS,
        Action.REPORT_PROBLEM,
    ):
        if action not in seen:
            out.append(("Bez skrótu", labels[action], action))
    return out


def describe(
    session_label: str | None = None,
    available_actions: set[Action] | None = None,
    shortcut_overrides: dict[Action, str] | None = None,
    *,
    downloaded_files: bool = False,
) -> list[tuple[str, str]]:
    """Tekst spisu F1 bez technicznych identyfikatorów poleceń.

    Nazwa sesji jest opcjonalna dla zgodności narzędzi diagnostycznych. Okno
    AMC przekazuje ją zawsze, dzięki czemu polecenia kontekstowe mają ten sam
    opis co menu, Ctrl+F1 i paleta.
    """
    overrides = dict(shortcut_overrides or {})
    ctrl_f5 = ctrl_f5_presentation(session_label)
    return [
        (
            (
                readable_shortcut(overrides[action])
                if action in overrides
                else ctrl_f5.shortcut
                if ctrl_f5 is not None and ctrl_f5.action is action
                else chord
            ),
            describe_action(action, session_label, downloaded_files=downloaded_files),
        )
        for chord, _label, action in _help_entries()
        if available_actions is None or action in available_actions
    ]


DOWNLOAD_HISTORY_COPY_LABELS = {
    Action.COPY_NAME: "Skopiuj pełną ścieżkę pobranego pliku",
    Action.COPY_ADDRESS: "Skopiuj pobrany plik do wklejenia",
}


def describe_action(
    action: Action, session_label: str | None = None, *, downloaded_files: bool = False
) -> str:
    """Jedna użytkowa nazwa polecenia dla Pomocy klawiatury Ctrl+F1."""
    if downloaded_files and action in DOWNLOAD_HISTORY_COPY_LABELS:
        return DOWNLOAD_HISTORY_COPY_LABELS[action]
    if (session_label or "").casefold() == "radio archiwista":
        archive_labels = {
            Action.VIEW_LIBRARY: "Pokaż pełną listę stacji Radio Archiwisty",
            Action.VIEW_SAVED_QUEUE: "Pokaż lokalną kolejkę nagrań Radio Archiwisty",
            Action.ADD_TO_QUEUE: "Dodaj nagranie do lokalnej kolejki albo je usuń",
            Action.TOGGLE_PLAY_NEXT: "Dodaj nagranie za bieżącym w lokalnej kolejce albo je usuń",
            Action.COPY_ADDRESS: "Skopiuj bezpośredni link do nagrania",
        }
        if action in archive_labels:
            return i18n.command_description(action.value, archive_labels[action])
    ctrl_f5 = ctrl_f5_presentation(session_label)
    if ctrl_f5 is not None and ctrl_f5.action is action:
        return ctrl_f5.description
    shared = command_presentation(action)
    if shared is not None:
        return shared.description
    if (session_label or "").casefold() == "spotify":
        spotify_chapter_labels = {
            Action.VIEW_CHAPTERS:
                "Pokaż rozdziały użytkownika oraz, w odcinku, znaczniki czasu z opisu",
            Action.ADD_NAMED_CHAPTER:
                "Dodaj nazwany rozdział użytkownika w bieżącym miejscu",
            Action.PREVIOUS_CHAPTER:
                "Poprzedni rozdział użytkownika albo, w odcinku, znacznik czasu z opisu",
            Action.NEXT_CHAPTER:
                "Następny rozdział użytkownika albo, w odcinku, znacznik czasu z opisu",
        }
        if action in spotify_chapter_labels:
            return i18n.command_description(
                action.value, spotify_chapter_labels[action]
            )
    if (
        action is Action.MANAGE_PLAYLIST_MEMBERSHIP
        and (session_label or "").casefold() == "spotify"
    ):
        return i18n.command_description(
            action.value, "Dodaj do playlist Spotify lub utwórz playlistę"
        )
    for _chord, label, candidate in _help_entries():
        if candidate is action:
            return i18n.command_description(action.value, label)
    return i18n.command_description(action.value, "Polecenie AMC")


def configurable_shortcuts() -> tuple[ShortcutDefinition, ...]:
    """Zwróć stabilną, użytkową listę pozycji karty Klawiatura."""
    defaults = _default_chords_by_action()
    definitions = [
        ShortcutDefinition(
            action=action,
            name=describe_action(action),
            default_shortcut=shortcuts[0],
        )
        for action, shortcuts in defaults.items()
        if shortcuts and is_configurable_shortcut(action)
    ]
    return tuple(sorted(
        definitions,
        key=lambda item: (item.name.casefold(), item.default_shortcut),
    ))


def readable_chord(chord: Chord) -> str:
    """Nazwa klawisza przeznaczona do mowy, nie zapis wewnętrzny wx."""
    text = chord.canonical
    for internal, spoken in {
        "Prior": "Page Up",
        "Next": "Page Down",
        "Return": "Enter",
        "Back": "Backspace",
        "Space": i18n.tr("shortcut.space"),
    }.items():
        text = text.replace(internal, spoken)
    return text


def keyboard_help_announcement(
    chord: Chord,
    action: Action | None,
    session_label: str,
    *,
    available: bool = True,
    downloaded_files: bool = False,
) -> str:
    """Zwięzły opis Ctrl+F1: skrót, polecenie i kontekst sesji."""
    shortcut = readable_chord(chord)
    if action is None:
        return i18n.tr(
            "keyboardHelp.noCommand",
            shortcut=shortcut,
            session=session_label,
        )
    label = describe_action(action, session_label, downloaded_files=downloaded_files)
    if not available:
        return i18n.tr(
            "keyboardHelp.unavailable",
            shortcut=shortcut,
            command=label,
            session=session_label,
        )
    return i18n.tr(
        "keyboardHelp.available",
        shortcut=shortcut,
        command=label,
        session=session_label,
    )
