# Rejestr ryzyk i niejednoznaczności AMC

Ten plik przechowuje obserwacje, których nie należy jeszcze nazywać ani
potwierdzonym błędem, ani prawidłowym zachowaniem. Ma umożliwić przekazanie
projektu innemu modelowi, testerowi albo narzędziu automatycznemu bez
odtwarzania całej historii rozmów.

Rejestr i wynikająca z niego końcowa macierz testów obejmują **całą aplikację
AMC**: wspólny rdzeń, wszystkie sesje, listy i widoki, odtwarzanie,
nagrywanie, wyszukiwanie, ustawienia, dostępność, zapis danych oraz integracje
systemowe. Wpis dodany podczas pracy nad jednym modułem nie ogranicza testów
wyłącznie do tego modułu, jeżeli ten sam mechanizm jest współdzielony.

## Zasady prowadzenia rejestru

- Każdy wpis rozdziela **fakt zaobserwowany** od hipotezy i interpretacji.
- Brak drugiego odtworzenia nie zamyka sprawy; wpis otrzymuje stan
  **do ponownego testu**.
- Po potwierdzeniu błędu powstaje minimalny scenariusz w
  `TESTY_ZADANIA_PL.md`, a jeśli to możliwe także test automatyczny.
- Po naprawie wpis pozostaje w rejestrze ze stanem **zamknięte**, numerem
  wersji, commitem i wskazaniem testu regresji.
- Dane wrażliwe, podpisane adresy strumieni, tokeny i pełna zawartość schowka
  nie mogą trafiać do tego pliku ani do logów.
- Przed wydaniem publicznym wszystkie wpisy otwarte i „do ponownego testu”
  należy przejrzeć osobno, także z NVDA i co najmniej jednym niezależnym
  przebiegiem testów automatycznych.

## Szablon wpisu

### AMC-RYZYKO-NNN — krótka nazwa

- Stan: otwarte / do ponownego testu / potwierdzone / zamknięte.
- Dotyczy: wersja, moduł i widok.
- Fakt zaobserwowany: dokładnie to, co rzeczywiście wystąpiło.
- Warunki: plik, format, źródło, aktywny widok, klawisze i istotne ustawienia.
- Częstość: jeden raz / kilka razy / zawsze / nieustalona.
- Hipotezy: możliwe przyczyny, wyraźnie oddzielone od faktów.
- Dowody: zakres czasu logu, komunikat, test, zrzut albo commit — bez sekretów.
- Następny test: najmniejsza próba, która może potwierdzić albo obalić problem.
- Rozstrzygnięcie: wersja, commit i test regresji po zamknięciu.

## Aktualne wpisy

### AMC-RYZYKO-001 — chwilowa kolizja równoległego zapisu stanu

- Stan: do ponownego testu.
- Dotyczy: `alpha.229`, automatyczny test rdzenia.
- Fakt zaobserwowany: podczas pierwszego łącznego przebiegu test
  „Serializacja równoległych zapisów stanu” jeden raz zakończył się
  komunikatem „Nie można usunąć pliku, który ma zostać zamieniony”.
- Częstość: jeden raz. Natychmiastowe powtórzenie samego zestawu rdzenia oraz
  pełny przebieg publikacyjny zakończyły się poprawnie.
- Hipotezy: krótkotrwała blokada testowego pliku przez system Windows albo
  wyścig w ścieżce atomowej zamiany. Nie ma dowodu, że zmiana pobierania
  Podcastów była przyczyną.
- Dowody: przebieg przygotowania commita `86500b3`; końcowe testy rdzenia i
  Windows przeszły.
- Następny test: uruchomić test równoległego zapisu wielokrotnie w pętli,
  osobno na NTFS i w folderze synchronizowanym, oraz sprawdzić pozostawione
  pliki tymczasowe po ewentualnym niepowodzeniu.

### AMC-RYZYKO-002 — rzeczywiste przerwanie pobierania odcinka

- Stan: do testu ręcznego.
- Dotyczy: `alpha.229`, `Ctrl+D`, Podcasty.
- Fakt zaobserwowany: test automatyczny potwierdza odrzucenie odpowiedzi
  krótszej niż zadeklarowana i brak urwanego pliku pod nazwą końcową.
  Nie przeprowadzono jeszcze ręcznej próby odłączenia sieci ani zamknięcia
  AMC podczas pobierania dużego, prawdziwego odcinka.
- Hipotezy: lokalny plik roboczy powinien zostać usunięty przy przerwaniu
  transferu. Kompletny plik może świadomie pozostać w katalogu ratunkowym,
  jeżeli zawiedzie dopiero publikacja do folderu docelowego.
- Dowody: automatyczny test „odporne pobieranie odcinków podcastów”.
- Następny test: zadanie `AMC-229-05` z pliku `TESTY_ZADANIA_PL.md`, osobno dla
  zwykłego folderu i folderu synchronizowanego.

### AMC-RYZYKO-003 — komunikat ukończenia przy otwartym oknie wyszukiwania

- Stan: do testu NVDA.
- Dotyczy: `alpha.229`, wyniki `Ctrl+F` i `Ctrl+Shift+F`.
- Fakt zaobserwowany: pobieranie można rozpocząć bez zamknięcia wyników, a
  okno zachowuje zaznaczenie. Kod wysyła komunikat rozpoczęcia z okna wyników,
  natomiast końcowy komunikat pochodzi z głównego okna AMC.
- Hipotezy: zależnie od aktywnego okna i ustawień NVDA końcowy komunikat może
  być usłyszany prawidłowo albo zostać pominięty. Nie ma jeszcze wyniku testu
  ręcznego.
- Następny test: pobrać jeden oraz kilka odcinków z otwartych wyników,
  pozostając w tym oknie do końca, i sprawdzić mowę, brajl oraz fokus.

### AMC-RYZYKO-004 — nazwa i rozszerzenie pliku ustalane z metadanych kanału

- Stan: świadome ograniczenie, do testów zgodności.
- Dotyczy: `alpha.229`, `Ctrl+D` i `Ctrl+S`.
- Fakt zaobserwowany: AMC wybiera rozszerzenie z adresu audio albo typu MIME;
  gdy oba są niejednoznaczne, używa `.mp3`.
- Hipotezy: wadliwy kanał może deklarować typ niezgodny z rzeczywistym
  kontenerem. Sam transfer pozostanie kompletny, ale nazwa może mieć mylące
  rozszerzenie.
- Następny test: przygotować odpowiedzi MP3, AAC/M4A, OGG, OPUS i nietypowy
  adres bez rozszerzenia z poprawnym oraz błędnym `Content-Type`. W przyszłości
  rozważyć ograniczone rozpoznanie sygnatury po pobraniu, bez dekodowania całego
  pliku.

### AMC-RYZYKO-005 — plik pobrany wcześniej, a później przeniesiony poza AMC

- Stan: świadome zachowanie wymagające oceny użyteczności.
- Dotyczy: widok **Pobrane** Podcastów.
- Fakt zaobserwowany: `Ctrl+D` pomija ponowne pobranie tylko wtedy, gdy zapisany
  `DownloadPath` nadal istnieje. Po przeniesieniu albo usunięciu pliku polecenie
  może pobrać odcinek ponownie do folderu domyślnego.
- Hipotezy: jest to bezpieczniejsze niż pozostawienie martwego wpisu, ale
  przyszłe skanowanie folderów lub ręczne wskazanie przeniesionego pliku może
  być wygodniejsze.
- Następny test: pobrać odcinek, przenieść go poza AMC, odświeżyć widok
  **Pobrane** i ponowić `Ctrl+D`; ocenić komunikat i oczekiwany stan wpisu.

### AMC-RYZYKO-006 — techniczny agregat Radia po wyszukiwaniu

- Stan: zamknięte w `alpha.230`, pozostaje test regresji NVDA.
- Dotyczy: `alpha.229`, Radio internetowe, `Ctrl+F`, Ulubione i przejście
  zwykłym Enterem.
- Fakt zaobserwowany: po wyszukaniu Radia Kolor, dodaniu go do Ulubionych i
  naciśnięciu Enter główna lista odsłoniła ponad sto nieznanych stacji zamiast
  właściwego widoku użytkownika.
- Częstość: potwierdzone raz przez użytkownika i jednoznacznie przez log.
- Przyczyna: wynik był kierowany do technicznego widoku `Multimedia`, czyli
  całego wewnętrznego indeksu Radia. Log z `alpha.229` o 21:20 wskazuje 161
  widocznych wierszy przy działaniu na jednym wyniku.
- Rozstrzygnięcie: `alpha.230` kieruje ulubiony wynik do Ulubionych, pozostały
  do Biblioteki i normalizuje każde techniczne `Multimedia` Radia do
  Biblioteki. Test automatyczny sprawdza wszystkie trzy warianty.
- Następny test: wykonać `AMC-230-01` i `AMC-230-02`, a potem powtórzyć
  analogiczny przepływ w Podcastach oraz przyszłej usłudze streamingowej.

### AMC-RYZYKO-007 — niepełne Ctrl+Z po usunięciu podcastu

- Stan: zamknięte w `alpha.240`, pozostaje test regresji NVDA i restartu.
- Dotyczy: usunięcia całego podcastu z Biblioteki i natychmiastowego `Ctrl+Z`.
- Fakt zaobserwowany: wiersz kanału wracał na listę, lecz Enter nie otwierał
  już jego odcinków; następne `Ctrl+Shift+L` ponownie oznajmiało usunięcie.
- Przyczyna: historia przywracała flagę w żywym elemencie listy, ale gałąź
  cofania nie zapisywała odtworzonej przynależności do trwałego rekordu
  Podcastów. Po przebudowie listy możliwa była dodatkowo nieaktualna referencja
  do wcześniejszej instancji elementu.
- Rozstrzygnięcie: cofnięcie rozwiązuje aktualny element po stabilnym
  identyfikatorze, synchronizuje rekord subskrypcji i zleca trwały zapis.
  Operacja nie pobiera kanału z sieci ani nie uruchamia odtwarzania.
- Następny test: wykonać `AMC-240-01`–`AMC-240-03`, w tym przebudowę listy i
  ponowne uruchomienie programu.

## Stałe, globalne obszary regresji przed publikacją

Poniższe obszary dotyczą całego AMC, a nie wyłącznie modułu rozwijanego w
danej wersji. Miały w historii projektu problemy zależne od czasu, urządzenia
albo programu zewnętrznego. Nie oznacza to, że obecnie są zepsute:

- fokus NVDA po szybkim przełączaniu widoków, Escape, oknach modalnych i
  asynchronicznym otwieraniu źródła;
- odłączenie i ponowne podłączenie zapamiętanego urządzenia audio;
- bardzo duże, uszkodzone lub częściowo dostępne pliki oraz placeholdery
  iCloud, OneDrive i Dysku Google;
- HLS, przekierowania i starsze strumienie ICY podczas odtwarzania oraz
  równoległego nagrywania;
- schowek Windows przy szybkim `Ctrl+C`, `Ctrl+Shift+C`, menedżerach schowka i
  czytniku ekranu;
- globalny prefiks oraz kombinacje przejmowane przez NVDA, Narratora albo
  system Windows;
- każda nowa lista, pole kombi i menu: pierwszy element, ruch strzałkami,
  ponowne otwarcie, powrót fokusa i brak technicznych reprezentacji obiektów.

Ten zestaw powinien zasilać końcowy plan testów macierzowych, a nie zastępować
konkretne zgłoszenia i wyniki kolejnych wersji.
