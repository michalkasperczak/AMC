# PERF-001 — duże listy i odtwarzanie według zgłoszeń Huberta

## Status

W trakcie. PR #6 zastosowany jako lokalny kandydat w bieżącej gałęzi AMC Test.
Testy celowane 65/65 zaliczone. Nie ma nowego commitu, pakietu, instalacji ani
wydania AMC Public. PR #6 na GitHubie pozostaje otwarty.

## Materiał do dokumentacji użytkownika

Po przyjęciu poprawki bardzo duża lista Plików lokalnych nie powinna co sekundę
przeliczać wszystkich nazw, gdy żaden wiersz ani filtr się nie zmienił. Strzałki,
pauza i wznowienie mają zachować dotychczasowy fokus i odczyt NVDA. Zmiana
filtra oraz nowy stan odtwarzania nadal aktualizują odpowiednie pozycje.

Przełączenie na widok „Wszystkie pliki” i powrót Escape z odtwarzacza wymagają
osobnej poprawki. W obecnej kontrolce lista dziesiątek tysięcy pozycji może
zablokować interfejs i NVDA na wiele sekund. Użytkownik nie powinien traktować
PR #6 jako rozwiązania tych dwóch sytuacji.

## Uzasadnienie i decyzje

- 30.10.2026: PR #2 (nazwy i tytuły radia) i PR #3 (migracja prędkości 1×)
  są już w bieżącym kodzie: commity 239c7e8, 32fb6c0, f1c0934 i c36c833
  są przodkami HEAD. Są także opisane jako wydane w AMC Public 29.10.2026,
  19:24. Nie należy stosować ich powtórnie.
- 30.10.2026: PR #6 (commit Huberta 583dd644) zachowuje zwykłą listę wxPython.
  Pomija przeliczanie przy niezmienionej liście i filtrze, przechowuje tekst
  niezmienionych obiektów wierszy oraz liczy stan odtwarzania dopiero po
  zmianie danych lub transportu. Założenie o podmianie listy sprawdzono:
  produkcyjny kod przypisuje `model.rows` tylko w `ListModel.replace`.
- 30.10.2026: kod PR #6 nałożył się bez konfliktu na aktualną gałąź
  `codex/konfigurowalne-skroty-20261026`. Zmiany kodu PR są w drzewie roboczym, bez dodania do indeksu Git.
  Nie łączono PR-u na GitHubie; inne, już istniejące zmiany robocze w tym
  katalogu pozostawiono bez ingerencji.
- 30.10.2026: dwie stare próby GUI w `test_native_list_apply.py` nie działały
  na prawdziwym wxPython, bo przypisywały obiektowi atrapy nową klasę z innym
  układem C++. Atrapa teraz na czas testu podmienia typ rozpoznawany przez
  handler, a fixture przywraca go po próbie. Naprawa testu jest lokalna,
  jeszcze niezatwierdzona.
- Zgłoszenie #5 Huberta dotyczy innej części kosztu: 42 866 natywnych wierszy
  przy wypełnianiu widoku i obsłudze UI Automation po Escape. Zalecany następny
  etap to osobny prototyp dużego widoku z ograniczeniem liczby natywnych
  wierszy lub kontrolką wirtualną, porównany z obecnym zachowaniem NVDA.
  Samo pozostawienie listy widocznej w odtwarzaczu usuwa objaw Escape w
  eksperymencie Huberta, ale zmienia układ i nawigację, więc wymaga odbioru.

## Testy i wynik

- Środowisko: `D:\Projekty Codex\AMC - zależności\python-test-envs\wxpython-4.3.1-cp314\Scripts\python.exe`, pytest 9.0.2, bez okien.
- Polecenie: `python -m pytest -q -p no:cacheprovider tests/test_list_sync_plan.py tests/test_native_list_apply.py tests/test_playback_activity.py` z katalogu `wxlite`, bez zapisu bytecode.
- Pierwszy bieg na przypiętym środowisku: 63 zaliczone, 2 błędy wcześniejszych
  atrap testowych. Po ich naprawie: 65 zaliczonych, 0 błędów.
- `git diff --check` i kontrola pięciu plików PR #6: bez błędów formatowania.
- Nie wykonano testu żywego interfejsu z NVDA ani pomiaru na 42 866 plikach;
  wyniki takiego pomiaru w PR #6 są relacją Huberta, nie naszym pomiarem.

## Znane ograniczenia

- PR #6 nie skraca inicjalnego wstawienia dziesiątek tysięcy natywnych wierszy
  ani odbudowy UI Automation po powrocie z odtwarzacza.
- Przy zmianie architektury listy trzeba ponownie sprawdzić NVDA: pierwszy
  fokus, strzałki, filtr, zaznaczenie wielu pozycji, Enter, Escape, powrót
  fokusu i ponowne otwarcie. Sama poprawa czasu nie wystarcza.
- Stan roboczy repozytorium zawiera równolegle inne modyfikacje. Przed commitem
  i budową wydania należy wyodrębnić dokładny zakres PERF-001.

## Wersja i commit

Podstawa bieżącej gałęzi przy zastosowaniu: `89a0dcbf178f16f5c1624a1476134c8404dfeb95`.
Źródło poprawki: PR #6, `583dd644ab7dc495c17e849f16cf336a15bdb4de`.
Nowy commit: brak. Build: brak. Instalacja: brak. Publikacja: brak.

## Źródła

- PR #6: https://github.com/michalkasperczak/AMC/pull/6
- Zgłoszenie #5: https://github.com/michalkasperczak/AMC/issues/5
- Wcześniejsze PR: https://github.com/michalkasperczak/AMC/pull/2 i https://github.com/michalkasperczak/AMC/pull/3
