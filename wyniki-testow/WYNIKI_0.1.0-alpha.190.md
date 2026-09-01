# Wyniki testów AMC 0.1.0-alpha.190

## Co zmieniono

- Tymczasowy widok **Nagrywane** otwiera `Alt+R`.
- Escape i Backspace wracają z niego do dokładnego wcześniejszego widoku.
- `Alt+1` w Radiu zawsze otwiera Wszystkie stacje, a `Alt+2` jest wolne.
- Spacja pauzuje wyłącznie odsłuch. Nagrywanie i bufor nadal pracują.
- Wznowienie słuchanej i równocześnie nagrywanej stacji wraca na żywo.

## Wyniki weryfikacji automatycznej

- Kompilacja Release: OK, 0 ostrzeżeń i 0 błędów.
- Testy rdzenia: wszystkie OK.
- Testy Windows: wszystkie OK, w tym polityka `Alt+R`, powrotu Escape oraz
  wznowienia nagrywanej stacji na żywo.
- Publikacja samowystarczalnego pakietu: OK.
- Aplikacji nie uruchamiano automatycznie.

## Pakiet

- Program: `publish\AccessibleMediaController-0.1.0-alpha.190\AccessibleMediaController-0.1.0-alpha.190.exe`.
- Rozmiar programu: `165999520` bajtów.
- SHA-256: `8A63EAFD9B14257C83A0763696276885572E3CDE95CE8202705D668FE7385D8A`.

## Próby ręczne

### AMC-190-01 — Nagrywane z Ulubionych

Uwagi:

### AMC-190-02 — wolne Alt+2 w Radiu

Uwagi:

### AMC-190-03 — Spacja bez nagrywania

Uwagi:

### AMC-190-04 — Spacja podczas nagrywania słuchanej stacji

Uwagi:
