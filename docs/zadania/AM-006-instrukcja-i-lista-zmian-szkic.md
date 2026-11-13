# AM-002 i AM-006 — projekt instrukcji i listy zmian

**Roboczy tekst do korekty użytkownika. AMC Test alpha.500 jest zainstalowany, ale pomocnik odtwarzania ulega awarii przy starcie. Poprawka alpha.501 pozostaje w źródłach i wymaga budowy oraz żywego odbioru.**

## Instrukcja

W sesji Apple Music otwórz „Utwory” albo playlistę i zaznacz utwór. W alpha.501 Enter na albumie ma najpierw otworzyć listę jego utworów; Backspace wraca do zaznaczonego albumu. Enter na utworze z danymi katalogowymi ma uruchamiać go w tle przez osobny odtwarzacz Apple Music. Jeśli potrzebne będzie pierwsze logowanie, AMC poda polecenie otwarcia okna: menu Odtwarzanie, „Pokaż okno odtwarzacza Apple Music”. Zaloguj się samodzielnie, wróć do AMC i ponownie naciśnij Enter. Tego przebiegu nie potwierdzono jeszcze na zainstalowanej wersji.

Pierwsza pozycja menu kontekstowego na utworze z identyfikatorem katalogowym „Otwórz utwór w oryginalnym Apple Music” przekazuje link do zainstalowanej aplikacji. Apple Music może wtedy pokazać album bez uruchomienia zaznaczonego utworu. Wybranie tej pozycji zamyka aktywny odtwarzacz webowy AMC. Gdy Apple Music nie poda danych katalogowych dla utworu, AMC wyjaśnia to po Enter i nie pokazuje obu poleceń odtwarzania dla tej pozycji. Nie wybiera podobnego nagrania po tytule.

Spacja w AMC ma wstrzymywać lub wznawiać aktywny odtwarzacz Apple Music; Shift+Page Down i Shift+Page Up przechodzą do następnego i poprzedniego utworu. Gdy użytkownik wybierze oryginalną aplikację, te klawisze sterują jej bieżącą sesją. Odczyt stanu podaje rzeczywisty tytuł zgłoszony przez wybrane źródło. Przewijanie do wskazanego czasu nie jest jeszcze dostępne z interfejsu AMC. Funkcja jest przeznaczona tylko dla AMC Test. Poprawka alpha.501 nie jest jeszcze zainstalowana.

## Lista zmian

- Poprawka alpha.501 usuwa awarię procesu pomocniczego przy uruchomieniu z przekierowanymi potokami. Działanie wymaga próby po budowie i instalacji.
- Enter na albumie otwiera jego utwory, a Backspace wraca do albumu na liście.
- Enter na utworze z identyfikatorem katalogowym wybiera ten utwór w izolowanym odtwarzaczu Apple Music. Działanie wymaga żywej próby.
- Utwór bez danych katalogowych otrzymuje jasny komunikat NVDA; AMC nie zastępuje go innym nagraniem.
- Polecenie otwierania utworu w oryginalnym Apple Music pozostaje pierwszą pozycją menu kontekstowego.
- Widoczne okno odtwarzacza otwiera się tylko na jawne polecenie użytkownika.
- Dodano sterowanie bieżącą sesją oficjalnej aplikacji przez Spację i Shift+Page Down/Up.
- Odczyt „Teraz odtwarzane” pochodzi z rzeczywistego stanu Windows, a nie z zaznaczonego wiersza AMC.
- Dla utworów Biblioteki bez gotowego łącza AMC tworzy bezpieczny publiczny adres z identyfikatora katalogowego.
- Komunikaty i dostępność polecenia uwzględniają rodzaj zaznaczenia i obecność oryginalnej aplikacji.
- Ręczne sprawdzenie aktualizacji pokazuje wynik na pierwszym planie; błąd pozostaje w oknie do zamknięcia.

**Odbiór wydanego kandydata:** po ręcznej aktualizacji i sygnale użytkownika, że komputer jest wolny, uruchomić dokładnie wskazany utwór bez przełączania okna, sprawdzić słyszalny dźwięk, pełną długość, komunikaty NVDA oraz informację o aktywnej jakości, jeżeli strona ją udostępnia. Sam test startu paczki nie potwierdza odtwarzania.
