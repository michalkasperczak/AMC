# Testy AMC 0.1.0-alpha.278

Po każdym zadaniu wpisz krótko wynik po dwukropku. Gdy wszystko działa,
wystarczy `OK`.

## AMC-278-01 — Otwieranie listy strumieni WiiM

Przejdź do sesji WiiM i naciśnij `Ctrl+L`. Program powinien powiedzieć
„Strumienie sieciowe”. Pusta lista nie może przełączyć sesji ani zgubić fokusu.

Wynik:

Uwagi:

## AMC-278-02 — Dodanie i uruchomienie strumienia

Naciśnij `Ctrl+N`, wpisz nazwę oraz publiczny adres HTTP lub HTTPS. Po zapisaniu
fokus powinien trafić na nowy wpis. Enter powinien zapytać o wysłanie go do
aktywnego WiiM, a po potwierdzeniu otworzyć odtwarzacz urządzenia.

Wynik:

Uwagi:

## AMC-278-03 — Edycja, kopiowanie i usunięcie

Na zapisanym strumieniu sprawdź `F2`, `Ctrl+C`, `Ctrl+Shift+C` oraz Delete.
`Ctrl+C` powinien kopiować nazwę, `Ctrl+Shift+C` adres, a Delete usuwać wpis
wyłącznie z listy AMC i wyraźnie to potwierdzać.

Wynik:

Uwagi:

## AMC-278-04 — Import playlisty

Naciśnij `Ctrl+O` i wskaż M3U, M3U8 albo PLS zawierający kilka publicznych
strumieni. Program powinien podać liczbę dodanych wpisów, pozostawić fokus na
pierwszym nowym wpisie i nie tworzyć duplikatów po ponownym imporcie.

Wynik:

Uwagi:

## AMC-278-05 — Trwałość i powrót do urządzeń

Zamknij i uruchom AMC ponownie. Lista `Ctrl+L` powinna zachować strumienie.
Backspace powinien wrócić do poprzedniego widoku sesji WiiM, a `Ctrl+F5`
otworzyć menedżer urządzeń bez usunięcia zapisanej listy.

Wynik:

Uwagi:

## AMC-278-06 — Regresja sterowania WiiM

Sprawdź natywne presety, `Alt+Page Up/Alt+Page Down`, regulację głośności oraz
odczyt bieżącego tytułu. Nowa lista nie powinna zmienić dotychczasowego
sterowania urządzeniem.

Wynik:

Uwagi:
