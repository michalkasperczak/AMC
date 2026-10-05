"""Filtrowanie listy: reguła dopasowania i stan filtra, bez wx.

KONTRAKT ODCZYTANY ZE ZRODEL pelnego AMC (nie wymyslony):

  src/AccessibleMediaController.Windows/MainWindow.xaml.cs
    13762-13778  ``ApplyFilter``:
                 ``var query = FilterBox.Text.Trim();``
                 pusty/bialy zapytanie => ``_unfilteredItems`` (CALA lista),
                 inaczej ``row.Label.Contains(query,
                 StringComparison.CurrentCultureIgnoreCase)``
                 => dopasowanie jest PODCIAGIEM (Contains), nie prefiksem,
                    i jest NIEWRAZLIWE na wielkosc liter biezacej kultury.
                 Po filtrowaniu wybor wyznacza
                 ``MainWindowNavigationPolicy.ResolveListSelectionIndex``
                 (preferowane Id, inaczej ``fallbackIndex ?? 0``).
    23650-23668  ``FilterBox_TextChanged``: filtr zapisuje sie PER WIDOK
                 (``navigation.Filters[_currentView]``) i ustawia status
                 ``Wyniki filtrowania: {liczba}`` albo ``Gotowy``.
    22210-22220  ``FocusFilter``: ``FilterBox.Focus()`` + ``SelectAll()``.
    22200-22208  ``FocusFilterResults``: pusty wynik NIE przenosi fokusu,
                 tylko oglasza komunikat.
    22549-22561  ``ReturnToMediaListFromEscape``: gdy filtr ma tekst,
                 Escape go CZYSCI (``Filtr wyczyszczony``) i zostaje na
                 liscie; dopiero pusty filtr pozwala wyjsc wyzej.
    10196-10204  ``RestoreFilterForCurrentView``: wejscie w widok przywraca
                 tekst filtra zapamietany dla TEGO widoku.

Ten plik mierzy WYLACZNIE czysta logike (``list_filter``). Podlaczenie GUI
mierzy ``test_filter_gui_wiring.py``, a mowe -- zywy NVDA (kwity osobno).
"""

from __future__ import annotations

from amc_wx_lite.list_filter import (
    FilterState,
    filter_rows,
    matches_query,
    results_status_text,
)
from amc_wx_lite.list_model import ListModel, Row


def track(item_id: str, title: str) -> Row:
    return Row(item_id=item_id, title=title, kind="track", path=f"C:\\m\\{title}.mp3")


# --------------------------------------------------------------- dopasowanie


def test_puste_zapytanie_przepuszcza_wszystko():
    """``string.IsNullOrEmpty(query)`` => ``_unfilteredItems`` (cs:13766)."""
    rows = [track("a", "Alfa"), track("b", "Beta")]
    assert filter_rows(rows, "") == rows
    # Same biale znaki tez: C# robi ``Trim()`` PRZED sprawdzeniem pustki.
    assert filter_rows(rows, "   ") == rows


def test_dopasowanie_jest_podciagiem_nie_prefiksem():
    """``Contains``, nie ``StartsWith`` (cs:13769).

    To rozroznienie jest istotne: ``ListModel.find_prefix`` (szukanie po
    pierwszej literze natywnej listy) uzywa PREFIKSU i zostaje nietkniety.
    Filtr to inna funkcja oryginalu i ma inna regule.
    """
    assert matches_query("Koncert fortepianowy", "fortepian")
    assert not matches_query("Koncert fortepianowy", "xfortepian")


def test_dopasowanie_ignoruje_wielkosc_liter_takze_polskich():
    """``CurrentCultureIgnoreCase`` (cs:13769) -- profil jest polski.

    ``casefold`` zalatwia ZARÓWNO ASCII, jak i polskie znaki. Gdyby uzyc
    ``lower()`` na porownaniu, czesc par i tak by przeszla, ale ``casefold``
    jest regula Unicode dla porownan bez wielkosci liter -- i to ona
    odpowiada intencji ``IgnoreCase``.
    """
    assert matches_query("Żółta Łódź", "żółta")
    assert matches_query("Żółta Łódź", "ŻÓŁTA")
    assert matches_query("żółta łódź", "ŻÓŁTA")
    assert matches_query("Śpiewy Ćwierkające", "ćwierk")


def test_filtr_nie_dopasowuje_po_sciezce_ani_adresie():
    """C# filtruje po ``row.Label`` -- czyli po NAZWIE wiersza (cs:13769).

    Sciezka i adres maja w tym porcie wlasne miejsce (``Row.address``,
    Ctrl+Shift+C). Filtrowanie po nich dawaloby trafienia, ktorych
    uzytkownik nie widzi na liscie -- czyli wyniki bez wytlumaczenia.
    """
    rows = [Row(item_id="t", title="Alfa", kind="track", path="C:\\beta\\Alfa.mp3")]
    assert filter_rows(rows, "beta") == []


def test_wiersz_rodzica_zostaje_w_wynikach():
    """".." to WYJSCIE z widoku, nie dana do filtrowania.

    Odfiltrowanie go odebraloby jedyna widoczna droge w gore przy aktywnym
    filtrze. Backspace dziala dalej, ale wiersz ma zostac widoczny.
    """
    rows = [
        Row(item_id="parent:C:\\", title="..", kind="parent", path="C:\\"),
        track("a", "Alfa"),
        track("b", "Beta"),
    ]
    assert [r.item_id for r in filter_rows(rows, "alfa")] == ["parent:C:\\", "a"]


def test_filtr_zachowuje_kolejnosc_widoku():
    """``Where`` nie sortuje (cs:13768). Kolejnosc kolejki/zapisu zostaje."""
    rows = [track("c", "Ala c"), track("a", "Ala a"), track("b", "Ala b")]
    assert [r.item_id for r in filter_rows(rows, "ala")] == ["c", "a", "b"]


# ------------------------------------------------------------- stan filtra


def test_stan_filtra_jest_per_widok():
    """``navigation.Filters[_currentView]`` (cs:23656, cs:10199)."""
    state = FilterState()
    state.set_for_view(("files", "allFiles"), "mozart")
    state.set_for_view(("files", "favorites"), "bach")
    assert state.text_for_view(("files", "allFiles")) == "mozart"
    assert state.text_for_view(("files", "favorites")) == "bach"
    # Widok, ktorego nikt nie filtrowal, ma PUSTY filtr -- nie dziedziczy.
    assert state.text_for_view(("radio", None)) == ""


def test_wyczyszczenie_filtra_dotyczy_tylko_swojego_widoku():
    state = FilterState()
    state.set_for_view(("files", "allFiles"), "mozart")
    state.set_for_view(("files", "favorites"), "bach")
    state.clear_for_view(("files", "allFiles"))
    assert state.text_for_view(("files", "allFiles")) == ""
    assert state.text_for_view(("files", "favorites")) == "bach"


def test_status_oddaje_liczbe_wynikow_jak_w_oryginale():
    """cs:23667-23669: pusty filtr => \"Gotowy\", inaczej liczba wynikow."""
    assert results_status_text("", 12) == "Gotowy"
    assert results_status_text("   ", 12) == "Gotowy"
    assert results_status_text("alfa", 3) == "Wyniki filtrowania: 3"
    assert results_status_text("alfa", 0) == "Wyniki filtrowania: 0"


# ------------------------------------------- wspolpraca z istniejacym modelem


def test_filtr_nie_rusza_modelu_ani_wyboru():
    """Filtr liczy WIDOK, a model zostaje zrodlem prawdy o pelnym zbiorze.

    Mechanizmu list (``ListModel``/``list_sync``) nie zmieniamy: filtr daje
    wiersze, a ``ListModel.replace`` zachowuje wybor po Id tak jak dotad.
    """
    model = ListModel()
    model.replace([track("a", "Alfa"), track("b", "Beta"), track("c", "Gamma")])
    model.select_id("b")

    visible = filter_rows(model.rows, "beta")

    assert [r.item_id for r in visible] == ["b"]
    # Zrodlo prawdy NIETKNIETE: filtr niczego nie usunal z modelu.
    assert [r.item_id for r in model.rows] == ["a", "b", "c"]
    assert model.selected_id == "b"


def test_wybor_przezywa_filtr_gdy_wiersz_zostaje_widoczny():
    """``ResolveListSelectionIndex`` woli preferowane Id (policy:101-121)."""
    rows = [track("a", "Alfa"), track("b", "Beta"), track("c", "Beta druga")]
    visible = filter_rows(rows, "beta")
    assert [r.item_id for r in visible] == ["b", "c"]
    # Zaznaczony "c" nadal jest w wynikach, wiec ma pozostac zaznaczony.
    assert any(row.item_id == "c" for row in visible)
