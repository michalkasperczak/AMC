# AMC 0.1.0-alpha.291 — zwięzły odczyt odtwarzacza WiiM

1. W odtwarzaczu WiiM zmień strumień przez `Alt+Page Up` lub `Alt+Page Down`.
   Oczekiwane: NVDA czyta np. „357. Odtwórz” albo „357. Wstrzymaj”.

2. Upewnij się, że automatyczny komunikat nie zawiera rodzaju odtwarzacza,
   pełnej nazwy urządzenia, „stanu nieznanego” ani wartości głośności.

3. Wycisz urządzenie i ponownie odczytaj przycisk. Oczekiwane: zwięzły
   komunikat zawiera słowo „wyciszone”.

4. Uruchom preset bezpośrednim skrótem. Oczekiwane: zachowane są numer presetu,
   jego nazwa oraz działanie przycisku, bez pełnego opisu urządzenia.

5. Sprawdź widoczne pola odtwarzacza i informację na żądanie. Oczekiwane: nadal
   można odczytać urządzenie, stan, głośność i pozostałe szczegóły.

Test automatyczny sprawdza wariant zwykłego strumienia, wyciszenia oraz presetu.
