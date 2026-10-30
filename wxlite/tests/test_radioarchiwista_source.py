"""Testy źródła Radio Archiwista na syntetycznym katalogu.

Zero sieci: fetcher i pobieranie są podmieniane atrapami, a katalog ma
dokładnie ten sam schemat, co publiczny (catalog_version 2).
"""

from __future__ import annotations

import json
from pathlib import Path
from typing import Any

from amc_wx_lite import radioarchiwista_source as rarch


def _fixture() -> dict[str, Any]:
    stations = {
        "catalog_version": 2,
        "generated_at": "2026-10-29T20:34:59Z",
        "latest_archive_hour_started_at": "2026-10-29T19:00:00+00:00",
        "stations": [
            {
                "slug": "r357",
                "display_name": "Radio 357",
                "public_name": "Radio 357",
                "archive_count": 5100,
                "timezone": "Europe/Warsaw",
            },
            {
                "slug": "pr1",
                "display_name": "Program 1",
                "public_name": "Polskie Radio Program 1",
                "archive_count": 4895,
                "timezone": "Europe/Warsaw",
            },
        ],
    }
    day = {
        "archives": [
            {
                "archive_item_identifier": "pr1-2026-10-29",
                "audio_url": "https://archive.org/download/pr1-2026-10-29/godz15.mp3",
                "completeness_ratio": 1.0,
                "day": 29,
                "duration_seconds": 3600.5,
                "hour": 15,
                "local_date_label": "29.10.2026",
                "local_hour_label": "15:00",
                "local_label": "29.10.2026 15:00 czasu polskiego",
                "local_started_at": "2026-10-29T15:00:00+01:00",
                "status": "published",
                "utc_started_at": "2026-10-29T14:00:00+00:00",
            },
            {
                "archive_item_identifier": "pr1-2026-10-29",
                "audio_url": "https://archive.org/download/pr1-2026-10-29/godz00.mp3",
                "completeness_ratio": 0.42,
                "day": 29,
                "duration_seconds": 1512.0,
                "hour": 0,
                "local_date_label": "29.10.2026",
                "local_hour_label": "00:00",
                "local_label": "29.10.2026 00:00 czasu polskiego",
                "local_started_at": "2026-10-28T23:00:00+00:00",
                "status": "published",
                "utc_started_at": "2026-10-28T23:00:00+00:00",
            },
            # Uszkodzony wpis: bez adresu audio ma zostać pominięty.
            {"hour": 6, "utc_started_at": "2026-10-29T05:00:00+00:00"},
        ]
    }
    return {
        "https://radioarchiwista.github.io/data/stations.json": stations,
        "https://radioarchiwista.github.io/data/stations/pr1/index.json": {"years": [2026, 2025]},
        "https://radioarchiwista.github.io/data/stations/pr1/2026/index.json": {"months": [10, 9]},
        "https://radioarchiwista.github.io/data/stations/pr1/2026/10/index.json": {"days": [29, 28]},
        "https://radioarchiwista.github.io/data/stations/pr1/2026/10/29.json": day,
    }


class _FakeResponse:
    def __init__(self, payload: bytes, status: int = 200, headers: dict | None = None):
        self._payload = payload
        self.status = status
        self.headers = headers or {"Content-Length": str(len(payload))}

    def read(self, size: int = -1) -> bytes:
        if size is None or size < 0 or size >= len(self._payload):
            chunk, self._payload = self._payload, b""
        else:
            chunk, self._payload = self._payload[:size], self._payload[size:]
        return chunk

    def __enter__(self) -> "_FakeResponse":
        return self

    def __exit__(self, *_args: object) -> None:
        return None


def _source(tmp_path: Path, catalog: dict[str, Any] | None = None):
    catalog = catalog if catalog is not None else _fixture()
    calls: list[str] = []

    def fetch_json(url: str) -> object:
        calls.append(url)
        if url in catalog:
            return json.loads(json.dumps(catalog[url]))
        raise OSError(f"404 dla {url}")

    source = rarch.RadioarchiwistaSource(tmp_path / "radioarchiwista", fetch_json=fetch_json)
    return source, calls


def test_resolve_catalog_path_podstawia_parametry_przed_zlaczeniem() -> None:
    url = rarch.resolve_catalog_path(
        "https://radioarchiwista.github.io/",
        "./data/stations/{slug}/{year}/{month}/{day}.json",
        slug="pr1", year=2026, month=10, day=29,
    )
    assert url == "https://radioarchiwista.github.io/data/stations/pr1/2026/10/29.json"
    assert "{" not in url and "}" not in url


def test_parse_stations_index_odrzuca_nieznana_wersje() -> None:
    try:
        rarch.parse_stations_index({"catalog_version": 3, "stations": []})
    except rarch.RadioarchiwistaError as error:
        assert "nieznaną wersję" in str(error)
    else:
        raise AssertionError("oczekiwano odmowy przy nieznanej wersji katalogu")


def test_station_rows_sa_posortowane_z_licznikiem_w_tytule() -> None:
    parsed = rarch.parse_stations_index(_fixture()["https://radioarchiwista.github.io/data/stations.json"])
    rows = rarch.station_rows(parsed)
    assert [row.title for row in rows] == [
        "Polskie Radio Program 1, 4895 nagrań",
        "Radio 357, 5100 nagrań",
    ]
    # Licznik w TYTULE, nie w kolumnie szczegolow: czytnik ekranu ogłasza
    # caly wiersz jednym ciagiem, bez naglowka kolumny przed liczba.
    assert all(row.detail == "" for row in rows)
    assert rows[0].item_id == "rarch:station:pr1"
    assert all("pr1" not in row.title for row in rows)


def test_wiersze_okresow_chrona_malejaco_z_polskimi_nazwami() -> None:
    years = rarch.year_rows("pr1", (2026, 2025))
    assert [row.title for row in years] == ["2026", "2025"]
    months = rarch.month_rows("pr1", 2026, (10, 9))
    assert [row.title for row in months] == ["październik", "wrzesień"]
    days = rarch.day_rows("pr1", 2026, 10, (29, 28))
    assert [row.title for row in days] == ["29.10.2026", "28.10.2026"]
    assert days[0].kind == "folder"


def test_nagrania_dnia_rosnaco_z_kompletnoscia_i_pozycja() -> None:
    parsed = rarch.parse_stations_index(_fixture()["https://radioarchiwista.github.io/data/stations.json"])
    station = parsed.find("pr1")
    assert station is not None
    day_payload = _fixture()["https://radioarchiwista.github.io/data/stations/pr1/2026/10/29.json"]
    recordings = rarch.parse_recordings(day_payload, station)
    assert [rec.hour for rec in recordings] == [0, 15]
    rows = rarch.recording_rows(recordings)
    assert [row.title for row in rows] == ["00:00", "15:00"]
    assert "nagranie niepełne" in rows[0].detail
    assert rows[1].detail == ""
    assert rows[1].duration_ticks == 36005000000
    assert rows[1].url.endswith("godz15.mp3")

    positioned = rarch.recording_rows(
        recordings,
        positions={recordings[1].item_id: 600.0},
    )
    assert "pozostało 50 min" in positioned[1].detail

    starred = rarch.recording_rows(recordings, favorite_ids={recordings[1].item_id})
    assert starred[1].state_prefix == "★ "
    assert starred[0].state_prefix == ""


def test_nagrania_stronnicuja_i_dokladaja_load_more() -> None:
    parsed = rarch.parse_stations_index(_fixture()["https://radioarchiwista.github.io/data/stations.json"])
    station = parsed.find("pr1")
    assert station is not None
    base = _fixture()["https://radioarchiwista.github.io/data/stations/pr1/2026/10/29.json"]["archives"][0]
    many = {
        "archives": [
            dict(base, hour=hour, utc_started_at=f"2026-10-29T{hour - 1:02d}:00:00+00:00")
            for hour in range(24)
        ]
    }
    recordings = rarch.parse_recordings(many, station)
    rows = rarch.recording_rows(recordings, loaded_count=10)
    assert len([row for row in rows if row.kind == "recording"]) == 10
    assert rows[-1].kind == "loadMore"
    assert "10 z 24" in rows[-1].title


def test_stabilny_identyfikator_nagrania() -> None:
    first = rarch.recording_item_id("pr1", "2026-10-29T14:00:00+00:00")
    assert first == rarch.recording_item_id("pr1", "2026-10-29T14:00:00+00:00")
    assert first != rarch.recording_item_id("pr1", "2026-10-29T15:00:00+00:00")
    try:
        rarch.station_item_id("../przejscie")
    except rarch.RadioarchiwistaError:
        pass
    else:
        raise AssertionError("nieprawidłowy slug musiał zostać odrzucony")


def test_zrodlo_cacheuje_poziomy_i_wraca_do_starego_cache() -> None:
    import tempfile
    with tempfile.TemporaryDirectory() as directory:
        source, calls = _source(Path(directory))
        index, from_cache = source.stations()
        assert from_cache is False
        assert len(calls) == 1
        index_again, from_cache_again = source.stations()
        assert from_cache_again is True
        assert len(calls) == 1
        assert [station.slug for station in index_again.stations] == ["r357", "pr1"]

        source.stations(force=True)
        assert len(calls) == 2


def test_zrodlo_odmawia_uczciwie_bez_sieci_i_bez_cache() -> None:
    import tempfile
    with tempfile.TemporaryDirectory() as directory:
        def fail(_url: str) -> object:
            raise OSError("brak sieci")

        source = rarch.RadioarchiwistaSource(Path(directory) / "r", fetch_json=fail)
        try:
            source.stations()
        except rarch.RadioarchiwistaError as error:
            assert "Nie udało się wczytać" in str(error)
        else:
            raise AssertionError("oczekiwano uczciwej odmowy bez sieci")


def test_zrodlo_oddaje_stary_cache_gdy_siec_padla() -> None:
    import tempfile
    with tempfile.TemporaryDirectory() as directory:
        source, calls = _source(Path(directory))
        source.stations(force=True)
        before = len(calls)

        def fail(_url: str) -> object:
            raise OSError("siec padla")

        source._fetch_json = fail  # celowo: awaria PO pierwszym wczytaniu
        index, from_cache = source.stations(force=True)
        assert from_cache is True
        assert index.stations, "stary cache ma pozwolić pracować offline"
        assert len(calls) == before


def test_najnowszy_dzien_stacji() -> None:
    import tempfile
    with tempfile.TemporaryDirectory() as directory:
        source, _calls = _source(Path(directory))
        recordings, year, month, day, _from_cache = source.latest_day("pr1")
        assert (year, month, day) == (2026, 10, 29)
        assert [rec.hour for rec in recordings] == [0, 15]


def test_ulubione_przelaczaja_i_buduja_wiersze_bez_sieci() -> None:
    import tempfile
    with tempfile.TemporaryDirectory() as directory:
        source, _calls = _source(Path(directory))
        recordings, *_rest = source.latest_day("pr1")
        favorite = recordings[1]

        assert source.toggle_favorite(favorite) is True
        assert source.is_favorite(favorite.item_id)
        rows = rarch.favorite_rows(source.load_favorites())
        assert len(rows) == 1
        assert rows[0].title == "Polskie Radio Program 1, 29.10.2026 15:00 czasu polskiego"
        assert rows[0].url.endswith("godz15.mp3")

        assert source.toggle_favorite(favorite) is False
        assert not source.is_favorite(favorite.item_id)


def test_pozycje_zapisuja_sie_i_czyszcza() -> None:
    import tempfile
    with tempfile.TemporaryDirectory() as directory:
        source, _calls = _source(Path(directory))
        source.save_position("rarch:rec:pr1:x", 123.0)
        assert source.position_for("rarch:rec:pr1:x") == 123.0
        source.save_position("rarch:rec:pr1:x", 0.5)
        assert source.position_for("rarch:rec:pr1:x") == 0.0


def test_ustawienia_bazy_katalogu() -> None:
    import tempfile
    with tempfile.TemporaryDirectory() as directory:
        source, _calls = _source(Path(directory))
        assert source.catalog_base() == rarch.DEFAULT_CATALOG_BASE
        source.write_settings(catalog_base="https://stareaudycje.pl/")
        assert source.catalog_base() == "https://stareaudycje.pl/"


def test_pobieranie_zapisuje_plik_i_zglasza_postep() -> None:
    import tempfile
    with tempfile.TemporaryDirectory() as directory:
        payload = b"MP3" * 1000
        progress_values: list[int] = []

        def fetch(_url: str) -> _FakeResponse:
            return _FakeResponse(payload)

        destination = Path(directory) / "nagranie.mp3"
        result = rarch.stream_to_file(
            "https://archive.org/download/x/a.mp3",
            destination,
            progress=progress_values.append,
            fetch=fetch,
        )
        assert result == destination
        assert destination.read_bytes() == payload
        assert not destination.with_name(destination.name + ".part").exists()
        assert progress_values and progress_values[-1] == 100


def test_pobieranie_anulowane_nie_zostawia_smieci() -> None:
    import tempfile
    with tempfile.TemporaryDirectory() as directory:
        def fetch(_url: str) -> _FakeResponse:
            return _FakeResponse(b"MP3" * 1000)

        destination = Path(directory) / "nagranie.mp3"
        try:
            rarch.stream_to_file(
                "https://archive.org/download/x/a.mp3",
                destination,
                should_cancel=lambda: True,
                fetch=fetch,
            )
        except rarch.RadioarchiwistaError as error:
            assert "anulowane" in str(error)
        else:
            raise AssertionError("oczekiwano odmowy przy anulowaniu")
        assert not destination.exists()
        assert not destination.with_name(destination.name + ".part").exists()


def test_pobieranie_pustego_pliku_to_blad() -> None:
    import tempfile
    with tempfile.TemporaryDirectory() as directory:
        def fetch(_url: str) -> _FakeResponse:
            return _FakeResponse(b"")

        destination = Path(directory) / "pusty.mp3"
        try:
            rarch.stream_to_file("https://x/y.mp3", destination, fetch=fetch)
        except rarch.RadioarchiwistaError as error:
            assert "pusty" in str(error)
        else:
            raise AssertionError("pusty plik nie może uchodzić za pobrany")
        assert not destination.with_name(destination.name + ".part").exists()


def test_najnowsze_nagrania_ostatniej_godziny(tmp_path: Path) -> None:
    catalog = _fixture()
    day_url = "https://radioarchiwista.github.io/data/stations/pr1/2026/10/29.json"
    catalog[day_url]["archives"].append(
        {
            "archive_item_identifier": "pr1-2026-10-29",
            "audio_url": "https://archive.org/download/pr1-2026-10-29/godz20.mp3",
            "completeness_ratio": 1.0,
            "day": 29,
            "duration_seconds": 3600.0,
            "hour": 20,
            "local_date_label": "29.10.2026",
            "local_hour_label": "20:00",
            "local_label": "29.10.2026 20:00 czasu polskiego",
            "local_started_at": "2026-10-29T20:00:00+01:00",
            "status": "published",
            "utc_started_at": "2026-10-29T19:00:00+00:00",
        }
    )
    source, _calls = _source(tmp_path, catalog)
    recordings, stamp = source.latest_recordings()
    assert stamp.startswith("2026-10-29T19")
    # Tylko nagranie godziny 19:00 UTC; Radio 357 nie ma shardów i jest pominięte.
    assert [rec.local_hour_label for rec in recordings] == ["20:00"]


def test_najnowsza_godzina_laczy_nagrania_wielu_stacji(tmp_path: Path) -> None:
    catalog = _fixture()
    pr1_day = catalog["https://radioarchiwista.github.io/data/stations/pr1/2026/10/29.json"]
    pr1_day["archives"].append({
        "archive_item_identifier": "pr1-2026-10-29",
        "audio_url": "https://archive.org/download/pr1-2026-10-29/godz20.mp3",
        "completeness_ratio": 1.0,
        "day": 29,
        "duration_seconds": 3600.0,
        "hour": 20,
        "local_date_label": "29.10.2026",
        "local_hour_label": "20:00",
        "local_label": "29.10.2026 20:00 czasu polskiego",
        "local_started_at": "2026-10-29T20:00:00+01:00",
        "status": "published",
        "utc_started_at": "2026-10-29T19:00:00+00:00",
    })
    catalog["https://radioarchiwista.github.io/data/stations/r357/2026/10/29.json"] = {
        "archives": [{
            "archive_item_identifier": "r357-2026-10-29",
            "audio_url": "https://archive.org/download/r357-2026-10-29/godz20.mp3",
            "completeness_ratio": 1.0,
            "day": 29,
            "duration_seconds": 3600.0,
            "hour": 20,
            "local_date_label": "29.10.2026",
            "local_hour_label": "20:00",
            "local_label": "29.10.2026 20:00 czasu polskiego",
            "local_started_at": "2026-10-29T20:00:00+01:00",
            "status": "published",
            "utc_started_at": "2026-10-29T19:00:00+00:00",
        }]
    }
    source, _calls = _source(tmp_path, catalog)
    recordings, _stamp = source.latest_recordings()
    # Dni stacji czytane równolegle nadal dają jeden scalony, posortowany
    # widok: obie stacje nagrały godzinę 19:00 UTC.
    assert [rec.station_name for rec in recordings] == [
        "Polskie Radio Program 1",
        "Radio 357",
    ]


def test_najnowsze_nagrania_odmawiaja_gdy_nikt_nie_nagra(tmp_path: Path) -> None:
    source, _calls = _source(tmp_path)  # brak nagrania godziny 19:00 UTC
    try:
        source.latest_recordings()
    except rarch.RadioarchiwistaError as error:
        assert "ostatniej godziny" in str(error)
    else:
        raise AssertionError("oczekiwano uczciwej odmowy")


def test_recording_by_id_pamietta_dzien_i_ulubione(tmp_path: Path) -> None:
    source, _calls = _source(tmp_path)
    recordings, _from_cache = source.recordings("pr1", 2026, 10, 29)
    found = source.recording_by_id(recordings[0].item_id)
    assert found is not None and found.audio_url == recordings[0].audio_url

    fresh, _calls2 = _source(tmp_path)
    assert fresh.recording_by_id(recordings[0].item_id) is None
    fresh.toggle_favorite(recordings[0])
    again = fresh.recording_by_id(recordings[0].item_id)
    assert again is not None and again.audio_url == recordings[0].audio_url

    favorite_rows = rarch.favorite_rows(
        fresh.load_favorites(),
        positions={recordings[0].item_id: 240.0},
    )
    assert favorite_rows[0].position_seconds == 240.0
    assert favorite_rows[0].kind == "recording"


def test_station_rows_niosa_service_kind_dla_nawigacji() -> None:
    # Regresja: Enter na stacji działa dzięki service_kind; bez niego
    # nawigator nie rozpoznaje wiersza poziomu stacji.
    rows = rarch.station_rows(rarch.parse_stations_index(_fixture()[rarch.DEFAULT_CATALOG_BASE + "data/stations.json"]))
    assert rows
    assert all(row.service_kind == "rarchStation" for row in rows)
    assert {row.service_id for row in rows} == {"r357", "pr1"}


def test_search_rows_bez_sieci_vracaja_ulubione(tmp_path: Path) -> None:
    source, calls = _source(tmp_path)
    source.toggle_favorite(_favorite_first_recording(source))
    calls.clear()
    rows = source.search_rows()
    # Kluczowa obietnica: wyszukiwanie nie schodzi do sieci.
    assert calls == []
    assert "recording" in [row.kind for row in rows]


def test_search_rows_dokladaja_stacje_z_cache(tmp_path: Path) -> None:
    source, calls = _source(tmp_path)
    source.stations()  # rozgrzewa cache stacji
    source.toggle_favorite(_favorite_first_recording(source))
    calls.clear()
    rows = source.search_rows()
    assert calls == []
    kinds = [row.kind for row in rows]
    assert kinds.count("folder") == 2  # Radio 357, PR1
    assert kinds.count("recording") == 1


def _favorite_first_recording(source) -> object:
    # W fixture tylko PR1 ma shard dnia 29.10.2026.
    recordings, _ = source.recordings("pr1", 2026, 10, 29)
    return recordings[0]


def test_starsze_nagrania_ogg_dostaja_url_mp3(tmp_path: Path) -> None:
    # Archiwum 2025 i wczesniejsze trzyma nagrania jako OGG; Media Foundation
    # ich nie dekoduje, a archive.org wystawia transkod MP3 pod ta sama nazwa.
    katalog = _fixture()
    dzien = "https://radioarchiwista.github.io/data/stations/pr1/2026/10/29.json"
    katalog[dzien]["archives"][0]["audio_url"] = (
        "https://archive.org/download/pr1-2025-10-27/pr1_2025-10-27T23-00-00Z.ogg"
    )
    source, _calls = _source(tmp_path, katalog)
    recordings, _from_cache = source.recordings("pr1", 2026, 10, 29)
    # Katalog dnia sortuje rosnaco po godzinie; podmieniony wpis godziny 15.
    assert recordings[1].audio_url == (
        "https://archive.org/download/pr1-2025-10-27/pr1_2025-10-27T23-00-00Z.mp3"
    )
    assert all(not rec.audio_url.endswith(".ogg") for rec in recordings)

    # Ulubione zapisane przed poprawka, jeszcze z adresem OGG, wraca jako MP3.
    favorites_path = tmp_path / "radioarchiwista" / "favorites.json"
    favorites_path.write_text(json.dumps([
        {
            "item_id": "rarch:rec:pr1:stary",
            "station_slug": "pr1",
            "station_name": "Polskie Radio Program 1",
            "title": "27.10.2025 23:00",
            "audio_url": "https://archive.org/download/pr1-2025-10-27/pr1_2025-10-27T23-00-00Z.ogg",
            "utc_started_at": "2025-10-27T23:00:00+00:00",
        }
    ]), encoding="utf-8")
    rows = rarch.favorite_rows(source.load_favorites())
    assert len(rows) == 1
    assert rows[0].url.endswith("pr1_2025-10-27T23-00-00Z.mp3")


def test_najnowsza_godzina_etykieta_czasu_lokalnego() -> None:
    # Zgłoszenie z odbioru: komunikat widoku podawał godzinę UTC, a to są
    # stacje polskie. Etykieta ma iść z czasu lokalnego nagrań, a bez nagrań
    # z konwersji znacznika katalogu na czas warszawski.
    from amc_wx_lite import radioarchiwista_source as rarch2

    nagranie = rarch2.ArchiveRecording(
        station_slug="pr1", station_name="Polskie Radio Program 1",
        item_id="rarch:rec:pr1:a", title="x",
        audio_url="https://example.net/a.mp3", duration_seconds=3600.0,
        local_date_label="29.10.2026", local_hour_label="23:00",
        local_label="29.10 23:00", utc_started_at="2026-10-29T23:00:00Z",
        hour=23, completeness_ratio=1.0, status="published",
    )
    assert rarch2.latest_local_label((nagranie,), "2026-10-29T21:00:00Z") == (
        "29.10.2026 23:00"
    )
    # Pusty widok: 23:00 UTC konwertuje sie na polska 00:00 dnia nastepnego
    # (koniec pazdziernika 2026 to czas zimowy, CET, +1).
    assert rarch2.latest_local_label((), "2026-10-29T23:00:00+00:00") == (
        "30.10.2026 00:00"
    )
    assert rarch2.latest_local_label((), "") == ""
