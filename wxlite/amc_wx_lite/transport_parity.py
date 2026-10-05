"""Parytet transportu: kroki przewijania, format czasu, polityka komunikatow.

Po co osobny modul
------------------
``shortcuts.py`` odpowiada na pytanie "KTORY klawisz", a ten plik na pytanie
"CO ten klawisz robi i czy wolno o tym powiedziec". W dostarczonej wersji ta
druga czesc byla rozsypana po ``gui.py`` i dlatego rozjechala sie z oryginalem:
callback przewijania wolal ``format_time`` BEZWARUNKOWO (``gui.py:1855``),
a odpowiedz na pytanie o czas doklejala wlasne slowa (``gui.py:1897``).
Tutaj trzymamy to w jednym miejscu, bez wx, wiec da sie zmierzyc testem.

Wzorzec, przepisany ze zrodel (nie zgadniety)
---------------------------------------------
``src/AccessibleMediaController.Core/Commands/CommandRouter.cs``
  285-289  ``SeekBackwardCustom``/``SeekForwardCustom`` ->
           ``PlaybackSeekRules.NormalizeCustomSeekSeconds(settings.CustomSeekSeconds)``
  325-336  ``TimeElapsed``/``TimeRemaining``/``TimeTotal`` -> ``AnnounceTemplate``
           z szablonami ``{elapsed}``/``{remaining}``/``{total}``; czas pozostaly
           owinety w ``Max(TimeSpan.Zero, Duration - Position)``
  534-542  ``Seek``: mowi tylko gdy ``SeekMessages`` **i** ``ArrowSeekMessages``
  544-565  ``SeekPercent``: ``SeekMessages`` **i** ``PercentageSeekMessages``,
           tresc wedlug ``PercentageSeekAnnouncement`` (Percent/Time/PercentAndTime);
           czas trwania <= 0 -> osobne zdanie o nieznanym czasie
  566-575  ``Volume``: ``SeekMessages`` **i** ``VolumeMessages``; wyciszenie ma
           wlasne zdanie, reszta idzie szablonem ``volume.changed`` = ``{value}%``
  600-604  ``FormatPlaybackRate``
  678-687  ``AnnounceTemplate``: szablon z ustawien ma pierwszenstwo nad
           domyslnym, a szablon PUSTY oznacza CISZE (``return``)
  689-692  ``FormatTime`` = ``MediaItemFormatter.FormatDuration``

``src/AccessibleMediaController.Core/Presentation/MediaItemFormatter.cs:69-73``
  ``h:mm:ss`` od godziny w gore, inaczej ``m:ss``.

``src/AccessibleMediaController.Core/Configuration/AppSettings.cs``
  280-295  ``CustomSeekSeconds``: domyslnie 300, zakres 5..1800
  539-578  ``MessageSettings`` -- domyslne wartosci przelacznikow i szablonow

``src/AccessibleMediaController.Core/Sessions/DemoMediaSession.cs``
  11       drabina predkosci ``[0.50 .. 2.00]``
  364-385  ``ChangePlaybackRate``/``SetPlaybackRate``

``src/AccessibleMediaController.Windows/MainWindow.xaml.cs``
  659-664  ``Messages.Enabled`` wylaczone => tekst idzie do pola statusu, ale
           NIE jest wymawiany (to wylacznik MOWY, nie wylacznik tresci)
  5898-5906 ``ToggleSeekMessages`` i jego dwa zdania, doslownie

Czego tu NIE ma, swiadomie
--------------------------
* Zadnego zapisu do ``state.json``. W trybie ``READ_ONLY_MIRROR`` wlascicielem
  jest host C# (``profile_layout.py``), wiec ``Ctrl+Shift+G`` mowi prawde
  ("zmien w AMC") zamiast udawac zapis albo milczec.
* Zadnego dotykania silnika audio ani czytnika ekranu. Czy komunikat ZOSTAL
  wypowiedziany, sprawdza zywa proba NVDA u rodzica; tutaj odpowiadamy tylko na
  pytanie, czy wolno go wypowiedziec i jak ma brzmiec.
"""

from __future__ import annotations

import json
from dataclasses import dataclass, field
from enum import Enum
from typing import Mapping

from .profile_layout import ProfileLayout
from .shortcuts import Action

# --------------------------------------------------------------- kroki czasu

#: ``AppSettings.cs:280`` -- ``PlaybackSeekRules.DefaultCustomSeekSeconds``.
CUSTOM_SEEK_DEFAULT_SECONDS = 300
#: ``AppSettings.cs:282``
CUSTOM_SEEK_MIN_SECONDS = 5
#: ``AppSettings.cs:285``
CUSTOM_SEEK_MAX_SECONDS = 1800
#: ``CommandRouter.cs:321`` -- End celuje w ``Duration - 10s``, nie na 100%.
#: Liczba jest w kodzie oryginalu wpisana na stale (``TimeSpan.FromSeconds(10)``),
#: nie pochodzi z ustawien.
TRACK_END_MARGIN_SECONDS = 10

#: ``AppSettings.cs:291`` -- gotowe propozycje w oknie ustawien.
SUGGESTED_CUSTOM_SEEK_SECONDS = (15, 30, 60, 120, 300, 600)

#: Krok w sekundach dla kazdej komendy przewijania oryginalu.
#: ``MainWindow.xaml.cs:21583-21590`` / ``22387-22394`` wiaza z nimi klawisze,
#: a ``CommandRouter.cs:271-289`` zamienia komende na liczbe sekund.
SEEK_STEPS: dict[Action, int] = {
    Action.SEEK_BACK_10: -10,
    Action.SEEK_FORWARD_10: 10,
    Action.SEEK_BACK_30: -30,
    Action.SEEK_FORWARD_30: 30,
    Action.SEEK_BACK_60: -60,
    Action.SEEK_FORWARD_60: 60,
}

#: Komendy, ktorych krok bierze sie z USTAWIEN, nie ze stalej.
CUSTOM_SEEK_ACTIONS: dict[Action, int] = {
    Action.SEEK_BACK_CUSTOM: -1,
    Action.SEEK_FORWARD_CUSTOM: 1,
}


def normalize_custom_seek_seconds(seconds: int | None) -> int:
    """``AppSettings.cs:294-295`` -- ``Math.Clamp(seconds, 5, 1800)``."""
    if seconds is None:
        return CUSTOM_SEEK_DEFAULT_SECONDS
    try:
        value = int(seconds)
    except (TypeError, ValueError):
        return CUSTOM_SEEK_DEFAULT_SECONDS
    return max(CUSTOM_SEEK_MIN_SECONDS, min(CUSTOM_SEEK_MAX_SECONDS, value))


def seek_step_seconds(action: Action | None, *, custom_seconds: int) -> int | None:
    """Ile sekund przesuwa dana komenda. ``None`` = to nie jest przewijanie.

    Kierunek i wielkosc pochodza z NAZWY komendy C#, zeby nie dalo sie znowu
    przypisac ``Shift`` szescdziesieciu sekund "z pamieci".
    """
    if action is None:
        return None
    if action in SEEK_STEPS:
        return SEEK_STEPS[action]
    sign = CUSTOM_SEEK_ACTIONS.get(action)
    if sign is None:
        return None
    return sign * normalize_custom_seek_seconds(custom_seconds)


#: ``CommandIds.SeekPercent(digit * 10)`` -- ``MainWindow.xaml.cs:21557``.
SEEK_PERCENT_PREFIX = "seek.percent."


def seek_percent_action(percent: int) -> Action | None:
    """Komenda skoku procentowego dla 0..90 (wielokrotnosci 10)."""
    try:
        return Action(f"{SEEK_PERCENT_PREFIX}{int(percent)}")
    except ValueError:
        return None


def seek_percent_value(action: Action | None) -> int | None:
    """Procent zakodowany w komendzie albo ``None``, gdy to inna komenda."""
    if action is None or not action.value.startswith(SEEK_PERCENT_PREFIX):
        return None
    return int(action.value[len(SEEK_PERCENT_PREFIX) :])


# --------------------------------------------------------------- format czasu


def format_clock(seconds: float | None) -> str:
    """``MediaItemFormatter.FormatDuration`` (``cs:69-73``), slowo w slowo.

    ``h:mm:ss`` od godziny w gore, inaczej ``m:ss``. Czas NIEZNANY to brak
    danych, nie zero -- mowimy "nieznany", bo "0:00" byloby nieprawda.
    """
    if seconds is None:
        return "nieznany"
    try:
        total = int(float(seconds))
    except (TypeError, ValueError):
        return "nieznany"
    if total < 0:
        return "nieznany"
    hours, rest = divmod(total, 3600)
    minutes, secs = divmod(rest, 60)
    if hours >= 1:
        return f"{hours}:{minutes:02d}:{secs:02d}"
    return f"{minutes}:{secs:02d}"


def format_playback_rate(rate: float) -> str:
    """``CommandRouter.cs:600-604``.

    Kultura polska ma przecinek dziesietny, a ``"0.##"`` obcina zera na koncu:
    ``1,25``, ale ``2`` -- nie ``2,00``.
    """
    if abs(rate - 1.0) < 0.001:
        return "Prędkość normalna"
    text = f"{rate:.2f}".rstrip("0").rstrip(".").replace(".", ",")
    return f"Prędkość {text} razy"


# ----------------------------------------------------------------- predkosc

#: ``DemoMediaSession.cs:11``. Drabina SIEDMIU wartosci, nie suwak procentowy.
PLAYBACK_RATES: tuple[float, ...] = (0.50, 0.75, 1.00, 1.25, 1.50, 1.75, 2.00)

#: ``MainWindow.xaml.cs:6827-6829`` -- menu "Prędkość odtwarzania".
PLAYBACK_RATE_ACTIONS = (Action.RATE_DOWN, Action.RATE_UP, Action.RATE_RESET)


def next_playback_rate(current: float, direction: int) -> float:
    """``DemoMediaSession.ChangePlaybackRate`` (``cs:364-376``).

    Uwaga na kolejnosc: gdy biezacej wartosci NIE MA na drabinie, C# bierze
    ``FindLastIndex(rate < current)`` i DOPIERO potem dodaje kierunek. Dla 1,10
    w dol daje to 0,75, nie 1,00. Przepisujemy zachowanie oryginalu, a nie
    wersje, ktora wydaje sie rozsadniejsza -- inaczej te same klawisze robily
    by w dwoch programach dwie rozne rzeczy.
    """
    if direction == 0:
        return clamp_playback_rate(current)
    index = next(
        (i for i, rate in enumerate(PLAYBACK_RATES) if abs(rate - current) < 0.001),
        -1,
    )
    if index < 0:
        lower = [i for i, rate in enumerate(PLAYBACK_RATES) if rate < current]
        index = lower[-1] if lower else -1
    step = 1 if direction > 0 else -1
    target = max(0, min(len(PLAYBACK_RATES) - 1, index + step))
    return PLAYBACK_RATES[target]


def clamp_playback_rate(rate: float) -> float:
    """``DemoMediaSession.SetPlaybackRate`` (``cs:381``).

    C# nie przycina do przedzialu -- robi ``MinBy(|rate - x|)``, czyli
    PRZYCIAGA do najblizszego szczebla drabiny. 1,10 staje sie 1,00, a 5,0
    staje sie 2,00. Suwak musi zachowac sie tak samo, bo inaczej pokazywalby
    wartosc, ktorej silnik nie ustawi.
    """
    try:
        value = float(rate)
    except (TypeError, ValueError):
        return 1.0
    return min(PLAYBACK_RATES, key=lambda candidate: abs(candidate - value))


#: Granice drabiny -- suwak w oknie ma pokrywac DOKLADNIE ten zakres.
PLAYBACK_RATE_MIN = PLAYBACK_RATES[0]
PLAYBACK_RATE_MAX = PLAYBACK_RATES[-1]


# ------------------------------------------------------- polityka komunikatow


class PercentAnnouncement(str, Enum):
    """``PercentageSeekAnnouncementMode`` (``AppSettings.cs:13-18``)."""

    PERCENT = "Percent"
    TIME = "Time"
    PERCENT_AND_TIME = "PercentAndTime"

    @classmethod
    def parse(cls, raw: object) -> "PercentAnnouncement":
        """``System.Text.Json`` czyta enum po nazwie, bez wzgledu na wielkosc
        liter. Nieznana wartosc nie moze wywrocic okna -- wraca domyslna."""
        if isinstance(raw, cls):
            return raw
        if isinstance(raw, int) and 0 <= raw < len(cls.__members__):
            return list(cls)[raw]
        if isinstance(raw, str):
            for member in cls:
                if member.value.casefold() == raw.strip().casefold():
                    return member
        return cls.PERCENT


#: Domyslne szablony oryginalu (``MessageSettings.CreateDefault``,
#: ``AppSettings.cs:561-576``) -- tylko te, ktore dotycza transportu.
DEFAULT_TEMPLATES: dict[str, str] = {
    "time.elapsed": "{elapsed}",
    "time.remaining": "{remaining}",
    "time.total": "{total}",
    "volume.changed": "{value}%",
}

#: Komenda pytania o czas -> (klucz szablonu, nazwa pola).
TIME_TEMPLATES: dict[Action, tuple[str, str]] = {
    Action.TIME_ELAPSED: ("time.elapsed", "elapsed"),
    Action.TIME_REMAINING: ("time.remaining", "remaining"),
    Action.TIME_TOTAL: ("time.total", "total"),
}


@dataclass(frozen=True, slots=True)
class ToggleResult:
    """Wynik ``Ctrl+Shift+G``: czy stan SIE ZMIENIL i co powiedziec.

    ``changed=False`` z wyjasniajacym zdaniem to NIE to samo, co cisza.
    Uzytkownik musi wiedziec, ze opcja ma wlasciciela, a nie zgadywac, czy
    klawisz dziala.
    """

    changed: bool
    message: str


@dataclass
class MessagePolicy:
    """Przelaczniki komunikatow oryginalu i wynikajaca z nich tresc.

    Domyslne wartosci sa TE SAME, co ``MessageSettings`` (``cs:541-555``), zeby
    brak pliku nie zmienial zachowania programu.
    """

    enabled: bool = True
    seek_messages: bool = True
    arrow_seek_messages: bool = True
    percentage_seek_messages: bool = True
    volume_messages: bool = True
    playback_messages: bool = True
    bookmark_navigation_messages: bool = True
    percent_announcement: PercentAnnouncement = PercentAnnouncement.PERCENT
    templates: Mapping[str, str] = field(default_factory=dict)
    custom_seek_seconds: int = CUSTOM_SEEK_DEFAULT_SECONDS
    #: Czy wolno zapisac zmiane przelacznika (tylko wlasna piaskownica).
    may_persist: bool = False

    # ------------------------------------------------------------- bramki

    @property
    def speaks_routine(self) -> bool:
        """``MainWindow.xaml.cs:659-664``: ``Messages.Enabled`` to wylacznik
        MOWY. Tekst nadal powstaje i trafia do pola statusu."""
        return self.enabled

    @property
    def announces_arrow_seek(self) -> bool:
        """``CommandRouter.cs:537`` -- KONIUNKCJA dwoch przelacznikow.

        To jest dokladnie ta bramka, ktorej brak slyszal Michal: dostarczony
        ``gui.py:1855`` mowil czas przy kazdej strzalce.
        """
        return self.seek_messages and self.arrow_seek_messages

    @property
    def announces_percent_seek(self) -> bool:
        """``CommandRouter.cs:555`` -- OSOBNA rodzina, wlasny przelacznik."""
        return self.seek_messages and self.percentage_seek_messages

    @property
    def announces_volume(self) -> bool:
        """``CommandRouter.cs:570``."""
        return self.seek_messages and self.volume_messages

    # ------------------------------------------------------------- tresci

    def template(self, key: str) -> str | None:
        """``AnnounceTemplate`` (``cs:678-681``): ustawienia > domyslne, a
        szablon PUSTY to swiadoma cisza, nie powod do podstawienia domyslnego.
        """
        for source in (self.templates, DEFAULT_TEMPLATES):
            if key in source:
                raw = source[key]
                return raw if raw else None
        return None

    def render(self, key: str, **values: str) -> str | None:
        """Podstawienie ``{pole}`` bez wzgledu na wielkosc liter -- C# uzywa
        ``StringComparison.OrdinalIgnoreCase`` (``cs:684``)."""
        template = self.template(key)
        if template is None:
            return None
        out = template
        for name, value in values.items():
            for variant in (f"{{{name}}}", f"{{{name.upper()}}}", f"{{{name.capitalize()}}}"):
                out = out.replace(variant, value)
        return out

    def arrow_seek_text(self, position_seconds: float | None) -> str | None:
        """``Seek`` (``cs:534-542``): sam czas, i tylko gdy obie bramki otwarte."""
        if not self.announces_arrow_seek:
            return None
        return format_clock(position_seconds)

    def percent_text(self, percent: int, *, position_seconds: float | None) -> str:
        """``SeekPercent`` (``cs:556-561``). Trzy tryby, dokladnie te trzy."""
        if self.percent_announcement is PercentAnnouncement.TIME:
            return format_clock(position_seconds)
        if self.percent_announcement is PercentAnnouncement.PERCENT_AND_TIME:
            return f"{percent}%, {format_clock(position_seconds)}"
        return f"{percent}%"

    def percent_seek_text(
        self, percent: int, *, position_seconds: float | None, duration_seconds: float | None
    ) -> str | None:
        """Cala sciezka skoku procentowego, razem ze zdaniem o braku czasu.

        ``cs:546-551``: gdy czas trwania nie jest znany, oryginal mowi o tym
        ZAWSZE -- to blad wykonania, nie rutynowy komunikat, wiec nie podlega
        przelacznikom.
        """
        if duration_seconds is None or duration_seconds <= 0:
            return "Skok procentowy niedostępny: czas trwania jest nieznany"
        if not self.announces_percent_seek:
            return None
        return self.percent_text(percent, position_seconds=position_seconds)

    def volume_text(self, value: int, *, muted: bool = False) -> str | None:
        """``Volume`` (``cs:567-575``). Wyciszenie ma zdanie POZA szablonem.

        Bramka jest TUTAJ, a nie u wolajacego: ``cs:570`` sprawdza
        ``SeekMessages && VolumeMessages`` przed jakimkolwiek tekstem.
        """
        if not self.announces_volume:
            return None
        if muted:
            return f"{value}%, wyciszono"
        return self.render("volume.changed", value=str(value))

    # -------------------------------------------------------- przelacznik

    def toggle_seek_messages(self) -> ToggleResult:
        """``ToggleSeekMessages`` (``MainWindow.xaml.cs:5898-5906``).

        We wspolnym profilu wlascicielem ``state.json`` jest host C#, wiec
        zamiast cichego zignorowania gestu mowimy, gdzie te opcje zmienic.
        Martwe pole byloby gorsze od braku pola.
        """
        if not self.may_persist:
            return ToggleResult(
                changed=False,
                message=(
                    "Automatycznymi komunikatami odtwarzacza zarządza AMC. "
                    "Zmień je w ustawieniach AMC."
                ),
            )
        self.seek_messages = not self.seek_messages
        return ToggleResult(
            changed=True,
            message=(
                "Automatyczne komunikaty odtwarzacza włączone"
                if self.seek_messages
                else "Automatyczne komunikaty odtwarzacza wyłączone"
            ),
        )


def time_announcement(
    action: Action,
    policy: MessagePolicy,
    *,
    position: float | None,
    duration: float | None,
) -> str | None:
    """Odpowiedz na ``Ctrl+Shift+E/R/T`` (``CommandRouter.cs:325-336``).

    Dwie rzeczy, w ktorych dostarczona wersja mijala sie z oryginalem:

    * Tresc to SAM CZAS. Szablony ``{elapsed}``/``{remaining}``/``{total}`` nie
      maja zadnych dodatkowych slow, a release .383 cytuje wprost "3:51".
      Doklejanie "Minelo"/"Pozostalo"/"Calosc" bylo naszym wymyslem.
    * Pytanie zadane RECZNIE dostaje odpowiedz nawet przy wylaczonych
      komunikatach -- ``AnnounceTemplate`` nie pyta tu o ``SeekMessages``.
      Bramka ``SeekMessages``/``ArrowSeekMessages`` dotyczy zapowiedzi
      AUTOMATYCZNYCH przy przewijaniu.

    Wylacznik ``Messages.Enabled`` tego tekstu NIE kasuje: w C# ta sama tresc
    idzie wtedy do pola statusu (``MainWindow.xaml.cs:659-663``), wiec o mowie
    decyduje wolajacy przez ``policy.speaks_routine``.
    """
    entry = TIME_TEMPLATES.get(action)
    if entry is None:
        return None
    key, placeholder = entry

    if action is Action.TIME_ELAPSED:
        value = format_clock(position)
    elif action is Action.TIME_TOTAL:
        value = format_clock(duration)
    else:
        if duration is None:
            # Czasu pozostalego NIE da sie policzyc bez czasu calkowitego.
            # Oryginal liczy go z ``CurrentItem.Duration``; gdy tej liczby nie
            # mamy, nie wymyslamy jej -- milczymy, zamiast podac zera.
            return None
        value = format_clock(max(0.0, float(duration) - float(position or 0.0)))

    return policy.render(key, **{placeholder: value})


# ------------------------------------------- czytanie polityki z profilu AMC


def load_message_policy(layout: ProfileLayout) -> MessagePolicy:
    """Polityka z PRAWDZIWEGO ``state.json`` AMC. Tylko odczyt.

    ``ConfigurationStore.cs:30-35`` zapisuje ``camelCase``, wiec czytamy
    ``settings.messages`` i ``settings.customSeekSeconds`` tak, jak je zapisal
    host. Zadna galaz tej funkcji nie otwiera pliku do zapisu -- jeden pisarz
    zostaje jeden (``profile_layout.py``).

    Plik uszkodzony albo nieobecny daje domysly oryginalu. Transport musi
    dzialac takze wtedy, a cisza "bo nie udalo sie wczytac ustawien" byla by
    dla uzytkownika czytnika gorsza od zlej wartosci.
    """
    data: dict = {}
    try:
        raw = layout.state_json.read_text(encoding="utf-8")
        parsed = json.loads(raw)
        if isinstance(parsed, dict):
            settings = parsed.get("settings")
            if isinstance(settings, dict):
                data = settings
    except (OSError, ValueError):
        data = {}

    messages = data.get("messages")
    if not isinstance(messages, dict):
        messages = {}

    templates = messages.get("templates")
    if not isinstance(templates, dict):
        templates = {}
    # Klucze szablonow sa w C# nieczule na wielkosc liter (``cs:555``).
    normalized = {
        str(key).casefold(): str(value)
        for key, value in templates.items()
        if isinstance(value, str)
    }

    def flag(name: str, default: bool = True) -> bool:
        value = messages.get(name)
        return bool(value) if isinstance(value, bool) else default

    return MessagePolicy(
        enabled=flag("enabled"),
        seek_messages=flag("seekMessages"),
        arrow_seek_messages=flag("arrowSeekMessages"),
        percentage_seek_messages=flag("percentageSeekMessages"),
        volume_messages=flag("volumeMessages"),
        playback_messages=flag("playbackMessages"),
        bookmark_navigation_messages=flag("bookmarkNavigationMessages"),
        percent_announcement=PercentAnnouncement.parse(
            messages.get("percentageSeekAnnouncement")
        ),
        templates=normalized,
        custom_seek_seconds=normalize_custom_seek_seconds(data.get("customSeekSeconds")),
        may_persist=layout.may_write_profile,
    )
