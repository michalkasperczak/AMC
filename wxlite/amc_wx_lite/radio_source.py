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
    #: Krotkie zdanie o BLEDZIE odczytu, albo ``None`` gdy odczyt sie udal.
    #:
    #: Dlaczego osobne pole, a nie "pusta lista znaczy blad": profil moze
    #: naprawde nie miec stacji i wtedy nie ma o czym mowic. Dopoki oba
    #: przypadki konczyly sie ta sama pusta lista, uzytkownik niewidomy slyszal
    #: dokladnie to samo -- cisze -- gdy AMC trzymalo plik albo plik byl
    #: uszkodzony. Taki blad trzeba zglosic, bo to on wymaga reakcji.
    load_error: str | None = None
    #: ``True`` gdy pokazujemy POPRZEDNIA liste, bo odswiezenie sie nie udalo.
    kept_previous: bool = False

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
    """Wyciagnij stacje BIBLIOTEKI radia z ``radio.stations`` profilu AMC.

    Nazwy pol pochodza ze ZMIERZONEGO ``state.json`` (``schemaVersion`` 54):
    ``id`` / ``name`` / ``streamUrl``. Gdy glowny adres jest pusty, bierzemy
    ``backupStreamUrl`` -- tak jak robi to AMC, zeby wpis nie stawal sie
    niegrywalny tylko z powodu brakujacego jednego pola.

    Czlonkostwo: ``isInLibrary``
    ----------------------------
    ``radio.stations`` to TRWALY CACHE calej sesji radia, a nie lista
    Biblioteki. Czlonkostwo nosi osobna flaga ``isInLibrary``, ktorej ten
    czytnik wczesniej w ogole nie czytal -- i dlatego oddawal caly osad
    cache'u. Zmierzone na jednym odczycie glownego komputera
    (``amc_pomoc/wx-library-compare-20261006/verified-comparison.json``):
    Python 180 wpisow, widok WPF 422 ,,Wszystkie stacje'' 60.

    Filtr oryginalu (``MainWindow.xaml.cs:12833-12835``, commit ``1f5dccb6``)
    dla sesji ``radio`` -- ``UsesTidalStyleCollections`` jest tam falszywe --
    sprowadza sie DOKLADNIE do ``item.IsInLibrary == true``. Bez
    ``IsAvailable``, bez ``Kind``, bez ``DirectoryId``.

    Brak pola = POZA Biblioteka. ``RadioStationSettings.IsInLibrary``
    (``Core/Configuration/AppSettings.cs``) to ``bool`` BEZ inicjalizatora,
    czyli domyslnie ``false`` -- odwrotnie niz ``LocalMediaItemSettings``,
    gdzie stoi ``= true``. Wpis radia bez tego pola laduje wiec poza
    Biblioteka i tak samo musi go widziec Python.

    Flage czytamy jako BOOLEAN (``is True``), nie przez prawdziwosc: napis
    ``"false"`` jest w Pythonie prawdziwy i cicho przepuscilby caly cache.

    Czego ta funkcja NIE robi
    -------------------------
    * Nie usuwa ani nie zmienia ani jednego wpisu w profilu -- wpisy spoza
      Biblioteki zostaja zapisane, sa tylko NIEPOKAZYWANE, dokladnie jak
      w WPF (``RemoveSelected...``, ``MainWindow.xaml.cs:15276-15282``,
      zdejmuje flage i NIE robi ``_radioItems.Remove``).
    * Nie patrzy na ``isFavorite``. Ulubione to OSOBNY zakres; w tym porcie
      nie ma wlasnego widoku Ulubionych radia, wiec nie podstawiamy jednego
      zakresu pod drugi. Podniesienie ``IsInLibrary`` dla ulubionych robi
      ``ConfigurationStore.NormalizeRadio`` (``:873``) po stronie C#, ktory
      jest wlascicielem zapisu -- nie powielamy tego tutaj.

    Kolejnosci NIE zmieniamy: to kolejnosc, ktora uzytkownik zna z AMC.
    """
    radio = raw.get("radio")
    if not isinstance(radio, dict):
        return [], None

    stations: list[Station] = []
    for entry in radio.get("stations") or []:
        if not isinstance(entry, dict):
            continue
        if entry.get("isInLibrary") is not True:
            # Osad cache'u: wyniki katalogu Radio Browser, jednorazowe
            # strumienie i stacje kiedykolwiek zdjete z Biblioteki.
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

    def load(self, previous: RadioSnapshot | None = None) -> RadioSnapshot:
        """Wczytaj stacje. ``previous`` ratuje liste przy nieudanym odswiezeniu.

        Przy pierwszym wczytaniu ``previous`` nie ma i blad konczy sie pusta
        lista z komunikatem -- nie ma czego ratowac. Przy ODSWIEZANIU juz jest:
        165 stacji nie moze zniknac z ekranu dlatego, ze AMC akurat trzymalo
        plik na zapisie przez ulamek sekundy.
        """
        if self.layout.mode is ProfileMode.READ_ONLY_MIRROR:
            return self._load_from_amc(previous)
        return self._load_private()

    def _load_from_amc(self, previous: RadioSnapshot | None = None) -> RadioSnapshot:
        path = Path(self.layout.state_json)
        try:
            raw = json.loads(path.read_text(encoding="utf-8"))
        except FileNotFoundError:
            return self._read_failure("Nie znalazłem profilu AMC", previous)
        except json.JSONDecodeError:
            return self._read_failure("Profil AMC jest uszkodzony", previous)
        except UnicodeDecodeError:
            return self._read_failure("Nie mogę odczytać profilu AMC: złe kodowanie", previous)
        except OSError as error:
            # Najczesciej: AMC trzyma plik na zapisie, albo brak uprawnien.
            # Nie rozwijamy wyjatku do czytnika - sama nazwa klasy nic nie mowi.
            reason = getattr(error, "strerror", None) or "błąd odczytu"
            return self._read_failure(f"Nie mogę odczytać profilu AMC: {reason}", previous)
        if not isinstance(raw, dict):
            return self._read_failure("Profil AMC ma nieoczekiwaną zawartość", previous)

        stations, current = stations_from_amc_state(raw)
        # Odczyt sie udal: nawet pusta lista jest teraz PRAWDA o profilu, wiec
        # nie wskrzeszamy poprzednich stacji i gasimy komunikat bledu.
        return RadioSnapshot(
            list=StationList(stations), current_id=current, from_amc_profile=True
        )

    def _read_failure(
        self, message: str, previous: RadioSnapshot | None
    ) -> RadioSnapshot:
        """Snapshot bledu: mowi co sie stalo i nie gubi tego, co juz bylo.

        Profilu NIE zapisujemy ani nie tworzymy -- wlascicielem zapisu zostaje
        host C#, tak jak w ``ProfileLayout.assert_may_write``.
        """
        if previous is not None and previous.stations:
            return RadioSnapshot(
                list=StationList(list(previous.stations)),
                current_id=previous.current_id,
                from_amc_profile=True,
                load_error=f"{message}. Pokazuję poprzednią listę.",
                kept_previous=True,
            )
        return RadioSnapshot(
            list=StationList(), from_amc_profile=True, load_error=message
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
