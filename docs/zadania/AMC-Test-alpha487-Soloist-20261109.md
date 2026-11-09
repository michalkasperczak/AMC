# AMC Test alpha.487 — Soloist, Sygnalista i lokalny Sonos

## Decyzja użytkownika — 9 listopada 2026

Użytkownik upoważnił do włączenia Soloist do następnej aktualizacji AMC Test, budowy i przygotowania zmian na GitHubie. Instalację zaplanowano na następny dzień. Ta decyzja rozszerza wcześniejszy kandydat Test z Sygnalistą, lokalnym Sonosem i wycofaniem LibreSpot; nie oznacza potwierdzenia działania na koncie Spotify ani zgody na przerwanie bieżącej pracy programu.

## Zachowanie widoczne dla użytkownika

- W sesji Spotify `Ctrl+F5` otwiera niemodalne okno konta. Przycisk „Konfiguruj odtwarzacz Soloist” otwiera osobne niemodalne okno. Ponowne wywołanie podnosi istniejące okno; główne AMC pozostaje aktywne.
- Użytkownik wskazuje oficjalny `soloist.exe`, wkleja swój klucz Soloist w ukrytym polu, wybiera „Zapisz ustawienia”, „Uruchom urządzenie” i „Sprawdź połączenie”. Adresy panelu klucza i pobierania są polami do skopiowania. Hasło konta wpisuje tylko na stronie Spotify. Katalog Spotify wymaga osobnego Client ID.
- Komunikaty stanu mówią o zapisanych ustawieniach, działającym urządzeniu lub braku połączenia; nie wypowiadają klucza. Po zamknięciu okna fokus wraca do przycisku konfiguracji konta, jeśli okno konta nadal istnieje, albo do aktywnej listy lub odtwarzacza AMC.
- Gdy Soloist potwierdzi sparowaną sesję Spotify Connect, nowe odtworzenie w AMC może użyć tego urządzenia. Przy braku gotowej sesji zostaje dotychczasowy tor Chrome. Ponowne naciśnięcie „Uruchom urządzenie” nie uruchamia drugiego procesu.
- Kolejka `Ctrl+Q` / `Ctrl+Shift+Q` jest kolejką AMC i nie synchronizuje się z kolejką konta Spotify. Soloist nie ma w tym przyroście sterowania prędkością, także dla podcastów Spotify; normalna prędkość ma etykietę `1,00x`. Nie obiecujemy kodeka, bitrate ani ręcznego wyboru wyjścia przez `Shift+A` dla Soloist.
- Ta kandydatka dotyczy tylko AMC Test. AMC Public pozostaje na dotychczasowej wersji. Aktualizacja usuwa dawny host LibreSpot z katalogu programu, zachowując dane profilu. Oficjalny plik Soloist nie znajduje się w instalatorze.

## Uzasadnienie i decyzje

Osobny klucz Soloist oraz Client ID katalogu wynikają z interfejsów Spotify. Klucz jest przechowywany w Menedżerze poświadczeń Windows, a ścieżka do programu w pliku ustawień danego profilu. Okno konfiguracji jest niemodalne zgodnie z zasadą zarządzania kontami w AMC. Adapter używa lokalnego oficjalnego `soloist ctl`; dopiero potwierdzony stan odtwarzania wywołuje zdarzenie startu i naturalne przejście lokalnej kolejki.

## Testy i rzeczywisty wynik

- Zintegrowany kod: 204/204 celowane testy Python (Spotify, profil, pakiet, instalator, Sonos i klient hosta); 6/6 sprawdzeń adaptera .NET. Test niemodalnego okna sprawdza ponowne wywołanie, jedno okno i powrót fokusu po ponownym otwarciu konta.
- Zbudowano samowystarczalną paczkę Test z czystych źródeł: 3455 plików; pełna kontrola integralności i cichy start GUI oraz hosta zaliczone. W paczce nie ma `soloist.exe` ani LibreSpot.
- Zbudowano instalator `0.1.0-alpha.487`. Kontrola instalatora potwierdziła zgodność źródła i paczki, profil Test, kompletność plików, poprawny dodatek NVDA i sumę SHA-256.
- Nie instalowano, nie uruchamiano użytkowo nowej wersji ani nie testowano rzeczywistej mowy NVDA i odtwarzania na sparowanym koncie. Działające instalacje Test i Public pozostały bez zmian.

## Znane ograniczenia i następny odbiór

Na prawdziwym koncie trzeba sprawdzić własny klucz, parowanie i odsłuch, opóźnienie odpytywania `soloist ctl`, przejście kolejki i fallback Chrome. Z NVDA trzeba sprawdzić Tab, Escape, początkowy fokus, komunikaty, ponowne otwarcie oraz powrót fokusu. Sonos czeka na próbę z rzeczywistym głośnikiem. Instalacja jutro wymaga ponownej kontroli nagrywania i spokojnego zakończenia bieżącej pracy w AMC Test.

## Wersja, commit i stan publikacji

Kod paczki i instalatora: `5b2d2263401a839f2162839c1427464445aa0a2f`. Techniczna wersja: `0.1.0-alpha.487`; nazwa użytkowa paczki: `AMC Test — 09.11.2026, 01:06`. Instalator ma 136490343 bajty, SHA-256 `a27d65730236027718f4947b9e3d469d02d6051248cacf09a2d86477f07e0143`. Stan: kod zapisany, testy zaliczone, paczka i instalator zbudowane; źródła wysłane na gałąź `codex/amc-test-alpha487-soloist-20261109` i otwarto [roboczy PR #13](https://github.com/michalkasperczak/AMC/pull/13). Instalacja i wydanie GitHub oczekują.

## Materiał do dokumentacji użytkownika

W AMC Test otwórz Spotify i naciśnij `Ctrl+F5`. W oknie konta wybierz „Konfiguruj odtwarzacz Soloist”, wskaż oficjalny plik Soloist i wklej własny klucz. Zapisz ustawienia i uruchom urządzenie. Następnie w aplikacji Spotify wybierz urządzenie „AMC Test” i sprawdź połączenie w AMC. Jeśli urządzenie nie jest gotowe, AMC korzysta z Chrome. Katalog i logowanie przez przeglądarkę nadal wymagają osobnego Client ID. Szkic wymaga korekty użytkownika po próbie NVDA; nie jest opublikowaną instrukcją.

## Szkic listy zmian do odbioru

AMC Test dodaje lokalne sterowanie odtwarzaczem Spotify Soloist po sparowaniu z kontem, upraszcza jego konfigurację w oknie konta i zachowuje Chrome przy braku połączenia. Dodaje internetowego Sygnalistę i lokalny Sonos, a usuwa nieużywany host LibreSpot. Szkic wymaga potwierdzenia po instalacji; nie jest opisem wydania opublikowanego na GitHubie.
