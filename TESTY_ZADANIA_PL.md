# Zadania testowe AMC

- Numer zestawu: `AMC-TEST-053`
- Tytuł zestawu: Dostępne właściwości, kopiowanie i historia widoków
- Wersja programu: `0.1.0-alpha.53`
- Utworzono: 2026-08-18 22:10, Europe/Warsaw
- Plik wyników: `wyniki-testow/WYNIKI_2026-08-18_2210_0.1.0-alpha.53.md`

W pliku wyników po dwukropku wpisz krótko, co się wydarzyło. Jeżeli wszystko działa, wystarczy `OK`. Nie trzeba przed każdym zadaniem wybierać wariantu odpowiedzi.

## AMC-053-01 — Właściwości jako dostępna lista

1. Na lokalnym pliku naciśnij `Alt+Enter`.
2. Naciskaj strzałki w dół i w górę, Home, End oraz Page Up i Page Down.
3. Zamknij okno Enterem, otwórz ponownie i zamknij Escape.
4. Powtórz z poziomu odtwarzacza.

Oczekiwane:

- fokus trafia na zwykłą listę informacji;
- pierwszy wiersz zaczyna się od „Tytuł”, bez „Podstawowy element” i bez osobnego nagłówka „Podstawowe informacje”;
- każda strzałka przechodzi do osobnego, zrozumiałego wiersza;
- sekcje Odtwarzanie, Techniczne i Źródło występują jako osobne pozycje;
- zamknięcie przywraca właściwą listę albo odtwarzacz.

## AMC-053-02 — Kopiowanie z okna właściwości

1. W `Alt+Enter` zaznacz jeden wiersz i naciśnij `Ctrl+C`.
2. Użyj `Ctrl+A`, następnie `Ctrl+C`.
3. Otwórz okno ponownie i użyj przycisku „Kopiuj wszystko”.

Oczekiwane:

- pierwsze kopiowanie zwraca zaznaczony wiersz;
- `Ctrl+A`, `Ctrl+C` zwraca wszystkie niepuste wiersze;
- przycisk zwraca pełny tekst wraz z pustymi odstępami między sekcjami;
- żadne kopiowanie nie ujawnia tokenów ani tymczasowych adresów odtwarzania.

## AMC-053-03 — Ctrl+C oraz Ctrl+Shift+C

1. Na lokalnym pliku naciśnij `Ctrl+C` i wklej wynik do edytora.
2. Na tym samym pliku naciśnij `Ctrl+Shift+C` i wklej wynik.
3. Przejdź do demonstracyjnej sesji TIDAL lub Apple Music i powtórz oba skróty.

Oczekiwane:

- `Ctrl+C` zawsze kopiuje tylko nazwę zaznaczonego elementu;
- lokalne `Ctrl+Shift+C` kopiuje pełną ścieżkę z literą dysku i nazwą pliku;
- w demonstracyjnej usłudze `Ctrl+Shift+C` kopiuje bezpieczne łącze `demo://...`;
- przyszły prawdziwy adapter użyje w tym miejscu publicznego łącza usługi, a nie źródła strumienia.

## AMC-053-04 — Eksperymentalna historia widoków

1. Przejdź kolejno: Multimedia, Biblioteka, Ulubione, Kolejka.
2. Naciskaj `Alt+strzałka w lewo` trzy razy, a następnie `Alt+strzałka w prawo` trzy razy.
3. Powtórz raz z fokusem w filtrze i raz z fokusem na głównym przycisku.
4. Otwórz odtwarzacz, naciśnij `Alt+strzałka w lewo` i sprawdź powrót.

Oczekiwane:

- historia wraca dokładnie: Kolejka, Ulubione, Biblioteka, Multimedia, a potem idzie naprzód w odwrotnej kolejności;
- NVDA mówi „Wstecz” albo „Naprzód”, docelowy widok i jego zaznaczony element;
- skrót działa w całym głównym widoku, nie tylko na liście;
- `Alt+lewo` z odtwarzacza wraca do ostatniej listy;
- jeśli mimo tej jednoznaczności funkcja nadal jest myląca, wpisz to w uwagach — wtedy usuniemy ją zamiast dalej komplikować.
