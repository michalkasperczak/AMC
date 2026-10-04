"""Testy ZGODNOSCI klienta Python z PRAWDZIWA petla protokolu hosta w C#.

To nie jest atrapa protokolu: uruchamiamy ten sam ``LiteDispatchLoop``, ktory
pojdzie na Windows, skompilowany na net8.0 i uruchomiony w WSL przez
``dotnet amc_lite_protocol_tests.dll --wire-server``. Handlery nie dotykaja
dzwieku (NAudio wymaga Windows), wiec mierzymy WYLACZNIE warstwe komunikacji -
dokladnie te czesc, ktora na Linuksie da sie sprawdzic uczciwie.

Jesli zbudowanego serwera nie ma, testy sa POMIJANE jawnym komunikatem,
zeby brak buildu nie udawal zielonego wyniku.
"""

from __future__ import annotations

import os
import subprocess
import time
from pathlib import Path

from amc_wx_lite.host_client import HostError, HostUnavailable, LiteHostClient

REPO = Path(__file__).resolve().parents[2]
WIRE_DLL = (
    REPO
    / "tests"
    / "AccessibleMediaController.LiteHost.ProtocolTests"
    / "bin"
    / "Release"
    / "net8.0"
    / "amc_lite_protocol_tests.dll"
)
DOTNET = Path(os.environ.get("DOTNET", "/home/michal/dotnet/dotnet"))


class SkipTest(Exception):
    """Runner traktuje to jako POMINIETE, nie jako zdane."""


def require_wire_server() -> None:
    if not DOTNET.exists():
        raise SkipTest(f"brak dotnet: {DOTNET}")
    if not WIRE_DLL.exists():
        raise SkipTest(f"brak zbudowanego serwera protokolu: {WIRE_DLL}")


def make_client(**kwargs) -> LiteHostClient:
    require_wire_server()

    def spawn(_cmd, **popen_kwargs):
        popen_kwargs.pop("creationflags", None)
        return subprocess.Popen(
            [str(DOTNET), str(WIRE_DLL), "--wire-server"], **popen_kwargs
        )

    client = LiteHostClient(str(DOTNET), spawn=spawn, **kwargs)
    client.start()
    return client


# --------------------------------------------------------- podstawy rozmowy


def test_python_client_talks_to_real_csharp_loop() -> None:
    client = make_client()
    try:
        result = client.hello()
        assert result["host"] == "amc-lite-host"
        assert result["protocol"] == 1
    finally:
        client.close()


def test_request_identifiers_match_across_languages() -> None:
    # Najwazniejszy test zgodnosci: C# zapisuje "id" jako NAPIS, wiec klient
    # Python musi rozpoznawac wlasne odpowiedzi takze w tej postaci.
    client = make_client()
    try:
        for index in range(15):
            result = client.call("echo", {"n": index})
            assert result["args"]["n"] == index, "odpowiedz nalezy do wlasnego zadania"
    finally:
        client.close()


def test_folder_listing_payload_feeds_the_list_model() -> None:
    from amc_wx_lite.list_model import rows_from_folder_payload

    client = make_client()
    try:
        payload = client.list_folder("/muzyka")
        rows = rows_from_folder_payload(payload)
        assert [row.kind for row in rows] == ["folder", "track"]
        assert rows[0].title == "album"
        assert rows[1].path.endswith("a.mp3")
    finally:
        client.close()


def test_status_payload_has_fields_the_player_view_needs() -> None:
    client = make_client()
    try:
        status = client.status()
        for field in ("engine", "paused", "positionSeconds", "durationSeconds", "volume", "rate"):
            assert field in status, f"brak pola {field} w statusie"
    finally:
        client.close()


# ------------------------------------------------------------ granice i bledy


def test_host_clamps_volume_and_rate_to_engine_limits() -> None:
    client = make_client()
    try:
        assert client.call("transport.setVolume", {"volume": 5000})["volume"] == 100
        assert client.call("transport.setVolume", {"volume": -20})["volume"] == 0
        assert client.call("transport.setRate", {"rate": 99})["rate"] == 2.0
        assert client.call("transport.setRate", {"rate": 0.01})["rate"] == 0.5
    finally:
        client.close()


def test_content_error_is_reported_and_host_survives() -> None:
    client = make_client()
    try:
        try:
            client.call("fail")
        except HostError as error:
            assert "nie znalazlem pliku" in str(error)
        else:
            raise AssertionError("ZALOZENIE NIESPELNIONE: oczekiwano HostError")
        assert client.call("echo", {"po": "bledzie"})["args"] == {"po": "bledzie"}
    finally:
        client.close()


def test_unexpected_handler_exception_does_not_kill_the_host() -> None:
    client = make_client()
    try:
        try:
            client.call("crash")
        except HostError:
            pass
        assert client.alive, "host zyje po wyjatku handlera"
        assert client.call("echo", {"x": 1})["args"] == {"x": 1}
    finally:
        client.close()


def test_polish_characters_survive_the_round_trip_to_csharp() -> None:
    """Regresja: host MUSI czytac stdin jako UTF-8.

    Zmierzone na zywym hoscie na Windows: ``Console.In`` dekodowalo
    przekierowany stdin strona kodowa konsoli, wiec polskie znaki docieraly
    uszkodzone. Objaw przy sortowaniu: zgadzaly sie tylko tytuly czysto ASCII
    (1333 z 2596), ale psulo to KAZDA operacje z polskim tekstem -- w tym
    sciezki plikow do odtwarzania.
    """
    client = make_client()
    try:
        polish = "Bóg pojednał ŁÓDŹ żółć ćma ęąśń"
        echoed = client.call("echo", {"tytul": polish})["args"]["tytul"]
        assert echoed == polish, f"host oddal {echoed!r} zamiast {polish!r}"

        # Takze sciezka w stylu AMC, ze znakami i spacjami.
        path = "D:\\Muzyka\\Pieśni\\Żółta łódź - ćwierć.mp3"
        assert client.call("echo", {"path": path})["args"]["path"] == path
    finally:
        client.close()


def test_unknown_operation_is_an_error_not_a_crash() -> None:
    client = make_client()
    try:
        try:
            client.call("takiejOperacjiNieMa")
        except HostError:
            assert client.alive
            return
        raise AssertionError("ZALOZENIE NIESPELNIONE: nieznana operacja ma byc bledem")
    finally:
        client.close()


def test_polish_characters_survive_the_round_trip() -> None:
    client = make_client()
    try:
        text = "Zażółć gęślą jaźń — ĄĆĘŁŃÓŚŹŻ"
        assert client.call("utf8", {"text": text})["text"] == text
    finally:
        client.close()


def test_large_payload_arrives_whole() -> None:
    client = make_client()
    try:
        assert len(client.call("bigPayload")["text"]) == 100_000
    finally:
        client.close()


def test_events_from_the_host_reach_the_frontend() -> None:
    events: list[tuple[str, dict]] = []
    client = make_client(on_event=lambda name, data: events.append((name, data)))
    try:
        client.call("emit", {"title": "Mazurek"})
        deadline = time.time() + 5
        while time.time() < deadline and not events:
            time.sleep(0.05)
        assert events and events[0][0] == "playback.started"
        assert events[0][1]["title"] == "Mazurek"
    finally:
        client.close()


def test_missing_required_argument_is_rejected_by_the_host() -> None:
    client = make_client()
    try:
        try:
            client.call("files.listFolder", {})
        except HostError:
            assert client.alive
            return
        raise AssertionError("ZALOZENIE NIESPELNIONE: brak 'path' ma byc bledem")
    finally:
        client.close()


def test_eof_on_stdin_ends_the_host_process() -> None:
    client = make_client()
    client.hello()
    client.close()
    assert not client.alive, "host sam konczy sie na EOF"


def test_host_diagnostics_go_to_stderr_not_to_the_protocol() -> None:
    # WireServer przekierowuje Console.Out na stderr, zeby przypadkowy zapis
    # handlera nie zepsul strumienia protokolu. Sprawdzamy, ze rozmowa dziala.
    diagnostics: list[str] = []
    client = make_client(on_stderr=diagnostics.append)
    try:
        assert client.hello()["wire"] is True
    finally:
        client.close()
