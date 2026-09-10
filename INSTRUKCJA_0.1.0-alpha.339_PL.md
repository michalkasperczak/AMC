# AMC alpha 339 — próbny dodatek NVDA

Ta wersja dodaje lokalne połączenie z oddzielnym dodatkiem NVDA. Prefiks i dotychczasowe klawisze AMC zostają bez zmian. Nie ma zmian w logowaniu ani pełnym odtwarzaniu TIDAL.

Program: `D:\Projekty Codex\Accessible Multimedia Controller\publish\AccessibleMediaController-0.1.0-alpha.339\AccessibleMediaController-0.1.0-alpha.339.exe`

Dodatek: `D:\Projekty Codex\Accessible Multimedia Controller\AMC-NVDA-0.1.1.nvda-addon`

Aktualizacja dodatku 0.1.1 ma już zaakceptowany zestaw Ctrl+Windows. AMC pozostaje w wersji 339, nie trzeba aktualizować samego programu. Jeśli Free Radio jest nadal włączone w NVDA, wyłącz ten dodatek lub zmień jego kolidujące skróty. Własne przypisania możesz zmieniać w NVDA > Ustawienia > Zdarzenia wejścia > AMC. Dodatek nie nadpisuje wcześniejszych przypisań użytkownika. Zmiana akordu korzysta ze standardowego okna NVDA, nie z mechanizmu przechwytywania prefiksu AMC.

Na początek otwórz nagranie w AMC, przejdź Alt+Tab do edytora i sprawdź Ctrl+Windows+I (stan) oraz Ctrl+Windows+P (odtwórz/wstrzymaj). Następnie Ctrl+Windows+góra/dół (głośność), lewo/prawo (poprzedni/następny), J/K (cofnij/przewiń 10 sekund), E/R/T (czas od początku/pozostały/całkowity), M (wyciszenie) i PageUp/PageDown (sesje). Fokus ma zostać w edytorze. Testuj najpierw bez ważnego nagrywania. Ctrl+Windows+lewo/prawo przejmują systemowe przełączanie pulpitów podczas działania dodatku; Ctrl+Windows+Enter i cyfry nie są przypisane.

Pauza i wyciszenie dotyczą odsłuchu aktywnej sesji, a nie rejestracji. Ten dodatek jeszcze nie ma poleceń nagrywania, kasowania, kolekcji ani przeglądania biblioteki. Następny/poprzedni zachowuje kontekst AMC. W WiiM jest to transport urządzenia, nie lista presetów.

W przypadku braku odpowiedzi polecenie nie jest automatycznie ponawiane. Odczytaj stan przed kolejną pauzą. Starszy uruchomiony AMC trzeba zastąpić 339; samo wgranie dodatku nie aktualizuje programu.

Nie instalowano dodatku w działającym NVDA ani nie przeprowadzono testów odsłuchowych na koncie użytkownika. Szczegóły i plan testów: `PROJEKT_NVDA_PL.md` oraz pomoc dołączona do dodatku.
