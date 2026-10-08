"""Dostepne wiersze harmonogramu; tresc etykiet przychodzi z C# AMC."""

from __future__ import annotations

from .list_model import Row


def schedule_rows(payload: object) -> list[Row]:
    """Zamien odpowiedz formattera C# na wiersze bez technicznych nazw."""
    if not isinstance(payload, dict):
        return []
    source = payload.get("schedules")
    if not isinstance(source, list):
        return []
    rows: list[Row] = []
    for index, item in enumerate(source):
        if not isinstance(item, dict):
            continue
        label = item.get("label")
        navigation_text = item.get("navigationText")
        if not isinstance(label, str) or not label.strip():
            continue
        name = (
            navigation_text.strip()
            if isinstance(navigation_text, str) and navigation_text.strip()
            else "Wybrany plan"
        )
        raw_id = item.get("id")
        item_id = raw_id if isinstance(raw_id, str) and raw_id else f"wiersz-{index}"
        rows.append(Row(
            item_id=f"radio-schedule:{item_id}",
            title=label.strip(),
            kind="track",
            show_kind=False,
            activation_message=(
                f"{name}. Plan jest wykonywany automatycznie przez AMC wxPython. "
                "Edycję planu wykonaj w głównym AMC"
            ),
        ))
    return rows
