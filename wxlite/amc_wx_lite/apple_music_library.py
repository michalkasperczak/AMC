"""Apple Music library adapter for the private AMC Test pilot.

The optional ``applemusic`` dependency is imported lazily.  Provider objects,
exceptions and response dictionaries are reduced to the narrow contract from
``apple_music_source`` before a caller can hand anything to the interface.
Only verified song/album membership changes are exposed beyond reading.
"""

from __future__ import annotations

from dataclasses import dataclass
import importlib
from urllib.parse import quote
from urllib.parse import urlsplit

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

_LIBRARY_PAGE_PATHS: dict[str, str] = {
    "songs": "/v1/me/library/songs",
    "albums": "/v1/me/library/albums",
    "artists": "/v1/me/library/artists",
    "playlists": "/v1/me/library/playlists",
}
_AMP_API_HOST = "amp-api.music.apple.com"
_MAX_CURSOR_LENGTH = 16 * 1024


class AppleMusicLibraryError(RuntimeError):
    """A deliberate message that never contains a provider exception."""

    def __str__(self) -> str:
        return "Nie udało się odczytać Biblioteki Apple Music."


class AppleMusicNotConnected(AppleMusicLibraryError):
    def __str__(self) -> str:
        return "Apple Music nie jest zalogowane."


class AppleMusicLibraryMutationError(AppleMusicLibraryError):
    """A sanitized failure specific to an account mutation."""

    def __str__(self) -> str:
        return "Nie udało się zmienić Biblioteki Apple Music."


@dataclass(frozen=True, slots=True, repr=False)
class AppleMusicPageCursor:
    """Opaque continuation owned by the adapter, never presentation text."""

    view: str
    _path: str
    _base_path: str

    def __repr__(self) -> str:
        return "AppleMusicPageCursor()"

    def __str__(self) -> str:
        return "Dalsza strona Apple Music"


@dataclass(frozen=True, slots=True)
class AppleMusicLibraryResult:
    heading: str
    records: tuple[AppleMusicRecord, ...]
    next_cursor: AppleMusicPageCursor | None = None

    @property
    def rows(self) -> tuple[Row, ...]:
        return tuple(record.row for record in self.records)


@dataclass(frozen=True, slots=True)
class AppleMusicLibraryMutation:
    """Confirmed membership result without provider identifiers."""

    added: bool
    item_kind: str


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

    def view_page(
        self,
        view: str,
        *,
        cursor: AppleMusicPageCursor | None = None,
        page_size: int = 25,
    ) -> AppleMusicLibraryResult:
        """Read one bounded page and keep the provider cursor outside rows."""

        spec = _LIBRARY_VIEWS.get(view)
        base_path = _LIBRARY_PAGE_PATHS.get(view)
        if (
            spec is None
            or base_path is None
            or isinstance(page_size, bool)
            or not isinstance(page_size, int)
            or not 1 <= page_size <= 100
        ):
            raise AppleMusicLibraryError()
        heading, _method_name = spec
        if cursor is None:
            path = base_path
            params: dict[str, object] | None = {
                "l": "pl-PL",
                "limit": page_size,
            }
        else:
            if not isinstance(cursor, AppleMusicPageCursor) or cursor.view != view:
                raise AppleMusicLibraryError()
            path = cursor._path
            params = None
        try:
            getter = getattr(self._client, "_get")
            envelope = getter(path, params)
            if not isinstance(envelope, dict):
                raise AppleMusicLibraryError()
            raw = envelope.get("data")
            if not isinstance(raw, list):
                raise AppleMusicLibraryError()
            next_cursor = _page_cursor(view, envelope.get("next"), base_path)
            return AppleMusicLibraryResult(
                heading=heading,
                records=records_from_resources(raw),
                next_cursor=next_cursor,
            )
        except AppleMusicLibraryError:
            raise
        except Exception:
            raise AppleMusicLibraryError() from None

    def playlist_tracks_page(
        self,
        playlist: AppleMusicRecord,
        *,
        cursor: AppleMusicPageCursor | None = None,
        page_size: int = 25,
    ) -> AppleMusicLibraryResult:
        """Read one bounded page tied to exactly one library playlist."""

        target = playlist.target
        if (
            target.resource_type != "library-playlists"
            or not target.from_library
            or isinstance(page_size, bool)
            or not isinstance(page_size, int)
            or not 1 <= page_size <= 100
        ):
            raise AppleMusicLibraryError()
        base_path = (
            "/v1/me/library/playlists/"
            + quote(target.resource_id, safe="")
            + "/tracks"
        )
        scope = "playlist-tracks:" + target.resource_id
        if cursor is None:
            path = base_path
            params: dict[str, object] | None = {
                "l": "pl-PL",
                "limit": page_size,
            }
        else:
            if (
                not isinstance(cursor, AppleMusicPageCursor)
                or cursor.view != scope
                or cursor._base_path != base_path
            ):
                raise AppleMusicLibraryError()
            path = cursor._path
            params = None
        try:
            getter = getattr(self._client, "_get")
            envelope = getter(path, params)
            if not isinstance(envelope, dict):
                raise AppleMusicLibraryError()
            raw = envelope.get("data")
            if not isinstance(raw, list):
                raise AppleMusicLibraryError()
            return AppleMusicLibraryResult(
                heading=f"Playlista: {playlist.row.title}",
                records=records_from_resources(raw),
                next_cursor=_page_cursor(scope, envelope.get("next"), base_path),
            )
        except AppleMusicLibraryError:
            raise
        except Exception:
            raise AppleMusicLibraryError() from None

    def album_tracks_page(
        self,
        album: AppleMusicRecord,
        *,
        cursor: AppleMusicPageCursor | None = None,
        page_size: int = 25,
    ) -> AppleMusicLibraryResult:
        """Read one page of a library album's tracks through its relationship."""

        target = album.target
        if (
            target.resource_type != "library-albums"
            or not target.from_library
            or isinstance(page_size, bool)
            or not isinstance(page_size, int)
            or not 1 <= page_size <= 100
        ):
            raise AppleMusicLibraryError()
        base_path = (
            "/v1/me/library/albums/"
            + quote(target.resource_id, safe="")
            + "/tracks"
        )
        scope = "album-tracks:" + target.resource_id
        if cursor is None:
            path = base_path
            params: dict[str, object] | None = {
                "l": "pl-PL",
                "limit": page_size,
            }
        else:
            if (
                not isinstance(cursor, AppleMusicPageCursor)
                or cursor.view != scope
                or cursor._base_path != base_path
            ):
                raise AppleMusicLibraryError()
            path = cursor._path
            params = None
        try:
            getter = getattr(self._client, "_get")
            envelope = getter(path, params)
            if not isinstance(envelope, dict):
                raise AppleMusicLibraryError()
            raw = envelope.get("data")
            if not isinstance(raw, list):
                raise AppleMusicLibraryError()
            return AppleMusicLibraryResult(
                heading=f"Album: {album.row.title}",
                records=records_from_resources(raw),
                next_cursor=_page_cursor(scope, envelope.get("next"), base_path),
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

    def toggle_library(
        self, record: AppleMusicRecord
    ) -> AppleMusicLibraryMutation:
        """Add a catalog song/album or remove its library counterpart.

        Apple exposes separate identifiers for catalog and library resources.
        Keep that distinction inside the adapter and support only the two
        reversible kinds whose add and remove endpoints are both available.
        """

        if not isinstance(record, AppleMusicRecord):
            raise AppleMusicLibraryMutationError()
        target = record.target
        try:
            if target.resource_type == "library-songs" and target.from_library:
                self._client.library_remove_song(target.resource_id)
                return AppleMusicLibraryMutation(False, "track")
            if target.resource_type == "library-albums" and target.from_library:
                self._client.library_remove_album(target.resource_id)
                return AppleMusicLibraryMutation(False, "album")
            catalog_id = target.catalog_id
            if not target.from_library and catalog_id:
                if target.resource_type == "songs":
                    self._client.library_add(songs=[catalog_id])
                    return AppleMusicLibraryMutation(True, "track")
                if target.resource_type == "albums":
                    self._client.library_add(albums=[catalog_id])
                    return AppleMusicLibraryMutation(True, "album")
            raise AppleMusicLibraryMutationError()
        except AppleMusicLibraryMutationError:
            raise
        except Exception:
            raise AppleMusicLibraryMutationError() from None

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


def library_membership_direction(record: AppleMusicRecord | None) -> str | None:
    """Return the supported direction without leaking provider identity."""

    if not isinstance(record, AppleMusicRecord):
        return None
    target = record.target
    if target.from_library and target.resource_type in {
        "library-songs",
        "library-albums",
    }:
        return "remove"
    if (
        not target.from_library
        and bool(target.catalog_id)
        and target.resource_type in {"songs", "albums"}
    ):
        return "add"
    return None


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


def _page_cursor(
    view: str,
    value: object,
    base_path: str,
) -> AppleMusicPageCursor | None:
    """Accept only same-host continuation paths for the selected collection."""

    if value in (None, ""):
        return None
    if (
        not isinstance(value, str)
        or not 0 < len(value) <= _MAX_CURSOR_LENGTH
        or any(character in value for character in ("\r", "\n", "\x00"))
    ):
        raise AppleMusicLibraryError()
    if not isinstance(base_path, str) or not base_path.startswith("/v1/me/library/"):
        raise AppleMusicLibraryError()
    parsed = urlsplit(value)
    if parsed.fragment or parsed.username or parsed.password:
        raise AppleMusicLibraryError()
    if parsed.scheme or parsed.netloc:
        if parsed.scheme != "https" or parsed.netloc != _AMP_API_HOST:
            raise AppleMusicLibraryError()
    if parsed.path != base_path:
        raise AppleMusicLibraryError()
    path = parsed.path
    if parsed.query:
        path += "?" + parsed.query
    return AppleMusicPageCursor(view=view, _path=path, _base_path=base_path)
