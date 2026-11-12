"""Lokalne sterowanie Sonosem przez SSDP/UPnP, bez konta i OAuth.

Adapter zachowuje wąski kontrakt używany już przez GUI AMC.  Obiekty SoCo,
adresy IP oraz identyfikatory urządzeń nigdy nie stają się etykietami
dostępnymi.  Import SoCo jest opóźniony, dzięki czemu brak zależności daje
kontrolowany komunikat zamiast uniemożliwiać start całego AMC.
"""

from __future__ import annotations

import threading
import time
from dataclasses import dataclass
from typing import Callable, Protocol


class LocalSonosError(RuntimeError):
    """Błąd lokalnego Sonosa bez technicznych szczegółów w tekście UI."""


class _DiscoveryApi(Protocol):
    def discover(
        self, *, timeout: float, include_invisible: bool
    ) -> object: ...


class _SoCoDiscoveryApi:
    """Mała, leniwa granica zależności zewnętrznej."""

    def discover(
        self, *, timeout: float, include_invisible: bool
    ) -> object:
        try:
            from soco import discover
        except (ImportError, OSError) as error:
            raise LocalSonosError(
                "Brakuje składnika lokalnej obsługi Sonosa. "
                "Zainstaluj ponownie pełny pakiet AMC Test."
            ) from error
        return discover(
            timeout=timeout,
            include_invisible=include_invisible,
        )


@dataclass(frozen=True, slots=True)
class _GroupTarget:
    household_id: str
    group_id: str
    title: str
    group: object


@dataclass(frozen=True, slots=True)
class _Material:
    title: str
    value: object


class LocalSonosClient:
    """Lokalny odpowiednik operacji ``sonos.*`` używanych przez GUI."""

    local_control = True

    def __init__(
        self,
        host_client: object,
        *,
        discovery_api: _DiscoveryApi | None = None,
        discovery_timeout: float = 3.0,
        sleeper: Callable[[float], None] = time.sleep,
    ) -> None:
        self._host = host_client
        self._api = discovery_api or _SoCoDiscoveryApi()
        self._discovery_timeout = max(0.2, float(discovery_timeout))
        self._sleep = sleeper
        self._gate = threading.RLock()
        self._groups: dict[tuple[str, str], _GroupTarget] = {}
        self._materials: dict[tuple[str, str, str], _Material] = {}

    def sonos_targets(self) -> dict[str, object]:
        """Wykryj widoczne grupy w tej samej sieci lokalnej."""

        with self._gate:
            try:
                discovered = self._api.discover(
                    timeout=self._discovery_timeout,
                    include_invisible=False,
                )
            except LocalSonosError:
                raise
            except Exception as error:
                raise LocalSonosError(
                    "Nie udało się wyszukać głośników Sonos w sieci lokalnej."
                ) from error

            zones = tuple(discovered or ())
            if not zones:
                self._groups.clear()
                return {
                    "items": [],
                    "incomplete": False,
                    "message": (
                        "Nie znaleziono głośników Sonos. Komputer i Sonos "
                        "muszą być w tej samej sieci lokalnej, a dostęp "
                        "lokalny UPnP musi być włączony."
                    ),
                }

            targets: dict[tuple[str, str], _GroupTarget] = {}
            rows: list[dict[str, object]] = []
            observed_groups: set[str] = set()
            for zone in zones:
                try:
                    group = getattr(zone, "group")
                    group_id = _identifier(getattr(group, "uid", None))
                    if not group_id or group_id in observed_groups:
                        continue
                    observed_groups.add(group_id)
                    coordinator = getattr(group, "coordinator")
                    household_id = _identifier(
                        getattr(coordinator, "household_id", None)
                    )
                    if not household_id:
                        continue
                    members = tuple(getattr(group, "members", ()) or ())
                    member_names = sorted(
                        {
                            text
                            for member in members
                            if (text := _user_text(
                                getattr(member, "player_name", None)
                            ))
                        },
                        key=str.casefold,
                    )
                    title = ", ".join(member_names) or _user_text(
                        getattr(coordinator, "player_name", None)
                    ) or "Grupa Sonos"
                    member_ids = sorted(
                        {
                            value
                            for member in members
                            if (value := _identifier(
                                getattr(member, "uid", None)
                            ))
                        }
                    )
                    state = _transport_state(coordinator)
                    state_label = _state_label(state)
                    count = len(members) or 1
                    key = (household_id, group_id)
                    targets[key] = _GroupTarget(
                        household_id,
                        group_id,
                        title,
                        group,
                    )
                    rows.append({
                        "itemId": f"sonos-local:{household_id}:{group_id}",
                        "householdId": household_id,
                        "groupId": group_id,
                        "playerIds": member_ids,
                        "title": title,
                        "detail": f"głośników: {count}, {state_label}",
                    })
                except Exception:
                    # Jeden wyłączony lub właśnie przegrupowany głośnik nie
                    # może unieważnić pozostałych poprawnie odczytanych grup.
                    continue

            if not rows:
                self._groups.clear()
                raise LocalSonosError(
                    "Wykryto Sonos, ale nie udało się odczytać jego grup. "
                    "Odczekaj chwilę i ponów wyszukiwanie."
                )
            rows.sort(key=lambda row: str(row["title"]).casefold())
            self._groups = targets
            return {
                "items": rows,
                "incomplete": False,
                "message": _count_message("grupę Sonos", "grupy Sonos", rows),
            }

    def sonos_snapshot(self, household_id: str, group_id: str) -> dict[str, object]:
        with self._gate:
            target = self._target(household_id, group_id)
            return self._snapshot(target)

    def sonos_transport(
        self,
        household_id: str,
        group_id: str,
        command: str,
        *,
        volume: int | None = None,
        seconds: float | None = None,
    ) -> dict[str, object]:
        """Wykonaj jedno polecenie na koordynatorze aktualnej grupy."""

        with self._gate:
            target = self._target(household_id, group_id)
            group = target.group
            coordinator = getattr(group, "coordinator")
            try:
                if command == "toggle":
                    if _transport_state(coordinator) in {
                        "PLAYING", "TRANSITIONING"
                    }:
                        try:
                            coordinator.pause()
                        except Exception:
                            # Radio internetowe i część materiałów Music
                            # Services nie udostępnia pauzy. Sonos przyjmuje
                            # wtedy Stop; GUI zapamiętuje świadomie wybrany
                            # materiał i po kolejnym Odtwórz uruchamia go
                            # ponownie zamiast wysyłać niedozwolone Resume.
                            coordinator.stop()
                    else:
                        coordinator.play()
                elif command == "previous":
                    coordinator.previous()
                elif command == "next":
                    coordinator.next()
                elif command == "toggleMute":
                    group.mute = not bool(group.mute)
                elif command == "setVolume" and volume is not None:
                    group.volume = max(0, min(100, int(volume)))
                elif command in {
                    "volumeUp5", "volumeDown5", "volumeUp1", "volumeDown1"
                }:
                    delta = {
                        "volumeUp5": 5,
                        "volumeDown5": -5,
                        "volumeUp1": 1,
                        "volumeDown1": -1,
                    }[command]
                    group.set_relative_volume(delta)
                elif command == "seekRelative" and seconds is not None:
                    track = _mapping(coordinator.get_current_track_info())
                    current = _time_seconds(track.get("position")) or 0.0
                    duration = _time_seconds(track.get("duration"))
                    requested = max(0.0, current + float(seconds))
                    if duration is not None:
                        requested = min(duration, requested)
                    coordinator.seek(_time_value(requested))
                else:
                    raise LocalSonosError(
                        "To polecenie sterowania Sonosem nie jest dostępne."
                    )
            except LocalSonosError:
                raise
            except Exception as error:
                raise LocalSonosError(
                    "Sonos nie przyjął polecenia. Odśwież grupy i spróbuj ponownie."
                ) from error
            self._sleep(0.12)
            return self._snapshot(target)

    @staticmethod
    def sonos_library() -> dict[str, object]:
        return {
            "items": [
                {
                    "itemId": "sonos.library.favorites",
                    "title": "Ulubione Sonos",
                    "detail": "Materiały zapisane jako ulubione w Sonos",
                    "kind": "favorites",
                },
                {
                    "itemId": "sonos.library.playlists",
                    "title": "Playlisty Sonos",
                    "detail": "Playlisty zapisane w Sonos",
                    "kind": "playlists",
                },
                {
                    "itemId": "sonos.library.ownstreams",
                    "title": "Moje stacje",
                    "detail": "Stacje zapisane w AMC",
                    "kind": "ownStreams",
                },
            ],
            "message": "Biblioteka Sonos",
        }

    def sonos_materials(self, household_id: str, kind: str) -> dict[str, object]:
        """Czytaj treści lokalnie; własne stacje nadal należą do profilu AMC."""

        with self._gate:
            if kind == "ownStream":
                payload = self._host.sonos_materials(household_id, kind)
                self._remember_own_streams(household_id, payload)
                return payload
            if kind not in {"favorite", "playlist"}:
                raise LocalSonosError("Nieznany rodzaj materiału Sonos.")

            target = self._first_household_target(household_id)
            coordinator = getattr(target.group, "coordinator")
            try:
                library = getattr(coordinator, "music_library")
                if kind == "favorite":
                    values = library.get_sonos_favorites(complete_result=True)
                else:
                    values = library.get_music_library_information(
                        "sonos_playlists", complete_result=True
                    )
            except Exception as error:
                label = "ulubionych" if kind == "favorite" else "playlist"
                raise LocalSonosError(
                    f"Nie udało się odczytać {label} z lokalnego Sonosa."
                ) from error

            for key in tuple(self._materials):
                if key[0] == household_id and key[1] == kind:
                    del self._materials[key]
            rows: list[dict[str, object]] = []
            for value in tuple(values or ()):
                material_id = _identifier(getattr(value, "item_id", None))
                title = _user_text(getattr(value, "title", None))
                if not material_id or not title:
                    continue
                self._materials[(household_id, kind, material_id)] = _Material(
                    title,
                    value,
                )
                rows.append({
                    "itemId": f"sonos-{kind}:{material_id}",
                    "materialId": material_id,
                    "title": title,
                    "kind": kind,
                })
            noun_one = "ulubiony element" if kind == "favorite" else "playlistę"
            noun_many = "ulubione elementy" if kind == "favorite" else "playlisty"
            return {
                "items": rows,
                "message": _count_message(noun_one, noun_many, rows),
            }

    def sonos_load_material(
        self,
        household_id: str,
        group_id: str,
        kind: str,
        material_id: str,
    ) -> dict[str, object]:
        """Dopisz wybrany materiał do kolejki i rozpocznij odtwarzanie."""

        with self._gate:
            target = self._target(household_id, group_id)
            material = self._material(household_id, kind, material_id)
            coordinator = getattr(target.group, "coordinator")
            try:
                if kind == "ownStream":
                    data = material.value if isinstance(material.value, dict) else {}
                    address = _user_text(data.get("streamUrl"))
                    if not address:
                        raise LocalSonosError(
                            "Nie można odnaleźć adresu tej stacji Sonos."
                        )
                    coordinator.play_uri(
                        uri=address,
                        title=material.title,
                        start=True,
                        # SoCo rozróżnia zwykły plik HTTP od strumienia
                        # radiowego. Bez tego argumentu Sonos odrzuca adresy
                        # z profilu AMC, mimo że ten sam adres działa jako
                        # Ulubione Sonos.
                        force_radio=True,
                    )
                else:
                    queueable = material.value
                    if kind == "favorite":
                        # Typowe DidlFavorite wskazuje właściwy obiekt przez
                        # ``reference``; niektóre radiowe ulubione są już
                        # bezpośrednio odtwarzalnymi obiektami DIDL.
                        queueable = getattr(queueable, "reference", queueable)
                    queue_number = coordinator.add_to_queue(queueable)
                    if (
                        not isinstance(queue_number, int)
                        or isinstance(queue_number, bool)
                        or queue_number < 1
                    ):
                        raise LocalSonosError(
                            "Sonos nie potwierdził miejsca materiału w kolejce."
                        )
                    coordinator.play_from_queue(queue_number - 1)
            except LocalSonosError:
                raise
            except Exception as error:
                raise LocalSonosError(
                    "Nie udało się uruchomić wybranego materiału na Sonosie."
                ) from error
            self._sleep(0.22)
            result = self._snapshot(target)
            result["message"] = f"Uruchamianie: {material.title}"
            return result

    def _target(self, household_id: str, group_id: str) -> _GroupTarget:
        key = (household_id, group_id)
        target = self._groups.get(key)
        if target is None:
            self.sonos_targets()
            target = self._groups.get(key)
        if target is None:
            raise LocalSonosError(
                "Wybrana grupa Sonos nie jest już dostępna. Odśwież listę grup."
            )
        return target

    def _first_household_target(self, household_id: str) -> _GroupTarget:
        target = next(
            (
                value
                for key, value in self._groups.items()
                if key[0] == household_id
            ),
            None,
        )
        if target is None:
            self.sonos_targets()
            target = next(
                (
                    value
                    for key, value in self._groups.items()
                    if key[0] == household_id
                ),
                None,
            )
        if target is None:
            raise LocalSonosError(
                "Wybrany dom Sonos nie jest już dostępny. Odśwież listę grup."
            )
        return target

    def _snapshot(self, target: _GroupTarget) -> dict[str, object]:
        coordinator = getattr(target.group, "coordinator")
        try:
            transport = _mapping(coordinator.get_current_transport_info())
            track = _mapping(coordinator.get_current_track_info())
            state = _identifier(transport.get("current_transport_state")).upper()
            playback_state = _playback_state(state)
            state_label = _state_label(state)
            title = _user_text(track.get("title")) or "Brak informacji"
            source_parts = []
            for key in ("artist", "album"):
                value = _user_text(track.get(key))
                if value and value.casefold() not in {
                    item.casefold() for item in source_parts
                }:
                    source_parts.append(value)
            if not source_parts:
                media_reader = getattr(
                    coordinator, "get_current_media_info", None
                )
                media = _mapping(
                    media_reader() if callable(media_reader) else None
                )
                channel = _user_text(media.get("channel"))
                if channel:
                    source_parts.append(channel)
            source = " — ".join(source_parts)
            volume = max(0, min(100, int(target.group.volume)))
            muted = bool(target.group.mute)
            try:
                fixed_volume = bool(coordinator.fixed_volume)
            except Exception:
                fixed_volume = None
            position = _time_seconds(track.get("position"))
            duration = _time_seconds(track.get("duration"))
        except Exception as error:
            raise LocalSonosError(
                "Nie udało się odczytać stanu wybranej grupy Sonos."
            ) from error

        position_text = _position_label(position, duration)
        volume_text = f"głośność {volume}%"
        if muted:
            volume_text += ", wyciszona"
        announcement = [title]
        if source and source.casefold() != title.casefold():
            announcement.append(source)
        announcement.extend((state_label, volume_text))
        return {
            "itemId": (
                f"sonos-local:{target.household_id}:{target.group_id}"
            ),
            "householdId": target.household_id,
            "groupId": target.group_id,
            "groupTitle": target.title,
            "title": title,
            "source": source,
            "stateText": state_label,
            "volumeText": volume_text,
            "positionText": position_text,
            "playbackState": playback_state,
            "volume": volume,
            "muted": muted,
            "fixedVolume": fixed_volume,
            "positionSeconds": position,
            "durationSeconds": duration,
            "message": ", ".join(announcement),
        }

    def _remember_own_streams(
        self, household_id: str, payload: object
    ) -> None:
        for key in tuple(self._materials):
            if key[0] == household_id and key[1] == "ownStream":
                del self._materials[key]
        if not isinstance(payload, dict) or not isinstance(payload.get("items"), list):
            return
        for value in payload["items"]:
            if not isinstance(value, dict):
                continue
            material_id = _identifier(value.get("materialId"))
            title = _user_text(value.get("title"))
            if material_id and title:
                self._materials[
                    (household_id, "ownStream", material_id)
                ] = _Material(title, dict(value))

    def _material(
        self, household_id: str, kind: str, material_id: str
    ) -> _Material:
        key = (household_id, kind, material_id)
        material = self._materials.get(key)
        if material is None:
            self.sonos_materials(household_id, kind)
            material = self._materials.get(key)
        if material is None:
            raise LocalSonosError(
                "Wybranego materiału nie ma już na liście Sonosa. Odśwież listę."
            )
        return material


def _mapping(value: object) -> dict:
    return value if isinstance(value, dict) else {}


def _identifier(value: object) -> str:
    return value.strip() if isinstance(value, str) else ""


def _user_text(value: object) -> str:
    return value.strip() if isinstance(value, str) else ""


def _transport_state(coordinator: object) -> str:
    try:
        transport = _mapping(coordinator.get_current_transport_info())
        return _identifier(transport.get("current_transport_state")).upper()
    except Exception:
        return ""


def _playback_state(value: str) -> str:
    return {
        "PLAYING": "playing",
        "TRANSITIONING": "buffering",
        "PAUSED_PLAYBACK": "paused",
        "STOPPED": "idle",
        "NO_MEDIA_PRESENT": "idle",
    }.get(value, "unknown")


def _state_label(value: str) -> str:
    return {
        "PLAYING": "odtwarzanie",
        "TRANSITIONING": "wczytywanie",
        "PAUSED_PLAYBACK": "pauza",
        "STOPPED": "zatrzymane",
        "NO_MEDIA_PRESENT": "brak materiału",
    }.get(value, "stan niedostępny")


def _time_seconds(value: object) -> float | None:
    if not isinstance(value, str) or not value.strip():
        return None
    parts = value.strip().split(":")
    if len(parts) not in {2, 3}:
        return None
    try:
        numbers = [float(part) for part in parts]
    except ValueError:
        return None
    if any(number < 0 for number in numbers):
        return None
    if len(numbers) == 2:
        return numbers[0] * 60 + numbers[1]
    return numbers[0] * 3600 + numbers[1] * 60 + numbers[2]


def _time_value(seconds: float) -> str:
    total = max(0, int(round(seconds)))
    hours, remainder = divmod(total, 3600)
    minutes, secs = divmod(remainder, 60)
    return f"{hours}:{minutes:02d}:{secs:02d}"


def _position_label(position: float | None, duration: float | None) -> str:
    if position is None:
        return ""
    current = _time_value(position)
    if duration is None:
        return current
    return f"{current} z {_time_value(duration)}"


def _count_message(one: str, many: str, rows: list[object]) -> str:
    count = len(rows)
    if count == 0:
        return "Zero elementów"
    if count == 1:
        return f"1 {one}"
    return f"{count} {many}"
