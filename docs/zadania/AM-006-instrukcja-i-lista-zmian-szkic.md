# AM-002 i AM-006 — projekt instrukcji i listy zmian

**Roboczy tekst do korekty użytkownika. Funkcja nie jest jeszcze zainstalowana ani wydana.**

## Instrukcja

W sesji Apple Music otwórz „Utwory” lub playlistę i zaznacz utwór. W przygotowywanej wersji AMC Test Enter ma uruchamiać ten utwór w tle przez osobny odtwarzacz Apple Music. Jeśli potrzebne będzie pierwsze logowanie, AMC poda polecenie otwarcia okna: menu Odtwarzanie, „Pokaż okno odtwarzacza Apple Music”. Zaloguj się samodzielnie, wróć do AMC i ponownie naciśnij Enter. Tego przebiegu nie potwierdzono jeszcze na zainstalowanej wersji.

Pierwsza pozycja menu kontekstowego „Otwórz utwór w oryginalnym Apple Music” przekazuje link do zainstalowanej aplikacji. Apple Music może wtedy pokazać album bez uruchomienia zaznaczonego utworu. Wybranie tej pozycji zamyka aktywny odtwarzacz webowy AMC. Gdy utwór nie ma identyfikatora katalogowego, AMC nie udostępnia dla niego odtwarzania webowego.

Spacja w AMC ma wstrzymywać lub wznawiać aktywny odtwarzacz Apple Music; Shift+Page Down i Shift+Page Up przechodzą do następnego i poprzedniego utworu. Gdy użytkownik wybierze oryginalną aplikację, te klawisze sterują jej bieżącą sesją. Odczyt stanu podaje rzeczywisty tytuł zgłoszony przez wybrane źródło. Przewijanie do wskazanego czasu nie jest jeszcze dostępne z interfejsu AMC. Funkcja jest przeznaczona tylko dla AMC Test i pozostaje niezainstalowana.

## Lista zmian

- W kandydacie źródłowym Enter wybiera numeryczny utwór katalogowy w izolowanym odtwarzaczu Apple Music. Działanie wymaga żywej próby.
- Polecenie otwierania utworu w oryginalnym Apple Music pozostaje pierwszą pozycją menu kontekstowego.
- Widoczne okno odtwarzacza otwiera się tylko na jawne polecenie użytkownika.
- Dodano sterowanie bieżącą sesją oficjalnej aplikacji przez Spację i Shift+Page Down/Up.
- Odczyt „Teraz odtwarzane” pochodzi z rzeczywistego stanu Windows, a nie z zaznaczonego wiersza AMC.
- Dla utworów Biblioteki bez gotowego łącza AMC tworzy bezpieczny publiczny adres z identyfikatora katalogowego.
- Komunikaty i dostępność polecenia uwzględniają rodzaj zaznaczenia i obecność oryginalnej aplikacji.

**Przed wydaniem jako odtwarzania Apple Music:** po sygnale użytkownika, że komputer jest wolny, uruchomić dokładnie wskazany utwór bez przełączania okna, sprawdzić słyszalny dźwięk, pełną długość i komunikaty NVDA, a następnie zbudować i przetestować pakiet. Dotychczasowa kompilacja źródeł nie zalicza tej bramki.
