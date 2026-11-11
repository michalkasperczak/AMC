# RECOG-HIST-20261111 — Historia TIDAL i działania na rozpoznanym utworze

Status: **W trakcie — kod, testy celowane i kompilacja hosta zaliczone; bez instalacji i odbioru NVDA**.

## Zgłoszenie i wynik

11.11.2026 użytkownik zgłosił, że lewa strzałka w historii TIDAL (`Ctrl+H`) czasem czyta tylko wykonawcę zamiast bitrate. Po otwarciu rozpoznanego utworu z `Ctrl+Alt+S` prawą strzałką odtwarzacz działał, lecz `Ctrl+Shift+L` i `Ctrl+Shift+U` nie dawały oczekiwanego zapisu ani jasnego potwierdzenia. Te skróty nie działały także przed otwarciem usługi, na liście rozpoznanych utworów i działań. Kopiowany link wymagał jednoznacznego rozróżnienia dokładnego utworu i wyszukiwania.

W źródłach AMC Test lewa strzałka w historii TIDAL odczytuje bitrate zapisany dla tego konkretnego odtworzenia. Host podaje go jako osobne pole, a historia zachowuje pomiar z odpowiedzi dotyczącej właściwego utworu. Dla wpisu bez pomiaru odczyt mówi, że bitrate jest niedostępny. W odtwarzaczu skróty kolekcji kierują się rzeczywiście odtwarzanym utworem, nie ukrytym zaznaczeniem poprzedniej listy. Biblioteka dostaje album utworu, Ulubione sam utwór. Na liście rozpoznanych utworów i pod prawą strzałką te same skróty korzystają z potwierdzonego dopasowania do Spotify lub TIDAL i ze wspólnej obsługi zmian kolekcji używanej przez wyniki wyszukiwania. Rozbieżny album albo nierozstrzygnięte dwa wydania nie są traktowane jako dokładny wynik.

`Ctrl+Shift+C` w historii i pod prawą strzałką kopiuje dokładny link, gdy wynik jest potwierdzony. Jeśli takiego wyniku brak, kopiuje nazwany link wyszukiwania i mówi o tym wyraźnie. Nie przypisuje linkowi wyszukiwania tożsamości konkretnego nagrania.

## Materiał do dokumentacji użytkownika

- W AMC Test otwórz rozpoznane utwory skrótem `Ctrl+Alt+S`. Na wybranym wpisie `Ctrl+Shift+L` zmienia zapis dopasowanego albumu w Bibliotece usługi domyślnej, a `Ctrl+Shift+U` zmienia zapis samego utworu w Ulubionych. Prawa strzałka otwiera listę działań; te same skróty działają na działaniu dotyczącym Spotify lub TIDAL. Program potwierdza zmianę dopiero po odpowiedzi usługi. Jeśli nie ma dokładnego dopasowania lub albumu, mówi dlaczego nie zapisał pozycji.
- Po otwarciu rozpoznanego utworu w odtwarzaczu AMC Test `Ctrl+Shift+L` dotyczy jego albumu, a `Ctrl+Shift+U` odtwarzanego utworu.
- `Ctrl+Shift+C` kopiuje dokładne łącze do wybranej pozycji, jeśli jest potwierdzone, albo link wyszukiwania. Komunikaty rozróżniają „Skopiowano link TIDAL/Spotify” i „Skopiowano link wyszukiwania TIDAL/Spotify”.
- W historii odtwarzania TIDAL (`Ctrl+H`) lewa strzałka podaje czas, wykonawcę, album i zmierzony bitrate, jeśli jest dostępny dla tego odtworzenia. W innym wypadku mówi „Bitrate niedostępny dla tego odtworzenia”. Nie należy odczytywać wartości z bieżącego odtwarzacza jako danych starego wpisu.
- Skróty zmiany kolekcji rozpoznanego utworu dotyczą AMC Test. W profilu Public niedostępne usługi nie pojawiają się jako działania zapisu. Nie jest potrzebna dodatkowa konfiguracja poza zalogowaniem do wybranej usługi.

## Uzasadnienie i decyzje

- 11.11.2026: użyto istniejących poleceń zapisu z wyników wyszukiwania, aby zachować potwierdzenie operacji i nazwy komunikatów. Wcześniejszy brak działania skrótów w oknie rozpoznawania zostaje zastąpiony obsługą wybranego, dokładnego wyniku.
- 11.11.2026: bitrate zapisuje się tylko przy zgodnej tożsamości natywnego odtwarzania TIDAL. Nie wyprowadza się go z gotowego tekstu paska stanu.
- 11.11.2026: brak dokładnego linku przy kopiowaniu powoduje jawnie nazwane wyszukiwanie; nie powoduje cichego skopiowania podobnego nagrania.

## Zmienione miejsca

`wxlite/amc_wx_lite/gui.py`, `recognition_actions.py`, `playback_history.py`, `shortcuts.py`, `src/AccessibleMediaController.LiteHost/LiteEngineHandlers.cs`, `README_PL.md`, `wxlite/tests/test_recognition_actions.py`, `test_streamer_playback_history.py`, `test_command_palette.py` oraz ten rejestr. Wspólny checkout zawiera także niezależne, równoległe prace nad YouTube; nie są częścią tego zadania.

## Testy i wynik

- 42 testy celowane rozpoznawania i historii: zaliczone (11.11.2026). Obejmują oba serwisy, zapis albumu i utworu, rozbieżny album, dwuznaczne wydania, dokładny i wyszukiwany link oraz właściwe przypisanie bitrate.
- 194 testy skrótów, palety, menu i nawigacji wyszukiwania: zaliczone (11.11.2026).
- `dotnet build src/AccessibleMediaController.LiteHost/AccessibleMediaController.LiteHost.csproj -c Release --no-restore --nologo -v:q`: sukces, zero ostrzeżeń i błędów (11.11.2026).
- `git diff --check`: bez błędów białych znaków; Git zgłosił jedynie przewidywaną konwersję końców linii w teście palety.
- Nie uruchamiano widocznych testów GUI, odsłuchu NVDA, rzeczywistej zmiany kolekcji w koncie Spotify/TIDAL ani instalacji.

## Znane ograniczenia

Starsze wpisy historii nie zawierają zachowanego pomiaru bitrate. Rozpoznanie bez jednoznacznego wyniku katalogu nie pozwala na zmianę kolekcji. Skuteczność w rzeczywistym koncie i mowa NVDA wymagają odbioru po instalacji nowej wersji.

## Wersja i commit

Zmiany zapisano na gałęzi `codex/amc-test-public-20261109` w commicie `cf0de877` na bazie `24535a8189b5549ad7501cc48a2d3a659cb4dbcf`. Kod zmieniony i testy celowane zaliczone; host skompilowany. Paczka tej poprawki nie została zbudowana, aplikacja nie została zaktualizowana, wydanie nie zostało opublikowane. Zaobserwowana wcześniej zainstalowana aplikacja nosiła nazwę `AMC Test — 10.11.2026, 12:52` i nie obejmuje tej poprawki.
