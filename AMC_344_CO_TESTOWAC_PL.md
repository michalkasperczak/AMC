# AMC 0.1.0-alpha.344 — co testować

## Dlaczego ta wersja powstała

Wersja 343 zepsuła logowanie do TIDAL-a. Wersja 344 to naprawia.

W 343 dopisałem do żądania logowania dwa uprawnienia: `r_usr` i `w_usr`.
Miały odblokować pełne utwory. Skutek był odwrotny: strona
login.tidal.com odrzucała **całe logowanie** błędem 1002 („Coś poszło nie
tak"), przeglądarka nigdy nie wracała do programu, a logowanie kończyło
się po pięciu minutach oczekiwania. Zamiast pełnych utworów wyszedł brak
możliwości zalogowania.

W 344 te dwa uprawnienia są usunięte. Logowanie wraca do stanu z 342.

## Co przetestować

1. **Logowanie do TIDAL-a.** Menu, wyloguj się, zaloguj ponownie.
   Oczekiwane: strona logowania TIDAL-a otwiera się normalnie i po
   zalogowaniu wraca do AMC. Nie powinno być błędu 1002.

2. **Odtwarzanie z TIDAL-a.** Włącz dowolny utwór.
   Oczekiwane: gra około 30 sekund i kończy. To niestety poprawne
   zachowanie, wyjaśnienie poniżej.

3. **Reszta programu.** Radio, podkasty, biblioteka lokalna — czy nic
   nie ucierpiało. W 344 zmieniony jest tylko plik odpowiedzialny za
   logowanie do TIDAL-a, więc nie powinno być różnic.

## Pełne utwory z TIDAL-a: dlaczego ich nie ma

To nie jest błąd w AMC i nie da się tego naprawić kodem.

Oficjalna dokumentacja TIDAL-a dla programistów mówi wprost, że moduł
odtwarzacza w SDK to jedyna dozwolona droga odtwarzania treści TIDAL-a
przez programy zewnętrzne — i że tą drogą programy zewnętrzne mogą
odtwarzać **próbki**. Regulamin dla programistów potwierdza to samo,
a oficjalny przykład „na start" nazywa się „odtworzenie próbki utworu".

Źródła:
- https://developer.tidal.com/documentation/api-sdk/api-sdk-overview
- https://developer.tidal.com/documentation/guidelines/guidelines-developer-terms

Ograniczenie dotyczy rejestracji aplikacji, nie konta. Subskrypcja
TIDAL HiFi tego nie zmienia. Pełne utwory wymagają zgody nadanej przez
TIDAL tej konkretnej rejestracji aplikacji — czyli odpowiedzi na
zgłoszenie do TIDAL-a.

Moja pierwotna diagnoza (brak `r_usr`) była błędna. Starsze API
faktycznie autoryzuje się tym uprawnieniem, ale TIDAL nie nadaje go
aplikacjom zewnętrznym. Powinienem był sprawdzić regulamin przed
wypuszczeniem 343.

W kodzie jest teraz wyraźne ostrzeżenie, żeby nikt nie dopisał tych
uprawnień ponownie i nie zepsuł logowania po raz drugi.

## Gdzie jest program

D:\Projekty Codex\Hermes\AMC-344\AccessibleMediaController.exe

## Stan testów

Kompilacja na komputerze głównym: 0 błędów, 0 ostrzeżeń.
