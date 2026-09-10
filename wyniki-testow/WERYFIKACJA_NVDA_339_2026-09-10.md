# Weryfikacja dodatku NVDA — AMC alpha 339

## Wynik automatyczny

- `build.ps1 -Publish`: kod 0. Kompilacja bez ostrzeżeń i błędów; pełne zestawy Core i Windows smoke zakończone pomyślnie.
- `python -X utf8 -B -m unittest discover -s nvda-addon/tests -v`: 6 testów, OK. Rzeczywiste połączenie Python / Win32 / named pipe / .NET dla wszystkich 14 komend, na izolowanym serwerze testowym bez danych i bez dźwięku użytkownika.
- Test serwera: allow-list, odrzucenie nieobsługiwanych i niekompletnych wiadomości, wersjonowanie, limity, polskie znaki, jedno wykonanie na połączenie, rozłączenie milczącego klienta i ponowna dostępność serwera, zamknięcie.
- Test Dispatcher: niepodjęta komenda po upływie terminu nie uruchamia się po wznowieniu obsługi zdarzeń.
- Test klienta: brak programu, błędna odpowiedź, opisy wszystkich komend, brak domyślnych gestów przejmujących Free Radio, ograniczona kolejka, nieblokujące wyłączenie i brak ponawiania niepotwierdzonej pauzy.
- `git diff --check`: bez błędów białych znaków.

Pierwszy przebieg pełnych testów zatrzymał się w nowym teście transportu, ponieważ wcześniejsze testy WPF pozostawiały DispatcherSynchronizationContext. Zakończono wyłącznie własny proces testowy, nie AMC użytkownika. Test transportu przeniesiono na Task.Run, zachowując osobną próbę anulowania na Dispatcherze; kolejny pełny przebieg zakończył się kodem 0. Nie był to błąd działającego odtwarzacza. Pierwszy test Python wykazał również krótki brak pipe między połączeniami: dodano ograniczone ponawianie samego otwarcia, przed wysłaniem komendy, bez ponawiania wysłanego działania.

## Paczki

- `publish/AccessibleMediaController-0.1.0-alpha.339/AccessibleMediaController-0.1.0-alpha.339.exe` — pakiet Windows x64.
- `AMC-NVDA-0.1.0.nvda-addon` — 6393 bajty, SHA-256 `639EF06A9D3B533DF781FC59F387932F32605B4FD24F228538C70C3605281734`.
- Zweryfikowano 5 wpisów dodatku: manifest, pomoc HTML po polsku, trzy pliki źródłowe Python. Bez ustawień użytkownika, logów, danych kont, nagrań i skompilowanych cache Pythona.

## Niezbadane / do testów użytkowych

Nie instalowano dodatku i nie przeładowywano działającego NVDA. Nie uruchamiano alfy 339; działająca 338 i jej pakiet pozostały zachowane. Nie potwierdzono jeszcze rzeczywistego odsłuchu/brajla, konfliktów gestów, fokusa przy odpowiedziach asynchronicznych, uśpienia ani zachowania z rzeczywistym urządzeniem WiiM. Manifest wskazuje docelowy poziom kompatybilności NVDA 2026.1, a nie zaliczony odsłuchowy test tej wersji.

Docelowa mapa skrótów wymaga rozdzielenia z Free Radio. Wersja próbna ma wszystkie komendy widoczne w kategorii AMC w standardowym oknie Zdarzenia wejścia, ale nie przydziela automatycznie klawiszy. Plan i ręczne scenariusze: `PROJEKT_NVDA_PL.md`.
