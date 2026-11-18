# PODCAST-COPY-20261118 — opis i linki pod Ctrl+C

Status: wdrożone w źródłach i zintegrowane; odbiór Windows/NVDA niewykonany.
Jawne zlecenie Michała z 18.11.2026, po zakończeniu
zmiany zaznaczenia po Ctrl+Shift+L. Nie wznawia testów odtwarzania Apple.

## Cel i decyzja użytkownika

Na odcinku Podcastów `Ctrl+C` ma kopiować tytuł i pełny tekst opisu,
a następnie adresy w tej kolejności:

1. Publiczna strona odcinka.
2. Bezpośredni adres pliku audio — ten sam, który udostępnia `Ctrl+Shift+C`.
3. Publiczna strona podcastu, na końcu.

Nie zamieniać strony podcastu na kanał RSS ani pliku audio na stronę WWW.
Nie generować brakujących adresów z tytułów. Pomijać nieobecne pola.
Zachować pełną treść opisu i dotychczasowe zaznaczenie użytkownika.
Przy kilku zaznaczonych odcinkach kopiować osobne bloki w kolejności listy.
`Ctrl+Shift+C` pozostaje bez zmian.

## Zakres i granice

Bieżącym produktem jest AMC Python/wxPython. Historyczny formatter C# może
posłużyć jako wzorzec; nie należy poprawiać wycofanego interfejsu WPF.
Na początku kod `LiteFrame._copy_name` i `SearchDialog._copy` w Pythonie
kopiował same tytuły. Trzeba sprawdzić i połączyć rzeczywiste ścieżki, a nie
zmienić wyłącznie nieużywany formatter lub opis pomocy.

Zmiana dotyczy odcinków Podcastów, w tym ich zapisanych wyników wyszukiwania.
Pozostałe sesje i istniejący wybór adresów YouTube pozostają nietknięte.
Nie zmieniać zachowania pobierania, odtwarzania, baz danych ani profili.
Opis okna Alt+D i odczyt listy nie są nowym celem tej zmiany.

Bez instalacji/zamykania AMC na głównym PC, lokalnych Windows buildów,
instalatorów, zmian AV i publikacji nowego wydania. Nie używać płatnych API.
Kod wykonywany przez bieżący openai-codex; nieobecność osobnej binarki CLI
nie oznacza zmiany dostawcy wykonującego tę sesję.

## Baza i praca

Repozytorium: `/home/michal/projekty/AMC`.
Baza: `031349fedb92a96975d5e4895f843160d3db3d89`.
Gałąź główna: `codex/amc-test-public-20261109`, początkowo czysta.
Implementacja w izolowanym worktree `codex/amc-podcast-copy-20261118`.
Przed integracją przejrzeć diff i sprawdzić konkretny wynik produkcyjnej drogi.

## Kryteria odbioru

- Rzeczywisty RED na obecnym kodzie, potem GREEN.
- Produkcyjny handler z kontrolowaną bazą SQLite, nie sam tekst oczekiwania.
- Kolejność opis → strona odcinka → plik → strona podcastu.
- Brakujące opis/adresy nie produkują pustych etykiet ani zgadywanych łączy.
- Przy kilku elementach zachowana kolejność i rozdzielenie bloków.
- `Ctrl+Shift+C`, zwykłe kopiowanie w innych sesjach i YouTube bez regresji.
- Wspólna implementacja dla dróg do tej samej funkcji.
- Pomoc, paleta, menu i instrukcja zgodne z faktycznym działaniem.
- Jeden proporcjonalny przebieg testów; bez kolejnego pełnego audytu.

## Testy i wynik

Przygotowano kandydata `605667cf868853d2dc1525b04cb2109c988b3537`
w izolowanym worktree. Wykonawca uruchomił 30 funkcji testowych własnym
harnessem, a nie standardową kolekcją pytest; nie utożsamiać tych trybów.
Rodzic wykonał standardowy celowany przebieg przez istniejące `uv`:

```
PYTHONPATH=wxlite:wxlite/tests uv run --python 3.11 --with pytest python -m pytest -q \
  wxlite/tests/test_podcast_clipboard.py \
  wxlite/tests/test_multiple_selection_clipboard.py \
  wxlite/tests/test_clipboard_message_matches_data.py \
  wxlite/tests/test_search_gui_navigation.py
```

Wynik rzeczywisty: `126 passed in 1.59s`. Obejmuje nowy formatter+handler,
zwykłe kopiowanie, komunikaty i sąsiednią nawigację wyszukiwania.
Pierwsza próba miała błędną nazwę pliku testowego (exit 4, brak kolekcji),
co naprawiono przed tym przebiegiem; nie jest produkcyjnym RED.
Niezależne review `deleg_e28c1e18` wykazało brak pustego wiersza między
blokami w SearchDialog i komunikaty obiecujące szczegóły także dla nazw
nagłówków/YouTube. Brak uwag bezpieczeństwa. Uwagi odtworzono w dodatkowym
RED: `4 failed, 3 passed`, następnie poprawiono bez rozszerzania zakresu.
Menu, paleta i Ctrl+F1 korzystają teraz z jednego opisu warunkowego w
`shortcuts.describe_action`; komunikat zależy od faktycznie skopiowanych danych.
Po poprawce ten sam celowany zestaw: `128 passed in 1.26s`.
Nowy commit `d7627f2ebe458da576186765243ab1eccbd97be7` obejmuje poprawki
review. Po sprawdzeniu rozłączności zmian dokumentacyjnych wykonano fast-forward
głównego checkoutu i porównano bajty wszystkich 5 plików przychodzących z Gitem.
Krótki test produkcyjnego handlera po integracji: `7 passed in 1.05s`.
Logi RED/GREEN są w `.test-logs/` izolowanego worktree, nie w profilu użytkownika.
Testy bez okien nie są dowodem działania schowka Windows ani odczytu NVDA.

## Uzasadnienie i decyzje

Michał chce jednym zwykłym kopiowaniem uzyskać opis oraz komplet przydatnych
adresów, z najważniejszymi dla odcinka przed ogólną stroną audycji. Osobny
skrót do samego adresu pliku nadal ma być szybki i niezmieniony.

## Materiał do dokumentacji użytkownika

Na liście odcinków w Podcastach naciśnij `Ctrl+C`, aby skopiować tytuł,
pełny opis, stronę odcinka, bezpośredni adres pliku audio i na końcu stronę
podcastu. AMC pomija brakujące adresy. Kilka zaznaczonych pozycji to osobne
bloki w kolejności listy. `Ctrl+Shift+C` nadal kopiuje sam adres.
Potwierdzone testami produkcyjnych handlerów, nie informacja o instalacji.

## Wersja i commit

Brak nowego pakietu, instalacji i publikacji. Commit zmiany:
`d7627f2ebe458da576186765243ab1eccbd97be7` (kandydat początkowy `605667c`).

## Następny krok

Przejść do uzgodnionego zadania `SEARCH-SCOPE-20261118`: Ctrl+F Radio
Archiwisty i wybór usług wyszukiwania globalnego. Rzeczywisty schowek Windows
i NVDA pozostają osobną bramką, nie zastępować ich zielonym testem headless.
