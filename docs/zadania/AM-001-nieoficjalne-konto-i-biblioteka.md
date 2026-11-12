# AM-001 — nieoficjalne konto i Biblioteka Apple Music

- Status: `Kod GUI AMC Test gotowy i przetestowany — żywe logowanie, DPAPI, stronicowany odczyt Biblioteki, cztery kategorie i playlisty są podłączone; odtwarzanie pozostaje w AM-002`
- Utworzono: 12 listopada 2026
- Obszar: AMC Test, konto, katalog, Biblioteka, playlisty
- Punkt wznowienia: podłączyć stronicowany odczyt do nieblokującej sesji GUI AMC Test, a odtwarzanie badać osobno w AM-002

## Cel

Zbudować wyłącznie dla AMC Test adapter `apple-music-py`, który po widocznym
logowaniu Apple i 2FA odczyta katalog, Bibliotekę, wykonawców, albumy i playlisty
do wąskiego modelu AMC. Pierwszy żywy etap jest tylko do odczytu. Zmiany
Biblioteki, ulubionych i playlist wchodzą dopiero po potwierdzeniu stabilnego
konta, odnowienia sesji i świeżej weryfikacji przed zapisem.

## Uzasadnienie i decyzje

- Biblioteka jest Alpha 0.0.1 i używa nieudokumentowanego
  `amp-api.music.apple.com`. Nie jest podstawą AMC Public.
- Domyślny `credentials.json` i `storage-state.json` zapisują na Windows tokeny
  oraz cookies zwykłym tekstem. AMC nie może ich używać.
- Powstał profilowo izolowany magazyn DPAPI CurrentUser w
  `wxlite/amc_wx_lite/apple_music_credentials.py`. Plik leży w prywatnych
  ustawieniach wxPython, nigdy w `state.json`, bazach, logach ani historii.
- Proces logowania nie może blokować głównego okna. Logowanie i 2FA pozostają
  widoczne, po powrocie AMC pokazuje jedno jawne działanie „Sprawdź logowanie”.
- Surowe rekordy Apple, identyfikatory, tokeny, cookies i odpowiedzi HTTP nie
  mogą trafić do tekstu list, NVDA ani Sygnalisty.
- `apple_music_source.py` przyjmuje tylko znane typy zasobów i pola prezentacji.
  Identyfikator zasobu i katalogu pozostaje w osobnym celu adaptera; jawny tekst
  wiersza zawiera wyłącznie nazwę, polski rodzaj, wykonawcę i czas.
- `apple_music_library.py` leniwie otwiera zależność dopiero po odczycie
  zaszyfrowanej sesji, działa tylko w profilu Test i udostępnia obecnie
  wyłącznie odczyt utworów, albumów, wykonawców, playlist oraz ich zawartości.
- Profil wydania otrzymał możliwość `apple-music.experimental`. Budowniczy
  kopiuje moduły konta, poświadczeń, Biblioteki i rekordów oraz
  `requirements-apple-music-test.txt` wyłącznie do Test;
  Public odrzuca je również podczas walidacji manifestu.
- `apple_music_account.py` rozdziela bezpieczną kontrolę lokalnego stanu od
  logowania. Kontrola stanu nie otwiera sieci ani Chromium. Tylko jawne
  `connect_account` może wywołać widoczne logowanie.
- Sonda `wxlite/tools/apple_music_library_probe.py` bez parametru wykonuje
  wyłącznie kontrolę lokalną. Parametr `--login` jest jedyną drogą do
  uruchomienia autoryzacji i nie wypisuje poświadczeń ani błędów dostawcy.
- Pełny odczyt wszystkich stron trwał ponad dwie minuty i został bezpiecznie
  przerwany. Adapter otrzymał więc `view_page` i `playlist_tracks_page`:
  domyślnie pobiera 25 pozycji, a nie całą kolekcję. Kursor jest modelem
  prywatnym, ma neutralny tekst i nie może przejść do obcego hosta, innego
  widoku ani innej playlisty.

## Następne kroki

1. Dodać usługę ładowania stron poza głównym wątkiem GUI, z pierwszą stroną
   widoczną od razu, kontrolowanym doładowaniem i anulowaniem.
2. Podłączyć wyłącznie odczyt do sesji Apple Music w AMC Test, wraz z cache
   podzielonym według profilu i konta oraz przewidywalnym fokusem NVDA.
3. Dopiero wtedy dodać sesję, menu, paletę i pomoc z jednego modelu poleceń.
   Operacje zapisu pozostają wyłączone, a odtwarzanie należy do AM-002.

**Aktualizacja 12.11.2026 — zastępuje powyższą listę jako bieżący punkt
wznowienia:** trzy kroki GUI zostały wykonane. Do zakończenia pozostaje
zbudowanie i odbiór zainstalowanej paczki AMC Test oraz osobny AM-002.
Zapisy Biblioteki i Ulubionych nadal są celowo wyłączone.

## Materiał do dokumentacji użytkownika

Apple Music nie jest jeszcze sesją AMC. Przy pierwszej przyszłej próbie AMC
otworzy prawdziwe okno logowania Apple; hasło i 2FA nie będą wpisywane w AMC.
Poświadczenie sesji ma zostać zaszyfrowane przez Windows dla bieżącego
użytkownika i oddzielone od AMC Public.

## Testy i wynik

- Pięć celowanych obszarów Apple Music — 35/35: 8 magazynu i autoryzacji, 10
  normalizacji rekordów, 10 adaptera Biblioteki, 5 konta i 2 sondy.
- Zaliczone jest także rzeczywiste szyfrowanie i odszyfrowanie DPAPI CurrentUser
  w katalogu tymczasowym. Plik nie zawiera jawnych trzech wartości testowych.
- Adapter opcjonalnej biblioteki jest wyłącznie w AMC Test, wymusza widoczne
  Chromium, wyłącza upstreamowy `storage-state.json`, zapisuje poświadczenia
  przez DPAPI i ponownie używa ich po restarcie. Test używa syntetycznej
  biblioteki bez konta, sieci i przeglądarki.
- `py_compile` modułu i testu — powodzenie.
- `python wxlite/run_tests.py apple_music release_profile bundle_layout` —
  89/89, bez pominięć. Zakres obejmuje izolację Test/Public, dokładne piny,
  zawartość syntetycznych paczek i walidację wspólnego kontraktu profili.
- Przypięte zależności zainstalowano z `--no-cache-dir` w
  `work/AM-001-apple-music-venv`; `pip check` zgłosił brak uszkodzonych
  zależności, a import potwierdził `applemusic 0.0.1` i Playwright 1.65.0.
- Playwright pobrał Chromium 1254, wariant headless, FFmpeg i Winldd do
  `work/AM-001-playwright-browsers`. Katalog zajmuje około 740 MiB. Nie
  uruchamiano przeglądarki ani strony Apple.
- Zwykłe uruchomienie sondy zwróciło „Apple Music: brak zapisanej sesji” oraz
  potwierdzenie braku przeglądarki i połączenia z Apple.
- 12.11.2026 użytkownik jawnie uruchomił logowanie, odnalazł przycisk
  „Zaloguj się” w web playerze, ukończył 2FA i wybrał zaufanie temu
  izolowanemu profilowi. Sonda zakończyła się komunikatem o bezpiecznym
  zapisaniu sesji.
- Plik DPAPI istnieje i miał po próbie 3636 bajtów. Nie powstały jawne
  `credentials.json` ani `storage-state.json`.
- Odczyt storefrontu przeszedł. Odczyt playlist i utworów zwrócił HTTP 400;
  ten sam wynik dały warianty bez parametrów, z samym limitem i z językiem
  `en-US` lub `pl-PL`.
- Bezpieczny pomiar żądania web playera potwierdził, że sam web player wysyła
  wymagane parametry i dodatkowy nagłówek wersji klienta, lecz również dostaje
  HTTP 400. Odpowiedź została zredukowana do kategorii „wymagana aktywna
  subskrypcja”. Strona nie prosi ponownie o logowanie i nie pokazuje oferty
  subskrypcji, dlatego wynik nie rozstrzyga jeszcze, czy użyto niewłaściwego
  Apple ID, abonament wygasł, czy prywatny endpoint ma błąd uprawnienia.
- Po dodaniu sond bezpieczeństwa `python wxlite/run_tests.py apple_music
  release_profile bundle_layout` zaliczył 97/97, bez pominięć.
- **Aktualizacja 12.11.2026 po zakupie subskrypcji — zastępuje wcześniejszy
  wynik HTTP 400 jako bieżący stan:** web player zaczął zwracać HTTP 200 bez
  ponownego logowania. Ograniczony adapter odczytał po 25 playlist, utworów,
  albumów i wykonawców; każda kolekcja ma dalsze strony.
- Pierwsza i druga strona utworów zwróciły po 25 pozycji. Przykładowa celowo
  zbudowana etykieta drugiej strony: „Ach, cóż to za romans był, utwór,
  Urszula Sipinska, 3:28”.
- Pierwsza playlista „Billy Joel — niezbędnik” zwróciła 25 utworów i dalszą
  stronę. Przykładowa etykieta: „Piano Man, utwór, Billy Joel, 5:39”.
- Celowany przebieg wykonał 100 testów poprawnie, a jeden niezwiązany test
  paczki nie mógł wrócić z katalogu tymczasowego na dysk D z powodu piaskownicy.
  Powtórzony samodzielnie poza piaskownicą przeszedł 1/1. Ostateczny wynik
  unikalnych testów: 101/101, bez błędu kodu i bez pominięć.
- `dotnet build` Launcher i Updater w `Release --no-restore` — oba powodzenie,
  0 ostrzeżeń i 0 błędów.
- Wykonano logowanie i kontrolowane zapytania odczytowe Apple. Nie wykonano
  testu NVDA, nie zbudowano kompletnej paczki AMC, niczego nie instalowano i
  nie publikowano.

## Znane ograniczenia

- Adapter nie jest jeszcze wywoływany przez hosta ani interfejs AMC.
- Pakiety Python i Chromium są przygotowane wyłącznie w projektowym katalogu
  `work`. Nie zostały dołączone do paczki ani zainstalowanej wersji AMC Test;
  gotowe laboratorium nie oznacza jeszcze działającej sesji programu.
- Profil Chromium sam ma własne dane sesji; przed żywym testem trzeba ustalić,
  które pliki są konieczne i nie tworzyć dodatkowego jawnego storage state.
- Bieżące nieudokumentowane endpointy zostały potwierdzone na koncie
  użytkownika, ale nadal mogą zmienić się bez zapowiedzi Apple.
- Zalogowanie nie oznacza automatycznie uprawnienia do spersonalizowanej
  Biblioteki. Oficjalna dokumentacja Apple wymaga dla niej aktywnego
  subskrybenta; bieżąca odpowiedź konta wskazuje właśnie tę bramkę.

## Wersja i commit

- Gałąź: `codex/amc-test-public-20261109`.
- Stan bazowy: `a51d90b44ab2fc43e65dc3ed5c872d4d3782c6ee`.
- Kod jest w źródłach i ma testy; izolowane zależności są przygotowane, ale bez
  pełnego buildu, instalacji i wydania AMC.
- Commit kodu i zapisu etapu: `5e37190abd3cf76229447544b0bcef6190f7a66d`.

## Zmienione pliki — 12 listopada 2026, etap modelu i odczytu

- `wxlite/amc_wx_lite/apple_music_source.py`
- `wxlite/amc_wx_lite/apple_music_library.py`
- `wxlite/amc_wx_lite/release_profile.py`
- `src/Shared/ReleaseProfileContract.cs`
- `wxlite/tools/build_bundle.py`
- `wxlite/requirements-apple-music-test.txt`
- `wxlite/tests/test_apple_music_source.py`
- `wxlite/tests/test_apple_music_library.py`
- `wxlite/tests/test_release_profile.py`
- `wxlite/tests/test_bundle_layout.py`

Commit modelu, odczytu i izolacji paczek:
`118913826c3a07195de682ca4c77b4fbe90b2681`.

## Zmienione pliki — 12 listopada 2026, etap jawnej sondy

- `wxlite/amc_wx_lite/apple_music_account.py`
- `wxlite/tools/apple_music_library_probe.py`
- `wxlite/tests/test_apple_music_account.py`
- `wxlite/tests/test_apple_music_probe.py`
- `wxlite/tools/build_bundle.py`
- `wxlite/tests/test_bundle_layout.py`

Commit jawnej bramki logowania: `964385fc`.

## Zmienione pliki — 12 listopada 2026, etap żywego logowania

- `wxlite/tools/apple_music_safe_diagnostic.py`
- `wxlite/tools/apple_music_web_request_probe.py`
- `wxlite/tests/test_apple_music_safe_diagnostic.py`
- `wxlite/tests/test_apple_music_web_request_probe.py`

Commit sond bez ujawniania danych: `71dcd385`.

## Zmienione pliki — 12 listopada 2026, aktywna Biblioteka i paginacja

- `wxlite/amc_wx_lite/apple_music_library.py`
- `wxlite/tools/apple_music_safe_diagnostic.py`
- `wxlite/tests/test_apple_music_library.py`
- `wxlite/tests/test_apple_music_safe_diagnostic.py`

Commit stronicowanego odczytu: `55935bb7`.
