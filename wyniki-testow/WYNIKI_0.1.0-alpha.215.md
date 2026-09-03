# Wyniki testów AMC 0.1.0-alpha.215

Na początku opisz zauważone zachowanie. Nie trzeba wpisywać osobnego wariantu
„OK” albo „błąd” przed każdym zadaniem. Po dwukropku wpisuj spację.

Uwagi ogólne:

## AMC-215-01 — kolejność komunikatu po Escape

W Radiu uruchom stację, pozostaw włączone wstrzymywanie przy wyjściu z
odtwarzacza i naciśnij `Escape`. NVDA powinien powiedzieć najpierw nazwę stacji
i stan, a na końcu „lista”, na przykład: „Radio 24, wstrzymany, lista”. Nie
powinien wcześniej wypowiadać osobnego słowa „lista”.

Wynik:

## AMC-215-02 — spójność innych sesji

Powtórz powrót `Escape` dla lokalnego pliku i odcinka podcastu. Kolejność ma być
ta sama: element, stan, lista. Fokus powinien pozostać na właściwej pozycji, a
strzałki od razu mają nawigować po liście.

Wynik:

## AMC-215-03 — brak ucieczki fokusu

Po powrocie `Escape` od razu użyj strzałek. W żadnym wariancie fokus nie może
przejść samoczynnie do pola „Filtruj listę”.

Wynik:
