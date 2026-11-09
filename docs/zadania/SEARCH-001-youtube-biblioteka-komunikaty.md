# SEARCH-001 — YouTube, Biblioteka i komunikaty wyszukiwania

> Korekta z 9 listopada 2026 r.: opis zapisanej stacji Radia poniżej,
> według którego wynik wyszukiwania tylko mówi „Stacja jest już w Bibliotece”,
> został zastąpiony. Bieżący kontrakt przełączania Biblioteki i Ulubionych
> opisuje `SEARCH-MEMBERSHIP-20261109-wyniki-ulubione-biblioteka.md`.

- Status: `Wdrożone i zainstalowane w AMC Test; czeka na ręczny odbiór NVDA`
- Utworzono: 18 października 2026
- Obszar: wyszukiwanie Podcastów i YouTube, Biblioteka, NVDA i fokus
- Polecenie uruchamiające: `Uruchom zadanie SEARCH-001`

## Zgłoszenie użytkownika

W wynikach wyszukiwania co najmniej dla YouTube nie można dodać kanału do
Biblioteki skrótem `Ctrl+Shift+L`. Dodatkowo po prawej strzałce w wynikach oraz
po `Ctrl+Shift+L` pojawia się ogólny komunikat „AMC Python”, który przesłania
właściwą informację, w szczególności „Dodano do Biblioteki”.

Te dwa objawy należy badać razem, ponieważ mogą wynikać z błędnego routingu
tożsamości wyniku, niepotrzebnego odświeżenia głównego okna albo ponownego
ogłoszenia jego tytułu po zmianie fokusu.

## Oczekiwane zachowanie

1. Na wyniku będącym kanałem YouTube `Ctrl+Shift+L` dodaje lub usuwa właśnie
   ten kanał jako źródło Biblioteki.
2. Na pojedynczym filmie obowiązuje istniejący kontrakt `Media internetowe`;
   nie wolno przypadkiem zamienić filmu w cały kanał ani kanału w pojedynczy
   materiał. Potwierdzenie ma wymienić kolekcję `Media internetowe`, aby było
   wiadomo, gdzie znaleźć zapisany film.
3. Sukces jest ogłaszany dopiero po potwierdzeniu zapisu: „Dodano do
   Biblioteki: <nazwa>” albo „Usunięto z Biblioteki: <nazwa>”.
4. Po potwierdzeniu nie może pojawić się wtórny komunikat „AMC Python”, który
   zagłusza wynik operacji.
5. Prawa strzałka podaje wyłącznie właściwą relację, akcję lub informację dla
   zaznaczonego wyniku. Sam tytuł aplikacji nie jest komunikatem funkcji.
6. Fokus pozostaje na logicznie tym samym wyniku, a Escape wraca do właściwego
   miejsca wyszukiwania.
7. Błąd API, brak obsługiwanej tożsamości albo samo przeładowanie listy nie
   mogą zostać ogłoszone jako dodanie do Biblioteki.

## Zakres sprawdzenia

- wyniki: kanał YouTube, film YouTube, podcast i odcinek podcastu;
- wyszukiwanie globalne i każdy widok korzystający z tego samego modelu wyniku;
- routing po `session_id`, typie i identyfikatorze dostawcy;
- prawa strzałka, `Ctrl+Shift+L`, Enter, Escape i powrót fokusu;
- kolejność komunikatów własnych AMC, zdarzeń fokusu i odczytu NVDA;
- trwałość zapisu po ponownym otwarciu Biblioteki oraz po odświeżeniu źródeł.

## Pomoc i paleta poleceń

Jeżeli naprawa zmieni dostępność albo opis polecenia, ten sam warunek ma zostać
odzwierciedlony w pomocy `Ctrl+F1`, menu kontekstowym i palecie `Ctrl+Shift+K`.
Nie dodajemy drugiego skrótu do tej samej operacji.

## Kryteria akceptacji

1. Test automatyczny wykazuje rzeczywistą zmianę Biblioteki dla kanału, a nie
   tylko obecność tekstu sukcesu.
2. Test rozróżnia kanał od filmu i sprawdza właściwy zapis po przeładowaniu.
3. Test kolejności ogłoszeń nie dopuszcza wtórnego „AMC Python” po właściwym
   komunikacie prawej strzałki lub `Ctrl+Shift+L`.
4. Test z NVDA obejmuje początkowe zaznaczenie, ruch po wynikach, wykonanie
   operacji, zachowanie fokusu i ponowne otwarcie wyszukiwarki.
5. Kontrola regresji obejmuje Podcasty oraz inne usługi używające
   `Ctrl+Shift+L`.

## Wynik wykonania

### Korekta po odbiorze Spotify — 18 października 2026

Użytkownik potwierdził, że w wyszukiwarce Spotify `Ctrl+Shift+L` nie daje
czytelnego skutku, a ogłoszenie nazwy głównego okna nadal może przesłonić
operację. Przyczyna jest analogiczna do pierwotnego błędu YouTube: bezpośrednie
wykonanie bez zamykania wyszukiwarki ograniczono wcześniej tylko do Podcastów
i YouTube. Rozszerzamy ten sam kontrakt fokusu i komunikatów na katalog
Spotify oraz TIDAL. Dla utworu lub odcinka polecenie podaje wprost właściwy
skrót `Ctrl+Shift+U`, zgodnie ze wspólnym kontraktem TIDAL/Spotify; nie może
pozostać bez komunikatu.

- Data rozpoczęcia i wdrożenia: 18 października 2026
- Przyczyna: `SearchDialog` zamykał okno modalne przed wykonaniem
  asynchronicznego zapisu i przed wyborem pojedynczej relacji. Fokus wracał
  wtedy do głównego okna, dlatego NVDA ogłaszał „AMC Python” i przesłaniał
  wynik operacji.
- Rozwiązanie: `Ctrl+Shift+L` dla publicznego wyniku Podcastów i YouTube
  wykonuje zapis bez zamykania wyszukiwarki. Komunikaty przygotowania, sukcesu
  i błędu są kierowane do aktywnego regionu wyszukiwarki. Sukces wymaga flagi
  `added` albo `restored` z hosta. Prawa strzałka zawsze pokazuje relacje w
  drugiej liście tego samego okna, również gdy istnieje tylko jedna relacja.
- Zmienione pliki: `wxlite/amc_wx_lite/gui.py`,
  `wxlite/tests/test_search_gui_navigation.py` oraz niniejszy rejestr.
- Commit implementacji: `e57cfc2480467939c751280fa2d6d64ac7ceaa81`.
- Testy ukierunkowane: `38 passed`.
- Testy regresji pomocy, palety, skrótów i zapisu profilu: `85 passed`.
- Pełny odbiór: `1714 passed, 110 skipped, 11 subtests passed`.
  Dodatkowo wszystkie trzy projekty wykonywalne zostały opublikowane, a
  manifest i sumy SHA-256 paczki oraz instalacji przeszły kontrolę.
- Pomoc i paleta: istniejące polecenie `Ctrl+Shift+L` w `Ctrl+F1`, menu i
  `Ctrl+Shift+K` zostało zachowane i objęte testem regresji. Szczegółowa pomoc
  listy wyników opisuje teraz prawą strzałkę i `Ctrl+Shift+L`.
- Wersja instalacji:
  `2026.10.18-e57cfc2480467939c751280fa2d6d64ac7ceaa81-3dc84e49`.
  Uruchomiona ze stałej instalacji `D:\Projekty Codex\AMC Python\AMC.exe`.
- Do odbioru z NVDA: wyszukać kanał YouTube, nacisnąć prawą strzałkę,
  wrócić lewą, następnie nacisnąć `Ctrl+Shift+L`. Potwierdzić brak
  wtórnego „AMC Python”, pozostanie fokusu na wyniku i obecność kanału po
  ponownym otwarciu Biblioteki.

### Korekta widoczności zapisu YouTube — 18 października 2026

Odczyt implementacji i bieżącej bazy potwierdził dwa niezależne źródła
niejasności:

- zbiorcza Biblioteka podcastów i kanałów jest obecnie czytana według pola
  `ordinal`, czyli w kolejności własnej, a nie alfabetycznie ani według
  ostatniego dodania;
- pojedynczy film YouTube jest prawidłowo zapisywany jako odcinek we wspólnej
  pozycji `Media internetowe`, natomiast wcześniejszy komunikat podawał tylko
  tytuł filmu i nie wskazywał tej pozycji;
- po zapisie z modalnej wyszukiwarki lista Biblioteki pod spodem nie była
  odświeżana ani ustawiana na zwróconym `subscriptionId`.

W bieżącej bibliotece kontrola tylko do odczytu wykazała 302 źródła, a
`Media internetowe` znajdowały się na pozycji 287. To wyjaśnia, dlaczego
poprawnie zapisany film był praktycznie niewidoczny bez filtrowania.

Poprawka:

1. Film otrzymuje komunikat „Dodano do Biblioteki, Media internetowe: nazwa”
   albo „Materiał jest już w Bibliotece, Media internetowe: nazwa”.
2. Po zapisie kanału, podcastu lub filmu widoczna Biblioteka jest odświeżana
   bez dodatkowej mowy i bez odbierania fokusu wyszukiwarce.
3. Zwrócony przez host identyfikator źródła staje się zaznaczeniem listy, więc
   po zamknięciu wyszukiwania użytkownik trafia na dodany kanał albo na
   `Media internetowe`, niezależnie od ich pozycji w kolejności własnej.
4. Osobne sortowanie listy źródeł zapisano jako zadanie `POD-001`.

- Testy ukierunkowane po korekcie: `84 passed`.
- Commit korekty widoczności: `74cab87ec05e2f25e45e253f0fceae93efdcaff3`.
- Pełny odbiór po obu korektach: `1723 passed, 110 skipped, 11 subtests
  passed`; kompilacja projektów .NET zakończona bez ostrzeżeń i błędów.
- Zainstalowana kompilacja:
  `2026.10.18-74cab87ec05e2f25e45e253f0fceae93efdcaff3-a3214a5e`.
  Uruchomiona 18 października 2026 ze stałej instalacji
  `D:\Projekty Codex\AMC Python\AMC.exe`.
- Odbiór ręczny z NVDA pozostaje otwarty: sprawdzić osobno kanał YouTube,
  pojedynczy film kierowany do `Media internetowe` i wynik Spotify.

## Materiał do dokumentacji użytkownika

W oknie wyników wyszukiwania `Ctrl+Shift+L` nie zamyka wyszukiwarki. Dla
kanału YouTube lub podcastu zmienia członkostwo właściwego źródła w
Bibliotece. Pojedynczy film YouTube jest zapisywany w `Media internetowe`, a
ponowne użycie skrótu na zapisanym filmie usuwa tylko ten film. W Spotify i
TIDAL polecenie pozostaje w oknie wyszukiwania i albo wykonuje zmianę właściwej
Biblioteki, albo jasno podaje właściwy skrót dla typu elementu. Po operacji
fokus pozostaje na wyniku; dopiero `Escape` wraca do odświeżonej Biblioteki.

Ta sama zasada obejmuje Radio. Na stacji z publicznego katalogu
`Ctrl+Shift+L` pozostaje na liście wyników i po potwierdzonym zapisie mówi
„Dodano do Biblioteki: nazwa”. Nie poprzedza już tego nadmiarowym komunikatem
„Przygotowywanie stacji”. Jeżeli wyszukiwanie
znalazło stację już zapisaną, podaje „Stacja jest już w Bibliotece: nazwa” i
nie usuwa jej ukradkiem. `Ctrl+L` w oknie wyszukiwania zamyka je i wykonuje to
samo wspólne polecenie Biblioteki co menu, pomoc i paleta.

Od 21 października 2026 zasada pozostania w wyszukiwarce obejmuje wszystkie
sesje: Pliki lokalne, Radio, Podcasty i YouTube, Spotify, TIDAL, WiiM oraz
Sonos. Jeżeli dana sesja nie obsługuje zmiany Biblioteki, użytkownik otrzymuje
krótki komunikat w oknie wyszukiwania, a fokus i zaznaczony wynik pozostają na
miejscu. Wynik lokalny jest już elementem Biblioteki, dlatego skrót niczego
nie usuwa. AMC podaje wtedy, że wpis można usunąć po przejściu `Ctrl+L` do
Biblioteki i użyciu `Delete`; sam plik pozostaje na dysku.

Na świeżym profilu bez zapisanych danych wyszukiwanie nadal jest poprawnym,
pustym katalogiem. Użycie `Ctrl+Shift+L` bez zaznaczonego wyniku mówi
„Najpierw wykonaj wyszukiwanie i wybierz wynik” i zwraca fokus do pola
wyszukiwania zamiast zamykać okno.

Przykładowe komunikaty:

- „Przygotowywanie wyniku katalogu”, a po potwierdzeniu „Dodano do Biblioteki:
  nazwa”;
- „Dodano do Biblioteki, Media internetowe: nazwa” dla pojedynczego filmu;
- „Usunięto z Biblioteki, Media internetowe: nazwa” dla zapisanego filmu;
- w Spotify utwór kieruje do `Ctrl+Shift+U`, ponieważ nie jest albumem,
  wykonawcą ani podcastem Biblioteki.

## Uzasadnienie i decyzje

**Korekta użytkownika z 21 października 2026, druga:** komunikat
„Przygotowywanie stacji” po `Ctrl+Shift+L` w Radiu był nadmiarowy. Operacja
pozostaje widoczna w pasku stanu, ale NVDA otrzymuje dopiero potwierdzony wynik
zapisu albo konkretny błąd. Ta korekta zastępuje wcześniejsze wymaganie dwóch
komunikatów dla dodawania stacji.

**Korekta użytkownika z 21 października 2026:** po naprawie Radia należało
sprawdzić ten sam przypadek na nowym profilu oraz w wyszukiwaniu lokalnym,
Podcastach i YouTube. Audyt wykazał wspólną lukę: pozostanie w modalu było
uzależnione od zamkniętej listy kilku sesji. Pliki lokalne, WiiM i Sonos mogły
więc zamknąć wyszukiwarkę przed podaniem wyniku operacji. Warunek został
zastąpiony wspólną zasadą: każda akcja `Ctrl+Shift+L` jest rozstrzygana w
aktywnym oknie wyszukiwania. Poprawka zastępuje wcześniejsze, wyliczane osobno
warunki dla Radia, Podcastów i YouTube, Spotify oraz TIDAL.

Dla Plików lokalnych nie wprowadzono ukrytego usuwania rekordu z poziomu
wyszukiwania. Byłaby to nowa, destrukcyjna semantyka tego samego skrótu.
Zamiast tego AMC wyjaśnia bezpieczną drogę przez Bibliotekę. `Ctrl+L` pozostaje
jedyną świadomą nawigacją zamykającą wyszukiwarkę.

**Korekta użytkownika z 20 października 2026:** wyszukanie „Polskie radio” i
`Ctrl+Shift+L` gubiło kursor, po czym `Ctrl+L` nie pozwalało wejść do
Biblioteki. Przyczyną było pominięcie sesji Radio w bezpośredniej obsłudze
modalnego okna: skrót zamykał wyszukiwarkę, a operacja przechodziła przez
główne okno. Radio korzysta teraz z tej samej drogi pozostającej w modalu co
Podcasty i YouTube, Spotify oraz TIDAL. Komunikaty asynchronicznego zapisu są
kierowane do aktywnego regionu wyników, a `Ctrl+L` wywołuje wspólną akcję
`Biblioteka` dopiero po zamknięciu modala.

Korekta z 19 października 2026 zastępuje wcześniejsze ograniczenie bezpośredniej
obsługi tylko do wyników mających `remote_kind`. Zapisany wynik o tym samym
adresie zastępował wynik katalogowy podczas scalania, dlatego wcześniejszy kod
zamykał modal, przechodził do zwykłej listy i dopiero tam wykonywał polecenie.
To dokładnie odpowiadało zgłoszeniu: „nic nie mówi, tylko przechodzi na listę
podcastów”. Teraz zarówno zapisane, jak i niezapisane wyniki Podcastów i
YouTube, Spotify oraz TIDAL używają jednej drogi działania wewnątrz okna
wyszukiwania. Jawny parametr `add=True/False` usuwa dwuznaczność dodania i
usunięcia w usługach streamingowych.

Pojedynczy film w `Media internetowe` korzysta z operacji usunięcia pojedynczego
rekordu. Nie wolno przełączyć całego wspólnego źródła i ukryć wszystkich
pozostałych filmów. Podcast, kanał i zwykły odcinek nadal odnoszą się do
rzeczywistego źródła nadrzędnego.

Opis i skrót polecenia w `Ctrl+F1`, pełnej pomocy, menu oraz palecie
`Ctrl+Shift+K` nie zmieniły się; nadal korzystają z istniejącego wpisu wspólnego
katalogu poleceń. Zmieniono routing i warunek wykonania, a test menu potwierdza
brak rozbieżności powierzchni.

## Testy i wynik

- Ponowna kontrola po pytaniu użytkownika o inne wyszukiwarki na świeżym
  profilu: **88/88**. Obejmuje pustą Bibliotekę Plików lokalnych, Radio,
  Podcasty i YouTube, wspólny modal wszystkich sesji, `Ctrl+Shift+L`, `Ctrl+L`,
  fokus pola zapytania oraz start z trzema początkowo nieistniejącymi
  magazynami profilu. Nie znaleziono drugiej odmiany błędu radiowego.

- Korekta wspólna z 21 października 2026: **78/78 testów zaliczonych** dla
  nawigacji wyszukiwarki, katalogu źródeł i startu aplikacji.
- Macierz obejmuje Pliki lokalne, Radio, zapisane i publiczne wyniki Podcastów
  i YouTube, WiiM, Sonos, Spotify oraz TIDAL. Każdy przypadek sprawdza brak
  zamknięcia modala po `Ctrl+Shift+L`.
- Osobne testy potwierdzają poprawny pusty katalog na świeżym profilu, brak
  ostrzeżeń przy pustych bazach, komunikat bez zaznaczenia, powrót fokusu do
  pola zapytania oraz bezpieczny komunikat dla pliku lokalnego.
- Testy uruchomiono w przypiętym środowisku developerskim wxPython 4.3.1.
  Zapakowany runtime instalacji celowo nie zawiera programu `pytest`.

- Korekta radiowa i `Ctrl+L`: ukierunkowany zestaw wyszukiwania, startu hosta i
  instalatora — **75 zaliczonych**; szersza regresja skrótów, menu, pomocy,
  logiki GUI, wyszukiwania i startu — **194 zaliczone**.
- Testy obejmują brak `EndModal` po radiowym `Ctrl+Shift+L`, dokładnie jeden
  zapis stacji, dwa komunikaty w regionie wyszukiwarki, brak użycia announcera
  głównego okna, komunikat dla stacji już zapisanej oraz przejście `Ctrl+L`
  przez wspólną akcję `VIEW_LIBRARY`.
- Zestaw protokołu hosta: **22/22**, zestaw uruchamiacza: **27/27**. Pierwsze
  przebiegi w piaskownicy miały wyłącznie odmowy nazwanych potoków i atomowych
  plików tymczasowych; powtórzenie poza piaskownicą przeszło w całości.

- `wxlite/tests/test_search_gui_navigation.py`: `50 passed`.
- Szersza izolowana regresja wyszukiwania, Spotify, TIDAL, menu, schowka i
  poprawki NVDA: `154 passed`.
- Testy obejmują pozostanie modala otwartego dla wyników zapisanych i
  katalogowych, jawne `add=True/False`, pojedyncze usunięcie filmu z `Media
  internetowe`, zmianę źródła podcastowego, komunikat w aktywnym regionie
  wyszukiwarki oraz zakaz użycia announcera głównego okna.
- Po bezpiecznej bramce nagrywania pełna regresja wxPython zakończyła się
  wynikiem **1754 zaliczone, 110 pominiętych, 11 podtestów**. Paczkę
  zbudowano, sprawdzono manifest i SHA-256, zainstalowano i uruchomiono.
- Nie wykonano jeszcze ręcznego odbioru wyszukiwania z NVDA ani operacji na
  prawdziwym koncie Spotify lub TIDAL.

## Znane ograniczenia

- Automatyczne testy nie zastępują ręcznego odbioru z NVDA na świeżym profilu.
  Po instalacji trzeba sprawdzić otwieranie wyszukiwania, pusty wynik,
  `Ctrl+Shift+L`, `Ctrl+L`, `Escape` i powrót fokusu w obu profilach.

- Korekta Radia i `Ctrl+L` jest zbudowana, zainstalowana i uruchomiona w AMC
  Test. Pozostaje ręczny odbiór fokusu i komunikatów z NVDA.

- Ręczny odbiór na prawdziwym koncie Spotify/TIDAL wymaga obecności użytkownika;
  nocny test nie loguje usług i nie tworzy danych konta.
- Pełny odbiór YouTube wymaga po bezpiecznej instalacji sprawdzenia osobno
  nowego kanału, zapisanego kanału, nowego filmu i zapisanego filmu.
- Sortowanie zbiorczej Biblioteki pozostaje osobnym zadaniem `POD-001`.

## Wersja i commit

- Wspólna korekta wszystkich sesji z 21 października 2026: commit
  `00903317bca522ee0c5c6833a2350ec67d5b9b1c`. Zmieniono
  `wxlite/amc_wx_lite/gui.py`,
  `wxlite/tests/test_search_gui_navigation.py`,
  `wxlite/tests/test_search_catalog.py` oraz niniejszy dokument.
- Pełna bramka po tej korekcie ujawniła nieaktualną atrapę startu silnika;
  naprawił ją osobny commit `bd708f7522f5365d1397595bb98366d1f93403c2`.
  Powtórzony pełny wynik: **1910 zaliczonych, 110 pominiętych i 11 podtestów**.
- Usunięcie zapowiedzi „Przygotowywanie stacji”: commit
  `a947f79d83f11888e2d1ea353909a1ee50b3ccaa`. Dokładnie ten commit przeszedł
  pełną regresję: **1910 zaliczonych, 110 pominiętych i 11 podtestów**.
- Zainstalowany i uruchomiony AMC Test:
  `2026.10.21-b258fb69fef580f7899c30abd0d48e6b877119be-726f0791`.
  Paczka zawiera 1668 plików; integralność i kontrolowany start kandydata oraz
  kopii po atomowej instalacji są zaliczone. Publikacji nie wykonywano.
- Zainstalowany publiczny AMC:
  `2026.10.21-a947f79d83f11888e2d1ea353909a1ee50b3ccaa-5b07f9a1`.
  Pełna kontrola 1073 plików i odizolowany start zainstalowanej kopii są
  zaliczone. Publikacji nie wykonywano; ręczny odbiór NVDA pozostaje otwarty.

- Korekta z 20 października 2026: `3be15559` —
  `fix(amc): utrzymaj wyszukiwanie i profil hosta`.
- Zainstalowana kompilacja:
  `2026.10.21-233767f1971349a32bb33468c16bfca19217403e-0c73b6fb`,
  uruchomiona ze stałej ścieżki `D:\Projekty Codex\AMC Python\AMC.exe`.
  Publikacji nie wykonywano.

- Implementacja i testy: commit
  `1169e6aa17414c0d30c7cf4d3c40e530463ea317`.
- Zmienione pliki:
  `wxlite/amc_wx_lite/gui.py` oraz
  `wxlite/tests/test_search_gui_navigation.py`.
- Kompilacja AMC: Release, zaliczona bez ostrzeżeń i błędów.
- Instalacja AMC:
  `2026.10.19-e0f97512d1eee067d225d7b8c73b808ff3f3e561-c0e62181`;
  manifest, sumy oraz uruchomienie zweryfikowane.
- Publikacja: niewykonana i niedozwolona w nocnym etapie.
