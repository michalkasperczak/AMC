# Projekt modułu podcastów AMC

Status: etap subskrypcji i pierwszego odtwarzania jest ukończony. `alpha.211`
oczyszcza użytkową prezentację autora z początkowych oznaczeń praw autorskich
i zrównuje Escape z Backspace na zagnieżdżonych listach.
Działa trwała sesja, bezpieczne dodawanie bezpośrednich kanałów RSS/Atom,
dostępny import OPML, odświeżanie metadanych, przejście z audycji do jej
odcinków oraz odtwarzanie skończonych materiałów HTTP/HTTPS we wspólnym
odtwarzaczu AMC.

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
- **Nowe odcinki** są automatyczną skrzynką odbiorczą. Zawierają wyłącznie
  odcinki naprawdę nowe, których odtwarzanie nie osiągnęło jeszcze jednej
  minuty. To celowo nie
  jest playlista: użytkownik nie układa jej ręcznie, a odświeżenie kanału może
  bezpiecznie dopisać nowe pozycje. Odcinek może być nowy, rozpoczęty,
  odsłuchany albo świadomie pominięty.
- **W trakcie słuchania** zawiera rozpoczęte, lecz nieukończone odcinki. Dzięki
  temu materiał zatrzymany w połowie nie pozostaje bez końca oznaczony jako
  nowy. Widok otwiera `Ctrl+Shift+I`.
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
Pozycja jest trwała osobno dla każdego odcinka, a prędkość jest trwałym
ustawieniem całej sesji Podcasty; oznaczenie jako odsłuchany jest oddzielnym
stanem. Zakładki działają tak samo jak w trwałych mediach lokalnych.
Nieukończone pobranie pozostaje lokalnym plikiem roboczym poza chmurą, a do
folderu użytkownika trafia dopiero ukończony plik przez bezpieczną publikację
stosowaną przy nagraniach Radia.

`Ctrl+D` w sesji Podcasty będzie znaczyć **Pobierz odcinek** i zadziała także
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
2. `alpha.205` — zrealizowane: **Nowy podcast…** przez `Ctrl+N`, ograniczony klient
   HTTP/HTTPS, bezpośredni RSS/Atom, aktualizacja pojedynczej audycji i całej
   Biblioteki, otwieranie podcastu do listy odcinków oraz wypisywanie się przez
   Delete. `Ctrl+O` importuje lokalny plik OPML. Kanał nie pobiera automatycznie
   plików audio.
3. `alpha.206` — zrealizowane: odtwarzanie skończonych materiałów HTTP/HTTPS
   przez osobny tor Podcastów, zapamiętywanie pozycji per odcinek i prędkości
   sesji,
   Page Up/Page Down w bieżącym kontenerze, Historia i zakładki. Import OPML
   zaczyna od wszystkich kanałów; strzałki nie zmieniają wyboru, Spacja
   przełącza jeden kanał, a Ctrl+A zaznacza wszystkie.
4. `alpha.207` — zrealizowane: naprawa formatu próbek sieciowego dekodera;
   rzeczywiste odcinki MP3 i MP4 przechodzą przez regulację prędkości i wspólny
   tor wyjścia audio.
5. `alpha.208` — zrealizowane: bezpieczne przewijanie sieciowych odcinków,
   zachowanie bieżącego odcinka w Kolejce do zakończenia lub przejścia dalej,
   tytuł przed autorem na listach oraz trwała nazwa podcastu pod `F2`.
6. `alpha.209` — zrealizowane poza modułem Podcastów: bezpośredni skrót
   cichego automatycznego rozpoznawania Radia; plan Podcastów pozostaje bez
   zmian funkcjonalnych.
7. `alpha.210` — zrealizowane: poprawka trwałej prędkości sesji Podcasty po
   wyjściu Escape i ponownym otwarciu odcinka. Wyszukiwanie publicznego katalogu
   Apple Podcasts przechodzi do kolejnego etapu.
8. `alpha.211` — zrealizowane: zwięzła prezentacja autora bez początkowych
   oznaczeń `℗`, `©`, `®` i `™` oraz wspólne przejście o poziom wyżej przez
   Escape i Backspace na listach zagnieżdżonych.
9. `alpha.217` — zrealizowane: wyszukiwanie publicznego katalogu Apple
   Podcasts, dodanie wyniku przez zweryfikowany RSS/Atom, pełny opis pod
   `Alt+D` i przeładowanie danych przed pokazaniem skrzynki `Ctrl+I`.
   Rozpoznanie stron Overcast i wykrywanie RSS/Atom na dowolnej zwykłej stronie
   pozostaje następnym etapem.
10. `alpha.214` — zrealizowane: odcinek pozostaje nowy najwyżej do osiągnięcia
    pierwszej minuty, następnie przechodzi do trwałego stanu **w trakcie**, a po
    zakończeniu do stanu **odtworzony**. `Ctrl+Shift+I` otwiera osobny widok
    rozpoczętych odcinków. Starsze archiwum zaimportowane wraz z nową audycją
    nie jest przez to fałszywie oznaczane jako rozpoczęte. Lewa strzałka podaje
    również kodek, oszacowany bitrate i rozmiar, jeżeli RSS, serwer albo pobrany
    plik udostępnia potrzebne dane.
11. `alpha.218–219` — zrealizowane: dokładny powrót `Ctrl+L` do ostatnio
    otwartej audycji i odcinka oraz polecenie **Przejdź do podcastu** z odcinka
    pokazanego w skrzynce, Ulubionych, Kolejce, Historii, playliście,
    wyszukiwaniu albo odtwarzaczu. Polecenie ustawia fokus na źródłowym odcinku
    i nie jest pokazywane poza sesją Podcasty ani na samym nagłówku audycji.
12. Jawne Pobierz/Usuń pobranie i `Ctrl+D`, kolejka pobierania,
   anulowanie, postęp i atomowa publikacja gotowego pliku. Części robocze
   pozostają poza iCloud, OneDrive, Dyskiem Google i innymi folderami
   synchronizowanymi.
13. Playlisty odcinków, import i eksport OPML, osobny eksport
   danych Podcastów AMC i pełne odtworzenie ich z kopii zapasowej.
14. Strony z osadzonym audio, istniejące adaptery Polskiego Radia
   i innych rozgłośni oraz przekazywanie wyników z rozszerzenia Chrome i
   dodatku NVDA.
15. Rozdziały dostarczone przez podcast i rozdziały użytkownika
   oparte na nazwanych zakładkach.
16. Dalsze katalogi publiczne i usługi kontowe pozostają wymiennymi adapterami;
   nie mogą uzależnić od siebie RSS, Biblioteki ani lokalnych pobrań.

Pozostała mapa skrótów zostanie ustalona po pierwszym działającym widoku. Nie
należy rezerwować dalszych klawiszy na podstawie samego dokumentu
koncepcyjnego.

## 7. Dodawanie, import i wyszukiwanie

- `Ctrl+N` otwiera **Nowy podcast**. W `alpha.205` formularz przyjmuje
  bezpośredni adres kanału RSS/Atom i opcjonalną nazwę użytkownika. Przycisk
  **Sprawdź** pobiera wyłącznie ograniczone metadane kanału; dopiero aktywny po
  pomyślnej weryfikacji przycisk **Dodaj** zapisuje podcast w Bibliotece.
- `Ctrl+O` otwiera plik OPML i pokazuje listę znalezionych kanałów z
  niezależnymi polami wyboru. Strzałki przesuwają fokus bez zmiany wyboru,
  Spacja zaznacza lub odznacza bieżący kanał, a `Ctrl+A` zaznacza wszystkie.
  Import
  odświeża metadane maksymalnie czterech kanałów równocześnie, nie pobiera
  odcinków audio i nie tworzy duplikatów.
- `F5` odświeża zaznaczoną lub otwartą audycję, a `Ctrl+F5` wszystkie
  obserwowane audycje. Połączenia mają ograniczenie czasu, liczby przekierowań
  i rozmiaru odpowiedzi; DTD, encje zewnętrzne, adresy inne niż HTTP/HTTPS oraz
  adresy z danymi logowania są odrzucane.
- Enter na audycji otwiera jej odcinki od najnowszego, a Backspace wraca do
  Biblioteki. Delete na audycji wypisuje z niej bez kasowania zapisanych danych
  odcinków; ponowne dodanie przywraca subskrypcję. `F2` ustawia nazwę własną
  podcastu w AMC; nie zmienia źródłowego kanału i nie jest nadpisywane przez
  późniejsze odświeżenia RSS.
- `Ctrl+C` kopiuje nazwę i publiczną stronę podcastu lub odcinka, jeśli kanał
  ją podaje, natomiast
  `Ctrl+Shift+C` nazwę i bezpośredni adres kanału albo pliku audio. Menu
  kontekstowe wypowiada te różnice wprost.
- **Pokaż plik w folderze** jest dostępne tylko dla rzeczywiście pobranego,
  lokalnego odcinka i zaznacza go w Eksploratorze Windows. Zdalny odcinek ma
  zamiast tego **Otwórz stronę odcinka**, a nagłówek audycji — **Otwórz stronę
  podcastu**. Gdy kanał nie podaje strony publicznej, AMC nie przedstawia
  adresu RSS ani bezpośredniego pliku audio jako strony internetowej. AMC nie
  zgaduje już, która „domyślna aplikacja” miałaby otworzyć odcinek.
- `Ctrl+F` otwiera wyszukiwanie w Podcastach. Pierwszy poziom wyników zawiera
  audycje, a nie pomieszane odcinki ze wszystkich kanałów. Enter na audycji
  otwiera podgląd jej odcinków bez automatycznego dodawania subskrypcji;
  Backspace wraca do listy audycji, a Escape zamyka wyszukiwanie.
- `Ctrl+Shift+L` na wyniku audycji dodaje ją do Biblioteki albo z niej usuwa.
  Na poziomie odcinków obowiązują wspólne działania AMC: Enter, `Ctrl+Enter`,
  Kolejka, Odtwórz jako następne, Ulubione, Playlisty, `Ctrl+D`, kopiowanie i
  Właściwości. Wynik pozostaje dostępny po działaniu, zgodnie z dotychczasową
  mechaniką wyszukiwania.
- `Ctrl+K` nigdy nie odpytuje internetu: filtruje tylko już załadowaną listę.

## 8. Rozdziały odcinków

Podcasty nie otrzymują w tym celu osobnego odtwarzacza ani dużego modułu
edycyjnego. Dziedziczą niewielką funkcję rozdziałów wspólnego odtwarzacza AMC.
Rozdziały wykorzystają nazwaną Zakładkę jako stabilny punkt czasu zamiast
tworzyć drugi system znaczników. `Ctrl+Shift+B` nadal zapisuje nazwany punkt,
który może później zostać oznaczony jako początek rozdziału także w istniejącym
odcinku. Pełny model, dostępny edytor, wykrywanie ciszy i sposoby eksportu
opisuje `PROJEKT_ROZDZIALOW_AUDIO_PL.md`.

## 9. Zasady interfejsu

- `Ctrl+L` otwiera Bibliotekę obserwowanych audycji. Enter na audycji pokaże
  jej odcinki; nie spróbuje odtwarzać samego kanału.
- Biblioteka pamięta swój ostatni poziom niezależnie od Kolejki, Ulubionych i
  innych widoków. Jeżeli użytkownik opuści otwartą audycję z fokusem na
  odcinku, `Ctrl+L` wraca do tej audycji i odcinka. Dopiero Backspace lub
  Escape świadomie ustawia nadrzędną listę podcastów jako miejsce kolejnego
  powrotu. Zasada i fokus przetrwają ponowne uruchomienie; brakująca albo
  usunięta audycja powoduje bezpieczny powrót na poziom nadrzędny.
- `Ctrl+I` otwiera skrzynkę **Nowe odcinki**. Litera pochodzi od powszechnego
  określenia Inbox; wcześniejsze polecenie informacji spod `Ctrl+I` zostało w
  AMC zastąpione przez `Alt+Enter`, więc skrót nie ma konfliktu. Polecenie nie
  pobiera samo sieciowych aktualizacji; `F5` świadomie odświeża wszystkie
  obserwowane kanały. Przy migracji starszego importu do skrzynki trafia
  wyłącznie najnowszy nierozpoczęty odcinek audycji, która nie ma jeszcze
  żadnego nowego wpisu — nigdy całe archiwum.
- Escape z odtwarzacza wraca na odcinek, z którego rozpoczęto odtwarzanie,
  jeżeli nadal znajduje się on w skrzynce. Gdy po odsłuchaniu przestał być
  nowy i zniknął z automatycznego widoku, fokus pozostaje w jego dawnej
  pozycji na najbliższym odcinku; nie przeskakuje bez powodu na początek.
- `Ctrl+Shift+I` otwiera **W trakcie słuchania**. Jest to osobna lista
  niedokończonych odcinków, a nie rozszerzenie znaczenia słowa „nowe”.
- `Alt+D` otwiera pełny opis audycji albo odcinka jako tekst tylko do odczytu.
  Tekst pozwala na nawigację znakami i słowami, zaznaczanie i kopiowanie, a
  zachowane adresy są aktywnymi łączami. Opis nie jest czytany automatycznie
  przy przechodzeniu strzałkami po liście.
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
  listy, z wyjątkiem przyjętego `Ctrl+I` dla skrzynki. `Alt+1`, `Alt+2` i
  `Alt+3` nie zostają bez sprawdzenia skopiowane z lokalnej Biblioteki ani
  Radia.

Skrzynka ma własne, dostępne ustawienia, ale nie ręczną kolejność playlisty:

- sortowanie od najnowszych, od najstarszych albo grupami według audycji;
- pokazywanie nowych i rozpoczętych oraz opcjonalne pozostawianie odsłuchanych;
- próg uznania odcinka za odsłuchany;
- zachowanie przy pierwszym dodaniu audycji. Domyślnie stare archiwum jest
  widoczne wewnątrz audycji, ale nie zalewa skrzynki jako rzekomo nowe;
- częstotliwość automatycznego odświeżania oraz możliwość wyłączenia wybranej
  audycji ze skrzynki;
- liczba odcinków i łączny znany czas w nagłówku, tak jak dla playlisty. Czas
  częściowy musi być wyraźnie oznaczony, jeśli nie wszystkie odcinki podają
  długość.

Domyślne zasady widoków są następujące:

- Biblioteka pokazuje audycje alfabetycznie. Planowane warianty to: ostatnio
  dodane, ostatnio zaktualizowane, najczęściej słuchane i kolejność własna;
- skrzynka pokazuje odcinki od najnowszego. Planowane filtry obejmują:
  wszystkie nowe i rozpoczęte, tylko nowe, tylko rozpoczęte, wybraną audycję,
  zakres daty, czas trwania i stan pobrania;
- pozycja krótsza niż minuta pozostawia odcinek jako nowy. Po osiągnięciu jednej
  minuty odcinek znika z Nowych i trafia do W trakcie słuchania. Ukończenie
  przenosi jego stan do odtworzonych, bez usuwania go z Biblioteki ani Historii;
- `F5` w skrzynce odświeża wszystkie obserwowane audycje, ponieważ skrzynka
  łączy wiele kanałów. W otwartej audycji `F5` odświeża tylko ją, a `Ctrl+F5`
  wszędzie w Podcastach odświeża całą Bibliotekę;
- zwykłe playlisty są ręcznymi, trwałymi zestawami i nie odświeżają kanałów.
  Przyszła playlista inteligentna będzie osobnym zapisanym filtrem, który
  aktualizuje swoją zawartość automatycznie i nie pozwala na ręczne
  przestawianie wyników;
- ustawienie sortowania jest pamiętane osobno dla Biblioteki, skrzynki i każdej
  playlisty. Tymczasowy `Ctrl+K` nie zmienia zapisanej reguły sortowania.

W Podcastach `Ctrl+S` zostaje przeznaczony na świadome zapisanie audio jednego
lub wielu zaznaczonych odcinków. Nie działa na nagłówku całej audycji bez
osobnego wyboru zakresu i potwierdzenia. Pobieranie ma używać lokalnego pliku
roboczego, anulowania, postępu i atomowej publikacji, tak samo bezpiecznie jak
nagrania Radia.

## 10. Granica pierwszej wersji

`alpha.206` pozwala dodać bezpośredni kanał RSS/Atom, zaimportować OPML,
odświeżyć pojedynczą audycję albo całą Bibliotekę, wejść do listy odcinków i
odtworzyć skończony materiał HTTP/HTTPS.
Pierwsze pobranie zachowuje starsze archiwum wewnątrz audycji, ale nie oznacza
go całego jako nowe. Dopiero odcinki odnalezione podczas późniejszego
odświeżenia trafiają do podstawowej skrzynki **Nowe odcinki**. Odświeżanie
nigdy nie pobiera zawartości plików audio. Jawne pobieranie do widoku
**Pobrane**, katalog Apple i wydobywanie audio ze zwykłych stron pozostają
kolejnymi etapami; ich brak nie oznacza błędu odtwarzania `alpha.206`.
