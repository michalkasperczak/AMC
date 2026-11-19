# AUDIO-RECOVERY-20261119 — niezależny odbiór lokalnego wyjścia i Spotify

## Werdykt

**TAK do integracji kodu po tym commicie odbioru; NIE jako dowód gotowości wydania ani odbioru sprzętowego.**

Przyrost od `a245d2249c850d038dea36ce9eb2068146b34573` do zamrożonego
`e758ae409689bb81f7aed94379bd1f03557ddcff` zawierał potwierdzony defekt:
po utracie urządzenia z wyłączoną ochronną pauzą poprzedni `IWavePlayer` był
już `Stopped`, więc podmiana nie wywoływała `Play` na fallbacku. Mała poprawka
rozróżnia teraz utratę urządzenia od świadomej pauzy (`_pauseRequested`).
Nie integruj w tym kroku PR #29 (`aedb5f0649226bebef5376677d43bb8731d2ca89`).

## Zakres i tożsamość

- Worktree: `/home/michal/.hermes/profiles/amc/cache/scratch/amc-output-acceptance-20261119`
- Branch: `codex/amc-output-acceptance-20261119`
- Baza odbioru: `a245d2249c850d038dea36ce9eb2068146b34573`
- Zamrożony kandydat: `e758ae409689bb81f7aed94379bd1f03557ddcff`
- Kod lokalny w kandydacie odpowiada wcześniejszemu przyrostowi `0901f8d`;
  Spotify — `17dcde5`.
- Własny NTFS staging: `C:\Users\Michal\AppData\Local\AMC-BuildTools\staging\amc-output-acceptance-review-20261119`.
  Staging ma `HEAD e758ae4` i wyłącznie opisane niżej, niezatwierdzone pliki
  odbioru. `git.exe` nie jest dostępny w WSL, dlatego tożsamość stagingu
  potwierdzono przez WSL Git oraz SHA-256 par źródło–staging; natywny build
  uruchomiono prywatnym `dotnet.exe 8.0.423` z katalogu NTFS.

## Potwierdzony defekt i jedna poprawka RED/GREEN

`WindowsMediaOutput.ReplacePlaybackOutput` decydował o automatycznym `Play`
z warunku `previous.Output.PlaybackState == Playing`. Prawdziwa ścieżka
awarii najpierw ustawia stan starego wyjścia na `Stopped`, zatem przy
`pauseOnOutputFallback=false` nowy fallback pozostawał cichy mimo że polityka
nie żądała ochronnej pauzy.

Dodano warunek: dla `failedDevice` uruchom fallback, chyba że aktywna jest
ochrona albo `_pauseRequested`. Dzięki temu użytkowa pauza nadal blokuje
automatyczny dźwięk, a wyłączona ochrona wznawia odtwarzanie.

RED został wykonany przed zmianą produkcyjną na faktycznym publicznym
`WindowsMediaOutput.Play` z prawdziwym dekoderem WAV i kontrolowanym
`IWavePlayer`:

```text
Fallback bez ochronnej pauzy nie wznowił odtwarzania po utracie urządzenia.
exit 1
```

Po zmianie GREEN obejmuje trzy scenariusze w
`ProtectedOutputRecoveryTests`:

1. ochrona włączona: fallback jest cichy, a publiczne `Play` tej samej pozycji
   uruchamia drugi, już istniejący output bez trzeciego pipeline;
2. ochrona wyłączona: callback `AUDCLNT_E_DEVICE_INVALIDATED` automatycznie
   uruchamia fallback;
3. pauza użytkownika przed callbackiem pozostawia fallback cichy, a spóźniony
   callback po `Stop` nie buduje nowego outputu.

## Granica LiteHost

Dodano kontrolowany test `LiteEngineHandlers.Build()` bez GUI i fizycznego
audio. Przez produkcyjny dispatcher uruchamia `audio.configure`, `files.play`,
awarię outputu i `transport.pauseResume`.

Potwierdza on, że przy ochronie `_filesPaused` jest ustawiane przez
`OutputDeviceFallback`, odpowiedź `transport.pauseResume` ma `paused=false`,
a to samo wyjście fallback otrzymuje jedno `Play`; liczba outputów pozostaje
równa 2. To jest dowód rzeczywistej granicy hosta, nie tylko test predykatu
lub tekstu źródła.

Przegląd wąskich wyścigów:

- `Pause()` ustawia `_pauseRequested` przed wywołaniem NAudio; warunek
  fallbacku respektuje ten stan.
- `Stop()` odczepia handler i usuwa pipeline pod `_gate`; callback dodatkowo
  weryfikuje identyczność pipeline i nadawcy. Zielony test obejmuje spóźniony
  callback po `Stop`.
- Zmiana pozycji zachowuje ten sam pipeline; worker seek sprawdza aktualność
  pipeline/worker. Callback starego outputu po podmianie jest odrzucany przez
  porównanie `sender` z aktualnym outputem.
- Sama podmiana wyjścia zachowuje dekoder, `TempoStream` i `MediaItem`.
  Testy sprawdzają brak trzeciego outputu przy jawnym wznowieniu.

`DefaultOutputFollowTests` pozostają głównie kontrolą fragmentów źródeł;
nie zostały uznane za funkcjonalny dowód odbioru.

## Spotify

Przyrost Spotify jest poprawny w swoim deklarowanym zakresie. Test adaptera
teraz potwierdza trzy rzeczy po `SelectOutput("Denon", restart=false)`, gdy
transport odmawia: dokładnie jedno żądanie `"Denon"`, odpowiedź
`available=false` i `usingDefault=false`, a prywatna pamięć
`selectedDeviceName` nadal wynosi `"Denon"`.

Brak jest dowodu pełnego odzyskiwania Soloist lub Chrome po utracie urządzenia.
Aktualny Soloist nie udostępnia takiego protokołu; nie dodawano pozornej
komendy ani nie zmieniano Chrome, kont, tokenów czy urządzeń Windows.

## Rzeczywiste testy i build

W prywatnym SDK .NET 8.0.423, ze źródeł NTFS i względnymi ścieżkami Windows:

```text
dotnet restore/build tests\AccessibleMediaController.Windows.SmokeTests\... --ignore-failed-sources
dotnet run --project tests\AccessibleMediaController.Windows.SmokeTests\... --no-build -- --protected-output-recovery-only
=> sukces, 0 ostrzeżeń, 0 błędów

dotnet restore/build src\AccessibleMediaController.LiteHost\AccessibleMediaController.LiteHost.csproj --ignore-failed-sources
=> sukces, 0 ostrzeżeń, 0 błędów

dotnet restore/build/run tests\AccessibleMediaController.LiteHost.HandlerTests\...
=> LiteHost automatic refresh dispatcher and protected output transport: OK

dotnet restore/build/run tests\AccessibleMediaController.SoloistAdapterTests\...
=> Soloist adapter: 7 checks passed
```

Logi poleceń i wyników: `/home/michal/.hermes/profiles/amc/cache/scratch/amc-output-acceptance-evidence-20261119/`.
Kontrola `git diff --check` oraz skan dodanych linii pod kątem sekretów,
`eval`/`exec`, shell injection i SQL injection nie zwróciły wyniku.

## PR #29

PR #29 modyfikuje ten sam `WindowsMediaOutput`, lecz w innym celu: zapisuje
czas pauzy i omija seek przy wznowieniu w miejscu. Kandydat ustawia
`_pauseRequested` także dla ochronnej pauzy w `ReplacePlaybackOutput`, a PR #29
inicjuje swój przyszły znacznik czasu tylko w `Pause()`. Proste połączenie może
więc potraktować ochronną pauzę jako pauzę o nieustalonym/nieprawidłowym czasie
i zmienić decyzję `ResumesInPlace`. Wymaga to osobnego odbioru i jawnego
zdefiniowania czasu ochronnej pauzy; PR #29 nie został scalony ani kopiowany.

## Ograniczenia i jeden następny krok

Nie wykonano GUI, NVDA, Chrome, logowania Spotify, zmiany domyślnego urządzenia
Windows ani fizycznego odsłuchu/odłączenia Denona. Testy headless nie są
potwierdzeniem aktywnego sprzętu ani Spotify.

**Następny krok:** po integracji tego czystego commitu wykonać osobno
uzgodnioną próbę fizycznego odłączenia i zmiany wyjścia systemowego z NVDA,
zanim kandydat zostanie uznany za gotowy do wydania.
