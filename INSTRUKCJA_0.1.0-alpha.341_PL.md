# AMC alpha 341 i dodatek NVDA 0.2.0

Nowa wersja jest gotowa lokalnie do Twoich testów. Nie zamykano działającego
AMC 340, nie uruchamiano 341 ani nie instalowano dodatku w NVDA. Publikacja
GitHub dopiero po akceptacji testów tej wersji.

## Pliki

- Dodatek: `D:\Projekty Codex\Accessible Multimedia Controller\AMC-NVDA-0.2.0.nvda-addon`
- Program: `D:\Projekty Codex\Accessible Multimedia Controller\publish\AccessibleMediaController-0.1.0-alpha.341\AccessibleMediaController-0.1.0-alpha.341.exe`
- Pełna mapa 53 skrótów i próby: `D:\Projekty Codex\Accessible Multimedia Controller\INSTRUKCJA_NVDA_PL.md`

Gdy nie nagrywasz, zamknij starszą wersję i uruchom 341. Potem zainstaluj
aktualizację dodatku zgodnie z instrukcjami NVDA. Nie usunięto starszego
pakietu aplikacji ani dodatku, aby można było wrócić do poprzedniej wersji.

## Najważniejsze

Do poniższych klawiszy zawsze dodaj Ctrl+Windows:

- P — pauza/wznowienie odsłuchu; lewo/prawo uruchamia poprzedni/następny
  element, również po pauzie. Zakres jest taki jak przy rozpoczęciu
  odtwarzania, np. Ulubione albo Presety.
- Alt+I — odczyt tego zakresu i pozycji.
- Alt+Page Up/Page Down — jawne przełączanie presetów; w WiiM presetów
  lub zapisanych strumieni. Zwykłe lewo/prawo w WiiM to transport urządzenia.
- Alt+R — rozpocznij/zakończ nagrywanie bieżącej stacji.
- Shift+R — pauza/wznowienie samego nagrywania, bez zmiany odsłuchu.
- Shift+T — nowy plik bieżącego nagrania, bez zmiany końca harmonogramu.
- B — zakładka; Shift+Page Up/Page Down — poprzednia/następna zakładka.
- L, U, Alt+P — otwórz odpowiednio Bibliotekę, Ulubione i Presety w AMC.
- Shift+A — otwórz wybór urządzenia audio; Shift+S — wybór sesji.

Otwieranie list jawnie przywołuje okno AMC. Polecenia odsłuchu pozostają
w tle. Czynności Ulubione/kolejka/nagrywanie dotyczą aktualnego nagrania,
nie innego elementu zaznaczonego na ukrytej liście. Nowych poleceń nie
obsłuży AMC 340; potrzebne są oba nowe pakiety.

Pełne testy automatyczne oraz 7 testów Pythona przeszły. Nie zastępuje to
odsłuchu i testu brajla w NVDA. Sprawdź zwłaszcza pomoc NVDA+1, pracę
z edytora, Escape po otwarciu listy, ponowne otwarcie dialogu, presety
po pauzie i krótkie nagranie testowe. Nie testuj pauzy/podziału na ważnym
nagraniu bez wcześniejszej próby. Oryginalny format bez konwersji może
nie obsługiwać pauzy nagrywania.
