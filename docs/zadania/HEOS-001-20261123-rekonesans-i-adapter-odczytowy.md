# HEOS-001 — rozpoznanie urządzenia HEOS 200 i adapter odczytowy

**Status 24.11.2026: AMC Test alpha.523 z HEOS jest opublikowanym wydaniem GitHub, dostępnym w aktualizatorze zainstalowanej alpha.522. Zainstalowana alpha.522 nadal nie zawiera HEOS; nie instalowano alpha.523 ani nie wykonano sterowania głośnikiem lub odbioru NVDA. Wcześniejsze sekcje pokazują kolejne etapy; stan końcowy znajduje się na końcu raportu.**

## Cel i zakres

Wprowadzić HEOS do AMC Test według `D:\Projekty Codex\Hermes\Nowe\HEOS-20261122-integracja.md`. Pierwsza bramka to próba odczytowa na wskazanym przez Michała urządzeniu „HEOS 200” (dokładny model do potwierdzenia), a następnie sprawdzony adapter, sesja i dostępny interfejs. HEOS nie trafia jeszcze do AMC Public.

## Stan i wykonane pliki

- Izolowana gałąź HEOS: `codex/heos-next-test-20261123`, oparta na commicie `eabaa0e8` trwającej aktualizacji, w worktree `D:\Projekty Codex\AMC - integracja wxPython\work\HEOS-001-next-test`. Główny checkout na D i worktree bieżącego wydania mają odrębne niezapisane zmiany; nie zostały naruszone.
- `wxlite/amc_wx_lite/heos_client.py`: klient CLI, wykrywanie SSDP i ręczny adres IP, bezpieczny odczyt, stronicowanie, jawne polecenia transportu/głośności/wyciszenia. Odpowiedź spóźniona po limicie czasu nie trafia do następnego żądania; mutacji nie ponawia automatycznie. Klasa odczytowa nadal odrzuca polecenia zapisu.
- `wxlite/tests/test_heos_client.py`: izolowane testy protokołu, wykrywania, stronicowania i braku ponowienia mutacji.
- `wxlite/amc_wx_lite/heos_source.py` i `wxlite/tests/test_heos_source.py`: osobne tożsamości i etykiety urządzeń, grup, źródeł, pozycji katalogu i kolejki oraz stanu; testy duplikatów tytułów, zagnieżdżonego źródła i braku wycieku identyfikatorów do mowy.
- `wxlite/amc_wx_lite/heos_navigation.py` i `wxlite/tests/test_heos_navigation.py`: stan poziomów katalogu, powrót do zaznaczenia oraz odrzucanie spóźnionych list po zmianie widoku lub urządzenia.
- `wxlite/amc_wx_lite/heos_service.py` i `wxlite/tests/test_heos_service.py`: operacje wykonywane przez przyszły wątek roboczy; odczyt celu, katalogu, kolejki i stanu oraz osobne wywołania sterowania. Stacja korzysta z polecenia strumienia, utwór z kolejki z identyfikatorem katalogu nadrzędnego, a album z własnym kontenerem.
- Adapter nie jest jeszcze wpięty w sesję, interfejs, pakiet ani instalację.

## Próba sieciowa

Pierwsze próby SSDP 23.11.2026 nie przyniosły odpowiedzi, ponieważ środowisko odmówiło dostępu do lokalnej sieci. Późniejsza próba z dostępem sieciowym wykryła urządzenie i potwierdziła działanie CLI; szczegóły w korekcie poniżej. Nie wysłano komend zmieniających stan.

## Uzasadnienie i decyzje

- Próba odczytowa przed sterowaniem wynika z notatki integracyjnej. Najpierw sprawdzić model, firmware, graczy, grupy, konto, źródła, znaną kolekcję i kolejkę. Bez danych użytkownika w udostępnianych raportach.
- Klient odczytowy nie przyjmuje komend zapisu; osobny klient sterowania ma jawne metody, lecz nie uruchomiono ich na urządzeniu. Nie ma logowania do konta HEOS w AMC ani zależności `pyheos`: własny adapter działa na Denon Home 200 bez dodatkowej biblioteki.
- Brak wykrycia SSDP nie zamyka pracy: po uruchomieniu urządzenia można powtórzyć wykrycie albo użyć jego lokalnego adresu IP.

## Testy i wynik

- `D:\Projekty Codex\AMC - zależności\python-test-envs\wxpython-4.3.1-cp314\Scripts\python.exe -m pytest wxlite\tests\test_heos_client.py wxlite\tests\test_heos_source.py wxlite\tests\test_heos_navigation.py wxlite\tests\test_heos_service.py -q` z `PYTHONPATH=wxlite`: **41 passed in 0.97s** (ostatnia próba 23.11.2026).
- Testy automatyczne są bezokienkowe i używają atrap. Osobna próba odczytowa na Denon Home 200 potwierdziła wykrywanie, model celu, strukturę źródeł, stronicowanie i model stanu. Nie potwierdza sterowania, dźwięku, interfejsu ani NVDA.
- Nowa warstwa operacji przeszła 23.11.2026 odczyt na Denon Home 200: jeden cel, siedem źródeł, znana głośność i tytuł statusu, pusta kolejka; pięć źródeł zwróciło stronę katalogu, dwa nie udostępniły listy. To stan tej próby, a nie stała liczba źródeł. Nie wysłano żadnej komendy sterowania.
- Nie wykonano kompilacji, instalacji ani publikacji.

## Znane ograniczenia

- Odczyt CLI na Denon Home 200 działa; zgodność poleceń sterowania i zachowanie po utracie połączenia pozostają do próby.
- Brak sesji HEOS i czynności użytkowych w GUI. Odczytowy adapter jest etapem wewnętrznym.
- Stronicowanie katalogu i format bieżącej odpowiedzi sprawdzono odczytowo na Denon Home 200; dalsza zgodność z innymi źródłami i firmware wymaga prób.

## Wersja i commit

Baza aktualizacji `eabaa0e8`; wcześniejszy klient odczytowy `99e602ec6532`, adapter i model `2c7cc6f086f8`, komendy kolejki `00b23968`, nawigacja `996a074e`, warstwa operacji `b6f42b1b`. Żadna wersja AMC z HEOS nie powstała.

## Materiał do dokumentacji użytkownika

HEOS nie jest jeszcze dostępny w AMC Test. Wykryto Denon Home 200 i odczytano jego stan bez wpływu na graną stację. Planowana sesja pozwoli wybrać urządzenie i sterować nim z klawiatury; dźwięk pozostanie na urządzeniu. Biblioteka pokaże tylko źródła naprawdę dostępne przez CLI. Komendy i komunikaty NVDA zostaną opisane po przygotowaniu interfejsu.

## Następny krok

Wpiąć HEOS do AMC Test na podstawie potwierdzonego odczytu: wybór celu, stan odtwarzacza, dostępne źródła, Biblioteka i kolejka. Rozróżnić źródło bez listy od pustej listy oraz odświeżać zmienną listę źródeł. Zaktualizować wspólny model poleceń, menu, pomoc, paletę, testy parytetu i dokumentację. Po przygotowaniu sprawdzalnego interfejsu osobno uzgodnić fizyczną próbę sterowania.

## Korekta i wynik próby sprzętowej — 23.11.2026

Michał uruchomił głośnik i wznowił zadanie tego samego dnia. Wcześniejsze „nie wykryto urządzenia” było wynikiem odmowy dostępu do sieci przez piaskownicę (Windows 10013), a nie dowodem uśpienia lub niezgodności głośnika. Ta korekta zastępuje wcześniejsze przypuszczenie o uśpieniu. Po uruchomieniu próby z dostępem do lokalnej sieci SSDP wykrył jedno urządzenie HEOS; CLI na porcie 1255 odpowiada.

Odczyt potwierdził model **Denon Home 200**, firmware **3.139.175**, jednego odtwarzacza i brak grup. `system/check_account` zwrócił `success`, co potwierdza wykonanie komendy, lecz samo nie dowodzi zalogowania konta. Odczyt stanu, bieżących metadanych, głośności i wyciszenia się powiódł; podczas próby grała stacja. Kolejka zwróciła zero pozycji. Nie zapisano tytułów, nazw kont, identyfikatorów urządzenia ani adresu sieciowego w tym raporcie.

Lista źródeł w kolejnych odczytach miała 5, a następnie 8 pozycji. Po ustabilizowaniu pojawiły się trzy źródła `music_service`, jedno `heos_server` i cztery `heos_service`. `Local Music` zwróciło pustą listę. Dwa serwisy muzyczne zwróciły katalogi, a Historia dwie pozycje typu kontener, z których jedna była oznaczona jako odtwarzalna. Trzy źródła zwróciły `success` bez pola `payload` i bez liczników `returned`/`count`; obecny klient zgłasza dla nich `HeosProtocolError`, co jest uczciwsze od uznania ich za puste. Nie stwierdzono obecności DLNA ani USB. Nie uruchomiono żadnej pozycji.

Próba była wyłącznie odczytowa. Kod klienta i 8 testów na atrapach pozostają w commitcie `99e602ec6532`. Nie wykonano testu sterowania, interfejsu, NVDA, kompilacji, instalacji ani publikacji. Następny krok: zaprojektować adapter źródeł z osobnym stanem źródła bez listy, odświeżaniem źródeł i rozróżnieniem stacji od kolejki; następnie wpiąć HEOS wyłącznie do AMC Test, z pełnym parytetem poleceń i dostępności. Fizyczną próbę zmieniającą odtwarzanie uzgodnić osobno po przygotowaniu interfejsu.

## Decyzja Michała z 23.11.2026 — następna wersja testowa

Michał zezwolił rozwijać HEOS równolegle do właśnie składanej aktualizacji AMC. HEOS ma trafić do testów dopiero w **następnej** wersji AMC Test, po zakończeniu bieżącego wydania. Ta decyzja zastępuje wcześniejsze prowadzenie HEOS na gałęzi kandydata do bieżącej aktualizacji. Prace Apple Music mogą zostać dołączone do następnego Test po osobnej weryfikacji; nie są częścią zadania HEOS. Kod HEOS jest izolowany na własnej gałęzi. Nie skompilowano ani nie opublikowano wersji z HEOS na GitHubie.

Nowy adapter wykrył Denon Home 200 i odczytał jednego gracza. Model źródeł odczytał siedem dostępnych pozycji; źródło zagnieżdżone występujące na urządzeniu ma własny `sid` bez `cid` i otrzymało osobną tożsamość w modelu do przyszłej nawigacji. Źródła zwracające sukces bez listy pozostają stanem niedostępnej listy, a nie pustą kolekcją. W działającym radiu model stanu rozpoznał typ stacji oraz znaną głośność i stan wyciszenia. Nie wysłano mutacji do urządzenia.

## Uzupełnienie z 23.11.2026 — kolejka i nawigacja

Wcześniejsze zdanie, że brak `payload` kończy się `HeosProtocolError`, opisuje stan z pierwszej próby i zostało zastąpione: aktualny klient zgłasza `HeosListUnavailable`, odrębne od pustej listy. Odczyt stronicowany i model stanu sprawdzono też na rzeczywistym Denon Home 200. Nie odtworzono żadnej pozycji.

Dodano jawne polecenia dodania do kolejki w czterech różnych trybach; zastąpienie kolejki wymaga osobnego potwierdzenia wywołującego. Uruchomienie pozycji o przesuwającym się `qid` wymaga ponownego odczytu i zgodności odcisku pełnej kolejki. Radio korzysta z odrębnego polecenia stacji. Testy tych poleceń korzystały wyłącznie z atrap, więc nie dowodzą zgodności sterowania z fizycznym głośnikiem. Nowy model nawigacji odrzuca wyniki poprzedniej listy lub poprzedniego celu oraz przywraca identyfikator zaznaczonego kontenera po Backspace. Tekst dla NVDA pozostaje nazwą użytkową, bez technicznych identyfikatorów.

Nie wykonano testów GUI/NVDA, kompilacji, instalacji ani publikacji. Następnym etapem jest podłączenie sesji AMC Test wraz ze wspólnym routingiem, menu, pomocą i paletą poleceń. Dopiero po gotowym interfejsie trzeba uzgodnić fizyczne próby zmieniające odtwarzanie i kolejkę.

## Etap interfejsu — 23.11.2026

Ta sekcja zastępuje wcześniejsze stwierdzenia w tym raporcie, że sesja HEOS nie jest podłączona do GUI. Kod sesji jest przygotowany na izolowanej gałęzi następnej wersji AMC Test; **nie ma go w zainstalowanej ani opublikowanej wersji**.

- HEOS jest osobną sesją Test, domyślnie wyłączoną w Menedżerze sesji. Włączenie wymaga wolnego miejsca w limicie dziesięciu sesji i nie przenumerowuje dotychczasowych skrótów. Profil Public nie pokazuje HEOS.
- **Korekta z 24.11.2026:** powyższy limit dziesięciu aktywnych sesji został usunięty w przygotowywanej alpha.524. Dotyczy to wszystkich sesji; tylko pierwszych dziesięć ma skróty numeryczne.
- Po wejściu do sesji AMC wyszukuje urządzenia i istniejące grupy. Ctrl+F5 daje wybór ponownego wykrycia albo ręcznego adresu IP. Enter wybiera cel bez zmiany odtwarzania i otwiera odtwarzacz. Ctrl+L pokazuje zmienną listę źródeł; Enter otwiera katalog lub odtwarza pozycję; Backspace wraca do rodzica. Ctrl+U otwiera Ulubione, Ctrl+P Playlisty, Ctrl+Q kolejkę. Ctrl+Shift+Q dodaje odtwarzalną pozycję na koniec kolejki, Ctrl+Shift+Enter jako następną; stacje nie są tak dodawane. Akcje kolejki wymagają odczytu przed i po zmianie. Spacja steruje play/pauzą, Page Up/Down poprzednią/następną pozycją, Ctrl+M wyciszeniem; suwak i istniejące skróty głośności sterują wybranym celem.
- Każdy widok ma użytkową nazwę dostępną dla NVDA, a identyfikatory HEOS pozostają w modelu. Wyniki list z poprzedniego widoku lub celu są odrzucane. Dźwięk gra na urządzeniu; zamknięcie AMC nie wysyła Stop. Filtr dotyczy tylko aktualnie wczytanych pozycji, nie całego zdalnego katalogu.
- Wspólny model poleceń zasila routing klawiatury, menu, paletę i pomoc. Test parytetu porównuje dla HEOS skrót, widoczność w menu i palecie, opis w pomocy oraz przekierowanie wykonania. Testy obejmują także jawny odczyt kolejki po dodaniu.

Test bez otwierania okien: `cd wxlite; python -m pytest -q tests/test_heos_session_contract.py tests/test_menu_exposes_existing_actions.py tests/test_session_order.py tests/test_session_manager.py tests/test_release_profile.py tests/test_command_palette.py tests/test_shortcuts.py tests/test_shortcut_customization.py tests/test_heos_client.py tests/test_heos_source.py tests/test_heos_navigation.py tests/test_heos_service.py tests/test_heos_rows.py` — **195 passed w 5,39 s**. `py_compile` GUI, skrótów i menu oraz `git diff --check` przeszły. Próba sprzętowa pozostała wyłącznie odczytowa; nie sprawdzono jeszcze sterowania, dźwięku, fizycznego NVDA, instalacji ani budowy na GitHubie. Obowiązuje wcześniejsza decyzja o osobnym uzgodnieniu mutujących testów na głośniku.

### Szkic instrukcji dla użytkownika — do korekty Michała

W Menedżerze sesji włącz HEOS i przełącz się do niego. Wybierz głośnik Enterem. Jeśli go nie widać, użyj Ctrl+F5 do ponownego wyszukania lub wpisania adresu IP. Ctrl+L otwiera Bibliotekę źródeł, Ctrl+U Ulubione, Ctrl+P Playlisty, a Ctrl+Q kolejkę. Enter otwiera folder albo odtwarza pozycję; Backspace wraca. Ctrl+Shift+Q dodaje utwór na koniec kolejki, a Ctrl+Shift+Enter jako następny. Spacja wstrzymuje lub wznawia, Page Up/Down zmienia pozycję, Ctrl+M wycisza. Muzyka pozostaje na głośniku HEOS. Dostępne źródła zależą od jego konfiguracji.

### Szkic changelogu — do korekty Michała

AMC Test: dodano opcjonalną sesję HEOS z wykrywaniem urządzeń, ręcznym adresem IP, przeglądaniem dostępnych źródeł, podglądem i dodawaniem do kolejki oraz podstawowym sterowaniem odtwarzaniem i głośnością. AMC Public nie udostępnia sesji HEOS. Kompatybilność sterowania z Denon Home 200 i odbiór NVDA pozostają do potwierdzenia przed szerszym udostępnieniem.

## Integracja z wydaną alpha.518 — 23.11.2026

Po potwierdzeniu wydania alpha.518 scalono jej gałąź `codex/amc-unified-release-20261123` z izolowaną gałęzią HEOS. Zachowano wideo w AMC Public zgodnie z późniejszą decyzją Michała opisaną w `RELEASE-ALPHA518-20261123.md`; z profilu Public wykluczono tylko HEOS i Apple Music. Moduły HEOS pozostają w paczce Public jako wymagane przez wspólny import GUI, lecz sesja i możliwość `session.heos` są ukryte w profilu Public. Wcześniejszy zapis wydania alpha.518 o wykluczeniu `heos_client.py` nadal opisuje wyłącznie tamto wydanie, nie przyszłą paczkę.

Po scaleniu: 195 celowanych testów HEOS i wspólnych poleceń zaliczonych; kolejne 110 testów profili, pakowania, instalatora i wideo oraz 49 podtestów zaliczonych na przypiętym wxPython 4.3.1. Test pakowania wykazał brak klienta HEOS w Public mimo bezwarunkowego importu GUI; poprawiono listę modułów i sprawdzono ponownie import paczki Public. Nie wykonano jeszcze budowy wersji następnej ani instalacji.

## Przygotowanie alpha.519 na GitHubie — 23.11.2026

Podniesiono wersję źródeł do `0.1.0-alpha.519` i dodano workflow `.github/workflows/amc-heos-next-test-build.yml` dla gałęzi HEOS. Ma budować wyłącznie AMC Test na GitHub Actions, pobrać opublikowany i zweryfikowany SHA-256 instalator Test alpha.518 jako przypięty runtime oraz zależności, sprawdzić SHA-256 narzędzia rozpakowującego i podpis Inno Setup, po czym przesłać instalator i potwierdzenie integralności jako artefakt. Workflow nie instaluje AMC na komputerze Michała i nie publikuje kolejnego wydania. To opis przygotowanego kodu, nie wynik uruchomienia — status kompilacji i artefaktu wymaga osobnej weryfikacji po uruchomieniu GitHub Actions. Kod alpha.519 zawiera także Apple Music odziedziczone po Test alpha.518; nowe sterowanie Apple Music nie było przedmiotem tego etapu.

Korekta interfejsu po pierwszym wypchnięciu gałęzi: po zatwierdzeniu wyboru wykrywania lub ręcznego IP fokus wraca na listę HEOS. Gdy źródło nie udostępnia listy, NVDA słyszy użytkowy komunikat bez wewnętrznej nazwy polecenia CLI. Dwa nowe testy bezokienkowe sprawdzają te przypadki. Zmiana wymaga nowego commitu i ponownej kompilacji GitHub Actions; pierwszy uruchomiony build nie zawiera tej korekty.

## Korekta stanu po równoległych aktualizacjach — 23.11.2026

Powyższe zdanie o braku kompilacji na GitHubie jest już nieaktualne: workflow dla commitu `211f4c5e` zakończył się powodzeniem i utworzył techniczny artefakt `alpha.519`. Ten artefakt powstał z bazy alpha.518, więc nie zawiera późniejszych równoległych aktualizacji. Nie został pobrany, zainstalowany ani opublikowany jako wydanie HEOS; jego numer koliduje z późniejszymi wydaniami alpha.519. Potwierdza wyłącznie możliwość zbudowania ówczesnego kodu na GitHubie.

Do izolowanej gałęzi HEOS scalono później poprawki nagrywania, wznawiania podcastów i YouTube (`73465a5d`) oraz wydane Cast/AirPlay z alpha.519 (`544e1eff`). Skróty Cast pozostają dostępne w obsługiwanych sesjach TIDAL i Radio Archiwisty; sesja HEOS ich nie reklamuje. Public nadal nie udostępnia HEOS ani Apple Music. Apple Music w Test pochodzi z wcześniejszej bazy i nie otrzymało tu osobnej walidacji logowania ani odtwarzania.

Po obu scaleniach: 48 celowanych testów HEOS, 220 testów Cast/AirPlay, skrótów, profili i klienta hosta (1 pominięty), 144 testy pakowania, sesji i parytetu poleceń oraz kompilacja LiteHost Release (0 błędów, 0 ostrzeżeń) zakończyły się powodzeniem. Test lokalnego HTTP Cast wymagał dostępu do gniazda; odmowa Windows 10013 w piaskownicy nie była awarią kodu. Dodatkowe 174 testy HEOS i poprawek nagrywania/podcastów oraz 5 testów YouTube z 16 podtestami przeszły po pierwszym scaleniu. Fizyczne sterowanie Denon Home 200, dźwięk HEOS i nawigacja NVDA nie były sprawdzane.

Bieżąca aktualizacja AMC przygotowuje alpha.520 bez niedokończonego Cast. Dopiero po jej zakończeniu należy ustalić kolejny numer wersji, zweryfikować przypięty runtime, zbudować aktualny stan HEOS na GitHubie i sprawdzić wynikowy instalator. Nie należy instalować technicznego artefaktu alpha.519 HEOS.

## Wynik kompilacji następnego Test alpha.521 — 23.11.2026

Ta sekcja zastępuje powyższy plan czekania na alpha.520. Oba profile alpha.520 zostały opublikowane z commitu `15f76ae4`; źródła tego wydania zostały scalone do gałęzi HEOS. Lokalna instalacja Test alpha.520 nie została dokończona w równoległym zadaniu, więc dalsze testy użytkowe HEOS nadal czekają na właściwy moment.

Gałąź HEOS łączy poprawki wideo, nagrywania i podcastów, wydane Cast/AirPlay oraz istniejące Apple Music w Test. Wersja źródeł to `0.1.0-alpha.521`. Workflow użył zweryfikowanego instalatora Test alpha.519 Cast jako źródła runtime i zależności, sprawdził jego SHA-256, zbudował paczkę i instalator wyłącznie dla profilu Test oraz potwierdził zgodność etykiety czasu paczki ze strefą Polski. Pierwszy techniczny build alpha.521 (`552e9f81`) miał etykietę czasu UTC `22:00`; nie nadaje się do odbioru. Workflow poprawiono bez zmiany kodu aplikacji, a build z commitu `64026c64888bc0945f0cc96eec8d4a3bc5ac87aa` zakończył się powodzeniem: GitHub Actions run `45276304618`, artefakt `14866977568`.

Pobrany instalator: `D:\Projekty Codex\AMC - integracja wxPython\publish\installers-alpha521-heos-github-64026c64\AMC-Test-2026-11-23-2307.exe`. Rozmiar **214627826 bajtów**, SHA-256 **`10eed5b01dbe7376ca5b1fa3f82b5f44d3683eaad071152e0d9bb98db4fc5827`**. Lokalny plik zgadza się z pobranym manifestem integralności: profil `test`, wersja `0.1.0-alpha.521`, etykieta `23.11.2026, 23:07`, ten sam commit źródeł i instalatora, `secretScan=passed`. To potwierdza kompilację i integralność pliku, nie instalację, działanie GUI, głośnika ani NVDA. Artefakt GitHub ma ograniczony czas przechowywania; pobrany instalator pozostaje w folderze projektu.

Do kompilacji prowadziły celowane wyniki: 48 testów HEOS, 220 testów Cast/AirPlay, skrótów, profili i klienta (1 pominięty), 144 testy pakowania i parytetu sesji, 174 testy HEOS z poprawkami nagrywania/podcastów, 5 testów YouTube i 16 podtestów; LiteHost Release bez błędów i ostrzeżeń oraz test handlerów LiteHost zakończony wynikiem OK. Zestawy uruchamiano osobno, ponieważ atrapa `wx` z testów HEOS koliduje z importem `wx.html2` w jednym procesie pytest. Test lokalnego HTTP Cast wymagał dostępu do gniazda; odmowę piaskownicy potwierdzono poprawnym wynikiem po nadaniu dostępu. Nie wykonywano sterowania Denon Home 200, odsłuchu ani NVDA. Żadnego wydania HEOS nie opublikowano w kanale aktualizacji, a instalatora nie uruchomiono.

## Korekta po instalacji alpha.522 — 24.11.2026

Michał zgłosił, że w menedżerze sesji `Ctrl+Shift+S` nie widzi HEOS. Odczytowy pomiar zainstalowanego `D:\Projekty Codex\AMC Python\bundle-manifest.json` potwierdził profil Test, etykietę `24.11.2026, 01:00` i commit `f549090be998a718959ba7c587a33c05845cfee2`, czyli alpha.522. Manifest nie ma możliwości `session.heos`, a paczka nie zawiera `heos_client.py`. To wyjaśnia brak sesji; nie jest to awaria listy ani uśpienie głośnika. Poprzedni instalator HEOS alpha.521 stał się starszy od zainstalowanej wersji i nie należy go używać do aktualizacji. Ta sekcja zastępuje wcześniejsze określenie alpha.521 jako następnej wersji Test.

Scalono opublikowane i lokalnie zainstalowane źródła alpha.522 z gałęzią HEOS. Rozwiązano konflikt profilów tak, by wspólne funkcje nagrywania pozostały także w Public, historyczne pole `recording.monitor` pozostało tylko w manifeście Test, a HEOS tylko w Test. Numer źródeł zmieniono na `0.1.0-alpha.523`. Testy po scaleniu: **48 HEOS**, **218 profili, skrótów, palety, sesji i pakowania**, **37 wyboru Cast/AirPlay**, **101 Cast, nagrywania i podcastów (1 pominięty)** oraz **5 YouTube z 16 podtestami**. Zestawy GUI i HEOS uruchamiano osobno z powodu atrap `wx`. Kompilacja testowego hosta C# zakończyła się bez błędów i ostrzeżeń; test handlerów zwrócił OK. Nie uruchamiano zainstalowanego AMC ani głośnika.

GitHub Actions dla commitu `822b1fd78b37bbce52bb68fa87ff76d025818f50` zakończył się powodzeniem, run `45384808699`, artefakt `14916740328`. Workflow pobrał zweryfikowany instalator Test alpha.522 jako źródło runtime i zależności, zachował oba dodatki NVDA, wykonał budowę i kontrolę paczki oraz kompilację instalatora. [Wynik kompilacji](https://github.com/michalkasperczak/AMC/actions/runs/45384808699).
https://github.com/michalkasperczak/AMC/actions/runs/45384808699

Pobrany instalator: `D:\Projekty Codex\AMC - integracja wxPython\publish\installers-alpha523-heos-github-822b1fd7\AMC-Test-2026-11-24-1232.exe`. Manifest integralności potwierdza profil `test`, wersję `0.1.0-alpha.523`, etykietę `24.11.2026, 12:32`, ten sam commit źródeł i instalatora, `secretScan=passed` oraz dodatek AMC do NVDA 0.4.2. Plik ma **214635965 bajtów**; obliczony lokalnie SHA-256 **`f521afe1f511fe6b7340a14439b3683588d852889ffe672188be71a7cea8340e`** zgadza się z manifestem. Żadnej wersji HEOS nie opublikowano w kanale aktualizacji ani nie zainstalowano. Test fizycznego sterowania Denon Home 200 i odbiór NVDA pozostają otwarte.

## Korekta definicji wydania i publikacja Test — 24.11.2026

Michał doprecyzował, że interesuje go **wydanie do pobrania z GitHub**, z kompilacją po stronie GitHub, a nie lokalna kompilacja czy sam artefakt GitHub Actions. Ta decyzja zastępuje wcześniejsze traktowanie gotowego artefaktu alpha.523 jako końca zadania. Wydanie Test wymaga instalatora, opisu, manifestu aktualizacji oraz zdalnego sprawdzenia zasobów. Public z HEOS nie jest przedmiotem tego wydania.

Przygotowano opis i krótkie podsumowanie w folderze instalatora, uruchomiono odczytową walidację `scripts/Publish-AMCRelease.ps1 -Profile test -Version 0.1.0-alpha.523`, a następnie publikację tego samego, wcześniej zbudowanego na GitHubie instalatora. Wydanie [AMC Test — 24.11.2026, 12:32](https://github.com/michalkasperczak/AMC/releases/tag/test-2026.11.24.1232) jest prerelease kanału Test.
https://github.com/michalkasperczak/AMC/releases/tag/test-2026.11.24.1232

Zdalny tag wskazuje dokładnie `822b1fd78b37bbce52bb68fa87ff76d025818f50`. GitHub potwierdził trzy zasoby w stanie `uploaded`: instalator **214635965 bajtów**, SHA-256 `f521afe1f511fe6b7340a14439b3683588d852889ffe672188be71a7cea8340e`; plik integralności **973 bajty**, SHA-256 `a84ef6f3359fdc9e13f7c8242737a39af6b59ac202d768d1aa66cbc8ac80ebea`; manifest `amc-update-test.json` **950 bajtów**, SHA-256 `95ccd399b2c4bd16517bc0692e6e84743ab7b0d1b8ef0e74845ccd92fd9706a4`. Zdalny digest instalatora zgadza się z lokalnym plikiem.

Odczytowy `check_for_application_update` z runtime zainstalowanej alpha.522 i jej katalogu `D:\Projekty Codex\AMC Python` zwrócił `available=true`, bieżącą wersję `24.11.2026, 01:00`, nowy tag `test-2026.11.24.1232` i wersję `24.11.2026, 12:32`. To potwierdza widoczność w aktualizatorze bez pobierania przez niego instalatora i bez lokalnej instalacji. Działanie sesji HEOS na głośniku oraz odbiór NVDA nadal wymagają próby użytkowej.

## Korekta Menedżera sesji — 24.11.2026

Michał zgłosił, że w zainstalowanej alpha.523 HEOS jest wyłączona, a Spacja i Enter w Menedżerze sesji nie pozwalają jej uruchomić. Odczyt zapisanej kolejności pokazał dziesięć aktywnych sesji, od Plików lokalnych do Apple Music. Stary limit odrzucał dopisanie HEOS jako jedenastej; Enter prawidłowo odmawiał przejścia do sesji wyłączonej. Ta diagnoza zastępuje wcześniejsze założenie, że do HEOS wystarczy wolne miejsce w limicie.

### Uzasadnienie i decyzje

Usunięto limit aktywnych sesji we wspólnym modelu kolejności, zapisu i menu. Limit dziesięciu pozostał wyłącznie dla skrótów `Ctrl+1–9` i `Ctrl+0`; jedenastą i dalsze sesje można włączyć Spacją, wybrać Enterem albo osiągnąć przez `Ctrl+Page Up`/`Ctrl+Page Down`. Migracja historycznego profilu zachowuje dziesięć dawnych sesji i nie włącza HEOS samoczynnie. Dla sesji bez skrótu etykieta i komunikat mówią „bez skrótu numerycznego”, bez wartości technicznej `None`. Zmiana obejmuje wszystkie sesje, nie specjalny wyjątek dla HEOS.

### Testy i wynik

Na przypiętym Pythonie z wxPython 4.3.1: **214 testów** Menedżera, kolejności, stanu, skrótów, palety i profilu przeszło; po dodaniu testu pełnego przejścia Spacja/Enter **24 testy** Menedżera i kolejności przeszły ponownie. **48 testów** HEOS przeszło w osobnym procesie. **95 testów** pakowania i publikacji przeszło. Testy sprawdzają jedenastą sesję po zapisie i ponownym odczycie, trasę Spacja/Enter, stabilność `Ctrl+0`, nawigację sąsiednią, etykiety NVDA oraz zgodność skrótów w menu, palecie i pomocy. Próba zestawu pakowania o dwóch błędnych nazwach plików zakończyła się `no tests ran`; nie jest wynikiem regresji. Kompilacja GitHub pozostaje do wykonania. Nie uruchamiano lokalnej aplikacji ani głośnika, ponieważ Michał sprawdza równolegle inne funkcje.

### Materiał do dokumentacji użytkownika — szkic

W AMC Test otwórz Menedżer sesji przez `Ctrl+Shift+S`. Strzałkami zaznacz HEOS i naciśnij Spację. Po komunikacie „Włączono sesję HEOS, bez skrótu numerycznego” naciśnij Enter, aby się przełączyć. Gdy dziesięć sesji jest już włączonych, `Ctrl+0` nadal prowadzi do dziesiątej, a do HEOS można także przejść przez `Ctrl+Page Up` lub `Ctrl+Page Down`. Kolejność zmienisz przez `Alt+strzałka w górę/w dół`.

### Szkic changelogu — do korekty Michała

Menedżer sesji pozwala włączać wszystkie dostępne sesje, także HEOS jako jedenastą. Skróty numeryczne pozostają przypisane do pierwszych dziesięciu sesji; pozostałe są dostępne z Menedżera i przez przełączanie sąsiednich sesji. Poprawiono komunikaty NVDA dla sesji bez skrótu.

### Znane ograniczenia oraz wersja i commit

Kod przygotowywany jako AMC Test `0.1.0-alpha.524` na gałęzi `codex/heos-next-test-20261123`. Commit, wynik GitHub Actions, instalator, wydanie GitHub oraz widoczność w aktualizatorze wymagają jeszcze osobnego potwierdzenia. Fizyczna próba NVDA i sterowania Denon Home 200 pozostaje do wykonania przez Michała po aktualizacji.
