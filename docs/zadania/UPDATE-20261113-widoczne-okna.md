# UPDATE-20261113 — widoczny wynik aktualizacji AMC

Data: 13.11.2026. Produkt: AMC Test wxPython. Stan: źródła i testy; brak
nowego buildu, instalacji i wydania w chwili zapisu.

## Zgłoszenie i korekta

Użytkownik ręcznie aktualizował AMC podczas wcześniejszego nagrywania.
Okno instalacji lub wyniku mogło być ukryte, a ponowne „Sprawdź aktualizację
AMC” nie dało widocznego rezultatu. Potwierdził zakończenie nagrywania.
Istniejący aktualizator dokończył instalację alpha.500: manifest zainstalowany
pod `D:\Projekty Codex\AMC Python` wskazuje wersję `13.11.2026, 19:52`
i źródło `16eb05b5`; działa nowe okno tej wersji. To nie potwierdza
odtwarzania Apple Music.

Decyzja z 13.11 zastępuje wcześniejszy opis wyłącznie mówionego błędu w
`UPDATE-20261101-ponowna-aktualizacja.md`. Ręczny wynik i błąd mają
pozostać widoczne do zamknięcia przez użytkownika, także gdy otwarte jest
inne okno AMC.

## Zachowanie użytkowe

- `F11`, menu Pomoc „Sprawdź aktualizację AMC” i paleta poleceń prowadzą do
  wspólnej akcji. Ręczna kontrola pokazuje okno wyniku na pierwszym planie.
  Powtórzenie podczas automatycznej kontroli przejmuje jej wynik i mówi:
  „Kończę sprawdzanie aktualizacji. Wynik pokażę na pierwszym planie”.
- Podczas pobierania lub instalacji kolejna kontrola nie uruchamia drugiej
  operacji; NVDA mówi: „Pobieranie lub instalacja aktualizacji już trwa”.
- Błąd kontroli, pobierania lub instalacji jest zarówno ogłaszany przez NVDA,
  jak i pokazany w modalnym oknie „Aktualizacja AMC”. Okno ma priorytet
  pierwszego planu; po zamknięciu fokus wraca do AMC.
- Automatyczna kontrola nadal czeka, aż inne okna AMC zostaną zamknięte,
  aby nie przerywać pracy użytkownika. Ręczne wywołanie ma pierwszeństwo.
- Nagrywanie i okno zaplanowanego nagrania nadal blokują instalację.

## Dowody i ograniczenia

`tests/test_application_updates.py` w przypiętym środowisku Python 3.14 /
wxPython 4.3.1: 23 testy zaliczone. Wspólny przebieg po zmianach Apple Music:
68 testów i 3 podtesty zaliczone. Zweryfikowano istnienie flagi
`wx.STAY_ON_TOP` i klasy `wx.MessageDialog` w rzeczywistym wxPython.
Ręczny odbiór NVDA, układ okna na pulpicie, build, instalacja i wydanie
alpha.501 pozostają do potwierdzenia. Nie uruchamiano drugiego instalatora
podczas pracy AMC.

## Wersja i commit

Kandydat źródłowy `0.1.0-alpha.501`; commit i identyfikator wydania należy
uzupełnić po zapisie i publikacji. Poprzednie wydanie alpha.500 jest
zainstalowane i uruchomione.

## Korekta 13.11.2026: odtwarzanie a nagrywanie i wznowienie po aktualizacji

Wcześniejszy wniosek asystenta, że działający FFmpeg oznacza aktywne
nagrywanie, był błędny. Proces potomny hosta miał wyjście PCM do potoku i
nie miał ścieżki zapisu nagrania; służył do odtwarzania. Blokadę aktualizacji
wyznacza odpowiedź `radio.recordingStatus` właściciela nagrywania, a nie sama
obecność FFmpeg. Użytkownik potwierdził brak nagrywania i upoważnił do
instalacji bieżącego kandydata.

Przed zamknięciem nowy AMC Test zapisuje tylko potwierdzone, grające źródła
Radia, Plików, Podcastów, Radioarchiwisty i natywnego TIDAL. Launcher przekazuje
znacznik wyłącznie po uruchomieniu przez aktualizator; wtedy AMC zużywa
migawkę raz i wznawia źródło, głośność, tempo, wyciszenie oraz pozycję lokalnych
materiałów i TIDAL. Dla radia wznowienie zaczyna się od bieżącej transmisji;
bufor Time Shift nie jest przenoszony. Dla TIDAL zapisuje się identyfikator
utworu, nigdy tymczasowy adres proxy. Zwykły start aplikacji nie uruchamia
samoczynnie dźwięku. Pozostałe usługi nie mają tu lokalnego toru, który można
bezpiecznie odtworzyć z tej migawki. Istniejąca wersja alpha.500 nie tworzy
jeszcze takiej migawki; pierwsza instalacja wymaga osobnego rozpoznania
obecnie grającego źródła.

Testy ukierunkowane w Pythonie 3.14 i wxPython 4.3.1: 94 zaliczone, 0
nieudanych. Test obejmuje atomowy zapis/odczyt migawki, odrzucenie
niegrających i błędnych danych, dwa równoczesne tory, jednorazowe wznowienie
Radia i natywnego TIDAL. Build, instalację, wydanie i odsłuch należy zapisać
osobno po ich faktycznym wykonaniu.

## Korekta 13.11.2026: tytuł okna blokował aktualizator

Kandydat alpha.501 (`4e4f71cc`) przeszedł build i został opublikowany jako
`test-2026.11.13.2155`, ale nie został zainstalowany. Aktualizator trzykrotnie
odmówił z kodem 3, a stary AMC alpha.500 nadal grał. Odczyt błędu wykazał
procesy starej instalacji, mimo że aktualizator poprosił okno o zamknięcie.
Przyczyną jest porządek tytułu okna wybrany przez użytkownika: „Radio Poznań
— AMC Test — 13.11.2026, 19:52”. Kontrakt rozpoznawał tylko tytuły
zaczynające się od „AMC Test”, zatem nie wysłał WM_CLOSE do właściwego okna.

Decyzję zastępuje alpha.502: kontrakt rozpoznaje nazwę produktu również po
tytule odtwarzanego materiału, przy zachowaniu sprawdzenia, że okno należy do
procesu z dokładnie tej instalacji. Testy launchera sprawdzają ten przypadek
dla wykrywania instancji i aktualizatora. Przed instalacją nowej wersji trzeba
ponownie zbudować paczkę oraz instalator; publikacja alpha.501 nie dowodzi
lokalnej instalacji.
