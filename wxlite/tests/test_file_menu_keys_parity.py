"""Cztery rozbieznosci menu Plik / Escape / Home-End wobec oryginalu.

ZGLOSZENIE (Michal, watek 36196)
-------------------------------
  \"A pliki ESC powinien tez skakac o poziom wyzej. Nie powinien mowic
  Wczytuje biblioteke. W otwartych plikach nie dziala Home i End. Zle
  przyporzadkowanie klawiszy w Menu plik. CTRL-Shift-o to folder, a CTRL-O
  otworz plik.\"

ZRODLO PRAWDY (odczytane, nie zgadniete)
----------------------------------------
``MainWindow.xaml:42-45``  OpenLocalFilesMenuItem   InputGestureText=\"Ctrl+O\"
``MainWindow.xaml:46-49``  OpenLocalFolderMenuItem  InputGestureText=\"Ctrl+Shift+O\"
``MainWindow.xaml.cs:20797-20828``  Escape: w odtwarzaczu -> lista; na liscie
    -> ``ReturnToMediaListFromEscape``.
``MainWindow.xaml.cs:22551-22563``  ten sam Escape: NIEPUSTY filtr -> wyczysc
    i wroc na liste; PUSTY filtr -> ``NavigateToParentLevel()`` (poziom wyzej).
``MainWindow.xaml.cs:21598-21599`` i ``22398-22399``  (None, Home) -> TrackStart,
    (None, End) -> TrackEnd -- tylko w odtwarzaczu.
``CommandRouter.cs:316-324``  TrackStart = pozycja 0; TrackEnd =
    ``Max(Zero, Duration - 10s)``. NIE 100%.

Mierzymy SCIEZKI, ktore juz istnieja (resolver, ``_dispatch``, model menu),
bez okna i bez czytnika -- ``wx`` w WSL nie ma.
"""

from __future__ import annotations

from types import SimpleNamespace

from test_gui_logic import FakeKeyEvent, install_wx_stub

install_wx_stub()

from amc_wx_lite import gui, menu_model  # noqa: E402
from amc_wx_lite.gui import chord_from_event  # noqa: E402
from amc_wx_lite.library_source import LibrarySnapshot, Row  # noqa: E402
from amc_wx_lite.navigation import View  # noqa: E402
from amc_wx_lite.shortcuts import Action, describe, resolve  # noqa: E402
from amc_wx_lite.transport_parity import MessagePolicy  # noqa: E402


class FakeRunner:
    """Zamiast watku: wykonuje prace od razu i oddaje wynik do ``done``.

    Domyslnie przekazuje WYNIK ``work()`` -- dokladnie tak, jak prawdziwy
    runner. ``payload`` nadpisuje go tylko tam, gdzie test udaje odpowiedz
    hosta.
    """

    _BRAK = object()

    def __init__(self, payload: object = _BRAK) -> None:
        self.payload = payload
        self.slots: list[str] = []

    def submit(self, slot, work, done, failed):  # noqa: ANN001 - API runnera
        self.slots.append(slot)
        try:
            wynik = work()
        except Exception as error:
            failed(error)
            return
        done(wynik if self.payload is FakeRunner._BRAK else self.payload)


# =========================================================== PUNKT 1: Ctrl+O


def test_ctrl_o_siega_po_dialog_plikow_a_nie_folderow() -> None:
    """Nie sam napis: ``_dispatch`` ma wywolac DIALOG PLIKU dla Ctrl+O.

    Odwrocone byly obie strony (``shortcuts.py:217-218`` i
    ``menu_model.py:77-78``), wiec test napisow by tego nie wylapal -- byly
    spojnie zle. Dlatego mierzymy wywolanie.
    """
    frame = gui.LiteFrame.__new__(gui.LiteFrame)
    called: list[str] = []
    frame._choose_file = lambda: called.append("plik")
    frame._choose_folder = lambda: called.append("folder")

    chord = chord_from_event(FakeKeyEvent(ord("o"), ctrl=True))
    frame._dispatch(resolve(chord, player_view=False, radio_session=False))
    assert called == ["plik"]

    chord = chord_from_event(FakeKeyEvent(ord("O"), ctrl=True, shift=True))
    frame._dispatch(resolve(chord, player_view=False, radio_session=False))
    assert called == ["plik", "folder"]


def test_menu_plik_podpisuje_klawisze_jak_oryginal() -> None:
    """Podpis w menu musi zgadzac sie z oryginalem, nie tylko z resolverem."""
    files = next(menu for menu in menu_model.build_menus() if menu.title == "&Pliki")
    podpisy = {
        item.action: item.shortcut
        for item in files.items
        if item.action in (Action.OPEN_FILE_DIALOG, Action.OPEN_FOLDER_DIALOG)
    }
    assert podpisy == {
        Action.OPEN_FILE_DIALOG: "Ctrl+O",
        Action.OPEN_FOLDER_DIALOG: "Ctrl+Shift+O",
    }


def test_pomoc_f1_podaje_te_same_klawisze_co_resolver() -> None:
    """Pomoc nie moze uczyc gestu, ktorego program nie ma."""
    pomoc = dict(describe())
    assert pomoc["Ctrl+O"] == "Wybierz plik"
    assert pomoc["Ctrl+Shift+O"] == "Wybierz folder"


# ========================================================== PUNKT 2: Escape


def test_escape_na_liscie_wychodzi_o_poziom_wyzej() -> None:
    """``ReturnToMediaListFromEscape`` z pustym filtrem woła poziom wyzej.

    Brak wpisu ``Escape`` w ``LIST_VIEW`` znaczyl, ze klawisz szedl do
    kontrolki i NIC nie robil -- dokladnie to zglosil Michal.
    """
    chord = chord_from_event(FakeKeyEvent(27))  # WXK_ESCAPE w zastepniku
    assert chord.canonical == "Escape"
    assert resolve(chord, player_view=False, radio_session=False) is Action.PARENT_FOLDER


def test_escape_z_odtwarzacza_nadal_wraca_na_liste() -> None:
    """cs:20806-20813 -- w odtwarzaczu Escape to powrot na liste, nie w gore."""
    chord = chord_from_event(FakeKeyEvent(27))
    assert resolve(chord, player_view=True, radio_session=False) is Action.SHOW_LIST


def test_escape_dziala_tez_na_pustej_liscie() -> None:
    """Pusty widok ma nadal wyjscie: akcja jest w tablicy, nie w wierszu."""
    chord = chord_from_event(FakeKeyEvent(27))
    assert resolve(chord, player_view=False, radio_session=True) is Action.PARENT_FOLDER


def test_escape_nie_jest_akceleratorem_menu() -> None:
    """Akcelerator okna POLYKA klawisz -- i to w polu filtra, gdzie Escape ma
    czyscic filtr (``_on_filter_key``). Podpis zostaje, akcelerator nie.

    To ta sama pulapka, co Backspace (``menu_model.MenuItem.accelerator``).
    """
    for menu in menu_model.build_menus():
        for item in menu.items:
            if item.shortcut == "Escape":
                assert item.accelerator is False, item.label


# ============================================== PUNKT 3: Wczytywanie Biblioteki


def test_otwarcie_biblioteki_nie_zapowiada_wczytywania() -> None:
    """Rutynowy komunikat \"Wczytywanie Biblioteki...\" ma zniknac.

    Oryginal przy wejsciu w widok nic takiego nie mowi; dla uzytkownika
    czytnika to szum przed kazda lista. Uzywamy PRAWDZIWEGO ``LibrarySnapshot``
    (nie atrapy pol), zeby test szedl ta sama droga, co okno -- razem z
    ``degradation_notice``.
    """
    frame = gui.LiteFrame.__new__(gui.LiteFrame)
    powiedziane: list[str] = []
    frame.announcer = SimpleNamespace(say=powiedziane.append)
    zdrowa = LibrarySnapshot(
        rows=[Row(kind="folder", title="Muzyka", path="/muzyka", item_id="dir:/muzyka")],
        heading="Biblioteka",
        folder_path="/muzyka",
        total_active=1,
        order_matches_amc=True,
        sees_live_writes=True,
    )
    frame.library = SimpleNamespace(load=lambda folder: zdrowa)
    frame.navigator = SimpleNamespace(apply_folder=lambda *a, **k: [])
    frame._run = lambda plan: None
    frame.runner = FakeRunner()

    frame._open_library("/muzyka")
    assert powiedziane == []


def test_blad_wczytania_biblioteki_nadal_mowi() -> None:
    """Usuwamy SZUM, nie diagnostyke: blad musi zostac slyszalny."""
    frame = gui.LiteFrame.__new__(gui.LiteFrame)
    powiedziane: list[str] = []
    frame.announcer = SimpleNamespace(say=powiedziane.append)

    def wybuchnij(folder):  # noqa: ANN001 - atrapa zrodla
        raise OSError("baza zajeta")

    frame.library = SimpleNamespace(load=wybuchnij)
    frame.runner = FakeRunner()
    frame._open_library(None)
    assert powiedziane == ["Nie moge wczytac Biblioteki: baza zajeta"]


def test_pusta_biblioteka_nadal_mowi() -> None:
    frame = gui.LiteFrame.__new__(gui.LiteFrame)
    powiedziane: list[str] = []
    frame.announcer = SimpleNamespace(say=powiedziane.append)
    frame.library = SimpleNamespace(load=lambda folder: SimpleNamespace(is_empty=True))
    frame.runner = FakeRunner()
    frame._open_library(None)
    assert powiedziane == ["Biblioteka jest pusta"]


# ======================================================= PUNKT 4: Home / End


def test_home_i_end_w_odtwarzaczu_to_poczatek_i_koniec_utworu() -> None:
    """cs:21598-21599, 22398-22399 -- GOLY Home/End, tylko w odtwarzaczu."""
    home = chord_from_event(FakeKeyEvent(313))  # WXK_HOME w zastepniku
    end = chord_from_event(FakeKeyEvent(312))  # WXK_END
    assert resolve(home, player_view=True, radio_session=False) is Action.TRACK_START
    assert resolve(end, player_view=True, radio_session=False) is Action.TRACK_END


def test_home_i_end_na_liscie_zostaja_kontrolce() -> None:
    """Na LISCIE Home/End to natywny skok na pierwszy/ostatni wiersz.

    Podmiana zabralaby uzytkownikowi czytnika podstawowa nawigacje listy --
    oryginal tez mapuje te klawisze TYLKO przy ``_playerViewActive``.
    """
    for code in (313, 312):
        chord = chord_from_event(FakeKeyEvent(code))
        assert resolve(chord, player_view=False, radio_session=False) is None
        assert resolve(chord, player_view=False, radio_session=True) is None


def test_end_skacze_dziesiec_sekund_przed_koniec_nie_na_sam_koniec() -> None:
    """``CommandRouter.cs:320-324``: ``Max(Zero, Duration - 10s)``.

    Skok na 100% zatrzymalby utwor natychmiast. Oryginal celowo zostawia
    dziesiec sekund, zeby bylo czego posluchac.
    """
    frame = gui.LiteFrame.__new__(gui.LiteFrame)
    powiedziane: list[str] = []
    frame.announcer = SimpleNamespace(say=powiedziane.append)
    frame.messages = MessagePolicy()
    frame._last_status = {"positionSeconds": 10.0, "durationSeconds": 600.0}
    skoki: list[float] = []
    frame.client = SimpleNamespace(seek_to_position=lambda s: skoki.append(s))
    frame.runner = FakeRunner({"positionSeconds": 590.0})

    frame._dispatch(Action.TRACK_END)
    assert skoki == [590.0]
    # Komunikat jak przy strzalkach (cs:323 pyta o te same dwie bramki).
    assert powiedziane == ["9:50"]


def test_end_w_krotkim_utworze_nie_schodzi_pod_zero() -> None:
    """``Max(TimeSpan.Zero, ...)`` z cs:321 -- utwor krotszy niz 10 s."""
    frame = gui.LiteFrame.__new__(gui.LiteFrame)
    frame.announcer = SimpleNamespace(say=lambda _text: None)
    frame.messages = MessagePolicy(enabled=False)
    frame._last_status = {"durationSeconds": 4.0}
    skoki: list[float] = []
    frame.client = SimpleNamespace(seek_to_position=lambda s: skoki.append(s))
    frame.runner = FakeRunner({})
    frame._dispatch(Action.TRACK_END)
    assert skoki == [0.0]


def test_home_w_odtwarzaczu_idzie_na_zero() -> None:
    frame = gui.LiteFrame.__new__(gui.LiteFrame)
    powiedziane: list[str] = []
    frame.announcer = SimpleNamespace(say=powiedziane.append)
    frame.messages = MessagePolicy()
    frame._last_status = {"durationSeconds": 600.0}
    skoki: list[float] = []
    frame.client = SimpleNamespace(seek_to_position=lambda s: skoki.append(s))
    frame.runner = FakeRunner({"positionSeconds": 0.0})
    frame._dispatch(Action.TRACK_START)
    assert skoki == [0.0]
    # cs:318 mowi doslownie \"0:00\".
    assert powiedziane == ["0:00"]


def test_track_end_milczy_przy_wylaczonych_komunikatach() -> None:
    """cs:323 pyta o ``SeekMessages && ArrowSeekMessages`` -- jak strzalki."""
    frame = gui.LiteFrame.__new__(gui.LiteFrame)
    powiedziane: list[str] = []
    frame.announcer = SimpleNamespace(say=powiedziane.append)
    frame.messages = MessagePolicy(arrow_seek_messages=False)
    frame._last_status = {"durationSeconds": 600.0}
    frame.client = SimpleNamespace(seek_to_position=lambda s: None)
    frame.runner = FakeRunner({"positionSeconds": 590.0})
    frame._dispatch(Action.TRACK_END)
    assert powiedziane == []


def test_brak_czasu_trwania_mowi_zamiast_milczec() -> None:
    """Bez ``durationSeconds`` nie da sie policzyc konca -- gest nie moze
    wygladac na zepsuty, wiec mowimy o tym zawsze (jak cs:546-551 przy %)."""
    frame = gui.LiteFrame.__new__(gui.LiteFrame)
    powiedziane: list[str] = []
    frame.announcer = SimpleNamespace(say=powiedziane.append)
    frame.messages = MessagePolicy()
    frame._last_status = {}
    frame.client = SimpleNamespace(seek_to_position=lambda s: None)
    frame.runner = FakeRunner({})
    frame._dispatch(Action.TRACK_END)
    assert powiedziane == ["Nie znam czasu trwania, nie moge skoczyc na koniec"]


def test_home_i_end_nie_ruszaja_gdy_nie_ma_silnika() -> None:
    frame = gui.LiteFrame.__new__(gui.LiteFrame)
    frame.announcer = SimpleNamespace(say=lambda _text: None)
    frame.messages = MessagePolicy()
    frame._last_status = {"durationSeconds": 600.0}
    frame.client = None
    frame.runner = FakeRunner({})
    frame._dispatch(Action.TRACK_END)
    frame._dispatch(Action.TRACK_START)
    assert frame.runner.slots == []


def test_pomoc_opisuje_home_i_end() -> None:
    pomoc = dict(describe())
    assert pomoc["Home"] == "Poczatek utworu"
    assert pomoc["End"] == "Koniec utworu, dziesiec sekund przed koncem"


# ======================================== granica: gesty czytnika zostaja czytnikowi


def test_nvda_end_nie_jest_przejmowany() -> None:
    """NVDA+End (Insert albo CapsLock) czyta PASEK STANU.

    Mapujemy wylacznie GOLY End -- ``ModifierKeys.None`` z cs:22399. Gdyby
    wpis lapal End z modyfikatorem, zabralibysmy uzytkownikowi odczyt statusu
    na zadanie, ktory ten port dopiero co dostal.

    ``wx`` nie raportuje klawisza NVDA jako modyfikatora, wiec zywego cyklu
    NVDA tu nie zmierzymy -- to zostaje dla rodzica. Tu pilnujemy warunku
    koniecznego: End z JAKIMKOLWIEK modyfikatorem nie jest nasza komenda.
    """
    for kwargs in (
        {"ctrl": True},
        {"shift": True},
        {"alt": True},
        {"ctrl": True, "shift": True},
    ):
        chord = chord_from_event(FakeKeyEvent(312, **kwargs))
        assert resolve(chord, player_view=True, radio_session=False) is None
        chord = chord_from_event(FakeKeyEvent(313, **kwargs))
        assert resolve(chord, player_view=True, radio_session=False) is None


def test_pole_filtra_zachowuje_wlasny_escape_i_home() -> None:
    """W polu filtra Escape czysci filtr, a Home idzie do pola (edycja).

    ``_on_player_shortcut_hook`` kieruje klawisze z pola do
    ``_on_filter_key``, wiec nowy wpis Escape w ``LIST_VIEW`` nie moze tego
    przeciac.
    """
    frame = gui.LiteFrame.__new__(gui.LiteFrame)
    wyczyszczone: list[bool] = []
    frame._clear_filter_and_return = lambda: wyczyszczone.append(True)
    frame._focus_filter_results = lambda: None
    pominiete: list[bool] = []

    event = FakeKeyEvent(27)
    event.Skip = lambda: pominiete.append(True)  # type: ignore[method-assign]
    frame._on_filter_key(event)
    assert wyczyszczone == [True]

    # Home w polu NALEZY DO POLA -- kursor na poczatek tekstu.
    event = FakeKeyEvent(313)
    event.Skip = lambda: pominiete.append(True)  # type: ignore[method-assign]
    frame._on_filter_key(event)
    assert pominiete == [True]
