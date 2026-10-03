"""Zasady POSIADANIA profilu: kto czyta, kto zapisuje, co jest zakazane.

Problem do rozwiazania
----------------------
Celem etapu 3 jest JEDEN zestaw danych dla WPF i dla wxPython. Naiwne
podejscie -- "Python tez niech zapisuje state" -- dalo by dwie kopie stanu,
dwa harmonogramy i to samo nagranie uruchomione dwa razy. Osobny
``LiteStateStore`` z czesciowym importem tez nie spelnia celu, bo rozjezdza
dane.

Przyjeta zasada (najmniejsza bezpieczna zmiana)
-----------------------------------------------
* Profil AMC (``state.json``, ``library.db``, ``podcasts.db``) ma DOKLADNIE
  jednego wlasciciela zapisu: host C#. Python czyta go ``mode=ro&immutable=1``.
* Python trzyma swoje WLASNE ustawienia osobno (``amc-wx-lite/``) i nigdy nie
  dopisuje ich do profilu AMC.
* Harmonogramy, odtwarzanie nagran radiowych i aktualizator NALEZA do hosta.
  Wariant wxPython uruchamiany rownolegle MUSI miec je wylaczone, inaczej dwa
  procesy odpalilyby to samo nagranie.
* Tryb ``READ_ONLY_MIRROR`` jest jedynym trybem dopuszczonym do pracy na
  prawdziwym profilu uzytkownika.
"""

from __future__ import annotations

import os
from dataclasses import dataclass
from enum import Enum
from pathlib import Path


class ProfileMode(str, Enum):
    """Wariant uruchomienia wxPython wzgledem profilu AMC."""

    #: Czytamy prawdziwy profil, nie zapisujemy do niego NIC.
    READ_ONLY_MIRROR = "read-only-mirror"
    #: Wlasna piaskownica (fixture/testy). Zapis dozwolony, profilu nie ma.
    PRIVATE_SANDBOX = "private-sandbox"


class ProfileWriteDenied(RuntimeError):
    """Proba zapisu do profilu, ktorego wlascicielem jest host C#."""


#: Nazwy plikow nalezacych do hosta C#. Python ich NIE otwiera do zapisu.
HOST_OWNED_FILES = ("state.json", "library.db", "podcasts.db")

#: Zadania, ktorych rownoległy wariant wxPython nie wykonuje nigdy.
HOST_OWNED_DUTIES = (
    "recording-scheduler",   # harmonogramy nagran radiowych
    "component-updater",     # aktualizator skladnikow
    "state-migration",       # migracja schematu bazy
    "library-rescan",        # przeskanowanie i zapis Biblioteki
)


def default_amc_profile_dir() -> Path:
    """Katalog profilu AMC (``%APPDATA%\\AccessibleMediaController``)."""
    appdata = os.environ.get("APPDATA")
    if appdata:
        return Path(appdata) / "AccessibleMediaController"
    return Path.home() / "AppData" / "Roaming" / "AccessibleMediaController"


def default_amc_local_dir() -> Path:
    """Katalog baz AMC (``%LOCALAPPDATA%\\AccessibleMediaController``)."""
    local = os.environ.get("LOCALAPPDATA")
    if local:
        return Path(local) / "AccessibleMediaController"
    return Path.home() / "AppData" / "Local" / "AccessibleMediaController"


@dataclass(frozen=True, slots=True)
class ProfileLayout:
    """Gdzie leza dane i jakie prawa ma do nich wariant wxPython."""

    mode: ProfileMode
    library_db: Path
    podcasts_db: Path
    state_json: Path
    #: Wlasne ustawienia Pythona -- ZAWSZE poza profilem AMC.
    lite_settings_dir: Path

    @property
    def may_write_profile(self) -> bool:
        return self.mode is ProfileMode.PRIVATE_SANDBOX

    @property
    def runs_schedulers(self) -> bool:
        """Rownolegly wariant NIGDY nie prowadzi harmonogramow."""
        return False

    def assert_may_write(self, target: Path) -> None:
        """Bramka zapisu. Wolana przed KAZDA proba zapisu do profilu."""
        if self.may_write_profile:
            return
        if target.name in HOST_OWNED_FILES:
            raise ProfileWriteDenied(
                f"{target.name} nalezy do hosta AMC (C#). Wariant wxPython "
                f"pracuje w trybie {self.mode.value} i tylko czyta."
            )

    def duties_refused(self) -> tuple[str, ...]:
        return HOST_OWNED_DUTIES


def read_only_mirror(
    local_dir: Path | None = None,
    profile_dir: Path | None = None,
    lite_settings_dir: Path | None = None,
) -> ProfileLayout:
    """Prawdziwy profil AMC, wylacznie do odczytu."""
    local = Path(local_dir) if local_dir else default_amc_local_dir()
    profile = Path(profile_dir) if profile_dir else default_amc_profile_dir()
    lite = Path(lite_settings_dir) if lite_settings_dir else local.parent / "amc-wx-lite"
    return ProfileLayout(
        mode=ProfileMode.READ_ONLY_MIRROR,
        library_db=local / "library.db",
        podcasts_db=local / "podcasts.db",
        state_json=profile / "state.json",
        lite_settings_dir=lite,
    )


def private_sandbox(directory: Path | str) -> ProfileLayout:
    """Wlasna kopia (fixture). Tu zapis jest dozwolony, bo to nie profil."""
    base = Path(directory)
    return ProfileLayout(
        mode=ProfileMode.PRIVATE_SANDBOX,
        library_db=base / "library.db",
        podcasts_db=base / "podcasts.db",
        state_json=base / "state.json",
        lite_settings_dir=base / "lite-settings",
    )


def resolve_layout() -> ProfileLayout:
    """Wybor wariantu ze srodowiska. Domyslnie: tylko odczyt."""
    fixture = os.environ.get("AMC_WX_FIXTURE")
    if fixture:
        return private_sandbox(fixture)
    return read_only_mirror()
