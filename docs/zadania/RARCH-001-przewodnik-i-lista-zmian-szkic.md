# Radio Archiwista — szkic przewodnika i listy zmian

Projekt do korekty po instalacji i próbie z NVDA. Niepublikowany.

## Przewodnik

W Menedżerze sesji (`Ctrl+Shift+S`) włącz Radio Archiwistę. W sesji wybierz stację, rok, miesiąc, dzień i nagranie. Enter rozpoczyna odtwarzanie. `Ctrl+L` wraca do pełnej listy stacji; lista nie ukrywa lokalnie żadnej stacji. `F5` odświeża bieżący poziom katalogu.

`F6` przełącza między listą i odtwarzaczem. `Spacja` wstrzymuje lub wznawia także podczas przeglądania listy. W odtwarzaczu cyfry `0–9` przewijają do procentu nagrania, a `Page Up/Down` przechodzi między nagraniami listy. Lewa strzałka na liście podaje dostępny czas, bez mylenia wielkości pliku z bitrate.

`Ctrl+Shift+U` dodaje lub usuwa nagranie z ulubionych; `Ctrl+U` pokazuje ulubione. `Ctrl+Shift+C` kopiuje bezpośredni link do nagrania. `Ctrl+D` pobiera nagranie do lokalnego folderu Radio Archiwisty. Ctrl+Shift+D otwiera formularz pobierania wycinka z początkiem i końcem; po zapisie AMC podaje pełną ścieżkę. W Podcastach/YouTube ten skrót pobiera wideo.

W odtwarzaczu `B` dodaje zakładkę, `Ctrl+Shift+B` zakładkę nazwaną, a `Shift+Page Up/Down` porusza się po zakładkach. Rozdziały użytkownika można dodać i przeglądać skrótami dostępnymi w pomocy klawiatury.

`Ctrl+Shift+Q` dodaje lub usuwa nagranie z lokalnej kolejki, `Ctrl+Shift+Enter` ustawia je jako następne, a `Ctrl+Q` pokazuje kolejkę. Enter na kolejce rozpoczyna odtwarzanie; po końcu nagrania program przechodzi do następnego wpisu. Kolejka, ulubione i zakładki są danymi AMC; nie zmieniają strony archiwum.

## Lista zmian do następnej aktualizacji AMC Test

- Naprawiono skróty Radio Archiwisty w widoku listy i odtwarzacza: pauzę, skoki procentowe, ulubione, kopiowanie linku, powrót do stacji i przechodzenie między nagraniami.
- Dodano zakładki i rozdziały użytkownika w odtwarzaczu nagrań.
- Dodano zapisywaną lokalnie kolejkę nagrań z poleceniem „odtwórz jako następne” i automatycznym przejściem do kolejnego nagrania.
- Informacja pod lewą strzałką nie przedstawia rozmiaru pliku jako bitrate. Lista sygnalizuje stan odtwarzania i pauzy.

## Do sprawdzenia przy odbiorze

Przejście całego drzewa katalogu, odtwarzanie i pauza przy aktywnej liście, cyfry, zakładki, rozdziały użytkownika, kopiowanie adresu, kolejka i automatyczne przejście, pobieranie, komunikaty NVDA, fokus po otwarciu i zamknięciu widoków oraz działanie przy niedostępnym serwerze. Zbiorczy widok zakładek (`Ctrl+B`) nie jest jeszcze częścią sesji.
