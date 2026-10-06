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

Dlaczego dane, a nie napis: wiersz listy w tym porcie ma dzis tylko
``item_id/title/kind/path/url/detail`` (``list_model.Row``). Bitrate, czestotliwosc
probkowania, kodek i rozmiar MIERZY silnik -- lokalny plik przez
``WindowsMediaOutput.TryReadMetadataAsync`` (5 s), stacje przez
``RadioMediaOutput.TryReadStreamMetadataAsync`` (6 s). Liczenie tego w Pythonie
byloby DRUGA implementacja tej samej matematyki i rozjechaloby sie z AMC.

TRWALOSC: oryginal po zmierzeniu parametrow ZAPISUJE je do profilu
(``TrySaveLocalMediaState`` / ``CaptureRadioState`` + ``QueueStateSave``).
Port wx ma profil AMC w trybie TYLKO DO ODCZYTU -- wlascicielem zapisu zostaje
host C#. Dlatego ta droga NICZEGO nie zapisuje i nie udaje, ze zapisuje.
"""

from __future__ import annotations

from dataclasses import dataclass
from typing import Callable

from .list_model import Row

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
    say(text or NO_SOURCE_MESSAGE)


def quick_info_failure(
    *,
    guard: QuickInfoGuard,
    item_id: str | None,
    session: str,
    view: str,
    say: Callable[[str], None],
) -> None:
    """Spozniony BLAD przechodzi przez TE SAMA bramke co wynik."""
    if guard.accepts(item_id=item_id, session=session, view=view):
        say(HOST_ERROR_MESSAGE)


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
    except Exception:  # noqa: BLE001 - blad pomiaru ma dojsc do uzytkownika
        quick_info_failure(
            guard=plan.guard,
            item_id=current_item_id(),
            session=current_session(),
            view=current_view(),
            say=say,
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
