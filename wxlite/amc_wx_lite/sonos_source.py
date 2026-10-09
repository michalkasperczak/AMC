"""Jawny model grup Sonos i ich stanu dla natywnych kontrolek wx."""

from __future__ import annotations

from .list_model import Row


class SonosPayloadError(ValueError):
    """Host zwrocil odpowiedz, ktorej nie wolno pokazac jako listy."""


def target_rows(payload: object) -> tuple[Row, ...]:
    """Przyjmij tylko celowe etykiety grup; identyfikatory zostaja w modelu."""
    if not isinstance(payload, dict):
        raise SonosPayloadError("Host Sonos zwrócił nieprawidłową odpowiedź")
    raw_items = payload.get("items")
    if not isinstance(raw_items, list):
        raise SonosPayloadError("Host Sonos nie zwrócił listy grup")

    rows: list[Row] = []
    seen: set[str] = set()
    for raw in raw_items:
        if not isinstance(raw, dict):
            continue
        item_id = _text(raw.get("itemId"))
        household_id = _text(raw.get("householdId"))
        group_id = _text(raw.get("groupId"))
        title = _text(raw.get("title"))
        if (
            not item_id
            or not household_id
            or not group_id
            or not title
            or item_id in seen
        ):
            continue
        seen.add(item_id)
        rows.append(Row(
            item_id=item_id,
            title=title,
            kind="sonosGroup",
            detail=_text(raw.get("detail")),
            parent_id=household_id,
            service_id=group_id,
            service_kind="sonosGroup",
        ))
    return tuple(rows)


def snapshot(payload: object) -> dict:
    """Odetnij techniczne i nieznane pola przed użyciem danych w interfejsie."""
    if not isinstance(payload, dict):
        raise SonosPayloadError("Host Sonos zwrócił nieprawidłowy stan")
    result = {
        name: _text(payload.get(name))
        for name in (
            "itemId", "householdId", "groupId", "groupTitle", "title",
            "source", "stateText", "volumeText", "positionText",
            "playbackState", "message",
        )
    }
    for name in ("muted", "fixedVolume"):
        value = payload.get(name)
        result[name] = value if isinstance(value, bool) else None
    volume = payload.get("volume")
    result["volume"] = (
        max(0, min(100, volume))
        if isinstance(volume, int) and not isinstance(volume, bool)
        else None
    )
    for name in ("positionSeconds", "durationSeconds"):
        value = payload.get(name)
        result[name] = (
            max(0.0, float(value))
            if isinstance(value, (int, float)) and not isinstance(value, bool)
            else None
        )
    return result


def now_playing_label(state: dict) -> str:
    """Nazwa widoku odtwarzacza złożona wyłącznie z tekstów użytkowych."""
    parts: list[str] = []
    title = _text(state.get("title"))
    if title and title.casefold() != "brak informacji":
        parts.append(title)
    source = _text(state.get("source"))
    if source and source.casefold() not in {part.casefold() for part in parts}:
        parts.append(source)
    if not parts:
        parts.append(_text(state.get("groupTitle")) or "Sonos")
    if state.get("muted") is True:
        parts.append("wyciszone")
    return ", ".join(parts)


def _text(value: object) -> str:
    return value.strip() if isinstance(value, str) else ""
