# Wyniki testów AMC 0.1.0-alpha.117

Na początku opisz zauważone zachowanie. Nie trzeba przed każdym zadaniem dopisywać osobnego wariantu „OK” lub „błąd”. Po dwukropku wpisuj spację.

## AMC-117-01 — Bezpośredni adres Polskiego Radia

W Radiu internetowym dodaj albo wyszukaj stację używającą adresu `http://stream3.polskieradio.pl:8080/;.mp3`. Uruchom ją Enterem. Stacja powinna zacząć grać bez komunikatu o nieudanym odtworzeniu.

## AMC-117-02 — Odtwarzacz i timeshift starszego strumienia

Po uruchomieniu tej stacji przejdź do odtwarzacza. Sprawdź głośność, pauzę, wznowienie, kilka przejść w lewo i w prawo oraz powrót na żywo klawiszem End. Fokus i komunikaty NVDA nie powinny różnić się od zwykłej stacji.

## AMC-117-03 — Nagrywanie MP3

Naciśnij `Ctrl+Alt+R`, odczekaj co najmniej 20 sekund i zatrzymaj nagrywanie tym samym skrótem. Gotowy plik MP3 powinien zawierać prawidłowy dźwięk, a nie metadane stacji ani zakłócenia w ich miejscach.

## AMC-117-04 — Inne anteny Polskiego Radia

Jeśli katalog zwraca je na starszych adresach, sprawdź co najmniej Polskie Radio 24 i Polskie Radio Dzieciom. Awaria jednej anteny nie może zablokować interfejsu ani uniemożliwić uruchomienia następnej.

## AMC-117-05 — Zwykła stacja bez regresji

Uruchom stację, która działała już w `alpha.116`, na przykład regionalną antenę Polskiego Radia lub inne radio z katalogu. Odtwarzanie, timeshift i nagrywanie powinny nadal działać standardowym torem.
