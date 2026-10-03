# AMC 0.1.0-alpha.419 — zmiana wyłącznie wielkości liter w nazwie pliku

## Shift+F2 w Bibliotece i Sideloadach

Zmiana samej wielkości liter w nazwie pliku już nie jest odrzucana. „No cześć”
zmienisz na „No Cześć” jednym Shift+F2, bez obchodzenia problemu przez nazwę
tymczasową.

Nowa pisownia trafia na dysk i na listę od razu. Zostaje też po F5 i po
ponownym otwarciu programu — to zmiana rzeczywistego pliku, nie samej etykiety
na liście.

Odmowa przy zajętej nazwie działa jak dotąd: jeśli w tym samym katalogu jest
już inny plik o docelowej nazwie, zmiana zostaje odrzucona, a żaden plik nie
jest nadpisywany ani usuwany.

## Zakres sprawdzenia

Sprawdzono na Windows z żywym NVDA, w izolowanej kopii i na własnym krótkim
pliku WAV — bez dotykania nagrań użytkownika. Czytnik potwierdził kolejno:
zaznaczony wiersz „No cześć”, otwarte okienko zmiany nazwy, nową treść pola
„No Cześć”, a po zatwierdzeniu Enterem wiersz „No Cześć” na liście. Nowa
pisownia została potwierdzona na dysku co do znaku (wielkie „C”), przetrwała
F5, a próba zmiany na nazwę już zajętą została odrzucona z obydwoma plikami
nienaruszonymi.

Testy automatyczne mierzą tę drogę krokami produkcyjnymi: wyliczenie nazwy
docelowej, zmianę nazwy na dysku i aktualizację ścieżek w danych programu.
Sprawdzono też, że test faktycznie rozróżnia poprawne i błędne zachowanie —
po celowym zepsuciu aktualizacji ścieżek test zapala się na czerwono.
