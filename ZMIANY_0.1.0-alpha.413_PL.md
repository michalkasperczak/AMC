# AMC 0.1.0-alpha.413 — głośniki i grupy Sonos

## Co się zmienia

- W oknie Konto Sonos dostępny jest przycisk Głośniki i grupy.
- Lista pokazuje domy Sonos, grupy, należące do nich głośniki i zgłoszony stan odtwarzania grupy.
- Przy kilku domach można wybrać właściwy. Odświeżanie zachowuje ten wybór i miejsce klawiatury.
- Czytnik otrzymuje informację o wczytywaniu, wyniku, niepełnej liście lub błędzie. Escape pozwala zamknąć okno także podczas oczekiwania.
- Poprawiono ochronę przed spóźnionym wynikiem odczytu po zmianie konta.

## Ważne ograniczenie

To etap odczytu urządzeń. Nie dodaje jeszcze osobnej sesji Sonos, odtwarzacza, sterowania muzyką, regulacji głośności ani presetów. Enter na liście niczego nie uruchamia; zamknięcie okna nie zatrzymuje muzyki na urządzeniu.

## Sprawdzenie dostępności

Przed poprawką odświeżenie z wiersza mogło zgubić miejsce klawiatury i wywołać ponowny odczyt całego okna. Po poprawce żywy NVDA potwierdził powrót na właściwy wiersz także po zmianie kolejności długiej listy oraz działanie strzałek. Pomiar okien używał danych próbnych; nie jest dowodem odpowiedzi konta użytkownika.
