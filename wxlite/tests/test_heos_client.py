from __future__ import annotations

import json
import socket

import pytest

from amc_wx_lite.heos_client import (
    HeosCommandError, HeosProtocolError, HeosReadClient, HeosTimeout,
)


def packet(command, *, result="success", message="", payload=None):
    response = {"heos": {"command": command, "result": result, "message": message}}
    if payload is not None:
        response["payload"] = payload
    return json.dumps(response).encode() + b"\r\n"


class FakeSocket:
    def __init__(self, chunks):
        self.chunks = list(chunks)
        self.sent = []
        self.closed = False

    def settimeout(self, value):
        assert value > 0

    def sendall(self, value):
        self.sent.append(value)

    def recv(self, count):
        if not self.chunks:
            raise socket.timeout
        value = self.chunks.pop(0)
        if isinstance(value, Exception):
            raise value
        return value

    def close(self):
        self.closed = True


def client_for(*sockets):
    pool = list(sockets)
    calls = []

    def connect(address, *, timeout):
        calls.append(address)
        return pool.pop(0)

    return HeosReadClient("192.168.0.22", connector=connect, timeout=0.5), calls


def test_fragmented_lines_events_and_interim_response():
    event = packet("event/player_state_changed", message="pid=1&state=play")
    interim = packet("browse/browse", message="command under process")
    final = packet("browse/browse", message="sid=2&returned=1&count=1", payload=[{"name": "Album"}])
    stream = event + interim + final
    fake = FakeSocket([stream[:7], stream[7:31], stream[31:95], stream[95:]])
    client, calls = client_for(fake)
    assert client.browse("2") == [{"name": "Album"}]
    assert len(client.events) == 1
    assert len(calls) == 1
    assert fake.sent == [b"heos://browse/browse?sid=2&range=0,99\r\n"]


def test_timeout_closes_connection_before_reconnect():
    first = FakeSocket([socket.timeout()])
    second = FakeSocket([packet("player/get_players", payload=[])])
    client, calls = client_for(first, second)
    with pytest.raises(HeosTimeout):
        client.players()
    assert first.closed
    assert client.players() == []
    assert len(calls) == 2


def test_fail_is_not_mistaken_for_empty_collection():
    fake = FakeSocket([packet("browse/get_music_sources", result="fail", message="eid=1&text=No account")])
    client, _ = client_for(fake)
    with pytest.raises(HeosCommandError) as error:
        client.sources()
    assert error.value.message == "eid=1&text=No account"
    assert fake.closed


def test_invalid_json_and_other_command_are_protocol_errors():
    broken = FakeSocket([b"not-json\r\n"])
    other = FakeSocket([packet("player/get_players", payload=[])])
    client, _ = client_for(broken, other)
    with pytest.raises(HeosProtocolError):
        client.players()
    with pytest.raises(HeosProtocolError):
        client.groups()


def test_only_read_only_commands_and_local_ip_are_accepted():
    client, _ = client_for(FakeSocket([]))
    with pytest.raises(ValueError):
        client.request("player/set_volume", pid=1, level=90)
    with pytest.raises(ValueError):
        HeosReadClient("example.com")
    with pytest.raises(ValueError):
        HeosReadClient("8.8.8.8")


class PagedSocket(FakeSocket):
    def __init__(self, items, count):
        super().__init__([])
        self.items = items
        self.count = count

    def sendall(self, value):
        super().sendall(value)
        query = value.decode().split("range=", 1)[1].split("\r", 1)[0]
        start, end = (int(part) for part in query.split(","))
        # The device returns only 40 entries even when 100 were requested.
        payload = self.items[start:min(end + 1, start + 40)]
        message = f"sid=2&range={start},{end}&returned={len(payload)}&count={self.count}"
        self.chunks.append(packet("browse/browse", message=message, payload=payload))


@pytest.mark.parametrize("count", [0, 125])
def test_pages_continue_after_short_page_and_keep_duplicate_titles(count):
    items = [{"name": "Same title", "mid": str(i)} for i in range(125)]
    fake = PagedSocket(items, count)
    client, _ = client_for(fake)
    result = client.browse("2")
    assert len(result) == 125
    assert [row["mid"] for row in result] == [str(i) for i in range(125)]
    assert len(fake.sent) == (4 if count else 5)


def test_empty_page_is_empty_collection_not_error():
    fake = PagedSocket([], 0)
    client, _ = client_for(fake)
    assert client.browse("2") == []
