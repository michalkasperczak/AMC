"""Apple Music diagnostics expose only approved result summaries."""

from __future__ import annotations

import importlib.util
from pathlib import Path
import unittest


TOOL_PATH = (
    Path(__file__).resolve().parents[1]
    / "tools"
    / "apple_music_safe_diagnostic.py"
)
SPEC = importlib.util.spec_from_file_location(
    "apple_music_safe_diagnostic_tool", TOOL_PATH
)
assert SPEC is not None and SPEC.loader is not None
DIAGNOSTIC = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(DIAGNOSTIC)


PRIVATE_VALUE = "header.private.signature"


class HttpFailure(RuntimeError):
    def __init__(self, status_code: int) -> None:
        super().__init__(PRIVATE_VALUE)
        self.status_code = status_code


class FakeClient:
    def current_user_storefront(self):
        return {"id": "pl", "private": PRIVATE_VALUE}

    def current_user_playlists(self):
        raise HttpFailure(403)

    def current_user_saved_tracks(self):
        raise RuntimeError(PRIVATE_VALUE)

    def _request(self, _method, _path, *, params=None):
        if params == {"limit": 25}:
            return FakeResponse()
        raise HttpFailure(400)


class FakeResponse:
    def json(self):
        return {"data": [{"private": PRIVATE_VALUE}, {"private": PRIVATE_VALUE}]}


class AppleMusicSafeDiagnosticTests(unittest.TestCase):
    def test_output_contains_only_approved_labels_counts_and_status(self) -> None:
        messages: list[str] = []

        result = DIAGNOSTIC.diagnose_client(FakeClient(), output=messages.append)

        self.assertFalse(result)
        self.assertEqual(
            messages,
            [
                "Storefront: OK, elementów 1",
                "Playlisty: HTTP 403",
                "Utwory: błąd bez szczegółów",
            ],
        )
        self.assertFalse(any(PRIVATE_VALUE in message for message in messages))

    def test_parameter_matrix_reports_only_variant_status_and_count(self) -> None:
        messages: list[str] = []

        result = DIAGNOSTIC.diagnose_library_parameters(
            FakeClient(), output=messages.append
        )

        self.assertTrue(result)
        self.assertEqual(
            messages,
            [
                "Utwory, bez parametrów: HTTP 400",
                "Utwory, limit 25: OK, elementów na stronie 2",
                "Utwory, język en-US i limit 25: HTTP 400",
                "Utwory, język pl-PL i limit 25: HTTP 400",
            ],
        )
        self.assertFalse(any(PRIVATE_VALUE in message for message in messages))


if __name__ == "__main__":
    unittest.main()
