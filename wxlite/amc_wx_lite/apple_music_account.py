"""Safe account-level actions for the experimental AMC Test Apple Music pilot.

The default status check is deliberately local: it reads only AMC's DPAPI
blob and never validates it on the network or opens Chromium.  A browser may
be opened only through the explicit ``connect`` function.
"""

from __future__ import annotations

from dataclasses import dataclass
from typing import Callable

from .apple_music_credentials import (
    AppleMusicCredentialStore,
    AppleMusicCredentialStoreError,
    AppleMusicLibraryUnavailable,
    create_secure_library_auth,
)
from .profile_layout import ProfileLayout
from .release_profile import TEST_PROFILE_ID, ReleaseProfile, current_profile


class AppleMusicAccountError(RuntimeError):
    """One intentionally generic account message without provider details."""

    def __init__(self, stage: str = "generic") -> None:
        self.stage = stage

    def __str__(self) -> str:
        return {
            "browser": "Nie można uruchomić przeglądarki logowania Apple Music.",
            "page": "Nie udało się otworzyć strony logowania Apple Music.",
            "login": "Logowanie Apple Music zostało przerwane lub nie zakończyło się.",
            "capture": "Apple Music nie przekazało sesji po logowaniu.",
            "validate": "Apple Music odrzuciło nową sesję.",
            "save": "Nie udało się bezpiecznie zapisać sesji Apple Music.",
        }.get(self.stage, "Nie udało się połączyć konta Apple Music.")


@dataclass(frozen=True, slots=True)
class AppleMusicAccountStatus:
    stored_session: bool
    message: str


AuthFactory = Callable[..., object]


def stored_account_status(
    layout: ProfileLayout,
    *,
    profile: ReleaseProfile | None = None,
    store: AppleMusicCredentialStore | None = None,
) -> AppleMusicAccountStatus:
    """Report only whether an encrypted blob exists and decodes locally."""

    selected = profile or current_profile()
    if selected.profile_id != TEST_PROFILE_ID:
        raise AppleMusicAccountError()
    secure_store = store or AppleMusicCredentialStore(layout, profile=selected)
    try:
        saved = secure_store.read()
    except AppleMusicCredentialStoreError:
        raise AppleMusicAccountError() from None
    if saved is None:
        return AppleMusicAccountStatus(
            stored_session=False,
            message="Apple Music: brak zapisanej sesji.",
        )
    return AppleMusicAccountStatus(
        stored_session=True,
        message=(
            "Apple Music: zapisano zaszyfrowaną sesję. "
            "Jej ważność nie została sprawdzona."
        ),
    )


def connect_account(
    layout: ProfileLayout,
    *,
    profile: ReleaseProfile | None = None,
    auth_factory: AuthFactory = create_secure_library_auth,
    auth_timeout: float = 900.0,
) -> AppleMusicAccountStatus:
    """Allow a visible Apple login only after an explicit caller action."""

    selected = profile or current_profile()
    if selected.profile_id != TEST_PROFILE_ID:
        raise AppleMusicAccountError()
    auth = None
    try:
        auth = auth_factory(
            layout,
            profile=selected,
            auth_timeout=auth_timeout,
        )
        authenticate = getattr(auth, "authenticate")
        # This call follows an explicit Yes to opening a visible login window.
        # The upstream default silently returns a valid cached session instead.
        authenticate(force=True)
    except AppleMusicCredentialStoreError:
        raise AppleMusicAccountError("save") from None
    except AppleMusicLibraryUnavailable:
        raise AppleMusicAccountError("browser") from None
    except Exception:
        # Upstream errors can contain URLs, headers or raw provider messages.
        stage = getattr(auth, "_login_stage", "generic")
        raise AppleMusicAccountError(stage) from None
    return AppleMusicAccountStatus(
        stored_session=True,
        message="Apple Music: logowanie zakończone i sesja zapisana bezpiecznie.",
    )
