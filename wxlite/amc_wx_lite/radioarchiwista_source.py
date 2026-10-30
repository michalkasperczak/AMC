"""Źródło sesji Radio Archiwista: publiczny katalog nagrań do odsłuchu.

Moduł celowo nie zna wx ani hosta: parsuje katalog, buduje wiersze listy,
przechowuje ulubione i pozycje odtwarzania oraz pobiera nagrania na dysk.
Dzięki temu cała logika jest testowalna bez okna i bez sieci.

Katalog jest publiczny i shardowany (catalog_version 2):

- ``data/stations.json``: lista stacji i metadane świeżości;
- ``data/stations/{slug}/index.json``: lata;
- ``data/stations/{slug}/{rok}/index.json``: miesiące;
- ``data/stations/{slug}/{rok}/{miesiac}/index.json``: dni;
- ``data/stations/{slug}/{rok}/{miesiac}/{dzien}.json``: nagrania godzina po
  godzinie, z bezwzględnym adresem MP3 na archive.org.

Szablony ścieżek w katalogu są WZGLĘDNE. Rozwiązujemy je względem adresu
bazowego i NAJPIERW podstawiamy parametry: pójście szablonem bez
podstawienia kończy się 404, które wygląda jak błąd danych, a jest błędem
adresu.
"""

from __future__ import annotations

import hashlib
import json
import re
import time
import urllib.error
import urllib.parse
import urllib.request
from collections.abc import Callable
from dataclasses import dataclass, field, replace
from pathlib import Path
from typing import Any

from . import i18n
from .list_model import Row


#: Wartość ``SessionId`` sesji. Trzyma się konwencji camelCase enuma.
SESSION_VALUE = "radioarchiwista"

#: Stabilna, publiczna baza katalogu. Konfigurowalna per profil.
DEFAULT_CATALOG_BASE = "https://radioarchiwista.github.io/"

#: Katalog, który rozumiemy. Nowsza wersja to uczciwa odmowa, nie zgadywanie.
SUPPORTED_CATALOG_VERSION = 2

#: Ile wierszy materiału wczytujemy na jedną stronę listy.
PAGE_SIZE = 25

#: Odświeżamy indeks stacji najwyżej tak często; shardy dnia mogą rosnąć.
STATIONS_TTL_SECONDS = 30 * 60
SHARD_TTL_SECONDS = 15 * 60

_FETCH_TIMEOUT_SECONDS = 20.0
_DOWNLOAD_TIMEOUT_SECONDS = 60.0
_DOWNLOAD_CHUNK_BYTES = 256 * 1024

_SLUG_PATTERN = re.compile(r"^[A-Za-z0-9][A-Za-z0-9._-]*$")


class RadioarchiwistaError(RuntimeError):
    """Błąd z komunikatem przeznaczonym wprost do odczytania przez NVDA."""


def _clean_text(value: object, fallback: str = "") -> str:
    if not isinstance(value, str):
        return fallback
    cleaned = " ".join(value.replace("\x00", " ").split())
    return cleaned or fallback


def _as_int(value: object, fallback: int = 0) -> int:
    if isinstance(value, bool):
        return fallback
    if isinstance(value, int):
        return value
    if isinstance(value, float):
        return int(value)
    # Nagłówki HTTP (np. Content-Length) są napisami: to nie błąd danych.
    if isinstance(value, str) and value.strip().isdigit():
        return int(value.strip())
    return fallback


def _as_float(value: object, fallback: float = 0.0) -> float:
    if isinstance(value, (int, float)) and not isinstance(value, bool):
        return float(value)
    return fallback


def check_slug(slug: str) -> str:
    """Waliduj identyfikator stacji, zanim trafi do ścieżki katalogu."""

    if not _SLUG_PATTERN.match(slug or ""):
        raise RadioarchiwistaError("Nieprawidłowy identyfikator stacji katalogu")
    return slug


def resolve_catalog_path(base_url: str, template: str, **params: Any) -> str:
    """Rozwiąż szablon ścieżki katalogu względem adresu bazowego.

    Najpierw podstawiamy parametry (``{slug}`` i spółka), dopiero potem
    doklejamy względną ścieżkę do bazy. Odwrotna kolejność produkuje 404
    z literałem ``{slug}`` w adresie.
    """

    rendered = template.format(**params) if params else template
    return urllib.parse.urljoin(base_url.rstrip("/") + "/", rendered)


def recording_item_id(station_slug: str, utc_started_at: str) -> str:
    """Stabilny identyfikator nagrania między odświeżeniami listy."""

    stamped = _clean_text(utc_started_at).replace(":", "-").replace(" ", "T")
    return f"rarch:rec:{station_slug}:{stamped}"


def station_item_id(station_slug: str) -> str:
    return f"rarch:station:{check_slug(station_slug)}"


@dataclass(frozen=True, slots=True)
class StationRecord:
    slug: str
    display_name: str
    public_name: str
    archive_count: int = 0
    timezone: str = ""


@dataclass(frozen=True, slots=True)
class StationsIndex:
    stations: tuple[StationRecord, ...]
    catalog_version: int
    generated_at: str = ""
    latest_hour_started_at: str = ""

    def find(self, slug: str) -> StationRecord | None:
        for station in self.stations:
            if station.slug == slug:
                return station
        return None


@dataclass(frozen=True, slots=True)
class CatalogResult:
    """Wynik wczytania poziomu katalogu wraz z uczciwą informacją o źródle."""

    payload: dict
    from_cache: bool = False
    fetched_at: float = 0.0


@dataclass(frozen=True, slots=True)
class ArchiveRecording:
    station_slug: str
    station_name: str
    item_id: str
    title: str
    audio_url: str
    duration_seconds: float
    local_date_label: str
    local_hour_label: str
    local_label: str
    utc_started_at: str
    hour: int
    completeness_ratio: float = 1.0
    status: str = "published"

    @property
    def is_complete(self) -> bool:
        return self.completeness_ratio >= 0.995


def _require_mapping(payload: object, what: str) -> dict:
    if not isinstance(payload, dict):
        raise RadioarchiwistaError(f"Katalog Radio Archiwisty: nieprawidłowy plik {what}")
    return payload


def parse_stations_index(payload: object) -> StationsIndex:
    data = _require_mapping(payload, "indeksu stacji")
    version = _as_int(data.get("catalog_version"), -1)
    if version != SUPPORTED_CATALOG_VERSION:
        raise RadioarchiwistaError(
            "Katalog Radio Archiwisty ma nieznaną wersję. Zaktualizuj AMC."
        )
    raw_stations = data.get("stations")
    if not isinstance(raw_stations, list) or not raw_stations:
        raise RadioarchiwistaError("Katalog Radio Archiwisty nie zawiera stacji")
    stations: list[StationRecord] = []
    seen: set[str] = set()
    for raw in raw_stations:
        if not isinstance(raw, dict):
            continue
        slug = _clean_text(raw.get("slug"))
        if not slug or slug in seen:
            continue
        seen.add(slug)
        stations.append(StationRecord(
            slug=slug,
            display_name=_clean_text(raw.get("display_name"), slug),
            public_name=_clean_text(raw.get("public_name"), _clean_text(raw.get("display_name"), slug)),
            archive_count=_as_int(raw.get("archive_count")),
            timezone=_clean_text(raw.get("timezone")),
        ))
    if not stations:
        raise RadioarchiwistaError("Katalog Radio Archiwisty nie zawiera stacji")
    return StationsIndex(
        stations=tuple(stations),
        catalog_version=version,
        generated_at=_clean_text(data.get("generated_at")),
        latest_hour_started_at=_clean_text(data.get("latest_archive_hour_started_at")),
    )


def parse_years(payload: object) -> tuple[int, ...]:
    data = _require_mapping(payload, "indeksu lat")
    years = data.get("years")
    if not isinstance(years, list) or not years:
        raise RadioarchiwistaError("Katalog Radio Archiwisty: brak lat dla stacji")
    return tuple(sorted({_as_int(year) for year in years if _as_int(year) > 0}, reverse=True))


def parse_months(payload: object) -> tuple[int, ...]:
    data = _require_mapping(payload, "indeksu miesięcy")
    months = data.get("months")
    if not isinstance(months, list) or not months:
        raise RadioarchiwistaError("Katalog Radio Archiwisty: brak miesięcy")
    return tuple(sorted({_as_int(month) for month in months if 1 <= _as_int(month) <= 12}, reverse=True))


def parse_days(payload: object) -> tuple[int, ...]:
    data = _require_mapping(payload, "indeksu dni")
    days = data.get("days")
    if not isinstance(days, list) or not days:
        raise RadioarchiwistaError("Katalog Radio Archiwisty: brak dni")
    return tuple(sorted({_as_int(day) for day in days if 1 <= _as_int(day) <= 31}, reverse=True))


def _prefer_mp3_url(url: str) -> str:
    """Zamień końcówkę .ogg/.oga na .mp3.

    Archiwum 2025 i wcześniejszych lat przechowuje nagrania jako Ogg Vorbis,
    ale obok każdego pliku serwer archive.org wystawia transkod MP3 pod tą
    samą nazwą. Odtwarzacz sieciowy AMC idzie przez Media Foundation, który
    nie dekoduje OGG, więc sesja prosi o MP3 od razu; pobieranie również
    dostaje plik o powszechnie odtwarzalnym formacie.
    """

    if url.lower().endswith((".ogg", ".oga")):
        return url[: url.rfind(".")] + ".mp3"
    return url


def parse_recordings(payload: object, station: StationRecord) -> tuple[ArchiveRecording, ...]:
    data = _require_mapping(payload, "katalogu dnia")
    raw_archives = data.get("archives")
    if not isinstance(raw_archives, list):
        raise RadioarchiwistaError("Katalog Radio Archiwisty: nieprawidłowa lista nagrań")
    recordings: list[ArchiveRecording] = []
    seen: set[str] = set()
    for raw in raw_archives:
        if not isinstance(raw, dict):
            continue
        audio_url = _clean_text(raw.get("audio_url")) or _clean_text(raw.get("download_url"))
        utc_started_at = _clean_text(raw.get("utc_started_at"))
        if not audio_url or not utc_started_at or utc_started_at in seen:
            continue
        seen.add(utc_started_at)
        audio_url = _prefer_mp3_url(audio_url)
        date_label = _clean_text(raw.get("local_date_label"))
        hour_label = _clean_text(raw.get("local_hour_label"))
        item_id = recording_item_id(station.slug, utc_started_at)
        recordings.append(ArchiveRecording(
            station_slug=station.slug,
            station_name=station.public_name,
            item_id=item_id,
            title=_clean_text(raw.get("local_label")) or f"{date_label} {hour_label}".strip(),
            audio_url=audio_url,
            duration_seconds=_as_float(raw.get("duration_seconds")),
            local_date_label=date_label,
            local_hour_label=hour_label,
            local_label=_clean_text(raw.get("local_label")),
            utc_started_at=utc_started_at,
            hour=_as_int(raw.get("hour"), -1),
            completeness_ratio=_as_float(raw.get("completeness_ratio"), 1.0),
            status=_clean_text(raw.get("status"), "published"),
        ))
    recordings.sort(key=lambda rec: (rec.local_date_label, rec.hour, rec.utc_started_at))
    return tuple(recordings)


def station_rows(index: StationsIndex) -> list[Row]:
    """Wiersze poziomu stacji.

    Licznik nagrań wchodzi do TYTUŁU, a nie do kolumny „Szczegóły":
    czytnik ekranu ogłasza wtedy „Polskie Radio 24, 4916 nagrań" jednym
    ciągiem, zamiast wymieniać nagłówek kolumny przed liczbą.
    """

    return [
        Row(
            item_id=station_item_id(station.slug),
            title=(
                f"{station.public_name}, {i18n.recordings_phrase(station.archive_count)}"
                if station.archive_count
                else station.public_name
            ),
            kind="folder",
            detail="",
            show_kind=False,
            service_id=station.slug,
            service_kind="rarchStation",
        )
        for station in sorted(index.stations, key=lambda s: s.public_name.casefold())
    ]


def _period_rows(
    items: tuple[int, ...],
    item_id_prefix: str,
    *,
    service_kind: str,
    service_id: str,
    label_for: Callable[[int], str],
) -> list[Row]:
    # service_kind i service_id są OBOWIĄZKOWE: bez nich nawigacja nie
    # rozpoznaje poziomu katalogu i Enter kończy się „nie można otworzyć".
    return [
        Row(
            item_id=f"{item_id_prefix}:{value}",
            title=label_for(value),
            kind="folder",
            show_kind=False,
            service_kind=service_kind,
            service_id=service_id,
        )
        for value in items
    ]


def year_rows(slug: str, years: tuple[int, ...]) -> list[Row]:
    return _period_rows(
        years,
        f"rarch:years:{check_slug(slug)}",
        service_kind="rarchYear",
        service_id=check_slug(slug),
        label_for=str,
    )


def month_rows(slug: str, year: int, months: tuple[int, ...]) -> list[Row]:
    return _period_rows(
        months,
        f"rarch:months:{check_slug(slug)}:{year}",
        service_kind="rarchMonth",
        service_id=check_slug(slug),
        label_for=i18n.month_name,
    )


def day_rows(slug: str, year: int, month: int, days: tuple[int, ...]) -> list[Row]:
    return _period_rows(
        days,
        f"rarch:days:{check_slug(slug)}:{year}:{month}",
        service_kind="rarchDay",
        service_id=check_slug(slug),
        label_for=lambda day: f"{day:02d}.{month:02d}.{year}",
    )


def _recording_details(
    recording: ArchiveRecording,
    position: float,
) -> str:
    """Szczegóły wiersza nagrania w języku interfejsu."""

    details: list[str] = []
    if not recording.is_complete:
        details.append(i18n.tr("rarch.detail.incomplete"))
    if recording.status and recording.status != "published":
        details.append(i18n.tr(
            "rarch.detail.status", status=recording.status,
        ))
    if position > 1.0 and recording.duration_seconds > 0:
        remaining = max(0.0, recording.duration_seconds - position)
        details.append(i18n.tr(
            "rarch.detail.remaining", minutes=int(remaining // 60),
        ))
    return ", ".join(details)


def recording_rows(
    recordings: tuple[ArchiveRecording, ...],
    *,
    positions: dict[str, float] | None = None,
    favorite_ids: set[str] | None = None,
    loaded_count: int = PAGE_SIZE,
) -> list[Row]:
    """Wiersze nagrań jednego dnia, rosnąco wg godziny, ze stronnicowaniem."""

    positions = positions or {}
    favorite_ids = favorite_ids or set()
    rows: list[Row] = []
    for recording in recordings[:max(0, loaded_count)]:
        position = positions.get(recording.item_id, 0.0)
        rows.append(Row(
            item_id=recording.item_id,
            title=recording.local_hour_label or f"{recording.hour:02d}:00",
            kind="recording",
            detail=_recording_details(recording, position),
            show_kind=False,
            url=recording.audio_url,
            service_id=recording.station_slug,
            service_start_utc=recording.utc_started_at,
            position_seconds=position,
            duration_ticks=int(recording.duration_seconds * 10_000_000),
            state_prefix="★ " if recording.item_id in favorite_ids else "",
            parent_id=station_item_id(recording.station_slug),
        ))
    if len(recordings) > loaded_count:
        rows.append(Row(
            item_id="rarch:load-more",
            title=i18n.tr(
                "rarch.loadMore.row", shown=len(rows), total=len(recordings),
            ),
            kind="loadMore",
            show_kind=False,
        ))
    return rows


def latest_rows(
    recordings: tuple[ArchiveRecording, ...],
    *,
    positions: dict[str, float] | None = None,
    favorite_ids: set[str] | None = None,
) -> list[Row]:
    """Wiersze widoku „Najnowsze nagrania" ze wszystkich stacji.

    Tytuł MUSI nazywać stację: w przeciwieństwie do widoku dnia jednej
    stacji, sama godzina nie mówi, do której stacji należy nagranie.
    """

    positions = positions or {}
    favorite_ids = favorite_ids or set()
    rows: list[Row] = []
    for recording in recordings:
        date_label = recording.local_date_label
        hour_label = (
            recording.local_hour_label or f"{recording.hour:02d}:00"
            if recording.hour >= 0
            else ""
        )
        when = " ".join(part for part in (date_label, hour_label) if part)
        title = (
            f"{recording.station_name}, {when}" if when else recording.station_name
        )
        position = positions.get(recording.item_id, 0.0)
        rows.append(Row(
            item_id=recording.item_id,
            title=title,
            kind="recording",
            detail=_recording_details(recording, position),
            show_kind=False,
            url=recording.audio_url,
            service_id=recording.station_slug,
            service_start_utc=recording.utc_started_at,
            position_seconds=position,
            duration_ticks=int(recording.duration_seconds * 10_000_000),
            state_prefix="★ " if recording.item_id in favorite_ids else "",
            parent_id=station_item_id(recording.station_slug),
        ))
    return rows


def _recording_row_from_record(recording: ArchiveRecording) -> Row:
    return Row(
        item_id=recording.item_id,
        title=f"{recording.station_name}, {recording.title}",
        kind="recording",
        detail=recording.local_date_label,
        show_kind=False,
        url=recording.audio_url,
        service_id=recording.station_slug,
        service_start_utc=recording.utc_started_at,
        duration_ticks=int(recording.duration_seconds * 10_000_000),
        parent_id=station_item_id(recording.station_slug),
    )


def favorite_rows(
    favorites: list[dict],
    *,
    positions: dict[str, float] | None = None,
) -> list[Row]:
    """Wiersze ulubionych nagrań: najnowsze dodane na górze, bez sieci."""

    positions = positions or {}
    ordered = sorted(
        favorites,
        key=lambda item: (_as_float(item.get("added_at")), _clean_text(item.get("item_id"))),
        reverse=True,
    )
    rows = [
        _recording_row_from_record(_favorite_to_recording(item)) for item in ordered
    ]
    if positions:
        rows = [
            replace(row, position_seconds=positions.get(row.item_id, 0.0))
            for row in rows
        ]
    return rows


def _favorite_to_recording(item: dict) -> ArchiveRecording:
    return ArchiveRecording(
        station_slug=_clean_text(item.get("station_slug"), "?"),
        station_name=_clean_text(item.get("station_name"), "Radio Archiwista"),
        item_id=_clean_text(item.get("item_id"), "rarch:rec:nieznany"),
        title=_clean_text(item.get("title"), "Nagranie"),
        audio_url=_prefer_mp3_url(_clean_text(item.get("audio_url"))),
        duration_seconds=_as_float(item.get("duration_seconds")),
        local_date_label=_clean_text(item.get("local_date_label")),
        local_hour_label=_clean_text(item.get("local_hour_label")),
        local_label=_clean_text(item.get("title")),
        utc_started_at=_clean_text(item.get("utc_started_at")),
        hour=_as_int(item.get("hour"), -1),
        completeness_ratio=_as_float(item.get("completeness_ratio"), 1.0),
        status=_clean_text(item.get("status"), "published"),
    )


def favorite_record(recording: ArchiveRecording, added_at: float) -> dict:
    """Zapis ulubionego z polami wystarczającymi do odtworzenia bez katalogu."""

    return {
        "item_id": recording.item_id,
        "station_slug": recording.station_slug,
        "station_name": recording.station_name,
        "title": recording.title,
        "audio_url": recording.audio_url,
        "duration_seconds": recording.duration_seconds,
        "local_date_label": recording.local_date_label,
        "local_hour_label": recording.local_hour_label,
        "utc_started_at": recording.utc_started_at,
        "hour": recording.hour,
        "completeness_ratio": recording.completeness_ratio,
        "status": recording.status,
        "added_at": added_at,
    }


#: Pobieranie pliku strumieniowo, bez wx i bez wątków: wołane w tle GUI.
def stream_to_file(
    url: str,
    destination: Path,
    *,
    progress: Callable[[int], None] | None = None,
    should_cancel: Callable[[], bool] | None = None,
    fetch: Callable[[str], Any] | None = None,
) -> Path:
    """Pobierz nagranie do pliku ``.part``, potem atomowo zmień nazwę.

    Anulowanie i błąd sieci zostawzają katalog czysty: częściowy plik jest
    usuwany, a wywołujący dostaje uczciwy komunikat.
    """

    destination.parent.mkdir(parents=True, exist_ok=True)
    partial = destination.with_name(destination.name + ".part")
    opener = fetch or (lambda target: urllib.request.urlopen(target, timeout=_DOWNLOAD_TIMEOUT_SECONDS))
    try:
        with opener(url) as response, open(partial, "wb") as output:
            if response.status != 200:
                raise RadioarchiwistaError(
                    "Serwer nagrań odpowiedział błędem, nie pobrano pliku"
                )
            total = _as_int(response.headers.get("Content-Length"), 0)
            downloaded = 0
            while True:
                if should_cancel is not None and should_cancel():
                    raise RadioarchiwistaError("Pobieranie anulowane")
                chunk = response.read(_DOWNLOAD_CHUNK_BYTES)
                if not chunk:
                    break
                output.write(chunk)
                downloaded += len(chunk)
                if progress is not None and total > 0:
                    progress(min(100, downloaded * 100 // total))
        if partial.stat().st_size == 0:
            raise RadioarchiwistaError("Pobrany plik jest pusty")
        partial.replace(destination)
    except RadioarchiwistaError:
        partial.unlink(missing_ok=True)
        raise
    except urllib.error.URLError as error:
        partial.unlink(missing_ok=True)
        raise RadioarchiwistaError(
            f"Nie udało się pobrać nagrania: {error.reason}"
        ) from error
    except OSError as error:
        partial.unlink(missing_ok=True)
        raise RadioarchiwistaError(
            f"Nie udało się zapisać pobieranego nagrania: {error}"
        ) from error
    return destination


class RadioarchiwistaSource:
    """Dostęp do katalogu nagrań, ulubionych i pozycji odtwarzania.

    Wszystkie metody są blokujące i bezpieczne do wołania w wątku roboczym
    GUI. Plików nie czytamy przy konstrukcji: świeża instalacja bez katalogu
    też potrafi otworzyć sesję i dostać uczciwy komunikat o sieci.
    """

    def __init__(
        self,
        settings_dir: Path,
        *,
        fetch_json: Callable[[str], object] | None = None,
        clock: Callable[[], float] = time.time,
    ) -> None:
        self._dir = Path(settings_dir)
        self._cache_dir = self._dir / "cache"
        self._settings_path = self._dir / "settings.json"
        self._favorites_path = self._dir / "favorites.json"
        self._positions_path = self._dir / "positions.json"
        self._clock = clock
        #: Materiały ostatnio wczytanych dni; akcje na zaznaczonym wierszu
        #: nie pobierają niczego drugi raz.
        self._recent_recordings: dict[str, ArchiveRecording] = {}
        if fetch_json is None:
            def _default_fetch(url: str) -> object:
                with urllib.request.urlopen(url, timeout=_FETCH_TIMEOUT_SECONDS) as response:
                    return json.loads(response.read().decode("utf-8"))
            self._fetch_json = _default_fetch
        else:
            self._fetch_json = fetch_json

    # -- Ustawienia ------------------------------------------------------

    def read_settings(self) -> dict:
        try:
            data = json.loads(self._settings_path.read_text(encoding="utf-8"))
        except (OSError, ValueError):
            return {}
        return data if isinstance(data, dict) else {}

    def write_settings(self, **values: Any) -> None:
        data = self.read_settings()
        data.update(values)
        self._dir.mkdir(parents=True, exist_ok=True)
        self._settings_path.write_text(
            json.dumps(data, ensure_ascii=False, indent=2),
            encoding="utf-8",
        )

    def catalog_base(self) -> str:
        base = _clean_text(self.read_settings().get("catalog_base"))
        return base or DEFAULT_CATALOG_BASE

    # -- Katalog ---------------------------------------------------------

    def _cache_path(self, url: str) -> Path:
        digest = hashlib.sha1(url.encode("utf-8")).hexdigest()
        return self._cache_dir / f"{digest}.json"

    def _load_cached(
        self,
        url: str,
        *,
        ttl_seconds: float,
        force: bool,
    ) -> CatalogResult | None:
        path = self._cache_path(url)
        try:
            raw = path.read_text(encoding="utf-8")
            fetched_at = path.stat().st_mtime
        except OSError:
            return None
        age = self._clock() - fetched_at
        if force or age > ttl_seconds:
            return None
        try:
            payload = json.loads(raw)
        except ValueError:
            return None
        return CatalogResult(payload=payload, from_cache=True, fetched_at=fetched_at)

    def _store_cached(self, url: str, payload: dict) -> None:
        self._cache_dir.mkdir(parents=True, exist_ok=True)
        self._cache_path(url).write_text(
            json.dumps(payload, ensure_ascii=False),
            encoding="utf-8",
        )

    def fetch_level(
        self,
        template: str,
        *,
        ttl_seconds: float,
        force: bool,
        label: str,
        **params: Any,
    ) -> tuple[dict, bool]:
        """Wczytaj jeden plik katalogu: sieć, potem padające na cache."""

        url = resolve_catalog_path(self.catalog_base(), template, **params)
        cached = self._load_cached(url, ttl_seconds=ttl_seconds, force=force)
        if cached is not None:
            return cached.payload, True
        try:
            payload = self._fetch_json(url)
        except (OSError, ValueError) as error:
            stale = self._stale_cache(url)
            if stale is not None:
                return stale, True
            raise RadioarchiwistaError(
                f"Nie udało się wczytać {label}: {error}"
            ) from error
        data = _require_mapping(payload, label)
        self._store_cached(url, data)
        return data, False

    def _stale_cache(self, url: str) -> dict | None:
        try:
            payload = json.loads(self._cache_path(url).read_text(encoding="utf-8"))
        except (OSError, ValueError):
            return None
        return payload if isinstance(payload, dict) else None

    def stations(self, *, force: bool = False) -> tuple[StationsIndex, bool]:
        payload, from_cache = self.fetch_level(
            "./data/stations.json",
            ttl_seconds=STATIONS_TTL_SECONDS,
            force=force,
            label="listę stacji",
        )
        return parse_stations_index(payload), from_cache

    def years(self, slug: str, *, force: bool = False) -> tuple[tuple[int, ...], bool]:
        payload, from_cache = self.fetch_level(
            "./data/stations/{slug}/index.json",
            ttl_seconds=SHARD_TTL_SECONDS,
            force=force,
            label="lata stacji",
            slug=check_slug(slug),
        )
        return parse_years(payload), from_cache

    def months(self, slug: str, year: int, *, force: bool = False) -> tuple[tuple[int, ...], bool]:
        payload, from_cache = self.fetch_level(
            "./data/stations/{slug}/{year}/index.json",
            ttl_seconds=SHARD_TTL_SECONDS,
            force=force,
            label="miesiące stacji",
            slug=check_slug(slug),
            year=year,
        )
        return parse_months(payload), from_cache

    def days(self, slug: str, year: int, month: int, *, force: bool = False) -> tuple[tuple[int, ...], bool]:
        payload, from_cache = self.fetch_level(
            "./data/stations/{slug}/{year}/{month}/index.json",
            ttl_seconds=SHARD_TTL_SECONDS,
            force=force,
            label="dni stacji",
            slug=check_slug(slug),
            year=year,
            month=month,
        )
        return parse_days(payload), from_cache

    def recordings(
        self,
        slug: str,
        year: int,
        month: int,
        day: int,
        *,
        force: bool = False,
    ) -> tuple[tuple[ArchiveRecording, ...], bool]:
        index, _ = self.stations(force=False)
        station = index.find(slug)
        if station is None:
            raise RadioarchiwistaError("Tej stacji nie ma już w katalogu")
        payload, from_cache = self.fetch_level(
            "./data/stations/{slug}/{year}/{month}/{day}.json",
            ttl_seconds=SHARD_TTL_SECONDS,
            force=force,
            label="nagrania dnia",
            slug=check_slug(slug),
            year=year,
            month=month,
            day=day,
        )
        parsed = parse_recordings(payload, station)
        if len(self._recent_recordings) > 5000:
            self._recent_recordings.clear()
        for recording in parsed:
            self._recent_recordings[recording.item_id] = recording
        return parsed, from_cache

    def latest_day(
        self,
        slug: str,
        *,
        force: bool = False,
    ) -> tuple[tuple[ArchiveRecording, ...], int, int, int, bool]:
        """Najnowszy dzień stacji: skok pod Enter na stacji.

        Cztery małe pliki katalogu zamiast chodzić po całym drzewku ręcznie.
        Zwraca nagrania dnia oraz rok, miesiąc i dzień do nawigacji wstecz.
        """

        years, _ = self.years(slug, force=force)
        if not years:
            raise RadioarchiwistaError("Katalog nie zna lat tej stacji")
        year = years[0]
        months, _ = self.months(slug, year, force=force)
        if not months:
            raise RadioarchiwistaError("Katalog nie zna miesięcy tej stacji")
        month = months[0]
        days, _ = self.days(slug, year, month, force=force)
        if not days:
            raise RadioarchiwistaError("Katalog nie zna dni tej stacji")
        day = days[0]
        recordings, from_cache = self.recordings(slug, year, month, day, force=force)
        return recordings, year, month, day, from_cache

    def recording_by_id(self, item_id: str) -> ArchiveRecording | None:
        """Materiał dla akcji na zaznaczonym wierszu, bez drugiego pobierania."""

        found = self._recent_recordings.get(item_id)
        if found is not None:
            return found
        for item in self.load_favorites():
            if item.get("item_id") == item_id:
                return _favorite_to_recording(item)
        return None

    def latest_recordings(
        self,
        *,
        force: bool = False,
    ) -> tuple[tuple[ArchiveRecording, ...], str]:
        """Nagrania ostatniej opublikowanej godziny ze stacji, które ją nagrały.

        Jednym Enterem słucha się „tej godziny” bez wchodzenia w drzewko
        stacji. Stacje bez tego dnia w katalogu są po prostu pomijane:
        niespójności publicznego katalogu nie mogą blokować całego widoku.
        """

        index, _ = self.stations(force=force)
        stamp = index.latest_hour_started_at
        if not stamp:
            raise RadioarchiwistaError(
                "Katalog nie podaje najnowszej godziny nagrań"
            )
        date_parts = stamp[:10].split("-")
        if len(date_parts) != 3:
            raise RadioarchiwistaError(
                "Katalog podaje niezrozumiałą godzinę nagrań"
            )
        try:
            year, month, day = (int(part) for part in date_parts)
        except ValueError as error:
            raise RadioarchiwistaError(
                "Katalog podaje niezrozumiałą godzinę nagrań"
            ) from error
        hour_stamp = stamp[:13]
        collected: list[ArchiveRecording] = []
        for station in index.stations:
            try:
                recordings, _ = self.recordings(
                    station.slug, year, month, day, force=force
                )
            except RadioarchiwistaError:
                continue
            for recording in recordings:
                if recording.utc_started_at[:13] == hour_stamp:
                    collected.append(recording)
        if not collected:
            raise RadioarchiwistaError(
                "Żadna stacja nie ma jeszcze nagrań ostatniej godziny"
            )
        collected.sort(key=lambda rec: (rec.station_name.casefold(), rec.item_id))
        return tuple(collected), stamp

    # -- Ulubione --------------------------------------------------------

    def search_rows(self) -> list[Row]:
        """Wiersze do globalnego wyszukiwania AMC: tylko zapisane dane.

        Stacje dokłada wyłącznie z ważnego cache (wyszukiwanie nie może
        schodzić do sieci), ulubione nagrania zawsze, bo mieszkają
        w profilu.
        """
        url = resolve_catalog_path(self.catalog_base(), "./data/stations.json")
        rows: list[Row] = []
        cached = self._load_cached(
            url, ttl_seconds=STATIONS_TTL_SECONDS, force=False,
        )
        if cached is not None:
            rows.extend(station_rows(parse_stations_index(cached.payload)))
        rows.extend(favorite_rows(
            self.load_favorites(),
            positions=self.load_positions(),
        ))
        return rows

    def load_favorites(self) -> list[dict]:
        try:
            data = json.loads(self._favorites_path.read_text(encoding="utf-8"))
        except (OSError, ValueError):
            return []
        if not isinstance(data, list):
            return []
        return [item for item in data if isinstance(item, dict)]

    def save_favorites(self, favorites: list[dict]) -> None:
        self._dir.mkdir(parents=True, exist_ok=True)
        self._favorites_path.write_text(
            json.dumps(favorites, ensure_ascii=False, indent=2),
            encoding="utf-8",
        )

    def toggle_favorite(self, recording: ArchiveRecording) -> bool:
        """Przełącz ulubione nagranie. Zwraca stan PO zmianie (True = dodane)."""

        favorites = self.load_favorites()
        remaining = [
            item for item in favorites
            if _clean_text(item.get("item_id")) != recording.item_id
        ]
        added = len(remaining) != len(favorites)
        if not added:
            remaining.append(favorite_record(recording, self._clock()))
        self.save_favorites(remaining)
        return not added

    def is_favorite(self, item_id: str) -> bool:
        return any(
            _clean_text(item.get("item_id")) == item_id
            for item in self.load_favorites()
        )

    def favorite_ids(self) -> set[str]:
        return {
            _clean_text(item.get("item_id"))
            for item in self.load_favorites()
        }

    # -- Pozycje odtwarzania ----------------------------------------------

    def load_positions(self) -> dict[str, float]:
        try:
            data = json.loads(self._positions_path.read_text(encoding="utf-8"))
        except (OSError, ValueError):
            return {}
        if not isinstance(data, dict):
            return {}
        return {
            str(key): _as_float(value)
            for key, value in data.items()
            if _as_float(value) > 0.0
        }

    def save_position(self, item_id: str, position_seconds: float) -> None:
        positions = self.load_positions()
        position = max(0.0, _as_float(position_seconds))
        if position <= 1.0:
            positions.pop(item_id, None)
        else:
            positions[item_id] = position
        self._dir.mkdir(parents=True, exist_ok=True)
        self._positions_path.write_text(
            json.dumps(positions, ensure_ascii=False, indent=2),
            encoding="utf-8",
        )

    def position_for(self, item_id: str) -> float:
        return self.load_positions().get(item_id, 0.0)

    def clear_position(self, item_id: str) -> None:
        self.save_position(item_id, 0.0)
