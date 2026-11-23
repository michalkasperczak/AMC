"""View changes and target changes must not apply old HEOS responses."""

from amc_wx_lite.heos_navigation import HeosBrowseState
from amc_wx_lite.heos_source import HeosSource, HeosTarget, catalog_items, queue_items


def target(name: str, pid: str) -> HeosTarget:
    return HeosTarget(f"target-{pid}", name, pid)


def test_target_disappearance_never_selects_another_room():
    state = HeosBrowseState()
    left, right = target("Salon", "1"), target("Kuchnia", "2")
    state.set_targets((left, right))
    state.choose_target(left.item_id)
    state.set_targets((right,))
    assert state.selected_target is None
    assert state.location.kind == "targets"
    assert state.items == (right,)


def test_back_restores_parent_selection_even_with_duplicate_titles():
    state = HeosBrowseState()
    state.set_targets((target("Salon", "1"),))
    state.choose_target("target-1")
    sources = (HeosSource("source-a", "Muzyka", "3", "music_service"),)
    ticket = state.begin_list_request()
    assert state.apply_list(ticket, sources)
    state.open_source(sources[0])
    albums = catalog_items("3", None, [
        {"name": "Album", "type": "album", "cid": "a", "container": "yes"},
        {"name": "Album", "type": "album", "cid": "b", "container": "yes"},
    ])
    ticket = state.begin_list_request()
    assert state.apply_list(ticket, albums)
    state.open_item(albums[1])
    assert state.back()
    assert state.selected_item_id == albums[1].item_id
    ticket = state.begin_list_request()
    assert state.apply_list(ticket, albums)
    assert state.selected_item_id == albums[1].item_id
    assert state.back()
    assert state.selected_item_id == sources[0].item_id


def test_late_browse_result_after_target_change_is_ignored():
    state = HeosBrowseState()
    a, b = target("Salon", "1"), target("Kuchnia", "2")
    state.set_targets((a, b))
    state.choose_target(a.item_id)
    ticket = state.begin_list_request()
    state.choose_target(b.item_id)
    assert not state.apply_list(ticket, (HeosSource("x", "X", "3", "music_service"),))
    assert state.items == ()


def test_late_result_after_newer_refresh_is_ignored():
    state = HeosBrowseState()
    state.set_targets((target("Salon", "1"),))
    state.choose_target("target-1")
    old = state.begin_list_request()
    new = state.begin_list_request()
    assert not state.apply_list(old, ())
    assert state.apply_list(new, ())


def test_late_target_refresh_cannot_replace_new_target():
    state = HeosBrowseState()
    a, b = target("Salon", "1"), target("Kuchnia", "2")
    state.set_targets((a, b))
    old = state.begin_list_request()
    state.choose_target(b.item_id)
    assert not state.apply_targets(old, (a,))
    assert state.selected_target == b


def test_queue_rows_keep_ids_private_and_full_snapshot_token():
    rows = queue_items([
        {"qid": 1, "mid": "a", "song": "Ten sam", "artist": "Wykonawca"},
        {"qid": 2, "mid": "b", "song": "Ten sam"},
    ])
    assert len(rows) == 2
    assert rows[0].item_id != rows[1].item_id
    assert rows[0].title == "Ten sam"
    assert rows[0].detail == "Wykonawca"
    assert rows[0].fingerprint == rows[1].fingerprint
    assert "qid" not in rows[0].title
    assert queue_items([]) == ()
