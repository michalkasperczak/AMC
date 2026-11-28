# UNDO-001 — przywracanie pozycji kolekcji

Status: Część podcastowa wdrożona w kodzie 28.11.2026; czeka na odbiór NVDA i instalację. Pozostałe kolekcje i kolejki wymagają osobnych decyzji. **Rekord odtworzony 16.11.2026
wyłącznie z wpisu w `docs/zadania/README.md`; pierwotnego pełnego pliku nie
odnaleziono.**

## Zachowany zakres

Przegląd `Ctrl+Z`, trwałych porządków Ulubionych i kolejek oraz kontraktu
cofania. Rejestr nie zawiera szczegółowych kryteriów, testów ani decyzji
użytkownika dla tego zadania.

## Następny krok

Przed implementacją odczytać obecny kod i pomoc `Ctrl+Z`, ustalić z
użytkownikiem, które operacje na kolekcji mają być cofane, i zapisać
kryteria odbioru oraz scenariusze NVDA. Nie wywodzić nowych uprawnień
do usuwania danych z samego tytułu zadania.


## Decyzja użytkownika z 28.11.2026

Użytkownik potwierdził, że chodzi o Ctrl+Z po usunięciu podcastu lub odcinka
YouTube z Ulubionych; pozycja ma wrócić dokładnie na poprzednie miejsce.
Ta decyzja zastępuje wcześniejszy brak zakresu z odtworzonego rekordu z
16.11.2026. W tej iteracji cofanie obejmuje tylko ostatnią zmianę członkostwa
Ulubionych w sesji Podcasty i YouTube. Kolejki, TIDAL, Spotify i inne sesje
wymagają osobnych decyzji.

### Zachowanie i komunikaty

- Ctrl+Shift+U lub Delete na liście Ulubionych zmienia zaznaczone podcasty
  i odcinki. Host zwraca jednorazowy token cofania po faktycznej zmianie.
- Ctrl+Z z listy albo odtwarzacza sesji Podcasty i YouTube cofa ostatnią
  zmianę. Menu Biblioteka, paleta Ctrl+Shift+K, pomoc kontekstowa Ctrl+F1
  i pełna pomoc pokazują to samo polecenie wyłącznie w tej sesji i gdy istnieje
  ostatnia zmiana. W polu filtra Ctrl+Z pozostaje natywną edycją tekstu.
- Po sukcesie NVDA dostaje „Przywrócono poprzedni stan Ulubionych”. Lista
  odświeża się z preferencją identyfikatora przywróconego elementu i zachowuje
  fokus na liście. Bez dostępnego cofnięcia NVDA mówi „Nie ma zmiany Ulubionych
  do cofnięcia”.
- Cofnięcie jest jednorazowe i gaśnie po następnej zmianie Ulubionych. Host
  sprawdza aktualne odciski wszystkich dotkniętych wierszy w jednej transakcji.
  Późniejsza zmiana odcinka lub podcastu powoduje odmowę bez częściowego
  przywrócenia. Trwałe tabele kolejności favorite_order i
  favorite_added_order nie są zmieniane przy przełączeniu członkostwa ani
  przy cofnięciu, więc odzyskany element wraca na swą poprzednią pozycję.
  Zmiana kolejności przez zewnętrzny proces między operacjami nie jest
  osobno wykrywana.

### Weryfikacja i stan wydania

Kod: Core SQLite, LiteHost, klient protokołu i GUI wxPython. Test hosta
--podcast-progress sprawdza mieszany wybór, błędny token, jednorazowość,
spójność kolumn i odmowę po późniejszym zapisie odcinka. Test Python sprawdza
skrót, menu, paletę, pomoc, sesję wyłączną, wskazanie przywróconego wiersza i
wygaśnięcie tokenu. Kompilacja Release hosta: 0 błędów, 0 ostrzeżeń.
Rzeczywisty odsłuch NVDA, instalacja i publikacja: jeszcze niewykonane w tym
worktree. Commit kodu: wskazany w historii Git pliku po scaleniu.

### English guide and changelog draft

In Podcasts and YouTube, press Ctrl+Z to undo your last Favorites change.
After removing a podcast or episode, it returns to its previous list position.
The command is available from the Library menu, command palette, and keyboard
help while an undo is pending. A later change to a selected item makes the
undo safely unavailable. This is a source and test change; installation and
screen reader acceptance are still pending.
