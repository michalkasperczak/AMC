# UPDATE-20261101 — wynik i ponowne sprawdzenie aktualizacji AMC

Stan: W trakcie. Data: 1.11.2026. Dotyczy bieżącej aplikacji wxPython AMC Test.

## Zgłoszenie i decyzja

Użytkownik uruchomił aktualizację, po czym ponownie wybrał „Sprawdź aktualizację AMC” i nie dostał czytelnego wyniku. Potwierdził zgodę na aktualizację po wyłączeniu nagrywania. Poprzedni wariant interfejsu zerował stan zajętości już po uruchomieniu pomocnika, nie znając wyniku instalacji. Decyzja z 1.11 zastępuje tę praktykę: stan zajętości trwa do końca pomocnika, a błąd otrzymuje komunikat NVDA. Jednoczesne wywołanie nie uruchamia drugiej próby.

## Materiał do dokumentacji użytkownika

Polecenie „Sprawdź aktualizację AMC” jest dostępne w menu Pomoc i palecie poleceń. Działa w obu profilach, tylko dla przypisanego kanału wydania. Podczas pobierania lub instalacji ponowne wywołanie informuje, że operacja już trwa. Jeśli program nagrywa albo trwa okno zaplanowanego nagrania, instalacja nie jest uruchamiana, a NVDA podaje przyczynę. Gdy aktualizator nie może zamknąć AMC, NVDA informuje o zatrzymaniu i można ponowić polecenie po zakończeniu nagrywania. Nie ma nowego skrótu.

## Uzasadnienie i decyzje

Pomocnik był startowany z katalogu instalacji i natychmiast tworzył kopię roboczą, przez co sam proces startowy kończył się wcześniej niż właściwa próba. Frontend nie mógł znać końcowego kodu. Teraz kopia pomocnika jest uruchamiana z unikalnego katalogu przygotowanego instalatora, poza katalogiem AMC, a frontend obserwuje kod zakończenia. Pomocnik nie pokazuje dodatkowego okna błędu; wynik przekazuje główne okno AMC przez NVDA. Przed uruchomieniem pobiera świeży status nagrywania i harmonogramu z hosta. Aktywne okno planu, nawet w przerwie między plikami, blokuje próbę od pięciu minut przed rozpoczęciem do dwóch minut po końcu. Stan nieznany również blokuje próbę. Nadal działa niezależna ochrona po stronie pomocnika przed podmianą plików działającego programu.

Przy ręcznej aktualizacji alpha.463 1.11 o 19:31 pierwsza próba z absolutną wartością `--launch` została odrzucona przed instalacją, bo pomocnik wymaga ścieżki względnej. Druga próba z `AMC.exe` zakończyła się kodem 0. Po restarcie harmonogram wznowił nagrywanie „Jarosław” do drugiego pliku. Oznacza to, że wcześniejszy krótki brak aktywnego zapisu nie gwarantował końca okna harmonogramu. Właśnie dlatego poprawka bada bieżący status, a nowe wydanie nie może być instalowane podczas tego nagrania. Dziennik wskazuje lukę w nagraniu podczas podmiany; nie twierdzimy, że zapis jest kompletny.

## Testy i wynik

Alpha.463 z GitHuba jest lokalnie zainstalowana: manifest wskazuje commit `ae8ae822c0a1c734c382aebe270bbb17459769e1`, wersję 0.1.0-alpha.463. Wszystkie 2141 plików z manifestu mają zgodne SHA-256; działają procesy `AMC.exe`, prywatny Python i host z katalogu AMC Test. Nowa poprawka: 78 testów aktualizacji, palety i skrótów zaliczonych w przypiętym Pythonie 3.14 / wxPython 4.3.1. Brak jeszcze builda, instalacji i wydania tej poprawki. Rzeczywiste komunikaty NVDA wymagają odbioru po bezpiecznej instalacji.

## Znane ograniczenia

Po pomyślnym zamknięciu AMC kod aktualizatora dociera do zamykanego procesu; nowa instancja sama potwierdza wersję. Gdy nagrywanie zakończy się między odczytem statusu a próbą zamknięcia, pomocnik nadal chroni działające pliki. Harmonogram zadań może wznowić nagrywanie po restarcie; przed każdą następną instalacją należy sprawdzić całe okno zaplanowanego nagrania. Nie wymuszać zamknięcia działającego AMC.

## Wersja i commit

Poprawka źródłowa po alpha.463 jest kandydatem AMC Test 0.1.0-alpha.464. Commit wydania do uzupełnienia po zapisaniu zmian. Opublikowana i zainstalowana alpha.463 pozostaje osobnym, już zweryfikowanym wydaniem.
