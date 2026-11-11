"""Secure local storage for the experimental Apple Music web session.

``apple-music-py`` writes the developer token, media-user-token and the full
cookie header to plain JSON on Windows.  AMC Test must not use that default.
This module keeps the same narrow three-value contract in one DPAPI CurrentUser
blob below the profile-owned wxPython settings directory.

No credential value is included in an exception, accessible label, log or
normal profile file.  The module does not import the optional third-party
library and does not open a browser.
"""

from __future__ import annotations

import ctypes
import importlib
import json
import os
import sys
from ctypes import wintypes
from dataclasses import asdict, dataclass
from pathlib import Path
from typing import Callable

from .profile_layout import ProfileLayout
from .release_profile import TEST_PROFILE_ID, ReleaseProfile, current_profile


_SCHEMA_VERSION = 1
_MAX_PLAINTEXT_BYTES = 256 * 1024
_MAX_PROTECTED_BYTES = 512 * 1024
_CRYPTPROTECT_UI_FORBIDDEN = 0x1


class AppleMusicCredentialStoreError(RuntimeError):
    """A deliberately detail-free storage or validation failure."""

    def __str__(self) -> str:
        return "Bezpieczny magazyn Apple Music jest niedostępny."


class AppleMusicLibraryUnavailable(RuntimeError):
    """The optional experimental dependency is absent or not usable."""

    def __str__(self) -> str:
        return "Eksperymentalny składnik Apple Music nie jest dostępny."


@dataclass(frozen=True, slots=True)
class AppleMusicWebCredentials:
    """The three secrets captured from one signed-in music.apple.com session."""

    developer_token: str
    media_user_token: str
    cookie_header: str


class _DataBlob(ctypes.Structure):
    _fields_ = [
        ("size", wintypes.DWORD),
        ("data", ctypes.POINTER(ctypes.c_ubyte)),
    ]


Protect = Callable[[bytes, bytes], bytes]


def credential_path(layout: ProfileLayout) -> Path:
    """Keep Apple credentials outside host-owned state and databases."""

    return layout.lite_settings_dir / "apple-music" / "credentials.dpapi"


def credential_purpose(profile: ReleaseProfile) -> bytes:
    """Bind one protected blob to the selected Test/Public credential scope."""

    return (
        f"{profile.credential_namespace}.AppleMusicWebCredentials.v1"
    ).encode("utf-8")


def _safe_header_value(value: object, *, maximum: int) -> str | None:
    if not isinstance(value, str) or not value or len(value) > maximum:
        return None
    if "\r" in value or "\n" in value or "\x00" in value:
        return None
    return value


def encode_credentials(credentials: AppleMusicWebCredentials) -> bytes:
    """Validate and serialize without ever formatting a secret into an error."""

    developer = _safe_header_value(credentials.developer_token, maximum=64 * 1024)
    user = _safe_header_value(credentials.media_user_token, maximum=32 * 1024)
    cookies = _safe_header_value(credentials.cookie_header, maximum=128 * 1024)
    if (
        developer is None
        or developer.count(".") != 2
        or user is None
        or cookies is None
        or "=" not in cookies
    ):
        raise AppleMusicCredentialStoreError()
    payload = {
        "version": _SCHEMA_VERSION,
        **asdict(credentials),
    }
    try:
        encoded = json.dumps(
            payload, ensure_ascii=False, separators=(",", ":")
        ).encode("utf-8")
    except (TypeError, UnicodeError):
        raise AppleMusicCredentialStoreError() from None
    if not 0 < len(encoded) <= _MAX_PLAINTEXT_BYTES:
        raise AppleMusicCredentialStoreError()
    return encoded


def decode_credentials(data: bytes) -> AppleMusicWebCredentials:
    """Decode only the exact, versioned credential shape."""

    try:
        if not isinstance(data, bytes) or not 0 < len(data) <= _MAX_PLAINTEXT_BYTES:
            raise ValueError()
        payload = json.loads(data.decode("utf-8"))
        if not isinstance(payload, dict) or payload.get("version") != _SCHEMA_VERSION:
            raise ValueError()
        if set(payload) != {
            "version", "developer_token", "media_user_token", "cookie_header"
        }:
            raise ValueError()
        credentials = AppleMusicWebCredentials(
            developer_token=payload["developer_token"],
            media_user_token=payload["media_user_token"],
            cookie_header=payload["cookie_header"],
        )
        # Reuse one validation policy for both directions.
        encode_credentials(credentials)
        return credentials
    except (AppleMusicCredentialStoreError, KeyError, TypeError, ValueError, UnicodeError):
        raise AppleMusicCredentialStoreError() from None


def _input_blob(data: bytes) -> tuple[_DataBlob, object]:
    if not data:
        raise AppleMusicCredentialStoreError()
    buffer = (ctypes.c_ubyte * len(data)).from_buffer_copy(data)
    return _DataBlob(len(data), ctypes.cast(buffer, ctypes.POINTER(ctypes.c_ubyte))), buffer


def _dpapi_transform(data: bytes, entropy: bytes, *, protect: bool) -> bytes:
    if sys.platform != "win32" or not data or not entropy:
        raise AppleMusicCredentialStoreError()
    try:
        crypt32 = ctypes.WinDLL("Crypt32.dll", use_last_error=True)
        kernel32 = ctypes.WinDLL("Kernel32.dll", use_last_error=True)
        function = (
            crypt32.CryptProtectData if protect else crypt32.CryptUnprotectData
        )
        function.argtypes = [
            ctypes.POINTER(_DataBlob),
            wintypes.LPCWSTR if protect else ctypes.c_void_p,
            ctypes.POINTER(_DataBlob),
            ctypes.c_void_p,
            ctypes.c_void_p,
            wintypes.DWORD,
            ctypes.POINTER(_DataBlob),
        ]
        function.restype = wintypes.BOOL
        kernel32.LocalFree.argtypes = [ctypes.c_void_p]
        kernel32.LocalFree.restype = ctypes.c_void_p
        source, source_buffer = _input_blob(data)
        extra, extra_buffer = _input_blob(entropy)
        output = _DataBlob()
        description = "AMC Apple Music" if protect else None
        # Keep both input buffers alive through the native call.
        _ = source_buffer, extra_buffer
        if not function(
            ctypes.byref(source),
            description,
            ctypes.byref(extra),
            None,
            None,
            _CRYPTPROTECT_UI_FORBIDDEN,
            ctypes.byref(output),
        ):
            raise AppleMusicCredentialStoreError()
        try:
            if not output.data or not 0 < output.size <= _MAX_PROTECTED_BYTES:
                raise AppleMusicCredentialStoreError()
            return ctypes.string_at(output.data, output.size)
        finally:
            if output.data:
                kernel32.LocalFree(output.data)
    except AppleMusicCredentialStoreError:
        raise
    except Exception:
        raise AppleMusicCredentialStoreError() from None


def protect_current_user(data: bytes, entropy: bytes) -> bytes:
    """Encrypt one blob with Windows DPAPI for the current user only."""

    return _dpapi_transform(data, entropy, protect=True)


def unprotect_current_user(data: bytes, entropy: bytes) -> bytes:
    """Decrypt one matching DPAPI blob without allowing system UI."""

    return _dpapi_transform(data, entropy, protect=False)


class AppleMusicCredentialStore:
    """Atomic, profile-isolated DPAPI storage for the experimental account."""

    def __init__(
        self,
        layout: ProfileLayout,
        *,
        profile: ReleaseProfile | None = None,
        protect: Protect = protect_current_user,
        unprotect: Protect = unprotect_current_user,
    ) -> None:
        if os.environ.get("AMC_STARTUP_SMOKE") == "1":
            raise AppleMusicCredentialStoreError()
        selected = profile or current_profile()
        self.path = credential_path(layout)
        self._entropy = credential_purpose(selected)
        self._protect = protect
        self._unprotect = unprotect

    def read(self) -> AppleMusicWebCredentials | None:
        try:
            if not self.path.exists():
                return None
            if not self.path.is_file() or self.path.stat().st_size > _MAX_PROTECTED_BYTES:
                raise AppleMusicCredentialStoreError()
            protected = self.path.read_bytes()
            return decode_credentials(self._unprotect(protected, self._entropy))
        except AppleMusicCredentialStoreError:
            raise
        except OSError:
            raise AppleMusicCredentialStoreError() from None

    def write(self, credentials: AppleMusicWebCredentials) -> None:
        temporary = self.path.with_name(self.path.name + ".tmp")
        try:
            plaintext = encode_credentials(credentials)
            protected = self._protect(plaintext, self._entropy)
            if (
                not isinstance(protected, bytes)
                or not 0 < len(protected) <= _MAX_PROTECTED_BYTES
                or protected == plaintext
            ):
                raise AppleMusicCredentialStoreError()
            self.path.parent.mkdir(parents=True, exist_ok=True)
            temporary.write_bytes(protected)
            os.replace(temporary, self.path)
        except AppleMusicCredentialStoreError:
            try:
                temporary.unlink(missing_ok=True)
            except OSError:
                pass
            raise
        except OSError:
            try:
                temporary.unlink(missing_ok=True)
            except OSError:
                pass
            raise AppleMusicCredentialStoreError() from None

    def clear(self) -> None:
        """Remove only this profile's Apple Music credential blob."""

        try:
            self.path.unlink(missing_ok=True)
        except OSError:
            raise AppleMusicCredentialStoreError() from None


def create_secure_library_auth(
    layout: ProfileLayout,
    *,
    profile: ReleaseProfile | None = None,
    store: AppleMusicCredentialStore | None = None,
    auth_module: object | None = None,
    auth_timeout: float = 900.0,
) -> object:
    """Create apple-music-py's visible login with AMC-owned secure storage.

    The third-party class owns the browser flow.  This adapter replaces all of
    its explicit credential persistence points: the extracted values go to
    DPAPI and Playwright's plaintext ``storage-state.json`` is not written.
    Chromium's own persistent profile remains under the profile-isolated
    Apple Music directory and is never exposed to the GUI.

    The import is intentionally lazy, so AMC can start normally without the
    optional laboratory dependency.
    """

    selected = profile or current_profile()
    if selected.profile_id != TEST_PROFILE_ID:
        raise AppleMusicLibraryUnavailable()
    try:
        module = auth_module or importlib.import_module("applemusic.auth")
        base = getattr(module, "AppleMusicAuth")
        external_credentials = getattr(module, "AppleMusicCredentials")
        if not isinstance(base, type) or not isinstance(external_credentials, type):
            raise TypeError()
    except (ImportError, AttributeError, TypeError):
        raise AppleMusicLibraryUnavailable() from None

    secure_store = store or AppleMusicCredentialStore(layout, profile=selected)
    session_directory = layout.lite_settings_dir / "apple-music"

    class _AmcSecureAppleMusicAuth(base):
        def load_cached(self):
            try:
                saved = secure_store.read()
                if saved is None:
                    return None
                candidate = external_credentials(
                    developer_token=saved.developer_token,
                    media_user_token=saved.media_user_token,
                    cookie_header=saved.cookie_header,
                )
                return candidate if self._validate(candidate) else None
            except AppleMusicCredentialStoreError:
                raise
            except Exception:
                # Network and dependency failures must not disclose credential
                # values or silently start a second browser login.
                return None

        def save(self, candidate) -> None:
            try:
                secure_store.write(AppleMusicWebCredentials(
                    developer_token=getattr(candidate, "developer_token"),
                    media_user_token=getattr(candidate, "media_user_token"),
                    cookie_header=getattr(candidate, "cookie_header"),
                ))
            except AppleMusicCredentialStoreError:
                raise
            except Exception:
                raise AppleMusicCredentialStoreError() from None

        def _save_storage_state(self, _context) -> None:
            # The upstream JSON contains cookies in clear text on Windows.
            return None

    try:
        return _AmcSecureAppleMusicAuth(
            session_dir=session_directory,
            headless=False,
            wait_for_enter=False,
            auth_timeout=auth_timeout,
        )
    except AppleMusicCredentialStoreError:
        raise
    except Exception:
        raise AppleMusicLibraryUnavailable() from None
