# Hubert #30 / PR #31 — słuchawki i podążanie za domyślnym wyjściem Windows

**Status bieżący: WDROŻENIE ZLECONE; niżej historyczna ocena read-only.**

Późniejsza decyzja Michała z 19.11.2026: wdrożyć we wszystkich odpowiednich
sesjach, w tym TIDAL i Spotify, oraz sprawdzić brak wznowienia tej samej stacji
po utracie zapamiętanego wyjścia. Pauza ochronna po utracie ma pozostać;
Spacja/Enter mają umożliwiać świadome odzyskanie dźwięku bez zmiany stacji.
[Nadrzędny kontrakt i rzeczywisty stan prac](AUDIO-RECOVERY-20261119-kontrakt.md).
Rozpoczęto dwa izolowane zadania Codex: wspólny tor i Spotify. Nie jest to
odbiór PR, nowy instalator ani zgoda na GUI/zmianę urządzeń na głównym PC.

## Zlecenie i źródła — 19.11.2026

Michał zacytował wiadomość Huberta o nowym znalezisku #30 i polecił: „O co chodzi, sprawdź, opisz”. Zakres to objaśnienie zgłoszenia oraz sprawdzenie proponowanej zmiany, nie jej scalenie, instalacja, publikacja ani kontakt z autorem.

Odczytano świeżo pełne issue #30 i jego jedyny komentarz wskazujący PR #31, opis PR, pełny diff, komentarze i review oraz check-runs dokładnego HEAD. Issue i PR są otwarte; PR nie jest draftem i `merged_at=null`. Head `16350cef8cbe9cfd958107bc9fccef1df042105e`, baza `1723786c4557ef68bb3922189a028099ce96db31` na `codex/amc-protected-public-test-20261118-issue21-r2`. API: osiem zmienionych plików, zero check-runs; brak komentarzy/review. Nie oznacza to zielonego CI.

## Co zgłoszono

W AMC Test 18.11.2026, 03:37, przy ustawieniu domyślnego urządzenia systemowego:

- Po podłączeniu słuchawek Windows i NVDA przechodzą na słuchawki, a AMC nadal gra przez działające głośniki.
- Po odłączeniu urządzenia AMC odzyskuje dźwięk, lecz przy YouTube autor obserwuje około sześciu sekund ciszy, ponieważ następuje ponowne rozwiązanie adresu i otwarcie/przewinięcie materiału.

To nie jest problem wyszukiwania, historii czy list Radio Archiwisty. Dotyczy wspólnego lokalnego wyjścia audio Plików, Podcastów i YouTube, Radio Archiwisty i TIDAL oraz radia. Spotify jest poza zakresem tej poprawki: działa przez oddzielny proces/silnik.

## Co robi proponowany kod

Nowy `DefaultAudioOutputWatcher` nasłuchuje zmiany domyślnego wyjścia Render/Console Windows. Kod domyślnego wyjścia zapamiętuje faktyczny identyfikator urządzenia. LiteHost reaguje tylko dla sesji bez ręcznie wskazanego urządzenia; nie ma przejmować wybranego na stałe wyjścia.

Dla nadal aktywnego starego wyjścia host przenosi samą końcówkę audio. Dla utraty urządzenia `WindowsMediaOutput.TryBeginOutputDeviceRecovery` próbuje najpierw wymiany wyjścia, pozostawiając ten sam dekoder, bufor i tempo; pełny restart materiału zostaje awaryjnym fallbackiem. Radio wykorzystuje istniejący restart samego wyjścia, z zachowaniem jego reguł pauzy po odłączeniu.

Kierunek odpowiada opisanym objawom: podłączenie słuchawek nie musi zepsuć starego urządzenia, więc czekanie na błąd nie wystarcza; wymiana działającego wyjścia nie powinna wymagać ponownego pobierania adresu YouTube.

## Co jest dowodem, a co deklaracją autora

Hubert opisuje odsłuch YouTube, pauzy/wznowienia i radia po podmianie hosta. Raportuje 17 ms przy podłączeniu i 23 ms przy odłączeniu, build LiteHost/Windows Smoke bez ostrzeżeń oraz 37/37 zestawów protokołu. **Rodzic nie powtórzył tych pomiarów ani buildów; nie są niezależnym odbiorem.**

Istotne ograniczenie liczb: watcher celowo czeka na ustabilizowanie powiadomień przez `SettleDelay=500 ms`. Wartości 17/23 ms nie są zatem dowodem całkowitego czasu od fizycznego podłączenia do przełączenia dźwięku; dotyczą zgłoszonej operacji/logu autora.

Dodane testy obejmują czyste decyzje, rejestrację obserwatora i odczyt urządzenia bez odtwarzania oraz sprawdzanie tekstu/kolejności w źródłach. Nie wykonują funkcjonalnej podmiany grającego toru, realnego odłączenia ani wyścigu z pauzą/zmianą utworu. Przed akceptacją potrzebny jest proporcjonalny odbiór tych granic, a nie same asercje obecności fragmentów kodu.

Próba utworzenia izolowanego worktree przeglądu przekroczyła limit i nie udostępniła katalogu. Ref PR został pobrany; do odczytu zmian i watchera wykorzystano diff oraz `git show` niezmiennego SHA. Brak katalogu nie jest brakiem plików w PR. Nie zmieniono produkcyjnego checkoutu.

## Wniosek i następny krok

Wartościowa propozycja rozwiązania konkretnego problemu przełączania urządzeń; nie nowa wydana funkcja. PR pozostaje otwarty, nie scalony, bez niezależnego odbioru działania i bez zaobserwowanego CI. Jeśli Michał następnie zleci wdrożenie, wykonać ograniczony review/RED-GREEN dla zmiany wyjścia, pauzy, braku urządzenia i szybkich zmian, zachowania jawnie wybranego wyjścia oraz bezpieczeństwa nagrywania. Sprawdzić wspólną integrację z PR #29, bo oba dotyczą `WindowsMediaOutput`; nie scalać automatycznie na podstawie stwierdzenia autora „u mnie działa”.

Nie uruchamiano GUI, audio, instalatora, kont ani głównego PC. Nie publikowano komentarzy/review, nie zamykano zgłoszeń i nie wdrażano PR.

## Materiał do dokumentacji użytkownika

Planowany efekt: przy wyborze „domyślne urządzenie systemowe” AMC podąża za Windows po podłączeniu słuchawek. Po utracie wyjścia próbuje zachować odtwarzany materiał i miejsce, zamiast od nowa otwierać film. Ręcznie wskazane urządzenie pozostaje wyborem użytkownika. To opis propozycji, jeszcze nie możliwości dostępnej w nowym wydaniu.
