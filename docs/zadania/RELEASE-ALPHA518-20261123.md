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
