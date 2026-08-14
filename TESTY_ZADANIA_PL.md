# Zadania testowe AMC

- Numer zestawu: `AMC-TEST-018`
- Tytuł zestawu: Odtwarzanie, trwałe wyniki wyszukiwania i szczegółowe podpowiedzi
- Wersja programu: `0.1.0-alpha.18`
- Utworzono: 2026-08-14 16:12:39, Europe/Warsaw
- Plik wyników: `wyniki-testow/WYNIKI_2026-08-14_161239_0.1.0-alpha.18.md`

Najważniejsze są zadania 1–5. Wpisuj wynik przy każdym podpunkcie, zwłaszcza gdy NVDA mówi coś więcej niż tekst oczekiwany.

## AMC-018-01 — Enter przełącza odtwarzanie i pauzę

1. Uruchom TIDAL i widok **Teraz odtwarzane**.
2. Wybierz „Brzeg ciszy” i naciśnij Enter trzy razy, czekając za każdym razem na komunikat.
3. Przejdź do „Ciepłego deszczu” i naciśnij Enter.
4. Na „Ciepłym deszczu” naciśnij dwa razy `Ctrl+Enter`.

Oczekiwane wyniki:

- kolejne zwykłe naciśnięcia Enter na „Brzegu ciszy” mówią kolejno: „Odtwarzanie”, „Pauza”, „Odtwarzanie”;
- Enter na innym utworze uruchamia ten utwór, zamiast pauzować poprzedni;
- `Ctrl+Enter` zawsze oznacza „Odtwórz teraz”, więc nie przełącza na pauzę;
- po każdym komunikacie fokus pozostaje na właściwym elemencie listy.

## AMC-018-02 — Działania nie zamykają wyników wyszukiwania

1. Naciśnij `Ctrl+F`, wpisz `Ciepły deszcz` i naciśnij Enter.
2. Naciśnij kolejno `Ctrl+Enter`, `Shift+Enter`, `Ctrl+Shift+Enter` i `Ctrl+Shift+U`.
3. Po każdym działaniu sprawdź strzałkami, czy nadal jesteś na liście wyników.
4. Naciśnij `Alt+Enter`, zamknij informacje i sprawdź fokus.
5. Otwórz menu kontekstowe, zamknij je Escape i sprawdź fokus.
6. Dopiero zwykłym Enter otwórz wynik i wróć do głównej listy.

Oczekiwane wyniki:

- żadne działanie z modyfikatorem nie zamyka okna wyszukiwania;
- fokus pozostaje na „Ciepłym deszczu”;
- komunikaty kolejki, „jako następne” i Ulubionych zawierają nazwę „Ciepły deszcz”;
- `Alt+Enter` pokazuje właściwy element, a po zamknięciu informacji wraca do wyniku;
- menu kontekstowe nadal odczytuje skróty;
- dopiero zwykły Enter zamyka wyszukiwanie, przechodzi do głównej listy i zaznacza wynik bez jego automatycznego odtworzenia.

## AMC-018-03 — Wyszukiwanie globalne podaje usługę

1. W TIDAL-u naciśnij `Ctrl+Shift+F` i wyszukaj `Zielony horyzont`.
2. Wybierz wynik z Apple Music i naciśnij `Ctrl+Enter`.
3. Sprawdź komunikat i użyj strzałek.
4. Naciśnij Escape.

Oczekiwane wyniki:

- komunikat zawiera „Odtwarzanie: Zielony horyzont” oraz „Apple Music”;
- po `Ctrl+Enter` wyszukiwanie pozostaje otwarte i fokus jest na wyniku Apple Music;
- Escape wraca do głównej listy;
- główna sesja to Apple Music, a „Zielony horyzont” pozostaje zaznaczony i odtwarzany.

## AMC-018-04 — Krótkie i szczegółowe podpowiedzi

1. Otwórz **Ustawienia**, kartę **Komunikaty** i sprawdź opcję **Pokazuj szczegółowe podpowiedzi klawiatury przy polach i listach**.
2. Przy opcji wyłączonej użyj `Ctrl+K`, `Ctrl+F` i `Ctrl+Shift+F`.
3. Włącz opcję, zapisz ustawienia i powtórz te trzy próby.
4. Ponownie wyłącz opcję i zapisz.

Oczekiwane wyniki:

- opcja jest domyślnie wyłączona;
- w trybie krótkim NVDA nie powtarza przy każdym wyniku instrukcji o Enterze, Escape i wszystkich skrótach;
- w trybie szczegółowym filtr i oba zakresy wyszukiwania otrzymują pełne podpowiedzi;
- ustawienie działa wspólnie dla wszystkich tych wariantów i pozostaje zachowane po ponownym otwarciu ustawień.

## AMC-018-05 — Escape oraz Alt+F4

1. Otwórz wyszukiwanie przez `Ctrl+F`, a następnie naciśnij Escape.
2. Otwórz wyszukiwanie ponownie i naciśnij `Alt+F4`.
3. W głównym oknie naciśnij `Alt+F4`.

Oczekiwane wyniki:

- Escape zamyka tylko wyszukiwanie i wraca na główną listę;
- `Alt+F4` w wyszukiwaniu również zamyka tylko aktywne okno podrzędne — jest to standardowe zachowanie Windows;
- `Alt+F4` w głównym oknie zamyka całą aplikację;
- żaden z tych skrótów nie pozostawia niewidocznego okna ani procesu blokującego prefiks.

## AMC-018-06 — Brak wyników po skróceniu podpowiedzi

1. Przy wyłączonych szczegółowych podpowiedziach wyszukaj nieistniejący tekst.
2. Sprawdź komunikat, zaznaczenie tekstu i Escape.

Oczekiwane wyniki:

- nadal słychać „Brak wyników. Zmień wyszukiwany tekst”;
- tekst pozostaje zaznaczony do poprawy;
- wyłączenie instrukcji klawiszowych nie wyłącza ważnych komunikatów o wyniku i błędzie;
- Escape wraca do listy głównej.

## Następne funkcje po tym zestawie

Po zatwierdzeniu alpha.18 następnym małym etapem pozostaje dostępna paleta poleceń pod `Ctrl+Shift+K`. W dalszym planie są nadal: niskopoziomowy konfigurowalny prefiks i próby z `Ctrl+Numeryczny Enter`, instalator i bezpieczne aktualizacje komponentów, AMC.Host, WiiM jako pierwszy prawdziwy adapter oraz logowanie OAuth do usług.
