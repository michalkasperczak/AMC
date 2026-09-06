# AMC 0.1.0-alpha.280 — strumienie i presety WiiM

Instrukcja: przy każdym punkcie dopisz krótko wynik. Testy wysyłające dźwięk
wykonaj dopiero po włączeniu urządzenia WiiM. Uruchomiona starsza wersja nie
jest automatycznie zamykana przez przygotowanie tej paczki.

## 1. Kolejność eksportu dla WiiM Home

1. W sesji WiiM otwórz `Ctrl+L`.
2. Zapamiętaj pierwsze i ostatnie trzy strumienie.
3. Wyeksportuj listę przez `Ctrl+Shift+O` i zaimportuj plik w WiiM Home.
4. Sprawdź kolejność widoczną w WiiM Home.

Oczekiwane: po imporcie kolejność jest taka sama jak w AMC. Techniczna
kolejność pliku jest celowo odwrotna, ponieważ WiiM Home dokłada importowane
wpisy na początek.

Wynik:

## 2. Uruchomienie bez dodatkowego pytania

1. Na liście strumieni naciśnij Enter.
2. Powtórz test poleceniem `Ctrl+Alt+W`.

Oczekiwane: każde świadome polecenie od razu wysyła adres do aktywnego WiiM;
AMC nie pyta ponownie, czy otworzyć strumień. Po powodzeniu fokus znajduje się
w odtwarzaczu urządzenia.

Wynik:

## 3. Alt+Page Up i Alt+Page Down po uruchomieniu strumienia

1. Otwórz środkowy strumień z listy AMC.
2. W odtwarzaczu naciśnij `Alt+Page Down`, a potem `Alt+Page Up`.

Oczekiwane: przełączane są sąsiednie strumienie z listy AMC, z zawijaniem na
końcach. Nie jest uruchamiany przypadkowy preset sprzętowy.

Wynik:

## 4. Alt+Page Up i Alt+Page Down po uruchomieniu presetu

1. Otwórz preset sprzętowy przez `Ctrl+Alt+P` albo przypisany skrót.
2. Naciśnij `Alt+Page Down`, a potem `Alt+Page Up`.

Oczekiwane: przełączane są sąsiednie zajęte presety urządzenia, a nie lokalne
strumienie AMC.

Wynik:

## 5. Skróty cyfr

Sprawdź `Ctrl+Shift+1–0/-/=`.

Oczekiwane: skróty pozostają jednoznacznie przypisane do presetów. Strumienie
AMC nie przejmują tych kombinacji.

Wynik:

## 6. Ponowne uruchomienie

1. Otwórz strumień AMC i zamknij program, kiedy nic nie jest nagrywane.
2. Uruchom alpha.280, przejdź do odtwarzacza WiiM i użyj `Alt+Page Down`.

Oczekiwane: po odświeżeniu urządzenia AMC rozpoznaje ostatni strumień jako
punkt nawigacji. Jeśli źródło zostało w międzyczasie zmienione poza AMC,
program nie udaje nieaktualnego stanu.

Wynik:
