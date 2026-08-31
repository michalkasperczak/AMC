# Wyniki testów AMC 0.1.0-alpha.185

## Zakres korekty

- Etykieta wiersza harmonogramu rozpoczyna się od „włączone” albo „wyłączone”.
- Dalej NVDA otrzymuje nazwę stacji, termin i dotychczasowe parametry planu.
- Komunikat po Spacji nie zawiera słowa „harmonogram”, opisu „pole wyboru” ani
  instrukcji „Wybierz Zapisz”.
- Fokus nadal pozostaje na tym samym wierszu listy.

## Weryfikacja automatyczna

- Kompilacja Release: OK, 0 ostrzeżeń i 0 błędów.
- Testy rdzenia i Windows: OK. Test dostępności działa na pokazanym oknie i
  sprawdza rzeczywisty fokus, kolejność etykiety oraz brak usuniętych zwrotów.
- Publikacja samowystarczalnego programu dla `win-x64`: OK.

## Pakiet

- Program: `publish\AccessibleMediaController-0.1.0-alpha.185\AccessibleMediaController-0.1.0-alpha.185.exe`.
- Wersja produktu: `0.1.0-alpha.185`.
- Rozmiar programu: `165958048` bajtów.
- SHA-256: `ED51CB92AFE53FFB067A5AC6D26C24B7D0EBF7555AA774291875E48B390C1F25`.

## Test ręczny

Otwórz listę harmonogramów, wybierz plan i naciśnij Spację dwa razy.

Oczekiwane:

- NVDA zaczyna od „wyłączone, nazwa stacji…” albo „włączone, nazwa stacji…”;
- następnie czyta termin i pozostałe parametry planu;
- nie dodaje numeru harmonogramu, „pole wyboru” ani instrukcji o przycisku
  Zapisz;
- fokus i przewinięcie pozostają na zmienianym wierszu.

Uwagi:
