# Zadania testowe AMC

- Numer zestawu: `AMC-TEST-017`
- Tytuł zestawu: Nawigacja po tytułach i bezpośrednie działania w wyszukiwaniu
- Wersja programu: `0.1.0-alpha.17`
- Utworzono: 2026-08-14 13:06:40, Europe/Warsaw
- Plik wyników: `wyniki-testow/WYNIKI_2026-08-14_130640_0.1.0-alpha.17.md`

Najważniejsze są zadania 1–4. Każdy oczekiwany rezultat jest zapisany w osobnym podpunkcie, aby można go było sprawdzać kolejno z NVDA.

## AMC-017-01 — Nawigacja literami niezależna od kolejności odczytu

1. W ustawieniach, na karcie **Listy i odczyt**, ustaw **Wykonawcę** przed **Tytułem**.
2. Wróć do widoku **Teraz odtwarzane** w sesji TIDAL; lista powinna mieć 17 pozycji.
3. Naciśnij szybko kolejno `Z`, `I`, `E`.
4. Po krótkiej przerwie naciskaj pojedyncze `B`, za każdym razem czekając na odczyt elementu.
5. Powtórz sprawdzenie z literą `C`.

Oczekiwane wyniki:

- `ZIE` przechodzi do „Zielonego horyzontu”.
- Kolejne pojedyncze `B` przechodzą między „Brzegiem ciszy” i „Błękitną godziną”.
- Kolejne pojedyncze `C` przechodzą między „Ciepłym deszczem” i „Ciszą o świcie”.
- Dopasowanie zawsze korzysta z tytułu, mimo że NVDA najpierw odczytuje wykonawcę.
- Litery nie wpisują się do filtra i nie uruchamiają poleceń.

## AMC-017-02 — Jednoznaczne otwarcie wyniku i Enter na liście głównej

1. W TIDAL-u naciśnij `Ctrl+F`.
2. Wpisz `Brzeg` i naciśnij Enter.
3. Na wyniku „Brzeg ciszy” naciśnij Enter.
4. Po powrocie do listy głównej naciśnij jeszcze raz Enter.

Oczekiwane wyniki:

- Pierwszy Enter wykonuje wyszukiwanie i przenosi fokus na wynik.
- Drugi Enter zamyka okno wyszukiwania, wraca do widoku „Teraz odtwarzane” i zaznacza „Brzeg ciszy”.
- Po powrocie element nie jest odczytywany przez dodatkowy komunikat aplikacji drugi raz.
- Trzeci Enter uruchamia wybrany utwór i podaje jednoznacznie „Odtwarzanie: Brzeg ciszy”.
- Enter nie przełącza przypadkowo pauzy na wcześniej odtwarzanym utworze.

## AMC-017-03 — Jeden wynik pozostaje do świadomego wyboru

1. Naciśnij `Ctrl+F`.
2. Wpisz `Ciepły deszcz` i naciśnij Enter.
3. Nie naciskaj jeszcze żadnego działania; sprawdź, gdzie znajduje się fokus.
4. Naciśnij Escape.

Oczekiwane wyniki:

- Po znalezieniu jednego wyniku okno nie zamyka się automatycznie.
- Fokus znajduje się na jedynym wyniku, dzięki czemu można wybrać działanie.
- Samo wyszukanie nie rozpoczyna odtwarzania.
- Escape anuluje wybór i przywraca fokus głównej liście.

## AMC-017-04 — Bezpośrednie działania na wyniku wyszukiwania

Każde działanie sprawdź osobno: ponownie otwórz `Ctrl+F`, wyszukaj `Ciepły deszcz`, a następnie użyj wskazanego skrótu na wyniku.

1. `Ctrl+Enter` — odtwórz teraz.
2. `Shift+Enter` — dodaj do kolejki.
3. `Ctrl+Shift+Enter` — odtwórz jako następne.
4. `Ctrl+Shift+U` — dodaj lub usuń z Ulubionych.
5. `Alt+Enter` — pokaż informacje.
6. Otwórz menu kontekstowe na wyniku i sprawdź nazwy oraz odczytane skróty tych działań.

Oczekiwane wyniki:

- Każdy skrót działa bez potrzeby wcześniejszego powrotu do listy głównej.
- Po `Ctrl+Enter` słychać nazwę rzeczywiście uruchomionego utworu.
- Działania kolejki, „Odtwórz jako następne” i Ulubionych podają nazwę zmienianego elementu.
- `Alt+Enter` otwiera informacje o właściwym wyniku.
- Menu kontekstowe zawiera widoczne dla NVDA skróty klawiszowe.
- Po zamknięciu menu bez wyboru fokus pozostaje na wyniku.

## AMC-017-05 — Wyszukiwanie globalne i zmiana sesji

1. Będąc w TIDAL-u, naciśnij `Ctrl+Shift+F`.
2. Wpisz `Zielony horyzont` i naciśnij Enter.
3. Wybierz wynik z Apple Music.
4. Naciśnij `Ctrl+Enter`.

Oczekiwane wyniki:

- Każdy wynik zawiera nazwę usługi.
- `Ctrl+Enter` zamyka wyszukiwanie i przełącza główną sesję na Apple Music.
- Zaznaczony i odtwarzany jest „Zielony horyzont” z Apple Music.
- Słychać jeden jasny komunikat o rozpoczęciu odtwarzania.

## AMC-017-06 — Brak wyników i anulowanie

1. Naciśnij `Ctrl+F`, wpisz tekst, którego nie ma, i naciśnij Enter.
2. Sprawdź komunikat i zaznaczenie tekstu.
3. Naciśnij Escape.

Oczekiwane wyniki:

- Słychać „Brak wyników. Zmień wyszukiwany tekst”.
- Wpisany tekst pozostaje zaznaczony do poprawy.
- Escape zamyka okno jednym krokiem.
- Fokus wraca do głównej listy bez zmiany widoku.

## Następne funkcje po tym zestawie

Jeżeli alpha.17 przejdzie testy, następnym małym etapem będzie dostępna paleta poleceń pod `Ctrl+Shift+K`. W planie nadal pozostają: niskopoziomowy konfigurowalny prefiks i test `Ctrl+Numeryczny Enter`, instalator oraz bezpieczne aktualizacje komponentów, wydzielenie AMC.Host, WiiM jako pierwszy prawdziwy adapter, a później logowanie OAuth do usług. Nie dokładamy tych tematów jednocześnie do testu alpha.17; pozostają zapisane w `MEDIA_CONTROLLER_PL.md`.
