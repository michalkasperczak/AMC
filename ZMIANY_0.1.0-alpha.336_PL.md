# AMC 0.1.0-alpha.336 — TIDAL: kolekcje, komunikaty i fokus

Data: 10 września 2026.

## Poprawki

- Dodanie lub usunięcie albumu/utworu z wyników wyszukiwania nie otwiera
  wyniku i nie zmienia listy w tle. Escape zachowuje miejsce powrotu.
- Szybkie ponowienie Ctrl+Shift+L albo Ctrl+Shift+U uwzględnia poprzednią
  potwierdzoną operację. Delete zachowuje jednoznaczną funkcję usunięcia.
- Aktualizacja kolekcji chroni fokus wiersza; aktualizacja wyników
  wyszukiwania zmienia ich etykiety w miejscu, bez przebudowy kontrolek.
- Komunikaty powodzenia i błędu zapisu nie przejmują fokusa z pola
  wyszukiwania. Nie trafiają do nowego okna wyszukiwarki otwartego później.
- Spóźnione otwarcie albumu/wykonawcy oraz jego błąd nie przenosi użytkownika
  z powrotem po Escape, zmianie sesji, widoku lub zaznaczenia.
- Stany kontenerów w pamięci są aktualizowane razem z kolekcją; diagnostyka
  rozróżnia niewłaściwy rodzaj polecenia i kod HTTP niedostępnego albumu.

## Weryfikacja

Kompilacja Release: bez błędów i ostrzeżeń. Pełne testy Core i Windows:
zakończone poprawnie. Nowy zestaw `TidalInteractionSmokeTests` sprawdza
seryjne żądania POST/DELETE, nieudany zapis i ponowienie, granice kontekstu
odpowiedzi, fokus wiersza oraz filtra, aktualizację nazwy dostępnościowej
bez wymiany wiersza i powtórne otwarcie wyszukiwarki.

Testy używają sztucznych danych i odpowiedzi HTTP. Nie modyfikowano
Biblioteki ani Ulubionych na prawdziwym koncie użytkownika. Nowy test został
odizolowany od kontekstu wątku interfejsu pozostawianego przez wcześniejsze
testy; po tej poprawce powtórzono cały przebieg.

Do sprawdzenia przez użytkownika: rzeczywiste wypowiedzi NVDA, wolna sieć
i wybrane albumy na koncie, według `TESTY_0.1.0-alpha.336_PL.md`. Test nazw
UI Automation nie zastępuje odsłuchu NVDA.

## Bez zmian

Ta wersja nie odblokowuje pełnych utworów zamiast próbek TIDAL. Nie zmienia
logowania, harmonogramów, formatów nagrań ani ustawień wyjść audio. Nie
usunięto elementów konta z powodu odpowiedzi 404.

Foldery uruchomieniowe alf 334 i 335 zostały zastąpione folderem 336 przez
skrypt wydania; wcześniejsze archiwa ZIP pozostają w `publish` jako kopie
umożliwiające powrót. Dane użytkownika znajdują się poza tymi pakietami.
