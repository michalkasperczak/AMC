# RARCH-004 — ulubione stacji i lokalne nazwy

Stan 22.11.2026: **etap 1, ulubione całych stacji, gotowy w kodzie**; etap 2, lokalne nazwy F2, pozostaje otwarty. Gałąź robocza `codex/rarch-004-station-favorites-20261122`, commit `218e25a8`; w kandydacie aktualizacji `codex/alpha512-update-candidate-20261122` commit integracyjny `563aa802`. Kod nie jest zainstalowany ani wydany.

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

## Etap 2 — otwarte

F2 ma pozwolić zapisać własną nazwę stacji lub nagrania lokalnie w AMC, bez zmiany katalogu serwera. Nazwa musi działać w wyszukiwaniu oraz zachować powiązania ze stabilnym identyfikatorem w zakładkach, Ulubionych, historii i kolejce. Fizyczna zmiana nazwy pobranego pliku pozostaje osobną decyzją. Po implementacji sprawdzić początkowy fokus, Enter/Escape, powrót fokusu i ponowne otwarcie dialogu z NVDA.

## Szkic instrukcji i zmian do odbioru

Na liście stacji Radio Archiwisty zaznacz całą stację i naciśnij `Ctrl+Shift+U`, aby dodać ją do Ulubionych. Tego samego skrótu użyj na nagraniu. Listę otworzysz przez `Ctrl+U`; Enter na stacji otwiera jej katalog. Ponowne `Ctrl+Shift+U` usuwa wybraną pozycję z Ulubionych. Ten tekst jest szkicem do korekty użytkownika przed publikacją instrukcji.
