"""Warstwa DANYCH aktywnosci lokalnej Biblioteki (tylko odczyt).

Trzy odczyty, ktorych brakowalo nad widokami biblioteki:

1. **historia odtwarzania** plikow lokalnych,
2. **ZAPISANA kolejka** (to, co AMC utrwalilo w profilu),
3. **zakladki wybranego elementu** -- wejsciem jest RZECZYWISTE Id elementu,
4. **ZBIORCZY widok wszystkich zakladek** (``BookmarkIndex.GetForDisplay``) --
   wszystkie sesje, inny filtr i inna kolejnosc niz punkt 3.

Reguly filtrow, kolejnosci, duplikatow i pozycji nieznanych sa PRZEPISANE
z aktualnego kodu AMC (C#), nie zgadniete z nazw tabel. Cytaty i numery
wierszy: ``wxlite/LIBRARY_ACTIVITY_CONTRACT.md``.

Czego ten modul NIE robi
------------------------
Nie jest kolejka dzialajacego odtwarzacza Pythona. ``saved_queue_rows`` czyta
UTRWALONY stan profilu; kolejka zywego silnika to inna rzecz i nie ma tu
zadnego API odtwarzania. Modul nie zapisuje do bazy ani do profilu i nie
dotyka ``library_db.py``, ``library_views.py`` ani ``library_source.py`` --
pracuje na gotowym, read-only ``LibraryDatabase``.
"""

from __future__ import annotations

from dataclasses import dataclass, field
from typing import Sequence

from .collation import (
    COLLATION_TITLE_IGNORE_CASE,
    HostCollation,
    HostCollationUnavailable,
)
from .library_db import LibraryDatabase, LibraryItem, _ACTIVE, _format_detail
from .list_model import Row

#: Sesja plikow lokalnych.
LOCAL_SESSION = "local"

#: ``PlaybackHistory.MaxEntriesPerSession`` (``PlaybackHistory.cs:5``).
MAX_HISTORY_ENTRIES_PER_SESSION = 500

_ITEM_COLUMNS = (
    "id, title, path, duration_ticks, is_favorite, is_available, "
    "is_in_library, is_in_queue, is_play_next, is_radio_recording"
)


@dataclass(frozen=True, slots=True)
class ActivityResult:
    """Wynik jednego odczytu: wiersze + uczciwy opis tego, czego brakuje.

    ``missing_item_count`` to liczba ZAPISANYCH wpisow, ktorych oryginal NIE
    pokazuje (brak w katalogu sesji). Nie tworzymy dla nich wierszy, bo WPF
    tego nie robi -- ale liczba jest jawna, zeby puste pole nie wygladalo jak
    utrata danych.
    """

    rows: list[Row]
    heading: str
    order_matches_amc: bool = True
    sees_live_writes: bool = True
    missing_item_count: int = 0
    #: Metadane kolejki/zakladek OBOK ``Row`` (bez zmiany konstruktora ``Row``).
    queue: tuple["QueueRow", ...] = ()
    bookmarks: tuple["BookmarkRow", ...] = ()
    #: Tylko dla ZBIORCZEGO widoku zakladek: kontekst biezacego materialu i
    #: jawna informacja, czy da sie skoczyc lokalnie. Widok jednego pliku tego
    #: nie ma, bo nie zna obcych sesji.
    display: tuple["BookmarkDisplayRow", ...] = ()

    @property
    def is_empty(self) -> bool:
        return not self.rows


@dataclass(frozen=True, slots=True)
class QueueRow:
    """Jedna pozycja ZAPISANEJ kolejki, metadane obok ``Row``."""

    row: Row
    item_id: str
    is_in_queue: bool
    is_play_next: bool


@dataclass(frozen=True, slots=True)
class BookmarkRow:
    """Jedna zakladka, metadane obok ``Row``."""

    row: Row
    bookmark_id: str
    item_id: str
    item_title: str
    name: str
    session_id: str
    session_name: str
    position_ticks: int
    created_utc_ticks: int


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


def _active_catalog(db: LibraryDatabase) -> dict[str, LibraryItem]:
    """Katalog sesji ``local`` po Id, ``StringComparer.Ordinal``.

    Odpowiednik ``_sessions.Current.Items.ToDictionary(item => item.Id,
    StringComparer.Ordinal)`` (``MainWindow.xaml.cs:12855``). Sesja lokalna
    powstaje z ``ActiveLocalItems()`` (``MainWindow.xaml.cs:9972``,
    ``10116-10117``: ``IsAvailable && IsInLibrary``), a ``ReplaceItems`` robi
    ``DistinctBy(Id, Ordinal)`` -- pierwsze wystapienie wygrywa.
    """
    catalog: dict[str, LibraryItem] = {}
    rows = db.connection.execute(
        f"SELECT {_ITEM_COLUMNS} FROM local_items WHERE {_ACTIVE} ORDER BY rowid"
    )
    for row in rows:
        item = _item(row)
        catalog.setdefault(item.id, item)
    return catalog


def _stored_ids(db: LibraryDatabase, table: str, session: str) -> list[str]:
    """Zapisana lista Id jednej sesji, ``ORDER BY ordinal``.

    Nazwa tabeli pochodzi z ZAMKNIETEGO zbioru, nie z wejscia uzytkownika.
    Klucz sesji w C# jest ``OrdinalIgnoreCase``
    (``PlaybackHistory.cs:47-49``, ``CollectionOrderSettings``
    ``AppSettings.cs:1199-1212``), wiec dopasowanie robimy bez wzgledu na
    wielkosc liter -- ale WYLACZNIE dla klucza sesji. Id zostaja ``Ordinal``.
    """
    if table not in (
        "playback_history",
        "queue_order",
        "queue_regular_order",
        "queue_play_next_order",
    ):
        raise ValueError(f"Nieznana tabela aktywnosci: {table}")
    rows = db.connection.execute(
        f"SELECT item_id FROM {table} WHERE session_id = ? COLLATE NOCASE "
        "ORDER BY ordinal",
        (session,),
    )
    return [str(row[0]) for row in rows]


def _distinct_ordinal(ids: Sequence[str]) -> list[str]:
    """``Normalize``: odrzuc biale Id, potem ``Distinct(Ordinal)``.

    ``PlaybackHistory.cs:52-55``. Biale Id jest ODRZUCANE, a nie liczone jako
    wpis bez pozycji w katalogu -- ``Record`` takiego Id w ogole nie zapisze
    (``cs:19``).
    """
    seen: set[str] = set()
    result: list[str] = []
    for item_id in ids:
        if not item_id or not item_id.strip():
            continue
        if item_id in seen:
            continue
        seen.add(item_id)
        result.append(item_id)
    return result


# ---------------------------------------------------------------- 1. historia


def history_rows(
    db: LibraryDatabase, *, session: str = LOCAL_SESSION
) -> ActivityResult:
    """Widok "Historia odtwarzania" (``MainWindow.xaml.cs:12853-12860``).

    Kolejnosc jest ZAPISANA, nie liczona: ``ordinal`` 0 to ostatnio odtworzone,
    bo ``Record`` wstawia na przod (``PlaybackHistory.cs:29``), a widok czyta
    ``GetItemIds`` bez zadnego ``OrderBy``. Zadnej alfabetyki, wiec klucze
    kolatora nie sa tu potrzebne i ``order_matches_amc`` zostaje ``True``.

    Reguly przeniesione 1:1:

    * puste/biale ``sessionId`` -> pusta lista (``PlaybackHistory.cs:9-13``),
    * ``Distinct(Ordinal)`` i ``Take(500)`` z ``Normalize``
      (``PlaybackHistory.cs:52-56``) -- obcinany jest OGON,
    * pozycja nieobecna w katalogu sesji NIE daje wiersza
      (``MainWindow.xaml.cs:12857-12859``), bez placeholdera,
    * katalog sesji lokalnej to ``ActiveLocalItems()``, wiec wpis niedostepny
      albo poza biblioteka nie jest widoczny -- to regula ORYGINALU
      (``MainWindow.xaml.cs:9972``, ``10116-10117``), nie nasz skrot.
    """
    heading = "Biblioteka — Historia odtwarzania"
    if not session or not session.strip():
        return ActivityResult(
            rows=[],
            heading=heading,
            sees_live_writes=db.sees_live_writes,
        )

    stored = _distinct_ordinal(_stored_ids(db, "playback_history", session))[
        :MAX_HISTORY_ENTRIES_PER_SESSION
    ]
    catalog = _active_catalog(db)
    rows: list[Row] = []
    missing = 0
    for item_id in stored:
        item = catalog.get(item_id)
        if item is None:
            missing += 1
            continue
        rows.append(_track_row(item))
    return ActivityResult(
        rows=rows,
        heading=heading,
        sees_live_writes=db.sees_live_writes,
        missing_item_count=missing,
    )


# --------------------------------------------------------- 2. zapisana kolejka


def saved_queue_rows(
    db: LibraryDatabase, *, session: str = LOCAL_SESSION
) -> ActivityResult:
    """ZAPISANA kolejka sesji (``MainWindow.xaml.cs:13508-13525``).

    To odczyt tego, co AMC UTRWALILO w profilu: ``queue_order`` plus listy
    czlonkostwa ``queue_regular_order`` i ``queue_play_next_order``. NIE jest
    to kolejka zywego silnika odtwarzania -- taki silnik w tej warstwie nie
    istnieje i nie udajemy jego API.

    Kolejnosc jest ZAPISANA, dwustopniowa, dokladnie jak w
    ``OrderedQueueItems`` (``MainWindow.xaml.cs:13518-13525``):

    1. ``LocalLibraryManualOrder.Order(items, stored)``
       (``LocalLibraryManualOrder.cs:89-105``): pozycja = indeks PIERWSZEGO
       wystapienia Id w zapisie, nieznane Id dostaje ``int.MaxValue``, remis
       rozstrzyga indeks wejsciowy katalogu;
    2. ``OrderByDescending(IsPlayNext)`` -- stabilne, wiec blok "odtworz
       nastepne" idzie na gore, a kolejnosc wewnatrz blokow zostaje.

    Czlonkostwo bierzemy z ZAPISU, a nie z kolumn ``is_in_queue`` /
    ``is_play_next``: ``TransientQueuePersistence.Restore`` najpierw zeruje
    oba znaczniki dla wszystkich pozycji (``cs:109-113``), a potem nadaje je
    tylko Id opisanym zapisana kolejnoscia (``cs:114-127``). Kolumny w bazie
    sa migawka POPRZEDNIEJ sesji i nie moga wygrac z zapisem.

    ``legacyRegularQueue`` (``cs:101``): gdy OBIE listy czlonkostwa sa puste,
    cala ``queue_order`` jest zwykla kolejka -- starszy zapis nadal dziala
    i nie wolno go wyrzucac.

    Mapowanie Id na klucz magazynu (``StorageItemId``, ``cs:130-138``) dotyczy
    sesji ``tidal`` z ``ExternalId``; dla ``local`` kluczem jest samo ``Id``,
    wiec nie wprowadzamy tu zdalnej logiki, ktorej ten modul nie obsluguje.
    """
    heading = "Biblioteka — Kolejka (zapisana)"
    if not session or not session.strip():
        return ActivityResult(
            rows=[], heading=heading, sees_live_writes=db.sees_live_writes
        )

    stored = _distinct_ordinal(_stored_ids(db, "queue_order", session))
    regular = set(_stored_ids(db, "queue_regular_order", session))
    play_next = set(_stored_ids(db, "queue_play_next_order", session))
    legacy_regular = not regular and not play_next

    catalog = _active_catalog(db)
    #: Indeks wejsciowy katalogu -- tie-break ``ThenBy(originalIndex)``
    #: z ``LocalLibraryManualOrder.Order``.
    original_index = {item_id: index for index, item_id in enumerate(catalog)}

    entries: list[tuple[int, int, str, bool, bool]] = []
    missing = 0
    for position, item_id in enumerate(stored):
        if item_id not in catalog:
            missing += 1
            continue
        in_queue = legacy_regular or item_id in regular
        is_play_next = item_id in play_next
        if not in_queue and not is_play_next:
            # ``Restore`` zostawil oba znaczniki na false, a filtr widoku
            # (``MainWindow.xaml.cs:13520``) przepuszcza tylko
            # ``IsInQueue || IsPlayNext``.
            continue
        entries.append(
            (position, original_index.get(item_id, 0), item_id, in_queue, is_play_next)
        )

    # Dwa stabilne przebiegi, tak jak LINQ: najpierw kolejnosc reczna,
    # potem OrderByDescending(IsPlayNext).
    entries.sort(key=lambda entry: (entry[0], entry[1]))
    entries.sort(key=lambda entry: not entry[4])

    rows: list[Row] = []
    queue: list[QueueRow] = []
    for _, _, item_id, in_queue, is_play_next in entries:
        row = _track_row(catalog[item_id])
        rows.append(row)
        queue.append(
            QueueRow(
                row=row,
                item_id=item_id,
                is_in_queue=in_queue,
                is_play_next=is_play_next,
            )
        )
    return ActivityResult(
        rows=rows,
        heading=heading,
        sees_live_writes=db.sees_live_writes,
        missing_item_count=missing,
        queue=tuple(queue),
    )


# ------------------------------------------- 3. zakladki wybranego elementu


#: ``BookmarkPurpose.Bookmark`` (``AppSettings.cs:1151``). ``Chapter`` = 2.
_PURPOSE_BOOKMARK = 1

#: Nazwy miesiecy w dopelniaczu, czyli to, co .NET daje dla ``pl-PL`` i wzorca
#: ``"d MMMM yyyy"`` (``MainWindow.xaml.cs:13773``). Zmierzone na .NET 8.0.425.
_PL_MONTHS_GENITIVE = (
    "stycznia",
    "lutego",
    "marca",
    "kwietnia",
    "maja",
    "czerwca",
    "lipca",
    "sierpnia",
    "września",
    "października",
    "listopada",
    "grudnia",
)

#: ``DateTime`` .NET liczy ticki od 0001-01-01; ``datetime`` Pythona ma ten sam
#: punkt zerowy, wiec konwersja nie potrzebuje zadnej stalej "magicznej".
_TICKS_PER_MICROSECOND = 10


def format_position(ticks: int) -> str:
    """``CommandRouter.FormatTime`` -> ``MediaItemFormatter.FormatDuration``.

    ``MediaItemFormatter.cs:69-73``: ``h:mm:ss`` od godziny, inaczej ``m:ss``.
    Jednostka wejscia to TICKI C# (1 s = 10 000 000), nie sekundy.
    """
    total_seconds = max(0, int(ticks)) // 10_000_000
    hours, rest = divmod(total_seconds, 3600)
    minutes, seconds = divmod(rest, 60)
    if hours:
        return f"{hours}:{minutes:02d}:{seconds:02d}"
    return f"{minutes}:{seconds:02d}"


def format_created_date(utc_ticks: int) -> str:
    """``FormatBookmarkCreatedDate`` (``MainWindow.xaml.cs:13767-13779``).

    Nieznany czas to NIE ``0:00`` ani 1 stycznia roku 1: przy ``utcTicks <= 0``
    oryginal mowi wprost "data utworzenia nieznana", a przy tickach poza
    zakresem ``DateTime`` lapie ``ArgumentOutOfRangeException`` i daje to samo.
    """
    from datetime import datetime, timedelta, timezone

    if utc_ticks <= 0:
        return "data utworzenia nieznana"
    try:
        moment = datetime(1, 1, 1, tzinfo=timezone.utc) + timedelta(
            microseconds=utc_ticks // _TICKS_PER_MICROSECOND
        )
        local = moment.astimezone()
    except (OverflowError, OSError, ValueError):
        return "data utworzenia nieznana"
    month = _PL_MONTHS_GENITIVE[local.month - 1]
    return f"utworzono {local.day} {month} {local.year}"


def _bookmark_row(record) -> BookmarkRow:
    """Jeden rekord tabeli ``bookmarks`` -> ``BookmarkRow`` z etykieta.

    JEDNA kopia na oba widoki zakladek. Etykieta i Id wiersza pochodza z
    ``CreateBookmarkRow`` (``MainWindow.xaml.cs:13744-13764``), ktory w
    oryginale tez jest jeden -- ``GetForItem`` i ``GetForDisplay`` oba przez
    niego przechodza. Gdyby kazdy widok sklejal etykiete osobno, roznilyby sie
    po pierwszej poprawce formatu.
    """
    bookmark_id = str(record["id"])
    stored_title = str(record["item_title"])
    name = str(record["name"]).strip()
    session_name = str(record["session_name"]).strip() or str(record["session_id"])
    position_ticks = int(record["position_ticks"])
    created_ticks = int(record["created_utc_ticks"])
    name_part = f", {name}" if name else ""
    label = (
        f"{stored_title}, {format_created_date(created_ticks)}, "
        f"{format_position(position_ticks)}{name_part}, {session_name}, zakładka"
    )
    return BookmarkRow(
        row=Row(
            # Wlasne Id wiersza, zeby dwie zakladki tego samego utworu nie
            # zlaly sie w wyborze (``MainWindow.xaml.cs:13756``).
            item_id=f"bookmark:{bookmark_id}",
            title=label,
            kind="track",
            detail="",
        ),
        bookmark_id=bookmark_id,
        item_id=str(record["item_id"]),
        item_title=stored_title,
        name=name,
        session_id=str(record["session_id"]),
        session_name=session_name,
        position_ticks=position_ticks,
        created_utc_ticks=created_ticks,
    )


def bookmark_rows(
    db: LibraryDatabase, *, item_id: str, session: str = LOCAL_SESSION
) -> ActivityResult:
    """Zakladki JEDNEGO elementu (``BookmarkIndex.GetForItem``, ``cs:30-36``).

    Wejsciem jest RZECZYWISTE Id elementu, nie indeks listy -- w C# tej funkcji
    nie da sie zawolac inaczej.

    Reguly przeniesione 1:1:

    * widoczne sa tylko wpisy z bitem ``Bookmark`` w ``Purpose``
      (``IsBookmark``, ``BookmarkIndex.cs:189-190``); czysty rozdzial
      (``Chapter``) NIE jest zakladka i nie jest usuwany -- tylko niewidoczny,
    * ``SessionId`` porownuje sie ``OrdinalIgnoreCase``, a ``ItemId``
      ``Ordinal`` (``cs:32-33``) -- dwa rozne porownania w jednym warunku,
    * kolejnosc ``OrderBy(PositionTicks).ThenBy(CreatedUtcTicks)``
      (``cs:34-35``): porzadek liczbowy, bez alfabetyki, wiec klucze kolatora
      sa tu niepotrzebne,
    * dwa wpisy o tej samej pozycji ZOSTAJA oba: tolerancja 1 s dziala tylko
      w ``Add`` (``cs:10``, ``53-56``), odczyt niczego nie scala,
    * zakladka pozycji nieobecnej w katalogu NADAL jest widoczna, z tytulem
      zapisanym w zakladce (``MainWindow.xaml.cs:13749-13757``) -- tu
      placeholder w oryginale JEST, inaczej niz w historii,
    * wiersz ma wlasne Id ``bookmark:{Id}`` (``MainWindow.xaml.cs:13756``),
      wiec dwie zakladki tego samego utworu nie zlewaja sie w wyborze.
    """
    heading = "Biblioteka — Zakładki"
    if not item_id or not item_id.strip() or not session or not session.strip():
        return ActivityResult(
            rows=[], heading=heading, sees_live_writes=db.sees_live_writes
        )

    # Filtr i porzadek robi SQL, ale predykat ``purpose`` jest bitowy, tak jak
    # ``IsBookmark`` -- nie "purpose = 1", bo wpis o obu bitach (3) tez jest
    # zakladka. Id elementu zostaje binarne (odpowiednik ``Ordinal``), a klucz
    # sesji dostaje ``COLLATE NOCASE`` (odpowiednik ``OrdinalIgnoreCase``).
    rows_sql = db.connection.execute(
        "SELECT id, session_id, session_name, item_id, item_title, name, "
        "position_ticks, created_utc_ticks FROM bookmarks "
        "WHERE session_id = ? COLLATE NOCASE AND item_id = ? "
        f"AND (purpose & {_PURPOSE_BOOKMARK}) != 0 "
        "ORDER BY position_ticks, created_utc_ticks",
        (session, item_id),
    )

    rows: list[Row] = []
    bookmarks: list[BookmarkRow] = []
    for record in rows_sql:
        bookmark = _bookmark_row(record)
        rows.append(bookmark.row)
        bookmarks.append(bookmark)
    return ActivityResult(
        rows=rows,
        heading=heading,
        sees_live_writes=db.sees_live_writes,
        bookmarks=tuple(bookmarks),
    )


# --------------------------------- 4. ZBIORCZY widok wszystkich zakladek


@dataclass(frozen=True, slots=True)
class BookmarkDisplayRow:
    """Jedna zakladka w widoku ZBIORCZYM: dane plus kontekst wyswietlania.

    ``BookmarkRow`` zostaje nietkniety -- widok jednego pliku go uzywa i nie ma
    pojecia o obcych sesjach. Tutaj dochodzi to, co jest prawda tylko dla
    widoku zbiorczego: czy wpis nalezy do AKTUALNIE odtwarzanego materialu i
    czy ten przyrost potrafi go odtworzyc.
    """

    bookmark: BookmarkRow
    #: ``IsCurrentItem(entry, currentSessionId, currentItemId)``
    #: (``BookmarkIndex.cs:182-187``) -- pierwszy klucz sortowania.
    is_current_item: bool
    #: ``True`` tylko dla sesji ``local``. Skok w tym przyroscie idzie przez
    #: ``files.play(positionSeconds)``, a ten kanal istnieje WYLACZNIE dla
    #: plikow lokalnych. Obca sesja (TIDAL, podcasty) nie ma tu odtwarzania i
    #: nie udajemy, ze ma -- odtwarzanie sieciowe to osobna sprawa.
    can_play_locally: bool

    @property
    def row(self) -> Row:
        return self.bookmark.row

    @property
    def position_seconds(self) -> float:
        """Pozycja w SEKUNDACH z ulamkiem -- argument dla ``files.play``.

        Ticki C# to 1/10 000 000 s, wiec dzielenie jest DOKLADNE tylko w
        ``float``; nie zaokraglamy do calych sekund, bo zakladka na 1,85 s
        skoczylaby na 1 s albo 2 s.
        """
        return self.bookmark.position_ticks / 10_000_000


def ordinal_sort_key(value: str) -> bytes:
    """Klucz odtwarzajacy ``StringComparer.Ordinal`` -- ostatni tie-break.

    ``Ordinal`` w .NET porownuje ``char``, czyli JEDNOSTKI KODOWE UTF-16, i
    jest CASE-SENSITIVE. Dwie konsekwencje, obie zmierzone sonda
    ``all-bookmarks-after422/probe-display-order`` na .NET 8:

    * ``utf-16-be`` zachowuje dokladnie ten porzadek (0 niezgodnych par na 69
      napisach, 2346 porownan);
    * ``utf-8`` i zwykle ``str`` Pythona porzadkuja PUNKTY KODOWE -- 12
      niezgodnych par. Emoji ``U+1F600`` zaczyna sie w UTF-16 od ``D83D`` i
      jest MNIEJSZE od ``U+FFFD``, a w punktach kodowych wieksze.

    Dlatego NIE uzywamy tu hostowego ``ORDINAL_IGNORE_CASE``: ten tryb
    podnosi litery do wersalikow (862 niezgodne pary wzgledem ``Ordinal``) i
    zrownalby Id ``"a"`` z ``"A"``, oddajac kolejnosc przypadkowi. Klucz
    liczymy LOKALNIE, bo to czysta transformacja bajtow bez udzialu kultury --
    nie potrzebuje hosta i nie jest nowym API kolacji.

    Zadnej normalizacji Unicode nie robimy: ``Ordinal`` jej nie robi, a
    ``NFC``/``NFD`` na Id zmienilaby porzadek wpisow, ktorych oryginal nie
    rusza.
    """
    return value.encode("utf-16-be", errors="surrogatepass")


def all_bookmark_rows(
    db: LibraryDatabase,
    *,
    current_session_id: str,
    current_item_id: str,
    collation: HostCollation | None,
) -> ActivityResult:
    """ZBIORCZY widok wszystkich zakladek (``BookmarkIndex.GetForDisplay``).

    To NIE jest ``bookmark_rows``. Tam wejsciem jest jeden element i widoczne
    sa tylko jego zakladki; tutaj widoczne sa zakladki WSZYSTKICH sesji, a
    biezacy material jedynie wedruje na gore listy. Oryginal ma dwie osobne
    metody (``cs:19-28`` i ``cs:30-36``) i my tez.

    Kontekst jest JAWNY
    ---------------------
    ``current_session_id``/``current_item_id`` sa argumentami, bo w WPF tak samo
    pochodza z ``_sessions.Current.Id`` i ``_sessions.Current.CurrentItem.Id``
    (``MainWindow.xaml.cs:12530-12532``). To AKTUALNIE ODTWARZANY material, a
    nie zaznaczony wiersz listy -- ta warstwa niczego nie zgaduje z globalnego
    stanu ani z timera. Pusty kontekst jest legalny (``MediaItem`` z pustym
    ``Id``, gdy sesja nic nie gra) i po prostu do niczego nie pasuje.

    Kolejnosc 1:1 z ``cs:19-28``
    ----------------------------
    1. ``IsCurrentItem ? 0 : 1`` -- zakladki granego materialu na gorze,
    2. ``SessionName`` ``CurrentCultureIgnoreCase``,
    3. ``ItemTitle`` ``CurrentCultureIgnoreCase``,
    4. ``PositionTicks``, 5. ``CreatedUtcTicks``, 6. ``Id`` ``Ordinal``.

    Punkty 2-3 to ``CompareOptions.IgnoreCase`` SAMO, czyli tryb kolacji
    ``TITLE_IGNORE_CASE``, a NIE ``AMC_PL``: ``IgnoreNonSpace`` zrownalby
    ``"etap"`` z ``"étap"`` i remis spadlby na pozycje, dajac inna liste.
    Klucze liczy host WSADOWO (jedno wywolanie na caly ekran), wiec nie ma tu
    porownan parami przez IPC -- 5000 zakladek to dwa pola tekstowe na wpis,
    nie 12 mln porownan.

    ``collation=None`` zwraca wiersze w kolejnosci ZAPISU z ``order_matches_amc
    = False``. Brak kolatora nie moze udawac zgodnosci -- to bylaby cicha
    niezgodnosc w widoku, ktory wyglada poprawnie.

    Czego ten widok NIE robi
    ------------------------
    Nie filtruje po dostepnosci pliku: wpis, ktorego material zniknal, NADAL
    jest widoczny z tytulem zapisanym w zakladce (``CreateBookmarkRow``,
    ``MainWindow.xaml.cs:13749-13757``). Przeniesienie tu filtra z historii
    ukrylo by polowe prawdziwych danych. Nie odtwarza tez niczego: zwraca
    jawne ``session_id``/``item_id``/``position_seconds`` i mowi wprost
    (``can_play_locally``), ze obca sesja nie ma w tym przyroscie kanalu
    odtwarzania.
    """
    heading = "Biblioteka — Wszystkie zakładki"

    records = db.connection.execute(
        "SELECT id, session_id, session_name, item_id, item_title, name, "
        "position_ticks, created_utc_ticks FROM bookmarks "
        # Predykat bitowy, tak jak ``IsBookmark`` (``cs:189-190``): wpis o obu
        # bitach (3) tez jest zakladka, a czysty ``Chapter`` (2) nie jest -- i
        # nie jest usuwany, tylko niewidoczny w tym widoku.
        f"WHERE (purpose & {_PURPOSE_BOOKMARK}) != 0 "
        # Kolejnosc z SQL jest tylko DETERMINISTYCZNYM wejsciem (``ordinal`` to
        # kolejnosc zapisu w profilu). Prawdziwy porzadek liczymy nizej, bo
        # SQLite nie zna ``CurrentCultureIgnoreCase``.
        "ORDER BY ordinal"
    )

    bookmarks: list[BookmarkRow] = []
    flags: list[bool] = []
    for record in records:
        bookmark = _bookmark_row(record)
        bookmarks.append(bookmark)
        flags.append(
            _is_current_item(bookmark, current_session_id, current_item_id)
        )

    if collation is None:
        display = tuple(
            BookmarkDisplayRow(
                bookmark=bookmark,
                is_current_item=flag,
                can_play_locally=_is_local_session(bookmark.session_id),
            )
            for bookmark, flag in zip(bookmarks, flags)
        )
        return ActivityResult(
            rows=[d.row for d in display],
            heading=heading,
            order_matches_amc=False,
            sees_live_writes=db.sees_live_writes,
            bookmarks=tuple(d.bookmark for d in display),
            display=display,
        )

    # Jedno wsadowe zadanie na WSZYSTKIE napisy obu pol. Kolator pamieta
    # klucze per tryb, wiec powtorzony tytul nie generuje drugiego zapytania.
    texts = [b.session_name for b in bookmarks] + [b.item_title for b in bookmarks]
    if texts:
        collation.load(texts, mode=COLLATION_TITLE_IGNORE_CASE)

    def sort_key(pair: tuple[BookmarkRow, bool]) -> tuple:
        bookmark, is_current = pair
        session_key = collation.key_for(
            bookmark.session_name, mode=COLLATION_TITLE_IGNORE_CASE
        )
        title_key = collation.key_for(
            bookmark.item_title, mode=COLLATION_TITLE_IGNORE_CASE
        )
        if session_key is None or title_key is None:
            # Czesc listy zgodna z C#, a czesc nie, to gorsze niz jawny blad:
            # nikt by tego nie zauwazyl. ``sort_key_order`` w ``collation``
            # odmawia z tego samego powodu.
            brak = bookmark.session_name if session_key is None else bookmark.item_title
            raise HostCollationUnavailable(
                f"Brak klucza {COLLATION_TITLE_IGNORE_CASE} dla {brak!r} "
                f"(zakladka {bookmark.bookmark_id})."
            )
        return (
            0 if is_current else 1,
            session_key,
            title_key,
            bookmark.position_ticks,
            bookmark.created_utc_ticks,
            ordinal_sort_key(bookmark.bookmark_id),
        )

    ordered = sorted(zip(bookmarks, flags), key=sort_key)

    display = tuple(
        BookmarkDisplayRow(
            bookmark=bookmark,
            is_current_item=flag,
            can_play_locally=_is_local_session(bookmark.session_id),
        )
        for bookmark, flag in ordered
    )
    return ActivityResult(
        rows=[d.row for d in display],
        heading=heading,
        sees_live_writes=db.sees_live_writes,
        bookmarks=tuple(d.bookmark for d in display),
        display=display,
    )


def _is_local_session(session_id: str) -> bool:
    """Czy to sesja plikow lokalnych -- klucz sesji jest ``OrdinalIgnoreCase``."""
    return session_id.casefold() == LOCAL_SESSION.casefold()


def _is_current_item(
    bookmark: BookmarkRow, current_session_id: str, current_item_id: str
) -> bool:
    """``IsCurrentItem`` (``BookmarkIndex.cs:182-187``).

    DWA rozne porownania w jednym warunku: ``SessionId`` ``OrdinalIgnoreCase``,
    ``ItemId`` ``Ordinal``. Zrownanie ich (choc kusi) zmienialoby wynik --
    zmierzone sonda: kontekst ``ITEM-BIEZ`` daje w C# INNA liste niz
    ``item-biez``.

    ``casefold`` jest tu bezpieczne, bo ``OrdinalIgnoreCase`` w .NET podnosi
    znaki INVARIANTNIE, bez kultury -- a nie jest to porzadek, tylko rownosc,
    wiec zaden klucz kolacji nie jest potrzebny.
    """
    return (
        bookmark.session_id.casefold() == current_session_id.casefold()
        and bookmark.item_id == current_item_id
    )
