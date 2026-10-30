"""Podpisy odtwarzacza muszą powstać tuż przed kontrolkami.

Windows (oleacc) nadaje suwakowi i liście wyboru nazwę z poprzedzającego je
podpisu, a skrót z ``&`` przenosi fokus na następną kontrolkę. Podpis tworzony
po kontrolce dawał w NVDA samo "suwak, 40" i "lista rozwijana, 1,00 razy".
Inna kontrolka wstawiona między podpis a suwak zepsułaby to tak samo.
"""

from __future__ import annotations

import ast
from pathlib import Path

GUI = Path(__file__).resolve().parents[1] / "amc_wx_lite" / "gui.py"


def _player_widgets() -> list[tuple[str, str]]:
    """``wx.<Klasa>(self.player_panel, ...)`` z ``_build_ui`` w kolejności tworzenia."""
    tree = ast.parse(GUI.read_text(encoding="utf-8"))
    frame = next(
        node
        for node in ast.walk(tree)
        if isinstance(node, ast.ClassDef) and node.name == "LiteFrame"
    )
    build = next(
        node
        for node in frame.body
        if isinstance(node, ast.FunctionDef) and node.name == "_build_ui"
    )
    widgets = []
    for node in ast.walk(build):
        if (
            isinstance(node, ast.Call)
            and isinstance(node.func, ast.Attribute)
            and isinstance(node.func.value, ast.Name)
            and node.func.value.id == "wx"
            and node.args
            and ast.unparse(node.args[0]) == "self.player_panel"
        ):
            label = next(
                (
                    keyword.value.value
                    for keyword in node.keywords
                    if keyword.arg == "label" and isinstance(keyword.value, ast.Constant)
                ),
                "",
            )
            widgets.append((node.lineno, node.col_offset, node.func.attr, label))
    return [(kind, label) for _line, _column, kind, label in sorted(widgets)]


def test_player_labels_are_created_right_before_their_controls() -> None:
    widgets = _player_widgets()
    kinds = [kind for kind, _label in widgets]

    assert widgets[kinds.index("Slider") - 1] == ("StaticText", "&Głośność:")
    assert widgets[kinds.index("Choice") - 1] == ("StaticText", "&Prędkość:")
