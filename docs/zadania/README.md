# Rejestr zadań AMC

Korekta kandydata Public z 30.10.2026: [PUBLIC-20261030-uzupelnienie-poprawek-huberta.md](PUBLIC-20261030-uzupelnienie-poprawek-huberta.md). Obejmuje dodatkowe PR #7 i #8; zgłoszenie #5 ma osobne zadanie do następnej aktualizacji z Radioarchiwistą.

Ten katalog jest kanonicznym miejscem zapisywania osobnych zadań, które mają
przetrwać między rozmowami i być czytelne zarówno dla Codexa, jak i Hermesa.
Rejestr nie uruchamia pracy samodzielnie. Zadanie rozpoczyna się dopiero po
jawnym poleceniu użytkownika albo przez osobno uzgodniony harmonogram.

## Bieżący punkt wznowienia

Najnowszą bazą wszystkich dalszych prac jest AMC Test z commitu
`4c8fceda738afd0163040a400150007d16f8409c`, pakiet `faef4e7f`, zbudowany,
atomowo zainstalowany i uruchomiony 26.10.2026 o 14:03 w
`D:\Projekty Codex\AMC Python`. Raport promocji potwierdza 2062 zaliczone
testy, 111 kontrolowanie pominiętych i 13 zaliczonych podtestów oraz tytuł
okna `AMC Test — 26.10.2026, 14:01`. Pozostaje ręczny odbiór funkcji z
czterech najnowszych zadań.

Nadrzędny plan uporządkowania jednej linii źródeł, instalatorów, aktualizacji,
GitHuba, profili danych oraz historycznego AMC WPF jest w:
[REL-002-kanoniczne-wydania-test-public-github.md](REL-002-kanoniczne-wydania-test-public-github.md).
Nie budować ani nie instalować kolejnej użytkowej wersji ze starego repozytorium
WPF `D:\Projekty Codex\Accessible Multimedia Controller`.
Stara instalacja WPF na dysku C została usunięta; repozytorium źródłowe pozostaje
do kontrolowanej archiwizacji opisanej w REL-003.

## Zasady prowadzenia

- Każde większe zadanie ma stały identyfikator i osobny plik o nazwie
  `<OBSZAR>-<NUMER>-<krotka-nazwa>.md`.
- Dozwolone statusy: `Oczekuje`, `W trakcie`, `Zablokowane`, `Zrobione`.
- Przed pracą przeczytaj ten rejestr, właściwy plik zadania, `AGENTS.md` oraz
  dokumenty wskazane przez zadanie.
- Nie rozpoczynaj pozostałych wpisów tylko dlatego, że znajdują się w rejestrze.
- Przy rozpoczęciu zmień status w pliku zadania i tutaj na `W trakcie`.
- Po zakończeniu zapisz w pliku zadania datę, wynik, zmienione pliki, commit,
  uruchomione testy z rezultatami i dokładną wersję instalacji, jeśli powstała.
  Dopiero po zweryfikowaniu kryteriów akceptacji ustaw status `Zrobione`.
- W każdym rozpoczętym zadaniu prowadź również sekcje `Materiał do dokumentacji
  użytkownika`, `Uzasadnienie i decyzje`, `Testy i wynik`, `Znane ograniczenia`
  oraz `Wersja i commit`. Tekst użytkowy ma być gotowy do automatycznego
  złożenia instrukcji i opisu wydania, ale pozostaje projektem do korekty i
  zatwierdzenia użytkownika.
- Korekty użytkownika zapisuj z datą i oznaczaj, które wcześniejsze ustalenie
  zastępują. Nie utrzymuj dwóch sprzecznych wariantów jako równoważnych.
- Jeżeli praca utknęła, ustaw `Zablokowane` i opisz konkretną przeszkodę oraz
  najkrótszy sposób wznowienia.
- Nie zapisuj tu haseł, tokenów, danych kont ani prywatnych logów użytkownika.
- Korekta użytkownika z 27.10.2026: domyślnie stosuj celowane testy bez
  widocznego interfejsu. Pełną regresję GUI, Sonosa lub WiiM, testy otwierające
  okna, przejmujące fokus albo schowek uruchamiaj dopiero na wyraźną prośbę;
  przed proponowanym testem wydaniowym najpierw opisz wpływ i uzyskaj zgodę.

## Zadania

- `PERF-001` — **W trakcie; PR #6 Huberta zastosowany lokalnie, 65/65 testów celowanych zaliczonych, bez commitu, builda, instalacji i publikacji** — pominięcie zbędnego przeliczania dużych list; osobny projekt dla przełączania widoku i Escape z odtwarzacza ze zgłoszenia #5. Szczegóły: [PERF-001-duze-listy-hubert.md](PERF-001-duze-listy-hubert.md).

- `PERF-002` — **W trakcie; izolowany kandydat źródłowy, testy bez okien i próba rzeczywistej kontrolki zaliczone; czeka na odbiór NVDA, bez instalacji i publikacji** — duża lista Plików lokalnych i powrót Escape ze zgłoszenia #5. Szczegóły: [PERF-002-duze-biblioteki-plikow.md](PERF-002-duze-biblioteki-plikow.md).

- `PODRAD-001` — **W trakcie; dwa domyślnie wyłączone przełączniki osobnych folderów Radia i Podcastów, celowane testy oraz build hosta zaliczone; bez instalacji i próby NVDA**. Szczegóły:
  [PODRAD-001-osobne-foldery-stacji-i-podcastow.md](PODRAD-001-osobne-foldery-stacji-i-podcastow.md).

- `RARCH-001` — **W trakcie; poprawki po pierwszym odbiorze przygotowane
  do osobnego wydania AMC Test 0.1.0-alpha.451; potrzebny odbiór NVDA;
  kod, testy i pull request
  [michalkasperczak/AMC#4](https://github.com/michalkasperczak/AMC/pull/4)
  (gałąź `radioarchiwista-sesja`, autor radioarchiwista) są gotowe;
  pozostaje odbiór właściciela i ręczny odbiór NVDA na AMC Test** —
  sesja „Radio Archiwista” wyłącznie w profilu Test: przeglądanie
  publicznego katalogu nagrań (stacja → rok → miesiąc → dzień → godzina,
  „Najnowsze nagrania”), odtwarzanie wspólnym torem podcastów (tempo,
  wznowienie pozycji), pobieranie MP3 `Ctrl+D`, ulubione `Ctrl+U`/
  `Ctrl+Shift+U`, odświeżanie `F5`, globalne wyszukiwanie bez sieci.
  Bez sterowania nagrywaniem. Szczegóły:
  [RARCH-001-integracja-radioarchiwista.md](RARCH-001-integracja-radioarchiwista.md).

- `DOWNLOADS-001` — **AMC Test `alpha.455` opublikowany na GitHubie i
  zainstalowany; 2211 zaliczonych testów, pozostaje ręczny odbiór NVDA** —
  historia pobranych plików Radio Archiwisty, Podcastów i wideo pod `Ctrl+J`.
  Szczegóły: [DOWNLOADS-001-historia-pobierania.md](DOWNLOADS-001-historia-pobierania.md).

- `DOWNLOADS-002` — **AMC Test `alpha.456` opublikowany na GitHubie i
  zainstalowany; 2215 testów zaliczonych, odbiór NVDA oczekuje** — w Historii
  pobrań `Ctrl+C` kopiuje ścieżkę, a
  `Ctrl+Shift+C` plik do wklejenia. Szczegóły:
  [DOWNLOADS-002-schowek-historii-pobran.md](DOWNLOADS-002-schowek-historii-pobran.md).

- `QUEUE-002` — **Oczekuje na osobny przegląd** — kolejki Podcastów i YouTube,
  Spotify oraz TIDAL nie wchodzą do poprawki Radio Archiwisty. Szczegóły:
  [QUEUE-002-przeglad-kolejek-sesji-internetowych.md](QUEUE-002-przeglad-kolejek-sesji-internetowych.md).

- `EDIT-001` — **Zrobione w źródłach; 3 testy kontraktu, celowany test
  rzeczywistego wycinania oraz kompilacja LiteHosta są zaliczone; poprawka nie
  została jeszcze zainstalowana ani wydana** —
  wycinanie z odtwarzanego pliku czeka na faktyczne zwolnienie dekodera i
  blokuje ponowne otwarcie lokalnego pliku do końca edycji. Szczegóły:
  [EDIT-001-zwalnianie-pliku-przed-wycinaniem.md](EDIT-001-zwalnianie-pliku-przed-wycinaniem.md).

- `POD-004` — **Wydane w AMC Test `0.1.0-alpha.436`; poprawa polskiego
  wyszukiwania SoundCloud i parytet globalnego `Ctrl+Shift+F` są zapisane w
  commitach `11ba6271` i `a8263cc0`, lecz nie ma jeszcze nowej instalacji ani
  publikacji** —
  wyszukiwanie podcastów SoundCloud przez `Ctrl+F` i globalne `Ctrl+Shift+F`,
  lokalna subskrypcja przez `Ctrl+Shift+L`, adres profilu lub RSS w `Ctrl+N`
  oraz zwykłe pliki audio do odtwarzania i pobierania. Korekta zastępuje
  wcześniejszą webową integrację Mixcloud.
  Szczegóły:
  [POD-004-soundcloud-rss-wyszukiwanie-i-subskrypcje.md](POD-004-soundcloud-rss-wyszukiwanie-i-subskrypcje.md).

- `YT-005` — **Zaakceptowane przez użytkownika i wydane w AMC Test
  `0.1.0-alpha.436`; kod, testy, paczka, instalator, GitHub oraz ręczny odbiór
  głównych funkcji są zweryfikowane** — ponowne `Ctrl+I` w otwartej skrzynce
  doładowuje następne 150 pozycji, a kolekcje YouTube żądają polskich tytułów
  przy zachowaniu dat przybliżonych. Szczegóły:
  [YT-005-skrzynka-ctrl-i-i-tytuly-youtube.md](YT-005-skrzynka-ctrl-i-i-tytuly-youtube.md).

- `KEY-001` — **Zrobione; kod, pełna regresja, czysta paczka, instalator,
  wydanie GitHub i rzeczywista aktualizacja instalacji są zweryfikowane, a
  użytkownik zatwierdził obsługę klawiatury i skróty 28.10.2026** — osobna
  karta **Klawiatura** z filtrem poleceń, podglądem zmiany przed zapisem,
  wykrywaniem konfliktów, usuwaniem i przywracaniem skrótów oraz jednym
  kontraktem menu, palety i pomocy. Szczegóły:
  [KEY-001-konfigurowalne-skroty-klawiaturowe.md](KEY-001-konfigurowalne-skroty-klawiaturowe.md).

- `SET-001` — **W trakcie; kod priorytetów 1–3, pełna regresja, czysta paczka,
  instalator, wydanie GitHub i rzeczywista aktualizacja instalacji są
  zweryfikowane; robocza korekta kolejności kart, sekcji i końcowego edytora
  własnych komunikatów oraz automatyczne powiadamianie o nowej wersji zostały
  wydane w Public i AMC Test `0.1.0-alpha.445`; lokalnej instalacji nowej
  wersji nie wykonano, a ręczny odbiór NVDA pola pozostaje** —
  naprawa osiągalności pól domyślnych folderów przez Tab, przegląd nazw
  kontrolek, dostępna karta integracji z Windows, nowa karta Aktualizacje oraz
  karta Dostępność i komunikaty. Szczegóły:
  [SET-001-audyt-ustawien-dostepnosc-integracja-windows.md](SET-001-audyt-ustawien-dostepnosc-integracja-windows.md).

- `I18N-001` — **Poprawka po odbiorze wydana w AMC Test `0.1.0-alpha.444`;
  wspólna warstwa językowa, polski, angielski, wybór zgodny z systemem,
  osobne szablony komunikatów, pełny katalog 713 statycznych napisów i 312
  komunikatów dynamicznych, parytet menu/pomocy/palety, natychmiastowa zmiana
  języka i zgodna z językiem nazwa listy są zweryfikowane. Wyjątek kontrolki
  tekstowej, który po zapisaniu języka przerywał przebudowę menu, jest
  naprawiony; 31 testów i dwie sondy wxPython są zaliczone. Paczka, instalator
  i wydanie GitHub są zweryfikowane; lokalnej instalacji nie wykonano** —
  przygotowanie interfejsu wxPython do obsługi wielu języków bez korzystania
  z dawnej aplikacji WPF. Szczegóły:
  [I18N-001-wielojezyczny-interfejs-wxpython.md](I18N-001-wielojezyczny-interfejs-wxpython.md).

- `REL-002` — **W trakcie; najnowszy Test `0.1.0-alpha.445` i stabilny Public
  z 29.10.2026, 19:24 są opublikowane i odczytowo zweryfikowane. Public powstał
  z najnowszej bazy Test i ma WiiM oraz Frontier Smart, bez Sonosa, Spotify i
  TIDAL-a; 231/231 celowanych testów Pythona, 38/38 Launchera/aktualizatora,
  paczka `89473955`, cichy start, instalator, skan sekretów i zdalny SHA-256 są
  zaliczone. Najnowsze stabilne wydanie Public z 29.10.2026, 19:24
  automatycznie wykrywa nowe wydanie, informuje i pyta przed pobraniem;
  119 testów, paczka, instalator, bramka i publikacja GitHub są zaliczone;
  Hubert potwierdził działanie Public i zamykanie aplikacji. Test zachowuje
  TIDAL, Spotify i eksperymentalny, nadal niedziałający Sonos; 205 testów
  Pythona, 3 testy M3U/ICY, 38 testów aktualizatora, paczka, instalator i
  publikacja prerelease są zaliczone. Lokalnej instalacji nie zmieniono** — jedna
  kanoniczna linia AMC Python,
  wydania AMC Test i AMC Public z tego samego commitu, dwa kanały aktualizacji,
  standardowa instalacja Windows oraz kontrolowane uporządkowanie starego C#,
  gałęzi lokalnych i GitHuba. Szczegóły:
  [REL-002-kanoniczne-wydania-test-public-github.md](REL-002-kanoniczne-wydania-test-public-github.md).

- `REL-003` — **W trakcie; instalacja WPF, stare instalatory, artefakty
  kompilacji, trzy worktree alpha 425, dwa stare kandydaty Frontier, 26
  zbędnych gałęzi lokalnych i dziesięć powiązanych worktree usunięte; dwie
  gałęzie robocze pozostały, a końcowy snapshot źródeł WPF zarchiwizowano na
  GitHubie bez zmiany `main`** — potwierdza
  niezależne od logowania Google odświeżanie już zapisanych kanałów YouTube i
  dokumentuje bezpieczny zakres dalszego porządkowania starego repozytorium.
  Szczegóły:
  [REL-003-usuniecie-wpf-i-stan-youtube.md](REL-003-usuniecie-wpf-i-stan-youtube.md).

- `RECOG-001` — **W trakcie; poprawki Test są w opublikowanym `alpha.438`, a
  korekta Public usuwająca wewnętrzne działania Spotify/TIDAL jest w
  stabilnym `alpha.440`. Poprawka odświeżania otwartej historii po nowym
  rozpoznaniu jest zainstalowana w AMC Test 30.10.2026; pozostaje ręczny odbiór NVDA** — prawa strzałka
  od razu fokusuje pierwszy element, obie listy potwierdzają kopiowanie przez
  lokalny region aktywny, a historia jest dostępna z listy i odtwarzacza
  Plików lokalnych oraz Radia, lecz nie z pozostałych sesji. Pełne podmenu
  zachowuje `Usługi, łącza i powiązania`. `Escape` zachowuje sesję, widok,
  zaznaczony element i wraca fokusem do miejsca wywołania.
  Szczegóły:
  [RECOG-001-interakcje-z-rozpoznanymi-utworami.md](RECOG-001-interakcje-z-rozpoznanymi-utworami.md).

- `RADIO-001` — **Kandydat AMC Public zbudowany i zweryfikowany lokalnie;
  instalator oczekuje na odbiór Huberta, bez instalacji i publikacji** — listy
  M3U z samymi adresami uzupełniają nazwy bez
  nadpisywania nazw użytkownika, a kompatybilne radio przekazuje bieżący tytuł
  do `NVDA+T`. Odrzucona pierwsza paczka ujawniła niepodpisane cache Pythona;
  końcowy builder i instalator blokują pliki spoza manifestu. Po odbiorze ten
  sam commit ma trafić do AMC Test. Szczegóły:
  [RADIO-001-import-m3u-icy-i-tytuly.md](RADIO-001-import-m3u-icy-i-tytuly.md).

- `REC-002` — **Zrobione i zainstalowane w AMC Test 26.10.2026, 13:32** —
  prawa strzałka w widoku `Nagrywane` i harmonogramach podaje format, nazwę
  pliku oraz folder bez powtarzania stacji i adresu. Szczegóły:
  [REC-002-prawa-strzalka-nagrywane-pliki.md](REC-002-prawa-strzalka-nagrywane-pliki.md).

- `SOCIAL-001` — **Wydane próbnie w AMC Test `0.1.0-alpha.444`,
  bez instalacji i odbioru NVDA** — pojedyncze publiczne filmy Facebooka i
  Instagrama oraz ręcznie odświeżane strony Facebooka; odtwarzanie dźwięku,
  pobieranie MP3 i wideo.
  Odczyt stron korzysta z nieudokumentowanego mechanizmu Meta i może być
  niepełny. Szczegóły:
  [SOCIAL-001-facebook-instagram-wideo.md](SOCIAL-001-facebook-instagram-wideo.md).

- `YT-004` — **W trakcie; kod, pełna regresja, build i instalacja AMC Test są
  gotowe; pozostaje ręczny odbiór prawdziwego pobrania i NVDA** — pobieranie
  filmów YouTube pod `Ctrl+Shift+D`, pojedyncze `Zapisz jako` pod `Ctrl+S` oraz
  domyślny wybór MP4 albo najlepszego oryginalnego formatu. Szczegóły:
  [YT-004-pobieranie-wideo-youtube.md](YT-004-pobieranie-wideo-youtube.md).

- `PLAY-001` — **W trakcie; kod, pełna regresja 2010/2010, instalacja i
  uruchomienie AMC Test 23.10.2026, 11:24 są gotowe; czeka na ręczny odbiór
  NVDA** — trwałe
  `Odtwarzaj losowo (R)` w odtwarzaczach Plików, Podcastów i Spotify, również
  w menu kontekstowym, palecie i pomocy; jawne kolejki zachowują własny porządek.
  Szczegóły:
  [PLAY-001-odtwarzanie-losowe-r.md](PLAY-001-odtwarzanie-losowe-r.md).

- `STAB-001` — **Zrobione; zniknięcie i powrót okna z 23.10.2026 były
  kontrolowaną promocją AMC Test, nie awarią Pythona** — korelacja manifestu,
  procesów, logu hosta i dzienników Windows oraz izolowane testy operacji
  asynchronicznych, launchera i kolejki hosta. Nie zmieniono źródeł ani
  instalacji. Szczegóły:
  [STAB-001-stabilnosc-okna-po-operacjach-sieciowych.md](STAB-001-stabilnosc-okna-po-operacjach-sieciowych.md).

- `SESSION-001` — **W trakcie; korekta po odbiorze NVDA ma 76/76 testów
  celowanych, pełną regresję Pythona 1983/1983 i została zainstalowana w
  zbiorczym AMC Test; migracja starej prędkości i kopii z 29.10 ma dodatkowo
  90 zaliczonych testów celowanych, lecz nie została jeszcze spakowana,
  zainstalowana ani wydana; pozostaje ponowny odbiór NVDA** —
  pełne, wykonywalne opcje sesji z historycznego AMC oraz nowa domyślna
  prędkość osobno dla Plików i Podcastów. Szczegóły:
  [SESSION-001-opcje-sesji-i-predkosc.md](SESSION-001-opcje-sesji-i-predkosc.md).

- `SESSION-002` — **Zrobione i opublikowane w AMC Test `0.1.0-alpha.441`;
  ustawienie pozostawania na liście
  działa per sesja także dla plików lokalnych i pozostałych wykonywalnych
  odtwarzaczy, a Menedżer ma osobny przycisk „Ustawienia sesji…”; testy
  celowane, paczka, instalator i GitHub są zaliczone, bez instalacji i ręcznego
  odbioru NVDA** —
  rozdzielenie ustawień od konfiguracji folderów, kont i urządzeń oraz wspólna
  polityka Entera, kolejek, zakładek i wyników katalogu. Szczegóły:
  [SESSION-002-ustawienia-sesji-i-zachowanie-odtwarzacza.md](SESSION-002-ustawienia-sesji-i-zachowanie-odtwarzacza.md).

- `SPOT-001` — **Zrobione w zakresie prędkości; użytkownik potwierdził
  działanie przyspieszania. Kod, natywny DSP, komunikat przygotowania, testy,
  build i bieżąca instalacja AMC Test są zaliczone** —
  prawdziwa prędkość `0,5–2,0x` w wewnętrznym odtwarzaczu Spotify, wspólne
  skróty `Shift+,`, `Shift+.` i `Ctrl+.`, menu, pomoc, paleta oraz dostępne
  etykiety wartości. Szczegóły:
  [SPOT-001-predkosc-odtwarzania.md](SPOT-001-predkosc-odtwarzania.md).
  Bieżąca instalacja:
  [AMC-TEST-2026-10-24-0021-youtube-refresh-shortcuts.md](AMC-TEST-2026-10-24-0021-youtube-refresh-shortcuts.md).

Roboczy indeks materiału z nocnych etapów do przyszłej instrukcji i opisu
zmian, wymagający korekty użytkownika:
[INDEKS-MATERIALU-INSTRUKCJA-I-ZMIANY-2026-10-19.md](INDEKS-MATERIALU-INSTRUKCJA-I-ZMIANY-2026-10-19.md).

- `BACKUP-001` — **Wdrożone i zweryfikowane w źródłach; czeka na instalację
  kandydata oraz ręczny odbiór NVDA** — bezpieczna pełna kopia bieżącego
  profilu oraz osobny eksport i import ustawień, Biblioteki, playlist AMC,
  Radia, harmonogramów, Podcastów i YouTube oraz urządzeń i usług. Szczegóły:
  [BACKUP-001-eksport-import-danych.md](BACKUP-001-eksport-import-danych.md).

- `REC-001` — **Zrobione; korekta zachowania istniejącego pliku po zerwaniu
  strumienia jest zainstalowana w AMC Test** — trwałość wykonanego
  harmonogramu nagrywania, zgodność listy `Alt+R` z blokadą zamknięcia oraz
  zachowanie niepustego pliku domkniętego przed awarią odbioru. Szczegóły:
  [REC-001-harmonogram-alt-r-zamykanie.md](REC-001-harmonogram-alt-r-zamykanie.md).
- `AMC02` — **Publiczny `alpha.431` zainstalowany i uruchomiony do odbioru
  NVDA; AMC Test czeka na bezpieczne zamknięcie** — `Alt+R`
  i `Alt+Shift+R` przy jednym wpisie podają widok, pozycję oraz nazwę pliku.
  `Ctrl+H` zapisuje od teraz potwierdzony start Radia, pliku i podcastu, a
  pojedynczy wpis Historii jest nazwany niezależnie od zdarzenia fokusu;
  wcześniejszych niezapisanych odtworzeń nie da się odtworzyć wstecz.
  Zachowano osobną pamięć zaznaczenia oraz bitrate, wielkość i folder pod lewą
  strzałką także w `Ctrl+H`. Bieżący zapis realizacji:
  [AMC02-listy-nagrywania-i-historii.md](AMC02-listy-nagrywania-i-historii.md).
  Zatwierdzona analiza historyczna:
  `D:\Projekty Codex\Accessible Multimedia Controller\AMC02_ANALIZA_2026-10-18.md`
  oraz `AMC02_REALIZACJA_ETAP_A_2026-10-18.md`. Właściwe wdrożenie wykonujemy
  w bieżącym AMC Test, a stary kod służy tylko jako wzorzec.
- `SCHED-001` — **Druga korekta po ręcznym odbiorze zainstalowana w AMC Test;
  czeka na ponowny odbiór NVDA** — dostępna przebudowa listy i edytora
  harmonogramów: informacje pod strzałkami, segmentowa data i czas oraz
  jednoznaczne wpisywanie czasu trwania. Szczegóły:
  [SCHED-001-edytor-i-klawisze-harmonogramow.md](SCHED-001-edytor-i-klawisze-harmonogramow.md).
- `NVDA-001` — **W trakcie; użytkownik potwierdził bardzo szybką reakcję
  `Ctrl+C` w AMC, pozostaje kontrola zwykłej aplikacji** — usunięcie opóźnienia `Ctrl+C` powodowanego przez
  NVDAExtensionGlobalPlugin bez zmiany zwykłego schowka w innych programach.
  Szczegóły:
  [NVDA-001-opoznienie-ctrl-c.md](NVDA-001-opoznienie-ctrl-c.md).
- `SEARCH-001` — **Wspólny kontrakt wyszukiwania dla wszystkich sesji oraz
  pustego profilu jest zainstalowany w obu profilach po pełnej regresji 1910
  testów; czeka na ręczny odbiór NVDA** — wyszukiwanie Plików lokalnych,
  YouTube, Radia,
  Spotify, TIDAL, WiiM i Sonosa:
  `Ctrl+Shift+L` pozostaje w oknie także dla zapisanego wyniku, wykonuje jedną
  właściwą operację i nie oddaje mowy głównemu oknu. `Ctrl+L` zamyka modal i
  przechodzi wspólnym poleceniem do Biblioteki. Szczegóły:
  [SEARCH-001-youtube-biblioteka-komunikaty.md](SEARCH-001-youtube-biblioteka-komunikaty.md).
- `POD-001` — **Wdrożone i zainstalowane w AMC Test; czeka na ręczny odbiór
  NVDA** — trzy tryby sortowania
  zbiorczej Biblioteki podcastów, kanałów YouTube i „Media internetowe”,
  prywatna trwałość wyboru i zachowanie zaznaczenia po stabilnym ID. Szczegóły:
  [POD-001-sortowanie-biblioteki-zrodel.md](POD-001-sortowanie-biblioteki-zrodel.md).
- `POD-002` — **Zrobione i przyjęte przez użytkownika 23.10.2026 w AMC Test;
  odbiór obejmuje `Ctrl+N` w sesji `Podcasty i YouTube`. Poprawki szybkiego
  `Ctrl+V`, fokusu, blokady drugiego Entera i ukrywania procesów są scalone
  oraz znajdują się również w bieżącej instalacji 21:25** — formularz
  `Ctrl+N` pozostaje otwarty przez oczekiwanie, procesy pomocnicze nie tworzą
  okna, a zwykła mowa nie ujawnia „Windows 32”, terminala, ścieżek ani nazw
  technicznych. AMC Public, instalator i GitHub bez zmian. Szczegóły:
  [POD-002-import-youtube-ctrl-n.md](POD-002-import-youtube-ctrl-n.md).
- `POD-003` — **Zrobione, zainstalowane i uruchomione w AMC Test 23.10.2026,
  11:24; ręczny odbiór NVDA pozostaje** — naprawa niejawnego ucinania dużych kanałów
  RSS, uczciwy wynik pełny/częściowy/błędny oraz jawne stronicowanie `wczytano
  N z całości` z zachowaniem fokusu. Szczegóły:
  [POD-003-niezawodnosc-odswiezania-i-duze-kanaly.md](POD-003-niezawodnosc-odswiezania-i-duze-kanaly.md).
- `AMC04` — **Ręczny odbiór 23.10.2026 potwierdził, że odtwarzanie dużego
  podcastu Spotify działa, ale rozdziały w Spotify nadal nie działają.
  Obecny kod potrafi tworzyć rozdziały tylko ze znaczników czasu w opisie;
  nie należy przedstawiać natywnych rozdziałów Spotify jako wdrożonych.
  Zakładki i rozdziały zwykłych podcastów pozostają osobnym, wcześniej
  zaimplementowanym zakresem** — przeniesiony do głównego AMC
  Test/Python zatwierdzony model skrótów
  zakładek, nazwanych zakładek, rozdziałów, historii widoków i historii
  odtwarzanych elementów. Bieżący zapis realizacji:
  [AMC04-nawigacja-zakladki-rozdzialy-historia.md](AMC04-nawigacja-zakladki-rozdzialy-historia.md).
  Źródłowy audyt i rozpoczęta realizacja są zapisane w
  `D:\Projekty Codex\Accessible Multimedia Controller\AMC03_AUDYT_SKROTOW_2026-10-18.md`
  oraz `AMC04_REALIZACJA_ETAP_A_2026-10-18.md`. Stare źródła WPF służą tylko
  jako wzorzec; właściwe wdrożenie wykonujemy w tym repozytorium.
- `MENU-001` — **W trakcie; naprawa `Shift+F10` harmonogramów i nazwa `Alt+D`
  oraz korekta pełnych ścieżek profilu hosta są zainstalowane w AMC Test i
  czekają na odbiór NVDA** —
  wspólne menu kontekstowe bez odczytu technicznych cyfr i przypadkowych
  liter dostępu, nazwy zależne od rodzaju elementu, rzeczywiste skróty oraz
  audyt dostępności poleceń i nawigacji między sesjami. Szczegóły:
  [MENU-001-menu-kontekstowe-i-skroty.md](MENU-001-menu-kontekstowe-i-skroty.md).
- `AMC05` — **Wdrożone i zainstalowane w AMC Test; czeka na ręczny odbiór
  NVDA** — stan `Odtwarzane` lub `Wstrzymane` na właściwych listach bez utraty
  etykiety i bez przestawiania fokusa. Bieżący zapis realizacji:
  [AMC05-odtwarzany-element-na-listach.md](AMC05-odtwarzany-element-na-listach.md).
  Zatwierdzona specyfikacja:
  `D:\Projekty Codex\Accessible Multimedia Controller\AMC05_ODTWARZANY_ELEMENT_NA_LISTACH_2026-10-18.md`.
- `AMC13` — **Rozszerzenie skoków procentowych zainstalowane w AMC Test;
  czeka na odbiór NVDA** — `End` w otwartym
  odtwarzaczu Time Shiftu wraca na żywo i do tempa normalnego, `NVDA+End`
  pozostaje poleceniem czytnika, a cyfry `0`–`9` przechodzą do 0–90%
  aktualnego bufora. Bieżący zapis realizacji:
  [AMC13-timeshift-end-na-zywo.md](AMC13-timeshift-end-na-zywo.md).
  Zatwierdzona specyfikacja:
  `D:\Projekty Codex\Accessible Multimedia Controller\AMC13_TIMESHIFT_RADIA_2026-10-18.md`.
  Eksport fragmentów Time Shiftu pozostaje późniejszym rozszerzeniem.
- `AMC08` — **Po odbiorze instalacji 11:24 znaleziono i naprawiono rzeczywistą
  przyczynę pustego Tyflo Podcast: 10 rekordów `null` w 2873 elementach
  przerywało parser. Odcinki programu są teraz ładowane narastająco po 150;
  prawdziwy odczyt zwrócił pierwszą porcję w 3,38 s. Pełna regresja, budowa i
  instalacja AMC Test 12:56 są zaliczone. Prawdziwe OAuth i dodawanie do playlist zostały potwierdzone
  przez użytkownika; korekta mowy `Ctrl+F5` i widoczności pod `Ctrl+P`
  playlist istniejących oraz nowych jest zainstalowana w AMC Test; również
  czyszczenie migawki po ponownym logowaniu jest zbudowane i zainstalowane,
  całość czeka na ręczny odbiór NVDA; korekta z 23.10.2026 naprawiająca
  jednorazowe pełne odświeżenie zapisanych podcastów i odcinków została
  zainstalowana i uruchomiona w AMC Test 23.10.2026, 11:24**
  — natywne playlisty
  Spotify w AMC Test: `Ctrl+P`, `Ctrl+Shift+P`, pobranie katalogu raz na
  uruchomienie, natychmiastowa widoczność nowej playlisty i zapamiętanie
  ostatnich celów. Ręczny test z odłączonym interfejsem Denon USB ujawnił, że
  wcześniejszy fallback następował za późno. Korekta z 23.10.2026 sprawdza
  nazwane wyjście przy konfiguracji sesji bez logowania, przełącza bieżący tor
  na urządzenie domyślne przed pierwszym Play i rozróżnia kod
  `trackUnavailable`; kod, testy, build Release oraz instalacja AMC Test 16:30
  są zaliczone, a ręczny odbiór dźwięku pozostaje do wykonania. Mechanizm
  ma partycje usług i kont, aby kolejne integracje
  używały tego samego kontraktu bez kopiowania kodu Spotify. Przebieg:
  `AMC08-playlisty-spotify.md`. Zatwierdzona specyfikacja:
  `D:\Projekty Codex\Accessible Multimedia Controller\AMC08_PLAYLISTY_SPOTIFY_2026-10-18.md`.
  Historyczne źródła są tylko wzorcem; kod powstaje w tym repozytorium.
- `AMC09` — **Menedżer, pierwsze rozszerzenie Konfiguruj, ujednolicone
  `Ctrl+F5`, precyzyjne nazwy dla każdej sesji oraz Foldery Biblioteki są
  zbudowane; po ręcznym wykryciu brakującego mapowania poprawiono całą rodzinę
  `F1`–`F24` i nawigację bloku numerycznego; najnowsza korekta jest
  zainstalowana i czeka na ponowny odbiór NVDA** — dostępny
  Menedżer sesji pod `Ctrl+Shift+S`, włączanie Spacją, zwarta kolejność
  `Ctrl+1–9`, zmiana kolejności `Alt+strzałkami`, stan gotowości i stała nazwa
  `Radio`. Ma działać już w AMC Test; profil publiczny będzie później filtrował
  tę samą listę przez manifest możliwości. Zatwierdzona specyfikacja:
  `D:\Projekty Codex\Accessible Multimedia Controller\AMC09_MENEDZER_SESJI_I_PIERWSZE_LOGOWANIE_2026-10-18.md`.
  Automatyczne otwarcie na czystej instalacji pozostaje do odbioru AMC12 i nie
  może zmienić obecnego profilu testowego. W Plikach lokalnych `Ctrl+F5`
  otwiera teraz niedestrukcyjny menedżer dodawania, odświeżania i odłączania
  Folderów Biblioteki; pozostałe sesje zachowują własne znaczenie tego skrótu.
  Korekta z 20.10.2026 dodaje w Radiu rzeczywiste „Odśwież stacje Radia” pod
  `Ctrl+F5`, również z odtwarzacza, przy wspólnym opisie menu, pomocy i palety.
  Kod i testy kontraktu są potwierdzone. Ręczny odbiór ujawnił jednak wspólną
  przyczynę ciszy `F5` i `Ctrl+F5`: brak mapowania rzeczywistego kodu klawisza
  wxPython. Poprawka obejmuje już wszystkie klawisze funkcyjne, a nie tylko
  `F5`; testy zaczynają się od surowych zdarzeń klawiatury. Najnowsza wersja
  została zainstalowana 20.10.2026. Korekta z 21.10.2026 utrwala zasadę, że
  `Ctrl+F5` po skonfigurowaniu pokazuje stan i zapisane cele bez ponownego
  logowania lub skanowania. Audyt wykazał, że Spotify i TIDAL nadal wymagają
  domknięcia jawnego wylogowania oraz stanowych przycisków konta.
  Bieżący zapis realizacji:
  [AMC09-menedzer-sesji.md](AMC09-menedzer-sesji.md).
- `AMC12` — **Lokalny kandydat `alpha.433` przeszedł pełną regresję źródeł,
  został zbudowany i zainstalowany w `D:\Projekty Codex\AMC Public`, lecz
  decyzją użytkownika z 21.10.2026 jest wstrzymany do odbioru Smart Frontier.
  Nie uruchomiono go do odbioru, nie opublikowano i nie należy go jeszcze
  przekazywać. Dodaje ręczną kolejność Biblioteki i Ulubionych Radia:
  `Alt+strzałka góra/dół` oraz `Ctrl+X`, `Ctrl+V`, z nazwaniem stacji nad albo
  pod którą przeniesiono wybór.
  Publiczny AMC jest budowany z tego samego kodu co AMC Test; zgodnie z korektą
  użytkownika z 21.10.2026 wyłącza
  wyłącznie niedopracowane funkcje streamingowe, a wszystkie ukończone funkcje,
  w tym Sonosa, zachowuje. Obecnie TIDAL, Spotify i nieodebrana jeszcze
  integracja Frontier Smart pozostają tylko w AMC Test. Końcowa paczka
  `e6d4269d` ma 1075 plików; integralność i kontrolowany start paczki oraz
  integralność zainstalowanej kopii są zaliczone. Instalator usuwa także stare moduły niedostępnych usług
  pozostałe po wcześniejszej wersji. Najnowsza poprawka rozpoznaje ten sam plik
  w `Ctrl+O` i Bibliotece podczas `Ctrl+Shift+U`, nie przełącza folderu na pustą
  Bibliotekę oraz jasno wskazuje `Ctrl+F5`, gdy folder nie jest źródłem.
  Wcześniejsza poprawka zachowuje
  kolejność importowanej playlisty Radia, uruchamia `Alt+1–3` w Bibliotece i
  Ulubionych, wiąże `Ctrl+Shift+U` z Ulubionymi oraz `Ctrl+Shift+L` z
  członkostwem w Bibliotece. Ręczny odbiór w NVDA i decyzja o dołączeniu
  Frontier Smart pozostają do wykonania; niczego nie opublikowano** — jeden kod, jawny manifest możliwości, osobne
  katalogi, blokady, logi i kanały AMC Test oraz publicznego AMC. Bieżący zapis
  realizacji:
  [AMC12-profile-wydania-publicznego-i-testowego.md](AMC12-profile-wydania-publicznego-i-testowego.md).
  Zatwierdzona specyfikacja:
  `D:\Projekty Codex\Accessible Multimedia Controller\AMC12_PROFILE_WYDANIA_PUBLICZNEGO_I_TESTOWEGO_2026-10-18.md`.
- `AMC10` — **W trakcie odbioru użytkownika; publiczny broker działa pod
  `https://sonos.stareaudycje.pl/`, callback jest zapisany w portalu Sonos, a
  paczka AMC Test `0423232e` z commita `8f42e319` została zbudowana, atomowo zainstalowana i
  uruchomiona z `D:\Projekty Codex\AMC Python`. Rozłączona sesja ma jeden
  wiersz logowania, Enter otwiera konto z jednym polem informacji, a
  `Rozpocznij logowanie` automatycznie otwiera oficjalną stronę i zamyka modal.
  Ponowne otwarcie konta zachowuje w pamięci pierwotny adres tej samej próby i
  udostępnia „Otwórz stronę ponownie” oraz „Kopiuj adres logowania”.
  Po powrocie główne okno pozostaje dostępne, a jeden wiersz `Sprawdź logowanie
  do Sonos` prowadzi do następnego kroku. `Ctrl+Alt+Shift+R` zatrzymuje
  nagrania globalnie także z Sonosa i pozostałych sesji. Pierwsza wcześniejsza
  próba dotarła do oficjalnego wyboru
  gospodarstwa domowego, lecz Sonos zwrócił błąd przed callbackiem. Próba w
  oknie prywatnym wymaga powtórzenia z poprawnie skopiowanego adresu startowego.
  Pozostają ponowna próba na koncie przypisanym do systemu, udane logowanie, odmowa,
  odnowienie, sprzęt i NVDA. GitHub i AMC Public pozostają bez zmian** — działające
  sterowanie Sonosem i WiiM jest już przepisane i
  nie wymaga ponownej implementacji. `Ctrl+F5` wybiera dom i grupę, a przy braku
  połączenia prowadzi do konta zamiast kończyć się błędem. Okno udostępnia
  rozpoczęcie, jawne
  sprawdzenie, anulowanie i odświeżenie logowania bez przekazywania tokenów do
  Pythona. Dodano wcześniej serwerowy
  kontrakt logowania, PKCE, jednorazowe sesje, odnowienie, ograniczenie tempa,
  bezpieczne logowanie oraz ścisłą obsługę zakończenia TLS przez jawnie zaufane
  reverse proxy. AMC Public pozostaje bez zmian.
  Bieżący zapis realizacji:
  [AMC10-logowanie-sonos-i-publiczny-broker.md](AMC10-logowanie-sonos-i-publiczny-broker.md).
  Zatwierdzona specyfikacja:
  `D:\Projekty Codex\Accessible Multimedia Controller\AMC10_SONOS_WIIM_URUCHOMIENIE_I_UJEDNOLICENIE_2026-10-18.md`.
  Bez obecności użytkownika nie logować konta i nie sterować głośnikami.
- `AMC14` — **W trakcie; folder zawierający, argument pliku, osobny kanał
  działającej instancji i idempotentne `/kolejka` są zainstalowane w AMC Test;
  opcjonalna i odwracalna rejestracja instalatora jest rozdzielona dla AMC
  oraz AMC Test, ma
  potwierdzoną składnię, lecz nie została zastosowana w systemie** —
  aplikacyjna integracja z powłoką Windows,
  przekazywanie plików do właściwej instancji i późniejsza rejestracja przez
  instalator. Bieżący zapis realizacji:
  [AMC14-foldery-i-integracja-powloki.md](AMC14-foldery-i-integracja-powloki.md).
  Zatwierdzona specyfikacja:
  `D:\Projekty Codex\Accessible Multimedia Controller\AMC14_FOLDERY_I_INTEGRACJA_POWLOKI_2026-10-18.md`.
- `YT-001` — **Wdrożone do ręcznego odbioru — logowanie, subskrypcje i opisy
  są odebrane; trwałe respektowanie usunięć, uczciwe ponawianie oraz obsługa
  kanałów bez standardowej karty Filmy są zbudowane, zainstalowane i działają
  na rzeczywistej bazie. Duplikatów kanałów ani filmów nie wykryto**
  — oficjalne logowanie do
  konta YouTube, synchronizacja subskrypcji i zwykłych playlist oraz bezpieczny
  zakres czynności konta. Etap rozpoczęto 21 października 2026 na nowe jawne
  polecenie użytkownika; sam start nadal nie loguje konta ani nie otwiera
  przeglądarki. Profil publiczny pozostaje wyłączony. Korekta z 27.10.2026:
  rzeczywiste ponowne logowanie przez zainstalowany LiteHost i systemową
  przeglądarkę zakończyło się wynikiem `Zalogowano do YouTube`, a kolejny odczyt
  potwierdził aktywne połączenie. Nie zmieniano Google Cloud, źródeł, instalacji
  ani GitHuba. Dalsza korekta z 27.10.2026: źródła profilu Public mają już
  osobny bezsekretowy tor przez broker tokenów, a projekty polityki
  prywatności, warunków i listy gotowości są zapisane, lecz nieopublikowane.
  Źródła Public cofają token w Google, usuwają oznaczone dane konta i
  automatycznie usuwają je po 30 dniach bez odświeżenia; publiczne kanały
  dodane ręcznie pozostają nienaruszone. Celowane testy, build i audyt
  zależności są zaliczone. Decyzja użytkownika z 27.10.2026 wyklucza wdrożenie
  na `stareaudycje.pl`: do czasu świadomego wyboru innej domeny moduł pozostaje
  przygotowany, ale AMC Public powstaje bez zasobów OAuth i bez martwych pozycji
  konta w interfejsie. Przed ewentualnym włączeniem pozostają: zgoda i dostęp do
  polityki w interfejsie, uzupełnienie danych administratora, własna
  zweryfikowana domena, wdrożenie brokera, konfiguracja produkcyjnego projektu
  Google oraz rzeczywisty odbiór zakresu `youtube.readonly` z NVDA.
  Szczegóły:
  [YT-001-konto-youtube-i-synchronizacja.md](YT-001-konto-youtube-i-synchronizacja.md).
  Bieżąca instalacja:
  [AMC-TEST-2026-10-24-0021-youtube-refresh-shortcuts.md](AMC-TEST-2026-10-24-0021-youtube-refresh-shortcuts.md).
- `YT-002` — **W trakcie — dotychczasowy zakres jednego kanału jest
  zainstalowany, a 24.10.2026 wdrożono źródła zbiorczej listy wybranych
  kanałów pod `Ctrl+Shift+I`; pełna regresja wxPython 2032/2032, build,
  integralność, atomowa instalacja i start AMC Test 24.10.2026, 13:33 są
  zaliczone. Rzeczywisty odbiór potwierdził listę i ujawnił nieczytelne
  oczekiwanie; commit `efb8c090` blokuje powtórne `Ctrl+Shift+I` i `F5`
  podczas trwającego skanu. Pełna regresja po poprawce: 2033 zaliczone,
  111 pominiętych i 13 podtestów; paczka `8af5940f` z commita `81a1e4c5`
  przeszła build, integralność i kontrolowany start, została atomowo
  zainstalowana i uruchomiona jako `AMC Test — 24.10.2026, 14:48`** — lista transmisji live/upcoming zapisanego kanału, Enter w
  wewnętrznym silniku, kopiowanie adresu, F5 i Backspace oraz dynamiczna stacja
  Radia oparta na stałym adresie kanału `/live`. Tego samego dnia rozpoczęto
  poprawkę zwykłej listy kanału: łączy ona kartę `Filmy` z publicznymi,
  zakończonymi transmisjami, bez osobnej historii i bez duplikatów. Test
  syntetyczny oraz rzeczywiste kanały Dominikanie Poznań, Wrocław, Freta i
  Szczecin są zaliczone; build źródeł i 52 celowane testy wxPython także są
  zaliczone. Użytkownik potwierdził również prawidłowe rozpoznanie i nagranie
  z harmonogramu transmisji Dominikanie Poznań i Teleexpress oraz przygotowanie
  następnych transmisji na 25.10.2026. Kod jest w commicie `921b784e`; nowa
  paczka AMC Test `5b3ac88c` została zbudowana, sprawdzona, atomowo
  zainstalowana i uruchomiona jako `AMC Test — 24.10.2026, 21:20`. Pełna
  integralność zainstalowanej kopii i kontrolowany start są zaliczone. AMC
  Public i GitHub pozostały bez zmian. Szczegóły:
  [YT-002-transmisje-na-zywo-i-dynamiczne-radio.md](YT-002-transmisje-na-zywo-i-dynamiczne-radio.md).
- `YT-003` — **Podstawowy zakres opisów jest zainstalowany i odebrany; korekta
  z 27.10.2026 pełnego automatycznego odczytu wszystkich akapitów ma commit
  `a4471493` i 88/88 samowystarczalnych testów, lecz czeka na następną paczkę,
  instalację i ręczny odsłuch NVDA** — `Alt+D` pokazuje opisy kanałów, playlist
  i materiałów YouTube, zapisuje nowe opisy z importu i synchronizacji oraz
  odczytuje na żądanie brakujący opis starszego publicznego wpisu bez logowania
  Google. Limit 500 znaków w automatycznej mowie został usunięty również dla
  RSS i Spotify; Radio nie skracało tej odpowiedzi i otrzymało test ochronny.
  GitHub pozostał bez zmian. Szczegóły:
  [YT-003-opisy-youtube-alt-d.md](YT-003-opisy-youtube-alt-d.md).
- `NAME-001` — **Kod, testy, paczka, instalacja i uruchomienie AMC Test gotowe;
  bez nowego instalatora AMC Public** —
  krótkie nazwy `AMC Test — data, godzina` i `AMC Public — data, godzina`,
  z technicznymi identyfikatorami pozostawionymi poza zwykłą nazwą.
  Szczegóły: [NAME-001-amc-test.md](NAME-001-amc-test.md).
- `FRONTIER-001` — **W trakcie — użytkownik potwierdził 25.10.2026 na bieżącym
  AMC Test, że `Ctrl+Alt+M` i wybór trybów znacznie przyspieszyły, presety
  działają, AMC widzi pełną listę 30 pozycji, a skróty obsługują pierwszych 12.
  Ustalono, że nie powstaną osobne skróty dla Spotify, USB, FM, DAB+ ani
  Bluetooth. Następny osobny etap ma rozważyć pod `Ctrl+L` dwie pozycje:
  `Tryby urządzenia` i `Presety urządzenia`, bez udawanych Ulubionych. Tego
  rozszerzenia jeszcze nie zaimplementowano** —
  `Ctrl+8`, `Ctrl+F5` i ręczny adres korzystają z jednego modelu stanów.
  AMC wymaga potwierdzonego opisu FSAPI/UNDOK i użytkowej nazwy urządzenia,
  zanim pokaże pole PIN. Brak wyniku i błąd nie prowadzą do PIN-u. Po zapisaniu
  urządzenia `Ctrl+F5` pokazuje listę bez ponownego skanowania. Funkcja nadal
  pozostaje wyłącznie w profilu AMC Test.
  Szczegóły:
  [FRONTIER-001-frontier-smart-fsapi.md](FRONTIER-001-frontier-smart-fsapi.md).
  Bieżąca instalacja:
  [AMC-TEST-2026-10-23-2241-frontier-pin-spotify-start.md](AMC-TEST-2026-10-23-2241-frontier-pin-spotify-start.md).
- `ARC-001` — **Oczekuje** — wspólny rdzeń poleceń dla GUI, palety, NVDA,
  przyszłego CLI i lokalnego sterowania AI. Szczegóły:
  [ARC-001-wspolny-rdzen-polecen-cli-ai.md](ARC-001-wspolny-rdzen-polecen-cli-ai.md).
- `MAC-001` — **Oczekuje, odłożone bez terminu** — ewentualna ocena portu AMC
  na macOS dopiero po wydaniu Windows i wyłącznie na nowe jawne polecenie.
  Szczegóły:
  [MAC-001-port-macos-wxpython-litehost.md](MAC-001-port-macos-wxpython-litehost.md).
- `LIC-001` — **Oczekuje** — Radio Browser, licencja BASS i bezpieczna decyzja
  o dobrowolnych wpłatach. Szczegóły:
  [LIC-001-radio-browser-bass-darowizny.md](LIC-001-radio-browser-bass-darowizny.md).
- `REL-001` — **W trakcie; lokalny formularz, klient HTTPS, Worker i samodzielna
  bramka wdrożenia są gotowe; nieaktywny bootstrap istnieje, a aktywacja czeka
  na subdomenę `workers.dev`, osobny token GitHuba i odbiór** — menu, F1 i
  paleta prowadzą do dostępnego formularza z pełnym
  podglądem, domyślnie wyłączonym logiem, redakcją danych oraz kopiowaniem lub
  atomowym zapisem w magazynie profilu. Po zweryfikowanym endpointcie ten sam
  formularz wysyła dokładnie widoczny raport w tle. Worker ma drugą redakcję,
  limit, idempotencję i rozdziela prywatną treść od publicznego issue; nie
  został wdrożony wyłącznie jako bootstrap bez sekretu i nie utworzył
  prawdziwego zgłoszenia. Zależności
  Workera są przypięte, a skrypt wdrożenia wykonuje testy oraz dwa pakowania
  `--dry-run`. Jawny bootstrap przygotowuje migrację Durable Object bez sekretu,
  ustawienie tokenu i właściwe wdrożenie są osobnymi etapami, a skrypt nigdy sam
  nie rozpoczyna logowania. Sesja Cloudflare jest potwierdzona. Prywatne
  repozytorium intake i wymagane etykiety są już utworzone. Lokalna bramka
  publikacji instalatora sprawdza profil, rewizję, rozmiar, SHA-256 i wynik
  odczytany z GitHuba, ale bez jawnego `-Publish` niczego nie wysyła. Czysta
  publiczna linia źródeł powstaje jako lokalny snapshot identycznego drzewa,
  bez publikowania roboczej historii i bez przełączania bieżącej gałęzi.
  Szczegóły:
  [REL-001-github-aktualizacje-sygnalista.md](REL-001-github-aktualizacje-sygnalista.md).
- `STR-001` — **W trakcie; bezsekretny prototyp wydawania tokenów Apple Music
  ma 12/12 testów, ale prawdziwe wdrożenie jest wstrzymane do próby
  MusicKit/WebView2/NVDA i decyzji o właścicielu Apple Developer Team** —
  osobne laboratorium TIDAL i Apple Music; Apple Music nie jest jeszcze sesją
  AMC Test ani AMC Public.
  Szczegóły:
  [STR-001-tidal-apple-music-laboratorium.md](STR-001-tidal-apple-music-laboratorium.md).
- `BLUOS-001` — **Oczekuje; opracowanie źródłowe jest gotowe, a pierwszy etap
  wymaga pomiaru na Playerze użytkownika** — osobna lokalna sesja BluOS do
  sterowania Playerami, grupami, kolejką, presetami i katalogiem udostępnionym
  przez urządzenie. Nie jest to bezpośrednie API Apple Music ani TIDAL-a.
  Szczegóły:
  [BLUOS-001-sesja-bluos.md](BLUOS-001-sesja-bluos.md).

## Krótkie polecenia użytkownika

Wystarczy powiedzieć na przykład: `Uruchom zadanie NVDA-001`. Agent ma wtedy
odczytać rejestr oraz właściwy plik i wykonać wyłącznie wskazane zadanie.
