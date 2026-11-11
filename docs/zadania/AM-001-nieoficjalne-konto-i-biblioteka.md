# AM-001 — nieoficjalne konto i Biblioteka Apple Music

- Status: `W trakcie — bezpieczny magazyn, model rekordów i adapter odczytu mają 28/28 testów Apple; przypięty manifest zależności jest tylko w Test; żywe logowanie oczekuje`
- Utworzono: 12 listopada 2026
- Obszar: AMC Test, konto, katalog, Biblioteka, playlisty
- Punkt wznowienia: zbudować izolowany venv i Chromium z przypiętego manifestu, następnie wykonać jawny test logowania

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
  kopiuje trzy moduły i `requirements-apple-music-test.txt` wyłącznie do Test;
  Public odrzuca je również podczas walidacji manifestu.

## Następne kroki

1. Utworzyć osobny venv z `requirements-apple-music-test.txt` i przygotować
   przypięte Chromium Playwrighta w projektowym katalogu roboczym.
2. Dodać jawną sondę tylko do testu: bez parametru logowania nie może otworzyć
   przeglądarki; z parametrem pokazuje prawdziwe okno Apple i nie drukuje
   żadnych poświadczeń ani surowych odpowiedzi.
3. Po zgodzie użytkownika wykonać jedno widoczne logowanie oraz odczyt
   storefrontu, listy playlist i kilku elementów Biblioteki bez zmian konta.

## Materiał do dokumentacji użytkownika

Apple Music nie jest jeszcze sesją AMC. Przy pierwszej przyszłej próbie AMC
otworzy prawdziwe okno logowania Apple; hasło i 2FA nie będą wpisywane w AMC.
Poświadczenie sesji ma zostać zaszyfrowane przez Windows dla bieżącego
użytkownika i oddzielone od AMC Public.

## Testy i wynik

- Trzy celowane moduły Apple Music — 28/28: 8 magazynu i autoryzacji, 10
  normalizacji rekordów oraz 10 adaptera Biblioteki.
- Zaliczone jest także rzeczywiste szyfrowanie i odszyfrowanie DPAPI CurrentUser
  w katalogu tymczasowym. Plik nie zawiera jawnych trzech wartości testowych.
- Adapter opcjonalnej biblioteki jest wyłącznie w AMC Test, wymusza widoczne
  Chromium, wyłącza upstreamowy `storage-state.json`, zapisuje poświadczenia
  przez DPAPI i ponownie używa ich po restarcie. Test używa syntetycznej
  biblioteki bez konta, sieci i przeglądarki.
- `py_compile` modułu i testu — powodzenie.
- `python wxlite/run_tests.py apple_music release_profile bundle_layout` —
  84/84, bez pominięć. Zakres obejmuje izolację Test/Public, dokładne piny,
  zawartość syntetycznych paczek i walidację wspólnego kontraktu profili.
- `dotnet build` Launcher i Updater w `Release --no-restore` — oba powodzenie,
  0 ostrzeżeń i 0 błędów.
- Nie wykonano logowania, zapytania sieciowego Apple ani testu NVDA. Nie
  zbudowano kompletnej paczki AMC, niczego nie instalowano i nie publikowano.

## Znane ograniczenia

- Adapter nie jest jeszcze wywoływany przez hosta ani interfejs AMC.
- Manifest zależności jest przypięty i profilowo izolowany, ale pakiety Python
  oraz Chromium Playwrighta nie zostały jeszcze dołączone do runtime. Sam plik
  wymagań w paczce nie oznacza działającego logowania.
- Profil Chromium sam ma własne dane sesji; przed żywym testem trzeba ustalić,
  które pliki są konieczne i nie tworzyć dodatkowego jawnego storage state.
- Nie ma dowodu, że bieżące nieudokumentowane endpointy działają na koncie
  użytkownika albo zachowają zgodność po aktualizacji Apple.

## Wersja i commit

- Gałąź: `codex/amc-test-public-20261109`.
- Stan bazowy: `a51d90b44ab2fc43e65dc3ed5c872d4d3782c6ee`.
- Kod jest w źródłach i ma testy; bez buildu, instalacji i wydania.
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

Commit etapu: do uzupełnienia po zapisie.
