"""Czy OKNO naprawde uzywa parytetu transportu -- nie tylko sam modul.

Dlaczego osobny plik
--------------------
``test_transport_parity.py`` sprawdza czysta logike: tabele klawiszy, kroki,
szablony. To nie dowodzi jeszcze NICZEGO o programie, ktory Michal uruchamia:
``gui.py`` moglo dalej wolac stary, bezwarunkowy ``format_time``. Zgloszenie
"stale zapowiadanie uplynietego przy strzalkach" dotyczylo wlasnie tej sciezki.

``wx`` nie jest w WSL dostepne, wiec uzywamy zastepnika z ``test_gui_logic`` i
wykonujemy POJEDYNCZE metody ``LiteFrame`` na obiekcie-atrapie (bez ``__init__``,
bez okna, bez pulpitu). Sprawdzamy to, czego AST nie udowodni: ze przy
wylaczonym przelaczniku NIE MA mowy, a przy wlaczonym jest i ma format oryginalu.

Zywy cykl NVDA zostaje dla rodzica -- tu mierzymy tylko, co wola kod.
"""

from __future__ import annotations

from types import SimpleNamespace

from test_gui_logic import install_wx_stub

install_wx_stub()

from amc_wx_lite import gui  # noqa: E402  (zastepnik wx musi byc pierwszy)
from amc_wx_lite.shortcuts import Action  # noqa: E402
from amc_wx_lite.transport_parity import MessagePolicy  # noqa: E402


class FakeRunner:
    """Zamiast watku: natychmiast oddaje zadany wynik do wywolania zwrotnego."""

    def __init__(self, payload: dict | None = None) -> None:
        self.payload = payload if payload is not None else {"positionSeconds": 231.0}
        self.calls: list[str] = []

    def submit(self, slot, work, done, failed):  # noqa: ANN001 - API runnera
        self.calls.append(slot)
        self.work_result = work()
        done(self.payload)


def make_frame(
    policy: MessagePolicy | None = None,
    *,
    status: dict | None = None,
    payload: dict | None = None,
):
    """Atrapa okna: tylko pola, ktorych dotyka badana sciezka transportu."""
    frame = gui.LiteFrame.__new__(gui.LiteFrame)
    spoken: list[str] = []
    frame.announcer = SimpleNamespace(say=spoken.append)
    frame.messages = policy if policy is not None else MessagePolicy()
    frame._last_status = status if status is not None else {
        "positionSeconds": 231.0,
        "durationSeconds": 600.0,
    }
    frame.runner = FakeRunner(payload)
    seeks: list[dict] = []
    frame.client = SimpleNamespace(
        seek_by=lambda seconds: seeks.append({"delta": seconds}),
        seek_to_position=lambda seconds: seeks.append({"position": seconds}),
        set_rate=lambda rate: seeks.append({"rate": rate}),
        set_volume=lambda value: seeks.append({"volume": value}),
    )
    frame.options = SimpleNamespace(rate=1.0, volume=70)
    frame.rate_slider = SimpleNamespace(SetValue=lambda value: seeks.append({"slider": value}))
    frame.volume_slider = SimpleNamespace(SetValue=lambda value: None)
    return frame, spoken, seeks


# ------------------------------------------------- bezwarunkowe gadanie znika


def test_strzalka_milczy_gdy_uzytkownik_wylaczyl_komunikaty_strzalek() -> None:
    """To jest dokladnie zgloszenie Michala, zmierzone na sciezce okna.

    ``ArrowSeekMessages=false`` w profilu AMC ma uciszyc przewijanie strzalka.
    Dostarczona wersja mowila czas zawsze (``gui.py`` wolalo ``format_time``
    wprost w wywolaniu zwrotnym).
    """
    policy = MessagePolicy(arrow_seek_messages=False)
    frame, spoken, _ = make_frame(policy)
    frame._dispatch(Action.SEEK_FORWARD_10)
    assert spoken == []


def test_strzalka_mowi_sam_czas_gdy_komunikaty_wlaczone() -> None:
    frame, spoken, _ = make_frame()
    frame._dispatch(Action.SEEK_FORWARD_10)
    # Format oryginalu (MediaItemFormatter.cs:72): "3:51", bez slow i bez
    # "3 min 51 s".
    assert spoken == ["3:51"]


def test_wylacznik_mowy_wycisza_rutynowe_przewijanie() -> None:
    """``Messages.Enabled=false`` (MainWindow.xaml.cs:659-664)."""
    frame, spoken, _ = make_frame(MessagePolicy(enabled=False))
    frame._dispatch(Action.SEEK_FORWARD_10)
    assert spoken == []


def test_etykieta_czasu_w_oknie_ma_format_oryginalu() -> None:
    """Pasek czasu pokazuje ``0:30 z 3:51``, nie ``30 s / 3 min 51 s``.

    ``MainWindow.xaml.cs:2643-2647`` sklada etykiete z ``CommandRouter.FormatTime``
    (czyli ``MediaItemFormatter.FormatDuration``) i laczy je slowem " z ".
    Wariant wx mial tu wlasny, dluzszy format -- a to ten sam ekran, ktory
    Michal porownuje z oryginalem.
    """
    assert gui.player_time_label(30, 231) == "0:30 z 3:51"
    # Bez znanego czasu trwania (np. radio) oryginal pokazuje sama pozycje.
    assert gui.player_time_label(30, 0) == "0:30"
    assert gui.player_time_label(30, None) == "0:30"
    # Brak odtwarzania w ogole -- zdanie oryginalu, nie puste pole.
    assert gui.player_time_label(None, None) == "Stan czasu nieznany"


def test_glosnosc_ma_swoj_przelacznik_i_szablon() -> None:
    frame, spoken, _ = make_frame(MessagePolicy(volume_messages=False))
    frame._dispatch(Action.VOLUME_UP_5)
    assert spoken == []

    frame, spoken, _ = make_frame()
    frame._dispatch(Action.VOLUME_UP_5)
    # Szablon oryginalu to "{value}%" (AppSettings.cs), nie "Glosnosc 75".
    assert spoken == ["75%"]


# ------------------------------------------------------- kroki przewijania


def test_okno_wysyla_dokladnie_kroki_oryginalu() -> None:
    """Cztery pary krokow docieraja do hosta jako LICZBY SEKUND."""
    expected = {
        Action.SEEK_FORWARD_10: 10.0,
        Action.SEEK_BACK_10: -10.0,
        Action.SEEK_FORWARD_30: 30.0,
        Action.SEEK_BACK_30: -30.0,
        Action.SEEK_FORWARD_60: 60.0,
        Action.SEEK_BACK_60: -60.0,
    }
    for action, delta in expected.items():
        frame, _, seeks = make_frame()
        frame._dispatch(action)
        assert seeks == [{"delta": delta}], action


def test_krok_wlasny_bierze_liczbe_z_profilu_a_nie_stala() -> None:
    """Ctrl+Alt+strzalka = ``CustomSeekSeconds`` z ustawien AMC."""
    frame, _, seeks = make_frame(MessagePolicy(custom_seek_seconds=120))
    frame._dispatch(Action.SEEK_FORWARD_CUSTOM)
    assert seeks == [{"delta": 120.0}]

    frame, _, seeks = make_frame(MessagePolicy(custom_seek_seconds=120))
    frame._dispatch(Action.SEEK_BACK_CUSTOM)
    assert seeks == [{"delta": -120.0}]


# --------------------------------------------------------- skok procentowy


def test_skok_procentowy_liczy_pozycje_i_ma_wlasna_rodzine_przelacznikow() -> None:
    frame, spoken, seeks = make_frame(
        payload={"positionSeconds": 300.0},
        status={"positionSeconds": 0.0, "durationSeconds": 600.0},
    )
    frame._dispatch(Action.SEEK_PERCENT_50)
    # 50% z 600 s = 300 s, podane jako pozycja BEZWZGLEDNA.
    assert seeks == [{"position": 300.0}]
    assert spoken == ["50%"]


def test_skok_procentowy_milczy_pod_wlasnym_przelacznikiem() -> None:
    frame, spoken, _ = make_frame(
        MessagePolicy(percentage_seek_messages=False),
        payload={"positionSeconds": 300.0},
    )
    frame._dispatch(Action.SEEK_PERCENT_50)
    assert spoken == []


def test_skok_procentowy_bez_czasu_trwania_mowi_o_tym_zawsze() -> None:
    """Blad wykonania, nie rutyna: inaczej gest wyglada na niedzialajacy."""
    frame, spoken, seeks = make_frame(
        MessagePolicy(percentage_seek_messages=False, enabled=False),
        status={"positionSeconds": 0.0, "durationSeconds": None},
    )
    frame._dispatch(Action.SEEK_PERCENT_50)
    assert seeks == []
    assert spoken == ["Skok procentowy niedostępny: czas trwania jest nieznany"]


# ------------------------------------------------------------- czasy na zadanie


def test_ctrl_shift_e_mowi_sam_czas_takze_przy_wylaczonych_komunikatach() -> None:
    """Pytanie RECZNE dostaje odpowiedz -- CommandRouter.cs:325-335.

    I bez doklejonego "Minelo": release .383 cytuje samo "3:51".
    """
    frame, spoken, _ = make_frame(MessagePolicy(seek_messages=False, arrow_seek_messages=False))
    frame._dispatch(Action.TIME_ELAPSED)
    assert spoken == ["3:51"]


def test_pozostalo_i_calosc_tez_sa_samym_czasem() -> None:
    frame, spoken, _ = make_frame()
    frame._dispatch(Action.TIME_TOTAL)
    frame._dispatch(Action.TIME_REMAINING)
    # 600 s = 10:00, pozostalo 600-231 = 369 s = 6:09.
    assert spoken == ["10:00", "6:09"]


def test_uzytkownik_moze_dopisac_slowo_w_szablonie() -> None:
    """Slowo "Minelo" to WYBOR uzytkownika w ustawieniach, nie nasz dodatek."""
    frame, spoken, _ = make_frame(
        MessagePolicy(templates={"time.elapsed": "Minęło {elapsed}"})
    )
    frame._dispatch(Action.TIME_ELAPSED)
    assert spoken == ["Minęło 3:51"]


# -------------------------------------------------------------------- predkosc


def test_predkosc_idzie_po_drabinie_oryginalu_i_mowi_jak_oryginal() -> None:
    frame, spoken, seeks = make_frame()
    frame._dispatch(Action.RATE_UP)
    assert frame.options.rate == 1.25
    assert spoken == ["Prędkość 1,25 razy"]
    assert {"rate": 1.25} in seeks


def test_predkosc_normalna_wraca_na_jeden() -> None:
    frame, spoken, _ = make_frame()
    frame.options.rate = 1.75
    frame._dispatch(Action.RATE_RESET)
    assert frame.options.rate == 1.0
    assert spoken == ["Prędkość normalna"]


def test_predkosc_nie_wychodzi_poza_drabine() -> None:
    frame, _, _ = make_frame()
    frame.options.rate = 2.0
    frame._dispatch(Action.RATE_UP)
    assert frame.options.rate == 2.0
    frame.options.rate = 0.5
    frame._dispatch(Action.RATE_DOWN)
    assert frame.options.rate == 0.5


# ------------------------------------------------------------- przelacznik G


def test_ctrl_shift_g_nie_pisze_po_cichu_do_cudzego_profilu() -> None:
    """Wspolny profil: mowimy, gdzie zmienic opcje, zamiast udawac zapis."""
    frame, spoken, _ = make_frame(MessagePolicy(may_persist=False))
    frame._dispatch(Action.TOGGLE_SEEK_MESSAGES)
    assert frame.messages.seek_messages is True
    assert spoken == [
        "Automatycznymi komunikatami odtwarzacza zarządza AMC. "
        "Zmień je w ustawieniach AMC."
    ]


def test_ctrl_shift_g_przelacza_we_wlasnej_piaskownicy() -> None:
    frame, spoken, _ = make_frame(MessagePolicy(may_persist=True))
    frame._dispatch(Action.TOGGLE_SEEK_MESSAGES)
    assert frame.messages.seek_messages is False
    assert spoken == ["Automatyczne komunikaty odtwarzacza wyłączone"]
    # Po wylaczeniu strzalka naprawde milczy -- przelacznik jest WYKONYWANY.
    frame._dispatch(Action.SEEK_FORWARD_10)
    assert spoken[-1] == "Automatyczne komunikaty odtwarzacza wyłączone"
