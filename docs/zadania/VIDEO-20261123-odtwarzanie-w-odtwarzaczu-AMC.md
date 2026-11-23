# VIDEO-001 — obraz lokalnego filmu w odtwarzaczu AMC

- Data: 23.11.2026.
- Status: kod i testy celowane gotowe w AMC Test; widoczny odbiór obrazu, NVDA, build, instalacja i publikacja pozostają otwarte.
- Baza: wydana `0.1.0-alpha.513`; aktywna gałąź `codex/alpha512-update-candidate-20261122`.
- Commit bazowej funkcji: `4d0d775832006f264fc2a9e24e4f3833af454359`; późniejsza bramka Test/Public: do uzupełnienia.
- Kanoniczny checkout D po scaleniu historii: `codex/amc-current-d-20261123`, commit scalający `faf67433`.

## Decyzja użytkownika i zachowanie

23.11.2026 Michał zastąpił wstępny wariant osobnego okna wideo wyborem „Odtwarzaj wideo” / „Odtwarzaj tylko audio” w tym samym odtwarzaczu AMC. Obraz jest częścią istniejącego panelu. Przycisk odtwarzania, czas, głośność, prędkość, strzałki, kolejka, zakładki i nawigacja pozostają tymi samymi funkcjami AMC. Enter na pliku wideo zachowuje domyślne uruchomienie samego audio. W sesji Pliki polecenia są dostępne na istniejącym lokalnym pliku z rozszerzeniem wideo, także w Pobranych plikach. Wybranie „Odtwarzaj wideo” pokazuje obraz; „Odtwarzaj tylko audio” chowa obraz i zostawia odtwarzanie dźwięku.

Osobne doprecyzowanie Michała z 23.11: najlepiej obsłużyć również YouTube online, ale przy trudności z osadzeniem wystarcza pobrany plik. Dlatego w sesji Podcasty i YouTube dodano „Otwórz w przeglądarce” dla zaznaczonego filmu z poprawnym publicznym adresem HTTPS. Polecenie przekazuje dokładny adres filmu do domyślnej przeglądarki systemowej po świadomym uruchomieniu polecenia. Dotyczy rozpoznanych pozycji YouTube, Facebooka i Instagrama. Nie dotyczy Spotify, TIDAL ani Apple Music. Odtwarzania YouTube online wewnątrz AMC w tym etapie nie dodano.

Korekta wydania z 23.11.2026: Michał przeznaczył obraz wideo na razie do AMC Test. Polecenia obrazu i otwarcia filmu w przeglądarce są ukryte w Public przez wspólną możliwość profilu `local-video-preview`; Public zachowuje poprawki wyszukiwania i harmonogramu. Decyzja zastępuje wcześniejszy brak rozdzielenia profili dla tej funkcji.

## Polecenia, miejsca i komunikaty NVDA

| Polecenie | Miejsce | Skrót | Opis w pomocy |
| --- | --- | --- | --- |
| Odtwarzaj wideo | Pliki: menu, kontekst listy i odtwarzacza, paleta, pomoc | Brak | Odtwarzaj zaznaczony lokalny film z obrazem w obecnym odtwarzaczu AMC |
| Odtwarzaj tylko audio | Te same miejsca | Brak | Odtwarzaj zaznaczony lokalny film tylko jako dźwięk |
| Otwórz w przeglądarce | Podcasty i YouTube: menu, kontekst listy, paleta, pomoc | Brak | Otwórz stronę zaznaczonego filmu w domyślnej przeglądarce |

Paleta jest dostępna pod `Ctrl+Shift+K`, pomoc klawiatury pod `Ctrl+F1`; istniejące skróty transportu nie zmieniają się. Nazwy i opisy nowych poleceń pochodzą ze wspólnego modelu `CommandPresentation`; stany dostępności używają tych samych warunków w menu, kontekście i palecie. Obraz ma nazwę dostępną „Obraz odtwarzanego wideo”, ale nie przejmuje fokusu od przycisku odtwarzania. Po rozpoczęciu obrazu AMC mówi „Wyświetlanie wideo”; przy braku obrazu „Nie można wyświetlić obrazu tego pliku. Dźwięk pozostaje w AMC”; po przejściu na samo audio „Odtwarzanie tylko dźwięku”. Błędny wybór otrzymuje „Wybierz dostępny lokalny plik wideo” albo „Wybierz film internetowy z poprawnym adresem”. Te komunikaty są sprawdzone kodem i testami; rzeczywista mowa NVDA wymaga odbioru.

## Uzasadnienie i zależności

Wykorzystano `wx.media.MediaCtrl` z już zainstalowanego wxPython; nie dokładano kolejnego środowiska odtwarzacza do instalatora. Obraz jest wyciszony. Dotychczasowy host C# odtwarza dźwięk i pozostaje właścicielem stanu transportu. Panel wideo odczytuje jego bieżącą pozycję, pauzę i tempo, korygując większy dryf. Po opuszczeniu widoku lub wybraniu audio dekoder obrazu zatrzymuje się. Wykrycie błędu dekodera pozostawia dźwięk i zgłasza ograniczenie.

Odtwarzanie YouTube osadzone w AMC wymagałoby osobnego odtwarzacza przeglądarkowego i spełnienia zasad odtwarzacza YouTube. Wywołanie zwykłej przeglądarki na żądanie użytkownika realizuje obecny przypadek bez obchodzenia jej interfejsu.

## Testy i rzeczywisty wynik

- Jedenaście testów `wxlite/tests/test_video_display.py`: **11/11 PASS** w runtime AMC Python 3.14.7 / wxPython 4.3.1. Obejmują wybór pliku, walidację dokładnej strony wideo, zgodność nazw/opisów/skrótów, trasę dispatchera, dostępność według zaznaczenia, użycie tego samego nawigatora i `PlayTrack`, synchronizację widoczności z hostem, przekazanie adresu do przeglądarki, powrót do audio po błędzie oraz izolację profilu Test/Public.
- Syntetyczny MP4: `wx.media.MediaCtrl.Load` zwrócił sukces i długość 3000 ms w ukrytym oknie; obraz ani zachowanie w widocznym odtwarzaczu nie zostały przez tę próbę potwierdzone.
- Po połączeniu źródeł na D: **159 testów i 12 podtestów zaliczonych** dla wideo, profili Test/Public, wyszukiwania i harmonogramu. Oddzielnie 86 testów i 10 podtestów profili, menu i palety zaliczonych.
- Celowana regresja menu, palety, tłumaczeń, podcastów i plików: **142 zaliczone, 1 pominięty, 10 podtestów zaliczonych, 2 niezaliczone**. Dwa niezaliczone testy kompletności angielskich tłumaczeń zawodzą także na zastanym checkoutcie D, przed przeniesieniem tej poprawki. Nie są dowodem regresji wideo, ale nie wolno uznać całej regresji za zieloną.
- `git diff --check` bez błędów; nie uruchomiono pełnej regresji, kompilacji, instalatora, instalacji ani publikacji.

## Ograniczenia i odbiór

- Obsługa formatów zależy od dekoderów dostępnych w Windows. Rozszerzenia MP4, M4V, MOV, AVI, WMV, WebM i MKV oznaczają możliwość wyboru, nie gwarantują dekodowania obrazu. Szczególnie oryginalny WebM/MKV pobrany z YouTube wymaga rzeczywistej próby. Przy braku obrazu AMC ma pozostawić audio.
- Osobny dekoder obrazu jest synchronizowany ze stanem hosta okresowo; płynność, opóźnienie po przewijaniu i tempo inne niż 1× wymagają próby na rzeczywistym pliku.
- Wideo bez ścieżki audio zależy od możliwości obecnego hosta plików. Taki przypadek nie został potwierdzony.
- Odbiór: krótki MP4 z audio, pobrany WebM/MKV, pauza/wznowienie, strzałki, przewijanie, zmiana tempa, przełączanie audio/wideo, fokus przycisku, powrót Escape, ponowne otwarcie, NVDA na menu/kontekście/palecie/pomocy oraz otwarcie dokładnego filmu YouTube w zwykłej przeglądarce. W systemie działa już AMC z hostem; nie podmieniano go ani nie uruchamiano widocznej drugiej instancji.
- Dopiero po odbiorze można budować i oceniać instalator AMC Test. Public powinien wykluczać Apple Music zgodnie z procesem profilu. Brak nowego wydania GitHub w tym zadaniu.

## Projekt instrukcji użytkownika do korekty

> W Plikach zaznacz pobrany lub lokalny film. W menu kontekstowym wybierz „Odtwarzaj wideo”, aby oglądać go w obecnym odtwarzaczu AMC z tymi samymi przyciskami i strzałkami. „Odtwarzaj tylko audio” pozostawia sam dźwięk. W Podcastach i YouTube polecenie „Otwórz w przeglądarce” otwiera wybrany film na jego stronie.

## Projekt opisu zmian do korekty

> Dodano wybór obrazu lub samego audio dla lokalnych filmów w obecnym odtwarzaczu AMC oraz otwieranie wskazanego filmu internetowego w domyślnej przeglądarce.
