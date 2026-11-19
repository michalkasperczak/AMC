"""Dostępnościowa dekoracja faktycznie odtwarzanego materiału.

Moduł nie zna wx ani hosta. Dostaje potwierdzony stan jednej sesji i zmienia
wyłącznie użytkowe pole :class:`Row`. Dzięki temu lista, wyszukiwanie i testy
korzystają z tej samej reguły, a identyfikatory i techniczne stany nie trafiają
do nazwy dostępnej.
"""

from __future__ import annotations

from dataclasses import replace
from typing import Iterable

from .list_model import Row
from .radio_activity import normalize_state_position


PLAYABLE_KINDS = frozenset((
    "track", "episode", "video", "recording", "station",
    "wiimNetworkStream", "sonosFavorite", "sonosOwnStream",
))


def _same_material_session(row: Row, session_id: str | None) -> bool:
    """Porównaj sesję wiersza prezentacyjnego bez zgadywania między sesjami."""

    expected = (session_id or "").strip().casefold()
    actual = (row.material_session_id or "").strip().casefold()
    if not actual or not expected:
        return not actual
    # Lokalna baza historycznie nazywa sesję ``local``, a navigator ``files``.
    return actual == expected or {actual, expected} == {"local", "files"}


def row_material_id(row: Row) -> str:
    """Zwróć stabilne Id materiału, nie techniczne Id wiersza prezentacji."""

    return (row.material_id or row.item_id).strip()


def row_matches_material(
    row: Row, *, current_item_id: str | None, session_id: str | None = None
) -> bool:
    """Czy wiersz oznacza dokładnie potwierdzony materiał tej sesji."""

    item_id = (current_item_id or "").strip()
    return bool(
        item_id
        and row_material_id(row) == item_id
        and _same_material_session(row, session_id)
        and row_can_show_playback_state(row)
    )


def playback_state_label(*, playing: bool, paused: bool) -> str:
    """Zwróć krótki stan użytkowy albo pusty tekst dla niepewnego stanu."""

    if not playing:
        return ""
    return "Wstrzymane" if paused else "Odtwarzane"


def row_can_show_playback_state(row: Row) -> bool:
    """Czy wiersz przedstawia materiał, a nie kontener lub techniczną akcję."""

    return (
        row.kind in PLAYABLE_KINDS
        and not row.is_openable
        and not row.activation_message
        # Zakładka może reprezentować prawdziwy materiał tylko, gdy loader
        # podał jej osobno jego Id i sesję. Samo ``bookmark:<id>`` nigdy nie
        # jest tożsamością utworu.
        and (not row.item_id.startswith("bookmark:") or bool(row.material_id))
    )


def activity_rows(
    rows: Iterable[Row],
    *,
    current_item_id: str | None,
    playing: bool,
    paused: bool,
    session_id: str | None = None,
    position: object = "before",
) -> list[Row]:
    """Ustaw stan tylko na dokładnie jednym, potwierdzonym materiale.

    Każde wywołanie czyści wcześniejszą dekorację, więc przejście A -> B,
    koniec odtwarzania i błąd nie zostawiają fałszywego napisu. Zwykłe dane
    wiersza oraz wybór modelu pozostają nietknięte.
    """

    item_id = (current_item_id or "").strip()
    label = playback_state_label(playing=playing, paused=paused)
    placement = normalize_state_position(position)
    result: list[Row] = []
    marked = False
    for row in rows:
        active = bool(
            not marked
            and label
            and row_matches_material(
                row, current_item_id=item_id, session_id=session_id
            )
        )
        prefix = ""
        detail = ""
        if active:
            marked = True
            if placement == "before":
                # ``playback_state`` is the legacy primary-column prefix used
                # by list reading; the underlying title stays untouched.
                prefix = label
            elif placement == "after":
                detail = label
        result.append(
            row if (
                row.playback_state == prefix
                and row.state_detail == detail
                and row.playback_activity == active
            ) else replace(
                row,
                playback_state=prefix,
                state_detail=detail,
                playback_activity=active,
            )
        )
    return result
