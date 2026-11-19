# Hubert #26 — rozdziały YouTube, wykonanie po odbiorze #25

**Status: ODEBRANY KOD I ZINTEGROWANY — `36ba1fed868aa655d38974ee93ec8af80932aeb7`, z poprawką procesu `516e5b3`.**

## Decyzja i zależności

Michał zaakceptował kolejność #25, #26, #28 w [planie wspólnym](HUBERT-20261119-triage-zgloszen.md). Dane i harmonogram #25 są już odebrane i zintegrowane (`adf6e51`, `d113fae`). Nie wymaga to ponownego pytania o zgodę. Kontynuacja nie upoważnia do logowania usług, instalacji na głównym komputerze ani publikacji niesprawdzonego kandydata.

## Potwierdzony stan wejściowy

Świeży odczyt GitHub: #26 OPEN, pełny opis i jeden komentarz przeczytane. PR #27 nadal OPEN, mergedAt=null, głowa `d46e4c85a380a9997b8a671e1099b00ab40b2462`, baza `codex/amc-protected-public-test-20261118-issue21-r2`. Checków nie zaobserwowano; `gh pr checks` zgłasza „no checks reported”, nie zielone CI. Główna kanoniczna gałąź nie jest tą starszą bazą PR. Nie scalać całej chronionej gałęzi wydania ani nie przyjmować PR bez korekt.

Problem: filmy z kanału mają pusty opis w płaskiej liście, a zwykły parser strony nie odczytuje rozdziałów umieszczonych w skrypcie YouTube. PR dodaje parser wyniku yt-dlp `--print "%(chapters)j"` i gałąź YouTube w koordynatorze rozdziałów. Deklaracje autora o żywym YouTube nie są pomiarem wykonawcy.

[Wcześniejszy review PR i konkretne zastrzeżenia](HUBERT-20261119-pr27-pr29-review.md) pozostaje wymaganiem wykonania.

## Zakres wdrożenia

- Przenieść wąsko rozwiązanie rozdziałów do aktualnej bazy, nie całą starą gałąź.
- Parser poprawnych rozdziałów, normalizacja tytułów/czasów i jawny prawidłowy brak rozdziałów (`NA`/pusta lista).
- Dla pojedynczego adresu YouTube użyć yt-dlp zamiast pobierania strony watch. Zachować zwykłe podcasty, pojedynczy import Ctrl+N i nie przeszukiwać całego kanału/playlisty.
- Awaria uruchomienia/brak narzędzia, nonzero, uszkodzony JSON, timeout, anulowanie i nadmiar danych nie mogą stać się trwałą pamięcią „brak rozdziałów”. Następna próba ma być możliwa.
- Prawidłowy brak rozdziałów może korzystać z istniejącego cache negatywnego.
- Limitować stdout i stderr już podczas odczytu, nie dopiero po ReadToEndAsync. Zamknąć wyłącznie własny proces po limicie/anulowaniu/timeout; nie pozostawiać wiszących pipe-readers.
- Zachować budżet procesu poniżej istniejącego oczekiwania GUI na chapter.prepare i bezpieczny przekaz błędu. Nie ujawniać surowych danych kont ani sekretów w diagnostyce.

## Kryteria odbioru i stop

Rzeczywiste pionowe RED → implementacja → GREEN dla zmienionych zachowań. Celowane scenariusze: poprawna lista, poprawny brak i cache, błąd oraz skuteczny retry w tej samej sesji, nonzero/JSON/timeout/cancel bez cache, nadmiar stdout i stderr przez prawdziwy proces-fixture bez sieci, poprawny routing YouTube i regresja zwykłego podcastu. Test procesu ma potwierdzać jego zakończenie, a nie tylko wyjątek helpera. Dopasować bezpieczne krótkie testy do rzeczywistych API i runnerów; nie uruchamiać pełnego Windows.SmokeTests otwierającego GUI.

Zbudować rzeczywiste LiteHost i Windows, jeżeli oba konsumują nowy wspólny helper. Nie wystarcza osobny parser testów. Jeden spójny odbiór plus niezależny przegląd delty; bez rutynowego ponawiania niezmienionej szerokiej suite. Po spełnieniu kryteriów zintegrować i przejść do #28. Kod/testy/build nie oznaczają żywego YouTube, odsłuchu, NVDA, instalacji ani wydania.

## Ograniczenia wykonania

Bez GUI, żywego audio, sieci YouTube, kont, cookies, tokenów, instalatora, AV obejść, głównego PC, pushu i zamykania zgłoszeń. Proces-fixture działa wyłącznie na własnych plikach i bazie. Codex przez konto, bez osobno płatnego API. Nie zmieniać #25, nagrywania, Spotify, tempa, mDNS, range/cache ani wznowienia #28 w tym etapie. Rodzic prowadzi wspólne rejestry; wykonawca zapisuje osobny raport.

## Materiał do dokumentacji użytkownika

Materiał do instrukcji: w istniejącej liście „Pokaż rozdziały bieżącego materiału”
(`Ctrl+Alt+B` w bieżącym katalogu poleceń) kod pobiera także rozdziały filmu
YouTube udostępnione przez twórcę. Nie dodano nowego skrótu ani osobnego widoku.
Prawidłowy brak rozdziałów jest odróżniany od przejściowego błędu pobierania,
po którym można spróbować ponownie. Zachowano istniejące polecenia poprzedniego
i następnego rozdziału (`Ctrl+Shift+Left` / `Ctrl+Shift+Right`). Mowy NVDA ani
fizycznego działania tych skrótów w tym odbiorze nie mierzono.

## Wersja i następny krok

Źródła #25: merge `d113fae9895a214d8782a524cbfe797902be2dfa`. Gałąź wykonawcza: `codex/amc-hubert26-chapters-20261119`. Dokładną bazę po zapisaniu niniejszej dokumentacji utrwali raport wykonawcy.

Następny etap: uzgodnione #28/PR #29. Przed integracją jego zmian
WindowsMediaOutput uwzględnić trwający odbiór wspólnego audio (`4a791c2`,
`deleg_96958dcb`) i czas ochronnej pauzy. Nie scalać całej starej bazy PR.
Odbiór #26 nie wymaga już kolejnego powtórzenia niezmienionych zestawów.

## Odebrane dowody i kontrola rodzica — 19.11.2026

Wykonawca `deleg_5ad77976` dostarczył kod/testy
`626332908c612bc2177ad498325e8b09af3304cf` oraz raport
`39357efce300f77dce60ac8c44bbb347083c6060` w branchu
`codex/amc-hubert26-chapters-20261119`. Baza `4b5a4a4a07968eae69ba7cbc602d30bbaa07f07e`,
funkcjonalny RED `51f9c9be4999b03f37e410f01d6238ef1ae43ad7`:
protocol `--podcast-chapters`, 1 z 1 FAIL na drodze YouTube.

GREEN wykonawcy na świeżym archive `6263329`: protocol, bezsieciowy smoke
z prawdziwym apphostem (`--youtube-chapters-only`), Release buildy realnych
LiteHost i Windows, wszystkie exit 0. Obejmuje JSON/NA, retry po błędach,
stdout/stderr overflow, timeout, anulowanie i cleanup potomnego procesu.
Poprzedni `9046372` miał błąd kompilacji metody z opcjonalnym parametrem;
to nie jest finalny kandydat. Poprawiono jawną lambdą i przebudowano świeżą bazę.

Rodzic odczytał raport, diff i pliki dowodów stagingu
`C:\Users\Michal\AppData\Local\AMC-Hermes-Build\hubert26-chapters-20261119-final2\.amc-hubert26-evidence`.
Porównał bajty i SHA-256 dziewięciu krytycznych źródeł z blobami `6263329`:
zero różnic. Niezależnie uruchomił istniejący Release runner
`amc_lite_protocol_tests.dll --podcast-chapters`: **1 zestaw PASS**, exit 0,
w tym kontrolowane błędy i retry w tej samej sesji. Log rodzica w
`C:\Users\Michal\AppData\Local\AMC-Hermes-Build\hubert26-parent-check`.
Nie powtarzano całej suite ani niezmienionych buildów. Pierwszy odczyt stdout
przez UTF-8 spowodował błąd dekodera po wyjściu procesu; zapis surowych bajtów
i ponowienie wyłącznie tego zestawu z CP852 potwierdziły wynik. To nie był RED produktu.

Niezależny Codex `sa-0-131cfb98`, `deleg_371395e2`, ma własny worktree
`amc-hubert26-acceptance-20261119` od `39357ef` i odczytuje dokładną deltę.
Sprawdza celowo niepewną granicę runnera: czy wczesne wyjście rodzica z
potomkiem trzymającym odziedziczone pipe'y może ominąć timeout czytników.
To HIPOTEZA do falsyfikacji, nie wykryty błąd; maksymalnie dwa cykle
celowanej korekty, bez rozszerzania do pełnego audytu.

Powyższa sekcja opisuje stan przed werdyktem. Przyrost nie zmienia odtwarzania
ani nagrywania; równoległe prace audio na `e758ae4`/`4a791c2` wymagają później
zwykłego scalenia zmian LiteEngineHandlers/csproj, nie podmiany całych plików.

## Końcowy odbiór i integracja

Niezależny Codex potwierdził blocker: po wyjściu rodzica dziecko mogło utrzymywać
stdout/stderr bez respektowania deadline. Kontrolowany rzeczywisty Windows
apphost dał RED exit 1; poprawka `516e5b3495707fc05c90c82d435d4f9a4d18f7f8`
dodała Windows Job Object z `KILL_ON_JOB_CLOSE` i deadline także dla odczytu
obu strumieni. Regresja sprawdza zakończenie PID rodzica i dziecka. GREEN:
celowany smoke exit 0, Release buildy Windows smoke oraz LiteHost exit 0.
[Pełny niezależny raport](HUBERT26-CHAPTERS-20261119-independent-review.md).

Rodzic odczytał pełny runner, diff regresji i raport, sprawdził statusy RED/GREEN
oraz porównał dziewięć zmienionych źródeł finalnego stagingu z blobami `516e5b3`:
zero różnic. SHA-256 źródła runnera:
`bbce563ff93229cd40f74f0645e4572333573b39cc774faeb4b99b506cafc5eb`.

Zintegrowano czysty branch odbioru do `codex/amc-test-public-20261109` jako
`36ba1fed868aa655d38974ee93ec8af80932aeb7`. `git diff --exit-code 516e5b3 -- src tests`
nie wykazał żadnej różnicy; `git diff --check` przeszedł. Nie scalano starej
chronionej gałęzi PR. Po integracji rodzic uruchomił wyłącznie zmienioną granicę:
`Windows.SmokeTests --youtube-chapters-only` na zweryfikowanym stagingu —
**exit 0, stdout i stderr puste**, czas 1,347 s. Nie powtarzano niezmienionego
zestawu protokołu ani całych buildów. To wykonanie istniejącego zbudowanego
runnera, a nie nowa budowa wydania z commita integracyjnego.

Surowe logi i JSON wyniku rodzica:
`/home/michal/.hermes/profiles/amc/cache/scratch/hubert26-integrated-parent-20261119/`.

Odbiór kodu/offline zakończony. Nie wykonywano prawdziwego YouTube/yt-dlp,
sieci, kont, Chrome, GUI, NVDA, odsłuchu, instalacji, publikacji ani zamykania
zgłoszeń. Te granice nie są objęte powyższym werdyktem.
