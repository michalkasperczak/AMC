# Zadania testowe AMC

- Numer zestawu: `AMC-TEST-023`
- Tytuł zestawu: Jedna nieprzerwana wypowiedź po powrocie z wyszukiwania
- Wersja programu: `0.1.0-alpha.23`
- Utworzono: 2026-08-15 01:09:54, Europe/Warsaw
- Plik wyników: `wyniki-testow/WYNIKI_2026-08-15_010954_0.1.0-alpha.23.md`

Alpha.22 potwierdziła, że osobny komunikat nazwy usługi przerywa odczyt elementu. Alpha.23 nie wysyła już drugiego komunikatu na żywo. Nazwa usługi jest chwilowo dopisana do dostępnościowej nazwy powracającego elementu, aby NVDA otrzymał jedno zdarzenie fokusu i jedną pełną wypowiedź.

## AMC-023-01 — Zwykły Enter: element i usługa bez przerwania

1. Przełącz się do TIDAL skrótem `Ctrl+1`.
2. Naciśnij `Ctrl+Shift+F`, wyszukaj `Zielony horyzont` i strzałką wybierz wynik Apple Music.
3. Otwórz wynik zwykłym Enter.
4. Zapisz dokładnie całą wypowiedź NVDA po powrocie.
5. Naciśnij strzałkę w górę, a następnie strzałkę w dół, aby wrócić do „Zielonego horyzontu”. Zapisz drugą wypowiedź tego elementu.

Oczekiwane wyniki:

- po powrocie NVDA czyta nazwę elementu, jego pola, rodzaj, nazwę usługi „Apple Music” i pozycję jako jedną nieprzerwaną wypowiedź;
- nie pojawia się późniejszy osobny komunikat „Usługa: Apple Music”;
- po przejściu w górę i z powrotem w dół element ma już zwykłą etykietę bez dodatkowego „Apple Music”, ponieważ kontekst powrotu był jednorazowy.

## AMC-023-02 — Ctrl+Enter, Escape i jedna wypowiedź po powrocie

1. Naciśnij `Ctrl+Shift+F`, wyszukaj `Nieznany brzeg` i wybierz wynik TIDAL.
2. Naciśnij `Ctrl+Enter`.
3. Potwierdź, że okno wyników pozostało otwarte i że komunikat działania kończy się nazwą „TIDAL”.
4. Naciśnij Escape i zapisz dokładnie całą wypowiedź po powrocie do głównej listy.
5. Przejdź strzałką na inny element.

Oczekiwane wyniki:

- `Ctrl+Enter` wykonuje działanie, nie zamyka wyników i podaje TIDAL;
- po Escape NVDA czyta „Nieznany brzeg” wraz z polami, nazwą „TIDAL” i pozycją jako jedną nieprzerwaną wypowiedź;
- nie występuje urwanie w rodzaju „niez… TIDAL” ani późniejszy osobny komunikat usługi;
- kolejny element ma zwykłą etykietę bez dopisanej nazwy usługi.

## Następne funkcje po tym zestawie

Po zatwierdzeniu oznajmiania następnym małym etapem będzie lokalna historia wyszukiwania: osobna dla każdej usługi i zakresu globalnego, do 20 unikatowych zapytań, wybierana strzałką w dół przy pustym polu. Następnie powstanie dostępna paleta poleceń pod `Ctrl+Shift+K`. Nadal pozostają niskopoziomowy konfigurowalny prefiks, instalator i bezpieczne aktualizacje, AMC.Host, WiiM oraz pierwsze logowanie OAuth.
