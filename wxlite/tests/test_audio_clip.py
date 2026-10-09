from __future__ import annotations

import math
from pathlib import Path

from amc_wx_lite.audio_clip import (
    AudioClipSelection,
    clip_context_from_status,
    format_choices_from_payload,
    format_clip_time,
    read_clip_selections,
    restore_clip_selection,
    suggested_clip_file_name,
    update_clip_selections,
)
from amc_wx_lite.host_client import LiteHostClient
from amc_wx_lite.shortcuts import Action, Chord, resolve


def test_selection_matches_csharp_order_and_clamping_rules() -> None:
    selection = AudioClipSelection()
    assert selection.set_end("file:a", r"C:\A.mp3", 30.0, 100.0)
    assert not selection.set_start("file:a", r"c:\a.MP3", 30.0, 100.0)
    assert selection.set_start("file:a", r"c:\a.MP3", -5.0, 100.0)
    assert selection.start_seconds == 0.0
    assert selection.set_end("file:a", r"c:\a.MP3", 120.0, 100.0)
    assert selection.end_seconds == 100.0
    assert selection.is_complete

    # Nowy material zeruje granice, dokladnie jak BeginItem w Core.
    assert selection.set_start("file:b", r"C:\B.wav", 10.0, 60.0)
    assert selection.end_seconds is None


def test_relative_boundaries_are_strict_and_directional() -> None:
    selection = AudioClipSelection("id", "a.wav", 10.0, 20.0)
    assert selection.find_relative_boundary(10.0, -1) is None
    assert selection.find_relative_boundary(10.0, 1) == 20.0
    assert selection.find_relative_boundary(15.0, -1) == 10.0
    assert selection.find_relative_boundary(15.0, 1) == 20.0
    assert selection.find_relative_boundary(20.0, 1) is None


def test_time_format_is_the_full_amc_millisecond_format() -> None:
    assert format_clip_time(65.432) == "1:05.432"
    assert format_clip_time(65.4329) == "1:05.432"
    assert format_clip_time(3_661.007) == "1:01:01.007"


def test_context_uses_authoritative_source_and_never_repr(tmp_path: Path) -> None:
    source = tmp_path / "Żółć.wav"
    source.write_bytes(b"RIFF")
    context, error = clip_context_from_status(
        {
            "engine": "files",
            "id": "file:żółć",
            "source": str(source),
            "title": "Żółć",
            "positionSeconds": 7.0,
            "durationSeconds": 60.0,
        },
        files_session_active=True,
        player_view_active=True,
    )
    assert error is None
    assert context is not None
    assert context.source_path == str(source)
    assert context.title == "Żółć"


def test_context_reports_unknown_duration_before_touching_missing_file() -> None:
    context, error = clip_context_from_status(
        {
            "engine": "files",
            "id": "file:a",
            "source": r"Z:\brak.mp3",
            "durationSeconds": 0,
        },
        files_session_active=True,
        player_view_active=True,
    )
    assert context is None
    assert error == "Nie można zaznaczyć fragmentu, ponieważ czas trwania pliku jest nieznany"


def test_saved_selections_reject_nan_reverse_duplicates_and_unknown_fields() -> None:
    raw = [
        {
            "itemId": "a",
            "sourcePath": r"C:\A.wav",
            "startSeconds": 1.0,
            "endSeconds": 2.0,
            "technicalObject": "must not survive",
        },
        {"itemId": "a", "sourcePath": r"c:\a.WAV", "startSeconds": 3.0},
        {"itemId": "b", "sourcePath": "b.wav", "startSeconds": math.nan},
        {"itemId": "c", "sourcePath": "c.wav", "startSeconds": 4.0, "endSeconds": 3.0},
    ]
    assert read_clip_selections(raw) == [{
        "itemId": "a",
        "sourcePath": r"C:\A.wav",
        "startSeconds": 1.0,
        "endSeconds": 2.0,
    }]


def test_update_restore_and_clear_are_scoped_to_one_file() -> None:
    existing = [{
        "itemId": "other",
        "sourcePath": "other.wav",
        "startSeconds": 1.0,
        "endSeconds": None,
    }]
    current = AudioClipSelection("id", "current.wav", 2.0, 5.0)
    saved = update_clip_selections(existing, current)
    restored = restore_clip_selection(saved, "id", "CURRENT.WAV", 20.0)
    assert restored.start_seconds == 2.0
    assert restored.end_seconds == 5.0
    missing = restore_clip_selection(saved, "missing", "missing.wav", 20.0)
    assert not missing.matches("missing", "missing.wav")

    cleared = update_clip_selections(
        saved, AudioClipSelection("id", "current.wav")
    )
    assert [item["itemId"] for item in cleared] == ["other"]


def test_format_choices_keep_user_labels_separate_from_protocol_values() -> None:
    choices = format_choices_from_payload({
        "formats": [
            {
                "value": "original",
                "label": "Bez konwersji; zachowaj jakość",
                "extension": ".m4a",
                "available": True,
            },
            {
                "value": "FlacEnum",
                "label": "AudioClipExportFormat.Flac",
                "extension": ".flac",
                "available": True,
            },
            {
                "value": "wav",
                "label": "WAV; dokładny fragment",
                "extension": ".wav",
                "available": False,
            },
        ]
    })
    assert [(choice.value, choice.label) for choice in choices] == [
        ("original", "Bez konwersji; zachowaj jakość")
    ]
    assert suggested_clip_file_name('A/B: "test"', ".wav") == "A_B_ _test_ - fragment.wav"


def test_player_shortcuts_match_full_amc_clip_gestures() -> None:
    expected = {
        Chord("I"): Action.CLIP_MARK_START,
        Chord("O"): Action.CLIP_MARK_END,
        Chord("I", shift=True): Action.CLIP_JUMP_START,
        Chord("O", shift=True): Action.CLIP_JUMP_END,
        Chord("Prior", alt=True): Action.CLIP_PREVIOUS_BOUNDARY,
        Chord("Next", alt=True): Action.CLIP_NEXT_BOUNDARY,
        Chord("S", ctrl=True): Action.CLIP_EXPORT,
        Chord("X", shift=True): Action.CLIP_CLEAR,
    }
    for chord, action in expected.items():
        assert resolve(chord, player_view=True, radio_session=False) is action
        assert resolve(chord, player_view=False, radio_session=False) is not action


def test_host_client_sends_exact_export_payload() -> None:
    client = object.__new__(LiteHostClient)
    calls: list[tuple[str, dict, float]] = []
    client.call = lambda op, args=None, timeout=10.0: calls.append(  # type: ignore[method-assign]
        (op, args or {}, timeout)
    ) or {"ok": True}
    client.export_audio_clip(
        source_path=r"C:\źródło.wav",
        destination_path=r"C:\fragment.wav",
        start_seconds=1.25,
        end_seconds=3.5,
        format_value="wav",
    )
    assert calls == [(
        "audio.clipExport",
        {
            "sourcePath": r"C:\źródło.wav",
            "destinationPath": r"C:\fragment.wav",
            "startSeconds": 1.25,
            "endSeconds": 3.5,
            "format": "wav",
        },
        3_600.0,
    )]
