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
