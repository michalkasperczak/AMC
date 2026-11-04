"""Compatibility import for standalone laboratory probes."""

from pathlib import Path
import sys

_WXLITE = str(Path(__file__).resolve().parents[1])
if _WXLITE not in sys.path:
    sys.path.insert(0, _WXLITE)

from amc_wx_lite.tidalapi_session_store import (
    CredentialStoreError,
    WindowsCredentialStore,
    restore_session,
    save_if_changed,
)
