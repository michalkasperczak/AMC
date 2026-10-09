"""Opcje sesji: zdolnosci, dziedziczenie i opis dla czytnika ekranu.

PORT ``SessionPlaybackOptionsEditor.cs`` + ``PlayerExitPausePolicy.cs``.
Zawiera WYLACZNIE logike -- zadnego wx, zeby dala sie zmierzyc bez pulpitu.

ZASADA DOBORU KONTROLEK (powod, dla ktorego ten modul w ogole jest)
-------------------------------------------------------------------
Dialog pokazuje opcje, ktore MA KTO WYKONAC w silniku tej sesji. Oryginal
liczy to w ``SessionPlaybackOptionsEditor.Describe`` (cs:18-31):

    SupportsAudioProcessing: local || podcasts
    SupportsPlaybackRate:    false            (zawsze, dla kazdej sesji)
    SupportsPlayerExitPause: known && id != "wiim"
    SupportsResumePosition:  local || podcasts || spotify

Przełożenie na port, który ma trzy sesje (``files``, ``radio`` i
``podcasts``):

* ``files`` to odpowiednik ``local``: ``LiteEngineHandlers`` woła
  ``_files.ConfigureAudioProcessing(settings)``, czyli ``WindowsMediaOutput``
  z pełnym łańcuchem DSP. Podcasty korzystają z tego samego wykonawcy, ale
  przed startem dostają własny bazowy zestaw sesji; host nakłada następnie
  ustawienia podcastu i odcinka. Obie sesje mają więc rzeczywiste DSP.
* ``radio`` NIE ma odpowiednika w ``SupportsAudioProcessing``. Potwierdza to
  sam silnik: ``LiteEngineHandlers.cs:771`` wola dla radia TYLKO
  ``_radio.ConfigureTempoAlgorithm(...)``, a ``RadioMediaOutput`` nie ma
  metody normalizacji, lagodnych przejsc ani ciszy miedzy utworami.
  Wystawienie tych trzech pol w sesji Radio dalo by MARTWE KONTROLKI --
  zapisalyby sie i nic by nie zrobily.
* Wstrzymanie po wyjsciu z odtwarzacza dziala na POZIOMIE OKNA (Escape wola
  ``transport.pauseResume``), nie w silniku, wiec maja je wszystkie sesje -- tak
  jak w oryginale, gdzie wyjatkiem jest tylko obce urzadzenie ``wiim``.
* ``SupportsPlaybackRate`` jest w oryginale ZAWSZE falszem: dialog sesji nie
  zapisuje wyboru predkosci, a algorytm tempa zostaje osobna funkcja (menu
  Dzwiek). Dlatego ten port tez go tu nie zapisuje -- dwa miejsca ustawiajace
  to samo pole silnika rozjechalyby sie przy pierwszej rozbieznosci.
* ``SupportsResumePosition`` NIE jest tu portowany: port nie ma jeszcze
  znacznika pozycji ani ``ResumePositionPolicy``. Pole bez wykonawcy to
  martwa kontrolka, wiec zostaje na nastepny etap (patrz IMPLEMENTACJA.md).

ZAKRES W SILNIKU (dlaczego protokol zostaje bez zmian)
------------------------------------------------------
``ConfigureAudio`` jednym wywołaniem ustawia wspólne wyjście plików i
podcastów. Rozdział następuje tuż przed świadomym startem: ``_play_track`` i
drogi kolejki podają bazę sesji Pliki, a ``_play_media`` bazę sesji Podcasty;
host dokłada do niej ustawienie podcastu i odcinka. Do radia trafia wyłącznie
algorytm tempa. Dlatego:

* nie dodajemy ``sessionId`` do ``audio.configure`` -- byla by to zmiana
  protokolu bez pokrycia w zachowaniu, a starsi klienci musieliby ja znac;
* ``scope_for_session`` nazywa zakres JAWNIE, a odtwarzacz wybiera bazę przed
  każdym startem zamiast pozostawiać ustawienia poprzedniej sesji.
"""

from __future__ import annotations

from dataclasses import dataclass, fields
from typing import TYPE_CHECKING

from .navigation import SessionId

if TYPE_CHECKING:  # pragma: no cover - tylko dla kontroli typow
    from .state_store import Options


#: Dozwolone dlugosci ciszy. TE SAME wartosci co
#: ``PlaybackAudioSettingsRules`` w Core i ``Options.clamp`` w tym porcie --
#: silnik odrzuca inne (``LiteAudioSettings.cs:15-16``), wiec dialog nie moze
#: ich zaproponowac.
INTER_TRACK_SILENCE_CHOICES: tuple[int, ...] = (0, 500, 1000, 2000, 3000, 5000)


#: Etykiety ciszy SLOWO W SLOWO jak ``GetInterTrackSilenceLabel``
#: (AppSettings.cs:393-401). Nie skracamy ich do "2 s": uzytkownik zna te
#: opcje ze slyszenia, a inne brzmienie to dla niego inna opcja.
_SILENCE_LABELS: dict[int, str] = {
    500: "pół sekundy",
    1000: "1 sekunda",
    2000: "2 sekundy",
    3000: "3 sekundy",
    5000: "5 sekund",
}


def inter_track_silence_label(value: int) -> str:
    """Etykieta ciszy. Port ``PlaybackAudioSettingsRules.GetInterTrackSilenceLabel``."""
    return _SILENCE_LABELS.get(value, "bez dodatkowej ciszy")


@dataclass(frozen=True, slots=True)
class SessionCapabilities:
    """Co sesja NAPRAWDE umie. Niezmienne: zdolnosc to cecha silnika."""

    supports_audio_processing: bool
    supports_playback_rate: bool
    supports_player_exit_pause: bool

    @property
    def has_options(self) -> bool:
        """Port ``SessionPlaybackCapabilities.HasOptions`` (cs:12)."""
        return self.supports_audio_processing or self.supports_player_exit_pause


#: Zdolnosci obu sesji, ktore port ma DZISIAJ. Tabela jest jawna, zeby nowa
#: sesja nie odziedziczyla po cichu cudzych opcji.
_CAPABILITIES: dict[SessionId, SessionCapabilities] = {
    SessionId.FILES: SessionCapabilities(
        # LiteEngineHandlers.cs:770 -- _files.ConfigureAudioProcessing(settings)
        supports_audio_processing=True,
        # Jak w oryginale (cs:28): dialog sesji nie zapisuje predkosci.
        supports_playback_rate=False,
        supports_player_exit_pause=True,
    ),
    SessionId.RADIO: SessionCapabilities(
        # RadioMediaOutput ma tylko ConfigureTempoAlgorithm -- brak wykonawcy.
        supports_audio_processing=False,
        supports_playback_rate=False,
        # Wstrzymanie jest na poziomie okna, wiec radio tez je ma.
        supports_player_exit_pause=True,
    ),
    SessionId.PODCASTS: SessionCapabilities(
        # Przed ``media.play`` frontend ustawia bazę tej sesji, a host nakłada
        # nadpisania podcastu i odcinka przed wywołaniem WindowsMediaOutput.
        supports_audio_processing=True,
        supports_playback_rate=False,
        supports_player_exit_pause=True,
    ),
}


def capabilities_for(session: SessionId) -> SessionCapabilities:
    """Zdolnosci TEJ sesji. Nieznana sesja nie dostaje zadnych opcji."""
    return _CAPABILITIES.get(
        session,
        SessionCapabilities(
            supports_audio_processing=False,
            supports_playback_rate=False,
            supports_player_exit_pause=False,
        ),
    )


def scope_for_session(session: SessionId) -> str:
    """Ktore wyjscie silnika nalezy do tej sesji.

    Jawna nazwa zakresu, nie "oba". Patrz uwaga o protokole w naglowku modulu.
    """
    return session.value


@dataclass(slots=True)
class SessionPlaybackOverrides:
    """Wybory UZYTKOWNIKA dla jednej sesji. ``None`` = "jak ustawienie ogolne".

    Port ``SessionPlaybackAudioOverrides`` (AppSettings.cs:230-257). Pol bez
    wykonawcy w porcie (``resume_mode``, ``output_device``, ``auto_add``,
    ``search_enter``, ``tempo_algorithm``) tu CELOWO NIE MA -- zapisywaly by
    decyzje, ktorej nikt nie wykonuje.
    """

    loudness_normalization: bool | None = None
    smooth_track_transitions: bool | None = None
    inter_track_silence_ms: int | None = None
    pause_on_player_exit: bool | None = None

    @property
    def is_empty(self) -> bool:
        """Port ``SessionPlaybackAudioOverrides.IsEmpty`` (AppSettings.cs:251)."""
        return all(getattr(self, f.name) is None for f in fields(self))

    def to_payload(self) -> dict:
        """Tylko JAWNE wybory. Pominiete pole to brak decyzji, nie falsz."""
        return {
            f.name: getattr(self, f.name)
            for f in fields(self)
            if getattr(self, f.name) is not None
        }

    @staticmethod
    def from_payload(raw: object) -> "SessionPlaybackOverrides":
        """Wczytaj wybory z zapisanego profilu.

        STRICT ``type(...) is bool``, nie ``bool(...)``: napis ``"tak"`` albo
        ``"false"`` z recznie poprawionego pliku jest prawdziwy po rzutowaniu i
        wlaczylby opcje po cichu. Obca wartosc wraca do BRAKU wyboru, czyli do
        dziedziczenia ustawienia ogolnego -- nigdy do cichego falszu.
        """
        result = SessionPlaybackOverrides()
        if not isinstance(raw, dict):
            return result
        for name in ("loudness_normalization", "smooth_track_transitions", "pause_on_player_exit"):
            value = raw.get(name)
            if type(value) is bool:
                setattr(result, name, value)
        silence = raw.get("inter_track_silence_ms")
        # Silnik odrzuca nieobslugiwana dlugosc (LiteAudioSettings.cs:15-16),
        # wiec wartosc spoza listy nie moze wejsc do stanu okna.
        if type(silence) is int and silence in INTER_TRACK_SILENCE_CHOICES:
            result.inter_track_silence_ms = silence
        return result

    def restricted_to(self, caps: SessionCapabilities) -> "SessionPlaybackOverrides":
        """Zostaw tylko wybory, ktore TA sesja umie wykonac.

        Odpowiednik ``Apply`` (cs:82-92): niewidoczne pole nie jest decyzja
        uzytkownika. Tu idziemy dalej i je ZERUJEMY, bo w porcie zapis
        prywatny nie ma skad wziac starej wartosci spoza zdolnosci sesji.
        """
        return SessionPlaybackOverrides(
            loudness_normalization=self.loudness_normalization
            if caps.supports_audio_processing
            else None,
            smooth_track_transitions=self.smooth_track_transitions
            if caps.supports_audio_processing
            else None,
            inter_track_silence_ms=self.inter_track_silence_ms
            if caps.supports_audio_processing
            else None,
            pause_on_player_exit=self.pause_on_player_exit
            if caps.supports_player_exit_pause
            else None,
        )


#: Identyfikatory opcji, ktore dialog MOZE pokazac. Test pilnuje, zeby nie
#: wjechalo tu pole bez wykonawcy (np. ``tempo_algorithm``).
SESSION_OPTION_IDS: tuple[str, ...] = (
    "loudness_normalization",
    "smooth_track_transitions",
    "inter_track_silence_ms",
    "pause_on_player_exit",
)


def resolve_audio_payload(options: "Options", overrides: SessionPlaybackOverrides) -> dict:
    """Payload ``audio.configure`` po nalozeniu wyborow sesji.

    Kolejnosc jak w ``LocalPlaybackAudioSettingsResolver``: wybor SESJI, potem
    ustawienie OGOLNE. ``tempoAlgorithm`` zostaje globalny -- to jedyne pole,
    ktore czyta takze radio (``LiteEngineHandlers.cs:771``), wiec wybor sesji
    nie moze go ruszac.
    """
    payload = options.audio_payload()
    if overrides.loudness_normalization is not None:
        payload["loudnessNormalization"] = overrides.loudness_normalization
    if overrides.smooth_track_transitions is not None:
        payload["smoothTrackTransitions"] = overrides.smooth_track_transitions
    if overrides.inter_track_silence_ms is not None:
        payload["interTrackSilenceMs"] = overrides.inter_track_silence_ms
    return payload


def effective_overrides(state, session: SessionId) -> SessionPlaybackOverrides:
    """Wybory TEJ sesji z zapisanego stanu, w postaci obiektu.

    Stan w pamieci trzyma juz obiekty, a swiezo wczytany profil moze dac
    slownik. Jedno wejscie dla obu, zeby wolacz nie musial znac tej roznicy.
    """
    raw = (getattr(state, "session_overrides", None) or {}).get(session.value)
    if isinstance(raw, SessionPlaybackOverrides):
        return raw
    return SessionPlaybackOverrides.from_payload(raw)


def engine_audio_payload(state) -> dict:
    """JEDEN efektywny payload ``audio.configure`` dla TRWALEGO stanu.

    Przy starcie nie ma jeszcze materiału, więc wspólny WindowsMediaOutput
    dostaje bazę sesji Pliki. Przed każdym późniejszym startem Plików lub
    Podcastów właściwa droga odtwarzania ustawia bazę swojej sesji. Radio
    czyta z tego wywołania tylko ``tempoAlgorithm``, którego sesja nie
    nadpisuje.
    """
    return resolve_audio_payload(
        state.options, effective_overrides(state, SessionId.FILES)
    )


def resolve_pause_on_player_exit(
    options: "Options", overrides: SessionPlaybackOverrides
) -> bool:
    """Czy Escape z odtwarzacza ma wstrzymac TE sesje.

    Port ``PlayerExitPausePolicy.ShouldPause`` (cs:34-39): wybor sesji, potem
    ustawienie ogolne. Sesja bez wlasnego wyboru zachowuje sie jak dotad.
    """
    if overrides.pause_on_player_exit is not None:
        return overrides.pause_on_player_exit
    return bool(options.pause_on_player_exit)


# --------------------------------------------------- zapis z faktycznym skutkiem


#: Nazwy sesji w mowie. Te same slowa co w menu Widok, zeby uzytkownik slyszal
#: w dialogu dokladnie to, czym przelacza sesje.
SESSION_DISPLAY_NAMES: dict[SessionId, str] = {
    SessionId.FILES: "Pliki lokalne",
    SessionId.RADIO: "Radio",
    SessionId.PODCASTS: "Podcasty i YouTube",
    SessionId.TIDAL: "TIDAL",
}


def session_display_name(session: SessionId) -> str:
    return SESSION_DISPLAY_NAMES.get(session, session.value)


def dialog_title(session: SessionId) -> str:
    """Tytul okna. Mowi ZAKRES (sesja) i KTORA sesje konfigurujemy.

    Wzor ``SessionPlaybackOptionsEditor.CreateDialog`` (cs:41): ``$"Sesja:
    {displayName}"``. Tutaj nazwa idzie w druga strone, bo samo "Sesja: Radio"
    nie mowi, ze to OPCJE -- a czytnik czyta tytul jako pierwszy.
    """
    return f"Opcje sesji: {session_display_name(session)}"


@dataclass(frozen=True, slots=True)
class ApplyResult:
    """Wynik zatwierdzenia dialogu. ``saved`` to PRAWDA tylko gdy utrwalono."""

    saved: bool
    message: str
    error: str | None = None
    #: Czy host powiedzial, ze skutek jest od NASTEPNEGO odtwarzania.
    #: Komunikat nie moze tego zjesc: biezace granie zostaje po staremu.
    applies_on_next_playback: bool = False


def restore_session_options(
    state,
    session: SessionId,
    previous: SessionPlaybackOverrides,
    *,
    client=None,
) -> None:
    """Przywroc stan TRWALY po odmowie zapisu na dysk.

    Potrzebne, bo ``apply_session_options`` zmienia RAM i silnik PRZED zapisem
    na dysk (inaczej nie dalo by sie odmowic po stronie silnika). Gdy dysk
    odmowi, RAM i silnik musza wrocic do tego, co naprawde jest w profilu --
    inaczej do zamkniecia okna gralo by ustawienie, ktorego nigdzie nie ma.

    Bledy silnika sa tu POLYKANE swiadomie: komunikat o odmowie zapisu juz
    poszedl do uzytkownika, a drugi (o nieudanym wycofaniu) tylko by go zagluszyl.
    """
    caps = capabilities_for(session)
    effective = previous.restricted_to(caps)
    if state.session_overrides is None:
        state.session_overrides = {}
    if effective.is_empty:
        state.session_overrides.pop(session.value, None)
    else:
        state.session_overrides[session.value] = effective
    if (
        client is None
        or not caps.supports_audio_processing
        or session is SessionId.PODCASTS
    ):
        return
    try:
        client.configure_audio(**resolve_audio_payload(state.options, effective))
    except Exception:  # noqa: BLE001 - patrz uwaga w docstringu
        pass


def apply_session_options(
    state,
    session: SessionId,
    overrides: SessionPlaybackOverrides,
    *,
    client=None,
) -> ApplyResult:
    """Zatwierdz Opcje sesji: ogranicz do zdolnosci, wykonaj, potem utrwal.

    KOLEJNOSC JEST ISTOTNA. Najpierw prosimy silnik, a stan zmieniamy TYLKO
    gdy przyjal. Odwrotna kolejnosc dawalaby "zapisano" po odmowie i profil
    rozjechalby sie z tym, co naprawde gra.

    Wywołujemy silnik od razu tylko dla sesji Pliki. Podcasty używają tego
    samego WindowsMediaOutput, więc ich wybory zapisujemy teraz, a stosujemy
    tuż przed następnym ``media.play``. Dzięki temu zmiana Podcastów nie
    przestawia aktualnie grającego pliku. Opcja ``pause_on_player_exit``
    działa w oknie, a Radio nie ma pól DSP, więc te przypadki nie dotykają
    silnika.
    """
    caps = capabilities_for(session)
    effective = overrides.restricted_to(caps)
    name = session_display_name(session)

    needs_engine = any(
        getattr(effective, field_name) is not None
        for field_name in (
            "loudness_normalization",
            "smooth_track_transitions",
            "inter_track_silence_ms",
        )
    )
    # Powrot do dziedziczenia TEZ jest zmiana dzwieku: silnik musi dostac
    # wartosci ogolne, inaczej dalej grałby poprzednim wyborem sesji.
    previous = (state.session_overrides or {}).get(session.value)
    if previous is not None and caps.supports_audio_processing:
        needs_engine = True

    deferred_until_playback = session is SessionId.PODCASTS and needs_engine
    if deferred_until_playback:
        needs_engine = False

    applies_next = deferred_until_playback
    if needs_engine:
        if client is None:
            return ApplyResult(
                saved=False,
                message=f"Silnik nie jest gotowy. Opcje sesji {name} bez zmian.",
                error="brak silnika",
            )
        try:
            answer = client.configure_audio(
                **resolve_audio_payload(state.options, effective)
            )
        except Exception as error:  # noqa: BLE001 - odmowa silnika to dana, nie awaria
            return ApplyResult(
                saved=False,
                message=f"Silnik odrzucił opcje sesji {name}: {error}",
                error=str(error),
            )
        # Host MOWI, od kiedy zmiana dziala (``appliesOnNextPlayback``).
        # Nie zgadujemy tego za niego.
        applies_next = bool((answer or {}).get("appliesOnNextPlayback")) if isinstance(
            answer, dict
        ) else False

    # Dopiero teraz utrwalamy. Pusty wpis USUWAMY (jak cs:93), zeby brak
    # decyzji nie zostal w profilu jako wartosc.
    if state.session_overrides is None:
        state.session_overrides = {}
    if effective.is_empty:
        state.session_overrides.pop(session.value, None)
    else:
        state.session_overrides[session.value] = effective

    return ApplyResult(
        saved=True,
        message=f"Zapisano opcje sesji {name}. "
        + describe_session_options(session, state.options, effective),
        applies_on_next_playback=applies_next,
    )


# ----------------------------------------------------------- mowa czytnika


def _choice(value: bool | None, *, yes: str, no: str) -> str:
    if value is True:
        return yes
    if value is False:
        return no
    return "według ustawienia ogólnego"


def describe_pause_mode(options: "Options", overrides: SessionPlaybackOverrides) -> str:
    """Port ``PlayerExitPausePolicy.DescribeSessionMode`` (cs:69-77).

    Wariant dziedziczony mowi WPROST, co z niego wynika, zeby uzytkownik nie
    musial sprawdzac ustawien ogolnych osobnym gestem.
    """
    if overrides.pause_on_player_exit is True:
        return "Wstrzymuj odtwarzanie po wyjściu z odtwarzacza"
    if overrides.pause_on_player_exit is False:
        return "Odtwarzaj dalej po wyjściu z odtwarzacza"
    return (
        "Jak ustawienie ogólne: wstrzymuj po wyjściu z odtwarzacza"
        if options.pause_on_player_exit
        else "Jak ustawienie ogólne: odtwarzaj dalej po wyjściu"
    )


def describe_session_options(
    session: SessionId, options: "Options", overrides: SessionPlaybackOverrides
) -> str:
    """Krotki opis stanu opcji TEJ sesji. Port ``DescribeSession`` (cs:98-132).

    Wymienia WYLACZNIE opcje, ktore sesja ma -- inaczej czytnik mowilby o
    normalizacji w radiu, ktorego radio nie ma.
    """
    caps = capabilities_for(session)
    parts: list[str] = []
    if caps.supports_audio_processing:
        parts.append(
            "normalizacja "
            + _choice(overrides.loudness_normalization, yes="włączona", no="wyłączona")
        )
        parts.append(
            "łagodne przejścia "
            + _choice(overrides.smooth_track_transitions, yes="włączone", no="wyłączone")
        )
        parts.append(
            "cisza: "
            + (
                inter_track_silence_label(overrides.inter_track_silence_ms)
                if overrides.inter_track_silence_ms is not None
                else "według ustawienia ogólnego"
            )
        )
    if caps.supports_player_exit_pause:
        parts.append(describe_pause_mode(options, overrides))
    if not parts:
        return "Ta sesja nie ma własnych opcji odtwarzania"
    return "; ".join(parts)
