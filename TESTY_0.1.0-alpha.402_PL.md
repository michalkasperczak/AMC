# AMC 0.1.0-alpha.402 — okresowy zapis pozycji

## Co się zmienia

Okresowe zapisy pozycji plików lokalnych, podcastów i Spotify nie kopiują już za każdym razem całych bibliotek na wątku interfejsu.

Zapis pozycji nadal działa co15sekund. Zmiana katalogu, ustawień i zamknięcie programu nadal korzystają z pełnego zapisu.

To poprawka jednego potwierdzonego kosztu. Nie jest stwierdzeniem usunięcia wszystkich zgłaszanych opóźnień Enter, Escape lub nawigacji.

## Próby użytkowe

1. Uruchom lokalny plik z włączonym zapamiętywaniem pozycji. Słuchaj co najmniej minutę, przejdź do innej sesji i wróć. Program ma zachować ten sam materiał i pozycję.
2. Wstrzymaj odtwarzanie, zamknij program i otwórz ponownie. Sprawdź tytuł, pozycję, głośność, wyciszenie i widok. Wznów materiał.
3. Powtórz próbę z podcastem i Spotify. Jeśli dla materiału wybrano „Zawsze od początku”, program nie powinien narzucać zapamiętanej pozycji.
4. W czasie słuchania zmień kolejkę albo usuń zbędną pozycję z kolekcji. Po ponownym starcie usunięty wpis nie może wrócić, a kolejka ma zachować zamierzoną kolejność.
5. Sprawdź zwykłą nawigację czytnikiem, przejście do odtwarzacza i powrót na listę. Okresowy zapis nie ma przestawiać fokusu.

## Kontrola automatyczna

Celowane testy obejmują trzy rzeczywiste handlery zapisu, pełny zapis i odczyt JSON/SQLite, zgodność danych, przeplatane checkpointy, usuwanie oraz błąd i końcowe opróżnienie kolejki.

Skrypt mutacyjny sprawdza, czy testy wykrywają powrót pełnego kopiowania, nałożenie nieaktualnego checkpointu, zgubienie wcześniejszego odcinka i pominięcie pozycji Spotify.

Pozostałe planowane zmiany nagrań, fragmentów audio i Apple Music nie należą do tej wersji.
