# Zadania testowe AMC

- Numer zestawu: `AMC-TEST-015`
- Tytuł zestawu: Powrót z wyszukiwania, fokus po Delete i rozbudowana lista
- Wersja programu: `0.1.0-alpha.15`
- Utworzono: 2026-08-13 21:11:09, Europe/Warsaw
- Plik wyników: `wyniki-testow/WYNIKI_2026-08-13_211109_0.1.0-alpha.15.md`

Najważniejsze są trzy pierwsze zadania. Skrótów menu kontekstowego i globalnego prefiksu nie trzeba ponownie sprawdzać.

W lokalnie zainstalowanym dodatku **NVDA global commands extension 14.1.2** funkcja **Clipboard command announcement** przechwytuje `Ctrl+Z`, wypowiada „Undo”, a następnie przekazuje klawisz aplikacji. Jest to pierwszy podejrzany w diagnostyce zadania AMC-015-04.

## AMC-015-01 — Escape po wyszukiwaniu

1. Otwórz zwykły widok, na przykład Bibliotekę.
2. Naciśnij `Ctrl+F`, a następnie `Escape`.
3. Sprawdź, czy NVDA podaje listę multimediów oraz zaznaczony element.
4. Powtórz po `Ctrl+Shift+F`.

Oczekiwany wynik: `Ctrl+F` podaje „Wyszukiwanie”, a `Ctrl+Shift+F` — „Szukaj we wszystkich usługach”. Jedno naciśnięcie `Escape` wraca do wcześniejszej listy. NVDA podaje „Lista multimediów” i aktualny element, a fokus pozwala od razu nawigować strzałkami.

## AMC-015-02 — Delete bez utraty fokusu

1. W zwykłym widoku, w którym usuwanie nie jest dostępne, naciśnij `Delete`.
2. Po komunikacie użyj strzałki w dół i otwórz menu kontekstowe.
3. Przejdź do Ulubionych skrótem `Ctrl+U`, usuń element klawiszem `Delete`, a potem przywróć go przez `Ctrl+Z`.
4. Powtórz w Bibliotece (`Ctrl+L`) i Kolejce (`Ctrl+Q`).

Oczekiwany wynik: w zwykłym widoku słychać „Usuwanie jest dostępne tylko w widokach Ulubione, Biblioteka i Kolejka”, ale fokus pozostaje na elemencie listy i nie przechodzi do menu Plik. W trzech obsługiwanych widokach element znika, fokus pozostaje na liście, a `Ctrl+Z` przywraca zmianę.

## AMC-015-03 — Nawigacja literami na liście 17 elementów

1. W zwykłym widoku sprawdź, czy lista zawiera 17 zróżnicowanych elementów.
2. Naciśnij szybko `Z`, `I`, `E`, aby przejść do „Zielonego horyzontu”.
3. Naciskaj wielokrotnie `B`, aby przechodzić między „Brzegiem ciszy” i „Błękitną godziną”.
4. To samo sprawdź literą `C` dla „Ciepłego deszczu” i „Ciszy o świcie”.
5. Po krótkiej przerwie rozpocznij inną sekwencję i wróć do jednego z wcześniejszych elementów.

Oczekiwany wynik: sekwencje przechodzą do właściwych elementów, a powtarzanie jednej litery przechodzi po kolejnych pasujących pozycjach. Test zawsze zaczyna się z tym samym zestawem nazw.

## AMC-015-04 — Kontrola Ctrl+Z i możliwego konfliktu NVDA

1. Zmień stan Ulubionych skrótem `Ctrl+Shift+U`, a następnie naciśnij `Ctrl+Z`.
2. Zapisz dokładnie, czy stan został cofnięty i co powiedział NVDA.
3. Jeżeli wystąpi angielskie „Undo”, w ustawieniach dodatku **NVDA global commands extension** przejdź do kategorii **Features's installation** i ustaw **Clipboard command announcement** na **Do not install**.
4. Uruchom ponownie NVDA zgodnie z prośbą dodatku i powtórz próbę.
5. Dopiero jeżeli problem nadal występuje, powtórz test po jednorazowym uruchomieniu NVDA ze wszystkimi dodatkami wyłączonymi.

Oczekiwany wynik AMC: zmiana zostaje cofnięta. Jeżeli angielskie „Undo” znika po wyłączeniu funkcji **Clipboard command announcement**, konflikt jest jednoznacznie rozpoznany i nie wymaga dalszych zmian mechanizmu AMC.

## AMC-015-05 — Krótka regresja filtra

1. Naciśnij `Ctrl+K`, wpisz fragment nazwy i przejdź strzałką w dół do wyników.
2. Naciśnij `Escape`.

Oczekiwany wynik: filtr działa, `Escape` czyści go jednym krokiem i fokus pozostaje na liście.
