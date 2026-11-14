from __future__ import annotations

from dataclasses import dataclass
from types import SimpleNamespace

from amc_wx_lite.sonos_local import LocalSonosClient


@dataclass(eq=False)
class _Member:
    uid: str
    player_name: str


class _Favorite:
    def __init__(self, item_id: str, title: str, reference: object) -> None:
        self.item_id = item_id
        self.title = title
        self.reference = reference


@dataclass
class _Playlist:
    item_id: str
    title: str


class _Library:
    def __init__(self, favorite: object, playlist: object) -> None:
        self.favorite = favorite
        self.playlist = playlist

    def get_sonos_favorites(self, *, complete_result: bool) -> list[object]:
        assert complete_result is True
        return [self.favorite]

    def get_music_library_information(
        self, kind: str, *, complete_result: bool
    ) -> list[object]:
        assert kind == "sonos_playlists"
        assert complete_result is True
        return [self.playlist]


class _Coordinator(_Member):
    def __init__(self) -> None:
        super().__init__("player-private-one", "Salon")
        self.household_id = "house-private"
        self.transport_state = "PLAYING"
        self.track = {
            "title": "Audycja poranna",
            "artist": "Radio Poznań",
            "album": "",
            "position": "0:01:00",
            "duration": "0:10:00",
        }
        self.fixed_volume = False
        self.queue: list[object] = []
        self.played_queue_indices: list[int] = []
        self.played_uris: list[tuple[str, str, bool, bool]] = []
        self.reject_forced_radio = False
        self.pause_fails = False
        self.stop_calls = 0
        self.seeks: list[str] = []
        reference = _Playlist("reference-private", "Treść ulubionego")
        self.music_library = _Library(
            _Favorite("favorite-private", "Radio ulubione", reference),
            _Playlist("playlist-private", "Lista wieczorna"),
        )

    def get_current_transport_info(self) -> dict[str, str]:
        return {"current_transport_state": self.transport_state}

    def get_current_track_info(self) -> dict[str, str]:
        return dict(self.track)

    def get_current_media_info(self) -> dict[str, str]:
        return {"channel": ""}

    def pause(self) -> None:
        if self.pause_fails:
            raise RuntimeError("pause unsupported")
        self.transport_state = "PAUSED_PLAYBACK"

    def stop(self) -> None:
        self.stop_calls += 1
        self.transport_state = "STOPPED"

    def play(self) -> None:
        self.transport_state = "PLAYING"

    def previous(self) -> None:
        self.track["title"] = "Poprzednia audycja"

    def next(self) -> None:
        self.track["title"] = "Następna audycja"

    def seek(self, value: str) -> None:
        self.seeks.append(value)
        self.track["position"] = value

    def add_to_queue(self, value: object) -> int:
        self.queue.append(value)
        return len(self.queue)

    def play_from_queue(self, index: int) -> None:
        self.played_queue_indices.append(index)
        self.transport_state = "PLAYING"

    def play_uri(
        self, *, uri: str, title: str, start: bool, force_radio: bool
    ) -> None:
        self.played_uris.append((uri, title, start, force_radio))
        if force_radio and self.reject_forced_radio:
            raise RuntimeError("forced radio URI rejected")
        self.track["title"] = title
        self.track["artist"] = ""
        self.transport_state = "PLAYING"


class _Group:
    def __init__(self, coordinator: _Coordinator) -> None:
        self.uid = "group-private"
        self.coordinator = coordinator
        self.members = {
            coordinator,
            _Member("player-private-two", "Kuchnia"),
        }
        self.volume = 30
        self.mute = False
        self.relative_changes: list[int] = []

    def set_relative_volume(self, value: int) -> int:
        self.relative_changes.append(value)
        self.volume = max(0, min(100, self.volume + value))
        return self.volume


class _Discovery:
    def __init__(self, groups: list[_Group]) -> None:
        self.groups = groups
        self.calls: list[tuple[float, bool]] = []

    def discover(self, *, timeout: float, include_invisible: bool) -> list[object]:
        self.calls.append((timeout, include_invisible))
        zones: list[object] = []
        for group in self.groups:
            for member in group.members:
                zones.append(SimpleNamespace(group=group, member=member))
        return zones


class _Host:
    def __init__(self) -> None:
        self.calls: list[tuple[str, str]] = []

    def sonos_materials(self, household_id: str, kind: str) -> dict:
        self.calls.append((household_id, kind))
        return {
            "items": [{
                "itemId": "sonos-stream:stream-private",
                "materialId": "stream-private",
                "title": "Radio domowe",
                "kind": "ownStream",
                "detail": "Stacja zapisana w AMC",
                "streamUrl": "https://secret.example/live",
            }],
            "message": "Moje stacje, 1 element",
            "sortMode": "Custom",
        }


def _client() -> tuple[LocalSonosClient, _Coordinator, _Group, _Host, _Discovery]:
    coordinator = _Coordinator()
    group = _Group(coordinator)
    host = _Host()
    discovery = _Discovery([group])
    client = LocalSonosClient(
        host,
        discovery_api=discovery,
        discovery_timeout=0.4,
        sleeper=lambda _seconds: None,
    )
    return client, coordinator, group, host, discovery


def test_local_targets_expose_only_intentional_labels() -> None:
    client, _coordinator, _group, _host, discovery = _client()

    payload = client.sonos_targets()

    assert discovery.calls == [(0.4, False)]
    assert payload["message"] == "1 grupę Sonos"
    row = payload["items"][0]
    assert row["title"] == "Kuchnia, Salon"
    assert row["detail"] == "głośników: 2, odtwarzanie"
    assert row["playerIds"] == ["player-private-one", "player-private-two"]
    spoken = row["title"] + " " + row["detail"]
    assert "private" not in spoken
    assert "SimpleNamespace" not in spoken


def test_empty_local_discovery_gives_actionable_nontechnical_message() -> None:
    client = LocalSonosClient(
        _Host(),
        discovery_api=_Discovery([]),
        sleeper=lambda _seconds: None,
    )

    payload = client.sonos_targets()

    assert payload["items"] == []
    assert "tej samej sieci lokalnej" in payload["message"]
    assert "UPnP" in payload["message"]


def test_local_snapshot_and_transport_keep_existing_contract() -> None:
    client, coordinator, group, _host, _discovery = _client()
    targets = client.sonos_targets()
    row = targets["items"][0]

    state = client.sonos_snapshot(row["householdId"], row["groupId"])

    assert state["groupTitle"] == "Kuchnia, Salon"
    assert state["title"] == "Audycja poranna"
    assert state["source"] == "Radio Poznań"
    assert state["playbackState"] == "playing"
    assert state["positionSeconds"] == 60
    assert state["durationSeconds"] == 600
    assert state["volume"] == 30
    assert "private" not in state["message"]

    paused = client.sonos_transport(
        row["householdId"], row["groupId"], "toggle"
    )
    assert paused["playbackState"] == "paused"

    louder = client.sonos_transport(
        row["householdId"], row["groupId"], "volumeUp5"
    )
    assert group.relative_changes == [5]
    assert louder["volume"] == 35

    client.sonos_transport(
        row["householdId"], row["groupId"], "seekRelative", seconds=30
    )
    assert coordinator.seeks == ["0:01:30"]


def test_local_library_loads_favorites_playlists_and_profile_streams() -> None:
    client, coordinator, _group, host, _discovery = _client()
    target = client.sonos_targets()["items"][0]
    household_id = target["householdId"]
    group_id = target["groupId"]

    library = client.sonos_library()
    assert [item["title"] for item in library["items"]] == [
        "Ulubione Sonos",
        "Playlisty Sonos",
        "Moje stacje",
    ]

    favorites = client.sonos_materials(household_id, "favorite")
    assert favorites["items"][0]["title"] == "Radio ulubione"
    assert "private" not in favorites["items"][0]["title"]
    favorite_state = client.sonos_load_material(
        household_id,
        group_id,
        "favorite",
        favorites["items"][0]["materialId"],
    )
    assert coordinator.queue[0].title == "Treść ulubionego"
    assert coordinator.played_queue_indices == [0]
    assert favorite_state["message"] == "Uruchamianie: Radio ulubione"

    playlists = client.sonos_materials(household_id, "playlist")
    client.sonos_load_material(
        household_id,
        group_id,
        "playlist",
        playlists["items"][0]["materialId"],
    )
    assert coordinator.queue[1].title == "Lista wieczorna"
    assert coordinator.played_queue_indices == [0, 1]

    streams = client.sonos_materials(household_id, "ownStream")
    assert host.calls == [(household_id, "ownStream")]
    stream_state = client.sonos_load_material(
        household_id,
        group_id,
        "ownStream",
        streams["items"][0]["materialId"],
    )
    assert coordinator.played_uris == [(
        "https://secret.example/live",
        "Radio domowe",
        True,
        True,
    )]
    assert "secret.example" not in repr(stream_state)
    assert stream_state["message"] == "Uruchamianie: Radio domowe"


def test_local_transport_stops_a_radio_when_pause_is_not_supported() -> None:
    client, coordinator, _group, _host, _discovery = _client()
    target = client.sonos_targets()["items"][0]
    coordinator.pause_fails = True

    state = client.sonos_transport(
        target["householdId"], target["groupId"], "toggle"
    )

    assert coordinator.stop_calls == 1
    assert state["playbackState"] == "idle"


def test_own_stream_retries_as_direct_uri_when_radio_uri_is_rejected() -> None:
    client, coordinator, _group, _host, _discovery = _client()
    target = client.sonos_targets()["items"][0]
    streams = client.sonos_materials(target["householdId"], "ownStream")
    coordinator.reject_forced_radio = True

    state = client.sonos_load_material(
        target["householdId"],
        target["groupId"],
        "ownStream",
        streams["items"][0]["materialId"],
    )

    assert [call[3] for call in coordinator.played_uris] == [True, False]
    assert state["playbackState"] == "playing"


