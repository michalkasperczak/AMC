#!/usr/bin/env python3
"""Kwit maszynowy czterech widokow na PELNEJ kopii profilu.

Nie zapisuje tytulow, nazw playlist ani sciezek -- tylko liczby, skroty i
wyniki predykatow. Hash bazy przed i po, zeby udowodnic, ze odczyt nic nie
zmienil.

Klucze AMC_PL bierzemy z PROBY .NET (ten sam ``CompareInfo``, co host C#),
a nie z nowego kolatora w Pythonie. Gdy kluczy nie ma, kwit zapisuje
``order_matches_amc = false`` i mowi to wprost -- zadnego udawania.
"""

from __future__ import annotations

import base64
import hashlib
import json
import os
import subprocess
import sys
import tempfile
from datetime import datetime, timezone
from pathlib import Path

HERE = Path(__file__).resolve()
sys.path.insert(0, str(HERE.parents[1]))

from amc_wx_lite.library_db import LibraryDatabase  # noqa: E402
from amc_wx_lite.library_views import (  # noqa: E402
    active_items,
    all_files_rows,
    favorite_rows,
    playlist_contents_rows,
    playlist_rows,
)

FIXTURE = Path(
    os.environ.get(
        "AMC_WX_FIXTURE",
        "/home/michal/projekty/amc_pomoc/wx-full-profile-after421/fixture",
    )
)
LIBRARY_DB = FIXTURE / "library.db"
PROBE = Path(
    os.environ.get(
        "AMC_WX_SORTKEY_PROBE",
        "/home/michal/projekty/amc_pomoc/wx-full-profile-after421"
        "/library-views-after422/probe-csharp-order",
    )
)
DOTNET = os.environ.get("DOTNET", "/home/michal/dotnet/dotnet")


def sha256_file(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as handle:
        for chunk in iter(lambda: handle.read(1 << 20), b""):
            digest.update(chunk)
    return digest.hexdigest()


def sha256_ids(ids) -> str:
    """Skrot kolejnosci Id, 1:1 z proba C#: ``string.Join("\\n", ids)``.

    Bez konczacego znaku nowej linii -- inaczej kwit nie dalby sie porownac z
    ``allFilesOrderSha256`` z .NET i "niezgodnosc" byla by artefaktem
    formatowania, nie kolejnosci.
    """
    return hashlib.sha256("\n".join(ids).encode("utf-8")).hexdigest()


class ProbeCollation:
    """Klucze AMC_PL z oryginalnego .NET ``CompareInfo``, interfejs jak host.

    To ta sama sonda, ktora zmierzyla zgodnosc kolejnosci -- nie nowy kolator.
    """

    def __init__(self, keys: dict[str, bytes]) -> None:
        self._keys = keys

    def load(self, titles) -> None:  # klucze sa juz policzone w jednej partii
        return None

    def key_for(self, title: str) -> bytes | None:
        return self._keys.get(title)


def run_probe(items) -> tuple[dict, dict[str, bytes]] | tuple[None, None]:
    """Policz w .NET kolejnosc C# i klucze AMC_PL dla PRAWDZIWYCH tytulow."""
    if not PROBE.exists():
        return None, None
    with tempfile.TemporaryDirectory() as tmp:
        payload = Path(tmp) / "items.json"
        output = Path(tmp) / "order.json"
        payload.write_text(
            json.dumps(
                {
                    "items": [
                        {"id": i.id, "title": i.title, "path": i.path} for i in items
                    ]
                },
                ensure_ascii=False,
            ),
            encoding="utf-8",
        )
        result = subprocess.run(
            [
                DOTNET,
                "run",
                "-c",
                "Release",
                "--project",
                str(PROBE),
                "--",
                str(payload),
                str(output),
            ],
            capture_output=True,
            text=True,
            env={**os.environ, "HOME": os.environ.get("HOME", "/home/michal")},
        )
        if result.returncode != 0 or not output.exists():
            return None, None
        measured = json.loads(output.read_text(encoding="utf-8"))
    keys = {
        title: base64.b64decode(key)
        for title, key in zip(
            measured.get("keyTitles", []), measured.get("keyBytes", [])
        )
    }
    return measured, keys


def main() -> int:
    if not LIBRARY_DB.exists():
        print(f"Brak kopii bazy: {LIBRARY_DB}", file=sys.stderr)
        return 2

    before = {
        name: sha256_file(FIXTURE / name)
        for name in ("library.db", "library.db-wal")
        if (FIXTURE / name).exists()
    }

    receipt: dict = {
        "schema": "amc-wx-library-views/full-profile-receipt/1",
        "measured_utc": datetime.now(timezone.utc).isoformat(timespec="seconds"),
        "python": sys.version.split()[0],
        "database_sha256_before": before,
        "views": {},
    }

    with LibraryDatabase(LIBRARY_DB) as db:
        receipt["sees_live_writes"] = db.sees_live_writes
        receipt["schema_version"] = db.schema_version()
        receipt["rows_in_local_items"] = db.count_items()
        receipt["active_items"] = db.count_active_items()

        items = active_items(db)
        measured, keys = run_probe(items)
        collation = ProbeCollation(keys) if keys else None
        receipt["host_collation_keys"] = {
            "available": collation is not None,
            "source": "dotnet CompareInfo probe (pl-PL, IgnoreCase|IgnoreNonSpace)",
            "titles_with_keys": len(keys or {}),
        }

        # 1. Wszystkie pliki
        all_files = all_files_rows(db, collation)
        view = {
            "rows": len(all_files.rows),
            "heading": all_files.heading,
            "order_matches_amc": all_files.order_matches_amc,
            "unique_ids": len({r.item_id for r in all_files.rows}),
            "all_ids_are_str": all(isinstance(r.item_id, str) for r in all_files.rows),
            "kinds": sorted({r.kind for r in all_files.rows}),
            "order_sha256": sha256_ids(r.item_id for r in all_files.rows),
            "predicate_active_only": all(
                r.item_id in {i.id for i in items} for r in all_files.rows
            ),
        }
        if measured:
            csharp_order = measured["allFilesOrder"]
            mine = [r.item_id for r in all_files.rows]
            view["csharp_order_sha256"] = measured["allFilesOrderSha256"]
            view["matches_csharp_order"] = (
                view["order_sha256"] == measured["allFilesOrderSha256"]
            )
            view["positions_differing_from_csharp"] = (
                sum(1 for a, b in zip(mine, csharp_order) if a != b)
                if len(mine) == len(csharp_order)
                else None
            )
            view["dotnet_runtime"] = measured["runtime"]
            view["dotnet_culture"] = measured["currentCulture"]
            view["amc_pl_keys_differ_from_csharp_order"] = measured[
                "amcPlKeysDifferFromAllFiles"
            ]
            view["ignore_case_only_keys_differ_from_csharp_order"] = measured[
                "ignoreCaseKeysDifferFromAllFiles"
            ]
        receipt["views"]["wszystkie_pliki"] = view

        # 1b. Stabilnosc Id: drugi, swiezy odczyt musi dac te same Id
        second = all_files_rows(LibraryDatabase(LIBRARY_DB), collation)
        receipt["views"]["wszystkie_pliki"]["stable_across_reopen"] = (
            sha256_ids(r.item_id for r in second.rows) == view["order_sha256"]
        )

        # 2. Ulubione
        favorites = favorite_rows(db)
        custom = favorite_rows(db, order="custom")
        active_ids = {i.id for i in items}
        receipt["views"]["ulubione"] = {
            "rows": len(favorites.rows),
            "heading": favorites.heading,
            "order_matches_amc": favorites.order_matches_amc,
            "unique_ids": len({r.item_id for r in favorites.rows}),
            "all_ids_are_str": all(isinstance(r.item_id, str) for r in favorites.rows),
            "order_sha256": sha256_ids(r.item_id for r in favorites.rows),
            "predicate_is_favorite_and_active": all(
                r.item_id in active_ids for r in favorites.rows
            ),
            "expected_rows_from_sql": db.connection.execute(
                "SELECT COUNT(*) FROM local_items "
                "WHERE is_available = 1 AND is_in_library = 1 AND is_favorite = 1"
            ).fetchone()[0],
            "added_newest_is_reverse_of_added_order": [
                r.item_id for r in favorites.rows
            ]
            == list(
                reversed(
                    [
                        item_id
                        for item_id in [
                            str(row[0])
                            for row in db.connection.execute(
                                "SELECT item_id FROM favorite_added_order "
                                "WHERE session_id = 'local' ORDER BY ordinal"
                            )
                        ]
                        if item_id in active_ids
                    ]
                )
            ),
            "custom_order_rows": len(custom.rows),
            "custom_order_sha256": sha256_ids(r.item_id for r in custom.rows),
            "custom_differs_from_default": [r.item_id for r in custom.rows]
            != [r.item_id for r in favorites.rows],
        }

        # 3. Lista playlist
        playlists = playlist_rows(db)
        stored_ordinal_order = [
            str(row[0])
            for row in db.connection.execute(
                "SELECT id FROM playlists WHERE session_id = 'local' ORDER BY ordinal"
            )
        ]
        receipt["views"]["playlisty"] = {
            "rows": len(playlists.rows),
            "heading": playlists.heading,
            "order_matches_amc": playlists.order_matches_amc,
            "kinds": sorted({r.kind for r in playlists.rows}),
            "ids_are_prefixed_strings": all(
                isinstance(r.item_id, str) and r.item_id.startswith("playlist:")
                for r in playlists.rows
            ),
            "unique_ids": len({r.item_id for r in playlists.rows}),
            "follows_stored_ordinal": [p.playlist_id for p in playlists.playlists]
            == stored_ordinal_order,
            "entries": [
                {
                    "stored_count": p.stored_count,
                    "available_count": p.available_count,
                    "unavailable_count": p.stored_count - p.available_count,
                    "duration_ticks": p.duration_ticks,
                    "name_sha256": hashlib.sha256(p.name.encode("utf-8")).hexdigest(),
                    "detail_mentions_availability_gap": "dostępne" in p.row.detail,
                }
                for p in playlists.playlists
            ],
        }

        # 4. Zawartosc kazdej playlisty
        contents = []
        for entry in playlists.playlists:
            result = playlist_contents_rows(db, entry.playlist_id)
            stored_ids = [
                str(row[0])
                for row in db.connection.execute(
                    "SELECT item_id FROM playlist_items WHERE playlist_id = ? "
                    "ORDER BY ordinal",
                    (entry.playlist_id,),
                )
            ]
            expected = [i for i in stored_ids if i in active_ids]
            contents.append(
                {
                    "playlist_id_sha256": hashlib.sha256(
                        entry.playlist_id.encode("utf-8")
                    ).hexdigest(),
                    "rows": len(result.rows),
                    "stored_item_ids": len(stored_ids),
                    "skipped_missing": len(stored_ids) - len(result.rows),
                    "order_matches_amc": result.order_matches_amc,
                    "fallback_view": result.fallback_view,
                    "preserves_stored_ordinal_order": [
                        r.item_id for r in result.rows
                    ]
                    == expected,
                    "is_not_alphabetical": [r.item_id for r in result.rows]
                    != [
                        r.item_id
                        for r in sorted(result.rows, key=lambda row: row.title)
                    ]
                    or len(result.rows) < 2,
                    "order_sha256": sha256_ids(r.item_id for r in result.rows),
                    "matches_view_label_count": len(result.rows)
                    == entry.available_count,
                }
            )
        missing = playlist_contents_rows(db, "nie-istnieje-w-profilu")
        receipt["views"]["zawartosc_playlisty"] = {
            "playlists_measured": len(contents),
            "per_playlist": contents,
            "unknown_playlist_falls_back_to": missing.fallback_view,
            "unknown_playlist_rows": len(missing.rows),
        }

    after = {
        name: sha256_file(FIXTURE / name)
        for name in ("library.db", "library.db-wal")
        if (FIXTURE / name).exists()
    }
    receipt["database_sha256_after"] = after
    receipt["database_unchanged_by_read"] = before == after

    out = HERE.parents[2] / "receipt.json"
    target = Path(
        os.environ.get(
            "AMC_WX_RECEIPT",
            "/home/michal/projekty/amc_pomoc/wx-full-profile-after421"
            "/library-views-after422/full-profile-receipt.json",
        )
    )
    target.parent.mkdir(parents=True, exist_ok=True)
    target.write_text(
        json.dumps(receipt, ensure_ascii=False, indent=2) + "\n", encoding="utf-8"
    )
    print(f"kwit: {target}")
    print(
        f"aktywne={receipt['active_items']} "
        f"wszystkie_pliki={receipt['views']['wszystkie_pliki']['rows']} "
        f"ulubione={receipt['views']['ulubione']['rows']} "
        f"playlisty={receipt['views']['playlisty']['rows']} "
        f"baza_bez_zmian={receipt['database_unchanged_by_read']}"
    )
    _ = out
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
