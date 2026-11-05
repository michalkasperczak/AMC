"""Dostępność wspólnych chwilowych podglądów zgodna z nowym AMC."""

from __future__ import annotations

import json
from pathlib import Path

from .navigation import SessionId
from .shortcuts import Action


TRANSIENT_PREVIEW_ACTIONS = frozenset({
    Action.VIEW_ACTIVE_RECORDINGS,
    Action.VIEW_RECORDED_RADIO_FILES,
    Action.VIEW_DOWNLOADED_FILES,
    Action.VIEW_PODCAST_INBOX,
    Action.VIEW_YOUTUBE_LIVE_AGGREGATE,
    Action.VIEW_HISTORY,
})

# Te listy są częstymi punktami nawigacji z każdej sesji. Ustawienie
# wspólnych podglądów nadal dotyczy transmisji wybranych kanałów.
ALWAYS_AVAILABLE_PREVIEWS = frozenset({
    Action.VIEW_ACTIVE_RECORDINGS,
    Action.VIEW_RECORDED_RADIO_FILES,
    Action.VIEW_DOWNLOADED_FILES,
    Action.VIEW_HISTORY,
    Action.VIEW_PODCAST_INBOX,
})


def load_global_transient_previews(state_path: Path) -> bool:
    """Odczytaj wcześniejszy wybór bez zapisywania wspólnego profilu.

    Brak albo uszkodzenie starego profilu wraca do domyślnego ``True`` z
    ``AppSettings.GlobalTransientPreviews``. To ustawienie nie jest destrukcyjne.
    """
    try:
        raw = json.loads(Path(state_path).read_text(encoding="utf-8-sig"))
    except (FileNotFoundError, json.JSONDecodeError, UnicodeDecodeError, OSError):
        return True
    if not isinstance(raw, dict):
        return True
    settings = raw.get("settings")
    if not isinstance(settings, dict):
        return True
    value = settings.get("globalTransientPreviews")
    return value if type(value) is bool else True


def resolve_global_transient_previews(
    override: bool | None,
    inherited: bool,
) -> bool:
    return override if type(override) is bool else inherited


def is_transient_preview_available(
    action: Action,
    session: SessionId,
    *,
    global_previews: bool,
) -> bool:
    if action not in TRANSIENT_PREVIEW_ACTIONS:
        return False
    if action in ALWAYS_AVAILABLE_PREVIEWS:
        return True
    if global_previews:
        return True
    return session is SessionId.PODCASTS


def transient_preview_unavailable_message(action: Action) -> str:
    suffix = " Wspólne podglądy ze wszystkich sesji włączysz w Ustawieniach"
    if action is Action.VIEW_YOUTUBE_LIVE_AGGREGATE:
        return (
            "Transmisje wybranych kanałów są dostępne w sesji Podcasty "
            "i YouTube." + suffix
        )
    return ""
