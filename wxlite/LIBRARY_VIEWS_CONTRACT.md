# Kontrakt kolejnych widokow lokalnej Biblioteki (warstwa DANYCH)

Zakres: `amc_wx_lite/library_views.py`. CZTERY operacje tylko do odczytu:
wszystkie pliki alfabetycznie, Ulubione, lista playlist, zawartosc playlisty.
Bez okien i bez skrotow -- to warstwa pod pozniejsze wlaczenie GUI.

Kazda regula nizej jest PRZEPISANA z aktualnego kodu AMC (C#), nie z nazw
tabel ani ze zdrowego rozsadku. Numery wierszy to stan bazy `fa46923`.

## 0. Wspolny filtr: `ActiveLocalItems()`

`MainWindow.xaml.cs:10004-10005`

```csharp
private IEnumerable<MediaItem> ActiveLocalItems() =>
    _localItems.Where(item => item.IsAvailable && item.IsInLibrary);
```

SQL: `is_available = 1 AND is_in_library = 1` (to samo, co `_ACTIVE`
w `library_db.py`). Dla sesji `local` katalog sesji to wynik
`ReplaceItems(ActiveLocalItems())` (`MainWindow.xaml.cs:10011`), a
`DemoMediaSession.ReplaceItems` robi
`DistinctBy(item => item.Id, StringComparer.Ordinal)`
(`DemoMediaSession.cs:463-465`). Stad `session.Items` dla `local` = aktywne
rekordy, bez duplikatow Id.

**Konsekwencja, ktorej NIE wolno zgadywac:** Ulubione i playlisty czytaja
`session.Items`, wiec rowniez przechodza przez ten sam filtr aktywnosci.
To nie jest "automatyczne przeniesienie ActiveLocalItems" -- to po prostu
to samo zrodlo, ktorego uzywa C#.

## 1. "Wszystkie pliki" -- `all_files_rows`

`MainWindow.xaml.cs:12415-12426`

```csharp
ViewHeading.Text = "Biblioteka — Wszystkie pliki";
_unfilteredItems = ActiveLocalItems()
    .OrderBy(item => item.Title, StringComparer.CurrentCultureIgnoreCase)
    .ThenBy(item => item.Source ?? string.Empty, StringComparer.OrdinalIgnoreCase)
```

* filtr: aktywne (p. 0), BEZ warunku na folder;
* klucz 1: `Title`, `CurrentCultureIgnoreCase` (kultura `pl-PL`);
* klucz 2: `Source ?? ""`, `OrdinalIgnoreCase`. Dla rekordu lokalnego
  `Source` to sciezka pliku (`MainWindow.xaml.cs:7619` `Source = saved.Path`);
* sort jest STABILNY (LINQ `OrderBy`/`ThenBy`), wiec przy identycznym
  tytule i sciezce zostaje kolejnosc wejsciowa.

Alfabetyka idzie z `HostCollation` (klucze AMC_PL z hosta), nie z nowego
kolatora. Zmierzone na peinej bazie (2475 aktywnych, kwit
`csharp-order-allfiles.json`): klucze AMC_PL
(`IgnoreCase|IgnoreNonSpace`) daja DOKLADNIE te sama kolejnosc, co
`CurrentCultureIgnoreCase` -- `amcPlKeysDifferFromAllFiles = 0`, ten sam
SHA-256 ciagu Id. Dlatego reuzycie kluczy hosta jest uprawnione.

Bez hosta NIE udajemy zgodnosci: `LibraryViewResult.order_matches_amc`
schodzi na `False`, tak samo jak w `LibrarySnapshot`.

## 2. "Ulubione" -- `favorite_rows`

`MainWindow.xaml.cs:12524-12534`

```csharp
IEnumerable<MediaItem> items = _sessions.Current.Items;
if (_currentView == "Ulubione")
{
    var favoriteItems = items.Where(item => item.IsFavorite
        && (!UsesTidalStyleCollections(_sessions.Current.Id)
            || TidalCollectionSemantics.UsesFavorites(item.Kind))).ToArray();
    items = OrderCurrentCollection(_sessions.Current, _currentView, favoriteItems);
}
```

Dla sesji `local` `UsesTidalStyleCollections` jest falszywe, wiec filtr to
samo `IsFavorite` nad `session.Items` (czyli: aktywne ORAZ `is_favorite = 1`).

Kolejnosc: `OrderCurrentCollection` (`MainWindow.xaml.cs:12978-13001`).
Tryb domyslny to `CollectionSortMode.AddedNewest`
(`CurrentCollectionSortMode`, 12923-12929 -- `GetValueOrDefault(..., AddedNewest)`):

```csharp
_ => LocalLibraryManualOrder.Order(
        items,
        EnsureCollectionOrder(session, viewName, items, custom: false))
    .Reverse()
    .ToArray()
```

czyli: ulozyc po zapisanej kolejnosci "dodania", potem ODWROCIC. Slownik dla
widoku "Ulubione" i `custom: false` to `FavoriteAddedItemIdsBySession`
(`MainWindow.xaml.cs:13010-13015`), a w SQLite to tabela
`favorite_added_order(session_id, ordinal, item_id)`
(`LocalLibraryDatabase.cs:445-450`), czytana
`ORDER BY session_id, ordinal` (`LocalLibraryDatabase.cs:234` dla
`favorite_order`, analogicznie 230-231 dla `favorite_added_order`).

`LocalLibraryManualOrder.Order` (`LocalLibraryManualOrder.cs:89-105`):
pozycja = indeks pierwszego wystapienia Id w zapisanej kolejnosci, brak Id
= `int.MaxValue`, tie-break = indeks wejsciowy. Czyli pozycje NIEZNANE
zapisowi ida na koniec listy przed odwroceniem, a po `.Reverse()` -- na
POCZATEK, w odwroconej kolejnosci wejsciowej.

Tryb `Custom` (Alt+3) uzywa `favorite_order` bez odwracania; udostepniamy go
jako `order="custom"`, ale DOMYSLNY jest `added_newest`, bo tak robi AMC bez
zapisanego wyboru uzytkownika.

## 3. "Playlisty" -- `playlist_rows`

`MainWindow.xaml.cs:12800-12839` (`CreatePlaylistRows`)

```csharp
var itemsById = _sessions.Current.Items.ToDictionary(item => item.Id, StringComparer.Ordinal);
return new PlaylistIndex(_state.Playlists)
    .GetForSession(_sessions.Current.Id)
    .Select(playlist => { ... availableItems = playlist.ItemIds
            .Select(itemId => itemsById.GetValueOrDefault(itemId))
            .Where(item => item is not null) ... })
```

* zbior: `PlaylistIndex.GetForSession` (`PlaylistIndex.cs:22-25`) --
  `SessionId` rowny `local`, porownanie `OrdinalIgnoreCase`;
* kolejnosc listy playlist: kolejnosc wpisow w `settings.Entries`, a ta
  pochodzi z `SELECT ... FROM playlists ORDER BY session_id, ordinal`
  (`LocalLibraryDatabase.cs:286`). NIE jest to sort po nazwie -- mimo
  `COLLATE AMC_PL` na kolumnie `name`;
* Id wiersza: `$"playlist:{playlist.Id}"` (12822) -- NAPIS, stabilny miedzy
  odswiezeniami, nadaje sie na powrot/wybor playlisty po Id;
* `durationTicks`: suma `Duration.Ticks` DOSTEPNYCH pozycji z pominieciem
  `MediaItemKind.Station`, z saturacja do `long.MaxValue` (12813-12819);
* etykieta: `PlaylistPresentation.BuildLabel(name, playlist.ItemIds.Count,
  availableItems.Length, availableItems)` (12830-12834).

`BuildLabel` (`PlaylistPresentation.cs:17-49`) -- semantyka pozycji
niedostepnych jest tutaj i tylko tutaj:

* `availableItemCount == storedItemCount` -> `FormatItemCount(stored)`
  (`1 element` / `N elementy` / `N elementow`, reguly 68-76);
* inaczej -> `dostepne {available} z {stored}`;
* `stored == 0` -> `"{name}, {availability}"` i KONIEC (pusta playlista nie
  dostaje czasu);
* brak pozycji skonczonych przy niepustych dostepnych -> `, transmisje na zywo`;
* zero znanych czasow -> `, laczny czas nieznany`;
* wszystkie znane -> `, laczny czas {X godz. Y min | X min Y s}`;
* czesc znana -> `, znany czas {...}, czesc bez danych`.

## 4. "Playlista: <id>" -- `playlist_contents_rows`

`MainWindow.xaml.cs:12462-12482`

```csharp
if (TryGetPlaylistIdFromView(_currentView, out var playlistId))
{
    var playlist = new PlaylistIndex(_state.Playlists).Find(playlistId);
    if (playlist is null
        || !string.Equals(playlist.SessionId, _sessions.Current.Id, StringComparison.OrdinalIgnoreCase))
    { _currentView = "Playlisty"; ... return; }
    ViewHeading.Text = $"Playlista — {playlist.Name}";
    var itemsById = _sessions.Current.Items.ToDictionary(item => item.Id, StringComparer.Ordinal);
    _unfilteredItems = playlist.ItemIds
        .Select(itemId => itemsById.GetValueOrDefault(itemId))
        .Where(item => item is not null)
```

* kolejnosc: DOKLADNIE `playlist.ItemIds`, czyli
  `SELECT playlist_id, item_id FROM playlist_items ORDER BY playlist_id, ordinal`
  (`LocalLibraryDatabase.cs:303`). Zadnego sortu po tytule;
* pozycje, ktorych nie ma w katalogu sesji (np. plik juz niedostepny albo
  usuniety z Biblioteki), sa POMIJANE -- cicho, bez wiersza zastepczego.
  Dlatego liczba wierszy moze byc mniejsza niz `len(item_ids)`, a roznica
  jest widoczna w etykiecie z p. 3;
* `Find` porownuje Id `Ordinal` (`PlaylistIndex.cs:27-28`), a przynaleznosc
  do sesji `OrdinalIgnoreCase` (12466);
* playlista nieistniejaca albo z innej sesji = NIE blad: AMC wraca do widoku
  "Playlisty". W warstwie danych odpowiada temu
  `PlaylistMissing`/`fallback_view="Playlisty"`, zeby GUI mialo co zrobic;
* nazwa widoku: prefiks `"Playlista:"` (`MainWindow.xaml.cs:67`), rozbior
  w `TryGetPlaylistIdFromView` (20057-20067): `StartsWith(..., Ordinal)`
  i `Length >` prefiksu, inaczej `false`.

## 5. Kontrakt wiersza (`Row`) -- bez przebudowy

`Row` z `list_model.py` zostaje BEZ ZMIAN: ten sam konstruktor, te same pola.
Uzywamy wylacznie pol nazwanych.

* plik: `kind="track"`, `item_id` = `local_items.id` (NAPIS),
  `title` = tytul, `path` = sciezka, `detail` = jak `library_db._format_detail`;
* playlista: `kind="playlist"`, `item_id` = `"playlist:<id>"`,
  `title` = nazwa, `path=None`, `detail` = pelna etykieta `BuildLabel`
  bez przedrostka nazwy (czyli czesc po `"{name}, "`).

`kind="playlist"` to JEDYNE rozszerzenie. `Row.is_openable` sprawdza
`kind in ("folder", "parent")`, wiec playlista NIE jest tam wymieniona --
`is_openable` zostawiamy nietkniete (to plik drugiego autora), a warstwa
danych oddaje osobne `PlaylistRow.playlist_id` dla GUI. Metadane playlisty
(`playlist_id`, `stored_count`, `available_count`, `duration_ticks`) jada
w lekkim rekordzie `PlaylistRow` OBOK `Row`, nie w nim.

## 6. Co zostalo ZMIERZONE, a co tylko przepisane

Rozroznienie jest istotne: ponizej nie ma ani jednego uruchomienia GUI AMC
ani czytnika ekranu.

ZMIERZONE wykonaniem oryginalnego kodu .NET (`probe-csharp-order`,
`CompareInfo` z `pl-PL`, runtime 8.0.31):

* kolejnosc widoku "Wszystkie pliki" na 2475 PRAWDZIWYCH tytulach z kopii
  profilu zgadza sie z `OrderBy(Title, CurrentCultureIgnoreCase)
  .ThenBy(Source, OrdinalIgnoreCase)` **co do pozycji**:
  `positions_differing_from_csharp = 0`, ten sam SHA-256 ciagu Id
  (`bcb8c52d...`);
* klucze AMC_PL (`IgnoreCase|IgnoreNonSpace`), ktorych uzywa
  `library.collationKeys` w LiteHost, odtwarzaja te kolejnosc bez roznicy
  (`amcPlKeysDifferFromAllFiles = 0`). Dlatego reuzycie kluczy hosta jest
  uprawnione i NIE powstal nowy kolator Unicode.

ZMIERZONE odczytem pelnej bazy (kwit `full-profile-receipt.json`):
licznosci czterech widokow, unikalnosc i napisowosc Id, stabilnosc Id po
ponownym otwarciu, predykaty z p. 0-4, hash bazy przed i po odczycie.

PRZEPISANE Z KODU, ale NIE wykonane w oryginale: tresc etykiet
`BuildLabel`, galaz "transmisje na zywo" (w sesji lokalnej nie ma
`MediaItemKind.Station`, wiec nie da sie jej zapalic danymi z tej bazy),
saturacja `durationTicks` do `long.MaxValue`, tryb `Custom` sortu Ulubionych
przy braku zapisanego wyboru uzytkownika.

## 7. Czego ta warstwa NIE robi

* nie pisze do bazy ani do profilu, nie tworzy tabel, nie migruje;
* nie dotyka podcastow, TIDAL-a, Spotify, Sonos ani harmonogramow;
* nie otwiera okien i nie obsluguje skrotow;
* nie zaklada wlasnej bazy -- czyta BIEZACY plik przez `LibraryDatabase`
  (`mode=ro`, widzi WAL zywego hosta).
