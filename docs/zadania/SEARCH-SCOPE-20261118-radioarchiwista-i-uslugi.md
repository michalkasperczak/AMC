# SEARCH-SCOPE-20261118 — Radio Archiwista i zakres wyszukiwania globalnego

Status: zintegrowane w źródłach i sprawdzone testami headless 18.11.2026;
bez instalacji, publikacji ani odbioru NVDA. Kod: `bb3fdcd3284aa94847b6b45e6dc6215aa89d29e7`.
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

### Kontrola i integracja

Odczytano rzeczywisty log wykonawcy: `224 passed in 1.79s` w pięciu wskazanych
plikach, nie pełna bateria repozytorium. Niezależne review `deleg_65b990c2`
dotyczyło niezmiennego kandydata `339a904`: odrzuciło niewłaściwe API listy,
nie zgłosiło uwag bezpieczeństwa i zaakceptowało pozostały kontrakt źródeł,
zapisu oraz wykluczenia niedostępnych usług. Bloker odtworzono i usunięto
w kolejnym commicie `bb3fdcd`, zgodnie z zaleceniem użycia natywnych pól
wyboru per sesja; cztery testy ścisłego API ustawień przeszły.

Własna próba publicznego katalogu na zimnym, tymczasowym profilu rzeczywiście
pobrała tylko `https://radioarchiwista.github.io/data/stations.json`: 43 stacje,
zapytania „Nowy Świat” i „357” znalazły odpowiednio `rns` i `radio357`.
Nie pobrano żadnego fragmentu historii rocznej/miesięcznej/dziennej.
Osobno potwierdzono dla obu skrótów resolver, zbiór obsługiwanych akcji, menu,
paletę i pomoc klawiatury. Instrukcję użytkownika uzupełniono w `README_PL.md`.

Przed integracją wykryto nieprawidłowe natywne API: `CheckListBox.Enable`
przyjmuje jeden stan całego okna, nie `(indeks, stan)`. Test wykonujący
rzeczywisty blok budowy nowej części Ogólnych ze ścisłą sygnaturą API dał
`3 failed` na TypeError. Zastąpiono kontrolkę listą natywnych `wx.CheckBox`,
z faktycznym stanem nieaktywności osobno dla każdej wyłączonej sesji.
To kontrakt kontrolki bez GUI, nie dowód odczytu NVDA.

Domyślne `None` nie oznacza rezygnacji z czasowo wyłączonych usług: przy
zapisie Ogólnych zachowujemy dla nich domyślne „tak”, ale efektywne wykonanie
nadal przecina aktywne sesje. Jawne `[]` pozostaje „nic”. Zachowujemy także
preferencje identyfikatorów niedostępnych w bieżącym manifeście, bez ich
pokazywania lub uruchamiania.

Drugi produkcyjny RED pokazał powrót wyniku wykluczonej usługi z migawki innego
zakresu. Migawka okna jest teraz związana z `catalog_sessions`; inny zestaw
wymaga nowego zbierania, a ten sam może być ponownie użyty.

Po poprawkach wykonano ograniczony przebieg na produkcyjnych handlerach,
kontrolkach ustawień ze ścisłym API, sąsiedniej nawigacji wyszukiwania oraz
kopiowaniu Podcastów: `124 passed in 3.20s`. Obejmuje także zachowanie
zapamiętanego „nie” po ponownym włączeniu oraz wykluczenie usługi przez manifest.
Log: `amc-search-scope-20261118-parent-acceptance.log` w katalogu scratch profilu.
Nie sumować go z 224 testami wykonawcy — zestawy częściowo się pokrywają.

Po kontroli czystego głównego checkoutu i gałęzi wykonano fast-forward do
`bb3fdcd`. Wszystkie osiem przychodzących plików porównano bajtowo z Gitem.
Po integracji krótki test zmienionej granicy: `14 passed in 2.91s`.

Brak nowego pakietu, instalacji i publikacji; zainstalowany AMC pozostaje
niezmieniony. Pozostaje odbiór natywnego okna, klawiatury, fokusu i komunikatów
z żywym NVDA w uzgodnionym oknie testowym. Testów bez GUI nie uznawać za ten
odbiór. Implementacja źródłowa i jej ograniczony odbiór automatyczny zakończone.
