# AMC Python — zakres względem pełnego AMC

## 10.10 — bezpieczny start, jedna instancja i aktualizator EXE

AMC Python uruchamia się zmaksymalizowany i jest przenoszony na pierwszy plan.
Jeżeli działa wcześniejsza instancja, nowa wysyła jej zwykłą prośbę o
zamknięcie, czeka na zwolnienie blokady i dopiero potem otwiera własne okno.
Nie używa wymuszonego zakończenia procesu. Gdy trwa nagrywanie albo nie można
wiarygodnie potwierdzić jego stanu, starsza instancja odmawia zamknięcia,
wraca na pierwszy plan, a druga nie uruchamia się równolegle.

Do paczki trafia teraz `AMC-Python-Updater.exe`. Aktualizator przyjmuje
wcześniej przygotowaną paczkę, sprawdza obecność i sumę SHA-256 każdego pliku,
odrzuca ścieżki wychodzące poza katalog aplikacji, wykonuje kopię zastępowanych
plików i cofa zmianę po błędzie. Czeka na bezpieczne zakończenie starego
procesu i nie zabija go po przekroczeniu limitu czasu. Automatyczne
sprawdzanie i pobieranie wydania z Internetu wymaga jeszcze podłączenia
docelowego kanału wydań AMC Python; obecny plik EXE jest gotowym, działającym
etapem weryfikacji i instalacji przygotowanej aktualizacji.

## 10.10 — playlisty Radia i ich ręczna kolejność

`Ctrl+P` w sesji Radia pokazuje playlisty zapisane dla Radia, Enter otwiera
stacje w kolejności playlisty, a Backspace wraca na wcześniej wybrany wiersz.
Wiersz podaje nazwę oraz uczciwą liczbę dostępnych stacji; identyfikatory bazy
i brakujących wpisów nie trafiają do tekstu NVDA. W otwartej radiowej
playliście `Alt+Up`/`Alt+Down` przesuwa ciągły blok, a `Ctrl+X` i `Ctrl+V`
przenosi również zaznaczenie nieprzylegające. Zapis pozostaje pojedynczą
operacją hosta C#, po czym fokus i całe zaznaczenie wracają według stabilnych
identyfikatorów niewidocznych dla użytkownika.

## 10.10 — podcasty, lewa strzałka i planowanie nagrania

Pakiet wxPython jest docelowym samodzielnym interfejsem. Użytkownik nie
uruchamia starego okna WPF; wewnątrz pakietu pozostaje sprawdzony LiteHost C#
dla dźwięku, nagrywania, harmonogramów i zapisu profilu.

Lewa strzałka w Podcastach przekazuje teraz do wspólnego formatera autora,
adres źródła, czas, rozmiar i typ medium, jeżeli te dane są znane. Nie dopisuje
ich do zwykłej nazwy dostępnej wiersza. `Ctrl+Shift+U` dociera do istniejącej
operacji Ulubionych, `Ctrl+Shift+L` zmienia Bibliotekę podcastu także wtedy,
gdy zaznaczony jest jego odcinek, a `Ctrl+Shift+Q` i `Shift+Enter`
zmieniają trwałą kolejkę odcinków. Kolejka podcastów nie jest mieszana z
kolejką plików lokalnych. `Shift+R` na stacji otwiera nowy plan nagrania z tą
stacją już wybraną; `Ctrl+Shift+H` nadal zarządza wszystkimi planami.

AMC ma już trzy rzeczywiste silniki tempa: Google Speedy dla mowy,
Signalsmith Stretch dla muzyki oraz SoundTouch jako wariant dotychczasowy.
Paperback używa WSOLA; to punkt do późniejszego porównania odsłuchowego, nie
powód do usuwania obecnego podziału bez pomiaru.

Sonos pozostaje do odbioru na obudzonych urządzeniach. Moduł zgłaszania błędów
Michała Dziwisza nazywa się **Sygnalista**:
`https://github.com/michaldziwisz/sygnalista`. Jego integracja jest osobnym,
późniejszym etapem po odbiorze bieżących sesji. Repozytorium zawiera klienta
Python oraz Cloudflare Workera tworzącego GitHub Issues; dane diagnostyczne i
logi wymagają osobnego potraktowania pod kątem prywatności.

## 10.10 — TIDAL, WiiM i Sonos; stan przed odbiorem urządzeń

Strategia pozostaje bez zmian: wxPython zastępuje interfejs, a LiteHost używa
sprawdzonego rdzenia C# dla odtwarzania, nagrywania, harmonogramów, timeshiftu
i integracji usług. Nie powstaje równoległy silnik Python.

W TIDAL `Ctrl+Shift+L` zmienia Bibliotekę albumów, wykonawców i playlist,
`Ctrl+Shift+U` zmienia Ulubione utworów i wideo, a `Ctrl+Shift+Q` zmienia
prywatną, trwałą Kolejkę wxPython otwieraną przez `Ctrl+Q`. Enter nadal
przekazuje odtwarzanie do oficjalnego TIDAL. Tokeny pozostają w C#.

WiiM oraz nowa sesja Sonos są podłączone przez istniejące usługi AMC. Sonos
korzysta ze wspólnego magazynu poświadczeń i nie ujawnia tokenu w protokole.
Obie sesje wymagają jeszcze odbioru na obudzonych urządzeniach; testy kodu nie
są takim odbiorem. Spotify jest odłożone. Aktualizacje na wzór „Sygnalisty”
zostaną podjęte po odzyskaniu i sprawdzeniu właściwego źródła lub ticketu.

Pełny runner Python: **1236 PASS / 0 FAIL / 138 historycznych SKIP**. Testy
protokołu C#: **14 zestawów OK**. Szczegółowy, bieżący stan i kolejność prac:
`AMC_PYTHON_STAN_2026-10-10_PL.md` w katalogu głównym repozytorium.

Interfejs odmawia teraz uruchomienia drugiego równoległego okna i podaje
jednoznaczny komunikat. Wcześniej drugie okno mogło pozostać bez hosta, gdy
pierwsze trzymało blokadę postępu podcastów; wtedy lista była widoczna, ale
Enter, Ctrl+R i lewa strzałka nie miały wykonawcy.

## 09.10 — Opcje odtwarzania podcastu i odcinka

`Dźwięk → Opcje odtwarzania elementu…` oraz `Alt+Shift+Enter` otwierają
natywny dialog dla zaznaczonego podcastu lub odcinka; skrót działa również
w odtwarzaczu dla bieżącego odcinka. Dostępne są: zasada wznawiania,
prędkość, normalizacja, łagodne przejścia, cisza i algorytm zmiany tempa.
Podcast ma ponadto częstotliwość odświeżania i własny folder pobierania.

Odcinek dziedziczy po podcaście. Ustawienia dźwięku i prędkość przechodzą
dalej do sesji i ustawień ogólnych; domyślną zasadą pozycji podcastu jest
jawnie opisane pamiętanie miejsca.
Wartości modelu pozostają oddzielone od etykiet czytanych przez NVDA;
`1,00 razy` jest jawnie opisane jako normalna prędkość. Zapis przechodzi
przez jedynego właściciela bazy w hoście C#, a tor odtwarzania rozstrzyga
dziedziczenie przed otwarciem materiału. Usunięcie indywidualnej prędkości
wraca do prędkości zadanej przez sesję, nie do poprzedniego nadpisania.

Prawdziwe ukryte okno wxPython na Windows potwierdziło pierwszy fokus,
celowe nazwy kontrolek, zmianę wyboru i ponowne otwarcie. Żywy odbiór NVDA
pozostaje do wykonania w pakiecie testowym.

## 07.10 — Opcje sesji: historyczny przyrost, zastąpiony 23.10

Kod bbe40bcd i końcowa korekta rodzica w tym commicie. Wspólne wejście
Dźwięk → Opcje sesji oraz Ctrl+Alt+Enter. Ówczesny wybór konfigurowanej sesji
nie przełączał odsłuchu, lecz został usunięty po odbiorze NVDA 2026-10-23.
Pliki: normalizacja, przejścia, cisza i zachowanie
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

## Opcje sesji — rozszerzenie 2026-10-22

Korekta użytkownika z 2026-10-23 zastępuje projekt zbiorczego edytora:
`Ctrl+Alt+Enter` zawsze dotyczy aktywnej sesji i nie pokazuje dodatkowego pola
wyboru sesji. Dziedziczona prędkość ma etykietę wyłącznie „Jak ustawienie
ogólne”, bez dopisywania obliczonej wartości ogólnej.

Poniższy opis zastępuje wcześniejszą decyzję o celowym pominięciu pozycji i
prędkości sesji. Historyczna sekcja pozostawała poprawna dla wcześniejszego
kandydata, ale nie opisuje już bieżących źródeł.

Wspólne wejście do opcji odtwarzania **aktywnej sesji**: pozycja
`Dźwięk → Opcje sesji…` i `Ctrl+Alt+Enter` (lista i odtwarzacz).
Dialog pokazuje WYŁĄCZNIE opcje, które dana sesja naprawdę wykona:

| Opcja | Pliki lokalne | Radio | Podcasty i YouTube | Dlaczego |
|---|---|---|---|---|
| Pozycja odtwarzania | tak | **nie** | tak | host zachowuje hierarchię element/kontener → sesja → ustawienie ogólne |
| Domyślna prędkość | tak | **nie** | tak | start materiału przyjmuje bazę sesji; podcast/odcinek może ją nadpisać |
| Normalizacja głośności | tak | **nie** | tak | `RadioMediaOutput` nie ma wykonawcy |
| Łagodne przejścia | tak | **nie** | tak | to samo |
| Cisza między utworami | tak | **nie** | tak | radio nie ma granicy utworów |
| Po wyjściu z odtwarzacza | tak | tak | tak | polityka okna, nie wyjścia audio |

Radio nadal dostaje jedną kontrolkę, a nie sześć z pięcioma martwymi.
Podcasty mają wykonawczy tor plikowy, dlatego dostają te same sześć pól co
Pliki lokalne. Indywidualne ustawienie podcastu lub odcinka ma pierwszeństwo
przed domyślną pozycją i prędkością sesji. Wartość `1,00 razy` jest jawnie
nazwana normalną, mniejsze wolniejszymi, a większe szybszymi.

TIDAL, Spotify, Sonos i Frontier Smart mogą ustawić zachowanie po wyjściu z
odtwarzacza. WiiM nie dostaje martwych opcji. Wyjście audio pozostaje osobnym,
działającym wyborem pod `Shift+A`; nie wraca jako nieaktywne pole dialogu.

Każde pole ma wariant „Jak ustawienie ogólne”, więc wybór jest odwracalny;
przy prędkości wariant ten nie dopowiada bieżącej wartości ogólnej.
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
- Powrót `Escape`/`F6` z odtwarzacza nie dodaje już własnego „Lista, nazwa”.
  Natywna lista odzyskuje fokus i sama czyta dokładnie zaznaczony wiersz;
  własny komunikat „Lista, zero elementów” pozostaje tylko wtedy, gdy podczas
  odtwarzania wszystkie wiersze naprawdę zniknęły.
- Dalsze widoki Biblioteki i wyszukiwanie. Filtr listy `Ctrl+K` jest odebrany (sekcja wyżej). Historia i oba zakresy zakładek są odczytem. `B` dodaje szybką zakładkę przez jedynego pisarza C#, a `Delete` usuwa całe wielokrotne zaznaczenie w obu widokach bez usuwania plików; wspólny wpis zakładki i rozdziału zachowuje bit rozdziału. Żywa kolejka jest odebrana na pełnej kopii przez rodzica (`parent-speech-full-profile/`): naturalne B→A→C, C wybrane PRZED przejściem i zachowane, samoaktualizacja otwartej listy, prawdziwa mowa NVDA oraz 0 po wyczerpaniu i ponownym Ctrl+Q. Trwała kolejność jest odebrana na prywatnej kopii (sekcja niżej); czas wznowienia jest już odebrany. Zapis pozostaje blokowany, gdy działa starsze WPF nieznające wspólnej blokady.
- Radio: oddzielenie Biblioteki według `isInLibrary` oraz krótki odczyt pozycji i jego wyłącznik są **odebrane** (sekcja „Scalone Radio i lewa strzałka” niżej). Historia nagrywania scala teraz wpisy prób z dostępnymi plikami oznaczonymi w `library.db`, także spoza zwykłego członkostwa Biblioteki; nie dubluje tej samej ścieżki i nie zmienia profilu.
- Zapis Ulubionych plików lokalnych i stacji radiowych jest przeniesiony przez
  jednego właściciela C#. `Ctrl+Shift+U` stosuje jeden wspólny stan do całego
  zaznaczenia, zachowuje oba zapisane porządki Ulubionych, a stację dodawaną do
  Ulubionych pozostawia też w Bibliotece. Ta sama pozycja menu prowadzi do
  właściwej implementacji w Plikach, Radiu, Podcastach i TIDAL-u; nie jest
  oferowana na rodzajach elementów, których dana usługa nie może polubić.
  `Ctrl+Shift+P` otwiera teraz natywne okno przynależności do playlist dla
  plików, stacji i odcinków podcastów. Stan każdego wiersza jest jawnie nazwany
  „zaznaczona”, „niezaznaczona” albo „stan mieszany”; Spacja zmienia stan
  całego wielokrotnego zaznaczenia. Insert lub Ctrl+N tworzy playlistę, F2
  zmienia nazwę, a Delete usuwa ją bez usuwania multimediów. Wszystkie zmiany
  pozostają robocze do przycisku Zapisz i trafiają atomowo przez jednego
  właściciela C#; Anuluj nie zapisuje częściowej operacji. `Ctrl+P` w Radiu
  i Podcastach pokazuje wyłącznie playlisty bieżącej sesji; Enter otwiera ich
  stacje albo odcinki w zapisanej kolejności, a Backspace wraca na tę samą
  playlistę. Nazwy i liczniki są użytkowe, bez identyfikatorów profilu, a
  brakujący element jest uczciwie uwzględniony w liczniku dostępności. W
  otwartej lokalnej, radiowej albo podcastowej playliście działa też ręczna
  kolejność głównego AMC: `Alt+Up`/`Alt+Down`
  przesuwa ciągły blok o jedno miejsce, a `Ctrl+X`, wybór celu i `Ctrl+V`
  przenosi także elementy nieprzylegające przed bieżący wiersz. Filtr musi być
  pusty, zapis jest jedną transakcją C#, niedostępne pozycje zachowują swoje
  miejsca, a po odświeżeniu fokus i całe zaznaczenie wracają według ID.
  `Delete` w zawartości playlisty podcastów usuwa odcinek wyłącznie z tej
  playlisty. Członkostwo, widok zawartości i ręczna kolejność Podcastów są
  obsługiwane przez ten sam magazyn playlist co główne AMC.
- Podcasty i YouTube mają już wąski zapis postępu przez jednego właściciela C#:
  checkpoint co 15 sekund, stan „w trakcie” po minucie i „odtworzony” po
  naturalnym końcu. Python pozostaje czytelnikiem `podcasts.db`; blokada drugiego
  hosta i wykrycie starego WPF chronią przed dwoma pisarzami. `Ctrl+I` ma już
  globalny, chwilowy widok „Nowe odcinki i materiały” z dokładnym powrotem po
  Escape, a menu Widok zawiera „Pobrane” dla plików faktycznie istniejących na
  dysku. Widok „W trakcie słuchania” i jego skrót `Ctrl+Shift+I` zostały
  świadomie usunięte; pozycja wznowienia nadal jest zapisywana, a materiały
  pozostają dostępne przez Historię, Ulubione i Kolejkę. Nowe odcinki filtrują
  wyłącznie źródła pozostające w Bibliotece; Pobrane
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
  `Ctrl+Shift+U` zmienia stan Ulubionych podcastów i odcinków przez tego samego
  pojedynczego pisarza C#. Dla wielokrotnego zaznaczenia zachowuje regułę
  głównego AMC: jeżeli wszystkie elementy są ulubione, usuwa stan wszystkim;
  w przeciwnym razie ustawia go wszystkim. W mowie pozostaje nazwa jednego
  elementu albo sama liczba elementów, nigdy identyfikator bazy. `Ctrl+U`
  otwiera teraz wspólny widok ulubionych podcastów, kanałów i odcinków także
  z odtwarzacza. Kolejność dodania i kolejność własna czyta z tabel głównego
  AMC, a alfabetyczną układa kluczami hosta C#. `Delete` w tym widoku usuwa
  całe wielokrotne zaznaczenie wyłącznie z Ulubionych; nie archiwizuje źródła
  i nie usuwa pobranego pliku. Widok zachowuje 150-elementowe stronicowanie,
  fokus oraz jawnie nazywa kolejność zastępczą, gdy zapisu porządku nie da się
  bezpiecznie odczytać. `Ctrl+H` otwiera Historię odtwarzania podcastów także
  z odtwarzacza. Odcinki są w dokładnej, zapisanej kolejności najnowszy
  pierwszy, a materiały ze źródeł później usuniętych z Biblioteki zachowują
  nazwę źródła. `Delete` usuwa w tym widoku tylko wpis historii; nie usuwa
  odcinka, pobranego pliku ani źródła.
  `Ctrl+O` w tej sesji importuje wybrane RSS z OPML (Spacja przełącza,
  `Ctrl+A` zaznacza wszystkie), a menu Pliki eksportuje zapisane podcasty RSS
  do OPML. To samo menu eksportuje kanały jako zgodny z Google Takeout CSV
  przeznaczony m.in. do NewPipe i FreeTube, a kanały wraz z playlistami jako
  OPML do czytników RSS. Wybrany w natywnym dialogu typ pliku rozstrzyga
  format i nadaje właściwe rozszerzenie. Wszystkie te drogi korzystają ze
  wspólnych parserów i eksporterów Core;
  techniczne rekordy ani podpisane adresy nie trafiają do mowy. `Ctrl+Q`
  otwiera zapisaną kolejkę odcinków w kolejności głównego AMC, z pozycjami
  „Odtwórz następny” przed częścią zwykłą. `Enter`, `Page Up`, `Page Down`,
  stronicowanie i `Delete` działają bez ujawniania technicznych identyfikatorów;
  usunięcie czyści flagi odcinka oraz wszystkie trzy tabele kolejności jedną
  transakcją C#. `Ctrl+Shift+Q` przyrostowo dodaje lub usuwa całe zaznaczenie
  ze zwykłej kolejki, a `Shift+Enter` przełącza blok „Odtwórz następny”.
  Host zachowuje stabilny porządek obu bloków, zapisuje go i zużywa kolejkę po
  naturalnym końcu odcinka bez przebudowy toru odtwarzania.
- Radio ma działające nagrywanie ręczne i w tle, historię, podział pliku,
  harmonogramy wykonywane przez host C# oraz osobne komunikaty stanu. `Alt+D`
  na liście i w odtwarzaczu czyta nazwę stacji, dostępne parametry audio oraz
  bieżącą audycję lub utwór tym samym formatterem co główne AMC.
- TIDAL ma jawnie ograniczony etap sesji wxPython: `Ctrl+4` otwiera
  tylko do odczytu zapisaną przez główne AMC migawkę kolekcji. `Ctrl+L`,
  `Ctrl+U` i `Ctrl+P` pokazują odpowiednio Bibliotekę, Ulubione i Playlisty,
  zachowując osobny widok oraz zaznaczenie tej sesji. Nazwa, wykonawca, rodzaj,
  czas i publiczny adres są oddzielone od identyfikatorów technicznych. Albumy,
  playlisty oraz trzy kategorie wykonawcy można otwierać przez istniejącą
  oficjalną integrację katalogu C# i ten sam Windows Credential Manager;
  token nie przechodzi przez protokół Pythona. Backspace odtwarza poprzednią
  listę i fokus, a przełączanie sesji zachowuje otwarty kontener. Enter na
  utworze przekazuje go do oryginalnej aplikacji TIDAL, Ctrl+Enter przekazuje
  album albo playlistę. Spacja oraz następny/poprzedni sterują wyłącznie
  sesją multimedialną oryginalnego TIDAL-a; AMC nie pokazuje fałszywego
  własnego odtwarzacza. Odczyt czasu jest dostępny tylko wtedy, gdy TIDAL
  rzeczywiście udostępni go systemowi. Zmiany konta nadal należą do TIDAL-a.
- WiiM ma pierwszy działający tor autonomicznej sesji pod `Ctrl+5`. Lista
  urządzeń pochodzi z profilu AMC, ale adresy sieciowe pozostają wyłącznie po
  stronie hosta C# i nie trafiają do nazw czytanych przez NVDA. Enter odczytuje
  prawdziwy stan urządzenia i otwiera odtwarzacz; Spacja, Page Up/Page Down,
  strzałki głośności, suwak oraz odczyt czasu prowadzą przez wąskie operacje
  `wiim.*`, nigdy przez lokalny silnik audio. Stan jest odświeżany okresowo.
  `Ctrl+L` otwiera **Bibliotekę źródeł WiiM** z dwiema jawnymi pozycjami:
  **Presety urządzenia** oraz **Strumienie zapisane w AMC**. Enter na drugiej
  pozycji otwiera strumienie dla wybranego albo aktywnego urządzenia i
  przekazuje do hosta wyłącznie stabilne ID;
  właściwy URL nie opuszcza C# i nie trafia do nazwy, komunikatu ani NVDA.
  Backspace wraca na dokładnie to urządzenie. Odtwarzacz pokazuje zapisaną
  nazwę także wtedy, gdy firmware podaje jako metadane tylko adres manifestu.
  `Ctrl+N`, `F2` i Delete zarządzają tą samą listą profilu przez serializowane
  operacje hosta. Zwykły widok otrzymuje wyłącznie nazwę; adres wraca do
  Pythona tylko po jawnym F2, do oznaczonego pola edycji. Delete czyści też
  zapamiętany wybór urządzenia i kolejności AMC, ale nie zmienia WiiM Home.
  F2/Delete są kontekstowe i nie przechwytują listy urządzeń.
  `Ctrl+O` otwiera playlistę lub plik ze strumieniami do importu; ukryty
  alias `Ctrl+Shift+O` pozostaje zgodny ze starszym układem. Import korzysta
  z istniejącego parsera playlist AMC i
  zachowuje kolejność partii. Eksport `Ctrl+E` używa istniejącego zapisu M3U
  WiiM i tego samego porządku co ekran. `Alt+1/2/3` zapisują w głównym profilu
  odpowiednio tryb najnowsze, alfabetyczny i własny; menu przekazuje stan jako
  natywne pole zaznaczone, nie jako dopowiedziany tekst.
  `Ctrl+Alt+P` pokazuje 12 natywnych miejsc urządzenia. `Ctrl+Alt+Shift+P`
  przypisuje zajęty preset do jednego z 12 skrótów AMC, a
  `Ctrl+Shift+1…0/-/=` uruchamia mapowanie osobne dla bieżącego urządzenia.
  Pierwsza prywatna zmiana wxPython nie modyfikuje wspólnego profilu; bez niej
  obowiązuje mapowanie głównego AMC albo domyślna zgodność numerów. Do
  dostępnej listy trafiają wyłącznie jawna nazwa, źródło i czytelny opis;
  adres urządzenia, URI i techniczny identyfikator pozostają poza kontrolką.
  Odtwarzacz ma też opcje z pełnego AMC: `I` — wejście, `O` — wyjście,
  `E` — korektor, `R` — powtarzanie, `S` — losowanie, `T` — timer uśpienia.
  Dialogi wx dostają wyłącznie jawne etykiety zwrócone przez wąski kontrakt
  hosta, a zastosowanie opcji wraca jako zwykła, bezpieczna migawka
  odtwarzacza. Te litery są kontekstowe tylko w odtwarzaczu WiiM, więc nie
  uruchamiają lokalnego zaznaczania fragmentu ani obcych funkcji sesji.
  `Alt+Page Up`/`Alt+Page Down` przechodzą kołowo po zapisanych strumieniach
  w bieżącym porządku albo po zajętych presetach urządzenia — zależnie od
  naprawdę aktywnego źródła. Presety Connect, których WiiM nie uruchomi bez
  aplikacji dostawcy, są pomijane. Pamięć bieżącego strumienia i presetu jest
  oddzielna dla każdego urządzenia i nie ujawnia identyfikatorów ani URI w
  interfejsie.
  Próba na dwóch urządzeniach zapisanych w bieżącym profilu potwierdziła listę
  i bezpieczny komunikat braku odpowiedzi; oba urządzenia były w czasie próby
  niedostępne, więc żywy transport wymaga jeszcze odbioru na włączonym WiiM.
- Sonos ma autonomiczny tor pod `Ctrl+6`, oparty na tym samym właścicielu
  konta, magazynie DPAPI i oficjalnym kliencie Control API co główne AMC.
  `Ctrl+F5` pokazuje grupy jako cele sterowania. `Ctrl+L` otwiera Bibliotekę
  materiałów, a `Ctrl+U` i `Ctrl+P` odpowiednio Ulubione Sonos i Playlisty
  Sonos. Trzecią kategorią Biblioteki są **Moje stacje** odczytane z tego samego
  profilu AMC. Enter tworzy sesję Sonosa i uruchamia zapisany adres w wybranej
  grupie; adres pozostaje wyłącznie po stronie hosta C# i nie trafia do Pythona,
  nazwy wiersza ani NVDA. W tej liście `Ctrl+N`, F2 i Delete dodają, edytują
  i usuwają wpis przez jedynego pisarza profilu C#. `Ctrl+O` korzysta z tego
  samego importera M3U/PLS co Radio i WiiM, a `Alt+1/2/3` zapisują odpowiednio
  kolejność według dodania, alfabetyczną i własną pod tym samym kluczem co
  pełne AMC. W kolejności własnej `Alt+Up`/`Alt+Down` przesuwają ciągły blok,
  a `Ctrl+X`, wybór celu i `Ctrl+V` przenoszą także nieprzylegające stacje
  zaznaczone przez `Ctrl+Spacja`. `Ctrl+E` eksportuje widoczną kolejność do
  przenośnego M3U przez ten sam zapis Core co Radio i WiiM. Adres wraca do
  Pythona tylko na czas jawnego dialogu F2; podczas eksportu pozostaje w C#.
  Presety materiałów działają w istniejącym układzie 12 miejsc AMC:
  `Ctrl+Alt+P` pokazuje listę, `Ctrl+Alt+Shift+P` przypisuje zaznaczony
  ulubiony, playlistę albo własną stację, a `Ctrl+Shift+1…0/-/=` uruchamia
  miejsce. Grupa Sonos jest celem odtwarzania, więc nie może zostać zapisana
  jako materiał. Ulubione i playlisty zachowują dom Sonos; ten sam niejawny
  identyfikator w dwóch domach jest traktowany jako dwa różne materiały.
  Istniejący preset ze stałym zestawem głośników odświeża topologię i działa
  tylko przy jednym dokładnym dopasowaniu logicznych identyfikatorów
  odtwarzaczy; nie przegrupowuje sprzętu i uczciwie odmawia przy braku albo
  niejednoznaczności. Identyfikatory domu, grupy i odtwarzaczy są polami
  modelu, nigdy etykietą kontrolki. Przy przypisywaniu dostępne jest jawne
  pole „Zawsze na tym zestawie głośników: nazwa grupy”; niezaznaczone oznacza
  bieżącą grupę, zaznaczone zapisuje dokładny logiczny skład. Jak pozostałe
  presety wxPython, przypisanie zapisuje prywatne nadpisanie portu zamiast
  ścigać się ze starszym AMC o `state.json`. Enter na ulubionym lub
  playliście wysyła jedno polecenie `INSERT` z odtwarzaniem do
  wcześniej wybranej grupy, bez ponawiania zapisu i bez przepuszczania tokenu,
  adresu ani identyfikatora do etykiety NVDA. Backspace wraca z podlisty na
  właściwą kategorię. Odczyt stanu, transport i uruchamianie materiału używają
  jednego toru żądań; chwilowe zajęcie hosta jest ponawiane bez komunikatu
  „trwa odczyt”. Menu sesji sieciowych filtruje polecenia innych usług i nie
  pokazuje lokalnego wyboru urządzenia audio Windows. Żywy odbiór na włączonej
  grupie pozostaje do wykonania przez użytkownika; testy automatyczne nie są
  dowodem, że fizyczny głośnik rozpoczął odtwarzanie.
- Do dalszego portu pozostają wejścia, wyjścia, korektor,
  powtarzanie i timer, Spotify, dalsze ustawienia oraz
  pozostała redakcja materiałów.

## Polityka zaniku wybranego urządzenia audio

Wcześniejsza stała decyzja o automatycznej kontynuacji zostaje zastąpiona
ustawieniem użytkownika. Jeżeli podczas odtwarzania wybrane wyjście znika,
host zachowuje ten wybór dla sesji i przełącza bieżący tor na urządzenie
domyślne. Następnie, zależnie od ustawienia, materiał albo gra dalej od
bieżącego miejsca, albo odsłuch przechodzi w pauzę. W żadnym wariancie wymiana
wyjścia nie może wznowić materiału wstrzymanego wcześniej przez użytkownika.
Radio zachowuje dekoder, bufor timeshift i aktywne nagrywanie także wtedy, gdy
odsłuch zostaje wstrzymany.

Powrót zapamiętanego urządzenia nie przenosi dźwięku samoczynnie w trakcie
bieżącego materiału; wybór zostanie użyty przy następnym uruchomieniu albo po
świadomej zmianie przez `Shift+A`. Nowe `Ctrl+[` i `Ctrl+]` przechodzą
cyklicznie odpowiednio do poprzedniego i następnego dostępnego wyjścia dla
aktywnej sesji podczas odtwarzania. Podają użytkową nazwę, zapamiętują wybór
dla tej sesji i nie zmieniają urządzeń innych równolegle grających sesji.

## Niezależne tory sesji — wykonane technicznie, odbiór otwarty

Główne AMC pozwalało jednocześnie odtwarzać radio i plik lokalny, w tym na
różnych urządzeniach. W porcie rozdzielono technicznie transport, wyjście,
pauzę, głośność i wyciszenie Radia od wspólnego toru Plików/Podcastów.
Przełączenie sesji ustala wyłącznie cel `Spacji`, `Ctrl+M` i pozostałych
poleceń. `Ctrl+M` ma też stanową, zaznaczaną pozycję menu dostępną tylko w
lokalnych sesjach audio. Pozostaje odbiór dwóch rzeczywistych strumieni i
dwóch wyjść na sprzęcie użytkownika; Pliki i Podcasty nadal współdzielą jeden
dekoder.

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

## Gotowy dodatek AMC do NVDA — most LiteHost/wxPython

13 października LiteHost dostał ten sam lokalny, ograniczony do bieżącego
użytkownika potok `AMC.NVDA.v1.<sesja Windows>`, którego używa pełny AMC.
Frontend rejestruje swój prawdziwy uchwyt okna i otrzymuje wyłącznie
zweryfikowaną nazwę z zamkniętej listy 65 poleceń. Odpowiedź jest korelowana w
hoście i trafia raz do dodatku; lokalny live region nie powtarza przechwyconego
komunikatu. Polecenia widoków mogą przywołać okno, a transport i odczyty nie
kradną fokusu.

Mapa obejmuje istniejące akcje transportu, czasów, szybkości, zakładek,
Ulubionych, Kolejki, radia, nagrywania, sesji, widoków i 12 presetów. Brakujące
jeszcze funkcje rozdziałów, globalnego wyciszenia, listy sesji, palety poleceń
oraz nawigacji po presetach odmawiają pod polską nazwą i nigdy nie są
podmieniane na „podobny” skrót. Stan TIDAL-a nie zgaduje odtwarzania z samego
zapamiętanego tytułu.

Testy bezokienne dowodzą zgodności całej allow-listy dodatku, mapy akcji,
odpowiedzi użytkowych, korelacji i anulowania żądań oraz wspólnego,
synchronizowanego zapisu protokołu. Pozostaje odbiór na żywym NVDA. Na czas
próby stary AMC musi być zamknięty: jedna sesja Windows ma jeden wspólny potok,
a dodatek nie zawiera selektora aplikacji.
