# #25 P0 — wąski zapis odświeżenia celu

Data: 2026-11-19
Worktree: `amc-hubert25-target-store-20261119`
Zakres: tylko trwały zapis odświeżenia Podcastów/YouTube; bez harmonogramu, GUI, klienta YouTube, instalatora i publikacji.

## Problem

`PodcastLibraryDatabase.ApplyRefreshBatch` materializował przez `LoadInto` wszystkie subskrypcje i odcinki, a po zastosowaniu feedów wykonywał globalne `Save`. Wywołanie pojedynczego `ApplyRefresh` z LiteHostu szło przez tę samą drogę.

## RED

Dodano `PodcastTargetedRefreshStoreTests` uruchamiany przełącznikiem `--podcast-targeted-refresh`.

Test przygotowuje bazę z celem i rekordami poza celem. Payload subskrypcji poza celem jest celowo niepoprawnym JSON-em (`{nie jest ...}`), podobnie payload jej odcinka. W starej implementacji test zakończył się kodem 1:

```text
System.Text.Json.JsonException: 'n' is an invalid start of a property name.
... PodcastLibraryDatabase.LoadInto(...): line 115
... PodcastLibraryDatabase.ApplyRefreshBatch(...): line 1650
```

To jest deterministyczny dowód niepożądanego globalnego odczytu/deserializacji, nie pomiar czasu na małej fixture.

## GREEN i granica transakcji

`ApplyRefreshBatch` wykonuje teraz `ApplyTargetedRefresh` osobno dla każdego żądania. Jedna taka operacja:

1. otwiera transakcję SQLite;
2. czyta wyłącznie wybraną subskrypcję po `id` oraz jej odcinki po `subscription_id`;
3. stosuje istniejący `PodcastLibraryUpdater` na tym ograniczonym modelu;
4. aktualizuje wyłącznie subskrypcję celu i zmienione/dopisane odcinki celu;
5. zachowuje istniejące globalne ordinale odcinków oraz przydziela nowym następny globalny ordinal;
6. respektuje `podcast_removed_episodes`; kolizja globalnego `id` z innym kanałem przerywa transakcję zamiast nadpisać cudzy rekord;
7. commit wykonuje przed przejściem batcha do kolejnego celu.

Batch nadal zwraca końcowy `InboxCount` w każdym wyniku, lecz poprzedni ukończony kanał jest już trwale zapisany, gdy późniejszy kanał zawiedzie.

Test GREEN sprawdza po ponownym otwarciu połączenia: zmienione dane celu, własny tytuł (`HasCustomTitle`), flagi `IsNew`/`IsStarted`/ulubiony/kolejka/play-next, postęp, zachowany odcinek nieobecny w feedzie i datę sukcesu. Sprawdza także bitową równość niepoprawnych payloadów poza celem oraz ich ordinali, więc przejście testu dowodzi, że tych rekordów nie dało się globalnie zdeserializować ani zapisać. `podcast_state` (bieżący element i głośność) pozostaje bez zmian.

## Wykonane weryfikacje Windows / NTFS

Staging: `C:\Users\Michal\AppData\Local\AMC-Hermes-Build\hubert25-target-store` (własny znacznik `.amc-hermes-owned-stage`). SDK: `C:\Users\Michal\AppData\Local\AMC-BuildTools\dotnet\dotnet.exe` 8.0.423.

1. RED: `dotnet run ...Core.SmokeTests... -- --podcast-targeted-refresh` — kod 1, `JsonException` z pełnego `LoadInto`.
2. GREEN: ten sam test po implementacji — kod 0.
3. Sąsiedni faktyczny caller Core (`--youtube-subscription-sync-apply`) — kod 0, `OK: atomowy zapis subskrypcji YouTube i seria podcastów (9 scenariuszy)`. Fixture serii zawiera dwa istniejące kanały, po nowym i znanym odcinku; potwierdza końcowy InboxCount, reopen DB oraz idempotentne ponowienie.
4. Faktyczny caller LiteHost (`--podcast-progress`) — kod 0, `Wszystkie zestawy przeszly (1).` Obejmuje checkpoint/progress, Inbox, tombstone usuniętego materiału i utrwalenie ukończonego kanału automatu przed przerwaniem kolejnego.
5. Prawdziwy build `src\AccessibleMediaController.LiteHost\AccessibleMediaController.LiteHost.csproj -c Release` — kod 0, 0 ostrzeżeń, 0 błędów.

Nie deklarowano czasu wykonania ani liczby SQL `UPDATE`: test dowodzi zakresu przez niedeserializowalny rekord poza celem i niezmienione bajty/ordinale, a nie przez nieprzenośny benchmark.
