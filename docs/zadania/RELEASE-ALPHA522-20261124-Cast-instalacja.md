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
