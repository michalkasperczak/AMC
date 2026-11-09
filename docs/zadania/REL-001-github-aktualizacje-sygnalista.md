# REL-001 — GitHub Releases, aktualizacje i Sygnalista

- Status: `W trakcie — osobny Worker AMC Test wdrożony bez sekretu; aktywacja czeka na ograniczony token GitHuba i odbiór`
- Utworzono: 18 października 2026
- Obszar: wydania, aktualizator i dostępne zgłoszenia
- Polecenie uruchamiające: `Uruchom zadanie REL-001`
- Roboczy opis pierwszego wydania:
  [REL-001-opis-pierwszego-wydania-publicznego-roboczy.md](REL-001-opis-pierwszego-wydania-publicznego-roboczy.md).

## Cel

Po przygotowaniu pierwszego publicznego instalatora połączyć jeden kontrolowany
proces: oznaczony kod w GitHubie, wydanie GitHub Releases, manifest aktualizacji
oraz dostępne zgłaszanie błędów bez wymagania konta GitHub od użytkownika.

## Zatwierdzony kierunek

- GitHub jest źródłem kodu, tagów, opisów zmian i gotowych wydań.
- `AMC-Python-Updater.exe` pozostaje wykonawcą transakcyjnej podmiany. Moduł
  internetowy jedynie sprawdza podpisany lub jednoznacznie zweryfikowany
  manifest, pobiera właściwy pakiet i przekazuje go aktualizatorowi.
- Kanał testowy i publiczny mają osobne manifesty. Aktualizacja nigdy nie
  zamyka AMC podczas nagrywania i nie instaluje niesprawdzonego pliku.
- Formularz `Zgłoś błąd lub sugestię` korzysta z idei Sygnalisty Michała
  Dziwisza: aplikacja wysyła zatwierdzony raport do Cloudflare Workera, a
  Worker tworzy issue w repozytorium AMC. Użytkownik nie potrzebuje konta
  GitHub.

## Wynik porównania zewnętrznego projektu

Sprawdzono projekt `michaldziwisz/sygnalista` z 18 października 2026:

- licencja MIT pozwala użyć i zmodyfikować kod z zachowaniem informacji
  licencyjnej;
- endpoint `POST /v1/report` przyjmuje rodzaj, tytuł, opis, wersję, kanał,
  diagnostykę oraz opcjonalny skompresowany log;
- pełny raport i log mogą trafić do prywatnego repozytorium `support-intake`,
  a publiczne issue dostaje etykietę `bug` lub `enhancement` oraz `from-app`;
- mapowanie aplikacji, token aplikacji, limity rozmiaru i tempo żądań są
  konfigurowane w Workerze;
- rozwiązanie jest wzorcem i możliwym składnikiem, ale AMC potrzebuje własnej
  polityki prywatności, filtrowania danych i testów nadużyć.

## Zakres formularza AMC

- Typ: błąd albo sugestia.
- Krótki tytuł, opis i opcjonalne kroki odtworzenia.
- Automatycznie: wersja AMC, commit, kanał wydania, wersja Windows, rodzaj
  aktywnej sesji i wersje składników — bez nazw prywatnych plików i bez danych
  kont.
- Log jest domyślnie wyłączony. Przed wysłaniem użytkownik może zobaczyć
  dokładny raport, usunąć log i zatwierdzić operację.
- Po powodzeniu aplikacja podaje numer zgłoszenia i umożliwia skopiowanie
  adresu. Nie otwiera wbudowanej przeglądarki.

## Przejście zgłoszeń do pracy

Sygnalista już tworzy zwykłe GitHub Issues, więc dalszy proces może być
automatyzowany bez przepisywania danych:

1. etykiety `bug`, `enhancement` i `from-app` rozdzielają kolejkę;
2. dodatkowe etykiety AMC określają sesję i wydanie testowe lub publiczne;
3. po weryfikacji zgłoszenia tworzymy albo wskazujemy zadanie w
   `docs/zadania`, zachowując numer issue;
4. Codex lub Hermes może później analizować wybrane issue wyłącznie na jawne
   polecenie użytkownika; samo nadejście raportu nie uruchamia zmian kodu.

## Kolejność wdrożenia

1. Repozytorium i model wersji.
2. Powtarzalne wydanie GitHub Releases z manifestem i sumą SHA-256.
3. Internetowe sprawdzanie aktualizacji oraz bezpieczne przekazanie paczki do
   istniejącego aktualizatora.
4. Własna instancja Workera Sygnalisty i prywatne repo raportów.
5. Dostępny formularz AMC, filtrowanie danych, testy NVDA, brak sieci,
   ponowienie, limit i odrzucenie zbyt dużego logu.

## Podział umożliwiający wcześniejszą pracę

Po przygotowaniu lokalnego instalatora wolno wdrożyć bez połączenia z usługą:

- dostępny formularz błędu lub sugestii;
- jawny podgląd całego raportu i domyślnie wyłączony log;
- filtrowanie ścieżek, nazw prywatnych plików, adresów, tokenów i danych kont;
- wspólny kontrakt transportu z atrapą sukcesu, błędu, braku sieci, limitu i
  zbyt dużego raportu;
- możliwość skopiowania albo zapisania zatwierdzonego raportu lokalnie, gdy
  prawdziwy serwer nie jest jeszcze skonfigurowany;
- testy NVDA fokusa, ponownego otwarcia, podglądu, Anuluj i Wyślij.

Nie wolno przed etapem GitHub uznawać wysyłania za działające, osadzać tokenu,
stawiać publicznego Workera ani tworzyć prawdziwych Issues. Wdrożenie Workera,
prywatnego repozytorium raportów, mapowania etykiet i końcowego endpointu
pozostaje częścią tego samego REL-001, wykonywaną razem z zatwierdzonym
repozytorium, kanałami wydań i polityką prywatności.

## Korekty użytkownika

- 19 października 2026: użytkownik polecił kontynuować lokalny fundament
  Sygnalisty, a integrację GitHub wykonać później. To doprecyzowuje wcześniejszą
  kolejność: bieżący etap nie wdraża Workera, tokenu, GitHub Issues ani
  publikacji, lecz przygotowuje bezpieczny formularz, redakcję i kontrakt
  transportu.
- 20 października 2026: użytkownik polecił dokończyć internetową część
  Sygnalisty przed pierwszym publicznym wydaniem. To zastępuje wcześniejsze
  odłożenie kodu Workera, ale nie jest zgodą na publikację AMC bez odbioru ani
  na pozorowane wdrożenie bez konta Cloudflare, prywatnego repozytorium i
  końcowego HTTPS. Kod klienta i Workera wolno przygotować oraz testować na
  atrapach; prawdziwy endpoint musi przejść osobny odbiór.
- 29 października 2026: użytkownik polecił wdrażać Sygnalistę do AMC, z
  zachowaniem wcześniejszej kolejności: najpierw AMC Test, potem — po odbiorze —
  wersja publiczna. Zastępuje to wcześniejsze wstrzymanie zdalnej infrastruktury,
  ale nie znosi podglądu raportu, rozdziału kanałów ani wymogu osobnego sekretu.

## Materiał do dokumentacji użytkownika

- Polecenie `Zgłoś błąd lub sugestię…` znajduje się w menu `Pomoc` i w
  palecie `Ctrl+Shift+K`. Pełna pomoc F1 pokazuje je jako polecenie bez
  przypisanego skrótu.
- Formularz pozwala wybrać `Błąd` albo `Sugestia`, wpisać tytuł, opis i
  opcjonalne kroki odtworzenia. Początkowy fokus trafia do pola tytułu, a
  wszystkie pola i wybory mają jawne etykiety dla czytnika ekranu.
- Cały raport jest widoczny w polu `Pełny podgląd zredagowanego raportu`.
  Dokładnie ten tekst jest kopiowany do schowka, zapisywany lokalnie albo —
  po skonfigurowaniu serwera — wysyłany.
- Log jest domyślnie wyłączony. Po świadomym włączeniu dołączany jest tylko
  ograniczony ogon dziennika po redakcji danych prywatnych.
- Raport może zostać skopiowany albo zapisany atomowo w osobnym folderze
  zgłoszeń bieżącego profilu. AMC Test i publiczny AMC nie współdzielą żywego
  folderu raportów.
- Gdy wydanie nie ma zweryfikowanego endpointu, formularz uczciwie pozostaje
  w trybie lokalnym i nie pokazuje martwego przycisku. Po wpisaniu końcowego
  endpointu HTTPS pojawia się `Wyślij zgłoszenie`; w trakcie pracy przycisk
  jest wyłączony, NVDA słyszy `Wysyłanie zgłoszenia`, a po sukcesie numer issue
  i przycisk `Skopiuj adres zgłoszenia`. AMC nie otwiera przeglądarki.

## Uzasadnienie i decyzje

- Jedna funkcja przygotowuje zredagowaną treść używaną przez podgląd,
  schowek, zapis i przyszły transport. Dzięki temu późniejsza wysyłka nie może
  dostać innego raportu niż tekst zatwierdzony przez użytkownika.
- Redakcja usuwa między innymi ścieżki lokalne i sieciowe, nazwy plików
  multimedialnych i diagnostycznych, adresy WWW i e-mail, adresy IP i MAC,
  identyfikatory, nagłówki autoryzacji, tokeny, hasła oraz nazwy kont.
- Odczyt logu jest ograniczony do ostatnich 512 KiB, najwyżej 200 wierszy i
  64 KiB zredagowanej treści. Cały raport ma bramkę 60 KiB przed wysyłką, aby
  wraz z bezpiecznym zapasem mieścił się w pojedynczym prywatnym GitHub Issue.
- Kontrakt transportu rozróżnia sukces, brak konfiguracji, brak sieci,
  ograniczenie tempa, zbyt duży raport i pozostały błąd. Bieżący transport
  lokalny zawsze zwraca uczciwy stan `nieskonfigurowane` i nie używa sieci.
- Internetowy klient przyjmuje tylko prosty adres HTTPS bez danych logowania,
  parametrów i fragmentu. Wysyła widoczny raport w wątku roboczym, nie zawiera
  tokenu GitHuba i nie ponawia automatycznie niepewnego żądania.
- Każda wersja treści ma losowy identyfikator raportu. Ponowienie niezmienionej
  treści zachowuje identyfikator, a edycja przydziela nowy. Osobny koordynator
  Durable Object dla każdego identyfikatora serializuje równoległe żądania i
  zapisuje etap przed zewnętrznym wywołaniem. Ten sam raport zwraca istniejące
  issue, a inna treść z tym samym identyfikatorem jest odrzucana.
- Worker ponownie redaguje dane, ogranicza wielkość i tempo żądań, zapisuje
  pełną treść w prywatnym repozytorium intake, a w publicznym AMC tworzy tylko
  krótki wpis. Sekret GitHuba występuje wyłącznie jako sekret Workera.
- Limiter sprawdza osobno adres klienta i wspólny ruch całej usługi. Adres nie
  jest zapisywany w Issue ani w trwałym stanie raportu; służy tylko do
  krótkookresowej ochrony przed nadużyciem.
- Własna domena nie jest wymagana: zatwierdzona konfiguracja używa HTTPS pod
  `workers.dev`, wymaga sekretu `GITHUB_TOKEN`, Durable Object i limitera.
  Dane OAuth Wranglera na Windows są przechowywane w szyfrowanym pliku, którego
  klucz znajduje się w systemowym magazynie poświadczeń.
- Zależności Workera są utrwalone w `package-lock.json`. Skrypt wdrożenia
  domyślnie wykonuje wyłącznie czyste `npm ci`, testy i dwa pakowania
  `--dry-run`. Zdalny przebieg jest rozdzielony na `-Bootstrap`, interaktywny
  `-SetGitHubToken` i właściwy `-Deploy`. Bootstrap używa jednorazowej
  konfiguracji bez nieistniejącego jeszcze sekretu i usuwa ją także po błędzie;
  nie aktywuje endpointu AMC. Skrypt nie wywołuje logowania. Dopiero jawne
  `-Deploy -ActivateEndpoint` po próbach zdrowia, metody i schematu zapisuje
  adres raportów atomowo; żaden etap nie tworzy automatycznie próbnego issue.
- Profil testowy i publiczny mają odrębne Workery, konfiguracje, limitery i
  oczekiwane kanały: `amc-sygnalista-test` przyjmuje wyłącznie `test`, a
  `amc-sygnalista` wyłącznie `stable`. Niezgodny kanał jest odrzucany przed
  wywołaniem GitHuba.
- Pakowacz obu profili rozpoznaje odpowiednio opcjonalny
  `problem-report-endpoint-test.txt` albo
  `problem-report-endpoint-public.txt`. Kopiuje go do paczki pod wspólną nazwą
  dopiero po walidacji prostego adresu
  HTTPS zakończonego `/v1/report`; dane logowania, parametry, fragment,
  wielowierszowa lub zbyt długa wartość zatrzymują budowę. Brak konfiguracji
  usuwa stary zasób z odświeżanej paczki, więc wydanie nie zachowa przypadkiem
  wycofanego serwera.
- Publikacja instalatora ma osobną bramkę PowerShell. Domyślny przebieg tylko
  sprawdza profil `public`, wersję, nazwę, rozmiar, SHA-256 i pełną rewizję
  źródeł oraz przygotowuje opis i manifest gotowości. Dopiero jawne `-Publish`
  może utworzyć wydanie; wymaga czystych śledzonych źródeł i obecności rewizji
  instalatora na GitHubie, nie zastępuje istniejącego tagu i odczytowo
  potwierdza oba przesłane pliki oraz sumę w opisie.
- Źródła pierwszego wydania mają osobną, czystą linię `public/release-line`.
  Skrypt przygotowawczy tworzy lokalny commit z drzewem identycznym z
  zatwierdzonym stanem roboczym, ale bez setek wcześniejszych commitów
  integracyjnych. Nie przełącza bieżącej gałęzi, nie zmienia jej historii i
  nie wykonuje `push`. Następne snapshoty wskazują poprzedni snapshot publiczny
  jako jedynego rodzica. Dzięki temu wydanie może wskazać dokładne źródła bez
  ujawniania dawnych lokalnych ścieżek występujących w roboczej historii.
- Przed utworzeniem snapshotu skrypt automatycznie skanuje zatwierdzone drzewo
  pod kątem tokenów GitHub, OpenAI i Google, JWT, nagłówków kluczy prywatnych
  oraz rzeczywistych ścieżek `C:\Users\...`. Wykrycie któregokolwiek wzorca
  zatrzymuje etap przed utworzeniem publicznego commitu.
- Polecenie korzysta ze wspólnego modelu akcji dla menu, F1, palety i
  wykonania oraz jest jawnie dostępne we wszystkich sesjach i obu profilach.
- Możliwość lokalnego formularza jest częścią zamkniętego manifestu zarówno
  Python, jak i uruchamiacza .NET. Rozbieżność tych list zatrzymuje testy przed
  instalacją zamiast tworzyć paczkę, której uruchamiacz później odmówi.
- Korekta użytkownika z 21 października 2026: internetowy Sygnalista ma być
  domknięty przed rozpoczęciem widocznego logowania YouTube przez Google.
  Dlatego nie kontynuowano YT-001, a w REL-001 usunięto ręczne przepisywanie
  zwykłego adresu `workers.dev`.
- Korekta techniczna z 21 października 2026 zastępuje wcześniejszą decyzję o
  Cloudflare KV. KV nie zapewniało bezpiecznej serializacji równoległych
  żądań. Bieżący kontrakt używa wersjonowanej migracji SQLite Durable Object
  `ReportCoordinator`, osobnej dla każdego `reportId`; nie wymaga ręcznego
  identyfikatora zasobu. Lokalny `wrangler.jsonc` pozostaje ignorowany przez
  Git, a token nie jest w nim zapisywany.
- Bramka lokalna nie wymaga adresu serwera: wykonuje czyste odtworzenie
  zależności, testy i dwa rzeczywiste pakowania `--dry-run` w jednorazowym
  katalogu: zwykłe oraz pierwszego bootstrapu bez sekretu.
  Przy wdrożeniu skrypt odczytuje standardowy adres `workers.dev` z
  potwierdzenia Wranglera, sprawdza go, weryfikuje `/health` i dopiero wtedy
  może atomowo włączyć endpoint w źródłach AMC.
- Nie użyto istniejącego logowania GitHub jako sekretu Workera. Sygnalista ma
  otrzymać osobny token o minimalnym zakresie tylko do Issues w dwóch
  wskazanych repozytoriach; brak takiego sekretu jest twardą bramką.

## Testy i wynik

- `8 passed` — ukierunkowane testy `test_problem_report.py`: redakcja danych,
  log domyślnie wyłączony, ograniczony ogon logu, atomowy zapis dokładnego
  podglądu, wszystkie stany kontraktu transportu, zgodność menu/palety/F1/
  wykonania, oba profile i kontrakt dostępnego dialogu.
- `119 passed, 1 failed` — regresje katalogu poleceń, profili, skrótów i menu.
  Jedyny błąd to zastana kolizja litery dostępu `Z` w menu `Pliki`; nowy wpis
  znajduje się w menu `Pomoc` i tej kolizji nie tworzy ani nie zmienia.
- `py_compile` zmienionych modułów i testu: zaliczony.
- Nie wykonano pełnej kompilacji, instalacji ani uruchomienia AMC, ponieważ
  użytkownik w tym czasie testuje bieżącą instalację.
- Ręczny odbiór NVDA — pierwszy fokus, poruszanie się, Anuluj, ponowne
  otwarcie, schowek i zapis — pozostaje do wykonania po instalacji przyszłej
  paczki.
- Pierwsza zbiorcza paczka zawierająca Sygnalistę została poprawnie odrzucona
  przed instalacją, ponieważ uruchamiacz .NET nie znał jeszcze możliwości
  `problem-report-local`. Wszystkie sumy plików startowych były zgodne.
  Kontrakt .NET uzupełniono, a test profili porównuje odtąd pełne listy
  możliwości obu implementacji. Wtedy wymagane było ponowne zielone pakowanie.
- Powtórzona pełna bramka: **1831 zaliczonych, 110 pominiętych i 11
  podtestów**. Paczka 1665 plików przeszła kontrolę profilu, manifest i
  SHA-256; AMC Test z lokalnym formularzem zostało zainstalowane i uruchomione.
  Ręczny odbiór NVDA formularza nadal pozostaje otwarty.
- Internetowy etap z 20 października: **20 zaliczonych testów Python**
  (`test_problem_report.py` i `test_async_gate.py`) oraz **8 zaliczonych testów
  Node Workera**. Sprawdzono dokładną treść żądania bez sekretu, wyłącznie
  HTTPS, brak sieci, limit, rozmiar, uszkodzoną odpowiedź, drugą redakcję,
  prywatne i publiczne issue na atrapach, idempotencję, konflikt treści,
  częściową awarię i brak wycieku błędu GitHuba.
- Kod Workera nie został wdrożony, nie połączono prawdziwego tokenu i nie
  utworzono próbnego GitHub Issue. To są testy kodu i atrap, nie odbiór sieci.
- Utworzono prywatne repozytorium `michalkasperczak/amc-support-intake`.
  Odczyt zwrotny GitHuba potwierdził `PRIVATE`, włączone Issues i etykiety
  `bug`, `enhancement`, `from-app`. Publiczne repozytorium AMC również ma
  `from-app`. Nie utworzono żadnego issue.
- Zmiana internetowa nie została zbudowana ani zainstalowana, ponieważ bramka
  bezpieczeństwa wykazała aktywne nagrywanie w AMC Test. Nie przerwano go.
- Testy skryptu pierwszego publicznego wydania: **3 zaliczone** — poprawne
  przygotowanie bez publikacji oraz odmowa profilu testowego i zmienionej sumy.
  Istniejący instalator `alpha.424` przeszedł dodatkowo rzeczywistą kontrolę
  rozmiaru i SHA-256; powstał lokalny opis oraz manifest gotowości z polem
  `published: false`. Niczego nie wysłano na GitHub.
- Audyt śledzonego drzewa przed przyszłym publicznym push: nie znaleziono
  wzorców tokenów GitHub/OpenAI/Google/JWT ani nagłówków kluczy prywatnych.
  Siedem wpisów dokumentacji zawierało rzeczywistą nazwę profilu Windows;
  zastąpiono ją `%LOCALAPPDATA%` albo `C:\Users\<użytkownik>`, a ponowny skan
  nie znalazł tej nazwy. Sześć małych logów w `review/sonos-login-client-fix1`
  zachowano jako śledzone dowody czerwonej i zielonej próby; skan nie znalazł w
  nich typowych wzorców poświadczeń.
- Audyt całej roboczej historii: zero commitów z wzorcami tokenów GitHub,
  OpenAI i Google, JWT albo nagłówkami kluczy prywatnych. Dawna nazwa lokalnego
  profilu Windows występuje w 10 commitach i pięciu plikach historycznych,
  dlatego pierwsze wydanie nie będzie publikowało całej tej historii.
- Audyt pierwszej lokalnej linii snapshotów wykrył jeden komentarz kodu z
  rzeczywistą ścieżką profilu Windows. Zastąpiono ją neutralnym przykładem,
  dodano stałą bramkę i zdecydowano odtworzyć nieopublikowaną linię publiczną
  od zera, zamiast zachowywać wcześniejsze cztery lokalne snapshoty.
- Rozszerzony audyt wykazał także historyczne ścieżki profili Windows i Unix
  w dokumentacji, dowodach testowych, fiksturach i starych skryptach. W 51
  śledzonych plikach zastąpiono rzeczywiste nazwy neutralnym profilem
  testowym. Aktywne skrypty nie dostały fikcyjnej ścieżki: wyliczają teraz
  katalog narzędzi z `AMC_BUILD_TOOLS_ROOT` albo profilu systemowego, staging
  z systemowego katalogu tymczasowego, a polecenie `dotnet` z `PATH`.
- Testy czystej publicznej linii źródeł wraz z istniejącą bramką wydania:
  **6 zaliczonych**. Potwierdzono snapshot bez rodzica, identyczne drzewo,
  idempotencję, liniowego rodzica następnego wydania, brak zmiany bieżącego
  checkoutu, odmowę przy niezapisanych zmianach i brak polecenia wysyłania.
- Powtarzalna bramka wdrożenia Sygnalisty: czyste `npm ci` z pliku blokady i
  **8/8 testów Workera**, następnie **23/23 testy Python** transportu,
  współbieżności i kontraktu skryptu wdrożenia. Parser PowerShell przyjął
  skrypt bez błędów. Nie wykonano `wrangler deploy`, logowania ani prawdziwego
  zgłoszenia.
- Pakowanie endpointu i jego kontrakt z klientem: **42 zaliczone testy**
  układu paczki, transportu i bramki wdrożenia. Sprawdzono oba profile,
  odrzucenie adresu z danymi logowania i parametrem oraz usunięcie starego
  zasobu przy braku konfiguracji.
- Bramka prywatności snapshotu oraz regresje GUI i klawiatury: **59
  zaliczonych testów**. Syntetyczne źródło z `C:\Users\jan\...` zostało
  odrzucone przed utworzeniem snapshotu.
- Po anonimizacji ścieżek: **298 zaliczonych, 109 pominiętych i 6 podtestów
  zaliczonych** dla baz, bibliotek, historii, profili, transportu i pakowania.
  Pięć skryptów PowerShell oraz trzy skrypty Bash przeszły kontrolę składni.
  Pominięcia są platformowe i zastane; zestaw zakończył się kodem 0.
- Etap z 21 października: **45 zaliczonych testów Python** dla formularza,
  transportu, pakowania endpointu i nowej bramki wdrożenia; parser PowerShell
  nie zgłosił błędu. Osobna rzeczywista bramka wykonała `npm ci`, **8/8 testów
  Workera** oraz pakowanie Wranglera `--dry-run` z ówczesnym kompletem KV,
  limitera i zmiennych, bez wysyłania kodu. Ten wariant magazynu został
  zastąpiony niżej opisanym Durable Object.
- Końcowa bramka magazynu Durable Object: **11/11 testów Node** — w tym dwa
  równoległe żądania jednego raportu oraz błędy odczytu i zapisu magazynu — i
  **27/27 testów Python** klienta, współbieżności oraz kontraktu skryptu.
  Parser PowerShell nie zgłosił błędu. Rzeczywisty skrypt wykonał czyste
  `npm ci` i dwa pakowania Wranglera `--dry-run`; oba zawierały
  `REPORT_COORDINATORS`, limiter i zmienne, niczego nie wdrażając.
- Odczytowe `wrangler whoami` potwierdziło aktywną sesję Cloudflare dla
  właściwego konta. Osobny odczyt po nazwie potwierdził, że Worker
  `amc-sygnalista` jeszcze nie istniał przed bootstrapem.
- Jawny bootstrap utworzył i w 100% wdrożył nieaktywną wersję Workera
  `087e988f-3096-4e8b-b1df-ddff1f63bc2d` wraz z bindingami Durable Object i
  limitera. Odczyt zwrotny potwierdził pustą listę sekretów. Próba HTTPS nie
  przeszła, ponieważ konto Cloudflare nie ma jeszcze zarejestrowanej subdomeny
  `workers.dev`; TLS zwraca błąd uzgadniania. Endpoint AMC pozostał wyłączony,
  tokenu nie ustawiono i nie utworzono próbnego issue.
- Etap z 29 października: **20/20 ukierunkowanych testów Python** klienta,
  pakowania profili i bramki wdrożenia oraz **11/11 testów Node** Workera
  przeszło. Parser PowerShell nie zgłosił błędu; oba pakowania `--dry-run`
  zawierały Durable Object, limiter i `EXPECTED_CHANNEL=test`.
- Utworzono odrębny Worker `amc-sygnalista-test`, wersja
  `bf7e923d-0c85-42a4-846c-a4910fba9604`, pod adresem
  `https://amc-sygnalista-test.amc-sygnalista-worker.workers.dev`. Odczyt
  potwierdził bezpieczne `503` z `/health` i `405` dla `GET /v1/report`.
  Początkowa bramka zdążyła odczytać stan przejściowy po deployu, dlatego
  dodano ograniczone ponowienia wyłącznie odczytowych prób gotowości.
- W obu repozytoriach utworzono lub ujednolicono etykiety `channel:test` i
  `channel:stable`; istniejące `bug`, `enhancement` i `from-app` zachowano.
  Sekretu nie ustawiono, endpointu AMC Test
  nie aktywowano i nie utworzono żadnego próbnego issue.

## Znane ograniczenia

- Jest gotowy kod internetowego transportu, Workera i powtarzalnej bramki, ale
  zdalny Worker AMC Test jest na razie tylko nieaktywnym bootstrapem. Trasa
  HTTPS działa, lecz nie ma jeszcze sekretu GitHuba ani aktywnego endpointu AMC.
  Prywatne repozytorium raportów i etykiety już istnieją. Zbudowane obecnie
  wydanie nadal pozostaje w trybie lokalnym i nie tworzy GitHub Issues.
- Prawdziwe uruchomienie wymaga świadomego logowania do właściwego konta
  Cloudflare, ustawienia ograniczonego tokenu GitHuba w sekrecie Workera i
  kontrolowanego raportu próbnego. Sesja Cloudflare jest już potwierdzona;
  osobny token GitHuba o minimalnym zakresie nie został jeszcze ustawiony.
- Skrypt nie może sam utworzyć ograniczonego tokenu GitHuba. Potrafi natomiast
  bezpiecznie przygotować ignorowany plik konfiguracji profilu, wykonać migrację Durable
  Object i odczytać adres `workers.dev`. Brak autoryzacji lub nazwy sekretu
  zatrzymuje właściwe wdrożenie przed aktywacją endpointu.
- Subdomena konta `amc-sygnalista-worker.workers.dev` jest już aktywna, a
  bezpieczne `503` z `/health` zostało potwierdzone. Następny zdalny etap to
  interaktywny `-ReleaseProfile test -SetGitHubToken`; token nie może trafić do
  czatu, argumentu polecenia, pliku ani dziennika.
- Przy niejednoznacznym błędzie transportu po wysłaniu żądania do GitHuba
  koordynator celowo zatrzymuje automatyczne ponowienie, ponieważ issue mogło
  już powstać. W takim przypadku opiekun musi sprawdzić oba repozytoria i
  rozstrzygnąć raport ręcznie; ważniejsze jest niedublowanie wpisów.
- Publiczny snapshot źródeł pozostaje lokalny do czasu odbioru zainstalowanego
  kandydata. Samo jego utworzenie nie jest kopią zdalną ani publikacją.
- Zapisany raport pozostaje plikiem lokalnym; AMC nie otwiera przeglądarki.
- Filtry prywatności mają charakter ostrożny i mogą usunąć z logu więcej
  tekstu niż bezwzględnie konieczne.
- Bieżąca zmiana jest zainstalowana w AMC Test, lecz nie ma jeszcze odbioru
  formularza z NVDA ani internetowego transportu.

## Porządek dysku

- Po testach usunięto osiem odtwarzalnych plików `.pyc` utworzonych w
  `D:\Projekty Codex\AMC - integracja wxPython\wxlite\amc_wx_lite\__pycache__`
  oraz `D:\Projekty Codex\AMC - integracja wxPython\wxlite\tests\__pycache__`.
  Odzyskano 1 600 458 bajtów, około 1,53 MiB. Pliki nie wymagają odzyskania,
  ponieważ Python utworzy je ponownie przy następnym uruchomieniu.
- Zachowano jedyną zweryfikowaną paczkę publiczną, instalator, całe zastane
  `.test-output` i `.test-tmp`, źródła, dokumentację, ustawienia oraz dane
  użytkownika.
- Po zaliczeniu bramki usunięto utworzony w tym etapie odtwarzalny katalog
  `D:\Projekty Codex\AMC - integracja wxPython\deploy\amc-sygnalista-worker\node_modules`
  o rozmiarze 180 906 448 bajtów, około 172,53 MiB. Nie jest odzyskiwany z
  Kosza; dokładną zawartość odtworzy przypięte `npm ci`. Zachowano zastane
  `.test-output`, `.test-tmp` i wszystkie źródła.
- Po końcowej bramce ponownie usunięto odtworzony przez `npm ci` katalog
  `D:\Projekty Codex\AMC - integracja wxPython\deploy\amc-sygnalista-worker\node_modules`
  (180 906 448 bajtów) oraz trzy własne podkatalogi `sygnalista-*` utworzone
  przez ten przebieg w `D:\Projekty Codex\AMC - integracja wxPython\.test-tmp`
  (łącznie 22 911 bajtów). Odzyskano około 172,55 MiB. Dane są odtwarzalne;
  nie ruszono żadnych zastanych katalogów ani niepewnych wyników testów.
- Po bootstrapie usunięto kolejną odtwarzalną kopię `node_modules` z tego
  samego katalogu Workera (180 906 567 bajtów, około 172,53 MiB). Zachowano
  ignorowany `wrangler.jsonc`, ponieważ nie zawiera sekretu i jest potrzebny
  do następnych jawnych etapów. Usunięte zależności odtworzy `npm ci`.
- 29 października po testach i bootstrapie profilu testowego usunięto dokładnie
  `deploy\amc-sygnalista-worker\node_modules` (180 906 567 bajtów, około
  172,53 MiB). Katalog jest odtwarzalny przez przypięte `npm ci`; zachowano
  źródła i ignorowany `wrangler.test.jsonc`. Usunięcie nie korzystało z Kosza.

## Wersja i commit

- Wersja źródeł: `0.1.0`, profile `AMC Test` i `AMC`.
- Commit implementacji: `f2e310fc` (`feat(rel): dodaj lokalny fundament
  sygnalisty`).
- Ujednolicenie możliwości profili Python–.NET: `fb9ba25f` —
  `fix(rel): ujednolić możliwości profili launchera`.
- Internetowy klient i lokalnie testowalny Worker: `d9426684` —
  `feat(rel): przygotuj internetowego Sygnaliste`.
- Bezpieczna bramka publikacji publicznego instalatora: `61837b26` —
  `feat(rel): dodaj bezpieczna publikacje instalatora`.
- Czysta publiczna linia źródeł: `9e4024c4` —
  `feat(rel): przygotuj czysta linie zrodel`; narzędzie
  `scripts/Prepare-AMCPublicSourceSnapshot.ps1` nie publikuje danych.
- Integralna bramka wdrożenia Workera: `65037b21` —
  `feat(rel): domknij bramke wdrozenia Sygnalisty`; pakiet nie zawiera
  konfiguracji konta ani sekretu.
- Warunkowe pakowanie zweryfikowanego endpointu: `f9afe750` —
  `fix(rel): pakuj endpoint internetowego Sygnalisty`.
- Automatyczna odmowa publicznego snapshotu przy prywatnej ścieżce lub
  poświadczeniu: `adcf1d9c` —
  `fix(rel): blokuj prywatne dane w snapshocie`.
- Anonimizacja historycznych ścieżek oraz przenośne wartości domyślne starych
  narzędzi: `98e5549f` — `chore(rel): zanonimizuj sciezki zrodel`.
- Druga redakcja serwerowa ścieżek, przypisań tokenów i kont,
  identyfikatorów oraz nazw plików: `8149d076` —
  `fix(rel): wzmocnij redakcje Workera`.
- Wymagany sekret, `workers.dev`, systemowy magazyn logowania i ochrona plików
  lokalnych: `94d6a0f7` — `fix(rel): utwardz wdrozenie Workera`; 8/8 testów
  Workera zaliczone.
- Samodzielna lokalna bramka i dawny wariant automatycznego przygotowania KV:
  `05741a4e` — `fix(rel): uprosc przygotowanie Workera Sygnalisty`; wariant KV
  został 21 października zastąpiony koordynatorem Durable Object.
- Odczyt standardowego adresu z potwierdzonego wdrożenia: `c1554642` —
  `fix(rel): odczytaj adres wdrozonego Workera`.
- Koordynator Durable Object, bezpieczny bootstrap i trzyczęściowa bramka
  wdrożenia: `84c1d988` — `fix(rel): utwardz wdrozenie Sygnalisty`.
- Rozdzielenie Workera, konfiguracji i endpointu kanału testowego od
  publicznego oraz wymaganie zgodności kanału: `cafb0bb7` —
  `feat(rel): rozdziel wdrozenie Sygnalisty na kanaly`.
- Instalacja: `2026.10.19-fb9ba25fe0680cf7bf5435d53eb5b073de091cca-0debb6aa`;
  manifest 1665 plików, pełna integralność i uruchomienie zweryfikowane.
- Bieżąca uruchomiona instalacja AMC Test ma źródło `b258fb69fef580f7899c30abd0d48e6b877119be`
  i build `2026.10.21-b258fb69fef580f7899c30abd0d48e6b877119be-726f0791`.
  Dzisiejszy etap zmienił wyłącznie narzędzia wdrożeniowe Workera i dokumentację,
  więc nie przebudowywano ani nie restartowano aplikacji bez działającego endpointu.
- Publikacja: nie wykonano.
- Infrastruktura: wykonano wyłącznie nieaktywny bootstrap testowego Cloudflare Workera;
  nie jest to publikacja AMC ani aktywacja internetowego formularza.

## Poza zakresem

- Publikacja obecnego stanu bez osobnej decyzji użytkownika.
- Wysyłanie logów, adresów stacji, historii lub danych kont bez podglądu i
  zgody.
- Automatyczne wdrażanie poprawek na podstawie samego zgłoszenia.
- Instalowanie lub utrzymywanie zewnętrznej aplikacji Michała Dziwisza.

## Wynik wykonania

- Data etapów: 19–21 oraz 29 października 2026.
- Repozytorium i kanały: profile i kanały są rozdzielone przez AMC12. GitHub
  został użyty wyłącznie do utworzenia i zweryfikowania prywatnego repozytorium
  intake oraz etykiet; nie wysłano kodu, instalatora, wydania ani zgłoszenia.
- Kontrakt Workera: `POST /v1/report`, schemat 1; testowy adres HTTPS istnieje,
  ale pozostaje celowo nieaktywny bez sekretu i nie jest jeszcze spakowany w AMC Test.
- Testy aktualizacji: poza zakresem tego etapu.
- Testy prywatności: zaliczone na poziomie kodu; ręczny NVDA po instalacji.
- Wydanie: nie utworzono i nie opublikowano.


## Aktualizacja 8 listopada 2026 — wznowienie po uwierzytelnieniu GitHub

### Uzasadnienie i decyzje

- Użytkownik wznowił zadanie w nowej rozmowie po ukończeniu uwierzytelnienia GitHub i polecił połączyć tę pracę z REL-001. Korekta ta zastępuje pauzę z 29 października spowodowaną problemem 2FA; nadal obowiązuje kolejność AMC Test przed AMC Public.
- Uwierzytelnienie konta w Codex/GitHub nie jest sekretem Cloudflare Workera. Użytkownik potwierdził, że nie ma jeszcze osobnego tokenu fine-grained. Do aktywacji potrzebny jest token ograniczony do repozytoriów `michalkasperczak/AMC` i `michalkasperczak/amc-support-intake` oraz `Issues: read and write`. Token ma być wpisany wyłącznie w interaktywnym monicie Workera, bez przesyłania go przez czat lub plik.

### Testy i wynik

- Odczyt 8 listopada: `https://amc-sygnalista-test.amc-sygnalista-worker.workers.dev/health` zwrócił HTTP 503 i `Serwer zgłoszeń nie jest skonfigurowany`. Nie wykonywano żądania tworzącego issue.
- Testy kodu Workera bez sieci: 11 zaliczonych (`node --test deploy/amc-sygnalista-worker/test/worker.test.mjs`).
- Ukierunkowane testy klienta i kontraktu wdrożenia: 18 zaliczonych (`test_problem_report.py`, `test_sygnalista_deploy_script.py`) w przypiętym środowisku `python-test-envs/wxpython-4.3.1-cp314`. Pierwsza próba z runtime zainstalowanej aplikacji nie uruchomiła testów, ponieważ nie ma w nim pytest; nie jest to błąd kodu.
- Odczyt pakietów AMC Test (`ed9d61ac`) i AMC Public (`ecd2f443`): oba zawierają formularz, żaden nie zawiera `problem-report-endpoint.txt`. Obie aplikacje były uruchomione. Nie zatrzymywano ich ani nie instalowano aktualizacji.
- Nie zmieniono źródeł klienta ani Workera, nie wdrażano nowej wersji Workera, nie ustawiono sekretu, nie aktywowano endpointu i nie utworzono próbnego issue. Bieżący commit źródeł podczas odczytu: `19d08fd60da97736daa933a6cfc0d2ae901827a0`; repozytorium ma niezwiązane, niezapisane zmiany innych zadań, zachowane bez ingerencji.

### Znane ograniczenia

- Wysyłanie internetowe w AMC Test nadal czeka na osobny token i bezpieczne wpisanie go do Cloudflare, kontrolę gotowości Workera, aktywację endpointu, budowę i odbiór instalacji. Formularz lokalny działa w obecnych pakietach.
- Ręczny odbiór NVDA i kontrolowane próbne zgłoszenie pozostają do wykonania po aktywacji. Wyniki testów kodu i odpowiedź `/health` nie potwierdzają jeszcze przepływu GitHub Issues.

### Materiał do dokumentacji użytkownika

- W menu Pomoc wybierz `Zgłoś błąd lub sugestię…` albo użyj palety `Ctrl+Shift+K`. Obecnie można obejrzeć zredagowany raport, skopiować go i zapisać lokalnie. Internetowy przycisk `Wyślij zgłoszenie` pojawi się dopiero w pakiecie z aktywnym, zweryfikowanym endpointem. Log pozostaje domyślnie wyłączony.

### Wersja i commit

- Etap odczytowy: repozytorium `19d08fd60da97736daa933a6cfc0d2ae901827a0`, bez nowego commitu, builda, instalacji i publikacji.

## Aktualizacja 8 listopada 2026 — aktywacja testowego Workera

Ta aktualizacja zastępuje powyższy stan odczytowy z tego samego dnia: token i endpoint testowy zostały już ustawione. Użytkownik utworzył token GitHub fine-grained dla `michalkasperczak/AMC` i `michalkasperczak/amc-support-intake` z uprawnieniem Issues: Read and write; ważność na formularzu GitHub ustawiono do 8 listopada 2027. Sekret przekazano ręcznie w interaktywnym monicie Wranglera do `GITHUB_TOKEN` Workera `amc-sygnalista-test`. Skrypt potwierdził obecność sekretu bez odczytu jego wartości. Wartość tokenu pojawiła się wcześniej w technicznym wyniku narzędzia podczas oglądania strony GitHub; użytkownik został o tym poinformowany i wyraźnie odmówił rotacji. Nie zapisano wartości w źródłach, tym raporcie ani w aplikacji.

- Wdrożono testowego Workera `amc-sygnalista-test`, wersja `a034e335-c924-459e-ba45-9e293be293a1`. `GET /health` zwrócił HTTP 200 i `{"status":"ok","schemaVersion":1}`; `GET /v1/report` zwrócił oczekiwane HTTP 405. Są to kontrole gotowości, nie dowód utworzenia Issue.
- Skrypt wdrożenia atomowo utworzył `wxlite/amc_wx_lite/problem-report-endpoint-test.txt` z adresem `https://amc-sygnalista-test.amc-sygnalista-worker.workers.dev/v1/report`. Jest to publiczny adres HTTPS bez sekretu. Profil publiczny nadal nie ma osobnego endpointu.
- Właściwe wdrożenie wykonało `npm ci` i 11 testów Workera. Po aktywacji 2 testy pakowania endpointu w `test_bundle_layout.py` przeszły. Wcześniejsze 18 testów klienta i kontraktu wdrożenia także przeszło.
- Zainstalowane AMC Test i AMC Public nie zostały przebudowane ani uruchomione ponownie. Obecny formularz w obu instalacjach nadal działa lokalnie bez przycisku wysyłania. Testowy endpoint jest aktywny wyłącznie w źródłach do następnej zweryfikowanej paczki AMC Test.
- Kontrolowane próbne zgłoszenie i odczyt obu GitHub Issues czekają na osobną zgodę użytkownika, wymaganą przez instrukcję Sygnalisty. Nie utworzono jeszcze żadnego Issue. Ręczny odbiór NVDA i instalacji pozostaje otwarty.
- Repozytorium nadal zawiera niezwiązane, niezapisane zmiany innych zadań. Nie scalać ich niejawnie z wydaniem Sygnalisty. Bieżący commit przy wdrożeniu: `19d08fd60da97736daa933a6cfc0d2ae901827a0`. Nie wykonano commitu, builda ani publikacji AMC.

Materiał do instrukcji użytkownika po instalacji: menu Pomoc → `Zgłoś błąd lub sugestię…` lub paleta `Ctrl+Shift+K`. W AMC Test z endpointem pojawi się `Wyślij zgłoszenie`. Użytkownik przegląda tę samą zredagowaną treść, którą wysyła, a dołączenie logu jest domyślnie wyłączone. Po błędzie nadal może skopiować lub zapisać raport lokalnie. To jest szkic instrukcji do korekty i zatwierdzenia, bez publikacji.
### Szkic listy zmian po odbiorze

- AMC Test: formularz „Zgłoś błąd lub sugestię…” może przesłać zredagowany raport do opiekuna przez testowy serwer zgłoszeń, po sprawdzeniu podglądu przez użytkownika. Log jest domyślnie wyłączony.
- Nadal dostępne są kopiowanie i zapis raportu lokalnie. Każde skuteczne wysłanie otrzymuje numer publicznego zgłoszenia.
- Ten szkic opisuje planowane zachowanie paczki AMC Test z endpointem. Nie ogłasza jeszcze wydania ani instalacji; wymaga korekty i zatwierdzenia użytkownika po ręcznym odbiorze.
### Kontrolowane zgłoszenie 8 listopada 2026

Użytkownik zgodził się na jedno próbne zgłoszenie przez testowego Workera. Wysłano dokładnie jeden `POST /v1/report` z kanałem `test`, rodzajem `suggestion`, `includedLog=false` i identyfikatorem `d3a30180-5874-4a4f-9b96-8646b8c12996`. Worker zwrócił HTTP 201, `duplicate=false` i publiczne Issue #12. Odczyt GitHuba osobno potwierdził:

- prywatne `michalkasperczak/amc-support-intake#1` — pełna, zredagowana treść testowa, bez logu;
- publiczne `michalkasperczak/AMC#12` — krótkie podsumowanie, kanał, brak logu, odsyłacz opiekuna do prywatnego Issue i ten sam identyfikator;
- oba Issues mają etykiety `enhancement`, `from-app` i `channel:test`.

Nie było ponowienia ani drugiego żądania. To potwierdza przepływ testowego Workera do obu repozytoriów; nie potwierdza jeszcze zachowania formularza w zainstalowanej aplikacji. Użytkownik następnie autoryzował zbiorczą aktualizację AMC Test z najnowszymi poprawkami, lokalnym Sonosem i bez LibreSpot, choć odbiór na głośniku odbędzie się później. Instalacja, ręczny odbiór NVDA i publikacja wydania pozostają osobnymi etapami. AMC Public jest już osobno wydany i na razie nie włączono w nim internetowego Sygnalisty.
### Czysty kandydat AMC Test po próbie GitHub

Z opublikowanego wcześniej źródła `ecd2f4439ff19561dc1351baaf1a4735ac099b0c` przygotowano osobny, czysty checkout wydania. Przeniesiono dokładnie 17 plików: endpoint testowego Sygnalisty, lokalnego Sonosa, wycofanie LibreSpot z nowych paczek i instalatora oraz ich testy. Dodano poprawkę skryptu `Build-AMCBundle.ps1`, aby profil Test przekazywał przypięte zależności Sonosa; składnia PowerShell przeszła. Prywatny commit dokumentacyjny `19d08fd6` i pozostałe niezapisane zmiany z kanonicznego drzewa nie weszły do historii kandydata.

- Testy w czystym checkoutcie: 94/94 dla pakowania, profili, Sonosa i Sygnalisty; 45/45 dla interfejsu Sonosa. Kontrola `git diff --check` i ograniczona kontrola wzorców sekretów przeszły.
- Commity źródła: `1f57050aa9605db44fdc6863374c5675321bd223` i `066a61b33592de86fb1d9647cf0779e72826e6ab`. Gałąź `codex/amc-test-sygnalista-sonos-20261108` jest na GitHubie `michalkasperczak/AMC`; zdalny i lokalny HEAD zgodnie wskazują `066a61b3`.
- To jest publikacja źródeł na gałęzi, nie wydanie GitHub Releases. Nie zbudowano jeszcze nowej kompletnej paczki ani instalatora, nie podmieniono AMC Test i nie wykonano ręcznego odbioru NVDA.
- Użytkownik 8 listopada wieczorem polecił wrócić do instalacji jutro, ponieważ teraz będzie nagrywał. Otwarte okno wyboru źródeł podcastów w bieżącym AMC Test pozostawiono bez zmian. Pełną kompilację i instalację odłożono, aby nie konkurowały z nagraniem. Przed instalacją ponownie sprawdzić aktywne i bliskie planowane nagrania oraz bezpieczne zamknięcie okna użytkownika.

### Korekta 9 listopada 2026 — skrót i odkrywanie polecenia

Użytkownik zgłosił brak widocznego polecenia Sygnalisty w używanej instalacji. Stan źródeł z 8.11 (menu Pomoc i paleta bez skrótu) zastępuje nowa decyzja: `Ctrl+Alt+Z` ma otwierać formularz z listy i odtwarzacza Test/Public. Ten sam opis zasila menu Pomoc, F1, Ctrl+F1 i paletę. Public nadal nie ma aktywnego endpointu internetowego; lokalny formularz działa. Źródła i testy opisano w `UPDATE-20261109-hubert-kod5-i-sygnalista.md`; instalacja, publikacja i odbiór NVDA nie nastąpiły.
