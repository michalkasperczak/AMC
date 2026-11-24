"""Stable keyboard cycling of a session's available network audio targets."""

from __future__ import annotations

from collections.abc import Iterable
from dataclasses import dataclass


@dataclass(frozen=True, slots=True)
class NetworkOutputChoice:
    device_id: str
    label: str
    available: bool = True

    def __str__(self) -> str:
        return self.label


def adjacent_target_id(
    available_ids: Iterable[str], selected_id: str | None, direction: int,
) -> str | None:
    """Cycle through off/local and each available target, wrapping at the ends."""

    if direction not in (-1, 1):
        raise ValueError("Kierunek musi wynosić -1 albo 1.")
    values: tuple[str | None, ...] = (None, *dict.fromkeys(
        value for value in available_ids if isinstance(value, str) and value
    ))
    index = values.index(selected_id) if selected_id in values else 0
    return values[(index + direction) % len(values)]
