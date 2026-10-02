# AMC-wx-Lite — jak to uruchomic i sprawdzic

Lekki wariant AMC: **pliki lokalne** i **radio internetowe**, natywne kontrolki
wxPython, a dzwiek robi **istniejacy silnik AMC** w malym bezokiennym hoscie .NET.
Pelny AMC zostaje nietkniety — ten wariant ma wlasny folder stanu i wlasny proces.

---

## 1. Co jest juz zmierzone, a co czeka na Windows

### Zmierzone w WSL (bez pulpitu, bez NVDA)

| Co | Jak sprawdzone | Wynik |
|---|---|---|
| Logika listy, nawigacji, stanu, skrotow, protokolu | `python3 run_tests.py` | **107 testow, 0 bledow** |
| Protokol po stronie hosta (C#) | `dotnet run --project tests/...ProtocolTests` | **3 zestawy OK** |
| Zgodnosc Python ↔ **prawdziwa petla C#** | `run_tests.py test_wire` | **14 testow OK** |
| Crossbuild hosta na Windows | `dotnet publish -r win-x64` | **`amc_lite_host.exe` 151 552 B** |
| Zlozenie pakietu | `tools/build_bundle.py` | **38 plikow, hasz policzony** |

### NIE zmierzone — wymaga pulpitu Windows

- Samo **okno** (wxPython potrzebuje pulpitu).
- **Realne odtwarzanie** pliku i stacji (NAudio/WASAPI dzialaja tylko na Windows).
- Zachowanie z **NVDA i Narratorem**.

> Uczciwie: w WSL sprawdzilem wszystko **poza** dzwiekiem i oknem. Tych dwoch
> rzeczy **nie udawalem atrapa** — pierwsze realne odtwarzanie bedzie na Windows.

---

## 2. Uruchomienie na Windows

```bat
rem 1. Zloz pakiet (w WSL albo na Windows)
python3 wxlite/tools/build_bundle.py --out D:\AMC-wx-Lite --clean

rem 2. Srodowisko Pythona — PRYWATNE, nie globalne
cd /d D:\AMC-wx-Lite
py -3.14 -m venv .venv
.venv\Scripts\python -m pip install -r requirements-win64.txt

rem 3. Diagnostyka BEZ okna
AMC-wx-Lite.cmd --sprawdz

rem 4. Okno
AMC-wx-Lite.cmd
```

Masz juz prywatny runtime 3.14.7 z wxPython 4.3.1? Wskaz go i pomin krok 2:

```bat
set AMC_WX_LITE_PYTHON=D:\runtime\python.exe
AMC-wx-Lite.cmd --sprawdz
```

**Wazne:** uruchamiaj z **NTFS** (`D:\...`), nie z `\\wsl$\...`.

### MSVCP140.dll

Runtime wymaga `MSVCP140.dll` wydobytej z oficjalnego `vc_redist`. Binarka
**nie jest** w Git — prawa redystrybucji VC++ pozostaja bramka publikacji.
`build_bundle.py` **nie kopiuje** runtime, chyba ze podasz `--runtime`.

---

## 3. Co sprawdzic recznie (pierwsze uruchomienie)

### Pliki lokalne
1. `Ctrl+O` → wybierz folder z muzyka. Czytnik ma powiedziec nazwe folderu i liczbe elementow.
2. **Strzalki** chodza po liscie, pisanie litery skacze do pozycji — to natywny `ListCtrl`.
3. `Enter` na folderze → wchodzi. `Backspace` → wraca **i staje na opuszczonym folderze**.
4. `Enter` na utworze → **gra** i przechodzi do odtwarzacza.
5. `Escape` → ta sama lista, **to samo zaznaczenie**.
6. `F6` → odtwarzacz, `Shift+F6` → lista.
7. `Spacja` pauza/wznowienie. `Lewo/Prawo` ±10 s, `Shift` ±60 s. `Gora/Dol` glosnosc.
8. `Ctrl+E` / `Ctrl+R` / `Ctrl+T` — czas miniony / pozostaly / calkowity.

### Radio
1. `Ctrl+2` → sesja radiowa. `Ctrl+N` dodaj stacje (adres musi byc http/https).
2. `Enter` → **laczy i gra**. `F2` zmiana, `Delete` usuniecie (z potwierdzeniem).
3. `Ctrl+I` → import M3U/PLS **istniejacym importerem AMC**. Duplikaty pominiete,
   Twoje wpisy maja pierwszenstwo.
4. `Ctrl+1` / `Ctrl+2` — przelaczanie sesji **nie gubi** listy ani zaznaczenia.

### Czego sprawdzic, ze NIE ma
- Program **nie dotyka** `state.json` pelnego AMC (stan: `%APPDATA%\AMC-wx-Lite`).
- Nie ma portu sieciowego — host to proces potomny na stdin/stdout.
- Zamkniecie okna konczy hosta (EOF), nie zostaje sierocy proces.

---

## 4. Testy w WSL

```bash
cd /home/michal/projekty/amc-wx-lite-after416/wxlite
python3 run_tests.py                 # wszystko
python3 run_tests.py test_wire       # zgodnosc z prawdziwa petla C#
python3 run_tests.py test_state      # prywatny stan
```

Testy zgodnosci potrzebuja zbudowanego serwera protokolu:

```bash
cd /home/michal/projekty/amc-wx-lite-after416
/home/michal/dotnet/dotnet build tests/AccessibleMediaController.LiteHost.ProtocolTests -c Release
```

Brak buildu = testy **POMINIETE** (nigdy nie udaja zdanych).

Testy hosta w C#:

```bash
/home/michal/dotnet/dotnet run --project tests/AccessibleMediaController.LiteHost.ProtocolTests -c Release
```

---

## 5. Build silnika

```bash
cd /home/michal/projekty/amc-wx-lite-after416
/home/michal/dotnet/dotnet publish src/AccessibleMediaController.LiteHost \
  -c Release -r win-x64 --self-contained false
```

Host linkuje **faktyczne** pliki silnika (`WindowsMediaOutput`,
`TimeshiftMediaOutput`, `RadioMediaOutput`, `RadioPlaylistImporter`,
`LocalAudioFileDiscovery`) + `Core`. Zadnego `MainWindow`, zadnego WPF,
zadnych wlasnych dekoderow w Pythonie.

---

## 6. Granice tej wersji (swiadome)

- **Dodatek NVDA**: zwykla obsluga + krotkie standardowe komunikaty. Zgodnosc
  protokolu dodatku bez kolizji z pelnym AMC sprawdzi rodzic osobno.
- **Tempo**: dziala na obecnym silniku. `PlaybackTempoAlgorithm` (SoundTouch /
  Speech / Music) dodaje agent DSP — w tej bazie tego typu **jeszcze nie ma**,
  wiec opcja **nie jest reklamowana**. Host ma wydzielony handler
  `audio.configure`, ktory rodzic podlaczy po scaleniu. Zadnego reflection,
  zadnego podrabiania typow.
- Brak: WebView2, Sonos, harmonogramow, uslug startowych. Niczego takiego
  **nie wycinalem** z pelnego AMC — po prostu tego tu nie ma.

---

## 7. Protokol (dla ciekawych)

JSON-lines po stdin/stdout. `stdout` to **wylacznie** protokol, diagnostyka na `stderr`.

```json
{"id":"1","op":"files.play","args":{"path":"D:\\muzyka\\a.mp3","volume":35,"rate":1.0}}
{"id":"1","result":{"playing":true,"durationSeconds":212.4}}
{"event":"playback.ended","data":{}}
```

Zasady: `id` jest **napisem**; zly JSON daje blad, nie zabija hosta; linia nad
64 KB odrzucona przed parsowaniem; EOF konczy hosta. Operacje: `host.hello`,
`files.listFolder`, `files.play`, `radio.play`, `radio.importPlaylist`,
`transport.pauseResume|stop|seek|setVolume|setRate|status`, `audio.configure`,
`audio.outputs`, `host.shutdown`.
