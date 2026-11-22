# AMC po alpha.512 — wspólna kolejka Codexa i Hermesa

Stan: 22.11.2026. Punkt odniesienia: wydane Test i Public alpha.512, commit `d360cd89f622b90ae351f1ed876904b38856b625`. Ten plik porządkuje pracę, nie oznacza wykonania pozycji. Po każdym etapie wpisujemy do osobnego pliku zadania bazę, zmienione pliki, commit, komendy, prawdziwe wyniki testów, ograniczenia i jeden następny krok; tutaj zmieniamy status.

Źródło listy: przekazany przez Michała `AMC-zadania-po-alpha512-dla-Codexa.md` z 22.11.2026, zweryfikowany z rejestrem `docs/zadania/README.md` z alpha.512. Lista z przekazania jest materiałem do ustalenia kolejności; nie zastępuje plików poszczególnych zadań i nie rozpoczyna wszystkich zmian jednocześnie.

## Zasada pracy hybrydowej

Kanoniczny katalog przekazania: `D:\Projekty Codex\AMC - integracja wxPython\docs\zadania`. Przed rozpoczęciem etapu czytamy jego plik zadania z aktualnej bazy, sprawdzamy już zapisane wyniki na D i stan źródeł. Nową zmianę trzymamy w odrębnym worktree, gdy istnieją inne prace lub działająca instalacja. Po testach kopiujemy aktualny raport na D, podajemy gałąź i commit, aktualizujemy ten rejestr. Hermes i Codex nie uznają wiadomości w czacie za jedyne źródło stanu.

W tej kolejce **kod, testy, kompilacja, instalacja, publikacja i odbiór NVDA to odrębne stany**. Nie pracujemy na oryginalnych nagraniach. Nie uruchamiamy widocznych prób GUI/NVDA bez uzgodnienia ich wpływu na bieżącą pracę komputera. Nie przenosimy mechanicznie cudzych PR na alpha.512.

## Korekta użytkownika z 22.11.2026

Michał zastąpił wcześniejszą kolejność: incydent wycinania nagrań był jednorazowy, a Apple Music może wymagać dłuższej pracy, więc oba zadania są **odłożone na później**. Pierwszeństwo mają YouTube, Radio Archiwista, pozostałe problemy Huberta i kolejki. Oznaczanie aktualnie odtwarzanego materiału jest już zrobione w uzgodnionym zakresie; nie otwierać go ponownie bez konkretnego brakującego przypadku. Przed korektą Codex tylko przeczytał pliki i kod zadania nagrań; nie wykonał testów ani nie zmienił źródeł lub nagrań.

## Pilna korekta użytkownika z 22.11.2026 — transmisja na żywo

Michał zgłosił nieudaną próbę zaplanowanego nagrania transmisji „Hermanice”/Dominikanie po 16:00 i błąd po ręcznym Ctrl+R po podmianie linku w Ulubionych. To **nowe zadanie dotyczące startu nagrania i aktualnego adresu stacji**, odrębne od odłożonego wycinania nagrań Dwójki. Ma pierwszeństwo przed dalszym `RARCH-004`. **Korekta z 22.11.2026:** zdarzenie wystąpiło na głównym komputerze, nie na Hermesie; zastępuje wcześniejsze błędne przypisanie. Lokalny dziennik i historię zbadano odczytowo; szczegóły w [raporcie](RADIO-LIVE-REC-20261122-zmiana-adresu-i-start.md). Gałąź `codex/live-radio-recording-20261122`, commit `f21fb18d`, 136 powiązanych testów zaliczonych i LiteHost zbudowany bez błędów. Brak instalacji, publikacji i próby z prawdziwą transmisją. Uruchomione w osobnym worktree `RARCH-004` ma tylko cztery czerwone testy rozpoznawcze, bez zmian źródeł; wrócić po tej poprawce.

Michał ustalił także, że **po faktycznej aktualizacji** należy niezwłocznie poinformować Huberta o poprawionej sprawie przez dostępny kanał. GitHub jest dostępny w tej sesji; dopóki zmiany pozostają tylko kandydatami kodu, nie wysyłamy informacji sugerującej wydanie. To ustalenie zastępuje wcześniejszy brak osobnego kroku kontaktu po aktualizacji; nie zmienia stanów testu, instalacji ani wydania.

## Postęp integracji 22.11.2026

Kandydat `codex/alpha512-update-candidate-20261122`, początkowo HEAD `cadc8ead`, łączy poprawkę nagrania, Huberta #32, Huberta #25, Delete kolejki Radio Archiwisty i poprawkę odświeżania kanału Hermanic z wyjaśnieniem Ctrl+R podczas planu. [Raport integracji](ALPHA512-INTEGRATION-20261122.md) rozróżnia testy celowane, kompilację i **30 niepowodzeń pełnego zestawu** obecnych także na gałęzi porównawczej. [Raport YouTube i Ctrl+R](YOUTUBE-RADIO-HERMANICE-20261122.md) zawiera lokalną diagnozę, wynik odczytu publicznych metadanych kanału i ograniczenia. Ten kod nie został zainstalowany ani wydany; Hubert nie otrzymał jeszcze wiadomości sugerującej wydanie.

**Uzupełnienie 22.11.2026:** do kandydata dołączono istniejące poprawki presetów TIDAL i bezpośredniego przypisywania, commity `a01fbfe2` i `1d5e68b2`; HEAD po tym etapie wynosił `1d5e68b2`. [Raport presetów](PRESET-20261122-tidal-i-bezposrednie-przypisanie.md) pochodzi z kanonicznej gałęzi. Po integracji testy presetów, skrótów, menu i tłumaczeń dały 228 zaliczonych, 1 pominięty i 2 znane niepowodzenia testów kompletności tłumaczeń. Instalacja, NVDA i publikacja nadal otwarte.

**Uzupełnienie etapu Radio Archiwisty 22.11.2026:** [ulubione całych stacji](RARCH-004-ulubione-stacji-i-nazwy-lokalne.md) dołączono do kandydata w commicie `563aa802`; HEAD po tym etapie wynosił `563aa802`. Połączone testy powiązanych funkcji: 242 zaliczone. Lokalna nazwa F2 była kolejnym otwartym etapem tego zadania.

**Uzupełnienie F2 z 22.11.2026:** lokalne nazwy stacji i nagrań Radio Archiwisty dołączono w commicie `25fe2834`; HEAD po tym etapie wynosił `25fe2834`. Zastępuje wcześniejszy status „F2 otwarte”. Powiązane testy po integracji: 267 zaliczonych i 2 podtesty zaliczone. [Raport RARCH-004](RARCH-004-ulubione-stacji-i-nazwy-lokalne.md) odróżnia kod od jeszcze niewykonanego odbioru NVDA.

**Uzupełnienie Huberta #22 z 22.11.2026:** [pomiar i poprawka Folderów](HUBERT-22-20261122-foldery.md) są w commicie `12dd0c50`; obecny HEAD to `12dd0c50`. Zastępuje wcześniejszy status „#22 do odtworzenia”. Syntetyczny pomiar 60 000 pozycji: korzeń 4,986 → 0,249 s, folder 8,766 → 0,544 s, zgodne skróty wyników; 114 testów powiązanych zaliczonych, 49 pominiętych, 2 podtesty zaliczone. Odbiór na rzeczywistym profilu i NVDA pozostaje otwarty.

## Kolejność po korekcie

1. **#32, duża Biblioteka lokalna — kod zakończony, odbiór otwarty.** Osobna gałąź `codex/hubert-32-large-library-20261122`, commit `bf8992b4` na alpha.512; 207 testów zaliczonych, 24 pominięte. Raport: [HUBERT-32-20261122-duza-biblioteka.md](HUBERT-32-20261122-duza-biblioteka.md). Następnie próba prawdziwego hosta i NVDA na izolowanej dużej Bibliotece, później decyzja wydania. Wyszukiwanie po nazwach folderów pozostaje osobno.
2. **YouTube #25, RSS-first — kandydat kodu i testów gotowy; odbiór na żywych kanałach otwarty.** Gałąź `codex/hubert-25-rss-first-20261122`, commit `616a4c93`: automat sprawdza RSS przed yt-dlp, gdy zna kanał; znany feed pomija pełne pobranie, nowy ID/brak RSS/stary pełny odczyt uruchamiają dotychczasową ścieżkę. Shorts nie wywołują zbędnego skanu transmisji. Dwa zestawy Protocol i celowany Windows Smoke przeszły, rzeczywisty LiteHost zbudował się bez błędów. [Raport](HUBERT25-RSS-FIRST-20261122.md). Nie zmierzono prawdziwych 122 kanałów, nie instalowano.
3. **Radio Archiwista i spójność poleceń — Delete kolejki, ulubione stacji i lokalne nazwy F2 gotowe w kodzie; odbiór otwarty.** Gałąź `codex/radioarchiwista-queue-delete-20261122`, commit `095fe4fd`: po usunięciu B wybiera C, przy filtrze widocznego sąsiada; 58 celowanych testów zaliczonych, odbiór NVDA otwarty. [Raport](RARCH-QUEUE-DELETE-20261122.md). `RARCH-004`: [ulubione stacji i F2](RARCH-004-ulubione-stacji-i-nazwy-lokalne.md) w commitach `563aa802` i `25fe2834`; odbiór NVDA otwarty. `RARCH-HOURS-20261119-diagnoza.md`: najpierw rzeczywista próba natywnej nawigacji literami/cyframi. Sprawdzić istniejące zakładki czasu, nie implementować ich ponownie. `CAST-001-google-cast.md`: Cast i AirPlay od Archiwisty, z jednoczesną aktualizacją menu, palety, pomocy i testów parytetu. Szybkie presety są już w kandydacie.
4. **#22, wolne Foldery Biblioteki — poprawka i pomiar gotowe w kodzie.** Commit `12dd0c50`, [raport](HUBERT-22-20261122-foldery.md). Wyłącznie widok Foldery czyta rekordy w kolejności `rowid`, pozostałe wywołania zachowują sortowanie tytułów. Na syntetycznej dużej bazie potwierdzono poprawę i zgodność wyników. Odbiór na rzeczywistym profilu i NVDA pozostaje otwarty. Powiązane #5, wolna natywna lista, pozostaje odrębną pracą.
5. **Wyjścia audio i wznowienie — jeden blok integracyjny.** Przeczytać `AUDIO-RECOVERY-20261119-kontrakt.md`, `AUDIO-RECOVERY-20261120-late-start-odbior.md`, `AUDIO-RECOVERY-20261119-spotify-parent-check.md`, #30/PR #31 i #28/PR #29. PR #29 i #31 dotykają `WindowsMediaOutput.cs`. Najpierw rozstrzygnąć spóźniony start, pauzę ochronną i jawne pierwsze wznowienie; potem podążanie za domyślnym wyjściem w właściwych sesjach. Próby sprzętowe, TIDAL i Spotify osobno od testów atrap.
6. **Kolejki i TIDAL — poprawki połączone, odbiór otwarty.** [Delete Podcastów i zapisanych filmów YouTube](QUEUE-DELETE-20261122-podcast-youtube.md) jest kandydatem kodu `76b00d66`; 6 nowych i 26 powiązanych testów przeszło. Wskazanego w notatce commitu `948c62ff` nie ma w dostępnych repozytoriach; poprawkę wykonano na obecnym kodzie. [Czas Kolejki, Enter i mowa TIDAL](TIDAL-QUEUE-ENTER-20261122.md) są kandydatem w commitach `b6cc7c0b` i `0ed160af`; testy celowane przeszły. [Filtr i Delete w Spotify, TIDAL i Plikach](QUEUE-FILTER-DELETE-20261122.md) to commit `5c249c68`, 25 testów przeszło. Wcześniej wskazany plik `TIDAL-20261120-kolejka-enter-zapowiedzi.md` nie jest dostępny w kanonicznym katalogu D:. Próba na kontach, hostem i NVDA pozostaje otwarta.
7. **Nagrania Dwójki — później.** Pliki `AUDIO-EDIT-20261119-znikajace-nagrania.md` i `AUDIO-EDIT-20261120-directcutrs-wzorzec.md` przeczytane. Przy powrocie odtworzyć dwa cięcia na prywatnych kopiach, nigdy na oryginałach Michała.
8. **Apple Music Test — później.** `AM-008-utrata-sesji-i-ponowne-logowanie.md`: osobno pusty wynik, odmowa dostępu i okno logowania. Public nadal bez Apple Music; odtwarzanie w AMC to inne zadanie.

## Odłożone i już wykonane

- HEOS: wyłącznie osobny, późniejszy rekonesans na jednym urządzeniu; brak implementacji.
- Odtwarzanie Apple Music w samym AMC: osobno od punktu 8, po uzgodnionej próbie interaktywnej.
- Podejrzenie ucięcia początku utworu: wrócić tylko przy powtórzeniu objawu lub odsłuchu; pomiar 2,8 s nie wykazał ucięcia przed wyjściem Windows.
- Rozdziały YouTube, dzień tygodnia w Radio Archiwiście oraz oznaczanie odtwarzanego materiału w potwierdzonym zakresie są już w alpha.512; nie wdrażać drugi raz tylko dlatego, że stare PR lub zgłoszenie pozostają widoczne.

## Najbliższy krok

Kandydat jest połączony w kodzie, łącznie z szybkimi presetami, Radio Archiwistą, Folderami Biblioteki #22, Delete Podcastów/YouTube, TIDAL i filtrowanymi kolejkami. Następny krok: przygotować bezpieczną próbę wydania. Przed wydaniem wyjaśnić 30 niepowodzeń pełnego zestawu, sprawdzić brak aktywnych nagrań oraz wykonać odbiór na izolowanym profilu i z NVDA. #25 czeka na pomiar rzeczywistych kanałów, Radio Archiwista Delete na odbiór NVDA, #32 na odbiór dużej Biblioteki. Po rzeczywistym wydaniu poprawki poinformować Huberta w odpowiednim zgłoszeniu GitHub.
