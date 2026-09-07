# Projekt modułu podcastów AMC

Status: etap subskrypcji, odtwarzania i jawnego pobierania jest ukończony.
`alpha.229` rozdziela szybkie pobieranie `Ctrl+D` od interaktywnego
`Ctrl+S`, udostępnia trwały domyślny folder i zasila widok **Pobrane**.
Działa trwała sesja, bezpieczne dodawanie bezpośrednich kanałów RSS/Atom,
dostępny import OPML, odświeżanie metadanych, przejście z audycji do jej
odcinków oraz odtwarzanie skończonych materiałów HTTP/HTTPS we wspólnym
odtwarzaczu AMC.

Od `alpha.294` `Ctrl+N` przyjmuje także publiczny adres filmu albo transmisji
YouTube. Taki wpis nie udaje kanału RSS: trafia do jawnej kolekcji **Media
internetowe** z trwałym adresem strony. AMC nie loguje się do konta, nie czyta
cookies i nie synchronizuje biblioteki YouTube. Tymczasowy adres audio jest
wyznaczany dopiero w tle przy każdym odtwarzaniu i nie trafia do ustawień,
SQLite, eksportu ani dziennika. Skończony materiał można przewijać, przyspieszać
i świadomie zapisać jako MP3 przez `Ctrl+D` lub `Ctrl+S`.

Od `alpha.252` odcinki korzystają również z rozdziałów dostawcy. Obsługiwane są
Podcasting 2.0 JSON Chapters, Podlove Simple Chapters, czytelne znaczniki czasu
w opisie oraz osadzone rozdziały ID3/MP4 pobranego pliku. Sieciowy plik
rozdziałów przez HTTPS jest pobierany dopiero po wywołaniu `Ctrl+Alt+B`; zwykłe odświeżanie
Podcastów nie wysyła dodatkowych żądań do serwerów rozdziałów.
Sama litera `C` pozostaje na listach częścią szybkiej nawigacji po nazwach i nie
otwiera rozdziałów. Była próbnie przechwytywana w odtwarzaczu w `alpha.244`,
lecz po regresji fokusa NVDA została wycofana; bezpiecznym, jednoznacznym
poleceniem listy rozdziałów jest `Ctrl+Alt+B` zarówno na odcinku, jak i w
odtwarzaczu.

Od `alpha.247` każdy podcast ma opcjonalne ustawienia dziedziczone przez jego
odcinki: pozycję wznowienia, prędkość, przetwarzanie dźwięku, częstotliwość
automatycznego odświeżania i folder pobierania. Pojedynczy odcinek może
nadpisać ustawienia odtwarzania, ale nie częstotliwość kanału. `Ctrl+D` wybiera
folder w kolejności: własny folder podcastu, ogólny folder Podcastów, domyślny
folder systemowy. `Ctrl+S` nadal zawsze pyta o nazwę i miejsce jednego pliku.
Radio może wskazać ogólny folder Podcastów, lecz domyślnie zachowuje oddzielny
folder nagrań.

Od `alpha.306` przynależność do Biblioteki jest cechą subskrypcji, a nie
pojedynczego odcinka. `Ctrl+Shift+L` na nazwie podcastu oraz na dowolnym jego
odcinku wskazuje ten sam podcast nadrzędny. Usunięcie podczas przeglądania
odcinków wraca do nadrzędnej listy Podcastów; nie zostawia odłączonego widoku
ani pozornego stanu odcinka. Wielokrotne zaznaczenie odcinków tej samej audycji
zmienia subskrypcję tylko raz. Kolejka, Ulubione odcinków, historia, pobrania i
pozycje wznowienia pozostają oddzielnymi danymi i nie są fizycznie kasowane.

Ta sama wersja serializuje odświeżanie całej Biblioteki Podcastów. Automatyczna
i ręczna operacja nie mogą przebudowywać jej równocześnie. Jeżeli pobieranie
metadanych kończy się podczas używania odtwarzacza albo innej sesji, ciężka
podmiana listy odcinków jest odraczana do powrotu na widoczną listę Podcastów;
nie może odbierać fokusa przyciskowi odtwarzacza.

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
  nie oznacza automatycznego pobrania wszystkich odcinków. Jest też trwałym
  katalogiem metadanych wszystkich odcinków, które AMC już zobaczył. Kolejne
  odświeżenie scala bieżący RSS lub Atom z tym katalogiem: dodaje nowe pozycje
  i aktualizuje ponownie napotkane, ale nie usuwa starszych tylko dlatego, że
  wydawca skrócił kanał. Zachowanie nie pobiera dźwięku i nie potrafi odzyskać
  odcinka, którego żadne użyte źródło nigdy nie udostępniło.
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

`Ctrl+D` w sesji Podcasty znaczy **Pobierz odcinek** i działa także
na ciągłym zaznaczeniu wielu odcinków. Zapisuje bez dalszych pytań do
domyślnego folderu z **Ustawienia > Podcasty**, zapamiętuje lokalizację przy
odcinku i udostępnia go w widoku **Pobrane**. Gdy nazwa już istnieje, program
wybiera kolejno wariant „(2)”, „(3)” i nigdy po cichu nie nadpisuje pliku.
Na nagłówku całej audycji nie uruchomi
bez ostrzeżenia pobierania całego archiwum; taka operacja będzie dostępna
wyłącznie jako jawne polecenie z zakresem i potwierdzeniem.

`Ctrl+S` znaczy **Zapisz odcinek jako**. Działa wyłącznie dla jednego odcinka,
otwiera standardowe okno wyboru nazwy i folderu oraz nie zmienia domyślnego
folderu ani rekordu widoku **Pobrane**. Jest przeznaczone do jednorazowego
eksportu w wybrane miejsce.

Kopiowanie rozróżnia adres dla człowieka i adres techniczny:

- `Ctrl+C` na odcinku kopiuje czytelny blok: nazwę, pełny opis i publiczne
  łącze do jego strony, jeżeli istnieje; wiele zaznaczonych odcinków daje
  osobne bloki rozdzielone pustym wierszem;
- `Ctrl+Shift+C` kopiuje wyłącznie bezpośredni adres pliku lub strumienia
  audio, po jednym adresie w wierszu i bez powtarzania nazwy;
- na nagłówku audycji `Ctrl+C` kopiuje nazwę, opis i publiczną stronę, a
  `Ctrl+Shift+C` wyłącznie adres RSS/Atom;
- `Ctrl+Shift+C` jest jawną prośbą o adres techniczny, dlatego może skopiować
  także podpisany adres, który po czasie wygaśnie. Adres zawierający jawne dane
  logowania nie jest kopiowany; tokeny kont przyszłych adapterów nie mogą
  trafiać ani do schowka, ani do eksportu.

## 5. Prywatność i odporność

Odświeżanie kanałów ma ograniczenia czasu, rozmiaru i liczby przekierowań.
HTML, RSS i metadane z sieci są danymi niezaufanymi. AMC nie uruchamia ich jako
poleceń, nie zapisuje tokenów w eksporcie i nie wysyła historii odsłuchu do
źródeł. Adapter wymagający konta przechowuje poświadczenia w systemowym
magazynie sekretów i pozostaje niezależny od adapterów publicznych.

Publiczne katalogi Apple i Spreaker służą do wyszukania podcastu i jego kanału,
a nie do synchronizacji prywatnych kont. Wyniki są buforowane i ograniczane.
Od `alpha.242` oba adaptery działają równolegle, a awaria jednego nie wyłącza
drugiego ani lokalnych wyników Biblioteki. Spreaker zwraca stabilny identyfikator
audycji; AMC buduje z niego udokumentowany adres publicznego RSS i zawsze
weryfikuje kanał przed zapisaniem.

Pierwszy adapter katalogowy korzysta z udokumentowanych operacji Search i
Lookup z parametrami `media=podcast`, `entity=podcast` i `country=PL`.
Dokumentacja Apple podaje orientacyjny limit około 20 zapytań na minutę, dlatego
AMC nie wysyła zapytania po każdym znaku i przechowuje krótki cache wyników:
[iTunes Search API](https://developer.apple.com/library/archive/documentation/AudioVideo/Conceptual/iTuneSearchAPI/Searching.html).
Spreaker udostępnia publiczne operacje GET wyszukiwania audycji bez logowania;
każda audycja ma kanał w postaci
`https://www.spreaker.com/show/IDENTYFIKATOR/episodes/feed`.

SoundCloud jest obsługiwany jako dostawca zwykłego publicznego RSS: jego kanał
można dodać przez `Ctrl+N`, import OPML albo odnaleźć przez inny katalog.
Przeszukiwanie całego SoundCloud pozostaje osobnym, opcjonalnym adapterem,
ponieważ oficjalne API wymaga zarejestrowania aplikacji i tokenu. Nie należy
zastępować go nietrwałym parsowaniem strony ani prywatnym kluczem wbudowanym w
program.

Publiczny YouTube jest pierwszym działającym adapterem pojedynczego medium ze
strony. Jest celowo oddzielony od RSS/Atom i od przyszłego kontowego adaptera
YouTube. Obejmuje zwykłe filmy i transmisje bez logowania; transmisje dodane do
Radia nadal zachowują radiowe nagrywanie i harmonogram. Kolejne serwisy będą
dodawane jako jawne, testowane adaptery albo jako bezpośrednie publiczne pliki,
nie przez nieograniczone parsowanie dowolnej strony.

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
12. `alpha.222–224` — zrealizowane: stabilny powrót ze skrzynki po zniknięciu
    odsłuchanego odcinka, naprawa inicjalizacji Nowych odcinków po dawnym
    imporcie oraz bezpieczne przejście z wyszukiwania do Biblioteki albo
    właściwej audycji bez ujawniania płaskiego indeksu wszystkich rekordów.
13. `alpha.229` — zrealizowane: jawne `Ctrl+D` do domyślnego folderu,
   wielokrotne zaznaczenie, `Ctrl+S` jako zapis jednego odcinka pod wskazaną
   nazwą, postęp i atomowa publikacja gotowego pliku. Części robocze
   pozostają poza iCloud, OneDrive, Dyskiem Google i innymi folderami
   synchronizowanymi.
14. Playlisty odcinków, import i eksport OPML, osobny eksport
   danych Podcastów AMC i pełne odtworzenie ich z kopii zapasowej.
15. Strony z osadzonym audio, istniejące adaptery Polskiego Radia
   i innych rozgłośni oraz przekazywanie wyników z rozszerzenia Chrome i
   dodatku NVDA.
16. Rozdziały dostarczone przez podcast i rozdziały użytkownika
   oparte na nazwanych zakładkach.
17. Dalsze katalogi publiczne i usługi kontowe pozostają wymiennymi adapterami;
   nie mogą uzależnić od siebie RSS, Biblioteki ani lokalnych pobrań.
18. `alpha.242` — zrealizowane: publiczne wyszukiwanie Spreaker obok Apple
   Podcasts, jawne etykiety źródła, deduplikacja według adresu kanału i
   częściowa odporność na awarię katalogu. Publiczne RSS-y SoundCloud i
   Spreaker są obsługiwane bez konta.

Pozostała mapa skrótów zostanie ustalona po pierwszym działającym widoku. Nie
należy rezerwować dalszych klawiszy na podstawie samego dokumentu
koncepcyjnego.

## 7. Dodawanie, import i wyszukiwanie

- `Ctrl+N` otwiera **Nowy podcast lub medium internetowe**. Formularz przyjmuje
  bezpośredni adres kanału RSS/Atom i opcjonalną nazwę użytkownika. Przycisk
  **Sprawdź** pobiera wyłącznie ograniczone metadane kanału; dopiero aktywny po
  pomyślnej weryfikacji przycisk **Dodaj** zapisuje podcast w Bibliotece. Od
  `alpha.294` ten sam formularz rozpoznaje publiczną stronę YouTube i zapisuje
  ją jako pojedynczy wpis kolekcji **Media internetowe**.
- `Ctrl+O` otwiera plik OPML i pokazuje listę znalezionych kanałów z
  niezależnymi polami wyboru. Strzałki przesuwają fokus bez zmiany wyboru,
  Spacja zaznacza lub odznacza bieżący kanał, a `Ctrl+A` zaznacza wszystkie.
  Import
  odświeża metadane maksymalnie czterech kanałów równocześnie, nie pobiera
  odcinków audio i nie tworzy duplikatów.
- Od `alpha.272` polecenie **Eksportuj bibliotekę podcastów do OPML…** w menu
  Plik zapisuje obserwowane kanały RSS/Atom, ich nazwy oraz dostępne strony.
  Wynik można ponownie wczytać `Ctrl+O` w AMC lub innym czytniku podcastów.
  OPML nie zawiera plików audio, pozycji odsłuchu, zakładek, rozdziałów ani
  playlist odcinków. Nie zawiera też pojedynczych wpisów **Media internetowe**;
  pełna kopia AMC zachowuje te dane osobno.
- Dalszy eksport playlist odcinków ma dwa poziomy: standardowy M3U8 z nazwami i
  publicznymi adresami audio oraz format AMC JSON dla pełnej tożsamości,
  wybranych rozdziałów i ustawień. OPML pozostaje formatem subskrypcji, a nie
  playlistą odtwarzania.
- `F5` odświeża zaznaczoną lub otwartą audycję, a `Ctrl+F5` wszystkie
  obserwowane audycje. Połączenia mają ograniczenie czasu, liczby przekierowań
  i rozmiaru odpowiedzi; DTD, encje zewnętrzne, adresy inne niż HTTP/HTTPS oraz
  adresy z danymi logowania są odrzucane.
- Odświeżenie nigdy nie zastępuje lokalnego katalogu samą bieżącą odpowiedzią
  RSS. Odcinek nieobecny w odpowiedzi pozostaje w otwartej audycji ze swoim
  opisem, datą, pozycją i stanami AMC. Jeśli dawny adres audio wygasł, program
  zachowuje dane i zgłasza błąd dopiero przy próbie odtworzenia; nie usuwa
  rekordu po nieudanym połączeniu.
- Enter na audycji otwiera jej odcinki od najnowszego, a Backspace wraca do
  Biblioteki. Delete na audycji wypisuje z niej bez kasowania zapisanych danych
  odcinków; ponowne dodanie przywraca subskrypcję. `F2` ustawia nazwę własną
  podcastu w AMC; nie zmienia źródłowego kanału i nie jest nadpisywane przez
  późniejsze odświeżenia RSS.
- `Ctrl+C` kopiuje nazwę, pełny opis i publiczną stronę podcastu lub odcinka,
  jeśli kanał ją podaje, natomiast
  `Ctrl+Shift+C` kopiuje wyłącznie bezpośredni adres kanału albo pliku audio. Menu
  kontekstowe wypowiada te różnice wprost.
- **Pokaż plik w folderze** jest dostępne tylko dla rzeczywiście pobranego,
  lokalnego odcinka i zaznacza go w Eksploratorze Windows. Zdalny odcinek ma
  zamiast tego **Otwórz stronę odcinka**, a nagłówek audycji — **Otwórz stronę
  podcastu**. Gdy kanał nie podaje strony publicznej, AMC nie przedstawia
  adresu RSS ani bezpośredniego pliku audio jako strony internetowej. AMC nie
  zgaduje już, która „domyślna aplikacja” miałaby otworzyć odcinek.
- `Ctrl+F` otwiera wyszukiwanie w Podcastach. Wyniki mogą zawierać obserwowane
  audycje, zapisane odcinki oraz nieobserwowane audycje z katalogów Apple
  Podcasts i Spreaker, ale każdy rodzaj ma jawną etykietę. Podcast ma stan „w
  Bibliotece”, „poza Biblioteką”, „katalog Apple Podcasts” albo „katalog
  Spreaker”; odcinek podaje audycję
  nadrzędną i jej stan w Bibliotece.
- Zwykły Enter na zapisanym wyniku zamyka wyszukiwanie i ustawia fokus na jego
  właściwym miejscu: nagłówek audycji w nadrzędnej Bibliotece, a odcinek
  wewnątrz tej audycji. Następny Enter otwiera audycję albo odcinek. Wynik
  katalogowy jest najpierw weryfikowany przez RSS lub Atom, dodawany do
  Biblioteki i otwierany. Escape zamyka wyszukiwanie.
- Płaski magazyn wszystkich nagłówków i odcinków jest wyłącznie wewnętrznym
  indeksem wyszukiwania. Nie jest widokiem interfejsu i nie wolno wystawiać go
  jako listy liczącej tysiące pomieszanych pozycji.
- `Ctrl+Shift+L` na wyniku audycji dodaje ją do Biblioteki albo z niej usuwa.
  Na poziomie odcinków obowiązują wspólne działania AMC: Enter, `Ctrl+Enter`,
  Kolejka, Odtwórz jako następne, Ulubione, Playlisty, `Ctrl+D`, kopiowanie i
  Właściwości. Wynik pozostaje dostępny po działaniu, zgodnie z dotychczasową
  mechaniką wyszukiwania.
- `Ctrl+K` nigdy nie odpytuje internetu: filtruje tylko już załadowaną listę.

## 8. Rozdziały odcinków

Podcasty nie otrzymują w tym celu osobnego odtwarzacza ani dużego modułu
edycyjnego. Dziedziczą niewielką funkcję rozdziałów wspólnego odtwarzacza AMC.
Rozdziały wykorzystują tę samą trwałą oś czasu co nazwane Zakładki zamiast
tworzyć drugi system znaczników. `Ctrl+Alt+B` otwiera listę rozdziałów,
`Ctrl+Alt+Shift+B` dodaje własny nazwany początek, a
`Ctrl+Alt+Page Up` i `Ctrl+Alt+Page Down` nawigują po początkach. Rozdziały
dostawcy są oznaczone na liście i nie można ich skasować jak własnego punktu.
Własny punkt w tym samym czasie ma pierwszeństwo i nie jest nadpisywany przez
odświeżenie RSS. Pełny model, dostępny edytor, wykrywanie ciszy i sposoby
eksportu opisuje `PROJEKT_ROZDZIALOW_AUDIO_PL.md`.

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
- **Nowe odcinki** są widokiem automatycznym z trzema trwałymi porządkami:
  `Alt+1` od najnowszego, `Alt+2` alfabetycznie według tytułu odcinka i
  `Alt+3` grupami według podcastu, z odcinkami od najnowszego wewnątrz grupy.
  Żaden wariant nie pozwala ręcznie przestawiać skrzynki. **Pobrane** są
  filtrem rzeczywiście ukończonych plików lokalnych.
- Historia, Ulubione, Kolejka, Playlisty, Presety, wyszukiwanie, kopiowanie i
  wspólny odtwarzacz zachowują ustaloną mechanikę AMC, ale działają na
  odcinkach, nie na nagłówku audycji.
- Menu i paleta pokazują wyłącznie czynności możliwe w Podcastach. Nie wolno
  przenosić tu nagrywania Radia, zarządzania folderami ani lokalnego cięcia
  pliku strumieniowanego.
- Dla Pobranych osobne skróty porządku zostaną ustalone po teście listy.
  Skrzynka ma już przyjęte `Ctrl+I` oraz opisane wyżej `Alt+1`, `Alt+2` i
  `Alt+3`.

Skrzynka ma własne, dostępne ustawienia, ale nie ręczną kolejność playlisty:

- sortowanie od najnowszych, alfabetycznie według tytułu odcinka albo grupami
  według audycji;
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

W Podcastach `Ctrl+D` pobiera jeden lub wiele zaznaczonych odcinków do
zapamiętanego folderu, a `Ctrl+S` zapisuje jako dokładnie jeden odcinek w
wybranym miejscu. Żadne z poleceń nie działa na nagłówku całej audycji.
Pobieranie używa lokalnego pliku roboczego, anulowania, postępu i atomowej
publikacji, tak samo bezpiecznie jak nagrania Radia.

Nazwy nowych plików z `Ctrl+D` i nazwy proponowane przez `Ctrl+S` są przenośne
między Windows i macOS. Program zachowuje oryginalny tytuł w bazie, ale z nazwy
na dysku usuwa cudzysłowy i apostrofy, a przecinki, dwukropki, ukośniki oraz
podobne separatory zastępuje myślnikiem. Usuwa też znaki sterujące i końcowe
kropki lub spacje, chroni nazwy zarezerwowane oraz ogranicza długość bez utraty
rozszerzenia. Istniejące pobrania pozostają bez zmian.

## 10. Granica pierwszej wersji

`alpha.206` pozwala dodać bezpośredni kanał RSS/Atom, zaimportować OPML,
odświeżyć pojedynczą audycję albo całą Bibliotekę, wejść do listy odcinków i
odtworzyć skończony materiał HTTP/HTTPS.
Pierwsze pobranie zachowuje starsze archiwum wewnątrz audycji, ale nie oznacza
go całego jako nowe. Dopiero odcinki odnalezione podczas późniejszego
odświeżenia trafiają do podstawowej skrzynki **Nowe odcinki**. Odświeżanie
nigdy nie pobiera zawartości plików audio. Jawne pobieranie do widoku
**Pobrane** jest dostępne od `alpha.229`, a katalog Apple od `alpha.217`.
Wydobywanie audio ze zwykłych stron pozostaje kolejnym etapem.

## 11. Magazyn i duże biblioteki

Archiwum metadanych Podcastów nie może rosnąć we wspólnym `state.json`.
Kanały i odcinki są przechowywane w lokalnej bazie
`%LocalAppData%\AccessibleMediaController\podcasts.db`; pliki audio nadal
powstają wyłącznie po jawnym pobraniu i nigdy nie są zawartością bazy.

Pierwsze uruchomienie wersji z nowym magazynem wykonuje następujący przebieg:

1. odczytuje dotychczasowy stan, ale go nie nadpisuje;
2. zachowuje `state.pre-podcast-sqlite-migration.json`;
3. zapisuje kanały i odcinki w jednej transakcji SQLite;
4. porównuje liczbę rekordów źródłowych i docelowych;
5. sprawdza integralność bazy;
6. dopiero wtedy zapisuje mały `state.json` bez archiwum Podcastów.

Niepowodzenie przed punktem szóstym pozostawia źródłowy JSON i jego kopię.
Pełny eksport `.amcbackup.json` pozostaje formatem przenośnym i zawiera
Podcasty niezależnie od wewnętrznego podziału baz.

Widok nie tworzy kontrolek dla całego archiwum. Skrzynka, rozpoczęte, pobrane
i wnętrze audycji pokazują po 150 odcinków. Ostatnią pozycją jest wtedy
**Załaduj więcej odcinków, pozostało N**. Enter dodaje następną porcję i
ustawia fokus na pierwszym nowym odcinku; zapamiętany odcinek zostaje włączony
do odpowiedniej porcji, aby powrót nie przenosił użytkownika na początek.
Pozycja doładowania nie może trafić do trwałego zaznaczenia, wyszukiwania,
odtwarzania ani menu działań multimedialnych. `Ctrl+K` filtruje cały logiczny
widok i dopiero potem stosuje stronicowanie.

Aktualizacja zapisu porównuje rekordy i przepisuje tylko zmienione kanały oraz
odcinki. Kolejny etap architektury ma wykonywać także zapytania i odświeżanie
bez ładowania pełnego archiwum do modelu procesu. Do czasu jego ukończenia baza
i ograniczony zestaw wierszy rozwiązują największy koszt `state.json` i WPF,
ale nie są jeszcze końcem optymalizacji pamięci.

Automatyczne odświeżenie może trwać długo, dlatego nie wolno kotwiczyć fokusu
na samej kontrolce listy przed rozpoczęciem operacji sieciowej. Bezpośrednio
przed końcową przebudową AMC ponownie odczytuje bieżącą sesję, widok,
zaznaczenie i powierzchnię odtwarzacza. Listę odświeża i przywraca jej fokus
tylko wtedy, gdy użytkownik nadal znajduje się w tym samym widoku Podcastów.
Wynik rozpoczęty w Podcastach nie może po kilku minutach przejąć fokusu w
Plikach lokalnych, innym widoku ani w odtwarzaczu.

Ta sama kontrola obowiązuje pobieranie odcinka oraz równoległe skanowanie
Folderów Biblioteki. Operacja lokalna może przebudować i zakotwiczyć wyłącznie
widoczną listę Plików lokalnych; nigdy listę Podcastów lub Radia, która akurat
znalazła się pod fokusem przed zakończeniem skanowania.
