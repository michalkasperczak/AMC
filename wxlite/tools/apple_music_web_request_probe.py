#!/usr/bin/env python3
"""Observe Apple Music web request shape without exposing header values."""

from __future__ import annotations

from dataclasses import dataclass
from pathlib import Path
import sys
from urllib.parse import parse_qsl, urlsplit


WXLITE = Path(__file__).resolve().parent.parent
if str(WXLITE) not in sys.path:
    sys.path.insert(0, str(WXLITE))

from amc_wx_lite.profile_layout import resolve_layout  # noqa: E402


APPLE_MUSIC_HOME = "https://music.apple.com"
AMP_HOST = "amp-api.music.apple.com"
CLIENT_HEADER_NAMES = frozenset({
    "accept",
    "accept-language",
    "authorization",
    "cookie",
    "media-user-token",
    "origin",
    "referer",
})
IGNORED_BROWSER_HEADERS = frozenset({
    "priority",
    "sec-ch-ua",
    "sec-ch-ua-mobile",
    "sec-ch-ua-platform",
    "sec-fetch-dest",
    "sec-fetch-mode",
    "sec-fetch-site",
    "user-agent",
})


@dataclass(frozen=True, slots=True)
class SafeRequestShape:
    method: str
    path: str
    query_names: tuple[str, ...]
    extra_header_names: tuple[str, ...]


@dataclass(frozen=True, slots=True)
class SafePageClassification:
    sign_in_prompt: bool
    subscription_prompt: bool


def classify_page_text(text: object) -> SafePageClassification:
    """Reduce the page to two booleans; never retain account or library text."""

    normalized = str(text or "").casefold()
    sign_in_terms = ("sign in", "zaloguj się", "zaloguj sie")
    subscription_terms = (
        "subscribe",
        "subscription",
        "try it free",
        "subskrybuj",
        "subskrypcj",
        "wypróbuj bezpłatnie",
        "wyprobuj bezplatnie",
    )
    return SafePageClassification(
        sign_in_prompt=any(term in normalized for term in sign_in_terms),
        subscription_prompt=any(term in normalized for term in subscription_terms),
    )


def classify_error_body(body: object) -> str:
    """Map an untrusted provider body to one fixed Polish category."""

    normalized = str(body or "").casefold()
    if any(term in normalized for term in (
        "subscription", "subscribe", "subscriber", "subskrypcj",
    )):
        return "wymagana aktywna subskrypcja"
    if any(term in normalized for term in (
        "unauthorized", "forbidden", "authentication", "user token",
        "not authorized",
    )):
        return "brak autoryzacji"
    if any(term in normalized for term in (
        "invalid parameter", "invalid request", "bad request",
        "invalid argument",
    )):
        return "nieprawidłowe żądanie"
    if "not found" in normalized:
        return "brak zasobu"
    return "nierozpoznany błąd"


def safe_request_shape(url: str, method: str, headers: dict[str, str]) -> SafeRequestShape | None:
    """Retain names and route only; discard every potentially secret value."""

    parsed = urlsplit(url)
    if parsed.scheme != "https" or parsed.hostname != AMP_HOST:
        return None
    if not parsed.path.startswith("/v1/me/library/"):
        return None
    query_names = tuple(sorted({name for name, _value in parse_qsl(parsed.query)}))
    header_names = {str(name).casefold() for name in headers}
    extras = tuple(sorted(
        header_names - CLIENT_HEADER_NAMES - IGNORED_BROWSER_HEADERS
    ))
    return SafeRequestShape(
        method=str(method).upper()[:12],
        path=parsed.path,
        query_names=query_names,
        extra_header_names=extras,
    )


def main() -> int:
    reconfigure = getattr(sys.stdout, "reconfigure", None)
    if callable(reconfigure):
        reconfigure(encoding="utf-8")
    try:
        from playwright.sync_api import sync_playwright

        profile_dir = resolve_layout().lite_settings_dir / "apple-music" / "browser-profile"
        if not profile_dir.is_dir():
            print("Brak profilu przeglądarki Apple Music.")
            return 1
        shapes: list[SafeRequestShape] = []
        statuses: dict[SafeRequestShape, list[int]] = {}
        error_categories: dict[SafeRequestShape, list[str]] = {}
        classification = SafePageClassification(False, False)

        with sync_playwright() as playwright:
            context = playwright.chromium.launch_persistent_context(
                str(profile_dir),
                headless=True,
                viewport={"width": 1280, "height": 800},
                args=["--disable-blink-features=AutomationControlled"],
            )
            try:
                page = context.pages[0] if context.pages else context.new_page()

                def on_request(request) -> None:
                    shape = safe_request_shape(
                        request.url, request.method, request.headers
                    )
                    if shape is not None and shape not in shapes and len(shapes) < 10:
                        shapes.append(shape)

                def on_response(response) -> None:
                    shape = safe_request_shape(
                        response.url, response.request.method, response.request.headers
                    )
                    if shape is not None:
                        values = statuses.setdefault(shape, [])
                        status = int(response.status)
                        if status not in values and len(values) < 10:
                            values.append(status)
                        if status >= 400:
                            category = classify_error_body(response.text())
                            categories = error_categories.setdefault(shape, [])
                            if category not in categories and len(categories) < 10:
                                categories.append(category)

                page.on("request", on_request)
                page.on("response", on_response)
                page.goto(
                    f"{APPLE_MUSIC_HOME}/library/songs",
                    wait_until="domcontentloaded",
                    timeout=60_000,
                )
                page.wait_for_timeout(10_000)
                classification = classify_page_text(page.locator("body").inner_text())
            finally:
                context.close()

        if not shapes:
            print("Web player nie wysłał żądania Biblioteki w czasie próby.")
            return 1
        for index, shape in enumerate(shapes, start=1):
            observed = statuses.get(shape, [])
            status_text = ", ".join(str(value) for value in observed) or "brak"
            print(f"Żądanie {index}: {shape.method}, statusy {status_text}")
            print("Parametry: " + (", ".join(shape.query_names) or "brak"))
            print(
                "Dodatkowe nagłówki: "
                + (", ".join(shape.extra_header_names) or "brak")
            )
            categories = error_categories.get(shape, [])
            if categories:
                print("Kategoria błędu: " + ", ".join(categories))
        print(
            "Strona ponownie prosi o logowanie: "
            + ("tak" if classification.sign_in_prompt else "nie")
        )
        print(
            "Strona pokazuje ofertę subskrypcji: "
            + ("tak" if classification.subscription_prompt else "nie")
        )
        return 0
    except Exception:
        print("Nie udało się bezpiecznie zmierzyć żądania web playera.")
        return 1


if __name__ == "__main__":
    raise SystemExit(main())
