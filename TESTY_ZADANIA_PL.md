# Zadania testowe AMC

- Numer zestawu: `AMC-TEST-032`
- Tytuł zestawu: Czytelna lista szablonów komunikatów
- Wersja programu: `0.1.0-alpha.32`
- Utworzono: 2026-08-17 00:05, Europe/Warsaw
- Plik wyników: `wyniki-testow/WYNIKI_2026-08-17_0005_0.1.0-alpha.32.md`

W pliku wyników po dwukropku wpisz krótko, co się wydarzyło. Jeżeli wszystko działa, wystarczy `OK`. Nie trzeba przed każdym zadaniem wybierać wariantu odpowiedzi.

## AMC-032-01 — Lista zdarzeń komunikatów

1. Otwórz Ustawienia i przejdź na kartę `Komunikaty`.
2. Ustaw fokus na liście `Zdarzenia komunikatów`.
3. Przejdź strzałkami po kilku pozycjach, w tym `Zmiana sesji`.

Oczekiwane:

- NVDA czyta wyłącznie przyjazną nazwę zdarzenia i pozycję na liście;
- dla pierwszej pozycji nie dopowiada `{slot}`, `{service}` ani całego tekstu szablonu.

## AMC-032-02 — Edycja szablonu i powrót z palety

1. Na liście zaznacz `Zmiana sesji` i przejdź Tabem do pola `Tekst szablonu wybranego komunikatu`.
2. Sprawdź, że pole nadal zawiera tekst `{slot}, {service}`; nie zmieniaj go albo anuluj ustawienia.
3. Zamknij Ustawienia, otwórz paletę `Ctrl+Shift+K`, znajdź `Ustawienia: szablony komunikatów` i wykonaj polecenie.

Oczekiwane:

- techniczne znaczniki pozostają dostępne tylko w polu edycji;
- paleta nadal otwiera właściwą kartę i ustawia fokus na czytelnej liście zdarzeń.
