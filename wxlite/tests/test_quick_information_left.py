"""LEWA STRZALKA na liscie = krotka informacja uzupelniajaca (jak w AMC).

ZRODLO WZORCA (odczytane, nie zgadniete):

  src/AccessibleMediaController.Windows/MainWindow.xaml.cs:23206-23216
    MediaList_PreviewKeyDown, galaz ``Keyboard.Modifiers == ModifierKeys.None
    && SelectedItem is { } item && key == Key.Left``:
        FolderPath is { } folderPath  ->  Announce($"{item.Title}: {folderPath}")
        else                          ->  AnnounceQuickMediaInformation(item)
    a WCZESNIEJ (23190-23197) wiersz "zaladuj wiecej odcinkow" dostaje
    WLASNY komunikat ``"Załaduj więcej odcinków. Naciśnij Enter"``.

  :5425-5483  BuildQuickMediaInformation -- sklada czesci i oddaje je
    ``QuickMediaInformationFormatter.Format`` (Core/Presentation).
  :5485-5541  AnnounceQuickMediaInformation -- UZUPELNIA brakujace parametry
    (lokalny plik: WindowsMediaOutput 5 s; stacja: RadioMediaOutput 6 s), a PO
    AWAIT sprawdza ``requestVersion`` i ``ActionItem?.Id`` -- czyli nie czyta
    parametrow CUDZEGO wyboru.

Te testy mierza PORT tej samej drogi: zadanie do hosta, ochrona fokusu i
roznice sesji. Formatowanie zostaje w C# -- tu go NIE powtarzamy.
"""

from __future__ import annotations

from amc_wx_lite.list_model import Row
from amc_wx_lite.quick_info import (
    NO_SOURCE_MESSAGE,
    QuickInfoGuard,
    folder_announcement,
    load_more_announcement,
    quick_info_plan,
    quick_info_reply,
    quick_info_request,
)
from amc_wx_lite.shortcuts import Action, Chord, resolve


def _folder_row() -> Row:
    return Row(item_id="dir:D:\\Muzyka\\Zdjęcia", title="Zdjęcia", kind="folder",
               path="D:\\Muzyka\\Zdjęcia")


def _station_row() -> Row:
    return Row(item_id="st:1", title="Radio Nowy Świat", kind="station",
               url="https://stream.nowyswiat.online/mp3")


def _no_source_row() -> Row:
    return Row(item_id="ghost:1", title="Wiersz bez zapisanego zrodla", kind="track")


# ------------------------------------------------------- klawisz na LISCIE

def test_lewa_strzalka_na_liscie_plikow_ma_wlasna_akcje() -> None:
    """Dzis resolver oddaje ``None`` i klawisz ginie w kontrolce."""
    action = resolve(Chord("Left"), player_view=False, radio_session=False)
    assert action is Action.QUICK_INFORMATION


def test_lewa_strzalka_na_liscie_stacji_ma_te_sama_akcje() -> None:
    action = resolve(Chord("Left"), player_view=False, radio_session=True)
    assert action is Action.QUICK_INFORMATION


def test_lewa_strzalka_w_odtwarzaczu_nadal_przewija() -> None:
    """Parytet transportu nie moze zginac: w PLAYERZE Left to przewijanie."""
    assert resolve(Chord("Left"), player_view=True, radio_session=False) is Action.SEEK_BACK_10
    assert resolve(Chord("Left"), player_view=True, radio_session=True) is Action.SEEK_BACK_10


def test_lewa_strzalka_z_modyfikatorem_na_liscie_nie_jest_nasza() -> None:
    """Oryginal wymaga ``ModifierKeys.None``; Shift/Ctrl/Alt zostaja kontrolce."""
    for chord in (
        Chord("Left", shift=True),
        Chord("Left", ctrl=True),
        Chord("Left", alt=True),
        Chord("Left", ctrl=True, alt=True),
    ):
        assert resolve(chord, player_view=False, radio_session=False) is None, chord.canonical


# --------------------------------------------- folder ma INNA informacje

def test_folder_oddaje_tytul_i_sciezke_bez_pytania_hosta() -> None:
    """``$"{item.Title}: {folderPath}"`` (cs:23211) -- bez parametrow audio."""
    row = Row(item_id="dir:D:\\Muzyka\\Zaległe", title="Zaległe", kind="folder",
              path="D:\\Muzyka\\Zaległe")
    assert folder_announcement(row) == "Zaległe: D:\\Muzyka\\Zaległe"


def test_folder_nadrzedny_tez_jest_folderem() -> None:
    row = Row(item_id="parent:D:\\Muzyka", title="..", kind="parent", path="D:\\Muzyka")
    assert folder_announcement(row) == "..: D:\\Muzyka"


def test_folder_bez_sciezki_nie_udaje_pustej_sciezki() -> None:
    """Korzen zrodel ma ``path=None`` -- nie wolno oddac \"..: \"."""
    row = Row(item_id="parent:", title="..", kind="parent", path=None)
    assert folder_announcement(row) is None


def test_utwor_nie_jest_folderem() -> None:
    row = Row(item_id="file:1", title="Piosenka", kind="track", path="D:\\a.mp3")
    assert folder_announcement(row) is None


def test_stacja_nie_jest_folderem() -> None:
    row = Row(item_id="st1", title="Radio Nowy Świat", kind="station",
              url="https://example.invalid/stream")
    assert folder_announcement(row) is None


# ------------------------------------------- zadanie do hosta (dane, nie napis)

def test_zadanie_dla_pliku_niesie_sciezke_i_sesje_plikow() -> None:
    row = Row(item_id="file:7", title="Łąka", kind="track", path="D:\\Muzyka\\Łąka.mp3")
    request = quick_info_request(row, session="files")
    assert request["session"] == "files"
    assert request["kind"] == "track"
    assert request["source"] == "D:\\Muzyka\\Łąka.mp3"
    assert request["itemId"] == "file:7"
    assert request["title"] == "Łąka"
    # Wiersz Pythona NIE ma bitrate -- host go mierzy. Zadne pole nie moze
    # podawac zmyslonego zera.
    assert "bitrateKbps" not in request
    assert "sampleRateHz" not in request


def test_zadanie_dla_stacji_niesie_adres_i_sesje_radia() -> None:
    row = Row(item_id="st1", title="Radio Nowy Świat", kind="station",
              url="https://stream.example.invalid/live")
    request = quick_info_request(row, session="radio")
    assert request["session"] == "radio"
    assert request["kind"] == "station"
    assert request["source"] == "https://stream.example.invalid/live"
    assert request["itemId"] == "st1"


def test_zadanie_bierze_czas_z_biblioteki_gdy_jest_znany() -> None:
    """Czas z SQLite (``duration_ticks``) pozwala hostowi OSZACOWAC bitrate
    tak samo jak ``LocalAudioFileDiscovery.EstimateBitrateKbps``."""
    row = Row(item_id="file:7", title="Łąka", kind="track", path="D:\\Łąka.mp3")
    request = quick_info_request(row, session="files", duration_ticks=1_234_567_890)
    assert request["durationTicks"] == 1_234_567_890


def test_zadanie_nie_wysyla_niedodatniego_czasu() -> None:
    """``item.Duration > TimeSpan.Zero`` jest warunkiem wzorca -- zero NIE
    jest dana, wiec nie udajemy, ze ja mamy."""
    row = Row(item_id="file:7", title="Łąka", kind="track", path="D:\\Łąka.mp3")
    assert "durationTicks" not in quick_info_request(row, session="files", duration_ticks=0)
    assert "durationTicks" not in quick_info_request(row, session="files", duration_ticks=-5)


def test_zadanie_dla_wiersza_bez_zrodla_nie_powstaje() -> None:
    row = Row(item_id="x", title="Nic", kind="track", path=None, url=None)
    assert quick_info_request(row, session="files") is None


# ------------------------- kontrakt PODCASTOW (droga gotowa, lista nieobecna)

def test_kontrakt_podcastu_niesie_dlugosc_i_typ_mime() -> None:
    """Wzorzec: ``podcastEpisode?.MediaLength`` (cs:5468-5471) jako ROZMIAR
    oraz ``FormatPodcastCodec(mediaType)`` (cs:5413-5423) jako kodek.

    Lista Podcastow w tym porcie NIE ISTNIEJE. Ten test utrwala KONTRAKT, zeby
    jej przyszly port szedl TA SAMA droga, a nie wlasna.
    """
    row = Row(item_id="ep1", title="Odcinek 12", kind="episode",
              url="https://feed.example.invalid/ep12.mp3")
    request = quick_info_request(
        row, session="podcasts",
        duration_ticks=18_000_000_000,
        podcast_media_length=24_117_248,
        podcast_media_type="audio/mpeg",
    )
    assert request["session"] == "podcasts"
    assert request["kind"] == "episode"
    assert request["podcastMediaLength"] == 24_117_248
    assert request["podcastMediaType"] == "audio/mpeg"
    assert request["durationTicks"] == 18_000_000_000


def test_tablica_mime_podcastu_nalezy_do_hosta_a_nie_do_pythona() -> None:
    """Typ MIME idzie do hosta SUROWY -- bez tablicy w Pythonie.

    Tlumaczenie ``audio/mpeg -> MP3`` zostaje w
    ``LiteQuickInformation.FormatPodcastCodec`` (port cs:5413-5423). Gdyby
    Python mial wlasna kopie, kazdy nowy format rozjechal by sie po cichu.
    Zgodnosc liczb i jednostek mierzy ROUNDTRIP w
    ``test_quick_information_wire.py``, na prawdziwym formatterze C#.
    """
    import amc_wx_lite.quick_info as module

    assert not hasattr(module, "PODCAST_MIME_CODECS"), (
        "tablica MIME nie moze wrocic do Pythona -- host jest zrodlem prawdy"
    )
    request = quick_info_request(
        Row(
            item_id="podcast:ep1",
            title="Odcinek 12",
            kind="episode",
            url="https://feed.example.invalid/ep12.mp3",
        ),
        session="podcasts",
        podcast_media_type="audio/mpeg",
    )
    assert request is not None
    assert request["podcastMediaType"] == "audio/mpeg", "MIME bez tlumaczenia"
    assert "codec" not in request, "Python NIE wysyla gotowego kodeka"


def test_wiersz_zaladuj_wiecej_ma_wlasny_komunikat() -> None:
    """cs:23190-23197 -- ten wiersz NIE jest elementem multimedialnym."""
    row = Row(item_id="podcast:loadMore", title="Załaduj więcej", kind="loadMorePodcast")
    assert load_more_announcement(row) == "Załaduj więcej odcinków. Naciśnij Enter"
    assert load_more_announcement(
        Row(item_id="file:1", title="a", kind="track", path="D:\\a.mp3")) is None


# ---------------------------------------- OCHRONA FOKUSU po powrocie z hosta

def test_spozniony_wynik_dla_innego_wiersza_jest_odrzucany() -> None:
    """Port ``!string.Equals(ActionItem?.Id, item.Id)`` (cs:5497, 5525)."""
    guard = QuickInfoGuard(item_id="file:7", session="files", view="list")
    assert guard.accepts(item_id="file:7", session="files", view="list")
    assert not guard.accepts(item_id="file:8", session="files", view="list")


def test_spozniony_wynik_po_zmianie_sesji_jest_odrzucany() -> None:
    guard = QuickInfoGuard(item_id="st1", session="radio", view="list")
    assert not guard.accepts(item_id="st1", session="files", view="list")


def test_spozniony_wynik_po_wejsciu_do_odtwarzacza_jest_odrzucany() -> None:
    """W odtwarzaczu Left znaczy PRZEWIJANIE -- stary parametr nie moze tam
    dojsc i udawac odpowiedzi na biezacy gest."""
    guard = QuickInfoGuard(item_id="file:7", session="files", view="list")
    assert not guard.accepts(item_id="file:7", session="files", view="player")


def test_plan_dla_folderu_mowi_od_razu_bez_pytania_hosta() -> None:
    """Rozbicie na PLAN i ODPOWIEDZ istnieje dla watku interfejsu.

    ``announce_quick_information`` blokuje do konca pomiaru, co w okienku
    zamrozilo by interfejs na 5-6 s. Okno uzywa wiec tych samych dwoch krokow
    osobno: ``quick_info_plan`` w watku GUI, pomiar w watku, a
    ``quick_info_reply`` znowu w GUI. Jedna decyzja, dwa sposoby wykonania.
    """
    plan = quick_info_plan(_folder_row(), session="files", view="list")
    assert plan.message == "Zdjęcia: D:\\Muzyka\\Zdjęcia"
    assert plan.request is None, "folder NIE uruchamia pomiaru silnika"


def test_plan_dla_stacji_niesie_zadanie_i_bramke() -> None:
    plan = quick_info_plan(_station_row(), session="radio", view="list")
    assert plan.message is None
    assert plan.request is not None
    assert plan.request["session"] == "radio"
    assert plan.guard is not None and plan.guard.item_id == "st:1"


def test_plan_bez_zrodla_mowi_uczciwie_i_nie_pyta() -> None:
    plan = quick_info_plan(_no_source_row(), session="files", view="list")
    assert plan.message == NO_SOURCE_MESSAGE
    assert plan.request is None


def test_odpowiedz_na_stary_wiersz_jest_milczeniem() -> None:
    plan = quick_info_plan(_station_row(), session="radio", view="list")
    assert plan.guard is not None
    said: list[str] = []
    quick_info_reply(
        {"text": "MP3, 128 kb/s"},
        guard=plan.guard,
        item_id="st:2",  # uzytkownik zszedl nizej
        session="radio",
        view="list",
        say=said.append,
    )
    assert said == [], f"spozniony wynik NIE moze opisac cudzego wiersza: {said}"


def test_odpowiedz_na_ten_sam_wiersz_jest_wymawiana() -> None:
    plan = quick_info_plan(_station_row(), session="radio", view="list")
    assert plan.guard is not None
    said: list[str] = []
    quick_info_reply(
        {"text": "MP3, 128 kb/s"},
        guard=plan.guard,
        item_id="st:1",
        session="radio",
        view="list",
        say=said.append,
    )
    assert said == ["MP3, 128 kb/s"]


def test_pusta_odpowiedz_hosta_nie_zostawia_ciszy() -> None:
    plan = quick_info_plan(_station_row(), session="radio", view="list")
    assert plan.guard is not None
    said: list[str] = []
    quick_info_reply(
        {"text": "   "},
        guard=plan.guard,
        item_id="st:1",
        session="radio",
        view="list",
        say=said.append,
    )
    assert said == [NO_SOURCE_MESSAGE], f"martwy klawisz to ten sam blad: {said}"


def test_wynik_dla_zamknietego_okna_jest_odrzucany() -> None:
    guard = QuickInfoGuard(item_id="file:7", session="files", view="list", alive=lambda: False)
    assert not guard.accepts(item_id="file:7", session="files", view="list")


# ------------------------- CALA DROGA klawisza (bez wx, bez zywego hosta)

class FakeSpeech:
    """Zbiera to, co uslyszalby czytnik. Kolejnosc ma znaczenie."""

    def __init__(self) -> None:
        self.said: list[str] = []

    def __call__(self, text: str) -> None:
        self.said.append(text)


def run_left(
    row: Row | None,
    *,
    session: str,
    view: str = "list",
    host_text: str | None = "MP3, 128 kb/s",
    duration_ticks: int = 0,
    after: dict | None = None,
    host_error: Exception | None = None,
):
    """Jedno nacisniecie LEWEJ STRZALKI na liscie, bez wx i bez procesu hosta.

    ``after`` opisuje, co zmienilo sie W CZASIE pomiaru hosta (wybor, sesja,
    widok, zycie okna) -- czyli dokladnie te zdarzenia, przed ktorymi broni
    warunek po ``await`` z cs:5496-5499.
    """
    from amc_wx_lite.quick_info import announce_quick_information

    speech = FakeSpeech()
    calls: list[dict] = []
    state = {
        "item_id": row.item_id if row is not None else None,
        "session": session,
        "view": view,
        "alive": True,
    }

    def ask_host(request: dict) -> dict:
        calls.append(request)
        # Stan zmienia sie DOKLADNIE w chwili, gdy host liczy -- jak w oknie.
        if after:
            state.update(after)
        if host_error is not None:
            raise host_error
        return {"itemId": request["itemId"], "text": host_text}

    announce_quick_information(
        row,
        session=session,
        view=view,
        say=speech,
        ask_host=ask_host,
        duration_ticks=duration_ticks,
        current_item_id=lambda: state["item_id"],
        current_session=lambda: state["session"],
        current_view=lambda: state["view"],
        alive=lambda: bool(state["alive"]),
    )
    return speech.said, calls


def test_lewa_na_pliku_czyta_napis_ZLOZONY_PRZEZ_HOSTA() -> None:
    """Komunikat przychodzi z formattera C# i NIE jest tu przerabiany."""
    row = Row(item_id="file:7", title="Łąka", kind="track", path="D:\\Muzyka\\Łąka.flac")
    said, calls = run_left(row, session="files", host_text="FLAC, 320 kb/s, 44,1 kHz, 5 MB")
    assert said == ["FLAC, 320 kb/s, 44,1 kHz, 5 MB"]
    assert len(calls) == 1 and calls[0]["session"] == "files"


def test_lewa_na_stacji_pyta_hosta_z_sesja_radia() -> None:
    """Roznica sesji nie ginie: stacja idzie jako ``radio``."""
    row = Row(item_id="st1", title="Radio Nowy Świat", kind="station",
              url="https://stream.example.invalid/live")
    said, calls = run_left(row, session="radio", host_text="MP3, 128 kb/s, 48 kHz")
    assert said == ["MP3, 128 kb/s, 48 kHz"]
    assert calls[0]["session"] == "radio"
    assert calls[0]["source"] == "https://stream.example.invalid/live"


def test_lewa_na_folderze_NIE_pyta_hosta() -> None:
    """Folder ma INNA informacje (cs:23211) -- sciezke, bez pomiaru audio."""
    row = Row(item_id="dir:D:\\Muzyka", title="Muzyka", kind="folder", path="D:\\Muzyka")
    said, calls = run_left(row, session="files")
    assert said == ["Muzyka: D:\\Muzyka"]
    assert calls == [], "folder nie uruchamia pomiaru silnika"


def test_lewa_na_wierszu_zaladuj_wiecej_ma_wlasny_komunikat() -> None:
    row = Row(item_id="podcast:more", title="Załaduj więcej", kind="loadMorePodcast")
    said, calls = run_left(row, session="podcasts")
    assert said == ["Załaduj więcej odcinków. Naciśnij Enter"]
    assert calls == []


def test_lewa_bez_zaznaczenia_nic_nie_robi() -> None:
    """``SelectedItem is { } item`` -- bez wiersza nie ma czego opisac."""
    said, calls = run_left(None, session="files")
    assert said == [] and calls == []


def test_lewa_na_wierszu_bez_zrodla_mowi_krotko_i_uczciwie() -> None:
    row = Row(item_id="x", title="Nic", kind="track", path=None, url=None)
    said, calls = run_left(row, session="files")
    assert said == ["Brak zapisanych informacji uzupełniających"]
    assert calls == [], "nie ma czego mierzyc"
    assert "0" not in said[0], "zadnych zmyslonych zer"


def test_spozniony_napis_NIE_dochodzi_po_zmianie_zaznaczenia() -> None:
    """Pomiar trwa do 6 s. Jesli uzytkownik zszedl na inny wiersz, parametry
    starego wiersza NIE MOGA zostac wypowiedziane -- brzmialyby jak opis
    wiersza, na ktorym stoi teraz."""
    row = Row(item_id="file:7", title="Łąka", kind="track", path="D:\\a.mp3")
    said, calls = run_left(row, session="files", after={"item_id": "file:8"})
    assert len(calls) == 1, "zadanie poszlo"
    assert said == [], "ale spozniony wynik jest porzucony BEZ SLOWA"


def test_spozniony_napis_NIE_dochodzi_po_zmianie_sesji() -> None:
    row = Row(item_id="st1", title="Stacja", kind="station",
              url="https://stream.example.invalid/live")
    said, _ = run_left(row, session="radio", after={"session": "files"})
    assert said == []


def test_spozniony_napis_NIE_dochodzi_po_wejsciu_do_odtwarzacza() -> None:
    """W ODTWARZACZU Left znaczy przewijanie -- stary parametr nie moze tam
    dojsc i udawac odpowiedzi na biezacy gest."""
    row = Row(item_id="file:7", title="Łąka", kind="track", path="D:\\a.mp3")
    said, _ = run_left(row, session="files", after={"view": "player"})
    assert said == []


def test_spozniony_napis_NIE_dochodzi_po_zamknieciu_okna() -> None:
    row = Row(item_id="file:7", title="Łąka", kind="track", path="D:\\a.mp3")
    said, _ = run_left(row, session="files", after={"alive": False})
    assert said == []


def test_pusta_odpowiedz_hosta_nie_jest_wypowiadana_jako_cisza() -> None:
    """Host nie powinien oddac pustki, ale gdy to zrobi, czytnik ma powiedziec
    JEDNO uczciwe zdanie, a nie milczec jak przy martwym klawiszu."""
    row = Row(item_id="file:7", title="Łąka", kind="track", path="D:\\a.mp3")
    said, _ = run_left(row, session="files", host_text="   ")
    assert said == ["Brak zapisanych informacji uzupełniających"]


def test_blad_hosta_jest_slyszalny_i_nie_wywraca_okna() -> None:
    """Awaria pomiaru nie moze byc cicha: martwy klawisz wyglada jak ten sam
    blad, ktory wlasnie naprawiamy."""
    row = Row(item_id="file:7", title="Łąka", kind="track", path="D:\\a.mp3")
    said, _ = run_left(row, session="files", host_error=OSError("host nie odpowiada"))
    assert len(said) == 1
    assert "Nie mogę odczytać informacji" in said[0], said[0]


def test_blad_hosta_dla_STAREGO_wiersza_tez_milczy() -> None:
    """Spozniony BLAD jest tak samo mylacy jak spozniony wynik."""
    row = Row(item_id="file:7", title="Łąka", kind="track", path="D:\\a.mp3")
    said, _ = run_left(
        row, session="files",
        after={"item_id": "file:8"},
        host_error=OSError("host nie odpowiada"),
    )
    assert said == []


def test_nic_nie_zmienia_wyboru_ani_odtwarzania() -> None:
    """Klawisz jest CZYSTO INFORMACYJNY: zadnego Play, Seek, SetSelection."""
    row = Row(item_id="file:7", title="Łąka", kind="track", path="D:\\a.mp3")
    _, calls = run_left(row, session="files")
    assert len(calls) == 1
    # Jedyna operacja protokolu, jaka ta droga wolno wyslac.
    from amc_wx_lite.quick_info import QUICK_INFO_OP

    assert QUICK_INFO_OP == "media.quickInformation"
    forbidden = ("volume", "rate", "seek", "play", "pause", "position")
    for key in calls[0]:
        assert not any(word in key.lower() for word in forbidden), key
