# Przegląd wyników AMC do alpha.18

Data przeglądu: 2026-08-14. Zakres: wszystkie zapisane pliki `WYNIKI_*.md` od alpha.3 do alpha.18. Puste, niewypełnione arkusze nie są traktowane jako wykonane testy.

## Zamknięte grupy problemów

- Surowe nazwy obiektów `MediaItemRow`, `ItemRow` i `SessionRow` zastąpiły zwykłe, dostępne listy z jednoznacznymi etykietami.
- Fokus startowy, powrót z menu i okien, usuwanie środka listy oraz zachowanie pustej Kolejki zostały ustabilizowane i później sprawdzone ponownie.
- `Escape` w filtrze czyści filtr i wraca do listy; filtr `Ctrl+K` oraz wyszukiwanie `Ctrl+F` i `Ctrl+Shift+F` mają osobne znaczenia.
- Kolejka, „Odtwórz jako następne” i Ulubione działają jak przełączniki, a komunikaty zawierają nazwę elementu.
- Nawigacja wpisywanymi literami korzysta z semantycznej nazwy zasobu i obsługuje kolejne litery oraz powtarzanie jednej litery.
- Zwykły Enter na bieżącym utworze przełącza odtwarzanie i pauzę; bezpośrednie akcje na wynikach nie zamykają wyszukiwania.
- `Alt+F4` ma standardowy zakres: zamyka aktywne wyszukiwanie albo całą aplikację, gdy aktywne jest okno główne.

## Uwagi alpha.18 rozwiązane w alpha.19

1. Tryb wyszukiwania jest częścią tytułu okna i dostępnościowej nazwy pola edycyjnego.
2. Po powrocie z wyszukiwania globalnego program podaje usługę i bieżący widok, jeśli kontekst mógł się zmienić.
3. Lista podaje etykietę i pozycję wyniku bez dodatkowego komunikatu na żywo „1 wynik” lub „N wyników”. Widoczna liczba pozostaje na ekranie.
4. Szczegółowa instrukcja klawiszowa jest pomocą elementu wyniku, więc ma zostać odczytana po jego nazwie. Tryb krótki nie zawiera tej instrukcji.
5. „Odtwórz teraz” nie uruchamia równoległego dźwięku. Sesja ma jeden tor: inny utwór zastępuje bieżący, a ten sam zostaje ustawiony w stanie odtwarzania bez przełączania na pauzę.

## Przyjęte dalsze zasady

- Historia wyszukiwania będzie lokalna, rozdzielona dla usług i zakresu globalnego, ograniczona do 20 unikatowych zapytań. Strzałka w dół przy pustym polu otworzy historię, a Enter uruchomi wybrane zapytanie. Historia otrzyma opcję wyczyszczenia i wyłączenia.
- Strzałki w lewo i w prawo mają docelowo przeglądać opcjonalne, znormalizowane metadane bieżącego zasobu we wszystkich listach. Funkcja powstanie po rozszerzeniu modelu danych i deklaracji możliwości adapterów.

## Sprawy nadal otwarte

- Dodatkowe słowo „Undo” pochodzi z funkcji **Clipboard command announcement** dodatku NVDA Global Commands Extension. Logika `Ctrl+Z` w AMC działa; pozostaje ewentualny profil zgodności lub instrukcja dla dodatku.
- Globalny prefiks wymaga niskopoziomowego, konfigurowalnego przechwytywania oraz testów kandydatów, w tym `Ctrl+Numeryczny Enter`, z NVDA, JAWS-em i menedżerami schowka.
- `Ctrl+Shift+K` jest zarezerwowane dla dostępnej palety poleceń, która nie została jeszcze zaimplementowana.
- AMC.Host, adapter WiiM, pierwsze logowanie OAuth oraz bezpieczny instalator i aktualizator komponentów pozostają kolejnymi etapami po ustabilizowaniu okna.
- W drzewie roboczym brakuje `ZBUDUJ_I_URUCHOM.cmd`, choć dokumentacja nadal go opisuje. Usunięcie nie zostało włączone do zmian alpha.19; przed publikacją trzeba świadomie wybrać przywrócenie programu uruchamiającego albo usunięcie odwołań z dokumentacji.
