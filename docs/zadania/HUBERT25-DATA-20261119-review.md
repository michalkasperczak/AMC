# Odbiór kandydata #25 P0 — 19.11.2026

## Wersja i werdykt

**Nie przyjmować zamrożonego `6f55a14b53703201ecf30da793e23e39e2e9c28b` bez dwóch korekt.** Baza: `80c257668ef3abd07607b97c4a1561222d67986d`. Przegląd niezależny obejmował osiem zmienionych plików i produkcyjną ścieżkę zapisu. Nie oceniał GUI, NVDA, audio, kont, instalatora ani następnych etapów. Źródłowy raport: `hubert25-data-review-20261119.md` w scratch profilu AMC; delegacja `deleg_ec2bbff4`.

## Wymagane korekty

### Pusty lub uszkodzony RSS po scaleniu list

Po zmianie `MergeChannelDocuments` utrzymuje kandydatów z dwóch list przed nałożeniem dat. Jednak `CompleteFromChannelFeed` przy pustym słowniku wraca przed `OrderChannelEpisodes`. Brak/awaria RSS to rzeczywista ścieżka `YouTubeChannelFeedClient`, nie wyłącznie stan testowy. Dwie listy po 100 pozycji mogą więc dać 200 zamiast końcowego limitu 100 i kolejność scalania zamiast końcowego sortowania.

Rodzic wskazał tę granicę, a niezależny recenzent uzyskał rzeczywisty RED na odizolowanym snapshotcie z Windows SDK 8.0.423: exit 1 i komunikat „Brak lub błąd RSS nie może pominąć końcowego limitu kanału YouTube.” Frozen worktree pozostał nietknięty. Naprawa ma zachować scalanie przed datami RSS, ale zapewnić końcowe sortowanie i limit także przy empty/failure RSS. Wąska korekta została przekazana wykonawcy na gałęzi `codex/amc-hubert25-rss-fallback-20261119`.

### Odbiór korekty RSS — `9700a613`, po przeglądzie bazowego SHA

Korekta `9700a6133b7157053cd44be5a36856134ef3644f`, baza `6f55a14b`, naprawia wskazaną granicę: przy pustym RSS kanał przechodzi końcowe sortowanie i limit 100, a playlista zachowuje swój dokument. Rodzic odczytał pełny delta-diff trzech plików (helper, test, aneks raportu), potwierdził czysty worktree i porównał bajtowo źródła helpera/testu ze stagingiem Windows.

Wykonawca wykazał RED na nowej asercji 200 kandydatów i GREEN po poprawce, wraz z buildem rzeczywistego LiteHost bez błędów i ostrzeżeń. Rodzic niezależnie uruchomił `Windows.SmokeTests --youtube-collection-normalization`: exit 0, „OK: ograniczone kanały i playlisty publicznego YouTube”. Hash DLL tego przebiegu: `44a60f3aa28dd6cafa033990ce7cced0cdf943be244289560c5066546edb8b44`. Test obejmuje pusty i uzupełniony RSS oraz zachowanie playlist. Korektę RSS odebrano na poziomie źródeł i celowanego testu; nie znosi to drugiego blokera bazowego kandydata i nie oznacza integracji, GUI ani wydania.

### Koszt zapisu po każdym kanale

Rodzic potwierdził ścieżkę z raportu recenzenta:

`LitePodcastRefreshCoordinator` → `LitePodcastProgressStore.ApplyRefresh` → `PodcastLibraryMutationStore` → `PodcastLibraryDatabase.ApplyRefresh` → `ApplyRefreshBatch([request])`.

Ostatnia metoda wywołuje `LoadInto(settings)` dla całej biblioteki, a następnie `Save(settings)`. `SaveCore` pobiera hashe i skanuje wszystkie subskrypcje oraz odcinki. Faktyczne upserty są warunkowe — **nie stwierdzono masowego UPDATE wszystkich odcinków**. Niemniej N sukcesów wymaga N pełnych materializacji, deserializacji i przebiegów fingerprintowania. Nie zmierzono konkretnego czasu ani spowolnienia na bibliotece użytkownika.

To koryguje wcześniejsze zapewnienie raportu wykonawcy o krótkim zapisie ograniczonym do kanału: samo wywołanie metody dla pojedynczego ID nie zapewnia wąskiej operacji DB.

Zlecono w `codex/amc-hubert25-target-store-20261119`, baza `6f55a14b`, minimalny zapis i odczyt ograniczony do celu. Wymagania: rzeczywisty RED/GREEN granicy zakresu, dowód zakresu odczytu/zapisu, zachowane tombstones, postępy, bookmarki, flagi, globalna kolejność oraz dane innych kanałów. Wynik pierwszego kanału nadal ma być trwale zatwierdzony przed pobieraniem następnego. Nie cofać tej ochrony do odroczonego batcha i nie dodawać globalnego cache ani przebudowy bazy.

## Odbiór korekty DB i integracja po przeglądzie

Niezależny delta-review `9611e851` względem `6f55a14b` zakończył się PRZYJĄĆ. Pełny raport w scratch: `hubert25-target-store-review-20261119.md`, delegacja `deleg_bd4eafc8`. Recenzent prześledził scoped SQL, wywołania Updatera i LiteHost, zachowanie danych, tombstones i rollback kolizji; nie ponawiał suite. Nie znalazł blockera. Rodzic odczytał cały raport oraz potwierdził zgodność blobów.

Obie korekty odebrano na wspólnym `ec34b3b` w czterech celowanych trasach Windows i rzeczywistym buildzie LiteHost bez ostrzeżeń/błędów. Merge kanoniczny `adf6e51c6880a03335fc082e6881e0363273c824` zachował 12 plików bajtowo zgodnych z tym kandydatem. Werdykt BLOCKED dla historycznego, niepoprawionego `6f55a14b` nadal opisuje tamten artefakt; nie jest bieżącą blokadą skorygowanego P0. [Szczegóły odbioru i granice](HUBERT25-P0-20261119-akceptacja.md).

## Przyjęte dowody bez ponawiania pełnego odbioru

- Trzy celowane RED/GREEN wykonawcy potwierdzono w raporcie i logu: Core `--podcast-library-update`, Windows `--youtube-collection-normalization`, Protocol `--podcast-progress`.
- Rzeczywisty LiteHost wykonawca zbudował bez ostrzeżeń i błędów.
- Rodzic dodatkowo wykonał Protocol `--podcast-progress`: exit 0, jeden zestaw PASS, po bajtowym porównaniu czterech plików źródłowych stagingu z kandydatem.
- Zmiana `IsNew` przy braku wspólnych ID jest we wspólnym Core i testuje datę po poprzednim sukcesie, datę starą oraz brak daty. Nie wykonano masowej migracji starych flag.
- Regresja przerwania i ponownego otwarcia DB dowodzi trwałości, ale nie dowodzi kosztu proporcjonalnego do kanału.

## Następny krok i ograniczenia

Odebrać obie wąskie korekty, sprawdzić ich delta-diff i właściwe dowody, a dopiero potem integrować #25 P0. Harmonogram, rozdziały i wznowienie pozostają w ustalonej kolejności. Nie publikowano, nie instalowano, nie uruchamiano GUI ani kont i nie zmieniano głównego komputera.

## Materiał do dokumentacji użytkownika

Przygotowywana poprawka ma zachować pobrane wyniki po zakończeniu każdego kanału także wtedy, gdy dalsze odświeżanie zostanie przerwane. Kontrola przed wdrożeniem obejmuje również niedostępny RSS i brak wielokrotnego przeliczania całej biblioteki. Nie jest to jeszcze funkcja dostarczona w wydaniu użytkowym. Nie zmienia skrótów ani sposobu obsługi programu.
