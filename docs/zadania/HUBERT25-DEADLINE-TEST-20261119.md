# Hubert 25 — deterministyczny test deadline automatycznego odświeżania (2026-11-19)

## Cel i zakres

Ten etap usuwa wyłącznie zależność testu
`TestAutomaticRefreshDeadlineDefersRemainingSources` od rzeczywistego zegara 25 ms,
CPU i zimnego JIT. Nie zmienia kodu produkcyjnego harmonogramu, admission SQLite,
fetchowania ani kontraktu odpowiedzi hosta.

Zmieniono tylko:

- `tests/AccessibleMediaController.LiteHost.ProtocolTests/PodcastProgressStoreTests.cs`;
- ten raport.

## Ustalenie błędu i granica dowodu

Poprzedni test przekazywał `automaticTimeBudget: 25 ms` bez `TimeProvider`, a
atrapa fetch czekała nieskończenie na token. Przy zimnym uruchomieniu czas
rzeczywisty mógł upłynąć jeszcze podczas pracy SQLite przed wejściem do fetch;
wtedy `TryMarkRefreshAttempt` legalnie odmawia admission, a wynik ma
`requested=0`. To nie jest dowód braku anulowania in-flight fetch, mimo że dawny
test wymagał `requested=1`.

Sprawdzono lokalnie przez Pythona pełny tekst
`/home/michal/.hermes/profiles/amc/cache/delegation/live/deleg_3e935600/task-0.log`.
Zapis zawiera wynik B2 z pełnym stackiem, ale nie zachowuje pełnego historycznego
wiersza `TestAutomaticRefreshDeadlineDefersRemainingSources`: odpowiedź narzędzia
została w nim uprzednio skrócona jako `…(+… chars)`. Dlatego nie przypisuje się
mu tu niezweryfikowanego kompletnego stacka ani twierdzenia „existing/unrelated”.
Zachowana informacja o historycznym objawie `requested=0` pochodzi z przekazania
etapu; przyczynę i nową granicę potwierdza kontrolowany test poniżej.

Test B2 `TestAutomaticAdmissionDoesNotPersistAfterDeadlineDuringWriteLock` nadal
jest deterministycznym testem negatywnego oczekiwania na blokadę SQLite. Sam fakt,
że oczekiwanie nie kończy się przez 25 ms, nie dowodzi uruchomienia `StartTask` ani
nie jest używany jako taki dowód w tym etapie.

## Aparatura i asercje

Dodano minimalny testowy `ControlledTimerTimeProvider`. Przechwytuje dokładnie
jeden timer utworzony przez `CancellationTokenSource(TimeSpan, TimeProvider)`,
zapisuje jego `dueTime` i wywołuje callback wyłącznie przez jawne `Fire()`.
Nie jest schedulerem produkcyjnym i nie przesuwa czasu ściennego.

`TestAutomaticRefreshDeadlineDefersRemainingSources` tworzy nadal dwa należne
źródła i budżet 25 ms, ale:

1. potwierdza, że rzeczywisty CTS zażądał timera o `dueTime == 25 ms`;
2. rozpoczyna pierwszy fetch dopiero po skutecznym admission;
3. wewnątrz pierwszego fetch wywołuje `Fire()`, rejestruje anulowanie tokenu i
   potem czeka przez `Task.Delay(Timeout.InfiniteTimeSpan, token)`;
4. potwierdza `fetchStarted`, anulowanie tokenem, `requested=1`, `dueSources=2`,
   `deferredDueSources=1` i `budgetExpired=true`.

Zwykły GREEN nie czeka 25 ms ani na realny deadline. Jednosekundowy
`Task.WhenAny` jest wyłącznie ograniczonym watchdogiem kontroli dyskryminującej:
nie zastępuje deadline'u i zapobiega zawieszeniu, gdy fetch dostałby błędny token.

## Kontrola dyskryminująca

W wyłącznym stagingu NTFS celowo zmieniono jedyne wywołanie w
`LitePodcastRefreshCoordinator.RefreshCore`:

```csharp
_fetch(target, cancellationToken)
```

na:

```csharp
_fetch(target, CancellationToken.None)
```

Kotwica była pełną linią w `RefreshCore`; liczba wystąpień przed podmianą wyniosła
1. `--podcast-progress` zakończył się kodem 1 i komunikatem:

```text
NIEPOWODZENIE --podcast-progress: atrappa fetch nie dostała tokenu anulowanego przez deadline
```

Stagingowy plik źródłowy został przywrócony w `trap` przed zakończeniem polecenia.
Mutacja więc rozróżnia brak przekazania tokenu do rzeczywistego fetch od poprawnej
ścieżki anulowania, zamiast polegać na zielonym payloadzie.

## Weryfikacja

- Materiałizacja do własnego stagingu
  `C:\Users\Michal\AppData\Local\AMC-Hermes-Build\hubert25-deadline-test-20261119`:
  `source-tracked=3048`, `stage-files=3048`, bez braków i nadmiarów.
- Pierwszy GREEN w stagingu: `--podcast-progress` — `Wszystkie zestawy przeszly (1)`.
- Kontrola mutacyjna: opisany wyżej deterministyczny RED, kod 1.
- Końcowy GREEN po przywróceniu źródła: staging ponownie zmaterializowano z
  `3049` plików śledzonych, usunięto 4 katalogi `bin`/`obj`, wykonano restore i
  rzeczywisty rebuild `--podcast-progress`; wynik: `Wszystkie zestawy przeszly (1)`.
  SHA-256 finalnego `LitePodcastRefreshCoordinator.cs` jest identyczne w stagingu
  i worktree: `bfd5bef1500b4ee21dfede786d7254782b188983ffc915dfbf2d98291b48d2f5`.

## Materiał do dokumentacji użytkownika

Brak zmiany użytkowej: automatyczne odświeżanie i jego komunikaty nie zostały
zmienione. Etap wzmacnia wyłącznie wiarygodność testu regresji.

## Znane ograniczenia

Test dowodzi przekazania tokenu i anulowania pierwszego fetch po admission oraz
odroczenia kolejnego należnego źródła. Nie jest testem atomowości `CancellationToken`
i SQLite w samym `Commit`; tę węższą granicę nadal opisuje osobny test B2.

## Wersja i commit

- Gałąź: `codex/amc-hubert25-deadline-test-20261119`.
- Baza: `5f0242ff5e20c0c51999438349c4688407f9bf6d`.
- Commit: do uzupełnienia po końcowym GREEN i sprawdzeniu czystego drzewa.
