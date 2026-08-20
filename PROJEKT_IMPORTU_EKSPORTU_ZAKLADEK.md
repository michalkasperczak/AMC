# Osobny import i eksport zakładek AMC

## Cel

Planowany plik `*.amcbookmarks.json` ma pozwolić przenieść wybrane albo wszystkie zakładki niezależnie od pełnej kopii AMC. Nie może zawierać haseł, tokenów, podpisanych adresów strumieni ani danych logowania.

## Tożsamość materiału

Sama ścieżka lokalna nie jest tożsamością pliku. Służy wyłącznie jako podpowiedź dla użytkownika. Dwie bezpieczne metody dopasowania to:

- stabilny identyfikator elementu zwrócony przez adapter danej usługi;
- dla pliku lokalnego skrót treści SHA-256, uzupełniony rozmiarem i czasem trwania.

Obliczanie skrótu może potrwać przy dużych nagraniach, dlatego będzie wykonywane wyłącznie podczas jawnego eksportu lub importu, z postępem i możliwością anulowania. Nie może spowalniać zwykłego dodawania zakładki ani odtwarzania.

## Reguły importu

1. Element usługi jest łączony wyłącznie z tym samym adapterem i stabilnym identyfikatorem.
2. Plik lokalny jest łączony automatycznie tylko po zgodnym SHA-256. Nazwa, ścieżka, rozmiar i czas nie wystarczają samodzielnie do cichego dopasowania.
3. Gdy pliku nie znaleziono, zakładki pozostają nierozwiązane. Użytkownik może wskazać plik albo folder do bezpiecznego wyszukania.
4. Zakładka w tym samym materiale i w granicy jednej sekundy jest traktowana jako duplikat. Pozostałe rekordy są scalane bez usuwania istniejących danych.
5. Import nigdy nie uruchamia odtwarzania i nie zmienia kont usług.

## Format

Koperta JSON otrzyma numer schematu, rodzaj `bookmarks`, datę eksportu oraz rekordy materiałów i pozycji. Dla każdego materiału zapisze przyjazny tytuł, typ źródła, stabilny identyfikator lub lokalny odcisk treści oraz opcjonalną dawną ścieżkę jako podpowiedź. Pozycje zachowają czas, datę utworzenia i przyszłą opcjonalną nazwę zakładki.

## Zakres wersji

`alpha.69` nie dodaje jeszcze przycisków osobnego importu i eksportu. Zakładki nadal znajdują się w `state.json` oraz pełnej kopii `*.amcbackup.json`. Ten dokument ustala bezpieczny format następnego etapu i wyklucza zawodne dopasowanie tylko po nazwie lub ścieżce.
