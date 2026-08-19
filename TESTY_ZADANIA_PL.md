# Zadania testowe AMC

- Numer zestawu: `AMC-TEST-061`
- Tytuł zestawu: Systemowe Otwórz w, usuwanie w odtwarzaczu i czyszczenie historii
- Wersja programu: `0.1.0-alpha.61`
- Utworzono: 2026-08-19, Europe/Warsaw
- Plik wyników: `wyniki-testow/WYNIKI_0.1.0-alpha.61.md`

Możesz testować całkowicie opisowo. Nie trzeba wypełniać pliku ani wybierać przed każdym zadaniem wariantu „OK” lub „błąd”. Najważniejsze jest podanie użytego skrótu, miejsca w programie i tego, co powiedział NVDA.

## Nowości alpha 61

### AMC-061-01 — Prawa strzałka i systemowe Otwórz w

Na pliku lokalnym naciśnij prawą strzałkę w głównej liście, Bibliotece i Historii odtwarzania.

Oczekiwane: bez otwierania menu AMC pojawia się bezpośrednio systemowy wybór aplikacji. Można jednorazowo otworzyć plik np. w foobar2000; ewentualna opcja zmiany aplikacji domyślnej zależy od wersji Windows.

### AMC-061-02 — Shift+Delete w odtwarzaczu

Odtwórz kopię pliku testowego, naciśnij `Shift+Delete`, najpierw wybierz Nie, a przy drugiej próbie Tak.

Oczekiwane: po Nie odtwarzanie i plik pozostają. Po Tak AMC zatrzymuje i zwalnia plik, przenosi go do Kosza, usuwa wpis i pozostawia działający odtwarzacz z następnym dostępnym elementem albo przechodzi do innej sesji, gdy lokalna była pusta.

### AMC-061-03 — Usunięty plik a Historia odtwarzania

Odtwórz plik, usuń go przez `Shift+Delete`, otwórz `Ctrl+H`, uruchom AMC ponownie i sprawdź historię ponownie.

Oczekiwane: usunięty element nie jest widoczny i nie wraca po restarcie. `Alt+góra/dół` również go pomija.

### AMC-061-04 — Krótka regresja

Sprawdź lewą strzałkę, `Ctrl+Shift+E/R/T`, zwykły `Delete` z `Ctrl+Z`, kopiowanie i naturalne przejście do następnego pliku.

Oczekiwane: zachowanie alpha 60 pozostaje bez zmian.

## Poprzedni zestaw regresyjny alpha 60

## AMC-060-01 — Start, ostatnio odtwarzany i F6

Uruchom AMC po wcześniejszym odtworzeniu pliku, lecz bez jego ponownego włączania. Sprawdź listę i `F6`.

Oczekiwane:

- program nie zaczyna sam odtwarzać i nie przesuwa zaznaczenia;
- zapisany element ma początek „Wstrzymany” albo „Ostatnio odtwarzany”;
- `F6` otwiera ten element w odtwarzaczu.

## AMC-060-02 — Jawne pytania o czas

Sprawdź `Ctrl+Shift+E`, `Ctrl+Shift+R` i `Ctrl+Shift+T` najpierw na liście, potem po `F6`.

Oczekiwane:

- w obu miejscach słychać odpowiednio czas od początku, pozostały i całkowity;
- działanie nie zależy od wyciszenia automatycznych komunikatów transportu.

## AMC-060-03 — Dwie niezależne historie

Na liście otwórz kolejno Bibliotekę, Kolejkę i Ulubione, po czym użyj `Alt+lewo` oraz `Alt+prawo`. Następnie odtwórz trzy różne pliki A, B i C, otwórz odtwarzacz i użyj `Alt+dół`, `Alt+dół`, `Alt+góra`.

Oczekiwane:

- boczny Alt cofa i ponawia widoki bieżącej sesji;
- w odtwarzaczu `Alt+dół` wybiera B, potem A, a `Alt+góra` wraca do B;
- każdy plik zachowuje własną pozycję;
- po ponownym uruchomieniu historia odtwarzania pozostaje, natomiast stos Wstecz/Naprzód może być pusty.

## AMC-060-04 — Widok Historia i lista źródłowa

Naciśnij `Ctrl+H`, sprawdź kolejność wpisów, następnie wróć do odtwarzacza i użyj `Page Up/Down`.

Oczekiwane:

- Historia jest uporządkowana od najnowszego wpisu i nie zawiera powtórzeń;
- `Page Up/Down` wybiera sąsiada odtwarzanego pliku na liście źródłowej, nie sąsiada z historii.

## AMC-060-05 — Strzałki lokalne we wszystkich widokach

Na pliku lokalnym użyj lewej i prawej strzałki w głównym katalogu, Bibliotece, Kolejce i Ulubionych.

Oczekiwane:

- lewa podaje krótkie informacje, a prawa otwiera menu działań i ustawia w nim fokus w każdym z lokalnych widoków;
- w sesjach nielokalnych strzałki zachowują zwykłą semantykę listy.

## AMC-060-06 — Delete i Shift+Delete

Na kopiach testowych plików sprawdź zwykły `Delete`, `Ctrl+Z`, a następnie `Shift+Delete`: najpierw odpowiedź Nie, potem Tak. Powtórz z wielokrotnym zaznaczeniem i z aktualnie otwartym plikiem.

Oczekiwane:

- `Delete` usuwa wpis z katalogu AMC i daje się cofnąć bez dotykania dysku;
- `Shift+Delete` wymaga potwierdzenia, po odpowiedzi Nie niczego nie zmienia, a po Tak zwalnia plik, przenosi udane pliki do systemowego Kosza i usuwa je z AMC;
- `Ctrl+Z` nie przywraca fizycznie usuniętych plików.

## AMC-060-07 — Kopiowanie wielokrotnego zaznaczenia

Zaznacz kilka plików. Wklej wynik `Ctrl+C` do edytora, a wynik `Ctrl+Shift+C` do edytora i do pustego folderu w Total Commanderze lub Eksploratorze.

Oczekiwane:

- `Ctrl+C` daje wszystkie nazwy, po jednej w wierszu;
- `Ctrl+Shift+C` daje pełne ścieżki w tekście i pozwala wkleić wszystkie fizyczne pliki.

## AMC-060-08 — Przełączanie sesji i regresja

Przejdź w każdej sesji do innego widoku, po czym przełączaj ją przez `Ctrl+1–9`. Sprawdź wyrywkowo OGG, prędkość, pasek, `Alt+Enter`, Kolejkę i naturalne przejście do następnego pliku.

Oczekiwane: komunikat zaczyna się od numeru i nazwy sesji, potem podaje przywrócony widok i element, np. „4, Pliki lokalne, Biblioteka…”. Pozostałe funkcje nie mają regresji.
