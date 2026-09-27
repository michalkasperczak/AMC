# Sonos: trwały magazyn poświadczeń szyfrowany DPAPI — raport przyrostu

Worktree: `/home/michal/projekty/amc-sonos-credentials-after410`
Gałąź: `hermes/sonos-credentials-after410`, baza `bfbcb84`.
Data przebiegu: 2026-09-27.

## Co powstało

| Plik | Rola |
| --- | --- |
| `src/AccessibleMediaController.Core/Sonos/SonosCredentialStoreContract.cs` | kontrakt, model, serializacja i walidacja — bez I/O i bez szyfrowania |
| `src/AccessibleMediaController.Windows/Services/SonosDpapiCredentialStore.cs` | implementacja Windows: natywne DPAPI bieżącego użytkownika, atomowy zapis |
| `tests/SonosCredentialStoreHarness/{run.sh,csharp/*}` | odtwarzalny harness uruchamiany natywnie na Windows |

Nie dotknięto: `AppSettings`, `state.json`, eksportów, żadnej biblioteki,
cudzych magazynów (`TidalCredentialStore`, `SpotifyLibrespotCredentialStore`,
`SpotifyCredentialStore`) ani zamrożonych `Sonos/SonosLoginClient.cs`,
`SonosLoginContract.cs`, `SonosRefreshContract.cs`.

## Decyzje projektowe i ich uzasadnienie

- **Plik DPAPI, nie Menedżer poświadczeń.** Wzorce TIDAL/Librespot używają
  `CredRead`/`CredWrite`, ale blob Menedżera ma ścisły limit rozmiaru, a zestaw
  tokenów Sonos może być duży. Zamiast obchodzić limit — jeden plik szyfrowany
  natywnym DPAPI.
- **`CryptProtectData`/`CryptUnprotectData` przez P/Invoke `crypt32`**, bez
  nowych pakietów (nie mamy helpera `ProtectedData`). Zakres BIEŻĄCEGO
  UŻYTKOWNIKA (brak flagi `CRYPTPROTECT_LOCAL_MACHINE`), zawsze
  `CRYPTPROTECT_UI_FORBIDDEN`. Stała entropia domeny
  `AccessibleMediaController/Sonos/credentials/v1`.
- **Świadome odejście od wzorca TIDAL:** odczyt i walidacja NIE kasują
  zepsutego pliku, a `ToString` modeli nie wypisuje sekretów.
- **Brak `expires_in` zostaje stanem NIEZNANYM.** `ExpiresAtUtc` jest wtedy
  `null` — żadnego wymyślonego 24 h TTL. Brak RT po pierwszym logowaniu jest
  zapisywany jak jest; nie produkujemy fałszywego RT.
- **Wiązanie z brokerem.** Rekord niesie SKONFIGUROWANY, znormalizowany
  `BrokerOrigin`; odczyt z inną konfiguracją zwraca `BrokerMismatch` i NIE
  przełącza adresu na zapisany w pliku.
- **Limit rozmiaru** `MaxEncryptedFileBytes` = 512 KiB, sprawdzany PRZED
  alokacją bufora (również dla wyjścia DPAPI).
- **Zgodność z warstwą odbioru.** `MaxOpaqueValueBytes` = 64 KiB
  (`SonosLoginClient.MaxResponseBytes`) dla access tokenu i scope; wcześniejsze
  8 KiB / 4 KiB odrzucały tokeny, które klient już przyjął jako `Success`.
  `MaxPlaintextBytes` jest WYLICZANE z limitów pól, nie dobrane. JSON pisany
  enkoderem `UnsafeRelaxedJsonEscaping` (plik pod DPAPI, nie HTML), więc `<` to
  1 B, nie 6 B. Scope pusty = jawnie `""`, nigdy `null`, nigdy trim. Wartości
  nadal bez trim i bez normalizacji; UTF-8/surogaty i znaki sterujące dalej
  odrzucane, a jeden bajt ponad limit to `InvalidRecord`.
- **Zapis obcego brokera.** `Write` porównuje `BrokerOrigin` ze SKONFIGUROWANYM
  brokerem PRZED szyfrowaniem i I/O → `InvalidRecord`, stary ciphertext bit w
  bit, właściciel dalej czyta swój rekord.
- **Atomowość:** szyfrowanie przed I/O → unikalny temp w tym samym folderze →
  `Flush(flushToDisk: true)` → `File.Replace` istniejącego albo `File.Move`
  pierwszego. Nigdy Delete+Write, bez `.bak`, bez plaintextowych tempów.

## Przebieg TDD

1. **RED.** Zaślepka `SonosDpapiCredentialStore` (Read→Missing, Write→Ok,
   Delete→false) + minimalny test roundtrip. Przebieg na Windows:
   `WYNIK: porazek 3`, `EXITCODE=1`.
2. **GREEN.** Implementacja DPAPI — ten sam test: 12/12, `EXITCODE=0`.
3. **Rozbudowa RED/GREEN** do 15 scenariuszy, 111 kontroli.
4. **RED zgodności i zapisu obcego brokera** (scenariusze 16–18, dodane do TEGO
   harnessu, bez nowego): `WYNIK: porazek 13`, `EXITCODE=1` na prawdziwym
   Windows DPAPI — cztery wartości, które klient zwraca jako `Success`
   (`scope=""`, access 8193 B, scope 4097 B, 23000× `<`), magazyn odrzucał; duża
   wartość równa `MaxResponseBytes` też; zapis rekordu obcego brokera KOŃCZYŁ
   SIĘ sukcesem i podmieniał ciphertext.
5. **GREEN:** limity pól przestawione na limit całej odpowiedzi brokera,
   enkoder zapisu mniej escapujący, jawnie pusty scope, kontrola origin w
   `Write` przed szyfrowaniem. 18 scenariuszy, **139 kontroli**, `EXITCODE=0`.

## Wynik końcowy (rzeczywisty Windows, runtime 8.0.31)

```
tests/SonosCredentialStoreHarness/run.sh
WYNIK: wszystkie kontrole zaliczone
EXITCODE=0
STATUS=0
```

139 kontroli `OK`, 0 `BLAD`. Liczone programatycznie z `run.log`; kod wyjścia 1
dla dowolnej porażki. Żaden komunikat — także przy porażce — nie wypisuje
tokenu, scope ani origin.

Scenariusze: roundtrip całego zestawu; ciphertext bez markerów UTF-8/UTF-16;
literalne Unicode i spacje zachowane bez trim; brak RT + nieznana ważność;
NOWY PROCES Windows czyta zachowany UTC; rotacja RT utrwalona i widoczna w
nowym procesie; brak pliku = `Missing`; uszkodzony ciphertext nie kasuje
danych; nieobsługiwana wersja formatu i nieprzewidziane pole = `Invalid` bez
kasowania; obcy broker odrzucony; złe wejście nie nadpisuje poprzedniego;
PRAWDZIWA blokada pliku → `WriteFailure` i stary rekord nietknięty; jawny
`Delete` idempotentny + `Missing` w nowym procesie; limit rozmiaru i pusty plik
= `Invalid`; `ToString` bez sekretów.

## Mutacje dyskryminujące

Każda wstrzyknięta osobno, potem wycofana. **Wszystkie 7 zabite (exit 1):**

| # | Mutacja | Wynik |
| --- | --- | --- |
| M1 | usunięta kontrola obcego brokera | exit 1 |
| M2 | porzucony plik tymczasowy | exit 1 (`12.brak-porzuconych-tempow`) |
| M3 | nieprzewidziane pola JSON przepuszczone | exit 1 |
| M4 | odczyt kasuje zepsuty plik (wzorzec TIDAL) | exit 1 |
| M5 | `Trim()` na wartości nieprzezroczystej | exit 1 |
| M6 | wymyślony TTL 24 h przy braku `expires_in` | exit 1 |
| M7 | inna entropia domeny przy odczycie | exit 1 |

Uwaga metodyczna: przywrócenie pliku zachowujące mtime sprawia, że MSBuild
używa starej binarki — po wycofaniu mutacji trzeba wymusić przebudowę
(`touch`), inaczej kontrolny przebieg fałszywie świeci na czerwono.

## Pozostałe przebiegi

- `AccessibleMediaController.Core` (Release, WSL): Build succeeded, 0 ostrzeżeń,
  0 błędów.
- `--sonos-refresh-client`: **58/58**, exit 0 (bez zmian).
- `--sonos-login-client`: **36/36**, exit 0 (bez zmian).
- Pełnego Core WSL (6/7 znanych awarii) świadomie nie powtarzano.
- `Windows.SmokeTests` nie uruchamiano (otwierają okna).

## Hasze (z ostatniego przebiegu `sha256.txt`)

```
622e863e305110a4106c18148982737881105ebf9c8d57dd70ecfb0bafa85bb1  Core/Sonos/SonosCredentialStoreContract.cs
6a1e3861eddf521cff3f244baeb614be0cf4d08bd0dcccbbbf265a4f8b905ccb  Windows/Services/SonosDpapiCredentialStore.cs
8056a9778635723ba24c9b3b4e242eb431724e506ed6d90b6296098d18111c5a  tests/SonosCredentialStoreHarness/csharp/Program.cs
710efa22cca75e1a08ec2f182b2337ba98a0ed36c48556ab7534c1f178326c11  SonosCredentialStoreHarness.dll
```

## Czego NIE udowodniono

- **Nie badano przenoszenia między kontami Windows.** Nie tworzono innego
  użytkownika testowego; twierdzenie „blob nie otworzy się na innym koncie"
  NIE jest tu zmierzone. Zmierzone jest to, że blob z INNĄ entropią domeny się
  nie odszyfrowuje (mutacja M7).
- Nie uruchamiano AMC, GUI, NVDA ani żadnego okna; nie używano konta Sonos,
  produkcyjnego brokera ani sieci.
- Nie ma tu koordynatora generacji/wyścigów, pollingu, UI ani sesji Sonos.

## Granice tego etapu

Brak blokad wieloprocesowych, brak CAS i brak ogólnego frameworka plików —
właścicielem operacji będzie JEDNA instancja przyszłego koordynatora.
