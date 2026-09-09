# Wyniki testów AMC 0.1.0-alpha.331

Na początku opisz zauważone zachowanie. Nie trzeba przy każdym punkcie wpisywać
wariantu „OK” albo „błąd”. Po dwukropku wpisuj spację.

## Uwagi

## Lista sesji Ctrl+Shift+S

- Przenieś środkową sesję `Alt+strzałka w dół`, potem w górę.
- Sprawdź, czy fokus pozostaje na przeniesionej sesji i czy komunikat podaje
  sąsiada oraz nowy skrót `Ctrl+cyfra`.
- Anuluj okno, otwórz je ponownie i sprawdź nową kolejność, `Ctrl+cyfra` oraz
  `Ctrl+Page Up/Page Down`.
- Sprawdź granice na pierwszej i ostatniej sesji oraz trwałość po restarcie.

## Weryfikacja automatyczna

- Pełna kompilacja Release: bez ostrzeżeń i błędów.
- Wszystkie testy rdzenia: zakończone powodzeniem.
- Wszystkie testy interfejsu Windows: zakończone powodzeniem.
- Zmiana zamienia właściwe sloty, zachowuje bieżącą sesję i natychmiast
  aktualizuje ustawienia.
- Okno zachowuje zaznaczenie, aktualizuje nazwę dostępnościową i nie ujawnia
  technicznej reprezentacji wiersza.
