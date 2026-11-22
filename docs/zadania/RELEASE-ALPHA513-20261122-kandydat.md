# AMC alpha.513 — kandydat Test i Public, przekazanie dla Codexa i Hermesa

Stan tego etapu: 22.11.2026, po zbudowaniu obu instalatorów. **Późniejsza publikacja obu profili jest potwierdzona w `RELEASE-ALPHA513-20261122-publikacja.md`; niniejszy raport zachowuje stan kandydata przed publikacją.** Gałąź `codex/alpha512-update-candidate-20261122`, pełny commit źródeł `a07b11c0a8bbdf76e413ed695479d8b2ee4e757d`, wersja techniczna `0.1.0-alpha.513`. Zastępuje wcześniejszy stan „brak paczki” w `ALPHA512-INTEGRATION-20261122.md` i `AMC-PO-ALPHA512-20261122-kolejka-codex-hermes.md`; nie zastępuje otwartych zadań odbioru.

## Gotowe pliki na D

| Profil | Instalator | Bajty | SHA-256 |
| --- | --- | ---: | --- |
| Test | `D:\Projekty Codex\AMC - integracja wxPython\publish\installers-alpha513-a07b11c0\AMC-Test-2026-11-22-1851.exe` | 214422916 | `51c7fcdd3225632d0d1c2966075c91765a4e7f8ef5e57e58480232c89a92c4a3` |
| Public | `D:\Projekty Codex\AMC - integracja wxPython\publish\installers-alpha513-public-a07b11c0\AMC-Public-2026-11-22-1853.exe` | 133957142 | `63a9ec2f8c6bfb272894742375e0cad5a298f8450dafb19ab45e0131891c2bd7` |

Obok każdego EXE leżą plik `.integrity.json` i `installer-compile-evidence.json`. Rzeczywiste sumy EXE są zgodne z oboma plikami integralności; `secretScan` ma wynik `passed`, a `sourceRevision` i `installerSourceRevision` wskazują ten sam czysty commit. Paczki źródłowe są w `publish\AMC-Test-2026-11-22-a07b11c0` i `publish\AMC-Public-2026-11-22-a07b11c0`; manifesty zawierają odpowiednio 3646 i 2919 plików, `sourceDirty=false`, wspólny commit i wersję `alpha.513`. Test ma etykietę użytkową `22.11.2026, 18:51`, Public `22.11.2026, 18:53`.

## Zakres zmiany

Kandydat obejmuje: #32 wyszukiwanie w Bibliotece ponad 50 000 elementów, #25 szybsze odświeżanie kanałów YouTube z RSS, #22 widok Foldery Biblioteki, zmianę adresu stacji i start nagrania Hermanic, odświeżanie transmisji kanału, Radio Archiwistę (Delete kolejki, ulubione stacji, lokalne nazwy), presety TIDAL i szybkie przypisanie, Delete w kolejkach Podcastów/YouTube/Spotify/TIDAL/Plików, zachowanie czasu i Entera TIDAL oraz #28/PR #29 i #30/PR #31 autorstwa Huberta dla wznowienia dźwięku i domyślnego wyjścia Windows. Szczegóły, różnice profili i granice potwierdzenia są w poszczególnych plikach zadania oraz w `ALPHA512-INTEGRATION-20261122.md`.

Nagrania Dwójki (wycinanie) i nowe prace Apple Music są nadal odłożone zgodnie z korektą Michała. Istniejące składniki Apple Music z alpha.512 zostały tylko zachowane w profilu Test, zgodnie z dotychczasowym procesem pakowania.

## Pochodzenie zależności i komendy

Lokalna pamięć `uv` nie zawierała `tidalapi`; próba `uv pip install --offline` zakończyła się informacją o niedostępnym pakiecie. Zamiast instalować globalnie lub zmieniać działającą instalację, odczytano manifest zainstalowanej alpha.512 (`d360cd89f622b90ae351f1ed876904b38856b625`), sprawdzono SHA-256 i odtworzono w izolowanym katalogu 2560 plików prywatnego runtime oraz 883 pliki przypiętych bibliotek w trzech osobnych środowiskach TIDAL, Sonos i Apple Music. Jeden dodatkowy plik instalacji bez wpisu w manifeście pominięto. Rewizja źródła `applemusic` z `direct_url.json` zgadza się z pinem `1d698359155d9b70d640234a1489165a06e6e01b`. Zainstalowany AMC był źródłem tylko do odczytu; nowa paczka powstała z czystego kandydata.

Obie paczki zbudowano `wxlite/tools/Build-AMCBundle.ps1 -Profile test/public`, wskazując zweryfikowany runtime, odpowiednie izolowane `site-packages` i nowy katalog wyjścia. Ustawiono osobne `APPDATA`, `LOCALAPPDATA`, `AMC_WX_LITE_HOME`. Obie przeszły pełną kontrolę plików, cichy start interfejsu i hosta oraz kontrolę pozostałych procesów. Instalatory zbudowano `wxlite/tools/Build-AMCInstaller.ps1 -Profile test/public -Version 0.1.0-alpha.513` z Inno Setup 6, po ponownej kontroli paczek. Test zawiera Quick App Switcher 0.1.6 i dodatek AMC NVDA 0.4.2; Public zawiera dodatek AMC NVDA 0.4.2. Instalatory nie zostały uruchomione.

Kompilacja Test zakończyła się mimo ostrzeżenia MSB3277 dotyczącego konfliktu wersji `WindowsBase` w dotychczasowym projekcie AppleMusicWebHost; Public nie miał tego ostrzeżenia. Nie nazywać budowy Test kompilacją bez ostrzeżeń. Logi tej budowy i rekonstrukcji są w roboczym katalogu Codexa `C:\Users\micha\Documents\Codex\2026-11-21\bbb\work\audio-pr29-pr31`; nie są źródłem kanonicznym dla Hermesa.

## Testy i rzeczywisty stan

- Dla #28/#30 po integracji: Windows SmokeTests i LiteHost Release zbudowane; dwie celowane ścieżki SmokeTests zakończone kodem 0, LiteHost ProtocolTests 40/40 PASS. Testy nie zastępują odsłuchu urządzeń.
- Po zmianie wersji: `python -m pytest wxlite/tests/test_protected_public_test_build_workflow.py -q` — 12 passed. Ostrzeżenie pytest dotyczyło braku zapisu podręcznej `.pytest_cache` w izolowanym worktree, nie wyniku testów.
- Ostatni **ukończony** pełny Python wcześniej w integracji: 2880 passed, 148 skipped, 30 failed, 34 subtests passed; te same 30 niepowodzeń pojawiło się w porównywanych plikach bazowej gałęzi. Po późniejszych przyrostach i wersji alpha.513 pełny zestaw nie został ukończony. Przebieg izolowany utknął i został przerwany; `-x` dotarł do pierwszego błędu kopii zapasowej obecnego także na bazie. Nie wolno przedstawiać całego zestawu jako zielonego.
- Testów na prawdziwym profilu >50 000 elementów, 122 kanałach YouTube, transmisji Hermanic, realnym przełączeniu wyjścia dźwięku i w NVDA nie wykonano. Nie sprawdzono kont TIDAL/Spotify na żywo.
- Nie ma instalacji Test/Public ani publikacji GitHub. Hubert nie otrzymał jeszcze informacji o wydaniu; wysłać ją niezwłocznie po faktycznej aktualizacji, z numerem wersji i ograniczeniami.

## Blokada bezpiecznej instalacji i następny krok

Po budowie wykryto działający `D:\Projekty Codex\AMC Python\AMC.exe`, jego `amc_lite_host.exe` i procesy ffmpeg. Mogą oznaczać ważne nagranie. Nie zamykano ich ani nie podmieniano instalacji. Stan procesu zmieniał się w trakcie pracy; ponownie sprawdzić go bezpośrednio przed instalacją. Gdy nie da się odczytowo ustalić, że nic nie nagrywa, uzgodnić bezpieczne okno z Michałem.

Następny krok: na bezpiecznym oknie uruchomić odbiór AMC Test z NVDA i krótkimi próbami #32, Hermanic (niedziałający i aktualny URL), Radio Archiwisty, kolejek, presetów oraz wyjścia audio. Po usunięciu ewentualnych regresji przygotować odpowiadające temu samemu commitowi paczki, zainstalować Test, oddzielnie odebrać Public, dopiero potem opublikować zweryfikowane wydanie w `michalkasperczak/AMC` zgodnie z kanałami `test` i `stable`. Po rzeczywistym wydaniu poinformować Huberta w odpowiednich Issues. Nie usuwać obecnych instalatorów ani starych działających plików przed potwierdzeniem zastępstwa.

## Materiał do dokumentacji użytkownika

Robocza lista zmian i skróty są w `AMC-ALPHA513-lista-zmian-i-instrukcja-robocza.md`. To szkic do korekty i aprobaty Michała, nie opublikowana instrukcja.

## Późniejsza kontrola publikacji 22.11.2026

`scripts/Publish-AMCRelease.ps1` uruchomiono dla Test i Public **bez** `-Publish`. Obie kontrole powtórnie sprawdziły rozmiar, SHA-256, profil i commit instalatora; utworzyły lokalne opisy, `amc-update-test.json`/`amc-update-public.json` i manifesty gotowości. Planowane tagi to `test-2026.11.22.1851` oraz `public-2026.11.22.1853`; kanały odpowiednio `test` i `stable`. Oba manifesty gotowości mają `published=false`. Skrypt nie wysłał plików ani komentarzy na GitHub. Robocze opisy wydania są w tym katalogu zadań jako `release-notes-test-alpha513-draft.md` i `release-notes-public-alpha513-draft.md`; wymagają korekty po odbiorze.
