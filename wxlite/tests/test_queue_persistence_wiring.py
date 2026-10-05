"""Podlaczenie TRWALOSCI kolejki do ZWYKLEGO startu okna.

Backend (host C#) umie juz zapisywac kolejke do wlasnej kopii profilu, ale
sam z siebie tego nie robi: wymaga jawnych argumentow ``--profile-dir`` i
``--queue-write``. Dopoki frontend ich nie podaje, normalnie uruchomione okno
ma kolejke WYLACZNIE w pamieci procesu -- i nikt sie o tym nie dowiaduje.

Te testy pilnuja trzech rzeczy, kazda osobno:
 1. klient hosta potrafi podac te argumenty i BEZ nich zachowuje stary ksztalt
    wywolania (zadnych nowych argumentow w domyslnym starcie),
 2. okno wyprowadza tryb z UKLADU PROFILU: prywatna kopia (piaskownica) jest
    swiadomie pisalna, wspolny profil uzytkownika pozostaje tylko do odczytu,
 3. odmowa zapisu jest SLYSZALNA -- takze po naturalnym koncu utworu, gdy
    uzytkownik patrzy na odtwarzacz, a nie na Ctrl+Q -- i nie powtarza sie
    przy kazdym zdarzeniu.

Czego te testy NIE mierza: samego zapisu do SQLite. To jest zmierzone po
stronie hosta (zestawy protokolu + rzeczywisty restart procesu) i tutaj
celowo nie dublowane.
"""

from __future__ import annotations

import sys
import tempfile
from pathlib import Path
from types import SimpleNamespace
from typing import Any

sys.path.insert(0, str(Path(__file__).resolve().parent))

from test_gui_logic import install_wx_stub  # noqa: E402

install_wx_stub()

import amc_wx_lite.gui as gui  # noqa: E402
from amc_wx_lite.host_client import LiteHostClient  # noqa: E402
from amc_wx_lite.profile_layout import private_sandbox, read_only_mirror  # noqa: E402
from amc_wx_lite.state_store import Options  # noqa: E402


# ----------------------------------------------------------------- argumenty


def _spawned_command(**client_kwargs) -> list[str]:
    """Polecenie, ktore klient NAPRAWDE zlozylby dla procesu hosta."""
    captured: list[list[str]] = []

    def spawn(command, **_popen):
        captured.append(list(command))
        return SimpleNamespace(
            poll=lambda: None, stdin=None, stdout=None, stderr=None,
            wait=lambda timeout=None: 0, kill=lambda: None,
        )

    client = LiteHostClient(sys.executable, spawn=spawn, **client_kwargs)
    client.start()
    assert captured, "klient nie uruchomil procesu"
    return captured[0]


def test_default_start_keeps_the_old_command_shape() -> None:
    """Bez jawnego trybu nie dokladamy NICZEGO do startu hosta."""
    command = _spawned_command()
    assert command[1:] == ["--timeshift-minutes", "30"], (
        "domyslny start hosta musi zostac dokladnie taki jak dotad"
    )


def test_profile_directory_is_passed_to_the_host() -> None:
    command = _spawned_command(profile_dir=r"C:\kopia\profil")
    assert "--profile-dir" in command, "host nie dostal sciezki kopii profilu"
    assert command[command.index("--profile-dir") + 1] == r"C:\kopia\profil"
    assert "--queue-write" not in command, (
        "sam katalog to jeszcze NIE zgoda na zapis"
    )


def test_write_mode_requires_and_accompanies_the_profile_directory() -> None:
    command = _spawned_command(profile_dir="/kopia", queue_write=True)
    assert command[command.index("--profile-dir") + 1] == "/kopia"
    assert "--queue-write" in command, "swiadomy tryb pisarza nie dotarl do hosta"


def test_write_mode_alone_is_not_sent() -> None:
    """``--queue-write`` bez katalogu host i tak odrzuca -- nie wysylamy polowy
    kontraktu, zeby okno nie startowalo z gwarantowanym bledem."""
    command = _spawned_command(queue_write=True)
    assert "--queue-write" not in command
    assert "--profile-dir" not in command


# ------------------------------------------------------------- wybor trybu


class _Client:
    """Atrapa klienta: zapamietuje argumenty konstruktora."""

    last_kwargs: dict = {}

    def __init__(self, *args, **kwargs) -> None:
        type(self).last_kwargs = kwargs

    def start(self) -> None: ...
    def hello(self) -> dict: return {"protocol": 1}
    def configure_audio(self, **options) -> dict: return options
    def close(self) -> None: ...
    def call(self, op, args=None, timeout=None): return {"keys": []}


def _frame_for_start(layout) -> SimpleNamespace:
    frame = SimpleNamespace(
        options=Options(),
        layout=layout,
        client=None,
        runner=SimpleNamespace(submit=lambda *args: None),
        timer=SimpleNamespace(Start=lambda n: None),
        announcer=SimpleNamespace(say=lambda text: None),
        _on_engine_event=lambda *args: None,
        library=SimpleNamespace(use_collation=lambda collation: None),
        _load_initial_content=lambda: None,
    )
    # Mierzymy PRAWDZIWA metode wyboru trybu, nie wlasna atrape jej logiki.
    frame._queue_persistence_arguments = (
        lambda: gui.LiteFrame._queue_persistence_arguments(frame)
    )
    return frame


def _start_with(layout) -> dict:
    original = gui.LiteHostClient
    gui.LiteHostClient = _Client
    _Client.last_kwargs = {}
    try:
        gui.LiteFrame._start_engine(_frame_for_start(layout))
    finally:
        gui.LiteHostClient = original
    return _Client.last_kwargs


def test_private_copy_starts_a_persisting_host() -> None:
    """Wlasna pelna kopia: okno SWIADOMIE prosi o zapis kolejki."""
    with tempfile.TemporaryDirectory() as directory:
        base = Path(directory)
        (base / "library.db").write_bytes(b"")
        kwargs = _start_with(private_sandbox(base))
    assert kwargs.get("profile_dir") == str(base)
    assert kwargs.get("queue_write") is True


def test_shared_profile_stays_read_only() -> None:
    """Wspolny profil uzytkownika: ani sciezki, ani zgody na zapis.

    Wlascicielem tych plikow jest pelne AMC (WPF). Nasza blokada pliku nie
    chroni przed starym programem, wiec nie dotykamy go nawet z zamiarem.
    """
    with tempfile.TemporaryDirectory() as directory:
        base = Path(directory)
        (base / "library.db").write_bytes(b"")
        kwargs = _start_with(read_only_mirror(local_dir=base, profile_dir=base))
    assert not kwargs.get("profile_dir")
    assert not kwargs.get("queue_write")


def test_sandbox_without_a_database_does_not_break_plain_file_playback() -> None:
    """Brak bazy = zwykle przegladanie dysku. Host zostaje w pamieci."""
    with tempfile.TemporaryDirectory() as directory:
        kwargs = _start_with(private_sandbox(Path(directory)))
    assert not kwargs.get("profile_dir")
    assert not kwargs.get("queue_write")


# ------------------------------------------------------- slyszalny blad zapisu


def _frame_for_error(queue_write: bool = True) -> SimpleNamespace:
    said: list[str] = []
    submitted: list[tuple] = []
    frame = SimpleNamespace(
        announcer=SimpleNamespace(say=said.append),
        client=SimpleNamespace(queue_status=lambda: {}),
        runner=SimpleNamespace(submit=lambda *args: submitted.append(args)),
        _queue_write_requested=queue_write,
        _last_persist_error=None,
        said=said,
        submitted=submitted,
    )
    return frame


def _note(frame, payload: dict) -> None:
    gui.LiteFrame._note_queue_persistence(frame, payload)


def test_refused_write_is_announced_once_not_every_event() -> None:
    frame = _frame_for_error()
    payload = {"persistent": False, "persistError": "kopia zajeta przez inny proces"}

    _note(frame, payload)
    _note(frame, payload)
    _note(frame, payload)

    assert len(frame.said) == 1, "techniczne powtorzenia co zdarzenie sa halasem"
    assert "kopia zajeta przez inny proces" in frame.said[0]
    assert "zapisano" not in frame.said[0].lower().replace("nie zapisano", ""), (
        "odmowa nie moze brzmiec jak powodzenie"
    )


def test_a_new_reason_is_announced_again() -> None:
    frame = _frame_for_error()
    _note(frame, {"persistError": "kopia zajeta"})
    _note(frame, {"persistError": "baza tylko do odczytu"})
    assert len(frame.said) == 2, "INNA przyczyna to nowa informacja"


def test_successful_write_says_nothing() -> None:
    frame = _frame_for_error()
    _note(frame, {"persistent": True, "persistError": None, "persistedWrites": 3})
    assert frame.said == [], "udany zapis nie jest komunikatem"


def test_plain_read_only_run_does_not_promise_or_mourn_writes() -> None:
    """Zwykly start bez zgody na zapis NIE obiecywal trwalosci.

    ``persistent=false`` jest wtedy stanem NORMALNYM, a nie awaria -- gadanie
    o nim przy kazdym koncu utworu byloby halasem bez tresci.
    """
    frame = _frame_for_error(queue_write=False)
    _note(frame, {"persistent": False, "persistError": "brak --profile-dir"})
    assert frame.said == []


def test_manual_queue_result_reports_refused_save() -> None:
    from amc_wx_lite.list_model import Row
    from amc_wx_lite.navigation import PlayFromQueue, PlayQueueAt, Navigator

    for action in ("set", "at", "next"):
        frame: Any = gui.LiteFrame.__new__(gui.LiteFrame)
        frame.options = Options()
        frame.navigator = Navigator()
        frame._queue_write_requested = True
        frame._last_persist_error = None
        said = []
        frame.announcer = SimpleNamespace(say=said.append)
        frame._run = lambda effects: None
        frame._refresh_status = lambda: None
        payload = {"persistError": "test odmowy", "moved": False}
        frame.client = SimpleNamespace(
            queue_set=lambda *a, **k: payload,
            queue_play_at=lambda *a, **k: payload,
            queue_next=lambda **k: payload,
        )
        frame.runner = SimpleNamespace(submit=lambda slot, work, done, fail: done(work()))
        if action == "set":
            frame._play_from_queue(PlayFromQueue("q", (Row("q", "Utwór", "track", path="file.wav"),), "Utwór"))
        elif action == "at":
            frame._play_queue_at(PlayQueueAt("q", "Utwór"))
        else:
            frame._queue_step(True)
        assert any("test odmowy" in x for x in said), (action, said)


def test_last_track_end_checks_write_failure_without_queue_advanced() -> None:
    frame: Any = _frame_for_error()
    frame._window_alive = lambda: True
    frame._refresh_live_queue = lambda: None
    calls = []
    frame._check_queue_persistence = lambda: calls.append("checked")
    gui.LiteFrame._handle_engine_event(frame, "playback.ended", {
        "engine": "files", "id": "last", "queueContinues": False,
    })
    assert calls == ["checked"], "ostatni utwór nie emituje queue.advanced"


def test_natural_end_checks_persistence_outside_the_queue_view() -> None:
    """Blad zapisu po NATURALNYM koncu slychac takze w odtwarzaczu.

    Dotad persistError dalo sie zobaczyc tylko wchodzac w Ctrl+Q, czyli
    wtedy, gdy uzytkownik i tak podejrzewal klopot. Sprawdzamy PELNA droge:
    zdarzenie hosta -> zapytanie o stan kolejki -> wypowiedziana przyczyna.
    """
    frame = _frame_for_error()
    frame._window_alive = lambda: True
    frame._refresh_live_queue = lambda: None
    frame.navigator = SimpleNamespace(note_queue_advanced=lambda *a: [])
    frame._run = lambda effects: None
    frame.client = SimpleNamespace(
        queue_status=lambda: {"persistError": "odmowa zapisu kopii"}
    )
    # Cala droga ma byc PRAWDZIWA: i pytanie o stan, i wypowiedzenie przyczyny.
    frame._check_queue_persistence = (
        lambda: gui.LiteFrame._check_queue_persistence(frame)
    )
    frame._note_queue_persistence = (
        lambda payload: gui.LiteFrame._note_queue_persistence(frame, payload)
    )

    gui.LiteFrame._handle_engine_event(
        frame, "queue.advanced", {"engine": "files", "id": "b", "title": "B"}
    )

    jobs = [job for job in frame.submitted if job[0] == "queue-persist"]
    assert jobs, "po przejsciu kolejki nikt nie pyta o stan zapisu"
    _name, work, done, _failed = jobs[0]
    done(work())

    assert frame.said, "odmowa zapisu pozostala cicha poza widokiem kolejki"
    assert "odmowa zapisu kopii" in frame.said[0]
