# Silniki tempa AMC - jak to zbudowac i sprawdzic

Katalog zawiera natywna czesc nowych algorytmow tempa:

- **Mowa** - Google Speedy nad libsonic. Przyspiesza **nieliniowo**: ciche i
  trudne fragmenty skraca mocniej niz wyrazna mowe.
- **Muzyka** - Signalsmith Stretch (fazowy vocoder), trzyma barwe muzyki.
- **Dotychczasowe** - SoundTouch po stronie C#, bez zmian; nie dotyka tego kodu.

Wlasny adapter AMC jest w `src/`. Kod obcy w `vendor/` jest przypiety; jego
pochodzenie, sumy SHA-256, licencje i **nasze zmiany** opisuje
[`vendor/PROVENANCE.md`](vendor/PROVENANCE.md).

## Budowanie w WSL (Linux, do testow)

```bash
cmake -S native/AmcTempoEngines -B /tmp/amc-tempo-build -G Ninja -DCMAKE_BUILD_TYPE=Release
cmake --build /tmp/amc-tempo-build
```

## Budowanie biblioteki dla Windows x64 (cross-build przez mingw)

Toolchain (`/tmp/mingw-toolchain.cmake`):

```cmake
set(CMAKE_SYSTEM_NAME Windows)
set(CMAKE_SYSTEM_PROCESSOR x86_64)
set(CMAKE_C_COMPILER x86_64-w64-mingw32-gcc-posix)
set(CMAKE_CXX_COMPILER x86_64-w64-mingw32-g++-posix)
set(CMAKE_RC_COMPILER x86_64-w64-mingw32-windres)
set(CMAKE_FIND_ROOT_PATH /usr/x86_64-w64-mingw32)
set(CMAKE_FIND_ROOT_PATH_MODE_PROGRAM NEVER)
set(CMAKE_FIND_ROOT_PATH_MODE_LIBRARY ONLY)
set(CMAKE_FIND_ROOT_PATH_MODE_INCLUDE ONLY)
```

```bash
cmake -S native/AmcTempoEngines -B /tmp/amc-tempo-win -G Ninja \
  -DCMAKE_BUILD_TYPE=Release -DCMAKE_TOOLCHAIN_FILE=/tmp/mingw-toolchain.cmake
cmake --build /tmp/amc-tempo-win
```

Wynik: `AmcTempoEngines.dll` (PE32+ x86-64). Biblioteka zalezy **tylko** od
`KERNEL32.dll` i `msvcrt.dll` - nie trzeba dokladac bibliotek mingw. Gotowa
kopia lezy w `third_party/AmcTempoEngines/win-x64/` i jej suma SHA-256 jest
sprawdzana przy budowaniu projektu Windows (cel `VerifyPinnedTempoEnginesBinary`).
Po przebudowaniu biblioteki trzeba **zaktualizowac** `AmcTempoEnginesExpectedSha256`
w `src/AccessibleMediaController.Windows/AccessibleMediaController.Windows.csproj`.

## Sonda PCM - co faktycznie mierzy

```bash
/tmp/amc-tempo-build/amc_tempo_probe
```

Material to **jawny syntetyczny fixture** (ton z modulacja i przerwami ciszy),
a nie odsluch. Sonda sprawdza na prawdziwych probkach:

| Co | Dlaczego to wazne |
|----|-------------------|
| Tempo faktycznie zmienia dlugosc | Czy silnik naprawde pracuje, a nie przepuszcza dzwieku |
| Mono i stereo | Czy kanaly nie sa mieszane ani gubione |
| Wszystkie probki skonczone | Brak NaN/nieskonczonosci, czyli brak trzaskow |
| Tempo 1,0x jest obejsciem | Dzwiek niezmieniony, gdy uzytkownik nie zmienia tempa |
| Reset czysci stan | Po przewinieciu nie slychac resztek poprzedniego miejsca |
| Domkniecie konca (flush) | Koniec nagrania nie jest obcinany |
| Nieliniowosc Speedy wlaczona | Sila 1,0 daje **inny** wynik niz 0,0 (czysty Sonic) |

## Granice mapowania czasu (wazne przy nieliniowym tempie)

Speedy przyspiesza nieliniowo, wiec czasu w materiale **nie wolno** liczyc jako
`wyjscie * tempo`. `NativeTempoStream` bierze pozycje i dlugosc **wprost z
czytnika zrodla** - to jedyna rzetelna miara dla takiego silnika. Cena tego
wyboru: pozycja wyprzedza to, co uzytkownik slyszy, o material zakolejkowany w
silniku (rzad setek milisekund). Przy przewijaniu kolejka jest czyszczona, wiec
blad nie kumuluje sie miedzy przeskokami.

## Brak biblioteki - co sie dzieje

Gdy `AmcTempoEngines.dll` nie da sie wczytac albo ma inna wersje ABI, wybor
Mowa/Muzyka **nie udaje dzialania**: `NativeTempoStream.TryCreate` zwraca `null`
z czytelnym powodem, a tor wraca do SoundTouch i zapisuje powod w logu.
