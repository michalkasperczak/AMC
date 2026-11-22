# AM-008 — wylogowanie i dokładne odtwarzanie Apple Music

## Status i zlecenie

22.11.2026. Osobne zadanie po alpha.513. Michał zgłosił, że Apple Music
wylogowało się: wyszukiwanie nie dawało oczekiwanego wyjaśnienia, a po
wybraniu „Tak” w „Konto Apple Music” widoczne logowanie nie otworzyło się.
Równolegle należy ustalić, czy AMC może niezawodnie odtwarzać **dokładnie
wybrany** utwór. W tym etapie poprawiono i przetestowano jedną przyczynę
pominięcia okna; pełne odtwarzanie i przywrócenie rzeczywistej sesji pozostają
niepotwierdzone. Nie odczytywano tokenów, cookies ani osobistej Biblioteki.

## Uzasadnienie i decyzje

- Zainstalowany moduł `applemusic.auth.AppleMusicAuth.authenticate(force=False)`
  jawnie wraca z ważnej sesji cache bez otwierania Chromium. AMC wywoływało
  go bez `force`, choć pytanie „Czy rozpocząć logowanie ... w widocznej
  przeglądarce?” obiecuje okno po „Tak”. To potwierdzona luka w kodzie.
- `connect_account` wywołuje teraz `authenticate(force=True)` **tylko po
  świadomym potwierdzeniu użytkownika**. Odczyt stanu konta nadal pozostaje
  lokalny i nie otwiera okna. Brak zależności/Chromium, błąd sieci lub inny
  problem logowania wciąż mogą uniemożliwić start; ta zmiana nie dowodzi,
  że każdy przypadek zgłoszony przez Michała miał tę jedną przyczynę.
- Status „zapisano zaszyfrowaną sesję” nie oznacza jej ważności. Katalog i
  Biblioteka Apple Music to odrębne wyszukiwania; obecne błędy są opisywane
  ogólnie, bez rozróżnienia wygasłej sesji i awarii sieci. Potrzebna osobna
  poprawka dostępnych komunikatów, bez wycieku danych dostawcy.
- Oficjalna dokumentacja Apple opisuje Apple Music API jako dostęp do
  katalogu/Biblioteki, a MusicKit on the Web jako drogę odtwarzania w webie.
  Nie wynika z tego, że otwarcie linku w aplikacji Windows wybierze utwór.
  W AMC `AppleMusic.exe /url` i SMTC nie dowiodły takiego wyboru. Web host
  próbuje kliknięcia wiersza i `MusicKit.setQueue({song:id})`, po czym porównuje
  `nowPlayingItem.id`; żywa próba na obecnym wydaniu nie została wykonana.
  Dodatkowo po niezgodności ID web host zgłasza błąd, ale w obecnym kodzie
  nie zatrzymuje możliwego wcześniejszego dźwięku. To osobna granica
  bezpieczeństwa przed uznaniem odtwarzania za gotowe.

## Kod, testy i wyniki

- `wxlite/amc_wx_lite/apple_music_account.py`: jawne `force=True`.
- `wxlite/tests/test_apple_music_account.py`: test oczekiwania wymuszonego
  logowania. RED przed poprawką: 1 niezaliczony z 5, `['cached']` zamiast
  `['force']`. GREEN po poprawce: 5/5 zaliczonych.
- Sąsiednie testy bezpiecznego magazynu i zależności: 8/8 zaliczonych.
- Odczyt zainstalowanego modułu `applemusic.auth` potwierdził obsługę
  `authenticate(force=True)` i pomijanie okna przy `force=False` z cache.
- Nie uruchamiano logowania, Apple Music, widocznego okna, odtwarzania,
  instalatora ani testu NVDA. Nie zbudowano ani nie opublikowano nowego wydania.

## Warunki rzeczywistego odbioru i następny krok

1. W odizolowanym profilu AMC Test po potwierdzeniu „Konto Apple Music”
   sprawdzić widoczne okno, fokus NVDA, logowanie oraz powrót do AMC bez
   restartu. Nie przesyłać żadnych tokenów ani plików sesji do raportu.
2. Oddzielić w interfejsie pusty wynik od braku autoryzacji i awarii sieci;
   katalog ma nadal działać, jeśli nie działa prywatna Biblioteka.
3. Dwa znane różne utwory z katalogowymi ID odtworzyć kolejno przez tor webowy;
   potwierdzić `nowPlayingItem.id`, słyszalne pełne nagranie, pauzę/wznowienie,
   czas i zatrzymanie dźwięku po błędzie lub zmianie toru. Nie uznawać
   otwartego linku ani samego licznika za pełny sukces.

## Materiał do dokumentacji użytkownika

Po przyszłej aktualizacji AMC Test wybór „Konto Apple Music” (`Ctrl+F5` w tej
sesji) i potwierdzenie „Tak” powinny uruchomić widoczne logowanie nawet wtedy,
gdy AMC ma zapisaną sesję. Sama obecność zapisanej sesji nie potwierdza, że
Apple ją nadal akceptuje. Odtwarzanie dokładnie wybranego utworu nadal wymaga
sprawdzenia; polecenie otwarcia w oryginalnym Apple Music nie daje tej gwarancji.
Funkcja nie należy do profilu AMC Public.

## Źródła i wersja

- Gałąź do pobrania przez Hermesa: `codex/apple-account-20261122`
  w repozytorium `michalkasperczak/AMC`.
- Apple Music API: https://developer.apple.com/documentation/applemusicapi
- MusicKit on the Web: https://developer.apple.com/musickit/
- Autoryzacja MusicKit: https://developer.apple.com/documentation/applemusicapi/user-authentication-for-musickit
- Baza kodu: `7751dee7c0d417ce58e941745a0a81ebdfc03ada`.
- Commit poprawki: `cc4ebdf655aa671729d5049a23bfa65afc1fa8c1`.
  Stan: źródła i celowane testy; bez buildu,
  instalacji, publikacji i żywego odbioru.
