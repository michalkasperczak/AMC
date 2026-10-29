"""Prywatna kolejność sesji i wynikające z niej skróty ``Ctrl+1–9``.

Wartości modelu pozostają oddzielone od tekstów dostępnościowych.  Ten moduł
nie zna wx, dzięki czemu walidację zapisu, zawijanie kolejności i mapowanie
skrótów można sprawdzić bez otwierania okna.
"""

from __future__ import annotations

from collections.abc import Iterable

from . import i18n
from .navigation import SessionId
from .release_profile import current_profile
from .shortcuts import Action


# Świeży profil zaczyna od trzech zawsze użytecznych sesji. Pozostałe można
# świadomie dopisać w Menedżerze sesji. Istniejący profil jest migrowany
# osobną funkcją poniżej, aby aktualizacja nie wyłączyła mu usług.
DEFAULT_SESSION_ORDER: tuple[SessionId, ...] = (
    SessionId.FILES,
    SessionId.RADIO,
    SessionId.PODCASTS,
)
LEGACY_SESSION_ORDER: tuple[SessionId, ...] = tuple(SessionId)
AVAILABLE_TEST_SESSIONS: tuple[SessionId, ...] = tuple(SessionId)
MAX_ACTIVE_SESSIONS = 9

SESSION_ACTIONS: dict[SessionId, Action] = {
    SessionId.FILES: Action.SESSION_FILES,
    SessionId.RADIO: Action.SESSION_RADIO,
    SessionId.PODCASTS: Action.SESSION_PODCASTS,
    SessionId.TIDAL: Action.SESSION_TIDAL,
    SessionId.WIIM: Action.SESSION_WIIM,
    SessionId.SONOS: Action.SESSION_SONOS,
    SessionId.SPOTIFY: Action.SESSION_SPOTIFY,
    SessionId.FRONTIER_SMART: Action.SESSION_FRONTIER_SMART,
    SessionId.RADIOARCHIWISTA: Action.SESSION_RADIOARCHIWISTA,
}

SESSION_NAMES: dict[SessionId, str] = {
    SessionId.FILES: "Pliki lokalne",
    SessionId.RADIO: "Radio",
    SessionId.PODCASTS: "Podcasty i YouTube",
    SessionId.TIDAL: "TIDAL",
    SessionId.WIIM: "WiiM",
    SessionId.SONOS: "Sonos",
    SessionId.SPOTIFY: "Spotify",
    SessionId.FRONTIER_SMART: "Frontier Smart",
    SessionId.RADIOARCHIWISTA: "Radio Archiwista",
}

SESSION_MENU_LABELS: dict[SessionId, str] = {
    SessionId.FILES: "Sesja: &Pliki lokalne",
    SessionId.RADIO: "Sesja: &Radio",
    SessionId.PODCASTS: "Sesja: Podcasty i &YouTube",
    SessionId.TIDAL: "Sesja: &TIDAL",
    SessionId.WIIM: "Sesja: &WiiM",
    SessionId.SONOS: "Sesja: &Sonos",
    SessionId.SPOTIFY: "Sesja: Spoti&fy",
    SessionId.FRONTIER_SMART: "Sesja: Frontier S&mart",
    SessionId.RADIOARCHIWISTA: "Sesja: Radio &Archiwista",
}


def session_name(session: SessionId) -> str:
    """Intentional label derived from the stable session Action."""

    return i18n.command_name(
        SESSION_ACTIONS[session].value,
        SESSION_NAMES[session],
    )


def _message(polish: str, english: str) -> str:
    return english if i18n.active_language() == i18n.ENGLISH else polish


def available_sessions() -> tuple[SessionId, ...]:
    """Sesje jawnie dopuszczone przez manifest bieżącego wydania."""

    allowed: list[SessionId] = []
    for value in current_profile().session_ids:
        try:
            session = SessionId(value)
        except ValueError:
            continue
        if session not in allowed:
            allowed.append(session)
    return tuple(allowed)


def normalize(raw: object) -> tuple[SessionId, ...]:
    """Zachowaj zwartą listę aktywnych sesji, bez dopisywania wyłączonych."""

    values: Iterable[object]
    if isinstance(raw, (list, tuple)):
        values = raw
    else:
        values = ()

    result: list[SessionId] = []
    seen: set[SessionId] = set()
    allowed = set(available_sessions())
    for value in values:
        try:
            session = value if isinstance(value, SessionId) else SessionId(value)
        except (TypeError, ValueError):
            continue
        if session in seen or session not in allowed:
            continue
        seen.add(session)
        result.append(session)

    if not result:
        result.extend(session for session in DEFAULT_SESSION_ORDER if session in allowed)
    return tuple(result[:MAX_ACTIVE_SESSIONS])


def migrate_legacy(raw: object) -> tuple[SessionId, ...]:
    """Jednorazowo zachowaj wszystkie sesje starego profilu i ich kolejność."""

    migrated = list(normalize(raw))
    migrated.extend(
        session
        for session in LEGACY_SESSION_ORDER
        if session in available_sessions() and session not in migrated
    )
    return tuple(migrated[:MAX_ACTIVE_SESSIONS])


def values(raw: object) -> list[str]:
    """Wartości do prywatnego JSON-u, nigdy etykiety wypowiadane przez NVDA."""

    return [session.value for session in normalize(raw)]


def migrated_values(raw: object) -> list[str]:
    """Wartości jednorazowej migracji starego zapisu kolejności."""

    return [session.value for session in migrate_legacy(raw)]


def slot_for_session(session: SessionId, raw: object) -> int | None:
    ordered = normalize(raw)
    try:
        return ordered.index(session) + 1
    except ValueError:
        return None


def session_for_slot(slot: int, raw: object) -> SessionId | None:
    ordered = normalize(raw)
    index = slot - 1
    return ordered[index] if 0 <= index < len(ordered) else None


def action_for_slot(slot: int, raw: object) -> Action | None:
    session = session_for_slot(slot, raw)
    return SESSION_ACTIONS.get(session) if session is not None else None


def adjacent(current: SessionId, direction: int, raw: object) -> SessionId:
    """Poprzednia/następna sesja z zawijaniem na obu końcach."""

    ordered = normalize(raw)
    try:
        index = ordered.index(current)
    except ValueError:
        index = 0
    step = -1 if direction < 0 else 1
    return ordered[(index + step) % len(ordered)]


def enable(session: SessionId, raw: object) -> tuple[tuple[SessionId, ...], str]:
    """Dopisz sesję na końcu bez naruszania limitu dziewięciu."""

    ordered = list(normalize(raw))
    if session not in available_sessions():
        return tuple(ordered), _message(
            "Ta sesja nie jest dostępna w tym wydaniu AMC",
            "This session is not available in this AMC edition",
        )
    if session in ordered:
        return tuple(ordered), _message(
            f"{SESSION_NAMES[session]} jest już włączona",
            f"{session_name(session)} is already enabled",
        )
    if len(ordered) >= MAX_ACTIVE_SESSIONS:
        return (
            tuple(ordered),
            _message(
                "Można włączyć najwyżej dziewięć sesji. Najpierw wyłącz jedną z nich",
                "At most nine sessions can be enabled. Disable one first",
            ),
        )
    ordered.append(session)
    return tuple(ordered), _message(
        f"Włączono sesję {SESSION_NAMES[session]}, Ctrl+{len(ordered)}",
        f"Enabled {session_name(session)}, Ctrl+{len(ordered)}",
    )


def disable(
    session: SessionId,
    raw: object,
    *,
    current: SessionId,
    blocked: Iterable[SessionId] = (),
) -> tuple[tuple[SessionId, ...], SessionId, str]:
    """Wyłącz sesję, wybierając zwarty, bezpieczny nowy cel interfejsu."""

    ordered = list(normalize(raw))
    if session not in ordered:
        return tuple(ordered), current, _message(
            f"{SESSION_NAMES[session]} jest już wyłączona",
            f"{session_name(session)} is already disabled",
        )
    if len(ordered) == 1:
        return tuple(ordered), current, _message(
            "Nie można wyłączyć ostatniej aktywnej sesji",
            "The last active session cannot be disabled",
        )
    if session in set(blocked):
        return (
            tuple(ordered),
            current,
            _message(
                f"Nie można teraz wyłączyć sesji {SESSION_NAMES[session]}, ponieważ trwa odtwarzanie albo nagrywanie",
                f"{session_name(session)} cannot be disabled while playback or recording is active",
            ),
        )
    index = ordered.index(session)
    ordered.remove(session)
    if current is session:
        current = ordered[min(index, len(ordered) - 1)]
    return tuple(ordered), current, _message(
        f"Wyłączono sesję {SESSION_NAMES[session]}",
        f"Disabled {session_name(session)}",
    )


def move(
    session: SessionId,
    direction: int,
    raw: object,
) -> tuple[tuple[SessionId, ...], str]:
    """Przesuń aktywną sesję o jedno miejsce i od razu przelicz skróty."""

    ordered = list(normalize(raw))
    if session not in ordered:
        return tuple(ordered), _message(
            f"{SESSION_NAMES[session]} jest wyłączona",
            f"{session_name(session)} is disabled",
        )
    index = ordered.index(session)
    target = index + (-1 if direction < 0 else 1)
    if target < 0 or target >= len(ordered):
        return tuple(ordered), _message(
            "Sesja jest już na skraju listy",
            "The session is already at the edge of the list",
        )
    ordered[index], ordered[target] = ordered[target], ordered[index]
    return (
        tuple(ordered),
        f"{session_name(session)}, Ctrl+{target + 1}",
    )


def manager_labels(
    raw: object,
    readiness: dict[SessionId, str] | None = None,
    *,
    available: Iterable[SessionId] | None = None,
) -> tuple[tuple[SessionId, str], ...]:
    """Celowe etykiety Menedżera: aktywne najpierw, potem dostępne do dodania."""

    ready = readiness or {}
    allowed = tuple(dict.fromkeys(available_sessions() if available is None else available))
    active = tuple(session for session in normalize(raw) if session in allowed)
    inactive = tuple(session for session in allowed if session not in active)
    result: list[tuple[SessionId, str]] = []
    for slot, session in enumerate(active, start=1):
        status = ready.get(session, _message("Wymaga konfiguracji", "Needs configuration"))
        result.append((
            session,
            _message(
                f"Ctrl+{slot}, {SESSION_NAMES[session]}, włączona, {status.casefold()}",
                f"Ctrl+{slot}, {session_name(session)}, enabled, {status.casefold()}",
            ),
        ))
    for session in inactive:
        status = ready.get(session, _message("Wymaga konfiguracji", "Needs configuration"))
        result.append((
            session,
            _message(
                f"{SESSION_NAMES[session]}, wyłączona, {status.casefold()}",
                f"{session_name(session)}, disabled, {status.casefold()}",
            ),
        ))
    return tuple(result)


def selection_labels(raw: object) -> tuple[str, ...]:
    return tuple(
        f"Ctrl+{slot}, {session_name(session)}"
        for slot, session in enumerate(normalize(raw), start=1)
    )
