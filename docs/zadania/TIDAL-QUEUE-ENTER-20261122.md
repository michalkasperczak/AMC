# Kolejka TIDAL: Lewa strzałka, Enter i mowa

Stan: 22.11.2026. Kandydat kodu w gałęzi `codex/alpha512-update-candidate-20261122`. Commity `b6cc7c0b` (długość utworu) i `0ed160af` (Enter i mowa). Nie zainstalowano ani nie wydano.

## Zachowanie

- Znana długość utworu przechodzi przez zapis i ponowne otwarcie prywatnej Kolejki TIDAL. Lewa strzałka czyta długość i tytuł jak na liście albumu. Nagłówek Kolejki może podać łączny znany czas. Gdy źródło nie zna długości, program jej nie wymyśla.
- `Ustawienia sesji` → zachowanie Entera: przy „pozostań na liście” ręcznie uruchomiony utwór TIDAL nie otwiera odtwarzacza, zachowuje nowszy wybór na liście i nie dodaje zapowiedzi startu. Przy „otwórz odtwarzacz” zachowuje dotychczasowy widok i komunikat startu. Ponowny Enter na już odtwarzanym utworze respektuje ten wybór.
- Przy faktycznym automatycznym przejściu do następnego utworu NVDA ma usłyszeć raz sam tytuł; bieżący widok pozostaje listą albo odtwarzaczem. Spóźnione zdarzenie końca poprzedniego utworu nie może uruchomić następcy, jeśli nowe żądanie Enter jest już w toku.
- Nie dodano nowego skrótu ani polecenia. Menu, paleta i pomoc korzystają z istniejącego ustawienia sesji.

## Przyczyna i granice danych

Serializacja Kolejki TIDAL oraz filtr stanu prywatnego odrzucały `durationTicks`, chociaż wiersz z katalogu lub albumu mógł zawierać tę wartość. Handler Lewej strzałki potrafił ją przeczytać, lecz po ponownym otwarciu kolejki widział tylko wykonawcę. Teraz liczba jest przenoszona i sprawdzana jako nieujemna liczba całkowita; wartość brakująca lub błędna staje się nieznana.

W ścieżce odtwarzania potwierdzenie startu bezwarunkowo ustawiało widok odtwarzacza i wypowiadało tytuł z komunikatem. Nowa ścieżka odróżnia ręczny start od automatycznego następcy i czyta istniejące ustawienie sesji. Spóźniony `ended` wcześniejszego utworu jest ignorowany, gdy przygotowywany jest inny utwór.

Starsze wpisy prywatnej kolejki zapisane przed tą poprawką nie zawierają długości. Bez ponownego uzyskania metadanych pozostaną z czasem nieznanym; po ponownym dodaniu utworu z listy znającej długość nowy zapis już ją zachowa. TIDAL nie dostarcza w tym wierszu pewnego bitrate ani rozmiaru; nie dodano tych parametrów do mowy.

## Testy i odbiór

- `test_gui_logic.py`, `test_tidal_source.py`, `test_tidal_navigation.py`, `test_state_store.py`: **138 passed** po zmianie czasu Kolejki.
- `test_gui_logic.py`, `test_tidal_native_gui.py`, `test_tidal_navigation.py`, `test_session_options_wiring.py`: **115 passed, 1 deselected** po zmianie Entera. Wyłączony test pomocy kontekstowej pada także na niezmienionej gałęzi kanonicznej: atrapa okna nie ma `_in_download_history`. Nie jest to zielony pełny zestaw.
- Nie wykonano próby z rzeczywistym kontem TIDAL, odsłuchu, NVDA, builda ani pełnego zestawu dla tych commitów. Wymagany jest odbiór obu ustawień Entera, automatycznego przejścia, szybkiego ponownego Entera, fokusu i mowy.

## Szkic instrukcji i listy zmian do korekty Michała

W Ustawieniach sesji TIDAL wybierz, czy Enter ma otwierać odtwarzacz, czy zostawić fokus na liście. W Kolejce Lewa strzałka czyta znaną długość zaznaczonego utworu. Gdy kolejny utwór zacznie grać automatycznie, AMC poda jego tytuł.

Lista zmian: Kolejka TIDAL zachowuje czas utworu, respektuje ustawienie Entera i nie uruchamia dodatkowego następcy od spóźnionego zdarzenia końca.
