# Mapa 1:1 — czego brakuje do pelnego AMC w wxPython

Stan po checkpoincie 1 (etap 3). **To NIE jest gotowy AMC 1:1.** Ponizsza mapa
jest policzona z PRAWDZIWEGO menu WPF, a nie wymyslona jako ogolna lista TODO.

## Jak policzone

```
grep -oP 'Header="\K[^"]+' src/AccessibleMediaController.Windows/MainWindow.xaml   # 237 pozycji
grep -oP 'InputGestureText="\K[^"]+' .../MainWindow.xaml                           # 196 skrotow
```

Zrodlo: `amc-cloud-edit-release421` @ `d029573`.

| Miara | AMC (WPF) | wxPython dzis | Pokrycie |
|---|---|---|---|
| Pozycje menu | 237 | 0 (brak paska menu 1:1) | ~0% |
| Skroty z menu | 196 | 20 akcji | ~10% |
| Sesje | local, radio, tidal, wiim, spotify, podcast | local (Biblioteka) + radio | 2/6 |
| Widoki Biblioteki | Foldery, Wszystkie pliki, Albumy, Ulubione, Kolejka, Historia, Zakladki, Playlisty, Presety, Nagrywane… | Foldery | 1/~12 |

## Co DZIALA po tym checkpoincie

- Odczyt `library.db` (11 200 rekordow, 2 475 aktywnych) z kolacja `AMC_PL`.
- Widok **Foldery** Biblioteki: 3 zrodla + 7 sierot w korzeniu, zejscie w glab,
  wiersz `..`, polskie nazwy, ID jako napisy.
- Twarda zasada posiadania profilu: Python tylko czyta, host C# zapisuje.

## Czego BRAKUJE — po obszarach (z menu)

### Plik (27 pozycji)
Otworz pliki/folder, Foldery Biblioteki…, urzadzenia WiiM, konta TIDAL /
Spotify / Sonos, grupy i dom Sonos, import/eksport stacji i strumieni,
podcasty (OPML), kanaly YouTube, pobieranie odcinkow, **Ustawienia…**.
Dzis w wxPython: tylko „Otworz plik/folder" i import stacji.

### Edycja (5)
Cofnij zmiane, zmiana nazwy w Bibliotece, zmiana nazwy na dysku,
przenoszenie w gore/dol listy. Dzis: brak — i wszystkie te operacje **pisza**
do profilu, wiec wymagaja decyzji o wlascicielu zapisu (patrz nizej).

### Sesja (3)
Lista sesji, poprzednia/nastepna sesja. Dzis: tylko Ctrl+1/Ctrl+2.

### Odtwarzanie (ok. 60)
Wyciszanie sesji, wybor urzadzenia audio, globalna normalizacja glosnosci,
lagodne przejscia, cisza miedzy utworami, **nagrywanie radia** (start, pauza,
nowa czesc, stop, harmonogram), rozpoznawanie utworow, zakladki (4),
rozdzialy (4), fragmenty (10 — zaznaczanie, zapis, dopisanie, usuwanie),
skok do czasu/procentu, tempo, wlasciwosci, opcje elementu i sesji.
Dzis: play/pauza, tempo, czasy.

### Widok (ok. 25)
Teraz odtwarzane, Ulubione, Playlisty, Presety, Biblioteka (podwidoki),
podcasty Spotify, nowe odcinki, w trakcie sluchania, pobrane,
Foldery Biblioteki, wszystkie pliki alfabetycznie, kolejnosc wlasna,
wg dodania, albumy, kolejka, historia odtwarzania, zakladki, filtr,
szukanie lokalne i globalne, **paleta polecen**.
Dzis: Foldery + lista stacji.

## Dane, ktorych jeszcze nie ruszamy

| Tabela | Rekordy | Status |
|---|---|---|
| `local_items` | 11 200 | czytane |
| `folder_sources` | 3 | czytane |
| `bookmarks` | 5 000 | **nieczytane** |
| `excluded_paths` | 8 147 | nieczytane |
| `favorite_order` / `favorite_added_order` | 7 926 / 7 934 | nieczytane |
| `custom_order` | 11 200 | nieczytane |
| `library_added_order` / `library_custom_order` | 2 565 / 2 437 | nieczytane |
| `playback_history` | 811 | nieczytane |
| `queue_order` / `queue_regular_order` | 92 / 92 | nieczytane |
| `playlists` / `playlist_items` | 1 / 54 | nieczytane |
| `folder_playback_options` | 5 | nieczytane |
| `podcast_subscriptions` | 338 | nieczytane |
| `podcast_episodes` | 47 455 | nieczytane |

Ustawienia, harmonogramy i presety siedza w `state.json` (11,5 MB) — jeszcze
nieparsowane.

## Decyzja, ktora trzeba podjac przed etapem „zapis"

Dzis Python **tylko czyta** i to jest bezpieczne. Kazda funkcja z menu, ktora
zmienia dane (zakladki, ulubione, kolejnosc, zmiana nazwy, harmonogramy)
wymaga wybrania jednego z wariantow:

1. **Python wola hosta C#** (host zostaje jedynym pisarzem) — najbezpieczniejsze,
   wymaga rozszerzenia protokolu LiteHost o operacje zapisu;
2. **Python pisze sam, gdy AMC nie dziala** — wymaga blokady (mutex
   `Local\AccessibleMultimediaController.SingleInstance`) i obslugi migracji
   schematu; ryzyko podwojnego zapisu.

Rekomendacja: wariant 1. Nie dubluje duzej logiki z WPF i nie tworzy drugiego
wlasciciela stanu.

## Ograniczenia pomiaru

- Kopia danych jest z **25.09.2026**, nie jest biezaca Biblioteka z 03.10.
- Odtwarzanie i NVDA nie byly tu mierzone (brak pulpitu Windows w WSL).
- Czasy ladowania (200–320 ms) zmierzone na WSL, nie na docelowym Windows.
