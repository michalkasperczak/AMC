# Lokalne wyjścia audio — domyślne Windows i jawne wznowienie po utracie urządzenia

## Status i zlecenie

**BLOKADA lokalnego audio po końcowym cyklu; próbna integracja cofnięta.**
`3948aec` poprawia synchronizację testu i przechodzi w Debug także u rodzica,
lecz świeżo zbudowany wspólny Release `aef4626` ponownie daje HandlerTests exit 1
na wznowieniu. Revert `8d1da71` przywrócił wcześniejsze `src/tests`, zachowując
odebrany #26. Późniejszy `6c70141` dowiódł przyczyny: spóźnione PlaybackStarted
nadpisuje ochronną pauzę hosta. Rodzic porównał 749 źródeł sondy i niezależnie
odtworzył późny start — exit 1 na zachowanej asercji publicznego wznowienia.
[Odbiór diagnozy i minimalna poprawka](AUDIO-RECOVERY-20261120-late-start-odbior.md):
`deleg_de2324a4`, osobne worktree, bez ponownego scalania przed GREEN i odbiorem.
Polityka Spotify `7a26f3a` ma potwierdzony przez rodzica build i test Release offline,
ale pozostaje odrębnym kandydatem bez integracji.
[Odbiór Spotify i granice dowodu](AUDIO-RECOVERY-20261119-spotify-parent-check.md).
Bez instalacji, publikacji i GUI w tym etapie.

Michał 19.11.2026 polecił wdrożyć zmianę ze zgłoszenia Huberta #30/PR #31 we wszystkich odpowiednich sesjach, w tym TIDAL i Spotify. Następnie zgłosił dokładny scenariusz: po wyłączeniu zapamiętanego interfejsu Denon poprawna jest cisza wynikająca z opcji wstrzymania po utracie urządzenia, ale późniejsza Spacja lub Enter na tej samej stacji nie odzyskuje odtwarzania. Dopiero wybór innej stacji i powrót przywracają dźwięk na domyślnych głośnikach. Wspomniał także odciszanie sesji: należy prześledzić tę ścieżkę bez utożsamiania wyciszenia z pauzą.

Nowe zlecenie zastępuje wcześniejszy status „tylko odczyt, brak zgody na wdrożenie” w ocenie PR #31. Nie jest zgodą na zmianę ustawień Windows, restart AMC na głównym PC, uruchomienie GUI, logowanie do usług ani użycie płatnego API. PR nie jest przyjęty na podstawie deklaracji autora.

## Wymagane zachowanie

1. Dla wyboru „domyślne urządzenie systemowe” aktywny lokalny odtwarzacz podąża za rzeczywistą zmianą wyjścia Windows, także gdy poprzednie urządzenie nadal działa.
2. Jawnie wskazane i nadal dostępne wyjście nie podąża za zmianą domyślnego urządzenia. Nie kasować zapisanej preferencji tylko dlatego, że urządzenie chwilowo zniknęło.
3. Włączona opcja wstrzymania po utracie urządzenia pozostaje skuteczna: samo odłączenie nie może uruchomić dźwięku na głośnikach komputera. Odtwarzacz nie może na chwilę zagrać przed wstrzymaniem nowego wyjścia.
4. Późniejsza jawna próba odtwarzania tej samej pozycji (Spacja/Enter przez produkcyjne polecenie) sprawdza dostępność urządzenia i używa dostępnego domyślnego wyjścia, bez zmiany na inną stację. Nie może ponownie nałożyć pauzy ochronnej z poprzedniego zdarzenia.
5. Zachować tożsamość materiału, pozycję dla materiałów z osią czasu, pauzę użytkownika, głośność/wyciszenie, tempo, bufor i nagrywanie. Odciszenie nie jest bezwarunkowym poleceniem wznowienia rzeczywistej pauzy użytkownika. Dokładną ścieżkę po utracie wyjścia sprawdzić osobno.
6. Jeśli nie istnieje żadne dostępne wyjście albo podmiana nie powiedzie się, pozostawić bezpieczny stan i przekazać uczciwy polski komunikat, nie pozorny sukces.
7. Nazwa użytego urządzenia i ewentualny komunikat zastępczego wyjścia wynikają z rzeczywistego wyniku, nie ze stałej nazwy „Denon”.

## Zakres adapterów

- Radio: RadioMediaOutput, z zachowaniem Time Shift i nagrywania.
- Pliki, Podcasty/YouTube, Radio Archiwista, TIDAL: WindowsMediaOutput i produkcyjny routing hosta; sprawdzić listę wszystkich faktycznych konsumentów.
- Spotify Soloist i fallback Chrome: osobna diagnoza rzeczywistych adapterów, ten sam kontrakt, tylko możliwości potwierdzone w kodzie/protokole. Nie zmyślać poleceń Soloist i nie utożsamiać lokalnych endpointów Windows ze Spotify Connect.
- Zewnętrzne Sonos/WiiM/Frontier/Cast nie są lokalnymi wyjściami Windows. Nie przejmować ich na głośniki PC ani rozszerzać zadania o ich niezależne protokoły.

## Uzasadnienie i decyzje

Oddzielić automatyczne podążanie za wyjściem systemowym od ochronnej pauzy po utracie i od jawnego wznowienia. Sam watcher z PR #31 nie dowodzi rozwiązania zgłoszonej przez Michała martwej ścieżki Enter/Spacja. PR #29 również dotyka WindowsMediaOutput: przy odbiorze wymagane sprawdzenie zgodności i tożsamości pipeline/MediaItem; nie scalać drugiego PR w ramach tej diagnozy bez jego odbioru.

## Plan i kryteria odbioru

1. Dwie izolowane ścieżki Codex: wspólny tor/Radio i Spotify. Bez równoległej edycji kanonicznych źródeł lub wspólnych rejestrów przez wykonawców.
2. Odtworzyć defekt funkcjonalnym testem RED na rzeczywistym wznowieniu/aktywacji tej samej pozycji, z kontrolowaną dostępnością urządzeń. Sam test predykatu, fragmentów źródła lub wywołania mocka nie dowodzi podmiany toru.
3. Minimalna implementacja i GREEN dla wariantów: systemowe/stale wskazane/brakujące/brak wszystkich urządzeń, ochrona włączona/wyłączona, grające/pauza użytkownika/wyciszenie, Spacja/Enter/odciszenie, powrót urządzenia, szybkie zmiany, zmiana materiału/Stop/Dispose i spóźnione callbacki.
4. Użyć jednego wspólnego mechanizmu polityki tam, gdzie zachowanie rzeczywiście jest to samo; adaptery zachowują własny transport.
5. Zbudować rzeczywiste projekty-consumery i uruchomić ograniczone testy. Windows SDK: prywatny dotnet.exe w AppData/Local/AMC-BuildTools/dotnet; źródła do natywnego builda stage'ować na NTFS, nie argumenty POSIX do MSBuild. Nie omijać blokad Defendera.
6. Niezależny przegląd, integracja bez zgubienia równoległych zmian #26/Radio Archiwisty i odbioru #28, celowane potwierdzenie krytycznej granicy.
7. Oddzielnie uzgodniona próba fizycznego odłączenia/domyślnego wyjścia oraz komunikatów NVDA. Testy headless nie dowodzą działania Denona ani aktywnego Spotify. Bez kont nie udawać odbioru odsłuchowego tych usług.

## Wersja i commit

Start lokalny: branch codex/amc-test-public-20261109, HEAD 8bd3811. PR #31 head 16350cef8cbe9cfd958107bc9fccef1df042105e, baza 1723786c4557ef68bb3922189a028099ce96db31, OPEN, merged_at=null odczytane ponownie przy zleceniu. GitHub branch wskazuje 86add1c14e146d321cb9042403c1ec0122ca5a8d; po fetch sprawdzono, że jest przodkiem lokalnego HEAD (78 commitów tylko lokalnie, 0 tylko zdalnie). Nie ma nowych zdalnych przyrostów do integracji; nie resetowano ani nie pushowano pracy.

## Testy i wynik

### Wyniki wykonawców i dalsza bramka — 19.11.2026

- Wspólny tor: `0901f8dc02cd5310eb0a3bf6dfa7e881600496bd`.
  Ochronna pauza w WindowsMediaOutput i zdarzenie do hosta, watcher z #31,
  zachowanie zapisanej preferencji. Wykonawca raportuje dwa buildy (Windows
  SmokeTests i LiteHost) bez ostrzeżeń/błędów oraz GREEN publicznego `Play`
  adaptera po kontrolowanej utracie wyjścia i późniejszym `Play` tej samej pozycji.
  Nie uzyskał funkcjonalnego RED starej logiki; brak pełnego testu hostowego
  Space/Enter. To wyniki wykonawcy, nie końcowy werdykt odbioru.
- Spotify: `17dcde5ef2d2b53f2567f65d42908298cd6c4d39`.
  Potwierdzony RED/GREEN usunięcia ukrytego wyboru `null` po odrzuconym ręcznym
  wyjściu przy `restart=false`: 7 checks PASS; build LiteHost bez ostrzeżeń/błędów.
  Pełne recovery Chrome/Soloist nie było przedmiotem tej minimalnej poprawki.
- Rodzic przeczytał oba raporty i diffy; brak błędów `git diff --check`.
  Połączono kandydaty WYŁĄCZNIE w worktree
  `amc-output-acceptance-20261119`, branch `codex/amc-output-acceptance-20261119`,
  baza kanoniczna `a245d22`, zamrożony wspólny head
  `e758ae409689bb81f7aed94379bd1f03557ddcff` (lokalny przyrost `50bd9ff`).
  Kanoniczna produkcja pozostała nietknięta.

Niezależny Codex `sa-0-8d1a12d8`, delegacja `deleg_98a3c8c0`, odbiera ten
zamrożony kandydat. Ma sprawdzić krytyczną granicę hosta, pauzę użytkownika,
Stop/spóźniony callback i falsyfikować podejrzenie: przy ochronie wyłączonej
stare wyjście zgłasza `Stopped`, więc warunek `previous.PlaybackState == Playing`
może pozostawić nowe wyjście ciche. To hipoteza z odczytu, nie potwierdzony błąd.
Maksymalnie dwa cykle poprawki i celowanych pomiarów, nie pełny audyt GUI.

Osobny Codex `sa-1-b02c748f` tej samej delegacji uzupełnia politykę Chrome
w `amc-spotify-output-policy-20261119` od tego samego frozen head. Brak istniejącego
wejścia polityki nie jest sam w sobie zewnętrzną blokadą: ma wykorzystać wspólny
watcher i potwierdzone `setSinkId`, zachować ciszę ochronną i późniejszy jawny
Play, z funkcjonalnym testem offline. Brak komend wyboru/odświeżenia lokalnego
sinka w OBECNYM adapterze Soloist pozostaje osobnym ograniczeniem; nie jest
twierdzeniem o wszystkich wersjach upstream. Nie dodawać fikcyjnych komend.

Konkretne następne kroki: odebrać niezależny werdykt oraz przyrost polityki
Chrome, rozstrzygnąć wspólne punkty LiteEngineHandlers/watchera, potwierdzić
zintegrowaną krytyczną granicę i dopiero wtedy przyjąć do kanonicznej produkcji.
Nie integruje to #28/PR #29 ani nie zastępuje jego odrębnego odbioru.

## Aktualizacja po niezależnym odbiorze i pomiarze rodzica

Powyższe akapity opisują start odbioru; zastępuje je następujący wynik.
Niezależny Codex dostarczył `4a791c22980157a3a449b730a16ee813dd45e1eb`:
hipoteza ciszy przy ochronie wyłączonej została potwierdzona funkcjonalnym RED
oraz minimalnie naprawiona, z GREEN i testem rzeczywistego dispatchera.
Spotify Chrome dostarczono osobno jako `06bb4b0dbd8c5711105e914a5b79c856609d465f`.

Rodzic odczytał diffy i logi, porównał 747 źródeł stagingu z Git `4a791c2`
bez różnic, lecz własne uruchomienie HandlerTests zakończyło się exit 1:
`transport.pauseResume nie zwrócił wznowionego stanu po ochronnym fallbacku`.
Nie uznano tego za flaky ani za potwierdzony błąd aparatury. `deleg_96958dcb`
dostarczył `3948aec`: Debug rodzica PASS, lecz świeży wspólny Release znów FAIL.
Próbę integracji cofnięto; szczegóły są w parent-check. Po kolejnym poleceniu
Michała `deleg_12ac386e` diagnozuje przyczynę, nie wprowadza dalszej zgadywanej
poprawki. Codex Spotify zakończył osobny odbiór jako `7a26f3a`; rodzic zbudował
zamrożony commit w Release i potwierdził adapter/politykę offline exit 0.
Zdarzeń natywnego odłączenia ani pełnego wspólnego kandydata nie odebrano.

[Dokładne polecenie, wynik, logi, hipotezy i granice](AUDIO-RECOVERY-20261119-parent-check.md).
Do ich rozstrzygnięcia produkcja kanoniczna pozostaje bez tych przyrostów.

## Znane ograniczenia

Brak na Hermesie fizycznego Denona użytkownika; brak zgody na GUI/zmianę systemowego urządzenia i logowanie do Spotify. Rzeczywisty odsłuch i NVDA pozostają odrębną bramką. Aktualny wydany instalator nie został podmieniony.

## Materiał do dokumentacji użytkownika

Oczekiwane zachowanie po odebranej zmianie: przy wyborze wyjścia systemowego AMC podąża za Windows. Jeśli ustawiono wstrzymanie po utracie urządzenia, wyłączenie interfejsu pozostawia ciszę. Późniejsze jawne uruchomienie tej samej stacji lub materiału pozwala skorzystać z dostępnego wyjścia i informuje o nim bez konieczności wybierania innej pozycji. To kontrakt wdrożenia, a nie opis już wydanej wersji.
