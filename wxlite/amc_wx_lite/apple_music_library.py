"""Apple Music library adapter for the private AMC Test pilot.

The optional ``applemusic`` dependency is imported lazily.  Provider objects,
exceptions and response dictionaries are reduced to the narrow contract from
``apple_music_source`` before a caller can hand anything to the interface.
Only verified song/album membership changes are exposed beyond reading.
"""

from __future__ import annotations

from dataclasses import dataclass, replace
import importlib
import re
from typing import Callable
from urllib.parse import parse_qsl, quote, urlencode, urlsplit

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
_CATALOG_ARTIST_VIEWS = {
    "full-albums": "Albumy",
    "singles": "Single i EP",
    "top-songs": "Popularne utwory",
    "featured-playlists": "Playlisty z wykonawcą",
}


class AppleMusicLibraryError(RuntimeError):
    """A deliberate message that never contains a provider exception."""

    def __str__(self) -> str:
        return "Nie udało się odczytać Biblioteki Apple Music."


class AppleMusicNotConnected(AppleMusicLibraryError):
    def __str__(self) -> str:
        return "Apple Music nie jest zalogowane."


class AppleMusicAuthenticationRejected(AppleMusicLibraryError):
    def __str__(self) -> str:
        return "Apple Music odrzuciło logowanie. Otwórz Konto Apple Music (Ctrl+F5)."


class AppleMusicConnectionUnavailable(AppleMusicLibraryError):
    def __str__(self) -> str:
        return "Nie udało się połączyć z Apple Music. Spróbuj ponownie."


def search_failure_message(error: AppleMusicLibraryError, fallback: str) -> str:
    """Expose only intentional, actionable account/search messages to NVDA."""

    if isinstance(error, AppleMusicNotConnected):
        return str(error) + " Otwórz Konto Apple Music (Ctrl+F5)."
    if isinstance(error, (AppleMusicAuthenticationRejected, AppleMusicConnectionUnavailable)):
        return str(error)
    return fallback


def _sanitized_search_error(error: Exception) -> AppleMusicLibraryError:
    # The optional provider puts HTTP 401 and network failures on this numeric
    # field. Never include its exception text or response body in UI messages.
    status_code = getattr(error, "status_code", None)
    if type(status_code) is int and status_code == 401:
        return AppleMusicAuthenticationRejected()
    if type(status_code) is int and status_code == 0:
        return AppleMusicConnectionUnavailable()
    return AppleMusicLibraryError()


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
    _page_size: int = 100

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


@dataclass(frozen=True, slots=True, repr=False)
class AppleMusicRelationChoice:
    """Intentional spoken label and private target for a search relation."""

    label: str
    record: AppleMusicRecord
    view: str = ""

    def __str__(self) -> str:
        return self.label


def read_apple_music_batch(
    read_page: Callable[[AppleMusicPageCursor | None], AppleMusicLibraryResult],
    cursor: AppleMusicPageCursor | None = None,
) -> AppleMusicLibraryResult:
    """Show up to 150 items using Apple's bounded 100 + 50 page requests."""

    first_cursor = replace(cursor, _page_size=100) if cursor else None
    first = read_page(first_cursor)
    if first.next_cursor is None or len(first.records) >= 150:
        return first
    second_cursor = replace(
        first.next_cursor, _page_size=min(100, 150 - len(first.records))
    )
    second = read_page(second_cursor)
    return AppleMusicLibraryResult(
        heading=first.heading,
        records=(*first.records, *second.records),
        next_cursor=second.next_cursor,
    )


@dataclass(frozen=True, slots=True)
class AppleMusicLibraryMutation:
    """Confirmed membership result without provider identifiers."""

    added: bool
    item_kind: str


class AppleMusicLibraryClient:
    """Own one authenticated unofficial client without exposing it to wx."""

    def __init__(self, client: object) -> None:
        self._client = client
        self._storefront_code: str | None = None

    def _catalog_base(self) -> str:
        if self._storefront_code is None:
            try:
                raw = self._client.current_user_storefront()
                code = raw.get("id") if isinstance(raw, dict) else None
                if not isinstance(code, str) or not re.fullmatch(r"[a-z]{2}", code):
                    raise AppleMusicLibraryError()
                self._storefront_code = code
            except AppleMusicLibraryError:
                raise
            except Exception:
                raise AppleMusicLibraryError() from None
        return f"/v1/catalog/{self._storefront_code}"

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
        page_size: int = 100,
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
            effective_size = page_size
            params: dict[str, object] | None = {
                "l": "pl-PL",
                "limit": page_size,
            }
        else:
            if (
                not isinstance(cursor, AppleMusicPageCursor)
                or cursor.view != view
                or cursor._base_path != base_path
            ):
                raise AppleMusicLibraryError()
            effective_size = cursor._page_size
            path = _continuation_path(cursor, base_path)
            params = None
        try:
            getter = getattr(self._client, "_get")
            envelope = getter(path, params)
            if not isinstance(envelope, dict):
                raise AppleMusicLibraryError()
            raw = envelope.get("data")
            if not isinstance(raw, list):
                raise AppleMusicLibraryError()
            next_cursor = _page_cursor(
                view, envelope.get("next"), base_path, effective_size
            )
            return AppleMusicLibraryResult(
                heading=heading,
                records=records_from_resources(raw),
                next_cursor=next_cursor,
            )
        except AppleMusicLibraryError:
            raise
        except Exception:
            raise AppleMusicLibraryError() from None

    def search_library(self, query: str) -> tuple[AppleMusicRecord, ...]:
        """Search the account library, including items outside loaded pages."""

        term = query.strip() if isinstance(query, str) else ""
        if not term or len(term) > 256:
            raise AppleMusicLibraryError()
        types = (
            "library-songs",
            "library-albums",
            "library-artists",
            "library-playlists",
        )
        try:
            getter = getattr(self._client, "_get")
            envelope = getter(
                "/v1/me/library/search",
                {"term": term, "types": ",".join(types), "limit": 25, "l": "pl-PL"},
            )
            groups = envelope.get("results") if isinstance(envelope, dict) else None
            if not isinstance(groups, dict):
                raise AppleMusicLibraryError()
            found: list[AppleMusicRecord] = []
            seen: set[str] = set()
            for kind in types:
                group = groups.get(kind)
                if group is None:
                    continue
                if not isinstance(group, dict):
                    raise AppleMusicLibraryError()
                for record in records_from_resources(group.get("data")):
                    if record.row.item_id not in seen:
                        seen.add(record.row.item_id)
                        found.append(record)
            return tuple(found)
        except AppleMusicLibraryError:
            raise
        except Exception as error:
            raise _sanitized_search_error(error) from None

    def search_catalog(self, query: str) -> tuple[AppleMusicRecord, ...]:
        """Search navigable resources in the account's storefront."""

        term = query.strip() if isinstance(query, str) else ""
        if not term or len(term) > 256:
            raise AppleMusicLibraryError()
        types = ("artists", "albums", "playlists", "songs")
        try:
            groups = self._client.search(term, types=list(types), limit=25)
            if not isinstance(groups, dict):
                raise AppleMusicLibraryError()
            found: list[AppleMusicRecord] = []
            seen: set[str] = set()
            for kind in types:
                group = groups.get(kind)
                if group is None:
                    continue
                if not isinstance(group, dict):
                    raise AppleMusicLibraryError()
                for record in records_from_resources(group.get("data")):
                    if record.row.item_id not in seen:
                        seen.add(record.row.item_id)
                        found.append(record)
            return tuple(found)
        except AppleMusicLibraryError:
            raise
        except Exception as error:
            raise _sanitized_search_error(error) from None

    def resolve_catalog_artist(self, artist: AppleMusicRecord) -> AppleMusicRecord:
        """Resolve one library artist by exact public name without guessing."""

        if artist.target.resource_type == "artists":
            return artist
        if artist.target.resource_type != "library-artists":
            raise AppleMusicLibraryError()
        matches = tuple(
            record for record in self.search_catalog(artist.row.title)
            if record.target.resource_type == "artists"
            and record.row.title.casefold() == artist.row.title.casefold()
        )
        if len(matches) != 1:
            raise AppleMusicLibraryError()
        return matches[0]

    def artist_view_page(
        self,
        artist: AppleMusicRecord,
        view: str,
        *,
        cursor: AppleMusicPageCursor | None = None,
        page_size: int = 100,
    ) -> AppleMusicLibraryResult:
        """Read a named catalogue view of one artist with a bounded cursor."""

        if (
            artist.target.resource_type != "artists"
            or view not in _CATALOG_ARTIST_VIEWS
            or type(page_size) is not int
            or not 1 <= page_size <= 100
        ):
            raise AppleMusicLibraryError()
        base_path = (
            f"{self._catalog_base()}/artists/"
            + quote(artist.target.resource_id, safe="")
            + f"/view/{view}"
        )
        scope = f"artist-view:{artist.target.resource_id}:{view}"
        if cursor is None:
            path = base_path
            params: dict[str, object] | None = {"l": "pl-PL", "limit": page_size}
            effective_size = page_size
        else:
            if (
                not isinstance(cursor, AppleMusicPageCursor)
                or cursor.view != scope
                or cursor._base_path != base_path
            ):
                raise AppleMusicLibraryError()
            path = _continuation_path(cursor, base_path)
            params = None
            effective_size = cursor._page_size
        try:
            envelope = self._client._get(path, params)
            raw = envelope.get("data") if isinstance(envelope, dict) else None
            if not isinstance(raw, list):
                raise AppleMusicLibraryError()
            return AppleMusicLibraryResult(
                heading=f"{_CATALOG_ARTIST_VIEWS[view]}: {artist.row.title}",
                records=records_from_resources(raw),
                next_cursor=_page_cursor(
                    scope, envelope.get("next"), base_path, effective_size
                ),
            )
        except AppleMusicLibraryError:
            raise
        except Exception:
            raise AppleMusicLibraryError() from None

    def song_relations(self, song: AppleMusicRecord) -> tuple[AppleMusicRecord, ...]:
        """Resolve a song's album and artist to named, safe records."""

        catalog_id = song.target.catalog_id
        if song.row.kind != "track" or not catalog_id or not catalog_id.isdigit():
            return ()
        path = f"{self._catalog_base()}/songs/{quote(catalog_id, safe='')}"
        try:
            envelope = self._client._get(
                path, {"include": "albums,artists", "l": "pl-PL"}
            )
            data = envelope.get("data") if isinstance(envelope, dict) else None
            resource = data[0] if isinstance(data, list) and data else None
            relations = resource.get("relationships") if isinstance(resource, dict) else None
            if not isinstance(relations, dict):
                raise AppleMusicLibraryError()
            found: list[AppleMusicRecord] = []
            for kind in ("albums", "artists"):
                group = relations.get(kind)
                raw = group.get("data") if isinstance(group, dict) else None
                if isinstance(raw, list):
                    found.extend(records_from_resources(raw))
            return tuple(found)
        except AppleMusicLibraryError:
            raise
        except Exception:
            raise AppleMusicLibraryError() from None

    def playlist_tracks_page(
        self,
        playlist: AppleMusicRecord,
        *,
        cursor: AppleMusicPageCursor | None = None,
        page_size: int = 100,
    ) -> AppleMusicLibraryResult:
        """Read one bounded page tied to exactly one library playlist."""

        target = playlist.target
        if (
            target.resource_type not in {"library-playlists", "playlists"}
            or isinstance(page_size, bool)
            or not isinstance(page_size, int)
            or not 1 <= page_size <= 100
        ):
            raise AppleMusicLibraryError()
        prefix = (
            "/v1/me/library"
            if target.from_library else self._catalog_base()
        )
        base_path = (
            prefix + "/playlists/"
            + quote(target.resource_id, safe="")
            + "/tracks"
        )
        scope = "playlist-tracks:" + target.resource_id
        if cursor is None:
            path = base_path
            effective_size = page_size
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
            effective_size = cursor._page_size
            path = _continuation_path(cursor, base_path)
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
                next_cursor=_page_cursor(
                    scope, envelope.get("next"), base_path, effective_size
                ),
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
        page_size: int = 100,
    ) -> AppleMusicLibraryResult:
        """Read one page of a library album's tracks through its relationship."""

        target = album.target
        if (
            target.resource_type not in {"library-albums", "albums"}
            or isinstance(page_size, bool)
            or not isinstance(page_size, int)
            or not 1 <= page_size <= 100
        ):
            raise AppleMusicLibraryError()
        prefix = (
            "/v1/me/library"
            if target.from_library else self._catalog_base()
        )
        base_path = (
            prefix + "/albums/"
            + quote(target.resource_id, safe="")
            + "/tracks"
        )
        scope = "album-tracks:" + target.resource_id
        if cursor is None:
            path = base_path
            effective_size = page_size
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
            effective_size = cursor._page_size
            path = _continuation_path(cursor, base_path)
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
                next_cursor=_page_cursor(
                    scope, envelope.get("next"), base_path, effective_size
                ),
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
    page_size: int = 100,
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
    if (
        not isinstance(base_path, str)
        or not (
            base_path.startswith("/v1/me/library/")
            or re.match(r"/v1/catalog/[a-z]{2}/", base_path)
        )
    ):
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
    return AppleMusicPageCursor(
        view=view,
        _path=path,
        _base_path=base_path,
        _page_size=page_size,
    )


def _continuation_path(cursor: AppleMusicPageCursor, base_path: str) -> str:
    """Keep Apple's opaque offset while repeating the chosen page size."""

    if (
        type(cursor._page_size) is not int
        or not 1 <= cursor._page_size <= 100
        or _page_cursor(cursor.view, cursor._path, base_path) is None
    ):
        raise AppleMusicLibraryError()
    parsed = urlsplit(cursor._path)
    query = [
        (key, value)
        for key, value in parse_qsl(parsed.query, keep_blank_values=True)
        if key not in ("limit", "l")
    ]
    query.extend((("limit", str(cursor._page_size)), ("l", "pl-PL")))
    return base_path + "?" + urlencode(query)
