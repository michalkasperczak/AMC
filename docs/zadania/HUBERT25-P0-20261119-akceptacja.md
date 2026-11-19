# #25 P0 — wspólny kandydat i wykonany odbiór Windows

## Wersja i zakres

Kandydat: `ec34b3bfc2621e6b5d002f9158cffecf36a0ab10`, gałąź `codex/amc-hubert25-p0-combined-20261119`, baza kanoniczna `4b747dc`. Trzy zmiany przeniesiono bez konfliktów do oddzielnego worktree, bez modyfikowania produkcji gałęzi kanonicznej:

- `6f55a14b` → `ffd0bcc`: daty, flagi „nowe”, trwałość wyniku po kanale;
- `9700a613` → `529714d`: końcowy limit i sortowanie także przy pustym RSS;
- `9611e851` → `ec34b3b`: wąski zapis celu zamiast materializacji całej biblioteki.

Oryginalne worktree pozostają czyste. Nie pushowano i nie publikowano.

## Wykonany odbiór rodzica

Rodzic odczytał produkcyjny diff korekty DB, test zakresu i raport wykonawcy. Następnie utworzył nowy staging Windows/NTFS ze wspólnego SHA, potwierdził obecność wszystkich 3043 śledzonych plików i bajtowo porównał 12 zmienionych plików z Git. Nie używał binariów poprzedniego kandydata.

SDK: istniejący prywatny Windows .NET `8.0.423`. Restore czterech projektów z lokalnego cache NuGet: wszystkie exit 0. Wykonano jeden spójny przebieg dla połączonego kandydata:

- Core Smoke `--podcast-library-update`: exit 0.
- Core Smoke `--youtube-subscription-sync-apply`: exit 0; „OK: atomowy zapis subskrypcji YouTube i seria podcastów (9 scenariuszy)”. W tej trasie jest również nowy test zakresu zapisu, więc nie doliczono go osobno i nie ponawiano pod drugim przełącznikiem.
- Windows Smoke `--youtube-collection-normalization`: exit 0; „OK: ograniczone kanały i playlisty publicznego YouTube”.
- LiteHost Protocol `--podcast-progress`: exit 0; „Wszystkie zestawy przeszly (1).”
- Rzeczywisty LiteHost `build -c Release --no-restore --nologo`: exit 0, 0 ostrzeżeń, 0 błędów.

Testy uruchamiano przez `dotnet run --project <projekt> -c Release --no-restore -- <przełącznik>`. To cztery celowane trasy testowe, nie pełna suite. Windows test kolekcji i Protocol test zawierają więcej niż jedną asercję; ich liczby nie sumowano do sztucznego łącznego wyniku.

Dowody: `C:\Users\Michal\AppData\Local\AMC-Hermes-Build\hubert25-p0-combined-20261119\.amc-parent-acceptance\results.json`, sąsiednie logi oraz `source-identity.json`. Skrypt wykonania rodzica: `accept-hubert25-combined-20261119.py` w scratch profilu AMC.

## Zakres dowodu DB

Nowa operacja ładuje wskazaną subskrypcję oraz jej odcinki, zachowuje ordinale istniejących wierszy i nie zapisuje globalnego `podcast_state`. Test niepoprawnego JSON poza celem wykazał u wykonawcy RED w dawnym `LoadInto`, a po zmianie GREEN; porównuje niezmienione payloady, hashe i ordinale poza celem oraz postęp i flagi celu. Nie jest to pomiar czasu ani dowód, że wszystkie zapytania SQL mają stały koszt: pozostają agregaty SQL, w tym końcowy InboxCount i przydział globalnego ordinalu. Nie deklaruje się zmierzonego przyspieszenia biblioteki użytkownika.

## Bieżąca bramka i następny krok

**Wspólny kandydat przeszedł wymieniony odbiór automatyczny, ale oczekuje na niezależny delta-review korekty DB `9611e851`.** Review obejmuje tylko tę zmianę i integralność sąsiednich danych; nie zlecono kolejnego pełnego audytu ani mechanicznego powtarzania suite.

Po pozytywnym wyniku odczytać dokładny raport, zweryfikować niezmienność źródeł kandydata, włączyć kandydat do kanonicznej gałęzi i oznaczyć #25 dane/trwałość jako odebrane. Dopiero potem rozpocząć #25 ograniczony catch-up i harmonogram; #26 oraz #28 nadal później. Przy konkretnym blokerze nie integrować i zapisać jego rzeczywisty zakres.

## Materiał do dokumentacji użytkownika i ograniczenia

Przygotowane zmiany mają zapobiegać znikaniu nowych filmów podczas scalania kanału, błędnemu oznaczaniu starych materiałów jako nowe oraz utracie wyniku ukończonego kanału po przerwaniu odświeżania. Zachowane mają być postęp odtwarzania, własne nazwy, ulubione i kolejka. Nie czyszczono masowo dotychczasowych oznaczeń.

Nie ma nowego skrótu ani kontrolki. To jeszcze nie dostarczone wydanie: nie wykonano instalacji, publikacji, GUI/NVDA, żywego YouTube, odsłuchu ani działań na głównym komputerze. Dane kont i nagrania pozostają nietknięte.
