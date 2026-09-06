# Testy AMC 0.1.0-alpha.279

Po każdym zadaniu wpisz krótko wynik po dwukropku. Gdy wszystko działa,
wystarczy `OK`.

## AMC-279-01 — Eksport przy wyłączonym WiiM

Przejdź do sesji WiiM, otwórz `Ctrl+L` i naciśnij `Ctrl+Shift+O`. Zapisz listę
jako M3U. Urządzenie WiiM może być wyłączone; eksport nie powinien próbować się
z nim łączyć ani zmieniać fokusu po zakończeniu.

Wynik:

Uwagi:

## AMC-279-02 — Ponowny import

Zaimportuj zapisany plik przez `Ctrl+O`. AMC powinien poinformować, że wszystkie
strumienie są już zapisane, bez tworzenia duplikatów.

Wynik:

Uwagi:

## AMC-279-03 — Polskie znaki i kolejność

Otwórz wyeksportowany plik w edytorze. Nazwy z polskimi znakami i kolejność
strumieni powinny być zachowane, a każdy wpis powinien zawierać nazwę i adres.

Wynik:

Uwagi:

## AMC-279-04 — Niedostępne urządzenie

Przy wyłączonym WiiM spróbuj uruchomić zapisany strumień Enterem. Program
powinien podać krótki błąd połączenia, zachować listę i fokus oraz nadal
pozwalać na `F2`, kopiowanie i eksport.

Wynik:

Uwagi:
