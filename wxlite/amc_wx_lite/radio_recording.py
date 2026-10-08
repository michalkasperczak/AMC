"""Kontrakt nagrywania radia miedzy wxPython i silnikiem AMC.

Python nie koduje dzwieku i nie zapisuje wspolnego profilu. Czyta jedynie
ustawienia pelnego AMC, a nagranie wykonuje host C# tym samym silnikiem, co
glowne okno. Dzieki temu zwykle stacje i YouTube na zywo maja jedna droge.
"""

from __future__ import annotations

from dataclasses import dataclass, field

from .state_store import Station


_FORMATS = ("Mp3", "Aac", "Flac", "Original", "Wav")
_BITRATES = (96, 128, 160, 192, 256, 320)


@dataclass(frozen=True, slots=True)
class RadioRecordingPreferences:
    """Ustawienia odczytane z ``state.json`` pelnego AMC."""

    format: str = "Mp3"
    bitrate_kbps: int = 192
    default_folder: str | None = None
    folder_preset: str = "radio"
    station_folders: dict[str, str] = field(default_factory=dict)

    def payload_for(self, station: Station) -> dict:
        """Argumenty hosta. Id jest techniczne; nazwa jest jedyna etykieta UI."""
        payload = {
            "stationId": station.id,
            "stationName": station.name,
            "url": station.url,
            "format": self.format,
            "bitrateKbps": self.bitrate_kbps,
            "folderPreset": self.folder_preset,
        }
        folder = self.station_folders.get(station.id) or self.default_folder
        if folder:
            payload["folder"] = folder
        return payload


def preferences_from_amc_state(raw: dict) -> RadioRecordingPreferences:
    """Odtworz wybor folderu/formatu pelnego AMC bez zapisu profilu."""
    radio = raw.get("radio")
    if not isinstance(radio, dict):
        return RadioRecordingPreferences()

    raw_format = radio.get("recordingFormat", "Mp3")
    if isinstance(raw_format, int) and not isinstance(raw_format, bool):
        recording_format = _FORMATS[raw_format] if 0 <= raw_format < len(_FORMATS) else "Mp3"
    elif isinstance(raw_format, str):
        recording_format = next(
            (value for value in _FORMATS if value.casefold() == raw_format.casefold()),
            "Mp3",
        )
    else:
        recording_format = "Mp3"

    raw_bitrate = radio.get("recordingBitrateKbps", 192)
    if not isinstance(raw_bitrate, int) or isinstance(raw_bitrate, bool):
        raw_bitrate = 192
    bitrate = min(_BITRATES, key=lambda value: abs(value - raw_bitrate))

    use_podcasts = radio.get("usePodcastDownloadsFolderForRecordings") is True
    folder_preset = "podcasts" if use_podcasts else "radio"
    configured: str | None = None
    if use_podcasts:
        podcasts = raw.get("podcasts")
        if isinstance(podcasts, dict):
            value = podcasts.get("downloadsFolder")
            if isinstance(value, str) and value.strip():
                configured = value.strip()
    else:
        value = radio.get("recordingsFolder")
        if isinstance(value, str) and value.strip():
            configured = value.strip()

    station_folders: dict[str, str] = {}
    for entry in radio.get("stations") or []:
        if not isinstance(entry, dict):
            continue
        station_id = entry.get("id")
        folder = entry.get("recordingFolder")
        if (isinstance(station_id, str) and station_id
                and isinstance(folder, str) and folder.strip()):
            station_folders[station_id] = folder.strip()

    return RadioRecordingPreferences(
        format=recording_format,
        bitrate_kbps=bitrate,
        default_folder=configured,
        folder_preset=folder_preset,
        station_folders=station_folders,
    )


def format_duration(seconds: object) -> str:
    """Krotki czas nagrania bez technicznych wartosci w komunikacie NVDA."""
    try:
        total = max(0, int(float(seconds)))
    except (TypeError, ValueError, OverflowError):
        total = 0
    hours, rest = divmod(total, 3600)
    minutes, secs = divmod(rest, 60)
    return f"{hours}:{minutes:02d}:{secs:02d}" if hours else f"{minutes}:{secs:02d}"
