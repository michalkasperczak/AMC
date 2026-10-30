"""Read-only source aggregation for the wxPython search window."""

from __future__ import annotations

from dataclasses import dataclass

from .library_source import LibrarySource
from .list_model import rows_from_stations
from .navigation import LibraryView, SessionId
from .podcast_source import PodcastSource
from .radio_source import RadioSource, SCOPE_LIBRARY
from .search_catalog import SearchEntry

TIDAL_VIEW_LIBRARY = "library"
TIDAL_VIEW_FAVORITES = "favorites"
TIDAL_VIEW_PLAYLISTS = "playlists"


@dataclass(frozen=True, slots=True)
class SearchCollection:
    entries: tuple[SearchEntry, ...]
    warnings: tuple[str, ...] = ()


class SearchSources:
    """Build the searchable cached catalogue without modifying the profile."""

    def __init__(
        self,
        library: LibrarySource,
        radio: RadioSource,
        podcasts: PodcastSource,
        tidal: object | None,
        radioarchiwista: object | None = None,
    ) -> None:
        self.library = library
        self.radio = radio
        self.podcasts = podcasts
        self.tidal = tidal
        self.radioarchiwista = radioarchiwista

    def collect(self, session_ids: tuple[SessionId, ...]) -> SearchCollection:
        entries: list[SearchEntry] = []
        warnings: list[str] = []
        for session_id in session_ids:
            try:
                entries.extend(self._session_entries(session_id))
            except Exception:
                # One unavailable profile/service must not erase valid global
                # results from the other sessions.  The dialog receives only
                # a short, intentional label, never the exception repr.
                warnings.append(
                    f"Nie udało się odczytać zapisanych danych: "
                    f"{self._session_label(session_id)}"
                )
        return SearchCollection(tuple(entries), tuple(warnings))

    def _session_entries(self, session_id: SessionId) -> list[SearchEntry]:
        if session_id is SessionId.FILES:
            view = self.library.load_view("all_files")
            return [
                SearchEntry(SessionId.FILES, row, LibraryView.ALL_FILES)
                for row in view.rows
            ]
        if session_id is SessionId.RADIO:
            # Only durable Library entries can currently be selected again in
            # the wx browser.  Online Radio Browser results will be added by
            # the remote-search endpoint; cached one-off residue must not be
            # presented as an Enter target that immediately disappears.
            snapshot = self.radio.load(scope=SCOPE_LIBRARY)
            if snapshot.load_error:
                raise RuntimeError(snapshot.load_error)
            return [
                SearchEntry(SessionId.RADIO, row, None)
                for row in rows_from_stations(snapshot.as_payload())
            ]
        if session_id is SessionId.PODCASTS:
            return [
                SearchEntry(
                    SessionId.PODCASTS,
                    row,
                    LibraryView.PODCAST_LIBRARY
                    if row.kind == "podcast"
                    else LibraryView.PODCAST_EPISODES,
                    parent_id=row.parent_id,
                )
                for row in self.podcasts.search_rows()
            ]
        if session_id is SessionId.TIDAL:
            if self.tidal is None:
                return []
            results: list[SearchEntry] = []
            for source, target in (
                (TIDAL_VIEW_LIBRARY, LibraryView.TIDAL_LIBRARY),
                (TIDAL_VIEW_FAVORITES, LibraryView.TIDAL_FAVORITES),
                (TIDAL_VIEW_PLAYLISTS, LibraryView.TIDAL_PLAYLISTS),
            ):
                result = self.tidal.load_view(source)
                results.extend(
                    SearchEntry(SessionId.TIDAL, row, target)
                    for row in result.rows
                )
            return results
        if session_id is SessionId.RADIOARCHIWISTA:
            # Stacje z ważnego cache i ulubione nagrania z profilu;
            # globalne wyszukiwanie nie schodzi do sieci (źródło gwarantuje
            # to w ``search_rows``).
            if self.radioarchiwista is None:
                return []
            rows = self.radioarchiwista.search_rows()
            return [
                SearchEntry(
                    SessionId.RADIOARCHIWISTA,
                    row,
                    LibraryView.RADIOARCHIWISTA_FAVORITES
                    if row.kind == "recording"
                    else LibraryView.RADIOARCHIWISTA_STATIONS,
                )
                for row in rows
            ]
        # WiiM i Sonos nie udają globalnego katalogu. Ich rzeczywiście
        # wczytane listy są migawkami stanu nawigatora i dokłada je warstwa
        # GUI przez wspólny kontrakt ``speaker_search_entries``.
        return []

    @staticmethod
    def _session_label(session_id: SessionId) -> str:
        return {
            SessionId.FILES: "Pliki lokalne",
            SessionId.RADIO: "Radio",
            SessionId.PODCASTS: "Podcasty i YouTube",
            SessionId.TIDAL: "TIDAL",
            SessionId.WIIM: "WiiM",
            SessionId.SONOS: "Sonos",
            SessionId.FRONTIER_SMART: "Frontier Smart",
            SessionId.SPOTIFY: "Spotify",
            SessionId.RADIOARCHIWISTA: "Radio Archiwista",
        }[session_id]
