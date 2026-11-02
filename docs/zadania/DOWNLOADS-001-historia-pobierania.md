# DOWNLOADS-001 — wspólna historia pobierania

- Status: **W trakcie ręcznego odbioru NVDA; kod, pełne testy, paczka,
  instalator, wydanie GitHub i lokalna instalacja AMC Test są gotowe**.
- Decyzja użytkownika: 31.10.2026. Wspólna lista pobranych plików w Radio
  Archiwiście i Podcastach, również wideo, analogiczna do historii nagrywania.
- Wersja docelowa: AMC Test `0.1.0-alpha.455`.

## Zachowanie i polecenia

- `Ctrl+J` w liście i odtwarzaczu każdej sesji lub Widok → Historia pobrań:
  tymczasowy widok „Pobrane pliki” w sesji Pliki lokalne.
- Jeden wiersz to nazwa pliku. Lewa strzałka lub `Alt+T` mówi źródło, datę,
  rozmiar i folder. Enter otwiera istniejący plik. Brakujący plik ma etykietę
  „Brak pliku” i komunikat po Enter zamiast próby odtworzenia.
- `F5` odczytuje historię ponownie. Escape wraca do miejsca wywołania.
- Zakończone pobrania Radio Archiwisty przez `Ctrl+D`, audio Podcastów przez
  `Ctrl+D` i wideo przez `Ctrl+Shift+D` zapisują położenie w prywatnym
  `download-history.json` profilu AMC Test. Pliki mogą leżeć w różnych
  folderach. Starsze audio z `podcasts.db` i MP3 z folderu archiwum są
  dołączane bez modyfikacji bazy. Podgląd „Pobrane” w Podcastach pozostaje
  listą dostępnych odcinków audio.

## Uzasadnienie i różnice między trybami

Lista jest lokalnym indeksem plików, bez zarządzania stroną Radio Archiwisty.
Nie usuwa wpisów samoczynnie, gdy użytkownik przeniesie plik. Poprzednich
pobrań wideo z dowolnych folderów nie da się bezpiecznie odtworzyć wstecz;
indeksowane są nowe pobrania. Funkcja jest na razie wyłącznie w AMC Test.
Spotify jest oddzielnym zadaniem i nie jest częścią tej zmiany.

## Testy i wynik

- Testy przechowywania, scalania starego audio i archiwum, brakującego pliku,
  aktywacji i zgodności skrótu, menu, palety i pomocy oraz pobrania na
  rzeczywistym hoście: **94 zaliczone, 0 błędów**.
- `dotnet build` hosta LiteHost w Release: **powodzenie, 0 ostrzeżeń i błędów**.
- Końcowa pełna regresja w prywatnym runtime AMC 31.10.2026:
  **2211 zaliczonych, 143 pominięte, 13 podtestów zaliczonych, 0 błędów**.
  Wcześniejsze próby ujawniły stare atrapy wx i luki w katalogu angielskim;
  naprawiono je przed końcowym przebiegiem.
- Samowystarczalna paczka i instalator: **zbudowane**, kontrolowany start
  interfejsu i hosta zaliczony. Instalator ma 133636988 bajtów i SHA-256
  `85231cc00043c7e050e2f94a6a7aa1003de3be11721b553d0e64f16cb1e62176`.
- GitHub: wydanie wstępne `test-2026.10.31.1335` **opublikowane**;
  zdalny załącznik ma tę samą nazwę, rozmiar i SHA-256.
- Instalacja użytkowa `C:\Users\micha\AppData\Local\Programs\AMC Test`:
  **zaktualizowana i uruchomiona**. Manifest oraz pojedyncze procesy AMC,
  Pythona i hosta po instalacji wskazują commit wydania.
- Ręczny odbiór NVDA na żywym oknie: **oczekuje na próbę użytkownika**.
  Testy kodowe nie dowodzą tego odbioru.

## Materiał do dokumentacji użytkownika — projekt

„Otwórz Historię pobrań skrótem Ctrl+J. Lista łączy pliki pobrane w Radiu
Archiwiście oraz w Podcastach i YouTube. Enter odtwarza plik, lewa strzałka
podaje źródło, datę, rozmiar i folder. Escape wraca do poprzedniej listy.”

## Projekt opisu zmian

„AMC Test dodaje wspólną Historię pobrań dla nagrań Radio Archiwisty,
podcastów i wideo. Wpisy pozostają czytelne, jeśli plik został przeniesiony.”

## Wersja i commit

AMC Test `0.1.0-alpha.455`, nazwa okna `AMC Test — 31.10.2026, 13:35`.
Commit źródeł instalatora: `e4347862bb90f27c87f6e82754aacd47d49bb29b`.
GitHub: `https://github.com/michalkasperczak/AMC/releases/tag/test-2026.10.31.1335`.
Instalator lokalny:
`D:\Projekty Codex\AMC aktualizacje\AMC Test 0.1.0-alpha.455 pobrania 2026-10-31\instalator\AMC-Test-2026-10-31-1335.exe`.
