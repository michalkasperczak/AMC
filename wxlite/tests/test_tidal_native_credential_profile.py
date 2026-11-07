"""The public trial must not reuse a Test playback session."""

import pytest

from amc_wx_lite.release_profile import PUBLIC_PROFILE, TEST_PROFILE
from amc_wx_lite.tidalapi_session_store import (
    CredentialStoreError, WindowsCredentialStore, credential_target,
)


def test_tidal_playback_credentials_are_isolated_by_release_profile() -> None:
    assert credential_target(TEST_PROFILE) == (
        "AccessibleMediaController.Test.TidalapiPlayback.v1"
    )
    assert credential_target(PUBLIC_PROFILE) == (
        "AccessibleMediaController.Public.TidalapiPlayback.v1"
    )
    assert credential_target(TEST_PROFILE) != credential_target(PUBLIC_PROFILE)


def test_startup_smoke_never_opens_windows_credential_manager(monkeypatch) -> None:
    monkeypatch.setenv("AMC_STARTUP_SMOKE", "1")
    with pytest.raises(CredentialStoreError):
        WindowsCredentialStore(PUBLIC_PROFILE)
