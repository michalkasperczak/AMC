# AMC Public z Sonosem — korekta zakresu wydania 14.11.2026

Użytkownik 14.11.2026 polecił przygotować Public z działającym lokalnym Sonosem oraz uwzględnić zapisane zmiany YouTube i podcastów w aktualizacji AMC. Decyzja zastępuje wcześniejsze założenie w `AMC10-lokalny-Sonos-instrukcja-i-zmiany-szkic.md`, że Sonos jest wyłącznie w Test. Nie oznacza włączenia niezakończonej usługi logowania Sonos przez broker; chodzi o dostępne lokalne wyszukiwanie i sterowanie.

## Zachowanie i kontrakt

- Sesja Sonos pojawia się także w AMC Public jako istniejąca pozycja Menedżera sesji, menu Widok, pełnej pomocy, pomocy kontekstowej i palety. Dostępność wynika ze wspólnego modelu możliwości.
- Public zawiera `sonos_source.py` i przypięte zależności SoCo. Instalator Public nie usuwa już tego modułu przy aktualizacji. Apple Music pozostaje poza Public.
- Stan Test i Public, katalogi ustawień, przestrzenie poświadczeń, identyfikatory instalacji oraz kanały aktualizacji nadal są odrębne. Migracja starszego Public bez Sonosa jest akceptowana przez aktualizator.
- Wydanie ma zawierać poprawkę dodawania wielu pozycji do playlist, potwierdzenie odpowiedzi TIDAL, zmiany kolejek i wcześniej zapisane prace nad YouTube oraz podcastami. Dokładny commit i wersję ustalić po zakończeniu równoległych zmian źródeł.

## Weryfikacja bieżąca

- Python: `test_release_profile.py`, `test_bundle_layout.py`, `test_installer_profiles.py`: **82 zaliczone**.
- .NET: `AccessibleMediaController.PythonLauncher.Tests`: **46 zaliczonych**, w tym migracja Public bez Sonosa.
- Po zapisie równoległej zmiany Apple Music: menu i klawisze dostępu **44 zaliczone** po poprawieniu dwóch pozycji usuwania z kolejki; playlisty na atrapach wx **34 zaliczone**; Sonos i Frontier Smart w rzeczywistym wx **61 zaliczonych**. Pierwsza próba połączenia testów prawdziwego wx i dialogów na atrapach w jednym procesie dała błąd konstrukcji atrapy; osobny proces przeszedł. Wspólne menu wykryło realny brak `&` w dwóch pozycjach kolejki, już poprawiony.
- Szkic instalatora Test z wcześniejszego czystego commitu przeszedł kontrolę paczki; nowy wspólny zestaw źródeł będzie wymagał osobnych paczek i instalatorów. Nie przypisywać starego wyniku do nowej wersji.

## Ryzyko instalacji

Starsze AMC Test nadal działa z `C:\Users\micha\AppData\Local\Programs\AMC Test`, podczas gdy bieżąca instalacja projektowa Test leży w `D:\Projekty Codex\AMC Python`. W starszej instancji jest niedokończony formularz dodawania stacji YouTube. Nie zamykać jej ani nie nadpisywać katalogu C przed zabezpieczeniem wpisu. Poprzednie nieudane aktualizacje zgłoszone przez użytkownika nie mają jeszcze ustalonej przyczyny; zaliczona kompilacja instalatora nie zastępuje sprawdzenia rzeczywistej instalacji i uruchomienia.

## Szkic instrukcji i listy zmian

**Instrukcja:** W Menedżerze sesji AMC Public włącz Sonos. Wybierz lokalny głośnik, a następnie steruj odtwarzaniem i przeglądaj dostępne źródła. `Ctrl+Shift+P` dodaje kilka zaznaczonych pozycji do playlist zgodnie z bieżącą sesją; w Radiu playlista stacji nie podaje łącznego czasu.

**Lista zmian:** Dodano lokalnego Sonosa do Public. Zachowano osobne profile Test/Public. Sprawdzono migrację starszego Public. Do wspólnego wydania wchodzą potwierdzone poprawki playlist i zapisane zmiany YouTube oraz podcastów. Ten tekst jest szkicem do korekty użytkownika, bez automatycznej publikacji.
