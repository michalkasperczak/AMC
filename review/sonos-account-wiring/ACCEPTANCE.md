Podłączenie konta Sonos — odbiór lokalny kandydata 2ebe6ebecb3f41f289843dd6cd742c033d06832b.

Zaliczone: rzeczywiste MainWindow, menu, paleta, właściciel konta, ponowne otwarcie po błędzie zapisu oraz produkcyjne potwierdzenie wylogowania. Niezależny przegląd diffu nie wykazał blokad. Rodzic przeczytał raport i sam wykonał poniższe pomiary.

Automat rodzica: 49/49 asercji, kod 0, completed=true. Przechodzi przez kontrolkę menu i rzeczywiste ShowCommandPalette z filtrowaniem, wyborem i przyciskiem Wykonaj. Sprawdza faktyczny Begin/Fetch/WriteFailure, przeżycie nowego tokenu w pamięci, zapis dokładnie tego tokenu po reopen bez wywołania bramki, domyślne Nie, rzeczywiste Nie/Tak, anulowanie własnej próby po Close, operację koordynatora po zamknięciu dialogu oraz Dispose po rzeczywistym zamknięciu MainWindow. Log zawiera dynamiczny licznik asercji.

Kontrola ujemna: ten sam harness z osobną DLL Core, w której usunięto wyłącznie wywołanie otwarcia konta z routera. Poprawna kompilacja, kod 1, konkretna asercja account opened through requested caller. Nie zmieniano DLL pozytywnego przebiegu ani kandydata.

Żywy NVDA 7332, apphost procesu AccessibleMediaController12736: otwarcie menu Alt+P i strzałkami, Konto Sonos i Enter; właściwy pierwszy fokus pola instrukcji. Logowanie i sprawdzenie na danych syntetycznych, Escape do rzeczywistej listy głównej. Ctrl+Shift+K, wpisane sonos, wybrana pozycja Konto Sonos, ponowne otwarcie z zachowanym kontem. W prawdziwym AccessibleDialog odczyt treści i przejście do Nie/Tak; Escape, Alt+F4 i Nie zachowują konto, tylko Tak wykonuje Delete. Ponowne logowanie z rzeczywistym nieudanym wywołaniem atrapy Write, zamknięcie, ponowne otwarcie z palety i Tab/Enter na Ponów zapis. Stan i liczniki potwierdzają zachowanie konta, jeden odczyt magazynu oraz brak dodatkowego wywołania gateway przy RetryPersist.

Mowę odczytano z prawdziwego Podglądu mowy, nie z LastAnnouncement. Dokładne wiersze: sukces połączenia1, odwołanie wylogowania3 dla trzech osobnych akcji, potwierdzone wylogowanie1, nieudany zapis1, udany ponowny zapis1. Przy pierwszym otwarciu podgląd zawiera również odczyt treści dialogu i powtórzony opis pola instrukcji; nie twierdzimy, że każda początkowa etykieta wystąpiła tylko raz. Pojedyncze komunikaty wynikowe są rozliczone oddzielnie.

Izolacja: własne ConfigurationStore i SQLite, puste konta/podcasty/urządzenia/foldery/harmonogramy, wyłączone aktualizacje i InstallOnExit, jawny odmowny override instalatora, brak IPC/globalnych skrótów sondy, odpięty ContentRendered, syntetyczne wyłącznie gateway/store/browser. Prawdziwe ShowDialog, AccessibleDialog i UIA. Weryfikowane moduły produktu identyczne SHA z wcześniejszym własnym buildem rodzica. Wszystkie1922 pliki kandydata i zbiór nazw pozostały identyczne po pomiarach.

Sprzątnięcie: własny proces zakończony kodem0, utworzony Podgląd mowy zamknięty, brak AMC/dotnet/harness, NVDA7332 działa dalej. Nie zamykano ani nie uruchamiano instalacji użytkownika. Świeży stan przed próbami nie zawierał działającego AMC.

Granice: nie jest to prawdziwe logowanie Sonos ani próba produkcyjnego brokera/DPAPI/przeglądarki w tym przebiegu. Te niższe warstwy mają wcześniejsze osobne dowody. Żywy powrót był do pustej listy lokalnej, nie odtwarzacza. Nie uruchamiano pełnej regresji Windows ani instalatora. Trasa odnawiania dostępu w serwerze nadal wymaga wdrożenia przed właściwą próbą konta.

Błędy i naprawy aparatury, bez zmiany produktu: pierwotny automat jedynie zapisywał wyniki i ogłaszał ok, omijał paletę oraz maskował Escape przez Close; brakowało deklarowanego override instalatora. Zachowano oryginał w sonos-account-wiring-gui1. Własna pierwsza próba nie przeszła inicjalizacji SQLite; dodano zależność tej samej wersji co produkt. Dwa wcześniejsze odczyty NVDA wymagały oczekiwania na świeży fokus i poprawnej nazwy ASCII pola Tresc komunikatu. Wszystkie nieudane wyniki zachowano.

Dowody i źródła harnessu: `/home/michal/projekty/amc_pomoc/sonos-account-wiring-gui-parent1/` (ACCEPTANCE.json, nvda.jsonl, runtime-evidence/). Wcześniejszy build/komenda6/login36/refresh58/coordinator26/headless19: `/home/michal/projekty/amc_pomoc/sonos-account-wiring-parent1/`. Pełny przegląd: `/home/michal/projekty/amc_pomoc/sonos-account-wiring-quality1/REPORT.md`.

Ten dokument dodano po pomiarze jako zapis odbioru. Zmiany dokumentacyjne nie zmieniają badanych źródeł produktu.
