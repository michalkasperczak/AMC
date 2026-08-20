# Zadania testowe AMC

- Numer zestawu: `AMC-TEST-068`
- Tytuł zestawu: Trwałe zakładki
- Wersja programu: `0.1.0-alpha.68`
- Utworzono: 2026-08-20, Europe/Warsaw
- Plik wyników: `wyniki-testow/WYNIKI_0.1.0-alpha.68.md`

Możesz testować całkowicie opisowo. Nie trzeba wypełniać pliku ani wybierać przed każdym zadaniem wariantu „OK” lub „błąd”. Najważniejsze jest podanie użytego skrótu, miejsca w programie i tego, co powiedział NVDA.

## Nowości alpha 68

### AMC-068-01 — Dodawanie szybkich zakładek

Otwórz dłuższy plik w odtwarzaczu. Przejdź mniej więcej do 2 minut i naciśnij `B`, potem przejdź do innego miejsca i ponownie naciśnij `B`.

Oczekiwane: program krótko mówi „Dodano zakładkę” oraz czas. Nie otwiera się żadne dodatkowe okno, odtwarzanie i fokus pozostają w odtwarzaczu.

### AMC-068-02 — Duplikat w tym samym miejscu

Bez zmiany pozycji naciśnij `B` ponownie.

Oczekiwane: program mówi, że zakładka już istnieje; lista nie otrzymuje drugiego wpisu w tej samej sekundzie.

### AMC-068-03 — Nawigacja wewnątrz jednego materiału

W tym samym pliku użyj kilka razy `Shift+Page Up` i `Shift+Page Down`, także przed pierwszą i za ostatnią zakładką.

Oczekiwane: skróty ustawiają dokładne zapisane miejsca i czytają czas. Nie otwierają innego pliku. Na krańcach pojawia się jednoznaczny komunikat o braku poprzedniej albo następnej zakładki.

### AMC-068-04 — Globalna lista Ctrl+B

Dodaj zakładki w co najmniej dwóch sesjach, następnie naciśnij `Ctrl+B`. Nawiguj strzałkami, wpisz początkową literę tytułu i sprawdź filtr `Ctrl+K`.

Oczekiwane: zwykła dostępna lista zawiera zakładki ze wszystkich sesji. Każdy wiersz podaje kolejno tytuł, czas, usługę i słowo „zakładka”. Nawigacja literowa oraz filtr działają jak na innych listach.

### AMC-068-05 — Otwarcie zakładki z innej sesji

Na globalnej liście wybierz zakładkę należącą do innej sesji i naciśnij Enter. Potem naciśnij `Escape`.

Oczekiwane: AMC przełącza właściwą sesję, otwiera materiał i ustawia zapisany czas. `Escape` wraca do globalnej listy na tej samej zakładce.

### AMC-068-06 — Bezpieczne usuwanie

Na liście Zakładek naciśnij najpierw `Shift+Delete`, a następnie zwykły `Delete`. Sprawdź, czy plik nadal istnieje na dysku.

Oczekiwane: `Shift+Delete` wyjaśnia, że z tego widoku nie usuwa pliku. `Delete` usuwa tylko wybraną zakładkę i ustawia fokus na sąsiednim wpisie. Źródłowy plik pozostaje bez zmian.

### AMC-068-07 — Zapis po ponownym uruchomieniu

Pozostaw kilka zakładek, zamknij AMC przez `Alt+F4`, uruchom ponownie i naciśnij `Ctrl+B`.

Oczekiwane: wszystkie pozostawione zakładki, ich czasy i sesje są zachowane. Program nie rozpoczyna odtwarzania samoczynnie.

### AMC-068-08 — B na zwykłej liście i krótka regresja

Wróć do zwykłej listy mediów i naciśnij `B`. Sprawdź też lewą strzałkę, wznowienie pozycji, `Page Up/Down`, `Alt+góra/dół`, OGG i `Delete` w odtwarzaczu.

Oczekiwane: na zwykłej liście `B` nadal przechodzi do tytułu zaczynającego się na B, zamiast tworzyć zakładkę. Pozostałe potwierdzone funkcje nie zmieniają się. `Ctrl+Shift+B` jest na razie celowo wolne i zarezerwowane dla zakładki nazwanej.

## Poprzedni zestaw regresyjny alpha 67

## Nowości alpha 67

### AMC-067-01 — Prawa strzałka i fokus listy

Na lokalnym pliku naciśnij prawą strzałkę, potem strzałkę w dół i w górę.

Oczekiwane: nie otwiera się żadne okno systemowe, fokus pozostaje na liście, a NVDA nadal czyta kolejne elementy.

### AMC-067-02 — Menu lokalnego pliku

Otwórz menu kontekstowe na liście i w odtwarzaczu.

Oczekiwane: nie ma pozycji „Otwórz w…”. Pozostaje „Otwórz w domyślnej aplikacji” oraz właściwe działania AMC.

### AMC-067-03 — Regresja

Sprawdź lewą strzałkę, wznowienie pozycji po restarcie, `Delete` w odtwarzaczu i `Shift+Delete` na liście.

Oczekiwane: pozostałe funkcje działają bez zmian.

## Poprzedni zestaw regresyjny alpha 66

## Nowości alpha 66

### AMC-066-01 — Osobny proces z menu

Na pliku lokalnym otwórz menu kontekstowe i wybierz „Otwórz w…”.

Oczekiwane: systemowy wybór aplikacji staje się aktywnym oknem i NVDA odczytuje jego kontrolki. Po `Escape` fokus wraca do AMC.

### AMC-066-02 — Osobny proces prawą strzałką

Na tym samym pliku naciśnij prawą strzałkę.

Oczekiwane: działanie i fokus są takie same jak z menu. Jeżeli ponownie wystąpi cisza lub konieczność użycia `Alt+Tab`, uznajemy mechanizm za niezgodny i usuwamy funkcję.

### AMC-066-03 — Regresja

Sprawdź lewą strzałkę, wznowienie pozycji po restarcie, `Delete` w odtwarzaczu oraz `Shift+Delete` na liście.

Oczekiwane: pozostałe funkcje nie zmieniają się.

## Poprzedni zestaw regresyjny alpha 65

## Nowości alpha 65

### AMC-065-01 — Otwórz w przez menu

Na pliku lokalnym otwórz menu kontekstowe, wybierz „Otwórz w…” i odczekaj chwilę.

Oczekiwane: systemowy wybór aplikacji otrzymuje fokus, NVDA odczytuje jego kontrolki i można wskazać program albo anulować. Po zamknięciu fokus wraca do tego samego pliku w AMC.

### AMC-065-02 — Otwórz w prawą strzałką

Na tym samym pliku naciśnij prawą strzałkę i odczekaj chwilę.

Oczekiwane: wynik jest taki sam jak z menu. Jeżeli działa menu, ale nie strzałka, zapisz dokładnie tę różnicę — wtedy zachowamy funkcję tylko w menu.

### AMC-065-03 — Anulowanie i regresja

Anuluj systemowe okno przez `Escape`, sprawdź nawigację listy, lewą strzałkę, pamiętanie pozycji i `Delete` w odtwarzaczu.

Oczekiwane: `Escape` nie zamyka AMC, fokus wraca na listę, a potwierdzone funkcje `alpha.63–64` pozostają bez zmian.

## Poprzedni zestaw regresyjny alpha 64

## Nowości alpha 64

### AMC-064-01 — Prawa strzałka nie gubi fokusu

Na pliku lokalnym naciśnij prawą strzałkę, potem strzałkę w dół i w górę.

Oczekiwane: nie pojawia się systemowe okno ani cisza spowodowana utratą fokusu. NVDA pozostaje na liście, a zwykła nawigacja nadal czyta elementy.

### AMC-064-02 — Menu pliku lokalnego

Otwórz menu kontekstowe pliku na liście i w odtwarzaczu.

Oczekiwane: nie ma pozycji „Otwórz w…”. „Otwórz w domyślnej aplikacji” pozostaje dostępne. `Delete` w odtwarzaczu nadal usuwa tylko wpis AMC, a `Shift+Delete` jest dostępne wyłącznie na liście.

### AMC-064-03 — Krótka regresja alpha 63

Sprawdź wznowienie pozycji po restarcie, lewą strzałkę z `kb/s`, `Delete` w odtwarzaczu i `Ctrl+Z`.

Oczekiwane: wszystkie potwierdzone funkcje `alpha.63` działają bez zmian.

## Poprzedni zestaw regresyjny alpha 63

## Nowości alpha 63

### AMC-063-01 — Pozycja ostatniego pliku po ponownym uruchomieniu

Odtwórz dłuższy plik, przejdź co najmniej minutę od początku, wstrzymaj i zamknij AMC przez `Alt+F4`. Uruchom ponownie, użyj `F6`, a następnie `Ctrl+Shift+E`. Wznów odtwarzanie.

Oczekiwane: AMC nie uruchamia dźwięku samoczynnie, ale od razu pokazuje i podaje zapisaną pozycję. Pierwsze wznowienie zaczyna się z tego miejsca, a nie od `0:00`.

### AMC-063-02 — Prawa strzałka bez skojarzenia pliku — historyczne, wycofane w alpha 64

Na lokalnym pliku naciśnij prawą strzałkę. Najlepiej sprawdzić także rozszerzenie, dla którego Windows nie ma poprawnej aplikacji domyślnej.

Ręczny test wykazał utratę czytelnego fokusu NVDA. Aktualne wymaganie opisuje `AMC-064-01`.

### AMC-063-03 — Delete w lokalnym odtwarzaczu

Otwórz kopię pliku testowego w odtwarzaczu i naciśnij `Delete`. Sprawdź dysk, wypowiedź NVDA i następny element. Następnie naciśnij `Ctrl+Z`.

Oczekiwane: plik pozostaje na dysku. AMC usuwa tylko swój wpis, wstrzymuje usuwany element, podaje następny element albo sesję, a `Ctrl+Z` przywraca wpis.

### AMC-063-04 — Fizyczne usuwanie tylko na liście

W odtwarzaczu naciśnij `Shift+Delete`, a potem otwórz jego menu kontekstowe. Następnie wróć na listę i użyj `Shift+Delete` na kopii pliku testowego.

Oczekiwane: w odtwarzaczu skrót niczego fizycznie nie usuwa i nie ma pozycji przenoszenia do Kosza. Na liście pozostaje dotychczasowe pytanie potwierdzające i systemowy Kosz.

### AMC-063-05 — Krótka regresja

Sprawdź lewą strzałkę z `kb/s`, `Alt+F4` z odtwarzacza, `Ctrl+Shift+E/R/T`, OGG i zmianę prędkości.

Oczekiwane: zachowanie `alpha.62` pozostaje bez zmian.

## Poprzedni zestaw regresyjny alpha 62

## Nowości alpha 62

### AMC-062-01 — Bitrate pliku wcześniej odtwarzanego

Na liście zaznacz odtwarzany wcześniej plik i naciśnij lewą strzałkę.

Oczekiwane: komunikat zawiera rozszerzenie, czas, wartość `kb/s`, dostępne `kHz` i rozmiar.

### AMC-062-02 — Bitrate pliku jeszcze nieodtwarzanego

Dodaj nowy plik, nie uruchamiaj go i od razu naciśnij lewą strzałkę. Powtórz ją drugi raz.

Oczekiwane: AMC odczytuje metadane tylko tego pliku i podaje średni bitrate w `kb/s`. Druga próba korzysta z zapisanego wyniku. Nie rozpoczyna się odtwarzanie ani skanowanie całej listy.

### AMC-062-03 — Alt+F4 z odtwarzacza

Otwórz odtwarzacz przez `F6` i naciśnij `Alt+F4`.

Oczekiwane: cała aplikacja zamyka się od razu i zapisuje stan; nie następuje najpierw powrót do listy. Po ponownym uruchomieniu `Escape` i `Shift+F6` nadal wracają tylko do listy.

## Poprzedni zestaw regresyjny alpha 61

## Nowości alpha 61

### AMC-061-01 — Prawa strzałka i systemowe Otwórz w

Na pliku lokalnym naciśnij prawą strzałkę w głównej liście, Bibliotece i Historii odtwarzania.

Oczekiwane: bez otwierania menu AMC pojawia się bezpośrednio systemowy wybór aplikacji. Można jednorazowo otworzyć plik np. w foobar2000; ewentualna opcja zmiany aplikacji domyślnej zależy od wersji Windows.

### AMC-061-02 — Shift+Delete w odtwarzaczu — historyczne, wycofane w alpha 63

Odtwórz kopię pliku testowego, naciśnij `Shift+Delete`, najpierw wybierz Nie, a przy drugiej próbie Tak.

To zachowanie było testowane w `alpha.61–62`, lecz zostało świadomie wycofane. Aktualne wymaganie opisuje `AMC-063-04`.

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
