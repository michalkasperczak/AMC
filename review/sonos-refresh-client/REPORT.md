# Klient odnawiania dostępu Sonos w Core AMC — raport

Gałąź `hermes/sonos-refresh-client-after410`, baza `310162b`, izolowany worktree
`/home/michal/projekty/amc-sonos-refresh-client-after410`. Worktree odebranego
logowania (`amc-sonos-login-client-after410`) pozostał nietknięty.

Kontrakt odczytany ze **źródeł** odebranego backendu:
`/home/michal/projekty/amc-sonos-auth-refresh1`, commit
`b27ed4b2e31fcc232677f9fe2f36aa36f237d1d3` — `amc_sonos_auth/core.py` i
`README.md`. Katalogu produkcyjnego nie czytałem, poświadczeń z `/etc` ani
instalacji w `/opt` nie dotykałem, publicznego brokera i Sonosa nie wołałem.

## Co dodałem

`SonosLoginClient.RefreshAsync(string? refreshToken, CancellationToken)` — jedno
żądanie `POST /login/refresh` przez **ten sam** ograniczony transport, którym już
idą `StartAsync` i `FetchResultAsync` (metoda `SendAsync` + `ReadBoundedAsync`,
bez kopiowania kodu sieciowego). Dziedziczy więc: przypięty, skonfigurowany
origin HTTPS, brak przekierowań, wyłączone ciasteczka, **jeden** skończony
deadline łączony z tokenem wołającego na nagłówki i ciało, limit odpowiedzi
64 KiB, brak pollingu i brak automatycznych ponowień.

Nowy plik `src/.../Sonos/SonosRefreshContract.cs`: `SonosRefreshTokenPolicy`
(nasza polityka wejścia), `SonosRefreshStatus`, `SonosRefreshMessages`,
`SonosRefreshOutcome`. Do konfiguracji doszło jedno pole `RefreshUri`,
wyprowadzone ze **skonfigurowanego** origin, nigdy z odpowiedzi serwera.
Semantyki `Start`/`Fetch` nie zmieniałem — diff w `SonosLoginClient.cs` to
wyłącznie dopisanie, 0 linii usuniętych.

## Bezpieczeństwo, o które prosiłeś

* **Brak sekretu aplikacji.** Ciało żądania ma **dokładnie jedno** pole
  `refresh_token`; test R-48 wylicza pola z rzeczywistego ciała i sprawdza brak
  jakiegokolwiek „secret”.
* **Brak automatycznego kasowania poświadczeń.** Ta warstwa nie ma magazynu,
  timera ani kontrolera sesji i nigdy nic nie usuwa. Wystawia tylko
  `RequiresReauthorization`, wyprowadzone **wyłącznie** z HTTP 401 z ciałem JSON
  tego backendu i `error` dokładnie równym `reauthorization_required`. 401 z HTML,
  401 z nieznanym kodem i 401 z zepsutym JSON dają `InvalidResponse` i
  `RequiresReauthorization == false`. To nadal **nie** jest zgoda, by później
  w UI nadpisać nowszą generację logowania.
* **Token nieprzezroczysty.** Zero trim, zero normalizacji, zero reguł UUID.
  Test R-17 porównuje bajt w bajt wartość wysłaną z wartością wejściową, razem
  ze spacjami wiodącymi/końcowymi, niezłamliwą spacją i znakami wielobajtowymi.
* **Nasza polityka, nie limit Sonosa.** `SonosRefreshTokenPolicy` powtarza
  politykę brokera i mówi to wprost w komentarzu: niepusty tekst, poprawny
  ścisły UTF-8, ≤ 2048 B zdekodowanych, bez C0/DEL/C1, oraz zachowawczy budżet
  zakodowanego ciała (bezpieczne ASCII `A-Za-z0-9`, spacja, `-._~` po 1 B; każdy
  inny znak po 6 B na jednostkę UTF-16, czyli 12 B poza BMP; plus zmierzone
  opakowanie jednego pola, brane jako `max(nasz pomiar, 21 B)`), razem ≤ 4096 B.
* **Druga, niezależna kontrola.** Po serializacji liczę `Encoding.UTF8
  .GetByteCount` **rzeczywistego** ciała i odmawiam wysyłki powyżej 4096 B —
  model budżetu jest szacunkiem, pomiar jest dowodem.
* **Złe wejście = zero HTTP.** Zwracam typowany `InvalidLocalToken`, bez
  żadnego zapytania i bez sugerowania, że poświadczenia są nieważne.
* **Brak echa.** `Message`/`ToString` nie wypisują tokenów, ciała odpowiedzi ani
  `token_type` przysłanego przez serwer.

## Rozdzielone wyniki

`Success` (zawsze z **rotowanym** tokenem od brokera), `InvalidLocalToken`,
`RequestRejected` (400 `invalid_body`/`invalid_json`/`invalid_refresh_token`,
405, 413), `ReauthorizationRequired` (tylko dokładne 401), `RateLimited` (429),
`BrokerNotConfigured` (503 `server_not_configured`), `RefreshUnavailable`
(503 `refresh_unavailable`), `BrokerError` (502 i inne 5xx), `BrokerUnreachable`
(sieć/TLS/timeout), `Canceled`, `InvalidResponse` (w tym 404 — **nigdy**
„w toku”), `RedirectRefused`.

W sukcesie `token_type` i `refresh_token` są **wymagane**. Brak pola, `null`
i zły typ to trzy osobne wady tej samej odpowiedzi i żadnej z nich nie domyślam
się na Bearer ani na „zostaw stary token”; broker sam realizuje dopuszczalny
nawrót do dotychczasowej wartości, więc milczenie odpowiedzi to zepsuta
odpowiedź, a nie zgoda. Poprawną wielkość liter `bearer` kanonizuję do `Bearer`.
`expires_in` jest opcjonalne, ale obecne musi być dodatnią liczbą całkowitą;
`scope` obecne musi być tekstem. Kontraktu `/login/result` (gdzie brak
`token_type` **jest** dopuszczalny) nie zmieniałem.

## Testy — rzeczywiste przebiegi

SDK: `/home/michal/dotnet/dotnet` 8.0.425, `/m:1`, cel `net8.0`, bez GUI/WPF.
Cały ruch przez syntetyczne atrapy `HttpMessageHandler`; zero sieci, zero konta.

| Krok | Polecenie | Wynik |
|---|---|---|
| Kompilacja | `dotnet build tests/...SmokeTests.csproj -c Debug /m:1` | exit 0, 0 błędów, 0 ostrzeżeń |
| RED behawioralny | ta sama tabela przypadków na **kompilującej się**, naiwnej `RefreshAsync` | exit 1, **18/50** zaliczonych, 32 awarie |
| GREEN wąski | `...SmokeTests.dll --sonos-refresh-client` | exit 0, **50/50** |
| Regresja logowania | `...SmokeTests.dll --sonos-login-client` | exit 0, „Sonos: 36 testow klienta pierwszego logowania zaliczonych.” |
| Pełny Core (×2) | `...SmokeTests.dll` | exit 1, **6 awarii — dokładnie znane bazowe**, 0 nowych |
| Kontrola bez mojego diffu | `git stash push -u` → build → run → `stash pop` | exit 1, te same 6 awarii |

RED nie był błędem kompilatora o brakującym symbolu: naiwna wersja się budowała
(0 błędów) i przechodziła 18 przypadków, a padała na zachowaniu — m.in. „brak
`refresh_token` w 200” dawało `Success` z cicho zachowanym starym tokenem, każde
401 dawało `ReauthorizationRequired`, a złe wejście generowało zapytanie HTTP.
Licznik zliczam z tabeli przypadków: każdy jest łapany osobno, jedna awaria nie
zatrzymuje pozostałych, a podsumowanie podaje liczbę zaliczonych i pełną listę
niepowodzeń przed rzuceniem wyjątku.

Zakres 50 przypadków: sukces i rotacja, wartość dotychczasowa oddana przez
brokera, brak/`null`/zły typ/pusty/nieprzechodzący polityki `refresh_token`,
`token_type` (brak, `null`, `bearer`, obcy bez echa), `access_token`,
`expires_in` (brak, 0, tekst), `scope` nie-tekst, spacje i Unicode zachowane,
2048 bezpiecznych ASCII, granica budżetu w limicie i o jeden znak za dużo,
2048 cudzysłowów i 512 emoji (0 zapytań), pusty/`null`/C0/C1/DEL/niesparowany
surogat/2049 B (0 zapytań), dokładne 401 kontra 401 HTML/nieznane/zepsuty JSON,
400, 413, 429, 503 ×2, 502, 404, przekierowanie i obcy adres końcowy,
skończony deadline zawieszonego ciała, anulowanie w fazie ciała i anulowanie
z góry, odpowiedź ponad 64 KiB, brak ponowień, brak wycieku markera, jedno pole
bez sekretu, zaufany `RefreshUri`, opisy bez wartości.

### Sześć awarii bazowych pełnego Core pod WSL

Niezwiązane z Sonosem, zmierzone wcześniej w
`/home/michal/projekty/amc_pomoc/sonos-login-final-core/results.json`: edycja
nagrań/kopia bezpieczeństwa, dwa testy TIDAL o adresie kolejnej strony, „Pokaż
w folderze”, odkrywanie plików lokalnych, bezpieczna zmiana nazwy. Nie nazywam
pełnego Core zaliczonym i nie nazywam tych awarii flaky.

Jedno zastrzeżenie uczciwości: w **pierwszych dwóch** przebiegach pełnego Core
pojawiła się siódma awaria — „Transport osobnego procesu hosta Librespot:
Proces hosta Librespot zakończył się (kod 131)”. Nie odpisałem jej jako flaky:
uruchomiłem kontrolę z odłożonym (`git stash`) moim diffem — 6 awarii, bez
Librespota — i dwa dalsze przebiegi **z** moim diffem, również po 6 awarii.
Siódma awaria wystąpiła wyłącznie w przebiegach nakładających się z równoległym
procesem testowym i nie jest skutkiem tej zmiany; mój diff nie dotyka Spotify
ani Librespota.

## Kwity

`/home/michal/projekty/amc_pomoc/sonos-refresh-client-final/`: `results.json`
(polecenia, kody wyjścia, liczniki, SHA-256 źródeł), `red.txt`, `refresh.txt`,
`login.txt`, `full.txt`…`full4.txt`, `kontrola-baseline.txt`, `diff.patch` oraz
kopie dostarczonych plików.

## Czego tu NIE ma

* Zapisu tokenów, DPAPI, timera odświeżania i kontrolera sesji — osobny etap.
* Interfejsu użytkownika: żadne okno ani komunikat AMC jeszcze nie woła
  `RefreshAsync`; tu nie ma nic dla NVDA do przeczytania.
* Dowodu wobec **prawdziwego** Sonosa i prawdziwego brokera. Wszystkie
  odpowiedzi w testach są syntetyczne; publiczne wdrożenie backendu nie
  nastąpiło, konta nie używałem.
* Logiki decydującej, czy i kiedy usunąć zapisane poświadczenia po
  `ReauthorizationRequired`. Ta warstwa tylko raportuje; ochrona nowszej
  generacji logowania musi powstać razem z magazynem.
* Publikacji: commit lokalny, bez push, merge, wydania i zmiany wersji.
