"""Enter w widoku Zapisanej kolejki uruchamia ZYWA kolejke hosta.

Dotychczas widok Ctrl+Q byl tylko do odczytu: Enter skladal zwykle
``files.play`` i po koncu utworu nie dzialo sie nic. Te testy pilnuja, ze z
tego widoku powstaje zlecenie ``PlayFromQueue`` -- czyli ze nastepstwo liczy
kolejka hosta, a nie pojedyncze odtworzenie pliku.

Czego te testy NIE mierza: samego przejscia miedzy utworami. To rozstrzyga
sesja Core po stronie hosta i sprawdza zestaw ``--queue`` testow protokolu.
Tutaj chodzi o droge GUI: ktore zlecenie powstaje i czy wybor zostaje tam,
gdzie uzytkownik stoi.
"""

from __future__ import annotations

from amc_wx_lite.list_model import Row
from amc_wx_lite.navigation import (
    Announce,
    LibraryView,
    Navigator,
    PlayFromQueue,
    PlayTrack,
    View,
)


def _rows() -> list[Row]:
    """Trzy pozycje kolejki. Kolejnosc B, A, C NIE jest alfabetyczna."""
    return [
        Row(item_id="q1", title="B utwor", kind="track", path=r"C:\muzyka\B.wav"),
        Row(item_id="q2", title="A utwor", kind="track", path=r"C:\muzyka\A.wav"),
        Row(item_id="q3", title="C utwor", kind="track", path=r"C:\muzyka\C.wav"),
    ]


def _navigator_in_queue_view() -> Navigator:
    navigator = Navigator()
    state = navigator.session
    state.library_view = LibraryView.SAVED_QUEUE
    state.model.replace(_rows())
    return navigator


def test_enter_w_kolejce_daje_zlecenie_kolejki_nie_pojedynczy_plik() -> None:
    """Enter z Ctrl+Q uruchamia KOLEJKE, nie samotne odtworzenie pliku."""
    navigator = _navigator_in_queue_view()
    navigator.session.model.select_index(2)  # wiersz C, nie pierwszy

    effects = navigator.activate_selected()

    orders = [effect for effect in effects if isinstance(effect, PlayFromQueue)]
    assert len(orders) == 1, "powstaje dokladnie jedno zlecenie kolejki"
    order = orders[0]
    assert order.item_id == "q3", "kolejka startuje od WYBRANEGO wiersza"
    # Pozycje idą w kolejnosci WIDOKU, bo taka jest kolejnosc zapisanej kolejki.
    assert [row.item_id for row in order.rows] == ["q1", "q2", "q3"]
    assert not any(isinstance(effect, PlayTrack) for effect in effects), (
        "z widoku kolejki NIE wychodzi zwykle files.play: to zerwaloby nastepstwo"
    )


def test_enter_w_kolejce_przechodzi_do_odtwarzacza_i_zapamietuje_wiersz() -> None:
    navigator = _navigator_in_queue_view()
    navigator.session.model.select_index(1)

    navigator.activate_selected()
    state = navigator.session

    assert state.view is View.PLAYER, "Enter przechodzi do odtwarzacza jak dotad"
    assert state.now_playing_id == "q2", "biezacy material to wybrany wiersz"
    assert state.list_anchor_id == "q2", (
        "powrot Escape ma wrocic na TEN wiersz, nie na poczatek listy"
    )


def test_naturalne_przejscie_aktualizuje_biezacy_material() -> None:
    """``queue.advanced`` z hosta zmienia BIEZACY material, nie zaznaczenie.

    Host policzyl przejscie i NAPRAWDE juz gra nastepna pozycje. Okno ma to
    odwzorowac, ale kursor uzytkownika na liscie zostaje tam, gdzie byl --
    inaczej fokus uciekalby sam z siebie w trakcie sluchania.
    """
    navigator = _navigator_in_queue_view()
    navigator.session.model.select_index(0)
    navigator.activate_selected()
    anchor_before = navigator.session.list_anchor_id

    effects = navigator.note_queue_advanced("q2", "A utwor")
    state = navigator.session

    assert state.now_playing_id == "q2", "biezacy material to TO, co host zaczal grac"
    assert state.now_playing_title == "A utwor"
    assert state.list_anchor_id == anchor_before, (
        "zaznaczenie na liscie NIE idzie za audio: fokus nie moze uciekac sam"
    )
    assert any(isinstance(effect, Announce) for effect in effects), (
        "zmiane utworu trzeba powiedziec, inaczej uzytkownik jej nie zauwazy"
    )


def test_przejscie_bez_identyfikatora_nie_psuje_stanu() -> None:
    """Zdarzenie bez ``id`` nie moze wyczyscic biezacego materialu."""
    navigator = _navigator_in_queue_view()
    navigator.session.model.select_index(0)
    navigator.activate_selected()

    navigator.note_queue_advanced("", "")

    assert navigator.session.now_playing_id == "q1", "stan zostaje nietkniety"


def test_przejscie_nie_przerzuca_widoku_gdy_uzytkownik_wrocil_na_liste() -> None:
    """Slucham kolejki, ale przegladam liste: przejscie NIE ma mnie porywac."""
    navigator = _navigator_in_queue_view()
    navigator.session.model.select_index(0)
    navigator.activate_selected()
    navigator.back_to_list()

    navigator.note_queue_advanced("q2", "A utwor")

    assert navigator.session.view is View.LIST, (
        "przejscie utworu nie przerzuca widoku pod rekami uzytkownika"
    )


def test_poza_kolejka_enter_dziala_jak_dotad() -> None:
    """Zwykly folder NIE zmienia zachowania: dalej pojedynczy files.play."""
    navigator = Navigator()
    navigator.session.model.replace(_rows())  # library_view pozostaje None

    effects = navigator.activate_selected()

    assert any(isinstance(effect, PlayTrack) for effect in effects)
    assert not any(isinstance(effect, PlayFromQueue) for effect in effects)


def test_wiersz_bez_sciezki_w_kolejce_nie_udaje_odtwarzania() -> None:
    navigator = Navigator()
    state = navigator.session
    state.library_view = LibraryView.SAVED_QUEUE
    state.model.replace([Row(item_id="q1", title="bez pliku", kind="track", path="")])

    effects = navigator.activate_selected()

    assert not any(isinstance(effect, PlayFromQueue) for effect in effects)
    assert state.view is not View.PLAYER, "odmowa zostawia uzytkownika na liscie"


def test_pozycje_bez_sciezki_nie_wchodza_do_zlecenia_kolejki() -> None:
    """Wiersz bez pliku nie moze trafic do kolejki hosta jako material."""
    navigator = Navigator()
    state = navigator.session
    state.library_view = LibraryView.SAVED_QUEUE
    state.model.replace(
        [
            Row(item_id="q1", title="B", kind="track", path=r"C:\muzyka\B.wav"),
            Row(item_id="q2", title="bez pliku", kind="track", path=""),
            Row(item_id="q3", title="C", kind="track", path=r"C:\muzyka\C.wav"),
        ]
    )
    state.model.select_index(0)

    effects = navigator.activate_selected()
    order = next(effect for effect in effects if isinstance(effect, PlayFromQueue))

    assert [row.item_id for row in order.rows] == ["q1", "q3"], (
        "pozycja bez sciezki wypada z kolejki, reszta zachowuje kolejnosc"
    )
