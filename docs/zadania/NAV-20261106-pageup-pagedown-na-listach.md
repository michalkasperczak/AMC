# NAV-20261106 — Page Up i Page Down na listach

## Decyzja użytkownika — 6 listopada 2026

Na listach plików, strumieni, podcastów i utworów zwykłe Page Up i Page Down mają przesuwać zaznaczenie o stronę. W widoku odtwarzacza te same klawisze nadal wybierają poprzedni lub następny utwór kolejki. Użytkownik potwierdził rozróżnienie według widoku. Decyzja zastępuje wcześniejszy wyjątek listy TIDAL, gdzie oba klawisze sterowały odtwarzaniem również bez otwarcia odtwarzacza.

## Zachowanie i dostępność

Na każdej liście oba klawisze są oddawane natywnej kontrolce Windows; AMC nie uruchamia ani nie pomija utworu. Dotyczy to również aktywnego odtwarzania TIDAL. `Ctrl+Page Up` i `Ctrl+Page Down` nadal zmieniają sesję. Dotychczasowe `Shift+Page Up` i `Shift+Page Down` w TIDAL zachowują wcześniejszą funkcję zmiany utworu. W odtwarzaczu Page Up i Page Down nadal sterują kolejką.

Menu TIDAL i paleta poleceń nie pokazują skrótu Page Up/Down, gdy użytkownik jest na liście, choć same polecenia poprzedniego/następnego utworu pozostają dostępne z menu. `Ctrl+F1` na liście opisuje klawisze jako nawigację listy; pełna pomoc nie reklamuje tam zmiany utworu. W odtwarzaczu menu, paleta i pomoc pokazują skróty. Nazwy poleceń TIDAL nie sugerują już, że Page Up/Down działa na liście.

## Weryfikacja i wydanie

Test zdarzeń wx sprawdza obie strony na wszystkich sesjach, resolver odtwarzacza i zachowanie `Ctrl+Page Up`; 3 celowane testy skrótów wraz z Ctrl+I zaliczone. Pełny zestaw, rzeczywisty odbiór NVDA, build, GitHub, instalacja i numer wersji pozostają do sprawdzenia przy wspólnym wydaniu z zadaniami równoległymi. Zmiana nie wymaga migracji ustawień.
