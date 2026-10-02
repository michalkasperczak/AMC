# Pochodzenie i licencje kodu obcego - silniki tempa AMC

Katalog `native/AmcTempoEngines/vendor/` zawiera kod upstream pobrany z
podanych nizej zrodel. Poza zmianami wymienionymi w sekcji "Nasze zmiany",
pliki sa niezmienione. Wlasny adapter AMC lezy w `../src/` i NIE jest
kopia zadnego cudzego wrappera (w szczegolnosci nie jest kopia wrappera
FastPlay, ktorego licencji nie ustalono - nie uzywamy go).

## Skladniki

### speedy (mowa, nieliniowe przyspieszanie)
- Zrodlo: https://github.com/google/speedy
- Commit (pin): e05c9b6fa473881e9c2b5ddaa323435b5513012c
- Licencja: Apache License 2.0 (plik `speedy/LICENSE`)
- Wlasciciel praw: Google LLC
- Pliki: `speedy.c`, `speedy.h`, `soniclib.c`, `sonic2.h`,
  `dynamic_time_warping.h`
- Uwaga do API: w TYM przypietym commicie `sonicEnableNonlinearSpeedup` ma
  w `soniclib.c` sygnature `(sonicStream, float)` - czyli JEDEN parametr
  tuningu. Wartosc 0 wylacza Speedy i zostawia czysty Sonic. Komentarz w
  `sonic2.h` wspomina o czasie normalizacji, ale kod przypietej wersji go
  nie przyjmuje. Adapter trzyma sie KODU, nie README.
- Uwaga do kontraktu: `sonicWriteFloatToStream` zwraca FLAGE powodzenia
  (1/0), a nie liczbe przyjetych ramek - przepisuje caly podany blok.

### sonic (baza dla speedy)
- Zrodlo: https://github.com/waywardgeek/sonic
- Licencja: Apache License 2.0 (plik `sonic/LICENSE`)
- Wlasciciel praw: Bill Cox
- Pliki: `sonic.c`, `sonic.h`

### kissfft (zaleznosc speedy)
- Zrodlo: https://github.com/mborgerding/kissfft (kopia z drzewa speedy)
- Licencja: BSD-3-Clause (plik `kissfft/COPYING`)
- Wlasciciel praw: Mark Borgerding
- Pliki: `kiss_fft.c`, `kiss_fft.h`, `_kiss_fft_guts.h`, `kiss_fft_log.h`

### signalsmith-stretch (muzyka, time-stretch z zachowaniem wysokosci)
- Zrodlo: https://github.com/Signalsmith-Audio/signalsmith-stretch
- Commit (pin): a670068d9aeb64913331d5cc29337b19a457a7df
- Licencja: MIT (plik `signalsmith-stretch/LICENSE.txt`)
- Wlasciciel praw: Signalsmith Audio Ltd / Geraint Luff
- Pliki: `signalsmith-stretch.h` (header-only)

### signalsmith-linear (zaleznosc signalsmith-stretch)
- Zrodlo: https://github.com/Signalsmith-Audio/linear
- Licencja: MIT (plik `signalsmith-linear/LICENSE.txt`)
- Wlasciciel praw: Signalsmith Audio Ltd / Geraint Luff
- Uklad katalogow: naglowki sa zarowno w korzeniu, jak i w
  `include/signalsmith-linear/` - stretch wlacza je jako
  `"signalsmith-linear/stft.h"`, dlatego CMake dodaje `include/`.

## Nasze zmiany w kodzie obcym

Celowo minimalne, tylko po to, zeby biblioteka nie pisala na stdout
procesu-hosta (DLL w aplikacji GUI nie moze zasmiecac strumieni):

1. `speedy/soniclib.c` - dwa bezwarunkowe `printf` ("Allocating %d buffers",
   "speedyBufferSize is %d") zamkniete w `#ifdef DEBUG`. Zadnej zmiany
   logiki przetwarzania dzwieku.

Nic wiecej nie zostalo zmienione. Zadnych zmian w algorytmach.

## Pakowanie DLL

Z tych zrodel budowany jest JEDEN plik `AmcTempoEngines.dll` (win-x64),
statycznie zawierajacy speedy+sonic+kissfft+signalsmith. Nie ma dodatkowych
zaleznosci run-time poza CRT. Budowanie: `native/AmcTempoEngines/CMakeLists.txt`.
Brak DLL = jawny blad w warstwie C# (powrot do SoundTouch), nie ciche udawanie.

## Sumy kontrolne SHA-256 zawartosci tego katalogu

```
a2840585f8411be8e6826a31ef15ae65c950bd74a2437a73b013398a934ad0c6  kissfft/COPYING
8140ad59872effc33922c954f3a682a2dc5668532de8000a134e1a6c0e994595  kissfft/_kiss_fft_guts.h
84154815c4e734bdc986fae5f2301212b1ed4dd91e005bc88680218d4bc3bae3  kissfft/kiss_fft.c
6907d7d90187eab2cd1e264e11d986b322e419e1a543846d4529da0f23218d47  kissfft/kiss_fft.h
f954cb6890ec999f7fbc80cdd1c8c0194bbaeb0f1c27e11bd23450f53871cf8c  kissfft/kiss_fft_log.h
072b5e9eb5b22880bdf6256324654cf3ce53dba828edb911921121e1be09d9c3  signalsmith-linear/LICENSE.txt
bf6f703193253a241e7a80d44547bb73c383a9a1f1ebe1f7fefdcf3d234515bd  signalsmith-linear/approx.h
fb55f69ac14cdbac66b9c76053069ff63bc36d18af20d8c028b9acc0aad6516a  signalsmith-linear/fft.h
55c090c621657ee1fbe23a8c582811e103acf8ba10263c4a6d9b66a62809fa1a  signalsmith-linear/include/signalsmith-linear/approx.h
98e7e64093b6fc8ccbba31f710370d5548243bad35beb489e8282de4dc25d76d  signalsmith-linear/include/signalsmith-linear/fft.h
5c5d55810af0b7f8c8fe27ef1a36a87a4529f95cbebbb5e49f39845f6a2a0d5e  signalsmith-linear/include/signalsmith-linear/linear.h
a323656e5ed80b271262106ea782d6150ef2f1c8b2f3c5896552b90b2ad6c17e  signalsmith-linear/include/signalsmith-linear/stft.h
9e777fe14653d496f5e0678fe4b9d8b01a0909bb2130bf4757a21ccad7292a02  signalsmith-linear/linear.h
c2ebbedbbbcf4b13fa236404a249d418a07f6edfc5ef2effd08d8be1e594b02e  signalsmith-linear/platform/basic-fill-warnings.h
7c2ed61df7895af7b1a37f1073f833abdc971953277ece58128c9c7b4ca6cef2  signalsmith-linear/platform/fft-linear.h
7422ff5ddc2a017dc0587dcce99ce5ba30a2414bc2996081f6b00d56905d9b8c  signalsmith-linear/stft.h
ee2ef82481ffb445ecdd4b3a4c1f82c0cddb2da8fe39d8e8dc384fffb3e7f06f  signalsmith-stretch/LICENSE.txt
1188667959ac19dd40c0a6abbce694e44705615ec4f0e8db8af0f1cfb4c5dea7  signalsmith-stretch/signalsmith-stretch.h
cfc7749b96f63bd31c3c42b5c471bf756814053e847c10f3eb003417bc523d30  sonic/LICENSE
4a21d8086f844e3e68cb4f85961623a69d35da8ca93cf6ea704ccd125e64eaf5  sonic/sonic.c
a2fc087b68c25141e2fb7ab56e71b111bee79cf311fcc850d492eb574c0eeffe  sonic/sonic.h
cfc7749b96f63bd31c3c42b5c471bf756814053e847c10f3eb003417bc523d30  speedy/LICENSE
b09134851fa16fb44d606de00051f505917115c38d7f307c6dd9a86460796f29  speedy/dynamic_time_warping.h
ed941d2665193f6e2902383106e21b5f2d7f3cfb57d20be4bad6a5f1dec6f2f1  speedy/sonic2.h
f652c995c4c6ad61d56dd4fbc26911613f5d1d40d17297d872edf6125a85d34b  speedy/soniclib.c
2e73ca4306279cb8a8f774fab283d04de73f05d78dc7b3a454c78da36feddd0d  speedy/speedy.c
08777f5e7ba4df479017eb29e67567f80cb527f478945df62d9bd969bd44ad2d  speedy/speedy.h
```
