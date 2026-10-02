# AMC — mapa kodu

## Sonos po415: treść, fokus i krótka reakcja

- `MainWindow.SonosLibrary.cs` / `SonosLibraryPresentation.IsContentRootView`: korzeń pokazuje kategorie materiału; model grup pozostaje osobno dla Ctrl+F5 i transportu. Enter otwiera istniejącą kategorię. `MainWindowNavigationPolicy` zamienia domyślne Multimedia na Bibliotekę Sonosa.
- Brama okien rozpoznaje własny proces, ale nie uznaje nieznanego pierwszego planu za własny.
- `SonosSessionPresentation`: canStop pozwala zatrzymać radio przez pause; odczyt Idle potwierdza zatrzymanie.
- `MainWindow.SonosPresets.cs`: nazwa wybranego materiału pada przed odczytami sieci, raz; błędy nadal ogłaszane. Nie wymusza powrotu z filtra na listę. `MainWindow.SonosOwnStreams.cs` podaje krótkie Uruchamianie.
- `SonosOwnStreamsWindow`: F2 i Delete używają tych samych dróg co przyciski Edytuj/Usuń. Sonos Favorites nie zyskały nieistniejącego API edycji.


## Sonos: rzeczywisty identyfikator sesji z @

`SonosSessionIdPolicy` dopuszcza literalne `@` wewnątrz segmentu ścieżki. Rzeczywisty createSession oddał45/46znakowy identyfikator zawierający ten znak; poprzednia walidacja odrzucała go mimo HTTP200 i Connected. Nie zmieniono wartości identyfikatora, limitu46 ani blokad separatorów/fragmentu/query. Po poprawce rzeczywiste create, loadStreamUrl i suspend dały200; GET potwierdził Playing, echo itemId i końcowy Idle, bez cloud queue servera. Nagrywanie w AMC nie było zatrzymywane. Oddzielić ten pomiar od fizycznego potwierdzenia dźwięku.


## Sonos: tożsamość materiału Ulubionych (resource.id / container.id) — tylko Core

Przyrost Core domyka brakujące ogniwo rozpoznania materiału: do tej pory Core czytał z Ulubionych wyłącznie `id`/`name`/`service`, a z metadanych kontenera `name`/`type`/`service`, więc nie było czym porównać wpisu z tym, co grupa ma załadowane. Rzeczywisty pomiar odpowiedzi usługi (parent, tylko GET) pokazał, że `favorites.items[].resource.id` oraz `playbackMetadata.container.id` niosą tę samą trójkę `serviceId`/`objectId`/`accountId` (schema `universalMusicObjectId`). Wcześniejszy werdykt sondy „brak kandydata” porównywał tylko identyfikator wiersza katalogu i to ograniczenie sondy, nie właściwość API.

- `Sonos/SonosResourceIdentity.cs`: `SonosResourceIdentity` (niemutowalna trójka) + `SonosResourceIdentityLimits` (objectId 256, serviceId 20, accountId 128 — wprost z definicji OpenAPI). `TryCreate` odrzuca pustkę i przekroczony limit jako brak tożsamości. `IsComplete` wymaga wszystkich trzech pól; `Matches`/`AreSameMaterial` porównują **wyłącznie** te trzy pola `StringComparison.Ordinal`. Nazwa, rodzaj, `service.name`, `favorite.id`, wersja kolejki ani kod 200 nie wchodzą do porównania. Celowo brak `Equals`/`GetHashCode` — zgodność liczy tylko `Matches`.
- `SonosFavorite.ResourceIdentity` i `SonosMetadataContainer.Identity` (oba nullable, dodane jako opcjonalne parametry konstruktorów — starsze wywołania i fixtures bez nowych pól działają bez zmian).
- `SonosControlApiClient.Favorites.cs`: `ReadFavoriteResourceIdentity` i wspólny `ReadResourceIdentity` traktują tożsamość jako opcjonalne uzupełnienie. Nieobsługiwany typ lub przekroczony limit pola wyłącza tylko rozpoznanie, nie listę/metadane. Zachowane są odmowy zduplikowanego JSON, wadliwych odczytywanych ciągów UTF-16 i nadmiernej głębokości. `GroupPlayback.cs` używa tego samego helpera; nie ma drugiego parsera JSON.
- Brak `resource` lub `container.id` (starsze odpowiedzi) nie psuje listy ani odczytu metadanych; niekompletna trójka wyłącza rozpoznanie tylko tej pozycji i nigdy nie działa jak wieloznacznik. Wartości zachowane literalnie — bez trim, zmiany wielkości liter i normalizacji URI.
- Prywatność: identyfikatory (zwłaszcza `accountId`) żyją w RAM; `ToString` podaje jedynie obecność i kompletność pól. Nic nie trafia do AppSettings, cache ani logów.
- Windows korzysta z tej tożsamości w presetach Ulubionych — patrz niżej. Natywne playlisty Sonosa (`getPlaylists`) w zmierzonej odpowiedzi nie mają pola `resource`, więc ich powtórzenie pozostaje nierozwiązane. Dom, konto i cel to osobne bramki warstwy Windows.


## Sonos: presety materiału — obsługa UI odebrana, ograniczenie powtórzeń pozostaje

Aktualizacja odbioru: Ctrl+Alt+Shift+P działa fizycznymi klawiszami w trzech listach po obsłudze Key.System/SystemKey. Przypisanie wiąże materiał z kontekstem otwarcia listy i odrzuca zapis po zmianie domu/konta. Klucz porównania w dialogu zawiera rodzaj, dom i ID; sam zapis zachowuje dosłowny identyfikator. Windows 17 przypadków zaliczone. Żywy NVDA: checkbox i zapis stałego miejsca, usunięcie, konflikt zajętego slotu, anulowanie i właściwy wiersz, lista Ctrl+Alt+P, uruchamianie Ctrl+Shift+cyfra. Własna stacja przy zgodnym odczycie mówi samą nazwę (0 POST), z Idle i kontenerem wznawia (1 Play). To pomiar na danych próbnych, nie dźwięku Sonosa.

- `SonosPresetContract.cs`: rodzaje favorite/playlist/own-stream, dokładny stały zestaw logicznych playerIds i dom, klucz własnej stacji zależny od ID i URL, decyzja powtórzenia ze świeżego odczytu.
- `SessionPresetEntry`, normalizacja i clone zachowują `SonosHouseholdId` / `SonosFixedPlayerIds`; opaque ID favorite/playlist bez obcinania. Domyślnie bieżący cel; brak stałego zestawu nie powoduje przegrupowania.
- `MainWindow.SonosPresets.cs`: callbacki przypisywania z trzech list do `RadioPresetAssignmentWindow`, uruchomienie przez istniejące zaplecze. Własna stacja: zgodny itemId+Playing/Buffering daje tylko nazwę; Paused/Idle z kontenerem jest ponownie sprawdzany przed Play. Błąd odczytu, zmiana konta lub niepełna topologia nie uprawnia do przejęcia grupy.
- `MainWindow.SonosPresets.cs`, `DecideSonosFavoriteRepeatAsync`: osobna gałąź dla rodzaju favorite. Czyta ŚWIEŻY katalog domu z presetu (`ReadFavoritesAsync`), dopasowuje dosłowny `TargetId`, bierze `ResourceIdentity`, potem świeże `ReadGroupMetadataAsync` + `ReadGroupPlaybackAsync` właściwej grupy i porównuje PEŁNE trójki (`AreSameMaterial`). Playing/Buffering → sama nazwa i 0 POST; Paused/radio Idle z kontenerem → ponowny odczyt metadanych i dokładnie jeden `SendGroupCommand(Play)`. Niepełna lub nieznana tożsamość, błąd GET i brak pozycji w katalogu kończą się Unavailable bez POST — nigdy restartem w ciemno. Strażnik `Current` w całym wywołaniu wiąże także dom z chwili startu, również przed mową i przywróceniem fokusu. Stan odtwarzania jest ujęty między dwoma odczytami metadanych dla KAŻDEJ decyzji: zmiana źródła blokuje także fałszywe ogłoszenie nazwy oraz niepotrzebny load, nie tylko wznowienie.
- Odbiór częściowy: Windows `--sonos-presets-real` 16 przypadków oraz Core `--sonos-preset-model` 12. Potwierdzono prawdziwy dialog przypisania i zapis/odczyt, a nie ręczne wstawienie rekordu jako dowód działania okna.
- Powtórzenie ULUBIONYCH nie wykonuje już load: zmierzone `--sonos-favorite-repeat` 14/14 i żywym NVDA (0 POST gdy gra, 1 Play po pauzie). OTWARTE zostaje powtórzenie NATYWNEJ playlisty Sonosa — `getPlaylists` nie oddaje `resource`, gałąź jawnie nieukończona. Dźwięk na prawdziwym Sonosie nadal nie zmierzony.


## Sonos: wybór składu głośników pod Ctrl+F5

- `SonosTargetSelectionWindow` zachowuje wybór istniejącej grupy przez Ustaw jako cel. Osobne Wybierz głośniki przekazuje intencję po zamknięciu okna; `MainWindow.SonosLibrary.cs` uruchamia nową drogę bez fałszywego komunikatu anulowania.
- `SonosSpeakerSelectionWindow.xaml(.cs)` to prawdziwe pola CheckBox logicznych graczy: strzałki, Spacja, Zaznacz wszystkie, Zastosuj i anulowanie. Wybór lokalny nie wysyła poleceń; stereo/kino to jedna logiczna pozycja, nie lista deviceIds.
- `SonosSpeakerSelectionContract.cs` wylicza pełny zestaw i konflikty. Ten przepływ zawsze używa `setGroupMembers`, także gdy dawny koordynator wypada. Nie kopiuje muzyki przez `createGroup` ani nie wysyła dodatkowego Play/Stop.
- `MainWindow.SonosSpeakers.cs`: świeży GET przed oknem i zapisem, porównanie składu z otwarciem, pytanie o grające/nieznane źródła, ponowny GET po pytaniu, jeden zapis przez wspólną bramkę. `ISonosGroupMembershipSessionBackend` i owner tylko przekazują do odebranego Core.
- Wynik jest publikowany dopiero po świeżym, pełnym GET z dokładnym zestawem i zwróconym/odczytanym jednoznacznie groupId. Zmiana konta/domu/celu unieważnia stare komunikaty. Niepełny GET lub jego błąd nie jest sukcesem ani dowodem niewysłania już wykonanego POST. Odczyt członkostwa nie dowodzi ciągłości dźwięku.
- Odbiór: Windows `--sonos-speaker-selection` (9 przypadków) oraz `--sonos-navigation-ux`. Końcowe pola i komunikaty zmierzone żywym NVDA na danych próbnych. Realny Sonos i pełny gate wydania pozostają osobno.


## Sonos: ZMIANA SKŁADU GRUP — utworzenie grupy i nowy zestaw głośników, warstwa Core (bez UI)

**Tylko Core.** Ten przyrost dodaje **dwie** operacje składu grupy:
`createGroup` (nowa grupa z podanych głośników) i `setGroupMembers` (**pełny**
nowy zestaw istniejącej grupy). **Nie ma** tu okna, skrótu, checkboxów,
algorytmu planowania grup, odczytu konfliktów, presetów ani subskrypcji
topologii. Okno „Wybierz głośniki” pod `Ctrl+F5` to **następny** krok i to on
wybierze operację: `createGroup` (z `musicContextGroupId` aktualnej grupy, by
zachować materiał) albo `setGroupMembers` (gdy aktywna grupa ma zostać).

Operujemy na **logicznych graczach** (`playerId`), **nie** na fizycznych
`deviceId`: para stereo i kino domowe pozostają **jedną** jednostką — ten
przyrost **nie obiecuje** ich rozdzielania.

- `SonosGroupMembershipContract.cs` — niemutowalny kontrakt: `SonosGroupMembershipRequest`
  (zestaw głośników + opcjonalny `musicContextGroupId`), `SonosGroupMembershipOutcome`
  (etykieta PL + `Sent` + `groupId` **z odpowiedzi**) oraz interfejs
  `ISonosGroupMembershipApi` z **dwiema** metodami. Limity z **rzeczywistej**
  definicji: 32 pozycje, 24 znaki w jednostkach **UTF-16** — nie dziedziczymy
  hipotezy „24 ASCII”. Pusty zestaw dla `setGroupMembers` odrzucamy **u siebie**:
  definicja dopuszcza `null`, ale **nasz** workflow wymaga jawnego pełnego
  zestawu i to jest **nasza** polityka, nie dopisek do kontraktu Sonosa.
- `SonosControlApiClient.GroupMembership.cs` — oba `POST` idą przez **istniejący**
  `WriteCoreAsync` (opcjonalne czytanie ciała) i wspólny `ReadLimitedJsonAsync`:
  bez nowego `HttpClient`, bez drugiego OAuth; host, `Bearer`, odmowa
  przekierowań, typ treści, budżet odpowiedzi, ścisłe UTF-8, duplikaty i limit
  zagnieżdżenia zachowane. URI domu buduje **istniejąca** polityka
  `SonosHouseholdIdPolicy`, nie `TryGroupUri`. Identyfikatory głośników to
  **wartości JSON** — wysyłane **dosłownie**, bez wzorca ze ścieżki `groupId`.
  Odmowa wejścia = **zero** `Send`. `200` **bez** rozpoznanego obiektu grupy
  **nie** jest potwierdzeniem składu i **nie** daje identyfikatora.
- `SonosAccountCoordinator.GroupMembership.cs` — dwa minimalne opakowania na
  **istniejącej** bramce `RunSessionWriteAsync<TOutcome, TResult>`: ten sam
  generyczny mechanizm pilnuje generacji konta i biletu odczytu dla `POST`.
  **Przed** wysłaniem możliwe odnowienie wygasłego dostępu; **po** `401`
  **zero** powtórzeń i odnowień. Zmiana konta w trakcie trzymanego `POST` →
  wynik **odrzucony**, `groupId` **nie** publikowany staremu kontu (konto
  **nie** jest kasowane). Anulowanie **po** wysłaniu nie jest obietnicą cofnięcia.
- Zwracamy **faktyczny** `groupId` z odpowiedzi (może być **inny** lub nowy).
  `HTTP 200` to **przyjęcie polecenia**, nie dowód dźwięku ani składu —
  zwłaszcza gdy odpowiedź jest nierozpoznana. Etykiety statusu są **po polsku**,
  bez surowych powodów dostawcy i bez poświadczeń.
- Odbiór: Core `--sonos-group-membership` (12 przypadków, 112 sprawdzeń);
  sąsiednie `--sonos-stream-url` 12/12 i `--sonos-group-playback` 79/79 bez zmian.
  UI wyboru głośników, właściciel okna i realny Sonos to osobne bramki.

## Sonos: własne stacje zapisane w AMC

- `SonosSessionSettings.OwnStreams` / `SonosOwnStreamSettings` (`AppSettings.cs`) przechowują ID, nazwę i dosłowny URL. `ConfigurationStore.NormalizeSonos` odtwarza pustą listę starego formatu i usuwa powtórzone ID; wybór konta/grupy nie kasuje lokalnych stacji.
- `SonosLibraryPresentation` dodaje kategorię „Moje stacje”. `MainWindow.SonosLibrary.cs` kieruje ją do `MainWindow.SonosOwnStreams.cs`.
- `SonosOwnStreamsWindow.xaml(.cs)` udostępnia listę, Odtwórz, Dodaj, Edytuj, Usuń i Zamknij. `RadioStationWindow` zachowuje dawny konstruktor i ma osobny wariant Sonosa: dwa pola, ścisły dosłowny URL, bez pobierania adresu i bez autostartu po zapisie.
- `ISonosOwnStreamsSessionBackend` i cienkie metody istniejącego `SonosAccountOwnerGroupBackend`/`SonosAccountOwner` korzystają z odebranego koordynatora i klienta. Enter: walidacja → `createSession` → potwierdzone ID sesji → `loadStreamUrl` z jawnym autostartem. Bez drugiego Play, automatycznego ponowienia, zapisu ID sesji w ustawieniach i bez tworzenia Sonos Favorite.
- Caller zachowuje wspólną bramkę poleceń oraz sprawdza konto, dom, grupę i życie okna między dwoma zapisami. Po zmianie kontekstu nie przekazuje adresu do starej sesji.
- Odbiór: Core `--sonos-own-streams`; Windows `--sonos-own-streams-main`; żywy NVDA na izolowanej kopii. Testy rzeczywistego konta/głośnika i skutku audio pozostają osobną bramką. Presety i przebudowa grup to następne etapy.


## Sonos: WŁASNE RADIO — sesja odtwarzania i adres strumienia, warstwa Core (bez UI)

**Tylko Core.** Ten przyrost dodaje **dwie** operacje sesji odtwarzania:
utworzenie sesji w grupie i wczytanie **własnego adresu radia** zapisanego w
AMC. **Nie ma** tu okna, skrótu, menu, listy adresów, presetów, grupowania,
stałego celu, cache, pollingu ani subskrypcji `playbackStatus`. Orkiestracja UI
(„utwórz sesję, potem wczytaj adres”) to **następny** krok — świadomie nie
dorabiamy tu nowej maszyny stanów.

To droga **własnego `radioURL`**, a **nie** zapisanie ulubionego Sonosa:
F3a/F3b i playlisty zostają nietknięte.

Źródło: oficjalna definicja OpenAPI 3.0.3 „Sonos Control API (cloud)”
`v1.56.0-alpha.1-1-gc264f93f-production-cloud`, operacje
`PlaybackSession-CreateSession-GroupId` (`POST /groups/{groupId}/playbackSession`)
i `PlaybackSession-LoadStreamUrl-SessionId`
(`POST /playbackSessions/{sessionId}/playbackSession/loadStreamUrl`).

- `Core/Sonos/SonosPlaybackSessionContract.cs` — niemutowalny model + **wąskie,
  osobne** granice: `ISonosSessionCreateApi` (tylko tworzenie) i
  `ISonosStreamUrlLoadApi` (tylko wczytanie adresu), żeby późniejsze UI zależało
  od kontraktu, nie od całego transportu. `SonosPlaybackSessionLimits` bierze
  limity **z pól `maxLength` definicji**, nie z nazw: `appId` **127**,
  `appContext` **127**, **suma UTF-8 obu < 255** (definicja mówi o tym wprost,
  osobno od limitów pojedynczych pól), `sessionId` **46**, `streamUrl` **1024**,
  `itemId` **128**.
  **Kluczowa różnica, której nie wolno zatrzeć:** `sessionStatus.sessionId` jest
  w definicji **nullable**, a `sessionState` **opcjonalne**. Dlatego `HTTP 200`
  **bez** identyfikatora to **NIE** gotowa sesja — `SonosSessionCreateOutcome`
  rozróżnia **próbę**, **przyjęcie** i **nieznany skutek**, a `sessionId` oddaje
  **wyłącznie** z ważnego wyniku bieżącego konta. Nieznana wartość
  `sessionState` daje `Unknown`, ale odpowiedzi nie unieważnia.
  `SonosSessionIdPolicy` jest **osobna** od `SonosGroupIdPolicy`: `sessionId` to
  **inny zasób** niż `groupId`. Definicja podaje dla tego parametru ścieżki
  wyłącznie `type: string` — **żadnego wzorca** — więc nie wymyślamy regexa
  tożsamości: wartość zostaje **literalna**, kodowany jest tylko segment adresu.
  Kontrolowane `ToString` nie wypisuje `sessionId`, adresu ani `appContext`.
- `Core/Sonos/SonosControlApiClient.PlaybackSession.cs` — `CreateSessionAsync`
  i `LoadStreamUrlAsync` na **ISTNIEJĄCYM** silniku: ten sam `HttpClient`,
  polityka hosta, `Bearer`, kontrola końcowego adresu, deadline, budżet treści
  i ścisły UTF-8. Żadnego drugiego `HttpClient`, żadnej kopii transportu.
  `TrySessionUri` jest **osobna** od `TryGroupUri` — przepuszczenie `groupId`
  tam, gdzie ma iść `sessionId` (albo odwrotnie), trafiłoby w nieistniejący
  zasób. Ciało `createSession` niesie **tylko** `appId` i `appContext` (oba
  **wymagane**); `accountId` i `customData` są **pominięte** — nie zgadujemy
  `accountId` usługi, a dla minimalnego radia nie jest potrzebny. `appId` to
  identyfikator **aplikacji**, **nie** OAuthowy `client_id` ani token.
  `playOnCompletion` jest **obowiązkowy i bez wartości domyślnej** (autostart
  zmienia zachowanie u użytkownika); gdy jest `true`, **nie wolno** dosyłać
  osobnego `Play`. `stationMetadata` **pominięte** w minimum; `itemId`
  opcjonalne (`null` = **pominięcie pola**) i przechodzi **wspólną**
  `IsAcceptableBodyId` — bez whitelisty znaków, bo serializator je escapuje i
  tożsamość zostaje ta sama. `streamUrl` wymaga **bezpiecznego** `http`/`https`
  (`SonosStreamUrlPolicy`) i **nigdy** nie jest przez nas pobierany — zero `GET`,
  nawet sprawdzającego.
- `Core/Sonos/SonosControlApiClient.cs` + `.GroupPlayback.cs` — **minimalne
  WSPÓLNE** rozszerzenie odbioru: czytanie ciała wyjęte z `ReadAsync` do
  `ReadLimitedJsonAsync` (**bez zmiany polityki**), a `WriteCoreAsync` dostał
  **opcjonalny** odbiór odpowiedzi. Stare `GET` i stare `POST` (ulubione,
  playlisty, polecenia grupy) idą **tą samą** ścieżką co dotąd i **nie**
  parsują ciała — zachowanie niezmienione, co potwierdzają ich regresje.
- `Core/Sonos/SonosAccountCoordinator.PlaybackSession.cs` — **cienkie**
  podłączenie **dwóch jawnych, rozdzielonych** metod (`CreateSessionAsync`,
  `LoadStreamUrlAsync`) przez **wspólną** ścieżkę zapisu `RunSessionWriteAsync`:
  bilet pod blokadą, HTTP **poza** blokadą, kontrola **oryginalnej** generacji
  po każdym `await`. Zero kopii OAuth, odświeżania i genlocka.
  **`createSession` to ZAPIS, który może WYPRZEĆ cudze odtwarzanie.** Dlatego
  **nigdy** nie idzie przez `RunGroupReadAsync`, jego ponowienie po `401` ani
  przy odczycie/wejściu/listowaniu; **zero** powtórzeń `POST`, **zero**
  odnowień po `401`, **zero** kasowania konta. Po eviction
  (`ERROR_SESSION_EVICTED`), błędzie czy anulowaniu **żadna sesja nie powstaje
  sama** — dopiero jawne żądanie użytkownika. Anulowanie **po wysłaniu nie cofa**
  przejęcia sesji, więc wynik mówi o skutku **nieznanym**, nie o cofnięciu.
  Przy zmianie konta w trakcie wstrzymanej odpowiedzi stary `sessionId`
  **nie jest publikowany** nowemu kontekstowi.
- Testy: `tests/…/SonosStreamUrlTests.cs`, runner `--sonos-stream-url`
  (**12** sprawdzeń), pozycja w pełnej tabeli Core. Kontrolowany
  `HttpMessageHandler`, atrapa bramki i magazyn w pamięci — **zero** realnego
  I/O, konta, DPAPI i dźwięku.
- **Czego tu świadomie NIE MA:** serwera kolejki w chmurze (`cloudQueue`),
  odtwarzania utworów na żądanie, SMAPI, `audioClip`. Dokumentacja wymaga
  **otwartej sesji** dla `loadStreamUrl`; **rzeczywista wykonalność radia bez
  własnego serwera kolejki pozostaje do próby na prawdziwym koncie** — atrapa
  tego nie rozstrzyga i nie udaje.

## Sonos: PLAYLISTY — odczyt i uruchomienie, warstwa Core (bez UI)

**Tylko Core.** Ten przyrost dodaje odczyt playlist Sonosa domu i ich
uruchomienie w grupie. **Nie ma** tu okna, skrótu, menu, presetów, cache,
pollingu ani subskrypcji `playlistsVersionChange`. UI biblioteki (`Ctrl+L`:
kategorie „Ulubione Sonos” + „Playlisty Sonos”) to **następny** krok;
`Ctrl+U` i `Ctrl+F5` zostają bez zmian.

Źródło: oficjalna definicja OpenAPI 3.0.3 „Sonos Control API (cloud)”
`v1.56.0-alpha.1-1-gc264f93f-production-cloud`, operacje
`Playlists-GetPlaylists-HouseholdId` i `Playlists-LoadPlaylist-GroupId`.

- `Core/Sonos/SonosPlaylistsContract.cs` — niemutowalny model + wąskie granice.
  `SonosPlaylistsLimits` bierze limity **ze źródła**, nie z ulubionych:
  `version` 36, **100** playlist (ulubione mają 70), `id` 36, `name` 100,
  `type` 64, `trackCount` int32. **Kluczowa różnica wobec F1, której nie wolno
  zatrzeć:** w `playlistsList` **zarówno `version`, jak i `playlists` są
  opcjonalne i nullable**, więc brak pola `playlists` i `playlists: null` to
  **normalny sukces z pustą listą** — w `favoritesList` brak `items` jest
  błędem. `type` i `trackCount` nullable: `null` znaczy „Sonos nie podał”, a
  **nie zero**. Wartości źródłowe bez `trim` i bez zmiany wielkości liter.
  Kontrolowane `ToString` nie wypisuje identyfikatorów ani nazw.
  Granice są **wąskie i osobne**: `ISonosPlaylistsApi` (tylko odczyt) i
  `ISonosPlaylistLoadApi` (tylko jeden zapis), żeby późniejsze UI zależało od
  kontraktu, nie od całego transportu.
- `Core/Sonos/SonosControlApiClient.Playlists.cs` — `GetPlaylistsAsync` na
  **ISTNIEJĄCYM** torze `ReadAsync`/`Parse`/`Text`/`Array` i istniejącym
  `SonosHouseholdIdPolicy.TryEncode`. Żadnego nowego `HttpClient`, żadnej kopii
  polityki transportu. `Text`/`Array` idą tu **bez `required: true`** — to
  zgodność z definicją, nie przeoczenie. Powtórzony **identyfikator** odrzuca
  **całą** odpowiedź (`JsonException`), bo nie da się wskazać, który wiersz jest
  który; powtórzona **nazwa jest legalna — tytuł nie jest kluczem**. `trackCount`
  czyta **wspólny** `OptionalInt32`; znaku pilnuje model (ujemne odrzucone).
  Zwrócona lista niesie **literalny `householdId` z zapytania**, nie z odpowiedzi.
  Brak wyniku częściowego: pierwsza niezgodność odrzuca całość.
- `Core/Sonos/SonosControlApiClient.PlaylistLoad.cs` — `LoadPlaylistAsync`
  (`POST /groups/{groupId}/playlists`) na **tym samym** `SendAsync`/`TryGroupUri`
  co ulubione. Pole w ciele to **`playlistId`**, nie `favoriteId`. `action` i
  `playOnCompletion` są **obowiązkowe i bez wartości domyślnych** — mimo że
  definicja pozwala je pominąć, bo `REPLACE` niszczy kolejkę użytkownika.
  **Żadnego `playModes`** (pominięcie zachowuje tryby głośnika, jawne `false` by
  je wyłączyło), żadnego dodatkowego `Play`, żadnego ponowienia `POST`.
  Akcja to **istniejący** `SonosFavoriteQueueAction`: definicja używa dla obu
  operacji **tego samego** schematu `queueAction`, więc drugi enum o tych samych
  wartościach byłby drugim, rozjeżdżającym się źródłem prawdy. Nazwa działającego
  enuma **nie została zmieniona** — przemianowanie dotknęłoby odebranych testów
  F3 bez zysku dla użytkownika.
- `Core/Sonos/SonosControlApiClient.FavoriteLoad.cs` — **wspólne** (nie
  zduplikowane): `IsAcceptableBodyId(id, maxLength)` i `QueueLoadBody(idField, …)`.
  To bramka identyfikatora w **ciele** JSON, **nie** walidacja segmentu adresu:
  bez kodowania procentowego, bez zakazu ukośnika, `IsNullOrEmpty` (nie
  `IsNullOrWhiteSpace`), bez `trim`; odrzucany jest samotny surogat, bo
  serializator po cichu wysłałby inną wartość. Jedna reguła, dwa zasoby.
- `Core/Sonos/SonosGroupPlaybackContract.cs` — **minimalne** rozszerzenie:
  `SonosGroupCommand.LoadPlaylist` dopisane **na końcu** enuma (bez zmiany
  numeracji istniejących pozycji), `PathSuffix` = `"playlists"` (bez przedrostka
  `playback/`), `IsStateDependent` = **true** — polecenie zmienia wspólną kolejkę,
  więc nie jest idempotentne i nie wolno go ponawiać.
- `Core/Sonos/SonosAccountCoordinator.Playlists.cs` — **cienkie** podłączenie:
  `ReadPlaylistsAsync` przez istniejący `RunGroupReadAsync<T>` (bilet pod blokadą,
  HTTP poza blokadą, jedno odnowienie przy znanej minionej ważności, kontrola
  **oryginalnej** generacji po każdym `await`) i `LoadPlaylistAsync` przez
  istniejący `RunGroupCommandAsync` (**dokładnie jeden** `POST`, bez ponawiania i
  bez kasowania konta). Żadnej kopii biletu, odnawiania, blokady ani własnej
  bramki identyfikatorów — walidację ma klient, przed `HTTP`.
  `SonosPlaylistsReadMessages` i `SonosPlaylistsReadResult` są **osobne** od
  ulubionych, żeby użytkownik usłyszał, czego naprawdę dotyczył odczyt; dane
  wychodzą **tylko** przy sukcesie, więc wynik porzucony nie przenosi starej listy
  pod nową tożsamość. Pusta lista **nadal jest sukcesem**.

Czego mierzone zachowanie **nie** obiecuje: `HTTP 200` to **przyjęcie** zlecenia,
nie dowód, że muzyka zagrała (`EffectConfirmed` zawsze `false`). `INSERT` znaczy
dopisanie i przejście do pierwszej dodanej pozycji.

## Sonos: DOSTĘPNY PODGLĄD ULUBIONYCH w Windows (F2)

**Tylko podgląd: `GET`, zero `POST`, zero odtwarzania, zero presetów.** Enter na
liście świadomie **nic nie uruchamia** — prawdziwe uruchamianie i presety to F3
z osobnym kontraktem i osobnym pomiarem.

- `Windows/Services/SonosAccountOwner.cs` — **cienkie, leniwe**
  `ReadFavoritesAsync(string? householdId, CancellationToken)`. Używa
  **ISTNIEJĄCEGO** koordynatora i **ISTNIEJĄCEGO** klienta Control API przez
  prywatne `EnsureControlClient`, ten sam tor co odczyt domów i grup. Żadnego
  drugiego ownera, żadnego drugiego `HttpClient`. Konto **nie budzi się** przy
  starcie programu ani w innych sesjach — dopiero przy jawnym wywołaniu.
  Disposal bez zmian.
- `Core/Sonos/ISonosFavoritesSessionBackend.cs` — **OPCJONALNA** granica sesji z
  jedną metodą `ReadFavoritesAsync`. Stary obowiązkowy
  `ISonosGroupSessionBackend` **nie dostał nowych metod**, więc wszystkie
  starsze atrapy dalej się kompilują. Implementuje ją istniejący
  `SonosAccountOwnerGroupBackend` (w `MainWindow.Sonos.cs`), delegując do
  ownera. Sesje bez ulubionych po prostu tego interfejsu nie implementują.
- `Core/Sonos/SonosFavoritesLabels.cs` — **bezpieczne etykiety**: nazwa jest
  pierwsza, usługa i opis tylko **jeśli istnieją**; identyfikatory, nazwy typów,
  tokeny i wartości enum **nigdy** nie trafiają do etykiety. Rodzaj materiału
  (radio/album/utwór) **nie jest zgadywany** — API nie ma takiego pola.
- `Windows/SonosFavoritesWindow.xaml(.cs)` — dostępny **modal** wzorowany
  organizacyjnie na `WiiMDevicePresetsWindow`: tytuł „Ulubione Sonos”,
  `AutomationProperties.LabelledBy` na liście, opis mówiący wprost o podglądzie,
  początkowy fokus na pierwszej pozycji (lub na komunikacie pustego stanu),
  strzałki, `Tab`, `Escape`/`Alt+F4`/przycisk Zamknij. **`Enter` jest jawnie
  pochłaniany** i nie schodzi do `ActivateSonosGroup` ani do ogólnego
  odtwarzania. Typowane wiersze `SonosFavoriteRow` — bez udawanego `Track`,
  bez udawanego `Device`, bez nowego `MediaItemKind`.
- `Windows/MainWindow.SonosFavorites.cs` — wywołanie pod **ISTNIEJĄCYM**
  `CommandIds.ViewFavorites` (`view.favorites`, dziś `Ctrl+U`), **wyłącznie** w
  sesji Sonos; to samo menu i ta sama paleta co dotąd, **bez** nowego skrótu.
  Granice sprawdzane **przed** I/O i **po każdym** `await` — wzorzec z
  odebranego `MainWindow.SonosHousehold.cs`: konto, bilet/kontekst domu, sesja,
  `closing`, aktywność okna, inny modal. Jedna bramka `_sonosFavoritesInFlight`
  zwalniana **w `finally`** przez właściciela; spóźniony odczyt A **nie**
  zwalnia trwającego B (`_sonosFavoritesGateTicket`). Obie te bramki są wpięte w
  **centralne** `CancelSonosPendingWork` obok bramek polecenia, odświeżania i
  wyboru domu: wyjście z sesji podnosi `_sonosFavoritesGateTicket` i zeruje
  `_sonosFavoritesInFlight`, więc porzucony odczyt A **nie blokuje** nowego
  odczytu B po powrocie do sesji, a spóźnione `finally` A widzi już CUDZY bilet
  i nie rusza zajętości B. Każde jawne otwarcie czyta
  **świeżą** listę; **zero pollingu**. Odmowa (403/429), timeout i błąd
  **nigdy** nie udają świeżej pustej listy — poprawne `0 items` to dostępny
  pusty stan, nie błąd i nie udawana pozycja. Bez sortowania: kolejność z API.
  Brak konta/domu → uczciwe wyjaśnienie plus **istniejąca** droga odzyskania
  (`Ctrl+F5` dla konta, polecenie Wybierz dom Sonos dla domu).

## Sonos: UKŁAD WIDOKÓW (Biblioteka / Ulubione / Ctrl+F5)

Uwagi Michała: głośnik pojawiał się w Ulubionych, Biblioteka była pusta, wiersz
powtarzał nazwę i liczby, a `Ctrl+F5` mówiło „nie ma polecenia w bieżącej sesji”.

**STAN PO ETAPIE BIBLIOTEKI MATERIAŁU — poniższe akapity opisują warstwę
GŁÓWNEGO WIDOKU i nadal obowiązują, ale `Ctrl+L`, `Ctrl+F5` i `Ctrl+U` mają
dziś WŁASNE OKNA MODALNE. Prawdziwy układ opisuje sekcja „Sonos: BIBLIOTEKA
MATERIAŁU, PLAYLISTY i WYBÓR CELU”; czytaj ją razem z tym rozdziałem i nie
traktuj zdania „Biblioteka = CELE STEROWANIA” jako opisu tego, co robi dziś
`Ctrl+L`.**

- **Główny widok NADAL ma źródło grup.** `MainWindow.xaml.cs` `RefreshCurrentView`
  pomija filtr `IsInLibrary`, gdy `IsSonosSession(_sessions.Current.Id)`: grupy
  i głośniki są dostępne z samego odczytu topologii, bez ręcznego dodawania.
  Pozostałe sesje filtrują jak dotąd. Te wiersze NIE zniknęły z MainWindow —
  modal Biblioteki tylko je PRZYKRYWA na czas swojego życia.
- **Normalizacja starego widoku.** `NormalizeSonosNavigationAtStartup()` (obok
  `NormalizeRadioNavigationAtStartup`) przenosi odziedziczone
  `CurrentView = "Ulubione"` sesji Sonos na `Biblioteka`, żeby lista grup nie
  ukazywała się pod etykietą Ulubione. Rusza WYŁĄCZNIE wpis sesji Sonos.
- **Ulubione = RZECZYWISTY materiał.** `CommandIds.ToggleFavorite` i
  `ToggleLibrary` w sesji Sonos są odrzucane z krótkim komunikatem, zanim
  polecenie sięgnie `ActionItems` — guard działa też przy PUSTEJ liście z
  obecnym `CurrentItem`. Sterowanie (`PlayPause`, głośność, Enter) nietknięte.
  `Ctrl+U` nadal otwiera podgląd ulubionych z konta (F2).
- **`Ctrl+F5` → DZIŚ WYBÓR CELU STEROWANIA, nie okno konta.** Skrót otwiera
  `SonosTargetSelectionWindow` (grupy z już odczytanej topologii, pełne nazwy
  głośników). Konto i dom NIE zginęły: zostają pod istniejącym poleceniem
  `CommandIds.ManageSonosConnection` z menu Plik i palety, a okno wyboru celu
  mówi o tym wprost przy zamknięciu. Historyczne kierowanie `Ctrl+F5` na
  `ManageSonosConnection` przez `TryHandleLocalLibraryViewShortcut` już NIE
  opisuje zachowania.
- **Krótki wiersz.** `ApplySonosGroupRows` w `MainWindow.Sonos.cs` nie ustawia
  już `Artist = row.Text` (nazwa + „głośników: N” + stan). Wiersz to sama nazwa
  grupy; szczegóły są w oknie Głośniki i grupy oraz w odtwarzaczu po Enter.

## Sonos: ODCZYT ULUBIONYCH przez KOORDYNATORA KONTA (F1b)

Jedna nowa metoda koordynatora ponad **istniejącym** torem poświadczeń. Bez UI,
bez właściciela Windows, bez XAML, bez skrótów, presetów, ustawień, cache,
pollingu i bez żadnego `POST` — te należą do F2 i etapów dalszych.

- `Core/Sonos/SonosAccountCoordinator.Favorites.cs` (nowy partial) —
  `ReadFavoritesAsync(ISonosFavoritesApi, string? householdId, CancellationToken)`.
  **Cienki delegat** nad WSPÓLNYM, już rozstrzygniętym prywatnym
  `RunGroupReadAsync<SonosFavoritesList>` z `SonosAccountCoordinator.GroupOperations.cs`:
  bilet pod blokadą, HTTP **poza** blokadą, jedno odnowienie przy znanej minionej
  ważności, po 401 najwyżej jedno odnowienie i jedno powtórzenie, kontrola
  ORYGINALNEJ generacji po każdym `await`. **Żadnej kopii** ticket/gate/refresh/
  retry/generacji i żadnej zmiany wspólnego kodu.
- Rzeczywisty `SonosControlApiClient` **już implementuje** `ISonosFavoritesApi`
  (F1a), więc nie ma tu nowego adaptera, `HttpClient` ani konstrukcji klienta.
- `SonosFavoritesReadResult` — **wyłącznie mapowanie** `SonosGroupReadResult<…>`:
  `Status` (1:1, w tym `NoAccount` i `Discarded`), `Favorites`, `Renewed`,
  `Snapshot` **przeniesiona z wyniku** (nie pobierana ponownie po `await`, by
  starej odpowiedzi nie nadać nowej tożsamości), `Succeeded` = `Success` +
  obiekt ≠ null, `Discarded`. Każde niepowodzenie i porzucenie ma **brak
  danych** — stara `Value` nie przechodzi przez wrapper.
- `SonosFavoritesReadMessages` — stałe PL mówiące o **ULUBIONYCH**, nie
  odziedziczona „Lista urządzeń”. `Discarded` mówi o zmianie **kontekstu konta**
  bez orzekania jednej przyczyny (mogło to być nowe logowanie, wylogowanie albo
  zakończenie pracy właściciela). Zero tokenów, klucza, `householdId`, nazw i
  identyfikatorów ulubionych oraz surowego `globalError.reason`; `ToString`
  podaje tylko status, obecność danych i licznik.
- Walidacja domu zostaje **tam, gdzie była**: klient sprawdza segment ścieżki
  PRZED HTTP (`SonosHouseholdIdPolicy`) i sam zwraca `InvalidConfiguration`;
  koordynator nie trimuje, nie podmienia i nie zgaduje domu, a `HouseholdId`
  wyniku pochodzi literalnie z zapytania.
- Testy: `tests/.../SonosFavoritesAccountTests.cs`, CLI
  `--sonos-favorites-account`, wpięte też w pełną tabelę Core. Rzeczywisty
  koordynator + rzeczywisty klient na własnym `HttpMessageHandler`, atrapa
  bramki logowania i magazynu w pamięci; **prawdziwe** nowe logowanie przez
  publiczny tor (bez refleksyjnego ustawiania generacji). Zero sieci, zero
  konta, zero GUI.

## Sonos: ZAŁADOWANIE ULUBIONEGO przez KONTO (F3b)

**Cienkie** podłączenie `loadFavorite` do **istniejącego** właściciela konta —
nie drugi koordynator. Bez GUI, presetów (to F3d), ustawień i realnego API.

- `Core/Sonos/SonosAccountCoordinator.FavoriteLoad.cs` — osobny `partial`, jedna
  publiczna `LoadFavoriteAsync(ISonosFavoriteLoadApi, groupId, favoriteId,
  action, playOnCompletion, ct)` → `SonosGroupCommandResult`. `action` i
  `playOnCompletion` są **obowiązkowe, bez wartości domyślnych** (różnica między
  dopisaniem a **zastąpieniem** kolejki użytkownika jest nieodwracalna i nie może
  zależeć od milczenia wołającego). Po `ArgumentNullException.ThrowIfNull(api)`
  jedno przekazanie do **zastanego** `RunGroupCommandAsync(LoadFavorite, …)`.
- Zero kopii logiki: bilet, odnawianie, generacja, porzucanie i transport
  pochodzą z odebranego przebiegu poleceń. Żadnej nowej polityki.
- Walidacja identyfikatorów, grupy i akcji zostaje **tam, gdzie była** — w
  kliencie (F3a), który odrzuca złe wejście PRZED HTTP. Druga bramka w
  koordynatorze byłaby drugim, rozjeżdżającym się źródłem prawdy.
- `loadFavorite` jest **zależne od stanu** (`IsStateDependent` = true): dopisuje
  albo zastępuje wspólną kolejkę, więc **nigdy** nie jest ponawiane automatycznie
  — po 401/403/404/429/5xx i po utracie odpowiedzi jest **dokładnie jeden** POST,
  bez odnowienia i bez kasowania konta. HTTP 200 to przyjęcie zlecenia,
  `EffectConfirmed` zawsze `false`; `RequestSent` znaczy **próbę**, nie doręczenie.
- Testy: `tests/.../SonosFavoriteLoadAccountTests.cs` (17), CLI
  `--sonos-favorite-load-account`, wpięte też w pełną tabelę Core. Rzeczywisty
  koordynator + **rzeczywisty klient** na własnym `HttpMessageHandler` (klient
  implementuje `ISonosFavoriteLoadApi` wprost — żaden adapter nie jest potrzebny),
  atrapa bramki logowania i magazynu w pamięci; prawdziwe nowe logowanie przez
  publiczny tor. Zero sieci, zero konta, zero GUI.

## Sonos: ODCZYT ULUBIONYCH domu w kliencie Core (F1a)

Tylko **odczyt** listy ulubionych. Bez konta, UI, presetów, skrótów, zapisu
(`loadFavorite`), subskrypcji, cache i pollingu — te należą do osobnych etapów.

- `Core/Sonos/SonosFavoritesContract.cs` — niemutowalny kontrakt:
  `SonosFavorite` (id, name wymagane i niepuste; description i service
  opcjonalne/nullable), `SonosFavoriteService`, `SonosFavoritesList`
  (**kopia defensywna** pozycji + literalny `HouseholdId` z zapytania),
  `SonosFavoritesOutcome`, wąska granica `ISonosFavoritesApi`.
  Limity z definicji OpenAPI (`SonosFavoritesLimits`): version 36, items 70,
  id 36, name 100, description 256, service.name 31, service.id 10.
  **`imageUrl` jest deprecated i NIE jest czytany**; nieznane pola ignorowane.
  Definicja nie podaje rodzaju materiału — AMC go **nie zgaduje**.
- `Core/Sonos/SonosControlApiClient.Favorites.cs` — `GetFavoritesAsync`
  (`GET /households/{householdId}/favorites`) na **tym samym** transporcie co
  odczyt domów i grup: prywatne `ReadAsync`/`Parse`/`Array`/`Text`, 256 KiB,
  MaxDepth 16, odrzucanie duplikatów pól JSON, bramka adresu końcowego. Żadnego
  nowego `HttpClient` i żadnej liberalizacji walidatorów.
- **Polityka AMC, nie twierdzenie o API**: `items:[]` to SUKCES, natomiast brak
  `items`, `items:null`, zły typ, przekroczony limit i **powtórzony `id`** to
  `InvalidResponse` CAŁEJ odpowiedzi — bez listy częściowej. Dwa różne `id`
  o tej samej nazwie to **dwie** pozycje (nazwa nie jest kluczem). Wartości
  źródłowe zachowane bez `trim`, obcinania i zmiany wielkości liter.
- `ToString` modeli i wyniku podaje **tylko** typ, status i liczniki; nigdy
  treści odpowiedzi, identyfikatorów, nazw ani tokenu. Komunikaty idą ze
  wspólnego `SonosControlApiMessages` — bez surowego `globalError.reason`.
- Testy: `tests/.../SonosFavoritesTests.cs`, CLI `--sonos-favorites`, wpięte
  też w pełną tabelę Core. Mierzone na rzeczywistym kliencie z własnym
  `HttpMessageHandler`, bez sieci i bez konta.

## Sonos: ZAŁADOWANIE ulubionego do kolejki grupy (F3a, tylko klient Core)

Zapisowy odpowiednik odczytu z F1 — jeden `POST` na jedno wywołanie, przez
**ten sam** transport co pozostałe polecenia grupy.

- `Core/Sonos/SonosFavoriteLoadContract.cs` — `SonosFavoriteQueueAction`
  (pełny enum `queueAction` z definicji: `Replace`, `Append`, `Insert`,
  `InsertNext`, `PlayNow`), `SonosFavoriteQueueActions.WireValue`/`IsDefined`
  oraz **wąski, opcjonalny** `ISonosFavoriteLoadApi`. Interfejs jest osobny
  celowo: `ISonosGroupApi`/`ISonosFavoritesApi` mają dziesiątki atrap w
  pomiarach, więc nowa metoda **nie** trafia do obowiązkowego kontraktu.
- `Core/Sonos/SonosControlApiClient.FavoriteLoad.cs` — `LoadFavoriteAsync`
  (`POST /groups/{groupId}/favorites`). `action` i `playOnCompletion` są
  **obowiązkowe i bez wartości domyślnych**: wybór polityki należy do
  wołającego, bo `REPLACE` niszczy kolejkę użytkownika. Ciało składa
  **ścisły** `Utf8JsonWriter` (`favoriteId`, `action`, `playOnCompletion`),
  nie ręczna sklejka — identyfikator Sonosa może zawierać cudzysłów, odwrotny
  ukośnik, spacje i znaki spoza ASCII.
- `playModes` **celowo pominięte**: brak pola ZACHOWUJE tryby odtwarzania
  głośnika, jawne `false` by je wyłączyło. Transport nie ma prawa ich ruszać.
- `favoriteId` idzie w **ciele**, nie w ścieżce; bramka identyczna jak w F1
  (`IsNullOrEmpty` + `MaxFavoriteIdLength` z `SonosFavoritesLimits`, **nie**
  `IsNullOrWhiteSpace`), bez `trim`, obcinania i whitelisty ASCII. Niepoprawny
  UTF-16 (samotny surogat) odrzucany **przed** serializatorem, żeby nie wysłać
  po cichu innej wartości.
- `SonosGroupCommand.LoadFavorite` dopisane na **końcu** enuma bez
  renumeracji; `PathSuffix` = `favorites`, `IsStateDependent` = **true**
  (dopisuje/zastępuje kolejkę, więc nie jest idempotentne). Bezparametrowe
  `SendGroupCommandAsync` nadal **odmawia** go obsłużyć — bez identyfikatora
  ulubionego nie ma polecenia.
- **HTTP 200 (`{}` wg definicji) to PRZYJĘCIE zlecenia**, nie dowód, że muzyka
  zagrała: `EffectConfirmed` zawsze `false`. Zerwane połączenie i anulowanie
  po wejściu dają `EffectAmbiguous` wg **istniejących** reguł, bez ponowień.
- Testy: `tests/.../SonosFavoriteLoadTests.cs` (31 sprawdzeń), CLI
  `--sonos-favorite-load`, wpięte też w pełną tabelę Core. Własny
  `HttpMessageHandler`, bez sieci, konta i poświadczeń.

## Sonos: URUCHOMIENIE ulubionego z okna (F3c, UI na istniejącym oknie F2)

Bez nowego skrótu, bez nowej pozycji w menu, bez presetów i bez trwałości
(to F3d). `Ctrl+U` otwiera **to samo** okno podglądu ulubionych, które
w wariancie z uruchamianiem dostaje przycisk **Odtwórz** i obsługę `Enter`.

- `Windows/SonosFavoritesWindow.xaml.cs` — jedno okno, DWA warianty:
  `_loadFavorite is null` to **czysty podgląd** (przycisk `Collapsed`, `Enter`
  nic nie zleca, pomocniczy opis listy mówi wprost, że nic tu nie uruchamia),
  a wariant z uruchamianiem ma przycisk **widoczny**. Brak grupy lub pusta lista
  **nie usuwa** przycisku, tylko go **wyłącza** (`IsEnabled=false`). Powód
  dokłada się w `HelpText` **tylko** przy braku grupy; dla pustej listy domu
  `HelpText` zostaje bez dopisku, a powód odmowy słyszy się dopiero po próbie
  (`PlayNothingSelected`) — martwy przycisk bez żadnego wyjaśnienia byłby gorszy
  od odmowy, ale to NIE jest ta sama droga w obu przypadkach.
  Zlecenie niesie NIEZMIENNĄ tożsamość tej instancji okna i jej token życia;
  zamknięcie okna anuluje **wyłącznie własne** oczekiwanie, nie całą sesję.
- `Windows/MainWindow.SonosFavorites.cs` — `LoadSonosFavoriteAsync` sprawdza
  granice **przed** działaniem i **ponownie po każdym await**: żywy zlecający,
  granica konta (po rzeczywistej zmianie konta stary identyfikator ulubionego
  **nie** idzie przez nowe konto), ten sam bilet celu, dom, grupa i sesja,
  oraz bramka jednego polecenia Sonos.
  **PRE-POST vs PO-AWAIT to dwie różne prawdy i dwa różne komunikaty.**
  Odmowa **przed** wysłaniem to ZERO POST-ów, więc mówi wprost „nie zostało
  wysłane” (`PlayNotSentAccountChanged`, `PlayNotSentTargetChanged`). Guard
  **po** await rozstrzyga `RequestSent`: false nadal oznacza niewysłanie
  (np. wymiana konta podczas odnowienia przed POST), a true tylko podjętą próbę
  i brak potwierdzenia wyniku (`PlayAttemptedOutcomeUnknown`). Porzucenie
  oczekiwania nie obiecuje cofnięcia (`PlayAbandonedOutcomeUnknown`).
  Żaden z nich nie twierdzi wykonania, odrzucenia ani cofnięcia i żaden nie
  wysyła niczego, żeby odkręcić możliwy POST.
  ŻYWY zlecający modal **zawsze** dostaje koniec: zostawienie go na „Wysyłam
  polecenie uruchomienia. Czekaj.” było defektem L1. Spóźniony wynik trafia
  **tylko** do tej instancji okna albo nikogo: po `Closed` i przy własnym
  zamykaniu AMC jest CISZA, a w oknie NIEAKTYWNYM odświeża status do
  odczytania, ale **nie ogłasza** go i **nie** przejmuje obcego ogniska.
- Akcja kolejki jest JAWNA: `INSERT` + `playOnCompletion: true`, **bez**
  `playModes`. Wg oficjalnego `queue-action` `INSERT` dopisuje materiał i
  przesuwa głowicę na pierwszą wstawioną pozycję — to **zamiar**, nie pomiar
  dźwięku. „Przyjęto polecenie uruchomienia” nie obiecuje potwierdzonej
  tożsamości tego, co gra.
- `SonosFavoritesWindow.RunPlayAsync` nie wyłącza skupionego przycisku Odtwórz;
  powtórne wywołanie blokuje bramka. Zakończenie nie cofa świadomej zmiany
  fokusu na inną kontrolkę. Komunikat oczekiwania powstaje tylko dla zadania
  rzeczywiście w toku, które nie podało już wyniku przez `AnnounceForOwner`.
  Natychmiastowa odmowa daje jeden komunikat końcowy, bez konkurującego Czekaj.
- Testy: `tests/.../SonosFavoritePlayUiTests.cs` (64 sprawdzenia, CLI
  `--sonos-favorite-play-ui`) oraz `tests/.../SonosFavoritePlayRealOwnerTests.cs`
  (5 przypadków, CLI `--sonos-favorite-play-real-owner`) — ten drugi mierzy **całą** drogę
  produkcyjną: `MainWindow` → istniejące zaplecze konta → `SonosAccountOwner` →
  koordynator → `SonosControlApiClient` → syntetyczny `HttpMessageHandler`
  (transport podstawiany TEST-ONLY refleksją, **bez** nowej publicznej fabryki
  w produkcji). Oba wpięte w pełną tabelę Windows. Zero sieci, poświadczeń,
  DPAPI i dźwięku.

## Sonos: UŻYTKOWA sesja — aktywna grupa, lista, odtwarzacz, polecenia (po alfa413)

Sesja Sonos wzorowana na WiiM: **urządzenie autonomiczne**, bez własnego
silnika odtwarzania w AMC. Pełny opis obsługi i granic:
`TESTY_SONOS_ODTWARZACZ_PL.md`.

- `Core/Sonos/ISonosGroupSessionBackend.cs` — WĄSKA granica API/transportu dla
  UI: trzy odczyty (playback/metadata/volume), polecenia grupy, seek
  bezwzględny i względny, głośność, wyciszenie, odczyt domów i grup.
  **Domyślnie `null`** w `MainWindow` — produkcyjnie podstawia go właściciel
  konta, w pomiarze zaplecze syntetyczne. Poświadczenia nie wychodzą poza
  właściciela.
- `Core/Sonos/SonosSessionPresentation.cs` — Core sesji bez WPF: pozycje listy
  z grup, pusty stan z drogą do konta, formatery tytułu/wykonawcy/źródła/
  stanu/głośności, bramka dostępności z `availablePlaybackActions` i
  `volume.Fixed`, werdykty po jawnym odczycie (potwierdzone / niepotwierdzone /
  odczytany stan), brak zegara demonstracyjnego, ekstrapolacja czasu tylko dla
  `Known + Playing` z jawną świeżością.
- `Core/Configuration/AppSettings.cs` — `SonosSessionSettings` w
  `PersistedState.Sonos`: pamięć **domu i grupy po identyfikatorze, BEZ
  tokenów**; oddzielony cache topologii od danych konta. `SessionSlotOrder`:
  Sonos dopisany na końcu (`sonos`, numer 8), a nowa sesja **nigdy nie wchodzi
  w zwolniony numer w środku** — przy braku miejsca ponad szczytem zostaje bez
  numeru, żeby nie zmienić wyuczonego Alt+cyfra.
- `Core/Sessions/SessionManager.cs` — `sonos` dopisany jako OSTATNIA sesja
  (po Spotify), bez utworów demonstracyjnych.
- `Windows/MainWindow.Sonos.cs` — cała obsługa sesji poza monolitem: wejście do
  sesji (leniwa inicjalizacja właściciela — **start programu nie czyta konta
  ani sieci**), lista grup, uczynienie grupy aktywną **bez POST**, odczyt stanu
  na widoku odtwarzacza, mapowanie wspólnych poleceń na operacje grupy z jawnym
  GET-em po każdym POST, jeden przelot poleceniowy naraz (jawna odmowa
  zajętości), oszczędny pojedynczy odczyt w tle z backoffem, bilet celu
  unieważniający spóźnione odpowiedzi, zakończenie własnych liczników na Close.
- `Windows/MainWindow.xaml.cs` — tylko PODPIĘCIE: `ApplyPlaybackPolicyWhenLeavingPlayer`
  traktuje Sonos jak WiiM (**zero stop/pause** przy wyjściu, zmianie sesji i
  zamykaniu AMC), gałąź Sonos w `ExecuteCommand`, ujście komunikatów do pomiaru.
- `Windows/Services/SonosAccountOwner.cs` — wąskie operacje grupy na TYM SAMYM
  leniwym właścicielu (wspólny koordynator i klient), bez wyciągania tokenów.

Pomiary: `Core.SmokeTests --sonos-session-presentation` (WSL) oraz
`Windows.SmokeTests --sonos-session-ui` — **prawdziwy `MainWindow` bez
pokazywania okna**, ale mierzy POMOCNICZE metody sesji, nie drogę klawiatury.
Wejście rzeczywistą drogą użytkownika mierzy osobny
`Windows.SmokeTests --sonos-session-entry-ui` na POKAZANYM własnym oknie.
Odsłuchu NVDA i fizycznej klawiatury tu NIE było.

KONTROLKI, PLAY/PAUSE, CZAS, SKOK DO POZYCJI I POZOSTAŁE POLECENIA (części B3a
i B4, odebrane na syntetycznej granicy API) mierzy `Windows.SmokeTests --sonos-player-ui`
(**317 sprawdzeń**, POKAZANE własne okno, zaplecze grupy tylko syntetyczne):

- Przewijanie **±Custom** i **cyfry 0–9** idą istniejącym `SeekRelativeAsync`
  aktywnej grupy: custom bierze długość z konfiguracji przez tę samą regułę
  `PlaybackSeekRules.NormalizeCustomSeekSeconds` co `CommandRouter`, a cyfry
  przekładają cel procentowy na deltę od **świeżego** odczytu tej samej grupy i
  materiału (`ResolveSonosPercentSeekAsync`) — bez modalu i bez nowego punktu
  końcowego. Wcześniej oba wpadały w `seconds == 0` i kończyły się odmową.
- Next/Previous, głośność ±1/±5 i wyciszenie były już poprawne — zmierzono je
  (jedno żądanie do aktywnej grupy + jawny GET, bramki `CanSkip`,
  `SkipToPreviousAllowed`, brak odczytu i `volume.fixed` dają zero żądań,
  wyciszenie odwraca wyłącznie znany stan) bez zmiany produkcji; dyskryminację
  pomiarów potwierdziła cofnięta mutacja kontrolna celu grupy.

- `UpdateSonosPlayerView` (`MainWindow.Sonos.cs`) ustawia **wszystkie** kontrolki
  odtwarzacza z odczytu grupy — także `PlayerTimeText` i etykietę oraz **dostępną
  nazwę `PlayerPlayPauseButton`** — a to, czego Sonos nie ma (prędkość, zakładki,
  nagrywanie radia), jawnie ukrywa. Bez tego odtwarzacz **dziedziczył czas i stan
  po poprzedniej sesji**.
- Dostępna nazwa przycisku idzie za odczytem, ale przestawia się **tylko przy
  faktycznej zmianie** — nazwa nadpisuje treść dla czytnika, a odczyt w tle nie
  może generować zdarzeń UIA co cykl.
- `TimeElapsed` / `TimeRemaining` / `TimeTotal` w sesji Sonos obsługuje
  `AnnounceSonosTime`, **nie** ogólny router (ten czytałby `DemoMediaSession`,
  czyli pozycję 0 z długości 0). Brak pozycji lub długości = „nie jest znany”.
- Format kontrolki i poleceń czasu korzysta z istniejącego
  `MediaItemFormatter.FormatDuration` (całkowite godziny, bez zawijania po dobie).
  D6 sprawdza pozycję 24:30:00 z długości 25:00:00 i pozostałe 30:00.
- `SeekSonosToPositionAsync` (`MainWindow.Sonos.cs`) obsługuje **SKOK DO POZYCJI**
  (`transport.seekToTime` / `transport.seekToPercentage`) w sesji grupy: oba
  rzeczywiste przyciski (`PlayerSeekTimeButton`, `PlayerSeekPercentButton`), oba
  wpisy menu i **Ctrl+J / Ctrl+Shift+J** trafiają tu przez gałąź Sonos w
  `ExecuteCommand`. Wcześniej szły do ogólnego `ShowSeekPositionDialog`, który
  czyta `_sessions.Current.CurrentItem.Duration`; wiersz grupy powstaje jako
  `MediaItemKind.Device` **bez długości**, więc odpowiedź była **zawsze** „czas
  trwania jest nieznany” — mimo że odczyt grupy znał i pozycję, i długość.
  Teraz otwiera się **istniejące** `SeekPositionWindow` z długością **z odczytu
  Sonosa**; `DemoMediaSession.SetPosition` nie jest używane, a zwykłe sesje idą
  dotychczasową drogą.
- Backend ma **wyłącznie** `SeekRelativeAsync(groupId, deltaMillis, itemId, token)` —
  nie ma skoku absolutnego i **żadnego endpointu nie dodano**. Pozycja docelowa
  jest przeliczana na **deltę** względem **ponownego** odczytu po zamknięciu
  modalu (modal trwa dowolnie długo, liczenie od pozycji z chwili otwarcia
  trafiałoby gdzie indziej). Bramka to ta sama `CanSeek` co reszta przewijania,
  z `_sonosCommandInFlight`, biletem celu i **bez ponowień**.
- Bramka (`EvaluateSonosSeekGate`) jest oceniana **dwa razy**: przed modalem i —
  po odbiorze — **po świeżym odczycie, przed wysłaniem POST-a**, na fladze
  `CanSeek`, która właśnie przyszła (**bez dodatkowego GET-a** dla samej
  walidacji). Zmierzone: przy `CanSeek=false` ze świeżego odczytu **tego samego**
  `itemId` wcześniej i tak szedł jeden `SeekRelativeAsync`.
- Rezerwacja bramki (`gateTicket` + `_sonosCommandInFlight`) jest brana
  **przed `ShowDialog`**, czyli **najpóźniej przed pierwszym `await`** tej drogi,
  a `ReleaseSonosCommandGate(gateTicket)` stoi w `finally` obejmującym **wszystkie**
  wyjścia (odmowa, anulowanie, wyjątek). Warunek właściciela biletu został, więc
  **cudzy bilet nie jest zwalniany**. Zmierzone: wcześniej w czasie przedskokowego
  `GET`-a bramka była otwarta (`busy=False`) i głośność z późniejszego callbacka
  Dispatchera **nakładała się** na skok, a `finally` zerowało `busy` mimo obcego
  polecenia w locie. Stara bramka `ExecuteSonosCommandAsync` nietknięta.
- ZERO POST przy: anulowaniu, braku odczytanej długości lub pozycji,
  `CanSeek=false` (przed **i po** odczycie), zmianie grupy oraz **zmianie materiału
  w trakcie modalu** (`itemId` sprawdzany przed i po). Bieżąca odmowa mówi,
  czego brakuje; anulowanie i porzucony cel milczą. Nie wymyślamy zera.
  Po próbie skoku idzie **jawny GET**, a werdykt
  zostaje dotychczasowy: `Accepted` bez zmiany odczytu **nie** jest dowodem
  trafionej pozycji.
- **Błąd przedskokowego odczytu nie jest cichy.** Oba `await ReadSonosGroupStateAsync`
  mają własne `try`/`catch`; wcześniej wyjątek/timeout **uciekał** z fire-and-forget
  `_ =` jako porzucony fault i użytkownik nie słyszał nic. Komunikaty są **rozróżnione**:
  przed POST-em „nie udało się odczytać aktualnej pozycji, **skok nie został wysłany**”,
  po POST-cie **wg `RequestSent` z kontraktu**: `false` = ZERO prób, więc „skok nie
  został wysłany, a odczytu stanu też nie udało się wykonać”; `true` = tylko
  **podjęta próba**, więc „podjęto próbę skoku… brak potwierdzenia” — **bez** obietnicy,
  że żądanie opuściło maszynę albo dotarło do głośnika (`RequestSent=true` tego
  **nie** dowodzi). Treść wyjątku idzie tylko do `Debug.WriteLine` (typ, **bez
  payloadu/sekretów**, kanał znika w Release); anulowanie milczy. **Porzucenie celu
  milczy też w gałęziach nieudanego przedskokowego odczytu**: kontrola
  `_isClosing`/biletu celu stoi PRZED `Announce`, bo `false` z
  `ReadSonosGroupStateAsync` znaczy także anulowanie i nieaktualny cel. Zero
  ponowień zachowane, starych helperów transportu nie ruszano.
- **Fokus wraca tam, skąd skok wyszedł**: `FocusSonosPlayerAfterSeek(focusBefore, byTime)`
  bierze zmierzony `Keyboard.FocusedElement` sprzed modalu, potem przycisk **danego
  trybu**, potem `PlayPause`, potem panel; ukryty/nieogniskowalny element pomija.
  Wcześniej **zawsze** wybierany był `PlayerSeekTimeButton`, również gdy modal
  procentowy otwarto z `PlayerSeekPercentButton` (zmierzone w obu trybach, przy
  zatwierdzeniu i anulowaniu). Fokusu **nie** przywracamy, gdy okno nie jest aktywne
  ani nie ma w sobie fokusu klawiatury (obcy foreground, odejście, zmiana sesji);
  skrót z `PlayPause` zostawia fokus na `PlayerPlayPauseButton`.
- B4 odebrano również fizyczną klawiaturą i żywym NVDA: 25 poleceń, dodatkowe
  odmowy i granice wartości, świeża pozycja, zajętość oraz porzucenie celu.
  Dane były syntetyczne; nie jest to próba HTTP ani dźwięku rzeczywistych głośników.
- `ExecuteSonosCommandAsync` ma wąski `catch` wyłącznie wokół potwierdzającego
  odczytu, poniżej wywołania backendu. Błąd nie porzuca zadania jako Faulted:
  po sprawdzeniu biletu celu mówi ogólnie o poleceniu i rozróżnia `RequestSent`
  false (brak próby) od true (próba bez potwierdzenia). Anulowanie i porzucony cel
  milczą. `finally` zwalnia własną bramkę; nie dodano ponowienia polecenia.
  B4-7 oraz niezależny NVDA potwierdziły oba warianty dla cyfry i custom.

ODŚWIEŻANIE sesji (etap B1) mierzy `Windows.SmokeTests --sonos-session-polling-ui`
na POKAZANYM własnym oknie i na **prawdziwym `PlayerUiTimer_Tick`**, nie na samym
helperze. Podpięcie i bezpieczniki:

- `PlayerUiTimer_Tick` (`MainWindow.xaml.cs`) woła `PollSonosGroupFromPlayerTimer`,
  gdy odtwarzacz jest otwarty w sesji Sonos. Termin i barierę rozstrzyga sam
  `PollSonosGroupIfDueAsync`; odczyt tła **nic nie mówi i nie zabiera fokusu**.
- `_sonosBackgroundRead` — PRAWDZIWA bariera `Task`: dopóki odczyt (stan +
  metadane + głośność) się nie domknie, kolejne tyknięcia **nie wysyłają ani
  jednego GET-u**. Bilet aktywacji zostaje nietknięty, więc Enter po przebudzeniu
  licznika dalej działa.
- `_sonosReadSequence` + `IsSonosReadStale` — kolejność odczytów TEJ SAMEJ grupy.
  Sam bilet celu tego nie łapał: odczyt tła i odczyt potwierdzający polecenie mają
  ten sam bilet celu, więc **starszy odczyt tła nadpisywał świeższy wynik**.
- `_sonosCommandGateTicket` + `ReleaseSonosCommandGate` — właściciel bramki
  polecenia. Wcześniej zwolnienie sprawdzało bilet CELU, więc po zmianie grupy w
  trakcie polecenia bramka zostawała **trwale zajęta** i następne polecenie nie
  docierało do backendu. Spóźnione `finally` starszego przelotu nie zwalnia bramki
  nowszego. Nadal **brak kolejki i brak ponowienia POST**.
- Decyzja o pełnym odczycie obejmuje też METADANE (`playback && metadata &&
  volume`); wcześniej brak metadanych udawał potwierdzony odczyt. Wyjątek odczytu
  (np. timeout) zapisuje backoff i **leci dalej** — nie ginie cicho.

Interwały (10 s / 60 s backoff) to **polityka AMC, nie deklarowany limit API
Sonosa**. Odsłuchu NVDA, prawdziwego konta i odbioru produktu tu NIE było.

Dwie usterki WEJŚCIA naprawione po alfa413 (zmiana CALLERA, nie nowa ścieżka):

- `ApplySonosGroupRows` (`MainWindow.Sonos.cs`) po asynchronicznym wejściu
  odświeża **PRAWDZIWY widok** (`RefreshCurrentView` z zachowaniem wyboru po
  identyfikatorze), tylko gdy Sonos jest bieżącą sesją i widać listę. Wcześniej
  `ReplaceItems` kończył się po odświeżeniu widoku, więc **pierwsze Ctrl+8**
  dawało pustą kontrolkę listy przy odczytanych grupach.
- `ActivateSelected` (`MainWindow.xaml.cs`) dla wiersza grupy w sesji Sonos
  wchodzi w istniejące `ActivateSonosGroupThenShowPlayerAsync`. Fizyczny Enter
  szedł wcześniej do `NavigateTo(item.Title)` — widok nazywał się jak grupa,
  grupa nie stawała się aktywna, odtwarzacz się nie otwierał. Gałąź
  `ExecuteCommand(ActivateSelected)` bierze cel WYŁĄCZNIE z zaznaczonego wiersza,
  żeby nie aktywować niewidocznej grupy przy pustej liście.

ZMIANA KONTA wobec sesji (etap B2b) mierzy `Windows.SmokeTests
--sonos-session-account-ui` na prawdziwym `MainWindow` i produkcyjnym koordynatorze
konta; zaplecze poświadczeń/grup jest syntetyczne w pamięci. Cztery bezpieczniki
tego etapu:

- `ApplySonosAccountBinding` (`MainWindow.Sonos.cs`) — RZECZYWISTE porzucenie stanu
  idzie JEDNĄ istniejącą drogą publikacji `ApplySonosGroupRows`, więc razem z cache
  czyszczą się `Session.Items`, `CurrentItem` i kontrolka listy. Wcześniej
  bezpośrednie `_sonosGroupRows = []` zostawiało w sesji **pokoje starego konta**.
  Kryterium: brak STARYCH identyfikatorów — uczciwy pusty stan jest w porządku.
- `IsSonosReadStaleOrAccountChanged` (`MainWindow.Sonos.cs`) — granica konta **PO
  każdym await** (playback, metadane, głośność), a w `EnterSonosSessionAsync` po
  `ReadHouseholdsAsync` i `ReadGroupsAsync`. Wcześniej sprawdzenie było tylko PRZED
  await, więc wynik starego konta wracał na listę. Samo `IsSonosReadStale` (B1)
  zostaje nietknięte — nowa bramka jest OBOK. Porzucenie podnosi bilet celu, więc
  **kolejny GET ze starym identyfikatorem nie wychodzi**, bez dodatkowego poll-a.
- `EstablishSonosAccountBindingReference` woła `ShowSonosAccountManager`
  (`MainWindow.xaml.cs`) **PRZED** modalem. Przy zimnym starcie znacznik był `null`,
  więc pierwsza migawka po modalu udawała odniesienie i zapisany wybór starego
  konta zostawał. Zero znacznika to prawidłowe odniesienie, **nie „brak konta”**;
  metoda niczego nie porzuca. Ogólny start nadal konta NIE czyta.
- `SonosAccountChangedInstruction` — instrukcja po zmianie konta opisuje ZMIERZONĄ
  działającą drogę (przejście do INNEJ sesji i powrót). Poprzednie brzmienie
  („wejdź do sesji Sonos”) nie działało: ponowne wejście będąc już w sesji nie
  przechodzi przez gałąź `sessionChanged` i nie odświeża niczego. Pełne odświeżanie
  w miejscu należy do etapu B2c.

### B2c1 — jawne odświeżanie grup i unieważnienie znikniętego celu

Mierzone przez `--sonos-topology-refresh-ui` (64 policzone postwarunki) na
prawdziwym `MainWindow` i prawdziwym `ExecuteCommand`; granica to
`ISonosGroupSessionBackend`, bez konta, HTTP, DPAPI i audio.

- `CommandIds.RefreshSonosGroups` (`sonos.groups.refresh`, nazwa „Odśwież grupy
  Sonos”) — **BEZ skrótu klawiszowego**, więc nic się nie renumeruje. Widoczność:
  `CommandVisibleInPalette` zwraca `true` tylko w sesji Sonos, a
  `RefreshSonosGroupsMenuItem` w menu Plik jest `Collapsed` poza nią.
  `ManageSonosConnection` zostaje dostępne wszędzie — bez zmian.
- `RefreshSonosTopologyAsync` (`MainWindow.Sonos.cs`) — czyta `ReadHouseholdsAsync`
  **i** `ReadGroupsAsync` z zaplecza, nie z cache. Jedno odświeżenie naraz
  (`_sonosRefreshInFlight`); bramka ma właściciela (`_sonosRefreshGateTicket`), więc
  spóźniony przelot A nie zwalnia ani nie podmienia trwającego B. Bilet celu i
  granica konta sprawdzane **po każdym await** i przed publikacją. Zero POST.
- **Zniknięcie po POTWIERDZONYM odczycie** unieważnia cel: najpierw
  `CancelSonosPendingWork`, potem publikacja świeżej topologii i
  `ClearSonosTargetState` (wybór, playback, metadane, głośność, czasy), powrót z
  odtwarzacza `ReturnFromPlayerToList` i `ApplySonosGroupRows`, więc `Session.Items`,
  `CurrentItem` i kontrolka listy są prawdziwe. Stary GET zwolniony PO odświeżeniu
  nic nie publikuje. Wybór **nie** przechodzi na sąsiada; zniknięcie samej grupy nie
  kasuje istniejącego domu.
- **Błąd odczytu to nie dowód zniknięcia**: `AnnounceSonosRefreshNotFresh` mówi o
  braku świeżości, a poprawne identyfikatory i wiersze zostają — pustki nie
  publikujemy jako sukcesu.
- Ten sam identyfikator po zmianie nazwy i kolejności **zostaje** (ścieżka
  niedestrukcyjna idzie przez `ApplySonosTopology`, rozwiązywanie po ID).
- Instrukcja odzyskania w `ActivateSonosGroupAsync` nazywa teraz ISTNIEJĄCE
  polecenie („Użyj polecenia Odśwież grupy Sonos”), a nie nieistniejący F5.
- Wielodomowy wybór to nadal **B2c2**: istniejąca reguła „pusty wybór + dokładnie
  jeden dom” zostaje, żadnego okna wyboru tu nie ma.

Odsłuchu NVDA, prawdziwego konta i prawdziwych głośników tu NIE było.

### B2c2 — dostępny wybór domu, anulowanie bez zmian, zapis wyboru

Mierzone przez `--sonos-household-choice-ui` (**97 sprawdzeń**, własny Windows,
kod `9be70c6`) na prawdziwym `MainWindow`, `ExecuteCommand` i kontrolkach WPF.
Osobny pomiar potwierdza rzeczywiste `ShowDialog` oraz pierwszy plan. Granica
zaplecza to syntetyczne `ISonosGroupSessionBackend` i prywatny
`ConfigurationStore` — bez rzeczywistego konta, HTTP, DPAPI i audio.

UCZCIWA GRANICA APARATURY: `Harness.RunChoice` podstawia POKAZANIE okna (Show z
pompą zamiast modalnego `ShowDialog`) i „Escape” realizuje przez UIA `Invoke` na
przycisku Anuluj, a nie przez fizyczny klawisz. Guardy produkcyjne siedzą PRZED
punktem podstawienia, więc testowa droga ich NIE obchodzi. Niezależnym dowodem na
prawdziwe `ShowDialog` i realny pierwszy plan jest osobna sonda (`ParentProbe`).

- `CommandIds.ChooseSonosHousehold` (`sonos.household.choose`, nazwa „Wybierz dom
  Sonos”) — **BEZ skrótu klawiszowego**, więc nic się nie renumeruje. Widoczność
  jak przy odświeżaniu grup: `CommandVisibleInPalette` tylko w sesji Sonos,
  `ChooseSonosHouseholdMenuItem` w menu Plik `Collapsed` poza nią.
- `MainWindow.SonosHousehold.cs` — `ChooseSonosHouseholdAsync`: JEDEN warunek
  prezentacji `CanPresentSonosHouseholdChoice()` sprawdzany w DWÓCH miejscach tej
  samej metody — na WEJŚCIU, PRZED pierwszym `EnsureSonosBackend` /
  `ApplySonosAccountBinding` / `ReadHouseholdsAsync` (niewidoczny lub nieaktywny
  właściciel albo inny widoczny modal AMC = **zero GET**, zero biletu bramki), i
  PONOWNIE po await, na bilecie celu, PRZED utworzeniem okna. Świeży
  `ReadHouseholdsAsync` z ISTNIEJĄCEGO zaplecza właściciela konta, bez nowego
  HTTP i bez własnego magazynu. **Spóźniony** odczyt (wyjście z sesji, zmiana
  konta, zamknięcie) NIE pokazuje okna ani pól starego domu.
- L1 — PORZUCONY wybór nie zatrzaskuje polecenia: `CancelSonosPendingWork`
  (`MainWindow.Sonos.cs`) JAWNIE podnosi `_sonosHouseholdChoiceGateTicket` i
  zeruje `_sonosHouseholdChoiceInFlight`, tą samą regułą co bramki polecenia i
  odświeżania. Porzucony przelot A widzi w finally CUDZY bilet i NIE odblokuje
  trwającego B, a powrót do sesji robi NOWY GET zamiast odbić się od „już trwa”.
  PUŁAPKA: `SwitchSonosHouseholdAsync` sam woła Cancel, więc pracuje na NOWYM
  bilecie — i zwalnia go w SWOIM finally (outer finally ma bilet stary), we
  wszystkich zakończeniach, także przy błędzie grup.
- L2 — `CanPresentSonosHouseholdChoice()` sprawdzane w DWÓCH miejscach tej samej
  metody: na WEJŚCIU (przed pierwszym GET) i PONOWNIE po await, PRZED
  utworzeniem okna: widoczne, aktywne, włączone, nie zamykane okno główne, sesja
  Sonos i **brak innego widocznego** `OwnedWindows`. Odmowa NIE podnosi licznika
  utworzonych okien, na wejściu NIE podnosi też licznika odczytów domów, nic nie
  odtwarza po późniejszym powrocie do aplikacji i mówi, co zrobić. Właściciel
  modala wiązany PRZED pokazaniem i wyjątku NIE tłumimy — modal bez właściciela
  to dokładnie ta wada.
- `SonosHouseholdSelectionWindow` — mały dostępny modal na wzór
  `SessionSelectionWindow`, ale **bez** renumeracji poleceń. Zaznaczenie startowe
  po **IDENTYFIKATORZE** bieżącego domu, nie po indeksie; etykiety z nazw
  zaplecza (`SonosHouseholdLabel`), bez `ToString()` i bez sekretów. Ruch
  zaznaczeniem sam z siebie **niczego nie zapisuje**.
- Anulowanie / Escape / zamknięcie okna: **zero mutacji** — identyfikator domu,
  grupy, wiersze listy i metadane zostają, `settings.json` nietknięty, zero POST.
- Potwierdzenie **innego** domu: identyfikator sprawdzany wobec świeżo
  odczytanej listy tego samego konta, potem `CancelSonosPendingWork` +
  unieważnienie lotów starego celu, wyczyszczenie grupy, odtwarzania, metadanych,
  głośności, czasu i **rzeczywistych wierszy** PRZED odczytem grup nowego domu.
  Zapis przez ISTNIEJĄCE `QueueStateSave(announceFailure: true)`. Grupy nowego
  domu tą samą drogą zaplecza — **bez POST, Stop i Pause**; aktywna grupa nie
  jest wybierana po cichu i odtwarzacz nie otwiera się sam.
- Błąd grup nowego domu: wybór B **zostaje** (jest świadomy), a puste/nieaktualne
  dane są opisane uczciwie — grupy domu A **nie** są pokazywane jako grupy B.
- Potwierdzenie **tego samego** domu nie restartuje celu: aktywna grupa,
  metadane i model zostają.
- Trwałość mierzona realnie: `ConfigurationStore.Save` → `LoadOrCreate` →
  **drugie** `MainWindow` z zapisanego pliku; `SelectedHouseholdId` = DOM-2
  przeżywa, a odtworzony wybór rzeczywiście kieruje odczytem grup. Zmiana nazwy
  i kolejności domów nie rusza identyfikatora; do JSON nie doszły żadne tokeny
  ani liczniki kont.

Dodatkowo odebrano rzeczywiste menu i paletę, Tab, strzałki, Enter, Escape,
Anuluj naciśnięte spacją oraz zamknięcie modala Alt+F4 z żywym NVDA i prawdziwym
Podglądem mowy. Drugi proces wczytał zapisany wybór; po rzeczywistym wejściu do
sesji odczyt grup wskazywał ten sam dom, a modal odtwarzał jego zaznaczenie.
Harness pomijał uruchamianie źródeł w `ContentRendered`; to nie pomiar zwykłego
startu produkcji. Osobny `AnnouncementSinkForTests` nadal mierzy tylko tekst,
nie mowę czytnika. Prawdziwego konta i głośników nie używano.
Końcowy niezależny przegląd zgodności i jakości `9be70c6` nie wykazał blokad.

## Sonos: AUTORYZOWANE operacje grupy przez właściciela konta (Core, po alfa413)

Wąskie powiązanie trzech GET-ów i dziesięciu POST-ów grupy z ISTNIEJĄCYM
`SonosAccountCoordinator`. Nadal BEZ UI, pollingu, weryfikacji skutku, parsera
kodów błędu, `MediaOutput`, EQ, ulubionych i kolejki — to kolejne etapy.

- `Core/Sonos/SonosGroupOperationsContract.cs`:
  - `ISonosGroupApi` — wąski szew analogiczny do `ISonosDeviceApi`: dokładnie
    trzy odczyty i polecenia grupy, token jako ARGUMENT jednego wywołania.
    Implementacja go nie zapisuje i nie oddaje na zewnątrz;
  - `SonosControlApiGroupApi` — CIENKI adapter PRAWDZIWEGO
    `SonosControlApiClient`: bez własnej logiki, bez zapamiętywania tokenu,
    bez ponowień. Nie przejmuje własności klienta. Żadnej zaślepki;
  - `SonosGroupReadResult<T>` — publiczny wynik ODCZYTU: status, dane tylko
    przy sukcesie, `Renewed` i bezpieczna `Snapshot`. Żadnego tokenu, gettera
    tokenu ani callbacku tokenu do UI;
  - `SonosGroupOperationStatus` + `SonosGroupCommandResult` — co zrobiło KONTO
    (`NoAccount`, `Attempted`, `Discarded`, `Canceled`, `Unauthorized`) jest
    ODDZIELONE od odpowiedzi usługi (`Outcome`) i od skutku
    (`EffectConfirmed` zawsze `false`). `Accepted` nigdy nie znaczy „wykonane”;
  - `SonosGroupOperationMessages` — osobny słownik PL dla wyniku KONTA, bez
    tokenu, klucza i treści odpowiedzi. Porzucenie PO wysłaniu ma własne
    zdanie o NIEZNANYM skutku: nie obiecuje cofnięcia i nie twierdzi, że
    polecenia nie było.
- `Core/Sonos/SonosAccountCoordinator.GroupOperations.cs` (część `partial`
  istniejącego koordynatora):
  - bilet konta (token + generacja) czytany RAZEM pod tą samą blokadą przez
    ISTNIEJĄCY `TryTakeReadTicketLocked`; HTTP i cudze callbacki zawsze POZA
    blokadą. Po KAŻDYM `await` weryfikowana jest ORYGINALNA generacja;
  - ODCZYT: znana MINIONA ważność → jedno odnowienie PRZED zapytaniem;
    nieznana ważność nie odnawia niczego; 401 → najwyżej JEDNO odnowienie i
    JEDNO powtórzenie GET, dokładnie jak w `DeviceRead`. Zero pętli reauth;
  - POLECENIE: odnowienie TYLKO PRZED wysłaniem i tylko przy znanej minionej
    ważności, następnie DOKŁADNIE JEDEN POST. Retry GET-a NIE jest kopiowane
    do POST — ani po 401, ani po 429/5xx, ani po utraconej odpowiedzi.
    Polecenie nie kasuje konta i nie niesie w sobie kolejnej autoryzacji;
    jawne późniejsze odnowienie to OSOBNA operacja;
  - jedna wspólna centralna logika odnawiania: `RenewForReadAsync` nad
    `RefreshCoreAsync` z KOTWICĄ generacji. Żadnej kopii refresh, tokenów ani
    blokady; I/O nie jest zamknięte zewnętrznym reentrant lockiem;
  - dawna operacja po nowym logowaniu, `Disconnect` albo `Dispose` kończy się
    `Discarded`: nie odnawia konta B, nie używa jego biletu, nie publikuje
    spóźnionych danych ani komunikatu sukcesu. Konto B zostaje nietknięte
    (bez `Delete`). Konto działające w pamięci po błędzie zapisu ma sprawne
    operacje, bez ponownego logowania.
- Testy: `SonosGroupAccountOperationsTests` (`--sonos-group-account`,
  sprawdzany dosłownie w `Program.cs`, plus wpis w pełnej tabeli Core) —
  18 przypadków na PRAWDZIWYM `SonosControlApiClient` ze sztucznym
  `HttpMessageHandler` i PRAWDZIWEJ drodze `BeginLoginAsync`/`CheckLoginAsync`
  (bez podstawiania prywatnych pól). Blokady zdarzeniowe na
  `TaskCompletionSource` z `RunContinuationsAsynchronously`. Tokeny wyłącznie
  syntetyczne; zero sieci, konta, DPAPI i GUI.

## Sonos Control API: odczyt odtwarzania grupy i podstawowe polecenia (Core, po alfa413)

Warstwa WYŁĄCZNIE niskopoziomowa: klient, modele i testy. Nie ma tu
koordynatora uwierzytelnienia dla zapisu, `MainWindow`, nowej sesji, pollingu,
ulubionych, kolejki ani EQ — to kolejne etapy.

- `Core/Sonos/SonosGroupPlaybackContract.cs`: niemutowalne modele odczytu
  (`SonosGroupPlaybackStatus`, `SonosGroupMetadata`, `SonosQueueItem`,
  `SonosTrackMetadata`, `SonosGroupVolume`, `SonosPlaybackActions`,
  `SonosPlayModes`) oraz rozdzielne wyniki. Trzy rzeczy pilnowane wprost:
  - `bool?` zachowuje różnicę BRAK pola kontra `false`, a `int?` różnicę
    „pozycja nieznana” kontra `0` — DTO nie wymyśla czasu ani uprawnień;
  - `artist` i `album` są OBIEKTAMI z wymaganym `name`; napis w tym miejscu to
    niezgodna odpowiedź, nie nazwa wykonawcy;
  - `canSkipBack` jest przeterminowane i zastępowane przez `canSkipToPrevious`;
    `SkipToPreviousAllowed` daje pierwszeństwo nowemu polu. `skipBack`
    (powrót na początek utworu) to NIE `skipToPreviousTrack`.
  Modele nie trzymają tokenów i nie ujawniają ich w `ToString`.
- `Core/Sonos/SonosControlApiClient.GroupPlayback.cs` (część `partial` istniejącego
  klienta, bez duplikowania dojrzałych zabezpieczeń): trzy GET-y
  (`playback`, `playbackMetadata`, `groupVolume`) i polecenia POST na GRUPIE —
  `play`, `pause`, `togglePlayPause`, `skipToNextTrack`, `skipToPreviousTrack`,
  `seek`, `seekRelative`, `groupVolume`, `groupVolume/mute`,
  `groupVolume/relative`. `seek` NIE wymaga sesji odtwarzania. Nie ma
  niepotwierdzonego `/stop` ani odtwarzania URI.
- `SonosGroupCommandOutcome` rozdziela PRZYJĘCIE zlecenia od jego SKUTKU:
  HTTP 200 znaczy tylko, że Sonos przyjął polecenie. Po utracie odpowiedzi
  polecenie PRZEŁĄCZAJĄCE (`togglePlayPause`) lub WZGLĘDNE (`seekRelative`,
  `groupVolume/relative`) ma skutek NIEROZSTRZYGNIĘTY (`EffectAmbiguous`).
  Żadnego automatycznego ponawiania POST — powtórzenie dałoby inny skutek.
  Jawny odczyt stanu i weryfikacja skutku należą do przyszłego koordynatora,
  nie do transportu; nie ma tego po cichu w kliencie.
- `SonosGroupCommandMessages` to OSOBNY, mały stały słownik komunikatów WYNIKU
  POLECENIA. `SonosGroupCommandOutcome.Message` bierze tekst z niego, nie ze
  wspólnego `SonosControlApiMessages` (słownika ODCZYTU) — po `POST` nie ma
  żadnego odczytu, więc tekst „Odczyt z Sonos zakończony.” po `pause` byłby
  fałszywym opisem zdarzenia, a 400 nie dotyczy „zapytania o urządzenia”.
  Rozgraniczenie słów: HTTP 200 → „przyjął polecenie; wykonanie
  niepotwierdzone”, brak żądania (anulowanie przed wysłaniem, lokalnie
  odrzucony argument albo `groupId`) → „nie zostało wysłane” BEZ obwiniania
  identyfikatora domu, zerwane polecenie przełączające/względne → „skutek
  nieznany”. Komunikaty ODCZYTU zostają bez zmian. `Sent` nadal znaczy tylko
  „przekazane do `HttpClient`”, więc żaden tekst nie ogłasza dostarczenia do
  Sonosa ani wykonania.
- Błąd polecenia NIE odświeża tokenu i NIE kasuje konta — to warstwa
  koordynatora. Treść błędu sterowana przez serwer nie trafia do diagnostyki.
- `capabilities`/`fixed` będą bramką UI; tutaj jest tylko wierny odczyt i
  walidacja argumentów (zakres `volume` 0..100, `volumeDelta` -100..100,
  nieujemny `positionMillis`, `itemId` do 128 znaków ze spec) — bez martwych
  funkcji i bez zaślepek.
- Testy: `SonosGroupPlaybackTests` (`--sonos-group-playback`, sprawdzany
  dosłownie w `Program.cs`) plus wpis w pełnej tabeli Core. Tylko sztuczny
  handler, bez sieci, konta, DPAPI i GUI.

## Sonos: lista urządzeń — zakres kandydata alfa413

- `SonosAccountPresenter.ShowDevices` otwiera `SonosDevicesWindow` z okna konta i uruchamia asynchroniczne `LoadAsync` bezpośrednio przed `ShowDialog`. Właściciel konta przekazuje bezpieczne operacje odczytu; modele UI nie przejmują poświadczeń.
- `SonosDevicesWindow.DescribeFocusedRow` rozpoznaje prawdziwy `ListBoxItem` albo jego potomka. `RowFocusTarget` zachowuje nazwę listy, ID domu i ID grupy/głośnika. `groupIds`/`playerIds` są spójne z tekstowymi elementami list.
- `FindFreshRow` po przebudowie odszukuje ID, wykonuje `ScrollIntoView` i `UpdateLayout`, a następnie zwraca nowy kontener. Nie używa starego wiersza ani pozycji, nie wymusza zaznaczenia i nie aktywuje okna. Gdy cel znika, pozostaje czytelna instrukcja.


- `Core/Sonos/SonosAccountCoordinator.cs`: odnawianie ma jedną, wspólną drogę `RefreshCoreAsync(oczekiwanaGeneracja)`. Wewnętrzny wynik `RefreshRun` niesie GENERACJĘ, dla której odnowienie faktycznie zaszło, więc świeża generacja nie legalizuje starej operacji. Weryfikacja oczekiwanej generacji jest atomowa: pod tą samą blokadą, która decyduje o starcie/dołączeniu do odnowienia, więc nie ma okienka między sprawdzeniem a startem. Bez dodatkowego zewnętrznego locka na I/O — pojedynczy przelot (single-flight), nieprzerywanie cudzego odnowienia, anulowanie waitera i kasowanie konta wyłącznie przy dokładnym 401 brokera zostają bez zmian.
- `Core/Sonos/SonosAccountCoordinator.DeviceRead.cs`: `RenewForReadAsync` przekazuje KOTWICĘ generacji do centralnej logiki i porównuje zestaw poświadczeń z DOKŁADNYM wynikiem odnowienia, nie z dowolną bieżącą migawką. Skutek: odczyt starego konta nie odnawia nowego, nie czyta danych konta B i ich nie publikuje; spóźniona operacja kończy się `Discarded`, a konto B zostaje nietknięte (bez `Delete`).
- `Windows/SonosDevicesWindow.xaml.cs`: zajętość nie gubi już fokusu — przed wyłączeniem skupionej kontrolki fokus przechodzi na włączone pole instrukcji, a po zakończeniu wraca na poprzednią kontrolkę, o ile użytkownik sam nie wybrał innej (np. Zamknij). Odświeżanie ogłasza JEDEN jawny komunikat oczekiwania przed każdym czekaniem i jeden wynik po nim (bez podwójnego czytania „lista domów + topologia”). Wybrany dom wraca PO IDENTYFIKATORZE, także po zmianie kolejności listy; powrót do pierwszego domu następuje dopiero, gdy wybrany dom zniknie. Nowe kwity: `LoadingAnnouncementCount`, `FocusedControlName`.
- `Windows/SonosDevicesWindow.xaml`: usunięta kolizja klawiszy dostępu — `Głośni_ki` zamiast drugiego Alt+G przy `_Grupy`. Skróty programu nietknięte.
- `Core/Sonos/SonosDeviceLabels.cs`: `DescribeTopology` nie dubluje już przedimka („Dom Dom Sonos 1”); nowe stałe `LoadingHouseholds` i `LoadingTopology`. Nazwy modeli bez zmian.
- Testy: `SonosDeviceReadTests` (`--sonos-device-read`) o dwa przypadki wyścigu przez PRAWDZIWĄ drogę logowania; `SonosDevicesWindowTests` (`--sonos-devices-window`) o fokus w zajętości, komunikaty ładowania i zachowanie wybranego domu.

## Sonos Control API: odczyt domów, grup i głośników

- `Core/Sonos/SonosControlApiContract.cs`: niemutowalne modele i rozdzielne wyniki, stały adres Sonosa, publiczny klucz integracji przekazywany jawnie, lokalna polityka segmentu ID.
- `Core/Sonos/SonosControlApiClient.cs`: tylko dwa GET-y: households i groups. Token tylko w nagłówku pojedynczego wywołania; brak zapisu, odnawiania, wylogowania i sterowania głośnikami. Bez automatycznych redirectów i cookies; wspólny skończony deadline wysłania i całego odczytu, limit rozmiaru, ścisły UTF-8/JSON, bez surowych błędów w diagnostyce.
- `docs/SONOS_CONTROL_READ_PL.md`: źródła Sonosa oraz wyraźnie oddzielone lokalne polityki AMC.
- `SonosControlApiClientTests`: `--sonos-control-api` i wpis w pełnej tabeli Core; syntetyczny transport, bez sieci/kont/GUI.
- Klient jest podłączony przez `SonosControlApiDeviceApi`, koordynator i właściciela konta do okna listy. Nie obsługuje jeszcze sterowania odtwarzaniem.

## Pozostawaj na liście po uruchomieniu stacji Enterem (ustawienie globalne)

- `Core/Configuration/AppSettings.cs`: `StayOnListAfterRadioEnter`, domyślnie `false`. Stary zapis bez tej własności też daje `false`, więc aktualizacja niczego nie włącza po cichu.
- `MainWindow.ActivateSelected` (gałąź `Track or Station or Episode`) rozpoczyna odtwarzanie jak dotąd; zmieniona jest WYŁĄCZNIE jedna linia — `ShowPlayerView()` stoi teraz pod `!ShouldStayOnListAfterRadioEnter(session, item)`. Lista, jej zaznaczenie i fokus nie są dotykane, bo nic poza widokiem się nie zmienia.
- `MainWindow.ShouldStayOnListAfterRadioEnter` to cały warunek: włączone ustawienie ORAZ `MediaItemKind.Station` ORAZ sesja o identyfikatorze `radio`. Utwory i odcinki (także w sesji radia), inne sesje, Spacja, gesty i menu idą starą drogą.
- Presety mają własne `OpenPlayerWhenActivatingPreset` w `ActivatePreset` i nowa opcja ich nie dotyka — to osobne pole, osobny `SettingsTarget` i osobne polecenie.
- `SettingsWindow.xaml(.cs)`: `StayOnListAfterRadioEnterCheck` w zakładce Ogólne, między opcją presetu a pamięcią pozycji plików. `AutomationProperties.Name`/`HelpText` mówią, czego dotyczy (sesja radia, Enter na liście), że odtwarzanie mimo wszystko startuje, że do odtwarzacza przechodzi się F6 oraz że presety mają własną opcję.
- Dojście z palety: `CommandIds.SettingsStayOnListAfterRadioEnter` = `settings.radio.stayOnListAfterEnter`, nazwa w `CommandCatalog`, stan włączone/wyłączone w `CommandPaletteSearch`, cel `SettingsTarget.StayOnListAfterRadioEnter` w `CommandRouter` — ten sam wzorzec co `KeepAudioEditBackups`.
- `RadioEnterStaysOnListTests` mierzy obie wartości; `--radio-enter-stay-model`, `--radio-enter-stay-controls`, `--radio-enter-stay`.
## Sonos: trwały magazyn poświadczeń (DPAPI bieżącego użytkownika)

- `Core/Sonos/SonosCredentialStoreContract.cs` — wyłącznie kontrakt i model,
  bez I/O i bez szyfrowania: `SonosCredentialPolicy` (limity, m.in.
  `MaxEncryptedFileBytes` 512 KiB sprawdzane PRZED alokacją bufora),
  `MaxOpaqueValueBytes` = `SonosLoginClient.MaxResponseBytes` (64 KiB) dla
  access tokenu i scope — magazyn NIE może być węższy od warstwy, która te
  wartości już przyjęła, więc limit pola to limit CAŁEJ odpowiedzi brokera, a
  nie osobna dobrana liczba; `MaxPlaintextBytes` WYLICZANE z limitów pól
  (współczynnik doboru budżetu i zapas), nadal skończone. To NIE gwarancja
  narzutu enkodera 2×; rzeczywista długość JSON-a jest sprawdzana po zapisie.
  Zapis JSON-a używa `JavaScriptEncoder.UnsafeRelaxedJsonEscaping` — plik idzie
  pod DPAPI na własny dysk, nie do HTML, więc domyślne escapowanie HTML-owe
  (`<` → 6 B) tylko zjadałoby budżet. Scope PUSTY jest jawnie pusty
  (`IsStorableScopeValue`): zapisywany i oddawany jako `""`, nigdy jako `null`
  i nigdy przez trim.
  `SonosStoredCredentials` (cały zestaw `SonosTokens`, moment otrzymania w UTC,
  `BrokerOrigin`, jawny `FormatVersion`), `SonosCredentialSerializer`
  (serializacja JSON o jawnej wersji i ścisła walidacja),
  `ISonosCredentialStore` oraz rozdzielne wyniki `SonosCredentialReadStatus`
  (`Success`/`Missing`/`Invalid`/`BrokerMismatch`/`ReadFailure`) i
  `SonosCredentialWriteStatus` (`Success`/`InvalidRecord`/`WriteFailure`).
  Termin ważności jest WYLICZANY z `expires_in`; jego brak zostaje stanem
  nieznanym — żadnego domyślnego TTL. Brak refresh tokenu po pierwszym
  logowaniu jest zapisywany jak jest, bez produkowania fałszywego RT. RT, jeśli
  obecny, musi przejść istniejącą `SonosRefreshTokenPolicy`. Nierozpoznana
  wersja formatu i nieprzewidziane pola to `Invalid`, nigdy cichy sukces.
  `ToString` modeli i wyników nie wypisuje tokenów, scope ani origin.
  `Write` porównuje `BrokerOrigin` rekordu ze SKONFIGUROWANYM brokerem PRZED
  szyfrowaniem i przed I/O: poświadczenia obcego brokera to `InvalidRecord`, a
  poprzedni ciphertext zostaje bit w bit.
- `Windows/Services/SonosDpapiCredentialStore.cs` — JEDEN plik zaszyfrowany
  natywnym DPAPI (P/Invoke `crypt32`, bez nowych pakietów) w zakresie
  BIEŻĄCEGO UŻYTKOWNIKA, nigdy `LocalMachine`, zawsze
  `CRYPTPROTECT_UI_FORBIDDEN`, ze stałą entropią domeny
  `AccessibleMediaController/Sonos/credentials/v1`. Wybór pliku zamiast
  Menedżera poświadczeń (wzorzec `TidalCredentialStore`,
  `SpotifyLibrespotCredentialStore`) wynika z możliwego dużego zestawu tokenów
  Sonos i limitu rozmiaru bloba `CredWrite`. Domyślna ścieżka
  `LocalAppData/AccessibleMediaController/credentials/sonos.bin` jest osobno od
  `state.json` i jego kopii; konstruktor nie czyta zapisanych kont. Zapis:
  szyfrowanie PRZED I/O, unikalny plik tymczasowy w tym samym folderze, pełny
  flush, `File.Replace` istniejącego albo `File.Move` pierwszego — nigdy
  Delete+Write, więc nieudany zapis zachowuje poprzedni plik; bez plaintextu na
  dysku, bez pliku `.bak`. Odczyt i walidacja NIE kasują zepsutego pliku
  (świadome odejście od kasowania w `TidalCredentialStore`); usuwa tylko jawne,
  idempotentne `Delete()`. Bufory native zwalniane w `finally`, plaintextowe
  tablice bajtów zerowane; niemutowalnych stringów C# nie obiecujemy wymazać.
  Zero PowerShella, CLI i zmiennych środowiskowych z tokenami.
- `tests/SonosCredentialStoreHarness/` — `run.sh` buduje w WSL minimalny
  harness `net8.0` (linkuje pliki produktowe, bez `ProjectReference` do WPF) i
  uruchamia go NATYWNIE na Windows przez
  `powershell.exe -NoProfile -NonInteractive -File`, więc DPAPI jest prawdziwe.
  Scenariusze obejmują roundtrip, brak markerów plaintext w pliku, literalne
  Unicode/spacje, brak RT i nieznana ważność, odczyt w NOWYM procesie, rotacja
  RT, brak pliku, uszkodzony ciphertext, nieobsługiwany format, obcy broker,
  złe wejście i błąd zapisu zachowujące poprzedni rekord, limit rozmiaru,
  `ToString` bez sekretów. Wyłącznie wartości syntetyczne i własny, świeży
  katalog w Windows TEMP; żaden istniejący plik danych nie jest czytany.
- Magazyn nie zawiera pollingu ani UI. Właścicielem jego operacji jest jedna
  instancja koordynatora opisanego poniżej; brak blokad wieloprocesowych.

## Sonos: koordynator konta (odtworzenie, logowanie, odnawianie, wylogowanie)

- `Core/Sonos/SonosAccountContract.cs` — wyłącznie kontrakt i niemutowalne
  wyniki: `SonosAccountState`
  (`NoAccount`/`AwaitingBrowser`/`Connected`/`NeedsLogin`/`StoreFailure`),
  rozdzielna PRZYCZYNA `SonosAccountIssue` (`ReadFailure`,
  `InvalidStoredRecord`, `BrokerMismatch`, `WriteFailure`, `InvalidRecord`,
  `Reauthorization`, `DeleteFailure`), stałe komunikaty
  `SonosAccountMessages` oraz `SonosAccountSnapshot` i wyniki operacji
  (`RestoreResult`, `LoginStartResult`, `LoginCheckResult`, `RefreshResult`,
  `PersistRetryResult`, `DisconnectResult`). Migawka NIE zawiera tokenów, scope,
  origin ani identyfikatora sesji — ani w polach, ani w `ToString`. `IsPersisted`
  mówi o UTRWALENIU, a `IsAwaitingBrowser` jest NIEZALEŻNE od stanu; nieznana
  ważność zostaje nieznana.
- `Core/Sonos/SonosAccountCoordinator.cs` — właściciel stanu konta w procesie.
  `ISonosLoginGateway` to NAJMNIEJSZY szew na odebrany `SonosLoginClient`
  (`SonosLoginClientGateway` nie owija jego zachowań). `RestoreOnce` czyta
  magazyn DOKŁADNIE raz; `Missing` nie jest błędem, a `Invalid`, `BrokerMismatch`
  i `ReadFailure` nie kasują ani nie nadpisują pliku. `BeginLoginAsync` nie
  uruchamia przeglądarki — oddaje zaufany `AuthorizeUri` warstwie UI.
  `CheckLoginAsync` wykonuje DOKŁADNIE jedno `FetchResult`, bez pollingu, timera
  i powtórek HTTP; `Pending` zostawia próbę oczekującą. DWIE NIEZALEŻNE
  GENERACJE: generacja ZESTAWU rośnie przy zmianie użytecznego zestawu, a
  generacja PRÓBY LOGOWANIA przy jej rozpoczęciu i anulowaniu — dlatego samo
  `BeginLogin`/`CancelPendingLogin` nie porzuca trwającego odnowienia konta.
  Spóźnioną odpowiedź logowania odrzuca porównanie generacji ORAZ tożsamości
  sesji (`ReferenceEquals` z `pendingSession`), więc późny `Pending` po sukcesie
  nie wskrzesza oczekiwania na przeglądarkę. `ClearPendingLoginLocked` zdejmuje
  `AwaitingBrowser` i wraca do stanu sprzed oczekiwania, żeby zakończona próba
  nie kazała kończyć logowania w przeglądarce, której już nie ma.
  `RefreshAsync` to SINGLE-FLIGHT w obrębie generacji: wspólny placeholder
  (`TaskCompletionSource`) publikujemy pod blokadą, a samo zapytanie startuje
  POZA nią (`StartRefreshOutsideGate`), więc bramka wykonana synchronicznie do
  pierwszego `await` nie biegnie pod lockiem. Wołający, który zrezygnował PRZED
  startem, dostaje `Canceled` bez żadnego zapytania; rezygnacja jednego
  wołającego NIE anuluje wspólnej operacji pozostałym, a trwającego zapytania
  innego wołającego nie przerywamy. Unieważnić poświadczenia może WYŁĄCZNIE
  dokładne 401 `ReauthorizationRequired` tej samej generacji — 429, 502, 503,
  transport, niezgodny JSON i anulowanie nie kasują niczego. `WriteFailure`
  zostawia NOWY zestaw w pamięci jako niezapisany (bez powrotu do starego RT),
  a `RetryPersist` ponawia SAM zapis bez ani jednego zapytania. `InvalidRecord`
  nie udaje działającego konta. `Disconnect` z nieudanym `Delete` nie udaje
  potwierdzonego wylogowania (`StoreFailure` + `DeleteFailure` +
  `PersistedRecordMayRemain`). TRZECIA, NIEZALEŻNA GENERACJA (B2a):
  `SonosAccountSnapshot.AccountBindingGeneration` — monotoniczny licznik LOKALNEGO
  CYKLU PODŁĄCZENIA konta w polu `accountBindingGeneration`. Rośnie WYŁĄCZNIE gdy
  konto zostało ZASTĄPIONE albo REALNIE ODŁĄCZONE: udane `CheckLoginAsync`
  (`InstallLocked(..., replacesAccount: true)`), `Disconnect` przy istniejącym
  koncie (także przy nieudanym `Delete`, bo zestaw przestaje być używany),
  `InvalidateLocked` po dokładnym 401 bieżącej generacji oraz `InvalidRecord`,
  który usuwa działające konto. NIE rośnie przy zwykłym odnowieniu i rotacji
  zestawu (`InstallLocked(..., replacesAccount: false)`), przy `RestoreOnce`
  własnego zapisu, przy `RetryPersist`, przy rozpoczęciu/anulowaniu/nieudanym
  logowaniu ani przy spóźnionych odpowiedziach starszej generacji —
  `CredentialGeneration` zgodnie ze starym kontraktem zmienia się wtedy nadal.
  Znacznik NIE jest identyfikatorem użytkownika po stronie Sonosa: nie zawiera
  tokenów, scope, origin, proof ani ID i NIE trafia do `AppSettings`, DPAPI ani
  pliku konta, więc jego zakres życia to JEDNA instancja koordynatora — między
  procesami nie jest stabilny. Konsument porównuje PIERWSZĄ odczytaną migawkę jako
  punkt odniesienia; pierwszy odczyt po starcie nie jest zmianą konta. Samego
  konsumenta (UI, porzucanie grup/odczytów) tu NIE MA. Świadomie NIE MA tu UI, uruchamiania
  przeglądarki, timerów, pollingu, planowania odnowień, workerów w tle, powtórek
  HTTP ani frameworka DI.
- `Core.SmokeTests/SonosAccountCoordinatorTests.cs` — 33 przypadki zachowania na
  małych atrapach (magazyn z licznikami i kontrolowanym `WriteStatus`/`Delete`,
  bramka z kolejkami i barierami `TaskCompletionSource`
  `RunContinuationsAsynchronously`, bez usypiania wątku). Sesja logowania
  pochodzi z PRAWDZIWEGO `SonosLoginClient.StartAsync` z syntetycznym
  `HttpMessageHandler`, więc do produkcji nie dodano publicznego szwu ani
  Reflection do `SonosLoginSession`. Argument `--sonos-account-coordinator` oraz
  pełny zestaw Core. Siedem ostatnich przypadków (B2a) mierzy
  `AccountBindingGeneration` PRAWDZIWYMI operacjami koordynatora: stabilny punkt
  odniesienia pustego i odtworzonego stanu, brak zmiany po
  rozpoczęciu/anulowaniu/odmowie logowania, zmiana po udanym nowym logowaniu przy
  już istniejącym koncie, brak zmiany po odnowieniu i rotacji zestawu (przy
  zmienionej `CredentialGeneration`), zmiana po 401 i po jawnym wylogowaniu,
  odrzucenie spóźnionego 401 jako zastąpienia oraz brak tokenów w samym
  znaczniku. Testy napisano PO drafcie, dlatego dyskryminację
  udowodniono siedmioma mutacjami w kopii pliku produkcyjnego (raport:
  `amc_pomoc/sonos-account-coordinator-recovery1/`).

- `Core.SmokeTests/SonosAccountCoordinatorStateTests.cs` — dodatkowe przypadki
  podłączone do tego samego zestawu: odnowienie zachowuje niezakończoną próbę
  logowania; przejściowy błąd Fetch pozwala ponowić odbiór bez nowego Start;
  origin normalizowany przez istniejącą konfigurację jest zgodny z rzeczywistą
  polityką magazynu. Ponadto: BrokerMismatch, odwrotna kolejność Start,
  anulowana próba, stare 401 po nowym logowaniu i Dispose w trakcie refresh.
  Walidacja formatu magazynu pochodzi z produkcyjnego serializatora, nie tylko
  z atrapy zawsze przyjmującej dane. Trzy naprawy rodzica mają RED 23/26,
  GREEN 26/26. Nie wykonywano testu prawdziwego konta ani GUI.

## Sonos: okno konta (UI na odebranym koordynatorze)

Uzupełnienie odbioru: `RescueFocusBefore` chroni przed ukryciem ORAZ wyłączeniem
skupionego przycisku. `SetAvailability(..., allowWhileBusy: true)` utrzymuje
Anuluj bez chwilowego wyłączenia. `ownLoginAttempt` powstaje przed await Start,
a `RunSynchronous` osłania trzy synchroniczne przyciski po Dispose właściciela.
`LifecycleCases.cs` jest w domyślnym przebiegu harnessu (łącznie116sprawdzeń).
`--focus-cases --busy-only` mierzy oczekiwanie z prawdziwym Keyboard.FocusedElement.
Żywy NVDA na próbnym modalu zweryfikował pojedyncze komunikaty, Tab/Enter/Escape,
powrót do właściciela oraz brak odebrania fokusu innemu oknu przy zakończeniu
w tle. To nie test konta Sonos ani docelowego MainWindow; integracja nadal osobno.

## Sonos: podłączenie okna konta do AMC (menu, paleta, właściciel)

`Windows/Services/SonosAccountOwner.cs` to JEDEN aplikacyjny właściciel trójki
klient+koordynator+magazyn DPAPI na całe uruchomienie. Jest LENIWY: `MainWindow`
tworzy sam obiekt właściciela, ale koordynator, klient HTTP i magazyn powstają
dopiero przy JAWNYM otwarciu konta, więc start AMC i cudze testy nie czytają
konta ani nie wysyłają żądań. `EnsureCoordinator` robi `RestoreOnce` PRZED
pierwszym pokazaniem okna i tylko raz — drugie otwarcie dostaje TEN SAM
koordynator bez ponownego Restore. Zamknięcie okna nie woła Dispose ani Delete;
zwolnienie następuje wyłącznie w faktycznym zakończeniu AMC (obok
`_tidalIntegration.Dispose()`), więc ANULOWANE zamykanie (np. ochrona
nagrywania) nic nie zwalnia. `RebuildCore` i zapis ustawień właściciela nie
odtwarzają.

`SonosAccountPresenter` w tym samym pliku buduje okno i pilnuje, żeby drugie
polecenie NIE zbudowało drugiego okna na tym samym właścicielu (guard bez
drugiego Start i Restore, wraca do otwartego okna). Potwierdzenie wylogowania
idzie przez istniejące `Services/AccessibleDialog.Show(owner, …, YesNo,
Question, MessageBoxResult.No)` z ownerem WŁAŚCIWEGO `SonosAccountWindow`, nie
nieaktywnego okna głównego pod modalem — Nie, Escape i Alt+F4 nie usuwają
konta. Pojedyncza informacja po operacji zostaje w oknie konta; `MainWindow` jej
nie powtarza własnym Announce.

Domyślny zaufany broker to ROOT `https://hermes.tail6caad7.ts.net/`
(`SonosAccountOwner.DefaultBrokerOrigin`); ścieżki `/login/start|result|refresh`
wyprowadza `SonosLoginBrokerConfiguration`. Magazyn używa istniejącego kontraktu
`SonosDpapiCredentialStore.DefaultFilePath`. Żadna wartość konta nie idzie do
`AppSettings`, `state.json` ani logów.

Wejścia użytkownika: pozycja `Plik -> Konto Sonos…` (`ManageSonosConnectionMenuItem`,
bez skrótu i bez litery dostępu — dochodzi się strzałkami) oraz polecenie
`CommandIds.ManageSonosConnection` w istniejącej palecie, przechodzące
prawdziwym routerem i katalogiem. Sesji Sonos jeszcze nie ma, więc nie ruszono
`SessionSlotOrder`, numeracji sesji ani `Ctrl+F5`.

Pomiary: `Core.SmokeTests --sonos-account-command` (droga router/katalog/paleta)
i `Windows.SmokeTests --sonos-account-wiring` (właściciel, RestoreRead 1 przy
2 otwarciach, brak Delete/Dispose po Close, guard, potwierdzenie budowane przez
`AccessibleDialog.CreateForMeasurement` BEZ Show). Oba bez pokazywania GUI.


- `Windows/SonosAccountWindow.xaml(.cs)` — `Controls.AccessibleWindow` na
  ODEBRANYM `SonosAccountCoordinator`. ZERO pól deweloperskich: żadnego Client
  ID, sekretu ani adresu powrotu (`TidalAccountWindow` posłużył za wzór
  STYLISTYKI, jego pola deweloperskie NIE zostały przeniesione). Jedyne pole
  tekstowe okna to instrukcja — mierzone asercją, nie deklaracją.
- Kolejność mowy z BUDOWY okna, nie z opóźnienia: treść stanu i instrukcji to
  `TextBox IsReadOnly` (przeglądalny strzałkami, kopiowalny), `TabIndex=0`,
  fokus startowy ustawiony w konstruktorze przez `FocusManager.SetFocusedElement`
  — nie wiązaniem `FocusedElement`, żeby dał się zmierzyć BEZ `Show`. Przyciski
  mają `TabIndex` 10..70. Żadnego `sleep` ani timera ustawiającego kolejność.
- `AutomationProperties.Name` przycisków BEZ skrótu i bez podkreślnika (czytnik
  ogłasza klawisz dostępu sam; dopisek brzmiałby jak podwojenie).
- Logowanie: `BeginLoginAsync` → `AuthorizeUri` z WYNIKU Start → wstrzyknięty
  `Func<Uri,bool>`; domyślny otwieracz przepuszcza tylko adres zgodny z
  ISTNIEJĄCĄ `SonosAuthorizeUrlPolicy` (nigdy adresu od użytkownika) i nie
  cytuje URI w komunikacie. Potem jawne `Sprawdź logowanie` — DOKŁADNIE jedno
  `CheckLoginAsync` na kliknięcie, bez pollingu i timera.
- `DescribeCheck` nie zamienia każdego `StillWaiting` w „dokończ w
  przeglądarce”: `Pending` kieruje do przeglądarki, ale `ProofMismatch` i błąd
  przejściowy ogłaszają WŁASNĄ przyczynę plus możliwość ponowienia. `WriteFailure`
  to „działa, ale nie zapisane” + `Ponów zapis logowania` (0 HTTP), nigdy
  „Wylogowano”. Nieudane `Delete` to jawnie NIEDOKOŃCZONE wylogowanie.
- Przycisk bez warunków jest `Collapsed`, nie martwy: nie kosztuje tabulacji.
  `Zamknij` i `Anuluj logowanie` działają także w trakcie zajętości; `busy`
  blokuje wyłącznie duplikaty operacji.
- WŁAŚCICIELEM koordynatora jest aplikacja: `ShutdownOwnWork` anuluje TYLKO
  własną próbę logowania i własny token okna — bez `Dispose` i bez `Disconnect`,
  więc tokeny we wspólnym koordynatorze zostają. `windowLifetime` jest
  anulowany, ale ROZMYŚLNIE nie zwalniany (trwająca operacja trzyma token
  powiązany z tym źródłem). Spóźniona kontynuacja po zamknięciu nie rusza UI,
  nie ogłasza i nie wskrzesza okna; wynik async nigdy nie odbiera fokusu
  przeglądarce. Brak `Wait`/`Result`.
- Caller woła `RestoreOnce` PRZED otwarciem; okno czyta tylko `Snapshot` i NIE
  sięga do produkcyjnego magazynu z bezparametrowego konstruktora (takiego
  konstruktora nie ma). Potwierdzenie wylogowania to wstrzyknięty `Func<bool>`
  (bez wstrzyknięcia = BRAK zgody), nie nowy dialog ogólny; wylogowanie nie jest
  domyślną akcją Enter.
- `tests/SonosAccountWindowHarness/` — `run.sh` buduje w WSL (`UseWPF`,
  `EnableWindowsTargeting`, `net8.0-windows`) i uruchamia na Windows. Projekt
  LINKUJE źródła (`Core/Sonos/*.cs`, `Controls/AccessibleWindow.cs`,
  `Controls/AccessibleStatusTextBlock.cs`, nowe XAML i kod) — bez
  `ProjectReference` do Core/SQLite i bez `MainWindow`. Przy
  `EnableDefaultCompileItems=false` SDK nie dołącza `GlobalUsings.g.cs`, dlatego
  jest własny `GlobalUsings.cs`; wewnętrzne fabryki wyników i `SonosLoginSession`
  są tworzone Reflection, żeby NIE poszerzać widoczności produktu dla pomiaru.
- Pierwotne 94 sprawdzenia, 94 zaliczone na prawdziwym Windows (kod 0), BEZ `Show`,
  `ShowDialog`, `Activate` i `EnsureHandle`, z testowym sinkiem ogłoszeń zamiast
  czytnika i atrapami przeglądarki/potwierdzenia. Test async w STA ustawia
  `DispatcherSynchronizationContext` i pompuje `DispatcherFrame` z limitem,
  zamiast blokować wątek. Dyskryminację udowodniono: celowe zepsucie okna
  (treść jako edytowalne pole z `TabIndex=99`, skrót dopisany do nazwy) dało
  RED 10 niezaliczonych i kod 1, po cofnięciu znów GREEN 94/94. Pomiar NIE
  dowodzi żywego NVDA ani pierwszej wypowiedzi — okna nie pokazywano.
- `--show-fixture`: pokazuje rzeczywiste okno na
  jawnie nazwanych danych próbnych (`Fakes`, origin `.invalid`), właściciel
  stub, syntetyczna bramka i magazyn w pamięci, testowy `shellOpen` tylko
  zapisujący adres; zero IPC, aktualizacji, audio i sieci. Tytuł jednoznaczny
  („AMC PROBA A11Y…”), `ShowInTaskbar=true` bez ownera, kwity PID/HWND/gotowe i
  liczniki operacji w NOWYM katalogu na każde odtworzenie (nie kasuje
  poprzednich), watchdog zamyka WYŁĄCZNIE swoje okno.
- Okno NIE jest jeszcze podłączone do `MainWindow`, menu, skrótów globalnych ani
  konfiguracji produkcyjnej — to osobny następny krok. Instrukcja użytkownika:
  `TESTY_SONOS_KONTO_PL.md`.

## Procenty bufora transmisji (TimeShift)

- `RadioMediaOutput.TryGetBufferedSeekPosition` odczytuje pod blokadą rzeczywisty zakres bufora i zwraca bezwzględną pozycję strumienia. Uwzględnia częściowe zapełnienie oraz nadpisanie najstarszych danych.
- `MainWindow.ExecuteCommandCore` kieruje istniejące polecenia `transport.seekPercent.*` radia przez `DemoMediaSession.SetPosition` i zwykły tor przewijania/tempa. Nie zmienia mapowania klawiszy ani dekoderów.
- `TimeshiftPercentageTests` sprawdza próbki dźwięku i rzeczywiste okno; `--timeshift-percent`, `--timeshift-percent-ui`. Prywatna sonda `--timeshift-percent-nvda <katalog>` oczekuje na klawisze żywego czytnika i zapisuje pozycję bez nagrywania ani dostępu do kont.

## Zachowuj kopie po edycji (ustawienie globalne)

- `Core/Configuration/AppSettings.cs`: `KeepAudioEditBackups`, domyślnie `false`. Stary zapis bez tej własności też daje `false`, więc aktualizacja niczego nie włącza po cichu.
- `Core/Configuration/SettingsTarget.cs`, `Commands/CommandIds.cs` (`settings.editing.keepBackups`), `Commands/CommandCatalog.cs`, `Commands/CommandRouter.cs`, `Presentation/CommandPaletteSearch.cs`: ustawienie jest do znalezienia w palecie i prowadzi wprost do własnego pola, a opis pokazuje bieżący stan.
- `SettingsWindow.xaml(.cs)`: `KeepAudioEditBackupsCheck` w zakładce Ogólne, między pamięcią pozycji a długością przeskoku. `AutomationProperties.Name`/`HelpText` mówią, czego ustawienie dotyczy (cięcie i dopisywanie do istniejącego pliku), czego NIE dotyczy (zapis do nowego pliku) i że nie sprząta kopii utworzonych wcześniej ani przy starcie.
- `AudioClipAppendWindow.xaml.cs`: ostatni opcjonalny `bool keepBackup = false` w konstruktorze, przekazany do `AudioClipAppender.AppendAsync` argumentem nazwanym `keepBackup`. `DescribeBackupOutcome` buduje komunikat z RZECZYWISTEGO `BackupPath`, więc nie obiecuje kopii, której nie ma; niepusta ścieżka przy wyłączonej opcji jest opisana jako kopia, której nie usunięto.
- `AudioEditBackupSettingsTests`: domyślna wartość również przy starym JSON, zapis i odczyt obu wartości, `CloneState`, wyszukiwalność, kontrolka odtwarzająca wartość, dostępność i kolejność tabulacji, prawdziwy Zapisz i Anuluj. Tryby `RunModel` (bez okien) i `RunControls` (okno bez `ShowDialog`) pozwalają mierzyć część twierdzeń poza pełnym GUI.

Bez tego ustawienia kopia z danej edycji jest usuwana po sprawdzeniu pliku wynikowego. Przy błędzie albo niepewności zostaje NIEZALEŻNIE od ustawienia.


## Powielanie harmonogramu (alfa407)

- `Core/Configuration/AppSettings.cs`: opcjonalna nazwa planu `RadioRecordingScheduleSettings.Name`, niezależna od stacji i szablonu pliku. Stary zapis bez nazwy nadal używa nazwy stacji.
- `RadioSchedulesWindow.xaml(.cs)`: Ctrl+D tylko na liście i przycisk Powiel; `DuplicateSelected`, `NextCopyName`, `DisplayName`; nowy ID i wyłączona kopia, głębokie ActiveDays.
- `RadioScheduleEditorWindow.xaml(.cs)`: pole Nazwa planu, zapis i odtworzenie. `MainWindow.CloneRadioSchedule` przenosi nazwę w rzeczywistym zapisie menedżera.
- `RadioScheduleCopyTests`: prawdziwe okna i klawisze, stary JSON, edycja, zapis i ponowny start, brak powrotu usuniętej kopii. `--schedule-copy-nvda <katalog>` jest izolowaną sondą samego okna bez silnika nagrywania.
- `scripts/test-schedule-copy-mutations.py`: kontrolowane błędy dla sprawdzenia skuteczności testów.


## Powiązania Spotify (alfa406)

`MainWindow.xaml.cs`, `ShowSpotifyRelationsMenu`: album pod prawą strzałką prowadzi bezpośrednio do wykonawcy; wykonawca pokazuje Albumy/Utwory. Enter albumu pozostaje osobną drogą otwarcia zawartości. `SpotifyRelationsAcceptanceTests` mierzy prawdziwe handlery, API z transportem próbnym i powrót. `--spotify-relations-nvda <katalog>` otwiera izolowane prawdziwe okno na jawnych danych próbnych. `scripts/test-spotify-relations-mutations.py` sprawdza rozróżnianie błędnych tras.


## Fragmenty audio (alfa405)

- `MainWindow.xaml.cs`: `TryResolveClipFileShortcut`, `AppendClip`, `ExportClip`; I/O zaznacza, Ctrl+S eksportuje, Ctrl+D dopisuje.
- `AudioClipAppendWindow.xaml/.cs`: wybór istniejącego celu, zgoda na ponowną kompresję, postęp i anulowanie.
- `Services/AudioClipAppender.cs`: eksport zakresu, dopisanie, sprawdzenie długości/zmiany celu, kopia i podmiana; korzysta z istniejącego eksportera.
- `AudioClipShortcutAcceptanceTests.cs`, `AudioClipAppendTests.cs`: rzeczywisty routing okna i pliki audio. `scripts/test-audio-clips-mutations.py` sprawdza dyskryminację testów.
- `TESTY_FRAGMENTY_AUDIO_405_PL.md`: instrukcja sprawdzenia klawiszami.


Ten plik odpowiada na jedno pytanie: **gdzie w kodzie leży dana funkcja programu**.
Nie opisuje planów ani decyzji projektowych — te są w `MEDIA_CONTROLLER_PL.md`,
`PROJEKT_TIDAL_PL.md`, `PROJEKT_NVDA_PL.md` i pozostałych `PROJEKT_*`.

Stan na wersję `0.1.0-alpha.387` (commit d7ee353).
Zmierzone na drzewie źródeł, nie przepisane z dokumentacji.

## Uzupełnienie: pliki historii nagrań, alfa 404

- `Windows/MainWindow.RecordingFiles.cs`: dostępność i odczyt nagrania, kontrola Enter, odświeżenie tylko podglądu po zmianie pliku lub powrocie do okna.
- `Core/Presentation/RecordingPathProbe.cs`: pamięć obserwacji ścieżek, wspólny klucz dla równoważnych ścieżek; odróżnienie braku pliku od niedostępnego folderu. Używana w wątku UI.
- `RadioRecordingHistoryPathRewriter.cs` aktualizuje znaną parę stara/nowa ścieżka bez zgadywania po nazwie; `RadioRecordingRowLabels.cs` podaje nazwę, datę i folder na końcu.
- `MainWindow.xaml.cs`: zdarzenia obserwatora przez Dispatcher, deduplikacja bez ukrywania przerwanego lub nieudanego nagrania oraz ponowna kontrola po otwarciu historii.
- `RecordingFilesAcceptanceTests.cs`: save/load, rename/delete/restart, cache, niedostępny folder, zachowanie powrotu z podglądu w siedmiu sesjach; runner `--recording-files-acceptance` i pełny zestaw Windows.
- `scripts/test-recording-files-mutations.py`: izolowane celowe uszkodzenia reguł z pełnym licznikiem wykonanych przypadków.

## Uzupełnienie: tańsza migawka stanu (CloneState), alfa 407

- `Core/Configuration/StateSnapshotCopier.cs`: odłączona kopia modelu konfiguracji bez obiegu JSON. Dla każdego typu raz buduje i zapamiętuje skompilowany plan: głęboko dla obiektów, list i słowników (z zachowaniem komparatora), wprost dla wartości niezmiennych. Obsługuje DOKŁADNIE kształty aktualnego modelu — klasy danych z konstruktorem bezparametrowym, `List<T>`, `Dictionary<TKey, TValue>` o niezmiennym kluczu. Inna kolekcja, tablica, słownik o mutowalnym kluczu i typ bez konstruktora bezparametrowego są ODRZUCANE wyjątkiem, nie kopiowane płytko. Brak cykli jest założeniem o modelu pilnowanym testem, nie ochroną w czasie działania.
- `Core.SmokeTests/CloneStateCostTests.cs`: budżet alokacji `CloneState` (mediana z 5 prób, 6 MiB), pełna zgodność JSON na bogatym i pustym modelu, niezależność w OBU kierunkach, zachowanie komparatorów, odmowa nieobsługiwanych kształtów z kontrolą pozytywną oraz zapis i ponowny odczyt z usunięciami i kolejnością. Strażnik przechodzi cały model refleksyjnie i ZGŁASZA BŁĄD (nie cichy skip) przy nieznanym kształcie, nieznanym kluczu słownika, przekroczeniu głębokości i właściwości bez publicznego ustawiacza spoza kontrolowanej listy. Argument `--clone-state-cost` oraz pełny zestaw Core.

## Uzupełnienie: okresowy zapis pozycji, alfa 402

- `Windows/Services/StatePersistenceQueue.cs`: odłączona migawka pełnego stanu należąca wyłącznie do workera; pełny zapis unieważnia wcześniejsze oczekujące checkpointy. `Flush` nadal wykonuje końcowy pełny zapis i zgłasza jego błąd.
- `Windows/Services/PlaybackStateCheckpoint.cs`: odłączone pozycje lokalne, postęp odcinków roboczych i opcje/pozycje Spotify, bez kopiowania katalogów. Nakładanie nie tworzy usuniętych rekordów; scalanie zachowuje wcześniejsze odcinki spoza najnowszego zestawu roboczego.
- `Windows/MainWindow.PlaybackCheckpoint.cs`, `MainWindow.xaml.cs` i `MainWindow.SpotifyOptions.cs`: okresowe zapisy nadal z progiem15s; lokalny zapis nie przebudowuje niezmienionej listy rekordów. Zmiany katalogów i końcowe zamykanie zachowują pełną ścieżkę.
- `Core/Configuration/ConfigurationStore.cs`: wspólne `CreateStateShell` pomija dane SQLite przed serializacją JSON, bez dodatkowej głębokiej kopii już odłączonego stanu. `CloneState` nadal daje niezależną pełną kopię.
- `PeriodicPlaybackCheckpointTests`, `PlaybackCheckpointQueueTests`: rzeczywiste handlery timerów i kolejka, porównanie całości danych po zapisie/odczycie, usuwanie, przeplatanie, awarie i zamknięcie kolejki. Argument `--periodic-playback-checkpoint` oraz pełny zestaw Windows.
- `scripts/test-playback-checkpoint-mutations.py`: odizolowane celowe uszkodzenia i niezmienny licznik przypadków; nie modyfikuje repozytorium wejściowego.

## Uzupełnienie: natywne polecenia czytnika, alfa 401

- `Windows/MainWindow.xaml.cs`: wspólne `IsNativeReaderReadingKey` pozostawia NVDA+góra i NVDA+End czytnikowi w trzech ścieżkach klawiatury. Nie wywołuje informacji o odtwarzaniu i nie zmienia głośności. Zwykła strzałka w odtwarzaczu pozostaje aktywna.
- `Windows.SmokeTests/NativeReaderGesturePassThroughTests.cs`: rzeczywiste handlery okna, WPF i transportu, siedem sesji, brak zapowiedzi i zmian stanu po gestach czytnika; oddzielna kontrolka zwykłej strzałki. Argument `--reader-native-gestures` i pełny zestaw.
- `nvda-addon/tests/test_gesture_scope.py`: wyłącznie kombinacje Ctrl+Windows w dodatku; brak appModule zastępującego polecenia NVDA. Testy kodu nie zastępują zapisu rzeczywistej mowy czytnika.

## Uzupełnienie: dynamiczne nazwy menu bez powtarzania skrótów

- `Windows/MainWindow.xaml.cs`: `SetMenuItemNameAndShortcut` zachowuje osobny Header, Name oraz AcceleratorKey/InputGestureText. `UpdateFileMenuForCurrentSession` i gałąź WiiM w `PlayerContextMenu_Opened` nie wpisują skrótu ponownie do nazwy po normalizacji.
- `Windows.SmokeTests/DynamicMenuShortcutNameTests.cs`: rzeczywiste końcowe właściwości czterech pozycji, wszystkie sesje i jawny powrót WiiM → Pliki lokalne; dokładne nazwy/etykiety, zachowane skróty, zbiorczy wynik. Runner `--dynamic-menu-shortcut-names` oraz pełny zestaw.

## Uzupełnienie robocze: Enter a Biblioteka usług

- `Core/Podcasts/OpenedSearchResultLibraryPlan.cs`: wspólna decyzja o dodaniu otwartego wyniku radia, TIDAL i Spotify; tryb bez dodawania nie uruchamia zapisów, ponowne otwarcie nie usuwa członkostwa.
- `Windows/MainWindow.OpenedSearchResultLibrary.cs`: wykonanie planu przez zapis stacji lub istniejących klientów kolekcji usług; publiczny odcinek YouTube awansuje z podglądu dopiero po zaakceptowanym otwarciu, nie podczas przygotowania wyniku ani pauzy. `SearchResultOpenWithoutLibraryTests` obejmuje rzeczywiste polecenia pauzy, wznowienia ON/OFF i zachowania wcześniejszego członkostwa.
- `Windows/MainWindow.xaml.cs`: podłączenie w `ShowSearch` i `ExecuteSearchResultAction`; odmowa aktywacji i pauza już grającego wyniku nie uruchamiają automatycznego dodania.
- `OpenedSearchResultLibraryTests`: rzeczywiste okno wyszukiwania, oba ustawienia dla trzech usług, odróżnienie rozpoczęcia od pauzy i odmowy. Sieć zastąpiona transportem testowym, dane i integracja pulpitu odizolowane.
- To kod kandydata, nie opis zainstalowanego wydania.

## Uzupełnienie robocze: zakres menu sesji

- `Windows/MainWindow.xaml`: „Zapisane podcasty Spotify” przeniesione do Widok, z zachowaniem handlera i Ctrl+Alt+O; nazwane pole strumienia i separator przed Ustawieniami.
- `Windows/MainWindow.xaml.cs`, `UpdateFileMenuForCurrentSession`: zwykły strumień widoczny tylko w Radiu internetowym; brak pustych i podwójnych separatorów w pozostałych sesjach.
- `Windows.SmokeTests/SessionMenuScopeTests.cs`: rzeczywiste menu WPF wszystkich sesji, rodzic pozycji podcastów, widoczność, separatory oraz brak skrótu w nazwie UIA; runner `--session-menu-scope`.

## Uzupełnienie robocze: presety i powtórne uruchomienie

- `Windows/MainWindow.xaml.cs`, `ActivatePreset`: bieżący utwór/stacja/odcinek zachowuje kolejkę, a pauza wznawia pozycję. Wyjątek TIDAL jest kierowany do oryginalnego programu, nie do SDK próbek.
- `Windows/MainWindow.TidalDesktop.cs`, `TryPlayTrackInTidalDesktop`: wspólna bramka Enter/preset. Potwierdzone ponowienie bieżącego utworu nie nadpisuje kontekstu presetów. Skrót w tle nie otwiera pytania o restart.
- `Windows/Services/TidalDesktopController.cs`, `ITidalDesktopPlayback`: granica sterowania używana również przez izolowane testy okna; wynik odróżnia nowy start od już załadowanego utworu.
- `Core/Tidal/TidalDesktopPlaybackPlan.cs`: aktualny wiersz bez przycisku może korzystać ze stopki tylko po zgodności identyfikatorów. Play wznawia, Pause nie jest klikane; brak kontrolki nie oznacza odmowy usługi.
- Testy: `TidalPresetRoutingTests`, `TidalDesktopPlaybackTests` oraz `tests/tidal-preset-dom.test.cjs`. Ten ostatni przyjmuje plik wyrażenia wyeksportowany przez runner Windows z `--tidal-track-expression <plik>` i wykonuje je w Node na jawnym kontrakcie DOM; nie zastępuje próby oryginalnego TIDAL-a.

## Uzupełnienie robocze: preset albumu Spotify

- `Windows/MainWindow.SpotifyAlbumPreset.cs`: odtwarzanie albumu z presetu przez istniejące pobieranie i rejestrację kontenera, bez nawigacji do jego widoku; filtr grywalności, potwierdzenie nazwy oraz wznowienie już aktywnego albumu.
- `Windows/MainWindow.xaml.cs`, `ActivatePreset`: osobna gałąź albumu Spotify i unieważnienie starszego pobrania albumu przez nowy preset utworu.
- `Windows/MainWindow.SpotifyBrowse.cs`: domyślnie nieaktywne punkty podstawienia HTTP i tokenu do testów, bez zastępowania parsowania klienta. Po scaleniu istnieją oba szwy: gotowy transport i token (`SpotifyHttpClientForTests`, `SpotifyAccessTokenForTests`) oraz ich odpowiedniki fabryczne (`SpotifyApiHttpClientFactoryForTests`, `SpotifyAccessTokenFactoryForTests`) dla testów wielu kolejnych pobrań na wspólnym handlerze.
- `Windows.SmokeTests/SpotifyAlbumPresetTests.cs`: rzeczywisty handler, jawne atrapy HTTP/dźwięku, powtórzenie, pauza, brak grywalnych utworów oraz spóźnione odpowiedzi. Nie zastępuje pomiaru rzeczywistego fokusu, mowy NVDA ani konta Spotify.

## Uzupełnienie: konto Spotify, alfa 398

- `Windows/MainWindow.xaml.cs`: Ctrl+F5 otwiera bezpośrednio `SpotifyAccountWindow`, niezależnie od aktywnego silnika.
- `Windows/SpotifyAccountWindow.xaml(.cs)`: osobny przycisk parowania wyłącznie dla Librespot, blokowany podczas operacji konta.
- `Windows/MainWindow.SpotifyLibrespotAccount.cs`: parowanie jako okno podrzędne konta katalogu; powrót bez ponownego tworzenia konta i bez przestawiania fokusu na główne okno.
- `Core/Spotify/SpotifyLibraryWriteClient.cs`: komunikat braku zgody kieruje do rzeczywiście dostępnego logowania i odróżnia je od parowania.
- Testy: `SpotifyAccountRoutingTests` (obie implementacje odtwarzacza, rzeczywiste okna i powrót), `SpotifyLibrespotAccountWindowTests` i `SpotifyMembershipWriteTests`.

## Uzupełnienie: Spotify, alfa 394

Poniższy spis rozmiarów pozostaje historycznym pomiarem alfy 387.

- `Windows/MainWindow.SpotifyOptions.cs`, `Core/Spotify/SpotifyPlaybackSettingsResolver.cs`: opcje i trwała pamięć pozycji, bez martwych pól DSP.
- `Windows/MainWindow.SpotifySearch.cs`: zdalne Ctrl+F; `Services/SpotifyApiClient.cs`: odczyt katalogu i relacji wykonawca/album.
- `Windows/MainWindow.SpotifyBrowse.cs`: zawartości oraz menu relacji pod strzałką w prawo.
- `Core/Spotify/SpotifyLibraryWriteClient.cs`, `SpotifyCollectionSemantics.cs`: zapis biblioteki, odczyt potwierdzający, uprawnienia i częściowe awarie.
- `SessionManager` może zachować istniejącą sesję Spotify przy odbudowie ustawień; `DemoMediaSession.ConfigureRememberPositionPolicy` przepina wyłącznie regułę pamięci.
- `InformationWindow`: właściwości domyślnie z natywnym kursorem, przełącznik tekst/dokument. Standardowe polecenia NVDA pozostają w czytniku; dodatek AMC nie instaluje appModule.
- `Windows/MainWindow.SpotifyLibrespot.cs`: podłączenie dodatkowego silnika, kolekcji i okna wyjścia; `MainWindow.SpotifyLibrespotAccount.cs`: odrębne parowanie oraz przejście do wspólnego konta katalogu.
- `Windows/Services/SpotifyLibrespotAuthenticationService.cs` i `SpotifyLibrespotCredentialStore.cs`: device flow, odświeżanie i osobny zapis poświadczeń, bez zastępowania tokenów SDK.
- `Windows/SpotifyLibrespotAccountWindow.xaml(.cs)`: dostępne okno kodu, adresu, potwierdzenia i anulowania; testy w `SpotifyLibrespotAccountWindowTests.cs`.
- `Core/Spotify/LibrespotHostClient.cs`, `Windows/Services/SpotifyLibrespotMediaOutput.cs`, `native/AmcSpotifyLibrespotHost`: transport, adapter sesji i proces odtwarzania Rust; scenariusze cyklu życia i regresji są w testach Windows.
- `SPOTIFY-LOSSLESS-I-MONITORING.md`: źródła, granice Lossless i Librespot; harmonogram i kolejność wdrożenia pozostają w `PLAN-16-09-2026.md`.

## Uzupełnienie: tempo TimeShift, alfa 396

- `Core/Playback/IPlaybackRateStateOutput.cs`: opcjonalny odczyt przyjętego tempa; `DemoMediaSession` korzysta z niego zamiast potwierdzać samo żądanie.
- `Windows/Services/TimeshiftTempoStage.cs`: zmiana tempa za buforem, konwersja PCM16 do float32, ochrona zapasu, natychmiastowy odczyt ustawienia oraz wspólna blokada odczytu, przewijania i zwalniania zasobów.
- `Windows/Services/RadioMediaOutput.cs`: wpięcie etapu bez zmian dekoderów; możliwości zależne od rzeczywistego potoku, wyczyszczenie starych próbek po Seek/End i przekazanie powiadomienia o normalnym tempie.
- `Windows/MainWindow.xaml.cs`: wspólny próg live i komunikat `NormalTempoResumed`; `SettingsWindow.xaml`: rzeczywiste skróty TimeShift.
- Testy: `PlaybackRateStateTests`, `TimeshiftRateHelpTests`, `TimeshiftTempoAudioTests`, `TimeshiftRateIntegrationTests` i `TimeshiftTempoLifetimeTests`. Ostatnie mierzą blokady oraz długie okno po rozgrzewce; testy integracyjne używają rzeczywistego bufora radia.

## Uzupełnienie robocze: jedna sesja Spotify i podcasty

- `Core/Spotify/SpotifySessionMigration.cs`: wersjonowane scalenie danych i identyfikatorów; `SpotifyPlaybackEngine.cs`: wybór Librespot/SDK i konwerter JSON. `ConfigurationStore.IsPersistedAudioSession` zachowuje stare klucze wyjścia i wyciszenia do tej migracji. `MainWindow.RestoreSpotifyCachedItems` odtwarza także zapisaną kolejkę, nie tylko katalog.
- `Core/Sessions/SessionManager.cs`: jedna sesja `spotify`, wybór rzeczywistego wyjścia, zachowanie numerów i ruch przez luki. `SessionSelectionWindow.xaml.cs`: numery wierszy pobrane ze słownika sesji.
- `Windows/SettingsWindow.xaml(.cs)`: wybór odtwarzacza Spotify obowiązujący po restarcie.
- `Windows/MainWindow.SpotifyPodcasts.cs`: widok zapisanych podcastów i tekst opisu; `MainWindow.xaml.cs`: Ctrl+Alt+O, Alt+D, powrót od odcinka przez kontener Podcast i polecenie GoToPodcast.
- `Windows/MainWindowShortcutRouter.cs`: wspólna decyzja Alt+D; `Core/Presentation/CommandPaletteSearch.cs`: skróty widoczne w pomocy i palecie.
- Testy: `SpotifySingleSessionEngineTests`, `SessionSlotGapsAndEngineTests`, `SpotifyStartupEngineTests`, `SpotifyDescriptionAndSlotUiTests`, `SpotifyPodcastParentTests`, `SpotifyMigrationReviewTests` oraz `SpotifyEngineSettingsTests`.
- Stan odbioru, w tym otwarta kontrola przywracania kolejki: `PLAN-16-09-2026.md`. Ta sekcja opisuje kod roboczy, nie opublikowane wydanie.

## Uzupełnienie robocze: opcje każdej sesji z Ustawień

- `Windows/SessionPlaybackOptionsEditor.cs`: wspólne możliwości, utworzenie okna i zapis opcji dla bezpośredniego Ctrl+Alt+Enter oraz sesji zaznaczonej w Ustawieniach.
- `Windows/SettingsWindow.xaml(.cs)`: wybór sesji bez jej aktywacji, edycja na roboczej kopii, zewnętrzne Zapisz i Anuluj.
- `Core/Podcasts/PodcastPlaybackSettingsResolver.cs`: pamięć odcinka → podcastu → jawna opcja sesji → dotychczasowe domyślne pamiętanie; przetwarzanie dźwięku uwzględnia sesję przed ustawieniami ogólnymi.
- `MainWindow.CapturePodcastState` i `ShouldRememberPodcastPosition` stosują tę samą regułę; `ItemPlaybackOptionsWindow` opisuje rzeczywiste dziedziczenie.
- Testy: `SessionOptionsInSettingsTests`, `PodcastSessionResumeCaptureTests` oraz przypadek niezależności podcastów od przełącznika lokalnych plików w Core.
- To mapa kodu roboczego, nie potwierdzenie wydania. Odbiór prowadzi `PLAN-16-09-2026.md`.

## 1. Rozmiar i podział

Dwa projekty C# plus dodatek NVDA w Pythonie.

- `src/AccessibleMediaController.Core` — 95 plików, ok. 18 700 linii.
  Logika bez Windows: model danych, reguły, formatowanie tekstu dla czytnika.
  Tu trafia wszystko, co da się przetestować bez uruchamiania okna.
- `src/AccessibleMediaController.Windows` — 125 plików, ok. 55 900 linii.
  Okna WPF, odtwarzanie dźwięku, sieć, integracje, mostek NVDA.
- `nvda-addon/` — dodatek do NVDA (Python), wersja manifestu 0.3.1, 66 poleceń.
- `tests/` — dwa projekty testów dymnych, uruchamiane z `build.ps1`.

Najcięższy plik w całym repo: `MainWindow.xaml.cs`, 23 794 linie, 837 metod.
Osobny punkt niżej opisuje, jak się w nim poruszać.

## 2. Start programu i gdzie leżą dane użytkownika

`src/AccessibleMediaController.Windows/App.xaml.cs` (317 linii) — cały rozruch.

- Mutex jednej instancji, zdarzenie aktywacji okna z drugiego uruchomienia.
- Licznik bicia serca interfejsu i watchdog zawieszenia (timer + raport problemu).
- Ustala katalogi i tworzy `ConfigurationStore`, potem `MainWindow`.
- Po pokazaniu okna startuje aktualizacje składników zewnętrznych.

Katalogi danych (ustalane właśnie tutaj):

- `%APPDATA%\AccessibleMediaController\state.json` — ustawienia i stan.
- `%LOCALAPPDATA%\AccessibleMediaController\library.db` — biblioteka lokalna (SQLite).
- `%LOCALAPPDATA%\AccessibleMediaController\podcasts.db` — podcasty (SQLite).
- `%LOCALAPPDATA%\AccessibleMediaController\logs\amc.log` — log, rotacja po 5 MB,
  pięć plików wstecz (`amc.1.log` … `amc.4.log`). Kod: `Services/DiagnosticLog.cs`.

## 3. Polecenia i skróty klawiszowe

Jedna komenda ma identyfikator tekstowy, a skrót to tylko przypisanie do niego.

- `Core/Commands/CommandIds.cs` — 191 stałych z identyfikatorami poleceń.
  Zaczynasz zawsze tutaj, gdy dodajesz nową funkcję wywoływaną skrótem.
- `Core/Commands/CommandCatalog.cs` — katalog poleceń; sprawdza przez refleksję,
  czy każde `CommandIds` jest opisane. Brak opisu wychodzi w teście, nie po cichu.
- `Core/Commands/CommandRouter.cs` — interfejsy `IApplicationActions`
  i `IAnnouncementSink` oraz kierowanie polecenia do działania.
- `Core/Input/KeyChord.cs`, `KeyModifiers.cs` — reprezentacja skrótu i jego
  postać kanoniczna (po niej porównujemy).
- `Core/Input/KeyboardProfile.cs` — profile klawiatury; `CreateDefault()` zawiera
  wszystkie skróty domyślne (cyfry to sesje, spacja pauza, strzałki przewijanie
  i głośność itd.).
- `Windows/MainWindowShortcutRouter.cs` — skróty zależne od kontekstu: co robi
  Alt+cyfra w zależności od bieżącej sesji i widoku.
- `Windows/MainWindow.TransientPreviews.cs` — **wspólne podglądy** Alt+R
  (nagrywane stacje), Alt+Shift+R (historia nagrywania) i Ctrl+I (nowe odcinki).
  Jeden router dla wszystkich trzech: routing skrótu, dostępność w sesji, opis
  pomocy, zapamiętanie miejsca wywołania i powrót Escape. To NIE są skróty
  systemowe (`RegisterHotKey`) — działają w obrębie okna, ale ze WSZYSTKICH
  sesji AMC, gdy włączony jest przełącznik `AppSettings.GlobalTransientPreviews`.
  Czysta polityka i pamięć powrotu siedzą w
  `Core/Presentation/TransientPreviewNavigation.cs` (testowalne bez WPF).
  Odbiór prawdziwych poleceń, powrotu, fokusu oraz ochrony odsłuchu:
  `Windows.SmokeTests/TransientPreviewAcceptanceTests.cs`; świadome mutacje:
  `scripts/test-transient-preview-mutations.py`. Samo badanie polityki nie
  zastępuje sprawdzenia wcześniejszych bramek WiiM i pól edycji.
- `Windows/Services/WindowsKeyMap.cs` — tłumaczenie nazw klawiszy na kody Windows.
- `Windows/Services/GlobalPrefixService.cs` — skróty globalne poza oknem programu:
  `RegisterHotKey` plus niskopoziomowy hak klawiatury. Obsługa: `HandleGlobalChord`
  w `MainWindow.xaml.cs` (linia ok. 10420).
- `Core/Presentation/ShortcutHelpCatalog.cs` — treść okna pomocy skrótów.
- `Windows/ShortcutCaptureWindow.xaml.cs` — okno przechwytywania nowego skrótu.

## 4. Sesje: czym są zakładki Tab / Shift+Tab

`Core/Sessions/SessionManager.cs` trzyma listę sesji i kolejność slotów.

Sesje wbudowane powstają w `CreateDemoSessions`: `tidal`, `appleMusic`, `wiim`.
Sesje realne dokłada `MainWindow` przez `AddOrUpdateTransientSession`
(w okolicy linii 9344–9440 `MainWindow.xaml.cs`):

- `local` — „Pliki lokalne”
- `radio` — „Radio internetowe”
- `podcasts` — „Podcasty i YouTube”
- `wiim` — „WiiM”

`Core/Sessions/DemoMediaSession.cs` to model pojedynczej sesji: lista elementów,
bieżąca pozycja, prędkość (0,50–2,00), wyciszenie, kolejka.
`Core/Sessions/MediaItem.cs` to pojedynczy element (utwór, album, playlista, stacja).
`Core/Sessions/TransientQueuePersistence.cs` zapisuje kolejkę między uruchomieniami.

## 5. Odtwarzanie dźwięku

Wspólny kontrakt: `Core/Playback/IMediaOutput.cs`. Trzy implementacje:

- `Windows/Services/WindowsMediaOutput.cs` (74 KB) — pliki lokalne i podcasty,
  NAudio, zmiana prędkości przez SoundTouch, przetwarzanie dźwięku.
- `Windows/Services/RadioMediaOutput.cs` (68 KB) — strumienie radiowe.
- `Windows/Services/TidalMediaOutput.cs` (31 KB) — TIDAL przez WebView2.

Wokół nich:

- `FfmpegLocalAudioWaveStream.cs`, `FfmpegRadioWaveProvider.cs` — dekodowanie FFmpeg.
- `BassRadioWaveProvider.cs` — ścieżka przez BASS (`third_party/BASS/win-x64`).
- `LegacyIcyMp3StreamReader.cs`, `LegacyIcyAudioStream.cs` — stare strumienie ICY.
- `LiveVorbisWaveProvider.cs`, `NormalizedVorbisWaveReader.cs` — Ogg/Vorbis.
- `PlaybackAudioProcessors.cs` — korekcja, normalizacja, cisza międzyutworowa.
- `AudioOutputDeviceCatalog.cs`, `AudioOutputPauseGuard.cs` — wybór urządzenia.
- `Core/Playback/ResumePositionPolicy.cs`, `PlaybackVolumeMemory.cs` — pamięć
  pozycji i głośności; `PlayerExitPausePolicy.cs` — co się dzieje przy wyjściu.

## 6. Radio internetowe

Największy pojedynczy obszar funkcji w `MainWindow` (linie ok. 15000–16000
i 21000–22000). Serwisy:

- `RadioStreamResolver.cs` — ustalenie rzeczywistego adresu strumienia.
- `RadioStreamMetadataProbe.cs`, `RadioStreamTitleMetadata.cs` — tytuł utworu ze
  strumienia.
- `RadioBrowserClient.cs` — katalog stacji Radio-Browser.
- `RadioPlaylistImporter.cs`, `RadioLibraryMerge.cs` — import list stacji,
  scalanie z biblioteką.
- Nagrywanie: `RadioMp3Recorder.cs`, `ManualRadioRecorder.cs`,
  `RadioOriginalStreamRecorder.cs`, `RadioRecordingControl.cs`,
  `RadioRecordingStagingStore.cs`, `RadioRecordingFolderResolver.cs`.
- Harmonogram: `ScheduledRadioRecorder.cs`,
  `ScheduledRadioRecordingInterruptionTracker.cs`, `SystemWakeTimer.cs`
  (budzenie komputera), `Core/Configuration/RadioScheduleCalculator.cs`,
  `RadioRecordingFileNameTemplate.cs`.
- Rozpoznawanie utworu: `ShazamTrackRecognitionService.cs`,
  `Core/Presentation/RecognizedTrackLookup.cs`.
- Okna: `RadioStationWindow`, `RadioPresetsWindow`, `RadioPresetAssignmentWindow`,
  `RadioSchedulesWindow`, `RadioScheduleEditorWindow` (38 KB),
  `RadioRecognitionHistoryWindow`.
- Presety pod klawiszami: `Windows/RadioPresetKeyMap.cs`.

## 7. Pliki lokalne

- `Core/LocalMedia/` — reguły wykrywania plików
  (`LocalAudioFileDiscovery.cs`), import (`LocalLibraryImporter.cs`),
  synchronizacja folderów (`LocalLibrarySynchronizer.cs`), wnioskowanie albumu
  (`LocalAlbumInference.cs`), zmiana nazw (`LocalFileRenamePolicy.cs`),
  pliki w chmurze niepobrane (`CloudFileAvailability.cs`), sonda kontenera
  (`MediaContainerProbe.cs`, `Mp3StructureProbe.cs`).
- `LocalFolderPathNormalizer.cs` — jawna pamięć normalizacji ograniczona do jednego
  przebiegu `MainWindow.CaptureLocalMediaState`; używana przez
  `LocalFolderSourcePolicy.IsSameOrDescendant` przy rozstrzyganiu opcji folderu.
- `Core/Configuration/LocalLibraryDatabase.cs` (40 KB) — baza SQLite biblioteki.
- Okna: `LocalSourcesWindow`, `RenameLocalItemWindow`, `UnavailableLocalItemsWindow`.

## 8. Podcasty i YouTube

- `Core/Podcasts/` — 17 plików: parser kanału (`PodcastFeedParser.cs`),
  OPML w obie strony (`PodcastOpmlParser.cs`, `PodcastOpmlWriter.cs`),
  rozdziały (`PodcastChapterParsers.cs`), kolejność i stronicowanie odcinków,
  postęp odsłuchu, eksport subskrypcji YouTube.
- `Core/Configuration/PodcastLibraryDatabase.cs` — baza SQLite podcastów.
- Serwisy: `PodcastFeedClient.cs`, `PodcastEpisodeDownloader.cs`,
  `PodcastChapterClient.cs`, `ApplePodcastDirectoryClient.cs`,
  `SpreakerPodcastDirectoryClient.cs`.
- YouTube: `YouTubeSearchClient.cs`, `YouTubeChannelFeedClient.cs`,
  `YouTubeCollectionClient.cs`, `YouTubeSourceResolver.cs`,
  `YouTubeMediaDownloader.cs`, `YouTubeErrorTranslator.cs`.
  Premiery: `YouTubeErrorTranslator.DescribePremiere`, zachowanie tego komunikatu
  w `WindowsMediaOutput.FriendlyPlaybackError`; regresja i jawne próby żywe
  w `tests/AccessibleMediaController.Windows.SmokeTests/YouTubePremiereTests.cs`.
- Okna: `PodcastSourceWindow`, `PodcastOpmlImportWindow`.

## 9. TIDAL

- `Core/Tidal/TidalApiClient.cs` (46 KB) — API konta i katalogu.
- `Core/Tidal/TidalPkce.cs` — logowanie OAuth PKCE;
  `Windows/Services/TidalOAuthClient.cs` — przepływ w oknie.
- `Windows/Services/TidalCredentialStore.cs` — przechowanie poświadczeń.
- `Windows/Services/TidalIntegrationService.cs` — spięcie API z interfejsem.
- `Windows/Services/TidalDesktopController.cs` — sterowanie aplikacją desktopową
  TIDAL; plan i kolejka: `Core/Tidal/TidalDesktopPlaybackPlan.cs`,
  `TidalDesktopTrackQueue.cs`.
- Odtwarzacz w WebView2: `src/AccessibleMediaController.Windows/TidalPlayerHost/`
  (`src/bridge.js`, `src/index.js`, `dist/tidal-player.js`, `index.html`).
  Polityka WebView2: `Services/TidalWebViewPolicy.cs`.
- Części `MainWindow`: `MainWindow.TidalDesktop.cs` (483 linie),
  `MainWindow.TidalArtist.cs`, `TidalInteractionContext.cs`.
- Okna: `TidalAccountWindow`, `TidalPlaylistPickerWindow`.

Uwaga trwała: pełne odtwarzanie TIDAL nie jest rozwiązane, granice opisuje
`PROJEKT_TIDAL_PL.md`. Nie nazywać próbek pełnymi utworami.

## 10. WiiM (urządzenie sieciowe)

- `Core/Devices/WiiM/` — klient HTTP urządzenia (`WiiMDeviceClient.cs`),
  modele API (`WiiMApiModels.cs`, 26 KB), kolejność i zapis list strumieni,
  stan bieżącego źródła.
- Okna: `WiiMDevicesWindow`, `WiiMDevicePresetsWindow`, `WiiMOptionWindow`.
- W `MainWindow` blok linii ok. 17900–19000 to prawie wyłącznie WiiM.

## 11. NVDA — mostek i dodatek

Po stronie programu:

- `Windows/Services/NvdaCommandServer.cs` — serwer nazwanego potoku Windows,
  nazwa `AMC.NVDA.v1.<id sesji>`, tylko bieżący użytkownik.
- `Windows/Services/NvdaNowPlaying.cs` — tekst „co teraz leci” (stacja, utwór,
  wykonawca — bez powtarzania).
- `Windows/Services/NvdaInteractionPolicy.cs` — kiedy wolno mówić.
- `Windows/MainWindow.Nvda.cs` — podpięcie serwera do okna.

Po stronie NVDA (`nvda-addon/addon/`):

- `globalPlugins/amcController/__init__.py` — 66 poleceń użytkownika.
- `globalPlugins/amcController/transport.py` — klient potoku, czyste `ctypes`,
  bez importów NVDA, dzięki czemu da się go testować poza czytnikiem.
- `globalPlugins/amcController/worker.py` — wątek roboczy.
- Dodatek nie zawiera appModule i przypisuje wyłącznie skróty Ctrl+Win; `tests/test_gesture_scope.py` sprawdza ten zakres.
- `manifest.ini` — wersja dodatku; `build.ps1` buduje paczkę `.nvda-addon`.

Dostępność samego interfejsu: `Windows/Controls/AccessibleWindow.cs`,
`AccessiblePlaybackStatusStrip.cs`, `AccessibleStatusTextBlock.cs`,
`ListRefreshFocus.cs`, `ListSelectionRefresh.cs`, `MenuAccessibility.cs`,
`Services/AccessibleDialog.cs`.

## 12. Teksty czytane użytkownikowi

Cały katalog `Core/Presentation/` to formatowanie wypowiedzi, nie widok:

- `MediaItemFormatter.cs` — jak nazywa się element na liście.
- `NowPlayingParts.cs` — części komunikatu o bieżącym odtwarzaniu.
- `QuickMediaInformationFormatter.cs` — szybka informacja o pliku.
- `AudioParametersFormatter.cs` — parametry dźwięku słowami.
- `SeekInputParser.cs` — rozumienie wpisanej pozycji („1:23”, „90”).
- `CommandPaletteSearch.cs` — wyszukiwanie w palecie poleceń.
- `MuteMenuLabels.cs`, `RadioRecordingHistoryLabels.cs`, `ArtistBrowseSection.cs`,
  `ShowInFolderAvailability.cs`, `LocalPlaybackAudioSettingsPresentation.cs`.

## 13. MainWindow.xaml.cs — jak się w nim poruszać

23 794 linie, 837 metod, bez regionów. Podział tematyczny wynika z układu linii
(policzone z nazw metod, więc orientacyjne, ale sprawdzalne):

- 1 000–2 000 — rozdziały nagrania i wycinki dźwięku
- 2 000–3 000 — radio i rozpoznawanie utworu
- 3 000–4 000 — playlisty
- 4 000–5 000 — podcasty
- 5 000–6 000 — WiiM, radio, foldery lokalne
- 6 000–7 000 — odtwarzanie, głośność, ustawienia dźwięku
- 7 000–9 000 — pliki lokalne i podcasty, YouTube
- 9 000–10 000 — stan, zapisy, tworzenie sesji (tu `AddOrUpdateTransientSession`)
- 10 000–11 000 — zdarzenia końca odtwarzania, skróty globalne (`HandleGlobalChord`)
- 11 000–13 000 — widoki list, foldery, fokus, filtrowanie
- 13 000–14 000 — TIDAL i playlisty
- 14 000–15 000 — członkostwo elementów w kolekcjach
- 15 000–17 000 — radio: harmonogramy i nagrywanie (najgęstszy fragment)
- 17 000–19 000 — WiiM i presety
- 20 000–21 000 — skróty klawiszowe okna
- 21 000–23 000 — obsługa kliknięć menu (`*_Click`, zwykle jedna linia do `ExecuteCommand`)
- 23 000–23 794 — widoki, rekordy pomocnicze

Praktyczna zasada: szukaj po nazwie polecenia z `CommandIds`, nie po numerze linii.
Metody `*_Click` na końcu pliku prowadzą prosto do właściwej funkcji.

## 14. Aktualizacje programu i składników

- `Windows/Services/ApplicationUpdateManager.cs` — sprawdzanie wydań GitHub,
  pobieranie, SHA-256, osobny pomocnik instalujący po wyjściu AMC i wznawiający
  program po sukcesie. Ręczne odłożenie paczki jest oddzielone od zgody na
  automatyczną instalację przy zamknięciu.
- `Windows/ApplicationUpdateWindow.xaml/.cs` — dostępne okno aktualizacji:
  treść i wersje z kursorem, sprawdzenie, pobranie, anulowanie, jawna zgoda.
- `Windows/MainWindow.ApplicationUpdates.cs` — F11/menu, modalne okno, powrót
  fokusu oraz połączenie aktualizacji z końcowym zapisem i zamykaniem.
- `Core/Updates/ApplicationUpdateInstallFlow.cs` — żądanie zamknięcia, cofnięcie
  zgody po odmowie/wyjątku i instalacja tylko po poprawnym zapisie.
- Testy aktualizacji: Core `ApplicationUpdateShortcutTests` i
  `ApplicationUpdateInstallFlowTests`; Windows `ApplicationUpdateRoutingTests`,
  `ApplicationUpdateWindowTests`, `ApplicationUpdateSaveFailureTests` i
  `ApplicationUpdateManagerTests`.
- `Core/Updates/ApplicationUpdatePolicy.cs` — `ReadChecksumFor` czyta sumę
  z opisu. Format opisu jest dwuwierszowy i pilnuje go test
  `ReleaseNotesChecksumTests`; zmiana formatu w `scripts/wydaj.sh` bez zmiany
  testu psuje weryfikację po cichu.
- `Windows/Services/FfmpegComponentManager.cs`, `YtDlpComponentManager.cs` —
  instalacja i aktualizacja składników zewnętrznych;
  `ExternalToolProcess.cs` uruchamia je w izolacji.
- `Core/Updates/ProblemReportComposer.cs` + `Windows/ProblemReportWindow` —
  zgłoszenie problemu z logiem.

## 15. Budowanie, testy, wydanie

- `build.ps1` — restore, build Release, oba projekty testów dymnych.
  Sprząta zduplikowane pliki `obj` po synchronizacji NuGet (bez tego build padał).
- `tests/AccessibleMediaController.Core.SmokeTests` — 9 plików testów.
- `tests/AccessibleMediaController.Windows.SmokeTests` — 10 plików, w tym mostek
  NVDA i TIDAL w WebView2; `SmokeTestRunner.cs` to własny biegacz.
- `installer/AMC_Setup.iss` — Inno Setup, instalator `.exe`.
- `scripts/wydaj.sh` — publikacja wydania. Odmawia publikacji bez sum SHA-256
  i wymaga w katalogu `.exe`, `.zip` oraz `.nvda-addon`.
- `Directory.Build.props` — jedno miejsce z numerem wersji.

## 16. Zależności zewnętrzne

NuGet: `Microsoft.Data.Sqlite` 8.0.30, `Microsoft.Web.WebView2` 1.0.4191.47,
`NAudio` 2.3.0, `NAudio.Vorbis` 1.5.0, `NLayer` 2.0.1 (+ wsparcie NAudio),
`SoundTouch.Net` 2.3.2 (+ wsparcie NAudio).

Poza NuGet: BASS (`third_party/BASS/win-x64`), FFmpeg i `yt-dlp` pobierane
w czasie działania do osobnych katalogów, SDK TIDAL w `TidalPlayerHost`
(pnpm), Inno Setup do instalatora.

## 17. Gdzie czego NIE ma

- Nie ma warstwy wstrzykiwania zależności — obiekty powstają wprost w `App.xaml.cs`
  i w `MainWindow`.
- Nie ma osobnego modelu widoku; `MainWindow.xaml.cs` łączy widok i sterowanie.
- Nie ma testów jednostkowych w klasycznym sensie, są testy dymne z własnym
  biegaczem kończącym się niezerowym kodem po niepowodzeniu.
