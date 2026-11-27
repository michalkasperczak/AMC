# QUEUE-004 — Shift+Enter: Odtwarzaj jako następne

Stan 27.11.2026: kod i testy celowane wykonane w bieżącym AMC wxPython. Test alpha.533 zbudowany w GitHub Actions i opublikowany jako prerelease. Żywy odbiór NVDA i fizyczny HEOS pozostają otwarte.

## Decyzja i zachowanie

Korekta z 27.11.2026 zastępuje wariant przełączający Shift+Enter opisany w wydaniu alpha.530 oraz wcześniejszy zapis w tym pliku, że klawisz jest wolny. `Shift+Enter` jednorazowo dodaje zaznaczone pliki, odcinki Podcastów albo nagrania Radio Archiwisty do bloku „Odtwarzaj jako następne”. Nie usuwa ich przy ponownym wskazaniu. `Ctrl+Shift+Enter` jest osobnym przełącznikiem dodaj/usuń. `Ctrl+Shift+Q` nadal przełącza zwykłą kolejkę.

Kolejne wywołania zachowują porządek: bieżący materiał → pierwszy dodany → drugi dodany → dawna następna pozycja. Wielokrotny wybór zachowuje kolejność widoczną na liście. Pozycja już czekająca jako następna jest bez zmian; bieżący materiał nie trafia ponownie do kolejki. Dodanie nie wywołuje pauzy ani zmiany odtwarzania. W oknie wyszukiwania lista i fokus pozostają w oknie. Główna lista zachowuje zaznaczenie.

Polecenie dotyczy zapisanych pozycji w Plikach, Podcastach i Radiu Archiwiście. Zwykłe Radio nie ma kolejki. Zdalne wyniki katalogu, TIDAL, Spotify, WiiM, Sonos, HEOS i Apple Music nie otrzymują tego skrótu: ich adaptery nie zapewniają tu zweryfikowanego, przewidywalnego wstawienia. HEOS czeka na test fizycznego głośnika; Apple Music pozostaje odłożone zgodnie z decyzją użytkownika. Nie uruchamiać ich w tej wersji pod Shift+Enter.

## Miejsca wykonania i komunikaty

- `wxlite/amc_wx_lite/shortcuts.py`: jeden model poleceń z nazwą, opisem i gestem; `menu_model.py`, `command_palette.py` i pomoc pobierają tę prezentację. Menu Biblioteka i kontekstowe pokazują polecenie w dostępnych sesjach. `Ctrl+F1` i pełna pomoc podają aktualny gest.
- `wxlite/amc_wx_lite/gui.py`: wykonanie z listy/odtwarzacza i zapisanych wyników wyszukiwania. Przy sukcesie komunikat „Odtwarzaj jako następne: {tytuł}” albo liczba elementów. Przy powtórzeniu: „Zaznaczona pozycja już czeka jako następna” lub „Wybrane pozycje już czekają jako następne”. Wyszukiwarka odmawia mieszanego wyboru sesji i pozostaje otwarta.
- `host_client.py`, `LiteEngineHandlers.cs`, `LiteQueueCoordinator.cs`: operacja `queue.addPlayNext` dla Plików. `LiteProfileMutationStore.cs`: trwały blok następnych odcinków Podcastów z opcją jednorazowego dodania. `radioarchiwista_source.py`: trwała, atomowa zmiana dla wielu nagrań.
- Model kolejki i identyfikatory pozostają wewnętrzne; nazwy odczytywane przez NVDA są etykietami użytkowymi.

## Weryfikacja

27.11.2026: testy Python dla skrótów, menu, palety, pomocy, wyszukiwarki, Plików, Podcastów, Radio Archiwisty i braku gestu w HEOS: 202 zaliczonych testów (w tym parytet palety, menu, pomocy i szablonu lokalizacji). Test `QueueCoordinatorTests` przeszedł: B → X → Y → dawna następna, duplikat i bieżący materiał bez zmiany odtwarzania. `dotnet build` hosta przeszedł, 0 ostrzeżeń i 0 błędów. Wybrany test logiki GUI kolejki przeszedł (1 zaliczony). Pełny `test_gui_logic.py` zgłasza niezwiązane błędy atrapy GUI, a `test_podcast_source.py` zawiesza się przed wynikiem; nie traktować ich jako zaliczonych. Pełny `test_i18n.py` ma istniejący brak pokrycia wielu starszych tekstów w angielskim inwentarzu. Docelowy test szablonu językowego przeszedł.

Nie przeprowadzono żywego testu NVDA: na głównym komputerze działa obecnie AMC Test z Radiem oraz NVDA. Przełączenie tej sesji zakłóciłoby odtwarzanie użytkownika. Potrzebny odbiór klawiaturą i NVDA: początkowe zaznaczenie, rozwinięte menu, strzałki, powrót fokusu, ponowne otwarcie, kilka pozycji i komunikaty. HEOS wymaga osobnego testu na głośniku. Te ograniczenia nie są potwierdzeniem instalacji ani wydania.

## Wersja, migracja i wydanie

AMC Test `0.1.0-alpha.533`. Zmiana nie migruje istniejących kolejek; zachowuje ich wpisy i dopisuje nowe w bloku priorytetowym. Commit źródeł i tagu: `7e8afda585a2b9dbf4a8050a59e80640bab49638`. GitHub Actions run `46078107196`: wszystkie etapy zakończone sukcesem, w tym kontrola rewizji/profilu/wersji, budowa paczki i instalatora, `secretScan=passed` oraz upload artifact `15235003886`. Instalator `AMC-Test-2026-11-27-2342.exe`: 214670480 B, SHA-256 `f41cd873e167d79cb9de2107e427fa656c0009b2e938f922e0efa20fb9c79147`. Plik jest w `D:\Projekty Codex\AMC - integracja wxPython\wydania\AMC-Test-alpha533-20261127`. GitHub prerelease `test-2026.11.27.2342`: tag wskazuje dokładnie commit źródeł, profil `test`, `draft=false`, `prerelease=true`, cztery załączniki. Zdalny digest/rozmiar EXE zgadzają się z lokalnym plikiem; trzy małe JSON-y pobrano z wydania i porównano bajt po bajcie. Manifest zaakceptował rzeczywisty walidator aktualizatora AMC. Instalator nie był uruchamiany ani instalowany. Kod źródłowy, testy, build hosta, build instalatora, publikacja i żywy odbiór pozostają odrębnymi stanami.

## Szkic instrukcji

Zaznacz plik, odcinek Podcastów albo nagranie Radio Archiwisty i naciśnij `Shift+Enter`. Materiał zagra po obecnym i po materiałach wcześniej dodanych tym skrótem. Bieżący będzie grał dalej. Ponowne naciśnięcie nie usunie pozycji. Aby przełączyć dodanie lub usunięcie, użyj `Ctrl+Shift+Enter`. Szkic wymaga korekty i zatwierdzenia przez użytkownika przed publikacją jako instrukcja.
