"""Ctrl+L pamięta wybór w tym oknie, nie stary tryb drugiego AMC."""
from test_ctrl_l_library_return import _frame, _ctrl_l, _rows, FOLDER
from amc_wx_lite.navigation import Navigator, LibraryView, LIBRARY_VIEW_FOLDERS, LIBRARY_VIEW_ALL_FILES
from amc_wx_lite.shortcuts import Action


def test_ctrl_l_in_radio_does_not_open_local_library():
    from amc_wx_lite.navigation import SessionId
    nav = Navigator()
    nav.active = SessionId.RADIO
    frame, folders, views, said = _frame(nav, LIBRARY_VIEW_FOLDERS)
    _ctrl_l(frame)
    # Port Radia ma juz wlasna Biblioteke. Zakaz dotyczy PLIKOW,
    # nie kazdego odczytu OpenLibraryView niezaleznie od sesji.
    assert folders == [], "Ctrl+L Radia nie może otworzyć lokalnego folderu"
    assert len(views) == 1
    assert views[0].target_session_id is SessionId.RADIO
    assert views[0].view is None
    assert nav.active is SessionId.RADIO


def test_alt2_then_favorites_ctrl_l_keeps_all_files_despite_old_shared_profile():
    nav = Navigator()
    nav.apply_folder(FOLDER, _rows("plik", 5))
    frame, folders, views, said = _frame(nav, LIBRARY_VIEW_FOLDERS)
    frame._dispatch(Action.VIEW_ALL_FILES)
    assert views[-1].view is LibraryView.ALL_FILES
    nav.apply_library_view(views[-1].view, "Wszystkie pliki", _rows("wsz", 6))
    nav.session.model.select_id("wsz:5")
    frame._dispatch(Action.VIEW_FAVORITES)
    nav.apply_library_view(views[-1].view, "Ulubione", _rows("ulub", 2))
    folders.clear()
    views.clear()
    _ctrl_l(frame)
    assert folders == [], "Ctrl+L nie może cofać wyboru Alt+2 do trybu starego profilu"
    assert [(v.view, v.preferred_id) for v in views] == [(LibraryView.ALL_FILES, "wsz:5")]
    assert said == []


def test_folder_then_history_ctrl_l_keeps_folder_despite_old_shared_profile():
    nav = Navigator()
    nav.apply_library_view(LibraryView.ALL_FILES, "Wszystkie pliki", _rows("wsz", 6))
    nav.apply_folder(FOLDER, _rows("plik", 5))
    nav.session.model.select_id("plik:4")
    frame, folders, views, said = _frame(nav, LIBRARY_VIEW_ALL_FILES)
    frame._dispatch(Action.VIEW_HISTORY)
    nav.apply_library_view(views[-1].view, "Historia", _rows("hist", 2))
    folders.clear()
    views.clear()
    _ctrl_l(frame)
    assert views == [], "Ostatnio oglądane Foldery mają pierwszeństwo przed starym zapisem"
    assert folders == [(FOLDER, "plik:4")]
    assert said == []
