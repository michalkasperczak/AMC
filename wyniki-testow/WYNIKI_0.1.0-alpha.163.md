# Wyniki testów AMC 0.1.0-alpha.163

## Testy automatyczne i pakiet

- Kompilacja Release: OK, 0 ostrzeżeń, 0 błędów.
- Testy dymne Core: OK.
- Testy dymne Windows: OK.
- Publikacja samodzielnego pakietu win-x64: OK.
- Wersja produktu pliku EXE: `0.1.0-alpha.163`.
- Rozmiar pliku EXE: 165 865 888 bajtów.
- SHA-256 pliku EXE: `985F02541587A428BD3C7970E67CA8D7EC01BA4178C76959C23C4B3D3170AD80`.

Testy automatyczne obejmują dotychczasowy zestaw regresji oraz nowe kontrole:
trwałość historii rozpoznawania, zgodność lokalnego podpisu akustycznego z
wynikiem referencyjnym ShazamIO, pierwszeństwo zatrzymania nad równoczesnym
podziałem, lokalne przygotowanie nagrania i jego atomową publikację do folderu
docelowego. Test potwierdza też, że stary plik roboczy synchronizatora nie może
zostać pomylony z nowym nagraniem.

Poniższe zadania z `TESTY_ZADANIA_PL.md` wymagają odsłuchu NVDA, prawdziwej
stacji oraz wybranego folderu chmurowego i pozostają do sprawdzenia w
działającym programie.

Na początku opisz zauważone zachowanie. Nie trzeba przed każdym zadaniem
dopisywać osobnego wariantu „OK” lub „błąd”. Po dwukropku wpisuj spację.

## Uwagi

