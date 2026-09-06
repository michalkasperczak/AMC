# Wyniki testów AMC 0.1.0-alpha.295

Wpisuj obserwacje pod odpowiednim zadaniem. Nie trzeba przy każdym punkcie
dopisywać osobnego słowa „OK” albo „błąd”. Po dwukropku wpisuj spację.

## AMC-295-01 — T podczas nagrania z harmonogramu

Uruchom nagranie przez harmonogram. W widoku **Nagrywane** ustaw fokus na tej
stacji, odczekaj co najmniej pięć sekund i naciśnij `T`.

Oczekiwane: AMC zapisuje bieżącą część, rozpoczyna następny plik i nadal
pokazuje stację jako nagrywaną. Nie podaje komunikatu o nagraniu ręcznym.

Wynik:

## AMC-295-02 — T w odtwarzaczu nagrywanej stacji

Podczas tego samego zaplanowanego nagrania otwórz stację w odtwarzaczu i po
co najmniej pięciu sekundach naciśnij `T`.

Oczekiwane: powstaje kolejna część tego samego nagrania, a odtwarzanie i
harmonogram pozostają aktywne.

Wynik:

## AMC-295-03 — ochrona szybkiego podwójnego T

Po rozpoczęciu nowej części szybko naciśnij `T` ponownie.

Oczekiwane: AMC pomija zbyt wczesny podział i nie zatrzymuje harmonogramu.

Wynik:

## AMC-295-04 — przyszły termin harmonogramu

Po zakończeniu bieżącego nagrania otwórz `Ctrl+Shift+H` i sprawdź plan
cykliczny.

Oczekiwane: ręczny podział części nie wyłączył planu i nie zmienił jego
następnego terminu.

Wynik:

## AMC-295-05 — regresja nagrania ręcznego

Rozpocznij zwykłe nagranie radia i podziel je `T` z odtwarzacza oraz z widoku
**Nagrywane**.

Oczekiwane: dotychczasowy podział nagrania ręcznego nadal działa.

Wynik:

## Inne obserwacje
