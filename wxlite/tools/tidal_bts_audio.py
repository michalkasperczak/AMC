"""Compatibility import for standalone laboratory probes."""

from pathlib import Path
import sys

_WXLITE = str(Path(__file__).resolve().parents[1])
if _WXLITE not in sys.path:
    sys.path.insert(0, _WXLITE)

from amc_wx_lite.tidal_bts_audio import BtsAudioPlan, BtsResolutionError, RangeProxy, resolve_bts
