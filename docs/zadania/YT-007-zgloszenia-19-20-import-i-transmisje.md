# YT-007 — zgłoszenia Huberta #19 i #20: import i transmisje

- Status: `Źródła zintegrowane i celowane testy zaliczone; odbiór użytkowy i wydanie wstrzymane bramką bezpieczeństwa`.
- Rozpoczęcie i integracja: 17 listopada 2026.
- Polecenie: „To Hubert znowu? Sprawdź oba zgłoszenia, wdrożyc je”. Pełne zgłoszenia i komentarze odczytano; oba napisał `HubertMeyer`.
- Repozytorium: `michalkasperczak/AMC`, gałąź `codex/amc-test-public-20261109`, baza `cbad568eaabcea7f733dcb1e62e43ed232b1afcb`.

## Potwierdzenie Michała — 19.11.2026

„Potwierdzam, YouTube jest znacznie lepiej, są transmisje, które wcześniej
były pomijane.” To odbiór poprawy widoczności transmisji w używanym przez niego
AMC, nie benchmark ani potwierdzenie wszystkich kryteriów #19/#20. W tej
wiadomości nie podał wersji, SHA ani konkretnych kanałów; nie przypisujemy
obserwacji do niezweryfikowanego artefaktu. Import, natywny wx/NVDA i pozostałe
scenariusze zachowują osobny stan odbioru. Nie przebudowywać działającej drogi
listowania z powodu tego pozytywnego potwierdzenia. Starsze informacje niżej
o braku odbioru użytkowego nie obejmują już tego potwierdzonego zakresu.

## Zakres i kryteria odbioru

1. Import CSV/ZIP Takeout, JSON NewPipe i tekstowego FreeTube profiles.db zgodnego z eksportem AMC; świadomy wybór przed zapisem, stabilne ID, ochrona istniejących danych i brak masowego pobierania.
2. OPML: rzeczywisty typ kanału/playlisty YouTube, zachowane RSS i zgodność z eksportem AMC.
3. Limity wejścia/tekstu/liczby wpisów; brak ekstrakcji ZIP na dysk, wykonywania HTML i importu historii oglądania/kont/sekretów.
4. #20: rzeczywisty pomiar, niezależne statusy/daty i polskie tytuły łączone po ID; bez włączania live/upcoming do archiwum na podstawie czasu trwania.
5. Publiczne live/upcoming przez yt-dlp bez konta, zachowana oficjalna ścieżka połączonego konta, częściowe błędy, F5 i limit 20 wybranych kanałów.
6. Wspólny kontrakt skrótów/menu/palety/pomocy/instrukcji; osobny odbiór natywnego wx/NVDA.
7. Testy źródłowe na nieszkodliwych fixtures bez AMC, instalatora, ISCC, dotkniętych kandydatów, danych użytkownika ani zmian ochrony.

## Uzasadnienie i decyzje

Nie wdrożono nieoficjalnego logowania cudzym klientem TV: jest sprzeczne z decyzją YT-001 i niepotrzebne do importu/publicznych transmisji. Ta część #19 nie jest ukończoną funkcją. Nie zamykać całych zgłoszeń przed użytkowym odbiorem.

Zachowano Action.IMPORT_PODCAST_OPML, Ctrl+O i podcast.opml.inspect/import. Podgląd nadal zwraca title, feedUrl, label, count; dodano sourceKind: rss, youtubeChannel, youtubePlaylist. Import nadal selekcjonuje po feedUrls.

YouTube zachowuje wielkość liter stabilnych ID; RSS wcześniejsze porównania bez rozróżniania liter. Jawna flaga preserveExistingMetadata chroni placeholderowy import YouTube, ale nie blokuje aktualizacji metadanych przez prawdziwy refresh.

Dwa osobne --extractor-args dla youtubetab:approximate_date i youtube:lang=pl prawidłowo rozdzielają przestrzenie ekstraktorów. Dawne połączenie youtubetab:approximate_date;youtube:lang=pl błędnie umieszczało youtube:lang w youtubetab. Daty approximate_date pozostają przybliżone.

## Wykonane źródła

- Takeout CSV i ZIP (tylko właściwy subscriptions/subscriptions.csv), NewPipe JSON i rekordy FreeTube profiles.db; podgląd bez zapisu, import tylko zaznaczenia, źródła bez masowego pobierania.
- OPML rozpoznaje kanały/playlisty z feedów YouTube; pozostałe źródła pozostają RSS.
- Limity: 1000 wpisów, tekst do 4 Mi znaków, ZIP do 64 MiB, 128 plików, głębokość ścieżki 8 i ograniczony współczynnik kompresji. Parser ogranicza też rzeczywisty strumień non-seekable, nie zamyka strumienia wywołującego. Dla większego Takeout trzeba wskazać sam CSV.
- #20: statusowy odczyt /streams bez lokalizacji; drugi, ograniczony odczyt lokalizowany uzupełnia wyłącznie tytuł po ID. Zachowane status i data, również przy brakach/błędzie drugiego odczytu. Sam dodatni czas trwania nie dowodzi zakończenia transmisji.
- Publiczna lista tylko is_live/is_upcoming, bez OAuth/cookies, maksymalnie 50 pozycji karty /streams; nie gwarantuje pełnego spisu wszystkich wydarzeń. Oficjalny backend pozostaje wybierany przy połączonym koncie.
- Poprawione bramki bez konta: Ctrl+T, agregat, wybór kanałów, menu, paleta, Radio YouTube; PL/EN ostrzeżenia o składniku.
- Brak/nienumeryczna data nie odrzuca transmisji. Dodano System.IO wymagane rzeczywistym projektem Windows, mimo wcześniejszego zielonego LiteHosta.

## Testy i wynik

### Interfejs i import

- RED UI: 2 failed, 2 passed (CSV i etykieta YouTube); RED tłumaczenia: 1 failed, 4 passed.
- Wcześniejsza grupa: 83 passed, 1 skipped, 4 deselected; cztery wyłączenia to potwierdzone braki bazy cbad568 w innych klawiszach dostępu i ogólnej kompletności starszych tłumaczeń. Nie jest to pełna zielona regresja.
- Review UI ujawnił brak jawnego tłumaczenia selektora/dynamicznych stanów. Po korekcie: 9 passed, 15 deselected; wygenerowany szablon i raport import-ui-review-correction.xml w state/youtube-issues-19-20 profilu AMC.
- Review backendu ujawnił scalanie ID różniących się literami i czyszczenie metadanych placeholderem. RED protokołu importował dwa kanały po wyborze jednego; poprawiono selekcję, OPML, updater i trwały stan.
- Testy trwałej bazy: podgląd dwóch ID, wybór jednego, restart, reimport i import obu; ochrona niecustomowej nazwy, autora, opisu, strony, daty odświeżenia oraz danych/flag/pozycji odcinka. Zwykły refresh nadal aktualizuje prawdziwe dane.
- RED ZIP wykazał odczyt ponadlimitowego strumienia; GREEN dla seekable/non-seekable, poprawnego ZIP i leave-open.
- Po integracji w głównym repozytorium: Core.SmokeTests --youtube-file-import — exit 0; przełącznik bezpośrednio wykonuje test, nie pusty wybór. LiteHost.ProtocolTests --podcast-file-import --youtube-public-broadcasts --youtube-account — wszystkie 3 zestawy zaliczone; publiczny koordynator 3 scenariusze, kontrakt konta 9.

### Transmisje i integracja

- Niezależny reviewer ponowił realne publiczne odczyty UC22dMuSZ7XTCkEY72rodgIA/streams, po 6 wpisów, bez konta. Status: 6/6 was_live i daty; lokalizacja: te same ID i polskie tytuły, 6/6 puste statusy/daty. Parser yt-dlp potwierdził prawidłowość osobnych argumentów ekstraktorów.
- RED rzeczywistej metody publicznego parsera: InvalidOperationException przy timestamp:null. Po poprawce zachowany poprawny wiersz. Stały test obejmuje null, tekstową datę i wykluczenie was_live.
- RED bramek GUI: 3 niepowodzenia dla Ctrl+T, menu i rzeczywistej metody agregatu bez konta. Finalna grupa: **88 passed, 2 subtests passed** (import GUI, transmisje, skróty, paleta, Radio YouTube). Raport: state/youtube-issues-19-20/integrated-python.xml w profilu AMC.
- Osobne tłumaczenia menu/palety i zgodność szablonu: 2 passed, 14 deselected.
- Finalny build rzeczywistego LiteHost i Windows.SmokeTests: oba 0 warnings, 0 errors. Poprzedni build Windows ujawnił 6 CS0246; skorygowano je, nie ukryto za testem atrapy.
- Finalny runner Windows --youtube-collection-normalization: **OK: ograniczone kanały i playlisty publicznego YouTube**, exit 0. Kopia NTFS runnera 1136/1136 plików, wszystkie hashe zgodne. Nie uruchamiano AMC/GUI.
- git -c core.whitespace=cr-at-eol diff --check bez błędów; opcja uwzględnia istniejący CRLF runnera Windows.

Testy publicznego koordynatora używają atrapy backendu, nie dowodzą żywego protokołu/odtwarzania. Test parsera wykonuje rzeczywisty kod klienta. Nie wykonano odbioru całej aplikacji, konta ani wx/NVDA.

## Materiał do dokumentacji użytkownika

Ctrl+O w Podcastach i YouTube wybiera plik, potem źródła. Spacja zmienia wybór, Ctrl+A zaznacza wszystkie, Anuluj nic nie zapisuje. Konto Google nie jest potrzebne. Kanały można później odświeżać zwykłą drogą; nie powstaje automatycznie setka pobrań.

Ctrl+T na kanale pokazuje trwające/zaplanowane transmisje, Ctrl+Shift+I agregat do 20 jawnie wybranych kanałów, F5 odświeża. Potrzebny yt-dlp; brak składnika oznacza ostrzeżenie, nie automatyczne logowanie. Zakończone transmisje pozostają na zwykłej liście kanału. Nie powstaje osobne archiwum zauważonych transmisji.

Skróty zachowano we wspólnym modelu; okno importu i nowe ostrzeżenie mają PL/EN. Fokus, odczyt listy i powrót po zamknięciu wymagają żywego odbioru bezpiecznej paczki. Public/Test używają wspólnych źródeł, ale nie ma nowych zaakceptowanych pakietów.

## Znane ograniczenia

- Hold po nierozstrzygniętych detekcjach RCX i niekanonicznym Test nadal obowiązuje. Nie budowano instalatora/pakietu, nie instalowano/uruchamiano AMC ani dotkniętych kandydatów, nie zmieniano ochrony. Kompilacje i nieszkodliwe runnery nie zamykają bramki bezpieczeństwa. Główny komputer nietknięty.
- Brak nieoficjalnego logowania TV, prywatnych danych konta, importu historii/prywatnych playlist, HTML Takeout i monitorowania wszystkich kanałów.
- Publiczne /streams zależy od YouTube/yt-dlp i limitu 50 pozycji, nie gwarantuje kompletności.
- Cache backendu: 5 minut, F5 go omija. Brak osobnego pomiaru cache/single-flight na żywym procesie. Opuszczenie widoku odrzuca stary wynik, ale protokół nie przekazuje anulowania do yt-dlp; proces ogranicza timeout 60 sekund. Nie deklarować anulowania end-to-end.
- #19/#20 pozostają otwarte; kod i testy nie oznaczają dostarczonej aktualizacji.

## Wersja i commit

Integracja: c8426ac (UI), c5e5e50 (import), 743582b (archiwa), 5065522 (publiczne transmisje), 0cf8ce7 (korekta importu po review; oryginał workera 1dd73aac76b36fc3ad5f3fc9717a82bebc894520). Dalsze korekty GUI/parsera i raport należą do końcowego commitu integracyjnego. Brak nowego wydania, instalacji i publikacji binariów. Push wymaga odczytu dokładnego zdalnego HEAD i nie zastępuje CI.

## Następny krok

Po zamknięciu niezależnej bramki bezpieczeństwa i przygotowaniu kanonicznej paczki: jeden ograniczony odbiór wx/NVDA importu (wybór/anulowanie/PL/EN/fokus), transmisji bez konta (Ctrl+T, agregat/F5, Radio) i odświeżonego kanału z archiwami; potem osobne zatwierdzenie wydania. Nie ponawiać ważnych fixtures bez zmiany kodu lub konkretnego braku dowodu.
