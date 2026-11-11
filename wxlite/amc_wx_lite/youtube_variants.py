"""User-facing labels for one YouTube format offer.

Protocol selectors and signatures are model data, never list box text.
"""

from __future__ import annotations

from dataclasses import dataclass
from typing import Any, Sequence
from urllib.parse import urlparse

from . import i18n


@dataclass(frozen=True, slots=True)
class DownloadVariant:
    selector: str
    signature: str
    extension: str
    label: str


def is_youtube_source_row(row: object) -> bool:
    if getattr(row, "kind", "") != "episode":
        return False
    address = str(getattr(row, "url", "") or "").strip()
    try:
        host = (urlparse(address).hostname or "").casefold()
    except ValueError:
        return False
    return host in {"youtube.com", "youtu.be"} or host.endswith(".youtube.com")


def can_choose_variant(rows: Sequence[object]) -> bool:
    return len(rows) == 1 and is_youtube_source_row(rows[0])


def _codec(value: object, *, video: bool) -> str | None:
    text = str(value or "").casefold()
    if video:
        for prefix, label in (
            ("avc1", "H.264"), ("h264", "H.264"),
            ("hev1", "H.265"), ("hvc1", "H.265"),
            ("vp9", "VP9"), ("vp09", "VP9"),
            ("av01", "AV1"), ("av1", "AV1"),
        ):
            if text.startswith(prefix):
                return label
    else:
        for prefix, label in (
            ("mp4a", "AAC"), ("aac", "AAC"),
            ("opus", "Opus"), ("vorbis", "Vorbis"),
            ("mp3", "MP3"), ("flac", "FLAC"),
        ):
            if text.startswith(prefix):
                return label
    return None


def _positive_int(value: object) -> int | None:
    if isinstance(value, bool):
        return None
    try:
        number = int(value)
    except (ValueError, TypeError, OverflowError):
        return None
    return number if 0 < number <= 1_000_000_000_000 else None


def _extension(value: object) -> str | None:
    extension = str(value or "").strip().lower().lstrip(".")
    return extension if extension in {
        "mp4", "m4v", "webm", "mkv", "mov", "ts",
        "m4a", "mp3", "opus", "ogg", "aac", "flac",
    } else None


def _label(item: dict[str, Any], extension: str) -> str:
    english = i18n.active_language() == i18n.ENGLISH
    kind = item.get("kind")
    if kind == "audioOnly":
        parts = ["Audio only" if english else "Tylko dźwięk"]
    elif kind == "videoOnly":
        parts = ["Video without audio" if english else "Wideo bez dźwięku"]
    else:
        parts = ["Video with audio" if english else "Wideo z dźwiękiem"]

    height = _positive_int(item.get("height"))
    if height and kind != "audioOnly":
        parts.append(f"{height}p")
    parts.append(extension.upper())

    video_codec = _codec(item.get("videoCodec"), video=True)
    audio_codec = _codec(item.get("audioCodec"), video=False)
    if video_codec:
        parts.append(("video " if english else "obraz ") + video_codec)
    if audio_codec:
        parts.append(("audio " if english else "dźwięk ") + audio_codec)

    bitrate = _positive_int(item.get("bitrateKbps"))
    if bitrate:
        parts.append(f"{bitrate} kb/s")
    language = str(item.get("language") or "").casefold()
    if language in {"pl", "pol"}:
        parts.append("Polish audio" if english else "dźwięk polski")
    elif language in {"en", "eng"}:
        parts.append("English audio" if english else "dźwięk angielski")

    size = _positive_int(item.get("sizeBytes"))
    if size:
        amount = max(0.1, size / 1_000_000)
        number = f"{amount:.1f}".rstrip("0").rstrip(".")
        if not english:
            number = number.replace(".", ",")
        approx = bool(item.get("sizeApproximate"))
        parts.append(
            ("about " if english else "około ") + number + " MB"
            if approx else number + " MB"
        )
    if item.get("needsMerge") is True:
        parts.append(
            "video and audio merged without transcoding"
            if english else "połączenie obrazu i dźwięku bez przekodowania"
        )
    return ", ".join(parts)


def read_variants(payload: object) -> tuple[DownloadVariant, ...]:
    if not isinstance(payload, dict) or not isinstance(payload.get("variants"), list):
        return ()
    choices: list[DownloadVariant] = []
    seen_labels: set[str] = set()
    for item in payload["variants"]:
        if not isinstance(item, dict):
            continue
        selector = item.get("selector")
        signature = item.get("signature")
        extension = _extension(item.get("extension"))
        if not isinstance(selector, str) or not selector or not isinstance(signature, str):
            continue
        if not signature or extension is None or item.get("kind") not in {
            "audioOnly", "videoOnly", "videoAudio",
        }:
            continue
        label = _label(item, extension)
        if label in seen_labels:
            continue
        seen_labels.add(label)
        choices.append(DownloadVariant(selector, signature, extension, label))
    return tuple(choices)
