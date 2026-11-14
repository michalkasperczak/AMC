# AMC10 — proste logowanie Sonos i publiczny broker

## Status

**Kierunek zmieniony 08.11.2026 — dla publicznego instalatora wymagany jest
lokalny Sonos każdego użytkownika, a nie zdalne sterowanie przez wspólny
broker. Przygotować w AMC Test adapter SSDP/UPnP oparty na SoCo i podłączyć go
pod istniejącą sesję, grupy, treści oraz presety. Publiczny broker
`https://sonos.stareaudycje.pl/` działa technicznie, ale OAuth kończy się na
stronie Sonosa przed callbackiem; dalsza praca nad tym torem zostaje odłożona.
Nie usuwać brokera ani istniejącego kodu chmurowego przed odbiorem lokalnego
wariantu na prawdziwym głośniku. Sprzęt będzie dostępny za kilka dni. Nie
zmieniono jeszcze kodu produktu, instalacji AMC Test, AMC Public ani GitHuba.**

Sterowanie Sonosem i WiiM było już przeszczepione i użytkownik ocenił je jako
prawie gotowe. Ten etap nie zmienia adapterów, list, transportu ani WiiM.

## Korekty użytkownika

- 2026-10-25: użytkownik przekazał dokładny tekst końcowej strony Sonosa:
  „Oops, there was a problem. There seems to be a problem. Please restart the
  account linking process from the beginning to try again.” Wyszukiwanie
  oficjalnej dokumentacji i pomocy Sonosa nie znalazło opisu tego konkretnego
  komunikatu. Strona pozostaje po stronie Sonosa, przed callbackiem, więc tekst
  nie pochodzi z AMC ani brokera. Przygotowano bezsekretowy raport po polsku i
  gotową część angielską dla Sonos Developer Support:
  `D:\Projekty Codex\AMC - pakiety do weryfikacji\Sonos\2026-10-25\SONOS_OAUTH_BLAD_SELECTHOUSEHOLD_DO_WYSLANIA_2026-10-25.md`,
  4791 bajtów, SHA-256
  `93c9521fb6651f0baa62e5edb75c7d2735233b613941dcde136da587903924fa`.
  Raport prosi operatora wyłącznie o potwierdzenie obecności callbacku bez
  ujawniania query stringu, kodu, `state`, tokenu ani sekretu.

- 2026-10-25: użytkownik potwierdził, że w zwykłej aplikacji Sonos jest
  zalogowany na właściwe konto i widzi swój system. Ponowna bezsekretowa próba
  `/login/start` potwierdziła zarazem, że publiczny broker używa dokładnie tego
  samego jawnego klucza integracji co pakiet wdrożeniowy i klient sterowania
  AMC; zgadzają się również `playback-control-all`, oficjalny endpoint
  autoryzacji oraz callback `https://sonos.stareaudycje.pl/login/callback`.
  Oficjalna strona stanu Sonosa oznaczała konto i usługi Sonos jako działające
  i nie podawała incydentu 25.10.2026. Zastępuje to hipotezy o niewłaściwym
  koncie, innym kluczu na serwerze albo ogólnej awarii. Pozostaje błąd strony
  autoryzacji tej integracji na etapie wyboru gospodarstwa, przed callbackiem;
  następna użyteczna próba to czysta sesja prywatna przeglądarki, a przy tym
  samym wyniku zgłoszenie do Sonos z godziną próby i kluczem integracji, bez
  przekazywania `state`, tokenów lub sekretu.

- 2026-10-25: po instalacji kompilacji `af731db1` użytkownik zauważył przed
  logowaniem i ponownie po nieudanej próbie komunikat: „Konto Sonos wymaga
  ponownego zalogowania. Zapisane logowanie Sonos należy do innego serwera
  logowania; nie zostało usunięte”. Kontrola kodu potwierdziła, że ten zapis
  nie uczestniczy w rozpoczęciu nowej próby: `BeginLoginAsync` czyści wyłącznie
  ewentualną oczekującą sesję i wywołuje `/login/start` bieżącego publicznego
  brokera. Stary rekord pozostaje nietknięty do czasu udanego callbacku i
  przyjęcia nowych tokenów. Ponieważ Sonos ponownie zakończył próbę przed
  callbackiem, nie było czym zastąpić rekordu i ten sam komunikat pojawił się
  ponownie. Jest to skutek nieukończonego OAuth, nie przyczyna błędu Sonosa ani
  dowód użycia Hermesa.

- 2026-10-25: na prośbę użytkownika ponownie porównano tylko do odczytu
  archiwalne, nierozwijane AMC WPF z bieżącym AMC Test. Historyczne
  `SonosAccountCoordinator.cs` jest identyczne z obecnym, a klient logowania
  różni się tylko późniejszym rozpoznawaniem braku endpointu. Archiwum nie
  zawiera źródeł osobnego brokera ani innego mechanizmu OAuth do przeniesienia;
  wskazuje wyłącznie dawny origin Hermesa i callback `/sonos/callback`.
  Historyczne raporty `TESTY_SONOS_KONTO_PL.md` i
  `review/sonos-login-client/REPORT.md` jawnie rozróżniają próby syntetyczne od
  prawdziwego logowania i nie stanowią dowodu ukończonego E2E. Jednocześnie na
  komputerze nadal istnieje chroniony przez Windows rekord
  `%LOCALAPPDATA%\AccessibleMediaController\credentials\sonos.bin`, 518 bajtów,
  zapisany 20.10.2026. Potwierdza to zapisane poświadczenie historycznej
  instalacji, ale nie upoważnia do odczytu, kopiowania ani migracji tokenu do
  nowego brokera. Wniosek: nie ma poprawki ze starego klienta do skopiowania;
  trzeba dokończyć świeże logowanie na koncie przypisanym do systemu Sonos.

- 2026-10-25: użytkownik doprecyzował, że publiczny serwer i wdrożenie brokera
  przygotował **Michał Dziwisz**. Określenia „Hermes” nie wolno używać jako nazwy
  tej osoby ani bieżącego serwera; Hermes oznacza wyłącznie wcześniejsze
  środowisko i materiały historyczne. Brak lokalnej kopii pakietu zwrotnego od
  Michała Dziwisza nie jest potrzebny do ustalenia bieżącego miejsca awarii:
  publiczne endpointy brokera odpowiadają, a próba OAuth nie dotarła do
  callbacku. Pakiet pozostaje potrzebny tylko do niezależnego sprawdzenia jego
  manifestu i podanej sumy kontrolnej.

- 2026-10-25: ponowne sprawdzenie publiczne bez logowania i bez ujawniania
  parametrów sesji potwierdziło `200 {"status":"ok"}` dla `/health/live`,
  `200 {"status":"ready","sonos_configured":true}` dla `/health/ready` oraz
  przyjęcie świeżej sesji przez `/login/start` na 600 sekund. W zwróconym adresie
  sprawdzono wyłącznie zgodność pól: oficjalny host i ścieżka Sonosa,
  `response_type=code`, `scope=playback-control-all`, obecność Client ID i state
  oraz dokładny callback `https://sonos.stareaudycje.pl/login/callback`.
  Wszystkie kontrole przeszły. To wyklucza bieżącą awarię konfiguracji brokera;
  nie wyklucza problemu konta lub gospodarstwa domowego po stronie Sonosa.

- 2026-10-25: po pierwszej próbie OAuth użytkownik doprecyzował, że nacisnął
  „Kontynuuj”, a dopiero potem zobaczył stronę błędu Sonos. Przeglądarka była
  nadal w oficjalnym kroku `selectHousehold`; nie nastąpił powrót na callback
  brokera. Zastępuje to robocze podejrzenie błędu samego przycisku albo
  callbacku. Najbardziej prawdopodobna przyczyna to konto bez gospodarstwa
  domowego używanego przez ten system Sonos; jest to wniosek do potwierdzenia
  przez ponowną próbę na właściwym zwykłym koncie, bez ujawniania loginu lub
  hasła.

- 2026-10-25: użytkownik wskazał nadmiarowy, dwuetapowy interfejs. W stanie
  rozłączonym sesja ma zawierać jeden wiersz „Rozpocznij logowanie do Sonos”,
  uruchamiany Enterem. Okno konta pokazuje tylko czynności właściwe dla stanu:
  przed rozpoczęciem „Rozpocznij logowanie”, podczas oczekiwania otwarcie,
  kopiowanie, sprawdzenie i anulowanie, a „Wyloguj konto” dopiero po faktycznym
  połączeniu. Po rozpoczęciu fokus ma trafić na „Otwórz stronę logowania”.
  Zastępuje to wcześniejszy układ, w którym „Wyloguj konto” było widoczne bez
  połączonego konta, a fokus zaczynał od „Sprawdź logowanie”.

- 2026-10-25: portal Sonos dla tej integracji udostępnił jedno pole adresu
  powrotu, a nie listę callbacków. Za jawną zgodą użytkownika zapisano w nim
  `https://sonos.stareaudycje.pl/login/callback`; po ponownym otwarciu
  integracji wartość nadal była obecna. Zastępuje to wcześniejszy plan
  równoległego pozostawienia callbacku Hermesa w tym samym kluczu. Hermes nie
  uczestniczy już w zwykłej ścieżce nowego logowania; pozostaje wyłącznie
  historycznym punktem odniesienia i możliwym ręcznym planem powrotu.

- 2026-10-25: podczas ręcznego odbioru zainstalowanego AMC Test użytkownik
  otworzył sesję Sonos i nacisnął `Ctrl+F5`. W oknie dostępny klawiaturą był
  tylko przycisk „Zamknij”, a instrukcję dało się odczytać dopiero kursorem
  myszy. Jest to dowód działania starszej instalacji z `canLogin=false`, ale
  także rzeczywista wada dostępności: instrukcja i stan były statycznymi
  napisami poza kolejnością Tab. Zastępuje to wcześniejsze założenie, że samo
  wypowiedzenie stanu przy otwarciu wystarcza. Użytkownik polecił poprawić
  interfejs wyłącznie AMC Test i zgodził się dodać nowy callback Sonosa.

- 2026-10-25: użytkownik przekazał raport operatora z działającego publicznego
  brokera. Zastępuje on stan „broker nie został wdrożony”. Operator podał
  17/17 kontroli publicznych, 20/20 testów źródeł, prawidłowy limit per klient,
  bezpieczne logi, start usług po restarcie i pamięć 37–62 MB przy limicie
  300 MB. Prawdziwe logowanie pozostaje zablokowane do dodania callbacku
  `https://sonos.stareaudycje.pl/login/callback`. Podana suma pakietu zwrotnego
  to `c435a3ee6315a226463f86828d087e4a845203834e1914bc95398cc9b6928a01`,
  ale samego pliku nie ma w dotychczas wskazanym folderze, dlatego nie jest to
  jeszcze lokalnie potwierdzony wynik. Commit `bc56ce5` pozostaje tylko lokalny.

- 2026-10-24: użytkownik przekazał odpowiedź operatora serwera. Zastępuje ona
  wcześniejsze ograniczenia o braku hostingu, domeny i znanego reverse proxy.
  Gotowe są Ubuntu 24.04, ASP.NET Core Runtime 8.0.32, systemd, Cloudflare
  Tunnel do `127.0.0.1:5080`, origin `https://sonos.stareaudycje.pl/`,
  chroniony plik sekretu oraz zaufane proxy `127.0.0.1`. Blokadą był brak
  dostępnego kodu brokera; przygotowano wariant B, bez publikacji repozytorium.

- 2026-10-18: ustalenie, że Sonos i WiiM już działają; zastępuje wcześniejsze
  szerokie rozumienie AMC10. Zakres to wyłącznie proste logowanie Sonos i
  publiczny broker.
- 2026-10-19: użytkownik polecił kontynuować broker podczas ręcznego testowania
  AMC. Nie zezwala to na otwieranie przeglądarki, logowanie konta ani wysyłanie
  poleceń do głośników.
- 2026-10-20: użytkownik wskazał Sonosa jako wcześniejszą bramę do publicznego
  instalatora. Zastępuje to późniejszą kolejność AMC10, ale nie znosi bramy
  prawdziwej domeny, danych integracji, odbioru na zewnętrznym koncie ani
  osobnej zgody na publikację w GitHubie.
- 2026-10-20: użytkownik potwierdził, że bieżący publiczny kandydat ma pozostać
  czysty, bez Spotify i TIDAL-a, a logowanie Sonos oraz `Ctrl+F5` należy najpierw
  dopracować w AMC Test i dopiero po odbiorze podpiąć do profilu publicznego.
  Zastępuje to ewentualne bezpośrednie odsłonięcie Sonosa w obecnym instalatorze.
- 2026-10-20: późniejsza kontrola dziennika zastępuje roboczą diagnozę, według
  której zakończony harmonogram pozostawił fałszywy stan nagrywania. Harmonogram
  z 12:30 zakończył plik o 12:35, ale równolegle trwały cztery prawdziwe
  nagrania rozpoczęte około 10:25–10:33. Zakończyły się dopiero około 12:51,
  dlatego odmowa zamknięcia była prawidłowa.
- 2026-10-20: użytkownik polecił dalej dopracowywać Sonosa w AMC Test, ale nie
  budować teraz następnego publicznego instalatora. Nowy publiczny kandydat ma
  powstać dopiero po domknięciu kolejnych funkcji. Zastępuje to przebudowywanie
  profilu publicznego po każdej małej korekcie.
- 2026-10-20: po potwierdzeniu działania Spotify i poprawki wyszukiwania
  użytkownik polecił wrócić do Sonosa. Odczyt Hermesa wykazał działający host
  HTTPS, ale brak usługi brokera. AMC rozróżnia teraz ten stan od uszkodzonej
  odpowiedzi i nie ujawnia użytkownikowi adresu ani treści odpowiedzi serwera.
- 2026-10-20: użytkownik doprecyzował, że obecna architektura Sonosa **nie
  bazuje na Hermesie i nie ma z nim związku**. To ustalenie zastępuje wcześniejszy
  roboczy plan uruchomienia brokera na Hermesie. Wykonany odczyt hosta i lokalny
  ZIP pozostają wyłącznie historycznym śladem eksperymentu; nie są celem
  wdrożenia. Docelowym elementem jest niezależny, wspólny publiczny broker HTTPS.
- 2026-10-20: kontrola źródeł po tej korekcie wykazała pozostały stały origin
  Hermesa w konfiguracji klienta. Został usunięty z aktywnej ścieżki AMC Test.
  Zastępuje go nierutowalny znacznik konfiguracyjny oraz jawna bramka
  dostępności. Nie jest to nowy adres usługi ani próba połączenia.
- 2026-10-20: użytkownik polecił wdrażać niezależny broker i wskazał Lyd jako
  przykład innej aplikacji Sonos, zaznaczając, że prawdopodobnie nie jest ona
  open source. Lyd jest wyłącznie porównaniem użytkowym; nie kopiujemy jego
  kodu ani nie opieramy architektury AMC na przypuszczeniach o zamkniętej
  implementacji. Obowiązującym kontraktem są oficjalne wymagania Control API.
- 2026-10-20: użytkownik potwierdził, że wcześniej logował się do portalu Sonos
  prawdopodobnie kontem Google i tworzył integrację, oraz zezwolił sprawdzić
  historyczny AMC. Audyt tylko do odczytu potwierdził prawdziwy zapis konta
  Sonos z 19 października. Nie oznacza to zgody na ujawnienie lub przesłanie
  sekretu w rozmowie; należy go odnaleźć i przenieść wyłącznie chronionym
  kanałem wdrożeniowym.
- 2026-10-20: użytkownik przypomniał, że w historycznym AMC działało pełne
  sterowanie i grupowanie Sonosa, a po przeniesieniu działa ono również w
  zainstalowanym AMC Test. To zastępuje zbyt szerokie wcześniejsze sformułowanie,
  że bieżąca architektura „nie ma związku” z Hermesem. Dokładnie: gotowa
  obsługa Sonosa nie zależy od Hermesa przy zwykłym sterowaniu, ale zainstalowany
  wariant używa go jako brokera logowania i odnowienia tokenu. Docelowe wydanie
  publiczne nadal ma otrzymać broker niezależny od Hermesa.
- 2026-10-20: użytkownik dostarczył odczytowy raport Hermesa. Zastępuje on
  wcześniejszy wniosek, że odpowiedź `404` z `/health/*` oznacza brak dawnej
  usługi. Historyczny broker i proxy nadal działają, lecz nie udostępniają
  nowych endpointów zdrowia. Raport potwierdził zgodny identyfikator klienta,
  obecność niepustego sekretu i obsługę odnowienia bez ujawnienia wartości.

## Materiał do dokumentacji użytkownika

Etap z 24.10.2026 przygotował sprawdzony pakiet serwerowy do zewnętrznego
wdrożenia. Raport z 25.10.2026 potwierdził działanie serwera. Tego samego dnia
callback zapisano w Sonos Integration Manager, a AMC Test z publicznym originem
i uproszczonym logowaniem zainstalowano lokalnie. Zmiana pozostaje wyłącznie w
AMC Test; AMC Public, GitHub i publiczne wydanie nie zostały zmienione.

W rozłączonej sesji Sonos AMC Test pokazuje jeden dostępny wiersz „Rozpocznij
logowanie do Sonos”. Enter otwiera okno „Konto Sonos”. To samo okno można
otworzyć poleceniem „Konfiguruj…” w Menedżerze sesji. Hasło podaje się tylko na
oficjalnej stronie Sonosa.

Przyciski okna zależą od stanu. Przed rozpoczęciem dostępne są „Rozpocznij
logowanie” i „Zamknij”. Po przygotowaniu adresu dostępne są „Otwórz stronę
ponownie”, „Kopiuj adres logowania”, „Sprawdź logowanie”, „Anuluj logowanie” i
„Zamknij”, a po ponownym otwarciu fokus trafia na „Sprawdź logowanie”. „Wyloguj konto” i
„Głośniki i grupy Sonos” pojawiają się dopiero po połączeniu konta.

„Rozpocznij logowanie” przygotowuje adres, automatycznie otwiera oficjalną
stronę Sonosa w domyślnej przeglądarce i zamyka modal konta. Po ukończeniu
logowania użytkownik wraca do aktywnego okna AMC i wybiera w sesji „Sprawdź
logowanie do Sonos”. Ponowne otwarcie strony i skopiowanie adresu są dostępne
awaryjnie po wejściu do oczekującego logowania. Adres pozostaje dostępny tylko
w pamięci bieżącego procesu i znika po anulowaniu, powodzeniu albo zakończeniu
AMC. `Ctrl+F5` nadal oznacza
„Głośniki i grupy Sonos”. Gdy konto jest połączone, otwiera wybór domu i grupy;
gdy konto nie jest gotowe, prowadzi do okna konta zamiast kończyć się niejasnym
błędem.

Po otwarciu okna konta AMC jawnie wypowiada aktualny stan Sonosa, mimo że fokus
trafia na przycisk. Gdy `Ctrl+F5` wykryje już połączone konto, mówi „Konto
Sonos jest połączone. Wczytuję głośniki i grupy”, a następnie otwiera listę.
Nie uruchamia ponownie logowania i nie wysyła polecenia do głośnika.

Instrukcja logowania oraz bieżący stan konta są także polami tylko do odczytu
w kolejności Tab. Można wejść do nich klawiaturą i czytać je strzałkami. Gdy
logowanie jest dostępne, początkowy fokus trafia na „Rozpocznij logowanie”. Gdy
usługa jest niedostępna, fokus trafia na pole stanu z wyjaśnieniem zamiast na
sam przycisk „Zamknij”. Błąd operacji również przenosi fokus na czytelny stan.

Jeżeli skonfigurowany host odpowiada, ale nie ma pod nim uruchomionej usługi
logowania AMC, po wybraniu „Rozpocznij logowanie” program mówi: „Usługa
logowania Sonos nie jest uruchomiona pod skonfigurowanym adresem”. Ten stan
nie jest mylony z odmową Sonosa ani z oczekiwaniem na przeglądarkę.

Zainstalowany AMC Test używa publicznego brokera i ma zapisany właściwy callback.
Pierwsza próba potwierdziła otwarcie oficjalnego logowania Sonos, lecz nie
dotarła do callbacku, ponieważ Sonos zakończył ją błędem po wyborze kontynuacji
na etapie gospodarstwa domowego. Następna próba musi użyć tego samego zwykłego
konta, które w aplikacji Sonos widzi system użytkownika.

Jeżeli użytkownik wybierze „Sprawdź logowanie” przed powrotem z Sonosa, broker
odpowiada technicznym kodem `unknown_session`, ale AMC nie wypowiada tego kodu.
Program mówi, że logowanie nie jest jeszcze zakończone i pozwala sprawdzić
ponownie. Jeżeli oficjalna strona Sonosa pokazała błąd po wybraniu
„Kontynuuj”, komunikat poleca sprawdzić, czy przeglądarka używa tego samego
konta, które w aplikacji Sonos widzi system, a następnie rozpocząć próbę od
nowa. Nie wypowiada technicznych nazw callbacku, brokera ani etapu
`selectHousehold`. Istniejące testy obejmują kolejno oczekiwanie oraz udany
drugi odczyt tej samej próby.

Wydanie publiczne nie będzie wymagało od zwykłego użytkownika własnego klucza,
sekretu ani serwera. Użytkownik wybierze logowanie, otrzyma adres oficjalnej
strony Sonosa, zaloguje się tam i wróci do AMC, aby sprawdzić wynik. Sekret
integracji pozostaje tylko na wspólnym serwerze AMC.

Portal tej integracji udostępnił jedno pole adresu powrotu. Nowy callback
zastąpił w nim historyczną wartość za zgodą użytkownika. Stary broker może
pozostać uruchomiony jako niezależny punkt powrotu, ale bieżący klucz i AMC Test
korzystają z `sonos.stareaudycje.pl`; Hermes nie jest potrzebny w normalnym
przebiegu.

Istniejące konto autora jest prawidłowo połączone z Sonosem i obsługuje
sterowanie oraz grupowanie w zainstalowanym AMC Test. Zaszyfrowany rekord jest
jednak przypisany do historycznego originu. Przed instalacją wariantu z nowym
brokerem AMC albo poprosi autora o świeże, świadome logowanie, albo zastosuje
osobno sprawdzoną migrację tego samego konta i integracji. Nie będzie po cichu
przepisywać starego tokenu ani udawać, że należy on do innego serwera.

AMC nie zna i nie przechowuje sekretu integracji Sonos. Tokeny użytkownika
pozostają wyłącznie w procesie C# i w istniejącym lokalnym magazynie chronionym
systemem Windows; interfejs wxPython otrzymuje tylko stan, komunikaty i zaufany
adres autoryzacji.

Sonos z publicznym brokerem jest widoczny wyłącznie w AMC Test. Do AMC Public
trafi dopiero po świadomym odbiorze konta, odnowienia, sprzętu i NVDA. Broker
nie jest usługą Hermesa. WiiM nie zależy od tego brokera.

## Robocza instrukcja użytkownika po tym etapie

1. W AMC Test naciśnij `Ctrl+6` i wybierz Enterem „Rozpocznij logowanie do
   Sonos”.
2. W oknie konta wybierz „Rozpocznij logowanie”. Po przygotowaniu adresu fokus
   przejdzie na „Otwórz stronę logowania”.
3. Zaloguj się na oficjalnej stronie tym samym zwykłym kontem, które w aplikacji
   Sonos widzi Twój system. AMC nie otrzymuje hasła.
4. Po powrocie wybierz „Sprawdź logowanie”. Po powodzeniu wybierz „Głośniki i
   grupy Sonos” albo użyj `Ctrl+F5`.

To jest projekt instrukcji do odbioru i korekty użytkownika, nie opublikowana
instrukcja wydania.

## Roboczy changelog po tym etapie

- uproszczono widok rozłączonego Sonosa do jednego wiersza logowania;
- Enter na tym wierszu otwiera konto Sonos;
- ukryto czynności niepasujące do bieżącego stanu konta;
- po rozpoczęciu logowania fokus trafia na otwarcie oficjalnej strony;
- błąd braku konta pokazuje drogę do logowania, ale zwykły błąd sieci nie jest
  mylnie nazywany brakiem konta.

To jest projekt changelogu; nie został opublikowany.

## Uzasadnienie i decyzje

- Zachowano istniejący kontrakt klienta AMC:
  `POST /login/start`, `GET /login/callback`, `POST /login/result` i
  `POST /login/refresh`.
- Sekret Sonosa jest wyłącznie zmienną środowiskową serwera. Nie trafia do
  źródeł, instalatora, adresu przeglądarki ani aplikacji użytkownika.
- Każdy start używa osobnego losowego `state` oraz wyzwania PKCE. Callback może
  rozpocząć wymianę kodu tylko raz, a tokeny można odebrać tylko raz po
  przedstawieniu właściwego verifiera.
- Sesje żyją dziesięć minut i są trzymane tylko w pamięci. Restart unieważnia
  rozpoczęte logowanie zamiast zapisywać tokeny na serwerowym dysku.
- Broker ma ograniczenie tempa per adres i ścisły limit aktywnych sesji.
- Wyłączono informacyjne logi HTTP zawierające query string callbacku, ponieważ
  znalazłyby się tam krótkotrwały kod Sonosa i `state`.
- Produkcja wymaga HTTPS. Reverse proxy może zakończyć TLS tylko wtedy, gdy
  jego konkretny adres IP znajduje się w `SONOS_TRUSTED_PROXIES`. Bez tej
  wartości broker ignoruje `X-Forwarded-For` i `X-Forwarded-Proto`; nie ufa
  nagłówkom przesłanym bezpośrednio przez publicznego klienta.
- Przyjęto najwyżej jeden krok przekazania i najwyżej 16 dokładnych adresów IP.
  Nazwy hostów, zakresy CIDR, adresy ogólne oraz puste elementy zatrzymują start
  zamiast przypadkowo poszerzać zaufanie.
- Publiczny profil AMC nadal nie udostępnia Sonosa. W czasie rozwoju adres
  klienta pozostaje konfigurowalny, lecz wydanie ma używać jednego zaufanego,
  publicznego brokera. Zwykły użytkownik nie podaje identyfikatora klienta,
  sekretu ani własnego serwera. Przełączenie originu nastąpi dopiero po
  rzeczywistym publicznym wdrożeniu i odbiorze.
- Nie zmieniono znaczenia `Ctrl+F5` ani istniejącego transportu Sonosa. Dodano
  jedynie świadomą bramkę konta przed odczytem grup. Nazwa, skrót, opis, menu,
  pomoc `Ctrl+F1`, pełna pomoc i paleta `Ctrl+Shift+K` nadal pochodzą ze
  wspólnego katalogu poleceń.
- Host udostępnia wxPythonowi siedem wąskich operacji konta bez argumentów
  poświadczeń: stan, rozpoczęcie, sprawdzenie i anulowanie logowania,
  odświeżenie, ponowienie samego zapisu oraz wylogowanie. Odpowiedzi nie
  zawierają tokenów, sekretu, adresu brokera, nazw klas ani wartości enumów.
- `404` z punktu rozpoczęcia logowania nie jest prawidłową odpowiedzią
  brokera. Oznacza, że pod zaufanym hostem nie działa usługa AMC, dlatego ma
  osobny stały komunikat. Treść HTML lub JSON odpowiedzi nie jest cytowana.
- Sama wartość adresu w modelu nie może włączać logowania. Program przekazuje
  do hosta osobną informację o dostępności publicznego brokera. Gdy jest
  wyłączona, stan konta, próba logowania oraz odczyt celów kończą się lokalnym,
  użytkowym komunikatem przed wykonaniem jakiegokolwiek żądania HTTP.
- Nierutowalna domena `.invalid` jest wyłącznie bezpiecznym znacznikiem
  konfiguracji. Zostanie zastąpiona prawdziwym publicznym originem dopiero razem
  z wdrożeniem, konfiguracją Sonosa i odbiorem. Historyczne poświadczenia
  powiązane z innym originem nie są automatycznie używane ani migrowane.
- Wybrano przenośny wariant jednej stale działającej instancji Linux:
  Docker Compose uruchamia broker w prywatnej sieci, a Caddy wystawia wyłącznie
  porty 80 i 443 oraz automatycznie obsługuje certyfikat. Stałe adresy prywatne
  pozwalają brokerowi ufać dokładnie jednemu reverse proxy, a nie całej sieci.
- Obecny pamięciowy magazyn dziesięciominutowych prób logowania wyklucza hosting
  zasypiający lub skalujący usługę do zera. Restart bezpiecznie unieważnia próbę,
  ale nie może następować między rozpoczęciem logowania i callbackiem.
- Sekret może być podany z chronionego pliku `SONOS_CLIENT_SECRET_FILE`.
  Jednoczesna wartość i plik, względna ścieżka, brak pliku, nadmierny rozmiar
  albo wielowierszowa zawartość zatrzymują start bez ujawnienia sekretu.
- Historyczny AMC i bieżący AMC Test używają tego samego publicznego
  identyfikatora integracji
  `b051a8f0-499c-4deb-9f66-843a752c36e4`. Identyfikator nie jest sekretem.
  Historyczna dokumentacja wskazuje źródła dawnego brokera pod
  `/home/test/projekty/amc-sonos-auth` oraz konfigurację produkcyjną pod
  `/etc/amc-sonos-auth/sonos.env`; tych danych nie znaleziono lokalnie na
  komputerze Windows.
- Token użytkownika i sekret integracji są różnymi danymi. Zaszyfrowany plik
  Windows potwierdza udane logowanie użytkownika, ale nie zawiera i nie może
  zastąpić serwerowego `SONOS_CLIENT_SECRET`.
- Zainstalowany pakiet ma `sourceRevision`
  `1df89b186803cd6c4b1b4163a2a4ebd41e54f118`. W tej rewizji
  `SonosIntegrationDefaults.BrokerOrigin` wskazuje Hermesa. Broker jest używany
  do rozpoczęcia, sprawdzenia i odnowienia logowania. Wszystkie operacje
  urządzeń, grup, ulubionych, playlist i strumieni używają bezpośrednio
  `SonosControlApiClient` z tokenem konta i publicznym kluczem integracji.
- Źródła przygotowane 25.10.2026 mają publiczny origin
  `https://sonos.stareaudycje.pl/` i `BrokerAvailable=true`. Zainstalowany AMC
  Test pozostaje niezmieniony. Instalację wolno rozpocząć dopiero po dodaniu
  nowego callbacku, sprawdzeniu pakietu zwrotnego i kontroli aktywnych nagrań;
  historyczny rekord poświadczeń związany ze starym originem nie jest po cichu
  przepisywany.
- Oficjalny kontrakt Sonosa wymaga, aby `redirect_uri` odpowiadał jednemu z
  adresów zapisanych dla klucza integracji. Pozwala to najpierw dodać callback
  niezależnego brokera i zachować callback Hermesa jako punkt powrotu, zamiast
  wykonywać ryzykowne przełączenie jednoczesne.
- Nowy pakiet zawiera `MIGRATION_FROM_LEGACY.md`: kolejność bezprzerwowego
  wdrożenia, zakaz kopiowania tokenów, świeże logowanie po zmianie originu,
  odbiór i wycofanie nieudanej próby.

Kontrakt sprawdzono 2026-10-19 z aktualną oficjalną dokumentacją Sonosa:

- `https://docs.sonos.com/docs/authorize`;
- `https://docs.sonos.com/reference/create-authorization-code`;
- `https://docs.sonos.com/reference/refreshtoken`.

## Zmienione pliki

- `src/AccessibleMediaController.SonosBroker/AccessibleMediaController.SonosBroker.csproj`
- `src/AccessibleMediaController.SonosBroker/Program.cs`
- `src/AccessibleMediaController.SonosBroker/SonosBrokerApplication.cs`
- `src/AccessibleMediaController.SonosBroker/SonosBrokerOptions.cs`
- `src/AccessibleMediaController.SonosBroker/SonosBrokerModels.cs`
- `src/AccessibleMediaController.SonosBroker/SonosLoginSessionStore.cs`
- `src/AccessibleMediaController.SonosBroker/SonosOAuthGateway.cs`
- `src/AccessibleMediaController.SonosBroker/README.md`
- `src/AccessibleMediaController.Core/Sonos/SonosLoginContract.cs`
- `src/AccessibleMediaController.Core/Sonos/SonosLoginClient.cs`
- `scripts/Build-SonosBrokerBundle.ps1`
- `tests/AccessibleMediaController.SonosBroker.SmokeTests/*`
- `tests/AccessibleMediaController.Core.SmokeTests/SonosLoginClientTests.cs`
- `src/AccessibleMediaController.LiteHost/LiteSonosAccountOwner.cs`
- `src/AccessibleMediaController.LiteHost/LiteSonosCoordinator.cs`
- `src/AccessibleMediaController.LiteHost/LiteEngineHandlers.cs`
- `src/AccessibleMediaController.LiteHost/Protocol/LiteSonosContract.cs`
- `src/AccessibleMediaController.LiteHost/Program.cs`
- `wxlite/amc_wx_lite/gui.py`
- `wxlite/amc_wx_lite/host_client.py`
- `wxlite/amc_wx_lite/menu_model.py`
- `wxlite/amc_wx_lite/shortcuts.py`
- `wxlite/tests/test_sonos_account_dialog.py`
- `wxlite/tests/test_sonos_session.py`
- `wxlite/tests/test_command_palette.py`
- `tests/AccessibleMediaController.LiteHost.ProtocolTests/SonosContractTests.cs`
- `tests/AccessibleMediaController.Windows.SmokeTests/SonosAccountWiringTests.cs`
- `src/AccessibleMediaController.Core/Sonos/SonosIntegrationDefaults.cs`
- `src/AccessibleMediaController.Windows/Services/SonosAccountOwner.cs`
- `.dockerignore`
- `deploy/sonos-broker/Dockerfile`
- `deploy/sonos-broker/Dockerfile.runtime`
- `deploy/sonos-broker/compose.yaml`
- `deploy/sonos-broker/Caddyfile`
- `deploy/sonos-broker/.env.example`
- `deploy/sonos-broker/README.md`
- `deploy/sonos-broker/MIGRATION_FROM_LEGACY.md`
- `scripts/Build-SonosBrokerDeploymentBundle.ps1`
- `docs/zadania/AMC10-HERMES-AUDYT-SONOS-2026-10-20.md`
- `AccessibleMediaController.sln`
- `docs/zadania/README.md`

## Testy i wynik

- 2026-10-25: poprawka ponownego udostępniania adresu oczekującej sesji
  zaliczyła **38/38** testów koordynatora konta Sonos, **1/1** zestaw kontraktu
  protokołu i **8/8** testów okna konta wxPython. Ukierunkowane kompilacje Core,
  ProtocolTests i właściwego LiteHosta zakończyły się z 0 ostrzeżeń i 0 błędów.
- Pełna bramka paczki `8f42e319` zaliczyła **30/30** testów launchera i
  aktualizatora, obie kompilacje .NET z 0 ostrzeżeń i 0 błędów oraz regresję
  interfejsu: **2042 zaliczone**, 111 pominiętych i 13 podtestów zaliczonych.
  Kandydat przeszedł integralność i kontrolowany start. Po atomowej instalacji
  pierwsza kontrola stałego katalogu zostawiła osierocony launcher
  diagnostyczny `AMC.exe`; bramka nie ogłosiła sukcesu. Po zakończeniu wyłącznie
  tego procesu identyczna kontrola z `D:\Projekty Codex\AMC Python` przeszła,
  a zwykły start potwierdził dokładnie jedno odpowiadające okno, jeden launcher
  i jeden LiteHost z katalogu instalacji.

- 2026-10-24: bazowy broker zaliczył 18/18 testów. Dodano kontrolę, że
  zaufany `X-Forwarded-For` rozdziela limity dwóch klientów, oraz kontrolę
  filtrów logowania przy globalnym Debug/Trace. Rozszerzony zestaw zaliczył
  **20/20** testów zarówno w repozytorium, jak i ponownie ze źródeł
  rozpakowanych z finalnego ZIP-a. Testy używały atrap Sonosa i lokalnego
  portu; nie otwierały przeglądarki, nie logowały konta i nie sterowały
  głośnikami.
- Skrypt pakowania odtworzył zależności, wykonał testy, opublikował
  framework-dependent `net8.0` dla `linux-x64` bez apphosta oraz dołączył
  minimalny snapshot źródeł i testów. Manifest finalnego ZIP-a obejmuje 22
  pliki; wszystkie rozmiary i SHA-256 sprawdzono ponownie bez rozbieżności.
  Manifest ma `workingTreeClean=true`, `runtimeIdentifier=linux-x64`,
  `includesSecrets=false` i `includesSource=true`.
- Finalny ZIP ma 63 404 bajty i SHA-256
  `fdf9cb33560d80403ecdaa3991244a44567417a0f7f82d14ed7ef7f37e9a1e18`.
  Skan nie znalazł kluczy prywatnych, `.p8`, JWT, tokenów GitHub/AWS,
  `sonos_client_secret.txt`, symboli PDB ani wykonywalnego apphosta.
- Kontrolny proces Release na Windows odpowiedział `200` z `/health/ready`.
  Pięć próbek wskazało 48,59–50,26 MiB working set i 40,39–40,96 MiB private
  memory. Jest to pomiar orientacyjny; operator ma zwrócić `MemoryCurrent` i
  `MemoryPeak` z docelowego Ubuntu.
- Kod, testy i pakiet zostały przygotowane. Nie wykonano publikacji GitHub,
  instalacji na serwerze, wpisu w portalu Sonos, prawdziwego logowania ani
  zmiany i instalacji AMC Test.

- `dotnet build src/AccessibleMediaController.SonosBroker/AccessibleMediaController.SonosBroker.csproj -c Release --no-restore`
  — sukces, 0 ostrzeżeń, 0 błędów.
- `dotnet run --project tests/AccessibleMediaController.SonosBroker.SmokeTests/AccessibleMediaController.SonosBroker.SmokeTests.csproj -c Release`
  — 16/16 testów przeszło 2026-10-20.
- `dotnet build AccessibleMediaController.sln -c Release --no-restore` — sukces;
  nowy broker i jego testy kompilują się razem z całym rozwiązaniem.
- Testy obejmują: brak i częściową konfigurację, zgodność originu i callbacku,
  PKCE, jednorazową sesję, zły verifier bez utraty wyniku, ochronę przed drugim
  callbackiem, wygaśnięcie, równoległy limit pojemności, dokładny adres Sonosa,
  serwerową wymianę kodu, odnowienie z rotacją i bez rotacji oraz zgodność
  endpointów start/odmowa/wynik z klientem AMC, ścisłą listę proxy, odrzucenie
  fałszywego `X-Forwarded-Proto` od niezaufanego klienta oraz poprawne
  zakończenie TLS przez jawnie zaufane lokalne proxy.
- Integracyjny test HTTP działał wyłącznie na lokalnym porcie w środowisku
  `Development` i `Production`; nie otworzył przeglądarki, nie użył sieci
  Sonosa, konta ani głośnika. Pierwsza próba w piaskownicy nie mogła połączyć
  się z lokalnym gniazdem; identyczny test uruchomiony z dostępem do localhost
  zaliczył komplet 16 przypadków.
- Historyczny eksperyment z 20 października 2026 obejmował wyłącznie bezpieczne odczyty
  `https://hermes.tail6caad7.ts.net/health/live` i `/health/ready`. Host
  odpowiada przez HTTPS, ale oba adresy zwracają `404`. Pod originem wpisanym
  wtedy w AMC nie był więc uruchomiony przygotowany broker Sonos. Po późniejszej
  korekcie użytkownika Hermes nie jest już kandydatem do wdrożenia. Nie
  rozpoczynano logowania, nie otwierano przeglądarki i nie wysyłano poleceń do
  głośników.
- Skrypt `Build-SonosBrokerBundle.ps1` zbudował próbny pakiet framework-dependent
  bez symboli i sekretów. Manifest objął 4 pliki opublikowanego brokera, a
  kontrola ponownie obliczonych rozmiarów i SHA-256 nie wykazała rozbieżności.
  Próbny ZIP miał 30 025 bajtów i SHA-256
  `30e691e3726e09c689bd4df8896ef4b4fcd8c61c900feb215d6615d11f7cb2ae`.
  Ponieważ źródła były w trakcie pracy, manifest uczciwie oznaczył go jako
  `dirty`; nie jest to paczka do wdrożenia ani publikacji.
- Po zapisaniu zmian zbudowano czysty następca, który pierwotnie miał służyć
  próbie na Hermesie:
  `.test-output/sonos-broker/AMC-Sonos-Broker-0.1.0-alpha.422-9431fd8d2619.zip`.
  Archiwum ma 30 029 bajtów i SHA-256
  `388f5e7a1dd31f8cd8b2c609c2096a641571a528ca6c8fe20569e2f3359c8660`.
  Manifest wskazuje czysty commit
  `9431fd8d2619a1c267299e91b70f89637980ba40`, .NET 8, cztery pliki
  kontrolowane i pięć plików razem z manifestem. Sumę archiwum obliczono
  ponownie niezależnie. Pakiet nie został wdrożony ani uruchomiony i po korekcie
  z 20 października jest wyłącznie artefaktem historycznym, nie kandydatem do
  uruchomienia.
- Nie wykonano instalacji, publikacji, logowania ani wdrożenia publicznego.
- `python -m compileall -q wxlite/amc_wx_lite` — sukces.
- `dotnet build src/AccessibleMediaController.LiteHost/AccessibleMediaController.LiteHost.csproj --no-restore`
  — sukces, 0 ostrzeżeń, 0 błędów.
- `dotnet build tests/AccessibleMediaController.LiteHost.ProtocolTests/AccessibleMediaController.LiteHost.ProtocolTests.csproj --no-restore`
  — sukces, 0 ostrzeżeń, 0 błędów.
- `dotnet run --project tests/AccessibleMediaController.LiteHost.ProtocolTests/AccessibleMediaController.LiteHost.ProtocolTests.csproj --no-build -- --sonos`
  — jeden ukierunkowany zestaw Sonos zaliczony. Test potwierdza jawne etykiety
  i brak tokenu, adresu brokera oraz technicznych nazw stanów w odpowiedzi.
- Po rozdzieleniu odpowiedzi `404` historycznego hosta kompilacje Release testów Core i
  protokołu LiteHost zakończyły się z 0 ostrzeżeń i 0 błędów. Ukierunkowane
  testy klienta pierwszego logowania: **36 zaliczonych**. Ukierunkowany
  kontrakt Sonos LiteHost: **1/1 zaliczony**. Test potwierdza stały komunikat
  bez adresu brokera i rozróżnienie `404` od niezgodnej odpowiedzi innego
  rodzaju.
- 136 ukierunkowanych testów wxPython — zaliczone. Obejmują `Ctrl+F5`, menu,
  pomoc, paletę, Menedżer sesji, jawne etykiety, rozdzielenie stanu konta od
  tokenów, drogę konto–grupy oraz zachowanie innych kont i sesji.
- Pierwszy niepoprawny równoległy przebieg dwóch kompilacji został odrzucony:
  oba procesy próbowały pisać ten sam plik `obj`. Powtórzenie sekwencyjne
  przeszło bez błędów. Nie jest to wada kodu.
- Próba pełnego `wxlite/tests` w piaskownicy została zatrzymana po potwierdzeniu,
  że `tempfile` nie może pisać SQLite w wirtualnym katalogu `%TEMP%`. Wynik ten
  nie jest przedstawiany jako regresja ani jako zaliczenie.
- Pełny `wxlite/tests` poza piaskownicą: **1861 zaliczonych, 116 świadomie
  pominiętych, 11 podtestów zaliczonych**. Jedyny początkowy brak dotyczył
  wymaganego wcześniejszego pliku LiteHost Release, a nie zachowania kodu.
- `dotnet build src/AccessibleMediaController.LiteHost/AccessibleMediaController.LiteHost.csproj -c Release --no-restore`
  — sukces, 0 ostrzeżeń, 0 błędów; po nim brakujący
  `test_profile_edits_go_through_the_real_host` przeszedł: **1 zaliczony**.
- Zbudowano pełnego kandydata AMC Test
  `2026.10.20-fd5f423117f87c34aff6e9c4def22a985aa307a6-b7475d46`:
  manifest obejmuje 1667 plików, a cały katalog 1668 plików i około 133,59 MiB.
  SHA-256 manifestu:
  `C60D5072408CFC936160531CF627B8F35432EF4A2F14CEA064F4D08C098C1F66`.
- Pierwsza próba instalacji została prawidłowo zatrzymana przed podmianą.
  Między wcześniejszą kontrolą a etapem zamykania programu harmonogram o 12:30
  rozpoczął nagrywanie `3 - 2026-10-20 12-30.mp3`. AMC Test odmówił zwykłego
  zamknięcia, aktualizator zakończył się kodem 3 i nie zmienił zainstalowanych
  plików. To potwierdza działanie ochrony nagrywania, nie błąd instalatora.
- Po zakończeniu harmonogramu z 12:30 o 12:35 wykonano drugą bramkę: 15/15
  folderów było dostępnych, nie znaleziono plików `.amc-partial`, a następny
  harmonogram był dopiero o 13:00. Aktualizator ponownie nie podmienił plików,
  ponieważ działający frontend nie zakończył się po bezpiecznym `WM_CLOSE`.
  Dwa dodatkowe zwykłe polecenia zamknięcia skierowane do potwierdzonego
  głównego okna również zostały odrzucone. Okno było widoczne, włączone i bez
  okna modalnego. Tekst kontrolki stanu brzmiał: „Nie zamykam AMC Test,
  ponieważ trwa nagrywanie”. Późniejsza analiza pełnego dziennika wykazała, że
  wciąż działały cztery inne nagrania: Gdańsk Regiony, Meteor, RDC i PIK.
  Wszystkie zakończyły pliki oraz wpisy stanu około 12:50:51–12:50:56.
  Odmowa zamknięcia była więc prawidłową ochroną rzeczywistych nagrań, a nie
  pozostawionym wpisem po harmonogramie. Procesów nie zakończono wymuszenie.
- O 12:53 ponowna bramka wykazała 15/15 dostępnych folderów, 0 plików
  `.amc-partial`, 0 plików rosnących i następny sześciominutowy harmonogram
  o 13:00. Cztery równoległe nagrania zakończyły się około 12:51, więc zwykłe
  zamknięcie zostało przyjęte. Pierwsze wywołanie aktualizatora zwróciło kod 2 wyłącznie
  przez nieprawidłowe ujęcie ścieżek ze spacjami i nie rozpoczęło podmiany.
  Powtórzenie z poprawnymi argumentami zakończyło się kodem 0.
- Zainstalowana paczka przeszła pełne `--sprawdz-pakiet-cicho` z kodem 0.
  Manifest ma 1667 plików i SHA-256
  `C60D5072408CFC936160531CF627B8F35432EF4A2F14CEA064F4D08C098C1F66`.
  O 12:55 działały dokładnie trzy nowe procesy z katalogu instalacji: jeden
  uruchamiacz, jeden frontend i jeden LiteHost.
- Po odłączeniu aktywnych źródeł od Hermesa wyszukiwanie w `src` nie znalazło
  originu `hermes.tail6caad7.ts.net`; jedyny pozostały tekst znajduje się w
  asercji testowej chroniącej przed jego ponownym wprowadzeniem.
- `python -m compileall -q wxlite/amc_wx_lite` — sukces. Cztery bezpośrednie
  testy okna konta Sonos — **4/4 zaliczone**; potwierdzają ukrycie rozpoczęcia
  logowania, jawny komunikat, fokus „Zamknij” i brak technicznego adresu.
- Kompilacje Release LiteHost i jego testów protokołu — sukces, 0 ostrzeżeń,
  0 błędów. Ukierunkowany zestaw `--sonos` — **1/1 zaliczony**.
- Kompilacja Release `AccessibleMediaController.Windows.SmokeTests` — sukces,
  0 ostrzeżeń, 0 błędów. Pierwszy przebieg w piaskownicy zatrzymał resolver
  workloadów MSBuild bez diagnostyki źródłowej; identyczny przebieg poza tą
  izolacją przeszedł. `--sonos-account-wiring` zaliczył **24 sprawdzenia** bez
  pokazywania GUI.
- Przed testami nie znaleziono uruchomionego procesu AMC, LiteHost ani Pythona
  należącego do aplikacji oraz plików `.amc-partial` w jawnych katalogach
  projektu. Nie zamykano, nie instalowano i nie uruchamiano użytkowego AMC Test.
- Po dodaniu plikowego sekretu i pakietu wdrożeniowego kompilacja Release
  testów brokera zakończyła się z 0 ostrzeżeń i 0 błędów. Wszystkie
  **18/18** testów przeszło; nowe przypadki obejmują poprawny plik sekretu,
  konflikt dwóch źródeł, brak pliku i ścieżkę względną.
- Publikacja framework-dependent utworzyła 5 plików aplikacji o łącznym
  rozmiarze 97 504 bajtów. Na tym komputerze nie ma Docker Engine ani innego
  runtime'u kontenerowego, więc nie przedstawia się statycznej kontroli plików
  Compose jako rzeczywistego uruchomienia kontenerów.
- Skrypt pakowania z czystego commitu `2bac12d0` utworzył samodzielny ZIP
  `AMC-Sonos-Deployment-0.1.0-alpha.422-2bac12d03241.zip`: 34 856 bajtów,
  SHA-256 `defd288ec942bb22706ffe8e05d3a5c73454df3cec651e1f1a1abceffc3ce9cd`.
  Manifest obejmuje 10 plików, ma `workingTreeClean=true` i
  `includesSecrets=false`; niezależne ponowne obliczenie rozmiarów i sum nie
  wykazało rozbieżności. ZIP rozpakowano próbnie i ponownie zweryfikowano.
- Audyt historycznego magazynu wykazał plik
  `%LOCALAPPDATA%\AccessibleMediaController\credentials\sonos.bin`
  o rozmiarze 518 bajtów, zmieniony 19 października 2026 o 18:27 czasu
  lokalnego. Kontrolowany odczyt przez DPAPI bieżącego użytkownika nie wypisał
  żadnego poświadczenia. Potwierdził format 1, typ Bearer, zakres
  `playback-control-all`, obecność access tokenu i refresh tokenu oraz
  przypisanie do historycznego originu. Token dostępu wygasł 20 października,
  ale sam rekord nadal zawiera refresh token. Bufory jawnego tekstu wyzerowano
  po sprawdzeniu.
- Manifest zainstalowanego AMC Test wskazuje rewizję
  `1df89b186803cd6c4b1b4163a2a4ebd41e54f118`. Odczyt tej rewizji potwierdził
  broker `https://hermes.tail6caad7.ts.net/`, a odczyt bieżącego HEAD —
  nieroutowalny placeholder. To wyjaśnia jednocześnie działanie obecnej
  instalacji oraz brak gotowości nowszych źródeł do instalacji. W czasie audytu
  proces AMC nie był uruchomiony; nie wykonywano polecenia do głośników,
  odnowienia tokenu, kompilacji ani instalacji.
- Odczytowy raport Hermesa potwierdził działające usługi
  `amc-sonos-auth.service` i `amc-sonos-proxy.service`, nginx, zgodny publiczny
  identyfikator oraz niepusty sekret w chronionej konfiguracji pod nazwami
  `AMC_SONOS_*`. Nie ujawniono wartości, nie wykonano odnowienia, restartu ani
  zmiany konfiguracji. Źródła dawnego brokera są czyste na commicie
  `b27ed4b2e31fcc232677f9fe2f36aa36f237d1d3`.
- Próba pakowania po dodaniu instrukcji migracji zakończyła się sukcesem.
  Brudny pakiet kontrolny miał 37 148 bajtów i SHA-256
  `ff213bb24755b5e2f7cf8609aa11b34a68d1eb9dee31246c1633399dbbf067b5`.
  Manifest obejmował 11 kontrolowanych plików, zawierał
  `MIGRATION_FROM_LEGACY.md`, miał `includesSecrets=false` i nie zawierał
  `secrets/sonos_client_secret.txt`. Pakiet służy wyłącznie weryfikacji i nie
  jest kandydatem wdrożeniowym, ponieważ powstał przed commitem zmian.
- Ukierunkowane testy brokera: pierwsze 15 testów logiki przeszło w
  piaskownicy, a trzy próby HTTP zostały przez nią zablokowane na localhost.
  Identyczny zestaw uruchomiony z dostępem wyłącznie do `127.0.0.1` zaliczył
  wszystkie **18/18** przypadków. Nie użyto sieci Sonosa, konta ani głośników.
- Po commicie zbudowano czysty pakiet
  `AMC-Sonos-Deployment-0.1.0-alpha.422-b762c9b59b58.zip`: 37 153 bajty,
  SHA-256 `6b799ec360f62c1008494b44fd556ee8e11a616154825efd885cdd1de96d4643`.
  Manifest wskazuje pełny commit
  `b762c9b59b585fe3571a1fb0aaaf8fc83c3439fc`,
  `workingTreeClean=true`, `includesSecrets=false` i 11 kontrolowanych plików.
  Ponowne obliczenie wszystkich sum dało zero rozbieżności. Instrukcja migracji
  jest obecna w manifeście i ZIP-ie; `sonos_client_secret.txt` nie występuje.

## Wynik przygotowania klienta 25.10.2026

Poniższa lista zachowuje wynik pośredni sprzed instalacji. Została zastąpiona
przez późniejszą paczkę `5867ba48-3b26879e` opisaną w sekcji „Wersja i commit”;
nie należy jej czytać jako bieżącego stanu programu.

- Zmieniono wyłącznie źródła kandydata **AMC Test**: domyślny origin to
  `https://sonos.stareaudycje.pl/`, a publiczna możliwość logowania jest
  włączona. Stary AMC, jego instalacja, poświadczenia i broker nie zostały
  zmienione.
- Potwierdzono w kodzie i testach, że `404 {"error":"unknown_session"}` przed
  callbackiem jest stanem `Pending`: próba pozostaje aktywna, komunikat dla
  użytkownika nie ujawnia kodu technicznego, a kolejne sprawdzenie może
  zakończyć tę samą sesję sukcesem.
- Zaliczone ukierunkowane testy Sonosa: klient pierwszego logowania 36/36,
  odnowienie 58/58, koordynator konta 37/37 oraz parytet routera, katalogu i
  palety 6/6. Kompilacja Release projektu testów okien Windows zakończyła się
  bez ostrzeżeń i błędów.
- Kompilacja Release projektu testów okien Windows oraz późniejsza kompilacja
  LiteHosta zakończyły się bez ostrzeżeń i błędów. Pierwsza równoległa próba
  testów spowodowała kolizję pliku kompilacji, dlatego nie jest zaliczana;
  kolejne kompilacje wykonano pojedynczo.
- Na tym pośrednim etapie nie wykonano jeszcze instalacji, uruchomienia AMC
  Test, prawdziwego logowania, polecenia do głośnika ani publikacji GitHub.
- Po ręcznym zgłoszeniu poprawiono źródła okna konta AMC Test: instrukcja i
  stan są klawiaturowymi polami tylko do odczytu, a stan bez dostępnego
  logowania przejmuje początkowy fokus. Ukierunkowane testy okna, sesji i
  palety zaliczyły 48/48 przypadków. Pełna regresja wxPython po skierowaniu
  katalogu tymczasowego do worktree zaliczyła 1996 przypadków, 147 pominęła
  zgodnie z warunkami i zatrzymała dwa testy wymagające wcześniej zbudowanego
  LiteHosta; po jego poprawnej kompilacji oba pozostałe testy zaliczyły 2/2.
  W tym momencie zmiana czekała na bezpieczną instalację i ręczny odbiór NVDA.
  Późniejsza instalacja jest zapisana osobno i nie zmienia historycznego wyniku
  tej wcześniejszej bramki.

## Korekta odbioru NVDA i globalnych poleceń — 25.10.2026

Użytkownik wrócił z przeglądarki po rozpoczęciu logowania Sonos i zastał
główne okno AMC Test jako `niedostępne`; nie działały `Ctrl+1`, `Ctrl+2` ani
inne polecenia. Odczyt UI Automation potwierdził, że proces odpowiadał, lecz
okno główne było wyłączone przez nadal otwarty modal `Konto Sonos`. Nie było to
zawieszenie silnika ani skutek odtwarzania YouTube. Za zgodą użytkownika
zamknięto dokładnie ten modal i przywrócono dostęp do okna głównego.

Ta obserwacja zastępuje wcześniejszy projekt, w którym po `Rozpocznij
logowanie` okno konta pozostawało otwarte na przycisku `Sprawdź logowanie`.
Poprawiony kontrakt źródłowy:

- okno ma jedno pole tylko do odczytu `Informacje o logowaniu Sonos`, zamiast
  dwóch pól z powtarzającą się treścią;
- `Rozpocznij logowanie` przygotowuje próbę i automatycznie otwiera oficjalną
  stronę w domyślnej przeglądarce;
- po udanym otwarciu modal jest zamykany, więc główne okno AMC nie pozostaje
  wyłączone;
- zwykły widok Sonosa zawiera wtedy jeden wiersz `Sprawdź logowanie do Sonos`;
  Enter ponownie otwiera konto z fokusem na `Sprawdź logowanie`;
- `Otwórz stronę ponownie` i kopiowanie adresu pozostają awaryjnymi
  czynnościami stanu oczekiwania;
- błąd otwarcia przeglądarki nie zamyka modala: komunikat pozostaje w
  klawiaturowo czytelnym polu stanu i tam trafia fokus.

Podczas tego samego odbioru wykryto drugi błąd: globalny skrót
`Ctrl+Alt+Shift+R` był rozpoznawany w Sonosie, ale później odrzucał go filtr
dozwolonych akcji sesji. Dlatego zatrzymanie wszystkich nagrań wymagało
przełączenia do Radia. Od tej korekty polecenie ma jeden jawny zakres globalny,
dziedziczony przez TIDAL, Spotify, WiiM, Sonos i Frontier Smart. Pozycja menu,
paleta, `Ctrl+F1`, pełna pomoc, skrót i dispatcher korzystają z tego samego
modelu.

Rzeczywiste działania ochronne przed pracą źródłową i instalacją:

- o 19:42–19:44 zakończono nagrania `Jarosław`, `Tarnobrzeg` i `TVP Info`;
- po ponownej kontroli wykryto dwa później uruchomione nagrania: `Tarnobrzeg —
  YouTube na żywo` zakończył się sam po 60 sekundach, a `Kramer` został jawnie
  zatrzymany poleceniem aplikacji o 19:56:13; wynik `Completed`, 689,2 s, jeden
  plik o rozmiarze 10 987 823 bajty;
- zachowano starszy plik `.amc-partial`, ponieważ nie pochodzi z bieżącego
  zakończenia i nie wolno go usuwać bez osobnej oceny.

Testy źródłowe: 151/151 ukierunkowanych zaliczonych. Pełna regresja dała 2038
zaliczonych, 111 kontrolowanie pominiętych i 13 zaliczonych podtestów; cztery
przypadki lokalnego HTTP zostały zablokowane przez piaskownicę. Te same cztery
testy uruchomione ponownie z dostępem wyłącznie do `127.0.0.1` zaliczyły 4/4.
Na tym etapie są to dowody kodu i testów; nowa paczka, instalacja oraz ręczny
odbiór NVDA są zapisywane osobno po rzeczywistym wykonaniu.

Reguły zostały też utrwalone jako kontrakt, a nie jednorazowy wyjątek: projektowy
`AGENTS.md` wymaga zwalniania rodzica po przekazaniu do przeglądarki i wspólnego
zakresu poleceń ratunkowych. Wspólny skill `accessible-desktop-contracts`,
używany również przez inne projekty, zawiera odtąd te same wymagania w
`command-surfaces.md` i `service-pickers.md`.

### Materiał do dokumentacji użytkownika — projekt

W rozłączonej sesji Sonos wybierz `Rozpocznij logowanie`. AMC otworzy oficjalną
stronę Sonos i zamknie okno konta, dlatego można bezpiecznie wrócić Alt+Tabem do
działającego okna AMC. W sesji pozostaje jeden wiersz `Sprawdź logowanie do
Sonos`; naciśnij Enter po zakończeniu albo odmowie w przeglądarce. `Ctrl+F5`
otwiera zarządzanie kontem, a `Ctrl+Alt+Shift+R` zatrzymuje wszystkie nagrania
niezależnie od bieżącej sesji.

## Badanie niezależnego toru lokalnego — 08.11.2026

### Wynik

Obecny broker chmurowy pozostaje poprawną architekturą dla oficjalnego Sonos
Control API: sekret integracji jest na serwerze, a aplikacja publiczna nie zna
go i nie wymaga od użytkownika własnego konta deweloperskiego. Niezależna
biblioteka nie naprawi jednak błędu występującego na stronie Sonosa przed
callbackiem. Oficjalna dokumentacja nadal wymaga trójstronnego OAuth, publicznego
callbacku HTTPS oraz serwerowej wymiany kodu z użyciem sekretu. Identyczny tekst
„Oops, there was a problem” jest też zgłoszony w otwartej sprawie
`jjlawren/sonos_cloud#58`, więc błąd nie jest swoisty dla AMC; nie jest to jednak
dowód jednej konkretnej przyczyny po stronie Sonosa.

Jest realny drugi tor: lokalne sterowanie Sonosem przez SSDP/UPnP, bez logowania
do konta w AMC. Oficjalny Sonos określa UPnP jako protokół niewspierany, a
oficjalny Control API na LAN nie jest dostępny do szerokiej dystrybucji. Mimo
tego ten sam lokalny model działa w Home Assistant i aplikacji Sonos dla
Windows/macOS. Sonos domyślnie włącza UPnP i dostęp gościa; użytkownik może je
wyłączyć w `Konto > Prywatność i bezpieczeństwo > Bezpieczeństwo połączenia`.

Najlepszym kandydatem do próby w AMC Test jest `SoCo`:

- aktywnie utrzymywany Python, wydanie `0.32.2` z 06.11.2026, licencja MIT;
- testy projektu obejmują Windows, a bieżący kod wspiera nowe wersje Pythona;
- lokalne wykrywanie, grupy, transport, głośność, stan, kolejka, zdarzenia,
  biblioteka lokalna, Sonos Favorites i Sonos Playlists;
- brak OAuth Sonosa, client ID, sekretu, tokenu użytkownika i publicznego
  serwera; komputer musi być w tej samej sieci co głośniki;
- operacje sieciowe są synchroniczne, więc muszą działać poza wątkiem wxPython,
  z ograniczonym timeoutem, anulowaniem i pamięcią podręczną ostatnich urządzeń.

`SoCo-CLI` (Apache-2.0) jest wartościowym źródłem zachowania i wysokopoziomowych
operacji, ale nie powinien być uruchamiany przez AMC jako zewnętrzne polecenie.
Potwierdza lokalne odtwarzanie Sonos Favorites oraz linków udostępniania
Spotify, TIDAL, Deezer i Apple Music. W przypadku Apple Music nie używa tokenu
deweloperskiego AMC: Sonos odtwarza link przez usługę Apple Music już dodaną do
systemu użytkownika. To daje wąski, ale zgodny praktycznie wariant „Apple Music
na Sonosie”: odtworzenie zapisanego Ulubionego albo linku utworu, albumu lub
playlisty. Nie zastępuje to pełnej sesji Apple Music w AMC, wyszukiwania całego
katalogu ani dostępu do biblioteki konta Apple.

Pozostałe kandydatury są słabsze dla bieżącej architektury:

- `svrooij/sonos-net` jest eksperymentalny, niepełny, ma słabe wykrywanie i
  licencję GPL-3.0; nie włączać go do AMC;
- `svrooij/node-sonos-ts` jest dojrzalszy i ma licencję MIT, ale wymagałby
  dołączenia Node.js albo nowego procesu pomocniczego, choć AMC ma już prywatny
  runtime Python;
- własny klient UPnP w C# może później ograniczyć zależności, a AMC ma już
  sprawdzone SSDP/SOAP dla WiiM i Frontier Smart, lecz odtworzenie grup,
  metadanych, ulubionych i wyjątków Sonosa byłoby większym kosztem utrzymania
  niż odizolowanie SoCo za naszym kontraktem.

### Korekta użytkownika i zalecany model publiczny — 08.11.2026

Użytkownik doprecyzował, że „publiczny” oznacza instalator przekazywany wielu
osobom, z których każda uruchamia AMC na komputerze w swojej sieci domowej obok
własnego systemu Sonos. Nie chodzi o sterowanie Sonosem użytkownika z obcej
sieci ani o wspólny zdalny dostęp. To doprecyzowanie zastępuje zalecenie, aby
oba tory były potrzebne w pierwszym publicznym wydaniu.

1. `Sonos lokalny` jest jedynym wymaganym torem dla AMC Test i przyszłego AMC
   Public. Każda kopia aplikacji wykrywa Sonosy w sieci swojego użytkownika;
   nie łączy się z siecią autora programu. Nie ma okna logowania, client ID,
   sekretu, tokenu ani zależności od wspólnego serwera.
2. Istniejący broker chmurowy zostaje zachowany w źródłach i na serwerze jako
   bezpieczny punkt powrotu, ale dalsza praca nad nim zostaje odłożona. Nie jest
   warunkiem publicznego Sonosa. Nie usuwać go przed rzeczywistym odbiorem toru
   lokalnego na sprzęcie; później można świadomie zdecydować o jego utrzymaniu
   albo wycofaniu.
3. Tor chmurowy miałby wartość tylko dla sterowania własnym Sonosem spoza jego
   sieci, dla sieci blokującej ruch lokalny albo jako oficjalnie wspierane API.
   Użytkownik nie zgłosił takiej potrzeby, więc nie uzasadnia ona obecnej
   złożoności logowania.
4. Bieżący interfejs Sonosa — grupy, transport, Ulubione, playlisty, własne
   stacje, kolejka i presety — pozostaje. Zmieniana jest warstwa transportu pod
   istniejącym kontraktem, a nie projekt sesji i poleceń. Techniczne
   identyfikatory UPnP nie mogą trafić do NVDA.

Historyczny wariant z Hermesem również nie był lokalnym proxy do głośnika przez
VPN. Zgodnie z audytem i commitem `2759b508` Hermes uczestniczył tylko w OAuth i
odnawianiu tokenu, natomiast polecenia AMC szły do oficjalnego Sonos Control
API w chmurze. To wyjaśnia, dlaczego stary wariant działał także przy rozbudowanym
sterowaniu, ale nie daje dodatkowego kodu lokalnego do przeniesienia. Gotowy
interfejs i logika sesji są już w AMC Test; lokalny adapter ma zastąpić wyłącznie
źródło urządzeń, stanu i poleceń.

### Najmniejszy bezpieczny eksperyment w AMC Test

1. Dodać SoCo wyłącznie do prywatnego pakietu testowego i przygotować
   bezokienną sondę: wykrywanie z limitem czasu, ręczny adres IP oraz lista
   nazw głośników i grup. Niczego jeszcze nie odtwarzać.
2. Na rzeczywistym systemie sprawdzić S1/S2, UPnP, dostęp gościa, zaporę
   Windows, kilka kart sieciowych, brak multicastu i powrót po restarcie.
3. Włączyć odczyt stanu oraz play/pause/stop, następny/poprzedni i głośność;
   każda operacja w wątku roboczym, z anulowaniem i bez blokowania okna.
4. Dopiero później dodać kolejkę, grupowanie, Sonos Favorites i test linku
   Apple Music na koncie już podłączonym w Sonosie.
5. Przed publicznym wydaniem wykonać parytet poleceń i pomocy, odbiór NVDA,
   test zapory, test z wyłączonym UPnP, czysty instalator oraz macierz modeli
   Sonos. Awaria lokalnego toru ma dawać krótki komunikat i nigdy nie otwierać
   modala blokującego AMC.

### Źródła sprawdzone 08.11.2026

- `https://docs.sonos.com/docs/authorize`
- `https://docs.sonos.com/docs/connected-home-architecture`
- `https://support.sonos.com/en-gb/article/adjust-connection-security-settings`
- `https://github.com/SoCo/SoCo`
- `https://github.com/SoCo/SoCo/releases/tag/v0.32.2`
- `https://github.com/SoCo/SoCo/blob/master/soco/plugins/sharelink.py`
- `https://github.com/avantrec/soco-cli`
- `https://www.home-assistant.io/integrations/sonos`
- `https://github.com/jjlawren/sonos_cloud/issues/58`

W tym etapie wykonano badanie źródeł i architektury oraz zapis dokumentacji.
Nie zmieniono kodu produktu, zależności, serwera, integracji w portalu Sonos,
instalacji AMC Test ani AMC Public; nie wykonano kompilacji ani publikacji.

## Przyrost 08.11.2026 — lokalny Sonos w AMC Test

### Korekta stanu

Powyższe zdanie o braku zmian w kodzie opisuje wyłącznie wcześniejszy etap
badawczy z 08.11.2026. Później tego samego dnia, za zgodą użytkownika, zostało
zastąpione następującym stanem: lokalna obsługa Sonosa jest zaimplementowana,
przetestowana bez sprzętu, skompilowana i złożona jako nieinstalowany kandydat
AMC Test. AMC Public i serwerowy broker nie zostały zmienione.

### Zachowanie użytkowe

- AMC Test wykrywa Sonosy przez SSDP/UPnP w tej samej sieci lokalnej. Nie
  wymaga konta Sonos, logowania w przeglądarce, tokenu OAuth ani publicznego
  brokera.
- Wejście do sesji Sonos automatycznie wczytuje lokalne grupy. `Ctrl+F5`
  ponawia wyszukiwanie, a konfiguracja Sonosa w Menedżerze sesji prowadzi do
  tego samego wyboru grup, nie do okna konta.
- Stan w Menedżerze sesji brzmi `Wymaga wyszukania głośników`, a nie
  `Wymaga zalogowania`.
- Po wybraniu grupy pozostaje dotychczasowy interfejs AMC: odtwarzanie i pauza,
  poprzedni/następny, głośność, wyciszenie, skok w czasie, stan odtwarzania,
  Ulubione Sonos, Playlisty Sonos, Moje stacje i presety.
- Lista grup i wszystkie kontrolki przekazują NVDA wyłącznie nazwy głośników,
  nazwy materiałów i opisany stan. Adres IP, obiekty SoCo, identyfikatory domu,
  grupy i odtwarzaczy pozostają w modelu i nie są etykietami dostępnymi.
- Brak urządzeń daje użytkowy komunikat o tej samej sieci i lokalnym UPnP.
  Jeden niedostępny albo przegrupowywany głośnik nie unieważnia pozostałych
  poprawnie odczytanych grup.
- Nie dodano nowego skrótu. Zmieniono zachowanie istniejącego `Ctrl+F5`, więc
  menu, pomoc kontekstowa, pełna pomoc i paleta nadal korzystają ze wspólnego
  modelu poleceń bez nowej, równoległej listy.

### Realizacja i pakowanie

- Nowy adapter: `wxlite/amc_wx_lite/sonos_local.py`.
- Biblioteka SoCo jest importowana leniwie, a brak składnika daje kontrolowany
  komunikat zamiast blokować start całego AMC.
- `SoCo 0.32.2` i wszystkie zależności są przypięte wraz z SHA-256 w
  `wxlite/requirements-sonos-local-test.txt`. Instalacja próbna z
  `pip --require-hashes` zakończyła się poprawnie.
- Profil możliwości otrzymał `sonos.local` wyłącznie w AMC Test. Budowa AMC
  Public odrzuca zarówno moduł lokalnego Sonosa, jak i składniki SoCo w
  prywatnym runtime.
- Skrypt paczki kopiuje SoCo, `xmltodict`, `appdirs` i `lxml` tylko z osobnego,
  przypiętego środowiska. Wspólne zależności są przed budową sprawdzane po
  dokładnych wersjach.
- `Moje stacje` pozostają częścią profilu AMC. LiteHost może je odczytać bez
  domu znanego chmurowemu zapleczu Sonosa, po czym lokalny adapter uruchamia
  adres strumienia bezpośrednio na wybranej grupie.
- Dotychczasowy kod konta i brokera pozostaje jako nieaktywny wariant rezerwowy.
  Nie usunięto go i nie zmieniono konfiguracji działającego serwera.

### Wyniki automatycznej weryfikacji

- adapter, profile wydania i układ paczki: **54/54** testy Pythona;
- sesja Sonos, zachowanie klawiatury, stary ekran konta jako rezerwa i spójność
  zmienionego katalogu językowego: **46/46** w prywatnym runtime wxPython;
- ukierunkowane testy kontraktu Sonosa i `Moich stacji`: **2/2**;
- testy profili launchera i aktualizatora: **39/39**;
- kompilacja `AccessibleMediaController.LiteHost` w Release: **0 ostrzeżeń,
  0 błędów**;
- kontrola składni wszystkich zmienionych plików Pythona: zaliczona;
- braki angielskiego katalogu dla tekstów zawierających `Sonos`: **0**;
- instalacja zależności z obowiązkowymi sumami i import `SoCo 0.32.2`: zaliczone;
- kandydat paczki: **4043** pliki z manifestu, kontrola
  `AMC.exe --sprawdz-pakiet` zaliczona, import SoCo bezpośrednio z runtime
  kandydata zaliczony.

Pełny zastany test inwentaryzacji tłumaczeń nadal zgłasza wcześniejsze braki
spoza Sonosa, między innymi z równoległych zmian TIDAL i Time Shift. Nie zostały
one przedstawione jako regresja lokalnego Sonosa ani poprawiane w tym zadaniu.

### Kandydat i rozdzielenie etapów

- Źródła: zmienione i zweryfikowane w katalogu roboczym.
- Build: zakończony powodzeniem.
- Nieinstalowany kandydat:
  `D:\Projekty Codex\AMC - integracja wxPython\.test-output\amc-test-sonos-local-20261108`.
- Szkic instrukcji użytkownika i changelogu:
  `docs/zadania/AMC10-lokalny-Sonos-instrukcja-i-zmiany-szkic.md`.
- Identyfikator zawartości: `ab8f6843`; etykieta:
  `2026.11.08-19d08fd60da97736daa933a6cfc0d2ae901827a0-ab8f6843-robocza`;
  manifest ma `sourceDirty=true`, ponieważ jest to świadomy kandydat z
  niezatwierdzonego katalogu roboczego.
- SHA-256 manifestu:
  `11c10fb8c7ae252bd0b23349afb0e69469f5865966d6dc439d80e779e2be7c7d`.
- Rozmiar całego katalogu: 198 901 444 bajty, 4046 plików fizycznych.
- Instalacja: **nie wykonana**.
- Uruchomienie aplikacji: **nie wykonane**; wykonano wyłącznie bezokienkową
  kontrolę integralności i import bibliotek.
- Odbiór na prawdziwym Sonosie i ręczny odbiór NVDA: **niewykonane**, ponieważ
  głośnik nie jest obecnie dostępny.
- Wydanie AMC Public, publikacja instalatora i GitHub: **niewykonane**.
- Gałąź: `codex/konfigurowalne-skroty-20261026`; commit przyrostu: brak — stan
  roboczy oparty na `19d08fd60da97736daa933a6cfc0d2ae901827a0`.

### Odbiór z głośnikiem

1. Połączyć komputer i Sonos z tą samą siecią lokalną; profil sieci Windows
   powinien zezwalać na wykrywanie urządzeń i multicast SSDP.
2. Uruchomić kandydata dopiero po zwykłej bramce bezpieczeństwa instalacji,
   przejść `Ctrl+6` do Sonosa i sprawdzić automatyczny odczyt grup.
3. Nacisnąć `Ctrl+F5`, przejść strzałkami po grupach i sprawdzić z NVDA nazwę,
   liczbę głośników, stan oraz powrót fokusu.
4. Enterem wejść do grupy; sprawdzić pauzę/odtwarzanie, poprzedni/następny,
   głośność, wyciszenie i skok w czasie.
5. Sprawdzić `Ctrl+L`, `Ctrl+U`, `Ctrl+P`, Moje stacje i istniejące presety,
   w tym ulubioną stację radiową oraz playlistę.
6. Powtórzyć po przegrupowaniu lub wyłączeniu jednego głośnika i po ponownym
   uruchomieniu AMC. Dopiero po tym odbiorze przygotować instalację użytkową.

Znane ograniczenie tej ścieżki: automatyczne SSDP działa w tej samej podsieci.
Nie dodano jeszcze ręcznego adresu IP ani obsługi Sonosa przez VPN. Sterowanie
spoza domu nadal wymagałoby osobnego wariantu chmurowego i nie jest potrzebne
do obecnego celu.

## Znane ograniczenia

- Broker, domena, TLS, testy Michała Dziwisza i callback są gotowe. Nadal nie
  odnaleziono lokalnie pakietu zwrotnego operatora, więc jego manifest i
  podana suma nie mają niezależnego potwierdzenia na tym komputerze. Nie blokuje
  to diagnozy aktualnego błędu OAuth, który wystąpił przed callbackiem.
- Docelowy wariant to pojedyncza usługa systemd na Mikrusie srv28 z Ubuntu
  24.04 i ASP.NET Core Runtime 8.0.32. Docker i Caddy pozostają wariantem
  referencyjnym, a nie sposobem tego wdrożenia.
- Magazyn sesji jest pamięciowy i przeznaczony dla jednej instancji. Skalowanie
  na wiele replik wymaga wspólnego atomowego magazynu.
- Znanym i jedynym zaufanym peerem jest lokalny `cloudflared` pod
  `127.0.0.1`; działanie publicznej ścieżki zostało potwierdzone przez
  operatora. Zmiana topologii proxy wymaga ponownego audytu zaufanych peerów.
- Użytkownik wykonał wstępny odbiór NVDA starszego układu: pola instrukcji i
  stanu były czytelne, a nadmiarowe przyciski oraz niewłaściwy fokus zostały
  wykryte. Nowsza korekta zamykania modala i pojedynczego pola jest już
  zainstalowana w paczce `0423232e`, lecz jej ręczny odbiór NVDA pozostaje do
  wykonania. Udane logowanie, odmowa i odnowienie nadal wymagają obecności
  użytkownika.
- Prawdziwe rozpoczęcie logowania i otwarcie oficjalnej strony wykonano
  świadomie. Próba zakończyła się po „Kontynuuj” w wyborze gospodarstwa
  domowego Sonos, przed callbackiem. Odczytu grup i polecenia do głośnika nie
  wykonano. Próba w oknie prywatnym nie była miarodajna, ponieważ użyła adresu
  skopiowanego z już otwartej strony Sonosa. Należy ją powtórzyć od oryginalnego
  adresu udostępnionego przez poprawioną paczkę AMC Test.
- Nie potwierdzono regresji pozostawiającej fałszywe aktywne nagranie. Bramka
  instalacji widziała równoległe, rzeczywiste nagrania, których nie obejmowała
  początkowa kontrola samych plików harmonogramowych. Kolejne bramki muszą więc
  nadal łączyć stan hosta z kontrolą wszystkich jawnych folderów nagrań.
- Oznajmienie stanu, rozpoznanie niedostępnej usługi i bezpieczny stan
  nieskonfigurowanego brokera są obecne w zainstalowanej paczce. Końcowe
  potwierdzenie ich kolejności fokusu i mowy należy do odbioru NVDA.
- Produkcyjna integracja Sonos jest ponownie użyta, a nowy callback jest w niej
  zapisany. Portal pokazał jedno pole adresu powrotu; przywrócenie Hermesa
  wymagałoby świadomej ręcznej zmiany tego pola.
- Nie znaleziono na komputerze Docker Engine, Azure CLI, narzędzi Fly.io,
  Cloudflare, Google Cloud ani zmiennych `SONOS_*`. Nie ma więc lokalnego dowodu
  dostępu do zewnętrznego hostingu lub sekretu integracji. Publicznego
  wdrożenia nie wolno pozorować samym ZIP-em.
- Lokalny magazyn użytkownika nie zawiera sekretu integracji. Audyt potwierdził
  go w chronionej konfiguracji dawnego brokera; jego wartość nadal nie może
  trafić do odpowiedzi, historii terminala, repozytorium ani pliku zadania.

## Wynik bezpiecznego sprawdzenia na Hermesie

Pełny odczytowy raport znajduje się w
`AMC10-HERMES-AUDYT-SONOS-2026-10-20.md`. Nie zawiera sekretów ani tokenów.
Potwierdza następcę repozytorium, czysty commit, działające usługi, nginx,
zgodny identyfikator, obecność sekretu oraz możliwość bezpiecznego przeniesienia
go bez czatu. Historyczny callback to `/sonos/callback`; niezależny broker używa
`/login/callback`. Oba adresy należy utrzymać w integracji do zakończenia
migracji.

## Brama publicznego udostępnienia

1. [Zaliczone] Stała domena i hosting obsługują prawdziwy HTTPS.
2. [Zaliczone] W integracji Sonos zapisano dokładny callback nowego brokera
   `/login/callback`. Portal udostępnił jedno pole, więc zastąpił on historyczny
   callback za zgodą użytkownika.
3. [Zaliczone przez operatora] Sekrety są poza repozytorium, a
   `/health/ready` odpowiada stanem gotowości.
4. [W trakcie] Z użytkownikiem wykonać logowanie, odmowę, odnowienie oraz ponowne
   uruchomienie brokera.
5. Dopiero wtedy przenieść funkcję z AMC Test do profilu publicznego, wykonać testy
   parytetu możliwości/pomocy/palety i rozważyć odsłonięcie Sonosa w AMC.

## Odbiór lokalnego Sonosa i poprawki z 12.11.2026

Ten wpis zastępuje wcześniejsze, wyłącznie bezsprzętowe założenie, że lokalny
transport i `Moje stacje` są już odebrane. Użytkownik potwierdził na prawdziwym
Sonosie: wykrywanie grup, Ulubione Sonos, Playlisty Sonos oraz playlistę z Apple
Music. Jednocześnie zgłosił odrzucanie `Moich stacji`, nieskuteczne
odtwarzanie/zatrzymywanie, odczyt nazwy grupy przez NVDA+End zamiast informacji
o materiale oraz nieco wolną nawigację listy.

### Zachowanie po poprawce

- `Moje stacje` uruchamiają adres z profilu jako radio internetowe. SoCo dostaje
  jawne `force_radio=True`; wcześniej traktował adres HTTP/HTTPS jak zwykły
  plik i Sonos mógł go odrzucić.
- Gdy bieżące źródło nie obsługuje pauzy, adapter próbuje `Stop`. Następne
  `Odtwórz` nie wysyła niedozwolonego ogólnego wznowienia: ponownie uruchamia
  ostatni materiał świadomie wybrany w AMC, ale tylko w tej samej grupie.
- Gdy po zatrzymaniu AMC nie zna materiału, nie zgaduje ani nie uruchamia innego
  źródła. NVDA podaje: `Sonos nie może wznowić tego źródła. Uruchom materiał
  ponownie z listy Sonosa`.
- NVDA+End otrzymuje nazwę lub źródło materiału, stan, dostępną pozycję i
  głośność. Lokalna odpowiedź Sonosa nie zawiera wiarygodnego bitrate'u, więc
  AMC go nie wymyśla. Nazwa ostatniego materiału wybranego w AMC ma
  pierwszeństwo przed samą nazwą grupy.
- Automatyczna migawka Sonosa jest pobierana najwyżej raz na trzy sekundy,
  zamiast co sekundę. Jedna migawka wymaga kilku zapytań UPnP; ograniczenie
  zmniejsza konkurencję z poleceniami i nawigacją, nie opóźniając odpowiedzi na
  świadome polecenie użytkownika.
- Przejrzano równoległy adapter WiiM. WiiM uruchamia zapisany strumień własnym,
  urządzeniowym poleceniem i nie korzysta z semantyki `play_uri` SoCo; nie
  wymaga analogicznej zmiany.

### Weryfikacja i ograniczenia

- testy lokalnego adaptera i sesji Sonos: **43/43 zaliczone**;
- testy natywnego paska stanu NVDA+End: **14/14 zaliczone**;
- kompilacja składni całego pakietu `amc_wx_lite`: zaliczona;
- `git diff --check`: zaliczony;
- nie wykonano jeszcze ponownego odbioru na prawdziwym głośniku po tej
  poprawce; szczególnie trzeba sprawdzić stację HTTP i HTTPS, źródło bez pauzy,
  ponowne `Odtwórz`, NVDA+End oraz szybkość strzałek;
- testy źródłowe i kompilacja składni nie są dowodem zbudowania, instalacji ani
  publikacji paczki. Stan tych etapów jest zapisywany osobno poniżej.

### Build i instalacja z 13.11.2026

- Lokalny przyrost Sonosa zapisano w commicie `29ce4390`; drugi commit
  `5cc8e236` oddzielił fragmenty równoległej pracy nad Menedżerem sesji, które
  pojawiły się w `gui.py` w chwili pierwszego commita. Równoległe pliki
  robocze zachowano bez zatwierdzania i bez włączania do paczki Sonosa.
- Czysty, odłączony worktree na rewizji
  `5cc8e236b5f7f3f7d51f72d2fbf82617e6759e57` zbudował profil AMC Test na
  zachowanym runtime bazowym. Trzy projekty .NET skompilowały się poprawnie.
- Kandydat:
  `D:\Projekty Codex\AMC worktrees\sonos-local-fix-20261112\.test-output\amc-test-sonos-fix-20261112`.
  Etykieta `2026.11.12-5cc8e236b5f7f3f7d51f72d2fbf82617e6759e57-605b8721`,
  identyfikator zawartości `605b8721`, `sourceDirty=false`, **4225** plików.
- Pełna kontrola manifestu oraz kontrolowany start interfejsu i hosta kandydata
  są zaliczone. SoCo w paczce ma wersję `0.32.2`.
- Paczkę zainstalowano atomowo do `D:\Projekty Codex\AMC Python`; pełna
  kontrola zainstalowanego pakietu potwierdziła tę samą etykietę, commit,
  identyfikator i 4225 pliki. Nie wykonano publikacji GitHub ani AMC Public.
- Pierwsze dwa ręczne wywołania aktualizatora nie zmieniły instalacji: jedno
  dostało nieprawidłowo rozdzielone ścieżki i kod 2, a następne poprawnie
  rozpoznało roboczą kopię pierwszego aktualizatora i zwróciło kod 6. Po jej
  zakończeniu właściwe wywołanie z `ProcessStartInfo.ArgumentList` wykonało
  jedną udaną transakcję.
- Standardowy launcher uruchamiany przez krótkie polecenie narzędziowe był
  kończony razem z drzewem tego polecenia. Do ręcznego odbioru uruchomiono ten
  sam zainstalowany frontend bezpośrednio z prywatnego runtime, z hostem i
  profilem `test`; proces pozostaje aktywny. Nie jest to jeszcze dowód wyniku
  testu na głośniku ani odbioru NVDA.

### Szkic instrukcji użytkownika i changelogu

- W sesji Sonos wybierz grupę, otwórz `Biblioteka`, następnie `Moje stacje` i
  uruchom stację Enterem. Spacja zatrzymuje albo wstrzymuje, zależnie od
  możliwości źródła; kolejna Spacja ponownie uruchamia ostatni materiał AMC.
- NVDA+End podaje wyłącznie dane dostępne z głośnika. Brak bitrate'u w tym
  komunikacie oznacza, że Sonos go nie zwrócił, a nie błąd obliczeń AMC.
- Zmiany: poprawione uruchamianie własnych stacji radiowych, bezpieczne
  zatrzymanie i ponowne uruchomienie źródeł bez pauzy, użyteczniejszy status
  NVDA+End i rzadsze odpytywanie UPnP podczas nawigacji.

### Wydanie poprawki lokalnego Sonosa — 13.11.2026

- Końcowy kandydat AMC Test oraz instalacja przeszły pełną integralność i
  kontrolowany start. Zainstalowano wersję użytkową `13.11.2026, 00:28` w
  `D:\Projekty Codex\AMC Python`.
- Instalator `AMC-Test-2026-11-13-0028.exe` został zbudowany z commita
  `f38ea768`; źródłowa poprawka Sonosa pozostaje w `29ce4390`.
- Wydanie wstępne `test-2026.11.13.0028` opublikowano na GitHubie. Instalator
  ma 73 219 776 bajtów i SHA-256
  `16542007d32c9a89c64127c06ccc91c5c58d44f0d43088fd76b273ad9229846e`;
  zdalny załącznik ma ten sam rozmiar i digest.
- Ponowny odbiór na prawdziwym Sonosie nadal jest wymagany. Publikacja dowodzi
  źródeł, automatycznych testów, buildu i dystrybucji, ale nie wyniku tej próby
  sprzętowej.
- Adres wydania:
  `https://github.com/michalkasperczak/AMC/releases/tag/test-2026.11.13.0028`.

## Odbiór na prawdziwym głośniku i korekta — 14.11.2026

### Korekta użytkownika zastępująca wcześniejsze założenie odbioru

Użytkownik potwierdził, że lokalny AMC Test w tej samej sieci poprawnie
wykrywa grupę, odtwarza Ulubione Sonos, Playlisty Sonos oraz playlistę opartą
na Apple Music. Nie zadziałały natomiast `Moje stacje Sonos` oraz
odtwarzanie/zatrzymanie w części stanów. Nawigacja listy była odczuwalnie
wolna. `NVDA+End` podał nazwę grupy, bez bitrate'u. To jest pierwszy odbiór na
rzeczywistym głośniku po wydaniu `test-2026.11.13.0028` i zastępuje wcześniejszy
stan „odbiór sprzętowy niewykonany”.

Brak bitrate'u nie jest obecnie regresją obliczeń: lokalne API Sonosa nie
zwraca AMC wiarygodnego bitrate'u bieżącego materiału. Gdy urządzenie nie
zwraca też tytułu ani źródła, nazwa grupy pozostaje uczciwym tekstem
zastępczym; wartości bitrate'u nie wolno zgadywać.

### Zmiany źródłowe

- automatyczna migawka UPnP działa tylko w widoku odtwarzacza; chodzenie po
  Bibliotece i listach Sonosa nie uruchamia już odpytywania co trzy sekundy;
- przy stanie `zatrzymane`, gdy materiał uruchomiono poza AMC i nie ma go w
  pamięci bieżącego procesu, Spacja przekazuje próbę wznowienia do głośnika
  zamiast blokować ją z góry;
- `Moje stacje` najpierw zachowują radiowy wariant URI SoCo, a po jego
  odrzuceniu wykonują jedną bezpośrednią próbę tego samego adresu. Adres nie
  trafia do komunikatu NVDA ani logiki etykiet;
- działające Ulubione i Playlisty Sonos nie zostały zmienione;
- przejrzano równoległy adapter WiiM: nie używa cyklicznej migawki SoCo ani
  dwóch wariantów `play_uri`, więc nie wymaga analogicznej poprawki.

### Testy i wynik

- `test_sonos_local.py`, `test_sonos_session.py` i
  `test_speaker_content_contract.py`: **56/56 zaliczonych** bez widocznego GUI;
- pierwsza próba testów nie zebrała modułów z powodu brakującego `PYTHONPATH`;
  po uruchomieniu z właściwym katalogiem pakietu identyczne testy przeszły;
- źródła są poprawione i testy celowane są zaliczone. Czysty kandydat oraz
  stała instalacja przeszły kontrolę integralności i kontrolowany start;
  ponowny odsłuch na głośniku oraz publikacja GitHub nie zostały jeszcze
  wykonane dla tej korekty.

### Profil Public sprawdzony 14.11.2026

Nocne wydanie `public-2026.11.14.0329`, opublikowane 14.11.2026 o 03:36 czasu
polskiego z rewizji `ea5a8bd0`, **nie zawiera Sonosa**. Profil wydania usuwa
sesję `sonos` oraz możliwości `session.sonos` i `sonos.local`, a skrypt budowy
odrzuca zależności SoCo dla AMC Public. Sonos pozostaje na tym etapie wyłącznie
w AMC Test; samo opublikowanie nocnego aktualizatora Public nie jest dowodem
włączenia tej funkcji.

### Materiał do dokumentacji użytkownika

W sesji Sonos listy Biblioteki nie są już w tle odpytywane o stan głośnika.
Spacja próbuje wznowić także materiał zatrzymany wcześniej poza AMC. W
`Biblioteka > Moje stacje Sonos` Enter próbuje dwóch zgodnych lokalnych sposobów
przekazania adresu, bez pokazywania technicznych danych. `NVDA+End` podaje tylko
dane zwrócone przez głośnik; brak bitrate'u oznacza brak takiej informacji w
lokalnym API Sonosa.

### Wersja i commit korekty

- stan źródeł: zmieniony, przetestowany, zbudowany i zainstalowany;
- commit źródeł i testów: `a92ab963861a2b16413a3159142457a66399ccd0`;
- commit dokumentujący odbiór: `1eff98e50eeca6df63b393859f012dd154bf73d5`;
- zbudowana i atomowo zainstalowana paczka AMC Test:
  `2026.11.14-1eff98e50eeca6df63b393859f012dd154bf73d5-c5e46a82`,
  `sourceDirty=false`, 3812 plików, katalog
  `D:\Projekty Codex\AMC Python`;
- kandydat i stała instalacja przeszły integralność oraz kontrolowany start
  interfejsu i hosta. Zwykły start widocznego okna z tej instalacji został
  przechwycony przez starszą, już otwartą instalację `AMC Test — 12.11.2026,
  23:20`, w której użytkownik ma niedokończone okno „Dodaj dynamiczną stację
  YouTube”; tej instancji nie zamykano, aby nie utracić wpisanych danych;
- build i instalacja: wykonane; publikacja GitHub i odbiór na głośniku:
  niewykonane;
- następny krok: po bezpiecznym zakończeniu starszej instancji uruchomić nowe
  AMC Test, a następnie powtórzyć na głośniku `Moje stacje`, Spację,
  `NVDA+End` i szybkość strzałek.

## Wersja i commit

- Korekta diagnostyczna z 25.10.2026: użytkownik nie pominął przycisku.
  Po automatycznym przekazaniu logowania do przeglądarki AMC zamykał modal,
  lecz ponowny `sonos.accountStatus` zwracał tylko stan oczekiwania, bez
  zapamiętanego w procesie adresu tej samej próby. Dlatego przy ponownym
  otwarciu konta znikały „Otwórz stronę ponownie” i „Kopiuj adres logowania”.
  Skopiowanie adresu z już otwartej strony Sonosa nie stanowi czystej próby w
  oknie prywatnym, ponieważ mógł to być adres pośredni zależny od sesji
  przeglądarki. Ta korekta zastępuje wcześniejsze uznanie próby prywatnej za
  miarodajne powtórzenie błędu.
- Poprawka utrzymuje pierwotny, zweryfikowany adres autoryzacji wyłącznie w
  pamięci bieżącego procesu i zwraca go razem z atomowo odczytaną migawką tylko
  wtedy, gdy ta sama próba nadal oczekuje. Anulowanie, sukces albo stan
  końcowy usuwają adres. Protokół ponownie sprawdza host i ścieżkę Sonosa przed
  przekazaniem adresu do wxPython. Zmieniono również opis starego zapisu:
  wyjaśnia teraz, że AMC go nie używa i że nie blokuje on nowego logowania.
- Weryfikacja źródłowa poprawki: koordynator konta Sonos **38/38**, kontrakt
  protokołu Sonos **1/1**, okno konta wxPython **8/8**; obie ukierunkowane
  kompilacje .NET zakończone z 0 ostrzeżeń i 0 błędów. Ręczna próba prywatna
  pozostaje osobnym, jeszcze niewykonanym etapem.
- Poprawka jest w lokalnym commicie `8f42e319453164779a0ff2c3ab574998f4694a76`
  — `fix(amc10): zachowaj adres logowania Sonos`. Zbudowano, atomowo
  zainstalowano i uruchomiono paczkę AMC Test
  `2026.10.25-8f42e319453164779a0ff2c3ab574998f4694a76-0423232e`.
  Manifest ma `sourceDirty=false`, identyfikator zawartości `0423232e` i 1672
  pliki. O 21:06 potwierdzono launcher, LiteHost i dokładnie jedno odpowiadające
  okno `AMC Test — 25.10.2026, 21:04`, wszystkie wyłącznie z
  `D:\Projekty Codex\AMC Python`. AMC Public i GitHub pozostają bez zmian.

- Podpowiedź dla oczekującego logowania oraz zapis porównania z archiwalnym AMC:
  commit `af731db1` — `fix(amc10): wyjasnij oczekujace logowanie Sonos`.
  Ukierunkowane testy: 36/36 klienta logowania Sonos oraz 1/1 zestaw protokołu
  LiteHost. Następnie pełna bramka promocji zaliczyła 30/30 testów launchera i
  aktualizatora, obie kompilacje .NET z 0 ostrzeżeń i 0 błędów oraz pełną
  regresję 2042 zaliczonych, 111 pominiętych i 13 podtestów.
- Zbudowana, atomowo zainstalowana i uruchomiona paczka AMC Test:
  `2026.10.25-af731db1de8269e9223c1e11e4cc8a9f8abf2f6c-4be5eba1`.
  Manifest ma `sourceDirty=false`, identyfikator zawartości `4be5eba1` i 1672
  pliki. Kandydat i stała instalacja przeszły pełną kontrolę integralności oraz
  kontrolowany start. O 20:26 potwierdzono odpowiadające okno
  `AMC Test — 25.10.2026, 20:25` i trzy składniki uruchomione wyłącznie z
  `D:\Projekty Codex\AMC Python`. AMC Public, instalatora publicznego ani
  publikacji GitHub nie wykonano.
- Zamykanie modala po przekazaniu do przeglądarki, jeden wiersz powrotu,
  pojedyncze pole informacji oraz globalny zakres zatrzymania nagrań: commit
  `9d6cf642` — `fix(amc10): zwolnij AMC po otwarciu Sonosa`. Testy źródłowe:
  151/151 ukierunkowanych oraz pełna regresja 2038 zaliczonych, 111 pominiętych
  i 13 podtestów; cztery testy lokalnego HTTP zablokowane w piaskownicy
  zaliczyły 4/4 po kontrolowanym powtórzeniu na `127.0.0.1`. Późniejsza paczka
  `a537c97b` zawiera ten commit; ręczny odbiór NVDA nadal pozostaje osobną
  bramką.
- Zbudowana, atomowo zainstalowana i uruchomiona paczka AMC Test:
  `2026.10.25-f283950e218008a54b89a03f4aede25f2dca27b8-a537c97b`.
  Manifest ma `sourceDirty=false`, identyfikator zawartości `a537c97b` i 1672
  pliki. Bramka promocji zaliczyła testy launchera i aktualizatora 30/30,
  obie kompilacje .NET z 0 ostrzeżeń i 0 błędów, pełną regresję 2042
  zaliczonych, 111 pominiętych i 13 podtestów, integralność oraz kontrolowany
  start kandydata i instalacji. O 20:06 potwierdzono dokładnie jedną instancję
  interfejsu, proces launchera i LiteHosta z `D:\Projekty Codex\AMC Python`;
  główne okno `AMC Test — 25.10.2026, 20:05` odpowiada i jest włączone.
  Pierwsza próba promocji w piaskownicy została przerwana przed budową po dwóch
  blokadach testów procesów Windows; identyczna bramka poza piaskownicą
  zaliczyła 30/30. Publikacji GitHub ani AMC Public nie wykonano.
- Uproszczenie rozłączonej sesji i okna konta Sonos: commit
  `5867ba4826ec6def767367fd146c5d4a38c8eb7b` —
  `fix(amc10): uprosc logowanie Sonos`.
- Zbudowana i zainstalowana paczka AMC Test:
  `2026.10.25-5867ba4826ec6def767367fd146c5d4a38c8eb7b-3b26879e`,
  identyfikator zawartości `3b26879e`, 1672 pliki, `sourceDirty=false`.
  Instalacja i uruchomienie z
  `D:\Projekty Codex\AMC Python` są potwierdzone 25.10.2026 o 19:32; tytuł
  okna: `AMC Test — 25.10.2026, 19:30`.
- Przed instalacją zaliczono 117/117 testów ukierunkowanych. Bramka promocji
  zaliczyła 30/30 testów launchera i aktualizatora, obie kompilacje kontrolne z
  0 ostrzeżeń i 0 błędów, pełną regresję interfejsu 2037 zaliczonych, 111
  pominiętych i 13 zaliczonych podtestów, integralność paczki oraz odizolowany
  start kandydata i instalacji. Pokwitowanie ma `tests: zaliczone`.
- Pierwsza prawdziwa próba OAuth doszła do oficjalnego wyboru gospodarstwa
  domowego Sonos, ale zakończyła się błędem przed callbackiem. Nie dowodzi to
  błędu brokera; odbiór prawdziwego połączenia pozostaje otwarty.
- Publiczny origin i włączenie logowania wyłącznie w źródłach AMC Test:
  commit `bf0925d566079359caa8c2d9fae785a361c59f34` —
  `feat(amc10): przygotuj AMC Test na publiczny broker`.
- Klawiaturowy dostęp do instrukcji i stanu oraz poprawiony fokus okna konta:
  commit `5b43ce95161f55ffc6dcbee7a119b857d18cb826` —
  `fix(amc10): udostepnij stan logowania Sonos`.
- Publikacji GitHub ani wydania AMC Public nie wykonano.
- Przekazanie systemd/Cloudflare, pakowanie `linux-x64`, ochrona logów i testy
  20/20: commit `bc56ce577bfac4d86c68ecc4f6f90a56a6c22e7a` —
  `build(amc10): przygotuj przekazanie brokera dla systemd`.
- Finalny pakiet:
  `AMC-Sonos-Deployment-0.1.0-alpha.433-bc56ce577bfa.zip`, 63 404 bajty,
  SHA-256 `fdf9cb33560d80403ecdaa3991244a44567417a0f7f82d14ed7ef7f37e9a1e18`.
  Lokalizacja przekazania:
  `D:\Projekty Codex\AMC - pakiety do weryfikacji\Sonos\2026-10-24\do-operatora`.
- Gałąź `integration/sonos-broker-20261024` pozostaje lokalna. Publikacji
  GitHub nie wykonano, ponieważ wymaga osobnej zgody na konkretny stan.
- Wersja finalnego pakietu brokera: `0.1.0-alpha.433`. Wcześniejsze wpisy
  `alpha.422` poniżej opisują historyczne pakiety z etapu 20.10.2026.
- Commit źródeł, testów i pierwszego zapisu dokumentacji: `97a29c09`.
- Commit bezpiecznego reverse proxy i testów 16/16: `1db05e74`.
- Commit okna konta Sonos, bramki `Ctrl+F5`, testów i zapisu realizacji:
  `6b967a29`.
- Powtarzalny skrypt tworzenia lokalnego pakietu brokera i jego manifestu:
  przygotowany oraz sprawdzony 20 października 2026; commit `2bac12d0`.
- Kandydat AMC Test: `2026.10.20-fd5f423117f87c34aff6e9c4def22a985aa307a6-b7475d46`.
- Instalacja i uruchomienie AMC Test są potwierdzone dla
  `2026.10.20-fd5f423117f87c34aff6e9c4def22a985aa307a6-b7475d46`
  w `D:\Projekty Codex\AMC Python` 20 października 2026 o 12:55.
- Publikacja: brak.
- Jawna mowa bieżącego stanu kont Spotify i Sonos: commit `39386e90`.
  **124** testy reprezentatywnych powierzchni interfejsu oraz ukierunkowane
  zestawy protokołu Spotify i Sonos **2/2** zaliczone; kompilacja Release bez
  ostrzeżeń i błędów. Instalacji i publikacji nie wykonywano.
- Historyczny lokalny pakiet brokera, pierwotnie przygotowany do próby na Hermesie:
  źródło `9431fd8d`, ZIP 30 029 bajtów, SHA-256
  `388f5e7a1dd31f8cd8b2c609c2096a641571a528ca6c8fe20569e2f3359c8660`.
  Budowa i integralność są potwierdzone; wdrożenia, konfiguracji sekretów,
  logowania i publikacji nie wykonywano. Pakiet nie jest obecnym celem
  wdrożenia i nie należy go uruchamiać na Hermesie.
- Osobny użytkowy stan braku endpointu brokera: commit `c6eaeee9` —
  `fix(amc10): rozpoznaj brak uslugi logowania Sonos`. Kompilacja Release oraz
  testy 36 i 1/1 są zaliczone; instalacji ani publikacji nie wykonywano.
- Usunięcie Hermesa z aktywnych źródeł klienta, jawna bramka dostępności,
  użytkowy stan niedostępności i testy: commit `3ab4c079` —
  `fix(amc10): oddziel logowanie Sonos od Hermesa`. Kod i testy są
  zweryfikowane; paczki, instalacji, uruchomienia i publikacji nie wykonywano.
- Kontenerowy wariant niezależnego serwera, Caddy, plikowy sekret i testy
  18/18: commit `5cfb60bb` — `feat(amc10): przygotuj niezalezne wdrozenie
  brokera`.
- Powtarzalny samodzielny ZIP bez źródeł i sekretów: commit `2bac12d0` —
  `build(amc10): pakuj broker do wdrozenia`. Pakiet został zbudowany i
  zweryfikowany lokalnie; nie został wysłany ani uruchomiony na serwerze.
- Dowód historycznego logowania użytkownika, rozdzielenie tokenu od sekretu
  integracji i bezpieczna lista kontroli dla Hermesa: commit `8ac9b2be` —
  `docs(amc10): zapisz dowod historycznego logowania`.
- Korekta stanu działającego konta, dokładne rozdzielenie bezpośredniego
  sterowania od brokera logowania oraz bramka przed instalacją źródeł z
  wyłączonym brokerem: commit `2759b508` —
  `docs(amc10): doprecyzuj dzialajace konto Sonos`.
- Raport Hermesa, bezprzerwowa instrukcja przejścia i dołączenie jej do pakietu
  wdrożeniowego: commit `b762c9b5` —
  `docs(amc10): przygotuj migracje dzialajacej integracji`.

## Porządek dysku

- 2026-10-25: po zakończeniu regresji usunięto wyłącznie utworzony w tym etapie
  katalog tymczasowy
  `D:\Projekty Codex\AMC worktrees\sonos-broker-handoff-20261024\.test-tmp\sonos-ui-20261025`
  o rozmiarze 188 326 bajtów, około 184 KiB. Zawierał odtwarzalne pliki
  tymczasowe pytest; nie podlega odzyskaniu. Źródła, wyniki w dokumentacji,
  kompilacje Release i instalacja użytkowa pozostały nietknięte.
- 2026-10-24: po pozytywnej kontroli usunięto wyłącznie brudny pakiet próbny,
  rozpakowaną kopię finalnego ZIP-a i drugi katalog weryfikacyjny. Ostatnie dwa
  katalogi zajmowały łącznie 1 627 287 bajtów, około 1,55 MiB; wcześniejszy
  brudny pakiet zajmował 223 381 bajtów. Są odtwarzalne i nie podlegają
  odzyskaniu. Zachowano finalny ZIP, plik jego sumy, odpowiedź dla operatora,
  źródła, commit oraz pliki kompilacji do czasu odbioru etapu.
- Po pomyślnej weryfikacji usunięto wyłącznie odtwarzalny dziennik diagnostyczny
  `D:\Projekty Codex\AMC - integracja wxPython\.test-output\sonos-windows-build.log`
  o rozmiarze 1 402 075 bajtów, czyli około 1,34 MiB. Plik nie jest możliwy do
  odzyskania, ale można go ponownie wytworzyć kompilacją diagnostyczną.
- Nie usunięto zastanych katalogów `.test-output` ani `.test-tmp`, ponieważ ich
  pozostała zawartość nie powstała wyłącznie w tym etapie i wymaga osobnej oceny.
- Po weryfikacji usunięto należące wyłącznie do tego etapu brudny pakiet próbny,
  osobny katalog publikacji oraz rozpakowany czysty pakiet i katalog kontroli:
  razem około 346 KiB z
  `D:\Projekty Codex\AMC - integracja wxPython\.test-output`. Są odtwarzalne i
  nie podlegają odzyskaniu. Zachowano jedyny czysty ZIP wdrożeniowy o rozmiarze
  34 856 bajtów oraz nie dotknięto zastanych `.test-output` i `.test-tmp`.
- Po kontroli nowego pakietu usunięto dokładnie katalog brudnej próby
  `.test-output\sonos-deployment-migration-check` oraz rozpakowany katalog
  czystego kandydata pod `.test-output\sonos-deployment-migration-final`.
  Odzyskano około 189 KiB. Pliki są odtwarzalne i nie podlegają odzyskaniu;
  zachowano jedyny zweryfikowany ZIP nowego kandydata o rozmiarze 37 153 bajtów.
