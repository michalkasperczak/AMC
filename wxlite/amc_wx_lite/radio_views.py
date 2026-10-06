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

from .library_activity import (
    MAX_HISTORY_ENTRIES_PER_SESSION,
    _distinct_ordinal,
    _stored_ids,
)
from .library_db import LibraryDatabase
from .list_model import Row, rows_from_stations
from .radio_source import (
    SCOPE_ALL,
    SCOPE_FAVORITES,
    SCOPE_LIBRARY,
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

    @property
    def is_empty(self) -> bool:
        return not self.rows


def load_view(
    source: RadioSource,
    view: str,
    *,
    db: LibraryDatabase | None = None,
) -> RadioViewResult:
    """Wczytaj jeden widok radia.

    ``view`` to ``"library"`` / ``"favorites"`` / ``"history"``.

    ``db`` dotyczy tylko Historii. Gdy caller poda otwarte polaczenie, uzywamy
    go i NIE zamykamy -- wlascicielem uchwytu zostaje ten, kto go otworzyl.
    Gdy ``db`` jest ``None``, otwieramy baze z ``source.layout.library_db``
    na czas odczytu i zamykamy ja sami.
    """
    if view not in RADIO_VIEWS:
        raise ValueError(f"Nieznany widok radia: {view}")
    if view == VIEW_HISTORY:
        return _history_view(source, db=db)
    scope = SCOPE_FAVORITES if view == VIEW_FAVORITES else SCOPE_LIBRARY
    return _station_view(source, view=view, scope=scope)


# ------------------------------------------------ 1-2. Biblioteka i Ulubione


def _station_view(source: RadioSource, *, view: str, scope: str) -> RadioViewResult:
    """Wiersze stacji jednego zakresu cache'u radia.

    ``rows_from_stations`` jest tu REUZYTE, nie przepisane: adres idzie
    wylacznie do ``Row.url`` (Ctrl+Shift+C), a ``show_kind=False`` zdejmuje
    slowo ,,stacja'' z kazdego wiersza jednorodnej listy -- czytnik nie powtarza
    rodzaju 165 razy.
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
    rows = rows_from_stations(snapshot.as_payload())
    return RadioViewResult(
        rows=rows,
        heading=heading,
        # Udany odczyt z ratowana poprzednia lista: wiersze SA, ale powod
        # trzeba powiedziec, bo dane moga byc nieaktualne.
        unavailable_reason=snapshot.load_error,
        current_id=snapshot.current_id,
    )


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
