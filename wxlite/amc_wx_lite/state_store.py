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
    last_folder: str | None = None

    def clamp(self) -> "Options":
        """Trzymaj wartosci w granicach, ktore silnik faktycznie przyjmuje."""
        self.volume = max(0, min(100, int(self.volume)))
        self.rate = max(0.5, min(2.0, float(self.rate)))
        self.timeshift_minutes = max(1, min(720, int(self.timeshift_minutes)))
        # Te same dozwolone wartosci co PlaybackAudioSettingsRules w Core.
        allowed = (0, 500, 1000, 2000, 3000, 5000)
        if self.inter_track_silence_ms not in allowed:
            self.inter_track_silence_ms = 0
        return self


@dataclass(slots=True)
class LiteState:
    options: Options = field(default_factory=Options)
    stations: list[Station] = field(default_factory=list)
    navigation: dict = field(default_factory=dict)


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
        for key, value in (raw.get("options") or {}).items():
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
