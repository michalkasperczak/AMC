"""The AMC list must speak labels, never HEOS object or protocol fields."""

from amc_wx_lite.heos_rows import catalog_rows, queue_rows, source_rows, target_rows
from amc_wx_lite.heos_source import (
    HeosSource, HeosTarget, catalog_items, queue_items,
)
from amc_wx_lite.list_model import ListModel


def spoken(rows):
    model = ListModel()
    model.replace(rows)
    return [tuple(model.text_for(i, column) for column in range(3))
            for i in range(len(model))]


def test_targets_and_sources_have_user_facing_kind_and_no_ids():
    targets = target_rows((
        HeosTarget("internal-device", "Salon", "984237"),
        HeosTarget("internal-group", "Parter", "984237", "group-secret"),
    ))
    sources = source_rows((
        HeosSource("internal-source", "Radio", "1024", "music_service"),
    ))
    assert spoken(targets) == [("Salon", "urządzenie", ""), ("Parter", "grupa", "")]
    assert spoken(sources) == [("Radio", "źródło", "")]


def test_catalog_and_queue_keep_protocol_ids_out_of_spoken_columns():
    catalog = catalog_rows(catalog_items("101", "secret-folder", [
        {"name": "Stacja", "type": "station", "mid": "secret-media",
         "playable": "yes"},
        {"name": "Album", "type": "album", "cid": "secret-album",
         "container": "yes", "playable": "yes"},
    ]))
    queue = queue_rows(queue_items([
        {"qid": 1, "song": "Utwór", "mid": "secret-track", "artist": "Autor"},
    ]))
    assert spoken(catalog) == [("Stacja", "stacja", ""), ("Album", "album", "")]
    assert spoken(queue) == [("Utwór", "utwór", "Autor")]
    assert all("secret" not in value for row in spoken(catalog + queue)
               for value in row)
