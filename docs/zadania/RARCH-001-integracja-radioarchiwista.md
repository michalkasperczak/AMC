# RARCH-001 — sesja Radio Archiwista

- Status: **W trakcie; kod i testy gotowe w gałęzi `radioarchiwista-sesja`,
  oczekuje na pull request i ręczny odbiór na AMC Test**.
- Data: 29.10.2026.
- Profil: wyłącznie AMC Test (`session.radioarchiwista` w capabilities;
  AMC Public nie widzi sesji, dopóki się nie ustabilizuje).

## Zachowanie widoczne dla użytkownika

- Nowa sesja „Radio Archiwista" przegląda publiczny katalog nagrań
  (radioarchiwista.github.io): 43 stacje, ok. 242 tys. godzin.
- Hierarchia list: stacje → lata → miesiące → dni → godziny nagrań.
  Na górze listy stacji jest „Najnowsze nagrania": ostatnia opublikowana
  godzina ze wszystkich stacji, które ją nagrały.
- Enter na nagraniu odtwarza je przez ten sam tor co podcasty: tempo
  odtwarzania, wznowienie z zapisanej pozycji, obróbka audio. Pauza zapisuje
  pozycję.
- `Ctrl+D` pobiera zaznaczone nagranie (MP3 z archive.org) z postępem
  i możliwością anulowania; przerwane pobieranie nie zostawia pliku.
- `Ctrl+U` pokazuje ulubione nagrania (w pełni offline, zapisane w profilu),
  `Ctrl+Shift+U` przełącza ulubione, `F5` odświeża katalog, `Ctrl+F5`
  otwiera konfigurację sesji.
- Globalne wyszukiwanie AMC dokłada stacje (z ważnego cache) i ulubione
  nagrania; samo wyszukiwanie nigdy nie schodzi do sieci.
- Katalog jest publiczny: bez logowania, bez okna konta.

## Uzasadnienie i decyzje

- Nagrania to gotowe pliki, więc v1 świadomie nie steruje nagrywaniem
  (to rola serwera archiwizującego); pobieranie jest pełnoprawną funkcją.
- Nawigacja odwzorowuje shardy katalogu (stacja/rok/miesiąc/dzień), więc
  każdy widok pobiera jeden mały plik JSON; `catalog_version` jest sprawdzany
  i nieznana wersja kończy się uczciwą odmową zamiast błędów parsowania.
- Cache w profilu (`lite_settings_dir/radioarchiwista`): lista stacji
  i odwiedzone dni działają offline; wiek danych widać po `published_at`.
- Identyfikatory nagrań są stabilne (`radioarchiwista:{slug}:{utc_started_at}`)
  i zależą wyłącznie od katalogu, dzięki czemu ulubione i pozycje przetrwają
  zmiany kolejności w katalogu.
- Sesja jest rejestrowana dokładnie według wzorca Frontier Smart: akcja,
  allowlisty skrótów, profil wydania, menu, paleta, pomoc `Ctrl+F1`,
  gotowość w Menedżerze sesji. Nowy profil ma sesję wyłączoną; użytkownik
  włącza ją w Menedżerze (`Ctrl+Shift+S`), który przydziela skrót `Ctrl+1–9`.

## Testy i wynik

- Źródło (`test_radioarchiwista_source.py`): parsowanie katalogu
  wersji 2, hierarchia poziomów, etykiety, stabilne id, niepełne nagrania,
  odmowa przy nieznanej wersji, cache i jego wiek, ulubione i pozycje,
  pobieranie z postępem, anulowaniem i odmową przy pustym pliku,
  „Najnowsze nagrania" ostatniej godziny, `search_rows` bez sieci.
- Nawigacja (`test_radioarchiwista_session.py`): przejścia poziomów,
  stos powrotu Backspace ze staniem na opuszczonym wierszu, Enter na
  „Najnowsze", PlayMedia z pozycją i tempem, błąd sieci bez zmiany widoku.
- Kontrakty sesji: skróty, allowlisty, kolejność sesji, profil wydania
  (Public wyklucza sesję do czasu ustabilizowania), menu i mnemoniki,
  paleta, pomoc, i18n PL/EN (w tym szablon „Pobrano nagranie do: {arg0}").
- Wyszukiwanie: agregacja stacji i ulubionych bez sieci, brak źródła
  nie psuje pozostałych wyników.
- Pełna regresja wxlite: **2094 zaliczone, 41 padnięć — wszystkie
  potwierdzone jako istniejące już na main (baseline worktree), zero
  nowych**; 149 pominiętych wymaga prawdziwego wxPython.
- Zmiana .NET ograniczona do `src/Shared/ReleaseProfileContract.cs`
  (lista sesji Test + capability), bez zmian funkcjonalnych; kompilacji
  nie wykonano w tej gałęzi.

## Znane ograniczenia i ręczny odbiór

- Archive.org bywa wolny przy pierwszym otwarciu pliku; `media.play` ma
  limit 120 s na start.
- Nagrania oznaczone statusem innym niż „published" są pokazywane
  i opisane zgodnie z danymi katalogu.
- Do ręcznego odbioru pozostaje: przejście całego drzewka na żywym
  katalogu, odtworzenie z wznowieniem i tempem, pobranie jednego pliku,
  ulubione na żywo, odświeżenie F5, wyszukiwanie globalne i NVDA
  (odczyt wierszy, komunikaty postępu).

## Materiał do dokumentacji użytkownika

> Włącz sesję Radio Archiwista w Menedżerze sesji (Ctrl+Shift+S). Wybierz
> stację, rok, miesiąc i dzień, a Enter odtworzy nagranie z zachowaną
> pozycją i tempem. Ctrl+D zapisze nagranie jako plik MP3, Ctrl+Shift+U
> doda je do ulubionych, a F5 odświeży katalog.
