"""Model listy: wiersze i wybor po STABILNYM identyfikatorze.

Zasada: model NIE przechowuje widgetow. Trzyma wiersze i wybor po stabilnym
``item_id``, zeby po odswiezeniu folderu fokus nie skakal na pozycje 0.

``text_for`` oddaje tekst komorki. Dawniej odpowiadal na ``OnGetItemText``
wirtualnej kontrolki; teraz jest zrodlem tekstu dla ZWYKLEJ listy -- czyta go
``list_sync.model_row_texts`` i porownuje z tym, co kontrolka juz pokazuje.
Semantyka kolumn sie NIE zmienila, zmienilo sie tylko to, kiedy tekst trafia
do kontrolki (raz przy zmianie, nie przy kazdym malowaniu).

Kod celowo bez importu wx: dzieki temu da sie go przetestowac w WSL bez pulpitu.
"""

from __future__ import annotations

from dataclasses import dataclass, field
from typing import Iterable, Sequence


#: Slowa rodzaju przeniesione ZE ZRODEL pelnego AMC, nie wymyslone:
#: ``src/AccessibleMediaController.Core/Sessions/MediaItem.cs:69-77``
#: (``KindLabel``). Kolumna "Rodzaj" mowi RODZAJ, nigdy adres ani sciezke.
#: ``parent`` (wiersz "..") celowo nie ma slowa: wejscie do folderu wyzej nie
#: jest rodzajem medium, a falszywy rodzaj bylby klamstwem wobec czytnika.
KIND_LABELS: dict[str, str] = {
    "folder": "folder",
    "track": "utwór",
    "station": "stacja",
    # Playlista to POJEMNIK, nie plik: Enter ma w nia wejsc. Bez tego slowa
    # czytnik mowilby w kolumnie "Rodzaj" pustke i nie bylo by roznicy miedzy
    # playlista a utworem.
    "playlist": "playlista",
    "parent": "",
}


@dataclass(frozen=True, slots=True)
class Row:
    """Jeden wiersz listy. ``item_id`` musi byc trwaly miedzy odswiezeniami."""

    item_id: str
    title: str
    kind: str  # "folder" | "track" | "station" | "playlist" | "parent"
    path: str | None = None
    url: str | None = None
    detail: str = ""
    # Jednorodny widok moze pominac rodzaj bez zmiany danych elementu.
    show_kind: bool = True
    # Uczciwa odmowa po Enterze dla wiersza, ktory ma pozostac widoczny, ale
    # nie ma czego uruchomic (np. nieudane nagranie albo brakujacy plik).
    # To tekst dla uzytkownika, nigdy techniczny identyfikator lub repr.
    activation_message: str | None = None
    # Krotki, dynamiczny stan elementu (np. odtwarzanie albo nagrywanie).
    # Jest osobny od trwalego ``detail``, zeby odswiezenie statusu moglo go
    # wymienic bez zgubienia zwyklych informacji wiersza. Pole zawiera juz
    # tekst uzytkowy -- nigdy enum, identyfikator ani reprezentacje obiektu.
    state_detail: str = ""

    @property
    def is_openable(self) -> bool:
        """Czy Enter ma WEJSC w element, zamiast go odtworzyc."""
        return self.kind in ("folder", "parent", "playlist")

    @property
    def kind_label(self) -> str:
        """Slowo rodzaju dla kolumny "Rodzaj". Nieznany rodzaj = puste."""
        return KIND_LABELS.get(self.kind, "")

    @property
    def address(self) -> str:
        """Adres albo sciezka NA ZADANIE (Ctrl+Shift+C), nie do odczytu listy.

        Odpowiednik ``CopyActionItemLocation`` z
        ``MainWindow.xaml.cs:20725-20732``: w AMC pelny adres dostaje sie
        osobnym skrotem, bo czytany przy kazdym wierszu jest nie do sluchania.
        """
        return (self.url or self.path or "").strip()


@dataclass(slots=True)
class ListModel:
    """Wiersze + wybor. Jedno zrodlo prawdy dla widoku wirtualnego."""

    rows: list[Row] = field(default_factory=list)
    _selected_id: str | None = None

    def __len__(self) -> int:
        return len(self.rows)

    @property
    def selected_id(self) -> str | None:
        return self._selected_id

    @property
    def selected_index(self) -> int:
        """Pozycja wyboru albo -1. Liczona z ID, nie pamietana osobno."""
        if self._selected_id is None:
            return -1
        for index, row in enumerate(self.rows):
            if row.item_id == self._selected_id:
                return index
        return -1

    @property
    def selected_row(self) -> Row | None:
        index = self.selected_index
        return self.rows[index] if index >= 0 else None

    def row_at(self, index: int) -> Row | None:
        if 0 <= index < len(self.rows):
            return self.rows[index]
        return None

    def text_for(self, index: int, column: int = 0) -> str:
        """Tekst komorki NA ZADANIE, osobna tresc dla KAZDEJ kolumny.

        Dlaczego nie jedno ``row.detail`` dla wszystkich kolumn poza zerowa:
        ``MediaListCtrl`` ma trzy kolumny, wiec czytnik ekranu wymawial te sama
        wartosc dwa razy -- na stacji radiowej byl to pelny adres strumienia,
        raz jako "Rodzaj", raz jako "Szczegoly". Zmierzone zywym NVDA.

        Semantyka przeniesiona ze zwyklego AMC:

        * 0 "Nazwa"     -- ``MediaItem.Title`` / ``PrimaryText``.
        * 1 "Rodzaj"    -- ``MediaItem.KindLabel`` (slowo, nie adres).
        * 2 "Szczegoly" -- krotki dodatek wiersza; PUSTY, gdy powtarzalby
          nazwe albo rodzaj. ``MediaItemFormatter.Format`` pomija wartosci
          powtorzone (``spokenValues.Add``), wiec i my nie dublujemy.

        Poza zakresem (wiersz albo kolumna) zwracamy puste, nie wyjatek: wx
        potrafi zapytac o wiersz w trakcie zmiany dlugosci listy.
        """
        row = self.row_at(index)
        if row is None:
            return ""
        if column == 0:
            return row.title
        if column == 1:
            return row.kind_label if row.show_kind else ""
        if column == 2:
            details = [
                value.strip()
                for value in (row.detail, row.state_detail)
                if value and value.strip()
            ]
            detail = ", ".join(dict.fromkeys(details))
            # Powtorzenie nazwy albo rodzaju to dokladnie ten podwojny odczyt,
            # ktory zglosil uzytkownik. Adres tu NIE wchodzi: jest pod skrotem.
            if not detail or detail in (row.title, row.kind_label) or detail == row.address:
                return ""
            return detail
        return ""

    def select_index(self, index: int) -> bool:
        row = self.row_at(index)
        if row is None:
            return False
        self._selected_id = row.item_id
        return True

    def select_id(self, item_id: str | None) -> bool:
        if item_id is None:
            self._selected_id = None
            return True
        if any(row.item_id == item_id for row in self.rows):
            self._selected_id = item_id
            return True
        return False

    def replace(
        self,
        rows: Iterable[Row],
        *,
        keep_selection: bool = True,
        preferred_id: str | None = None,
    ) -> None:
        """Podmien zawartosc listy.

        ``preferred_id`` ma pierwszenstwo (np. wracamy do folderu, z ktorego
        wyszlismy przez Backspace i chcemy stanac na nim). Gdy nie ma ani jego,
        ani poprzedniego wyboru, stajemy na pierwszym wierszu, ktory NIE jest
        wejsciem do rodzica - inaczej kazde odswiezenie konczy sie na "..".
        """
        previous = self._selected_id
        self.rows = list(rows)

        for candidate in (preferred_id, previous if keep_selection else None):
            if candidate is not None and self.select_id(candidate):
                return

        self._selected_id = None
        for row in self.rows:
            if row.kind != "parent":
                self._selected_id = row.item_id
                return
        if self.rows:
            self._selected_id = self.rows[0].item_id

    def find_prefix(self, prefix: str, start_index: int = 0) -> int:
        """Szukanie po pierwszej literze, zawijane. Natywny ListCtrl robi to sam
        dla zwyklych list, ale w trybie wirtualnym musimy podac wynik sami."""
        if not prefix or not self.rows:
            return -1
        needle = prefix.casefold()
        count = len(self.rows)
        for offset in range(1, count + 1):
            index = (start_index + offset) % count
            if self.rows[index].title.casefold().startswith(needle):
                return index
        return -1


def rows_from_folder_payload(payload: dict, *, include_parent: bool = True) -> list[Row]:
    """Zamien odpowiedz hosta ``files.listFolder`` na wiersze listy.

    ``detail`` zostaje PUSTE: slowo rodzaju nalezy do kolumny "Rodzaj"
    (``Row.kind_label``). Wczesniej szlo tu "folder", wiec kolumny Rodzaj i
    Szczegoly mowily to samo slowo -- znow podwojny odczyt, tylko krotszy.
    """
    rows: list[Row] = []
    parent = payload.get("parent")
    if include_parent and parent:
        rows.append(Row(item_id=f"parent:{parent}", title="..", kind="parent", path=parent))
    for entry in payload.get("items", ()):
        kind = entry.get("kind", "track")
        rows.append(
            Row(
                item_id=str(entry.get("id") or entry.get("path") or entry.get("title", "")),
                title=str(entry.get("title", "")),
                kind=kind,
                path=entry.get("path"),
            )
        )
    return rows


def rows_from_queue_status(payload: dict) -> list[Row]:
    """Wiersze ZYWEJ kolejki z odpowiedzi hosta ``queue.status``.

    Host oddaje wiersze w kolejnosci SESJI (``QueueItemIds``), czyli w tym, co
    zostalo do odtworzenia -- skonsumowane pozycje juz w niej nie wracaja.
    Niczego tu nie sortujemy i nie dokladamy: widok ma byc tym, czym jest
    kolejka, a nie jej poprawiona wersja.

    ``playNext`` idzie do ``detail``, bo blok "odtworz nastepne" to jedyna
    roznica miedzy wierszami, ktorej sama nazwa nie powie. ``current`` NIE
    trafia do tekstu wiersza: biezacy material ma wlasne miejsce w oknie, a
    powtarzanie go w liscie bylo by tym drugim odczytem tego samego.

    ``path`` jest tu PUSTA i tak ma byc: ``QueuePayload`` jej nie oddaje
    (``LiteEngineHandlers.QueuePayload`` -> ``id``/``title``/``playNext``/
    ``current``). Enter w tym widoku nie potrzebuje sciezki, bo nie sklada
    kolejki od nowa -- woła ``queue.playAt`` po samym Id, a material zna host.
    """
    rows: list[Row] = []
    for entry in payload.get("rows", ()) or ():
        item_id = str(entry.get("id") or "")
        if not item_id:
            # Wiersz bez tozsamosci nie da sie ani wybrac, ani odtworzyc.
            continue
        rows.append(
            Row(
                item_id=item_id,
                title=str(entry.get("title") or ""),
                kind="track",
                path=entry.get("path"),
                detail="odtwórz następne" if entry.get("playNext") else "",
            )
        )
    return rows


def rows_from_stations(stations: Sequence[dict]) -> list[Row]:
    """Wiersze dla sesji radiowej.

    Adres trafia WYLACZNIE do ``url`` (czyli pod ``Row.address`` i skrot
    Ctrl+Shift+C). Do ``detail`` NIE wchodzi: wielki adres strumienia czytany
    przy kazdym wierszu to zglaszany podwojny, nieczytelny odczyt.
    """
    return [
        Row(
            item_id=str(station["id"]),
            title=str(station.get("name", "")),
            kind="station",
            url=str(station.get("url", "")),
            show_kind=False,
        )
        for station in stations
    ]
