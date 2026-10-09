"""Odbior kierowania i powrotow Radia przez istniejacy Navigator."""
from amc_wx_lite.navigation import LibraryView, Navigator, OpenLibraryView, SessionId


def test_radio_favorites_request_targets_radio_not_local_files():
    nav = Navigator()
    nav.switch_session(SessionId.RADIO)
    request = nav.open_library_view(LibraryView.FAVORITES)[0]
    assert isinstance(request, OpenLibraryView)
    assert getattr(request, "target_session_id", SessionId.FILES) is SessionId.RADIO, (
        "Ulubione Radia nadal kieruja odczyt do lokalnych plikow"
    )


def test_radio_backspace_returns_to_radio_library_not_local_folder():
    nav = Navigator()
    nav.switch_session(SessionId.RADIO)
    nav.session.library_view = LibraryView.FAVORITES
    request = nav.go_to_parent()[0]
    assert isinstance(request, OpenLibraryView), "Backspace Radia otwiera lokalny folder"
    assert request.target_session_id is SessionId.RADIO
    assert request.view is None


def test_radio_ctrl_l_targets_radio_without_reading_local_preferences():
    nav = Navigator()
    nav.switch_session(SessionId.RADIO)
    request = nav.return_to_library("Foldery")[0]
    assert isinstance(request, OpenLibraryView), "Ctrl+L Radia nadal wraca do lokalnych Folderow"
    assert request.target_session_id is SessionId.RADIO
    assert request.view is None


def test_radio_view_return_keeps_each_selection_and_local_state():
    from amc_wx_lite.list_model import Row
    from amc_wx_lite.navigation import PlayStation, View, view_context
    nav = Navigator()
    nav.switch_session(SessionId.RADIO)
    library = [Row(item_id=x, title=x, kind="station", url="https://example.invalid/"+x)
               for x in ("a", "b")]
    favorite = [Row(item_id="outside", title="Outside", kind="station", url="https://example.invalid/outside")]
    nav.apply_stations(library, preferred_id="b")
    original_context = view_context(nav.session)
    local_before = nav.sessions[SessionId.FILES].model.rows[:]
    nav.apply_radio_view(LibraryView.FAVORITES, "Ulubione radia", favorite)
    assert view_context(nav.session) != original_context
    assert nav.sessions[SessionId.FILES].model.rows == local_before
    commands = nav.activate_selected()
    played = next(x for x in commands if isinstance(x, PlayStation))
    assert played.url == favorite[0].url and played.item_id == "outside"
    nav.back_to_list()
    assert nav.session.view is View.LIST and nav.session.model.selected_id == "outside"
    nav.apply_radio_view(None, "Biblioteka radia", library)
    assert nav.session.model.selected_id == "b", "Powrot zgubil wiersz Biblioteki radia"
    assert view_context(nav.session) == original_context


def test_switching_sessions_keeps_radio_favorites_and_selected_station():
    """Dokladna regresja: Radio/Ulubione -> Pliki -> Radio.

    Samo przelaczenie sesji nie moze ponownie otwierac Biblioteki radia ani
    wybierac pierwszej stacji. Widok, lista i stabilne Id sa stanem sesji.
    """
    from amc_wx_lite.list_model import Row

    nav = Navigator()
    nav.switch_session(SessionId.RADIO)
    favorites = [
        Row(item_id="f1", title="Pierwsza", kind="station", url="https://example.invalid/1"),
        Row(item_id="f2", title="Druga", kind="station", url="https://example.invalid/2"),
    ]
    nav.apply_radio_view(LibraryView.FAVORITES, "Ulubione radia", favorites)
    nav.session.model.select_id("f2")

    nav.switch_session(SessionId.FILES)
    nav.switch_session(SessionId.RADIO)

    assert nav.session.library_view is LibraryView.FAVORITES
    assert [row.item_id for row in nav.session.model.rows] == ["f1", "f2"]
    assert nav.session.model.selected_id == "f2"


def test_radio_views_resolve_from_player_as_well_as_list():
    from amc_wx_lite.shortcuts import Chord, Action, resolve
    for key, action in [("L", Action.VIEW_LIBRARY), ("U", Action.VIEW_FAVORITES), ("H", Action.VIEW_HISTORY)]:
        for player in (False, True):
            assert resolve(Chord(key, ctrl=True), player_view=player, radio_session=True) is action


def test_radio_library_escape_or_backspace_is_quiet():
    nav = Navigator()
    nav.switch_session(SessionId.RADIO)
    commands = nav.go_to_parent()
    assert commands == []
