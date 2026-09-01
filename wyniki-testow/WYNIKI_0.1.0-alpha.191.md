# Wyniki testów AMC 0.1.0-alpha.191

Wersja naprawia zapis i ponowną rejestrację globalnego prefiksu, w tym
automatyczne rozpoznanie starszego zapisu domyślnej kombinacji z myślnikami.

## Wyniki użytkownika


## Weryfikacja automatyczna

- Kompilacja Release: zaliczona, 0 ostrzeżeń i 0 błędów.
- Testy rdzenia: zaliczone, w tym starszy zapis z myślnikami, kanoniczny zapis
  z plusami oraz bezpieczny powrót z pustej wartości do prefiksu domyślnego.
- Testy Windows: zaliczone, w tym dostępne przechwytywanie prefiksu i ochrona
  natywnego haka klawiatury.
- Kontrola prawdziwego uruchomienia: `alpha.191` wczytała 2703 rekordy
  Biblioteki oraz 3 foldery. Stan został zapisany jako
  `Ctrl+Alt+Windows+F12`; po uruchomieniu nie pojawił się nowy błąd rejestracji
  prefiksu. Zautomatyzowane sterowanie prawdziwym oknem nie zostało wykonane,
  ponieważ dwukrotnie wygasła systemowa zgoda na przejęcie aplikacji.

## Uwagi

- Program uruchomiono do testów użytkownika.
- Pakiet: `publish/AccessibleMediaController-0.1.0-alpha.191`.
- SHA-256 pliku EXE:
  `DD66F000439AF2DDC9DB0513192CA7884C29EBBC7281EED0EAAAF8F6095F0BC6`.
