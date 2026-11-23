# VIDEO-002 — YouTube online w odtwarzaczu AMC: ocena wykonalności

- Data: 23.11.2026.
- Stan: po późniejszej zgodzie użytkownika wdrożono adapter YouTube online w źródłach AMC Test; wynik testów i wydania podano w końcowym zapisie poniżej.
- Baza: bieżąca aplikacja wxPython na D; najnowsze wydanie Test `test-2026.11.23.0147` zawiera tylko obraz lokalnych i pobranych plików wideo.
- Korekta późniejszej decyzji z 23.11.2026: użytkownik ponownie zlecił ocenę YouTube online. Zastępuje to wcześniejsze odłożenie strumieniowego obrazu opisane w VIDEO-001, ale nie zmienia gotowego zachowania dla plików lokalnych.

## Wynik i propozycja

**Jest to wykonalne technicznie.** Na Windows AMC ma już wxPython 4.3.1 z `wx.html2.WebView`, natywnym backendem Edge WebView2, biblioteką `WebView2Loader.dll` i zainstalowanym WebView2 Runtime. Nie potrzeba osobnej biblioteki dekodującej YouTube. Wariant docelowy to oficjalny YouTube IFrame Player API w panelu obecnego odtwarzacza AMC. Obraz i dźwięk online mają pochodzić z jednego odtwarzacza YouTube, a przyciski AMC, strzałki, kolejka i stan mają sterować nim przez adapter transportu; nie wolno równolegle uruchamiać dźwięku z hosta plików ani wyciszać filmu w celu odtworzenia samego audio z innego źródła.

Na początek funkcja tylko w AMC Test. „Odtwarzaj wideo” przy niepobranym materiale YouTube powinno otworzyć w tej samej zakładce panel oficjalnego odtwarzacza. Wybór lokalnego lub pobranego pliku pozostaje w dotychczasowym `wx.media.MediaCtrl`. „Otwórz w przeglądarce” pozostaje opcją, gdy film nie pozwala na osadzenie lub WebView2 nie działa. Nie obejmować Facebooka, Instagrama, TIDAL, Spotify ani Apple Music.

## Sprawdzenie na tym komputerze

- `wx.version()` zwrócił `4.3.1 msw (phoenix) wxWidgets 3.3.3`; `wx.html2.WebView.IsBackendAvailable(WebViewBackendEdge)` zwrócił `True`.
- Izolowana, niewidoczna kontrolka WebView2 z odrębnym profilem utworzyła się (`created True`). Lokalna strona HTML pod wirtualnym adresem HTTPS bazującym na stałym AppId instalatora AMC Test załadowała się.
- Żądanie HTTPS z tej strony podało `Referer: https://amc-test.0b807571-bdc5-4287-bd7c-4f4a7c71438f/`. To sprawdzono przez zewnętrzny serwis diagnostyczny nagłówków; żadne dane użytkownika, konta ani lokalne pliki nie były przekazywane. Próba żądania HTTP do localhost nie miała Referer, zgodnie z zasadą nieprzekazywania go przy zejściu z HTTPS do HTTP — nie należy jej używać do oceny żądań YouTube.
- Oficjalny skrypt `https://www.youtube.com/iframe_api` zgłosił `api-ready`, `player-ready`, a przykładowy film z dokumentacji YouTube przeszedł do stanu `5` (przygotowany). Nie było `onError`. W próbie nie wywołano `playVideo`, więc nie potwierdzono obrazu, dźwięku, autoodtwarzania, kolejki, skrótów ani współpracy z NVDA.
- Próba używała obszaru 640×360 px i nie zmieniała istniejącego uruchomionego AMC ani jego profilu WebView2. Powstał wyłącznie odrębny profil diagnostyczny w katalogu tymczasowym systemu.

## Warunki wdrożenia

1. Zachować stały identyfikator aplikacji osobno dla Test i Public oraz wirtualny origin HTTPS. Potwierdzić, czy obecny Inno Setup `AppId`, zarejestrowany podczas instalacji, jest wystarczającym identyfikatorem aplikacji w rozumieniu aktualnych zasad YouTube; to kwestia zgodności wymagająca osobnej oceny przed wydaniem funkcji.
2. Używać Edge WebView2 jawnie, nie domyślnego starego backendu IE. Obsłużyć brak WebView2 i błędy osadzenia z czytelnym komunikatem oraz poleceniem otwarcia dokładnego filmu w zwykłej przeglądarce.
3. Utrzymać widoczny oryginalny odtwarzacz z kontrolkami i marką YouTube, co najmniej 200×200 px, zalecane 480×270 px. Nie zasłaniać elementów YouTube. Rozpoczęcie odtwarzania dopiero po jawnym działaniu użytkownika i po pokazaniu panelu.
4. Dodać adapter transportu IFrame API do wspólnego modelu poleceń i stanu AMC. `play/pause`, przewijanie, czas, głośność, następny/poprzedni element, zakończenie filmu, zamknięcie panelu i zmiana sesji muszą mieć ten sam sens co w pozostałych źródłach. Nie używać istniejącego zegara hosta audio jako źródła czasu dla filmu YouTube. Sprawdzić, czy tempo i zakładki mają sens; nie reklamować niedziałających poleceń.
5. Wspólnie zmienić menu, kontekst, paletę `Ctrl+Shift+K`, pomoc `Ctrl+F1`, dokumentację i testy parytetu. Sprawdzić NVDA: etykiety, początkowy fokus, strzałki, przejście do/od webview, Escape, ponowne otwarcie. Testować film bez możliwości osadzenia oraz brak internetu.
6. Nie projektować osobnego „tylko audio” online z ukrytym odtwarzaczem YouTube ani nie pobierać strumieni do `wx.media`: polityka YouTube zakazuje rozdzielania audio i wideo oraz odtwarzania z niewidocznego odtwarzacza. Lokalny plik już znajdujący się w bibliotece jest odrębnym przypadkiem AMC.

## Źródła

- [YouTube — Required Minimum Functionality](https://developers.google.com/youtube/terms/required-minimum-functionality): WebView desktop, Referer, identyfikator aplikacji, rozmiar i autoplay.
- [YouTube — IFrame Player API](https://developers.google.com/youtube/iframe_api_reference): sterowanie, zdarzenia i stany odtwarzacza.
- [YouTube — Complying with Developer Policies](https://developers.google.com/youtube/terms/developer-policies-guide): kontrolki, separacja audio/wideo, odtwarzanie w tle.
- [wxPython — wx.html2.WebView](https://docs.wxpython.org/wx.html2.WebView.html): Edge WebView2, wirtualne hosty, skrypty i ograniczenie `SetPage(baseUrl)` na Edge.

## Projekt instrukcji i opisu zmian do późniejszej korekty

Instrukcja: „W sesji Podcasty i YouTube zaznacz film YouTube i wybierz «Odtwarzaj wideo». Film otworzy się w panelu odtwarzacza AMC. Gdy materiał nie pozwala na osadzenie, użyj «Otwórz w przeglądarce».”

Lista zmian: „Dodano odtwarzanie strumieniowych filmów YouTube przez oficjalny odtwarzacz w panelu AMC Test, z obsługą przycisków i nawigacji AMC.” **Projekt wyłącznie dla przyszłego wdrożenia; nie opisuje aktualnego wydania.**

## Wdrożenie po zgodzie Michała z 23.11.2026

Późniejsze polecenie „Tak, oczywiście, podłączaj” zastąpiło stan „ocena bez integracji” z początku dokumentu. Kod w gałęzi Test osadza oficjalny IFrame Player API w `wx.html2.WebView` z backendem Edge, w dotychczasowym panelu Podcastów i YouTube. Lokalny i pobrany plik nadal używa `wx.media`. Wybór „Odtwarzaj wideo” na internetowym filmie z poprawnym adresem przechodzi do tego samego odtwarzacza AMC. Przyciski i istniejące skróty kierują pauzę, wznowienie, przewijanie, skoki, głośność, wyciszenie, tempo oraz poprzedni/następny element listy do adaptera. Czas pochodzi z YouTube IFrame API, nie z zatrzymanego hosta audio. Wyjście z panelu i zmiana sesji pauzują niewidoczny film. Błąd osadzenia wraca na listę z komunikatem o poleceniu „Otwórz w przeglądarce”.

Polecenia zapisujące zakładki, rozdziały, historię odtwarzania i wycinki wymagają obecnie osi czasu hosta audio. W trybie filmu YouTube online są wyłączone w menu, palecie i pomocy; wywołanie skrótu daje komunikat „To polecenie nie jest dostępne podczas oglądania YouTube online”. Lokalny i pobrany plik nadal ma te funkcje. Samodzielne „Odtwarzaj tylko audio” dla strumienia YouTube nie jest oferowane.

### Komendy, miejsca, dostępność

| Polecenie | Miejsce | Zachowanie w Test | Public |
| --- | --- | --- | --- |
| Odtwarzaj wideo | Menu Odtwarzanie, menu kontekstowe, paleta i pomoc; bez nowego skrótu | Na filmie online otwiera oficjalny odtwarzacz w panelu Podcastów; na pliku lokalnym odtwarza obraz z pliku | Ukryte |
| Odtwarzaj tylko audio | Te same miejsca; bez skrótu | Dostępne na pliku lokalnym lub pobranym, niedostępne dla YouTube online | Ukryte |
| Otwórz w przeglądarce | Menu i kontekst listy, paleta i pomoc; bez skrótu | Otwiera wybraną stronę filmu po jawnym poleceniu | Ukryte |
| Spacja, strzałki, Home/End, cyfry, Page Up/Page Down, głośność, wyciszenie, tempo, odczyt czasu | Istniejący odtwarzacz i pomoc klawiatury | Sterują filmem online przez adapter | Bez nowego trybu online |

### Odbiór i ograniczenia

- Izolowana, niewidoczna próba WebView2: `ready=True`, stan `5`, bez błędu.
- Izolowana, wyciszona widoczna próba oficjalnego przykładowego filmu: `ready=True`, następnie stany `3` (buforowanie) i `1` (odtwarzanie), bez `onError`; zrzut ekranu pokazuje rzeczywisty obraz filmu. Nie podmieniono działającego AMC i nie przejęto jego fokusu.
- Testy modułu, nawigacji, wideo lokalnego i menu: **79 passed, 49 subtests passed**; wynik dotyczy źródeł przed pełnym buildem. `compileall` i `git diff --check` przeszły.
- Nie przeprowadzono jeszcze żywego odbioru całej zakładki AMC z NVDA. Działający AMC pozostawiono nietknięty, ponieważ stan nagrywania nie został wiarygodnie potwierdzony. Rzeczywiste filmy mogą odmawiać osadzenia; wtedy działa polecenie otwarcia w przeglądarce.
- Nowe moduły wideo są zależne od możliwości profilu `local-video-preview`; pakowarka Test je dołącza, Public usuwa również stare kopie po aktualizacji. Nie dodano zewnętrznej biblioteki wideo ani osobnego dekodera YouTube.

### Szkic instrukcji i listy zmian do korekty użytkownika

Instrukcja: „W Podcastach i YouTube zaznacz film YouTube i wybierz «Odtwarzaj wideo». Obraz otworzy się w dotychczasowym odtwarzaczu AMC Test. Spacja zatrzymuje i wznawia, strzałki przewijają, Page Up i Page Down przechodzą po liście. Escape wraca do listy i pauzuje film. Jeżeli YouTube odmówi osadzenia, wybierz «Otwórz w przeglądarce» na liście.”

Lista zmian: „AMC Test: film YouTube online w panelu odtwarzacza, ze wspólnymi kontrolkami transportu. Lokalny i pobrany film nadal może odtwarzać sam dźwięk albo obraz. Uzupełniono status, pomoc i obsługę błędów osadzenia.”
