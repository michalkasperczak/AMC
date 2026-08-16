# Zadania testowe AMC

- Numer zestawu: `AMC-TEST-029`
- Tytuł zestawu: Dostępna paleta poleceń
- Wersja programu: `0.1.0-alpha.29`
- Utworzono: 2026-08-16 19:10:09, Europe/Warsaw
- Plik wyników: `wyniki-testow/WYNIKI_2026-08-16_191009_0.1.0-alpha.29.md`

W pliku wyników po dwukropku wpisz `OK`, jeżeli zadanie działa, albo krótko opisz problem. Nie trzeba powtarzać wariantów odpowiedzi przy każdym zadaniu.

Alpha.29 uruchamia paletę wszystkich poleceń pod `Ctrl+Shift+K`. Paleta filtruje podczas pisania, pokazuje skróty aktywnego profilu po prefiksie i pozwala wykonywać również polecenia, które nie mają osobnego skrótu.

## AMC-029-01 — Otwarcie, fokus i wykonanie polecenia

1. Na głównej liście naciśnij `Ctrl+Shift+K`.
2. Sprawdź, gdzie znajduje się fokus i co odczytuje NVDA.
3. Wpisz `ulubione`.
4. Naciśnij strzałkę w dół i wybierz polecenie „Pokaż ulubione”.
5. Naciśnij Enter.

Oczekiwane wyniki:

- otwiera się okno „Paleta poleceń — AMC”, a fokus jest w polu „Filtr poleceń”;
- wpisywanie nie przenosi fokusu i od razu zawęża listę;
- strzałka w dół przechodzi na natywną listę poleceń;
- wiersz zawiera nazwę polecenia oraz „prefiks U”;
- Enter zamyka paletę, otwiera Ulubione i ustawia fokus na głównej liście.

## AMC-029-02 — Wyszukiwanie bez polskich znaków

1. Otwórz paletę przez `Ctrl+Shift+K`.
2. Wpisz dokładnie `czas pozostaly`, bez litery „ł”.
3. Jeżeli pozostało jedno właściwe polecenie, naciśnij Enter bez przechodzenia na listę.

Oczekiwane wyniki:

- paleta znajduje „Czas pozostały” mimo braku polskiego znaku;
- Enter bezpośrednio z pola wykonuje zaznaczone polecenie;
- paleta się zamyka, a AMC podaje pozostały czas tylko raz.

## AMC-029-03 — Nawigacja między polem i listą

1. Otwórz paletę i wpisz `sesja`.
2. Naciśnij strzałkę w dół, a następnie poruszaj się po wynikach strzałkami.
3. Na pierwszym wyniku naciśnij strzałkę w górę.
4. Ponownie przejdź na listę i zacznij wpisywać literę lub krótki fragment nazwy.

Oczekiwane wyniki:

- NVDA czyta kolejne polecenia i ich pozycje bez nakładającego się komunikatu o liczbie wyników;
- strzałka w górę z pierwszego wyniku wraca do pola filtra;
- rozpoczęcie pisania na liście przenosi wpisywany tekst do filtra i odświeża wyniki;
- fokus nie ginie poza oknem palety.

## AMC-029-04 — Brak wyniku i Escape

1. Otwórz paletę i wpisz `polecenie którego nie ma 029`.
2. Naciśnij Enter.
3. Następnie naciśnij Escape.

Oczekiwane wyniki:

- widoczny jest stan „Brak pasujących poleceń”;
- Enter podaje krótko „Brak polecenia do wykonania” i pozostawia fokus w filtrze;
- Escape zamyka paletę i jednoznacznie przywraca fokus do głównej listy;
- aplikacja nie zamyka się i nie wykonuje przypadkowego polecenia.

## AMC-029-05 — Doprecyzowanie Enter i Ctrl+Enter

1. Na głównej liście wybierz utwór, który obecnie nie jest odtwarzany, i naciśnij Enter.
2. Nie zmieniając zaznaczenia, naciśnij Enter drugi raz, a następnie trzeci raz.
3. Naciśnij `Ctrl+Enter` dwa razy.
4. Otwórz wyszukiwanie, zwykłym Enterem otwórz wynik na głównej liście i sprawdź Enter na tym elemencie.

Oczekiwane wyniki:

- pierwszy Enter na nowym utworze mówi „Odtwarzanie”;
- drugi Enter na tym samym odtwarzanym utworze rzeczywiście włącza pauzę, a trzeci wznawia;
- każde `Ctrl+Enter` mówi „Odtwarzanie” i nigdy nie przełącza na pauzę;
- zwykły Enter na wyniku wyszukiwania tylko otwiera go na liście; późniejszy Enter stosuje powyższą zasadę zależnie od faktycznego stanu tego utworu.

## Następny etap

Po zatwierdzeniu palety kolejnym etapem pierwszej fazy będzie niskopoziomowe, konfigurowalne przechwytywanie prefiksu oraz test kandydatów z NVDA, JAWS-em i menedżerami schowka. Osobną decyzją ustawień listy pozostaje możliwość całkowitego wyłączenia pola czasu trwania.
