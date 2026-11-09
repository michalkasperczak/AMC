# AMC Test i Public — Soloist, Lossless i aktualizacja

Stan: 9 listopada 2026. Użytkownik zlecił włączenie Soloist z jakością Spotify Lossless oraz wydanie zbiorczych aktualizacji Test i Public. Zastępuje to decyzję z 9 listopada w `AMC-Test-alpha487-Soloist-20261109.md`, według której kandydat dotyczył wyłącznie Test i nie żądał konkretnego poziomu jakości.

## Zachowanie

- Konto Spotify (`Ctrl+F5`) ma przycisk „Konfiguruj odtwarzacz Soloist”. Jego niemodalne okno przyjmuje ścieżkę oficjalnego `soloist.exe` i własny klucz, uruchamia urządzenie oraz sprawdza połączenie. Klucz jest zapisywany w Menedżerze poświadczeń Windows, osobno dla Test i Public. Katalog nadal wymaga Client ID.
- Przed uruchomieniem Soloist AMC zapisuje w globalnych i istniejących ustawieniach użytkowników trzy preferencje silnika z poziomem `5`, czyli żądaniem Lossless. Pozostałe preferencje zachowuje i podmienia pliki atomowo. Błąd zapisu zatrzymuje start, zamiast zgłaszać niepotwierdzoną konfigurację.
- To jest żądany maksymalny poziom dla muzyki Premium. Faktyczny kodek może zależeć od utworu, konta i połączenia. Publiczne API Soloist nie podaje dostarczonej jakości. Podcasty nie są deklarowane jako FLAC. Okno wyjaśnia to bez obiecywania wyniku odsłuchu.
- Po potwierdzeniu parowania odtwarzanie może korzystać z Soloist; w przeciwnym razie pozostaje Chrome. `soloist.exe` i prywatny klucz nie są częścią instalatora ani repozytorium. Dla Soloist nie ma potwierdzonego sterowania prędkością ani wyborem wyjścia przez `Shift+A`.

## Polecenia i dostępność

`Ctrl+F5` otwiera konto Spotify w obu profilach. To samo polecenie ma zaktualizowany opis w pomocy i palecie. Przycisk Soloist ma jawną nazwę dla NVDA. Okno jest niemodalne, po ponownym wywołaniu podnosi istniejące okno, a po zamknięciu przywraca fokus. Rzeczywisty odbiór NVDA, Tab, Escape, ponowne otwarcie oraz odsłuch na koncie czekają na instalację.

## Weryfikacja

- Po integracji z dzisiejszymi poprawkami: 79/79 celowanych testów Python dla Spotify, kont, palety, hosta i Sonosa; 6/6 testów adaptera .NET. Po dodaniu żądania Lossless: 16/16 testów modułu i okien Spotify. Test preferencji obejmuje ustawienia globalne i użytkownika, zachowanie innego klucza oraz zastąpienie niższego poziomu. Nie jest to dowód faktycznego strumienia FLAC.
- Pełny zestaw Python zatrzymał się najpierw na teście aktualizacji zależnym od stanu modułu wx w kolejności suite; test uruchomiony osobno przeszedł 20/20. Testy uodporniono na brak chwilowo usuniętych atrybutów wx; należy ponowić pełny zestaw.
- Zbudowano i opublikowano oba profile z czystego commitu. Instalacja lokalna, ręczna próba NVDA, parowanie i potwierdzenie jakości na koncie czekają. Przed instalacją Test trzeba sprawdzić bieżące nagrywanie.

## Źródła i ograniczenia

Spotify deklaruje obsługę Lossless przez Soloist, ale nie udostępnia publicznego ustawienia jakości ani odczytu rzeczywistego kodeka. Implementacja zapisu preferencji opiera się na pierwotnym kodzie i pomiarze projektu Music Assistant (`music_assistant/providers/spotify_connect/soloist/prefs.py` oraz `README.md`), zbadanym dla Soloist 1.3.7.349. Lokalnie dostępny Windows Soloist ma wersję 1.3.10.51, więc wynik wymaga próby na sparowanym koncie.

https://developer.spotify.com/blog/2026-08-13-introducing-spotify-soloist
https://developer.spotify.com/documentation/soloist/reference/command-line
https://github.com/music-assistant/server/blob/dev/music_assistant/providers/spotify_connect/soloist/prefs.py
https://github.com/music-assistant/server/blob/dev/music_assistant/providers/spotify_connect/soloist/README.md

## Szkic dla użytkownika

W koncie Spotify otwórz konfigurację Soloist, wskaż oficjalny plik, wpisz swój klucz, zapisz ustawienia i uruchom urządzenie. W aplikacji Spotify wybierz AMC Test lub AMC Public. AMC poprosi Soloist o najwyższą jakość dla muzyki Premium; rzeczywisty format strumienia trzeba potwierdzić podczas odsłuchu, bo Soloist nie raportuje go do AMC. Każdy profil ma osobne ustawienia.

## Wynik publikacji — 9 listopada 2026

- Czyste źródło bez prywatnych zapisów nagrań: `bf78903399254390ee5432354d45e0c30b5227d8`, gałąź `codex/amc-test-public-release-20261109`, potwierdzona na GitHubie. Techniczna wersja obu profili: `0.1.0-alpha.487`.
- Regresja na dokładnym commicie: 424 testy Python zaliczone, 2 pominięte; 43 testy launchera, 7 zestawów protokołu hosta i 6 sprawdzeń adaptera Soloist. Pełny historyczny zestaw Python wykazał przestarzałe założenia testów TIDAL w `test_gui_wires_activity_views.py`; nie został zaliczony w całości. Nowe testy kopii profilu wykryły i usunęły brak kolejki Spotify w eksporcie.
- Test: paczka 3455 plików, integralność i cichy start GUI/hosta zaliczone. Instalator `D:\Projekty Codex\AMC - integracja wxPython\publish\test-alpha487-20261109\AMC-Test-2026-11-09-2007.exe`, 136486176 bajtów, SHA-256 `021d8ed1ad70041075a25706adf1cc010b40e0ae0f17eb5e0948b8317a43b120`. Zdalne zasoby, rozmiar i SHA-256 potwierdzone. Wydanie wstępne: https://github.com/michalkasperczak/AMC/releases/tag/test-2026.11.09.2007
- Public: paczka 3148 plików, integralność i cichy start GUI/hosta zaliczone. Instalator `D:\Projekty Codex\AMC - integracja wxPython\publish\public-alpha487-20261109\AMC-Public-2026-11-09-2011.exe`, 133896447 bajtów, SHA-256 `a8904618f5f7452a388520c55b685fc0becead66633a735cfe6508c8dd2e72ea`. Zdalne zasoby, rozmiar i SHA-256 potwierdzone. Wydanie zwykłe: https://github.com/michalkasperczak/AMC/releases/tag/public-2026.11.09.2011
- Obie paczki zawierają adapter Soloist i żądanie Lossless; żadna nie zawiera pliku `soloist.exe` ani LibreSpot. Sonos jest tylko w Test. Oba instalatory przeszły skan sekretów. Nie instalowano ich lokalnie i nie zmieniano działających procesów.
- GitHub przerwał pierwszy upload Test HTTP 408. Istniejący szkic sprawdzono, dołączono tylko brakujący instalator i opublikowano po weryfikacji. Nie powstał drugi tag.
