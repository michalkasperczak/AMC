# CAST-001 — Google Cast jako wyjście odtwarzania

Status: **Zrobione w podstawowym zakresie — AMC Test alpha.465 wydany i zainstalowany 02.11.2026. Ograniczenia oraz dodatkowy odbiór w głównym AMC opisano poniżej.**

## Nadrzędna korekta użytkownika 01.11.2026

**Shift+C** ma otwierać osobny wybór Google Cast bieżącej sesji. **Shift+A** pozostaje wyborem toru audio. Ta decyzja zastępuje wcześniejszy wariant z Tab wewnątrz Shift+A. Użytkownik udostępnił WiiM Sound i wznowił zadanie. Mirroring i proponowane Shift+M są osobnym pomysłem, bez działającego polecenia i bez wiążącej rezerwacji skrótu.

## Aktualny materiał do dokumentacji użytkownika

W Plikach lokalnych, Radiu i Podcastach `Shift+C` otwiera listę **Urządzenia Google Cast**. To samo polecenie jest w menu Dźwięk, palecie i pomocy klawiatury. Lista zawiera **Ten komputer** oraz nazwy odbiorników. Fokus i zaznaczenie wskazują bieżący wybór. Strzałki zmieniają zaznaczenie, Enter zatwierdza, Escape anuluje. Po zamknięciu fokus wraca do miejsca wywołania. **Odśwież urządzenia** ponawia wykrywanie; zwykłe ponowne otwarcie korzysta z migawki procesu.

Wybranie urządzenia przekazuje bieżący materiał do odbiornika. Gdy nic nie gra, wybiera cel dla następnego pojedynczego pliku, stacji lub odcinka. Wybór dotyczy jednej sesji i bieżącego uruchomienia AMC. Komunikat **Wyjście sesji: [nazwa]** następuje po potwierdzeniu połączenia i, jeśli jest materiał, startu. Błąd nie zapisuje niepotwierdzonego wyboru; AMC wznawia poprzednie lokalne odtwarzanie, jeśli musiał je wstrzymać.

Pauza, wznowienie, głośność i wyciszenie sterują wyjściem aktywnej sesji. Pliki i zwykłe odcinki można przewijać. **Ten komputer** pod Shift+C wraca do lokalnego hosta z zachowaniem znanej pozycji i pauzy. Radio jest przesyłane na żywo. Shift+A oraz Ctrl+[ / Ctrl+] nadal wybierają lokalny tor audio; nie przełączają odbiornika Cast. Ten opis jest roboczy i nie oznacza zainstalowania funkcji w użytkowym AMC.

## Aktualne uzasadnienie i decyzje

- Osobny adapter sesji odróżnia Cast od WASAPI. Polecenia przechodzą przez wspólnego klienta hosta; inne sesje nadal używają własnych wyjść.
- Lokalny plik jest udostępniany przez ograniczony serwer HTTP: jeden plik, losowy adres, tylko adres IP wybranego odbiornika, zakresy bajtów, brak listy katalogów. Zakończenie udostępniania domyka również aktywne połączenia i plik.
- Ograniczone czasowo wykrywanie mDNS w sieci IPv4 znalazło WiiM Sound i WiiM Pro Plus. Model używa stabilnej tożsamości, natomiast NVDA otrzymuje wyłącznie zamierzone nazwy.
- Wbudowany adapter protokołu obsługuje ramki, heartbeat, potwierdzenie startu i zamknięcie wątku. Nie dodaje zależności z PyPI.
- Jeden odbiornik nie jest przypisywany równocześnie dwóm sesjom AMC. Przełączanie wspólnej ścieżki Plików i Podcastów nie wprowadza niezamierzonego mirroringu.
- Jedna funkcja możliwości blokuje nieobsługiwane komendy w wykonaniu, menu, pomocy i palecie. Potwierdzony start nowego materiału Cast zapisuje historię przez hosta we właściwej sesji; samo wskazanie odbiornika nie zapisuje odtworzenia.

## Aktualne ograniczenia pierwszego etapu

- Obsługiwane są pojedyncze media dostępne odbiornikowi. Spotify, TIDAL i źródłowe URL YouTube wymagają własnej integracji usług. Nie ma przesyłania całego dźwięku Windows.
- Kodeki zależą od odbiornika. Dopuszczone lokalnie: MP3, AAC/M4A, WAV, FLAC, Ogg/Opus, MP4. URL radia bez rozszerzenia jest przekazywany jako audio/mpeg; playlisty i nietypowe serwery mogą zostać odrzucone przez urządzenie.
- Radio Cast nie ma Time Shift AMC. Tempo, zakładki, rozdziały, edycja fragmentów i ciągłe kolejki wymagają komputera. Odpowiednie komendy są blokowane; odtwarzanie pozycji kolejki daje komunikat o powrocie przez Shift+C.
- Znana pozycja odbiornika służy bieżącemu powrotowi do komputera. Trwałe zapisanie i automatyczne odtworzenie pozycji Cast po zamknięciu AMC nie jest wdrożone. Stary checkpoint lokalnego hosta nie nadpisuje postępu Cast.
- Po utracie sieci nie ma automatycznego powrotu do komputera. AMC zgłasza utratę sterowania; bez łączności nie może potwierdzić zatrzymania głośnika.
- Nie ma wyboru wielu odbiorników, mirroringu ani synchronizacji komputera z głośnikiem. Próby sprzętowe objęły WiiM Sound, nie wszystkie urządzenia, grupy i formaty.

## Testy i wynik 01.11.2026

- Wykryto WiiM Sound-CED0 i WiiM Pro Plus-8B96. WiiM Sound odpowiada na TLS i odczyt Cast.
- Trzysekundowy cichy WAV został pobrany z lokalnego serwera. Użytkownik potwierdził słyszalny sygnał. Przywrócono wcześniejszą głośność.
- Na prawdziwym odbiorniku sprawdzono start, pauzę, wznowienie, skok do 8 sekund, głośność i zamknięcie połączenia. Wynik pozytywny.
- Pierwsza rozszerzona bramka celowana bez okien: 126 zaliczonych, 0 niepowodzeń. Obejmuje protokół, HTTP, brak dostępu do katalogu, izolację sesji, rollback, klienta hosta, Shift+A, Shift+C oraz parytet menu/pomocy/palety. Po późniejszych zmianach wymaga końcowego ponowienia.
- `dotnet build src\AccessibleMediaController.LiteHost\AccessibleMediaController.LiteHost.csproj -c Release --no-restore -o .test-output\cast-host -v minimal`: zaliczone, 0 błędów i 0 ostrzeżeń. Po dodaniu historii wymaga ponownej kompilacji.
- Izolowany rzeczywisty host bez profilu: lokalny plik → WiiM Sound → lokalny host. Zaliczone: wyłączenie lokalnego odtwarzania, pauza, skok oraz powrót z zachowaniem pauzy i pozycji. Nie jest to test głównego okna wxPython.
- Poprawiono błąd zamykania wątku starego prototypu i obsługę aktualnego statusu odbiornika po przejściowym timeout. Końcowa próba po ostatnich zmianach pozostaje do zapisania.
- Po zgodzie użytkownika 01.11.2026 otwarto osobne okno próby rzeczywistego dialogu z NVDA: `AMC — próba Shift+C z NVDA`. Użytkownik potwierdził działanie Shift+C, utrzymanie fokusu, prawidłowy odczyt listy, Escape i Alt+F4. Odbiór listy NVDA zaliczony. Próba nie uruchamiała odtwarzania ani nie restartowała AMC.

## Aktualne pliki, wersja i commit

Nowe moduły: `cast_protocol.py`, `cast_media.py`, `cast_session.py`, `cast_dialog.py`; testy `test_cast_session.py`. Integracje: `host_client.py`, `gui.py`, `shortcuts.py`, `menu_model.py`, `i18n.py`, `LiteEngineHandlers.cs`. Zastane zmiany robocze innych zadań są zachowane. Dawny `CAST-001-prototyp-cast-protocol.py` jest archiwalnym materiałem z 29.10, poza pakietem aplikacji.

Baza przy wznowieniu: `833568c99b9f18cb02ab06802a352246a720f944`, gałąź `codex/konfigurowalne-skroty-20261026`. Zmiany Cast są robocze, bez osobnego commitu. Jest testowy build hosta; nie ma nowej paczki, instalatora, instalacji ani publikacji Cast. Użytkowe AMC Test działa z `C:\Users\micha\AppData\Local\Programs\AMC Test` i nie było restartowane.

## Źródła sprawdzone 01.11.2026

[WiiM Sound](https://www.wiimhome.com/products/wiim-sound)

https://www.wiimhome.com/products/wiim-sound

[Włączanie Google Cast w WiiM](https://faq.wiimhome.com/en/support/solutions/articles/72000600942-how-to-enable-or-disable-google-cast-on-wiim-device)

https://faq.wiimhome.com/en/support/solutions/articles/72000600942-how-to-enable-or-disable-google-cast-on-wiim-device

[Domyślny odbiornik multimediów](https://developers.google.com/cast/docs/web_receiver)

https://developers.google.com/cast/docs/web_receiver

[Formaty mediów Google Cast](https://developers.google.com/cast/docs/media)

https://developers.google.com/cast/docs/media

[Oficjalne platformy SDK](https://developers.google.com/cast/docs/reference/)

https://developers.google.com/cast/docs/reference/

Dokumentacja była dostępna, co zastępuje przeszkodę sieciową z 29.10. Oficjalne SDK nadawcy obejmują Android, iOS i Web. Natywny adapter AMC jest własną implementacją protokołu, sprawdzoną w opisanym zakresie na WiiM Sound.

## Końcowy punkt wznowienia — 01.11.2026

- Użytkownik zatwierdził publikację GitHub i instalację wspólnej aktualizacji po 02:00 najbliższej nocy, także z najnowszym Spotify i poprawą Shift+A. Plan i bramki: [REL-20261102](REL-20261102-nocna-aktualizacja-test.md). Nie pytać ponownie o zgodę na te czynności; wykonać po terminie i po sprawdzeniu bezpieczeństwa.
- Końcowa celowana bramka: **134 zaliczone testy, 0 niepowodzeń**. Ostatnie doprecyzowanie jawnej tożsamości sesji w zdarzeniach sprawdzono ponownie wyłącznie testami Cast: **20/20 zaliczonych**.
- Test z prawdziwym hostem i nowym, odizolowanym profilem potwierdził zapis historii do `local` oraz `radio`, izolację sesji i odrzucenie niedozwolonej sesji. Wcześniejszy błąd sprzątania testu wynikał z niedomkniętego połączenia SQLite w samym teście; po poprawce test jest zaliczony.
- Kompilacja końcowego testowego hosta z operacją historii: **0 błędów i 0 ostrzeżeń**. Artefakt: `.test-output\cast-host\amc_lite_host.exe`.
- Końcowy rzeczywisty przepływ: host lokalny → WiiM Sound → host lokalny, kolejny plik przez ten sam Cast, pauza, przewijanie, powrót w pauzie bez krótkiego dźwięku, wznowienie z właściwej pozycji. Wynik pozytywny. Próba sprzętowa działała bez profilu; sam zapis historii został niezależnie potwierdzony opisanym wyżej testem bazy.
- Import całej integracji GUI oraz rzeczywiste kontrolki wxPython sprawdzono bez pokazania okna. Potwierdzono napisy `Ten komputer`, `Salon`, `Gabinet`, nazwę listy `Urządzenia Google Cast` i odtworzenie zaznaczenia po stabilnym identyfikatorze. To **nie jest odbiór NVDA**.
- Końcowa kontrola wykazała **0 pozostawionych procesów testowego hosta**. Użytkowy AMC i NVDA nie były restartowane. Brak użytkowego pakietu, instalacji i publikacji Cast; brak nowego commitu. Zastane zmiany innych zadań są zachowane.
- Uaktualniono README_PL i README_EN. [Robocza instrukcja i lista zmian](CAST-001-instrukcja-i-lista-zmian-szkic.md) pozostają materiałem do korekty, nie opublikowaną instrukcją gotowej wersji.
- **Odbiór NVDA użytkownika — 01.11.2026: zaliczony.** Użytkownik potwierdził Shift+C, utrzymanie fokusu, poprawny odczyt wszystkich elementów oraz Escape i Alt+F4. Okno uruchomiono skryptem `C:\Users\micha\Documents\Codex\2026-10-29\a-3\work\cast_dialog_probe.py`, używając `runtime\pythonw.exe` z instalacji AMC Test (PID 32936). Wynik zastępuje wcześniejsze oczekiwanie na odbiór. Nie jest to próba zainstalowanej integracji całego AMC.
- **Uwaga użytkownika — 01.11.2026:** osobne okno trzeba było odszukać. Przyszłe zatwierdzone próby mają pojawiać się na pierwszym planie od razu. Zapisano tę zasadę w AGENTS.md i dodano jednokrotne podniesienie okna w skrypcie próby. Poprawki sposobu otwierania nie uruchamiano ponownie; nie jest oznaczona jako sprawdzona przez użytkownika.
- **Następny etap:** przygotowanie spójnej paczki i uzgodniona instalacja do odbioru w głównym AMC. Pozostałe zmiany robocze repozytorium muszą być uwzględnione przy ustalaniu zakresu paczki. Brak instalacji, publikacji i nowego commitu Cast.
- Odczyt limitu po odbiorze: 75% wykorzystane, 25% pozostało, odnowienie 04.11.2026 o 07:45 Europe/Warsaw. Nie wykorzystano dostępnego resetu. Próg zatrzymania użytkownika 15% nie został osiągnięty.

Polecenia celowane: prywatny runtime `C:\Users\micha\AppData\Local\Programs\AMC Test\runtime\python.exe -B`; `-m unittest discover -s wxlite\tests -p test_cast_session.py` z jawnym `AMC_CAST_TEST_HOST` wskazującym zbudowany host. Lokalna zbiorcza bramka i próby sprzętowe są zachowane w `C:\Users\micha\Documents\Codex\2026-10-29\a-3\work\run_cast_checks.py` oraz `cast_host_probe.py`; widoczna próba to `cast_dialog_probe.py` w tym samym katalogu. Są to materiały robocze bieżącej rozmowy.

---

## Archiwalny zapis 29.10.2026 — zastąpiony powyższym stanem

Poniższe sekcje dokumentują wcześniejszą decyzję i wynik tamtego dnia. Wariant z Tab, zawieszenie i brak integracji nie są już aktualnymi ustaleniami.

## Zakres uzgodniony 29.10.2026
Użytkownik potwierdził wdrożenie dostępnego zakresu pomimo ograniczeń Google Cast. W `Shift+A` pierwsza kontrolka zachowuje lokalne wyjścia audio, a Tab prowadzi do listy urządzeń Google Cast. Wdrożenie ma obejmować tylko media, które urządzenie może pobrać i odtworzyć legalnie oraz technicznie. Żadnego pozornego wyboru urządzenia bez przekazania odtwarzania.

Korekta użytkownika z 29.10.2026: obie listy mają być równoległe i osobne. Tab przechodzi z listy zwykłych wyjść na listę Cast; urządzeń nie wolno mieszać w jednej liście. Sposób aktywacji urządzenia (przełączenie lub oznaczenie Spacją) pozostaje osobną decyzją projektową. Użytkownik poprosił o zawieszenie zadania, jeżeli na tym etapie niezbędny jest fizyczny głośnik; wznowić po jego udostępnieniu. Ta korekta zastępuje wcześniejsze założenie, że można przeprowadzić odbiór bez urządzenia.

## Uzasadnienie i decyzje
- Google Cast jest zdalnym odtwarzaczem sieciowym, a nie punktem końcowym WASAPI. `audio.selectOutput` hosta obsługuje tylko lokalne wyjścia i nie może być użyte dla Cast.
- Osobny adapter musi wykryć urządzenia, utrzymywać połączenie, przekazać media, sterować transportem i zwracać prawdziwy stan. Dla lokalnego pliku potrzebny jest ograniczony serwer HTTP dostępny urządzeniu w tej samej sieci, z obsługą zakresów bajtów i z automatycznym zakończeniem udostępniania.
- Nie przekazywać tokenów, plików tylko z konta ani treści DRM do odbiornika domyślnego. Nie obiecywać Spotify, TIDAL, YouTube, całego dźwięku Windows ani Time Shift Radia bez osobnego potwierdzenia możliwości.
- Wybrany Cast i lokalne wyjście są różnymi modelami. Trwała pamięć wyboru wymaga stabilnej tożsamości urządzenia, a nie samej nazwy. Po utracie połączenia nie wolno ogłosić sukcesu; powrót do lokalnego odtwarzania wymaga jawnego, spójnego stanu.
- Przed wyborem źródła sprawdzić format i osiągalność adresu. Dla URL nie wystarczy, że odtwarza go komputer: odbiornik musi mieć do niego dostęp.
- Oddzielne kontrolki w dialogu: początkowy fokus na liście wyjść lokalnych; Tab przechodzi na listę Google Cast. Pusta lista Cast ma mieć jednoznaczny komunikat, a po zamknięciu fokus wraca do punktu wywołania. Nie dodawać Cast do `audio.outputs`, lokalnego zapisu identyfikatorów WASAPI ani cyklu `Ctrl+[` / `Ctrl+]` bez osobnej decyzji.
- Rzeczywista próba na odbiorniku jest bramką ukończenia: wykrywanie, początek odtwarzania, pauza, wznowienie, zmiana wyjścia, utrata sieci, ponowne otwarcie i mowa NVDA. Bez tej próby nie instalować ani nie reklamować funkcji jako działającej.

## Materiał do dokumentacji użytkownika
Projektowane zachowanie: `Shift+A` otwiera wybór wyjścia bieżącej sesji. Pierwsza lista zawiera urządzenia audio komputera. Tab przenosi do osobnej listy urządzeń Google Cast w sieci. Wybór urządzenia jest potwierdzany dopiero po faktycznym rozpoczęciu odtwarzania; lista mówi, gdy nie znaleziono urządzeń lub wybrane medium nie może być przesłane. Google Cast wymaga tej samej sieci lokalnej i obsługi formatu przez odbiornik. Treści chronione usług i cały dźwięk systemu nie przechodzą automatycznie na Cast. Ten opis nie dotyczy jeszcze zainstalowanej funkcji.

## Testy i wynik
Sprawdzono składnię niezintegrowanego prototypu poleceniem `python -B -c "import ast,pathlib; ast.parse(pathlib.Path('docs/zadania/CAST-001-prototyp-cast-protocol.py').read_text(encoding='utf-8'))"`; wynik: poprawna. To nie dowodzi działania protokołu. Nie uruchamiano testów GUI, próby Cast, builda ani instalacji. Do wykonania: testy adaptera z atrapą urządzenia, kontrakt UI i klawiatury, test połączenia, formatów, zakresów HTTP, błędów i odzyskiwania stanu oraz próba z prawdziwym urządzeniem i NVDA.

## Znane ograniczenia
Bieżące środowisko nie może pobrać dokumentacji Google Cast (usługa wyszukiwania zwraca 503, a bezpośrednie żądanie sieciowe nie rozwiązuje hosta). Nie zastępować weryfikacji aktualnych zasad domysłami. Użytkownik potwierdził brak urządzenia do próby sprzętowej i polecił zawiesić zadanie. Niezintegrowany prototyp `CAST-001-prototyp-cast-protocol.py` zachowano wyłącznie jako materiał roboczy, poza pakietem aplikacji; wymaga przeglądu protokołu, poprawek i testów. Nie jest funkcją AMC.

## Wersja i commit
Źródła: gałąź `codex/konfigurowalne-skroty-20261026`; zmieniono wyłącznie tę kartę zadania i zachowano niezintegrowany prototyp. Brak nowego commitu, builda, instalacji i wydania.

## Aktualizacja wydania — 02.11.2026

Kod tego przyrostu jest wydany i zainstalowany we wspólnej aktualizacji **AMC Test — 02.11.2026, 02:18**, technicznie `0.1.0-alpha.465`, commit `f2a91a208afd2495c5ebe9c78466771a89a4c0e0`. Ten wpis zastępuje wcześniejszy status „bez paczki/instalacji/publikacji”; zachowuje opisane ograniczenia oraz brakujące ręczne odbiory. Główna gałąź robocza pozostała nietknięta; wydanie scala przyrost z nowszą bazą instalacji w worktree wydania. Dokładne wyniki: [REL-20261102](REL-20261102-nocna-aktualizacja-test.md).

https://github.com/michalkasperczak/AMC/releases/tag/test-2026.11.02.0218
