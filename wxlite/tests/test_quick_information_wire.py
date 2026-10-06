"""ROUNDTRIP: klient Python <-> PRAWDZIWY formatter C# (LEWA STRZALKA).

Dlaczego ten plik istnieje osobno od ``test_quick_information_left.py``:
tamten mierzy DECYZJE po stronie Pythona (czyj jest klawisz, ktory wiersz ma
inna informacje, co odrzuca bramka). Tutaj mierzymy ZGODNOSC KONTRAKTU --
czy zadanie zlozone przez ``quick_info_request`` jest tym, co host
rzeczywiscie potrafi przeczytac, i czy wraca z niego napis zlozony przez
``QuickMediaInformationFormatter`` z Core.

To nie jest atrapa: ``--wire-server`` uruchamia ten sam ``LiteDispatchLoop``
i ten sam ``LiteQuickInformation.Build``, ktore pojda na Windows. Nie ma tu
tylko POMIARU silnika (NAudio i atrybuty chmury wymagaja Windows), dlatego
wynik sondy wchodzi w zadaniu jako pola ``probe*``. Granica jest swiadoma:
test nie udaje, ze czyta dzwiek w WSL, ale liczby, jednostki i polskie
separatory sa PRAWDZIWE, bo liczy je produkcyjny kod C#.

Brak zbudowanego serwera = jawny SKIP, nie cichy zielony wynik.
"""

from __future__ import annotations

from amc_wx_lite.list_model import Row
from amc_wx_lite.quick_info import QUICK_INFO_OP, quick_info_request

from test_wire_compatibility import make_client


def ask(row: Row, *, session: str, **kwargs) -> dict:
    """Prawdziwa droga: ``quick_info_request`` -> host -> napis z Core."""
    request = quick_info_request(row, session=session, **{
        key: value for key, value in kwargs.items() if not key.startswith("probe")
    })
    assert request is not None, "wiersz musi miec zrodlo do zmierzenia"
    for key, value in kwargs.items():
        if key.startswith("probe"):
            request[key] = value
    client = make_client()
    try:
        return client.call(QUICK_INFO_OP, request)
    finally:
        client.close()


# ---------------------------------------------------- tozsamosc odpowiedzi

def test_host_oddaje_itemid_napisem_i_bez_zmian() -> None:
    """Bramka fokusu porownuje ID, wiec musi ono przejsc przez JSON NIETKNIETE.

    ``item_id`` w tym porcie jest NAPISEM (``file:7``, ``station:3``) i host
    nie moze go zamienic na liczbe ani obciac.
    """
    row = Row(item_id="file:7", title="Łąka", kind="track", path="D:\\Muzyka\\Łąka.flac")
    result = ask(row, session="files", probeExists=True, probeSizeBytes=5_242_880,
                 probeBitrateKbps=320, probeSampleRateHz=44_100)
    assert result["itemId"] == "file:7"
    assert isinstance(result["itemId"], str), "ID zostaje napisem"
    assert result["session"] == "files"


# ------------------------------------------------------------ PLIKI (sesja)

def test_plik_lokalny_dostaje_kontener_parametry_i_rozmiar() -> None:
    """Sesja PLIKOW: kontener z rozszerzenia + parametry + rozmiar z dysku."""
    row = Row(item_id="file:7", title="Łąka", kind="track", path="D:\\Muzyka\\Łąka.flac")
    text = ask(row, session="files", probeExists=True, probeSizeBytes=5_242_880,
               probeBitrateKbps=320, probeSampleRateHz=44_100)["text"]
    assert text.startswith("FLAC,"), text
    assert "320 kb/s" in text, text
    # Polski separator dziesietny liczy formatter C#, nie Python.
    assert "44,1 kHz" in text, text
    assert "5 MB" in text, text
    # Tytul wiersza czytnik wypowiedzial JUZ przy nawigacji (celowo bez niego).
    assert "Łąka" not in text, f"komunikat nie powtarza tytulu: {text}"


def test_bitrate_pliku_jest_SZACOWANY_ta_sama_matematyka_co_amc() -> None:
    """cs:5450-5458 -- brak bitrate + znany czas => oszacowanie z rozmiaru.

    Liczbe liczy ``LocalAudioFileDiscovery.EstimateBitrateKbps`` w C#. Gdyby
    Python liczyl to sam, wynik rozjechal by sie z pelnym AMC.
    """
    row = Row(item_id="file:8", title="Bez bitrate", kind="track", path="D:\\a.mp3")
    text = ask(row, session="files", duration_ticks=4 * 60 * 10_000_000,
               probeExists=True, probeSizeBytes=5_760_000)["text"]
    assert "192 kb/s" in text, text
    assert "4:00" in text, text


def test_niedostepny_plik_nie_dostaje_zmyslonego_zera() -> None:
    """Pliku nie da sie otworzyc: zostaje sam kontener, bez ``0 B``/``0 kb/s``."""
    row = Row(item_id="file:0", title="Niedostepny", kind="track", path="D:\\brak.mp3")
    text = ask(row, session="files", probeExists=False)["text"]
    assert "0 B" not in text and "0 kb/s" not in text, text
    assert text == "MP3", text


def test_plik_w_chmurze_jest_OGLASZANY_a_nie_pobierany() -> None:
    """Ostrzezenie o chmurze pochodzi z ATRYBUTOW, bez hydratacji pliku."""
    row = Row(item_id="file:c", title="W chmurze", kind="track",
              path="D:\\OneDrive\\a.mp3")
    text = ask(row, session="files", probeExists=True, probeCloud=True)["text"]
    assert "chmurze" in text, text
    assert "MB" not in text, f"placeholdera nie mierzymy: {text}"


# ------------------------------------------------------------ RADIO (sesja)

def test_stacja_dostaje_kodek_i_parametry_strumienia_bez_rozmiaru() -> None:
    """Sesja RADIA rozni sie od plikow: nie ma rozmiaru ani dlugosci."""
    row = Row(item_id="station:3", title="Radio Nowy Świat", kind="station",
              url="https://stream.example.invalid/live")
    text = ask(row, session="radio", probeBitrateKbps=128, probeSampleRateHz=48_000,
               probeCodec="mp3")["text"]
    assert text.startswith("MP3,"), text
    assert "128 kb/s" in text and "48 kHz" in text, text
    assert "MB" not in text and "KB" not in text, f"stacja bez rozmiaru: {text}"
    assert "0:00" not in text, f"stacja bez zerowego czasu: {text}"


def test_milczaca_stacja_dostaje_krotki_uczciwy_komunikat() -> None:
    """Sonda strumienia nic nie zmierzyla -- jedno zdanie, nie zera."""
    row = Row(item_id="station:9", title="Cicha stacja", kind="station",
              url="https://stream.example.invalid/milczy")
    text = ask(row, session="radio")["text"]
    assert text == "Brak zapisanych informacji uzupełniających", text
    assert "0 kb/s" not in text, text


# --------------------------------------------- PODCASTY (tylko KONTRAKT)

def test_kontrakt_podcastu_dziala_ta_sama_droga_choc_listy_jeszcze_nie_ma() -> None:
    """Lista Podcastow w porcie wx NIE ISTNIEJE.

    Ten test NIE twierdzi, ze jest gotowa. Mierzy wylacznie, ze ta sama
    droga (``quick_info_request`` -> ``media.quickInformation``) obsluzy ja
    bez nowego kanalu, gdy lista powstanie: rozmiar bierze sie z
    ``MediaLength`` kanalu, a kodek z typu MIME po stronie hosta.
    """
    row = Row(item_id="podcast:ep1", title="Odcinek 12", kind="episode",
              url="https://feed.example.invalid/ep12.mp3")
    text = ask(row, session="podcasts", duration_ticks=30 * 60 * 10_000_000,
               podcast_media_length=24_117_248, podcast_media_type="audio/mpeg")["text"]
    assert text.startswith("MP3,"), text
    assert "23 MB" in text and "30:00" in text, text
    assert "107 kb/s" in text, f"bitrate z MediaLength i czasu: {text}"


def test_nieznany_mime_podcastu_nie_wymysla_kodeka() -> None:
    row = Row(item_id="podcast:ep2", title="Odcinek 13", kind="episode",
              url="https://feed.example.invalid/ep13.webm")
    text = ask(row, session="podcasts", podcast_media_length=1024,
               podcast_media_type="audio/webm")["text"]
    assert "webm" not in text.lower() and "nieznan" not in text.lower(), text
    assert "1 KB" in text, text


# ------------------------------------------------------- bledy kontraktu

def test_zadanie_bez_zrodla_jest_bledem_hosta_a_nie_pustym_napisem() -> None:
    """Brak zrodla to BLAD TRESCI: cichy pusty napis wyglada jak dzialanie."""
    from amc_wx_lite.host_client import HostError

    client = make_client()
    try:
        try:
            client.call(QUICK_INFO_OP, {"session": "files", "itemId": "x"})
        except HostError:
            assert client.alive, "host zyje dalej po bledzie tresci"
            return
        raise AssertionError("ZALOZENIE NIESPELNIONE: brak 'source' ma byc bledem")
    finally:
        client.close()
