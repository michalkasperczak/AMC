# Projekt modułu podcastów AMC

Status: wdrażanie rozpoczęte w `alpha.203`. Działa fundament sesji, trwały model
danych oraz bezpieczny parser RSS/Atom. Pobieranie kanałów z sieci i dodawanie
subskrypcji przez interfejs rozpoczyna następny etap.

## 1. Osobna sesja Podcasty

Podcasty będą osobną sesją korzystającą ze wspólnego odtwarzacza AMC. Zachowają
pozycję, prędkość, zakładki, historię, urządzenie audio i dostępne komunikaty,
ale nie będą udawały Radia ani Plików lokalnych. Każda operacja w menu, palecie
i menu kontekstowym ma być widoczna wyłącznie tam, gdzie ma znaczenie.

Nazwa sesji pozostaje krótka: **Podcasty**. Zakres wejściowy będzie jednak
szerszy i obejmie także audycje oraz pojedyncze materiały audio znalezione na
stronach. W interfejsie rodzaj elementu będzie jawny: podcast, audycja, odcinek
albo materiał ze strony. Dzięki temu rozszerzenie funkcji nie zmieni sesji w
nieczytelny zbiór wszystkich możliwych multimediów.

## 2. Kontenery użytkownika

- **Biblioteka** przechowuje obserwowane audycje i ich źródła. Dodanie podcastu
  nie oznacza automatycznego pobrania wszystkich odcinków.
- **Nowe odcinki** są automatyczną skrzynką odbiorczą. Zawierają nieodsłuchane
  lub jeszcze nieprzejrzane odcinki z Biblioteki, od najnowszego. To celowo nie
  jest playlista: użytkownik nie układa jej ręcznie, a odświeżenie kanału może
  bezpiecznie dopisać nowe pozycje. Odcinek może być nowy, rozpoczęty,
  odsłuchany albo świadomie pominięty.
- **Playlisty** są ręcznymi, trwałymi zestawami odcinków w kolejności
  użytkownika i mogą łączyć różne audycje.
- **Pobrane** pokazują pliki dostępne bez sieci. Usunięcie pobrania nie usuwa
  subskrypcji ani historii.
- **Historia** zaczyna się od ostatnio odtwarzanego odcinka. Delete usuwa wpis
  wyłącznie z historii.

## 3. Źródła, wyszukiwanie i import

AMC nie uzależnia Biblioteki od jednego katalogu. Wszystkie źródła przechodzą
przez ten sam, izolowany mechanizm rozpoznawania i kończą jako jeden
znormalizowany podcast albo odcinek. Kolejność prób jest następująca:

1. bezpośredni kanał RSS lub Atom z `enclosure`;
2. adres podcastu z katalogu Apple Podcasts, rozpoznany przez publiczne
   wyszukiwanie albo identyfikator katalogowy, a następnie sprowadzony do
   publicznego adresu kanału;
3. zwykła strona zawierająca wskazanie RSS/Atom;
4. zwykła strona z jednym lub wieloma osadzonymi plikami audio, manifestami
   albo metadanymi odtwarzacza;
5. kontrolowany adapter strony, gdy wydawca stosuje własny format, na początku
   między innymi dla serwisów Polskiego Radia;
6. jawnie uruchamiany adapter `yt-dlp` jako rozwiązanie ostatniej szansy dla
   obsługiwanych stron, nigdy jako warunek działania zwykłego RSS.

Wynikiem może być audycja, pojedynczy odcinek albo kilka znalezionych
materiałów. W przypadku niejednoznacznym program pokazuje dostępną listę i
niczego nie dodaje ani nie pobiera bez potwierdzenia. Pojedynczy materiał bez
kanału trafia do czytelnie nazwanej grupy **Materiały ze stron**, a nie do
fikcyjnej subskrypcji udającej prawdziwy podcast.

Kontenery oraz ekstraktory używane przez rozszerzenie Chrome i konwerter
multimediów zostaną wykorzystane jako wymienne adaptery wejściowe. Krucha
analiza konkretnej strony nie może trafić do rdzenia. Rdzeń otrzymuje wyłącznie
znormalizowany rekord: tytuł audycji, tytuł odcinka, autor, data, opis, czas,
adres strony, adres pliku lub manifestu oraz stabilną tożsamość.

Duplikaty są rozpoznawane kolejno przez GUID kanału, adres `enclosure`, adres
kanoniczny i kontrolowany skrót metadanych. Zmiana adresu CDN nie może sama
tworzyć drugiego odcinka, a dwa rzeczywiście różne odcinki o podobnym tytule
nie mogą zostać automatycznie scalone.

### Wykorzystanie istniejących projektów

- Z rozszerzenia Chrome wykorzystujemy wykrywanie `audio`, `video`, `source`,
  RSS/Atom, `enclosure`, Open Graph, manifestów HLS/DASH, zasobów odtwarzacza
  oraz zamianę strony Apple Podcasts na publiczny kanał.
- Z dodatku NVDA do konwersji wykorzystujemy ograniczony parser zwykłych stron,
  rozpoznawanie wielu plików, istniejące reguły Polskiego Radia, Radia Poznań,
  Radia Kraków i Radia Wrocław oraz kontrolowany fallback `yt-dlp`.
- Kod nie będzie kopiowany jako jedna duża zależność od NVDA lub Chrome.
  Wspólne reguły zostaną przeniesione do testowalnych adapterów .NET, a dodatki
  mogą później przekazywać AMC już rozpoznane adresy.
- Adapter wydawcy ma osobne testy z zapisanymi, pozbawionymi danych prywatnych
  próbkami. Awaria jednej strony nie może blokować pozostałych podcastów.

## 4. Odtwarzanie i pobieranie

Odcinek może być odtwarzany strumieniowo lub pobrany świadomym poleceniem.
Pozycja i prędkość są trwałe per odcinek; oznaczenie jako odsłuchany jest
oddzielnym stanem. Zakładki działają tak samo jak w trwałych mediach lokalnych.
Nieukończone pobranie pozostaje lokalnym plikiem roboczym poza chmurą, a do
folderu użytkownika trafia dopiero ukończony plik przez bezpieczną publikację
stosowaną przy nagraniach Radia.

`Ctrl+S` w sesji Podcasty będzie znaczyć **Pobierz odcinek** i zadziała także
na ciągłym zaznaczeniu wielu odcinków. Na nagłówku całej audycji nie uruchomi
bez ostrzeżenia pobierania całego archiwum; taka operacja będzie dostępna
wyłącznie jako jawne polecenie z zakresem i potwierdzeniem.

Kopiowanie rozróżnia adres dla człowieka i adres techniczny:

- `Ctrl+C` na odcinku kopiuje nazwę i publiczne łącze do jego strony, jeżeli
  istnieje; na wielu odcinkach tworzy powtarzalne pary nazwa–łącze;
- `Ctrl+Shift+C` kopiuje nazwę i bezpośredni adres pliku lub strumienia audio;
- na nagłówku audycji `Ctrl+C` kopiuje nazwę i publiczną stronę, a
  `Ctrl+Shift+C` nazwę i adres RSS/Atom;
- wygasający adres podpisany, adres wymagający ciasteczek albo zawierający
  poświadczenia nie jest bez ostrzeżenia umieszczany w schowku. Program kopiuje
  wtedy publiczną stronę albo informuje, dlaczego bezpośredni adres jest
  nietrwały.

## 5. Prywatność i odporność

Odświeżanie kanałów ma ograniczenia czasu, rozmiaru i liczby przekierowań.
HTML, RSS i metadane z sieci są danymi niezaufanymi. AMC nie uruchamia ich jako
poleceń, nie zapisuje tokenów w eksporcie i nie wysyła historii odsłuchu do
źródeł. Adapter wymagający konta przechowuje poświadczenia w systemowym
magazynie sekretów i pozostaje niezależny od adapterów publicznych.

Publiczny katalog Apple służy do wyszukania podcastu i jego kanału, a nie do
synchronizacji prywatnego konta Apple Podcasts. Wyniki są buforowane i
ograniczane, ponieważ publiczna usługa wyszukiwania ma limit zapytań. Pozostałe
katalogi będą opcjonalnymi adapterami; żaden z nich nie stanie się jedynym
indeksem potrzebnym do działania Biblioteki.

Pierwszy adapter katalogowy korzysta z udokumentowanych operacji Search i
Lookup z parametrami `media=podcast`, `entity=podcast` i `country=PL`.
Dokumentacja Apple podaje orientacyjny limit około 20 zapytań na minutę, dlatego
AMC nie wysyła zapytania po każdym znaku i przechowuje krótki cache wyników:
[iTunes Search API](https://developer.apple.com/library/archive/documentation/AudioVideo/Conceptual/iTuneSearchAPI/Searching.html).

## 6. Kolejność wdrożenia

1. `alpha.203` — prawdziwa, pusta sesja Podcasty bez danych demonstracyjnych,
   szóste miejsce w konfigurowanej kolejności sesji, trwały model subskrypcji i
   odcinków, widoki Biblioteka, Nowe odcinki i Pobrane oraz parser RSS/Atom.
2. `alpha.204` — **Dodaj podcast lub materiał z adresu** przez `Ctrl+O` w sesji
   Podcasty, ograniczony klient HTTP/HTTPS, bezpośredni RSS/Atom, aktualizacja
   pojedynczej audycji i całej Biblioteki, otwieranie podcastu do listy
   odcinków oraz wypisywanie się przez Delete. Kanał nie pobiera automatycznie
   plików audio.
3. `alpha.205` — odtwarzanie skończonych materiałów HTTP przez osobny tor
   Podcastów, zapamiętywanie pozycji i prędkości per odcinek, poprawne
   Page Up/Page Down w bieżącym kontenerze, Historia i zakładki.
4. `alpha.206` — wyszukiwanie publicznego katalogu Apple Podcasts, rozpoznanie
   stron Apple/Overcast i wykrywanie RSS/Atom na zwykłej stronie.
5. `alpha.207` — pełna skrzynka Nowe odcinki, stany nowy, przejrzany,
   odsłuchany i w trakcie, filtrowanie oraz operacje zbiorowe. Stan odsłuchania
   nie będzie utożsamiany z usunięciem odcinka.
6. `alpha.208` — jawne Pobierz/Usuń pobranie i `Ctrl+S`, kolejka pobierania,
   anulowanie, postęp i atomowa publikacja gotowego pliku. Części robocze pozostają poza
   iCloud, OneDrive, Dyskiem Google i innymi folderami synchronizowanymi.
7. `alpha.209` — playlisty odcinków, import i eksport OPML, osobny eksport
   danych Podcastów AMC i pełne odtworzenie ich z kopii zapasowej.
8. `alpha.210` — strony z osadzonym audio, istniejące adaptery Polskiego Radia
   i innych rozgłośni oraz przekazywanie wyników z rozszerzenia Chrome i
   dodatku NVDA.
9. `alpha.211` — rozdziały dostarczone przez podcast i rozdziały użytkownika
   oparte na nazwanych zakładkach.
10. Dalsze katalogi publiczne i usługi kontowe pozostają wymiennymi adapterami;
   nie mogą uzależnić od siebie RSS, Biblioteki ani lokalnych pobrań.

Mapa skrótów zostanie ustalona po pierwszym działającym widoku. Nie należy
rezerwować klawiszy na podstawie samego dokumentu koncepcyjnego.

## 7. Rozdziały odcinków

Podcasty nie otrzymują w tym celu osobnego odtwarzacza ani dużego modułu
edycyjnego. Dziedziczą niewielką funkcję rozdziałów wspólnego odtwarzacza AMC.
Rozdziały wykorzystają nazwaną Zakładkę jako stabilny punkt czasu zamiast
tworzyć drugi system znaczników. `Ctrl+Shift+B` nadal zapisuje nazwany punkt,
który może później zostać oznaczony jako początek rozdziału także w istniejącym
odcinku. Pełny model, dostępny edytor, wykrywanie ciszy i sposoby eksportu
opisuje `PROJEKT_ROZDZIALOW_AUDIO_PL.md`.

## 8. Zasady interfejsu

- `Ctrl+L` otwiera Bibliotekę obserwowanych audycji. Enter na audycji pokaże
  jej odcinki; nie spróbuje odtwarzać samego kanału.
- **Nowe odcinki** są widokiem automatycznym od najnowszego. **Pobrane** są
  filtrem rzeczywiście ukończonych plików lokalnych. Żaden z tych widoków nie
  zmienia ręcznie kolejności danych źródłowych.
- Historia, Ulubione, Kolejka, Playlisty, Presety, wyszukiwanie, kopiowanie i
  wspólny odtwarzacz zachowują ustaloną mechanikę AMC, ale działają na
  odcinkach, nie na nagłówku audycji.
- Menu i paleta pokazują wyłącznie czynności możliwe w Podcastach. Nie wolno
  przenosić tu nagrywania Radia, zarządzania folderami ani lokalnego cięcia
  pliku strumieniowanego.
- Skróty dla Nowych odcinków i Pobranych zostaną ustalone po teście pierwszej
  listy. `Alt+1`, `Alt+2` i `Alt+3` nie zostają bez sprawdzenia skopiowane z
  lokalnej Biblioteki ani Radia.

## 9. Granica pierwszej wersji

`alpha.203` pozwala wybrać sesję Podcasty i sprawdzić jej pustą Bibliotekę oraz
puste widoki Nowe odcinki i Pobrane. To celowa wersja fundamentu: nie przyjmuje
jeszcze adresu kanału i nie łączy się z siecią. Parser jest sprawdzany na RSS,
Atom, adresach względnych, metadanych iTunes, wpisach bez audio oraz złośliwym
DTD. Dzięki temu następna wersja dołącza sieć do gotowego i migrowalnego modelu,
zamiast zapisywać subskrypcje w prowizorycznej strukturze.
