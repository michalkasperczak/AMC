"""AMC12: profile wydania sa jawne, odseparowane i steruja mozliwosciami."""

from __future__ import annotations

import os
import importlib
import re
import tempfile
from pathlib import Path

from amc_wx_lite import menu_model
from amc_wx_lite.command_palette import entries_for_session
from amc_wx_lite.navigation import SessionId
from amc_wx_lite.profile_layout import (
    common_data_snapshot_path,
    default_amc_local_dir,
    default_amc_profile_dir,
)
from amc_wx_lite.release_profile import (
    PUBLIC_PROFILE,
    TEST_PROFILE,
    current_profile,
    profile_directory,
    profile_from_id,
)
from amc_wx_lite.session_order import (
    SESSION_ACTIONS,
    available_sessions,
    enable,
    manager_labels,
    normalize,
)
from amc_wx_lite.shortcuts import Action, Chord, describe, resolve
from amc_wx_lite.state_store import default_state_dir


REPOSITORY_ROOT = Path(__file__).resolve().parents[2]
SHARED_PROFILE_CONTRACT = REPOSITORY_ROOT / "src" / "Shared" / "ReleaseProfileContract.cs"


def _with_release_profile(profile_id: str):
    """Maly kontekst bez zaleznosci od pytest i bez wycieku do innych testow."""

    class ProfileEnvironment:
        def __enter__(self):
            self.previous = os.environ.get("AMC_RELEASE_PROFILE")
            os.environ["AMC_RELEASE_PROFILE"] = profile_id
            return self

        def __exit__(self, _type, _value, _traceback):
            if self.previous is None:
                os.environ.pop("AMC_RELEASE_PROFILE", None)
            else:
                os.environ["AMC_RELEASE_PROFILE"] = self.previous

    return ProfileEnvironment()


def test_profiles_have_distinct_identity_storage_channel_and_credentials() -> None:
    assert TEST_PROFILE.display_name == "AMC Test"
    assert PUBLIC_PROFILE.display_name == "AMC Public"
    assert TEST_PROFILE.product_id != PUBLIC_PROFILE.product_id
    assert TEST_PROFILE.install_id != PUBLIC_PROFILE.install_id
    assert TEST_PROFILE.update_channel != PUBLIC_PROFILE.update_channel
    assert TEST_PROFILE.ui_state_folder != PUBLIC_PROFILE.ui_state_folder
    assert TEST_PROFILE.roaming_profile_folder != PUBLIC_PROFILE.roaming_profile_folder
    assert TEST_PROFILE.local_profile_folder != PUBLIC_PROFILE.local_profile_folder
    assert TEST_PROFILE.cache_folder != PUBLIC_PROFILE.cache_folder
    assert TEST_PROFILE.log_folder != PUBLIC_PROFILE.log_folder
    assert TEST_PROFILE.credential_namespace != PUBLIC_PROFILE.credential_namespace


def test_public_manifest_exposes_only_approved_sessions_and_test_has_all() -> None:
    assert PUBLIC_PROFILE.session_ids == (
        "files", "radio", "podcasts", "wiim", "frontierSmart"
    )
    assert set(TEST_PROFILE.session_ids) == {session.value for session in SessionId}
    # Radio Archiwista jest dostępny wyłącznie w AMC Test do czasu
    # ustabilizowania sesji; do Public trafi świadomą decyzją.
    assert PUBLIC_PROFILE.session_ids == tuple(
        session for session in TEST_PROFILE.session_ids
        if session not in {"tidal", "spotify", "sonos", "radioarchiwista"}
    )
    public_text = " ".join(PUBLIC_PROFILE.capabilities).casefold()
    assert "spotify" not in public_text
    assert "tidal" not in public_text
    assert "youtube.account" not in public_text
    assert "sonos" not in public_text
    assert "frontier-smart" in public_text
    assert PUBLIC_PROFILE.capabilities == tuple(
        capability for capability in TEST_PROFILE.capabilities
        if "spotify" not in capability.casefold()
        and "tidal" not in capability.casefold()
        and "sonos" not in capability.casefold()
        and "youtube.account" not in capability.casefold()
        and "radioarchiwista" not in capability.casefold()
    )


def test_public_profile_keeps_presets_and_radio_timeshift_navigation() -> None:
    """Wycięcie usług prywatnych nie może wyciąć wspólnego sterowania."""

    assert "radio" in PUBLIC_PROFILE.session_ids
    assert "recording" in PUBLIC_PROFILE.capabilities
    assert (
        resolve(Chord("Ctrl+Shift+0"), player_view=False, radio_session=False)
        is Action.PRESET_10
    )
    assert resolve(Chord("End"), player_view=True, radio_session=True) is Action.RADIO_JUMP_LIVE
    assert resolve(Chord("5"), player_view=True, radio_session=True) is Action.SEEK_PERCENT_50


def test_python_capabilities_match_closed_launcher_contract() -> None:
    """Pakiet nie może przejść testów, a potem zostać odrzucony przez launcher."""

    source = SHARED_PROFILE_CONTRACT.read_text(encoding="utf-8")
    common_match = re.search(
        r"CommonCapabilities\s*=\s*\[(.*?)\];",
        source,
        flags=re.DOTALL,
    )
    test_only_match = re.search(
        r"CommonCapabilities\.Concat\(\s*\[(.*?)\]\)\.ToArray\(\)",
        source,
        flags=re.DOTALL,
    )
    assert common_match is not None
    assert test_only_match is not None
    public_contract = source.split(
        "internal static readonly ReleaseProfileDescriptor Public", 1
    )[1].split("internal static ReleaseProfileDescriptor Validate", 1)[0]
    assert "PublicCapabilities," in public_contract
    assert 'capability != "session.sonos"' in source
    assert '.Append("session.frontier-smart")' in source
    assert "youtube.account.readonly" not in public_contract
    quoted = re.compile(r'"([^"]+)"')
    common = tuple(quoted.findall(common_match.group(1)))
    test_only = tuple(quoted.findall(test_only_match.group(1)))
    assert tuple(
        capability for capability in common if capability != "session.sonos"
    ) + ("session.frontier-smart",) == PUBLIC_PROFILE.capabilities
    assert common + test_only == TEST_PROFILE.capabilities


def test_profile_selection_prefers_environment_then_manifest_and_rejects_unknown() -> None:
    with tempfile.TemporaryDirectory(prefix="amc-profile-manifest-") as folder:
        root = Path(folder)
        (root / "bundle-manifest.json").write_text(
            '{"releaseProfileSchema":1,"releaseProfile":"public"}',
            encoding="utf-8",
        )
        assert current_profile({}, root) is PUBLIC_PROFILE
        assert current_profile({"AMC_RELEASE_PROFILE": "test"}, root) is TEST_PROFILE
    try:
        profile_from_id("nieznany")
    except ValueError:
        pass
    else:
        raise AssertionError("Nieznany profil nie moze uruchomic aplikacji")


def test_profile_folders_cannot_escape_system_base() -> None:
    base = Path("C:/Users/test/AppData/Roaming")
    assert profile_directory(base, "AMC/ui") == base / "AMC" / "ui"
    for unsafe in ("../AMC", "/AMC", "AMC/../../inne"):
        try:
            profile_directory(base, unsafe)
        except ValueError:
            pass
        else:
            raise AssertionError(f"Niebezpieczna sciezka zostala przyjeta: {unsafe}")


def test_public_runtime_directories_are_separate_from_test_directories() -> None:
    previous_appdata = os.environ.get("APPDATA")
    previous_local = os.environ.get("LOCALAPPDATA")
    previous_override = os.environ.pop("AMC_WX_LITE_HOME", None)
    os.environ["APPDATA"] = "C:/Users/test/AppData/Roaming"
    os.environ["LOCALAPPDATA"] = "C:/Users/test/AppData/Local"
    try:
        with _with_release_profile("test"):
            test_paths = (
                default_state_dir(),
                default_amc_profile_dir(),
                default_amc_local_dir(),
            )
        with _with_release_profile("public"):
            public_paths = (
                default_state_dir(),
                default_amc_profile_dir(),
                default_amc_local_dir(),
            )
    finally:
        if previous_appdata is None:
            os.environ.pop("APPDATA", None)
        else:
            os.environ["APPDATA"] = previous_appdata
        if previous_local is None:
            os.environ.pop("LOCALAPPDATA", None)
        else:
            os.environ["LOCALAPPDATA"] = previous_local
        if previous_override is not None:
            os.environ["AMC_WX_LITE_HOME"] = previous_override
    assert all(test != public for test, public in zip(test_paths, public_paths))


def test_common_data_snapshot_is_neutral_and_shared_by_both_profiles() -> None:
    previous_local = os.environ.get("LOCALAPPDATA")
    os.environ["LOCALAPPDATA"] = "C:/Users/test/AppData/Local"
    try:
        with _with_release_profile("test"):
            test_path = common_data_snapshot_path()
        with _with_release_profile("public"):
            public_path = common_data_snapshot_path()
    finally:
        if previous_local is None:
            os.environ.pop("LOCALAPPDATA", None)
        else:
            os.environ["LOCALAPPDATA"] = previous_local
    assert test_path == public_path
    assert test_path == Path("C:/Users/test/AppData/Local/AMC/shared/common-data-v1.json")


def test_common_data_commands_follow_profile_in_menu_palette_and_help() -> None:
    previous_local = os.environ.get("LOCALAPPDATA")
    with tempfile.TemporaryDirectory(prefix="amc-common-data-menu-") as folder:
        os.environ["LOCALAPPDATA"] = folder
        snapshot = common_data_snapshot_path()
        snapshot.parent.mkdir(parents=True)
        snapshot.write_text("{}", encoding="utf-8")
        try:
            with _with_release_profile("public"):
                settings = next(
                    menu for menu in menu_model.build_menus() if menu.title == "&Ustawienia"
                )
                public_actions = {item.action for item in settings.items}
                assert Action.REFRESH_COMMON_DATA in public_actions
                assert Action.COMMON_DATA_STATUS in public_actions
                assert Action.EXPORT_BACKUP in public_actions
                assert Action.IMPORT_BACKUP in public_actions
                palette = {
                    entry.action
                    for entry in entries_for_session(SessionId.FILES)
                }
                assert Action.REFRESH_COMMON_DATA in palette
                assert Action.COMMON_DATA_STATUS in palette
                assert Action.EXPORT_BACKUP in palette
                assert Action.IMPORT_BACKUP in palette
                for session in SessionId:
                    session_actions = {
                        entry.action for entry in entries_for_session(session)
                    }
                    assert Action.EXPORT_BACKUP in session_actions
                    assert Action.IMPORT_BACKUP in session_actions

            with _with_release_profile("test"):
                settings = next(
                    menu for menu in menu_model.build_menus() if menu.title == "&Ustawienia"
                )
                test_actions = {item.action for item in settings.items}
                assert Action.REFRESH_COMMON_DATA not in test_actions
                assert Action.COMMON_DATA_STATUS in test_actions
                assert Action.EXPORT_BACKUP in test_actions
                assert Action.IMPORT_BACKUP in test_actions
                for session in SessionId:
                    session_actions = {
                        entry.action for entry in entries_for_session(session)
                    }
                    assert Action.EXPORT_BACKUP in session_actions
                    assert Action.IMPORT_BACKUP in session_actions
                help_entries = set(describe())
                assert (
                    "Bez skrótu",
                    "Eksportuj dane AMC",
                ) in help_entries
                assert (
                    "Bez skrótu",
                    "Importuj dane AMC",
                ) in help_entries
        finally:
            if previous_local is None:
                os.environ.pop("LOCALAPPDATA", None)
            else:
                os.environ["LOCALAPPDATA"] = previous_local


def test_window_identity_uses_user_facing_name_of_selected_profile() -> None:
    from amc_wx_lite import version_info

    try:
        with _with_release_profile("public"):
            public_version = importlib.reload(version_info)
            assert public_version.PRODUCT_NAME == "AMC Public"
            assert public_version.window_title().startswith("AMC Public — ")
            assert public_version.is_main_window_title("AMC Public — wersja 1")
            assert public_version.is_main_window_title("AMC — wersja 1")
            assert not public_version.is_main_window_title("AMC Test — wersja 1")
    finally:
        importlib.reload(version_info)


def test_public_capability_model_filters_execution_manager_menu_and_palette() -> None:
    raw = [session.value for session in SessionId]
    with _with_release_profile("public"):
        assert available_sessions() == (
            SessionId.FILES,
            SessionId.RADIO,
            SessionId.PODCASTS,
            SessionId.WIIM,
            SessionId.FRONTIER_SMART,
        )
        assert normalize(raw) == available_sessions()
        rows = dict(manager_labels(raw))
        assert set(rows) == set(available_sessions())
        unchanged, message = enable(SessionId.SPOTIFY, raw)
        assert unchanged == available_sessions()
        assert message == "Ta sesja nie jest dostępna w tym wydaniu AMC"

        session_actions = set(SESSION_ACTIONS.values())
        view = next(menu for menu in menu_model.build_menus(raw) if menu.title == "&Widok")
        visible_session_actions = {
            item.action for item in view.items if item.action in session_actions
        }
        assert visible_session_actions == {
            Action.SESSION_FILES,
            Action.SESSION_RADIO,
            Action.SESSION_PODCASTS,
            Action.SESSION_WIIM,
            Action.SESSION_FRONTIER_SMART,
        }
        palette_actions = {
            entry.action for entry in entries_for_session(SessionId.FILES, raw)
        }
        assert visible_session_actions <= palette_actions
        assert Action.SESSION_SPOTIFY not in palette_actions
        assert Action.SESSION_TIDAL not in palette_actions
        assert Action.SESSION_SONOS not in palette_actions
        assert Action.SESSION_FRONTIER_SMART in palette_actions
