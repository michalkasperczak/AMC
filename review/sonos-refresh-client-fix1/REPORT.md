# Naprawa: wadliwe Unicode w odpowiedzi /login/refresh

Gałąź `hermes/sonos-refresh-client-after410`, baza `093d548`.
SDK: `/home/michal/dotnet/dotnet` **8.0.425**, budowa `/m:1`.

## Zmierzony błąd (stan przed naprawą)

Odpowiedź brokera zawierająca **escapowany samotny surogat UTF-16** w polu
tekstowym (`access_token`, `refresh_token`, `token_type`, `scope`, `error`)
parsowała się jako dokument JSON poprawnie, ale `JsonElement.GetString()`
rzucał `InvalidOperationException` w górę z `RefreshAsync`:

- `Cannot read incomplete UTF-16 JSON text as string with missing low surrogate.`
- `Cannot read invalid UTF-16 JSON text as string. Invalid surrogate value: '0xDC00'.`

Wyjątek zamiast wyniku oznacza, że warstwa wyżej nie dostaje `InvalidResponse`,
a niezgodna odpowiedź 401 nie może być bezpiecznie sklasyfikowana.

Dowód niezależny (probe rodzica, pojedyncze pola, bez zduplikowanych kluczy):
`/home/michal/projekty/amc_pomoc/sonos-refresh-client-parent1/probe-single-fields/red-result.json`
— exit 1, 5× `pass: false`.

## Zmiana w produkcji (minimalna)

`src/AccessibleMediaController.Core/Sonos/SonosLoginClient.cs`, tylko `RefreshAsync`:

1. Wywołanie `MapRefreshFailure(read)` (ścieżka nie-200, w tym 401) objęte wąskim
   `try/catch (InvalidOperationException)` → `InvalidResponse`.
2. Blok dekodowania ciała 200 (`using (document) { ... }`) objęty tym samym wąskim
   `catch (InvalidOperationException)` → `InvalidResponse`.

Nic poza tym: transport, `SendAsync`, deadline ciała, anulowanie i mapowanie
transportowe pozostają **poza** blokami `catch`. `StartAsync`/`FetchResult`,
broker, globalna konfiguracja JSON i `TryParseObject`/`ReadString` bez zmian.
Żaden generyczny parser nie powstał. Treść ciała ani tekst wyjątku nie są nigdzie
wypisywane.

Skutek: `Status == InvalidResponse`, `Tokens == null`,
`RequiresReauthorization == false`. Kasowania poświadczeń domaga się nadal
**wyłącznie** 401 z kontraktowym `reauthorization_required`.

## TDD

`tests/.../SonosRefreshClientTests.cs`, 8 nowych przypadków (R-51..R-58), każdy
liczony osobno. Fixture'y to **surowy tekst JSON** (`"\ud800"`, `"\udc00"`) —
`JsonSerializer` podmieniłby zły znak na U+FFFD i przypadek przestałby istnieć;
każdy negatywny przypadek dodatkowo potwierdza, że `JsonDocument.Parse` ciała
**przechodzi** (błąd jest dopiero w dekodowaniu).

- R-51..R-54 — samotny wysoki surogat w `access_token`/`refresh_token`/`token_type`/`scope` (200)
- R-55 — samotny wysoki surogat w `error` przy 401, `RequiresReauthorization == false`
- R-56 — samotny niski surogat z tekstem wokół, 4 pola + `error` przy 401
- R-57 — kontrola POZYTYWNA: poprawna para surogatów (`\ud83c\udfb5`) w `scope` → `Success`, `Scope` bajt w bajt
- R-58 — kontrola POZYTYWNA: kontraktowe 401 `reauthorization_required` nadal ustawia flagę

### RED (produkcja niezmieniona)

`Sonos odnawianie: nieprzeszle przypadki: 6/58` — dokładnie R-51..R-56,
z komunikatami `Cannot read ... UTF-16 ...`. R-57 i R-58 już wtedy zaliczone,
więc testy różnicują naprawę, a nie tylko sygnalizują zmianę.

### GREEN

- `Sonos odnawianie: 58/58 przypadkow zaliczonych.`
- `Sonos: 36 testow klienta pierwszego logowania zaliczonych.` (regresja logowania)
- budowa: `0 Warning(s), 0 Error(s)`
- probe rodzica po naprawie: `PROBE_EXIT=0`, 5× `pass: true`

## Granice tego pomiaru

- Cały transport jest **syntetyczny** (`HttpMessageHandler`-atrapa). Żadnego
  kontaktu z prawdziwym brokerem, kontem Sonos, siecią ani poświadczeniami.
- Nie uruchamiano pełnego zestawu Core — pozostałe znane awarie WSL (6/7,
  edycja nagrań, TIDAL, pliki lokalne) były już zmierzone przez rodzica i nie
  dotyczą tej zmiany.
- Brak publikacji: tylko lokalny commit wybranych plików.
