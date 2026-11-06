# POD-20261106 — wybór źródeł skrzynki podcastów

Status: **W trakcie — kod, testy bez okien i test hosta gotowe; bez instalacji oraz odbioru NVDA**.

## Korekta po teście integracyjnym — 6 listopada 2026

Test serii odświeżania wykrył, że licznik skrzynki filtrował odcinki po odstępie automatycznego odświeżania jeszcze przed pierwszym ręcznym wyborem źródeł. To zastępuje wcześniejsze założenie, że stara biblioteka zachowa domyślne liczenie bez migracji. Licznik filtruje po wybranych źródłach dopiero wtedy, gdy zapisano indywidualny wybór; wcześniej zachowuje dotychczasową liczbę wszystkich nowych odcinków. Test synchronizacji konta potwierdza też, że nowy kanał YouTube zostaje poza skrzynką po ręcznym ustawieniu źródeł. Zestaw Core obejmuje teraz 8 scenariuszy i przechodzi.

## Zgłoszenie i korekta użytkownika

5 listopada 2026 użytkownik poprosił, aby `Ctrl+I` obejmowało tylko najważniejsze podcasty i kanały YouTube, a pozostałe źródła dało się sprawdzać ręcznie w ich widoku. 6 listopada zaakceptował wdrożenie i wybrał dokładną nazwę **Wybierz źródła skrzynki podcastów**. Ta nazwa zastępuje wcześniejszą roboczą propozycję „Wybierz źródła skrzynki”.

6 listopada użytkownik poprosił, aby wybór kanałów zbiorczej listy transmisji na żywo był obok wyboru źródeł skrzynki w menu Ustawienia. To zastępuje wcześniejszą lokalizację polecenia w menu Pliki; sam sposób sprawdzania transmisji pozostaje bez zmiany.

## Materiał do dokumentacji użytkownika

W sesji **Podcasty i YouTube** otwórz menu **Ustawienia → Wybierz źródła skrzynki podcastów…**. Lista pokazuje nazwy źródeł i ich rodzaje. Spacją zaznacz źródła, które mają być widoczne w skrzynce `Ctrl+I`. Pole filtra pomaga znaleźć podcast lub kanał; przyciski **Zaznacz widoczne** i **Odznacz widoczne** działają wyłącznie na wynikach filtra. Wybierz **Zapisz** albo **Anuluj**.

Obok jest polecenie **Wybierz kanały do sprawdzania transmisji na żywo…**. Wybiera kanały zbiorczej listy transmisji YouTube. `Ctrl+Shift+I` sprawdza wybrane kanały na żądanie; to osobny wybór od skrzynki podcastów. Polecenie pojawia się tylko wtedy, gdy funkcja konta YouTube jest dostępna w bieżącym profilu.

Zaznaczone podcasty i kanały YouTube pozostają w automatycznym odświeżaniu. Odznaczone źródła nie są sprawdzane automatycznie i ich nowe materiały nie pojawiają się w skrzynce. Nadal możesz wejść do źródła i nacisnąć `F5`, aby sprawdzić je ręcznie. Odznaczenie nie usuwa wcześniej zapisanych odcinków. Strony Facebooka można wybrać do skrzynki, ale sprawdza się je ręcznie.

Po pierwszym zapisaniu wyboru nowo dodane podcasty i kanały pozostają poza skrzynką do czasu ich zaznaczenia w tym oknie.

`Ctrl+I` otwiera zapisane nowe materiały i doładowuje kolejne porcje po 150; samo nie łączy się z siecią. `Ctrl+F5` w sesji Podcastów nadal odświeża wszystkie źródła na wyraźne żądanie. Włączenie źródła ponownie przywraca jego wcześniejszy odstęp automatycznego odświeżania; gdy źródło było od początku ustawione tylko ręcznie, otrzymuje obsługiwany odstęp z ustawień Podcastów albo 60 minut.

## Uzasadnienie i decyzje

- Polecenie jest w menu **Ustawienia** tylko w sesji Podcasty i YouTube; nie trafia do ustawień sesji dotyczących głównie odtwarzania. Ten sam model polecenia zasila menu, paletę i pełną pomoc. Nowy skrót nie jest potrzebny.
- Polecenie wyboru kanałów transmisji przeniesiono z Pliki do Ustawienia bez zmiany identyfikatora wykonania; nazwa w menu, palecie i pomocy mówi teraz wprost o sprawdzaniu transmisji na żywo.
- Stan wyboru wykorzystuje istniejący odstęp odświeżania każdego źródła: zero oznacza sprawdzanie ręczne i brak w skrzynce. Poprzedni odstęp jest zapamiętany osobno, więc ponowny wybór go przywraca.
- Host C# wykonuje jeden zbiorczy zapis źródeł w transakcji. Python nadal otwiera bazę podcastów tylko do odczytu. Zapis zmienia wyłącznie źródła obecne w otwartym oknie; źródła dodane w międzyczasie pozostają nienaruszone.
- Pierwszy zapis ustawia trwały znacznik indywidualnego wyboru. Nowe źródła RSS, SoundCloud i YouTube otrzymują odtąd tryb ręczny do czasu zaznaczenia; kanały zsynchronizowane z konta YouTube podlegają tej samej regule.
- Skrzynka filtruje po źródłach przed pobraniem porcji i w każdym z trzech porządków. Licznik hosta używa tej samej reguły. Znaczniki „nowy”, odsłuch, pobrania i odcinki pozostają bez zmian.

## Testy i wynik

- 96 celowanych testów Pythona bez okien: zaliczone 6 listopada 2026. Obejmują wszystkie trzy porządki skrzynki, obecność źródła ręcznego w Bibliotece, menu, paletę i pełną pomoc. Po dodaniu etykiet angielskich szerszy zestaw dał 107 zaliczonych, 1 pominięty i 3 wyłączone testy innych znanych braków katalogu.
- Kompilacja testowego hosta Release: zaliczona, 0 ostrzeżeń i 0 błędów.
- Zestaw protokołu `--podcast-progress`: zaliczony. Sprawdza wyłączenie z licznika i automatu, zachowanie ręcznego odświeżania i zapisanych odcinków, ponowny wybór oraz ochronę źródeł niewidocznych w oknie.
- Pełny zestaw lokalizacji uruchomiono: 107 zaliczonych, 1 pominięty, 3 niezaliczone. Trzy niezaliczone dotyczą braków katalogu przy innych funkcjach: szablonu języka, tekstów TIDAL i Mirror oraz dotychczasowych ustawień. Nowe statyczne etykiety tego okna sprawdzono osobno; nie pozostały wśród braków.
- Testy w działającym GUI i mowa NVDA: niewykonane. Użytkownik wcześniej zastrzegł, że testy otwierające okna i przejmujące fokus wymagają osobnej prośby.
- Po przeniesieniu wyboru kanałów transmisji: 63 testy modelu menu i palety oraz 7 testów transmisji z atrapą wx (2 dodatkowe podtesty) zaliczone 6 listopada 2026. Zwykłe uruchomienie `test_youtube_live.py` przez globalnego Pythona zatrzymało się podczas zbierania testów, bo nie ma modułu wx; ponowienie z atrapą wx przeszło bez okien. To nie jest próba NVDA.

## Znane ograniczenia

Źródła pojedynczych materiałów internetowych nie są pokazywane w selektorze; nie są okresowo sprawdzanymi podcastami ani kanałami. Zmiana odstępu w opcjach pojedynczego podcastu wpływa także na jego obecność w skrzynce. Ręczna próba fokusu, stanu pól wyboru i powrotu po Zapisz/Anuluj z NVDA jest potrzebna przed odbiorem instalacji. Baza podcastów przechodzi z wersji schematu 3 na 4 przez dodanie znacznika indywidualnego wyboru; starsze bazy zachowują domyślne dotychczasowe działanie do pierwszego zapisu wyboru.

## Wersja i commit

Praca w kanonicznym repozytorium `D:\Projekty Codex\AMC - integracja wxPython`, gałąź `codex/konfigurowalne-skroty-20261026`. Podstawowa funkcja: commit `e8d339ff`; korekta Ctrl+I i Ctrl+Shift+I: `b8a3dd6e`. Poprawka licznika oraz synchronizacji nowego kanału jest w przygotowaniu do wspólnego commitu. Pakiet, instalacja i publikacja tej wersji nie zostały wykonane. Repozytorium zawiera równoległe niezapisane zmiany innych zadań; wspólne wydanie wymaga ich połączenia i weryfikacji.

## Szkic instrukcji i listy zmian do korekty użytkownika

Instrukcja: W sesji Podcasty i YouTube wybierz z menu Ustawienia polecenie **Wybierz źródła skrzynki podcastów…**. Zaznacz najważniejsze źródła i zapisz. Pozostałe sprawdzisz ręcznie klawiszem `F5` po wejściu do źródła.

Lista zmian: Skrzynka `Ctrl+I` pokazuje nowe materiały tylko z wybranych źródeł. Odznaczenie źródła wyłącza jego automatyczne sprawdzanie bez kasowania zapisanych materiałów.
