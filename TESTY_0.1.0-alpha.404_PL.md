# AMC 0.1.0-alpha.404 — historia nagrań

## Odczyt wiersza

Otwórz Historię nagrywania przez Alt+Shift+R. Przechodząc strzałkami, sprawdź nazwę nagrania, datę i godzinę. Nazwa folderu powinna być na końcu wiersza.

Nagranie przerwane lub nieudane ma nadal podawać swój rzeczywisty wynik. Plik w Bibliotece nie może go zasłaniać jako zwykłego udanego nagrania.

## Zmiana nazwy i usunięcie

Użyj kopii nagrania, nie jedynego oryginału. Zmień jego nazwę w obserwowanym folderze. Wróć do AMC: historia ma wskazywać nową ścieżkę, bez drugiego wpisu ze starą ścieżką. Sprawdź także po ponownym uruchomieniu.

Usuń kopię. Historia nie może nadal oznaczać jej jako gotowej do odtwarzania. Enter ma podać brak pliku zamiast próbować odtworzyć starą ścieżkę. Sprawdź również usunięcie poza obserwowanym folderem: powrót do okna lub ponowne otwarcie Historii odświeża obserwację.

## Niedostępny folder

Jeśli folder lub dysk jest niedostępny, zapis nagrania nie jest usuwany. Wiersz odróżnia niedostępność od potwierdzonego braku pliku. Po przywróceniu dostępu i ponownym wejściu do Historii nagranie powinno być znów dostępne.

## Powrót i granice

Otwórz historię z innej sesji i wróć Escape. Sesja oraz wcześniejszy filtr mają pozostać zachowane. Samo przeglądanie nie uruchamia ani nie zatrzymuje nagrania lub odsłuchu.

Przeniesienie poza obserwowane foldery może oznaczać nieznane nowe miejsce. AMC nie zgaduje go po samej nazwie obcego pliku: zachowuje zapis historii i uczciwie zgłasza brak starej ścieżki.

Ta wersja nie dodaje eksportu/dopisywania fragmentów ani integracji Apple Music. Nie jest deklaracją usunięcia wszystkich zgłaszanych opóźnień.

## Próby automatyczne

`--recording-files-acceptance` uruchamia rzeczywiste okno i magazyn na izolowanych danych Windows. `scripts/test-recording-files-mutations.py` celowo uszkadza osiem reguł w osobnej kopii i wymaga pełnego licznika przypadków przy każdej próbie. Nie uruchamiać tych testów równolegle z pracą na pulpicie lub podczas nagrywania.
