"""Podcast/YouTube session navigation stays separate and accessible."""

from amc_wx_lite.list_model import Row
from amc_wx_lite.navigation import (
    LibraryView,
    Navigator,
    OpenPodcastAggregateView,
    OpenPodcastView,
    PlayMedia,
    SessionId,
    View,
)
from amc_wx_lite.shortcuts import Action, Chord, resolve


def _episode(item_id: str, position: float = 0.0) -> Row:
    return Row(
        item_id=item_id,
        title=f"Odcinek {item_id}",
        kind="episode",
        path=f"https://example.invalid/{item_id}",
        url=f"https://example.invalid/{item_id}",
        position_seconds=position,
    )


def test_ctrl_3_selects_podcast_session_in_both_views() -> None:
    chord = Chord("3", ctrl=True)
    assert resolve(chord, player_view=False, radio_session=False) is Action.SESSION_PODCASTS
    assert resolve(chord, player_view=True, radio_session=False) is Action.SESSION_PODCASTS


def test_enter_opens_subscription_then_plays_episode_at_resume_position() -> None:
    nav = Navigator()
    nav.switch_session(SessionId.PODCASTS)
    nav.apply_podcast_library([
        Row("source-one", "Audycja", "podcast", detail="podcast", show_kind=False)
    ])

    open_effects = nav.activate_selected()
    opened = next(effect for effect in open_effects if isinstance(effect, OpenPodcastView))
    assert opened.subscription_id == "source-one"

    nav.apply_podcast_episodes("source-one", "Audycja", [_episode("episode-one", 75.0)])
    play_effects = nav.activate_selected()
    play = next(effect for effect in play_effects if isinstance(effect, PlayMedia))
    assert play.item_id == "episode-one"
    assert play.position_seconds == 75.0
    assert nav.view is View.PLAYER
    assert nav.session.pending_material_id == "episode-one"


def test_backspace_from_episodes_returns_to_exact_subscription() -> None:
    nav = Navigator()
    nav.switch_session(SessionId.PODCASTS)
    nav.apply_podcast_library([
        Row("one", "Pierwszy", "podcast"),
        Row("two", "Drugi", "podcast"),
    ], preferred_id="two")
    nav.activate_selected()
    nav.apply_podcast_episodes("two", "Drugi", [_episode("e")])

    effects = nav.go_to_parent()
    target = next(effect for effect in effects if isinstance(effect, OpenPodcastView))
    assert target.subscription_id is None
    assert target.preferred_id == "two"


def test_load_more_is_navigation_not_fake_playback() -> None:
    nav = Navigator()
    nav.switch_session(SessionId.PODCASTS)
    nav.apply_podcast_library([Row("source", "Kanał", "podcast")])
    nav.activate_selected()
    nav.apply_podcast_episodes(
        "source",
        "Kanał",
        [_episode("e"), Row("more", "Załaduj więcej odcinków", "loadMore")],
        preferred_id="more",
    )
    effects = nav.activate_selected()
    request = next(effect for effect in effects if isinstance(effect, OpenPodcastView))
    assert request.load_more
    assert not any(isinstance(effect, PlayMedia) for effect in effects)


def test_page_down_uses_the_episode_source_not_the_local_file_queue() -> None:
    nav = Navigator()
    nav.switch_session(SessionId.PODCASTS)
    nav.apply_podcast_episodes("source", "Audycja", [_episode("a"), _episode("b")],
                               preferred_id="a")
    nav.activate_selected()
    effects = nav.step_playback_source(True)
    play = next(effect for effect in effects or [] if isinstance(effect, PlayMedia))
    assert play.item_id == "b"


def test_sessions_remember_independent_podcast_selection() -> None:
    nav = Navigator()
    nav.switch_session(SessionId.PODCASTS)
    nav.apply_podcast_library([Row("one", "Pierwszy", "podcast"), Row("two", "Drugi", "podcast")],
                              preferred_id="two")
    nav.switch_session(SessionId.FILES)
    nav.switch_session(SessionId.PODCASTS)
    assert nav.session.model.selected_id == "two"
    assert nav.session.library_view is LibraryView.PODCAST_LIBRARY


def test_podcast_playback_confirmation_updates_only_its_session() -> None:
    nav = Navigator()
    nav.switch_session(SessionId.PODCASTS)
    nav.apply_podcast_episodes("source", "Audycja", [_episode("e")])
    nav.activate_selected()
    nav.note_playback_started(SessionId.PODCASTS)
    assert nav.sessions[SessionId.PODCASTS].current_material_id == "e"
    assert nav.sessions[SessionId.FILES].current_material_id == ""


def test_top_level_escape_or_backspace_is_quiet() -> None:
    nav = Navigator()
    nav.switch_session(SessionId.PODCASTS)
    nav.apply_podcast_library([])
    effects = nav.go_to_parent()
    assert effects == []


def test_aggregate_episode_plays_and_load_more_keeps_aggregate_identity() -> None:
    nav = Navigator()
    nav.switch_session(SessionId.PODCASTS)
    nav.apply_podcast_aggregate(
        LibraryView.PODCAST_INBOX,
        "Nowe odcinki i materiały",
        [_episode("new"), Row("more", "Załaduj więcej odcinków", "loadMore")],
        preferred_id="more",
    )

    effects = nav.activate_selected()
    request = next(
        effect for effect in effects if isinstance(effect, OpenPodcastAggregateView)
    )
    assert request.view is LibraryView.PODCAST_INBOX and request.load_more
    assert nav.go_to_parent() == []

    nav.session.model.select_id("new")
    play = next(
        effect for effect in nav.activate_selected() if isinstance(effect, PlayMedia)
    )
    assert play.item_id == "new"


def test_empty_aggregate_messages_are_intentional_user_facing_labels() -> None:
    nav = Navigator()
    inbox = nav.apply_podcast_aggregate(
        LibraryView.PODCAST_INBOX, "Nowe odcinki i materiały", []
    )
    progress = nav.apply_podcast_aggregate(
        LibraryView.PODCAST_IN_PROGRESS, "W trakcie słuchania", []
    )
    downloads = nav.apply_podcast_aggregate(
        LibraryView.PODCAST_DOWNLOADS, "Pobrane", []
    )
    assert inbox[0].text == "Nowe odcinki i materiały, brak nowych materiałów"
    assert progress[0].text == "W trakcie słuchania, brak rozpoczętych odcinków"
    assert downloads[0].text == "Pobrane, brak pobranych odcinków"
