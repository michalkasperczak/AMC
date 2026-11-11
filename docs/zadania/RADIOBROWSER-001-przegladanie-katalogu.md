# RADIOBROWSER-001 — przeglądanie katalogu w sesji Radio

## Stan 10.11.2026

W trakcie. Kod jest zapisany, testy modelu i celowany test hosta zaliczone.
Powstała zweryfikowana paczka próbna AMC Test. Instalacja i wydanie nie
nastąpiły; rzeczywisty odbiór NVDA i odsłuch stacji czekają.

## Materiał do dokumentacji użytkownika

W sesji Radio skrót `Ctrl+F5` otwiera bezpośrednio „Radio Browser”. Można
przechodzić klawiszem Enter przez kontynenty, kraje,
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

TuneIn wycofano z wyboru katalogu 11.11.2026; nie ma pozorowanego logowania.
Przeglądanie Radia nie wymaga posiadania playlisty. W profilach Test i Public funkcja ma
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
- Izolowany test natywnych okien wxPython pierwotnego wariantu: wybór dostawcy
  wraz z niedostępnym TuneIn, poziomy list,
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
Kod i testy zapisano w commicie `535128c3`, poprawkę wyboru serwerów w
`6af086af`. Pierwszy zapis dziennika i indeksu: `d6112e58`.

## Kontynuacja 10.11.2026, po pierwszym punkcie kontrolnym

- Oficjalne wskazówki Radio Browser nakazują rozwiązać adres zbiorczy przez
  DNS, odczytać nazwy serwerów i używać ich przez HTTPS, przechodząc do
  kolejnej przy błędzie. Poprzedni kod łączył się przez HTTPS z adresem
  zbiorczym. Nowe przeglądanie stosuje DNS A/PTR, sprawdza sufiks domeny,
  pomija obce nazwy i próbuje kolejnych serwerów. Przy braku wyniku DNS
  korzysta z `de1.api.radio-browser.info`. Źródło:
  https://api.radio-browser.info/ oraz https://www.radio-browser.info/faq.
- Celowany test C# mapowania i przełączenia między serwerami — zaliczony.
  Kompilacja LiteHost po poprawce — 0 błędów, 0 ostrzeżeń. Testy zgodności
  menu, palety i skrótu — 6 zaliczonych po cofnięciu równoległej zmiany
  `RADIO-FAST-001`.
- Pierwszą paczkę z commita `d6112e58` zastępuje paczka z commita
  `6af086af046a20d70f5d575d8f6122684e49e9ff`, zapisana w
  `.test-output/radio-browser-mirrors-20261110`. Jej manifest: profil `test`,
  `sourceDirty=false`, 3458 plików, identyfikator kompilacji
  `2026.11.10-6af086af046a20d70f5d575d8f6122684e49e9ff-fd491cf4`.
  Pełna kontrola paczki i kontrolowany start GUI oraz hosta przeszły.
- Odczyt rzeczywistego katalogu na tym komputerze nie przeszedł: żądania do
  aktualnego serwera Radio Browser kończyły się limitem czasu. DNS zwrócił
  `91.98.4.78`, a PTR `de1.api.radio-browser.info`; ten sam serwer nie
  odpowiedział bezpośrednio w próbie HTTPS. Nie ma potwierdzenia, że katalog
  zadziała w sieci użytkownika; błąd jest jawnie komunikowany przez UI.
- Aktualizator nie zmienił bieżącej instalacji. Kontrola wykryła działające
  `C:\Users\micha\AppData\Local\Programs\AMC Test` z odtwarzaną stacją
  „Tyflo”. Stan nagrywania nie został pewnie ustalony, więc nie zamknięto
  programu. Zainstalowany manifest nadal wskazuje commit `4c898482`.
- NVDA działa, lecz rzeczywista mowa nowego okna i odsłuch nie zostały
  zweryfikowane. Paczka próbna nie jest instalatorem ani wydaniem.
- Szkic instrukcji użytkownika i zmian do korekty zapisano w
  `RADIOBROWSER-001-szkic-instrukcji-i-zmian.md`; nie opublikowano go.

## Odpowiedź użytkownika i doprecyzowanie, 10.11.2026, 23:42

Użytkownik potwierdził, że bieżące AMC Test nagrywa albo wkrótce rozpocznie
nagranie. Nie zamykać tej instancji, nie podmieniać instalacji i nie uruchamiać
drugiego widocznego AMC podczas tej pracy. Paczka próbna pozostaje oddzielna.

Użytkownik zwrócił uwagę, że HTTPS było już wcześniej. To prawda: wcześniejszy
klient używał `https://all.api.radio-browser.info`. Korekta `6af086af`
zmienia wybór nazwy serwera przez DNS i użycie konkretnego hosta HTTPS; nie
„dodaje HTTPS” jako nowej funkcji. Zastępuje wcześniejsze nieprecyzyjne opisy
tej zmiany. Rzeczywisty timeout do konkretnego serwera pozostał nieusunięty
na tym komputerze.

## Korekta użytkownika, 11.11.2026 — TuneIn

Użytkownik zdecydował zrezygnować na teraz z TuneIn i potwierdził wdrożenie
bez tej pozycji. Ta decyzja zastępuje wybór dwóch katalogów opisany
10.11.2026. `Ctrl+F5` otwiera bezpośrednio Radio Browser. Nazwa i opis
polecenia w menu, palecie i pomocy zostały dostosowane, a nieaktywny wybór
TuneIn usunięty. Powrót do TuneIn wymaga dostępu partnerskiego i osobnej
decyzji produktowej. Zmiana źródłowa jest jeszcze robocza; nie ma nowej
paczki, instalacji ani odbioru NVDA dla tego wariantu.

Celowane testy polskiej i angielskiej nazwy `Ctrl+F5` oraz menu: 3 zaliczone
po zmianie źródeł. Parsowanie składni zmienionych plików Python: zaliczone.
Izolowany test natywnej listy Radio Browser po usunięciu wyboru dostawcy:
zaliczony; ukryta rama wx zgłosiła ostrzeżenie `SetFocus`, więc nie jest to
odbiór rzeczywistego fokusu ani mowy NVDA. Ponowny przebieg tych samych testów
po zmianie samej nazwy testu trwał zbyt długo i został przerwany bez wyniku.

Równoległe zadania zmieniały `gui.py`, `shortcuts.py` i inne pliki. Na aktualnej
bazie wydzielono wyłącznie fragmenty Radio Browser i zapisano je w commicie
`9878fbb5` 11.11.2026. Po korekcie 22 testy palety poleceń i katalogu
Radio Browser przeszły. Ten wynik dotyczy źródeł, nie nowej paczki, instalacji
ani odbioru NVDA. Wcześniejsza nieudana próba wydzielenia patcha i stan limitu
sprzed resetu pozostają zapisem historycznym, nie stanem bieżącym.
