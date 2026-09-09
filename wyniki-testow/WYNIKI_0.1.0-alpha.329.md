# Wyniki testów AMC 0.1.0-alpha.329

Na początku opisz zauważone zachowanie. Nie trzeba przy każdym punkcie wpisywać
wariantu „OK” albo „błąd”. Po dwukropku wpisuj spację.

## Uwagi

## Weryfikacja automatyczna

- Pełna kompilacja i publikacja Release: bez ostrzeżeń i błędów.
- Wszystkie testy rdzenia i interfejsu Windows: zakończone powodzeniem.
- Parser kolekcji zachowuje relacyjną datę `addedAt`, nawet gdy obiekty
  techniczne odpowiedzi przychodzą w innej kolejności.
- Niepełny katalog zdalny zachowuje także chwilowo niewidoczne identyfikatory
  zapisanej kolejności.
- Pełna synchronizacja odbudowuje rzeczywisty porządek od najstarszego do
  najnowszego; interfejs pokazuje go odwrotnie, z najnowszymi na początku.
