"""Jawny model listy i migawki WiiM dla natywnych kontrolek wx."""

from __future__ import annotations

from .list_model import Row


class WiiMPayloadError(ValueError):
    """Host zwrocil odpowiedz, ktorej nie wolno pokazac jako listy."""


def device_rows(payload: object) -> tuple[Row, ...]:
    """Przyjmij tylko celowe etykiety; nigdy repr obiektu ani adres IP."""
    if not isinstance(payload, dict):
        raise WiiMPayloadError("Host WiiM zwrócił nieprawidłową odpowiedź")
    raw_items = payload.get("items")
    if not isinstance(raw_items, list):
        raise WiiMPayloadError("Host WiiM nie zwrócił listy urządzeń")

    rows: list[Row] = []
    seen: set[str] = set()
    for raw in raw_items:
        if not isinstance(raw, dict):
            continue
        item_id = _text(raw.get("itemId"))
        device_id = _text(raw.get("deviceId"))
        title = _text(raw.get("title"))
        if not item_id or not device_id or not title or item_id in seen:
            continue
        seen.add(item_id)
        rows.append(Row(
            item_id=item_id,
            title=title,
            kind="device",
            detail=_text(raw.get("detail")),
            service_id=device_id,
            service_kind="device",
        ))
    return tuple(rows)


def snapshot(payload: object) -> dict:
    """Odetnij nieznane pola przed użyciem danych do nazw kontrolek."""
    if not isinstance(payload, dict):
        raise WiiMPayloadError("Host WiiM zwrócił nieprawidłowy stan")
    result = {
        name: _text(payload.get(name))
        for name in (
            "itemId", "deviceId", "deviceTitle", "title", "subtitle",
            "artist", "album", "source", "playbackState", "message",
        )
    }
    result["muted"] = payload.get("muted") is True
    volume = payload.get("volume")
    result["volume"] = (
        max(0, min(100, volume))
        if isinstance(volume, int) and not isinstance(volume, bool)
        else 0
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
    """Nazwa kontrolki transportu: tylko treść dla użytkownika."""
    parts: list[str] = []
    for name in ("title", "subtitle", "artist", "album"):
        value = _text(state.get(name))
        if value and value.casefold() not in {part.casefold() for part in parts}:
            parts.append(value)
    if not parts:
        parts.append(_text(state.get("deviceTitle")) or "WiiM")
    if state.get("muted"):
        parts.append("wyciszone")
    return ", ".join(parts)


def _text(value: object) -> str:
    return value.strip() if isinstance(value, str) else ""
