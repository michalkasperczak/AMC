"""Local video targets for the existing Files player."""

from __future__ import annotations

from pathlib import Path

from .list_model import Row


# Keep this set aligned with Core.LocalAudioFileDiscovery.VideoExtensions.
VIDEO_SUFFIXES = frozenset({
    ".mkv", ".webm", ".mp4", ".m4v", ".mov", ".ogv",
    ".3g2", ".3gp", ".3gp2", ".3gpp", ".avi", ".wmv",
    ".mpeg", ".mpg", ".mpe", ".ts", ".mts", ".m2ts", ".vob", ".flv",
})


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
