"""Input validation for explicit player position commands."""

from __future__ import annotations

import re


def parse_time(raw: str) -> int:
    value = raw.strip()
    if not re.fullmatch(r"[0-9]{1,3}:[0-9]{2}(?::[0-9]{2})?", value):
        raise ValueError("Wpisz czas jako mm:ss albo hh:mm:ss")
    parts = [int(part) for part in value.split(":")]
    if len(parts) == 2:
        minutes, seconds = parts
        if seconds >= 60:
            raise ValueError("Sekundy muszą być mniejsze niż 60")
        total = minutes * 60 + seconds
    else:
        hours, minutes, seconds = parts
        if minutes >= 60 or seconds >= 60:
            raise ValueError("Minuty i sekundy muszą być mniejsze niż 60")
        total = hours * 3600 + minutes * 60 + seconds
    if total > 86_400:
        raise ValueError("Podany czas jest zbyt długi")
    return total


def parse_percent(raw: str) -> int:
    value = raw.strip().removesuffix("%").strip()
    if not re.fullmatch(r"[0-9]{1,3}", value):
        raise ValueError("Wpisz procent jako liczbę od 0 do 100")
    percent = int(value)
    if percent > 100:
        raise ValueError("Procent musi być od 0 do 100")
    return percent
