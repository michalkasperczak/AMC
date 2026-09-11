# AMC alpha 343

Wersja przygotowana 11 września 2026 r. Zawiera poprawkę odtwarzania TIDAL oraz raport audytu kodu.

Do wykonania po instalacji: **wyloguj się z TIDAL w AMC i zaloguj ponownie.** Bez tego poprawka nie zadziała. Powód jest wyjaśniony niżej.

## Pliki

- Raport audytu: `AUDYT_HERMES_2026-09-11_PL.md`
- Zmieniony kod: `src\AccessibleMediaController.Windows\Services\TidalOAuthClient.cs`

## Na czym polegał problem z TIDAL

Mówiąc najprościej: użyta była niezgodność między starym i nowym interfejsem TIDAL.

TIDAL ma dziś dwa interfejsy programistyczne. Nowy obsługuje kolekcję, playlisty i wyszukiwanie. Stary, nadal działający, wydaje pozwolenie na odtworzenie pełnego utworu. Każdy z nich rozpoznaje inny zestaw uprawnień.

AMC prosiło o uprawnienia tylko z nowego interfejsu. Kolekcja, wyszukiwanie i logowanie działały bez zarzutu, bo to właśnie domena nowego interfejsu. Ale gdy program pytał stary interfejs o pozwolenie na pełny utwór, ten nie rozpoznawał przedstawionych uprawnień i oddawał to, co daje każdemu bez uprawnień, czyli próbkę około 30 sekund.

Abonament nie miał z tym nic wspólnego. Mylący był komunikat TIDAL, który brzmiał „pełne odtwarzanie wymaga abonamentu", choć w rzeczywistości znaczył „ten token nie ma prawa do pełnego odtwarzania".

## Co zostało zmienione

AMC prosi teraz dodatkowo o dwa stare uprawnienia, `r_usr` i `w_usr`, których wymaga stary interfejs. Nowe uprawnienia pozostały, bo na nich działa kolekcja i playlisty.

Naprawiono też wynikający z tego błąd. Odświeżanie logowania żądało zawsze pełnej listy uprawnień. Po rozszerzeniu listy serwer odrzuciłby takie żądanie jako szersze niż przyznane i wylogowałby użytkownika po aktualizacji. Teraz odświeżanie prosi wyłącznie o to, co konto faktycznie przyznało.

Dlatego konieczne jest ponowne zalogowanie: istniejące logowanie nie może samo zyskać nowych uprawnień. Nowe uprawnienia przyznaje się tylko przy świeżym logowaniu.

AMC zapisuje teraz w dzienniku, czy TIDAL przyznał uprawnienie `r_usr`.

## Krótki test do wykonania

1. Wyloguj się z TIDAL w AMC, następnie zaloguj ponownie i zatwierdź zgodę.
2. Włącz dowolny utwór z TIDAL i sprawdź, czy gra dłużej niż 30 sekund.
3. Jeżeli nadal urywa się na 30 sekundach, zajrzyj do dziennika AMC i odszukaj wpis o zakresach przyznanych przez TIDAL. Będzie tam napisane wprost, czy uprawnienie `r_usr` zostało przyznane.

Wynik punktu trzeciego jest istotny w obu przypadkach. Jeżeli uprawnienie nie zostało przyznane, to znaczy, że TIDAL odmawia go aplikacjom spoza własnej listy zatwierdzonych. Wtedy problem nie jest w kodzie AMC, a zapis z dziennika staje się konkretnym argumentem w rozmowie z pomocą TIDAL.

## Uwaga o niestabilnym teście

Audyt wykazał, że test mostka NVDA (`NvdaBridgeSmokeTests`, metoda `TestDispatcherDeadline`) zawodzi losowo, w około dwóch na trzy uruchomienia, i to niezależnie od zmian z tej wersji. Awaria przerywa cały zestaw testów Windows, więc testy po niej nie wykonują się wcale. Rzecz do naprawy w kolejnej wersji; szczegóły w raporcie audytu.
