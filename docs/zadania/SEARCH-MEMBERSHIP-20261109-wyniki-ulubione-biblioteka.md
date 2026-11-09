# SEARCH-MEMBERSHIP-20261109 — Biblioteka i Ulubione w wynikach wyszukiwania

- Status: **W trakcie — kod i testy celowane gotowe; bez instalacji i odbioru NVDA**
- Zgłoszenie: 9 listopada 2026 r.
- Produkt: AMC Test, bieżąca gałąź `codex/konfigurowalne-skroty-20261026`

## Zgłoszenie użytkownika

Na liście wyników `Ctrl+F` w TIDAL ponowne `Ctrl+Shift+L` nadal mówiło o
dodawaniu do Biblioteki. `Ctrl+Shift+U` dla Ulubionych raz podawało komunikat,
a raz nie dawało czytelnego wyniku. Użytkownik polecił sprawdzić także Spotify,
Radio, podcasty i pozostałe sesje. Publiczną instalację Spotify sprawdza
osobno; ten etap nie zmienia jej instalacji.

## Uzasadnienie i decyzje

- TIDAL i Spotify z katalogu wysyłały zawsze `add=True`; zapisany wynik
  wysyłał zawsze `add=False`. To uniemożliwiało odwracanie stanu bez ponownego
  wyszukania. Wyszukiwarka przekazuje teraz zamiar przełączenia, a host
  rozstrzyga kierunek według potwierdzonego członkostwa. Spotify sprawdza je
  przez API konta, TIDAL korzysta z potwierdzonego stanu kolekcji w hostcie.
- `Ctrl+Shift+U` zamykał okno wyników przed odpowiedzią. Oba skróty działają
  teraz w tym samym oknie; fokus pozostaje na wyniku. Nieudana lub
  niepotwierdzona operacja nie otrzymuje komunikatu sukcesu.
- W Radiu zapisany wynik przełącza Bibliotekę w hostcie. Wynik katalogowy
  rozpoznaje nowe dodanie, ponowne dodanie i usunięcie. Host oznacza
  przywrócenie wcześniej usuniętej stacji jako przywrócenie, także gdy
  aktualizuje jej nazwę.
- Podcast lub kanał z katalogu po ponownym skrócie usuwa zapisane źródło,
  a pojedynczy film YouTube usuwa tylko własny rekord w `Media internetowe`.
  Ulubione korzystają z wyniku przełączenia zwróconego przez host.
- Korekta z 9 listopada 2026 r. zastępuje opis w `SEARCH-001` z 21
  października, według którego zapisanej stacji Radia z wyników nie można
  usunąć i wyszukiwarka tylko mówi „Stacja jest już w Bibliotece”.

## Materiał do dokumentacji użytkownika

Na liście wyników `Ctrl+F` albo globalnego `Ctrl+Shift+F` zaznacz pozycję.
`Ctrl+Shift+L` dodaje ją do Biblioteki lub usuwa z niej po ponownym
naciśnięciu. `Ctrl+Shift+U` analogicznie zmienia Ulubione. Po potwierdzeniu
NVDA powinien usłyszeć „Dodano do Biblioteki: nazwa”, „Usunięto z Biblioteki:
nazwa”, „Dodano do ulubionych: nazwa” albo „Usunięto z ulubionych: nazwa”.
TIDAL i Spotify dodają nazwę usługi do komunikatu. Film YouTube wymienia
`Media internetowe`. Okno wyników pozostaje otwarte, a `Escape` je zamyka.

Zakres zależy od sesji i typu wyniku. W TIDAL Biblioteka obejmuje album,
wykonawcę i playlistę, a Ulubione utwór i wideo. W Spotify Biblioteka obejmuje
album, wykonawcę, podcast i odcinek, a Ulubione utwór i odcinek. W Radiu
działają oba skróty dla stacji; w Podcastach i YouTube dla źródeł i odcinków;
w Plikach lokalnych `Ctrl+Shift+U` zmienia Ulubione. W Radio Archiwiście
Ulubione dotyczą nagrań. Sesje bez tej możliwości podają krótki komunikat.

Lokalny plik już należy do Biblioteki: `Ctrl+Shift+L` w wynikach wyjaśnia
przejście `Ctrl+L` do Biblioteki i użycie `Delete`. Usuwa to wyłącznie wpis,
nie plik na dysku. Jest to świadomy wyjątek zachowujący dotychczasową
bezpieczną semantykę. WiiM, Sonos i pozostałe sesje bez własnego zapisu do
Biblioteki lub Ulubionych nie udają wykonania tej czynności.

## Zmiany źródłowe

- `wxlite/amc_wx_lite/gui.py`: routing skrótów w oknie, odpowiedzi i
  przełączanie członkostwa dla sesji.
- `src/AccessibleMediaController.LiteHost/LiteRadioLibraryMutation.cs`:
  potwierdzenie przywrócenia stacji.
- `src/AccessibleMediaController.LiteHost/LiteSpotifyCatalogCoordinator.cs`:
  komunikat „bez zmian” przy zerowej liczbie potwierdzeń.
- Celowane testy Python i hosta w istniejących plikach testowych.

## Testy i wynik

- 88 testów `wxlite/tests/test_search_gui_navigation.py`: zaliczone, bez okien.
- 105 testów `test_menu_exposes_existing_actions.py`, `test_shortcuts.py` i
  `test_command_palette.py`: zaliczone, bez okien.
- `dotnet build tests/AccessibleMediaController.LiteHost.ProtocolTests/AccessibleMediaController.LiteHost.ProtocolTests.csproj -c Release --no-restore --nologo`:
  sukces, zero ostrzeżeń i błędów. To kompilacja testów hosta, nie pakiet AMC.
- `dotnet .../amc_lite_protocol_tests.dll --spotify-catalog --radio-library`:
  oba zestawy zaliczone. Pierwsza próba Radia w piaskownicy nie mogła zapisać
  własnego pliku tymczasowego; powtórzenie z dostępem do katalogu projektu
  przeszło.
- Nie uruchamiano aplikacji, widocznego GUI ani NVDA. Nie sprawdzono na
  żywych kontach TIDAL i Spotify. Nie budowano, nie instalowano ani nie
  publikowano AMC Test lub AMC Public.

## Znane ograniczenia i odbiór

Odbiór z NVDA pozostaje potrzebny na prawdziwej liście wyników: początkowy
fokus, kolejne naciśnięcia obu skrótów, strzałki, `Escape`, ponowne otwarcie
i potwierdzenie stanu w Bibliotece oraz Ulubionych. Osobno sprawdzić żywe API
TIDAL i Spotify. Instalacja AMC Public jest odrębnym etapem po jej bieżącym
odbiorze przez użytkownika.

## Wersja i commit

Kod jest w bieżącym katalogu roboczym z istniejącymi wcześniej, niezwiązanymi
zmianami. Brak osobnego commitu i nowej wersji instalacji.
