# Hubert 25 — korekta harmonogramu (2026-11-19)

## Zakres

Korekta zachowuje kontrakt starszego wywołania `podcast.refreshAutomatic` bez
argumentów oraz ogranicza obsługę wygaśniętego budżetu do admission znacznika
próby odświeżenia. Nie zmienia planowania 30 s/75 s, adaptacyjnego limitu dla
jawnego taktu, fairness, ręcznego odświeżania ani ścieżek pobierania.

## B1 — legacy bez argumentu

- `LiteHostClient.refresh_podcasts_automatically()` bez argumentu wysyła `args: {}`.
- Przy jawnym interwale wrapper wysyła `cadenceSeconds`, ograniczone do
  `1..86_400`; `5` minut daje `300` sekund.
- Rzeczywisty handler rozróżnia obecność pola: brak przechodzi do
  `RefreshAutomatic()` (legacy `automatic_refresh_batch_size`), zaś obecne
  `cadenceSeconds` do adaptacyjnego `RefreshAutomatic(TimeSpan)`. Jawne `0`
  pozostaje wartością jawną i jest ograniczone przez `LiteArgs.ReadInt` do 1 s;
  nie jest mylone z brakiem argumentu.

`AccessibleMediaController.LiteHost.HandlerTests` przechodzi przez prawdziwe
`LiteEngineHandlers.Build()` i koordynator, na pięciu należnych źródłach:

| args | oczekiwane `requested` |
| --- | ---: |
| `{}` | 4 (legacy pełny limit) |
| `{"cadenceSeconds":60}` | 1 (adaptacyjny limit) |
| `{"cadenceSeconds":0}` | 1 (walidacja jawną wartością) |

RED został sprawdzony na tym samym stagingu po przywróceniu dawnego handlera,
który zawsze czytał domyślne `cadenceSeconds=60`: test zakończył się błędem
`oczekiwano 4, otrzymano 1`. Po odtworzeniu wersji korekty test przeszedł:
`LiteHost automatic refresh dispatcher: OK`.

## B2 — granica admission podczas blokady SQLite

Dodano `TryMarkRefreshAttempt` przez warstwy bazy, mutacji, store'a i
koordynatora. Admission to pojedyncza transakcja zapisu znacznika próby.
Przed rozpoczęciem oraz po realnym oczekiwaniu na dostęp do SQLite token jest
sprawdzany; po `INSERT ... ON CONFLICT` jest sprawdzany ponownie bezpośrednio
przed `Commit`. Gdy token jest już anulowany, transakcja jest wycofywana przez
`Dispose` i metoda zwraca `false`; koordynator nie zwiększa `requested` i nie
przyjmuje kolejnego źródła. Nie ma rollbacku ani przywracania prób innych
procesów.

Test `TestAutomaticAdmissionDoesNotPersistAfterDeadlineDuringWriteLock` bierze
prawdziwy `BEGIN EXCLUSIVE`, uruchamia admission w osobnym zadaniu, potwierdza,
że nie kończy się ono przez 25 ms, anuluje token przed `COMMIT` blokady i potem
sprawdza zarówno `false`, jak i zero wierszy dla `sub-1` w
`podcast_refresh_attempts`.

RED został sprawdzony na tym samym teście przez zastąpienie admission dawną
ścieżką `MarkRefreshAttempt(...); return true;`. Test zakończył się błędem:
`po wygaśnięciu budżetu podczas oczekiwania przyjęcie nie może zostać zapisane`.
Po odtworzeniu transakcyjnego admission `--podcast-progress` przeszedł.

### Dokładna granica

Test dowodzi tylko granicy wymaganej dla odroczonego celu: token anulowany
podczas oczekiwania na blokadę zapisu nie prowadzi do trwałego admission po
zwolnieniu blokady. Punktem decyzji admission jest ostatni odczyt tokenu przed
`Commit`; po tej decyzji źródło jest przyjęte. Anulowanie już po tym punkcie,
także przed późniejszym socketem, jest legalnym anulowaniem in-flight i nie
jest dowodem „admit after deadline”. Nie jest to atomowa wspólna operacja
`CancellationToken` i SQLite ani gwarancja niemożliwości anulowania dokładnie
podczas samego `Commit`; takiej własności test nie zgłasza.

## Baza i zgodność

Nie dodano tabel ani migracji. `PodcastLibraryDatabase.DatabaseSchemaVersion`
pozostaje **4** (`PRAGMA user_version = 4`). Wykorzystany jest istniejący
`podcast_refresh_attempts`.

## Weryfikacja

- Python: `62 passed in 4.92s` — `test_podcast_refresh.py` oraz
  `test_host_client.py` przez offline `uv` i jawne `PYTHONPATH`.
- Handler: po czystym przebudowaniu `LiteHost automatic refresh dispatcher: OK`.
- Protocol: `--podcast-progress` przeszedł po GREEN (i dwa końcowe powtórzenia
  po rozgrzaniu artefaktów przeszły).
- Core smoke: `OK: atomowy zapis subskrypcji YouTube i seria podcastów
  (9 scenariuszy)`.
- Rzeczywisty build `AccessibleMediaController.LiteHost` w NTFS stagingu:
  0 ostrzeżeń, 0 błędów.
- `git diff --check`: bez błędu.

Staging buildów: `C:\Users\Michal\AppData\Local\AMC-Hermes-Build\hubert25-schedule-correction-20261119`.

## Komit

Base: `c3e140401aad1eb08bfee8a597cc073abcb50ef8`.
