# Sonos Control API — kontrakt ODCZYTU domów, grup i głośników

Ustalone u źródła (Sonos), nie z pamięci. Data odczytu dokumentacji: 2026-09-27.

## Źródła

| Co | Adres |
| --- | --- |
| Przewodnik „Control” (ścieżki, nagłówki, odpowiedzi) | https://docs.sonos.com/docs/control |
| getHouseholds + OpenAPI | https://docs.sonos.com/reference/households-gethouseholds |
| getGroups + OpenAPI | https://docs.sonos.com/reference/groups-getgroups-householdid |
| Discover (kolejność odkrywania) | https://docs.sonos.com/docs/discover |

Wersja OpenAPI odczytana z obu stron referencyjnych (pole `info.version`):

* `v1.55.0-1-g8af99d51-production-cloud` (strona getGroups, HTML)
* `v1.56.0-alpha.1-1-gc264f93f-production-cloud` (ta sama definicja, wariant `.md`)

Dokumentacja jest generowana z jednej definicji „Sonos Control API (cloud)”;
różnica dotyczy numeru builda, nie kontraktu dwóch tras, których używamy.

## Adres bazowy i wersja — ROZSTRZYGNIĘTE

`servers` w OpenAPI:

```
{protocol}://api.ws.sonos.com/control/api/{version}
  version: default "v1", enum ["v1"]
  protocol: default "https", enum ["https"]
```

Czyli jedyny dopuszczony adres bazowy to `https://api.ws.sonos.com/control/api/v1`.
Zgadza się to z przewodnikiem „Control”
(`https://{base URL}/v{version}/{target}/{target ID}/{namespace}/{command}`,
base URL = `api.ws.sonos.com/control/api`). **Nie ma w tej definicji V2** —
`v1` nie jest naszym domysłem, to całe `enum` wersji u dostawcy.

Trasy (dokładnie te dwie, tylko GET):

* `GET /households`
* `GET /households/{householdId}/groups`

## Nagłówki — ROZSTRZYGNIĘTE

`securitySchemes` w definicji: `BearerAuth` (http/bearer), `ApiKeyAuth`
(apiKey w nagłówku `X-Sonos-Api-Key`) oraz `LegacyCloudOAuth2`
(authorizationCode, scope `playback-control-all`,
authorize `https://api.sonos.com/login/v3/oauth`,
token/refresh `https://api.sonos.com/login/v3/oauth/access`).

Operacja `Households-GetHouseholds` ma `security`:
`[{ LegacyCloudOAuth2: ["playback-control-all"], BearerAuth: [], ApiKeyAuth: [] }]`
— czyli w JEDNYM wymaganiu, co w OAS znaczy **koniunkcję**: token ORAZ klucz API.

Parametr nagłówkowy, obecny na obu trasach:

```
in: header, name: X-Sonos-Api-Key
description: "Client API Key for the integration. Required in V1 but deprecated in V2."
required: false        (na poziomie parametru)
schema: string, format uuid
deprecated: true, x-muse-deprecated-version: 2.0.0, x-muse-removed-version: 2.0.0
```

Wniosek dla nas: skoro `servers.version.enum` zawiera WYŁĄCZNIE `v1`,
`X-Sonos-Api-Key` jest w praktyce **wymagany**, mimo `required: false`
w schemacie parametru. Dokumentacja jest tu niespójna; klient dla V1 wysyła oba nagłówki według opisu i wymagań security, nie domyśla się obsługi V2.

**Sygnał dla rodzica:** klient przyjmuje klucz jako JAWNY parametr
konfiguracji (`SonosControlApiConfiguration.CreateDefault(apiKey)`).
To PUBLICZNY klucz klienta integracji (ten sam, który idzie w adresie
autoryzacji OAuth), **nie** `client_secret` — sekret zostaje w brokerze.
Wykonawca tego przyrostu nie czytał `.env` serwera ani sekretu i **nie wpisał
żadnej wartości zastępczej**. Wartość musi podać warstwa wyżej przed
pierwszym prawdziwym wywołaniem; bez niej klient zwraca lokalny wynik
`InvalidConfiguration` i **nie wysyła żądania**.

Pozostałe nagłówki dopuszczone przez definicję (`X-Sonos-Corr-Id` — UUID do
korelacji): nie wysyłamy, bo nie są wymagane i nie wnoszą nic do odczytu.

Nagłówki odpowiedzi opisane w przewodniku: `Content-Type: application/json`,
`X-Sonos-Type` (typ obiektu), `X-Sonos-Household-Id`, `X-Sonos-Group-Id`.
Nie opieramy na nich decyzji — kontraktem jest ciało JSON.

## Schematy odpowiedzi — dosłownie z definicji

### `households` (200 dla `GET /households`)

```
households: array of household, maxItems 4, nullable
```

### `household`

| Pole | Typ | maxLength | Wymagane | Uwagi z definicji |
| --- | --- | --- | --- | --- |
| `id` | string | 64 | **tak** (`required: ["id"]`) | „This is the householdId, used in household targeted commands such as getGroups” |
| `name` | string | 1024 | nie, `nullable` | „Some user households might be unnamed, in which case this field is **omitted** from the response” |
| `swVersion` | string | 64 | nie, `nullable` | od 1.42.0 |
| `ownerLuid` | string | 64 | nie, `nullable` | identyfikator konta właściciela |

**Brak nazwy domu jest kontraktowy** — pole może być POMINIĘTE. Nie zastępujemy
go identyfikatorem ani tekstem zastępczym w modelu; `Name` zostaje `null`
i decyzja o prezentacji należy do warstwy UI (której tu nie ma).

`ownerLuid` to identyfikator konta użytkownika. **Nie modelujemy go** — ten
przyrost ma czytać urządzenia, nie tożsamość właściciela.

### `groups` (200 dla `GET /households/{householdId}/groups`)

| Pole | Typ | maxItems | Uwagi |
| --- | --- | --- | --- |
| `groups` | array of `group` | 32 | `nullable` |
| `players` | array of `player` | 32 | `nullable`; „filtered variant of the devices array… only primary players (PLAYBACK capability)”, bez urządzeń w kwarantannie |
| `partial` | boolean | — | `nullable`, od 1.18.1: „whether this is a partial output (where players or groups were dropped)… Invalid groups can appear when the household is in a transient state during a grouping operation” |

Żadne z tych pól nie jest w `required`, więc **pusta lista jest legalnym stanem**
(konto bez podłączonych głośników), a `partial` bywa nieobecne.

### `group`

| Pole | Typ | maxLength/maxItems | Wymagane |
| --- | --- | --- | --- |
| `id` | string | 35 | **tak** |
| `name` | string | 69 | **tak** („Living Room”, „Kitchen + 2”) |
| `coordinatorId` | string | 24 | **tak** (to `playerId` koordynatora) |
| `playerIds` | array of string (24) | 32 | **tak** („This list includes the coordinatorId”) |
| `playbackState` | enum `playbackState` | — | nie, `nullable`; „only sent back in the getGroups response” |
| `areaIds` | array of string (36) | 33 | nie, `nullable`, od 1.14.0 |

`playbackState` = `PLAYBACK_STATE_IDLE | PLAYBACK_STATE_BUFFERING |
PLAYBACK_STATE_PAUSED | PLAYBACK_STATE_PLAYING`.

`areaIds` pomijamy — poza zakresem odczytu domów/grup/głośników.

### `player`

Ponowny bezpośredni odczyt oficjalnej definicji `/reference/groups-getgroups-householdid.md` potwierdził `properties`:

| Pole | maxLength |
| --- | --- |
| `id` | 24 |
| `name` | 64 |
| `softwareVersion` | 64 |
| `apiVersion` | 64 |
| `minApiVersion` | 16 |

Limit nazwy 64 jest ograniczeniem z definicji dostawcy, nie domysłem AMC. Wcześniejszy opis pomijał tę tabelę. Limit `minApiVersion` został skorygowany z nadmiernie liberalnego 64 do 16; nie są to dane używane jako sekret ani adres.

`required` w definicji: `id`, `name`, `websocketUrl`, `softwareVersion`,
`apiVersion`, `minApiVersion`, `capabilities`, `deviceIds`.

Pola **jawnie przeterminowane** w definicji, których **nie modelujemy**:

* `capabilities` — `deprecated: true` od 1.10.0, „use deviceInfo object in devices array”,
* `deviceIds` — „deprecated; use deviceInfo object in devices array”,
* `devices` — `deprecated` od 1.40.0,
* `isUnregistered` — `deprecated` od 1.10.0, „use capabilities (CLOUD) instead”.

Dlatego modelujemy tylko `id`, `name` oraz (opcjonalnie, do diagnostyki
zgodności) `softwareVersion`, `apiVersion`, `minApiVersion`.
`websocketUrl` pomijamy świadomie: to adres lokalnego API urządzenia, a ten
przyrost nie otwiera żadnego dodatkowego połączenia.

## Kody błędów z definicji (te same dla obu tras)

| HTTP | `x-muse-error-codes` |
| --- | --- |
| 400 | `ERROR_INVALID_OBJECT_ID`, `ERROR_INVALID_SYNTAX`, `ERROR_MISSING_PARAMETERS` |
| 401 | `ERROR_NOT_AUTHORIZED` |
| 403 | `ERROR_NO_PERMISSION` |
| 404 | `ERROR_GROUP_CHANGED`, `ERROR_UNSUPPORTED_NAMESPACE`, `ERROR_CMD_FUTURE` |
| 499 | `ERROR_COMMAND_FAILED`, `ERROR_QUEUE_FULL`, `ERROR_NYI` |
| 500 | `ERROR_INTERNAL` |
| 503 | `ERROR_SERVICE_UNAVAILABLE` |

499 to niestandardowy kod Sonos (przewodnik pokazuje go dla `playbackError`).
Mapujemy go osobno, nie jako 4xx „nasz błąd”.

## Czego ten przyrost NIE robi

* żadnego POST/DELETE, tworzenia ani zmiany grup,
* żadnych subskrypcji ani webhooków (`x-webhooks` w definicji zignorowane),
* żadnego `playbackSession`, `audioClip`, `playlists`, sterowania odtwarzaniem,
* żadnego zapisu tokenu — token jest argumentem pojedynczego wywołania,
* żadnego odświeżania tokenu ani kasowania poświadczeń przy 401/403.

## Polityki klienta AMC (nie dodatkowe wymagania Sonosa)

- Token i klucz przekazywane literalnie, tylko niepuste drukowalne ASCII bez spacji i znaków sterujących, maks. 64 KiB na pole nagłówka. To bramka transportu, nie walidator autentyczności tokenu.
- Host/HTTPS/port/ścieżka stałe; nie można skonfigurować obcego hosta. Własny handler HTTP jest zaufanym szwem testowym. Produkcyjny handler nie przekierowuje i nie używa cookies. Każda odpowiedź musi wskazywać wysłany URI; brak RequestMessage lub RequestUri jest odmową. Testowy handler odwzorowuje metadane transportu jawnie. Klient zwalnia wyłącznie handler, który sam utworzył; wstrzyknięty pozostaje własnością wywołującego.
- Identyfikator domu: do 64 znaków, lokalny konserwatywny zestaw ASCII (litery, cyfry, podkreślenie, łącznik, kropka), bez segmentu z samych kropek. OpenAPI nie podaje takiego regexu. Odrzucenie nie stwierdza, że identyfikator jest nieprawidłowy u dostawcy; wynik nie jest wysyłany.
- Cała odpowiedź do 256 KiB, JSON o głębokości do 16; duplikaty nazw pól odrzucone. Pusta lista jest sukcesem. Duplikaty elementów list pozostają, bez cichego scalania.
- Klient mapuje tylko używane pola; nie jest walidatorem całej definicji dostawcy. Pominięte, przeterminowane lub nieużywane pola nie są warunkiem odczytu urządzeń.
- Błędy nie oddają ciała odpowiedzi, nagłówków ani treści wyjątków. Nie wylogowują i nie ponawiają żądania.
- Ten etap nie jest odczytem rzeczywistego konta ani gotowym oknem AMC. Do podłączenia pozostaje właściciel konta, publiczny klucz integracji i dostępna lista w UI.
