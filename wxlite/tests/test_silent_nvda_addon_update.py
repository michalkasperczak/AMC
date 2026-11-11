"""Silent AMC installer updates only its own NVDA add-on."""

from __future__ import annotations

import importlib.util
from pathlib import Path
import zipfile

import pytest


HELPER = Path(__file__).resolve().parents[2] / "installer" / "update_amc_nvda_addon.py"
SPEC = importlib.util.spec_from_file_location("update_amc_nvda_addon", HELPER)
assert SPEC and SPEC.loader
addon_update = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(addon_update)


def _fixture(tmp_path: Path, version: str = "0.4.2") -> tuple[Path, Path]:
    addons = tmp_path / "nvda" / "addons"
    installed = addons / "amcController"
    installed.mkdir(parents=True)
    (installed / "manifest.ini").write_text("name = amcController\nversion = 0.4.1\n")
    (installed / "old.txt").write_text("keep for rollback")
    bundle = tmp_path / "AMC-NVDA.nvda-addon"
    with zipfile.ZipFile(bundle, "w") as archive:
        archive.writestr("manifest.ini", f"name = amcController\nversion = {version}\n")
        archive.writestr("globalPlugins/amcController/__init__.py", "# current")
    return bundle, addons


def test_silent_update_replaces_addon_and_keeps_recovery_copy(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch
) -> None:
    bundle, addons = _fixture(tmp_path)
    launches: list[tuple[str, str]] = []
    monkeypatch.setattr(addon_update, "_amc_running", lambda: False)
    monkeypatch.setattr(addon_update, "_nvda_running", lambda: True)
    monkeypatch.setattr(addon_update, "_nvda_executable", lambda: Path("C:/NVDA/nvda.exe"))
    monkeypatch.setattr(
        addon_update.os, "startfile", lambda path, *, arguments: launches.append((path, arguments)),
        raising=False,
    )

    message = addon_update.install(bundle, "0.4.2", addons)

    assert "Uruchomiono ponownie NVDA" in message
    assert (addons / "amcController" / "manifest.ini").read_text().endswith("0.4.2\n")
    assert launches == [("C:\\NVDA\\nvda.exe", "-r")] or launches == [("C:/NVDA/nvda.exe", "-r")]
    backups = list(addons.parent.glob("amcController-backup-*"))
    assert len(backups) == 1
    assert (backups[0] / "old.txt").read_text() == "keep for rollback"


def test_silent_update_skips_current_addon(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch
) -> None:
    bundle, addons = _fixture(tmp_path, "0.4.1")
    monkeypatch.setattr(addon_update, "_amc_running", lambda: False)
    monkeypatch.setattr(addon_update, "_nvda_running", lambda: False)
    assert "już aktualną" in addon_update.install(bundle, "0.4.1", addons)
    assert (addons / "amcController" / "old.txt").is_file()
    assert not list(addons.parent.glob("amcController-backup-*"))


def test_silent_update_installs_missing_addon_without_starting_nvda(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch
) -> None:
    bundle, addons = _fixture(tmp_path)
    installed = addons / "amcController"
    for file in installed.iterdir():
        file.unlink()
    installed.rmdir()
    monkeypatch.setattr(addon_update, "_amc_running", lambda: False)
    monkeypatch.setattr(addon_update, "_nvda_running", lambda: False)
    monkeypatch.setattr(
        addon_update.os,
        "startfile",
        lambda *args, **kwargs: pytest.fail("NVDA must not start unexpectedly"),
        raising=False,
    )

    message = addon_update.install(bundle, "0.4.2", addons)

    assert "przy następnym starcie" in message
    assert (installed / "manifest.ini").is_file()
    assert not list(addons.parent.glob("amcController-backup-*"))


def test_silent_update_rolls_back_if_nvda_cannot_restart(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch
) -> None:
    bundle, addons = _fixture(tmp_path)
    monkeypatch.setattr(addon_update, "_amc_running", lambda: False)
    monkeypatch.setattr(addon_update, "_nvda_running", lambda: True)
    monkeypatch.setattr(addon_update, "_nvda_executable", lambda: Path("C:/NVDA/nvda.exe"))

    def fail(*args: object, **kwargs: object) -> None:
        raise OSError("NVDA cannot start")

    monkeypatch.setattr(addon_update.os, "startfile", fail, raising=False)
    with pytest.raises(OSError, match="NVDA cannot start"):
        addon_update.install(bundle, "0.4.2", addons)
    assert (addons / "amcController" / "old.txt").read_text() == "keep for rollback"


def test_silent_update_does_not_interrupt_running_amc(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch
) -> None:
    bundle, addons = _fixture(tmp_path)
    monkeypatch.setattr(addon_update, "_amc_running", lambda: True)
    with pytest.raises(RuntimeError, match="AMC nadal działa"):
        addon_update.install(bundle, "0.4.2", addons)
    assert (addons / "amcController" / "old.txt").is_file()


def test_silent_update_rejects_archive_path_escape(tmp_path: Path) -> None:
    bundle, addons = _fixture(tmp_path)
    with zipfile.ZipFile(bundle, "a") as archive:
        archive.writestr("../other-addon/manifest.ini", "surprise")
    with zipfile.ZipFile(bundle) as archive:
        with pytest.raises(ValueError, match="Niebezpieczna ścieżka"):
            addon_update._validate_archive(archive, "0.4.2")
    assert (addons / "amcController" / "old.txt").is_file()
