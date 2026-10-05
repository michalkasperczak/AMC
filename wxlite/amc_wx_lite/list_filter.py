"""Filtrowanie listy -- regula dopasowania i stan filtra. BEZ ``wx``.

KONTRAKT PRZENIESIONY ZE ZRODEL pelnego AMC (odczytany, nie wymyslony):

* ``MainWindow.xaml.cs:13762-13778`` ``ApplyFilter``
  ``var query = FilterBox.Text.Trim();``
  pusty/bialy => CALA lista (``_unfilteredItems``), inaczej
  ``row.Label.Contains(query, StringComparison.CurrentCultureIgnoreCase)``.
  Czyli: PODCIAG (nie prefiks), bez wielkosci liter, po NAZWIE wiersza.
* ``MainWindow.xaml.cs:23650-23669`` ``FilterBox_TextChanged``
  filtr zapisany PER WIDOK (``navigation.Filters[_currentView]``), status
  ``Wyniki filtrowania: {n}`` albo ``Gotowy``.
* ``MainWindow.xaml.cs:10196-10204`` ``RestoreFilterForCurrentView``
  wejscie w widok PRZYWRACA jego wlasny tekst filtra.

CZEGO TEN MODUL NIE ROBI I DLACZEGO:

* nie dotyka ``ListModel`` ani ``list_sync``. Mechanizm list (tozsamosc po
  ``item_id``, przyrostowy plan, brak ``LC_VIRTUAL``) jest ODEBRANY i zostaje
  bez zmian. Filtr liczy, ktore wiersze sa WIDOCZNE; zrodlem prawdy o pelnym
  zbiorze pozostaje model.
* nie sortuje. ``Where`` w C# zachowuje kolejnosc, a widoki Kolejki i
  Zapisanej kolejki MAJA znaczaca kolejnosc, ktorej nie wolno przeliczyc.
* nie filtruje po sciezce ani adresie. C# porownuje ``row.Label``, czyli to,
  co uzytkownik widzi. Trafienie po niewidocznej sciezce bylo by wynikiem bez
  wytlumaczenia.
"""

from __future__ import annotations

from dataclasses import dataclass, field
from typing import Sequence

from .list_model import Row

#: Rodzaje wierszy, ktorych filtr NIE usuwa niezaleznie od zapytania.
#: ``parent`` (wiersz "..") to WYJSCIE z widoku, nie dana. Ukrycie go
#: zabraloby jedyna widoczna droge w gore przy aktywnym filtrze.
_ALWAYS_VISIBLE_KINDS = ("parent",)


def normalized_query(text: str | None) -> str:
    """Zapytanie po ``Trim()`` -- dokladnie jak ``ApplyFilter`` (cs:13765)."""
    return (text or "").strip()


def matches_query(title: str, query: str) -> bool:
    """Czy nazwa wiersza zawiera zapytanie, bez wielkosci liter.

    ``casefold`` zamiast ``lower``: to regula Unicode dla porownan bez
    wielkosci liter i poprawnie obsluguje polskie znaki, ktorych w profilu
    Michala jest pelno. ``CurrentCultureIgnoreCase`` w C# dziala na kulturze
    polskiej, wiec ``casefold`` jest tu wlasciwym odpowiednikiem.
    """
    needle = normalized_query(query)
    if not needle:
        return True
    return needle.casefold() in (title or "").casefold()


def row_is_visible(row: Row, query: str | None) -> bool:
    """Czy TEN wiersz zostaje przy danym zapytaniu.

    JEDNA regula widocznosci dla calego portu: uzywa jej i ``filter_rows``
    (wiersze modelu), i ``list_sync.model_row_texts`` (stan zadany kontrolki).
    Gdyby kazde miejsce liczylo widocznosc po swojemu, filtr i lista mogly by
    sie rozjechac o jeden wiersz bez zadnego widocznego bledu.
    """
    if row.kind in _ALWAYS_VISIBLE_KINDS:
        return True
    return matches_query(row.title, query or "")


def filter_rows(rows: Sequence[Row], query: str | None) -> list[Row]:
    """Widoczne wiersze dla zapytania. Kolejnosc WIDOKU zachowana.

    Pusty filtr oddaje ten sam obiekt listy, gdy dostal liste -- dzieki temu
    ``list_sync`` widzi NIEZMIENIONE zrodlo i nie planuje zadnej operacji na
    kontrolce (ta oszczednosc jest calym sensem mechanizmu list).
    """
    needle = normalized_query(query)
    if not needle:
        return list(rows) if not isinstance(rows, list) else rows
    return [row for row in rows if row_is_visible(row, needle)]


def results_status_text(query: str | None, visible_count: int) -> str:
    """Tekst statusu po zmianie filtra (cs:23667-23669).

    Slowa oryginalu, nie wlasne: ``Gotowy`` dla pustego filtra i
    ``Wyniki filtrowania: {n}`` dla aktywnego -- takze dla zera, bo
    uzytkownik ma wiedziec, ze filtr dziala i nic nie znalazl.
    """
    if not normalized_query(query):
        return "Gotowy"
    return f"Wyniki filtrowania: {visible_count}"


#: Komunikat wejscia w pole filtra. Tresc ze ``FocusFilter``
#: (``MainWindow.xaml.cs:22214-22216``). Wariant szczegolowy zalezy w
#: oryginale od ``Settings.Messages.DetailedHints``; ten port nie ma jeszcze
#: tego przelacznika, wiec mowimy KROTKO -- tak jak AMC z wylaczonymi
#: podpowiedziami (``AppSettings.cs:542``: ``DetailedHints`` domyslnie false).
FILTER_ENTERED_MESSAGE = "Filtr listy"

#: Pusty wynik: fokus ZOSTAJE w polu filtra, a komunikat mowi, co robic
#: (``FocusFilterResults``, ``MainWindow.xaml.cs:22202-22206``). Slowa z
#: oryginalu, zeby uzytkownik slyszal to samo zdanie w obu programach.
NO_RESULTS_MESSAGE = (
    "Brak wyników filtrowania. Zmień tekst lub naciśnij Escape, aby wyczyścić filtr"
)

#: Escape z niepustym filtrem (``ReturnToMediaListFromEscape``, cs:22556).
FILTER_CLEARED_MESSAGE = "Filtr wyczyszczony"


@dataclass(slots=True)
class FilterState:
    """Tekst filtra PER WIDOK -- odpowiednik ``SessionNavigationState.Filters``.

    Kluczem jest TOZSAMOSC WIDOKU, ta sama, ktorej uzywa juz
    ``navigation.view_context`` (sesja, widok Biblioteki, playlista, element,
    sciezka folderu). Nie wprowadzamy drugiego systemu identyfikacji widokow:
    gdyby klucz byl inny niz ten, po ktorym kontrolka rozpoznaje zmiane
    widoku, filtr i lista rozjechalyby sie po cichu.
    """

    _by_view: dict[tuple, str] = field(default_factory=dict)

    def text_for_view(self, context: tuple) -> str:
        """Filtr TEGO widoku. Widok nieodwiedzony ma pusty -- nie dziedziczy."""
        return self._by_view.get(context, "")

    def set_for_view(self, context: tuple, text: str | None) -> None:
        value = text or ""
        if not value:
            self._by_view.pop(context, None)
            return
        self._by_view[context] = value

    def clear_for_view(self, context: tuple) -> None:
        """Wyczysc filtr JEDNEGO widoku. Pozostale zostaja bez zmian."""
        self._by_view.pop(context, None)

    def has_text_for_view(self, context: tuple) -> bool:
        return bool(normalized_query(self._by_view.get(context)))
