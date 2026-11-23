# AMC alpha.518 — połączone wydania Test i Public

- Decyzja Michała z 23.11.2026: połączyć ukończone zmiany wideo, wyszukiwania,
  Spotify, kolejki i lewej strzałki w aktualizacjach obu profili. Niedokończony
  HEOS wykluczyć. Public pozostaje bez Apple Music.
- Korekta z 23.11.2026 zastępuje wcześniejsze ograniczenie wideo do Test:
  wideo lokalne, pobrane filmy i odtwarzacz YouTube online są dostępne także
  w Public. Moduły wideo i polecenia menu oraz palety wynikają z tej samej
  możliwości profilu `local-video-preview`.
- Public i Test mają wspólną kolejność wyników Ctrl+F, poprawkę przedwczesnego
  przejścia kolejki Spotify Soloist i odczyt czasu albumu TIDAL lewą strzałką.
  Wcześniejszy wspólny kod pomijania pojedynczego terminu harmonogramu pozostaje.
- Test dodatkowo zawiera poprawkę logowania do Apple Music przez zainstalowany
  Microsoft Edge. To poprawka logowania; wskazanie i sterowanie utworem w
  osadzonym odtwarzaczu nie zostało potwierdzone. Ukrytego odtwarzania nie
  należy przedstawiać jako gotowej funkcji.
- W obu paczkach `heos_client.py` jest wykluczony przez pakowanie, a instalator
  usuwa pozostałość tego modułu po starszych kandydatach. Źródło HEOS zostaje
  w repozytorium do dalszej pracy, bez aktywnej sesji w wydaniu.

## Sterowanie i komunikaty

- W Plikach oraz Podcastach i YouTube menu kontekstowe i paleta udostępniają
  „Odtwarzaj wideo”, „Odtwarzaj tylko audio” i, dla filmu online, „Otwórz w
  przeglądarce”. Enter nadal wykonuje domyślną czynność listy.
- Ctrl+F zachowuje kolejność wykonawcy, albumy, utwory i playlisty. Lewa
  strzałka na albumie TIDAL podaje znany czas albo mówi „Sprawdzam czas
  albumu”, następnie czas lub „Czas trwania nieznany”. Prawa strzałka na
  utworze pokazuje znane relacje.
- Nie dodano osobnego skrótu, polecenia ani komunikatu dla poprawki kolejki
  Spotify; koryguje ona przejście do następnego utworu po wczesnym pominięciu.
- Zmiany są wspólne dla obu profili z wyjątkiem Apple Music, które należy
  wyłącznie do Test. HEOS nie jest dostępny w żadnym profilu.

## Weryfikacja i ograniczenia

- Źródła połączono w izolowanym worktree z wydanego Test `ff8eafd9` oraz
  zmian `cf6861eb` i `25ae0757`, następnie przeniesiono tylko ukończoną
  poprawkę TIDAL. Robocze zmiany innych zadań w katalogu D: pozostały
  nienaruszone.
- Celowane testy Python profili, wideo, wyszukiwania, TIDAL i Apple Music:
  pierwszy przebieg 286 zaliczonych, 54 podtesty, jeden niezaliczony wskutek
  starego oczekiwania „wideo tylko Test”. Po aktualizacji kontraktu: 120
  zaliczonych i 49 podtestów dla profili, wideo oraz TIDAL.
- Kompilacja LiteHost Release po przywróceniu zależności: sukces, 0 ostrzeżeń,
  0 błędów. Pierwsza próba `--no-restore` nie miała zasobów nowego worktree;
  nie była błędem kodu.
- Odbiór NVDA, rzeczywistego wideo na komputerze użytkownika i sterowania
  Apple Music po instalacji pozostaje do wykonania. Kodu, testu, paczki,
  instalatora, publikacji i instalacji nie należy utożsamiać.

## Szkic instrukcji i listy zmian do korekty

W Plikach lub Podcastach zaznacz film i wybierz z menu „Odtwarzaj wideo”.
„Odtwarzaj tylko audio” ukrywa obraz. W wynikach Ctrl+F wykonawcy są przed
albumami i utworami. Lewa strzałka na albumie TIDAL podaje jego czas, jeśli
dane są kompletne. Poprawiono też przechodzenie kolejki Spotify po szybkim
pominięciu utworu. W Test logowanie Apple Music korzysta z Edge.

Szkic nie jest opublikowaną instrukcją użytkownika ani dowodem odbioru NVDA.

## Wynik wydania, 23.11.2026 wieczorem

- Commit źródeł: `bb7c5e0a8f5ba3626b17c62817d3bd708be474c0`, gałąź
  `codex/amc-unified-release-20261123`, wypchnięta do GitHub i odczytowo
  potwierdzona. Dokumentacyjny commit po publikacji nie zmienia kodu wydania.
- Końcowy test poleceń, profili i instalatora: 60 zaliczonych, 49 podtestów.
  Wcześniejszy przebieg 120 zaliczonych i 49 podtestów obejmował dodatkowo
  pakowanie i TIDAL. LiteHost Release: 0 błędów i 0 ostrzeżeń.
- Paczka Test: `publish/AMC-Test-2026-11-23-bb7c5e0a`, 3649 plików; Public:
  `publish/AMC-Public-2026-11-23-bb7c5e0a`, 2922 pliki. Obie mają
  `sourceDirty=false`, właściwy commit, pełną kontrolę plików i zaliczony
  kontrolowany start GUI oraz hosta. Test zawiera wideo i Apple Music, Public
  wideo bez Apple Music; obie nie zawierają HEOS.
- Instalator Test `AMC-Test-2026-11-23-2136.exe`: 214456190 bajtów, SHA-256
  `0b055bdeae984f287d38755579245380052c9abedf7c41407c201433c2c62c64`.
  Instalator Public `AMC-Public-2026-11-23-2138.exe`: 133950153 bajty,
  SHA-256 `3f5a018221be7d0560f0f4f7019f9233e89dc27e368923c8c44d407222c9e94a`.
  Lokalne pliki `.integrity.json` potwierdzają `secretScan=passed`, wersję
  `0.1.0-alpha.518`, profil i commit.
- GitHub Test: https://github.com/michalkasperczak/AMC/releases/tag/test-2026.11.23.2136
  — wydanie wstępne. GitHub Public:
  https://github.com/michalkasperczak/AMC/releases/tag/public-2026.11.23.2138
  — wydanie stabilnego kanału. W obu GitHub potwierdził 3 przesłane zasoby,
  rozmiary, zdalny digest SHA-256 instalatora identyczny z lokalnym i ten sam
  commit źródłowy. Manifesty gotowości mają `published=true`.
- Odczytowy test z aktualnie zainstalowanych profili: Test
  `23.11.2026, 20:04` widzi `available=True` i nowy tag `2136`; Public
  `14.11.2026, 16:45` widzi `available=True` i nowy tag `2138`.
  Nie pobrano instalatorów przez aktualizator, nie uruchomiono instalacji ani
  nie zamknięto działającego AMC. Użytkownik wykona aktualizację ręcznie.
- Kontrola zgłoszeń Huberta: 23.11.2026 nie było nowego zgłoszenia ani
  komentarza w `michalkasperczak/AMC`; najnowsze zmiany widoczne w GitHub
  pochodzą z 22.11.2026. Nie znaleziono też dzisiejszej poczty od Huberta.
- Po instalacji użytkownik sprawdzi NVDA, wideo w Public, lewą strzałkę TIDAL
  i wyszukiwanie. Dokładne odtwarzanie wskazanego utworu Apple Music nadal
  pozostaje odrębnym, niepotwierdzonym zadaniem.
