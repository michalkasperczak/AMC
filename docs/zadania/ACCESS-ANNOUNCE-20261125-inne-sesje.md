# Komunikaty o zmianie utworu z innej sesji

Stan: wymaganie zapisane 25.11.2026; bez zmiany kodu, testu, kompilacji i wydania.

## Zgłoszenie

Gdy w TIDAL zaczyna się kolejny utwór, AMC mówi jego tytuł przez NVDA także wtedy, gdy użytkownik pracuje w lokalnej Kolejce albo innej sesji. Informacja bywa przydatna, lecz może przerywać czytanie i przenoszenie plików. Użytkownik chce móc decydować o tych automatycznych komunikatach. Nie chodzi o wyłączenie informacji na żądanie ani o zmianę tego, czy Enter otwiera odtwarzacz.

## Projekt zachowania

- W **Ustawienia programu → Dostępność** dodać jasną preferencję zapowiedzi zmiany odtwarzanego materiału, zwłaszcza gdy gra inna sesja niż aktualnie przeglądana. Rozważyć wybór: „zawsze”, „tylko w aktywnej sesji”, „nigdy automatycznie”; zachować odczyt na żądanie i komunikaty błędów.
- Zmiana utworu nie może przejmować fokusu ani przenosić kursora na odtwarzany wiersz w innej sesji. W aktywnej liście oznaczenie pozycji odtwarzanej i mowa NVDA powinny mieć spójną politykę, bez podwójnego odczytu.
- Sprawdzić Radio, TIDAL, Spotify, Podcasty, Pliki, Radio Archiwistę oraz odbiorniki. Zdarzenie `radio.nowPlaying` i automatyczna zmiana TIDAL mają dziś odrębne ścieżki mowy; jedna preferencja musi być stosowana w punktach wysyłających komunikaty, nie w samym ogólnym wyłączniku wszystkich komunikatów dostępności.
- W ustawieniach sesji TIDAL istnieje już osobny wybór „Po uruchomieniu materiału Enterem: Otwórz odtwarzacz / Pozostań na liście”. `Alt+Shift+Enter` w obecnych skrótach dotyczy opcji odtwarzania Podcastów, nie ustawienia TIDAL. Te dwa zachowania trzeba jasno rozdzielić w pomocy.

## Uzasadnienie i decyzje

Domyślną wartość dla istniejących profili i dokładny podział automatycznych komunikatów trzeba ustalić przy implementacji na podstawie testów sesji. Zachować obecne ustawienia użytkownika i nie wyłączyć globalnie ważnych powiadomień o nagrywaniu lub błędach.

## Testy i wynik

Wykonano odczyt źródeł: `gui.py` wysyła tytuł bezpośrednio przy `radio.nowPlaying`, a przy automatycznym starcie kolejnego utworu TIDAL również wywołuje mówienie tytułu. Nie wykonano testu NVDA ani zmian programu. Test wdrożenia: odtwarzać TIDAL, pracować w innej sesji, zmieniać utwory przy każdej wartości opcji, sprawdzić fokus, mówienie, pasek stanu, pomoc i ponowne uruchomienie.

## Materiał do dokumentacji użytkownika

Szkic przyszłej instrukcji: „W Ustawieniach programu, na karcie Dostępność, wybierz, czy AMC ma automatycznie mówić tytuł nowego utworu, gdy pracujesz w innej sesji. Informację o bieżącym odtwarzaniu nadal możesz wywołać ręcznie”. Ta opcja nie jest jeszcze dostępna.

## Znane ograniczenia, wersja i commit

Brak implementacji i commitu funkcji. Zgłoszenie powstało po AMC Test alpha.528. Następny krok: tabela zdarzeń i istniejących komunikatów dla każdej sesji, potem jedna polityka preferencji i testy parytetu dostępności.
