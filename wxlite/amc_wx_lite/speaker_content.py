"""Wspólny kontrakt prezentacji i wyszukiwania treści WiiM oraz Sonosa.

Adapter dostawcy dostarcza dane w :class:`Row`. Ten moduł odpowiada za to,
co wolno wypowiedzieć i co może trafić do lokalnych wyników wyszukiwania.
Identyfikatory urządzeń, domów i materiałów nigdy nie są częścią etykiety.
"""

from __future__ import annotations

from dataclasses import dataclass
from typing import Callable, Iterable

from .list_model import Row
from .navigation import LibraryView, SessionId
from .quick_info import (
    NO_SOURCE_MESSAGE,
    QuickInfoGuard,
    ordered_quick_info_parts,
    quick_info_request,
)
from .search_catalog import SearchEntry


SPEAKER_SESSIONS = frozenset({SessionId.WIIM, SessionId.SONOS})

_KIND_LABELS = {
    "device": "urządzenie WiiM",
    "wiimSource": "kategoria Biblioteki WiiM",
    "wiimNetworkStream": "strumień WiiM",
    "sonosGroup": "grupa Sonos",
    "sonosCategory": "kategoria Biblioteki Sonos",
    "sonosFavorite": "ulubione Sonos",
    "sonosPlaylist": "playlista Sonos",
    "sonosOwnStream": "stacja Sonos",
}

_SONOS_CONTENT_WITHOUT_ADDRESS = frozenset({
    "sonosFavorite",
    "sonosPlaylist",
})


def speaker_kind_label(row: Row) -> str:
    """Zwróć wyłącznie zamierzoną, użytkową nazwę rodzaju elementu."""

    return _KIND_LABELS.get(row.kind, row.kind_label)


def speaker_search_scope_label(session_id: SessionId) -> str:
    """Jawna nazwa lokalnego zakresu ``Ctrl+F``."""

    if session_id is SessionId.WIIM:
        return "WiiM — lokalnie w wczytanej liście"
    if session_id is SessionId.SONOS:
        return "Sonos — lokalnie w wczytanej liście"
    raise ValueError("Zakres głośników wymaga sesji WiiM albo Sonos")


def speaker_search_entries(
    session_id: SessionId,
    rows: Iterable[Row],
    source_view: LibraryView | None,
) -> tuple[SearchEntry, ...]:
    """Zamień migawkę rzeczywiście wczytanej listy na wyniki tylko do odczytu."""

    if session_id not in SPEAKER_SESSIONS:
        raise ValueError("Wyniki głośników wymagają sesji WiiM albo Sonos")
    return tuple(
        SearchEntry(
            session_id,
            row,
            source_view,
            result_kind_label=speaker_kind_label(row),
        )
        for row in rows
        if row.title.strip()
    )


def _distinct(parts: Iterable[str]) -> list[str]:
    result: list[str] = []
    seen: set[str] = set()
    for raw in parts:
        value = raw.strip()
        key = value.casefold()
        if value and key not in seen:
            seen.add(key)
            result.append(value)
    return result


def speaker_information_text(
    row: Row, measured: str = "", *, fields: tuple[str, ...] | None = None,
) -> str:
    """Użytkowe parametry wiersza, bez identyfikatorów protokołu.

    Adres albo ścieżka są wypowiadane tylko z jawnego pola modelu. Ulubione i
    playlisty Sonosa pozostają obsługiwane mimo braku adresu w oficjalnym API;
    brak jest nazwany wprost, a nie zastępowany ``materialId``.
    """

    location = ""
    if (row.url or "").strip():
        location = f"adres: {row.url.strip()}"
    elif (row.path or "").strip():
        location = f"ścieżka: {row.path.strip()}"
    elif row.kind in _SONOS_CONTENT_WITHOUT_ADDRESS:
        location = "adres nieudostępniony przez Sonos"

    useful_measurement = measured.strip()
    if useful_measurement == NO_SOURCE_MESSAGE:
        useful_measurement = ""
    if fields is None:
        parts = _distinct((useful_measurement, speaker_kind_label(row), row.detail, location))
        return ", ".join(parts) or NO_SOURCE_MESSAGE
    if not fields:
        return ""
    if location == "adres nieudostępniony przez Sonos":
        location = ""
    values: list[str] = []
    measurement_added = False
    for field in fields:
        if field in ("duration", "bitrate", "format", "sampleRate") and not measurement_added:
            values.append(useful_measurement)
            measurement_added = True
        elif field == "artist":
            values.append(row.artist_name)
        elif field == "other":
            values.extend((speaker_kind_label(row), row.detail, location))
    parts = _distinct(values)
    return ", ".join(parts)


@dataclass(frozen=True, slots=True)
class SpeakerInformationPlan:
    """Plan jednego odczytu lewą strzałką dla obu integracji."""

    row: Row | None = None
    message: str | None = None
    request: dict | None = None
    guard: QuickInfoGuard | None = None
    fields: tuple[str, ...] | None = None


def speaker_information_plan(
    row: Row | None,
    *,
    session: SessionId,
    view: str,
    alive: Callable[[], bool] | None = None,
    fields: tuple[str, ...] | None = None,
) -> SpeakerInformationPlan:
    """Przygotuj natychmiastowy odczyt lub pomiar dostępnego adresu strumienia."""

    if session not in SPEAKER_SESSIONS:
        raise ValueError("Informacja głośników wymaga sesji WiiM albo Sonos")
    if row is None:
        return SpeakerInformationPlan()
    if not row.address:
        return SpeakerInformationPlan(
            row=row,
            message=speaker_information_text(row, fields=fields),
            fields=fields,
        )

    # Ten sam pomiar sieciowego źródła, którego używa Radio internetowe.
    # ``radio`` jest rodzajem sondy hosta, nie sesją prezentowaną użytkownikowi.
    if fields is not None and not any(
        field in fields for field in ("bitrate", "format", "sampleRate", "duration")
    ):
        return SpeakerInformationPlan(
            row=row, message=speaker_information_text(row, fields=fields), fields=fields
        )
    request = quick_info_request(row, session="radio", duration_ticks=row.duration_ticks)
    if request is None:
        return SpeakerInformationPlan(
            row=row,
            message=speaker_information_text(row, fields=fields),
            fields=fields,
        )
    if fields is not None:
        request = dict(request)
        request["fields"] = [field for field in fields if field != "other"]
    return SpeakerInformationPlan(
        row=row,
        request=request,
        fields=fields,
        guard=QuickInfoGuard(
            item_id=row.item_id,
            session=session.value,
            view=view,
            alive=alive,
        ),
    )


def speaker_information_reply(
    response: dict | None,
    *,
    plan: SpeakerInformationPlan,
    item_id: str | None,
    session: str,
    view: str,
    say: Callable[[str], None],
) -> None:
    """Wypowiedz wynik tylko wtedy, gdy fokus nadal wskazuje ten sam wiersz."""

    if plan.row is None or plan.guard is None:
        return
    if not plan.guard.accepts(item_id=item_id, session=session, view=view):
        return
    measured = str((response or {}).get("text") or "").strip()
    if plan.fields is not None and isinstance((response or {}).get("parts"), list):
        other = speaker_information_text(plan.row, fields=("other",))
        message = ", ".join(ordered_quick_info_parts(response, plan.fields, other))
    else:
        message = speaker_information_text(plan.row, measured, fields=plan.fields)
    if message:
        say(message)


def speaker_information_failure(
    *,
    plan: SpeakerInformationPlan,
    item_id: str | None,
    session: str,
    view: str,
    say: Callable[[str], None],
) -> None:
    """Awaria pomiaru nie ukrywa danych, które adapter już udostępnił."""

    if plan.row is None or plan.guard is None:
        return
    if plan.guard.accepts(item_id=item_id, session=session, view=view):
        message = speaker_information_text(plan.row, fields=plan.fields)
        if message:
            say(message)
