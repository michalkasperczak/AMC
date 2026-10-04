#!/usr/bin/env python3
"""Pomiar warstwy aktywnosci na PELNEJ chronionej bazie (tylko odczyt).

Narzedzie jednorazowe. Liczy CALA wlasciwa czesc fixture (nie probke) i zlicza
programowo to, co w raporcie musialoby byc zgadywane: liczbe badanych
elementow, duplikaty, unikalne Id i poprawnosc porzadku. Nazw wlasnych NIE
wypisuje -- tylko liczby i skroty, bo kwit idzie do repozytorium.

Uzycie::

    python3 tools/measure_library_activity.py /sciezka/do/library.db
"""

from __future__ import annotations

import hashlib
import json
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))

from amc_wx_lite.library_activity import (  # noqa: E402
    MAX_HISTORY_ENTRIES_PER_SESSION,
    bookmark_rows,
    history_rows,
    saved_queue_rows,
)
from amc_wx_lite.library_db import LibraryDatabase  # noqa: E402

LOCAL = "local"


def _digest(values: list[str]) -> str:
    """Skrot kolejnosci: dowodzi porzadku, nie ujawniajac nazw ani sciezek."""
    return hashlib.sha256("\u001f".join(values).encode("utf-8")).hexdigest()[:16]


def _stored(db: LibraryDatabase, table: str) -> list[str]:
    return [
        str(r["item_id"])
        for r in db.connection.execute(
            f"SELECT item_id FROM {table} WHERE session_id = ? COLLATE NOCASE "
            "ORDER BY ordinal",
            (LOCAL,),
        )
    ]


def _count(db: LibraryDatabase, sql: str, *params: object) -> int:
    return int(db.connection.execute(sql, params).fetchone()[0])


def _is_sorted(values: list[tuple[int, int]]) -> bool:
    return all(values[i] <= values[i + 1] for i in range(len(values) - 1))


def main(path: str) -> int:
    db_path = Path(path).resolve()
    receipt: dict[str, object] = {
        "db_sha256_before": hashlib.sha256(db_path.read_bytes()).hexdigest(),
        "db_bytes": db_path.stat().st_size,
    }

    with LibraryDatabase(db_path) as db:
        receipt["sees_live_writes"] = db.sees_live_writes
        active_items = _count(
            db,
            "SELECT COUNT(*) FROM local_items WHERE is_available = 1 AND is_in_library = 1",
        )
        receipt["catalog"] = {
            "local_items_total": _count(db, "SELECT COUNT(*) FROM local_items"),
            "active_local_items": active_items,
        }

        # --- 1. historia odtwarzania -------------------------------------
        stored_history = _stored(db, "playback_history")
        hist = history_rows(db)
        hist_ids = [row.item_id for row in hist.rows]
        receipt["history"] = {
            "table_rows_all_sessions": _count(db, "SELECT COUNT(*) FROM playback_history"),
            "stored_ids_local_session": len(stored_history),
            "repeated_stored_ids": len(stored_history) - len(set(stored_history)),
            "rows_returned": len(hist.rows),
            "cap_per_session": MAX_HISTORY_ENTRIES_PER_SESSION,
            "cap_respected": len(hist.rows) <= MAX_HISTORY_ENTRIES_PER_SESSION,
            "unique_returned_ids": len(set(hist_ids)),
            "duplicate_returned_ids": len(hist_ids) - len(set(hist_ids)),
            "stored_ids_without_active_item": hist.missing_item_count,
            # Porzadek jest ZAPISANY (ordinal 0 = ostatnio odtworzone), nie
            # liczony z zadnej daty: tabela nie ma kolumny czasu. Sprawdzamy
            # wiec, ze wynik jest podciagiem zapisu, bo tylko to C# obiecuje.
            "order_is_subsequence_of_stored": hist_ids
            == [i for i in dict.fromkeys(stored_history) if i in set(hist_ids)],
            "order_digest": _digest(hist_ids),
        }

        # --- 2. zapisana kolejka ----------------------------------------
        stored_queue = _stored(db, "queue_order")
        regular = _stored(db, "queue_regular_order")
        play_next = _stored(db, "queue_play_next_order")
        queue = saved_queue_rows(db)
        queue_ids = [q.item_id for q in queue.queue]
        flags = [q.is_play_next for q in queue.queue]
        receipt["saved_queue"] = {
            "queue_order_rows_all_sessions": _count(db, "SELECT COUNT(*) FROM queue_order"),
            "stored_queue_order_local": len(stored_queue),
            "stored_regular_local": len(regular),
            "stored_play_next_local": len(play_next),
            "legacy_regular_fallback_active": not regular and not play_next,
            "repeated_stored_ids": len(stored_queue) - len(set(stored_queue)),
            "rows_returned": len(queue.rows),
            "play_next_rows": sum(flags),
            "regular_rows": len(flags) - sum(flags),
            "play_next_block_first": flags == sorted(flags, reverse=True),
            "unique_returned_ids": len(set(queue_ids)),
            "duplicate_returned_ids": len(queue_ids) - len(set(queue_ids)),
            "stored_ids_without_active_item": queue.missing_item_count,
            # W kazdym bloku kolejnosc musi rosnac wzgledem pozycji w zapisie.
            "stored_order_kept_within_blocks": _is_sorted(
                [
                    (0 if q.is_play_next else 1, stored_queue.index(q.item_id))
                    for q in queue.queue
                ]
            ),
            # Kolumny migawkowe NIE moga decydowac o czlonkostwie.
            "column_is_in_queue_rows": _count(
                db, "SELECT COUNT(*) FROM local_items WHERE is_in_queue = 1"
            ),
            "column_is_play_next_rows": _count(
                db, "SELECT COUNT(*) FROM local_items WHERE is_play_next = 1"
            ),
            "order_digest": _digest(queue_ids),
        }

        # --- 3. zakladki -------------------------------------------------
        # KAZDY lokalny element majacy jakikolwiek wpis w bookmarks, czyli cala
        # wlasciwa czesc bazy -- nie probka.
        item_ids = [
            str(r["item_id"])
            for r in db.connection.execute(
                "SELECT DISTINCT item_id FROM bookmarks "
                "WHERE session_id = ? COLLATE NOCASE ORDER BY item_id",
                (LOCAL,),
            )
        ]
        probed = returned = bad_order = absent = shared_pos = unknown_date = 0
        row_ids: list[str] = []
        for item_id in item_ids:
            result = bookmark_rows(db, item_id=item_id)
            probed += 1
            returned += len(result.rows)
            keys = [(b.position_ticks, b.created_utc_ticks) for b in result.bookmarks]
            if not _is_sorted(keys):
                bad_order += 1
            shared_pos += len(keys) - len({k[0] for k in keys})
            unknown_date += sum(1 for b in result.bookmarks if b.created_utc_ticks <= 0)
            if not _count(db, "SELECT COUNT(*) FROM local_items WHERE id = ?", item_id):
                absent += 1
            row_ids.extend(row.item_id for row in result.rows)
        receipt["bookmarks"] = {
            "table_rows_all_sessions": _count(db, "SELECT COUNT(*) FROM bookmarks"),
            "table_rows_local_session": _count(
                db,
                "SELECT COUNT(*) FROM bookmarks WHERE session_id = ? COLLATE NOCASE",
                LOCAL,
            ),
            "table_rows_local_with_bookmark_bit": _count(
                db,
                "SELECT COUNT(*) FROM bookmarks WHERE session_id = ? COLLATE NOCASE "
                "AND (purpose & 1) != 0",
                LOCAL,
            ),
            "chapter_only_rows_local": _count(
                db,
                "SELECT COUNT(*) FROM bookmarks WHERE session_id = ? COLLATE NOCASE "
                "AND (purpose & 1) = 0",
                LOCAL,
            ),
            "items_probed": probed,
            "bookmark_rows_returned": returned,
            "items_with_wrong_order": bad_order,
            "items_absent_from_local_items": absent,
            "entries_sharing_a_position": shared_pos,
            "entries_with_unknown_created_date": unknown_date,
            "unique_row_ids": len(set(row_ids)),
            "row_id_collisions": len(row_ids) - len(set(row_ids)),
            "order_digest": _digest(row_ids),
        }

    receipt["db_sha256_after"] = hashlib.sha256(db_path.read_bytes()).hexdigest()
    receipt["db_unchanged"] = receipt["db_sha256_after"] == receipt["db_sha256_before"]
    print(json.dumps(receipt, indent=2, ensure_ascii=False))
    return 0


if __name__ == "__main__":
    if len(sys.argv) != 2:
        print(__doc__)
        raise SystemExit(2)
    raise SystemExit(main(sys.argv[1]))
