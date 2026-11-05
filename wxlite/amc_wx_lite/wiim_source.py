"""Jawny model listy i migawki WiiM dla natywnych kontrolek wx."""

from __future__ import annotations

from dataclasses import dataclass

from .list_model import Row


class WiiMPayloadError(ValueError):
    """Host zwrocil odpowiedz, ktorej nie wolno pokazac jako listy."""


@dataclass(frozen=True, slots=True)
class WiiMPresetChoice:
    """Jedno miejsce urządzenia; do kontrolki trafia wyłącznie ``label``."""

    number: int
    title: str
    source: str
    label: str
    occupied: bool
    current: bool
    activation_error: str


@dataclass(frozen=True, slots=True)
class WiiMOptionChoice:
    """Wartość modelu i osobna, jawna etykieta dla natywnej kontrolki."""

    value: str
    label: str


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


def network_stream_rows(payload: object, *, device_id: str) -> tuple[Row, ...]:
    """Zapisane strumienie WiiM bez adresów w tekście dostępnym.

    ``device_id`` pozostaje polem modelu potrzebnym do wykonania Entera.
    Zwykła lista przekazuje NVDA nazwę i natywny licznik wiersza. Stałe
    dopowiedzenie rodzaju nie odróżnia pozycji w jednorodnym widoku i tylko
    powtarza informację zawartą w nagłówku „Strumienie zapisane w AMC”.
    """
    if not isinstance(payload, dict):
        raise WiiMPayloadError("Host WiiM zwrócił nieprawidłową odpowiedź")
    raw_items = payload.get("items")
    if not isinstance(raw_items, list):
        raise WiiMPayloadError("Host WiiM nie zwrócił listy strumieni")
    if not isinstance(device_id, str) or not device_id.strip():
        raise WiiMPayloadError("Nie wybrano urządzenia WiiM")

    rows: list[Row] = []
    seen: set[str] = set()
    for raw in raw_items:
        if not isinstance(raw, dict):
            continue
        item_id = _text(raw.get("itemId"))
        stream_id = _text(raw.get("streamId"))
        title = _text(raw.get("title"))
        if not item_id or not stream_id or not title or item_id in seen:
            continue
        seen.add(item_id)
        rows.append(Row(
            item_id=item_id,
            title=title,
            kind="wiimNetworkStream",
            url=_text(raw.get("streamUrl")),
            detail="",
            show_kind=False,
            parent_id=device_id.strip(),
            service_id=stream_id,
            service_kind="wiimNetworkStream",
        ))
    return tuple(rows)


def network_stream_edit_info(payload: object) -> tuple[str, str, str]:
    """Dane do jawnie otwartego okna F2, nie do zwykłej nazwy dostępnej."""
    if not isinstance(payload, dict):
        raise WiiMPayloadError("Host WiiM zwrócił nieprawidłowe dane strumienia")
    stream_id = _text(payload.get("streamId"))
    name = _text(payload.get("name"))
    url = _text(payload.get("url"))
    if not stream_id or not name or not url:
        raise WiiMPayloadError("Nie można odczytać danych wybranego strumienia WiiM")
    return stream_id, name, url


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
    bitrate = payload.get("bitrateKbps")
    result["bitrateKbps"] = (
        bitrate
        if isinstance(bitrate, int) and not isinstance(bitrate, bool)
        and 0 < bitrate <= 30_000
        else None
    )
    return result


def preset_choices(payload: object) -> tuple[str, tuple[WiiMPresetChoice, ...]]:
    """Przyjmij dokładnie 12 celowo opisanych miejsc, bez adresów i repr."""
    if not isinstance(payload, dict):
        raise WiiMPayloadError("Host WiiM zwrócił nieprawidłową listę presetów")
    device_title = _text(payload.get("deviceTitle")) or "WiiM"
    raw_items = payload.get("items")
    if not isinstance(raw_items, list):
        raise WiiMPayloadError("Host WiiM nie zwrócił listy presetów")

    choices: dict[int, WiiMPresetChoice] = {}
    for raw in raw_items:
        if not isinstance(raw, dict):
            continue
        number = raw.get("presetNumber")
        label = _text(raw.get("label"))
        if (
            type(number) is not int
            or not 1 <= number <= 12
            or not label
            or number in choices
        ):
            continue
        choices[number] = WiiMPresetChoice(
            number=number,
            title=_text(raw.get("name")) or f"Preset {number}",
            source=_text(raw.get("source")),
            label=label,
            occupied=raw.get("occupied") is True,
            current=raw.get("current") is True,
            activation_error=_text(raw.get("activationError")),
        )
    if set(choices) != set(range(1, 13)):
        raise WiiMPayloadError("Host WiiM zwrócił niepełną listę presetów")
    return device_title, tuple(choices[number] for number in range(1, 13))


def option_choices(
    payload: object,
) -> tuple[str, str, str, tuple[WiiMOptionChoice, ...], int]:
    """Odczytaj dialog opcji bez przekazywania obiektów ani ID do NVDA."""
    if not isinstance(payload, dict):
        raise WiiMPayloadError("Host WiiM zwrócił nieprawidłowe ustawienia")
    device_title = _text(payload.get("deviceTitle")) or "WiiM"
    title = _text(payload.get("title"))
    description = _text(payload.get("description"))
    selected_value = _text(payload.get("selectedValue"))
    raw_items = payload.get("items")
    if not title or not description or not isinstance(raw_items, list):
        raise WiiMPayloadError("Host WiiM nie zwrócił pełnej listy ustawień")

    result: list[WiiMOptionChoice] = []
    seen: set[str] = set()
    for raw in raw_items:
        if not isinstance(raw, dict):
            continue
        value = _text(raw.get("value"))
        label = _text(raw.get("label"))
        if not value or not label or value in seen:
            continue
        seen.add(value)
        result.append(WiiMOptionChoice(value, label))
    if not result:
        raise WiiMPayloadError("Urządzenie WiiM nie udostępniło tej opcji")
    selected_index = next(
        (index for index, choice in enumerate(result)
         if choice.value == selected_value),
        0,
    )
    return device_title, title, description, tuple(result), selected_index


def now_playing_label(state: dict) -> str:
    """Nazwa kontrolki transportu: tylko treść dla użytkownika."""
    parts: list[str] = []
    for name in ("title", "subtitle", "artist", "album"):
        value = _text(state.get(name))
        if value and value.casefold() not in {part.casefold() for part in parts}:
            parts.append(value)
    if not parts:
        parts.append(_text(state.get("deviceTitle")) or "WiiM")
    bitrate = state.get("bitrateKbps")
    if isinstance(bitrate, int) and not isinstance(bitrate, bool) and 0 < bitrate <= 30_000:
        parts.append(f"{bitrate} kb/s")
    if state.get("muted"):
        parts.append("wyciszone")
    return ", ".join(parts)


def _text(value: object) -> str:
    return value.strip() if isinstance(value, str) else ""
