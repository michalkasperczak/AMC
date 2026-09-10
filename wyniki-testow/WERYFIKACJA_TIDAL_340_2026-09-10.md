# Weryfikacja alfa 340 — 10 września 2026

## Wynik

`build.ps1 -Publish` zakończył się kodem 0. Kompilacja Release: 0 ostrzeżeń,
0 błędów. Pełny zestaw testów Core i Windows przeszedł. Osobno przeszedł
`--tidal-interaction-smoke`, obejmujący teraz etykiety kategorii wykonawcy.

Nowe testy sprawdzają trzy różne relacje HTTP, brak pobierania innych
kategorii przy wejściu w jedną, paginację i zachowanie kolejności, pomijanie
obcych zasobów included, metadane i przynależność utworów do Ulubionych,
pustą odpowiedź, błąd 403, anulowanie i odrzucenie niepoprawnego kontenera.

Test WPF korzysta z rzeczywistych obiektów wierszy tworzonych przez fabrykę
MainWindow, ale z izolowanego okna testowego. Sprawdza początkową pozycję,
przechodzenie po trzech wierszach, Name przez UI Automation, ToString, tekst
wyszukiwania literowego, stabilność ID, ponowne otwarcie oraz odtworzenie
wiersza po wymianie zawartości listy. Sprawdza też, że zdjęcie tymczasowego
kontekstu mowy przywraca podstawową etykietę wiersza.

Zachowane testy regresji obejmują spóźnione odpowiedzi TIDAL, zachowanie
zaznaczenia przy synchronizacji, wyszukiwanie, kolejkę, mostek NVDA, podcasty,
rozdziały, radio, nagrywanie, wyjścia dźwięku, schowek i pliki lokalne.

Nie uruchamiano alfa 340 na profilu użytkownika, nie zmieniano jego kolekcji,
nie zatrzymywano AMC ani NVDA. Nadal działała alfa 339. Bez odsłuchowego
testu rzeczywistego NVDA, brajla i nowej hierarchii na koncie TIDAL; pozostają
do odbioru według instrukcji 340. Test odtworzenia wiersza jest izolowany,
nie stanowi pełnego testu Enter/Escape w rzeczywistej sesji.

## Pakiet lokalny

Plik: `publish\AccessibleMediaController-0.1.0-alpha.340\AccessibleMediaController-0.1.0-alpha.340.exe`

- ProductVersion: `0.1.0-alpha.340`.
- Rozmiar: 167237312 bajtów.
- SHA-256: `6450215BC8FE507BDC7A552C21AC2071BA62C9BB24A940B726DA4F65C51F1D7B`.
- Sprawdzono zawartość pakietu: program, wymagane biblioteki, licencje oraz
  statyczny moduł oficjalnego odtwarzacza TIDAL. Bez kont, tokenów, baz,
  harmonogramów, nagrań i logów użytkownika.
- Nie tworzono ani nie wysyłano ZIP-a do GitHub Releases.

Zmiana nie rozwiązuje ograniczenia odtwarzania TIDAL do próbek.
