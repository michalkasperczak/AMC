# Weryfikacja alpha 342 / NVDA 0.2.1 — 11 września 2026

## Zakres

Nowa uzgodniona mapa, 12 jawnych presetów sesji, przyciski tworzenia playlist
i presetów, przypisanie lokalnego skrótu WiiM bez powrotu do okna głównego.
Sterowanie w tle nie kradnie fokusa; jawny preset kontenera przechodzi przez
ten sam mechanizm zezwolenia foreground co otwarcie list.

## Wyniki

- Ostateczne `build.ps1 -Publish -KeepPreviousPackages`: kod 0.
  Kompilacja bez ostrzeżeń i błędów; pełny zestaw Core i Windows smoke przeszedł.
- Osobny test `--nvda-bridge-smoke`: kod 0 po uzupełnieniu testów przycisków.
- Python 3.14: `python -B nvda-addon/tests/test_controller.py` — 7 testów, OK.
  Zgodność wszystkich 65 gestów i poleceń, unikalne przypisania, brak zarezerwowanych
  Ctrl+Windows+Shift+cyfra, granice kolejki, brak ponownego przełącznika,
  timeout i wymiana JSON z odizolowanym serwerem .NET.
- 12 presetów mapuje się na istniejący router AMC. Odrzucone preset0, preset13,
  preset01, liczby ujemne i dodatkowe polecenia; kontenery wymagają widocznego
  przeglądania, elementy odtwarzalne mogą pozostać w tle.
- Izolowane kontrolki WPF: Utwórz odświeża listę i wskazuje nowe przypisanie,
  bez uruchomienia presetu; anulowanie zachowuje dotychczasową pozycję.
  WiiM odświeża lokalny skrót i pozostawia listę otwartą.
  Enter i Spacja spoza listy nie są przechwytywane przez obsługę listy
  w oknach presetów, przypisania, WiiM i przynależności do playlist.
- Weryfikacja pięciu wpisów pakietu dodatku: treść zgodna ze źródłami,
  manifest 0.2.1, wymagane AMC alpha 342.
- `git -c core.safecrlf=false diff --check`: bez błędów.

Jedna pośrednia próba testów zatrzymała budowę prawidłowo kodem 1:
nowa symulacja klawisza ustawiła Source przed RoutedEvent. Naprawiono konstrukcję
zdarzenia w teście, nie wyłączono testu. Powtórny test izolowany i pełna kompilacja
przeszły. Końcowy pakiet został przebudowany po ostatnich poprawkach.

## Granice weryfikacji

Nie wykonywano ręcznych prób mowy/brajla NVDA, przełączania rzeczywistego
urządzenia, aktywacji presetów na koncie ani tworzenia playlisty na serwerze TIDAL.
Testy API i kontrolek są izolowane; nie są potwierdzeniem fizycznego odtwarzania.
Systemowa tabela Microsoft nie wymienia Ctrl+Windows+A ani Ctrl+Windows+Tab
jako osobnych poleceń, ale inne dodatki mogą je zajmować.

Przed publikacją wykonać próby z INSTRUKCJA_0.1.0-alpha.342_PL.md: pomoc NVDA,
pełna mapa i wszystkie 12 miejsc, puste listy, pierwszy fokus, Tab/strzałki,
Save/Cancel, ponowne otwarcie, zminimalizowane AMC i opóźnione odpowiedzi urządzeń.
Nie uruchamiano nowego AMC ani nie instalowano/restartowano NVDA. Nie publikowano.
Zachowano poprzednie wersje; nie edytowano prywatnego pliku wiadomości TIDAL.

### Uzupełnienie po testach użytkownika

11 września 2026 r. użytkownik potwierdził poprawioną najnowszą wersję i zlecił
publikację na GitHubie do dalszego testowania, również z innymi modelami.
Nie podano wyników każdego scenariusza osobno — powyższe granice weryfikacji
pozostają aktualne. Zgoda na przyszłe aktualizowanie dodatku i restartowanie
NVDA/AMC obowiązuje wyłącznie, gdy nie trwa nagrywanie; zapisano ją w AGENTS.md.

## Źródła weryfikacji mapy

- Microsoft: https://support.microsoft.com/en-us/accessibility/windows/keyboard-shortcuts-in-windows
- NV Access, nazwy klawiszy i modyfikatory: https://github.com/nvaccess/nvda/blob/master/source/keyboardHandler.py

## Dodatek

- AMC-NVDA-0.2.1.nvda-addon, 9741 bajtów
- SHA-256: 8F39B85C52D5E1B8307FB12FC3740DFE5AF5BA0AF0067EE32D02B31F06E8EA14

## Program

- publish/AccessibleMediaController-0.1.0-alpha.342/AccessibleMediaController-0.1.0-alpha.342.exe
- ProductVersion: 0.1.0-alpha.342; 167253696 bajtów
- SHA-256: 0A94BB59482C93A6C1D1CA0647C0937B120A027714E5B35106C1066A75534B6D
