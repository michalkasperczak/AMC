# Wyniki testów AMC 0.1.0-alpha.182

## Weryfikacja automatyczna

- Kompilacja Release: OK, 0 ostrzeżeń i 0 błędów.
- Testy rdzenia: OK w konfiguracji Release.
- Testy Windows: OK w konfiguracji Release.
- Segmentowe wpisywanie daty i czasu: OK; `2310` ustawia `23:10`, `04`
  ustawia kwiecień, rok przyjmuje cztery cyfry, a ukończenie poprawnej części
  przechodzi do następnej.
- Zakresy: OK; błędna godzina nie zmienia czasu, a zmiana miesiąca dopasowuje
  dzień do końca krótszego miesiąca.
- Klawiatura: OK dla górnego rzędu cyfr i bloku numerycznego; nieukończony
  segment wygasa po dłuższej przerwie.
- Dostępność: OK; pola mają użytkowe nazwy i opisy wpisywania cyfr dla NVDA,
  bez technicznych nazw kontrolek.
- Dotychczasowe testy Radia, harmonogramów, nagrywania, Shazam, ustawień
  dźwięku, odtwarzania lokalnego i dekoderów: OK.
- Publikacja samowystarczalnego programu dla `win-x64`: OK.

## Pakiet

- Program: `publish\AccessibleMediaController-0.1.0-alpha.182\AccessibleMediaController-0.1.0-alpha.182.exe`.
- Wersja produktu: `0.1.0-alpha.182`.
- Rozmiar programu: `165958048` bajtów.
- SHA-256: `4E2D89113826CAC780AD9AD918D5B69B96B6A303336D748DF24A298E893501B0`.

## Test ręczny

### AMC-182-01 — pełna godzina bez dwukropka

Otwórz `Shift+R`, wybierz późniejsze rozpoczęcie i przejdź do pola godziny.
Od części godzin wpisz bez strzałek `2310`.

Oczekiwane:

- po `23` program przechodzi automatycznie do minut;
- kolejne `10` ustawia minuty;
- całe pole ma wartość `23:10`;
- NVDA oznajmia użytkową część i wartość, bez nazwy klasy kontrolki.

Uwagi:

### AMC-182-02 — pełna data jednym ciągiem

W polu daty ustaw się na dniu i wpisz kolejno `04092026`, bez używania
strzałek między częściami.

Oczekiwane:

- `04` ustawia dzień i przechodzi do miesiąca;
- `09` ustawia wrzesień i przechodzi do roku;
- `2026` ustawia rok;
- wynik to `04.09.2026`.

Powtórz ten sam test cyframi z bloku numerycznego.

Uwagi:

### AMC-182-03 — błąd i dotychczasowe strzałki

W części godziny wpisz `29`.

Oczekiwane:

- AMC podaje, że godzina jest nieprawidłowa i zakres wynosi od `00` do `23`;
- poprzednia godzina pozostaje bez zmiany;
- następne prawidłowe dwie cyfry rozpoczynają nową wartość;
- lewo i prawo nadal wybiera część, a góra i dół nadal zmienia jej wartość.

Uwagi:
