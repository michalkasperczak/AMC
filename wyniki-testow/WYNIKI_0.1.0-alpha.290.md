# AMC 0.1.0-alpha.290 — dzień tygodnia podczas ustawiania daty

1. W harmonogramie wybierz rozpoczęcie **Później** i przejdź do pola daty.

2. Na segmencie dnia użyj strzałek góra i dół. Oczekiwane: NVDA czyta datę w
   postaci „23.09, środa”, a nie samą liczbę dnia.

3. Strzałką w prawo wybierz miesiąc i zmieniaj go strzałkami góra i dół.
   Oczekiwane: NVDA czyta np. „4 września, piątek”.

4. Wybierz rok i zmień go strzałkami. Oczekiwane: komunikat zawiera pełną datę,
   rok i dzień tygodnia.

5. Przechodź strzałkami lewo i prawo między segmentami. Oczekiwane: zachowany
   jest wcześniejszy zwięzły odczyt nazwy segmentu; komunikaty nie nakładają się
   i nie są powielane.

Test automatyczny sprawdza polskie etykiety dla zmiany dnia, miesiąca i roku.
