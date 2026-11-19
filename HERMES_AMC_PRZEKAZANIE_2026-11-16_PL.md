# AMC — pełne przekazanie prac Hermesowi

Stan odczytu: **16 listopada 2026 r.** Ten dokument ma pozwolić Hermesowi na
drugim komputerze zacząć pracę bez historii rozmów Codex. Użytkownik wybrał
Hermesa do dalszych, większych prac nad AMC, ponieważ łatwiej mu przełączać się
tam między zadaniami z NVDA. Historia czatów Codex nie jest częścią repozytorium
GitHub. Trwałym źródłem ustaleń są pliki projektu, kod, testy i historia Git.

## Przeczytaj przed pierwszą zmianą

1. `HERMES_AMC_START_PL.md` — krótka instrukcja wejścia.
2. `AGENTS.md` — obowiązujące decyzje użytkownika i zasady bezpieczeństwa.
3. `docs/zadania/README.md` — rejestr zadań. Ma starsze wpisy i jeden nieaktualny
   „Bieżący punkt wznowienia”; nowsze korekty w plikach zadania i ten dokument
   mają pierwszeństwo.
4. `docs/zadania/HERMES_AMC_KATALOG_ZADAN_2026-11-16_PL.md` — wszystkie 173
   zastane pliki oraz sześć jawnie oznaczonych rekonstrukcji brakujących
   rekordów; katalog kontroluje też odsyłacze.
5. Plik zadania wybranego przez użytkownika, w całości, wraz z późniejszymi
   korektami. Nie uruchamiaj innych zadań tylko dlatego, że są w rejestrze.
6. Dla budowania: `ZALEZNOSCI_AMC_STAN_PL.md`, `wxlite/JAK_TESTOWAC.md`,
   `build.ps1`, skrypty `scripts/`, `installer/` i odpowiedni rekord wydania.

Starsze `AMC_PYTHON_STAN_2026-10-10_PL.md`, `PRZEKAZANIE_2026-10-14_WTOREK_PL.md`,
`README_PL.md`, `MAPA_KODU_PL.md` i `TESTY_ZADANIA_PL.md` są użyteczne do
architektury i historii, lecz ich zdania o „następnym” Spotify, starszej
gałęzi lub instalacji nie opisują automatycznie stanu z 16 listopada.

## Tożsamość projektu i synchronizacja między komputerami

- Rozwijany produkt to **AMC Python**, interfejs wxPython i wspólny host C#.
  Lokalny katalog tego komputera: `D:\Projekty Codex\AMC - integracja wxPython`.
  Stary projekt WPF w `D:\Projekty Codex\Accessible Multimedia Controller`
  jest historyczny i nie jest miejscem wdrażania aktualnego AMC.
- Repozytorium GitHub: `https://github.com/michalkasperczak/AMC`.
  **Bieżąca gałąź źródeł:** `codex/amc-test-public-20261109`.
  Odczyt 16.11: HEAD `f55892435c5795cad48b7eb5f178f30f9985b158`,
  drzewo główne czyste. Gałąź GitHub wskazywała ten sam commit.
- Lokalny `origin` tego komputera wskazuje inną kopię na D, a nie GitHub.
  Zdalna nazwa prowadząca do GitHub to `github`. Na drugim komputerze nazwy
  remote mogą być inne. Sprawdź `git remote -v`, pobierz gałąź z GitHub i
  zweryfikuj jej HEAD. Nie zakładaj, że samo otwarcie folderu odczyta zmiany
  z GitHub albo że lokalna ścieżka drugiego komputera jest identyczna.
- Na początku każdej pracy: `git status --short --branch`,
  `git branch --show-current`, `git log -n 12 --oneline`, `git remote -v`.
  Zachowaj cudze zmiany i worktree. Bez świadomej integracji nie rób merge,
  rebase, resetu, czyszczenia ani podmiany działającego programu.
- Jest wiele starszych worktree wydaniowych. Ich istnienie nie oznacza, że są
  aktualną gałęzią. Pracuj na wskazanym, czystym checkoutcie lub jawnie
  wybranym, izolowanym worktree, po porównaniu commitów i zmian.

## Co jest programem i gdzie leży kod

- `wxlite/amc_wx_lite/` — bieżący interfejs, model sesji i list, menu,
  dostępność, ustawienia, adaptery usług. `gui.py` łączy okno z nawigatorem;
  `shortcuts.py`, `command_palette.py`, `i18n.py` i model poleceń odpowiadają
  za zgodność skrótów, menu, palety i pomocy. `release_profile.py` definiuje
  dozwolone sesje, możliwości, nazwę, kanał aktualizacji oraz rozdzielenie
  danych Test/Public.
- `src/AccessibleMediaController.LiteHost/` — host multimediów i protokół
  z GUI; `src/AccessibleMediaController.Core/` — wspólny rdzeń;
  `src/AccessibleMediaController.Windows/` — integracja Windows.
  `src/AccessibleMediaController.PythonLauncher/` i `PythonUpdater/` prowadzą
  uruchamianie i aktualizację. Sonos i Apple Music mają osobne moduły hosta.
- `wxlite/tests/`, `tests/` — testy Python, hosta, protokołu, profili,
  instalatora i zachowania poleceń. `nvda-addon/` — dodatek NVDA.
  `installer/`, `deploy/`, `scripts/`, `build.ps1` — pakowanie i wydania.
- `docs/zadania/` jest dziennikiem decyzji i dowodów. Kod, zaliczone testy,
  build, instalacja, publikacja GitHub i odsłuch NVDA to **oddzielne etapy**.
  Nie nazywaj funkcji odebraną tylko na podstawie testów automatycznych.

## Interfejs i kontrakt dostępności

- AMC jest aplikacją Windows obsługiwaną przede wszystkim z klawiatury i NVDA.
  `Ctrl+Shift+S` otwiera Menedżer sesji. Włączone sesje mają zwartą kolejność
  `Ctrl+1–9` i `Ctrl+0`; Space przełącza dostępność sesji, Alt+góra/dół zmienia
  kolejność, Enter wchodzi do włączonej sesji. Wyłączenie nie kasuje konta,
  biblioteki, kolejki ani historii. Szczegóły: `AMC09-menedzer-sesji.md`.
- Jedna deklaracja polecenia i możliwości musi zasilać wykonanie, skrót,
  menu, menu kontekstowe, pomoc `Ctrl+F1`, pełną pomoc klawiatury i paletę
  `Ctrl+Shift+K`. Nazwy, opisy, widoczność i aktywność zależą od sesji,
  profilu i rzeczywistej możliwości. Nie dodawaj działającej funkcji tylko
  w jednym z tych miejsc. Testuj parytet i żywą nawigację NVDA.
- `F5` i `Ctrl+F5` mają znaczenie zależne od sesji; opis zawsze musi mówić,
  co zostanie odświeżone lub otwarte. `Ctrl+L` otwiera Bibliotekę, `Ctrl+U`
  Ulubione, `Ctrl+Shift+L/U` zmienia członkostwo tylko tam, gdzie adapter
  potwierdza zapis. `Ctrl+Q` czyta kolejkę; Enter, Escape, Backspace, lewa
  i prawa strzałka mają spójne znaczenie na listach i w relacjach.
- Nazwa dostępna kontrolki i wiersza musi być tekstem dla użytkownika.
  Pythonowe obiekty, enumy, ID, ścieżki modelu, nazwy właściwości i dumpy
  rekordów nie mogą trafić do mowy NVDA. Wybór liczby `1.00x` to prędkość
  normalna; wolniejsza i szybsza muszą być nazwane. Sprawdzaj pierwszą
  pozycję, strzałki, powrót fokusu po Zapisz/Anuluj i ponowne otwarcie.
- Testy okien przejmujących fokus, schowek lub aktywną aplikację są
  uciążliwe. Stosuj domyślnie testy celowane bez widocznego interfejsu;
  pełne próby GUI i NVDA wykonuj w uzgodnionym z użytkownikiem oknie.
  Nie otwieraj próbnego Apple Music podczas korzystania przez niego z pulpitu.

## Sesje i ważne decyzje produktu

- **Profile:** w bieżącym `release_profile.py` Test ma Pliki, Radio,
  Podcasty i YouTube, TIDAL, WiiM, Sonos, Spotify, Frontier Smart,
  Radio Archiwistę i eksperymentalne Apple Music. Public ma te same sesje
  poza Apple Music. Profile mają odrębne dane, poświadczenia i kanały
  aktualizacji `test` oraz `stable`. Starsze dokumenty mówiące, że Public
  nie ma Spotify lub Sonosa, zostały zastąpione. Weryfikuj profil także w
  manifeście i rzeczywistej zawartości paczki.
- **Pliki lokalne:** Foldery Biblioteki i lokalny odtwarzacz, kolejki,
  playlisty, zakładki, nagrania, harmonogramy, edycja audio. Odłączenie
  folderu nie usuwa plików ani zapisanej historii. Nie usuwaj kopii
  ratunkowych, nagrań ani danych użytkownika podczas porządków.
- **Radio i Radio Archiwista:** stacje, presety, wyszukiwanie, nagrywanie;
  Archiwista ma katalog audycji, odtwarzanie i pobieranie. Wydanie z 15.11
  dodało wybór folderu pobierania Archiwisty i naprawę Historii pobrań `Ctrl+J`.
  Odbiór NVDA tego zachowania jest otwarty. `RADIOBROWSER-001` opisuje
  kandydata katalogu Radio Browser, ale nie należy mylić źródeł i testów z
  instalacją. TuneIn pozostaje odłożony.
- **Podcasty i YouTube:** osobne źródła, skrzynka, biblioteka i pobieranie.
  Wydana 14.11 alpha.507 daje wybór taktu automatycznego odświeżania; `0`
  oznacza tylko ręczne F5/Ctrl+F5. Użytkownik zgłosił spowolnienie po
  `Ctrl+3`, nie całkowite zawieszenie. Nie zmierzono jeszcze kosztu natywnej
  listy i NVDA. `ApplyRefreshBatch` nadal wczytuje i zapisuje dużą bazę
  odcinków przy małej porcji; nie przedstawiaj modułu jako w pełni
  zoptymalizowanego. Szczegóły i pomiary: `POD-PERF-20261114`.
- **Spotify (sprostowanie 19.11.2026):** Soloist jest już wydanym, natywnym
  torem preferowanym, wybieranym po poprawnej konfiguracji i potwierdzonej
  sesji. Nie działa wewnątrz Chrome. Web Playback SDK w osobnym ukrytym
  systemowym Chrome pozostaje fallbackiem, gdy Soloist nie jest gotowy.
  Katalog i przeglądarka logowania OAuth są odrębne od obu silników audio.
  Aktualne źródła i nowsze zadania Soloist zastępują wcześniejszy opis
  Soloist jako wyłącznie przyszłego kierunku. Nie deklaruj Lossless dla
  Chrome ani faktycznego kodeka na podstawie samych preferencji Soloist.
  Ocena ewentualnego usunięcia fallbacku, konsekwencje i testy:
  `docs/zadania/SPOTIFY-LUNA-20261119-ocena-i-fallback-chrome.md`.
- **TIDAL:** katalog, historia, relacje, playlisty i natywne odtwarzanie
  mają osobne kontrakty. Pełny utwór i próbka nie mogą być mylone; poziom
  dostępu aplikacji wpływa na wynik. Stan i ograniczenia: `TID-001` oraz
  `PROJEKT_TIDAL_PL.md`. Zadania bitrate i przełączania Biblioteki nadal
  wymagają sprawdzenia na żywej usłudze/NVDA tam, gdzie tak zapisano.
- **Apple Music:** eksperymentalne tylko w Test. Biblioteka i katalog,
  wyszukiwanie czterech typów wyników, hierarchia, relacje i porcje po 150
  są w wydanych źródłach. `Ctrl+Shift+L` ma ograniczony kontrakt dla utworów
  i albumów. Oryginalną aplikację można otworzyć jawnie. Nie potwierdzono
  sterowanego z AMC odtwarzania dokładnie wybranego utworu; próby osadzonego
  odtwarzacza zostały wycofane. Żywy odsłuch i NVDA są otwarte.
  Czytaj kolejno `AM-001` do `AM-007`, zwłaszcza późniejsze korekty.
- **Sonos, WiiM, Frontier Smart, Cast:** lokalny Sonos jest już w Public;
  niedokończony broker konta Sonos nie jest tym samym. Sterowanie urządzeniami
  wymaga realnego potwierdzenia; testy na atrapach nie dowodzą działania
  sprzętu. `Shift+A`, `Shift+C`, `Shift+M` i pomoc muszą odpowiadać możliwości
  bieżącej sesji. Odrębne pliki zadań zawierają szczegóły.

Osobne automatyczne sprawdzanie nowości AMC, Spotify, TAccess i Ditto było
prowadzone w zadaniu Codex. Jego harmonogram i historia uruchomień nie są
częścią GitHub i nie przeniosą się automatycznie do Hermesa. Jeśli użytkownik
zleci Hermesowi monitoring, podstawą mają być istniejące rekordy AMC i
konkretne źródła, a nowe informacje należy dopisywać do właściwego zadania.
Samo wykrycie nowości nie upoważnia do instalacji, publikacji ani zmiany kodu.

## Stan wydania i instalacji odczytany 16.11.2026

- Ostatnie potwierdzone na GitHub wydania to
  `test-2026.11.15.2036` (prerelease) i `public-2026.11.15.2040` (stable).
  Obydwa zawierają poprawkę `DOWNLOADS-003`; Test zawiera także ostatnie
  Apple Music. Oba wskazują commit źródeł
  `2b6b061db22523ed9cfb66d26e2808b3d7bf00a7` i wersję techniczną
  `0.1.0-alpha.510`. GitHub pokazuje po trzy załączniki każdego wydania.
  Dokładne rozmiary i sumy SHA-256 są w `DOWNLOADS-003`.
- Rekord `DOWNLOADS-003` sporządzony 15.11 mówi, że lokalnej instalacji
  nie wykonano. **Nowszy odczyt z 16.11:** manifest w
  `D:\Projekty Codex\AMC Python\bundle-manifest.json` wskazuje profil Test,
  `sourceRevision=2b6b061d...`, `sourceDirty=false`, datę paczki
  `15.11.2026, 20:36` i `bundleId=c80cc46c`; pliki `AMC.exe` oraz updater
  mają czas modyfikacji z wieczora 15.11. Odczyt procesu Windows wskazał
  uruchomiony `AMC.exe` z tego folderu (PID 28124), start 16.11 o 02:14:28,
  po zapisaniu paczki. Potwierdza to użytkowy Test z tej instalacji D;
  nie potwierdza jeszcze odbioru pobierania ani NVDA. Public na tym
  komputerze nie był tu ponownie sprawdzany.
- **Korekta użytkownika z 16.11.2026:** użytkownik uważa, że ma już
  najnowszą wersję. Odczuwa wyraźną poprawę szybkości Podcastów i Radio
  Archiwisty. Pobierania z nowego wydania jeszcze nie sprawdzał. Ta nowsza
  obserwacja zastępuje założenie, że aktualizacja na pewno czeka. Osobny
  odczyt procesu potwierdził uruchomienie z folderu D; odbiór pobierania
  i NVDA pozostaje otwarty.
- `AM-007`: celowane 254 testy i 3 podtesty zaliczone; szeroki przebieg
  miał 2786 zaliczonych, 117 pominiętych i 20 niepowodzeń. Nie ukrywaj
  tych niepowodzeń ani nie przedstawiaj ich jako pełnej zielonej regresji.
  `DOWNLOADS-003`: 73 testy celowane, build hosta, kontrola obu paczek i
  instalatorów zaliczone. Odbiór klawiszy, fokusu i mowy NVDA jest otwarty.
- Przed każdą aktualizacją sprawdź odczytowo nagrywanie, harmonogramy i
  procesy, ale sam `yt-dlp.exe` nie dowodzi nagrywania: może służyć do
  sprawdzania kanału YouTube. Nie zamykaj AMC podczas nagrywania. Korzystaj
  z aktualnych decyzji w `AGENTS.md`, nie z wycofanego październikowego
  wymogu osobnej zgody na każde wydanie.

## Mapa bieżących i otwartych prac

Poniższe hasła są punktami wejścia, nie poleceniem równoczesnej realizacji.
Pełny spis 173 zastanych plików i sześciu rekonstrukcji jest w katalogu zadań.

| Obszar | Co ustalono i jaki jest następny sprawdzalny krok | Plik |
|---|---|---|
| Pobieranie | Wydanie Test/Public jest na GitHub; sprawdzić rzeczywisty proces, historię Ctrl+J, folder i fokus z NVDA. | `DOWNLOADS-003` |
| Apple Music | Alpha.509/510 zawiera hierarchię i katalog; sprawdzić żywą nawigację i odtwarzanie wybranego utworu bez przejmowania pulpitu. | `AM-007`, `AM-002` |
| Podcasty | Alpha.507 opublikowana; zmierzyć po wejściu Ctrl+3 listę, NVDA, zapis porcji bazy i wpływ taktu `0`. | `POD-PERF-20261114` |
| Kolejki | Kod i wydanie są, ręczny odbiór usuwania, przenoszenia i czasu nadal otwarty. | `QUEUE-002` |
| Spotify | Soloist jest preferowany po potwierdzeniu gotowości; Chrome pozostaje fallbackiem. Analiza usunięcia zakończona bez zmian kodu; Hubert korzysta z Chrome w AMC, więc zachowujemy ten tor. Tempo podcastów ma kod CDP, lecz blokadę GUI i wymaga osobnego eksperymentu. Analiza zgłoszeń zakończona; Michał zatwierdził wdrożenie YouTube #25, potem #26 i #28. Etap danych/trwałości #25 odebrano i zintegrowano w adf6e51; zaliczone celowane testy Windows, build i niezależny przegląd. Teraz ograniczony catch-up/harmonogram #25, bez instalacji ani publikacji. | `SPOTIFY-LUNA-20261119-ocena-i-fallback-chrome`, `A11Y-20261110-powiazania-status-spotify` |
| Radio Browser | Źródła i testy modelu gotowe, brak instalacji i odbioru NVDA; TuneIn odłożony. | `RADIOBROWSER-001` |
| TIDAL i historia | Celowane poprawki bitrate, relacji, członkostwa i linków zapisane; odbiór w instalacji sprawdzać osobno. | `RECOG-HIST-20261111`, `HIST-REL-20261109` |
| Dodatek NVDA | Jeden dodatek dla Test/Public i komunikaty NVDA+End wymagają zgodności z uruchomioną wersją oraz żywego odsłuchu. | `NVDA-DUAL-20261110`, `A11Y-20261110` |
| Sonos | Lokalny Sonos w Public; broker konta i testy urządzeń są osobnymi etapami. | `RELEASE-20261114-public-sonos-playlisty`, `AMC10-logowanie-sonos-i-publiczny-broker` |

## Porządek źródeł prawdy i niespójności

1. Najnowsza korekta użytkownika z datą w `AGENTS.md` lub w pliku zadania.
2. Bieżący kod, manifest konkretnej paczki i sprawdzony commit GitHub.
3. Rzeczywiste wyniki testów, instalacji i NVDA zapisane w rekordzie zadania.
4. Starsze plany i README — materiał historyczny, gdy są sprzeczne.

Przykłady: `HERMES_AMC_START_PL.md` sprzed tej aktualizacji opisywał stan
15 października i żądał osobnej zgody na publikację; zastępują go nowsze
decyzje z `AGENTS.md`. `README_PL.md` zaczyna się od alpha.504. Rejestr
zadań zawiera stary punkt wznowienia alpha.482, mimo późniejszych wpisów
alpha.510. Nie bierz pierwszego napotkanego numeru wersji za aktualny stan.

W katalogu zadań wykryto **sześć niedziałających lokalnych odsyłaczy** i
**52 pliki bez odsyłacza w rejestrze**. Dla sześciu odsyłaczy utworzono
rekordy oznaczone „odtworzony 16.11.2026”, wyłącznie na podstawie krótkich
opisów z rejestru. To naprawia nawigację, **nie odzyskuje pierwotnej treści**.
Historia Git wszystkich lokalnych referencji nie znalazła tych plików pod
wskazanymi nazwami. Pełna lista luk i materiałów pomocniczych jest na końcu
katalogu. Przed rozpoczęciem jednego z takich tematów odtwórz wymagania z
użytkownikiem i istniejących dowodów; nie przyjmuj, że rekonstrukcja
potwierdza nieznane testy lub dawne decyzje.

## Jak prowadzić każde kolejne zadanie

1. Odczytaj `AGENTS.md`, rejestr, jeden wskazany plik zadania i powiązany kod.
   Sprawdź datowane korekty oraz status Git i GitHub. Jeśli zadanie nie ma
   kompletnego pliku, uzupełnij go przed implementacją, z oznaczeniem źródeł.
2. Zapisz cel, zakres, stan zastany, jeden konkretny następny krok i
   kryteria odbioru. Nie przenoś prywatnych poświadczeń ani logów na GitHub.
3. Wdrażaj na bieżącej linii wxPython. Dla zmiany funkcji aktualizuj wspólny
   model poleceń, klawisze, menu, paletę, pomoc i dokumentację. Dodaj
   celowane testy parytetu oraz klawiatury/NVDA, gdy zmieniasz kontrolkę.
4. Zapisuj oddzielnie: zmienione pliki, commit, testy i ich rzeczywiste
   wyniki, build, instalację, uruchomienie, NVDA, GitHub. Przed wydaniem
   sprawdź profil, manifest, integralność, skan sekretów i stan nagrywania.
5. Po zadaniu uaktualnij jego plik i rejestr. Zapisz materiał do szkicu
   instrukcji i zmian; szkic zostaw użytkownikowi do korekty. Podaj
   użytkownikowi dokładny wynik i miejsca, gdzie może kontynuować.

## Tekst do przekazania Hermesowi

> Pracuj nad bieżącym AMC Python wxPython z repozytorium
> `https://github.com/michalkasperczak/AMC`, gałąź
> `codex/amc-test-public-20261109`. Najpierw przeczytaj
> `HERMES_AMC_START_PL.md`, `HERMES_AMC_PRZEKAZANIE_2026-11-16_PL.md`,
> `AGENTS.md`, `docs/zadania/README.md` i katalog wszystkich zadań.
> Sprawdź aktualny stan Git, manifest i nowsze korekty. Historię rozmów
> Codex traktuj jako niedostępną; kontekst jest zapisany w plikach. Czekaj
> na wskazanie konkretnego zadania i prowadź jego rekord po każdej zmianie.
