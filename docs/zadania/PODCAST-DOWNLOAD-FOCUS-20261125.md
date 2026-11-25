# Podcasty: kursor wraca po zakończeniu pobierania Ctrl+D

Stan: zgłoszone 25.11.2026; przyczyna wskazana w kodzie; poprawka, testy i wydanie jeszcze niepowstałe.

**Aktualizacja 25.11.2026:** poprawka jest w źródłach gałęzi AMC Test.
Po Ctrl+D wynik pobierania zachowuje pozycję listy aktualną przy zakończeniu
pobierania, a druga asynchroniczna faza odświeżenia odczytuje ją ponownie tuż
przed podmianą danych. Dodano test przejścia z odcinka A na B w trakcie
pobierania. Skupione testy Pythona: 62/62. Ręczny odbiór NVDA na
zainstalowanej aplikacji nadal oczekuje. Ta aktualizacja zastępuje poniższy
stan „tylko diagnoza”; starszy tekst pozostaje jako historia zgłoszenia.

**Wydanie 25.11.2026:** poprawka należy do opublikowanych Test i Public alpha.531, commit `bc49032e`; 295 testów Pythona w łącznym zestawie przeszło. Odbiór kursora z NVDA w zainstalowanej aplikacji pozostaje otwarty. [Pełny rekord wydania](RELEASE-ALPHA531-20261125-Test-Public.md).

## Zgłoszenie i zachowanie użytkowe

W sesji Podcasty i YouTube użytkownik naciska `Ctrl+D` na odcinku, a podczas pobierania idzie dalej po liście. Gdy pobieranie się kończy, kursor wraca na odcinek, na którym rozpoczęto pobieranie. To przerywa czytanie i nawigację.

Oczekiwane zachowanie: wynik pobierania ma być ogłoszony bez odbierania bieżącego zaznaczenia. Jeśli użytkownik przeszedł do innej sesji, odtwarzacza lub widoku, ukończenie zadania nie może przywrócić starej listy ani fokusu. Gdy lista wymaga odświeżenia, powinna zachować element wskazywany *w chwili zakończenia* lub najbliższego widocznego sąsiada, jeśli ten element zniknął.

## Diagnoza ze źródeł

W `wxlite/amc_wx_lite/gui.py`, `_download_podcast_episodes`, `preferred_id = rows[0].item_id` jest ustalany przed rozpoczęciem pracy w tle. Po zakończeniu `done` wywołuje `_reload_podcast_list_after_download(preferred_id, message)`. Ten drugi kod przekazuje stare ID do ponownie otwartej listy odcinków lub widoku zbiorczego. Jest to bezpośrednie wyjaśnienie cofnięcia zaznaczenia zgłoszonego przez użytkownika; rzeczywisty przebieg NVDA nie był w tym zadaniu testowany.

## Uzasadnienie i decyzje

Zgłoszenie dotyczy asynchronicznej zmiany danych, więc wybór użytkownika może się zmienić podczas pobierania. W przyszłej poprawce należy odróżnić pozycję pobieranego odcinka od aktualnego zaznaczenia i użyć aktualnego stanu listy przy odświeżeniu. Potwierdzenie pobrania powinno nastąpić po odświeżeniu, bez powtórnej mowy nazwy starego wiersza.

## Testy i wynik

Wykonano tylko odczyt kodu; nie zmieniono działania, nie uruchomiono testów ani AMC. Do poprawki dodać test z odroczonym zakończeniem pobierania: rozpocząć `Ctrl+D` na A, przejść na B, zakończyć zadanie i potwierdzić, że B pozostaje wybrane. Osobno sprawdzić zmianę widoku/sesji, pojedynczy i wielokrotny wybór, filtrowanie oraz komunikat NVDA po odświeżeniu.

## Znane ograniczenia

Nie ustalono jeszcze, czy problem występuje również w innych komendach pobierania. Nie wykonano próby na zainstalowanym AMC.

## Materiał do dokumentacji użytkownika

Do szkicu po wykonaniu poprawki: „Po uruchomieniu pobierania odcinka przez `Ctrl+D` możesz dalej przeglądać listę. Po zakończeniu pobierania AMC poda wynik i pozostawi kursor tam, gdzie aktualnie czytasz”. Ten opis nie jest jeszcze obowiązującym zachowaniem wydanej wersji.

## Wersja i commit

Zgłoszenie zapisano w commicie dokumentacji `d08b5ed813e703d94072f168aa4876e7a85797b0`, po opublikowaniu AMC Test alpha.528 (`test-2026.11.25.0200`). Użytkownik później zainstalował tę wersję; lokalny manifest wskazuje commit `4ba3ea7d`. Poprawka nie należy do alpha.528.

Następny krok: poprawić wybór po asynchronicznym zakończeniu `Ctrl+D`, dodać test odroczonego zakończenia i wykonać odbiór NVDA.
