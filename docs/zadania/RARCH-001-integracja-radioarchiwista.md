# RARCH-001 — sesja Radio Archiwista

- Status: **Kod przejrzany, pakiet i instalator alpha.449 zbudowane; instalacja wstrzymana do wspólnego wydania z poprawką dużych list, odbiór NVDA oczekuje**.
- Data: 29.10.2026; decyzja o wdrożeniu w Test: 30.10.2026.
- Profil: wyłącznie AMC Test (`session.radioarchiwista` w capabilities;
  AMC Public nie widzi sesji, dopóki się nie ustabilizuje).

## Zachowanie widoczne dla użytkownika

- Nowa sesja „Radio Archiwista" przegląda publiczny katalog nagrań
  (radioarchiwista.github.io): 43 stacje, ok. 242 tys. godzin.
- Hierarchia list: stacje → lata → miesiące → dni → godziny nagrań.
  Na górze listy stacji jest „Najnowsze nagrania": ostatnia opublikowana
  godzina ze wszystkich stacji, które ją nagrały.
- Enter na nagraniu odtwarza je przez ten sam tor co podcasty: tempo
  odtwarzania, wznowienie z zapisanej pozycji, obróbka audio. Pauza zapisuje
  pozycję.
- `Ctrl+D` pobiera zaznaczone nagranie (MP3 z archive.org) z postępem
  i możliwością anulowania; przerwane pobieranie nie zostawia pliku.
- `Ctrl+U` pokazuje ulubione nagrania (w pełni offline, zapisane w profilu),
  `Ctrl+Shift+U` przełącza ulubione, `F5` odświeża katalog, `Ctrl+F5`
  otwiera konfigurację sesji.
- Globalne wyszukiwanie AMC dokłada stacje (z ważnego cache) i ulubione
  nagrania; samo wyszukiwanie nigdy nie schodzi do sieci.
- Katalog jest publiczny: bez logowania, bez okna konta.

## Uzasadnienie i decyzje

- 30.10.2026: użytkownik zatwierdził wdrożenie najpierw w swojej wersji AMC Test. Przeniesienie do AMC Public nastąpi dopiero po dobrym odbiorze Test; ta decyzja zastępuje wcześniejszy stan oczekiwania na decyzję właściciela.

- Nagrania to gotowe pliki, więc v1 świadomie nie steruje nagrywaniem
  (to rola serwera archiwizującego); pobieranie jest pełnoprawną funkcją.
- Nawigacja odwzorowuje shardy katalogu (stacja/rok/miesiąc/dzień), więc
  każdy widok pobiera jeden mały plik JSON; `catalog_version` jest sprawdzany
  i nieznana wersja kończy się uczciwą odmową zamiast błędów parsowania.
- Cache w profilu (`lite_settings_dir/radioarchiwista`): lista stacji
  i odwiedzone dni działają offline; wiek danych widać po `published_at`.
- Identyfikatory nagrań są stabilne (`radioarchiwista:{slug}:{utc_started_at}`)
  i zależą wyłącznie od katalogu, dzięki czemu ulubione i pozycje przetrwają
  zmiany kolejności w katalogu.
- Sesja jest rejestrowana dokładnie według wzorca Frontier Smart: akcja,
  allowlisty skrótów, profil wydania, menu, paleta, pomoc `Ctrl+F1`,
  gotowość w Menedżerze sesji. Nowy profil ma sesję wyłączoną; użytkownik
  włącza ją w Menedżerze (`Ctrl+Shift+S`), który przydziela skrót `Ctrl+1–9`.

## Testy i wynik

- Źródło (`test_radioarchiwista_source.py`): parsowanie katalogu
  wersji 2, hierarchia poziomów, etykiety, stabilne id, niepełne nagrania,
  odmowa przy nieznanej wersji, cache i jego wiek, ulubione i pozycje,
  pobieranie z postępem, anulowaniem i odmową przy pustym pliku,
  „Najnowsze nagrania" ostatniej godziny, `search_rows` bez sieci.
- Nawigacja (`test_radioarchiwista_session.py`): przejścia poziomów,
  stos powrotu Backspace ze staniem na opuszczonym wierszu, Enter na
  „Najnowsze", PlayMedia z pozycją i tempem, błąd sieci bez zmiany widoku.
- Kontrakty sesji: skróty, allowlisty, kolejność sesji, profil wydania
  (Public wyklucza sesję do czasu ustabilizowania), menu i mnemoniki,
  paleta, pomoc, i18n PL/EN (w tym szablon „Pobrano nagranie do: {arg0}").
- Wyszukiwanie: agregacja stacji i ulubionych bez sieci, brak źródła
  nie psuje pozostałych wyników.
- Autor PR podał wynik pełnej regresji wxlite: **2094 zaliczone, 41 padnięć — wszystkie
  potwierdzone jako istniejące już na main (baseline worktree), zero
  nowych**; 149 pominiętych wymaga prawdziwego wxPython.
- Zmiany .NET obejmują kontrakt profilu Test, routing transportu i obsługę elementu odtwarzanego w hoście. 30.10.2026: zestaw `--transport-routing` zaliczony, kompilacja LiteHosta Release zaliczona bez ostrzeżeń.

- 30.10.2026: celowane testy pytest sesji, źródła, profili, wyszukiwania, transportu i menu: **155 zaliczonych** w przypiętym środowisku wxPython 4.3.1/Python 3.14. Próba żywego katalogu: HTTP 200, wersja 2, 43 stacje; Program 1, 30.10.2026 — 13 nagrań, bez próby odtwarzania.

## Znane ograniczenia i ręczny odbiór

- Archive.org bywa wolny przy pierwszym otwarciu pliku; `media.play` ma
  limit 120 s na start.
- Nagrania oznaczone statusem innym niż „published" są pokazywane
  i opisane zgodnie z danymi katalogu.
- Do ręcznego odbioru pozostaje: przejście całego drzewka na żywym
  katalogu, odtworzenie z wznowieniem i tempem, pobranie jednego pliku,
  ulubione na żywo, odświeżenie F5, wyszukiwanie globalne i NVDA
  (odczyt wierszy, komunikaty postępu).

## Materiał do dokumentacji użytkownika

> Włącz sesję Radio Archiwista w Menedżerze sesji (Ctrl+Shift+S). Wybierz
> stację, rok, miesiąc i dzień, a Enter odtworzy nagranie z zachowaną
> pozycją i tempem. Ctrl+D zapisze nagranie jako plik MP3, Ctrl+Shift+U
> doda je do ulubionych, a F5 odświeży katalog.

## Wersja i commit

- Integracja bazuje na PR #4 (`b940f133`) z gałęzi Radio Archiwisty; wersja kandydata AMC Test: `0.1.0-alpha.446`. Identyfikator commitu pakietu i wynik instalacji zostaną dopisane po budowie.


## Przegląd kodu i kandydat alpha.449 — 30.10.2026

- Decyzja użytkownika z 30.10.2026 o wznowieniu i wdrożeniu zastąpiła
  wstrzymanie podczas równoległych poprawek Michała Dziwisza. Wydanie Test
  alpha.449 bazuje na faktycznie zainstalowanym alpha.448 (`5e474b92`),
  więc zachowuje zmiany Huberta z PR #7 i #8. Profil Public nadal nie
  udostępnia sesji Radio Archiwista.
- Przegląd znalazł i usunął: błędne F5 w „Najnowszych nagraniach”, gubienie
  zakończenia pobierania podczas nawigacji, nadpisywanie plików o tej samej
  etykiecie godziny, brak postępu i anulowania w GUI, pozostawianie
  usuniętego nagrania w Ulubionych, martwe Ctrl+F5, niezgodny zestaw
  poleceń lokalnego odtwarzacza oraz menu oferujące pobieranie na stacji.
- Polecenia: F5 odświeża bieżący widok; Ctrl+F5 otwiera adres katalogu
  HTTPS; Ctrl+U otwiera Ulubione; Ctrl+Shift+U przełącza ulubione nagranie;
  Ctrl+D pobiera zaznaczone nagranie. Menu, paleta, pełna pomoc i pomoc
  kontekstowa korzystają z tych samych akcji. Pobieranie ma standardowe
  okno postępu z przyciskiem Anuluj; sukces, odmowa i anulowanie są
  oznajmiane. Nazwa pliku zawiera stację i stabilny skrót identyfikatora,
  a ponowne pobranie tworzy kolejny plik.
- Testy czystej integracji na bazie alpha.448: 283 zaliczone, 1 pominięty
  (źródło, sesja, GUI bez okna, skróty, menu, i18n, szybkie listy, nagrywanie
  radia i pobieranie podcastów). W głównym repo pełna regresja przed
  poprawką szablonu i18n dała 2202 zaliczone, 114 pominiętych i 7 błędów;
  2 błędy i18n usunięto i ponownie sprawdzono, pozostałe 5 dotyczy
  aktualizatora/startu i opisu podcastów, niezwiązanych z Radio Archiwistą.
- Wersja: `0.1.0-alpha.449`. Commit kodu na bazie alpha.448:
  `511999f1`. Commit kandydata, budowa, instalator, instalacja i wynik
  testu NVDA zostaną dopisane po wykonaniu tych etapów; żadnego z nich
  nie należy wywodzić z samych testów źródeł.


## Zmiana planu wydania — 30.10.2026

- Po zbudowaniu osobnego kandydata alpha.449 otrzymano informację z równoległego
  zadania o decyzji użytkownika, aby dołączyć poprawkę dużych list do jednego
  wydania AMC Test. Ta decyzja zastępuje wcześniejszy plan osobnej instalacji
  alpha.449. Nie uruchomiono instalatora i nie opublikowano wydania; działające
  AMC Test pozostawiono bez zmian.
- Pakiet Test z czystego commitu `dcaf53f3214c1a447ca6f87757e4926a293acb5e`
  przeszedł kompilację .NET, sprawdzenie integralności i kontrolowany start
  interfejsu oraz hosta. Instalator ma 133619010 bajtów i SHA-256
  `f7f209714b82547030bf2fbdb6fda6b8c4c1219fee81bbede377a55ae583c48f`.
- Pełna regresja z dokładnego commitu: 2172 zaliczone, 150 pominiętych,
  7 błędów. Dwa błędy wynikały z brakującego standardowego artefaktu hosta
  Release; po jego zbudowaniu oba testy przeszły. Pozostałe pięć dotyczy
  starszych testów aktualizatora, opisu podcastów i atrap startu.
- Kandydat i szkic listy zmian leżą w
  `D:\Projekty Codex\AMC aktualizacje\AMC Test 0.1.0-alpha.449 Radio Archiwista 2026-10-30`.
  Są zachowane do porównania; wspólne wydanie wymaga nowego commitu, nowej
  wersji, testów, budowy, instalacji i odbioru NVDA.

## Wspólny kandydat alpha.450 — 30.10.2026

- Gałąź `codex/amc-test-radioarchiwista-large-lists-alpha450` łączy kod
  Radio Archiwisty z poprawką dużych list lokalnych (`a814e65a`). Wersja
  techniczna to `0.1.0-alpha.450`; profil Public pozostaje bez tej sesji.
- 210 celowanych testów obu funkcji i powiązanej nawigacji zaliczonych.
  Paczka z commitu `481302de6d810d510578c7782365479c23a745c7` przeszła
  kompilację, pełną kontrolę integralności i kontrolowany start.
- Instalator w `D:\Projekty Codex\AMC aktualizacje\AMC Test 0.1.0-alpha.450 Radio Archiwista duze listy 2026-10-30\instalator` ma
  133629195 bajtów i SHA-256
  `c88a44003acf913c9b9275d0c73796242fc57c936b25dab0e0021ce0c6994539`.
  Instalacja, publikacja i odbiór NVDA pozostają do wykonania.

### Wynik wydania alpha.450 — 30.10.2026

- Na późniejsze polecenie użytkownika wykonano bezpieczną aktualizację
  działającego AMC Test. Aktualizator poprosił aplikację o zwykłe zamknięcie;
  nagrywania nie przerwano wymuszeniem. Windows rejestruje wersję
  `0.1.0-alpha.450`, a zainstalowany manifest potwierdza commit paczki
  `481302de6d810d510578c7782365479c23a745c7`. Uruchomiono jedną nową
  instancję z prywatnym runtime i hostem.
- Opublikowano GitHub prerelease `test-2026.10.30.2036` z instalatorem,
  plikiem integralności i `amc-update-test.json`. Zdalny instalator ma
  właściwy rozmiar i SHA-256. Gałąź źródłowa wskazuje `10622a22`.
- Odbiór rzeczywistej mowy NVDA i prób na żywym katalogu pozostają do
  wykonania przez użytkownika; kontrolowany start nie zastępuje tych prób.
