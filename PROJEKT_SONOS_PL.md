# Sonos — wytyczne, stan prac i otwarte sprawy

Stan na 29 września 2026 r. To dokument rozwoju, nie deklaracja, że wszystkie opisane funkcje są już dostępne w instalatorze.

## Co jest wydane

[AMC 0.1.0-alpha.413](https://github.com/michalkasperczak/AMC/releases/tag/v0.1.0-alpha.413) jest opublikowane z instalatorem i ZIP-em. Dostarcza listę domów, grup i głośników przez **Plik → Konto Sonos → Głośniki i grupy**. [Opis zmian alfa413](ZMIANY_0.1.0-alpha.413_PL.md).

Pełny build i testy Core zakończyły się poprawnie; pełna tabela Windows zaliczyła 169 testów. Instalację oraz uruchomienie sprawdzono na obu komputerach; zachowano bieżący materiał, stan i widok, a na głównym komputerze również pozycję. Potwierdzono już odczyt z rzeczywistego, wcześniej połączonego konta Sonos bez ponownego logowania i bez poleceń zmieniających muzykę na urządzeniu.

Alfa413 nie zawiera jeszcze sesji odtwarzacza Sonos ani sterowania i presetów. W kodzie roboczym odebrano już Core, jego powiązanie z kontem, wejście do sesji użytkowej oraz automatyczne odświeżanie stanu. Nadal nie są częścią tego instalatora. Reakcja sesji na zmianę konta jest już odebrana w kodzie roboczym; trwają prace nad odświeżaniem grup i wyborem domu. Nie wymaga to teraz ponownego logowania użytkownika.

[AMC 0.1.0-alpha.412](https://github.com/michalkasperczak/AMC/releases/tag/v0.1.0-alpha.412) zawiera obsługę konta Sonos: logowanie w oficjalnej przeglądarce, sprawdzenie wyniku, zapis dostępu dla bieżącego użytkownika Windows, odnawianie dostępu, ponowienie zapisu i wylogowanie z potwierdzeniem.

Pierwsze rzeczywiste logowanie zostało potwierdzone po publikacji tego wydania. Nie oznacza to jeszcze potwierdzenia sterowania głośnikami ani wszystkich przebiegów odnawiania dostępu na rzeczywistym koncie.

**Alfa412 nie zawiera listy głośników, osobnej sesji Sonos ani sterowania jego odtwarzaniem.** Zakres wydania opisuje również [lista zmian alfa412](ZMIANY_0.1.0-alpha.412_PL.md).

## Ustalony kierunek: sposób obsługi możliwie jak w WiiM

Wzorcem jest istniejąca sesja WiiM, nie osobny, niepowiązany sposób obsługi Sonosa:

- wybór aktywnego celu; w Sonosie trzeba uwzględnić domy i grupy głośników;
- lista oraz widok odtwarzacza, podobna nawigacja i powrót;
- spójne skróty, ustawienia i komunikaty o bieżącym stanie;
- zachowanie wyboru i miejsca klawiatury po odświeżeniu;
- wyjście z kontrolera nie zatrzymuje samoczynnie muzyki na autonomicznym urządzeniu;
- brak zmian numerów i skrótów dotychczasowych sesji AMC.

Podobieństwo obsługi nie oznacza kopiowania protokołu WiiM. Nie przenosimy w ciemno dodawania urządzeń po IP, komend sieci lokalnej ani ograniczeń usług muzycznych właściwych dla WiiM. Nie należy pokazywać martwych odpowiedników funkcji WiiM.

Oficjalna dokumentacja Sonosa potwierdza sterowanie odtwarzaniem grupy, przewijanie, odczyt metadanych oraz regulację głośności i wyciszenia. Istnieją także odczyt ulubionych i obsługa wejścia liniowego na odpowiednim sprzęcie. Dostępności pełnego korektora nie ustalono. [Szczegóły, źródła i ograniczenia](SONOS_CONTROL_API_PL.md) zapisano oddzielnie — potwierdzenie dokumentacji nie oznacza jeszcze wdrożenia ani pomiaru na rzeczywistym urządzeniu.

Okno **Konto Sonos → Głośniki i grupy** jest przyrostem pomocniczym. Samo jego przygotowanie nie kończy całej integracji.

## Zakres wydanego alfa413 — lista urządzeń

Zakres alfa413, oddzielony od późniejszej sesji odtwarzania:

- klient odczytu domów, grup i głośników z oficjalnego Control API;
- odczyt przez wspólnego właściciela konta, bez przekazywania tokenów do modeli okna;
- lista urządzeń, wybór domu, odświeżanie, obsługa pustej i częściowej odpowiedzi;
- poprawki ochrony przed wynikiem starej operacji po zmianie konta;
- poprawki komunikatów wczytywania, fokusu i zachowania wybranego domu.

Końcowy kod `cf04f9a` przeszedł ponowną próbę żywym NVDA oraz wąskie testy Windows: 68 sprawdzeń okna urządzeń i 19 podłączenia konta. Sprawdzono rzeczywiste otwarcie, mówione komunikaty, odświeżenie z wierszy, zmianę kolejności list 20-elementowych, nawigację strzałkami, pierwszeństwo wyboru użytkownika, brak przejmowania obcego okna oraz Escape i ponowne otwarcie. Dane pochodziły z syntetycznego transportu i magazynu. Był to pomiar syntetyczny. Późniejsza próba z zainstalowanego alfa413 potwierdziła również rzeczywisty odczyt grupy i głośnika; publikację i instalację potwierdzono oddzielnie.

## Rejestr spraw tego przyrostu

### S-01. Stary odczyt przechodził na nowe konto podczas odnawiania

Fakt: w niewydanym kandydacie opóźniona odmowa dostępu albo odnowienie wygasłego dostępu mogły doprowadzić do użycia nowego konta przez dawną operację. Zostało to odtworzone na danych próbnych przez rzeczywistą drogę logowania, a nie przez ręczne podstawienie wewnętrznych pól.

Stan: poprawka i testy automatyczne potwierdzają odrzucenie starej operacji oraz zachowanie nowego konta. Nie jest to zgłoszenie utraty rzeczywistego konta użytkownika ani błąd przypisany wydaniu alfa412.

### S-02. Odświeżanie gubiło miejsce klawiatury

Fakt: żywy NVDA po wolnym odświeżeniu wskazywał całe okno zamiast kontrolki.

Stan: zamknięte w kodzie `a067dd8` + `cf04f9a`, potwierdzone żywym NVDA. Fokus w zajętości przechodzi na instrukcję, a po wyniku wraca na świeży wiersz tego samego ID w tym samym domu, także po przeniesieniu poza dawny widok. Przy zniknięciu elementu pozostaje instrukcja. Strzałki działają od przywróconego wiersza; świadomy wybór innej kontrolki i obce okno mają pierwszeństwo. Poprawka jest zawarta w opublikowanym i zainstalowanym alfa413.

### S-03. Odświeżanie zmieniało wybrany dom

Fakt: odświeżenie drugiego domu wybierało pierwszy mimo dalszej obecności wybranego domu.

Stan: poprawka `f1801bb` zachowuje wybór po identyfikatorze, a nie pozycji na liście. Testy automatyczne obejmują zmianę kolejności. Ponowna próba z żywym NVDA potwierdziła zachowanie drugiego domu po wolnym odświeżeniu z przycisku, wraz z powrotem na przycisk. Końcowy pomiar `cf04f9a` potwierdził ten przypadek ponownie; osobne zamknięcie fokusu wierszy opisano w S-02.

### S-04. Brakowało informacji o trwającym odczycie

Fakt: podczas oczekiwania pozostawał poprzedni wynik, bez komunikatu o wczytywaniu.

Stan: na poprawce `f1801bb` potwierdzono wypowiedzi rozpoczęcia odczytu domów, odczytu grup oraz wyniku w rzeczywistym Podglądzie mowy NVDA. Pomiar dotyczy pierwszego otwarcia i wolnego odświeżania z przycisku. Po `cf04f9a` potwierdzono także komunikaty przy odświeżeniu z wiersza bez ponownego odczytu całego dialogu.

## Odebrany roboczy Core po alfa413 — jeszcze bez odtwarzacza

Przygotowano odczyt stanu, metadanych i głośności grupy oraz podstawowe polecenia: odtwarzaj, pauza, przełączanie, następny/poprzedni utwór, przewijanie bezwzględne i względne, poziom głośności, wyciszenie i względna zmiana poziomu.

Końcowy kod roboczy `e27ba78` przeszedł niezależne testy na Windows: **79/79** nowej warstwy poleceń, **69/69** klienta odczytu urządzeń i **18/18** odczytu przez konto. Pełny Core na Windows także zakończył się kodem 0. Wąskie zestawy są zielone również na WSL. Wszystkie odpowiedzi Sonosa w tych testach były syntetyczne; nie sterowano rzeczywistym głośnikiem.

### S-05. Wynik polecenia używał mylącego komunikatu odczytu

W roboczym kodzie wynik POST mówił o zakończonym odczycie, a niektóre błędy o niepoprawnym identyfikatorze domu zamiast argumencie polecenia. Osobny słownik komunikatów usuwa tę pomyłkę. Po przyjęciu polecenia nie ogłasza jego wykonania. Po utracie odpowiedzi, błędzie usługi lub odrzuceniu odpowiedzi nie twierdzi bez dowodu, że polecenie nie zostało przyjęte. Anulowanie samego oczekiwania jest odróżnione od anulowania przed wysłaniem.

Końcowy plik testów uruchomiony niezależnie na kodzie sprzed poprawki dawał **71/79** i osiem właściwych awarii tekstu; na poprawce daje **79/79**. Końcowy przegląd nie znalazł uwag blokujących. Był to błąd niewydanego kodu, nie działającej wersji alfa413.

### S-06. Odebrane operacje grupy przez obsługę konta

Kod `bbfb5b5` łączy odczyty i polecenia grupy z istniejącym właścicielem konta w Core. Własne testy na Windows: **18/18** nowej warstwy, **26/26** właściciela konta, **79/79** transportu grupy, **69/69** klienta odczytu i **18/18** odczytu przez konto. Pełny Core na Windows również przeszedł. Sprawdzono spóźnione wyniki po zmianie konta, odnawianie przed operacją, pojedynczy POST bez automatycznej powtórki oraz działanie dostępu pozostającego w pamięci po błędzie zapisu. Testy nie korzystały z rzeczywistego konta.

Dwa błędy komunikatów przy porzuceniu polecenia zostały odtworzone i poprawione: próba wysłania nie jest gwarancją dostarczenia, a potwierdzone zero prób oznacza komunikat „nie zostało wysłane”, nie „skutek nieznany”. Niezależny test tych samych scenariuszy zmienił wynik z **16/18** na **18/18**. Celowe wyłączenie ochrony przed spóźnionym wynikiem dało **17/18** z właściwą awarią; po odtworzeniu ochrony ponownie **18/18**. Końcowy przegląd nie zgłosił uwag blokujących.

### Granice tego odbioru i kolejny krok

Mechanizm operacji przez konto został podłączony do roboczej sesji Windows. Odbiór wejścia i automatycznego odświeżania opisano poniżej w S-07 i S-08. Nie oznacza to jeszcze zakończenia całej sesji: pozostają wybór domu, jawne odświeżanie topologii oraz pełny odbiór odtwarzacza. Presety i próba rzeczywistego sterowania sprzętem są osobnymi, jeszcze niezaliczonymi etapami. Polecenia zmieniającego stan nie wolno automatycznie powtarzać po błędzie ani utracie odpowiedzi.

Jedna wcześniejsza pełna próba zgłosiła błąd asercji zdarzenia procesu Librespot. Nie odtworzono go w 60 izolowanych próbach, pięciu pełnych przebiegach czystej alfa413 ani w końcowym pełnym przebiegu poprawki. Przyczyna pozostaje nierozstrzygnięta; nie uznajemy tego błędu za naprawiony i nie przypisujemy go bez dowodu zmianom Sonosa.

Odebrane fundamenty są zachowane na [gałęzi roboczej Sonosa](https://github.com/michalkasperczak/AMC/tree/hermes/sonos-group-account-messages), commit `908b0c5` (wyłącznie uzupełnienie dokumentacji po kodzie `bbfb5b5`). Kompilacja aplikacji WPF i projektu jej testów na Windows zakończyła się bez błędów i ostrzeżeń; nie uruchamiano przy tym okien. Numer wersji i instalator nie zostały zmienione, ponieważ ten etap nie daje jeszcze nowej funkcji w interfejsie. Ta aktualizacja dokumentacji nie jest nowym wydaniem aplikacji ani potwierdzeniem działania sterowania na koncie użytkownika.

## Odebrana część roboczej sesji użytkowej — jeszcze niewydana

### S-07. Wejście do sesji i spóźniona aktywacja

Kod roboczy `4bfcf72` zamyka puste pierwsze wejście na listę oraz Enter, który wcześniej nie otwierał odtwarzacza wybranej grupy. Spóźniona odpowiedź nie może otworzyć odtwarzacza po świadomej zmianie sesji albo grupy. Wyjście do listy lub innej sesji nie wysyła polecenia zatrzymującego muzykę.

Potwierdzono rzeczywistą drogę klawiatury i fokus żywego NVDA na izolowanej kopii z syntetycznymi danymi. Test wejścia obejmuje **29 sprawdzeń**. Celowe usunięcie zabezpieczenia powoduje konkretną awarię przy zakończeniu starej aktywacji, kiedy nowa grupa nadal czeka na odczyt; po przywróceniu zabezpieczenia test przechodzi. Ochrona zapisu ustawień pozwala zachować identyfikatory domu i grupy, ale nie poświadczenia.

### S-08. Automatyczne odświeżanie i zakończenie polecenia po zmianie grupy

Kod roboczy `4376b34` podłącza odczyt do rzeczywistego licznika odtwarzacza. Trwający odczyt nie powoduje mnożenia zapytań przy kolejnych tyknięciach. Błąd stanu, metadanych lub głośności wydłuża odstęp do następnej próby. Starsza odpowiedź nie nadpisuje nowszych danych, również gdy oba odczyty dotyczą tej samej grupy.

Zakończenie polecenia po zmianie grupy nie pozostawia sterowania trwale zablokowanego. Jednocześnie stare polecenie nie może odblokować jeszcze trwającego, nowszego polecenia. Nie dodano automatycznego powtarzania poleceń.

Własny pełny Core na Windows oraz wąskie zestawy Windows zakończyły się poprawnie: **27 sprawdzeń odświeżania, 29 wejścia, 24 pomocniczej obsługi sesji i 24 podłączenia konta**. Niezależny przegląd zgodności i jakości nie wskazał blokad. Nie był to pełny zestaw testów Windows ani przygotowanie instalatora.

W widocznej próbie rzeczywisty timer sam odczytał zmieniony tytuł, a fokus NVDA pozostał na miejscu. Sprawdzono wolną odpowiedź, zmianę grupy, wyjście do innej sesji, zakończenie starego polecenia podczas nowszego oraz zamknięcie okna z trwającym odczytem. Pomiar obejmował obiekty i fokus NVDA, nie transkrypt wypowiedzianej mowy. Dane i polecenia były syntetyczne — nie jest to potwierdzenie działania na rzeczywistym głośniku.

### S-09. Rozróżnienie zmiany konta od odnowienia dostępu — odebrane w Core

Kod roboczy `8295935` udostępnia bezpieczny, lokalny znacznik zastąpienia lub utraty konta. Zwykłe odnowienie dostępu go nie zmienia. Znacznik nie zawiera poświadczeń, nie jest identyfikatorem użytkownika Sonosa i nie jest zapisywany do ustawień. Pierwszy odczyt po uruchomieniu stanowi punkt odniesienia, a nie zdarzenie zmiany konta.

Własny test dodatkowy wykrył i następnie potwierdził usunięcie jednego błędu: odrzucone pierwsze podłączenie nie może zgłaszać zmiany konta, którego wcześniej nie było. Ten sam zestaw testów dał **36/37 przed poprawką i 37/37 po niej**. Niezależny przebieg na Windows potwierdził **37/37**, pełny Core oraz regresje Sonosa. Końcowy przegląd poprawki nie wskazał blokad.

Podłączenie znacznika do sesji zostało odebrane w kolejnym kroku opisanym poniżej. Sam licznik w Core nie był wystarczającym dowodem działania interfejsu.

### S-10. Zmiana konta usuwa stare grupy i porzuca spóźnione odpowiedzi

Kod roboczy `09e02bd` zamyka cztery potwierdzone błędy niewydanego przyrostu: pozostawianie starej listy i bieżącego elementu, przyjmowanie spóźnionych danych po zmianie konta, nierozpoznawanie zmiany przy pierwszym otwarciu okna konta oraz instrukcję powrotu, która nie działała przy ponownym wyborze już aktywnej sesji.

Lista widoczna użytkownikowi jest teraz czyszczona razem z modelem sesji. Po każdym oczekiwaniu na odpowiedź sprawdzane jest powiązanie z kontem; kolejny odczyt ze starym identyfikatorem nie jest uruchamiany, a spóźniony wynik nie wraca do odtwarzacza. Pierwsze odtworzenie zapisanego konta pozostaje punktem odniesienia i nie usuwa poprawnego wyboru. Zwykłe odnowienie dostępu także zachowuje wybór.

Własny przebieg na Windows potwierdził **69 sprawdzeń reakcji sesji na konto**, wcześniejsze zestawy Sonosa i pełny Core. Trzy niezależne próby, które wykazywały błędy przed poprawką, przeszły po niej. Końcowy niezależny przegląd zgodności i jakości nie wskazał blokad; nie uruchamiał testów i nie zastępuje tych pomiarów.

Żywy NVDA potwierdził zniknięcie starych wierszy po odłączeniu oraz możliwość powrotu do listy klawiaturą. W prawdziwym Podglądzie mowy sprawdzono pojedynczy komunikat z wykonalną instrukcją przejścia do innej sesji i powrotu. Dodatkowa próba z Enterem, zatrzymaną odpowiedzią i zastąpieniem konta potwierdziła brak spóźnionego otwarcia odtwarzacza. Dane i konta były syntetyczne; nie sterowano rzeczywistym głośnikiem.

**To nadal nie jest nowe wydanie.** Alfa413 pozostaje wersją z instalatorem. Następny przyrost obejmuje jawne odświeżanie grup i unieważnienie celu, który zniknął, a następnie dostępny wybór domu. Pełny odbiór odtwarzacza, presety i rzeczywiste sterowanie sprzętem nie są przez ten wynik uznane za zakończone.

## Warunki przed publikacją kolejnego przyrostu

- Zakończony odbiór klawiaturą i NVDA: pierwsze wczytanie, odświeżanie, wybór domu, Escape w czasie oczekiwania, powrót do okna konta i ponowne otwarcie.
- Potwierdzenie prawdziwej drogi wywołania, nie wyłącznie ręczne wywołanie pomocniczej metody w teście.
- Odczyt rzeczywistych urządzeń dopiero po odebraniu bezpieczeństwa połączenia; wyniki syntetyczne nie są przedstawiane jako odpowiedź konta Sonos.
- Sterowanie odtwarzaniem, metadane i głośność jako kolejne, jawnie sprawdzane możliwości; bez pozornego odtwarzacza z niezmierzonym czasem.
- Opis konkretnej wersji, testy, paczki i weryfikacja publikacji. Wydanie, instalacja oraz uruchomienie u użytkownika są osobnymi stanami.

## Gdzie utrwalamy ustalenia

Ten plik jest publicznym punktem odniesienia dla kierunku i bieżących spraw Sonosa. Ogólne zasady projektu są w [AGENTS.md](AGENTS.md), scenariusze w [TESTY_ZADANIA_PL.md](TESTY_ZADANIA_PL.md), a szersze niejednoznaczności w [rejestrze ryzyk](REJESTR_RYZYK_I_NIEJEDNOZNACZNOSCI_PL.md).

Każde zamknięcie sprawy powinno podawać wersję lub commit oraz zakres rzeczywistej weryfikacji. Opisy kolejnych wydań są w [GitHub Releases](https://github.com/michalkasperczak/AMC/releases) i plikach zmian wersji. Nie publikujemy surowych logów użytkownika, poświadczeń, prywatnych kolekcji ani nagrań.
