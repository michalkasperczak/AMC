"""Regresje niezależnego Radia oraz Plików/Podcastów w LiteHost."""

from pathlib import Path


HOST = (
    Path(__file__).resolve().parents[2]
    / "src"
    / "AccessibleMediaController.LiteHost"
    / "LiteEngineHandlers.cs"
).read_text(encoding="utf-8")


def _between(start: str, end: str) -> str:
    return HOST[HOST.index(start) : HOST.index(end, HOST.index(start) + len(start))]


def test_starting_files_does_not_stop_radio() -> None:
    play_file = _between(
        "private object PlayFile(JsonElement args, LiteEventSink events)",
        "private object PlayMedia(JsonElement args, LiteEventSink events)",
    )
    assert "_radio.Stop()" not in play_file


def test_starting_radio_does_not_stop_files_or_detach_their_queue() -> None:
    play_radio = _between(
        "private object PlayStation(JsonElement args, LiteEventSink events)",
        "private LiteTransportTarget ResolveTransportTarget(JsonElement args)",
    )
    assert "_files.Stop()" not in play_radio
    assert "_queue.DetachFromDirectPlay()" not in play_radio


def test_stopping_files_does_not_stop_radio() -> None:
    stop_files = _between(
        "private object StopFilesPlayback()",
        "private object StopRadioPlayback()",
    )
    assert "_radio.Stop()" not in stop_files


def test_each_transport_operation_resolves_explicit_session() -> None:
    assert '["transport.pauseResume"] = (request, _) => PauseResume(request.Args)' in HOST
    assert '["transport.stop"] = (request, _) => StopTransport(request.Args)' in HOST
    assert '["transport.status"] = (request, _) => Status(request.Args)' in HOST
    for signature in (
        "private object PauseResume(JsonElement args)",
        "private object Seek(JsonElement args)",
        "private object SetVolume(JsonElement args)",
        "private object SetRate(JsonElement args)",
        "private object Status(JsonElement args)",
    ):
        start = HOST.index(signature)
        body = HOST[start : start + 1200]
        assert "ResolveTransportTarget(args)" in body, signature


def test_shared_decoder_remembers_the_actual_session_before_switching() -> None:
    assert 'var sessionId = LiteArgs.ReadText(args, "sessionId")?.Trim() ?? "podcasts";' in HOST
    assert '_filesSessionId = sessionId;' in HOST
    assert '_filesSessionId = "files";' in HOST
    assert 'string.Equals(_filesSessionId, sessionId, StringComparison.Ordinal)' in HOST
    assert 'RememberFilesSession();' in _between(
        'private object PlayMedia(JsonElement args, LiteEventSink events)',
        'private object PlayStation(JsonElement args, LiteEventSink events)',
    )
    pause = _between(
        'private object PauseResumeResolved(LiteTransportTarget target)',
        'private string BuildCurrentRadioBroadcastText(MediaItem item)',
    )
    assert '_filesSessions.TryGetValue(target.SessionId, out remembered)' in pause
    assert 'ConfigureOutputForSession(target.SessionId);' in pause
    assert '_files.Play(remembered.Item, remembered.Position,' in pause
