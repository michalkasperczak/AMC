# Wyniki testów AMC 0.1.0-alpha.181

## Weryfikacja automatyczna

- Kompilacja Release: OK, 0 ostrzeżeń i 0 błędów.
- Testy rdzenia: OK w konfiguracji Release, w tym wspólna klasyfikacja pliku
  lokalnego, pliku zdalnego i strumienia sieciowego.
- Testy Windows: OK w konfiguracji Release, w tym nieblokująca kolejka zapisu,
  łączenie stanów pośrednich i końcowe opróżnienie najnowszej migawki.
- Dostępność harmonogramu: OK; pole daty zachowuje segmentową obsługę
  strzałkami, rolę pola pokrętła oraz użytkową nazwę i opis dla NVDA.
- Dotychczasowe testy Radia, harmonogramów, nagrywania, formatów, Shazam,
  odtwarzania lokalnego i dekoderów: OK.
- Publikacja samowystarczalnego programu dla `win-x64`: OK.

## Pakiet

- Program: `publish\AccessibleMediaController-0.1.0-alpha.181\AccessibleMediaController-0.1.0-alpha.181.exe`.
- Wersja produktu: `0.1.0-alpha.181`.
- Rozmiar programu: `165953952` bajtów.
- SHA-256: `9424542108E1B8E0EAD1700144B8F088B59E52AF7790703AC970B158DCEBAFA2`.

## Test ręczny

### AMC-181-01 — fokus i automatyczny zapis

Odtwórz dłuższy plik przez co najmniej minutę. W odtwarzaczu naciskaj strzałki
w czasie, gdy AMC automatycznie zapamiętuje pozycję. Fokus nie powinien
przeskakiwać ani przestawać reagować; Escape ma zawsze wrócić do listy.

Uwagi:

### AMC-181-02 — plik dociągany z dowolnej chmury

Powtórz odtwarzanie i przewijanie dla pliku dostępnego tylko online w iCloud,
OneDrive, Dysku Google albo Dropbox. Program może oznajmić oczekiwanie na
źródło, lecz lista, odtwarzacz i fokus mają pozostać dostępne. Po pobraniu
odtwarzanie powinno rozpocząć się bez restartu AMC.

Uwagi:

### AMC-181-03 — trwałość wszystkich sesji

Zmień pozycję pliku, głośność stacji albo inną zapisywaną właściwość, następnie
zamknij AMC zwykłym sposobem i uruchom ponownie. Ostatni stan powinien zostać
przywrócony mimo tego, że zapisy podczas pracy są wykonywane w tle.

Uwagi:

### AMC-181-04 — data harmonogramu

Otwórz `Shift+R`, przejdź Tabem do daty i sprawdź: lewo oraz prawo wybiera
dzień, miesiąc lub rok, a góra oraz dół zmienia wybraną część. NVDA powinien
podawać użytkową wartość daty, bez nazwy klasy kontrolki.

Uwagi:
