# CAST-005 — badanie ciszy AirPlay i niepewnego startu Cast

Data zgłoszenia: 25.11.2026. Status: badanie w toku, poprawka źródłowa Cast; AirPlay wymaga próby odsłuchowej na wskazanym odbiorniku.

## Zgłoszenie i zakres

Michał zgłosił, że po wybraniu AirPlay następuje cisza, a AMC wraca do głośników komputera. Google Cast bywa zależny od protokołu i konkretnego odbiornika. Prosił o odświeżenie wcześniejszego zadania i głębsze badanie. To jest nowy wynik praktycznego użycia, który zastępuje wcześniejszy stan „odsłuch głównego AirPlay niepotwierdzony” z CAST-004; nie jest jeszcze dowodem jednej przyczyny.

Badanie odbywa się w bieżącym czystym na starcie worktree `work/HEOS-001-next-test`, gałąź `codex/heos-next-test-20261123`, baza `f04c28c7`. Dotyczy wyłącznie wxPython. `Shift+C` wybiera główne wyjście: komputer, Cast lub AirPlay; `Ctrl+Shift+[` i `Ctrl+Shift+]` przełączają je kolejno. `Shift+M` pozostaje dodatkowym odsłuchem. Nie zmieniono skrótów, menu, palety ani opisów poleceń.

## Wyniki analizy i prób bez dźwięku

- Wcześniej `CAST-001` potwierdził na WiiM Sound rzeczywisty przepływ plik → Cast → komputer, pauzę, przewijanie i kolejny plik. Nie dowodzi to działania wszystkich źródeł ani odbiorników. `CAST-002` zapisał historyczne ciszę i timeout RTSP AirPlay na WiiM. `CAST-004` dodał główny AirPlay, lecz nie zawierał rzeczywistego odsłuchu.
- `PrimaryAirPlayRouter.select` wycisza lokalny silnik przed `MirrorAirPlayRouter.enable`. Gdy próba strumienia zawiedzie, kod przywraca lokalne wyjście. Objaw zgłoszony przez Michała jest zgodny z tą ścieżką, ale dotychczasowy komunikat ukrywa przyczynę błędu. FFmpeg wysyła MP3 przez potok do pyatv RAOP; stderr FFmpeg jest odrzucany. `metadata.playing()` pyatv odzwierciedla stan własnego nadawcy, nie niezależny pomiar słyszalnego dźwięku z głośnika.
- Odczytowe wykrywanie w ograniczonej sieci narzędzia pokazało pustą listę Cast, a skan AirPlay nie zakończył się w oczekiwanym czasie. Po uruchomieniu tej samej kontroli z normalnym dostępem do sieci wykryto Cast: WiiM Pro Plus-8B96 i WiiM Sound-CED0; AirPlay: Denon Home 200, WiiM Pro Plus-8B96 i Biuro. To był skutek izolacji próby, nie wniosek o sieci użytkownika.
- Odczytowe połączenie pyatv RAOP bez odtwarzania powiodło się z każdym z trzech odbiorników AirPlay. Nie sprawdza to negocjacji RTSP strumienia ani odsłuchu. Nie uruchamiano audio, nie zatrzymywano AMC, NVDA ani nagrań.
- Zainstalowany AMC pobiera FFmpeg przez `shutil.which("ffmpeg")` z ogólnego PATH; obecnie znajduje zewnętrzną instalację należącą do BlindRSS, a nie własny plik AMC. Wykryty FFmpeg ma enkoder `libmp3lame`. Próba lokalnego wygenerowania jednosekundowego MP3 zakończyła się kodem 0, 25 121 bajtami wyjścia i pustym stderr. To wyklucza brak ogólnego enkodera w tej instalacji, lecz nie błędy dekodowania konkretnego źródła. Zależność od innej aplikacji jest osobnym ryzykiem pakowania do poprawy.
- W `CastConnection.load` odpowiedź odbiornika `MEDIA_STATUS` z `playerState=IDLE` i powodem innym niż `ERROR` była uznawana za powodzenie. Router mógł wtedy zatrzymać lokalny silnik mimo braku odtwarzania na Cast. Jest to potwierdzona luka w warunku stanu, niezależna od tego, czy wyjaśnia konkretną próbę Michała.

## Próba Sonos Biuro — 25.11.2026

Po wskazaniu Sonosa „Biuro” Michał wyraźnie zgodził się na krótki cichy sygnał diagnostyczny, świadom możliwego przerwania obecnego odtwarzania. Plik tymczasowy z trzema cichymi sygnałami usunięto po próbach; nie dotykano nagrań AMC.

- Z Pythona dołączonego do zainstalowanego AMC połączenie RAOP przeszło, lecz bezpośredni WAV zakończył się `TimeoutError: no response to SETUP (RTSP/1.0)` wewnątrz pyatv AirPlay 2. To nastąpiło przed uruchomieniem FFmpeg. Skrypt diagnostyczny dodatkowo błędnie użył znaku spoza kodowania konsoli, przez co jego próba wypisania błędu zakończyła się `UnicodeEncodeError`; traceback pyatv jednoznacznie pokazuje poprzedzający timeout. Skrypt poprawiono przed następną próbą.
- Reguły lokalnej Zapory Windows dla dokładnego `D:\Projekty Codex\AMC Python\runtime\python.exe`: osobne aktywne reguły `Block` dla TCP i UDP, kierunek przychodzący, wszystkie porty i adresy, profile Private i Public. Bieżące Wi-Fi „Oppo 5G” ma profil Public i adres w podsieci 192.168.0.0/24. Systemowy Python ma aktywne reguły `Allow` dla ruchu przychodzącego.
- Ten sam skrypt z systemowym Pythonem 3.14 i tym samym zainstalowanym pakietem pyatv ukończył bezpośredni WAV do Sonosa w 4,2 s oraz tor FFmpeg → MP3 → RAOP taki jak w AMC w 5,5 s. Proces nie pozostawił uruchomionej transmisji ani pliku próbnego. To mocno wskazuje na reguły zapory przypisane do ścieżki wykonywalnej AMC, choć potwierdzenie słyszalności obu zestawów sygnałów od Michała jest jeszcze oczekiwane.
- Przygotowano najmniejszy zakres ewentualnej korekty systemowej: wyłączyć dwie lokalne reguły blokujące tylko ten plik wykonywalny i dodać zezwolenie na ruch przychodzący wyłącznie z bieżącej podsieci Wi-Fi dla tego samego programu. Bieżący profil sieci jest Public, więc zezwolenie ograniczone do Private nie naprawiłoby próby. Zmiana ustawień bezpieczeństwa Windows czeka na jawną odpowiedź Michała. Nie wykonano jej podczas tych prób.

## Zmiana źródłowa Cast

Odpowiedź `IDLE` przy starcie Cast jest teraz błędem. Dla `ERROR` zostaje dotychczasowy komunikat o nieodtwarzalnym materiale, a dla pozostałych przyczyn AMC podaje istniejący przyjazny komunikat „Urządzenie nie rozpoczęło odtwarzania materiału”. Dzięki temu router zachowuje odtwarzanie lokalne przy nieudanym wyborze. Model, identyfikatory, adresy i powody protokołu nie trafiają do NVDA. Początkowy wybór, fokus i pozostałe etykiety nie zostały zmienione.

Testy: `python -m pytest -q -p no:cacheprovider -o pythonpath=wxlite wxlite/tests/test_cast_protocol_load.py wxlite/tests/test_cast_session.py` — **23 zaliczone, 1 pominięty** (istniejąca próba wymagająca zewnętrznego hosta). Nowe testy obejmują `IDLE` z FINISHED, CANCELLED i bez powodu oraz poprawny PLAYING. Pierwsza próba z dołączonym runtime zakończyła się brakiem modułu pytest; właściwy interpreter systemowy wykonał testy. Nie wykonano kompilacji, instalacji, publikacji ani sprzętowego testu tej poprawki. Odbiór NVDA pozostaje otwarty.

## Następny krok i ograniczenia

Potrzebna jest wskazana przez Michała kombinacja odbiornika i materiału oraz krótka świadoma próba dźwiękowa, gdy nie przerwie ważnego odtwarzania. Należy osobno sprawdzić RAOP bezpośrednio i przez AMC, zachować kategorię rzeczywistego błędu RTSP lub FFmpeg bez prywatnych URL i dopiero wtedy korygować transport. Dla Cast sprawdzić konkretny zawodny format/strumień i odbiornik; różnice formatów i dostępu do URL są prawdopodobną klasą problemu, lecz nie zdiagnozowanym wynikiem tej próby.

Wersja źródłowa: zmiana po wydaniu alpha.531, bez numeru nowej paczki. Commit kodu: `e8f99af2`. Żaden wynik testu kodu nie oznacza działającego wydania lub instalacji.
