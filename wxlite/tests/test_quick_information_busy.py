"""Odmowa przeciążonego odczytu nie udaje awarii ani nie blokuje transportu."""
import ast
from types import SimpleNamespace as N

from amc_wx_lite import quick_info
from amc_wx_lite.host_client import HostError
from amc_wx_lite.list_model import Row
from test_quick_information_gui_wiring import _method, HANDLER
from test_wire_compatibility import make_client


def test_klient_zachowuje_kod_bledu_z_prawdziwego_procesu():
    client = make_client()
    try:
        try:
            client.call("nie.ma.takiej.operacji")
        except HostError as error:
            assert getattr(error, "code", None) == "unknown_op", repr(error)
        else:
            raise AssertionError("Nieznana operacja musi dać błąd hosta")
    finally:
        client.close()


def test_handler_czyta_krotka_odmowe_zamiast_bledu_elementu():
    namespace = dict(vars(quick_info))
    exec(compile(ast.Module(body=[_method(HANDLER)], type_ignores=[]), "gui-handler", "exec"), namespace)
    row = Row("wybrany", "Plik", "track", path="D:\\a.mp3")
    failures, spoken = [], []
    obj = N(client=N(), layout=None,
            navigator=N(session=N(model=N(selected_row=row)), active=N(value="files"), view=N(value="list")),
            _selected_item_id=lambda: "wybrany", _window_alive=lambda: True,
            announcer=N(say=spoken.append),
            runner=N(submit=lambda stream, work, answer, failed: failures.append(failed)))
    namespace[HANDLER](obj)
    assert len(failures) == 1
    error = HostError("Trwa odczyt informacji. Powtórz skrót za chwilę.")
    error.code = "operation_busy"
    failures[0](error)
    assert spoken == [str(error)], spoken
