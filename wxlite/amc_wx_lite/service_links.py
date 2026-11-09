"""Exact public catalogue links for supported service identities.

The provider key is explicit. Future adapters may supply a public HTTPS URI;
unknown identifiers are never turned into guessed links.
"""

from __future__ import annotations

import re
from urllib.parse import urlsplit


_KINDS = frozenset({
    "track", "album", "artist", "podcast", "episode", "playlist", "video",
})
_SPOTIFY_ID = re.compile(r"[A-Za-z0-9]{1,64}\Z")
_TIDAL_ID = re.compile(r"[0-9]{1,18}\Z")


def exact_service_link(
    service: str,
    kind: str,
    service_id: str | None,
    public_uri: str | None = None,
) -> str:
    """Use a provider supplied URL or a validated catalogue identity."""

    if kind not in _KINDS:
        return ""
    uri = (public_uri or "").strip()
    if uri:
        try:
            parsed = urlsplit(uri)
            host = (parsed.hostname or "").casefold()
        except ValueError:
            parsed = None
            host = ""
        allowed_host = {
            "spotify": "open.spotify.com",
            "tidal": "tidal.com",
        }.get(service)
        path_kind = "show" if kind == "podcast" else kind
        path_parts = parsed.path.strip("/").split("/") if parsed else []
        matches_kind = (
            service == "spotify" and len(path_parts) >= 2
            and path_parts[0] == path_kind
            or service == "tidal" and len(path_parts) >= 3
            and path_parts[:2] == ["browse", path_kind]
            or service not in {"spotify", "tidal"}
        )
        if (
            parsed is not None
            and parsed.scheme.casefold() in (
                {"http", "https"} if service == "podcasts" else {"https"}
            )
            and parsed.username is None
            and parsed.password is None
            and host
            and (allowed_host is None or host == allowed_host)
            and matches_kind
        ):
            return uri

    identifier = (service_id or "").strip()
    if service == "spotify":
        path_kind = "show" if kind == "podcast" else kind
        prefix = f"spotify:{path_kind}:"
        if identifier.startswith(prefix):
            identifier = identifier[len(prefix):]
        if kind != "video" and _SPOTIFY_ID.fullmatch(identifier):
            return f"https://open.spotify.com/{path_kind}/{identifier}"
    elif service == "tidal" and kind in {"track", "album", "artist"}:
        prefix = f"{kind}s:"
        if identifier.startswith(prefix):
            identifier = identifier[len(prefix):]
        if _TIDAL_ID.fullmatch(identifier):
            return f"https://tidal.com/browse/{kind}/{identifier}"
    return ""
