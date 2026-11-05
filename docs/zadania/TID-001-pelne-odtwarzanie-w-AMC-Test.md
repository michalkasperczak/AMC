# TID-001 — Pełne odtwarzanie TIDAL w odtwarzaczu AMC Test

## Status i decyzja użytkownika

W trakcie, od 4 listopada 2026. Użytkownik prosi o odwracalny eksperyment z alternatywnymi bibliotekami i nieoficjalnym torem odtwarzania, nawet jeśli nie jest autoryzowany przez TIDAL. Chce zachować bieżący dostęp do katalogu i Biblioteki oraz istniejące przekazanie do oryginalnej aplikacji jako punkt powrotu. Ta decyzja zastępuje dla **prywatnego laboratorium AMC Test** wcześniejszą decyzję STR-001, która wykluczała próby odtwarzania poza oficjalnym Playerem. Nie upoważnia do publikacji niezweryfikowanego rozwiązania w AMC Public ani do obchodzenia DRM.

## Cel i granice

Pełny utwór subskrybenta powinien grać w istniejącym odtwarzaczu AMC Test: pauza, wznowienie, przewijanie, następny/poprzedni utwór, kolejka i czytelny stan NVDA. Katalog, Biblioteka, playlisty i ich obecne logowanie pozostają osobnym torem. Funkcja eksperymentalna ma być wyłączona domyślnie i możliwa do wycofania bez utraty danych konta, list lub ustawień. Nie przechowywać tokenów ani podpisanych adresów audio w logach, protokole do GUI, historii, kolejkach i dokumentacji.

## Etapy i bramki

1. Zabezpieczyć punkt powrotu: obecna gałąź `codex/konfigurowalne-skroty-20261026`, HEAD `5c666b31`; zidentyfikować istniejące zmiany, instalację i procesy. Nie zastępować instalacji podczas prób źródłowych.
2. Dodać izolowaną diagnostykę jednego utworu: czy **obecny** token AMC ma prawo do pełnego strumienia. Odpowiedź ujawnia tylko status i bezpieczny kod wyniku. Testy bez konta przez fałszywy serwer HTTP.
3. Jeżeli token nie ma uprawnień, rozważyć osobne logowanie odtwarzania w odizolowanym magazynie poświadczeń; nie rozszerzać obecnych zakresów OAuth, ponieważ próba `r_usr` z 11.09.2026 zrywała całe logowanie. Żadne drugie logowanie nie może popsuć katalogu.
4. Po pozytywnym wyniku pełnego, niezaszyfrowanego strumienia podłączyć osobny resolver do istniejącego `WindowsMediaOutput`; zachować tylko ID utworu w kolejce i pobierać krótko ważny adres na moment odtwarzania. Dla DRM lub odmowy zgłaszać błąd i zachować tor oryginalnej aplikacji, bez prób odszyfrowania.
5. Dodać przełącznik w AMC Test, wspólny model poleceń, menu, Ctrl+F1, pełną pomoc, Ctrl+Shift+K, instrukcję i testy parytetu. Następnie test klawiatury i NVDA: fokus, pauza, przewijanie, kolejka, koniec utworu, ponowne otwarcie. AMC Public pozostaje bez eksperymentu do osobnego odbioru.
6. Dopiero po realnym teście pełnego utworu, braku wycieków, regresji i odbiorze NVDA zdecydować o budowie, instalacji i ewentualnym wydaniu. W razie porażki wyłączyć adapter i wrócić do oryginalnego TIDALa.

## Materiał do dokumentacji użytkownika

Na obecnym etapie sposób odtwarzania TIDAL w AMC Test się nie zmienił. Analizujemy możliwość pełnego odtwarzania w odtwarzaczu AMC; nie ma jeszcze nowego skrótu, menu ani komunikatu NVDA. Dotychczasowe przekazanie do oryginalnego TIDALa pozostaje dostępne.

## Uzasadnienie i decyzje

- `tidalapi` jest biblioteką Python, ale jej wewnętrzne API prawdopodobnie wymaga innego logowania niż katalog AMC. Nie dodawać zależności, dopóki nie ma potwierdzonego pełnego strumienia.
- Tid3 i Riptide dowodzą koncepcji natywnego odtwarzania przez starsze prywatne API. Ich licencje AGPL/GPL nie pozwalają traktować kodu jako dowolnego fragmentu do skopiowania do AMC. Projektować własny, mały adapter.
- Oficjalny Player SDK ma potrzebne operacje, ale test AMC we wrześniu dał próbkę i `FULL_REQUIRES_SUBSCRIPTION`; nie utożsamiać tego z błędem dostępu raportowanym przez innych. Embed ma ograniczony publiczny mostek poleceń. TIDAL Hi-Fi na Windows raportuje problem DRM.
- `WindowsMediaOutput` ma już obsługę zdalnego HTTP i seek, ale loguje źródło; ochronę diagnostyki wykonać przed rzeczywistym audio TIDAL.

## Testy i wynik

Na początku etapu: przegląd źródeł AMC oraz projektów GitHub. Brak próby z kontem, odtwarzania, buildu, instalacji, publikacji i testu NVDA. Wyniki kodu i testów dopisać po ich zakończeniu.

## Znane ograniczenia

Działanie obecnego tokenu w starszym API jest niepotwierdzone. Możliwe są drugie logowanie, zmiany protokołu, wygasanie adresów i odmowa/DRM. Nie twierdzić, że test z fałszywym serwerem dowodzi pełnego odtwarzania TIDAL.

## Wersja i commit

Brak nowej wersji AMC, instalacji i wydania. Commit eksperymentu: do uzupełnienia.

## Wynik etapu 1 — 4 listopada 2026

### Korekta użytkownika z 4 listopada 2026

W dawnym eksperymencie TIDAL program sygnalizował odtwarzanie, lecz nie było słychać dźwięku; później wprowadzono przekazanie do oryginalnej aplikacji. Ta informacja **zastępuje** każde wcześniejsze kryterium, które uznawałoby status `PLAYING`, manifest lub ruch licznika za dowód sukcesu. Warunek odbioru przyszłego backendu: użytkownik rzeczywiście słyszy pełny utwór przez wybrane wyjście audio w odtwarzaczu AMC, a następnie potwierdza pauzę, wznowienie, seek, następny/poprzedni i komunikaty NVDA. Bez tego eksperyment pozostaje niedziałający.

### Materiał do dokumentacji użytkownika

Na obecnym etapie nie zmieniono sposobu odtwarzania ani skrótów AMC. Dodano tylko niewidoczną w GUI operację diagnostyczną. Konto do katalogu pozostaje dotychczasowe, a testowy login audio ma osobny, jednorazowy kod. Oryginalny TIDAL pozostaje działającym sposobem odtwarzania.

### Uzasadnienie i decyzje

- Próbnik `tidal.playbackProbe` użył istniejącego logowania AMC i dla jednego utworu otrzymał HTTP 403. To nie ustala dokładnej przyczyny odmowy i nie dowodzi, że konto użytkownika nie ma subskrypcji. Nie zmieniono obecnego OAuth ani jego zakresów.
- Izolowany test .NET 8/HTTP z syntetycznym FLAC: `WindowsMediaOutput` i jego MediaFoundationReader nie odczytały sieciowego FLAC (COM E_ABORT), chociaż lokalny FLAC i kontrolne HTTP MP3 działały. Istniejący `FfmpegLocalAudioWaveStream.TryOpenNetwork` odczytał ten sam sieciowy FLAC i przewinął do około 6,043 s. Przy późniejszym pilocie rzeczywistego audio FLAC trzeba wybrać ten tor z potwierdzonym czasem trwania; samo istniejące `media.play` nie wystarcza.
- `tidalapi==0.8.11` zainstalowano wyłącznie w `work/TID-001-venv` (Python 3.14.2), bez zmiany globalnego Pythona, instalacji AMC i pliku zależności aplikacji. Narzędzie jednorazowego logowania nie zapisuje sesji. Próba na koncie oczekuje na działanie użytkownika lub wynik kodu.

### Testy i wynik

- `dotnet run --no-restore --project tests/AccessibleMediaController.LiteHost.ProtocolTests -c Release -- --tidal-playback-probe`: 1/1 celowany zestaw zaliczony.
- `dotnet build src/AccessibleMediaController.LiteHost -c Release --no-restore`: powodzenie, 0 błędów i ostrzeżeń (przed końcowym przeglądem).
- `dotnet run --no-restore --project tests/AccessibleMediaController.Windows.SmokeTests -c Release -- --diagnostic-redaction`: powodzenie. Test obejmuje URL w ścieżce/query/fragmencie oraz treść wyjątku, z zachowaniem lokalnej ścieżki.
- `python -m unittest wxlite.tests.test_tidalapi_device_probe -v`: 8/8 testów bez sieci zaliczonych na Pythonie 3.14.2.
- Test FLAC opisany wyżej używał syntetycznego pliku i wyłącznie 127.0.0.1; nie jest dowodem odtwarzania TIDAL ani odsłuchu z wyjścia audio.
- `git diff --check` pierwszego etapu: bez błędów. Kod próbnika i redakcji zapisany lokalnie w commicie `a2fd6ca7`. Testowego narzędzia Python jeszcze nie commitowano.

### Znane ograniczenia

Nie ma jeszcze pełnego strumienia TIDAL w AMC, rzeczywistego odsłuchu, testu NVDA ani nowej instalacji. HTTP 403 na obecnym tokenie wymaga osobnej diagnostyki logowania audio. `tidalapi` korzysta z nieoficjalnego interfejsu, którego działanie i uprawnienia mogą się zmienić.

### Dwie pierwsze próby logowania 4 listopada 2026

Dwa jednorazowe kody urządzenia wygasły przed zatwierdzeniem. Użytkownik wyjaśnił, że najpierw musi przejść logowanie TIDAL przez Google. W tych dwóch próbach nie otrzymano tokenu z `tidalapi`, nie sprawdzono nim manifestu i nie wykonano odtwarzania. Kolejny kod należało wygenerować dopiero, gdy użytkownik będzie przy polu kodu. Żadnego kodu jednorazowego ani danych konta nie zapisano w tym pliku. Ta informacja doprecyzowuje wcześniejszy zapis o próbie oczekującej.

### Wynik osobnego logowania 4 listopada 2026

Po przejściu logowania TIDAL przez Google i zatwierdzeniu kodu urządzenia `tidalapi==0.8.11` otrzymało odpowiedź HTTP 200 z `assetPresentation=FULL` i poprawnie zakodowanym manifestem jednego utworu. Narzędzie zwróciło wyłącznie `full_manifest`, kod HTTP i wartość logiczną; token, treść manifestu oraz adres audio nie trafiły do odpowiedzi ani dokumentacji. Poprzedni HTTP 403 dotyczył **istniejącego tokenu katalogu AMC**, a nie konta abonenta jako takiego. Ten wynik potwierdza osobną ścieżkę uprawnienia do pełnego manifestu, lecz nie potwierdza jeszcze pobrania danych audio, ich dekodowania, słyszalności w AMC ani przewijania. Sesja testowa była tylko w pamięci i zniknęła po zakończeniu procesu; kolejna próba audio będzie wymagała nowego kodu, dopóki nie powstanie bezpieczny magazyn tej osobnej sesji.

## Wynik etapu 2 — 4 listopada 2026

Dodano wyłącznie w profilu `test` operację `tidal.nativePlay` dla osobnego procesu LiteHost. Przyjmuje ona ID utworu, nazwę, czas oraz adres lokalnego proxy `127.0.0.1`; odrzuca zdalny URL, dodatkowe pola i nieprawidłowy token ścieżki. Sesja `tidal` korzysta z dotychczasowego toru audio, ale nie zapisuje podpisanego adresu TIDAL w modelu utworu, historii ani odpowiedzi protokołu. Dla pauzy, przewijania, zatrzymania i zmiany wyjścia istnieje rozróżnienie sesji. Publiczny profil nie rejestruje nowej operacji. Dotychczasowy `tidal.desktopPlay` pozostaje dostępny.

Izolowany resolver Python dopuszcza tylko `assetPresentation=FULL`, BTS, `encryptionType=NONE`, jeden adres HTTPS z domeny audio TIDAL oraz rozpoznane FLAC/AAC/MP3. Lokalne proxy Range działa tylko w pamięci, na `127.0.0.1`, pod losową ścieżką, bez przekierowań. Test syntetycznego FLAC potwierdził odczyt próbek PCM i seek przez FFmpeg. Testy protokołu `--tidal-native --transport-routing`: 2/2; build Release LiteHost: 0 błędów i ostrzeżeń; testy resolvera/proxy: 9/9. Nie zainstalowano nowej aplikacji.

Pierwszy pilot odtwarzania wygasł podczas logowania. W drugiej próbie logowanie się udało, ale narzędzie zakończyło się na zbyt wąskim warunku „tylko FLAC” (`lossless_flac_unavailable`), zanim uruchomiono hosta. Nie dowodzi to braku uprawnienia; format lub brak czasu nie były jeszcze osobno raportowane. Poprawiony pilot przyjmuje rozpoznane FLAC/AAC/MP3, sprawdza postęp odtwarzania przez 45 sekund i pokazuje dokładny bezpośredni adres autoryzacji urządzenia. Testy pilota bez konta: 12/12. Następny bezpośredni kod wygasł, ponieważ użytkownik nie mógł stale czekać na krótkie okno autoryzacji. Nie odtworzono jeszcze dźwięku TIDAL ani nie przeprowadzono odsłuchu i NVDA.

### Korekta użytkownika z 4 listopada 2026 — logowanie

Użytkownik wyjaśnił, że nie może wpisywać nowego kodu przy każdej próbie ani czekać stale na następny kod. Ta korekta zastępuje wcześniejszy plan kolejnych sesji wyłącznie w pamięci. Przed następnym kodem należy dodać osobny, chroniony przez Windows magazyn sesji odtwarzania i sprawdzić jej ponowne użycie po restarcie procesu. Nie zapisywać tokenu w pliku tekstowym ani nie łączyć go z obecnym logowaniem katalogu AMC. Połączenie urządzenia wymaga zatwierdzenia, a sam widok albumów w przeglądarce nie jest potwierdzeniem połączenia pilota.

### Stan i commity

Kod hosta i testy: `11f4d5eb`. Resolver, lokalne proxy i testy: `8deda9d5`. Instalacja użytkownika działała oddzielnie; żaden proces AMC nie został zatrzymany. Pełny utwór i słyszalny dźwięk w AMC pozostają niepotwierdzone. Brak nowej wersji, instalacji i wydania.

## Następny etap GUI — projekt z 4 listopada 2026

### Stan obecny i powód odroczenia

W wxPython `Enter` na utworze TIDAL tworzy `PlayTidalTrack`, a `gui.py::_play_tidal_track` wywołuje `LiteHostClient.tidal_desktop_play` (`tidal.desktopPlay`). `Ctrl+Enter` na albumie lub playliście przekazuje cały kontener oryginalnej aplikacji. `Space`, następny/poprzedni i odczyt czasu używają `tidal.externalTransport` oraz częściowego stanu sesji multimedialnej Windows. Widok i komunikaty NVDA uczciwie wskazują oryginalny TIDAL. Nie ma jeszcze opcji natywnego odtwarzania w GUI, nowego skrótu, instalacji ani testu NVDA.

Nie podłączać GUI do `tidalapi_amc_player_probe.run()`: ten jednorazowy próbnik uruchamia i zamyka własny proces LiteHost. Osobny magazyn sesji Windows jest przygotowany, lecz brak trwałego adaptera dla GUI, który odtwarza sesję, w razie potrzeby prowadzi dostępne logowanie urządzenia, rozwiązuje pojedynczy utwór, utrzymuje lokalne proxy przez czas odtwarzania i zamyka je przy końcu lub błędzie. Dodatkowo nie potwierdzono jeszcze słyszalnego pełnego utworu. Z tych przyczyn przełącznik GUI jest odroczony; dotychczasowy Enter pozostaje punktem powrotu.

### Kontrakt wdrożenia po teście audio

1. Dodać w AMC Test osobne, jawne polecenie „Odtwórz utwór w AMC Test (eksperymentalnie)” dla zaznaczonego utworu TIDAL. Enter i `Ctrl+Enter` zachowują dotychczasowe przekazanie do oryginalnego TIDALa. Nie dodawać komendy do profilu Public ani pokazywać jej bez gotowego adaptera i działającego hosta. Decyzja, czy później zmienić domyślny Enter, wymaga osobnego odbioru użytkownika.
2. Adapter działa poza wątkiem wx, używa osobnego magazynu poświadczeń AMC Test i istniejącej instancji `LiteHostClient`, a do `tidal.nativePlay` przekazuje tylko cyfrowe ID, tytuł, pełny czas i `http://127.0.0.1:<port>/audio/<32-znakowy-token>`. Nie przenosi tokenu ani adresu upstream do GUI, zdarzeń, historii lub stanu aplikacji. Proxy żyje do `playback.ended`, `playback.failed`, jawnego Stop lub zamknięcia aplikacji; przy nowym utworze poprzednie proxy jest domykane po skutecznym przełączeniu. Błąd natywnego startu jest ogłaszany bez automatycznego uruchamiania oryginalnego TIDALa.
3. Potwierdzenie startu pochodzi ze zdarzenia `playback.started` z `sessionId=tidal` i pasującym `id`, a nie z samej odpowiedzi `preparing=true`. W natywnej sesji `Space`, seek, Stop, głośność, mute, wyjście audio i odczyt czasu używają `transport.*` z `sessionId=tidal`; status/Alt+D mówi „TIDAL w AMC Test” i prawdziwy czas. Następny/poprzedni wymaga jawnej listy źródłowej AMC i nowego resolvera dla kolejnego ID; do czasu jego ukończenia polecenie ma być niedostępne w tym trybie, bez przekazania do zewnętrznej kolejki. Po zatrzymaniu lub błędzie GUI wraca do zwykłego stanu katalogu TIDAL.
4. Dodać jedno źródło prawdy dla dostępności nowej komendy i wariantu transportu: `shortcuts.py` (Action, zbiór sesji, etykieta/opis), `menu_model.py`, dispatcher `gui.py`, paleta `command_palette.py`, kontekstowe `Ctrl+F1`, pełna pomoc F1 oraz instrukcja `wxlite/JAK_TESTOWAC.md` muszą odczytywać ten sam stan profilu i adaptera. Dla polecenia bez skrótu pomoc ma podać „Bez skrótu”; ewentualny nowy skrót dodać dopiero z testem kolizji. Etykiety i komunikaty NVDA są po polsku, bez ID, obiektu Python, adresu proxy i nazw pól protokołu.

### Testy odbioru po wdrożeniu

- Testy parytetu w Test/Public oraz przed/po gotowości adaptera: nazwa, opis, widoczność, enabled i wykonanie tego samego polecenia w menu, palecie, pełnej pomocy, `Ctrl+F1` i dispatcherze. Sprawdzić Enter/Ctrl+Enter jako niezmieniony punkt powrotu oraz brak martwej komendy w Public.
- Test adaptera na fałszywym hoście i lokalnym serwerze: brak sekretów w wywołaniu i zdarzeniach, oczekiwanie na właściwy `playback.started`, bezpieczny błąd, odnowienie sesji, zamknięcie proxy przy Stop/End/Failed i przy zamknięciu okna. Test transportu: Space, seek, czas, mute, wyjście i zatrzymanie są kierowane do sesji `tidal`, bez zapisania historii/resume.
- Ręczny odbiór na pojedynczym pełnym utworze powyżej 30 s: słyszalne próbki na wybranym wyjściu, prawdziwy postęp poza 30 s, pauza/wznowienie, seek i koniec. NVDA: początkowy fokus, nazwa komendy, nawigacja menu/palety, zamknięcie dialogu logowania, powrót fokusu po Anuluj/Zapisz i ponowne otwarcie. Nie uznawać samego `PLAYING` lub wzrostu licznika za sukces.

Wersja, build, instalacja i publikacja po tym etapie: bez zmian. Projekt GUI nie jest implementacją.

## Wynik pilota audio i logowania — 4 listopada 2026

Ta sekcja zastępuje wcześniejsze stwierdzenie, że nie potwierdzono słyszalnego dźwięku. Po ponownym, bezpośrednim zatwierdzeniu kodu urządzenia oddzielna sesja `tidalapi` została zapisana w Menedżerze poświadczeń Windows pod osobnym kluczem AMC Test. Jednorazowy pilot wywołał testowy `tidal.nativePlay` przez lokalne proxy. Otrzymano `playback.started`, a później `transport.status` z pozycją **45 sekund** podczas odtwarzania; pilot zakończył się `played_beyond_30s` i zatrzymał testowy host. Użytkownik w trakcie tej próby wyraźnie potwierdził: **słyszę muzykę**. To pierwszy potwierdzony odsłuch rzeczywistego utworu TIDAL w torze audio AMC, odrębny od oryginalnej aplikacji TIDAL.

Osobny proces sprawdził później `restore_session` z Menedżera poświadczeń: `session_reused=True`, bez nowego kodu i bez odtwarzania. Odczyt przed autoryzacją dał `credential_present=False`. Testy pilota i resolvera bez konta: 29/29. Testowy LiteHost uruchomił się również z nieistniejącym plikiem ustawień (`host_ready=True`); skasowano utworzoną wcześniej kopię `work/TID-001/pilot-state.json` o wielkości około 12 MB. Oryginalnego `state.json` użytkownika i działającej instalacji AMC Test nie zmieniono.

Zakres potwierdzenia: faktycznie słyszalny dźwięk i postęp przez 45 sekund w **osobnym pilocie**, a nie w interfejsie wxPython. Nie sprawdzono jeszcze całego utworu, pauzy, seek, następny/poprzedni, wyjścia wybranego w GUI ani NVDA. Dokumentowany wcześniej plan odroczenia GUI nie jest już uzasadniony brakiem dźwięku; pozostaje potrzeba trwałego adaptera sesji/proxy oraz parytetu poleceń i pomocy. Do dalszego wdrożenia wyznaczono agentów równoległych. Brak nowej instalacji i wydania.

Commity etapu: `11f4d5eb` (host), `8deda9d5` (resolver/proxy), `4695655c` (magazyn sesji i pilot), `d7a9994d` (wcześniejszy zapis). Sam wynik testu na koncie nie zawiera kodu, tokenu, manifestu ani adresu źródłowego.

### Doprecyzowanie użytkownika z 4 listopada 2026 — Shift+A i TIDAL Connect

Użytkownik potwierdził działanie pilota i poprosił o sprawdzenie przeniesienia dźwięku przez Shift+A, ewentualnie TIDAL Connect. W obecnym GUI Shift+A dla sesji TIDAL zgłasza tylko urządzenie oryginalnego odtwarzacza; nowa sesja natywna ma już w hoście operacje `audio.outputs`/`audio.selectOutput` na lokalnych wyjściach Windows, lecz GUI musi rozpoznać ten tryb i dopiero wtedy udostępnić dialog. To pozwoli wybrać kartę/DAC Windows, a nie urządzenie TIDAL Connect. Oficjalna dokumentacja TIDAL podaje, że integracje TIDAL Connect są obecnie dostępne dla partnerów sprzętowych (`https://developer.tidal.com/documentation/connect`), a użytkownik wybiera zgodny głośnik w oryginalnej aplikacji TIDAL (`https://support.tidal.com/hc/en-us/articles/360004565898-Tidal-Connect`). `tidalapi` nie daje potwierdzonego publicznego sterowania TIDAL Connect. Nie reklamować zwykłego wyboru wyjścia Windows jako Connect; ewentualny Cast/UPnP byłby osobnym, późniejszym torem i wymaga testu jakości, opóźnienia oraz sterowania.

## Wdrożenie GUI i paczka próbna — 4 listopada 2026

Ten zapis zastępuje wcześniejszy plan odroczenia interfejsu: adapter i jawna komenda są już w kodzie AMC Test. Nie zastępuje potwierdzonego odbioru użytkownika z pilota, który dotyczył osobnego procesu.

### Zachowanie i miejsca w interfejsie

- W sesji TIDAL na **utworze** dostępne jest polecenie „Odtwórz utwór TIDAL w AMC Test (eksperymentalnie)” w menu, palecie i pomocy, bez nowego skrótu. `Enter` na utworze i `Ctrl+Enter` na albumie lub playliście nadal przekazują odtwarzanie do oryginalnego TIDALa.
- Podczas natywnego odtwarzania `Space` steruje pauzą, istniejące skróty seek i czasu dotyczą sesji AMC, `PageUp`/`PageDown` wybierają sąsiedni utwór z bieżącej listy AMC, a `Shift+A` otwiera wybór **lokalnego wyjścia Windows**. To nie jest TIDAL Connect. Status odróżnia „TIDAL w AMC Test” od oryginalnej aplikacji. Polecenie Stop kończy strumień i zamyka lokalne proxy. W profilu Public komenda natywna i jej moduły są niedostępne.
- Logowanie do odtwarzania jest osobną sesją `tidalapi` w Menedżerze poświadczeń Windows. Dostępne okno podaje adres i kod urządzenia z etykietami dla czytnika ekranu. Poprzednie komunikaty o wygasłych kodach są historyczne; udany pilot zapisał sesję, którą osobny proces odczytał bez kolejnego kodu.

### Kod, testy i build

- Adapter wxPython i testy: `bf3fe6ef`; podłączenie GUI, transportu, menu, pomocy i zachowanie Public: `a0c2955b` (z poprawką `16ba61ca`). Próby hosta na syntetycznym 72-sekundowym FLAC, w tym pauza, seek, zmiana wyjścia i Stop: 1/1, commit `85e957f0`. Testy adaptera: 7/7; zestaw adapter/proxy/pilot/logowanie: 44/44; celowane testy GUI: 110/110. Wyniki testów syntetycznych nie dowodzą odsłuchu w oknie GUI.
- Build Release LiteHost ukończony bez błędów i ostrzeżeń. Z HEAD `a0c2955b` utworzono przenośną paczkę Test `work/TID-001/bundle-candidate` (3734 pliki), z przypiętym `tidalapi==0.8.11` i zależnościami tylko dla Test. `AMC.exe --sprawdz-pakiet`, `AMC.exe --sprawdz`, diagnostyka wbudowanego Pythona i izolowany `--sprawdz-start-cicho` zakończyły się kodem 0. Jest to **paczka próbna**, nie instalacja ani wydanie.
- Pełny zestaw layoutu uruchomiony w globalnym Pythonie: 34/35; jedyna porażka wynikała z braku wxPython w tym interpreterze. Ten jeden test powtórzono w interpreterze z zainstalowanego AMC Test i przeszedł. Nie oznacza to pełnego zestawu 35/35 w jednym środowisku.

### Podgląd na kopii danych i pozostały odbiór

- Uruchomiono paczkę obok instalacji użytkownika na oddzielnej nazwie blokady i kopiach `state.json`/`library.db`; nie zatrzymano działającego AMC Test. Podgląd otworzył prawdziwe okno, Menedżer sesji miał czytelne pozycje, klawiaturą włączono TIDAL, katalog pokazał 808 pozycji, a wybrany album zgłosił 13 utworów. Podczas dalszej nawigacji podgląd się zamknął; log hosta wskazuje zwykłe zamknięcie wejścia, a w bieżącej próbie nie powstał zrzut awarii. Nie potwierdzono jeszcze startu utworu z GUI ani zachowania NVDA.
- Po zakończeniu procesu usunięto dokładnie pięć tymczasowych kopii plików stanu i baz z `C:\Users\micha\Documents\Codex\2026-11-04\w\work\tidal-preview-profile` (około 38 MB); oryginały użytkownika i zapis sesji w Menedżerze poświadczeń pozostały. Zachowano tylko niejawny dziennik techniczny podglądu i paczkę do następnej próby.
- Do odbioru pozostaje jeden pełny utwór uruchomiony z GUI, pauza/wznowienie, seek, `PageUp`/`PageDown`, `Shift+A` na wskazanym wyjściu, fokus i etykiety odczytywane przez NVDA oraz decyzja, czy po akceptacji zmienić domyślne `Enter`. Nie instalowano i nie publikowano paczki, gdy używana instalacja AMC Test nadal działa.

Wersja wydana: bez zmian. Commit źródłowy paczki: `a0c2955b`. Bieżący zapis zadania należy zacommitować oddzielnie. Nieoficjalny interfejs `tidalapi` może przestać działać; zaszyfrowane manifesty są odrzucane bez próby obchodzenia DRM. TIDAL Connect pozostaje poza tą integracją.

## Przygotowanie aktualizacji — 4 listopada 2026

Użytkownik polecił wysłać kod na GitHub i zaktualizować AMC Test. Aktywna instalacja z godziny 12:40 pochodzi z commitu `ff2d2acc` i zawiera poprawki rozpoznawania nieobecne wcześniej w gałęzi TIDAL. Scalono oba kierunki bez konfliktów kodu; konflikty dwóch opisów rozwiązano z zachowaniem historii. Commit scalenia: `d3322097`. Testy celowane po scaleniu: 173/173 zaliczone na runtime wxPython z katalogiem bieżących źródeł na początku ścieżki modułów. Pierwsza próba testów wskazała starszy pakiet zainstalowany przed źródłami i zakończyła się błędem importu; po poprawieniu kolejności ścieżek przeszła w całości.

Skrypty budowania Test przyjmują teraz jawne `-TidalSitePackages` z odizolowanego venv; Public odrzuca ten parametr. Numer techniczny nowego kandydata: `0.1.0-alpha.469`, po zainstalowanym `alpha.468`. To przygotowanie źródeł, jeszcze bez nowej paczki, instalacji, publikacji wydania ani testu GUI/NVDA.

### Materiał do dokumentacji użytkownika — szkic

> W AMC Test otwórz sesję TIDAL i zaznacz pojedynczy utwór. W menu Biblioteka wybierz „Odtwórz utwór TIDAL w AMC Test (eksperymentalnie)”. Po starcie muzyki Spacja wstrzymuje i wznawia, Page Up i Page Down przechodzą do sąsiedniego utworu z bieżącej listy, a Shift+A wybiera lokalne wyjście dźwięku Windows. Enter nadal przekazuje utwór do oryginalnej aplikacji TIDAL. Gdy zapisane połączenie odtwarzania wygaśnie, AMC Test pokaże jednorazowy kod do zatwierdzenia na stronie TIDAL. TIDAL Connect nie jest obsługiwany przez to polecenie.

### Korekta kontroli paczki — 4 listopada 2026

Po wspólnym commicie zmiany folderu Time Shift `63bc2b36` testy celowane TIDAL i Time Shift przeszły 44/44. Pierwszy nowy bundle `alpha.469` nie przeszedł kontroli manifestu C# (kod 2), mimo zgodności rozmiarów i skrótów wszystkich 4193 plików. Przyczyną była brakująca deklaracja `tidal.native-playback` w kontrakcie profilu Test launchera i aktualizatora. Zsynchronizowano kontrakt C# z profilem Python; profil Public pozostaje bez tej możliwości. Testy launchera po poprawce: 39/39, w tym rozszerzenie profilu Test przy aktualizacji ze starszej wersji. Paczkę trzeba zbudować ponownie i przejść pełną kontrolę przed instalacją i wydaniem. Poprzednia paczka próbna po uruchomieniu niezależnych testów Pythona miała zmienione pliki `.pyc`; nie była podstawą nowego wydania.

### Wydanie wspólne na GitHub — 4 listopada 2026

Źródło paczki i instalatora: `89698cde12edf912edd3407b9a5507f81b9616af` (gałąź GitHub `codex/konfigurowalne-skroty-20261026`). `Build-AMCBundle.ps1` zakończył pełną kontrolę 4193 plików i kontrolowany start GUI/hosta. `Build-AMCInstaller.ps1` zakończył kontrolę plików, skan poświadczeń i kompilację instalatora `AMC-Test-2026-11-04-2106.exe` (143817216 bajtów, SHA-256 `22dcbcc40ec97f0e5162925d901e0dea52150c655fccbd24862896da961ba051`). Wydanie wstępne GitHub `test-2026.11.04.2106` jest publicznie odczytane jako opublikowane; instalator, plik integralności i manifest aktualizacji mają stan `uploaded` i oczekiwane rozmiary. To potwierdza publikację, nie lokalną instalację. Użytkownikowi zadano pytanie o aktywne/zaplanowane nagrywanie przed zamknięciem AMC Test; do odpowiedzi bieżąca instalacja `alpha.468` pozostaje nietknięta. Rzeczywisty odbiór natywnego TIDAL z okna, `Shift+A` i NVDA nadal jest do wykonania.

## Zgłoszenie po instalacji — 5 listopada 2026

Użytkownik zainstalował `alpha.469`. Zgłosił komunikaty diagnostyczne przy pierwszym starcie, kilkusekundowe opóźnienie dużej listy TIDAL, a także to, że Enter przekazuje utwór do oryginalnego TIDAL i F6 mówi o braku odtwarzacza AMC. To zgłoszenie zastępuje wcześniejsze przypuszczenie, że samo wydanie stanowi odbiór GUI.

Odczyt instalacji potwierdził manifest źródłowy `89698cde` i działający proces z folderu AMC Test. Zainstalowany launcher przechodzi podstawową i pełną kontrolę pakietu (0/0). W dzienniku hosta synchronizacja 944 elementów TIDAL trwała od 23:39:54 do 23:40:21 czasu polskiego; o 23:50:25 host odnotował przekazanie wybranego utworu oryginalnej aplikacji. Pierwszy proces hosta po instalacji wystartował o 23:27:21 i zamknął się z kodem 0 po około 0,5 s; drugi wystartował o 23:27:27. Dziennik launchera nie zawiera bieżącego wyjątku tego startu, a stare ślady wx `SetLabel()` pochodzą z poprzednich dni. Dokładna treść okna diagnostycznego i powtarzalność są przedmiotem pytania do użytkownika.

Enter/F6 działają zgodnie z wcześniej wydanym kontraktem: Enter wybiera zewnętrzny TIDAL, natomiast natywne GUI wymaga polecenia Biblioteka → Odtwórz utwór TIDAL w AMC Test (eksperymentalnie). Doprecyzowano komunikat F6, aby wskazać tę drogę. Użytkownika poproszono o sprawdzenie natywnego polecenia, dźwięku i F6 przed ewentualną zmianą domyślnego Enter.

W izolowanym oknie testowym wypełnienie 944 wierszy zwykłej listy trwało 363,55 ms, a listy wirtualnej 5,99 ms; przesunięcie zaznaczenia w obu wariantach około 1 ms. Zmieniono listę TIDAL na sprawdzony już dla dużych list mechanizm wirtualny. Testy zachowania listy i natywnego TIDAL: 12/12. Szeroki plik `test_gui_logic.py` uruchomiony z rzeczywistym wxPython miał 34 zaliczone i 14 niezaliczonych testów; wiele niepowodzeń wynika z założeń środowiskowych testów (m.in. brak `wx.App` oraz konstrukcja obiektu C++ przez `object.__new__`), ale pozostałych nie rozstrzygnięto. Nie traktuje się tego pliku jako zaliczonej regresji. Zmiana listy i komunikatu istnieje wyłącznie w źródłach; nie zbudowano ani nie zainstalowano nowej wersji. Potrzebny żywy odbiór NVDA na dużej liście.

### Korekta użytkownika i naprawa źródeł — 5 listopada 2026

Użytkownik potwierdził, że `alpha.469` mocno spowalnia menu i nawigację na całej liście albumów TIDAL, a polecenia natywnego odtwarzania nie ma ani w menu kontekstowym, ani w widocznych miejscach interfejsu. Wskazał też brak użytecznej strzałki w lewo i czasu utworu przy zewnętrznym odtwarzaniu. Ta korekta zastępuje wcześniejszy wniosek, że samo przełączenie listy na tryb wirtualny wystarcza. Wcześniejsze żywe próby NVDA na wirtualnych listach AMC ujawniły opóźnienia odczytu i nieaktualne nazwy wierszy; dlatego niewydaną zmianę `virtual=True` dla TIDAL wycofano.

Przyczyną niedostępności polecenia był rozdźwięk identyfikatorów: rzeczywisty katalog przekazuje `tracks:123`, a pięć bramek GUI akceptowało tylko `123`. Wspólny parser wyodrębnia teraz cyfrowy identyfikator dla adaptera; menu, paleta, pomoc i wykonanie polecenia używają tego samego warunku. Polecenie dodano do menu kontekstowego utworu. Enter na utworze nadal wybiera zewnętrzny TIDAL, zgodnie z przyjętą przez użytkownika kolejnością: najpierw sprawdzić opcję w menu kontekstowym.

Główna kolekcja albumów, ulubionych i playlist trafia do zwykłej listy Windows po 150 wierszy i ma jawny wiersz „Załaduj więcej”. Zwiększenie strony korzysta z kompletnego, uporządkowanego wyniku w pamięci, bez ponownego wywołania katalogu. Filtr przeszukuje cały wynik przed ograniczeniem strony, więc może znaleźć album po pozycji 800. Pozycje utworów w otwartym albumie mają już pole czasu w szczegółach, jeśli katalog TIDAL podał czas; odczyt czasu w odtwarzaczu wymaga działającej sesji natywnej i osobnego odbioru NVDA.

Testy źródeł po tej korekcie: 137/137 testów TIDAL i regresji filtra, listy, menu oraz palety (stronicowanie, filtr całego katalogu, format identyfikatorów, menu kontekstowe, nawigacja i transport). Pierwsza próba szerszej regresji ujawniła, że metoda filtrowania wywoływała nową ścieżkę także w sesji plików; ograniczono wywołanie do TIDAL i ponowiono zestaw z wynikiem 137/137. To potwierdza kod, nie potwierdza jeszcze płynności i odczytu w żywym NVDA, słyszalności z okna, nowej paczki, instalacji ani wydania. Do sprawdzenia: realny czas reakcji menu, fokus po „Załaduj więcej”, utwór w GUI, F6, przewijanie i czas oraz komunikat diagnostyczny pierwszego uruchomienia.

Szkic instrukcji dla użytkownika: Otwórz TIDAL w AMC Test, wejdź do albumu i zaznacz utwór. Otwórz menu kontekstowe i wybierz „Odtwórz utwór TIDAL w AMC Test (eksperymentalnie)”. Po rozpoczęciu dźwięku F6 otwiera odtwarzacz, strzałki przewijają, a polecenia czasu odczytują postęp. W głównej kolekcji TIDAL wiersz „Załaduj więcej” na końcu listy pokazuje następne 150 pozycji. To szkic do odbioru, bez publikacji.

### Próba GUI na osobnej kopii profilu — 5 listopada 2026

Z commitu `c20f1c03` zbudowano paczkę Test z 4193 plikami. Pełna kontrola manifestu i kontrolowany start GUI oraz hosta przeszły. Pierwsza próba z runtime zainstalowanej wersji została słusznie odrzucona, bo zawierał już pakiety TIDAL; starszy czysty runtime nie miał wymaganych wspólnych wersji. Zrobiono kopię runtime `alpha.469` tylko w `work/TID-001/alpha470-runtime-base`, usunięto z tej kopii 12 jawnych katalogów sześciu pakietów TIDAL, a skrypt dołożył je z przypiętego `TID-001-venv`. Instalacja użytkownika nie została zmieniona. Manifest gotowej paczki: profil Test, `sourceRevision=c20f1c03`, `sourceDirty=false`.

Podgląd uruchomiono z `work/TID-001/alpha470-bundle` na kopii `state.json`, `library.db` i stanu wxPython w `work/TID-001/alpha470-preview-profile`. Log hosta pokazuje synchronizację 944 elementów w około 67 sekund przy bieżącym łączu. GUI pokazał „Biblioteka TIDAL, 808 pozycji, pokazano 150”; menu Pliki otworzyło się w obserwacji narzędzia w mniej niż sekundę, a pierwszy album miał 13 utworów z czasem w kolumnie szczegółów. Menu kontekstowe rzeczywistego utworu „Manhattan Man” pokazało jako drugą pozycję polecenie natywne. Po jego wybraniu odtwarzacz AMC wyświetlił tytuł i czas `0:00 z 5:45`, postęp czasu rósł. Strzałki prawo/lewo zmieniały pozycję o około 10 sekund, Spacja przestawiła przycisk na „Odtwórz”, F6 przełączał listę i odtwarzacz, Page Down przeszedł do „Paris Groove”, a Page Up wrócił do „Manhattan Man”. Shift+A otworzył listę „Domyślne urządzenie systemowe”, „Głośniki (2 — Realtek(R) Audio) — obecnie domyślne w Windows” oraz „Interfejs SPDIF (USB SPDIF Adapter)”; po Anuluj fokus wrócił na przycisk odtwarzacza. Narzędzie nie nagrywało dźwięku i nie słyszało NVDA, więc nie stanowi to potwierdzenia odsłuchu ani mowy czytnika.

W tej próbie Stop zamknął strumień i status mówił „brak bieżącego materiału”, lecz GUI zostawił martwy widok odtwarzacza z przyciskiem „Wstrzymaj”. To odkrycie zastępuje wcześniejsze założenie o poprawnym powrocie po Stop. Źródło poprawiono: po zakończeniu aktywnego natywnego utworu czyści jego stan i wraca do listy, zachowując zaznaczenie. Test tej ścieżki i stronicowania: 17/17. Zmiana po tej próbie nie jest jeszcze w zbudowanej paczce; wymaga nowego commitu, ponownego buildu i odbioru. Po zakończeniu próby procesy podglądu zostały zamknięte; prywatne kopie profilu czekają na ponowny test i usunięcie.

### Potwierdzenie poprawki Stop i przygotowanie wersji — 5 listopada 2026

Poprawka ma commit `8b351f10`; 138/138 celowanych testów TIDAL oraz regresji filtra, listy, menu i palety przeszło. Ponownie zbudowana paczka Test ma 4193 pliki, profil Test, `sourceDirty=false` i manifest commitu `8b351f10`; pełna kontrola integralności i kontrolowany start GUI/hosta przeszły. Powtórny podgląd na kopii profilu potwierdził start „Manhattan Man” z menu kontekstowego, a następnie Stop: widok wrócił do listy 13 utworów, fokus do listy, status do „TIDAL, brak bieżącego materiału w AMC”, komunikat do „Odtwarzanie TIDAL zatrzymane”. Brak odsłuchu i mowy NVDA w automatycznym podglądzie pozostaje ograniczeniem. Ponowny podgląd startował z zapamiętaną sesją TIDAL i pustą listą; przełączenie na Radio i z powrotem rozpoczęło pobranie katalogu i pokazało 808 pozycji. To zachowanie startowe wymaga osobnej diagnostyki, jeśli wystąpi w instalacji.

Pierwsza próba instalatora `alpha.470` słusznie odmówiła: `Directory.Build.props` wciąż podawał `alpha.469`, a narzędzie wymaga identycznej wersji skompilowanego uruchamiacza i instalatora. Wspólny numer źródłowy został podniesiony do `0.1.0-alpha.470`; przed wydaniem trzeba ponownie zbudować paczkę i instalator z jednego nowego commitu. Żadnego instalatora nie utworzono ani nie zainstalowano na podstawie poprzedniej paczki.

### Wydanie i instalacja alpha.470 — 5 listopada 2026

Źródło naprawy listy i polecenia TIDAL ma commit `c20f1c03`, poprawka powrotu po Stop `8b351f10`, a wspólna wersja do wydania `bbfd9873eb7aa9c91574b876514f268066f9dffa`. Z ostatniego commitu zbudowano paczkę Test: 4193 pliki, `sourceDirty=false`, kontrola integralności i kontrolowany start GUI/hosta zaliczone. Instalator `AMC-Test-2026-11-05-0056.exe` ma 143819425 bajtów i SHA-256 `548396d0b8aa8227f33cc03f90fc5701e406409737445f13ac04f8cb58c691b2`; skan sekretów zaliczony. Celowo zachowano poprzedni instalator `alpha.469` w `publish/installers/alpha.469` jako punkt powrotu.

Commit `bbfd9873` wypchnięto do gałęzi `codex/konfigurowalne-skroty-20261026`. Wydanie wstępne GitHub `test-2026.11.05.0056` jest opublikowane. Odczyt zdalny potwierdził trzy zasoby: instalator 143819425 bajtów w stanie `uploaded` z tą samą sumą SHA-256, plik integralności i manifest aktualizacji. Lokalny `release-ready-test-0.1.0-alpha.470.json` ma `published=true`. To potwierdza publikację, oddzielnie od instalacji.

Tuż przed instalacją nie działał żaden proces AMC Test; wcześniej użytkownik potwierdził brak nagrywania i zgodę na zamknięcie programu. Instalator uruchomiony w trybie cichym zakończył się kodem 0, bez instalowania dodatku NVDA. Zainstalowany `C:\Users\micha\AppData\Local\Programs\AMC Test\AMC.exe` ma ProductVersion `0.1.0-alpha.470`, manifest `sourceRevision=bbfd9873`, `sourceDirty=false` i `installId=AMC.Test.win-x64`. Pełna kontrola zainstalowanego pakietu zakończyła się kodem 0. Stabilny launcher otworzył dokładnie jedną instancję, PID 29856, z oknem „AMC Test — 05.11.2026, 00:56”; początkowy widok Radio miał fokus na liście i nie wyświetlił okna diagnostycznego. Nie sprawdzono jeszcze odsłuchu i mowy NVDA na instalacji użytkownika. Wcześniejszy izolowany podgląd GUI potwierdził pozycję polecenia, odczyt czasu, F6, przewijanie, następny/poprzedni i Shift+A na poziomie stanu interfejsu. Zdalna synchronizacja katalogu może nadal trwać kilkadziesiąt sekund, choć jej wynik pojawia się teraz po 150 wierszy; zachowanie startu z zapamiętaną sesją TIDAL na osobnej kopii profilu pozostaje do obserwacji.

Szkic krótkiego changelogu do korekty użytkownika: TIDAL pokazuje katalog po 150 pozycji z wierszem „Załaduj więcej”; w menu kontekstowym utworu jest „Odtwórz utwór TIDAL w AMC Test (eksperymentalnie)”; po uruchomieniu działają widok odtwarzacza, czas i przewijanie, a Stop wraca do listy. Enter nadal uruchamia dotychczasowe przekazanie do oryginalnego TIDAL. Nie publikować tego szkicu jako instrukcji bez odbioru NVDA i odsłuchu w zainstalowanym programie.

### Korekta użytkownika: cyfry i płynność całego AMC — 5 listopada 2026

Użytkownik potwierdził, że zainstalowane `alpha.470` uruchamia się, odtwarza TIDAL i pozwala przewijać utwór. Zgłosił brak skoków procentowych cyframi, możliwe braki innych funkcji odtwarzacza, zbędny komunikat „Przygotowywanie utworu” oraz lekkie spowolnienie strzałek Radia i przełączania sesji przez Ctrl+cyfry. To potwierdzenie zastępuje wcześniejszy brak odbioru odsłuchu w instalacji; nie stanowi jeszcze testu każdej funkcji i mowy NVDA.

#### Uzasadnienie i decyzje

Cyfry `0–9` były już przypisane do skoku co 10%, lecz sesja TIDAL odrzucała ich akcje przed wykonaniem. Tak samo blokowała Home/End i regulację głośności, choć lokalny host TIDAL obsługuje skok absolutny oraz głośność. Dopisano te akcje do jednej listy możliwości TIDAL i do bramki wymagającej natywnego odtwarzania; dlatego zewnętrzny TIDAL nie otrzymuje mylących poleceń. Skok procentowy i End odczytują na wątku roboczym bieżący status utworu TIDAL, sprawdzają jego identyfikator i czas, zamiast używać potencjalnie starego czasu Radia po zmianie sesji. Home używa tej samej drogi z pozycją zero. Zdarzenie „preparing” zostało usunięte z mowy; pozostały potwierdzenie startu, żądanie logowania i błędy. Nie włączono losowania TIDAL ani zakładek, których pojedynczy strumień natywny nie zapewnia.

Porównanie kodu `alpha.469` i `alpha.470` nie wykazało zmian w obsłudze strzałek Radia ani Ctrl+cyfry. Nowe stronicowanie kończy się natychmiast poza TIDAL. W działającej instalacji proces GUI zużywał około 0,14 s CPU na 5 s, iCloudDrive 0,00 s, natomiast host audio okresowo 4,86 s CPU na 5 s, głównie na jednym wątku; później spadł do 0,08 s na 3 s bez restartu. Migawka stosów wątku pokazała odczyt pliku/potoku Windows, a osobny wątek automatycznego odświeżania podcastów czekał na wynik. Plik migawki: `work/TID-001/alpha470-host-stack-20261105.txt`, lokalny, prywatny, nie publikować. Nie dowodzi to źródła odczuwalnego opóźnienia. Potrzebne jest porównanie czasu reakcji strzałek i Ctrl+cyfry przy aktywnym TIDAL oraz po jego zatrzymaniu; nie zmieniono na ślepo obsługi Radia.

#### Testy i wynik

Celowane testy TIDAL, menu, palety i adaptera: 84/84. Szersza regresja TIDAL, menu, palety, skrótów i transportu: 201 zaliczonych, 2 istniejące niepowodzenia poza zmianą (stary oczekiwany opis Ctrl+, i pominięty w teście dynamiczny Ctrl+Enter albumu Spotify). Osobny starszy moduł `test_transport_parity_gui_wiring.py` nadal ma trzy błędy niepełnych atrap ramki, niezwiązane z nową funkcją. Test tłumaczeń ma 12 zaliczonych i 3 wcześniejsze niepowodzenia katalogu, obejmujące wiele istniejących komunikatów. Brak jeszcze kompilacji, instalacji i publikacji zmian po `alpha.470` oraz ręcznej próby NVDA cyfr, Home/End i głośności.

#### Znane ograniczenia

Natywne TIDAL nie oferuje tu TIDAL Connect. Enter nadal otwiera oryginalny TIDAL; natywny odtwarzacz uruchamia się z menu kontekstowego utworu. Skoki procentowe wymagają potwierdzonego czasu bieżącego utworu. Przyczyna okresowego obciążenia hosta i wpływ na odczuwalne opóźnienie Radia pozostają nieustalone bez pomiaru A/B.

#### Materiał do dokumentacji użytkownika

W odtwarzaczu TIDAL w AMC Test cyfry od `0` do `9` skaczą odpowiednio do 0–90% utworu. Home wraca na początek, End przechodzi dziesięć sekund przed końcem. Strzałki w górę i w dół zmieniają głośność o 5%, z Shift o 1%. Te funkcje są również opisane w menu Odtwarzanie lub Dźwięk, w palecie `Ctrl+Shift+K` i pomocy. Po wyborze utworu program potwierdza jego rozpoczęcie; nie czyta już komunikatu o przygotowywaniu. Funkcje te dotyczą odtwarzacza AMC, a nie przekazania do oryginalnego TIDAL przez Enter. To szkic do odbioru, bez automatycznej publikacji.

#### Wersja i commit

Poprawki odtwarzacza mają commit `904d9d0a7251696b0fda37f5379d6521db92b017`. Wspólny numer następnego kandydata to `0.1.0-alpha.471`. Stan budowy i wydania uzupełnia następna sekcja; instalacja pozostaje osobnym krokiem.

### Paczka i wydanie alpha.471 — 5 listopada 2026

Commit wydania `9e3834850f2fa4047876a69adaba061fa741fca9` jest na gałęzi `codex/konfigurowalne-skroty-20261026`. Z niego zbudowano izolowaną paczkę Test z 4193 plikami, `sourceDirty=false`, ProductVersion `0.1.0-alpha.471` i nazwą okna `AMC Test — 05.11.2026, 01:45`. Pełna kontrola pakietu i kontrolowany start GUI/hosta zaliczone. Instalator `D:\Projekty Codex\AMC - integracja wxPython\publish\installers\AMC-Test-2026-11-05-0145.exe` ma 143839647 bajtów i SHA-256 `b295bca5b031ea21d7512f937a7edbf3856263795060f3094928b833aaf1ce5d`. Skan sekretów przeszedł; opcjonalny dodatek NVDA Quick App Switcher 0.1.6 ma tę samą sumę co w poprzednim wydaniu. Sama budowa instalatora nie zmieniła instalacji.

Wydanie wstępne GitHub `test-2026.11.05.0145` jest opublikowane. Odczyt zdalny potwierdził instalator, plik integralności i manifest aktualizacji w stanie `uploaded`; rozmiar i suma instalatora są zgodne z lokalnymi. Manifest gotowości ma `published=true`. Stara instalacja `alpha.470` i jej instalator są nadal zachowane. Przed instalacją `alpha.471` zapytano użytkownika o aktywne lub bliskie nagrywanie; do odpowiedzi nie zamykać programu. Nowe cyfry, Home/End i głośność nie mają jeszcze odbioru w zainstalowanym oknie i NVDA, więc publikacja nie jest dowodem ich działania na komputerze użytkownika.

Po potwierdzeniu przez użytkownika działania `alpha.470` usunięto tylko nieużywaną kopię profilu testowego `D:\Projekty Codex\AMC - integracja wxPython\work\TID-001\alpha470-preview-profile` (11 plików, około 75 MB) spod katalogu `work\TID-001`. Oryginalny profil, poświadczenia Windows i instalacja pozostały nienaruszone. Kopia diagnostyczna nie ma Kosza; jej źródłowe pliki pozostają w oryginalnym profilu i można utworzyć nową kopię do kolejnego testu. Paczki i instalatory zachowano do odbioru nowej wersji.
