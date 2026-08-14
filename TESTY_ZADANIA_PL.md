# Zadania testowe AMC

- Numer zestawu: `AMC-TEST-019`
- Tytuł zestawu: Zakres wyszukiwania, krótkie wyniki i kontekst sesji
- Wersja programu: `0.1.0-alpha.19`
- Utworzono: 2026-08-14 18:31:15, Europe/Warsaw
- Plik wyników: `wyniki-testow/WYNIKI_2026-08-14_183115_0.1.0-alpha.19.md`

Najważniejsze są zadania 1–5. Wpisuj możliwie dokładnie pierwszą wypowiedź NVDA — zwłaszcza jej kolejność.

## AMC-019-01 — Jednoznaczny tryb wyszukiwania

1. W TIDAL-u, na głównej liście, naciśnij `Ctrl+F`.
2. Zapisz pierwszą pełną wypowiedź NVDA, a potem zamknij okno Escape.
3. Naciśnij `Ctrl+Shift+F` i ponownie zapisz pierwszą wypowiedź.

Oczekiwane wyniki:

- bieżący zakres zawiera „Szukaj w usłudze TIDAL”;
- zakres globalny zawiera „Szukaj we wszystkich usługach”;
- nazwa trybu jest związana z polem „Wyszukiwany tekst”, więc nie ginie nawet wtedy, gdy NVDA nie odczyta osobno belki tytułu;
- nie jest podawany czas ani liczba elementów poprzedniego widoku.

## AMC-019-02 — Wynik bez powtórzonej liczby

1. Wyłącz szczegółowe podpowiedzi w **Ustawieniach**, na karcie **Komunikaty**.
2. Naciśnij `Ctrl+F`, wpisz `Ciepły deszcz` i naciśnij Enter.
3. Zapisz dokładnie komunikat NVDA.
4. Wróć do pola wyszukiwania, wpisz `demonstracyjny` i naciśnij Enter.

Oczekiwane wyniki:

- fokus przechodzi na pierwszy wynik;
- NVDA podaje etykietę elementu i pozycję natywnej listy, np. „1 z 1” albo „1 z N”;
- po tym nie występuje dodatkowy komunikat na żywo „1 wynik” ani „N wyników”;
- liczba wyników pozostaje widoczna dla użytkownika widzącego;
- drugi Enter na wyniku nadal go otwiera, zamiast ponownie uruchamiać zapytanie.

## AMC-019-03 — Kolejność szczegółowej podpowiedzi

1. Włącz opcję **Pokazuj szczegółowe podpowiedzi klawiatury przy polach i listach**.
2. Wyszukaj `Ciepły deszcz` przez `Ctrl+F`.
3. Zapisz kolejność: nazwa elementu, pozycja na liście i instrukcja klawiszowa.
4. Wyłącz opcję i powtórz próbę.

Oczekiwane wyniki:

- w trybie szczegółowym najpierw jest czytany wynik i jego pozycja, a dopiero potem pomoc o strzałkach, Enterze, Escape i działaniach bezpośrednich;
- pomoc jest własnością wyniku, a nie całej listy odczytaną przed elementem;
- w trybie krótkim instrukcja nie występuje;
- ważny komunikat o braku wyników pozostaje dostępny w obu trybach.

## AMC-019-04 — Kontekst po wyszukiwaniu globalnym

1. Będąc w TIDAL-u, naciśnij `Ctrl+Shift+F`, wyszukaj `Zielony horyzont` i wybierz wynik Apple Music.
2. Otwórz go zwykłym Enter.
3. Sprawdź fokus oraz komunikat po powrocie do głównego okna.
4. Powtórz wyszukiwanie, na wyniku Apple Music naciśnij `Ctrl+Enter`, a potem Escape.

Oczekiwane wyniki:

- zwykły Enter wraca do głównej listy na wybranym elemencie;
- po powrocie słychać „Apple Music, Teraz odtwarzane”;
- działanie `Ctrl+Enter` pozostawia wyniki otwarte i podaje usługę;
- Escape po działaniu wraca na właściwy element głównej listy i również jednoznacznie podaje zmienioną sesję;
- komunikat kontekstu następuje po przywróceniu fokusu, a nie przed odczytaniem elementu.

## AMC-019-05 — Jeden tor odtwarzania

1. Na „Brzegu ciszy” naciśnij zwykły Enter trzy razy.
2. Naciśnij na nim dwa razy `Ctrl+Enter`.
3. Przejdź na „Ciepły deszcz” i naciśnij `Ctrl+Enter`.

Oczekiwane wyniki:

- zwykły Enter mówi kolejno „Odtwarzanie”, „Pauza”, „Odtwarzanie”;
- każde `Ctrl+Enter` mówi „Odtwarzanie”, nigdy „Pauza”;
- wybranie „Ciepłego deszczu” zastępuje bieżący element tej sesji;
- aplikacja nie sugeruje równoległego nakładania utworów;
- fokus pozostaje na zaznaczonym elemencie.

## AMC-019-06 — Regresja Escape, Alt+F4 i braku wyników

1. Wyszukaj nieistniejący tekst i sprawdź komunikat oraz zaznaczenie pola.
2. Zamknij wyszukiwanie Escape.
3. Otwórz je ponownie i zamknij `Alt+F4`.
4. Sprawdź, czy w obu przypadkach wracasz na listę główną.

Oczekiwane wyniki:

- słychać „Brak wyników. Zmień wyszukiwany tekst”, a tekst jest zaznaczony do poprawy;
- Escape i `Alt+F4` zamykają tylko okno wyszukiwania;
- fokus wraca na listę główną;
- `Alt+F4` użyte dopiero w oknie głównym zamyka aplikację.

## Następne funkcje po tym zestawie

Następne małe funkcje to historia wyszukiwania oraz dostępna paleta poleceń pod `Ctrl+Shift+K`. Historia ma być lokalna, osobna dla usługi i zakresu globalnego, ograniczona do 20 unikatowych zapytań; strzałka w dół przy pustym polu wybierze historię, a Enter wykona zapytanie. Strzałki lewo i prawo do przeglądania metadanych zostaną dodane dopiero po rozszerzeniu wspólnego modelu pól. Nadal pozostają: niskopoziomowy konfigurowalny prefiks, instalator i bezpieczne aktualizacje, AMC.Host, WiiM oraz pierwsze logowanie OAuth.
