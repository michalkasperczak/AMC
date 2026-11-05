"""LEWA STRZALKA na liscie: krotka informacja uzupelniajaca.

Przenosi do portu wx droge, ktora w pelnym AMC obsluguje
``MediaList_PreviewKeyDown`` (``MainWindow.xaml.cs:23206-23216``):

    if (Keyboard.Modifiers == ModifierKeys.None
        && SelectedItem is { } item
        && key == Key.Left)
    {
        if ((MediaList.SelectedItem as MediaItemRow)?.FolderPath is { } folderPath)
            Announce($"{item.Title}: {folderPath}");
        else
            AnnounceQuickMediaInformation(item);
        e.Handled = true;
    }

PODZIAL ODPOWIEDZIALNOSCI (swiadomy, nie przypadkowy)
-----------------------------------------------------
Ten modul NIE formatuje komunikatu o parametrach. Napis sklada
``QuickMediaInformationFormatter.Format`` (``Core/Presentation``) razem z
``AudioParametersFormatter.FormatCompact`` -- ten sam kod C#, ktorego uzywa
pelne AMC. Tutaj jest tylko:

  * decyzja, CZY klawisz nalezy do nas (resolver w ``shortcuts``),
  * rozpoznanie wiersza, ktory ma INNA informacje (folder, "zaladuj wiecej"),
  * ZADANIE do hosta -- dane, nie napis,
  * BRAMKA WYNIKU, czyli port ``requestVersion`` + ``ActionItem?.Id``
    z ``AnnounceQuickMediaInformation`` (cs:5485-5541).

Surowe zapisane parametry wybranego źródła czyta ``read_cached_information``
poza wątkiem GUI. Brakujące dane uzupełnia silnik C#, a wspólny formatter Core
odpowiada za jednostki i kolejność. Python nie kopiuje tego formatowania.

TRWALOSC: oryginal po zmierzeniu parametrow ZAPISUJE je do profilu
(``TrySaveLocalMediaState`` / ``CaptureRadioState`` + ``QueueStateSave``).
Port wx ma profil AMC w trybie TYLKO DO ODCZYTU -- wlascicielem zapisu zostaje
host C#. Dlatego ta droga NICZEGO nie zapisuje i nie udaje, ze zapisuje.
"""

from __future__ import annotations

import json
import ntpath
import sqlite3
import threading
import time
from collections import OrderedDict
from dataclasses import dataclass
from typing import Callable

from .library_db import LibraryDatabase, normalize_path
from .list_model import Row
from .profile_layout import ProfileLayout

#: Rodzaje wierszy, ktore sa FOLDEREM, a nie elementem multimedialnym.
#: ``MediaItemRow.FolderPath`` oryginalu jest niepuste wlasnie dla nich, a
#: ``Row.is_openable`` obejmuje tez ``playlist`` -- playlista NIE jest folderem
#: na dysku i nie ma sciezki do przeczytania.
FOLDER_KINDS = frozenset({"folder", "parent"})

#: Wiersz "zaladuj wiecej odcinkow" (cs:23190-23197). Lista Podcastow w tym
#: porcie NIE ISTNIEJE; nazwa rodzaju jest tu po to, zeby jej przyszly port
#: poszedl TA SAMA droga.
LOAD_MORE_PODCAST_KIND = "loadMorePodcast"
LOAD_MORE_PODCAST_MESSAGE = "Załaduj więcej odcinków. Naciśnij Enter"

#: Typ MIME odcinka podcastu idzie do hosta BEZ TLUMACZENIA. Tablice
#: ``audio/mpeg -> MP3`` trzyma ``LiteQuickInformation.FormatPodcastCodec``
#: (port ``MainWindow.FormatPodcastCodec``, cs:5413-5423). Kopia tej tablicy w
#: Pythonie byla by DRUGIM zrodlem prawdy i przy kazdym nowym formacie
#: rozjechala by sie z AMC po cichu.

#: Operacja protokolu. Host sklada odpowiedz PRAWDZIWYM formatterem C#.
QUICK_INFO_OP = "media.quickInformation"

#: Nazwa strumienia dla ``StaleResultGate``. Nowe pytanie uniewaznia poprzednie
#: -- odpowiednik ``Interlocked.Increment(ref _quickInformationRequestVersion)``.
QUICK_INFO_STREAM = "quickInfo"

#: Komunikat, gdy wiersz nie ma zadnego zrodla do zmierzenia. KROTKI i
#: UCZCIWY: nie podaje zerowego rozmiaru ani zerowego bitrate.
NO_SOURCE_MESSAGE = "Brak zapisanych informacji uzupełniających"


class QuickInfoMemoryCache:
    """Krótki cache wyniku pomiaru i bramka powtórzonego zapytania.

    Pomiar lokalnego pliku może potrwać do pięciu sekund. Powtarzanie lewej
    strzałki nie może uruchamiać kolejnych takich samych pomiarów, bo każdy z
    nich zajmuje osobny wątek i kolejkę hosta. Cache jest wyłącznie procesowy:
    niczego nie zapisuje do profilu, a po krótkim czasie ponownie pyta silnik.
    """

    def __init__(
        self,
        *,
        max_entries: int = 128,
        local_ttl_seconds: float = 300.0,
        remote_ttl_seconds: float = 15.0,
        clock: Callable[[], float] = time.monotonic,
    ) -> None:
        self._max_entries = max(1, int(max_entries))
        self._local_ttl = max(0.0, float(local_ttl_seconds))
        self._remote_ttl = max(0.0, float(remote_ttl_seconds))
        self._clock = clock
        self._entries: OrderedDict[tuple[object, ...], tuple[float, dict]] = OrderedDict()
        self._inflight: set[tuple[object, ...]] = set()
        self._lock = threading.Lock()

    @staticmethod
    def _key(request: dict) -> tuple[object, ...]:
        """Tożsamość danych wejściowych bez gotowego tekstu dostępnościowego."""
        names = (
            "session", "itemId", "source", "kind", "durationTicks",
            "podcastMediaLength", "podcastMediaType", "bitrateKbps",
            "sampleRateHz", "isBitrateEstimated", "codec", "artist",
            "country", "language",
        )
        return (*tuple(request.get(name) for name in names),
                tuple(request.get("fields") or ()))

    def get(self, request: dict) -> dict | None:
        key = self._key(request)
        now = self._clock()
        with self._lock:
            entry = self._entries.get(key)
            if entry is None:
                return None
            expires_at, payload = entry
            if expires_at <= now:
                self._entries.pop(key, None)
                return None
            self._entries.move_to_end(key)
            return dict(payload)

    def begin(self, request: dict) -> bool:
        """Zwróć ``True`` tylko pierwszemu równoległemu pomiarowi tego celu."""
        key = self._key(request)
        with self._lock:
            if key in self._inflight:
                return False
            self._inflight.add(key)
            return True

    def finish(self, request: dict, payload: dict | None = None) -> None:
        """Zakończ pomiar; poprawną odpowiedź zachowaj w ograniczonym cache."""
        key = self._key(request)
        now = self._clock()
        with self._lock:
            self._inflight.discard(key)
            if payload is None:
                return
            ttl = self._local_ttl if request.get("session") == "files" else self._remote_ttl
            if ttl <= 0.0:
                return
            self._entries[key] = (now + ttl, dict(payload))
            self._entries.move_to_end(key)
            while len(self._entries) > self._max_entries:
                self._entries.popitem(last=False)


def local_media_folder_detail(
    row: Row | None,
    *,
    session: str,
    library_view: str | None,
) -> str | None:
    """Folder tylko dla lokalnego pliku w Historii lub Historii nagrywania.

    Zdalny adres nie staje sie udawanym folderem. Zwracamy sama informacje
    uzupelniajaca, bez tytulu wiersza, bo ten wlasnie przeczytala lista.
    """
    if (
        row is None
        or session != "files"
        or library_view not in {"history", "recordedRadioFiles"}
    ):
        return None
    path = (row.path or "").strip()
    if not path:
        return None
    is_windows_path = path.startswith("\\\\") or (
        len(path) >= 3
        and path[0].isalpha()
        and path[1] == ":"
        and path[2] in "\\/"
    )
    if not is_windows_path:
        return None
    directory = ntpath.dirname(path.rstrip("\\/"))
    if not directory:
        return None
    name = ntpath.basename(directory.rstrip("\\/")) or directory
    return f"folder {name}" if name else None


def read_cached_information(layout: ProfileLayout, row: Row, session: str) -> dict:
    """Czytaj dane wskazanego źródła bez zapisu i bez hydratacji pliku w chmurze."""
    saved = None
    if session == "radio":
        try:
            state = json.loads(layout.state_json.read_text(encoding="utf-8-sig"))
        except (OSError, ValueError):
            return {}
        radio = state.get("radio", {}) if isinstance(state, dict) else {}
        stations = radio.get("stations", []) if isinstance(radio, dict) else []
        if not isinstance(stations, list):
            return {}
        for station in stations:
            if not isinstance(station, dict) or str(station.get("id", "")) != row.item_id:
                continue
            if row.address not in (station.get("streamUrl"), station.get("backupStreamUrl")):
                return {}
            saved = station
            break
        names = {name: name for name in (
            "bitrateKbps", "sampleRateHz", "codec", "country", "language", "isBitrateEstimated"
        )}
    elif session == "files" and row.path:
        try:
            with LibraryDatabase(layout.library_db) as db:
                record = db.connection.execute(
                    "SELECT * FROM local_items WHERE id = ?", (row.item_id,)
                ).fetchone()
                if record is None:
                    record = db.connection.execute(
                        "SELECT * FROM local_items WHERE path = ? COLLATE NOCASE LIMIT 1",
                        (normalize_path(row.path),),
                    ).fetchone()
                saved = dict(record) if record is not None else None
        except (OSError, sqlite3.Error):
            return {}
        if saved and normalize_path(str(saved.get("path", ""))).casefold() != normalize_path(row.path).casefold():
            return {}
        names = {"duration_ticks": "durationTicks", "bitrate_kbps": "bitrateKbps",
                 "sample_rate_hz": "sampleRateHz", "bitrate_estimated": "isBitrateEstimated"}
    else:
        return {}
    if saved is None:
        return {}
    result = {}
    for source, target in names.items():
        value = saved.get(source)
        if target == "isBitrateEstimated":
            if value is True or value == 1:
                result[target] = True
        elif target in ("durationTicks", "bitrateKbps", "sampleRateHz"):
            if isinstance(value, int) and not isinstance(value, bool) and value > 0:
                result[target] = value
        elif isinstance(value, str) and value.strip():
            result[target] = value.strip()
    return result


def folder_announcement(row: Row) -> str | None:
    """``$"{item.Title}: {folderPath}"`` (cs:23211) albo ``None``.

    Folder dostaje INNA informacje niz plik: sciezke, nie parametry audio.
    Brak sciezki (korzen zrodel ma ``path=None``) oddajemy jako ``None``,
    zeby nie przeczytac ogona ``": "`` bez tresci.
    """
    if row.kind not in FOLDER_KINDS:
        return None
    path = (row.path or "").strip()
    if not path:
        return None
    return f"{row.title}: {path}"


def load_more_announcement(row: Row) -> str | None:
    """Wlasny komunikat wiersza "zaladuj wiecej odcinkow" (cs:23190-23197)."""
    return LOAD_MORE_PODCAST_MESSAGE if row.kind == LOAD_MORE_PODCAST_KIND else None


def quick_info_request(
    row: Row,
    *,
    session: str,
    duration_ticks: int = 0,
    podcast_media_length: int | None = None,
    podcast_media_type: str | None = None,
) -> dict | None:
    """Zadanie do hosta: DANE wiersza, nie gotowy napis.

    ``session`` rozroznia drogi uzupelniania parametrow dokladnie tak, jak
    oryginal: ``"files"`` -> metadane lokalnego pliku, ``"radio"`` -> metadane
    strumienia, ``"podcasts"`` -> rozmiar i typ MIME z kanalu (ta lista w
    porcie wx jeszcze nie istnieje, ale kontrakt jest juz ten sam).

    ``None`` znaczy: tego wiersza nie ma czym zmierzyc.
    """
    source = (row.path or row.url or "").strip()
    if not source:
        return None

    request: dict = {
        "session": session,
        "itemId": row.item_id,
        "title": row.title,
        "kind": row.kind,
        "source": source,
    }
    if row.artist_name.strip():
        request["artist"] = row.artist_name.strip()
    # ``item.Duration > TimeSpan.Zero`` jest warunkiem wzorca (cs:5451, 5489
    # i Formatter:37). Niedodatni czas to BRAK pomiaru, nie utwor dlugosci
    # zero -- nie wysylamy go, zeby host nie policzyl z niego bitrate.
    if duration_ticks > 0:
        request["durationTicks"] = duration_ticks
    if podcast_media_length is not None and podcast_media_length > 0:
        request["podcastMediaLength"] = podcast_media_length
    if podcast_media_type:
        request["podcastMediaType"] = podcast_media_type
    return request


@dataclass(frozen=True, slots=True)
class QuickInfoGuard:
    """Bramka SPOZNIONEGO wyniku. Port warunku po ``await`` z cs:5496-5499
    i :5524-5528::

        if (requestVersion != Interlocked.Read(ref _quickInformationRequestVersion)
            || !string.Equals(ActionItem?.Id, item.Id, StringComparison.Ordinal))
            return;

    Port pilnuje TRZECH rzeczy naraz, bo w oknie wx wszystkie trzy moga sie
    zmienic w czasie pomiaru hosta: wybor wiersza, sesja i widok. Widok jest tu
    istotny osobno: w ODTWARZACZU Left znaczy przewijanie, wiec spozniony
    parametr nie moze tam dojsc i udawac odpowiedzi na biezacy gest.

    ``alive`` to zycie okna (w GUI dokladnie to samo, co dostaje
    ``StaleResultGate``). Numer generacji zostaje po stronie bramy -- tutaj
    sprawdzamy TOZSAMOSC celu, nie kolejnosc zadan.
    """

    item_id: str
    session: str
    view: str
    alive: Callable[[], bool] | None = None

    def accepts(self, *, item_id: str | None, session: str, view: str) -> bool:
        if self.alive is not None and not self.alive():
            return False
        return (
            item_id is not None
            and item_id == self.item_id
            and session == self.session
            and view == self.view
        )


#: Komunikat, gdy POMIAR sie nie udal. Awaria MUSI byc slyszalna: cichy klawisz
#: wyglada dokladnie jak blad, ktory ta zmiana naprawia.
HOST_ERROR_MESSAGE = "Nie mogę odczytać informacji o tym elemencie"


@dataclass(frozen=True)
class QuickInfoPlan:
    """DECYZJA podjeta w watku interfejsu, przed jakimkolwiek pomiarem.

    Albo mamy komunikat od razu (``message``), albo zadanie do silnika
    (``request``) i bramke (``guard``) na jego powrot -- nigdy obu naraz.
    """

    message: str | None = None
    request: dict | None = None
    guard: QuickInfoGuard | None = None


def quick_info_plan(
    row: Row | None,
    *,
    session: str,
    view: str,
    duration_ticks: int = 0,
    podcast_media_length: int | None = None,
    podcast_media_type: str | None = None,
    alive: Callable[[], bool] | None = None,
) -> QuickInfoPlan:
    """Krok 1 LEWEJ STRZALKI: ROZPOZNANIE wiersza (cs:23190-23216).

    Nie siega do hosta, dysku ani wx, wiec wolno ja wykonac w watku
    interfejsu. Folder i \"zaladuj wiecej\" maja WLASNA informacje i nie
    uruchamiaja pomiaru -- tak samo jak w oryginale.
    """
    if row is None:
        # ``SelectedItem is { } item`` -- bez wiersza nie ma czego opisac.
        return QuickInfoPlan()

    own_message = load_more_announcement(row) or folder_announcement(row)
    if own_message is not None:
        return QuickInfoPlan(message=own_message)

    request = quick_info_request(
        row,
        session=session,
        duration_ticks=duration_ticks,
        podcast_media_length=podcast_media_length,
        podcast_media_type=podcast_media_type,
    )
    if request is None:
        # Wiersz bez zrodla: jedno uczciwe zdanie, bez zmyslonych zer.
        return QuickInfoPlan(message=NO_SOURCE_MESSAGE)

    return QuickInfoPlan(
        request=request,
        guard=QuickInfoGuard(
            item_id=row.item_id, session=session, view=view, alive=alive),
    )


def quick_info_reply(
    response: dict | None,
    *,
    guard: QuickInfoGuard,
    item_id: str | None,
    session: str,
    view: str,
    say: Callable[[str], None],
    extra_detail: str | None = None,
    omit_missing: bool = False,
) -> None:
    """Krok 3: wypowiedz wynik, ale TYLKO jesli nadal opisuje ten sam wiersz.

    Port kontroli po await (cs:5496-5499 i 5523-5526): ``requestVersion`` oraz
    ``ActionItem?.Id``. Spoznione parametry zabrzmialyby jak opis wiersza, na
    ktorym uzytkownik stoi TERAZ -- czyli jako klamstwo czytnika.
    """
    if not guard.accepts(item_id=item_id, session=session, view=view):
        return
    text = ((response or {}).get("text") or "").strip()
    # Pusta odpowiedz nie moze zamienic sie w CISZE: martwy klawisz to ten sam
    # objaw, ktory naprawiamy.
    parts = [text] if text else ([] if omit_missing else [NO_SOURCE_MESSAGE])
    detail = (extra_detail or "").strip()
    if detail and (not parts or detail.casefold() not in parts[0].casefold()):
        parts.append(detail)
    if parts:
        say(", ".join(parts))


def quick_info_failure(
    *,
    guard: QuickInfoGuard,
    item_id: str | None,
    session: str,
    view: str,
    say: Callable[[str], None],
    error: Exception | None = None,
) -> None:
    """Spozniony BLAD przechodzi przez TE SAMA bramke co wynik."""
    if guard.accepts(item_id=item_id, session=session, view=view):
        say(str(error) if getattr(error, "code", None) == "operation_busy" else HOST_ERROR_MESSAGE)


def announce_quick_information(
    row: Row | None,
    *,
    session: str,
    view: str,
    say: Callable[[str], None],
    ask_host: Callable[[dict], dict],
    duration_ticks: int = 0,
    podcast_media_length: int | None = None,
    podcast_media_type: str | None = None,
    current_item_id: Callable[[], str | None],
    current_session: Callable[[], str],
    current_view: Callable[[], str],
    alive: Callable[[], bool] | None = None,
) -> None:
    """CALA droga LEWEJ STRZALKI zlozona z trzech krokow, synchronicznie.

    Wariant dla pomiaru i dla wywolan, ktore moga poczekac na ``ask_host``.
    Okno uzywa tych samych krokow OSOBNO (``quick_info_plan`` w watku GUI,
    pomiar w watku, ``quick_info_reply`` z powrotem w GUI), bo blokowanie
    interfejsu na 5-6 s bylo by gorsze od braku komunikatu.

    CZEGO NIE ROBI: nie zmienia zaznaczenia, fokusu, odtwarzania ani
    czlonkostwa Biblioteki i NICZEGO nie zapisuje do profilu (oryginal wola tu
    ``TrySaveLocalMediaState``; port wx czyta profil TYLKO DO ODCZYTU).
    """
    plan = quick_info_plan(
        row,
        session=session,
        view=view,
        duration_ticks=duration_ticks,
        podcast_media_length=podcast_media_length,
        podcast_media_type=podcast_media_type,
        alive=alive,
    )
    if plan.message is not None:
        say(plan.message)
        return
    if plan.request is None or plan.guard is None:
        return

    try:
        response = ask_host(plan.request)
    except Exception as error:  # noqa: BLE001 - blad pomiaru ma dojsc do uzytkownika
        quick_info_failure(
            guard=plan.guard,
            item_id=current_item_id(),
            session=current_session(),
            view=current_view(),
            say=say,
            error=error,
        )
        return

    quick_info_reply(
        response,
        guard=plan.guard,
        item_id=current_item_id(),
        session=current_session(),
        view=current_view(),
        say=say,
    )
