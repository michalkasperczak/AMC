# Zadania testowe AMC

- Numer zestawu: `AMC-TEST-034`
- Tytuł zestawu: Otwieranie folderów i lokalne informacje o czasie
- Wersja programu: `0.1.0-alpha.34`
- Utworzono: 2026-08-17 12:48, Europe/Warsaw
- Plik wyników: `wyniki-testow/WYNIKI_2026-08-17_1248_0.1.0-alpha.34.md`

W pliku wyników po dwukropku wpisz krótko, co się wydarzyło. Jeżeli wszystko działa, wystarczy `OK`. Nie trzeba przed każdym zadaniem wybierać wariantu odpowiedzi.

Do pierwszej próby wybierz niewielki folder zawierający kilka plików audio. Dobrze, jeśli ma podfolder oraz pliki o nazwach podobnych do `Utwór 2` i `Utwór 10`. Program nie zmienia ani nie usuwa wybranych plików.

## AMC-034-01 — Otwieranie całego folderu

1. Uruchom AMC i naciśnij `Ctrl+Shift+O`.
2. Wybierz folder zawierający pliki audio i zatwierdź okno systemowe.

Oczekiwane:

- program krótko podaje `Wczytywanie folderu`;
- wczytuje rozpoznane pliki z folderu i podfolderów, ale nie rozpoczyna odtwarzania;
- fokus wraca na pierwszy dodany element w sesji `Lokalne multimedia`;
- numerowane nazwy występują w naturalnej kolejności, np. `Utwór 2` przed `Utwór 10`.

## AMC-034-02 — Pliki, folder i duplikaty

1. Naciśnij `Ctrl+O` i wybierz plik znajdujący się już we wczytanym folderze.
2. Ponownie otwórz ten sam folder przez `Ctrl+Shift+O`.

Oczekiwane:

- ten sam plik nie tworzy kolejnych pozycji;
- program informuje, że pliki były już na liście;
- sesja lokalna i zaznaczenie pozostają dostępne.

## AMC-034-03 — Menu Plik i paleta poleceń

1. Otwórz menu Plik i przejdź przez `Otwórz pliki audio` oraz `Otwórz folder z plikami audio`.
2. Otwórz paletę `Ctrl+Shift+K` i wyszukaj kolejno oba polecenia.
3. Wyszukaj także `Otwórz w oficjalnej aplikacji`.

Oczekiwane:

- NVDA podaje `Ctrl+O` przy plikach i `Ctrl+Shift+O` przy folderze zarówno w menu, jak i w palecie;
- polecenie oficjalnej aplikacji nadal istnieje, ale nie podaje `Ctrl+Shift+O`;
- samo przechodzenie po pozycjach niczego nie otwiera.

## AMC-034-04 — Czas bez globalnego prefiksu

1. Uruchom odtwarzanie lokalnego pliku Enterem.
2. Będąc na głównej liście, naciśnij bez prefiksu `Ctrl+E`, `Ctrl+R` i `Ctrl+T`.

Oczekiwane:

- program podaje kolejno czas od początku, czas pozostały i czas całkowity;
- wartości dotyczą rzeczywiście odtwarzanego pliku;
- skróty nie przenoszą fokusu i nie zatrzymują dźwięku.

## AMC-034-05 — Folder bez audio i dalsza praca

1. Przez `Ctrl+Shift+O` wybierz niewielki folder, w którym nie ma plików audio.
2. Następnie otwórz poprawny plik przez `Ctrl+O` i uruchom go Enterem.

Oczekiwane:

- program podaje `W folderze nie znaleziono obsługiwanych plików audio`;
- nie tworzy pustych pozycji i nie zawiesza się;
- po komunikacie nadal można normalnie otworzyć i odtworzyć plik.

Uwaga: zwykłe strzałki góra/dół w głównym oknie nadal nawigują po liście. Sterowanie czasem i głośnością strzałkami pozostaje w tej wersji w warstwie prefiksowej. Lista lokalna nadal znika po zamknięciu AMC.
