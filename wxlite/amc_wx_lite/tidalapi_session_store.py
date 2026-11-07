"""Profile-isolated TIDAL playback storage in Windows Credential Manager.

The credential is intentionally separate from AMC's official TIDAL login.
No token or signed media URL is written to a normal file or exception text.
"""

from __future__ import annotations

import ctypes
import datetime as dt
import json
import os
import sys
from ctypes import wintypes

from .release_profile import ReleaseProfile, current_profile

_MAX_BLOB_BYTES = 5 * 512
_ERROR_NOT_FOUND = 1168


def credential_target(profile: ReleaseProfile) -> str:
    """Keep Test's existing target while isolating every release profile."""
    return f"{profile.credential_namespace}.TidalapiPlayback.v1"


class CredentialStoreError(Exception):
    """An intentionally detail-free storage failure."""

    def __str__(self) -> str:
        return "credential_store_unavailable"


class _Credential(ctypes.Structure):
    _fields_ = [
        ("Flags", wintypes.DWORD),
        ("Type", wintypes.DWORD),
        ("TargetName", wintypes.LPWSTR),
        ("Comment", wintypes.LPWSTR),
        ("LastWritten", wintypes.FILETIME),
        ("CredentialBlobSize", wintypes.DWORD),
        ("CredentialBlob", ctypes.POINTER(ctypes.c_ubyte)),
        ("Persist", wintypes.DWORD),
        ("AttributeCount", wintypes.DWORD),
        ("Attributes", ctypes.c_void_p),
        ("TargetAlias", wintypes.LPWSTR),
        ("UserName", wintypes.LPWSTR),
    ]


class WindowsCredentialStore:
    """Read and write a per-user, local-machine generic credential."""

    def __init__(self, profile: ReleaseProfile | None = None) -> None:
        if os.environ.get("AMC_STARTUP_SMOKE") == "1":
            raise CredentialStoreError()
        if sys.platform != "win32":
            raise CredentialStoreError()
        selected_profile = profile or current_profile()
        self._target = credential_target(selected_profile)
        self._user_name = f"{selected_profile.display_name} TIDAL playback"
        try:
            library = ctypes.WinDLL("Advapi32.dll", use_last_error=True)
            library.CredReadW.argtypes = [
                wintypes.LPCWSTR, wintypes.DWORD, wintypes.DWORD,
                ctypes.POINTER(ctypes.POINTER(_Credential)),
            ]
            library.CredReadW.restype = wintypes.BOOL
            library.CredWriteW.argtypes = [ctypes.POINTER(_Credential), wintypes.DWORD]
            library.CredWriteW.restype = wintypes.BOOL
            library.CredFree.argtypes = [ctypes.c_void_p]
            library.CredFree.restype = None
            self._library = library
        except Exception:
            raise CredentialStoreError() from None

    def read(self) -> bytes | None:
        pointer = ctypes.POINTER(_Credential)()
        if not self._library.CredReadW(self._target, 1, 0, ctypes.byref(pointer)):
            if ctypes.get_last_error() == _ERROR_NOT_FOUND:
                return None
            raise CredentialStoreError()
        try:
            credential = pointer.contents
            size = credential.CredentialBlobSize
            if not 0 < size <= _MAX_BLOB_BYTES or not credential.CredentialBlob:
                raise CredentialStoreError()
            return ctypes.string_at(credential.CredentialBlob, size)
        finally:
            self._library.CredFree(pointer)

    def write(self, data: bytes) -> None:
        if not isinstance(data, bytes) or not 0 < len(data) <= _MAX_BLOB_BYTES:
            raise CredentialStoreError()
        blob = (ctypes.c_ubyte * len(data)).from_buffer_copy(data)
        credential = _Credential()
        credential.Type = 1  # CRED_TYPE_GENERIC
        credential.TargetName = self._target
        credential.UserName = self._user_name
        credential.CredentialBlobSize = len(data)
        credential.CredentialBlob = blob
        credential.Persist = 2  # CRED_PERSIST_LOCAL_MACHINE
        if not self._library.CredWriteW(ctypes.byref(credential), 0):
            raise CredentialStoreError()


def _decode(data: bytes) -> dict[str, object] | None:
    try:
        values = json.loads(data.decode("utf-8"))
        if not isinstance(values, dict) or values.get("version") != 1:
            return None
        if values.get("token_type") != "Bearer":
            return None
        for key in ("access_token", "refresh_token"):
            value = values.get(key)
            if not isinstance(value, str) or not value or len(value) > 2048:
                return None
        expiry = values.get("expiry_time")
        if expiry is not None:
            if not isinstance(expiry, str):
                return None
            dt.datetime.fromisoformat(expiry)
        if values.get("is_pkce") is not False:
            return None
        return values
    except (UnicodeError, ValueError, TypeError):
        return None


def restore_session(session: object, store: object) -> tuple[bool, bytes | None]:
    """Return (authenticated, previous blob); False permits device login."""
    data = store.read()
    if data is None:
        return False, None
    values = _decode(data)
    if values is None:
        return False, None
    expiry = values["expiry_time"]
    parsed_expiry = dt.datetime.fromisoformat(expiry) if expiry else None
    # tidalapi 0.8.11 validates via GET sessions and may refresh an expired token.
    try:
        authenticated = session.load_oauth_session(
            values["token_type"], values["access_token"],
            values["refresh_token"], parsed_expiry, is_pkce=False,
        )
    except Exception as exc:
        # tidalapi raises AuthenticationError for an expired/invalid refresh token.
        # Other failures (including connectivity) must not trigger another code.
        try:
            from tidalapi.exceptions import AuthenticationError
        except ImportError:
            raise exc from None
        if isinstance(exc, AuthenticationError):
            return False, None
        raise
    return bool(authenticated), data if authenticated else None


def save_if_changed(session: object, store: object, previous: bytes | None) -> bytes:
    """Persist current OAuth fields, including access token refreshed by tidalapi."""
    expiry = getattr(session, "expiry_time", None)
    if expiry is not None and not isinstance(expiry, dt.datetime):
        raise CredentialStoreError()
    values = {
        "version": 1,
        "token_type": getattr(session, "token_type", None),
        "access_token": getattr(session, "access_token", None),
        "refresh_token": getattr(session, "refresh_token", None),
        "expiry_time": expiry.isoformat() if expiry else None,
        "is_pkce": False,
    }
    data = json.dumps(values, separators=(",", ":")).encode("utf-8")
    if len(data) > _MAX_BLOB_BYTES or _decode(data) is None:
        raise CredentialStoreError()
    if data != previous:
        store.write(data)
    return data
