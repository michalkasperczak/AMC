"""The laboratory CLI cannot open Apple login without --login."""

from __future__ import annotations

from contextlib import redirect_stdout
import importlib.util
from io import StringIO
from pathlib import Path
import unittest
from unittest.mock import patch

from amc_wx_lite.apple_music_account import AppleMusicAccountStatus


TOOL_PATH = (
    Path(__file__).resolve().parents[1]
    / "tools"
    / "apple_music_library_probe.py"
)
SPEC = importlib.util.spec_from_file_location("apple_music_probe_tool", TOOL_PATH)
assert SPEC is not None and SPEC.loader is not None
PROBE = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(PROBE)


class AppleMusicProbeTests(unittest.TestCase):
    def test_default_action_checks_local_status_and_never_calls_login(self) -> None:
        output = StringIO()
        status = AppleMusicAccountStatus(False, "Apple Music: brak zapisanej sesji.")
        with (
            patch.object(PROBE, "resolve_layout", return_value=object()),
            patch.object(PROBE, "stored_account_status", return_value=status) as local,
            patch.object(PROBE, "connect_account") as login,
            redirect_stdout(output),
        ):
            result = PROBE.main([])

        self.assertEqual(result, 0)
        local.assert_called_once()
        login.assert_not_called()
        self.assertIn("nie otworzyła przeglądarki", output.getvalue())

    def test_login_flag_is_the_only_action_that_calls_login(self) -> None:
        output = StringIO()
        status = AppleMusicAccountStatus(
            True,
            "Apple Music: logowanie zakończone i sesja zapisana bezpiecznie.",
        )
        with (
            patch.object(PROBE, "resolve_layout", return_value=object()),
            patch.object(PROBE, "stored_account_status") as local,
            patch.object(PROBE, "connect_account", return_value=status) as login,
            redirect_stdout(output),
        ):
            result = PROBE.main(["--login"])

        self.assertEqual(result, 0)
        login.assert_called_once()
        local.assert_not_called()
        self.assertNotIn("nie otworzyła przeglądarki", output.getvalue())


if __name__ == "__main__":
    unittest.main()
