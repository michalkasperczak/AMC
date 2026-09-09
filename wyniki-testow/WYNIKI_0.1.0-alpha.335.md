# AMC alpha.335 — przełączanie TIDAL i dokładniejsza diagnoza próbek

Można testować opisowo w rozmowie. Uwagi wpisuj po dwukropku.
Nowej wersji nie uruchamiano automatycznie. Nie zamykaj poprzedniej w trakcie nagrywania.

## Zmiany

- Usunięty zbędny reset przed SDK load; Stop nadal resetuje odtwarzacz.
- Równoległe przygotowanie poświadczeń i silnika zamiast szeregowego startu.
- Ciche pomiary etapów przełączania w logu i rzeczywisty powód próbki.
- Ogólny błąd SDK nie jest już automatycznie uznawany za brak praw aplikacji.
- TIDAL Connect nie został wdrożony; wymaga dostępu partnerskiego. Wyjścia
  Windows i istniejące sterowanie WiiM nie są kontrolerem TIDAL Connect.

## Krótkie testy

1. Uruchom znany utwór. Czy próbka gra? Zanotuj komunikat o powodzie próbki.
Uwagi: 

2. Przełącz kolejno kilka utworów Page Up / Page Down. Czy czas oczekiwania
   zmienił się względem 334? Jeśli znów długo czeka, zapisz orientacyjną godzinę;
   w logu będzie można rozróżnić oczekiwanie w AMC i wewnątrz SDK.
Uwagi: 

3. Przełącz szybko kilka razy, potem zatrzymaj. Starszy utwór nie powinien
   samoistnie wracać. Po ponownym otwarciu mają działać Spacja i przewijanie.
Uwagi: 

4. Poczekaj do końca próbki w Kolejce. Pełny utwór ma pozostać w Kolejce;
   próbka ani błąd nie mogą uruchomić samoczynnie następnych elementów.
Uwagi: 

## Weryfikacja automatyczna

- 10 testów JS: zakończenia/error/skip, odmowa odtwarzania, stare błędy,
  pauza, Stop, wznowienie, brak podwójnego resetu, najnowsze polecenie,
  pomiary bez poświadczeń oraz przekazanie powodu i długości próbki.
- Test C#: rozdzielenie powodów próbki, brak zgadywania z S3016,
  redakcja danych i zachowanie Kolejki.
- Pełny zestaw Core i Windows: przeszedł; kompilacja bez błędów i ostrzeżeń.
- Odsłuch z kontem TIDAL i NVDA oraz porównanie szybkości wymagają testu
  użytkownika; symulowane SDK nie mierzy szybkości rzeczywistej usługi.

## Gotowy pakiet

`publish\AccessibleMediaController-0.1.0-alpha.335.zip`

SHA-256: `61F7FFBFB37B61479D3D6570784765C3694D7C42889A2BA635C53D4ABF574D6B`

Sprawdzono 22 pliki wewnątrz folderu wersji, zgodność sumy EXE i mostka JS.
Pozostawiono uruchomioną wersję 334 bez ingerencji. Wersja 335 nie była
uruchamiana na profilu użytkownika; nie opublikowano jej w GitHub Releases.
