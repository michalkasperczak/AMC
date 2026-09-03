# Wyniki testów AMC 0.1.0-alpha.214

Na początku opisz zauważone zachowanie. Nie trzeba wpisywać osobnego wariantu
„OK” albo „błąd” przed każdym zadaniem. Po dwukropku wpisuj spację.

Uwagi ogólne:

## AMC-214-01 — nowy odcinek i próg jednej minuty

W skrzynce `Ctrl+I` uruchom naprawdę nowy odcinek. Przed upływem minuty wróć do
listy i sprawdź, czy nadal jest oznaczony jako nowy. Następnie odtwórz lub
przewiń go co najmniej do `1:00`. Odcinek nie powinien już pozostawać w Nowych.

Wynik:

## AMC-214-02 — osobna lista W trakcie słuchania

W Podcastach naciśnij `Ctrl+Shift+I`. Rozpoczęty, niedokończony odcinek powinien
być na liście **W trakcie słuchania**. Enter ma wznowić go z zapamiętanego
miejsca. Odcinki ukończone oraz stare archiwum nigdy nierozpoczęte nie powinny
znaleźć się na tej liście.

Wynik:

## AMC-214-03 — zakończenie odcinka

Dokończ rozpoczęty odcinek. Sprawdź, czy znika z W trakcie słuchania, pozostaje
w Bibliotece audycji i ma w `Alt+Enter` stan odsłuchania **odtworzony**.

Wynik:

## AMC-214-04 — szybka informacja podcastu

Na zdalnym i na pobranym odcinku naciśnij strzałkę w lewo. AMC powinien podać
kodek, bitrate i rozmiar, jeśli kanał albo plik udostępnia te dane. Brak danych
ma być pominięty, bez wymyślonej wartości i bez komunikatu technicznego.

Wynik:

## AMC-214-05 — fokus i dostępne nazwy nowych widoków

Otwórz Nowe odcinki oraz W trakcie słuchania z menu Widok, skrótami i paletą
poleceń. Pierwszy element albo informacja o pustej liście powinny być czytane
jednoznacznie. Escape na najwyższym poziomie ma pozostawić fokus na liście.

Wynik:
