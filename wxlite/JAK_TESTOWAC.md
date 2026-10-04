# AMC Python — uruchamianie i sprawdzanie bieżącego przyrostu

To rozwijany równoległy interfejs pełnegoAMC. Nie jest jeszcze zamiennikiem wszystkich funkcji programu. Katalog i nazwa uruchamiacza `wxlite` są historyczne; nie oznaczają decyzji o ograniczeniu docelowego zakresu do dwóch sesji.

## Co zostało wykonane

Przy kodzie bf712d6:368testów/0błędów/0pominięć. NaWindows sprawdzono zwykły start z hostem, pełną kopię Biblioteki/Radia, cztery nowe operacje widoków, rzeczywiste skróty, mówione potwierdzenia NVDA i etykiety transportu. Wcześniejszy połączony przebieg potwierdził także odtwarzanie/pauzę/wznowienie przez klawiaturę i sygnał urządzenia audio. Nie jest to pomiarNarratora ani wszystkich funkcjiAMC.

## Przygotowane stanowisko prywatne

Odebrany układ naHermesie: `C:\Users\Michal\AppData\Local\Temp\amc-wx-425` z podkatalogami `app`, `runtime`, `host` i prywatną kopią `profile`. Jest to staging, nie numer publicznego wydania.

Uruchomienie bez obserwatora, z tym samym prawdziwym punktem wejścia:

```powershell
$stage = 'C:\Users\Michal\AppData\Local\Temp\amc-wx-425'
$env:APPDATA = "$stage\profile"
$env:LOCALAPPDATA = "$stage\profile"
$env:AMC_LITE_HOST = "$stage\host\amc_lite_host.exe"
Remove-Item Env:AMC_WX_FIXTURE -ErrorAction SilentlyContinue
& "$stage\runtime\python.exe" -m amc_wx_lite
```

Prywatny runtime ma `python314._pth` wskazujący `../app`. Embeddable Python może ignorować `PYTHONPATH`: zawsze sprawdzaj faktyczne `gui.__file__` i zgodność kopiowanych modułów. Stary moduł kandydata w innym katalogu nie testuje nowego kodu.

Bez przekierowania `APPDATA`/`LOCALAPPDATA` aplikacja domyślnie czyta profil właściwego użytkownikaWindows. Testy wykonuj na kopii. Zmienna `AMC_WX_FIXTURE` wybiera inny tryb prywatnej piaskownicy, dlatego nie zastępuje automatycznie prób lustrzanego, pełnego profilu.

## Odbiór klawiatury i NVDA

1. Ctrl+1: Biblioteka. Foldery mają pochodzić z bazy, nawet gdy zapisane dyski nie istnieją na stanowisku. Enter i Backspace zachowują logiczny powrót.
2. Alt+2: wszystkie lokalne pliki alfabetycznie. Porównaj liczność ze źródłem, nie z wpisaną na stałe liczbą.
3. Ctrl+U: Ulubione. Zachowaj właściwą kolejność i wybór.
4. Ctrl+P: playlisty; Enter do zawartości; Backspace na tę samą playlistę.
5. Ctrl+C: nazwa, pojedyncze potwierdzenie na każde świadome naciśnięcie. Ctrl+Shift+C: pełna ścieżka jakoTEKST, komunikat „Skopiowano pełną ścieżkę". Sprawdź rzeczywistą zawartość schowka zWindows (`Get-Clipboard`, czytaj bajty i dekoduj sam — `text=True` wywala się na polskich znakach), nie sam komunikat. Komunikat celowo NIE mówi o skopiowaniu pliku: obsługi formatuFileDrop nadal nie ma.
6. Ctrl+O: zwykły dialog folderu, nie Biblioteka. W zmierzonym dialogu pierwszyEnter wybiera wpisany folder, drugi zatwierdza; kontroluj rzeczywisty fokus i zamknięcie okna zamiast wysyłać gesty w ciemno.
7. Na dostępnym pliku:Enter, Spacja, Spacja. Stan przycisku Odtwórz/Wstrzymaj ma zgadzać się z działaniem; cisza czytnika i sam tekst statusu nie dowodzą komunikatuNVDA.
8. Ctrl+2: Radio z `radio.stations` wspólnego profilu. W trybie tylko do odczytu dodawanie/zmiana/usuwanie/import nadal mają odmówić uczciwie. Prywatna lista piaskownicy to odrębny tryb, nie zapis doAMC.

9. Menu (Alt): pasek ma być w całości klawiaturowy — Alt otwiera, strzałki chodzą, litery wybierają, Escape zamyka. Każda pozycja musi wykonać TO SAMO co jej skrót (ten sam `_dispatch`). W Radiu pozycje lokalnych Ulubionych/playlist mają być wyszarzane, nie ukryte. Nie dodawaj pozycji dla funkcji, których nie ma.

10. Widoki aktywności. Ctrl+H historia, Ctrl+Q kolejka **zapisana**, Ctrl+Shift+B zakładki ZAZNACZONEGO pliku. Trzy rzeczy do sprawdzenia poza samą licznością:
    - Etykieta kolejki musi mówić „(zapisana)". Nie wolno zaliczyć przebiegu jako „kolejka działa", bo czytamy zapis profilu, a nie kolejkę żywego silnika.
    - Ctrl+Shift+B, nie Ctrl+B. Ctrl+B w C# to `GetForDisplay` (wszystkie zakładki); nasz widok to węższe `GetForItem`.
    - Backspace z zakładek ma wrócić na TEN plik. Sprawdzaj ID zaznaczenia, nie numer wiersza — po powrocie do Wszystkich plików numer jest inny (zmierzono 2309 z 2476).
11. Fizyczny skok zakładki mierz pozycją z ŻYWEGO hosta (`transport.status`), nie oczekiwanym payloadem. `play.file` dostaje `positionSeconds` w jednym wywołaniu — nie wysyłaj seeka obok play, bo pozycja ginie przy starcie nowego pliku. Materiał do próby zrób SYNTETYCZNY: wygenerowany plik plus wstrzyknięty rekord w OSOBNEJ kopii pełnej bazy (nie w małej próbce, nie na pliku użytkownika). Przy wstrzykiwaniu zarejestruj zastępczą kolację `AMC_PL`, inaczej `INSERT` padnie na indeksie. Pozycję daj z częścią ułamkową (próba:83,456s) — kontrakt to dzielenie `/ 10_000_000`, nie `//`.

Przed audio/GUI sprawdź rezerwację pulpitu, obce procesy i ŻYWY stan nagrywania. Nie wyprowadzaj braku nagrywania z pustych pólJSON. Nie restartujNVDA ani nie wysyłaj klawiszy w obce okno. Podgląd mowy musi być otwarty podczas gestów. Zachowaj surowy przyrost i osobno skutek działania, nie tylko informację„niepusta mowa”.

Próba mowy/menu dla tego przyrostu: `amc_pomoc/wx-full-profile-after421/menu-and-speech-after422/proba_koncowa.py` (kwit `proba-koncowa.json`). Mierzy tylko to, co zmienione: mowę po zmianie widoku, oba kopiowania z odczytem schowka i obejście menu. Po próbie zamyka własne okno, sprawdza procesy i przywraca schowek.

Mowa natywnej listy: dwie klasy objawów NADAL NIE SĄ naprawione — „poprzednia nazwa z nowym licznikiem” przy zmianie zbioru oraz wielokrotny odczyt tego samego wiersza. Naprawione jest tylko „nieznane” na liście bez nazwy (nakładka dostępności). Nie uznawaj tego za zrobione na podstawie testów jednostkowych: para kwitów z żywego NVDA to `amc_pomoc/wx-full-profile-after421/list-speech-after422/odbior-listy-FINAL.json` i `REPORT.md`. Przy zmianach w `gui.py` mierz ZAWSZE widok 2476 wierszy (Alt+2) — objawy mowy zależą od rozmiaru zbioru i na małych widokach nie wychodzą.

Próba widoków aktywności: `amc_pomoc/wx-full-profile-after421/activity-gui-after422/proba_koncowa_activity.py` (etapy A–C) oraz `dopiecie_de.py` (etapy D–F na tym samym żywym oknie). Nazwa gestu Backspace w mostku NVDA to `backspace`; `back` zwraca HTTP 500 i pierwszy przebieg na tym padł.

## Dane i silnik

- SQLite: `mode=ro`, dziennikWAL uwzględniany. Błąd odczytu nie przechodzi automatycznie na `immutable=1`.
- ID są napisami. Dostępność i członkostwo w Bibliotece pochodzą z danychAMC, nie z `Path.exists` na innym komputerze.
- Jeden właścicielC# ma zapisywać wspólne dane; Python nie uruchamia drugiego harmonogramu. Implementacja wszystkich mutacji pozostaje kolejnym etapem.
- Foldery:AMC_PL. Kolekcje alfabetyczne: właściwy osobny tryb tytułu iOrdinalIgnoreCase ścieżki. Zgoda jednego korpusu nie dowodzi równoważności dwóch komparatorów.
- ProtokółJSON-lines, identyfikatorynapisy, limit64KiB po stronie żądania, wsady dzielone po bajtach. Nowe tryby kluczy wymagają nowegoLiteHost; klient odmawia niezgodnego trybu starego hosta.
- Host wykorzystuje istniejące silnikiAMC, nie własny dekoderPython. Sprawdzenie Roslynem lub kluczy w osobnej sondzie nie zastępuje kompilacji i uruchomienia rzeczywistego hostaWindows.

## Testy kodu

```bash
cd /home/michal/projekty/amc-wx-full-profile/wxlite
python3 run_tests.py
```

Wąskie przebiegi wybiera się fragmentem nazwy modułu, np. `library_view_navigation`, `library_views_through_source`, `announcement_reaches_screen_reader`. Testy protokołu z rzeczywistymC# wymagają zbudowanych `AccessibleMediaController.LiteHost.ProtocolTests`; pominięć nie przedstawiaj jako zaliczenia.

Przed publikacją potrzebny jest osobny, pełny odbiór niezmienianej paczki. Ten dokument nie potwierdza publicznego wydania ani instalacji na komputerze Michała.

## Znane ograniczenia

Przy zmianie widoków **nadal** zdarzają się powtórzenia: po ogłoszeniu widoku bieżący element bywa odczytany drugi raz, a przy dużym skoku długości listy (1→2475) jeden odczyt niesie poprzednią nazwę z nowym licznikiem. Ubyła natomiast własna nadmiarowa zapowiedź wiersza i jedno z trzech powtórzeń na Ulubionych. Nieznany czas jest już pokazywany jako brak, nie `0:00`. Całość nie ma jeszcze wszystkich widoków, funkcji zapisu, usług i ustawień oryginału. Tempo i wybór silników mają osobne wcześniejsze kwity; obecny odbiór ich nie powtarza ani nie rozszerza.

Pełne dowody robocze: `amc_pomoc/wx-full-profile-after421/library-gui-after422/REPORT.md` i `parent-acceptance/`. Nie kopiuj prywatnych tytułów/profilu do repo ani paczki.
