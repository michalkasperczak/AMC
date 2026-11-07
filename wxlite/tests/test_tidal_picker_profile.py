"""Playlist flow in a truly filtered Public package and deferred GUI delivery."""
import importlib.util
import os
from pathlib import Path
import subprocess
import sys
from types import SimpleNamespace

sys.path.insert(0, str(Path(__file__).resolve().parent))
from test_gui_logic import install_wx_stub
install_wx_stub()

from amc_wx_lite import gui, i18n
from amc_wx_lite.async_gate import BackgroundRunner, StaleResultGate
from amc_wx_lite.list_model import Row
from amc_wx_lite.navigation import Navigator, SessionId


def test_filtered_public_imports_and_opens_tidal_picker_in_fresh_process(tmp_path):
    root = Path(__file__).resolve().parents[1]
    spec = importlib.util.spec_from_file_location("picker_bundle", root / "tools/build_bundle.py")
    builder = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(builder)
    files = builder.copy_python_code(tmp_path, builder.profile_from_id("public"))
    assert "app/amc_wx_lite/service_playlist_dialog.py" in files
    assert not (tmp_path / "app/amc_wx_lite/spotify_playlist_dialog.py").exists()
    assert not (tmp_path / "app/amc_wx_lite/spotify_source.py").exists()
    code = """
import sys
sys.path.insert(0, sys.argv[1])
sys.path.insert(1, sys.argv[2])
from test_gui_logic import install_wx_stub
install_wx_stub()
import amc_wx_lite.gui as gui
from amc_wx_lite import service_playlist_dialog as shared
assert gui._RELEASE_PROFILE.profile_id == 'public'
assert gui.SpotifyPlaylistDialog is shared.ServicePlaylistDialog
assert gui.SpotifyPlaylistChoice is shared.ServicePlaylistChoice
assert 'amc_wx_lite.spotify_playlist_dialog' not in sys.modules
assert 'amc_wx_lite.spotify_source' not in sys.modules
from test_tidal_playlist_gui import test_tidal_album_playlist_creation_and_cached_reopening
test_tidal_album_playlist_creation_and_cached_reopening()
print('FILTERED_PUBLIC_PICKER_OK')
"""
    env = dict(os.environ, AMC_RELEASE_PROFILE="public")
    result = subprocess.run([sys.executable, "-c", code, str(tmp_path / "app"), str(root / "tests")],
                            env=env, capture_output=True, text=True, timeout=30)
    assert result.returncode == 0, result.stderr
    assert "FILTERED_PUBLIC_PICKER_OK" in result.stdout


def test_failed_window_construction_releases_pending_flag_and_allows_cached_retry(monkeypatch):
    queued, spoken, fetches, focus = [], [], [], []
    nav = Navigator()
    nav.active = SessionId.TIDAL
    row = Row("row", "Test track", "track", service_id="tracks:123")
    client = SimpleNamespace(tidal_collection_view=lambda _view: (
        fetches.append(True) or {"items": [{"externalId": "playlists:one", "title": "Ulubione"}]}))
    frame = SimpleNamespace(navigator=nav, client=client,
        runner=BackgroundRunner(StaleResultGate(), to_gui=queued.append, spawn=lambda work: work()),
        state=SimpleNamespace(service_picker_destination_ids={}),
        announcer=SimpleNamespace(say=spoken.append), _playlist_membership_pending=False,
        _selected_action_rows=lambda: [row], _restore_focus_after_dialog=lambda: focus.append(True))

    class Dialog:
        attempts = 0
        def __init__(self, _parent, **_kwargs):
            type(self).attempts += 1
            if self.attempts == 1:
                raise RuntimeError("private error must not be spoken")
        def __enter__(self):
            return self
        def __exit__(self, *_args):
            pass
        def ShowModal(self):
            return gui.wx.ID_CANCEL

    monkeypatch.setattr(gui, "SpotifyPlaylistDialog", Dialog)
    gui.LiteFrame._manage_tidal_playlist_membership(frame)
    assert frame._playlist_membership_pending
    assert len(queued) == 1
    queued.pop()()
    assert not frame._playlist_membership_pending
    assert spoken[-1] == i18n.tr("tidal.playlists.openFailed")
    assert not any("private error" in message for message in spoken)
    gui.LiteFrame._manage_tidal_playlist_membership(frame)
    assert not frame._playlist_membership_pending
    assert Dialog.attempts == 2
    assert len(fetches) == 1
    assert len(focus) == 2


def test_new_tidal_playlist_label_starts_with_its_name(monkeypatch):
    from amc_wx_lite import service_playlist_dialog as shared
    class NameDialog:
        result_name = "Ulubione nowe"
        result_public = False
        def __init__(self, _parent, *, provider):
            assert provider == "TIDAL"
        def __enter__(self):
            return self
        def __exit__(self, *_args):
            pass
        def ShowModal(self):
            return gui.wx.ID_OK
    monkeypatch.setattr(shared, "SpotifyPlaylistNameDialog", NameDialog)
    monkeypatch.setattr(shared, "announce_status", lambda *_args: None)
    dialog = object.__new__(shared.ServicePlaylistDialog)
    dialog.provider = "TIDAL"
    dialog._temporary_counter = 0
    dialog._choices = []
    dialog.status = object()
    dialog.playlist_list = SimpleNamespace(SetFocus=lambda: None)
    dialog._rebuild = lambda _id: None
    dialog._create()
    assert dialog._choices[0].label.startswith("Ulubione nowe,")
