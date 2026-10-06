"""Odczyt PRAWDZIWEJ Biblioteki AMC z ``library.db`` (SQLite).

Dlaczego ten modul istnieje
---------------------------
Biblioteka AMC nie jest katalogiem na dysku. Jest tabela ``local_items`` w
``library.db`` plus trzy ``folder_sources``. Poprzednia wersja wxlite pytala
system plikow (``files.listFolder`` + ``Path.exists``), wiec na maszynie bez
``D:\\`` i bez ``C:\\Users\\micha`` lista pod Ctrl+1 byla PUSTA -- dokladnie
to zglosil uzytkownik.

Zasady, ktore ten modul trzyma twardo
-------------------------------------
1. **Tylko odczyt -- ale SWIEZY.** Polaczenie otwieramy jako ``mode=ro``.
   Wczesniej bylo ``mode=ro&immutable=1`` i to byl BLAD: ``immutable`` mowi
   SQLite, ze plik sie nie zmienia, wiec silnik pomija ``-wal`` i ``-shm``.
   Zmierzone na kopii profilu przy ZYWYM, nadal otwartym writerze:

   =========================  =========================================
   ``mode=ro``                widzi zatwierdzony commit hosta
   ``mode=ro&immutable=1``    oddaje STARA wartosc sprzed commitu
   =========================  =========================================

   ``mode=ro`` nie zapisuje do bazy ani nie migruje schematu; moze jedynie
   odwzorowac istniejacy ``-shm`` (techniczna koordynacja czytelnikow WAL),
   co nie jest zmiana danych aplikacji. Wlasciciel zapisu zostaje jeden:
   host C#. Nie migrujemy schematu, nie robimy checkpointu, nie dopisujemy
   niczego i nie uruchamiamy harmonogramow.

1a. **Migawka NIE jest domyslna i nigdy nie jest cicha.** Gdy baza lezy w
   miejscu BEZ prawa zapisu, ``mode=ro`` nie potrafi obsluzyc ``-shm`` i konczy
   sie ``OperationalError`` (zmierzone: ``attempt to write a readonly
   database`` albo ``unable to open database file``, zaleznie od tego, czy
   ``-wal`` istnieje). Wczesniej kod przechodzil wtedy AUTOMATYCZNIE na
   ``immutable=1`` i oddawal stan sprzed cudzych commitow -- bez slowa.
   Zmierzone na kopii profilu, writer OTWARTY, ``-wal`` 4152 B:

   ================================  =====================================
   ``mode=ro``, katalog bez zapisu   ``unable to open database file``
   ``immutable=1`` na tym pliku      znacznik SPRZED commitu (``-wal`` pominiety)
   ================================  =====================================

   Dlatego teraz:

   * domyslnie ``allow_snapshot_fallback=False`` -- blad wychodzi jako
     ``LiveProfileReadDenied`` i nikt nie dostaje starych danych w milczeniu,
   * jawna migawka (``allow_snapshot_fallback=True``) jest brana TYLKO gdy
     jest spojna, czyli gdy nie ma nieprzeniesionego ``-wal``; inaczej leci
     ``StaleSnapshotRefused``,
   * udana migawka nadal mowi o sobie ``sees_live_writes == False``,
   * czesciowo otwarte polaczenie jest ZAMYKANE przed retry/wyjsciem, zeby nie
     zostawic uchwytu do bazy po nieudanym odczycie.
2. **Kolacja AMC_PL.** Schemat deklaruje ``COLLATE AMC_PL`` na ``title`` i
   ``display_name``. Bez zarejestrowania tej kolacji SQLite odmawia zapytan,
   wiec ``polish_collation`` MUSI tu byc. Ale to NIE jest zgodnik z C# --
   wczesniejszy komentarz nazywal ja "portem 1:1", co bylo nieprawda:
   zgodnosc z ``CompareInfo`` pl-PL zostala zmierzona i jej NIE MA.
   ``polish_collation`` sluzy wylacznie temu, zeby zapytanie sie wykonalo;
   kolejnosc PREZENTOWANA uzytkownikowi ustala ``collation.HostCollation``
   na kluczach policzonych oryginalnym ``CompareInfo`` w hoscie.
3. **ID sa NAPISAMI.** W tym projekcie pomylenie napisu z liczba raz juz
   zepsulo protokol. Nigdzie nie rzutujemy ID na int i nie przenumerowujemy.
4. **Nie filtrujemy przez istnienie pliku.** ``D:\\...`` i
   ``C:\\Users\\micha\\...`` nie istnieja na tej maszynie; rekordy i tak
   musza byc widoczne. Dostepnosc bierzemy z kolumny ``is_available``, ktora
   zapisal host -- nie z ``Path.exists`` tutaj.
"""

from __future__ import annotations

import ntpath
import sqlite3
import unicodedata
from dataclasses import dataclass
from pathlib import Path
from typing import Iterable, Sequence

from .list_model import Row

AMC_PL = "AMC_PL"

# ``ł``/``Ł`` nie rozkladaja sie przez NFD, a pl-PL z IgnoreNonSpace traktuje je
# jak ``l``. Reszta polskich znakow znika po zdjeciu znakow laczacych.
_FOLDED = {"\u0142": "l", "\u0141": "L", "\u00df": "ss"}


def _fold(text: str) -> str:
    """Odpowiednik ``CompareOptions.IgnoreCase | IgnoreNonSpace`` dla pl-PL."""
    swapped = "".join(_FOLDED.get(char, char) for char in text)
    decomposed = unicodedata.normalize("NFD", swapped)
    stripped = "".join(c for c in decomposed if not unicodedata.combining(c))
    return stripped.casefold()


def polish_collation(left: str, right: str) -> int:
    """Kolacja AMC_PL -- **tylko** zeby zapytanie SQL dalo sie wykonac.

    SQLite odmawia zapytania na kolumnie ``COLLATE AMC_PL``, dopoki funkcja o
    tej nazwie nie jest zarejestrowana. To jest jej CALY zakres. Zgodnosc z
    ``CompareInfo`` pl-PL zostala zmierzona i jej nie ma -- kolejnosc
    prezentowana liczy ``collation.HostCollation`` przez host C#.
    """
    a, b = _fold(left), _fold(right)
    if a < b:
        return -1
    if a > b:
        return 1
    # Remis po zlozeniu: rozstrzygamy stabilnie, zeby kolejnosc listy nie
    # zmieniala sie miedzy odswiezeniami ("Łódź" vs "Lodz").
    if left < right:
        return -1
    if left > right:
        return 1
    return 0


def _ci_key(text: str) -> str:
    """Klucz sortowania list -- ``StringComparer.CurrentCultureIgnoreCase``."""
    return _fold(text)


# --------------------------------------------------------------------- sciezki
# AMC zapisuje sciezki Windows. Pracujemy na nich przez ``ntpath``, bo na
# Linuksie ``pathlib`` nie rozumie ``D:\\`` i rozsypalby porownania.

def normalize_path(path: str) -> str:
    return ntpath.normpath(path).rstrip("\\/") or path


def is_same_or_descendant(candidate: str, ancestor: str) -> bool:
    """Port ``IsSameOrDescendantPath`` (porownanie bez wzgledu na wielkosc)."""
    if not candidate or not ancestor:
        return False
    c = normalize_path(candidate).lower()
    a = normalize_path(ancestor).lower()
    return c == a or c.startswith(a + "\\")


def parent_path(path: str) -> str | None:
    norm = normalize_path(path)
    head = ntpath.dirname(norm)
    if not head or head == norm:
        return None
    return head


def display_name(path: str) -> str:
    norm = normalize_path(path)
    return ntpath.basename(norm) or norm


# ---------------------------------------------------------------------- rekordy

@dataclass(frozen=True, slots=True)
class FolderSource:
    id: str
    ordinal: int
    path: str
    display_name: str
    resume_mode: int


@dataclass(frozen=True, slots=True)
class LibraryItem:
    id: str
    title: str
    path: str
    duration_ticks: int
    is_favorite: bool
    is_available: bool
    is_in_library: bool
    is_radio_recording: bool

    @property
    def duration_seconds(self) -> float:
        return self.duration_ticks / 10_000_000


@dataclass(frozen=True, slots=True)
class LocalState:
    library_view: str
    current_folder_path: str | None
    current_item_id: str | None
    volume: int
    playback_rate: float


# Filtr ``ActiveLocalItems()`` z MainWindow: dostepne ORAZ w Bibliotece.
# To filtr widokow CZLONKOSTWA (Foldery, Wszystkie pliki, Ulubione,
# playlisty, zapisana kolejka) i jego tresc sie NIE zmienia.
_ACTIVE = "is_available = 1 AND is_in_library = 1"

# Filtr samej DOSTEPNOSCI, bez czlonkostwa. Uzywa go WYLACZNIE katalog
# historii odtwarzania (``library_activity._history_catalog``): historia
# odpowiada na pytanie ,,co odtwarzalem'', a nie ,,co mam w Bibliotece'',
# wiec plik otwarty bez dodania do Biblioteki ma zostac w Ctrl+H.
# Niedostepny plik nadal nie dostaje wiersza -- Enter na nim i tak nie
# zagralby. Zadnego innego widoku ta stala nie dotyczy.
_AVAILABLE = "is_available = 1"


def _close_quietly(connection: sqlite3.Connection | None) -> None:
    """Zamknij uchwyt, ktory moze byc juz czesciowo otwarty po bledzie."""
    if connection is None:
        return
    try:
        connection.close()
    except sqlite3.Error:
        pass


class LiveProfileReadDenied(sqlite3.OperationalError):
    """Nie da sie otworzyc ZYWEGO profilu, a stary obraz to nie odpowiedz."""


class StaleSnapshotRefused(sqlite3.OperationalError):
    """Zadano migawki, ale obok bazy lezy nieprzeniesiony ``-wal``.

    ``immutable=1`` pomija dziennik, wiec taka migawka pokazalaby stan SPRZED
    zatwierdzonych zmian. Lepiej odmowic, niz oddac ciche stare dane.
    """


class LibraryDatabase:
    """Polaczenie TYLKO DO ODCZYTU z ``library.db`` AMC.

    ``sees_live_writes`` mowi, czy to polaczenie sledzi WAL (zwykly ``mode=ro``),
    czy jest zamrozona migawka (``immutable=1``). Nigdy nie udajemy pierwszego,
    gdy mamy drugie -- a migawki nie bierzemy samowolnie.
    """

    def __init__(
        self,
        path: str | Path,
        *,
        allow_snapshot_fallback: bool = False,
    ) -> None:
        self.path = Path(path)
        if not self.path.exists():
            raise FileNotFoundError(f"Brak bazy Biblioteki: {self.path}")
        # Path.as_uri() escapuje '#', '?' i spacje; sklejanie "file:" + str()
        # gubilo wszystko po '?' i konczylo sie "no such table" (zmierzone).
        base = self.path.as_uri()
        self.uri = f"{base}?mode=ro"
        self.sees_live_writes = True
        connection: sqlite3.Connection | None = None
        try:
            connection = sqlite3.connect(self.uri, uri=True, check_same_thread=False)
            # Samo polaczenie moze sie udac, a ``-shm`` powstaje dopiero przy
            # pierwszym czytaniu strony z WAL. Dotykamy bazy TERAZ, zeby
            # ewentualna odmowa wyszla tutaj, a nie w trakcie pracy okna.
            connection.execute("SELECT count(*) FROM sqlite_schema").fetchone()
        except sqlite3.OperationalError as error:
            # Uchwyt moze byc juz czesciowo otwarty. Zamykamy go PRZED decyzja
            # o migawce albo wyjsciem -- inaczej zostaje wiszace polaczenie.
            _close_quietly(connection)
            connection = None
            if not allow_snapshot_fallback:
                raise LiveProfileReadDenied(
                    f"Nie moge otworzyc zywego profilu {self.path.name}: {error}. "
                    "Zamrozonej migawki NIE biore automatycznie, bo pokazalaby "
                    "stan sprzed zapisow AMC."
                ) from error
            wal = Path(str(self.path) + "-wal")
            if wal.exists() and wal.stat().st_size > 0:
                raise StaleSnapshotRefused(
                    f"Obok {self.path.name} lezy nieprzeniesiony dziennik "
                    f"{wal.name} ({wal.stat().st_size} B). Migawka "
                    "``immutable=1`` pomija dziennik, wiec nie byłaby spojna."
                ) from error
            # Dopiero tutaj migawka jest uczciwa: zadnego WAL do pominiecia.
            self.uri = f"{base}?mode=ro&immutable=1"
            connection = sqlite3.connect(self.uri, uri=True, check_same_thread=False)
            self.sees_live_writes = False
        self.connection = connection
        self.connection.create_collation(AMC_PL, polish_collation)
        self.connection.row_factory = sqlite3.Row

    def close(self) -> None:
        self.connection.close()

    def __enter__(self) -> "LibraryDatabase":
        return self

    def __exit__(self, *exc) -> None:
        self.close()

    # ------------------------------------------------------------ liczniki

    def count_items(self) -> int:
        return self.connection.execute("SELECT COUNT(*) FROM local_items").fetchone()[0]

    def count_active_items(self) -> int:
        return self.connection.execute(
            f"SELECT COUNT(*) FROM local_items WHERE {_ACTIVE}"
        ).fetchone()[0]

    def count_active_under(self, folder: str) -> int:
        norm = normalize_path(folder)
        return self.connection.execute(
            f"SELECT COUNT(*) FROM local_items WHERE {_ACTIVE} "
            "AND (path = ? COLLATE NOCASE OR path LIKE ? ESCAPE '\\')",
            (norm, _like_prefix(norm)),
        ).fetchone()[0]

    def schema_version(self) -> str | None:
        row = self.connection.execute(
            "SELECT value FROM metadata WHERE key = 'database_schema_version'"
        ).fetchone()
        return row[0] if row else None

    def active_path_prefixes(self) -> dict[str, int]:
        """Rozklad aktywnych rekordow po dyskach -- dowod, ze nic nie wycieto."""
        rows = self.connection.execute(
            f"SELECT substr(path, 1, 3) AS prefix, COUNT(*) AS n "
            f"FROM local_items WHERE {_ACTIVE} GROUP BY prefix"
        ).fetchall()
        return {row["prefix"]: row["n"] for row in rows}

    # ------------------------------------------------------------- odczyty

    def local_state(self) -> LocalState:
        row = self.connection.execute(
            "SELECT library_view, current_folder_path, current_item_id, volume, "
            "playback_rate FROM local_state WHERE singleton = 1"
        ).fetchone()
        if row is None:
            return LocalState("Foldery", None, None, 100, 1.0)
        return LocalState(
            library_view=row["library_view"],
            current_folder_path=row["current_folder_path"],
            current_item_id=row["current_item_id"],
            volume=row["volume"],
            playback_rate=row["playback_rate"],
        )

    def folder_sources(self) -> list[FolderSource]:
        """Zrodla folderowe w kolejnosci z C#: display_name, potem path."""
        rows = self.connection.execute(
            "SELECT id, ordinal, path, display_name, resume_mode FROM folder_sources"
        ).fetchall()
        sources = [
            FolderSource(
                id=str(row["id"]),            # ID zostaje napisem
                ordinal=int(row["ordinal"]),
                path=row["path"],
                display_name=row["display_name"],
                resume_mode=int(row["resume_mode"]),
            )
            for row in rows
        ]
        sources.sort(key=lambda s: (_ci_key(s.display_name), s.path.lower()))
        return sources

    def active_items_under(
        self, folder: str | None, limit: int | None = None
    ) -> list[LibraryItem]:
        sql = (
            "SELECT id, title, path, duration_ticks, is_favorite, is_available, "
            f"is_in_library, is_radio_recording FROM local_items WHERE {_ACTIVE}"
        )
        args: list[object] = []
        if folder:
            norm = normalize_path(folder)
            sql += " AND (path = ? COLLATE NOCASE OR path LIKE ? ESCAPE '\\')"
            args += [norm, _like_prefix(norm)]
        sql += " ORDER BY title COLLATE AMC_PL"
        if limit is not None:
            sql += " LIMIT ?"
            args.append(limit)
        return [_item(row) for row in self.connection.execute(sql, args)]


def _like_prefix(folder: str) -> str:
    escaped = folder.replace("\\", "\\\\").replace("%", "\\%").replace("_", "\\_")
    return escaped + "\\\\%"


def _item(row: sqlite3.Row) -> LibraryItem:
    return LibraryItem(
        id=str(row["id"]),                    # NAPIS, nie liczba
        title=row["title"],
        path=row["path"],
        duration_ticks=int(row["duration_ticks"]),
        is_favorite=bool(row["is_favorite"]),
        is_available=bool(row["is_available"]),
        is_in_library=bool(row["is_in_library"]),
        is_radio_recording=bool(row["is_radio_recording"]),
    )


# ----------------------------------------------------------------- wiersze listy

def _format_detail(item: LibraryItem) -> str:
    """Kolumna "Szczegoly": czas (gdy ZNANY) oraz znaczniki wiersza.

    Brak danych o czasie oddajemy jako BRAK, nie jako ``"0:00"``. To wzorzec
    z oryginalu, nie nasza decyzja -- ``MediaItemFormatter.FieldValue``
    (``Presentation/MediaItemFormatter.cs:75-82``)::

        MediaItemField.Duration when item.Duration > TimeSpan.Zero
            => FormatDuration(item.Duration),
        _ => null

    Warunek ``> TimeSpan.Zero`` jest CZESCIA wzorca: niedodatni czas daje
    ``null``, a ``Format`` (54) pomija wartosci puste. Oryginal nigdy nie
    wypisuje wiec zerowej dlugosci. Czytnik mowil "0:00" przy 2475 i 54
    pozycjach profilu -- czyli nazywal brak pomiaru utworem dlugosci zero.

    Same znaczniki ZOSTAJA przy braku czasu: "ulubione" to osobna, znana
    informacja i nie ma powodu jej tracic.
    """
    marks = []
    if item.duration_ticks > 0:
        total = int(item.duration_seconds)
        hours, rest = divmod(total, 3600)
        minutes, seconds = divmod(rest, 60)
        # Ten sam format co ``FormatDuration`` (69-73): h:mm:ss albo m:ss.
        marks.append(
            f"{hours}:{minutes:02d}:{seconds:02d}" if hours else f"{minutes}:{seconds:02d}"
        )
    if item.is_favorite:
        marks.append("ulubione")
    if item.is_radio_recording:
        marks.append("nagranie radia")
    return ", ".join(marks)


def _folder_row(path: str, name: str) -> Row:
    return Row(item_id="dir:" + path, title=name, kind="folder", path=path)


def _track_row(item: LibraryItem) -> Row:
    return Row(
        item_id=item.id,
        title=item.title,
        kind="track",
        path=item.path,
        detail=_format_detail(item),
    )


def folder_rows(db: LibraryDatabase, current_folder: str | None) -> list[Row]:
    """Port ``MainWindow.CreateFolderRows`` na dane SQLite.

    Korzen: ``folder_sources`` + aktywne rekordy, ktore nie naleza do zadnego
    zrodla (inaczej 7 plikow z profilu byloby nieosiagalne). Wejscie w folder:
    bezposrednie podfoldery wyliczone ze SCIEZEK rekordow -- nie z dysku, bo
    dysku tu nie ma.
    """
    sources = db.folder_sources()

    if not current_folder:
        rows = [_folder_row(s.path, s.display_name) for s in sources]
        orphans = [
            item
            for item in db.active_items_under(None)
            if not any(is_same_or_descendant(item.path, s.path) for s in sources)
        ]
        orphans.sort(key=lambda i: _ci_key(i.title))
        rows.extend(_track_row(item) for item in orphans)
        return rows

    # Najbardziej szczegolowe zrodlo zawierajace biezaca sciezke.
    root = max(
        (s for s in sources if is_same_or_descendant(current_folder, s.path)),
        key=lambda s: len(s.path),
        default=None,
    )
    if root is None:
        return folder_rows(db, None)

    current = normalize_path(current_folder)
    child_folders: dict[str, str] = {}
    direct_files: list[LibraryItem] = []
    for item in db.active_items_under(current):
        parent = ntpath.dirname(normalize_path(item.path))
        if parent.lower() == current.lower():
            direct_files.append(item)
            continue
        remainder = normalize_path(item.path)[len(current) :].lstrip("\\/")
        head = remainder.split("\\")[0].split("/")[0]
        if not head:
            continue
        child_path = current + "\\" + head
        child_folders.setdefault(child_path.lower(), (child_path, head))  # type: ignore[arg-type]

    rows = [
        _folder_row(path, name)
        for path, name in sorted(
            child_folders.values(), key=lambda pair: _ci_key(pair[1])  # type: ignore[index]
        )
    ]
    direct_files.sort(key=lambda i: _ci_key(i.title))
    rows.extend(_track_row(item) for item in direct_files)
    return rows


def breadcrumb_rows(db: LibraryDatabase, current_folder: str) -> list[Row]:
    """Wiersz ".." -- powrot do rodzica albo do korzenia Biblioteki."""
    sources = db.folder_sources()
    if any(normalize_path(current_folder).lower() == normalize_path(s.path).lower()
           for s in sources):
        return [Row(item_id="parent:", title="..", kind="parent", path=None)]
    parent = parent_path(current_folder)
    if parent is None:
        return []
    return [Row(item_id="parent:" + parent, title="..", kind="parent", path=parent)]
