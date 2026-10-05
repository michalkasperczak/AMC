"""Guard early player shortcuts; native Win32 processing eats KEY_DOWN first.

The real RED was measured on a wx.Button with NVDA and keybd_event:
Right moved focus to volume and Ctrl+E stayed silent. AST extraction tests the
small method without importing wx on Linux; the live cycle is still required.
"""
import ast
from pathlib import Path
from types import SimpleNamespace
from typing import Any

SOURCE = Path(__file__).parents[1] / "amc_wx_lite" / "gui.py"

def _call(*, player=True, arrow=True, foreign=False, time_key=False):
    tree = ast.parse(SOURCE.read_text(encoding="utf-8"))
    methods: list[ast.stmt] = [n for n in ast.walk(tree) if isinstance(n, ast.FunctionDef)
               and n.name == "_on_player_shortcut_hook"]
    assert len(methods) == 1, "early player arrow hook is missing"
    view = SimpleNamespace(PLAYER=object(), LIST=object())
    actions = []
    frame = SimpleNamespace(navigator=SimpleNamespace(view=view.PLAYER if player else view.LIST),
                            _on_key=lambda event: actions.append("key" if arrow or time_key else "skip"))
    focus = SimpleNamespace(owner=object() if foreign else frame)
    wx = SimpleNamespace(KeyEvent=object, WXK_LEFT=1, WXK_RIGHT=2, WXK_UP=3, WXK_DOWN=4,
                         Window=SimpleNamespace(FindFocus=lambda: focus),
                         GetTopLevelParent=lambda focus: focus.owner)
    event = SimpleNamespace(GetKeyCode=lambda: 69 if time_key else 2 if arrow else 9,
                            Skip=lambda: actions.append("skip"))
    ns: dict[str, Any] = {"wx": wx, "View": view}
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

def test_strzalka_w_modalnym_dialogu_nie_steruje_odtwarzaczem():
    assert _call(foreign=True) == ["skip"]

def test_hook_jest_podpiety_w_zwyklym_oknie():
    tree = ast.parse(SOURCE.read_text(encoding="utf-8"))
    method = next(n for n in ast.walk(tree) if isinstance(n, ast.FunctionDef) and n.name == "_bind_keys")
    calls = [n for n in ast.walk(method) if isinstance(n, ast.Call)
             and ast.unparse(n.func) == "self.Bind"
             and [ast.unparse(x) for x in n.args] == ["wx.EVT_CHAR_HOOK", "self._on_player_shortcut_hook"]]
    assert len(calls) == 1
