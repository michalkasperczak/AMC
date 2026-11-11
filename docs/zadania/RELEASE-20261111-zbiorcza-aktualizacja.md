# RELEASE-20261111 — zbiorcza aktualizacja AMC

Status: **Przygotowanie; wydanie, instalacja i odbiór jeszcze niewykonane**.

## Decyzja użytkownika i zakres

11.11.2026 użytkownik polecił po zakończeniu bieżących zadań wdrożyć całość, opublikować ją na GitHubie i zaktualizować lokalne AMC. Ta decyzja zastępuje ewentualne wcześniejsze oczekiwanie na osobne polecenie publikacji lub aktualizacji. Wydanie obejmie gotowe, zweryfikowane zmiany bieżącej gałęzi wxPython, w tym historię i rozpoznane utwory TIDAL/Spotify oraz ręczny zapis fragmentu radia. Profil Test i Public powstają z jednego commitu; funkcje prywatne pozostają ograniczone profilem.

## Stan na 11.11.2026

- Poprawka historii i działań na rozpoznanym utworze ma zaliczone testy celowane i kompilację hosta; commit źródła `cf0de877`. Brak odbioru w zainstalowanym AMC i rzeczywistym koncie. Szczegóły: [RECOG-HIST-20261111](RECOG-HIST-20261111-tidal-bitrate-kolekcje.md).
- Ręczny zapis fragmentu radia jest w źródłach i przeszedł wcześniejsze testy celowane. Pomiar przejść ICY jest zapisany w `a3072a4c`, lecz nie potwierdza słyszalnych granic utworu. Brak odbioru NVDA i instalacji. Szczegóły: [RADIO-TRACK-001](RADIO-TRACK-001-zapis-rozpoznanego-utworu.md).
- W chwili przygotowania tego rekordu checkout zawierał niezapisane zmiany w kodzie radia, GUI i instalatora. Nie przygotowano z tego stanu paczki ani wydania GitHub. Instalator wymaga czystego, zatwierdzonego commitu zgodnego z manifestem paczki.
- W używanym AMC Test zgłoszono zakończenie jednego nagrania, lecz nie potwierdzono braku innych nagrań. Użytkownik korzystał z okna równocześnie z próbą odczytu. Nie zamykano AMC ani NVDA.

## Warunki wydania i aktualizacji

1. Zakończyć zmiany bieżących zadań, zatwierdzić je i uruchomić adekwatne testy regresji oraz kontrolę profili, komend, pomocy i dostępności. Odbiór NVDA wykonać na bezpiecznej instalacji testowej, gdy stan nagrań na to pozwoli.
2. Z jednego czystego commitu zbudować i zweryfikować paczki oraz instalatory Test/Public: wersję, manifest, start, integralność, zawartość bez danych i sekretów, SHA-256.
3. Wysłać dokładnie ten commit do `michalkasperczak/AMC`; opublikować Test jako wydanie wstępne i Public jako stabilne, z odpowiadającymi paczkami. Odczytać zdalny commit oraz nazwy, rozmiary i sumy artefaktów.
4. Bezpośrednio przed instalacją odczytać listę aktywnych nagrań. Jeśli nie można potwierdzić, że nagrywanie ustało, nie zamykać bieżącego AMC ani NVDA. Po bezpiecznej instalacji sprawdzić wersję uruchomionego programu, pojedynczą instancję, skróty i komunikaty NVDA w obu profilach. Dodatek NVDA 0.4.2 instalować razem ze zgodnymi hostami.

## Materiał do dokumentacji użytkownika

W planowanej aktualizacji AMC Test historia TIDAL ma czytać bitrate konkretnego odtworzenia, a lista rozpoznanych utworów ma obsługiwać zapis dopasowanego albumu w Bibliotece (`Ctrl+Shift+L`), utworu w Ulubionych (`Ctrl+Shift+U`) i kopiowanie jednoznacznie nazwanego linku (`Ctrl+Shift+C`). Nowe okno radia ma pozwolić odsłuchać i ręcznie wybrać fragment przed zapisem. Ten opis dotyczy przygotowywanego wydania; bieżąca instalacja nie została tu potwierdzona jako zaktualizowana.

## Testy i wynik

W tym etapie sprawdzono stan źródeł i procedurę wydania. Nie uruchomiono budowy paczek, instalacji, publikacji ani odbioru NVDA. Wyniki testów poszczególnych funkcji są w ich rekordach.

## Znane ograniczenia

Niezweryfikowany stan wszystkich aktywnych nagrań i równolegle zmieniany checkout blokują teraz bezpieczną instalację oraz zamknięcie zakresu wydania. Pomiar dwóch zmian sygnału nie dowodzi granic całej piosenki.

## Wersja i commit

Baza przygotowania: gałąź `codex/amc-test-public-20261109`, commit `a3072a4c`. Docelowa wersja, commit wydania, paczki, instalacja i publikacja: jeszcze nieustalone.
