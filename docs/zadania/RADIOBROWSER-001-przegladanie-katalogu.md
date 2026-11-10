# RADIOBROWSER-001 — przeglądanie katalogu w sesji Radio

## Stan 10.11.2026

W trakcie. Pierwszy przyrost źródłowy jest zapisany, testy modelu i
celowany test hosta zaliczone. Nie powstała paczka, instalacja ani wydanie;
rzeczywisty odbiór NVDA i odsłuch stacji czekają.

## Materiał do dokumentacji użytkownika

W sesji Radio skrót `Ctrl+F5` otwiera „Katalogi Radia”. Po wybraniu
„Radio Browser” można przechodzić klawiszem Enter przez kontynenty, kraje,
sposób uporządkowania i listę stacji. Wybór kolejności obejmuje A–Z,
najpopularniejsze oraz ostatnio zmienione stacje. Lista pobiera do 100 pozycji
na raz; „Wczytaj kolejne stacje” otwiera następną część. Backspace i Escape
wracają o poziom wyżej, a na poziomie kontynentów Escape zamyka okno.

Na stacji Enter rozpoczyna odtwarzanie bez automatycznego zapisu. Lewa lub
prawa strzałka podaje bitrate, `Ctrl+C` kopiuje adres strumienia,
`Ctrl+Shift+C` kopiuje link do strony stacji, jeśli katalog go udostępnia.
`Ctrl+Shift+L` zmienia przynależność do Biblioteki, a `Ctrl+Shift+U` do
Ulubionych. `F5` w głównej sesji Radia odświeża stacje i bieżący widok.
Komunikaty NVDA obejmują m.in. „Wybierz kontynent”, „Wybierz kolejność stacji”,
„Wczytano ... stacji”, „Bitrate ...” i potwierdzenia kopiowania lub zmiany
kolekcji. Nazwy krajów i stacji są tekstem użytkowym; kody dostawcy nie są
częścią list ani nazw dostępnych.

TuneIn jest drugą pozycją wyboru z oznaczeniem „niedostępny”. Jej wybór
wyjaśnia, że oficjalna integracja wymaga dostępu partnerskiego; nie uruchamia
Radio Browser ani pozorowanego logowania. Przeglądanie
Radia nie wymaga posiadania playlisty. W profilach Test i Public funkcja ma
ten sam zakres, ale jej paczkowanie i odbiór obu profili wymagają weryfikacji.

## Uzasadnienie i decyzje

- `Ctrl+F5` służy zarządzaniu bieżącą sesją. Dotychczasowe odświeżenie Radia
  przeniesiono na `F5`; menu, pomoc i paleta korzystają ze wspólnej prezentacji
  polecenia.
- Radio Browser udostępnia kody krajów, stacje i paginację, lecz nie podaje
  kontynentu, wiarygodnej daty pierwszego dodania ani jednoznacznego miasta.
  Kontynenty są warstwą nawigacji AMC; nieznane kody trafiają do „Pozostałych
  krajów”, żeby nie znikały z katalogu. Opcja „Ostatnio zmienione” uczciwie
  odpowiada parametrowi API.
- Wynik katalogu wykorzystuje istniejący kontrakt wyniku wyszukiwania Radia.
  Przeglądanie i Enter same nie zmieniają Biblioteki ani Ulubionych.
- Oficjalna dokumentacja TuneIn opisuje przydzielenie partnerID, testy i
  zatwierdzenie integracji przed wydaniem. Własne konto słuchacza nie jest
  obecnie wystarczającą podstawą do obiecania logowania w AMC.
- Aplikacja WiiM Home pokazuje katalogi radiowe, lecz nie oznacza to, że
  dostępny interfejs urządzenia oddaje tę samą listę z tą samą strukturą.
  Sonos także wymaga osobnego sprawdzenia dostępnych uprawnień i API.
  Widoki streamerów powinny opierać się na rzeczywiście udostępnianych
  pozycjach danego urządzenia, bez udawania listy Radio Browser jako jego listy.

## Zmienione pliki

Klient i host: `RadioBrowserClient.cs`, `LiteRadioDirectorySearchCoordinator.cs`,
`LiteEngineHandlers.cs`, `Program.cs`, `host_client.py`.
Nawigacja i polecenia: `radio_browser_catalog.py`, `radio_browser_dialog.py`,
`gui.py`, `shortcuts.py`, `menu_model.py`.
Testy: `test_radio_browser_catalog.py`, `test_host_client.py`,
`test_shortcuts.py`, `test_command_palette.py` oraz test C# w `Program.cs`.

## Testy i wynik

- `python -m py_compile` zmienionych plików Python — zaliczony.
- Pierwsze uruchomienie `python wxlite/run_tests.py radio_browser_catalog shortcuts
  command_palette host_client` — 183 zaliczone, 1 niezaliczony. Powodem były
  równolegle dodane do `RADIO-FAST-001` skróty `Prior` i `Next`. Tamta zmiana
  została później cofnięta w commicie `7e36bdca`; tymczasowe dopuszczenie
  skrótów w naszym teście usunięto.
- `dotnet build` LiteHost `--no-restore` — kompilacja zaliczona bez ostrzeżeń.
- Celowany test C# `--radio-browser-browse-only` — zaliczony na sztucznych
  odpowiedziach HTTP dla krajów, metadanych, stronicowania i braku zapisu.
- Import okna przez interpreter `D:\Projekty Codex\AMC Python\runtime\python.exe`
  z wxPython — zaliczony. Nie zastępuje próby klawiatury i NVDA.
- Izolowany test natywnych okien wxPython: wybór dostawcy wraz z niedostępnym
  TuneIn, poziomy list,
  powrót do wybranego kraju, bitrate i kopiowanie obu adresów — zaliczony.
- Celowane testy zgodności `Ctrl+F5` w menu, palecie i pomocy po polsku i
  angielsku — 5 zaliczonych.
- Test menu oraz polskiej i angielskiej prezentacji `Ctrl+F5` — 3 celowane
  testy zaliczone. Szerszy przebieg
  `radio_browser_catalog shortcuts command_palette host_client
  menu_exposes_existing_actions` nie zakończył się w rozsądnym czasie i został
  przerwany; nie jest zaliczony.
- Ponowna kompilacja LiteHost po ostatnich zmianach — zaliczona, bez ostrzeżeń.
- Ponowny test C# `--radio-browser-browse-only` — zaliczony.

## Znane ograniczenia i następny odbiór

Do sprawdzenia: rzeczywiste pobieranie katalogu, wszystkie poziomy okna,
powrót fokusu po zamknięciu, paginacja, komunikaty NVDA i odtwarzanie.
Teksty samego nowego okna są obecnie po polsku także przy angielskiej wersji
interfejsu; angielska nazwa polecenia w menu, palecie i pomocy jest już spójna.
Nie ma podziału krajów na miasta; API udostępnia pole regionu, które można
rozważyć w kolejnym przyroście. Brak wydania i instalacji. Nie usuwano
żadnych plików użytkownika ani artefaktów instalacyjnych.

## Wersja i commit

Gałąź `codex/amc-test-public-20261109`, początkowa baza `49d1a85a`.
Równoległe zadanie `RADIO-FAST-001` dodało commity w trakcie tego przyrostu,
a następnie cofnęło zmianę źródłową w `7e36bdca`. Nasz zakres pozostaje
oddzielony od tej decyzji.
Kod i testy zapisano w commicie `535128c3` bez numeru wersji. Ten dokument
i indeks zadań zostaną zapisane osobnym commitem. Paczka, instalacja i wydanie
nie powstały.
