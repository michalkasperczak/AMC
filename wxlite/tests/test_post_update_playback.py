"""Post-update playback is a one-shot, validated handoff."""

from types import SimpleNamespace

from amc_wx_lite import gui
from amc_wx_lite.navigation import SessionId
from amc_wx_lite.post_update_playback import normalize_snapshot
from amc_wx_lite.state_store import LiteState, StateStore


def test_snapshot_accepts_concurrent_radio_and_local_file(tmp_path) -> None:
    entries = [
        dict(sessionId="radio", source="https://example.org/stream",
             id="station-1", title="Stacja", playing=True, volume=34,
             rate=1.0, positionSeconds=30.0),
        dict(sessionId="files", source=r"D:\Muzyka\piosenka.mp3",
             id="track-1", title="Piosenka", playing=True, volume=51,
             rate=1.25, positionSeconds=42.5, muted=True),
    ]
    store = StateStore(tmp_path)
    store.save(LiteState(playback_before_update=entries))
    loaded = store.load().playback_before_update
    assert [entry["sessionId"] for entry in loaded] == ["radio", "files"]
    assert loaded[1]["positionSeconds"] == 42.5
    assert loaded[1]["muted"] is True


def test_snapshot_rejects_stopped_missing_and_untrusted_entries() -> None:
    assert normalize_snapshot([
        dict(sessionId="radio", source="https://example.org/live", id="one",
             title="Nie gra", playing=False),
        dict(sessionId="radio", source="file:///wrong", id="two",
             title="Zła stacja", playing=True),
        dict(sessionId="files", source=r"D:\a.mp3", id="three",
             title="Błędna prędkość", playing=True, rate=float("nan")),
        dict(sessionId="spotify", source="spotify:track:one", id="four",
             title="Inna usługa", playing=True),
    ]) == []


def test_tidal_snapshot_never_persists_transient_proxy_url() -> None:
    entries = normalize_snapshot([dict(
        sessionId="tidal", source="http://127.0.0.1:12345/private",
        trackId="569367896", id="tidal:tracks:569367896", title="Utwór",
        playing=True, positionSeconds=12.0,
    )])
    assert len(entries) == 1
    assert entries[0]["trackId"] == "569367896"
    assert entries[0]["source"] == ""


def test_close_snapshot_reads_live_transport_and_ignores_stopped_session() -> None:
    frame = gui.LiteFrame.__new__(gui.LiteFrame)
    requests = []

    def status(session):
        requests.append(session)
        return dict(sessionId=session, source="https://example.org/live",
                    id="station-1", title="Stacja", playing=session == "radio")

    frame.client = SimpleNamespace(alive=True, status=status)
    frame.state = LiteState()
    frame._playing_sessions = {SessionId.RADIO, SessionId.FILES}
    gui.LiteFrame._capture_playback_before_close(frame)
    assert requests == ["radio", "files"]
    assert [entry["sessionId"] for entry in frame.state.playback_before_update] == ["radio"]


def test_post_update_launch_consumes_snapshot_and_starts_station(monkeypatch) -> None:
    frame = gui.LiteFrame.__new__(gui.LiteFrame)
    calls = []
    messages = []
    frame.state = LiteState(playback_before_update=[
        dict(sessionId="radio", source="https://example.org/live", id="station-1",
             title="Stacja", playing=True, volume=37, rate=1.0),
    ])
    frame.client = SimpleNamespace(
        play_station=lambda source, **kw: calls.append((source, kw)),
    )
    frame._playing_sessions = set()
    frame._playing_session = None
    frame._save_state = lambda: True
    frame._refresh_status = lambda: None
    frame.announcer = SimpleNamespace(say=messages.append)
    frame.runner = SimpleNamespace(submit=lambda _key, work, done, _failed: done(work()))
    monkeypatch.setenv("AMC_POST_UPDATE_RESUME", "1")
    gui.LiteFrame._resume_playback_after_update(frame)
    assert calls == [("https://example.org/live", {
        "volume": 37, "item_id": "station-1", "title": "Stacja",
    })]
    assert frame.state.playback_before_update == []
    assert frame._playing_sessions == {SessionId.RADIO}
    assert messages == ["Przywrócono odtwarzanie po aktualizacji"]


def test_post_update_launch_reopens_tidal_track(monkeypatch) -> None:
    frame = gui.LiteFrame.__new__(gui.LiteFrame)
    frame.state = LiteState(playback_before_update=[dict(
        sessionId="tidal", trackId="569367896", id="tidal:tracks:569367896",
        title="Utwór", playing=True, volume=45, rate=1.0,
    )])
    frame.client = SimpleNamespace()
    frame._playing_sessions = set()
    frame._playing_session = None
    frame._save_state = lambda: True
    frame.announcer = SimpleNamespace(say=lambda _message: None)
    frame.runner = SimpleNamespace(submit=lambda _key, work, done, _failed: done(work()))
    rows = []
    frame._play_tidal_native_row = lambda row: rows.append(row) or True
    monkeypatch.setenv("AMC_POST_UPDATE_RESUME", "1")
    gui.LiteFrame._resume_playback_after_update(frame)
    assert len(rows) == 1
    assert rows[0].service_id == "tracks:569367896"
    assert frame._tidal_post_update_resume["positionSeconds"] == 0.0
