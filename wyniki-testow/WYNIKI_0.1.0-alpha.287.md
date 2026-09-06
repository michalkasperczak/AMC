# AMC 0.1.0-alpha.287 — odczyt grupy multiroom WiiM

1. Włącz WiiM bez grupy, otwórz menedżer urządzeń `Ctrl+F5` i naciśnij F5 na
   wybranym urządzeniu. Oczekiwane: szczegóły zawierają sekcję Multiroom i rolę
   urządzenia samodzielnego. Odczyt nie zmienia źródła ani odtwarzania.

2. Zamknij menedżer, ustaw fokus na urządzeniu WiiM i naciśnij `Alt+Enter`.
   Oczekiwane: wśród właściwości znajduje się rola i nazwa grupy, bez nazw klas,
   identyfikatorów pól i surowej odpowiedzi urządzenia.

3. Jeżeli masz dwa urządzenia, utwórz grupę wyłącznie w WiiM Home, po czym
   odśwież oba w AMC. Oczekiwane: urządzenie główne pokazuje dostępnych członków,
   ich głośność i wyciszenie; podrzędne jest opisane jako urządzenie podrzędne.

4. Jeżeli masz obecnie tylko jeden WiiM, pomiń punkt 3. AMC celowo nie pozwala
   jeszcze tworzyć ani rozłączać grup bez wcześniejszych testów na prawdziwym
   zestawie co najmniej dwóch urządzeń.

5. Po całym teście sprawdź presety, Strumienie sieciowe, Spację, strzałki
   głośności i `Alt+Page Up/Down`. Oczekiwane: brak regresji, a opcjonalny odczyt
   multiroom nie opóźnia ani nie blokuje podstawowego sterowania.
