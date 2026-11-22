# PRESET — TIDAL i bezpośrednie przypisanie, 22.11.2026

## Zgłoszenie i decyzja

Użytkownik zgłosił, że `Ctrl+Alt+P` i przypisanie presetu TIDAL prowadzi do
komunikatu o wybraniu elementu, choć element jest zaznaczony. Poprosił też o
`Ctrl+Alt+Shift+cyfra` na wzór Radia Archiwisty w innych sesjach. Rozpoznana
przyczyna: `target_from_row` nie mapował żadnego wiersza katalogu TIDAL na cel
presety. Nie był również dostępny tor uruchomienia takiego wpisu.

## Widoczne zachowanie

- W TIDAL zaznaczony utwór, album, playlista lub wykonawca jest celem presetu.
  Utwór uruchamia się w odtwarzaczu AMC; album i playlista są przekazywane jako
  całość do oryginalnego TIDALa; wykonawca otwiera katalog wykonawcy.
- `Ctrl+Alt+P` pokazuje dwanaście miejsc bieżącej sesji. Samo otwarcie listy
  nie zapisuje celu. `Ctrl+Alt+Shift+P` prowadzi do wyboru miejsca.
- `Ctrl+Alt+Shift+1…9, 0` przypisuje zaznaczony element bez otwierania dialogu,
  gdy wskazane miejsce jest puste. Zajęte miejsce otwiera dialog, który czyta
  nazwę nowego celu i starego wpisu. Dwa wskazania miejsca i Enter potwierdzają
  zastąpienie; Escape anuluje. To samo miejsce z tym samym celem nie jest
  zapisywane ponownie. Radio Archiwista zachowuje istniejące natychmiastowe
  zastępowanie. Skróty uruchamiające presety `Ctrl+Shift+1…0/-/=` nie zmieniły
  znaczenia.
- Bezpośrednie przypisanie jest oferowane w Plikach, Radiu, Podcastach,
  TIDAL, Spotify, Sonosie i Radiu Archiwiście, gdy wybrany wiersz da się
  zapisać. WiiM i Frontier Smart zachowują własny model presetów urządzenia.
  Apple Music nie oferuje presetów do czasu obsługi ich uruchamiania.
- Lista i okno przypisania podają NVDA nazwę zaznaczonego celu w nazwie
  dostępnościowej kontrolki. Gdy cel jest niewłaściwy, komunikat podaje nazwę
  zaznaczonego wiersza oraz możliwe rodzaje elementów; gdy TIDAL nie zwraca
  identyfikatora katalogowego, wskazuje ten brak wprost.

## Miejsca i zgodność powierzchni

`profile_presets.py` mapuje cele; `gui.py` zapisuje i uruchamia;
`shortcuts.py` jest źródłem routera i pomocy; `menu_model.py` wystawia te same
akcje i warunek celu w osobnym menu Presety; `i18n.py` daje angielskie nazwy użytkowe. Kontrola menu wykryła dwa
wcześniejsze wpisy pobranych plików bez klawisza dostępu; dostały wolne
klawisze 3 i 4, aby test menu mógł sprawdzić cały aktualny układ. Identyfikatory
TIDAL pozostają danymi modelu, a nie etykietą NVDA. Zmiana obejmuje oba widoki
listy i odtwarzacza, ale cel w odtwarzaczu pochodzi z faktycznie granego utworu.

## Weryfikacja i granice

- Etykiety listy, dialog przypisania, menu i nowe komunikaty są dostępne po polsku oraz angielsku.

- Testy bez widocznego GUI z runtime bieżącej instalacji: 178 zaliczonych,
  2 pominięte ze świadomym filtrem, 0 niepowodzeń. Uruchomiono
  `test_profile_presets.py`, `test_preset_gui_wiring.py`, `test_shortcuts.py`,
  `test_tidal_navigation.py`, `test_menu_exposes_existing_actions.py`,
  `test_command_palette.py` i `test_i18n.py`. Dwa pominięte testy pełnej
  inwentaryzacji tłumaczeń obejmują starsze, niezwiązane teksty ustawień;
  uruchomione bez filtra nadal nie przechodzą. Dodatkowy test English sprawdza
  etykiety 12 miejsc, menu i komunikat braku celu w sesji TIDAL. Test zaznaczonego wiersza
  TIDAL bez identyfikatora sprawdza dokładny komunikat po polsku i angielsku.
- Szerszą serię `wxlite/tests` rozpoczęto dodatkowo i przerwano po wykryciu
  niepowodzeń poza zakresem presetów. Powtórzenie z zatrzymaniem na pierwszym
  błędzie dało 516 zaliczonych, 11 pominiętych i błąd
  `test_full_backup.py::test_full_backup_combines_core_and_sanitized_ui_state`:
  walidator kopii odrzuca nieznane dane interfejsu. Nie diagnozowano
  pozostałych błędów; pełna seria nie jest zaliczona.
- Bezokienny test wykonania zapisuje utwór TIDAL w wolnym miejscu i wywołuje
  prawdziwą metodę ramki; wynik to `PlayTidalTrack`. Dla albumu wynik to
  `PlayTidalContainer` całej płyty. To test bez rzeczywistego konta i odsłuchu.
- Import `amc_wx_lite.gui` przez runtime z bieżącej instalacji — zaliczony.
- `py_compile` czterech zmienionych modułów — zaliczony.
- Odizolowana paczka próbna AMC Test w `.test-output/preset-tidal-20261122`:
  3800 plików, wszystkie SHA-256 zgodne z manifestem, profil `test`, etykieta
  `22.11.2026, 17:03`. Pięć zmienionych modułów Pythona w paczce ma te same
  sumy co źródła. `sourceDirty=true`, więc nie jest kandydatem do publikacji.
  Składanie użyło istniejących binariów hosta oraz przypiętych zależności
  bieżącej instalacji; nie jest dowodem nowej kompilacji C#.
- Ręczny odbiór NVDA, instalacja użytkowa, uruchomienie normalnej aplikacji i
  publikacja: jeszcze niewykonane. Repozytorium wymaga osobnej zgody na
  uruchomienie aplikacji lub widocznego testu przy zwykłej poprawce. Zapytanie
  o próbę wysłano użytkownikowi po przygotowaniu kandydata.
- Zapis celu TIDAL wymaga katalogowego identyfikatora. Materiał, którego
  usługa go nie podaje, pozostaje niedostępny. Album i playlista korzystają
  z istniejącej zewnętrznej trasy całego kontenera, więc wymagają działającego
  oryginalnego TIDALa.

## Szkic instrukcji i zmian do odbioru

Zaznacz utwór lub album TIDAL. Naciśnij `Ctrl+Alt+Shift+cyfra`, aby zapisać go
w miejscu 1–9 albo 0 (preset 10). Jeśli miejsce jest zajęte, usłyszysz obie
nazwy; wskaż tę cyfrę dwa razy i potwierdź Enterem. `Ctrl+Shift+cyfra`
uruchamia zapis. `Ctrl+Alt+P` pozwala sprawdzić listę. Szkic wymaga korekty i
aprobaty użytkownika przed publikacją jako przewodnik.

Commit poprawki źródłowej: `5f1297a771ce7dde01a657a4738d5b3bbb125bce` (bieżąca gałąź `codex/amc-test-public-20261109`). Paczka próbna została odświeżona po commicie; pozostałe niezależne dokumenty są nadal zmianami roboczymi, dlatego manifest uczciwie zgłasza `sourceDirty=true`.

Dodatkowy commit testu komunikatu PL/EN: `a12aafb7`.

## Korekta 22.11.2026 — wydanie GitHub przed odbiorem NVDA

Użytkownik doprecyzował, że chce teraz wydania na GitHubie; próby z NVDA
wykonuje zwykle po wydaniu. Ta decyzja zastępuje wcześniejsze oczekiwanie na
zgodę na widoczną próbę NVDA przed publikacją. Nie obejmuje żądania lokalnej
instalacji ani uruchomienia programu.

Połączony kandydat AMC Test `0.1.0-alpha.513` ma czysty commit
`a07b11c0a8bbdf76e413ed695479d8b2ee4e757d`. Commit `a01fbfe2` w jego
historii ma taki sam stabilny patch-id poprawki presetów jak `5f1297a7`, a
`1d5e68b2` dodaje test komunikatu PL/EN. Na kandydacie zaliczono 180 celowanych
testów presetów, TIDAL, skrótów, menu, palety i tłumaczeń; dwa wcześniejsze
testy pełnej inwentaryzacji językowej świadomie wyłączono. Nie oznacza to
zaliczenia pełnej regresji ani żywej próby TIDAL i NVDA.

GitHub już zawiera opublikowane wydanie wstępne Test
`test-2026.11.22.1851` pod adresem
`https://github.com/michalkasperczak/AMC/releases/tag/test-2026.11.22.1851`.
Odczyt przez API potwierdził: tag wskazuje dokładnie commit `a07b11c0`,
`prerelease=true`, trzy zasoby mają stan `uploaded`. Instalator
`AMC-Test-2026-11-22-1851.exe` ma 214422916 bajtów i SHA-256
`51c7fcdd3225632d0d1c2966075c91765a4e7f8ef5e57e58480232c89a92c4a3`;
wartości GitHub są równe lokalnemu plikowi i manifestowi integralności.
Wydanie powstało równolegle podczas tej pracy; nie tworzono duplikatu.
GitHub CLI w tym środowisku ma nieważne uwierzytelnienie, ale odczyt przez
złącze GitHub potwierdził stan zdalny. Public `public-2026.11.22.1853` nie
istnieje w chwili kontroli. Lokalnej instalacji i próby NVDA nie wykonano.
