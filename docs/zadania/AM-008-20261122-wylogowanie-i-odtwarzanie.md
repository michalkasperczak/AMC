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
  Biblioteka Apple Music to odrębne wyszukiwania. Poprawka z 22.11.2026
  zastępuje wcześniejszą decyzję o pozostawieniu ogólnego błędu: kod 401
  daje komunikat „Apple Music odrzuciło logowanie. Otwórz Konto Apple Music
  (Ctrl+F5).”, błąd połączenia oznaczony przez dostawcę kodem 0 daje
  „Nie udało się połączyć z Apple Music. Spróbuj ponownie.”, a brak zapisanej
  sesji prowadzi do „Apple Music nie jest zalogowane. Otwórz Konto Apple Music
  (Ctrl+F5).”. Inne błędy nadal dają neutralny komunikat bez treści wyjątku,
  tokenów i cookies. Awaria Biblioteki nie blokuje próby katalogu.
- Oficjalna dokumentacja Apple opisuje Apple Music API jako dostęp do
  katalogu/Biblioteki, a MusicKit on the Web jako drogę odtwarzania w webie.
  Nie wynika z tego, że otwarcie linku w aplikacji Windows wybierze utwór.
  W AMC `AppleMusic.exe /url` i SMTC nie dowiodły takiego wyboru. Web host
  próbuje kliknięcia wiersza i `MusicKit.setQueue({song:id})`, po czym porównuje
  `nowPlayingItem.id`; żywa próba na obecnym wydaniu nie została wykonana.
  Po niezgodności ID web host wcześniej zgłaszał błąd, ale nie zatrzymywał
  możliwego wcześniejszego dźwięku. Poprawka z 22.11.2026 zastępuje tę
  decyzję: po nieudanej weryfikacji próbuje `stop()`, a po jego błędzie
  `pause()`. Dopiero potwierdzony stan bez odtwarzania daje komunikat
  „Odtwarzanie zatrzymano”; w innym przypadku komunikat nakazuje otworzyć
  okno odtwarzacza i zatrzymać dźwięk ręcznie. To nadal wymaga próby na żywo.

## Kod, testy i wyniki

- `wxlite/amc_wx_lite/apple_music_account.py`: jawne `force=True`.
- `wxlite/tests/test_apple_music_account.py`: test oczekiwania wymuszonego
  logowania. RED przed poprawką: 1 niezaliczony z 5, `['cached']` zamiast
  `['force']`. GREEN po poprawce: 5/5 zaliczonych.
- Sąsiednie testy bezpiecznego magazynu i zależności: 8/8 zaliczonych.
- Po połączeniu w jedną gałąź ponownie zaliczono 5/5 testów konta i 8/8
  testów magazynu; to nadal testy źródeł, nie próba logowania na żywo.
- `wxlite/amc_wx_lite/apple_music_library.py` mapuje 401 i 0 na bezpieczne
  błędy wyszukiwania; `wxlite/amc_wx_lite/gui.py` przekazuje ich komunikaty do
  ostrzeżeń wyników, osobno dla Biblioteki, katalogu i otwarcia sesji.
  `wxlite/tests/test_apple_music_library.py` sprawdza oba wyszukiwania oraz
  brak wycieku przykładowych sekretów. RED: 1 błąd importu nowego typu przed
  kodem. GREEN na aktywnym źródle: 21/21 testów Biblioteki, 5/5 konta i 8/8
  magazynu, kod zakończenia 0. Składnia dwóch zmienionych modułów przechodzi.
  Wbudowany Python AMC ignoruje `PYTHONPATH`, więc testy uruchomiono z
  `sys.path.insert(0, <aktywne źródła>/wxlite)`; bez tego ładował starą kopię
  modułu z `D:\Projekty Codex\AMC Python\app` i pozornie nie przechodził.
- `src/AccessibleMediaController.AppleMusicWebHost/PlayerWindow.cs` zatrzymuje
  odtwarzanie po niezgodności ID wybranego utworu. Celowany build projektu
  AppleMusicWebHost: kod 0, zero błędów, jedno ostrzeżenie o konflikcie wersji
  `WindowsBase`. Pierwsza próba buildu bez dostępu do NuGet nie pobrała audytu
  pakietów (`NU1900`); powtórzono ją z lokalnego cache, z `NuGetAudit=false`,
  do osobnego katalogu roboczego. Projektu nie zainstalowano ani nie uruchomiono.
  Nie oznacza to zbudowania całego AMC ani potwierdzenia dźwięku na żywo.
- Odczyt zainstalowanego modułu `applemusic.auth` potwierdził obsługę
  `authenticate(force=True)` i pomijanie okna przy `force=False` z cache.
- Sąsiednia próba pojedynczego testu GUI wyszukiwania wypisała kropkę
  wykonania, ale proces pytest nie zakończył się po około 50 s i został
  przerwany. Nie liczymy jej jako zaliczonego przebiegu. Przy następnym
  etapie GUI trzeba rozdzielić wynik asercji od problemu zamykania wx.
- Nie uruchamiano logowania, Apple Music, widocznego okna, odtwarzania,
  instalatora ani testu NVDA. Nie zbudowano ani nie opublikowano nowego wydania.

## Warunki rzeczywistego odbioru i następny krok

1. W odizolowanym profilu AMC Test po potwierdzeniu „Konto Apple Music”
   sprawdzić widoczne okno, fokus NVDA, logowanie oraz powrót do AMC bez
   restartu. Nie przesyłać żadnych tokenów ani plików sesji do raportu.
2. W widocznym interfejsie i NVDA potwierdzić rozdział pustego wyniku, 401,
   braku połączenia oraz innych błędów. Sprawdzić, że katalog działa, kiedy
   nie działa prywatna Biblioteka. Kod i testy jednostkowe są gotowe,
   odbiór widocznego interfejsu nadal czeka.
3. Dwa znane różne utwory z katalogowymi ID odtworzyć kolejno przez tor webowy;
   potwierdzić `nowPlayingItem.id`, słyszalne pełne nagranie, pauzę/wznowienie,
   czas i zatrzymanie dźwięku po błędzie lub zmianie toru. Osobno sprawdzić
   niezgodność ID, automatyczne zatrzymanie oraz komunikat ręcznego zatrzymania
   po jego błędzie. Nie uznawać
   otwartego linku ani samego licznika za pełny sukces.

## Materiał do dokumentacji użytkownika

Po przyszłej aktualizacji AMC Test wybór „Konto Apple Music” (`Ctrl+F5` w tej
sesji) i potwierdzenie „Tak” powinny uruchomić widoczne logowanie nawet wtedy,
gdy AMC ma zapisaną sesję. Sama obecność zapisanej sesji nie potwierdza, że
Apple ją nadal akceptuje. Odtwarzanie dokładnie wybranego utworu nadal wymaga
sprawdzenia; polecenie otwarcia w oryginalnym Apple Music nie daje tej gwarancji.
Funkcja nie należy do profilu AMC Public.
Wyszukiwanie po przyszłej aktualizacji powinno podpowiedzieć ponowne otwarcie
Konta przy odrzuceniu sesji oraz ponowienie próby przy błędzie połączenia.

## Źródła i wersja

- Jedna aktywna gałąź dalszych prac: `codex/alpha512-update-candidate-20261122`
  w repozytorium `michalkasperczak/AMC`. Dawna gałąź Apple była tylko
  etapem izolowanej pracy.
- Apple Music API: https://developer.apple.com/documentation/applemusicapi
- MusicKit on the Web: https://developer.apple.com/musickit/
- Autoryzacja MusicKit: https://developer.apple.com/documentation/applemusicapi/user-authentication-for-musickit
- Baza kodu: `7751dee7c0d417ce58e941745a0a81ebdfc03ada`.
- Zintegrowany commit poprawki: `918c1cc8`.
- Zintegrowany commit komunikatów wyszukiwania:
  `159519872bc4dd21d97195300a97d1e35e279fce`.
- Zintegrowany commit bezpiecznego zatrzymania:
  `c57f209f02f1dfa8778f6c51a0454d3ec34a8f4f`.
  Stan: źródła, celowane testy i build projektu AppleMusicWebHost; bez
  instalacji, nowego wydania i żywego odbioru.
