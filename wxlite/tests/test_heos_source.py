from __future__ import annotations

import pytest

from amc_wx_lite.heos_source import (
    catalog_items, playback_status, source_choices, target_choices,
)


def test_targets_keep_group_leader_and_hide_group_members():
    players = [
        {"pid": 11, "name": "Salon", "model": "Denon Home 200"},
        {"pid": 12, "name": "Kuchnia", "model": "Denon Home 200"},
        {"pid": 13, "name": "Gabinet", "model": "Denon Home 150"},
    ]
    groups = [{"gid": 90, "name": "Parter", "players": [
        {"pid": 11, "role": "leader"}, {"pid": 12, "role": "member"},
    ]}]
    rows = target_choices(players, groups)
    assert [(row.title, row.kind_label, row.player_id) for row in rows] == [
        ("Parter", "grupa", "11"), ("Gabinet", "urządzenie", "13"),
    ]
    assert rows[0].member_ids == ("11", "12")
    assert all("90" not in row.title and "11" not in row.title for row in rows)


def test_target_without_user_name_uses_model_never_object_repr():
    rows = target_choices([{"pid": 1, "name": {"raw": "bad"}, "model": "Denon Home 200"}], [])
    assert rows[0].title == "Denon Home 200"


def test_sources_keep_user_names_but_filter_unavailable_or_invalid():
    rows = source_choices([
        {"sid": 1, "name": "Historia", "type": "heos_service", "available": True},
        {"sid": 2, "name": "Nieaktywne", "type": "music_service", "available": "false"},
        {"sid": 3, "name": "Serwer NAS", "type": "dlna_server", "available": "true"},
        {"sid": 4, "name": {"bad": "object"}, "type": "heos_service", "available": True},
    ])
    assert [row.title for row in rows] == ["Historia", "Serwer NAS"]
    assert [row.source_id for row in rows] == ["1", "3"]


def test_catalog_distinguishes_duplicate_titles_and_playable_containers():
    rows = catalog_items("2", "album-1", [
        {"name": "To samo", "type": "song", "mid": "a", "container": "no", "playable": "yes"},
        {"name": "To samo", "type": "song", "mid": "b", "container": "no", "playable": "yes"},
        {"name": "Album", "type": "album", "cid": "c", "container": "yes", "playable": "yes"},
    ])
    assert [row.title for row in rows] == ["To samo", "To samo", "Album"]
    assert len({row.item_id for row in rows}) == 3
    assert rows[0].source_id == rows[1].source_id == "2"
    assert rows[2].can_open and rows[2].can_play
    assert rows[2].container_id == "c"
    assert rows[2].kind_label == "album"


def test_catalog_identity_is_scoped_to_source_and_parent():
    payload = [{"name": "Utwór", "type": "song", "mid": "same", "playable": "yes"}]
    a = catalog_items("1", "parent", payload)[0]
    b = catalog_items("2", "parent", payload)[0]
    c = catalog_items("1", "other", payload)[0]
    assert len({a.item_id, b.item_id, c.item_id}) == 3


def test_catalog_skips_unusable_items_and_never_stringifies_objects():
    rows = catalog_items("1", None, [
        {"name": {"raw": "bad"}, "mid": "a", "playable": "yes"},
        {"name": "No action", "type": "song"},
        {"name": "Piosenka", "type": "song", "mid": "b", "playable": "yes", "artist": {"raw": "bad"}},
    ])
    assert len(rows) == 1
    assert rows[0].title == "Piosenka"
    assert rows[0].detail == ""


def test_nested_heos_service_uses_child_source_id_from_device_response():
    rows = catalog_items("root", None, [
        {"name": "Podźródło", "type": "heos_service", "sid": 211, "image_url": "https://private.invalid/x"},
    ])
    assert len(rows) == 1
    assert rows[0].title == "Podźródło"
    assert rows[0].kind_label == "źródło"
    assert rows[0].can_open and not rows[0].can_play
    assert rows[0].child_source_id == "211"
    assert "211" not in rows[0].title


def test_catalog_rejects_missing_list_instead_of_reporting_empty():
    with pytest.raises(ValueError):
        catalog_items("1", None, None)
    assert catalog_items("1", None, []) == ()


def test_playback_status_speaks_content_not_raw_ids_or_objects():
    play = {"heos": {"message": "pid=private-1&state=play"}}
    media = {"payload": {
        "song": "Audycja", "station": "Radio", "artist": "Prowadzący",
        "type": "station", "mid": "private-mid", "image_url": "https://private.invalid/cover",
    }}
    volume = {"heos": {"message": "pid=private-1&level=36"}}
    mute = {"heos": {"message": "pid=private-1&state=off"}}
    status = playback_status(play, media, volume, mute)
    assert status.spoken_label() == "Audycja, Radio, Prowadzący, odtwarzanie, głośność 36"
    assert "private" not in status.spoken_label()
    assert status.media_type == "station"


def test_playback_status_missing_fields_are_not_object_strings():
    status = playback_status({}, {"payload": {"song": {"unexpected": "object"}}}, {}, {})
    assert status.spoken_label() == "Nic nie jest odtwarzane"
    assert status.volume is None and status.muted is None
