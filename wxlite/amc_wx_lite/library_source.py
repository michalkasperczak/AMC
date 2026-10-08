"""Zrodlo wierszy Biblioteki dla okna wxPython.

Rozdziela dwie rzeczy, ktore wczesniej byly zlepione w jedna:

* **Biblioteka AMC** -- rekordy z ``library.db``. Dziala tez wtedy, gdy
  sciezek nie ma na tej maszynie (``D:\\``, ``C:\\Users\\micha``). To ona
  stoi pod Ctrl+1.
* **Przegladanie dysku** -- ``files.listFolder`` w hoscie. Przydatne przy
  Ctrl+O, ale NIE jest Biblioteka.

Caly odczyt jest synchroniczny i tani (SQLite), ale okno i tak wola go przez
``runner.submit``, zeby wczytanie 11 tysiecy rekordow nie zatrzymalo GUI.
"""

from __future__ import annotations

from dataclasses import dataclass
from pathlib import Path
from typing import TYPE_CHECKING

from .collation import HostCollation, HostCollationUnavailable, order_library_rows
from .library_db import LibraryDatabase, LibraryItem, breadcrumb_rows, folder_rows
from .list_model import Row
from .profile_layout import ProfileLayout, resolve_layout

if TYPE_CHECKING:  # tylko do adnotacji -- import w metodzie, zeby nie robic kola
    from .library_views import LibraryViewResult


@dataclass(slots=True)
class LibrarySnapshot:
    """Wiersze + opis naglowka dla jednego poziomu Biblioteki."""

    rows: list[Row]
    heading: str
    folder_path: str | None
    total_active: int
    #: Czy kolejnosc wierszy jest zgodna z oryginalnym C#. ``False`` znaczy
    #: "host nie podal kluczy AMC_PL, kolejnosc jest zastepcza" -- i okno moze
    #: to powiedziec, zamiast milczeniem udawac zgodnosc.
    order_matches_amc: bool = False
    #: Czy odczyt sledzil zywy WAL. ``False`` = zamrozona migawka, wiec okno
    #: MUSI to powiedziec, a nie udawac swiezych danych.
    sees_live_writes: bool = True

    @property
    def is_empty(self) -> bool:
        return not self.rows


def degradation_notice(snapshot: LibrarySnapshot) -> str:
    """Krotki, PRAWDZIWY komunikat o degradacji odczytu. Puste = wszystko OK.

    Istnieje, bo ``order_matches_amc`` i ``sees_live_writes`` nie byly nigdzie
    czytane: uzytkownik dostawal zastepcza kolejnosc albo stary obraz profilu
    w calkowitej ciszy.
    """
    problems: list[str] = []
    if not snapshot.sees_live_writes:
        problems.append("zamrozona migawka profilu, bez zywych zmian AMC")
    if not snapshot.order_matches_amc:
        problems.append("kolejnosc zastepcza, niezgodna z AMC")
    if not problems:
        return ""
    return "Uwaga: " + "; ".join(problems) + "."


class LibrarySource:
    """Czyta Biblioteke AMC na zadanie. Nie trzyma uchwytu miedzy odczytami.

    Uchwyt otwieramy na KAZDY odczyt, zeby zobaczyc to, co host C# wlasnie
    zapisal. Polaczenie idzie w trybie ``mode=ro`` (bez ``immutable=1``), bo
    ``immutable`` kazal SQLite pominac ``-wal`` i oddawal stan sprzed commitu.

    Kolejnosc wierszy ustala ``HostCollation`` -- klucze kolacji AMC_PL
    policzone oryginalnym ``CompareInfo`` w hoscie. Bez hosta zostaje kolejnosc
    z SQL (niezgodna) i wtedy ``LibrarySnapshot.order_matches_amc`` jest
    ``False``.
    """

    def __init__(
        self,
        layout: ProfileLayout | None = None,
        *,
        collation: HostCollation | None = None,
    ) -> None:
        self.layout = layout or resolve_layout()
        self._collation = collation

    @property
    def database_path(self) -> Path:
        return self.layout.library_db

    @property
    def is_available(self) -> bool:
        return self.database_path.exists()

    def _open(self) -> LibraryDatabase:
        return LibraryDatabase(self.database_path)

    def saved_folder(self) -> str | None:
        """Folder zapamietany przez AMC (``local_state.current_folder_path``)."""
        with self._open() as db:
            return db.local_state().current_folder_path

    def saved_library_view(self) -> str:
        """Zapamietany widok Biblioteki (``local_state.library_view``).

        Odpowiednik ``_state.LocalMedia.LibraryView`` (``AppSettings.cs:1236``),
        ktory ``MainWindow.xaml.cs:704`` podstawia za nazwe "Biblioteka" pod
        Ctrl+L. Zwraca NAZWE widoku z profilu, nie ``LibraryView``.
        """
        with self._open() as db:
            return db.local_state().library_view

    def recorded_radio_items(self) -> list[LibraryItem]:
        """Pliki nagran radia, takze dostepne poza zwykla Biblioteka."""
        with self._open() as db:
            return db.recorded_radio_items()

    def use_collation(self, collation: "HostCollation | None") -> None:
        """Podepnij kolejnosc liczona przez host C#.

        Wolane po starcie silnika: wczesniej nie ma z kim rozmawiac.
        """
        self._collation = collation

    def load(self, folder: str | None) -> LibrarySnapshot:
        with self._open() as db:
            rows = folder_rows(db, folder)
            if folder:
                rows = breadcrumb_rows(db, folder) + rows
                heading = f"Biblioteka — Foldery — {_leaf(folder)}"
            else:
                heading = "Biblioteka — Foldery"

            # Kolejnosc zgodna z C# liczymy PO odczycie: SQL moze uzyc tylko
            # tej kolacji, ktora da sie zarejestrowac w SQLite, a zgodny
            # komparator pl-PL siedzi w hoscie.
            ordered, matches = self._apply_amc_order(rows)
            return LibrarySnapshot(
                rows=ordered,
                heading=heading,
                folder_path=folder,
                total_active=db.count_active_items(),
                order_matches_amc=matches,
                sees_live_writes=db.sees_live_writes,
            )

    def _apply_amc_order(self, rows: list[Row]) -> tuple[list[Row], bool]:
        """Kolejnosc C#, jesli host odpowie; inaczej wejscie i uczciwe ``False``.

        Awaria kolacji NIE moze przewrocic Biblioteki: lepiej pokazac liste
        w kolejnosci zastepczej i oznaczyc ja, niz nie pokazac nic.
        """
        if self._collation is None:
            return rows, False
        try:
            return order_library_rows(rows, self._collation), True
        except HostCollationUnavailable:
            return rows, False

    def load_view(
        self,
        view: str,
        *,
        playlist_id: str | None = None,
        item_id: str | None = None,
        current_session_id: str = "",
        current_item_id: str = "",
    ) -> "LibraryViewResult":
        """Jeden z nazwanych widokow Biblioteki.

        Osobno od ``load``, bo tamta czyta DRZEWO folderow, a te widoki sa
        plaskie i maja wlasne naglowki. Uchwyt otwieramy tak samo na kazdy
        odczyt -- inaczej nie zobaczylibysmy tego, co host wlasnie zapisal.

        ``current_session_id``/``current_item_id`` dotycza TYLKO widoku
        zbiorczego zakladek i sa JAWNYM argumentem, tak jak w WPF
        (``MainWindow.xaml.cs:12530-12532``). Pusty kontekst jest legalny: sesja
        moze nic nie odtwarzac.
        """
        from . import library_activity, library_views

        with self._open() as db:
            if view == "all_files":
                return library_views.all_files_rows(db, collation=self._collation)
            if view == "favorites":
                return library_views.favorite_rows(db)
            if view == "playlists":
                return library_views.playlist_rows(db)
            if view == "playlist_contents":
                if not playlist_id:
                    raise ValueError("playlist_contents wymaga playlist_id")
                return library_views.playlist_contents_rows(db, playlist_id)
            # Widoki AKTYWNOSCI. Dane liczy odebrany ``library_activity``;
            # tutaj tylko sprowadzamy jego ``ActivityResult`` do typu, ktory
            # okno juz umie wyswietlic.
            if view == "history":
                return _from_activity(library_activity.history_rows(db))
            if view == "saved_queue":
                return _from_activity(library_activity.saved_queue_rows(db))
            if view == "item_bookmarks":
                if not item_id:
                    # ``GetForItem`` bez Id nie istnieje. Cicha pusta lista
                    # wygladalaby jak "ten plik nie ma zakladek" i skasowalaby
                    # blad wywolania.
                    raise ValueError("item_bookmarks wymaga item_id")
                activity = library_activity.bookmark_rows(db, item_id=item_id)
                return _from_activity(activity, targets=_bookmark_targets(db, activity))
            if view == "all_bookmarks":
                # ``GetForDisplay``. Kolejnosc i flagi liczy odebrana warstwa;
                # tutaj dochodzi wylacznie to, czego ona celowo nie robi:
                # zamiana lokalnego ``item_id`` na SCIEZKE pliku.
                activity = library_activity.all_bookmark_rows(
                    db,
                    current_session_id=current_session_id,
                    current_item_id=current_item_id,
                    collation=self._collation,
                )
                return _from_activity(
                    activity,
                    targets=_local_bookmark_targets(db, activity),
                    contexts=_bookmark_contexts(activity),
                )
        raise ValueError(f"nieznany widok Biblioteki: {view}")

    def describe(self) -> str:
        """Komunikat dla czytnika ekranu, gdy Biblioteki nie ma."""
        if self.is_available:
            return ""
        return (
            f"Nie znalazlem Biblioteki AMC ({self.database_path}). "
            "Uruchom AMC albo wskaz folder: Ctrl+O."
        )


def _leaf(path: str) -> str:
    import ntpath

    norm = ntpath.normpath(path).rstrip("\\/")
    return ntpath.basename(norm) or norm


#: Tickow na sekunde w .NET. ``BookmarkIndex`` trzyma pozycje w tickach.
_TICKS_PER_SECOND = 10_000_000


def _from_activity(activity, *, targets=None, contexts=None) -> "LibraryViewResult":
    """``ActivityResult`` -> ``LibraryViewResult``, bez drugiego frameworka.

    Okno umie wyswietlic jeden typ wyniku. Zamiast uczyc je drugiego,
    przepisujemy pola, ktore sa wspolne, i dokladamy mape celow zakladek.
    """
    from .library_views import LibraryViewResult

    return LibraryViewResult(
        rows=list(activity.rows),
        heading=activity.heading,
        order_matches_amc=activity.order_matches_amc,
        sees_live_writes=activity.sees_live_writes,
        bookmark_targets=dict(targets or {}),
        bookmark_contexts=dict(contexts or {}),
        # Flagi kolejki ida DALEJ, a nie gina tutaj: ``QueueRow`` juz je zna,
        # a host potrzebuje ich w ``queue.set``, zeby odroznic zwykla kolejke
        # od bloku "odtworz nastepne".
        queue_flags={
            entry.row.item_id: (entry.is_in_queue, entry.is_play_next)
            for entry in getattr(activity, "queue", ())
        },
    )


def _bookmark_contexts(activity) -> dict:
    """``display`` z warstwy danych -> kontekst per wiersz, bez przeliczania.

    ``is_current_item`` i ``can_play_locally`` policzyla warstwa danych wedle
    regul C#. Tutaj ich NIE liczymy od nowa -- druga kopia tej samej reguly
    rozjechalaby sie po pierwszej poprawce. Przepisujemy tylko to, co jest, i
    kluczujemy po ``Row.item_id``, bo tym kluczem dysponuje lista.
    """
    from .library_views import BookmarkContext

    display = getattr(activity, "display", ())
    return {
        entry.row.item_id: BookmarkContext(
            item_id=entry.bookmark.item_id,
            session_id=entry.bookmark.session_id,
            session_name=entry.bookmark.session_name,
            can_play_locally=entry.can_play_locally,
            is_current_item=entry.is_current_item,
            position_seconds=entry.position_seconds,
        )
        for entry in display
    }


def _local_bookmark_targets(db, activity) -> dict[str, tuple[str, float, str]]:
    """Cele skoku TYLKO dla zakladek z kanalem lokalnym.

    Widok zbiorczy ma zakladki wszystkich sesji, a ``local_items`` opisuje
    wylacznie material lokalny. Dwie sesje moga uzywac tego samego ``item_id``
    (zmierzone w prawdziwym profilu), wiec zapytanie o Id bez filtra po sesji
    dalo by zakladce Spotify sciezke lokalnego pliku. Filtrujemy po
    ``can_play_locally``, czyli po tym samym kryterium, ktore rozstrzyga o
    odmowie -- jedno zrodlo prawdy, nie dwa.
    """
    display = getattr(activity, "display", ())
    local = [entry for entry in display if entry.can_play_locally]
    if not local:
        return {}
    wanted = {entry.bookmark.item_id for entry in local}
    placeholders = ",".join("?" for _ in wanted)
    found = {
        str(row["id"]): (row["path"], row["title"])
        for row in db.connection.execute(
            f"SELECT id, path, title FROM local_items WHERE id IN ({placeholders})",
            tuple(wanted),
        )
        if row["path"]
    }
    targets: dict[str, tuple[str, float, str]] = {}
    for entry in local:
        target = found.get(entry.bookmark.item_id)
        if target is None:
            # Zakladki NIE maja filtra ``ActiveLocalItems``: material moze w
            # katalogu nie istniec. Wtedy celu po prostu nie ma -- i nie
            # wysylamy do hosta zmyslonej sciezki.
            continue
        path, title = target
        targets[entry.row.item_id] = (
            path,
            entry.position_seconds,
            title or entry.bookmark.item_title,
        )
    return targets


def _bookmark_targets(db, activity) -> dict[str, tuple[str, float, str]]:
    """Cel skoku dla kazdej zakladki: PLIK i pozycja w sekundach.

    ``BookmarkRow.item_id`` jest Id PLIKU, a ``BookmarkRow.row.item_id`` ma
    prefiks ``bookmark:`` -- do backendu musi pojsc to pierwsze, zamienione
    jeszcze na sciezke. Zakladki NIE maja filtra ``ActiveLocalItems``, wiec
    plik moze w katalogu nie istniec; wtedy celu po prostu nie ma i wiersz
    zostaje nieaktywny, zamiast wyslac do hosta zmyslona sciezke.
    """
    marks = getattr(activity, "bookmarks", ())
    if not marks:
        return {}
    wanted = {mark.item_id for mark in marks}
    placeholders = ",".join("?" for _ in wanted)
    found = {
        str(row["id"]): (row["path"], row["title"])
        for row in db.connection.execute(
            f"SELECT id, path, title FROM local_items WHERE id IN ({placeholders})",
            tuple(wanted),
        )
        if row["path"]
    }
    targets: dict[str, tuple[str, float, str]] = {}
    for mark in marks:
        target = found.get(mark.item_id)
        if target is None:
            continue
        path, title = target
        # Dzielenie ZWYKLE, nie calkowite: zakladka 83,456 s nie moze
        # wrocic jako 83 s.
        targets[mark.row.item_id] = (
            path,
            mark.position_ticks / _TICKS_PER_SECOND,
            title or mark.item_title,
        )
    return targets
