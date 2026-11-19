# HUBERT26 — niezależny odbiór rozdziałów YouTube (2026-11-19)

## Werdykt

**PRZYJĄĆ po poprawce odbiorowej z tego worktree.** Sam commit produktu
`626332908c612bc2177ad498325e8b09af3304cf` miał blokadę procesu potomnego
opisaną niżej; usunięto ją minimalnie razem z regresją. SHA commitu poprawki:
(committowany jako `fix: bound orphaned chapter tool children`; jego SHA podaje
wynik `git rev-parse HEAD` odbioru).

## Zakres i integralność

- Porównano przyrost od `4b5a4a4a07968eae69ba7cbc602d30bbaa07f07e` do
  `626332908c612bc2177ad498325e8b09af3304cf`: dziewięć plików implementacji,
  integracji i testów rozdziałów.
- Niezależny check rodzica: `9/9` plików zgodnych bajtowo i SHA z committem
  `6263329`, bez rozbieżności (`hubert26-parent-check/parent-verdict.json`).
- Dowód wykonawcy `results.json` potwierdza: oba buildy Release exit 0,
  `--podcast-chapters` exit 0 oraz rzeczywisty `--youtube-chapters-only`
  exit 0 w 28,822 s. Rodzic osobno uruchomił gotowy Release
  `amc_lite_protocol_tests.dll --podcast-chapters`: exit 0, jeden zestaw,
  w tym pięć diagnostyk kontrolowanego retry. Surowy output został zachowany
  jako bytes i po ustaleniu statusu zdekodowany CP852.

## Review delty

- `%(chapters)j` jest pobierane przez jawne argumenty `yt-dlp`, bez cookies
  i bez przeglądarki; parser akceptuje wyłącznie `NA` albo ograniczoną listę
  JSON, odrzuca nieprawidłowe pozycje i ogranicza wynik.
- Koordynator wybiera pobieranie YouTube wyłącznie dla adresów rozpoznanych
  jako YouTube. Pusta prawidłowa odpowiedź jest cache'owana przez
  `_emptyPageChecks`; błędy odzyskiwalne nie są cache'owane, więc kolejne
  ręczne wywołanie próbuje ponownie. Timeout jest klasyfikowany jako
  odzyskiwalny.
- Nieznany `stderr` nie trafia do komunikatu rozdziałów: ścieżka
  `DescribeChapterFailure` zastępuje wynik zawierający `Powód:` komunikatem
  ogólnym. Rozmiary stdout/stderr są ograniczone odpowiednio do 1 MiB i 64 KiB.

## Znaleziona blokada i RED

`BoundedExternalToolProcess.RunAsync` obejmował tokenem tylko
`WaitForExitAsync`. Po zakończeniu rodzica przechodził bez terminu do
`await outputTask/errorTask`. Dziecko, które odziedziczyło stdout/stderr,
mogło utrzymać oba readery; callback deadline widział wtedy `HasExited=true`
i nie zabijał już drzewa.

Odtworzono to bez sieci, cookies, GUI ani zewnętrznego narzędzia:

- osobny probe z dokładną kopią źródła z `6263329`: rodzic wychodzi, jego
  dziecko utrzymuje odziedziczone pipe'y;
- status procesu zebrano przed dekodowaniem danych: `parent_exited=True`,
  `child_live_at_watchdog=True`, `run_pending_at_2s=True`;
- po wyłącznie kontrolowanym cleanupie własnego dziecka runner wrócił
  **sukcesem**, co potwierdzało utratę deadline; exit probe `2`, raw SHA-256
  `be2c3f0f37509d63d5607898be2d062578421254b0841fde9fb10bbc7b5caf7f`.
- trwała regresja w rzeczywistym apphostie (`--youtube-chapters-only`) była
  RED: exit `1`, 4082 ms, raw SHA-256
  `27ae5c4ef53f40e185e5e115a8f142561f14c503f00e2f3352e22b4d4f333172`.

## Poprawka i GREEN

- Runner przypisuje uruchomiony proces do prywatnego Windows Job Object z
  `KILL_ON_JOB_CLOSE`; odziedziczone dzieci należą do tego samego joba.
  Deadline/cancellation zamyka job, więc kończy także dziecko po wyjściu
  rodzica. Nie ma ryzykownego fallbacku po PID (mógłby uderzyć w użyty ponownie
  PID).
- Oczekiwanie na oba readery jest także związane z deadline przez
  `WaitAsync(deadline.Token)`.
- Jeśli utworzenie/przypisanie joba się nie powiedzie, uruchomiony proces jest
  najpierw zakończony, a dopiero potem błąd wraca do wywołującego.
- Nowy fixture apphosta tworzy dokładnie ten przypadek rodzic–dziecko,
  wymusza anulowanie dopiero po sygnale dziecka i sprawdza zakończenie obu PID.

Końcowe wykonanie na nowym stagingu NTFS:

| Kontrola | Wynik |
|---|---:|
| Release build Windows smoke runner | exit 0, 19,56 s |
| `--youtube-chapters-only` z nową regresją | exit 0, 3867 ms, stdout/stderr 0 B |
| Release build LiteHost (linkuje ten sam runner) | exit 0, 3,97 s |

Surowy końcowy output: `review-final-run.raw`, SHA-256
`169f24774e13a79b6341d6c41c58f159e180701e87e9028aa70afdf8712b6fbb`.
Staging NTFS: `/mnt/c/Users/Michal/AppData/Local/AMC-Hermes-Build/hubert26-independent-review-6263329/`.

## Granice

Nie uruchamiano prawdziwego YouTube/yt-dlp, sieci, Chrome, GUI, NVDA, audio,
kont ani instalatora. Nie powtarzano niezmienionego pełnego zestawu podcastów
ani wcześniejszych buildów poza potrzebnym buildem poprawionego runnera i
LiteHosta. Job Object jest mechanizmem Windows; badanie dotyczy docelowego
`net8.0-windows` i zostało wykonane na Windows apphostie.
