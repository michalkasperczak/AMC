# Zadania testowe AMC

- Numer zestawu: `AMC-TEST-022`
- Tytuł zestawu: Jednorazowy odczyt usługi i znaczenie tytułów okien
- Wersja programu: `0.1.0-alpha.22`
- Utworzono: 2026-08-14 23:38:14, Europe/Warsaw
- Plik wyników: `wyniki-testow/WYNIKI_2026-08-14_233814_0.1.0-alpha.22.md`

Alpha.21 potwierdziła pozostałe poprawki. Ten krótki zestaw sprawdza tylko kolejność powrotu z wyszukiwania globalnego i doprecyzowaną semantykę tytułów.

## AMC-022-01 — Zwykły Enter i usługa po elemencie

1. Przełącz się do TIDAL skrótem `Ctrl+1`.
2. Naciśnij `Ctrl+Shift+F`, wyszukaj `Zielony horyzont` i strzałką wybierz wynik Apple Music.
3. Otwórz wynik zwykłym Enter.
4. Zapisz dokładnie całą kolejność wypowiedzi NVDA po powrocie.
5. Odczytaj tytuł głównego okna.

Oczekiwane wyniki:

- fokus najpierw odczytuje zaznaczony element „Zielony horyzont” wraz z jego polami i pozycją;
- dopiero później słychać osobny, jednorazowy komunikat „Usługa: Apple Music”;
- komunikat nie powtarza tytułu elementu ani nazwy widoku;
- ponieważ zwykły Enter w wyszukiwaniu tylko otwiera wynik, tytuł głównego okna nadal wskazuje dotychczas odtwarzany element Apple Music, a nie „Zielony horyzont”.

## AMC-022-02 — Działanie bezpośrednie, Escape i usługa

1. Otwórz ponownie wyszukiwanie globalne i wyszukaj `Zielony horyzont`.
2. Wybierz wynik WiiM i naciśnij `Ctrl+Enter`.
3. Potwierdź, że wyniki pozostały otwarte i że komunikat działania zawiera WiiM.
4. Naciśnij Escape i zapisz całą kolejność wypowiedzi po powrocie.
5. Odczytaj tytuł głównego okna.

Oczekiwane wyniki:

- `Ctrl+Enter` mówi „Odtwarzanie: Zielony horyzont, WiiM” i nie zamyka wyników;
- po Escape najpierw czytany jest element głównej listy, a następnie „Usługa: WiiM”;
- nazwa usługi nie zostaje przykryta kolejnym odczytem fokusu;
- tytuł głównego okna zaczyna się od „Zielony horyzont — WiiM”, ponieważ tym razem utwór został uruchomiony.

## AMC-022-03 — Tytuł wyszukiwania i tytuł odtwarzania

1. W głównym oknie WiiM uruchom `Ctrl+F` i odczytaj tytuł okna.
2. Zamknij wyszukiwanie Escape i ponownie odczytaj tytuł głównego okna.
3. Przejdź do Kolejki skrótem `Ctrl+Q` i jeszcze raz odczytaj tytuł.

Oczekiwane wyniki:

- w wyszukiwaniu tytuł brzmi „Szukaj w usłudze WiiM — AMC”, ponieważ opisuje zakres aktualnego okna;
- po zamknięciu tytuł główny ponownie zaczyna się od aktualnie odtwarzanego elementu i usługi;
- przejście do Kolejki zmienia tylko człon widoku, nie nazwę aktualnie odtwarzanego elementu.

## Następne funkcje po tym zestawie

Po zatwierdzeniu kolejności powrotu następnym małym etapem będzie lokalna historia wyszukiwania: osobna dla każdej usługi i zakresu globalnego, do 20 unikatowych zapytań, wybierana strzałką w dół przy pustym polu. Następnie powstanie dostępna paleta poleceń pod `Ctrl+Shift+K`. Nadal pozostają niskopoziomowy konfigurowalny prefiks, instalator i bezpieczne aktualizacje, AMC.Host, WiiM oraz pierwsze logowanie OAuth.
