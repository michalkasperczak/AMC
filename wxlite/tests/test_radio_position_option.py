"""Przelacznik odczytu POZYCJI stacji: licznik "X z Y" na liscie radia.

CO TU NAPRAWDE MIERZYMY
-----------------------
Licznika "1 z 37" NIE mowi nasz ``Announcer``. Mowi go NVDA z WLASNEGO,
natywnego zrodla -- ``NVDAObjects/IAccessible/sysListView32.py:463-466``:

    def _get_positionInfo(self):
        index = self.IAccessibleChildID
        totalCount = watchdog.cancellableSendMessage(self.windowHandle, LVM_GETITEMCOUNT, 0, 0)
        return dict(indexInGroup=index, similarItemsInGroup=totalCount)

a ``speech.speech`` (linie 756-762, 2174-2182) zamienia to na wypowiedz.
Dlatego wyciszenie naszych komunikatow NIC by nie dalo -- zmiana musi
dotrzec do tego slownika. Robi to nakladka obiektu NVDA wybierana przez
ISTNIEJACY globalPlugin ``amcController`` (NVDAObjects/__init__.py:125-131
wola ``chooseNVDAObjectOverlayClasses`` KAZDEGO uruchomionego pluginu --
appModule NIE jest do tego potrzebny i nadal go nie tworzymy).

Strona AMC ma tu jedno zadanie: OZNACZYC swoja liste radiowa tak, zeby
nakladka trafila wylacznie w nia. Znacznikiem jest nazwana wlasciwosc okna
(``SetPropW``/``RemovePropW``) -- widoczna miedzy procesami, bo lista
wlasciwosci wisi przy HWND w ``user32``, a nie w pamieci procesu.

Te testy sa BEZ GUI i BEZ Windows: logika znacznikow jest czysta, a samo
wywolanie ``user32`` wstrzykujemy jako atrapa. Granica atrapy jest jawna:
atrapa dowodzi, KTORE wywolania robimy i kiedy, nie dowodzi, ze NVDA
naprawde zamilknie. To moze pokazac wylacznie zywy odbior.
"""

from __future__ import annotations

import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))

from amc_wx_lite import radio_position
from amc_wx_lite.state_store import LiteState, Options, StateStore


class FakeUser32:
    """Atrapa ``user32``: zapisuje wywolania zamiast dotykac okien.

    GRANICA: nie ma tu HWND ani czytnika. Dowodzi kolejnosci i tresci
    wywolan ``SetPropW``/``RemovePropW``, nie skutku w mowie.
    """

    def __init__(self, fail: bool = False) -> None:
        self.calls: list[tuple[str, int, str]] = []
        self.props: dict[tuple[int, str], int] = {}
        self.fail = fail

    def set_prop(self, hwnd: int, name: str, value: int) -> bool:
        self.calls.append(("set", hwnd, name))
        if self.fail:
            return False
        self.props[(hwnd, name)] = value
        return True

    def remove_prop(self, hwnd: int, name: str) -> bool:
        self.calls.append(("remove", hwnd, name))
        self.props.pop((hwnd, name), None)
        return True


# --------------------------------------------------------------- znaczniki


def test_dwa_rozne_znaczniki_bo_jeden_nie_przezyje_cache_obiektu() -> None:
    """Obecnosc listy i TRYB ukrywania to DWIE rozne informacje.

    Gdyby byl jeden znacznik, nakladka dobierana przy tworzeniu obiektu
    (NVDAObjects/__init__.py:110-131 -- raz, nie przy kazdym odczycie) nie
    zostalaby dolaczona, gdy opcja jest wlaczona. Pozniejsze wylaczenie
    opcji nie mialoby juz czego przelaczyc bez ponownego ustawienia fokusu.
    Dlatego: znacznik OBECNOSCI wisi zawsze na liscie radia (nakladka sie
    dolacza), a znacznik TRYBU czytany jest dopiero w ``_get_positionInfo``.
    """
    assert radio_position.PRESENCE_PROP != radio_position.MODE_PROP
    for name in (radio_position.PRESENCE_PROP, radio_position.MODE_PROP):
        assert name.startswith("AMC."), f"znacznik {name} musi byc nasza prywatna nazwa"
        assert "\x00" not in name


def test_lista_radia_z_ukrywaniem_dostaje_oba_znaczniki() -> None:
    fake = FakeUser32()
    markers = radio_position.WindowMarkers(fake.set_prop, fake.remove_prop)

    markers.apply(4242, is_radio_list=True, hide_position=True)

    assert fake.props.get((4242, radio_position.PRESENCE_PROP)) == radio_position.FLAG_ON
    assert fake.props.get((4242, radio_position.MODE_PROP)) == radio_position.FLAG_ON


def test_wlaczona_opcja_zdejmuje_TRYB_ale_zostawia_OBECNOSC() -> None:
    """Normalny licznik po wlaczeniu opcji -- bez utraty zaczepienia nakladki."""
    fake = FakeUser32()
    markers = radio_position.WindowMarkers(fake.set_prop, fake.remove_prop)

    markers.apply(4242, is_radio_list=True, hide_position=True)
    markers.apply(4242, is_radio_list=True, hide_position=False)

    assert fake.props.get((4242, radio_position.PRESENCE_PROP)) == radio_position.FLAG_ON
    assert (4242, radio_position.MODE_PROP) not in fake.props


def test_lista_plikow_nie_dostaje_ZADNEGO_znacznika() -> None:
    """Nie psujemy reszty list: poza radiem nie zostawiamy sladu."""
    fake = FakeUser32()
    markers = radio_position.WindowMarkers(fake.set_prop, fake.remove_prop)

    markers.apply(777, is_radio_list=False, hide_position=True)

    assert fake.props == {}
    assert not [call for call in fake.calls if call[0] == "set"]


def test_przejscie_na_pliki_zdejmuje_znaczniki_ktore_sami_zalozylismy() -> None:
    """Znacznik ma znikac przy wyjsciu z radia, nie wisiec do konca procesu."""
    fake = FakeUser32()
    markers = radio_position.WindowMarkers(fake.set_prop, fake.remove_prop)

    markers.apply(4242, is_radio_list=True, hide_position=True)
    markers.apply(4242, is_radio_list=False, hide_position=True)

    assert fake.props == {}


def test_forget_czysci_przy_zamknieciu_okna() -> None:
    """WM_NCDESTROY: dokumentacja SetProp zada usuniecia WLASNYCH wpisow."""
    fake = FakeUser32()
    markers = radio_position.WindowMarkers(fake.set_prop, fake.remove_prop)
    markers.apply(4242, is_radio_list=True, hide_position=True)
    fake.calls.clear()

    markers.forget(4242)

    assert fake.props == {}
    assert {call[2] for call in fake.calls} == {
        radio_position.PRESENCE_PROP,
        radio_position.MODE_PROP,
    }


def test_bez_zmiany_stanu_nie_ma_ZADNEGO_wywolania_user32() -> None:
    """Wspolny przyrostowy sync wola nas po KAZDYM przebiegu ``_run``.

    Ta sama zasada co ``sync_rows``: brak zmian = zero operacji. Inaczej
    dolozylibysmy dwa wywolania ``user32`` do kazdego ticku statusu.
    """
    fake = FakeUser32()
    markers = radio_position.WindowMarkers(fake.set_prop, fake.remove_prop)
    markers.apply(4242, is_radio_list=True, hide_position=True)
    fake.calls.clear()

    for _ in range(5):
        markers.apply(4242, is_radio_list=True, hide_position=True)

    assert fake.calls == []


def test_blad_user32_nie_wywraca_okna_i_nie_klamie_ze_sie_udalo() -> None:
    """UIPI moze odmowic (GetLastError 5). Okno ma zyc dalej, stan ma byc szczery."""
    fake = FakeUser32(fail=True)
    markers = radio_position.WindowMarkers(fake.set_prop, fake.remove_prop)

    assert markers.apply(4242, is_radio_list=True, hide_position=True) is False
    # Nie zapamietujemy nieudanego zapisu jako "zrobione" -- inaczej bramka
    # idempotencji juz nigdy by nie sprobowala ponownie.
    fake.fail = False
    assert markers.apply(4242, is_radio_list=True, hide_position=True) is True


def test_wyjatek_z_user32_tez_nie_wychodzi_na_zewnatrz() -> None:
    def boom(*_args: object) -> bool:
        raise OSError("user32 nie odpowiada")

    markers = radio_position.WindowMarkers(boom, boom)
    assert markers.apply(1, is_radio_list=True, hide_position=True) is False


def test_brak_hwnd_jest_obslugiwany_a_nie_wysadza_okna() -> None:
    fake = FakeUser32()
    markers = radio_position.WindowMarkers(fake.set_prop, fake.remove_prop)
    assert markers.apply(0, is_radio_list=True, hide_position=True) is False
    assert fake.calls == []


# ------------------------------------------------------------------ opcja


def test_domyslnie_radio_NIE_czyta_pozycji() -> None:
    """Uzgodniony domysl: sama nazwa stacji, bez licznika."""
    assert Options().radio_announce_position is False


def test_opcja_przezywa_zapis_i_odczyt(tmp_path_factory=None) -> None:
    """Zapis/readback: ustawienie ma przetrwac restart programu."""
    import tempfile

    with tempfile.TemporaryDirectory() as directory:
        store = StateStore(directory)
        state = LiteState()
        state.options.radio_announce_position = True
        store.save(state)

        assert StateStore(directory).load().options.radio_announce_position is True


def test_smiec_w_pliku_nie_wlacza_opcji_po_cichu() -> None:
    import json
    import tempfile

    with tempfile.TemporaryDirectory() as directory:
        path = Path(directory) / StateStore.FILE_NAME
        path.write_text(
            json.dumps({"options": {"radio_announce_position": "tak"}}),
            encoding="utf-8",
        )
        loaded = StateStore(directory).load().options.radio_announce_position
        assert loaded is False, "obca wartosc ma wrocic do bezpiecznego domyslu"


# --------------------------------------------- uczciwosc wobec braku dodatku


def test_wykrycie_dodatku_szuka_PLIKU_nakladki_nie_samego_dodatku() -> None:
    """Stary dodatek nie umie ukrywac pozycji -- i nie wolno udawac, ze umie.

    Sprawdzamy ISTNIEJACA mozliwosc wykrycia: obecnosc pliku nakladki w
    katalogu dodatkow NVDA. Bez handshake'u, bez serwera, bez pytania
    czytnika o cokolwiek.
    """
    import tempfile

    with tempfile.TemporaryDirectory() as directory:
        root = Path(directory)
        assert radio_position.overlay_addon_installed(root) is False

        plugin = root / "addons/amcController/globalPlugins/amcController"
        plugin.mkdir(parents=True)
        (plugin / "__init__.py").write_text("", encoding="utf-8")
        assert radio_position.overlay_addon_installed(root) is False, (
            "sam dodatek bez pliku nakladki NIE ukrywa pozycji"
        )

        (plugin / radio_position.OVERLAY_MODULE).write_text("", encoding="utf-8")
        assert radio_position.overlay_addon_installed(root) is True


def test_brak_katalogu_nvda_to_po_prostu_brak_wiedzy_nie_wyjatek() -> None:
    assert radio_position.overlay_addon_installed(None) is False
    assert radio_position.overlay_addon_installed(Path("/nie/ma/takiej/sciezki")) is False


def test_komunikat_mowi_PRAWDE_o_tym_czy_ukrywanie_dziala() -> None:
    """Zapowiedz nie moze byc checkboxem bez skutku.

    Gdy nakladki nie ma, uzytkownik ma uslyszec, czego brakuje -- zamiast
    "wylaczone" i dalej slyszanego licznika.
    """
    z_dodatkiem = radio_position.announcement(hide_position=True, overlay_available=True)
    bez_dodatku = radio_position.announcement(hide_position=True, overlay_available=False)
    wlaczone = radio_position.announcement(hide_position=False, overlay_available=False)

    assert "dodat" in bez_dodatku.lower(), bez_dodatku
    assert "dodat" not in z_dodatkiem.lower(), z_dodatkiem
    assert z_dodatkiem != bez_dodatku
    # Wlaczony licznik jest natywny -- dziala BEZ naszego dodatku, wiec tam
    # ostrzezenie byloby nieprawda.
    assert "dodat" not in wlaczone.lower(), wlaczone
    for text in (z_dodatkiem, bez_dodatku, wlaczone):
        assert text and text[0].isupper() and text.endswith(".")


# --------------------------------------------------------------- menu i akcja


def test_menu_ma_przelacznik_w_menu_radio_jako_pozycje_zaznaczalna() -> None:
    from amc_wx_lite import menu_model
    from amc_wx_lite.shortcuts import Action

    radio = next(m for m in menu_model.build_menus() if "Radio" in m.title)
    entry = next(
        (i for i in radio.items if i.action is Action.TOGGLE_RADIO_POSITION), None
    )
    assert entry is not None, "przelacznik musi byc osiagalny istniejaca droga menu"
    assert entry.checkable, "stan przelacznika ma byc widoczny dla czytnika ekranu"
    assert "&" in entry.label


def test_przelacznik_nie_dokłada_wariantu_do_pelnego_AMC() -> None:
    """Ustawienie jest PRYWATNE dla portu wx -- zadnego zapisu do C#."""
    source = (
        Path(__file__).resolve().parents[1] / "amc_wx_lite/radio_position.py"
    ).read_text(encoding="utf-8")
    for forbidden in ("AccessibleMediaController", "state.json", "host_client"):
        assert forbidden not in source, forbidden


def test_odmowa_usuniecia_markera_nie_jest_sukcesem_i_mozna_ponowic() -> None:
    fake = FakeUser32()
    allow_remove = False

    def remove(hwnd, name):
        return fake.remove_prop(hwnd, name) if allow_remove else False

    markers = radio_position.WindowMarkers(fake.set_prop, remove)
    assert markers.apply(4242, is_radio_list=True, hide_position=True)
    assert not markers.apply(4242, is_radio_list=True, hide_position=False)
    assert fake.props[(4242, radio_position.MODE_PROP)] == 1
    allow_remove = True
    assert markers.apply(4242, is_radio_list=True, hide_position=False)
    assert (4242, radio_position.MODE_PROP) not in fake.props


def test_plik_dodatku_nie_dowodzi_aktywnego_wykonawcy() -> None:
    text = radio_position.announcement(hide_position=True, overlay_available=True)
    assert "w AMC" in text


def test_blad_markera_ma_wlasna_przyczyne_komunikatu() -> None:
    import inspect

    assert "marker_applied" in inspect.signature(radio_position.announcement).parameters
    text = radio_position.announcement(
        hide_position=True, overlay_available=True, marker_applied=False,
    )
    assert "nie udało się" in text
    assert "dodat" not in text.lower()


def test_przelacznik_ma_menu_bez_nowego_globalnego_skrotu() -> None:
    from amc_wx_lite.menu_model import build_menus
    from amc_wx_lite.shortcuts import Action, RADIO_LIST_VIEW

    entry = next(i for m in build_menus() for i in m.items
                 if i.action is Action.TOGGLE_RADIO_POSITION)
    assert not entry.shortcut
    assert "Ctrl+Shift+N" not in RADIO_LIST_VIEW
