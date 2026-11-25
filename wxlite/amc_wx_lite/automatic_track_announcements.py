"""User preference for unsolicited now-playing speech."""

MODES = ("always", "active", "never")
LABELS = (
    "Zawsze oznajmiaj nowy utwór",
    "Oznajmiaj tylko w bieżącej sesji",
    "Nie oznajmiaj automatycznie",
)


def normalize_mode(value: object) -> str:
    return value if isinstance(value, str) and value in MODES else "always"


def should_announce(mode: object, source_session: object, active_session: object) -> bool:
    selected = normalize_mode(mode)
    return selected == "always" or (
        selected == "active" and source_session == active_session
    )
