"""Prywatny stan AMC-wx-Lite: stacje uzytkownika i opcje.

WAZNE o izolacji: ten magazyn NIE dotyka state.json pelnego AMC. Siedzi w
osobnym folderze ``AMC-wx-Lite``, zeby lekki wariant nie mogl uszkodzic
ustawien, sesji, stacji ani nagran dzialajacego programu Michala.

Zapis jest ATOMOWY: najpierw plik tymczasowy w TYM SAMYM folderze, potem
``os.replace``. Przerwanie w trakcie zapisu zostawia starą, poprawna wersje,
a nie obciety plik JSON.
"""

from __future__ import annotations

import json
import os
import tempfile
import uuid
from dataclasses import asdict, dataclass, field
from pathlib import Path

from .audio_clip import read_clip_selections
from .audio_output import read_output_overrides
from .i18n import POLISH, SUPPORTED_LANGUAGES, normalize_preference
from .profile_presets import read_preset_overrides
from .playback_history import normalize_histories
from .radio_activity import normalize_state_position
from .radio_recognition import normalize_scope, read_tracks
from .recognition_actions import normalize_default_service
from .radio_schedule_settings import read_schedule_overrides
from .release_profile import current_profile, profile_directory
from .session_order import (
    migrated_values as migrated_session_order_values,
    values as normalized_session_order_values,
)
from .service_picker import (
    normalize_destination_ids,
    normalize_remembered_destinations,
)
from .shortcuts import normalize_shortcut_overrides, serialize_shortcut_overrides
from .transport_parity import (
    normalize_custom_seek_seconds,
    normalize_message_templates,
)


_COLLECTION_SORT_MODES = {"AddedNewest", "Alphabetical", "Custom"}
_TIDAL_SORT_VIEWS = {"library", "favorites", "playlists"}
_SPOTIFY_SORT_VIEWS = {
    "liked", "albums", "artists", "playlists", "podcasts", "episodes",
}
YOUTUBE_LIVE_CHANNEL_LIMIT = 20


def _read_service_picker_destination_ids(
    raw: object,
    legacy_spotify_raw: object,
) -> dict[str, list[str]]:
    """Read generic selections and migrate the earlier Spotify-only field."""

    result = normalize_remembered_destinations(raw)
    legacy_spotify_ids = normalize_destination_ids(legacy_spotify_raw)
    if legacy_spotify_ids and "spotify:playlists" not in result:
        result["spotify:playlists"] = legacy_spotify_ids
    return result


def _read_podcast_inbox_sort_mode(raw: object) -> str | None:
    """Only a known model value may survive in the private state file."""
    return raw if isinstance(raw, str) and raw in _COLLECTION_SORT_MODES else None


def _read_podcast_library_sort_mode(raw: object) -> str | None:
    """Validate the private main-library mode without exposing model text."""
    return raw if isinstance(raw, str) and raw in _COLLECTION_SORT_MODES else None


def normalize_youtube_live_channel_ids(raw: object) -> list[str]:
    """Zachowaj najwyżej 20 stabilnych identyfikatorów wybranych kanałów."""

    if not isinstance(raw, list):
        return []
    result: list[str] = []
    seen: set[str] = set()
    for value in raw:
        if not isinstance(value, str):
            continue
        channel_id = value.strip()
        if not channel_id or channel_id in seen:
            continue
        seen.add(channel_id)
        result.append(channel_id)
        if len(result) >= YOUTUBE_LIVE_CHANNEL_LIMIT:
            break
    return result


def _read_tidal_sort_modes(raw: object) -> dict[str, str]:
    """Keep only supported TIDAL views and model values.

    The values remain internal.  Accessible labels are produced by the GUI,
    never from this dictionary or from an enum representation.
    """
    if not isinstance(raw, dict):
        return {}
    return {
        view: mode
        for view, mode in raw.items()
        if (
            isinstance(view, str)
            and view in _TIDAL_SORT_VIEWS
            and isinstance(mode, str)
            and mode in _COLLECTION_SORT_MODES
        )
    }


def _read_spotify_sort_modes(raw: object) -> dict[str, str]:
    """Keep only the six visible Spotify collections and known sort modes."""

    if not isinstance(raw, dict):
        return {}
    return {
        view: mode
        for view, mode in raw.items()
        if (
            isinstance(view, str)
            and view in _SPOTIFY_SORT_VIEWS
            and isinstance(mode, str)
            and mode in _COLLECTION_SORT_MODES
        )
    }


def _read_search_history(raw: object) -> dict[str, list[str]]:
    """Sanitize per-scope queries; never retain arbitrary objects."""

    if not isinstance(raw, dict):
        return {}
    result: dict[str, list[str]] = {}
    for scope, values in raw.items():
        if not isinstance(scope, str) or not scope.strip() or not isinstance(values, list):
            continue
        entries: list[str] = []
        seen: set[str] = set()
        for value in values:
            if not isinstance(value, str):
                continue
            query = value.strip()
            key = query.casefold()
            if not query or key in seen:
                continue
            seen.add(key)
            entries.append(query)
            if len(entries) >= 50:
                break
        if entries:
            result[scope.strip()] = entries
    return result


def _read_spotify_history(raw: object) -> list[dict]:
    """Sanitize AMC Python's own confirmed Spotify playback history."""

    if not isinstance(raw, list):
        return []
    text_fields = (
        "itemId", "externalId", "title", "kind", "artist", "publicUri",
        "relatedAlbumExternalId", "relatedAlbumTitle",
        "relatedArtistExternalId", "relatedArtistName",
        "relatedPodcastExternalId", "relatedPodcastTitle",
    )
    result: list[dict] = []
    seen: set[str] = set()
    for raw_item in raw:
        if len(result) >= 500 or not isinstance(raw_item, dict):
            continue
        item = {
            name: (
                value.strip()
                if isinstance(value := raw_item.get(name), str)
                else ""
            )
            for name in text_fields
        }
        if (
            not item["itemId"]
            or not item["externalId"]
            or not item["title"]
            or item["kind"] not in ("track", "episode")
            or item["externalId"] in seen
        ):
            continue
        seen.add(item["externalId"])
        duration = raw_item.get("durationTicks")
        item["durationTicks"] = (
            max(0, duration) if type(duration) is int else 0
        )
        result.append(item)
    return result


def _read_tidal_queue(raw: object) -> list[dict]:
    """Wczytaj tylko pola potrzebne do kolejki TIDAL, bez dowolnych rekordów."""
    if not isinstance(raw, list):
        return []
    result: list[dict] = []
    seen: set[str] = set()
    text_fields = (
        "itemId", "externalId", "title", "kind", "artist", "publicUri",
        "relatedAlbumExternalId", "relatedAlbumTitle",
        "relatedArtistExternalId", "relatedArtistName",
    )
    for raw_item in raw:
        if len(result) >= 2_000 or not isinstance(raw_item, dict):
            continue
        item = {
            name: value.strip() if isinstance(value := raw_item.get(name), str) else ""
            for name in text_fields
        }
        if (
            not item["itemId"]
            or item["itemId"] in seen
            or not item["externalId"]
            or not item["title"]
            or item["kind"] != "track"
            or not item["relatedAlbumExternalId"]
        ):
            continue
        seen.add(item["itemId"])
        result.append(item)
    return result


@dataclass(slots=True)
class Station:
    """Stacja z WLASNEJ listy uzytkownika."""

    id: str
    name: str
    url: str

    @staticmethod
    def new(name: str, url: str) -> "Station":
        return Station(id=uuid.uuid4().hex, name=name.strip(), url=url.strip())


@dataclass(slots=True)
class Options:
    volume: int = 35
    rate: float = 1.0
    timeshift_minutes: int = 30
    #: Ekran po uruchomieniu. Wartość modelu pozostaje prywatna; dialog
    #: ustawień pokazuje wyłącznie pełne polskie etykiety.
    startup_target: str = "MediaList"
    #: Preferencja języka jest niezależna od konkretnego katalogu. Wartość
    #: ``system`` rozstrzyga się przy każdym starcie aplikacji.
    interface_language: str = "system"
    loudness_normalization: bool = False
    smooth_track_transitions: bool = False
    inter_track_silence_ms: int = 0
    tempo_algorithm: int = 1
    #: Kolejność ustala Ctrl+1–9, listę sesji oraz Ctrl+Page Up/Down.
    #: Zapis zawiera wyłącznie stabilne wartości modelu; GUI tworzy osobne,
    #: celowe etykiety użytkowe.
    session_order: list[str] = field(default_factory=lambda: [
        "files", "radio", "podcasts"
    ])
    last_folder: str | None = None
    #: Czy wyjscie z odtwarzacza (Escape, F6) ma wstrzymac odtwarzanie.
    #:
    #: Ustawienie OGOLNE, dziedziczone przez sesje bez wlasnego wyboru -- port
    #: ``AppSettings.PausePlaybackWhenLeavingPlayer`` (AppSettings.cs:98), ta
    #: sama wartosc domyslna ``True``. Sesja moze je nadpisac w Opcjach sesji
    #: (``session_options.resolve_pause_on_player_exit``).
    pause_on_player_exit: bool = True
    #: Po wyjsciu z odtwarzacza fokus moze sledzic element wybrany w nim
    #: Page Up/Page Down. Domysl taki sam jak ``AppSettings`` pelnego AMC.
    follow_playback_on_player_exit: bool = True
    #: Preset moze grac w tle albo otworzyc odtwarzacz. Pelne AMC domyslnie
    #: zostawia fokus w biezacym widoku.
    open_player_when_activating_preset: bool = False
    #: Enter na stacji moze zaczac granie bez zabierania fokusu z listy.
    stay_on_list_after_radio_enter: bool = False
    #: Polozenie slowa opisujacego odtwarzanie wzgledem nazwy stacji.
    #: Wartosc modelu nigdy nie jest pokazywana uzytkownikowi; dialog mapuje
    #: ja na pelne polskie etykiety.
    radio_playback_state_position: str = "after"
    #: Niezalezna pozycja komunikatu o nagrywaniu.
    radio_recording_state_position: str = "after"
    #: Delikatne sygnaly przy przejsciu fokusu na aktywna stacje.
    radio_playback_state_sound: bool = False
    radio_recording_state_sound: bool = False
    #: Rozpoznawanie korzysta z bufora AMC; do uslugi wychodzi wylacznie
    #: podpis akustyczny tworzony przez wspolny silnik C#.
    radio_recognition_monitoring: bool = False
    radio_recognition_scope: str = "CurrentStation"
    radio_recognition_announcements: bool = True
    #: Service used by Enter, Ctrl+Enter and Ctrl+Shift+C in recognized tracks.
    #: Apple Music deliberately stays link-only and is never a default.
    radio_recognition_default_service: str = "spotify"
    #: Po odłączeniu wybranego wyjścia Radio przechodzi na domyślne. Ten
    #: przełącznik decyduje, czy nowy tor ma pozostać w pauzie. Domyślne False
    #: zachowuje dotychczasowe zachowanie AMC do świadomej zmiany ustawienia.
    pause_on_audio_output_fallback: bool = False
    #: Zachowanie kopii poprzedniej wersji po udanej edycji istniejącego
    #: nagrania. ``None`` zachowuje zgodność przejściową i dziedziczy wartość
    #: z profilu głównego AMC; zapis okna ustawień tworzy już jawny wybór
    #: należący do samodzielnej wersji wxPython.
    keep_audio_edit_backups: bool | None = None
    #: Dłuższy przeskok Ctrl+Alt+strzałką. ``None`` dziedziczy dotychczasową
    #: wartość z profilu głównego AMC do pierwszego zapisu nowych ustawień.
    custom_seek_seconds: int | None = None
    #: Czy trzy szybkie podglądy mają działać ze wszystkich sesji. ``None``
    #: dziedziczy wybór z profilu głównego AMC do pierwszego zapisu.
    global_transient_previews: bool | None = None
    #: Czy lokalne pliki wracaja do zapamietanej pozycji. ``None`` dziedziczy
    #: wybor z profilu glownego AMC do pierwszego zapisu Ustawien wxPython.
    remember_local_playback_positions: bool | None = None
    #: Czy pola i listy maja udostepniac dluzsze podpowiedzi klawiatury.
    #: ``None`` dziedziczy ``settings.messages.detailedHints`` z profilu
    #: glownego AMC do pierwszego jawnego zapisu Ustawien wxPython.
    detailed_hints: bool | None = None
    #: Prywatne nadpisania komunikatow odtwarzacza. ``None`` oznacza dalsze
    #: dziedziczenie dotychczasowego wyboru z profilu glownego AMC. Po zapisie
    #: karty Komunikaty wlascicielem tych wartosci staje sie AMC Python, bez
    #: modyfikowania wspolnego ``state.json`` starego interfejsu.
    message_enabled: bool | None = None
    message_seek_messages: bool | None = None
    message_arrow_seek_messages: bool | None = None
    message_percentage_seek_messages: bool | None = None
    message_volume_messages: bool | None = None
    message_playback_messages: bool | None = None
    message_bookmark_navigation_messages: bool | None = None
    message_percent_announcement: str | None = None
    #: Prywatne teksty komunikatow. Klucze sa filtrowane do zamknietego
    #: katalogu, a pusty tekst pozostaje swiadomym wylaczeniem zdarzenia.
    #: Pole pojedynczego słownika jest zachowane wyłącznie do migracji stanu
    #: sprzed wielojęzyczności. Przy zapisie nie wraca już do pliku.
    message_templates: dict[str, str] | None = None
    #: Własne teksty są rozdzielone według konkretnego języka, aby polski
    #: tekst użytkownika nigdy nie zastąpił angielskiego komunikatu.
    message_templates_by_language: dict[str, dict[str, str]] = field(
        default_factory=dict
    )
    #: Sprawdzanie nowej wersji calego AMC po starcie. Domyslnie wlaczone:
    #: samo sprawdzenie niczego nie pobiera ani nie instaluje, a decyzja
    #: pozostaje w dostepnym oknie aktualizacji.
    application_check_automatically: bool = True
    #: Prywatne nadpisania polityki aktualizacji składników. ``None`` oznacza
    #: dziedziczenie dotychczasowego wyboru z profilu głównego AMC do
    #: pierwszego zapisu karty Aktualizacje.
    component_check_automatically: bool | None = None
    component_download_automatically: bool | None = None
    #: Prywatne nadpisania ustawien nagrywania. ``None`` oznacza dalsze
    #: dziedziczenie wartosci z profilu glownego AMC; pusty folder oznacza
    #: swiadomy wybor systemowego folderu domyslnego hosta.
    radio_recording_format: str | None = None
    radio_recording_bitrate_kbps: int | None = None
    radio_recordings_folder: str | None = None
    radio_use_podcast_downloads_folder: bool | None = None
    radio_separate_station_folders: bool = False
    radio_prefer_station_folder_in_new_schedules: bool | None = None
    #: Globalne ustawienia Podcastow. ``None`` dziedziczy dotychczasowa
    #: wartosc z podcasts.db; po pierwszym zapisie karta staje sie ich
    #: jawnym wlascicielem i host stosuje je takze przy kolejnym starcie.
    podcast_downloads_folder: str | None = None
    podcast_separate_source_folders: bool = False
    podcast_rss_refresh_interval_minutes: int | None = None
    podcast_youtube_refresh_interval_minutes: int | None = None
    podcast_automatic_refresh_batch_size: int | None = None
    podcast_youtube_video_format: str = "mp4"

    def audio_payload(self) -> dict:
        return {
            "tempoAlgorithm": self.tempo_algorithm,
            "loudnessNormalization": self.loudness_normalization,
            "smoothTrackTransitions": self.smooth_track_transitions,
            "interTrackSilenceMs": self.inter_track_silence_ms,
            "pauseOnOutputFallback": self.pause_on_audio_output_fallback,
        }

    def clamp(self) -> "Options":
        """Trzymaj wartosci w granicach, ktore silnik faktycznie przyjmuje."""
        self.volume = max(0, min(100, int(self.volume)))
        self.rate = max(0.5, min(2.0, float(self.rate)))
        self.timeshift_minutes = max(1, min(720, int(self.timeshift_minutes)))
        # Te same dozwolone wartosci co PlaybackAudioSettingsRules w Core.
        allowed = (0, 500, 1000, 2000, 3000, 5000)
        if self.inter_track_silence_ms not in allowed:
            self.inter_track_silence_ms = 0
        if type(self.tempo_algorithm) is not int or self.tempo_algorithm not in (0, 1, 2):
            self.tempo_algorithm = 1
        if self.startup_target not in ("MediaList", "SessionList"):
            self.startup_target = "MediaList"
        self.interface_language = normalize_preference(self.interface_language)
        self.session_order = normalized_session_order_values(self.session_order)
        # STRICT ``is not bool``, nie ``bool(...)``: napis z recznie
        # poprawionego pliku nie moze po cichu zmienic zachowania Escape.
        if type(self.pause_on_player_exit) is not bool:
            self.pause_on_player_exit = True
        if type(self.follow_playback_on_player_exit) is not bool:
            self.follow_playback_on_player_exit = True
        if type(self.open_player_when_activating_preset) is not bool:
            self.open_player_when_activating_preset = False
        if type(self.stay_on_list_after_radio_enter) is not bool:
            self.stay_on_list_after_radio_enter = False
        self.radio_playback_state_position = normalize_state_position(
            self.radio_playback_state_position
        )
        self.radio_recording_state_position = normalize_state_position(
            self.radio_recording_state_position
        )
        if type(self.radio_playback_state_sound) is not bool:
            self.radio_playback_state_sound = False
        if type(self.radio_recording_state_sound) is not bool:
            self.radio_recording_state_sound = False
        if type(self.radio_recognition_monitoring) is not bool:
            self.radio_recognition_monitoring = False
        self.radio_recognition_scope = normalize_scope(
            self.radio_recognition_scope
        )
        if type(self.radio_recognition_announcements) is not bool:
            self.radio_recognition_announcements = True
        self.radio_recognition_default_service = normalize_default_service(
            self.radio_recognition_default_service
        )
        if type(self.pause_on_audio_output_fallback) is not bool:
            self.pause_on_audio_output_fallback = False
        if (
            self.keep_audio_edit_backups is not None
            and type(self.keep_audio_edit_backups) is not bool
        ):
            self.keep_audio_edit_backups = None
        if self.custom_seek_seconds is not None:
            if type(self.custom_seek_seconds) is int:
                self.custom_seek_seconds = normalize_custom_seek_seconds(
                    self.custom_seek_seconds
                )
            else:
                self.custom_seek_seconds = None
        if (
            self.global_transient_previews is not None
            and type(self.global_transient_previews) is not bool
        ):
            self.global_transient_previews = None
        if (
            self.remember_local_playback_positions is not None
            and type(self.remember_local_playback_positions) is not bool
        ):
            self.remember_local_playback_positions = None
        if self.detailed_hints is not None and type(self.detailed_hints) is not bool:
            self.detailed_hints = None
        for name in (
            "message_enabled",
            "message_seek_messages",
            "message_arrow_seek_messages",
            "message_percentage_seek_messages",
            "message_volume_messages",
            "message_playback_messages",
            "message_bookmark_navigation_messages",
        ):
            value = getattr(self, name)
            if value is not None and type(value) is not bool:
                setattr(self, name, None)
        if self.message_percent_announcement is not None:
            allowed_message_modes = ("Percent", "Time", "PercentAndTime")
            normalized_message_mode = str(
                self.message_percent_announcement
            ).strip().casefold()
            self.message_percent_announcement = next(
                (
                    value
                    for value in allowed_message_modes
                    if value.casefold() == normalized_message_mode
                ),
                None,
            )
        if self.message_templates is not None:
            self.message_templates = normalize_message_templates(
                self.message_templates
            )
        normalized_language_templates: dict[str, dict[str, str]] = {}
        if isinstance(self.message_templates_by_language, dict):
            for language in SUPPORTED_LANGUAGES:
                raw_templates = self.message_templates_by_language.get(language)
                if isinstance(raw_templates, dict):
                    normalized_language_templates[language] = (
                        normalize_message_templates(raw_templates)
                    )
        # Wszystkie istniejące własne teksty były polskie. Migracja jest
        # jednokierunkowa i nie kopiuje ich do angielskiego katalogu.
        if self.message_templates is not None:
            normalized_language_templates[POLISH] = {
                **self.message_templates,
                **normalized_language_templates.get(POLISH, {}),
            }
        self.message_templates_by_language = normalized_language_templates
        # Read compatibility for code/tests that still inspect the former
        # single-language attribute. Persistence nevertheless uses only the
        # grouped field, so this cannot overwrite another language.
        self.message_templates = (
            dict(normalized_language_templates[POLISH])
            if POLISH in normalized_language_templates
            else None
        )
        for name in (
            "application_check_automatically",
            "component_check_automatically",
            "component_download_automatically",
        ):
            value = getattr(self, name)
            if type(value) is bool:
                continue
            setattr(
                self,
                name,
                True if name == "application_check_automatically" else None,
            )
        if self.radio_recording_format is not None:
            formats = ("Mp3", "Aac", "Flac", "Original", "Wav")
            value = str(self.radio_recording_format).strip().casefold()
            self.radio_recording_format = next(
                (item for item in formats if item.casefold() == value), None
            )
        if self.radio_recording_bitrate_kbps is not None:
            bitrates = (96, 128, 160, 192, 256, 320)
            if isinstance(self.radio_recording_bitrate_kbps, bool):
                self.radio_recording_bitrate_kbps = None
            else:
                try:
                    requested_bitrate = int(
                        self.radio_recording_bitrate_kbps
                    )
                except (TypeError, ValueError, OverflowError):
                    self.radio_recording_bitrate_kbps = None
                else:
                    self.radio_recording_bitrate_kbps = min(
                        bitrates, key=lambda item: abs(item - requested_bitrate)
                    )
        if self.radio_recordings_folder is not None:
            self.radio_recordings_folder = str(
                self.radio_recordings_folder
            ).strip()
        if (
            self.radio_use_podcast_downloads_folder is not None
            and type(self.radio_use_podcast_downloads_folder) is not bool
        ):
            self.radio_use_podcast_downloads_folder = None
        if (
            self.radio_prefer_station_folder_in_new_schedules is not None
            and type(
                self.radio_prefer_station_folder_in_new_schedules
            ) is not bool
        ):
            self.radio_prefer_station_folder_in_new_schedules = None
        if type(self.radio_separate_station_folders) is not bool:
            self.radio_separate_station_folders = False
        if type(self.podcast_separate_source_folders) is not bool:
            self.podcast_separate_source_folders = False
        if self.podcast_downloads_folder is not None:
            self.podcast_downloads_folder = str(
                self.podcast_downloads_folder
            ).strip()
        for name in (
            "podcast_rss_refresh_interval_minutes",
            "podcast_youtube_refresh_interval_minutes",
        ):
            value = getattr(self, name)
            if value is None:
                continue
            if isinstance(value, bool):
                setattr(self, name, None)
                continue
            try:
                setattr(self, name, max(0, min(10_080, int(value))))
            except (TypeError, ValueError, OverflowError):
                setattr(self, name, None)
        if self.podcast_automatic_refresh_batch_size is not None:
            value = self.podcast_automatic_refresh_batch_size
            if isinstance(value, bool):
                self.podcast_automatic_refresh_batch_size = None
            else:
                try:
                    self.podcast_automatic_refresh_batch_size = max(
                        1, min(50, int(value))
                    )
                except (TypeError, ValueError, OverflowError):
                    self.podcast_automatic_refresh_batch_size = None
        if self.podcast_youtube_video_format not in ("mp4", "original"):
            self.podcast_youtube_video_format = "mp4"
        return self


@dataclass(slots=True)
class LiteState:
    options: Options = field(default_factory=Options)
    stations: list[Station] = field(default_factory=list)
    navigation: dict = field(default_factory=dict)
    #: Prywatne, użytkowe skróty AMC Python. Klucze techniczne nie trafiają
    #: do kontrolek; interfejs zawsze mapuje je na jawne nazwy poleceń.
    shortcut_overrides: dict[str, str] = field(default_factory=dict)
    #: Prywatny, trwaly przelacznik losowania dla sesji, ktore faktycznie
    #: maja w AMC Test wspierany tor nastepstwa. Brak klucza oznacza wylaczone.
    shuffle_enabled_by_session: dict[str, bool] = field(default_factory=dict)
    #: Opcje sesji: wybory uzytkownika per sesja (``files`` / ``radio``).
    #: Pusty slownik = kazda sesja dziedziczy ustawienia ogolne, czyli
    #: zachowanie sprzed tego przyrostu.
    session_overrides: dict = field(default_factory=dict)
    #: Wyjscie audio per sesja. Brak klucza dziedziczy wybor glownego AMC,
    #: pusty napis oznacza jawny wybor urzadzenia domyslnego Windows.
    audio_output_device_ids_by_session: dict[str, str] = field(default_factory=dict)
    #: Prywatna historia prob nagrywania wxPython. Nie trafia do profilu
    #: pelnego AMC, ale przezywa ponowne uruchomienie lekkiego interfejsu.
    #: Wartosci sa zwyklymi danymi protokolu; etykiety NVDA powstaja dopiero
    #: w ``radio_recording.py``.
    recording_history: list[dict] = field(default_factory=list)
    #: Prywatna historia gotowych wynikow. Probki audio i podpisy akustyczne
    #: nie opuszczaja hosta i nie sa tu nigdy zapisywane.
    recognized_tracks: list[dict] = field(default_factory=list)
    #: Historia pełnego AMC jest przejmowana tylko raz. Dzięki temu świadomie
    #: usunięty wpis nie wraca przy następnym uruchomieniu nowego interfejsu.
    recognized_tracks_imported_from_amc: bool = False
    #: Niedestrukcyjne znaczniki I/O dla lokalnych plikow. Tak jak historia
    #: nagran sa prywatne dla wxPython i nie zapisuja sie do profilu WPF.
    #: Magazyn przechowuje jedynie wartosci modelu; tekst dla NVDA powstaje w
    #: ``audio_clip.py`` i nigdy nie jest serializowany jako repr obiektu.
    clip_selections: list[dict] = field(default_factory=list)
    #: Prywatne presety sesji. BRAK klucza sesji = nadal czytaj presety z
    #: profilu pelnego AMC. Obecny klucz z pusta lista = uzytkownik swiadomie
    #: usunal wszystkie presety tej sesji. To rozroznienie pozwala zachowac
    #: zgodnosc bez zapisywania do wspolnego ``state.json``.
    preset_overrides: dict[str, list[dict]] = field(default_factory=dict)
    #: Prywatny, kompletny zestaw harmonogramow wxPython. ``None`` oznacza:
    #: nadal odczytuj plany z profilu pelnego AMC. Pusta lista oznacza:
    #: uzytkownik swiadomie usunal wszystkie plany w interfejsie wxPython.
    radio_schedule_overrides: list[dict] | None = None
    #: Jak wyzej dla ogolnego wybudzania. ``None`` dziedziczy profil AMC.
    radio_schedule_wake_override: bool | None = None
    #: Prywatne nadpisanie kolejnosci widoku „Nowe odcinki”. ``None`` znaczy:
    #: czytaj wybor glownego AMC, ale nigdy nie zapisuj do jego ``state.json``.
    podcast_inbox_sort_mode: str | None = None
    #: Prywatny porządek głównej listy podcastów, kanałów YouTube i kolekcji
    #: Media internetowe. ``None`` zachowuje dotychczasową kolejność własną z
    #: bazy, a jawny wybór przeżywa restart wyłącznie w magazynie AMC Test.
    podcast_library_sort_mode: str | None = None
    #: Kanały jawnie wybrane do zbiorczego sprawdzania transmisji. Lista jest
    #: prywatna dla profilu AMC Test; host C# nadal jest jedynym właścicielem
    #: tokenu Google. Limit chroni dzienny limit API przed skanem całej
    #: biblioteki po każdym Ctrl+Shift+I.
    youtube_live_channel_ids: list[str] = field(default_factory=list)
    #: Prywatna kolejka utworów przekazywanych do oryginalnego TIDALa.
    #: Nie zapisujemy jej do wspólnego state.json, aby dwa programy nie
    #: ścigały się o profil. Pola techniczne pozostają wyłącznie modelem.
    tidal_queue_items: list[dict] = field(default_factory=list)
    #: Prywatny tryb kolejności osobno dla trzech głównych widoków TIDAL.
    #: Brak klucza oznacza dalsze dziedziczenie z profilu pełnego AMC.
    tidal_sort_modes: dict[str, str] = field(default_factory=dict)
    #: Prywatny tryb kolejności osobno dla sześciu kolekcji Spotify.
    #: Wartości są wyłącznie modelem; NVDA dostaje jawne polskie etykiety.
    spotify_sort_modes: dict[str, str] = field(default_factory=dict)
    #: Ostatnie natywne playlisty Spotify wybrane pod Ctrl+Shift+P. To dane
    #: zgodności wstecznej; nowe usługi używają mapy poniżej.
    spotify_playlist_destination_ids: list[str] = field(default_factory=list)
    #: Ostatnie cele zdalnych okien wyboru według usługi i rodzaju listy.
    #: Identyfikatory pozostają modelem; etykiety NVDA powstają w adapterze.
    service_picker_destination_ids: dict[str, list[str]] = field(
        default_factory=dict
    )
    #: Historia zapytań osobno dla każdej sesji oraz zakresu globalnego.
    #: Zawiera wyłącznie tekst użytkownika, bez wyników i identyfikatorów.
    search_history: dict[str, list[str]] = field(default_factory=dict)
    #: Prywatna historia tego, co AMC Python faktycznie uruchomił w Spotify.
    #: Nie udaje pełnej historii konta; wpis powstaje dopiero po potwierdzonym
    #: starcie Librespota. Identyfikatory są wyłącznie danymi modelu.
    spotify_playback_history: list[dict] = field(default_factory=list)
    #: Prywatne historie streamerów, których nie da się odbudować z lokalnej
    #: bazy plików. WiiM i Sonos zapisują migawki faktycznie grającego
    #: urządzenia; historia TIDAL i Spotify pozostaje na swoich drogach.
    playback_history_by_session: dict[str, list[dict]] = field(
        default_factory=dict
    )


def _read_session_overrides(raw: object) -> dict:
    """Wczytaj Opcje sesji z profilu.

    Import jest LOKALNY, zeby ``session_options`` mogl zalezec od ``Options``
    bez cyklu. Przyjmujemy WYLACZNIE sesje, ktore port naprawde ma: wpis dla
    obcej nazwy zapisalby decyzje, ktorej nikt nie wykona. Puste wpisy nie
    wchodza -- brak decyzji ma zostac brakiem, nie wartoscia.
    """
    from .navigation import SessionId
    from .session_options import SessionPlaybackOverrides

    result: dict = {}
    if not isinstance(raw, dict):
        return result
    known = {session.value for session in SessionId}
    for key, value in raw.items():
        if key not in known or not isinstance(value, dict):
            continue
        overrides = SessionPlaybackOverrides.from_payload(value)
        if not overrides.is_empty:
            result[key] = overrides
    return result


def _read_shuffle_enabled(raw: object) -> dict[str, bool]:
    """Przepusc tylko trzy wspierane sesje i wylacznie jawne ``true``."""

    if not isinstance(raw, dict):
        return {}
    supported = {"files", "podcasts", "spotify"}
    return {
        key: True
        for key, value in raw.items()
        if key in supported and value is True
    }


def _read_recording_history(raw: object) -> list[dict]:
    """Wczytaj najwyzej 1000 bezpiecznych wpisow prywatnej historii.

    Nie przechowujemy dowolnych obiektow ani nieznanych pol z pliku. Dzieki
    temu uszkodzony lub recznie zmieniony stan nie moze pozniej wyciec jako
    reprezentacja obiektu do listy dostepnej dla NVDA.
    """
    if not isinstance(raw, list):
        return []
    result: list[dict] = []
    seen: set[str] = set()
    text_fields = ("stationId", "stationName", "path", "reason", "scheduleName")
    for item in raw:
        if len(result) >= 1_000:
            break
        if not isinstance(item, dict):
            continue
        entry_id = item.get("id")
        if not isinstance(entry_id, str) or not entry_id.strip():
            continue
        entry_id = entry_id.strip()
        if entry_id in seen:
            continue
        seen.add(entry_id)
        entry: dict = {"id": entry_id}
        for name in text_fields:
            value = item.get(name)
            entry[name] = value.strip() if isinstance(value, str) else ""
        outcome = item.get("outcome")
        entry["outcome"] = (
            outcome.strip().casefold()
            if isinstance(outcome, str)
            and outcome.strip().casefold() in ("completed", "stopped", "interrupted", "failed")
            else "completed"
        )
        for name in ("startedUtcTicks", "finishedUtcTicks", "savedFileCount"):
            value = item.get(name)
            entry[name] = max(0, value) if isinstance(value, int) and not isinstance(value, bool) else 0
        result.append(entry)
    return result


def default_state_dir() -> Path:
    """Prywatny folder stanu. Osobny od AccessibleMediaController."""
    override = os.environ.get("AMC_WX_LITE_HOME")
    if override:
        return Path(override)
    appdata = os.environ.get("APPDATA")
    if appdata:
        profile = current_profile()
        return profile_directory(Path(appdata), profile.ui_state_folder)
    return profile_directory(
        Path.home() / ".config",
        current_profile().ui_state_folder,
    )


class StateStore:
    """Czyta i zapisuje prywatny stan lekkiego wariantu."""

    FILE_NAME = "state.json"
    VERSION = 1
    SESSION_MANAGER_VERSION = 1
    PLAYBACK_RATE_VERSION = 1

    def __init__(self, directory: Path | str | None = None) -> None:
        self.directory = Path(directory) if directory is not None else default_state_dir()
        self.path = self.directory / self.FILE_NAME

    # ------------------------------------------------------------- odczyt

    def load(self) -> LiteState:
        """Wczytaj stan. Brak pliku, uszkodzony JSON ani obcy kształt NIE MOGA
        zablokowac startu programu - w najgorszym razie wracamy do domyslnych."""
        try:
            raw = json.loads(self.path.read_text(encoding="utf-8"))
        except (FileNotFoundError, json.JSONDecodeError, OSError, UnicodeDecodeError):
            return LiteState()
        return self.load_payload(raw)

    def load_payload(self, raw: object) -> LiteState:
        """Zweryfikuj stan przekazany w pamięci, np. z pełnej kopii AMC.

        Ta sama droga obsługuje zwykły start i import. Dzięki temu kopia nie
        może ominąć ograniczeń rozmiaru list, dozwolonych trybów ani jawnych
        etykiet modelu tylko dlatego, że nie pochodzi bezpośrednio z pliku
        ``state.json`` interfejsu.
        """
        if not isinstance(raw, dict):
            return LiteState()

        options = Options()
        raw_options = raw.get("options")
        if not isinstance(raw_options, dict):
            raw_options = {}
        for key, value in raw_options.items():
            if key == "session_order":
                # To jedyna lista w ``Options``. Nie przepuszczamy jej przez
                # ogólną gałąź skalarną, ale też nie ignorujemy przy odczycie:
                # ``clamp`` usunie duplikaty i obce identyfikatory.
                options.session_order = (
                    list(value) if isinstance(value, (list, tuple)) else []
                )
                continue
            if key == "message_templates":
                options.message_templates = (
                    normalize_message_templates(value)
                    if isinstance(value, dict)
                    else None
                )
                continue
            if key == "message_templates_by_language":
                options.message_templates_by_language = (
                    dict(value) if isinstance(value, dict) else {}
                )
                continue
            if hasattr(options, key) and not isinstance(value, (dict, list)):
                try:
                    setattr(options, key, value)
                except (TypeError, ValueError):
                    pass
        # Stary zapis nie odróżniał kolejności od włączenia. Pierwszy odczyt
        # zachowuje więc wszystkie znane sesje i ich wzajemny porządek. Dopiero
        # jawny znacznik AMC09 pozwala traktować brak sesji jako jej wyłączenie.
        if raw.get("session_manager_version") != self.SESSION_MANAGER_VERSION:
            options.session_order = migrated_session_order_values(
                raw_options.get("session_order")
            )
        # Do 6ce2241 każda zmiana prędkości (np. 2x dla podcastu) trafiała do
        # ``rate``. Teraz to ogólna prędkość startowa każdej sesji bez własnej,
        # bez pola w ustawieniach, więc stara wartość kazała startować z 2x
        # nawet radiu na żywo. Zapis bez znacznika niesie tylko ostatnio użytą
        # prędkość, nie wybór użytkownika.
        if raw.get("playback_rate_version") != self.PLAYBACK_RATE_VERSION:
            options.rate = Options().rate
        try:
            options.clamp()
        except (TypeError, ValueError):
            options = Options()

        stations: list[Station] = []
        seen: set[str] = set()
        for entry in raw.get("stations") or []:
            if not isinstance(entry, dict):
                continue
            name = entry.get("name")
            url = entry.get("url")
            if not isinstance(name, str) or not isinstance(url, str) or not url.strip():
                continue
            station_id = entry.get("id")
            if not isinstance(station_id, str) or not station_id or station_id in seen:
                station_id = uuid.uuid4().hex
            seen.add(station_id)
            stations.append(Station(id=station_id, name=name.strip(), url=url.strip()))

        navigation = raw.get("navigation")
        raw_wake_override = raw.get("radio_schedule_wake_override")
        wake_override = (
            raw_wake_override if type(raw_wake_override) is bool else None
        )
        return LiteState(
            options=options,
            stations=stations,
            navigation=navigation if isinstance(navigation, dict) else {},
            shortcut_overrides=serialize_shortcut_overrides(
                normalize_shortcut_overrides(raw.get("shortcut_overrides"))
            ),
            shuffle_enabled_by_session=_read_shuffle_enabled(
                raw.get("shuffle_enabled_by_session")
            ),
            session_overrides=_read_session_overrides(raw.get("session_overrides")),
            audio_output_device_ids_by_session=read_output_overrides(
                raw.get("audio_output_device_ids_by_session")
            ),
            recording_history=_read_recording_history(raw.get("recording_history")),
            recognized_tracks=read_tracks(raw.get("recognized_tracks")),
            recognized_tracks_imported_from_amc=(
                raw.get("recognized_tracks_imported_from_amc") is True
            ),
            clip_selections=read_clip_selections(raw.get("clip_selections")),
            preset_overrides=read_preset_overrides(raw.get("preset_overrides")),
            radio_schedule_overrides=(
                read_schedule_overrides(raw.get("radio_schedule_overrides"))
                if "radio_schedule_overrides" in raw
                else None
            ),
            radio_schedule_wake_override=wake_override,
            podcast_inbox_sort_mode=_read_podcast_inbox_sort_mode(
                raw.get("podcast_inbox_sort_mode")
            ),
            podcast_library_sort_mode=_read_podcast_library_sort_mode(
                raw.get("podcast_library_sort_mode")
            ),
            youtube_live_channel_ids=normalize_youtube_live_channel_ids(
                raw.get("youtube_live_channel_ids")
            ),
            tidal_queue_items=_read_tidal_queue(raw.get("tidal_queue_items")),
            tidal_sort_modes=_read_tidal_sort_modes(raw.get("tidal_sort_modes")),
            spotify_sort_modes=_read_spotify_sort_modes(
                raw.get("spotify_sort_modes")
            ),
            spotify_playlist_destination_ids=normalize_destination_ids(
                raw.get("spotify_playlist_destination_ids")
            ),
            service_picker_destination_ids=(
                _read_service_picker_destination_ids(
                    raw.get("service_picker_destination_ids"),
                    raw.get("spotify_playlist_destination_ids"),
                )
            ),
            search_history=_read_search_history(raw.get("search_history")),
            spotify_playback_history=_read_spotify_history(
                raw.get("spotify_playback_history")
            ),
            playback_history_by_session=normalize_histories(
                raw.get("playback_history_by_session")
            ),
        )

    # -------------------------------------------------------------- zapis

    def to_payload(self, state: LiteState) -> dict:
        """Znormalizuj stan do jednego, wersjonowanego dokumentu JSON."""

        state.options.clamp()
        options_payload = asdict(state.options)
        # Stary, jednojęzyczny klucz był zawsze polski. Po migracji jego dane
        # są już w ``message_templates_by_language.pl`` i nie zapisujemy
        # dwóch sprzecznych źródeł prawdy.
        options_payload.pop("message_templates", None)
        payload = {
            "version": self.VERSION,
            "session_manager_version": self.SESSION_MANAGER_VERSION,
            "playback_rate_version": self.PLAYBACK_RATE_VERSION,
            "options": options_payload,
            "stations": [asdict(station) for station in state.stations],
            "navigation": state.navigation,
            "shortcut_overrides": serialize_shortcut_overrides(
                normalize_shortcut_overrides(state.shortcut_overrides)
            ),
            "shuffle_enabled_by_session": _read_shuffle_enabled(
                state.shuffle_enabled_by_session
            ),
            # Puste wpisy sa USUWANE (jak cs:93): w pliku nie moga zostawac
            # wartosci nieodroznialne od braku decyzji uzytkownika.
            "session_overrides": {
                key: overrides.to_payload()
                for key, overrides in (state.session_overrides or {}).items()
                if not overrides.is_empty
            },
            "audio_output_device_ids_by_session": read_output_overrides(
                state.audio_output_device_ids_by_session
            ),
            "recording_history": _read_recording_history(state.recording_history),
            "recognized_tracks": read_tracks(state.recognized_tracks),
            "recognized_tracks_imported_from_amc": bool(
                state.recognized_tracks_imported_from_amc
            ),
            "clip_selections": read_clip_selections(state.clip_selections),
            # NIE usuwamy pustych list. Dla presetow pusty wpis sesji jest
            # swiadoma decyzja i rozni sie od braku prywatnego nadpisania.
            "preset_overrides": read_preset_overrides(state.preset_overrides),
        }
        # Brak klucza oznacza dalsze dziedziczenie profilu. Pusta lista jest
        # natomiast wazna decyzja uzytkownika i musi zostac zapisana.
        if state.radio_schedule_overrides is not None:
            payload["radio_schedule_overrides"] = (
                read_schedule_overrides(state.radio_schedule_overrides) or []
            )
        if type(state.radio_schedule_wake_override) is bool:
            payload["radio_schedule_wake_override"] = (
                state.radio_schedule_wake_override
            )
        podcast_sort_mode = _read_podcast_inbox_sort_mode(
            state.podcast_inbox_sort_mode
        )
        if podcast_sort_mode is not None:
            payload["podcast_inbox_sort_mode"] = podcast_sort_mode
        podcast_library_sort_mode = _read_podcast_library_sort_mode(
            state.podcast_library_sort_mode
        )
        if podcast_library_sort_mode is not None:
            payload["podcast_library_sort_mode"] = podcast_library_sort_mode
        youtube_live_channel_ids = normalize_youtube_live_channel_ids(
            state.youtube_live_channel_ids
        )
        if youtube_live_channel_ids:
            payload["youtube_live_channel_ids"] = youtube_live_channel_ids
        payload["tidal_queue_items"] = _read_tidal_queue(state.tidal_queue_items)
        tidal_sort_modes = _read_tidal_sort_modes(state.tidal_sort_modes)
        if tidal_sort_modes:
            payload["tidal_sort_modes"] = tidal_sort_modes
        spotify_sort_modes = _read_spotify_sort_modes(state.spotify_sort_modes)
        if spotify_sort_modes:
            payload["spotify_sort_modes"] = spotify_sort_modes
        remembered_destinations = normalize_remembered_destinations(
            state.service_picker_destination_ids
        )
        legacy_spotify_ids = remembered_destinations.get(
            "spotify:playlists",
            normalize_destination_ids(state.spotify_playlist_destination_ids),
        )
        payload["service_picker_destination_ids"] = remembered_destinations
        # Jeden cykl zgodności pozwala na bezpieczny powrót do starszej paczki.
        payload["spotify_playlist_destination_ids"] = legacy_spotify_ids
        payload["search_history"] = _read_search_history(state.search_history)
        payload["spotify_playback_history"] = _read_spotify_history(
            state.spotify_playback_history
        )
        payload["playback_history_by_session"] = normalize_histories(
            state.playback_history_by_session
        )
        return payload

    def save(self, state: LiteState) -> None:
        """Zapis ATOMOWY. Nie zostawia obcietego pliku przy przerwaniu."""

        self.directory.mkdir(parents=True, exist_ok=True)
        payload = self.to_payload(state)
        text = json.dumps(payload, ensure_ascii=False, indent=2)

        # Plik tymczasowy MUSI lezec w tym samym folderze: os.replace jest
        # atomowy tylko w obrebie jednego wolumenu.
        handle, temporary = tempfile.mkstemp(
            dir=str(self.directory), prefix=".state-", suffix=".tmp"
        )
        try:
            with os.fdopen(handle, "w", encoding="utf-8") as stream:
                stream.write(text)
                stream.flush()
                os.fsync(stream.fileno())
            os.replace(temporary, self.path)
        except BaseException:
            try:
                os.unlink(temporary)
            except OSError:
                pass
            raise


# ------------------------------------------------------- operacje na stacjach


class StationList:
    """Lista stacji uzytkownika z regułami: bez duplikatow adresu, bez pustych."""

    def __init__(self, stations: list[Station] | None = None) -> None:
        self.stations: list[Station] = list(stations or [])

    def __len__(self) -> int:
        return len(self.stations)

    def add(self, name: str, url: str) -> Station:
        url = url.strip()
        name = name.strip()
        if not url:
            raise ValueError("Adres stacji nie moze byc pusty.")
        if not url.lower().startswith(("http://", "https://")):
            raise ValueError("Adres stacji musi zaczynac sie od http:// albo https://")
        existing = self.find_by_url(url)
        if existing is not None:
            raise ValueError(f"Ta stacja jest juz na liscie: {existing.name}")
        station = Station.new(name or url, url)
        self.stations.append(station)
        return station

    def edit(self, station_id: str, name: str, url: str) -> Station:
        station = self.find(station_id)
        if station is None:
            raise KeyError(station_id)
        url = url.strip()
        name = name.strip()
        if not url.lower().startswith(("http://", "https://")):
            raise ValueError("Adres stacji musi zaczynac sie od http:// albo https://")
        clash = self.find_by_url(url)
        if clash is not None and clash.id != station_id:
            raise ValueError(f"Ten adres ma juz stacja: {clash.name}")
        station.name = name or url
        station.url = url
        return station

    def remove(self, station_id: str) -> Station:
        station = self.find(station_id)
        if station is None:
            raise KeyError(station_id)
        self.stations.remove(station)
        return station

    def find(self, station_id: str) -> Station | None:
        return next((s for s in self.stations if s.id == station_id), None)

    def find_by_url(self, url: str) -> Station | None:
        needle = url.strip().casefold()
        return next((s for s in self.stations if s.url.casefold() == needle), None)

    def merge_imported(self, imported: list[dict]) -> tuple[int, int]:
        """Dolacz stacje z importu (M3U/PLS liczony przez HOST, nie tutaj).

        Zwraca (dodane, pominiete_duplikaty). Istniejacych wpisow NIE zmieniamy:
        wlasna lista uzytkownika ma pierwszenstwo nad plikiem.
        """
        added = 0
        skipped = 0
        for entry in imported:
            name = str(entry.get("name") or "").strip()
            url = str(entry.get("url") or "").strip()
            if not url:
                skipped += 1
                continue
            try:
                self.add(name, url)
            except ValueError:
                skipped += 1
            else:
                added += 1
        return added, skipped

    def as_payload(self) -> list[dict]:
        """Format dla modelu listy (``rows_from_stations``)."""
        return [{"id": s.id, "name": s.name, "url": s.url} for s in self.stations]
