"""The web request probe discards all header and query values."""

from __future__ import annotations

import importlib.util
from pathlib import Path
import sys
import unittest


TOOL_PATH = (
    Path(__file__).resolve().parents[1]
    / "tools"
    / "apple_music_web_request_probe.py"
)
SPEC = importlib.util.spec_from_file_location(
    "apple_music_web_request_probe_tool", TOOL_PATH
)
assert SPEC is not None and SPEC.loader is not None
PROBE = importlib.util.module_from_spec(SPEC)
sys.modules[SPEC.name] = PROBE
SPEC.loader.exec_module(PROBE)


PRIVATE = "header.private.signature"


class AppleMusicWebRequestProbeTests(unittest.TestCase):
    def test_shape_keeps_only_path_and_names(self) -> None:
        shape = PROBE.safe_request_shape(
            "https://amp-api.music.apple.com/v1/me/library/songs"
            f"?l=pl-PL&limit=25&secret={PRIVATE}",
            "get",
            {
                "Authorization": "Bearer " + PRIVATE,
                "Cookie": "media-user-token=" + PRIVATE,
                "media-user-token": PRIVATE,
                "X-Apple-Store-Front": PRIVATE,
                "User-Agent": PRIVATE,
            },
        )

        self.assertIsNotNone(shape)
        self.assertEqual(shape.method, "GET")
        self.assertEqual(shape.path, "/v1/me/library/songs")
        self.assertEqual(shape.query_names, ("l", "limit", "secret"))
        self.assertEqual(shape.extra_header_names, ("x-apple-store-front",))
        self.assertNotIn(PRIVATE, str(shape))

    def test_non_library_or_non_apple_request_is_ignored(self) -> None:
        self.assertIsNone(PROBE.safe_request_shape(
            "https://example.com/v1/me/library/songs", "GET", {}
        ))
        self.assertIsNone(PROBE.safe_request_shape(
            "https://amp-api.music.apple.com/v1/me/storefront", "GET", {}
        ))

    def test_page_classification_does_not_retain_private_text(self) -> None:
        classification = PROBE.classify_page_text(
            "Biblioteka użytkownika " + PRIVATE + " — Wypróbuj bezpłatnie"
        )

        self.assertFalse(classification.sign_in_prompt)
        self.assertTrue(classification.subscription_prompt)
        self.assertNotIn(PRIVATE, str(classification))

    def test_error_body_is_reduced_to_fixed_category(self) -> None:
        category = PROBE.classify_error_body(
            '{"detail":"active subscription required", "private":"'
            + PRIVATE
            + '"}'
        )

        self.assertEqual(category, "wymagana aktywna subskrypcja")
        self.assertNotIn(PRIVATE, category)


if __name__ == "__main__":
    unittest.main()
