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
        raw_members = raw.get("playerIds")
        members = tuple(sorted({
            value.strip()
            for value in raw_members
            if isinstance(value, str) and value.strip()
        })) if isinstance(raw_members, list) else ()
        rows.append(Row(
            item_id=item_id,
            title=title,
            kind="sonosGroup",
            detail=_text(raw.get("detail")),
            parent_id=household_id,
            service_id=group_id,
            service_kind="sonosGroup",
            service_members=members,
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


def library_rows(payload: object, household_id: str) -> tuple[Row, ...]:
    """Kategorie Biblioteki z jawnymi etykietami; dom zostaje w modelu."""
    raw_items = _items(payload, "Host Sonos nie zwrócił Biblioteki")
    rows: list[Row] = []
    seen: set[str] = set()
    for raw in raw_items:
        if not isinstance(raw, dict):
            continue
        item_id = _text(raw.get("itemId"))
        title = _text(raw.get("title"))
        kind = _text(raw.get("kind"))
        if (
            not item_id
            or not title
            or kind not in {"favorites", "playlists", "ownStreams"}
            or item_id in seen
        ):
            continue
        seen.add(item_id)
        rows.append(Row(
            item_id=item_id,
            title=title,
            kind="sonosCategory",
            detail=_text(raw.get("detail")),
            parent_id=household_id,
            service_kind=kind,
        ))
    return tuple(rows)


def material_rows(payload: object, household_id: str) -> tuple[Row, ...]:
    """Materiały Sonosa: techniczny identyfikator nigdy nie jest etykietą."""
    raw_items = _items(payload, "Host Sonos nie zwrócił listy materiałów")
    rows: list[Row] = []
    seen: set[str] = set()
    for raw in raw_items:
        if not isinstance(raw, dict):
            continue
        item_id = _text(raw.get("itemId"))
        material_id = _text(raw.get("materialId"))
        title = _text(raw.get("title"))
        kind = _text(raw.get("kind"))
        if (
            not item_id
            or not material_id
            or not title
            or kind not in {"favorite", "playlist", "ownStream"}
            or item_id in seen
        ):
            continue
        seen.add(item_id)
        own_stream = kind == "ownStream"
        rows.append(Row(
            item_id=item_id,
            title=title,
            kind={
                "favorite": "sonosFavorite",
                "playlist": "sonosPlaylist",
                "ownStream": "sonosOwnStream",
            }[kind],
            url=(
                _text(raw.get("streamUrl"))
                if kind == "ownStream"
                else None
            ),
            # W jednorodnym widoku własnych strumieni nagłówek już podaje
            # kontekst. NVDA ma czytać samą nazwę, nie „nazwa, stacja zapisana
            # w AMC”. Adres nadal jest jawnie dostępny pod Ctrl+Shift+C.
            detail="" if own_stream else _text(raw.get("detail")),
            show_kind=not own_stream,
            parent_id=household_id,
            service_id=material_id,
            service_kind=kind,
        ))
    return tuple(rows)


def response_message(payload: object) -> str:
    """Tylko celowy tekst użytkowy z odpowiedzi listy."""
    return _text(payload.get("message")) if isinstance(payload, dict) else ""


def own_stream_edit_info(payload: object) -> tuple[str, str, str]:
    """Adres wolno odczytać wyłącznie do jawnie otwartego edytora F2."""
    if not isinstance(payload, dict):
        raise SonosPayloadError("Nie można odczytać danych wybranej stacji Sonos")
    stream_id = _text(payload.get("streamId"))
    name = _text(payload.get("name"))
    url = _text(payload.get("url"))
    if not stream_id or not name or not url:
        raise SonosPayloadError("Nie można odczytać danych wybranej stacji Sonos")
    return stream_id, name, url


def _items(payload: object, error: str) -> list:
    if not isinstance(payload, dict):
        raise SonosPayloadError(error)
    raw_items = payload.get("items")
    if not isinstance(raw_items, list):
        raise SonosPayloadError(error)
    return raw_items


def now_playing_label(state: dict, *, fallback_title: str = "") -> str:
    """Nazwa widoku odtwarzacza złożona wyłącznie z tekstów użytkowych."""
    parts: list[str] = []
    title = _text(state.get("title"))
    if title and title.casefold() != "brak informacji":
        parts.append(title)
    source = _text(state.get("source"))
    if source and source.casefold() not in {part.casefold() for part in parts}:
        parts.append(source)
    fallback = _text(fallback_title)
    if not parts and fallback:
        parts.append(fallback)
    if not parts:
        parts.append(_text(state.get("groupTitle")) or "Sonos")
    if state.get("muted") is True:
        parts.append("wyciszone")
    return ", ".join(parts)


def playback_status_label(state: dict, *, fallback_title: str = "") -> str:
    """Tekst natywnego paska stanu czytanego przez NVDA+End.

    Lokalny interfejs Sonosa nie podaje wiarygodnego bitrate'u dla bieżącego
    materiału. Pokazujemy tylko zmierzone dane: materiał lub źródło, stan,
    pozycję i głośność. Nazwa grupy zostaje wyłącznie ostatnim ratunkiem, gdy
    nie znamy nawet ostatniego materiału uruchomionego w AMC.
    """
    parts = [now_playing_label(state, fallback_title=fallback_title)]
    for name in ("stateText", "positionText", "volumeText"):
        value = _text(state.get(name))
        if value and value.casefold() not in {part.casefold() for part in parts}:
            parts.append(value)
    return ", ".join(parts)


def _text(value: object) -> str:
    return value.strip() if isinstance(value, str) else ""
