# AUDIO-RECOVERY-20261119 — Spotify Soloist i Chrome: wynik toru

## Zakres i wynik

Zmiana obejmuje wyłącznie `LiteSpotifyPlaybackCoordinator` i jego celowany test adaptera. Naprawiono potwierdzony błąd wspólnego kontraktu: odmowa skonfigurowania ręcznie wybranego wyjścia nie przełącza już po cichu transportu Spotify na domyślne wyjście ani nie kasuje zapamiętanego wyboru. Odpowiedź nie twierdzi też fałszywie, że faktycznie użyto domyślnego wyjścia.

Nie wdrożono pozornej obsługi pełnego odzyskiwania dla Soloist. Aktualny, potwierdzony protokół `soloist ctl` nie udostępnia wyboru, odświeżenia ani sygnału utraty lokalnego wyjścia Windows. Soloist używa wyłącznie domyślnego wyjścia systemowego. Chrome ma własne `setSinkId`, ale coordinator nie otrzymuje ani obserwacji zmiany wyjścia, ani ustawienia „pauza po utracie”; nie może więc bezpiecznie odróżnić automatycznej ochronnej ciszy od późniejszego jawnego Play.

## Przyczyna i minimalna poprawka

Przed poprawką `SelectOutput(... restart=false)` przy `TrySetOutputDeviceAsync("Denon") == false` wykonywał jeszcze `TrySetOutputDeviceAsync(null)` i kasował `selectedDeviceName`. To:

- zmieniało transport bez jawnego polecenia użytkownika;
- niszczyło informację, że preferencja była ręczna;
- przedstawiało odpowiedź jako użycie domyślnego wyjścia, mimo że adapter nie potwierdził takiej podmiany.

Teraz odmowa zwraca `available=false`, pozostawia ręczny wybór i nie wysyła wtórnego żądania `null`. `usingDefault` jest prawdziwe wyłącznie dla jawnego wyboru domyślnego.

## RED/GREEN i build

RED (po dodaniu testu, przed zmianą produkcyjną):

```text
/mnt/c/Users/Michal/AppData/Local/AMC-BuildTests/spotify-output-red
... dotnet.exe run --project tests\AccessibleMediaController.SoloistAdapterTests\AccessibleMediaController.SoloistAdapterTests.csproj -c Release --no-restore
Unhandled exception. System.InvalidOperationException:
niedostępny ręczny wybór przełączył transport po cichu na domyślne wyjście
exit 82
```

GREEN na osobnym stagingu NTFS, prywatnym SDK Windows i względnych ścieżkach Windows:

```text
... dotnet.exe run --project tests\AccessibleMediaController.SoloistAdapterTests\AccessibleMediaController.SoloistAdapterTests.csproj -c Release --no-restore
Soloist adapter: 7 checks passed

... dotnet.exe build src\AccessibleMediaController.LiteHost\AccessibleMediaController.LiteHost.csproj -c Release --no-restore
Kompilacja powiodła się. Ostrzeżenia: 0. Liczba błędów: 0.
```

Nowy test uruchamia rzeczywisty `LiteSpotifyPlaybackCoordinator.SelectOutput` z kontrolowanym transportem implementującym `ILiteSpotifyOutput` i sprawdza pełną sekwencję żądań transportu, nie predykat ani tekst źródła. Ten sam binarny zestaw dalej wykonuje istniejące bezsekretowe fixture oficjalnego `soloist ctl` (`now`, `activate`, `play`, `pause`, `seek`, `volume`) oraz stany playing/buffering/end/failure.

## Konkretna blokada Soloist

Dowód w kodzie i dokumentacji jest zgodny:

- `LiteSpotifySoloistOutput.GetOutputDevicesAsync()` zwraca pustą listę.
- `LiteSpotifySoloistOutput.TrySetOutputDeviceAsync(wanted)` zwraca sukces tylko dla `null`; nie emituje żadnej wymyślonej komendy upstream.
- Oficjalny zestaw poleceń używany przez adapter to wyłącznie `activate`, `play`, `now --json`, `pause`, `seek`, `volume` (`SPOTIFY-SOLOIST-20261108-proste-logowanie.md`, wiersze 23–26).
- Ten sam rekord explicite ogranicza Soloist na Windows do domyślnego wyjścia WASAPI i nie obiecuje `Shift+A` (wiersze 26 i 44); rekord wydania z 9.11 potwierdza brak sterowania wyborem wyjścia.

W konsekwencji AMC nie może przez obecny protokół Soloist sprawdzić, czy Denon zniknął, wymusić odświeżenie sinka ani przywrócić konkretnego endpointu. Bez potwierdzonego rozszerzenia Soloist nie wolno dodać komendy `output`, `device`, `sink` ani przełączać Spotify Connect.

## Chrome i wymagana integracja wspólnej polityki

`LiteSpotifyChromeOutput` ma lokalne `navigator.mediaDevices.enumerateDevices()` i `HTMLMediaElement.setSinkId`, więc pozostaje jedynym adapterem Spotify z potwierdzonym wyborem nazwanych wyjść. Obecnie stosuje go przed startem i po `PlaybackStarted`, ale nie rejestruje `devicechange` ani nie dostaje stanu watchera Windows.

W `LiteSpotifyPlaybackCoordinator` nie ma też wejścia dla opcji ochronnej pauzy ani zdarzenia „wybrane wyjście utracone”. `PauseResume` bezwarunkowo przekazuje `Resume()`, a `ToggleMute` wywołuje wyłącznie `SetVolume(0 / volume)` — odciszenie nie wywołuje Resume i nie jest dowodem zgody na dźwięk. Bez wspólnego zdarzenia/polityki nie da się poprawnie spełnić scenariusza „utrata → cicha pauza → później Space/Enter na tym samym materiale sprawdza dostępne wyjście → jawne wznowienie” bez ryzyka automatycznego dźwięku lub fałszywego powodzenia.

Punkt integracyjny dla workera wspólnego toru: po utworzeniu rzeczywistej polityki odzyskiwania i obserwacji wyjść musi on przekazać do Spotify wynik utraty wraz z aktywną opcją ochronnej pauzy. Dopiero wtedy można dodać do coordinatatora mały, jawny kontrakt obsługi tego wyniku oraz testy Play/Space/Enter/mute/Stop/Dispose. Nie należy używać `ToggleMute` jako takiego sygnału ani zmieniać Soloist/Chrome bez potwierdzonego transportu.

## Pliki

- `src/AccessibleMediaController.LiteHost/LiteSpotifyPlaybackCoordinator.cs`
- `tests/AccessibleMediaController.SoloistAdapterTests/Program.cs`
- `docs/zadania/AUDIO-RECOVERY-20261119-spotify-wynik.md`

## Granice odbioru

Nie otwierano Chrome, GUI ani audio; nie logowano do Spotify, nie odczytywano tokenów/cookies ani nie zmieniano urządzenia domyślnego Windows. Nie ma dowodu odsłuchu Denona, aktywnego konta Spotify ani NVDA. Baza pracy: `8bd38117de2640b582b7916283dcdee328c1eada`; finalny SHA jest przekazany wraz z wynikiem do integracji.