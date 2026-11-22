# RARCH-004 — ulubione stacji i lokalne nazwy

Stan 22.11.2026: **etap 1, ulubione całych stacji, i etap 2, lokalne nazwy F2, gotowe w kodzie**. Gałąź robocza `codex/rarch-004-station-favorites-20261122`: commity `218e25a8` i `818520da`; w kandydacie aktualizacji `codex/alpha512-update-candidate-20261122`: commity integracyjne `563aa802` i `25fe2834`. Kod nie jest zainstalowany ani wydany.

## Etap 1 — zachowanie

W sesji Radio Archiwista `Ctrl+Shift+U` przełącza ulubione dla zaznaczonej całej stacji albo pojedynczego nagrania. Foldery roku, miesiąca i dnia nie są celem. `Ctrl+U` pokazuje stacje i nagrania na jednej liście, ostatnio dodane na początku. Enter na ulubionej stacji otwiera jej katalog lat; Enter na ulubionym nagraniu zachowuje dotychczasowe odtwarzanie. Na liście wszystkich stacji ulubiona jest oznaczona gwiazdką bez zmiany jej właściwej nazwy i licznika nagrań.

Menu, paleta i pomoc podają oba rodzaje celu. W menu pozycja brzmi „Zmień stan ulubionych stacji lub nagrania”, skrót `Ctrl+Shift+U`; polecenie jest dostępne tylko dla całej stacji albo nagrania z adresem audio. W angielskim interfejsie nazwa obejmuje „station or recording”. Przy dodaniu i usunięciu NVDA otrzymuje „Dodano do ulubionych: [nazwa]” albo „Usunięto z ulubionych: [nazwa]”; przy innym folderze „Zaznacz stację lub nagranie, aby zmienić ulubione”. Po usunięciu z widoku Ulubionych pozycja znika z listy.

Zapis używa prywatnego `favorites.json` Radio Archiwisty. Nowy rekord stacji ma jawny rodzaj `station` i stabilny identyfikator `rarch:station:{slug}`; stare rekordy nagrań bez tego pola nadal są odczytywane. Kod nie pobiera katalogu sieciowego podczas otwierania Ulubionych, nie miesza stacji z nagraniami przy odtwarzaniu ani nie zmienia serwera. Nie potrzeba migracji istniejących ulubionych nagrań.

## Zmiany i weryfikacja

Źródła: `wxlite/amc_wx_lite/radioarchiwista_source.py`, `gui.py`, `list_model.py`, `menu_model.py`, `shortcuts.py`, `i18n.py`; test: `wxlite/tests/test_radioarchiwista_station_favorites.py`. Jeden warunek celu polecenia w `menu_model.py` jest używany przez menu i wykonanie skrótu.

- Nowe cztery scenariusze (zapis stacji obok starego nagrania, oznaczenie wiersza, skrót i usunięcie, zgodność menu/palety/pomocy PL/EN): **4 passed**.
- Szerszy zestaw w osobnej gałęzi: **184 passed, 3 failed**. Niepowodzenia dotyczyły wcześniejszej atrapy `wx.GetTopLevelWindows` oraz dwóch brakujących klawiszy dostępu w menu pobranych Plików; nie były następstwem tej zmiany. Te dwa błędy menu są już poprawione przez niezależną poprawkę presetów w kandydacie.
- Po integracji z kandydatem: `PYTHONPATH=wxlite python -m pytest` dla nagrywania, źródła i sesji Radio Archiwisty, nowego testu, Delete kolejki, presetów, menu, palety i skrótów: **242 passed**.
- Nie wykonano widocznego GUI, testu NVDA, instalacji, builda nowego HEAD ani publikacji. Testy bez okna nie dowodzą faktycznego odczytu przez NVDA.

## Etap 2 — lokalne nazwy F2

W sesji Radio Archiwista F2 na całej stacji albo nagraniu otwiera dialog „Zmień nazwę stacji lub nagrania w AMC”. Fokus pada na pole „Nowa nazwa w AMC” z dotychczasową nazwą zaznaczoną do podmiany. Enter zapisuje, Escape anuluje. Puste pole przywraca nazwę katalogową. Po zamknięciu wraca fokus do listy i pozostaje wybrany ten sam identyfikator pozycji. Nazwa działa na liście stacji, w widokach nagrań, w Ulubionych, kolejce i wyszukiwaniu AMC; źródłowy katalog, plik pobrany na dysk i adres audio nie zmieniają się. Foldery roku, miesiąca i dnia nie oferują F2. Menu, skrót, paleta i pomoc mają wspólny warunek celu; etykiety i komunikaty są po polsku i angielsku. Angielska nazwa polecenia brzmi „Rename station or recording in AMC”.

Alias jest przechowywany w prywatnym `aliases.json` według trwałego identyfikatora `rarch:station:...` lub `rarch:rec:...`; nie nadpisuje `favorites.json` ani `queue.json`. Te same identyfikatory pozostają w zakładkach, Ulubionych, historii i kolejce, więc nazwa nie zrywa ich powiązań. Testy sprawdziły faktycznie wyświetlaną nazwę w wyszukiwaniu i kolejce, zachowanie ID oraz przywrócenie katalogowej nazwy bez ponownego pobierania. Nie wykonano widocznej próby zakładek, historii i NVDA; to pozostaje do odbioru. Fizyczne przemianowanie wcześniej pobranego pliku nie weszło do tej poprawki.

`test_radioarchiwista_aliases.py` sprawdza zapis i ponowne odczytanie, zmianę i usunięcie aliasu, brak zmiany identyfikatorów, wyszukiwanie i kolejkę, dialog Enter/Escape, powrót fokusu, ponowne otwarcie, skrót F2, menu, paletę i pomoc w PL/EN. Zestaw aliasów, ulubionych stacji, źródła, sesji, wyszukiwania, Delete kolejki, palety i skrótów w gałęzi roboczej: **130 passed**. Po integracji zestaw nagrywania, YouTube, Radio Archiwisty, presetów, menu, palety i skrótów: **267 passed, 2 subtests passed**. Sprawdzono brak kolizji klawiszy dostępu w menu Radio Archiwisty. To testy bez widocznego GUI; ręczna próba NVDA, instalacja, pełny zestaw i publikacja nie zostały wykonane po commicie `25fe2834`.

## Szkic instrukcji i zmian do odbioru

Na liście stacji Radio Archiwisty zaznacz całą stację i naciśnij `Ctrl+Shift+U`, aby dodać ją do Ulubionych. Tego samego skrótu użyj na nagraniu. Listę otworzysz przez `Ctrl+U`; Enter na stacji otwiera jej katalog. Ponowne `Ctrl+Shift+U` usuwa wybraną pozycję z Ulubionych. Aby nadać stacji lub nagraniu własną nazwę, zaznacz je i naciśnij F2. Wpisz nazwę i potwierdź Enterem. Wyczyszczenie pola przywróci nazwę katalogową. Ten tekst jest szkicem do korekty użytkownika przed publikacją instrukcji.
