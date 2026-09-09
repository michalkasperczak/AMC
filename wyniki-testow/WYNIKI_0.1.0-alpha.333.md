# Wyniki testów AMC 0.1.0-alpha.333

Na początku opisz zauważone zachowanie. Nie trzeba przy każdym punkcie wpisywać
wariantu „OK” albo „błąd”. Po dwukropku wpisuj spację.

## Uwagi

## Odtwarzanie TIDAL po uruchomieniu

- Nie loguj się ponownie. Uruchom AMC ze wcześniej zapisanym kontem TIDAL.
- Otwórz album **Współgłosy** i uruchom utwór **Usta**.
- Sprawdź drugi zwykły utwór z innego albumu.
- Jeżeli poziom dostępu aplikacji pozwala tylko na próbkę, AMC powinien
  powiedzieć to wprost. Nie powinien podawać ogólnego polecenia ponownego
  logowania.

## Błąd i kolejna pozycja

- Jeżeli wybrany materiał nie zostanie odtworzony, odczekaj kilka sekund bez
  naciskania klawiszy.
- AMC nie może samo przejść do Darii ze Śląska, następnej pozycji albumu,
  Ulubionych ani Kolejki.
- Ponów ten sam utwór raz. Następnie ręcznie wybierz inny utwór.

## Pauza i nawigacja

- Po rozpoczęciu odtwarzania sprawdź Enter lub Spację, przewijanie strzałkami,
  cyfry `0–9` oraz `Page Up` i `Page Down`.
- Escape powinien wrócić do listy i właściwego elementu bez utraty fokusa.

## Logowanie i synchronizacja

- `Ctrl+F5 → Synchronizuj teraz` powinno korzystać z zapisanego logowania.
- Nie wybieraj ponownego logowania, jeżeli synchronizacja jest udana, a błąd
  dotyczy tylko konkretnego materiału.
- Ponowne logowanie ma być potrzebne dopiero po jawnym komunikacie o odrzuconej
  lub wygasłej autoryzacji.

## Weryfikacja automatyczna

- Pełna kompilacja Release: bez ostrzeżeń i błędów.
- Wszystkie testy rdzenia: zakończone powodzeniem.
- Wszystkie testy interfejsu Windows: zakończone powodzeniem.
- Oficjalny host Playera zawiera token i `userId`, lecz nie zapisuje tokenu ani
  chronionych adresów w logu, stanie lub schowku.
