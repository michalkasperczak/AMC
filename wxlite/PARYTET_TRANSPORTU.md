# Parytet TRANSPORTU: wxPython wobec oryginalnego AMC (C#)

Dokument opisuje, co w warstwie transportu wariantu wxPython odpowiada ktoremu
miejscu w kodzie oryginalu, funkcja po funkcji. Powstal po uruchomieniu wersji
`cd326da1` na prawdziwym profilu, gdy okazalo sie, ze transport rozjechal sie z
AMC w kilku punktach naraz.

**Zasada nadrzedna.** Wzorcem sa reguly oryginalu (C#) i ustawienia uzytkownika
w `state.json`. Python ich nie wymysla na nowo: albo czyta je z profilu, albo
odwzorowuje literaly z kodu C# — a wtedy pilnuje ich testem, ktory CZYTA zrodlo
C# i pada, gdy wartosci sie rozjada
(`tests/test_transport_parity_matches_csharp_source.py`).
Silnik audio, biblioteka i nawigacja plikow zostaja po stronie C#; ta zmiana
niczego z nich nie przepisuje.

---

## 1. Czasy na zadanie: Ctrl+Shift+E / R / T

| | |
|---|---|
| Oryginal: klawisze | `MainWindow.xaml.cs:21674-21676` (okno listy), `22190-22192` (okno odtwarzacza) — `ModifierKeys.Control \| ModifierKeys.Shift` + `Key.E/R/T` |
| Oryginal: tresc | `CommandRouter.cs:325-335` → `AnnounceTemplate("time.elapsed"/"time.remaining"/"time.total")`, domyslne szablony `{elapsed}`/`{remaining}`/`{total}` (`AppSettings.cs`, `MessageSettings.CreateDefault`) |
| Oryginal: format | `MediaItemFormatter.FormatDuration` (`cs:69-73`): `h:mm:ss` od godziny w gore, inaczej `m:ss` |
| Bylo w Pythonie | `Ctrl+E/R/T` (bez Shift) oraz doklejane slowa `Minelo` / `Pozostalo` / `Calosc` (`gui.py:_announce_time`) |
| Jest | `Ctrl+Shift+E/R/T` w obu widokach; tresc to **sam czas** z szablonu |
| Dlaczego bez slow | Release `.383` cytuje wprost `3:51`, bez `Minelo`. Slowo moze dodac uzytkownik, wpisujac np. `Minęło {elapsed}` w ustawieniach AMC — i wtedy je slyszy. |
| Dlaczego nie ma aliasu `Ctrl+E` | W warstwie OKNA `Ctrl+E` nalezy do eksportu ulubionych stacji (`cs:21662`). `KeyboardProfile.cs:70-72` wiaze `Ctrl+E/R/T` z czasami, ale to profil czytany **po akordzie prefiksu** — inna warstwa. Alias zabralby istniejaca komende. |
| Test | `test_transport_parity.py` (tabele + format), `test_transport_parity_gui_wiring.py` (tresc ze sciezki okna) |

Czas na zadanie odpowiada **takze przy wylaczonych komunikatach** — `AnnounceTemplate`
nie pyta tu o `SeekMessages`. To pytanie recznie zadane przez uzytkownika, nie
zapowiedz automatyczna.

### Pasek czasu w oknie

Ten sam format obowiazuje na ekranie, nie tylko w mowie. `MainWindow.xaml.cs:2643-2647`
sklada etykiete jako `0:30 z 3:51`, bez znanej dlugosci pokazuje sama pozycje, a
bez odtwarzania mowi `Stan czasu nieznany`. Wariant wx mial tu wlasny, dluzszy
format (`30 s / 3 min 51 s`) — poprawione, bo to ekran, ktory uzytkownik
porownuje z oryginalem wprost.

## 2. Kroki przewijania: cztery pary, nie dwie

| Gest | Krok | Komenda oryginalu | Zrodlo |
|---|---|---|---|
| `Left` / `Right` | 10 s | `SeekBackward10` / `SeekForward10` | `cs:21583-21584`, `22387-22388` |
| `Shift+Left` / `Shift+Right` | **30 s** | `SeekBackward30` / `SeekForward30` | `cs:21585-21586`, `22389-22390` |
| `Ctrl+Left` / `Ctrl+Right` | **60 s** | `SeekBackward60` / `SeekForward60` | `cs:21587-21588`, `22391-22392` |
| `Ctrl+Alt+Left` / `Ctrl+Alt+Right` | czas z ustawien | `SeekBackwardCustom` / `SeekForwardCustom` | `cs:21589-21590`, `22393-22394` |

Bylo: `Shift` = 60 s, `Ctrl+Left/Right` = brak. Dwa gesty robily to samo, a dwie
pary nie istnialy. Nazwy komend w `CommandIds.cs:14-27` zawieraja liczbe sekund,
wiec test porownuje nasz krok z **nazwa komendy oryginalu** — pomylka „Shift to
60 s z pamieci” jest teraz niemozliwa.

Czas wlasny pochodzi z `settings.customSeekSeconds` w profilu AMC, przyciety jak
w `AppSettings.cs` (`Math.Clamp(5, 1800)`, domyslnie 300 s). Nie jest to u nas
stala — release `.367` opisuje go jako 5 minut, ale wartosc nalezy do
uzytkownika.

## 3. Skok procentowy: cyfry 0–9

Oryginal: `cs:22348-22350` — goła cyfra bez modyfikatora, `CommandIds.SeekPercent(digit * 10)`.
U nas tylko w widoku odtwarzacza; na liscie cyfry zostaja przy natywnej
kontrolce (wpisywanie/nawigacja), tak jak w AMC.

Przeliczenie procentu na pozycje robi klient, a host dostaje **pozycje
bezwzgledna** — `transport.seek` juz przyjmuje `positionSeconds`
(`LiteEngineHandlers.cs:655`), wiec protokol nie wymagal nowej komendy.

Brak znanego czasu trwania: oryginal (`CommandRouter.cs:546-551`) mowi o tym
**zawsze**, bo to blad wykonania, nie rutynowy komunikat. Milczenie wygladaloby
na niedzialajacy klawisz.

## 4. Polityka komunikatow: koniec bezwarunkowego gadania

To byl najglosniejszy objaw: przy kazdej strzalce leciał czas.

| Zapowiedz | Bramka w oryginale | Zrodlo |
|---|---|---|
| przewijanie strzalka | `Messages.SeekMessages && Messages.ArrowSeekMessages` | `CommandRouter.cs:537` |
| skok procentowy | `SeekMessages && PercentageSeekMessages` (+ tryb `PercentageSeekAnnouncement`) | `cs:555-562` |
| glosnosc | `SeekMessages && VolumeMessages`, szablon `volume.changed` = `{value}%` | `cs:570-573` |
| mowa w ogole | `Messages.Enabled` — wylacznik **mowy**; tekst nadal idzie do pola statusu | `MainWindow.xaml.cs:659-664` |

Przelaczniki czytamy z prawdziwego `state.json` (`settings.messages`,
camelCase jak zapisuje `ConfigurationStore`). Odczyt **tylko do odczytu**:
wlascicielem pliku pozostaje host C#, drugiego pisarza nie wprowadzamy.
Brak albo uszkodzony plik daje domysly oryginalu — transport musi dzialac,
a cisza „bo nie wczytano ustawien” byla by dla uzytkownika czytnika gorsza.

Tryby zapowiedzi procentu (`cs:558-561`) sa trzy i dokladnie te trzy:
`Percent` → `50%`, `Time` → `3:51`, `PercentAndTime` → `50%, 3:51`.

### Ctrl+Shift+G — przelacznik, ktory nie udaje

`CommandIds.SettingsToggleSeekMessages` (`cs:21670`, `22188`). We wspolnym
profilu gest **nie zapisuje nic po cichu**: mowi, ze opcja nalezy do AMC i gdzie
ja zmienic. W prywatnej piaskownicy (`AMC_WX_FIXTURE`) przelacza naprawde i
wynik jest **wykonywany** — po wylaczeniu strzalka faktycznie milczy. Martwe
pole byloby gorsze od braku pola.

## 5. Predkosc odtwarzania

| | |
|---|---|
| Drabina | `DemoMediaSession.cs:11`: `0,50 0,75 1,00 1,25 1,50 1,75 2,00` — siedem szczebli, nie suwak co 0,1 |
| Krok | `ChangePlaybackRate` (`cs:364-376`): indeks na drabinie ± kierunek, przyciety do konca |
| Wartosc z boku | `FindLastIndex(rate < current)`, potem kierunek — dla 1,10 w dol daje **0,75**, nie 1,00. Odwzorowane celowo: ten sam klawisz nie moze robic w dwoch programach dwoch roznych rzeczy. |
| Ustawienie wprost | `SetPlaybackRate` (`cs:381`): `MinBy(|rate − x|)` — **przyciaga do najblizszego szczebla**, nie przycina do przedzialu |
| Klawisze | `Shift+,` wolniej, `Shift+.` szybciej, `Ctrl+.` normalna — `MainWindow.xaml:382-393` |
| Komunikat | `CommandRouter.cs:600-604`: `Prędkość 1,25 razy`, `Prędkość normalna` (przecinek dziesietny, bez zer na koncu) |

Bylo: `Ctrl+Up/Down` ± 0,1, zakres suwaka 50–200%, komunikat `Tempo 125 procent`,
a w mapie klawiszy i w menu **nie bylo nic** — dlatego regulacji nie dalo sie
znalezc. Sam suwak w oknie nie jest odbiorem: uzytkownik czytnika szuka funkcji
w menu, w pomocy `F1` i pod klawiszem z oryginalu.

Jest: oryginalne klawisze, pozycje w menu **Odtwarzanie** (`Wol&niej` /
`&Szybciej` / `Prędkość no&rmalna` z akceleratorami), wiersze w pomocy `F1`,
nazwa suwaka `Predkosc odtwarzania w procentach` i zakres rowny drabinie
(50–200%, bo drabina konczy sie na 2,00).

**Nie zbadane w tej zmianie:** jakosc brzmienia bibliotek przyspieszania
(Speedy/Signalsmith/SoundTouch). Menu `Dzwiek → Algorytm przyspieszania`
istnieje i wysyla wybor do hosta, ale ocena brzmienia wymaga odsluchu — jest na
liscie prob dla rodzica. Nie twierdzimy, ze cokolwiek tu „brzmi dobrze”.

## 6. Czego ta zmiana NIE robi

- nie przepisuje silnika audio ani logiki C# — host zostaje wlascicielem odtwarzania;
- nie dotyka biblioteki, kolejki, widokow ani otwierania plikow;
- nie zmienia NVDA i nie zaklada zadnych przechwytywaczy gestow czytnika;
- nie zapisuje do profilu AMC (jeden pisarz: host C#);
- nie zajmuje `NVDA+Up` / `End` ani innych standardowych gestow czytnika;
- nie zmienia opublikowanych opisow wydan.

## 7. Czego nie da sie udowodnic bez zywego hosta i NVDA

Testy pokazuja, **co kod wola** i z jakimi liczbami. Nie slychac w nich mowy i
nie plynie w nich czas. Do odbioru u rodzica (jeden waski cykl):

1. `Ctrl+Shift+E/R/T` w obu widokach — czy NVDA mowi sam czas w formacie `3:51`.
2. Strzalki przy `ArrowSeekMessages=false` — czy jest cisza; po `true` czy wraca czas.
3. `Shift+strzalka` i `Ctrl+strzalka` — czy slychac i widac ruch o 30 s i o 60 s.
4. `Ctrl+Alt+strzalka` — czy skok rowna sie `customSeekSeconds` z profilu.
5. Cyfry 0–9 w odtwarzaczu — czy pozycja ladzie na wlasciwym procencie; na liscie czy nie przeszkadzaja.
6. `Shift+,` / `Shift+.` / `Ctrl+.` — czy tempo **naprawde** sie zmienia (pomiar czasu, nie sam komunikat) i czy NVDA czyta `Prędkość 1,25 razy`.
7. `Ctrl+Shift+G` na wspolnym profilu — czy mowi o AMC i czy niczego nie zapisal.
8. Menu `Odtwarzanie` i pomoc `F1` — czy regulacja predkosci jest **znajdywalna** klawiatura.
9. Odsluch algorytmow przyspieszania — osobno, to ocena jakosci brzmienia.
