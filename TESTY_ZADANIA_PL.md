# Zadania testowe AMC

- Numer zestawu: `AMC-TEST-024`
- Tytuł zestawu: Usługa po Escape bez zmiany sesji
- Wersja programu: `0.1.0-alpha.24`
- Utworzono: 2026-08-15 15:10:16, Europe/Warsaw
- Plik wyników: `wyniki-testow/WYNIKI_2026-08-15_151016_0.1.0-alpha.24.md`

Alpha.23 potwierdziła, że jedna wypowiedź fokusowa usuwa wzajemne przerywanie komunikatów. Ten pojedynczy test sprawdza wykryty przypadek: działanie bezpośrednie w wyszukiwaniu globalnym dotyczy usługi, która była aktywna już przed otwarciem wyszukiwania.

## AMC-024-01 — Ctrl+Enter i Escape w tej samej usłudze

1. Przełącz się do TIDAL skrótem `Ctrl+1`.
2. Naciśnij `Ctrl+Shift+F`, wyszukaj `Brzeg ciszy` i wybierz wynik TIDAL.
3. Naciśnij `Ctrl+Enter` i zapisz komunikat działania.
4. Naciśnij Escape i zapisz dokładnie całą wypowiedź NVDA po naciśnięciu Escape.
5. Przejdź strzałką na inny element.

Oczekiwane wyniki:

- `Ctrl+Enter` nie zamyka wyników i podaje „Odtwarzanie: Brzeg ciszy, TIDAL”;
- po naciśnięciu Escape NVDA czyta „Anna Kowalska, Brzeg ciszy”, pozostałe pola, nazwę „TIDAL” i pozycję jako jedną nieprzerwaną wypowiedź;
- nie pojawia się późniejszy osobny komunikat usługi;
- kolejny element ma zwykłą etykietę bez dopisanej nazwy usługi.

## Następne funkcje po tym zestawie

Po zatwierdzeniu poprawki następnym małym etapem będzie lokalna historia wyszukiwania: osobna dla każdej usługi i zakresu globalnego, do 20 unikatowych zapytań, wybierana strzałką w dół przy pustym polu. Następnie powstanie dostępna paleta poleceń pod `Ctrl+Shift+K`. Nadal pozostają niskopoziomowy konfigurowalny prefiks, instalator i bezpieczne aktualizacje, AMC.Host, WiiM oraz pierwsze logowanie OAuth.
