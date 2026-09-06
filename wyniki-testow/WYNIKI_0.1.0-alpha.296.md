# Wyniki testów AMC 0.1.0-alpha.296

Wpisuj obserwacje pod odpowiednim zadaniem. Nie trzeba przy każdym punkcie
dopisywać osobnego słowa „OK” albo „błąd”. Po dwukropku wpisuj spację.

## AMC-296-01 — eksport Ulubionych Radia przez Ctrl+E

W sesji Radio otwórz Ulubione i naciśnij `Ctrl+E`.

Oczekiwane: otwiera się zapis playlisty M3U z ulubionymi stacjami. Po zapisaniu
albo anulowaniu fokus wraca do dotychczasowej listy.

Wynik:

## AMC-296-02 — brak dawnego skrótu w Radiu

W sesji Radio naciśnij `Ctrl+Shift+O`.

Oczekiwane: skrót nie uruchamia eksportu Ulubionych. Menu i pomoc klawiszy
podają wyłącznie `Ctrl+E`.

Wynik:

## AMC-296-03 — brak regresji Ctrl+Shift+O

W Plikach lokalnych sprawdź `Ctrl+Shift+O` dla Folderów Biblioteki, a w sesji
WiiM sprawdź eksport strumieni.

Oczekiwane: oba dotychczasowe zastosowania `Ctrl+Shift+O` nadal działają.

Wynik:

## AMC-296-04 — T w nagraniu z harmonogramu

Podczas aktywnego zaplanowanego nagrania naciśnij `T` w odtwarzaczu lub widoku
**Nagrywane**.

Oczekiwane: bieżąca część zostaje zapisana, rozpoczyna się następna, a plan
pozostaje aktywny.

Wynik:

## Inne obserwacje
