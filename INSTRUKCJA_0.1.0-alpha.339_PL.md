# AMC alpha 339 — próbny dodatek NVDA

Ta wersja dodaje lokalne połączenie z oddzielnym dodatkiem NVDA. Prefiks i dotychczasowe klawisze AMC zostają bez zmian. Nie ma zmian w logowaniu ani pełnym odtwarzaniu TIDAL.

Program: `D:\Projekty Codex\Accessible Multimedia Controller\publish\AccessibleMediaController-0.1.0-alpha.339\AccessibleMediaController-0.1.0-alpha.339.exe`

Dodatek: `D:\Projekty Codex\Accessible Multimedia Controller\AMC-NVDA-0.1.0.nvda-addon`

Po instalacji dodatku w dogodnym momencie wejdź do NVDA > Ustawienia > Zdarzenia wejścia > AMC. Polecenia są opisane po polsku. Nie mają jeszcze domyślnych skrótów: Ctrl+Windows ze strzałkami jest już zajęte przez Free Radio. Możesz przypisać własne kombinacje; nie dawaj tego samego skrótu obu dodatkom. Zmiana akordu korzysta ze standardowego okna NVDA, nie z mechanizmu przechwytywania prefiksu AMC.

Na początek przypisz Odczytaj bieżące nagranie oraz Odtwórz lub wstrzymaj. Otwórz nagranie w AMC, przejdź Alt+Tab do edytora i sprawdź oba polecenia. Następnie przetestuj głośność, odczyt czasu i poprzednią/następną sesję. Fokus ma zostać w edytorze. Testuj najpierw bez ważnego nagrywania.

Pauza i wyciszenie dotyczą odsłuchu aktywnej sesji, a nie rejestracji. Ten dodatek jeszcze nie ma poleceń nagrywania, kasowania, kolekcji ani przeglądania biblioteki. Następny/poprzedni zachowuje kontekst AMC. W WiiM jest to transport urządzenia, nie lista presetów.

W przypadku braku odpowiedzi polecenie nie jest automatycznie ponawiane. Odczytaj stan przed kolejną pauzą. Starszy uruchomiony AMC trzeba zastąpić 339; samo wgranie dodatku nie aktualizuje programu.

Nie instalowano dodatku w działającym NVDA ani nie przeprowadzono testów odsłuchowych na koncie użytkownika. Szczegóły i plan testów: `PROJEKT_NVDA_PL.md` oraz pomoc dołączona do dodatku.
