# Projekt modułu podcastów AMC

Status: zatwierdzony kierunek po ustabilizowaniu Radia internetowego. Dokument
opisuje model danych i interfejs; nie oznacza jeszcze gotowej implementacji.

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

1. RSS/Atom, Biblioteka, Nowe odcinki, Historia i wspólny odtwarzacz.
2. Pobieranie, Pobrane, trwała pozycja, prędkość i zakładki.
3. Ręczne playlisty oraz import i eksport OPML i danych AMC.
4. Adapter adresu strony korzystający ze sprawdzonych kontenerów konwertera.
5. Dalsze publiczne katalogi i usługi kontowe jako osobne adaptery.

Mapa skrótów zostanie ustalona po pierwszym działającym widoku. Nie należy
rezerwować klawiszy na podstawie samego dokumentu koncepcyjnego.

## 7. Rozdziały odcinków

Przyszłe rozdziały podcastów wykorzystają nazwaną Zakładkę jako stabilny punkt
czasu zamiast tworzyć drugi system znaczników. `Ctrl+Shift+B` nadal zapisuje
nazwany punkt, który może później zostać oznaczony jako początek rozdziału.
Pełny model, dostępny edytor, wykrywanie ciszy i sposoby eksportu opisuje
`PROJEKT_ROZDZIALOW_AUDIO_PL.md`.
