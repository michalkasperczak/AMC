# Weryfikacja AMC alpha 341 / NVDA 0.2.0 — 11 września 2026

## Wynik

- `build.ps1 -Publish -KeepPreviousPackages`: kod 0; restore, kompilacja
  Release (0 błędów, 0 ostrzeżeń), pełne Core.SmokeTests,
  Windows.SmokeTests oraz pakiet win-x64 zakończone prawidłowo.
- `python -B nvda-addon/tests/test_controller.py`: 7 testów, OK.
- `git diff --check`: bez błędów białych znaków.
- Dodatek spakowany przez `nvda-addon/build.ps1`; 5 wpisów: manifest,
  polska pomoc HTML, trzy moduły Python. Bez danych konta, logów, stanu
  użytkownika, konfiguracji gestów czy cache Pythona.
- Nie uruchomiono alpha 341 ani nie instalowano dodatku. Działający
  proces alpha 340 (PID 16068) pozostał uruchomiony. Nie publikowano
  źródeł ani artefaktów tej wersji na GitHub.

## Co sprawdzono automatycznie

- 53 unikalne domyślne gesty, opis każdego skryptu i zgodność skryptu
  z komendą; jawnie zarezerwowane klawisze Windows nie są nadpisywane
  przez nowe gesty. Lewo/prawo pozostają świadomym wcześniejszym wyjątkiem.
- Wymiana każdego z 53 poleceń między prawdziwym klientem Python
  a izolowanym serwerem .NET; żadne polecenie nie trafiało do realnego AMC.
- Odmowa nieznanej komendy, nieprawidłowej wersji, zbyt dużego zapytania,
  braku serwera; poprawne polskie znaki, limit czasu, odzyskanie nasłuchu,
  jedna komenda na połączenie, brak ponowienia przełącznika.
- Ograniczona kolejka pracownika NVDA i nieblokujące zakończenie.
- Udzielenie prawa foreground tylko PID-owi odczytanemu z połączonego
  pipe i tylko dla komend show...; błąd opcjonalnego API nie przerywa IPC.
- Powrót z żądania UI przed wykonaniem dialogu; jedna oczekująca akcja,
  blokada ponownego wejścia, odrzucenie po zmianie warunków/wygaśnięciu.
- AsyncLocal zachowuje pochodzenie zdalnej akcji po await, bez wpływu
  na późniejsze zwykłe akcje użytkownika.
- Faktyczne właściwości ActionItem/ActionItems MainWindow, na izolowanej
  kontrolce STA: lokalny skrót dotyczy zaznaczenia, zdalny bieżącego pliku;
  ukryty folder nie jest rozwijany do masowej operacji.
- Następny po pauzie uruchamia sąsiedni element zapamiętanej listy,
  nie przypadkową sąsiednią stację całego katalogu. Koniec listy nie
  wypada do katalogu. Kontekst ma nazwę i pozycję; Presety pozostają
  odrębnym zakresem.
- Istniejące pełne regresje obejmują odtwarzanie, kodeki, harmonogramy,
  nagrywanie, podcasty/rozdziały, TIDAL, schowek i dostępność kontrolek.

Podczas przygotowania nowych testów poprawiono dwie usterki samej
izolowanej makiety: brak wątku STA oraz brak zarejestrowanej sesji lokalnej
w domyślnym SessionManager. Pełny końcowy przebieg już ich nie wykazuje.
Nie były to awarie uruchomionego odtwarzacza użytkownika.

## Artefakty

- AMC-NVDA-0.2.0.nvda-addon, SHA-256:
  `8AE55D779784548E5334BBAFA3F841DBA85CF8E8DF4359C763F43681D4491853`
- AccessibleMediaController-0.1.0-alpha.341.exe, SHA-256:
  `45456D1327B63372A419A37F1F266C9B132CA393F907E845280EE92C6CFF30C3`

## Nie uznawać za przetestowane

Rzeczywisty odsłuch mowy NVDA i brajl, instalacja dodatku, przekazanie
fokusa przez Windows z innych aplikacji, konfiguracje urządzeń użytkownika,
opóźniona sieć/realne WiiM, zachowanie komend podczas rzeczywistych nagrań
i współdziałanie ze wszystkimi globalnymi skrótami innych programów.
Szczególnie sprawdzić stare, odrębne zdarzenia silnika i timery naprawiające
fokus — test AsyncLocal nie dowodzi bezpieczeństwa każdego takiego zdarzenia.
Scenariusze dla użytkownika: INSTRUKCJA_NVDA_PL.md; dla kolejnego audytu:
PROJEKT_NVDA_PL.md. Prywatnego TIDAL_MAIL_DO_POMOCY_EN.md nie publikować.
