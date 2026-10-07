# AMC 0.1.0-alpha.418 — grupy Sonosa po starcie i odświeżaniu

## Grupy głośników Sonos

Po uruchomieniu programu lista grup nie jest już pusta: pierwsze otwarcie okna
grup wczytuje je samo i od razu pokazuje aktualny stan.

Jedno otwarcie wystarczy także wtedy, gdy odczyt z Sonosa właśnie trwa w tle.
Wcześniej trzeba było zamknąć okno i otworzyć je ponownie, żeby zobaczyć grupy;
teraz okno uzupełnia się samo, bez drugiego naciśnięcia.

F5 na liście głównej i F5 w otwartym oknie celu odświeżają grupy tą samą drogą,
co polecenie z menu Plik. Jedno naciśnięcie to jedno odświeżenie — nie dwa
równoległe odczyty.

Odświeżanie nie gubi miejsca pracy: zaznaczenie i fokus zostają na tym samym
rzeczywistym wierszu, a gdy fokus był na przycisku, F5 go nie zabiera.
Zamknięcie okna przed zakończeniem odczytu nie wpuszcza już spóźnionego wyniku
do zamkniętego widoku.

## Klawisze w podlistach Ulubionych, Playlist i Moich stacji

Spacja wstrzymuje i wznawia odtwarzanie. Enter uruchamia dokładnie tę pozycję,
na której stoisz. Ctrl+Shift z cyfrą, zerem, minusem lub znakiem równości
uruchamia preset we wszystkich trzech podlistach, bez zmiany zaznaczonego
wiersza i bez przeskoku fokusu.

Spacja na przycisku działa jak zwykle w Windows — naciska przycisk, a nie
uruchamia transportu.

## Zakres sprawdzenia

Zmienioną obsługę sprawdzono w izolowanej kopii na Windows, z żywym NVDA:
czytnik potwierdził rzeczywisty wiersz celu, zachowanie fokusu wiersza
i przycisku po F5, widok Ulubionych po Ctrl+L oraz niezmieniony wiersz i fokus
po Spacji, Enterze i presecie.

Automatyczne testy mierzą dokładne żądania do Sonosa i stan odczytu: jedno
otwarcie okna w trakcie trwającego odczytu nie wysyła drugiego zapytania,
Spacja wysyła wyłącznie polecenie transportu, Enter wyłącznie uruchomienie
wybranego identyfikatora, a preset — materiał presetu, nie zaznaczonego wiersza.

Czego ta wersja NIE zawiera i czego nie zmierzono: nie ma nowych funkcji
edycji grup w Sonosie, nie badano słyszalności dźwięku na głośniku i nie
liczono żądań na żywym koncie — odsłuch pozostaje do ręcznej próby.
Nie zmieniano kont, nie budzono głośnika i nie zmieniano przepisu instalatora.
Poza zakresem tej wersji są też zmiany tempa odtwarzania, Apple Music
i osobny lekki interfejs.
