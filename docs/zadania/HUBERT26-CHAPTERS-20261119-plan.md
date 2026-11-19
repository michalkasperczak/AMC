# Hubert #26 — rozdziały YouTube, wykonanie po odbiorze #25

**Status: W REALIZACJI — wykonanie przez Codex w izolowanym worktree.**

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

Cel: w istniejącej liście rozdziałów filmu YouTube będą dostępne rozdziały udostępnione przez twórcę. Brak rozdziałów ma być odróżniony od przejściowego błędu pobierania. Nie jest to jeszcze opis wydanej funkcji. Wykonawca uzupełni dokładne istniejące polecenie, zakres sesji i komunikaty na podstawie produkcyjnej ścieżki, bez wymyślania nowych skrótów.

## Wersja i następny krok

Źródła #25: merge `d113fae9895a214d8782a524cbfe797902be2dfa`. Gałąź wykonawcza: `codex/amc-hubert26-chapters-20261119`. Dokładną bazę po zapisaniu niniejszej dokumentacji utrwali raport wykonawcy.

Następny krok: Codex w izolowanym worktree, testy i poprawki wymagane powyżej, czysty commit oraz raport `HUBERT26-CHAPTERS-20261119-wdrozenie.md`. Brak gotowego artefaktu/testów jest blokadą, nie zaliczeniem planu.
