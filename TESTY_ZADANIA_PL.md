# Zadania testowe AMC

- Numer zestawu: `AMC-TEST-054`
- Tytuł zestawu: Tekstowe właściwości i menu kontekstowe odtwarzacza
- Wersja programu: `0.1.0-alpha.54`
- Utworzono: 2026-08-19 11:02, Europe/Warsaw
- Plik wyników: `wyniki-testow/WYNIKI_2026-08-19_1102_0.1.0-alpha.54.md`

W pliku wyników po dwukropku wpisz krótko, co się wydarzyło. Jeżeli wszystko działa, wystarczy `OK`. Nie trzeba przed każdym zadaniem wybierać wariantu odpowiedzi.

## AMC-054-01 — Właściwości jako zwykły tekst

1. Na lokalnym pliku naciśnij `Alt+Enter`.
2. Użyj strzałek w lewo i w prawo, `Ctrl+strzałka w lewo/prawo`, strzałek w górę i w dół, Home i End.
3. Zaznacz kilka znaków przez `Shift+strzałka`, słowo przez `Ctrl+Shift+strzałka` i kilka wierszy przez `Shift+strzałka w dół`.
4. Skopiuj każdy wybrany fragment przez `Ctrl+C` i wklej do edytora.
5. Sprawdź `Ctrl+A`, `Ctrl+C`, a potem zamknij okno Escape.

Oczekiwane:

- fokus trafia do wielowierszowego tekstu tylko do odczytu;
- NVDA pozwala poruszać się po znakach, słowach i wierszach, a zaznaczenie nie jest ograniczone do całych pozycji listy;
- standardowe kopiowanie zwraca dokładnie zaznaczony fragment;
- Escape zamyka okno i przywraca listę albo odtwarzacz.

## AMC-054-02 — Kopiuj wszystko bez zamykania

1. W oknie `Alt+Enter` przejdź Tabem do „Kopiuj wszystko” i naciśnij Enter.
2. Sprawdź, czy NVDA mówi „Skopiowano wszystkie informacje”.
3. Upewnij się, że okno nadal jest otwarte, i wklej schowek do edytora.
4. Wróć do tekstu, naciśnij sam Enter, a następnie Escape.

Oczekiwane:

- przycisk kopiuje pełny tekst wraz z podziałem na sekcje;
- Enter nie zamyka okna ani na przycisku kopiowania, ani w tekście;
- dopiero Escape lub przycisk „Zamknij” zamyka dialog.

## AMC-054-03 — Kolejność informacji i ścieżka

1. Otwórz właściwości lokalnego pliku.
2. Przeczytaj pierwsze wiersze od tytułu do czasu.
3. Powtórz w demonstracyjnej sesji TIDAL albo Apple Music.

Oczekiwane:

- dla pliku lokalnego pełna linia „Plik: litera dysku…nazwa” występuje bezpośrednio po „Usługa: Lokalne multimedia”, a nie dopiero na końcu;
- informacje techniczne nadal zawierają dostępny format, rozmiar, bitrate i częstotliwość;
- usługa nie pokazuje prywatnego ani tymczasowego adresu strumienia.

## AMC-054-04 — Menu kontekstowe odtwarzacza

1. Odtwórz lokalny plik i pozostań w odtwarzaczu.
2. Na przycisku odtwarzacza otwórz menu klawiszem Aplikacji albo `Shift+F10`.
3. Przejrzyj wszystkie pozycje i wykonaj po jednej: Ulubione, Biblioteka, Kolejka oraz kopiowanie pełnej ścieżki.
4. Ponownie otwieraj menu po każdej zmianie.
5. Uruchom „Właściwości i informacje”, zamknij je Escape i sprawdź fokus.

Oczekiwane:

- menu otwiera się z dowolnego przycisku odtwarzacza;
- zawiera odtwarzanie/pauzę, „Odtwórz jako następne”, Kolejkę, Ulubione, Bibliotekę, playlisty, informacje, oba rodzaje kopiowania i otwarcie oficjalnej aplikacji;
- etykiety mówią „Dodaj” albo „Usuń” zgodnie z bieżącym stanem;
- zamknięcie menu lub właściwości przywraca fokus do odtwarzacza.

## AMC-054-05 — Menu listy i krótka regresja historii

1. Na liście otwórz menu kontekstowe i sprawdź nową pozycję Biblioteki oraz kopiowanie nazwy i ścieżki lub łącza.
2. Zaznacz dwa elementy Shiftem i sprawdź, czy stanowe działania nadal obejmują oba.
3. Przejdź kolejno do Biblioteki, Ulubionych i Kolejki; użyj `Alt+strzałka w lewo`, a potem `Alt+strzałka w prawo`.

Oczekiwane:

- menu listy zachowuje działanie zbiorowe, a kopiowanie dotyczy bieżącego elementu;
- historia mówi kierunek i docelowy widok oraz nie uruchamia się wewnątrz menu;
- jeśli historia mimo tego pozostaje myląca, zapisz to w uwagach — funkcja nadal jest eksperymentalna.
