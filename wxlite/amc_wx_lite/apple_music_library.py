"""Read-only Apple Music library adapter for the private AMC Test pilot.

The optional ``applemusic`` dependency is imported lazily.  Provider objects,
exceptions and response dictionaries are reduced to the narrow contract from
``apple_music_source`` before a caller can hand anything to the interface.
"""

from __future__ import annotations

from dataclasses import dataclass
import importlib

from .apple_music_credentials import (
    AppleMusicCredentialStore,
    AppleMusicCredentialStoreError,
)
from .apple_music_source import AppleMusicRecord, records_from_resources
from .list_model import Row
from .profile_layout import ProfileLayout
from .release_profile import TEST_PROFILE_ID, ReleaseProfile, current_profile


_LIBRARY_VIEWS: dict[str, tuple[str, str]] = {
    "songs": ("Utwory Apple Music", "current_user_saved_tracks"),
    "albums": ("Albumy Apple Music", "current_user_saved_albums"),
    "artists": ("Wykonawcy Apple Music", "current_user_saved_artists"),
    "playlists": ("Playlisty Apple Music", "current_user_playlists"),
}


class AppleMusicLibraryError(RuntimeError):
    """A deliberate message that never contains a provider exception."""

    def __str__(self) -> str:
        return "Nie udało się odczytać Biblioteki Apple Music."


class AppleMusicNotConnected(AppleMusicLibraryError):
    def __str__(self) -> str:
        return "Apple Music nie jest zalogowane."


@dataclass(frozen=True, slots=True)
class AppleMusicLibraryResult:
    heading: str
    records: tuple[AppleMusicRecord, ...]

    @property
    def rows(self) -> tuple[Row, ...]:
        return tuple(record.row for record in self.records)


class AppleMusicLibraryClient:
    """Own one authenticated unofficial client without exposing it to wx."""

    def __init__(self, client: object) -> None:
        self._client = client

    def view(self, view: str) -> AppleMusicLibraryResult:
        spec = _LIBRARY_VIEWS.get(view)
        if spec is None:
            raise AppleMusicLibraryError()
        heading, method_name = spec
        try:
            method = getattr(self._client, method_name)
            raw = method()
            return AppleMusicLibraryResult(
                heading=heading,
                records=records_from_resources(raw),
            )
        except AppleMusicLibraryError:
            raise
        except Exception:
            raise AppleMusicLibraryError() from None

    def playlist_tracks(self, playlist: AppleMusicRecord) -> AppleMusicLibraryResult:
        target = playlist.target
        if target.resource_type != "library-playlists" or not target.from_library:
            raise AppleMusicLibraryError()
        try:
            raw = self._client.playlist_tracks(target.resource_id)
            return AppleMusicLibraryResult(
                heading=f"Playlista: {playlist.row.title}",
                records=records_from_resources(raw),
            )
        except AppleMusicLibraryError:
            raise
        except Exception:
            raise AppleMusicLibraryError() from None

    def close(self) -> None:
        try:
            close = getattr(self._client, "close")
            close()
        except Exception:
            raise AppleMusicLibraryError() from None

    def __enter__(self) -> "AppleMusicLibraryClient":
        return self

    def __exit__(self, *_args: object) -> None:
        self.close()


def open_apple_music_library(
    layout: ProfileLayout,
    *,
    profile: ReleaseProfile | None = None,
    store: AppleMusicCredentialStore | None = None,
    client_module: object | None = None,
) -> AppleMusicLibraryClient:
    """Open the read-only adapter only for an already connected AMC Test."""

    selected = profile or current_profile()
    if selected.profile_id != TEST_PROFILE_ID:
        raise AppleMusicLibraryError()
    secure_store = store or AppleMusicCredentialStore(layout, profile=selected)
    try:
        saved = secure_store.read()
    except AppleMusicCredentialStoreError:
        raise AppleMusicLibraryError() from None
    if saved is None:
        raise AppleMusicNotConnected()

    try:
        module = client_module or importlib.import_module("applemusic")
        external_credentials = getattr(module, "AppleMusicCredentials")
        external_client = getattr(module, "AppleMusic")
        credentials = external_credentials(
            developer_token=saved.developer_token,
            media_user_token=saved.media_user_token,
            cookie_header=saved.cookie_header,
        )
        client = external_client(credentials, language="pl-PL")
    except Exception:
        raise AppleMusicLibraryError() from None
    return AppleMusicLibraryClient(client)
