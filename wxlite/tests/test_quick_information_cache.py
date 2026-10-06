"""Zapisane parametry zaznaczonego elementu, także gdy źródło nie odpowiada."""
import json
import sqlite3
import tempfile
from pathlib import Path

from amc_wx_lite import quick_info
from amc_wx_lite.list_model import Row
from amc_wx_lite.profile_layout import read_only_mirror
from test_wire_compatibility import make_client


def test_cache_radia_zachowuje_parametry_i_kraj_jezyk_bez_zapisu():
    with tempfile.TemporaryDirectory() as tmp:
        root = Path(tmp)
        path = root / "state.json"
        path.write_text(json.dumps({"radio": {"stations": [
            {"id": "other", "streamUrl": "https://invalid/other", "bitrateKbps": 64},
            {"id": "selected", "streamUrl": "https://invalid/selected", "codec": "mp3",
             "bitrateKbps": 192, "sampleRateHz": 44100, "country": "Polska", "language": "polski"}
        ]}}), encoding="utf-8")
        before = path.read_bytes()
        row = Row("selected", "Stacja", "station", url="https://invalid/selected")
        assert hasattr(quick_info, "read_cached_information"), "brak odczytu istniejącego cache"
        data = quick_info.read_cached_information(read_only_mirror(root, root), row, "radio")
        assert data == {"bitrateKbps": 192, "sampleRateHz": 44100, "codec": "mp3",
                        "country": "Polska", "language": "polski"}
        assert path.read_bytes() == before
        changed_url = Row("selected", "Stacja", "station", url="https://invalid/new")
        assert quick_info.read_cached_information(read_only_mirror(root, root), changed_url, "radio") == {}


def test_cache_pliku_czytany_po_tozsamosci_i_bez_filtra_czlonkostwa():
    with tempfile.TemporaryDirectory() as tmp:
        root = Path(tmp)
        db_path = root / "library.db"
        with sqlite3.connect(db_path) as db:
            db.execute("CREATE TABLE local_items(id TEXT, path TEXT, duration_ticks INTEGER, bitrate_kbps INTEGER, sample_rate_hz INTEGER, is_in_library INTEGER)")
            db.execute("INSERT INTO local_items VALUES(?,?,?,?,?,?)", ("selected", "D:\\Muzyka\\Łąka.mp3", 123456789, 320, 48000, 0))
        before = db_path.read_bytes()
        row = Row("selected", "Łąka", "track", path="D:\\Muzyka\\Łąka.mp3")
        assert hasattr(quick_info, "read_cached_information"), "czas nie może być stałym zerem"
        data = quick_info.read_cached_information(read_only_mirror(root, root), row, "files")
        assert data == {"durationTicks": 123456789, "bitrateKbps": 320, "sampleRateHz": 48000}
        assert db_path.read_bytes() == before
        wrong = Row("selected", "Inny plik", "track", path="D:\\Inny.mp3")
        assert quick_info.read_cached_information(read_only_mirror(root, root), wrong, "files") == {}


def test_prawdziwy_formatter_zachowuje_cache_gdy_sonda_nic_nie_dodaje():
    client = make_client()
    try:
        row = Row("selected", "Stacja", "station", url="https://invalid/selected")
        request = quick_info.quick_info_request(row, session="radio")
        assert request is not None
        request.update(bitrateKbps=192, sampleRateHz=44100, codec="mp3", country="Polska", language="polski")
        result = client.call(quick_info.QUICK_INFO_OP, request)
        assert result["text"] == "MP3, 192 kb/s, 44,1 kHz, Polska, polski", result
    finally:
        client.close()


def test_handler_czyta_cache_w_zadaniu_tla_i_przekazuje_do_hosta():
    # Wykonuje rzeczywiste ciało metody bez budowania kontrolek wx.
    import ast
    from types import SimpleNamespace as N
    gui_path = Path(__file__).resolve().parents[1] / "amc_wx_lite" / "gui.py"
    tree = ast.parse(gui_path.read_text(encoding="utf-8"))
    frame = next(node for node in tree.body if isinstance(node, ast.ClassDef) and node.name == "LiteFrame")
    method = next(node for node in frame.body if isinstance(node, ast.FunctionDef) and node.name == "_announce_quick_information")
    calls, jobs, reads = [], [], []
    namespace = dict(vars(quick_info))
    def cached(layout, row, session):
        reads.append((layout, row.item_id, session))
        return {"durationTicks": 123456789, "bitrateKbps": 320, "sampleRateHz": 48000}
    namespace["read_cached_information"] = cached
    exec(compile(ast.Module(body=[method], type_ignores=[]), str(gui_path), "exec"), namespace)
    row = Row("selected", "Plik", "track", path="D:\\x.mp3")
    obj = N(client=N(call=lambda op, request, **kwargs: calls.append((op, request))),
            navigator=N(session=N(model=N(selected_row=row)), active=N(value="files"), view=N(value="list")),
            layout="private-copy", _selected_duration_ticks=lambda row: 0,
            _window_alive=lambda: True, announcer=N(say=lambda text: None),
            runner=N(submit=lambda stream, work, answer, failed: jobs.append(work)))
    namespace["_announce_quick_information"](obj)
    assert not reads and not calls, "odczyt i host nie mogą blokować wątku GUI"
    assert len(jobs) == 1
    jobs[0]()
    assert reads == [("private-copy", "selected", "files")], reads
    assert calls[0][1]["durationTicks"] == 123456789, calls
    assert calls[0][1]["bitrateKbps"] == 320, calls


def test_uri_pliku_nie_jest_zrodlem_zdalnym_dla_formatera():
    client = make_client()
    try:
        result = client.call(quick_info.QUICK_INFO_OP, {
            "session": "files", "itemId": "uri", "source": "file:///D:/Muzyka/test.mp3", "kind": "track"
        })
        assert result["text"] == "MP3", result
    finally:
        client.close()
