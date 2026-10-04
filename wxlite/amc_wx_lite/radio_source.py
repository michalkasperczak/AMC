"""Skad biora sie stacje Radia: z PELNEGO profilu AMC albo z prywatnej listy.

Dlaczego ten modul istnieje
---------------------------
``StateStore`` czyta prywatny klucz ``stations`` (``{id,name,url}``). Pelny
profil AMC trzyma stacje w ``radio.stations`` i uzywa ``streamUrl``, nie
``url`` -- przy pracy na wspolnym profilu Radio bylo wiec PUSTE.

Odrzucona alternatywa: jednorazowa migracja 165 stacji do wlasnego pliku.
Dalaby druga kopie, ktora od razu zaczyna sie starzec: stacja dodana pozniej
w AMC nigdy by sie nie pokazala. Dlatego w trybie wspolnego profilu czytamy
ZA KAZDYM RAZEM aktualne zrodlo, a nie zapisana migawke.

Zasady
------
* ``READ_ONLY_MIRROR`` -- zrodlem jest ``state.json`` AMC. Kolejnosc, ``id``,
  nazwy i adresy zostaja DOKLADNIE takie jak w profilu. Zapis odmowiony
  (``ProfileWriteDenied``) -- nie udajemy, ze edycja wspolnego profilu sie
  udala.
* ``PRIVATE_SANDBOX`` -- zrodlem jest prywatny ``StateStore``; zapis dozwolony.
  Dzieki temu wlasna lista stacji i Ctrl+O dzialaja jak wczesniej.

Czytamy ``state.json`` strumieniowo? Nie: plik ma ~12 MB, a ``json.load``
zajmuje ~0,1 s (zmierzone). To mniej niz jedno nacisniecie klawisza, wiec
prosty odczyt calosci jest tu wlasciwa odpowiedzia -- bez wlasnego parsera.
"""

from __future__ import annotations

import json
from dataclasses import dataclass, field
from pathlib import Path

from .profile_layout import ProfileLayout, ProfileMode, ProfileWriteDenied
from .state_store import LiteState, Station, StationList, StateStore


@dataclass(slots=True)
class RadioSnapshot:
    """Stacje gotowe dla listy + ktora jest biezaca."""

    list: StationList
    current_id: str | None = None
    #: ``True`` gdy dane przyszly z profilu AMC (wspolny profil, bez edycji).
    from_amc_profile: bool = False
    #: Prywatny stan - istnieje tylko w trybie piaskownicy, do zapisu.
    private_state: LiteState | None = field(default=None, repr=False)

    @property
    def stations(self) -> list[Station]:
        return self.list.stations

    @property
    def index_of_current(self) -> int | None:
        """Pozycja biezacej stacji. ``None`` gdy profil nie wskazuje zadnej."""
        if not self.current_id:
            return None
        for index, station in enumerate(self.stations):
            if station.id == self.current_id:
                return index
        return None

    def as_payload(self) -> list[dict]:
        return self.list.as_payload()


def stations_from_amc_state(raw: dict) -> tuple[list[Station], str | None]:
    """Wyciagnij stacje z ``radio.stations`` profilu AMC.

    Nazwy pol pochodza ze ZMIERZONEGO ``state.json`` (``schemaVersion`` 54):
    ``id`` / ``name`` / ``streamUrl``. Gdy glowny adres jest pusty, bierzemy
    ``backupStreamUrl`` -- tak jak robi to AMC, zeby wpis nie stawal sie
    niegrywalny tylko z powodu brakujacego jednego pola.

    Kolejnosci NIE zmieniamy: to kolejnosc, ktora uzytkownik zna z AMC.
    """
    radio = raw.get("radio")
    if not isinstance(radio, dict):
        return [], None

    stations: list[Station] = []
    for entry in radio.get("stations") or []:
        if not isinstance(entry, dict):
            continue
        station_id = entry.get("id")
        if not isinstance(station_id, str) or not station_id:
            continue
        url = entry.get("streamUrl") or entry.get("backupStreamUrl") or ""
        name = entry.get("name")
        stations.append(
            Station(
                id=station_id,
                name=str(name).strip() if isinstance(name, str) and name.strip() else str(url),
                url=str(url).strip(),
            )
        )

    current = radio.get("currentItemId")
    return stations, current if isinstance(current, str) and current else None


class RadioSource:
    """Czyta stacje z wlasciwego miejsca dla danego trybu profilu."""

    def __init__(self, layout: ProfileLayout) -> None:
        self.layout = layout

    # ----------------------------------------------------------- uprawnienia

    @property
    def may_edit(self) -> bool:
        """Czy wolno dodawac/zmieniac/usuwac stacje w tym trybie."""
        return self.layout.may_write_profile

    def edit_refusal_reason(self) -> str:
        """Krotkie, prawdziwe zdanie dla czytnika ekranu."""
        return (
            "Stacje naleza do AMC. W trybie wspolnego profilu lista jest "
            "tylko do odczytu, zmiany rob w AMC."
        )

    # ---------------------------------------------------------------- odczyt

    def load(self) -> RadioSnapshot:
        if self.layout.mode is ProfileMode.READ_ONLY_MIRROR:
            return self._load_from_amc()
        return self._load_private()

    def _load_from_amc(self) -> RadioSnapshot:
        path = Path(self.layout.state_json)
        try:
            raw = json.loads(path.read_text(encoding="utf-8"))
        except (FileNotFoundError, json.JSONDecodeError, OSError, UnicodeDecodeError):
            # Brak albo uszkodzony profil nie moze wywrocic okna: pusta lista
            # i tak jest uczciwsza niz zastepcze dane udajace stacje AMC.
            return RadioSnapshot(list=StationList(), from_amc_profile=True)
        if not isinstance(raw, dict):
            return RadioSnapshot(list=StationList(), from_amc_profile=True)
        stations, current = stations_from_amc_state(raw)
        return RadioSnapshot(
            list=StationList(stations), current_id=current, from_amc_profile=True
        )

    def _load_private(self) -> RadioSnapshot:
        store = StateStore(self.layout.lite_settings_dir)
        state = store.load()
        navigation = state.navigation if isinstance(state.navigation, dict) else {}
        current = navigation.get("radio_current_id")
        return RadioSnapshot(
            list=StationList(list(state.stations)),
            current_id=current if isinstance(current, str) and current else None,
            from_amc_profile=False,
            private_state=state,
        )

    # ----------------------------------------------------------------- zapis

    def save(self, snapshot: RadioSnapshot) -> None:
        """Zapisz liste stacji. We wspolnym profilu ODMAWIA, nie udaje."""
        if not self.may_edit:
            # Bramka layoutu jest zrodlem prawdy o wlasnosci pliku.
            self.layout.assert_may_write(Path(self.layout.state_json))
            raise ProfileWriteDenied(self.edit_refusal_reason())

        state = snapshot.private_state or LiteState()
        state.stations = snapshot.stations
        navigation = state.navigation if isinstance(state.navigation, dict) else {}
        if snapshot.current_id:
            navigation["radio_current_id"] = snapshot.current_id
        state.navigation = navigation
        StateStore(self.layout.lite_settings_dir).save(state)
