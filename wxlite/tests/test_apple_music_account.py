"""Apple Music account actions require an explicit login action."""

from __future__ import annotations

from types import SimpleNamespace
import tempfile
import unittest
from pathlib import Path

from amc_wx_lite.apple_music_account import (
    AppleMusicAccountError,
    connect_account,
    stored_account_status,
)
from amc_wx_lite.apple_music_credentials import AppleMusicWebCredentials
from amc_wx_lite.profile_layout import private_sandbox
from amc_wx_lite.release_profile import PUBLIC_PROFILE, TEST_PROFILE


PRIVATE_TOKEN = "header.private.signature"


class FakeStore:
    def __init__(self, value: AppleMusicWebCredentials | None) -> None:
        self.value = value
        self.reads = 0

    def read(self) -> AppleMusicWebCredentials | None:
        self.reads += 1
        return self.value


def credentials() -> AppleMusicWebCredentials:
    return AppleMusicWebCredentials(
        PRIVATE_TOKEN,
        "private-media-user-token",
        "media-user-token=private-session",
    )


class AppleMusicAccountTests(unittest.TestCase):
    def setUp(self) -> None:
        self.temporary = tempfile.TemporaryDirectory()
        self.layout = private_sandbox(Path(self.temporary.name))

    def tearDown(self) -> None:
        self.temporary.cleanup()

    def test_status_without_session_is_local_and_does_not_create_auth(self) -> None:
        store = FakeStore(None)

        status = stored_account_status(
            self.layout,
            profile=TEST_PROFILE,
            store=store,
        )

        self.assertFalse(status.stored_session)
        self.assertEqual(status.message, "Apple Music: brak zapisanej sesji.")
        self.assertEqual(store.reads, 1)

    def test_status_does_not_claim_that_a_stored_session_is_valid(self) -> None:
        status = stored_account_status(
            self.layout,
            profile=TEST_PROFILE,
            store=FakeStore(credentials()),
        )

        self.assertTrue(status.stored_session)
        self.assertIn("ważność nie została sprawdzona", status.message)
        self.assertNotIn(PRIVATE_TOKEN, status.message)

    def test_connect_is_the_only_action_that_invokes_authentication(self) -> None:
        calls: list[str] = []

        class FakeAuth:
            def authenticate(self) -> None:
                calls.append("authenticate")

        def factory(*_args, **options):
            self.assertEqual(options["profile"], TEST_PROFILE)
            self.assertEqual(options["auth_timeout"], 12.0)
            return FakeAuth()

        status = connect_account(
            self.layout,
            profile=TEST_PROFILE,
            auth_factory=factory,
            auth_timeout=12.0,
        )

        self.assertEqual(calls, ["authenticate"])
        self.assertTrue(status.stored_session)
        self.assertIn("logowanie zakończone", status.message)

    def test_provider_failure_is_replaced_with_safe_message(self) -> None:
        def factory(*_args, **_options):
            return SimpleNamespace(
                authenticate=lambda: (_ for _ in ()).throw(
                    RuntimeError(PRIVATE_TOKEN)
                )
            )

        with self.assertRaises(AppleMusicAccountError) as caught:
            connect_account(
                self.layout,
                profile=TEST_PROFILE,
                auth_factory=factory,
            )

        self.assertEqual(
            str(caught.exception),
            "Nie udało się połączyć konta Apple Music.",
        )
        self.assertNotIn(PRIVATE_TOKEN, str(caught.exception))

    def test_public_profile_refuses_status_and_login(self) -> None:
        store = FakeStore(credentials())
        with self.assertRaises(AppleMusicAccountError):
            stored_account_status(
                self.layout,
                profile=PUBLIC_PROFILE,
                store=store,
            )
        with self.assertRaises(AppleMusicAccountError):
            connect_account(
                self.layout,
                profile=PUBLIC_PROFILE,
                auth_factory=lambda *_args, **_kwargs: None,
            )
        self.assertEqual(store.reads, 0)


if __name__ == "__main__":
    unittest.main()
