"""Official YouTube IFrame player hosted in AMC's wxPython player panel."""

from __future__ import annotations

import json
import re
from collections.abc import Callable
from urllib.parse import parse_qs, urlparse
from uuid import uuid4

import wx
import wx.html2

from . import i18n


_VIDEO_ID = re.compile(r"^[A-Za-z0-9_-]{11}$")
# Stable Inno Setup AppId registered for the Test installation.
_VIRTUAL_HOST = "amc-test.0b807571-bdc5-4287-bd7c-4f4a7c71438f"
_VIRTUAL_ORIGIN = f"https://{_VIRTUAL_HOST}"


def youtube_video_id(address: str | None) -> str | None:
    """Accept a single public HTTPS YouTube film, never a playlist or stream URL."""
    try:
        parsed = urlparse(str(address or "").strip())
        host = (parsed.hostname or "").casefold()
        if (parsed.scheme != "https" or parsed.username or parsed.password
                or parsed.port not in (None, 443)):
            return None
    except ValueError:
        return None
    parts = parsed.path.strip("/").split("/")
    if host == "youtu.be" and len(parts) == 1:
        candidate = parts[0]
    elif host == "youtube.com" or host.endswith(".youtube.com"):
        if parts == ["watch"]:
            values = parse_qs(parsed.query).get("v", [])
            candidate = values[0] if len(values) == 1 else ""
        elif len(parts) == 2 and parts[0] in {"shorts", "live", "embed"}:
            candidate = parts[1]
        else:
            return None
    else:
        return None
    return candidate if _VIDEO_ID.fullmatch(candidate) else None


def _player_html(video_id: str) -> str:
    # video_id has already been constrained to the YouTube ID alphabet.
    return f"""<!doctype html><html lang="pl"><head>
<meta charset="utf-8"><meta name="referrer" content="strict-origin-when-cross-origin">
<meta name="viewport" content="width=device-width,initial-scale=1">
<style>html,body,#player{{width:100%;height:100%;margin:0;background:#000;overflow:hidden}}</style>
</head><body><div id="player"></div>
<script>
const videoId = {json.dumps(video_id)};
let player = null;
function send(kind, value) {{
  window.amc_youtube.postMessage(JSON.stringify({{videoId, kind, value}}));
}}
function snapshot() {{
  if (!player || typeof player.getPlayerState !== 'function') return;
  send('status', {{
    state: player.getPlayerState(),
    positionSeconds: player.getCurrentTime(),
    durationSeconds: player.getDuration(),
    volume: player.getVolume(),
    muted: player.isMuted(),
    rate: player.getPlaybackRate()
  }});
}}
function onYouTubeIframeAPIReady() {{
  player = new YT.Player('player', {{
    width: '100%', height: '100%', videoId,
    playerVars: {{origin: location.origin, autoplay: 0, controls: 1}},
    events: {{
      onReady: () => {{ send('ready', null); snapshot(); }},
      onStateChange: event => {{ send('state', event.data); snapshot(); }},
      onError: event => send('error', event.data),
      onPlaybackRateChange: () => snapshot()
    }}
  }});
}}
</script><script src="https://www.youtube.com/iframe_api"></script>
</body></html>"""


class YouTubeEmbeddedPlayer:
    """UI-thread-only WebView2 adapter; callbacks contain no URLs or raw IDs."""

    def __init__(
        self,
        parent: wx.Window,
        on_status: Callable[[dict], None],
        on_error: Callable[[str], None],
        on_ready: Callable[[], None],
    ) -> None:
        if not wx.html2.WebView.IsBackendAvailable(wx.html2.WebViewBackendEdge):
            raise RuntimeError("Edge WebView2 is unavailable")
        self.control = wx.html2.WebView.New(wx.html2.WebViewBackendEdge)
        if not self.control.Create(parent):
            raise RuntimeError("Edge WebView2 could not be created")
        self.control.SetName(i18n.ui_text("Odtwarzacz filmu YouTube"))
        self.control.SetMinSize((480, 270))
        self.control.Hide()
        self._on_status = on_status
        self._on_error = on_error
        self._on_ready = on_ready
        self._video_id: str | None = None
        self._file_name: str | None = None
        self._ready = False
        self._play_when_ready = False
        wx.FileSystem.AddHandler(wx.MemoryFSHandler())
        handler = wx.html2.WebViewFSHandler("memory")
        handler.SetVirtualHost(_VIRTUAL_HOST)
        self.control.RegisterHandler(handler)
        self.control.AddScriptMessageHandler("amc_youtube")
        self.control.Bind(
            wx.html2.EVT_WEBVIEW_SCRIPT_MESSAGE_RECEIVED, self._message
        )
        self.control.Bind(wx.html2.EVT_WEBVIEW_ERROR, self._navigation_error)
        self.control.Bind(wx.html2.EVT_WEBVIEW_NEWWINDOW, self._new_window)

    @property
    def active(self) -> bool:
        return self._video_id is not None

    @property
    def video_id(self) -> str | None:
        return self._video_id

    def present(self, video_id: str, *, play: bool) -> None:
        if not _VIDEO_ID.fullmatch(video_id):
            raise ValueError("Invalid YouTube video ID")
        self.control.Show()
        if self._video_id == video_id:
            if play:
                self.play()
            return
        self._clear_memory_file()
        self._video_id = video_id
        self._ready = False
        self._play_when_ready = play
        self._file_name = f"amc-youtube-{uuid4().hex}.html"
        wx.MemoryFSHandler.AddFileWithMimeType(
            self._file_name, _player_html(video_id), "text/html"
        )
        self.control.LoadURL(f"{_VIRTUAL_ORIGIN}/{self._file_name}")

    def _message(self, event: wx.html2.WebViewEvent) -> None:
        try:
            message = json.loads(event.GetString())
        except (ValueError, TypeError):
            return
        if not isinstance(message, dict) or message.get("videoId") != self._video_id:
            return
        kind = message.get("kind")
        value = message.get("value")
        if kind == "ready":
            self._ready = True
            self._on_ready()
            if self._play_when_ready:
                self.play()
        elif kind == "status" and isinstance(value, dict):
            self._on_status(value)
        elif kind == "state" and type(value) is int:
            self._on_status({"state": value})
        elif kind == "error":
            self._on_error("Film YouTube nie pozwala na odtwarzanie w AMC")

    def _navigation_error(self, _event: wx.html2.WebViewEvent) -> None:
        address = _event.GetURL()
        host = (urlparse(address).hostname or "").casefold()
        if self.active and (host == _VIRTUAL_HOST or host.endswith(".youtube.com")):
            self._on_error("Nie można połączyć się z odtwarzaczem YouTube")

    def _new_window(self, event: wx.html2.WebViewEvent) -> None:
        event.Veto()
        address = event.GetURL()
        try:
            parsed = urlparse(address)
            host = (parsed.hostname or "").casefold()
            if (parsed.scheme == "https" and not parsed.username
                    and not parsed.password and parsed.port in (None, 443)
                    and (host == "youtube.com" or host.endswith(".youtube.com")
                         or host == "youtu.be")):
                wx.LaunchDefaultBrowser(address)
        except ValueError:
            pass

    def _command(self, script: str) -> None:
        if self._ready:
            self.control.RunScriptAsync(f"if (player) {{ {script}; snapshot(); }}")

    def play(self) -> None:
        self._play_when_ready = True
        if self.control.IsShownOnScreen():
            self._command("player.playVideo()")

    def pause(self) -> None:
        self._play_when_ready = False
        self._command("player.pauseVideo()")

    def seek_to(self, seconds: float) -> None:
        self._command(f"player.seekTo({max(0.0, float(seconds))!r}, true)")

    def set_volume(self, value: int) -> None:
        self._command(f"player.setVolume({max(0, min(100, int(value)))})")

    def toggle_mute(self) -> None:
        self._command("player.isMuted() ? player.unMute() : player.mute()")

    def set_rate(self, value: float) -> None:
        self._command(f"player.setPlaybackRate({float(value)!r})")

    def poll(self) -> None:
        self._command("snapshot()")

    def _clear_memory_file(self) -> None:
        if self._file_name is not None:
            wx.MemoryFSHandler.RemoveFile(self._file_name)
            self._file_name = None

    def stop(self) -> None:
        if self._video_id is not None:
            self.pause()
            self._video_id = None
            self._ready = False
            self._play_when_ready = False
            self.control.LoadURL("about:blank")
        self.control.Hide()
        self._clear_memory_file()
