# Radio Archiwista — pełny kontrakt wspólnego odtwarzacza

**Status: WERYFIKACJA W TOKU; brak deklaracji pełnego odbioru i nowego wydania.**

## Zlecenie Michała — 19.11.2026

Sprawdzić, czy w Radio Archiwiście działają historia Ctrl+H, zakładki, skok procentowy oraz pozostałe zwykłe funkcje odtwarzacza. Jeśli czegoś brakuje, uzupełnić w istniejącej wspólnej implementacji. Nie tworzyć osobnego uproszczonego odtwarzacza dla tego źródła.

Nadrzędne rozdzielenie kontekstów: lista audycji z serwera jest zwykłą listą nazw plików — litery i cyfry służą do wyboru po nazwie. W widoku odtwarzacza cyfry zachowują istniejące znaczenie skoków procentowych. Nie wprowadzać specjalnego interpretowania godzin, aliasu 7 → 07 ani nowego skrótu. Nawigacja listy nie uruchamia odtwarzania. Szczegóły i korekta wcześniejszej diagnozy: [nawigacja i Ctrl+F](RARCH-HOURS-20261119-diagnoza.md).

## Stan wejściowy i dowody źródłowe

Kanoniczny checkout `/home/michal/projekty/AMC`, gałąź `codex/amc-test-public-20261109`, wejściowy HEAD `e09d705`; przed pracą czysty. Pozostali wykonawcy pracują wyłącznie w oddzielnych worktree #26 i nawigacji listy.

- `shortcuts.py`, `RADIOARCHIWISTA_SUPPORTED_ACTIONS`: historia, zakładki zwykłe i nazwane, ich nawigacja, skoki procentowe i wspólny transport są przewidziane w katalogu poleceń. Sama obecność na liście nie dowodzi wykonania.
- `gui.py`, `_show_playback_history_preview`: Radio Archiwista ma własną historię `playback_history_by_session`, przekazaną przez `streamer_history_rows` i `apply_service_history`, nie historię plików lokalnych. Sprawdzić także zapis, ponowne otwarcie i aktywację pozycji.
- `gui.py`, `_add_bookmark` i `_navigate_bookmark`: sesja Radio Archiwista jest dopuszczona. Istnieje test przejścia dodawania do wspólnego klienta z `session_id=radioarchiwista` i stabilnym `rarch:rec:*`.
- `gui.py`, `_seek_percent`: po znanym czasie trwania wywołuje `seek_to_position` z jawną sesją. Zweryfikować faktyczną drogę od klawisza i izolację kontekstu.
- `LiteTransportRouting.cs`: `radioarchiwista` jawnie trafia na silnik `files`, bez zmiany tożsamości sesji.
- `BookmarkStoreTests.cs` ma scenariusz zapisu/odczytu zakładki Radio Archiwisty i braku przecieku do `local`. Jest to istniejący test źródłowy, nie wynik nowego wykonania C#.

## Pierwszy rzeczywisty przebieg rodzica

`PYTHONPATH=wxlite:wxlite/tests uv run --offline --with pytest python -m pytest wxlite/tests/test_radioarchiwista_followup.py -q --basetemp /home/michal/.hermes/profiles/amc/cache/scratch/rarch-player-parent -p no:cacheprovider`

Wynik: **11 passed, 1 failed in 1.50s**, exit 1. Nie przedstawiać zestawu jako zielonego.

Niepowodzenie: `test_open_archive_day_list_relocalizes_through_saved_language_change` przy zmianie języka. `SimpleNamespace` testowego okna nie ma `_native_playback_snapshot`, wywoływanego przez odświeżanie wspólnych oznaczeń. Przed jakąkolwiek zmianą produkcji ustalić, czy to niekompletne wyposażenie atrapy po wcześniejszej integracji. Nie maskować awarii przez usunięcie asercji ani zmieniać zachowania aplikacji dla samej atrapy. Nie jest to na tym etapie dowód uszkodzonej historii/zakładek/procentów.

## Zakres ograniczonego odbioru

1. Historia: rzeczywista droga Ctrl+H, poprawna sesja i źródło danych, zapis tylko potwierdzonego odtwarzania, deduplikacja i trwałość, Enter ponownie otwiera właściwe nagranie, Escape przywraca poprzednią listę/wybór/filtr.
2. Zakładki: zwykłe i nazwane, poprzednia/następna, tylko nazwane, właściwe ID nagrania i sesja, zapis i ponowny odczyt, brak przecieku do Plików/Podcastów. Korzystać ze wspólnego store/transportu, nie tworzyć drugiej bazy.
3. Skoki: cyfry 0–9 w odtwarzaczu odpowiadają 0–90%, poprawna pozycja i `session_id`, obsługa braku długości bez fałszywego sukcesu. Na liście i w edytorze brak procentowego seek/play jako skutku wybierania nazwy.
4. Wspólne polecenia materiału skończonego: start/pauza/stop, względny i bezwzględny seek, początek/koniec, czas/pozycja/status, głośność/wyciszenie, tempo/pozostałe istniejące ustawienia audio, kolejka i pamięć pozycji. Ustalić na podstawie istniejącego kontraktu Plików/Podcastów, które akcje są wspólne. Nie dodawać kont, sprzętu, funkcji strumienia live ani rozdziałów dostawcy, których źródło nie oferuje.
5. Menu, paleta Ctrl+Shift+K, Ctrl+F1 i pełna pomoc, instrukcja oraz metadane: zgodność z rzeczywistą dostępnością i wykonaniem, bez martwych pozycji. Aktualizować wspólne źródło prawdy przy zmianie, nie kopiować ręcznie list skrótów.

Testować faktyczne produkcyjne handlery i producentów danych, nie tylko resolvery/stałe ani idealnie dopasowane atrapy. Gdy zachowanie działa, nie przepisywać produkcji. Dla potwierdzonego braku obowiązuje pionowe RED → minimalna implementacja → GREEN. Jedna proporcjonalna seria sąsiednich regresji, niezależny przegląd krytycznych granic przed integracją. Nie powtarzać pełnej suite ani dawnych odbiorów bez potrzeby.

## Bezpieczeństwo i koordynacja

Bez instalacji, zamykania/restartu AMC, GUI/NVDA/audio, dostępu do głównego PC, kont i poświadczeń. Nie publikować, pushować ani zamykać zgłoszeń. Nie zakłócać nagrywania. Praca wyłącznie przez Codex/openai-codex; brak zgody na osobno płatny klucz API.

Wykonawca Codex `sa-0-101dad7b`, delegacja `deleg_fd163488`, pracuje w
`/home/michal/.hermes/profiles/amc/cache/scratch/amc-radioarchiwista-player-20261119`,
gałąź `codex/amc-radioarchiwista-player-20261119`, baza
`7bebd00419c8febbce50b5f8fbeded9ed70df03d` (zapis niniejszego planu).
Nie edytuje wspólnych rejestrów ani pliku nawigacji godzin prowadzonego przez rodzica;
raport szczegółowy może dopisać w tym własnym dokumencie. Jeśli potrzebuje zmiany tej samej drogi klawisza co wykonawca listy, najpierw zgłasza dokładny punkt integracji zamiast nadpisywać jego pracę.

## Materiał do dokumentacji użytkownika

Radio Archiwista ma korzystać z normalnego odtwarzacza AMC: historia Ctrl+H, szybkie i nazwane zakładki, przewijanie oraz skoki procentowe w widoku odtwarzacza. Na liście nagrań wpisywane litery i cyfry wybierają nazwę; samo zaznaczenie nie odtwarza ani nie przewija. Dokładne pozostałe skróty brać ze wspólnego katalogu, a nie tworzyć odmienną wersję dla Archiwisty.

To zapis wymagań i odnalezionej implementacji, nie deklaracja nowego wydania lub żywego odbioru NVDA.

## Wersja i następny krok

Nowy artefakt/instalacja/publikacja: brak. Odebrać macierz funkcji, rzeczywiste wyniki, ewentualny czysty commit i niezależny przegląd; dopiero potem integracja z poprawką zwykłej nawigacji listy i wspólnym kandydatem Public/Test.
