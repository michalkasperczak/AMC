# AUDIO-RECOVERY-20261119 — wynik lokalnego toru Windows/Radio

## Zakres

Włączono, po niezależnym przeglądzie, lokalny zakres PR #31 (`16350cef8cbe9cfd958107bc9fccef1df042105e`) dla WindowsMediaOutput, RadioMediaOutput i nie-Spotify routingu LiteHost. Nie zmieniano `LiteSpotify*`, klienta Soloist, Chrome hosta ani wspólnego GUI.

## Przyczyna zgłoszenia Michała

Radio miało już ochronę po utracie urządzenia i komunikat do hosta. `WindowsMediaOutput` podmieniał wyjście na domyślne po awarii urządzenia, ale nie znał opcji `pauseOnOutputFallback` ani nie zawiadamiał hosta, że transport jest ochronnie wstrzymany. Host mógł więc traktować lokalne Pliki/Podcasty/YouTube/Radio Archiwista/TIDAL jako nadal grające; pierwsza Spacja/Enter wykonywała pauzę zamiast jawnego wznowienia tej samej pozycji.

## Zmiana

- `DefaultAudioOutputWatcher` z PR #31 obserwuje zmianę domyślnego Render/Console. Tylko sesje z wyborem systemowym przechodzą za nowym domyślnym wyjściem; ręcznie wybrane aktywne urządzenie nie podąża za Windows.
- `WindowsMediaOutput` dostał tę samą politykę ochronnej pauzy po utracie co radio. Podmiana tworzy dostępne domyślne wyjście, ale przy opcji ochrony nie wywołuje `Play`; zapamiętany identyfikator urządzenia pozostaje bez zmian.
- `WindowsMediaOutput.OutputDeviceFallback` przekazuje faktyczny fallback do LiteHost. Host oznacza nie-Spotify tor plików jako wstrzymany, więc kolejne produkcyjne `transport.pauseResume` trafia w gałąź wznowienia i wywołuje publiczne `Play` tej samej pozycji.
- Dodano ograniczone złącze katalogu wyjść do headless testu: prawdziwy dekoder, podmiana wyjścia, callback awarii i publiczne `Play` działają z testowym `IWavePlayer`; test nie otwiera fizycznego urządzenia ani nie odtwarza dźwięku.
- Radio zachowuje własny Time Shift/nagrywanie oraz istniejącą ścieżkę wznowienia tej samej stacji. Uogólnienie typu lease na `IWavePlayer` nie zmienia produkcyjnego obiektu `WasapiOut`.

## Test funkcjonalny i GREEN

Nie udało się odtworzyć historycznego RED na niezmienionym PR #31: poprzednia wersja nie miała wstrzykiwalnego wyjścia, a użycie fizycznego urządzenia jest wyłączone zakresem. Nie należy przedstawiać tego jako pełnego RED/GREEN na sprzęcie.

Zbudowano natomiast ograniczony test funkcjonalny produkcyjnego adaptera i osiągnięto GREEN po poprawce. Pierwsza kompilacja tego testu ujawniła dwa niejawne zależności od konkretnego `WasapiOut` w pipeline Windows i Radio; kompilacja zakończyła się `CS0266`. Po ograniczonej zmianie tych właściwości na `IWavePlayer` oraz uzupełnieniu kontrolowanego fake'a:

```text
OK: utrata wyjścia pozostawia ciszę, a Play tej samej pozycji wznawia na domyślnym
```

Test `ProtectedOutputRecoveryTests` wykonuje: publiczne `WindowsMediaOutput.Play` WAV tej samej pozycji -> symulowane `AUDCLNT_E_DEVICE_INVALIDATED` wybranego wyjścia -> zbudowanie domyślnego wyjścia bez `Play` -> kolejne publiczne `Play` tej samej pozycji -> `Play` dokładnie tego fallbacku. Sprawdza zachowanie tożsamości `MediaItem` i brak trzeciego pipeline/wyjścia.

## Weryfikacja

W katalogu NTFS `C:\Users\Michal\AppData\Local\AMC-BuildTools\staging\amc-output-recovery-20261119` użyto prywatnego .NET 8.0.423 i cache `C:\Users\Michal\.nuget\packages`:

```text
dotnet restore tests/AccessibleMediaController.Windows.SmokeTests/AccessibleMediaController.Windows.SmokeTests.csproj --ignore-failed-sources -v:minimal
dotnet build tests/AccessibleMediaController.Windows.SmokeTests/AccessibleMediaController.Windows.SmokeTests.csproj --no-restore -v:minimal
dotnet run --project tests/AccessibleMediaController.Windows.SmokeTests/AccessibleMediaController.Windows.SmokeTests.csproj --no-build -- --protected-output-recovery-only
dotnet restore src/AccessibleMediaController.LiteHost/AccessibleMediaController.LiteHost.csproj --ignore-failed-sources -v:minimal
dotnet build src/AccessibleMediaController.LiteHost/AccessibleMediaController.LiteHost.csproj --no-restore -v:minimal
```

Wynik: oba buildy sukces, 0 ostrzeżeń i 0 błędów; test odzyskiwania sukces. `git diff --cached --check` również sukces.

## PR #29 i granice odbioru

PR #29 (`aedb5f0649226bebef5376677d43bb8731d2ca89`) zmienia także `WindowsMediaOutput`; automatyczne porównanie trzystronne wykazało `changed in both`. Nie został scalony ani kopiowany. Ta zmiana utrzymuje pipeline/MediaItem przy zwykłej podmianie wyjścia i dotyka fallbacku tylko przez wyjście, ale nie zastępuje osobnego odbioru wznowienia z PR #29.

Nie wykonano fizycznego odłączenia Denona, zmiany urządzenia Windows, odsłuchu, GUI, logowania do usług, testu Spotify/Soloist ani NVDA. Headless test nie jest dowodem działania aktywnego Denona ani Spotify. Nie przeprowadzono pełnej macierzy szybkich zmian/braku wszystkich urządzeń/wyciszenia i nagrywania na fizycznym audio; zachowano istniejące ścieżki radia i testowano kontrolowany lokalny tor Windows.