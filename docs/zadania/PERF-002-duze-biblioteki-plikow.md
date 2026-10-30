# PERF-002 — duże biblioteki Plików lokalnych

## Status

W trakcie. Izolowany kandydat źródłowy do następnej aktualizacji razem z
Radioarchiwistą. Bez instalacji i publikacji. Pozostaje odbiór mowy oraz
nawigacji na żywym NVDA z biblioteką o rozmiarze zgłoszonym przez Huberta.

## Materiał do dokumentacji użytkownika

W Plikach lokalnych widok „Wszystkie pliki” może pokazać dziesiątki tysięcy
utworów bez wielosekundowego wstawiania pozycji do kontrolki Windows. Wybór
pozostaje przy tym samym utworze. Filtr listy nadal zawęża wyniki; po
wyczyszczeniu filtra wraca cały widok. Strzałki, Enter i Escape zachowują
dotychczasowe znaczenie. W odtwarzaczu lista Plików pozostaje widoczna nad
kontrolkami odtwarzania, ale Tab i Shift+Tab ją omijają. Escape oddaje fokus
do wybranego utworu bez chowania i ponownego pokazywania listy.

Komunikaty użytkowe i skróty nie zostały zmienione: nazwa bieżącego widoku,
liczba wyników, tekst utworu i jego pozycja na liście nadal pochodzą z
istniejącej nawigacji i natywnej listy. Ta sekcja jest szkicem do korekty po
ręcznym odbiorze NVDA, nie opisem opublikowanego wydania.

## Uzasadnienie i decyzje

- 30.10.2026: zgłoszenie [#5](https://github.com/michalkasperczak/AMC/issues/5)
  dokumentuje 42 866 utworów, około 17 s przełączania na „Wszystkie pliki”
  oraz 10–34 s po Escape. Przyczyną są 42 866 natywnych wierszy oraz
  przebudowa UI Automation po ukryciu i pokazaniu listy. PR #6 usuwa osobny
  koszt cosekundowego przeliczania; jest już w bazie tego kandydata.
- 30.10.2026: tylko lista Plików lokalnych używa `LC_VIRTUAL`. Jej teksty
  nadal są gotowymi, użytkowymi etykietami z `ListModel`. Pierwsze
  wypełnienie 42 866 pozycji wymaga jednego `SetItemCount`. Przy pojedynczej
  zmianie tekstu odświeżany jest tylko odpowiedni indeks. Zmiana kolejności
  zeruje licznik przed nowym stanem, żeby natywny cache nie skleił starego
  tekstu z nową pozycją. Pozostałe sesje zachowują zwykłą listę i
  przyrostowy plan zmian.
- 30.10.2026: w Plikach lokalnych lista nie jest chowana przy odtwarzaczu.
  `AcceptsFocusFromKeyboard` usuwa ją z kolejności Tab w odtwarzaczu.
  Rozwiązanie jest zgodne z eksperymentem Huberta opisanym w komentarzu do
  #5; układ widocznego odtwarzacza wymaga odbioru użytkownika.
- Żaden skrót ani funkcja nie zmienia dostępności w profilu Test/Public.
  Help, menu i paleta poleceń nie wymagają nowych wpisów.

## Testy i wynik

- Przypięte środowisko: `D:\Projekty Codex\AMC - zależności\python-test-envs\wxpython-4.3.1-cp314\Scripts\python.exe`, pytest 9.0.2.
- Celowane testy bez okien (lista, filtr, Enter/Escape, odtwarzacz,
  dostępne nazwy): **159 zaliczonych**. Nowe testy sprawdzają 42 866
  pozycji, wybór, filtr, powrót do pełnej listy, zmianę tekstu i ponowne
  otwarcie bez zbędnych operacji.
- Próba na rzeczywistej, ukrytej kontrolce wxPython 4.3.1 z 42 866
  syntetycznymi utworami: pierwsze `sync_rows` **107 ms**, filtr do jednego
  wyniku **16 ms**, po wyczyszczeniu filtra ponownie 42 866 pozycji.
  `GetItemText` podał poprawny pierwszy i ostatni tytuł, wybór pozostał na
  pozycji 42 865. `CanAcceptFocusFromKeyboard` zwracał `False` w odtwarzaczu
  i `True` na liście.
- `git diff --check`: bez błędów.
- **Nie wykonano próby mowy żywego NVDA ani pomiaru UI Automation przy
  42 866 rzeczywistych plikach.** NVDA nie był uruchomiony na tym hoście;
  ukryta kontrolka nie dowodzi odczytu czytnika ani czasu po Escape.

## Znane ograniczenia

- Odbiór z NVDA musi objąć: początkowy fokus i pierwszy/ostatni utwór,
  strzałki, Shift/Ctrl wybór wielu pozycji, filtr i jego usunięcie, Enter,
  Escape, Tab/Shift+Tab w odtwarzaczu oraz powtórne otwarcie widoku. Trzeba
  sprawdzić rzeczywistą mowę, a nie tylko tekst zwracany przez wx.
- Lista Plików pozostaje widoczna obok panelu odtwarzacza. NVDA może dojść
  do niej nawigacją obiektową, mimo że Tab ją omija. Wymaga to oceny po
  ręcznej próbie.
- Test na syntetycznej bibliotece nie mierzy czasu pobierania i sortowania
  danych z rzeczywistej bazy. Pomiar 107 ms obejmuje wyłącznie synchronizację
  kontrolki i przygotowanie wierszy po `ListModel.replace`.

## Wersja i commit

Podstawa: `fba01de343ba95c0aa4b26c46fd83a5792c15cbd` (kandydat Public
z PR #6). Gałąź: `codex/perf-002-large-lists-20261030` w osobnym drzewie
`D:\Projekty Codex\AMC build worktrees\perf-002-large-lists-20261030`.
Commit kodu i testów: `3cf6fd89d6d496a0eb2820022cc7b177d19cafce`.
Build: brak. Instalacja: brak. Publikacja: brak.
