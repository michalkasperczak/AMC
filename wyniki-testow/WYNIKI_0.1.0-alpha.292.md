# AMC 0.1.0-alpha.292 — minimalny odczyt WiiM

1. Zmień strumień WiiM przez `Alt+Page Up` lub `Alt+Page Down`. Oczekiwane:
   NVDA podaje tylko nazwę strumienia, np. „357”.

2. Uruchom preset skrótem lub z listy. Oczekiwane: NVDA podaje tylko nazwę,
   np. „Lublin”, bez numeru presetu i bez słowa „Odtwórz” lub „Wstrzymaj”.

3. Wycisz urządzenie i ponów odczyt. Oczekiwane: program dodaje wyłącznie
   istotny stan, np. „Lublin, wyciszone”.

4. Odczytaj pełny odtwarzacz na żądanie. Oczekiwane: pozostałe szczegóły nadal
   są dostępne, ale nie są automatycznie wypowiadane przy każdym przełączeniu.

Test automatyczny sprawdza zwykły strumień, preset oraz wyciszenie.
