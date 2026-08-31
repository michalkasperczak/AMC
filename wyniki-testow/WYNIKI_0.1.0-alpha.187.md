# Wyniki testów AMC 0.1.0-alpha.187

## Zakres wersji

- Każdy standardowy klawisz bloku numerycznego może zostać globalnym
  prefiksem AMC.
- Modyfikatory Control, Alt, Shift i Windows są opcjonalne; sam Plus
  numeryczny jest prawidłowym prefiksem.
- Obsługiwane są cyfry, Enter, Plus, Minus, Gwiazdka, Ukośnik, kropka,
  separator oraz Num Lock.
- Przy wyłączonym Num Lock numeryczne Insert, Delete, Home, End, Page Up,
  Page Down i strzałki pozostają odrębne od zwykłego bloku nawigacyjnego.
- Okno przechwytywania, Ustawienia i komunikaty NVDA używają wyłącznie nazw
  użytkowych.

## Weryfikacja automatyczna

- Kompilacja Release: OK, 0 ostrzeżeń i 0 błędów.
- Testy rdzenia: OK. Sprawdzono normalizację użytkowych nazw całego bloku
  numerycznego, w tym Plusa, Minusa, kropki, cyfry, Insertu i Num Lock.
- Testy Windows: OK. Sprawdzono osobne kody operatorów, prefiks bez
  modyfikatorów, dostępne etykiety oraz rozróżnienie numerycznego Insertu i
  Delete od zwykłego bloku nawigacyjnego.
- Publikacja samowystarczalnego programu dla `win-x64`: OK.

## Pakiet

- Program: `publish\AccessibleMediaController-0.1.0-alpha.187\AccessibleMediaController-0.1.0-alpha.187.exe`.
- Wersja produktu: `0.1.0-alpha.187`.
- Rozmiar programu: `165980064` bajty.
- SHA-256: `C4C1483454E885C1932A37A44D93925832905ED27216FD63523BF448CB666F1D`.

## Test ręczny

### AMC-187-01 — sam Plus numeryczny

Ustaw sam Plus numeryczny jako prefiks, zapisz Ustawienia i sprawdź poza AMC,
czy Plus, a następnie polecenie warstwy uruchamia właściwe działanie.

Oczekiwane: AMC mówi „Plus numeryczny”, nie wymaga modyfikatora i nie
przekazuje przejętego Plusa do programu pod fokusem.

Uwagi:

### AMC-187-02 — cały blok numeryczny

Sprawdź kolejno Minus, Gwiazdkę, Ukośnik, kropkę, cyfrę numeryczną i Num Lock,
za każdym razem zastępując poprzedni prefiks.

Oczekiwane: każdy klawisz ma własną czytelną nazwę i działa jako samodzielny
prefiks. Poprzednia wartość znika bez ręcznego kasowania.

Uwagi:

### AMC-187-03 — stan Num Lock i osobny blok nawigacyjny

Przy wyłączonym Num Lock ustaw `0 numeryczny`, czyli Insert numeryczny.
Porównaj go ze zwykłym Insertem. Powtórz próbę dla Delete numerycznego i
zwykłego Delete.

Oczekiwane: tylko klawisz fizycznie należący do bloku numerycznego uruchamia
prefiks; zwykły blok nawigacyjny pozostaje wolny.

Uwagi:

### AMC-187-04 — modyfikator i przywrócenie wartości

Ustaw `Ctrl+Minus numeryczny`, sprawdź go, a następnie wybierz **Przywróć
domyślny** i zapisz Ustawienia.

Oczekiwane: kombinacja z modyfikatorem działa, a przywrócenie ustawia
`Ctrl+Alt+Windows+F12`.

Uwagi:
