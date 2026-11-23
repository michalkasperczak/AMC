"""Service routes a station and queued material to different HEOS commands."""

import pytest

from amc_wx_lite.heos_client import HeosPage
from amc_wx_lite.heos_service import HeosService
from amc_wx_lite.heos_source import catalog_items, queue_items


class FakeClient:
    def __init__(self):
        self.calls = []

    def players(self):
        return [{"pid": "1", "name": "Salon"}]

    def groups(self):
        return []

    def sources(self):
        return [{"sid": "2", "name": "Radio", "type": "music_service"}]

    def browse_page(self, sid, cid, *, start, limit):
        self.calls.append(("browse", sid, cid, start, limit))
        return HeosPage(({
            "name": "Stacja", "type": "station", "mid": "m", "playable": "yes",
        },), 1, False, 1)

    def queue(self, pid):
        self.calls.append(("queue", pid))
        return [{"qid": 1, "song": "Utwór", "mid": "m"}]

    def play_station(self, *args, **kwargs):
        self.calls.append(("station", args, kwargs))

    def add_to_queue(self, *args, **kwargs):
        self.calls.append(("add", args, kwargs))

    def play_queue_if_current(self, *args):
        self.calls.append(("play-queue", args))

    def close(self):
        self.calls.append(("close",))


def test_read_operations_do_not_send_controls():
    fake = FakeClient()
    service = HeosService(fake)
    assert service.targets()[0].title == "Salon"
    assert service.sources()[0].title == "Radio"
    page = service.catalog_page("2", "folder", start=4, limit=10)
    assert page.items[0].title == "Stacja"
    assert page.total == 1
    assert service.queue("1")[0].title == "Utwór"
    assert fake.calls == [
        ("browse", "2", "folder", 4, 10), ("queue", "1"),
    ]


def test_station_uses_stream_command_and_rejects_queue_placement():
    fake = FakeClient()
    service = HeosService(fake)
    station = catalog_items("2", "folder", [
        {"name": "Stacja", "type": "station", "mid": "m", "playable": "yes"},
    ])[0]
    with pytest.raises(ValueError, match="Stacji"):
        service.play_item("1", station, placement="next")
    assert fake.calls == []
    service.play_item("1", station)
    assert fake.calls == [("station", ("1", "2", "m", "Stacja"),
                           {"parent_cid": "folder"})]


def test_track_uses_parent_container_and_album_uses_own_container():
    fake = FakeClient()
    service = HeosService(fake)
    track = catalog_items("2", "album", [
        {"name": "Utwór", "type": "song", "mid": "m", "playable": "yes"},
    ])[0]
    album = catalog_items("2", "parent", [
        {"name": "Album", "type": "album", "cid": "c", "playable": "yes",
         "container": "yes"},
    ])[0]
    service.play_item("1", track, placement="next")
    service.play_item("1", album, placement="end")
    assert fake.calls == [
        ("add", ("1", "2"), {"cid": "album", "mid": "m",
                              "placement": "next", "replace_confirmed": False}),
        ("add", ("1", "2"), {"cid": "c", "mid": "",
                              "placement": "end", "replace_confirmed": False}),
    ]


def test_queue_item_passes_snapshot_token_for_recheck():
    fake = FakeClient()
    service = HeosService(fake)
    item = queue_items([{"qid": 1, "song": "Utwór", "mid": "m"}])[0]
    service.play_queue_item("1", item)
    assert fake.calls == [("play-queue", ("1", "1", item.fingerprint))]
