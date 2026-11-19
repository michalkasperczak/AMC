# Hubert #25 — ograniczone nadrabianie i harmonogram porcji — 19.11.2026

## Wersja i commit

- Gałąź: `codex/amc-hubert25-schedule-20261119`.
- Baza: `ac203b6d8d7731d3b5a9f283ac9d93dcc397dbd1`.
- Commit wdrożenia: `HEAD` gałęzi `codex/amc-hubert25-schedule-20261119` (pełny SHA jest w raporcie wykonania).
- Nie tworzono instalatora, nie instalowano ani nie publikowano.

## Zakres i decyzje

1. Po uruchomieniu automatyczne sprawdzanie ma jeden ograniczony termin catch-up: maksymalnie po **30 sekundach**, albo wcześniej, jeżeli ustawiony takt jest krótszy. `0` nadal wyłącza automat i zwraca nieskończony termin. Po każdym przebiegu nadal obowiązuje cały zapisany takt użytkownika — nie ma gorącej pętli zaległości.
2. `automatic_refresh_batch_size` zachowuje istniejącą semantykę **twardego maksimum** porcji: nie zmieniono jego wartości, walidacji ani ustawień GUI. Docelowa liczba jest wyliczana jako sufit sumy `takt / interwał źródła`, ograniczony tym maksimum i liczbą aktualnie należnych źródeł. GUI przesyła hostowi swój zapisany takt wyłącznie jako argument bieżącej automatycznej operacji; nie zapisuje go do bazy.
3. Podczas pierwszego nadrabiania nigdy nieodświeżone źródła są wybierane przed ponownym sprawdzeniem szybszego źródła. To zapobiega zagłodzeniu końcowych, wolniejszych kanałów; nie zmienia ich interwałów ani nie włącza źródeł z interwałem `0`.
4. Jeden automatyczny przebieg ma twardy budżet **75 s**. Token anulowania jest przekazywany do RSS, YouTube/yt-dlp i Facebooka, więc limit przerywa trwające pobranie, a nie tylko wybór następnego kanału. Przerwane wskutek budżetu źródło zachowuje zwykłe oznaczenie próby i cooldown; następnych źródeł przebieg nie oznacza próbą.
5. Odpowiedź automatu zawiera telemetrię `dueSources`, `targetedSources`, `requested`, `deferredDueSources` i `budgetExpired`. `requested` oznacza rzeczywiście rozpoczęte pobrania; odroczone źródła nie są przedstawiane jako przetworzone. Nie dodano kontrolki, skrótu ani dodatkowej rutynowej mowy.
6. Zachowano dotychczasową blokadę aktywnego lub nieznanego nagrywania, świeże sprawdzenie stanu tuż przed dispatch oraz blokadę nakładającego się odświeżenia automatycznego/ręcznego.

## Zmienione pliki

- `src/AccessibleMediaController.Core/Configuration/PodcastLibraryDatabase.cs`
- `src/AccessibleMediaController.Core/Configuration/PodcastLibraryMutationStore.cs`
- `src/AccessibleMediaController.LiteHost/LiteEngineHandlers.cs`
- `src/AccessibleMediaController.LiteHost/LitePodcastProgressStore.cs`
- `src/AccessibleMediaController.LiteHost/LitePodcastRefreshCoordinator.cs`
- `tests/AccessibleMediaController.LiteHost.ProtocolTests/PodcastProgressStoreTests.cs`
- `wxlite/amc_wx_lite/gui.py`
- `wxlite/amc_wx_lite/host_client.py`
- `wxlite/tests/test_host_client.py`
- `wxlite/tests/test_podcast_refresh.py`

## RED → GREEN i testy

1. RED: nowy test startowego catch-up zakończył się `AttributeError` dla nieistniejącego `podcast_automatic_refresh_due`.
   GREEN: po dodaniu czystej funkcji terminu: `1 passed in 1.96s`.
2. RED: Windows Protocol test harmonogramu nie kompilował się, bo nie było `GetDueRefreshPlan`.
   GREEN: po implementacji planu 122 kanałów o interwałach 10/60 minut oraz ochrony przed zagłodzeniem: `Wszystkie zestawy przeszly (1).`
3. RED: Windows Protocol test deadline nie kompilował się, bo koordynator nie przyjmował budżetu ani kadencji.
   GREEN: test przekazuje token do kontrolowanego oczekiwania, które zostaje anulowane; odpowiedź raportuje `requested=1`, `dueSources=2`, `deferredDueSources=1`, `budgetExpired=true`.

Końcowe, celowane sprawdzenia:

```text
PYTHONPATH=wxlite:wxlite/tests uv run --offline --with pytest python -m pytest \
  wxlite/tests/test_podcast_refresh.py wxlite/tests/test_host_client.py -q \
  --basetemp /home/michal/.hermes/profiles/amc/cache/scratch/pytest-hubert25-schedule-final \
  -p no:cacheprovider
62 passed in 5.36s

C:\Users\Michal\AppData\Local\AMC-BuildTools\dotnet\dotnet.exe run \
  --project tests\AccessibleMediaController.LiteHost.ProtocolTests\AccessibleMediaController.LiteHost.ProtocolTests.csproj \
  -c Release -- --podcast-progress
Wszystkie zestawy przeszly (1).

C:\Users\Michal\AppData\Local\AMC-BuildTools\dotnet\dotnet.exe run \
  --project tests\AccessibleMediaController.Core.SmokeTests\AccessibleMediaController.Core.SmokeTests.csproj \
  -c Release -- --youtube-subscription-sync-apply
OK: atomowy zapis subskrypcji YouTube i seria podcastów (9 scenariuszy)

C:\Users\Michal\AppData\Local\AMC-BuildTools\dotnet\dotnet.exe build \
  src\AccessibleMediaController.LiteHost\AccessibleMediaController.LiteHost.csproj \
  -c Release --no-restore --nologo
Kompilacja powiodła się. Ostrzeżenia: 0. Liczba błędów: 0.
```

Wszystkie komendy Windows działały z własnego stagingu NTFS
`C:\Users\Michal\AppData\Local\AMC-Hermes-Build\hubert25-schedule-20261119` i prywatnego SDK 8.0.423.

## Materiał do dokumentacji użytkownika

Jeżeli automatyczne odświeżanie Podcastów i YouTube jest włączone, AMC po starcie może szybko sprawdzić ograniczoną porcję już należnych źródeł, zamiast zawsze czekać do końca całego taktu. Nie zmienia to ustawionych przez użytkownika odstępów RSS, YouTube ani automatu, a wartość `0` nadal oznacza tylko odświeżanie ręczne przez F5/Ctrl+F5. Nagrywanie nadal ma pierwszeństwo: w czasie nagrywania automatyczne sprawdzanie jest odłożone. Nie ma nowego skrótu, kontrolki ani zapowiedzi czytnika.

## Znane ograniczenia

- To nie jest usługa działająca po zamknięciu AMC, RSS-first ani cache/range.
- Nie wykonano żywego YouTube, kont, odtwarzania, GUI/NVDA, instalacji ani publikacji.
- Budżet anuluje klientów honorujących przekazany token; test potwierdza pełną drogę tokenu do kontrolowanego pobrania oraz źródłowego klienta YouTube/yt-dlp. Nie jest to pomiar czasu ani obciążenia na bibliotece użytkownika.
