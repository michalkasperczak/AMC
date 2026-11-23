# CAST-002 — dostępność Google Cast i AirPlay w sesjach AMC

Status: **Wydane w Test i Public alpha.519; instalacja użytkownika, odsłuch sprzętowy i nowy odbiór NVDA pozostają otwarte.**

## Zlecenie i nadrzędna korekta — 23.11.2026

**Późniejsza decyzja 23.11.2026:** użytkownik zlecił teraz aktualizację obu profili Test i Public, jeśli rozszerzenie nie jest jeszcze wydane. Zastępuje to poniższe odłożenie pakowania. Integrację na wydanej bazie, wyniki oraz rzeczywistą publikację opisuje [RELEASE-ALPHA519](RELEASE-ALPHA519-20261123-Cast-AirPlay.md). Niezaliczone próby sprzętowe/NVDA nie stają się przez tę decyzję zaliczone.

Użytkownik poprosił o sprawdzenie Shift+C, wyjaśnienie miejsca AirPlay, dodanie obu funkcji w Radio Archiwiście i rozszerzenie na pozostałe sesje, w tym TIDAL bez wymogu TIDAL Connect. Następnie potwierdził, że bieżąca aktualizacja już się buduje: kontynuować ten przyrost do **kolejnej wersji**, obok dokończonego Apple Music i następnego etapu HEOS. Nie zmieniać zakresu trwającego wydania, nie uruchamiać drugiego pakowania ani instalacji. Apple Music i HEOS są osobnymi pracami, nie zakresem tego zadania.

Repozytorium: `D:\Projekty Codex\AMC - integracja wxPython`; zastana gałąź `codex/amc-current-d-20261123`, HEAD `3221ff09`. Zachowano zastane zmiany innych zadań. Bez przełączania gałęzi i bez nadpisywania bieżącej instalacji.

## Stwierdzony stan

- Shift+C, menu i możliwości były ograniczone do Plików, Radia i Podcastów.
- AirPlay znajdował się pod Shift+M jako **dodatkowe** wyjście, obok Cast i drugiego lokalnego urządzenia. Radio Archiwista był zablokowany w skrótach, menu i routerach mimo używania tego samego dekodera co Podcasty.
- `media.play` było w routerach przypisywane zawsze do Podcastów, z pominięciem jawnej sesji Radio Archiwisty. Samo dodanie pozycji menu powodowałoby błędne kierowanie kolejnego materiału.
- Nowszy natywny TIDAL ma lokalny, prywatny proxy strumienia FLAC/DASH. To zastępuje historyczne ogólne założenie, że każdy TIDAL w AMC wymaga zewnętrznego odtwarzacza. Nie oznacza to, że każdy strumień albo każda sesja ma źródło dostępne urządzeniu.

## Projekt i zmiany

- Jeden wspólny kontrakt sesji i operacji `network_audio.py` dla Cast, AirPlay i drugiego wyjścia lokalnego. Uwzględnia jawną sesję `media.play` oraz natywny TIDAL.
- Pliki, Radio, Podcasty i Radio Archiwista: źródła bezpośrednie. Shift+C przełącza do Cast; Shift+M wybiera dodatkowe wyjście lokalne, Cast albo AirPlay. Nazwa wspólnego polecenia zawiera teraz AirPlay, aby dało się je odnaleźć w menu i palecie.
- Natywny TIDAL: Cast dla dostępnego nieszyfrowanego BTS FLAC/AAC/MP3 przez nowy most `cast_tidal.py`, bez TIDAL Connect. Typ audio pochodzi z kontrolowanej odpowiedzi lokalnego proxy. Oryginalny proxy pozostaje wyłącznie na 127.0.0.1. Odbiornik dostaje nowy losowy adres ograniczony do jego IP; nie dostaje adresu źródłowego ani danych logowania. Obsługiwane są Range/HEAD i sprzątanie po zamknięciu.
- Cast nie otrzymuje manifestu DASH/MPD ani oryginalnego tokenu lokalnego proxy. Ten typ strumienia jest wyłączony dla Cast. AirPlay i drugie lokalne wyjście mogą używać lokalnego źródła natywnego TIDAL; nie jest przechwytywany cały dźwięk Windows ani NVDA.
- Dostępność TIDAL zależy od bieżącego, pasującego identyfikatora utworu i prywatnego strumienia, a nie tylko od nazwy sesji. Powrót Cast do TIDAL zachowuje pozycję dzięki opcjonalnemu `positionSeconds` w kontrakcie hosta. Wymaga zgodnej nowej wersji klienta Python i hosta C#; nie wdrażać samych plików frontendu do starego hosta.
- AirPlay używa migawki wykrytych urządzeń przy ponownym otwarciu i świeżego odczytu przy zatwierdzeniu. Odświeżenie pozostaje osobnym działaniem.
- Akceptacja kolejnego utworu TIDAL zachowuje kontrakt `ok=True` i zgodny identyfikator. Prywatne źródło jest pamiętane dopiero po takiej odpowiedzi; publiczny status natywnego TIDAL nadal nie zawiera adresu proxy. Wewnętrzna migawka obsługuje także etap przygotowywania utworu.
- Nowy materiał natywny we wspólnym dekoderze wyłącza dodatkowe wyjście poprzedniej sesji, zamiast przekierowywać je na inną sesję. Radio zachowuje osobny tor. Zapamiętany materiał Radio Archiwisty nie zatrzymuje obcego właściciela dekodera podczas wyboru Cast.
- Lista pod Shift+M podaje powód braku AirPlay: brak składnika, brak FFmpeg, brak wykrytych odbiorników albo nieudane wykrywanie. Awaria listy lokalnych wyjść nie ukrywa AirPlay. Zewnętrzne błędy uruchomienia AirPlay mają zamierzony komunikat z nazwą odbiornika, bez protokołu i prywatnego adresu.
- Wynik wykrywania jest odrzucany po zmianie sesji, nowszym wywołaniu albo zamknięciu okna. Wyłączenie istniejącego dodatkowego wyjścia jest dostępne także po zakończeniu źródła. Niepotwierdzone zatrzymanie odbiornika daje ostrzeżenie, nie komunikat zwykłego sukcesu.

## Znane ograniczenia i otwarty zakres

- AirPlay pod Shift+M oznacza dodatkowy odsłuch; główne wyjście nadal gra. Nie jest to nowa implementacja AirPlay jako jedynego głównego wyjścia pod Shift+A.
- Spotify, Apple Music oraz materiał odtwarzany wyłącznie na WiiM/Sonos/Frontier/HEOS nie udostępniają temu adapterowi bezpośredniego źródła w AMC. Nie pokazywać pozornego działającego Cast we wszystkich sesjach. Ogólne przechwytywanie dźwięku procesu wymaga oddzielnego toru i uwzględnienia ochrony treści. Brak TIDAL Connect sam w sobie nie jest przeszkodą dla obsługiwanego natywnego strumienia.
- Natywny TIDAL DASH: Cast pozostaje niedostępny w tym przyroście. AirPlay może przeliczać dźwięk i mieć opóźnienie. Nie deklarować Lossless ani synchronizacji urządzeń.
- Historyczne próby AirPlay na WiiM miały brak odsłuchu i timeout RTSP. Testy na atrapach nie stanowią potwierdzenia naprawy tego urządzenia. Potrzebny późniejszy świadomy odsłuch oraz odbiór NVDA; nie otwierać teraz widocznej próby przy trwającym wydaniu bez osobnego zlecenia.

## Testy i wynik

Końcowe celowane moduły Python: **240 zaliczonych, bez pominięć**. `test_network_audio_sessions.py` 37; Cast 20 (w tym rzeczywisty odizolowany host i sztuczna baza historii); kontrakt UI mirroringu 2; trasa AirPlay 7; wyrównanie tras 5; adapter AirPlay 12; lokalne dodatkowe wyjście 4; klient hosta 45; paleta 21; skróty 43; menu 44. Testy nie odtwarzały na fizycznym urządzeniu ani nie otwierały widocznego okna.

Nowy zestaw obejmuje wspólność nazw, skrótów, opisów i dostępności w menu/palecie/pomocy/obsłudze klawiatury, PL/EN, odpowiednich profilach Test/Public oraz widokach listy/odtwarzacza. Obejmuje izolację Podcasty–Radio Archiwista, przekazanie i powrót TIDAL, etap przygotowywania, odmowę DASH dla Cast, brak prywatnego proxy w publicznym statusie, zapamiętane źródło i właściciela dekodera, stare wyniki wykrywania, komunikaty braku AirPlay oraz niepotwierdzony STOP. Prawdziwe lokalne HTTP sprawdza Range/HEAD/404/416, inny adres klienta i brak przekazywania prywatnych nagłówków. Ukryta rzeczywista lista wx sprawdza etykiety i ponowne zaznaczenie; **nie jest to odbiór NVDA**.

Globalny `test_i18n.py`: **15 zaliczonych, 2 niezaliczone**. Pełny inwentarz zgłasza brakujące angielskie tłumaczenia innych bieżących prac, m.in. Apple Music, Soloist, ustawień i monitorowania nagrań. Nie rozszerzano tego zadania na cały katalog tłumaczeń. Nowe klucze tego przyrostu mają PL/EN i wpisy w szablonie; globalna bramka i18n nadal pozostaje otwarta przed wydaniem.

`AccessibleMediaController.LiteHost.ProtocolTests`: kompilacja Debug **0 błędów, 0 ostrzeżeń**; `--tidal-native --transport-routing`: **2 zestawy zaliczone**. Sprawdzono opcjonalną pozycję TIDAL oraz odrzucenie złego typu, pozycji ujemnej i poza czasem utworu. Pełny LiteHost Debug także **0 błędów, 0 ostrzeżeń**. Jest to kompilacja testowa, nie pakiet wydania.

Aparatura: przypięty Python `D:\Projekty Codex\AMC - zależności\python-test-envs\wxpython-4.3.1-cp314\Scripts\python.exe`; uruchamianie przez `C:\Users\micha\Documents\Codex\2026-10-29\a-3\work\cast002_checks.py`. Logi i odizolowane hosty: `C:\Users\micha\Documents\Codex\2026-10-29\a-3\work\cast002-20261123`. Powtórzenie rzeczywistego testu historii wymaga wskazania `AMC_CAST_TEST_HOST` na `host\amc_lite_host.exe` z tego katalogu. Nie użyto baz ani profilu użytkownika.

Nie ma nowej paczki, instalacji, publikacji ani uruchomienia użytkowego AMC dla tego przyrostu. Nie nadano nowej wersji. Historyczny odbiór listy CAST-001 nie jest odbiorem nowych sesji ani AirPlay.

## Materiał do instrukcji i listy zmian — szkic

Osobny [szkic instrukcji i listy zmian](CAST-002-instrukcja-i-lista-zmian-szkic.md) jest przeznaczony do korekty użytkownika, bez automatycznej publikacji.

## Stan przed zleceniem wydania — zapis historyczny

Bez nowego numeru wersji i bez commitu tego zadania; repozytorium ma wspólne niezapisane zmiany wielu zadań. Zakres źródeł tego przyrostu: nowe `network_audio.py`, `cast_tidal.py`; routery `cast_session.py`, `mirror_core.py`, `mirror_local.py`, `mirror_airplay.py`; `host_client.py`, `gui.py`, `mirror_dialog.py`, `shortcuts.py`, `menu_model.py`, `i18n.py`, `locales/template.json`; host `LiteTidalNativePlayRequest.cs` i odpowiednie fragmenty `LiteEngineHandlers.cs`; powiązane testy oraz dokumentacja. Nie obejmować automatycznie całych zastanych plików ani cudzych zmian przyszłym commitem.

Następny krok wydania: przegląd wspólnego zakresu, domknięcie bramki tłumaczeń, świadoma próba odsłuchu RARCH/TIDAL i rzeczywisty odbiór NVDA nowych wariantów listy (fokus początkowy, strzałki, Enter/Escape, powrót fokusu, ponowne otwarcie). Potem zgodny pakiet klienta i hosta w **następnym** wspólnym wydaniu. Nie dołączać samowolnie do już budującej się aktualizacji. Apple Music i HEOS pozostają osobnymi kartami.

## Wydanie i następny krok

Kod `dc000561d05ae7e0f2924c620e3d3f7599d943bd` opublikowano w obu profilach. Pełne wyniki, dokładne ścieżki instalatorów, SHA-256 i adresy wydań: [RELEASE-ALPHA519](RELEASE-ALPHA519-20261123-Cast-AirPlay.md). Aktualizatory obu instalacji widzą nową wersję. Nie wykonano lokalnej instalacji; odsłuch i NVDA pozostają do odbioru.
