# MENU-001 — menu kontekstowe i skróty zależne od sesji

Status: **W trakcie; korekta natywnej granicy `Ctrl+Shift+0` jest przetestowana
w źródłach, ale jeszcze niezainstalowana**.

## Korekta użytkownika z 25.11.2026 — przegląd wszystkich sesji

Użytkownik nadal słyszy cyfry na początku niektórych pozycji menu kontekstowego. Poprzednie testy etykiet nie zamykają tego zgłoszenia; trzeba odczytać faktyczne menu z NVDA w każdej sesji i ustalić, czy cyfra pochodzi z natywnego klawisza dostępu, etykiety menu, drugiej kolumny czy innej warstwy. Nie zakładać, że jest to identyfikator wewnętrzny bez pomiaru. NVDA ma otrzymać tylko nazwę czynności i ewentualnie jeden prawdziwy skrót.

Kolejność menu listy powinna zaczynać się od działania na wybranym materiale: „Odtwarzaj”, potem dostępne dodanie jako następne, do Ulubionych lub playlisty. Dalej powinny być „Kopiuj”, „Kopiuj ścieżkę/adres” i pozostałe właściwe operacje. Obecny kod buduje grupy z działań menu głównego, ale ma informacje przed Ulubionymi i playlistą oraz inne pozycje między nimi; wymaga to przeglądu semantycznego z każdą sesją. Nie pokazywać martwych poleceń.

Test odbiorczy: otwarcie `Shift+F10` i klawiszem menu na reprezentatywnym wierszu w Plikach, Radiu, Podcastach, TIDAL, Spotify, Apple Music, Radio Archiwiście, HEOS i obsługiwanych urządzeniach; początkowy fokus, strzałki, pierwszy znak odczytanej nazwy, Enter, Escape, ponowne otwarcie i powrót fokusu. Porównać nazwę, skrót, dostępność i wykonanie z `Ctrl+F1`, pełną pomocą i `Ctrl+Shift+K`. To nowe zgłoszenie jest otwarte; nie ma jeszcze poprawki, testów NVDA ani wydania.

## Materiał do dokumentacji użytkownika

`Shift+F10` albo klawisz menu otwiera menu bieżącej listy lub odtwarzacza.
Każda pozycja ma najpierw pełną nazwę czynności, a następnie w nawiasie jej
rzeczywisty skrót, na przykład „Szybkie informacje o elemencie (Alt+T)” albo
„Szukaj w bieżącej usłudze (Ctrl+F)”. Menu kontekstowe nie odczytuje osobno
technicznych klawiszy dostępu takich jak `0`, `j`, `t` lub `b`.

`F2` jest opisane według zaznaczonego elementu: „Zmień nazwę stacji”, „Zmień
nazwę playlisty”, „Zmień nazwę podcastu lub kanału” albo „Zmień nazwę pliku w
Bibliotece”. `Shift+F2` pozostaje osobnym poleceniem „Zmień nazwę pliku na
dysku”. Dla strumieni WiiM i własnych stacji Sonos nazwa uczciwie mówi, że
okno zmienia nazwę lub adres. `F2` na lokalnej playliście i playliście
podcastów rzeczywiście zmienia jej nazwę oraz zachowuje elementy.

Menu pokazuje tylko polecenia dozwolone przez bieżącą sesję, widok, rodzaj
wiersza i prawdziwy stan odtwarzania lub nagrywania. Korzysta z tych samych
akcji i bramek, co skróty, `Ctrl+F1`, pełna pomoc i paleta `Ctrl+Shift+K`.

W Radiu `Alt+D` nazywa się „Informacje o bieżącym odtwarzaniu”. Polecenie niczego
nie otwiera i nigdzie nie przechodzi: odczytuje nazwę stacji, parametry
strumienia oraz dostępne metadane audycji albo utworu. Ta sama nazwa występuje
w menu, pomocy `Ctrl+F1`, pełnej pomocy i palecie `Ctrl+Shift+K`.

Polecenie `Ctrl+Shift+Enter` nazywa się krótko „Odtwarzaj jako następne”, bez
mylącego dopisku „lub usuń”. `Ctrl+Shift+C` ma nazwę zależną od elementu:
na nagraniu lub innym lokalnym pliku menu mówi „Skopiuj ścieżkę do pliku”, a
w ogólnych powierzchniach „Skopiuj adres lub ścieżkę”.

Zakładki, nazwane zakładki, rozdziały i historia nadal mają rozdzielone
znaczenia z AMC04. `Alt+lewo/prawo` przechodzi po historii widoków.
`Alt+góra/dół` w odtwarzaczu wybiera starszy lub nowszy odtwarzany element,
natomiast na liście z ręczną kolejnością przenosi element. Menu nie reklamuje
tych poleceń w sesji lub widoku, który nie ma wymaganej tożsamości albo osi
czasu.

Ta sama zasada obowiązuje przy następnych funkcjach i w innych dużych
aplikacjach: klawisz dostępu menu nie jest drugim skrótem polecenia, a NVDA nie
może dostać technicznej cyfry, przypadkowej litery, identyfikatora polecenia ani
reprezentacji obiektu. Trwały kontrakt zapisano w instrukcjach repozytorium oraz
w osobistej umiejętności Codexa `accessible-desktop-contracts`.

`Ctrl+Shift+0` uruchamia preset 10 we wszystkich sesjach i w odtwarzaczu.
Windows może przekazać fizyczne zero dwiema natywnymi drogami; AMC utrzymuje
obie tylko wtedy, gdy jego okno jest aktywne, a jeden gest wykonuje preset
dokładnie raz. Skrót nie działa w polu filtrowania ani w trybie pomocy
klawiatury. Nazwa i dostępność w `Ctrl+F1`, pełnej pomocy, menu i palecie
`Ctrl+Shift+K` pozostają bez zmian, ponieważ regresja dotyczyła wyłącznie
dostarczenia istniejącego polecenia z natywnej listy Windows.

Klawisze funkcyjne korzystające z edycji profilu, w tym `F2`, otrzymują od
hosta pełne ścieżki Biblioteki, Podcastów i ustawień także na świeżym profilu,
zanim te trzy pliki powstaną. Brak jednego pliku nie wyłącza już całej edycji
komunikatem „Host nie dostał pełnych ścieżek profilu AMC”. Pliki są tworzone
wyłącznie przez hosta: na świeżym profilu host zakłada także katalog i pustą,
poprawną bazę Podcastów, zamiast kończyć start z powodu braku pliku.

## Uzasadnienie i decyzje

- **Korekta użytkownika z 20 października 2026:** po klawiszu funkcyjnym host
  mówił, że nie dostał pełnych ścieżek profilu i edycja jest niedostępna.
  Frontend przekazywał każdą ścieżkę tylko wtedy, gdy odpowiadający jej plik już
  istniał, natomiast host świadomie tworzy brakujące magazyny przy pierwszej
  transakcji. Od tej korekty komplet trzech docelowych ścieżek jest częścią
  kontraktu startu także dla świeżego AMC i AMC Test.

- **Korekta użytkownika z 20 października 2026:** `Ctrl+Shift+0` przestał
  wykonywać preset 10. Przyczyną było uznanie samego sukcesu `RegisterHotKey`
  za dowód, że wxPython otrzyma późniejsze zdarzenie, i wyłączenie wcześniejszej
  granicy klawiatury. Obie wąskie granice pozostają teraz aktywne dla okna AMC,
  a wspólna krótka bramka usuwa ewentualne podwójne zgłoszenie.
- **Korekta użytkownika z 19 października 2026:** nazwa „Bieżąca audycja lub
  utwór” mogła sugerować przejście do innego widoku. Zastąpiła ją nazwa
  „Informacje o bieżącym odtwarzaniu”, zgodna z rzeczywistym, wyłącznie
  informacyjnym działaniem `Alt+D`.
- **Korekta użytkownika z 19 października 2026:** samo podłączenie
  `EVT_CONTEXT_MENU` nie wystarczało dla `Shift+F10` na natywnej liście
  harmonogramów. Jawna trasa klawisza jest teraz wykonywana przed filtrem
  modyfikatorów i prowadzi do tej samej metody menu.
- **Korekta użytkownika z 19 października 2026:** usunięto z nazwy polecenia
  dopisek „lub usuń”, a lokalny plik nie jest już nazywany adresem. Zmiana
  objęła wspólny model menu, pomoc `Ctrl+F1`, pełną pomoc, paletę oraz
  kontekstową etykietę bieżącego wiersza.
- **Korekta użytkownika z 19 października 2026:** lista harmonogramów musi mieć
  własne menu kontekstowe. Jego pozycje prowadzą do tych samych metod co
  Insert, Enter, Ctrl+D, Spacja, strzałki i Delete.

- **Korekta użytkownika z 19 października 2026:** „Zmień stację” zastąpiono
  nazwą opisującą zmianę nazwy lub adresu, a ogólne `F2` opisuje dokładnie
  rodzaj zaznaczonego elementu.
- **Korekta użytkownika z 19 października 2026:** nazwa wyszukiwania brzmi
  „Szukaj w bieżącej usłudze”, nie samo „W bieżącej usłudze”.
- Pasek menu nadal zachowuje unikatowe klawisze dostępu, ponieważ są potrzebne
  do pełnej obsługi bez myszy. Menu kontekstowe nie czyta ich jako drugiego
  skrótu; pokazuje tylko rzeczywistą kombinację wykonującą polecenie.
- Menu kontekstowe nie ma własnych procedur wykonania. Filtruje aktualnie
  dostępne wpisy wspólnego modelu menu i wysyła tę samą akcję do jednego
  dispatchera.
- Zmiana nazwy playlisty używa pełnej, atomowej migawki playlist danej sesji.
  Nie usuwa elementów, nie zmienia kolejności i po zapisie przywraca fokus na
  ten sam stabilny identyfikator playlisty.
- **Korekta użytkownika z 19 października 2026:** uporządkowanie nie może być
  jednorazową łatką AMC. Reguła rozdzielenia mnemoniców od rzeczywistych
  skrótów, semantycznych nazw i dostępnych etykiet została zapisana jako
  trwały kontrakt projektu oraz automatycznie wykrywana umiejętność Codexa.

## Testy i wynik

- Test startu tworzy trzy nieistniejące ścieżki profilu i potwierdza, że każda
  trafia do konstruktora klienta hosta przed rozpoczęciem pracy w tle.
- Łączna bramka tej korekty: **75** testów ukierunkowanych, **194** testy
  skrótów/menu/pomocy/logiki GUI oraz **22/22** zestawy protokołu hosta.
- Pierwszy rzeczywisty kontrolowany start świeżego profilu został poprawnie
  odrzucony kodem 4 przed instalacją, ponieważ host wymagał już istniejącej
  bazy Podcastów. Po usunięciu tego błędnego założenia **22/22** testy Pythona
  startu i instalatora oraz wszystkie **22** zestawy protokołu hosta przeszły;
  test protokołu potwierdza utworzenie schematu i użytkowe wartości domyślne.

- Korekta `Ctrl+Shift+0`: **4/4 ukierunkowane testy zaliczone**. Potwierdzono
  rozpoznanie wyłącznie pierwszego dokładnego naciśnięcia, dostępność we
  wszystkich sesjach i widokach, równoczesne utrzymanie obu natywnych granic
  oraz pojedyncze wykonanie po podwójnym zgłoszeniu. `compileall` modułu
  wxPython zakończył się powodzeniem. Projektowy runner wykonał te cztery testy,
  ale cały przebieg oznaczył jako niezaliczony z powodu czterech niezwiązanych
  modułów testowych wymagających nieobecnego w systemowym Pythonie `pytest`;
  nie jest to przedstawiane jako pełna regresja.
- Najnowsza korekta: **71/71** testów menu, harmonogramów, pomocy i palety;
  pełny Core SmokeTests zakończony kodem 0. Test unikatowych klawiszy dostępu
  wykrył kolizję pierwszego wariantu mnemonika `Alt+D`; poprawiono ją przed
  zieloną bramką.
- Po korekcie nazw i harmonogramów: **136/136** testów ukierunkowanych oraz
  pełna regresja **1845 zaliczonych, 110 pominiętych, 11 podtestów**.
  Test kolizji mnemoniców wykrył pierwszą nieprawidłową literę dostępu;
  poprawiono ją przed powtórną zieloną bramką.

- 19.10.2026: wspólna bramka nagrywania, Spotify, menu, ustawień i
  harmonogramów — **204 zaliczone**.
- Testy potwierdzają kolejność „nazwa, rzeczywisty skrót”, usunięcie z menu
  kontekstowego `(&0)`, dynamiczną nazwę `F2`, atomową zmianę nazwy playlisty,
  zgodność podpisu skrótu z resolverem oraz unikatowe klawisze dostępu paska.
- Szeroki audyt sesji, skrótów AMC04, pomocy i palety: **403 zaliczone i 5
  podtestów zaliczonych**.
- Pełna regresja wxPython: **1837 zaliczonych, 110 pominiętych i 11 podtestów
  zaliczonych**. Kompilacja, instalacja i uruchomienie nadal pozostają
  osobnymi, jeszcze niewykonanymi krokami.
- Po dodaniu trwałego kontraktu ponownie zaliczono **121** testów menu,
  skrótów, pomocy i palety. Struktura umiejętności, odsyłacze i kodowanie UTF-8
  zostały sprawdzone; dostarczony walidator nie mógł ruszyć bez nieobecnego
  lokalnie modułu `yaml`, dlatego nie pobierano zależności z sieci.
- Pełna regresja po połączeniu kontraktu menu i wspólnego mechanizmu usług:
  **1842 zaliczone, 110 pominiętych i 11 podtestów zaliczonych** w 78,80 s.
- 19 października 2026 procedura promocji ponownie zaliczyła pełne **1842**
  testy wxPython, przy **110 pominiętych** i **11 podtestach zaliczonych**.
  Kompilacje Release przeszły bez ostrzeżeń i błędów; paczka 1666 plików
  przeszła manifest, SHA-256, instalację, kontrolę integralności z kodem 0 i
  weryfikację procesu oraz tytułu „AMC Test”.

## Znane ograniczenia

- Korekta pełnych ścieżek profilu i świeżej bazy Podcastów jest zbudowana,
  zainstalowana i uruchomiona; czeka na ręczny odbiór klawiszy funkcyjnych z
  NVDA.

- Korekta `Ctrl+Shift+0` z commitu `5d959607` jest w bieżącej instalacji;
  pozostaje ręczny odbiór z NVDA.
- Najnowsza korekta `Shift+F10` i nazwy `Alt+D` jest w bieżącej instalacji AMC
  Test; wymaga jeszcze ręcznego odbioru NVDA.

- Odbiór NVDA musi potwierdzić menu listy i odtwarzacza w Plikach lokalnych,
  Radiu, Podcastach i YouTube, Spotify, TIDAL, WiiM i Sonos.
- Pozycje niedostępne są pomijane w menu kontekstowym; pasek menu może je
  pozostawić wyszarzone, aby uczciwie pokazać chwilowy brak warunku.
- Zadanie nie zmienia przypisań AMC04 ani nie dodaje zakładek do sesji bez
  stabilnej pozycji.

## Wersja i commit

- Pełne ścieżki profilu: `3be15559` —
  `fix(amc): utrzymaj wyszukiwanie i profil hosta`; bezpieczny świeży magazyn:
  `7a58e926` — `fix(host): uruchom swiezy profil z pelnymi sciezkami`.
- Zainstalowana kompilacja:
  `2026.10.21-233767f1971349a32bb33468c16bfca19217403e-0c73b6fb`;
  pełna integralność, kontrolowany start instalacji i jeden komplet procesów
  AMC Test zostały potwierdzone. Publikacji nie wykonywano.

- Źródła regresji `Ctrl+Shift+0`: `5d959607` —
  `fix(amc): przywroc Ctrl Shift 0 na listach`. Testy ukierunkowane i analiza
  składni przeszły; kompilacji paczki, instalacji, uruchomienia ani publikacji
  nie wykonywano.
- Najnowsza korekta: `4c75f237`; pełna regresja wxPython: **1848 zaliczonych,
  110 pominiętych i 11 podtestów zaliczonych**. Kompilacje Release, manifest,
  SHA-256, instalacja i uruchomienie zaliczone.
- Instalacja zawierająca najnowszą korektę:
  `2026.10.19-2930a9498f7abc4e64a10362ab67631d6423b407-67d8b325`.
- Korekta po ręcznym odbiorze: `4b85e0c8` — źródła, testy, instalacja i
  uruchomienie zweryfikowane.
- Instalacja zawierająca korektę:
  `2026.10.19-0c098eb5dde2b039f5d6f86c415559d63c84adcc-9e2be1b9`.

- Źródła: commit `fdc26b83` — `fix(amc): uporzadkuj nagrania menu i playlisty
  Spotify`.
- Trwały kontrakt repozytorium: commit `e317cbbc` — `refactor(amc): ujednolic
  listy uslug i kontrakt menu`. Umiejętność osobista
  `accessible-desktop-contracts` jest zapisana poza repozytorium w katalogu
  umiejętności Codexa.
- Testy ukierunkowane: zaliczone.
- Kompilacja, instalacja i uruchomienie:
  `2026.10.19-8093e22adfa9b6abfc72c35c5c7029eaaade88c8-718893c9`;
  zweryfikowane 19 października 2026.
- Publikacja: niewykonana i niedozwolona bez osobnego polecenia użytkownika.
