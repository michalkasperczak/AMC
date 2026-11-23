"""Manual, silent WebView2 integration probe; does not play video or use AMC data."""

from __future__ import annotations

import os
import tempfile
from pathlib import Path

os.environ["WEBVIEW2_USER_DATA_FOLDER"] = str(
    Path(tempfile.gettempdir()) / "amc-youtube-embed-probe"
)

import wx

from amc_wx_lite.youtube_embedded import YouTubeEmbeddedPlayer


def main() -> int:
    app = wx.App(False)
    frame = wx.Frame(None, title="AMC YouTube probe", size=(640, 400))
    ready = []
    errors = []
    statuses = []
    player = YouTubeEmbeddedPlayer(
        frame, statuses.append, errors.append, lambda: ready.append(True)
    )
    player.present("M7lc1UVf-VE", play=False)
    wx.CallLater(15000, app.ExitMainLoop)
    app.MainLoop()
    player.stop()
    frame.Destroy()
    print({"ready": bool(ready), "errors": errors,
           "states": [value.get("state") for value in statuses]})
    return 0 if ready and not errors else 1


if __name__ == "__main__":
    raise SystemExit(main())
