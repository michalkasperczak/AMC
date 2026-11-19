# Niezależny delta review — Hubert 25, korekta harmonogramu

**Werdykt: PRZYJĄĆ kod 5f0242ff5e20c0c51999438349c4688407f9bf6d** względem bazy `c3e140401aad1eb08bfee8a597cc073abcb50ef8`.

## Zakres i tożsamość

- Przejrzano wyłącznie 12 plików delty B1/B2; nie wracano do P0 ani PR-27/PR-29.
- Zamrożony worktree `amc-hubert25-schedule-correction-20261119` wskazuje dokładnie `5f0242ff5e20c0c51999438349c4688407f9bf6d`, baza jest przodkiem, a `git status --porcelain` był pusty.
- `git diff --check c3e1404..5f0242f` nie zgłosił błędów.

## B1 — zachowanie starszego wywołania

- Pythonowy wrapper bez argumentu wysyła dokładnie `{}`; z argumentem wysyła wyłącznie `cadenceSeconds`, ograniczony do `1..86400`. Wywołanie GUI pozostaje jawne (`automatic_interval`), więc nie traci adaptacyjnego harmonogramu.
- Rzeczywisty dispatcher rozróżnia **obecność** `cadenceSeconds`: brak pola prowadzi do `RefreshAutomatic()` i pełnego `automatic_refresh_batch_size`; obecne `0` idzie nadal przez `LiteArgs.ReadInt` i zostaje ograniczone do 1 s.
- Nowy HandlerTests przechodzi przez produkcyjne `LiteEngineHandlers.Build()` i prawdziwy koordynator, nie przez atrapę dispatchera. Projekt referencjonuje faktyczny LiteHost, a `InternalsVisibleTo` zgadza się z jego assembly name.

## B2 — admission przy anulowaniu

- `TryMarkRefreshAttempt` zachowuje zmiany w jednej transakcji: po potencjalnym oczekiwaniu na SQLite oraz po UPSERT, bezpośrednio przed commitem, ponownie odczytuje token. Powrót `false` przed commitem kończy `using` transakcji, a więc nie utrwala znacznika.
- Koordynator zwiększa `requested` dopiero po zwróceniu `true`; przy `false` ustawia `budgetExpired` i nie oznacza następnego celu. Ścieżka ręczna pozostaje przy dotychczasowym `MarkRefreshAttempt`.
- Nie zmieniono schematu: w obu commitach `DatabaseSchemaVersion` i `PRAGMA user_version` pozostają równe 4.
- Przyjęta granica jest właściwie ograniczona: anulowanie po ostatnim odczycie tokenu, lecz przed lub po commitcie, jest prawidłowo traktowane jako anulowanie już przyjętego zadania in-flight, nie jako admission po deadline.

## Pokrycie i otwarta granica aparatury

Nie znalazłem blokera produkcyjnego. Test `BEGIN EXCLUSIVE` sprawdza wynik `false` i zero trwałych wpisów po zwolnieniu blokady; to pokrywa wymagany skutek B2. Jednak jego ujemne `Wait(25 ms)` samo w sobie nie dowodzi deterministycznie, że worker już wszedł w ścieżkę SQLite przed anulowaniem — przy zimnym JIT/schedulerze mógłby jeszcze nie dostać czasu procesora.

To nie jest regresja w 5f ani powód do rozszerzania globalnego potoku anulowania. Jeśli trwająca osobno naprawa harnessu ma doprecyzować ten dowód, wystarczy jedna asercja graniczna w aparaturze: testowy `TaskCompletionSource`, sygnalizowany po otwarciu połączenia admission i przed `EnsureSchema`, musi zostać osiągnięty przed anulowaniem; po nim test ma wymagać, że przy nadal trzymanym `BEGIN EXCLUSIVE` admission nie jest ukończone. Nie należy dodawać do produkcji ogólnego mechanizmu hooków ani obiecywać anulowalnego oczekiwania SQLite.

## Weryfikacja

Nie powtarzałem broad-suite ani krytycznego testu handlera wykonanych przez rodzica dla tego samego zamrożonego commitu. Ten review obejmuje statyczne prześledzenie wszystkich nowych symboli i konsumentów oraz tożsamość/projektową integrację testu. Odbiór runtime pozostaje zależny od końcowego GREEN niezależnie naprawianego harnessu deadline 25 ms; nie jest to blocker tej akceptacji kodu 5f.
