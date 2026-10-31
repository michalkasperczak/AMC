from __future__ import annotations

import hashlib
import json
from pathlib import Path
from types import SimpleNamespace

import pytest

from amc_wx_lite import application_update, gui
from amc_wx_lite.application_update import (
    ApplicationUpdateIntegrityError,
    PreparedApplicationUpdate,
    check_for_application_update,
    launch_prepared_application_update,
    prepare_application_update,
)
from amc_wx_lite.command_palette import entries_for_session
from amc_wx_lite.menu_model import menus_for_session
from amc_wx_lite.navigation import SessionId
from amc_wx_lite.release_profile import PROFILES
from amc_wx_lite.shortcuts import (
    Action,
    command_presentation,
    describe,
)


def _installed(root: Path, profile_id: str, display_version: str) -> None:
    profile = PROFILES[profile_id]
    root.mkdir(parents=True, exist_ok=True)
    (root / "bundle-manifest.json").write_text(
        json.dumps(
            {
                "releaseProfileSchema": 1,
                "releaseProfile": profile.profile_id,
                "productId": profile.product_id,
                "displayName": profile.display_name,
                "installId": profile.install_id,
                "updateChannel": profile.update_channel,
                "displayVersion": display_version,
                "sourceRevision": "1" * 40,
                "sourceDirty": False,
            }
        ),
        encoding="utf-8",
    )
    (root / "AMC-Python-Updater.exe").write_bytes(b"updater")


def _release_fixture(
    root: Path,
    *,
    profile_id: str,
    current: str,
    available: str,
) -> tuple[dict[str, bytes], bytes]:
    _installed(root, profile_id, current)
    profile = PROFILES[profile_id]
    revision = "2" * 40
    installer_bytes = b"syntetyczny pelny instalator " + profile_id.encode()
    installer_name = (
        "AMC-Test-2026-10-27-0900.exe"
        if profile_id == "test"
        else "AMC-Public-2026-10-27-0900.exe"
    )
    tag = ("test-" if profile_id == "test" else "public-") + "2026.10.27.0900"
    base = f"https://github.com/{application_update.REPOSITORY}/releases/download/{tag}/"
    manifest_name = f"amc-update-{profile_id}.json"
    manifest = {
        "schemaVersion": 1,
        "repository": application_update.REPOSITORY,
        "tag": tag,
        "version": "0.1.0-alpha.434",
        "displayName": profile.display_name,
        "displayVersion": available,
        "profile": profile.profile_id,
        "productId": profile.product_id,
        "installId": profile.install_id,
        "updateChannel": profile.update_channel,
        "prerelease": profile_id == "test",
        "sourceRevision": revision,
        "summary": "Nowy, dostępny mechanizm aktualizacji programu.",
        "installer": {
            "fileName": installer_name,
            "bytes": len(installer_bytes),
            "sha256": hashlib.sha256(installer_bytes).hexdigest(),
        },
    }
    releases = [
        {
            "tag_name": tag,
            "draft": False,
            "prerelease": profile_id == "test",
            "assets": [
                {
                    "name": manifest_name,
                    "size": len(json.dumps(manifest).encode()),
                    "browser_download_url": base + manifest_name,
                },
                {
                    "name": installer_name,
                    "size": len(installer_bytes),
                    "browser_download_url": base + installer_name,
                },
            ],
        }
    ]
    mapping = {
        application_update.RELEASES_API: json.dumps(releases).encode(),
        base + manifest_name: json.dumps(manifest).encode(),
    }
    return mapping, installer_bytes


def test_command_uses_one_presentation_in_menu_palette_help_and_dispatch() -> None:
    action = Action.CHECK_APPLICATION_UPDATE
    presentation = command_presentation(action)
    assert presentation is not None
    assert presentation.name == "Sprawdź aktualizację AMC"
    assert presentation.shortcut == ""
    for session in SessionId:
        menu_items = {
            item.action: item
            for menu in menus_for_session(session)
            for item in menu.items
            if item.action is not None
        }
        palette = {entry.action: entry for entry in entries_for_session(session)}
        assert action in menu_items
        assert action in palette
        assert palette[action].name == presentation.name
    assert ("Bez skrótu", presentation.description) in describe()
    source = Path(gui.__file__).read_text(encoding="utf-8")
    assert "elif action is Action.CHECK_APPLICATION_UPDATE:" in source
    assert "self._check_application_update()" in source


@pytest.mark.parametrize("profile_id", ["test", "public"])
def test_profile_selects_only_its_fixed_update_channel(
    tmp_path: Path, profile_id: str
) -> None:
    mapping, _package = _release_fixture(
        tmp_path / profile_id,
        profile_id=profile_id,
        current="26.10.2026, 14:01",
        available="27.10.2026, 09:00",
    )

    result = check_for_application_update(
        root=tmp_path / profile_id,
        fetch_bytes=lambda url, _maximum: mapping[url],
    )

    assert result.available
    assert result.release is not None
    assert result.release.profile_id == profile_id
    assert result.release.channel == PROFILES[profile_id].update_channel
    assert "kanał" not in result.information_text.casefold()


def test_older_release_never_downgrades_a_newer_local_build(tmp_path: Path) -> None:
    mapping, _package = _release_fixture(
        tmp_path,
        profile_id="test",
        current="28.10.2026, 09:00",
        available="27.10.2026, 09:00",
    )
    result = check_for_application_update(
        root=tmp_path,
        fetch_bytes=lambda url, _maximum: mapping[url],
    )
    assert not result.available


def test_foreign_profile_manifest_is_rejected_before_download(tmp_path: Path) -> None:
    mapping, _package = _release_fixture(
        tmp_path,
        profile_id="test",
        current="26.10.2026, 14:01",
        available="27.10.2026, 09:00",
    )
    manifest_url = next(url for url in mapping if url.endswith("amc-update-test.json"))
    manifest = json.loads(mapping[manifest_url])
    manifest["updateChannel"] = "stable"
    mapping[manifest_url] = json.dumps(manifest).encode()

    with pytest.raises(ApplicationUpdateIntegrityError):
        check_for_application_update(
            root=tmp_path,
            fetch_bytes=lambda url, _maximum: mapping[url],
        )


def test_verified_full_installer_is_prepared_for_existing_updater(tmp_path: Path) -> None:
    root = tmp_path / "installed"
    mapping, installer_bytes = _release_fixture(
        root,
        profile_id="test",
        current="26.10.2026, 14:01",
        available="27.10.2026, 09:00",
    )
    check = check_for_application_update(
        root=root,
        fetch_bytes=lambda url, _maximum: mapping[url],
    )

    def download(_url: str, destination: Path, _maximum: int) -> tuple[int, str]:
        destination.write_bytes(installer_bytes)
        return len(installer_bytes), hashlib.sha256(installer_bytes).hexdigest()

    prepared = prepare_application_update(
        check,
        root=root,
        cache_root=tmp_path / "cache",
        download_file=download,
    )
    assert prepared.target_root == root.resolve()
    assert prepared.installer_path.is_file()
    assert prepared.installer_path.read_bytes() == installer_bytes
    assert prepared.installer_path.suffix.lower() == ".exe"


def test_wrong_installer_hash_is_rejected_and_staging_is_removed(tmp_path: Path) -> None:
    root = tmp_path / "installed"
    mapping, _installer = _release_fixture(
        root,
        profile_id="test",
        current="26.10.2026, 14:01",
        available="27.10.2026, 09:00",
    )
    check = check_for_application_update(
        root=root,
        fetch_bytes=lambda url, _maximum: mapping[url],
    )
    bad = b"inny instalator"
    assert check.release is not None

    def download(_url: str, destination: Path, _maximum: int) -> tuple[int, str]:
        destination.write_bytes(bad)
        return len(bad), hashlib.sha256(bad).hexdigest()

    cache = tmp_path / "cache"
    with pytest.raises(ApplicationUpdateIntegrityError):
        prepare_application_update(
            check,
            root=root,
            cache_root=cache,
            download_file=download,
        )
    assert list(cache.iterdir()) == []


def test_launch_uses_verified_updater_and_requests_installer_cleanup(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch
) -> None:
    root = tmp_path / "AMC Test"
    root.mkdir()
    updater = root / "AMC-Python-Updater.exe"
    updater.write_bytes(b"updater")
    installer = tmp_path / "AMC-Test-2026-10-27-0900.exe"
    installer.write_bytes(b"installer")
    release = application_update.ApplicationUpdateRelease(
        tag="test-2026.10.27.0900",
        profile_id="test",
        channel="test",
        display_name="AMC Test",
        display_version="27.10.2026, 09:00",
        source_revision="2" * 40,
        summary="Zmiany",
        installer=application_update.ReleaseAsset(
            installer.name,
            installer.stat().st_size,
            "https://example.invalid",
        ),
        installer_sha256="3" * 64,
    )
    prepared = PreparedApplicationUpdate(release, installer, root, updater)
    captured: dict[str, object] = {}

    def fake_popen(arguments, **kwargs):
        captured["arguments"] = arguments
        captured["kwargs"] = kwargs
        return object()

    monkeypatch.setattr(application_update.subprocess, "Popen", fake_popen)
    launch_prepared_application_update(prepared)

    arguments = captured["arguments"]
    assert arguments[0] == str(updater)
    assert "--cleanup-installer" in arguments
    assert arguments[arguments.index("--installer") + 1] == str(installer)
    assert arguments[arguments.index("--installer-sha256") + 1] == "3" * 64
    assert arguments[arguments.index("--target") + 1] == str(root)


def test_dialog_contract_names_controls_and_focuses_release_notes() -> None:
    source = (
        Path(application_update.__file__).with_name("application_update_dialog.py")
    ).read_text(encoding="utf-8")
    assert 'name="Informacje o aktualizacji AMC"' in source
    assert 'SetName("Aktualizuj AMC")' in source
    assert 'SetName("Zamknij okno aktualizacji")' in source
    assert "wx.TE_READONLY" in source
    assert "self.information.SetFocus()" in source
    assert "self.information.SetInsertionPoint(0)" in source
    assert "wx.ID_CANCEL" in source


def test_user_facing_failure_never_exposes_exception_path() -> None:
    technical = ApplicationUpdateIntegrityError(
        r"C:\Users\tester\token\pakiet.zip ma złą sumę"
    )
    message = application_update.user_facing_update_error(technical)
    assert "C:\\Users" not in message
    assert "token" not in message
    assert "Nic nie zostało zainstalowane" in message


def _automatic_frame() -> tuple[SimpleNamespace, list[str], list[object]]:
    announcements: list[str] = []
    dialogs: list[object] = []

    class Runner:
        @staticmethod
        def submit(_name, work, done, failed):
            try:
                done(work())
            except Exception as error:  # pragma: no cover - used by failure test
                failed(error)

    frame = SimpleNamespace(
        _application_update_pending=False,
        announcer=SimpleNamespace(say=announcements.append),
        runner=Runner(),
        store=SimpleNamespace(directory=Path("nieuzywany")),
        _restore_focus_after_dialog=lambda: None,
        _download_application_update=lambda _check: None,
    )
    return frame, announcements, dialogs


def test_automatic_check_stays_silent_when_installed_version_is_current(
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    check = application_update.ApplicationUpdateCheck(
        PROFILES["test"], "29.10.2026, 19:05", False, None
    )
    frame, announcements, dialogs = _automatic_frame()
    monkeypatch.setattr(gui, "check_for_application_update", lambda: check)
    monkeypatch.setattr(
        gui,
        "ApplicationUpdateDialog",
        lambda *_args: dialogs.append(object()),
    )

    gui.LiteFrame._check_application_update(frame, automatic=True)

    assert announcements == []
    assert dialogs == []
    assert frame._application_update_pending is False


def test_automatic_check_opens_the_same_accessible_dialog_for_a_new_version(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch
) -> None:
    mapping, _installer = _release_fixture(
        tmp_path,
        profile_id="test",
        current="29.10.2026, 19:05",
        available="30.10.2026, 08:00",
    )
    check = check_for_application_update(
        root=tmp_path,
        fetch_bytes=lambda url, _maximum: mapping[url],
    )
    frame, announcements, dialogs = _automatic_frame()

    class Dialog:
        def __init__(self, _parent, received):
            dialogs.append(received)

        @staticmethod
        def ShowModal():
            return gui.wx.ID_CANCEL

        @staticmethod
        def Destroy():
            return None

    monkeypatch.setattr(gui, "check_for_application_update", lambda: check)
    monkeypatch.setattr(gui, "ApplicationUpdateDialog", Dialog)

    gui.LiteFrame._check_application_update(frame, automatic=True)

    assert announcements == []
    assert dialogs == [check]
    assert check.available


def test_automatic_check_logs_network_failure_without_interrupting_speech(
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    frame, announcements, _dialogs = _automatic_frame()
    logs: list[str] = []

    def fail():
        raise application_update.ApplicationUpdateNetworkError("brak sieci")

    monkeypatch.setattr(gui, "check_for_application_update", fail)
    monkeypatch.setattr(
        gui,
        "_append_frontend_log",
        lambda _store, message: logs.append(message),
    )

    gui.LiteFrame._check_application_update(frame, automatic=True)

    assert announcements == []
    assert len(logs) == 1
    assert "Sprawdzanie aktualizacji AMC nie powiodło się" in logs[0]


def test_automatic_check_is_scheduled_once_only_when_enabled(
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    scheduled: list[tuple[int, object]] = []
    calls: list[bool] = []
    monkeypatch.setattr(
        gui.wx,
        "CallLater",
        lambda milliseconds, callback: scheduled.append((milliseconds, callback)),
        raising=False,
    )
    monkeypatch.setattr(gui.wx, "GetTopLevelWindows", lambda: [], raising=False)
    frame = SimpleNamespace(
        _automatic_application_update_check_started=False,
        _automatic_application_update_check_call=None,
        options=SimpleNamespace(application_check_automatically=True),
        _window_alive=lambda: True,
        _check_application_update=lambda *, automatic: calls.append(automatic),
    )

    gui.LiteFrame._schedule_automatic_application_update_check(frame)
    gui.LiteFrame._schedule_automatic_application_update_check(frame)
    assert len(scheduled) == 1
    assert scheduled[0][0] == 5000
    scheduled[0][1]()
    assert calls == [True]

    disabled = SimpleNamespace(
        _automatic_application_update_check_started=False,
        _automatic_application_update_check_call=None,
        options=SimpleNamespace(application_check_automatically=False),
    )
    gui.LiteFrame._schedule_automatic_application_update_check(disabled)
    assert len(scheduled) == 1


def test_automatic_check_waits_until_another_modal_is_closed(
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    scheduled: list[object] = []
    calls: list[bool] = []

    class Modal:
        @staticmethod
        def IsShown():
            return True

        @staticmethod
        def IsModal():
            return True

    modal = Modal()
    monkeypatch.setattr(
        gui.wx,
        "CallLater",
        lambda _milliseconds, callback: scheduled.append(callback),
        raising=False,
    )
    monkeypatch.setattr(gui.wx, "GetTopLevelWindows", lambda: [modal], raising=False)
    frame = SimpleNamespace(
        _automatic_application_update_check_started=False,
        _automatic_application_update_check_call=None,
        options=SimpleNamespace(application_check_automatically=True),
        _window_alive=lambda: True,
        _check_application_update=lambda *, automatic: calls.append(automatic),
    )

    gui.LiteFrame._schedule_automatic_application_update_check(frame)
    scheduled.pop(0)()
    assert calls == []
    assert len(scheduled) == 1

    monkeypatch.setattr(gui.wx, "GetTopLevelWindows", lambda: [], raising=False)
    scheduled.pop(0)()
    assert calls == [True]
