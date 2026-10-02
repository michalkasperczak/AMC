# AMC 0.1.0-alpha.417 — powrót do podlisty Sonosa i import własnych stacji

## Sonos

Powrót z innej sesji wraca na TĘ SAMĄ podlistę i na TEN SAM wiersz, w którym byłeś — dotyczy Ulubionych Sonos, Playlist Sonos i Moich stacji. Zaznaczenie wracamy po identyfikatorze pozycji, nie po numerze wiersza, więc po zmianie listy nie trafisz na obcy materiał. Nieznana pozycja spada na pierwszy wiersz. Świadome wyjście Escape nie zostawia zadania powrotu.

Ctrl i cyfra przełącza sesję również z otwartej podlisty, bez ręcznego zamykania okna.

## Import stacji z playlisty do Moich stacji Sonosa

Menu Plik ma pozycję „Importuj stacje z playlisty do Moich stacji Sonosa”. W otwartym oknie Moich stacji to samo robi przycisk „Importuj z playlisty” oraz Ctrl+O — jedną drogą, bez drugiej kopii zapisu.

Import czyta M3U, M3U8, PLS, XSPF i JSON tą samą drogą, co import Radia. Jest LOKALNY: działa bez połączonego konta Sonos i bez wybranego głośnika, nic nie pobiera z sieci i niczego nie uruchamia. DOPISUJE stacje — anulowanie okna wyboru pliku, błąd pliku i manifest HLS nie zmieniają ani nie czyszczą stacji już zapisanych. Ulubione zapisane w samym Sonosie pozostają nietknięte.

Komunikat po imporcie podaje liczbę dodanych i PEŁNĄ sumę pominięć, z rozbiciem: już zapisane, adresy nieobsługiwane przez Sonosa, wpisy bez adresu lub powtórzone w pliku. Po udanym imporcie lista otwiera się na pierwszej dodanej stacji.

## Komunikaty

Krótka mowa w podlistach Ulubionych, Playlist i Moich stacji: nazwa pozycji bez technicznych dopisków przy zwykłym wyborze. Błędy nadal są zgłaszane w całości.

## Zakres sprawdzenia

Zmienioną obsługę sprawdzono w izolowanej kopii na Windows, z żywym NVDA: import z prawdziwym wyborem pliku z menu Plik, anulowanie, Ctrl+O i przycisk w Moich stacjach, nawigacja po trzech podlistach z granicami powrotu, presety i uruchomienie Ulubionego u rzeczywistego właściciela okna.

Nie zmieniano kont, nie budzono głośnika i nie zmieniano przepisu instalatora. Odsłuch własnych adresów pozostaje do ręcznej próby. Powtórzenie natywnych playlist Sonosa pozostaje odłożone.
