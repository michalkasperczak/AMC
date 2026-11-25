from amc_wx_lite.automatic_track_announcements import (
    LABELS,
    MODES,
    should_announce,
)


def test_all_modes_have_explicit_spoken_labels() -> None:
    assert len(MODES) == len(LABELS) == 3
    assert all(label and "." not in label for label in LABELS)


def test_automatic_title_policy_preserves_active_session() -> None:
    assert should_announce("always", "tidal", "files")
    assert should_announce("active", "tidal", "tidal")
    assert not should_announce("active", "tidal", "files")
    assert not should_announce("never", "tidal", "tidal")
    assert should_announce("unexpected", "tidal", "files")
