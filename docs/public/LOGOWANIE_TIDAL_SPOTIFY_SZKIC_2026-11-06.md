# AMC Public — logowanie TIDAL i Spotify (projekt decyzji i instrukcji)

Stan na 6 listopada 2026 r. Dokument jest szkicem do korekty przed wydaniem, nie instrukcją dla obecnego AMC Public. **Korekta użytkownika z 6.11.2026:** przyszły AMC Public ma zachować pełne audio TIDAL w wewnętrznym odtwarzaczu dla osób logujących się na własne, opłacone konta. Użytkownik akceptuje, że obecny tor jest nieoficjalny i niszowy. Ta decyzja zastępuje wcześniejszy wniosek tego dokumentu, aby usuwać pełne audio z planu Public do czasu uzyskania pisemnej zgody dostawcy.

**Aktualizacja próby z 7.11.2026, po porównaniu profili:** w paczce Public v6 użytkownik potwierdził odtwarzanie pełnego utworu TIDAL. Zastępuje to starsze zdania poniżej o niepotwierdzonym odsłuchu. Sesje Test i Public zwracają dla „The Golden Age” Becka z albumu „Sea Change” pełny FLAC 24 bit/192 kHz. Dekoder przez parser i proxy paczki Public v6 potwierdził te parametry oraz odczytał sekundę PCM. Dla „Change” obie sesje zwracają stratne AAC około 320 kb/s, mimo żądania najwyższej jakości. Wcześniejszy wniosek, że uproszczona sesja Public w ogóle nie ma Max, został wycofany. Jakość zależy od odpowiedzi TIDAL dla utworu. Poprawka niejasnej nazwy AAC jest w źródłach, jeszcze nie w działającej v6. Odbiór NVDA dla utworu Max oraz konto zewnętrznego użytkownika pozostają do sprawdzenia przed wydaniem.

## Krótka odpowiedź

**Kandydat alpha.482, 7.11.2026 — szkic do odbioru.** Źródła poprawki odczytują brakującą częstotliwość ze strumienia. Cicha próba rzeczywistej „Sara” potwierdziła AAC i PCM 44,1 kHz. Zastępuje to opis ograniczenia v7 poniżej. Nowy odczyt brzmi „źródło AAC, jakość stratna”; źródło i PCM pozostają opisane oddzielnie. Testy przeszły, a instalator i rzeczywisty odbiór tej aktualizacji są jeszcze przygotowywane. Public nadal wymaga sprawdzenia logowania na koncie zewnętrznym.

**Sprawdzone przykłady jakości, 7.11.2026.** Osiem prób z historii użytkownika potwierdzono na rzeczywistych danych audio. Próbowane utwory z „Otuchy” Misi Furtak mają FLAC 24/96, a „SZKODA BRAW” z „NIGREDO” Nosowskiej FLAC 24/44,1 — odczyt Max jest poprawny. Próbowane utwory Lisy Stansfield i Omegi otrzymały AAC 44,1 kHz — odczyt jakości stratnej również jest poprawny. Przy Lisie usługa podstawiła zgodne tytuły z „The Boxset Collection” zamiast identyfikatorów zapisanych przy „Real Love”. Wyniku nie należy uogólniać na wszystkie utwory ani edycje tych albumów.

**Korekta po próbie Test v7 z 7.11.2026.** Poprawiony kandydat zachowuje częstotliwość podaną przez TIDAL, w tym Max 192 kHz. Testy dekodowania, przewijania, wznowienia i odbudowy wyjścia przeszły. NVDA odczytuje osobno źródło i PCM w silniku. Dla „Sara” niezależny odczyt danych potwierdził AAC stereo 44,1 kHz. Przy braku częstotliwości w metadanych v7 nadal konwertuje ten utwór do PCM 48 kHz. Odbiór Max w GUI v7 oraz obsługa brakujących parametrów pozostają otwarte. Paczka Test v7 jest uruchomiona próbnie, bez instalacji i publikacji.

**Historyczna próba przed poprawką v7, 7.11.2026.** Dla utworu kontrolnego „The Golden Age” aplikacja ogłaszała źródło Max FLAC 24/192, a w pasku stanu podawała około 5612 kb/s i 48 kHz. Silnik FFmpeg wymuszał konwersję do PCM 48 kHz. W v7 znana częstotliwość źródła jest zachowana wewnątrz silnika, a końcowy format wyjścia nadal zależy od współdzielonego WASAPI i ustawień Windows. Dla „Sara” potwierdzono rzeczywisty strumień AAC. Nie należy obiecywać wyjścia HiRes na podstawie samego komunikatu startu.

Użytkownik publicznego AMC **nie powinien** kompilować programu. Celem dla TIDAL jest także brak obowiązku rejestracji własnej aplikacji deweloperskiej: AMC dostarcza własny Client ID do katalogu, a użytkownik loguje się na własne konto. Obecne pełne audio jest osobną sesją i przy pierwszym użyciu może wymagać zatwierdzenia kodu urządzenia na stronie TIDAL. Każdy użytkownik ma własne poświadczenia; Client ID katalogu jest identyfikatorem aplikacji AMC, nie hasłem użytkownika. Samo dostarczenie Client ID nie potwierdza jednak, że dostawca dopuścił nieograniczoną liczbę użytkowników.

Ten docelowy wariant nie jest obecnie gotowy do wydania. Zainstalowany AMC Public nie zawiera sesji TIDAL ani Spotify. Odizolowana paczka próbna Public zawiera TIDAL z pełnym odtwarzaniem i przeszła kontrolę integralności oraz startu, ale wymaga logowania i odbioru na świeżym profilu zewnętrznego użytkownika. Spotify pozostaje poza tym kandydatem.

**Wynik osobistej próby 6.11.2026:** użytkownik zalogował katalog TIDAL w przeglądarce, a AMC potwierdził „Zalogowano konto TIDAL.”. W starej paczce próbnej lista pozostawała pusta, ponieważ nie odświeżała się po callbacku. Poprawka źródeł już ją odświeża; wymaga jeszcze próby w nowej paczce. Nie potwierdzono pełnego odsłuchu.

Nowa paczka próbna po poprawce: `D:\Projekty Codex\AMC - integracja wxPython\.test-output\public-tidal-login-refresh-v2-20261106\AMC.exe`. Zamknij poprzednią paczkę Public i uruchom nową z Eksploratora plików. Otwórz TIDAL i `Ctrl+F5`; stan połączenia powinien być zachowany w profilu Public. Zobaczysz „Zaloguj ponownie w przeglądarce” i „Odłącz konto katalogu TIDAL”. Zamknij okno konta i sprawdź własną Bibliotekę, Ulubione oraz Playlisty. Nie wybieraj odłączenia tylko na potrzeby tej próby. Jeśli lista nadal jest pusta, zapisz komunikat z AMC; nie oznacza to automatycznie, że logowanie się nie udało.

## Stan techniczny, który wpływa na decyzję

| Usługa | Obecny AMC Test | Warunek zwykłego logowania w AMC Public |
|---|---|---|
| TIDAL, katalog i Biblioteka | Oficjalne OAuth 2.1 z PKCE wymaga Client ID. W źródłach kandydata `Ctrl+F5` ma już pole Client ID, adres powrotu i zapis. | Przygotować Client ID AMC w wydaniu i sprawdzić, czy zewnętrzne konta mogą go używać. Jeśli się nie uda, pokazać osobistą konfigurację jako wariant ograniczony; samo konto TIDAL nie zastępuje Client ID katalogu. |
| TIDAL, odtwarzanie wewnątrz AMC | Druga, osobna sesja przez nieoficjalną bibliotekę `tidalapi` i kod urządzenia. Nie korzysta z tokenu katalogowego. | Zachować pełne audio zgodnie z decyzją użytkownika. Sprawdzić pakowanie, logowanie nowego konta, trwałość sesji, odtwarzanie po 30 sekundach, wyjście audio, sterowanie i NVDA. Jawnie opisać nieoficjalny charakter i możliwość awarii po zmianach TIDAL. |
| Spotify, katalog i Biblioteka | OAuth z PKCE wymaga Client ID. Okno `Ctrl+F5` ma pole Client ID i pokazuje adres powrotu. | Rejestracja AMC zatwierdzona do szerszego dostępu (Extended Quota Mode), identyfikator dostarczony w paczce i test dostępnych endpointów. |
| Spotify, odtwarzanie | AMC Test ma osobny silnik Librespot; samo OAuth katalogu nie dowodzi prawa ani gotowości do publicznego odtwarzania. | Osobny przegląd zasad i technicznej ścieżki odtwarzania. Nie obiecywać w Public odtwarzacza tylko na podstawie działającego katalogu w Test. |

Według wytycznych TIDAL pełne odtwarzanie dla subskrybenta jest przewidziane wewnątrz TIDAL Embed; poza Embed dostępne są próbki do 30 sekund. TIDAL wymaga oficjalnego, niezmodyfikowanego Player SDK dla odtwarzania oferowanego przez jego narzędzia. Wcześniejsza próba Embed w AMC pozostała przy próbce mimo zalogowania. Opłacone konto daje użytkownikowi dostęp do muzyki w TIDAL, ale samo w sobie nie zmienia zasad użycia narzędzi przez aplikację zewnętrzną. Obecny pełny tor AMC Test pozostaje więc świadomie nieoficjalny. Nie obchodzić DRM, nie pobierać plików na trwałe i nie ukrywać tej granicy w opisie wydania.

Spotify ogranicza aplikację w Development Mode do pięciu autoryzowanych użytkowników i wymaga Premium od właściciela takiej aplikacji. Szerszy dostęp wymaga Extended Quota Mode. Obecne kryteria Spotify dla nowych wniosków dotyczą organizacji i usługi o co najmniej 250 tys. aktywnych użytkowników miesięcznie. Z tego powodu wspólny testowy Client ID AMC nie jest rozwiązaniem publicznym.

## Docelowa instrukcja dla zwykłego użytkownika (po przygotowaniu wydania)

1. Zainstaluj AMC Public i otwórz sesję TIDAL albo Spotify.
2. Naciśnij `Ctrl+F5`. Docelowo okno „Konto” pokaże stan połączenia i nie będzie żądało własnego Client ID ani Client Secret. W obecnym kandydacie źródłowym pole Client ID jest dostępne do ręcznej konfiguracji; AMC nie ma jeszcze zweryfikowanego wspólnego identyfikatora.
3. Wybierz „Zaloguj”. AMC otworzy zwykłą domyślną przeglądarkę na stronie właściwego dostawcy. Hasło wpisujesz tylko tam, nie w AMC.
4. Sprawdź nazwę aplikacji AMC oraz zakres dostępu, po czym potwierdź lub anuluj. Do odtwarzania muzyki Spotify przez integrację potrzebne jest Premium.
5. Po potwierdzeniu przeglądarka wróci na lokalny adres `127.0.0.1`. Wróć do AMC przez `Alt+Tab`, jeżeli fokus nie przełączy się sam. AMC poda wynik logowania i pokaże stan konta.
   Po zamknięciu okna konta zobaczysz w AMC własną Bibliotekę TIDAL, Ulubione i Playlisty. AMC nie pokazuje strony głównej witryny TIDAL. Wyszukiwanie katalogu jest dostępne w AMC.
6. **TIDAL, pierwsze pełne odtwarzanie:** wybierz utwór i uruchom odtwarzacz AMC. Jeśli pojawi się osobny kod, otwórz podany przez AMC adres TIDAL w zwykłej przeglądarce, wpisz kod i zatwierdź dostęp własnym kontem. Wróć do AMC i sprawdź, czy utwór słychać dłużej niż 30 sekund. To drugie zatwierdzenie jest cechą obecnego toru; nie przedstawiać go jako jednego logowania, dopóki implementacja rzeczywiście nie połączy obu sesji.
7. Kolejne uruchomienia korzystają z bezpiecznie zapisanych sesji do ich wygaśnięcia, cofnięcia zgody lub wylogowania. `Ctrl+F5` pozwala sprawdzić stan katalogu, ponowić logowanie i odłączyć konto w bieżącym profilu. Status osobnej sesji odtwarzania musi być w Public równie czytelny.

Zalecany tekst błędu przy braku uprawnienia usługi: „Dostawca nie udostępnił tej funkcji aplikacji AMC. Twoje hasło i abonament nie są przyczyną tego komunikatu. Możesz użyć oryginalnej aplikacji dostawcy”. Komunikat trzeba dopasować do rzeczywistej odpowiedzi API; nie przypisywać każdej odmowy do tej jednej przyczyny.

## Wariant osobistej konfiguracji dla testerów, gdy brak dostępu produkcyjnego

To **nie jest** zamiennik publicznego dostępu ani instrukcja, którą należy dołączyć jako obowiązkowy krok dla wszystkich użytkowników. Może służyć świadomemu użytkownikowi do własnej, ograniczonej integracji, o ile regulamin dostawcy i aktualny panel na to pozwalają.

### Spotify: własny Client ID do katalogu

1. Przygotuj konto Spotify Premium. Otwórz [Spotify Developer Dashboard](https://developer.spotify.com/dashboard) w swojej przeglądarce.

   https://developer.spotify.com/dashboard

2. W panelu utwórz aplikację do własnego użycia. Nazwij ją zgodnie z rzeczywistym zastosowaniem i wybierz Web API, jeśli panel o to pyta. Nie wpisuj cudzego Client ID.
3. W ustawieniach aplikacji dodaj dokładny adres powrotu AMC: `http://127.0.0.1:43822/spotify/callback/`. Zapisz go znak w znak. `localhost` nie jest akceptowany przez Spotify.
4. Skopiuj **Client ID**. Nie kopiuj Client Secret do AMC ani do wiadomości.
5. W AMC Test otwórz Spotify, naciśnij `Ctrl+F5`, wklej Client ID i zapisz. Wybierz logowanie w przeglądarce, zaloguj się na stronie Spotify i potwierdź dostęp.
6. Wróć do AMC i sprawdź stan konta. Jeśli OAuth się powiedzie, ale Web API zwróci `403`, sprawdź listę uprawnionych użytkowników aplikacji w panelu Spotify. Dla własnego konta i własnej aplikacji nie zastępuje to dostępu produkcyjnego do AMC jako programu publicznego.

### TIDAL: własny Client ID do katalogu, gdy wspólny identyfikator AMC nie działa

1. Otwórz [TIDAL Developer](https://developer.tidal.com/) i zaloguj się kontem TIDAL.

   https://developer.tidal.com/

2. Zarejestruj aplikację do własnych prób. W panelu ustaw dokładny adres powrotu `http://127.0.0.1:43821/tidal/callback/` i uprawnienia wymagane do katalogu i kolekcji.
3. Skopiuj tylko Client ID; Client Secret nie jest potrzebny klientowi desktopowemu z PKCE. W kandydacie źródłowym otwórz TIDAL → `Ctrl+F5`, wklej Client ID i wybierz „Zapisz identyfikator aplikacji”. Nie edytuj ręcznie plików stanu.
4. Po skonfigurowaniu wybierz TIDAL → `Ctrl+F5` → „Zaloguj”, potwierdź dostęp na stronie TIDAL i wróć do AMC. To logowanie daje dostęp do katalogu i kolekcji w granicach uprawnień. Nie odblokowuje samo z siebie pełnego utworu w oficjalnym Player SDK.
5. Osobny kod urządzenia do pełnego odtwarzacza pochodzi z nieoficjalnego toru i nie wymaga tworzenia aplikacji deweloperskiej TIDAL. W przyszłym Public musi być opisany jako osobna czynność, jeśli ten tor zostanie dołączony.

## Decyzja przed następną aktualizacją Public

1. Przygotować TIDAL z pełnym odtwarzaniem jako zakres przyszłego Public. Nie zakładać, że bieżąca paczka Test automatycznie spełnia warunki Public. Spotify można wydać osobno.
2. Dla TIDAL ustalić sposób podania Client ID katalogu bez pracy deweloperskiej po stronie użytkownika. Zachować osobne logowanie odtwarzania kodem urządzenia, o ile nadal jest potrzebne. Przejść realny test na świeżym profilu i własnym płatnym koncie osoby zewnętrznej. Sprawdzić brak wycieku poświadczeń, obsługę błędu, pełny utwór, klawiaturę i NVDA. Zapisać w dokumentacji, że tor pełnego audio jest nieoficjalny i może przestać działać po zmianach usługi. Oficjalną ścieżkę rozwijać równolegle, gdy będzie dostępna.
3. Dla Spotify ustalić realną możliwość uzyskania Extended Quota Mode i legalną ścieżkę odtwarzania. Jeśli nie ma dostępu szerszego, uczciwie oznaczyć integrację jako eksperymentalną w Test, bez obietnicy dla całej społeczności Public.
4. Przed wydaniem rozdzielić poświadczenia Test i Public, sprawdzić świeży profil i konto innej osoby, komunikaty NVDA, pomoc, menu, paletę, odłączanie obu sesji TIDAL oraz brak sekretów i tokenów w paczce.
5. Dopiero po tym skorygować ten szkic i włączyć właściwą instrukcję w dokumentacji wydania.

## Źródła dostawców

- [Autoryzacja TIDAL](https://developer.tidal.com/documentation/api-sdk/api-sdk-authorization)
  https://developer.tidal.com/documentation/api-sdk/api-sdk-authorization
- [Wytyczne projektu TIDAL](https://developer.tidal.com/documentation/guidelines/guidelines-developer-guidelines)
  https://developer.tidal.com/documentation/guidelines/guidelines-developer-guidelines
- [Wytyczne odtwarzania TIDAL](https://developer.tidal.com/documentation/guidelines/guidelines-design-guidelines)
  https://developer.tidal.com/documentation/guidelines/guidelines-design-guidelines
- [Tryby dostępu Spotify](https://developer.spotify.com/documentation/web-api/concepts/quota-modes)
  https://developer.spotify.com/documentation/web-api/concepts/quota-modes
- [Rejestracja aplikacji Spotify](https://developer.spotify.com/documentation/web-api/concepts/apps)
  https://developer.spotify.com/documentation/web-api/concepts/apps
- [PKCE Spotify](https://developer.spotify.com/documentation/web-api/tutorials/code-pkce-flow)
  https://developer.spotify.com/documentation/web-api/tutorials/code-pkce-flow
- [Adresy powrotu Spotify](https://developer.spotify.com/documentation/web-api/concepts/redirect_uri)
  https://developer.spotify.com/documentation/web-api/concepts/redirect_uri
- [Zasady platformy Spotify](https://developer.spotify.com/policy)
  https://developer.spotify.com/policy

## Zapis pracy

- Zachowanie widoczne: źródła kandydata Public zawierają TIDAL i pełny odtwarzacz. `Ctrl+F5` w TIDAL pokazuje pole Client ID, adres powrotu i panel deweloperski oraz pozwala zapisać identyfikator przed logowaniem. Etykiety odtwarzacza mówią „AMC” w obu profilach. Zainstalowany Public nie został zmieniony. Spotify w Public nadal nie występuje.
- Uzasadnienie: oddzielono OAuth katalogu od logowania pełnego odtwarzania i ograniczenia profilu Test od gotowości Public. Zapisano świadomą decyzję użytkownika o nieoficjalnym torze audio.
- Testy i wynik: 6.11.2026 celowane testy Pythona profilu, menu, palety i GUI 53/53, okna konta/klienta hosta 15/15, izolacji poświadczeń 2/2 i wyszukiwania 13/13; testy kontraktu hosta TIDAL 1/1, natywnego żądania 1/1 i launchera 39/39. Host zbudował się bez ostrzeżeń. Końcowa paczka próbna zawiera 3143 pliki, przeszła pełną kontrolę integralności i kontrolowany start interfejsu oraz hosta. Nie wykonano rzeczywistego OAuth, odsłuchu, NVDA, instalacji ani wydania.
- Znane ograniczenia: dostęp produkcyjny, decyzje dostawców i realny odbiór na koncie zewnętrznym nie są potwierdzone. Nazwy przycisków i przebieg mogą się zmienić przed wydaniem.
- Korekta 6.11.2026: pierwsza próba OAuth katalogu TIDAL zakończyła się komunikatem sukcesu w AMC. Poprawiono odświeżanie kolekcji po logowaniu, nazwę przycisku ponownego logowania i dodano odłączanie samego katalogu z potwierdzeniem. Osobnego kodu odtwarzacza i odsłuchu jeszcze nie sprawdzono; starsze zdanie o niewykonanym OAuth dotyczy stanu przed tą próbą.
- Wersja i commit: zmiany źródłowe bez commitu i bez nowej wersji aplikacji; zainstalowany AMC Public pozostaje bez zmian.
