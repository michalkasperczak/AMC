# Kontrakt warstwy danych: historia, zapisana kolejka, zakładki

Moduł: `wxlite/amc_wx_lite/library_activity.py`. Tylko **odczyt**. Bez GUI, bez
zapisu do bazy, bez zapisu profilu. To warstwa **danych** do późniejszej
integracji z listą — nie jest to kolejka działającego pythonowego odtwarzacza
i nie udaje jego API.

## Cztery wejścia

```python
from amc_wx_lite.library_db import LibraryDatabase
from amc_wx_lite.library_activity import (
    history_rows, saved_queue_rows, bookmark_rows, all_bookmark_rows,
)

with LibraryDatabase(sciezka_do_library_db) as db:
    hist  = history_rows(db)                               # sesja 'local'
    queue = saved_queue_rows(db)
    marks = bookmark_rows(db, item_id="<RZECZYWISTE Id>")   # NIE indeks listy
    # Zbiorczy widok WSZYSTKICH zakładek (Ctrl+B w AMC). Kontekst JAWNY:
    # to aktualnie ODTWARZANY materiał, nie zaznaczony wiersz listy.
    wszystkie = all_bookmark_rows(
        db,
        current_session_id=sesja_grajaca,   # '' gdy nic nie gra
        current_item_id=material_grajacy,   # ''
        collation=host_collation,           # None => order_matches_amc=False
    )
```

`bookmark_rows` ≠ `all_bookmark_rows`. Pierwsza to `BookmarkIndex.GetForItem`
(jeden element, porządek liczbowy), druga to `GetForDisplay` (wszystkie sesje,
porządek alfabetyczny z kolacją). Mają inny filtr, inną kolejność i inny
nagłówek; nie wolno ich podmieniać jedną za drugą.

Wszystkie cztery zwracają `ActivityResult`:

| pole | znaczenie |
|---|---|
| `rows: list[Row]` | gotowe wiersze dla istniejącego `list_model` (ten sam `Row`, co widoki) |
| `heading: str` | nagłówek widoku |
| `order_matches_amc: bool` | `True` dla trzech pierwszych widoków (porządek **zapisany** lub liczbowy). Dla `all_bookmark_rows` jest `False`, gdy nie podano kolatora — alfabetyka bez kolacji **nie** jest zgodna z C# |
| `sees_live_writes: bool` | czy połączenie śledzi WAL (`mode=ro`), czy jest zamrożoną migawką |
| `missing_item_count: int` | ile zapisanych wpisów nie dało wiersza (historia, kolejka) |
| `queue: tuple[QueueRow, ...]` | metadane kolejki **obok** wiersza (`is_in_queue`, `is_play_next`) |
| `bookmarks: tuple[BookmarkRow, ...]` | metadane zakładki obok wiersza (pozycja w tickach, data, nazwa, Id) |
| `display: tuple[BookmarkDisplayRow, ...]` | **tylko** `all_bookmark_rows`: `bookmark`, `is_current_item`, `can_play_locally`, `position_seconds` |
| `is_empty: bool` | brak wierszy |

Metadane leżą **obok** `Row`, bez przebudowy `Row`/`list_model`/`library_views`.

## Reguły przeniesione z C# (plik:linia)

### Historia odtwarzania

| reguła | źródło C# |
|---|---|
| kolejność jest **zapisana**, `ordinal 0` = ostatnio odtworzone (`Record` wstawia na przód) | `PlaybackHistory.cs:29` |
| puste/białe `sessionId` → pusta lista | `PlaybackHistory.cs:9-13` |
| `Distinct(Ordinal)` + `Take(500)`, obcinany jest **ogon** | `PlaybackHistory.cs:52-56` |
| klucz sesji `OrdinalIgnoreCase`, Id `Ordinal` | `PlaybackHistory.cs:47-49` |
| pozycja nieobecna w katalogu sesji **nie daje wiersza**, bez placeholdera | `MainWindow.xaml.cs:12857-12859` |
| katalog sesji lokalnej = `ActiveLocalItems()` → `IsAvailable && IsInLibrary` | `MainWindow.xaml.cs:9972`, `10116-10117` |

`ActiveLocalItems` zastosowane tu **nie** jest naszym skrótem — to katalog,
z którego widok historii w oryginale czyta (`_sessions.Current.Items`).
W zakładkach jest **inaczej** (patrz niżej) i tej reguły tam nie przenosimy.

Tabela `playback_history` nie ma kolumny czasu, więc „czas odtworzenia” nie
istnieje w danych i moduł go **nie wymyśla**.

### Zapisana kolejka

| reguła | źródło C# |
|---|---|
| dwustopniowy porządek: ręczna kolejność, potem `OrderByDescending(IsPlayNext)` | `MainWindow.xaml.cs:13518-13525` |
| pozycja = indeks **pierwszego** wystąpienia Id; nieznane Id → `int.MaxValue`; remis rozstrzyga indeks katalogu | `LocalLibraryManualOrder.cs:89-105` |
| członkostwo z **zapisu**, nie z kolumn `is_in_queue`/`is_play_next` (Restore najpierw zeruje oba, potem nadaje z zapisu) | `TransientQueuePersistence.cs:109-127` |
| `legacyRegularQueue`: gdy obie listy członkostwa puste, cała `queue_order` jest zwykłą kolejką | `TransientQueuePersistence.cs:101` |
| filtr widoku przepuszcza tylko `IsInQueue \|\| IsPlayNext` | `MainWindow.xaml.cs:13520` |

Rozróżnienie **kolejka zapisana vs kolejka silnika**: ten moduł czyta wyłącznie
to, co AMC utrwaliło w profilu (`queue_order`, `queue_regular_order`,
`queue_play_next_order`). Żywego silnika w tej warstwie nie ma.

Mapowanie `StorageItemId` (`TransientQueuePersistence.cs:130-138`) dotyczy sesji
`tidal` z `ExternalId`; dla `local` kluczem jest samo `Id`, więc zdalnej logiki
tu nie wprowadzamy.

### Zakładki wybranego elementu

| reguła | źródło C# |
|---|---|
| wejściem jest **rzeczywiste Id** elementu (`GetForItem(sessionId, itemId)`) | `BookmarkIndex.cs:30-36` |
| `SessionId` `OrdinalIgnoreCase`, `ItemId` `Ordinal` — dwa różne porównania | `BookmarkIndex.cs:32-33` |
| kolejność `OrderBy(PositionTicks).ThenBy(CreatedUtcTicks)` — liczbowa, bez alfabetyki | `BookmarkIndex.cs:34-35` |
| widoczne tylko wpisy z bitem `Bookmark` w `Purpose`; czysty rozdział **nie** jest zakładką i **nie** jest usuwany | `BookmarkIndex.cs:189-190` |
| dwa wpisy o tej samej pozycji **zostają oba** — tolerancja 1 s działa tylko w `Add` | `BookmarkIndex.cs:10`, `53-56` |
| zakładka pozycji nieobecnej w katalogu **nadal widoczna**, z tytułem zapisanym w zakładce (tu placeholder w oryginale **jest**) | `MainWindow.xaml.cs:13749-13757` |
| Id wiersza `bookmark:{Id}` — dwie zakładki tego samego utworu nie zlewają się | `MainWindow.xaml.cs:13756` |
| etykieta: `{tytuł}, {data}, {czas}, {nazwa}, {sesja}, zakładka`; pusta nazwa nie zostawia podwójnego przecinka | `MainWindow.xaml.cs:13762-13763` |
| nieznana data (`CreatedUtcTicks <= 0`) → „data utworzenia nieznana”, **nie** `0:00` ani rok 1 | `MainWindow.xaml.cs:13767-13779` |
| czas pozycji: `h:mm:ss` od godziny, inaczej `m:ss` | `MediaItemFormatter.cs:69-73` |

Historia i zakładki mają **różne** reguły widoczności: historia odfiltrowuje
nieaktywne pozycje, zakładki nie. `ActiveLocalItems` nie zostało zastosowane do
zakładek właśnie dlatego, że oryginał robi tam inaczej.

### Zbiorczy widok wszystkich zakładek (`all_bookmark_rows`)

`BookmarkIndex.GetForDisplay`, `BookmarkIndex.cs:19-28`. Wywołanie w oryginale:
`_bookmarkIndex.GetForDisplay(_sessions.Current.Id, _sessions.Current.CurrentItem.Id)`
→ `.Select(CreateBookmarkRow)`, `MainWindow.xaml.cs:12528-12536`.

| reguła | źródło C# |
|---|---|
| widoczny bit `Bookmark` w `Purpose`; czysty `Chapter` (2) niewidoczny, **nie** usuwany; `Purpose` 3 jest zakładką | `BookmarkIndex.cs:19`, `189-190` |
| **wszystkie** sesje, bez filtra po sesji — inaczej niż `GetForItem` | `BookmarkIndex.cs:19-28` |
| 1. klucz: `IsCurrentItem ? 0 : 1` — zakładki aktualnie **odtwarzanego** materiału na górze | `BookmarkIndex.cs:21`, `182-187` |
| `IsCurrentItem`: `SessionId` `OrdinalIgnoreCase`, `ItemId` `Ordinal` — **dwa różne** porównania | `BookmarkIndex.cs:184-186` |
| 2.–3. klucz: `SessionName`, potem `ItemTitle`, oba `StringComparer.CurrentCultureIgnoreCase` | `BookmarkIndex.cs:22-23` |
| 4.–5. klucz: `PositionTicks`, `CreatedUtcTicks` | `BookmarkIndex.cs:24-25` |
| 6. klucz: `Id`, `StringComparer.Ordinal` — **case-sensitive** | `BookmarkIndex.cs:26` |
| wiersz, etykieta i `bookmark:{Id}` — ta sama funkcja co widok jednego pliku | `MainWindow.xaml.cs:13744-13764` |
| wpis bez dostępnego materiału **nadal widoczny**, z tytułem zapisanym w zakładce | `MainWindow.xaml.cs:13749-13757` |

Klucze tekstowe (2.–3.) to tryb kolacji **`TITLE_IGNORE_CASE`**
(`CompareOptions.IgnoreCase` samo), a **nie** `AMC_PL`. `AMC_PL` dokłada
`IgnoreNonSpace` i zrównałby `"etap"` z `"étap"` — remis spadłby wtedy na
pozycję i lista wyszłaby inna. Klucze liczy host **wsadowo** (jedno zadanie na
wszystkie nazwy sesji i tytuły), więc nie ma tu porównań parami przez IPC.
`collation=None` zwraca kolejność zapisu z `order_matches_amc = False` —
brak kolatora nie udaje zgodności.

Ostatni tie-break (`Id`) **nie** używa hostowego `ORDINAL_IGNORE_CASE`: C#
porównuje tu z uwzględnieniem wielkości liter, więc tryb ignorujący ją
zrównałby Id `"a"` i `"A"` i oddałby kolejność przypadkowi (zmierzone: 500–862
niezgodne pary wobec `StringComparer.Ordinal`). Klucz liczy lokalna funkcja
`ordinal_sort_key` = `str.encode("utf-16-be")`, bo `Ordinal` porządkuje
**jednostki kodowe UTF-16**, nie punkty kodowe — `utf-8` i zwykłe `str`
Pythona dają 12 niezgodnych par (emoji `U+1F600` wobec `U+FFFD`). To czysta
transformacja bajtów bez udziału kultury, więc nie jest nowym API kolacji i nie
potrzebuje hosta. Żadnej normalizacji Unicode na Id nie robimy — `Ordinal` jej
nie robi.

## Jednostki i identyfikatory

- `position_ticks` i `created_utc_ticks` to **ticki .NET** (1 s = 10 000 000,
  epoka `0001-01-01`) — te same jednostki co `TimeSpan.Ticks`/`DateTime.Ticks`.
- Identyfikatory są napisami i są stabilne; wiersz zakładki ma własne
  `bookmark:{Id}`.
- Porównania wielkości liter: klucz sesji bez względu na wielkość
  (dla znanych identyfikatorów sesji ASCII `COLLATE NOCASE` odpowiada
  potrzebnemu porównaniu; nie jest ogólnie równoważne Unicode
  `OrdinalIgnoreCase`), Id elementu binarnie (≡ `Ordinal`).
  Żadnego `str.upper()` jako przybliżenia kolatora. Prawdziwe kolacje zostają
  w `HostCollation` i tu nie są potrzebne, bo te trzy widoki mają porządek
  zapisany lub liczbowy.

## Świeżość i brak zapisu

- Baza otwierana przez `LibraryDatabase` (`mode=ro`), nigdy do zapisu.
- Kontrolowany writer **otwarty** w teście jest widoczny przez ponowny odczyt
  (testy `test_sees_commit_from_open_writer` dla wszystkich trzech operacji).
  Zachowania WAL po **zamknięciu** writera nie zakładamy.
- Oryginalny fixture nie jest otwierany do zapisu; rogi mierzone są na bazie
  syntetycznej o schemacie przepisanym z prawdziwej bazy.
- `test_read_does_not_modify_the_fixture` porównuje SHA-256 pliku przed i po.

## Potrzebna zmiana integracyjna (do zrobienia przez autora GUI, nie tutaj)

Moduł jest kompletny jako warstwa danych. Do podłączenia pod listę brakuje
wyłącznie rzeczy leżących **poza tym przydziałem**:

1. **Wejście w widok zakładek** — `bookmark_rows` wymaga Id **wybranego**
   elementu. Nawigacja musi podać `Row.item_id` zaznaczonego wiersza, a nie
   indeks listy. Dla wierszy zakładek `item_id` ma prefiks `bookmark:` i **nie
   jest** Id elementu — do powrotu do utworu służy `BookmarkRow.item_id`.
2. **Rejestracja widoków** w `gui.py`/`navigation.py`/`shortcuts.py`
   (nagłówki w `ActivityResult.heading`). Tych plików ten przyrost nie dotyka.
   Widok zbiorczy to w AMC `Ctrl+B`, widok jednego pliku `Ctrl+Shift+B` — to
   **dwa** wpisy, nie jeden.
3. **Skok do pozycji** z zakładki wymaga odtwarzacza; `position_ticks` jest już
   podane w tickach C#. Przy przeliczaniu na sekundy zachowaj część ułamkową
   (`position_ticks / 10_000_000`); `//` błędnie obcinałoby pozycję.
   `BookmarkDisplayRow.position_seconds` robi to już poprawnie.
4. **Kontekst dla widoku zbiorczego** — `all_bookmark_rows` wymaga
   `current_session_id`/`current_item_id` **aktualnie odtwarzanego** materiału.
   To nie jest zaznaczony wiersz listy; podanie zaznaczenia przeniosłoby na górę
   złe zakładki. Gdy nic nie gra, przekaż `""`/`""` — jest to legalne i po
   prostu nie pasuje do niczego.
5. **Kolator** — `all_bookmark_rows` potrzebuje `HostCollation` z działającym
   `LiteHost` (tryb `TITLE_IGNORE_CASE`). Bez niego wolno pokazać listę, ale
   trzeba zajrzeć w `order_matches_amc` i powiedzieć użytkownikowi, że
   kolejność jest zastępcza.
6. **Odtwarzanie obcej sesji** — `can_play_locally` jest `False` dla TIDAL,
   podcastów i Spotify. W tym przyroście nie ma kanału, który by je odtworzył
   (`files.play` dotyczy plików lokalnych), więc wiersz wolno pokazać, ale nie
   wolno obiecać skoku. Odtwarzanie sieciowe to osobna, nieistniejąca jeszcze
   sprawa.

Nie zmierzono i nie deklaruje się tu żadnego zachowania GUI ani czytnika ekranu.
