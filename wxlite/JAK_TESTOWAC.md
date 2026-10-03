# AMC-wx-Lite — jak to uruchomic i sprawdzic

Lekki wariant AMC: **pliki lokalne** i **radio internetowe**, natywne kontrolki
wxPython, a dzwiek robi **istniejacy silnik AMC** w malym bezokiennym hoscie .NET.
Pelny AMC zostaje nietkniety — ten wariant ma wlasny folder stanu i wlasny proces.

---

## 1. Co jest juz zmierzone, a co czeka na Windows

### Zmierzone w WSL (bez pulpitu, bez NVDA)

| Co | Jak sprawdzone | Wynik |
|---|---|---|
| Logika listy, nawigacji, stanu, skrotow, protokolu | `python3 run_tests.py` | **124 testy, 0 błędów, 0 pominięć** |
| Protokol po stronie hosta (C#) | `dotnet run --project tests/...ProtocolTests` | **4 zestawy OK**, w tym wybór rzeczywistego enuma Core |
| Zgodnosc Python ↔ **prawdziwa petla C#** | `run_tests.py test_wire` | **14 testow OK** |
| Crossbuild hosta na Windows | `dotnet publish -r win-x64` | **publish win-x64 self-contained: exit 0** |
| Zlozenie pakietu | `tools/build_bundle.py` | **układ app/runtime/host, zachowane licencje i hasze bibliotek** |

### NIE zmierzone — wymaga pulpitu Windows

- Samo **okno** (wxPython potrzebuje pulpitu).
- **Realne odtwarzanie** pliku i stacji (NAudio/WASAPI dzialaja tylko na Windows).
- Zachowanie z **NVDA i Narratorem**.

Wykonano testy wymienionych modeli, protokołu i adaptera C# wywołującego
prawdziwe biblioteki natywne. Testy kontrolek na zastępnikach wx nie zastępują
uruchomienia okna z czytnikiem. Próbka PCM nie jest oceną brzmienia mowy.

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
`TimeshiftTempoStage`, `RadioMediaOutput`, `RadioPlaylistImporter`,
`LocalAudioFileDiscovery`) + `Core`. Zadnego `MainWindow`, zadnego WPF,
zadnych wlasnych dekoderow w Pythonie.

---

## 6. Granice tej wersji (swiadome)

- **Dodatek NVDA**: zwykla obsluga + krotkie standardowe komunikaty. Zgodnosc
  protokolu dodatku bez kolizji z pelnym AMC sprawdzi rodzic osobno.
- **Algorytm tempa**: menu Dźwięk → Algorytm przyspieszania zawiera Mowa —
  Speedy, Muzyka — Signalsmith i Dotychczasowy — SoundTouch. Wybór trafia do
  rzeczywistych silników plików i bufora radia oraz do prywatnych ustawień.
  Obecnie zmiana algorytmu obowiązuje przy utworzeniu nowego strumienia;
  zmiana samej prędkości działa w bieżącym strumieniu. Gdy materiał jest już
  otwarty, nie traktuj nazwy wybranej w menu jako dowodu przełączenia aktywnego
  procesora. Na żywo radio nie może przyspieszać niezarejestrowanego jeszcze dźwięku.
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

---

## 8. Biblioteka z prawdziwego profilu (etap 3, checkpoint 1)

### Co sie zmienilo

Biblioteka pod **Ctrl+1** czyta teraz `library.db` (SQLite), a nie dysk.
Wczesniej `_load_initial_content` decydowalo przez `Path(folder).exists()` —
a sciezki z profilu (`D:\…`, `C:\Users\micha\…`) na maszynie testowej nie
istnieja, wiec lista byla **pusta**. To byl zglaszany blad.

### Jak uruchomic na kopii danych

```bash
export AMC_WX_FIXTURE=/home/michal/projekty/amc_pomoc/wx-full-profile-after421/fixture
cd wxlite && python3 run_tests.py
```

Bez `AMC_WX_FIXTURE` wariant uzywa prawdziwego profilu
(`%LOCALAPPDATA%\AccessibleMediaController`) w trybie **tylko do odczytu**.

### Zasady, ktorych nie wolno zlamac

- Baza otwierana jako `mode=ro&immutable=1`. `immutable=1` jest konieczne,
  zeby nie powstaly pliki `-wal`/`-shm` obok profilu — ich utworzenie to **juz
  zapis** do cudzych danych.
- Skutek uboczny: czytelnik nie widzi zmian hosta na otwartym uchwycie.
  Dlatego `LibrarySource` otwiera baze **na kazdy odczyt**, nie trzyma jednego
  polaczenia.
- Kolacja `AMC_PL` musi byc zarejestrowana, inaczej SQLite odmawia zapytan
  o `title` / `display_name`. Port z C#: `pl-PL`, `IgnoreCase | IgnoreNonSpace`.
- ID sa **napisami**. Nie rzutowac na `int`, nie przenumerowywac.
- Nie filtrowac wierszy przez `Path.exists`. Dostepnosc bierzemy z kolumny
  `is_available`, ktora zapisal host.

### Co sprawdzic RECZNIE na Windows (tu sie nie da — brak pulpitu)

1. Ctrl+1 — czy lista pokazuje 3 foldery zrodlowe + 7 pojedynczych plikow;
2. Enter na „Kazania Dominikanie Grobla" — czy wchodzi i czy jest wiersz `..`;
3. Backspace / Enter na `..` — czy wraca na opuszczony folder (fokus!);
4. NVDA: czy nazwy z polskimi znakami sa czytane w calosci i w dobrej
   kolejnosci (Ł nie moze ladowac na koncu alfabetu);
5. Ctrl+2 i powrot Ctrl+1 — czy pozycja na liscie sie nie gubi;
6. czy okno **nie zamarza** na czas ladowania (na WSL odczyt to 200–320 ms,
   na Windows moze byc inaczej).

Pomiar predkosci porownuj tylko przy **tej samej liczbie rekordow** — mala
lista zawsze bedzie szybsza i nie dowodzi niczego o jezyku.

