# AMC 0.1.0-alpha.395 — aktualizacje i albumy wykonawcy Spotify

## Co się zmieniło

- F11 otwiera sprawdzanie aktualizacji AMC. To samo okno jest dostępne z menu Pomoc.
- Wynik sprawdzenia, numery wersji i postęp są dostępne dla NVDA w polach możliwych do ponownego odczytania.
- Pobranie i instalacja wymagają świadomego wyboru. Samo Escape ani zamknięcie okna aktualizacji nie uruchamia instalacji.
- AMC sprawdza sumę pobranego pliku. Instalator czeka na zakończenie starego procesu, a po udanej instalacji uruchamia nową wersję.
- Błąd zapisu przed jawną aktualizacją pozostawia AMC otwarte i pokazuje wyjaśnienie. Gdy zawiedzie dopiero końcowy zapis podczas zamykania, program wymaga potwierdzenia komunikatu i nie uruchamia instalatora.
- Również brak sprawdzonej paczki lub błąd uruchomienia instalatora po udanym zapisie wymaga potwierdzenia komunikatu. Treść rozróżnia błąd zapisu od odmowy instalacji.
- Zgoda użytkownika, wersja, suma i ścieżki są sprawdzane na tym samym zestawie danych, z którego uruchamiana jest instalacja.
- Poprawiono błąd HTTP 400 przy otwieraniu albumów wykonawcy Spotify. Mniejsza strona wyników nie wyłącza pobierania kolejnych stron.

YouTube, dekodery radia i regulacja tempa TimeShift nie były zmieniane w tym wydaniu.

## Krótkie sprawdzenie po instalacji

1. Naciśnij F11. Fokus powinien trafić do wyniku sprawdzenia; przeczytaj treść strzałkami. Tab pozwala odczytać wersje i przejść do przycisków.
2. Zamknij okno przez Escape. AMC ma pozostać otwarte, z fokusem na wcześniejszej liście. Ponownie naciśnij F11.
3. W Spotify otwórz menu powiązań utworu i wybierz przejście do wykonawcy. Lista albumów nie powinna kończyć się błędem 400.
4. Przy następnym nowszym wydaniu wybór „Pobierz i zainstaluj” ma pobrać paczkę, sprawdzić ją, zamknąć AMC zwykłą ścieżką i wznowić program po instalacji. Jeżeli trwają nagrania, nadal obowiązuje ostrzeżenie o ich zakończeniu.

## Wykonane sprawdzenia

- Pełny zestaw Windows po poprawkach: 134 testy zaliczone; osobno sprawdzona logika Core.
- Żywy NVDA: F11 w rzeczywistym oknie, początkowy fokus, odczyt treści, powrót na listę, oba komunikaty błędu zapisu oraz odmowa i wyjątek startu po poprawnym zapisie.
- Pomocnik instalacji: pozytywny przebieg i odmowy po niezgodnej sumie, braku pliku oraz błędzie instalatora.
- Rzeczywista instalacja na HERMES: finalna paczka zastąpiła wcześniejszego kandydata w dotychczasowym katalogu; pomocnik zakończył się kodem 0, EXE jest zgodny z paczką, nowy proces wystartował w sesji interaktywnej, a F11 sprawdzono w zainstalowanej wersji. Wcześniejsza próba 394 → 395 dotyczyła pierwszego kandydata, nie finalnej paczki.
- Spotify: test HTTP odtwarza błąd 400 przed poprawką i przechodzi po niej; mierzy również drugą stronę wyników. To test klienta na danych próbnych, nie ponowne logowanie do konta użytkownika.
