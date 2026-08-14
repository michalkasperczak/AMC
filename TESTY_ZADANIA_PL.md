# Zadania testowe AMC

- Numer zestawu: `AMC-TEST-020`
- Tytuł zestawu: Krótsze wyniki, nazwa usługi i kontekst głównego okna
- Wersja programu: `0.1.0-alpha.20`
- Utworzono: 2026-08-14 21:35:18, Europe/Warsaw
- Plik wyników: `wyniki-testow/WYNIKI_2026-08-14_213518_0.1.0-alpha.20.md`

Ten zestaw sprawdza tylko zmiany po alpha.19. Najważniejsze są dokładne wypowiedzi NVDA i ich kolejność.

## AMC-020-01 — Wynik bez zbędnego prefiksu

1. W Ustawieniach wyłącz szczegółowe podpowiedzi klawiatury.
2. Przełącz się do TIDAL skrótem `Ctrl+1`.
3. Naciśnij `Ctrl+F`, wpisz `Ciepły deszcz` i naciśnij Enter.
4. Zapisz pierwszą pełną wypowiedź NVDA.
5. Wyszukaj `demonstracyjny` i przejdź strzałką na drugi wynik.

Oczekiwane wyniki:

- odczyt zaczyna się od wykonawcy albo tytułu, zgodnie z ustawioną kolejnością pól;
- nie występuje początkowe „Wyniki wyszukiwania”;
- pozycja natywnej listy, np. „1 z 1” albo „2 z 3”, nadal jest czytana dokładnie raz;
- zwykły Enter na wyniku nadal wraca do głównej listy i nie uruchamia odtwarzania.

## AMC-020-02 — Skrócona pomoc szczegółowa

1. Włącz opcję **Pokazuj szczegółowe podpowiedzi klawiatury przy polach i listach**.
2. Przez `Ctrl+F` wyszukaj `Ciepły deszcz`.
3. Zapisz pełną wypowiedź wyniku.
4. Wyłącz szczegółowe podpowiedzi i powtórz próbę.

Oczekiwane wyniki:

- w trybie szczegółowym po elemencie i pozycji występuje tylko: „Strzałki wybierają wynik. Enter otwiera. Escape zamyka okno”;
- nie są powtarzane skróty kolejki, następnego utworu, Ulubionych ani informacji;
- w trybie krótkim instrukcji nie ma.

## AMC-020-03 — Usługa w lokalnych działaniach bezpośrednich

1. Przełącz się do TIDAL skrótem `Ctrl+1`.
2. Przez `Ctrl+F` wyszukaj `Brzeg ciszy`.
3. Na wyniku naciśnij `Shift+Enter`, a następnie naciśnij go drugi raz, aby przywrócić stan Kolejki.
4. Na tym samym wyniku naciśnij dwukrotnie `Ctrl+Shift+U`, aby sprawdzić Ulubione i przywrócić stan.
5. Naciśnij `Alt+Enter` i sprawdź treść okna Informacje.
6. Za każdym razem zapisz komunikat oraz sprawdź, czy okno wyników pozostało otwarte.

Oczekiwane wyniki:

- każdy komunikat kończy się nazwą `TIDAL`, także w wyszukiwaniu bieżącej usługi;
- komunikat zawiera nazwę elementu;
- okno Informacje zawiera osobny wiersz „Usługa: TIDAL”;
- fokus pozostaje na wyniku i można od razu wykonać kolejne działanie.

## AMC-020-04 — Tytuł głównego okna i Kolejka

1. Zamknij wyszukiwanie, przejdź do „Ciepłego deszczu” na głównej liście i naciśnij `Ctrl+Enter`.
2. Odczytaj tytuł głównego okna poleceniem NVDA do odczytu tytułu.
3. Naciśnij `Ctrl+Q`, przejdź na „Nocny pociąg” i zapisz wypowiedź pozycji Kolejki.
4. Ponownie odczytaj tytuł głównego okna.

Oczekiwane wyniki:

- tytuł zaczyna się od „Ciepły deszcz — TIDAL” i dopiero potem podaje widok oraz AMC;
- po przejściu do Kolejki tytuł nadal zaczyna się od bieżącego utworu i usługi, a widok zmienia się na „Kolejka”;
- etykieta „Nocnego pociągu” zawiera `TIDAL` przed informacją o pozycji na liście.

## AMC-020-05 — Jednoznaczny powrót z wyszukiwania globalnego

1. Będąc w TIDAL-u, naciśnij `Ctrl+Shift+F` i wyszukaj `Zielony horyzont`.
2. Strzałką wybierz wynik Apple Music i otwórz go zwykłym Enter.
3. Zapisz kolejno wypowiedź elementu po odzyskaniu fokusu i późniejszy komunikat kontekstu.
4. Odczytaj tytuł głównego okna.
5. Powtórz wyszukiwanie globalne, wybierz wynik WiiM, użyj `Ctrl+Enter`, a następnie Escape.

Oczekiwane wyniki:

- zwykły Enter wraca na „Zielony horyzont” w głównej liście Apple Music;
- komunikat kontekstu podaje w tej kolejności: „Zielony horyzont, Apple Music, Teraz odtwarzane”;
- tytuł głównego okna zaczyna się od bieżącego utworu i usługi;
- `Ctrl+Enter` pozostawia wyniki otwarte i mówi „Odtwarzanie: Zielony horyzont, WiiM”;
- po Escape fokus wraca do właściwego elementu sesji WiiM.

## Następne funkcje po tym zestawie

Po zatwierdzeniu tych poprawek następny mały etap to lokalna historia wyszukiwania: osobna dla każdej usługi i dla zakresu globalnego, do 20 unikatowych zapytań, z wyborem strzałką w dół przy pustym polu. Następnie powstanie dostępna paleta poleceń pod `Ctrl+Shift+K`. Nadal pozostają: niskopoziomowy konfigurowalny prefiks i test kandydatów z NVDA, JAWS-em oraz menedżerami schowka, instalator i bezpieczne aktualizacje, AMC.Host, WiiM oraz pierwsze logowanie OAuth.
