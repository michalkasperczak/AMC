# RARCH-002 — pobieranie fragmentu Radio Archiwisty

- Życzenie użytkownika: 31.10.2026, po wydaniu `alpha.456`.
- Wersja docelowa: AMC Test `0.1.0-alpha.457`.
- Status: kod i pełna regresja gotowe; pakiet, publikacja i lokalna instalacja
  do weryfikacji.
- Wcześniejsza decyzja `RARCH-001` o pobieraniu pełnego nagrania przez `Ctrl+D`
  pozostaje aktualna. Ta funkcja dodaje osobny zapis zakresu.

## Zachowanie i uzasadnienie

- W sesji Radio Archiwista zaznacz nagranie na liście lub w odtwarzaczu.
  `Ctrl+Shift+D`, menu Pliki → Pobierz fragment nagrania Radio Archiwisty
  oraz paleta `Ctrl+Shift+K` otwierają okno początku i końca.
- Początek i koniec są polami tekstowymi o jawnych nazwach dla NVDA. Przyjmują
  `MM:SS` albo `GG:MM:SS`; początek domyślnie `0:00`, koniec to czas nagrania.
  Tab przechodzi między polami i przyciskami, Enter zatwierdza, Escape anuluje.
  Po zamknięciu fokus wraca do listy lub odtwarzacza. Błędny zakres jest
  wyjaśniany komunikatem i okno pozwala poprawić dane.
- AMC pobiera źródłowy MP3 do tymczasowego katalogu profilu, a istniejący
  silnik przycinania zapisuje nowy MP3 bez zmiany oryginału. Granice mogą
  zostać dopasowane do ramki kodeka, jak w trybie „Bez konwersji” lokalnego
  przycinania. Gdy nie ma FFmpeg, AMC wyjaśnia, że jest wymagany.
- Postęp pobierania jest widoczny, operację można anulować. Fragment dostaje
  nazwę z godzinami początku i końca oraz kolejny numer, gdy nazwa już istnieje.
  Trafia do katalogu `radioarchiwista/pobrane` i Historii pobrań (`Ctrl+J`).
  Oryginał i istniejące pliki nie są nadpisywane. Po błędzie lub anulowaniu
  źródło tymczasowe i niepełny fragment są usuwane.
- `Ctrl+Shift+D` pozostaje skrótem pobierania wideo w Podcastach. Menu,
  kontekstowa pomoc `Ctrl+F1`, pełna pomoc i paleta wskazują działanie właściwe
  dla bieżącej sesji. W Public funkcja nie jest dostępna.

Publiczna strona Radio Archiwisty udostępnia pola początku i końca oraz
wycina MP3 po pobraniu w przeglądarce. Jej obecny konfigurator nie podaje
oddzielnego adresu serwera do pobrania już przyciętego pliku:
https://github.com/radioarchiwista/radioarchiwista.github.io/blob/main/assets/player.js

## Testy i wdrożenie

- Testy celowane 31.10.2026: **112 zaliczonych** po końcowej poprawce
  usuwania pustego pliku. Pełna regresja przed tą wąską poprawką:
  **2220 zaliczonych, 143 pominięte, 13 podtestów zaliczonych,
  0 błędów** (145,76 s). Pierwszy przebieg był zakłócony przez odmowę
  zapisu w katalogach tymczasowych piaskownicy. Powtórzono go z prawidłowym
  dostępem do projektowego `.tmp-tests`; wynik końcowy jest czysty.
- Kompilacja hosta, pakiet, instalator, suma SHA-256 i publikacja: do
  uzupełnienia po weryfikacji.
- Instalacja i działająca wersja AMC Test: do uzupełnienia. Odbiór z NVDA
  wymaga próby użytkownika.

## Projekt instrukcji i opisu zmian do akceptacji użytkownika

Instrukcja: „W Radio Archiwiście zaznacz nagranie i naciśnij Ctrl+Shift+D.
Wpisz początek i koniec jako minuty:sekundy, np. 12:30 i 18:00. Enter zapisuje
fragment jako osobny plik MP3; Escape rezygnuje. Pobrany plik znajdziesz w
Historii pobrań pod Ctrl+J.”

Opis zmian: „AMC Test pozwala pobierać wybrany fragment nagrania Radio
Archiwisty. Zakres wpisuje się w dostępnym z klawiatury oknie, zapis odbywa
się przez obecny silnik przycinania i pojawia w Historii pobrań.”
