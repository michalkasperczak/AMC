# Radio Archiwista — ograniczony odbiór wspólnego odtwarzacza

**Status: zakończony ograniczony odbiór kodu i testów offline (19.11.2026).**

Nie był to odbiór GUI/NVDA ani odtwarzania audio. Nie utworzono wydania, instalacji ani publikacji.

## Granice i niezmieniony kontrakt

Lista audycji Radio Archiwisty jest zwykłą listą nazw plików z serwera: litery i cyfry pozostają literalnym wybieraniem nazwy. Zaznaczenie nie uruchamia odtwarzania ani przewijania. W widoku odtwarzacza cyfry `0`–`9` oznaczają odpowiednio `0%`–`90%`. Nie dodano interpretowania godzin, aliasu `7 → 07`, osobnego selektora godzin ani funkcji live/time-shift.

Sprawdzano tylko gotowe nagrania Radio Archiwisty i wspólne mechanizmy Plików/Podcastów: bez kont, urządzeń, rozdziałów dostawcy, GUI, NVDA, audio i sieci.

## Stan wejściowy

- izolowany worktree: `/home/michal/.hermes/profiles/amc/cache/scratch/amc-radioarchiwista-player-20261119`
- gałąź: `codex/amc-radioarchiwista-player-20261119`
- baza: `7bebd00419c8febbce50b5f8fbeded9ed70df03d`
- LiteHost: `LiteTransportRouting.cs` jawnie mapuje `radioarchiwista` na silnik `files`, zachowując identyfikator sesji; nie ma drugiego odtwarzacza Archiwisty.

## Macierz funkcji i dowodów

| Obszar | Rzeczywista ścieżka / dowód | Wynik ograniczonego odbioru |
|---|---|---|
| Historia `Ctrl+H` | resolver `shortcuts.resolve` → `LiteFrame._dispatch(Action.VIEW_HISTORY)` → `_show_playback_history_preview` → `streamer_history_rows` / `Navigator.apply_service_history` | Jest. Nowy test prowadzi `Ctrl+H` z listy i odtwarzacza do historii **radioarchiwista**, bez przełączenia na Pliki. |
| Historia: trwałość, deduplikacja, Enter, Escape | `test_streamer_playback_history.py`: `record_row`, `normalize_histories`, `history_rows`, `Navigator.activate_selected`, `_restore_transient_preview` | Jest: wpis replayable ma URL, Enter tworzy `PlayMedia`, Escape wraca do tej samej sesji i zaznaczenia. C# `--playback-history` zielony. |
| Zakładka szybka | `LiteFrame._add_bookmark` → `client.add_bookmark(item_id, item_title, session_id, name)` | Jest. Test sprawdza stabilne `rarch:rec:*` i `session_id="radioarchiwista"`. |
| Zakładki nazwane / dalej-wstecz / tylko nazwane | resolver → `_dispatch` → `_navigate_bookmark` → `client.navigate_bookmark(direction, session_id, named_only)` | Jest. Nowy test wykonuje dispatcher i sprawdza `(1, "radioarchiwista", True)` oraz pozycję zwróconą przez wspólny host. C# `--bookmarks` zielony. |
| Izolacja magazynu zakładek | wspólny `BookmarkStore`, testy C# `BookmarkStoreTests` | Jest. Host nie zamienia sesji na `local`; nie wprowadzono osobnej bazy Archiwisty. |
| Cyfry w odtwarzaczu | resolver (tylko `player_view=True`) → `_dispatch` → `_seek_percent` → `client.seek_to_position(target, session_id)` | Jest. Nowy test prowadzi `50%` z 600 s do `seek_to_position(300.0, "radioarchiwista")`. |
| Cyfry na liście | resolver dla `player_view=False` zwraca `None` | Jest. Nowa regresja obejmuje wszystkie cyfry `0`–`9`: nie powstaje akcja play/seek, więc klawisz pozostaje natywnej liście dla przyrostowego wyboru nazwy. |
| Skok bez długości | `_seek_percent` zatrzymuje się przed wywołaniem hosta i używa `percent_seek_text` | Jest. Nowy test w sesji Archiwisty potwierdza brak requestu i komunikat „Skok procentowy niedostępny: czas trwania jest nieznany”, nawet przy wyłączonych komunikatach rutynowych. |
| Start/pauza/stop, względny/bezwzględny seek, początek/koniec, czas/status | wspólny `LiteFrame` przekazuje `navigator.active.value`; LiteHost routuje sesję do `files` | Brak potwierdzonej rozbieżności Archiwisty. Parytet transportu zweryfikowano testami GUI bez okna oraz C# routingiem, ale nie jest to odtwarzanie audio. |
| Głośność, mute, tempo i wspólne ustawienia audio | wspólne handlery `set_volume`, `toggle_mute`, `set_rate` z jawnym `session_id`; `test_transport_parity_gui_wiring.py` | Brak potwierdzonej rozbieżności Archiwisty. Test atrapy uzupełniono o rzeczywisty prerekwizyt `LiteState`, wymagany przez wspólną ścieżkę tempa. |
| Kolejka i resume | `RadioarchiwistaSource.toggle_queue`, `continue_radioarchiwista_queue_after_end`, zapis pozycji przy pauzie | Jest w zakresie nagrań zdalnych: kolejka trwała, następne nagranie używa zdalnego URL; pamięć pozycji nie dostała osobnego magazynu. |
| Menu, paleta i opisy | `RADIOARCHIWISTA_SUPPORTED_ACTIONS`, `menu_model.menus_for_session`, `entries_for_session`, `describe_action` | Nie zmieniano źródła prawdy: odkryte akcje są dopuszczone i dispatcher je obsługuje. Test istniejący obejmuje menu/paletę kolejki i linku; nie stwierdzono martwej pozycji dla badanych funkcji. |

## RED/GREEN i poprawki

Nie wykryto funkcjonalnej luki odtwarzacza Radio Archiwisty, więc zgodnie z zakresem nie zmieniono produkcyjnego `gui.py`, klienta hosta, nawigacji ani C# LiteHost.

Jedyny potwierdzony RED był testowym prerekwizytem po wspólnym przyroście GUI, nie usterką funkcji:

1. **RED:** pierwszy przebieg `test_radioarchiwista_followup.py`: `11 passed, 1 failed`, exit `1`; `test_open_archive_day_list_relocalizes_through_saved_language_change` nie wyposażał atrapy w `_native_playback_snapshot` (a po dalszym przebiegu także snapshoty WiiM/Sonos), których wymaga odświeżenie wspólnych oznaczeń odtwarzania.
2. **GREEN:** atrapa otrzymała neutralny stan tych snapshotów; asercji nie usunięto i produkcji nie dostosowano do atrapy.
3. Podczas rozszerzania dowodów faktyczny runner ujawnił jeszcze dwie niepełne atrapy: nowe sondy dispatchera muszą być instancją `LiteFrame` bez `__init__`, a wspólny fixture parytetu tempa musi mieć `LiteState`, tak jak prawdziwa ramka. To także były błędy wyposażenia testu, nie RED funkcji odtwarzacza.

Dodane regresje mierzą pełne drogi, nie same stałe: `Ctrl+H` resolver → dispatcher → historia sesji; nawigację nazwanych zakładek dispatcher → klient; skok procentowy dispatcher → klient; brak długości oraz brak seeka na liście.

## Wykonane polecenia i wyniki

Python (offline, bez GUI):

```text
PYTHONPATH=wxlite:wxlite/tests uv run --offline --with pytest python -m pytest \
  wxlite/tests/test_radioarchiwista_followup.py \
  wxlite/tests/test_streamer_playback_history.py \
  wxlite/tests/test_transport_parity_gui_wiring.py \
  wxlite/tests/test_quick_bookmark.py \
  -q --tb=short \
  --basetemp /home/michal/.hermes/profiles/amc/cache/scratch/rarch-player-contract-py-final \
  -p no:cacheprovider
```

Wynik: **73 passed in 0.71s**, exit `0`.

C# (prywatny, już dostępny SDK Windows `8.0.423`, staging NTFS; bez instalacji):

```text
/mnt/c/Users/Michal/AppData/Local/AMC-BuildTools/dotnet/dotnet.exe build \
  src\\AccessibleMediaController.LiteHost\\AccessibleMediaController.LiteHost.csproj --no-restore -v:minimal
```

Wynik: kompilacja LiteHost udana, `0` ostrzeżeń, `0` błędów, exit `0`.

```text
/mnt/c/Users/Michal/AppData/Local/AMC-BuildTools/dotnet/dotnet.exe run --project \
  tests\\AccessibleMediaController.LiteHost.ProtocolTests\\AccessibleMediaController.LiteHost.ProtocolTests.csproj \
  --no-restore -- --transport-routing --bookmarks --playback-history
```

Wynik: `TransportRoutingTests: OK`, `PlaybackHistoryMutationTests: OK`, `Wszystkie zestawy przeszly (3)`, exit `0`. Zestaw zakładek jest trzecią wybraną suite; jego runner nie emituje własnej linii sukcesu.

## Zmienione pliki

- `wxlite/tests/test_radioarchiwista_followup.py` — naprawa niepełnej atrapy oraz regresje rzeczywistych dróg historii, zakładek i skoku procentowego.
- `wxlite/tests/test_transport_parity_gui_wiring.py` — `LiteState` w istniejącym fixture wspólnej ścieżki tempa.
- `docs/zadania/RARCH-PLAYER-20261119-kontrakt.md` — ten raport/macierzy dowodów.

Nie zmieniono kodu produkcyjnego. Nie uruchomiono GUI, NVDA, audio ani odtwarzania. Nie wykonano instalacji, push, merge, cherrypick, wydania, publikacji ani zmian kont/sekretów.

## Integracja

Nie ma zmiany wspólnej metody do ręcznej integracji z pracą nawigacji listy ani rozdziałów YouTube. Zmiany testów i dokumentu można brać niezależnie; funkcjonalny routing klawiszy godzinowych nie został dotknięty.

## Niezależny odbiór rodzica i integracja — 19.11.2026

Rodzic odczytał pełny diff zamrożonego `c3f2eb92831caddfb2279221fdc0970a448a8467`
oraz raport. Potwierdził brak różnic kodu produkcyjnego względem bieżącej
bazy. Zintegrowano wyłącznie dwa pliki testów i dokument jako `8ff468c`.
Konflikt dotyczył wyłącznie nieaktualnego akapitu dokumentacji o wykonawcy
„w toku”; nie było konfliktu ani zmiany produkcji.

Niezależnie wykonano dwa zmienione moduły Python przed integracją:
**41 passed in 0.41s**, a po integracji **41 passed in 0.86s**, exit 0.
To nakładające się wybory, nie dodatkowe 82 scenariusze i nie pełna suite.
Polecenie po integracji:

```text
PYTHONPATH=wxlite:wxlite/tests uv run --offline --with pytest python -m pytest wxlite/tests/test_radioarchiwista_followup.py wxlite/tests/test_transport_parity_gui_wiring.py -q --tb=short --basetemp /home/michal/.hermes/profiles/amc/cache/scratch/rarch-player-parent-integrated -p no:cacheprovider
```

Porównano bajtowo 744 śledzone pliki `.cs`/`.csproj` z `src` i `tests`
między kanonicznym repo a stagingiem Windows
`C:\Users\Michal\AppData\Local\AMC-AgentStaging\rarch-player-7bebd004`:
zero różnic. Rodzic uruchomił rzeczywisty runner
`tests\AccessibleMediaController.LiteHost.ProtocolTests\bin\Debug\net8.0\amc_lite_protocol_tests.dll`
prywatnym `dotnet.exe` z flagami
`--transport-routing --bookmarks --playback-history`: **3 zestawy PASS**, exit 0.
Nie ponawiano całej kompilacji; jej wynik pozostaje dowodem wykonawcy.
Początkowe szukanie DLL po nazwie projektu, a nie jawnym `AssemblyName`,
nie znalazło runnera; po odczycie csproj użyto poprawnej nazwy. Nie był to
nieudany test produktu.

Przyjęto ograniczony kontrakt kodu/offline, nie fizyczne odtwarzanie,
GUI/NVDA, natywny Windows typeahead, instalację ani publikację. Nie wykryto
potwierdzonego braku wymagającego zmiany produkcji. Nowa praca nad utratą
wyjścia audio ma osobny kontrakt AUDIO-RECOVERY-20261119 i nie jest odebrana
przez powyższe testy.
