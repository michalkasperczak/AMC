# GH-TRIAGE-20261119 — zamknięcie wykonanych zgłoszeń

- Status: **Zrobione — porządek stanów Issues, bez nowych wdrożeń.**
- Polecenie Michała: „Te zgłoszenia, które już na githubie są zrobione, to zamykaj je, żeby był porządek”. Obowiązuje także po kolejnych odebranych etapach.
- Zakres: publiczne zgłoszenia w `michalkasperczak/AMC`; bez zmian prywatnego intake i cudzych repozytoriów.

## Podstawa i sposób weryfikacji

Odczytano wszystkie 18 zgłoszeń (17 otwartych), pełną treść otwartych wpisów i późniejsze komentarze. Dwa niezależne przeglądy tylko do odczytu sprawdziły #9–#11 oraz #14–#18. Prowadzący sprawdził pozostałe wpisy, istotne źródła i dokumenty dowodowe. Nie uruchamiano nowego audytu, kompilacji, instalatora, GUI ani NVDA.

Public `public-2026.11.18.0329` i Test `test-2026.11.18.0337` są opublikowane. Odczyt API obu tagów potwierdził źródła `1723786c4557ef68bb3922189a028099ce96db31`. Lokalny Git potwierdził, że istotne commity poprawek są przodkami tego źródła. Niewysłanych zmian bieżącego HEAD nie przedstawiano jako wydanych.

Do każdego zamknięcia dodano polski komentarz: zakres rozwiązania, wersja (dla poprawek produktu), dowód i ograniczenia. Ustawiono jawnie `state=closed`, `state_reason=completed`, następnie osobnymi odczytami sprawdzono dokładny komentarz oraz zgłoszenie. Końcowe ponowne pobranie wszystkich Issues potwierdziło cztery zamknięcia; pozostało 13 otwartych. Nie zmieniano starego zamkniętego #5.

## Zamknięte

- **#12 — próba Sygnalisty.** Kontrolowany test dotarł na GitHub; nie był zgłoszeniem usterki. Komentarz `7007769891`.
- **#16 — kolejka plików po restarcie.** Osobny magazyn `file-queue-v1.json`; rzeczywiste dwa procesy LiteHost potwierdziły zapis, pełne zakończenie pierwszego procesu i odtworzenie trzech pozycji przez drugi, z kolejnością i flagą „odtwórz następne”. Dowód: `HUBERT-AUDIO-ODBIOR-20261117.md`, źródło pomiaru `9b77369` jest w wydaniu. Komentarz `7007925567`.
- **#18 — dzień tygodnia Radio Archiwisty.** `c665022` jest w wydaniu; 44 istniejące testy obejmują etykiety, błędne daty, stabilne ID i PL/EN także po zmianie języka otwartej listy. Dowód: `HUBERT-18-20261116.md`. Komentarz `7007928872`.
- **#20 — odrzucanie zakończonych transmisji przez polskie `lang=pl`.** `743582b` i korekty są w wydaniu; status jest odczytywany bez lokalizacji, polski tytuł dopasowany po ID. Istnieją pomiary publicznego odczytu/parsera i potwierdzenie Michała o widoczności dawniej pomijanych transmisji. Nowe problemy pozostają w #25. Dowód: `YT-007-zgloszenia-19-20-import-i-transmisje.md`. Komentarz `7007772800`.

## Pozostawione otwarte i konkretne braki

- **#1 — historyczny problem pobierania YouTube:** brak dowodu potwierdzającego rozwiązanie dokładnego zgłoszonego przypadku; nie zamykano na podstawie samych późniejszych zmian pobierania.
- **#9 — konto Spotify:** konfiguracja jest w wydaniu, ale brak odbioru OAuth/komunikatów NVDA na świeżym profilu.
- **#10 — Spotify cache i wejście najpierw w Playlisty:** poprawka jest w wydaniu, ale brak rzeczywistej synchronizacji konta, restartu i odczytu cache.
- **#11 — Spotify:** nowy tor nie daje podstawy do zamknięcia wszystkich późniejszych uwag. Naturalny koniec/przejście dalej, ponowne połączenie i obsługa starego Chrome nadal nie mają pełnego odbioru.
- **#14 — aktualizacja i kod 5:** guard i testy są, ale nie wykonano rzeczywistej cichej aktualizacji z próbą startu podczas podmiany oraz scenariusza odzyskania po kodzie 5.
- **#15 — przester MP3:** istniejący pomiar produkcyjnego PCM jest ważny; brak odsłuchu wskazanego materiału w instalacji i odbioru wyjścia audio. Nie zrównano testu syntetycznego z odsłuchem zgłaszającego.
- **#17 — opcjonalny dodatek NVDA:** poprawka jest w wydaniu, ale brak rzeczywistej cichej aktualizacji dla braku/starego dodatku i błędu pomocnika oraz żywego NVDA.
- **#19 — masowy import i transmisje:** poprawki są w wydaniu, lecz brak pełnego odbioru importu oraz pozostałego uzgodnionego zakresu. Nie wdrażano nieoficjalnego logowania cudzym klientem TV.
- **#21 — komunikaty Folderów Biblioteki:** poprawka jest wydana, ale brak żywego odbioru wypowiedzenia wyniku przez NVDA. Nie uznano testu callbacku announcera za dowód mowy.
- **#22–#25 — nowe zgłoszenia Huberta:** odpowiednio wolne foldery, znikające komunikaty, jawna instalacja dodatku NVDA i odświeżanie/scalanie YouTube. Zachowano otwarte; to zadanie nie obejmowało wdrażania nowych poprawek.

## Materiał do dokumentacji użytkownika

Wykonane zgłoszenia zawierają komentarz z zakresem rozwiązania i dostępną wersją. Otwarte wpisy nie muszą oznaczać braku kodu: część czeka na konkretny odbiór działania. Nowe problemy YouTube z #25 nie są rozwiązane samym zamknięciem pierwotnego #20.

## Dowody i następny krok

Raporty API i dokładne kwity przejść: profil AMC, `state/github-issue-triage-20261119/` (`issues-before.json`, `comments-before.json`, `releases.json`, `closed-*.json`, `issues-after.json`). Prywatnej treści intake nie kopiowano do komentarzy ani dokumentacji publicznej.

Brak nowej publikacji źródeł/instalatorów i brak instalacji na głównym PC. Dalszy rozwój obejmuje wyłącznie wskazane przez Michała zadania; nowe zgłoszenia nie zostały automatycznie przydzielone do wdrożenia. Po następnym odebranym rozwiązaniu zaktualizować jego komentarz i stan na GitHubie bez czekania na osobne przypomnienie.
