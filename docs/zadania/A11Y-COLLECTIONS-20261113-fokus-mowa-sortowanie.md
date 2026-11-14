# A11Y-COLLECTIONS-20261113 — fokus i mowa po zmianach kolekcji

- Status: **wydane, zainstalowane i uruchomione w AMC Test alpha.498**.
- Nowe zgłoszenie `Ctrl+F` Apple Music z 13.11.2026: **pierwsza poprawka
  i naprawa dalszych stron są w AMC Test alpha.507 zainstalowanym na D
  i uruchomionym 14.11; bez próby NVDA w Apple Music**.
- Utworzono: 13 listopada 2026.
- Obszar: TIDAL, Spotify, lokalne Ulubione, Biblioteka Radia, playlisty,
  kolejki, wyszukiwanie, NVDA.

## Cel

Po dodaniu lub usunięciu elementu AMC ma zachować logiczny fokus, nie
przerywać komunikatu NVDA późnym odczytem pierwszego wiersza i pamiętać tylko
operacje potwierdzone przez usługę. Audyt obejmuje Bibliotekę, Ulubione,
playlisty, kolejki, sortowanie `Alt+1–3`, kategorie `Ctrl+Alt+1–3`, filtr i
wyniki wyszukiwania we wszystkich sesjach.

## Wdrożone zachowanie

- Usunięcie z otwartej kolekcji wybiera następny widoczny element; po usunięciu
  ostatniego wybiera poprzedni. Wiersz „Załaduj więcej” nie może zostać
  wyborem zastępczym.
- TIDAL po `Ctrl+Shift+U` lub `Ctrl+Shift+L` przekazuje sąsiedni element do
  odświeżonego widoku i wypowiada wynik dopiero po zakończeniu natywnej zmiany
  listy. Naprawia to skok fokusu na początek i zagłuszanie ponownego dodania.
- Ten sam wybór sąsiada zastosowano do lokalnych Ulubionych, Biblioteki Radia
  oraz ogólnego `Delete` w Bibliotece, Historii, kolejkach i playlistach.
- Prywatne kolejki TIDAL i Spotify stosują tę samą regułę oraz wypowiadają
  potwierdzenie dopiero po odświeżeniu listy; usunięcie nie przenosi już
  kursora na pierwszy wiersz.
- Spotify zapisuje ostatnio wybrane playlisty wyłącznie dla wyników, których
  pole `failed` wynosi zero. Niepotwierdzony cel częściowej operacji nie wróci
  jako pozornie poprawny wybór.
- W wydanej alpha.498 Apple Music `Ctrl+F` przeszukiwało aktualnie wczytaną, bezpiecznie
  znormalizowaną stronę i po Enterze wraca do tego samego wiersza. Nie udaje
  przeszukiwania całej, niewczytanej biblioteki.
- **Korekta użytkownika z 13.11.2026:** zapis albumu TIDAL z `Ctrl+F` albo
  `Ctrl+Alt+S` dochodził do konta, lecz lista AMC pozostawała stara. Późniejszy
  przyrost unieważnia właściwe strony kolekcji i po zamknięciu wyszukiwarki
  odświeża widoczną listę bez odbierania fokusu i bez drugiej wypowiedzi NVDA.
  Szczegóły: `TIDAL-20261113-odswiezenie-po-zapisie-z-wyszukiwania.md`.

## Uzasadnienie i decyzje

W TIDAL komunikat był wypowiadany przed asynchronicznym odświeżeniem, a po
usunięciu przekazywano brak preferowanego wiersza. Natywna lista wybierała
wtedy pierwszy element i jej późne zdarzenie fokusu przecinało komunikat NVDA.
Spotify miał już częściowo poprawny wzorzec, dlatego wspólną regułę wyboru
sąsiada przeniesiono do jednej funkcji i zastosowano w kolejnych ścieżkach.

Sortowanie Apple Music nie zostało włączone. Adapter pobiera strony po 25
pozycji, więc lokalne `Alt+1–3` sortowałoby tylko fragment i podawało mylący
wynik. Ulubione Apple pozostają wyłączone do czasu potwierdzenia rzeczywistego
API.

**Korekta 13.11.2026:** późniejszy przyrost AM-005 dodaje
dwukierunkowe `Ctrl+Shift+L` wyłącznie dla utworów i albumów. Wykorzystuje ten
sam kontrakt sąsiedniego fokusu i końcowej mowy. Został wydany i zainstalowany
w alpha.499; nie zmienia ograniczenia nowych Ulubionych Apple.

## Materiał do dokumentacji użytkownika

Po usunięciu utworu, stacji lub wpisu z otwartej listy fokus pozostaje w tym
samym miejscu listy: przechodzi na następny element, a na końcu listy na
poprzedni. Końcowy komunikat o dodaniu lub usunięciu jest ostatnią wypowiedzią
NVDA. W wydanej alpha.502 Apple Music `Ctrl+F` obejmuje tylko pozycje już
wczytane w bieżącym widoku; `Ctrl+K` filtruje tę listę bez otwierania
wyszukiwarki. Poniższa korekta źródeł nie jest jeszcze wydana.

## Testy i wynik

- Testy wyboru następnego, poprzedniego i pustej listy oraz pomijania
  „Załaduj więcej”: zaliczone.
- TIDAL: usunięcie środkowego Ulubionego przekazuje następny wiersz i gotowy
  komunikat do odświeżenia: zaliczone.
- Lokalne Ulubione i Biblioteka Radia: testy zapisu, fokusu i komunikatu:
  zaliczone.
- Spotify playlisty: testy tworzenia, potwierdzonych celów i pamięci wyboru:
  zaliczone.
- Kolejki Spotify i TIDAL: następny widoczny wiersz oraz mowa po synchronizacji:
  11/11 testów modułu kolejki zaliczonych.
- Szeroka regresja bez dwóch wcześniej znanych kontroli kompletności języka
  angielskiego: **2724 zaliczone, 117 pominiętych i 34 podtesty zaliczone**.
  Testy GUI Radio Archiwisty, izolowane z powodu konfliktu atrap `wx`: 3/3.
  Dwie kontrole pełnego katalogu angielskiego pozostają czerwone z powodu
  zastanego długu 127 tekstów i pięciu pól Ustawień; nie zostały ukryte ani
  przedstawione jako regresja tej zmiany.

## Znane ograniczenia

- Audyt wykazał dalsze miejsca do ręcznej próby NVDA: wielokrotne nieciągłe
  zaznaczenie z filtrem, szybka zmiana sesji podczas odpowiedzi i kolejki
  wszystkich dostawców.
- Apple Music nie ma jeszcze Ulubionych, sortowania pełnej kolekcji ani
  odtwarzacza. Zapis Biblioteki dla utworów i albumów jest dostępny od
  zainstalowanej alpha.499.
- W wydanej alpha.502 `Ctrl+F` Apple Music nie wykonuje zapytania do Biblioteki
  konta i nie pobiera automatycznie wszystkich stron. Korekta źródeł poniżej
  zmienia pierwsze z tych ograniczeń, ale czeka na próbę w aplikacji.

## Zgłoszenie użytkownika 13.11.2026 — Ctrl+F w Apple Music

Użytkownik potwierdził, że `Ctrl+F` otwiera okno, ale Enter odpowiada brakiem
wyników dla pozycji spoza wczytanej strony. Zgłosił także, że strony po około
25 pozycji i osobny wiersz „Więcej” są zbyt małe. Ta korekta zastępuje
wcześniejsze założenie, że wyszukiwanie wczytanej strony jest wystarczające.
Nie wykonywano próby na żywej aplikacji, ponieważ użytkownik odłożył dalsze
testy Apple Music do czasu, gdy komputer będzie wolny.

Przyczyna w alpha.502: `apple_music_cached_entries` obejmowało wyłącznie
aktualnie wczytane wiersze, a `_run_search_query` nie pytało serwera o
Bibliotekę. Dodatkowo wczytany wiersz miał oznaczenie wyniku zdalnego, co
kierowało jego Enter do niewłaściwej wspólnej obsługi. Gdy fokus jest w polu
filtra listy, `Ctrl+F` nadal nie otwiera wyszukiwarki; użytkownik potwierdził
jednak, że zgłoszenie dotyczy okna, które się otwiera.

Korekta źródeł: `Ctrl+F` w sesji Apple Music odpytuje Bibliotekę konta o
utwory, albumy, wykonawców i playlisty niezależnie od wczytanej strony.
Wynik można otworzyć jako listę Apple Music bez automatycznego odtwarzania.
Wczytany wiersz wraca do właściwej listy. Domyślna strona Biblioteki,
playlisty i albumu wzrosła z 25 do 100 pozycji; dalsza strona jest nadal
dostępna przez „Załaduj więcej”. Pomoc klawiatury nazywa dokładnie zakres
Apple Music. Nazwa w menu i palecie pozostaje wspólnym poleceniem „Szukaj w
bieżącej usłudze”, bo wykonuje tę samą akcję.

Testy izolowane: 172 zaliczone oraz 3 podtesty w modułach adaptera Apple,
wyszukiwania, nawigacji GUI i skrótów; po doprecyzowaniu podpowiedzi okna
99 testów nawigacji wyszukiwania ponownie zaliczonych. Osobny przebieg
nawigacji Apple i menu: 48 zaliczonych, 2 zastane błędy menu osadzonego
odtwarzacza (`APPLE_MUSIC_SHOW_WEB` bez klawisza dostępu); ten plik menu nie
był zmieniany. Nie przeprowadzono próby konta Apple, NVDA, budowy, instalacji
ani publikacji. Wydana alpha.502 nadal ma starą funkcję. Wyszukiwanie
serwerowe zwraca na razie pierwszą grupę dopasowań dla każdego typu; pełne
stronicowanie wyników wymaga późniejszej decyzji.

Następny krok: dokończyć testy jednostkowe, sprawdzić wynik na koncie Apple
i NVDA w uzgodnionym czasie, a dopiero potem budować i wydawać aktualizację
razem z rozwiązaniem usterki ukrytego odtwarzacza.

Commit korekty źródeł i testów: `0b68908d`. Brak nowej wersji, pakietu,
instalacji i wydania GitHub; zainstalowana pozostaje alpha.502.

### Materiał do dokumentacji użytkownika — projekt aktualizacji

W sesji Apple Music `Ctrl+F` otwiera wyszukiwanie osobistej Biblioteki
Apple Music, także wśród utworów niewczytanych jeszcze na bieżącej liście.
Po wybraniu wyniku Enter przenosi do listy Apple Music bez automatycznego
odtwarzania. `Alt+Enter` odczytuje informacje. W widokach Utwory, Albumy,
Playlisty oraz zawartości albumów i playlist jedna strona obejmuje do 100 pozycji;
„Załaduj więcej” pobiera następną. `Ctrl+K` nadal filtruje wyłącznie już
wczytaną listę. Opis dotyczy przygotowanych źródeł i zacznie obowiązywać
po sprawdzonej instalacji, nie w obecnej alpha.502.

## Korekta 14.11.2026 — rozmiar dalszych stron i aktywna instalacja

Powyższe zdanie o „obecnej alpha.502” jest historyczne. Instalacja na
`D:\Projekty Codex\AMC Python` ma manifest z 14.11.2026, 15:03,
commit `1eff98e5` i zawiera pierwszą poprawkę `Ctrl+F` oraz stron po 100.
Jednak proces aktualnie działający na komputerze pochodzi z osobnej starszej
instalacji `C:\Users\micha\AppData\Local\Programs\AMC Test`, manifest
12.11.2026, 23:20, commit `d7563d1a`; nie zawiera nowego wyszukiwania.
Stwierdzono to odczytowo, bez zamykania programu. Użytkownik potwierdził
trwające nagrywanie, więc instalacji ani przełączenia nie wykonywano.

Audyt adaptera wykazał błąd: Apple nie przenosi żądanego limitu do adresu
`next`, przez co po pierwszej stronie 100 kolejna wracała do domyślnego
rozmiaru. Korekta źródeł `7321f39a` przechowuje limit w prywatnym kursorze
i dopisuje go do następnego adresu, zachowując offset. Dopisano kontrolę
niedozwolonego adresu kursora. Testy adaptera: 17 zaliczonych i 3 podtesty.
Ponadto wyniki serwera dostają osobny kontekst filtra od listy Utworów,
z testem zachowania fokusu. Wspólny zestaw Apple Music, wyszukiwania i skrótów:
188 zaliczonych i 3 podtesty. Źródła nie były jeszcze testowane na koncie,
budowane ani instalowane. `Ctrl+F` nadal pokazuje pierwszych 25 dopasowań
każdego typu; stronicowanie wyników wyszukiwania pozostaje do dopracowania.

Materiał do dokumentacji użytkownika — projekt: po sprawdzonej aktualizacji
„Załaduj więcej” będzie pobierać kolejne strony po 100 pozycji, a Enter na
wyniku wyszukiwania otworzy listę bez przenoszenia filtra z poprzedniego
widoku. Nie jest to jeszcze potwierdzone przez NVDA na działającym AMC.

Korekta stanu 14.11.2026, po 17:55: powyższy odczyt starszego procesu C
jest historyczny. AMC Test alpha.507 z commitu `aafa2bc3` jest zainstalowany
i uruchomiony z `D:\Projekty Codex\AMC Python`. Kontrola paczki i cichy start
przeszły, lecz `Ctrl+F` i doładowanie dalszych stron Apple Music nie były
jeszcze sprawdzone z NVDA na żywym koncie.

## Wersja i commit

- Wersja: AMC Test `0.1.0-alpha.498`, użytkowo `13.11.2026, 02:17`.
- Commit źródłowy: `6e34a3fedc78642ab86e83ab16202b707a1293b2`.
- Paczka: `8fce9c2a`, 4256 plików, `sourceDirty=false`; kontrolowany start
  paczki i instalacji zaliczony.
- Wydanie GitHub: prerelease `test-2026.11.13.0217`, trzy załączniki w stanie
  `uploaded`, commit i SHA-256 instalatora zweryfikowane odczytowo.
- Instalacja użytkowa: `D:\Projekty Codex\AMC Python`; uruchomione okno
  potwierdziło tytuł `AMC Test — 13.11.2026, 02:17`.

**Późniejszy stan 13.11.2026:** AM-005 i odświeżanie kolekcji TIDAL wydano w
AMC Test `0.1.0-alpha.499`, użytkowo `13.11.2026, 14:58`, z commita
`7ef8396849eff477e618920dce39392371381e03` i paczki `cbf655af`.

