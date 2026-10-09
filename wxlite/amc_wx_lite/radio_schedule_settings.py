"""Prywatne, bezpieczne ustawienia harmonogramow interfejsu wxPython.

Pelny AMC nadal jest zrodlem poczatkowym. Dopoki uzytkownik nie zmieni planu
w wxPython, interfejs tylko go odczytuje. Pierwsza zmiana tworzy kompletne
prywatne nadpisanie, dzieki czemu proces WPF i wxPython nigdy nie zapisuja
jednoczesnie tego samego ``state.json``.

W tym module nie ma wx. To celowe: ksztalt danych, przeliczanie czasu i
walidacje daja sie sprawdzic bez pulpitu, a do kontrolek trafiaja dopiero
gotowe etykiety uzytkowe.
"""

from __future__ import annotations

import re
import uuid
from collections.abc import Iterable, Mapping, Sequence
from datetime import date, datetime, time, timedelta, timezone
from pathlib import Path
from urllib.parse import urlsplit

DOTNET_UNIX_EPOCH_TICKS = 621_355_968_000_000_000
DEFAULT_FILE_NAME_TEMPLATE = "{stacja} - {data} {czas}"
MAXIMUM_DURATION_MINUTES = 10_080
FORMATS = ("Mp3", "Aac", "Flac", "Original", "Wav")
BITRATES = (96, 128, 160, 192, 256, 320)
RECURRENCES = ("Once", "Daily", "SelectedDays")
DAY_NAMES = (
    "Monday",
    "Tuesday",
    "Wednesday",
    "Thursday",
    "Friday",
    "Saturday",
    "Sunday",
)
DAY_LABELS = (
    "Poniedziałek",
    "Wtorek",
    "Środa",
    "Czwartek",
    "Piątek",
    "Sobota",
    "Niedziela",
)
SUPPORTED_FILE_NAME_TOKENS = {
    "{stacja}",
    "{data}",
    "{data-polska}",
    "{data-zwarta}",
    "{rok}",
    "{miesiąc}",
    "{dzień}",
    "{dzień-tygodnia}",
    "{czas}",
    "{godzina}",
    "{minuta}",
    "{część}",
}
_TOKEN_PATTERN = re.compile(r"\{[^{}]+\}")
_DAY_FROM_NUMBER = {
    0: "Sunday",
    1: "Monday",
    2: "Tuesday",
    3: "Wednesday",
    4: "Thursday",
    5: "Friday",
    6: "Saturday",
}


class ScheduleValidationError(ValueError):
    """Blad gotowy do pokazania uzytkownikowi, bez nazw technicznych."""


def _text(value: object) -> str:
    return value.strip() if isinstance(value, str) else ""


def _strict_bool(value: object, default: bool) -> bool:
    return value if type(value) is bool else default


def _optional_bool(value: object) -> bool | None:
    return value if type(value) is bool else None


def _positive_int(value: object, default: int, maximum: int) -> int:
    if not isinstance(value, int) or isinstance(value, bool):
        return default
    return max(1, min(maximum, value))


def _optional_int(value: object) -> int | None:
    return value if isinstance(value, int) and not isinstance(value, bool) else None


def _canonical_choice(value: object, choices: Sequence[str], default: str) -> str:
    if isinstance(value, str):
        wanted = value.strip().casefold()
        for choice in choices:
            if choice.casefold() == wanted:
                return choice
    if (
        isinstance(value, int)
        and not isinstance(value, bool)
        and 0 <= value < len(choices)
    ):
        return choices[value]
    return default


def _active_days(value: object) -> list[str]:
    if not isinstance(value, list):
        return []
    result: list[str] = []
    for raw in value:
        day = (
            _DAY_FROM_NUMBER.get(raw)
            if isinstance(raw, int) and not isinstance(raw, bool)
            else None
        )
        if day is None and isinstance(raw, str):
            day = next(
                (
                    item
                    for item in DAY_NAMES
                    if item.casefold() == raw.strip().casefold()
                ),
                None,
            )
        if day is not None and day not in result:
            result.append(day)
    return result


def validate_file_name_template(value: str) -> str:
    template = value.strip()
    if not template:
        raise ScheduleValidationError("Wpisz nazwę pliku albo wybierz gotowy szablon")
    if len(template) > 240:
        raise ScheduleValidationError("Szablon nazwy może mieć najwyżej 240 znaków")
    for token in _TOKEN_PATTERN.findall(template):
        if token.casefold() not in {
            item.casefold() for item in SUPPORTED_FILE_NAME_TOKENS
        }:
            raise ScheduleValidationError(f"Nieznany token w nazwie pliku: {token}")
    without_tokens = _TOKEN_PATTERN.sub("", template)
    if "{" in without_tokens or "}" in without_tokens:
        raise ScheduleValidationError(
            "Token w nazwie pliku ma niepełny nawias klamrowy"
        )
    return template


def sanitize_schedule(raw: object) -> dict | None:
    """Przyjmij tylko pola znane hostowi; obcy obiekt nie trafi do UI ani IPC."""
    if not isinstance(raw, dict):
        return None
    schedule_id = _text(raw.get("id"))
    station_id = _text(raw.get("stationId"))
    station_name = _text(raw.get("stationName"))
    stream_url = _text(raw.get("streamUrl"))
    ticks = raw.get("nextStartUtcTicks")
    if (
        not schedule_id
        or not station_id
        or not station_name
        or not is_http_url(stream_url)
        or not isinstance(ticks, int)
        or isinstance(ticks, bool)
        or ticks <= 0
    ):
        return None

    duration = _positive_int(raw.get("durationMinutes"), 60, MAXIMUM_DURATION_MINUTES)
    segment = raw.get("segmentMinutes")
    if not isinstance(segment, int) or isinstance(segment, bool):
        segment = 0
    segment = max(0, min(MAXIMUM_DURATION_MINUTES, segment))
    if segment >= duration:
        segment = 0

    template = _text(raw.get("fileNameTemplate")) or DEFAULT_FILE_NAME_TEMPLATE
    try:
        template = validate_file_name_template(template)
    except ScheduleValidationError:
        template = DEFAULT_FILE_NAME_TEMPLATE

    raw_format = raw.get("recordingFormat")
    recording_format = (
        _canonical_choice(raw_format, FORMATS, "Mp3")
        if raw_format is not None
        else None
    )
    bitrate = _optional_int(raw.get("recordingBitrateKbps"))
    if bitrate is not None:
        bitrate = min(BITRATES, key=lambda candidate: abs(candidate - bitrate))

    result = {
        "id": schedule_id,
        "name": _text(raw.get("name")),
        "stationId": station_id,
        "stationName": station_name,
        "streamUrl": stream_url,
        "nextStartUtcTicks": ticks,
        "timeZoneId": _text(raw.get("timeZoneId")),
        "durationMinutes": duration,
        "segmentMinutes": segment,
        "recurrence": _canonical_choice(raw.get("recurrence"), RECURRENCES, "Once"),
        "activeDays": _active_days(raw.get("activeDays")),
        "outputFolder": _text(raw.get("outputFolder")),
        "fileNameTemplate": template,
        "recordingFormat": recording_format,
        "recordingBitrateKbps": bitrate,
        "wakeComputer": _optional_bool(raw.get("wakeComputer")),
        "enabled": _strict_bool(raw.get("enabled"), True),
        "suppressedOccurrenceStartUtcTicks": _optional_int(
            raw.get("suppressedOccurrenceStartUtcTicks")
        ),
        "lastFailureUtcTicks": _optional_int(raw.get("lastFailureUtcTicks")),
        "lastFailureMessage": _text(raw.get("lastFailureMessage")),
        "lastFailureAcknowledged": _strict_bool(
            raw.get("lastFailureAcknowledged"), True
        ),
    }
    if result["recurrence"] != "SelectedDays":
        result["activeDays"] = []
    return result


def read_schedule_overrides(raw: object) -> list[dict] | None:
    """``None`` oznacza dziedziczenie profilu; pusta lista oznacza brak planow."""
    if raw is None:
        return None
    if not isinstance(raw, list):
        return None
    result: list[dict] = []
    seen: set[str] = set()
    for item in raw:
        schedule = sanitize_schedule(item)
        if schedule is None or schedule["id"] in seen:
            continue
        seen.add(schedule["id"])
        result.append(schedule)
    return result


def clone_schedules(schedules: Iterable[Mapping]) -> list[dict]:
    return [
        schedule
        for item in schedules
        if (schedule := sanitize_schedule(dict(item))) is not None
    ]


def effective_schedules(
    profile_schedules: Iterable[Mapping], overrides: list[dict] | None
) -> list[dict]:
    if overrides is None:
        # Przed pierwsza zmiana zachowujemy kontrakt profilu bajtowo na
        # poziomie pol: host C# moze znac nowsze pole, ktorego Python jeszcze
        # nie interpretuje. Dopiero prywatne nadpisanie jest naszym modelem i
        # przechodzi przez zamknieta liste bezpiecznych pol.
        return [dict(item) for item in profile_schedules if isinstance(item, Mapping)]
    return clone_schedules(overrides)


def effective_wake(profile_value: bool, override: bool | None) -> bool:
    return profile_value if type(override) is not bool else override


def schedules_from_host_status(
    payload: object, fallback: Iterable[Mapping]
) -> list[dict]:
    """Odczytaj terminy przesuniete przez host, bez etykiet i technicznych repr."""
    fallback_schedules = clone_schedules(fallback)
    if not isinstance(payload, dict) or not isinstance(payload.get("schedules"), list):
        return fallback_schedules
    result: list[dict] = []
    seen: set[str] = set()
    for item in payload["schedules"]:
        schedule = sanitize_schedule(item)
        if schedule is None or schedule["id"] in seen:
            continue
        seen.add(schedule["id"])
        result.append(schedule)
    return result if result or not fallback_schedules else fallback_schedules


def is_http_url(value: str) -> bool:
    try:
        parsed = urlsplit(value.strip())
    except ValueError:
        return False
    return parsed.scheme.casefold() in ("http", "https") and bool(parsed.netloc)


def parse_local_start(date_text: str, time_text: str) -> datetime:
    try:
        chosen_date = date.fromisoformat(date_text.strip())
    except ValueError as error:
        raise ScheduleValidationError("Data musi mieć format RRRR-MM-DD") from error
    try:
        chosen_time = time.fromisoformat(time_text.strip())
    except ValueError as error:
        raise ScheduleValidationError("Czas musi mieć format GG:MM") from error
    if chosen_time.second or chosen_time.microsecond:
        chosen_time = chosen_time.replace(second=0, microsecond=0)
    return datetime.combine(chosen_date, chosen_time)


def _local_naive_to_utc(value: datetime) -> datetime:
    # ``astimezone`` na naiwnym datetime pyta system Windows o lokalny offset
    # DLA WSKAZANEJ DATY, wiec uwzglednia takze przyszla zmiane czasu.
    return value.astimezone().astimezone(timezone.utc)


def utc_ticks_from_local(
    local_start: datetime,
    recurrence: str,
    active_days: Sequence[str],
    *,
    now_utc: datetime | None = None,
) -> int:
    """Wyznacz najblizsze wystapienie zgodnie z regułami pelnego AMC."""
    recurrence = _canonical_choice(recurrence, RECURRENCES, "Once")
    selected = {day for day in active_days if day in DAY_NAMES}
    now = (now_utc or datetime.now(timezone.utc)).astimezone(timezone.utc)
    candidate = local_start.replace(second=0, microsecond=0)

    for _ in range(3_701):
        day_allowed = (
            recurrence != "SelectedDays" or DAY_NAMES[candidate.weekday()] in selected
        )
        candidate_utc = _local_naive_to_utc(candidate)
        if day_allowed and candidate_utc > now:
            return int(candidate_utc.timestamp() * 10_000_000) + DOTNET_UNIX_EPOCH_TICKS
        if recurrence == "Once":
            raise ScheduleValidationError(
                "Jednorazowe nagranie musi rozpoczynać się w przyszłości"
            )
        candidate = datetime.combine(
            candidate.date() + timedelta(days=1), candidate.time()
        )
    raise ScheduleValidationError("Nie można wyznaczyć następnego terminu")


def local_start_from_utc_ticks(ticks: int) -> datetime:
    seconds = (ticks - DOTNET_UNIX_EPOCH_TICKS) / 10_000_000
    try:
        return (
            datetime.fromtimestamp(seconds, timezone.utc)
            .astimezone()
            .replace(second=0, microsecond=0)
        )
    except (OverflowError, OSError, ValueError):
        return datetime.now().astimezone().replace(second=0, microsecond=0) + timedelta(
            minutes=5
        )


def next_copy_name(schedule: Mapping, schedules: Iterable[Mapping]) -> str:
    basis = _text(schedule.get("name")) or _text(schedule.get("stationName")) or "Plan"
    match = re.search(r" \((\d+)\)$", basis)
    if match and int(match.group(1)) >= 2:
        basis = basis[: match.start()]
    used = {
        (_text(item.get("name")) or _text(item.get("stationName"))).casefold()
        for item in schedules
    }
    number = 2
    while f"{basis} ({number})".casefold() in used:
        number += 1
    return f"{basis} ({number})"


def duplicate_schedule(schedule: Mapping, schedules: Iterable[Mapping]) -> dict:
    copy = sanitize_schedule(dict(schedule))
    if copy is None:
        raise ScheduleValidationError("Nie można powielić tego planu")
    copy["id"] = uuid.uuid4().hex
    copy["name"] = next_copy_name(schedule, schedules)
    copy["enabled"] = False
    copy["suppressedOccurrenceStartUtcTicks"] = None
    copy["lastFailureUtcTicks"] = None
    copy["lastFailureMessage"] = ""
    copy["lastFailureAcknowledged"] = True
    return copy


def output_folder_is_valid(value: str) -> bool:
    return not value or Path(value).is_absolute()
