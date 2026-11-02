"""Guard early player shortcuts; native Win32 processing eats KEY_DOWN first.

The real RED was measured on a wx.Button with NVDA and keybd_event:
Right moved focus to volume and Ctrl+E stayed silent. AST extraction tests the
small method without importing wx on Linux; the live cycle is still required.
"""
import ast
from functools import lru_cache
from pathlib import Path
from types import SimpleNamespace
from typing import Any

from amc_wx_lite.shortcuts import (
    ASSIGN_PRESET_SHORTCUTS, GLOBAL_VIEW_SHORTCUTS, PRESET_SHORTCUTS,
)

SOURCE = Path(__file__).parents[1] / "amc_wx_lite" / "gui.py"


@lru_cache(maxsize=1)
def _source_tree() -> ast.Module:
    """Parse the large GUI once; individual key cases only execute the hook."""

    return ast.parse(SOURCE.read_text(encoding="utf-8"))

def _call(*, player=True, arrow=True, foreign=False, time_key=False,
          in_filter=False, list_chord="Tab", session="files",
          screen_reader_modifier=False):
    tree = _source_tree()
    methods: list[ast.stmt] = [n for n in ast.walk(tree) if isinstance(n, ast.FunctionDef)
               and n.name == "_on_player_shortcut_hook"]
    assert len(methods) == 1, "early player arrow hook is missing"
    view = SimpleNamespace(PLAYER=object(), LIST=object())
    actions = []
    # Pole filtra MUSI byc w atrapie, bo hook bramkuje je PRZED reszta logiki
    # (gui.py, bramka ``FindFocus() is self.filter_box``). Atrapa bez tego pola
    # nie mierzyla zachowania, tylko wywalala sie na AttributeError.
    filter_box = SimpleNamespace(owner=None)
    frame = SimpleNamespace(navigator=SimpleNamespace(
                                view=view.PLAYER if player else view.LIST,
                                active=SimpleNamespace(value=session),
                                session=SimpleNamespace(library_view=None)),
                            filter_box=filter_box,
                            _screen_reader_modifier_down=lambda: screen_reader_modifier,
                            _show_context_menu=lambda _control, *, player: actions.append(
                                "context-player" if player else "context-list"
                            ),
                            _on_filter_key=lambda event, *a, **k: actions.append("filter"),
                            _on_key=lambda event, *a, **k: actions.append("key" if arrow or time_key else "skip"))
    filter_box.owner = frame
    focus = filter_box if in_filter else SimpleNamespace(owner=object() if foreign else frame)
    wx = SimpleNamespace(KeyEvent=object, WXK_LEFT=1, WXK_RIGHT=2, WXK_UP=3, WXK_DOWN=4,
                         Window=SimpleNamespace(FindFocus=lambda: focus),
                         GetTopLevelParent=lambda focus: focus.owner)
    event = SimpleNamespace(GetKeyCode=lambda: 69 if time_key else 2 if arrow else 9,
                            Skip=lambda: actions.append("skip"))
    ns: dict[str, Any] = {
        "wx": wx,
        "View": view,
        "GLOBAL_VIEW_SHORTCUTS": GLOBAL_VIEW_SHORTCUTS,
        "PRESET_SHORTCUTS": PRESET_SHORTCUTS,
        "ASSIGN_PRESET_SHORTCUTS": ASSIGN_PRESET_SHORTCUTS,
        "chord_from_event": lambda _event: SimpleNamespace(canonical=list_chord),
    }
    exec(compile(ast.Module(body=methods, type_ignores=[]), str(SOURCE), "exec"), ns)
    ns["_on_player_shortcut_hook"](frame, event)
    return actions

def test_strzalka_odtwarzacza_przechodzi_raz_przed_nawigacja_dialogu():
    assert _call() == ["key"]

def test_ctrl_e_dociera_do_tej_samej_obslugi_przed_dialogiem():
    assert _call(arrow=False, time_key=True) == ["key"]

def test_lista_i_tabulator_zachowuja_natywna_nawigacje():
    assert _call(player=False) == ["skip"]
    assert _call(arrow=False) == ["skip"]

def test_f2_i_delete_listy_docieraja_do_amc_przed_natywna_kontrolka():
    for chord in ("F2", "Shift+F2", "Delete", "Shift+Delete"):
        assert _call(player=False, list_chord=chord) == ["key"]


def test_f5_i_ctrl_f5_nie_gina_w_natywnej_liscie() -> None:
    for chord in ("F5", "Ctrl+F5"):
        assert _call(player=False, list_chord=chord) == ["key"]


def test_shift_f10_otwiera_menu_odtwarzacza_przed_obsluga_przycisku():
    assert _call(player=True, list_chord="Shift+F10") == ["context-player"]


def test_space_dociera_do_transportu_z_kazdej_listy_bez_psucia_ctrl_space():
    for session in (
        "files", "radio", "podcasts", "sonos", "wiim", "frontierSmart",
        "radioarchiwista", "spotify", "tidal",
    ):
        assert _call(player=False, list_chord="Space", session=session) == ["key"]
        assert _call(player=False, list_chord="Ctrl+Space", session=session) == ["skip"]


def test_wewnetrzny_hook_listy_przekazuje_tylko_gola_spacje():
    method = next(
        node for node in ast.walk(_source_tree())
        if isinstance(node, ast.FunctionDef)
        and node.name == "_on_list_shortcut_hook"
    )
    for chord, expected in (("Space", "key"), ("Ctrl+Space", "skip")):
        actions: list[str] = []
        frame = SimpleNamespace(_on_key=lambda _event: actions.append("key"))
        ns: dict[str, Any] = {
            "chord_from_event": lambda _event: SimpleNamespace(canonical=chord),
            "GLOBAL_VIEW_SHORTCUTS": GLOBAL_VIEW_SHORTCUTS,
            "PRESET_SHORTCUTS": PRESET_SHORTCUTS,
            "ASSIGN_PRESET_SHORTCUTS": ASSIGN_PRESET_SHORTCUTS,
        }
        exec(compile(ast.Module(body=[method], type_ignores=[]), str(SOURCE), "exec"), ns)
        ns["_on_list_shortcut_hook"](
            frame, SimpleNamespace(Skip=lambda: actions.append("skip"))
        )
        assert actions == [expected]


def test_modyfikowane_polecenia_listy_nie_gina_w_wyszukiwaniu_windows():
    for chord in (
        "Ctrl+Shift+U",
        "Ctrl+Shift+L",
        "Ctrl+Shift+Q",
        "Ctrl+Shift+Return",
        "Ctrl+Shift+H",
        "Ctrl+R",
        "Shift+R",
        "Ctrl+,",
    ):
        assert _call(player=False, list_chord=chord) == ["key"]


def test_sortowanie_spotify_nie_wydaje_systemowego_sygnalu_przed_obsluga_amc():
    for chord in ("Alt+1", "Alt+2", "Alt+3"):
        assert _call(player=False, list_chord=chord, session="spotify") == ["key"]


def test_wszystkie_presety_docieraja_z_listy_przed_natywna_kontrolka():
    """Zwlaszcza Ctrl+Shift+0 nie moze zginac w SysListView32.

    Sama tablica resolvera nie wystarcza: ten konkretny gest byl juz osobno
    chroniony niskopoziomowo w starym AMC, a natywna lista wx potrafi go
    skonsumowac przed EVT_KEY_DOWN.
    """
    for chord in PRESET_SHORTCUTS:
        assert _call(player=False, list_chord=chord) == ["key"]


def test_ctrl_c_is_stopped_by_the_top_level_hook_before_native_copy() -> None:
    """CHAR_HOOK okna musi zatrzymać gest przed SysListView32."""
    for chord in ("Ctrl+C", "Ctrl+Shift+C"):
        assert _call(player=False, list_chord=chord) == ["key"]


def test_lista_ma_wlasny_wczesny_hook_dla_copy_shift_r_i_ustawien():
    tree = _source_tree()
    bind = next(
        node for node in ast.walk(tree)
        if isinstance(node, ast.FunctionDef) and node.name == "_bind_list"
    )
    calls = [
        node for node in ast.walk(bind)
        if isinstance(node, ast.Call)
        and ast.unparse(node.func) == "control.Bind"
        and [ast.unparse(arg) for arg in node.args]
        == ["wx.EVT_CHAR_HOOK", "self._on_list_shortcut_hook"]
    ]
    assert len(calls) == 1

    shortcut_hook = next(
        node for node in ast.walk(tree)
        if isinstance(node, ast.FunctionDef) and node.name == "_on_list_shortcut_hook"
    )
    handled = {
        node.value
        for node in ast.walk(shortcut_hook)
        if isinstance(node, ast.Constant) and isinstance(node.value, str)
    }
    for chord in (
        "F2", "Shift+F2", "Delete", "Shift+Delete", "Ctrl+C", "F5", "Ctrl+F5"
    ):
        assert chord in handled

def test_strzalka_w_modalnym_dialogu_nie_steruje_odtwarzaczem():
    assert _call(foreign=True) == ["skip"]

def test_fokus_w_polu_filtra_oddaje_klawisz_jego_wlasnej_obsludze():
    # Przypadek ROZROZNIAJACY dla bramki z 6af38115: nawet w odtwarzaczu i nawet
    # dla strzalki fokus w polu filtra oznacza, ze klawisz nalezy do pola.
    # Bez bramki wynik byl ["key"] (nawigacja globalna porywala Enter/Backspace).
    assert _call(in_filter=True) == ["filter"]
    assert _call(in_filter=True, player=False, arrow=False) == ["filter"]


def test_gesty_nvda_nie_steruja_odtwarzaczem() -> None:
    for chord in ("Up", "Down", "End"):
        assert _call(
            player=True,
            list_chord=chord,
            screen_reader_modifier=True,
        ) == ["skip"]


def test_zwykle_klawisze_odtwarzacza_zachowuja_swoje_polecenia() -> None:
    for chord in ("Up", "Down", "End"):
        assert _call(player=True, list_chord=chord) == ["key"]


def test_ochrona_nvda_sprawdza_wszystkie_modyfikatory_starego_amc() -> None:
    tree = _source_tree()
    method = next(
        node for node in ast.walk(tree)
        if isinstance(node, ast.FunctionDef)
        and node.name == "_screen_reader_modifier_down"
    )
    constants = {
        node.value
        for node in ast.walk(method)
        if isinstance(node, ast.Constant) and isinstance(node.value, int)
    }
    assert {0x2D, 0x60, 0x14} <= constants


def _volume_key(chord: str, current: int = 50) -> list[int]:
    tree = _source_tree()
    method = next(
        node for node in ast.walk(tree)
        if isinstance(node, ast.FunctionDef)
        and node.name == "_on_player_shortcut_hook"
    )
    view = SimpleNamespace(PLAYER=object())
    values: list[int] = []
    slider = SimpleNamespace(owner=None, GetValue=lambda: current)
    rate_choice = SimpleNamespace(owner=None)
    filter_box = SimpleNamespace(owner=None)
    frame = SimpleNamespace(
        navigator=SimpleNamespace(view=view.PLAYER),
        filter_box=filter_box,
        volume_slider=slider,
        rate_choice=rate_choice,
        _screen_reader_modifier_down=lambda: False,
        _set_volume=values.append,
        _on_filter_key=lambda _event: None,
        _on_key=lambda _event: None,
    )
    slider.owner = frame
    rate_choice.owner = frame
    filter_box.owner = frame
    wx = SimpleNamespace(
        KeyEvent=object,
        Window=SimpleNamespace(FindFocus=lambda: slider),
        GetTopLevelParent=lambda focus: focus.owner,
    )
    ns: dict[str, Any] = {
        "wx": wx,
        "View": view,
        "GLOBAL_VIEW_SHORTCUTS": GLOBAL_VIEW_SHORTCUTS,
        "PRESET_SHORTCUTS": PRESET_SHORTCUTS,
        "chord_from_event": lambda _event: SimpleNamespace(canonical=chord),
    }
    exec(compile(ast.Module(body=[method], type_ignores=[]), str(SOURCE), "exec"), ns)
    ns["_on_player_shortcut_hook"](frame, SimpleNamespace(Skip=lambda: None))
    return values


def test_suwak_glosnosci_ma_intuicyjne_kierunki_i_krawedzie() -> None:
    assert _volume_key("Up") == [51]
    assert _volume_key("Right") == [51]
    assert _volume_key("Down") == [49]
    assert _volume_key("Left") == [49]
    assert _volume_key("Prior") == [60]
    assert _volume_key("Next") == [40]
    assert _volume_key("Home") == [0]
    assert _volume_key("End") == [100]

def test_hook_jest_podpiety_w_zwyklym_oknie():
    tree = _source_tree()
    method = next(n for n in ast.walk(tree) if isinstance(n, ast.FunctionDef) and n.name == "_bind_keys")
    calls = [n for n in ast.walk(method) if isinstance(n, ast.Call)
             and ast.unparse(n.func) == "self.Bind"
             and [ast.unparse(x) for x in n.args] == ["wx.EVT_CHAR_HOOK", "self._on_player_shortcut_hook"]]
    assert len(calls) == 1
