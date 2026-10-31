"""Profile-local history of files downloaded by AMC.

Podcast audio paths also live in podcasts.db; callers merge those records with
this journal so older audio downloads appear without changing the database.
"""

from __future__ import annotations

from dataclasses import dataclass
from datetime import datetime
import hashlib
import json
import os
from pathlib import Path
import threading
import time
from typing import Iterable

from .list_model import Row


_MAX_ENTRIES = 5000
_SOURCE_LABELS = {
    "archive": "Radio Archiwista",
    "podcast_audio": "Podcasty i YouTube, audio",
    "podcast_video": "Podcasty i YouTube, wideo",
}


@dataclass(frozen=True, slots=True)
class DownloadEntry:
    path: str
    title: str
    source: str
    finished_at: float

    @property
    def item_id(self) -> str:
        identity = os.path.normcase(os.path.abspath(self.path)).casefold()
        return "download:" + hashlib.sha256(identity.encode("utf-8")).hexdigest()[:24]


def _entry(raw: object) -> DownloadEntry | None:
    if not isinstance(raw, dict):
        return None
    path = str(raw.get("path") or "").strip()
    source = str(raw.get("source") or "").strip()
    if not path or not Path(path).is_absolute() or source not in _SOURCE_LABELS:
        return None
    try:
        finished_at = float(raw.get("finished_at") or 0)
    except (TypeError, ValueError):
        finished_at = 0.0
    return DownloadEntry(
        path=path,
        title=str(raw.get("title") or "").strip(),
        source=source,
        finished_at=max(0.0, finished_at),
    )


class DownloadHistoryStore:
    def __init__(self, settings_dir: str | Path) -> None:
        self.path = Path(settings_dir) / "download-history.json"
        self._lock = threading.Lock()

    def load(self) -> list[DownloadEntry]:
        try:
            raw = json.loads(self.path.read_text(encoding="utf-8"))
        except FileNotFoundError:
            return []
        except (OSError, ValueError) as error:
            raise OSError("Nie można odczytać historii pobierania") from error
        if not isinstance(raw, list):
            raise OSError("Historia pobierania ma nieprawidłowy format")
        return [value for item in raw if (value := _entry(item)) is not None]

    def record(
        self, path: str | Path, *, title: str, source: str,
        finished_at: float | None = None,
    ) -> DownloadEntry:
        value = _entry({
            "path": str(path), "title": title, "source": source,
            "finished_at": time.time() if finished_at is None else finished_at,
        })
        if value is None:
            raise ValueError("Nieprawidłowe dane pobranego pliku")
        with self._lock:
            entries = [item for item in self.load() if item.item_id != value.item_id]
            entries.insert(0, value)
            self.path.parent.mkdir(parents=True, exist_ok=True)
            temporary = self.path.with_suffix(".json.tmp")
            try:
                temporary.write_text(
                    json.dumps([
                        {
                            "path": item.path, "title": item.title,
                            "source": item.source, "finished_at": item.finished_at,
                        }
                        for item in entries[:_MAX_ENTRIES]
                    ], ensure_ascii=False, indent=2),
                    encoding="utf-8",
                )
                temporary.replace(self.path)
            finally:
                temporary.unlink(missing_ok=True)
        return value


def merge_entries(*groups: Iterable[DownloadEntry]) -> list[DownloadEntry]:
    by_id: dict[str, DownloadEntry] = {}
    for group in groups:
        for entry in group:
            earlier = by_id.get(entry.item_id)
            if earlier is None or entry.finished_at > earlier.finished_at:
                by_id[entry.item_id] = entry
    return sorted(
        by_id.values(),
        key=lambda entry: (-entry.finished_at, Path(entry.path).name.casefold()),
    )[:_MAX_ENTRIES]


def entries_from_podcast_rows(rows: Iterable[Row]) -> list[DownloadEntry]:
    result: list[DownloadEntry] = []
    for row in rows:
        if row.kind != "episode" or not row.path or not Path(row.path).is_file():
            continue
        try:
            finished_at = Path(row.path).stat().st_mtime
        except OSError:
            continue
        result.append(DownloadEntry(row.path, row.title, "podcast_audio", finished_at))
    return result


def entries_from_archive_folder(folder: str | Path) -> list[DownloadEntry]:
    result: list[DownloadEntry] = []
    try:
        paths = list(Path(folder).glob("*.mp3"))
    except OSError:
        return result
    for path in paths:
        try:
            result.append(DownloadEntry(str(path), path.stem, "archive", path.stat().st_mtime))
        except OSError:
            continue
    return result


def download_rows(entries: Iterable[DownloadEntry]) -> list[Row]:
    rows: list[Row] = []
    for entry in entries:
        path = Path(entry.path)
        available = path.is_file()
        name = path.name or entry.title or "Plik bez nazwy"
        rows.append(Row(
            item_id=entry.item_id,
            title=name if available else f"Brak pliku: {name}",
            kind="track",
            path=entry.path if available else None,
            detail=_SOURCE_LABELS[entry.source],
            show_kind=False,
            activation_message=(
                None if available else f"Pobrany plik {name} nie jest już dostępny"
            ),
        ))
    return rows


def download_information(entry: DownloadEntry) -> str:
    path = Path(entry.path)
    try:
        when = (
            datetime.fromtimestamp(entry.finished_at).strftime("%d.%m.%Y, %H:%M")
            if entry.finished_at > 0 else "nieznana data"
        )
    except (OverflowError, OSError, ValueError):
        when = "nieznana data"
    try:
        size = path.stat().st_size if path.is_file() else None
    except OSError:
        size = None
    details = [f"{_SOURCE_LABELS[entry.source]}. Pobrano: {when}"]
    if size is not None:
        amount = (
            f"{size / 1_048_576:.1f} MB" if size >= 1_048_576
            else f"{size / 1024:.0f} KB"
        )
        details.append(f"Rozmiar: {amount.replace('.', ',')}")
    details.extend((f"Folder: {path.parent}",
                    "Plik dostępny" if size is not None else "Plik niedostępny"))
    return ". ".join(details)
