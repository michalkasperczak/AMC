# Radio Archiwista — Ctrl+F i zwykła nawigacja listy nazw

**Status: Ctrl+F ponownie sprawdzone w źródłach; przyczyna zgłoszonej nawigacji natywnej listy nadal w weryfikacji.**

## Nadrzędne korekty Michała — 19.11.2026

Lista audycji to zwykła lista plików pobieranych z serwera, których nazwy mogą zaczynać się od cyfr. Ma działać zwykły przyrostowy wybór po nazwie literami i cyframi, a nie osobny selektor godzin. Sam wybór pozycji nie uruchamia odtwarzania.

To zastępuje wcześniejszą interpretację rodzica i pierwotne kryterium aliasu 7 → 07:00. Nie wprowadzać specjalnego bufora godzin ani zmieniać nazw 07:00 na 7:00 pod tę hipotezę. Porównać rzeczywisty routing i tekst natywnej kontrolki ze zwykłą listą plików. Bez reprodukcji nie przepisywać działających dróg.

Michał podał komunikat aktualizatora: AMC Test 18.11.2026, 03:37. Świeży odczyt GitHub potwierdził to jako najnowsze opublikowane wydanie kanału Test (`test-2026.11.18.0337`). Poprawka Ctrl+F nie jest jeszcze wydana; nie sugerować, że użytkownik nie zainstalował dostępnej aktualizacji. Jest to zgłoszona przez niego wersja, nie nowy odczyt instalacji przez agenta.

Michał następnie rozszerzył odbiór o historię Ctrl+H, zakładki, procenty i zwykłe funkcje odtwarzacza: [oddzielny kontrakt i wyniki](RARCH-PLAYER-20261119-kontrakt.md). Na liście cyfry wybierają nazwę; w odtwarzaczu pozostają skokami procentowymi. Nie mieszać tych kontekstów.

## Zlecenie i ograniczenia

Sprawdzić oddzielnie zgłoszenie nieobsługiwanego Ctrl+F i wybór nazw na liście wybranego dnia/stacji. Nie dodawać wyszukiwania treści audycji/transkrypcji ani pobierania całego archiwum. Bez instalacji, restartu/zamykania AMC, GUI/NVDA/audio, zmian głównego PC i dostępu do kont/cookies/sekretów. Praca nie zastępuje uzgodnionej kolejności #26/#28.

## Ctrl+F — sprawdzony stan źródeł

Poprawka `bb3fdcd3284aa94847b6b45e6dc6215aa89d29e7` jest przodkiem kanonicznej bazy `4b5a4a4a07968eae69ba7cbc602d30bbaa07f07e` (`git merge-base --is-ancestor`, exit 0). Przeczytano pełny [poprzedni kontrakt](SEARCH-SCOPE-20261118-radioarchiwista-i-uslugi.md).

Droga produkcyjna: hook listy → resolver SEARCH_CURRENT → dopuszczenie sesji → dispatcher wyszukiwania. Zimny profil pobiera indeks stacji; Enter w wyniku wskazuje stację, dalej wybiera się rok/miesiąc/dzień/nagranie. Nie jest to wyszukiwanie treści audycji.

`PYTHONPATH=wxlite:wxlite/tests uv run --offline --with pytest python -m pytest wxlite/tests/test_radioarchiwista_search_scope.py -q --basetemp /home/michal/.hermes/profiles/amc/cache/scratch/radioarchiwista-followup-parent-search -p no:cacheprovider` — **10 passed in 0.94s**, exit 0. Bez GUI/NVDA i nowego artefaktu. Nie odtworzono komunikatu o nieobsługiwanym Ctrl+F na tej bazie; brak podstaw do kolejnej zmiany tej funkcji.

## Wcześniejsza obserwacja helpera — ograniczony zakres dowodu

Producent `recording_rows` dla jawnych fikcyjnych nagrań godzin 0/7/17 wygenerował `['00:00', '07:00', '17:00']`. `ListModel.find_prefix('7')` zwrócił -1, a `find_prefix('07')` 1; obie asercje wykonano offline, exit 0.

To wyłącznie obserwacja literalnego prefiksu na helperze, który ma testowych callerów. **Nie jest reprodukcją przyczyny niedziałania liter/cyfr w natywnym Windows ListCtrl ani dowodem potrzeby aliasu godzinowego.** Poprzednie twierdzenie, że wyjaśnia zgłoszoną przyczynę, zostało wycofane. Sama zmiana helpera nie dowodzi naprawy aplikacji.

## Wykonanie i aktualne kryteria

Wykonawca Codex: `sa-0-eb5bbe09`, delegacja `deleg_d9b61458`, worktree `amc-radioarchiwista-hours-20261119`, gałąź `codex/amc-radioarchiwista-hours-20261119`, baza `4b5a4a4a07968eae69ba7cbc602d30bbaa07f07e`. Przekazano nadrzędną korektę kierunku.

Porównać faktyczne event gates, produkcję nazw i dekoracji, native prefix matching z Plikami oraz wpływ widocznego filtra. Sprawdzić litery i cyfry przez rzeczywistą drogę klawisza. Dopasowywać literalny początek rzeczywistej nazwy, nie interpretowaną godzinę/datę. Wybór tylko w widocznej projekcji, bez play/seek i bez podwójnej reakcji hook/KEY_DOWN. Zachować menu/dialogi, edytory, modyfikatory, skróty NVDA i procenty w odtwarzaczu.

W razie potwierdzonej usterki: funkcjonalny pionowy RED/GREEN, minimalna poprawka wspólnego mechanizmu, ograniczone regresje. Jeżeli headless nie może wykazać przyczyny zachowania natywnej kontrolki, zapisać dokładną granicę oraz potrzebę osobno uzgodnionej próby Windows zamiast implementować spekulację. Wycofać wyłącznie własną nieadekwatną logikę godzinową, jeśli już powstała; nie naruszać innych zmian.

Raport wykonawcy: `RARCH-HOURS-20261119-wdrozenie.md`. Rodzic odbiera konkretny diff i krytyczną granicę przed integracją. Brak GUI nie jest zaliczeniem żywego NVDA.

## Materiał do dokumentacji użytkownika

Ctrl+F wyszukuje stacje. Na liście audycji wybiera się nazwy jak na zwykłej liście plików, literami i cyframi. Enter pozostaje oddzielnym uruchomieniem nagrania. W widoku odtwarzacza cyfry nadal służą skokom procentowym. Nie reklamować specjalnego wybierania godziny 7 → 07.

## Wersja i następny krok

Brak nowego artefaktu i zmian głównego PC. Odebrać wyniki wykonawcy zgodne z korektą użytkownika i połączyć je z [weryfikacją odtwarzacza](RARCH-PLAYER-20261119-kontrakt.md). Nie uznawać diagnozy ani delegacji za ukończoną poprawkę.
