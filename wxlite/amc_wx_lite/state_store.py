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
from .profile_presets import read_preset_overrides


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
    loudness_normalization: bool = False
    smooth_track_transitions: bool = False
    inter_track_silence_ms: int = 0
    tempo_algorithm: int = 1
    last_folder: str | None = None
    #: Czy lista stacji ma czytac POZYCJE wiersza ("3 z 37").
    #:
    #: Domysl ``False`` jest uzgodniony: na liscie radia liczy sie nazwa
    #: stacji, a licznik powtarzany przy kazdej strzalce wydluza odczyt.
    #: Wykonawca ukrywania jest nakladka NVDA (``radio_position``), bo licznik
    #: pochodzi z NATYWNEGO ``positionInfo`` czytnika, nie z naszych
    #: komunikatow. Ustawienie jest PRYWATNE dla portu wx -- pelne AMC nie
    #: dostaje tu zadnego nowego wariantu.
    radio_announce_position: bool = False
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

    def audio_payload(self) -> dict:
        return {
            "tempoAlgorithm": self.tempo_algorithm,
            "loudnessNormalization": self.loudness_normalization,
            "smoothTrackTransitions": self.smooth_track_transitions,
            "interTrackSilenceMs": self.inter_track_silence_ms,
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
        # STRICT ``is not bool``, nie ``bool(...)``: napis "tak" albo "false" z
        # recznie poprawionego pliku jest prawdziwy po rzutowaniu i wlaczylby
        # licznik po cichu. Obca wartosc wraca do uzgodnionego domyslu.
        if type(self.radio_announce_position) is not bool:
            self.radio_announce_position = False
        # Ten sam STRICT powod co wyzej: napis z recznie poprawionego pliku nie
        # moze po cichu zmienic tego, czy Escape zatrzymuje odtwarzanie.
        if type(self.pause_on_player_exit) is not bool:
            self.pause_on_player_exit = True
        if type(self.follow_playback_on_player_exit) is not bool:
            self.follow_playback_on_player_exit = True
        if type(self.open_player_when_activating_preset) is not bool:
            self.open_player_when_activating_preset = False
        if type(self.stay_on_list_after_radio_enter) is not bool:
            self.stay_on_list_after_radio_enter = False
        return self


@dataclass(slots=True)
class LiteState:
    options: Options = field(default_factory=Options)
    stations: list[Station] = field(default_factory=list)
    navigation: dict = field(default_factory=dict)
    #: Opcje sesji: wybory uzytkownika per sesja (``files`` / ``radio``).
    #: Pusty slownik = kazda sesja dziedziczy ustawienia ogolne, czyli
    #: zachowanie sprzed tego przyrostu.
    session_overrides: dict = field(default_factory=dict)
    #: Prywatna historia prob nagrywania wxPython. Nie trafia do profilu
    #: pelnego AMC, ale przezywa ponowne uruchomienie lekkiego interfejsu.
    #: Wartosci sa zwyklymi danymi protokolu; etykiety NVDA powstaja dopiero
    #: w ``radio_recording.py``.
    recording_history: list[dict] = field(default_factory=list)
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
        return Path(appdata) / "AMC-wx-Lite"
    return Path.home() / ".config" / "amc-wx-lite"


class StateStore:
    """Czyta i zapisuje prywatny stan lekkiego wariantu."""

    FILE_NAME = "state.json"
    VERSION = 1

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
        if not isinstance(raw, dict):
            return LiteState()

        options = Options()
        raw_options = raw.get("options")
        if not isinstance(raw_options, dict):
            raw_options = {}
        for key, value in raw_options.items():
            if hasattr(options, key) and not isinstance(value, (dict, list)):
                try:
                    setattr(options, key, value)
                except (TypeError, ValueError):
                    pass
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
        return LiteState(
            options=options,
            stations=stations,
            navigation=navigation if isinstance(navigation, dict) else {},
            session_overrides=_read_session_overrides(raw.get("session_overrides")),
            recording_history=_read_recording_history(raw.get("recording_history")),
            clip_selections=read_clip_selections(raw.get("clip_selections")),
            preset_overrides=read_preset_overrides(raw.get("preset_overrides")),
        )

    # -------------------------------------------------------------- zapis

    def save(self, state: LiteState) -> None:
        """Zapis ATOMOWY. Nie zostawia obcietego pliku przy przerwaniu."""
        self.directory.mkdir(parents=True, exist_ok=True)
        payload = {
            "version": self.VERSION,
            "options": asdict(state.options),
            "stations": [asdict(station) for station in state.stations],
            "navigation": state.navigation,
            # Puste wpisy sa USUWANE (jak cs:93): w pliku nie moga zostawac
            # wartosci nieodroznialne od braku decyzji uzytkownika.
            "session_overrides": {
                key: overrides.to_payload()
                for key, overrides in (state.session_overrides or {}).items()
                if not overrides.is_empty
            },
            "recording_history": _read_recording_history(state.recording_history),
            "clip_selections": read_clip_selections(state.clip_selections),
            # NIE usuwamy pustych list. Dla presetow pusty wpis sesji jest
            # swiadoma decyzja i rozni sie od braku prywatnego nadpisania.
            "preset_overrides": read_preset_overrides(state.preset_overrides),
        }
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
