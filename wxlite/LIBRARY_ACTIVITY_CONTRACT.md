# Kontrakt warstwy danych: historia, zapisana kolejka, zakładki

Moduł: `wxlite/amc_wx_lite/library_activity.py`. Tylko **odczyt**. Bez GUI, bez
zapisu do bazy, bez zapisu profilu. To warstwa **danych** do późniejszej
integracji z listą — nie jest to kolejka działającego pythonowego odtwarzacza
i nie udaje jego API.

## Trzy wejścia

```python
from amc_wx_lite.library_db import LibraryDatabase
from amc_wx_lite.library_activity import history_rows, saved_queue_rows, bookmark_rows

with LibraryDatabase(sciezka_do_library_db) as db:
    hist  = history_rows(db)                               # sesja 'local'
    queue = saved_queue_rows(db)
    marks = bookmark_rows(db, item_id="<RZECZYWISTE Id>")   # NIE indeks listy
```

Wszystkie trzy zwracają `ActivityResult`:

| pole | znaczenie |
|---|---|
| `rows: list[Row]` | gotowe wiersze dla istniejącego `list_model` (ten sam `Row`, co widoki) |
| `heading: str` | nagłówek widoku |
| `order_matches_amc: bool` | zawsze `True`: te trzy widoki mają porządek **zapisany**, nie alfabetyczny |
| `sees_live_writes: bool` | czy połączenie śledzi WAL (`mode=ro`), czy jest zamrożoną migawką |
| `missing_item_count: int` | ile zapisanych wpisów nie dało wiersza (historia, kolejka) |
| `queue: tuple[QueueRow, ...]` | metadane kolejki **obok** wiersza (`is_in_queue`, `is_play_next`) |
| `bookmarks: tuple[BookmarkRow, ...]` | metadane zakładki obok wiersza (pozycja w tickach, data, nazwa, Id) |
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
2. **Rejestracja trzech widoków** w `gui.py`/`navigation.py`/`shortcuts.py`
   (nagłówki w `ActivityResult.heading`). Tych plików ten przyrost nie dotyka.
3. **Skok do pozycji** z zakładki wymaga odtwarzacza; `position_ticks` jest już
   podane w tickach C#. Przy przeliczaniu na sekundy zachowaj część ułamkową
   (`position_ticks / 10_000_000`); `//` błędnie obcinałoby pozycję.

Nie zmierzono i nie deklaruje się tu żadnego zachowania GUI ani czytnika ekranu.
