# RECOG-002 — album z historii rozpoznawania w Spotify i TIDAL

Status: **W trakcie; kod, testy celowane i kompilacja hosta gotowe, bez instalacji**. Zgłoszenie: 3.11.2026.

## Zachowanie widoczne dla użytkownika

- `Ctrl+Alt+S`, prawa strzałka: na działaniu albumu `Ctrl+Shift+C` kopiuje dokładny link, jeśli usługa podała tożsamość albumu. Mówi tylko „Skopiowano link Spotify” albo „Skopiowano link TIDAL”. Gdy dokładnego wyniku nie ma, kopiuje link wyszukiwania albumu i wyraźnie nazywa go linkiem wyszukiwania. Nie wypowiada wcześniejszego komunikatu o sprawdzaniu łącza.
- Po otwarciu albumu w sesji Spotify albo TIDAL `Ctrl+Shift+L` zapisuje **cały album** w Bibliotece tego usługodawcy, nawet gdy kursor stoi na jednym z utworów. W Spotify wysyła jawne dodanie, bez przypadkowego przełączenia istniejącego członkostwa. W nazwanym widoku zapisanej kolekcji istniejące polecenie usuwania nadal działa na wybranym wierszu.
- `Backspace` z albumu wraca do poprzedniej listy Spotify albo TIDAL. Jeżeli usługa nie miała przed wejściem załadowanej listy, wczytuje jej główny widok. Przejście z historii nie uruchamia równoległego ładowania korzenia, które mogłoby nadpisać album.
- W Spotify playlisty przyjmują utwory i odcinki. Z listy albumu można zaznaczyć wszystkie jego utwory (`Ctrl+A` w natywnej liście Windows), a `Ctrl+Shift+P` otwiera wybór playlist. Album jako jeden obiekt trafia do Biblioteki, a do playlisty trafiają wybrane utwory.
- W TIDAL `Ctrl+Shift+P` otwiera dostępny z klawiatury wybór playlist konta. Spacja zaznacza wiele playlist; można utworzyć nową. Z otwartego albumu polecenie dodaje wszystkie dostępne utwory albumu w kolejności usługi. Po zapisie dialog znika, a fokus wraca do listy.

## Uzasadnienie i decyzje

- Korekta użytkownika z 3.11.2026 uzupełnia RECOG-001: wcześniejsza poprawka `Ctrl+Shift+C` dotyczyła samej historii. Osobne okno działań otwierane prawą strzałką zachowało komunikat „Sprawdzam dokładne łącze” i nie miało użytecznego wyniku, gdy katalog nie potwierdził dokładnego dopasowania.
- Otwarty album wyświetla wiersze utworów. Dotychczasowe `Ctrl+Shift+L` i bramka menu patrzyły wyłącznie na zaznaczony wiersz, więc odrzucały cały album. Teraz korzystają ze wspólnej tożsamości albumu przechowywanej w kontekście widoku.
- Bezpośrednie wejście z historii do pustej sesji rozpoczynało dwa niezależne odczyty: korzenia i albumu. Późniejszy wynik korzenia mógł wyczyścić stos powrotu. Bezpośrednie przejście ładuje tylko album; `Backspace` w razie potrzeby ładuje korzeń.
- **Korekta z 3.11.2026:** wcześniejszy zapis, że TIDAL nie obsługuje playlist konta, był nieprawdziwy jako opis całego toru. Sesja TIDAL czytała playlisty, a istniejąca usługa miała metody tworzenia playlist i dodawania do nich utworów. W AMC Test brakowało połączenia tych metod ze skrótem, menu i dialogiem. Niniejsza zmiana dodaje to połączenie. Powiązanie rozpoznania z biblioteką plików lokalnych pozostaje osobnym zadaniem.

## Materiał do dokumentacji użytkownika

> W historii rozpoznanych utworów naciśnij prawą strzałkę i wybierz „Spotify — pokaż album w AMC” albo „TIDAL — pokaż album w AMC”. W oknie działań `Ctrl+Shift+C` kopiuje link wybranego działania; jeśli dokładnego albumu nie uda się potwierdzić, usłyszysz, że skopiowano link wyszukiwania. Po wejściu do albumu naciśnij `Ctrl+Shift+L`, aby zapisać cały album w Bibliotece usługi. `Backspace` wraca do poprzedniej listy tej usługi. W Spotify możesz zaznaczyć utwory albumu i użyć `Ctrl+Shift+P`, aby dodać je do wybranej playlisty. W TIDAL `Ctrl+Shift+P` z albumu dodaje cały album jako utwory do wybranych playlist konta; w tym oknie Spacja zaznacza playlisty, a przycisk „Nowa playlista” tworzy nową.

## Testy i wynik

- Testy końcowe bez widocznego GUI: 258/258 (rozpoznawanie, Spotify i TIDAL, nawigacja, menu, paleta i skróty) oraz osobno 6/6 (okno playlist Spotify). Testy pokrywają dokładny link i wyszukiwanie zastępcze, zapis albumu z listy utworów, brak równoległego ładowania korzenia i powrót do niego z bezpośredniego wejścia. Wcześniejszy wspólny przebieg obu grup dał 262 zaliczone i 1 błąd samej atrapy wx w teście playlist (`object.__new__` na prawdziwym wx); osobny przebieg tej grupy przeszedł.
- Po korekcie TIDAL: 101/101 testów celowanych Python (nowy tor playlist TIDAL, klient hosta, istniejące okno Spotify, nawigacja TIDAL, menu i paleta); zestaw kontraktu `--tidal-catalog` przeszedł; host `dotnet build --no-restore` przeszedł bez ostrzeżeń. Nie wykonano jeszcze testu na koncie TIDAL ani odbioru NVDA.
- Build instalatora, instalacja, publikacja i odbiór NVDA: **nie wykonano** w tym etapie. Kompilacja hosta testowego powiodła się. Żaden uruchomiony program nie został zamknięty.

## Znane ograniczenia

- Testy bez GUI nie potwierdzają jeszcze mowy NVDA, natywnego `Ctrl+A` ani kolejności fokusu po rzeczywistym przejściu między oknami. Przed odbiorem potrzebny jest test klawiaturą w AMC Test.
- Kopia linku wyszukiwania nie jest dokładnym łączem do albumu; komunikat celowo odróżnia te wyniki.
- Zapis playlist TIDAL wymaga połączonego konta z uprawnieniem zapisu playlist. Wynik częściowy podaje liczbę potwierdzonych playlist i nazwy nieudanych celów.
- Lista docelowych playlist jest trzymana w pamięci procesu; ponowne otwarcie nie ponawia odczytu. Nowa playlista trafia do tej listy po potwierdzeniu przez TIDAL. Ponowne logowanie unieważnia listę. Zmiany playlist zrobione poza AMC mogą wymagać ponownego uruchomienia aplikacji.

## Wersja i commit

- Baza pracy: `0bad8716` na `codex/konfigurowalne-skroty-20261026` w kanonicznym repozytorium.
- Commit kodu, testów i opisu: `09580684800e62d48ab09ffe21c058f5ec54f067`.
- Korekta playlist konta TIDAL, testów i dokumentacji z 3.11.2026: `7cf8adb2`.
- 3.11.2026 przed kompilacją: aktywna instalacja AMC Test działa z `C:\Users\micha\AppData\Local\Programs\AMC Test` i ma etykietę `2026.11.02-a415e5066d0b04078ee1146a83d16c94dd14fd7d-8e9b085e`. Starszy katalog `D:\Projekty Codex\AMC Python` nie jest aktywny.
- Identyfikator nowego pakietu i data instalacji: brak, ponieważ stan nagrań w aktywnej instancji nie został jeszcze potwierdzony.
