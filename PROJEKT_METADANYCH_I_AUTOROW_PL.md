# Projekt metadanych albumu i autorów utworu

Status: zatwierdzony kierunek. `alpha.164` dodaje wyłącznie bezpieczne łącza
wyszukiwania do eksportu rozpoznań; automatyczne dopasowanie i nowe skróty nie
są jeszcze wdrożone.

## 1. Dwie różne funkcje

- **Wydanie i album** odpowiada na pytania o konkretną płytę: rok, kraj,
  wytwórnię, nośnik, numer katalogowy, listę utworów i kredyty tego wydania.
  Podstawowym dodatkowym katalogiem będzie Discogs.
- **Autorzy utworu** odpowiadają o dzieło niezależnie od konkretnego wydania:
  kompozytor, autor tekstu, ogólny autor, aranżer i ewentualnie tłumacz tekstu.
  Podstawowym otwartym źródłem będzie MusicBrainz na poziomie Work.

Nie wolno scalać tych odpowiedzi w jedno nieopisane pole „autor”. Wykonawca,
kompozytor i autor tekstu to trzy niezależne role.

## 2. Kolejność źródeł

1. Trwałe identyfikatory: MBID, ISRC, ISWC, identyfikator wydania Discogs oraz
   natywny identyfikator katalogu usługi.
2. Jawne tagi pliku: Composer, Lyricist/Writer, Album, Album Artist, numer
   utworu i data. Tag jest prezentowany jako źródło, a nie jako pewnik.
3. Oficjalny katalog aktywnej usługi, jeśli naprawdę udostępnia daną rolę.
   Przykładowo Apple Music ma pole kompozytora; zwykły obiekt utworu Spotify
   opisuje przede wszystkim wykonawców i nie zastępuje bazy autorów.
4. MusicBrainz: wyszukanie nagrania, przejście do Work i relacje composer,
   lyricist lub writer.
5. Discogs: wydanie, tracklista i kredyty konkretnego wydania. Dane
   społecznościowe mogą być niepełne i dotyczyć tylko jednego wydania.

Tytuł i wykonawca bez identyfikatora tworzą kandydatury, nigdy automatyczne
dopasowanie. AMC pokazuje źródło, poziom pewności i różnice, a użytkownik może
zatwierdzić, odrzucić lub pozostawić wynik nierozstrzygnięty.

## 3. Bezpieczny interfejs

- `Alt+Enter` po dopasowaniu pokazuje sekcje **Autorzy**, **Album i wydanie**,
  **Identyfikatory** oraz **Źródła danych**.
- Planowany `Ctrl+Alt+D` otwiera dostępne okno **Dane utworu i wydania** z
  kandydaturami MusicBrainz i Discogs. `Ctrl+D` pozostaje pobieraniem.
- Strzałka w prawo na zwykłym wierszu utworu może podać zwięźle dane już
  zapisane, np. „muzyka: …; tekst: …”. Brak danych daje krótki komunikat z
  poleceniem otwarcia wyszukiwania. Klawisz nie odpytuje sieci samodzielnie.
- Strzałka w prawo na folderze lub albumie nadal wchodzi głębiej. W otwartym
  odtwarzaczu nadal przewija. Dzięki temu nie zmieniamy znaczenia hierarchii ani
  transportu.

## 4. Sieć, konta i pamięć podręczna

Odczyt MusicBrainz nie wymaga konta, ale AMC musi wysyłać rozpoznawalny
User-Agent, zachowywać średnio najwyżej jedno żądanie na sekundę i buforować
wyniki. Discogs udostępnia API wydań, lecz wyszukiwanie wymaga autoryzacji;
token należy przechowywać w Menedżerze poświadczeń Windows. Bez tokenu program
może jedynie otworzyć zwykłe wyszukiwanie Discogs w przeglądarce.

Pamięć podręczna przechowuje odpowiedź, źródło, czas pobrania i identyfikatory,
ale nie hasła ani tokeny. Odświeżenie jest jawne. Awaria katalogu nie blokuje
odtwarzania, listy ani lokalnych tagów.

## 5. YouTube Music

YouTube Music nie ma odrębnego publicznego API katalogowego. `alpha.164`
tworzy bezpieczne łącze wyszukiwania do `music.youtube.com`. Późniejszy adapter
może użyć oficjalnego YouTube Data API do publicznego wyszukiwania filmów i
playlist oraz do operacji konta objętych OAuth, a wynik otworzyć w YouTube
Music. Nie korzystamy z prywatnych, nieudokumentowanych punktów aplikacji i nie
obiecujemy pełnej synchronizacji biblioteki YouTube Music.

## 6. Tworzenie playlist w usługach

Rozpoznana pozycja najpierw otrzymuje trwały lokalny rekord. Dla Apple Music,
Spotify, TIDAL i YouTube Music każdy adapter wykonuje własne wyszukiwanie i
zwraca kandydatury. Do playlisty trafia wyłącznie natywny identyfikator
zatwierdzonego wyniku. Brak pewnego dopasowania pozostaje na liście „wymaga
wyboru” i nigdy nie jest zastępowany utworem o podobnym tytule bez zgody.
