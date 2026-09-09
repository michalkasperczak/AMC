# Wyniki testów AMC 0.1.0-alpha.330

Na początku opisz zauważone zachowanie. Nie trzeba przy każdym punkcie wpisywać
wariantu „OK” albo „błąd”. Po dwukropku wpisuj spację.

## Uwagi

## Test logowania i trwałości TIDAL

- Po ewentualnym jednorazowym logowaniu i synchronizacji zamknij AMC oraz
  uruchom ponownie. Sprawdź Bibliotekę, Ulubione i Kolejkę.
- Jeśli możesz, po udanej synchronizacji wykonaj jeden start bez internetu.
  Biblioteka nie powinna być pusta; operacje sieciowe mogą zgłosić błąd.
- Po przywróceniu internetu użyj **Synchronizuj teraz** i sprawdź, czy nie ma
  duplikatów ani zmiany kolejności.

## Weryfikacja automatyczna

- Pełna kompilacja Release: bez ostrzeżeń i błędów.
- Wszystkie testy rdzenia i interfejsu Windows: zakończone powodzeniem.
- Żądanie odświeżenia zawiera `client_id`, rodzaj operacji, token odświeżający
  i pełny zakres uprawnień zgodnie z oficjalnym modułem TIDAL.
- Odpowiedź bez obróconego tokenu zachowuje dotychczasowy token odświeżający.
- Lokalna kopia kolekcji zachowuje tytuły, rodzaje, członkostwo, czasy i
  rzeczywistą kolejność dodania, bez przechowywania poświadczeń.
