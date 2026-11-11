# YT-006 — wybór konkretnego wariantu pobrania

Status: **Kod i testy celowane gotowe; instalacja i test NVDA odłożone do aktualizacji aplikacji**. Decyzja użytkownika: 10.11.2026. Projekt i implementacja: 11.11.2026. Ten wynik zastępuje projektowy status „gotowy do implementacji” z 11.11.2026.

## Ustalenie użytkownika

Dostępny z klawiatury wybór konkretnego pliku lub strumienia YouTube, z czytelną informacją o jakości i formacie. Inspiracja: blindDL. Proste pobieranie zostaje.

## Stan źródeł AMC, sprawdzony 11.11.2026

- `Ctrl+S` zapisuje pojedynczy film jako plik, a `Ctrl+Shift+D` pobiera zaznaczone filmy. Ustawienie globalne wybiera MP4 albo najlepszy format oryginalny (`YT-004`). Osobna ścieżka audio `Ctrl+D` tworzy MP3.
- `YouTubeMediaDownloader` używa obecnie reguł yt-dlp `bv*+ba/b` dla wideo i `bestaudio[ext=m4a]/bestaudio/best[acodec!=none]` dla MP3. Nie ma operacji zwracającej ofertę wariantów. Istniejąca ścieżka wideo obsługuje etap tymczasowy i publikację pliku po sukcesie.
- `Ctrl+Shift+S` jest zajęty przez Menedżer sesji. Projekt nie przypisuje nowego skrótu bez przeglądu całej mapy poleceń. Dostęp z klawiatury zapewnią menu i paleta `Ctrl+Shift+K`.
- yt-dlp udostępnia maszynowy JSON `--dump-single-json` (`-J`) i jawny wybór `--format`. Połączenie oddzielnego obrazu i dźwięku jest odrębną operacją. Źródło: [dokumentacja yt-dlp](https://github.com/yt-dlp/yt-dlp/blob/master/README.md).

## Docelowa interakcja

1. Na **jednym** filmie YouTube użytkownik wybiera `Pliki > Wybierz wariant pobrania…` albo tę samą komendę w palecie. Komenda jest widoczna i aktywna tylko tam, gdzie pojedynczy materiał ma obsługiwaną ścieżkę YouTube; niedostępność ma wyjaśnienie w pomocy kontekstowej. `Ctrl+S`, `Ctrl+Shift+D` i `Ctrl+D` zachowują dotychczasowe znaczenie.
2. Okno ma tytuł zawierający nazwę filmu, informację „Pobieranie dostępnych wariantów…” podczas pracy i listę z fokusem na pierwszym **gotowym, kompletnym** wariancie po wczytaniu. Jeśli lista jest pusta, czytelny komunikat wyjaśnia przyczynę i daje `Ponów` oraz `Zamknij`.
3. Jeden wiersz to jeden wynikowy plik. Przykłady etykiet: `Wideo z dźwiękiem, 1080p, MP4, obraz H.264, dźwięk AAC, około 240 MB`; `Tylko dźwięk, M4A, 128 kb/s, rozmiar nieznany`; `Tylko obraz, WebM, 720p, bez dźwięku`. Wiersze złożone z dwóch strumieni mówią `połączenie obrazu i dźwięku, bez przekodowania`, gdy tak będzie wykonany zapis. Sam identyfikator formatu, URL, nazwa klasy i surowy obiekt nigdy nie są etykietą NVDA. Nieznane parametry są pomijane albo wyraźnie nazywane nieznanymi; nie zgadujemy rozmiaru ani kodeka.
4. Strzałki zmieniają zaznaczenie i odczytują pełną etykietę. `Enter` i przycisk `Zapisz wybrany wariant…` otwierają zwykły wybór nazwy i miejsca; `Escape`/`Anuluj` wracają do listy filmu bez pobierania. Po zapisaniu lub anulowaniu fokus wraca do tego samego filmu. Ponowne otwarcie może zachować wskazanie, o ile wariant nadal istnieje; po odświeżeniu oferta jest ogłaszana od nowa. Szczegóły techniczne są dostępne na żądanie, osobno od etykiety listy.
5. Początkowy zakres obejmuje jeden film i wynikowe wideo z dźwiękiem oraz oryginalny strumień audio albo wideo. Wariant bez dźwięku musi być nazwany wprost. Ścieżka `Ctrl+D` pozostaje szybkim zapisem MP3; nowa lista może oferować oryginalne audio, ale nie obiecuje MP3 bez jawnego przekodowania. Wybór jednego wariantu nie staje się ukrytym ustawieniem pobierania wielu filmów.

## Kontrakt hosta i pobrania

- Host pobiera JSON metadanych przez zarządzany yt-dlp dla jednego potwierdzonego identyfikatora filmu, z limitem czasu i wielkości odpowiedzi. Zwraca do GUI uporządkowane rekordy z oddzielnymi polami modelu; GUI tworzy **celową etykietę użytkową** z pól opisowych: formaty składowe, kodeki, kontener, wymiary, szybkość transmisji, szacowany rozmiar i potrzebę połączenia. GUI nie buduje etykiety z `ToString()` ani nie wyświetla wewnętrznych ID. Przefiltrować warianty niedostępne/DRM i duplikaty pozorne.
- Przed uruchomieniem pobrania host ponownie odczytuje ofertę. Wybrane ID i ważne właściwości muszą odpowiadać temu samemu materiałowi i wariantowi. Jeśli oferta się zmieniła, informuje `Wybrany wariant nie jest już dostępny. Wybierz ponownie`; nie przechodzi automatycznie na „best” ani inny format. Wygasający adres CDN nie jest przechowywany jako trwały wybór.
- Użyć istniejącej ścieżki pliku tymczasowego, walidacji i końcowej publikacji. Wynikowe rozszerzenie wynika z faktycznego kontenera. Połączenie strumieni wymaga dostępnego FFmpeg; odmówić z jasnym komunikatem, jeśli go brak. Nie przekodowywać ani nie remuksować do MP4 tylko dlatego, że globalny tryb prostego pobrania jest ustawiony na MP4; to jawny wybór konkretnego wariantu.
- Pobranie, przerwanie, błąd i kolizja nazwy używają istniejących komunikatów/procedur zapisu, lecz w postępie należy podać wybraną jakość i format. Nowe operacje hosta powinny współdzielić blokadę pobierania z `YT-004`.

## Obowiązkowe sprawdzenia przed uznaniem wdrożenia

- Test parytetu nazwy, opisu, dostępności i wykonania w modelu poleceń, menu, `Ctrl+F1`, pełnej pomocy, palecie i instrukcji, w języku polskim i angielskim oraz w kontekście jednego, wielu i żadnego filmu.
- Test danych: połączony plik, osobne strumienie, tylko audio, tylko obraz, brak rozmiaru/bitrate, duplikaty, zmiana oferty, wygaśnięcie adresu, błąd FFmpeg i brak cichej zamiany wybranego wariantu.
- Test klawiaturą i NVDA: pierwszy fokus, odczyt każdego wiersza, strzałki, Enter, Escape, wybór folderu, powrót fokusu i ponowne otwarcie. Test prawdziwego krótkiego pobrania dopiero po bezpiecznej weryfikacji aktywnej instalacji.

## Materiał do instrukcji i zmian — szkic

„Na pojedynczym filmie YouTube otwórz w menu Pliki polecenie «Wybierz wariant pobrania…» albo znajdź je w palecie `Ctrl+Shift+K`. Lista poda typ pliku, jakość obrazu i dźwięku oraz rozmiar, jeśli jest znany. Enter wybiera wariant i otwiera zapis pliku. Zwykłe pobieranie nadal działa bez tej listy.”

## Stan projektu przed implementacją

Projekt oparto na przeglądzie kodu AMC i dokumentacji yt-dlp. Wtedy nie pobrano żywej oferty YouTube ani nie wykonano testu NVDA, buildu, instalacji czy publikacji. Wielkości i kontenery w przykładach etykiet były ilustracyjne. Commit projektu: `24535a81`.

## Wynik implementacji 11.11.2026

- **Zachowanie:** na jednym filmie YouTube dostępne jest polecenie w menu Pliki, menu kontekstowym i palecie `Ctrl+Shift+K`, bez nowego skrótu. Okno listy pokazuje wyłącznie celowe etykiety formatu, jakości i rozmiaru, z początkowym fokusem na pierwszym wariancie. Enter otwiera zapis, Escape anuluje; po dialogu fokus wraca do filmu. Zwykłe pobieranie pozostaje odrębną komendą.
- **Host:** oferta yt-dlp ma limit czasu i wielkości; ponowne pobranie oferty i podpis parametrów odrzucają nieaktualny wybór. Format jest przekazany jawnie, bez cichej zamiany na najlepszy. Połączone strumienie są zapisywane do MKV bez przekodowania. Wynik trafia przez katalog tymczasowy do wskazanej ścieżki, bez nadpisania istniejącego pliku.
- **Sprawdzenie:** build LiteHost Release i C# SmokeTests Release zakończone bez ostrzeżeń i błędów; test wariantów C# zakończony kodem 0. Testy Python dotyczące wariantów, wideo, menu i palety: **78 zaliczonych** w przypiętym środowisku wxPython 4.3.1. Kontrola diff i składni GUI: bez błędów.
- **Ograniczenia:** nie pobrano żywej oferty ani pliku YouTube. Nie sprawdzono klawiatury w uruchomionym GUI i mowy NVDA. Komunikat rozpoczęcia pobierania zawiera opis wybranej jakości i formatu. Nie wykonano instalacji ani publikacji. Wersja zainstalowanej aplikacji: bez zmiany. Commit implementacji: `23cd981a`.
