# AMC 0.1.0-alpha.288 — zaznaczanie fragmentu od końca

1. W odtwarzaczu lokalnego pliku przejdź do późniejszego miejsca i naciśnij
   `O`. Oczekiwane: program zapisuje koniec i mówi, aby ustawić początek
   klawiszem `I`.

2. Cofnij się, naciśnij `I`, a następnie `X`. Oczekiwane: program podaje czas
   początku i długość; okno eksportu zawiera przedział od wcześniejszego `I` do
   późniejszego `O`.

3. Ustaw tylko `O`, zamknij program i uruchom ponownie. Po otwarciu tego samego
   pliku naciśnij `Shift+O`. Oczekiwane: odtwarzacz wraca do zapisanego końca,
   mimo że początek nie został wcześniej wskazany.

4. Spróbuj ustawić `I` później niż zapisane `O`. Oczekiwane: AMC odrzuca tylko
   nowy początek, zachowuje koniec i nie pozwala wyeksportować nieprawidłowego
   przedziału.

5. Powtórz dla pobranego odcinka podcastu oraz sprawdź `Ctrl+X` na kopii
   testowej pliku. Oczekiwane: te same reguły, a destrukcyjne usuwanie nadal
   wymaga potwierdzenia i pełnej kopii bezpieczeństwa.
