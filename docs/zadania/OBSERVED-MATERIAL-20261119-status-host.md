# Observed material identity — status hosta Lite

## Zakres wykonany

Host Lite zwraca opcjonalne pola migawki:

- `observedMaterialId`
- `identityProvenance`

Są to pola obserwacyjne: oba są `null`, jeżeli host nie ma jednoznacznego dowodu. Nie zmieniają istniejącego `itemId` grupy/urządzenia.

### WiiM

`LiteWiiMContract` normalizuje rzeczywiste `playback.ContentUri` istniejącą polityką URI i porównuje je z zapisanymi `networkStreams`.

- pojedyncze dopasowanie: `wiim-stream:<id>`, `uriMatch`;
- brak URI, proxy/redirect, brak dopasowania albo wiele zapisów o tym samym znormalizowanym URI: oba pola `null`.

Tytuł, preset, zapamiętany wybór i stan urządzenia nie są źródłem tożsamości.

### Sonos

`LiteSonosCoordinator` bierze aktualny `ItemId` z odczytu `ReadGroupPlaybackAsync` dla wybranego household/group. Dla odtwarzania (`Playing`) porównuje go z wyliczonym przez `SonosOwnStreamIdentity` identyfikatorem zapisanych własnych streamów.

- jedno dopasowanie: `sonos-stream:<id>`, `playbackItemId`;
- idle, brak `ItemId`, nieznany/mismatched item albo niedostępny profil lokalny: oba pola `null`.

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

## Weryfikacja

RED: po dodaniu asercji pól obserwowanych, przed implementacją, celowany proces testowy zakończył się kodem 1. Runner nie wypisał wtedy diagnostyki tekstowej.

GREEN:

```text
/home/michal/dotnet/dotnet run --project tests/AccessibleMediaController.LiteHost.ProtocolTests/AccessibleMediaController.LiteHost.ProtocolTests.csproj --no-restore -- --wiim --sonos --sonos-observed-material
Wszystkie zestawy przeszly (3).
```

Testy używają wyłącznie fixture i fake transportu Sonos; nie kontaktują urządzeń sieciowych.

## Pozostałe granice

Nie utworzono nowego silnika kolejki ani translacji playlist/favorites: dla takiego materiału brak potwierdzonego własnego streamu pozostaje `null`. Nie uruchamiano pełnego Windows LiteHost: środowisko WSL nie ma wymaganych Windows reference packs (`8.0.31` i `10.0.19041.56`).
