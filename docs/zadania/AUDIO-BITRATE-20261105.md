# AUDIO-BITRATE-20261105 — bitrate w odtwarzaczu i radiu

Status: kod, testy, paczka i instalacja AMC Test alpha.480 gotowe 6 listopada 2026; odbiór NVDA na rzeczywistych źródłach nadal potrzebny.

## Korekta użytkownika

5 listopada 2026 użytkownik usłyszał w odtwarzaczu TIDAL „bez ciebie, 48 kHz” bez przepływności i zgłosił podobny brak w radiu. Ten raport zastępuje wcześniejsze założenie, że sam odczyt częstotliwości dowodzi pełnego opisu parametrów audio. Wartość 48 kHz oznacza częstotliwość próbkowania; nie wolno jej podawać jako bitrate.

6 listopada 2026 użytkownik doprecyzował, że nie słyszał bitrate'u w żadnej sesji, w tym Spotify i Radio Archiwista. To zastępuje wcześniejszy zakres ograniczony do TIDAL i Radia. Odczyt bieżącego materiału i stanu przez dodatek NVDA również ma korzystać z pełnego opisu, a nie z samego tytułu.

## Zachowanie i miejsca

- TIDAL w AMC: po uruchomieniu utworu status odtwarzania używany przez NVDA+End podaje tytuł, dostępny bitrate w kb/s i częstotliwość w kHz. Dla zwykłego pliku FLAC proxy oblicza średni bitrate z całkowitej liczby bajtów i czasu utworu przy pierwszym normalnym żądaniu dźwięku. Nie wykonuje dodatkowego żądania przed startem. Dla Max/DASH bierze deklarowaną przepływność reprezentacji z manifestu, jeśli istnieje.
- Radio: gdy manifest HLS podaje kodek, ale nie podaje przepływności, pomiar nie kończy się na samym kodeku. Program może oszacować bitrate z rozmiaru i czasu pierwszego segmentu audio. Status radia i Alt+D używają tego samego formatu. Brak wiarygodnych danych nadal pozostaje brakiem danych, bez zmyślonej liczby.
- Odtwarzacz TIDAL zachowuje kompletny status hosta po odświeżeniu widoku; ponowne narysowanie nie zastępuje go samym tytułem.
- Pliki lokalne i nagrania: odtwarzacz szacuje średni bitrate z rozmiaru rzeczywistego pliku i czasu zgłoszonego przez dekoder; znane wartości trafiają do paska stanu i odczytu przez dodatek NVDA.
- Radio Archiwista i zdalne odcinki: czas znany z listy jest przekazywany wraz z poleceniem odtwarzania. Po rozpoczęciu odtwarzania host wykonuje jedną ograniczoną czasowo próbę odczytu rozmiaru pliku przez HEAD lub żądanie zakresu bez pobierania treści. Z rozmiaru i czasu oblicza średnie kb/s. Gdy serwer nie ujawni rozmiaru, pomija liczbę.
- Spotify Web Player: oficjalny odtwarzacz nie udostępnia pomiaru bitrate'u konkretnego utworu. Tekst mówi jawnie „jakość Spotify Web: AAC 256 kb/s” dla muzyki Premium oraz „około 128 kb/s” dla odcinków, zgodnie z deklaracją Spotify; nie przedstawia tego jako pomiaru pliku. Źródło: https://support.spotify.com/sg-en/article/audio-quality/ .
- WiiM: gdy urządzenie podaje bitrate bieżącego materiału, host i filtr dostępności przekazują go do odczytu „teraz odtwarzane” w kb/s. Sonos i Frontier Smart nie przekazują takiej wartości w bieżącym kontrakcie urządzenia; komunikat po prostu ją pomija.
- Most NVDA odczytuje gotowy pełny tekst hosta także w komendach stanu i bieżącego materiału dla TIDAL, Spotify, Radia, plików, podcastów i Archiwisty. Wcześniej część tych komend redukowała opis do samego tytułu.
- Nie ma nowych skrótów ani poleceń. Pomoc, menu i paleta zachowują istniejące nazwy odczytu bieżącego odtwarzania.

## Dlaczego

Wewnętrzny tor TIDAL znał częstotliwość z dekodera, lecz nie przekazywał do hosta rozmiaru skompresowanego źródła. Dodatkowo widok TIDAL mógł zastąpić gotowy status samym tytułem. W radiu kod przerywał pomiar HLS po znalezieniu samego kodeka. Poprawki dotyczą danych wejściowych i zachowania gotowego tekstu, a nie odgadywania kb/s z 48 kHz.

## Weryfikacja i ograniczenia

- Python: 59 testów celowanych i 6 podtestów: proxy BTS, Max/DASH, adapter, status NVDA i radio.
- C#: test protokołu TIDAL 1/1; test radia HLS z lokalnym serwerem przeszedł; sam host skompilowany bez ostrzeżeń i błędów.
- Rozszerzenie z 6 listopada: w izolowanym checkoutcie instalacyjnym przeszły 142 testy Python objętych modułów, w tym przekazanie czasu Archiwisty i odczyt NVDA; trzy zestawy C# (zdalny plik, WiiM i TIDAL) przeszły. Wyniki testów nie są dowodem zainstalowania rozszerzenia.
- Rzeczywiste konto TIDAL, stacje użytkownika i NVDA wymagają odsłuchu po instalacji. Przy strumieniu bez całkowitego rozmiaru, deklaracji przepływności ani mierzalnego segmentu liczba nie zostanie wypowiedziana. Wartości z manifestu i rozmiaru są średnie lub deklarowane, nie chwilową przepływnością każdej sekundy.

## Wersja i commit

Wspólny kandydat AMC Test 0.1.0-alpha.480. Wcześniejsze commity: 9a24c83c i 79532f3c. Rozszerzenie z 6 listopada: commit `b9f7ea86` oraz poprawka kompilacji `d42dfe6f` w izolowanym checkoutcie, bez równoległych niezapisanych zmian skrzynki podcastów. Publikacja: niewykonana.

## Budowa i instalacja 6 listopada

- Pierwszy build zatrzymał analizator wartości nullable w sondzie źródła; poprawiono i ponownie zbudowano hosta Release: 0 ostrzeżeń, 0 błędów. Dwie następne próby paczki wykryły zależności TIDAL już obecne w runtime z poprzedniej instalacji. Przygotowano osobną kopię runtime bez dwunastu katalogów zależności dodawanych przez kreator; nie zmieniano runtime działającej instalacji.
- Paczka testowa z commitu `d42dfe6f39d741cedb344a2da4dc1ad82a2d3143` zawiera 4196 plików, przeszła pełną kontrolę integralności oraz kontrolowany start interfejsu i hosta. Identyfikator paczki: `bd9f68b0`.
- Instalator: `D:\Projekty Codex\AMC - integracja wxPython\publish\installers\AMC-Test-2026-11-06-0046.exe`; 143829340 bajtów; SHA-256 `56a47ecb2e1f0aa29876edbdb74e7728d29d29c90a257072e580c6bea7b29b9a`. Kontrola sekretów i dołączonego Quick App Switchera 0.1.6 zaliczona.
- Przed podmianą lista aktywnych nagrań była pusta. Starą wersję zamknięto zwykłą drogą; launcher, GUI i host zniknęły. Instalator zakończył się kodem 0 i zweryfikował zainstalowany pakiet oraz próbny start. Zainstalowany launcher ma ProductVersion `0.1.0-alpha.480`, manifest wskazuje powyższy commit i `bd9f68b0`. Uruchomiono jeden launcher, jeden proces GUI i jeden host ze stałej instalacji; tytuł okna: `AMC Test — 06.11.2026, 00:46`.
- Dodatek NVDA był dołączony opcjonalnie, lecz nie został zainstalowany ani nie restartowano NVDA. Odsłuch rzeczywistego TIDAL, Radia, Spotify, Archiwisty i WiiM pozostaje do odbioru użytkownika.

## Korekta kolejności wydań — 6 listopada 2026

Po tej instalacji użytkownik wskazał, że przy dwóch lub trzech równoległych zadaniach aktualizację należy wstrzymać do zakończenia wszystkich i wykonać jedno wspólne wydanie. Ta decyzja zastępuje zastosowaną tu kolejność, w której alpha.480 zainstalowano bez równoległych, niezakończonych zmian skrzynki podcastów. Alpha.480 pozostaje faktem historycznym; następnej instalacji nie należy rozpoczynać przed ukończeniem i sprawdzeniem pozostałych zadań.
