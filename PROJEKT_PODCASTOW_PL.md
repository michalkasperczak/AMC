# Projekt modułu podcastów AMC

Status: wdrażanie rozpoczęte w `alpha.203`. Działa fundament sesji, trwały model
danych oraz bezpieczny parser RSS/Atom. Pobieranie kanałów z sieci i dodawanie
subskrypcji przez interfejs rozpoczyna następny etap.

## 1. Osobna sesja Podcasty

Podcasty będą osobną sesją korzystającą ze wspólnego odtwarzacza AMC. Zachowają
pozycję, prędkość, zakładki, historię, urządzenie audio i dostępne komunikaty,
ale nie będą udawały Radia ani Plików lokalnych. Każda operacja w menu, palecie
i menu kontekstowym ma być widoczna wyłącznie tam, gdzie ma znaczenie.

## 2. Kontenery użytkownika

- **Biblioteka** przechowuje obserwowane audycje i ich źródła. Dodanie podcastu
  nie oznacza automatycznego pobrania wszystkich odcinków.
- **Nowe odcinki** są automatyczną skrzynką odbiorczą. Zawierają nieodsłuchane
  lub jeszcze nieprzejrzane odcinki z Biblioteki, od najnowszego. To nie jest
  playlista i użytkownik nie układa jej ręcznie.
- **Playlisty** są ręcznymi, trwałymi zestawami odcinków w kolejności
  użytkownika i mogą łączyć różne audycje.
- **Pobrane** pokazują pliki dostępne bez sieci. Usunięcie pobrania nie usuwa
  subskrypcji ani historii.
- **Historia** zaczyna się od ostatnio odtwarzanego odcinka. Delete usuwa wpis
  wyłącznie z historii.

## 3. Źródła i import

Pierwszym źródłem jest RSS albo Atom z elementami podcastowymi. AMC przyjmuje
adres kanału lub strony i przekazuje go do izolowanego adaptera rozpoznawania.
Wynikiem może być kanał, pojedynczy odcinek albo kilka znalezionych materiałów
audio. W przypadku niejednoznacznym program pokazuje dostępny wybór i niczego
nie dodaje bez potwierdzenia.

Kontenery oraz ekstraktory używane przez rozszerzenie Chrome i konwerter
multimediów zostaną wykorzystane jako wymienne adaptery wejściowe. Krucha
analiza konkretnej strony nie może trafić do rdzenia. Rdzeń otrzymuje wyłącznie
znormalizowany rekord: tytuł audycji, tytuł odcinka, autor, data, opis, czas,
adres strony, adres pliku lub manifestu oraz stabilną tożsamość.

Duplikaty są rozpoznawane kolejno przez GUID kanału, adres `enclosure`, adres
kanoniczny i kontrolowany skrót metadanych. Zmiana adresu CDN nie może sama
tworzyć drugiego odcinka, a dwa rzeczywiście różne odcinki o podobnym tytule
nie mogą zostać automatycznie scalone.

## 4. Odtwarzanie i pobieranie

Odcinek może być odtwarzany strumieniowo lub pobrany świadomym poleceniem.
Pozycja i prędkość są trwałe per odcinek; oznaczenie jako odsłuchany jest
oddzielnym stanem. Zakładki działają tak samo jak w trwałych mediach lokalnych.
Nieukończone pobranie pozostaje lokalnym plikiem roboczym poza chmurą, a do
folderu użytkownika trafia dopiero ukończony plik przez bezpieczną publikację
stosowaną przy nagraniach Radia.

## 5. Prywatność i odporność

Odświeżanie kanałów ma ograniczenia czasu, rozmiaru i liczby przekierowań.
HTML, RSS i metadane z sieci są danymi niezaufanymi. AMC nie uruchamia ich jako
poleceń, nie zapisuje tokenów w eksporcie i nie wysyła historii odsłuchu do
źródeł. Adapter wymagający konta przechowuje poświadczenia w systemowym
magazynie sekretów i pozostaje niezależny od adapterów publicznych.

## 6. Kolejność wdrożenia

1. `alpha.203` — prawdziwa, pusta sesja Podcasty bez danych demonstracyjnych,
   szóste miejsce w konfigurowanej kolejności sesji, trwały model subskrypcji i
   odcinków, widoki Biblioteka, Nowe odcinki i Pobrane oraz parser RSS/Atom.
2. `alpha.204` — **Dodaj podcast z adresu** przez `Ctrl+O` w sesji Podcasty,
   ograniczony klient HTTPS, aktualizacja pojedynczej audycji i całej
   Biblioteki, otwieranie podcastu do listy odcinków oraz wypisywanie się przez
   Delete. Kanał nie pobiera automatycznie plików audio.
3. `alpha.205` — odtwarzanie skończonych materiałów HTTP przez osobny tor
   Podcastów, zapamiętywanie pozycji i prędkości per odcinek, poprawne
   Page Up/Page Down w bieżącym kontenerze, Historia i zakładki.
4. `alpha.206` — pełna skrzynka Nowe odcinki, stany nowy, przejrzany,
   odsłuchany i w trakcie, filtrowanie oraz operacje zbiorowe. Stan odsłuchania
   nie będzie utożsamiany z usunięciem odcinka.
5. `alpha.207` — jawne Pobierz/Usuń pobranie, kolejka pobierania, anulowanie,
   postęp i atomowa publikacja gotowego pliku. Części robocze pozostają poza
   iCloud, OneDrive, Dyskiem Google i innymi folderami synchronizowanymi.
6. `alpha.208` — playlisty odcinków, import i eksport OPML, osobny eksport
   danych Podcastów AMC i pełne odtworzenie ich z kopii zapasowej.
7. `alpha.209` — rozdziały dostarczone przez podcast i rozdziały użytkownika
   oparte na nazwanych zakładkach oraz adapter odkrywania kanału z adresu
   zwykłej strony.
8. Dalsze katalogi publiczne i usługi kontowe pozostają wymiennymi adapterami;
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
