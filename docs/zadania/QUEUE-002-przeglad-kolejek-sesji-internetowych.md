# QUEUE-002 — kolejki AMC Test i Public

Stan: 14.11.2026. Test i Public alpha.504 wydane oraz zainstalowane;
odbiór własnym odsłuchem NVDA i kont internetowych pozostaje otwarty.
Użytkownik zlecił przegląd aktywnego AMC Test wxPython,
z którego powstaje AMC Public. To zastępuje planistyczną decyzję z 30.10.2026
o odłożeniu przeglądu. Historyczny kod WPF nie należy do tego zadania.

## Zachowanie i decyzje

- `Ctrl+Q` otwiera żywą kolejkę plików, jeśli host ją zna; przed jej
  inicjalizacją otwiera kolejkę zapisaną. Komunikat otwarcia podaje nazwę,
  liczbę pozycji i łączny czas z biblioteki. Częściowo znany czas jest
  zapowiadany jako częściowy, a brak danych jako nieznany, bez fałszywego zera.
- Czas przy otwarciu podają także zapisane kolejki plików i podcastów,
  prywatne kolejki Spotify, TIDAL i Radio Archiwisty oraz zawartość lokalnej
  playlisty. Lewa strzałka na lokalnej playliście czyta jej czas, jeśli jest
  znany. Gdy usługa nie zwraca długości, program mówi o braku danych.
- W otwartej kolejce `Alt+strzałka w górę/w dół` przenosi jeden zaznaczony
  wpis. Zmiana jest trwała. W kolejce hosta nie przestawia grającego wpisu
  ani nie przenosi zwykłego wpisu przez grupę „Odtwórz jako następne”.
  Kolejki Spotify, TIDAL i Radio Archiwisty zapisują własny porządek;
  zapisane kolejki plików i podcastów zapisują porządek profilu.
- `Delete` usuwa wybrane wpisy z otwartej kolejki, także żywej. W żywej
  kolejce nie zatrzymuje aktualnego dźwięku. Następny i naturalny koniec
  uwzględniają aktualny porządek również po usunięciu bieżącego wpisu.
- Menus, opisy skrótów i dostępność komend uwzględniają kolejki w ich
  sesjach. Nie dodano kolejki do sesji, które nie mają takiej funkcji.

## Sprawdzenia

- Celowane testy Python: 170 istniejących zaliczonych; dodano 4 testy
  komunikatów czasu, odczytu długości, przenoszenia/Delete i skrótów.
- Test silnika `--queue`: zaliczony po dodaniu przypadków przestawienia,
  Delete następnego oraz Delete grającego wpisu i dalszego przestawienia.
- Kompilacja LiteHost: zaliczona. Pełny zestaw protokołu: 34 zestawy zaliczone.
- Pełny zestaw Python w środowisku offline bez prawdziwego wxPython: 2754
  zaliczone, 23 nieudane, 117 pominiętych, 34 podtesty zaliczone. Niepowodzenia
  obejmują starsze założenia testów profilu, ustawień, menu i GUI; dotyczący
  kolejki stary test opisu Page Down poprawiono i zaliczono ponownie.
  Nie traktować pełnego zestawu jako zielonego.

## Szkic instrukcji i listy zmian do korekty użytkownika

Otwórz kolejkę przez `Ctrl+Q`. AMC poda liczbę pozycji i jej czas trwania,
jeśli dane są dostępne. Zaznacz jedną pozycję i użyj `Alt+strzałka w górę` lub
`Alt+strzałka w dół`, aby zmienić kolejność. `Delete` usuwa zaznaczone pozycje
z kolejki. Podczas odtwarzania bieżąca pozycja nie zmienia miejsca, a usunięcie
jej z kolejki nie przerywa dźwięku. Otwarta playlista podaje czas trwania;
na lokalnej playliście lewa strzałka czyta czas samej pozycji.

Lista zmian: poprawiono usuwanie i ręczną kolejność kolejek w Test/Public.
`Ctrl+Q` oraz widoki playlist podają czas trwania z uczciwą informacją o
brakujących długościach. Poprawiono zgodność widoku z rzeczywistym następnym
utworem po Delete.

## Wersja i ograniczenia

Wydana wersja: `0.1.0-alpha.504`. Commit paczek i instalatorów:
`ea5a8bd0f4dcafee107e395dd2d618d954a348ca`.
Wcześniejsza wersja alpha.503 pochodziła z commitu
`5299b856361248538070de010ba85adc0aa736ce`.
Ręczne potwierdzenie NVDA oraz rzeczywiste konta Spotify/TIDAL/Radio
Archiwisty wymagają odbioru na zainstalowanej aplikacji.

## Wynik wydania i instalacji — 14.11.2026

- GitHub potwierdził commit źródła na gałęzi
  `codex/amc-test-public-20261109`. Obie paczki mają `sourceDirty=false`,
  Test 4730 plików, Public 3153. Pełna integralność i kontrolowany start GUI
  i hosta przeszły osobno dla obu paczek oraz po instalacji.
- Test: `D:\Projekty Codex\AMC - integracja wxPython\publish\test-queue-alpha503-20261114\AMC-Test-2026-11-14-0259.exe`,
  218499811 bajtów, SHA-256
  `19a6695b89351eda6499665d9191feb7b45942893cd3563e6cefb1fd8c546eba`.
  GitHub prerelease `test-2026.11.14.0259` zawiera instalator, plik
  integralności i manifest aktualizacji. Zdalna nazwa, rozmiar, stan
  `uploaded` oraz SHA-256 instalatora są zgodne z lokalnym plikiem.
- Public: `D:\Projekty Codex\AMC - integracja wxPython\publish\public-queue-alpha503-20261114\AMC-Public-2026-11-14-0304.exe`,
  133954042 bajty, SHA-256
  `7397751fd4126a4a48f4c6ac2a529bd441b18b6d009e74c547e5def29542c6eb`.
  GitHub stabilne wydanie `public-2026.11.14.0304` zawiera te same trzy typy
  plików; zdalna nazwa, rozmiar, stan i SHA-256 są zgodne.
- Przed aktualizacją Test w widoku aktywnych nagrań pokazał „zero elementów”.
  Zamknięto go zwykłą drogą. Cztery pliki profilu Test i cztery Public
  skopiowano do osobnych folderów pod
  `D:\Projekty Codex\AMC - kopie prywatne` i zweryfikowano SHA-256.
  Instalatory zakończyły się kodem 0. Wszystkie osiem plików profili po
  instalacji jest bajtowo zgodnych z kopiami.
- Test uruchomiono dokładnie z `D:\Projekty Codex\AMC Python\AMC.exe`.
  Okno `AMC Test — 14.11.2026, 02:59`, GUI
  `D:\Projekty Codex\AMC Python\runtime\python.exe` oraz host z tego
  samego folderu zostały potwierdzone. Public pozostał zamknięty, tak jak
  przed aktualizacją; jego zainstalowana paczka przeszła kontrolowany start
  z izolowanym profilem.
- Na rzeczywistym oknie Test `Ctrl+Q` w Plikach otworzył zapisaną kolejkę,
  ustawił fokus na natywnej liście i podał dostępny tekst:
  „Biblioteka — Kolejka (zapisana), 6 pozycji, łączny czas trwania 4 godz. 56 min”.
  Nie zmieniano wpisów użytkownika. Faktycznej wypowiedzi NVDA i działania
  na kontach Spotify/TIDAL/Radio Archiwisty nie przechwycono; odbiór otwarty.

Szkic instrukcji i listy zmian powyżej pozostaje materiałem do korekty
użytkownika, nie jest osobno opublikowanym podręcznikiem.

## Korekta zakresu — 14.11.2026

Po instalacji alpha.503 przegląd wykazał, że komunikat łącznego czasu
obejmował playlistę lokalną, lecz nie obejmował otwartych playlist Podcastów,
Spotify, TIDAL i Apple Music. Ta korekta rozszerza wcześniejszą decyzję
alpha.503 o wszystkie te widoki. Gdy playlista ma kolejne strony, komunikat
wyraźnie określa czas wczytanej części. Apple Music występuje tylko w Test.
Wersja następcza: alpha.504. Przed wydaniem zaliczono 122 celowane testy
nawigacji i komunikatów playlist.

## Wynik wydania alpha.504 — 14.11.2026

- Commit źródła `ea5a8bd0f4dcafee107e395dd2d618d954a348ca` wypchnięto
  do GitHub. Obie paczki mają `sourceDirty=false`, Test 4730 plików,
  Public 3153. Kompilacja, pełna kontrola integralności i kontrolowany
  start GUI i hosta przeszły dla obu paczek oraz po instalacji.
- Test: `D:\Projekty Codex\AMC - integracja wxPython\publish\test-queue-alpha504-20261114\AMC-Test-2026-11-14-0329.exe`,
  218512855 bajtów, SHA-256
  `afc2863c2d04683533886eade1b60df041d771b678c42af065da669cfed82fe7`.
  GitHub prerelease `test-2026.11.14.0329`; zdalna nazwa, rozmiar,
  stan `uploaded` i SHA-256 instalatora są zgodne.
- Public: `D:\Projekty Codex\AMC - integracja wxPython\publish\public-queue-alpha504-20261114\AMC-Public-2026-11-14-0329.exe`,
  133970175 bajtów, SHA-256
  `c4213d08a5fef16d3fed7e62ca2ad95c104be1c33880411c7ba137309c301a51`.
  GitHub stabilne wydanie `public-2026.11.14.0329`; zdalna nazwa,
  rozmiar, stan `uploaded` i SHA-256 są zgodne.
- Przed podmianą Test widok aktywnych nagrań pokazał „zero elementów”.
  Program zamknięto zwykłą drogą. Cztery pliki profilu Test skopiowano do
  `D:\Projekty Codex\AMC - kopie prywatne\QUEUE-20261114-przed-alpha504`
  i zweryfikowano SHA-256. Instalatory Test i Public zakończyły się kodem 0.
  Cztery pliki profilu Test po instalacji są zgodne z nową kopią;
  weryfikacja Public względem kopii sprzed alpha.503 też nie wykazała zmian.
- Zainstalowany Test uruchomiono z
  `D:\Projekty Codex\AMC Python\AMC.exe`. Potwierdzono okno
  `AMC Test — 14.11.2026, 03:29`, proces GUI i hosta. Proces GUI odpowiada.
  Odczyt drzewa dostępności po przejściu do Plików nie zwrócił danych,
  a kolejne wywołanie skrótu przekroczyło czas narzędzia. Nie potwierdzono
  więc głosem NVDA komunikatu alpha.504 ani zachowania na rzeczywistych
  kontach internetowych. Odbiór użytkownika pozostaje otwarty.
