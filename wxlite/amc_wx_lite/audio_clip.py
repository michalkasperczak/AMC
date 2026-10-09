"""Zaznaczenie fragmentu audio zgodne z modelem pelnego AMC.

Ten modul nie tnie ani nie dekoduje dzwieku. Trzyma jedynie dwie granice i
tworzy celowe, uzytkowe etykiety. Faktyczny eksport wykonuje wspolny silnik C#
``AudioClipExporter`` przez waski protokol LiteHost.
"""

from __future__ import annotations

import math
from dataclasses import dataclass
from pathlib import Path
from typing import Callable


MAX_SAVED_CLIP_SELECTIONS = 1_000


@dataclass(slots=True)
class AudioClipSelection:
    """Niedestrukcyjne granice jednego lokalnego materialu.

    Reguly sa lustrzanym portem
    ``AccessibleMediaController.Core.Playback.AudioClipSelection``:
    pozycje sa przycinane do zakresu pliku, poczatek musi byc przed koncem,
    a przejscie do innego materialu zeruje obie granice.
    """

    item_id: str | None = None
    source_path: str | None = None
    start_seconds: float | None = None
    end_seconds: float | None = None

    @property
    def is_complete(self) -> bool:
        return (
            self.start_seconds is not None
            and self.end_seconds is not None
            and self.end_seconds > self.start_seconds
        )

    def matches(self, item_id: str, source_path: str) -> bool:
        return (
            self.item_id == item_id
            and isinstance(self.source_path, str)
            and self.source_path.casefold() == source_path.casefold()
        )

    def _begin_item(self, item_id: str, source_path: str) -> None:
        if not item_id.strip() or not source_path.strip():
            raise ValueError("Materiał i plik źródłowy muszą być wskazane.")
        if self.matches(item_id, source_path):
            return
        self.item_id = item_id
        self.source_path = source_path
        self.start_seconds = None
        self.end_seconds = None

    @staticmethod
    def _clamp(position_seconds: float, duration_seconds: float) -> float:
        position = _finite_nonnegative(position_seconds, fallback=0.0)
        duration = _finite_nonnegative(duration_seconds, fallback=0.0)
        return min(position, duration) if duration > 0.0 else position

    def set_start(
        self,
        item_id: str,
        source_path: str,
        position_seconds: float,
        duration_seconds: float,
    ) -> bool:
        self._begin_item(item_id, source_path)
        candidate = self._clamp(position_seconds, duration_seconds)
        if self.end_seconds is not None and candidate >= self.end_seconds:
            return False
        self.start_seconds = candidate
        return True

    def set_end(
        self,
        item_id: str,
        source_path: str,
        position_seconds: float,
        duration_seconds: float,
    ) -> bool:
        self._begin_item(item_id, source_path)
        candidate = self._clamp(position_seconds, duration_seconds)
        if self.start_seconds is not None and candidate <= self.start_seconds:
            return False
        self.end_seconds = candidate
        return True

    def find_relative_boundary(self, position_seconds: float, direction: int) -> float | None:
        if direction == 0:
            raise ValueError("Kierunek musi być różny od zera.")
        position = _finite_nonnegative(position_seconds, fallback=0.0)
        boundaries = sorted({
            value
            for value in (self.start_seconds, self.end_seconds)
            if value is not None
        })
        candidates = (
            [value for value in boundaries if value < position]
            if direction < 0
            else [value for value in boundaries if value > position]
        )
        if not candidates:
            return None
        return candidates[-1] if direction < 0 else candidates[0]

    def clear(self) -> None:
        self.item_id = None
        self.source_path = None
        self.start_seconds = None
        self.end_seconds = None


@dataclass(frozen=True, slots=True)
class AudioClipContext:
    item_id: str
    source_path: str
    title: str
    position_seconds: float
    duration_seconds: float


@dataclass(frozen=True, slots=True)
class AudioClipFormatChoice:
    """Oddziela wartosc protokolu od tekstu widocznego i mowionego."""

    value: str
    label: str
    extension: str


def clip_context_from_status(
    status: object,
    *,
    files_session_active: bool,
    player_view_active: bool,
    path_exists: Callable[[Path], bool] = Path.is_file,
) -> tuple[AudioClipContext | None, str | None]:
    """Waliduj kontekst tak jak ``TryGetLocalClipContext`` pelnego AMC."""
    if not player_view_active:
        return None, "Wycinanie fragmentu działa w otwartym odtwarzaczu"
    if not files_session_active:
        return None, (
            "Wycinanie fragmentu jest dostępne dla plików lokalnych "
            "i pobranych odcinków podcastów"
        )
    if not isinstance(status, dict) or status.get("engine") != "files":
        return None, "Bieżący element nie jest lokalnym plikiem multimedialnym"

    item_id = status.get("id")
    source_path = status.get("source")
    title = status.get("title")
    if not isinstance(item_id, str) or not item_id.strip():
        return None, "Bieżący element nie jest lokalnym plikiem multimedialnym"
    if not isinstance(source_path, str) or not source_path.strip():
        return None, "Bieżący element nie jest lokalnym plikiem multimedialnym"

    duration = _finite_nonnegative(status.get("durationSeconds"), fallback=0.0)
    if duration <= 0.0:
        return None, (
            "Nie można zaznaczyć fragmentu, ponieważ czas trwania pliku "
            "jest nieznany"
        )
    if not path_exists(Path(source_path)):
        return None, (
            "Plik jest obecnie niedostępny. Jeśli znajduje się w chmurze, "
            "pobierz go i spróbuj ponownie"
        )
    position = min(
        _finite_nonnegative(status.get("positionSeconds"), fallback=0.0),
        duration,
    )
    return AudioClipContext(
        item_id=item_id.strip(),
        source_path=source_path,
        title=(title.strip() if isinstance(title, str) and title.strip() else Path(source_path).name),
        position_seconds=position,
        duration_seconds=duration,
    ), None


def format_clip_time(seconds: float) -> str:
    """Format ``m:ss.fff`` albo ``h:mm:ss.fff`` z pelnego AMC."""
    value = _finite_nonnegative(seconds, fallback=0.0)
    # ``TimeSpan`` z formatem ``fff`` pokazuje skladnik milisekund, nie
    # zaokragla do nastepnej sekundy.
    total_milliseconds = int(value * 1_000.0)
    hours, remainder = divmod(total_milliseconds, 3_600_000)
    minutes, remainder = divmod(remainder, 60_000)
    whole_seconds, milliseconds = divmod(remainder, 1_000)
    if hours:
        return f"{hours}:{minutes:02d}:{whole_seconds:02d}.{milliseconds:03d}"
    return f"{minutes}:{whole_seconds:02d}.{milliseconds:03d}"


def read_clip_selections(raw: object) -> list[dict]:
    """Odrzuc obce pola, NaN/Infinity, duplikaty i niepelne tozsamosci."""
    if not isinstance(raw, list):
        return []
    result: list[dict] = []
    seen: set[tuple[str, str]] = set()
    for item in raw:
        if len(result) >= MAX_SAVED_CLIP_SELECTIONS:
            break
        if not isinstance(item, dict):
            continue
        item_id = item.get("itemId")
        source_path = item.get("sourcePath")
        if not isinstance(item_id, str) or not item_id.strip():
            continue
        if not isinstance(source_path, str) or not source_path.strip():
            continue
        item_id = item_id.strip()
        source_path = source_path.strip()
        identity = (item_id, source_path.casefold())
        if identity in seen:
            continue
        start = _optional_finite_nonnegative(item.get("startSeconds"))
        end = _optional_finite_nonnegative(item.get("endSeconds"))
        if start is None and end is None:
            continue
        # Stan recznie uszkodzony nie moze stworzyc odwroconego fragmentu.
        if start is not None and end is not None and end <= start:
            continue
        seen.add(identity)
        result.append({
            "itemId": item_id,
            "sourcePath": source_path,
            "startSeconds": start,
            "endSeconds": end,
        })
    return result


def restore_clip_selection(
    records: object,
    item_id: str,
    source_path: str,
    duration_seconds: float,
) -> AudioClipSelection:
    # Brak zapisanego wpisu ma byc rozroznialny od wpisu bez jednej granicy.
    # W Core ``RestoreClipSelection`` najpierw robi Clear(), wiec Matches
    # zwraca false, dopoki SetStart/TrySetEnd nie odtworzy rzeczywistego wpisu.
    selection = AudioClipSelection()
    for item in read_clip_selections(records):
        if (
            item["itemId"] == item_id
            and item["sourcePath"].casefold() == source_path.casefold()
        ):
            start = item.get("startSeconds")
            end = item.get("endSeconds")
            if start is not None:
                selection.set_start(item_id, source_path, start, duration_seconds)
            if end is not None:
                selection.set_end(item_id, source_path, end, duration_seconds)
            break
    return selection


def update_clip_selections(
    records: object,
    selection: AudioClipSelection,
) -> list[dict]:
    """Zastap wpis jednego pliku; puste zaznaczenie usuwa jego rekord."""
    cleaned = read_clip_selections(records)
    if not selection.item_id or not selection.source_path:
        return cleaned
    identity = (selection.item_id, selection.source_path.casefold())
    result = [
        item
        for item in cleaned
        if (item["itemId"], item["sourcePath"].casefold()) != identity
    ]
    if selection.start_seconds is not None or selection.end_seconds is not None:
        result.insert(0, {
            "itemId": selection.item_id,
            "sourcePath": selection.source_path,
            "startSeconds": selection.start_seconds,
            "endSeconds": selection.end_seconds,
        })
    return result[:MAX_SAVED_CLIP_SELECTIONS]


def format_choices_from_payload(payload: object) -> list[AudioClipFormatChoice]:
    """Czytaj tylko jawne pola i nigdy nie uzywaj repr obiektu jako etykiety."""
    if not isinstance(payload, dict) or not isinstance(payload.get("formats"), list):
        return []
    result: list[AudioClipFormatChoice] = []
    allowed_values = {"original", "flac", "wav"}
    for item in payload["formats"]:
        if not isinstance(item, dict) or item.get("available") is not True:
            continue
        value = item.get("value")
        label = item.get("label")
        extension = item.get("extension")
        if value not in allowed_values or not isinstance(label, str) or not label.strip():
            continue
        if not isinstance(extension, str) or not extension.startswith("."):
            continue
        result.append(AudioClipFormatChoice(value, label.strip(), extension.lower()))
    return result


def suggested_clip_file_name(title: str, extension: str) -> str:
    invalid = '<>:"/\\|?*'
    cleaned = "".join("_" if character in invalid or ord(character) < 32 else character for character in title).strip()
    if not cleaned:
        cleaned = "fragment audio"
    return f"{cleaned} - fragment{extension}"


def _finite_nonnegative(value: object, *, fallback: float) -> float:
    if isinstance(value, bool) or not isinstance(value, (int, float)):
        return fallback
    converted = float(value)
    if not math.isfinite(converted) or converted < 0.0:
        return fallback
    return converted


def _optional_finite_nonnegative(value: object) -> float | None:
    if value is None:
        return None
    if isinstance(value, bool) or not isinstance(value, (int, float)):
        return None
    converted = float(value)
    return converted if math.isfinite(converted) and converted >= 0.0 else None
