# Wyniki testów AMC 0.1.0-alpha.184

## Ustalenia z logu

- `Opole - 2026-06-07 21-37.mp3` było dostępne lokalnie; log nie wskazuje
  pobierania z iCloud.
- Przed pierwszą potwierdzoną ramką MP3 wykryto 195 nietypowych bajtów.
- Systemowy Media Foundation zwrócił `InvalidCastException` podczas ustawiania
  pozycji, po czym AMC uruchamiał dekoder zarządzany.
- Automatyczne otwarcie `Papa Dance 2` następowało po zgłoszeniu końca
  `Opole`, a nie po błędzie pobierania z chmury.

## Weryfikacja automatyczna

- Kompilacja Release: OK, 0 ostrzeżeń i 0 błędów.
- Testy rdzenia i Windows: OK.
- Harmonogramy: OK na pokazanym oknie; po przełączeniu Spacją pozostają ten
  sam obiekt, indeks listy oraz rzeczywisty fokus klawiatury jego wiersza.
- Przejścia: OK; wyciszenie rozpoczyna się cztery sekundy przed końcem i
  używa łagodnej krzywej zamiast zmiany liniowej.
- Wznawianie: OK; automatycznie wybrany następny plik otrzymuje zapamiętaną
  pozycję oraz własną głośność. Ukończony plik zeruje tylko własną pozycję.
- Nietypowy lokalny MP3: OK; plik z danymi przed pierwszą ramką od początku
  wybiera zarządzany dekoder z indeksem ramek. Przetestowano odczyt początku,
  środka i okolicy końca. Plik nadal chmurowy nie uruchamia pełnego indeksu.
- Dotychczasowe testy Radia, harmonogramów, nagrywania, Shazam, ustawień
  dźwięku, odtwarzania lokalnego i dekoderów: OK.
- Publikacja samowystarczalnego programu dla `win-x64`: OK.

## Pakiet

- Program: `publish\AccessibleMediaController-0.1.0-alpha.184\AccessibleMediaController-0.1.0-alpha.184.exe`.
- Wersja produktu: `0.1.0-alpha.184`.
- Rozmiar programu: `165962144` bajty.
- SHA-256: `CADF4BC4A2732090E9B97564219155F4BBBB0D5319F0884C6B79246C0623554F`.

## Test ręczny

### AMC-184-01 — harmonogram poza początkiem listy

Otwórz `Ctrl+Shift+H`, przejdź kilka wierszy w dół i naciśnij Spację kilka
razy.

Oczekiwane:

- fokus i przewinięcie pozostają na tym samym harmonogramie;
- NVDA podaje „pole wyboru zaznaczone, harmonogram włączony” albo „pole wyboru
  niezaznaczone, harmonogram wyłączony”;
- zaznaczenie nie wraca do pierwszego wiersza ani do samej listy.

Uwagi:

### AMC-184-02 — łagodniejsze przejście

Włącz przejścia przez `Shift+T` i odtwórz naturalny koniec pliku, po którym
jest następny element.

Oczekiwane:

- wyciszenie zaczyna być słyszalne około czterech sekund przed końcem;
- poziom opada płynnie, bez nagłego uskoku;
- następny plik równie płynnie wchodzi.

Uwagi:

### AMC-184-03 — zapamiętana pozycja następnego pliku

Odtwórz `Papa Dance 2`, przejdź w nim do łatwego do rozpoznania miejsca i
zmień plik na poprzedni. Następnie pozwól poprzedniemu plikowi zakończyć się
naturalnie i automatycznie przejść do `Papa Dance 2`.

Oczekiwane:

- jeżeli efektywne ustawienie pozycji brzmi „Pamiętaj”, `Papa Dance 2` wraca
  do zapamiętanego miejsca;
- nie rozpoczyna się od zera;
- ustawienie „Zawsze od początku” nadal świadomie rozpoczyna od zera.

Uwagi:

### AMC-184-04 — rzeczywisty plik Opole

Otwórz `Opole - 2026-06-07 21-37.mp3`, przejdź do środka, następnie w pobliże
końca i wykonaj pauzę oraz wznowienie.

Oczekiwane:

- AMC może przez chwilę przygotowywać indeks ramek, ale interfejs pozostaje
  dostępny;
- przewijanie i wznowienie nie uruchamiają awaryjnej zmiany dekodera;
- program nie przechodzi sam do `Papa Dance 2`, dopóki `Opole` rzeczywiście
  się nie zakończy.

Uwagi:
