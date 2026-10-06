"""Warstwa DANYCH trzech widokow Radia: Biblioteka, Ulubione, Historia.

Co ten modul robi
-----------------
Jedno wejscie ``load_view(source, view)`` oddaje gotowe wiersze listy dla
jednego z trzech zakresow radia. Nic wiecej: zadnego ``wx``, zadnej nawigacji,
zadnego menu. Dzieki temu caly modul da sie przetestowac w WSL bez pulpitu --
i dokladnie tak jest testowany (``tests/test_radio_view_data.py``).

Skad biora sie dane
-------------------
* **Biblioteka** i **Ulubione** -- z cache'u ``radio.stations`` profilu AMC,
  przez ISTNIEJACY ``RadioSource.load(scope=...)``. Zakres dolozono do zrodla,
  a nie tutaj, zeby obsluga bledow odczytu (brak pliku, uszkodzony JSON, plik
  zajety przez AMC) zostala w JEDNYM miejscu.
* **Historia** -- zapisana tabela ``playback_history`` SQLite dla sesji
  ``radio``, czytana przez ISTNIEJACY ``LibraryDatabase`` (``mode=ro``) i
  istniejace ``library_activity._stored_ids`` / ``_distinct_ordinal``. Katalog
  do rozwiniecia Id to CALY cache radia (``scope="all"``), nie Biblioteka:
  historia odpowiada na pytanie ,,czego sluchalem'', a nie ,,co mam''. Stacja
  zagrana z katalogu Radio Browser i nigdy nie dodana do Biblioteki ma zostac
  w historii.

Czego ten modul NIE robi
------------------------
* Nie zapisuje niczego. Baza jest ``mode=ro``, profil AMC jest czytany, a
  prywatny profil nie jest TWORZONY -- pokazanie wiersza nie ustanawia
  czlonkostwa ani nie dopisuje historii.
* Nie udaje pustej kolekcji przy bledzie. Blad odczytu JSON/bazy wychodzi jako
  ``unavailable_reason``, bo niewidomy uzytkownik slyszy w obu przypadkach to
  samo -- cisze -- a reakcji wymaga tylko jeden z nich.
* Nie trimuje i nie prefiksuje Id. Id z ``playback_history`` jest dopasowywane
  do Id stacji DOKLADNIE (``Ordinal``); zapisane Id, ktorego nie ma w cache'u,
  nie dostaje wiersza-placeholdera, ale jest LICZONE w ``missing_item_count``,
  zeby brak nie wygladal jak utrata danych.
"""

from __future__ import annotations

from dataclasses import dataclass, field
from pathlib import Path

from .collation import COLLATION_TITLE_IGNORE_CASE
from .library_activity import (
    MAX_HISTORY_ENTRIES_PER_SESSION,
    _distinct_ordinal,
    _stored_ids,
)
from .library_db import LibraryDatabase
from .library_views import _stored_order
from .list_model import Row, rows_from_stations
from .radio_source import (
    SCOPE_ALL,
    SCOPE_FAVORITES,
    SCOPE_LIBRARY,
    SORT_ADDED_NEWEST,
    SORT_ALPHABETICAL,
    SORT_CUSTOM,
    RadioSource,
)

#: Sesja radia w ``playback_history``. Klucz sesji dopasowuje ``_stored_ids``
#: bez wzgledu na wielkosc liter (``COLLATE NOCASE``), tak jak C#.
RADIO_SESSION = "radio"

#: Nazwy widokow. Rodzic podaje ``enum.value`` swojego enuma nawigacji -- ten
#: modul CELOWO nie importuje ``navigation``, zeby warstwa danych nie zalezala
#: od warstwy okna.
VIEW_LIBRARY = "library"
VIEW_FAVORITES = "favorites"
VIEW_HISTORY = "history"
RADIO_VIEWS = (VIEW_LIBRARY, VIEW_FAVORITES, VIEW_HISTORY)

#: Nazwy widokow po stronie AMC. ``CollectionSortModes`` jest kluczowane
#: NAZWA WIDOKU C# (``MainWindow.xaml.cs:13196``: ``_currentView``), nie naszym
#: ``enum.value``, wiec tlumaczymy to tutaj JAWNIE. Klucze juz w ``casefold``,
#: bo slownik C# jest ``OrdinalIgnoreCase``.
_AMC_VIEW_NAME = {
    VIEW_LIBRARY: "biblioteka",
    VIEW_FAVORITES: "ulubione",
}

#: Widok -> (tabela kolejnosci wlasnej, tabela kolejnosci dodania).
#: ``EnsureCollectionOrder`` (``MainWindow.xaml.cs:13271-13302``) wybiera
#: ``Favorite*ItemIdsBySession`` dla "Ulubione" i ``Library*ItemIdsBySession``
#: dla pozostalych; ``LocalLibraryDatabase.cs:232-259`` mapuje te slowniki na
#: tabele ``favorite_order`` / ``favorite_added_order`` i
#: ``library_custom_order`` / ``library_added_order``.
_ORDER_TABLES = {
    VIEW_LIBRARY: ("library_custom_order", "library_added_order"),
    VIEW_FAVORITES: ("favorite_order", "favorite_added_order"),
}

_HEADINGS = {
    VIEW_LIBRARY: "Radio — Biblioteka",
    VIEW_FAVORITES: "Radio — Ulubione",
    VIEW_HISTORY: "Radio — Historia odtwarzania",
}


@dataclass(frozen=True, slots=True)
class RadioViewResult:
    """Wynik jednego odczytu widoku radia.

    ``unavailable_reason`` to jawne zdanie ,,tego nie da sie pokazac i dlaczego''.
    Jest ``None`` TYLKO wtedy, gdy odczyt faktycznie sie udal -- pusta lista z
    ``unavailable_reason=None`` znaczy ,,naprawde nic tu nie ma'', i to jest
    rozroznienie, ktorego pusta lista sama nie unosi.
    """

    rows: list[Row]
    heading: str
    unavailable_reason: str | None = None
    #: Zapisane wpisy historii bez odpowiednika w cache'u stacji. Wierszy dla
    #: nich NIE tworzymy (oryginal tez nie), ale liczba jest jawna.
    missing_item_count: int = 0
    #: Id biezacej stacji z profilu, gdy zrodlo je wskazuje.
    current_id: str | None = None
    #: ``False`` gdy baza historii jest zamrozona migawka, nie zywym profilem.
    sees_live_writes: bool = True
    #: ``False`` gdy kolejnosc jest ZASTEPCZA, nie ta z AMC (np. brak zapisu
    #: kolejnosci albo brak kluczy hosta dla sortu alfabetycznego). Znaczy
    #: dokladnie to samo, co w ``library_views.LibraryViewResult`` -- lista i
    #: tak jest oddawana, bo brak OPCJONALNEGO sortu nie jest powodem, zeby
    #: zabrac uzytkownikowi wszystkie stacje.
    order_matches_amc: bool = True
    #: Nazwa trybu, w ktorym kolejnosc zostala policzona. Tryb jest CZYTANY z
    #: profilu AMC, nie wybierany tutaj.
    sort_mode: str = SORT_ADDED_NEWEST

    @property
    def is_empty(self) -> bool:
        return not self.rows


def load_view(
    source: RadioSource,
    view: str,
    *,
    db: LibraryDatabase | None = None,
    collation=None,
) -> RadioViewResult:
    """Wczytaj jeden widok radia.

    ``view`` to ``"library"`` / ``"favorites"`` / ``"history"``.

    ``db`` dotyczy Historii ORAZ zapisanej kolejnosci Biblioteki i Ulubionych
    (tabele ``*_order`` leza w tej samej bazie). Gdy caller poda otwarte
    polaczenie, uzywamy go i NIE zamykamy -- wlascicielem uchwytu zostaje ten,
    kto go otworzyl. Gdy ``db`` jest ``None``, otwieramy baze z
    ``source.layout.library_db`` na czas odczytu i zamykamy ja sami.

    ``collation`` to ``collation.HostCollation`` -- potrzebna TYLKO wtedy, gdy
    profil ma zapisany tryb ``Alphabetical``. Bez niej kolejnosc alfabetyczna
    jest zastepcza i widok mowi o tym przez ``order_matches_amc=False``,
    zamiast udawac zgodnosc z ``CompareInfo`` pl-PL.
    """
    if view not in RADIO_VIEWS:
        raise ValueError(f"Nieznany widok radia: {view}")
    if view == VIEW_HISTORY:
        return _history_view(source, db=db)
    scope = SCOPE_FAVORITES if view == VIEW_FAVORITES else SCOPE_LIBRARY
    return _station_view(source, view=view, scope=scope, db=db, collation=collation)


# ------------------------------------------------ 1-2. Biblioteka i Ulubione


def _station_view(
    source: RadioSource,
    *,
    view: str,
    scope: str,
    db: LibraryDatabase | None = None,
    collation=None,
) -> RadioViewResult:
    """Wiersze stacji jednego zakresu cache'u radia, w kolejnosci Z AMC.

    ``rows_from_stations`` jest tu REUZYTE, nie przepisane: adres idzie
    wylacznie do ``Row.url`` (Ctrl+Shift+C), a ``show_kind=False`` zdejmuje
    slowo ,,stacja'' z kazdego wiersza jednorodnej listy -- czytnik nie powtarza
    rodzaju 165 razy.

    Kolejnosc nie jest kolejnoscia cache'u. Oryginal przepuszcza OBA te widoki
    przez ``OrderCurrentCollection`` (``MainWindow.xaml.cs:13246-13269``), wiec
    tutaj dzieje sie to samo: tryb z profilu, zapis z ``library.db``.
    """
    snapshot = source.load(scope=scope)
    heading = _HEADINGS[view]
    # Kolejnosc sprawdzen: najpierw BLAD odczytu, potem brak danych tego
    # rodzaju. Oba konczyly sie wczesniej pusta lista, wiec oba musza miec
    # wlasne zdanie.
    if snapshot.load_error and not snapshot.stations:
        return RadioViewResult(
            rows=[], heading=heading, unavailable_reason=snapshot.load_error
        )
    if snapshot.unavailable_reason:
        return RadioViewResult(
            rows=[], heading=heading, unavailable_reason=snapshot.unavailable_reason
        )

    # Tryb CZYTAMY z profilu -- wlasnego trybu ten port nie wprowadza. Brak
    # klucza = ``AddedNewest`` (``MainWindow.xaml.cs:13193-13196``).
    mode = snapshot.collection_sort_modes.get(
        _AMC_VIEW_NAME[view], SORT_ADDED_NEWEST
    )
    payload = snapshot.as_payload()
    ordered, matches = _ordered_payload(
        payload, view=view, mode=mode, source=source, db=db, collation=collation
    )
    return RadioViewResult(
        rows=rows_from_stations(ordered),
        heading=heading,
        # Udany odczyt z ratowana poprzednia lista: wiersze SA, ale powod
        # trzeba powiedziec, bo dane moga byc nieaktualne.
        unavailable_reason=snapshot.load_error,
        current_id=snapshot.current_id,
        order_matches_amc=matches,
        sort_mode=mode,
    )


def _ordered_payload(
    payload: list[dict],
    *,
    view: str,
    mode: str,
    source: RadioSource,
    db: LibraryDatabase | None,
    collation,
) -> tuple[list[dict], bool]:
    """Uloz wpisy zgodnie z ZAPISANYM trybem. Nigdy nie gubi wpisu.

    Zwraca ``(wpisy, czy_zgodne_z_AMC)``. Gdy zapisu kolejnosci nie da sie
    odczytac, oddajemy wpisy w kolejnosci cache'u i ``False`` -- tak samo jak
    ``library_views.LibraryViewResult``. Zabranie calej listy z powodu braku
    OPCJONALNEGO sortu byloby gorsze niz kolejnosc zastepcza, bo niewidomy
    uzytkownik slyszalby wtedy pusta Biblioteke.
    """
    if mode == SORT_ALPHABETICAL:
        return _alphabetical_payload(payload, collation)

    custom, added = _ORDER_TABLES[view]
    table = custom if mode == SORT_CUSTOM else added
    stored = _read_order(source, table, db=db)
    if stored is None:
        return payload, False
    ordered = _manual_order_payload(payload, stored)
    if mode != SORT_CUSTOM:
        # ``AddedNewest`` = ``Order(added)`` i ``.Reverse()``
        # (``MainWindow.xaml.cs:13263-13267``).
        ordered.reverse()
    return ordered, True


def _read_order(
    source: RadioSource, table: str, *, db: LibraryDatabase | None
) -> list[str] | None:
    """Zapisana kolejnosc z ``library.db``; ``None`` gdy nie da sie odczytac.

    REUZYWA ``library_views._stored_order`` (jedno zapytanie, whitelista nazw
    tabel) -- nie ma tu drugiego, surowego SQL-a. Uchwyt podany przez callera
    NIE jest zamykany.
    """
    owns_db = db is None
    try:
        if db is None:
            try:
                db = LibraryDatabase(Path(source.layout.library_db))
            except Exception:
                # Brak bazy / odmowa zywego odczytu. Lista zostaje, kolejnosc
                # jest zastepcza i powiemy to przez ``order_matches_amc``.
                return None
        try:
            return _stored_order(db, table, RADIO_SESSION)
        except Exception:
            # sqlite3.Error: brak tabeli kolejnosci, uszkodzona baza.
            return None
    finally:
        if owns_db and db is not None:
            db.close()


def _manual_order_payload(payload: list[dict], stored: list[str]) -> list[dict]:
    """``LocalLibraryManualOrder.Order`` + ``Normalize`` na wpisach cache'u.

    Ten sam porzadek, co ``library_views._manual_order``, tylko nad slownikami
    cache'u radia zamiast nad ``LibraryItem``: pozycja = indeks PIERWSZEGO
    wystapienia Id w zapisie, Id nieznane zapisowi laduje na koncu (to robi
    ``Normalize``, ``LocalLibraryManualOrder.cs:64-87``), remis rozstrzyga
    indeks wejsciowy. Zapisane Id, ktorego nie ma w cache'u, NIE tworzy
    wiersza i NIE jest nigdzie usuwane -- ``Order`` przebiega po pozycjach,
    nie po zapisie.
    """
    first: dict[str, int] = {}
    for index, item_id in enumerate(stored):
        first.setdefault(str(item_id), index)
    sentinel = len(stored) + 1
    return [
        entry
        for _, _, entry in sorted(
            (
                (first.get(str(entry["id"]), sentinel), original, entry)
                for original, entry in enumerate(payload)
            ),
            key=lambda triple: (triple[0], triple[1]),
        )
    ]


def _alphabetical_payload(
    payload: list[dict], collation
) -> tuple[list[dict], bool]:
    """``Alphabetical`` droga kluczy HOSTA, nie wlasnym collatorem.

    C#: ``OrderBy(NavigationTextForItem, CurrentCultureIgnoreCase)
    .ThenBy(Title, ...).ThenBy(Artist, ...).ThenBy(Id, Ordinal)``
    (``MainWindow.xaml.cs:13254-13259``). Dla stacji
    ``NavigationTextForItem`` sprowadza sie do ``Title``
    (``MediaItemFormatter.GetNavigationText``: pierwsze NIEPUSTE pole
    identyfikujace; stacja nie ma ``Artist``), a ``Artist`` jest pusty, wiec
    realne klucze to tytul i ``Id``.

    ``CurrentCultureIgnoreCase`` to ``COLLATION_TITLE_IGNORE_CASE``, nie
    ``AMC_PL`` -- ten sam wybor i ten sam powod, co w
    ``library_views._order_by_title_then_source``. Bez hosta NIE obiecujemy
    rownowaznosci ``str.casefold``: oddajemy kolejnosc cache'u i ``False``.
    """
    if collation is None:
        return payload, False
    titles = [str(entry.get("name") or "") for entry in payload]
    try:
        collation.load(titles, mode=COLLATION_TITLE_IGNORE_CASE)
        keys = [collation.key_for(title, mode=COLLATION_TITLE_IGNORE_CASE)
                for title in titles]
    except Exception:
        # HostCollationUnavailable / padniety most. Lista zostaje.
        return payload, False
    if any(key is None for key in keys):
        # Czesc listy zgodna, czesc nie, i nikt by tego nie zauwazyl.
        return payload, False
    ordered = [
        entry
        for _, _, entry in sorted(
            (
                (keys[index], str(entry["id"]), entry)
                for index, entry in enumerate(payload)
            ),
            key=lambda triple: (triple[0], triple[1]),
        )
    ]
    return ordered, True


# ------------------------------------------------------------- 3. Historia


def _history_view(
    source: RadioSource, *, db: LibraryDatabase | None
) -> RadioViewResult:
    heading = _HEADINGS[VIEW_HISTORY]

    # Katalog: CALY cache radia. Bez niego Id z historii nie ma nazwy ani
    # adresu, wiec katalog musi byc pierwszy -- i jego blad jest bledem widoku.
    catalog_snapshot = source.load(scope=SCOPE_ALL)
    if catalog_snapshot.load_error and not catalog_snapshot.stations:
        return RadioViewResult(
            rows=[], heading=heading, unavailable_reason=catalog_snapshot.load_error
        )
    if catalog_snapshot.unavailable_reason:
        return RadioViewResult(
            rows=[],
            heading=heading,
            unavailable_reason=catalog_snapshot.unavailable_reason,
        )

    catalog: dict[str, dict] = {}
    for entry in catalog_snapshot.as_payload():
        # ``DistinctBy(Id, Ordinal)``: pierwsze wystapienie wygrywa, kolejnosc
        # wejsciowa zostaje.
        catalog.setdefault(str(entry["id"]), entry)

    owns_db = db is None
    try:
        if db is None:
            path = Path(source.layout.library_db)
            try:
                # ``allow_snapshot_fallback`` zostaje domyslnie FALSZYWE:
                # zamrozonej migawki nie bierzemy samowolnie, bo pokazalaby
                # stan sprzed zapisow AMC jako biezaca historie.
                db = LibraryDatabase(path)
            except FileNotFoundError:
                return RadioViewResult(
                    rows=[],
                    heading=heading,
                    unavailable_reason=(
                        f"Nie znalazłem bazy Biblioteki ({path.name}), "
                        "więc historia radia jest nieznana."
                    ),
                )
            except Exception as error:  # LiveProfileReadDenied, StaleSnapshotRefused, OSError
                return RadioViewResult(
                    rows=[],
                    heading=heading,
                    unavailable_reason=(
                        f"Nie mogę odczytać historii radia: {error}"
                    ),
                )
        try:
            stored = _distinct_ordinal(
                _stored_ids(db, "playback_history", RADIO_SESSION)
            )[:MAX_HISTORY_ENTRIES_PER_SESSION]
        except Exception as error:  # sqlite3.Error: brak tabeli, uszkodzona baza
            # Pusta lista powiedzialaby ,,nic nie odtwarzales''. To nieprawda:
            # nie wiemy, bo odczyt sie nie udal.
            return RadioViewResult(
                rows=[],
                heading=heading,
                unavailable_reason=f"Nie mogę odczytać historii radia: {error}",
            )
        sees_live = bool(getattr(db, "sees_live_writes", True))
    finally:
        if owns_db and db is not None:
            db.close()

    payload: list[dict] = []
    missing = 0
    for item_id in stored:
        entry = catalog.get(item_id)
        if entry is None:
            # Id zapisane, ale cache radia go nie zna. Wiersza nie tworzymy
            # (nie ma nazwy ani adresu, Enter mogl by tylko odmowic) i NICZEGO
            # nie usuwamy z bazy -- liczymy.
            missing += 1
            continue
        payload.append(entry)
    return RadioViewResult(
        rows=rows_from_stations(payload),
        heading=heading,
        unavailable_reason=catalog_snapshot.load_error,
        missing_item_count=missing,
        current_id=catalog_snapshot.current_id,
        sees_live_writes=sees_live,
    )
