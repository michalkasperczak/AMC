# AMC 0.1.0-alpha.338

- Odtworzono i poprawiono odrzucanie oczekującego wejścia TIDAL przez
  techniczną zmianę zaznaczenia przy odświeżeniu listy. Jeżeli po przebudowie
  nadal wybrany jest ten sam element, żądanie pozostaje aktualne.
- Ochrona pozostaje aktywna przy faktycznej zmianie elementu, sesji, widoku,
  filtra lub po Escape. Ręczne odejście i powrót nie ożywia starego żądania.
- Ujęto zagnieżdżone odświeżanie oraz przywracanie wielu zaznaczonych wierszy.
- Dodano diagnostykę rozpoczęcia, zakończenia i pominięcia otwarcia TIDAL:
  czas, liczba elementów i zgodność kontekstu, bez danych logowania.
- Nie zmieniono sposobu logowania ani uprawnień do odtwarzania.
  Potwierdzenie dokładnej przyczyny wcześniejszego przypadku Stevie Wondera
  pozostaje ograniczone przez brak szczegółów w starym logu.

Instrukcja i testy: `INSTRUKCJA_0.1.0-alpha.338_PL.md`.
