# Zadania testowe AMC

- Numer zestawu: `AMC-TEST-030`
- Tytuł zestawu: Odtwarzanie, Spacja i skróty w palecie
- Wersja programu: `0.1.0-alpha.30`
- Utworzono: 2026-08-16 21:28, Europe/Warsaw
- Plik wyników: `wyniki-testow/WYNIKI_2026-08-16_2128_0.1.0-alpha.30.md`

W pliku wyników po dwukropku wpisz krótko, co się wydarzyło. Jeżeli wszystko działa, wystarczy `OK`. Nie trzeba przed każdym zadaniem wybierać wariantu odpowiedzi.

## AMC-030-01 — Enter jako działanie podstawowe

1. Na głównej liście wybierz utwór, który nie jest odtwarzany, i naciśnij Enter trzy razy.
2. Wybierz album albo playlistę i naciśnij Enter.

Oczekiwane:

- pierwszy Enter na utworze uruchamia go, drugi wstrzymuje, a trzeci wznawia;
- Enter na albumie albo playliście otwiera zawartość zamiast przełączać pauzę.

## AMC-030-02 — Ctrl+Enter bez otwierania

1. Na utworze naciśnij `Ctrl+Enter` trzy razy.
2. Otwórz wyszukiwanie przez `Ctrl+F`, wykonaj zapytanie i na wyniku naciśnij `Ctrl+Enter` trzy razy.

Oczekiwane:

- polecenie kolejno uruchamia, wstrzymuje i wznawia zaznaczony utwór;
- w wyszukiwaniu wynik nie jest otwierany na głównej liście, a okno wyników i fokus pozostają na miejscu;
- nazwa czynności brzmi „Odtwórz lub wstrzymaj”, bez słowa „zaznaczenie”.

## AMC-030-03 — Spacja steruje tym, co gra

1. Uruchom utwór A.
2. Samą strzałką przejdź na inny utwór B, ale go nie uruchamiaj.
3. Naciśnij Spację dwa razy.
4. Naciśnij `Ctrl+Enter` na utworze B, a potem Spację.

Oczekiwane:

- pierwsza Spacja wstrzymuje utwór A, a druga go wznawia, mimo że zaznaczony jest B;
- `Ctrl+Enter` przełącza odtwarzanie na B;
- kolejna Spacja wstrzymuje B.

## AMC-030-04 — Paleta podaje oba rodzaje skrótów

1. Naciśnij `Ctrl+Shift+K`, a następnie strzałkę w dół.
2. Odszukaj „Pokaż ulubione” i „Odtwórz lub wstrzymaj”.
3. Sprawdź pełny odczyt obu wierszy przez NVDA.

Oczekiwane:

- „Pokaż ulubione” podaje `Ctrl+U` oraz „prefiks U”;
- „Odtwórz lub wstrzymaj” podaje `Ctrl+Enter`;
- nie pojawiają się techniczne nazwy `CommandId` ani zapis obiektu `CommandPaletteEntry`.

## AMC-030-05 — Pisanie z listy palety

1. Otwórz paletę i przejdź strzałką w dół na listę.
2. Wpisz literę `Z`, a następnie `U`.
3. Ponownie przejdź na listę i wpisz `X`, dla którego nie powinno być dopasowania.

Oczekiwane:

- gdy ciąg `ZU` nie pasuje, ale samo `U` pasuje, filtr zaczyna nowe wyszukiwanie od `U`;
- po znaku bez dopasowania filtr zostaje wyczyszczony i wraca pełna lista;
- komunikat „Polecenia, lista” może pojawić się przy wejściu na listę, ale nie jest powtarzany przy każdym ruchu.

## AMC-030-06 — Regresja pozostałych działań

1. Sprawdź na elemencie menu kontekstowe przez klawisz aplikacji lub `Shift+F10`.
2. Uruchom „Odtwórz lub wstrzymaj”, „Dodaj do kolejki” i „Odtwórz jako następne”.
3. Zamknij menu Escape i sprawdź fokus.

Oczekiwane:

- każda pozycja ma widoczny i czytany skrót;
- polecenia działają na zaznaczonym elemencie;
- Escape zamyka menu o jeden poziom i przywraca fokus do tego samego elementu listy.
