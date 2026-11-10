"""Real LiteHost processes, separate profile pipes, and one NVDA transport."""

import importlib.util
import os
from pathlib import Path
import threading

import pytest

from amc_wx_lite.host_client import LiteHostClient


ROOT = Path(__file__).resolve().parents[2]
PLUGIN = ROOT / "nvda-addon/addon/globalPlugins/amcController/transport.py"
spec = importlib.util.spec_from_file_location("amc_nvda_transport_test", PLUGIN)
assert spec and spec.loader
transport = importlib.util.module_from_spec(spec)
spec.loader.exec_module(transport)


@pytest.mark.skipif(os.name != "nt", reason="Windows named pipes")
def test_two_real_hosts_route_without_crossing_profiles() -> None:
    """Set AMC_DUAL_HOST_EXE to the freshly built LiteHost apphost."""
    executable = os.environ.get("AMC_DUAL_HOST_EXE")
    if not executable or not Path(executable).is_file():
        pytest.skip("Brak jawnie wskazanego, świeżo zbudowanego hosta")
    if any(transport.pipe_available(profile) for profile in ("test", "public")):
        pytest.skip("Potok użytkowego AMC zajęty; nie uruchamiamy drugiego hosta")

    clients = {}
    seen = []
    reply_threads = []

    def on_event(profile, name, data):
        if name != "nvda.command":
            return
        seen.append((profile, data["command"]))

        def reply():
            clients[profile].reply_to_nvda(
                data["requestId"], ok=True,
                message="Odpowiedź %s: %s" % (profile, data["command"]),
            )

        thread = threading.Thread(target=reply, daemon=True)
        reply_threads.append(thread)
        thread.start()

    try:
        for profile in ("test", "public"):
            client = LiteHostClient(
                executable, release_profile=profile,
                on_event=lambda name, data, profile=profile: on_event(profile, name, data),
            )
            clients[profile] = client
            client.start()
            client.hello()
        assert all(client.alive for client in clients.values())
        assert all(transport.pipe_available(profile) for profile in clients)
        for profile, command in (("test", "status"), ("public", "playPause"),
                                 ("test", "next")):
            result = transport.exchange(command, transport.pipe_name(profile))
            assert result == {
                "version": 1, "ok": True,
                "message": "Odpowiedź %s: %s" % (profile, command),
            }
        assert seen == [("test", "status"), ("public", "playPause"),
                        ("test", "next")]
    finally:
        for client in clients.values():
            client.close()
        for thread in reply_threads:
            thread.join(timeout=2)
