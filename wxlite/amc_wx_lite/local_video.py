"""Local video targets for the existing Files player."""

from __future__ import annotations

from pathlib import Path

from .list_model import Row


VIDEO_SUFFIXES = frozenset({".mp4", ".m4v", ".mov", ".avi", ".wmv", ".webm", ".mkv"})


def local_video_file(path: str | None) -> bool:
    if not isinstance(path, str) or not path:
        return False
    try:
        value = Path(path)
        return value.suffix.casefold() in VIDEO_SUFFIXES and value.is_file()
    except (OSError, ValueError):
        return False


def local_video_target(row: Row | None) -> bool:
    return bool(row and row.kind == "track" and local_video_file(row.path))
