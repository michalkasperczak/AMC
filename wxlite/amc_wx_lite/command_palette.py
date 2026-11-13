"""Dane dostępnej palety poleceń AMC Python, bez zależności od wx.

Paleta nie przechowuje identyfikatorów tekstowych ani nazw enumów w etykiecie.
Każdy wpis prowadzi do istniejącej :class:`Action`, tej samej co menu i skrót.
"""

from __future__ import annotations

from dataclasses import dataclass
from typing import Iterable, Mapping

from . import i18n
from . import menu_model
from . import session_order as session_order_model
from .navigation import SessionId
from .search_catalog import normalize_search_text, query_words
from .shortcuts import Action, command_presentation, ctrl_f5_presentation


@dataclass(frozen=True, slots=True)
class CommandPaletteEntry:
    action: Action
    name: str
    shortcut: str | None = None

    @property
    def label(self) -> str:
        if self.shortcut and self.shortcut.casefold() not in self.name.casefold():
            return f"{self.name}, {self.shortcut}"
        return self.name


def _plain_label(value: str) -> str:
    return " ".join(value.replace("&", "").split()).strip()


def _shortcut_label(value: str | None) -> str | None:
    if not value:
        return None
    parts = value.split("+")
    key = parts[-1]
    parts[-1] = {
        "Prior": "Page Up",
        "Next": "Page Down",
        "Return": "Enter",
        "Back": "Backspace",
        "Space": i18n.tr("shortcut.space"),
        "Left": i18n.tr("shortcut.left"),
        "Right": i18n.tr("shortcut.right"),
        "Up": i18n.tr("shortcut.up"),
        "Down": i18n.tr("shortcut.down"),
    }.get(key, key)
    return "+".join(parts)


def entries_for_session(
    session: SessionId,
    session_order: object = None,
    *,
    youtube_account_available: bool = False,
    shortcut_overrides: Mapping[Action, str] | None = None,
) -> tuple[CommandPaletteEntry, ...]:
    """Zbuduj po jednym użytkowym wpisie na akcję widoczną w tej sesji."""

    result: list[CommandPaletteEntry] = []
    seen: set[Action] = set()
    for menu in menu_model.menus_for_session(
        session,
        session_order,
        youtube_account_available=youtube_account_available,
        shortcut_overrides=shortcut_overrides,
    ):
        for item in menu.items:
            if item.is_separator or item.action is None or item.action in seen:
                continue
            ctrl_f5 = ctrl_f5_presentation(
                session_order_model.session_name(session)
            )
            shared = command_presentation(item.action)
            name = (
                ctrl_f5.name
                if ctrl_f5 is not None and item.action is ctrl_f5.action
                else shared.name
                if shared is not None
                else _plain_label(item.label)
            )
            if session is SessionId.APPLE_MUSIC and item.action in {
                Action.QUEUE_NEXT, Action.QUEUE_PREVIOUS
            }:
                name = _plain_label(item.label)
            if not name:
                continue
            seen.add(item.action)
            result.append(CommandPaletteEntry(
                action=item.action,
                name=name,
                shortcut=_shortcut_label(item.shortcut),
            ))
    return tuple(sorted(
        result,
        key=lambda entry: (normalize_search_text(entry.name), entry.label.casefold()),
    ))


def filter_entries(
    entries: tuple[CommandPaletteEntry, ...], query: str
) -> tuple[CommandPaletteEntry, ...]:
    """Filtruj po wszystkich słowach, bez rozróżniania wielkości i akcentów."""

    words = query_words(query)
    if not words:
        return entries
    return tuple(
        entry
        for entry in entries
        if all(word in normalize_search_text(entry.label) for word in words)
    )


def available_entries(
    entries: tuple[CommandPaletteEntry, ...],
    actions: Iterable[Action],
) -> tuple[CommandPaletteEntry, ...]:
    """Zostaw tylko polecenia aktywne w bieżącym widoku i zaznaczeniu."""

    available = set(actions)
    return tuple(entry for entry in entries if entry.action in available)


def count_label(count: int) -> str:
    if count == 0:
        return i18n.tr("palette.count.none")
    if count == 1:
        return i18n.tr("palette.count.one")
    if 2 <= count <= 4:
        return i18n.tr("palette.count.few", count=count)
    return i18n.tr("palette.count.many", count=count)
