# AMC Test i Public — wspólna aktualizacja alpha.488

Status: **Wydane i Public zainstalowany**, 9 listopada 2026. AMC Test opublikowany, lecz nieinstalowany: użytkownik kończy nagrywanie w starej wersji.

## Zakres użytkowy

- Lokalne MP3 z głośnymi szczytami: automatyczna ścieżka FFmpeg float ogranicza szczyty przed wyjściem audio, jeśli składnik FFmpeg jest dostępny. Utrata urządzenia wyjściowego nie uruchamia już odzyskiwania dekodera MP3.
- Zwykła kolejka plików (`Ctrl+Q`): osobny zapis dla AMC Test i Public, odtworzenie po restarcie, zachowanie pustej kolejki i brakującego pliku. Gdy miejsce zapisu jest niedostępne, AMC działa z kolejką tymczasową i informuje o tym jednokrotnie.
- Historia TIDAL i Spotify (`Ctrl+H`): prawa strzałka pokazuje nazwane albumy, wykonawców i podcasty. `Ctrl+C` kopiuje nazwę, `Ctrl+Shift+C` dokładny link, jeśli jest dostępny. Podobne przejście jest dostępne dla historii odcinków Podcastów i YouTube.
- Rozpoznane utwory (`Ctrl+Alt+S`): prawa strzałka podaje nazwany utwór najpierw w usłudze domyślnej, następnie w drugiej oraz pozycję kopiowania Apple Music. Album pojawia się w krótkiej liście tylko przy znanej nazwie.

## Uzasadnienie i decyzje

Test i Public powstają z tego samego commitu i numeru technicznego. Instalacja Test jest odłożona ze względu na aktywne nagrywanie. Spotify Soloist i żądanie Lossless pozostają z wydania alpha.487; ta aktualizacja nie jest potwierdzeniem rzeczywistej jakości strumienia.

## Testy i wynik

- Na wspólnym drzewie źródeł: 153 celowane testy Python dla kolejki, historii, rozpoznawania, Spotify, menu i podcastów zaliczone; 123 dodatkowe testy nawigacji, profili i instalatora zaliczone w izolowanym środowisku wxPython.
- Celowane .NET `QueuePersistenceTests` oraz `--mp3-peaks-only` zaliczone. Dla kolejki sprawdzono także cichy start rzeczywistego hosta przy niedostępnym katalogu.
- Obie paczki zbudowano z czystego commitu `23a1490ef423325f01cf726cc09dbb1c4c1d5496`; pełna kontrola integralności i cichy start interfejsu oraz hosta powiodły się osobno dla Test i Public. Oba instalatory powstały z tych paczek i przeszły kontrolę sekretów oraz sum SHA-256.
- Wydania GitHub: [Public](https://github.com/michalkasperczak/AMC/releases/tag/public-2026.11.09.2214) (kanał stable) i [Test](https://github.com/michalkasperczak/AMC/releases/tag/test-2026.11.09.2217) (prerelease). Każde wydanie ma instalator, plik integralności i manifest aktualizacji; niezależny odczyt GitHub potwierdził stan `uploaded`, rozmiary i ten sam commit źródeł.
- Public zainstalowano cicho z `publish/public-alpha488-20261109/AMC-Public-2026-11-09-2214.exe`, kod wyjścia 0. Zainstalowany `AMC.exe` ma ProductVersion `0.1.0-alpha.488`, manifest profilu `public`, `sourceDirty=false` i zgodną rewizję. Pełna kontrola instalacji oraz cichy start interfejsu i hosta: kod 0. Log: `publish/public-alpha488-20261109/install-public-alpha488.log`.
- Przed instalacją cztery pliki ustawień Public zaszyfrowano DPAPI w `D:\Projekty Codex\AMC - kopie prywatne\PUBLIC-20261109-przed-alpha488\profil-przed-alpha488.dpapi`; odszyfrowanie, liczba plików i SHA-256 przeszły. Po instalacji pliki są bajtowo identyczne.
- AMC Test przez cały czas miał tylko swoje trzy procesy (`AMC.exe`, `python.exe`, `amc_lite_host.exe`); instalatora Test nie uruchomiono. Odbiór nawigacji NVDA i odsłuch na rzeczywistym pliku nadal wymagają użytkownika.

## Znane ograniczenia

FFmpeg pozostaje składnikiem opcjonalnym: bez niego przester problematycznego MP3 może pozostać. Nie sprawdzono odsłuchu na pliku Huberta, fizycznego odłączenia słuchawek ani odczytu nowej listy w NVDA. Test nadal nagrywa; nie zamykać ani nie instalować.

## Wersja i commit

Wersja obu profili: `0.1.0-alpha.488`. Commit źródeł: `23a1490ef423325f01cf726cc09dbb1c4c1d5496`. Public: `AMC-Public-2026-11-09-2214.exe`, 133898961 B, SHA-256 `5a6b9d86303ebedad51614beda34d47fbc0fef28b95cf5486a07777dfbec19f1`. Test: `AMC-Test-2026-11-09-2217.exe`, 136481714 B, SHA-256 `4f851bfa7a657604ae69d6fb706d501ed3f082bb7f2a84260516203781f897dc`. Wydania wskazują ten commit, a późniejszy commit dokumentacyjny nie zmienia binariów.
