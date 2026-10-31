# TEST-20261031-SPOTIFY — zbiorcza aktualizacja AMC Test

- Polecenie użytkownika: 31.10.2026. Włączyć ostatnie gotowe poprawki Spotify
  i niedostarczone nowości do jednego następnego wydania po testach, z ochroną
  nagrania zaplanowanego na 15:00.
- Wersja docelowa: `0.1.0-alpha.458`, wyłącznie profil AMC Test.
- Status: **kod, regresja, pakiet i wydanie GitHub gotowe; instalacja i odbiór
  na koncie do weryfikacji**.
- Podstawa: zainstalowana `alpha.457`, commit
  `22281e80b0574bdcf26060c671432e94e1551630`.

## Materiał do dokumentacji użytkownika

- W sesji Spotify `Ctrl+F5`, menu konfiguracji sesji i paleta `Ctrl+Shift+K`
  otwierają okno konta. Gdy brakuje konfiguracji, fokus trafia do Client ID.
  Widoczne są pola do skopiowania adresu powrotu i panelu aplikacji Spotify.
  AMC zapisuje wyłącznie publiczny Client ID; nie prosi o Client Secret.
- „Zaloguj w przeglądarce” łączy katalog. Osobne „Połącz odtwarzacz Spotify”
  pokazuje kod i adres parowania. „Sprawdź parowanie” odczytuje stan,
  a „Anuluj parowanie” kończy oczekiwanie. Zamknięcie podczas oczekiwania
  zleca anulowanie. Główne okno AMC pozostaje dostępne; ponowne `Ctrl+F5`
  podnosi to samo okno. Escape i przycisk Zamknij zamykają okno.
- Po pełnej synchronizacji AMC zachowuje migawkę kolekcji. Niepełne dane
  nie zastępują ostatniej poprawnej migawki. Komunikat `trackUnavailable`
  wyjaśnia zarówno możliwą niedostępność utworu, jak i odmowę klucza audio
  przez Librespot, bez ujawniania danych technicznych hosta.
- Okno konta TIDAL korzysta z tego samego zachowania bez blokowania głównego
  AMC. Spotify w profilu Public pozostaje wyłączone. `Shift+A` nadal dotyczy
  wyjścia Librespot; oficjalny silnik Chrome/Soloist nie został wdrożony.
- Dla aplikacji Spotify w Development Mode właściciel potrzebuje Premium,
  a do pięciu użytkowników musi znaleźć się na liście dozwolonych. Adres
  powrotu musi dokładnie odpowiadać zapisowi w panelu Spotify, łącznie z
  końcowym ukośnikiem. Źródła oficjalne:
  https://developer.spotify.com/documentation/web-api/concepts/quota-modes
  https://developer.spotify.com/documentation/web-api/concepts/redirect_uri

## Uzasadnienie i decyzje

- Porównano niezapisane zmiany kanonicznego repozytorium z `alpha.457` oraz
  ostatnie gałęzie prac. Poprawki list, wydajności, historii, pobrań i Radio
  Archiwisty były już w podstawie. Do integracji pozostały sprawdzone zmiany
  Spotify #9, #10 i komunikat #11. Nie przeniesiono starszych kopii tych samych
  funkcji ani eksperymentalnego toru odtwarzania.
- Zmiany Spotify przeniesiono z niezapisanej pracy do izolowanego worktree,
  zachowując kanoniczne repozytorium bez zmian. Okno konta zmieniono na
  niemodalne, zgodnie z kontraktem dostępności; spóźnione odpowiedzi po
  zamknięciu nie dotykają zniszczonych kontrolek.
- Korekta użytkownika z 31.10.2026 o zbiorczej aktualizacji zastępuje wcześniejsze
  odkładanie Spotify jako zupełnie osobnego wydania. Wydanie nadal wymaga
  sprawdzenia stanu nagrywania przed lokalną podmianą.

## Testy i wynik

- Testy Python celowane: 104 zaliczone po integracji, następnie 68 zaliczonych
  i 1 pominięty po zmianie okna na niemodalne.
- Pełna regresja Python: **2252 zaliczone, 114 pominiętych, 13 podtestów
  zaliczonych, bez błędów** (101,67 s).
- Testy protokołu hosta: 28 zestawów zaliczonych. Kompilacja LiteHost Release:
  0 ostrzeżeń i 0 błędów. Bezokienne testy cyklu Librespot i jego regresji:
  zaliczone, na fikcyjnym tokenie i atrapie hosta.
- Rzeczywiste logowanie, parowanie, odtwarzanie i wypowiedzi NVDA: nie były
  wykonywane na koncie użytkownika; wymagają odbioru po instalacji.

## Znane ograniczenia

- AMC pobiera informacje Spotify przez Web API z ograniczeniami konta i
  kwoty Development Mode. Nie ma potwierdzonego rozszerzonego limitu.
- Librespot może nadal odmówić klucza audio niektórym utworom. Nowy komunikat
  diagnozuje możliwe przyczyny, ale nie zastępuje silnika odtwarzania.
- Zamknięcie okna podczas długiego logowania nie anuluje żądania OAuth;
  odpowiedź nie modyfikuje zniszczonego okna. Stan można odczytać po ponownym
  otwarciu konta.

## Wersja i commit

- Źródła i pakiet: commit `acbe251960fce7fe15253f08d9a6fb0747999940`,
  `0.1.0-alpha.458`, nazwa użytkowa `AMC Test — 31.10.2026, 16:37`.
- Pakiet: 2141 plików; kontrolowany start interfejsu i hosta zaliczony.
  Instalator:
  `D:\Projekty Codex\AMC aktualizacje\AMC Test 0.1.0-alpha.458 Spotify 2026-10-31\instalator\AMC-Test-2026-10-31-1637.exe`.
- Weryfikacja instalatora: 133625328 bajtów,
  SHA-256 `caee61ec7e580aabe891db9d0cef6301e5813d7a8f0481f57653552dcf07709b`;
  kontrola sekretów zaliczona.
- GitHub: wydanie wstępne `test-2026.10.31.1637`, adres
  https://github.com/michalkasperczak/AMC/releases/tag/test-2026.10.31.1637.
  Zdalny instalator ma tę samą nazwę, rozmiar i sumę SHA-256.
- Lokalna instalacja: oczekuje na potwierdzenie braku aktywnego nagrywania,
  zgodnie z `AGENTS.md`; nie zatrzymano działającej aplikacji.

## Projekt instrukcji i listy zmian

Instrukcja: „Otwórz sesję Spotify i naciśnij Ctrl+F5. Jeśli katalog nie jest
skonfigurowany, wpisz Client ID i zapisz go. Skopiuj pokazany adres powrotu
do panelu Spotify. Zaloguj katalog w przeglądarce. Odtwarzacz połącz osobnym
przyciskiem, przepisując pokazany kod na stronie Spotify. Wróć do AMC i wybierz
Sprawdź parowanie.”

Opis zmian: „AMC Test dodaje konfigurację Client ID i parowanie odtwarzacza
Spotify, poprawia trwałość kompletnej kolekcji i wyjaśnia odmowę odtwarzania
Librespot. Okno konta nie blokuje głównego programu.”
