# Radio Archiwista — zwykłe wyszukiwanie przyrostowe listy

Data: 2026-11-19
Zakres: tylko sesja Radio Archiwista, poziom godzin wybranego dnia/stacji. Bez GUI, instalacji, sieci, dźwięku ani publikacji.

## Korekta kierunku

Zastosowano nadrzędną korektę użytkownika: jest to zwykła lista danych z serwera, więc zachowaniem docelowym jest natywne, dosłowne wyszukiwanie przyrostowe po pierwszej kolumnie — tak samo dla liter i cyfr. Nie dodano selektora godzin, bufora czasu, aliasu `7 -> 07`, zmiany tytułów ani nowego skrótu.

Nie było wcześniejszej zmiany produkcyjnej do wycofania.

## Poza zakresem

Ten raport dotyczy wyłącznie normalnej nawigacji nazw w liście Radio Archiwista. Cyfra w liście (`7`, `0` itd.) nie jest skokiem procentowym odtwarzacza ani jego skrótem. Ctrl+H, zakładki, skoki procentowe i pozostałe funkcje odtwarzacza są prowadzone osobno w worktree `amc-radioarchiwista-player-20261119`; nie zmieniano ich ani wspólnego `gui.py`.

## Co sprawdzono

1. `radioarchiwista_list` jest tą samą zwykłą `MediaListCtrl` (`wx.LC_REPORT`, bez `LC_VIRTUAL`) co pozostałe listy usług. Wyjątkiem jest lokalna lista plików, która może działać w trybie wirtualnym.
2. Każda lista, w tym Radio Archiwista, dostaje ten sam `EVT_CHAR_HOOK` (`LiteFrame._on_list_shortcut_hook`). Dla zwykłej litery lub cyfry bez modyfikatora handler wywołuje wyłącznie `event.Skip()`; nie wywołuje `_on_key`, `_dispatch`, `activate_selected` ani odtwarzania.
3. Produkcyjny ciąg danych `recording_rows` -> `Navigator.apply_radioarchiwista_rows(RADIOARCHIWISTA_HOURS)` -> `list_sync.model_row_texts` został sprawdzony na offline-fixture z godzinami `00:00`, `07:00`, `17:00`.
4. Przy rzeczywistych polach odczytu sesji (`list_reading.session_fields`) pierwsza, natywna kolumna pozostaje dosłownym tytułem: `00:00`, `07:00`, `17:00`. Status odtwarzania i gwiazdka ulubionego trafiają do dalszej kolumny, więc nie zmieniają prefiksu, po którym wyszukuje zwykły `SysListView32`.

## Wynik i granica dowodu

Kod aplikacji już przekazuje zwykłe litery i cyfry do natywnej listy, a dane godzin zachowują dosłowne teksty. Standardowe literalne wyszukiwanie nie może potraktować pojedynczego `7` jako prefiksu `07:00`: takim prefiksem jest `0`, potem `7`. Zgodnie z korektą użytkownika nie należy tego zastępować semantycznym aliasem.

Headless WSL nie uruchamia rzeczywistego `wx.ListCtrl`/`SysListView32`, więc nie może potwierdzić ani obalić zachowania Windows po fizycznym wpisaniu sekwencji `0`, `7` (ani klawiatury numerycznej). Nie zgłoszono fałszywie testu GUI/NVDA. Jeżeli na uzgodnionym oknie GUI `0`, następnie `7`, wciąż nie wybierze `07:00`, potrzebny jest zapis zdarzeń realnej kontrolki: fokus, `EVT_CHAR_HOOK`, `EVT_KEY_DOWN`, tekst pierwszej kolumny oraz stan filtra.

## Odbiór rodzica — bez deklarowania naprawy Windows

Kandydat `30b841921fa8552feb602a91308fc8f890c1d890` zmienia tylko ten raport
oraz nowy plik testu, bez źródeł produkcyjnych. Przeczytano oba pliki i
niezmienny zakres commita. Porównanie produkcji kandydata z kanonicznym
`7bebd00419c8febbce50b5f8fbeded9ed70df03d` (`git diff --exit-code ... -- wxlite/amc_wx_lite`)
nie wykazało różnic. Wykonawca zgłasza 68 zaliczeń w zestawie powyżej;
rodzic nie powtarzał całego nakładającego się wyboru.

Rodzic niezależnie wykonał nowy test graniczny na zamrożonym worktree:
`PYTHONPATH=wxlite:wxlite/tests uv run --offline --with pytest python -m pytest wxlite/tests/test_radioarchiwista_native_prefix_contract.py -q --basetemp /home/michal/.hermes/profiles/amc/cache/scratch/rarch-native-prefix-parent-check -p no:cacheprovider`
— **2 passed in 0.39s**, exit 0. Sprawdzono prawdziwy hook i łańcuch
producent/nawigator/renderowanie, nie zachowanie natywnej kontrolki.

Przyjęto i przeniesiono wyłącznie testy/dokumentację jako `a9b0e20`.
Początkowy `git diff --check` wykazał dwa końcowe odstępy twardego łamania
Markdown przy dacie; usunięto je w dokumentacji. Bez zmian wykonania aplikacji.
**Nie odtworzono ani nie naprawiono zgłoszonej nawigacji Windows.** Otwarta
pozostaje uzgodniona próba rzeczywistego ListCtrl/NVDA; nie wykonywać jej
automatycznie bez okna GUI. Historia/zakładki/procenty mają osobnego wykonawcę.

## Testy

Dodano test graniczny `wxlite/tests/test_radioarchiwista_native_prefix_contract.py`:

- realny producent danych + realny `Navigator` zachowują surowe prefiksy mimo statusu/gwiazdki;
- realny listowy handler przekazuje literalne `a`, `7` i `0` przez `Skip()` i nie wpuszcza ich do poleceń AMC.

Polecenie bez GUI:

```text
PYTHONPATH=wxlite:wxlite/tests uv run --offline --with pytest python -m pytest wxlite/tests/test_radioarchiwista_native_prefix_contract.py wxlite/tests/test_radioarchiwista_source.py wxlite/tests/test_radioarchiwista_session.py wxlite/tests/test_playback_activity.py wxlite/tests/test_list_reading_options.py -q --basetemp /home/michal/.hermes/profiles/amc/cache/scratch/amc-radioarchiwista-hours-20261119-test-native-prefix -p no:cacheprovider
```

Test sąsiedni `test_player_arrow_hook.py` nie jest obecnie uruchamialny samodzielnie: jego AST-harness kompiluje metodę z adnotacją `wx.KeyEvent`, ale nie przekazuje `wx` do własnego namespace, co kończy się `NameError` przed testowaniem zachowania. To wada istniejącego harnessu, niezwiązana z tą zmianą; nie została zmieniona poza zakresem.
