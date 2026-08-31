# Wyniki testów AMC 0.1.0-alpha.186

## Zakres wersji

- `B` dodaje szybką zakładkę do aktualnie zapisywanego pliku radia.
- `Shift+B` dodaje zakładkę z nazwą; nazwa może zastąpić brak nazwy zakładki
  utworzonej w tej samej sekundzie.
- Skróty działają w odtwarzaczu radia oraz na stacji wybranej w widoku
  Nagrywane.
- Czas pochodzi z nagrania, nie z pozycji odsłuchu w buforze timeshift.
- Zakładki sprzed i po podziale pozostają przypisane do właściwych plików.
- Po zakończeniu nagrania AMC importuje gotowy plik do Plików lokalnych i
  zapisuje przy nim zakładki w trwałym magazynie programu.
- Funkcje są również dostępne w menu Odtwarzanie oraz w menu kontekstowym
  nagrywanej stacji.

## Weryfikacja automatyczna

- Kompilacja Release: OK, 0 ostrzeżeń i 0 błędów.
- Testy rdzenia i Windows: OK. Sprawdzono rozróżnienie `B` i `Shift+B`, brak
  duplikatu w tej samej sekundzie, nadanie nazwy oraz przypisanie punktów do
  części sprzed i po podziale.
- Publikacja samowystarczalnego programu dla `win-x64`: OK.

## Pakiet

- Program: `publish\AccessibleMediaController-0.1.0-alpha.186\AccessibleMediaController-0.1.0-alpha.186.exe`.
- Wersja produktu: `0.1.0-alpha.186`.
- Rozmiar programu: `165970336` bajtów.
- SHA-256: `805D9ECC7CAD31DE8C7AB42BF824D1F102A6AB82AAF15D60380C6C9D17D82AD3`.

## Test ręczny

### AMC-186-01 — szybka i nazwana zakładka

Uruchom ręczne nagrywanie stacji klawiszem `R`. Gdy nagranie już trwa,
naciśnij `B`, odczekaj kilka sekund i naciśnij `Shift+B`, wpisując nazwę.
Zakończ nagrywanie klawiszem `R`, przejdź do Plików lokalnych i otwórz listę
Zakładek przez `Ctrl+B`.

Oczekiwane:

- po `B` AMC podaje czas dodanej zakładki bez otwierania okna;
- `Shift+B` otwiera dostępne pole nazwy i po zatwierdzeniu podaje nazwę oraz
  czas;
- po zakończeniu nagrania obie zakładki są widoczne przy gotowym pliku;
- Enter na zakładce otwiera plik w zapamiętanym miejscu.

Uwagi:

### AMC-186-02 — zakładki w dwóch częściach

Podczas ręcznego nagrywania dodaj zakładkę przez `B`, rozpocznij nową część
klawiszem `T`, a następnie dodaj kolejną zakładkę.

Oczekiwane:

- pierwsza zakładka należy do pierwszego pliku;
- druga zakładka należy do drugiego pliku i liczy czas od początku drugiej
  części;
- żadna zakładka nie przeskakuje między plikami po zakończeniu nagrania.

Uwagi:

### AMC-186-03 — widok Nagrywane i harmonogram

Uruchom albo poczekaj na zaplanowane nagranie, otwórz w Radiu widok Nagrywane
przez `Alt+2`, wybierz właściwą stację i użyj `B` oraz `Shift+B`.

Oczekiwane:

- skróty dotyczą zaznaczonego nagrania, nawet gdy słuchana jest inna stacja;
- po zamknięciu odpowiedniej części zakładki trafiają do jej pliku.

Uwagi:
