# Audio: potwierdzony wyścig spóźnionego startu i dalsza minimalna poprawka

## Werdykt rodzica — 20.11.2026

**Przyczyna nieudanego wznowienia jest potwierdzona. Funkcja nadal nie jest
naprawiona ani zintegrowana; trwa osobny minimalny RED/GREEN.**

Codex `deleg_12ac386e` zakończył etap diagnostyczny commitem
`6c701411dfdcf923e64c68ff827104a0581c4641`. Zmieniono wyłącznie test hosta i
raport. Produkcji nie zmieniano. Rodzic przeczytał deltę, raport, zapisane
wyniki kontrolowanej sondy i odpowiednie produkcyjne callbacki.

## Przyczyna i granica wcześniejszego testu

Początkowy `IWavePlayer.Play` wykonuje się przed publikacją asynchronicznego
`WindowsMediaOutput.PlaybackStarted`. Poprzedni test czekał na wejście do Play,
nie na obsłużenie pełnego potwierdzenia startu przez host.

Kontrolowany przeplot:

1. Początkowy Play wchodzi w bramę testu, przed publikacją PlaybackStarted.
2. Utrata wyjścia tworzy fallback; host publikuje `audio.outputFallback`
   i ustawia `_filesPaused=true`. Nowe wyjście nie gra bez jawnego wznowienia.
3. Zwolniony początkowy Play publikuje spóźnione `playback.started`.
   Handler startu dla tego samego materiału bezwarunkowo ustawia
   `_filesPlaying=true`, `_filesPaused=false`.
4. Drugie publiczne `transport.pauseResume` odczytuje false i ponownie
   pauzuje zamiast wznowić tę samą pozycję na gotowym fallbacku.

To wada produkcyjnego kontraktu zdarzeń/stanu, nie wyłącznie testowego licznika
outputów. Samo oczekiwanie dłużej w teście ukryłoby ten przeplot zamiast go
naprawić. Sam filtr ID materiału nie odróżnia spóźnionego startu tej samej pozycji.

## Dowody wykonania

Wykonawca przeprowadził małą macierz źródeł `3948aec`/`aef4626` i konfiguracji
Debug/Release: wszystkie cztery zwykłe przebiegi przeszły. Nie unieważnia to
wcześniejszego RED rodzica. Wyklucza stałe przypisanie defektu kompilacji Release
lub integracji #26; sam PASS nie był tu dowodem naprawy.

Następnie ta sama nowa DLL sondy przeszła normalną kolejność (exit 0), a
wymuszona późna kolejność dała exit 1 na asercji wznowienia, bez Sleep i retry.
Rodzic przeczytał `probe.json`, porównał 749 plików `.cs/.csproj/.props/.targets`
stagingu z `6c70141` — zero braków i różnic bajtowych — i zweryfikował SHA DLL:

`ac9601725d720772820a394cb233efc7121d0ea6640481d998fee902f0e0f119`.

**Własne wykonanie rodzica, 20.11 około 00:06 CET:** rzeczywisty Windows
HandlerTests z `--late-initial-playback-started` zakończył się exit 1 po 1,753 s.
Dokładnie zachowana asercja:
`transport.pauseResume nie zwrócił wznowionego stanu po ochronnym fallbacku`,
`Program.cs:142`. To oczekiwany, niezależnie potwierdzony RED przed poprawką.
Nie wykonywano ponownie całej macierzy ani szeregu losowych powtórzeń.

Dowody rodzica: profil AMC, scratch `amc-output-late-start-parent-20261120/`,
`red.json`, raw stdout/stderr i `reproduce.py`.

Faktyczne położenie dowodów wykonawcy po przeniesieniu:
`cache/scratch/amc-output-investigation2-evidence-20261119/`, podkatalogi
`diagnostic-evidence-output-matrix-20261119/` i
`diagnostic-evidence-late-start-probe-20261119/`. Starszy raport w zamrożonym
commicie wskazuje dawne położenie wewnątrz worktree; nie używać go jako
aktualnego adresu plików dowodowych.

## Rozpoczęta minimalna poprawka

Codex `deleg_de2324a4`, `sa-0-fcabf63e`, pracuje w osobnym worktree
`amc-output-late-start-fix-20261120`, gałąź
`codex/amc-output-late-start-fix-20261120`, baza `6c70141`.

Zachować czerwony scenariusz i naprawić właściwy produkcyjny porządek/ważność
zdarzenia lub stan, nie wyciąć test. Zielony wynik wymagany dla zwykłej i późnej
kolejności w świeżym Release; publiczne wznowienie ma uruchomić ten sam materiał
bez trzeciego outputu/dekodera. Sąsiednio zachować pauzę użytkownika, ochronę
wyłączoną, Stop/nowy Play i spóźnione callbacki. Build rzeczywistych konsumentów
LiteHost i Windows. Maksymalnie dwa celowane cykle, bez retry-until-green,
architektonicznego refaktoru i dołączania innych zadań.

Nie scalać osobnej polityki Spotify `7a26f3a` w tej poprawce. Kanoniczny checkout
zachowuje cofniętą próbę integracji i odebrany #26 do czasu odebrania nowej delty.
Nie wykonywano GUI/NVDA, odsłuchu, fizycznego Denona, kont, sieci usług,
instalacji, push ani publikacji. Odrębne zadanie zapisu/wycinania nagrań pozostaje
następne i nie jest przez tę diagnozę rozwiązane.

## Materiał dla użytkownika

Znaleziono przyczynę sytuacji, w której po utracie wyjścia AMC pozostaje cichy,
lecz następna Spacja zachowuje się jak ponowna pauza: spóźnione potwierdzenie
startu nadpisuje ochronny stan. Trwa minimalna poprawka tego porządku. Nie jest
to jeszcze nowa wersja do instalacji ani potwierdzenie działania na sprzęcie.
