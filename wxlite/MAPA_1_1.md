# AMC Python — zakres względem pełnego AMC

## 07.10 — Opcje sesji: domknięty przyrost, przed dostawą

Kod bbe40bcd i końcowa korekta rodzica w tym commicie. Wspólne wejście
Dźwięk → Opcje sesji oraz Ctrl+Alt+Enter. Wybór konfigurowanej sesji
nie przełącza odsłuchu. Pliki: normalizacja, przejścia, cisza i zachowanie
po wyjściu z odtwarzacza; Radio: tylko zachowanie po wyjściu.

Rodzic potwierdził na pełnej prywatnej kopii (11207 rekordów) i żywym NVDA:
natywny skrót z listy, odtwarzacza i pola filtra; Zapisz/Anuluj oraz powrót
fokusu; odmowę dysku bez fałszywego sukcesu i z przywróceniem ustawień
silnika; użycie zapisanego nadpisania przy nowym procesie. Rzeczywiste
odtwarzanie syntetycznego WAV: Escape przy ON wywołał pauzę, przy OFF
pozostawił odtwarzanie do naturalnego końca. Nie oceniano słuchowo audio.

Końcowy runner:1062/0/0, protokółC#:9 zestawówOK, w tym378 payloadów
parsowanych przez prawdziwy model;1 istniejący przypadek zależny od profilu
produkcyjnego pominięty naWSL. Parser nie jest nazywany pomiarem odtwarzania.
Kwity `amc_pomoc/wx-session-options-20261007/final/`.

To nie pełny port ani zakończenie autoAdd/searchEnter. Dotychczasowa
zainstalowana próba ecc9ab68 nie zawiera tego przyrostu do osobnej dostawy.

Stan historyczny sprzed dostawy ecc9ab68 (nie bieżąca instalacja): odebrany kod: `4fbc49b085a2fffa72a28d3aef7b5e19cf496d6c`. Dostarczona paczka na głównym pozostaje oparta na `5e1f2d4ed07503f7b9399f89aaf7143f5e5a1b71`, wraz ze statusem i schowkiem z `96b04131`. **To nadal rozwijany pełny interfejs równoległy, nie ukończony odpowiednik 1:1.** Nazwa katalogów `wxlite` jest pozostałością wcześniejszego prototypu. Nowsza dokumentacja nie zmienia SHA odebranego kodu ani gotowej paczki.

## Opcje sesji — kandydat bezokienny (ten przyrost)

Wspólne wejście do opcji odtwarzania **wybranej sesji**: pozycja
`Dźwięk → Opcje sesji…` i `Ctrl+Alt+Enter` (lista i odtwarzacz).
Dialog pokazuje WYŁĄCZNIE opcje, które dana sesja naprawdę wykona:

| Opcja | Pliki lokalne | Radio | Dlaczego |
|---|---|---|---|
| Normalizacja głośności | tak | **nie** | `RadioMediaOutput` nie ma wykonawcy |
| Łagodne przejścia | tak | **nie** | to samo |
| Cisza między utworami | tak | **nie** | radio nie ma granicy utworów |
| Po wyjściu z odtwarzacza | tak | tak | polityka okna, nie wyjścia audio |

Radio dostaje więc jedną kontrolkę, a nie cztery z trzema martwymi.
Tempo/wysokość i pozycja startowa **nie** są tu wystawione — port nie ma
dla nich wykonawcy per sesja.

Każde pole ma wariant „Jak ustawienie ogólne”, więc wybór jest odwracalny;
dziedziczenie to `None`, nie fałsz. Wariant dziedziczony przy wstrzymaniu
mówi wprost, co z niego wynika (port `PlayerExitPausePolicy`).

Trwałość jest **prywatna** (`session_overrides` w `state.json` portu).
Wspólny profil AMC/SQLite pozostaje tylko do czytania. Zapis następuje
dopiero po zgodzie silnika: odmowa nie daje słowa „zapisano” i nie zostawia
wpisu. Stary plik stanu bez tej sekcji wczytuje się bez zmian; wartości
spoza reguł silnika wracają do dziedziczenia, obce nazwy sesji odpadają.

Runner projektu: **968 PASS / 0 FAIL / 0 SKIP**. Testy protokołu C#
(8 zestawów) przechodzą na Linuksie, w tym `SessionOptionsPayloadTests`,
który czyta **378 payloadów wygenerowanych przez port** prawdziwym
`LiteAudioSettings.Read`.

**GRANICA:** to dowód bezokienny. Dialog jest wykonywany na atrapach wx —
dobór kontrolek i odczyt wyborów są zmierzone, ale **żywy NVDA i prawdziwe
wxWidgets nie zostały tu użyte** (w tym środowisku nie ma wxPython).
Odbiór z czytnikiem pozostaje do wykonania osobno.

**ZALEŻNOŚĆ OTWARTA:** pola „dodawaj automatycznie” i „Enter w wyszukiwaniu”
świadomie NIE są tu wystawione, bo port wyszukiwania i bezpieczny zapis
członkostwa ich jeszcze nie wykonują. Ich uzgodniony pełny zakres **pozostaje
otwarty** — to odłożenie, nie rezygnacja ani zmiana decyzji.

## Wspólny kandydat Radia — 9b947e44

Widoki Radia, zapisane porządki i odebrana osobno opcja pozycji są scalone
w `9b947e4455592bb82d15a457e454710ccb97f534`. Pełny runner projektu:
**915 PASS / 0 FAIL / 0 SKIP**, z rzeczywiście zbudowanym serwerem testów
protokołu. Dotyczy to testów bezokiennych, nie żywego NVDA nowych widoków.
Jeden taki odbiór został rozpoczęty; raport po wykonaniu trafi do
`amc_pomoc/wx-radio-activity-20261006/live/ACCEPTANCE.md`.
Stan scalenia i poprawka porządku pierwszego wejścia: `PARENT-ORDERING.md`
w tym samym katalogu dowodów. WPF D1/D2 mają odrębnego wykonawcę i nie są
zaliczone wynikiem Pythona. To jeszcze nie dostawa nowej próby na główny.

## Kandydat: opcjonalna pozycja stacji

Menu Radio → „Odczyt pozycji stacji na liście” zapisuje wybór w prywatnym
profilu interfejsu Python. Domyślnie wyłączone. Nie dodano nowego skrótu.
Ukrywanie wymaga aktywnego dodatku AMC do NVDA 0.4.0; samo ustawienie w AMC
nie dowodzi działania wyłączonego lub starszego dodatku. Inne czytniki
pozostają przy własnej obsłudze pozycji.

Żywy odbiór na Hermesie potwierdził nazwę bez licznika, jego powrót po ON,
przełączanie bez wyjścia z listy, filtr i zachowanie liczników Plików.
Rodzic potwierdził końcowe menu bez dodatkowego skrótu, zapis i komunikaty.
Test prawdziwego handlera na Windows obejmuje też odmowę zapisu i przywrócenie
zaznaczenia menu. Nie zmieniono globalnych ustawień NVDA.
Kwity: `amc_pomoc/wx-radio-position-option-20261006/live/ACCEPTANCE.json`
oraz nadrzędne `parent-final/ACCEPTANCE.md`. Mechanizm jest już scalony,
ale jeszcze nie dostarczony w paczce na głównym komputerze.

## Biblioteka, Historia i krótki odczyt radia — odbiór na Hermesie

Na `4fbc49b0` odebrano trzy zmienione zachowania fizycznymi gestami z NVDA:

- Biblioteka radia: 55 wierszy zamiast całego cache 165 stacji w użytej pełnej prywatnej kopii. Rodzic potwierdził zgodność całego zbioru ID z flagą członkostwa, nie tylko liczników.
- Czytnik na radiu mówi nazwę i pozycję, np. „Poznań 16 z 55”, bez rodzaju „stacja” i bez adresu. Filtr ograniczył listę do jednej pozycji. W tamtym przyroście brakowało opcji wyłączania pozycji; jej późniejszy kandydat jest opisany wyżej.
- Ctrl+H pozwala odtworzyć dostępny plik spoza Biblioteki. Rzeczywisty host pokazał rosnący czas 1,24–8,27 s przy długości 12 s; pauza i Escape zachowały wybrany wiersz. Członkostwo pozostało wyłączone.

Odbiór dotyczy próbnego środowiska Hermesa, jeszcze nie dostawy tego przyrostu.
Raport: `amc_pomoc/wx-library-compare-20261006/live/ACCEPTANCE.md`,
weryfikacja rodzica: `live/PARENT-VERIFIED.json`. Kontrolka Wszystkie pliki
miała 2482 wiersze, ale snapshot zachował 400 komórek; brak badanego ID w
pełnym zbiorze uzupełniono rzeczywistym loaderem tych samych źródeł na
niezmienionej bazie. Nie nazywamy tego drugim pełnym odczytem GUI.

Nowe osobne widoki Ulubionych i Historii **radia** oraz kontrolki Opcji sesji
pozostają kolejnymi etapami. Odbiór lokalnego Ctrl+H nie zalicza Historii
nagrywania WPF ani jej naturalnego następnego pliku.

## Kandydat: osobne widoki Radia

Ctrl+L, Ctrl+U i Ctrl+H mają wspólną drogę z menu/F1 oraz osobny cel sesji.
Biblioteka filtruje isInLibrary, Ulubione wyłącznie isFavorite, Historia
rozwija zapisane ID z SQLite w całym cache radia. Stacja spoza Biblioteki
może pozostać w Ulubionych lub Historii; żaden odczyt nie podnosi flag.

`radio_views.py` jest warstwą danych. `OpenLibraryView.target_session_id`
kieruje dispatcher do `_open_radio_view`; `Navigator.apply_radio_view`
przechowuje wybory i klucz widoku osobno od Plików. Opóźniona odpowiedź
nie przemawia w innej sesji. Błąd zachowuje starą listę. Ctrl+L/U/H działa
w resolverze Radia także z PLAYER, a Backspace wraca do Biblioteki stacji.

Wykonany test połączenia używa prawdziwego loadera i zapisanych danych
syntetycznych, ale atrapy wx i zamiaru PlayStation, nie fizycznego GUI/audio.
Python:855PASS/0FAIL/15SKIP (brak DLL testów protokołu w tym worktree).
Raport: `amc_pomoc/wx-radio-activity-20261006/PARENT-INTEGRATION.md`.

Pozostaje żywy odbiór finalnego połączenia. To kandydat, nie funkcje już
odebrane lub dostarczone. Prywatna lista lite-home bez danych pełnego
profilu uczciwie odmawia Ulubionych/ Historii zamiast udawać pusty wynik.

Zapisane porządki Biblioteki i Ulubionych są już **czytane**, nie są już
kolejnością cache'u. Tryb pochodzi z `CollectionSortModes` profilu
(domyślnie `AddedNewest`), zapis z czterech tabel `library.db` przez
rozszerzoną whitelistę `_stored_order`; `AddedNewest` odwraca, `Custom` nie,
`Alphabetical` idzie kluczami `HostCollation`, nie `casefold`. Zmiana trybu
i jakikolwiek zapis pozostają poza zakresem — czytamy wybór zapisany przez
AMC. Brak zapisu lub hosta nie zabiera listy: zostaje kolejność cache'u i
komunikat „kolejność zastępcza". Na prawdziwym profilu (kopia
`wx-full-profile-after421`) oba widoki mają zapisany `Custom`, 55 pozycji,
kolejność zgodna z zapisem i rozdzielna między widokami, pliki nietknięte.
Nowy `tests/test_radio_saved_order.py` 21/21. Wynik autora336/0/0 pochodzi
z `unittest discover`, które pomija funkcje testowe — NIE był pełnym wxlite.
Rodzic uruchomił runner projektu, dopasował starą atrapę do prawdziwego DTO
i wykazał osobnym RED brak zapisanego porządku po samym starcie, przed Ctrl+L.
Start zleca teraz właściwy odczyt także dla Radia, bez blokowania GUI i bez
nadpisywania wyboru dokonanego podczas oczekiwania. Wąskie154/0/0; pełny
przebieg wspólnego kandydata915/0/0 opisano na początku. Żywego NVDA
dla tych nowych widoków jeszcze nie było. Raport autora: `ORDERING.md`;
uzupełnienie rodzica: `amc_pomoc/wx-radio-activity-20261006/PARENT-ORDERING.md`.

## Poprawki plików — odebrane z rzeczywistym GUI i NVDA

Ctrl+O otwiera plik, Ctrl+Shift+O folder. Escape z listy najpierw czyści
aktywny filtr, bez filtra wraca wyżej; z odtwarzacza wraca na listę.
Nie ma rutynowego „Wczytywanie Biblioteki”. Home/End w odtwarzaczu mają
kontrakt oryginału: początek i dziesięć sekund przed końcem.

Ctrl+L w Plikach wraca z Ulubionych i Historii do ostatnich Folderów lub
Wszystkich plików oraz właściwego zaznaczenia. Ostatni tryb otwarty w tym
oknie ma pierwszeństwo przed starym wspólnym zapisem. Tryb Kolejność własna
nie jest jeszcze przeniesiony. Nowa lokalna komenda nie otwiera plików w Radiu.

Końcowy Python: **799 zdanych, 0 błędów, 0 pominiętych**. Osobno odebrano
prawdziwe dialogi pliku i folderu, anulowanie z powrotem fokusu, rzeczywiste
PLAYER→LIST przez Escape, pustą listę z fokusem, Home/End i powroty Ctrl+L.
NVDA+End odczytuje pasek na obu widokach bez wywołania seek. Sam zastępczy
ShowModal=ID_CANCEL ani próba LIST→LIST nie były uznane za właściwy dowód;
brakujące drogi uzupełnił rodzic na tym samym kodzie.

Nadrzędny raport: `amc_pomoc/wx-file-keys-parity-20261006/parent-final-live/ACCEPTANCE.md`.
Paczka została dostarczona do dotychczasowego folderu próby na głównym
komputerze. Obie stare instancje zamknięto normalnie; działa jedna nowa,
z zachowaniem najnowszych ustawień. Parent potwierdził569plików manifestu
i zgodność kodu5e1. Raport: `amc_pomoc/wx-file-keys-parity-20261006/delivery/PARENT-DELIVERY.md`.
To nie dostawa późniejszych zmian Biblioteki ani Opcji sesji.
Niżej zachowana historia wcześniejszych odebranych przyrostów.

## Działa i zostało odebrane

- Naturalny start wxPython z rzeczywistym hostemC#, nie ręczne wypełnienie okna.
- Biblioteka czytana z bieżącego SQLite w trybie `mode=ro`, z WAL. Przy błędzie nie przechodzi samowolnie na stary obraz `immutable`.
- Foldery, wejście i powrót z zachowaniem wyboru, identyfikatory jako napisy.
- Wszystkie pliki alfabetycznie: Alt+2.
- Lokalne Ulubione: Ctrl+U, kolejność według dodania lub własna w warstwie danych. Nie deklarujemy jeszcze alfabetyki Ulubionych.
- Katalog playlist: Ctrl+P; Enter otwiera zawartość, Backspace wraca do zaznaczonej playlisty.
- Historia odtwarzania: Ctrl+H (zgodnie z `CommandIds.ViewHistory`, `MainWindow.xaml:481`). Etykieta mówi „Historia odtwarzania" i jest to ODCZYT zapisanej historii AMC, nie historia własnego odtwarzania tego okna. Wiersze filtrowane po samej DOSTĘPNOŚCI (`is_available = 1`), więc dostępny plik otwarty bez dodania do Biblioteki też ma wiersz; niedostępny nadal nie. Widoki członkostwa (Wszystkie pliki, Foldery, playlisty, zapisana kolejka) filtrują dalej po `ActiveLocalItems` (`is_available AND is_in_library`).
- Biblioteka radia: wiersze pochodzą wyłącznie ze stacji z `isInLibrary == true`. `radio.stations` to trwały cache całej sesji radia (wyniki katalogu, jednorazowe strumienie, stacje zdjęte z Biblioteki) — samo zapisanie wpisu w profilu nie oznacza członkostwa. Brak pola = poza Biblioteką (`RadioStationSettings.IsInLibrary` to `bool` bez inicjalizatora). Nic nie jest z profilu usuwane, wpisy spoza Biblioteki są tylko niepokazywane.
- Kolejka Ctrl+Q: początkowo odczyt zapisu profilu, po inicjalizacji stan hosta, także po wyczerpaniu. Enter w zapisanej kolejce przesyła flagi członkostwa i kolejność, Enter w żywej wykonuje tylko `queue.playAt`. Naturalne B→A→C, samoaktualizacja otwartej listy i trwale pusta kolejka są odebrane. Bieżący utwór i czas również przeżywają restart prywatnej kopii; zapis nadal należy wyłącznie do C#.
- Dwa ODDZIELNE widoki zakładek, dwa skróty, dwie akcje:
  - **Ctrl+B** = `Action.VIEW_ALL_BOOKMARKS` → `all_bookmark_rows` (`BookmarkIndex.GetForDisplay`): WSZYSTKIE zakładki z bieżącym materiałem sesji na początku. Tak jak `CommandIds.ViewBookmarks` w C#.
  - **Ctrl+Shift+B** = `Action.VIEW_ITEM_BOOKMARKS` → `bookmark_rows` (`GetForItem`): zakładki JEDNEGO wybranego pliku. Ten skrót jest nasz, bo w C# ten węższy zakres nie ma własnej komendy.

  Wcześniej Ctrl+B był świadomie wolny — podpięcie pod niego węższego `GetForItem` byłoby podstawieniem węższego zakresu pod istniejącą szerszą komendę. Po dołożeniu `all_bookmark_rows` ten powód zniknął: Ctrl+B dostał zakres, który ma w AMC.
- Kontekst widoku zbiorczego to **`CurrentSession.CurrentItem`**, nie zaznaczony wiersz. Materiał w PAUZIE nadal jest bieżący — `CurrentItem` nie jest tym samym co `IsPlaying` (zmierzone: `status_text="Wstrzymano"`, a Ctrl+B dalej stawia zakładkę tego pliku u góry).
- Tożsamość materiału: dane mają profilowy `Id` (`local-...`), host potrafi zwracać `file:<path>`, a wiersz zakładki ma `bookmark:<id>`. Żaden z tych prefiksów nie zastępuje profilowego `Id` — kontekst bierzemy po POTWIERDZONYM starcie, nie po samym zaznaczeniu ani nieudanej próbie.
- Enter na zakładce przechodzi FIZYCZNIE do materiału i pozycji: `play.file` dostaje `positionSeconds` w jednym wywołaniu, bez osobnego seeka po starcie. `Row.item_id` zakładki (`bookmark:<id>`) nigdy nie trafia do backendu jako plik — ścieżka i pozycja idą z mapy celów, a `BookmarkRow.item_id` jest ID pliku.
- Pozycja liczona jako `position_ticks / 10_000_000` z zachowaną częścią ułamkową (bez `//`).
- Backspace z widoku zakładek wraca na TEN plik (zmierzone: wiersz 2309 z 2476, to samo ID), nie na wiersz pierwszy.
- Foldery Biblioteki są w menu (Alt+1), a „Powrót na listę” wywołuje istniejące SHOW_LIST. Prawdziwy PLAYER→LIST przez menu został odebrany w `all-bookmarks-gui-after422/parent-acceptance.json`; wcześniejszy niepełny P13 nie jest dowodem tego przejścia.
- Korekta 09.10.2026: wcześniejszy odbiór `MediaListAccessible` nie obejmował później zgłoszonej regresji zwykłych wierszy. Na rzeczywistym stanowisku nakładka podawała nazwę samej listy, ale zasłaniała jej natywne dzieci: strzałki i Enter działały, a NVDA nie czytał nazw. Wszystkie własne mechanizmy dostępności list zostały wycofane: `MediaListAccessible`, sztuczne zdarzenie fokusu pustej listy, znaczniki HWND oraz nakładka `radioList` dodatku NVDA. Etykiety użytkowe pozostają przez zwykłe `SetLabel`/`SetName`, zaś całe drzewo wierszy i informacja o pozycji należą do natywnego providera wx/Windows. Harmonogramy i presety również pozostają zwykłymi kontrolkami wx bez własnego providera. Wymagany jest ponowny żywy odbiór niepustej i pustej listy.
- Dalsza korekta 09.10.2026: rzeczywistą przyczyną utrzymującej się ciszy był odczyt `GetItemState(0)` przed wstawieniem pierwszego wiersza. wxMSW podnosił asercję, `sync_rows` kończyło się przed `InsertItem`, a Enter nadal działał na osobnym modelu. Dodano sprawdzenie `index < GetItemCount()` przed odczytem stanu. Odizolowana próba na tym samym wxPythonie potwierdziła trzy natywne wiersze, wybór i fokus na 0 oraz prawidłowy tekst pierwszego wiersza. Mechanizmy dostępności pozostają natywne.

## Mowa listy — odebrany zakres i pozostałe ograniczenia

- Wszystkie istniejące listy korzystają ze wspólnego LC_REPORT i `list_sync`: brak zmian = zero operacji, zmiana pola nie przepisuje reszty. Pełna podmiana danych jest bramkowana zmianą widoku, bez rekreacji HWND. Końcowy żywy odbiór potwierdził pojedynczy odczyt i prawidłową nazwę po Foldery/Playlisty → Wszystkie pliki, strzałki, rzeczywiste puste zakładki i powrót. Dane nie są obcinane ani stronicowane. To wynik pomiaru, nie wniosek z samej zmiany klasy kontrolki.
- Radio czytane z aktualnego `radio.stations` profilu. Brak/awaria odczytu nie jest już traktowana jak prawdziwie pusta lista. Wspólny profil nadal nie jest edytowany przez Python.
- Ctrl+C kopiuje nazwę. Ctrl+Shift+C kopiuje plik w systemowym formacie FileDrop, a Ctrl+X przygotowuje go do przeniesienia. Źródło znika dopiero po udanym wklejeniu, nie przy samym wycięciu. Rzeczywiste kopiowanie/przenoszenie i zgodność SHA oraz mowa zostały odebrane; raport `amc_pomoc/wx-status-clipboard-live-20261005/parent-recovery/ACCEPTANCE.md`. Nie utożsamiać tego z implementacją przeciągania myszą.
- Ctrl+Shift+O otwiera natywny dialog folderu; Ctrl+O otwiera dialog pliku (zgodnie z MainWindow.xaml:42-49 — ten pomiar wykonano, gdy port miał oba gesty odwrotnie). Odtwarzanie z normalnej listy, pauza i wznowienie Spacją: potwierdzony czas i sygnał wyjściowy.
- Przycisk transportu nazywa czynność: Odtwórz albo Wstrzymaj. Zmiany i krótkie komunikaty zostały odczytane w Podglądzie mowyNVDA.
- Nieznana długość jest pokazywana jako brak („łączny czas nieznany"), nie jako `0:00`. Wzorzec zC# `MediaItemFormatter.FieldValue`: przy `Duration <= TimeSpan.Zero` pole jest pomijane. Prawdziwe czasy (np. `5:03`) zostają.
- Pasek menu wxPython udostępnia działające już funkcje: sesje, otwieranie pliku/folderu, widoki Biblioteki, odtwarzacz/lista, transport, kopiowanie i pomoc. Każda pozycja woła ten sam `MainWindow._dispatch`, co skrót — bez drugiej implementacji. Zachowane menu Dźwięk/ustawienia. Pozycje niedostępne w kontekście (np. lokalne Ulubione w Radiu) są wyszarzane, nie ukrywane.

## Zakres pomiaru

Pełna zachowana kopia danych zawiera11200rekordów lokalnych, z których2475spełnia filtr aktywności. W próbie były9Ulubionych,1playlista/54pozycje i165stacji. To liczby badanej kopii, nie dzisiejszy odczyt głównego komputera ani stałe w kodzie.

Końcowy zachowany runner:**444zdanych,0padniętych,0pominiętych**. Odbiór rodzica:23gesty z prawidłową bramką,19niepustych przyrostów mowy; pomocniczeTab i ruch na jedynymwierszu nie muszą zmieniać odczytu. Na nowych widokach wykonano rzeczywisteAlt+2/Ctrl+U/Ctrl+P/Enter/Backspace, nie wywołania handlera.

Widoki aktywności (historia/kolejka/zakładki) zmierzone na żywymNVDA w `amc_pomoc/wx-full-profile-after421/activity-gui-after422/`: Ctrl+H→119wierszy, Ctrl+Q→6, Ctrl+Shift+B na wybranym pliku→1, na innym pliku→„Zakładki, pusto". Menu Alt+B+H/K/Z dało te same widoki ze stanu różnego od wyniku. Skok zakładki dowiedziony pozycją z ŻYWEGO hosta: start na≈83,4s przy długości120,06s (odczyty91,2→100,2s w kolejnych sekundach). Materiał jest SYNTETYCZNY — wygenerowany plik i wstrzyknięty rekord w OSOBNEJ prywatnej kopii pełnej bazy, nie plik użytkownika.

Próba mowy i menu po tej zmianie: `amc_pomoc/wx-full-profile-after421/menu-and-speech-after422/proba-koncowa.json`. Menu obchodzono rzeczywistymAlt/strzałkami/literami naWindows zNVDA; schowek czytano zWindows i przywrócono do stanu sprzed próby.

Dźwięk zmierzono wcześniej na jawnym syntetycznymWAV: sygnał występuje podczas grania, zanika w pauzie i wraca po wznowieniu. To pomiar wyjścia, nie ludzka ocena jakości brzmienia. Narrator nie został odebrany w tych przebiegach.

Dowody robocze pozaGit: `amc_pomoc/wx-full-profile-after421/integrated-after422/parent-closure/` oraz `library-gui-after422/parent-acceptance/`.

## Sortowanie

Foldery zachowują dotychczasoweAMC_PL (`IgnoreCase|IgnoreNonSpace`). Wszystkiepliki korzystają z osobnego trybu tytułów bez ignorowania akcentów i natywnego klucza ścieżki OrdinalIgnoreCase. Cache trybów są oddzielne, wsady mieszczą się w limicie64KiB. Klient wykrywa stary host nieobsługujący nowego trybu.

Rogié/e orazß/ss były odtworzone jakoRED i poprawione. LiteHost został następnie zbudowany naWindows; trzy tryby wywołano w rzeczywistym procesie. Pomiary dotycząpl-PL; taką kulturę odczytano też na stanowiskuWindows. Nie rozciągamy wyników na niezmierzone kultury.

## Zbiorczy widok zakładek w interfejsie — zmierzony odbiór

Kwity: `amc_pomoc/wx-full-profile-after421/all-bookmarks-gui-after422/`
(`odbior-zakladek.json`, `odbior-zakladek-bcd2.json`, `odbior-zakladek-d.json`,
`REPORT.md`). Żywe okno, własny profil `profile-bookmark`, żywy host,
odczyt mowyNVDA przez podgląd.

Zdane:

- **Ctrl+B**: wejście fizyczne z Wszystkich plików (2476 wierszy) → `LibraryView.ALL_BOOKMARKS`, `rows=30` == 30 zakładek odczytanych z bazy, rola 15, mowa „Biblioteka — Wszystkie zakładki, 30 pozycji, kolejność zastępcza".
- **Bieżący materiał sesji, także w pauzie**: zwykły Enter → PLAYER (`files.play` BEZ `positionSeconds`), Spacja → „Wstrzymano", Ctrl+B z pauzy → zakładka tego pliku u góry, zgodnie z wynikiem `all_bookmark_rows(current_item_id=...)`.
- **Lokalny skok**: dokładnie **jeden** `files.play` z `positionSeconds=83.456` (== `position_seconds` z danych), **zero** wywołań `*seek*`. Dziennik wywołań hosta w `host-calls.jsonl`.
- **Odmowa sesji nielokalnej**: Enter na wierszu Spotify → brak `files.play`, zostajemy na wierszu, mowa „Ta zakładka należy do sesji Spotify. Ten program odtwarza tylko pliki lokalne."
- **Powrót PLAYER→LIST menu**: `alt` (pasek menu, rola 11) → Widok → „Powrót na listę" → `session_view=View.LIST`, `player_shown=false`, wiersz czytany. Menu „Lista→Lista" nie jest dowodem tego przejścia — do jego pokazania obserwator dostał pole `session_view` (`library_view` nie rozróżnia odtwarzacza od listy).

### Historyczne obserwacje list — zamknięte przed obecną kolejką

Puste listy, podwójny odczyt i stare nazwy zostały odebrane po zmianie wspólnego mechanizmu list w `14bcc285`; dawne kwity powyżej nie otwierają ich ponownie. `library_view=None` oznacza poprawny widok Folderów, nie brak implementacji. Aktualne dowody wskazują początkowe sekcje tej mapy oraz `native-w02-valid-retest-after422/parent-final/`.

## Filtr listy (Ctrl+K) — historyczny odbiór przed wyciszeniem zapowiedzi

Poniższy zapis opisuje ówczesny pomiar. Obecny kod nadal filtruje, ale podczas
pisania NIE ogłasza automatycznie liczby wyników. Aktualne wymaganie i odbiór
zastępują opis mowy z punktu Ctrl+K poniżej; nie przywracać dawnej zapowiedzi.

Skrót jest lokalny dla widoku (`Ctrl+K`, nie `Ctrl+F`). Odebrane na pełnej kopii profilu
(2478 pozycji „Wszystkie pliki", 13 folderów, 165 stacji) w `search-filter-after-resume/final-recovery/`,
mowa czytana z Podglądu mowy NVDA, nie z `Announcer.say`:

- Start: fokus na LIŚCIE, nie w polu (`filterBoxHasFocus=false` w kwicie).
- `Ctrl+K` → „Filtr listy"; pisanie oznajmia „Wyniki filtrowania: N" (2478 → 1672 → 577 → 301).
- `Enter`/`Down` z pola oddaje fokus WYNIKOM; `Escape` czyści filtr („Filtr wyczyszczony") i wraca na listę.
- Brak wyników: fokus ZOSTAJE w polu i czytnik mówi odmowę
  („Brak wyników filtrowania. Zmień tekst lub naciśnij Escape, aby wyczyścić filtr").
- Wybór przechodzący przez filtr zostaje (plan A/E: „Kazania Dominikanie Grobla … 1 z 1").
- Filtr jest własnością WIDOKU: Foldery trzymały `o`, „Wszystkie pliki" trzymały `kaz`
  i każdy widok wracał do SWOJEGO tekstu (plan J, `filter.restore` z kontekstem).
- Radio (165 stacji) i widoki Biblioteki idą TĄ SAMĄ ścieżką — bez drugiego silnika.

ZMIERZONY i naprawiony w tym odbiorze defekt: `Backspace` w polu filtra nie kasował znaku,
tylko wychodził o poziom wyżej („To jest folder najwyzszego poziomu", `navigation.py:542`).
Przyczyną NIE była bramka klawiszy — ta działa. Pozycja menu „Folder nadrzędny" miała
etykietę z `\tBack`, z czego wx budował AKCELERATOR OKNA, szybszy od kontrolki z fokusem.
Oddanie klawisza przez `DoAllowNextEvent()` nic nie zmieniło (sprawdzone na żywo i wycofane).
Naprawa: `MenuItem.accelerator=False` dla `Back`, `Space` i `Delete` — skrót zostaje WIDOCZNY
w nazwie pozycji (czytnik go mówi), ale nie jest akceleratorem. Kontrdowód: `Backspace`
NA LIŚCIE nadal wychodzi do folderu nadrzędnego. Efekt uboczny: zniknęło ostrzeżenie
„Unrecognized accel key 'Spacja'" (puste `boot-stderr.txt`).

Granica, której NIE sprawdzono: równoważność `casefold()` z `CurrentCultureIgnoreCase`
dla całego Unicode nie jest dowiedziona. Zmierzone są zwykłe polskie znaki; nie dopisywano
własnej normalizacji diakrytyków.

## Najbliższe braki

- Mechanizm wszystkich dotychczasowych list jest odebrany; nie powtarzamy zamkniętego audytu W02. Pozostaje wdrażanie kolejnych funkcji przez ten sam mechanizm.
- Dalsze widoki Biblioteki i wyszukiwanie. Filtr listy `Ctrl+K` jest odebrany (sekcja wyżej). Historia i oba zakresy zakładek są odczytem. Żywa kolejka jest odebrana na pełnej kopii przez rodzica (`parent-speech-full-profile/`): naturalne B→A→C, C wybrane PRZED przejściem i zachowane, samoaktualizacja otwartej listy, prawdziwa mowa NVDA oraz 0 po wyczerpaniu i ponownym Ctrl+Q. Trwała kolejność jest odebrana na prywatnej kopii (sekcja niżej); czas wznowienia jest już odebrany; nadal brak mutacji zakładek i bezpiecznego wspólnego pisarza ze starym WPF.
- Radio: oddzielenie Biblioteki według `isInLibrary` oraz krótki odczyt pozycji i jego wyłącznik są **odebrane** (sekcja „Scalone Radio i lewa strzałka” niżej). Historia nagrywania scala teraz wpisy prób z dostępnymi plikami oznaczonymi w `library.db`, także spoza zwykłego członkostwa Biblioteki; nie dubluje tej samej ścieżki i nie zmienia profilu.
- Zapis Ulubionych, playlist, kolejności i pozostałego stanu przez jednego właścicielaC#.
- Podcasty i YouTube mają już wąski zapis postępu przez jednego właściciela C#:
  checkpoint co 15 sekund, stan „w trakcie” po minucie i „odtworzony” po
  naturalnym końcu. Python pozostaje czytelnikiem `podcasts.db`; blokada drugiego
  hosta i wykrycie starego WPF chronią przed dwoma pisarzami. `Ctrl+I` ma już
  globalny, chwilowy widok „Nowe odcinki i materiały” z dokładnym powrotem po
  Escape, `Ctrl+Shift+I` widok „W trakcie słuchania” w sesji podcastów, a menu
  Widok zawiera „Pobrane” dla plików faktycznie istniejących na dysku.
  Pierwsze dwa filtrują wyłącznie źródła pozostające w Bibliotece; Pobrane
  zachowują też materiały z później zarchiwizowanych źródeł jak główne AMC.
  „Nowe odcinki” obsługują też zgodne z głównym AMC `Alt+1`, `Alt+2` i
  `Alt+3`: według dodania, alfabetycznie i według podcastu. Prywatnie zapisany
  wybór nie zmienia wspólnego profilu. Wszystkie widoki mają stronicowanie po
  150 pozycji i nie ujawniają technicznych identyfikatorów. `Ctrl+N` dodaje
  RSS/Atom, kanał albo playlistę YouTube i pojedynczy publiczny materiał
  YouTube wspólnym torem C#. `F2` zmienia nazwę źródła, `Delete` usuwa je z
  Biblioteki, `F5`/`Ctrl+F5` odświeża, `Ctrl+D` pobiera zaznaczone odcinki,
  a `Ctrl+S` zapisuje jeden odcinek pod jawnie wybraną nazwą bez zmiany jego
  stanu „Pobrane”. `Alt+D` otwiera natywne pole tylko do odczytu: najpierw
  pełny opis podcastu lub odcinka, a dopiero pod nim właściwości. Polecenie
  „Przejdź do podcastu tego odcinka” otwiera źródło nadrzędne i zachowuje
  fokus na tym odcinku; podcast usunięty z Biblioteki nie jest udawany.
  `Ctrl+O` w tej sesji importuje wybrane RSS z OPML (Spacja przełącza,
  `Ctrl+A` zaznacza wszystkie), a menu Pliki eksportuje zapisane podcasty RSS
  do OPML. To samo menu eksportuje kanały jako zgodny z Google Takeout CSV,
  a kanały wraz z playlistami jako OPML. Wszystkie te drogi korzystają ze
  wspólnych parserów i eksporterów Core;
  techniczne rekordy ani podpisane adresy nie trafiają do mowy. Nadal brakuje
  pozostałych widoków specjalnych.
- Pozostałe sesje i ich pełna obsługa, nagrywanie/harmonogramy, pozostałe ustawienia, presety oraz redakcja materiałów.

Nie policzono rzetelnie procentu zgodności całego programu. Dawne237pozycji/196skrótów to historyczny spisWPF z421, nie aktualny mianownik pokrycia i nie dowód dostępności każdej funkcji.

## Trwała kolejność na prywatnej kopii — odebrana

Zwykłe GUI samo uruchamia hosta z argumentami prywatnego profilu i pisarza. Kolejność, członkostwo i świadomie pusty stan przeżywają restart. Odmowa zapisu jest czytana przez NVDA również po ostatnim utworze w odtwarzaczu, bez powtarzania tej samej przyczyny. Rodzic wykonał pełny cykl GUI na kopii11203/5000; `queue-persistence-after-live/gui-integration-parent/`. Czas wznowienia i bieżące ID są utrwalane od984c660; politykę domknięto w4ad9696 i70c833d. Trzy rzeczywiste GUI: zapis12,63s, wznowienie od12,75s, wyłączenie pamięci daje start od początku. NVDA odczytał czas przez Ctrl+E; Right przewija z fokusem na przycisku, Tab nadal nawiguje. Najnowszy Python608/0/0. Kwity `queue-resume-after-persistence/parent-final/`. Domyślny wspólny profil nadal pozostaje tylko do odczytu.

## Własność danych — zasada już przyjęta

Python czyta wspólny profil i trzyma własne ustawienia interfejsu osobno. Zapis danych wspólnych ma należeć do jednego właścicielaC#, z ochroną przed dwiema instancjami/harmonogramami. To przyjęta decyzja techniczna, a nie pytanie do użytkownika o wybór wariantu. Operacje zapisu nie są jeszcze przez to automatycznie zaimplementowane.

Pomiary są na kopii. Nie migruj ani nie nadpisuj profilu użytkownika przy testach. Techniczne plikiSQLiteWAL/SHM nie są podstawą do użycia `immutable=1` na żywych danych.

## Scalone Radio i lewa strzałka — odebrane na żywym NVDA

Jedna gałąź `hermes/wx-integrated-20261007` (`52b91d69`) łączy dwie rzeczy
odebrane wcześniej osobno: widoki/pozycję Radia (`0dfa4990` na bazie `30ec1ac9`)
oraz lewą strzałkę `quick_info.py`. `src` scalenia jest bit-w-bit tym samym
drzewem (`e5df7223`), co commit dostarczony jako `f2533ef2`, więc silnik .NET nie
został przebudowany „na podobny” — to ten sam plik.

**Lewa strzałka ma TRZY różne drogi i każda jest zmierzona osobno.** Folder ma
własny komunikat i NIE sięga do silnika (`plan.message`, `has_request=false`).
Plik i stacja idą planem do hosta i wracają mową. Pomiar opisuje wiersz
ZAZNACZONY, nie grający — bramka `QuickInfoGuard` odrzuca spóźnioną odpowiedź po
`item_id`/sesji/widoku, bo spóźniony parametr brzmiałby jak opis wiersza, na
którym użytkownik stoi teraz.

**W polu filtra lewa strzałka nie jest informacją.** 149 ujęć z fokusem w
`TextCtrl` dało **0** wywołań quick-info. To jest kryterium braku regresji, nie
kosmetyka: gdyby strzałka przechwytywała kursor w filtrze, naprawa jednego
martwego klawisza zabiłaby drugi.

Trzy widoki Radia (`Ctrl+L` 55, `Ctrl+U` 56, `Ctrl+H` 81 wierszy) mają
`order_matches_amc=true`, `missing_item_count=0` i `sees_live_writes=true`.

**Wyłącznik odczytu pozycji nie obiecuje ciszy, której nie dowiezie.** Licznik
„1 z 55” mówi NVDA z natywnego `positionInfo`, więc ukrycie wykonuje nakładka w
dodatku 0.4+. Przy zainstalowanym 0.3.3 opcja zapisuje się, a NVDA mówi wprost,
że ukrywanie wymaga dodatku 0.4 — i to jest zachowanie odebrane, nie usterka.

Python 977/0/0, protokół 8/8 zestawów. Kwit z 11 kryteriami i pełnym Podglądem
mowy: `amc_pomoc/wx-integrated-20261007/live/receipt.json`. Słuchalności dźwięku
nie mierzono — ocenia ją użytkownik. Nie dołożono nieodebranych zmian WPF ani
opcji sesji.
