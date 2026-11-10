# Radio Browser w AMC Test — szkic do korekty użytkownika

Ten tekst opisuje przygotowany kod. Nie jest jeszcze instrukcją opublikowanego
wydania: instalacja, odsłuch i odbiór NVDA pozostają do sprawdzenia.

## Szkic instrukcji

1. Przejdź do sesji Radio. Lista może być pusta; playlista nie jest wymagana.
2. Naciśnij `Ctrl+F5`, wybierz „Radio Browser” i naciśnij Enter.
   Druga pozycja, „TuneIn — niedostępny”, wyjaśnia brak integracji.
3. Wybierz kontynent, kraj i kolejność stacji. Dostępne są: alfabetycznie
   A–Z, najpopularniejsze oraz ostatnio zmienione. Każdy wybór potwierdź
   Enterem.
4. Na liście stacji Enter rozpoczyna odtwarzanie. Pozycja „Wczytaj kolejne
   stacje” pobiera następną część listy. Backspace lub Escape wraca na
   poprzedni poziom; Escape na liście kontynentów zamyka okno.

Na wybranej stacji lewa lub prawa strzałka podaje bitrate. `Ctrl+C` kopiuje
adres strumienia, `Ctrl+Shift+C` stronę stacji, `Ctrl+Shift+L` zmienia
przynależność do Biblioteki, a `Ctrl+Shift+U` do Ulubionych. Enter sam nie
zapisuje stacji. `F5` w głównym widoku Radia odświeża bieżącą listę.

## Szkic zmian

- Dodano przeglądanie katalogu Radio Browser przez kontynent, kraj i
  stronicowaną listę stacji, dostępne także przy pustej Bibliotece Radia.
- Ujednolicono `Ctrl+F5` w sesji Radio z menu, paletą poleceń i pomocą.
  Odświeżanie przeniesiono na `F5`.
- Dodano jawne akcje odtwarzania, odczytu bitrate, kopiowania adresów oraz
  Biblioteki i Ulubionych na stacjach katalogu.
- Dodano wybór serwera Radio Browser zgodny z DNS i przejście do kolejnego
  serwera, gdy pierwszy nie odpowiada.
- TuneIn jest widoczny jako niedostępny; integracja wymaga osobnego dostępu
  partnerskiego.

## Do odbioru przed wydaniem

Rzeczywiste pobranie katalogu, mowa NVDA, klawiatura i powrót fokusu,
odtwarzanie, instalacja, wersja Public oraz pełne angielskie teksty okna.
