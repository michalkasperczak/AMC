# RELEASE-20261111 — zbiorcza aktualizacja AMC

Status: **Wydanie Test/Public alpha.496 opublikowane i zainstalowane 11.11.2026; ręczny odsłuch gestów NVDA pozostaje do odbioru**.

11.11.2026 użytkownik potwierdził, że nagrywanie ustało, i zezwolił na aktualizację całości. To zastępuje wcześniejszy stan niepewności nagrań; przed samą podmianą aktualizator nadal ma użyć własnej ochrony przed zamknięciem programu podczas nagrywania.

## Decyzja użytkownika i zakres

11.11.2026 użytkownik polecił po zakończeniu bieżących zadań wdrożyć całość, opublikować ją na GitHubie i zaktualizować lokalne AMC. Ta decyzja zastępuje ewentualne wcześniejsze oczekiwanie na osobne polecenie publikacji lub aktualizacji. Wydanie obejmie gotowe, zweryfikowane zmiany bieżącej gałęzi wxPython, w tym historię i rozpoznane utwory TIDAL/Spotify oraz ręczny zapis fragmentu radia. Profil Test i Public powstają z jednego commitu; funkcje prywatne pozostają ograniczone profilem.

## Stan na 11.11.2026

- Poprawka historii i działań na rozpoznanym utworze ma zaliczone testy celowane i kompilację hosta; commit źródła `cf0de877`. Brak odbioru w zainstalowanym AMC i rzeczywistym koncie. Szczegóły: [RECOG-HIST-20261111](RECOG-HIST-20261111-tidal-bitrate-kolekcje.md).
- Ręczny zapis fragmentu radia jest w źródłach i przeszedł wcześniejsze testy celowane. Pomiar przejść ICY jest zapisany w `a3072a4c`, lecz nie potwierdza słyszalnych granic utworu. Brak odbioru NVDA i instalacji. Szczegóły: [RADIO-TRACK-001](RADIO-TRACK-001-zapis-rozpoznanego-utworu.md).
- W chwili przygotowania tego rekordu checkout zawierał niezapisane zmiany w kodzie radia, GUI i instalatora. Nie przygotowano z tego stanu paczki ani wydania GitHub. Instalator wymaga czystego, zatwierdzonego commitu zgodnego z manifestem paczki.
- W używanym AMC Test zgłoszono zakończenie jednego nagrania, lecz nie potwierdzono braku innych nagrań. Użytkownik korzystał z okna równocześnie z próbą odczytu. Nie zamykano AMC ani NVDA.
- W późniejszej wiadomości z 11.11 użytkownik potwierdził brak trwających nagrań i polecił przeprowadzić zbiorczą aktualizację. Ustalono wersję źródeł `0.1.0-alpha.495`; przed wydaniem testy instalatora, dodatku NVDA i mostka dały **63 zaliczone, 68 podtestów zaliczonych, 1 test interoperacyjności wyłączony**. Pierwszy przebieg wspólnych testów nie zebrał dwóch modułów bez projektowego `PYTHONPATH`; drugi, z poprawnym środowiskiem, przeszedł.
- Paczki i instalatory Test/Public `alpha.495` z czystego `c8bed6af` przeszły kontrolę integralności, startu i sekretów. Próbna cicha instalacja Test skopiowała aplikację, ale nie zaktualizowała dodatku 0.4.1: `CreateProcess` przy restarcie NVDA zwróciło WinError 740. Inno zapisało wyjątek w `ssPostInstall`, lecz zwróciło kod 0, więc aktualizator uznał niepełną instalację za udaną. **Instalatory alpha.495 nie nadają się do publikacji.**
- Korekta 11.11: restart NVDA przez Windows ShellExecute oraz jawny niezerowy kod wyjścia instalatora przy błędzie kontroli lub dodatku; docelowa wersja `alpha.496`. Testy celowane po zmianie: **32 zaliczone**. Pomocnik uruchomiony bez działającego AMC zaktualizował dodatek do 0.4.2, zachował kopię 0.4.1 i zrestartował NVDA; po 6 s pozostał jeden nowy proces NVDA. To jest odbiór instalacji dodatku, jeszcze nie ręczne sprawdzenie jego gestów.

## Warunki wydania i aktualizacji

1. Zakończyć zmiany bieżących zadań, zatwierdzić je i uruchomić adekwatne testy regresji oraz kontrolę profili, komend, pomocy i dostępności. Odbiór NVDA wykonać na bezpiecznej instalacji testowej, gdy stan nagrań na to pozwoli.
2. Z jednego czystego commitu zbudować i zweryfikować paczki oraz instalatory Test/Public: wersję, manifest, start, integralność, zawartość bez danych i sekretów, SHA-256.
3. Wysłać dokładnie ten commit do `michalkasperczak/AMC`; opublikować Test jako wydanie wstępne i Public jako stabilne, z odpowiadającymi paczkami. Odczytać zdalny commit oraz nazwy, rozmiary i sumy artefaktów.
4. Bezpośrednio przed instalacją odczytać listę aktywnych nagrań. Jeśli nie można potwierdzić, że nagrywanie ustało, nie zamykać bieżącego AMC ani NVDA. Po bezpiecznej instalacji sprawdzić wersję uruchomionego programu, pojedynczą instancję, skróty i komunikaty NVDA w obu profilach. Dodatek NVDA 0.4.2 instalować razem ze zgodnymi hostami.

## Materiał do dokumentacji użytkownika

W aktualizacji AMC Test historia TIDAL ma czytać bitrate konkretnego odtworzenia, a lista rozpoznanych utworów obsługuje zapis dopasowanego albumu w Bibliotece (`Ctrl+Shift+L`), utworu w Ulubionych (`Ctrl+Shift+U`) i kopiowanie jednoznacznie nazwanego linku (`Ctrl+Shift+C`). Nowe okno radia pozwala ręcznie wybrać fragment przed zapisem. Instalacja alpha.496 została potwierdzona manifestem i procesem; zachowanie w rzeczywistym koncie oraz odsłuch NVDA pozostają do odbioru.

## Testy i wynik

Testy instalatora, dodatku i mostka: **63 zaliczone, 68 podtestów zaliczonych, 1 test interoperacyjności z historycznym hostem .NET wyłączony**. Pełny przebieg bez wyłączenia: 63 zaliczone, 1 niezaliczony z powodu niedopasowania testowego hosta .NET, a nie profili wxPython. Obie paczki alpha.496 przeszły pełną kontrolę integralności i kontrolowany start; oba instalatory kontrolę sekretów i SHA-256. Ciche instalacje Test/Public zakończyły się w logach Inno komunikatem `Installation process succeeded`; oba procesy działają z właściwych katalogów. Osobny minimalny instalator z identyczną obsługą błędu po instalacji zwrócił kod **10**, gdy wymuszono wyjątek w `CurStepChanged(ssPostInstall)` i zaczekano na zamknięcie procesu. Pierwsza próba odczytu kodu przez bezpośrednie `&` w PowerShell była myląca, bo instalator GUI uruchomił się asynchronicznie; nie jest wynikiem testu. Polecenie odczytowe `status` przez dokładny protokół dodatku otrzymało `ok: true` z obu profili w jednej sesji. Gestów z klawiszem Windows nie wstrzykiwano automatycznie do aktywnego pulpitu użytkownika; ich rzeczywista mowa NVDA wymaga odsłuchu użytkownika.

## Znane ograniczenia

Pomiar dwóch zmian sygnału nie dowodzi granic całej piosenki. Działanie skrótów NVDA sprawdzono testami mapy gestów i połączeniem protokołu, bez automatycznego naciśnięcia kombinacji Windows w aktywnym pulpicie.

## Wersja i commit

Baza przygotowania: `a3072a4c`. Wydanie: gałąź `codex/amc-test-public-20261109`, commit `4e46c80b51cd96e95af41b0f3c999a8733164b63`, wersja `0.1.0-alpha.496`. Test: `test-2026.11.11.1922`, instalator 136 519 477 bajtów, SHA-256 `d2ce76398b2b8740edb99ae8550a3a5251c76b6a7597d7a0ef681b2755ff37ed`. Public: `public-2026.11.11.1925`, instalator 133 942 892 bajtów, SHA-256 `8b56a211f3a894dc61a3823ead103ccdd5fcf515d6aefc2fb806339a09f49b03`. Oba zdalne artefakty mają stan `uploaded`, identyczne rozmiary i skróty SHA-256. Dodatek NVDA 0.4.2 zainstalowany, poprzednia wersja zachowana w kopii. Limit Codex po zadaniu: 10% użyte, 90% pozostało, odnowienie 18.11.2026 07:46 czasu polskiego.
