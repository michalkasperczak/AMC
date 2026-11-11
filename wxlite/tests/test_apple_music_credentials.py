"""Apple Music secrets remain profile-isolated and unreadable at rest."""

from __future__ import annotations

import os
import sys
import tempfile
import unittest
from dataclasses import dataclass
from pathlib import Path
from types import SimpleNamespace
from unittest.mock import patch

from amc_wx_lite.apple_music_credentials import (
    AppleMusicCredentialStore,
    AppleMusicCredentialStoreError,
    AppleMusicLibraryUnavailable,
    AppleMusicWebCredentials,
    credential_path,
    credential_purpose,
    decode_credentials,
    encode_credentials,
    create_secure_library_auth,
)
from amc_wx_lite.profile_layout import private_sandbox
from amc_wx_lite.release_profile import PUBLIC_PROFILE, TEST_PROFILE


DEVELOPER_TOKEN = "header.payload.signature"
MEDIA_TOKEN = "private-media-user-token"
COOKIE = "media-user-token=private; myacinfo=private-session"


def credentials() -> AppleMusicWebCredentials:
    return AppleMusicWebCredentials(DEVELOPER_TOKEN, MEDIA_TOKEN, COOKIE)


def fake_protect(data: bytes, entropy: bytes) -> bytes:
    return b"AMC1" + bytes(
        value ^ entropy[index % len(entropy)] for index, value in enumerate(data)
    )


def fake_unprotect(data: bytes, entropy: bytes) -> bytes:
    if not data.startswith(b"AMC1"):
        raise AppleMusicCredentialStoreError()
    return bytes(
        value ^ entropy[index % len(entropy)]
        for index, value in enumerate(data.removeprefix(b"AMC1"))
    )


class AppleMusicCredentialTests(unittest.TestCase):
    def setUp(self) -> None:
        self.temporary = tempfile.TemporaryDirectory()
        self.root = Path(self.temporary.name)

    def tearDown(self) -> None:
        self.temporary.cleanup()

    def test_round_trip_uses_profile_owned_encrypted_file(self) -> None:
        layout = private_sandbox(self.root)
        store = AppleMusicCredentialStore(
            layout,
            profile=TEST_PROFILE,
            protect=fake_protect,
            unprotect=fake_unprotect,
        )

        self.assertIsNone(store.read())
        store.write(credentials())

        self.assertEqual(store.path, credential_path(layout))
        self.assertEqual(store.read(), credentials())
        raw = store.path.read_bytes()
        self.assertNotIn(DEVELOPER_TOKEN.encode(), raw)
        self.assertNotIn(MEDIA_TOKEN.encode(), raw)
        self.assertNotIn(COOKIE.encode(), raw)

        store.clear()
        self.assertIsNone(store.read())

    def test_profiles_use_different_dpapi_purposes_and_paths(self) -> None:
        self.assertNotEqual(
            credential_purpose(TEST_PROFILE), credential_purpose(PUBLIC_PROFILE)
        )
        test = AppleMusicCredentialStore(
            private_sandbox(self.root / "test"),
            profile=TEST_PROFILE,
            protect=fake_protect,
            unprotect=fake_unprotect,
        )
        public = AppleMusicCredentialStore(
            private_sandbox(self.root / "public"),
            profile=PUBLIC_PROFILE,
            protect=fake_protect,
            unprotect=fake_unprotect,
        )
        self.assertNotEqual(test.path, public.path)

    def test_invalid_values_are_rejected_without_echoing_them(self) -> None:
        cases = (
            AppleMusicWebCredentials("not-a-jwt", MEDIA_TOKEN, COOKIE),
            AppleMusicWebCredentials(DEVELOPER_TOKEN, "bad\nheader", COOKIE),
            AppleMusicWebCredentials(DEVELOPER_TOKEN, MEDIA_TOKEN, "no-cookie-pair"),
        )
        for bad in cases:
            with self.subTest(value=bad.media_user_token[:3]):
                with self.assertRaises(AppleMusicCredentialStoreError) as caught:
                    encode_credentials(bad)
                message = str(caught.exception)
                self.assertNotIn(DEVELOPER_TOKEN, message)
                self.assertNotIn(MEDIA_TOKEN, message)
                self.assertNotIn("bad", message.casefold())

    def test_decoder_rejects_extra_fields_without_plaintext_diagnostics(self) -> None:
        data = encode_credentials(credentials()).replace(
            b'"version":1', b'"version":1,"technicalObject":"secret"'
        )
        with self.assertRaises(AppleMusicCredentialStoreError) as caught:
            decode_credentials(data)
        self.assertNotIn("technicalObject", str(caught.exception))
        self.assertNotIn("secret", str(caught.exception))

    def test_startup_smoke_never_opens_dpapi(self) -> None:
        with patch.dict(os.environ, {"AMC_STARTUP_SMOKE": "1"}):
            with self.assertRaises(AppleMusicCredentialStoreError):
                AppleMusicCredentialStore(
                    private_sandbox(self.root), profile=TEST_PROFILE
                )

    @unittest.skipUnless(sys.platform == "win32", "Windows DPAPI")
    def test_real_current_user_dpapi_round_trip(self) -> None:
        store = AppleMusicCredentialStore(
            private_sandbox(self.root), profile=TEST_PROFILE
        )
        store.write(credentials())
        self.assertEqual(store.read(), credentials())
        self.assertNotIn(MEDIA_TOKEN.encode(), store.path.read_bytes())

    def test_library_auth_redirects_plaintext_persistence_to_dpapi(self) -> None:
        layout = private_sandbox(self.root)
        store = AppleMusicCredentialStore(
            layout,
            profile=TEST_PROFILE,
            protect=fake_protect,
            unprotect=fake_unprotect,
        )

        @dataclass
        class ExternalCredentials:
            developer_token: str
            media_user_token: str
            cookie_header: str

        class ExternalAuth:
            def __init__(self, session_dir, **options):
                self.session_dir = Path(session_dir)
                self.options = options

            def _validate(self, _candidate) -> bool:
                return True

            def authenticate(self):
                cached = self.load_cached()
                if cached is not None:
                    return cached
                context = SimpleNamespace(
                    storage_state=lambda **_kwargs: self.fail_plaintext()
                )
                self._save_storage_state(context)
                candidate = ExternalCredentials(
                    DEVELOPER_TOKEN, MEDIA_TOKEN, COOKIE
                )
                self.save(candidate)
                return candidate

            @staticmethod
            def fail_plaintext():
                raise AssertionError("storage-state.json must not be written")

        module = SimpleNamespace(
            AppleMusicAuth=ExternalAuth,
            AppleMusicCredentials=ExternalCredentials,
        )
        auth = create_secure_library_auth(
            layout,
            profile=TEST_PROFILE,
            store=store,
            auth_module=module,
        )

        first = auth.authenticate()
        second = create_secure_library_auth(
            layout,
            profile=TEST_PROFILE,
            store=store,
            auth_module=module,
        ).authenticate()

        self.assertEqual(first, second)
        self.assertFalse(auth.options["headless"])
        self.assertFalse(auth.options["wait_for_enter"])
        self.assertEqual(auth.options["auth_timeout"], 900.0)
        self.assertTrue(store.path.is_file())
        self.assertFalse((auth.session_dir / "credentials.json").exists())
        self.assertFalse((auth.session_dir / "storage-state.json").exists())

    def test_library_auth_is_test_only_and_optional(self) -> None:
        layout = private_sandbox(self.root)
        with self.assertRaises(AppleMusicLibraryUnavailable):
            create_secure_library_auth(
                layout, profile=PUBLIC_PROFILE, auth_module=SimpleNamespace()
            )
        with self.assertRaises(AppleMusicLibraryUnavailable) as caught:
            create_secure_library_auth(
                layout, profile=TEST_PROFILE, auth_module=SimpleNamespace()
            )
        self.assertNotIn("SimpleNamespace", str(caught.exception))


if __name__ == "__main__":
    unittest.main()
