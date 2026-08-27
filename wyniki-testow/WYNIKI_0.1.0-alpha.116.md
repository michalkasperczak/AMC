# Wyniki testów AMC 0.1.0-alpha.116

Na początku opisz zauważone zachowanie. Nie trzeba przed każdym zadaniem dopisywać osobnego wariantu „OK” lub „błąd”. Po dwukropku wpisuj spację.

## AMC-116-01 — Rozpoczęcie nagrywania MP3

Uruchom stację radia i naciśnij `Ctrl+Alt+R`. Sprawdź, czy komunikat podaje nazwę kończącą się na `.mp3`, a odtwarzanie nie zostaje przerwane.

## AMC-116-02 — Zatrzymanie i odtworzenie wyniku

Po co najmniej 20 sekundach ponownie naciśnij `Ctrl+Alt+R`. Otwórz zapisany plik z folderu `Muzyka\AMC — Nagrania radia` i sprawdź początek, środek oraz koniec nagrania.

## AMC-116-03 — Źródła o różnych kodekach

Jeśli są dostępne, wykonaj krótkie nagranie stacji źródłowej MP3 oraz AAC albo OGG. Każdy wynik powinien być zwykłym plikiem MP3, możliwym do odtworzenia poza AMC.

## AMC-116-04 — Zmiana stacji podczas nagrywania

Rozpocznij nagrywanie, a następnie uruchom inną stację. Pierwsze nagranie powinno zostać prawidłowo zakończone; nowa stacja nie może zostać dopisana do tego samego pliku.

## AMC-116-05 — Zamknięcie programu podczas nagrywania

Rozpocznij nagrywanie i zamknij AMC przez `Alt+F4`. Po ponownym uruchomieniu sprawdź, czy nagranie jest odtwarzalnym MP3 i nie pozostał plik z końcówką `.amc-partial`.

## AMC-116-06 — Komunikaty i dostępność

Sprawdź rozpoczęcie i zakończenie z widoku odtwarzacza, przyciskiem oraz z menu kontekstowego. NVDA powinien czytać wyłącznie nazwę i stan nagrywania, bez nazwy klasy, identyfikatora kodera lub pełnego rekordu technicznego.
