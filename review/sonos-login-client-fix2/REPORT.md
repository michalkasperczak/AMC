# Sonos — naprawa I-1: 404 unknown_session odróżnione od błędnego 404

Repozytorium: `/home/michal/projekty/amc-sonos-login-client-after410`,
gałąź `hermes/sonos-login-client-after410`, baza `37a1b5a`.
SDK: `/home/michal/dotnet/dotnet` (8.0.425). Cały ruch w testach i sondach jest
SYNTETYCZNY (atrapa `HttpMessageHandler`); żadnego kontaktu z brokerem, kontem
Sonos ani siecią. Backend `amc_sonos_auth` czytany tylko do odczytu.

## 1. Defekt

`SonosLoginClient.MapResultFailure` traktował KAŻDE 404 z `/login/result` jako
kontraktowy brak wyniku i mapował je na `Pending` (sesja żywa) albo `Expired`
(po lokalnym `expires_in`). Broker zwraca jednak 404 w co najmniej dwóch
różnych znaczeniach:

- `{"error": "unknown_session"}` — `amc_sonos_auth/core.py:328,337`, kontraktowy
  brak wyniku dla znanej sesji;
- `{"error": "not_found"}` — `amc_sonos_auth/server.py:299`, nieznana ścieżka
  (np. origin z prefiksem `/api`, literówka w konfiguracji, obcy serwer/proxy).

Skutek: przy złym adresie brokera klient cicho mówił „logowanie jeszcze trwa”
i zapraszał użytkownika do czekania na wynik, który nigdy nie przyjdzie.

## 2. TDD — RED na zastanej produkcji

Nowy przypadek `TestI1CzterystaCztery404RozroznioneOdBledu` (rejestracja w
`UruchomPrzypadkiPoprawek`) uruchomiony PRZED zmianą produkcji:

```
/home/michal/dotnet/dotnet build tests/AccessibleMediaController.Core.SmokeTests/AccessibleMediaController.Core.SmokeTests.csproj -c Debug /m:1
/home/michal/dotnet/dotnet tests/AccessibleMediaController.Core.SmokeTests/bin/Debug/net8.0/AccessibleMediaController.Core.SmokeTests.dll --sonos-login-client
```

Rzeczywisty wynik (`../../amc_pomoc/sonos-login-client-fix2/red-client.log`), kod wyjścia 1:

```
  [FAIL] I-1 404 unknown_session odrozniony od 404 not_found/niepoprawnej odpowiedzi
         => Sonos: 404 not_found z nieznanej sciezki (server.py 299)
            (przesuniecie 0 s) musi dac InvalidResponse, dostano: Pending
System.InvalidOperationException: Sonos: nieprzeszle przypadki poprawek: 1/12
```

Asercja jest konkretna: na zastanym kodzie 404 `not_found` daje `Pending`.
Pozostałe 11 przypadków poprawek było [OK] — RED dotyczy wyłącznie tego defektu.

## 3. Poprawka (minimalna)

`src/AccessibleMediaController.Core/Sonos/SonosLoginClient.cs`, gałąź
`case HttpStatusCode.NotFound` w `MapResultFailure`: przed rozstrzygnięciem
Pending/Expired sprawdzamy istniejącym już weryfikatorem `ReadErrorCode`, czy
ciało niesie dokładnie `unknown_session`. Każde inne 404 → `InvalidResponse`
(stały komunikat, zero tokenów).

Czego NIE zmieniono: base URL, prefiksów, pozostałych kodów HTTP, kolejności
sprawdzeń przed `expired` (gotowy wynik 200 po lokalnym deadline nadal jest
`Success` — `B4.3` zielony), cyklu życia `HttpClient`/`ownsHttpClient`.

MINOR opisowy: w `SendAsync` usunięto z komentarza fałszywą obietnicę, że jeden
deadline obejmuje „zwolnienie strumienia”. Faktycznie `Dispose` robi blok
`using` po wyjściu z operacji i deadline go nie przerywa. Zmieniony jest tylko
komentarz — algorytm cyklu życia nietknięty.

## 4. GREEN

```
/home/michal/dotnet/dotnet build tests/AccessibleMediaController.Core.SmokeTests/AccessibleMediaController.Core.SmokeTests.csproj -c Debug /m:1   # Build succeeded, 0 Warning(s), 0 Error(s)
/home/michal/dotnet/dotnet tests/.../AccessibleMediaController.Core.SmokeTests.dll --sonos-login-client                                          # kod wyjscia 0
```

`../../amc_pomoc/sonos-login-client-fix2/green-client.log`:

```
  [OK]   B4.3 gotowy wynik po lokalnym ExpiresAt nadal daje Success
  [OK]   I-1 404 unknown_session odrozniony od 404 not_found/niepoprawnej odpowiedzi
Sonos: 36 testow klienta pierwszego logowania zaliczonych.
```

Nowy przypadek pokrywa: kontrolę dodatnią `unknown_session` w OBU stanach zegara
(0 s → Pending, 601 s → Expired) oraz 8 wariantów obcego 404 × 2 stany zegara
(`not_found`, `{}`, puste ciało, HTML, `error` liczbą, `error` obiektem, ucięty
JSON, kod ze wstrzykniętym markerem sekretu) — wszystkie `InvalidResponse`, bez
tokenów, bez echa ciała i ze STAŁYM komunikatem.

### Licznik testów policzony programatycznie

`python3 /home/michal/projekty/amc_pomoc/sonos-login-client-fix2/policz_testy.py`:

```
bezposrednie: 24 przypadki-poprawek: 12 RAZEM: 36
I-1 zarejestrowany: True
licznik w komunikacie: 36
```

Czyli 35 → 36; komunikat i faktyczna liczba są zgodne.

## 5. Sonda jakości — 16 przypadków, NOWY katalog

Oryginalne dowody nie zostały nadpisane. Sondę skopiowano do
`/home/michal/projekty/amc_pomoc/sonos-login-client-fix2/probe` (ten sam
`Program.cs`, `Probe.csproj` wskazuje absolutnie na pliki produkcyjne w repo).

```
cd /home/michal/projekty/amc_pomoc/sonos-login-client-fix2/probe
/home/michal/dotnet/dotnet run -c Debug /m:1 > ../probe-post-fix.json
```

Kod wyjścia 0, `fail=0`, **16 przypadków, 16 OK, 0 PROBLEM**. Kluczowa różnica
wobec stanu przed naprawą (`../sonos-login-quality-parent1/pre-fix.json`,
exit 1, 16/15 OK/1 PROBLEM):

| przypadek | przed | po |
|---|---|---|
| `P1-404-not_found` | **Pending (PROBLEM)** | **InvalidResponse (OK)** |
| `P2-kontrola-unknown_session` | Pending (OK) | Pending (OK) |
| `P2b-expired` | Expired (OK) | Expired (OK) |
| `P0-kontrola` (200 pełny kontrakt) | Success (OK) | Success (OK) |

Pozostałe 12 przypadków bez zmian, wszystkie OK.

## 6. Sonda zgodności ze specyfikacją (spec)

`/home/michal/projekty/amc_pomoc/sonos-login-client-fix2/spec-probe` (kopia
`sonos-login-spec-review1/probe`), log `../spec-post-fix.log`:
14 × SPEC_PASS, 1 × SPEC_FAIL — `S2-odmowa-429-503-5xx`. To NIE jest regresja
mojej zmiany: ta sonda nadal oczekuje `token_exchange_failed == Denied`, co
zostało świadomie zmienione wcześniejszym commitem `37a1b5a` (B4.2 — błąd
wymiany po stronie brokera nie jest decyzją Sonos). Sonda nie dotyka 404 w tym
przypadku. Dla porównania baseline tej samej sondy przed naprawą miał 4 ×
SPEC_FAIL; `S1-pending-i-expiry` jest PASS przed i po (Pending/Expired
zachowane).

## 7. Sprostowania wcześniejszych narracji

- Recenzent raportował „15 przypadków / 14 OK / 1 PROBLEM”. Faktyczna liczba
  przypadków w sondzie to **16** (15 OK, 1 PROBLEM) — potwierdzone teraz
  programatycznym zliczeniem `results` w JSON.
- Twierdzenie „RED na anulowaniu FAIL, choć log OK” było błędne: przypadek
  `B1.4 anulowanie wolajacego w fazie ciala` jest w logach [OK] i taki pozostał.
- Twierdzenie „sączone ciało: Success, choć log mówi InvalidResponse” też było
  błędne: `B1.5 saczone cialo ma LACZNY deadline` jest [OK]; klient nie zwraca
  tam Success.
- Reviewer pisał o WPF; `AccessibleMediaController.Core.csproj` celuje w czysty
  `net8.0`, bez WPF. Poprawna ścieżka SDK to `/home/michal/dotnet/dotnet`
  (wersja 8.0.425) — nie `dotnet8.0.425/dotnet`.
- Librespot nie był tu uruchamiany i nie jest nazywany „flaky” — brak diagnozy,
  brak takiego twierdzenia.

Stare logi i historia raportów nie były modyfikowane ani nadpisywane.

## 8. Zmienione pliki i commit

- `src/AccessibleMediaController.Core/Sonos/SonosLoginClient.cs` — rozróżnienie
  404 + korekta komentarza o deadline/Dispose.
- `tests/AccessibleMediaController.Core.SmokeTests/SonosLoginClientTests.cs` —
  nowy przypadek I-1, rejestracja, licznik 35 → 36.
- `review/sonos-login-client-fix2/REPORT.md` — ten raport.

Commit lokalny, bez push/merge/publikacji/wersjonowania/instalacji.
Zero GUI, NVDA, SSH, dostępu do głównego komputera, kont, OAuth live, sekretów
produkcyjnych, zmian w backendzie, systemd i restartów.
