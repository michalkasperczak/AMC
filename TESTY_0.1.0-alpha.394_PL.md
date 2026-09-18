# AMC 0.1.0-alpha.394 — Spotify: obsługa i właściwości

## Co sprawdzić

- W Spotify otwórz `Ctrl+F`, wpisz wykonawcę lub tytuł spoza biblioteki. Wyniki mają pochodzić z katalogu Spotify; przy braku sieci mają pozostać lokalne wyniki i czytelny komunikat.
- `Ctrl+Shift+U` i `Ctrl+Shift+L` zapisują na koncie, nie tylko zmieniają lokalną flagę. Stare logowanie może wymagać ponownego zalogowania przez `Ctrl+F5` i przyznania nowych uprawnień. Odmowa nie może wyglądać jak sukces.
- Usunięcie utworu z Ulubionych nie może zatrzymać tego, co właśnie gra. Dodanie z wyszukiwania nie powinno tworzyć drugiego wiersza tego samego utworu.
- Strzałka w prawo na wykonawcy prowadzi do albumów; na albumie pokazuje także przejście do wykonawcy, jeżeli dane Spotify zawierają tę relację. Na starszym zapisie biblioteki może być potrzebne odświeżenie danych.
- `Alt+Shift+Enter`: opcje pamiętania pozycji elementu lub kontenera. Po zatwierdzeniu wraca lista, wybór ma przetrwać ponowne uruchomienie.
- `Ctrl+Shift+E`, `Ctrl+Shift+R`, `Ctrl+Shift+T`: czas od początku, pozostały i długość. Sprawdź kilka powtórzeń oraz powrót po oknie ustawień.
- `Alt+Enter`: właściwości otwierają się w polu tekstowym tylko do odczytu. Strzałki i `NVDA+góra` mają czytać kolejne i bieżący wiersz. Przycisk przełączania widoku pozostawia dostęp do dokumentu HTML.
- W sekcji Techniczne informacja o jakości odróżnia deklarację Spotify od rzeczywistego pomiaru. Program nie ogłasza Lossless bez dowodu.

## Granice tego etapu

Wydanie nie zawiera jeszcze Librespot ani zewnętrznego trybu Lossless. Odtwarzacz Web Playback SDK pozostaje bez zmian. Wybór wyjścia Spotify przez Shift+A oraz pełne zapisywanie playlist są kolejnymi etapami, nie ukończonymi funkcjami tej wersji.

## Weryfikacja przed wydaniem

Testy wykonywane na Windows, a interfejs także żywym NVDA na izolowanym zestawie próbnych albumów i utworów na Hermesie. Próby HTTP nie zmieniają prawdziwego konta Spotify. Zestaw czasu ma symulowane odtwarzanie 1:13 z 4:00: to test skrótów i mowy, nie pomiar przesyłania muzyki.
