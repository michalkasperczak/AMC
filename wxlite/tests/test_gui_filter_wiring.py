"""Podlaczenie filtra do OKNA: fokus, Escape, Enter, zmiana widoku, kolejka.

Testujemy PRAWDZIWE metody ``MainFrame`` (``_focus_filter``, ``_on_filter_key``,
``_on_filter_text``, ``_clear_filter_and_return``, ``_focus_filter_results``,
``_restore_filter_for_current_view``) przypiete do atrapy okna -- tak jak
``test_native_list_apply`` robi to dla ``MediaListCtrl``. Bez tego "podlaczenie
GUI" bylo by tylko twierdzeniem: kod moglby miec metody, ktorych NIC nie wola i
ktore nie zmieniaja widocznej listy.

Pulpitu tu nie ma (WSL), wiec sprawdzamy zachowanie, nie malowanie pikseli.
Fizyczny odbior na pulpicie Hermesa z NVDA jest osobno, w kwicie przyrostu.
"""

from __future__ import annotations

import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))

from test_gui_logic import install_wx_stub

install_wx_stub()

from amc_wx_lite import list_filter  # noqa: E402
from amc_wx_lite.list_model import ListModel, Row  # noqa: E402
from amc_wx_lite.shortcuts import Action, Chord, resolve  # noqa: E402
from amc_wx_lite.navigation import View  # noqa: E402
import test_native_list_apply as nla  # noqa: E402


def track(item_id: str, name: str) -> Row:
    return nla.track(item_id, name)


class FakeAnnouncer:
    """Zbiera to, co poszloby do czytnika ekranu."""

    def __init__(self) -> None:
        self.said: list[str] = []

    def say(self, text: str) -> None:
        self.said.append(text)


class FakeTextCtrl:
    """Atrapa ``wx.TextCtrl`` z karetka i zaznaczeniem."""

    def __init__(self) -> None:
        self._value = ""
        self.focused = False
        self.select_all_calls = 0
        #: Zdarzenia EVT_TEXT, ktore wx wyslalby po ``SetValue``.
        self.on_text = None

    def GetValue(self) -> str:  # noqa: N802 - API wx
        return self._value

    def SetValue(self, text: str) -> None:  # noqa: N802 - API wx
        # wx wysyla EVT_TEXT takze dla zmiany programowej. Odwzorowujemy to,
        # bo wlasnie na tym opiera sie bramka ``_restoring_filter``.
        self._value = text
        if self.on_text is not None:
            self.on_text(FakeTextEvent())

    def SetFocus(self) -> None:  # noqa: N802 - API wx
        self.focused = True

    def SelectAll(self) -> None:  # noqa: N802 - API wx
        self.select_all_calls += 1


class FakeTextEvent:
    def Skip(self) -> None:  # noqa: N802 - API wx
        pass


import contextlib  # noqa: E402


@contextlib.contextmanager
def focus_on(control):
    """Ustawia to, co ``wx.Window.FindFocus()`` zwroci bramce CHAR_HOOK.

    Atrapa wx z ``test_gui_logic`` daje ``wx.Window`` jako ``_Any``, wiec
    ``FindFocus()`` zwracalo obiekt-cokolwiek i bramka fokusu NIGDY nie
    trafiala w pole filtra -- test nie moglby jej zmierzyc.
    """
    import wx

    previous = getattr(wx.Window, "FindFocus", None)
    wx.Window.FindFocus = staticmethod(lambda: control)
    try:
        yield
    finally:
        if previous is None:
            del wx.Window.FindFocus
        else:
            wx.Window.FindFocus = previous


class FakeKeyEvent:
    def __init__(self, code: int) -> None:
        self._code = code
        self.skipped = False
        #: Ile razy kod poprosil wx o NORMALNE doreczenie klawisza do
        #: kontrolki z fokusem. To jedyna droga z ``EVT_CHAR_HOOK``, ktora
        #: pomija akceleratory menu.
        self.allowed_next = 0

    def GetKeyCode(self) -> int:  # noqa: N802 - API wx
        return self._code

    def Skip(self) -> None:  # noqa: N802 - API wx
        self.skipped = True

    def DoAllowNextEvent(self) -> None:  # noqa: N802 - API wx
        self.allowed_next += 1


class FakeSession:
    """Minimalna sesja: model + tozsamosc widoku, ktorej uzywa filtr."""

    def __init__(self, model: ListModel, context: tuple) -> None:
        self.model = model
        self._context = context

    def view_context_parts(self) -> tuple:
        return self._context


def make_list(rows: list[Row]):
    """Atrapa listy z ``SetFocus``, ktorego ``MediaListCtrl`` uzywa przy
    przejsciu z pola na wyniki. ``FakePlainList`` z testow mechanizmu list go
    nie ma, bo tamte testy nie dotykaly fokusu -- dokladamy go TU, zeby nie
    zmieniac odebranej aparatury."""
    model = ListModel()
    model.replace(rows)
    ctrl = nla.make_ctrl(model)
    ctrl.focused = False

    def set_focus() -> None:
        ctrl.focused = True

    ctrl.SetFocus = set_focus
    ctrl.HasFocus = lambda: ctrl.focused
    ctrl.sync_rows()
    return ctrl, model


class FakeFrame:
    """Atrapa okna z PRAWDZIWYMI metodami filtra z ``LiteFrame``."""

    def __init__(self, rows: list[Row], *, view: View = View.LIST) -> None:
        import amc_wx_lite.gui as gui

        self.files_list, self.model = make_list(rows)
        self.radio_list, _ = make_list([])
        self.filter_box = FakeTextCtrl()
        self.announcer = FakeAnnouncer()
        self.filter_state = list_filter.FilterState()
        self._filter_context = gui._BRAK_KONTEKSTU
        self._restoring_filter = False
        self._view = view
        self._context: tuple = ("files", "/muzyka")

        for name in ("_focus_filter", "_on_filter_text", "_on_filter_key",
                     "_apply_filter_to_list", "_focus_filter_results",
                     "_clear_filter_and_return", "_set_filter_text",
                     "_restore_filter_for_current_view",
                     "_on_player_shortcut_hook"):
            setattr(self, name, getattr(gui.LiteFrame, name).__get__(self, FakeFrame))
        self.filter_box.on_text = self._on_filter_text

    # --- to, co prawdziwe okno bierze z nawigatora
    def _refresh_menu_state(self) -> None:
        """Prawdziwe okno przelicza bramki menu po KAZDYM sync listy.

        ``_apply_filter_to_list`` wola to u siebie (gui.py:1332), bo
        ``needs_selection`` zmienia sie razem z wyborem, ktory ustawia
        ``sync_rows``. Atrapa tylko LICZY wywolania -- nie ma menu, wiec nie
        ma czego wlaczac.
        """
        self.menu_refreshes = getattr(self, "menu_refreshes", 0) + 1

    def _active_list(self):
        return self.files_list

    def _current_view_context(self) -> tuple:
        return self._context

    @property
    def navigator(self):
        return self

    @property
    def view(self):
        return self._view

    @property
    def session(self):
        return FakeSession(self.model, self._context)

    # --- pomoc testu
    def type_into_filter(self, text: str) -> None:
        """Wpisanie tekstu tak, jak robi to uzytkownik (przez EVT_TEXT)."""
        self.filter_box._value = text
        self._on_filter_text(FakeTextEvent())

    def shown_names(self) -> list[str]:
        return [row[0] for row in self.files_list.rows]


ROWS = [
    track("1", "Zażółć gęślą jaźń"),
    track("2", "Zazolc gesla jazn"),
    track("3", "Beta"),
    track("4", "Gamma"),
]


# ------------------------------------------------------------------ skroty

def resolved(chord_text: str, *, player: bool = False, radio: bool = False):
    """Akcja dla akordu w danym widoku, przez PRAWDZIWY ``resolve``."""
    parts = chord_text.split("+")
    chord = Chord(
        key=parts[-1],
        ctrl="Ctrl" in parts,
        shift="Shift" in parts,
        alt="Alt" in parts,
    )
    return resolve(chord, player_view=player, radio_session=radio)


def test_niepusty_wynik_ZAWSZE_ma_zaznaczony_wiersz():
    """Port ``ResolveListSelectionIndex`` (MainWindowNavigationPolicy.cs:101).

    Oryginal: ``rows.Count == 0`` => -1, w kazdym innym razie
    ``Math.Clamp(fallbackIndex ?? 0, 0, rows.Count - 1)`` -- czyli gdy
    zaznaczony dotad utwor WYPADL z filtra, zaznaczenie spada na pierwszy
    widoczny wiersz, a NIE na nic.

    Zmierzone na zywym GUI (kwit-filter-2/probe.jsonl): po zawezeniu do
    jednego wiersza sonda pokazala ``nativeSelected=-1``. Dla niewidomego
    pusty wybor znaczy, ze Enter nie ma co odtworzyc, a strzalka startuje od
    zera -- wynik jest na ekranie, ale nieosiagalny.
    """
    frame = FakeFrame(ROWS)
    # Zaznaczamy wiersz, ktory ZA CHWILE wypadnie z filtra: to wlasnie ten
    # przypadek dal na zywym GUI pusty wybor (preferredItemId nie trafia, wiec
    # oryginal siega po fallback 0 -- port musi zrobic to samo).
    frame.model.select_id(ROWS[0].item_id)
    frame.files_list.sync_rows()
    frame.type_into_filter("zazolc")

    assert frame.files_list.visible_count() == 1
    assert frame.model.selected_id == ROWS[1].item_id, (
        "wynik ma zaznaczac widoczny wiersz, nie zachowany ukryty element"
    )
    assert frame.files_list.GetFirstSelected() == 0
    assert frame.files_list.shown_item_id(0) == frame.model.selected_id


def test_bramka_CHAR_HOOK_oddaje_pole_filtra_jego_wlasnej_obsludze():
    """Regresja ZMIERZONA na zywym GUI (kwit-filter-2/probe.jsonl).

    Pierwszy przebieg pokazal, ze Enter i Backspace WPISANE W POLU nie
    docieraly do ``_on_filter_key`` (brak zdarzen ``filter.key``), tylko do
    globalnej nawigacji: Enter otworzyl folder, a osiem Backspace dalo osiem
    "Wczytywanie Biblioteki..." i wyniosło uzytkownika w gore drzewa. Dla
    niewidomego to znaczy, ze poprawianie zapytania gubi mu widok.

    Oryginal broni sie bramka ``if (Keyboard.FocusedElement is TextBox)``
    (MainWindow.xaml.cs:21000). Port musi miec rownowazna bramke w
    ``EVT_CHAR_HOOK``, bo to jedyne miejsce widzace te klawisze przed
    nawigacja; inaczej pole filtra nie jest szczelne.
    """
    import inspect

    import amc_wx_lite.gui as gui

    hook = inspect.getsource(gui.LiteFrame._on_player_shortcut_hook)
    assert "filter_box" in hook, (
        "CHAR_HOOK nie sprawdza, czy fokus jest w polu filtra"
    )
    assert "_on_filter_key" in hook, (
        "CHAR_HOOK nie kieruje klawiszy pola do jego wlasnej obslugi"
    )


def test_backspace_w_polu_NIE_moze_isc_do_akceleratora_menu():
    """Regresja ZMIERZONA na zywym GUI (final-recovery, plan D + exp-accel).

    Sonda: ``filter.key ... key=8`` wystapil TYLKO RAZ (kazdy inny klawisz
    pola loguje sie DWA razy: z ``EVT_CHAR_HOOK`` i z ``EVT_KEY_DOWN``
    kontrolki), tekst filtra ZOSTAL "kaz" (znak nieusuniety), a NVDA
    powiedzial "To jest folder najwyzszego poziomu" (navigation.py:542).

    Przyczyna NIE jest brakiem bramki CHAR_HOOK (ta dziala) i NIE da sie jej
    naprawic po stronie obslugi klawiszy: sprawdzone na zywo, oddanie
    klawisza przez ``DoAllowNextEvent()`` NIC nie zmienilo -- akcelerator
    menu jest szybszy od CALEJ obslugi okna.

    Prawdziwa przyczyna: etykieta "Folder &nadrzędny\\tBackspace" kazala wx
    zbudowac AKCELERATOR NA POZIOMIE OKNA. Kontrdowod (plan exp-accel): po
    zdjeciu tego JEDNEGO akceleratora Backspace w polu skasowal znak
    ("kaz" -> "ka", wyniki 1 -> 2, ``key=8`` zalogowany DWA razy), a plan H
    pokazal, ze Backspace NA LISCIE nadal wychodzi do folderu nadrzednego.

    Dlatego test pilnuje MODELU MENU, nie obslugi klawiszy.
    """
    from amc_wx_lite import menu_model

    items = [
        item
        for menu in menu_model.build_menus()
        for item in menu.items
        if not item.is_separator
    ]
    parent = [i for i in items if i.action is Action.PARENT_FOLDER]
    assert parent, "zniknela pozycja 'Folder nadrzedny'"
    for item in parent:
        # Skrot ZOSTAJE widoczny dla uzytkownika...
        assert item.shortcut == "Back"
        # ...ale nie wolno go oddac wx jako akceleratora okna.
        assert not item.accelerator, (
            "Backspace w menu jest akceleratorem okna, wiec polknie klawisz "
            "w polu filtra"
        )


def test_skroty_edycji_zostaja_WIDOCZNE_choc_nie_sa_akceleratorami():
    """Zdjecie akceleratora nie moze ukryc skrotu przed niewidomym.

    Pozycja musi dalej MOWIC, jakim klawiszem ja wywolac -- inaczej naprawa
    jednego problemu zabralaby uzytkownikowi wiedze o skrocie.
    """
    import inspect

    import amc_wx_lite.gui as gui

    source = inspect.getsource(gui.LiteFrame._build_menu)
    assert "entry.accelerator" in source, (
        "budowa menu nie rozroznia podpisu skrotu od akceleratora"
    )
    # Skrot bez akceleratora ma trafic do NAZWY pozycji, nie zniknac.
    assert '({text})' in source or '(\" + text' in source or "({text})" in source


def test_po_starcie_fokus_jest_NA_LISCIE_a_nie_w_polu_filtra():
    """Regresja ZMIERZONA na zywym GUI (kwit-filter-1/snapshot.json).

    Dodanie ``filter_box`` wstawilo nowa kontrolke PRZED liste w kolejnosci
    tabulacji, wiec wx dal jej fokus startowy: pierwszy przebieg pokazal
    ``focus_class='TextCtrl'``, ``filter_has_focus=True``. Dla niewidomego to
    znaczy, ze program startuje w pustej edycji zamiast na liscie utworow --
    strzalki nie chodza po pozycjach. Okno MUSI wiec jawnie oddac fokus
    aktywnej liscie po zbudowaniu interfejsu.

    Sprawdzamy ZRODLO, bo fokus startowy ustawia sie w konstruktorze okna,
    ktorego atrapa w tym pliku nie odtwarza; prawdziwym dowodem konca jest
    snapshot z zywego przebiegu.
    """
    import inspect

    import amc_wx_lite.gui as gui

    source = inspect.getsource(gui.LiteFrame._build_ui)
    assert "_focus_active_list_initially" in source, (
        "budowa interfejsu nie ustawia fokusu startowego na liscie"
    )
    body = inspect.getsource(gui.LiteFrame._focus_active_list_initially)
    assert "_active_list" in body and "SetFocus" in body


def test_ctrl_k_jest_gestem_filtra_a_nie_ctrl_f():
    """Gest ODCZYTANY z MainWindow.xaml:491, nie zgadniety.

    Ctrl+F w oryginale otwiera OSOBNE okno szukania w zdalnej usludze, ktorego
    ten port nie ma. Zamapowanie filtra na Ctrl+F byloby cicha zmiana kontraktu
    wobec uzytkownika, ktory zna AMC.
    """
    assert resolved("Ctrl+K") is Action.FOCUS_FILTER
    assert resolved("Ctrl+F") is not Action.FOCUS_FILTER


def test_filtr_nie_dziala_w_odtwarzaczu_bo_nie_ma_tam_listy():
    assert resolved("Ctrl+K", player=True) is None


def test_goly_k_nie_jest_gestem_bo_listy_uzywaja_liter_do_przeskoku():
    """``KeyboardProfile.cs:80`` wiaze gole ``K``, ale tam listy nie maja
    pisania-po-literach jako nawigacji. W wx ListCtrl litera przeskakuje do
    wiersza i zabranie jej zepsulo by szybka nawigacje czytnikiem."""
    assert resolved("K") is not Action.FOCUS_FILTER


def test_ctrl_k_dziala_takze_w_widoku_radia():
    """Lista stacji tez jest lista -- filtr musi byc TA SAMA droga."""
    assert resolved("Ctrl+K", radio=True) is Action.FOCUS_FILTER


# -------------------------------------------------------------- wejscie

def test_ctrl_k_daje_fokus_polu_zaznacza_tekst_i_mowi():
    frame = FakeFrame(ROWS)
    frame._focus_filter()
    assert frame.filter_box.focused
    # ``SelectAll`` z oryginalu (cs:22216): ponowne wejscie zastepuje stary
    # tekst, zamiast dokladac do niego po omacku.
    assert frame.filter_box.select_all_calls == 1
    assert frame.announcer.said == [list_filter.FILTER_ENTERED_MESSAGE]


def test_ctrl_k_w_odtwarzaczu_mowi_a_nie_przenosi_fokusu_pod_panel():
    frame = FakeFrame(ROWS, view=View.PLAYER)
    frame._focus_filter()
    assert not frame.filter_box.focused
    assert frame.announcer.said and "odtwarzacz" in frame.announcer.said[0]


# --------------------------------------------------------------- wyniki

def test_wpisanie_tekstu_zweza_widoczna_liste():
    frame = FakeFrame(ROWS)
    frame.type_into_filter("beta")
    assert frame.shown_names() == ["Beta"]
    # Model ZOSTAJE pelny: filtr to wlasciwosc widoku, nie usuwanie danych.
    assert len(frame.model.rows) == 4


def test_polskie_znaki_dopasowuja_sie_doslownie_z_ogonkami():
    """ODCZYTANE, nie zalozone: ``MainWindow.xaml.cs:13769`` uzywa
    ``StringComparison.CurrentCultureIgnoreCase`` -- BEZ ``IgnoreNonSpace``.

    W kulturze polskiej ``ż`` i ``z`` to rozne litery na podstawowym poziomie
    porownania, wiec oryginal NIE dopasowuje \"zazolc\" do \"Zażółć\". Port robi
    to samo. Gdybysmy \"ulatwili\" i dodali skladanie ogonkow, wyniki w obu
    programach rozjechalyby sie przy tym samym tekscie -- a Michal porownuje je
    sluchem, nie wzrokiem.
    """
    frame = FakeFrame(ROWS)
    frame.type_into_filter("zazolc")
    assert frame.shown_names() == ["Zazolc gesla jazn"]

    frame2 = FakeFrame(ROWS)
    frame2.type_into_filter("gęślą")
    assert frame2.shown_names() == ["Zażółć gęślą jaźń"]


def test_wielkosc_liter_nie_ma_znaczenia_takze_dla_polskich():
    frame = FakeFrame(ROWS)
    frame.type_into_filter("ZAŻÓŁĆ")
    assert frame.shown_names() == ["Zażółć gęślą jaźń"]


def test_pisanie_filtra_nie_oglasza_liczby_wynikow():
    """Michal: zwykle pisanie bez automatycznych 'Wyniki filtrowania'."""
    frame = FakeFrame(ROWS)
    for query in ("b", "be", "beta", "be", ""):
        frame.type_into_filter(query)
        assert frame.announcer.said == [], frame.announcer.said
    assert len(frame.shown_names()) == len(ROWS)


def test_pusty_wynik_podczas_pisania_nie_przerywa_echa_klawiszy():
    """Brak wynikow wyjasniamy przy probie wejscia na liste, nie co znak."""
    frame = FakeFrame(ROWS)
    frame.type_into_filter("xyzzy")
    assert frame.shown_names() == []
    assert frame.announcer.said == []


# ------------------------------------------------- przejscie na wyniki

def test_enter_przenosi_fokus_z_pola_na_liste_wynikow():
    frame = FakeFrame(ROWS)
    frame.type_into_filter("beta")
    import wx

    event = FakeKeyEvent(wx.WXK_RETURN)
    frame._on_filter_key(event)
    assert frame.files_list.focused
    # Enter jest OBSLUZONY, nie przepuszczony dalej.
    assert not event.skipped


def test_strzalka_w_dol_tez_przenosi_na_wyniki():
    frame = FakeFrame(ROWS)
    frame.type_into_filter("beta")
    import wx

    frame._on_filter_key(FakeKeyEvent(wx.WXK_DOWN))
    assert frame.files_list.focused


def test_enter_przy_pustym_wyniku_ZOSTAWIA_fokus_w_polu():
    """Fokus na liscie bez wierszy to miejsce, z ktorego czytnik nie ma co
    przeczytac. Uzytkownik musi moc dalej poprawiac tekst."""
    frame = FakeFrame(ROWS)
    frame.type_into_filter("xyzzy")
    import wx

    frame._on_filter_key(FakeKeyEvent(wx.WXK_RETURN))
    assert not frame.files_list.focused
    assert frame.announcer.said[-1] == list_filter.NO_RESULTS_MESSAGE


def test_litery_i_strzalki_w_poziomie_NALEZA_do_pola():
    """Nie wolno porywac klawiszy edycji: w polu tekstowym litera to litera."""
    frame = FakeFrame(ROWS)
    import wx

    for code in (ord("a"), ord("ż"), wx.WXK_LEFT, wx.WXK_BACK, wx.WXK_HOME):
        event = FakeKeyEvent(code)
        frame._on_filter_key(event)
        assert event.skipped, f"kod {code} powinien trafic do pola"


# ------------------------------------------------------ wyjscie/Escape

def test_escape_czysci_filtr_i_wraca_na_pelna_liste():
    frame = FakeFrame(ROWS)
    frame.type_into_filter("beta")
    assert frame.shown_names() == ["Beta"]
    import wx

    frame._on_filter_key(FakeKeyEvent(wx.WXK_ESCAPE))
    assert frame.filter_box.GetValue() == ""
    assert frame.shown_names() == ["Zażółć gęślą jaźń", "Zazolc gesla jazn", "Beta", "Gamma"]
    assert frame.files_list.focused
    assert frame.announcer.said[-1] == list_filter.FILTER_CLEARED_MESSAGE


def test_escape_przy_pustym_polu_tylko_wraca_na_liste_bez_gadania():
    frame = FakeFrame(ROWS)
    frame.filter_box.focused = False
    import wx

    frame._on_filter_key(FakeKeyEvent(wx.WXK_ESCAPE))
    assert frame.files_list.focused
    assert frame.announcer.said == []


# -------------------------------------------- zachowanie wyboru i fokusu

def test_zaznaczony_wiersz_ktory_przechodzi_filtr_ZOSTAJE_zaznaczony():
    """Port ``preferredItemId`` z ``ApplyFilter`` (cs:13762)."""
    frame = FakeFrame(ROWS)
    frame.model.select_id("3")  # Beta
    frame.files_list.sync_rows()
    frame.type_into_filter("et")  # Beta przechodzi
    assert frame.shown_names() == ["Beta"]
    assert frame.model.selected_id == "3"


def test_po_escape_wybor_sprzed_filtra_jest_nadal_wyborem():
    frame = FakeFrame(ROWS)
    frame.model.select_id("4")  # Gamma
    frame.files_list.sync_rows()
    frame.type_into_filter("gam")
    import wx

    frame._on_filter_key(FakeKeyEvent(wx.WXK_ESCAPE))
    assert frame.model.selected_id == "4"


def test_kursor_na_przefiltrowanej_liscie_wskazuje_WIDOCZNY_wiersz():
    """Indeks kursora musi byc liczony w WIDOCZNYCH wierszach. Inaczej
    czytnik przeczytalby inny utwor, niz jest zaznaczony."""
    frame = FakeFrame(ROWS)
    frame.model.select_id("4")  # Gamma: 4. w pelnym zbiorze
    frame.files_list.sync_rows()
    frame.type_into_filter("gamma")
    assert frame.shown_names() == ["Gamma"]
    # Jedyny widoczny wiersz => kursor na 0, nie na 3.
    assert frame.files_list._selected == 0
    assert frame.files_list.shown_item_id(0) == "4"


# ----------------------------------------------- filtr per widok (kontekst)

def test_kazdy_widok_ma_WLASNY_filtr():
    """Port ``RestoreFilterForCurrentView`` (cs:10196). Filtr katalogu nie
    moze w ciszy ukrywac wierszy w kolejce."""
    frame = FakeFrame(ROWS)
    frame.type_into_filter("beta")
    frame._context = ("queue", None)
    frame._restore_filter_for_current_view()
    assert frame.filter_box.GetValue() == ""
    frame._context = ("files", "/muzyka")
    frame._restore_filter_for_current_view()
    assert frame.filter_box.GetValue() == "beta"


def test_przywracanie_filtra_NIE_jest_gestem_uzytkownika():
    """Przywrocenie tekstu nie moze oglaszac statusu ani nadpisywac stanu --
    uzytkownik nic nie nacisnal."""
    frame = FakeFrame(ROWS)
    frame.type_into_filter("beta")
    frame._context = ("queue", None)
    frame._restore_filter_for_current_view()
    before = len(frame.announcer.said)
    frame._context = ("files", "/muzyka")
    frame._restore_filter_for_current_view()
    assert len(frame.announcer.said) == before
    assert frame.filter_state.text_for_view(("files", "/muzyka")) == "beta"


def test_wejscie_w_ten_sam_widok_nie_przestawia_karetki():
    frame = FakeFrame(ROWS)
    frame.type_into_filter("be")
    frame._filter_context = frame._context
    value_before = frame.filter_box.GetValue()
    frame._restore_filter_for_current_view()
    assert frame.filter_box.GetValue() == value_before
