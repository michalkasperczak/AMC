# SEARCH-SCOPE-20261118 — Radio Archiwista i zakres wyszukiwania globalnego

Status: zaimplementowane i sprawdzone testami headless 18.11.2026; bez instalacji,
publikacji ani odbioru NVDA.
Jawne zlecenie Michała z 18.11.2026; wykonanie po odbiorze poprzednika,
bez potrzeby dodatkowego polecenia „kontynuuj”.

## Cel

1. Udostępnić działające `Ctrl+F` w sesji Radio Archiwista, ze zwykłym
   oknem wyszukiwania AMC i kontraktem obsługi wyników.
   Doprecyzowanie Michała: brak dotyczy AMC, nie wyszukiwarki strony.
   Wykorzystać działające wyszukiwanie serwisu i sprawdzić jego rzeczywisty
   zakres. Samo dopuszczenie skrótu i przeszukiwanie lokalnego cache oraz
   Ulubionych NIE wystarcza do uznania tej funkcji za wdrożoną.
2. W Ustawieniach ogólnych dodać dostępną klawiaturowo listę wyboru usług
   „Uwzględniaj przy wyszukiwaniu we wszystkich usługach (Ctrl+Shift+F)”.
3. Oddzielić zapamiętane życzenie użytkownika od chwilowej aktywności sesji.

## Reguła użytkownika

Sesja bierze udział w `Ctrl+Shift+F` tylko wtedy, gdy jednocześnie:

- jest dostępna w bieżącym profilu AMC,
- jest włączona w Menedżerze sesji,
- użytkownik zaznaczył ją do wyszukiwania globalnego.

Wyłączenie sesji nie może usuwać jej zapamiętanego zaznaczenia.
Po ponownym włączeniu sesji wraca poprzednia decyzja, również wyłączenie.
Nie trzeba ponownie zaznaczać poprzednio uwzględnianej usługi.
Preferencja musi przeżyć zapis i ponowne uruchomienie AMC.

Lista ustawień ma jasno rozróżniać „wyłączona sesja” od „nie uwzględniaj
w wyszukiwaniu”. Dla wyłączonej sesji zachować wartość i czytelnie wskazać
nieaktywność. Nie przedstawiać niedostępnej sesji jako gotowej do użycia.

`Ctrl+F` w aktywnej sesji jest niezależne od zgody na wyszukiwanie globalne.
Jeśli wszystkie źródła `Ctrl+Shift+F` są odznaczone, pokazać krótki komunikat
zamiast wyszukiwać mimo wyboru użytkownika.

## Odczyt kodu przed rozpoczęciem

Baza repozytorium przy rejestracji: `031349fedb92a96975d5e4895f843160d3db3d89`.
Zmienione w głównym checkoutcie są wyłącznie własne dokumenty oczekującej
zmiany kopiowania Podcastów. Wykonawca Ctrl+C pracuje oddzielnie.

- `LiteFrame._run_search_query` obecnie wybiera dla trybu globalnego
  `session_order.available_sessions()`, a nie aktywny zestaw użytkownika.
  To konkretne miejsce wymagające przecięcia zbiorów PRZED odczytem źródeł,
  budową migawek, pracą w tle i ewentualnym zapytaniem sieciowym.
- Radio Archiwista ma już `SearchSources` i `RadioarchiwistaSource.search_rows`,
  które zwracają ważny cache stacji i zapisane ulubione. Nie jest to dowód
  pełnego działającego Ctrl+F ani przeszukiwania całego archiwum internetowego.
- Należy sprawdzić brak `SEARCH_CURRENT`/`SEARCH_ALL` w zbiorze wspieranych
  działań Radio Archiwisty oraz rzeczywistą drogę skrót → handler → wyniki.
  Nie dopisywać drugiego okna lub samego opisu klawisza.
- `SettingsDialog` ma kategorię Ogólne i odsyłacz do Menedżera sesji;
  dane przechowuje `Options` w `state_store.py`, aktywność `session_order`.

## Granice i wydajność

Nie zmieniać katalogu publicznego ani nie pobierać całego wieloletniego
archiwum w reakcji na każde wyszukiwanie. Przed deklaracją zakresu wyników
Radio Archiwisty zmierzyć, co faktycznie jest wyszukiwane i otwierane.
Nie obiecywać wyszukiwania w całej zawartości nagrań ani transkrypcjach.

Wykluczona usługa nie ma być najpierw ładowana, aby później ukryć jej wyniki.
Nie powinna otrzymywać wywołań źródła ani zapytań sieciowych; sprawdzić to
licznikami rzeczywistych wywołań adapterów w testach, nie samą listą wyników.
Nie mieszać preferencji z danymi logowania, bibliotekami ani kolejnością sesji.
Przy ponownym zapytaniu nie używać cache pochodzącego z niezgodnego zakresu.

Na starym profilu bez nowej opcji domyślnie uwzględnić aktywne obsługiwane
sesje, bez arbitralnego wyłączania usług. Ukryte/wyłączone sesje są wykluczone
niezależnie od wartości domyślnej. Osobne dane Public i Test zachowane.

Bez zmian instalacji na głównym PC, lokalnych buildów Windows/ISCC, AV,
restartów i nowego wydania w ramach samej implementacji źródeł.

## Testy i kryteria

- RED/GREEN rzeczywistego Ctrl+F Radio Archiwisty i poprawnego wykonania
  działania na wyniku; historia zapytań oraz powrót fokusu jak w AMC.
- Round-trip ustawień: zaznaczenie, odznaczenie, restart/zapis-odczyt.
- Wyłączona sesja nie uruchamia źródła, mimo dawnego zaznaczenia.
- Ponowne włączenie przywraca zarówno dawny wybór „tak”, jak i „nie”.
- Opcja globalna nie blokuje lokalnego `Ctrl+F` w aktywnej sesji.
- Same wykluczone usługi / brak źródeł: bez fallbacku i zbędnego I/O.
- Profil Public nie otrzymuje niedozwolonych usług, także po migracji.
- Menu, paleta, Ctrl+F1, pełna pomoc i instrukcja zgodne z działaniem.
- Jeden proporcjonalny zestaw; natywne wx/NVDA osobno od testów headless.

## Uzasadnienie i decyzje

Zapamiętywać preferencję wyszukiwania także dla czasowo wyłączonej sesji.
To usuwa potrzebę ponownego ustawiania po każdym przełączeniu i nie oznacza
ukrytego ponownego włączenia usługi. Prędkości nie deklarować bez pomiaru;
pewny skutek wymagania to niewywoływanie wyłączonych źródeł.

## Materiał do dokumentacji użytkownika

`Ctrl+F` szuka w bieżącej sesji. W Radio Archiwista wyszukuje publiczny katalog
**stacji**, tak jak pole „Filtr stacji” strony: pobiera wyłącznie
`data/stations.json`, bez pobierania historii nagrań. Wybierz wynik Enterem,
a następnie przejdź w zwykłej liście przez rok, miesiąc, dzień i godzinę.
Funkcja nie wyszukuje treści dźwięku ani transkrypcji.

`Ctrl+Shift+F` uwzględnia tylko usługi aktywne i zaznaczone w Ustawieniach
ogólnych, na liście „Uwzględniaj przy wyszukiwaniu we wszystkich usługach
(Ctrl+Shift+F)”. Wyłączona w Menedżerze sesji pozycja ma osobną etykietę,
nie jest przeszukiwana i nie wywołuje źródła ani sieci, lecz program pamięta
jej zaznaczenie. Po ponownym włączeniu wraca poprzednie „tak” albo „nie”.
Gdy nie zaznaczono żadnego aktywnego źródła, AMC krótko to komunikuje i nie
uruchamia wyszukiwania. Ta opcja nie zmienia `Ctrl+F` w bieżącej usłudze.

## Wersja i następny krok

Brak nowego pakietu/instalacji/publikacji. Kod ma przejść zwykły review i
odbiór dostępnościowy z NVDA po osobnym zleceniu.
