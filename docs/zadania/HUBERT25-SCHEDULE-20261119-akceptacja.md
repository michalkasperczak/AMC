# Hubert #25 — odbiór ograniczonego nadrabiania i harmonogramu

**Status: ODEBRANO ŹRÓDŁA I TESTY, ZINTEGROWANO.**

Kandydat końcowy: `dd01bcb9aaf927e1a19448c271c9de023cfc7541`.
Merge kanoniczny: `d113fae9895a214d8782a524cbfe797902be2dfa`.
Gałąź kanoniczna: `codex/amc-test-public-20261109`.

## Zakres i uzasadnienie

- Jeden startowy termin nadrabiania do 30 s; potem zwykły zapisany takt.
- Dobór porcji z jawnej kadencji i interwałów źródeł. Ustawiona liczba źródeł pozostaje twardym maksimum; źródła nigdy nieodświeżone mają pierwszeństwo. Test obejmuje 122 źródła bez pomijania części z nich.
- Budżet anulowania automatu 75 s, token w rzeczywistych klientach RSS/YouTube/Facebook i telemetria rozpoczętych oraz odroczonych celów. To budżet kooperacyjny, nie gwarancja ścisłego czasu ściennego całego przebiegu lub przerwania oczekiwania SQLite.
- Zachowane interwały użytkownika, wyłączenie automatu przez 0, blokady nagrywania i no-overlap. Ręczne odświeżanie nie otrzymuje limitu 75 s.
- Zachowana zgodność no-arg: wrapper wysyła `{}`, a rzeczywisty dispatcher stosuje dawny pełny limit. Dopiero jawna kadencja włącza dobór adaptacyjny. Jawne 0 podlega walidacji do 1 s, nie znaczy braku pola.
- Admission znacznika próby jest transakcyjne, z kontrolą tokenu po dostępie do DB i przed commitem. Odmowa nie zwiększa `requested`. Schemat bazy pozostaje 4. Anulowanie po punkcie przyjęcia jest anulowaniem in-flight, nie cofnięciem już przyjętej próby.

## Skład kandydata i przegląd

1. `c3e140401aad1eb08bfee8a597cc073abcb50ef8`: harmonogram; pierwszy niezależny review zgłosił niezgodność no-arg i granicę zapisu próby.
2. `5f0242ff5e20c0c51999438349c4688407f9bf6d`: korekta zgodności i admission. [Zachowany niezależny delta-review](HUBERT25-SCHEDULE-CORRECTION-20261119-review.md): **PRZYJĄĆ**, bez blokera produkcyjnego.
3. `dd01bcb9aaf927e1a19448c271c9de023cfc7541`: wyłącznie test i raport, bez zmiany produkcji. Kontrolowany timer zastępuje wyścig testowego deadline 25 ms z tempem CPU/JIT.

Rodzic przeczytał pełne raporty, delty i końcowy review. Po merge wszystkie 16 plików kandydata porównał bajtowo z `dd01bcb`; całość `src/`, `tests/`, `wxlite/` jest identyczna z zaakceptowanym kandydatem. Zachowano wcześniejsze kanoniczne aktualizacje dokumentacji.

## Testy i rzeczywiste wyniki

Wykonawcy: 62 Python PASS (`test_podcast_refresh.py`, `test_host_client.py`), Core `--youtube-subscription-sync-apply` 9 scenariuszy PASS i rzeczywisty LiteHost Release build bez błędów i ostrzeżeń. Po korekcie testu anulowania wykonano czysty rebuild Protocol: `Wszystkie zestawy przeszly (1)`.

Niezależne kontrole rodzica, bez mechanicznego powtarzania szerokiej suite:

- Windows HandlerTests na `5f0242f`: exit 0, `LiteHost automatic refresh dispatcher: OK`. Rzeczywisty dispatcher: brak pola daje 4 żądania, jawne 60 i 0 po 1.
- Wybrany pytest wrappera legacy/jawnej kadencji/ustawień: 1 PASS.
- Wcześniejsza selekcja `startup_catch_up or recording`: 5 PASS, 13 poza wyborem.
- Końcowy Protocol `--podcast-progress` na `dd01bcb`: exit 0, `Wszystkie zestawy przeszly (1)`.
- Finalny staging NTFS: 3049 śledzonych plików obecnych; 13 zmienionych plików kodu/testów od `ac203b6` bajtowo zgodnych z kandydatem. Produkcja nie zmieniła się od przejrzanego `5f0242f`.
- SHA-256 końcowej DLL Protocol: `2476e1a6d48c0d3c86109d32ad8ae1699f5454881ef9fd4169014401ab581a27`.
- Kwit rodzica: staging `hubert25-deadline-test-20261119/.amc-parent-acceptance/protocol-parent.json`.

Kontrola dyskryminująca wykonawcy: przekazanie `CancellationToken.None` zamiast tokenu budżetu do fetch daje RED, exit 1; po odtworzeniu źródła i usunięciu stagingowych bin/obj czysty rebuild daje GREEN. Test sprawdza rzeczywisty CTS, timer o due time 25 ms, rozpoczęty fetch, anulowanie, `requested=1`, `dueSources=2`, `deferredDueSources=1`, `budgetExpired=true`.

## Znane ograniczenia i uczciwe granice

Historyczny pojedynczy upadek testu 25 ms nie ma zachowanego kompletnego stacka w skróconym transkrypcie. Nie uznano go za niepowiązany na podstawie kolejnych PASS. Usunięto widoczną w źródle zależność od czasu CPU, a nowy kontrolowany test ma własny RED/GREEN; nie przypisuje się brakującemu stackowi zmyślonej diagnozy.

Test admission z `BEGIN EXCLUSIVE` sprawdza brak trwałej próby po anulowaniu i zwolnieniu blokady. Samo `Wait(25 ms)` nie dowodzi, że task zdążył wejść do SQLite. Niezależny review oznaczył to jako ograniczenie aparatury, nie bloker kodu. Nie deklarujemy deterministycznego dowodu momentu wejścia do DB, wspólnej atomowości tokenu/SQLite ani natychmiastowego anulowania SQLite.

Nie wykonano benchmarku biblioteki użytkownika, żywego YouTube, GUI/NVDA, audio, logowania kont, instalatora, instalacji ani publikacji. Nie zmieniono AMC na głównym komputerze. Nie zamknięto zgłoszeń ani PR.

## Materiał do dokumentacji użytkownika

Po uruchomieniu AMC włączone automatyczne odświeżanie wcześniej rozpoczyna nadrabianie należnych źródeł, ale nie zmienia zapisanych interwałów. W trakcie nagrywania zachowuje dotychczasowe blokady. Ustawienie taktu 0 nadal pozostawia odświeżanie ręczne F5/Ctrl+F5. Nie dodano skrótów, kontrolek ani nowych komunikatów NVDA. Jest to opis zaakceptowanych źródeł, nie już dostępnego wydania.

## Następny krok

#25 dane/trwałość i harmonogram są zakończone na poziomie źródeł. Rozpocząć #26/PR #27: rozdziały YouTube, retry po awarii zamiast cache „brak”, limit stdout/stderr podczas odczytu i rzeczywisty proces-fixture. Potem #28/PR #29. Wspólny odbiór wydania i jego odrębne bramki pozostają przed nami.
