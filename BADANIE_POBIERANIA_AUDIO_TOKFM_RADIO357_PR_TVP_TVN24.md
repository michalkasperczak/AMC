# Badanie pobierania materiałów audio dla AMC

Data badania: 16 sierpnia 2026 r.

## Cel i granice

Dokument opisuje możliwość zapisania do zwykłego pliku audio materiałów, do których użytkownik ma legalny dostęp: publiczny albo wynikający z własnej aktywnej subskrypcji. Chodzi o odsłuch osobisty, między innymi w KASTRO.

AMC nie powinien:

- obchodzić DRM ani innych skutecznych zabezpieczeń technicznych;
- pozyskiwać cudzego dostępu, omijać płatności ani przedłużać wygasłych uprawnień;
- eksportować wszystkich ciasteczek przeglądarki do pliku;
- udostępniać, publikować ani wysyłać pobranych nagrań innym osobom;
- automatycznie zgadywać, który z kilku różnych materiałów Polskiego Radia jest właściwy.

## Wynik w skrócie

| Usługa | Wynik badania | Zalecany sposób | Priorytet |
|---|---|---|---|
| TOK FM | Publiczny odcinek został rozpoznany jako bezpośredni plik MP3 przez HTTPS. Aktualny yt-dlp ma osobne ekstraktory TOK FM. Materiałów Premium nie testowano bez zalogowania. | Adapter TOK FM; publiczne materiały bez logowania, Premium dopiero po kontrolowanym teście legalnej sesji. | Wysoki |
| Radio 357 | Serwis ma własny katalog i oficjalny, autoryzowany mechanizm pobierania odcinków. Nie trzeba wyciągać HLS z odtwarzacza. | Natywny adapter korzystający z sesji użytkownika i oficjalnej funkcji pobierania; bez zapisywania tokenu w logach. | Wysoki |
| Polskie Radio | Stare strony i starsze podcasty są dobrze rozpoznawane przez yt-dlp. Nowa platforma zmieniła API i wymaga aktualizacji adaptera. Jeden wpis może mieć kilka nagrań. | Własny adapter nowej platformy; przy wielu plikach dostępna lista wyboru z tytułem, opisem, datą i czasem. | Wysoki |
| TVP VOD | Aktualny publiczny materiał testowy rozwinął się do HLS i DASH bez wykrytego DRM. | Pobieranie tylko pozycji bez DRM, następnie wyodrębnienie ścieżki audio przez FFmpeg. | Średni |
| TVN24+ | Oficjalna aplikacja oferuje odsłuch/oglądanie offline, ale obecny ekstraktor yt-dlp jest oznaczony jako zepsuty. Bez zalogowania nie potwierdzono zwykłego niechronionego strumienia. | Na początku oficjalny tryb offline lub otwarcie materiału w usłudze. Eksport dopiero po teście własnej sesji i wyłącznie bez DRM. | Niski / eksperymentalny |

Najważniejszy wniosek: dynamiczny albo krótko ważny adres HLS sam w sobie nie jest problemem. AMC może pobrać manifest w czasie ważnej sesji i przekazać segmenty do FFmpeg. Twardą granicą jest DRM; drugą granicą są warunki konkretnej subskrypcji.

## Ustalenia techniczne

### TOK FM

Oficjalna aplikacja TOK FM pozwala pobrać wybrane audycje i podcasty do odsłuchu bez internetu. Publiczny odcinek sprawdzony aktualnym yt-dlp 2026.07.04 zwrócił zwykły plik MP3 przez HTTPS, a nie HLS. Ekstraktor yt-dlp pobiera metadane TOK FM, a następnie prosi API Radia Agora o adres MP3.

To potwierdza możliwość zbudowania stabilnego adaptera dla treści publicznych. Trzeba osobno sprawdzić jeden odcinek Premium po zalogowaniu użytkownika. Nie należy zakładać, że publiczny ekstraktor automatycznie obsługuje Premium.

### Radio 357

Publiczny kod serwisu ujawnia rozdzielenie katalogu, autoryzacji i odtwarzania. Lista odcinków jest dostępna przez API treści, a pobieranie wykorzystuje oficjalną funkcję serwisu `download/{id}` wymagającą autoryzowanej sesji. Bez zalogowania test zwrócił kod oznaczający konieczność logowania. Oficjalne materiały Radia 357 mówią także o słuchaniu audycji na żądanie offline.

To jest korzystniejsza sytuacja niż przechwytywanie HLS. Adapter powinien wywoływać funkcję pobrania w ramach własnej sesji użytkownika, odebrać krótkotrwały podpisany adres i natychmiast zapisać plik. Adres, token i nagłówki autoryzacyjne muszą być usuwane z diagnostyki.

### Polskie Radio

Aktualny yt-dlp ma ekstraktory starszych artykułów, audycji, podcastów i odtwarzacza Polskiego Radia. Starszy wpis może zawierać wiele obiektów `data-media` lub kilka załączników audio, dlatego wynik bywa playlistą, a nie jednym plikiem.

Nowa platforma Polskiego Radia używa danych aplikacji Next.js i nowych identyfikatorów audio/wideo. W sprawdzonym współczesnym odcinku metadane zawierały oddzielny identyfikator audio, lecz bieżący yt-dlp nie potrafił jeszcze rozwinąć strony do pliku. Oznacza to potrzebę nowego adaptera, a nie brak technicznej możliwości.

Zasada interfejsu:

- jeden jednoznaczny plik audio — można wybrać automatycznie;
- kilka plików — AMC przedstawia listę i niczego nie zgaduje;
- elementy należy opisać tytułem, typem, czasem, datą oraz krótkim opisem;
- audio i wideo powinny być rozdzielone, nawet jeżeli należą do tego samego wpisu;
- użytkownik może odsłuchać próbkę albo otworzyć źródło przed pobraniem.

### TVP VOD

Aktualny yt-dlp obsługuje TVP, TVP Stream i TVP VOD. Publiczny materiał testowy został rozpoznany jako HLS i DASH. W takim przypadku AMC może wybrać najlepszą ścieżkę audio i:

- skopiować ją bez ponownej kompresji do M4A, jeśli kodek i kontener na to pozwalają;
- przekodować do MP3 albo Opus tylko na życzenie użytkownika;
- zachować tytuł, program, datę, opis, źródło i okładkę w metadanych.

Każdy materiał musi być najpierw sprawdzony. Wykrycie Widevine, PlayReady, FairPlay, licencji DRM albo zaszyfrowania wymagającego klucza kończy operację komunikatem o użyciu oficjalnej aplikacji.

### TVN24+

TVN24+ oficjalnie pozwala pobierać materiały do oglądania offline, ale nie oznacza to automatycznie eksportu do zwykłego pliku. Obecny ekstraktor TVN24 w yt-dlp jest oznaczony jako niedziałający, a publiczna próba nie znalazła formatów.

Pierwszy etap integracji powinien oferować katalog, dostępne metadane oraz polecenie otwarcia materiału w oficjalnej usłudze. Dopiero kontrolowany test pojedynczego materiału we własnej zalogowanej sesji pokaże, czy jest to zwykły podpisany HLS, czy DRM. W drugim przypadku AMC nie będzie go pobierał.

## Bezpieczna architektura modułu AMC

1. `MediaResolver` rozpoznaje usługę, pozycję i dostępne warianty.
2. Adapter usługi pobiera wyłącznie metadane oraz adres dostępny dla aktualnej sesji.
3. `ProtectionProbe` sprawdza protokół, szyfrowanie i DRM przed rozpoczęciem pobierania.
4. `DownloadManager` ma kolejkę, pauzę, wznowienie, kontrolę miejsca, limit równoległych zadań i łagodne limity zapytań.
5. `AudioExporter` kopiuje istniejącą ścieżkę dźwiękową albo, na wyraźne życzenie, koduje ją przez zarządzany przez AMC FFmpeg.
6. `MetadataWriter` zapisuje tytuł, audycję, prowadzącego, datę, źródło i okładkę.
7. `CredentialVault` przechowuje tokeny w Menedżerze poświadczeń Windows/DPAPI; później użyje odpowiednika pęku kluczy na macOS.
8. Historia nie zapisuje tokenów, pełnych podpisanych adresów, ciasteczek ani nagłówków autoryzacyjnych.

Nie należy implementować masowego `cookies-from-browser`, ponieważ eksport może objąć wszystkie ciasteczka profilu. Bezpieczniejszy jest osobny proces logowania danej usługi, token o możliwie małym zakresie i jego systemowe szyfrowanie.

## Proponowana kolejność wdrożenia

1. Polskie Radio: publiczne materiały, wybór spośród wielu nagrań i nowa platforma.
2. TOK FM: materiały publiczne, potem pojedynczy test Premium.
3. Radio 357: katalog, logowanie i oficjalne pobieranie jednego odcinka patrona.
4. Wspólny eksport audio, metadane i kolejka pobierania.
5. TVP VOD: tylko materiały bez DRM.
6. TVN24+: rozpoznanie w sesji użytkownika; bez obietnicy eksportu.

Pierwszy test uwierzytelniony powinien obejmować tylko jeden odcinek TOK FM Premium i jeden odcinek Radia 357. Użytkownik loguje się sam w zwykłej przeglądarce lub oknie systemowym. AMC nie wyświetla ani nie zapisuje hasła. Test rejestruje wyłącznie rodzaj strumienia, obecność DRM, kodek, czas oraz powodzenie pobrania krótkiego, dozwolonego materiału.

## Aspekt prawny i regulaminowy

Art. 23 polskiej ustawy o prawie autorskim pozwala co do zasady nieodpłatnie korzystać z już rozpowszechnionego utworu w zakresie własnego użytku osobistego. Art. 35 zastrzega, że dozwolony użytek nie może naruszać normalnego korzystania z utworu ani godzić w słuszne interesy twórcy. Ustawa przewiduje też odpowiedzialność związaną z niedozwolonym obchodzeniem skutecznych zabezpieczeń technicznych.

W praktyce oznacza to:

- osobista kopia legalnie udostępnionego, niechronionego materiału może mieścić się w dozwolonym użytku;
- sama opłacona subskrypcja nie daje prawa do obchodzenia DRM;
- regulamin może określać dostęp tylko w aplikacji i czas ważności pobrania;
- nie wolno rozpowszechniać kopii ani budować wspólnego archiwum dla innych osób;
- AMC powinien zachować adres źródłowy i datę pobrania oraz czytelnie oznaczać ograniczenia;
- niniejsze badanie nie jest indywidualną poradą prawną.

## Źródła

- TOK FM: [odsłuch bez internetu](https://pomoc.audycje.tokfm.pl/hc/pl/articles/36967776073745-Czy-aplikacja-TOK-FM-umo%C5%BCliwia-ods%C5%82uch-bez-internetu), [zalety Premium](https://rss.tokfm.pl/zalety-premium), [regulamin subskrypcji](https://static.tokfm.pl/prodstatic360/pdf/Regulamin_Subskrypcji_03.06.2025.pdf).
- Radio 357: [aplikacja i Twoje 357](https://radio357.pl/lp/aplikacja/), [regulamin](https://radio357.pl/regulamin/), [informacja o odsłuchu offline](https://radio357.pl/lista-piosenek/wakacje/informacje/).
- Polskie Radio: [opis nowej platformy](https://trojka.polskieradio.pl/artykul/3645931%2Cnowa-platforma-streamingowa-polskiego-radia), [bieżąca platforma](https://www.polskieradio.pl/).
- TVP VOD: [przykładowy publiczny materiał](https://vod.tvp.pl/filmy-dokumentalne%2C163/nie-badz-slepy-na-slepowrona%2C1775052).
- TVN24+: [tryb offline](https://pomoc.tvn24.pl/article/offline), [zakres serwisu](https://pomoc.tvn24.pl/article/o-serwisie).
- Narzędzia: [lista obsługiwanych serwisów yt-dlp](https://github.com/yt-dlp/yt-dlp/blob/master/supportedsites.md), [dokumentacja yt-dlp](https://github.com/yt-dlp/yt-dlp/blob/master/README.md), [uwagi o ciasteczkach](https://github.com/yt-dlp/yt-dlp/wiki/FAQ), [ekstraktor Polskiego Radia](https://github.com/yt-dlp/yt-dlp/blob/master/yt_dlp/extractor/polskieradio.py).
- Prawo: [obowiązujący tekst jednolity ustawy](https://eli.gov.pl/eli/DU/2025/24/ogl), [słowniczek prawa autorskiego](https://www.prawoautorskie.gov.pl/pages/strona-glowna/baza-wiedzy/slowniczek.php).

## Status badania

Nie pobrano żadnego materiału audio ani wideo. Testy publiczne obejmowały wyłącznie rozpoznanie adresów, metadanych, protokołów i odpowiedzi API. Nie użyto kont, haseł, ciasteczek ani tokenów użytkownika.
