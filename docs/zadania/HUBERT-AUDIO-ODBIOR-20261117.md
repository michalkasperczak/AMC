# Odbiór bezokienny #15 i #16 — 2026-11-17

## Aktualny stan GitHub — 19.11.2026

**#16 zamknięte jako wykonane i wydane.** Pomiar dwóch rzeczywistych procesów
poniżej oraz źródła `9b77369` są objęte opublikowanymi Public
`public-2026.11.18.0329` i Test `test-2026.11.18.0337` ze źródeł
`1723786c4557ef68bb3922189a028099ce96db31`. Komentarz `7007925567` i osobny
odczyt API potwierdziły `closed/completed`. Nie wykonano widocznego restartu
GUI ani odbioru NVDA i nie przypisuje się ich temu wynikowi.

**#15 pozostaje otwarte.** Poniższy pomiar rzeczywistego PCM jest ważny,
ale nie zastępuje odsłuchu wskazanego materiału w instalacji ani odbioru
wyjścia audio. Nie zamknięto słyszalnego przesteru wyłącznie na podstawie
syntetycznego MP3. Pełny rejestr decyzji: `GH-TRIAGE-20261119-zamkniecie-wykonanych-zgloszen.md`.

## Zakres i materiał

Odbiór wykonano na detached worktree:

- commit źródeł: `9b773694dba8e61ae527664ae195d194453ab211`;
- źródła robocze: `/home/michal/projekty/AMC-zadania/hubert-audio-acceptance`;
- materializacja Windows/NTFS: `C:\Users\Michal\AppData\Local\AMC-Hermes-Build\hubert-audio-acceptance`;
- SDK: `C:\Users\Michal\AppData\Local\AMC-BuildTools\dotnet\dotnet.exe` (8.0.423).

Nie uruchamiano GUI AMC, NVDA, urządzenia audio, kont ani Spotify. Dane kolejki należą wyłącznie do katalogu odbiorczego pod `headless-checks`; nie dotknięto profilu użytkownika ani baz produkcyjnych.

## #16 — trwała kolejka: **PASS**

Uruchomiono dwa **osobne**, kolejno kończone procesy rzeczywistego
`amc_lite_host.exe`, z własnym magazynem:

`C:\Users\Michal\AppData\Local\AMC-Hermes-Build\hubert-audio-acceptance\headless-checks\live-queue-20261117T013301\profile`

Pierwszy proces dostał dwa wiersze JSON-Lines:

1. `id="r1-set-α"`, `op="queue.set"`, trzy wpisy o ID `fixture:B`,
   `fixture:A`, `fixture:C` (C ma `isPlayNext=true`);
2. `id="r1-status"`, `op="queue.status"`.

Następnie stdin pierwszego procesu zakończono i host wyszedł z kodem `0`.
Drugi, nowy proces dostał wyłącznie `id="r2-status"`, `op="queue.status"`,
a następnie także wyszedł z kodem `0`.

Pomiar skryptem sprawdzającym odpowiedzi JSON miał wynik:

```text
host_exit_codes=(0,0)
LIVE_QUEUE_TWO_PROCESS_ACCEPTANCE=PASS
first order=fixture:C,fixture:B,fixture:A
second order=fixture:C,fixture:B,fixture:A
second restoredRows=3
```

Ważne wartości z odpowiedzi drugiego procesu:

- `persistent=true`, `persistError=null`, `initialized=true`;
- `count=3`, `restoredRows=3`, `persistedWrites=0`;
- kolejność widoku: `fixture:C, fixture:B, fixture:A`;
- `currentId="fixture:B"`, `playing=false`, `paused=false`.

To sprawdza zapis pierwszego hosta, jego pełne zakończenie, odczyt tego samego
magazynu przez drugi proces oraz zachowanie tożsamości, kolejności i flagi
„odtwórz następne”. Sam `queue.set` ani odczyt nie uruchomiły odtwarzania.

SHA-256 dowodów:

```text
out-1.jsonl                 138834ececf051c865d7b48ffe9f7f51e848bd8428550aa63b75e75ee04d2ad8
out-2.jsonl                 92a22c761b51d7e2921af95a92c89d7d2a87aa0379836330eb9864e4075d7f04
err-1.log                   560404142b1cef4f282343af875b16a88b4b0529b369cd59af01bd85206fa60d
err-2.log                   560404142b1cef4f282343af875b16a88b4b0529b369cd59af01bd85206fa60d
profile/file-queue-v1.json  0416206c81205f1a32c154ce887b3b581bc1a37ce950d068f5b556a098bd56b6
```

## #15 — pierwotny wynik wykonawcy: **historyczna blokada izolacji, wyjaśniona poniżej**

**Nowszy wynik prowadzącego zastępuje tę blokadę:** bezgłośny pomiar aktualnego toru MP3 zaliczony, kod 0. Komponent pod lokalnym `C:\Users\Michal\AppData\Local` był na komputerze **HERMES**, a nie na głównym komputerze Michała. Samo użycie istniejącej, rozpoznanej zależności spoza katalogu stagingu nie oznacza użycia starego kodu AMC. Nie wolno mylić tożsamości komputera ze zbieżną nazwą konta.

Prowadzący potwierdził zgodność sześciu plików źródłowych stagingu z bieżącym repo (cztery klasy toru FFmpeg/WindowsMediaOutput oraz `Mp3PeakPlaybackTests.cs` i `Program.cs`) i uruchomił tylko `--mp3-peaks-only` z już zbudowanego stagingu. Odczytane i zachowane metadane:

- komputer: `HERMES`;
- źródła: `9b773694dba8e61ae527664ae195d194453ab211`; późniejsza poprawka ostrzeżenia playlisty nie zmienia tych plików;
- FFmpeg: `n9.0.2-22-g46d8f462ee-20261003`;
- SHA-256 rzeczywistego `ffmpeg.exe` przed i po próbie: `f2f10171723ce54438dad4a084bde1dfd79f17567a80169ad90ea98524ebc705`;
- kod testu i procesu uruchamiającego: `0`;
- zaliczone asercje na rzeczywistym PCM: surowy szczyt `>1.1`, szczyt po limiterze `>0.75` i `<=0.981`, po przewinięciu do 1 s szczyt `>0.1`; rozróżnienie błędu urządzenia i dekodera;
- wynik: `headless-checks/parent-mp3-acceptance/result.json`, SHA-256 `4c56b4a30b03fa81e55e5dc03fcfb76e66f770e684571d856fd457b4ea7a214e`;
- stdout/stderr tej udanej próby są puste; wynik wynika z faktycznego wykonania testu z asercjami i kodem 0, nie z dopisanej do raportu liczby próbek.

Nie zmieniano ani nie kopiowano komponentu do profilu, nie pobierano go ponownie i nie otwierano urządzenia audio. To pomiar produkcyjnego dekodera/limitera na jawnym syntetycznym MP3, nie odsłuch, próba sterownika dźwięku ani odbiór całego interfejsu. Nie przedstawiać go jako testu z całkowicie odizolowanym runtime. Poniższy zapis dokumentuje wcześniejszą, nadmiernie restrykcyjną interpretację wykonawcy, nie aktualną blokadę.

Test produkcyjnego toru istnieje i uruchamia rzeczywiste:

- wygenerowanie jawnego fixture MP3 przez FFmpeg;
- `FfmpegLocalAudioWaveStream.TryOpen` bez limitera;
- `WindowsMediaOutput.OpenReaderForExport`, czyli aktualny tor odtwarzania z
  `alimiter=limit=0.98:level=false:latency=true`;
- odczyt float PCM, pomiar szczytu i seek do 1 s — bez wyjścia do urządzenia
  audio.

`AccessibleMediaController.Windows.SmokeTests --mp3-peaks-only` zwrócił kod
`0`, ale **nie jest to dowód odbiorczy**. W tym uruchomieniu
`FfmpegComponentManager` znalazł istniejący komponent z głównego komputera:

```text
C:\Users\Michal\AppData\Local\AccessibleMediaController\components\ffmpeg\releases\f566059a0885fff8\ffmpeg-n9.0-latest-win64-lgpl-shared-9.0\bin\ffmpeg.exe
```

Jego stan `current.json` deklaruje SHA-256 pakietu
`f566059a0885fff842091f185e27c6f49a284bcc15d3607bc2bf0a88d58f05c1`
i wersję `n9.0.2-22-g46d8f462ee-20261003`. Ten komponent nie należy do
izolowanego odbioru i zgodnie z zakresem nie został skopiowany, modyfikowany
ani przyjęty jako dowód dla nowych źródeł.

Własnego `ffmpeg.exe` nie znaleziono w stagingu ani w jego katalogu `deps`.
Dwie dozwolone próby pobrania otwartego narzędzia zostały ograniczone przez
łącze i pozostawiły niekompletne archiwa, których nie użyto:

```text
ffmpeg-release-essentials.zip       913408 B
SHA-256 027ebd45f4417805948ff5f6e18ad38482165b2c8f4398c14a128c0f85999ecf
sharex-ffmpeg-8.1.1-win-x64.zip     909312 B
```

Dlatego nie można uczciwie zadeklarować pomiaru surowego szczytu `> 1.1`,
ograniczonego szczytu `<= 0.981` ani seeku aktualnego toru na własnym
runtime. To jest zależność blokująca odbiór #15, nie wykryty błąd produktu.

## Kompilacja kontrolna

Po pomiarze wykonano ponownie oba projekty z NTFS, z `DOTNET_ROOT` wskazującym
na SDK 8.0.423 i prywatnym `TEMP`/`TMP`. Kod procesu kompilacji: `0`.

```text
LiteHost:   Kompilacja powiodła się.
SmokeTests: Kompilacja powiodła się.
```

SHA-256 logów kontrolnych:

```text
litehost.log 81bd1add64e161e914614a376fb6c6b76d8460da95e1607f4412c1b44c50045c
smoke.log    574fc95d12df9a63b78088f9210c2a95367ea3aff2514bc2a5236abceb24ac70
```

## Artefakty zbudowane w stagingu

```text
amc_lite_host.exe
SHA-256 adb3f61fb6569a0b3ac63205c6ca53603383cbfa9eb8105dfb9bb2d5859ba7d1

AccessibleMediaController.Windows.SmokeTests.exe
SHA-256 0cc064adee336345c95a7fe94e29e97272ab1ec3a95e0b3745a102efcf27dc0e

AccessibleMediaController.dll (użyta przez SmokeTests)
SHA-256 70f7709f0affa550fa8e8de3e7219c9fd7ab32adcb06bd5b2589e4a9f9985f0e
```

## Decyzja o zmianie kodu

Nie wykryto wady #16. Dla #15 nie ma ważnego pomiaru własnym dekoderem, więc
nie ma podstaw do pozornej poprawki ani do osłabienia testu. Nie zmieniano
kodu produktu; ten dokument jest jedyną zmianą w worktree.

Do zamknięcia odbioru #15 potrzebny jest zatwierdzony, lokalny `ffmpeg.exe`
w prywatnym katalogu stagingu lub dostarczenie go przez osobę mającą dostęp do
wymaganej zależności. Po jego dostarczeniu należy uruchomić powyższy test
`--mp3-peaks-only` z czystym, izolowanym `LOCALAPPDATA` i zapisać wynik oraz
SHA narzędzia.
