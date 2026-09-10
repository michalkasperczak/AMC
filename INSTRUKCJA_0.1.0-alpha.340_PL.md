# AMC alfa 340 — wykonawca podzielony na kategorie

Program:
`D:\Projekty Codex\Accessible Multimedia Controller\publish\AccessibleMediaController-0.1.0-alpha.340\AccessibleMediaController-0.1.0-alpha.340.exe`

W działającym TIDAL wybierz wykonawcę Enterem albo użyj „Przejdź do wykonawcy”
na albumie lub utworze. Zobaczysz Albumy, Utwory i Podobni wykonawcy.
Enter otwiera wybraną kategorię; w Albumach kolejny Enter otwiera utwory albumu.
W Podobnych wykonawcach Enter otwiera analogiczny widok innego wykonawcy.
Nie pojawiają się dodatkowe okna. Escape albo Backspace cofa widok i przywraca
zaznaczenie. Wyjście z odtwarzacza zachowuje dotychczasowe ustawienia odsłuchu.

Ctrl+Shift+A to nadal Twoje albumy, nie dyskografia otwartego wykonawcy.
Alt+1 przywraca kolejność TIDAL, Alt+2 sortuje alfabetycznie otwartą listę
albumów, utworów lub podobnych wykonawców. Same trzy kategorie mają stałą
kolejność. Nie ma tu kolejności własnej Alt+3; nie można zmieniać dyskografii
wykonawcy jak własnej playlisty. F5 odświeża bieżącą zawartość TIDAL.

Listy nie są nazywane „popularnymi” ani „polecanymi”: ta wersja korzysta
z relacji katalogowych, nie obiecuje rankingu popularności. Biografia
i teledyski nie są jeszcze kategoriami tego widoku.

## Krótki test z NVDA

1. Wejdź do wykonawcy z Biblioteki lub przez strzałkę w prawo na albumie.
   Powinny być dokładnie trzy czytelne kategorie. A wybiera Albumy, U — Utwory,
   P — Podobnych wykonawców.
2. Enter na Albumach, Enter na albumie. Sprawdź kolejność utworów i nawigację
   po literach tytułu. Dwa razy Escape: najpierw ten sam album, potem Albumy.
3. Otwórz Utwory, potem wróć i otwórz Podobnych wykonawców. Listy nie powinny
   się mieszać. Enter na innym wykonawcy pokazuje jego trzy kategorie.
4. Wewnątrz kategorii porównaj Alt+1 i Alt+2. Ctrl+Shift+A ma pokazać Twoje
   albumy, niezależnie od przeglądanego wykonawcy.
5. Podczas wolnego wczytywania naciśnij Escape lub przełącz sesję. Późniejszy
   wynik nie powinien przenieść Cię z powrotem. Błąd nie usuwa Biblioteki.
6. Na samej kategorii Ctrl+Shift+L, Ctrl+Shift+U, Shift+Enter i Delete nie mogą
   zmienić kolekcji ani przypadkiem zadziałać na ostatnio odtwarzanym utworze.

Nie trzeba ponownie instalować dodatku NVDA 0.1.1. Ta wersja nie zmienia
ograniczenia TIDAL do próbek. Testy automatyczne nie zastępują odsłuchu
rzeczywistego NVDA i sprawdzenia danych z Twojego konta.
