# AMC alpha.519 — Cast i AirPlay, Test oraz Public

## Decyzja użytkownika — 23.11.2026

Po przygotowaniu CAST-002 Michał zlecił aktualizację, jeśli zakres nie znajduje się jeszcze w aktualizacjach, ostatecznie w obu profilach Test i Public. Zastępuje to wcześniejsze odłożenie samego pakowania do nieokreślonej kolejnej wersji. Nie oznacza zaliczenia brakujących prób sprzętowych ani NVDA. Użytkownik dopuszcza dalsze poprawki obu profili.

Najnowsze zdalne wydania przed rozpoczęciem: Test `test-2026.11.23.2136`, Public `public-2026.11.23.2138`, kod `bb7c5e0a`. Nie zawierają `network_audio.py`, `cast_tidal.py` ani rozszerzenia CAST-002.

## Zakres i źródła

Użyto istniejącego, czystego worktree ostatniego wspólnego wydania: `C:\Users\micha\.codex\worktrees\amc-apple-music-login-release\AMC - integracja wxPython`, gałąź `codex/amc-unified-release-20261123`, baza dokumentacyjna `3d10673d`, kod wydania alpha.518 `bb7c5e0a`. Kanoniczne robocze źródła na D pozostają na dotychczasowej gałęzi z zachowaniem innych prac.

Przeniesiono wyłącznie przejrzane fragmenty CAST-002 z karty na D: routery Cast i dodatkowych wyjść, prywatny most TIDAL, dostępność i nazwy poleceń, bezpieczne wyniki listy, komunikaty, pozycję powrotu w kontrakcie hosta oraz testy. W plikach wspólnych wybrano konkretne fragmenty, bez hurtowego kopiowania GUI lub klienta hosta. Nie dołączono roboczego monitorowania nagrań, zmian wznawiania podcastów ani nowego HEOS. Dotychczasowe poprawki alpha.518 pozostają, w tym Apple Music tylko w Test. Public pozostaje bez Apple Music; oba profile bez aktywnej sesji HEOS.

Wersja techniczna kandydata: `0.1.0-alpha.519`. Oba profile mają powstać z tego samego commitu. Paczki, instalatory, manifesty i logi trafiają do `D:\Projekty Codex\AMC - integracja wxPython\publish`, bez podmiany działającej instalacji. Zgodnie z aktualnymi zasadami projektu użytkownik sam instaluje aktualizacje.

## Materiał do dokumentacji użytkownika

- Shift+C wybiera Google Cast dla dostępnego materiału w Plikach, Radiu, Podcastach i Radio Archiwiście oraz obsługiwanych natywnych strumieni TIDAL. Nie potrzeba TIDAL Connect. Cast TIDAL obsługuje nieszyfrowany BTS FLAC/AAC/MP3; DASH/MPD nie jest dostępny.
- Shift+M otwiera „AirPlay i dodatkowe wyjście audio”. Główne wyjście nadal gra; urządzenia mogą mieć opóźnienie. Lista wyjaśnia brak AirPlay, a aktywny dodatkowy odsłuch można wyłączyć po zakończeniu materiału. Shift+A nadal wybiera lokalny tor audio.
- Enter zatwierdza odbiornik, Escape anuluje; „Ten komputer” pod Shift+C wraca do komputera. Spóźnione wykrywanie nie otwiera listy w zmienionej sesji. Niepotwierdzony STOP ma ostrzeżenie.
- Nie dodano przechwytywania całego dźwięku Windows ani uniwersalnego Cast dla Spotify/Apple Music i odtwarzania wyłącznie na urządzeniach zewnętrznych.

## Testy i ograniczenia

Przed pakowaniem wykonano ponownie testy dokładnych źródeł wydania: **320 zaliczonych Python, 1 pominięty test wymagający jawnej ścieżki nowego hosta**. W tej liczbie są testy Cast/AirPlay, skrótów/menu/palety, klienta hosta, profili, układu paczki, instalatora i publikowania istniejących instalatorów. Wcześniejsze 240 testów na roboczym checkoutcie nie jest automatycznie wynikiem źródeł wydania.

Dodatkowo i18n: **15 zaliczonych, 2 niezaliczone**. Porównanie z nietkniętym HEAD alpha.518 odtworzyło te same dwa błędy. Braki są identyczne: 172 teksty statyczne, 64 szablony, 24 nadmiarowe dawne szablony; **zero nowych braków**. Nie ukryto niepowodzeń ani nie wyłączono testów. Dotyczy to istniejącej kompletności języka angielskiego, nie awarii nowego polskiego wyboru wyjścia. Użytkownik zlecił wydanie obu profili z możliwością dalszych poprawek, więc istniejące braki tłumaczeń są jawnie zachowanym ograniczeniem, nie pozornie zaliczoną bramką.

Kompilacja protokołu Debug: 0 błędów i ostrzeżeń; `--tidal-native --transport-routing`: oba zestawy zaliczone. Przed budową odczytano limit: 65% wykorzystane, 35% pozostało; bez płatnych kredytów i bez zużycia darmowego resetu. Odczyt zapisanej historii: 0 niedokończonych nagrań w ostatnich 12 godzinach, 55 włączonych harmonogramów. To nie jest odczyt żywego transportu: z tego powodu instalacja i zamykanie AMC nie są wykonywane, a odizolowaną budowę uruchamia się z obniżonym priorytetem.

Logi tej integracji: `C:\Users\micha\Documents\Codex\2026-10-29\a-3\work\cast002-release`. Build, manifesty, sekret-skan i zdalne zasoby zostaną uzupełnione po etapach.

Odsłuch fizyczny nowych wariantów i odbiór NVDA pozostają otwarte. Historyczne próby AirPlay na WiiM miały brak dźwięku/timeout RTSP; nie deklarować ich naprawy na podstawie testów atrap. AirPlay nie gwarantuje Lossless ani synchronizacji. Użytkownik zlecił udostępnienie obu profili z możliwością dalszych poprawek.

## Wersja, commit, publikacja i instalacja

Na etapie planu: brak nowego pakietu, publikacji i instalacji. Działający AMC na D pozostawiono bez zamykania. Następny krok: zakończyć celowane bramki, zapisać zakres, zbudować i zweryfikować oba instalatory, opublikować oraz potwierdzić manifesty aktualizacji.

## Potwierdzony wynik wydania

- Kod wydania: `dc000561d05ae7e0f2924c620e3d3f7599d943bd`; źródła opublikowane i zdalnie potwierdzone na `codex/amc-unified-release-20261123`.
- Celowane testy dokładnego źródła wydania: **321 zaliczonych Python** po uruchomieniu ostatniego testu historii z nowym hostem; dodatkowo **15 i18n zaliczonych, 2 niezaliczone jak w wydanej bazie**, zero nowych braków. Dwa zestawy C# zaliczone.
- Oba pełne pakiety mają `sourceDirty=false` i wspólny commit. Test: 3651 plików; Public: 2924 pliki. Kontrole integralności oraz kontrolowany start interfejsu i hosta przeszły w obu profilach. Bajty ośmiu kluczowych modułów zgadzają się ze źródłami. Składniki AirPlay są obecne, importy pyatv/TIDAL/Sonos i nowych modułów przeszły.
- Pierwsza próba budowy zatrzymała się na wskazaniu środowiska testowego zamiast kompletnego źródła zależności. Po korekcie użyto przypiętych składników ze zweryfikowanej paczki alpha.518, bez modyfikowania runtime użytkownika. Był to błąd konfiguracji budowy, nie test odtwarzania.
- Instalatory mają `secretScan=passed`; dodatkowy skan 1617 tekstowych plików Test i 1466 Public nie znalazł wzorców tokenów ani danych kluczy. Stały znacznik PEM w kodzie biblioteki cryptography odróżniono od rzeczywistego klucza.
- Public pozostaje bez Apple Music; oba profile bez aktywnego HEOS. Nie zmieniano lokalnych profili, baz, bibliotek ani nagrań.

### Test
- Wersja użytkowa: `23.11.2026, 22:31`, techniczna `0.1.0-alpha.519`.
- Instalator: `D:\Projekty Codex\AMC - integracja wxPython\publish\installers-alpha519-test-dc000561\AMC-Test-2026-11-23-2231.exe`.
- Rozmiar: 214459178 bajtów; SHA-256 `5cdff6a048a89a911879542180e1d4ff45185a59e33fff81d3b91dfaf551dbb8`.
- Wydanie: [test-2026.11.23.2231](https://github.com/michalkasperczak/AMC/releases/tag/test-2026.11.23.2231).
https://github.com/michalkasperczak/AMC/releases/tag/test-2026.11.23.2231
- Zdalnie sprawdzono tag wskazujący dokładny commit oraz nazwę, rozmiar, stan uploaded i identyczny SHA-256 wszystkich trzech zasobów: instalatora, manifestu aktualizacji i integralności.

### Public
- Wersja użytkowa: `23.11.2026, 22:33`, techniczna `0.1.0-alpha.519`.
- Instalator: `D:\Projekty Codex\AMC - integracja wxPython\publish\installers-alpha519-public-dc000561\AMC-Public-2026-11-23-2233.exe`.
- Rozmiar: 133974852 bajtów; SHA-256 `dfdd00500150cfb743403e4372d63f90e52b85b396fbb741fc9cc00a2d83e051`.
- Wydanie: [public-2026.11.23.2233](https://github.com/michalkasperczak/AMC/releases/tag/public-2026.11.23.2233).
https://github.com/michalkasperczak/AMC/releases/tag/public-2026.11.23.2233
- Zdalnie sprawdzono tag wskazujący dokładny commit oraz nazwę, rozmiar, stan uploaded i identyczny SHA-256 wszystkich trzech zasobów: instalatora, manifestu aktualizacji i integralności.

### Aktualizatory i instalacja
- Odczytowy test funkcji aktualizatora obu zainstalowanych profili wskazał `available=true`, właściwy nowy tag i ten sam commit źródeł. Nie pobierano instalatora przez aktualizator ani nie uruchamiano instalacji.
- Działającego AMC nie zamykano, nie uruchamiano nowej użytkowej instancji ani NVDA. Użytkownik sam instaluje oba profile. Przeprowadzono wyłącznie kontrolowane, ukryte testy pakietów.
- Odsłuch nowych wariantów Cast/AirPlay oraz rzeczywisty odbiór NVDA nadal są otwarte. Nie deklarować potwierdzonej naprawy historycznego AirPlay na WiiM.
- Manifesty i dowody zdalne: `publish/release-alpha519-dc000561`. Zachowano lokalne instalatory; nie usuwano jedynej używalnej paczki ani nieweryfikowanych prac. Następny krok: użytkownik instaluje i sprawdza odsłuch oraz listy NVDA.
- Limit po budowie i instalatorach: 66% wykorzystane, 34% pozostało; reset 27.11.2026 o 22:53 Europe/Warsaw. Nie użyto darmowego resetu.
