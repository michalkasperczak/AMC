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


PLAYABLE_KINDS = frozenset(("track", "episode", "video", "recording"))


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
        and not row.item_id.startswith("bookmark:")
    )


def activity_rows(
    rows: Iterable[Row],
    *,
    current_item_id: str | None,
    playing: bool,
    paused: bool,
) -> list[Row]:
    """Ustaw stan tylko na dokładnie jednym, potwierdzonym materiale.

    Każde wywołanie czyści wcześniejszą dekorację, więc przejście A -> B,
    koniec odtwarzania i błąd nie zostawiają fałszywego napisu. Zwykłe dane
    wiersza oraz wybór modelu pozostają nietknięte.
    """

    item_id = (current_item_id or "").strip()
    label = playback_state_label(playing=playing, paused=paused)
    result: list[Row] = []
    marked = False
    for row in rows:
        state = ""
        if (
            not marked
            and label
            and item_id
            and row.item_id == item_id
            and row_can_show_playback_state(row)
        ):
            state = label
            marked = True
        result.append(
            row if row.playback_state == state else replace(row, playback_state=state)
        )
    return result
