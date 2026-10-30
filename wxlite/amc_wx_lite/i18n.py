"""Wspólna warstwa językowa interfejsu AMC wxPython.

Klucze katalogu są trwałym API interfejsu.  Tekst polski nie pełni roli
identyfikatora i może być poprawiany bez zmiany ustawień, poleceń ani danych
zapisywanych przez użytkownika.  Brak tłumaczenia nigdy nie wystawia klucza
technicznego czytnikowi ekranu: bezpiecznym zapasem jest tekst polski podany
przez wywołującego.
"""

from __future__ import annotations

import json
import locale
import re
from collections.abc import Mapping
from pathlib import Path


SYSTEM = "system"
POLISH = "pl"
ENGLISH = "en"
LANGUAGE_PREFERENCES = (SYSTEM, POLISH, ENGLISH)
SUPPORTED_LANGUAGES = (POLISH, ENGLISH)


_CATALOGS: dict[str, dict[str, str]] = {
    POLISH: {
        "language.system": "Zgodny z systemem",
        "language.polish": "Polski",
        "language.english": "Angielski",
        "list.column.name": "Nazwa",
        "list.column.kind": "Rodzaj",
        "list.column.details": "Szczegoly",
        "session.files": "Pliki lokalne",
        "session.radio": "Radio",
        "session.podcasts": "Podcasty i YouTube",
        "session.tidal": "TIDAL",
        "session.spotify": "Spotify",
        "session.wiim": "WiiM",
        "session.sonos": "Sonos",
        "session.frontierSmart": "Frontier Smart",
        "session.radioarchiwista": "Radio Archiwista",
        # Radio Archiwista: komunikaty sesji (stabilne klucze, odmiana przez
        # rarch.recordings.one/few/many i rarch.items.one/few/many).
        "rarch.session.unavailable":
            "Sesja Radio Archiwista nie jest dostępna w tym profilu",
        "rarch.command.unavailable": "To polecenie nie jest dostępne w sesji Radio Archiwista",
        "rarch.catalog.loading": "Wczytuję stacje Radio Archiwisty",
        "rarch.catalog.refreshing": "Odświeżam katalog Radio Archiwisty",
        "rarch.catalog.addressTitle": "Adres katalogu Radio Archiwisty",
        "rarch.catalog.addressPrompt": "Adres katalogu nagrań (HTTPS):",
        "rarch.catalog.addressSaved": "Zapisano adres katalogu Radio Archiwisty",
        "rarch.catalog.addressFailed": "Nie zapisano adresu katalogu: {error}",
        "rarch.latest.loading": "Wczytuję najnowsze nagrania",
        "rarch.latest.menu": "Najnowsze nagrania",
        "rarch.latest.announcement":
            "Najnowsze nagrania, godzina {label} UTC, {phrase}",
        "rarch.error": "Radio Archiwista: {error}",
        "rarch.selectForFavorite": "Zaznacz nagranie, aby zmienić ulubione",
        "rarch.favorite.added": "Dodano do ulubionych: {title}",
        "rarch.favorite.removed": "Usunięto z ulubionych: {title}",
        "rarch.selectForDownload": "Zaznacz nagranie, aby je pobrać",
        "rarch.download.busy": "Trwa już pobieranie nagrania",
        "rarch.download.title": "Pobieranie nagrania Radio Archiwisty",
        "rarch.download.progress": "Pobrano {percent} procent. Anuluj, aby przerwać pobieranie.",
        "rarch.download.waiting": "Pobieram nagranie. Anuluj, aby przerwać pobieranie.",
        "rarch.download.cancelled": "Anulowano pobieranie nagrania",
        "rarch.download.started": "Pobieram nagranie",
        "rarch.download.done": "Pobrano nagranie do: {path}",
        "rarch.download.failed": "Nie udało się pobrać nagrania: {error}",
        "rarch.recording.noAudio": "To nagranie nie ma dostępnego pliku audio",
        "rarch.open.failed": "Nie można otworzyć tego elementu Radio Archiwisty",
        "rarch.loadMore.unknown": "Nie wiadomo, dla którego widoku wczytać więcej",
        "rarch.loadMore.row": "Wczytaj więcej (pokazane {shown} z {total})",
        "rarch.view.count": "{heading}, {phrase}",
        "rarch.view.empty": "{heading}, pusto",
        "rarch.heading.stations": "Radio Archiwista, stacje",
        "rarch.heading.years": "Lata",
        "rarch.heading.months": "Miesiące",
        "rarch.heading.days": "Dni",
        "rarch.heading.hours": "Nagrania",
        "rarch.heading.favorites": "Ulubione nagrania",
        "rarch.recordings.one": "{count} nagranie",
        "rarch.recordings.few": "{count} nagrania",
        "rarch.recordings.many": "{count} nagrań",
        "rarch.items.one": "{count} pozycja",
        "rarch.items.few": "{count} pozycje",
        "rarch.items.many": "{count} pozycji",
        "rarch.detail.incomplete": "nagranie niepełne",
        "rarch.detail.status": "status: {status}",
        "rarch.detail.remaining": "pozostało {minutes} min",
        "rarch.month.1": "styczeń",
        "rarch.month.2": "luty",
        "rarch.month.3": "marzec",
        "rarch.month.4": "kwiecień",
        "rarch.month.5": "maj",
        "rarch.month.6": "czerwiec",
        "rarch.month.7": "lipiec",
        "rarch.month.8": "sierpień",
        "rarch.month.9": "wrzesień",
        "rarch.month.10": "październik",
        "rarch.month.11": "listopad",
        "rarch.month.12": "grudzień",
        "view.allFiles": "Wszystkie pliki",
        "view.favorites": "Ulubione",
        "view.playlists": "Playlisty",
        "view.playlist": "Playlista",
        "view.playbackHistory": "Historia odtwarzania",
        "view.savedQueue": "Zapisana kolejka",
        "view.playbackQueue": "Kolejka odtwarzania",
        "view.bookmarks": "Zakładki",
        "view.activeRecordings": "Nagrywane",
        "view.recordingHistory": "Historia nagrywania",
        "view.recordingSchedules": "Harmonogramy nagrywania",
        "view.sonosLibrary": "Biblioteka Sonos",
        "view.sonosFavorites": "Ulubione Sonos",
        "view.sonosPlaylists": "Playlisty Sonos",
        "view.sonosOwnStreams": "Moje stacje Sonos",
        "view.spotifyLibrary": "Biblioteka Spotify",
        "view.spotifyHistory": "Spotify — Historia odtwarzania",
        "view.youtubeBroadcasts": "Transmisje YouTube na żywo i zaplanowane",
        "view.youtubeAggregateBroadcasts": (
            "Transmisje wybranych kanałów YouTube"
        ),
        "settings.title": "Ustawienia",
        "settings.categories.accessibleName": "Kategorie ustawień",
        "settings.interfaceLanguage.label": "Język interfejsu:",
        "settings.interfaceLanguage.accessibleName": "Język interfejsu",
        "settings.interfaceLanguage.help": (
            "Zmiana języka zostanie zastosowana po zapisaniu ustawień."
        ),
        "settings.tab.general": "Ogólne",
        "settings.tab.radio": "Radio i nagrywanie",
        "settings.tab.podcasts": "Podcasty i YouTube",
        "settings.tab.accessibility": "Dostępność i komunikaty",
        "settings.tab.keyboard": "Klawiatura",
        "settings.tab.updates": "Aktualizacje",
        "settings.tab.windows": "Integracja z Windows",
        "settings.save.label": "&Zapisz",
        "settings.save.accessibleName": "Zapisz ustawienia",
        "settings.cancel.label": "&Anuluj",
        "settings.cancel.accessibleName": "Anuluj ustawienia",
        "settings.messageLanguage.label": "Język własnych tekstów komunikatów:",
        "settings.messageLanguage.accessibleName": (
            "Język własnych tekstów komunikatów"
        ),
        "settings.messageLanguage.help": (
            "Każdy język ma osobne teksty. Zmiana tego wyboru nie zmienia "
            "języka interfejsu."
        ),
        "settings.messageTemplates.heading": "Własne teksty komunikatów",
        "settings.messageTemplates.heading.accessibleName": (
            "Sekcja własne teksty komunikatów"
        ),
        "settings.messageTemplates.explanation": (
            "Wybierz język i zdarzenie, a następnie zmień jego tekst. "
            "Pusty tekst wyłącza dany komunikat tylko w wybranym języku."
        ),
        "settings.messageTemplates.event.label": "Zdarzenie:",
        "settings.messageTemplates.event.accessibleName": "Zdarzenia komunikatów",
        "settings.messageTemplates.event.help": (
            "Strzałkami wybierz zdarzenie, a następnie naciśnij Tab, "
            "aby przejść do jego tekstu."
        ),
        "settings.messageTemplates.text.label": "Tekst wybranego komunikatu:",
        "settings.messageTemplates.text.accessibleName": (
            "Tekst szablonu wybranego komunikatu"
        ),
        "settings.messageTemplates.text.help": (
            "Zmień tekst komunikatu albo pozostaw pole puste, aby wyłączyć "
            "tylko wybrane zdarzenie w wybranym języku."
        ),
        "palette.count.none": "Brak pasujących poleceń",
        "palette.count.one": "1 polecenie",
        "palette.count.few": "{count} polecenia",
        "palette.count.many": "{count} poleceń",
        "keyboardHelp.noCommand": (
            "{shortcut}. Brak polecenia w sesji {session}"
        ),
        "keyboardHelp.unavailable": (
            "{shortcut}. {command}. Niedostępne w sesji {session}"
        ),
        "keyboardHelp.available": "{shortcut}. {command}. {session}",
        "shortcut.space": "Spacja",
        "shortcut.left": "strzałka w lewo",
        "shortcut.right": "strzałka w prawo",
        "shortcut.up": "strzałka w górę",
        "shortcut.down": "strzałka w dół",
    },
    ENGLISH: {
        "language.system": "Use system language",
        "language.polish": "Polish",
        "language.english": "English",
        "list.column.name": "Name",
        "list.column.kind": "Type",
        "list.column.details": "Details",
        "session.files": "Local files",
        "session.radio": "Radio",
        "session.podcasts": "Podcasts and YouTube",
        "session.tidal": "TIDAL",
        "session.spotify": "Spotify",
        "session.wiim": "WiiM",
        "session.sonos": "Sonos",
        "session.frontierSmart": "Frontier Smart",
        "session.radioarchiwista": "Radio Archiwista",
        # Radio Archiwista: sesja komunikatow (stabilne klucze).
        "rarch.session.unavailable":
            "The Radio Archiwista session is not available in this profile",
        "rarch.command.unavailable": "This command is not available in Radio Archiwista",
        "rarch.catalog.loading": "Loading Radio Archiwista stations",
        "rarch.catalog.refreshing": "Refreshing the Radio Archiwista catalog",
        "rarch.catalog.addressTitle": "Radio Archiwista catalog address",
        "rarch.catalog.addressPrompt": "Recording catalog address (HTTPS):",
        "rarch.catalog.addressSaved": "Radio Archiwista catalog address saved",
        "rarch.catalog.addressFailed": "Could not save catalog address: {error}",
        "rarch.latest.loading": "Loading the latest recordings",
        "rarch.latest.menu": "Latest recordings",
        "rarch.latest.announcement":
            "Latest recordings, {label} UTC, {phrase}",
        "rarch.error": "Radio Archiwista: {error}",
        "rarch.selectForFavorite": "Select a recording to change favorites",
        "rarch.favorite.added": "Added to favorites: {title}",
        "rarch.favorite.removed": "Removed from favorites: {title}",
        "rarch.selectForDownload": "Select a recording to download it",
        "rarch.download.busy": "A recording download is already in progress",
        "rarch.download.title": "Downloading Radio Archiwista recording",
        "rarch.download.progress": "Downloaded {percent} percent. Cancel to stop downloading.",
        "rarch.download.waiting": "Downloading recording. Cancel to stop downloading.",
        "rarch.download.cancelled": "Recording download cancelled",
        "rarch.download.started": "Downloading recording",
        "rarch.download.done": "Recording downloaded to: {path}",
        "rarch.download.failed": "Could not download the recording: {error}",
        "rarch.recording.noAudio": "This recording has no audio file available",
        "rarch.open.failed": "Cannot open this Radio Archiwista item",
        "rarch.loadMore.unknown": "Unknown view for loading more",
        "rarch.loadMore.row": "Load more ({shown} of {total} shown)",
        "rarch.view.count": "{heading}, {phrase}",
        "rarch.view.empty": "{heading}, empty",
        "rarch.heading.stations": "Radio Archiwista, stations",
        "rarch.heading.years": "Years",
        "rarch.heading.months": "Months",
        "rarch.heading.days": "Days",
        "rarch.heading.hours": "Recordings",
        "rarch.heading.favorites": "Favorite recordings",
        "rarch.recordings.one": "{count} recording",
        "rarch.recordings.few": "{count} recordings",
        "rarch.recordings.many": "{count} recordings",
        "rarch.items.one": "{count} item",
        "rarch.items.few": "{count} items",
        "rarch.items.many": "{count} items",
        "rarch.detail.incomplete": "incomplete recording",
        "rarch.detail.status": "status: {status}",
        "rarch.detail.remaining": "{minutes} min remaining",
        "rarch.month.1": "January",
        "rarch.month.2": "February",
        "rarch.month.3": "March",
        "rarch.month.4": "April",
        "rarch.month.5": "May",
        "rarch.month.6": "June",
        "rarch.month.7": "July",
        "rarch.month.8": "August",
        "rarch.month.9": "September",
        "rarch.month.10": "October",
        "rarch.month.11": "November",
        "rarch.month.12": "December",
        "view.allFiles": "All files",
        "view.favorites": "Favorites",
        "view.playlists": "Playlists",
        "view.playlist": "Playlist",
        "view.playbackHistory": "Playback history",
        "view.savedQueue": "Saved queue",
        "view.playbackQueue": "Playback queue",
        "view.bookmarks": "Bookmarks",
        "view.activeRecordings": "Active recordings",
        "view.recordingHistory": "Recording history",
        "view.recordingSchedules": "Recording schedules",
        "view.sonosLibrary": "Sonos library",
        "view.sonosFavorites": "Sonos favorites",
        "view.sonosPlaylists": "Sonos playlists",
        "view.sonosOwnStreams": "My Sonos stations",
        "view.spotifyLibrary": "Spotify library",
        "view.spotifyHistory": "Spotify — Playback history",
        "view.youtubeBroadcasts": "YouTube live and scheduled broadcasts",
        "view.youtubeAggregateBroadcasts": (
            "Broadcasts from selected YouTube channels"
        ),
        "settings.title": "Settings",
        "settings.categories.accessibleName": "Settings categories",
        "settings.interfaceLanguage.label": "Interface &language:",
        "settings.interfaceLanguage.accessibleName": "Interface language",
        "settings.interfaceLanguage.help": (
            "The language change is applied after you save the settings."
        ),
        "settings.tab.general": "General",
        "settings.tab.radio": "Radio and recording",
        "settings.tab.podcasts": "Podcasts and YouTube",
        "settings.tab.accessibility": "Accessibility and messages",
        "settings.tab.keyboard": "Keyboard",
        "settings.tab.updates": "Updates",
        "settings.tab.windows": "Windows integration",
        "settings.save.label": "&Save",
        "settings.save.accessibleName": "Save settings",
        "settings.cancel.label": "&Cancel",
        "settings.cancel.accessibleName": "Cancel settings",
        "settings.messageLanguage.label": "Custom message text &language:",
        "settings.messageLanguage.accessibleName": (
            "Custom message text language"
        ),
        "settings.messageLanguage.help": (
            "Each language has separate texts. This choice does not change "
            "the interface language."
        ),
        "settings.messageTemplates.heading": "Custom message texts",
        "settings.messageTemplates.heading.accessibleName": (
            "Custom message texts section"
        ),
        "settings.messageTemplates.explanation": (
            "Choose a language and an event, then edit its text. An empty "
            "text disables that message only in the selected language."
        ),
        "settings.messageTemplates.event.label": "&Event:",
        "settings.messageTemplates.event.accessibleName": "Message events",
        "settings.messageTemplates.event.help": (
            "Use the arrow keys to choose an event, then press Tab to move "
            "to its text."
        ),
        "settings.messageTemplates.text.label": "Selected message &text:",
        "settings.messageTemplates.text.accessibleName": (
            "Selected message template text"
        ),
        "settings.messageTemplates.text.help": (
            "Edit the message text or leave it empty to disable only the "
            "selected event in the selected language."
        ),
        "palette.count.none": "No matching commands",
        "palette.count.one": "1 command",
        "palette.count.few": "{count} commands",
        "palette.count.many": "{count} commands",
        "keyboardHelp.noCommand": (
            "{shortcut}. No command in the {session} session"
        ),
        "keyboardHelp.unavailable": (
            "{shortcut}. {command}. Unavailable in the {session} session"
        ),
        "keyboardHelp.available": "{shortcut}. {command}. {session}",
        "shortcut.space": "Space",
        "shortcut.left": "left arrow",
        "shortcut.right": "right arrow",
        "shortcut.up": "up arrow",
        "shortcut.down": "down arrow",
    },
}


_active_language = POLISH


def normalize_preference(value: object) -> str:
    """Return a supported persistent preference without accepting aliases."""

    if isinstance(value, str):
        normalized = value.strip().casefold()
        if normalized in LANGUAGE_PREFERENCES:
            return normalized
    return SYSTEM


def system_language(system_locale: str | None = None) -> str:
    """Map the Windows/user locale to a complete AMC language catalog."""

    raw = system_locale
    if raw is None:
        try:
            raw = locale.getlocale()[0]
        except (ValueError, TypeError):
            raw = None
    normalized = re.sub(r"[^a-z]+", "_", str(raw or "").casefold()).strip("_")
    primary_language = normalized.partition("_")[0]
    # CPython on Polish Windows returns ``Polish_Poland`` rather than
    # ``pl_PL``.  Both forms describe the same user language and must resolve
    # to the same catalog.
    return POLISH if primary_language in {"pl", "polish"} else ENGLISH


def resolve_language(
    preference: object,
    *,
    system_locale: str | None = None,
) -> str:
    normalized = normalize_preference(preference)
    if normalized == SYSTEM:
        return system_language(system_locale)
    return normalized


def configure(preference: object, *, system_locale: str | None = None) -> str:
    """Select the process-wide UI catalog and return its concrete language."""

    global _active_language
    _active_language = resolve_language(preference, system_locale=system_locale)
    return _active_language


def active_language() -> str:
    return _active_language


def tr(key: str, **values: object) -> str:
    """Translate a stable key, never exposing the key as user-facing text."""

    text = _CATALOGS.get(_active_language, _CATALOGS[POLISH]).get(key)
    if text is None:
        text = _CATALOGS[POLISH].get(key)
    if text is None:
        raise KeyError(f"Unknown translation key: {key}")
    return text.format(**values) if values else text


def text_for(language: str, key: str, **values: object) -> str:
    concrete = language if language in SUPPORTED_LANGUAGES else POLISH
    text = _CATALOGS[concrete][key]
    return text.format(**values) if values else text


def plural_category(language: str, count: int) -> str:
    """Zwraca kategorię odmiany liczebnika: one, few albo many.

    Polski: 1 → one; końcówka 2-4 poza 12-14 → few; reszta → many.
    Angielski: 1 → one; reszta → many (kategoria few trafia do many).
    """

    if count == 1:
        return "one"
    if language != ENGLISH and count % 10 in (2, 3, 4) and count % 100 not in (12, 13, 14):
        return "few"
    return "many"


def plural_phrase(key_prefix: str, count: int) -> str:
    """tr() z kluczem odmienionym przez kategorię, np. rarch.recordings.many."""

    category = plural_category(active_language(), count)
    return tr(f"{key_prefix}.{category}", count=count)


def recordings_phrase(count: int) -> str:
    """„1 nagranie", „3 nagrania", „4916 nagrań" w języku interfejsu."""

    return plural_phrase("rarch.recordings", count)


def items_phrase(count: int) -> str:
    """„1 pozycja", „3 pozycje", „5 pozycji" w języku interfejsu."""

    return plural_phrase("rarch.items", count)


def month_name(month: int) -> str:
    """Nazwa miesiąca katalogu Radio Archiwista w języku interfejsu."""

    return tr(f"rarch.month.{max(1, min(12, month))}")


def language_choices() -> tuple[tuple[str, str], ...]:
    return (
        (SYSTEM, tr("language.system")),
        (POLISH, tr("language.polish")),
        (ENGLISH, tr("language.english")),
    )


def concrete_language_choices() -> tuple[tuple[str, str], ...]:
    return (
        (POLISH, tr("language.polish")),
        (ENGLISH, tr("language.english")),
    )


def catalogs() -> Mapping[str, Mapping[str, str]]:
    """Read-only-by-convention view used by completeness tests and tooling."""

    return _CATALOGS


_CAMEL_BOUNDARY = re.compile(r"(?<=[a-z0-9])(?=[A-Z])")


_ACTION_NAME_OVERRIDES: dict[str, str] = {
    "session.files": "Local files",
    "session.radio": "Radio",
    "session.podcasts": "Podcasts and YouTube",
    "session.tidal": "TIDAL",
    "session.wiim": "WiiM",
    "session.sonos": "Sonos",
    "session.spotify": "Spotify",
    "session.frontierSmart": "Frontier Smart",
    "session.radioarchiwista": "Radio Archiwista",
    "session.list": "Session manager",
    "transport.playPause": "Play or pause",
    "transport.toggleMute": "Mute or unmute",
    "transport.shuffle.toggle": "Toggle shuffle",
    "view.commandPalette": "Command palette",
    "view.closePreview": "Close preview",
    "keyboard.help": "Keyboard help",
    "help": "Help",
    "help.version": "About AMC",
    "help.checkApplicationUpdate": "Check for AMC updates",
    "help.updateMediaComponents": "Update FFmpeg and yt-dlp",
    "help.reportProblem": "Report a problem or suggestion",
    "settings.general": "Settings",
    "settings.toggleSeekMessages": "Automatic player messages",
    "files.openFile": "Open file",
    "files.openFolder": "Open folder",
    "files.openContainingFolder": "Open containing folder",
    "action.favorite.toggle": "Add to or remove from favorites",
    "action.queue.add": "Add to or remove from queue",
    "action.queue.playNextToggle": "Play next",
    "action.playlists.membership": "Change playlist membership",
    "radio.currentBroadcastInformation": "Current broadcast information",
    "radio.recognition.now": "Recognize the current track",
    "radio.recognition.history": "Recognized tracks",
    "radio.recording.activeView": "Active recordings",
    "radio.recording.completedView": "Recording history",
    "radio.recording.schedules": "Recording schedule",
    "podcasts.inbox": "New episodes and media",
    "podcasts.downloads": "Downloaded episodes",
    "podcasts.description": "Podcast or episode description",
    "youtube.account.manage": "YouTube account, subscriptions, and playlists",
    "view.player": "Player view",
    "view.list": "List view",
    "view.history": "Playback history",
    "view.queue": "Queue",
    "view.library": "Library",
    "library.allFiles": "All files",
    "library.favorites": "Favorites",
    "library.playlists": "Playlists",
    "library.folders": "Library folders",
    "list.filter": "Filter the list",
    "list.quickInformation": "Selected item quick information",
    "list.itemProperties": "Item properties and full information",
    "time.elapsed": "Elapsed time",
    "time.remaining": "Remaining time",
    "time.total": "Total time",
    "rate.up": "Increase playback speed",
    "rate.down": "Decrease playback speed",
    "rate.reset": "Normal playback speed",
    "radio.jumpLive": "Return to live broadcast",
    "library.folderSources.manage": "Manage Library folders",
    "library.folderSources.refresh": "Refresh all Library folders",
    "presets.view": "Show presets",
    "presets.assign": "Create or assign a preset",
    "library.itemBookmarks": "Item bookmarks",
    "library.allBookmarks": "All bookmarks",
    "clipboard.copyName": "Copy name",
    "clipboard.copyAddress": "Copy address or path",
    "clipboard.cutFile": "Cut or mark for moving",
    "clipboard.pasteOrMove": "Paste or move here",
    "library.moveItemUp": "Move item up",
    "library.moveItemDown": "Move item down",
    "library.renameItem": "Rename item",
    "library.renameFile": "Rename file on disk",
    "library.removeSelected": "Remove from this view",
    "library.recycleSelected": "Move selected files to the Recycle Bin",
    "view.search.current": "Search the current service",
    "view.search.all": "Search all services",
    "radio.add": "Add station",
    "radio.edit": "Edit station",
    "radio.delete": "Delete station",
    "radio.import": "Import stations",
    "radio.exportFavorites": "Export favorite stations",
    "radio.recording.toggle": "Start or stop recording",
    "radio.recording.pause": "Pause or resume recording",
    "radio.recording.split": "Split the current recording",
    "radio.recording.stopAll": "Stop all recordings",
    "radio.recording.outputInformation": "Recording file information",
    "radio.recording.schedule.add": "Schedule a recording",
    "radio.recognition.monitor.toggle": "Toggle automatic track recognition",
    "radio.recognition.announcements.toggle": "Toggle recognized-track announcements",
    "radio.stations.refresh": "Refresh radio stations",
    "radio.library.toggle": "Add to or remove from the Radio Library",
    "radio.collection.sort.added": "Sort Radio by date added",
    "radio.collection.sort.alphabetical": "Sort Radio alphabetically",
    "radio.collection.sort.custom": "Use custom Radio order",
    "action.bookmark.add": "Add bookmark",
    "action.bookmark.addNamed": "Add named bookmark",
    "transport.bookmark.previous": "Previous bookmark",
    "transport.bookmark.next": "Next bookmark",
    "transport.bookmark.named.previous": "Previous named bookmark",
    "transport.bookmark.named.next": "Next named bookmark",
    "view.chapters": "Show chapters",
    "action.chapter.addNamed": "Add named chapter",
    "transport.chapter.previous": "Previous chapter",
    "transport.chapter.next": "Next chapter",
    "view.history.back": "Back in view history",
    "view.history.forward": "Forward in view history",
    "transport.history.older": "Older playback-history item",
    "transport.history.newer": "Newer playback-history item",
    "audio.clip.markStart": "Mark clip start",
    "audio.clip.markEnd": "Mark clip end",
    "audio.clip.jumpStart": "Jump to clip start",
    "audio.clip.jumpEnd": "Jump to clip end",
    "audio.clip.previousBoundary": "Previous clip boundary",
    "audio.clip.nextBoundary": "Next clip boundary",
    "audio.clip.export": "Export selected clip",
    "audio.clip.appendToFile": "Append selected clip to a file",
    "editing.clip.removeFromOriginal": "Remove selected clip from the original",
    "audio.clip.clear": "Clear clip selection",
    "seek.back10": "Seek back 10 seconds",
    "seek.forward10": "Seek forward 10 seconds",
    "seek.back30": "Seek back 30 seconds",
    "seek.forward30": "Seek forward 30 seconds",
    "seek.back60": "Seek back 60 seconds",
    "seek.forward60": "Seek forward 60 seconds",
    "seek.backCustom": "Seek back by the custom interval",
    "seek.forwardCustom": "Seek forward by the custom interval",
    "session.enable": "Enable a session",
    "session.disableCurrent": "Disable the current session",
    "session.configureCurrent": "Configure the current session",
    "session.previous": "Previous session",
    "session.next": "Next session",
    "audio.selectOutput": "Select audio output",
    "audio.previousOutput": "Previous audio output",
    "audio.nextOutput": "Next audio output",
    "transport.trackStart": "Go to the start of the track",
    "transport.trackEnd": "Go to the end of the track",
    "session.options": "Session settings",
    "profile.backup.export": "Export AMC data",
    "profile.backup.import": "Import AMC data",
    "profile.commonData.refresh": "Refresh shared AMC data",
    "profile.commonData.status": "Shared AMC data status",
    "podcasts.source.add": "Add a podcast, channel, Facebook Page, or online source",
    "podcasts.opml.import": "Import podcasts from OPML",
    "podcasts.opml.export": "Export podcasts to OPML",
    "podcasts.youtube.export": "Export YouTube channels and playlists",
    "podcasts.refresh.current": "Refresh the selected podcast, channel, or Facebook Page",
    "podcasts.refresh.all": "Refresh all podcast and Facebook sources",
    "podcasts.download.selected": "Download selected episodes or video audio as MP3",
    "podcasts.download.saveAs": "Save the episode as a file",
    "podcasts.youtube.video.download": "Download selected YouTube, Facebook or Instagram videos",
    "podcasts.youtube.video.saveAs": "Save one internet video as a file",
    "podcasts.goToRelated": "Go to the related podcast",
    "podcasts.library.toggle": "Add to or remove from the Podcast Library",
    "podcasts.playbackOptions": "Podcast playback options",
    "podcasts.library.sort.added": "Sort the Podcast Library by date added",
    "podcasts.library.sort.alphabetical": "Sort the Podcast Library alphabetically",
    "podcasts.library.sort.custom": "Use custom Podcast Library order",
    "podcasts.inbox.sort.added": "Sort new episodes by date added",
    "podcasts.inbox.sort.alphabetical": "Sort new episodes alphabetically",
    "podcasts.inbox.sort.byPodcast": "Sort new episodes by podcast",
    "youtube.broadcasts.open": "Show channel broadcasts",
    "youtube.broadcasts.refresh": "Refresh channel broadcasts",
    "youtube.broadcasts.aggregate.open": "Show broadcasts from selected channels",
    "youtube.broadcasts.aggregate.configure": "Choose channels for the combined broadcast list",
    "youtube.broadcasts.aggregate.channel.toggle": "Include the channel in the combined broadcast list",
    "youtube.broadcasts.aggregate.refresh": "Refresh broadcasts from selected channels",
    "youtube.broadcasts.aggregate.recordNow": "Start or stop recording the YouTube broadcast",
    "youtube.broadcasts.aggregate.schedule": "Schedule a YouTube broadcast recording",
    "youtube.liveStation.add": "Add a dynamic YouTube live station",
    "tidal.library.toggle": "Add to or remove from the TIDAL Library",
    "tidal.favorite.toggle": "Add to or remove from TIDAL favorites",
    "tidal.collection.sort.added": "Sort the TIDAL collection by date added",
    "tidal.collection.sort.alphabetical": "Sort the TIDAL collection alphabetically",
    "tidal.collection.sort.custom": "Use custom TIDAL collection order",
    "spotify.library.toggle": "Add to or remove from the Spotify Library",
    "spotify.favorite.toggle": "Add to or remove from Spotify favorites",
    "spotify.collection.sort.added": "Sort Spotify by date added",
    "spotify.collection.sort.alphabetical": "Sort Spotify alphabetically",
    "spotify.collection.sort.catalog": "Use Spotify catalog order",
    "spotify.view.liked": "Show liked Spotify tracks",
    "spotify.view.albums": "Show Spotify albums",
    "spotify.view.artists": "Show Spotify artists",
    "spotify.view.playlists": "Show Spotify playlists",
    "spotify.view.podcasts": "Show Spotify podcasts",
    "spotify.view.episodes": "Show Spotify episodes",
    "podcasts.favorite.toggle": "Add to or remove from Podcast favorites",
    "wiim.networkStreams.view": "Show WiiM network streams",
    "wiim.input.select": "Select a WiiM input",
    "wiim.output.select": "Select a WiiM output",
    "wiim.equalizer.select": "Select a WiiM equalizer preset",
    "wiim.repeat.select": "Select the WiiM repeat mode",
    "wiim.shuffle.toggle": "Toggle WiiM shuffle",
    "wiim.sleepTimer.set": "Set the WiiM sleep timer",
    "wiim.source.previous": "Previous WiiM source",
    "wiim.source.next": "Next WiiM source",
    "wiim.networkStreams.add": "Add a WiiM network stream",
    "wiim.networkStreams.edit": "Edit a WiiM network stream",
    "wiim.networkStreams.remove": "Remove a WiiM network stream",
    "wiim.networkStreams.import": "Import WiiM network streams",
    "wiim.networkStreams.export": "Export WiiM network streams",
    "wiim.networkStreams.sort.added": "Sort WiiM network streams by date added",
    "wiim.networkStreams.sort.alphabetical": "Sort WiiM network streams alphabetically",
    "wiim.networkStreams.sort.custom": "Use custom WiiM network stream order",
    "wiim.networkStreams.moveUp": "Move the WiiM network stream up",
    "wiim.networkStreams.moveDown": "Move the WiiM network stream down",
    "wiim.networkStreams.cut": "Cut the WiiM network stream",
    "wiim.networkStreams.paste": "Paste the WiiM network stream",
    "sonos.ownStreams.add": "Add a Sonos stream",
    "sonos.ownStreams.edit": "Edit a Sonos stream",
    "sonos.ownStreams.remove": "Remove a Sonos stream",
    "sonos.ownStreams.import": "Import Sonos streams",
    "sonos.ownStreams.export": "Export Sonos streams",
    "sonos.ownStreams.sort.added": "Sort Sonos streams by date added",
    "sonos.ownStreams.sort.alphabetical": "Sort Sonos streams alphabetically",
    "sonos.ownStreams.sort.custom": "Use custom Sonos stream order",
    "sonos.ownStreams.moveUp": "Move the Sonos stream up",
    "sonos.ownStreams.moveDown": "Move the Sonos stream down",
    "sonos.ownStreams.cut": "Cut the Sonos stream",
    "sonos.ownStreams.paste": "Paste the Sonos stream",
    "sonos.target.select": "Select a Sonos group",
    "frontierSmart.mode.select": "Select Frontier Smart mode",
    "radioarchiwista.favorite.toggle": "Toggle favorite recording",
    "radioarchiwista.download.selected": "Download the selected recording",
    "radioarchiwista.catalog.refresh": "Refresh the Radio Archiwista catalog",
}


# Static labels and accessibility text which do not have a command/event ID.
# Commands and messages deliberately use their stronger stable models above.
_UI_TEXT_EN: dict[str, str] = {
    "&Wyświetlaj po uruchomieniu:": "Show at &startup:",
    # Radio Archiwista: komunikaty sesji ida przez stabilne klucze rarch.*
    # w _CATALOGS (odmiana liczebnikow), nie przez statyczne literaly.
    "Wyświetlaj po uruchomieniu": "Show at startup",
    "Ostatnia używana sesja": "Last used session",
    "Menedżer sesji": "Session manager",
    "&Wstrzymuj odtwarzanie po wyjściu z odtwarzacza": "&Pause playback after leaving the player",
    "Wstrzymuj odtwarzanie po wyjściu z odtwarzacza": "Pause playback after leaving the player",
    "Po wyjściu ustaw &fokus na aktualnie odtwarzanym elemencie": "After leaving, &focus the currently playing item",
    "Po wyjściu ustaw fokus na aktualnie odtwarzanym elemencie": "After leaving, focus the currently playing item",
    "Domyślnie po uruchomieniu presetu &otwieraj odtwarzacz": "By default, &open the player after starting a preset",
    "Domyślnie po uruchomieniu presetu otwieraj odtwarzacz": "By default, open the player after starting a preset",
    "Domyślnie pozostawaj na li&ście po uruchomieniu materiału Enterem": "By default, stay on the li&st after starting an item with Enter",
    "Domyślnie pozostawaj na liście po uruchomieniu materiału Enterem": "By default, stay on the list after starting an item with Enter",
    "Po odłączeniu wybranego urządzenia przejdź na domyślne, ale &wstrzymaj dźwięk": "Use the default device after disconnection, but &pause audio",
    "Po odłączeniu wybranego urządzenia przejdź na domyślne, ale wstrzymaj dźwięk": "Use the default device after disconnection, but pause audio",
    "Zachowuj &kopie po edycji": "Keep &backups after editing",
    "Zachowuj kopie po edycji": "Keep backups after editing",
    "Wspólne podglądy &nagrywania i nowych odcinków we wszystkich sesjach": "Show &recording and new-episode previews in every session",
    "Wspólne podglądy nagrywania i nowych odcinków we wszystkich sesjach": "Show recording and new-episode previews in every session",
    "Pamiętaj pozycję odtwarzania lokalnych &plików": "Remember local &file playback positions",
    "Pamiętaj pozycję odtwarzania lokalnych plików": "Remember local file playback positions",
    "Włączaniem i kolejnością sesji zarządza Menedżer sesji pod Ctrl+Shift+S.": "Use Ctrl+Shift+S to manage enabled sessions and their order.",
    "Włączaniem i kolejnością sesji zarządza Menedżer sesji pod Control Shift S": "Use Control Shift S to manage enabled sessions and their order",
    "Dłuższy przeskok pod Ctrl+Alt+strzałką w &lewo i w prawo:": "Long seek with Ctrl+Alt+&Left or Right:",
    "Długość przeskoku pod Ctrl+Alt+strzałką w lewo i w prawo": "Ctrl+Alt+Left or Right seek length",
    "Przetwarzanie dźwięku": "Audio processing",
    "&Normalizuj odczuwaną głośność utworów": "&Normalize perceived track loudness",
    "Normalizuj odczuwaną głośność utworów": "Normalize perceived track loudness",
    "&Łagodnie przechodź między utworami": "Use &smooth transitions between tracks",
    "Łagodnie przechodź między utworami": "Use smooth transitions between tracks",
    "Dodatkowa &cisza po naturalnym końcu utworu:": "Additional &silence after a track ends naturally:",
    "Dodatkowa cisza po naturalnym końcu utworu": "Additional silence after a track ends naturally",
    "Sposób przeliczania zmienionego &tempa:": "Changed playback &tempo method:",
    "Sposób przeliczania zmienionego tempa": "Changed playback tempo method",
    "Zapisywanie nagrań": "Saving recordings",
    "Sekcja zapisywanie nagrań radia": "Radio recording storage section",
    "&Położenie nagrań Radia:": "Radio recording &location:",
    "Położenie nagrań Radia": "Radio recording location",
    "Osobny folder nagrywania Radia": "Separate Radio recording folder",
    "Ten sam folder co pobieranie Podcastów": "Same folder as Podcast downloads",
    "Twórz osobny folder dla &każdej stacji radiowej": "Create a separate folder for each &radio station",
    "Twórz osobny folder dla każdej stacji radiowej": "Create a separate folder for each radio station",
    "Twórz osobny folder dla &każdego podcastu lub kanału": "Create a separate folder for each &podcast or channel",
    "Twórz osobny folder dla każdego podcastu lub kanału": "Create a separate folder for each podcast or channel",
    "Osobny &folder nagrywania radia:": "Separate Radio recording &folder:",
    "Domyślny folder nagrywania radia": "Default Radio recording folder",
    "&Wybierz…": "&Choose…",
    "Wybierz domyślny folder nagrywania radia": "Choose the default Radio recording folder",
    "Użyj folderu &domyślnego AMC": "Use the AMC &default folder",
    "Użyj domyślnego folderu nagrywania AMC": "Use the default AMC recording folder",
    "&Format pliku:": "File &format:",
    "Format nagrania radia": "Radio recording format",
    "&Bitrate nagrania MP3 lub AAC:": "MP3 or AAC recording &bitrate:",
    "Bitrate nagrania MP3 lub AAC": "MP3 or AAC recording bitrate",
    "Transmisja i rozpoznawanie utworów": "Broadcast and track recognition",
    "Sekcja transmisja i rozpoznawanie utworów": "Broadcast and track recognition section",
    "&Długość bufora transmisji:": "Broadcast buffer &length:",
    "Długość bufora transmisji": "Broadcast buffer length",
    "&Obserwuj i automatycznie rozpoznawaj utwory": "&Monitor and automatically recognize tracks",
    "Obserwuj i automatycznie rozpoznawaj utwory": "Monitor and automatically recognize tracks",
    "&Zakres automatycznego rozpoznawania:": "Automatic recognition &scope:",
    "Zakres automatycznego rozpoznawania utworów": "Automatic track recognition scope",
    "Domyślna &usługa rozpoznanych utworów:": "Default recognized-track &service:",
    "Domyślna usługa dla rozpoznanych utworów": "Default service for recognized tracks",
    "Harmonogram nagrywania": "Recording schedule",
    "Sekcja harmonogram nagrywania radia": "Radio recording schedule section",
    "W nowym harmonogramie podpowiadaj &folder stacji, gdy stacja go ma": "Suggest the station &folder in a new schedule when available",
    "W nowym harmonogramie podpowiadaj folder stacji, gdy stacja go ma": "Suggest the station folder in a new schedule when available",
    "Domyślnie &wybudzaj komputer przed zaplanowanym nagraniem": "&Wake the computer before a scheduled recording by default",
    "Domyślnie wybudzaj komputer przed zaplanowanym nagraniem": "Wake the computer before a scheduled recording by default",
    "Zapisywanie pobranych materiałów": "Saving downloaded media",
    "Sekcja zapisywanie pobranych podcastów i materiałów YouTube": "Podcast and YouTube download storage section",
    "Domyślny &folder zapisu podcastów i dźwięku YouTube:": "Default folder for podcasts and YouTube &audio:",
    "Domyślny folder zapisu pobranych podcastów i dźwięku YouTube": "Default folder for downloaded podcasts and YouTube audio",
    "Wybierz domyślny folder zapisu podcastów i dźwięku YouTube": "Choose the default folder for podcasts and YouTube audio",
    "Użyj domyślnego folderu zapisu podcastów i dźwięku YouTube w AMC": "Use the default AMC folder for podcasts and YouTube audio",
    "Format pobieranego &wideo internetowego:": "Downloaded internet video &format:",
    "Format pobieranego wideo internetowego": "Downloaded internet video format",
    "Automatyczne odświeżanie": "Automatic refresh",
    "Sekcja automatyczne odświeżanie podcastów i kanałów YouTube": "Podcast and YouTube automatic refresh section",
    "Odstęp dla &podcastów w minutach:": "&Podcast interval in minutes:",
    "Odstęp automatycznego sprawdzania podcastów w minutach; zero wyłącza": "Automatic podcast check interval in minutes; zero disables it",
    "Odstęp dla kanałów &YouTube w minutach:": "&YouTube channel interval in minutes:",
    "Odstęp automatycznego sprawdzania kanałów YouTube w minutach; zero wyłącza": "Automatic YouTube channel check interval in minutes; zero disables it",
    "Liczba źródeł sprawdzanych w &jednym przebiegu:": "Sources checked in one &run:",
    "Liczba źródeł sprawdzanych w jednym przebiegu": "Sources checked in one run",
    "Pokazuj &szczegółowe podpowiedzi klawiatury przy polach i listach": "Show &detailed keyboard hints for fields and lists",
    "Pokazuj szczegółowe podpowiedzi klawiatury przy polach i listach": "Show detailed keyboard hints for fields and lists",
    "&Włącz komunikaty dostępności": "&Enable accessibility messages",
    "Włącz komunikaty dostępności": "Enable accessibility messages",
    "Włącz automatyczne komunikaty &odtwarzacza": "Enable automatic &player messages",
    "Włącz automatyczne komunikaty odtwarzacza": "Enable automatic player messages",
    "Oznajmiaj przewijanie &strzałkami oraz klawiszami Home i End": "Announce seeking with &arrow, Home, and End keys",
    "Oznajmiaj przewijanie strzałkami oraz klawiszami Home i End": "Announce seeking with arrow, Home, and End keys",
    "Oznajmiaj skoki klawiszami &cyfr": "Announce jumps with &number keys",
    "Oznajmiaj skoki klawiszami cyfr": "Announce jumps with number keys",
    "Oznajmiaj zmiany &głośności": "Announce &volume changes",
    "Oznajmiaj zmiany głośności": "Announce volume changes",
    "Oznajmiaj odtwarzanie i pau&zę": "Announce play and pau&se",
    "Oznajmiaj odtwarzanie i pauzę": "Announce play and pause",
    "Oznajmiaj nawigację po &zakładkach": "Announce &bookmark navigation",
    "Oznajmiaj nawigację po zakładkach": "Announce bookmark navigation",
    "Przy skoku cyfrą &podawaj:": "For number-key jumps, announce:",
    "Treść komunikatu po skoku klawiszem cyfry": "Number-key jump message content",
    "Oznajmiaj automatycznie rozpoznane utwory": "Announce automatically recognized tracks",
    "&Oznajmiaj automatycznie rozpoznane utwory": "&Announce automatically recognized tracks",
    "Komunikaty stanu stacji radiowej": "Radio station status messages",
    "Krótki &dźwięk dla odtwarzanej stacji": "Short &sound for the playing station",
    "Krótki dźwięk dla odtwarzanej stacji": "Short sound for the playing station",
    "Krótki &dźwięk dla nagrywanej stacji": "Short s&ound for the recording station",
    "Krótki dźwięk dla nagrywanej stacji": "Short sound for the recording station",
    "Stan &odtwarzania przy nazwie stacji:": "&Playback status by the station name:",
    "Stan odtwarzania przy nazwie stacji": "Playback status by the station name",
    "Stan &nagrywania przy nazwie stacji:": "&Recording status by the station name:",
    "Stan nagrywania przy nazwie stacji": "Recording status by the station name",
    "&Filtruj polecenia:": "&Filter commands:",
    "Filtruj polecenia klawiatury": "Filter keyboard commands",
    "&Polecenia i przypisane skróty:": "&Commands and assigned shortcuts:",
    "Polecenia i przypisane skróty": "Commands and assigned shortcuts",
    "&Nowy skrót dla wybranego polecenia:": "&New shortcut for the selected command:",
    "Nowy skrót dla wybranego polecenia": "New shortcut for the selected command",
    "Brak wybranego polecenia": "No command selected",
    "&Usuń skrót": "&Remove shortcut",
    "Usuń skrót wybranego polecenia": "Remove the selected command shortcut",
    "Przywróć &domyślny": "Restore &default",
    "Przywróć domyślny skrót wybranego polecenia": "Restore the selected command's default shortcut",
    "Przywróć &wszystkie domyślne": "Restore &all defaults",
    "Przywróć wszystkie domyślne skróty": "Restore all default shortcuts",
    "Zmiany skrótów oczekują na zapisanie ustawień.": "Shortcut changes are waiting for the settings to be saved.",
    "Aktualizacje": "Updates",
    "&Kanał aktualizacji programu:": "Application update &channel:",
    "Kanał aktualizacji programu": "Application update channel",
    "Automatycznie &sprawdzaj dostępność nowej wersji AMC po uruchomieniu programu": "Automatically &check for a new AMC version when the application starts",
    "Automatycznie sprawdzaj dostępność nowej wersji AMC po uruchomieniu programu": "Automatically check for a new AMC version when the application starts",
    "Automatycznie &sprawdzaj aktualizacje składników FFmpeg i yt-dlp przy uruchamianiu AMC": "Automatically &check FFmpeg and yt-dlp updates when AMC starts",
    "Automatycznie sprawdzaj aktualizacje składników FFmpeg i yt-dlp przy uruchamianiu AMC": "Automatically check FFmpeg and yt-dlp updates when AMC starts",
    "Automatycznie &pobieraj i instaluj zweryfikowane aktualizacje składników": "Automatically &download and install verified component updates",
    "Automatycznie pobieraj i instaluj zweryfikowane aktualizacje składników": "Automatically download and install verified component updates",
    "Stan składnika &FFmpeg:": "&FFmpeg component status:",
    "Stan składnika FFmpeg": "FFmpeg component status",
    "Stan składnika &yt-dlp:": "&yt-dlp component status:",
    "Stan składnika yt-dlp": "yt-dlp component status",
    "Sprawdź, &pobierz i zweryfikuj składniki FFmpeg i yt-dlp": "Check, &download, and verify FFmpeg and yt-dlp",
    "Sprawdź, pobierz i zweryfikuj składniki FFmpeg i yt-dlp": "Check, download, and verify FFmpeg and yt-dlp",
    "Integracja z Windows": "Windows integration",
    "Udostępniaj AMC dla następujących &typów plików:": "Make AMC available for these &file types:",
    "Udostępniaj AMC dla następujących typów plików": "Make AMC available for these file types",
    "Otwórz ustawienia aplikacji &domyślnych Windows…": "Open Windows &default app settings…",
    "Otwórz ustawienia aplikacji domyślnych Windows": "Open Windows default app settings",
    "Nie zapisano ustawień": "Settings were not saved",
    "Nie zmieniono skrótu": "Shortcut was not changed",
    "Zapisano ustawienia": "Settings saved",
    "Polecenie niedostępne": "Command unavailable",
    "Wstrzymano": "Paused",
    "Odtwarzanie": "Playing",
    "Najlepsza czytelność mowy": "Best speech clarity",
    "Najlepsza jakość muzyki": "Best music quality",
    "Tryb zgodności": "Compatibility mode",
    "bez dodatkowej ciszy": "no additional silence",
    "pół sekundy": "half a second",
    "1 sekunda": "1 second",
    "2 sekundy": "2 seconds",
    "3 sekundy": "3 seconds",
    "5 sekund": "5 seconds",
    "Nie odczytuj": "Do not announce",
    "Przed nazwą stacji": "Before the station name",
    "Po nazwie stacji": "After the station name",
    "Tylko procent": "Percentage only",
    "Tylko czas": "Time only",
    "Procent i czas": "Percentage and time",
    "Pliki lokalne": "Local files",
    "Stacje radiowe": "Radio stations",
    "Podcasty i YouTube": "Podcasts and YouTube",
    "Urządzenia WiiM": "WiiM devices",
    "Urządzenia Frontier Smart": "Frontier Smart devices",
    "Grupy Sonos": "Sonos groups",
    "&Filtruj liste:": "&Filter the list:",
    "Filtruj liste": "Filter the list",
    "Nic nie jest odtwarzane": "Nothing is playing",
    "Teraz odtwarzane": "Now playing",
    "0 s / nieznany": "0 s / unknown",
    "Czas": "Time",
    "&Odtwórz": "&Play",
    "&Wstrzymaj": "&Pause",
    "&Anuluj": "&Cancel",
    "Głośność w procentach": "Volume in percent",
    "Prędkość odtwarzania": "Playback speed",
    "&Głośność:": "&Volume:",
    "&Prędkość:": "&Speed:",
    "Gotowe": "Ready",
    "Komunikaty": "Messages",
    "Długość przeskoku musi być podana w minutach albo sekundach, na przykład: 5 minut, 90 sekund lub 1:30. Dopuszczalny zakres to od 5 sekund do 30 minut.": "Enter the seek length in minutes or seconds, for example: 5 minutes, 90 seconds, or 1:30. The allowed range is 5 seconds to 30 minutes.",
    "Instrukcja zmiany skrótów. Zmiany działają dopiero po zapisaniu ustawień": "Shortcut editing instructions. Changes take effect after saving the settings",
    "Kanał wynika z zainstalowanej wersji AMC i nie jest zmieniany w tym oknie.": "The channel is determined by the installed AMC edition and cannot be changed here.",
    "Gdy nowa wersja jest dostępna, AMC pokaże jej opis i zaproponuje pobranie. Bez zgody niczego nie pobiera ani nie instaluje.": "When a new version is available, AMC shows its description and offers to download it. Nothing is downloaded or installed without your approval.",
    "Naciśnij nową kombinację. Tab przechodzi dalej, a Escape anuluje ustawienia.": "Press the new key combination. Tab moves on and Escape cancels the settings.",
    "Sprawdzanie odbywa się najwyżej raz na 24 godziny.": "The check runs at most once every 24 hours.",
    "Stan zmiany skrótu. Zmiany oczekują na zapisanie ustawień": "Shortcut change status. Changes are waiting to be saved",
    "Szablony komunikatów dostępności": "Accessibility message templates",
    "To samo polecenie jest dostępne w menu Narzędzia i w palecie poleceń.": "The same command is available from the Tools menu and the command palette.",
    "To ustawienie działa podczas automatycznego sprawdzania przy uruchamianiu AMC.": "This setting applies during the automatic check when AMC starts.",
    "Wybierz polecenie, przejdź do pola nowego skrótu i naciśnij kombinację. Zmiana jest widoczna od razu na tej karcie, ale zacznie działać dopiero po wybraniu Zapisz.": "Choose a command, move to the new shortcut field, and press the key combination. The change appears on this page immediately but takes effect only after you choose Save.",
    "Wyłączenie zatrzymuje rutynową mowę AMC, ale nadal pozostawia tekst w pasku stanu i nie ukrywa komunikatów o błędach.": "Turning this off stops routine AMC speech, but text remains in the status bar and error messages are still shown.",
    "0,50 razy — wolniej": "0.50x — slower",
    "0,75 razy — wolniej": "0.75x — slower",
    "1,00 razy — normalna prędkość": "1.00x — normal speed",
    "1,25 razy — szybciej": "1.25x — faster",
    "1,50 razy — szybciej": "1.50x — faster",
    "1,75 razy — szybciej": "1.75x — faster",
    "2,00 razy — szybciej": "2.00x — faster",
    "Poniedziałek": "Monday",
    "Wtorek": "Tuesday",
    "Środa": "Wednesday",
    "Czwartek": "Thursday",
    "Piątek": "Friday",
    "Sobota": "Saturday",
    "Niedziela": "Sunday",
    "Jednorazowo": "Once",
    "Codziennie": "Daily",
    "W wybrane dni tygodnia": "On selected weekdays",
    "Oryginalny strumień bez konwersji": "Original stream without conversion",
    "zaznaczona": "selected",
    "niezaznaczona": "not selected",
    "stan mieszany": "mixed state",
    "Plan nagrywania": "Recording schedule",
}


_LEGACY_PLACEHOLDER = re.compile(r"\{(arg\d+)\}")


def _load_legacy_english_catalog() -> tuple[dict[str, str], dict[str, str]]:
    path = Path(__file__).with_name("locales") / "en_legacy.json"
    try:
        payload = json.loads(path.read_text(encoding="utf-8"))
    except (OSError, ValueError, TypeError):
        # A missing optional catalog must never expose a technical key or stop
        # AMC from starting.  Polish source text remains the safe fallback.
        return {}, {}

    def clean(section: str) -> dict[str, str]:
        raw = payload.get(section, {}) if isinstance(payload, dict) else {}
        if not isinstance(raw, dict):
            return {}
        return {
            source: target
            for source, target in raw.items()
            if isinstance(source, str)
            and source
            and isinstance(target, str)
            and target
        }

    return clean("static"), clean("templates")


def _compile_legacy_templates(
    templates: Mapping[str, str],
) -> tuple[tuple[re.Pattern[str], str], ...]:
    compiled: list[tuple[int, int, re.Pattern[str], str]] = []
    for source, target in templates.items():
        parts: list[str] = []
        position = 0
        placeholders: set[str] = set()
        for match in _LEGACY_PLACEHOLDER.finditer(source):
            parts.append(re.escape(source[position:match.start()]))
            name = match.group(1)
            if name in placeholders:
                parts.append(f"(?P={name})")
            else:
                parts.append(f"(?P<{name}>.*?)")
                placeholders.add(name)
            position = match.end()
        parts.append(re.escape(source[position:]))
        if not placeholders:
            continue
        pattern = re.compile("^" + "".join(parts) + "$", re.DOTALL)
        literal_size = len(_LEGACY_PLACEHOLDER.sub("", source))
        compiled.append((literal_size, len(placeholders), pattern, target))
    compiled.sort(key=lambda item: (-item[0], item[1], item[2].pattern))
    return tuple((pattern, target) for _size, _count, pattern, target in compiled)


_LEGACY_UI_TEXT_EN, _LEGACY_UI_TEMPLATES_EN = _load_legacy_english_catalog()
_COMPILED_LEGACY_TEMPLATES_EN = _compile_legacy_templates(
    _LEGACY_UI_TEMPLATES_EN
)
_STABLE_UI_TEXT_EN = {
    _CATALOGS[POLISH][key]: english
    for key, english in _CATALOGS[ENGLISH].items()
}


def _english_to_polish_exact_catalog() -> dict[str, str]:
    """Build a safe reverse catalog for an already localized window."""

    reverse: dict[str, str] = {}
    for key, english in _CATALOGS[ENGLISH].items():
        reverse.setdefault(english, _CATALOGS[POLISH][key])
    for polish, english in _UI_TEXT_EN.items():
        reverse.setdefault(english, polish)
    for polish, english in _LEGACY_UI_TEXT_EN.items():
        reverse.setdefault(english, polish)
    return reverse


_UI_TEXT_PL_FROM_EN = _english_to_polish_exact_catalog()
_LEGACY_UI_TEMPLATES_PL_FROM_EN = {
    english: polish
    for polish, english in _LEGACY_UI_TEMPLATES_EN.items()
}
_COMPILED_LEGACY_TEMPLATES_PL_FROM_EN = _compile_legacy_templates(
    _LEGACY_UI_TEMPLATES_PL_FROM_EN
)


def legacy_english_catalog() -> Mapping[str, Mapping[str, str]]:
    """Read-only-by-convention catalog view for completeness tests."""

    return {
        "static": _LEGACY_UI_TEXT_EN,
        "templates": _LEGACY_UI_TEMPLATES_EN,
    }


def _english_exact_text(text: str) -> str:
    return _STABLE_UI_TEXT_EN.get(
        text,
        _UI_TEXT_EN.get(text, _LEGACY_UI_TEXT_EN.get(text, text)),
    )


def _polish_exact_text(text: str) -> str:
    return _UI_TEXT_PL_FROM_EN.get(text, text)


def translate_ui_text(
    text: str,
    *,
    source_language: str,
    target_language: str,
) -> str:
    """Translate intentional UI text between complete AMC catalogs.

    This bidirectional path is used only for controls that are already open.
    Runtime list data remains outside it unless a caller explicitly opts in.
    """

    if (
        not isinstance(text, str)
        or source_language == target_language
        or source_language not in SUPPORTED_LANGUAGES
        or target_language not in SUPPORTED_LANGUAGES
    ):
        return text
    if source_language == POLISH and target_language == ENGLISH:
        exact = _english_exact_text
        templates = _COMPILED_LEGACY_TEMPLATES_EN
    elif source_language == ENGLISH and target_language == POLISH:
        exact = _polish_exact_text
        templates = _COMPILED_LEGACY_TEMPLATES_PL_FROM_EN
    else:
        return text
    translated = exact(text)
    if translated != text:
        return translated
    for pattern, template in templates:
        match = pattern.fullmatch(text)
        if match is not None:
            try:
                values = {
                    name: exact(value)
                    for name, value in match.groupdict().items()
                }
                return template.format(**values)
            except (KeyError, ValueError):
                break
    return text


def ui_text(text: str) -> str:
    """Translate an intentional Polish UI text without treating it as an ID."""

    return translate_ui_text(
        text,
        source_language=POLISH,
        target_language=_active_language,
    )


def _window_is_being_deleted(window: object) -> bool:
    """Return whether a native window is already pending destruction."""

    is_being_deleted = getattr(window, "IsBeingDeleted", None)
    if not callable(is_being_deleted):
        return False
    try:
        return bool(is_being_deleted())
    except (RuntimeError, TypeError, AttributeError, AssertionError):
        # A stale wx wrapper is no longer safe to inspect or mutate.
        return True


def _is_text_entry(window: object) -> bool:
    """Recognize wx text-entry controls without importing the GUI toolkit."""

    return callable(getattr(window, "GetValue", None)) and callable(
        getattr(window, "ChangeValue", None)
    )


def localize_window_tree(
    window: object,
    *,
    translate_items: bool = False,
    source_language: str = POLISH,
) -> None:
    """Localize native labels, accessible names, help, and list choices.

    Values of text-entry controls are deliberately untouched: they may contain
    paths, user text, addresses, or technical details explicitly requested by
    the user.
    """

    if (
        _active_language == source_language
        or window is None
        or _window_is_being_deleted(window)
    ):
        return
    text_entry = _is_text_entry(window)
    for getter_name, setter_name in (
        ("GetLabel", "SetLabel"),
        ("GetName", "SetName"),
        ("GetHelpText", "SetHelpText"),
    ):
        # wx.TextCtrl.GetLabel exposes its editable value, while SetLabel is
        # invalid for that control.  Values entered by the user are never UI
        # labels; only the explicit accessible name and help text are localised.
        if text_entry and getter_name == "GetLabel":
            continue
        getter = getattr(window, getter_name, None)
        setter = getattr(window, setter_name, None)
        if callable(getter) and callable(setter):
            try:
                current = getter()
                translated = translate_ui_text(
                    current,
                    source_language=source_language,
                    target_language=_active_language,
                )
                if translated != current:
                    setter(translated)
            except (RuntimeError, TypeError, AttributeError, AssertionError):
                pass
    # List items can be user data (a title, station, path, or remote device).
    # They are translated only for a control whose choices are known to be
    # static UI.  Labels, names, and help remain safe to translate globally.
    get_count = getattr(window, "GetCount", None)
    get_string = getattr(window, "GetString", None)
    set_string = getattr(window, "SetString", None)
    if (
        translate_items
        and callable(get_count)
        and callable(get_string)
        and callable(set_string)
    ):
        try:
            for index in range(int(get_count())):
                current = get_string(index)
                translated = translate_ui_text(
                    current,
                    source_language=source_language,
                    target_language=_active_language,
                )
                if translated != current:
                    set_string(index, translated)
        except (RuntimeError, TypeError, AttributeError, AssertionError):
            pass
    get_children = getattr(window, "GetChildren", None)
    if callable(get_children):
        try:
            children = tuple(get_children())
        except (RuntimeError, TypeError, AttributeError, AssertionError):
            children = ()
        for child in children:
            localize_window_tree(
                child,
                translate_items=translate_items,
                source_language=source_language,
            )


def reconfigure_window_tree(
    window: object,
    preference: object,
    *,
    system_locale: str | None = None,
    translate_items: bool = False,
) -> str:
    """Apply a saved language immediately to an already open window tree."""

    previous_language = active_language()
    current_language = configure(preference, system_locale=system_locale)
    if current_language != previous_language:
        localize_window_tree(
            window,
            translate_items=translate_items,
            source_language=previous_language,
        )
    return current_language


_TOKEN_WORDS = {
    "action": "",
    "activate": "Open or play",
    "add": "Add",
    "added": "date added",
    "aggregate": "combined",
    "all": "all",
    "alphabetical": "alphabetical",
    "append": "append",
    "assign": "assign",
    "audio": "audio",
    "back": "back",
    "bookmark": "bookmark",
    "broadcasts": "broadcasts",
    "chapter": "chapter",
    "check": "check",
    "clear": "clear",
    "clipboard": "clipboard",
    "close": "close",
    "collection": "collection",
    "common": "shared",
    "configure": "configure",
    "copy": "copy",
    "current": "current",
    "custom": "custom order",
    "cut": "cut",
    "delete": "delete",
    "disable": "disable",
    "down": "down",
    "download": "download",
    "edit": "edit",
    "enable": "enable",
    "episodes": "episodes",
    "export": "export",
    "favorite": "favorite",
    "files": "files",
    "folder": "folder",
    "forward": "forward",
    "history": "history",
    "import": "import",
    "information": "information",
    "input": "input",
    "jump": "jump",
    "library": "library",
    "liked": "liked tracks",
    "list": "list",
    "manage": "manage",
    "mark": "mark",
    "membership": "membership",
    "move": "move",
    "named": "named",
    "network": "network",
    "next": "next",
    "open": "open",
    "output": "output",
    "parent": "Parent folder",
    "paste": "paste",
    "pause": "pause",
    "playback": "playback",
    "playlist": "playlist",
    "playlists": "playlists",
    "podcasts": "podcasts",
    "previous": "previous",
    "queue": "queue",
    "radio": "radio",
    "recognition": "recognition",
    "recording": "recording",
    "refresh": "refresh",
    "relations": "related items",
    "remove": "remove",
    "rename": "rename",
    "repeat": "repeat",
    "report": "report",
    "save": "save",
    "schedule": "schedule",
    "search": "search",
    "seek": "seek",
    "select": "select",
    "session": "session",
    "settings": "settings",
    "shuffle": "shuffle",
    "sort": "sort",
    "source": "source",
    "start": "start",
    "station": "station",
    "status": "status",
    "stop": "stop",
    "streams": "streams",
    "tidal": "TIDAL",
    "toggle": "toggle",
    "track": "track",
    "transport": "playback",
    "up": "up",
    "update": "update",
    "view": "show",
    "volume": "volume",
    "wiim": "WiiM",
    "youtube": "YouTube",
}


def english_action_name(action_value: str) -> str:
    """Return an intentional English label for every stable Action value.

    Overrides handle idiomatic names.  The deterministic fallback is made of
    user-facing words and is covered by tests; it never returns the dotted or
    camel-cased internal identifier.
    """

    if action_value in _ACTION_NAME_OVERRIDES:
        return _ACTION_NAME_OVERRIDES[action_value]
    volume_match = re.fullmatch(r"volume\.(up|down)(\d+)", action_value)
    if volume_match:
        direction, amount = volume_match.groups()
        verb = "Increase" if direction == "up" else "Decrease"
        return f"{verb} volume by {amount} percent"
    seek_match = re.fullmatch(r"seek\.percent\.(\d+)", action_value)
    if seek_match:
        return f"Go to {seek_match.group(1)} percent"
    raw_tokens: list[str] = []
    for part in action_value.split("."):
        raw_tokens.extend(_CAMEL_BOUNDARY.sub(" ", part).split())
    words = [_TOKEN_WORDS.get(token.casefold(), token.casefold()) for token in raw_tokens]
    words = [word for word in words if word]
    if not words:
        return "AMC command"
    text = " ".join(words)
    return text[:1].upper() + text[1:]


def command_name(action_value: str, polish_text: str) -> str:
    if _active_language == POLISH:
        return polish_text
    return english_action_name(action_value)


def command_description(action_value: str, polish_text: str) -> str:
    if _active_language == POLISH:
        return polish_text
    # A concise, stable command name is preferable to a Polish fallback or a
    # technical identifier. More detailed English descriptions can be added
    # as catalog entries without changing the Action model.
    return english_action_name(action_value)


def menu_title(polish_title: str) -> str:
    if _active_language == POLISH:
        return polish_title
    plain = polish_title.replace("&", "")
    translated = {
        "Pliki": "&File",
        "Biblioteka": "&Library",
        "Szukaj": "&Search",
        "Radio": "&Radio",
        "Odtwarzanie": "&Playback",
        "Widok": "&View",
        "Dźwięk": "&Audio",
        "Ustawienia": "&Settings",
        "Pomoc": "&Help",
    }.get(plain)
    return translated or plain


def menu_action_label(action_value: str, polish_label: str) -> str:
    if _active_language == POLISH:
        return polish_label
    suffix = "…" if polish_label.rstrip().endswith("…") else ""
    return "&" + english_action_name(action_value) + suffix
