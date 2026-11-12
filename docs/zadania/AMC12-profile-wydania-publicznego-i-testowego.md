# AMC12 — profile wydania publicznego i testowego

## Nadrzędna korekta użytkownika z 28.10.2026 — Public bez Sonosa

### Materiał do dokumentacji użytkownika

AMC Public udostępnia Pliki lokalne, Radio, Podcasty i YouTube, WiiM oraz
Frontier Smart. Menedżer sesji, menu Widok, pomoc kontekstowa i paleta poleceń
nie pokazują Sonosa, Spotify ani TIDAL-a. Aktualizacja z błędnego
`alpha.439` usuwa pozostawiony moduł Sonosa i instaluje moduł Frontier Smart.

### Uzasadnienie i decyzje

Ta decyzja zastępuje korektę z 21.10.2026 mówiącą, że Sonos pozostaje w
Public, oraz błędny profil opublikowanego `0.1.0-alpha.439`. Właściwy Public
ma zawierać WiiM i odebrany Frontier Smart, ale nie ma zawierać Sonosa,
Spotify ani TIDAL-a. Nowy instalator otrzymuje numer `0.1.0-alpha.440`; starego
wydania nie przepisujemy ani nie podmieniamy pod tym samym tagiem.

Aktualizacja z `alpha.439` musi przyjąć starszą listę sesji jako zgodny stan
wejściowy, usunąć pozostawiony moduł Sonosa z katalogu programu i zainstalować
moduł Frontier Smart. Menu, Menedżer sesji, `Ctrl+F1`, pełna pomoc, paleta
`Ctrl+Shift+K` i wykonanie mają wynikać z jednego poprawionego profilu.

### Testy i wynik

- Celowane testy profilu, wspólnego modelu menu/palety, Frontier Smart,
  fizycznej zawartości paczki, instalatora i wersji: **100/100 zaliczonych**.
- Launcher i aktualizator: **38/38 zaliczonych**, w tym migracja starszego
  Public z listy Sonos na Frontier Smart.
- Testów z widocznym interfejsem ani pełnej regresji GUI nie uruchamiano.
  Paczka, instalator i publikacja zostały sprawdzone osobno.
- Czysta paczka `8da5168e` zawiera 1152 pliki. Manifest i fizyczny odczyt
  potwierdzają Frontier Smart oraz brak modułów Sonosa, Spotify i TIDAL-a;
  pełna cicha integralność i kontrolowany start są zaliczone.
- Instalator przeszedł skan sekretów i suchą bramkę kanału `stable`.
  Wydanie `public-2026.10.28.2201` zostało opublikowane i odczytowo
  zweryfikowane; GitHub potwierdził nazwę, rozmiar i SHA-256 zasobów.

### Znane ograniczenia

- Istniejący `alpha.439` pozostaje historycznym wydaniem o błędnym zakresie
  sesji. Nie jest usuwany ani przepisywany.
- Ręczny odbiór Frontier Smart z NVDA nie jest częścią niewidocznych testów
  profilu.

### Wersja i commit

- Wersja techniczna: `0.1.0-alpha.440`; wersja widoczna:
  `28.10.2026, 22:01`.
- Commit źródłowy:
  `eff12af289d0eaa81058d19bc17b2f418d415e28`.
- Paczka: `8da5168e`.
- Instalator:
  `D:\Projekty Codex\AMC aktualizacje\AMC Public 0.1.0-alpha.440 korekta 2026-10-28\instalator\AMC-Public-2026-10-28-2201.exe`.
- SHA-256:
  `e1c111cf4a0eb8432cfb0d2f8682443cd5bc3be481525c37108d0c6ea2485b0f`.
- Tag: `public-2026.10.28.2201`; wydanie stabilne, nie prerelease.
- Instalatora nie uruchamiano ani nie instalowano; działający AMC Test i
  oczekujące aktualizatory pozostały nietknięte.

## Nadrzędna korekta nazewnictwa z 22.10.2026

Nazwy widoczne dla użytkownika mają od tej chwili postać
`AMC Test — dd.mm.rrrr, gg:mm` oraz `AMC Public — dd.mm.rrrr, gg:mm`.
Przyszły plik instalatora ma nazywać się na przykład
`AMC-Public-2026-10-22-2125.exe`, bez widocznego numeru `alpha`. Commit,
`bundleId`, techniczny `AppVersion` i tag mogą pozostać wewnętrznymi
identyfikatorami kontroli oraz publikacji.

`AMC Public` oznacza paczkę zweryfikowaną i gotową do samodzielnego wysłania
innej osobie. Nie oznacza automatycznie, że paczka jest już na GitHubie;
gotowość lokalna, instalacja, ręczny odbiór i publikacja zdalna nadal są
osobnymi, jawnie raportowanymi stanami. Ta korekta zastępuje wcześniejsze
propozycje nazw użytkowych `AMC`, `alpha.4xx`, „kompilacja 4xx” i długich
etykiet BUILD. Występujące niżej stare nazwy plików pozostają wyłącznie
historycznym zapisem artefaktów, które rzeczywiście wtedy powstały.

## Punkt wznowienia po odnowieniu limitu — 21.10.2026

Nie rozpoczynać ponownej analizy architektury ani odtwarzania wcześniejszych
ustaleń. Bazą następnego publicznego instalatora ma być najnowsza odebrana
wersja AMC Test. Warunek użytkownika: dopiero po jego jednoznacznym odbiorze
Frontier Smart dołączyć tę sesję do profilu publicznego. Publiczny wariant ma
być kopią odebranego Testu z ukrytymi i wyłączonymi wyłącznie niedopracowanymi
funkcjami streamingowymi — obecnie TIDAL i Spotify.

Kolejność dalszych prac:

1. Po komunikacie użytkownika, że Frontier Smart został odebrany, dodać go do
   publicznego profilu sesji i możliwości. Nie publikować go warunkowo ani
   automatycznie przed tym potwierdzeniem.
2. Zbudować następcę wstrzymanego `alpha.433` z najnowszych odebranych źródeł,
   w tym z poprawką ręcznej kolejności playlist Radia z commita `c25ecb50`.
3. Osobno potwierdzić źródła, testy, paczkę, integralność, kontrolowany start,
   czystą instalację i działający proces. Dopiero po odbiorze uznać instalator
   za przeznaczony do wysłania.
4. Ten sam zweryfikowany plik umieścić jako zasób wydania GitHub, a następnie
   sprawdzić z wcześniejszej wersji publicznej wykrycie, pobranie, instalację i
   ponowny start aktualizacji. Zweryfikować zdalną nazwę, rozmiar i SHA-256.
5. Sprawdzić pełny przepływ zgłaszania błędów z aplikacji.
6. Następnym etapem funkcjonalnym jest dokończenie logowania YouTube.

Aktualny `alpha.433` pozostaje kandydatem wstrzymanym: został zbudowany około
21:26 z rewizji `8ab21a25`, nie ma Frontier Smart ani późniejszej poprawki
playlist Radia i nie powinien być wysyłany jako najnowsze wydanie. Aktualna
zainstalowana paczka Test powstała około 21:43 z rewizji `20a3ca68`, zawiera
Frontier Smart oraz poprawkę playlist Radia, lecz nie jest instalatorem
publicznym.

## Status

**Lokalny publiczny kandydat `0.1.0-alpha.433` jest zbudowany i zainstalowany w
`D:\Projekty Codex\AMC Public`, ale decyzją użytkownika z 21.10.2026 pozostaje
wstrzymany do odbioru Smart Frontier. Nie uruchomiono go do odbioru, nie
opublikowano i nie należy go jeszcze przekazywać użytkownikom. Kandydat ma
ręczne porządkowanie Biblioteki i Ulubionych Radia przez `Alt+strzałka góra/dół`
oraz `Ctrl+X`, `Ctrl+V`. Pełna regresja wxPython: 1962 zaliczone, 110 świadomie
pominiętych i 11 podtestów zaliczonych; 26 zestawów protokołu C# przeszło, a
kompilacja całego rozwiązania Release miała 0 ostrzeżeń i 0 błędów. Paczka
`e6d4269d` ma 1075 kontrolowanych plików; integralność i kontrolowany start
paczki są zaliczone, a integralność zainstalowanej kopii zakończyła się kodem
0. Ten wstrzymany wariant nie zawiera TIDAL-a, Spotify ani Frontier Smart;
WiiM i Sonos pozostają. Następny publiczny kandydat powstanie dopiero po
decyzji, czy odebrany Smart Frontier ma wejść do profilu publicznego.**

Korekta ręcznej kolejności jest zapisana w commicie
`8ab21a25bb61a0807fc4870b558b39a1eeb93c57` —
`fix(amc): dodaj reczne porzadkowanie radia`. Z tej czystej rewizji powstał
wstrzymany kandydat `0.1.0-alpha.433`; nie jest wydaniem publicznym.

### Korekta użytkownika z 21.10.2026 — ręczna kolejność w playliście Radia

Ta korekta zastępuje wcześniejsze ograniczenie, według którego gesty ręcznej
kolejności Radia dotyczyły wyłącznie Biblioteki i Ulubionych. Użytkownik
potwierdził w zainstalowanym AMC Test, że `Ctrl+X` oraz
`Alt+strzałka góra/dół` nadal nie były dostępne na liście stacji radiowych.

#### Zachowanie widoczne dla użytkownika

- W Bibliotece i Ulubionych Radia ręczne przesuwanie nadal wymaga kolejności
  własnej wybranej skrótem `Alt+3`.
- W otwartej playliście Radia `Alt+strzałka góra/dół` przesuwa ciągły blok o
  jedną pozycję, a `Ctrl+X`, wybór celu i `Ctrl+V` przenoszą jedną lub wiele
  stacji przed nazwany element — dokładnie przez wspólny mechanizm playlist.
- `Alt+1`, `Alt+2` i `Alt+3` w otwartej playliście nie zmieniają trybu
  sortowania całej Biblioteki ani Ulubionych.
- Historia, nagrania i pozostałe listy Radia nie przejmują przypadkowo
  `Ctrl+X`, `Ctrl+V` ani `Alt+strzałek` jako operacji porządkowania.

#### Przyczyna i rozwiązanie

Router rozpoznawał cztery gesty jako polecenia kolekcji Radia i poza
Biblioteką/Ulubionymi kończył obsługę wynikiem „brak polecenia”. W efekcie
otwarta playlista radiowa nie docierała do istniejącego wspólnego mechanizmu
playlist, mimo że wykonanie i zapis kolejności już ją obsługiwały. Dodano jawny
kontekst zawartości playlisty Radia w wykonaniu oraz w pomocy klawiatury;
wyłącznie cztery gesty ręcznej kolejności przechodzą wtedy do wspólnej akcji.

#### Testy i wersja

- Testy celowane routingu, rzeczywistego zdarzenia klawiatury i operacji GUI:
  23 zaliczone; po dodaniu testu okna dla playlisty ponowny zestaw: 8 zaliczonych.
- Pełna regresja wxPython: 1961 testów zaliczonych, 110 pominiętych i 11
  podtestów zaliczonych; cztery testy lokalnego serwera podcastów zostały
  zablokowane przez sieć piaskownicy, a następnie wszystkie 4 zaliczono poza
  tym ograniczeniem. Łączny wynik funkcjonalny: 1965 zaliczonych.
- Zmiana źródłowa: commit `c25ecb50` —
  `fix(radio): przywroc reczne porzadkowanie playlist`.
- Paczka Test z rewizji
  `20a3ca68dd2a078554821e9a2bdea21cd478a88e` ma identyfikator `a60df04f`
  i 1670 kontrolowanych plików. Integralność oraz kontrolowany start kandydata
  i zainstalowanej kopii przeszły. Atomowa instalacja do
  `D:\Projekty Codex\AMC Python` zakończyła się powodzeniem, a następnie
  potwierdzono okno `AMC Test — 21.10.2026, 21:43` z prywatnego runtime'u tej
  instalacji.
- Po uruchomieniu potok dodatku NVDA `AMC.NVDA.v1.1` był dostępny w tej samej
  sesji Windows co NVDA i AMC. Rzeczywiste polecenie dodatku `status` zwróciło
  `Radio, brak otwartego nagrania.` Ręczny odbiór czterech gestów oraz ich
  komunikatów przez użytkownika pozostaje wymagany; pomyślny test potoku nie
  zastępuje nawigacji klawiaturą z NVDA.

### Korekta użytkownika z 21.10.2026 — wstrzymanie publicznego kandydata

Ta decyzja zastępuje wcześniejszy plan natychmiastowego uruchomienia i odbioru
`alpha.433`. Użytkownik chce najpierw sprawdzić Smart Frontier. Jeżeli funkcja
przejdzie odbiór, kolejny publiczny instalator ma ją zawierać; dzisiejszego
kandydata bez Frontier nie publikujemy ani nie przekazujemy dalej.

#### Materiał do dokumentacji użytkownika

- W Bibliotece i Ulubionych Radia `Alt+3` wybiera kolejność własną.
- `Alt+strzałka w górę` i `Alt+strzałka w dół` przesuwają stację albo ciągły
  blok o jedną pozycję. NVDA podaje kierunek oraz konkretną stację, na przykład
  `Przeniesiono stację w dół, pod Trójka`.
- `Ctrl+X` zapamiętuje stację albo wielozaznaczenie do przeniesienia. Po
  ustawieniu fokusu na celu `Ctrl+V` wstawia wybór przed tą stacją i podaje jej
  nazwę. Identyfikatory techniczne nie trafiają do mowy.
- Gdy lista nie używa kolejności własnej, AMC mówi:
  `Przenoszenie działa w kolejności własnej. Naciśnij Alt 3`. Aktywny filtr
  należy wcześniej wyczyścić klawiszem `Escape`.
- Istniejące lokalne playlisty oraz własne strumienie WiiM i Sonos zachowują
  swoje analogiczne polecenia. Menu, `Ctrl+F1`, pełna pomoc i paleta poleceń
  korzystają z tych samych akcji i warunków dostępności.

#### Uzasadnienie i decyzje

- Brak działania w Radiu nie był skutkiem profilu ani dwóch instalatorów.
  Widok Radia miał sortowanie `Alt+1–3`, lecz nie był dołączony do istniejącego
  wspólnego kontraktu ręcznej kolejności.
- Zapis wykonuje wyłącznie host C# w jednej transakcji SQLite i zmienia tylko
  `library_custom_order` albo `favorite_order` sesji `radio`. Python nie zapisuje
  bazy bezpośrednio.
- Ręczna kolejność jest dostępna tylko w Bibliotece i Ulubionych Radia. Historia,
  wyniki wyszukiwania i odtwarzacz nie mają własnego trwałego porządku, więc
  skróty nie są tam reklamowane.
- Szybkie działanie pełnego profilu potwierdza, że liczba danych nie jest sama
  w sobie przyczyną wcześniejszych opóźnień. Wcześniejszy pusty profil,
  nieudane starty hosta i błędne odświeżanie widoków były osobnymi problemami.
  Nie jest to dowód, że dwa procesy powinny stale współdzielić jedną bazę.

#### Testy i wynik

- Testy celowane skrótów, menu, klienta hosta i komunikatów GUI: 127 zaliczonych.
- Rzeczywisty proces LiteHost i tymczasowa baza SQLite potwierdziły osobny
  zapis kolejności Biblioteki i Ulubionych oraz odrzucenie Historii.
- Pełna regresja wxPython po poprawce zgodności: `1962 passed, 110 skipped, 11
  subtests passed`.
- Wszystkie 26 zestawów protokołu C# przeszło. Kompilacja całego rozwiązania
  Release: 0 ostrzeżeń i 0 błędów.
- Paczka publiczna przeszła pełną integralność i kontrolowany start. Instalacja
  do `D:\Projekty Codex\AMC Public` zakończyła się kodem 0; cicha kontrola
  integralności zainstalowanej kopii również kodem 0.

#### Znane ograniczenia

- Ręczny odbiór komunikatów, wielozaznaczenia i powrotu fokusu z NVDA pozostaje
  do wykonania.
- Zdalne playlisty TIDAL/Spotify nie są częścią tego lokalnego kontraktu i nie
  występują w publicznym profilu. Własne strumienie WiiM i Sonos są obsługiwane
  przez ich istniejące, osobne operacje.
- `alpha.433` jest zachowany jako lokalny, odtwarzalny kandydat, ale został
  wstrzymany przed uruchomieniem i publikacją. Nie usuwać go przed decyzją o
  następcy z Frontier Smart.

#### Wersja i commit

- Źródła ręcznej kolejności: `8ab21a25bb61a0807fc4870b558b39a1eeb93c57`.
- Paczka publiczna: `e6d4269d`, 1075 plików, czyste źródła.
- Instalator:
  `D:\Projekty Codex\AMC - integracja wxPython\publish\installers\alpha.433\AMC-0.1.0-alpha.433-win-x64.exe`.
- Rozmiar: 124 085 306 bajtów. SHA-256:
  `0ee2acbb30842734aca0e81bfbd9fa685ea9c194d1db95e7f6994ea3160a89a1`.
- Instalacja: wykonana lokalnie; uruchomienie widocznego AMC Public i publikacja:
  wstrzymane.

### Korekta użytkownika z 21.10.2026

Ta decyzja zastępuje wcześniejsze opisy o stałym, ogólnym ograniczaniu profilu
publicznego do wybranej listy usług. Kryterium jest dojrzałość funkcji:
publiczny AMC ma być kopią AMC Test i nie zawierać wyłącznie tych funkcji
streamingowych, które nie są jeszcze dopracowane. Ukończone funkcje — w tym
Sonos — pozostają dostępne. Obecnie wyłączone pozostają TIDAL, Spotify i
nieodebrana jeszcze integracja Frontier Smart.

### Korekta odbiorcza z 21.10.2026 — Ctrl+F5 i kopia danych Test → Public

Ta korekta uzupełnia wcześniejszą decyzję o osobnych profilach. Użytkownik nie
powinien ręcznie szukać katalogu profilu ani reinstalować programu, żeby
przygotować publiczny AMC do odbioru. Wygodnym modelem jest pełna kopia danych
wspólnych z AMC Test do osobnego profilu publicznego. Oba uruchomione programy
nie mogą natomiast stale zapisywać do tych samych plików SQLite i JSON, bo
oznaczałoby to dwóch pisarzy, konflikty historii, ustawień i harmonogramów.

#### Zachowanie widoczne dla użytkownika

- Polecenie `Odśwież dane z AMC Test` w menu Ustawienia i palecie poleceń jest
  bezpieczną, jednokierunkową kopią Test → Public. Przy konflikcie pyta przed
  zastąpieniem danych; nie przenosi haseł, tokenów ani funkcji wyłączonych w
  profilu publicznym.
- W sesji Pliki lokalne `Ctrl+F5` otwiera `Foldery Biblioteki`. Jeżeli lista
  jest pusta, okno mówi: `Brak dodanych folderów. Wybierz Dodaj folder, aby
  utworzyć Bibliotekę.` i ustawia fokus na przycisku `Dodaj folder`.
- Po skopiowaniu danych `Ctrl+F5` pokazuje trzy foldery Biblioteki. Kopia
  zastosowana 21.10.2026 zawiera 11 685 plików, 242 stacje, 352 źródła
  podcastów i YouTube, 54 655 odcinków, 25 zakładek, 52 harmonogramy oraz 538
  wpisów historii nagrywania. Publiczna baza ma 931 wpisów historii
  odtwarzania.
- AMC Test i AMC Public należy podczas odbioru uruchamiać kolejno, nie
  równocześnie. Korekta użytkownika z 21.10.2026 zastępuje wcześniejsze
  rozważanie technicznej blokady obu procesów: wystarczającym modelem pracy
  jest świadome zamknięcie bieżącego wariantu przed przełączeniem. Nie dodajemy
  osobnej blokady ani automatycznej synchronizacji. Nie jest to utrudnienie dla
  zwykłego użytkownika, który ma tylko publiczny AMC.

#### Diagnoza, zabezpieczenie danych i dowody

- Profil publiczny był rzeczywiście pusty: 0 źródeł folderów i 0 lokalnych
  rekordów. Migawka AMC Test zawierała 3 źródła i 11 685 rekordów. Reinstalacja
  tego samego pakietu nie naprawiłaby tej różnicy.
- Dziennik frontendu zawierał cztery awarie startu hosta zakończone ogólnym
  `Silnik przestał odpowiadać`, w tym o 20:18, gdy AMC Test nadal pracował.
  Interfejs przekazywał dotąd `on_stderr=lambda line: None`, więc usuwał
  diagnostykę procesu potomnego. `alpha.432` zapisuje ją w lokalnym dzienniku
  frontendu; techniczne wiersze nie trafiają do zwykłej mowy NVDA.
- Przed kopią utworzono niezmienioną kopię bezpieczeństwa 7 plików, 315 834
  bajty, w `D:\Projekty Codex\AMC Public Profile
  Backups\before-test-copy-20261021-2028`. Następnie AMC Test zamknięto zwykłym
  żądaniem zamknięcia okna. Ostatnie ręczne nagranie miało potwierdzone
  zakończenie o 19:56; procesu nie kończono siłowo.
- Kontrolowany start AMC Test utworzył świeżą migawkę o 20:28 i zakończył się
  kodem 0. Import do publicznego profilu zakończył się stanem `imported`, a
  ponowny odczyt stanem `current`; host zwrócił 3 dostępne foldery. Kopia
  bezpieczeństwa nie została usunięta.
- Testy celowane poprawki: 113 zaliczonych. Pełna regresja wxPython: 1955
  zaliczonych, 110 świadomie pominiętych i 11 podtestów zaliczonych. Wszystkie
  26 zestawów protokołu C# przeszło. Kompilacja całego rozwiązania Release:
  0 ostrzeżeń i 0 błędów.
- Instalator `AMC-0.1.0-alpha.432-win-x64.exe` ma 124 071 145 bajtów i SHA-256
  `9313dd6a0fcaf179d705bd87949b188ff02de8f33b12d68cebb301c4f1bec6f4`.
  Zawiera paczkę `7f03dd5e`, profil `public`, czystą rewizję `20877cf7` oraz
  sesje Pliki, Radio, Podcasty i YouTube, WiiM i Sonos; nie zawiera TIDAL,
  Spotify ani Frontier Smart.
- Instalacja do `D:\Projekty Codex\AMC Public` zakończyła się kodem 0. Pełna
  kontrola integralności zainstalowanej kopii zakończyła się kodem 0, a
  izolowany test startu interfejsu i hosta wynikiem `OK`. Test rzeczywistego
  profilu po instalacji zwrócił 3 dostępne foldery, 11 685 elementów lokalnych
  i stan wspólnych danych `current`. Uruchomiono tylko procesy z katalogu AMC
  Public; AMC Test pozostał zamknięty.
- Powyższe potwierdza źródła, testy, kompilację, paczkę, instalator,
  zainstalowaną kopię i rzeczywisty profil. Nadal nie zastępuje ręcznego
  odbioru fokusu, komunikatów i nawigacji z NVDA.

#### Szkic listy zmian do zatwierdzenia przez użytkownika

- `Ctrl+F5` w Plikach lokalnych prowadzi do czytelnego zarządzania Folderami
  Biblioteki także na pustym profilu.
- Dane do odbioru publicznego AMC można skopiować z AMC Test jednym poleceniem,
  bez współdzielenia żywych baz i bez reinstalacji.
- Awarie silnika zachowują użyteczną diagnostykę w lokalnym dzienniku zamiast
  kończyć się wyłącznie komunikatem `Silnik przestał odpowiadać`.

### Korekta odbiorcza z 21.10.2026 — Ctrl+Shift+U i folder z Ctrl+O

Ta korekta zastępuje wcześniejsze założenie, że każdy wiersz zwykłego folderu
i każdy wiersz Biblioteki mają ten sam identyfikator. Folder otwarty przez
`Ctrl+O` używa identyfikatora `file:` z pełną ścieżką, natomiast ten sam plik
w bazie Biblioteki ma trwały identyfikator `local-*`. Host próbował wcześniej
odnaleźć w bazie dosłowne `file:...`, a po powodzeniu interfejs bez potrzeby
odświeżał bieżący folder jak widok Biblioteki. To mogło podać „zero elementów”
i stworzyć wrażenie, że zniknął folder albo cała Biblioteka.

#### Zachowanie widoczne dla użytkownika

- `Ctrl+Shift+U` w folderze z `Ctrl+O` przekazuje hostowi także zweryfikowaną
  ścieżkę. Host rozpoznaje po niej istniejący rekord Biblioteki i atomowo
  zmienia Ulubione; jeżeli rekord był czasowo poza zakresem Biblioteki,
  przywraca również jego członkostwo i oba trwałe porządki.
- Po zmianie Ulubionych AMC pozostaje w bieżącym folderze. Nie przełącza listy
  na pustą Bibliotekę i nie przykrywa komunikatu powodzenia komunikatem
  „zero elementów”. Tylko otwarty widok `Ulubione` jest odświeżany, bo jego
  skład rzeczywiście się wtedy zmienia; komunikat pada po odświeżeniu.
- Jeśli plik naprawdę nie ma jeszcze rekordu Biblioteki, AMC nie udaje
  powodzenia i nie dodaje potajemnie całego folderu. NVDA otrzymuje:
  `Nie mogę zmienić Ulubionych: Wybranego pliku nie ma w Bibliotece. Ctrl+O
  tylko otwiera folder. Dodaj folder poleceniem Foldery Biblioteki, Ctrl+F5.`
- Usunięcie z Ulubionych nie usuwa pliku z Biblioteki ani z dysku. Dodanie do
  Ulubionych gwarantuje natomiast, że plik należy również do Biblioteki.

#### Dowody i rozróżnienie etapów

- Odczyt aktywnego profilu AMC Test w chwili diagnozy potwierdził 11 685
  rekordów lokalnych, 3 źródła folderów, 3 517 elementów Biblioteki i 9
  Ulubionych; dane nie zostały skasowane. Nieaktywny profil publiczny miał 0
  źródeł i 0 lokalnych rekordów, ponieważ `Ctrl+O` jedynie przeglądał folder.
- Test integracyjny przez rzeczywisty proces hosta i tymczasową bazę potwierdza
  rozpoznanie dwóch wierszy `file:` po ścieżkach, ustawienie Ulubionych i
  Biblioteki, zapis kolejności oraz uczciwy błąd dla pliku spoza Biblioteki.
- Testy celowane: 59 zaliczonych. Pełna regresja wxPython po korekcie: 1953
  zaliczone, 110 świadomie pominiętych i 11 podtestów zaliczonych. Wszystkie
  26 zestawów protokołu C# przeszło. Kompilacja LiteHost Release: 0 ostrzeżeń,
  0 błędów.
- Z czystej rewizji `435eb49c6d4176a32a9b40f017d2e420790befaa` powstała
  publiczna paczka `1555412f` z 1075 plikami. Manifest potwierdza obecność WiiM
  i Sonosa oraz brak TIDAL-a, Spotify i Frontier Smart.
- Instalator `AMC-0.1.0-alpha.431-win-x64.exe` ma 124 075 026 bajtów i SHA-256
  `cf6309666f981b728167be16bbddf4066643ba9e2cbbed6d901b96310ba5910d`.
  Instalacja do `D:\Projekty Codex\AMC Public` zakończyła się kodem 0; pełna
  integralność, kontrolowany start i zwykłe uruchomienie trzech procesów
  profilu `public` są zaliczone.
- Działający równolegle AMC Test pozostał nietknięty. Ręczny odbiór mowy,
  zachowania listy i powrotu fokusu w NVDA pozostaje do wykonania przez
  użytkownika. Publikacji zdalnej nie wykonano.

### Korekta odbiorcza z 21.10.2026 — Historia i pojedynczy element

Ta decyzja zastępuje wcześniejsze założenie, że puste `Ctrl+H` po działającym
Radiu oznacza wyłącznie pusty profil publiczny. Odczyt bazy potwierdził 0
wpisów, ale przegląd drogi wykonania wykazał też brak zapisu Historii w
warstwie wxPython. Pełne AMC WPF wywoływało `PlaybackHistory.Record`, natomiast
LiteHost uruchamiał dźwięk bez odpowiednika tej operacji.

#### Zachowanie widoczne dla użytkownika

- Host zapisuje wpis dopiero ze zdarzenia `PlaybackStarted` prawdziwego
  silnika. Radio trafia do sesji `radio`, zwykły plik do `local`, a odcinek do
  `podcasts`; automatyczne przejście kolejki korzysta z tego samego zdarzenia.
- Ponowne odtworzenie przesuwa wpis na początek bez duplikatu. Każda sesja ma
  własną listę do 500 pozycji.
- Błąd zapisu Historii nie jest błędem odtwarzania: dźwięk pozostaje aktywny,
  NVDA mówi tylko „Odtwarzanie działa, ale nie udało się zapisać pozycji w
  Historii odtwarzania”, a techniczna przyczyna trafia wyłącznie do logu.
- Przy jednym elemencie `Alt+R`, `Alt+Shift+R`, `Ctrl+H` i `Ctrl+I` synchronizują
  listę, po czym podają wspólnie kontekst oraz użytkową nazwę wybranego wiersza.
  Identyfikator techniczny nie trafia do mowy. Przy wielu elementach pozostaje
  zwykła mowa natywnej listy.
- Starego zera w publicznym profilu nie da się uzupełnić wstecz: wcześniejsza
  wersja nie zapisała danych, z których można byłoby odtworzyć Historię.

#### Implementacja i testy źródłowe

- Jedynym pisarzem `library.db` pozostaje host C#. Atomowa mutacja przepisuje
  wyłącznie jedną sesję `playback_history` i aktualizuje metadane w tej samej
  transakcji.
- Commit zachowania i testów: `d94b4303` — `fix(amc): zapisz historie i nazwij
  pojedyncze podglady`.
- Testy celowane interfejsu: 129 zaliczonych; osobny test komunikatu błędu i
  źródła zdarzenia: 5 zaliczonych.
- Pełna regresja przed dopisaniem ostatniego świadectwa strukturalnego: 1951
  zaliczonych, 110 pominiętych i 11 podtestów zaliczonych. Pełne 26 zestawów
  protokołu C# przeszło, w tym nowy test kolejności, deduplikacji, rozdzielenia
  sesji i limitu 500.
- Jest to dowód źródeł i testów, nie jeszcze buildu ani instalacji `alpha.430`.

### Korekta odbiorcza z 21.10.2026 — `alpha.428`

Ta korekta zastępuje wcześniejsze założenie, że po fizycznym usunięciu pliku
zwykłe odświeżenie Biblioteki jest bezpieczne także w folderze otwartym przez
`Ctrl+O`. Folder z `Ctrl+O` jest bieżącym widokiem dysku, a nie widokiem bazy
SQLite. Próba odświeżenia go jako Biblioteki przełączała listę na pustą
Bibliotekę i stwarzała wrażenie, że zniknęła cała zawartość folderu.

#### Materiał do dokumentacji użytkownika

- `Shift+Delete` przenosi do systemowego Kosza tylko zaznaczone pliki
  potwierdzone przez silnik. Po operacji AMC pozostaje w tym samym folderze,
  usuwa z listy tylko te ścieżki i ustawia fokus na najbliższej pozostałej
  pozycji. NVDA otrzymuje na przykład: `Przeniesiono do Kosza: 2 pliki. Liczba
  pozycji pozostałych w folderze: 159`.
- Jeśli odpowiedź o usunięciu wróci po przejściu do innej sesji albo
  odtwarzacza, AMC nie przenosi użytkownika z powrotem i nie podmienia
  aktualnie oglądanego widoku.
- `B` w odtwarzaczu zapisuje zakładkę również dla pliku uruchomionego
  bezpośrednio z folderu, nawet gdy plik nie ma rekordu Biblioteki. Zwykły
  komunikat sukcesu pozostaje bez zmian, na przykład `Dodano zakładkę: 1:05`.
- Lewa strzałka nadal mierzy rzeczywiste parametry pliku. Kolejne naciśnięcie
  dla tego samego elementu korzysta z krótkiej pamięci procesu, a kilka
  szybkich naciśnięć nie uruchamia kilku kosztownych pomiarów jednocześnie.
- Pusta Biblioteka na świeżym profilu publicznym jest stanem prawidłowym.
  `Ctrl+O` tylko otwiera folder do przeglądania; trwałe dodanie folderu do
  Biblioteki jest osobnym, jawnym poleceniem zarządzania Biblioteką.
- AMC Test i publiczny AMC nadal powstają z jednego kodu i jednego procesu
  budowy. Publiczny profil różni się wyłącznie manifestem możliwości: obecnie
  nie zawiera TIDAL-a, Spotify ani Frontier Smart. Ich niedostępne adaptery są
  też fizycznie pomijane w paczce. WiiM i Sonos pozostają.

#### Uzasadnienie i decyzje

- Oględziny systemowego Kosza potwierdziły trzy pliki przeniesione dokładnie w
  czasie zgłoszonej operacji. Ich zawartość istnieje w Koszu, a rozmiary są
  zgodne. W folderze pozostało 159 plików multimedialnych. Nie przywracano ich
  bez osobnego polecenia użytkownika.
- Zniknięcie całej listy było zmianą widoku na pustą Bibliotekę, a nie
  skasowaniem całego folderu.
- Osobne instalatory są potrzebne dla tożsamości programu, profilu danych i
  kanału aktualizacji, lecz nie oznaczają dwóch implementacji. Oba warianty
  są składane z tego samego commitu i filtrowane manifestem wydania.
- Pierwsza instalacja `alpha.428` ujawniła, że Inno Setup pozostawiał w
  istniejącym katalogu plik modułu wycofany z nowej paczki. Paczka była
  prawidłowa, ale aktualizacja nie dawała jeszcze dokładnej kopii. Instalator
  usuwa teraz przed kopiowaniem moduły niedostępne w profilu publicznym, a
  budowa wymaga czystych śledzonych źródeł i zapisuje osobną rewizję kodu
  instalatora. Powtórna instalacja na celowo zastanym katalogu usunęła stary
  plik Frontier bez naruszenia WiiM, Sonosa i kopii danych.

#### Testy i wynik

- Testy ukierunkowane skrótu, dyspozycji polecenia, menu, zakładek, Kosza,
  szybkiej informacji i źródeł Biblioteki: `128 passed`.
- Pełna regresja wxPython po zmianie: `1945 passed, 110 skipped, 11 subtests
  passed` w końcowym przebiegu 108,52 s.
- Test obejmuje także spóźnioną odpowiedź `Shift+Delete` po przejściu do innej
  sesji oraz ponowne naciśnięcie lewej strzałki bez drugiego wywołania hosta.
- Kontrakt instalatora i pakowania po poprawce aktualizacji: `42 passed`;
  parser PowerShell bez błędów. Rzeczywista aktualizacja istniejącego AMC
  Public zakończyła się kodem 0, usunęła cztery niedostępne moduły i zachowała
  trzy wymagane moduły sprawdzane w odbiorze.
- Ręczny odbiór mowy i fokusu w NVDA pozostaje do wykonania na zainstalowanym
  kandydacie.

#### Znane ograniczenia

- Pierwszy, niebuforowany odczyt parametrów nowego pliku może nadal trwać tyle,
  ile rzeczywisty pomiar hosta. Poprawka usuwa powtarzanie tego kosztu i
  kumulowanie równoległych zadań, nie fałszuje wyniku szybszym przybliżeniem.
- **Korekta z 12.11.2026, zastępująca wcześniejszą decyzję o wolnym
  `Ctrl+0`:** po dodaniu dziesiątej sesji Apple Music `Ctrl+0` przełącza
  dziesiątą aktywną sesję. Menedżer nadal otwiera `Ctrl+Shift+S`, Historię
  odtwarzania `Ctrl+H`, aktywne nagrania `Alt+R`, a Historię nagrywania
  `Alt+Shift+R`.
- Pliki pozostają w systemowym Koszu do chwili decyzji użytkownika.

#### Wersja i commit

- Numer źródeł: `0.1.0-alpha.428`.
- Poprawka zachowania programu: `5a05f98d50496066606f3176f69a0dd45aea65e1`.
- Fizyczne wycięcie Frontier z paczki publicznej:
  `fa08432ea8db01fcbd80ba53c67d1cba19d6b138`; z tej czystej rewizji powstała
  paczka `90f156c2` z 1075 plikami.
- Poprawka aktualizacji instalatora: `555fbe6ab1b47c528c2aa24a6a3732a8a0cd9e74`.
- Instalator `AMC-0.1.0-alpha.428-win-x64.exe` ma 124 073 632 bajty i SHA-256
  `8f43f0251266953804776fbbf9ebecafeed762837b4f7326fcd72a67b6385581`.
  Plik integralności rozróżnia rewizję paczki i rewizję instalatora.
- Instalacja do `D:\Projekty Codex\AMC Public` zakończyła się kodem 0. Pełna
  integralność oraz kontrolowany start zainstalowanej kopii są zaliczone.
- Widocznego okna Public nie uruchomiono do równoległego odbioru: zwykłe
  żądanie zamknięcia AMC Test zostało odrzucone z komunikatem
  `Nie zamykam AMC Test, ponieważ trwa nagrywanie`. Procesów nie wymuszano,
  nagrania nie przerwano. Po jego zakończeniu należy zamknąć Test i uruchomić
  `D:\Projekty Codex\AMC Public\AMC.exe`.

Źródłowa, zatwierdzona specyfikacja pozostaje w pliku tylko do odczytu:
`D:\Projekty Codex\Accessible Multimedia Controller\AMC12_PROFILE_WYDANIA_PUBLICZNEGO_I_TESTOWEGO_2026-10-18.md`.

## Zakres etapu 1

- Dodano wersjonowany katalog dwóch profili wydania: `test` i `public`.
- Każdy profil ma osobną nazwę użytkową, identyfikator produktu i instalacji,
  kanał aktualizacji, katalog interfejsu, katalog danych rdzenia, pamięć
  podręczną, logi i przestrzeń nazw poświadczeń.
- Obecny AMC Test zachowuje istniejące katalogi, więc ten etap nie przenosi i
  nie zeruje danych użytkownika.
- Publiczny AMC ma od początku osobne katalogi. Funkcjonalnie odpowiada
  wydawanej wersji AMC Test; wyłączone są wyłącznie niedopracowane funkcje
  streamingowe. Sonos oraz każda inna ukończona wspólna sesja pozostają
  dostępne tak samo jak w AMC Test.
- Ten sam model możliwości filtruje kolejność `Ctrl+1–9`/`Ctrl+0`, Menedżer sesji,
  menu, pomoc `Ctrl+F1`, pełną pomoc klawiatury, paletę `Ctrl+Shift+K`,
  wykonanie poleceń sesji i zakres wyszukiwania we wszystkich usługach.
- Próba włączenia sesji niedopuszczonej w profilu jest odrzucana, a stary
  zapis kolejności nie może jej ponownie ujawnić.
- Narzędzie składania paczki przyjmuje `--profile test` albo
  `--profile public` i zapisuje w manifeście pełną tożsamość oraz listę
  możliwości. Odświeżenie istniejącej paczki nie może po cichu zmienić jej
  profilu.
- Interfejs odczytuje profil z jawnego środowiska uruchomieniowego albo z
  wersjonowanego manifestu paczki. Nieznany profil zatrzymuje wybór zamiast
  uruchamiać przypadkowy wariant.

## Materiał do dokumentacji użytkownika

AMC Test i przyszły publiczny AMC powstają z tego samego kodu, ale są dwoma
różnymi wydaniami. **AMC Test jest pełną, codzienną wersją właściciela projektu**
i zachowuje wszystkie sesje prywatne oraz funkcje przeznaczone do prób. Słowo
„Test” określa kanał dystrybucji, a nie niesprawny prototyp. **AMC** jest nazwą
wydania przeznaczonego dla wszystkich.
Publiczny AMC pokazuje te same ukończone funkcje co AMC Test, w tym Sonosa.
Wyłączone są tylko niedopracowane funkcje streamingowe; obecnie dotyczy to
TIDAL-a, Spotify i nieodebranej integracji Frontier Smart. Nie ma ich ani w
Menedżerze sesji, ani w menu, pomocy `Ctrl+F1`, pełnej pomocy klawiatury i
palecie `Ctrl+Shift+K`; ich stare skróty również ich nie uruchomią.

Oba wydania mają osobne ustawienia, logi, pamięć podręczną i dane kont. Samo
uruchomienie jednego wydania nie zmienia danych drugiego. Kopiowanie wybranych
danych z AMC Test do AMC jest osobną, jawną czynnością opartą na wersjonowanej
migawce. AMC Test przygotowuje ostatnią kompletną migawkę przy bezpiecznym
uruchomieniu silnika. Publiczne AMC udostępnia w menu Ustawienia, pełnej pomocy
i palecie polecenia „Odśwież dane z AMC Test” oraz „Stan synchronizacji danych”.
Polecenia pojawiają się tylko wtedy, gdy istnieje drugi profil albo migawka.

Migawka przenosi bibliotekę lokalną, stacje, podcasty i kanały YouTube,
historię wyszukiwania i odtwarzania, zakładki użytkownika, playlisty AMC,
harmonogramy oraz historię nagrywania. Nie kopiuje plików audio — oba profile
mogą wskazywać te same istniejące pliki. Nie przenosi Spotify, TIDAL-u,
poświadczeń, kolejności sesji, ostatniej sesji, bieżącego okna, fokusu ani stanu
trwającego nagrywania.

Przy pierwszym odświeżeniu publiczne AMC nie nadpisuje bez pytania istniejących
danych wspólnych. Przy następnych odświeżeniach porównuje stan z ostatnio
zastosowaną migawką. Jeśli dane publiczne zmieniły się osobno, pyta, czy
przyjąć dane z AMC Test, czy zachować dane publiczne. Po odświeżeniu pozostaje
ten sam widok i, o ile element nadal istnieje, ten sam zaznaczony wiersz.

Publiczny kandydat `alpha.423` został zainstalowany równolegle w osobnym
katalogu `D:\Projekty Codex\AMC Public`. Jego pliki i profil są oddzielone od
AMC Test, a pakiet nie zawiera Spotify, TIDAL-u ani Sonosa. Pierwsze
uruchomienie wykryło jednak, że dodatkowa blokada jednego okna wewnątrz
wxPython nadal miała wspólną nazwę obu profili. Ten kandydat pozostaje tylko
punktem diagnostycznym; przed odbiorem i publikacją zastąpi go instalator z
poprawioną blokadą. Żaden publiczny kandydat nie został jeszcze opublikowany.

AMC Test i publiczny AMC mają osobne blokady uruchomienia, dlatego mogą działać
równolegle. Drugie uruchomienie AMC Test pokazuje tylko okno AMC Test, a drugie
uruchomienie publicznego AMC pokazuje tylko okno AMC. Tak samo rozdzielone są
aktualizatory: paczka jednego wydania nie może zaktualizować drugiego.
Dzienniki uruchamiania trafiają do folderu właściwego profilu, a interfejs
zawsze otrzymuje jawny profil z zatwierdzonego manifestu.

Publiczna paczka nie zawiera dedykowanego hosta Spotify Librespot, jego zestawu
licencji zależności ani modułów interfejsu Spotify i TIDAL. AMC Test nadal
zawiera te moduły. Każdy dołączony plik ma w manifeście dokładny rozmiar i
SHA-256. Przed utworzeniem instalatora oraz po instalacji AMC wykonuje pełną
kontrolę wszystkich plików, a nie tylko plików potrzebnych do startu.

Kontrola sum i rozmiarów nie jest już ostatnim warunkiem wydania. Kandydat musi
jeszcze bez okna uruchomić własny interfejs wxPython, własny host, uzgodnić
protokół i ustawienia audio oraz jawnie zapisać wynik `READY`. Próba używa
jednorazowego profilu, nie odczytuje danych, kont ani poświadczeń użytkownika,
nie rejestruje globalnych skrótów i nie aktywuje już działającego AMC. Limit
czasu oraz kontrola pozostałych procesów zatrzymują wadliwy kandydat.

Nowy instalator ma osobne warianty `AMC-<wersja>-win-x64.exe` i
`AMC-Test-<wersja>-win-x64.exe`, różne identyfikatory produktu, katalogi,
skróty, wpisy systemowe i blokady uruchomienia. Nie wymusza zamknięcia
działającego AMC. Jeśli właściwy profil nadal działa, instalacja zostaje
zatrzymana; program trzeba zamknąć zwykłą drogą, która chroni nagrywanie.
Paczka zawiera prywatny runtime Pythona oraz samowystarczalne procesy .NET i
nie pobiera środowiska uruchomieniowego podczas instalacji.

Po pierwszym uruchomieniu zarówno AMC Test, jak i publiczny AMC sprawdzają w
tle wymagane składniki multimedialne FFmpeg i yt-dlp. Świeży profil domyślnie
pobiera je z oficjalnych, przypiętych źródeł, porównuje sumę SHA-256 podaną
przez dostawcę, sprawdza uruchomienie programu i dopiero wtedy atomowo uaktywnia
nową wersję. Następna automatyczna kontrola następuje najwcześniej po 24
godzinach. Poprzednia działająca wersja pozostaje aktywna, gdy pobranie,
kontrola sumy albo uruchomienie nowej wersji nie powiedzie się.

W menu Pomoc, pełnej pomocy i palecie `Ctrl+Shift+K` znajduje się polecenie
„Sprawdź i zaktualizuj składniki: FFmpeg i yt-dlp”. Nie ma ono osobnego skrótu.
Po ręcznym uruchomieniu NVDA najpierw mówi „Sprawdzanie składników FFmpeg i
yt-dlp”, a na końcu podaje jeden wynik: gotowość, niepełny stan albo konkretny
błąd bez ścieżek technicznych. Automatyczna kontrola pozostaje cicha, gdy nic
nie zmieniła; ogłasza dopiero zainstalowanie składników albo problem wymagający
uwagi.

Binaria FFmpeg i yt-dlp są wspólne dla obu profili, ponieważ są identycznymi,
bezstanowymi narzędziami multimedialnymi, a nie danymi użytkownika. Ustawienia
automatycznego sprawdzania pozostają osobne. Międzyprocesowa bramka zapobiega
jednoczesnej podmianie tego samego składnika przez AMC i AMC Test. Współdzielenie
nie obejmuje ustawień, baz, nagrań, logów ani poświadczeń.

Skrót na pulpicie jest dobrowolnym, domyślnie wyłączonym zadaniem instalatora.
Użytkownik może też zaznaczyć „Nie twórz folderu w menu Start”. Tryb
administracyjny `/NOICONS` ma to samo znaczenie i nie tworzy żadnego z tych
skrótów.

Od 20 października 2026 publiczne AMC można uruchamiać niezależnie od AMC Test.
Oba okna mają osobne procesy, hosty, profile i magazyny. Historyczny kandydat
`0.1.0-alpha.425` nie pokazywał Spotify, TIDAL-a ani Sonosa; wykluczenie Sonosa
zostało zastąpione decyzją użytkownika z 21 października 2026.

Kandydat z 21 października 2026 zawiera także wspólną poprawkę wyszukiwania:
`Ctrl+Shift+L` nie zamyka okna w żadnej sesji ani na świeżym, pustym profilu.
W Radiu NVDA mówi dopiero potwierdzony wynik dodania albo konkretny błąd, bez
nadmiarowej zapowiedzi „Przygotowywanie stacji”.

Publiczne AMC `alpha.426` przy pierwszym uruchomieniu samo tworzy kompletny,
osobny profil: ustawienia, Bibliotekę i bazę Podcastów i YouTube. Dzięki temu
pierwsze `Ctrl+Shift+L` w wynikach wyszukiwania Radia zapisuje stację, a import
playlisty radiowej działa również wtedy, gdy użytkownik nie miał wcześniej
żadnych danych AMC. Program nie kopiuje przy tym ustawień ani kont z AMC Test;
przeniesienie wybranych danych nadal wymaga jawnej migawki.

Następna poprawka Radia zachowuje kolejność wpisów z importowanej playlisty.
Jeśli plik zawiera kolejno „Jedynka”, „Radio Środek” i „Dominikanie Rzeszów”,
te stacje pojawiają się w tej samej kolejności. Ponowny import porządkuje także
stacje już obecne w Bibliotece, więc naprawia listę odwróconą przez starszą
wersję bez potrzeby ręcznego usuwania wpisów. W Bibliotece i Ulubionych Radia
`Alt+1` wybiera najnowsze pozycje na początku, `Alt+2` kolejność alfabetyczną,
a `Alt+3` kolejność własną. `Ctrl+Shift+U` dodaje albo usuwa zaznaczoną stację
z Ulubionych. `Ctrl+Shift+L` dodaje albo usuwa ją z Biblioteki i dopiero po
zapisie NVDA słyszy „Dodano do Biblioteki” albo „Usunięto z Biblioteki”.

## Uzasadnienie i decyzje

- **Korekta użytkownika z 21 października 2026:** odbiór `alpha.425` ujawnił,
  że publiczne AMC na pustym profilu mówiło „brak profilu AMC” po
  `Ctrl+Shift+L`, a import playlisty stacji nie działał. Zastępuje to
  wcześniejszy wniosek, że sam poprawny start pustego profilu dowodzi gotowości
  pierwszych operacji zapisu. Przyczyną nie był routing wyszukiwarki ani brak
  ścieżek w uruchamiaczu: pliki profilu nie istniały jeszcze, a magazyn mutacji
  umiał wyłącznie otworzyć istniejące dane. Host inicjalizuje teraz wszystkie
  trzy magazyny przez tę samą warstwę konfiguracji i aktualny schemat, zanim
  udostępni polecenia. Test wydania musi wykonać co najmniej jedną rzeczywistą
  zmianę danych na całkowicie pustym profilu.

- **Korekta użytkownika z 21 października 2026:** jutrzejszy odbiór ma dotyczyć
  publicznego AMC z tym samym zatwierdzonym kodem wspólnym co AMC Test, ale bez
  prywatnych usług. Dlatego pierwszy instalator z rewizji `8c5ef356` oraz
  pośredni z `bd708f75` zostały zastąpione po kolejnych korektach wyszukiwania.
  Odbiorczym lokalnym kandydatem jest dopiero rewizja `a947f79d`. Sonos nadal
  pozostaje ukryty w profilu publicznym i czeka na osobną decyzję o serwerze.

- **Korekta użytkownika z 20 października 2026:** wadliwy instalator nie może
  zostać zbudowany ani uruchomiony. Sama integralność plików nie wykryła
  wcześniejszego błędu startu publicznego interfejsu, bo wszystkie uszkodzone
  zależności mogły mieć poprawne SHA-256. Dlatego prawdziwy, odizolowany start
  jest obowiązkowy w trzech miejscach: przed przyjęciem paczki z budowy, przed
  kompilacją instalatora i po skopiowaniu plików przez instalator, jeszcze
  przed rejestracją powłoki i zwykłym uruchomieniem programu.
  Lokalna promocja AMC Test stosuje tę samą zasadę dwukrotnie: sprawdza
  kandydata przed atomową podmianą instalacji i zainstalowaną kopię przed
  pokazaniem jej okna. Nie istnieje już boczna droga wdrożenia oparta wyłącznie
  na zgodności manifestu i sum plików.

- **Korekta użytkownika z 20 października 2026:** pełne prywatne wydanie używane
  na co dzień nosi nazwę „AMC Test”, a wydanie dla wszystkich — „AMC”. Zastępuje
  to wcześniejsze robocze określanie bieżącej instalacji nazwą „AMC Python”;
  zgodność ze starym tytułem może pozostać wyłącznie technicznym mechanizmem
  przejściowym. Numer wydania nie jest częścią nazwy produktu.
- Każdy materialnie inny instalator otrzymuje kolejny, nigdy nieużyty numer.
  `alpha.423` pozostaje historycznym kandydatem diagnostycznym, który został
  naprawdę zbudowany i zainstalowany, więc nie wolno nadać tego numeru nowej
  paczce. Następny numer roboczy to `0.1.0-alpha.425`, zgodnie z istniejącym
  planem budowy; zostanie potwierdzony dopiero przy tworzeniu kandydata.
  Kolejne wydania zwiększają numer `alpha.N`, a przejście do `1.0.0` nastąpi po
  odbiorze publicznego instalatora i podstawowych funkcji, nie według samej daty.
- **Korekta użytkownika z 20 października 2026:** instalator i aktualizator
  obu profili — AMC Test i publicznego AMC — mają obejmować również wszystkie
  wymagane biblioteki i składniki potrzebne do działania. Zastępuje to
  wcześniejszy, zbyt wąski opis „pełnej integralności”, który potwierdzał tylko
  pliki zawarte w paczce, mimo że FFmpeg i yt-dlp pozostawały poza nią bez
  podłączonego aktualizatora w nowym interfejsie. Kandydat wydania nie może być
  odtąd nazwany kompletnym bez testu obu warstw: manifestu paczki oraz
  zarządzanych składników multimedialnych.
- Składniki wbudowane — prywatny Python, samowystarczalne procesy .NET i
  biblioteki paczki — są przypięte do wydania i objęte jego manifestem rozmiaru
  oraz SHA-256. FFmpeg i yt-dlp mają niezależny cykl aktualizacji, dlatego są
  pobierane przez istniejące menedżery AMC, weryfikowane sumą dostawcy i
  aktywowane atomowo. Nie udajemy, że suma instalatora obejmuje plik pobrany
  później z sieci.
- Automatyczna kontrola zachowuje dotychczasową politykę ustawień AMC:
  `CheckAutomatically` i `DownloadAutomatically`, z bezpiecznymi wartościami
  włączonymi dla świeżego profilu. Ręczne polecenie zawsze próbuje pobrać
  brakującą lub nowszą wersję i nie zmienia ustawień profilu.
- **Korekta użytkownika z 21 października 2026, zastąpiona 28.10.2026:** publiczny instalator
  ma być dokładną kopią gotowej wersji AMC Test, z wyłączonymi wyłącznie
  martwymi obecnie funkcjami TIDAL i Spotify. Sonos pozostaje w publicznym AMC
  i działa dokładnie tak samo jak w AMC Test. Ta decyzja zastępuje wszystkie
  wcześniejsze zapisy nakazujące wycięcie albo ukrycie Sonosa. Funkcja będąca
  dopiero w trakcie implementacji nie jest jeszcze częścią gotowej wersji Test;
  po ukończeniu i przyjęciu w Test trafia do obu profili z tego samego kodu.
- Kontrola z 21.10.2026 wykazała, że zainstalowany instalator nazwany
  `alpha.426` zawiera `AMC.exe` z metadanymi produktu `0.1.0-alpha.425`.
  Następny instalator nie może już powstać z takim rozjazdem: skrypt przed
  kompilacją porównuje żądaną wersję z `ProductVersion` rzeczywistego launchera
  i zatrzymuje się z jasnym błędem, zamiast nadać paczce mylącą nazwę.
- **Zastąpiona korekta użytkownika z 20 października 2026:** pierwszy publiczny instalator
  ma zawierać wszystkie zatwierdzone wspólne poprawki bieżącego AMC Test,
  w tym `Ctrl+Shift+0`, `End` oraz skoki procentowe Time Shiftu, ale nie może
  udostępniać Spotify, TIDAL-u ani Sonosa. Publiczne pakowanie usuwa teraz także
  moduł interfejsu Sonosa; wspólny host nadal odmawia operacji niedopuszczonej
  przez zamknięty manifest możliwości. Zastępuje to wcześniejszy wariant,
  w którym Sonos był ukryty profilem, lecz jego moduł Python pozostawał w
  paczce w oczekiwaniu na publiczny broker.
- **Korekta użytkownika z 20 października 2026:** nie budujemy kolejnego
  publicznego instalatora po każdej poprawce AMC Test. Istniejący kandydat
  pozostaje lokalnym punktem kontrolnym, a następny publiczny wariant powstanie
  dopiero z zestawu domkniętych i odebranych funkcji. Zastępuje to roboczą
  praktykę natychmiastowego przebudowywania profilu publicznego po małej
  korekcie wspólnego kodu.
- **Korekta wykonawcza z 20 października 2026:** równoległość oznacza dwie
  kompletne, działające instancje, nie tylko osobne procesy uruchamiacza.
  Próba `alpha.423` wykazała, że mutexy C# były rozdzielone, lecz
  `wx.SingleInstanceChecker` nadal łączył AMC z AMC Test. Blokada wxPython
  zawiera teraz tożsamość profilu. Zastępuje to wcześniejszy opis, według
  którego same rozdzielone mutexy uruchamiacza były wystarczającym dowodem.

- Jedna definicja profilu jest bezpieczniejsza niż osobne listy w interfejsie,
  pomocy i pakowaniu. Zapobiega sytuacji, w której ukryta usługa nadal pojawia
  się w palecie albo daje się uruchomić skrótem.
- Profil testowy zachowuje dotychczasowe ścieżki. Zmiana ich w tym etapie
  stworzyłaby niepotrzebną migrację i mogłaby wyglądać jak utrata ustawień.
- Profil publiczny otrzymał nowe katalogi od pierwszego uruchomienia. Nie
  współdzieli żywej bazy, tokenów ani stanu nagrywania z AMC Test.
- Manifest przechowuje celowe identyfikatory i listy wartości modelu. Nazwy
  techniczne nie są używane jako etykiety kontrolek ani komunikaty NVDA.
- Korekta użytkownika z 18–19 października 2026 zastępuje wcześniejszy pomysł
  równoległego używania tych samych żywych magazynów. Dane wspólne mają być
  później przenoszone wyłącznie przez kontrolowaną migawkę AMC Test → AMC.
- Uruchamiacz i aktualizator używają jednego zamkniętego kontraktu profili.
  Manifest może wybrać `test` albo `public`, ale nie może podmienić nazwy,
  katalogów, kanału aktualizacji, listy sesji ani przestrzeni poświadczeń.
- AMC Test zachowuje dotychczasową blokadę uruchamiacza oraz zgodność z dawnym
  tytułem „AMC Python”. Publiczny AMC ma nową blokadę i rozpoznaje wyłącznie
  własny tytuł „AMC”. Ułatwia to przejście bez uruchomienia dwóch kopii starego
  profilu testowego.
- Aktualizator sprawdza profil paczki i istniejącej instalacji przed prośbą o
  zamknięcie programu. Starsza instalacja bez pól profilu może zostać
  jednorazowo rozpoznana wyłącznie jako AMC Test w dotychczasowym folderze
  `AMC Python`; publiczny AMC nie korzysta z tego wyjątku.
- Migawka ma własny format wersji 1 niezależny od schematów `state.json` i
  SQLite. Powstaje przez jawne złożenie dopuszczonych pól; nie jest kopią
  żadnego żywego magazynu.
- Plik jest zapisywany przez plik tymczasowy, opróżnienie bufora i atomową
  podmianę. Przed podmianą host ponownie czyta plik tymczasowy i sprawdza
  SHA-256. Poprzednia poprawna migawka pozostaje jako wersja awaryjna.
- Metadane ostatniego importu są zapisywane dopiero po udanym zapisie obu baz
  i `state.json`. Brak wiarygodnych metadanych przy istniejących danych
  publicznych jest konfliktem, a nie zgodą na ciche nadpisanie.
- Historia wyszukiwania interfejsu wxPython jest przekazywana jawnie przez
  ograniczony kontrakt hosta, ponieważ celowo nie należy do baz C#. Przy
  imporcie wraca także do osobnego magazynu interfejsu publicznego AMC.
- Manifest integralności zawiera teraz zarówno SHA-256, jak i rozmiar każdego
  pliku. Zwykły start nadal sprawdza krytyczne pliki startowe, natomiast jawna
  kontrola instalatora sprawdza całą paczkę strumieniowo.
- Dedykowany binarny host Librespot jest składnikiem profilu testowego. Samo
  ukrycie sesji w interfejsie nie wystarczało, dlatego publiczny proces
  pakowania w ogóle go nie odczytuje ani nie kopiuje.
- Ta sama zasada obejmuje kod interfejsu prywatnych usług. Pakowanie publiczne
  usuwa moduły Spotify, okna playlist Spotify i TIDAL, a wspólny interfejs
  importuje je tylko wtedy, gdy manifest profilu rzeczywiście dopuszcza daną
  sesję. Odświeżenie starszej paczki także usuwa dokładnie te odziedziczone
  pliki, zamiast pozostawiać je poza manifestem.
- Instalator wxPython jest osobnym skryptem od historycznego instalatora WPF.
  Używa zamkniętych identyfikatorów obu profili i `CloseApplications=no`;
  działającego nagrania nie wolno chronić przez wymuszenie zakończenia procesu.
- Instalator odmawia nałożenia nowego AMC na niepusty folder historycznego albo
  obcego programu. Aktualizacja jest dozwolona tylko wtedy, gdy manifest
  istniejącej paczki ma dokładny `installId` bieżącego profilu. Chroni to
  szczególnie domyślny folder `AMC`, który na starszym komputerze może nadal
  należeć do historycznego WPF.
- Skróty nie są bezwarunkowe. Strona folderu menu Start pozostaje dostępna,
  pulpit jest osobnym niezaznaczonym zadaniem, a wszystkie trzy wpisy skrótów
  respektują wybór „bez skrótów”.
- Stałe opisujące wspólne widoki kolekcji nie mogą pochodzić wyłącznie z
  prywatnego adaptera TIDAL. Menu buduje się w każdym profilu, dlatego nazwy
  zakresów są teraz pobierane z zawsze obecnego wspólnego modułu wyszukiwania.
  Zapobiega to awarii startu publicznego AMC bez ponownego dołączania TIDAL-a.

## Zmienione pliki

- `wxlite/amc_wx_lite/release_profile.py`
- `wxlite/amc_wx_lite/state_store.py`
- `wxlite/amc_wx_lite/profile_layout.py`
- `wxlite/amc_wx_lite/version_info.py`
- `wxlite/amc_wx_lite/session_order.py`
- `wxlite/amc_wx_lite/gui.py`
- `wxlite/amc_wx_lite/search_sources.py`
- `wxlite/tools/build_bundle.py`
- `wxlite/tools/Build-AMCBundle.ps1`
- `wxlite/tools/Build-AMCInstaller.ps1`
- `installer/AMC_Wx_Setup.iss`
- `wxlite/tests/test_release_profile.py`
- `wxlite/tests/test_bundle_layout.py`
- `wxlite/tests/test_startup.py`
- `wxlite/tests/test_installer_profiles.py`
- `src/Shared/ReleaseProfileContract.cs`
- `src/AccessibleMediaController.PythonLauncher/AccessibleMediaController.PythonLauncher.csproj`
- `src/AccessibleMediaController.PythonLauncher/BundleContract.cs`
- `src/AccessibleMediaController.PythonLauncher/ProductIdentity.cs`
- `src/AccessibleMediaController.PythonLauncher/ExistingInstancePolicy.cs`
- `src/AccessibleMediaController.PythonLauncher/PythonLaunchPlan.cs`
- `src/AccessibleMediaController.PythonLauncher/Program.cs`
- `src/AccessibleMediaController.PythonUpdater/AccessibleMediaController.PythonUpdater.csproj`
- `src/AccessibleMediaController.PythonUpdater/Program.cs`
- `src/AccessibleMediaController.PythonUpdater/SafeApplicationStop.cs`
- `src/AccessibleMediaController.PythonUpdater/UpdaterInstanceLease.cs`
- `src/AccessibleMediaController.Core/Configuration/CommonDataSnapshotStore.cs`
- `src/AccessibleMediaController.Core/Configuration/ConfigurationStore.cs`
- `src/AccessibleMediaController.LiteHost/LiteCommonDataSnapshotCoordinator.cs`
- `src/AccessibleMediaController.LiteHost/LiteEngineHandlers.cs`
- `src/AccessibleMediaController.LiteHost/LiteProfileMutationStore.cs`
- `src/AccessibleMediaController.LiteHost/LiteRadioLibraryMutation.cs`
- `src/AccessibleMediaController.LiteHost/LiteRadioRecordingCoordinator.cs`
- `src/AccessibleMediaController.LiteHost/Program.cs`
- `src/AccessibleMediaController.LiteHost/LiteFreshProfileInitializer.cs`
- `wxlite/amc_wx_lite/host_client.py`
- `wxlite/amc_wx_lite/menu_model.py`
- `wxlite/amc_wx_lite/shortcuts.py`
- `src/AccessibleMediaController.LiteHost/LiteComponentUpdateCoordinator.cs`
- `src/AccessibleMediaController.LiteHost/Protocol/LiteComponentUpdatePolicy.cs`
- `src/AccessibleMediaController.LiteHost/Program.cs`
- `src/AccessibleMediaController.LiteHost/AccessibleMediaController.LiteHost.csproj`
- `src/AccessibleMediaController.Windows/Services/ComponentUpdateProcessGate.cs`
- `src/AccessibleMediaController.Windows/Services/FfmpegComponentManager.cs`
- `src/AccessibleMediaController.Windows/Services/YtDlpComponentManager.cs`
- `tests/AccessibleMediaController.LiteHost.ProtocolTests/ComponentUpdatePolicyTests.cs`
- `tests/AccessibleMediaController.LiteHost.ProtocolTests/Program.cs`
- `tests/AccessibleMediaController.LiteHost.ProtocolTests/FreshProfileInitializationTests.cs`
- `tests/AccessibleMediaController.LiteHost.ProtocolTests/RadioLibraryMutationTests.cs`
- `wxlite/tests/test_fresh_public_profile_host_e2e.py`
- `wxlite/tests/test_component_updates.py`
- `wxlite/tests/test_host_client.py`
- `wxlite/tests/test_radio_gui_wiring.py`
- `wxlite/tests/test_shortcuts.py`
- `wxlite/tests/test_menu_exposes_existing_actions.py`
- `tests/AccessibleMediaController.Core.SmokeTests/CommonDataSnapshotTests.cs`
- `tests/AccessibleMediaController.Core.SmokeTests/InstallerOfflineContractTests.cs`
- `tests/AccessibleMediaController.Core.SmokeTests/LibrespotHostClientTests.cs`
- `tests/AccessibleMediaController.Core.SmokeTests/Program.cs`
- `tests/AccessibleMediaController.PythonLauncher.Tests/AccessibleMediaController.PythonLauncher.Tests.csproj`
- `tests/AccessibleMediaController.PythonLauncher.Tests/Program.cs`
- `docs/zadania/README.md`
- `docs/zadania/AMC12-profile-wydania-publicznego-i-testowego.md`

## Testy i wynik

- 21.10.2026 publiczna paczka `alpha.428` z czystej rewizji
  `fa08432ea8db01fcbd80ba53c67d1cba19d6b138` ma identyfikator `90f156c2` i
  1075 kontrolowanych plików. Manifest dopuszcza Pliki, Radio,
  Podcasty/YouTube, WiiM i Sonos. Fizycznie nie zawiera modułów TIDAL, Spotify
  ani Frontier; zawiera WiiM, Sonosa i pełną kopię danych.
- Pierwszy instalator prawidłowej paczki pozostawił po aktualizacji stary plik
  Frontier z `alpha.427`. Nie został uznany za wynik końcowy. Poprawka
  `555fbe6a` usuwa wycofane moduły profilu podczas aktualizacji, wymaga czystych
  śledzonych źródeł instalatora i zapisuje ich rewizję. `42/42` testy kontraktu
  instalatora i paczki przeszły.
- Końcowy instalator ma 124 073 632 bajty i SHA-256
  `8f43f0251266953804776fbbf9ebecafeed762837b4f7326fcd72a67b6385581`.
  Aktualizacja rzeczywistego `D:\Projekty Codex\AMC Public` zakończyła się kodem
  0. Niezależny odbiór potwierdził brak czterech niedostępnych modułów, obecność
  WiiM, Sonosa i pełnej kopii danych, kod 0 pełnej integralności oraz poprawny
  kontrolowany start interfejsu z hostem.
- Po zweryfikowaniu następcy usunięto wyłącznie zastąpioną paczkę
  `.test-output/AMC-public-2026.10.21-5a05f98d-alpha428`, która zawierała
  niedostępny Frontier. Odzyskano 425 940 022 bajty, około 406,21 MiB. Artefakt
  nie trafił do Kosza, ale można go odtworzyć z commita `5a05f98d`. Zachowano
  poprawną paczkę `fa08432e`, instalator, zainstalowany program, źródła, dane
  użytkownika i nagrania.
- 21.10.2026 z czystej rewizji
  `3be68067cb033e0fc258df47ab1fa18834db0257` zbudowano publiczną paczkę
  `dc9821e2` z 1075 kontrolowanymi plikami. Profil zawiera sesje Pliki, Radio,
  Podcasty/YouTube, WiiM i Sonos; moduły TIDAL oraz Spotify są nieobecne.
  Pełna kontrola integralności i kontrolowany start interfejsu z hostem
  zakończyły się powodzeniem zarówno przed instalatorem, jak i po instalacji.
- Instalator `AMC-0.1.0-alpha.427-win-x64.exe` ma 124 069 512 bajtów i SHA-256
  `3867110ff00e719b0da5455f2902b67c587d38c1c998da6944e8fe5a890b3d63`.
  Jego wersja jest zgodna z `AMC.exe`: `0.1.0-alpha.427`. Instalacja do
  `D:\Projekty Codex\AMC Public` zakończyła się kodem 0, a kontrola pełnego
  pakietu zainstalowanej kopii także zwróciła 0.
- Ręczny odbiór skrótów i wypowiedzi w rzeczywistym NVDA pozostaje do wykonania
  przez użytkownika; automaty zaliczyły routing klawiatury, wykonanie GUI,
  wspólny model menu/pomocy/palety oraz teksty komunikatów dostępnościowych.

- 21.10.2026, poprawka kolejności i poleceń Radia: kompilacja LiteHost Release
  zakończyła się z **0 ostrzeżeń i 0 błędów**, a wszystkie **24 zestawy** testów
  protokołu przeszły. Rzeczywisty proces hosta na całkowicie świeżym publicznym
  profilu zaimportował kolejno „Jedynkę”, „Radio Środek” i „Dominikanie
  Rzeszów”, zachował tę kolejność widoczną, zapisał sortowanie alfabetyczne,
  usunięcie i ponowne dodanie do Biblioteki oraz dodanie do Ulubionych.
- Izolowany zestaw **151 testów Python** dla skrótów, menu, pomocy, profili,
  pakowania, klienta hosta i pełnego scenariusza publicznego przeszedł bez
  błędu. Sprawdza rzeczywiste wejście klawiatury dla `Alt+1–3`,
  `Ctrl+Shift+U` i `Ctrl+Shift+L`, wspólną drogę wykonania z menu oraz fizyczną
  obecność Sonosa i brak modułów TIDAL/Spotify w syntetycznej paczce publicznej.
- Ujawniona przez szerszy przebieg rozbieżność wspólnego polecenia wyciszenia
  w równolegle rozwijanej sesji Frontier Smart została usunięta. Po połączeniu
  obu przyrostów pełna bramka Python przeszła **1926 testów**, celowo pominęła
  **110 testów środowiskowych** i zaliczyła **11 podtestów**. Wcześniejsze pięć
  błędów importu z uruchomienia bez `pytest` nie było testami produktu i nie
  jest używane jako wynik odbiorczy.
- Pierwsza próba promocji AMC Test z nową sesją Frontier ujawniła, że updater
  wymaga identycznej listy sesji również od manifestu już zainstalowanej,
  starszej wersji. Paczka przeszła testy i odizolowany start, ale updater
  zatrzymał się przed zapisem. Poprawiony kontrakt nadal dokładnie sprawdza
  tożsamość i izolowane katalogi wydania, dopuszczając jedynie starszy podzbiór
  obecnie znanych sesji i możliwości. Zaliczył **29/29 testów
  launchera/updatera** oraz **52/52 testy profili i pakowania**; te 29 testów
  weszło do obowiązkowej bramki promocji.
- Końcowa promocja AMC Test zaliczyła **29/29 testów launchera/updatera**,
  **1927 testów Python**, **110 pominiętych testów środowiskowych** i **11
  podtestów**. Kandydat `82d28f84` oraz stała instalacja przeszły pełną kontrolę
  1669 plików i odizolowany start. Updater, kontrola integralności i kontrolowany
  start zakończyły się powodzeniem, a widoczne okno uruchomiono z
  `D:\Projekty Codex\AMC Python`. Nie zbudowano ani nie zmieniono publicznego
  AMC.

- 21 października 2026 poprawka świeżego profilu przeszła oba ukierunkowane
  zestawy protokołu, a następnie pełne **24/24 zestawy protokołu LiteHost**.
  Kompilacja Release hosta zakończyła się bez ostrzeżeń i błędów.
- Test od końca do końca uruchomił rzeczywisty host Release z nieistniejącymi
  wcześniej katalogami lokalnym i roamingowym. Host utworzył `state.json`,
  `library.db` i `podcasts.db`, dodał jedną stację jak po `Ctrl+Shift+L`,
  zaimportował drugą z M3U i zapisał obie w profilu: **1 test zaliczony**.
  Powtórna inicjalizacja nie zmieniła bajtów żadnego z trzech plików.
- Publiczna paczka z rewizji `15b38eb1` ma identyfikator `83400218`, profil
  `public` i 1073 pliki. Pełna kontrola paczki oraz kontrolowany start przed
  instalatorem i zainstalowanej kopii zakończyły się kodem 0.
- Instalator `AMC-0.1.0-alpha.426-win-x64.exe` ma 124 028 858 bajtów i SHA-256
  `1e21fd2f730df4387da908af7ed6f26509c8af85594edf0c4cac1f7f704fb294`.
  Instalacja do `D:\Projekty Codex\AMC Public` zakończyła się kodem 0.
  Uruchomiona instalacja ma osobny komplet trzech procesów, jawny profil
  `public` oraz wszystkie trzy utworzone magazyny publicznego profilu.
- Przed budową, instalacją i uruchomieniem sprawdzono 15 skonfigurowanych
  folderów nagrań. Wszystkie były dostępne; pomiary 15- i 5-sekundowe wykazały
  0 plików `.amc-partial` i 0 przyrostów. AMC Test nie został zatrzymany.

- 21 października 2026 rewizja `a947f79d` przeszła pełną regresję AMC Test:
  **1910 zaliczonych, 110 pominiętych i 11 podtestów**. Kompilacja hosta,
  uruchamiacza i aktualizatora zakończyła się bez błędów.
- AMC Test został atomowo zaktualizowany do paczki `1dfc80e8`; kontrolowany
  start kandydata i zainstalowanej kopii jest zaliczony, a dokładnie ta wersja
  została uruchomiona ze stałego katalogu `D:\Projekty Codex\AMC Python`.
- Publiczna paczka `5b07f9a1` ma profil `public`, czystą rewizję źródłową
  `a947f79d83f11888e2d1ea353909a1ee50b3ccaa` i 1073 kontrolowane pliki.
  Pełna integralność oraz kontrolowany start kandydata i zainstalowanej kopii
  są zaliczone.
- Instalator `AMC-0.1.0-alpha.425-win-x64.exe` ma 123 980 643 bajty i SHA-256
  `e35545f9be42f3ffe4693772a4b699708322642d58bc0589073f2a710192272c`.
  Niezależny odczyt zgadza się z plikiem integralności. Instalacja do
  `D:\Projekty Codex\AMC Public` zakończyła się kodem 0.
- Bramki przed podmianami sprawdziły 47 włączonych harmonogramów, 0 aktywnych
  okien oraz 15 znanych folderów nagrań: bez `.amc-partial` i bez przyrostu.
  Krótkie procesy `yt-dlp --dump-json --flat-playlist` zostały rozpoznane jako
  wyszukiwanie bez opcji wyjścia, a nie jako nagrywanie.

- Strukturalny kontrakt instalatora i obu skryptów budowy: **11/11**.
- Szersza regresja Python obejmująca instalator, start, wyszukiwanie, skróty,
  menu, pomoc i logikę GUI: **194 zaliczone**.
- Uruchamiacz C#: **27/27**; protokół hosta: **22/22**. Powtórzenie poza
  piaskownicą potwierdziło nazwane kanały IPC i atomowe pliki tymczasowe.
- Pierwsza rzeczywista paczka z nową bramką została odrzucona kodem 4 przed
  instalacją. Wynik `FAILED: HostError: Silnik przestał odpowiadać` ujawnił,
  że świeży profil przekazywał pełną docelową ścieżkę `podcasts.db`, ale host
  dopuszczał tylko już istniejący plik. Bramka nie podmieniła ani nie
  uruchomiła wadliwej wersji. Host tworzy teraz katalog, a warstwa Core tworzy
  pustą bazę z prawidłowym schematem i ustawieniami domyślnymi. Powtórne testy:
  **22/22 Python** oraz wszystkie **22 zestawy protokołu hosta** — zaliczone.
- Druga promocja również zatrzymała się przed instalacją: rozszerzony tekst
  błędu w pomocniczym skrypcie miał błąd składni PowerShell. Po zastąpieniu go
  bezpiecznym formatowaniem parser PowerShell nie zgłasza błędów, a zachowany
  kandydat z poprawionym hostem przeszedł rzeczywisty kontrolowany start
  interfejsu i hosta. Nadal nie utożsamiamy tego z instalacją.

### Wspólna aktualizacja składników obu profili — 20 października 2026

- 57 ukierunkowanych testów Python obejmujących nowe polecenie, menu Pomoc,
  pełną pomoc, paletę wszystkich sesji, wykonanie w tle, pojedynczy komunikat
  końcowy i cichą aktualną kontrolę automatyczną — zaliczone.
- Test protokołu `--component-updates`: wartości domyślne świeżej instalacji,
  osobne wybory profilu, brak zapisu przy odczycie i granica 24 godzin —
  zaliczone.
- Kompilacja Debug `AccessibleMediaController.LiteHost`: zaliczona bez
  ostrzeżeń.
- Kompilacja Debug projektu Windows i jego testów dymnych, które korzystają z
  tych samych menedżerów FFmpeg oraz yt-dlp: zaliczona bez ostrzeżeń. Pierwsza
  próba w ograniczonym środowisku zakończyła się przed kompilacją bez
  diagnostyki; powtórzenie poza tym ograniczeniem zbudowało oba projekty.
- Odczyt rzeczywistej operacji `components.status` ze skompilowanego hosta:
  odpowiedź poprawna i użytkowa; wykazała brak obu zarządzanych składników na
  tym komputerze. Nie wykonywano pobierania ani modyfikacji sieciowej.
- Próba pełnego pomocniczego runnera Python wykonała i zaliczyła 58 testów z
  wybranych modułów, ale sam runner zakończył się błędem importu pięciu innych
  modułów, ponieważ prywatny runtime nie zawiera pakietu `pytest`. Nie jest to
  zaliczenie pełnej regresji ani błąd nowej funkcji; testy ukierunkowane
  uruchomiono następnie bez importowania obcych modułów.
- Nie budowano paczki Release, instalatora ani nie instalowano AMC. Nie
  uruchamiano sieciowej aktualizacji FFmpeg lub yt-dlp i niczego nie
  publikowano.

- Testy profilu, kolejności sesji i manifestu paczki: **35 zaliczonych**.
- Rozszerzona regresja profilu, magazynów, menu, pomocy i palety:
  **162 zaliczone, 4 pominięte**.
- Pierwszy pełny przebieg wykonał **1789 testów poprawnie, 116 pominął i
  zaliczył 11 podtestów**. Jeden test integracyjny nie wystartował, ponieważ
  po wcześniejszym, udanym etapie AMC09 zgodnie z regułą porządku dysku
  usunięto odtwarzalny katalog `LiteHost/bin/Release`; nie był to błąd kodu.
- Pełny przebieg powtórzony z jawnym pominięciem wyłącznie tego testu:
  **1789 zaliczonych, 116 pominiętych, 1 jawnie niewybrany, 11 podtestów
  zaliczonych** w 77,64 s.
- Dla pierwszego fundamentu nie wykonywano kompilacji, instalacji, podmiany
  działającego AMC ani publikacji. Odbiór NVDA profilu publicznego nadal
  wymaga późniejszej, bezpiecznej kompilacji Release i osobnej instalacji.
- Po etapie rozdzielenia uruchamiacza i aktualizatora: **20 testów C#
  zaliczonych**. Obejmują oba prawidłowe profile, odmowę zmienionego manifestu,
  osobne blokady, tytuły okien, prywatny runtime, bezpieczne wskazanie okna,
  procesy instalacji oraz przyjęcie zgodnej i odrzucenie obcej aktualizacji.
- Testy profilu i układu paczki Python po zmianie: **27 zaliczonych**.
- Uruchamiacz Debug i aktualizator Debug: obie kompilacje zaliczone z 0
  ostrzeżeń i 0 błędów. Są to kompilacje odizolowane w repozytorium, nie
  instalacja ani podmiana działającej aplikacji.
- Przed kompilacjami pięciosekundowa bramka wykazała 0 plików
  `.amc-partial` i 0 plików rosnących. Nie zamykano uruchomionego AMC.
- Migawka, profil, menu, pełna pomoc, paleta i klient protokołu Python:
  **157 testów zaliczonych**. Dodatkowa kontrola składni zmienionych modułów
  Python zaliczona.
- Przed planowaną kompilacją nowej warstwy C# kolejne pięciosekundowe bramki
  wykazały **1 rosnący plik `.amc-partial`**. Kompilacji, testów C#,
  instalacji i restartu świadomie nie wykonano; nagranie użytkownika ma
  pierwszeństwo.
- Przy późniejszej bramce nie było żadnego pliku `.amc-partial`. Odizolowany
  test migawki C# zaliczył eksport, filtrowanie danych, atomową podmianę,
  integralność SHA-256, zapis i ponowne otwarcie obu baz SQLite, zachowanie
  prywatnych ustawień, pierwszy konflikt, następny konflikt oraz uszkodzony
  znacznik synchronizacji.
- Host LiteHost Debug zbudował się z **0 ostrzeżeń i 0 błędów**.
- Pierwsze uruchomienie pełnej regresji Core wykryło wyścig w istniejącym
  teście zakończenia osobnego procesu Librespot: błąd polecenia docierał przed
  równoległym zdarzeniem awarii. Test poprawiono tak, by czekał na oba wymagane
  skutki. Powtórzona pełna regresja Core zakończyła się kodem **0**.
- Publiczne pakowanie, profile, instalator i manifest integralności:
  **38 testów Python zaliczonych**. Obejmują pełne syntetyczne złożenie profilu
  publicznego bez odczytu Librespot oraz zgodność rozmiaru i SHA-256 każdego
  pliku.
- Po decyzji o pierwszym wydaniu bez Sonosa ukierunkowane testy profilu i
  układu paczki zakończyły się wynikiem **36 zaliczonych**. Sprawdzają fizyczne
  pominięcie modułów Spotify, TIDAL-u i Sonosa, import publicznego interfejsu
  bez tych modułów, brak ich sesji i możliwości oraz zamknięty kontrakt profilu.
- Pełna regresja wxPython po tej decyzji: **1875 zaliczonych, 110 pominiętych
  i 11 podtestów zaliczonych**. Testy protokołu hosta zaliczyły wszystkie
  **21 zestawów**. Pierwszy i drugi przebieg testów launchera wykrył timeout
  potoku, ponieważ test używał stałego użytkowego kanału profilu i mógł wejść
  w kolizję z inną instancją. Izolowano wyłącznie tożsamość kanału testowego;
  powtórzenie zaliczyło wszystkie **27 przypadków** bez osłabiania kontraktu
  produkcyjnego.
- Pierwsza rzeczywista równoległa instalacja `alpha.423` potwierdziła osobne
  pliki, manifesty i procesy uruchamiacza/Pythona, ale nie uruchomiła drugiego
  hosta i nie utworzyła publicznego magazynu. Przyczyną była wspólna nazwa
  wewnętrznego `SingleInstanceChecker` wxPython. Po poprawce **24 testy
  ukierunkowane zaliczono**; obejmują odrębne nazwy AMC/AMC Test, ochronę
  drugiej instancji tego samego profilu, ścieżki magazynów, publiczny import
  bez prywatnych modułów i syntetyczne złożenie publicznej paczki. Wcześniejszy
  długi przebieg przerwano, ponieważ runner importował wszystkie moduły testów
  i używał niedostępnego katalogu tymczasowego piaskownicy; nie został uznany
  za wynik funkcjonalny.
- Pełna regresja rdzenia uruchomiona jako użytkowy program testowy zakończyła
  się kodem **0**. Obejmuje między innymi Time Shift (`End`, czas i procent),
  nagrywanie, harmonogramy, pliki lokalne, Podcasty i YouTube, WiiM, profile,
  instalator offline, aktualizacje oraz wspólny katalog poleceń.
- Uruchamiacz i aktualizator po dodaniu pełnej kontroli wszystkich plików:
  **22 testy C# zaliczone**.
- Test strukturalny samowystarczalności instalatora zaliczony. Potwierdza trzy
  publikacje `.NET --self-contained true`, prywatny runtime Pythona, brak
  pobierania runtime i brak wymuszonego zamknięcia programu. Nie jest dowodem
  zbudowania ani uruchomienia instalatora.
- Oba nowe skrypty PowerShell przeszły kontrolę składni bez błędów.
- 19.10.2026 pierwsza pełna bramka późniejszej instalacji zbiorczej wykazała,
  że odizolowany test uruchamiania hosta nie przekazywał nowych zależności
  profilu wydania i ścieżki migawki. Uzupełniono atrapę o profil `test` i
  odizolowaną ścieżkę migawki. Był to brak testu, nie próba obejścia jawnego
  kontraktu profili; instalacja została zatrzymana do zielonego powtórzenia.
- Zielone powtórzenie zaliczyło **1830 testów, 110 pominiętych i 11
  podtestów**, a następnie kontrola kandydata zatrzymała instalację kodem 2.
  Wszystkie 92 pliki startowe miały zgodne rozmiary i SHA-256. Przyczyną była
  rozbieżność katalogów możliwości: manifest Python zawierał lokalny formularz
  zgłoszeń, a zamknięty kontrakt launchera .NET jeszcze go nie dopuszczał.
  Kontrakty ujednolicono i dodano automatyczny test pełnej listy możliwości
  obu profili Python–.NET. Kandydat nie został zainstalowany ani uruchomiony.
- Końcowa zbiorcza bramka profilu testowego: **1831 zaliczonych, 110
  pominiętych i 11 podtestów**. Zbudowano 1665 plików, potwierdzono manifest,
  SHA-256, profil `test`, stałą instalację i jedno uruchomione okno AMC Test.
  Nie była to próbna instalacja publicznego AMC; publiczny instalator nadal
  czeka na osobny odbiór.
- Rzeczywista samowystarczalna paczka publicznego AMC Release powstała w
  `.test-output/AMC-public-2026.10.19-bd80db01`. Manifest wskazuje profil
  `public`, czystą rewizję `bd80db01`, 1070 jawnych plików, 1070 rozmiarów i
  1070 sum SHA-256. Niezależna pełna kontrola zakończyła się kodem **0**;
  paczka nie zawiera plików Librespot. Ma 422 452 757 bajtów. Nie uruchamiano
  jej jako aplikacji użytkowej i nie instalowano.
- Inno Setup 6.7.3 skompilował lokalny instalator
  `.test-output/installers/amc12-2026.10.19/AMC-0.1.0-alpha.422-win-x64.exe`.
  Ma 123 320 342 bajty. Ponowne obliczenie SHA-256 dało
  `23f0df0d10910ba9e0e32867ffbf06651226b30f0ee02b1597a0dec969777ea3`
  i jest zgodne z osobnym plikiem integralności. Instalatora nie uruchamiano.
- 20.10.2026 strukturalny kontrakt instalatora zaliczył **7/7** przypadków.
  Inno Setup 6.7.3 skompilował wariant z ochroną istniejącego folderu.
  Próba cicha przeciw projektowemu katalogowi zawierającemu jeden plik
  kontrolny zakończyła się oczekiwanym kodem 7 i komunikatem odmowy; liczba i
  treść plików katalogu nie zmieniły się. Nie wykonywano tej próby przeciw
  rzeczywistemu historycznemu AMC.
- Czysta instalacja publicznego profilu do odizolowanego katalogu zakończyła
  się kodem **0**. Zainstalowano 1079 plików łącznie z deinstalatorem i jego
  danymi; manifest wskazał profil `public`, źródło `863539bd` i paczkę
  `00ab4530`. Ponowna pełna kontrola zainstalowanych 1076 plików zakończyła
  się kodem **0**, a próbne odinstalowanie także kodem **0** i usunęło katalog.
- Próba ujawniła, że `/NOICONS` było ignorowane przez wcześniejsze
  `DisableProgramGroupPage=yes`, przez co próbny instalator utworzył skróty.
  Przed odinstalowaniem rozpoznano oba zainstalowane produkty, a po nim
  odtworzono trzy skróty historycznego AMC; prowadzą ponownie do
  `AccessibleMediaController.exe` i jego deinstalatora.
- Po korekcie dobrowolnych skrótów test strukturalny zaliczył **8/8**
  przypadków, a Inno Setup 6.7.3 ponownie skompilował instalator. Drugiej
  instalacji na głównym koncie nie wykonywano bez dodatkowej zgody, aby po
  wcześniejszym wykryciu kolizji nie ryzykować ponownej zmiany skrótów.
- Końcowa paczka po tej korekcie powstała w
  `.test-output/AMC-public-2026.10.20-20babb7b`. Manifest wskazuje profil
  `public`, czystą rewizję `20babb7ba7b4485211f8da769b37156a5045f7e7`,
  paczkę `29558d7d` i 1076 plików kontrolowanych; katalog zawiera 1077 plików
  razem z manifestem i zajmuje 422 624 783 bajty. Ponowna pełna kontrola
  zakończyła się kodem **0**.
- Końcowy lokalny instalator znajduje się w
  `.test-output/installers/amc12-2026.10.20-20babb7b/AMC-0.1.0-alpha.422-win-x64.exe`.
  Ma 123 354 484 bajty, a jego SHA-256 to
  `3d9e969e3d9a51b4e0ace9f95b06091761970ad2ae31501f3d624e61a237df23`;
  wynik jest zgodny z osobnym plikiem integralności. Ten końcowy wariant został
  zbudowany i sprawdzony, lecz nie został ponownie zainstalowany ani uruchomiony.
- Fizyczne rozdzielenie modułów usług: **44 testy ukierunkowane zaliczone**.
  Obejmują usunięcie starych plików przy odświeżeniu, odmowę niedozwolonego
  manifestu, syntetyczne złożenie profilu publicznego oraz import kompletnego
  interfejsu bez modułów Spotify i TIDAL.
- Pełna regresja wxPython po zmianie: **1856 zaliczonych, 116 pominiętych,
  1 jawnie niewybrany i 11 podtestów zaliczonych**. Jedyny niewybrany test
  wymagał wcześniej zbudowanego `LiteHost` w innej ścieżce; właściwy host został
  następnie zbudowany przez pełny proces paczki.
- Końcowa odchudzona paczka
  `.test-output/AMC-public-2026.10.20-cf6d1e2c` ma 1073 pliki kontrolowane,
  1074 pliki razem z manifestem i 422 575 507 bajtów. Manifest wskazuje czystą
  rewizję `cf6d1e2c65cc99d87217fa01187125b5a3428715`, paczkę `6030732f`, zero
  prywatnych modułów Spotify/TIDAL i zero składników Librespot. Pełna kontrola
  zakończyła się kodem **0**.
- Odpowiadający instalator ma 123 363 456 bajtów i SHA-256
  `65e34d13dd7ff2449f7b3d356b354646cf991d3875c15cf1f434757c3b9673b5`.
  Plik integralności podaje tę samą wartość. Instalatora nie uruchamiano.
- Po wspólnej poprawce granicy klawiatury oraz zachowania nagrania po awarii
  strumienia zbudowano następcę
  `.test-output/AMC-public-2026.10.20-6e1f6bc9`. Manifest wskazuje profil
  `public`, czystą rewizję `6e1f6bc93e0fbcafb22c7a12a127471d325c0442`,
  paczkę `9bc38937`, 1073 pliki kontrolowane i 1074 pliki razem z manifestem.
  Katalog ma 422 602 524 bajty, nie zawiera prywatnych modułów Spotify,
  TIDAL ani Librespot, a pełna kontrola paczki zakończyła się kodem **0**.
- Nowy lokalny instalator znajduje się w
  `.test-output/installers/amc12-2026.10.20-6e1f6bc9/AMC-0.1.0-alpha.422-win-x64.exe`.
  Ma 123 357 916 bajtów i SHA-256
  `58caaa28b367a63f049c0a86ada8061e2940992468195736abce702a402d6680`;
  niezależny odczyt pliku i plik integralności podają tę samą wartość.
  Instalatora nie uruchamiano, nie instalowano i nie publikowano.
- Korekta użytkownika z 20.10.2026: pierwsze publiczne AMC ma zachować
  wspólne sterowanie obecnego AMC Test, w szczególności `Ctrl+Shift+0` dla
  presetu 10, `End` wracający na żywo w odtwarzaczu Radia oraz cyfry `0`–`9`
  skaczące po buforze Time Shift. Wycięcie Spotify, TIDAL i Sonosa nie może
  zmienić tych funkcji.
- Ukierunkowany test profilu publicznego i transportu: **60 zaliczonych**.
  Jawnie potwierdzono obecność sesji Radia i wspólnych możliwości oraz
  rozwiązywanie `Ctrl+Shift+0`, `End` i skoku `5` do 50% bufora. Obejmuje to
  kod i testy, jeszcze nie ręczny odbiór zainstalowanego następnego kandydata
  (roboczo `alpha.425`).
- Czysty, pojedynczy snapshot linii publicznej przeszedł dodatkowo **87
  zaliczonych testów** profili, instalatora, manifestu i pełnych sum plików,
  startu, niezależnych transportów obu wariantów, presetów oraz Menedżera
  sesji. Testy zapisywały dane poza worktree; po przebiegu źródła snapshotu
  pozostały czyste. To nadal test kodu, nie kompilacja ani instalacja.
- Decyzję o nazwach i numeracji sprawdzono względem rejestru artefaktów:
  `alpha.423` ma zapisaną rzeczywistą paczkę, instalator, rozmiar i SHA-256,
  natomiast `alpha.425` występuje jako następny planowany kandydat. Była to
  weryfikacja dokumentacji; kodu, kompilacji, instalacji ani publikacji nie
  wykonywano.
- Bramka wydania `alpha.425` z 20.10.2026 zaliczyła **51 testów Python** profilu,
  instalatora, układu paczki i aktualizatora składników. Osobny zestaw .NET
  `ComponentUpdatePolicyTests` również zaliczono. Pierwsza próba .NET nie
  uruchomiła testu z powodu niedostępnego internetowego audytu NuGet; powtórzenie
  z projektowym `NuGetAudit=false` zakończyło się kodem 0.
- Zbudowano czystą publiczną paczkę z rewizji `215e51e9`: profil `public`, nazwa
  `AMC`, identyfikator paczki `2cc62e59`, 1073 pliki kontrolowane i 1074 pliki
  razem z manifestem, 422 627 178 bajtów. Manifest ma `sourceDirty=false`, a
  kontrola nie znalazła modułów Spotify, TIDAL-u, Sonosa ani Librespot. Pełna
  kontrola paczki zakończyła się kodem 0; uruchamiacz i silnik zgłaszają
  `0.1.0-alpha.425`. Paczka znajduje się w
  `.test-output/AMC-public-2026.10.20-215e51e9-alpha425`.
- Instalator w `.test-output/installers/amc12-2026.10.20-215e51e9-alpha425/`
  pod nazwą `AMC-0.1.0-alpha.425-win-x64.exe` ma 123 378 797 bajtów i SHA-256
  `3a1e9e44cdf6de3521d5a59d20b9481fc3e1555b3fb9f0723b2bc08b7d70abae`.
  Niezależny odczyt pliku oraz manifest integralności są zgodne. Bezpośrednio
  przed instalacją wykryto nowy, rosnący plik `.amc-partial`; bramka
  bezpieczeństwa przerwała operację przed zamknięciem lub podmianą AMC.
  Instalatora nie uruchomiono, istniejących instalacji nie zmieniono i niczego
  nie opublikowano.
- Pierwsza instalacja `alpha.425` uruchomiła okno diagnostyczne zamiast
  interfejsu. Dziennik wykazał `NameError` dla stałej widoku TIDAL używanej
  przez wspólne menu. Po poprawce `0866d668` bezpośredni test importu profilu
  publicznego przeszedł, a nowa paczka `29f384d7` uruchomiła uruchamiacz,
  interfejs Python i host z `--release-profile public`.
- Poprawiony instalator ma 123 991 839 bajtów i SHA-256
  `074479a63e653f5f211db4c49d33312de792301259f552f1ac5e1f14dca1ca70`.
  Instalacja do `D:\Projekty Codex\AMC Public` zakończyła się kodem 0, pełna
  kontrola 1073 plików zakończyła się kodem 0, a wersja uruchomionego
  `AMC.exe` to `0.1.0-alpha.425`. Równocześnie potwierdzono osobny komplet
  trzech procesów AMC Test; żadnego z nich nie zatrzymano podczas instalacji.

## Znane ograniczenia

- Automatyczny test potwierdza dokładnie operacje, które wcześniej zawodziły,
  oraz zainstalowaną kopię `alpha.428`. Pozostaje krótki ręczny odbiór z NVDA:
  `B` dla pliku z folderu, małe zaznaczenie pod `Shift+Delete`, powrót fokusu,
  powtórzona lewa strzałka oraz polecenia Radia i Biblioteki. Dopiero ten odbiór
  zamknie błąd użytkowy; nie jest potrzebny do ponownego tworzenia profilu.

- Bramka sprawdza konstrukcję pełnego okna, start właściwego hosta, `hello` i
  konfigurację audio. Nie zastępuje ręcznych prób każdej funkcji sieciowej,
  logowania usług ani odbioru mowy NVDA.
- Bramka została sprawdzona na rzeczywistej paczce i zainstalowanej kopii obu
  profili. Publiczny instalator przebudowano i zainstalowano lokalnie, ale
  niczego nie opublikowano.

- Pozostaje kontrolowany odbiór rzeczywistego pobrania i aktualizacji FFmpeg
  oraz yt-dlp w obu profilach. Sama równoległa instalacja i praca obu hostów
  została już potwierdzona.
- Pierwsza instalacja offline uruchomi podstawowy interfejs i funkcje
  niewymagające tych narzędzi, ale nie pobierze FFmpeg ani yt-dlp bez sieci.
  Pełny instalator całkowicie offline wymagałby świadomego dołączenia dużego
  pakietu FFmpeg wraz z osobnym przeglądem licencji i rozmiaru; nie jest to
  obecnie deklarowane zachowanie.

- Publiczne pakowanie usuwa dedykowany binarny host Librespot i trzy moduły
  interfejsu prywatnych usług. Wspólny samowystarczalny host .NET nadal powstaje
  z jednego kodu, lecz uruchomiony profil publiczny blokuje niedopuszczone sesje
  i operacje przez manifest możliwości; nie otrzymuje poświadczeń ani osobnego
  hosta Spotify.
- Zainstalowany do próby kandydat `alpha.423` poprzedza poprawkę blokady
  wxPython i nie jest kandydatem do publikacji. Nie należy na jego podstawie
  oceniać równoległego działania silnika; zostanie zastąpiony po nowej
  kompilacji, instalacji i potwierdzeniu dwóch hostów.
- `alpha.425` pozostaje zastąpionym kandydatem, który nie obsługiwał pierwszego
  zapisu na pustym profilu. Zainstalowanym, nieopublikowanym następcą jest
  `alpha.426`.
- Wersjonowana, atomowa migawka została skompilowana i sprawdzona na
  odizolowanych magazynach testowych, ale nie została jeszcze odebrana w
  zainstalowanych AMC Test i AMC. Pierwsza wersja ma stały bezpieczny zakres;
  nie ma jeszcze ekranu ręcznego wyboru pojedynczych kategorii.
- Migawka automatyczna jest obecnie przygotowywana przy uruchomieniu AMC Test.
  Odświeżenie jej po zmianach wykonanych w tej samej długiej sesji oraz
  globalny właściciel wykonywania harmonogramów wymagają następnego podetapu.
- Nie ma jeszcze migracji poświadczeń; zgodnie ze specyfikacją tokeny nie będą
  kopiowane migawką.
- Jednorazowe otwarcie Menedżera sesji na czystej publicznej instalacji czeka
  na etap instalatora.
- Historyczny zapis, według którego Sonos nie miał blokady profilu Public,
  został zastąpiony korektą z 28.10.2026. Sonos pozostaje w AMC Test, ale nie
  jest sesją ani modułem paczki AMC Public.
- Aktualizacja istniejącego publicznego profilu `alpha.423` do `alpha.425`
  przeszła od końca do końca z `/NOICONS`; nadal potrzebny jest osobny odbiór
  zwykłego kreatora na czystym koncie użytkownika.
- Instalator nie jest jeszcze podpisany cyfrowo ani opublikowany. Plik
  integralności potwierdza lokalny wynik budowy, nie zastępuje podpisu kodu.
- Na głównym komputerze domyślny publiczny folder
  `%LOCALAPPDATA%\Programs\AMC` nadal zawiera historyczne AMC WPF. Próba
  odbiorcza nowego publicznego AMC musi użyć osobnego pustego katalogu albo
  nastąpić dopiero po świadomym odinstalowaniu starego programu.

## Porządek dysku

- 21.10.2026 po zweryfikowaniu i zainstalowaniu następcy `a947f79d` usunięto
  dokładnie dwie zastąpione paczki publiczne (`8c5ef356`, `bd708f75`), ich dwa
  katalogi instalatorów oraz dwa zastąpione kandydaty AMC Test (`233767f1`,
  `bd708f75`). Wszystkie sześć celów znajdowało się wyłącznie pod projektowym
  `.test-output`. Usunięto 5724 odtwarzalne pliki i odzyskano 1 385 876 578
  bajtów, około 1,29 GiB. Nie ma odzyskiwania z Kosza; artefakty można ponownie
  zbudować z zapisanych commitów. Zachowano najnowszą paczkę i instalator
  `a947f79d`, zainstalowane profile, źródła, dokumentację, dane użytkownika,
  nagrania oraz zastane katalogi `.test-tmp` o niepewnym pochodzeniu.

- Testy tworzyły pozostałe po nowym module wyłącznie trzy odtwarzalne pliki
  `.pyc`. Po weryfikacji usunięto je dokładnymi ścieżkami spod katalogów
  nadrzędnych `wxlite/amc_wx_lite/__pycache__` i
  `wxlite/tests/__pycache__`.
- Odzyskano 61 685 bajtów. Pliki nie są odzyskiwane z kopii, ponieważ Python
  tworzy je ponownie automatycznie przy kolejnym uruchomieniu testu.
- Pierwszą próbną paczkę z niezapisanej jeszcze poprawki usunięto po
  zweryfikowaniu czystego następcy. Usunięto dokładnie katalog
  `.test-output/AMC-public-2026.10.19-1cc6cc85` spod repozytorium i odzyskano
  422 452 640 bajtów. Był to odtwarzalny artefakt; nie ma potrzeby odzyskiwania.
- Zachowano jedyny czysty kandydat publiczny
  `.test-output/AMC-public-2026.10.19-bd80db01`. Skrypt sam usunął swoje
  jednoznacznie nazwane katalogi `.test-tmp/bundle-build-*`. Nie usunięto
  zastanych `.test-output`, `.test-tmp`, `.pytest_cache`, źródeł, dokumentacji,
  ustawień, nagrań ani zainstalowanej paczki AMC Test.
- Zachowano też jedyny zbudowany instalator i jego plik integralności w
  `.test-output/installers/amc12-2026.10.19`; nie ma jeszcze zweryfikowanej
  kopii zdalnej, więc tych plików nie wolno usuwać.
- Po udanej instalacji zbiorczej usunięto dokładnie odrzucony katalog
  `.test-output/AMC-Python-next-2026.10.19-acb81034e44cc818d27221c143c47f8773a44a74-b90cd4ee`.
  Zawierał niespójny kandydat profilu testowego i nie został zainstalowany.
  Pozostawione okno „Diagnostyka AMC” z tego katalogu zamknięto zwykłym
  żądaniem zamknięcia; działającego AMC Test ze stałej instalacji nie ruszano.
  Odzyskano około 133,45 MiB, oszacowane na podstawie równoważnej poprawnej
  paczki 139 933 384 bajtów. Kandydat był odtwarzalny z commitu `acb81034`,
  lecz jako odrzucony nie wymaga odzyskania. Zachowano poprawnego kandydata
  `fb9ba25f`, instalator publiczny, źródła, ustawienia i dane użytkownika.
- 20.10.2026 po sprawdzeniu następcy usunięto dokładnie dwa zastąpione katalogi
  paczek publicznych, dwa odpowiadające im katalogi instalatorów, dwa próbne
  katalogi kompilacji Inno Setup, katalog kontrolny obcej instalacji oraz dwa
  logi z bieżącego przebiegu. Wszystkie znajdowały się wyłącznie pod
  `.test-output` albo `.test-tmp` tego repozytorium. Odzyskano 1 339 154 013
  bajtów, około 1,25 GiB. Były to odtwarzalne artefakty z rewizji `cdad1bdf`
  i `863539bd`; odzyskanie wymagałoby ich ponownego zbudowania.
- Zachowano końcową paczkę i instalator z rewizji `20babb7b`, wcześniejszy
  jedyny historyczny instalator z 19.10.2026, źródła, dokumentację, ustawienia,
  dane użytkownika, nagrania oraz zastane niepewne katalogi testowe.
- Po zbudowaniu i pełnym sprawdzeniu następcy `cf6d1e2c` usunięto dokładnie
  zastąpioną paczkę `AMC-public-2026.10.20-20babb7b`, odpowiadający jej katalog
  instalatora oraz trzy jawne katalogi tymczasowe pełnej regresji tego etapu.
  Cele znajdowały się wyłącznie pod `.test-output` i `.test-tmp`; odzyskano
  546 252 450 bajtów, około 521 MiB. Artefakty można odtworzyć z commitu
  `20babb7b` albo ponownym uruchomieniem testów.
- Zachowano bieżącą paczkę i instalator `cf6d1e2c`, wcześniejszy instalator z
  19.10.2026, zastane katalogi testowe o niepewnym pochodzeniu oraz mieszane
  katalogi `__pycache__`; nie usunięto źródeł, danych użytkownika ani nagrań.
- Po pełnym sprawdzeniu następcy `6e1f6bc9` usunięto dokładnie zastąpioną
  paczkę `.test-output/AMC-public-2026.10.20-cf6d1e2c` oraz odpowiadający jej
  katalog `.test-output/installers/amc12-2026.10.20-cf6d1e2c`. Usunięto 1076
  plików i odzyskano 545 939 294 bajty, około 520,6 MiB. Były to odtwarzalne
  artefakty z commitu `cf6d1e2c`; lokalne odzyskanie wymaga ponownej budowy.
  Zachowano nową paczkę i instalator `6e1f6bc9`, źródła, dokumentację,
  ustawienia, dane użytkownika, nagrania oraz zastane niepewne katalogi
  testowe.
- Po instalacji i uruchomieniu poprawionego następcy `0866d668` usunięto
  dokładnie wadliwą paczkę
  `.test-output/AMC-public-2026.10.20-215e51e9-alpha425` oraz odpowiadający jej
  katalog instalatora
  `.test-output/installers/amc12-2026.10.20-215e51e9-alpha425`. Usunięto
  546 006 306 bajtów, około 520,7 MiB. Były to odtwarzalne artefakty, których
  interfejs publiczny nie uruchamiał się. Zachowano poprawioną paczkę i jedyny
  aktualny instalator `0866d668-alpha425-fix`, źródła, dokumentację, profile,
  ustawienia, nagrania oraz zastane katalogi testowe o niepewnym pochodzeniu.
- 21.10.2026 po potwierdzeniu integralności i kontrolowanego startu kandydata
  `233767f1` oraz zainstalowanej kopii usunięto dokładnie dwa zastąpione
  katalogi profilu testowego:
  `.test-output/AMC-Python-next-2026.10.21-8b8a472860302bc29c49b71638f63dfeac92ca97-6ec8efcd`
  i
  `.test-output/AMC-Python-next-2026.10.21-7a58e926f555055ebe77dcf88d8c10c2bc209f52-0c73b6fb`.
  Odzyskano 286 012 840 bajtów, około 272,76 MiB. Były to odtwarzalne
  artefakty; można je odzyskać przez ponowną budowę z odpowiadających im
  commitów. Zachowano bieżącego kandydata `233767f1`, źródła, dokumentację,
  ustawienia, dane użytkownika, nagrania i pozostałe katalogi testowe o
  niepewnym pochodzeniu.

## Wersja i commit

- Poprawka kolejności importu, skrótów i zasady „AMC Test minus tylko
  TIDAL/Spotify” została zatwierdzona razem z ukończoną implementacją Frontier
  Smart w commicie `412adf67` — `feat(amc): dodaj Frontier Smart i domknij
  poprawki Radia`. Wersję ustawiono w commicie `3be68067` —
  `chore(release): ustaw wersje alpha427`.
- Zainstalowana kompilacja AMC Test: pełna rewizja
  `63b304449076998a45079f257c6a002ece8ee0b4`, identyfikator zawartości
  `82d28f84`, etykieta
  `2026.10.21-63b304449076998a45079f257c6a002ece8ee0b4-82d28f84`.
- Poprawka pierwszego zapisu na świeżym profilu: `15b38eb1` —
  `fix(amc): inicjalizuj swiezy profil publiczny`.
- Zainstalowany publiczny AMC: `0.1.0-alpha.429`, rewizja paczki i instalatora
  `ed5db3679d046a6861283a7a98952396deef2663`, paczka `cf01ffda`, 1075 plików.
  Instalator i plik integralności pozostają lokalnie w
  `publish\installers\alpha.429`. Instalator ma 124 094 692 bajty i SHA-256
  `09df938029ee66722785ad27a3c3b3f1aadc2bedd21e35f2323bc25d0b01c81b`.
  Zainstalowana kopia z
  `D:\Projekty Codex\AMC Public\AMC.exe` przeszła pełną kontrolę integralności
  oraz kontrolowany start.
- Publikacja: niewykonana. Cloudflare, GitHub i zdalny aktualizator nie były
  częścią tej poprawki.

- Publiczny kandydat `alpha.430`: rewizja
  `462783c56383757819b9aa5df02f6d6ccbbbc826`, paczka `d8eb2766`, 1075 plików.
  Instalator ma 124 077 804 bajty i SHA-256
  `848f3d7ca9e397bb0e10d8ec006a2fcc1faca777d6531031b36215c3e2d45f1b`.
  Instalacja do `D:\Projekty Codex\AMC Public`, pełna integralność, kontrolowany
  start i widoczne uruchomienie są zaliczone. AMC Test nie został podmieniony,
  ponieważ dwukrotnie odrzucił bezpieczne zamknięcie; procesu nie wymuszono.
  Publikacji nie wykonywano.

- Najnowszy AMC Test: rewizja
  `b258fb69fef580f7899c30abd0d48e6b877119be`, paczka `726f0791`, 1668 plików,
  etykieta `2026.10.21-b258fb69fef580f7899c30abd0d48e6b877119be-726f0791`.
  Po bramce 15 katalogów nagrań kandydat i zainstalowana kopia przeszły pełną
  integralność oraz kontrolowany start; program uruchomiono ze stałej ścieżki
  `D:\Projekty Codex\AMC Python\AMC.exe`. W tej promocji nie powtarzano całej
  regresji wxPython: zaliczono 88 testów wyszukiwania i świeżego profilu oraz
  ukierunkowane testy protokołu nowych przyrostów. Publicznego AMC nie
  przebudowano i niczego nie opublikowano.

- Bieżąca wspólna rewizja instalacji: `a947f79d83f11888e2d1ea353909a1ee50b3ccaa`.
- AMC Test: paczka `1dfc80e8`, etykieta
  `2026.10.21-a947f79d83f11888e2d1ea353909a1ee50b3ccaa-1dfc80e8`;
  zainstalowana i uruchomiona.
- Publiczny AMC `0.1.0-alpha.425`: paczka `5b07f9a1`, etykieta
  `2026.10.21-a947f79d83f11888e2d1ea353909a1ee50b3ccaa-5b07f9a1`;
  zainstalowana, pełna kontrola i start izolowany zaliczone, zwykłego okna po
  instalacji nie pozostawiono otwartego, aby nie przerywać bieżącego testu.
- Publikacja: niewykonana. Najnowszy instalator pozostaje lokalnym kandydatem
  do ręcznego odbioru użytkownika.

- Trwała bramka uruchomienia kandydata: `3be15559`; objęcie nią lokalnej
  promocji AMC Test: `8b8a4728`; świeży profil hosta: `7a58e926`; czytelna
  przyczyna odrzucenia: `233767f1`.
- Zainstalowany AMC Test: rewizja `233767f1971349a32bb33468c16bfca19217403e`,
  paczka `0c73b6fb`, 1668 plików, etykieta
  `2026.10.21-233767f1971349a32bb33468c16bfca19217403e-0c73b6fb`.
  Pełna integralność, kontrolowany start kandydata i zainstalowanej kopii oraz
  pojedynczy komplet procesów zostały potwierdzone. Publiczny AMC pozostaje
  `0.1.0-alpha.425`; tego instalatora nie przebudowano i niczego nie
  opublikowano.

- Data pracy: 19–20 października 2026.
- Nazwy kanałów ustalone 20 października 2026: prywatne pełne wydanie „AMC
  Test”, wydanie dla wszystkich „AMC”; następny numer roboczy
  `0.1.0-alpha.425`. Zmiana dotyczy dokumentacji i nie zmienia jeszcze stałych
  wersji, paczki, instalacji ani opublikowanego wydania.
- Ustawienie numeru w metadanych źródeł: `215e51e9` —
  `chore(release): ustaw wersje alpha425`.
- Lokalna linia źródeł `public/release-line`: snapshot `670870d4` z drzewa
  rewizji `215e51e9`; nieopublikowany.
- Pierwszy kandydat publiczny `alpha.425`, paczka `2cc62e59`, został zastąpiony,
  ponieważ próba startu ujawniła zależność wspólnego menu od wyciętego adaptera
  TIDAL. Poprawka i test: `0866d668` —
  `fix(public): uruchom interfejs bez Tidala`.
- Zainstalowany kandydat publiczny `0.1.0-alpha.425`: paczka `29f384d7`, rewizja
  źródłowa `0866d6680adbb26d8cd5b1d54c8a145354dea79c`, instalator
  `AMC-0.1.0-alpha.425-win-x64.exe`, 123 991 839 bajtów, SHA-256
  `074479a63e653f5f211db4c49d33312de792301259f552f1ac5e1f14dca1ca70`.
  Test, kompilacja, instalacja, pełna kontrola i uruchomienie są zaliczone.
  Publikacji nie wykonywano.
- Wspólna, weryfikowana aktualizacja FFmpeg i yt-dlp dla AMC Test oraz
  publicznego AMC, razem z menu, pomocą, paletą i międzyprocesową bramką:
  `31381b2d` — `feat(amc12): aktualizuj skladniki obu profili`. Testy
  ukierunkowane i kompilacje Debug są zaliczone; paczki Release, instalacji,
  rzeczywistego pobrania składników i publikacji nie wykonywano.
- Kod i testy fundamentu: lokalny commit `d2a34ecf`.
- Rozdzielenie uruchamiacza i aktualizatora: `f9a70db8` —
  `feat(amc12): rozdziel launcher i updater profili`.
- Kompilacja: odizolowane kompilacje Debug uruchamiacza i aktualizatora —
  zaliczone; paczki Release nie zbudowano.
- Instalacja: nie wykonywano dla tego etapu.
- Publikacja: nie wykonywano; GitHub nie został użyty.
- Migawka danych wspólnych: `cdf9cb05` —
  `feat(amc12): dodaj kontrolowana migawke danych`; testy i kompilacja
  zaliczone, instalacji nie wykonywano.
- Publiczne pakowanie, pełna integralność i źródło instalatora: `7b586c14` —
  `feat(amc12): dodaj publiczne pakowanie i instalator`; testy zaliczone,
  pliku instalatora `.exe` nie zbudowano z powodu braku Inno Setup.
- Poprawka rzeczywistej budowy: `bd80db01` —
  `fix(amc12): ustabilizuj budowe publicznej paczki`. Kompilacja Release,
  złożenie czystej paczki i pełna kontrola integralności zaliczone; instalacji
  ani publikacji nie wykonywano.
- Wykrywanie użytkowej instalacji Inno Setup: `6571a4de` —
  `fix(amc12): znajdz uzytkowy kompilator instalatora`. Lokalny instalator
  zbudowany, rozmiar i SHA-256 zgodne; instalacji i publikacji nie wykonywano.
- Domknięcie pełnej regresji profili i menu: `acb81034`; zgodność możliwości
  Sygnalisty Python–.NET: `fb9ba25f`.
- Zbiorcza instalacja AMC Test:
  `2026.10.19-fb9ba25fe0680cf7bf5435d53eb5b073de091cca-0debb6aa`;
  publicznego instalatora nadal nie uruchamiano.
- Ochrona istniejącej instalacji: `863539bd` —
  `fix(amc12): nie nadpisuj historycznej instalacji`. Ten wariant przeszedł
  czystą instalację, pełną kontrolę i odinstalowanie w katalogu projektowym;
  nie został wydany publicznie.
- Dobrowolne skróty instalatora: `20babb7b` —
  `fix(amc12): zrob skroty instalatora dobrowolne`. Końcowy wariant został
  skompilowany i sprawdzony przez test strukturalny 8/8 oraz pełną kontrolę
  paczki; jego ponownej instalacji na głównym koncie nie wykonywano.
- Instalacja publiczna: wykonano wyłącznie odizolowaną próbę wariantu
  `863539bd`, po czym go odinstalowano. Końcowy wariant `20babb7b` pozostaje
  lokalnym kandydatem, a nie zainstalowanym wydaniem.
- Publikacja: nie wykonywano; GitHub nie został użyty.
- Fizyczne usunięcie prywatnych modułów z publicznej paczki: `cf6d1e2c` —
  `fix(amc12): usun prywatne moduly z publicznej paczki`. Testy ukierunkowane i
  pełna regresja zaliczone, paczka i instalator z pełną integralnością zbudowane;
  instalacji końcowego wariantu ani publikacji nie wykonywano.
- Aktualny publiczny kandydat: rewizja źródłowa `6e1f6bc9`, paczka `9bc38937`
  i instalator `AMC-0.1.0-alpha.422-win-x64.exe`. Zawiera kod poprawki
  `4d87189e`, obejmującej wspólne mapowanie `F1`–`F24`, nawigację bloku
  numerycznego oraz zachowanie niepustego nagrania po awarii strumienia.
  Paczka, manifest i integralność instalatora są potwierdzone lokalnie;
  instalacji publicznego wariantu ani publikacji nie wykonywano.
- Wykluczenie modułu interfejsu Sonosa z pierwszego publicznego wydania:
  commit `5ff46900`; pełna regresja jest zielona. Nowa paczka, instalator i
  równoległa instalacja odbiorcza pozostają do wykonania. Istniejący kandydat
  `6e1f6bc9` został przez
  tę decyzję zastąpiony jako planowany materiał wydania, ale pozostaje lokalnym
  artefaktem do czasu zweryfikowania następcy.
- Rzeczywista paczka i instalator `alpha.423` z rewizji `3176b7fb`, paczki
  `7ca86660`: zbudowane, integralność potwierdzona i zainstalowane do
  `D:\Projekty Codex\AMC Public`. Instalator ma 123 359 009 bajtów i SHA-256
  `60104491a6fb47f1bf98dd9684945e5c4343f85b96b84bc68f9c4cde25ab3695`.
  Uruchomienie ujawniło kolizję dodatkowej blokady wxPython, dlatego wariant
  nie jest przeznaczony do publikacji.
- Poprawka blokady profilu i test importu rzeczywistej kopii pakietu:
  `9893de59` — `fix(amc12): rozdziel instancje wx profili`; 24 testy
  ukierunkowane zaliczone. Nowy build, instalacja i uruchomienie pozostają do
  wykonania, więc sam commit nie jest jeszcze dowodem naprawionej instalacji.
- Ochrona wspólnych skrótów w profilu publicznym: `d5c16d2b` —
  `test(amc12): chron wspolne skroty profilu publicznego`. Docelowa rewizja
  czystej linii źródeł będzie odświeżona przed budową `alpha.425`.
- Pojedyncza, zanonimizowana linia źródeł `public/release-line` jest lokalna i
  nieopublikowana. Jej końcową rewizję należy zapisać dopiero po ostatnim
  odświeżeniu poprzedzającym budowę.
