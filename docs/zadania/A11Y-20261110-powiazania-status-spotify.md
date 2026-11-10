# AMC Test i Public — nawigacja po powiązaniach i status Spotify

Data: 10 listopada 2026. Planowana wersja obu profili: `0.1.0-alpha.489`. Status: źródła zmienione, wydanie i instalację zapisać po wykonaniu.

## Zachowanie użytkowe

- Prawa strzałka w listach TIDAL, Spotify oraz relacji odcinków otwiera listę „Powiązania”. Wiersze nadal podają nazwy albumów, wykonawców lub podcastów i natywną pozycję na liście. Enter otwiera, lewa strzałka i Escape wracają; Ctrl+C kopiuje nazwę, Ctrl+Shift+C dokładny link, jeśli istnieje. Skróty i dostępność nie zmieniają się między profilami.
- W Spotify `NVDA+End` na liście i w odtwarzaczu ma podawać bieżący utwór oraz tylko dostępne parametry. Migawka z innym `sessionId` nie zastępuje statusu Spotify; po zmianie sesji stan jest odpytywany od razu. Dla Web Playback SDK tekst opisuje deklarowaną jakość usługi, nie pomiar konkretnego pliku. Soloist nie podaje AMC rzeczywistego bitrate'u; pozostaje uczciwy opis niedostępnych parametrów.

## Korekta wcześniejszych ustaleń

Zgłoszenie użytkownika z 10 listopada zastępuje nazwę listy „Dokąd przejść” ustaloną w `HIST-REL-20261109-nazwy-i-linki.md`. Użytkownik potwierdził, że nazwy relacji są poprawne; zbędne było słowo „Dokąd”. W tym samym dniu zgłosił ponowny błędny odczyt `NVDA+End` w Spotify, z tekstem przypominającym tytuł podcastu. To zastępuje założenie z `A11Y-20261030-nvda-end-pasek-stanu.md`, że sama obecność natywnego paska wystarcza do odbioru.

## Pomiary i testy

- W działającym AMC Test odczyt bez zmiany fokusu znalazł natywny pasek `msctls_statusbar32`, rolę MSAA 23 oraz tekst „Komary, jakość Spotify Web: AAC 256 kb/s”. Był to pomiar jednego stanu, a nie dowód, że NVDA wypowiada go w każdej chwili. Użytkownik usłyszał również obcy tytuł, więc odbiór żywym NVDA pozostaje otwarty.
- Celowane testy list, statusu, Spotify i nawigacji: 147 zaliczonych. Dodatkowe testy sprawdzają odrzucenie migawki Podcastów przy aktywnym Spotify i zgodność statusu listy oraz odtwarzacza.
- Test katalogu tłumaczeń: 12 zaliczonych, 1 pominięty, 2 niezaliczone przez wcześniejsze braki tekstów Soloist, TIDAL i ustawień. Nowe klucze „Powiązania” oraz podpowiedź mają polskie i angielskie wpisy. Braki nie są skutkiem tej poprawki; osobne uzupełnienie jest potrzebne przed pełnym przejściem tej kontroli.
- Kompilacja, instalatory, GitHub, instalacja i żywy odczyt NVDA: do wpisania po sprawdzeniu. Nie utożsamiać zaliczonych testów z tymi etapami.

## Ograniczenia i odbiór

Nie ustalono jeszcze, dlaczego NVDA w zgłoszonej chwili wypowiedział tytuł z innego kontekstu, choć późniejszy odczyt MSAA paska Spotify był poprawny. Po aktualizacji sprawdzić `NVDA+End` od razu po wejściu do Spotify, po kilku sekundach, po rozpoczęciu utworu, po przejściu z Podcastów oraz na liście i w odtwarzaczu. AMC Test pozostaje uruchomiony; nie instalować go w trakcie nagrywania.