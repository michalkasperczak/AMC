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
