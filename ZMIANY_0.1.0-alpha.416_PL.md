# AMC 0.1.0-alpha.416 — porządek w sesji Sonos

## Sonos

Po powrocie z innej sesji wraca Biblioteka albo otwarty odtwarzacz, zamiast listy głośników. Biblioteka pokazuje Ulubione Sonos, Playlisty Sonos i Moje stacje. Wybrany cel sterowania pozostaje zachowany; głośniki i grupy wybiera się pod Ctrl+F5.

Poprawiono otwieranie Biblioteki i wyboru celu po zamknięciu własnych okien AMC. Nieznany lub obcy pierwszy plan nadal nie uprawnia programu do otwierania okien.

Spacja zatrzymuje również radio, które Sonos pozwala zatrzymać, ale nie zapauzować. Ponowne uruchomienie wraca do transmisji na żywo.

F2 edytuje własną stację zapisaną w AMC. Delete usuwa ją po potwierdzeniu. Nie zmienia to zasad edycji Ulubionych zapisanych w samym Sonosie.

Zwykła regulacja głośności, wyciszenie i uruchamianie stacji nie dodają technicznych wyjaśnień przy każdym poleceniu. Nazwa wybranego presetu jest ogłaszana od razu, bez czekania na odczyty z sieci i bez powtórzenia po nich. Preset nie zabiera fokusu z pola filtra. Błędy nadal są zgłaszane.

## Kopiowanie adresów

Ctrl+Shift+C kopiuje same adresy, bez tytułów. Przy kilku pozycjach każdy adres jest w osobnym wierszu. Dotyczy także wyników wyszukiwania i presetów. Ctrl+C nadal kopiuje nazwę lub opis; pliki lokalne zachowują kopiowanie pliku i pełnej ścieżki.

## Zakres sprawdzenia

Zmienioną obsługę sprawdzono w izolowanej kopii na Windows, z żywym NVDA i rzeczywistym schowkiem. Odczyt listy, zachowanie fokusu i wypowiedziane komunikaty sprawdzono osobno od testów poleceń.

Nie zmieniano kont ani nie budzono głośnika. Nie obiecujemy usunięcia wszystkich opóźnień całego AMC ani przyspieszenia samego urządzenia. Odsłuch własnych adresów pozostaje do ręcznej próby, zgodnie z ustaleniem. Powtórzenie natywnych playlist Sonosa pozostaje odłożone.
