"""PIATY punkt zgloszenia: Ctrl+L nie wracal z Ulubionych ani z Historii.

ZGLOSZENIE (Michal, watek 36196)
-------------------------------
  "Aha CTRL-l Biblioteka nie przeszlo z Ulubionych." + "i z Historii".

ZRODLO PRAWDY (odczytane, nie zgadniete)
----------------------------------------
``MainWindow.xaml:427-428``  ``LibraryViewMenuItem`` Header="_Biblioteka"
    InputGestureText="Ctrl+L" Click="LibraryView_Click".
``MainWindow.xaml.cs:22173``  ``(ModifierKeys.Control, Key.L) => CommandIds.ViewLibrary``.
``CommandRouter.cs:390``  ``case CommandIds.ViewLibrary: return ShowView("Biblioteka");``
``MainWindow.xaml.cs:701-705``  w sesji ``local`` nazwa "Biblioteka" jest
    PODMIENIANA na ``_state.LocalMedia.LibraryView`` -- czyli na ZAPAMIETANY
    widok, a NIE zawsze na korzen Folderow.
``AppSettings.cs:1236``  ``LibraryView { get; set; } = "Foldery"`` -- wartosci to
    nazwy widokow z cs:64-66: "Foldery", "Wszystkie pliki", "Kolejność własna".
``MainWindow.xaml.cs:18095``  ``RefreshCurrentView(preferredItemId:
    navigation.SelectedItemIds.GetValueOrDefault(_currentView))`` -- docelowy
    widok dostaje SWOJE wczesniejsze zaznaczenie, nie pierwszy wiersz.

Co bylo zle w porcie: ``Ctrl+L`` nie istnialo w ogole (brak w ``LIST_VIEW`` i w
menu), a ``Navigator.apply_library_view`` kasowalo ``folder_path``
(``navigation.py:720``) i nie pamietalo zaznaczenia widoku, z ktorego wychodzi.
Nawet po dodaniu gestu powrot ladowalby w korzeniu, na pierwszym wierszu.

Mierzymy PRAWDZIWA droge: ``resolve`` -> ``LiteFrame._dispatch`` -> ``Navigator``
-> ``LiteFrame._run``. Bez okna i bez czytnika (``wx`` w WSL nie ma); granice
zywego sprawdzenia sa w CTRLL.md.
"""

from __future__ import annotations

from types import SimpleNamespace

from test_gui_logic import FakeKeyEvent, install_wx_stub

install_wx_stub()

from amc_wx_lite import gui, menu_model  # noqa: E402
from amc_wx_lite.gui import chord_from_event  # noqa: E402
from amc_wx_lite.library_source import Row  # noqa: E402
from amc_wx_lite.navigation import (  # noqa: E402
    LIBRARY_VIEW_ALL_FILES,
    LIBRARY_VIEW_FOLDERS,
    LibraryView,
    Navigator,
    OpenFolder,
    OpenLibraryView,
    SessionId,
)
from amc_wx_lite.shortcuts import Action, describe, resolve  # noqa: E402

FOLDER = "D:\\Muzyka"


def _rows(prefix: str, ile: int) -> list[Row]:
    return [
        Row(kind="file", title=f"{prefix} {n}", path=f"{FOLDER}\\{prefix}{n}.mp3",
            item_id=f"{prefix}:{n}")
        for n in range(1, ile + 1)
    ]


def _navigator_w_folderze(wybrany: str) -> Navigator:
    """Nawigator stojacy w FOLDERZE Biblioteki, z zaznaczonym DALSZYM wierszem."""
    nav = Navigator()
    nav.apply_folder(FOLDER, _rows("plik", 5))
    state = nav.sessions[SessionId.FILES]
    state.model.select_id(wybrany)
    assert state.model.selected_id == wybrany
    return nav


def _frame(nav: Navigator, zapamietany_widok: str):
    """Okno bez wx: prawdziwy ``_dispatch`` i ``_run``, atrapy tylko na wejsciu
    do SQLite i na dwoch wykonawcach, zeby odczytac ZLECENIE."""
    frame = gui.LiteFrame.__new__(gui.LiteFrame)
    frame.navigator = nav
    powiedziane: list[str] = []
    frame.announcer = SimpleNamespace(say=powiedziane.append)
    frame.library = SimpleNamespace(
        is_available=True,
        describe=lambda: "",
        saved_library_view=lambda: zapamietany_widok,
    )
    foldery: list[tuple[str | None, str | None]] = []
    widoki: list[OpenLibraryView] = []
    frame._open_library = lambda folder, preferred_id=None: foldery.append((folder, preferred_id))
    frame._open_library_view = widoki.append
    # ``_run`` jest PRAWDZIWE (ono rozpoznaje zamiary). Wyciszamy tylko
    # ``_sync_views`` -- to czysto wx-owe odwzorowanie kontrolek, ktorych w WSL
    # nie ma; sama droga zamiarow zostaje nietknieta.
    frame._sync_views = lambda: None
    # Zadnego odtwarzania przy Ctrl+L: gdyby cokolwiek siegnelo do hosta,
    # ``None`` wysypie test zamiast przepuscic cichy start.
    frame.client = None
    return frame, foldery, widoki, powiedziane


def _ctrl_l(frame) -> None:
    chord = chord_from_event(FakeKeyEvent(ord("l"), ctrl=True))
    assert chord.canonical == "Ctrl+L"
    akcja = resolve(chord, player_view=False, radio_session=False)
    assert akcja is Action.VIEW_LIBRARY, "Ctrl+L musi byc komenda, nie klawiszem listy"
    frame._dispatch(akcja)


# ======================================================== gest i podpis w menu


def test_ctrl_l_jest_komenda_biblioteki() -> None:
    chord = chord_from_event(FakeKeyEvent(ord("l"), ctrl=True))
    assert resolve(chord, player_view=False, radio_session=False) is Action.VIEW_LIBRARY


def test_menu_biblioteka_ma_pozycje_z_ctrl_l() -> None:
    """MainWindow.xaml:427 ma te pozycje w menu; port jej nie mial."""
    library = next(m for m in menu_model.build_menus() if m.title == "&Biblioteka")
    podpisy = {i.action: i.shortcut for i in library.items if not i.is_separator}
    assert podpisy.get(Action.VIEW_LIBRARY) == "Ctrl+L"


def test_pomoc_opisuje_ctrl_l() -> None:
    assert "Ctrl+L" in dict(describe())


# =================================================== DROGA 1: z Ulubionych


def test_ctrl_l_z_ulubionych_wraca_do_zapamietanego_folderu_na_swoj_wiersz() -> None:
    """cs:701-705 + cs:18095 -- zapamietany widok ORAZ zapamietane zaznaczenie."""
    nav = _navigator_w_folderze("plik:4")
    nav.apply_library_view(LibraryView.FAVORITES, "Biblioteka — Ulubione", _rows("ulub", 3))
    nav.sessions[SessionId.FILES].model.select_id("ulub:2")

    frame, foldery, widoki, powiedziane = _frame(nav, LIBRARY_VIEW_FOLDERS)
    _ctrl_l(frame)

    assert widoki == []
    assert foldery == [(FOLDER, "plik:4")], (
        "Ctrl+L ma wrocic do FOLDERU, z ktorego weszlismy, i na jego wiersz"
    )
    assert powiedziane == []


def test_ctrl_l_z_ulubionych_wraca_do_wszystkich_plikow_gdy_tak_zapamietano() -> None:
    """``_state.LocalMedia.LibraryView == "Wszystkie pliki"`` (cs:65, :704)."""
    nav = Navigator()
    nav.apply_library_view(LibraryView.ALL_FILES, "Biblioteka — Wszystkie pliki", _rows("wsz", 6))
    nav.sessions[SessionId.FILES].model.select_id("wsz:5")
    nav.apply_library_view(LibraryView.FAVORITES, "Biblioteka — Ulubione", _rows("ulub", 3))

    frame, foldery, widoki, powiedziane = _frame(nav, LIBRARY_VIEW_ALL_FILES)
    _ctrl_l(frame)

    assert foldery == []
    assert [(w.view, w.preferred_id) for w in widoki] == [(LibraryView.ALL_FILES, "wsz:5")]


# ====================================================== DROGA 2: z Historii


def test_ctrl_l_z_historii_wraca_do_zapamietanego_folderu_na_swoj_wiersz() -> None:
    """Druga zgloszona droga. Historia to osobny widok (``LibraryView.HISTORY``),
    wiec jej wyjscie trzeba zmierzyc osobno, nie "przez analogie"."""
    nav = _navigator_w_folderze("plik:3")
    nav.apply_library_view(LibraryView.HISTORY, "Biblioteka — Historia odtwarzania",
                           _rows("hist", 4))
    nav.sessions[SessionId.FILES].model.select_id("hist:4")

    frame, foldery, widoki, powiedziane = _frame(nav, LIBRARY_VIEW_FOLDERS)
    _ctrl_l(frame)

    assert widoki == []
    assert foldery == [(FOLDER, "plik:3")]
    assert powiedziane == []


def test_ctrl_l_z_historii_wraca_do_wszystkich_plikow_gdy_tak_zapamietano() -> None:
    nav = Navigator()
    nav.apply_library_view(LibraryView.ALL_FILES, "Biblioteka — Wszystkie pliki", _rows("wsz", 6))
    nav.sessions[SessionId.FILES].model.select_id("wsz:2")
    nav.apply_library_view(LibraryView.HISTORY, "Biblioteka — Historia odtwarzania",
                           _rows("hist", 4))

    frame, foldery, widoki, powiedziane = _frame(nav, LIBRARY_VIEW_ALL_FILES)
    _ctrl_l(frame)

    assert foldery == []
    assert [(w.view, w.preferred_id) for w in widoki] == [(LibraryView.ALL_FILES, "wsz:2")]


# ================================================= granice: co NIE ma sie stac


def test_ctrl_l_nie_odtwarza() -> None:
    """``ShowView`` to NAWIGACJA. Gdyby Ctrl+L wolal hosta, ``client=None``
    wysypaloby test -- a brak zlecen odtwarzania potwierdzamy wprost."""
    nav = _navigator_w_folderze("plik:2")
    nav.apply_library_view(LibraryView.FAVORITES, "Biblioteka — Ulubione", _rows("ulub", 3))
    frame, foldery, widoki, _ = _frame(nav, LIBRARY_VIEW_FOLDERS)
    zagrane: list[object] = []
    frame._play_track = zagrane.append
    frame._play_from_queue = zagrane.append
    _ctrl_l(frame)
    assert zagrane == []
    assert len(foldery) + len(widoki) == 1


def test_niewspierany_zapamietany_widok_mowi_prawde() -> None:
    """"Kolejność własna" (cs:66) nie jest przeniesiona. Nie udajemy zgodnosci:
    zadnej listy nie przestawiamy i mowimy, czego brakuje."""
    nav = _navigator_w_folderze("plik:1")
    nav.apply_library_view(LibraryView.FAVORITES, "Biblioteka — Ulubione", _rows("ulub", 3))
    frame, foldery, widoki, powiedziane = _frame(nav, "Kolejność własna")
    _ctrl_l(frame)
    assert foldery == []
    assert widoki == []
    assert len(powiedziane) == 1
    assert "Kolejność własna" in powiedziane[0]


def test_brak_bazy_nie_udaje_powrotu() -> None:
    nav = _navigator_w_folderze("plik:1")
    nav.apply_library_view(LibraryView.FAVORITES, "Biblioteka — Ulubione", _rows("ulub", 3))
    frame, foldery, widoki, powiedziane = _frame(nav, LIBRARY_VIEW_FOLDERS)
    frame.library = SimpleNamespace(
        is_available=False,
        describe=lambda: "Nie widze bazy Biblioteki",
        saved_library_view=lambda: LIBRARY_VIEW_FOLDERS,
    )
    _ctrl_l(frame)
    assert foldery == [] and widoki == []
    assert powiedziane == ["Nie widze bazy Biblioteki"]


def test_blad_odczytu_zapamietanego_widoku_nie_odcina_biblioteki() -> None:
    """Jak przy ``saved_folder`` (gui.py:1661-1668): blad zapisu profilu nie moze
    zabrac calej Biblioteki -- mowimy i wracamy do Folderow."""
    nav = _navigator_w_folderze("plik:2")
    nav.apply_library_view(LibraryView.HISTORY, "Biblioteka — Historia odtwarzania",
                           _rows("hist", 2))
    frame, foldery, widoki, powiedziane = _frame(nav, LIBRARY_VIEW_FOLDERS)

    def wybuchnij():
        raise OSError("baza zajeta")

    frame.library = SimpleNamespace(
        is_available=True, describe=lambda: "", saved_library_view=wybuchnij
    )
    _ctrl_l(frame)
    assert foldery == [(FOLDER, "plik:2")]
    assert len(powiedziane) == 1 and "baza zajeta" in powiedziane[0]


def test_ctrl_l_nie_psuje_backspace_z_widoku() -> None:
    """Czwarte punkty i Backspace zostaja jak byly: wyjscie z Ulubionych nadal
    idzie przez ``_leave_library_view``, a nie przez nowa droge Ctrl+L."""
    nav = _navigator_w_folderze("plik:2")
    nav.apply_library_view(LibraryView.FAVORITES, "Biblioteka — Ulubione", _rows("ulub", 3))
    plan = nav.go_to_parent()
    assert [type(i) for i in plan] == [OpenFolder]
