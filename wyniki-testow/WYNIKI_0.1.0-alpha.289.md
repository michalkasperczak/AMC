# AMC 0.1.0-alpha.289 — Shift+R w widoku Nagrywane

1. Uruchom nagrywanie jednej stacji radiowej w tle i przejdź do widoku
   **Nagrywane** klawiszem `Alt+2`.

2. Ustaw fokus na nagrywanej stacji i naciśnij `Shift+R`. Oczekiwane: otwiera
   się edytor harmonogramu, a wskazana stacja jest wybrana początkowo.

3. Rozwiń listę stacji. Oczekiwane: znajdują się na niej także pozostałe stacje
   zapisane w Bibliotece Radia, a nie tylko stacje aktualnie nagrywane.

4. Sprawdź wynik wyszukiwania Radio Browser, którego nie dodano do Biblioteki.
   Oczekiwane: taki ukryty wynik nie pojawia się na liście harmonogramu.

5. Anuluj edytor klawiszem `Escape`. Oczekiwane: fokus wraca do tego samego
   nagrania w widoku **Nagrywane**.

Testy automatyczne: kompilacja Release bez ostrzeżeń i błędów; testy Core oraz
Windows zakończone powodzeniem. Nowy test sprawdza zakres listy, zachowanie
stacji początkowej i odrzucenie ukrytych wyników katalogu.
