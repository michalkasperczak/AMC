"""Page Up / Page Down w odtwarzaczu: poprzedni i nastepny utwor kolejki.

Skroty NIE sa wymyslone: w oryginale AMC odtwarzacz ma je pod ``Page Up`` i
``Page Down`` (``MainWindow.xaml:714-719`` -- pozycje menu "Poprzedni utwor" i
"Nastepny utwor" oraz przyciski :995-1000 z tymi samymi akceleratorami).

Te testy pilnuja TYLKO przypisania i zakresu: ze gest istnieje w widoku
odtwarzacza, nie koliduje z zakladkami (``Shift+Page Up``/``Shift+Page Down``)
i nie wchodzi w widok listy, gdzie Page Up/Down naleza do ListCtrl i sluza
czytnikowi do przewijania.
"""

from __future__ import annotations

from amc_wx_lite.shortcuts import Action, Chord, resolve


def _chord(name: str, **mods: bool) -> Chord:
    return Chord(name, **mods)


def test_page_down_w_odtwarzaczu_to_nastepny_utwor() -> None:
    action = resolve(_chord("Next"), player_view=True, radio_session=False)
    assert action is Action.QUEUE_NEXT


def test_page_up_w_odtwarzaczu_to_poprzedni_utwor() -> None:
    action = resolve(_chord("Prior"), player_view=True, radio_session=False)
    assert action is Action.QUEUE_PREVIOUS


def test_na_liscie_page_updown_zostaje_dla_kontrolki() -> None:
    """W widoku listy te klawisze naleza do ListCtrl -- czytnik nimi przewija."""
    assert resolve(_chord("Next"), player_view=False, radio_session=False) is None
    assert resolve(_chord("Prior"), player_view=False, radio_session=False) is None


def test_shift_page_updown_nie_jest_zmiana_utworu() -> None:
    """``Shift+Page Up/Down`` to w oryginale ZAKLADKI, nie utwory.

    Nie implementujemy ich tutaj, ale gest nie moze po cichu spasc na zmiane
    utworu -- to byloby ciche przejecie cudzego skrotu.
    """
    assert resolve(_chord("Next", shift=True), player_view=True, radio_session=False) is not Action.QUEUE_NEXT
    assert resolve(_chord("Prior", shift=True), player_view=True, radio_session=False) is not Action.QUEUE_PREVIOUS


def test_oba_gesty_sa_opisane_w_pomocy() -> None:
    """F1 ma wymieniac nowe gesty: inaczej uzytkownik ich nie odkryje."""
    from amc_wx_lite.shortcuts import describe

    entries = dict(describe())
    assert "Page Down" in entries
    assert "Page Up" in entries
