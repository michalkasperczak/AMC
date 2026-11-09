import json
from datetime import datetime

from amc_wx_lite.version_info import (
    is_main_window_title,
    read_build_label,
    window_title,
    window_title_with_playback,
)


def test_window_title_identifies_the_exact_bundle(tmp_path) -> None:
    revision = "591ea346a3c455ca3d71397e6f3f39240369280a"
    label = f"2026.10.14-{revision}-a9a96007"
    created = "2026-10-14T16:39:56+00:00"
    display_version = "14.10.2026, 18:39"
    (tmp_path / "bundle-manifest.json").write_text(
        json.dumps({
            "buildLabel": label,
            "created": created,
            "displayVersion": display_version,
        }),
        encoding="utf-8",
    )

    assert window_title(tmp_path) == "AMC Test — 14.10.2026, 18:39"


def test_legacy_manifest_uses_local_created_time(tmp_path) -> None:
    created = "2026-10-14T16:39:56+00:00"
    (tmp_path / "bundle-manifest.json").write_text(
        json.dumps({"created": created}),
        encoding="utf-8",
    )

    local_time = datetime.fromisoformat(created).astimezone()
    assert window_title(tmp_path) == f"AMC Test — {local_time:%d.%m.%Y, %H:%M}"


def test_legacy_short_build_label_remains_readable(tmp_path) -> None:
    label = "2026.10.13-7c246dba-a1b2c3d4"
    (tmp_path / "bundle-manifest.json").write_text(
        json.dumps({"buildLabel": label}),
        encoding="utf-8",
    )

    assert read_build_label(tmp_path) == label


def test_invalid_manifest_never_leaks_technical_content(tmp_path) -> None:
    (tmp_path / "bundle-manifest.json").write_text(
        '{"buildLabel":"RuntimeObject { Id = tajne }",'
        '"displayVersion":"RuntimeObject { Id = tajne }"}',
        encoding="utf-8",
    )

    assert read_build_label(tmp_path) == ""
    assert window_title(tmp_path) == "AMC Test — wersja 0.1.0"


def test_window_title_can_place_current_track_before_or_after_build_date() -> None:
    base = "AMC Test — 09.11.2026, 18:06"
    assert window_title_with_playback(base, "Artysta - Utwór", "after") == (
        "AMC Test — 09.11.2026, 18:06 — Artysta - Utwór"
    )
    before = window_title_with_playback(base, "Artysta - Utwór", "before")
    assert before == "Artysta - Utwór — AMC Test — 09.11.2026, 18:06"
    assert is_main_window_title(before)
    assert window_title_with_playback(base, "Artysta - Utwór", "off") == base
    assert window_title_with_playback(base, "", "before") == base


def test_main_window_matcher_excludes_diagnostic_alert() -> None:
    assert is_main_window_title("AMC Test")
    assert is_main_window_title("AMC Test — 14.10.2026, 18:39")
    assert is_main_window_title("AMC Python")
    assert is_main_window_title("AMC Python — 14.10.2026, 18:39")
    assert not is_main_window_title("Diagnostyka AMC Python")
    assert not is_main_window_title("Diagnostyka AMC Test")
