"""Testy klienta protokolu.

Dwie warstwy:
 1. Parsowanie wierszy - czyste funkcje, bez procesu.
 2. Rozmowa z PRAWDZIWYM procesem potomnym (skrypt echo w Pythonie), zeby
    sprawdzic watki, kolejnosc odpowiedzi, EOF i zly JSON w strumieniu.

Testem zgodnosci z hostem C# jest osobny zestaw protokolu w .NET
(tests/AccessibleMediaController.LiteHost.ProtocolTests) - ten sam ksztalt
komunikatow sprawdzany po stronie silnika.
"""

from __future__ import annotations

import subprocess
import sys
import tempfile
import time
from pathlib import Path

from amc_wx_lite.host_client import (
    HostError,
    HostUnavailable,
    LiteHostClient,
    Response,
    classify,
    decode_line,
)

# Atrapa SAMEGO TRANSPORTU (nie odtwarzania): mowi tym samym protokolem,
# zeby dalo sie zmierzyc zachowanie klienta bez Windows i bez karty dzwiekowej.
FAKE_HOST = r'''
import json, sys, time
print(json.dumps({"event": "host.ready", "data": {"pid": 1}}), flush=True)
sys.stderr.write("diagnostyka ktora NIE MOZE trafic do parsera\n")
sys.stderr.flush()
print("to nie jest json i ma byc pominiete", flush=True)
for line in sys.stdin:
    line = line.strip()
    if not line:
        continue
    try:
        request = json.loads(line)
    except json.JSONDecodeError:
        print(json.dumps({"id": 0, "error": {"message": "zly json"}}), flush=True)
        continue
    op = request.get("op")
    rid = request.get("id")
    if op == "slow":
        time.sleep(3)
    if op == "boom":
        print(json.dumps({"id": rid, "error": {"message": "nie znalazlem pliku"}}), flush=True)
        continue
    if op == "quit":
        break
    print(json.dumps({"id": rid, "result": {"op": op, "args": request.get("args")}}), flush=True)
    if op == "withEvent":
        print(json.dumps({"event": "playback.started", "data": {"title": "utwor"}}), flush=True)
'''


def fake_host_script() -> Path:
    path = Path(tempfile.gettempdir()) / "amc_wx_lite_fake_host.py"
    path.write_text(FAKE_HOST, encoding="utf-8")
    return path


def make_client(**kwargs) -> LiteHostClient:
    script = fake_host_script()

    def spawn(_cmd, **popen_kwargs):
        popen_kwargs.pop("creationflags", None)
        return subprocess.Popen([sys.executable, str(script)], **popen_kwargs)

    client = LiteHostClient(sys.executable, spawn=spawn, **kwargs)
    client.start()
    return client


# ------------------------------------------------------------- parsowanie


def test_bad_json_line_is_skipped_not_fatal() -> None:
    assert decode_line("{to nie json") is None
    assert decode_line("") is None
    assert decode_line("   ") is None
    assert decode_line("[1,2,3]") is None, "protokol to obiekty, nie listy"
    assert decode_line('{"id":1,"result":{}}') == {"id": 1, "result": {}}


def test_classify_separates_events_from_responses() -> None:
    kind, payload = classify({"event": "playback.started", "data": {"title": "x"}})
    assert kind == "event" and payload == ("playback.started", {"title": "x"})

    # Host C# zapisuje "id" jako NAPIS - to jest wlasciwy kontrakt.
    kind, payload = classify({"id": "7", "result": {"ok": True}})
    assert kind == "response"
    assert isinstance(payload, Response) and payload.request_id == "7" and payload.error is None

    kind, payload = classify({"id": "7", "error": {"message": "nie ma pliku"}})
    assert kind == "response" and payload.error == "nie ma pliku"  # type: ignore[union-attr]

    # Liczba tez ma byc przyjeta i znormalizowana do napisu.
    kind, payload = classify({"id": 7, "result": {}})
    assert kind == "response" and payload.request_id == "7"  # type: ignore[union-attr]

    assert classify({"cos": "innego"})[0] == "ignore"


def test_event_without_data_still_parses() -> None:
    kind, payload = classify({"event": "radio.nowPlaying"})
    assert kind == "event" and payload == ("radio.nowPlaying", {})


# ---------------------------------------------------- rozmowa z procesem


def test_call_returns_result_from_real_child_process() -> None:
    client = make_client()
    try:
        result = client.call("files.listFolder", {"path": "/m"})
        assert result["op"] == "files.listFolder"
        assert result["args"] == {"path": "/m"}
    finally:
        client.close()


def test_play_media_sends_stable_identity_source_and_resume_in_one_request() -> None:
    client = make_client()
    try:
        result = client.play_media(
            "https://example.invalid/watch?v=abc",
            item_id="episode-stable-id",
            title="Rozmowa tygodnia",
            volume=37,
            rate=1.25,
            position_seconds=91.5,
        )
        assert result["op"] == "media.play"
        assert result["args"] == {
            "source": "https://example.invalid/watch?v=abc",
            "id": "episode-stable-id",
            "title": "Rozmowa tygodnia",
            "volume": 37,
            "rate": 1.25,
            "positionSeconds": 91.5,
        }
    finally:
        client.close()


def test_podcast_checkpoint_is_a_narrow_host_operation() -> None:
    client = make_client()
    try:
        result = client.checkpoint_podcast()
        assert result["op"] == "podcast.checkpoint"
        assert result["args"] == {}
    finally:
        client.close()


def test_podcast_refresh_uses_one_narrow_host_operation() -> None:
    client = make_client()
    try:
        current = client.refresh_podcasts("podcast-stable-id")
        all_sources = client.refresh_podcasts()
        assert current["op"] == "podcast.refresh"
        assert current["args"] == {"subscriptionId": "podcast-stable-id"}
        assert all_sources["op"] == "podcast.refresh"
        assert all_sources["args"] == {}
    finally:
        client.close()


def test_podcast_download_uses_one_narrow_host_operation_and_deduplicates_ids() -> None:
    client = make_client()
    try:
        result = client.download_podcast_episodes(["episode-1", "episode-1", "episode-2"])
        assert result["op"] == "podcast.download"
        assert result["args"] == {"episodeIds": ["episode-1", "episode-2"]}
    finally:
        client.close()


def test_podcast_favorite_uses_one_narrow_operation_and_separates_item_kinds() -> None:
    client = make_client()
    try:
        result = client.toggle_podcast_favorites(
            ["podcast-1", "podcast-1", "podcast-2"],
            ["episode-1", "episode-1"],
        )
        assert result["op"] == "podcast.toggleFavorite"
        assert result["args"] == {
            "subscriptionIds": ["podcast-1", "podcast-2"],
            "episodeIds": ["episode-1"],
        }
    finally:
        client.close()


def test_podcast_playback_options_use_named_read_and_write_operations() -> None:
    client = make_client()
    try:
        read = client.podcast_playback_options("episode-1", "episode")
        saved = client.set_podcast_playback_options(
            "episode-1",
            "episode",
            {
                "resumePositionMode": 1,
                "playbackRate": 1.0,
                "loudnessNormalization": None,
                "smoothTrackTransitions": True,
                "interTrackSilenceMs": 0,
                "tempoAlgorithm": 1,
            },
        )
        assert read["op"] == "podcast.playbackOptions"
        assert read["args"] == {"itemId": "episode-1", "target": "episode"}
        assert saved["op"] == "podcast.playbackOptions.set"
        assert saved["args"] == {
            "itemId": "episode-1",
            "target": "episode",
            "resumePositionMode": 1,
            "playbackRate": 1.0,
            "loudnessNormalization": None,
            "smoothTrackTransitions": True,
            "interTrackSilenceMs": 0,
            "tempoAlgorithm": 1,
        }
    finally:
        client.close()


def test_podcast_save_as_uses_separate_info_and_copy_operations() -> None:
    client = make_client()
    try:
        info = client.podcast_save_as_info("episode-1")
        saved = client.save_podcast_episode_as(
            "episode-1", "D:/wymiana/Wybrany odcinek.mp3"
        )
        assert info["op"] == "podcast.downloadSaveAsInfo"
        assert info["args"] == {"episodeId": "episode-1"}
        assert saved["op"] == "podcast.downloadSaveAs"
        assert saved["args"] == {
            "episodeId": "episode-1",
            "path": "D:/wymiana/Wybrany odcinek.mp3",
        }
    finally:
        client.close()


def test_podcast_add_uses_one_narrow_host_operation() -> None:
    client = make_client()
    try:
        result = client.add_podcast_source(
            "https://example.invalid/feed.xml", "Moja nazwa"
        )
        assert result["op"] == "podcast.add"
        assert result["args"] == {
            "address": "https://example.invalid/feed.xml",
            "title": "Moja nazwa",
        }
    finally:
        client.close()


def test_podcast_opml_uses_named_narrow_host_operations() -> None:
    client = make_client()
    try:
        inspected = client.inspect_podcast_opml("D:/wymiana/podcasty.opml")
        imported = client.import_podcast_opml(
            "D:/wymiana/podcasty.opml",
            ["https://example.invalid/a.xml", "https://example.invalid/a.xml"],
        )
        exported = client.export_podcast_opml("D:/wymiana/eksport.opml")
        youtube = client.export_youtube_subscriptions("D:/wymiana/youtube.csv")
        assert inspected["op"] == "podcast.opml.inspect"
        assert inspected["args"] == {"path": "D:/wymiana/podcasty.opml"}
        assert imported["op"] == "podcast.opml.import"
        assert imported["args"] == {
            "path": "D:/wymiana/podcasty.opml",
            "feedUrls": ["https://example.invalid/a.xml"],
        }
        assert exported["op"] == "podcast.opml.export"
        assert exported["args"] == {"path": "D:/wymiana/eksport.opml"}
        assert youtube["op"] == "podcast.youtube.export"
        assert youtube["args"] == {"path": "D:/wymiana/youtube.csv"}
    finally:
        client.close()


def test_profile_mutations_use_narrow_named_operations() -> None:
    client = make_client()
    try:
        calls = [
            client.rename_library_item("local-1", "Nowa nazwa"),
            client.rename_local_file("local-1", "nowy plik"),
            client.rename_podcast_subscription("podcast-1", "Nowy podcast"),
            client.edit_radio_station(
                "radio-1", "Nowe radio", "https://example.invalid/radio"
            ),
            client.remove_profile_items("files", "favorites", ["local-1"]),
            client.recycle_local_files(["local-1"]),
        ]
        assert [call["op"] for call in calls] == [
            "library.renameTitle",
            "library.renameFile",
            "podcast.renameSubscription",
            "radio.editStation",
            "library.remove",
            "library.recycle",
        ]
    finally:
        client.close()


def test_current_radio_information_uses_a_narrow_read_only_operation() -> None:
    client = make_client()
    try:
        result = client.current_radio_broadcast_information()
        assert result["op"] == "radio.currentBroadcastInformation"
        assert result["args"] == {}
    finally:
        client.close()


def test_state_json_path_is_passed_explicitly_to_the_host() -> None:
    client = LiteHostClient(
        "host.exe",
        library_db="library.db",
        podcasts_db="podcasts.db",
        state_json="state.json",
    )
    assert client._command()[-6:] == [
        "--library-db", "library.db",
        "--podcasts-db", "podcasts.db",
        "--state-json", "state.json",
    ]


def test_host_error_is_raised_as_host_error_not_crash() -> None:
    client = make_client()
    try:
        try:
            client.call("boom")
        except HostError as error:
            assert "nie znalazlem pliku" in str(error)
        else:
            raise AssertionError("ZALOZENIE NIESPELNIONE: blad hosta mial podniesc HostError")
        # Po bledzie polaczenie DZIALA dalej.
        assert client.call("ping")["op"] == "ping"
    finally:
        client.close()


def test_garbage_and_stderr_do_not_break_the_stream() -> None:
    # Atrapa wypisuje smiec na stdout i diagnostyke na stderr ZANIM odpowie.
    diagnostics: list[str] = []
    client = make_client(on_stderr=diagnostics.append)
    try:
        assert client.call("ping")["op"] == "ping"
        time.sleep(0.2)
        assert any("diagnostyka" in line for line in diagnostics), "stderr trafia do logu"
    finally:
        client.close()


def test_events_reach_the_handler() -> None:
    events: list[tuple[str, dict]] = []
    client = make_client(on_event=lambda name, data: events.append((name, data)))
    try:
        client.call("withEvent")
        deadline = time.time() + 3
        while time.time() < deadline and not any(n == "playback.started" for n, _ in events):
            time.sleep(0.05)
        assert any(n == "playback.started" for n, _ in events)
    finally:
        client.close()


def test_responses_match_their_own_requests() -> None:
    client = make_client()
    try:
        for index in range(20):
            result = client.call("op", {"n": index})
            assert result["args"]["n"] == index, "odpowiedz nalezy do wlasnego zadania"
    finally:
        client.close()


def test_timeout_does_not_wedge_the_client() -> None:
    client = make_client()
    try:
        try:
            client.call("slow", timeout=0.3)
        except HostUnavailable as error:
            assert "nie odpowiedzial" in str(error)
        else:
            raise AssertionError("ZALOZENIE NIESPELNIONE: mial byc limit czasu")
        # Spozniona odpowiedz nie moze zostac wzieta za odpowiedz nastepnego zadania.
        time.sleep(3)
        assert client.call("ping")["op"] == "ping"
    finally:
        client.close()


def test_closing_stdin_ends_the_host() -> None:
    client = make_client()
    client.call("ping")
    client.close()
    assert not client.alive, "host konczy sie na EOF, bez zabijania procesu"


def test_calls_after_close_fail_clearly() -> None:
    client = make_client()
    client.close()
    try:
        client.call("ping")
    except HostUnavailable:
        return
    raise AssertionError("ZALOZENIE NIESPELNIONE: po zamknieciu mial byc jasny blad")


def test_missing_executable_is_reported_before_spawning() -> None:
    client = LiteHostClient("/nie/ma/takiego/amc_lite_host.exe")
    try:
        client.start()
    except HostUnavailable as error:
        assert "Nie znaleziono silnika" in str(error)
        return
    raise AssertionError("ZALOZENIE NIESPELNIONE: brak pliku mial byc zgloszony")


def test_dying_host_unblocks_waiting_callers() -> None:
    client = make_client()
    try:
        client.call("quit", timeout=1.0)
    except (HostUnavailable, HostError):
        pass
    deadline = time.time() + 3
    while time.time() < deadline and client.alive:
        time.sleep(0.05)
    try:
        client.call("ping", timeout=1.0)
    except HostUnavailable:
        return
    finally:
        client.close()
    raise AssertionError("ZALOZENIE NIESPELNIONE: wywolanie do martwego hosta mialo zawiesc")
