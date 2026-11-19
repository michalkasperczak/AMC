# HUBERT #26 — wdrożenie rozdziałów YouTube

## Zakres i identyfikacja

- Baza pracy: `4b5a4a4a07968eae69ba7cbc602d30bbaa07f07e`.
- Czerwony test funkcjonalny: `51f9c9be4999b03f37e410f01d6238ef1ae43ad7`.
- Zatwierdzony kod produktu i testów: `626332908c612bc2177ad498325e8b09af3304cf`.
- Budowanie offline: SDK `8.0.423` z lokalnym cachem NuGet; nie użyto sieci, kont ani GUI.

## RED → GREEN

### RED

Świeży archive commit `51f9c9be4999b03f37e410f01d6238ef1ae43ad7` został odtworzony do:

`C:\Users\Michal\AppData\Local\AMC-Hermes-Build\hubert26-chapters-20261119-red-verified`

Polecenie wybrane:

```text
dotnet run --project tests\AccessibleMediaController.LiteHost.ProtocolTests\AccessibleMediaController.LiteHost.ProtocolTests.csproj -c Release --no-restore -- --podcast-chapters
```

Restore zakończył się kodem `0`; test zakończył się kodem `1`: `Niepowodzenia: 1 z 1`, z asercją, że film YouTube bez opisu ma importować rozdziały twórcy zamiast pustej strony `watch`. Log: `red-1.log` w katalogu stagingu powyżej.

### GREEN

Świeży, pełny archive commit `626332908c612bc2177ad498325e8b09af3304cf` został zbudowany w:

`C:\Users\Michal\AppData\Local\AMC-Hermes-Build\hubert26-chapters-20261119-final2`

Zarchiwizowano i porównano 3054 śledzone pliki. Każdy z dziewięciu krytycznych plików źródłowych został porównany bajt-po-bajcie z blobem commit; ich SHA-256 są w `.amc-hubert26-evidence/source-identity.json`.

Wszystkie poniższe polecenia zwróciły `0`:

| Sprawdzenie | Czas |
|---|---:|
| restore ProtocolTests (lokalny cache, `--ignore-failed-sources`) | 1.766 s |
| restore Windows SmokeTests | 2.165 s |
| restore LiteHost | 1.779 s |
| restore Windows | 2.137 s |
| `ProtocolTests --podcast-chapters` | 7.670 s |
| `Windows.SmokeTests --youtube-chapters-only` | 28.822 s |
| Release build rzeczywistego LiteHost | 6.190 s |
| Release build rzeczywistego konsumenta Windows | 3.708 s |

Logi i pełna lista argumentów są w `.amc-hubert26-evidence/results.json`. Test protokołu raportuje `Wszystkie zestawy przeszly (1)`; obejmuje zwykły RSS, stronę odcinka, provider JSON, routing YouTube, prawidłowy pusty wynik oraz kolejne retry po braku narzędzia, złym JSON, anulowaniu, timeoutcie i przepełnieniu.

Test smoke uruchamia jako `yt-dlp` rzeczywisty offline apphost fixture. Sprawdza dwie poprawne pozycje, `NA`, zły JSON, limit stdout, limit stderr, timeout, cancellation i zakończenie potomnego procesu. Nie wykonuje HTTP ani nie czyta cookies.

## Zmiana

- Rozpoznanie adresu YouTube w koordynatorze rozdziałów kieruje pojedynczy odcinek do `yt-dlp --print %(chapters)j`, nie do pobrania strony `watch`.
- Parser przyjmuje `NA` i pusty wynik jako prawidłowy brak rozdziałów, normalizuje tytuły oraz odrzuca nadmiar JSON (> 1 MiB).
- `BoundedExternalToolProcess` czyta stdout i stderr równolegle z limitami, zachowuje tylko ograniczony tekst diagnostyczny, obsługuje timeout/anulowanie i kończy całe drzewo własnego procesu.
- Błędy startu, niezerowy kod, zły JSON, timeout, anulowanie i limit wyjścia nie są cache'owane; prawidłowy pusty wynik jest cache'owany.
- Wywołanie produkcyjne jest podłączone w `LiteEngineHandlers`, a wspólny runner jest kompilowany zarówno przez LiteHost (link w projekcie), jak i Windows.
- Nieznana surowa linia stderr nie jest już zwracana użytkownikowi jako komunikat rozdziałów.

## Artefakty i SHA-256

- `amc_lite_host.dll`: `1fe3cc93675fb393fbdec90ae4aca45f27d46fbafb05caa4cbd6d2a96591e9e4`
- `AccessibleMediaController.dll`: `527dbc3b976fb331b1b6f2f7269412cc01e24ee1c35b5a8f82c7a52f6e8a7e68`

Źródłem obu wartości jest finalny `.amc-hubert26-evidence/artifact-sha256.json`.

## Uwagi

Pierwszy próbny archive (`9046372611d2ca8db35daea9d1067d7035f7e1e6`) ujawnił błąd kompilacji LiteHost: grupa metod z opcjonalnym czwartym parametrem nie konwertuje się do trzyargumentowego `Func`. Zastąpiono ją jawną lambdą, poprawkę włączono do `6263329`, po czym wykonano powyższe świeże GREEN.

Nie wykonano push, merge, komentarzy GitHub, instalatora ani uruchomienia GUI.
