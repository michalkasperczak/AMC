# Wyniki testów AMC 0.1.0-alpha.332

Na początku opisz zauważone zachowanie. Nie trzeba przy każdym punkcie wpisywać
wariantu „OK” albo „błąd”. Po dwukropku wpisuj spację.

## Uwagi

## Pierwsze odtwarzanie TIDAL

- Przejdź do TIDAL, otwórz album lub playlistę i naciśnij Enter na utworze.
- Sprawdź, czy pojawia się natywny odtwarzacz AMC, a fokus NVDA nie przechodzi
  do przeglądarki ani niewidocznej kontrolki WebView2.
- Zanotuj, czy odtwarzany jest pełny utwór, czy próbka. Przy próbce AMC powinien
  podać jeden komunikat o poziomie dostępu aplikacji TIDAL.
- Sprawdź odtwarzanie przez co najmniej 40 sekund, o ile TIDAL udostępni pełny
  materiał.

## Sterowanie

- Enter lub Spacja: wstrzymaj i wznów bez ponownego logowania.
- Lewa i prawa strzałka, warianty z Shift i Ctrl: przewijanie.
- Cyfry `0–9`, `Ctrl+J` i `Ctrl+Shift+J`: skoki do procentu i czasu.
- Góra i dół: głośność wyłącznie TIDAL, bez zmiany głośności NVDA.
- `Page Up` i `Page Down`: poprzedni i następny utwór w otwartym albumie,
  playliście albo Kolejce.
- Sprawdź, czy polecenia zmiany prędkości nie są oferowane ani zapowiadane w
  odtwarzaczu TIDAL.

## Informacje i fokus

- `Ctrl+Shift+E`, `Ctrl+Shift+R` i `Ctrl+Shift+T`: po załadowaniu materiału
  powinny korzystać z jego rzeczywistego czasu.
- Lewa strzałka na liście oraz `NVDA+End`: format, częstotliwość i bitrate są
  podawane tylko wtedy, gdy oficjalny Player rzeczywiście je zwrócił.
- Escape: powrót dokładnie do poprzedniej listy zgodnie z ogólnym ustawieniem
  zatrzymywania po wyjściu z odtwarzacza.
- Kilka razy szybko użyj `Page Up`, `Page Down`, pauzy i wznowienia. Starsze
  oczekujące żądanie nie powinno później uruchomić poprzedniego utworu ani
  odebrać fokusu.

## Błędy kontrolowane

- Przy braku sieci ponów odtwarzanie i sprawdź krótki komunikat bez adresu
  technicznego, tokenu ani reprezentacji obiektu.
- Po odzyskaniu sieci spróbuj ponownie; nie powinno być konieczne ponowne
  uruchomienie AMC.
- Jeżeli logowanie wygasło, odśwież je przez `Ctrl+F5` i powtórz test.

## Weryfikacja automatyczna

- Pełna kompilacja Release: bez ostrzeżeń i błędów.
- Wszystkie testy rdzenia: zakończone powodzeniem.
- Wszystkie testy interfejsu Windows: zakończone powodzeniem.
- Test rdzenia potwierdza oddzielony tor TIDAL oraz przekazanie odtwarzania,
  przewijania i pauzy do właściwego wyjścia.
- Oficjalny Player, WebView2 i wymagane informacje licencyjne znajdują się w
  gotowym pakiecie; chronione adresy odtwarzania nie należą do modelu AMC.
