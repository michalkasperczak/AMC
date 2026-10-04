"""Warstwa DANYCH kolejnych widokow lokalnej Biblioteki (tylko odczyt).

Reguly filtrow, kolejnosci i semantyki pozycji niedostepnych sa PRZEPISANE
z aktualnego kodu AMC (C#), nie zgadniete z nazw tabel. Cytaty i numery
wierszy: ``wxlite/LIBRARY_VIEWS_CONTRACT.md``.

Modul celowo NIE dotyka ``library_db.py`` ani ``library_source.py`` -- oba sa
w uzyciu drugiego autora. Rozszerzamy istniejace polaczenie read-only przez
wlasne zapytania na gotowym ``LibraryDatabase``.
"""

from __future__ import annotations

from dataclasses import dataclass, field
from typing import Sequence

from .collation import COLLATION_ORDINAL_IGNORE_CASE, COLLATION_TITLE_IGNORE_CASE
from .library_db import AMC_PL, LibraryDatabase, LibraryItem, _ACTIVE, _format_detail
from .list_model import Row

#: Prefiks nazwy widoku zawartosci playlisty.
#: ``MainWindow.xaml.cs:67`` -- ``PlaylistContentsViewPrefix = "Playlista:"``.
PLAYLIST_VIEW_PREFIX = "Playlista:"

#: Widok, do ktorego AMC wraca, gdy playlista zniknela.
#: ``MainWindow.xaml.cs:12468``.
PLAYLISTS_VIEW = "Playlisty"

#: Sesja lokalna. Playlisty i Ulubione sa trzymane per sesja.
LOCAL_SESSION = "local"

_ITEM_COLUMNS = (
    "id, title, path, duration_ticks, is_favorite, is_available, "
    "is_in_library, is_radio_recording"
)


def view_name_for_playlist(playlist_id: str) -> str:
    """Nazwa widoku zawartosci playlisty, tak jak sklada ja AMC."""
    return f"{PLAYLIST_VIEW_PREFIX}{playlist_id}"


def playlist_id_from_view(view_name: str) -> str | None:
    """Port ``TryGetPlaylistIdFromView`` (``MainWindow.xaml.cs:20057-20067``).

    ``StartsWith(prefix, Ordinal)`` ORAZ dlugosc wieksza od prefiksu -- sam
    ``"Playlista:"`` bez identyfikatora NIE jest widokiem playlisty.
    """
    if view_name.startswith(PLAYLIST_VIEW_PREFIX) and len(view_name) > len(
        PLAYLIST_VIEW_PREFIX
    ):
        return view_name[len(PLAYLIST_VIEW_PREFIX) :]
    return None


@dataclass(frozen=True, slots=True)
class PlaylistRow:
    """Metadane jednej playlisty OBOK ``Row``, bez zmiany jego konstruktora.

    ``row`` jest zwyklym ``Row`` (``kind="playlist"``), wiec wchodzi do
    istniejacego ``ListModel`` bez przerobek. Reszta pol to dane, ktorych
    ``Row`` nie ma, a ktore GUI potrzebuje do powrotu i wyboru po Id.
    """

    row: Row
    playlist_id: str
    name: str
    stored_count: int
    available_count: int
    duration_ticks: int


@dataclass(frozen=True, slots=True)
class LibraryViewResult:
    """Wynik jednego widoku: wiersze + uczciwy opis degradacji.

    ``order_matches_amc=False`` znaczy dokladnie to samo, co w
    ``LibrarySnapshot``: kolejnosc jest ZASTEPCZA, bo host nie podal kluczy
    AMC_PL. Nie udajemy zgodnosci 1:1 bez hosta.
    """

    rows: list[Row]
    heading: str
    order_matches_amc: bool = True
    sees_live_writes: bool = True
    #: Wypelnione tylko dla widoku "Playlisty".
    playlists: tuple[PlaylistRow, ...] = ()
    #: Ustawiane, gdy AMC wrocilby do innego widoku (znikla playlista).
    fallback_view: str | None = None
    #: Tylko dla widoku zakladek: ``Row.item_id`` -> (sciezka PLIKU, sekundy,
    #: tytul pliku). ``Row.item_id`` zakladki ma postac ``bookmark:<id>`` i
    #: NIE jest identyfikatorem pliku, wiec backend nie moze go dostac.
    #: Pozycja jest ULAMKOWA: ``position_ticks / 10_000_000`` bez obcinania.
    bookmark_targets: dict[str, tuple[str, float, str]] = field(default_factory=dict)

    @property
    def is_empty(self) -> bool:
        return not self.rows


def _item(row) -> LibraryItem:
    return LibraryItem(
        id=str(row["id"]),  # ID zostaje NAPISEM, nawet gdy wyglada na liczbe
        title=row["title"],
        path=row["path"],
        duration_ticks=int(row["duration_ticks"]),
        is_favorite=bool(row["is_favorite"]),
        is_available=bool(row["is_available"]),
        is_in_library=bool(row["is_in_library"]),
        is_radio_recording=bool(row["is_radio_recording"]),
    )


def _track_row(item: LibraryItem) -> Row:
    return Row(
        item_id=item.id,
        title=item.title,
        kind="track",
        path=item.path,
        detail=_format_detail(item),
    )


def active_items(db: LibraryDatabase) -> list[LibraryItem]:
    """Port ``ActiveLocalItems()`` (``MainWindow.xaml.cs:10004-10005``).

    ``IsAvailable && IsInLibrary``. Dodatkowo odtwarzamy
    ``DemoMediaSession.ReplaceItems`` (``DemoMediaSession.cs:463-465``):
    ``DistinctBy(Id, Ordinal)`` -- bo Ulubione i playlisty czytaja wlasnie
    ten katalog sesji, a nie surowa tabele.
    """
    rows = db.connection.execute(
        f"SELECT {_ITEM_COLUMNS} FROM local_items WHERE {_ACTIVE} ORDER BY rowid"
    )
    seen: set[str] = set()
    items: list[LibraryItem] = []
    for row in rows:
        item = _item(row)
        if item.id in seen:
            continue
        seen.add(item.id)
        items.append(item)
    return items


def all_files_rows(
    db: LibraryDatabase, collation=None
) -> LibraryViewResult:
    """Widok "Wszystkie pliki" (``MainWindow.xaml.cs:12415-12426``).

    ``OrderBy(Title, CurrentCultureIgnoreCase).ThenBy(Source ?? "",
    OrdinalIgnoreCase)``. Dla rekordu lokalnego ``Source`` to sciezka pliku
    (``MainWindow.xaml.cs:7619``).

    Alfabetyke daje ``HostCollation`` (klucze AMC_PL z oryginalnego
    ``CompareInfo``). Bez hosta kolejnosc jest zastepcza i mowimy o tym
    wprost przez ``order_matches_amc=False`` -- zadnego cichego udawania.
    """
    items = active_items(db)
    heading = "Biblioteka — Wszystkie pliki"
    if collation is None:
        return LibraryViewResult(
            rows=[_track_row(i) for i in items],
            heading=heading,
            order_matches_amc=False,
            sees_live_writes=db.sees_live_writes,
        )

    ordered = _order_by_title_then_source(items, collation)
    return LibraryViewResult(
        rows=[_track_row(i) for i in ordered],
        heading=heading,
        order_matches_amc=True,
        sees_live_writes=db.sees_live_writes,
    )


def _order_by_title_then_source(
    items: Sequence[LibraryItem], collation
) -> list[LibraryItem]:
    """Dwa klucze C#, OBA liczone przez host, kazdy we WLASCIWYM trybie.

    ``OrderBy(Title, StringComparer.CurrentCultureIgnoreCase)
    .ThenBy(Source ?? "", StringComparer.OrdinalIgnoreCase)``.

    Klucze bierzemy z hosta PARTIAMI (``collation.load``), po jednej partii na
    tryb, a nie porownywarka IPC na pare -- to byloby 2475*log(2475) przejsc
    przez most.

    Dlaczego NIE ``AMC_PL`` dla tytulu
    ----------------------------------
    ``AMC_PL`` to ``IgnoreCase | IgnoreNonSpace``, a widok uzywa
    ``CurrentCultureIgnoreCase``, czyli ``IgnoreCase`` SAMO. ``IgnoreNonSpace``
    ignoruje akcenty, wiec ``"e"`` i ``"é"`` wychodza RowNE i kolejnosc miedzy
    nimi zostaje losowa (wejsciowa). Zmierzone na .NET 8.0.31 pl-PL: wzorzec to
    ``plain, accent``, a ``AMC_PL`` oddawalo ``accent, plain``.
    Foldery i indeks SQL zostaja na ``AMC_PL`` -- tam ``IgnoreNonSpace`` jest
    zamierzone (p. ``order_library_rows``).

    Dlaczego NIE ``str.upper()`` dla sciezki
    ----------------------------------------
    ``str.upper()`` w Pythonie robi PELNE mapowanie jezykowe i rozwija ``"ß"``
    do ``"SS"``. ``OrdinalIgnoreCase`` tego nie robi -- porownuje skalary, a
    ``U+00DF`` (223) jest ZA ``"S"`` (83). Po ``str.upper()`` klucze ``"C:\\ß"``
    i ``"C:\\ss"`` byly IDENTYCZNE i tie-break przestawal istniec. Host oddaje
    teraz ``Rune.ToUpperInvariant`` skalar po skalarze w UTF-8 (0 niezgodnych
    par z oryginalnym ``StringComparer.OrdinalIgnoreCase``, kwit
    ``collection-sort-after422/probe-order-modes``).
    """
    titles = [i.title for i in items]
    paths = [i.path or "" for i in items]

    collation.load(titles, mode=COLLATION_TITLE_IGNORE_CASE)
    collation.load(paths, mode=COLLATION_ORDINAL_IGNORE_CASE)

    def key_of(value: str, mode: str) -> bytes:
        key = collation.key_for(value, mode=mode)
        if key is None:
            # Brak choc jednego klucza to blad: czesc listy ulozylaby sie
            # zgodnie z C#, a czesc nie, i nikt by tego nie zauwazyl.
            raise ValueError(f"Host nie oddal klucza {mode} dla {value!r}")
        return key

    return sorted(
        items,
        key=lambda i: (
            key_of(i.title, COLLATION_TITLE_IGNORE_CASE),
            key_of(i.path or "", COLLATION_ORDINAL_IGNORE_CASE),
        ),
    )


# ------------------------------------------------------------------- Ulubione


def _stored_order(db: LibraryDatabase, table: str, session: str) -> list[str]:
    """Zapisana kolejnosc kolekcji, ``ORDER BY ordinal``.

    ``LocalLibraryDatabase.cs:234`` czyta ``favorite_order``
    ``ORDER BY session_id, ordinal``; ``favorite_added_order`` analogicznie
    (230-231). Nazwa tabeli jest z zamknietego zbioru, nie z wejscia.
    """
    if table not in ("favorite_order", "favorite_added_order"):
        raise ValueError(f"Nieznana tabela kolejnosci: {table}")
    rows = db.connection.execute(
        f"SELECT item_id FROM {table} WHERE session_id = ? ORDER BY ordinal",
        (session,),
    )
    return [str(row[0]) for row in rows]


def _manual_order(
    items: Sequence[LibraryItem], stored: Sequence[str]
) -> list[LibraryItem]:
    """Port ``LocalLibraryManualOrder.Order`` (``LocalLibraryManualOrder.cs:89-105``).

    Pozycja = indeks PIERWSZEGO wystapienia Id w zapisie; Id nieznane zapisowi
    dostaje ``int.MaxValue``; remis rozstrzyga indeks wejsciowy. Zapisane Id,
    ktorych nie ma w katalogu, NIE tworza wierszy -- ``Order`` przebiega po
    ``items``, nie po ``storedOrder``.
    """
    first: dict[str, int] = {}
    for index, item_id in enumerate(stored):
        first.setdefault(item_id, index)
    sentinel = len(stored) + 1
    return [
        item
        for _, _, item in sorted(
            (
                (first.get(item.id, sentinel), original, item)
                for original, item in enumerate(items)
            ),
            key=lambda triple: (triple[0], triple[1]),
        )
    ]


def favorite_rows(
    db: LibraryDatabase,
    *,
    session: str = LOCAL_SESSION,
    order: str = "added_newest",
) -> LibraryViewResult:
    """Widok "Ulubione" (``MainWindow.xaml.cs:12524-12534``).

    Filtr dla sesji lokalnej: ``IsFavorite`` nad katalogiem sesji, a ten jest
    juz zawezony do aktywnych (p. 0 kontraktu). ``UsesTidalStyleCollections``
    nie dotyczy ``local``, wiec zadnego filtra po ``Kind``.

    Kolejnosc z ``OrderCurrentCollection`` (12978-13001). Domyslny tryb to
    ``AddedNewest`` -- ``Order(favorite_added_order)`` i ``.Reverse()``. Tryb
    ``custom`` (Alt+3) to ``favorite_order`` BEZ odwracania.

    Nie ma tu sortu alfabetycznego, wiec klucze hosta nie sa potrzebne i
    ``order_matches_amc`` zostaje ``True``.
    """
    favorites = [i for i in active_items(db) if i.is_favorite]
    if order == "custom":
        ordered = _manual_order(favorites, _stored_order(db, "favorite_order", session))
    elif order == "added_newest":
        ordered = _manual_order(
            favorites, _stored_order(db, "favorite_added_order", session)
        )
        ordered.reverse()
    else:
        raise ValueError(f"Nieznana kolejnosc Ulubionych: {order}")
    return LibraryViewResult(
        rows=[_track_row(i) for i in ordered],
        heading="Ulubione",
        order_matches_amc=True,
        sees_live_writes=db.sees_live_writes,
    )


# ------------------------------------------------------------------ playlisty


@dataclass(frozen=True, slots=True)
class _PlaylistEntry:
    playlist_id: str
    session_id: str
    name: str
    item_ids: tuple[str, ...]


def _playlists(db: LibraryDatabase, session: str | None) -> list[_PlaylistEntry]:
    """Wpisy playlist w kolejnosci zapisu.

    ``LocalLibraryDatabase.cs:286``:
    ``SELECT id, session_id, name, created_utc_ticks FROM playlists
    ORDER BY session_id, ordinal``. ``PlaylistIndex.GetForSession``
    (``PlaylistIndex.cs:22-25``) filtruje potem po ``SessionId``
    ``OrdinalIgnoreCase``. NIE ma tu sortu po nazwie.
    """
    items: dict[str, list[str]] = {}
    for row in db.connection.execute(
        "SELECT playlist_id, item_id FROM playlist_items ORDER BY playlist_id, ordinal"
    ):
        items.setdefault(str(row[0]), []).append(str(row[1]))
    entries: list[_PlaylistEntry] = []
    for row in db.connection.execute(
        "SELECT id, session_id, name FROM playlists ORDER BY session_id, ordinal"
    ):
        playlist_id = str(row[0])
        session_id = str(row[1])
        if session is not None and session_id.casefold() != session.casefold():
            continue
        entries.append(
            _PlaylistEntry(
                playlist_id=playlist_id,
                session_id=session_id,
                name=row[2],
                item_ids=tuple(items.get(playlist_id, ())),
            )
        )
    return entries


def _format_item_count(count: int) -> str:
    """``PlaylistPresentation.FormatItemCount`` (``PlaylistPresentation.cs:68-76``)."""
    if count == 1:
        return "1 element"
    last_two = count % 100
    last = count % 10
    if 2 <= last <= 4 and not 12 <= last_two <= 14:
        return f"{count} elementy"
    return f"{count} elementów"


def _format_duration_words(ticks: int) -> str:
    """``FormatDurationWords`` (``PlaylistPresentation.cs:63-66``)."""
    total_seconds = ticks // 10_000_000
    hours, rest = divmod(total_seconds, 3600)
    minutes, seconds = divmod(rest, 60)
    if hours >= 1:
        return f"{hours} godz. {minutes} min"
    return f"{minutes} min {seconds} s"


def _build_label(
    name: str,
    stored_count: int,
    available_items: Sequence[LibraryItem],
) -> str:
    """Port ``PlaylistPresentation.BuildLabel`` (``PlaylistPresentation.cs:17-49``).

    Tu i tylko tu mieszka semantyka pozycji NIEDOSTEPNYCH. Dla sesji lokalnej
    nie ma ``MediaItemKind.Station``, wiec galaz "transmisje na zywo" nie
    moze sie zapalic -- zostawiamy ja mimo to, zeby kontrakt byl kompletny.
    """
    available_count = len(available_items)
    availability = (
        _format_item_count(stored_count)
        if available_count == stored_count
        else f"dostępne {available_count} z {stored_count}"
    )
    if stored_count == 0:
        return f"{name}, {availability}"
    finite = list(available_items)  # brak stacji w sesji lokalnej
    if not finite and available_count > 0:
        return f"{name}, {availability}, transmisje na żywo"
    known = [i for i in finite if i.duration_ticks > 0]
    if not known:
        return f"{name}, {availability}, łączny czas nieznany"
    label = _format_duration_words(_saturating_duration(known))
    if len(known) == len(finite):
        return f"{name}, {availability}, łączny czas {label}"
    return f"{name}, {availability}, znany czas {label}, część bez danych"


_LONG_MAX = 2**63 - 1


def _saturating_duration(items: Sequence[LibraryItem]) -> int:
    """Port sumy z ``CreatePlaylistRows`` (12813-12819).

    C#: ``Aggregate(0L, (total, item) => item.Duration.Ticks >
    long.MaxValue - total ? long.MaxValue : total + item.Duration.Ticks)``.
    Python liczy bez ograniczenia szerokosci, wiec nasycenie trzeba napisac
    jawnie -- inaczej oddalibysmy wartosc, ktorej oryginal nigdy nie zwroci.

    Stacje sa pomijane; w sesji lokalnej ich nie ma, ale warunek zostaje,
    zeby port byl wierny.
    """
    total = 0
    for item in items:
        ticks = item.duration_ticks
        if ticks > _LONG_MAX - total:
            return _LONG_MAX
        total += ticks
    return total


def playlist_rows(
    db: LibraryDatabase, *, session: str = LOCAL_SESSION
) -> LibraryViewResult:
    """Widok "Playlisty" -- port ``CreatePlaylistRows`` (12800-12839).

    Id wiersza to ``"playlist:<id>"`` (12822). Czas liczymy z pozycji
    DOSTEPNYCH, pomijajac stacje (12813-12819). Etykieta: ``BuildLabel``
    z ``playlist.ItemIds.Count`` jako liczba zapisana i liczba dostepnych
    osobno -- stad jawne "dostępne X z Y".
    """
    by_id = {i.id: i for i in active_items(db)}
    rows: list[Row] = []
    entries: list[PlaylistRow] = []
    for entry in _playlists(db, session):
        available = [by_id[i] for i in entry.item_ids if i in by_id]
        duration_ticks = _saturating_duration(available)
        label = _build_label(entry.name, len(entry.item_ids), available)
        detail = label[len(entry.name) + 2 :] if label.startswith(entry.name + ", ") else label
        row = Row(
            item_id=f"playlist:{entry.playlist_id}",
            title=entry.name,
            kind="playlist",
            detail=detail,
        )
        rows.append(row)
        entries.append(
            PlaylistRow(
                row=row,
                playlist_id=entry.playlist_id,
                name=entry.name,
                stored_count=len(entry.item_ids),
                available_count=len(available),
                duration_ticks=duration_ticks,
            )
        )
    return LibraryViewResult(
        rows=rows,
        heading=PLAYLISTS_VIEW,
        order_matches_amc=True,
        sees_live_writes=db.sees_live_writes,
        playlists=tuple(entries),
    )


def playlist_contents_rows(
    db: LibraryDatabase, playlist_id: str, *, session: str = LOCAL_SESSION
) -> LibraryViewResult:
    """Widok "Playlista: <id>" (``MainWindow.xaml.cs:12462-12482``).

    Kolejnosc to DOKLADNIE ``playlist.ItemIds`` (``playlist_items`` po
    ``ordinal``), bez sortu po tytule. Pozycje nieobecne w katalogu sesji sa
    POMIJANE (12477), wiec wierszy moze byc mniej niz zapisanych Id.

    Playlista nieznana albo z innej sesji to NIE blad: AMC przestawia widok na
    "Playlisty" (12468). Oddajemy to jako ``fallback_view``, zeby GUI mialo
    co zrobic, zamiast pokazywac pusta liste bez powodu.
    """
    match = next(
        (p for p in _playlists(db, None) if p.playlist_id == playlist_id),
        None,
    )
    if match is None or match.session_id.casefold() != session.casefold():
        return LibraryViewResult(
            rows=[],
            heading=PLAYLISTS_VIEW,
            order_matches_amc=True,
            sees_live_writes=db.sees_live_writes,
            fallback_view=PLAYLISTS_VIEW,
        )
    by_id = {i.id: i for i in active_items(db)}
    rows = [_track_row(by_id[i]) for i in match.item_ids if i in by_id]
    return LibraryViewResult(
        rows=rows,
        heading=f"Playlista — {match.name}",
        order_matches_amc=True,
        sees_live_writes=db.sees_live_writes,
    )
