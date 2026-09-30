# Sesja Sonos: obsługa i granice (stan tego przyrostu)

Sonos jest w AMC **urządzeniem autonomicznym**, dokładnie jak WiiM. AMC nie
przesyła do niego dźwięku i nie ma własnego silnika odtwarzania Sonosa —
steruje tylko **grupą** przez Sonos Control API i **odczytuje** jej stan.

## Jak się tym obsługuje

### Wejście do sesji

- Sonos jest sesją na liście sesji, **dopisaną na końcu** (domyślnie numer 8).
  Numery i kolejność dotychczasowych sesji są nietknięte — żaden wyuczony
  Alt+cyfra nie zmienił znaczenia.
- Jeżeli w zapisanych ustawieniach numery 1–9 są już zajęte, Sonos zostaje
  **bez numeru**: nadal jest na liście i dojdzie się do niego przechodzeniem
  między sesjami, ale nie zabiera skrótu innej sesji.
- Wejście do sesji Sonos (jawne — wybór sesji) dopiero wtedy inicjuje
  właściciela konta. **Start programu ani odbudowa menu nie czytają konta
  Sonos i nie ruszają sieci.**

### Lista

- Lista sesji Sonos pokazuje **grupy** z aktywnego domu, nie utwory
  demonstracyjne. Każda pozycja to grupa (nazwa + liczba głośników + stan).
- Brak konta albo brak grup daje **czytelny pusty stan z drogą do konta**
  („Nie ma konta Sonos…” / „Konto Sonos nie ma grup…”), nie pustą listę bez
  wyjaśnienia.
- Konto i lista grup są dostępne **z samej sesji**, nie tylko z historycznego
  Plik → Konto Sonos.

### Aktywna grupa

- Enter (wybór pozycji) czyni grupę **aktywną** i otwiera istniejący widok
  odtwarzacza.
- **Sam wybór grupy nie zmienia muzyki**: żadnego POST przy wyborze. Zmienia
  się tylko to, czym steruje AMC.
- Dom i grupa są pamiętane **po identyfikatorze** (nie po indeksie ani
  nazwie), w ustawieniach, **bez żadnych tokenów** (`PersistedState.Sonos`).
- Gdy aktywna grupa **zniknie** z topologii, AMC **nie wybiera po cichu
  innej**: mówi, że grupa zniknęła, i czeka na wybór.

### Wyjście z odtwarzacza

- Wyjście z odtwarzacza, zmiana sesji i zamknięcie AMC **nie zatrzymują i nie
  pauzują** Sonosa — zero POST. Muzyka gra dalej, jak przy WiiM.
- AMC kończy tylko **swoje** liczniki i oczekujące odczyty.

### Polecenia (istniejąca droga `ExecuteCommand`, bez nowych skrótów)

| Polecenie | Zachowanie |
|---|---|
| Odtwórz / Pauza / Przełącz | POST, potem **jawny odczyt stanu** |
| Następny / Poprzedni | POST, potem odczyt **stanu i metadanych** |
| Przewijanie (bezwzględne i względne) | POST, potem odczyt stanu |
| Głośność, wyciszenie | POST, potem odczyt **głośności** |

Zasady, których te polecenia trzymają się twardo:

- **Accepted 200 ≠ wykonane.** Po każdym poleceniu idzie jawny GET w rodzaju
  polecenia i dopiero on rozstrzyga, co powiedzieć.
- **Żadnego ponawiania POST**, nawet po 401. Wynik nieznany = „nie
  potwierdzono”, nigdy „zrobione”.
- Jeśli stan **już był docelowy**, komunikat mówi o **odczytanym stanie**, a
  nie o skutku naszego polecenia.
- Przy Następny/Poprzedni **udany GET sam nie jest potwierdzeniem**: bez
  porównywalnego materiału (poprzedni i nowy tytuł) wynik jest
  „niepotwierdzony”.
- **Jeden przelot poleceniowy naraz** dla aktywnej grupy. Drugie polecenie
  dostaje jawną, bezpieczną odmowę zajętości — nie ma niejawnej kolejki
  przełączeń.
- Dostępność bierze się z odczytanych `availablePlaybackActions`
  (`CanPlay` / `CanPause` / `CanSkip` / `SkipToPreviousAllowed` / `CanSeek`).
  Niedostępne polecenie daje **czytelną odmowę bez POST**.
- `volume.Fixed` **blokuje poziom** (odmowa zamiast POST).
- **Nieznane wyciszenie** prowadzi do odczytu albo odmowy — nigdy do
  zgadywania wartości logicznej.

### Odczyt stanu

- Tytuł, wykonawca, źródło, stan, głośność i wyciszenie pochodzą z
  rzeczywistych odczytów (`ReadGroupPlayback` / `ReadGroupMetadata` /
  `ReadGroupVolume`) przez tego samego właściciela konta.
- **Brak pozycji lub długości nie jest zerem ani fałszem** — to „brak
  informacji”.
- **Nie ma zegara demonstracyjnego.** Pokazywana jest ostatnia odczytana
  pozycja. Czas jest ekstrapolowany **tylko** gdy stan jest znany i to
  `Playing`, z jawną świeżością, i nigdy poza wygasły odczyt.
- **Radio bez `currentItem`** pozostaje poprawnym stanem (źródło bez utworu).
- **Nieudany odczyt niesie jasny stan** — nie udaje sukcesu na danych z
  poprzedniego odczytu.

### Odczyt w tle

- Jeden **oszczędny** odczyt w tle dla używanej grupy; interwał to **polityka
  AMC**, nie rzekomy limit Sonosa. Błąd i 429 dają backoff, pollowanie się nie
  nakłada.
- Odczyt w tle **nie mówi przy każdym cyklu** i **nie zabiera fokusu**.
- Zmiana grupy, zmiana domu i wyjście **unieważniają własne oczekujące
  wyniki** (osobny bilet celu ponad generacją konta): spóźniona odpowiedź dla
  grupy A nie nadpisze grupy B i nie wyczyści nowszego oczekiwania.

## Granice tego przyrostu (świadomie poza zakresem)

- Presety, ulubione, EQ, wejścia, kolejka i webhooki — **poza etapem**.
  Martwych przycisków tych funkcji **nie ma**.
- Brak `stop` / `repeat` / ładowania URI po HTTP: te polecenia nie są tu
  wymyślane.
- Sonos nie dostaje żadnego nowego globalnego skrótu klawiszowego.

## Zakres odbioru części B3a

Zestaw `--sonos-player-ui` ma teraz 30 sprawdzeń. D6 używa odczytanej pozycji
24:30:00 i długości 25:00:00 w pauzie: kontrolka oraz polecenia czasu mają
podawać całkowite godziny, a czas pozostały wynosi 30:00. Ten sam test na kodzie
sprzed poprawki kompiluje się, ale wykrywa błędny tekst „0:30:00 z 1:00:00”;
po zmianie dwóch helperów przechodzi. Nie zmieniono wspólnego formatera.

Niezależny odbiór tej poprawki objął również rzeczywiste Ctrl+8/Enter,
Ctrl+Shift+E/T/R i zapis wypowiedzi w Podglądzie mowy żywego NVDA na Hermesie.
Odczytano 24:30:00, 25:00:00 i 30:00 z dokładnych binariów zaliczonego przebiegu.
Wszystkie dane grupy były syntetyczne; nie odtwarzano rzeczywistego nagrania.

To odbiór wyświetlania, Play/Pause, czasu i **skoku do pozycji**, nie całego
odtwarzacza. Przyciski „Skocz do czasu…” i „Skocz do procentu…” wcześniej
odmawiały mimo znanej długości; tę usterkę potwierdzono osobno przez rzeczywiste
UIA Invoke i mowę NVDA, a **teraz jest naprawiona** i zmierzona syntetycznie
zestawem `--sonos-player-ui` (odsłuch NVDA tej poprawki robi osobny odbiór).
Next/Previous,
pozostałe przewijanie (`digit percent`, `seekCustom`), głośność/wyciszenie
i ich bramki wymagają osobnego odbioru — to jawna reszta B4.

## Czym to zmierzone

| Pomiar | Co sprawdza |
|---|---|
| `Core.SmokeTests -- --sonos-session-presentation` | formatery, pusty stan, pamięć wyboru po ID, pełny roundtrip starych ustawień, bramka dostępności, werdykty po odczycie, brak zegara demo |
| `Windows.SmokeTests -- --sonos-session-ui` | **prawdziwy `MainWindow`** bez pokazywania okna: lista grup, aktywna grupa bez POST, wyjście bez stop/pause, polecenia przez `ExecuteCommand`, odmowa zajętości, unieważnianie spóźnionych odczytów. **Mierzy pomocnicze metody sesji, NIE drogę klawiatury** — Enter i wypełnienie kontrolki listy sprawdza dopiero pomiar poniżej |
| `Windows.SmokeTests -- --sonos-session-entry-ui` | **WEJŚCIE rzeczywistą drogą użytkownika, na POKAZANYM własnym oknie**: prawdziwe `ExecuteCommand("session.slot.8")` (to, co robi Ctrl+8) i pompa dispatchera → **kontrolka `MediaList` ma 2 wiersze** (nie sama `session.Items`); prawdziwy routed `PreviewKeyDown`/Enter na **zaznaczonym wierszu** → `_playerViewActive`, `SelectedGroupId`, aktualny element sesji, jawne odczyty stanu i głośności, **zero POST z samego wyboru**; Escape wraca na listę bez transportu; druga grupa tą samą drogą; spóźniona aktywacja po świadomym wyjściu z sesji nie kradnie fokusu. Bez NVDA i bez klawiszy systemowych |
| `Windows.SmokeTests -- --sonos-player-ui` | **UŻYTKOWY odtwarzacz na POKAZANYM własnym oknie**: po wejściu drogą Ctrl+8 + Enter wszystkie kontrolki odtwarzacza (`PlayerTitleText` / `PlayerArtistText` / `PlayerSessionText` / `PlayerStateText` / `PlayerTimeText` + przycisk) pochodzą z **odczytu grupy** i nie dziedziczą tekstu po odtwarzaczu innej sesji; **rzeczywisty przycisk** ma treść i **dostępną nazwę** zgodną z odczytanym stanem, a zmiana Playing → Paused **odczytem** je przestawia; kliknięcie prawdziwego `PlayerPlayPause_Click` daje **dokładnie jeden POST** do aktywnej grupy + potwierdzający GET i **nie rusza `DemoMediaSession`**; Ctrl+Shift+E/R/T podają **odczytany czas Sonosa** (0:12 / 2:48 / 3:00), a radio bez `currentItem` daje „nie jest znany”, **nie 0:00**; **SKOK DO POZYCJI** — chroniony `Button.OnClick` na obu rzeczywistych `PlayerSeekTimeButton` / `PlayerSeekPercentButton` otwiera **istniejące** `SeekPositionWindow` z **odczytaną** długością 3:00 (Spacja NIE jest tu kliknięciem: przejmuje ją globalny PlayPause), zatwierdzenie prawdziwym przyciskiem „Skocz” daje **dokładnie jedno** `SeekRelativeAsync` z `GRUPA-SALON` / `UTWOR-1` i **deltą policzoną od ponownego odczytu** (2:30 → +138 000 ms, 50% → +78 000 ms) + jawny GET, `PlayerTimeText` z tego odczytu, `DemoMediaSession` nietknięty; **ZERO żądań** przy anulowaniu, przy braku odczytanej długości (radio, z nazwaniem czego brakuje) i przy **zmianie materiału w trakcie modalu** |

Co ten pomiar **wykazał i naprawił** (RED przed zmianą produkcji, potem GREEN):

- `UpdateSonosPlayerView` ustawiał tylko 5 tekstów i nazwę `PlayerPanel`. `PlayerTimeText`,
  `PlayerSpeedText`, etykieta i **dostępna nazwa przycisku** oraz przyciski prędkości,
  zakładek i przewijania do miejsca zostawały **z odtwarzacza poprzedniej sesji** — czytnik
  podawał czas i stan **cudzej sesji** jako stan Sonosa. Teraz każda z tych kontrolek
  pochodzi z odczytu grupy albo jest jawnie ukryta (Sonos nie ma prędkości ani zakładek AMC).
- Dostępna nazwa przycisku była ustawiana **tylko przy fokusie**, więc po zmianie stanu
  poznanej odczytem treść przycisku się zmieniała, a **nazwa czytana przez czytnik nie** —
  i to ona wygrywa. Teraz idzie za odczytem, ale przestawia się **wyłącznie przy rzeczywistej
  zmianie**, żeby odczyt w tle nie wywoływał zdarzeń UIA co cykl.
- Polecenia `TimeElapsed` / `TimeRemaining` / `TimeTotal` **spadały do ogólnego routera**,
  który czyta `DemoMediaSession` — w sesji Sonos oznaczało to **pozycję 0 z długości 0**,
  czyli dane, których Sonos nigdy nie zgłosił. Teraz odpowiada `AnnounceSonosTime` z
  odczytu grupy, a brak pozycji lub długości to **„nie jest znany”**, nie zero.
- **Oba dialogi skoku były martwe w sesji Sonos.** `PlayerSeekTimeButton` /
  `PlayerSeekPercentButton`, oba wpisy menu i Ctrl+J / Ctrl+Shift+J szły przez
  `SeekToTime_Click` → `ExecuteCommand(transport.seekToTime)` do **ogólnego**
  `ShowSeekPositionDialog`, a ten czyta `_sessions.Current.CurrentItem.Duration`.
  Wiersz grupy tworzy `ApplySonosGroupRows` jako `MediaItemKind.Device` **bez
  długości**, więc odpowiedź była **zawsze** „czas trwania jest nieznany” — mimo
  że odczyt grupy znał i pozycję, i długość. Usterkę potwierdzono osobno
  rzeczywistym UIA Invoke obu widocznych i włączonych kontrolek oraz mową NVDA
  („nieznany czas”, brak okna), a dopiero potem naprawiono. Teraz gałąź Sonos w
  `ExecuteCommand` kieruje oba polecenia do `SeekSonosToPositionAsync`, które
  otwiera **istniejące** `SeekPositionWindow` z długością **z odczytu Sonosa** i
  przelicza pozycję docelową na **deltę** dla istniejącego `SeekRelativeAsync`
  (backend **nie ma** skoku absolutnego i nie dodano mu endpointu). Zwykłe sesje
  zachowują dotychczasową drogę.
- Delta jest liczona od **ponownego** odczytu po zamknięciu modalu, nie od
  pozycji z chwili jego otwarcia — modal trwa dowolnie długo, więc stara pozycja
  trafiałaby gdzie indziej. Tożsamość grupy i `itemId` jest sprawdzana **przed i
  po** modalu: zmiana celu albo materiału kończy się **zerem żądań** i jawnym
  komunikatem, nie skokiem w nowy cel.

Pomiar `--sonos-session-ui` buduje się i uruchamia **bez GUI** (żadnego `Show`,
`ShowDialog`, `Activate`) — to się NIE zmieniło. Osobny `--sonos-session-entry-ui`
**świadomie POKAZUJE własne okno**, bo drogi klawiatury (routed `PreviewKeyDown`)
i zawartości kontrolki listy nie da się zmierzyć bez powierzchni prezentacji;
startowe źródła z `ContentRendered` są przy tym odłączone, a okno zamykane w
`Dispose`. Oba używają `SuppressDesktopIntegrationForTests`,
własnego magazynu ustawień z pustymi kontami/podcastami/urządzeniami
WiiM/harmonogramami, wyłączonych aktualizacji i odmowy uruchomienia
instalatora.

### Granice samych zestawów automatycznych

- **Odsłuch NVDA** i **fizyczna klawiatura** — te zestawy ich nie uruchamiają.
  Osobny wykonany odbiór rodzica opisano wyżej.
  `--sonos-session-entry-ui` i `--sonos-player-ui` wysyłają routed `KeyEventArgs`
  i routed `Click` na elemencie z fokusem we
  **własnym** oknie, nie klawisze systemowe. Komunikaty są sprawdzone jako tekst,
  który trafia do `Announce`, nie jako mowa.
- **Żadnego prawdziwego konta ani sieci Sonos.** Zaplecze grupy jest
  podstawione na faktycznej granicy API (domyślnie `null`), poświadczenia
  zostają wyłącznie u właściciela konta.
