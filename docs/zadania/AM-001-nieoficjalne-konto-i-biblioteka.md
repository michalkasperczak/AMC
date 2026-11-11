# AM-001 — nieoficjalne konto i Biblioteka Apple Music

- Status: `W trakcie — bezpieczny magazyn i adapter zapisu mają 8/8 testów; żywe logowanie i odczyt Biblioteki oczekują`
- Utworzono: 12 listopada 2026
- Obszar: AMC Test, konto, katalog, Biblioteka, playlisty
- Punkt wznowienia: podłączyć `apple-music-py` bez używania jego jawnych plików poświadczeń

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

## Następne kroki

1. Zsyntetyzować wąski kontrakt rekordów i testy etykiet bez technicznych pól.
2. Dołączyć przypiętą opcjonalną zależność tylko do paczki AMC Test.
3. Dopiero potem wykonać jedno widoczne logowanie oraz odczyt storefrontu,
   listy playlist i kilku elementów Biblioteki bez zapisu zmian na koncie.

## Materiał do dokumentacji użytkownika

Apple Music nie jest jeszcze sesją AMC. Przy pierwszej przyszłej próbie AMC
otworzy prawdziwe okno logowania Apple; hasło i 2FA nie będą wpisywane w AMC.
Poświadczenie sesji ma zostać zaszyfrowane przez Windows dla bieżącego
użytkownika i oddzielone od AMC Public.

## Testy i wynik

- `python -m unittest wxlite.tests.test_apple_music_credentials -v` — 8/8.
- Zaliczone jest także rzeczywiste szyfrowanie i odszyfrowanie DPAPI CurrentUser
  w katalogu tymczasowym. Plik nie zawiera jawnych trzech wartości testowych.
- Adapter opcjonalnej biblioteki jest wyłącznie w AMC Test, wymusza widoczne
  Chromium, wyłącza upstreamowy `storage-state.json`, zapisuje poświadczenia
  przez DPAPI i ponownie używa ich po restarcie. Test używa syntetycznej
  biblioteki bez konta, sieci i przeglądarki.
- `py_compile` modułu i testu — powodzenie.
- Nie wykonano logowania, zapytania sieciowego Apple, testu NVDA, buildu,
  instalacji ani publikacji.

## Znane ograniczenia

- Adapter nie jest jeszcze wywoływany przez hosta ani interfejs AMC.
- Profil Chromium sam ma własne dane sesji; przed żywym testem trzeba ustalić,
  które pliki są konieczne i nie tworzyć dodatkowego jawnego storage state.
- Nie ma dowodu, że bieżące nieudokumentowane endpointy działają na koncie
  użytkownika albo zachowają zgodność po aktualizacji Apple.

## Wersja i commit

- Gałąź: `codex/amc-test-public-20261109`.
- Stan bazowy: `a51d90b44ab2fc43e65dc3ed5c872d4d3782c6ee`.
- Kod jest w źródłach i ma testy; bez buildu, instalacji i wydania.
- Commit kodu i zapisu etapu: `5e37190abd3cf76229447544b0bcef6190f7a66d`.
