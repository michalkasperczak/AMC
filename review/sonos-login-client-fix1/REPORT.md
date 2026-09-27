# Naprawa czterech blokad odbioru klienta pierwszego logowania Sonos (fix1)

Zakres: WYŁĄCZNIE klient pierwszego logowania Sonos w izolowanym worktree.
Praca: `/home/michal/projekty/amc-sonos-login-client-after410`, branch
`hermes/sonos-login-client-after410`, HEAD przed pracą
`b1957d443d886aeec1c4e5013903f5b35a59f83f` (baza `73bd4fe`).
SDK `/home/michal/dotnet/dotnet` (8.0.425), `-m:1`.

Bez push, bez merge, bez publikacji, bez zmiany wersji. Backend produkcyjny
`/home/michal/projekty/amc-sonos-auth/amc_sonos_auth/{core.py,server.py}`
czytany TYLKO do rozstrzygnięcia semantyki błędów; nie zmieniany, konfiguracja
sekretów nietknięta. Zero GUI/WPF/NVDA/SSH/systemd/sieci/Sonos LIVE — wszystkie
transporty syntetyczne (wstrzyknięty `HttpMessageHandler`).

## 1. Pliki zmienione

| Plik | Rola | SHA256 przed | SHA256 po |
|---|---|---|---|
| `src/AccessibleMediaController.Core/Sonos/SonosLoginClient.cs` | produkcja | `2105a9f6de13b549c8f82838a51ec13c7ea8a083554c20413778aec2a81678b2` | `c45dd0904888275495852afba5f1f97f15c1e2343f54af03d9f779bbd5250b9e` |
| `src/AccessibleMediaController.Core/Sonos/SonosLoginContract.cs` | produkcja | `518b0adc71c13b2b8a29d49ce4a64353106b6acc70572ff714df631e5aa582dc` | `d7c9bb6b6253f5b8228bfb3a700bf8f23b9aea6bf1d1d5c507c189e405ff967c` |
| `tests/AccessibleMediaController.Core.SmokeTests/SonosLoginClientTests.cs` | testy | (24 testy) | `b209850408b2891dc96f3c4eee374566c08e3b9fc39503c1c475b514ae9d85e7` (35 testów) |

Nowe artefakty dowodowe (tylko dokumentacja, poza kodem aplikacji):
`review/sonos-login-client-fix1/{REPORT.md, red-1-compile.log, red-2-run.log,
green-1-run.log, green-2-core-build.log, green-3-full-smoke.log,
probe2/{Program.cs,Probe.csproj,run.log}}`.

Stare dowody (`amc_pomoc/sonos-login-spec-review1/probe/*`,
`amc_pomoc/sonos-login-parent-review1/*`, `sonos-login-adjudication1/result.json`)
NIE zostały nadpisane — sonda spec działa na NOWEJ kopii `probe2/`.

## 2. TDD: RED przed poprawką

11 nowych przypadków, każdy łapany i liczony osobno (własny `try/catch` na
przypadek), żeby jedna awaria nie ukrywała pozostałych.

Krok RED-1 (`red-1-compile.log`): kompilacja harnessu pada, bo kontrakt nie ma
statusu dla 403 (`ProofMismatch` nie istniał) — dowód, że stary kontrakt nie
umiał w ogóle wyrazić poprawnej semantyki.

Krok RED-2 (`red-2-run.log`), po dodaniu samej wartości enum + komunikatu,
przed właściwymi poprawkami zachowania — **8 z 11 awarii**:

| Przypadek | RED (zachowanie przed) |
|---|---|
| B1.1 zatrzymane ciało przy `StartAsync` | **zawieszenie** — test przerwany przez własny watchdog; limit 200 ms nie kończył operacji (ciało nigdy nie dochodzi) |
| B1.2 opóźnione ciało przy `FetchResultAsync` | FAIL: `Success` po ~1500 ms przy limicie 100 ms |
| B1.3 szybka ścieżka pozytywna | PASS (kontrola — nie było regresji do naprawy) |
| B1.4 anulowanie wołającego w fazie ciała | FAIL: nie odróżniane od timeoutu |
| B1.5 sączone ciało (slow drip) | FAIL: `Success` — limit de facto odnawiany przy każdym odczycie |
| B2 `Timeout.InfiniteTimeSpan` i złe budżety | FAIL: infinite oraz `-1 ms` przyjmowane, obiekt i `HttpClient` powstawały |
| B3.1 `token_type` walidowany, bez echa | FAIL: `Success` + dowolny tekst serwera w `ToString()` |
| B3.2 model `SonosTokens` nie przyjmuje dowolnego typu | FAIL: konstruktor przepuszczał dowolny tekst |
| B4.1 403 `verifier_mismatch` | FAIL: `Denied` + „Sonos nie przyznał dostępu” |
| B4.2 400 `invalid_callback` / `token_exchange_failed` / `login_failed` | FAIL: `Denied` (obwinianie Sonos) |
| B4.3 gotowy wynik po lokalnym `ExpiresAt` | PASS (kontrola regresji — zabezpieczone, patrz §4) |

## 3. GREEN po poprawce

* Kompilacja testów: `Build succeeded. 0 Warning(s)`.
* `green-1-run.log`: **35/35 OK**, exit 0 — w tym wszystkie 11 nowych przypadków.
  `Sonos: 35 testow klienta pierwszego logowania zaliczonych.`
* Kompilacja `AccessibleMediaController.Core` (`green-2-core-build.log`):
  `Build succeeded. 0 Warning(s)`.
* Niezależna sonda specyfikacji (NOWA kopia `probe2/`, ta sama lista 15
  przypadków z asercjami): **`failures: 0`, 15/15 `SPEC_PASS`, `PROBE_RESULT=SPEC_PASS`**
  (przed poprawką: 4 × `SPEC_FAIL`). Pomiary po poprawce:

  * `B1-timeout-calej-operacji`: limit 100 ms, opóźnienie ciała 1500 ms →
    `BrokerUnreachable` po **105 ms** (było `Success` po 1500 ms).
  * `B2-timeout-nieskonczony-odrzucony`: `infinite_timespan_zaakceptowany: false`,
    wyjątek `ArgumentOutOfRangeException`.
  * `B3-brak-echa-tekstu-serwera-w-diagnostyce`: `InvalidResponse`,
    `niezwalidowany_token_type_w_ToString: false`, `ToString_dlugosc: 0`.
  * `S2`: `access_denied → Denied`, `token_exchange_failed → BrokerError`,
    `429 → RateLimited`, `503 server_not_configured → BrokerNotConfigured`,
    `503 bez JSON → BrokerError`, `500 → BrokerError`.
  * `S3`: `403 → ProofMismatch`, komunikat „Dowód logowania Sonos nie zgadza się
    z rozpoczętą sesją. Rozpocznij logowanie ponownie.”

## 4. Co dokładnie naprawiono

### B1 — pełny, skończony deadline HTTP (nagłówki + ciało + Dispose)
Jeden `CancellationTokenSource.CreateLinkedTokenSource(callerToken)` z
`CancelAfter(operationTimeout)` obejmuje `SendAsync`, `ReadAsStreamAsync`,
każdy `ReadAsync` ciała **i** zamknięcie zasobów. Limit **nie jest resetowany
przy odczycie** — sączone ciało ma ten sam łączny budżet. `HttpClient.Timeout`
też ustawiony na ten budżet (druga linia obrony przy własnym handlerze).
Rozróżnienie przyczyn: anulowanie wołającego → `Canceled`, wyczerpanie budżetu →
`BrokerUnreachable` (status już istniejący, bez zbędnego refactoru). Czyszczenie
zasobów jest ograniczone czasowo — brak nieskończonego oczekiwania na `Dispose`.

### B2 — zakaz budżetu nieskończonego i niepoprawnego, PRZED tworzeniem zasobów
Walidacja w konstruktorze, **przed** utworzeniem/przypisaniem `HttpClient`, więc
przy złym budżecie nie zostaje żaden zasób. Dopuszczone: skończony, dodatni
`TimeSpan` (`> TimeSpan.Zero`) do górnego zakresu wynikającego z
`CancellationTokenSource`/`HttpClient` (`int.MaxValue` ms), nie z losowo dobranej
liczby produktowej. Odrzucane: `Timeout.InfiniteTimeSpan`, `0`, wartości ujemne
(w tym `-1 ms`, które wcześniej przechodziło), `MaxValue` poza zakresem.
**Korekta raportu recenzenta:** sugestia, że `0` i wartości ujemne „też były
przyjmowane”, jest błędna — własny pomiar (`sonos-login-adjudication1/result.json`)
pokazuje, że `0`/`-2 ms` już wcześniej odrzucał sam `HttpClient`
(`ArgumentOutOfRangeException`); realną luką były `Infinite` i `-1 ms`. Ta część
raportu recenzenta nie została powtórzona.

### B3 — bezpieczny `token_type`, stała diagnostyka
`SonosTokens.TryCanonicalizeTokenType`: brak pola / JSON `null` → domyślny
`Bearer` (kontrakt backendu zachowany, żadne nowe pole nie staje się
obowiązkowe); dowolna wartość równa `bearer` bez względu na wielkość liter i
otaczające białe znaki → kanoniczny `"Bearer"`; cokolwiek innego →
**fail-closed `InvalidResponse`**, bez echa wartości serwera. Publiczny model też
nie przyjmie dowolnego tekstu — konstruktor `SonosTokens` rzuca `ArgumentException`
z komunikatem, który **nie cytuje** odrzuconej wartości. Testy higieny sprawdzają
`Message` i `ToString()`: brak markera serwera, brak wartości tokenu.

### B4 — prawdziwe przypisanie winy (źródło: `core.py`)
* `403 verifier_mismatch` → nowy `ProofMismatch` ze stałym komunikatem o
  **lokalnym dowodzie klienta**; bez echa. Uzasadnienie w komentarzu kodu:
  przy 403 broker **nie konsumuje** wyniku (`result_taken` nie jest ustawiane),
  więc ponowna próba pobrania ma sens, a Sonos nie podjął tu żadnej decyzji.
* `400 invalid_callback` → `BrokerError`: backend zwraca to przy braku/niepoprawności
  `code` w callbacku, nie przy odmowie Sonos.
* `400 token_exchange_failed` / `login_failed` → `BrokerError`: backend ustawia to
  przy **wyjątku** wymiany tokenu (timeout / JSON / HTTP), nie przy decyzji Sonos.
* `400 access_denied` → **pozostaje** `Denied` (jedyna prawdziwa odmowa).
* `400` bez rozpoznanego kodu → `InvalidResponse` (bez zmian).
* Kontrola „błąd NIGDY nie nosi tokenu” zachowana dla wszystkich tych ścieżek.

**Rozstrzygnięcie zapisane:** recenzent błędnie pochwalił
`token_exchange_failed ⇒ Denied` jako zgodne z kontraktem. Źródło backendu
(`core.py`, blok 260-277) i własny pomiar to korygują; to jedna i ta sama poprawka
semantyczna, nie nowa funkcja.

Zmiana oczekiwań w istniejących 24 testach: dotknięte **tylko** te dwa oczekiwania,
które wynikały z błędnego kontraktu (`TestWynikPoprawnaOdmowaNieDajeTokenu`
w części niedotyczącej `access_denied`, oraz `TestWynikVerifierMismatchToOdmowa`).
Kontrole braku tokenu, `!Succeeded` i `InvalidResponse` dla nierozpoznanego kodu
zostały zachowane i wzmocnione.

### Czego świadomie NIE wdrożono
* **Odrzucone: „nie wysyłaj GET po lokalnym `expiresAt`”.** Backend ma osobne TTL
  `pending`/`inflight`/`ready` — gotowy wynik może żyć ~300 s od callbacku, także
  PO pierwotnym deadline sesji. Wczesny `return Expired` zgubiłby udane logowanie.
  Zachowano obecne zapytanie GET poza zewnętrznym deadline i **dodano tylko
  kontrolę** (B4.3): gotowy wynik odebrany po lokalnym `ExpiresAt` nadal daje
  `Success` z tokenami.
* Kosmetyka i refactor reszty (`ownsHttpClient` itp.) — nie ruszone.

## 5. Regresje

* Pełny zestaw smoke Core w WSL (`green-3-full-smoke.log`): **6 awarii**, wszystkie
  obce (edycja nagrań/historia, PKCE TIDAL, kategorie wykonawcy TIDAL, „Pokaż w
  folderze”, odkrywanie plików lokalnych, zmiana nazwy pliku).
* Baseline czystego worktree `73bd4fe` i kandydata (dowód
  `amc_pomoc/sonos-login-parent-review1/baseline-comparison.json`): **7 awarii** —
  te same 6 plus `Transport osobnego procesu hosta Librespot: kod 131`, który w
  tym uruchomieniu **przeszedł** (`OK: Transport osobnego procesu hosta Librespot`),
  czyli jest znanym flakerem środowiskowym, nie skutkiem tej zmiany.
* **Zero nowych awarii**; żadna obca awaria nie była naprawiana.
* Sekcja Sonos: `OK: Klient pierwszego logowania Sonos wobec brokera AMC`, 35/35.

## 6. Granice dowodu — brak Sonos LIVE

* Nie było **żadnego** kontaktu z prawdziwym Sonos ani z produkcyjnym brokerem:
  brak sieci, brak OAuth, brak `client_id`/sekretów, brak przeglądarki.
  Wszystkie odpowiedzi HTTP pochodzą z wstrzykniętego handlera w pamięci.
* Opóźnienia fazy ciała są **realne** (strumień syntetyczny z prawdziwym
  `Task.Delay` / nigdy niekończącym się odczytem), nie symulowane ustawieniem
  zegara — pomiary czasu w logach to `Stopwatch` na faktycznym wykonaniu.
* Tokeny w testach to **markery syntetyczne** (`SYNTHETIC-DO-NOT-LOG-EXAMPLE`);
  żaden prawdziwy klucz nie był użyty, odczytany ani zapisany.
* **Czego to NIE dowodzi:** że prawdziwy broker Sonos zwraca dokładnie te kody
  i kształty JSON w każdej sytuacji produkcyjnej. Mapowanie statusów oparte jest
  na lekturze źródła backendu (`core.py`) i syntetycznych odpowiedziach, nie na
  obserwacji ruchu LIVE. Pierwsze prawdziwe logowanie (interaktywna zgoda w
  przeglądarce, realne opóźnienia sieci, realny `token_type` od Sonos) pozostaje
  do wykonania na głównym komputerze — jest to jedyna niepokryta klasa ryzyka.
* Nie zweryfikowano zachowania GUI/WPF ani odczytu przez NVDA — poza zakresem.
