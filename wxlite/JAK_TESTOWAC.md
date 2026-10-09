# AMC Python — uruchamianie i sprawdzanie bieżącego przyrostu

To rozwijany równoległy interfejs pełnegoAMC. Nie jest jeszcze zamiennikiem wszystkich funkcji programu. Katalog i nazwa uruchamiacza `wxlite` są historyczne; nie oznaczają decyzji o ograniczeniu docelowego zakresu do dwóch sesji.

## Nagrywanie radia — potwierdzenie po Ctrl+R

`Ctrl+R` na stacji od razu mówi jedno krótkie zdanie:
„Rozpoczynam nagrywanie w tle: nazwa stacji”. Ponowne zdarzenie faktycznego
otwarcia pliku nie czyta już nazwy pliku i nie zagłusza nawigacji NVDA.
Ponowne `Ctrl+R` mówi „Zatrzymuję nagrywanie: nazwa stacji”. Stan wiersza
stacji oraz widok `Alt+R` nadal są odświeżane z rzeczywistego stanu hosta.
Gdy nadchodzi termin planu, interfejs mówi jedno zdanie „Rozpoczynam
zaplanowane nagrywanie: nazwa stacji”. Późniejsze otwarcie pliku tylko
odświeża stan listy i nie powtarza komunikatu.

Tor został sprawdzony końcowo na rzeczywistym LiteHost i jednej zapisanej
stacji, bez zmiany profilu: start MP3, aktywny stan, zatrzymanie, historia oraz
niepusty plik w katalogu tymczasowym. Powtarzalna próba znajduje się w
`tools/probe_radio_recording.py`. Testy modelu i komunikatów:

```powershell
python wxlite\run_tests.py radio_recording
# oczekiwane: 23 zdane, 0 błędów
```

Ten sam próbnik z parametrem `--schedule` sprawdza pełną drogę planu:
przyjęcie terminu, samoczynny start, aktywny stan, zapis MP3, historię oraz
wyłączenie wykonanego planu jednorazowego. Próba z rzeczywistym LiteHost i
zapisaną stacją została zaliczona 9 października 2026 r.

## Podcasty i YouTube — odczyt, odtwarzanie i trwały postęp

Skrót `Ctrl+3` przełącza na trzecią sesję „Podcasty i YouTube”. Lista czyta
pozycje należące do Biblioteki z tego samego `podcasts.db`, którego używa pełne
AMC, ale otwiera bazę wyłącznie przez SQLite `mode=ro`. Nazwy kontrolek i
wierszy zawierają tylko tekst przeznaczony dla użytkownika; identyfikatory,
rekordy JSON i nazwy typów nie trafiają do mowy NVDA.

1. `Enter` na podcaście, kanale lub playliście otwiera jego odcinki.
2. `Enter` na odcinku uruchamia istniejący silnik multimediów C# AMC. Jeżeli
   baza ma zapamiętaną pozycję, odtwarzanie zaczyna się od niej.
3. `Backspace` wraca na dokładnie to samo źródło, a `Ctrl+L` wraca do głównej
   Biblioteki podcastów.
4. Widok ładuje po 150 odcinków. Wiersz „Załaduj więcej odcinków” dokłada
   następną stronę i przenosi wybór na pierwszy nowo dołożony odcinek.
5. `Page Up` i `Page Down` w odtwarzaczu przechodzą po odtwarzalnych odcinkach
   bieżącej listy. `Ctrl+Shift+C` na odcinku kopiuje bezpośredni adres medium.
6. Pozycja jest zapisywana przez host C# co 15 sekund, przed zmianą materiału
   i przy zamknięciu. Po minucie odcinek dostaje stan „w trakcie”. Naturalny
   koniec ustawia „odtworzony” i zeruje punkt wznowienia, tak jak pełne AMC.
   Python nadal otwiera `podcasts.db` wyłącznie w `mode=ro`.
7. `Ctrl+I` z dowolnej sesji otwiera chwilowy widok „Nowe odcinki i
   materiały”. Są w nim tylko nowe, nieodtworzone materiały ze źródeł nadal
   należących do Biblioteki. `Escape` wraca do dokładnej poprzedniej sesji,
   listy, filtra i zaznaczenia bez komunikatu „Powrót”.
8. Zbiorcze widoki ładują po 150 pozycji. Każdy wiersz ma nazwę źródła, a
   materiały z kanałów i playlist YouTube mają jawne określenie „materiał
   YouTube”. Identyfikatory bazy i rekordy modelu nie trafiają do mowy NVDA.
9. `Ctrl+O` pozostaje kontekstowe jak w głównym AMC: w Plikach wybiera plik,
    w Radiu importuje M3U/PLS, a w Podcastach otwiera import OPML. Import OPML
    pokazuje natywną listę: wszystkie źródła są początkowo zaznaczone, Spacja
    przełącza bieżące, a `Ctrl+A` zaznacza wszystkie. `Ctrl+I` nie jest już
    błędnie zajęte przez import stacji.
10. Pozycja Widok → „Pobrane” jest dostępna w sesji Podcasty i YouTube.
    Pokazuje tylko odcinki, których zapisany plik nadal istnieje na dysku,
    także gdy źródło zostało później usunięte z Biblioteki. Najnowsze są na
    początku; Enter odtwarza lokalny plik zwykłym torem silnika C#.
11. W „Nowych odcinkach” `Alt+1` ustawia najnowsze według dodania,
    `Alt+2` porządek alfabetyczny, a `Alt+3` grupowanie według podcastu.
    Wybrany tryb jest zaznaczony w menu Widok, zachowuje wybrany odcinek
    według jego ID i przeżywa restart wxPython. Zapis trafia wyłącznie do
    prywatnego stanu `AMC-wx-Lite`; wspólny `state.json` pozostaje nietknięty.
12. `Ctrl+N` w sesji Podcasty i YouTube otwiera natywny formularz dodawania.
    Przyjmuje RSS/Atom, kanał lub playlistę YouTube oraz pojedynczy publiczny
    materiał YouTube. Adres sprawdza ten sam klient C# co w głównym AMC;
    tymczasowy podpisany adres audio z YouTube nigdy nie trafia do bazy.
13. `F2` na głównej liście zmienia własną nazwę podcastu lub kanału,
    `Delete` usuwa źródło z Biblioteki, `F5` odświeża bieżące źródło,
    `Ctrl+F5` wszystkie źródła, a `Ctrl+D` pobiera jeden lub wiele zaznaczonych
    odcinków. Zaznaczenie wielokrotne działa także przez `Ctrl+Spacja`.
14. Pliki → „Eksportuj bibliotekę podcastów do OPML” zapisuje wyłącznie
    podcasty RSS należące do Biblioteki. Parser i eksporter pochodzą ze
    wspólnego Core AMC; Python nie interpretuje XML-u i nie zapisuje bazy.
15. Pliki → „Eksportuj subskrypcje YouTube” zapisuje kanały jako CSV zgodny
    z Google Takeout i importem NewPipe oraz FreeTube albo kanały wraz
    z playlistami jako OPML do czytników RSS. Pole „Typ pliku” opisuje
    zastosowanie obu formatów i zawsze nadaje właściwe rozszerzenie, również
    po przełączeniu z CSV na OPML. CSV nie udaje obsługi playlist: komunikat
    jawnie podaje liczbę pominiętych.
16. `Ctrl+S` na jednym odcinku otwiera natywny dialog „Zapisz jako” z nazwą
    wygenerowaną przez wspólny `PodcastDownloadNaming`. Powstaje niezależna
    kopia; pole pobrania w bibliotece pozostaje niezmienione. W sesji plików
    ten sam skrót nadal eksportuje zaznaczony fragment audio.
17. `Ctrl+Shift+U` zmienia Ulubione dla wspólnego zaznaczenia podcastów i
    odcinków. `Ctrl+U` pokazuje oba rodzaje w jednym widoku również z
    odtwarzacza. `Delete` w tym widoku wyłącza stan Ulubionych, ale nie usuwa
    źródła z Biblioteki ani pobranego pliku. Enter na podcaście otwiera jego
    odcinki, a Enter na odcinku odtwarza go zwykłym torem silnika.
18. `Ctrl+H` w sesji Podcasty i YouTube pokazuje zapisaną Historię
    odtwarzania tej sesji, również z odtwarzacza. `Delete` usuwa zaznaczenie
    tylko z Historii; źródło, odcinek i pobrany plik pozostają bez zmian.

Próba zgodności została wykonana na kopii prawdziwej bazy bez wypisywania
tytułów ani adresów: 305 źródeł w Bibliotece, pierwsze 20 źródeł zwróciło 2487
wierszy z poprawną paginacją. Osobno rzeczywisty opublikowany LiteHost otworzył
syntetyczny plik przez nowe polecenie `media.play` i zgłosił
`playback.duration` oraz `playback.started`.

Zapis postępu jest wąską transakcją jednego odcinka. Dodawanie, zmiana nazwy,
usuwanie, odświeżanie, pobieranie i import OPML także przechodzą przez jednego właściciela
C# i nigdy nie otwierają bazy do zapisu z Pythona. Host odmawia drugiemu oknu
wxPython prawa pisarza i odmawia zapisu, gdy wykryje uruchomione główne AMC,
które mogłoby później nadpisać całą migawkę. Widoki „Nowe odcinki i materiały”
wraz z trzema trybami sortowania oraz „Pobrane” są już przeniesione. Widok
„W trakcie słuchania” został świadomie usunięty: rozpoczęty materiał można
odnaleźć w Historii, Ulubionych albo Kolejce. Import i eksport OPML oraz
eksport kanałów i playlist YouTube są przeniesione; nadal czekają pozostałe
widoki specjalne.

Test bez danych użytkownika:

```powershell
dotnet tests\AccessibleMediaController.LiteHost.ProtocolTests\bin\Release\net8.0\amc_lite_protocol_tests.dll --podcast-progress
python wxlite\run_tests.py podcast_progress podcast_source podcast_navigation shortcuts
```

Zestaw C# sprawdza na osobnej bazie: 30 sekund bez zmiany statusu, 75 sekund
ze stanem „w trakcie”, naturalny koniec oraz nienaruszenie obcej tabeli.
Żywy odbiór wznowienia na profilu użytkownika wymaga bezpiecznego restartu
pakietu; nie wolno zamykać działającego okna podczas nagrywania tylko po to,
żeby podmienić zablokowany plik hosta.

## Opcje sesji — jak to sprawdzić (ten przyrost)

Testy bezokienne (wszystko, co zmierzone):

```bash
cd /home/michal/projekty/amc-wx-session-options-20261007/wxlite
python3 run_tests.py          # 968 PASS / 0 FAIL / 0 SKIP
```

Zgodność z silnikiem — payloady portu czytane PRAWDZIWYM parserem C#.
Sam build i `dotnet run` nie dowodzą portu; potrzebny jest krok Pythona:

```bash
cd wxlite && python3 tools/dump_session_option_payloads.py /tmp/p.json
cd .. && AMC_SESSION_OPTIONS_PAYLOADS=/tmp/p.json \
  ~/dotnet/dotnet run --project \
  tests/AccessibleMediaController.LiteHost.ProtocolTests/AccessibleMediaController.LiteHost.ProtocolTests.csproj
# oczekiwane: "(payloady z portu Python przyjete parserem: 378)" + 8 zestawów OK
```

Bez zmiennej `AMC_SESSION_OPTIONS_PAYLOADS` ten zestaw **wypisuje, że
pominął** sprawdzenie — nie traktuj takiego przebiegu jako dowodu portu.

Kalibracja (wykonana): podmiana zdolności Radia na „umie przetwarzanie”
czerwieni 4 testy, a odwrócenie warunku `ID_OK` czerwieni test Anuluj.
Dołożenie payloadu z nieobsługiwaną ciszą czerwieni zestaw C#. Testy więc
rozróżniają, a nie tylko świecą zielono.

**Czego to NIE mierzy:** żywego NVDA ani prawdziwego wxWidgets. W tym
środowisku nie ma wxPython, więc dialog wykonuje się na atrapach wx
(`tests/test_session_options_dialog.py`). Mowa, kolejność Tab i fokus po
zamknięciu wymagają osobnego odbioru z czytnikiem na pulpicie.

## Biblioteka radia i lokalna Historia — odbiór kolejnego przyrostu

Kod `4fbc49b0` ma zakończony wąski odbiór na Hermesie: filtr członkostwa
Biblioteki radia, nazwa stacji bez powtarzania rodzaju oraz odtworzenie z
Ctrl+H pliku niebędącego członkiem Biblioteki. Raport z gestów i mowy:
`amc_pomoc/wx-library-compare-20261006/live/ACCEPTANCE.md`;
kwit rodzica: `live/PARENT-VERIFIED.json`. To nadal przyrost przed dostawą,
nie funkcje już obecne w zainstalowanej próbnej paczce `5e1f2d4e`.

Przy ponowieniu używaj prywatnej pełnej kopii i właściwego trybu profilu:
`AMC_WX_FIXTURE` wybiera prywatną listę stacji z lite-home, a nie czytnik
`radio.stations`. Dla próby READ_ONLY_MIRROR ustaw APPDATA i LOCALAPPDATA
na prywatną kopię i nie ustawiaj AMC_WX_FIXTURE. Potwierdź efektywne ścieżki
oraz rzeczywiste wiersze przed pomiarem. Puste Radio nie zalicza filtra.

## Poprawki plików — odbiór zakończony

Na kodzie `5e1f2d4ed07503f7b9399f89aaf7143f5e5a1b71` wykonano poniższe
próby z żywym NVDA. Lista pozostaje scenariuszem regresji, nie otwartą
prośbą o ponowne sprawdzanie całego zakresu:


1. Ctrl+O otwiera dialog pliku, Ctrl+Shift+O dialog folderu; Escape anuluje.
2. Escape: pole filtra, lista wyników z filtrem, lista bez filtra i pusta
   lista folderu, następnie odtwarzacz. Pierwszy Escape z wyników czyści filtr,
   dopiero następny wychodzi wyżej. Brak rutynowej zapowiedzi wczytywania.
3. Zaznaczyć dalszy plik w folderze, Ctrl+U, Ctrl+L: ten folder i plik.
   Powtórzyć z Ctrl+H. Sprawdzić oba powroty również po Alt+2.
4. Home i End: rzeczywista pozycja hosta 0 i Max(0, duration-10s).
   Osobno NVDA+End: odczyt paska i zero poleceń przewinięcia.

Końcowy wynik Pythona: **799/0/0**. Raport z faktycznych gestów, mowy,
stanów GUI i hosta: `amc_pomoc/wx-file-keys-parity-20261006/parent-final-live/ACCEPTANCE.md`.
Rodzic uzupełnił tylko brakujące pomiary: prawdziwe oba modale, Escape
z potwierdzonego PLAYER oraz NVDA+End na obu potwierdzonych widokach.
Zastępczy ShowModal zwracający ID_CANCEL nie zalicza próby dialogu.
W folderze nawet przy braku wyników może zostać wiersz nadrzędny „..”;
faktyczne zero wierszy zmierzono osobno w filtrowanym korzeniu.

Dane próbne muszą być prywatną kopią, nagrywanie i rezerwacja pulpitu
sprawdzone świeżo przy kolejnej próbie. Dostawa tej paczki została już
potwierdzona oddzielnym odczytem569plików oraz działającej jednej instancji
na głównym komputerze; raport `wx-file-keys-parity-20261006/delivery/PARENT-DELIVERY.md`.
Nie mylić jej z nadal przygotowywanymi zmianami Biblioteki i Opcji sesji.

## Co zostało wykonane

Bieżący odbiór mechanizmu wszystkich list: 562 testy, 0 błędów, 0 pominięć. Zwykłe listy Windows, przyrostowe zmiany w obrębie widoku, zachowanie wyboru po ID, puste zakładki i pojedynczy odczyt po zmianie dużego widoku sprawdzono w prywatnym stagingu z żywym NVDA. Końcowe kwity: `native-w02-valid-retest-after422/parent-final/`. To nie jest odbiór wszystkich funkcji pełnego AMC ani innego czytnika.

Odbiór KOŃCOWY żywej kolejki (po scaleniu): **588 testów Pythona, 0 błędów, 0 pominięć** i **6/6 zestawów C#**. Z żywym NVDA zmierzono na nowym hoście (DLL `6FFF6BD0…`): Ctrl+Q na starcie oddaje zapis, Enter uruchamia kolejkę w porządku **B→A→C (nie alfabet)**, a pozostawiona OTWARTA lista sama usuwa zużyty wiersz po naturalnym przejściu i nie kradnie wyboru grającej pozycji. Po ostatnim utworze lista ma 0 pozycji (potwierdzone też kontrolką Win32 `LVM_GETITEMCOUNT=0`), czytnik mówi „Kolejka odtwarzania, pusto", a ponowny Ctrl+Q NIE wraca do starego zapisu. Transport na przebudowanym hoście: 22/22. Kwity: `live-queue-after-native-lists/final-integrated/`. Trwały pisarz profilu i dalszy pełny port pozostają poza tym etapem.

## Cisza podczas wpisywania filtra — korekta po próbie użytkownika

Wpisywanie i usuwanie znaków nadal zawęża listę, ale aplikacja nie mówi
„Wyniki filtrowania” ani liczby po każdym znaku, również przy zerowym wyniku.
Zostaje zwykłe echo klawiszy NVDA. Jawne wejście na pustą listę nadal wyjaśnia
brak wyników; Escape w polu nadal czyści filtr i wraca na listę.

Dwa testy kodujące dawną automatyczną zapowiedź odwrócono: najpierw26PASS/2FAIL,
po zmianie74PASS/0FAIL/0SKIP w filtrze i jego bezpośrednich zależnościach.
Odbiór żywej mowy do wykonania na scalonym kandydacie. Historia fraz, nowe
zachowanie Tab/Down i zmiana pamiętania filtra pozostają propozycjami.

## Filtr listy Ctrl+K: jak to sprawdzić na żywo

Pełny Python po tym odbiorze: **649 testów, 0 błędów, 0 pominięć**
(`search-filter-after-resume/final-recovery/full-python-final.log`).

Staging i narzędzia (nie publikacja, nie instalacja):

1. Skopiuj `wxlite/amc_wx_lite` do prywatnego stagingu i SPRAWDŹ `sha256sum` oraz
   `gui.__file__` z kwitu, zanim cokolwiek orzekniesz — stary staging potrafi mieć
   inny kod niż repo.
2. Uruchom `launch-final.ps1 <nazwa-kwitu>`; kwit ląduje w `kwit-<nazwa>/`
   (`probe.jsonl`, `snapshot.json`, `boot-stderr.txt`).
3. Gesty: `gestures-final.ps1 <pid> <plan>`. Plany: `A` pisanie+Enter, `B` Escape,
   `C` brak wyników, `D` Backspace w polu, `E` Down, `F` Radio, `H` Backspace NA LIŚCIE,
   `I` filtr per widok, `J` dwa niezależne zapytania.
4. Mowę czytaj z Podglądu mowy NVDA (`harness-nvda/read-speech-viewer.ps1`), zachowując
   bufor PRZED gestami i porównując różnicę. `Announcer.say` z sondy NIE jest dowodem mowy.

Czego wymagać od aparatury (sprawdzone tu na własnych błędach):

- Bramka pierwszego planu musi zwracać TYLKO `bool` i logować osobno; `Write-Output`
  przed `return` zwraca tablicę i `if (-not ...)` przepuszcza gest mimo braku fokusu.
- `$pid` jest w PowerShellu tylko do odczytu — użyj innej nazwy (`$fgPid`).
- Owijka sondy MUSI przekazywać `*args/**kwargs`, inaczej zgubi nowy parametr metody
  i zdarzenie w ogóle się nie zaloguje.
- Nie owijaj tej samej metody dwa razy w jednej instalacji sondy — drugie owinięcie
  nadpisuje pierwsze i zdarzenia milkną.

Ograniczenie zmierzone, nie naprawione: przy szybkim wpisywaniu przez `SendKeys`
pierwszy znak serii bywa gubiony przez samą aparaturę (plan J: wysłane `bo`,
dotarło `o`). To artefakt wysyłki gestów, nie filtra — wynik czytaj z `filter.text`
w kwicie, a nie z zamierzonego napisu.

## Domknięcie żywej kolejki

Nadrzędny kwit `live-queue-after-native-lists/parent-speech-full-profile/`: pełna kopia 11200→11203 pliki, 5000 zakładek zachowanych, naturalne B→A→C z C zaznaczonym jeszcze podczas B. Natywne/model 3→2→1→0, stały fokus, ponowne Ctrl+Q nadal puste. Mowa z rzeczywistego Podglądu mowy; `Announcer.say` sam w sobie jej nie dowodzi. Kod wykonawczy 38d931e, host DLL 6fff6bd0…, bez kolejnych zmian produkcyjnych w tym odbiorze.

## Trwałość kolejki: próbny profil i rzeczywisty restart

Aktualny kwit `queue-persistence-after-live/gui-integration-parent/REPORT.md`: pełny Python603/0/0, GUI odmowa zapisu ostatniego utworu z prawdziwą mową NVDA, ponowne uruchomienie niepustej kolejki, udany zapis do zera i kolejny restart z0wierszy. Własny profil `profile-persist-parent-final` zawiera pełne11203 rekordy; nie zastępuj go małą próbką.

Host: `C:\Users\Michal\AppData\Local\Temp\amc-wx-queue-persist-parent\complete-host-8abc180\amc_lite_host.exe`, DLL13f38747…. Przed użyciem sprawdź komplet wymaganych bibliotek z deps.json i manifest41plików w kwicie. Sam SHA własnejDLL NIE wykrywa brakujących NAudio/SoundTouch; poprzedni katalog był niekompletny mimo zgodnego SHA. Starych kwitów/profili nie nadpisuj, użyj następnego backupuSQLite. Zapisy są dozwolone tylko na prywatnej kopii, a późniejszy odbiór czasu opisano poniżej.

## Czas wznowienia — odbiór z rzeczywistym NVDA

Kod70c833d nad984c660/4ad9696. Aktualny host: `C:\Users\Michal\AppData\Local\Temp\amc-wx-resume-parent-final\host-policy-completion\amc_lite_host.exe`, DLL9cedd23b…,41plików,22wymagane DLL z deps.json i0braków. Kwity: `queue-resume-after-persistence/parent-final/REPORT.md`, `accepted-gui-cycles.json`, pełny `python-final-full.log` (608/0/0).

Na NOWEJ pełnej kopii, nie na zachowanych kwitach: Ctrl+Q, wybór niepierwszego utworu, Enter, Right+10s. Sprawdź rzeczywisty `loadedId` i rosnącą pozycję; `transport.status` nie ma pola `playing`. Zamknij własne okno podczas grania. Nowy GUI/host nie może grać sam: ma odtworzyć porządek, wybrać bieżące ID, a świadomy Enter wznowić od zapisanego czasu. Dla czytnika wstrzymaj materiał, poczekaj na aktualizację stanu, wywołaj Ctrl+E i odczytaj NOWY tekst Podglądu mowy przed zamknięciem okna. Sam `Announcer.say` nie dowodzi mowy. Wykonana próba:12,63s zapisane →12,75s start, NVDA „Minelo 12 s”.

Następnie wyłącz pamięć dla pozycji dziedziczącej ustawienie, pozostawiając stary niezerowy checkpoint. Nowy proces: `resumeSeconds=0`, start od początku (zmierzono0,97s, NVDA „Minelo 1 s”), checkpoint po zamknięciu0. Jawny Remember pozycji jest osobnym priorytetem. Dziedziczenie źródeł folderów sprawdzaj zgodnie z oryginałem: najbliższe Inherit prowadzi do sesji, nie do dalszego źródła.

C#7 zielonych zestawów, jedna pominięta próba ścieżki produkcyjnej naWSL. Adapter WPF skompilowany naWindows bez błędów/ostrzeżeń. To nie jest odbiór pełnego portu ani zgoda na współdzielony pisarz z działającym starym WPF.

## Przygotowane stanowisko prywatne

Odebrany układ naHermesie: `C:\Users\Michal\AppData\Local\Temp\amc-wx-425` z podkatalogami `app`, `runtime`, `host` i prywatną kopią `profile`. Jest to staging, nie numer publicznego wydania.

Uruchomienie bez obserwatora, z tym samym prawdziwym punktem wejścia:

```powershell
$stage = 'C:\Users\Michal\AppData\Local\Temp\amc-wx-425'
$env:APPDATA = "$stage\profile"
$env:LOCALAPPDATA = "$stage\profile"
$env:AMC_LITE_HOST = "$stage\host\amc_lite_host.exe"
Remove-Item Env:AMC_WX_FIXTURE -ErrorAction SilentlyContinue
& "$stage\runtime\python.exe" -m amc_wx_lite
```

Prywatny runtime ma `python314._pth` wskazujący `../app`. Embeddable Python może ignorować `PYTHONPATH`: zawsze sprawdzaj faktyczne `gui.__file__` i zgodność kopiowanych modułów. Stary moduł kandydata w innym katalogu nie testuje nowego kodu.

Bez przekierowania `APPDATA`/`LOCALAPPDATA` aplikacja domyślnie czyta profil właściwego użytkownikaWindows. Testy wykonuj na kopii. Zmienna `AMC_WX_FIXTURE` wybiera inny tryb prywatnej piaskownicy, dlatego nie zastępuje automatycznie prób lustrzanego, pełnego profilu.

## Odbiór klawiatury i NVDA

1. Ctrl+1: Biblioteka. Foldery mają pochodzić z bazy, nawet gdy zapisane dyski nie istnieją na stanowisku. Enter i Backspace zachowują logiczny powrót.
2. Alt+2: wszystkie lokalne pliki alfabetycznie. Porównaj liczność ze źródłem, nie z wpisaną na stałe liczbą.
3. Ctrl+U: Ulubione. Zachowaj właściwą kolejność i wybór.
4. Ctrl+P: playlisty; Enter do zawartości; Backspace na tę samą playlistę.
5. Ctrl+C: nazwa lub nazwy zaznaczonych pozycji, każda w osobnym wierszu.
   Ctrl+Shift+C na istniejących plikach ustawia jednocześnie tekst pełnych
   ścieżek oraz Windows `CF_HDROP`, więc pliki można wkleić do folderu. Dla
   kilku zaznaczeń kolejność odpowiada kolejności listy. Dla pozycji
   internetowej skrót kopiuje adres jako tekst. Sprawdź rzeczywistą zawartość
   schowka, nie sam komunikat.
6. Ctrl+Shift+O: zwykły dialog folderu, nie Biblioteka (Ctrl+O to dialog PLIKU — do 06.10.2026 port miał te dwa gesty odwrotnie). W zmierzonym dialogu pierwszyEnter wybiera wpisany folder, drugi zatwierdza; kontroluj rzeczywisty fokus i zamknięcie okna zamiast wysyłać gesty w ciemno.
7. Na dostępnym pliku:Enter, Spacja, Spacja. Stan przycisku Odtwórz/Wstrzymaj ma zgadzać się z działaniem; cisza czytnika i sam tekst statusu nie dowodzą komunikatuNVDA.
8. Ctrl+2: Radio z `radio.stations` wspólnego profilu. W trybie tylko do odczytu dodawanie/zmiana/usuwanie/import nadal mają odmówić uczciwie. Prywatna lista piaskownicy to odrębny tryb, nie zapis doAMC.

9. Menu (Alt): pasek ma być w całości klawiaturowy — Alt otwiera, strzałki chodzą, litery wybierają, Escape zamyka. Każda pozycja musi wykonać TO SAMO co jej skrót (ten sam `_dispatch`). W Radiu pozycje lokalnych Ulubionych/playlist mają być wyszarzane, nie ukryte. Nie dodawaj pozycji dla funkcji, których nie ma.

10. Widoki aktywności. Ctrl+H historia, Ctrl+Q kolejka, Ctrl+Shift+B zakładki ZAZNACZONEGO pliku. Trzy rzeczy do sprawdzenia poza samą licznością:
    - Etykieta kolejki odpowiada stanowi hosta. Przed pierwszą inicjalizacją jest to „Kolejka (zapisana)”; potem „Kolejka odtwarzania”, także „pusto” po ostatnim utworze. Pozostaw Ctrl+Q otwarte przy B→A→C: wiersze mają ubywać bez ponownego skrótu, a po ponownym wejściu nie mogą wrócić z zapisu. Wybrany nadal istniejący wiersz i widok pozostają; przejście pliku nie zmienia bieżącej stacji w oglądanej sesji Radia.
    - Ctrl+Shift+B, nie Ctrl+B. Ctrl+B to teraz OSOBNY, szerszy widok zbiorczy (`GetForDisplay` / `all_bookmark_rows`); Ctrl+Shift+B zostaje przy węższym `GetForItem` dla jednego pliku. Dwa skróty, dwa widoki — sprawdzaj, że się nie podmieniają.
    - Backspace z zakładek ma wrócić na TEN plik. Sprawdzaj ID zaznaczenia, nie numer wiersza — po powrocie do Wszystkich plików numer jest inny (zmierzono 2309 z 2476).
11. Fizyczny skok zakładki mierz pozycją z ŻYWEGO hosta (`transport.status`), nie oczekiwanym payloadem. `play.file` dostaje `positionSeconds` w jednym wywołaniu — nie wysyłaj seeka obok play, bo pozycja ginie przy starcie nowego pliku. Materiał do próby zrób SYNTETYCZNY: wygenerowany plik plus wstrzyknięty rekord w OSOBNEJ kopii pełnej bazy (nie w małej próbce, nie na pliku użytkownika). Przy wstrzykiwaniu zarejestruj zastępczą kolację `AMC_PL`, inaczej `INSERT` padnie na indeksie. Pozycję daj z częścią ułamkową (próba:83,456s) — kontrakt to dzielenie `/ 10_000_000`, nie `//`.

Przed audio/GUI sprawdź rezerwację pulpitu, obce procesy i ŻYWY stan nagrywania. Nie wyprowadzaj braku nagrywania z pustych pólJSON. Nie restartujNVDA ani nie wysyłaj klawiszy w obce okno. Podgląd mowy musi być otwarty podczas gestów. Zachowaj surowy przyrost i osobno skutek działania, nie tylko informację„niepusta mowa”.

Próba mowy/menu dla tego przyrostu: `amc_pomoc/wx-full-profile-after421/menu-and-speech-after422/proba_koncowa.py` (kwit `proba-koncowa.json`). Mierzy tylko to, co zmienione: mowę po zmianie widoku, oba kopiowania z odczytem schowka i obejście menu. Po próbie zamyka własne okno, sprawdza procesy i przywraca schowek.

Mowa natywnej listy: odebrano pojedynczy odczyt po Foldery → Wszystkie pliki oraz Playlisty → Wszystkie pliki, bez poprzedniej nazwy; strzałki czytają właściwy wiersz. Puste zakładki zgłaszają „pusto” i dostępną listę zamiast „nieznane”, a Backspace wraca po ID. Sama migracja na LC_REPORT tego nie dowodziła — końcowe dowody są w `native-w02-valid-retest-after422/parent-final/`. Pełna podmiana zawartości jest ograniczona do zmiany widoku; nie odtwarza HWND.

Próba widoków aktywności: `amc_pomoc/wx-full-profile-after421/activity-gui-after422/proba_koncowa_activity.py` (etapy A–C) oraz `dopiecie_de.py` (etapy D–F na tym samym żywym oknie). Nazwa gestu Backspace w mostku NVDA to `backspace`; `back` zwraca HTTP 500 i pierwszy przebieg na tym padł.

12. Zbiorczy widok zakładek (Ctrl+B) i powrót z odtwarzacza. Aparatura:
    `amc_pomoc/wx-full-profile-after421/all-bookmarks-gui-after422/`
    (`odbior_zakladek.py` = Ctrl+B / odmowa / pusto, `dopiecie_bcd2.py` =
    start i pauza i skok, `dopiecie_d.py` = powrót menu). Pułapki, na które
    już wpadnięto — nie powtarzaj ich:
    - **Ścieżka hosta w stagingu to `host\amc_lite_host.exe`**, nie
      `AccessibleMediaController.LiteHost.exe`. Przy złej nazwie okno
      pokazuje widok odtwarzacza przy `engine_pid=null` i `HostUnavailable` —
      wygląda jak zaliczony skok, a nic nie grało. Zawsze sprawdź
      `runtime.json` i dziennik wywołań, nie sam widok.
    - **Klasa klienta to `LiteHostClient`**, nie `HostClient`. Podgląd
      wywołań po złej nazwie milczy; podglądu bez pokrycia nie wolno czytać
      jako „zero wywołań".
    - **Pasek menu otwiera gołe `alt`.** `alt+w` wysłane z fokusem na
      przycisku odtwarzacza trafia w przycisk (NVDA zgłasza rolę 9
      „Odtwórz"), nie w menu. Potwierdź rolę 11 po `alt`, dopiero potem
      wysyłaj literę.
    - **Nie dosięgniesz pliku 40 strzałkami w 2476 wierszach.** Materiał
      próbny ma `is_favorite=1` — wejdź przez Ulubione (Ctrl+U, ~10 wierszy).
      Start i tak jest zwykłym Enterem z listy, charakter dowodu bez zmian.
    - **Kontekst bierz z sesji, nie z zaznaczenia**, i sprawdź go RÓWNIEŻ w
      pauzie. `status_text="Wstrzymano"` nie znaczy „nic nie jest bieżące".
    - **Powrót PLAYER→LIST wymaga `session_view` z obserwatora.**
      `library_view` nazywa tylko widok Biblioteki i nie rozróżnia
      odtwarzacza od listy, więc „Lista→Lista" nie jest dowodem powrotu.
    - **Pusty widok rób na POTWIERDZONYM innym pliku.** Najpierw zmień
      wybrany plik, potwierdź jego rzeczywiste ID i 0 zakładek tą samą
      funkcją, której używa widok, dopiero potem Ctrl+Shift+B i wymagaj
      `rows==0`. Nazwanie czegoś „pusto" przy `rows=1` nie jest dowodem.

## Dane i silnik

- SQLite: `mode=ro`, dziennikWAL uwzględniany. Błąd odczytu nie przechodzi automatycznie na `immutable=1`.
- ID są napisami. Dostępność i członkostwo w Bibliotece pochodzą z danychAMC, nie z `Path.exists` na innym komputerze.
- Jeden wykonawca C# prowadzi zegar i nagrywanie; Python nie uruchamia drugiego
  harmonogramu. `Ctrl+Shift+H` otwiera natywne zarządzanie planami wxPython.
  Zmiany są zapisywane w prywatnym `AMC-wx-Lite\state.json`, a nie we wspólnym
  profilu WPF, i od razu synchronizowane z działającym hostem. `Insert` dodaje,
  `Ctrl+D` powiela jako wyłączoną kopię, `Enter` edytuje, `Spacja` przełącza,
  a `Delete` usuwa. Po wykonaniu host zwraca przesunięty lub wyłączony plan,
  który wxPython utrwala w swojej kopii.
- Foldery:AMC_PL. Kolekcje alfabetyczne: właściwy osobny tryb tytułu iOrdinalIgnoreCase ścieżki. Zgoda jednego korpusu nie dowodzi równoważności dwóch komparatorów.
- ProtokółJSON-lines, identyfikatorynapisy, limit64KiB po stronie żądania, wsady dzielone po bajtach. Nowe tryby kluczy wymagają nowegoLiteHost; klient odmawia niezgodnego trybu starego hosta.
- Host wykorzystuje istniejące silnikiAMC, nie własny dekoderPython. Sprawdzenie Roslynem lub kluczy w osobnej sondzie nie zastępuje kompilacji i uruchomienia rzeczywistego hostaWindows.

## Testy kodu

```bash
cd /home/michal/projekty/amc-wx-full-profile/wxlite
python3 run_tests.py
```

Wąskie przebiegi wybiera się fragmentem nazwy modułu, np. `library_view_navigation`, `library_views_through_source`, `announcement_reaches_screen_reader`. Testy protokołu z rzeczywistymC# wymagają zbudowanych `AccessibleMediaController.LiteHost.ProtocolTests`; pominięć nie przedstawiaj jako zaliczenia.

Przed publikacją potrzebny jest osobny, pełny odbiór niezmienianej paczki. Ten dokument nie potwierdza publicznego wydania ani instalacji na komputerze Michała.

### Kolejka podcastów i YouTube

- Przejdź do sesji „Podcasty i YouTube” i naciśnij `Ctrl+Q`. Widok ma pokazać
  odcinki w kolejności zapisanej przez główne AMC; pozycje „Odtwórz następny”
  są przed zwykłą częścią kolejki.
- Pusta kolejka ma powiedzieć „Kolejka, zero elementów” bez identyfikatorów
  bazy ani technicznych nazw rekordów.
- `Enter` odtwarza zaznaczony odcinek. `Page Up` i `Page Down` przechodzą po
  widocznej kolejności źródłowej.
- `Delete` usuwa zaznaczony odcinek tylko z kolejki podcastów. Nie usuwa
  subskrypcji, pobranego pliku ani historii. Po ponownym `Ctrl+Q` usunięta
  pozycja nie może wrócić.
- Stronicowanie zachowuje próg 150 pozycji i wiersz „Wczytaj więcej”.

## Znane ograniczenia

Wymienione wyżej przypadki mowy list są odebrane. Nie rozszerzaj tego na wszystkie możliwe scenariusze i czytniki. Całość nie ma jeszcze wszystkich funkcji zapisu, usług i ustawień oryginału. Kolejka żywego silnika DZIAŁA (Ctrl+Q oddaje `queue.status`, naturalne przejścia zmierzone), ale kolejność po `queue.set` NIE jest jeszcze zapisywana do profilu — trwały pisarz pozostaje poza zakresem. Tempo i wybór silników mają osobne wcześniejsze kwity — odbiór list ich nie powtarza.

Pełne dowody robocze: `amc_pomoc/wx-full-profile-after421/library-gui-after422/REPORT.md` i `parent-acceptance/`. Nie kopiuj prywatnych tytułów/profilu do repo ani paczki.

### Zwykła natywna lista: jak sprawdzać aktualizacje (po migracji z `LC_VIRTUAL`)

Lista jest zwykłą `wx.ListCtrl` (`LC_REPORT`) i trzyma teksty u siebie. Kontrolka
jest TRWAŁA — nie wolno jej niszczyć i odtwarzać przy zmianie danych (ta droga
była mierzona i odrzucona: zabierała mowę wybranego wiersza przy 2476 pozycjach).

Co sprawdzać:

- **Brak zbędnych aktualizacji.** Po samej strzałce, Ctrl+C, zmianie statusu czy
  ogłoszeniu widoku liczba operacji na liście ma być **0**. Mierz licznikiem
  operacji (`observer_native.py` owija `_apply_ops`/`sync_rows`), nie na oko.
- **Ten sam ID z nową treścią MA się odświeżyć.** Zmieniona nazwa, długość albo
  flaga Ulubione przy niezmienionym ID musi zaktualizować swoje pole. ID/kolejność
  i treść/stan porównywane są osobno — testuj oba warunki, nie jeden.
- **Jedna zmiana nie przepisuje listy.** Dodanie, usunięcie albo przeniesienie
  jednego wiersza daje jedną operację, nie czyszczenie całości. Pełna podmiana
  jest dopuszczalna tylko przy rzeczywistej zmianie całego zbioru.
- **Fokus i wybór to OSOBNE własności.** Sprawdzaj oba (`GetFirstSelected` nie
  wystarcza) i zachowanie świadomego ruchu użytkownika.
- **Programowa selekcja nie może zmienić modelu.** W trakcie podmiany wiersze
  wysyłają `EVT_LIST_ITEM_SELECTED`; bramka `updating` pilnuje, żeby przejściowy
  indeks nie wszedł jako nowy wybór. Prawdziwy ruch użytkownika musi nadal dojść.
- **Strzałki natywne.** Góra/dół działają same, bez naszej obsługi.
- **Pierwsze wypełnienie na prawdziwym wxMSW.** Model wybiera wiersz 0 przed
  wstawieniem go do kontrolki. Nie wolno wywołać `GetItemState(0)`, dopóki
  `GetItemCount()` wynosi 0; wx zgłasza wtedy asercję i przerywa synchronizację
  przed `InsertItem`. Sam działający Enter nie dowodzi, że kontrolka ma wiersze,
  ponieważ aktywacja korzysta z osobnego modelu.
- **Pusta lista i przejścia mały↔duży** (np. 1 → 2475 → 9) bez degradacji mowy.

Pomiar czasu planu na pełnej skali: `python3 tools/measure_list_sync.py 2476`.
Odbiór 10 widoków i kwity: `amc_pomoc/wx-full-profile-after421/native-lists-after422/`.

Pusta lista jest odebrana: nazwa „Pliki lokalne”, rola NVDA 14 (MSAA LIST ma
inny numer: 33), właściwy komunikat i wyjście Backspace. Dawna para A/B z
`native-lists-after422/diag_pustej.py` była nadpisana i nie dowodziła wersji
źródłowej usterki. Przy ponawianiu prób używaj `observer_w02v.py`, pełnego
przekazywania argumentów wrapperów, PID/runID producenta, zgodności modelu
z `GetItemCount` i czystego stdout; sam licznik modelu nie dowodzi wypełnienia GUI.

## Lewa strzałka i widoki Radia — jak mierzyć na żywo

Scena, kwit i gesty: `amc_pomoc/wx-integrated-20261007/live/`. Reuzyj tej
aparatury, nie buduj nowej.

```
powershell.exe -NoProfile -ExecutionPolicy Bypass -File launch-integ.ps1 integ-live
powershell.exe -NoProfile -ExecutionPolicy Bypass -File integ-gestures.ps1 <PID> 'end,left' <HWND>
powershell.exe -NoProfile -ExecutionPolicy Bypass -File read-speech-viewer.ps1
python3 mk_receipt.py <kwit> <speech.txt> receipt.json
```

- **`AMC_WX_FIXTURE` MUSI być puste.** Fixture daje `PRIVATE_SANDBOX` i Radio z
  zerem stacji, więc na nim filtry `isInLibrary`/`isFavorite` i cały
  `READ_ONLY_MIRROR` są nietestowalne. Zamiast fixture przekieruj `APPDATA`
  i `LOCALAPPDATA` na **własną pełną kopię** profilu (oba, nie jeden).
- **Mowa to Podgląd mowy NVDA, nie nasz log.** `read-speech-viewer.ps1` czyta
  okno przez UIA. Nasz własny `announcer.say` dowodzi tylko, że wywołaliśmy
  funkcję — nie że użytkownik to usłyszał.
- **Sonda musi łapać `say`, nie zwrotkę.** `quick_info_reply`/`quick_info_failure`
  zwracają `None`; treść idzie wyłącznie przez `say`. Owijaj `say`, bo inaczej
  zapiszesz `None` i uznasz to za „brak mowy”.
- **Podmieniaj nazwy TAKŻE w `gui.py`.** `gui.py` robi `from .quick_info import
  quick_info_plan`, więc podstawienie w samym module `quick_info` nie dociera do
  okna.
- **Folder, plik i stacja to trzy osobne drogi.** Folder kończy się na
  `plan.message` bez żądania do hosta; plik i stacja idą przez silnik. Zmierz
  wszystkie trzy — jedna przechodząca nie dowodzi pozostałych.
- **Pole filtra to kryterium braku regresji.** Wejdź w filtr (`Shift+Tab` z
  listy; `F6` tam NIE wchodzi), wpisz tekst, naciśnij lewą strzałkę i wymagaj
  **zera** wywołań quick-info w tym oknie czasu.
- **W menu wx `End` nie skacze na koniec.** Nawiguj strzałkami w dół; `End`
  zostawia kursor na pierwszej pozycji i cicho zmierzysz nie tę pozycję, co
  chciałeś.
- **Listy mierzymy bez nakładek.** Biblioteka, Radio, Podcasty, harmonogramy i
  presety mają korzystać z natywnych obiektów wx/Windows. Nie wolno dołączać
  `wx.Accessible`, znacznika HWND ani `chooseNVDAObjectOverlayClasses` do
  zmiany nazw wierszy lub informacji o ich pozycji.
- **Nie licz pierwszego snapshotu.** Pierwsze ujęcie pada przed załadowaniem
  wierszy; jego `model_count=0` opisuje sondę, nie profil. Bierz maksimum z
  przebiegu.
