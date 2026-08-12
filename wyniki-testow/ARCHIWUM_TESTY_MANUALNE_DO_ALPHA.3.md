# Testy ręczne AMC — etap porządkowania interfejsu

Ta lista służy do sprawdzenia wersji `0.1.0-alpha.2` po większych zmianach. Wykonaj ją z NVDA, a jeśli możesz, powtórz najważniejsze punkty bez czytnika ekranu.

Przy każdym punkcie zanotuj: **OK**, **Błąd** albo **Nie sprawdzono**. Przy błędzie dopisz ostatni usłyszany komunikat i element, na którym pozostał fokus.

## 1. Uruchomienie i początkowy fokus

1. Zamknij poprzednią wersję AMC.
2. Uruchom `AccessibleMediaController.exe`.
3. Nie naciskaj żadnego klawisza i posłuchaj pierwszego komunikatu NVDA.
4. Naciśnij strzałkę w dół.

Oczekiwany wynik: fokus znajduje się na liście elementów multimedialnych, a nie na pasku menu. NVDA odczytuje pierwszy element, a strzałka w dół przechodzi do drugiego.


Na pewno nie powinno być tak, że po uruchomieniu trzeba jakoś naciskać specjalnie strzałkę w dół, żeby się znaleźć na liście. Do tego, po nawigacji Alt-Tab i ponownym przywołaniu okna programu, fokus z reguły znajduje się na pasku menu.

Zasadniczo klawisz Tab na tym pasku menu chyba nie powinien działać.



## 2. Menu i widoki

1. Naciśnij `Alt+W`, aby otworzyć menu **Widok**.
2. Strzałkami sprawdź kolejno: Teraz odtwarzane, Ulubione, Playlisty, Biblioteka, Kolejka i Filtruj listę.
3. Wybierz **Ulubione**.

Oczekiwany wynik: menu ma logiczną kolejność, przy pozycjach są odczytywane skróty z prefiksem, a po wybraniu Ulubionych fokus wraca na listę.

Wynik: OK.

## 3. Nazwy przycisków

1. Wróć do listy i naciskaj `Tab`, aż przejdziesz przez przyciski pod listą.
2. Sprawdź, czy NVDA odczytuje: Odtwórz, Zmień stan ulubionych, Zmień playlisty i Ustawienia.

Oczekiwany wynik: nazwa każdego przycisku jednoznacznie opisuje działanie. Przycisk **Zmień stan ulubionych** zmienia stan wybranego elementu, a nie otwiera widoku Ulubione.

Wynik: Błąd. Otwiera Ulubione, przy niektórych elementach losowo mówi Pauza alnbo Odtwarzaj.


## 4. Filtrowanie

1. Naciśnij `Ctrl+F`.
2. Wpisz `Drugi`.
3. Naciśnij `Escape`.

Oczekiwany wynik: `Ctrl+F` przenosi fokus do pola filtra, wpisanie tekstu ogranicza listę, a `Escape` czyści filtr i wraca do listy bez otwierania menu.

Wynik: OK. Coś trzeba zrobić z przyciskami: odtwórz, pauza, ulubione i tak dalej, przy wyszukiwaniu.

Trudno jest dojść do właściwej listy po filtrowaniu. Naciskam CTRL+F, coś piszę, a właściwie nie wiem, gdzie jest ta lista.

Na lewy zadbać o przejrzystość, logiczne ułożenie kontrolek, żeby informacje były jak najbliżej siebie, ułożone w sposób logiczny.

Zastanawiam się też, czy te przyciski: odtwórz, dodaj do ulubionych, dodaj do playlisty i tak dalej, powinny być zawsze na wierzchu pod klawiszem Tab. Czy nie wystarczy w menu albo w menu kontekstowym?

Ale chyba na wierzchu jednak dobrze, żeby były. Warto jednak, żeby nie były w każdej sytuacji, a przy tym CTRL+F, filtrowanie – nie wiem, czy powinny być.


## 5. Ustawienia

1. Naciśnij `Ctrl+,`.
2. Przejdź po kartach ustawień za pomocą `Ctrl+Tab`.
3. Na karcie Ogólne sprawdź język i prefiks.
4. Na karcie Profile klawiatury wybierz profil i przejdź do listy przypisań.
5. Otwórz kartę Aktualizacje.

Oczekiwany wynik: zmiana języka i aktualizacje są jasno opisane jako planowane i nieaktywne. Lista skrótów używa polskich nazw poleceń, np. „Shift+F — Dodaj lub usuń z ulubionych”, a nie identyfikatorów takich jak `action.favorite.toggle`.

Wynik: Błąd. Nie mogę znaleźć języka. Sprawdź to, ale wydaje mi się, że klawisze prefiksy są źle ogłaszane, te etykiety.BindingRow { Chord = ctrl+Left, CommandId = transport.previous, Label = ctrl+Left — Poprzedni element }  15 z 50
BindingRow { Chord = ctrl+R, CommandId = information.timeRemaining, Label = ctrl+R — Czas pozostały }  16 z 50
BindingRow { Chord = ctrl+Right, CommandId = transport.next, Label = ctrl+Right — Następny element }  17 z 50
BindingRow { Chord = ctrl+0, CommandId = session.list, Label = ctrl+0 — Lista sesji }  2 z 50
BindingRow { Chord = ctrl+1, CommandId = session.slot.1, Label = ctrl+1 — Wybierz sesję 1 }  3 z 50
favorite.added  favorite.added
session.changed  session.changed

## 6. Globalny prefiks

1. Pozostaw uruchomione AMC i przejdź do innej aplikacji.
2. Naciśnij `Ctrl+Alt+Spacja`, następnie `Ctrl+2`.
3. Ponownie użyj prefiksu i naciśnij `Page Down`.

Oczekiwany wynik: AMC nie zabiera fokusu z innej aplikacji, ale NVDA odczytuje zmianę sesji. Polecenia nie wpisują znaków do aktywnego dokumentu.

Wynik: Nie przetestowane. Inny klawisz skrótu proponuję Alt-CTRL-Win-Enter. Ten ze spacją nie chciał mi działać, jakaś kolizja z NVDA.


## 7. Podstawowa regresja

Sprawdź po jednym razie:

- `Enter` na elemencie listy;
- `Alt+Enter` — informacje;
- klawisz aplikacji albo `Shift+F10` — menu kontekstowe;
- otwarcie i anulowanie okna playlist;
- otwarcie i anulowanie ustawień;
- zamknięcie programu klawiszami `Alt+F4`.

Oczekiwany wynik: żadne okno nie zawiesza się, po zamknięciu okna modalnego można dalej poruszać się po głównym oknie, a program zamyka się bez komunikatu o błędzie.

Wynik: OK.

## Informacje do zgłoszenia błędu

Jeśli coś nie działa, podaj:

- numer punktu z tej listy;
- użyty czytnik ekranu i jego wersję;
- dokładne naciśnięte klawisze;
- komunikat odczytany przez czytnik;
- miejsce, w którym pozostał fokus;
- czy błąd występuje po ponownym uruchomieniu AMC.
