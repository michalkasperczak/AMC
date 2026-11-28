from __future__ import annotations

from types import SimpleNamespace

import pytest

from amc_wx_lite import i18n, menu_model, profile_presets
from amc_wx_lite.command_palette import entries_for_session
from amc_wx_lite.gui import LiteFrame
from amc_wx_lite.list_model import Row
from amc_wx_lite.navigation import (
    LibraryView, Navigator, OpenPodcastPlaylistView, OpenPodcastView,
    PlayMedia, SessionId, View,
)
from amc_wx_lite.podcast_source import PodcastSource, _EpisodeRecord
from amc_wx_lite.profile_presets import PresetEntry, ResolvedPreset, target_from_row
from amc_wx_lite.shortcuts import (
    ASSIGN_PRESET_ACTIONS, PRESET_ACTIONS, Action, chord_from_canonical, describe, resolve,
)


def _rows():
    channel = Row("channel-id", "Kanał filmów", "podcast",
                  service_kind="youtubeChannel")
    podcast = Row("podcast-id", "Audycja", "podcast")
    playlist = Row("podcast-playlist:abc", "Lista filmów", "playlist")
    episode = Row("episode-id", "Film przykładowy", "episode",
                  path="https://example.org/watch", parent_id="channel-id",
                  service_kind="youtubeVideo")
    return channel, podcast, playlist, episode


def _frame(entry: PresetEntry, monkeypatch, *, episode: Row | None = None):
    navigator = Navigator()
    navigator.switch_session(SessionId.PODCASTS)
    spoken: list[str] = []
    opened: list[object] = []
    played: list[object] = []
    frame = SimpleNamespace(
        navigator=navigator,
        layout=SimpleNamespace(state_json="unused"),
        announcer=SimpleNamespace(say=spoken.append),
        _preset_inputs=lambda _session: ({}, []),
        _last_preset_slot_by_session={},
        _open_podcast_view=opened.append,
        _open_podcast_playlist_view=opened.append,
        _session_open_player_on_preset=lambda _session: False,
        _run=lambda events: played.extend(events),
        podcasts=SimpleNamespace(episode_row=lambda _item_id: episode),
    )
    frame.runner = SimpleNamespace(
        submit=lambda _name, work, done, _failed: done(work())
    )
    monkeypatch.setattr(
        profile_presets, "resolve_preset",
        lambda *_args, **_kwargs: ResolvedPreset(entry),
    )
    return frame, spoken, opened, played


def test_podcast_and_youtube_targets_preserve_kind_and_stable_identity():
    channel, podcast, playlist, episode = _rows()
    assert target_from_row(SessionId.PODCASTS, channel).target_kind == "podcast-source"
    assert target_from_row(SessionId.PODCASTS, podcast).target_id == "podcast-id"
    playlist_target = target_from_row(SessionId.PODCASTS, playlist)
    assert (playlist_target.target_kind, playlist_target.location) == (
        "podcast-playlist", "abc"
    )
    episode_target = target_from_row(SessionId.PODCASTS, episode)
    assert (episode_target.target_kind, episode_target.target_id,
            episode_target.location) == ("podcast-episode", "episode-id", None)
    assert target_from_row(SessionId.PODCASTS, Row(
        "missing", "Bez źródła", "episode"
    )) is None
    assert target_from_row(SessionId.PODCASTS, Row(
        "load-more", "Więcej", "loadMore"
    )) is None


def test_fresh_episode_lookup_uses_current_library_record(monkeypatch):
    record = _EpisodeRecord(
        item_id="episode-id", subscription_id="channel-id",
        title="Aktualny tytuł", parent_title="Kanał filmów", source_kind=2,
        published_ticks=0, is_new=False, is_started=False, is_played=False,
        download_path="", payload={"MediaUrl": "https://example.org/new"},
    )
    source = object.__new__(PodcastSource)
    monkeypatch.setattr(
        source, "_episode_records_by_ids",
        lambda ids: {ids[0]: record},
    )
    row = source.episode_row("episode-id")
    assert row.kind == "episode"
    assert row.path == "https://example.org/new"
    assert row.service_kind == "youtubeVideo"


@pytest.mark.parametrize("kind,row_index,intent_type", [
    ("podcast-source", 0, OpenPodcastView),
    ("podcast-source", 1, OpenPodcastView),
    ("podcast-playlist", 2, OpenPodcastPlaylistView),
])
def test_container_presets_open_a_list(monkeypatch, kind, row_index, intent_type):
    row = _rows()[row_index]
    target = target_from_row(SessionId.PODCASTS, row)
    frame, spoken, opened, played = _frame(target.as_entry(4), monkeypatch)
    LiteFrame._activate_preset(frame, 4)
    assert len(opened) == 1 and isinstance(opened[0], intent_type)
    assert played == []
    assert spoken == []
    assert frame._last_preset_slot_by_session[SessionId.PODCASTS] == 4


def test_legacy_podcast_playlist_is_not_sent_to_files(monkeypatch):
    entry = PresetEntry(3, "podcast-playlist:abc", "amcPlaylist",
                        "Stara lista", "abc")
    frame, spoken, opened, played = _frame(entry, monkeypatch)
    LiteFrame._activate_preset(frame, 3)
    assert len(opened) == 1
    assert isinstance(opened[0], OpenPodcastPlaylistView)
    assert opened[0].playlist_id == "abc"
    assert played == [] and spoken == []


def test_episode_preset_plays_one_fresh_item_without_moving_list_cursor(monkeypatch):
    episode = _rows()[3]
    entry = target_from_row(SessionId.PODCASTS, episode).as_entry(2)
    frame, spoken, opened, played = _frame(entry, monkeypatch, episode=episode)
    other = Row("other", "Bieżące zaznaczenie", "podcast")
    frame.navigator.session.model.replace([other])
    LiteFrame._activate_preset(frame, 2)
    assert opened == [] and spoken == []
    assert len(played) == 2 and isinstance(played[0], PlayMedia)
    assert played[0].source == episode.path
    assert frame.navigator.session.model.selected_id == "other"
    assert frame.navigator.session.view is View.LIST
    assert frame.navigator.session.playback_source_rows == (episode,)


def test_missing_or_unplayable_episode_reports_user_facing_text(monkeypatch):
    entry = PresetEntry(7, "gone", "podcast-episode", "Nieaktualny film", None)
    frame, spoken, opened, played = _frame(entry, monkeypatch)
    LiteFrame._activate_preset(frame, 7)
    assert spoken == [
        "Odcinek lub film z presetu 7 nie jest już dostępny w bibliotece"
    ]
    assert opened == [] and played == []
    frame.podcasts.episode_row = lambda _id: Row("gone", "Film", "episode")
    LiteFrame._activate_preset(frame, 7)
    assert spoken[-1] == (
        "Odcinek lub film z presetu 7 nie ma adresu do odtworzenia"
    )


def test_podcast_direct_assignment_surfaces_and_languages():
    menu_actions = {
        item.action: item
        for menu in menu_model.menus_for_session(SessionId.PODCASTS)
        for item in menu.items if item.action is not None
    }
    palette_actions = {
        item.action: item
        for item in entries_for_session(SessionId.PODCASTS)
    }
    help_entries = dict(describe("Podcasty i YouTube"))
    for slot, action in ASSIGN_PRESET_ACTIONS.items():
        chord = f"Ctrl+Alt+Shift+{slot % 10}"
        assert resolve(
            chord_from_canonical(chord),
            player_view=False, radio_session=False,
        ) is action
        assert menu_actions[action].shortcut == chord
        assert palette_actions[action].shortcut == chord
        assert chord in help_entries
    for slot, action in PRESET_ACTIONS.items():
        if slot > 10:
            continue
        chord = f"Ctrl+Shift+{slot % 10}"
        assert resolve(chord_from_canonical(chord), player_view=False, radio_session=False) is action
        assert menu_actions[action].shortcut == chord
        assert palette_actions[action].shortcut == chord
        assert chord in help_entries
    old = i18n.active_language()
    try:
        i18n.configure("pl")
        assert "kanał YouTube" in i18n.tr("presets.assign.types.podcasts")
        i18n.configure("en")
        assert "YouTube channel" in i18n.tr("presets.assign.types.podcasts")
        assert "episode or video" in i18n.tr(
            "presets.activate.podcastMissing", slot=7
        )
    finally:
        i18n.configure(old)


def test_preset_target_does_not_cross_into_other_sessions():
    channel, _, playlist, episode = _rows()
    for session in (SessionId.FILES, SessionId.RADIO, SessionId.TIDAL):
        assert target_from_row(session, channel) is None
        assert target_from_row(session, playlist) is None
        assert target_from_row(session, episode) is None


def test_activation_visibility_and_context_help_for_podcast_preset():
    from amc_wx_lite.release_profile import PUBLIC_PROFILE
    from amc_wx_lite.shortcuts import keyboard_help_announcement

    assert "appleMusic" not in PUBLIC_PROFILE.session_ids
    assert "heos" not in PUBLIC_PROFILE.session_ids
    for session in SessionId:
        menu_actions = {
            item.action
            for menu in menu_model.menus_for_session(session)
            for item in menu.items if item.action is not None
        }
        if session is SessionId.APPLE_MUSIC:
            assert Action.PRESET_1 not in menu_actions
        else:
            assert Action.PRESET_1 in menu_actions

    activation = keyboard_help_announcement(
        chord_from_canonical("Ctrl+Shift+1"),
        Action.PRESET_1, "Podcasty i YouTube",
    )
    assignment = keyboard_help_announcement(
        chord_from_canonical("Ctrl+Alt+Shift+1"),
        Action.ASSIGN_PRESET_1, "Podcasty i YouTube",
    )
    assert "Ctrl+Shift+1" in activation and "Uruchom preset" in activation
    assert "Ctrl+Alt+Shift+1" in assignment and "Przypisz" in assignment

@pytest.mark.parametrize("slot,chord", [
    (11, "Ctrl+Shift+-"), (12, "Ctrl+Shift+="),
])
def test_extra_preset_activation_keys_match_menu_palette_and_help(slot, chord):
    action = PRESET_ACTIONS[slot]
    assert resolve(chord_from_canonical(chord), player_view=False,
                   radio_session=False) is action
    menu_actions = {
        item.action: item
        for menu in menu_model.menus_for_session(SessionId.PODCASTS)
        for item in menu.items if item.action is not None
    }
    palette_actions = {
        item.action: item
        for item in entries_for_session(SessionId.PODCASTS)
    }
    assert menu_actions[action].shortcut == chord
    assert palette_actions[action].shortcut == chord
    assert chord in dict(describe("Podcasty i YouTube"))
