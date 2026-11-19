# Observed material identity — status hosta Lite

## Zakres wykonany

Host Lite zwraca opcjonalne pola migawki:

- `observedMaterialId`
- `identityProvenance`

Są to pola obserwacyjne: oba są `null`, jeżeli host nie ma jednoznacznego dowodu. Nie zmieniają istniejącego `itemId` grupy/urządzenia.

### WiiM

`LiteWiiMContract` najpierw normalizuje istniejący stan odtwarzania WiiM, a dla
`odtwarzanie` albo `pauza` normalizuje rzeczywiste `playback.ContentUri`
istniejącą polityką URI i porównuje je z zapisanymi `networkStreams`.

- pojedyncze dopasowanie: `wiim-stream:<id>`, `uriMatch`;
- zatrzymanie, ładowanie, stan nieznany, brak URI, proxy/redirect, brak
  dopasowania albo wiele zapisów o tym samym znormalizowanym URI: oba pola
  `null`.

Tytuł, preset, zapamiętany wybór i stan urządzenia nie są źródłem tożsamości.

### Sonos

`LiteSonosCoordinator` bierze aktualny `ItemId` z odczytu
`ReadGroupPlaybackAsync` dla wybranego household/group. Dla odtwarzania albo
potwierdzonej pauzy (`Playing`/`Paused`) porównuje go z wyliczonym przez
`SonosOwnStreamIdentity` identyfikatorem zapisanych własnych streamów.

- jedno dopasowanie: `sonos-stream:<id>`, `playbackItemId`;
- idle/buffering/nieznany stan, brak `ItemId`, nieznany/mismatched item albo
  niedostępny profil lokalny: oba pola `null`.

Wynik `LoadStreamUrlAsync`, nazwa, favorites i playlisty nie są używane jako zastępcze źródło tożsamości.

## Przykładowe payloady (bez URL-i)

```json
{"itemId":"wiim:device-1","observedMaterialId":"wiim-stream:saved-a","identityProvenance":"uriMatch"}
```

```json
{"itemId":"sonos:home-1:group-1","observedMaterialId":"sonos-stream:stream-b","identityProvenance":"playbackItemId"}
```

```json
{"observedMaterialId":null,"identityProvenance":null}
```

## Pliki

- `src/AccessibleMediaController.LiteHost/Protocol/LiteWiiMContract.cs`
- `src/AccessibleMediaController.LiteHost/LiteWiiMCoordinator.cs`
- `src/AccessibleMediaController.LiteHost/Protocol/LiteSonosContract.cs`
- `src/AccessibleMediaController.LiteHost/LiteSonosCoordinator.cs`
- `tests/AccessibleMediaController.LiteHost.ProtocolTests/WiiMContractTests.cs`
- `tests/AccessibleMediaController.LiteHost.ProtocolTests/SonosContractTests.cs`
- `tests/AccessibleMediaController.LiteHost.ProtocolTests/SonosObservedMaterialCoordinatorTests.cs`
- `tests/AccessibleMediaController.LiteHost.ProtocolTests/ObservedMaterialFixtureExport.cs`
- `tests/AccessibleMediaController.LiteHost.ProtocolTests/ObservedMaterialFixtureExportTests.cs`

## Wspólny eksport fixture C# → Python

Opcjonalna flaga testowego runnera przyjmuje ścieżkę pliku i nie uruchamia
całego procesu testowego: wymaga jawnego wyboru czterech zestawów związanych z
tym kontraktem. Dopiero po ich zielonym wyniku uruchamia rzeczywiste wywołania
`CreateNetworkStreamsResult` / `CreateSnapshotResult` oraz
`CreateOwnStreamsResult` / `CreateSnapshotResult` i zapisuje fixture.

Wspólny schemat ma jeden korzeń:

```json
{"cases":[{"name":"sonos-playing","session":"SONOS","deviceId":null,"householdId":"home-1","groupId":"group-1","listPayload":{},"snapshot":{},"expectedMaterialId":"sonos-stream:stream-a"}]}
```

Każdy przypadek ma wszystkie wymienione pola. `deviceId` należy do WiiM;
`householdId` i `groupId` do Sonosa, a niewłaściwe dla sesji pole jest `null`.
`listPayload` oraz `snapshot` są bezpośrednimi wynikami mapperów C#, natomiast
`expectedMaterialId` jest osobnym, deklarowanym oraklem — nie jest kopiowany do
pól obserwowanych migawki.

Fixture obejmuje dla obu sesji: `playing`, `paused`, `idle`, `mismatch` i
`ambiguous`. Używa wyłącznie fikcyjnych ustawień i adresów `example.invalid`;
nie wykonuje połączeń sieciowych ani nie czyta profilu użytkownika. Adresy w
`listPayload` pozostają celowo obecne, ponieważ prawdziwy mapper przekazuje je
do jawnego polecenia kopiowania.

Dokładna komenda produkcji fixture:

```text
/home/michal/dotnet/dotnet run --project tests/AccessibleMediaController.LiteHost.ProtocolTests/AccessibleMediaController.LiteHost.ProtocolTests.csproj --no-restore -- --wiim --sonos --sonos-observed-material --observed-material-fixture-contract --observed-material-fixtures /home/michal/.hermes/profiles/amc/cache/scratch/amc-observed-material-contract-20261119.json
```

Wykonany eksport zapisał wskazany plik. Odczyt zwrotny JSON potwierdził 10
przypadków: pięć WiiM i pięć Sonos; we wszystkich rzeczywiste pole
`snapshot.observedMaterialId` zgadzało się z niezależnym
`expectedMaterialId`.

## Weryfikacja

RED: przed poprawką stanów, celowany proces testowy zakończył się kodem 1 z
dwoma rozpoznawalnymi błędami: zatrzymany WiiM nadal zachowywał tożsamość
strumienia, a wstrzymany Sonos ją gubił.

GREEN:

```text
/home/michal/dotnet/dotnet run --project tests/AccessibleMediaController.LiteHost.ProtocolTests/AccessibleMediaController.LiteHost.ProtocolTests.csproj --no-restore -- --wiim --sonos --sonos-observed-material --observed-material-fixture-contract --observed-material-fixtures /home/michal/.hermes/profiles/amc/cache/scratch/amc-observed-material-contract-20261119.json
Wyeksportowano fixture obserwowanego materiału: /home/michal/.hermes/profiles/amc/cache/scratch/amc-observed-material-contract-20261119.json
Wszystkie zestawy przeszly (4).
```

Testy używają wyłącznie fixture i fake transportu Sonos; nie kontaktują urządzeń sieciowych.

## Pozostałe granice

Nie utworzono nowego silnika kolejki ani translacji playlist/favorites: dla takiego materiału brak potwierdzonego własnego streamu pozostaje `null`. Fixture jest kontrolą kontraktu mapperów, a nie testem żywego sprzętu, GUI ani Pythona. Nie uruchamiano pełnego Windows LiteHost: środowisko WSL nie ma wymaganych Windows reference packs (`8.0.31` i `10.0.19041.56`), a znany `NU1100` nie był ponawiany ani nie pobierano referencji.
