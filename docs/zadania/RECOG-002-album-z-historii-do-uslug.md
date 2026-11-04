# RECOG-002 — album z historii rozpoznawania w Spotify i TIDAL

Status: **W trakcie; odizolowane źródła na GitHubie, testy celowane gotowe, bez pakietu i instalacji**. Zgłoszenie: 3.11.2026.

## Zachowanie widoczne dla użytkownika

- `Ctrl+Alt+S`, prawa strzałka: na działaniu albumu `Ctrl+Shift+C` kopiuje dokładny link, jeśli usługa podała tożsamość albumu. Mówi tylko „Skopiowano link Spotify” albo „Skopiowano link TIDAL”. Gdy dokładnego wyniku nie ma, kopiuje link wyszukiwania albumu i wyraźnie nazywa go linkiem wyszukiwania. Nie wypowiada wcześniejszego komunikatu o sprawdzaniu łącza.
- Po otwarciu albumu w sesji Spotify albo TIDAL `Ctrl+Shift+L` zapisuje **cały album** w Bibliotece tego usługodawcy, nawet gdy kursor stoi na jednym z utworów. W Spotify wysyła jawne dodanie, bez przypadkowego przełączenia istniejącego członkostwa. W nazwanym widoku zapisanej kolekcji istniejące polecenie usuwania nadal działa na wybranym wierszu.
- `Backspace` z albumu wraca do poprzedniej listy Spotify albo TIDAL. Jeżeli usługa nie miała przed wejściem załadowanej listy, wczytuje jej główny widok. Przejście z historii nie uruchamia równoległego ładowania korzenia, które mogłoby nadpisać album.
- W Spotify playlisty przyjmują utwory i odcinki. Z listy albumu można zaznaczyć wszystkie jego utwory (`Ctrl+A` w natywnej liście Windows), a `Ctrl+Shift+P` otwiera wybór playlist. Album jako jeden obiekt trafia do Biblioteki, a do playlisty trafiają wybrane utwory.

## Uzasadnienie i decyzje

- Korekta użytkownika z 3.11.2026 uzupełnia RECOG-001: wcześniejsza poprawka `Ctrl+Shift+C` dotyczyła samej historii. Osobne okno działań otwierane prawą strzałką zachowało komunikat „Sprawdzam dokładne łącze” i nie miało użytecznego wyniku, gdy katalog nie potwierdził dokładnego dopasowania.
- Otwarty album wyświetla wiersze utworów. Dotychczasowe `Ctrl+Shift+L` i bramka menu patrzyły wyłącznie na zaznaczony wiersz, więc odrzucały cały album. Teraz korzystają ze wspólnej tożsamości albumu przechowywanej w kontekście widoku.
- Bezpośrednie wejście z historii do pustej sesji rozpoczynało dwa niezależne odczyty: korzenia i albumu. Późniejszy wynik korzenia mógł wyczyścić stos powrotu. Bezpośrednie przejście ładuje tylko album; `Backspace` w razie potrzeby ładuje korzeń.
- W bieżącym AMC nie ma zapisu do playlist konta TIDAL. Nie dodano pozornego polecenia ani lokalnej kopii playlisty. Powiązanie rozpoznania z biblioteką plików lokalnych pozostaje osobnym zadaniem.

## Materiał do dokumentacji użytkownika

> W historii rozpoznanych utworów naciśnij prawą strzałkę i wybierz „Spotify — pokaż album w AMC” albo „TIDAL — pokaż album w AMC”. W oknie działań `Ctrl+Shift+C` kopiuje link wybranego działania; jeśli dokładnego albumu nie uda się potwierdzić, usłyszysz, że skopiowano link wyszukiwania. Po wejściu do albumu naciśnij `Ctrl+Shift+L`, aby zapisać cały album w Bibliotece usługi. `Backspace` wraca do poprzedniej listy tej usługi. W Spotify możesz zaznaczyć utwory albumu i użyć `Ctrl+Shift+P`, aby dodać je do wybranej playlisty. Playlisty konta TIDAL nie są jeszcze obsługiwane w AMC.

## Testy i wynik

- Testy końcowe bez widocznego GUI: 258/258 (rozpoznawanie, Spotify i TIDAL, nawigacja, menu, paleta i skróty) oraz osobno 6/6 (okno playlist Spotify). Testy pokrywają dokładny link i wyszukiwanie zastępcze, zapis albumu z listy utworów, brak równoległego ładowania korzenia i powrót do niego z bezpośredniego wejścia. Wcześniejszy wspólny przebieg obu grup dał 262 zaliczone i 1 błąd samej atrapy wx w teście playlist (`object.__new__` na prawdziwym wx); osobny przebieg tej grupy przeszedł.
- Build, instalacja, publikacja i odbiór NVDA: **nie wykonano** w tym etapie. Żaden uruchomiony program nie został zamknięty.

## Znane ograniczenia

- Testy bez GUI nie potwierdzają jeszcze mowy NVDA, natywnego `Ctrl+A` ani kolejności fokusu po rzeczywistym przejściu między oknami. Przed odbiorem potrzebny jest test klawiaturą w AMC Test.
- Kopia linku wyszukiwania nie jest dokładnym łączem do albumu; komunikat celowo odróżnia te wyniki.
- Obsługa playlist TIDAL wymaga osobnego kontraktu hosta i konta.

## Wersja i commit

- Baza pracy: `0bad8716` na `codex/konfigurowalne-skroty-20261026` w kanonicznym repozytorium.
- Commit kodu, testów i opisu: `09580684800e62d48ab09ffe21c058f5ec54f067`.
- 3.11.2026 przed kompilacją: aktywna instalacja AMC Test działa z `C:\Users\micha\AppData\Local\Programs\AMC Test` i ma etykietę `2026.11.02-a415e5066d0b04078ee1146a83d16c94dd14fd7d-8e9b085e`. Starszy katalog `D:\Projekty Codex\AMC Python` nie jest aktywny.
- Identyfikator nowego pakietu i data instalacji: brak, ponieważ stan nagrań w aktywnej instancji nie został jeszcze potwierdzony.

## Stan aktualizacji — 4.11.2026

- Korekta użytkownika: odbiór playlist konta TIDAL może zostać osobnym etapem. Ten kandydat obejmuje rozpoznawanie, kopiowanie linku, zapis albumu do Biblioteki Spotify/TIDAL i nawigację; nie obejmuje późniejszych commitów zapisu do playlist TIDAL ani niezależnych zmian AirPlay.
- Bazą jest dokładny commit działającej instalacji `a415e5066d0b04078ee1146a83d16c94dd14fd7d`, etykieta `2026.11.02-a415e5066d0b04078ee1146a83d16c94dd14fd7d-8e9b085e`. W odizolowanym katalogu `D:\Projekty Codex\AMC build worktrees\amc-test-recognition-release-20261104` przeniesiono tylko poprawkę jako commity `6c187748` i `44836a7e`.
- Na tym kandydacie przeszło 216/216 celowanych testów Python (rozpoznawanie, nawigacja Spotify/TIDAL, menu i paleta) oraz osobno 6/6 testów okna playlist Spotify. Pierwsza próba miała 215 zaliczeń i błąd utworzenia katalogu tymczasowego pytest; po przygotowaniu katalogu zestaw przeszedł w całości.
- Gałąź `codex/amc-test-recognition-release-20261104` została wysłana do `michalkasperczak/AMC`. Odczyt zdalnego refa potwierdził dokładnie `44836a7e3d0dd5aa2cd7361cb47b9a1c6dadeaf3`. To publikacja źródeł gałęzi, bez GitHub Release i bez instalatora.
- Użytkownik potwierdził trwające nagranie. Nie zamykano AMC Test, nie rozpoczęto ciężkiego pakowania ani instalacji. Pakiet, prerelease na GitHubie, instalacja i odbiór NVDA pozostają do wykonania po zakończeniu nagrania i świeżej kontroli stanu.
