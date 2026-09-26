# Raport: pierwszy przyrost klienta logowania Sonos (Core, bez GUI)

Worktree: `/home/michal/projekty/amc-sonos-login-client-after410`
Branch: `hermes/sonos-login-client-after410`, baza `73bd4fe` ("Procentowa nawigacja po buforze TimeShift; alfa 410")
Zakres: **wyłącznie Core** — start logowania + jednorazowy odbiór wyniku. Bez GUI, bez instalowania, bez refreshu, bez DPAPI/storage.

## Czego NIE zrobiono (granice weryfikacji — czytać razem z wynikami)

- **Nie było żadnego prawdziwego logowania Sonos.** Nie wywołano publicznego brokera `https://hermes.tail6caad7.ts.net`, nie utworzono tam sesji, nie dotknięto konta Sonos ani `api.sonos.com`. **Nie twierdzę, że klient działa LIVE** — to jest niezweryfikowane.
- Nie czytano `/etc/amc-sonos-auth/sonos.env` ani niczego z `/opt` i produkcji. Klient nie zawiera i nie potrzebuje żadnego app secretu.
- Cały ruch w testach idzie przez prywatną atrapę `HttpMessageHandler` w procesie testu — **zero sieci**. Wszystkie dane są jawnie syntetyczne (`SYNTETYCZNY-*`, `broker-testowy.invalid`, `SEKRET-ATAKUJACEGO-4f2a9c`).
- Nie uruchamiano zestawów Windows/WPF ani GUI. Brak refreshu tokenu, brak trwałego zapisu, brak orkiestracji UI — świadomie poza zakresem tego przyrostu.
- Nie pushowano, nie publikowano, nie zmieniano wersji, `main`, wspólnego `PLAN-16-09-2026.md`, innych worktree ani README backendu.

## Skąd wzięty kontrakt (kod, nie README)

Odczyt tylko-do-odczytu `/home/michal/projekty/amc-sonos-auth/amc_sonos_auth/{core.py,server.py}`:

- `POST /login/start` `{code_challenge, code_challenge_method:"S256"}` → `200 {session_id, authorize_url, expires_in}`; `400 invalid_code_challenge`/`unsupported_challenge_method`/`invalid_body`, `429 too_many_sessions`, `503 server_not_configured` (+ `503 server_busy` z warstwy HTTP `server.py`).
- `POST /login/result` `{session_id, code_verifier}` → `200 {access_token, token_type, expires_in, refresh_token, scope}`; `404 unknown_session` (brak wyniku / już odebrany / wygasła), `403 verifier_mismatch`, `400` = `invalid_code_verifier` albo **rozpoznana odmowa** (`access_denied`, `invalid_callback`, `token_exchange_failed`, domyślnie `login_failed`).
- `_S256_RE = ^[A-Za-z0-9_-]{43}$` (dotyczy `code_challenge`, `session_id`, `state`); verifier walidowany jako `43 <= len <= 128`.
- `SONOS_AUTHORIZE_URL = "https://api.sonos.com/login/v3/oauth"` — stąd polityka adresu autoryzacji na **rzeczywistym host+path**, nie na samym `https`.
- Istotny szczegół złapany z kodu, nie z domysłu: backend składa tokeny przez `doc.get(...)`, więc `expires_in`, `refresh_token`, `scope` mogą przyjść jako **JSON `null`**; wymagany jest tylko `access_token` (`isinstance(..., str)`). Klient to akceptuje zamiast zgłaszać błąd kontraktu.

## Pliki

Nowe (produkcyjne, Core):
- `src/AccessibleMediaController.Core/Sonos/SonosLoginContract.cs` — `SonosLoginPkce` (32 B CSPRNG → base64url bez paddingu, S256), `SonosLoginBrokerConfiguration` (zaufany origin HTTPS + stałe ścieżki `/login/start`, `/login/result`), `SonosAuthorizeUrlPolicy`, `SonosLoginStatus`, `SonosLoginSession`, `SonosTokens`, modele wyników.
- `src/AccessibleMediaController.Core/Sonos/SonosLoginClient.cs` — asynchroniczny `StartAsync` / `FetchResultAsync`, mapowanie statusów, limit odpowiedzi, fail-closed na przekierowaniach.

Nowe (testy):
- `tests/AccessibleMediaController.Core.SmokeTests/SonosLoginClientTests.cs` — 24 testy + atrapa transportu `FakeBrokerHandler`.

Zmodyfikowane (minimalnie, 2 linie):
- `tests/AccessibleMediaController.Core.SmokeTests/Program.cs` — przełącznik `--sonos-login-client` oraz wpis w pełnym zestawie. Architektura innych klientów **nie** była przebudowywana.

## Właściwości bezpieczeństwa wymuszone w kodzie

- Verifier/challenge: 32 B CSPRNG, **osobny per operacja** (test sprawdza 64 unikalne verifiery i dwa różne challenge w dwóch startach); zgodność S256 potwierdzona wektorem kontrolnym RFC 7636.
- Verifier idzie **wyłącznie** na skonfigurowany zaufany origin. Odpowiedź brokera ani wejście nie mogą przekierować wysyłki: przekierowania są wyłączone i traktowane jako `RedirectRefused` (302/301/307/308), a odpowiedź z obcym adresem końcowym jest odrzucana bez parsowania.
- `authorize_url` walidowany po host+path Sonos: odrzucane obcy host, `http`, zła ścieżka, sufiksowany host (`api.sonos.com.zlosliwy.invalid`), niestandardowy port.
- Verifier nie jest publiczną właściwością → nie trafi do `state.json` (test serializuje sesję i sprawdza brak pola). `ToString()` sesji nie wypisuje `session_id`; `ToString()` tokenów i wyników nie wypisuje `access_token`/`refresh_token` (pokazuje tylko np. `Bearer` i fakt posiadania refreshu). Brak app secretów w kliencie.
- Błędy są **stałe i bezpieczne**: nigdy nie echują surowego ciała, treści wyjątku transportu ani tokenu. Marker `SEKRET-ATAKUJACEGO-4f2a9c` wstrzykiwany w złośliwe odpowiedzi (w `error`, `detail`, `error_description`, `access_token`, oraz jako nie-JSON) nie pojawia się w `Message` ani `ToString()`.
- Rozdzielone statusy: `Success`, `Pending`, `Denied`, `Expired`, `RateLimited`, `BrokerNotConfigured`, `BrokerError`, `BrokerUnreachable`, `RedirectRefused`, `InvalidResponse`, `Canceled`. HTTP 400 = poprawna odmowa **nigdy** nie wydaje tokenu. `404` rozróżnia `Pending` (sesja żywa) od `Expired` (po `expires_in`) na podstawie wstrzykiwanego zegara.
- Skończony timeout, `CancellationToken` propagowany do transportu, limit rozmiaru odpowiedzi (`MaxResponseBytes`) egzekwowany także przy zadeklarowanym `Content-Length` — odpowiedź ponad limit jest **odrzucana, nie obcinana**. Brak nieograniczonego pollingu i **brak automatycznych ponowień** jednorazowego odbioru po niejednoznacznym błędzie (test liczy dokładnie 2 żądania).

## Wykonane komendy i rzeczywiste wyniki

.NET nie jest w PATH; użyto zweryfikowanego `/home/michal/dotnet/dotnet` (8.0.425). Jedna kompilacja naraz, `/m:1`. Nie instalowano pakietów ani SDK.

1. Kompilacja:
   `dotnet build tests/AccessibleMediaController.Core.SmokeTests/AccessibleMediaController.Core.SmokeTests.csproj -c Debug /m:1`
   → `Build succeeded. 0 Warning(s) 0 Error(s)`, **kod wyjścia 0**.

2. Zestaw Sonos (GREEN):
   `dotnet .../AccessibleMediaController.Core.SmokeTests.dll --sonos-login-client`
   → `Sonos: 24 testy klienta pierwszego logowania zaliczone.`, **kod wyjścia 0**. **Liczba wykonanych testów: 24.**

3. Kontrolowana mutacja (dowód RED — że asercje rozróżniają błąd):
   W `SonosLoginClient.FetchResultAsync` tymczasowo zmieniono warunek na
   `read.HttpStatus == HttpStatusCode.OK || read.HttpStatus == HttpStatusCode.BadRequest`,
   czyli HTTP 400 zaczął wydawać token z ciała odmowy. Wynik:
   `System.InvalidOperationException: Sonos: rozpoznana odmowa access_denied to Denied`
   (`SonosLoginClientTests.cs:256`), **kod wyjścia 1**.
   Mutacja została **wycofana**; finalny kod zawiera wyłącznie `HttpStatusCode.OK`, a ponowny bieg dał znowu 24/24 i kod 0. W finalnym kodzie **nie ma** żadnej szkodliwej zmiany.
   Dodatkowo w samym zestawie jest test różnicujący bez modyfikacji produkcji: **identyczne ciało** `{"access_token": ...}` pod `200` daje `Success`, a pod `400` daje `Denied` bez tokenu — więc asercja odmowy nie jest spełniona trywialnie.

4. Pełny konsolowy zestaw Core w WSL (kontrola regresji):
   `dotnet .../AccessibleMediaController.Core.SmokeTests.dll`
   → `OK: Klient pierwszego logowania Sonos wobec brokera AMC` oraz `Sonos: 24 testy ... zaliczone`; łącznie **7 `BŁĄD:`**, kod wyjścia 1.
   Sprawdzono, że to stan **odziedziczony, nie spowodowany tym przyrostem**: `git stash push -u` → kompilacja i bieg czystej bazy `73bd4fe` → również dokładnie **7** `BŁĄD:` → `git stash pop`. Te awarie dotyczą TIDAL (adres kolejnej strony), lokalnych plików audio / zmiany nazwy / „Pokaż w folderze”, edycji nagrań i hosta Librespot (kod 131) — obszary zależne od Windows/środowiska, nieruszane przez ten przyrost.

## Pokrycie testowe (24 testy)

PKCE zgodny z `_S256_RE` + wektor kontrolny; osobny verifier per operacja; zaufany origin (odrzucenie `http`, danych logowania w URL, query, pustego, innego portu/hosta); polityka `authorize_url`; ścieżka pozytywna startu (w tym `expires_in` → czas absolutny); start wysyła S256 i **nie** wysyła verifiera; odrzucenie obcego `authorize_url` (3 warianty); `429`/`503 server_not_configured`/`503 server_busy`/`400` przy starcie; ścieżka pozytywna wyniku z polami kontraktu i dowodem, że wysłany verifier odpowiada wcześniejszemu challenge; `Pending`; `Expired`; poprawna odmowa (4 kody) bez tokenu; `403 verifier_mismatch`; `429`/`503`/`500` przy odbiorze; brak serwera i timeout jako `BrokerUnreachable` (osobno od `Canceled`); anulowanie przed wysyłką (0 żądań) i w trakcie wysyłki (token propagowany); fail-closed dla 4 kodów przekierowania + podmieniony adres końcowy; 9 wariantów złego/niepełnego JSON-a bez echa ciała; oversize i kłamliwy `Content-Length`; marker sekretu w złośliwych odpowiedziach; brak tokenów w `ToString`/serializacji; brak automatycznych ponowień; test mutacyjny; nullowe/brakujące pola opcjonalne wg `doc.get(...)` backendu.

## Stan i następne kroki

Commit wybranych plików na `hermes/sonos-login-client-after410` (bez push). Kolejne, osobne przyrosty: refresh tokenu, bezpieczny zapis (DPAPI), orkiestracja i UI WPF, oraz — dopiero wtedy — rzeczywisty E2E z brokerem. Brak gotowego E2E nie blokował implementacji Core.
