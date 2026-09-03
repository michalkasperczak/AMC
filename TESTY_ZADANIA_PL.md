# Zadania testowe AMC

- Numer zestawu: `AMC-TEST-236`
- Tytuł zestawu: Rzeczywiste wyniki katalogu Apple w wyszukiwaniu Podcastów oraz regresja całej aplikacji
- Wersja programu: `0.1.0-alpha.236`
- Utworzono: 2026-09-03, Europe/Warsaw
- Plik wyników: `wyniki-testow/WYNIKI_0.1.0-alpha.236.md`

Obserwacje, których nie uda się jednoznacznie potwierdzić ani odrzucić w tym
przebiegu, należy dopisać do `REJESTR_RYZYK_I_NIEJEDNOZNACZNOSCI_PL.md` wraz z
warunkami, częstością i najmniejszym proponowanym testem. Nie należy zmieniać
ich od razu w potwierdzony błąd.

Na początku pliku wyników wystarczy opisać zauważone zachowanie. Nie trzeba przed każdym zadaniem dopisywać osobnego wariantu „OK” lub „błąd”. Po dwukropku wpisuj spację.

Zestaw regresji jest globalny: obserwacje należy odnosić do wszystkich sesji
i wspólnych mechanizmów AMC, nawet jeżeli nowa poprawka dotyczy jednego modułu.

## Nowości alpha 236

### AMC-236-01 — katalog Apple bez lokalnego wyniku

W sesji Podcasty naciśnij `Ctrl+F` i wpisz nazwę podcastu, którego na pewno nie
ma w Bibliotece. Naciśnij Enter i przejdź po początkowej części listy.

Oczekiwane: co najmniej część nagłówków jest opisana jako „katalog Apple
Podcasts”. Wyniki katalogowe są widoczne również wtedy, gdy nie znaleziono
żadnego zapisanego podcastu ani odcinka. Nie pojawia się dawna lista wszystkich
odcinków sesji.

### AMC-236-02 — Polskie Radio i kolejność typów

Wyszukaj przez `Ctrl+F` tekst „Polskie Radio”. Przejdź po całej liście,
sprawdzając Home, End i nawigację literami.

Oczekiwane: wyszukiwanie obejmuje publiczne wyniki Apple, pozycje z Biblioteki
i zapisane odcinki. Wszystkie nagłówki podcastów występują przed odcinkami.
Pozycje katalogowe mówią „katalog Apple Podcasts”, zapisane — „w Bibliotece”.
Ten sam kanał nie występuje podwójnie tylko z powodu obecności w obu źródłach.

### AMC-236-03 — trzy działania Enter

Otwórz kolejno: zapisany nagłówek podcastu, zapisany odcinek oraz nowy wynik
z katalogu Apple.

Oczekiwane: nagłówek otwiera swoje odcinki; odcinek prowadzi do właściwej
audycji i ustawia na sobie fokus; wynik katalogowy sprawdza publiczny RSS lub
Atom, dodaje audycję do Biblioteki i otwiera jej odcinki. Żaden wariant nie
prowadzi do płaskiej listy wszystkich podcastów i odcinków.

### AMC-236-04 — kopiowanie i ponowne wyszukiwanie

Zaznacz Shiftem kilka wyników Podcastów i sprawdź `Ctrl+C` oraz
`Ctrl+Shift+C`. Zamknij okno, otwórz `Ctrl+F` ponownie i powtórz zapytanie.

Oczekiwane: `Ctrl+C` nadal kopiuje czytelne opisy i strony, a
`Ctrl+Shift+C` bezpośrednie źródła. Ponowne wyszukiwanie nie zwiększa liczby
duplikatów i nie zmienia zawartości Biblioteki bez jawnego wybrania wyniku.

## Nowości alpha 235

### AMC-235-01 — opis odcinka przed danymi

W Podcastach zaznacz odcinek zawierający dłuższy opis i naciśnij `Alt+D`.
Bez naciskania Tab przejdź strzałką w dół po pierwszych wierszach, potem użyj
`Ctrl+End` i przeczytaj końcową część pola.

Oczekiwane: fokus i karetka są od razu w polu tylko do odczytu, na jego
początku. Pierwszą treścią jest opis odcinka, a nie jego tytuł. Dopiero pod
całym opisem występują dane: tytuł, podcast, autor, data, czas, stan
odsłuchania, źródło audio i strona, o ile kanał je udostępnia. Opis nie jest
powtórzony drugi raz.

### AMC-235-02 — opis całego podcastu i łącza

Wróć na listę audycji, zaznacz podcast z opisem i co najmniej jednym adresem
w treści, po czym naciśnij `Alt+D`. Przejdź Tabem do listy łączy, otwórz jedno
Enterem, wróć do AMC i zamknij okno Escape.

Oczekiwane: najpierw znajduje się opis audycji, a pod nim jej dane wraz ze
źródłem RSS lub Atom. Lista łączy ma wyłącznie czytelne etykiety, bez nazw klas
i identyfikatorów. Escape wraca dokładnie do wcześniej zaznaczonego podcastu.

### AMC-235-03 — ponowne otwarcie i brak opisu

Kilkakrotnie otwórz i zamknij opis tego samego odcinka. Następnie użyj
`Alt+D` na pozycji, która nie ma opisu.

Oczekiwane: każde otwarcie rozpoczyna się od pierwszego znaku opisu; fokus nie
zostaje na końcu ani na liście łączy. Dla pozycji bez opisu AMC podaje krótki,
jednoznaczny komunikat i nie otwiera pustego okna.

## Nowości alpha 234

### AMC-234-01 — jedna stacja, dwa wyjścia

W Radiu uruchom stację na domyślnym urządzeniu, ustaw wyraźny poziom, np. 20%,
a przez `Shift+A` wybierz inne wyjście i ustaw np. 55%. Kilka razy zmieniaj
wyjście i stację, a po zakończeniu wszystkich nagrań uruchom AMC ponownie.

Oczekiwane: każda para stacja–wyjście przywraca swój poziom. AMC nie zmienia
głośności Windows, urządzenia ani NVDA. Pierwsze użycie nieustawionej pary
korzysta z bezpiecznej dotychczasowej wartości.

### AMC-234-02 — podcasty według audycji i wyjścia

Otwórz dwa odcinki tej samej audycji i ustaw jej poziom na jednym wyjściu.
Sprawdź drugi odcinek, potem zmień urządzenie i ustaw inną wartość. Na koniec
otwórz odcinek innej audycji.

Oczekiwane: odcinki tej samej audycji dzielą poziom na danym wyjściu; drugie
wyjście ma niezależną wartość. Inna audycja nie przejmuje jawnego nadpisania
pierwszej.

### AMC-234-03 — lokalny plik i zmiana urządzenia

W Plikach lokalnych ustaw różne poziomy tego samego pliku na dwóch wyjściach.
Przełącz plik `Page Up/Page Down`, wróć i ponownie zmieniaj urządzenie.

Oczekiwane: AMC przywraca poziom zapisany dla pliku i skutecznego wyjścia,
również po ponownym otwarciu. Nawigacja, pozycja i prędkość nie zmieniają się.

### AMC-234-04 — odłączone urządzenie i powrót awaryjny

Zapamiętaj osobny poziom na dodatkowym urządzeniu, odłącz je i uruchom ten sam
materiał. Zmień głośność podczas awaryjnego odsłuchu na urządzeniu domyślnym,
a następnie podłącz dodatkowe wyjście i wybierz je przez `Shift+A`.

Oczekiwane: zmiana podczas awaryjnego użycia wyjścia domyślnego nie nadpisuje
poziomu odłączonego urządzenia. Po jego powrocie AMC odtwarza jego własną
wartość; brak urządzenia nie blokuje fokusa ani klawiatury.

## Nowości alpha 233

### AMC-233-01 — Ctrl+D na liście odcinków

W Podcastach otwórz dowolną audycję albo `Ctrl+I`, zaznacz jeden odcinek i
naciśnij `Ctrl+D`. Powtórz z dwoma odcinkami zaznaczonymi Shiftem.

Oczekiwane: skrót działa od pierwszego naciśnięcia. Pliki trafiają do folderu
wybranego w **Ustawienia > Podcasty**, a AMC podaje wynik i zachowuje fokus.

### AMC-233-02 — Ctrl+S jako Zapisz jako

Na jednym odcinku naciśnij `Ctrl+S`, wybierz nazwę i folder, a następnie anuluj
drugą próbę Escapem. Powtórz próbę przy zaznaczeniu dwóch odcinków.

Oczekiwane: dla jednego odcinka otwiera się systemowe **Zapisz jako**. Anulowanie
niczego nie zapisuje. Przy wielu odcinkach AMC wyjaśnia, że polecenie działa dla
jednego odcinka, bez otwierania serii okien.

### AMC-233-03 — odtwarzacz, menu i niewłaściwy element

Otwórz odcinek w odtwarzaczu i sprawdź `Ctrl+D`, `Ctrl+S` oraz jego menu
kontekstowe. Potem wróć do nadrzędnej listy podcastów, ustaw fokus na nagłówku
audycji i ponów oba skróty. Sprawdź też menu Plik i menu kontekstowe odcinka.

Oczekiwane: oba skróty działają również w odtwarzaczu. Menu odcinka zawiera
czytelne polecenia ze skrótami. Nagłówek audycji nie rozpoczyna pobierania całego
archiwum; AMC prosi o wskazanie co najmniej jednego odcinka.

### AMC-233-04 — rozdzielenie skrótów i wyszukiwanie

Na odcinku z wyników `Ctrl+F` sprawdź `Ctrl+D` i `Ctrl+S`. Następnie sprawdź,
że `Ctrl+Shift+D`, `Ctrl+Shift+S` oraz te same skróty w Radiu nie uruchamiają
pobierania Podcastów.

Oczekiwane: wyniki wyszukiwania zapisują właściwy odcinek i zachowują okno oraz
fokus. Skróty o innych modyfikatorach zachowują swoje dotychczasowe znaczenie.

## Nowości alpha 232

### AMC-232-01 — jeden Enter zapisuje urządzenie

W odtwarzającej sesji Radia naciśnij `Shift+A`, rozwiń listę, wybierz inne
dostępne urządzenie strzałkami i naciśnij Enter tylko raz.

Oczekiwane: okno wyboru zamyka się, AMC oznajmia wybrane urządzenie, a dźwięk
przechodzi na nie bez drugiego Enteru.

### AMC-232-02 — trwałość wyboru

Ponownie otwórz `Shift+A`, sprawdź zaznaczenie, przełącz się do Podcastów i z
powrotem, a następnie — gdy nic nie jest nagrywane — uruchom AMC ponownie.

Oczekiwane: Radio zachowuje własne urządzenie po każdym powrocie i ponownym
uruchomieniu. Podcasty zachowują niezależny wybór.

### AMC-232-03 — anulowanie

Otwórz wybór urządzenia, wskaż inną pozycję i naciśnij Escape zamiast Enteru.

Oczekiwane: okno zamyka się bez przełączania i bez zapisania wskazanej pozycji.

## Nowości alpha 231

### AMC-231-01 — nazwa podcastu w skrzynce

Otwórz `Ctrl+I` w Podcastach i znajdź odcinki Radia Gdańsk, na przykład ze
„Srebrnego Pokolenia” albo „Nad Rozlewiskiem”.

Oczekiwane: wiersz podaje tytuł odcinka, nazwę podcastu, autora oraz słowo
„odcinek”, na przykład „Co sprawia, że seniorzy czują się dobrze?, Srebrne
Pokolenie, Anna Kobryń, odcinek”. Nazwa podcastu nie znika i nie jest
powtórzona.

### AMC-231-02 — pozostałe zbiorcze widoki Podcastów

Sprawdź odcinek z podanym autorem kolejno w Ulubionych, Kolejce, historii,
W trakcie słuchania, Pobranych i playliście, o ile występuje w danym widoku.

Oczekiwane: każdy zbiorczy widok zachowuje tę samą jednoznaczną kolejność
tytułu odcinka, podcastu i autora.

### AMC-231-03 — otwarty podcast bez powtórzeń

Wejdź z Biblioteki do „Srebrnego Pokolenia” i poruszaj się po jego odcinkach.

Oczekiwane: nagłówek widoku podaje nazwę podcastu, a poszczególne wiersze nie
powtarzają jej bez potrzeby. Data, czas i stan odsłuchania pozostają czytelne.

## Nowości alpha 230

### AMC-230-01 — Radio Kolor po dodaniu do Ulubionych

W Radiu naciśnij `Ctrl+F`, wyszukaj stację spoza bieżącego widoku, dodaj ją do
Ulubionych przez `Ctrl+Shift+U`, a następnie naciśnij zwykły Enter.

Oczekiwane: wyszukiwanie zamyka się, główna lista pokazuje Ulubione z fokusem
na tej stacji. Nie pojawia się lista setek dawnych wyników Radio Browser.

### AMC-230-02 — wynik zapisany w Bibliotece

Powtórz wyszukiwanie dla stacji należącej do Biblioteki, lecz nie do
Ulubionych, i naciśnij Enter. Następnie uruchom ją drugim Enterem, wyjdź z
odtwarzacza Escape i przełącz sesję tam i z powrotem.

Oczekiwane: każdy powrót prowadzi do Biblioteki i tej samej stacji. Wewnętrzny
widok `Multimedia` nie pojawia się ani przed odtwarzaniem, ani po nim.

### AMC-230-03 — globalna regresja wyszukiwania i fokusa

Powtórz zwykły Enter oraz jedno działanie bezpośrednie w wyszukiwaniu Plików
lokalnych, Radia i Podcastów. Sprawdź Escape, ponowne wejście do sesji,
strzałki, menu kontekstowe i `NVDA+End` podczas odtwarzania.

Oczekiwane: każda sesja wraca do własnego jawnego widoku i elementu, fokus nie
przechodzi do filtra ani ukrytej kontrolki, a żaden wewnętrzny katalog nie
staje się listą użytkownika. Pozostałe spostrzeżenia wpisz do globalnego
rejestru ryzyk, nawet jeżeli nie dotyczą Radia.

## Nowości alpha 229

### AMC-229-01 — domyślny folder i Ctrl+D

W **Ustawienia > Podcasty** wybierz pusty folder. Na liście odcinków zaznacz
jeden odcinek i naciśnij `Ctrl+D`.

Oczekiwane: AMC pobiera odcinek bez drugiego pytania, ogłasza zakończenie,
tworzy kompletny plik w wybranym folderze i pokazuje odcinek w widoku
**Pobrane**. Fokus wraca na ten sam odcinek.

### AMC-229-02 — wielokrotne pobieranie

Zaznacz Shiftem dwa albo trzy odcinki, także w wynikach `Ctrl+F`, i naciśnij
`Ctrl+D`. Po zakończeniu powtórz polecenie na tych samych pozycjach.

Oczekiwane: pierwsze polecenie pobiera cały zaznaczony zakres i podaje
podsumowanie. Drugie nie pobiera ponownie odcinków już zapisanych przez AMC.
Wyszukiwanie pozostaje otwarte i zachowuje fokus.

### AMC-229-03 — Ctrl+S jako Zapisz jako

Wybierz jeden odcinek i naciśnij `Ctrl+S`. Zapisz go pod inną nazwą poza
folderem domyślnym. Następnie zaznacz kilka odcinków i ponów skrót.

Oczekiwane: dla jednego odcinka pojawia się systemowe okno **Zapisz jako**.
Wielokrotne zaznaczenie daje jasny komunikat i nie otwiera kilku okien.
Jednorazowy eksport nie zmienia folderu skonfigurowanego dla `Ctrl+D`.

### AMC-229-04 — menu, paleta i NVDA

Sprawdź menu Plik, menu kontekstowe listy i odtwarzacza, wyniki wyszukiwania,
paletę `Ctrl+Shift+K` oraz Pomoc klawiatury `Ctrl+F1`.

Oczekiwane: pozycje pojawiają się tylko dla odcinków Podcastów, mają czytelne
nazwy `Ctrl+D` i `Ctrl+S`, nie ujawniają identyfikatorów ani reprezentacji
rekordów. Na nagłówku podcastu i w innych sesjach nie sugerują pobrania całego
archiwum.

### AMC-229-05 — odporność przerwanego pobierania

Jeżeli możesz, rozpocznij pobieranie większego odcinka i przerwij sieć albo
zamknij AMC przed końcem. Po ponownym uruchomieniu sprawdź folder docelowy.

Oczekiwane: pod końcową nazwą nie ma pustego ani urwanego pliku. Ponowne
`Ctrl+D` może zacząć czyste pobieranie, a folder synchronizowany przez iCloud,
OneDrive lub Dysk Google nie otrzymuje pliku częściowego jako gotowego.

## Nowości alpha 227

### AMC-227-01 — fokus podczas odtwarzania podcastu

Otwórz odcinek podcastu i przez co najmniej dwie minuty używaj na przemian
strzałek, cyfr, Page Up, Page Down oraz odczytu paska `NVDA+End`.

Oczekiwane: fokus przez cały czas pozostaje w odtwarzaczu. Każdy skrót działa
od pierwszego naciśnięcia; nie trzeba naciskać dodatkowego Escape ani wracać
do AMC przez Alt+Tab.

### AMC-227-02 — szybka zmiana odcinków

W odtwarzaczu kilka razy szybko zmień odcinek przez Page Up i Page Down,
również zanim poprzedni materiał zdąży się całkowicie otworzyć.

Oczekiwane: po zakończeniu otwierania fokus nadal znajduje się na przycisku
odtwarzania bieżącego odcinka, a klawisze sterowania nie przestają działać.

### AMC-227-03 — powrót do listy

Podczas odtwarzania naciśnij Escape, przejdź po liście, wróć do odtwarzacza
klawiszem F6 i ponownie sprawdź `NVDA+End`.

Oczekiwane: Escape ustawia fokus na zapamiętanym elemencie listy, F6 na
odtwarzaczu, a odczyt paska nie zabiera fokusa żadnemu z tych widoków.

## Nowości alpha 226

### AMC-226-01 — Ctrl+L przywraca zapamiętany podcast

W Bibliotece Podcastów otwórz dowolny podcast, przejdź kilka odcinków w dół,
następnie przejdź do Ulubionych lub Kolejki i naciśnij `Ctrl+L`.

Oczekiwane: program wraca do listy odcinków tego samego podcastu i zachowuje
ostatni fokus. Jest to zamierzone przywrócenie miejsca, a nie płaska lista
wszystkich odcinków.

### AMC-226-02 — jednoznaczny poziom nadrzędny

Na liście odcinków otwartej przez `Ctrl+L` naciśnij Escape. Powtórz próbę z
Backspace.

Oczekiwane: każdy z tych klawiszy przechodzi bezpośrednio do listy podcastów i
ustawia fokus na audycji, z której pochodziły odcinki. Nie pozostaje na tej
samej liście i nie wraca do Nowych odcinków, wyników wyszukiwania ani innego
wcześniejszego widoku.

### AMC-226-03 — zapamiętanie świadomego wyjścia

Po powrocie Escape do nadrzędnej listy przejdź do innego widoku i ponownie
naciśnij `Ctrl+L`.

Oczekiwane: Biblioteka pozostaje na liście podcastów. Ponowne wejście do listy
odcinków następuje dopiero po wybraniu podcastu Enterem.

## Nowości alpha 225

### AMC-225-01 — trzy porządki Nowych odcinków

W sesji Podcasty otwórz `Ctrl+I`, a następnie sprawdź kolejno `Alt+1`, `Alt+2`
i `Alt+3`.

Oczekiwane: Alt+1 pokazuje najnowsze odcinki na początku, Alt+2 porządkuje
alfabetycznie według tytułu odcinka, a Alt+3 grupuje według nazwy podcastu i
zachowuje odcinki od najnowszego wewnątrz każdej grupy. Program mówi
„Według podcastu”, nie „Kolejność własna”. Alt+strzałka góra/dół nie
przestawia automatycznej skrzynki.

### AMC-225-02 — Ctrl+C dla jednego i wielu odcinków

Na jednym odcinku użyj `Ctrl+C`. Potem zaznacz Shiftem kilka odcinków i
powtórz polecenie. Wklej wynik do edytora tekstu.

Oczekiwane: każdy odcinek tworzy osobny blok zawierający nazwę, pełny opis i
publiczny adres strony, jeśli kanał go udostępnia. Bloki wielu odcinków są
rozdzielone pustym wierszem; zaznaczenie i fokus w AMC pozostają bez zmian.

### AMC-225-03 — Ctrl+Shift+C kopiuje wyłącznie audio

Na tym samym wielokrotnym zaznaczeniu użyj `Ctrl+Shift+C`, również w wynikach
`Ctrl+F`, i wklej wynik do edytora.

Oczekiwane: schowek zawiera wyłącznie bezpośrednie adresy audio, po jednym w
wierszu. Nie ma przed nimi nazw odcinków, opisów ani adresów publicznych stron.

## Nowości alpha 224

### AMC-224-01 — stan audycji w wynikach

W sesji Podcasty naciśnij `Ctrl+F` i wyszukaj „Polskie radio” albo inną nazwę,
która występuje w kilku obserwowanych podcastach i odcinkach.

Oczekiwane: nagłówek obserwowanego podcastu zawiera „w Bibliotece”. Odcinek
jest jednoznacznie opisany jako odcinek właściwego podcastu i podaje, że
podcast znajduje się w Bibliotece. Wynik zewnętrzny ma etykietę „katalog Apple
Podcasts”, a nie fałszywy stan subskrypcji.

### AMC-224-02 — Enter na odcinku

Wybierz w wynikach odcinek obserwowanego podcastu i naciśnij Enter.

Oczekiwane: okno wyszukiwania zamyka się, a główna lista pokazuje wyłącznie
odcinki właściwego podcastu z fokusem na znalezionym odcinku. Nie pojawia się
lista około dziewięciu tysięcy wszystkich rekordów. Kolejny Enter uruchamia
odcinek.

### AMC-224-03 — działania bezpośrednie i nagłówek podcastu

Ponownie wyszukaj tę samą nazwę. Na jednym wyniku użyj bezpośredniego działania,
na przykład `Ctrl+Shift+U`, po czym zamknij wyszukiwanie Escape. Osobno wybierz
nagłówek obserwowanego podcastu i naciśnij Enter.

Oczekiwane: po działaniu program nadal wraca do właściwej audycji albo
Biblioteki. Enter na nagłówku ustawia go w nadrzędnej Bibliotece; następny Enter
otwiera jego odcinki. Techniczny płaski widok nie pojawia się żadną z tych dróg.

## Nowości alpha 223

### AMC-223-01 — migracja dotychczasowej skrzynki

Po pierwszym uruchomieniu `alpha.223` przejdź do sesji Podcasty i naciśnij
`Ctrl+I`.

Oczekiwane: skrzynka zawiera najnowsze nierozpoczęte odcinki obserwowanych
audycji, których brakowało po wcześniejszym imporcie. Nie pojawia się całe
archiwum liczące tysiące pozycji.

### AMC-223-02 — jawne odświeżenie

W widoku Nowe odcinki naciśnij `F5` i zaczekaj na zakończenie.

Oczekiwane: komunikat podaje liczbę odświeżonych podcastów, odcinków dodanych
właśnie teraz oraz wszystkich pozycji pozostających w skrzynce. `Ctrl+I` samo
nie rozpoczyna długiego pobierania sieciowego.

### AMC-223-03 — rozpoczęte odcinki

Otwórz jeden z nowych odcinków, słuchaj co najmniej minutę, wróć Escape i
ponownie otwórz `Ctrl+I`. Następnie sprawdź `Ctrl+Shift+I`.

Oczekiwane: rozpoczęty odcinek znika z Nowych odcinków i pojawia się we
„W trakcie słuchania”. Ponowne uruchomienie AMC nie oznacza go znów jako nowy.

## Nowości alpha 222

### AMC-222-01 — odcinek nadal należy do skrzynki

Otwórz `Ctrl+I`, przejdź kilka pozycji w dół, uruchom odcinek i przed upływem
minuty wróć Escape.

Oczekiwane: fokus wraca dokładnie na uruchomiony odcinek, a nie na pierwszy
wiersz listy.

### AMC-222-02 — odcinek znika z Nowych odcinków

W `Ctrl+I` uruchom odcinek znajdujący się w środku listy. Słuchaj go co
najmniej minutę, aby przeszedł do „W trakcie słuchania”, po czym naciśnij
Escape.

Oczekiwane: odcinek nie jest już pokazywany jako nowy, ale fokus pozostaje w
jego dotychczasowym miejscu — na najbliższym sąsiednim odcinku. Nie wraca na
początek skrzynki ani do pola filtrowania.

### AMC-222-03 — granice listy

Powtórz poprzedni test na ostatnim odcinku w skrzynce.

Oczekiwane: po zniknięciu ostatniego odcinka fokus trafia na nowy ostatni
wiersz. Jeżeli skrzynka stała się pusta, AMC pozostawia dostępny pusty widok i
nie przenosi fokusa do innej funkcji.

## Nowości alpha 221

### AMC-221-01 — szybka zmiana podczas odtwarzania

Otwórz odtwarzacz podcastu, pliku lokalnego i radia. W każdym z nich kilka razy
szybko użyj `Page Up`, `Page Down`, strzałek sterujących oraz `F6`.

Oczekiwane: wszystkie skróty działają od pierwszego naciśnięcia. Fokus
pozostaje w odtwarzaczu i nie trzeba odzyskiwać go klawiszem Escape.

### AMC-221-02 — menu kontekstowe odtwarzacza

W otwartym odtwarzaczu otwórz menu kontekstowe, przejdź po kilku poleceniach i
zamknij je Escape bez wykonywania polecenia. Od razu użyj strzałki albo
`Page Down`.

Oczekiwane: po zamknięciu menu fokus wraca do odtwarzacza, a następny klawisz
steruje nim bez dodatkowego Escape.

### AMC-221-03 — granice ochrony

Podczas odtwarzania otwórz okno `Shift+A`, anuluj je, a następnie przełącz się
Alt+Tab do innej aplikacji i wróć do AMC.

Oczekiwane: AMC nie odbiera fokusa oknu wyboru ani innej aplikacji. Po powrocie
do głównego okna fokus znajduje się w aktualnym odtwarzaczu albo na bieżącej
liście, zależnie od otwartego widoku.

## Nowości alpha 220

### AMC-220-01 — zniknięcie wybranego urządzenia

Uruchom plik albo podcast na dodatkowym urządzeniu, odłącz lub wyłącz to
urządzenie, a następnie przez `Shift+A` wybierz inne dostępne wyjście.

Oczekiwane: AMC przyjmuje nowy wybór i ponownie uruchamia bieżący element od
ostatniej bezpiecznej pozycji. Nie trzeba ponownie uruchamiać programu.

### AMC-220-02 — radio i ponowny wybór wyjścia

Powtórz zmianę w sesji Radio. Po zniknięciu urządzenia wybierz sprawne wyjście
i sprawdź także ponowne uruchomienie tej samej stacji.

Oczekiwane: radio staje się słyszalne na nowym urządzeniu; AMC nie pozostaje w
niemym, zablokowanym stanie.

### AMC-220-03 — świadomie wstrzymana sesja

Wstrzymaj zwykły plik lub podcast, gdy urządzenie nadal jest dostępne, a potem
zmień urządzenie przez `Shift+A`.

Oczekiwane: wybór zostaje zapamiętany, ale świadomie wstrzymany materiał nie
uruchamia się sam. Po ręcznym wznowieniu gra na nowym wyjściu.

## Poprzedni zestaw alpha 219

### AMC-219-01 — przejście z odcinka do audycji

W sesji Podcasty wybierz odcinek widoczny w Nowych odcinkach, Ulubionych,
Kolejce, Historii albo playliście. Otwórz menu kontekstowe i wybierz
**Przejdź do podcastu**.

Oczekiwane: AMC otwiera listę odcinków właściwej audycji i ustawia fokus na
tym samym odcinku. NVDA nie odczytuje identyfikatora ani technicznej nazwy
obiektu.

### AMC-219-02 — wyszukiwanie i odtwarzacz

Wyszukaj zapisany odcinek przez `Ctrl+F`, wybierz z jego menu kontekstowego
**Przejdź do podcastu**. Powtórz test z otwartego odtwarzacza.

Oczekiwane: oba przejścia kończą się na właściwej audycji i tym samym odcinku.
Po późniejszym przejściu do innego widoku `Ctrl+L` wraca w to miejsce.

### AMC-219-03 — właściwy zakres polecenia

Sprawdź menu kontekstowe nagłówka podcastu, zwykłego pliku lokalnego i stacji
radia oraz paletę poleceń w kilku sesjach.

Oczekiwane: polecenie jest dostępne wyłącznie dla odcinka mającego podcast
nadrzędny w Bibliotece. Nie zajmuje nowego skrótu domyślnego.

## Poprzedni zestaw alpha 218

### AMC-218-01 — powrót z Kolejki do audycji

W sesji Podcasty otwórz Bibliotekę, wejdź do wybranego podcastu i ustaw fokus
na odcinku innym niż pierwszy. Przejdź przez `Ctrl+Q` do Kolejki, a następnie
naciśnij `Ctrl+L`.

Oczekiwane: AMC wraca do listy odcinków tej samej audycji i ustawia fokus na
tym samym odcinku. Nie pokazuje nadrzędnej listy wszystkich podcastów.

### AMC-218-02 — świadome przejście poziom wyżej

Po powrocie do listy odcinków naciśnij Backspace albo Escape, a następnie
przejdź do innego widoku i wróć przez `Ctrl+L`.

Oczekiwane: Backspace lub Escape otwiera nadrzędną Bibliotekę. Po świadomym
opuszczeniu audycji kolejne `Ctrl+L` wraca już do nadrzędnej listy, z
zapamiętanym fokusem na właściwym podcaście.

### AMC-218-03 — ponowne uruchomienie i usunięta audycja

Pozostaw otwartą listę odcinków, zamknij bezpiecznie AMC i uruchom je ponownie.
Sprawdź także później wariant, w którym zapamiętana audycja została usunięta z
Biblioteki.

Oczekiwane: po ponownym uruchomieniu program zachowuje audycję, odcinek i
poziom listy. Dla usuniętej audycji bez błędu wraca do nadrzędnej Biblioteki.

## Poprzedni zestaw alpha 217

## Nowości alpha 217

### AMC-217-01 — wyszukiwanie Apple Podcasts

Przejdź do sesji Podcasty, naciśnij `Ctrl+F` i wyszukaj audycję, której nie ma
jeszcze w Bibliotece. Wybierz wynik oznaczony jako podcast i naciśnij Enter.

Oczekiwane: program mówi o wyszukiwaniu w katalogu Apple Podcasts. Enter
sprawdza publiczny kanał, dodaje podcast do Biblioteki i otwiera jego odcinki.
Nie pojawia się logowanie do Apple, techniczny obiekt ani pusta lista. Jeżeli
katalog lub kanał jest chwilowo niedostępny, program podaje kontrolowany błąd i
zachowuje fokus.

### AMC-217-02 — opis podcastu i odcinka

Na nagłówku podcastu, a potem na jednym z jego odcinków naciśnij `Alt+D`.
Przejdź po tekście strzałkami, `Ctrl+strzałkami`, zaznacz fragment Shiftem i
skopiuj `Ctrl+C`. Jeżeli opis zawiera adres, przejdź Tabem do listy łączy.
Zamknij okno Escape.

Oczekiwane: opis jest zwykłym tekstem tylko do odczytu, a nie jednym wierszem
listy ani technicznym rekordem. Łącza są czytelne i otwieralne Enterem. Escape
wraca dokładnie do wcześniejszego podcastu lub odcinka. Strzałki na zwykłej
liście nie rozpoczynają samoczynnego czytania całego opisu.

### AMC-217-03 — pierwsze dodanie i skrzynka

Po dodaniu nowej audycji naciśnij `Ctrl+I`. Sprawdź jej pozycje, a potem wróć
do samej audycji i porównaj liczbę wszystkich odcinków.

Oczekiwane: pełne archiwum jest dostępne wewnątrz audycji, lecz jako nowy w
skrzynce pojawia się tylko jej najnowszy odcinek. Skrzynka może jednocześnie
zawierać rzeczywiście nowe odcinki innych obserwowanych audycji.

### AMC-217-04 — odświeżenie skrzynki

Otwórz `Ctrl+I` i naciśnij `F5`; powtórz przez odpowiednią pozycję menu Widok.

Oczekiwane: oba sposoby odświeżają wszystkie obserwowane podcasty, po
zakończeniu podają liczbę poprawnie odświeżonych audycji i nowo znalezionych
odcinków, a następnie pokazują aktualną skrzynkę. Widok nie pozostaje pusty z
powodu starej kopii danych.

### AMC-217-05 — wyszukiwanie globalne

Naciśnij `Ctrl+Shift+F` i wyszukaj nazwę, która daje wyniki radiowe i
podcastowe. Przejdź po liście, kopiując wybrane nazwy przez `Ctrl+C` i
lokalizacje przez `Ctrl+Shift+C`.

Oczekiwane: komunikat mówi o katalogach radia i podcastów. Każdy wynik podaje
właściwą sesję. Podcast ma osobno publiczną stronę oraz bezpośredni adres
RSS/Atom; wynik radiowy zachowuje adres strumienia. Wielokrotne zaznaczenie
kopiuje wszystkie wskazane pozycje.

### AMC-217-06 — regresja sortowania

W Radiu otwórz Ulubione. Naciśnij `Alt+1`, `Alt+2` i `Alt+3`, a potem powtórz
w Bibliotece Podcastów i ich Ulubionych. Sprawdź także lokalną Bibliotekę.

Oczekiwane: skróty nie opuszczają Ulubionych. W trwałych kolekcjach oznaczają
odpowiednio kolejność dodania od najnowszych, alfabet i kolejność własną.
Lokalna Biblioteka zachowuje wyjątek: Foldery, Wszystkie pliki i Kolejność
własna. Historia, wyszukiwanie i Nowe odcinki nie przejmują tych skrótów.

### AMC-217-07 — dostępność polecenia opisu

W Podcastach sprawdź menu główne Odtwarzanie, menu kontekstowe listy,
odtwarzacz, paletę `Ctrl+Shift+K` i Pomoc klawiatury `Ctrl+F1` dla `Alt+D`.
Powtórz wyrywkowo w Radiu i Plikach lokalnych.

Oczekiwane: „Pokaż pełny opis podcastu lub odcinka, Alt+D” występuje tylko dla
podcastu lub odcinka. NVDA nigdy nie czyta nazwy klasy, identyfikatora polecenia
ani zrzutu właściwości obiektu.

## Poprzedni zestaw alpha 216

## Nowości alpha 216

### AMC-216-01 — Ulubione: trzy porządki

W dowolnej sesji dodaj do Ulubionych kilka elementów w kolejności innej niż
alfabetyczna. Otwórz `Ctrl+U`, a następnie sprawdź `Alt+1`, `Alt+2` i `Alt+3`.

Oczekiwane: `Alt+1` pokazuje ostatnio dodany element na początku, `Alt+2`
układa alfabetycznie, a `Alt+3` otwiera kolejność własną. Tylko po `Alt+3`
`Alt+strzałka w górę/w dół` przenosi element. NVDA podaje tryb, sesję i
zachowuje fokus na tym samym elemencie.

### AMC-216-02 — Biblioteka Radia i Podcastów

W Bibliotece Radia oraz Podcastów powtórz `Alt+1`, `Alt+2` i `Alt+3`.
Przenieś element w trybie własnym, przełącz widok i wróć.

Oczekiwane: znaczenia klawiszy są identyczne jak w Ulubionych, a własna
kolejność pozostaje zapisana. `Ctrl+L` nadal otwiera Bibliotekę i nie jest
zastępowany przez skrót sortowania.

### AMC-216-03 — wyjątek Plików lokalnych

W lokalnej Bibliotece sprawdź `Alt+1`, `Alt+2`, `Alt+3`, a potem przejdź do
lokalnych Ulubionych i ponów te skróty.

Oczekiwane: w Bibliotece są to nadal Foldery, Wszystkie pliki alfabetycznie i
Kolejność własna. W Ulubionych są to kolejność dodania, alfabet i kolejność
własna, tak jak w pozostałych sesjach.

### AMC-216-04 — listy wyliczane i zwykłe cyfry

Sprawdź cyfry z Altem w Historii, wynikach wyszukiwania, Nowych odcinkach i
widoku Nagrywane. Następnie szybko użyj `Alt+2`, puść Alt i naciśnij zwykłe
`2` albo inną literę nawigacji.

Oczekiwane: listy tymczasowe nie zmieniają sposobu sortowania ani sesji.
Zwykła cyfra pozostaje zwykłą cyfrą i nie jest odczytywana jako `Alt+2`.

### AMC-216-05 — zapis i ponowne dodanie

W Ulubionych wybierz `Alt+3`, ustaw własną kolejność, zamknij bezpiecznie AMC
i uruchom ponownie. Następnie usuń jeden element z Ulubionych, dodaj go znowu
i wybierz `Alt+1`.

Oczekiwane: program pamięta tryb i kolejność własną. Ponownie dodany element
jest najnowszy i pojawia się na początku kolejności dodania.

## Poprzedni zestaw alpha 213

## Nowości alpha 213

### AMC-213-01 — Escape na najwyższym poziomie

W sesji Podcasty przejdź z listy odcinków przez Escape do Biblioteki, a
następnie naciśnij Escape jeszcze raz.

Oczekiwane: program mówi, że jest to najwyższy poziom widoku, ale fokus
pozostaje na aktualnym podcaście na liście. Nie przechodzi do pola „Filtruj
listę”; strzałki i wpisywanie liter nadal od razu obsługują listę.

### AMC-213-02 — pusta skrzynka i F5

Naciśnij `Ctrl+I`, gdy nie ma nowych odcinków, a następnie `F5`.

Oczekiwane: program podaje „Brak nowych odcinków”. `F5` rozpoczyna odświeżenie
wszystkich podcastów z Biblioteki i po zakończeniu podaje liczbę wykrytych
nowych odcinków. `Ctrl+F5` nadal wykonuje pełne odświeżenie z każdego widoku
Podcastów.

### AMC-213-03 — rozpoczęty odcinek pozostaje w skrzynce

Gdy pojawi się nowy odcinek, rozpocznij go ze skrzynki, wróć Escape i ponownie
otwórz `Ctrl+I`.

Oczekiwane: rozpoczęty, lecz nieukończony odcinek nadal jest w skrzynce. Znika
po odtworzeniu do końca.

## Poprzedni zestaw alpha 212

## Nowość alpha 212

### AMC-212-01 — identyczny tytuł i autor podcastu

W Bibliotece Podcastów odszukaj „ZACZYTAJ SIĘ Z RADIEM POZNAŃ” i przejdź na
ten element strzałkami lub literami.

Oczekiwane: NVDA mówi „ZACZYTAJ SIĘ Z RADIEM POZNAŃ, podcast” tylko raz.
Jeżeli tytuł i autor są różne, nadal odczytuje obie informacje zgodnie z
ustawioną kolejnością pól.

## Poprzedni zestaw alpha 211

## Nowości alpha 211

### AMC-211-01 — autor bez znaków praw

W Bibliotece Podcastów przejdź po kilku audycjach Radia PiK i innych kanałach,
których autor rozpoczyna się od `℗`, `©`, `®` albo `™`. Otwórz również
Alt+Enter dla jednej z nich.

Oczekiwane: NVDA czyta zwykłą nazwę autora, na przykład „Polskie Radio PiK”,
bez wypowiadania znaków praw. Naturalne nazwy zawierające `&` pozostają bez
zmian, a w interfejsie nie pojawia się identyfikator techniczny.

### AMC-211-02 — Escape i Backspace na liście odcinków

Otwórz podcast Enterem. Na liście jego odcinków wróć raz przez Escape, a po
ponownym wejściu przez Backspace. Powtórz na playliście albo albumie.

Oczekiwane: oba klawisze wracają dokładnie o jeden poziom. Escape w aktywnym
filtrze najpierw tylko go czyści, w odtwarzaczu wraca do listy, a w polu
edycyjnym, menu i oknie dialogowym zachowuje ich standardową semantykę.

## Poprzedni zestaw alpha 210

## Nowość alpha 210

### AMC-210-01 — prędkość po wyjściu Escape

Uruchom dowolny odcinek Podcastów, zmień prędkość przez `Shift+kropka` co
najmniej raz, zapamiętaj podaną wartość, naciśnij Escape i ponownie otwórz ten
sam odcinek.

Oczekiwane: odcinek rozpoczyna się z ostatnio ustawioną prędkością sesji
Podcasty, a nie z `1,00 razy`.

### AMC-210-02 — prędkość po ponownym uruchomieniu

Pozostaw w Podcastach prędkość inną niż normalna, zamknij AMC w bezpiecznym
momencie, uruchom je ponownie i odtwórz odcinek.

Oczekiwane: zapisana prędkość Podcastów nadal obowiązuje. `Ctrl+kropka`
przywraca `1,00 razy — normalna prędkość` i ta zmiana również jest pamiętana.

## Poprzedni zestaw alpha 209

## Nowość alpha 209

### AMC-209-01 — rozpoznawanie bez automatycznej mowy

W Radiu włącz obserwowanie przez `Shift+S`, a następnie naciśnij
`Ctrl+Alt+Shift+S`, aby wyłączyć oznajmianie. Pozostaw muzyczną stację do
rozpoznania i otwórz historię przez `Ctrl+Alt+S`.

Oczekiwane: AMC potwierdza wyłączenie mowy i przypomina, że rozpoznawanie oraz
historia nadal działają. Automatyczny wynik nie przerywa odsłuchu komunikatem,
ale pojawia się w historii.

### AMC-209-02 — ponowne włączenie i ręczne rozpoznanie

Przy wyłączonym oznajmianiu naciśnij ręczne `S`, a następnie włącz
automatyczne oznajmianie przez `Ctrl+Alt+Shift+S`.

Oczekiwane: ręczne `S` nadal wypowiada wynik, ponieważ jest działaniem na
żądanie. Nowy skrót potwierdza włączenie, a kolejny wynik automatyczny jest
czytany tylko wtedy, gdy AMC jest aktywnym oknem.

### AMC-209-03 — menu, paleta i trwałość

Porównaj stan pozycji w menu Odtwarzanie, menu kontekstowym odtwarzacza,
Ustawieniach i palecie `Ctrl+Shift+K`. Wyłącz oznajmianie, bezpiecznie uruchom
AMC ponownie i sprawdź te miejsca jeszcze raz.

Oczekiwane: wszędzie występują wyłącznie nazwy użytkowe i ten sam stan.
Paleta podaje skrót `Ctrl+Alt+Shift+S`, a ustawienie pozostaje wyłączone po
ponownym uruchomieniu.

## Poprzedni zestaw alpha 208

## Nowości alpha 208

### AMC-208-01 — przewijanie sieciowego odcinka

Uruchom niepobrany odcinek podcastu trwający co najmniej kilka minut. Sprawdź
lewą i prawą strzałkę, cyfry `1`, `5` i `9`, `Ctrl+J` oraz `Ctrl+Shift+J`.
Powtórz jeden skok po wstrzymaniu.

Oczekiwane: każdy skrót zmienia pozycję tego samego odcinka. Materiał nie jest
traktowany jak transmisja na żywo, AMC nie milknie i nie zgłasza błędu.

### AMC-208-02 — bieżący odcinek pozostaje w Kolejce

Dodaj do Kolejki co najmniej dwa odcinki, otwórz `Ctrl+Q` i uruchom pierwszy.
Wróć do Kolejki, a następnie użyj w odtwarzaczu `Page Down`.

Oczekiwane: odtwarzany odcinek nadal jest widoczny w Kolejce. Po przejściu
dalej znika poprzedni, natomiast nowy bieżący odcinek pozostaje widoczny.

### AMC-208-03 — tytuł przed autorem

Wybierz odcinek mający osobnego autora. Porównaj jego wiersz w otwartym
podcaście, Kolejce, Ulubionych, Historii i playliście.

Oczekiwane: każdy wiersz zaczyna się od tytułu odcinka, a dopiero potem podaje
autora, czas i pozostałe skonfigurowane pola.

### AMC-208-04 — trwała nazwa podcastu pod F2

W Bibliotece Podcastów wybierz audycję, naciśnij `F2`, wpisz nazwę własną i
zatwierdź. Odśwież podcast przez `F5`, zamknij AMC i uruchom go ponownie.
Powtórz `F2`, ale tym razem anuluj przez Escape.

Oczekiwane: pole ma jednoznaczną etykietę „Nowa nazwa podcastu”, po zapisaniu
fokus wraca do audycji, odświeżenie RSS i restart zachowują nazwę własną, a
anulowanie niczego nie zmienia.

### AMC-208-05 — regresja Kolejki w Plikach lokalnych

Dodaj dwa krótkie pliki do Kolejki i uruchom pierwszy bezpośrednio z jej widoku.
Sprawdź `Ctrl+Q`, `Page Down`, `Page Up` oraz naturalny koniec drugiego pliku.

Oczekiwane: reguła pozostawiania bieżącej pozycji do zakończenia lub przejścia
dalej jest taka sama jak w Podcastach. Żaden zużyty plik nie odtwarza się drugi
raz, a Page Up nadal może wrócić w historii bieżącej Kolejki.

## Poprzedni zestaw alpha 203

## Nowość alpha 203

### AMC-203-01 — szósta sesja Podcasty

Otwórz listę sesji przez `Ctrl+Shift+S` albo `Ctrl+0` i przejdź strzałkami po
wszystkich pozycjach. Wybierz Podcasty.

Oczekiwane: Podcasty są osobną szóstą sesją, mają wyłącznie prawdziwą pustą
Bibliotekę i nie zawierają utworów demonstracyjnych. NVDA nie odczytuje nazwy
klasy, identyfikatora ani zapisu obiektu.

### AMC-203-02 — widoki pustego modułu

W sesji Podcasty otwórz menu Widok i kolejno wybierz Biblioteka, Nowe odcinki,
Pobrane, Playlisty, Historia odtwarzania, Ulubione i Kolejka.

Oczekiwane: każdy widok otwiera się bez błędu i jednoznacznie podaje nazwę.
Nowe odcinki oraz Pobrane są dostępne tylko w sesji Podcasty. Albumy, Foldery
Biblioteki i Nagrywane nie są w niej pokazywane.

### AMC-203-03 — zapamiętanie sesji i widoku

Pozostaw aktywną sesję Podcasty i widok Nowe odcinki, zamknij AMC, uruchom je
ponownie i otwórz listę sesji.

Oczekiwane: jeżeli w Ustawieniach działa pamiętanie ostatniej sesji, AMC wraca
do Podcastów i tego samego widoku. Pozostałe sesje oraz ich zapisany stan nie
ulegają zmianie.

### AMC-203-04 — polecenia właściwe dla kontekstu

Porównaj menu Plik, Widok, Odtwarzanie i menu kontekstowe pustej listy w
Podcastach z Radiem i Plikami lokalnymi.

Oczekiwane: Podcasty nie pokazują importu stacji, nagrywania Radia, Folderów
Biblioteki, Albumów ani lokalnego cięcia audio. W tej wersji nie ma jeszcze
polecenia dodawania adresu kanału; pojawi się w następnym etapie.

## Poprzedni zestaw alpha 202

## Nowość alpha 202

### AMC-202-01 — ponowienie rzeczywistego cięcia Tyflo

Na kopii pliku `Tyflo - 2026-09-01 20-18-22.mp3` ustaw początek około `53:03,6`,
koniec około `1:13:15,7` i użyj `Ctrl+X`.

Oczekiwane: operacja kończy się powodzeniem. Usunięty zostaje wskazany przedział,
materiał przed nim i po nim pozostaje, a obok znajduje się pełna kopia
`.amc-backup`. AMC nie zgłasza błędnej niezgodności czasu.

### AMC-202-02 — zachowanie fizycznego końca MP3

Po cięciu przejdź w pobliże końca zmienionego pliku i odsłuchaj ostatnią minutę.
Porównaj ją z końcem kopii `.amc-backup`.

Oczekiwane: końcowa treść jest zachowana. AMC nie skraca pliku o dodatkowe
sekundy wynikające z niedokładnego nagłówka czasu.

### AMC-202-03 — Ctrl+1, Ctrl+2 i niezależne nagrywanie

W Radiu rozpocznij ręczne nagranie, otwórz odtwarzacz, naciśnij `Ctrl+1`, a
następnie wróć do Radia jego przypisanym skrótem sesji.

Oczekiwane: przy domyślnie włączonym ustawieniu „Wstrzymuj odtwarzanie po
wyjściu z odtwarzacza” odsłuch Radia jest wstrzymany i przycisk mówi „Odtwórz”.
Nagranie w tle nadal trwa. Po wyłączeniu tej opcji sam wybór innej sesji nie
wstrzymuje odsłuchu.

### AMC-202-04 — T i Shift+T bez zmiany znaczeń

Sprawdź oba klawisze w ręcznym nagrywaniu Radia i w lokalnym odtwarzaczu.

Oczekiwane: `T` dzieli ręczne nagranie Radia. `Shift+T` nie tworzy kopii ani
części nagrania; w lokalnym torze obsługującym przetwarzanie przełącza łagodne
przejścia między utworami.

## Poprzedni zestaw alpha 201

## Nowość alpha 201

### AMC-201-01 — pojedyncze T i dalsze nagrywanie

Rozpocznij ręczne nagrywanie stacji, odczekaj co najmniej kilkanaście sekund i
naciśnij `T` jeden raz.

Oczekiwane: AMC zapisuje dotychczasową część i od razu rozpoczyna kolejną.
Stacja pozostaje na liście Nagrywane, a nagranie nie kończy się.

### AMC-201-02 — szybkie podwójne T

Podczas dłuższego ręcznego nagrania naciśnij `T` dwa razy szybko.

Oczekiwane: pierwszy klawisz rozpoczyna nową część. Drugi nie zamyka świeżego
pliku i nie zatrzymuje nagrania; NVDA mówi, że ponowne `T` zostało pominięte.

### AMC-201-03 — zakończona część w Bibliotece

Po pierwszym `T`, gdy następna część nadal się nagrywa, przejdź do Plików
lokalnych i wyszukaj pierwszą zakończoną część.

Oczekiwane: pierwsza część jest już widoczna i możliwa do odtworzenia. Nie
trzeba kończyć drugiej części ani ponownie uruchamiać AMC.

### AMC-201-04 — T i Shift+T

W Radiu sprawdź Pomoc klawiszy oraz oba klawisze podczas nagrywania.

Oczekiwane: wyłącznie `T` dzieli ręczne nagranie. `Shift+T` nie tworzy części
i zachowuje swoje dotychczasowe znaczenie w obsługiwanych odtwarzaczach.

## Poprzedni zestaw alpha 200

## Nowość alpha 200

### AMC-200-01 — potwierdzenie i prawidłowe usunięcie

Na kopii lokalnego pliku MP3 lub WAV ustaw `I` i `O`, naciśnij `Ctrl+X` i
najpierw wybierz „Nie”. Powtórz i wybierz „Tak”.

Oczekiwane: odpowiedź domyślna nie zmienia pliku. Po potwierdzeniu odtwarzanie
zatrzymuje się, zaznaczony przedział znika, a reszta materiału pozostaje przed
i po cięciu. Fokus wraca do odtwarzacza, punkty `I` i `O` są wyczyszczone, a
pozycja znajduje się w pobliżu początku usuniętego fragmentu.

### AMC-200-02 — kopia bezpieczeństwa i błąd bez utraty danych

Po udanej operacji sprawdź folder źródłowy. Następnie spróbuj `Ctrl+X` na pliku
tylko do odczytu albo na niepobranym pliku chmurowym.

Oczekiwane: obok zmienionego audio istnieje jeden plik z końcówką
`.amc-backup`, zawierający pełny wcześniejszy oryginał. Plik niedostępny lokalnie
lub tylko do odczytu nie jest pobierany ani zmieniany; AMC podaje przyczynę.

### AMC-200-03 — rozdzielenie X, Ctrl+X, Shift+X i Delete

W odtwarzaczu sprawdź Pomoc klawiszy, menu Odtwarzanie i menu kontekstowe.
Następnie sprawdź te klawisze na zwykłej liście.

Oczekiwane: `X` zapisuje zaznaczony fragment jako nowy plik, `Ctrl+X` usuwa go
z oryginału dopiero po potwierdzeniu, `Shift+X` usuwa same punkty, a `Delete`
nie wykonuje cięcia audio. Polecenia edycji nie działają przypadkowo poza
odtwarzaczem lokalnego pliku.

### AMC-200-04 — Zakładki globalne i bieżący plik

Dodaj zakładki do dwóch plików. Otwórz `Ctrl+B`, a następnie wróć do jednego
pliku i użyj `Shift+Page Up` oraz `Shift+Page Down`.

Oczekiwane: `Ctrl+B` pokazuje wspólny indeks obu plików, natomiast skróty w
odtwarzaczu przechodzą wyłącznie po zakładkach bieżącego pliku. Cięcie nie usuwa
globalnej listy Zakładek.

## Poprzedni zestaw alpha 199

## Nowość alpha 199

### AMC-199-01 — osobne granice dla dwóch plików

W pierwszym lokalnym pliku ustaw `I` i `O`, następnie przejdź `Page Down` do
drugiego pliku i ustaw inne granice. Wracaj pomiędzy plikami i sprawdzaj punkty
przez `Shift+I`, `Shift+O`, `Alt+Page Up` i `Alt+Page Down`.

Oczekiwane: każdy plik odzyskuje wyłącznie własny początek i koniec. Punkty
pierwszego pliku nie pojawiają się w drugim i odwrotnie.

### AMC-199-02 — ponowne uruchomienie i usuwanie

Zamknij AMC z zaznaczeniami w dwóch plikach i uruchom ponownie. Sprawdź oba,
następnie w jednym naciśnij `Shift+X`, ponownie zmień plik i uruchom program.

Oczekiwane: obie pary przetrwały pierwsze ponowne uruchomienie. `Shift+X`
usunął tylko parę bieżącego pliku i zrobił to trwale; zaznaczenie drugiego pliku
pozostało. Żaden plik źródłowy nie został zmieniony.

## Poprzedni zestaw alpha 198

## Nowość alpha 198

### AMC-198-01 — poprzednia i następna granica fragmentu

W lokalnym pliku ustaw początek klawiszem `I` i koniec klawiszem `O`. Ustaw
pozycję przed początkiem, pomiędzy granicami oraz za końcem i sprawdź
`Alt+Page Up` oraz `Alt+Page Down`, także podczas odtwarzania.

Oczekiwane: `Alt+Page Up` przechodzi tylko do najbliższej wcześniejszej granicy,
a `Alt+Page Down` tylko do najbliższej późniejszej. Na początku nie ma
poprzedniej, a na końcu następnej granicy; nawigacja nie zapętla fragmentu.
`Shift+I` i `Shift+O` nadal bezpośrednio wybierają początek i koniec. Po
wyłączeniu komunikatów przewijania skoki nie są oznajmiane.

## Poprzedni zestaw alpha 197

## Nowość alpha 197

### AMC-197-01 — stan i ręczna kontrola FFmpeg

Otwórz Ustawienia, kartę Aktualizacje, a następnie menu Pomoc i wybierz
„Sprawdź aktualizacje i składniki”.

Oczekiwane: Ustawienia podają użytkową wersję FFmpeg albo informację, że nie
został jeszcze zainstalowany. Ręczna kontrola nie blokuje NVDA i kończy się
jednym komunikatem o zainstalowanej, aktualnej albo niedostępnej wersji. Nie
pojawiają się nazwy klas, sumy ani techniczny zrzut obiektu.

### AMC-197-02 — automatyczna instalacja i ponowne uruchomienie

Pozostaw zaznaczone automatyczne sprawdzanie i pobieranie FFmpeg. Uruchom AMC,
odczekaj na zakończenie ewentualnego pobierania, zamknij program i uruchom go
ponownie.

Oczekiwane: pierwsza instalacja odbywa się w tle, a kolejne uruchomienie od razu
widzi zweryfikowany składnik. Program nie pobiera ponownie tej samej wersji i
nie zmienia Biblioteki, ustawień ani pozycji odtwarzania.

### AMC-197-03 — trzy sposoby zapisu fragmentu

W lokalnym MP3 zaznacz fragment klawiszami `I` i `O`, naciśnij `X` i sprawdź
kolejno zapis bez konwersji, FLAC oraz WAV.

Oczekiwane: okno pokazuje wszystkie trzy czytelne warianty. Każdy tworzy nowy
odtwarzalny plik, źródło pozostaje bez zmian. Bez konwersji może dopasować
granicę do ramki kodeka, FLAC i WAV zachowują dokładny fragment.

### AMC-197-04 — przechodzenie do punktów cięcia

Po ustawieniu początku i końca odejdź od nich na osi czasu. Naciśnij
`Shift+I`, potem `Shift+O`, również podczas odtwarzania.

Oczekiwane: `Shift+I` przechodzi dokładnie do początku, a `Shift+O` do końca.
Punkty się nie przesuwają i można wielokrotnie porównywać granice przed
zapisem. Po wyłączeniu komunikatów przewijania przejścia nie są oznajmiane.

### AMC-197-05 — brak sieci nie wyłącza FFmpeg

Po poprawnej instalacji uruchom ręczne sprawdzanie bez połączenia z internetem,
a następnie spróbuj zapisać fragment FLAC.

Oczekiwane: kontrola zgłasza brak możliwości aktualizacji, lecz nie usuwa
dotychczasowego FFmpeg. Eksport FLAC nadal działa, podobnie jak odtwarzanie i
nagrywanie korzystające z poprzednio zweryfikowanego składnika.

## Poprzedni zestaw alpha 196

## Nowość alpha 196

### AMC-196-01 — zaznaczenie fragmentu

Otwórz dłuższy lokalny plik, przejdź w odtwarzaczu do wybranego miejsca i
naciśnij `I`. Przejdź dalej, naciśnij `O`, a następnie `X`.

Oczekiwane: NVDA podaje czas początku i końca. Okno zapisu mówi nazwę pliku,
oba czasy i długość fragmentu. Fokus zaczyna na czytelnie opisanym sposobie
zapisu; nie pojawia się techniczna nazwa klasy ani zapis z klamrami.

### AMC-196-02 — dokładny WAV i nienaruszony oryginał

Wybierz WAV, zapisz wynik pod nową nazwą i odtwórz początek oraz koniec obu
plików.

Oczekiwane: nowy plik obejmuje zaznaczony fragment, a oryginał ma niezmienioną
nazwę, długość, treść i położenie. Zapis nie dodaje wyniku do Biblioteki bez
osobnej decyzji użytkownika.

### AMC-196-03 — błędna kolejność punktów

Po `I` cofnij odtwarzanie i spróbuj ustawić `O`, a następnie przejdź do innego
pliku i spróbuj użyć wcześniejszego zaznaczenia.

Oczekiwane: program nie tworzy błędnego fragmentu. Wyjaśnia, że koniec musi być
po początku albo że punkty muszą należeć do tego samego pliku.

### AMC-196-04 — anulowanie i bezpieczny plik wynikowy

Rozpocznij zapis długiego fragmentu WAV i użyj przycisku Przerwij. Powtórz
operację, wybierając istniejącą nazwę, lecz odrzuć systemowe potwierdzenie
zastąpienia.

Oczekiwane: anulowanie nie zmienia źródła ani istniejącego pliku docelowego i
nie pozostawia widocznego pliku częściowego. Fokus wraca do wyboru sposobu
zapisu albo do odtwarzacza po zamknięciu okna.

### AMC-196-05 — dostępność menu i skrótów

Sprawdź `I`, `O`, `X` i `Shift+X` z klawiatury, Pomocy klawiszy, menu
Odtwarzanie oraz menu kontekstowego odtwarzacza.

Oczekiwane: polecenia istnieją tylko dla lokalnego utworu w odtwarzaczu,
pojawiają się pojedynczo i mają te same nazwy. W Radiu, na liście oraz w sesjach
usług nie wykonują przypadkowej operacji.

### AMC-196-06 — opcjonalne sposoby z FFmpeg

Jeżeli okno pokazuje zapis bez konwersji i FLAC, sprawdź oba na MP3 lub innym
skompresowanym nagraniu.

Oczekiwane: bez konwersji zachowuje kodek bez ponownego kodowania, z możliwym
dopasowaniem granicy do ramki. FLAC daje dokładny, bezstratny fragment. Jeżeli
FFmpeg nie jest dostępny, obie opcje są ukryte, a WAV nadal działa.

## Poprzedni zestaw alpha 195

## Poprawka alpha 195

### AMC-195-01 — Page Down z listy Ctrl+H

Otwórz Historię przez `Ctrl+H`, zapamiętaj trzy kolejne widoczne pozycje i
uruchom środkową. W odtwarzaczu naciśnij `Page Up`, a następnie dwukrotnie
`Page Down`.

Oczekiwane: program przechodzi wyłącznie między pozycjami widocznymi w
Historii, zgodnie z jej kolejnością od najnowszej. Nie wybiera alfabetycznego
sąsiada z Biblioteki, jeżeli nie ma go w Historii.

### AMC-195-02 — kontekst Ulubionych i folderu

Uruchom element z Ulubionych i sprawdź `Page Up/Page Down`, a następnie zrób to
samo z plikiem otwartym z konkretnego folderu.

Oczekiwane: w pierwszym przypadku nawigacja pozostaje w Ulubionych, a w drugim
w bieżącym folderze. Samo wcześniejsze otwarcie Historii nie wpływa już na te
dwa konteksty.

### AMC-195-03 — chwilowa Historia w odtwarzaczu

Uruchom plik z folderu, w odtwarzaczu przejdź do starszego wpisu przez
`Alt+strzałka w dół`, a następnie użyj `Page Up/Page Down`.

Oczekiwane: `Alt+strzałka` pozwala chwilowo przejrzeć Historię, ale nie zmienia
zapamiętanego kontekstu folderu dla `Page Up/Page Down`.

## Poprzedni zestaw alpha 194

## Poprawka alpha 194

### AMC-194-01 — Spacja i timeshift podczas nagrywania

Uruchom stację, rozpocznij jej nagrywanie, odczekaj co najmniej kilkanaście
sekund i naciśnij Spację. Po chwili naciśnij Spację ponownie.

Oczekiwane: pierwsza Spacja zatrzymuje tylko słyszalny odsłuch. Nagrywanie
trwa bez przerwy. Druga Spacja wznawia dźwięk od miejsca pauzy, dlatego pasek
podaje opóźnienie względem transmisji. Nie przeskakuje samoczynnie na żywo.

### AMC-194-02 — przewijanie i jawny powrót na żywo

Podczas dalszego nagrywania użyj skrótów przewijania radia, a następnie `End`.

Oczekiwane: przewijanie działa i nie zatrzymuje nagrania. `End` mówi „Na żywo”
i zeruje opóźnienie odsłuchu, lecz nadal nie kończy zapisu.

### AMC-194-03 — Shazam bez krótkiej przerwy odsłuchu

Włącz automatyczne rozpoznawanie utworów i słuchaj stacji przez czas
wystarczający do co najmniej dwóch prób rozpoznania. Jedną z prób wykonaj też
ręcznie.

Oczekiwane: w chwili rozpoznawania dźwięk nie urywa się nawet na pół sekundy,
fokus i klawiatura pozostają płynne, a automatyczny wynik jest oznajmiany tylko
zgodnie z ustawieniem programu.

### AMC-194-04 — nagranie w czasie rozpoznawania

Nagraj co najmniej minutę stacji przy włączonym rozpoznawaniu, a następnie
odsłuchaj gotowy plik w okolicy chwili, w której pojawił się wynik Shazam.

Oczekiwane: plik nie zawiera przerwy ani ucięcia powstałego podczas
rozpoznawania.

## Poprzedni zestaw alpha 193

## Poprawka alpha 192

### AMC-192-01 — zwięzła nawigacja po historii odtwarzania

W Plikach lokalnych odtwórz kolejno co najmniej trzy różne pliki. Pozostając w
odtwarzaczu, użyj kilka razy `Alt+strzałka w dół`, a potem
`Alt+strzałka w górę`.

Oczekiwane: po każdym udanym przejściu NVDA mówi wyłącznie nazwę uruchomionego
pliku. Nie dodaje przed nią słowa „Historia”. Dopiero próba przejścia poza
początek lub koniec wyjaśnia, że nie ma starszego albo nowszego elementu w
historii.

### AMC-192-02 — trzy niezależne działania w Radiu

Uruchom stację oraz jej nagrywanie. Kolejno sprawdź Spację, `Ctrl+M` oraz
`Shift+Spacja`, za każdym razem wykonując skrót ponownie w celu wznowienia.

Oczekiwane: Spacja pauzuje tylko słyszalny odsłuch, a odbiór, bufor i nagranie
trwają. `Ctrl+M` wycisza dźwięk bez pauzy transportu, odbioru i nagrania.
`Shift+Spacja` pauzuje wyłącznie zapis nagrania, a odsłuch nadal gra. Przy
formacie Oryginalnym program jasno informuje, że pauza zapisu jest niedostępna.

### AMC-192-03 — najprostszy wariant ciszy podczas pauzy nagrania

Wstrzymaj zapis przez `Shift+Spacja`, a następnie wycisz odsłuch przez
`Ctrl+M`. Po chwili przywróć dźwięk `Ctrl+M` i wznów zapis przez
`Shift+Spacja`.

Oczekiwane: stacja pozostaje połączona przez cały test. Nie trzeba jej
zatrzymywać ani uruchamiać ponownie, a każde polecenie zmienia tylko swoją
warstwę.

## Poprzedni zestaw alpha 191

## Poprawka alpha 191

### AMC-191-01 — migracja starszego zapisu

Uruchom program, który wcześniej miał zapisany domyślny prefiks, następnie
otwórz **Ustawienia > Ogólne**.

Oczekiwane: ustawienia otwierają się bez błędu, a pole prefiksu podaje
`Ctrl+Alt+Windows+F12`. Starszy zapis z myślnikami nie jest czytany ani
odrzucany jako niedozwolony klawisz.

### AMC-191-02 — zapis Plusa numerycznego

Wybierz **Zmień prefiks…**, naciśnij sam Plus numeryczny, zapisz okno zmiany i
całe Ustawienia. Otwórz Ustawienia ponownie.

Oczekiwane: NVDA nie milknie, program nie zawiesza się, a zapisany prefiks jest
czytany jako „Plus numeryczny”. Ponowne otwarcie potwierdza, że zmiana
przetrwała zapis. Na końcu można przywrócić prefiks domyślny.

### AMC-191-03 — Pomoc klawiszy nie wyłącza prefiksu

Naciśnij `Ctrl+F1`, sprawdź kilka klawiszy, po czym ponownie naciśnij
`Ctrl+F1`. Następnie wywołaj skonfigurowany prefiks poza oknem AMC.

Oczekiwane: Pomoc opisuje klawisze bez wykonywania poleceń. Po jej wyłączeniu
prefiks zostaje ponownie zarejestrowany, nie pojawia się komunikat o
niedozwolonym klawiszu, a NVDA działa bez restartu.

### AMC-191-04 — konflikt nie usuwa poprzedniej wartości

Spróbuj zapisać kombinację zajętą przez system albo inny program.

Oczekiwane: AMC pozostawia Ustawienia otwarte, podaje zrozumiały komunikat i
nie zastępuje poprzednio działającego prefiksu.

## Poprzedni zestaw alpha 190

## Poprawka alpha 190

### AMC-190-01 — Nagrywane z Ulubionych

W Radiu otwórz Ulubione przez `Ctrl+U`, ustaw fokus na rozpoznawalnej stacji i
naciśnij `Alt+R`. Następnie naciśnij Escape. Powtórz z Biblioteki i użyj
Backspace zamiast Escape.

Oczekiwane: `Alt+R` pokazuje tylko aktualnie nagrywane stacje. Escape i
Backspace wracają dokładnie do wcześniejszego widoku oraz zaznaczenia. Nie
trzeba używać `Alt+1`, które zawsze oznacza Wszystkie stacje.

### AMC-190-02 — wolne Alt+2 w Radiu

Na zwykłej liście Radia naciśnij `Alt+2`, a potem `Alt+1`.

Oczekiwane: `Alt+2` nie zmienia widoku i podaje, że nie ma jeszcze znaczenia w
Radiu. `Alt+1` świadomie otwiera Wszystkie stacje. W Plikach lokalnych
`Alt+2` nadal pokazuje Wszystkie pliki alfabetycznie.

### AMC-190-03 — Spacja bez nagrywania

Uruchom stację bez jej nagrywania, odczekaj kilkanaście sekund, naciśnij
Spację, zaczekaj ponownie i wznów Spacją. Odczytaj pasek oraz użyj End.

Oczekiwane: podczas pauzy odbiór i bufor rosną. Wznowienie kontynuuje od
zatrzymanego punktu, więc pasek pokazuje opóźnienie względem transmisji. End
wraca na żywo.

### AMC-190-04 — Spacja podczas nagrywania słuchanej stacji

Uruchom i nagrywaj tę samą stację. Wstrzymaj sam odsłuch zwykłą Spacją,
odczekaj, a następnie wznów. Osobno użyj `Shift+Spacja` na nagraniu.

Oczekiwane: zwykła Spacja nie robi luki w nagraniu. Wznowienie odsłuchu wraca
od razu na żywo, ponieważ timeshift tej stacji jest podczas nagrywania
zablokowany. Dopiero `Shift+Spacja` wstrzymuje zapis w obsługiwanym formacie.

## Poprzedni zestaw alpha 189

## Poprawka alpha 189

### AMC-189-01 — przechwycenie Plusa numerycznego

Otwórz **Ustawienia > Ogólne > Prefiks globalny**, wybierz **Zmień
prefiks…** i naciśnij sam Plus numeryczny. Odczekaj kilka sekund, użyj Tabu,
strzałek i zwykłego Entera, ale na tym etapie możesz anulować bez zapisywania.

Oczekiwane: NVDA mówi „Plus numeryczny”, nadal czyta wszystkie kontrolki i
nie wymaga restartu. AMC nie zawiesza się ani nie zamyka. Anulowanie pozostawia
poprzedni prefiks.

### AMC-189-02 — zapis i użycie Plusa

Powtórz zmianę, naciśnij zwykły Enter na przycisku Zapisz, a następnie zapisz
Ustawienia. W innym programie naciśnij Plus numeryczny i po nim polecenie
warstwy, na przykład Spację. Po wygaśnięciu warstwy sprawdź zwykłe klawisze
NVDA.

Oczekiwane: Plus uruchamia prefiks jeden raz, polecenie wykonuje się, a
klawiatura wraca do zwykłej pracy. NVDA nie milknie. Jeśli rejestracja skrótu
nie powiedzie się, AMC pozostawia poprzedni prefiks i podaje komunikat zamiast
zawieszenia.

### AMC-189-03 — powtarzanie i anulowanie

Otwórz okno zmiany kilka razy. Przechwyć kolejno Plus numeryczny, Minus
numeryczny i ponownie Plus. Część prób anuluj Escape, a jedną zatwierdź.

Oczekiwane: każda próba ma użytkową nazwę, fokus przechodzi na Zapisz, Escape
bezpiecznie zamyka okno, a szybkie powtarzanie nie pozostawia przejętego
klawisza ani aktywnej warstwy.

## Poprzedni zestaw alpha 188

## Nowości alpha 188

### AMC-188-01 — zakres tylko dla słuchanej stacji

W **Ustawienia > Radio i nagrywanie** włącz automatyczne rozpoznawanie i w
polu **Rozpoznawaj automatycznie** wybierz **Tylko aktualnie odtwarzana
stacja**. Uruchom stację muzyczną, odczekaj co najmniej minutę, a następnie
sprawdź historię pod `Ctrl+Alt+S`.

Oczekiwane: rozpoznawany jest dźwięk rzeczywiście słuchany, również z bieżącej
pozycji timeshiftu. `Shift+S` wyłącza i ponownie włącza obserwowanie, ale nie
zmienia zapisanego zakresu. Ręczne `S` nadal dotyczy tej stacji.

### AMC-188-02 — tylko nagrania w tle

Wybierz **Tylko stacje nagrywane w tle**. Rozpocznij `Ctrl+Alt+R` nagrywanie
jednej stacji, a słuchaj innej. Pozostaw oba źródła na co najmniej minutę i
otwórz historię.

Oczekiwane: nowe automatyczne wpisy pochodzą ze stacji nagrywanej, a nie ze
stacji słuchanej. Nagranie działa bez przerwy i bez drugiego połączenia
widocznego jako osobne odtwarzanie. Ręczne `S` mimo tego ustawienia rozpoznaje
stację słuchaną.

### AMC-188-03 — słuchana i kilka nagrywanych stacji

Wybierz **Aktualnie odtwarzana stacja i wszystkie stacje nagrywane w tle**.
Słuchaj jednej stacji i nagrywaj w tle co najmniej dwie. Jeżeli jedną z nich
jest stacja słuchana, pozostaw taki układ przez kilka cykli.

Oczekiwane: historia przyjmuje wyniki ze wszystkich rzeczywiście odbieranych
źródeł. Ta sama stacja słuchana i nagrywana nie tworzy podwójnego zapytania w
jednym cyklu. Program, nagrania, fokus i skróty pozostają responsywne.

### AMC-188-04 — pauza nagrania i trwałość ustawienia

W zakresie obejmującym nagrania wstrzymaj jedną nagrywaną stację przez
`Shift+Spacja`. Zapisz Ustawienia, zamknij AMC dopiero po bezpiecznym
zakończeniu nagrań, uruchom je ponownie i odczytaj zakres.

Oczekiwane: wstrzymane nagranie nie dostarcza nowych automatycznych wyników.
Po wznowieniu może znów być rozpoznawane. Zakres i stan `Shift+S` są zachowane
po ponownym uruchomieniu.

### AMC-188-05 — jedna historia i filtr stacji

Otwórz `Ctrl+Alt+S`. Tabem przejdź do pola **Pokaż wpisy**, wybierz kolejno
**Wszystkie stacje** i dwie konkretne stacje. W każdym widoku zaznacz wpisy,
sprawdź `Ctrl+C`, `Ctrl+Shift+C`, menu kontekstowe i powrót do listy.

Oczekiwane: NVDA czyta wyłącznie użytkowe nazwy filtra i stacji. Lista pokazuje
tylko wybraną stację, a powrót do wszystkich przywraca wspólną historię.
Filtrowanie nie usuwa danych; kopiowanie i działania dotyczą widocznego
zaznaczenia.

### AMC-188-06 — niezależne oznajmianie

Na karcie **Komunikaty** wyłącz oznajmianie automatycznie rozpoznanych utworów,
pozostaw `Shift+S` włączone i użyj zakresu obejmującego nagrania w tle.

Oczekiwane: wyniki ze wszystkich wybranych źródeł nadal trafiają do historii,
ale nie są wypowiadane. Po ponownym włączeniu komunikat zawiera nazwę stacji i
pojawia się tylko w aktywnym oknie AMC.

## Poprzedni zestaw alpha 187

## Nowości alpha 187

### AMC-187-01 — sam Plus numeryczny

Otwórz **Ustawienia > Ogólne > Prefiks globalny**, wybierz **Zmień
prefiks…** i naciśnij sam Plus na bloku numerycznym, bez żadnego
modyfikatora. Potwierdź zwykłym Enterem, zapisz Ustawienia, przejdź do innego
programu i użyj Plusa numerycznego, a następnie polecenia warstwy, na przykład
Spacji.

Oczekiwane: okno i pole mówią „Plus numeryczny”. Prefiks zapisuje się i
działa globalnie bez Control, Alt, Shift ani Windows. Naciśnięcie przejętego
Plusa nie wpisuje równocześnie znaku do programu pod fokusem.

### AMC-187-02 — pozostałe działania i Num Lock

Kolejno przechwyć i zapisz sam Minus numeryczny, Gwiazdkę numeryczną,
Ukośnik numeryczny, kropkę numeryczną oraz Num Lock. Po każdym zapisie sprawdź
uruchomienie warstwy prefiksowej poza AMC.

Oczekiwane: każdy klawisz ma własną użytkową nazwę i może działać bez
modyfikatorów. Poprzednia wartość jest zastępowana w całości, bez ręcznego
kasowania i bez technicznych nazw odczytywanych przez NVDA.

### AMC-187-03 — Insert numeryczny a zwykły Insert

Wyłącz Num Lock. Ustaw klawisz `0` bloku numerycznego jako prefiks. AMC
powinien odczytać go jako „Insert numeryczny”. Zapisz ustawienie i porównaj
działanie tego klawisza ze zwykłym Insertem z osobnego bloku nawigacyjnego.
Powtórz porównanie dla kropki numerycznej odczytywanej jako „Delete
numeryczny” i zwykłego Delete.

Oczekiwane: tylko fizyczny klawisz bloku numerycznego uruchamia prefiks.
Zwykły Insert albo Delete nie jest przejmowany. Fokus i NVDA zachowują się
normalnie po użyciu klawisza z osobnego bloku.

### AMC-187-04 — cyfry i kombinacje z modyfikatorami

Włącz Num Lock i sprawdź jako prefiks samą cyfrę numeryczną, na przykład
`7 numeryczny`. Następnie ustaw `Ctrl+Minus numeryczny` i na końcu przywróć
wartość domyślną.

Oczekiwane: cyfra numeryczna pozostaje odrębna od cyfry górnego rzędu, a
modyfikatory są opcjonalne, lecz nadal poprawnie rejestrowane. Przycisk
przywracania ustawia `Ctrl+Alt+Windows+F12`.

## Poprzedni zestaw alpha 180

## Nowości alpha 180

### AMC-180-01 — Trwała opcja w Ustawieniach

Otwórz **Ustawienia > Radio i nagrywanie**, włącz **Automatycznie obserwuj i
rozpoznawaj utwory podczas odtwarzania radia** i zapisz. Zamknij AMC, uruchom
je ponownie i jeszcze raz sprawdź to pole.

Oczekiwane: pole jest łatwo dostępne Tabem, NVDA czyta wyłącznie jego użytkową
nazwę i nadal podaje stan „zaznaczone”. Nie trzeba włączać obserwowania po
każdym uruchomieniu programu.

### AMC-180-02 — Shift+S zapisuje ten sam stan

W odtwarzaczu Radia naciśnij `Shift+S`, sprawdź pozycję obserwowania w menu
**Odtwarzanie** i zamknij program. Po ponownym uruchomieniu sprawdź pole w
Ustawieniach. Powtórz próbę, przywracając poprzedni stan przez `Shift+S`.

Oczekiwane: skrót potwierdza, że nowy stan został zapamiętany. Menu,
Ustawienia i kolejne uruchomienie zawsze pokazują zgodnie tę samą wartość.
Obserwowanie można włączyć także przed uruchomieniem stacji; zacznie działać,
gdy stacja wystartuje.

### AMC-180-03 — Pierwsze rozpoznanie bez minutowego oczekiwania

Pozostaw obserwowanie włączone, uruchom stację nadającą wyraźny utwór i
zaczekaj. Następnie przełącz się na inną stację muzyczną i powtórz próbę.

Oczekiwane: AMC zbiera dźwięk przez około sześć sekund i wtedy rozpoczyna
pierwsze zapytanie; do tego dochodzi czas odpowiedzi sieciowej. Jeżeli pierwsza
próbka nie daje wyniku, następna próba jest planowana po około 15 sekundach,
a nie dopiero po kolejnej pełnej minucie. Znaleziony utwór trafia do
`Ctrl+Alt+S`.

### AMC-180-04 — Oznajmianie pozostaje niezależne

Na karcie **Komunikaty** wyłącz **Oznajmiaj automatycznie rozpoznane
utwory**, pozostawiając obserwowanie włączone w ustawieniach Radia. Odtwarzaj
muzykę, a po chwili otwórz historię `Ctrl+Alt+S`.

Oczekiwane: AMC nie przerywa pracy komunikatem, ale nadal wykonuje
rozpoznawanie i zapisuje wynik. Ponowne włączenie oznajmiania nie zmienia stanu
samego obserwowania.

## Poprzedni zestaw alpha 179

## Nowości alpha 179

### AMC-179-01 — Ctrl+Enter numeryczny

Otwórz **Ustawienia > Ogólne**, przejdź do grupy **Prefiks globalny** i
uaktywnij **Zmień prefiks…**. Naciśnij lewy lub prawy Ctrl razem z Enterem
na klawiaturze numerycznej, a następnie zwykły Enter i zapisz Ustawienia.

Oczekiwane: okno mówi „Ctrl+Enter numeryczny”, a nie zwykłe „Ctrl+Enter”.
Po zapisaniu ta kombinacja włącza warstwę prefiksową także poza oknem AMC.
Zwykły Ctrl+Enter nie uruchamia prefiksu.

### AMC-179-02 — Całkowite zastąpienie poprzedniej kombinacji

Ponownie wybierz **Zmień prefiks…**. Nie kasuj żadnego tekstu. Naciśnij
`Ctrl+Alt+Shift+F12`, potwierdź zwykłym Enterem i zapisz Ustawienia.

Oczekiwane: nowy skrót zastępuje cały stary; nie powstaje połączony tekst z
dwóch kombinacji. NVDA najpierw podaje obecny prefiks, potem nowy i instrukcję
potwierdzenia. Nie odczytuje nazwy klasy ani identyfikatora polecenia.

### AMC-179-03 — Zajęta kombinacja nie niszczy działającego prefiksu

Jeśli masz kombinację globalną zajętą przez NVDA, Windows albo inny program,
spróbuj ustawić ją jako prefiks AMC i wybierz **Zapisz**.

Oczekiwane: Ustawienia nie zamykają się. Komunikat mówi, że kombinacja jest
już używana, a wcześniej działający prefiks nadal włącza warstwę prefiksową.
Po wybraniu innej wolnej kombinacji zapis przebiega normalnie.

### AMC-179-04 — Przywrócenie wartości domyślnej

W grupie prefiksu wybierz **Przywróć domyślny**, zapisz Ustawienia i sprawdź
skrót `Ctrl+Alt+Windows+F12`.

Oczekiwane: pole tylko do odczytu i komunikat stanu podają dokładnie tę
kombinację. Nie trzeba ręcznie zaznaczać ani usuwać poprzedniej wartości.

## Poprzedni zestaw alpha 178

## Nowości alpha 178

### AMC-178-01 — Liczby w Shift+R zastępują poprzednią wartość

Otwórz `Shift+R`. Tabem wejdź do pola długości nagrania, które ma na przykład
wartość `60`, i od razu wpisz `5`. Włącz dzielenie na części, wejdź Tabem do
pola długości części i wpisz `2`.

Oczekiwane: pierwsze pole zawiera `5`, a drugie `2`; nie powstaje `605` ani
`302`. NVDA informuje o zaznaczonej poprzedniej wartości po wejściu. Strzałki
w górę i w dół nadal zwiększają lub zmniejszają gotową liczbę.

### AMC-178-02 — Zwykłe pola tekstowe w całym AMC

Sprawdź kolejno istniejącą nazwę stacji pod `F2`, adres strumienia, szablon
nazwy pliku harmonogramu oraz wartość czasu prefiksu w Ustawieniach. Do
każdego pola wejdź Tabem i zacznij pisać bez ręcznego kasowania.

Oczekiwane: pierwsza litera lub cyfra zastępuje całą starą wartość. Jeśli po
wejściu najpierw naciśniesz strzałkę, zaznaczenie znika i można poprawić tylko
fragment. Pole tylko do odczytu nie zmienia się.

### AMC-178-03 — Mysz oraz data i godzina

Kliknij myszą w środek istniejącego tekstu i wpisz znak. W `Shift+R` przejdź
do daty i godziny, wybierz część strzałkami lewo lub prawo i wpisz nowe cyfry.

Oczekiwane: mysz ustawia kursor w klikniętym miejscu zamiast zaznaczać całość.
W dacie i godzinie cyfry zastępują wybrany dzień, miesiąc, rok, godzinę albo
minutę; pozostałe części nie są kasowane.

## Poprzedni zestaw alpha 177

## Nowości alpha 177

### AMC-177-01 — Spacja przełącza wybrany harmonogram

Otwórz Radio i `Ctrl+Shift+H`. Strzałkami wybierz włączony harmonogram,
naciśnij Spację, a następnie ponownie Spację.

Oczekiwane: pierwsza Spacja wyłącza dokładnie wybrany plan, druga go włącza.
Edytor się nie otwiera. NVDA za każdym razem podaje nazwę stacji, termin oraz
„wyłączony” albo „włączony”. Nie powtarza zamiast wyniku samej instrukcji
„Zaplanowane nagrania radia. Strzałki wybierają plan”. Fokus pozostaje na tym
samym wierszu i można od razu przejść strzałką do następnego harmonogramu.

### AMC-177-02 — Zapis i anulowanie wielu zmian

Wyłącz Spacją dwa różne plany, wybierz **Zapisz**, ponownie otwórz listę i
sprawdź ich stan. Następnie włącz jeden z nich, wybierz **Anuluj** i jeszcze
raz otwórz listę.

Oczekiwane: po Zapisz oba harmonogramy pozostają wyłączone. Zmiana wykonana
przed Anuluj nie zostaje zapamiętana. Każdy wiersz mówi własny stan
zaznaczony lub niezaznaczony i żadna Spacja nie zmienia sąsiedniego planu.

## Poprzedni zestaw alpha 176

## Nowości alpha 176

### AMC-176-01 — Domyślna nazwa i podgląd

W Radiu wskaż stację, naciśnij `Shift+R` i przejdź Tabem do grupy **Nazwa
pliku**. Sprawdź pole tekstowe i podgląd, a następnie zmieniaj format między
MP3, M4A, FLAC i WAV.

Oczekiwane: pole zawiera `{stacja} - {data} {czas}`. Podgląd pokazuje nazwę
stacji, datę, czas oraz rozszerzenie odpowiadające formatowi. NVDA nie czyta
nazwy klasy ani identyfikatora pola. Rozszerzenia nie trzeba wpisywać ręcznie.

### AMC-176-02 — Gotowe szablony, tokeny i edycja

Uaktywnij przycisk **Wstaw token lub wybierz gotowy szablon**. Przejdź przez
podmenu **Gotowe szablony** oraz **Wstaw token**. Wybierz „Własna nazwa audycji
— data”, zmień tekst „Nazwa audycji” na rzeczywisty tytuł, ustaw kursor w
środku i wstaw jeszcze token dnia tygodnia.

Oczekiwane: oba podmenu i wszystkie pozycje mają zrozumiałe nazwy. Szablon
zastępuje pole, token trafia dokładnie w położenie kursora, a fokus wraca do
edycji. Podgląd od razu pokazuje wynik z polską nazwą dnia tygodnia.

### AMC-176-03 — Trwałość i rzeczywisty plik

Ustaw własny folder, nazwę `Moja audycja - {data-polska} - {czas}` oraz krótki
termin. Zapisz, ponownie otwórz plan przez `Ctrl+Shift+H`, a potem pozwól mu
się wykonać.

Oczekiwane: po ponownym otwarciu pole zachowuje cały szablon. W wybranym
folderze powstaje plik na przykład `Moja audycja - 31.08.2026 - 20-15.mp3`,
z rozszerzeniem właściwym dla formatu planu.

### AMC-176-04 — Części i ochrona przed nadpisaniem

Utwórz krótki plan dzielony na części z nazwą
`{stacja} - {data} - część {część}`. Osobno wykonaj dwa plany o identycznej
zwykłej nazwie bez tokenu części w tym samym folderze.

Oczekiwane: kolejne części kończą się `część 01`, `część 02` i dalej. Dwa
pliki o identycznej nazwie nie nadpisują się; drugi otrzymuje automatyczny
numer. Znaki niedozwolone w nazwie stacji są zastępowane bez utraty nagrania.

## Poprzedni zestaw alpha 175

## Nowości alpha 175

### AMC-175-01 — Alt+2 i Escape wracają do Ulubionych

W Radiu otwórz `Ctrl+U`, wybierz dowolną ulubioną stację i naciśnij `Alt+2`.
Następnie naciśnij `Escape`. Powtórz próbę, używając zamiast Escape klawisza
Backspace.

Oczekiwane: `Alt+2` otwiera **Nagrywane**. Escape i Backspace wracają do
**Ulubionych**, a fokus oraz zaznaczenie wracają na wcześniej wybraną stację.
Nie trzeba ponownie naciskać `Ctrl+U`.

### AMC-175-02 — Alt+1 oznacza wszystkie zapisane stacje

Będąc w Ulubionych lub Nagrywanych, naciśnij `Alt+1`, a następnie sprawdź menu
**Widok** i Pomoc klawiatury `Ctrl+F1`.

Oczekiwane: `Alt+1` otwiera **Wszystkie stacje**, czyli Bibliotekę zapisanych
stacji. Ta sama pozycja podaje oba skróty: `Ctrl+L` i `Alt+1`; NVDA nie czyta
technicznej nazwy ani nie powtarza skrótu w etykiecie.

### AMC-175-03 — Alt+3 pozostaje wolne

Na liście Radia naciśnij `Alt+3`, a do bieżącego pojedynczego odtwarzacza
przejdź osobno przez `F6`.

Oczekiwane: `Alt+3` informuje, że nie ma jeszcze widoku w Radiu, nie zmienia
listy i nie rusza fokusu. `F6` nadal otwiera aktualny odtwarzacz. Program nie
pokazuje pustej lub pozornej listy urządzeń.

## Poprzedni zestaw alpha 174

## Nowości alpha 174

### AMC-174-01 — Shift+N, Shift+T i Shift+C bez skoku fokusu

Uruchom lokalny utwór dziedziczący opcje globalne i przejdź `F6` do
odtwarzacza. Ustaw fokus kolejno na przyciskach **Odtwórz lub wstrzymaj**,
**Cofnij 10 sekund** i **Następny utwór**. Na każdym sprawdź `Shift+N`,
`Shift+T` oraz kilkakrotnie `Shift+C`.

Oczekiwane: za każdym razem wykonuje się właściwe polecenie i słychać nowy
stan globalny. Fokus pozostaje dokładnie na kontrolce, na której był przed
skrótem. `Shift+C` nie ustawia fokusu na **Cofnij 10 sekund**, a `Shift+N` nie
ustawia go na **Następny utwór** ani **Prędkość normalna**.

### AMC-174-02 — Stan globalny a wyjątek pliku lub folderu

Na pliku, dla którego `Alt+Shift+Enter` wskazuje własną wartość albo wartość
folderu, naciśnij `Shift+N`, `Shift+T` i `Shift+C`, a następnie otwórz menu
**Odtwarzanie** oraz pozycję **Zmień opcje bieżącego utworu**.

Oczekiwane: skróty zmieniają i oznajmiają ustawienia globalne. Pozycja
efektywna nadal uczciwie pokazuje wartość pliku lub folderu, jeżeli ta
przesłania poziom globalny. Po ustawieniu dziedziczenia globalnego ten sam
skrót zmienia również wartość efektywną bieżącego utworu.

### AMC-174-03 — Brak ukrytych liter dostępu

Powtórz `Shift+N`, `Shift+T` i `Shift+C` w odtwarzaczu Radia oraz na zwykłej
liście, gdzie tor nie udostępnia tych funkcji. W lokalnym odtwarzaczu włącz
też Pomoc klawiatury przez `Ctrl+F1` i sprawdź wszystkie trzy kombinacje.

Oczekiwane: w nieobsługiwanym kontekście kombinacje nie zmieniają ustawień i
nie przenoszą fokusu na żaden przycisk. Pomoc klawiatury wypowiada właściwy
opis bez wykonania polecenia; po wyłączeniu Pomocy fokus wraca bez zmiany.

## Poprzedni zestaw alpha 173

## Nowości alpha 173

### AMC-173-01 — Efektywny stan w obu menu

Uruchom lokalny plik należący do folderu z własnymi opcjami. Otwórz główne
menu **Odtwarzanie**, a następnie menu kontekstowe odtwarzacza.

Oczekiwane: oprócz jawnie globalnych przełączników jest pozycja **Zmień opcje
bieżącego utworu**. Podaje osobno normalizację, przejścia i ciszę oraz mówi przy
każdej wartości „ustawienie pliku”, „ustawienie folderu” albo „ustawienie
globalne”. `Alt+Shift+Enter` jest odczytywany raz, jako skrót tej pozycji.

### AMC-173-02 — Zapis folderu i naturalny koniec

Na folderze lub albumie otwórz `Alt+Shift+Enter`, włącz normalizację i
przejścia, a ciszę ustaw na 2 sekundy. Zapisz, ponownie otwórz okno i uruchom
dwa krótkie pliki z tego folderu bez ręcznego używania Page Down.

Oczekiwane: potwierdzenie wymienia zapisane wartości, ponownie otwarte okno je
pamięta, a pozycja efektywna w menu wskazuje źródło folderowe. Przejście i
dwusekundowa cisza dotyczą naturalnego końca utworu; ręczna zmiana nie jest
testem ciszy po utworze.

### AMC-173-03 — Własne ustawienie pliku i restart

Na jednym z plików ustaw wartości inne niż w folderze, zapisz, zamknij AMC,
uruchom ponownie i otwórz ten plik oraz drugi plik z tego samego folderu.

Oczekiwane: pierwszy plik przywraca własne wartości i menu mówi „ustawienie
pliku”. Drugi nadal dziedziczy folder. Zmiana globalnych przełączników nie
nadpisuje żadnego z tych wyjątków.

## Poprzedni zestaw alpha 172

## Nowości alpha 172

### AMC-172-01 — Lista sesji pod Ctrl+Shift+S

Na zwykłej liście, w odtwarzaczu i po włączeniu Pomocy klawiatury `Ctrl+F1`
naciśnij `Ctrl+Shift+S`. Powtórz próbę przez `Ctrl+0`, a w sesji z presetami
sprawdź osobno `Ctrl+Shift+0`.

Oczekiwane: `Ctrl+Shift+S` otwiera albo opisuje listę sesji i jest głównym
skrótem widocznym w menu Sesja. `Ctrl+0` nadal otwiera tę samą listę jako
alias, natomiast `Ctrl+Shift+0` wywołuje preset 0 i nigdy nie otwiera sesji.

### AMC-172-02 — Menu zależne od możliwości odtwarzania

W Plikach lokalnych otwórz menu **Odtwarzanie** i menu kontekstowe odtwarzacza,
a potem wykonaj to samo w Radiu oraz w demonstracyjnej sesji usługi, która nie
ma jeszcze prawdziwego toru odtwarzania. Przejrzyj także opis grupy
**Odtwarzanie — ustawienia globalne** w Ustawieniach.

Oczekiwane: lokalny tor pokazuje normalizację, przejścia i ciszę. Radio oraz
niepodłączona usługa nie pokazują martwych poleceń. Opis Ustawień wyjaśnia, że
przyszły adapter może udostępnić każdą opcję lokalnie albo przez oficjalne API.
NVDA nie odczytuje nazwy klasy, flag możliwości ani technicznego identyfikatora.

### AMC-172-03 — Spacja, Ctrl+M i pamięć głośności stacji

Uruchom Radio. Naciśnij Spację, ponownie Spację, `Ctrl+M` i jeszcze raz
`Ctrl+M`. Ustaw dwie różne głośności dla dwóch stacji, przełączaj je, a potem
uruchom AMC ponownie.

Oczekiwane: Spacja zatrzymuje i wznawia słyszalny transport. `Ctrl+M` jedynie
zeruje wyjście bieżącej sesji; odbiór, timeshift i nagrywanie nadal trwają.
Każda stacja przywraca swój zapamiętany poziom także po restarcie.

### AMC-172-04 — Nagrywane stacje i timeshift

Uruchom nagrywanie dwóch stacji, otwórz `Alt+2`, na jednej użyj
`Shift+Spacja`, a potem `Ctrl+Alt+R`. Następnie uruchom ponownie co najmniej dwa
nagrania i użyj `Ctrl+Alt+Shift+R`. Osobno podczas nagrywania słuchanej stacji
sprawdź lewo, prawo, Home i End, zakończ jej zapis bez Escape i ponów próbę.

Oczekiwane: pauza i zakończenie dotyczą wybranej stacji. Skrót z dodatkowym
Shiftem zatrzymuje wszystkie nagrania po wymaganym potwierdzeniu. Timeshift
jest zablokowany tylko dla aktualnie słuchanej i nagrywanej stacji, po czym
wraca natychmiast po zakończeniu jej zapisu.

### AMC-172-05 — Shift+R i lista harmonogramów

Dla dwóch stacji utwórz przez `Shift+R` nagrania o różnych formatach i bitrate.
Otwórz `Ctrl+Shift+H`, przechodź strzałkami i przełączaj plany Spacją, zapisz,
a następnie ponownie otwórz listę.

Oczekiwane: każde nagranie jednorazowe i harmonogram pamięta własny format oraz
bitrate. Lista działa jak lista pól wyboru, fokus pozostaje na przełączanym
planie, a zmiany są trwałe.

### AMC-172-06 — Historia Shazam i przyszłe usługi

Otwórz historię przez `Ctrl+Alt+S`, sprawdź jej menu kontekstowe, a następnie
naciśnij `Ctrl+Shift+S`.

Oczekiwane: `Ctrl+Alt+S` nadal otwiera rozpoznane utwory z działającym
otwarciem wyniku, kopiowaniem, eksportem i usuwaniem. `Ctrl+Shift+S` przechodzi
do listy sesji. Dopóki Apple Music, Spotify lub TIDAL nie mają podłączonych
adapterów, menu nie pokazuje martwego „Dodaj do…”. Po integracji właściwym
celem będzie album z rozpoznanym utworem zaznaczonym, a nie przypadkowy wynik.

## Poprzedni zestaw alpha 171

## Nowości alpha 171

### AMC-171-01 — Skróty bez prefiksu w lokalnym odtwarzaczu

Uruchom dowolny utwór w Plikach lokalnych i pozostaw fokus w odtwarzaczu.
Naciśnij kolejno `Shift+N`, `Shift+T` i kilka razy `Shift+C`.

Oczekiwane: `Shift+N` przełącza globalną normalizację, `Shift+T` globalne
łagodne przejścia, a `Shift+C` przechodzi przez brak, pół sekundy, jedną, dwie,
trzy i pięć sekund ciszy. Każda zmiana jest oznajmiana i od razu widoczna w
menu. Nie trzeba wcześniej naciskać globalnego prefiksu AMC.

### AMC-171-02 — Zakres odtwarzacza, listy i Radia

Wróć Escape do listy Plików lokalnych i naciśnij te same trzy kombinacje.
Następnie uruchom stację Radia, pozostaw fokus w radiowym odtwarzaczu i powtórz
próbę. Na końcu użyj dotychczasowego globalnego prefiksu, a po nim `Shift+N`,
`T` i `C`.

Oczekiwane: bezpośrednie `Shift+N`, `Shift+T` i `Shift+C` nie zmieniają tych
ustawień na liście ani w Radiu i nie przejmują radiowych poleceń. Warianty po
globalnym prefiksie nadal działają niezależnie od aktywnego widoku.

### AMC-171-03 — Menu kontekstowe i menu Odtwarzanie

Otwórz menu kontekstowe lokalnego odtwarzacza, a potem główne menu
**Odtwarzanie**. Przejdź NVDA przez normalizację, przejścia i ciszę.

Oczekiwane: menu kontekstowe podaje odpowiednio `Shift+N`, `Shift+T` i
`Shift+C`, bez słowa „prefiks”. Menu główne nadal mówi „po prefiksie Shift+N”,
„po prefiksie T” oraz „po prefiksie C”. Nazwa i skrót są czytane po jednym
razie, a etykieta każdego polecenia jawnie mówi, że zmienia ustawienie globalne.

### AMC-171-04 — Pomoc klawiatury i fokus

W lokalnym odtwarzaczu włącz `Ctrl+F1`, sprawdź po kolei trzy nowe kombinacje,
a następnie przejrzyj spis skrótów. Zamknij pomoc Escape i ponownie użyj
`Shift+N`.

Oczekiwane: pomoc rozpoznaje lokalne skróty i odczytuje ich użytkowe nazwy.
Spis pokazuje zarówno wariant lokalny z opisem „odtwarzacz Plików lokalnych”,
jak i wariant po prefiksie. Po zamknięciu pomocy fokus wraca i `Shift+N` nadal
działa bez utknięcia odtwarzacza.

## Poprzedni zestaw alpha 170

## Nowości alpha 170

### AMC-170-01 — Wznowienie po pomocy i po Escape

Uruchom zapis Gdańska albo PIK, otwórz `Ctrl+F1`, zamknij pomoc klawiszem
Escape, a potem kilka razy użyj Spacji do pauzy i wznowienia. Powtórz próbę po
wyjściu z odtwarzacza klawiszem Escape.

Oczekiwane: plik wznawia się w zachowanej pozycji. Nie pojawia się komunikat
techniczny ani trwały stan, w którym kolejne próby nie mogą już odtwarzać.
Jeżeli globalne pole **Wstrzymuj odtwarzanie po wyjściu z odtwarzacza** jest
włączone, sam Escape celowo robi pauzę; Spacja ma potem prawidłowo wznowić.
Po wyłączeniu tego pola Escape wraca do listy bez wstrzymania.

### AMC-170-02 — Naturalny koniec Łodzi i Rzeszowa

Poczekaj do naturalnego końca pełnych plików Radia Łódź i Radia Rzeszów, nie
zmieniając utworu ręcznie. Najlepiej ustaw za każdym z nich jeszcze jeden plik
w tym samym kontekście odtwarzania.

Oczekiwane: po dojściu do końca AMC raz oznajmia koniec albo uruchamia następny
utwór. Pozycja nie zatrzymuje się na kilka minut bez końca i bez błędu. Jeżeli
tor Windows nie wyśle zdarzenia końca, nadzór rozpoznaje koniec po krótkim
braku postępu; nie dubluje komunikatu ani następnego utworu.

### AMC-170-03 — Pobrany i dostępny tylko online plik iCloud

W Eksploratorze wybierz dla jednego pliku iCloud **Zawsze zachowuj na tym
urządzeniu**, a drugi pozostaw tylko online. Otwórz oba kolejno w AMC.

Oczekiwane: pobrany lub przypięty plik zaczyna się jak plik lokalny i nie jest
opisywany jako oczekiwanie na pobranie. Plik tylko online może wywołać
komunikat o pobieraniu z chmury i otrzymuje dłuższy limit. Brak sieci kończy się
czytelnym komunikatem, ale nie blokuje fokusu ani następnego pliku.

### AMC-170-04 — Ustawienia pliku pod Alt+Shift+Enter

Zaznacz lokalny utwór i naciśnij `Alt+Shift+Enter`. Przejdź Tabem przez pozycję,
prędkość, normalizację, łagodne przejścia i ciszę. Ustaw dla pliku wartości
inne niż globalne, zapisz, ponownie otwórz okno i uruchom plik.

Oczekiwane: każde pole ma użytkową nazwę, a rozwinięte pozycje nie czytają
nazwy klasy, właściwości ani rekordu. `1,00 razy` jest opisane jako normalna
prędkość. Wybrane wartości wracają po ponownym otwarciu i mają pierwszeństwo
przed folderem oraz ustawieniami globalnymi. Po Zapisz i Anuluj fokus wraca do
elementu, z którego otwarto okno.

### AMC-170-05 — Dziedziczenie ustawień folderu

W widoku Foldery wybierz folder i otwórz `Alt+Shift+Enter`. Ustaw tylko
normalizację, pozostaw przejścia i ciszę zgodne z folderem nadrzędnym lub
ustawieniem globalnym. Dla jednego pliku wewnątrz ustaw następnie własną ciszę.

Oczekiwane: folder wpływa na pliki w sobie i podfolderach. Każda właściwość
dziedziczy się niezależnie z najbliższego folderu. Wartość pojedynczego pliku
wygrywa tylko dla tego pliku. Powrót wszystkich pól folderu do dziedziczenia
usuwa pusty wyjątek, a ustawienia pozostają trwałe po ponownym uruchomieniu.

### AMC-170-06 — Jednoznaczne ustawienia globalne

Otwórz **Ustawienia > Ogólne** i grupę **Odtwarzanie — ustawienia globalne**,
a następnie menu **Odtwarzanie** oraz menu kontekstowe lokalnego odtwarzacza.

Oczekiwane: normalizacja, przejścia i cisza są nadal dostępne w ustawieniach.
Pozycje szybkiego menu jawnie mówią, że zmieniają wartości globalne. Zmiana
globalna wpływa na element dziedziczący, ale nie nadpisuje wyjątku pliku lub
folderu. Skróty prefiksowe z alpha 169 nadal zmieniają ustawienia globalne.

## Poprzedni zestaw alpha 169

## Nowości alpha 169

### AMC-169-01 — Opcje w menu Odtwarzanie

Wybierz sesję Pliki lokalne, otwórz menu **Odtwarzanie** i przejdź strzałkami
przez normalizację, łagodne przejścia oraz podmenu ciszy. Przełącz oba pola,
wybierz dwie sekundy ciszy, zamknij i ponownie otwórz menu.

Oczekiwane: pozycje znajdują się bezpośrednio za wyciszaniem sesji. NVDA czyta
nazwę, aktualny stan zaznaczenia i skrót tylko raz. W podmenu dokładnie jedna
wartość ciszy jest zaznaczona, a neutralna nazywa się „Bez dodatkowej ciszy”.
Po ponownym otwarciu wszystkie stany są aktualne.

### AMC-169-02 — Menu kontekstowe odtwarzacza i zakres lokalny

W Plikach lokalnych uruchom utwór, otwórz menu kontekstowe odtwarzacza i
sprawdź te same trzy opcje. Następnie przejdź do Radia i ponownie otwórz menu
Odtwarzanie oraz menu kontekstowe odtwarzacza.

Oczekiwane: w lokalnym odtwarzaczu opcje są tuż za Następnym utworem i mają ten
sam stan co menu główne. W Radiu są ukryte; nie pojawia się pusty separator ani
sugestia, że ustawienia zmienią transmisję.

### AMC-169-03 — Skróty po globalnym prefiksie

Na domyślnym profilu klawiatury naciśnij globalny prefiks AMC, potem kolejno
`Shift+N`, `T` i kilka razy `C`. Po każdej próbie otwórz menu Odtwarzanie.

Oczekiwane: `Shift+N` przełącza normalizację, `T` łagodne przejścia, a `C`
przechodzi przez brak, pół sekundy, jedną, dwie, trzy i pięć sekund, po czym
wraca do braku. Każda zmiana jest oznajmiona i od razu widoczna w menu. Jeśli
aktywny jest własny profil, skróty działają dopiero po przypisaniu tych poleceń
w Ustawieniach i AMC nie nadpisuje własnej mapy.

### AMC-169-04 — Trwałość, paleta i brak technicznych nazw

Ustaw dowolne trzy wartości z menu, zamknij i uruchom AMC ponownie. Otwórz
paletę `Ctrl+Shift+K`, wyszukaj „normalizacja”, „łagodne” i „cisza”, a następnie
sprawdź spis skrótów `Ctrl+F1`.

Oczekiwane: wartości pozostają zapisane. Paleta ma zarówno pozycję szybkiego
przełączenia, jak i wejście do Ustawień, podaje bieżący stan oraz aktywny skrót
prefiksowy. NVDA nie czyta nazwy klasy, właściwości, rekordu, identyfikatora ani
technicznej wartości w milisekundach.

## Poprzedni zestaw alpha 168

## Nowości alpha 168

### AMC-168-01 — Normalizacja bez zmiany głośności użytkownika

W `Ustawienia > Ogólne > Odtwarzanie` włącz normalizację. Odtwórz kolejno
wyraźnie cichy i wyraźnie głośny plik lokalny, a w trakcie zmień głośność
strzałkami. Po próbie sprawdź daty modyfikacji obu plików.

Oczekiwane: różnica odczuwanej głośności maleje, nie słychać przesterowania,
strzałki nadal przewidywalnie regulują głośność, a pliki nie są zmieniane.

### AMC-168-02 — Naturalne i ręczne łagodne przejście

Wyłącz dodatkową ciszę, włącz łagodne przejścia i uruchom co najmniej trzy
lokalne utwory w jednym kontekście. Posłuchaj automatycznej granicy, a potem
zmień utwór ręcznie przez Page Down.

Oczekiwane: naturalny koniec wygasa i następny utwór łagodnie wchodzi. Ręczna
zmiana nie urywa poprzedniego dźwięku; oba tory mogą krótko się nałożyć, lecz po
półtorej sekundy słychać wyłącznie nowy utwór. Fokus i sterowanie nie czekają
na koniec wygaszenia.

### AMC-168-03 — Dodatkowa cisza tylko po naturalnym końcu

Ustaw dwie sekundy ciszy i wyłącz łagodne przejście. Poczekaj na naturalny
koniec utworu, następnie sprawdź ręczne Page Down, pauzę i wznowienie. Powtórz
próbę w Radiu.

Oczekiwane: około dwie sekundy ciszy występują tylko przed automatycznym
lokalnym następcą. Ręczna zmiana, pauza, wznowienie i Radio nie otrzymują
opóźnienia. AMC mówi „Następny utwór po ciszy” zamiast przedwcześnie twierdzić,
że dźwięk już trwa.

### AMC-168-04 — NVDA, zapis i paleta poleceń

Przejdź Tabem po trzech nowych opcjach, rozwiń listę ciszy, wybierz kolejno
brak, pół sekundy i dwie sekundy, zapisz, ponownie otwórz Ustawienia i użyj
strzałek. W palecie `Ctrl+Shift+K` wyszukaj osobno „normalizacja”, „łagodne” i
„cisza między utworami”. Sprawdź też powrót fokusu po Zapisz i Anuluj.

Oczekiwane: NVDA czyta wyłącznie użytkowe nazwy i stan; „Bez dodatkowej ciszy”
jednoznacznie opisuje wartość neutralną. Nie pojawia się nazwa klasy, rekord,
właściwość ani identyfikator. Zapisany wybór wraca po ponownym otwarciu, a
Enter z palety ustawia fokus dokładnie na właściwej kontrolce.

## Poprzedni zestaw alpha 167

## Nowości alpha 167

### AMC-167-01 — Automatyczne rozpoznawanie bez oznajmiania

W `Ustawienia > Komunikaty` wyłącz **Oznajmiaj automatycznie rozpoznane
utwory**. W Radiu uruchom obserwowanie `Shift+S` i pozostaw je do znalezienia
co najmniej jednego utworu. Następnie otwórz historię `Ctrl+Alt+S`.

Oczekiwane: wynik nie jest wypowiadany, ale pojawia się w historii z nazwą
stacji i czasem. Obserwowanie nadal działa, a pole wyboru nie zmienia jego
stanu.

### AMC-167-02 — Oznajmianie wyłącznie w aktywnym AMC

Włącz opcję, pozostaw obserwowanie aktywne i przejdź do innej aplikacji. Po
kilku minutach wróć do AMC i sprawdź historię. Następnie poczekaj na kolejne
rozpoznanie, pozostając w oknie AMC.

Oczekiwane: poza AMC żaden wynik nie przerywa pracy NVDA, choć jest zapisany w
historii. Kolejny nowy wynik znaleziony przy aktywnym AMC zostaje oznajmiony.

### AMC-167-03 — Ręczne rozpoznanie i paleta poleceń

Wyłącz automatyczne oznajmianie, naciśnij `S` w odtwarzaczu i pozostań w AMC.
Potem otwórz paletę `Ctrl+Shift+K`, wyszukaj „rozpoznane utwory” i uruchom
pozycję ustawień.

Oczekiwane: ręczne rozpoznanie nadal podaje wynik. Paleta mówi, że oznajmianie
automatyczne jest wyłączone, a Enter otwiera kartę Komunikaty z fokusem na
właściwym polu wyboru. NVDA nie czyta nazwy klasy ani technicznego
identyfikatora ustawienia.

## Poprzedni zestaw alpha 166

## Nowości alpha 166

### AMC-166-01 — Format i bitrate osobnego nagrania

W Radiu wskaż stację i naciśnij `Shift+R`. Utwórz krótkie nagranie
natychmiastowe w MP3 z bitrate 128 kb/s. Potem utwórz drugi plan tej samej albo
innej stacji w M4A/AAC 192 kb/s. Otwórz oba ponownie przez `Ctrl+Shift+H`.

Oczekiwane: format i bitrate są zwykłymi, czytelnymi polami kombi. Każdy plan
pamięta własne ustawienia niezależnie od wartości ogólnej. NVDA nie czyta nazw
klas, identyfikatorów wyliczeń ani technicznych reprezentacji obiektów.

### AMC-166-02 — Lista harmonogramów jak lista pól wyboru

Otwórz `Ctrl+Shift+H`. Nawiguj strzałkami i naciskaj Spację na kilku planach,
następnie wybierz Zapisz i otwórz listę ponownie.

Oczekiwane: każdy wiersz jest jednoznacznie oznaczony jako zaznaczony albo
niezaznaczony. Spacja przełącza tylko bieżący plan, fokus zostaje na nim, a
stan jest trwały po zapisaniu i ponownym otwarciu.

### AMC-166-03 — Sterowanie wybraną nagrywaną stacją

Uruchom dwa nagrania różnych stacji, w tym przynajmniej jedno z harmonogramu.
Przejdź do `Alt+2`. Na pierwszej stacji naciśnij `Shift+Spacja` dwa razy, a
następnie `Ctrl+Alt+R`.

Oczekiwane: pauza i wznowienie dotyczą wyłącznie wybranej stacji.
`Ctrl+Alt+R` kończy jej bieżące nagranie także wtedy, gdy uruchomił je
harmonogram, ale nie wyłącza przyszłych terminów cyklicznego planu. Druga
stacja nadal się nagrywa.

### AMC-166-04 — Zatrzymanie wszystkich i rozdzielone skróty

Przy co najmniej dwóch aktywnych nagraniach naciśnij `Ctrl+Alt+Shift+R`, a
potem sprawdź `Ctrl+Shift+H`.

Oczekiwane: pierwszy skrót po potwierdzeniu zatrzymuje wszystkie nagrania.
Drugi zawsze otwiera harmonogram. Dawne `Alt+Shift+R` nie wykonuje ukrytej
operacji, a pomoc klawiszy i paleta podają nowe znaczenia.

### AMC-166-05 — Timeshift podczas nagrywania

Odtwórz stację, uruchom jej nagrywanie i w odtwarzaczu sprawdź lewo, prawo,
Home i End. Równolegle nagraj inną stację w tle i ponów próbę na stacji, która
nie jest nagrywana. Zakończ nagranie bieżącej stacji bez wychodzenia z
odtwarzacza i sprawdź przewijanie ponownie.

Oczekiwane: timeshift jest zablokowany tylko dla aktualnie słuchanej i
nagrywanej stacji. Nagranie innej stacji w tle go nie blokuje. Po zakończeniu
nagrywania przewijanie działa od razu, przed naciśnięciem Escape.

### AMC-166-06 — Głośność pamiętana osobno dla stacji

Ustaw pierwszej stacji głośność 20%, a drugiej 65%. Przełączaj je Page Up i
Page Down, następnie zamknij i uruchom AMC ponownie.

Oczekiwane: każda stacja przywraca swój poziom zarówno podczas bieżącej sesji,
jak i po restarcie. Zmiana nie reguluje głośności Windows ani NVDA.

### AMC-166-07 — Spacja a wyciszenie sesji

Podczas odtwarzania Radia sprawdź kolejno Spację, `Ctrl+M`, ponownie `Ctrl+M`
i `Ctrl+Shift+M`, również gdy trwa nagrywanie.

Oczekiwane: Spacja wstrzymuje albo wznawia odsłuch. `Ctrl+M` tylko wycisza
bieżącą sesję bez zatrzymania odbioru, timeshiftu i nagrania. `Ctrl+Shift+M`
pozostaje osobną warstwą wyciszenia wszystkich sesji AMC.

### AMC-166-08 — Menu kontekstowe rozpoznanych utworów

Otwórz `Ctrl+Alt+S`, wywołaj menu kontekstowe i sprawdź otwarcie wyniku,
kopiowanie zwykłe i bogate, eksport oraz usuwanie.

Oczekiwane: wszystkie pozycje mają użytkowe nazwy i właściwe skróty czytane
tylko raz. Działania odpowiadają istniejącym przyciskom i nie zmieniają
przypadkiem odtwarzanej stacji.

## Poprzedni zestaw alpha 165

## Nowości alpha 165

### AMC-165-01 — Menu główne bez powtarzania skrótów

W Radiu otwórz menu główne i przejdź po pozycjach Odtwarzanie oraz Widok,
zwłaszcza: wyciszanie sesji, nagrywanie i Rozpoznane utwory.

Oczekiwane: NVDA czyta nazwę polecenia, stan pozycji i właściwy skrót tylko
raz. Nie dokłada osobnej litery dostępu, takiej jak „W”, „B” albo „R”. Nazwy
głównych kategorii menu nadal są dostępne klawiaturą przez Alt.

### AMC-165-02 — Menu kontekstowe listy, odtwarzacza i wyszukiwania

Otwórz menu kontekstowe na stacji radiowej, następnie w odtwarzaczu oraz na
wyniku wyszukiwania. Sprawdź kilka pozycji ze skrótami.

Oczekiwane: każdy skrót jest oznajmiany tylko raz. Dynamiczna zmiana nazwy,
np. „Nagrywaj tę stację w tle” na „Zakończ nagrywanie tej stacji”, nie
przywraca podwójnego odczytu.

## Poprzedni zestaw alpha 164

## Nowości alpha 164

### AMC-164-01 — Bogate kopiowanie rozpoznanego utworu

Rozpoznaj utwór w Radiu, otwórz historię przez `Ctrl+Alt+S`, zaznacz wpis i
naciśnij `Ctrl+Shift+C`. Wklej wynik do zwykłego edytora tekstu.

Oczekiwane: po danych utworu znajdują się różne, prawidłowo zakodowane łącza do
Apple Music, Spotify, Tidal, YouTube Music, Discogs i MusicBrainz. Znaki
diakrytyczne ani spacje z tytułu nie uszkadzają adresów. `Ctrl+C` nadal kopiuje
sam opis bez tych łączy.

### AMC-164-02 — Eksport JSON i CSV

Wyeksportuj tę samą historię kolejno do JSON i CSV, po czym otwórz oba pliki w
edytorze.

Oczekiwane: eksport ma cztery osobne wyszukiwania usług streamingowych oraz
osobną grupę katalogową Discogs i MusicBrainz. AMC nie przedstawia łącza jako
pewnego dopasowania wydania ani nie zapisuje fikcyjnego identyfikatora. Pliki
są czytelne po polsku i zachowują poprzednie dane historii.

## Poprzedni zestaw alpha 163

## Nowości alpha 163

### AMC-163-01 — Szybkie zatrzymanie i dzielenie HLS

Uruchom nagrywanie Trójki albo innej stacji HLS. Po kilku sekundach naciśnij
`T` dwa razy w krótkim odstępie, po czym szybko zakończ nagrywanie klawiszem
`R`. Powtórz z formatem MP3 i Oryginalnym oraz z folderem nagrań w iCloud,
OneDrive albo Dysku Google.

Oczekiwane: kolejne polecenie nie uruchamia nowej części po żądaniu
zatrzymania. Gotowe pliki mają długość zbliżoną do rzeczywistego czasu próby,
nie zawierają wcześniejszych segmentów manifestu i dają się odtworzyć.
W folderze docelowym nie pozostaje `.amc-publishing`; plik roboczy nie jest
tworzony bezpośrednio w chmurze. Gdy publikacja do chmury zawiedzie, komunikat
podaje ścieżkę zachowanej lokalnej kopii.

### AMC-163-02 — Ręczne rozpoznawanie z odsłuchiwanego bufora

Odtwarzaj stację z muzyką przez co najmniej 12 sekund, otwórz odtwarzacz i
naciśnij `S`. Przed upływem kilku sekund wykonaj też próbę na nowo uruchomionej
stacji oraz próbę na mowie.

Oczekiwane: AMC mówi „Rozpoznaję utwór”, po czym tytuł i wykonawcę albo krótki
kontrolowany brak wyniku. Rozpoznawanie dotyczy tego miejsca transmisji, które
jest słyszane także po cofnięciu w timeshift, i nie otwiera drugiego połączenia
ze stacją. Fokus pozostaje w odtwarzaczu, a odsłuch i nagrywanie nie są
przerywane.

### AMC-163-03 — Obserwowanie i eliminacja powtórzeń

W odtwarzaczu Radia naciśnij `Shift+S`, pozostaw stację muzyczną na kilka
minut, zmień ją Page Down i po chwili wróć. Ponownie naciśnij `Shift+S`.

Oczekiwane: program potwierdza włączenie i wyłączenie. Nowy rozpoznany utwór
jest oznajmiany i zapisywany razem ze stacją oraz czasem. Ten sam wynik tej
samej stacji nie jest dopisywany co minutę. Obserwowanie nie uruchamia się
samoczynnie po ponownym starcie AMC.

### AMC-163-04 — Historia, kopiowanie i eksport

W Radiu naciśnij `Ctrl+Alt+S`. Nawiguj po liście, zaznacz kilka wpisów Shiftem,
sprawdź `Ctrl+C`, `Ctrl+Shift+C`, Delete oraz eksport kolejno do JSON i CSV.
Zamknij i ponownie uruchom AMC.

Oczekiwane: lista zaczyna się od najnowszego wpisu i podaje wyłącznie
użytkowe etykiety: utwór, wykonawcę, stację oraz datę. `Ctrl+C` kopiuje opisy,
a `Ctrl+Shift+C` również łącza wyszukiwania w Apple Music, Spotify, Tidal,
YouTube Music, Discogs i MusicBrainz.
Delete usuwa wyłącznie wpis historii. Historia i usunięcia są trwałe; fokus po
zamknięciu okna wraca do listy albo odtwarzacza.

## Poprzedni zestaw alpha 162

## Nowości alpha 162

### AMC-162-01 — Ręczny podział klawiszem T

W odtwarzaczu Radia rozpocznij ręczne nagrywanie klawiszem `R`. Po kilkunastu
sekundach naciśnij `T`, odczekaj i naciśnij `T` ponownie, a na końcu zakończ
nagrywanie klawiszem `R`.

Oczekiwane: każde `T` najpierw oznajmia zapisywanie bieżącej części, następnie
podaje numer nowej części i jej użytkową nazwę pliku. Odsłuch stacji nie jest
zatrzymywany ani przełączany. W folderze nagrań powstają trzy różne, poprawnie
zamknięte pliki, bez pozostawionych plików `.amc-partial`.

### AMC-162-02 — T w widoku Nagrywane i podczas pauzy

Uruchom ręczne nagranie, przejdź do widoku Nagrywane przez `Alt+2`, wstrzymaj
zapis klawiszem `Shift+Spacja` i naciśnij `T`. Odczytaj wiersz oraz
`Alt+Enter`, następnie wznów nagranie.

Oczekiwane: `T` dzieli wybrane nagranie ręczne. Nowa część zachowuje stan
„nagrywanie wstrzymane”, dopóki użytkownik go nie wznowi. Informacje podają
liczbę zapisanych części i bieżący plik. Punkty pauzy są przypisane do pliku,
w którym je utworzono.

### AMC-162-03 — Dostępny wybór podziału w Shift+R

Na stacji naciśnij `Shift+R`. Przejdź do pola Sposób zapisu i sprawdź wartości
Jeden plik oraz Dziel na części. Dla drugiej wartości wpisz 15 w polu Długość
części w minutach, a całkowitą długość ustaw na 120 minut. Zapisz plan i
otwórz listę harmonogramów przez `Ctrl+Shift+H`.

Oczekiwane: NVDA czyta wyłącznie podane etykiety, bez nazw klas, właściwości i
wartości technicznych. Pole długości części jest nieaktywne dla jednego pliku i
aktywne dla dzielenia. Wiersz planu podaje „120 min, części co 15 min”.

### AMC-162-04 — Całkowity czas planu nie mnoży się przez części

Utwórz krótki plan o długości 3 minut i podziale co 1 minutę. Pozwól mu
zakończyć się bez ręcznej ingerencji.

Oczekiwane: harmonogram kończy się po około 3 minutach łącznie i tworzy trzy
części, a nie trzy pliki po 3 minuty. Ostatnia część może być krótsza wskutek
czasu łączenia ze stacją. Komunikat końcowy podaje liczbę zapisanych plików.

## Poprzedni zestaw alpha 161

## Nowości alpha 161

### AMC-161-01 — Nazwa stacji przed stanem nagrywania

W Radiu uruchom ręczne nagranie stacji i wróć do listy. Przejdź strzałkami po
tej stacji w Bibliotece, Ulubionych, playliście i widoku Nagrywane.

Oczekiwane: NVDA najpierw podaje nazwę stacji, a następnie „nagrywanie”. Nazwa
klasy, identyfikator, adres strumienia ani reprezentacja obiektu nie są
odczytywane.

### AMC-161-02 — Wstrzymane nagranie i odsłuch

Na nagrywanej stacji naciśnij `Shift+Spacja`, potem zwykłą Spację i ponownie
przejdź po jej wierszu.

Oczekiwane: komunikat zaczyna się od nazwy stacji, po której występuje
„nagrywanie wstrzymane” oraz odpowiedni stan odsłuchu, na przykład
„wstrzymany”. Po wznowieniu nagrywania ta sama nazwa pozostaje na początku.

### AMC-161-03 — Pasek stanu Radia

Odtwarzaj i nagrywaj stację, następnie wstrzymaj oraz wznów nagranie. Odczytaj
pasek stanu NVDA.

Oczekiwane: pasek stanu rozpoczyna się nazwą stacji. Dopiero dalej podaje stan
nagrywania, parametry audio, odtwarzanie, położenie względem transmisji i nazwę
sesji.

## Poprzedni zestaw alpha 160

## Nowości alpha 160

### AMC-160-01 — Lista harmonogramów pod Ctrl+Shift+H

W sesji Radio naciśnij `Ctrl+Shift+H` kolejno na liście stacji, w otwartym
odtwarzaczu i po ustawieniu fokusu na innej kontrolce głównego okna. Zamknij
listę Escape i sprawdź powrót fokusu.

Oczekiwane: za każdym razem otwiera się ta sama dostępna lista harmonogramów,
bez zmiany stacji i bez technicznych nazw kontrolek. Menu, paleta, spis skrótów
oraz `Ctrl+F1` podają `Ctrl+Shift+H`. Fokus po zamknięciu wraca na właściwe
miejsce.

### AMC-160-02 — Granica sesji i zgodność

Naciśnij `Ctrl+Shift+H` poza Radiem, a w Radiu sprawdź również starszy
`Ctrl+Alt+Shift+R`.

Oczekiwane: poza Radiem program nie przełącza sesji i mówi, że harmonogram jest
dostępny w Radiu. Starszy skrót nadal otwiera tę samą listę jako alias.

## Poprzedni zestaw alpha 159

## Nowości alpha 159

### AMC-159-01 — Wyciszenie bieżącej sesji

Uruchom plik lokalny albo Radio i naciśnij `Ctrl+M`. Sprawdź odtwarzacz, listę,
pasek stanu, menu Odtwarzanie i ponownie naciśnij `Ctrl+M`.

Oczekiwane: odsłuch bieżącej sesji cichnie bez pauzy. NVDA mówi „Wyciszono” z
nazwą sesji, stan jest widoczny jako „wyciszono”, a po drugim naciśnięciu wraca
dokładnie wcześniejsza głośność. Skrót nie wycisza NVDA ani dźwięku Windows.

### AMC-159-02 — Wyciszenie wszystkich sesji

Uruchom dźwięk w dwóch sesjach, pozostawiając przynajmniej jedną w tle, i
naciśnij `Ctrl+Shift+M`. Spróbuj uruchomić jeszcze jeden element, a następnie
ponownie naciśnij `Ctrl+Shift+M`.

Oczekiwane: wszystkie istniejące i później uruchomione tory odsłuchu AMC są
ciche. Drugie naciśnięcie przywraca ich zapisane głośności. Inne aplikacje,
dźwięk systemowy i NVDA pozostają słyszalne.

### AMC-159-03 — Niezależne warstwy

Wycisz jedną sesję przez `Ctrl+M`, potem włącz i wyłącz globalne wyciszenie
przez `Ctrl+Shift+M`.

Oczekiwane: po wyłączeniu warstwy globalnej wcześniej wyciszona sesja nadal
pozostaje cicha, a pozostałe odzyskują dźwięk. Komunikat podaje liczbę sesji,
które zachowały indywidualne wyciszenie.

### AMC-159-04 — Głośność i nagrywanie

Przy aktywnym globalnym wyciszeniu zmień głośność w odtwarzaczu. Osobno
rozpocznij nagrywanie Radia, wycisz bieżącą sesję i wszystkie sesje, po czym
zakończ nagranie.

Oczekiwane: zmiana podaje nowy procent i „wyciszono”, ale nie obchodzi globalnej
blokady dźwięku. Nagrywanie trwa niezależnie i powstały plik zawiera dźwięk.

### AMC-159-05 — Bezpieczne ponowne uruchomienie

Ustaw różne głośności, włącz oba rodzaje wyciszenia, zamknij AMC i uruchom je
ponownie.

Oczekiwane: wartości głośności są zachowane, lecz ulotne wyciszenie nie jest
przywracane i program nie uruchamia się niespodziewanie bez dźwięku.

## Poprzedni zestaw alpha 158

## Nowości alpha 158

### AMC-158-01 — Wznowienie trwającego harmonogramu

Utwórz jednorazowy harmonogram na kilka minut. Po rozpoczęciu nagrania zamknij
AMC, potwierdź zakończenie nagrywania i uruchom program ponownie przed końcem
zaplanowanego czasu.

Oczekiwane: pierwszy fragment jest prawidłowo finalizowany. AMC mówi
„Wznawiam zaplanowane nagrywanie”, tworzy drugi plik i nagrywa wyłącznie
pozostałą część tego samego okna. Nagranie ręczne nie uruchamia się ponownie.

### AMC-158-02 — Harmonogram po zakończeniu okna

Powtórz zamknięcie, ale uruchom AMC dopiero po całkowitym upływie czasu planu.

Oczekiwane: zakończone wystąpienie nie rozpoczyna się z opóźnieniem. Plan
jednorazowy znika, a cykliczny zachowuje kolejny prawidłowy termin.

### AMC-158-03 — Tyflo Podcast i zapis Oryginalny

Uruchom Tyflo Podcast z adresu kończącego się `listen.pls`, wybierz format
Oryginalny, nagraj kilkanaście sekund i zakończ.

Oczekiwane: AMC rozwiązuje listę do bezpośredniego strumienia, nie próbuje
interpretować tekstu PLS jako dźwięku i finalizuje odtwarzalny plik.

### AMC-158-04 — Przekierowania i inne listy

Jeżeli masz stację korzystającą z M3U, M3U8, PLS albo XSPF, sprawdź odtwarzanie
i zapis Oryginalny, zwłaszcza gdy adres przekierowuje lub wskazuje kolejną listę.

Oczekiwane: adresy względne i zagnieżdżone listy są rozwiązywane. Prawdziwy HLS
pozostaje manifestem. Błędna pętla kończy się czytelnym błędem zamiast
zawieszenia albo nieograniczonego pobierania.

## Poprzedni zestaw alpha 157

## Nowości alpha 157

### AMC-157-01 — Pauza i wznowienie MP3

W ustawieniach wybierz MP3. Rozpocznij nagrywanie stacji, po kilku sekundach
naciśnij `Shift+Spacja`, odczekaj i naciśnij `Shift+Spacja` ponownie.

Oczekiwane: AMC mówi „Wstrzymano nagrywanie” wraz z czasem gotowego pliku,
a następnie „Wznowiono nagrywanie”. Odsłuch może trwać bez przerwy. Po
zakończeniu plik nie zawiera fragmentu odebranego podczas pauzy.

### AMC-157-02 — Stan, fokus i właściwości

Podczas pauzy sprawdź bieżącą listę, odtwarzacz, widok `Alt+2` i `Alt+Enter`.

Oczekiwane: NVDA najpierw podaje nazwę stacji, a potem „nagrywanie wstrzymane”, a nie zwykłe „nagrywanie”. Fokus
pozostaje na tej samej stacji. Ponowne `Shift+Spacja` działa z listy i z
odtwarzacza, bez przełączania pauzy odsłuchu.

### AMC-157-03 — Zakładka punktu pauzy

Zakończ nagranie, w którym była jedna lub dwie pauzy. Przejdź do sesji Pliki
lokalne i otwórz `Ctrl+B`.

Oczekiwane: ukończone nagranie jest w lokalnej Bibliotece, a jego zakładki
„Pauza 1”, „Pauza 2” wskazują miejsca łączenia zachowanych części. Enter na
zakładce otwiera ten plik w prawidłowym czasie.

### AMC-157-04 — Inne formaty i tryb Oryginalny

Powtórz krótki test w M4A/AAC, FLAC albo WAV. Następnie wybierz Oryginalny i
spróbuj `Shift+Spacja` podczas nagrywania.

Oczekiwane: formaty kodowane można wstrzymać i wznowić. Dla Oryginalnego AMC
jednoznacznie mówi, że pauza nie jest dostępna; nagrywanie trwa i plik nie jest
uszkadzany.

### AMC-157-05 — Tyflo Podcast z importowanego PLS

Uruchom importowaną pozycję Tyflo Podcast wskazującą na `listen.pls`, wybierz
format Oryginalny, nagraj kilkanaście sekund i zakończ.

Oczekiwane: AMC rozwiązuje PLS do bezpośredniego MP3, tworzy gotowe nagranie i
nie zgłasza błędu mapowania strumienia ani tekstowego formatu LRC.

## Poprzedni zestaw alpha 156

## Nowości alpha 156

### AMC-156-01 — Zatrzymanie jednego nagrania

Uruchom jedno ręczne nagranie i naciśnij `Alt+Shift+R`.

Oczekiwane: AMC nie pyta dodatkowo, oznajmia zatrzymywanie, finalizuje plik i
usuwa stan nagrywania podawany po nazwie stacji. Odtwarzanie słuchanej stacji pozostaje niezależne.

### AMC-156-02 — Zatrzymanie kilku nagrań

Uruchom równolegle co najmniej dwa nagrania i naciśnij `Alt+Shift+R`.

Oczekiwane: pojawia się jedno dostępne ostrzeżenie z liczbą nagrań. Nie jest
wyborem domyślnym. Po potwierdzeniu wszystkie fragmenty są finalizowane, bez
serii nakładających się komunikatów. Anulowanie pozostawia nagrania bez zmian.

### AMC-156-03 — Ostrzeżenie przy zamykaniu

Podczas nagrywania naciśnij `Alt+F4`.

Oczekiwane: AMC podaje liczbę nagrań i pyta, czy zakończyć je oraz zamknąć
program. Nie pozostawia uszkodzonych plików roboczych. Po ponownym uruchomieniu
nie wznawia przerwanego nagrania; harmonogram cykliczny zachowuje następny termin.

### AMC-156-04 — Spacja nie steruje nagrywaniem

Podczas równoczesnego odtwarzania i nagrywania użyj Spacji.

Oczekiwane: zmienia się tylko pauza odsłuchu. Nagrywanie trwa bez luki. AMC nie
udostępnia pozornej pauzy zapisu; bieżące nagranie można zakończyć i rozpocząć
nowe.

## Poprzedni zestaw alpha 155

## Nowości alpha 155

### AMC-155-01 — Rzeczywisty plik transmisji TS

Otwórz przez `Ctrl+O` plik `Vianneya - 2026-08-30 10-16.ts`, rozpocznij
odtwarzanie, przewiń w prawo i użyj `Ctrl+J` do skoku w okolice 4 minuty.

Oczekiwane: słychać dźwięk mimo niepełnego początku obrazu. Program podaje
czas około 7:34, reaguje na przewijanie i nie zawiesza fokusu.

### AMC-155-02 — Niedokończony plik na żądanie

W `Ctrl+O` wybierz filtr Niedokończone nagrania do odzyskania i wskaż kopię
pliku `.part`, `.partial` albo `.amc-partial`, jeżeli taki plik jest dostępny.

Oczekiwane: AMC odtwarza możliwą do odzyskania część. Uszkodzony koniec nie
zawiesza programu. Brak użytecznego dźwięku kończy się zwięzłym błędem.

### AMC-155-03 — Pliki częściowe nie trafiają same do Biblioteki

Odśwież Foldery Biblioteki zawierające plik częściowy.

Oczekiwane: `.part`, `.partial` i `.amc-partial` nie pojawiają się automatycznie.
Można je otworzyć wyłącznie świadomie przez `Ctrl+O`.

## Poprzedni zestaw alpha 154

## Nowości alpha 154

### AMC-154-01 — MP3 128 kb/s z Radia 357

W ustawieniach wybierz MP3 i 128 kb/s. Nagraj co najmniej kilkanaście sekund
Radia 357, zakończ nagranie i otwórz gotowy plik.

Oczekiwane: plik działa od początku do końca, ma 128 kb/s i 44,1 kHz. Jakość
źródła pozostaje taka jak w transmisji 22,05 kHz; program nie twierdzi, że ją
ulepszył.

### AMC-154-02 — MP3 128 kb/s z Radia Białystok

Przy tych samych ustawieniach nagraj Radio Białystok.

Oczekiwane: plik ma 128 kb/s i 48 kHz, a nie 80 kb/s i 24 kHz. Nagranie nie
zawiera przerw ani przyspieszenia.

### AMC-154-03 — Oryginalny HLS

W ustawieniach przejdź po wyborach formatu do opcji Oryginalny.

Oczekiwane: NVDA mówi, że HLS zapisuje plik TS. Krótkie nagranie Trójki w tym
trybie kończy się prawidłowym `.ts`; jest to gotowy kontener, nie plik tymczasowy.

## Poprzedni zestaw alpha 153

## Nowości alpha 153

### AMC-153-01 — Zmiana odsłuchu nie zatrzymuje nagrania

Otwórz stację A, rozpocznij `R`, a następnie zmieniaj stacje Page Up, Page Down
i presetem. Wróć do A i zakończ jej nagranie `R`.

Oczekiwane: nie pojawia się pytanie modalne. Każda stacja zaczyna grać od razu,
a nagranie A trwa w tle aż do ręcznego zatrzymania i tworzy poprawny plik.

### AMC-153-02 — Nagrywanie bez otwierania odtwarzacza

Na stacji B w Bibliotece lub Ulubionych naciśnij `Ctrl+Alt+R`, przejdź do innej
stacji i powtórz skrót na B.

Oczekiwane: pierwsze użycie rozpoczyna nagrywanie B w tle, drugie je finalizuje.
Menu kontekstowe ma tę samą funkcję z czytelną nazwą i skrótem.

### AMC-153-03 — Widok Nagrywane

Uruchom ręczne nagrania dwóch różnych stacji i ewentualnie krótki harmonogram.
Naciśnij `Alt+2` w sesji Radia, poruszaj się po liście i użyj `Alt+Enter`.

Oczekiwane: lista zawiera każdą nagrywaną stację jeden raz i nie przechodzi do
Plików lokalnych. Informacje rozróżniają nagranie ręczne i plan. `R` zatrzymuje
wybrane nagranie ręczne; plan wskazuje, że steruje nim Harmonogram.

### AMC-153-04 — Fokus po Escape

Podczas odtwarzania i podczas co najmniej jednego nagrania w tle kilka razy
wchodź F6 do odtwarzacza i wracaj Escape.

Oczekiwane: fokus zawsze wraca na wybraną listę i reaguje od razu na strzałki.
Nie trafia na ukryty przycisk, nagłówek ani pasek stanu.

### AMC-153-05 — MP3 oraz format Oryginalny

Nagraj Tyflo lub podobną bezpośrednią stację najpierw do MP3, a potem w trybie
Oryginalny. W obu przypadkach poczekaj na rozpoczęcie i zakończ ręcznie.

Oczekiwane: MP3 działa od pierwszej próby. Tryb Oryginalny nie zgłasza błędu
mapowania `0:a:0`, finalizuje właściwy kontener i nie pozostawia `.amc-partial`.

## Poprzedni zestaw alpha 152

## Nowości alpha 152

### AMC-152-01 — Lewo i prawo wybiera część

W dacie i godzinie naciskaj lewo oraz prawo.

Oczekiwane: NVDA mówi nazwę wybranej części i wartość, np. „Minuty: 45”,
„Dzień: 30” albo „Rok: 2026”.

### AMC-152-02 — Góra i dół mówi tylko wartość

Na minutach naciśnij kilka razy górę, a potem dół. Powtórz na pozostałych
częściach daty i godziny.

Oczekiwane: NVDA mówi kolejno tylko wartości, np. „46”, „47”, „46”. Nie powtarza
za każdym razem nazwy Minuty, Dzień, Miesiąc, Rok ani Godzina.

### AMC-152-03 — Szybka zmiana

Naciśnij szybko kilka razy górę lub dół.

Oczekiwane: komunikaty nie nawarstwiają się, ostatnia wartość odpowiada temu,
co jest zapisane w polu, a fokus pozostaje na tym samym segmencie.

## Poprzedni zestaw alpha 151

## Nowości alpha 151

### AMC-151-01 — Wybieranie części daty

W nowym harmonogramie wybierz Nagrywaj: Później. Przejdź do daty i naciskaj
strzałkę w prawo oraz w lewo.

Oczekiwane: NVDA od razu mówi Dzień, Miesiąc albo Rok wraz z bieżącą wartością.
Nie trzeba naciskać `NVDA+strzałka w górę`.

### AMC-151-02 — Zmienianie daty

Na każdym z trzech segmentów naciśnij kilka razy górę i dół.

Oczekiwane: NVDA po każdym naciśnięciu mówi nową wartość właściwej części.
Miesiąc zawiera numer i nazwę, np. „Miesiąc: 8, sierpień”.

### AMC-151-03 — Godzina i minuty

Przejdź do godziny. Lewo/prawo wybierz godzinę albo minuty i zmieniaj je
górą oraz dołem.

Oczekiwane: NVDA mówi aktywną część i każdą nową wartość. Tab przechodzi od
całej godziny do Długości nagrania; nie powstały dodatkowe punkty Tab.

### AMC-151-04 — Pozostała obsługa pola

Sprawdź Escape w dacie i godzinie, a następnie zapisz prawidłowy termin Enterem.

Oczekiwane: Escape anuluje okno, Enter zapisuje, a nowe oznajmianie nie dubluje
komunikatów ani nie zmienia wybranej części kontrolki.

## Poprzedni zestaw alpha 150

## Nowości alpha 150

### AMC-150-01 — Pole Nagrywaj

Na stacji naciśnij `Shift+R` i przejdź do pola Nagrywaj.

Oczekiwane: NVDA czyta pole kombi Nagrywaj oraz tylko dwie wartości:
**Natychmiast** i **Później**. Nie mówi „Termin pierwszego nagrania” ani
„Od razu po zapisaniu”.

### AMC-150-02 — Zachowanie obu wartości

Wybierz kolejno Natychmiast i Później.

Oczekiwane: Natychmiast wyłącza datę i godzinę oraz wyjaśnia, że nagrywanie
rozpocznie się po wybraniu Zapisz. Później uaktywnia segmentową datę i godzinę.

### AMC-150-03 — Nazwa wybudzania

Przejdź Tabem do pola wybudzania.

Oczekiwane: NVDA mówi „Wybudzanie komputera dla tego harmonogramu”. Lista nadal
zawiera trzy czytelne warianty dziedziczenia, włączenia i wyłączenia.

## Poprzedni zestaw alpha 149

## Nowości alpha 149

### AMC-149-01 — Jednoznaczny termin pierwszego nagrania

Na stacji naciśnij `Shift+R` i przejdź do pola Pierwsze nagranie. Sprawdź obie
wartości.

Oczekiwane: są tylko **Od razu po zapisaniu** i **W wybranym terminie**. Pierwsza
wyłącza datę oraz godzinę i wymusza włączenie planu. Druga uaktywnia datę,
godzinę i zwykłe pole Plan włączony. Nie pojawia się techniczna nazwa ani parametr.

### AMC-149-02 — Segmentowa data i godzina

Wybierz W wybranym terminie. W polu daty użyj lewej i prawej strzałki, a potem
góry i dołu. Powtórz w polu godziny.

Oczekiwane: lewo i prawo wybiera dzień, miesiąc lub rok, a góra i dół zmienia
wybraną część. W czasie wybierane są godzina i minuty. NVDA czyta wartości, data
pozostaje prawidłowa, a Tab wychodzi do następnej funkcji formularza.

### AMC-149-03 — Długość nagrania

Przejdź Tabem do Długości w minutach. Wpisz liczbę, zmień ją strzałkami i spróbuj
wpisać wartość spoza zakresu.

Oczekiwane: pole przyjmuje wyłącznie liczbę od 1 do 10080 i nie pozwala zapisać
uszkodzonego tekstu. NVDA nie czyta nazwy klasy ani właściwości kontrolki.

### AMC-149-04 — Escape, Enter i powrót fokusu

Otwórz edycję istniejącego planu. Sprawdź Escape wewnątrz daty, godziny i długości.
Otwórz ponownie, zapisz Enterem, następnie dodaj i usuń plan.

Oczekiwane: edytowany plan zaczyna na dacie; Escape anuluje bez zawieszenia.
Enter zapisuje. Po dodaniu, edycji i usunięciu fokus wraca na listę harmonogramów
i pozostaje na właściwym wierszu albo pierwszym dostępnym wpisie.

### AMC-149-05 — Kolejność Tab i dni tygodnia

Przejdź cały formularz Tabem, wybierz powtarzanie w wybrane dni i zaznacz dwa dni.

Oczekiwane: każde pole formularza występuje raz. Dni tygodnia pozostają jednym
punktem Tab, a poruszanie po nich i zaznaczanie Spacją działa jak w alpha 148.

## Poprzedni zestaw alpha 148

## Nowości alpha 148

### AMC-148-01 — Jedna lista dni tygodnia

W nowym planie ustaw Powtarzanie na W wybrane dni tygodnia i przejdź Tabem do
listy dni. Nawiguj strzałkami i zaznacz kilka dni Spacją.

Oczekiwane: wszystkie dni zajmują jeden punkt tabulacji. NVDA podaje nazwę dnia
i stan zaznaczenia. Spacja zmienia tylko bieżący dzień, bez otwierania nowego okna.

### AMC-148-02 — Przełączanie planu na liście

Otwórz `Ctrl+Alt+Shift+R`, wybierz istniejący plan i naciśnij Spację dwa razy.

Oczekiwane: plan zmienia się między włączonym i wyłączonym, fokus pozostaje na
nim, a komunikat przypomina o zapisaniu. Enter nadal otwiera edycję, Delete usuwa.

### AMC-148-03 — Rozpoczęcie pierwszego nagrania od razu

Naciśnij `Shift+R`. Sprawdź formularz najpierw z zaznaczoną, potem odznaczoną
opcją rozpoczęcia pierwszego nagrania od razu.

Oczekiwane: przy zaznaczeniu pola daty i godziny są nieaktywne, plan jest
włączony i opis wyjaśnia kolejne wystąpienia. Po odznaczeniu data, godzina oraz
pole Plan włączony stają się dostępne.

### AMC-148-04 — Folder planu

Przejdź po polu Folder nagrywania i wybierz obie wartości.

Oczekiwane: wartości brzmią Domyślny folder nagrywania i Folder użytkownika.
Tylko Folder użytkownika uaktywnia ścieżkę oraz przycisk Wybierz.

## Poprzedni zestaw alpha 147

## Nowości alpha 147

### AMC-147-01 — Domyślny folder w ustawieniach

Otwórz `Ustawienia > Radio i nagrywanie` i przejdź do grupy Domyślny folder
nagrywania.

Oczekiwane: jest jedno pole z pełną ścieżką i przycisk Wybierz. Nie ma przycisków
opcji „użyj domyślnego” i „użyj wybranego”. NVDA odczytuje użytkowe nazwy.

### AMC-147-02 — Wybór i zapamiętanie folderu

Naciśnij Wybierz, wskaż inny folder, zapisz ustawienia, zamknij je i otwórz
ponownie.

Oczekiwane: otwiera się standardowe okno wyboru folderu, fokus po wyborze wraca
do pola ścieżki, a pełna wybrana ścieżka pozostaje po ponownym otwarciu ustawień.

### AMC-147-03 — Folder harmonogramu

Otwórz nowy plan przez `Shift+R`. W polu sposobu wyboru folderu przełącz wartości
Domyślny folder nagrywania i Inny folder.

Oczekiwane: przy wartości domyślnej pole ścieżki i Wybierz są nieaktywne. Inny
folder je uaktywnia. Lista nie czyta nazw klas, właściwości ani identyfikatorów.

### AMC-147-04 — Dziedziczenie i wyjątek

Utwórz jeden plan z folderem domyślnym oraz drugi z innym folderem. Zmień folder
domyślny w ustawieniach i ponownie otwórz oba plany.

Oczekiwane: pierwszy plan nadal wybiera Domyślny folder nagrywania i użyje nowej
wartości ogólnej. Drugi zachowuje wybraną własną ścieżkę.

## Poprzedni zestaw alpha 146

## Nowości alpha 146

### AMC-146-01 — Dostępne etykiety formatów

Otwórz `Ustawienia > Radio i nagrywanie` i przejdź po polu Format nagrania.

Oczekiwane: lista zawiera MP3, M4A AAC, FLAC bezstratny, Oryginalny strumień
bez konwersji oraz WAV. Nie pojawiają się identyfikatory enumów ani nazwy klas.
Pole bitrate jest dostępne tylko przy MP3 i AAC.

### AMC-146-02 — Ręczne nagranie FLAC

Wybierz FLAC, uruchom stację, nagraj co najmniej kilkanaście sekund klawiszem
`R`, zakończ i sprawdź `Alt+Enter` oraz utworzony plik.

Oczekiwane: powstaje zakończony plik `.flac`, a odtwarzanie stacji nie przerywa
się. Informacje mówią „FLAC, bezstratny”. Nie pozostaje `.amc-partial`.

### AMC-146-03 — Oryginalny bezpośredni MP3 lub AAC

Wybierz format Oryginalny i nagraj bezpośrednią stację MP3, a następnie AAC.

Oczekiwane: MP3 zapisuje się jako `.mp3`, AAC jako `.aac`; nie następuje ponowne
kodowanie i odsłuch działa równolegle. `Alt+Enter` podaje format i pełny plik.

### AMC-146-04 — Oryginalny HLS

Nagraj działającą stację z adresem M3U8, na przykład jedną ze stacji Polskiego
Radia używających HLS.

Oczekiwane: powstaje jeden prawidłowo zakończony plik `.ts` z dźwiękiem, nie
sam manifest M3U8 ani zbiór luźnych segmentów. Zatrzymanie `R` nie zawiesza AMC.

### AMC-146-05 — Harmonogram i błąd komponentu

Wykonaj krótki plan w FLAC lub formacie oryginalnym. Jeżeli FFmpeg jest chwilowo
niedostępny, sprawdź też komunikat błędu.

Oczekiwane: harmonogram stosuje wybrany format ogólny i właściwy folder. Brak
FFmpeg albo wadliwy strumień nie pozostawia pustego pliku końcowego i nie
zatrzymuje innego odtwarzania.

## Poprzedni zestaw alpha 145

## Nowości alpha 145

### AMC-145-01 — Preset odtwarzany w tle

Pozostaw wyłączone ustawienie „Po uruchomieniu presetu otwieraj odtwarzacz”.
Ustaw fokus na elemencie innym niż cel presetu i uruchom zajęty preset bezpośrednim
`Ctrl+Shift+cyfra`.

Oczekiwane: element presetu zaczyna grać, ale pozostajesz w tym samym widoku i
na tej samej pozycji listy. NVDA podaje nazwę uruchomionego elementu tylko raz.

### AMC-145-02 — Preset otwierający odtwarzacz

Włącz opcję w `Ustawienia > Ogólne > Odtwarzanie`, wróć na listę i uruchom
ten sam preset.

Oczekiwane: otwiera się odtwarzacz z celem presetu. Escape wraca do widoku i
pozycji, z których wywołano preset, z uwzględnieniem osobnego ustawienia
podążania fokusu za odtwarzaniem.

### AMC-145-03 — Zmiana presetu w odtwarzaczu

Mając otwarty odtwarzacz, uruchom drugi zajęty preset przy wyłączonej opcji.

Oczekiwane: odtwarzacz pozostaje otwarty i pokazuje nowy element. Ustawienie
nie wyrzuca użytkownika z już otwartego odtwarzacza.

### AMC-145-04 — Lista presetów i różne sesje

Powtórz test przez `Ctrl+Alt+P` oraz w Radiu i Plikach lokalnych.

Oczekiwane: Enter na odtwarzalnym presecie respektuje tę samą opcję co skrót
bezpośredni. Folder, album i playlista nadal otwierają zawartość.

## Poprzedni zestaw alpha 144

## Nowości alpha 144

### AMC-144-01 — Shift+R w bieżącym widoku

Wejdź kolejno do Biblioteki Radia, Ulubionych, Historii oraz otwartej playlisty.
Na wybranej stacji naciśnij `Shift+R` i poruszaj się po polu Stacja strzałkami.

Oczekiwane: podświetlona stacja jest wybrana od razu. Strzałki pokazują tylko
stacje z widoku, z którego otwarto edytor, i zachowują jego kolejność. Nie ma
obcych wyników dawnych wyszukiwań Radio Browser.

### AMC-144-02 — Ręczne nagranie na listach

Otwórz stację, naciśnij `R`, wróć Escapem do listy i przejdź do innego widoku,
w którym występuje ta sama stacja.

Oczekiwane: odtwarzana stacja jest czytana „nazwa, nagrywanie, odtwarzany”. Gdy
nie jest bieżącym elementem, ale nadal jest zapisywana, mówi „nazwa, nagrywanie”.
Pozostałe stacje nie otrzymują tego stanu.

### AMC-144-03 — Szczegóły ręcznego nagrania

Podczas nagrywania naciśnij `Alt+Enter` na tej stacji w liście i w odtwarzaczu.
Po zatrzymaniu `R` otwórz informacje ponownie.

Oczekiwane: sekcja Nagrywanie podaje stan, rodzaj ręczny, godzinę rozpoczęcia,
ręczne zakończenie, format i pełny plik. Po zakończeniu mówi, że stacja nie jest
nagrywana, a prefiks znika ze wszystkich widoków.

### AMC-144-04 — Nagranie z harmonogramu w tle

Uruchom krótki plan stacji A, słuchając stacji B. Otwórz widok zawierający A i
jej `Alt+Enter`; potem poczekaj na zakończenie planu.

Oczekiwane: tylko A ma po nazwie stan „nagrywanie”. Informacje podają start, planowane
zakończenie, format i planowany folder. B pozostaje odtwarzana bez fałszywego
stanu nagrywania. Po zakończeniu stan A znika.

### AMC-144-05 — Pełny menedżer planów

Otwórz `Ctrl+Alt+Shift+R`, dodaj i edytuj plan oraz sprawdź pole Stacja.

Oczekiwane: dostępne są zapisane stacje Biblioteki i stacje istniejących
planów, lecz nieużywane, ukryte wyniki Radio Browser nie zaśmiecają listy.

## Poprzedni zestaw alpha 143

## Nowości alpha 143

### AMC-143-01 — Dostępność ustawień Radia

Otwórz Ustawienia i kartę „Radio i nagrywanie”. Przejdź Tabem po wszystkich
polach, rozwiń pola formatu i bitrate'u, poruszaj się strzałkami, anuluj okno,
a następnie otwórz je ponownie i zapisz zmiany.

Oczekiwane: NVDA czyta tylko pełne, polskie etykiety. Nie pojawiają się nazwy
klas, rekordy w nawiasach klamrowych, identyfikatory enumów ani nazwy
właściwości. Początkowo wybrany element i każda zmiana są oznajmiane, a po
Zapisz lub Anuluj fokus wraca do AMC.

### AMC-143-02 — Ogólny folder nagrań

Najpierw wybierz domyślny folder programu, nagraj ręcznie kilkanaście sekund
klawiszem `R` i sprawdź położenie pliku. Potem wybierz własny folder, zapisz
ustawienia i wykonaj drugie nagranie.

Oczekiwane: pierwsze nagranie trafia do „Muzyka, AMC — Nagrania radia”, drugie
do wybranego folderu. Ścieżka pozostaje zapamiętana po ponownym uruchomieniu.

### AMC-143-03 — Folder ogólny i własny dla harmonogramu

Utwórz dwa krótkie plany `Shift+R`: pierwszy z opcją użycia ogólnego folderu,
drugi z własnym folderem. Otwórz każdy ponownie do edycji przed terminem.

Oczekiwane: wybrany wariant i ścieżka są zachowane, a pliki trafiają do
właściwych folderów. Pole własnej ścieżki jest nieaktywne przy wariancie
ogólnym. Sam wybór folderu nie zmienia ogólnego ustawienia programu.

### AMC-143-04 — MP3, M4A/AAC i WAV

W ustawieniach wybierz kolejno MP3 192 kb/s, M4A/AAC 128 lub 160 kb/s oraz
WAV. Za każdym razem nagraj tę samą stację przez kilkanaście sekund.

Oczekiwane: powstają odpowiednio pliki `.mp3`, `.m4a` i `.wav`; każdy daje się
odtworzyć, ma rozsądny czas i nie pozostaje plik `.amc-partial`. Przy WAV pole
bitrate'u jest nieaktywne. WAV jest wyraźnie większy, co jest prawidłowe.

### AMC-143-05 — HLS i nietypowe parametry stacji

Nagraj ręcznie oraz przez krótki plan stację HLS, na przykład Trójkę albo
transmisję `m3u8`. Powtórz próbę dla stacji podającej 24 kHz, jeśli jest
dostępna. Podczas planu słuchaj innej stacji.

Oczekiwane: AMC nagrywa zdekodowany dźwięk także z HLS do wybranego formatu;
plan działa w tle i nie zmienia odsłuchu. Format wejściowy nie jest bezmyślnie
kopiowany do pliku, więc zmiany segmentów HLS nie uszkadzają nagrania. Jeżeli
dany HLS wymaga FFmpeg, program podaje kontrolowany błąd, a nie zawiesza się.

### AMC-143-06 — Globalne i indywidualne wybudzanie

Włącz globalne wybudzanie w Ustawieniach. Utwórz trzy plany: zgodny z
ustawieniem ogólnym, zawsze wybudzający i nigdy niewybudzający. Otwórz je
ponownie i sprawdź odczyty pól. Faktyczną próbę uśpienia wykonaj na końcu,
pozostawiając AMC uruchomiony i ustawiając termin kilka minut później.

Oczekiwane: przy wariancie dziedziczonym edytor mówi również, jaki jest obecny
stan ogólny. Wszystkie trzy wybory są zachowane. Obsługiwany komputer budzi się
przed planem; blokada wybudzania przez sprzęt, baterię albo plan zasilania nie
powoduje uszkodzenia harmonogramu.

### AMC-143-07 — Niedostępny folder i bezpieczny zapis

Wskaż własny folder harmonogramu na odłączanym albo synchronizowanym dysku,
następnie przed terminem uczyń go niedostępnym. Powtórz z niedostępnym folderem
ogólnym.

Oczekiwane: AMC sprawdza rzeczywistą możliwość zapisu i wybiera kolejny
bezpieczny folder: planowy, ogólny, a na końcu systemowy. Program nie publikuje
pustego lub niedokończonego nagrania i nie ujawnia technicznego wyjątku NVDA.

### AMC-143-08 — Regresja nagrywania i presetów

Sprawdź `R`, `Shift+R`, `Ctrl+Alt+Shift+R`, `Ctrl+0` oraz `Ctrl+Shift+0` na
liście i w odtwarzaczu.

Oczekiwane: `R` steruje nagrywaniem słyszanej stacji, `Shift+R` otwiera plan,
`Ctrl+Alt+Shift+R` listę planów, `Ctrl+0` listę sesji, a `Ctrl+Shift+0` preset 0.
Żaden skrót nie przejmuje roli innego.

## Poprzedni zestaw alpha 142

## Nowości alpha 142

### AMC-142-01 — Preset 0 z NVDA

Przy uruchomionym NVDA ustaw preset pod klawiszem `0`. W aktywnym AMC naciśnij
`Ctrl+0`, wróć Escapem, a następnie naciśnij `Ctrl+Shift+0`. Powtórz próbę na
liście i w odtwarzaczu. Na koniec przejdź do innej aplikacji i użyj tam tej
samej kombinacji.

Oczekiwane: `Ctrl+0` otwiera listę sesji, a `Ctrl+Shift+0` uruchamia preset 0 i
nie milczy. AMC nie przejmuje skrótu, kiedy jego okno nie jest aktywne.

### AMC-142-02 — R nagrywa aktualnie słyszaną stację

Otwórz stację w odtwarzaczu. Sprawdź menu kontekstowe, naciśnij `R`, odczekaj
kilkanaście sekund i naciśnij `R` ponownie.

Oczekiwane: menu zawiera rozpoczęcie lub zakończenie nagrywania z opisem `R`.
Program mówi o początku i końcu, a powstały MP3 można odtworzyć.

### AMC-142-03 — Zmiana stacji podczas ręcznego nagrywania

Ten starszy scenariusz został zastąpiony przez AMC-153-01. Nagranie działa teraz
w niezależnym tle, więc zmiana stacji nie wymaga pytania i nie kończy zapisu.

### AMC-142-04 — Shift+R rozpoczyna nagranie czasowe

Na stacji w liście naciśnij `Shift+R`. Zostaw zaznaczone rozpoczęcie natychmiast,
ustaw jedną minutę i zapisz. Podczas nagrania słuchaj innej stacji.

Oczekiwane: nagranie rusza od razu w tle, nie zmienia odsłuchu i po minucie
powstaje poprawnie zakończony MP3.

### AMC-142-05 — Termin przyszły i menu kontekstowe

Ponownie użyj `Shift+R`, wyłącz rozpoczęcie natychmiast i ustaw przyszły termin.
Sprawdź też menu kontekstowe stacji oraz odtwarzacza.

Oczekiwane: można zapisać przyszły plan i powtarzanie. Menu listy zawiera
nagranie czasowe i harmonogram, a menu odtwarzacza dodatkowo nagrywanie `R`.

## Poprzedni zestaw alpha 141

## Nowości alpha 141

### AMC-141-01 — Ctrl+0 oraz Ctrl+Shift+0 są rozłączne

W Radiu przypisz Radio Emaus do miejsca oznaczonego klawiszem `0`. Naciśnij
`Ctrl+0`, zamknij listę sesji Escapem, a następnie naciśnij `Ctrl+Shift+0` na
liście Biblioteki, w Ulubionych, na playliście i w odtwarzaczu.

Oczekiwane: `Ctrl+0` zawsze otwiera listę sesji. `Ctrl+Shift+0` nigdy jej nie
otwiera, lecz od razu uruchamia Radio Emaus i mówi „Preset 0”. W innej sesji z
pustym miejscem mówi „Preset 0 pusty” zamiast milczeć albo otwierać sesje.

### AMC-141-02 — Dostępność listy i edytora harmonogramu

Na wybranej stacji naciśnij `Ctrl+Alt+Shift+R`. Na pustej lub istniejącej
liście użyj Insert, przejdź Tabem przez wszystkie pola, rozwiń każde pole kombi
i poruszaj się strzałkami. Anuluj edytor, otwórz go ponownie i zapisz wpis.

Oczekiwane: fokus zaczyna na liście planów, a w nowym wpisie na wyborze stacji.
NVDA czyta wyłącznie polskie etykiety. Nie pojawiają się nazwy klas, rekordy w
nawiasach klamrowych ani identyfikatory wartości. Anulowanie i zapis wracają na
właściwy wpis listy.

### AMC-141-03 — Jednorazowe nagranie w tle

Utwórz plan wybranej stacji na 2–3 minuty w przyszłości, o długości jednej
minuty. Przed terminem rozpocznij słuchanie innej stacji albo lokalnego pliku i
nie zamykaj AMC.

Oczekiwane: o terminie AMC oznajmia rozpoczęcie planu, lecz nie przełącza ani
nie słychać zaplanowanej stacji. Po minucie oznajmia zakończenie i powstaje
poprawnie zamknięty MP3 w folderze nagrań. Bieżące odtwarzanie działa dalej.

### AMC-141-04 — Stan, zatrzymanie i kilka planów

Podczas aktywnego planu ponownie otwórz harmonogram. Jeżeli to możliwe, ustaw
drugi, nakładający się plan. Usuń jeden aktywny wpis i potwierdź ostrzeżenie.

Oczekiwane: aktywny wiersz mówi „nagrywanie trwa”. Plany mogą nagrywać
równolegle, nie mieszając plików. Usunięcie aktywnego wpisu zatrzymuje tylko
jego zapis i finalizuje dotychczasową część; drugi plan i odsłuch pozostają.

### AMC-141-05 — Trwałość i powtarzanie

Zapisz plan codzienny oraz plan na wybrane dni tygodnia. Zamknij i uruchom AMC,
otwórz harmonogram, edytuj jeden wpis i zapisz go ponownie.

Oczekiwane: wpisy, stacje, terminy, długości, dni, foldery i ustawienia
wybudzania są zachowane. Po zakończeniu plan cykliczny przechodzi do najbliższego
właściwego dnia, a jednorazowy znika. Fokus po edycji wraca na ten sam wpis.

### AMC-141-06 — Folder zastępczy i bezpieczne pominięcie

Opcjonalnie wskaż dla planu folder, który przed terminem stanie się niedostępny.
Uruchom AMC również raz w trakcie nadal trwającego okna oraz raz dopiero po jego
całkowitym końcu.

Oczekiwane: niedostępny folder nie przerywa planu — MP3 trafia do ogólnego
folderu nagrań. Start w trakcie okna zapisuje tylko pozostały czas. Całkowicie
pominięty termin nie rozpoczyna spóźnionego pełnego nagrania; wpis cykliczny
przechodzi do następnego terminu.

## Poprzedni zestaw alpha 140

## Nowości alpha 140

### AMC-140-01 — Bezpośredni preset 0

W Radiu przypisz Radio Emaus do miejsca 10, oznaczonego klawiszem `0`.
Naciśnij `Ctrl+Shift+0` kolejno w Bibliotece, Ulubionych, playliście oraz
w otwartym odtwarzaczu.

Oczekiwane: za każdym razem zostaje uruchomione Radio Emaus, program mówi
„Preset 0” i nazwę stacji, nie milczy i nie przełącza sesji. W innej sesji,
w której to miejsce jest wolne, program mówi „Preset 0 pusty”.

### AMC-140-02 — Podsumowanie czasu playlisty

Otwórz listę playlist zawierającą „Biskup” oraz co najmniej jedną playlistę
stacji radiowych.

Oczekiwane: przy „Biskup” po liczbie elementów jest podany łączny czas.
Jeżeli tylko część plików ma znany czas, program mówi „znany czas” i „część
bez danych”. Playlista składająca się wyłącznie ze stacji mówi „transmisje na
żywo” i nie podaje fikcyjnej długości.

### AMC-140-03 — Otwieranie i odtwarzanie całej playlisty

Na nazwie lokalnej playlisty naciśnij Enter, wróć do listy playlist, a następnie
naciśnij `Ctrl+Enter`.

Oczekiwane: Enter tylko otwiera zawartość. `Ctrl+Enter` odtwarza pierwszy
dostępny element, a Page Up i Page Down przechodzą po elementach tej playlisty.
Escape wraca do otwartej zawartości playlisty.

### AMC-140-04 — Działania zbiorcze na nazwie playlisty

Otwórz menu kontekstowe na nazwie playlisty. Sprawdź właściwości `Alt+Enter`,
dodawanie zawartości do Ulubionych i Biblioteki oraz — w Plikach lokalnych —
do Kolejki i jako następne.

Oczekiwane: nazwy poleceń jednoznacznie mówią o „zawartości playlisty”.
Właściwości opisują playlistę, a nie przypadkowy aktualnie odtwarzany plik.
W Radiu polecenia Kolejki i odtwarzania jako następne są ukryte.

### AMC-140-05 — Widoki lokalne z wnętrza playlisty

Wejdź do lokalnej playlisty i naciśnij kolejno `Alt+1`, `Alt+2` i `Alt+3`.

Oczekiwane: skróty świadomie opuszczają playlistę i otwierają odpowiednio
Foldery, Wszystkie pliki oraz Kolejność użytkownika w tej samej sesji. W Radiu
te skróty nadal nie przechodzą do Plików lokalnych.

### AMC-140-06 — Parametry stacji na pasku

Odtwórz Tyflo oraz Radio Białystok i odczytaj pasek przez `NVDA+End`.

Oczekiwane: Tyflo może podać `44,1 kHz` bez bitrate'u, jeżeli wiarygodny bitrate
nie został rozpoznany. Radio Białystok może podać `160 kb/s, 24 kHz`: pierwsza
wartość pochodzi z danych katalogu, druga z faktycznego formatu dekodera.
„Za transmisją” i „bufor” są podawane tylko podczas aktywnego odtwarzania.

## Poprzedni zestaw alpha 139

## Nowości alpha 139

### AMC-139-01 — Te same miejsca są niezależne w każdej sesji

W Plikach lokalnych przypisz element do presetu 1. Przejdź kolejno do Radia,
TIDAL, Apple Music i WiiM. W każdej sesji sprawdź preset 1, a następnie przypisz
do niego inny dostępny element demonstracyjny lub stację.

Oczekiwane: każda sesja ma własny preset 1. `Ctrl+Shift+1` uruchamia albo
otwiera tylko cel aktywnej sesji i nigdy sam nie przełącza usługi.

### AMC-139-02 — Plik, folder, album i playlista

W Plikach lokalnych przypisz do różnych miejsc: zwykły plik, folder, album
rozpoznany z folderu i playlistę AMC. Wywołaj każdy cel skrótem bezpośrednim
oraz Enterem z listy `Ctrl+Alt+P`.

Oczekiwane: plik zaczyna się odtwarzać. Folder otwiera wskazany poziom,
a album i playlista otwierają swoją zawartość bez samoczynnego odtwarzania.
Escape wraca do prawidłowego miejsca i żaden wiersz nie ujawnia technicznej
nazwy klasy ani pól rekordu.

### AMC-139-03 — Preset z wyniku wyszukiwania

Wyszukaj jeden plik albo stację. Na wyniku użyj `Ctrl+Alt+Shift+P`, przypisz
wolne miejsce i uruchom je później w tej samej sesji.

Oczekiwane: okno wyszukiwania przekazuje dokładnie zaznaczony wynik, tryb
przypisania ma nazwę bieżącej sesji, a zapisane miejsce działa po ponownym
uruchomieniu AMC.

### AMC-139-04 — Zachowanie dotychczasowych presetów po aktualizacji

Po uruchomieniu alpha 139 sprawdź presety zapisane wcześniej w Radiu i Plikach
lokalnych, w szczególności miejsca 10–12.

Oczekiwane: wcześniejsze przypisania pozostają. Radio zostało jednorazowo
przeniesione do wspólnego magazynu bez nadpisania istniejących miejsc; lista
nadal mówi „Preset numer 10, klawisz 0”, a `Ctrl+Shift+0` używa nazwy
fizycznego klawisza.

### AMC-139-05 — Niedostępny cel nie niszczy presetu

Przypisz lokalny plik lub folder, zamknij AMC, a następnie czasowo odłącz jego
źródło. Uruchom program i wywołaj preset.

Oczekiwane: AMC krótko informuje o niedostępnym celu, nie zawiesza się, nie
usuwa przypisania i nie przełącza sesji. Po ponownym udostępnieniu źródła
to samo miejsce znów działa.

## Poprzedni zestaw alpha 138

## Nowości alpha 138

### AMC-138-01 — Pusty preset pod Ctrl+Shift+0

W Plikach lokalnych i w Radiu pozostaw miejsce przypisane do klawisza `0` puste, a następnie naciśnij `Ctrl+Shift+0` na liście i w odtwarzaczu.

Oczekiwane: program za każdym razem mówi „Preset 0 pusty” i podaje sposób przypisania. Nie milczy, nie uruchamia innego miejsca i nie przełącza sesji.

### AMC-138-02 — Cyfry na liście presetów

Otwórz listę `Ctrl+Alt+P`. Naciśnij kolejno `5`, `8`, `0`, minus i znak równości, nie zatwierdzając Enterem.

Oczekiwane: fokus przechodzi odpowiednio do presetów 5, 8, 10, 11 i 12, a NVDA odczytuje pełną etykietę wybranego miejsca. Sama cyfra lub znak niczego nie uruchamia, nie zapisuje ani nie nadpisuje.

### AMC-138-03 — Brak regresji zajętych presetów

Przypisz stacje lub pliki do miejsc 1, 9 i 10. Uruchom je skrótami `Ctrl+Shift+1`, `Ctrl+Shift+9` i `Ctrl+Shift+0`, a następnie Enterem z listy presetów.

Oczekiwane: każdy skrót oraz lista uruchamiają właściwy element w bieżącej sesji. Lista nadal nazywa dziesiąte miejsce „Preset numer 10, klawisz 0”.

## Poprzedni zestaw alpha 137

## Nowości alpha 137

### AMC-137-01 — Lista presetów w Plikach lokalnych

W sesji Pliki lokalne otwórz kolejno Foldery (`Alt+1`), Wszystkie pliki (`Alt+2`) i Ulubione (`Ctrl+U`). W każdym widoku naciśnij `Ctrl+Alt+P`, przejdź po liście, po czym zamknij ją Escape.

Oczekiwane: zawsze otwiera się lista „Presety — Pliki lokalne” z dwunastoma jednoznacznie nazwanymi miejscami. Nie pojawiają się określenia radiowe ani techniczne dane obiektu. Escape wraca na element, z którego lista została otwarta.

### AMC-137-02 — Preset folderu Biblioteki

W `Alt+1` wskaż folder na dowolnym poziomie i naciśnij `Ctrl+Alt+Shift+P`. Wybierz wolne miejsce, zatwierdź Enterem, przejdź do innego widoku lub folderu i wywołaj zapisane miejsce przez `Ctrl+Shift+cyfra`.

Oczekiwane: przypisanie podaje nazwę folderu. Wywołanie nie uruchamia dźwięku i nie zmienia sesji, lecz otwiera dokładnie zapisany folder w widoku Foldery. Fokus trafia na jego pierwszą pozycję albo na czytelną pustą listę.

### AMC-137-03 — Preset pliku z Biblioteki i Ulubionych

Przypisz jeden plik z płaskiej Biblioteki, a drugi z Ulubionych. Wywołaj je bezpośrednimi skrótami oraz Enterem z listy `Ctrl+Alt+P`; sprawdź również przypisanie aktualnego pliku z otwartego odtwarzacza.

Oczekiwane: plik od razu rozpoczyna odtwarzanie, a Page Up i Page Down korzystają z właściwego lokalnego kontekstu. Preset jest ten sam niezależnie od widoku, z którego przypisano plik. Skrót nie przełącza do Radia.

### AMC-137-04 — Trwałość i niedostępny cel

Zamknij i ponownie uruchom AMC, po czym sprawdź zapisane presety. Następnie czasowo odłącz jeden Folder Biblioteki albo usuń przypisany plik z Biblioteki i spróbuj wywołać jego miejsce.

Oczekiwane: po ponownym uruchomieniu przypisania pozostają. Niedostępny cel nie powoduje zawieszenia, przełączenia sesji ani usunięcia presetu; program przekazuje krótki komunikat, że pliku albo folderu nie ma już w Bibliotece.

## Poprzedni zestaw alpha 136

## Nowości alpha 136

### AMC-136-01 — Pasek stanu na liście i w odtwarzaczu

W Radiu sprawdź `NVDA+End` na liście, podczas odtwarzania oraz po powrocie Escape z odtwarzacza. Powtórz po zmianie stacji przez Page Up lub Page Down.

Oczekiwane: NVDA za każdym razem odczytuje jedną aktualną treść paska. Nie milczy, nie powtarza tekstu i nie dodaje samej nazwy „Pasek stanu odtwarzania”. Fokus oraz skróty głównego widoku pozostają aktywne.

### AMC-136-02 — Jednoznaczne presety 10–12

Otwórz w Radiu `Ctrl+Alt+P` i przejdź do ostatnich trzech miejsc. Sprawdź także bezpośrednie skróty zajętych miejsc `Ctrl+Shift+0`, `Ctrl+Shift+-` i `Ctrl+Shift+=`.

Oczekiwane: lista mówi odpowiednio „Preset numer 10, klawisz 0”, „Preset numer 11, klawisz minus” i „Preset numer 12, klawisz znak równości”, po czym nazwę stacji albo informację, że miejsce jest puste. Skrót zajętego miejsca uruchamia jego stację i nie przełącza sesji.

### AMC-136-03 — Granica playlisty i presetu

Utwórz playlistę kilku stacji przez `Ctrl+P` i przejdź po niej Enterem oraz Page Up/Page Down. Osobno przypisz jedną z tych stacji do presetu przez `Ctrl+Alt+Shift+P`.

Oczekiwane: playlista jest trwałym widokiem wielu stacji, natomiast obecny preset wskazuje dokładnie jedną stację. Wywołanie presetu nie usuwa playlisty, nie zmienia jej składu i nie przedstawia całej playlisty jako zajętego miejsca. Obsługa całej playlisty przez pojedynczy preset jest zaplanowana po zakończeniu testów playlist.

## Poprzedni zestaw alpha 135

## Nowości alpha 135

### AMC-135-01 — Playlisty stacji radiowych

W Radiu naciśnij `Ctrl+P`, utwórz playlistę Insertem, wróć do Biblioteki i zaznacz jedną albo kilka stacji. Naciśnij `Ctrl+Shift+P`, dodaj je do playlisty, ponownie otwórz `Ctrl+P` i wejdź do niej Enterem.

Oczekiwane: Radio korzysta ze zwykłych playlist AMC. Lista zawiera wyłącznie stacje przypisane do tej playlisty. Page Up, Page Down, Delete, ręczna kolejność i ponowne uruchomienie programu zachowują się tak samo jak w innych sesjach. Usunięcie wpisu albo playlisty nie usuwa stacji z Biblioteki.

### AMC-135-02 — Playlisty w wyszukiwaniu i odtwarzaczu

Wyszukaj stacje, zaznacz kilka wyników Shiftem i naciśnij `Ctrl+Shift+P`. Powtórz dla aktualnie odtwarzanej stacji w odtwarzaczu.

Oczekiwane: menedżer playlist przyjmuje zaznaczone stacje z jednej sesji. Po zapisaniu fokus wraca do właściwego okna, a preset nie zostaje zmieniony.

### AMC-135-03 — Presety mają tylko skróty uniwersalne

W Radiu sprawdź kolejno `Ctrl+P`, `Ctrl+Shift+P`, `Ctrl+Alt+P` oraz `Ctrl+Alt+Shift+P`. Powtórz na liście i w odtwarzaczu, a następnie sprawdź menu Widok, menu kontekstowe i paletę poleceń.

Oczekiwane: pierwsze dwa skróty zawsze dotyczą playlist. `Ctrl+Alt+P` otwiera listę presetów, a `Ctrl+Alt+Shift+P` bezpieczny tryb utworzenia lub przypisania presetu. Nazwy dostępnościowe podają dokładnie te same skróty i nie ujawniają nazw technicznych.

### AMC-135-04 — Pasek stanu po zmianie widoków

Przejdź kilka razy między Biblioteką, playlistą, presetami i odtwarzaczem, po czym na każdym z głównych widoków naciśnij `NVDA+End`.

Oczekiwane: pasek pozostaje dostępny i za każdym razem przekazuje jedną aktualną treść. Samo otwieranie playlist lub presetów nie usuwa go ani nie przenosi na niego fokusu.

## Poprzedni zestaw alpha 134

## Nowości alpha 134

### AMC-134-01 — Pasek stanu odnajdywany przez NVDA

Na liście i w odtwarzaczu naciśnij `NVDA+End`, następnie zmień stację lub utwór i powtórz test.

Oczekiwane: NVDA za każdym razem czyta jedną aktualną treść paska. Nie mówi „Pasek stanu odtwarzania”, nie powtarza całego tekstu i nie przenosi fokusu.

### AMC-134-02 — Fokus podąża za odtwarzaniem

W `Ustawienia > Ogólne > Odtwarzanie` pozostaw włączone „Po wyjściu z odtwarzacza ustaw fokus na aktualnie odtwarzanym elemencie”. Uruchom element z listy, zmień go kilka razy przez Page Up lub Page Down i naciśnij Escape. Następnie wyłącz opcję i powtórz.

Oczekiwane: przy włączonej opcji fokus wraca na element aktualnie wybrany w odtwarzaczu, jeżeli jest widoczny w bieżącym widoku. Przy wyłączonej wraca na element zaznaczony przed wejściem do odtwarzacza. Żaden wariant nie przełącza widoku ani sesji.

### AMC-134-03 — Uniwersalne skróty presetów w Radiu

W Radiu sprawdź `Ctrl+Alt+P` i `Ctrl+Alt+Shift+P`, również z otwartego odtwarzacza. Porównaj z `Ctrl+P` i `Ctrl+Shift+P`.

Oczekiwane: oba skróty listy otwierają te same presety, a oba skróty przypisania otwierają bezpieczny tryb tworzenia presetu. Fokus po zamknięciu wraca do właściwego miejsca. Skróty z Altem nie uruchamiają menu głównego.

## Poprzedni zestaw alpha 133

## Nowości alpha 133

### AMC-133-01 — Numery i skróty wszystkich presetów

W Radiu otwórz `Ctrl+P` i przejdź po pozycjach 9–12. Osobno otwórz `Ctrl+Shift+P` i wskaż klawiszami miejsca 10, 11 i 12.

Oczekiwane: NVDA mówi kolejno numer miejsca i skrót. Ostatnie pozycje są jednoznaczne: „Preset 10, skrót Ctrl+Shift+0”, „Preset 11, skrót Ctrl+Shift+minus” oraz „Preset 12, skrót Ctrl+Shift+znak równości”. Żaden znak ani numer nie ginie, nie pojawia się techniczna nazwa rekordu.

### AMC-133-02 — Zwięzła zmiana stacji

Uruchom stację Enterem, następnie zmień ją kilka razy przez Page Up i Page Down oraz przez zajęte presety.

Oczekiwane: przy każdym wyborze AMC mówi samą nazwę stacji. Nie dodaje słów „Łączenie”, „Odtwarzanie” ani „stacja”. Komunikat o błędzie albo przekroczeniu czasu nadal pojawia się, jeśli dźwięk rzeczywiście nie ruszy.

## Poprzedni zestaw alpha 132

## Nowości alpha 132

### AMC-132-01 — Pasek bez zbędnego wstępu

W głównym oknie, na liście i w odtwarzaczu naciśnij `NVDA+End`. Powtórz podczas odtwarzania pliku oraz radia.

Oczekiwane: NVDA czyta aktualny stan, czas i dostępne parametry. Nie poprzedza ich zdaniem „Pasek stanu odtwarzania” ani nie powtarza treści. Fokus i działanie klawiatury pozostają bez zmian.

### AMC-132-02 — Nazwa stacji i utwór w tytule okna

Uruchom bezpośrednią stację ICY/MP3 albo OGG, która podaje nazwę bieżącego utworu. Odczytaj tytuł okna przez `NVDA+T`, odczekaj na zmianę utworu i odczytaj ponownie.

Oczekiwane: tytuł zaczyna się od nazwy stacji, a następnie podaje bieżący utwór lub audycję. Aktualizacja nie wywołuje osobnego automatycznego komunikatu i nie zmienia nazwy stacji na liście.

### AMC-132-03 — Brak i czyszczenie metadanych

Przełącz ze stacji pokazującej utwór na stację bez takich danych, szybko zmień antenę jeszcze raz, a następnie zatrzymaj radio.

Oczekiwane: nowa stacja nigdy nie dziedziczy tytułu poprzedniego utworu. Gdy strumień lub użyty dekoder nie udostępnia metadanych, w tytule pozostaje sama nazwa stacji oraz zwykły kontekst okna. Stary tytuł nie wraca po zatrzymaniu ani błędzie połączenia.

## Poprzedni zestaw alpha 131

## Nowości alpha 131

### AMC-131-01 — Kopiowanie jednego i wielu presetów

Otwórz `Ctrl+P`. Zaznacz Shiftem kilka miejsc, w tym przynajmniej jedno puste, i sprawdź `Ctrl+C` oraz `Ctrl+Shift+C`.

Oczekiwane: pierwsze polecenie kopiuje po jednej nazwie stacji w wierszu. Drugie kopiuje naprzemiennie nazwę i adres. Puste miejsca są pomijane, a zawartość i przypisania presetów nie zmieniają się.

### AMC-131-02 — Plik MP4 odtwarzany jako audio

Przez `Ctrl+O` dodaj MP4 zawierający dźwięk i obraz. Odtwórz go, przewiń, zmień prędkość, sprawdź lewą strzałkę i `Alt+Enter`.

Oczekiwane: nie otwiera się obraz ani dodatkowe okno. Działają zwykłe funkcje odtwarzacza, a właściwości mówią „ścieżka audio z pliku wideo”. Rozmiar całego MP4 nie jest podawany jako bitrate audio.

### AMC-131-03 — Pozostałe kontenery wideo

Jeżeli masz dostępne pliki MOV, M4V, MKV, WebM, AVI, WMV, MPEG albo M2TS ze ścieżką audio, dodaj je pojedynczo lub przez Folder Biblioteki.

Oczekiwane: pliki są indeksowane i AMC próbuje odtworzyć wyłącznie ścieżkę audio. Nieobsługiwany przez Windows kodek kończy się zwykłym komunikatem bez zawieszenia. Program nie obiecuje dźwięku w kontenerze, który nie ma ścieżki audio.

### AMC-131-04 — Ostatnia sesja i jej kolejność

W Ustawieniach wybierz uruchamianie ostatniej używanej sesji. Na liście kolejności sesji przesuń wybraną pozycję przez `Alt+góra/dół`, zapisz, przejdź do Radia i uruchom AMC ponownie. Powtórz z ustawieniem „Lista sesji”.

Oczekiwane: ruch jest oznajmiany wraz z nowym `Ctrl+numer`. Pierwszy tryb wraca bezpośrednio do Radia i zapamiętanego widoku. Drugi otwiera listę sesji z fokusem na Radiu, a nie zawsze na pierwszej pozycji.

### AMC-131-05 — Precyzyjna częstotliwość 357

Na Radiu 357 użyj lewej strzałki i rozpocznij odtwarzanie.

Oczekiwane: bieżący wariant, który dekoder otwiera jako 22050 Hz, jest czytany jako `22,05 kHz`, nie `22,5 kHz` ani `44,1 kHz`. Bitrate pozostaje oddzielną wartością około 128 kb/s.

## Poprzedni zestaw alpha 130

## Nowości alpha 130

### AMC-130-01 — Bezpieczne przypisanie wolnego presetu

W Radiu wybierz stację spoza Biblioteki i naciśnij `Ctrl+Shift+P`. Posłuchaj informacji o pierwszym wolnym miejscu, naciśnij jego cyfrę lub znak i zatwierdź Enterem. Zamknij i ponownie uruchom AMC.

Oczekiwane: początkowo czytany jest pełny użytkowy wiersz, bez nazwy klasy i pól technicznych. Enter zapisuje preset, zachowuje stację w Bibliotece radia i po restarcie nadal działa. Escape zamiast Entera nie zapisuje stacji ani presetu.

### AMC-130-02 — Ochrona zajętego miejsca

Wybierz inną stację i otwórz `Ctrl+Shift+P`. Wskaż raz zajęte miejsce i naciśnij Enter. Następnie wskaż dwukrotnie tę samą cyfrę lub znak i dopiero naciśnij Enter.

Oczekiwane: pierwszy wariant nie nadpisuje presetu i wyjaśnia wymagane potwierdzenie. Dopiero powtórne wskazanie tego samego miejsca oraz Enter zastępuje stację. Wybranie innego miejsca zmienia cel, a Escape anuluje.

### AMC-130-03 — Lista presetów tylko do uruchamiania

Naciśnij `Ctrl+P`. Przejdź po wszystkich dwunastu pozycjach, uruchom zajętą Enterem i Spacją, a na pustej spróbuj obu klawiszy. Powtórz przez menu i paletę poleceń.

Oczekiwane: fokus zaczyna się na rzeczywistym wierszu presetu. Zajęta pozycja uruchamia stację, pusta tylko wyjaśnia sposób przypisania. Lista nie zapisuje, nie zastępuje i nie usuwa presetów. `Ctrl+Shift+1–0/-/=` uruchamia zajęte miejsce bez otwierania listy.

### AMC-130-04 — Usunięcie przypisania

Otwórz `Ctrl+Shift+P`, wybierz zajęty preset, naciśnij Delete, a następnie Enter. Sprawdź `Ctrl+P`, Bibliotekę i Ulubione.

Oczekiwane: usuwane jest wyłącznie przypisanie miejsca. Sama stacja pozostaje w Bibliotece i zachowuje stan Ulubionej. Escape po Delete anuluje usunięcie.

### AMC-130-05 — Bitrate 357, HLS i zwykłego MP3

Na liście wybierz kolejno Radio 357, Trójkę HLS, BBC HLS oraz zwykłą stację MP3 i naciśnij lewą strzałkę. Powtórz po rozpoczęciu odtwarzania.

Oczekiwane: jeżeli strumień lub katalog ujawnia parametry, AMC podaje kodek, bitrate oraz dostępną częstotliwość bez wartości `44100 kb/s`. BBC z wariantem `audio=128000` podaje 128 kb/s. Chronione 357 i znany HLS Trójki mogą podać odpowiednio około 128 i około 192 kb/s jako jawnie przybliżony profil awaryjny. Nieznana transmisja wideo bez osobnego pasma audio nie może podać bitrate całego obrazu.

### AMC-130-06 — Właściwości radia i aktywne łącza

Na stacji z adresem strony naciśnij `Alt+Enter`. Przeczytaj całość znakami, słowami i wierszami, przejdź Tabem do listy łączy i otwórz Enterem stronę stacji. Osobno sprawdź adres strumienia, lecz zamknij zewnętrzną aplikację, jeżeli zacznie go odtwarzać.

Oczekiwane: okno nie zawiera Kolejki ani „Odtwarzaj jako następne”. Pokazuje dane stacji, Bibliotekę, Ulubione, dźwięk i łącza. NVDA czyta na liście wyłącznie „Otwórz adres strumienia” i „Otwórz stronę stacji”, bez technicznych rekordów. Enter otwiera wybrane łącze, a kopiowanie całej treści pozostawia okno otwarte.

## Poprzedni zestaw alpha 129

## Nowości alpha 129

### AMC-129-01 — Trójka z podstawowego HLS

W Bibliotece radia wybierz `PR TRÓJKA` z adresem `https://stream13.polskieradio.pl/pr3/pr3.sdp/playlist.m3u8` i rozpocznij odtwarzanie. Po uzyskaniu dźwięku przejdź do innej stacji i wróć do Trójki.

Oczekiwane: AMC najpierw używa właściwego HLS i odbiera dźwięk AAC. Starszy MP3 na porcie 8904 jest wyłącznie próbą awaryjną. Szybka zmiana stacji nie przywraca anulowanego połączenia i nie blokuje okna.

### AMC-129-02 — Menu właściwe dla Radia

Na zwykłej liście radia oraz w odtwarzaczu otwórz kolejno menu główne Widok i Odtwarzanie, menu kontekstowe oraz paletę `Ctrl+Shift+K`. Powtórz po otwarciu wyników `Ctrl+F`.

Oczekiwane w `alpha.129`: w Radiu nie ma Kolejki, „Odtwórz jako następne”, playlist, Albumów, Zakładek, skoków procentowych ani prędkości. Od `alpha.130` `Ctrl+P` i `Ctrl+Shift+P` są świadomie ponownie użyte wyłącznie dla lokalnych presetów radiowych; `Ctrl+Q`, `Shift+Enter` i `Ctrl+Shift+Enter` nadal niczego nie zmieniają i podają zwięzły komunikat o niedostępności.

### AMC-129-03 — Zwięzła pozycja stacji

Przejdź strzałkami po Bibliotece i Ulubionych radia, zwracając uwagę na nazwę elementu i pozycję listy.

Oczekiwane: NVDA czyta nazwę oraz pozycję, np. `PR TRÓJKA, 8 z 48`. Nie dodaje zbędnego słowa `stacja` bezpośrednio przed pozycją. Właściwości `Alt+Enter` nadal mogą podać rodzaj elementu.

### AMC-129-04 — Przeniesienie jednej stacji przez Ctrl+X i Ctrl+V

Otwórz `Ctrl+U`, wybierz stację ze środka Ulubionych i naciśnij `Ctrl+X`. Przejdź do innej stacji zwykłymi strzałkami i naciśnij `Ctrl+V`.

Oczekiwane: pierwszy skrót tylko zapamiętuje stację i wyjaśnia następny krok. Drugi umieszcza ją bezpośrednio przed bieżącą stacją, odświeża listę raz, zachowuje fokus oraz zapisuje porządek po ponownym uruchomieniu. Nie zmienia systemowego schowka plików.

### AMC-129-05 — Przeniesienie zaznaczonej grupy

W Ulubionych zaznacz Shiftem kilka sąsiednich stacji, naciśnij `Ctrl+X`, wybierz pozycję docelową poza blokiem i naciśnij `Ctrl+V`. Powtórz z aktywnym filtrem oraz z celem należącym do przenoszonego bloku.

Oczekiwane: grupa zachowuje wzajemną kolejność i jest przenoszona jednym odświeżeniem. Aktywny filtr wymaga najpierw `Escape`, a wybór celu z przenoszonej grupy niczego nie zmienia i daje jasny komunikat. `Alt+góra/dół` nadal działa jako wariant pojedynczego kroku.

## Poprzedni zestaw alpha 123

## Nowości alpha 123

### AMC-123-01 — Stabilne stacje i szybka zmiana

Uruchom kolejno Jedynkę, Dwójkę, Czwórkę, PR24, Radio Chopin i Radio Emaus. Kilka razy zmień stację szybko, również zanim poprzednia zdąży zagrać.

Oczekiwane: prawidłowe stacje rozpoczynają odbiór po pojawieniu się dźwięku. Anulowane połączenie nie wraca, nie przejmuje odtwarzacza i nie tworzy dodatkowego komunikatu o błędzie. Okno i NVDA pozostają dostępne.

### AMC-123-02 — Chwilowe zerwanie

Podczas działania stacji na kilka sekund rozłącz sieć, po czym ją przywróć bez wybierania innej pozycji. Jeżeli nie chcesz zmieniać sieci całego komputera, ten test można pominąć.

Oczekiwane: AMC najwyżej dwukrotnie próbuje przywrócić odbiór. Po szybkim powrocie sieci radio ponownie gra bez zamykania odtwarzacza i bez utraty istniejącego timeshiftu. Po wyczerpaniu prób pojawia się jeden krótki komunikat, a aplikacja nadal działa.

### AMC-123-03 — Ponowne połączenie podczas nagrywania

Rozpocznij `Ctrl+Alt+R`, spowoduj krótką przerwę połączenia i przywróć sieć. Następnie zakończ nagrywanie.

Oczekiwane: jeżeli stacja wróci w tym samym formacie, powstaje jeden prawidłowo zakończony MP3, a przerwa może zawierać ciszę. Zmiana formatu lub brak powrotu kończy nagranie bez pozostawienia fałszywego gotowego pliku.

### AMC-123-04 — Niedostępna stacja

Spróbuj uruchomić Trójkę, jeżeli jej adres akurat nie odpowiada, a bezpośrednio potem wybierz działającą stację.

Oczekiwane: dwa niedostępne warianty nie są przedstawiane jako działające. AMC nie zapętla prób i nie blokuje przejścia do następnej stacji. Jeżeli nadawca przywrócił już serwer, Trójka może działać normalnie.

## Poprzedni zestaw alpha 114

## Nowości alpha 114

### AMC-114-01 — Placeholder niezależny od nazwy chmury

W Folderach Biblioteki użyj dostępnego pliku tylko online z OneDrive, iCloud, Google Drive albo innego klienta korzystającego z mechanizmu Cloud Files. Najpierw wybierz go i użyj lewej strzałki, potem świadomie rozpocznij odtwarzanie.

Oczekiwane: indeksowanie i krótka informacja nie pobierają zawartości. Program rozpoznaje stan pliku z metadanych Windows, nawet jeśli nazwa folderu nie zawiera nazwy dostawcy. Dopiero odtwarzanie może rozpocząć hydratację, a okno i NVDA pozostają dostępne.

### AMC-114-02 — Ponawianie po awarii usługi chmurowej

Podczas otwierania pliku tylko online chwilowo odłącz sieć albo zatrzymaj klienta chmury. Po błędzie spróbuj natychmiast kilka razy, potem przywróć dostęp i ponów po podanym czasie.

Oczekiwane: AMC nie tworzy wielu równoległych, zablokowanych prób. Krótki komunikat podaje czas do ponowienia, rosnący od około 10 sekund najwyżej do 5 minut. Po udanym otwarciu ograniczenie znika; restart nie jest wymagany.

### AMC-114-03 — MP3 z nietypowym początkiem

Na kopiach materiałów sprawdź MP3 z bardzo dużą okładką lub tagiem ID3, z dodatkowymi bajtami przed dźwiękiem oraz plik zapisany przez nietypowy rejestrator. Jeżeli masz MP3 free-format, dodaj go do próby.

Oczekiwane: aplikacja czyta tylko ograniczony fragment, odnajduje potwierdzone ramki i może ominąć wadliwy początek bez tworzenia nowej kopii pliku. Czas, pozycja i przewijanie pozostają prawidłowe. Ostrzeżenia techniczne nie są wypowiadane przez NVDA.

### AMC-114-04 — MP3 większy niż 512 MiB i 4 GiB

Jeżeli masz bardzo duży lokalny MP3, sprawdź start, kilka odległych skoków, zmianę prędkości oraz Page Up i Page Down. Nie kopiuj pliku specjalnie do katalogu testowego i nie używaj ważnego materiału do eksperymentalnego uszkadzania.

Oczekiwane: limit 512 MiB dotyczy wyłącznie ostatniego awaryjnego NLayer, a nie zwykłego odtwarzania. Ścieżka systemowa i oczyszczony podstrumień używają pozycji 64-bitowych, nie ładują całości do pamięci i nie tworzą tymczasowej kopii.

### AMC-114-05 — Uszkodzone deklaracje kontenerów

Na nieistotnych kopiach sprawdź ucięty WAV, AIFF, FLAC, OGG, M4A lub AAC. Po każdej próbie uruchom od razu prawidłowy plik innego formatu.

Oczekiwane: rozmiar zadeklarowany poza fizycznym końcem jest odnotowany w logu, ale okno nie blokuje się na analizie. Wadliwy tor zostaje odłączony, prawidłowy następny materiał działa, a program nie modyfikuje żadnego źródła.

## Poprzedni zestaw alpha 113

## Nowości alpha 113

### AMC-113-01 — Podstawowa macierz formatów

Odtwórz po jednym zwykłym pliku WAV, FLAC, OGG/Vorbis, M4A lub AAC, WMA, AIFF i MP3. Jeżeli masz, sprawdź również Ogg Opus, WebM albo MKA z dźwiękiem. Dla każdego użyj paska, przewijania, prędkości, pauzy, Page Up lub Page Down i wyjścia Escape.

Oczekiwane: każdy obsługiwany przez bieżący Windows format korzysta z tego samego stabilnego odtwarzacza, czasu, głośności i pamięci pozycji. Brak systemowego kodeka daje krótki błąd bez zamrożenia. NVDA nie odczytuje nazw klas, kontenerów technicznych ani wyjątków.

### AMC-113-02 — WAV, RF64 i długi materiał bez kompresji

Sprawdź dostępny większy WAV, a jeżeli masz nagranie RF64 lub BWF zapisane jako `.wav`, również ten plik. Wykonaj kilka odległych skoków i zmian prędkości.

Oczekiwane: prawidłowy plik otwiera się bez pełnego wczytywania do pamięci. Rozmiar większy niż klasyczna granica RIFF nie powoduje ujemnego czasu ani błędnego końca. Interfejs pozostaje dostępny podczas przygotowania.

### AMC-113-03 — FLAC i duże metadane

Otwórz FLAC z okładką lub dużą liczbą tagów oraz zwykły FLAC bez rozbudowanych metadanych. Przewiń w kilka miejsc, zatrzymaj i uruchom inny format.

Oczekiwane: preflight czyta tylko początek, a Media Foundation obsługuje właściwy strumień. Zmiana pliku odłącza poprzedni tor; późny wynik ani błąd FLAC nie przejmuje ponownie odtwarzacza.

### AMC-113-04 — OGG Vorbis i Ogg Opus

Odtwórz zwykły OGG/Vorbis, w tym wcześniejszy fragment o przesuniętej osi czasu. Następnie, jeżeli masz, otwórz plik Ogg Opus z rozszerzeniem `.opus`, `.oga` albo `.ogg`.

Oczekiwane: Vorbis korzysta z normalizowanej osi czasu i nie zapętla się za końcem. Ogg Opus nie jest błędnie otwierany czytnikiem Vorbis; jest przekazywany do systemu i albo gra, albo zostaje bezpiecznie odrzucony, zależnie od kodeków Windows.

### AMC-113-05 — Pliki ucięte i błędnie nazwane

Na kopiach nieistotnych materiałów przygotuj po jednym uciętym WAV, FLAC i OGG. Możesz też nadać kopii FLAC rozszerzenie `.wav` albo plikowi tekstowemu jedno z rozszerzeń multimedialnych. Nie modyfikuj ważnych oryginałów.

Oczekiwane: AMC może odrzucić plik przy otwieraniu albo po rozpoczęciu dekodowania, ale zawsze zwalnia tor, zachowuje działający fokus i pozwala natychmiast otworzyć inny plik. Ponowienie nie używa pozornie działającego, uszkodzonego dekodera.

### AMC-113-06 — Kontenery z obrazem i ścieżką audio

Jeżeli masz MP4, MOV, 3GP, MKV, WebM, AVI albo WMV ze ścieżką audio, dodaj go przez `Ctrl+O` lub Folder Biblioteki i rozpocznij odtwarzanie.

Oczekiwane: AMC odtwarza wyłącznie dźwięk i zachowuje semantykę elementu multimedialnego. Kontener bez obsługiwanej ścieżki audio kończy się zwięzłym błędem. Wideo nie otwiera dodatkowego okna i nie odbiera fokusu.

### AMC-113-07 — Regresja chmury i bardzo dużego pliku

Powtórz serię odległych skoków w wielogodzinnym pliku z Dysku Google, OneDrive albo iCloud. Następnie przejdź do krótkiego lokalnego WAV, OGG lub FLAC.

Oczekiwane: ograniczone rozpoznanie kontenera nie powoduje pobrania całej zawartości. Chmura nadal ma dłuższe limity, skoki łączą się do ostatniego celu, a lokalny plik uruchamia się na świeżym torze.

## Poprzedni zestaw alpha 112

## Nowości alpha 112

### AMC-112-01 — Wielogodzinny plik z chmury i seria skoków

Otwórz plik `Radio Centrum_23.lut.2026_11.27.42 AM.mp3` z Dysku Google albo jeszcze większy dostępny materiał. Skocz kolejno w kilka odległych miejsc cyframi i `Ctrl+J`, podczas doczytywania przejdź Tabem, odczytaj pasek `NVDA+End`, zmień głośność i wróć Escape.

Oczekiwane: okno, fokus i NVDA pozostają dostępne. Seria skoków kończy się w ostatnim żądanym miejscu. AMC nie wybiera przypadkowego pliku, nie wymaga odzyskiwania fokusu przez Escape i nie blokuje źródła na cały czas działania programu po chwilowym braku sieci.

### AMC-112-02 — Inny dostawca chmury albo udział sieciowy

Jeżeli masz możliwość, użyj pliku tylko online z OneDrive, iCloud, Dropbox, Box, Nextcloud albo udziału `\\serwer\udział`. Rozpocznij odtwarzanie, chwilowo odłącz sieć lub zatrzymaj klienta chmury, a potem przywróć dostęp i spróbuj ponownie.

Oczekiwane: skan Biblioteki nie pobiera całego folderu. Próba odtwarzania może czekać na dostawcę, ale interfejs działa. Po niepowodzeniu komunikat jest krótki i pozbawiony nazw klas lub kodów; po przywróceniu dostępu ten sam plik można ponowić bez restartu AMC.

### AMC-112-03 — Nietypowy lub częściowo uszkodzony MP3

Sprawdź dostępne pliki z bardzo dużym tagiem lub okładką ID3, dodatkowymi danymi przed pierwszą ramką, urwanym końcem albo materiał zapisany przez nietypowy rejestrator. Jeżeli masz plik, który wcześniej zawieszał lub był odrzucany przez system, użyj właśnie jego.

Oczekiwane: prawidłowy MP3 zaczyna grać, ewentualnie po jednej automatycznej próbie dekodera awaryjnego. Uszkodzony plik zostaje odrzucony bez zamrożenia i bez pętli ponowień. Po błędzie od razu można przejść do następnego pliku i używać całego okna.

### AMC-112-04 — Plik tylko udający MP3

Utwórz kopię małego pliku tekstowego albo innego nieaudio i nadaj jej rozszerzenie `.mp3`. Dodaj ją do Biblioteki i spróbuj odtworzyć. Nie używaj ważnego oryginału.

Oczekiwane: AMC nie przestaje odpowiadać, niczego nie modyfikuje i podaje zwięzłą informację o uszkodzonym albo nieobsługiwanym formacie. Szczegóły można znaleźć tylko w `%LocalAppData%\AccessibleMediaController\logs\amc.log`.

### AMC-112-05 — Bardzo długi plik lokalny

Na najdłuższym dostępnym pliku lokalnym, także dłuższym niż doba, sprawdź start, pasek, czas upłynięty, pozostały i całkowity, skok do czasu, cyfry procentowe, zakładki oraz Page Up i Page Down.

Oczekiwane: program akceptuje wiarygodne nagrania znacznie dłuższe niż wcześniejsza granica 30 dni, nie przepełnia obliczeń czasu i nie wczytuje całego pliku do pamięci. Nawigacja pozostaje spójna z kontekstem listy.

### AMC-112-06 — Regresja formatów i Kolejki

Odtwórz krótki zwykły MP3 oraz po jednym dostępnym WAV, FLAC, M4A lub AAC i OGG. Powtórz też dodanie folderu do Kolejki, odtworzenie lub usunięcie jednego pliku i usunięcie pozostałej zawartości folderu.

Oczekiwane: aktualizacja NAudio nie zmienia czasu, wysokości dźwięku, prędkości, paska ani kontynuacji. Częściowy stan folderu w Kolejce nadal działa zgodnie z alfą 110.

## Poprzedni zestaw alpha 111

## Nowości alpha 111

### AMC-111-01 — Wielogodzinny plik z Dysku Google

Otwórz plik `Radio Centrum_23.lut.2026_11.27.42 AM.mp3` o czasie około `8:46:27` z `G:\Dyski współdzielone\Archiwum M\Radio Centrum`. Skocz cyfrą albo `Ctrl+J` w okolice szóstej lub siódmej godziny. Podczas ewentualnego doczytywania użyj Tabu, odczytu paska `NVDA+End` oraz jednego polecenia czasu.

Oczekiwane: dźwięk może chwilę czekać na dane dostawcy, ale AMC, fokus i NVDA nie zamrażają się. Nie trzeba naciskać Escape, aby odzyskać sterowanie. Pasek od razu pokazuje żądany cel.

### AMC-111-02 — Seria szybkich skoków

W tym samym pliku naciśnij szybko kilka razy `Ctrl+strzałka w prawo`, potem `Shift+strzałka w prawo`, a podczas doczytywania wybierz inną cyfrę procentową.

Oczekiwane: interfejs reaguje na każde polecenie, ale dekoder nie tworzy długiej kolejki wszystkich pośrednich operacji. Ostatecznie trafia do ostatniej żądanej pozycji. Komunikaty i pasek nie cofają się kolejno przez stare cele.

### AMC-111-03 — Zmiana elementu podczas doczytywania

Rozpocznij odległy skok w pliku chmurowym, po czym przez Page Up albo Page Down uruchom inny dostępny plik. Możesz też wyjść z odtwarzacza zgodnie z bieżącym ustawieniem Escape.

Oczekiwane: nowy element działa od razu, a spóźnione zakończenie starego skoku nie zmienia jego pozycji, nie odbiera fokusu i nie uruchamia poprzedniego nagrania.

### AMC-111-04 — Zwykły plik lokalny

Na krótszym MP3 zapisanym poza chmurą sprawdź strzałki, cyfry, `Ctrl+J`, zakładki oraz odczyt czasu.

Oczekiwane: wszystkie skoki pozostają szybkie, ich pozycje są dokładne, a odtwarzanie i zakładki nie mają regresji.

### AMC-111-05 — Regresja folderu w Kolejce

Powtórz skrócony scenariusz alpha 110: dodaj folder do Kolejki, odtwórz lub usuń z niej jeden plik, wróć do folderu i usuń jego pozostałą zawartość `Shift+Enter`.

Oczekiwane: folder nadal proponuje usunięcie, dopóki choć jeden jego plik pozostaje w Kolejce albo „odtwarzaj jako następne”.

## Poprzedni zestaw alpha 110

## Nowości alpha 110

### AMC-110-01 — Folder po odtworzeniu jednego pliku z Kolejki

W Folderach dodaj cały folder do Kolejki przez `Shift+Enter`. Otwórz Kolejkę, uruchom jeden z jego plików i usuń ten plik albo inny pojedynczy plik z Kolejki. Wróć do tego folderu i naciśnij `Shift+Enter`.

Oczekiwane: dopóki choć jeden plik tego folderu pozostaje w Kolejce lub w grupie „odtwarzaj jako następne”, menu i polecenie folderu proponują usunięcie. `Shift+Enter` usuwa wszystkie pozostałe pliki folderu z obu grup. Nie dodaje ponownie plików już odtworzonych ani ręcznie usuniętych.

### AMC-110-02 — Ponowne dodanie całkowicie usuniętego folderu

Po wykonaniu poprzedniego zadania ponownie naciśnij `Shift+Enter` na tym samym folderze i otwórz Kolejkę.

Oczekiwane: kiedy żaden plik folderu nie należy już do Kolejki, polecenie ponownie dodaje całą dostępną zawartość folderu.

### AMC-110-03 — Menu kontekstowe przy stanie częściowym

Dodaj folder do Kolejki, usuń z Kolejki jeden jego plik, wróć do folderu i otwórz menu kontekstowe.

Oczekiwane: menu mówi „Usuń zawartość folderu z kolejki”, a nie „Dodaj…”. Analogiczna zasada obowiązuje dla częściowego stanu Ulubionych i „odtwarzaj jako następne”.

### AMC-110-04 — Cofnięcie częściowego usunięcia

W częściowym stanie Kolejki usuń pozostałą zawartość poleceniem folderu, a następnie naciśnij `Ctrl+Z`.

Oczekiwane: jedno cofnięcie odtwarza dokładnie stan sprzed polecenia folderu, bez przywracania wcześniej odtworzonego albo ręcznie usuniętego pliku.

## Poprzedni zestaw alpha 109

## Nowości alpha 109

### AMC-109-01 — Dodanie folderu do Kolejki

W widoku Foldery ustaw fokus na folderze zawierającym pliki i naciśnij `Shift+Enter`. Otwórz Kolejkę i sprawdź jej zawartość. Wróć do tego samego folderu i ponownie naciśnij `Shift+Enter`.

Oczekiwane: pierwsze polecenie dodaje wszystkie dostępne, zaindeksowane pliki z folderu i podfolderów, ale nie dodaje wiersza rodzaju „folder”. Komunikat podaje nazwę folderu i liczbę plików. Drugie polecenie usuwa tę zawartość z Kolejki i również podaje jednoznaczny komunikat.

### AMC-109-02 — Odtwarzaj zawartość jako następną

Na folderze naciśnij `Ctrl+Shift+Enter`, otwórz Kolejkę i sprawdź pierwszą grupę elementów. Powtórz polecenie na folderze.

Oczekiwane: rzeczywiste pliki są oznaczone jako odtwarzane następne w kolejności folderu. Powtórzenie usuwa ten stan. Sam folder nie występuje na liście.

### AMC-109-03 — Ulubione i cofanie całej zmiany

Na folderze naciśnij `Ctrl+Shift+U`, otwórz Ulubione, a następnie naciśnij `Ctrl+Z`.

Oczekiwane: do Ulubionych trafiają pliki, nie folder. Jedno `Ctrl+Z` cofa całą operację, niezależnie od liczby plików.

### AMC-109-04 — Playlisty folderu

Na folderze naciśnij `Ctrl+Shift+P`, wybierz lub utwórz playlistę i zapisz zmiany. Otwórz playlistę.

Oczekiwane: menedżer działa na wszystkich plikach folderu i podfolderów, podaje liczbę plików i nie pokazuje technicznego wiersza folderu jako utworu.

### AMC-109-05 — Folder pusty i Biblioteka

Jeżeli masz widoczny folder bez aktywnych plików, wywołaj na nim polecenie Kolejki. Na zwykłym folderze naciśnij `Ctrl+Shift+L`.

Oczekiwane: pusty folder niczego nie zmienia. `Ctrl+Shift+L` wyjaśnia, że folder już należy do Biblioteki i że przynależność zmienia się na plikach po otwarciu folderu.

### AMC-109-06 — Menu kontekstowe i fokus

Otwórz menu kontekstowe na folderze. Przejdź po poleceniach, wykonaj dodanie do Kolejki, a następnie je cofnij.

Oczekiwane: menu mówi „Otwórz folder” oraz jawnie nazywa działania na zawartości folderu. Po zamknięciu menu i po odświeżeniu fokus pozostaje na tym samym folderze.

## Krótka regresja alpha 109

- Enter otwiera folder, a Backspace wraca do folderu nadrzędnego;
- działanie folderu nie otwiera plików i nie pobiera placeholderów chmurowych;
- zwykły utwór nadal przełącza Kolejkę, Ulubione i „odtwarzaj jako następne” pojedynczo;
- album i kontener playlisty nadal wymagają otwarcia Enterem;
- ochrona dekoderów alpha 108 nadal działa.

## Poprzedni zestaw alpha 108

## Nowości alpha 108

### AMC-108-01 — OGG Emaus i brak zawieszenia interfejsu

Otwórz `Emaus - 2026-08-27 11-30.ogg`, pozostaw odtwarzanie przez co najmniej minutę, odczytuj pasek NVDA+End, przechodź Tabem po odtwarzaczu i użyj kilku skrótów czasu.

Oczekiwane: czas wynosi około `5:06`; AMC i NVDA odpowiadają przez cały test. W logu nie powstaje nowe ostrzeżenie `ui-watchdog` ani `decoder-watchdog` dla prawidłowo odtwarzanego pliku.

### AMC-108-02 — Różne formaty

Otwórz kolejno dostępne pliki MP3, WAV, AAC lub M4A, FLAC, WMA, zwykły OGG i AIFF. Dla każdego sprawdź start, pasek, przewijanie, zmianę prędkości, pauzę i przejście Page Up/Page Down.

Oczekiwane: format obsługiwany przez system zaczyna się bez blokowania okna, pozycja zmienia się prawidłowo i koniec nie tworzy pętli. Format bez zainstalowanego dekodera, w szczególności Opus na obecnej konfiguracji Windows, może zostać odrzucony krótkim błędem, ale AMC i NVDA nadal działają.

### AMC-108-03 — Lewa strzałka i brakujące dane

Na świeżo dodanym lokalnym pliku, którego AMC jeszcze nie odtwarzało, naciśnij lewą strzałkę. Podczas odczytu danych przechodź po innych programach i wróć do AMC.

Oczekiwane: okno nie zawiesza się. Informacja pojawia się po zakończeniu odczytu albo po pięciosekundowym limicie korzysta z już zapisanych danych. Jeżeli przed zakończeniem przejdziesz na inny element, spóźniony komunikat starego pliku nie jest wypowiadany.

### AMC-108-04 — Plik chmurowy

Wybierz niepobrany plik iCloud, OneDrive albo Dysku Google. Najpierw naciśnij lewą strzałkę, a następnie świadomie rozpocznij odtwarzanie.

Oczekiwane: lewa strzałka nie pobiera placeholdera i nie blokuje interfejsu. Dopiero odtwarzanie może rozpocząć pobieranie. Podczas oczekiwania AMC pozostaje dostępne; po niepowodzeniu podaje błąd chmury zamiast nieskończonego oczekiwania.

### AMC-108-05 — Logi watchdogów

Po zwykłym użyciu sprawdź plik `%LocalAppData%\AccessibleMediaController\logs\amc.log` albo przekaż go do analizy po zauważonym problemie.

Oczekiwane: zwykłe odtwarzanie nie tworzy ostrzeżeń. `ui-watchdog` oznacza brak odpowiedzi całego okna przez osiem sekund, `metadata-watchdog` przekroczenie limitu odczytu informacji, a `decoder-watchdog` zatrzymany odczyt konkretnego pliku. Żaden z tych mechanizmów nie usuwa ani nie modyfikuje materiału.

## Krótka regresja alpha 108

- pasek stanu, czas bieżący, pozostały i całkowity są nadal prawidłowe;
- przewijanie, zakładki i wznowienie zachowują rzeczywistą pozycję;
- regulacja prędkości 0,50–2,00 razy nie zmienia wysokości dźwięku;
- `Alt+F4`, Escape oraz F6 zachowują ustalone działanie;
- Pomoc `F1` i `Ctrl+F1` nadal używa zwięzłego kontekstu sesji.

## Poprzedni zestaw alpha 107

## Nowości alpha 107

### AMC-107-01 — Lewa strzałka w Plikach lokalnych

W sesji Pliki lokalne przejdź do Folderów, włącz `Ctrl+F1` i naciśnij strzałkę w lewo.

Oczekiwane: komunikat brzmi w rodzaju „Strzałka w lewo: oznajmia wielkość i bitrate pliku. Kontekst: Pliki lokalne”. Nie mówi ogólnie o krótkich informacjach i nie dodaje słowa „Foldery”.

### AMC-107-02 — Kontekst po zmianie sesji

Włączaj Pomoc klawiatury kolejno w Plikach lokalnych, WiiM, TIDAL i Apple Music. W każdej sesji sprawdź kilka skrótów na różnych widokach.

Oczekiwane: po słowie „Kontekst” występuje tylko użytkowa nazwa aktywnej sesji. Zmiana Biblioteki na Kolejkę, Ulubione, Foldery albo odtwarzacz nie dopisuje nazwy widoku.

### AMC-107-03 — Lewa strzałka poza sesją lokalną

Na liście demonstracyjnej WiiM, TIDAL albo Apple Music włącz `Ctrl+F1` i naciśnij strzałkę w lewo.

Oczekiwane: opis mówi o bitrate i innych dostępnych parametrach elementu. Nie obiecuje wielkości lokalnego pliku, a kontekst zawiera wyłącznie nazwę usługi.

### AMC-107-04 — Spis F1

Otwórz `F1`, wyszukaj „bitrate” i przejdź do wpisu lewej strzałki.

Oczekiwane: wpis opisuje wielkość, bitrate i dostępne parametry, nie zawiera technicznych identyfikatorów i pozostaje tylko opisem — Enter nie wykonuje odczytu ani innego działania.

## Krótka regresja alpha 107

- `Ctrl+F1` nadal przechwytuje i opisuje klawisz bez wykonywania polecenia.
- `Escape` oraz ponowne `Ctrl+F1` wyłączają Pomoc klawiatury.
- Po wyłączeniu Pomocy lewa strzałka rzeczywiście odczytuje dane zaznaczonego elementu.
- Plik Emaus nadal ma około `5:06` i nie zawiesza AMC.

## Poprzedni zestaw alpha 106

## Nowości alpha 106

### AMC-106-01 — Plik Emaus z folderu YouTubeAudio

1. Otwórz plik `Emaus - 2026-08-27 11-30.ogg` z folderu `Sideloads\YouTubeAudio`.
2. Sprawdź pasek odtwarzacza, informacje o czasie i szybką informację pod strzałką w lewo.
3. Pozostaw odtwarzanie przez co najmniej minutę i poruszaj się po przyciskach odtwarzacza.

Oczekiwane: czas wynosi około `5:06`, a nie setki dni. AMC i NVDA pozostają responsywne, nie znika główne okno i proces nie obciąża stale całego rdzenia procesora.

### AMC-106-02 — Przewijanie fragmentu transmisji

W pliku Emaus sprawdź cyfry `0`, `5` i `9`, `Ctrl+J` z wartością `4:30`, strzałki oraz powrót na początek klawiszem Home.

Oczekiwane: wszystkie pozycje liczą się od początku pięciominutowego fragmentu. Skok nie trafia w absolutny czas źródłowej transmisji, nie zawiesza programu i nie wychodzi poza koniec pliku.

### AMC-106-03 — Rzeczywisty koniec i następny element

Przejdź blisko końca pliku Emaus i pozwól mu dojść do końca w odtwarzalnym widoku, na przykład folderze albo playliście z następnym elementem.

Oczekiwane: koniec zostaje rozpoznany jeden raz. Nie powstaje pętla dekodera; dalsze zachowanie wynika z bieżącego kontekstu odtwarzania i zachowuje ustaloną kolejność.

### AMC-106-04 — Zwykłe pliki OGG i pozostałe formaty

Otwórz zwykły plik OGG, na przykład `Nextfest`, oraz po jednym MP3 i AAC. Sprawdź czas, przewijanie, tempo, pauzę i zmianę elementu.

Oczekiwane: zwykłe OGG nadal ma prawidłowy czas i przewijanie, a zmiana czytnika OGG nie powoduje regresji MP3 ani AAC.

### AMC-106-05 — Zamknięcie po odtwarzaniu OGG

Podczas odtwarzania Emaus zamknij AMC przez `Alt+F4`, po czym uruchom je ponownie.

Oczekiwane: pierwsza instancja kończy się, nie pozostawia procesu bez okna, a ponowne uruchomienie odtwarza stan bez automatycznego uruchamiania błędnej pętli.

## Krótka regresja alpha 106

- `F1`, `?` i `Ctrl+F1` nadal działają jak w alpha 105.
- Lista, odtwarzacz, Historia i playlisty zachowują fokus oraz ustalone skróty.
- Szybka informacja o OGG i `Alt+Enter` pokazują ten sam rzeczywisty czas.
- Plik tymczasowy z rozszerzeniem `.converting` nie staje się pozycją audio Biblioteki.

## Poprzedni zestaw alpha 105

## Nowości alpha 105

### AMC-105-01 — Pierwsze otwarcie spisu F1

1. Na zwykłej liście naciśnij `F1`.
2. Sprawdź pierwszą informację oraz fokus.
3. `Tab`, `Shift+Tab`, strzałki, Enter i Escape powinny prowadzić przewidywalnie między wyszukiwaniem, sekcjami, skrótami i przyciskami.
4. Zamknij okno, otwórz je ponownie i sprawdź, czy nie pojawiają się techniczne zapisy w rodzaju nazwy klasy, klamer albo nazw właściwości.

### AMC-105-02 — Sekcje i wyszukiwanie

1. Przejdź po wszystkich sekcjach spisu.
2. Wyszukaj kolejno: `ulubione`, `czas`, `kosz`, `ctrl f1` i tekst bez polskich znaków.
3. Sprawdź, czy wynik podaje kolejno nazwę polecenia, skrót i kontekst.
4. Wyczyść wyszukiwanie i sprawdź powrót pełnej listy sekcji.

### AMC-105-03 — Wykonanie polecenia z Pomocy

1. Wyszukaj bezpieczne polecenie, na przykład „Pokaż kolejkę”.
2. Naciśnij Enter na jego pozycji.
3. Pomoc powinna się zamknąć, a polecenie wykonać tak samo jak z klawiatury lub palety.
4. Otwórz F1 ponownie i naciśnij Enter na informacyjnym opisie, na przykład `Shift+Delete`; nic destrukcyjnego nie może zostać wykonane.

### AMC-105-04 — Pomoc klawiatury na liście

1. Naciśnij `Ctrl+F1`.
2. Sprawdź `Ctrl+U`, `Ctrl+Shift+C`, `Delete`, `Alt+F4` i dowolny nieprzypisany skrót.
3. Każdy klawisz ma zostać opisany wraz z bieżącym kontekstem, ale nie może wykonać działania ani zamknąć programu.
4. Naciśnij `Escape`; tryb ma się wyłączyć.

### AMC-105-05 — Pomoc klawiatury w odtwarzaczu

1. Otwórz odtwarzacz i włącz `Ctrl+F1`.
2. Sprawdź strzałki, `Shift+kropka`, cyfrę, `PageDown`, `B` i klawisz bez przypisania.
3. Upewnij się, że czas, głośność, prędkość, plik i Zakładki nie zmieniają się w czasie opisywania.
4. Wyłącz tryb ponownym `Ctrl+F1` i sprawdź, że zwykłe sterowanie znów działa.

### AMC-105-06 — Wyciszone komunikaty i prefiks

1. Wyłącz zwykłe komunikaty dostępności w Ustawieniach.
2. Włącz i wyłącz Pomoc klawiatury; oba stany nadal muszą być oznajmione.
3. W trybie Pomocy naciśnij skonfigurowany globalny prefiks. Ma zostać opisany, ale nie może otworzyć warstwy.
4. Po wyjściu z Pomocy sprawdź, czy globalny prefiks znowu działa.

### AMC-105-07 — Znak zapytania i pola tekstowe

1. Na liście naciśnij znak `?`; powinien otworzyć spis.
2. Zamknij spis, wejdź do filtra albo wyszukiwania i wpisz `?`.
3. W polu tekstowym znak ma zostać wpisany i nie może otworzyć Pomocy.

## Krótka regresja

- `Ctrl+Shift+K`: paleta nadal ma czytelne pozycje i wykonuje polecenia.
- `Ctrl+H`: Historia nadal działa jak w alpha 104.
- `Ctrl+P` i `Ctrl+Shift+P`: playlisty nadal działają.
- `F1` z odtwarzacza po zamknięciu przywraca fokus do odtwarzacza, a z listy do listy.
- Przyciski Anuluj i Escape w Pomocy niczego nie wykonują.

## Poprzedni zestaw alpha 104

## Nowości alpha 104

### AMC-104-01 — Trwała Historia w odwrotnej kolejności

W sesji Pliki lokalne odtwórz kolejno trzy różne pliki, po czym naciśnij `Ctrl+H`. Zamknij i ponownie uruchom AMC, wróć przez `Ctrl+1`, `Ctrl+H`.

Oczekiwane: Historia odtwarzania istnieje osobno dla tej sesji, jest trwała i pokazuje najpierw plik odtwarzany ostatnio. Ponowne odtworzenie wcześniejszego pliku przenosi go na początek bez tworzenia duplikatu.

### AMC-104-02 — Delete usuwa tylko z Historii

Dodaj plik do Biblioteki, Ulubionych i Kolejki, odtwórz go, a następnie w `Ctrl+H` zaznacz ten wpis i naciśnij `Delete`. Powtórz z kilkoma wpisami zaznaczonymi Shiftem.

Oczekiwane: komunikat mówi o usunięciu z Historii. Wpisy znikają wyłącznie z tego widoku. Pliki pozostają na dysku oraz nadal należą do Biblioteki, Ulubionych i Kolejki. Po ponownym uruchomieniu usunięte wpisy nie wracają. Menu kontekstowe nazywa polecenie **Usuń z Historii odtwarzania**.

### AMC-104-03 — Playlista i pozostałe działania z Historii

Zaznacz kilka wpisów Historii i naciśnij `Ctrl+Shift+P`. Utwórz nową playlistę, zapisz zmiany i sprawdź jej zawartość. Wyrywkowo sprawdź również `Ctrl+C`, `Ctrl+Shift+C`, `Shift+Enter` i zmianę Ulubionych.

Oczekiwane: nowa playlista zawiera wszystkie zaznaczone elementy, a pozostałe polecenia działają zbiorczo tak samo jak na zwykłej liście. `Alt+strzałka w górę/w dół` nie zmienia kolejności Historii, ponieważ pozostaje ona chronologiczna.

### AMC-104-04 — Shift+Delete pozostaje operacją na pliku

Tylko na niepotrzebnej kopii pliku lokalnego wybierz w Historii `Shift+Delete` i przeczytaj ostrzeżenie. Najpierw anuluj, a dopiero w osobnej próbie świadomie potwierdź.

Oczekiwane: program wyraźnie odróżnia usunięcie wpisu `Delete` od fizycznego przeniesienia pliku do Kosza przez `Shift+Delete`. Anulowanie niczego nie zmienia; potwierdzenie usuwa plik z dysku i jego rekordy zgodnie z dotychczasową regułą.

## Poprzedni zestaw alpha 103

## Nowości alpha 103

### AMC-103-01 — Dostępna lista niedostępnych plików

Otwórz `Ctrl+F5`, wybierz Folder Biblioteki mający co najmniej jeden rekord niedostępny i użyj przycisku **Przejrzyj niedostępne…**.

Oczekiwane: otwiera się osobne okno, a fokus i odczyt NVDA trafiają na pierwszy plik. Każdy wiersz zawiera czytelną nazwę i pełną ścieżkę, bez nazw klas, pól `Id`, `Label` ani innych technicznych etykiet. Lista obsługuje zwykłe zaznaczanie Shiftem oraz `Ctrl+A`.

### AMC-103-02 — Anulowanie jest bezpieczne

Zaznacz jeden lub kilka rekordów, naciśnij `Delete` albo przycisk **Zapomnij zaznaczone w AMC…**, a w ostrzeżeniu wybierz **Nie**.

Oczekiwane: komunikat wyjaśnia pełny zakres operacji i zaznacza, że dysk nie zostanie zmieniony. Po anulowaniu rekordy i wszystkie ich dane nadal istnieją.

### AMC-103-03 — Potwierdzone zapomnienie w AMC

Wykonaj próbę na niedostępnym rekordzie testowym, który można bezpiecznie usunąć z danych AMC. Potwierdź **Tak**, zamknij listę i ponownie odczytaj licznik wybranego folderu.

Oczekiwane: rekord znika z listy, a liczba niedostępnych maleje. AMC usuwa jego powiązania z Biblioteką, Ulubionymi, Kolejką, playlistami, Historią, Zakładkami i zapamiętaną pozycją. Nie usuwa ani nie przenosi żadnego pliku na dysku. Jeżeli ten sam plik później wróci do widocznej ścieżki, synchronizacja traktuje go jak nowy rekord.

### AMC-103-04 — Plik chmurowy tylko online

Jeżeli masz w iCloud, OneDrive albo Google Drive plik widoczny jako placeholder, lecz niepobrany lokalnie, odśwież folder przez `F5` i otwórz listę niedostępnych.

Oczekiwane: widoczny dla Windows placeholder pozostaje aktywnym plikiem Biblioteki i nie jest proponowany do zapomnienia. AMC nie pobiera całego folderu tylko po to, by utworzyć indeks.

## Poprzedni zestaw alpha 102

## Nowości alpha 102

### AMC-102-01 — Pierwszy folder odczytany po Ctrl+F5

Na lokalnej liście naciśnij `Ctrl+F5` i niczego więcej nie naciskaj.

Oczekiwane: fokus znajduje się na pierwszym zaznaczonym Folderze Biblioteki. NVDA odczytuje jego pełną etykietę i pozycję na liście, a nie tylko komunikat „Foldery Biblioteki, lista”. Strzałka w dół przechodzi od razu do drugiego folderu.

### AMC-102-02 — Czytelne znaczenie liczników

Przejdź po Folderach Biblioteki i przeczytaj tekst objaśniający oraz etykietę folderu.

Oczekiwane: stan korzenia brzmi jednoznacznie „folder dostępny” albo „folder niedostępny — rekordy zachowane”. Objaśnienie rozróżnia aktywne, niedostępne i wykluczone pliki. Plik online widoczny w systemie pozostaje aktywny nawet wtedy, gdy treść zostanie pobrana dopiero przy odtwarzaniu.

## Poprzedni zestaw alpha 101

## Nowości alpha 101

### AMC-101-01 — Foldery Biblioteki pod Ctrl+F5

Na lokalnej liście naciśnij `Ctrl+F5`, a następnie przejdź po oknie strzałkami i Tabem.

Oczekiwane: tytuł, nagłówek, lista i menu używają nazwy „Foldery Biblioteki”, bez określenia „źródła Biblioteki” i bez technicznych reprezentacji obiektów. Pierwszy fokus znajduje się na liście folderów, a Escape zamyka okno i wraca do głównej listy.

### AMC-101-02 — Trzy warianty dla wybranego folderu

W `Ctrl+F5` wybierz folder, przejdź do pola kombi **Dla wybranego folderu** i przeczytaj wszystkie pozycje. Zapisz kolejno każdą z nich.

Oczekiwane: pole zawiera „Pamiętaj pozycję odtwarzania”, „Zawsze od początku” oraz „Zgodnie z ustawieniem globalnym”. Po zapisie komunikat podaje nazwę folderu i wybraną wartość, fokus wraca do pola kombi, a ustawienie pozostaje wybrane po ponownym otwarciu okna.

### AMC-101-03 — Ustawienia globalne i Alt+Shift+Enter

Otwórz Ustawienia ogólne i znajdź opcję **Pamiętaj pozycję odtwarzania lokalnych plików**. Następnie anuluj Ustawienia i wywołaj `Alt+Shift+Enter` kolejno na pliku i folderze.

Oczekiwane: Ustawienia jasno określają opcję jako globalną. W opcjach elementu oraz folderu występują te same krótkie warianty „Pamiętaj pozycję odtwarzania” i „Zawsze od początku”. Wariant dziedziczenia dokładnie wskazuje ustawienie folderu, folderu nadrzędnego lub globalne, zależnie od wybranego obiektu; NVDA nie czyta nazw `ResumeChoice`, `Value` ani `Label`.

### AMC-101-04 — Paleta poleceń i F5

Otwórz `Ctrl+Shift+K`, wyszukaj „foldery biblioteki” i wykonaj znalezione polecenie. Potem zamknij okno i naciśnij `F5`.

Oczekiwane: paleta pokazuje „Foldery Biblioteki, Ctrl+F5”, a osobne polecenie nazywa się „Odśwież foldery Biblioteki, F5”. Odświeżenie mówi o folderach, nie o źródłach.

## Poprzedni zestaw alpha 100

## Nowości alpha 100

### AMC-100-01 — Plik przeniesiony z Kolejki

Dodaj do Kolejki co najmniej dwa pliki i uruchom pierwszy bezpośrednio z widoku `Ctrl+Q`. Przełącz się do Total Commandera, przenieś odtwarzany plik do innego folderu, wróć do AMC i naciśnij Spację.

Oczekiwane: AMC informuje o niedostępności pliku i wybiera następny element z Kolejki. Nie uruchamia niczego samoczynnie. Po Spacji gra następna pozycja Kolejki, a nie pierwszy plik folderu ani Biblioteki.

### AMC-100-02 — Jedyny plik Kolejki

Pozostaw w Kolejce tylko jeden plik, uruchom go z `Ctrl+Q`, a potem przenieś poza AMC w Total Commanderze. Wróć do programu i naciśnij Spację.

Oczekiwane: program mówi, że w bieżącym widoku nie ma następnego elementu. Spacja nie uruchamia przypadkowego pliku. Odtwarzacz wraca do listy, z której można świadomie wybrać inny materiał.

### AMC-100-03 — Plik uruchomiony z folderu

Otwórz folder zawierający co najmniej trzy pliki, uruchom środkowy, a potem przenieś go poza AMC. Wróć do programu i naciśnij Spację.

Oczekiwane: wybrany zostaje kolejny plik z tego samego widoku folderu. Nie jest wybierany pierwszy element płaskiej Biblioteki, Kolejki ani innego folderu.

### AMC-100-04 — Automatyczne wejście i powrót z Kolejki

Uruchom plik z folderu, mając co najmniej jedną pozycję w Kolejce. Po automatycznym wejściu do Kolejki przenieś bieżący plik poza AMC. Sprawdź zachowanie z jeszcze jedną pozycją Kolejki oraz bez niej.

Oczekiwane: najpierw wybierana jest kolejna dostępna pozycja Kolejki. Gdy Kolejka jest wyczerpana, AMC wraca do następnego elementu wcześniejszego widoku folderu. Program nie przechodzi na początek całej Biblioteki.

### AMC-100-05 — Delete i Shift+Delete w różnych widokach

Powtórz usunięcie bieżącego pliku przez Delete oraz fizyczne Shift+Delete po uruchomieniu go kolejno z Ulubionych, playlisty, albumu i płaskiej Biblioteki. Wystarczą krótkie próby na kopiach plików przeznaczonych do usunięcia.

Oczekiwane: każda metoda używa tej samej reguły co przeniesienie w Total Commanderze. Następca pochodzi dokładnie z widoku, z którego uruchomiono usunięty plik; przy braku następcy Spacja nie uruchamia elementu z innej listy.

### AMC-100-06 — Relacyjny komunikat ręcznego przenoszenia

W Kolejności własnej, Ulubionych, Kolejce i otwartej playliście wybierz pojedynczy element i użyj `Alt+strzałki w górę`, a następnie `Alt+strzałki w dół`. Powtórz z ciągłym blokiem dwóch lub trzech pozycji.

Oczekiwane: NVDA mówi odpowiednio „Przeniesiono w górę, nad [tytuł sąsiada]” albo „Przeniesiono w dół, pod [tytuł sąsiada]”, po czym odczytuje przeniesiony element. Dla bloku podawana jest również liczba przeniesionych elementów. Fokus i całe zaznaczenie pozostają na przeniesionych pozycjach.

## Poprzedni zestaw alpha 99

## Nowości alpha 99

### AMC-099-01 — Czytelne części jednej Kolejki

Oznacz dwa pliki przez `Ctrl+Shift+Enter` jako „Odtwórz jako następne”, a dwa inne dodaj przez `Shift+Enter` do zwykłej Kolejki. Naciśnij `Ctrl+Q` i przejdź po wszystkich wierszach.

Oczekiwane: pierwszy komunikat podaje liczbę pozycji jako następne i pozostałych. Każda pozycja priorytetowa zaczyna się od „Następny”, zwykłe pozycje nie otrzymują zbędnego powtórzenia słowa „Kolejka”. Wszystko pozostaje jedną listą.

### AMC-099-02 — Page Up i Page Down po jawnym otwarciu Kolejki

W `Ctrl+Q` uruchom pierwszą pozycję Enterem. W odtwarzaczu przejdź kilka razy przez Page Down, Page Up i ponownie Page Down.

Oczekiwane: oba klawisze poruszają się wyłącznie po kolejności widocznej wcześniej w Kolejce. Można wrócić do już odtworzonego elementu. Żaden pominięty ani wcześniej odtworzony plik nie uruchamia się później drugi raz samoczynnie.

### AMC-099-03 — Automatyczne wejście do Kolejki

Uruchom plik z Biblioteki, mając przygotowane pozycje „Odtwórz jako następne” i zwykłą Kolejkę. Pozwól pierwszemu plikowi się zakończyć, a po automatycznym rozpoczęciu pozycji Kolejki użyj Page Down i Page Up.

Oczekiwane: Page Down przechodzi do następnej pozycji Kolejki, a Page Up wraca do poprzedniej pozycji Kolejki — nie do sąsiada z wcześniejszej Biblioteki. Po wykorzystaniu Kolejki automatyczna kontynuacja wraca do elementu następującego po pierwotnym pliku w Bibliotece.

### AMC-099-04 — Element odtwarzany nie oczekuje drugi raz

Uruchom pozycję bezpośrednio z `Ctrl+Q`, wróć Escape do listy i ponownie otwórz Kolejkę.

Oczekiwane: bieżący element nie pozostaje jednocześnie na liście oczekujących. Pozostałe elementy zachowują porządek, a Page Up w odtwarzaczu nadal może wrócić do wcześniejszej pozycji dzięki historii chwilowego kontekstu.

## Poprzedni zestaw alpha 98

## Nowości alpha 98

### AMC-098-01 — Ręczna kolejność Kolejki

Dodaj do Kolejki co najmniej cztery różne pliki, otwórz ją przez `Ctrl+Q`, zaznacz środkowy element i użyj `Alt+strzałka w górę` oraz `Alt+strzałka w dół`. Powtórz z dwoma sąsiednimi elementami zaznaczonymi Shiftem.

Oczekiwane: pojedynczy element i cały blok zmieniają położenie bez utraty zaznaczenia. NVDA podaje nazwę elementu oraz nowe miejsce. Żaden plik nie znika z Kolejki ani z dysku.

### AMC-098-02 — Odtwórz jako następne i zwykła Kolejka

W jednej Kolejce przygotuj co najmniej dwa elementy przez `Ctrl+Shift+Enter` jako „Odtwórz jako następne” oraz dwa przez `Shift+Enter` jako zwykłą Kolejkę. Spróbuj przenosić elementy wewnątrz obu grup, a następnie zaznacz blok obejmujący obie grupy i użyj `Alt+strzałki`.

Oczekiwane: elementy „Odtwórz jako następne” są na początku. Można porządkować każdą grupę osobno. Dla bloku mieszanego program niczego nie zmienia i mówi, że obie grupy należy przenosić osobno.

### AMC-098-03 — Rzeczywista kolejność odtwarzania

Uruchom plik, który nie należy do przygotowanej Kolejki, a następnie doprowadź go do końca albo użyj krótkich plików testowych. Obserwuj przechodzenie przez wszystkie elementy Kolejki.

Oczekiwane: najpierw odtwarzają się pozycje „Odtwórz jako następne” w kolejności widocznej na liście, następnie zwykła Kolejka także w widocznej kolejności. Zużyty element znika z Kolejki i nie jest powtarzany.

### AMC-098-04 — Cofanie dokładnej pozycji

Przenieś element ze środka Kolejki, naciśnij `Ctrl+Z`, a potem dodaj albo usuń element przez `Shift+Enter` i ponownie użyj `Ctrl+Z`.

Oczekiwane: pierwsze cofnięcie odtwarza dokładne położenie. Drugie przywraca zarówno przynależność, jak i poprzednie miejsce elementu, a fokus pozostaje na właściwym wierszu.

### AMC-098-05 — Restart i odświeżenie Biblioteki

Ustaw własną Kolejkę, zamknij i ponownie uruchom AMC. Następnie naciśnij `F5` w Plikach lokalnych i ponownie otwórz `Ctrl+Q`.

Oczekiwane: układ Kolejki pozostaje identyczny po restarcie oraz po odświeżeniu folderów. Nie wraca do kolejności alfabetycznej ani do kolejności katalogu Biblioteki.

### AMC-098-06 — Osobna Kolejka każdej sesji

Ustaw inną kolejność w Plikach lokalnych i w jednej sesji demonstracyjnej, przełączając sesje przez `Ctrl+1–9` i otwierając `Ctrl+Q`.

Oczekiwane: każda sesja zachowuje własne elementy i własną kolejność. Przenoszenie w jednej usłudze nie zmienia drugiej.

## Poprzedni zestaw alpha 97

## Nowości alpha 97

### AMC-097-01 — Tworzenie i otwieranie playlisty

Naciśnij `Ctrl+P`. Na liście Playlisty użyj Insert, wpisz własną nazwę i zatwierdź. Sprawdź F2, Enter oraz powrót przez Escape i Backspace.

Oczekiwane: NVDA czyta zwykłą nazwę playlisty, liczbę dostępnych elementów i ewentualny czas, bez nazw klas ani technicznych identyfikatorów. F2 zmienia nazwę, Enter otwiera zawartość, a Escape lub Backspace wraca na tę samą playlistę.

### AMC-097-02 — Jedna pozycja, wiele pozycji i stan mieszany

Na zwykłej liście zaznacz jeden plik i użyj `Ctrl+Shift+P`. Utwórz playlistę lub zaznacz istniejącą Spacją i zapisz Enterem. Następnie zaznacz Shiftem kilka plików, z których tylko część już należy do tej playlisty, i ponownie użyj `Ctrl+Shift+P`.

Oczekiwane: lista oznajmia „zaznaczona”, „niezaznaczona” albo „stan mieszany”. Spacja ze stanu mieszanego dodaje cały blok, ponowna Spacja usuwa cały blok. Escape anuluje zmiany, a fokus wraca do wcześniejszego elementu listy.

### AMC-097-03 — Filtr i zarządzanie w oknie playlist

W menedżerze `Ctrl+Shift+P` naciśnij `Ctrl+K`, wpisz część nazwy, następnie Escape. Sprawdź też Insert lub `Ctrl+N`, F2 i Delete.

Oczekiwane: filtr zawęża wyłącznie listę playlist; pierwszy Escape czyści niepusty filtr i wraca na listę, następny anuluje okno. Tworzenie i zmiana nazwy nie gubią fokusu. Usunięcie playlisty wymaga potwierdzenia i jasno mówi, że pliki pozostają bez zmian.

### AMC-097-04 — Kolejność, odtwarzanie i usuwanie elementu

Otwórz playlistę mającą co najmniej trzy utwory. Przesuń środkowy element przez `Alt+strzałka w górę/w dół`, uruchom go i sprawdź Page Up, Page Down oraz naturalny koniec pliku. Wróć do listy i naciśnij Delete na jednym utworze.

Oczekiwane: kolejność jest trwała, a odtwarzanie pozostaje w playliście. Delete usuwa tylko odwołanie z playlisty, nie plik z Biblioteki ani z dysku.

### AMC-097-05 — Cofanie i trwałość

Kolejno zmień nazwę playlisty, jej kolejność albo przynależność elementu i po każdej czynności użyj `Ctrl+Z`. Następnie pozostaw playlistę ze zmianami, zamknij AMC i uruchom ponownie.

Oczekiwane: każde cofnięcie odtwarza właściwy poprzedni stan i pozycję. Po restarcie pozostają nazwy, członkostwo i kolejność. Pełny eksport `.amcbackup.json` zawiera playlisty.

### AMC-097-06 — Playlisty z wyników wyszukiwania

Wyszukaj lokalny plik przez `Ctrl+F` albo `Ctrl+Shift+F`, wybierz jeden wynik lub zaznacz kilka wyników tej samej sesji i naciśnij `Ctrl+Shift+P`.

Oczekiwane: okno wyszukiwania zamyka się, otwiera się menedżer playlist właściwej sesji, a zapis przypisuje wybrane wyniki. Zaznaczenie wyników z różnych sesji pozostawia wyszukiwanie otwarte i prosi o wybranie jednej usługi; nic nie zostaje omyłkowo dodane.

## Poprzedni zestaw alpha 96

## Nowości alpha 96

### AMC-096-01 — Dokładnie zgłoszony przypadek w Ulubionych

Otwórz Ulubione, wybierz element ze środka listy, zapamiętaj jego bezpośrednich sąsiadów, naciśnij `Delete`, a potem `Ctrl+Z`.

Oczekiwane: element wraca pomiędzy tych samych sąsiadów, jest zaznaczony i NVDA nie podaje go jako ostatniego elementu listy.

### AMC-096-02 — Kolejność własna Biblioteki

Otwórz `Alt+3`, wybierz element ze środka, naciśnij `Delete` i `Ctrl+Z`. Powtórz z dwoma sąsiednimi elementami zaznaczonymi Shiftem.

Oczekiwane: pojedynczy element albo cały blok wraca na dokładne wcześniejsze miejsce i zachowuje kolejność.

### AMC-096-03 — Polecenie dodaj lub usuń z ulubionych

Na elemencie należącym do Ulubionych użyj polecenia dodaj lub usuń z ulubionych, następnie `Ctrl+Z` i ponownie otwórz Ulubione.

Oczekiwane: cofnięcie przywraca nie tylko stan Ulubiony, lecz także poprzednią pozycję elementu.

## Poprzedni zestaw alpha 95

## Nowości alpha 95

### AMC-095-01 — Jedno usunięcie i Ctrl+Z

Otwórz `Alt+3`, wybierz plik ze środka Kolejności własnej, naciśnij `Delete`, a następnie `Ctrl+Z`.

Oczekiwane: plik wraca dokładnie pomiędzy tych samych sąsiadów, jest zaznaczony i nie ląduje na końcu listy.

### AMC-095-02 — Zaznaczony blok

W Kolejności własnej zaznacz Shiftem dwa lub trzy sąsiednie pliki, usuń je i cofnij jednym `Ctrl+Z`.

Oczekiwane: cały blok wraca na poprzednią pozycję i zachowuje wewnętrzną kolejność.

### AMC-095-03 — Foldery, alfabet i nowy plik

Powtórz usunięcie i cofnięcie w `Alt+1` oraz `Alt+2`. Następnie dodaj rzeczywiście nowy plik i przejdź do `Alt+3`.

Oczekiwane: w Folderach plik wraca do właściwego folderu, we Wszystkich plikach do miejsca alfabetycznego, a jedynie nowy plik pojawia się na końcu Kolejności własnej.

## Poprzedni zestaw alpha 94

## Nowości alpha 94

### AMC-094-01 — Ctrl+Shift+C na liście i w odtwarzaczu

Na lokalnej liście zaznacz jeden plik i naciśnij `Ctrl+Shift+C`. Powtórz próbę z kilkoma plikami, a następnie w otwartym odtwarzaczu. Wklej do edytora tekstowego i do pustego folderu w Eksploratorze albo Total Commanderze.

Oczekiwane: za każdym razem pojawia się komunikat o skopiowaniu, tekst zawiera pełne ścieżki, a menedżer plików otrzymuje fizyczne pliki. Skrót nie przestaje działać po szybkim powtórzeniu.

### AMC-094-02 — Rozróżnienie konfliktu schowka

Jeżeli kopiowanie znowu zawiedzie, niczego nie zamykaj i od razu zapisz, co powiedział NVDA. Log `%LocalAppData%\AccessibleMediaController\logs\amc.log` powinien zawierać sekcję `clipboard`.

Oczekiwane: wpis „Polecenie Ctrl+Shift+C” dowodzi, że klawisz dotarł do AMC. Kolejny wpis mówi o udanym zapisie albo podaje wyjątek Windows po sześciu próbach. Jeżeli nie ma pierwszego wpisu, kombinację przejęła zewnętrzna aplikacja lub globalny dodatek, zanim dotarła do okna AMC.

### AMC-094-03 — Pola tekstowe i regresja

W polu filtra albo wyszukiwania wpisz tekst i użyj standardowych poleceń schowka. Następnie sprawdź zwykłe `Ctrl+C`, szybkie informacje, odtwarzanie pliku lokalnego oraz Bibliotekę SQLite.

Oczekiwane: niski mechanizm skrótu nie przejmuje klawiszy w polach tekstowych. Pozostałe funkcje alpha 93 działają bez regresji.

## Poprzedni zestaw alpha 93

## Nowości alpha 93

### AMC-093-01 — Jednorazowa migracja Biblioteki do SQLite

Uruchom alpha 93 na dotychczasowych danych. Sprawdź liczbę plików i źródeł oraz kilka pozycji w Bibliotece, Ulubionych, Kolejce, Historii i Zakładkach. Zamknij AMC, uruchom ponownie i sprawdź te same elementy.

Oczekiwane: pierwsze uruchomienie może potrwać chwilę, lecz nic nie znika i nie powstają duplikaty. Drugie uruchomienie odczytuje ten sam stan. W `%LocalAppData%\AccessibleMediaController` istnieje `library.db`, a w `%AppData%\AccessibleMediaController` pozostaje jednorazowa kopia `state.pre-sqlite-migration.json`.

### AMC-093-02 — Indeksowanie chmury bez pobierania

W **Folderach Biblioteki** odśwież przez `F5` folder zawierający pliki dostępne tylko online. Przejdź po nim przez Foldery, Wszystkie pliki i Albumy; użyj lewej strzałki oraz `Alt+Enter`, ale nie uruchamiaj odtwarzania.

Oczekiwane: pliki są widoczne, interfejs i NVDA pozostają responsywne, a dostawca chmury nie rozpoczyna pobierania plików ani całego folderu. Szybka informacja i właściwości określają element jako plik w chmurze zamiast wymuszać odczyt nagłówka.

### AMC-093-03 — Jawne pobranie jednego pliku

Na pliku dostępnym tylko online naciśnij Enter. Podczas pobierania użyj Tabu, menu, odczytu tytułu okna i zwykłych skrótów; w osobnej próbie anuluj przez Escape albo wybierz inny lokalny utwór.

Oczekiwane: AMC mówi „Pobieranie z chmury” i pozostaje dostępny. Pobierany jest tylko świadomie wybrany plik. Po ukończeniu zaczyna się odtwarzanie; po anulowaniu lub zmianie elementu spóźnione poprzednie żądanie nie może rozpocząć dźwięku.

### AMC-093-04 — Brak sieci, timeout i powrót do lokalnego pliku

Jeżeli możesz bezpiecznie zasymulować niedostępną chmurę, spróbuj otworzyć placeholder iCloud, OneDrive albo Google Drive. Nie czekaj na wynik, jeśli nie chcesz wykonywać pełnej próby limitu dwóch minut; sprawdź od razu anulowanie i uruchom lokalny plik.

Oczekiwane: brak sieci lub przekroczenie czasu daje pojedynczy zrozumiały błąd, nie zawiesza okna i nie zamyka AMC. Lokalny plik można następnie odtworzyć normalnie.

### AMC-093-05 — Log diagnostyczny i regresja audio

Odtwórz kolejno dwa lub trzy lokalne pliki, użyj Page Up/Page Down, przewijania, zmiany prędkości, pauzy i zamknięcia programu. Sprawdź `%LocalAppData%\AccessibleMediaController\logs\amc.log`.

Oczekiwane: wszystkie dotychczasowe funkcje audio i komunikaty NVDA działają bez regresji. Log zawiera start programu oraz etapy żądania i rozpoczęcia odtwarzania, ale nie zawiera treści plików. Pojedynczy log nie rośnie powyżej około 5 MB, a archiwów jest najwyżej cztery oprócz bieżącego.

## Poprzedni zestaw alpha 92

## Nowości alpha 92

### AMC-092-01 — Reguła pozycji bez nazw technicznych

Naciśnij `Alt+Shift+Enter` kolejno na pliku, folderze i albumie. Rozwiń pole pozycji i przejdź strzałkami po wszystkich wariantach.

Oczekiwane: NVDA czyta wyłącznie pełne etykiety, np. „Zgodnie z folderem nadrzędnym lub ustawieniem globalnym” oraz „Pamiętaj pozycję odtwarzania”. Nie pojawiają się `ResumeChoice`, `Value`, `Label`, nawiasy klamrowe ani nazwy enumów.

### AMC-092-02 — Prędkość i fokus

W tym samym oknie przejdź do pola prędkości, odczytaj wszystkie wartości, zapisz jedną, ponownie otwórz okno i anuluj.

Oczekiwane: NVDA czyta tylko „Według…” albo wartość typu „1,25 razy”. Zapisana pozycja jest zaznaczona po ponownym otwarciu, a Zapisz i Anuluj przywracają fokus do właściwego elementu listy lub odtwarzacza.

## Poprzedni zestaw alpha 91

## Nowości alpha 91

### AMC-091-01 — Szybkie powtórzenie Ctrl+Shift+C

Na istniejącym pliku lokalnym naciśnij szybko dwa albo trzy razy `Ctrl+Shift+C`. Wklej wynik najpierw do edytora tekstowego, a potem do pustego folderu w Eksploratorze lub Total Commanderze.

Oczekiwane: AMC za każdym razem pozostaje responsywny, podaje sukces dopiero po zapisaniu schowka, tekst zawiera pełną ścieżkę, a menedżer plików otrzymuje prawdziwy plik. Fokus i zaznaczenie nie zmieniają się.

### AMC-091-02 — Nazwy i wiele zaznaczonych plików

Zaznacz Shiftem kilka plików. Naciśnij szybko dwukrotnie `Ctrl+C`, sprawdź wszystkie nazwy w edytorze, a następnie powtórz próbę z `Ctrl+Shift+C` i wklejeniem plików.

Oczekiwane: żaden drugi skrót nie wyłącza dalszego kopiowania. `Ctrl+C` daje wszystkie nazwy w osobnych wierszach, a `Ctrl+Shift+C` wszystkie ścieżki oraz wszystkie istniejące pliki.

### AMC-091-03 — Wyszukiwanie, właściwości i menedżer schowka

Powtórz kopiowanie z wyników `Ctrl+F`, z globalnych wyników oraz przyciskiem „Kopiuj wszystko” w `Alt+Enter`. Jeśli używasz Ditto lub podobnego programu, pozostaw go włączonego.

Oczekiwane: wszystkie miejsca mają tę samą odporność. Chwilowe zajęcie schowka powoduje krótkie ponowienie, a trwałe zajęcie daje komunikat „Schowek jest zajęty przez inną aplikację” z kodem, bez zawieszenia AMC.

## Poprzedni zestaw alpha 90

## Nowości alpha 90

### AMC-090-01 — Opcje pojedynczego pliku

W Folderach, Wszystkich plikach i Ulubionych wybierz ten sam plik lokalny i naciśnij `Alt+Shift+Enter`. Zmień pamiętanie pozycji lub prędkość, zapisz, ponownie otwórz opcje i uruchom plik.

Oczekiwane: za każdym razem otwiera się okno ustawień tego pliku; nie pojawia się komunikat „Nie można odnaleźć ustawień tego pliku w Bibliotece”. Wartość jest zachowana po zmianie widoku i ponownym uruchomieniu AMC.

### AMC-090-02 — Opcje folderu i albumu

W widoku Foldery zaznacz Folder Biblioteki, potem zwykły podfolder, a w widoku Albumy zaznacz album. Na każdym użyj `Alt+Shift+Enter`, ustaw inną prędkość albo regułę pozycji i zapisz.

Oczekiwane: otwiera się dostępne okno „Opcje odtwarzania folderu”. Ustawienie obejmuje pliki znajdujące się poniżej wybranego folderu lub w albumie, ale nie zmienia plików ani układu katalogów.

### AMC-090-03 — Hierarchia nadpisań

Ustaw prędkość dla folderu nadrzędnego, inną dla jego podfolderu i trzecią dla jednego pliku. Odtwórz kolejno plik z nadpisaniem, drugi plik z podfolderu oraz plik tylko z folderu nadrzędnego. Następnie wybierz „według…” na jednym z poziomów.

Oczekiwane: obowiązuje kolejność `plik > najbliższy folder > folder nadrzędny > sesja`. Wybranie dziedziczenia usuwa tylko bieżące nadpisanie i odsłania wartość wyższego poziomu.

### AMC-090-04 — Fokus, zapis i regresja Backspace

Anuluj i zapisz opcje z pliku oraz folderu, używając klawiatury i NVDA. Po zamknięciu okna sprawdź fokus. Następnie użyj `Backspace` i `Delete` na bezpiecznych elementach.

Oczekiwane: fokus wraca na ten sam wiersz listy, program nie zaczyna sam odtwarzać, `Backspace` przechodzi do rodzica i niczego nie usuwa, a `Delete` zachowuje swoje dotychczasowe znaczenie.

## Poprzedni zestaw alpha 89

## Nowości alpha 89

### AMC-089-01 — Ręczna kolejność Ulubionych

Otwórz `Ctrl+U`, zaznacz jeden element, a potem ciągły blok dwóch elementów i użyj `Alt+strzałka w górę/w dół`. Przejdź do innego widoku, wróć do Ulubionych i uruchom AMC ponownie.

Oczekiwane: element lub blok przesuwa się o jeden wiersz, fokus i zaznaczenie zostają zachowane, a kolejność przetrwa zmianę widoku i restart. Przy aktywnym filtrze przesuwanie jest zablokowane jasnym komunikatem.

### AMC-089-02 — Ulubione jako kontekst odtwarzania

W Ulubionych uruchom element, otwórz odtwarzacz i użyj `Page Up` oraz `Page Down`. Pozwól też jednemu krótkiemu plikowi zakończyć się naturalnie.

Oczekiwane: poprzedni, następny i automatycznie uruchomiony element pochodzą z kolejności Ulubionych, a nie z pierwotnego folderu ani płaskiej Biblioteki. Na początku i końcu lista nie zapętla się.

### AMC-089-03 — Kontekst albumu, folderu i kolejności własnej

Powtórz nawigację `Page Up/Page Down` po uruchomieniu elementu kolejno z otwartego Albumu, konkretnego Folderu i `Alt+3`. Po uruchomieniu utworu przejdź bez odtwarzania do innego widoku, wróć `F6` i wybierz następny plik.

Oczekiwane: każdy start ustanawia kolejność bieżącej nieprzefiltrowanej listy. Samo przeglądanie innego widoku nie zmienia kontekstu. Filtr `Ctrl+K` nie ogranicza odtwarzania do chwilowych wyników.

### AMC-089-04 — Kolejka ma pierwszeństwo i wraca do listy

Uruchom element ze środka Ulubionych albo albumu, dodaj inny element jako następny lub do Kolejki i pozwól obu zakończyć się kolejno.

Oczekiwane: najpierw odtwarza się element jawnie dodany do Kolejki, a potem AMC wraca do elementu następującego po pierwotnym utworze w zapamiętanym kontekście. Element kolejki nie jest odtwarzany drugi raz.

### AMC-089-05 — Opcje elementu Alt+Shift+Enter

Na lokalnym pliku naciśnij `Alt+Shift+Enter`. Ustaw „Zawsze od początku” oraz prędkość inną niż sesji, zapisz i odtwórz plik. Przejdź do innego pliku i wróć. Następnie zmień wariant na „Pamiętaj pozycję odtwarzania”, zatrzymaj materiał w środku i uruchom program ponownie.

Oczekiwane: okno ma zwykłe dostępne pola, Zapisz i Anuluj, a fokus wraca do listy lub odtwarzacza. Prędkość nadpisania działa tylko dla wybranego pliku, inny plik wraca do prędkości sesji. Reguła od początku nie wznawia pozycji; reguła pamiętania ją zachowuje.

### AMC-089-06 — Informacje i przyszłe wyjście audio

Po zapisaniu opcji naciśnij `Alt+Enter`, przeczytaj sekcję „W aplikacji”, a następnie ponownie otwórz `Alt+Shift+Enter`.

Oczekiwane: `Alt+Enter` pozostaje tekstem tylko do odczytu i podaje skuteczną regułę wznawiania, prędkość elementu oraz domyślne wyjście współdzielone. W opcjach urządzenie jest jednoznacznie nieaktywne, a okno nie udaje, że zmiana wyjścia lub EQ jest już zaimplementowana.

### AMC-089-07 — Alias tytułu i kolejność albumu

W albumie z plikami `01`, `02` i `03` zmień przez `F2` nazwy widoczne w Bibliotece tak, aby nie zaczynały się cyframi. Otwórz album ponownie i przejdź po nim także przez `Page Down`.

Oczekiwane: NVDA czyta własne aliasy, ale kolejność albumu nadal jest 01, 02, 03 według rzeczywistych nazw plików. `F2` nie zmienia pliku na dysku.

### AMC-089-08 — Przejdź do albumu i wykonawcy

Na utworze rozpoznanego albumu w Ulubionych, Kolejce albo odtwarzaczu otwórz menu kontekstowe. Wybierz kolejno „Przejdź do albumu” i „Przejdź do wykonawcy”.

Oczekiwane: pierwsze polecenie otwiera zawartość właściwego albumu, drugie — folder wykonawcy z fokusem na folderze albumu. Polecenia nie pojawiają się dla luźnego pliku bez rozpoznanej relacji. Obowiązuje zwykła reguła opuszczania odtwarzacza, w tym ustawienie pauzy po Escape lub przejściu do listy.

### AMC-089-09 — Widoki bez ręcznego sortowania

Spróbuj `Alt+strzałka w górę/w dół` w Folderach (`Alt+1`), Wszystkich plikach (`Alt+2`), Albumie, Historii i wynikach wyszukiwania.

Oczekiwane: żaden z tych widoków nie zmienia kolejności. Foldery odpowiadają dyskowi, Wszystkie pliki są alfabetyczne, Album respektuje numery ścieżek, a Historia i wyszukiwanie zachowują własną semantykę.

### AMC-089-10 — Backspace jest tylko poziomem nadrzędnym

Sprawdź `Backspace` kolejno: na pliku w Folderach, w podfolderze, wewnątrz Albumu, w otwartym odtwarzaczu, na najwyższym poziomie Ulubionych oraz podczas edycji filtra. Osobno sprawdź `Delete` na bezpiecznym elemencie.

Oczekiwane: `Backspace` nie usuwa żadnego elementu. W Folderach idzie do rodzica, z Albumu do listy Albumów, z odtwarzacza do poprzedniej listy, a na poziomie głównym podaje brak rodzica. W polu filtra usuwa znak. Dopiero `Delete` wykonuje właściwe usuwanie z bieżącego widoku.

## Poprzedni zestaw alpha 88

### AMC-088-01 — Album z numerowanych plików

W Folderze Biblioteki przygotuj lub znajdź układ `Wykonawca\Album\01…`, `02…`, `10…`. Naciśnij `Ctrl+Shift+A`.

Oczekiwane: lista Albumy zawiera nazwę folderu albumu, nazwę folderu wykonawcy i poprawną liczbę utworów. Nie pojawia się ścieżka techniczna ani nazwa klasy programu.

### AMC-088-02 — Wejście, kolejność i powrót

Naciśnij Enter na rozpoznanym albumie, przejdź po utworach, uruchom jeden z nich i wróć z odtwarzacza. Następnie naciśnij Escape na liście utworów albumu.

Oczekiwane: pliki są w kolejności 01, 02, …, 10, niezależnie od zwykłego porządku tekstowego. Powrót z odtwarzacza prowadzi do tego samego utworu, a Escape z listy utworów do wcześniej zaznaczonego albumu.

### AMC-088-03 — Brak fałszywych albumów

Sprawdź folder z jednym plikiem, folder kilku plików bez numerów oraz nagrania zaczynające się od roku, np. `2026-08-24…`.

Oczekiwane: żaden z tych folderów nie pojawia się samoczynnie jako Album. AMC nie otwiera plików tylko po to, aby przeprowadzić klasyfikację, i nie wymusza pobrania całego Folderu Biblioteki z chmury.

### AMC-088-04 — Filtr i nawigacja literowa

Na liście Albumy wpisz pierwsze litery nazwy albumu, następnie użyj `Ctrl+K`, wpisz fragment nazwy i naciśnij Escape. Powtórz filtr wewnątrz albumu.

Oczekiwane: nawigacja literowa korzysta z nazwy albumu. Filtr dotyczy tylko aktualnego poziomu; Escape najpierw czyści aktywny filtr, a dopiero następny Escape opuszcza zawartość albumu.

### AMC-088-05 — Menu kontenera i działania utworu

Otwórz menu kontekstowe na wierszu albumu, a potem na utworze wewnątrz. Na bezpiecznym utworze sprawdź `Ctrl+C`, `Ctrl+Shift+C`, F2, Delete i natychmiastowe `Ctrl+Z`.

Oczekiwane: sam album oferuje otwarcie i informacje, ale nie udaje pliku ani elementu możliwego do dodania do kolejki. Utwór zachowuje zwykłe działania Biblioteki. Delete pozostawia plik na dysku, a `Ctrl+Z` przywraca go do Albumu.

### AMC-088-06 — Restart i inne sesje

Zamknij AMC będąc wewnątrz albumu, uruchom program ponownie i naciśnij `Ctrl+Shift+A`. Sprawdź też Albumy w demonstracyjnej sesji streamingowej.

Oczekiwane: start nie otwiera osieroconego ani technicznie nazwanego poziomu; lokalnie pojawia się lista Albumów. Istniejący album demonstracyjny innej usługi nadal jest widoczny i nie podlega lokalnej heurystyce folderów.

## Poprzedni zestaw alpha 87

### AMC-087-01 — Trzy układy Biblioteki

Na lokalnej liście użyj kolejno `Alt+1`, `Alt+2` i `Alt+3`, a potem `Ctrl+L`. Sprawdź menu Widok i paletę `Ctrl+Shift+K`.

Oczekiwane: `Alt+1` otwiera Foldery Biblioteki, `Alt+2` — Wszystkie pliki alfabetycznie, a `Alt+3` — Kolejność własną. `Ctrl+L` wraca do ostatniego z tych układów. NVDA podaje nazwę układu bez technicznych identyfikatorów.

### AMC-087-02 — Przesuwanie jednego pliku

W Kolejności własnej zaznacz plik znajdujący się z dala od początku i końca. Naciśnij `Alt+strzałka w górę`, a następnie `Alt+strzałka w dół`.

Oczekiwane: plik przesuwa się dokładnie o jedną pozycję, pozostaje zaznaczony, a NVDA mówi krótko „Przeniesiono wyżej” albo „Przeniesiono niżej”. Nazwa i pełna ścieżka z `Ctrl+Shift+C` nie zmieniają się.

### AMC-087-03 — Zaznaczenie wielu elementów i granice

Zaznacz Shiftem dwa lub trzy sąsiadujące pliki i przesuń je w górę oraz w dół. Spróbuj także przejść poza początek lub koniec listy. Jeśli wygodnie, sprawdź nieciągłe zaznaczenie myszą lub klawiaturą wspomagającą.

Oczekiwane: ciągły blok zachowuje kolejność wewnętrzną i przesuwa się razem. Na granicy AMC mówi o początku albo końcu. Nieciągłe zaznaczenie nie jest przestawiane i otrzymuje jasny komunikat.

### AMC-087-04 — Trwałość i nowe pliki

Zmień kolejność kilku pozycji, przejdź do `Alt+2`, wróć przez `Alt+3`, uruchom AMC ponownie i ponownie wybierz `Alt+3`. Następnie dodaj bezpieczny plik przez `Ctrl+O` albo do Folderu Biblioteki i użyj `F5`.

Oczekiwane: ręczny porządek przetrwa zmianę widoku i restart. `Alt+2` pozostaje alfabetyczne i nie przejmuje ręcznych zmian. Nowy plik zostaje dopisany na końcu Kolejności własnej.

### AMC-087-05 — Filtr nie zostaje ukrytą pułapką

W `Alt+3` naciśnij `Ctrl+K`, wpisz fragment nazwy i spróbuj `Alt+strzałka w górę`. Następnie naciśnij Escape. Powtórz filtr i zamiast Escape przejdź do `Alt+2`, `Alt+1`, innego folderu oraz innej sesji; wróć za każdym razem do wcześniejszego miejsca. Na końcu zamknij AMC z aktywnym filtrem i uruchom ponownie.

Oczekiwane: przy filtrze przesuwanie jest zablokowane. Escape czyści filtr i od razu przenosi fokus na pełną listę. Zmiana widoku, folderu albo sesji również usuwa filtr; nie wraca on po powrocie ani po restarcie.

### AMC-087-06 — Delete, wklejanie i regresja

W Kolejności własnej sprawdź Delete na bezpiecznym rekordzie i natychmiastowe `Ctrl+Z`. Wklej lokalny plik przez `Ctrl+V`, sprawdź menu kontekstowe oraz wyrywkowo Enter, Escape, `Ctrl+C`, `Ctrl+Shift+C`, F2, Shift+F2, wyszukiwanie i odtwarzacz.

Oczekiwane: Delete usuwa wyłącznie przynależność do Biblioteki i pozostawia plik na dysku, a `Ctrl+Z` przywraca rekord. Wklejony nowy plik trafia na koniec Kolejności własnej. Wyszukiwanie nie pozwala przestawiać wyników, a dotychczasowe funkcje nie mają regresji.

## Poprzedni zestaw alpha 86

Do prób `Shift+F2` użyj kopii pliku, którego utrata nie będzie problemem. AMC nie nadpisuje istniejącego pliku, ale test dotyczy rzeczywistej nazwy na dysku.

### AMC-086-01 — F5 i Ctrl+F5

Na lokalnej liście naciśnij `F5`, a następnie `Ctrl+F5`. Sprawdź też polecenia „Odśwież foldery Biblioteki” i „Foldery Biblioteki” w palecie `Ctrl+Shift+K`.

Oczekiwane: `F5` skanuje Foldery Biblioteki, `Ctrl+F5` otwiera ich okno, a oba polecenia są czytelnie opisane wraz ze skrótami. Po zamknięciu okna fokus wraca do listy.

### AMC-086-02 — F2 zmienia tylko nazwę w AMC

Zaznacz jeden lokalny plik, zapamiętaj jego pełną ścieżkę przez `Ctrl+Shift+C`, naciśnij `F2` i wpisz własną nazwę. Sprawdź ten element w Bibliotece, Ulubionych, Kolejce, Historii i Zakładkach, jeśli występuje, a następnie uruchom AMC ponownie i użyj `F5`.

Oczekiwane: wszędzie pojawia się nowa nazwa, lecz ścieżka i nazwa pliku na dysku pozostają bez zmian. Nazwa przetrwa restart oraz skan źródeł. Wpisanie później nazwy pliku bez rozszerzenia wyłącza alias i przywraca zwykłe zachowanie.

### AMC-086-03 — Shift+F2 zmienia plik na dysku

Na bezpiecznej kopii naciśnij `Shift+F2`, podaj nową nazwę bez rozszerzenia i zatwierdź. Sprawdź `Ctrl+Shift+C`, odtwarzanie, zapamiętaną pozycję, Ulubione, Kolejkę i Zakładki. Powtórz próbę na pliku wcześniej załadowanym do odtwarzacza.

Oczekiwane: rozszerzenie nie zmienia się ani nie dubluje, plik ma nową ścieżkę, a jego stabilny rekord i powiązane dane pozostają. Jeśli plik był załadowany, AMC zatrzymuje odtwarzanie, zwalnia uchwyt i zachowuje pozycję do wznowienia.

### AMC-086-04 — Ochrona nazwy i fokus

Spróbuj wpisać pustą nazwę, nazwę z niedozwolonym znakiem, nazwę zarezerwowaną Windows, np. `CON`, oraz nazwę już istniejącego pliku. Anuluj oba okna klawiszem Escape. Sprawdź również F2 przy zaznaczeniu wielu elementów, na folderze oraz w innej sesji.

Oczekiwane: AMC niczego nie nadpisuje i podaje jasny powód odmowy. Escape nie zmienia danych. Zmiana nazwy wymaga jednego pliku lokalnego, a po każdym oknie fokus wraca na ten plik.

### AMC-086-05 — Menu i regresja list

Na pliku lokalnym otwórz menu **Edycja**, menu kontekstowe i paletę poleceń. Potem wyrywkowo sprawdź Enter, Escape, `Alt+1/2`, `Ctrl+C`, `Ctrl+Shift+C`, Delete, `Shift+Delete`, wyszukiwanie i odtwarzacz.

Oczekiwane: obie operacje zmiany nazwy są dostępne z opisanymi skrótami, natomiast dotychczasowe działania nie zmieniły znaczenia. `F2` i `Shift+F2` nie przejmują klawiszy w polach tekstowych ani w odtwarzaczu.

## Poprzedni zestaw alpha 85

## Poprawka alpha 85

### AMC-085-01 — Nazwy źródeł bez danych technicznych

Otwórz **Plik → Foldery Biblioteki** i przejdź strzałkami po wszystkich folderach.

Oczekiwane: NVDA czyta wyłącznie przygotowaną etykietę, na przykład nazwę `Sideloads`, dostępność, zasadę pamiętania pozycji, liczby plików i ścieżkę. Nie może czytać `LocalFolderSourceStatus`, `Id`, `DisplayName`, `IsReachable`, nazw innych pól programistycznych ani nawiasów technicznego rekordu.

### AMC-085-02 — Szczegóły, ustawienie i fokus

Na wybranym folderze przejdź Tabem do szczegółów oraz ustawienia pamiętania pozycji, zmień wartość i wybierz **Zapisz dla folderu**. Wróć Shift+Tabem do listy.

Oczekiwane: lista nadal ma krótką czytelną etykietę, szczegóły zawierają pełną ścieżkę, zapis nie gubi wyboru, a fokus można przewidywalnie przywrócić do listy.

## Poprzedni zestaw regresyjny alpha 84

## Nowości alpha 84

### AMC-084-01 — Domyślny Escape

Odtwórz lokalny plik, przejdź do odtwarzacza, odczekaj kilkanaście sekund i naciśnij `Escape`.

Oczekiwane: dźwięk zostaje wstrzymany, a fokus wraca na listę do logicznie tego samego elementu. Zwykłe strzałki listy nadal służą do nawigacji i nie sterują czasem ani głośnością.

### AMC-084-02 — Wznowienie zapamiętywanego pliku

Przy domyślnie zaznaczonej opcji **Pamiętaj pozycje lokalnych plików** odtwórz dłuższe nagranie, wyjdź `Escape`, wróć przez `F6` i uruchom odtwarzanie. Powtórz po ponownym uruchomieniu AMC.

Oczekiwane: `Escape` zatrzymuje dźwięk, lecz nie zeruje zapamiętanego miejsca. Wznowienie zaczyna się od ostatniej pozycji także po restarcie.

### AMC-084-03 — Folder muzyczny zawsze od początku

Otwórz **Plik → Foldery Biblioteki**, wybierz bezpieczny folder z muzyką, ustaw **Dla wybranego folderu** na **Zawsze od początku** i zapisz. Odtwórz plik z tego folderu, wyjdź `Escape`, wróć do niego i uruchom ponownie.

Oczekiwane: okno i jego lista czytelnie podają nową zasadę. Po `Escape` plik z tego folderu rozpoczyna się od `0:00`; stara pozycja nie wraca także po restarcie.

### AMC-084-04 — Nadpisanie ustawienia ogólnego

Wyłącz w Ustawieniach ogólnych **Pamiętaj pozycję odtwarzania lokalnych plików**. Dla jednego folderu ustaw jednak **Pamiętaj pozycję odtwarzania**, a dla drugiego pozostaw **Zgodnie z ustawieniem globalnym**. Sprawdź po jednym pliku z każdego folderu.

Oczekiwane: pierwszy folder wznawia miejsce mimo wyłączonej zasady ogólnej, drugi zaczyna od początku. Plik dodany pojedynczo przez `Ctrl+O` korzysta z zasady ogólnej.

### AMC-084-05 — Opcjonalne granie po wyjściu

Odznacz **Wstrzymuj odtwarzanie po wyjściu z odtwarzacza**, rozpocznij odtwarzanie i naciśnij `Escape`. Potem ponownie włącz tę opcję.

Oczekiwane: przy wyłączonej opcji lista się pojawia, ale dźwięk trwa. Po ponownym włączeniu `Escape`, `Shift+F6`, przycisk **Wróć do listy** oraz bezpośrednie przejście do widoku wstrzymują dźwięk.

### AMC-084-06 — Paleta, fokus i regresja

W palecie `Ctrl+Shift+K` wyszukaj obie nowe opcje ustawień. Sprawdź fokus po Zapisz i Anuluj, przełączanie sesji z otwartego odtwarzacza, `Page Up/Down`, przewijanie i głośność w odtwarzaczu oraz zwykłą nawigację listy.

Oczekiwane: paleta otwiera Ustawienia na właściwym polu wyboru. Opuszczana sesja zostaje wstrzymana zgodnie z opcją, NVDA nie traci fokusu, a dotychczasowe skróty listy i odtwarzacza nie mają regresji.

## Poprzedni zestaw regresyjny alpha 83

## Nowości alpha 83

Do prób odłączania wybierz folder, który można później bezpiecznie dodać ponownie. **Odłącz folder** nie może usuwać żadnego pliku z dysku.

### AMC-083-01 — Fokus i zawartość menedżera

Otwórz **Plik → Foldery Biblioteki**. Sprawdź pierwszą kontrolkę, nawigację strzałkami i Tabem oraz Escape.

Oczekiwane: fokus zaczyna na zwykłej liście źródeł. Każdy wpis podaje nazwę, dostępność, liczby aktywnych, niedostępnych i wykluczonych plików oraz ścieżkę. Tab prowadzi do przycisków, Escape zamyka tylko menedżer, a fokus wraca do głównej listy AMC.

### AMC-083-02 — Odśwież jeden i wszystkie Foldery Biblioteki

W oknie **Foldery Biblioteki** wybierz folder i użyj **Odśwież wybrane**, następnie **Odśwież wszystkie**.

Oczekiwane: operacje kończą się czytelnym komunikatem, lista zachowuje zaznaczenie, NVDA nie traci fokusu, a niedostępny folder nie powoduje skasowania zapisanych rekordów.

### AMC-083-03 — Ochrona przed nakładającymi się folderami

Mając dodany Folder Biblioteki, spróbuj dodać ponownie tę samą ścieżkę, jej podfolder, a następnie folder nadrzędny obejmujący istniejący Folder Biblioteki.

Oczekiwane: ta sama ścieżka jest jedynie ponownie skanowana. Podfolder i folder nadrzędny nie tworzą drugiego Folderu Biblioteki; AMC jednoznacznie podaje, z którym istniejącym folderem wystąpił konflikt.

### AMC-083-04 — Bezpieczne odłączenie

Zanotuj liczbę plików i stan przykładowego folderu, wybierz **Odłącz folder**, przeczytaj całe pytanie i zatwierdź. Sprawdź dysk, `Alt+1`, `Alt+2`, Ulubione, Kolejkę, Historię oraz Zakładki. Uruchom AMC ponownie.

Oczekiwane: folder znika tylko z listy automatycznej synchronizacji. Żaden plik na dysku nie zostaje usunięty. Rekordy i ich relacje pozostają dostępne jako pliki dodane pojedynczo także po restarcie. Jeśli bieżący poziom Folderów należał do odłączonego folderu, AMC bezpiecznie wraca na główny poziom.

### AMC-083-05 — Pełna kopia AMC

W menedżerze wybierz **Eksportuj pełną kopię AMC**. Zapisz plik `.amcbackup.json` w bezpiecznym miejscu. Sprawdź też opis w **Ustawienia → Import i eksport**.

Oczekiwane: komunikat potwierdza eksport katalogu Biblioteki, źródeł, wykluczeń, Ulubionych, kolejek, historii, zakładek, pozycji i ustawień. Opis w Ustawieniach wymienia te dane i zaznacza brak haseł oraz tokenów. Sam eksport niczego nie zmienia w Bibliotece.

### AMC-083-06 — Paleta i regresja chmur

Otwórz `Ctrl+Shift+K`, wyszukaj „foldery biblioteki” i uruchom polecenie. Potem wykonaj `F5`, `Alt+1`, `Alt+2` oraz wyrywkowy test Folderu Biblioteki z iCloud, OneDrive albo Google Drive.

Oczekiwane: paleta otwiera ten sam menedżer. Dotychczasowa nawigacja, bezpieczne skanowanie chmury, kopiowanie, odtwarzanie i kolejki działają bez regresji.

## Poprzedni zestaw regresyjny alpha 82

## Poprawki alpha 82

### AMC-082-01 — OneDrive bez masowego pobierania

Przez `Ctrl+Shift+O` dodaj niewielki folder OneDrive zawierający kilka plików audio dostępnych lokalnie i, jeśli masz takie dane testowe, kilka pozycji „tylko online”.

Oczekiwane: AMC pokazuje nazwy obu rodzajów plików, nie pomija folderu z atrybutem Cloud Files i nie rozpoczyna samoczynnego odtwarzania ani pobierania całej zawartości.

### AMC-082-02 — Google Drive Mirror

Jeżeli używasz trybu lustrzanego Google Drive, dodaj niewielki folder z plikami audio i wykonaj `F5`.

Oczekiwane: źródło zachowuje się jak zwykły folder lokalny, bez duplikatów i bez specjalnych komunikatów chmurowych.

### AMC-082-03 — Google Drive Stream albo niedostępny dysk

Jeżeli używasz trybu strumieniowanego, dodaj folder z wirtualnego dysku Google Drive, zamknij AMC, zatrzymaj Google Drive for desktop, uruchom AMC i użyj `F5`. Następnie ponownie uruchom Google Drive i odśwież źródła. Jeśli nie masz trybu Stream, test można pominąć.

Oczekiwane: podczas niedostępności AMC nie usuwa rekordów, historii, zakładek ani pozycji. Informuje o niedostępnym źródle. Po powrocie dysku te same rekordy stają się dostępne bez duplikatów.

### AMC-082-04 — Regresja iCloud i Alt+1

Powtórz test `Sideloads` oraz przejście z zaznaczonego pliku w `Alt+2` do `Alt+1`.

Oczekiwane: `Sideloads` nadal pokazuje 317 zapisanych plików audio, a `Alt+1` prowadzi do folderu zaznaczonego elementu.

## Poprzedni zestaw regresyjny alpha 81

## Poprawki alpha 81

### AMC-081-01 — Naprawa źródła Sideloads

Uruchom alfę 81 na dotychczasowym stanie i otwórz Foldery Biblioteki przez `Alt+1`. Wejdź do źródła `Sideloads`.

Oczekiwane: jednorazowa migracja usuwa błędne zbiorowe wykluczenie z alfy 80, źródło nie jest puste i pokazuje rozpoznane pliki oraz podfoldery. Samo indeksowanie nie powinno rozpoczynać odtwarzania ani pobierania wszystkich nagrań z iCloud.

### AMC-081-02 — Alt+2 do Alt+1

W `Alt+2` zaznacz plik znajdujący się kilka poziomów pod jednym ze źródeł, a następnie naciśnij `Alt+1`.

Oczekiwane: AMC otwiera folder nadrzędny tego pliku, pozostawia fokus na tym samym pliku i odczytuje położenie w Folderach. Dla pliku dodanego pojedynczo przez `Ctrl+O`, który nie leży w żadnym źródle, `Alt+1` przechodzi do głównego poziomu Folderów i zachowuje zaznaczenie na samodzielnym pliku.

### AMC-081-03 — Placeholder iCloud i F5

W źródle iCloud sprawdź plik dostępny tylko jako placeholder, następnie naciśnij `F5`.

Oczekiwane: plik pozostaje w Bibliotece, skanowanie nie uznaje `ReparsePoint` za dowiązanie i nie uruchamia pobierania treści. Prawdziwe dowiązania katalogów nie są przeszukiwane.

### AMC-081-04 — Kolejki poszczególnych sesji

Dodaj element do Kolejki w WiiM lub innej demonstracyjnej sesji. Otwórz lokalny folder przez `Ctrl+Shift+O`, sprawdź lokalną Kolejkę, a potem wróć do poprzedniej sesji i ponownie otwórz jej Kolejkę.

Oczekiwane: po otwarciu folderu aktywna jest sesja Pliki lokalne, której Kolejka może być pusta. Powrót do poprzedniej sesji pokazuje jej wcześniejszą Kolejkę; samo skanowanie lokalnego źródła jej nie czyści.

## Poprzedni zestaw regresyjny alpha 80

## Nowości alpha 80

Do prób z kasowaniem i zmianą nazwy przygotuj osobny folder oraz niepotrzebne kopie plików. Nie wykonuj `Shift+Delete` na jedynym egzemplarzu nagrania.

### AMC-080-01 — Kilka źródeł i dwa układy Biblioteki

Dodaj przez `Ctrl+Shift+O` co najmniej dwa różne foldery, najlepiej z podfolderami. Użyj `Alt+1`, `Alt+2`, a potem `Ctrl+L`.

Oczekiwane: `Alt+1` pokazuje **Foldery Biblioteki** z osobnymi źródłami, `Alt+2` jedną alfabetyczną listę **Wszystkie pliki**, a `Ctrl+L` wraca do ostatnio wybranego układu. Są to dwa widoki tych samych rekordów, bez duplikatów.

### AMC-080-02 — Automatyczne dodanie pliku

Przy uruchomionym AMC skopiuj z zewnątrz nowy rozpoznawany plik audio do jednego ze źródeł i odczekaj około dwóch sekund. Sprawdź oba układy.

Oczekiwane: plik pojawia się bez ponownego wybierania folderu i bez przejęcia fokusu. Jeśli obserwator danego nośnika nie działa, `F5` dodaje plik po pełnym skanowaniu.

### AMC-080-03 — Brak pliku i jego powrót

Przenieś testowy plik poza źródło bez używania AMC. Sprawdź listę, a następnie umieść go z powrotem dokładnie pod tą samą ścieżką i naciśnij `F5`, jeśli zmiana nie pojawi się samoczynnie.

Oczekiwane: brakujący plik znika z aktywnych list, lecz AMC nie usuwa jego historii, zakładek ani zapamiętanej pozycji. Po powrocie pojawia się jako ten sam rekord.

### AMC-080-04 — Trwałe wykluczenie przez Delete

Zaznacz testowy plik i naciśnij zwykły `Delete`. Potem użyj `F5`, przełącz `Alt+1` i `Alt+2`, zakończ AMC i uruchom je ponownie.

Oczekiwane: plik pozostaje na dysku, ale nie wraca do Biblioteki po skanowaniu ani restarcie. W osobnej próbie wykonanej bezpośrednio po Delete `Ctrl+Z` przywraca go od razu.

### AMC-080-05 — Niedostępne całe źródło

Jeśli masz bezpieczny testowy folder na nośniku odłączanym, zarejestruj go, zamknij AMC, odłącz nośnik i uruchom program. Alternatywnie tymczasowo zmień nazwę folderu przy zamkniętym AMC. Użyj `F5`.

Oczekiwane: AMC zgłasza jedno niedostępne źródło, ale nie uznaje go za pusty folder i nie kasuje jego danych. Po ponownym udostępnieniu źródła rekordy wracają.

### AMC-080-06 — Zmiana nazwy podczas działania

Przy uruchomionym AMC zmień poza programem nazwę testowego pliku, a potem nazwę jego folderu. Wcześniej można dodać zakładkę lub zapamiętać pozycję odtwarzania.

Oczekiwane: lista aktualizuje nazwę i ścieżkę, nie tworzy duplikatu, a stan rekordu pozostaje. Ruch wykonany przy zamkniętym AMC nie jest jeszcze gwarantowany jako ten sam rekord i stanowi znane ograniczenie tej wersji.

### AMC-080-07 — Plik aktualnie odtwarzany staje się niedostępny

Odtwórz niepotrzebną kopię, a następnie przenieś ją poza źródło w Eksploratorze lub innym menedżerze plików.

Oczekiwane: AMC zatrzymuje własny tor, informuje, który plik stał się niedostępny, nie blokuje NVDA i pozostawia pozostałe elementy aktywne.

### AMC-080-08 — Nawigacja po znakach specjalnych

Umieść w jednym widocznym poziomie kopie o nazwach zaczynających się np. od `!`, `@`, `#` albo cyfry. Sprawdź wpisywanie znaków oraz `Shift+cyfrę` właściwą dla polskiego układu klawiatury, a następnie `Alt+1` i `Alt+2`.

Oczekiwane: znaki specjalne uczestniczą w nawigacji po nazwach; `Alt+1/2` przełącza układ i nie przechwytuje `Shift+cyfr`.

### AMC-080-09 — Wyszukiwanie po synchronizacji

Wyklucz jeden plik Delete, przenieś drugi poza źródło, dodaj trzeci i wykonaj `Ctrl+F` dla ich nazw.

Oczekiwane: wyszukiwanie lokalne znajduje nowy aktywny plik, ale nie zwraca wykluczonego ani niedostępnego.

### AMC-080-10 — Regresja działań i fokusu

W obu układach sprawdź Enter, Escape, `Ctrl+C`, `Ctrl+Shift+C`, Ulubione, Kolejkę, zakładki, `Alt+Enter`, zaznaczanie Shiftem oraz menu kontekstowe. Uruchom też pełne `F5` z fokusem wewnątrz listy.

Oczekiwane: stan jest wspólny w obu układach, fokus pozostaje na logicznie tym samym elemencie, a odświeżenie nie otwiera filtra ani nie przenosi fokusu na pasek stanu.

## Poprzedni zestaw regresyjny alpha 79

## Poprawki alpha 79

Do testu fizycznego kasowania używaj wyłącznie niepotrzebnych kopii plików. Zwykły `Delete` nie powinien w tym zestawie usuwać niczego z dysku.

### AMC-079-01 — Folder jako źródło Biblioteki

W pustej albo dotychczasowej sesji Pliki lokalne naciśnij `Ctrl+Shift+O` i wybierz folder zawierający rozpoznane pliki w katalogu głównym oraz podfolderach. Po zakończeniu skanowania naciśnij `Ctrl+L`.

Oczekiwane: Foldery pokazują rzeczywistą hierarchię, natomiast Biblioteka pokazuje płaską alfabetyczną listę wszystkich wczytanych plików. Nie pojawia się dawna, niezależna biblioteka ani duplikaty.

### AMC-079-02 — Ponowne włączenie znanego pliku

W Bibliotece usuń zwykłym `Delete` jeden plik, upewnij się, że zniknął z `Ctrl+L`, a następnie ponownie wybierz jego źródło przez `Ctrl+Shift+O` i wróć do Biblioteki.

Oczekiwane: znany plik wraca do Biblioteki bez powstania drugiego rekordu. Komunikat rozróżnia przywrócenie od dodania nowego pliku.

### AMC-079-03 — Delete na pliku w Folderach

W widoku Foldery zaznacz plik należący do Biblioteki i naciśnij `Delete`. Następnie sprawdź `Ctrl+L`, wróć do Folderów i użyj `Ctrl+Z`.

Oczekiwane: program mówi, że usunął plik z Biblioteki i pozostawił go w folderze. Plik znika z płaskiej Biblioteki, ale nadal istnieje na dysku i pozostaje widoczny w swoim fizycznym folderze. `Ctrl+Z` przywraca jego przynależność do Biblioteki.

### AMC-079-04 — Delete na wierszu folderu

Zaznacz podfolder albo wiersz źródła i naciśnij `Delete`.

Oczekiwane: nic nie zostaje usunięte. AMC wyjaśnia, że Delete nie usuwa folderu ani źródła, Enter otwiera folder, a zarządzanie źródłami będzie osobnym poleceniem.

### AMC-079-05 — Rozdzielenie Delete i Shift+Delete

Na niepotrzebnej kopii pliku porównaj `Delete` oraz `Shift+Delete` z potwierdzeniem.

Oczekiwane: `Delete` zmienia tylko przynależność w AMC. Dopiero `Shift+Delete` pyta o potwierdzenie i przenosi fizyczny plik do systemowego Kosza.

## Poprzedni zestaw regresyjny alpha 78

## Poprawki alpha 78

### AMC-078-01 — Pusta sesja lokalna

Uruchom AMC bez dodawania plików i naciśnij `Ctrl+1`. Następnie spróbuj F6 albo polecenia odtwarzania.

Oczekiwane: `Ctrl+1` wybiera „Pliki lokalne” i pustą listę, a nie sesję nieprzypisaną ani listę demonstracyjną. Polecenie wymagające pliku mówi „Brak elementów w sesji Pliki lokalne” i nie otwiera fikcyjnego odtwarzacza.

### AMC-078-02 — Wczytywanie folderu z innej sesji

Przejdź do TIDAL, Apple Music albo WiiM, naciśnij `Ctrl+Shift+O` i wybierz folder zawierający pliki audio, najlepiej także podfoldery.

Oczekiwane: po zatwierdzeniu wyboru program natychmiast przechodzi do „Pliki lokalne — Foldery” i mówi nazwę wczytywanego folderu. Podczas skanowania nie pokazuje demonstracyjnych utworów poprzedniej usługi. Po zakończeniu wyświetla prawdziwe podfoldery i pliki.

### AMC-078-03 — Pusty folder i trwałość źródła

Wybierz `Ctrl+Shift+O` pusty katalog, a potem zakończ i ponownie uruchom AMC.

Oczekiwane: program informuje, że nie znaleziono obsługiwanych plików, ale pozostaje w lokalnym widoku i nie wraca do danych demonstracyjnych. Źródło folderu i przypisanie `Ctrl+1` pozostają zapisane.

## Poprzedni zestaw regresyjny alpha 77

## Nowości alpha 77

Do testu Folderów najlepiej wybrać katalog zawierający co najmniej dwa pliki audio w katalogu głównym i dwa różne podfoldery. Zwykłe otwieranie folderu niczego fizycznie nie przenosi ani nie usuwa.

### AMC-077-01 — Domyślne miejsca sesji

Po pierwszym uruchomieniu tej wersji sprawdź `Ctrl+1`, `Ctrl+2`, `Ctrl+3` i `Ctrl+4`.

Oczekiwane w bieżącej wersji: `Ctrl+1` zawsze wybiera Pliki lokalne, także z pustą listą; dalej są WiiM, TIDAL i Apple Music. Program podaje numer, usługę oraz zapamiętany widok tej sesji.

### AMC-077-02 — Zmiana kolejności sesji

Otwórz `Ctrl+,`, kartę Ogólne i listę „Kolejność sesji i skrótów Ctrl+1–9”. Przesuń wybraną sesję `Alt+strzałka w górę` albo przyciskiem, zapisz ustawienia i sprawdź odpowiednie `Ctrl+cyfra`.

Oczekiwane: po każdym przesunięciu NVDA podaje nowy numer. Po zapisaniu skrót wybiera właściwą sesję, lista `Ctrl+0` ma tę samą kolejność, a `Ctrl+Page Up/Page Down` przechodzi według niej.

### AMC-077-03 — Trwałość kolejności

Zakończ AMC po zmianie kolejności i uruchom je ponownie.

Oczekiwane: własna kolejność oraz znaczenie `Ctrl+1–9` pozostają zachowane. Przycisk „Przywróć domyślną” odtwarza kolejność: Pliki lokalne, WiiM, TIDAL, Apple Music.

### AMC-077-04 — Połączona karta Komunikaty

W Ustawieniach odszukaj kartę Komunikaty i sekcję „Odczytywanie elementów list”. Zmień kolejność pól `Alt+strzałka w górę/w dół`, zapisz i sprawdź zwykłą listę.

Oczekiwane: nie ma osobnej, dublującej karty „Listy i odczyt”. Kolejność pól, podgląd, przyciski oraz wszystkie dotychczasowe ustawienia komunikatów są dostępne i działają. Paleta poleceń nadal potrafi ustawić fokus bezpośrednio na kolejności odczytu i na kolejności sesji.

### AMC-077-05 — Rejestracja źródła folderu

Naciśnij `Ctrl+Shift+O` i wybierz przygotowany katalog.

Oczekiwane: program niczego automatycznie nie odtwarza, rejestruje źródło i pokazuje widok Foldery. Na jednym poziomie podfoldery są przed plikami, a NVDA czyta każdy podfolder jako „folder”.

### AMC-077-06 — Nawigacja po poziomach

W widoku Foldery otwórz podfolder Enterem, uruchom plik Enterem, wróć Escape z odtwarzacza, a następnie użyj Backspace.

Oczekiwane: Enter na folderze schodzi dokładnie o poziom, Enter na pliku otwiera wspólny odtwarzacz, Escape wraca do tego samego poziomu i elementu, a Backspace wraca do folderu nadrzędnego. Na liście źródeł Backspace jedynie informuje, że wyżej przejść nie można.

### AMC-077-07 — Litery, filtr i wyszukiwanie

W podfolderze użyj kilku liter, następnie `Ctrl+K`; na końcu wykonaj `Ctrl+F` dla nazwy pliku znajdującego się w innym podfolderze.

Oczekiwane: litery i filtr dotyczą tylko widocznego poziomu. Wyszukiwanie obejmuje całą sesję Pliki lokalne i może znaleźć rekord spoza bieżącego folderu. Strzałka w lewo na pliku nadal podaje dostępne parametry techniczne.

### AMC-077-08 — Pamięć Folderów i Biblioteki

Pozostaw fokus wewnątrz podfolderu, zakończ AMC i uruchom ponownie. Następnie otwórz płaską Bibliotekę przez `Ctrl+L` i wróć do Folderów z menu Widok albo palety.

Oczekiwane: źródło oraz poziom folderu są pamiętane. Biblioteka nadal zawiera wszystkie zaimportowane pliki w płaskim zestawieniu, a powrót do Folderów nie tworzy duplikatów.

### AMC-077-09 — Regresja lokalnych działań

Na pliku dostępnym przez Foldery sprawdź odtwarzanie, zakładkę, `Ctrl+C`, `Ctrl+Shift+C`, Ulubione, Kolejkę i informacje `Alt+Enter`.

Oczekiwane: rekord jest tym samym elementem co w Bibliotece, więc odtwarzanie, pozycja, zakładki i stany przynależności są wspólne. Kopiowanie nazwy i fizycznego pliku działa jak dotychczas.

## Poprzedni zestaw regresyjny alpha 76

## Nowości alpha 76

Do tych prób użyj wyłącznie niepotrzebnych kopii plików, które można później odzyskać z systemowego Kosza.

### AMC-076-01 — Rezygnacja z potwierdzenia

Zaznacz testowy plik lokalny, naciśnij `Shift+Delete`, a w pytaniu wybierz „Nie”.

Oczekiwane: plik pozostaje na dysku i w AMC, a fokus wraca do listy.

### AMC-076-02 — Plik iCloud

Zaznacz niepotrzebną kopię pliku znajdującą się w iCloud Drive i potwierdź `Shift+Delete`.

Oczekiwane: plik trafia do systemowego Kosza i znika z katalogu AMC. Nie pojawia się okno „Nieoczekiwany błąd”, a aplikacja pozostaje uruchomiona.

### AMC-076-03 — Plik aktualnie odtwarzany

Uruchom testowy plik, wróć do listy, zaznacz go i wykonaj `Shift+Delete`.

Oczekiwane: AMC najpierw zamyka odtwarzanie i zwalnia plik, a następnie przenosi go do Kosza. Lista przechodzi do następnego dostępnego elementu i podaje wynik.

### AMC-076-04 — Kilka plików

Zaznacz Shiftem dwie lub trzy niepotrzebne kopie, także z iCloud Drive, i potwierdź `Shift+Delete`.

Oczekiwane: wszystkie przeniesione pliki znikają z AMC i można je znaleźć w Koszu. Program nie zawiesza się podczas odświeżania listy.

### AMC-076-05 — Kontrolowany błąd

Jeśli któryś plik jest zablokowany albo niedostępny, spróbuj go przenieść do Kosza.

Oczekiwane: AMC pozostaje uruchomiony, nie usuwa błędnego rekordu i podaje nazwę pliku, opis oraz kod `0x...`. Inne prawidłowo przetworzone elementy mogą zostać usunięte.

### AMC-076-06 — Delete pozostawia plik

Na osobnej kopii użyj zwykłego `Delete`.

Oczekiwane: znika tylko rekord AMC, natomiast fizyczny plik nadal istnieje w swoim folderze. `Backspace` nie usuwa rekordu i przechodzi do poziomu nadrzędnego.

## Poprzedni zestaw regresyjny alpha 75

## Nowości alpha 75

### AMC-075-01 — Lokalny wynik wyszukiwania

W sesji Pliki lokalne wykonaj `Ctrl+F`, wybierz znaleziony plik i naciśnij strzałkę w lewo.

Oczekiwane: NVDA podaje krótką informację o wyniku, zawierającą wszystkie dostępne dane, w tym format, czas, bitrate w kb/s, częstotliwość w kHz i rozmiar. Fokus pozostaje na tym samym wyniku, a okno wyszukiwania nie zamyka się.

### AMC-075-02 — Spójność ze zwykłą listą

Otwórz ten sam plik na zwykłej liście i ponownie naciśnij strzałkę w lewo.

Oczekiwane: zakres i kolejność informacji są takie same jak w wyszukiwaniu. Dopuszczalne jest uzupełnienie wcześniej nieznanego parametru po pierwszym odczytaniu metadanych.

### AMC-075-03 — Wyszukiwanie globalne

Użyj `Ctrl+Shift+F`, znajdź plik lokalny i naciśnij strzałkę w lewo.

Oczekiwane: szybka informacja działa również w globalnych wynikach i nie przełącza sesji ani widoku.

### AMC-075-04 — Sesja streamingowa

Na zwykłej liście i w wynikach wyszukiwania sesji demonstracyjnej naciśnij strzałkę w lewo.

Oczekiwane: AMC podaje te dane, które posiada, w tym bitrate i kHz, jeśli adapter je zwrócił. Nie szacuje nieznanych parametrów streamingu. Jeśli nie ma żadnych danych technicznych, mówi o ich braku.

### AMC-075-05 — Regresja schowka

Na lokalnym pliku sprawdź `Ctrl+C` w edytorze tekstowym oraz `Ctrl+Shift+C` przez wklejenie do folderu i do edytora.

Oczekiwane: pierwszy skrót kopiuje nazwę. Drugi przekazuje prawdziwy plik oraz pełną ścieżkę tekstową.

## Poprzedni zestaw regresyjny alpha 74

## Nowości alpha 74

### AMC-074-01 — Shift+Page na zwykłej liście

Na liście Multimedia, Biblioteka albo Ulubione naciśnij `Shift+Page Down`, a następnie `Shift+Page Up`.

Oczekiwane: jest to standardowe rozszerzone zaznaczanie większego zakresu listy. Widok nie zmienia się na Zakładki, nie pojawia się lista zakładek i tytuł okna nie zawiera słowa „Zakładki”.

### AMC-074-02 — Jawne otwarcie Zakładek

Zapamiętaj sesję, widok i zaznaczony element, a następnie naciśnij `Ctrl+B`.

Oczekiwane: dopiero ten skrót otwiera globalny widok Zakładek. NVDA podaje kontekst Zakładek i aktywnej sesji.

### AMC-074-03 — Dwustopniowy powrót

Na liście Zakładek otwórz wybraną zakładkę Enterem. W odtwarzaczu naciśnij Escape, a potem ponownie Escape.

Oczekiwane: pierwszy Escape wraca do tej samej zakładki na liście Zakładek. Drugi wraca do zapamiętanej sesji, poprzedniego widoku i możliwie tego samego zaznaczonego elementu.

### AMC-074-04 — Powrót między sesjami

Otwórz `Ctrl+B` z sesji Pliki lokalne, ale wybierz zakładkę należącą do innej sesji. Po jej otwarciu wróć dwukrotnie przez Escape.

Oczekiwane: pierwszy powrót prowadzi do rekordu Zakładek, drugi do pierwotnej sesji Pliki lokalne i miejsca sprzed `Ctrl+B`.

### AMC-074-05 — Escape i filtr Zakładek

W widoku Zakładek wpisz tekst do filtra. Naciśnij Escape dwa razy.

Oczekiwane: pierwszy Escape jedynie czyści filtr i pozostawia Zakładki otwarte. Drugi zamyka przejściowy widok i przywraca listę źródłową.

### AMC-074-06 — Nawigacja zakładek w odtwarzaczu

Otwórz materiał zawierający kilka zakładek i użyj `Shift+Page Up/Down` w odtwarzaczu.

Oczekiwane: skróty przechodzą wyłącznie po zakładkach tego materiału. Nie otwierają globalnej listy Zakładek.

### AMC-074-07 — Ponowne uruchomienie

Zakończ AMC, będąc na globalnej liście Zakładek, i uruchom program ponownie. Osobno wykonaj próbę, kończąc pracę w otwartym odtwarzaczu.

Oczekiwane: sama lista Zakładek nie jest po starcie przywracana bez ostrzeżenia — pojawia się podstawowa lista sesji. Odtwarzacz może zostać przywrócony; Escape prowadzi wtedy do zwykłej listy, a nie do niespodziewanego widoku Zakładek.

## Poprzedni zestaw regresyjny alpha 73

## Nowości alpha 73

### AMC-073-01 — Zaznaczanie wyników wyszukiwania

Wykonaj `Ctrl+F` albo `Ctrl+Shift+F`, a na wynikach użyj `Shift+strzałka w dół` kilka razy.

Oczekiwane: lista zachowuje się jak standardowa lista wielokrotnego wyboru i zaznacza kolejne wyniki. Nawigacja bez Shifta nadal wybiera pojedynczy wynik.

### AMC-073-02 — Zbiorcze kopiowanie nazw

Zaznacz kilka wyników i naciśnij `Ctrl+C`, następnie wklej do edytora tekstu.

Oczekiwane: każda nazwa znajduje się w osobnym wierszu i zachowana jest kolejność widoczna na liście.

### AMC-073-03 — Zbiorcze kopiowanie plików

W wyszukiwaniu sesji Pliki lokalne zaznacz kilka wyników, naciśnij `Ctrl+Shift+C` i wklej w pustym folderze testowym.

Oczekiwane: system wkleja wszystkie zaznaczone prawdziwe pliki. W schowku tekstowym dostępne są również ich pełne ścieżki.

### AMC-073-04 — Wyniki z różnych usług

Wyszukaj we wszystkich usługach, zaznacz wyniki lokalne i usługowe, po czym użyj `Ctrl+Shift+C` i wklej tekst do edytora.

Oczekiwane: lokalne pozycje mają pełne ścieżki, a usługowe — łącza. Jeśli wklejasz do folderu, system przekazuje tylko rzeczywiste pliki lokalne.

### AMC-073-05 — Blokada wycinania w wyszukiwaniu

Na wynikach naciśnij `Ctrl+X`, a potem `Ctrl+V`.

Oczekiwane: program jednoznacznie informuje, że wycinanie i wklejanie nie działają na liście wyników. W polu wyszukiwania `Ctrl+X`, `Ctrl+C` i `Ctrl+V` nadal standardowo edytują tekst.

### AMC-073-06 — Wklejanie do lokalnej biblioteki

Skopiuj w Eksploratorze jeden lub kilka obsługiwanych plików audio. W AMC przejdź do sesji Pliki lokalne, do widoku Multimedia lub Biblioteka, i naciśnij `Ctrl+V`.

Oczekiwane: pliki pojawiają się w AMC i są zaznaczone. Fizycznie pozostają w swoich dotychczasowych folderach. Ponowne wklejenie nie tworzy duplikatów.

### AMC-073-07 — Wklejanie do Kolejki i Ulubionych

Skopiuj plik spoza katalogu AMC. Wklej go najpierw w widoku Kolejka, a inny w widoku Ulubione.

Oczekiwane: pierwszy zostaje dodany do katalogu i kolejki, drugi do katalogu i ulubionych. Oba są dostępne również w lokalnych Multimediach.

### AMC-073-08 — Niedozwolone cele

Spróbuj wkleić plik w Historii odtwarzania, Zakładkach, odtwarzaczu i sesji demonstracyjnej.

Oczekiwane: żaden plik nie jest dodawany, a komunikat wskazuje dozwolone lokalne widoki.

### AMC-073-09 — Wytnij, a następnie wklej wewnątrz AMC

Na zwykłej lokalnej liście naciśnij `Ctrl+X`, następnie przejdź do Kolejki lub Ulubionych i naciśnij `Ctrl+V`. Potem spróbuj wkleić ten sam schowek do folderu w Eksploratorze.

Oczekiwane: AMC dodaje przynależność, ale nie przenosi pliku. Późniejsze wklejenie w Eksploratorze działa jak kopiowanie, a nie przenoszenie źródła.

## Poprzedni zestaw regresyjny alpha 72

## Nowości alpha 72

### AMC-072-01 — Globalna kolejność informacji

Otwórz `Ctrl+B` i przejdź po kilku zakładkach należących do jednego oraz do różnych plików.

Oczekiwane: każdy wiersz zaczyna się od nazwy pliku lub materiału, następnie podaje datę utworzenia zakładki, pozycję w materiale, opcjonalną nazwę, sesję i słowo „zakładka”. Kilka zakładek tego samego pliku pozostaje ułożonych według pozycji w nagraniu, a nie według daty utworzenia.

### AMC-072-02 — Krótka nawigacja w odtwarzaczu

W otwartym materiale użyj `Shift+Page Up/Down` najpierw dla zwykłej, a potem nazwanej zakładki.

Oczekiwane: zwykła zakładka podaje tylko czas. Nazwana podaje nazwę i czas. Nie powtarza tytułu pliku ani daty utworzenia.

### AMC-072-03 — Kopiowanie jednej zakładki

Na liście `Ctrl+B` wybierz zakładkę i naciśnij `Ctrl+C`, po czym wklej zawartość do edytora tekstu.

Oczekiwane: skopiowany jest pełny opis widocznego wiersza — razem z datą i czasem — a nie sam tytuł pliku źródłowego.

### AMC-072-04 — Kopiowanie kilku zakładek

Zaznacz kilka sąsiednich zakładek przez `Shift+strzałka`, naciśnij `Ctrl+C` i wklej wynik.

Oczekiwane: każda zaznaczona zakładka znajduje się w osobnym wierszu, w tej samej kolejności co na liście.

### AMC-072-05 — Powrót i fokus

Z listy Zakładek otwórz rekord Enterem, a następnie wróć przez Escape.

Oczekiwane: fokus wraca do tego samego rekordu i strzałki od razu działają. Nie pojawia się dodatkowy widok ani filtr.

### AMC-072-06 — Dwa sposoby kopiowania w wyszukiwaniu

W `Ctrl+F` i `Ctrl+Shift+F` wybierz lokalny wynik. Naciśnij `Ctrl+C`, wklej do edytora, następnie naciśnij `Ctrl+Shift+C` i wklej w folderze testowym albo menedżerze plików.

Oczekiwane: `Ctrl+C` kopiuje samą nazwę. `Ctrl+Shift+C` kopiuje prawdziwy plik oraz pełną ścieżkę. Okno wyszukiwania pozostaje otwarte, a fokus wraca na ten sam wynik. Dla wyniku usługowego `Ctrl+Shift+C` kopiuje łącze, nie fikcyjny plik.

### AMC-072-07 — Wycinanie lokalnego pliku

Na dowolnej zwykłej liście sesji Pliki lokalne wybierz niepotrzebny plik testowy, naciśnij `Ctrl+X`, przejdź do pustego folderu i naciśnij tam `Ctrl+V`.

Oczekiwane: samo `Ctrl+X` niczego nie usuwa. `Ctrl+V` przenosi prawdziwy plik. Po powrocie do AMC program wykrywa brak starej ścieżki, usuwa nieaktualny wpis i podaje jednoznaczny komunikat.

### AMC-072-08 — Anulowane wycinanie

Na lokalnej liście naciśnij `Ctrl+X`, ale nie wklejaj pliku. Wróć do AMC i dalej nawiguj.

Oczekiwane: plik pozostaje na dysku i na liście. `Ctrl+X` bez późniejszego `Ctrl+V` nie powoduje utraty danych.

### AMC-072-09 — Wycinanie z wyszukiwania

Wyszukaj lokalny plik przez `Ctrl+F` albo `Ctrl+Shift+F`, naciśnij `Ctrl+X`, wklej go do folderu testowego, wróć do wyszukiwania i zamknij je Escape.

Oczekiwane: plik jest przeniesiony przez system. Po zamknięciu wyszukiwania AMC porządkuje swój katalog. `Ctrl+X` dla wyniku TIDAL-a, Apple Music lub WiiM nie tworzy fikcyjnego pliku i zgłasza, że funkcja dotyczy tylko plików lokalnych.

## Poprzedni zestaw regresyjny alpha 71

## Nowości alpha 71

### AMC-071-01 — Kolejność niezależna od dodawania

W jednym pliku dodaj zakładki nie po kolei, na przykład najpierw w `10:00`, potem w `2:00`, a na końcu w `6:00`. Otwórz `Ctrl+B`.

Oczekiwane: zakładki tego pliku występują jako `2:00`, `6:00`, `10:00`, niezależnie od kolejności ich utworzenia.

### AMC-071-02 — Kilka materiałów i sesji

Utwórz zakładki w dwóch plikach albo sesjach, wróć do jednego z nich i otwórz `Ctrl+B`.

Oczekiwane: bieżący materiał znajduje się pierwszy i ma rosnący czas. Pozostałe zakładki są pogrupowane według sesji i tytułu; czasy różnych plików nie są przemieszane w jedną wspólną oś.

### AMC-071-03 — Enter z listy do odtwarzacza

Na liście `Ctrl+B` wybierz zakładkę i naciśnij Enter. Od razu użyj NVDA+strzałka w górę albo Spacji.

Oczekiwane: fokus jest na głównym przycisku odtwarzacza. NVDA czyta obiekt odtwarzacza, a Spacja wstrzymuje lub wznawia; nic nie pozostaje zablokowane na ukrytej liście.

### AMC-071-04 — Escape do tej samej zakładki

Po otwarciu zakładki Enterem naciśnij Escape, a następnie strzałkę w dół i w górę.

Oczekiwane: fokus wraca do tej samej zakładki na liście `Ctrl+B`, a obie strzałki natychmiast czytają sąsiednie rekordy.

### AMC-071-05 — Krótka regresja nazw

Dodaj szybką zakładkę przez `B`, nazwaną przez `Ctrl+Shift+B`, przejdź po nich `Shift+Page Up/Down` i sprawdź wyciszenie komunikatów.

Oczekiwane: funkcje alpha.69–70 działają bez zmian, nazwy są trwałe i nie powstają duplikaty w tej samej sekundzie.

## Poprzedni zestaw regresyjny alpha 70

## Nowości alpha 70

### AMC-070-01 — Dodanie nazwanej zakładki

Otwórz plik w odtwarzaczu, przejdź do wybranego miejsca i naciśnij `Ctrl+Shift+B`. Wpisz nazwę, na przykład „Początek rozmowy”, i zatwierdź Enterem.

Oczekiwane: fokus trafia bezpośrednio do pola „Nazwa zakładki”. Enter zapisuje, Escape anuluje, a po zatwierdzeniu AMC podaje nazwę i czas bez zatrzymywania odtwarzania.

### AMC-070-02 — Nazwanie istniejącej szybkiej zakładki

Dodaj szybką zakładkę klawiszem `B`. Bez przewijania naciśnij `Ctrl+Shift+B`, wpisz nazwę i zatwierdź.

Oczekiwane: istniejąca zakładka otrzymuje nazwę. Na liście nie pojawia się drugi wpis z tym samym czasem.

### AMC-070-03 — Globalna lista nazw

Dodaj kilka nazwanych i kilka szybkich zakładek, po czym otwórz `Ctrl+B`. Nawiguj strzałkami, użyj pierwszej litery nazwy i filtra `Ctrl+K`.

Oczekiwane: nazwana pozycja jest czytana w kolejności: nazwa zakładki, tytuł materiału, czas, sesja, zakładka. Wpis bez nazwy zachowuje krótszy dotychczasowy format. Nazwa działa w nawigacji literowej i filtrze.

### AMC-070-04 — Nawigacja i wyciszenie

Przejdź po nazwanych zakładkach przez `Shift+Page Up/Down`, potem wyłącz „Oznajmiaj nawigację po zakładkach” i powtórz test.

Oczekiwane: przy włączonej opcji AMC podaje nazwę oraz czas osiągniętej zakładki. Przy wyłączonej przechodzi prawidłowo, lecz bez automatycznego komunikatu. `Ctrl+Shift+E` nadal podaje czas na żądanie.

### AMC-070-05 — Trwałość i pełna kopia

Zamknij i uruchom AMC ponownie, sprawdź nazwy przez `Ctrl+B`. Opcjonalnie wykonaj pełny eksport i import na danych testowych.

Oczekiwane: nazwy, czasy i powiązania z materiałami pozostają zachowane. Eksport samych ustawień nadal nie zawiera zakładek.

## Poprzedni zestaw regresyjny alpha 69

## Nowości alpha 69

### AMC-069-01 — Kilka poprzednich zakładek podczas odtwarzania

W jednym dłuższym pliku utwórz co najmniej cztery zakładki. Odtwarzaj plik za ostatnią z nich i kilka razy dość szybko naciśnij `Shift+Page Up`.

Oczekiwane: każde naciśnięcie przechodzi do wcześniejszej zakładki. Odtwarzanie nie powoduje ponownego wyboru tej samej pozycji i nie następuje przejście do innego pliku.

### AMC-069-02 — Kilka następnych zakładek

Po dojściu do pierwszej zakładki kilka razy naciśnij `Shift+Page Down`.

Oczekiwane: każde naciśnięcie przechodzi do następnej zakładki w prawidłowej kolejności. Za ostatnią słychać komunikat o braku następnej zakładki.

### AMC-069-03 — Powrót do rzeczywistej pozycji

Przejdź do zakładki, następnie użyj zwykłego przewijania, skoku cyfrą albo `Ctrl+J`, po czym ponownie naciśnij `Shift+Page Up` lub `Shift+Page Down`.

Oczekiwane: po innym poleceniu AMC wybiera zakładkę względem nowej, rzeczywistej pozycji, a nie względem starej sekwencji.

### AMC-069-04 — Cicha nawigacja

Otwórz Ustawienia → Komunikaty i wyłącz „Oznajmiaj nawigację po zakładkach”. Wróć do odtwarzacza i użyj `Shift+Page Up/Down`.

Oczekiwane: po udanym skoku nie pojawia się automatyczny komunikat „Zakładka” z czasem, ale pozycja naprawdę się zmienia. `Ctrl+Shift+E` nadal odczytuje czas na żądanie. Na krańcu pozostaje komunikat o braku dalszej zakładki.

### AMC-069-05 — Paleta i trwałość ustawienia

W palecie `Ctrl+Shift+K` wyszukaj „komunikaty nawigacji po zakładkach”, naciśnij Enter i sprawdź, czy fokus trafia na właściwy checkbox. Zapisz ustawienia i ponownie uruchom AMC.

Oczekiwane: paleta podaje bieżący stan opcji, ustawienie jest zachowane po restarcie, a `B` nadal dodaje zakładkę i `Ctrl+B` otwiera ich listę.

## Poprzedni zestaw regresyjny alpha 68

## Nowości alpha 68

### AMC-068-01 — Dodawanie szybkich zakładek

Otwórz dłuższy plik w odtwarzaczu. Przejdź mniej więcej do 2 minut i naciśnij `B`, potem przejdź do innego miejsca i ponownie naciśnij `B`.

Oczekiwane: program krótko mówi „Dodano zakładkę” oraz czas. Nie otwiera się żadne dodatkowe okno, odtwarzanie i fokus pozostają w odtwarzaczu.

### AMC-068-02 — Duplikat w tym samym miejscu

Bez zmiany pozycji naciśnij `B` ponownie.

Oczekiwane: program mówi, że zakładka już istnieje; lista nie otrzymuje drugiego wpisu w tej samej sekundzie.

### AMC-068-03 — Nawigacja wewnątrz jednego materiału

W tym samym pliku użyj kilka razy `Shift+Page Up` i `Shift+Page Down`, także przed pierwszą i za ostatnią zakładką.

Oczekiwane: skróty ustawiają dokładne zapisane miejsca i czytają czas. Nie otwierają innego pliku. Na krańcach pojawia się jednoznaczny komunikat o braku poprzedniej albo następnej zakładki.

### AMC-068-04 — Globalna lista Ctrl+B

Dodaj zakładki w co najmniej dwóch sesjach, następnie naciśnij `Ctrl+B`. Nawiguj strzałkami, wpisz początkową literę tytułu i sprawdź filtr `Ctrl+K`.

Oczekiwane: zwykła dostępna lista zawiera zakładki ze wszystkich sesji. Każdy wiersz podaje kolejno tytuł, czas, usługę i słowo „zakładka”. Nawigacja literowa oraz filtr działają jak na innych listach.

### AMC-068-05 — Otwarcie zakładki z innej sesji

Na globalnej liście wybierz zakładkę należącą do innej sesji i naciśnij Enter. Potem naciśnij `Escape`.

Oczekiwane: AMC przełącza właściwą sesję, otwiera materiał i ustawia zapisany czas. `Escape` wraca do globalnej listy na tej samej zakładce.

### AMC-068-06 — Bezpieczne usuwanie

Na liście Zakładek naciśnij najpierw `Shift+Delete`, a następnie zwykły `Delete`. Sprawdź, czy plik nadal istnieje na dysku.

Oczekiwane: `Shift+Delete` wyjaśnia, że z tego widoku nie usuwa pliku. `Delete` usuwa tylko wybraną zakładkę i ustawia fokus na sąsiednim wpisie. Źródłowy plik pozostaje bez zmian.

### AMC-068-07 — Zapis po ponownym uruchomieniu

Pozostaw kilka zakładek, zamknij AMC przez `Alt+F4`, uruchom ponownie i naciśnij `Ctrl+B`.

Oczekiwane: wszystkie pozostawione zakładki, ich czasy i sesje są zachowane. Program nie rozpoczyna odtwarzania samoczynnie.

### AMC-068-08 — B na zwykłej liście i krótka regresja

Wróć do zwykłej listy mediów i naciśnij `B`. Sprawdź też lewą strzałkę, wznowienie pozycji, `Page Up/Down`, `Alt+góra/dół`, OGG i `Delete` w odtwarzaczu.

Oczekiwane: na zwykłej liście `B` nadal przechodzi do tytułu zaczynającego się na B, zamiast tworzyć zakładkę. Pozostałe potwierdzone funkcje nie zmieniają się. `Ctrl+Shift+B` jest na razie celowo wolne i zarezerwowane dla zakładki nazwanej.

## Poprzedni zestaw regresyjny alpha 67

## Nowości alpha 67

### AMC-067-01 — Prawa strzałka i fokus listy

Na lokalnym pliku naciśnij prawą strzałkę, potem strzałkę w dół i w górę.

Oczekiwane: nie otwiera się żadne okno systemowe, fokus pozostaje na liście, a NVDA nadal czyta kolejne elementy.

### AMC-067-02 — Menu lokalnego pliku

Otwórz menu kontekstowe na liście i w odtwarzaczu.

Oczekiwane: nie ma pozycji „Otwórz w…”. Pozostaje „Otwórz w domyślnej aplikacji” oraz właściwe działania AMC.

### AMC-067-03 — Regresja

Sprawdź lewą strzałkę, wznowienie pozycji po restarcie, `Delete` w odtwarzaczu i `Shift+Delete` na liście.

Oczekiwane: pozostałe funkcje działają bez zmian.

## Poprzedni zestaw regresyjny alpha 66

## Nowości alpha 66

### AMC-066-01 — Osobny proces z menu

Na pliku lokalnym otwórz menu kontekstowe i wybierz „Otwórz w…”.

Oczekiwane: systemowy wybór aplikacji staje się aktywnym oknem i NVDA odczytuje jego kontrolki. Po `Escape` fokus wraca do AMC.

### AMC-066-02 — Osobny proces prawą strzałką

Na tym samym pliku naciśnij prawą strzałkę.

Oczekiwane: działanie i fokus są takie same jak z menu. Jeżeli ponownie wystąpi cisza lub konieczność użycia `Alt+Tab`, uznajemy mechanizm za niezgodny i usuwamy funkcję.

### AMC-066-03 — Regresja

Sprawdź lewą strzałkę, wznowienie pozycji po restarcie, `Delete` w odtwarzaczu oraz `Shift+Delete` na liście.

Oczekiwane: pozostałe funkcje nie zmieniają się.

## Poprzedni zestaw regresyjny alpha 65

## Nowości alpha 65

### AMC-065-01 — Otwórz w przez menu

Na pliku lokalnym otwórz menu kontekstowe, wybierz „Otwórz w…” i odczekaj chwilę.

Oczekiwane: systemowy wybór aplikacji otrzymuje fokus, NVDA odczytuje jego kontrolki i można wskazać program albo anulować. Po zamknięciu fokus wraca do tego samego pliku w AMC.

### AMC-065-02 — Otwórz w prawą strzałką

Na tym samym pliku naciśnij prawą strzałkę i odczekaj chwilę.

Oczekiwane: wynik jest taki sam jak z menu. Jeżeli działa menu, ale nie strzałka, zapisz dokładnie tę różnicę — wtedy zachowamy funkcję tylko w menu.

### AMC-065-03 — Anulowanie i regresja

Anuluj systemowe okno przez `Escape`, sprawdź nawigację listy, lewą strzałkę, pamiętanie pozycji i `Delete` w odtwarzaczu.

Oczekiwane: `Escape` nie zamyka AMC, fokus wraca na listę, a potwierdzone funkcje `alpha.63–64` pozostają bez zmian.

## Poprzedni zestaw regresyjny alpha 64

## Nowości alpha 64

### AMC-064-01 — Prawa strzałka nie gubi fokusu

Na pliku lokalnym naciśnij prawą strzałkę, potem strzałkę w dół i w górę.

Oczekiwane: nie pojawia się systemowe okno ani cisza spowodowana utratą fokusu. NVDA pozostaje na liście, a zwykła nawigacja nadal czyta elementy.

### AMC-064-02 — Menu pliku lokalnego

Otwórz menu kontekstowe pliku na liście i w odtwarzaczu.

Oczekiwane: nie ma pozycji „Otwórz w…”. „Otwórz w domyślnej aplikacji” pozostaje dostępne. `Delete` w odtwarzaczu nadal usuwa tylko wpis AMC, a `Shift+Delete` jest dostępne wyłącznie na liście.

### AMC-064-03 — Krótka regresja alpha 63

Sprawdź wznowienie pozycji po restarcie, lewą strzałkę z `kb/s`, `Delete` w odtwarzaczu i `Ctrl+Z`.

Oczekiwane: wszystkie potwierdzone funkcje `alpha.63` działają bez zmian.

## Poprzedni zestaw regresyjny alpha 63

## Nowości alpha 63

### AMC-063-01 — Pozycja ostatniego pliku po ponownym uruchomieniu

Odtwórz dłuższy plik, przejdź co najmniej minutę od początku, wstrzymaj i zamknij AMC przez `Alt+F4`. Uruchom ponownie, użyj `F6`, a następnie `Ctrl+Shift+E`. Wznów odtwarzanie.

Oczekiwane: AMC nie uruchamia dźwięku samoczynnie, ale od razu pokazuje i podaje zapisaną pozycję. Pierwsze wznowienie zaczyna się z tego miejsca, a nie od `0:00`.

### AMC-063-02 — Prawa strzałka bez skojarzenia pliku — historyczne, wycofane w alpha 64

Na lokalnym pliku naciśnij prawą strzałkę. Najlepiej sprawdzić także rozszerzenie, dla którego Windows nie ma poprawnej aplikacji domyślnej.

Ręczny test wykazał utratę czytelnego fokusu NVDA. Aktualne wymaganie opisuje `AMC-064-01`.

### AMC-063-03 — Delete w lokalnym odtwarzaczu

Otwórz kopię pliku testowego w odtwarzaczu i naciśnij `Delete`. Sprawdź dysk, wypowiedź NVDA i następny element. Następnie naciśnij `Ctrl+Z`.

Oczekiwane: plik pozostaje na dysku. AMC usuwa tylko swój wpis, wstrzymuje usuwany element, podaje następny element albo sesję, a `Ctrl+Z` przywraca wpis.

### AMC-063-04 — Fizyczne usuwanie tylko na liście

W odtwarzaczu naciśnij `Shift+Delete`, a potem otwórz jego menu kontekstowe. Następnie wróć na listę i użyj `Shift+Delete` na kopii pliku testowego.

Oczekiwane: w odtwarzaczu skrót niczego fizycznie nie usuwa i nie ma pozycji przenoszenia do Kosza. Na liście pozostaje dotychczasowe pytanie potwierdzające i systemowy Kosz.

### AMC-063-05 — Krótka regresja

Sprawdź lewą strzałkę z `kb/s`, `Alt+F4` z odtwarzacza, `Ctrl+Shift+E/R/T`, OGG i zmianę prędkości.

Oczekiwane: zachowanie `alpha.62` pozostaje bez zmian.

## Poprzedni zestaw regresyjny alpha 62

## Nowości alpha 62

### AMC-062-01 — Bitrate pliku wcześniej odtwarzanego

Na liście zaznacz odtwarzany wcześniej plik i naciśnij lewą strzałkę.

Oczekiwane: komunikat zawiera rozszerzenie, czas, wartość `kb/s`, dostępne `kHz` i rozmiar.

### AMC-062-02 — Bitrate pliku jeszcze nieodtwarzanego

Dodaj nowy plik, nie uruchamiaj go i od razu naciśnij lewą strzałkę. Powtórz ją drugi raz.

Oczekiwane: AMC odczytuje metadane tylko tego pliku i podaje średni bitrate w `kb/s`. Druga próba korzysta z zapisanego wyniku. Nie rozpoczyna się odtwarzanie ani skanowanie całej listy.

### AMC-062-03 — Alt+F4 z odtwarzacza

Otwórz odtwarzacz przez `F6` i naciśnij `Alt+F4`.

Oczekiwane: cała aplikacja zamyka się od razu i zapisuje stan; nie następuje najpierw powrót do listy. Po ponownym uruchomieniu `Escape` i `Shift+F6` nadal wracają tylko do listy.

## Poprzedni zestaw regresyjny alpha 61

## Nowości alpha 61

### AMC-061-01 — Prawa strzałka i systemowe Otwórz w

Na pliku lokalnym naciśnij prawą strzałkę w głównej liście, Bibliotece i Historii odtwarzania.

Oczekiwane: bez otwierania menu AMC pojawia się bezpośrednio systemowy wybór aplikacji. Można jednorazowo otworzyć plik np. w foobar2000; ewentualna opcja zmiany aplikacji domyślnej zależy od wersji Windows.

### AMC-061-02 — Shift+Delete w odtwarzaczu — historyczne, wycofane w alpha 63

Odtwórz kopię pliku testowego, naciśnij `Shift+Delete`, najpierw wybierz Nie, a przy drugiej próbie Tak.

To zachowanie było testowane w `alpha.61–62`, lecz zostało świadomie wycofane. Aktualne wymaganie opisuje `AMC-063-04`.

### AMC-061-03 — Usunięty plik a Historia odtwarzania

Odtwórz plik, usuń go przez `Shift+Delete`, otwórz `Ctrl+H`, uruchom AMC ponownie i sprawdź historię ponownie.

Oczekiwane: usunięty element nie jest widoczny i nie wraca po restarcie. `Alt+góra/dół` również go pomija.

### AMC-061-04 — Krótka regresja

Sprawdź lewą strzałkę, `Ctrl+Shift+E/R/T`, zwykły `Delete` z `Ctrl+Z`, kopiowanie i naturalne przejście do następnego pliku.

Oczekiwane: zachowanie alpha 60 pozostaje bez zmian.

## Poprzedni zestaw regresyjny alpha 60

## AMC-060-01 — Start, ostatnio odtwarzany i F6

Uruchom AMC po wcześniejszym odtworzeniu pliku, lecz bez jego ponownego włączania. Sprawdź listę i `F6`.

Oczekiwane:

- program nie zaczyna sam odtwarzać i nie przesuwa zaznaczenia;
- zapisany element ma początek „Wstrzymany” albo „Ostatnio odtwarzany”;
- `F6` otwiera ten element w odtwarzaczu.

## AMC-060-02 — Jawne pytania o czas

Sprawdź `Ctrl+Shift+E`, `Ctrl+Shift+R` i `Ctrl+Shift+T` najpierw na liście, potem po `F6`.

Oczekiwane:

- w obu miejscach słychać odpowiednio czas od początku, pozostały i całkowity;
- działanie nie zależy od wyciszenia automatycznych komunikatów transportu.

## AMC-060-03 — Dwie niezależne historie

Na liście otwórz kolejno Bibliotekę, Kolejkę i Ulubione, po czym użyj `Alt+lewo` oraz `Alt+prawo`. Następnie odtwórz trzy różne pliki A, B i C, otwórz odtwarzacz i użyj `Alt+dół`, `Alt+dół`, `Alt+góra`.

Oczekiwane:

- boczny Alt cofa i ponawia widoki bieżącej sesji;
- w odtwarzaczu `Alt+dół` wybiera B, potem A, a `Alt+góra` wraca do B;
- każdy plik zachowuje własną pozycję;
- po ponownym uruchomieniu historia odtwarzania pozostaje, natomiast stos Wstecz/Naprzód może być pusty.

## AMC-060-04 — Widok Historia i lista źródłowa

Naciśnij `Ctrl+H`, sprawdź kolejność wpisów, następnie wróć do odtwarzacza i użyj `Page Up/Down`.

Oczekiwane:

- Historia jest uporządkowana od najnowszego wpisu i nie zawiera powtórzeń;
- `Page Up/Down` wybiera sąsiada odtwarzanego pliku na liście źródłowej, nie sąsiada z historii.

## AMC-060-05 — Strzałki lokalne we wszystkich widokach

Na pliku lokalnym użyj lewej i prawej strzałki w głównym katalogu, Bibliotece, Kolejce i Ulubionych.

Oczekiwane:

- lewa podaje krótkie informacje, a prawa otwiera menu działań i ustawia w nim fokus w każdym z lokalnych widoków;
- w sesjach nielokalnych strzałki zachowują zwykłą semantykę listy.

## AMC-060-06 — Delete i Shift+Delete

Na kopiach testowych plików sprawdź zwykły `Delete`, `Ctrl+Z`, a następnie `Shift+Delete`: najpierw odpowiedź Nie, potem Tak. Powtórz z wielokrotnym zaznaczeniem i z aktualnie otwartym plikiem.

Oczekiwane:

- `Delete` usuwa wpis z katalogu AMC i daje się cofnąć bez dotykania dysku;
- `Shift+Delete` wymaga potwierdzenia, po odpowiedzi Nie niczego nie zmienia, a po Tak zwalnia plik, przenosi udane pliki do systemowego Kosza i usuwa je z AMC;
- `Ctrl+Z` nie przywraca fizycznie usuniętych plików.

## AMC-060-07 — Kopiowanie wielokrotnego zaznaczenia

Zaznacz kilka plików. Wklej wynik `Ctrl+C` do edytora, a wynik `Ctrl+Shift+C` do edytora i do pustego folderu w Total Commanderze lub Eksploratorze.

Oczekiwane:

- `Ctrl+C` daje wszystkie nazwy, po jednej w wierszu;
- `Ctrl+Shift+C` daje pełne ścieżki w tekście i pozwala wkleić wszystkie fizyczne pliki.

## AMC-060-08 — Przełączanie sesji i regresja

Przejdź w każdej sesji do innego widoku, po czym przełączaj ją przez `Ctrl+1–9`. Sprawdź wyrywkowo OGG, prędkość, pasek, `Alt+Enter`, Kolejkę i naturalne przejście do następnego pliku.

Oczekiwane: komunikat zaczyna się od numeru i nazwy sesji, potem podaje przywrócony widok i element, np. „4, Pliki lokalne, Biblioteka…”. Pozostałe funkcje nie mają regresji.
