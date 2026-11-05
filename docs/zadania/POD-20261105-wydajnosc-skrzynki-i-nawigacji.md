# POD-20261105 — lekka skrzynka podcastów i spójny powrót

Status: lokalny kandydat alpha.478 zainstalowany, oczekuje na odbiór NVDA użytkownika, 5 listopada 2026. To nowe zgłoszenie użytkownika następuje po lokalnej instalacji alpha.477. Nie zastępuje zaakceptowanej funkcji doładowywania z YT-005; koryguje jej koszt na dużej bibliotece i zabezpiecza spóźnione wyniki.

## Zakres i decyzje użytkownika

Użytkownik prosi o sprawdzenie skrzynki podcastów oraz dostępnych narzędzi listowych pod kątem szybkiej prezentacji, nawigacji Enter/powrót i spójności między sesjami. Interfejs ma pozostać lekki. Zachowujemy istniejące polecenia i wybór porcji po 150; optymalizacja nie może zmienić kolejności ani punktu powrotu.

## Materiał do dokumentacji użytkownika

- `Ctrl+I` z dowolnej sesji otwiera **Nowe odcinki i materiały**. Ponowne `Ctrl+I` w otwartej skrzynce doładowuje następne 150 pozycji i ustawia fokus na pierwszej nowej. Enter na **Załaduj więcej odcinków** robi to samo. Dane pochodzą z zapisanej biblioteki; samo wejście na listę nie odświeża źródeł w sieci.
- Strzałki góra/dół poruszają się po gotowej liście. Enter na odcinku otwiera odtwarzacz, Escape wraca do tego samego wiersza skrzynki, a kolejne Escape wraca do poprzedniej sesji i zaznaczonej pozycji. Spóźniony odczyt skrzynki nie otwiera jej ponownie po Enter ani po wyjściu.
- `Alt+1`, `Alt+2`, `Alt+3` wybierają odpowiednio najnowsze, alfabetyczne lub według podcastu. Brak odcinków jest czytany jako „Nowe odcinki i materiały, brak nowych materiałów”. Dostępne komendy są opisane w menu, palecie, `Ctrl+F1` i pełnej pomocy klawiatury przez wspólny katalog.

## Uzasadnienie i decyzje

Na realnej bibliotece 28 709 nowych odcinków stare otwarcie pierwszej porcji materializowało 28 709 rekordów JSON i sortowało całość. W porządku najnowszych SQLite wybiera teraz tylko widoczną porcję oraz ewentualny remis daty na jej granicy, a osobno liczy wszystkie pozycje. Indeks skrzynki obsługuje datę; pełny remis nadal sortuje host C#, co zachowuje polską kolejność tytułów. Tryby alfabetyczny i według podcastu czytają do sortowania jedynie ID, tytuł, nazwę podcastu i datę, a pełne dane pobierają dla widocznej porcji. Wszystkie zapytania GUI otwierają bazę tylko do odczytu.

Wynik asynchroniczny jest stosowany tylko, gdy użytkownik nadal ogląda tę skrzynkę jako listę. Testy powrotu obejmują wszystkie sesje. Przegląd innych widoków zbiorczych wykazał, że Ulubione, Historia, Kolejka i Pobrane są znacznie lżejsze w obecnej bibliotece; zmiana ich algorytmów nie jest potrzebna.

## Testy i wynik

- Odczytowy pomiar prawdziwej bazy, bez tytułów w wyniku: stary pełny odczyt skrzynki 1,004 s i 28 709 rekordów; nowy pierwszy ekran 0,124 s i 150 wierszy. Zmierzony nowy widok alfabetyczny 0,235 s, według podcastu 0,249 s, bez żywej kolacji hosta. To pomiar etapu źródła, nie pełnej mowy NVDA.
- Pozostałe widoki na tym samym profilu: Ulubione 0,102 s, Historia 0,023 s, Kolejka 0,007 s, Pobrane 0,012 s. Pobrane były puste w chwili pomiaru.
- Wspólna regresja skrzynki, granicy strony, trzech porządków, odrzuconego spóźnionego wyniku, Enter/Escape i powrotu do wszystkich sesji oraz reprezentatywnych list, menu i palety: 211 testów zaliczonych. Test granicy potwierdza pobranie wszystkich remisów daty i wyłącznie 150 pełnych rekordów w dwóch innych trybach.

## Znane ograniczenia

Przy rzadkim przypadku, gdy tysiące odcinków mają identyczną datę na granicy strony, trzeba pobrać cały remis, aby zachować dokładną kolejność hosta. Pierwszy wybór alfabetyczny przy bardzo dużej skrzynce nadal musi posortować lekkie dane wszystkich odcinków; następna nawigacja po wczytanej porcji nie uruchamia sieci ani bazy. Rzeczywista szybkość NVDA po instalacji wymaga odbioru użytkownika.

## Wersja i commit

Źródła i testy: commit `a1003fe99617595505834acc0d705d831044c478`, repozytorium czyste po commicie. Build pakietu: zaliczony kontrolowany start interfejsu i hosta, manifest z `sourceDirty=False` i tym commitem. Instalator `D:\Projekty Codex\AMC - integracja wxPython\publish\installers\AMC-Test-2026-11-05-2151.exe`, 143 840 853 bajty, SHA256 `D37722323F8DB5E1429BE1E6A7BD0725B99A0461BCEBFE6532F08D75E3E24A2D`; instalacja kod wyjścia 0. Uruchomione okno ma tytuł `AMC Test — 05.11.2026, 21:51`. Próba w zainstalowanym GUI: `Ctrl+I` pokazuje 150 pozycji i fokus listy, strzałka w dół działa, Escape przywraca listę Radia. Test mowy i subiektywnej szybkości NVDA należy do odbioru użytkownika. GitHub nieopublikowany do tego odbioru.

## Szkic instrukcji i listy zmian do zatwierdzenia

Instrukcja: Naciśnij `Ctrl+I` w dowolnej sesji, aby otworzyć skrzynkę. Strzałkami wybierz odcinek i naciśnij Enter. Escape wraca do tego samego odcinka, kolejne Escape do sesji, z której otwarto skrzynkę. Ponowne `Ctrl+I` albo Enter na „Załaduj więcej odcinków” wczytuje następne 150 pozycji. `Alt+1`, `Alt+2`, `Alt+3` zmieniają porządek listy.

Lista zmian: Skrzynka otwiera pierwszą porcję bez pełnego odczytu wszystkich odcinków. Nawigacja po porcji nie pobiera danych. Spóźniony wynik ładowania nie przenosi fokusu z odtwarzacza lub innej sesji do skrzynki. Ten szkic wymaga oceny użytkownika po teście NVDA i nie jest opublikowaną instrukcją wydania.
