# AMC — mapa kodu

Ten plik odpowiada na jedno pytanie: **gdzie w kodzie leży dana funkcja programu**.
Nie opisuje planów ani decyzji projektowych — te są w `MEDIA_CONTROLLER_PL.md`,
`PROJEKT_TIDAL_PL.md`, `PROJEKT_NVDA_PL.md` i pozostałych `PROJEKT_*`.

Stan na wersję `0.1.0-alpha.387` (commit d7ee353).
Zmierzone na drzewie źródeł, nie przepisane z dokumentacji.

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
- `InformationWindow`: właściwości domyślnie z natywnym kursorem, przełącznik tekst/dokument; `nvda-addon/addon/appModules/accessiblemediacontroller.py`: ograniczenie NVDA+góra do okna głównego.
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
- `appModules/accessiblemediacontroller.py` — moduł aplikacji.
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
