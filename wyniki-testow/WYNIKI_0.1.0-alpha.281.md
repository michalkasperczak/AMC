# AMC 0.1.0-alpha.281 — test Strumieni sieciowych WiiM

Nie trzeba włączać urządzenia WiiM do testów 1–4. Testy 5–6 wykonaj dopiero,
gdy urządzenie jest dostępne w sieci.

1. W sesji WiiM otwórz `Ctrl+L`, wybierz `Alt+3`, zaznacz jeden strumień i
   przenieś go `Alt+strzałka w górę` albo `Alt+strzałka w dół`.
   Oczekiwane: komunikat podaje kierunek i sąsiedni element, a fokus pozostaje
   na przeniesionym strumieniu.

2. Zaznacz Shiftem dwa sąsiednie strumienie i przesuń blok.
   Oczekiwane: oba wpisy zmieniają miejsce razem i pozostają zaznaczone.

3. Na kilku zaznaczonych wpisach użyj `Ctrl+C`, następnie `Ctrl+Shift+C`.
   Oczekiwane: pierwsze polecenie kopiuje wszystkie nazwy, drugie pary
   „nazwa, adres”, bez technicznych identyfikatorów.

4. Zmień nazwę i adres przez `F2`, zapisz, ponownie otwórz `F2`, a potem anuluj.
   Oczekiwane: po zapisie i po anulowaniu fokus wraca do tego samego wpisu.

5. Ustaw kolejność przez `Alt+1`, `Alt+2` lub `Alt+3`, wyeksportuj
   `Ctrl+Shift+O` i zaimportuj plik w WiiM Home.
   Oczekiwane: kolejność widoczna w WiiM Home odpowiada kolejności wybranej w
   AMC, mimo technicznego dodawania wpisów od początku przez WiiM Home.

6. Uruchom strumień z listy AMC, potem użyj `Alt+Page Up` i
   `Alt+Page Down` w odtwarzaczu WiiM.
   Oczekiwane: program przechodzi po strumieniach w tym samym porządku co
   lista, a skróty `Ctrl+Shift+1–0/-/=` nadal dotyczą wyłącznie presetów.
