# NVDA-END-20261119 — parametry audio przed nazwą materiału

Status: **Zrobione w źródłach i testach celowanych; bez instalacji oraz żywego odbioru NVDA.**

## Cel i kontrakt

Zgłoszenie użytkownika z 19.11.2026: `NVDA+End` ma mówić najpierw potwierdzone parametry audio, a nazwę bieżącego pliku lub materiału na końcu.

- Dla lokalnych Plików, Podcastów, YouTube i Radio Archiwisty gotowy status hosta ma postać: dostępne parametry audio, potem niezmieniona nazwa materiału.
- Przy braku wszystkich parametrów gotowy status zawiera wyłącznie nazwę materiału; przy części danych zawiera wyłącznie tę część.
- TIDAL zachowuje opis jakości źródła, bitrate i PCM silnika; wszystkie te informacje poprzedzają tytuł.
- Spotify zachowuje istniejącą deklarację jakości Web lub uczciwy komunikat Soloist; informacja poprzedza tytuł. Nie zmieniono pomiaru ani deklarowanej jakości.
- Stan `Nic nie jest odtwarzane` i radio `currentBroadcastText` pozostają bez zmiany.
- Zmiana nie dotyczy kolejności list, lewej strzałki, `Alt+D`, dodatku NVDA ani ustawień GUI.

## Stan zastany i przyczyna

Baza zadania: `0b522c000335c7476e8e2cd6b4b35c2121e65d22` na gałęzi `codex/amc-nvda-end-order-20261119`.

`LiteEngineHandlers.Status` składał dla nieradiowego odtwarzania `tytuł, parametry`. Osobne ścieżki TIDAL i Spotify również zaczynały od tytułu. Python `LiteFrame._update_native_playback_status` przekazuje gotowe `currentPlaybackText` do natywnego paska statusu bez ponownego formatowania lub odgadywania bitrate'u.

Dodano jeden współdzielony formatter `LitePlaybackStatusText`, wywoływany przez rzeczywistych producentów statusu Plików/Podcastów/YouTube/Archiwisty, TIDAL i Spotify. Nie zmieniono `AudioParametersFormatter`.

## RED → GREEN

### RED

Przed zmianą uruchomiono rzeczywisty producent tekstu TIDAL:

```text
/home/michal/dotnet/dotnet run --no-restore --project \
  tests/AccessibleMediaController.LiteHost.ProtocolTests/AccessibleMediaController.LiteHost.ProtocolTests.csproj \
  -- --tidal-native
```

Wynik: exit `1`. Nowy test oczekiwał:

```text
źródło Max, FLAC 24 bit, 192 kHz, 5612 kb/s, PCM w silniku 192 kHz, Utwór, 192 kb/s (nie parametr)
```

Zastany wynik zaczynał się od `Utwór, 192 kb/s (nie parametr)`, więc test odtworzył dokładnie niewłaściwą kolejność bez utraty przecinków i tekstu wyglądającego jak parametr w samym tytule.

### GREEN i sąsiednie kontrakty

```text
/home/michal/dotnet/dotnet build --no-restore \
  tests/AccessibleMediaController.LiteHost.ProtocolTests/AccessibleMediaController.LiteHost.ProtocolTests.csproj \
  --configuration Release --verbosity minimal
```

Wynik: `Build succeeded`, `0 Warning(s)`, `0 Error(s)`.

```text
/home/michal/dotnet/dotnet \
  tests/AccessibleMediaController.LiteHost.ProtocolTests/bin/Release/net8.0/amc_lite_protocol_tests.dll \
  --tidal-native --remote-media-bitrate --spotify-profile
```

Wynik: `TidalNativePlayRequestTests: OK`, `RemoteMediaBitrateTests: OK`, `Wszystkie zestawy przeszly (3).`

Nowy test bezokienny obejmuje: pełny rzeczywisty tekst TIDAL, tytuł z przecinkiem i pozornym bitrate'em, jeden dostępny parametr, brak parametrów oraz zachowanie deklaracji Spotify przed nazwą. Współdzielona ścieżka lokalna obejmuje Pliki, Podcasty, YouTube i Archiwistę.

Sprawdzono także bez edycji GUI konsument gotowego tekstu Pythonem z atrapą wx:

```text
PYTHONPATH=wxlite:wxlite/tests python3 <jednorazowa sonda>
```

Wynik: `Python ready-text status consumer: OK`; `currentPlaybackText` z nową kolejnością trafił niezmieniony do paska statusu.

## Zmienione pliki

- `src/AccessibleMediaController.LiteHost/Protocol/LitePlaybackStatusText.cs`
- `src/AccessibleMediaController.LiteHost/LiteEngineHandlers.cs`
- `src/AccessibleMediaController.LiteHost/Protocol/LiteTidalNativePlayRequest.cs`
- `src/AccessibleMediaController.LiteHost/LiteSpotifyPlaybackCoordinator.cs`
- `tests/AccessibleMediaController.LiteHost.ProtocolTests/TidalNativePlayRequestTests.cs`
- `docs/zadania/NVDA-END-20261119-parametry-przed-nazwa.md`

## Ograniczenia

Nie uruchamiano GUI, instalatora, aplikacji ani dodatku NVDA; nie ma żywego odsłuchu `NVDA+End`.

Kompilacja rzeczywistego projektu LiteHost na Linuxie nie została wykonana: offline restore zakończył się `NU1100` dla nieobecnych Windows reference packs `Microsoft.WindowsDesktop.App.Ref 8.0.31` i `Microsoft.Windows.SDK.NET.Ref 10.0.19041.56`. Nie pobierano ich ani nie wykonywano buildu Windows. Protokół C# (net8.0) z tym samym plikiem formatera i rzeczywista ścieżka TIDAL zostały skompilowane oraz uruchomione.

## Wersja i commit

- Baza: `0b522c000335c7476e8e2cd6b4b35c2121e65d22`.
- Commit zadania: uzupełniony po zatwierdzeniu.
- Instalacja, publikacja i release: nie wykonano.

## Materiał do dokumentacji użytkownika

W odtwarzanym pliku lub materiale naciśnij `NVDA+End`, aby usłyszeć gotowy pasek stanu. AMC podaje najpierw dostępne parametry dźwięku, na przykład bitrate i częstotliwość lub znaną jakość usługi, a potem nazwę materiału. Gdy źródło nie udostępnia parametrów, AMC podaje samą nazwę i nie zgaduje brakujących wartości.
