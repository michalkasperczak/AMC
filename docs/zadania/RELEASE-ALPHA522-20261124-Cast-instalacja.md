# AMC alpha.522 — wspólne źródła Cast i alpha.520, automatyczna instalacja Test

## Decyzja użytkownika — 24.11.2026

Michał zlecił dołączenie testowego Google Cast do najnowszego AMC Test oraz automatyczną aktualizację. Wyjaśnił, że lokalne linki w Codex otwierają wybór projektu zamiast instalatora. Następnie doprecyzował kolejność: **najpierw GitHub, zgodne źródła i paczki, potem instalacja**; gotowe funkcje obu profili mają być zgodne poza Apple Music.

To zastępuje dla tego zadania wcześniejsze pozostawienie instalacji użytkownikowi oraz wykluczenie Cast z alpha.520. Nie oznacza zaliczenia odsłuchu Cast/AirPlay nowych wariantów ani NVDA. Nie otwierać odsyłacza w przeglądarce Codex; uruchomić rzeczywisty, zweryfikowany instalator lokalny przez istniejący bezpieczny mechanizm aktualizacji.

## Zakres i uzasadnienie

- Baza: wydana alpha.520 `15f76ae476b3a7022a4f2f3807091d6c9d6b163b` oraz Cast/AirPlay `dc000561d05ae7e0f2924c620e3d3f7599d943bd` z dokumentacyjnym HEAD `7aa4deca`. Połączono je w istniejącym worktree wydania `C:\Users\micha\.codex\worktrees\amc-apple-music-login-release\AMC - integracja wxPython`, na gałęzi `codex/amc-unified-release-20261123`. Nie przełączano roboczej gałęzi na D ani nie dołączano jej innych niezweryfikowanych zmian.
- Zachowano wideo, wspólne wyszukiwanie, Spotify Soloist, lewą strzałkę TIDAL, jednorazowe pomijanie harmonogramu, odsłuch i zmianę nazwy trwającego nagrania oraz poprawne wznowienie podcastów/YouTube z alpha.520. YouTube zachowuje osobny origin obu profili.
- Dołączono rozszerzony Cast/AirPlay dla Radio Archiwisty i obsługiwanych natywnych strumieni TIDAL. Klient Python oraz host C# powstają razem; nie instalować samego frontendu.
- Odsłuch nagrania i jego zmiana nazwy stają się wspólną gotową funkcją Test/Public. Python używa do dostępności wspólnej możliwości `recording`, host udostępnia oba polecenia obu profilom. Historyczny znacznik `recording.monitor` pozostaje wyłącznie w manifeście Test jako pole zgodności starszych zamkniętych kontraktów aktualizatora; nie stanowi już różnicy dostępności funkcji. Zestawy pól manifestu obu profili pozostają zgodne z alpha.520, aby stary pomocnik potrafił zweryfikować pełną instalację.
- Apple Music pozostaje eksperymentalne tylko w Test; HEOS nie jest dołączany do żadnego z tych profili. Osobny techniczny kandydat HEOS alpha.521 nie jest podstawą tej aktualizacji. Wybrano unikalny numer `0.1.0-alpha.522`.

## Materiał do dokumentacji użytkownika

- **Shift+C**: urządzenia Google Cast i „Ten komputer”. Pliki, Radio, Podcasty, Radio Archiwista i odpowiednie źródła natywnego TIDAL. TIDAL nie wymaga TIDAL Connect; Cast obsługuje nieszyfrowane BTS FLAC/AAC/MP3, bez DASH/MPD. Nie obejmuje całego dźwięku Windows, Spotify ani Apple Music.
- **Shift+M**: „AirPlay i dodatkowe wyjście audio”. Główne wyjście nadal gra; możliwe opóźnienie. Lista podaje przyczynę braku AirPlay. Strzałki wybierają, Enter zatwierdza, Escape anuluje; aktywny dodatkowy odsłuch można wyłączyć po zakończeniu materiału.
- **Shift+A** nadal wybiera lokalny tor audio. Nazwy i dostępność poleceń pozostają wspólne w menu Dźwięk, palecie Ctrl+Shift+K i odpowiedniej pomocy.
- **Alt+R**, Enter: odsłuch trwającego nagrania; **F2**: nazwa docelowego pliku. Funkcje są teraz dostępne także w Public. Odsłuch nie zatrzymuje zapisu nagrania.
- Instalacja tego zadania ma odbyć się automatycznie do `D:\Projekty Codex\AMC Python`, z uruchomieniem dokładnie `AMC.exe` z tego katalogu i sprawdzeniem jednej instancji. Public zostaje opublikowany, ale jego lokalnej instalacji nie zlecono.

## Testy, build i znane ograniczenia

Celowane testy scalonych źródeł: **596 zaliczonych Python, 16 podtestów, 1 pominięty test historii oczekujący na nowy host**. Cztery zestawy C# (dwa protokołu, sztuczne nagranie, rzeczywisty dispatcher) zaliczone; kompilacje 0 błędów i ostrzeżeń. Błędną pierwszą nazwę modułu testów aktualizacji poprawiono na `test_application_updates.py`; właściwe 23 testy przeszły. Obejmują Cast/AirPlay, wspólność komend, profile, nagrania, wznowienia podcastów, wideo, wyszukiwanie, TIDAL, układ paczki, instalator i aktualizator. Użyto przypiętego wxPython 4.3.1 i izolowanych baz; bez głośników i widocznej regresji GUI.

Dwa historyczne testy globalnej kompletności angielskich tłumaczeń były niezaliczone już przed Cast; poprzednie porównanie potwierdziło zero nowych braków CAST-002. Nie deklarować ich zaliczenia. Rzeczywisty odsłuch nowych wariantów Cast/AirPlay i odbiór NVDA pozostają otwarte. Historyczny timeout/brak dźwięku AirPlay na WiiM nie jest potwierdzony jako naprawiony.

Logi integracji: `C:\Users\micha\Documents\Codex\2026-10-29\a-3\work\cast003-alpha522`. Artefakty użytkowe i dowody wydania: `D:\Projekty Codex\AMC - integracja wxPython\publish`.

## Bezpieczeństwo instalacji

Przed budową odczytano limit: 67% wykorzystane, 33% pozostało, reset 27.11.2026 22:53 Europe/Warsaw; bez zużycia darmowego resetu. Pierwszy odczyt rzeczywistych procesów nie wykazał działających składników AMC; należy powtórzyć go bezpośrednio przed instalacją i sprawdzić harmonogramy, nagrania oraz inne ważne operacje. Jeśli AMC ruszy i nagrywa albo nie można ustalić bezpieczeństwa, nie zamykać go i odłożyć instalację. Nie kończyć zbiorczo procesów Python/.NET i nie restartować NVDA.

## Wersja i stan etapów

Na etapie planu: scalone źródła w trakcie testów, bez nowej paczki, publikacji i instalacji. Następny krok: zaliczyć testy, zapisać dokładny zakres, zbudować i zweryfikować oba profile, opublikować źródła i instalatory oraz potwierdzić zdalne sumy, następnie bezpiecznie automatycznie zainstalować Test i sprawdzić uruchomioną wersję.

Limit bezpośrednio przed budową: **68% wykorzystane, 32% pozostało**, reset bez zmian.

## Potwierdzony wynik etapów

- Źródła: `f549090be998a718959ba7c587a33c05845cfee2`; merge obejmuje Cast/AirPlay oraz pełną alpha.520. Opublikowane w `codex/amc-unified-release-20261123` i zdalnie potwierdzone przed instalacją. Commit dokumentacyjny po wydaniu nie zmienia kodu paczek.
- Testy scalonego źródła: **597 Python zaliczonych, 16 podtestów, bez pominięć po wskazaniu nowego hosta**. Cztery zestawy C# zaliczone (dwa protokołu, bufor/zmiana nazwy sztucznego nagrania, rzeczywisty dispatcher). Kompilacje 0 błędów i ostrzeżeń. Osobno 13 testów nowej polityki profili przeszło także w roboczym repozytorium na D.
- Build: Test 3651 plików, Public 2924; oba `sourceDirty=false` i ten sam commit. Pełna kontrola plików oraz kontrolowany start GUI/hosta zaliczone w obu profilach. Cast/AirPlay są obecne; nowe moduły mają bajty zgodne ze źródłami. Sesje i pola możliwości manifestu obu profili są identyczne jak alpha.520, co zachowuje kontrakt istniejących aktualizatorów.
- Dodatkowa próba rzeczywistego nowego hosta, na pustych odizolowanych profilach Test/Public, potwierdziła rejestrację i walidację poleceń odsłuchu/zmiany nazwy. Pierwsza próba miała niepełne argumenty; po podaniu kompletnych argumentów oba profile prawidłowo odrzuciły nieistniejące nagranie. To nie jest odsłuch prywatnego nagrania ani fizycznego głośnika.
- Sekrety: instalatory mają `secretScan=passed`; dodatkowy skan 1617 plików tekstowych Test i 1466 Public nie znalazł wzorców tokenów ani danych kluczy. Nie pakowano profili, kont, prywatnych nagrań ani logów użytkownika.
- Funkcje nagrań są wspólne dla obu profili. Apple Music pozostaje tylko w Test; HEOS nie dołączono. Dwa historyczne globalne testy kompletności angielskich tłumaczeń pozostają otwarte; nie były uznane za zaliczone w tym wydaniu.

### GitHub i instalator Test
- Wersja użytkowa `24.11.2026, 01:00`, techniczna `0.1.0-alpha.522`.
- Instalator: `D:\Projekty Codex\AMC - integracja wxPython\publish\installers-alpha522-test-f549090b\AMC-Test-2026-11-24-0100.exe`.
- 214452254 bajtów, SHA-256 `518752f1d6f497638a42df7349a1f7eef27f9441fa1bc28c6a07e98433d58036`.
- [test-2026.11.24.0100](https://github.com/michalkasperczak/AMC/releases/tag/test-2026.11.24.0100).
https://github.com/michalkasperczak/AMC/releases/tag/test-2026.11.24.0100
- Tag zdalnie wskazuje dokładny commit kodu; nazwa, rozmiar, stan uploaded i SHA-256 wszystkich trzech zasobów (instalator, manifest aktualizacji, integralność) są zgodne. Oba wydania i aktualizatory potwierdzono przed lokalną instalacją.

### GitHub i instalator Public
- Wersja użytkowa `24.11.2026, 01:02`, techniczna `0.1.0-alpha.522`.
- Instalator: `D:\Projekty Codex\AMC - integracja wxPython\publish\installers-alpha522-public-f549090b\AMC-Public-2026-11-24-0102.exe`.
- 133977936 bajtów, SHA-256 `0bbf8baeab0424e14cb85399a293f0f6fb9bad329aa72c20ffc774391daf3232`.
- [public-2026.11.24.0102](https://github.com/michalkasperczak/AMC/releases/tag/public-2026.11.24.0102).
https://github.com/michalkasperczak/AMC/releases/tag/public-2026.11.24.0102
- Tag zdalnie wskazuje dokładny commit kodu; nazwa, rozmiar, stan uploaded i SHA-256 wszystkich trzech zasobów (instalator, manifest aktualizacji, integralność) są zgodne. Oba wydania i aktualizatory potwierdzono przed lokalną instalacją.

### Instalacja i rzeczywisty start Test
- Bezpośredni preflight: 0 procesów instalacji AMC i 0 koderów FFmpeg; odczytano 55 włączonych harmonogramów, z czego 0 w najbliższych 10 minutach. Nie było działającego właściciela nagrań ani innej operacji AMC do przerwania. Nie zamykano ani nie kończono procesów zbiorczo.
- Uruchomiono dokładnie lokalny, zgodny ze zdalnym zasobem instalator Test przez Windows (Verb Open), z opcjami cichej instalacji do `D:\Projekty Codex\AMC Python`. Kod zakończenia **0**. Nie otwierano pliku przez Codex ani osadzoną przeglądarkę.
- Manifest potwierdził właściwy profil, commit i czyste źródła. Pełna integralność wszystkich plików zainstalowanego pakietu zakończyła się kodem **0**. Istniejące pliki profilu/baz pozostały obecne i zachowały rozmiary przed uruchomieniem aplikacji; ustawień, bibliotek ani mediów użytkownika nie podmieniano.
- Uruchomiono stabilny launcher `D:\Projekty Codex\AMC Python\AMC.exe`. Potwierdzono jedną instancję launchera, prywatnego Pythona i hosta audio z tego samego katalogu, z czasami startu po instalacji. Tytuł okna: **AMC Test — 24.11.2026, 01:00**.
- Aktualizator uruchomiony odczytowo z nowej instalacji Test pokazuje `available=false` i bieżącą wersję `24.11.2026, 01:00` na tym samym commicie. Public nadal ma aktualizację do pobrania; jego lokalnej instalacji nie wykonywano.
- NVDA nie restartowano. Zainstalowany dodatek AMC miał już wersję 0.4.2, zgodną z instalatorem. Nie przeprowadzano nowej widocznej próby NVDA ani odsłuchu Cast/AirPlay.
- Odsłuch nowych wariantów Cast/AirPlay i odbiór NVDA pozostają otwarte. Historyczny problem AirPlay na WiiM nie jest uznany za naprawiony. Następny krok: użytkownik sprawdza Shift+C i Shift+M na swoim materiale/odbiorniku; poprawki obu profili prowadzić od tego wspólnego commitu.
- Lokalne instalatory i nieweryfikowane prace zachowano. Dowody publikacji, spójności, preflightu, instalacji oraz startu: `publish/release-alpha522-f549090b`.

Końcowy odczyt limitu: **68% wykorzystane, 32% pozostało**, reset 27.11.2026 o 22:53 Europe/Warsaw. Darmowego resetu nie użyto.
