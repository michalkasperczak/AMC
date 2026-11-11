# AM-003 — sesja Apple Music i dostępny interfejs AMC

- Status: `Oczekuje — wymaga minimalnie działającego odczytu AM-001 i odtwarzania AM-002`
- Utworzono: 12 listopada 2026
- Obszar: sesje, nawigacja, klawiatura, menu, pomoc i NVDA

## Cel

Dodać Apple Music jako sesję AMC Test zgodną z obecnym, lepszym modelem TIDAL:
Biblioteka, wykonawcy, albumy, playlisty, wyszukiwanie, relacje pod prawą
strzałką, Enter do odtwarzania, transport, kolejka, historia i jawne operacje
Biblioteki, ulubionych oraz playlist.

## Uzasadnienie i decyzje

- Jedno źródło prawdy definiuje nazwę, opis, skrót, widoczność, enabled i
  wykonanie. Z niego korzystają klawiatura, menu, kontekst, `Ctrl+F1`, pełna
  pomoc i `Ctrl+Shift+K`.
- Sesja nie pojawia się, dopóki rzeczywiste adaptery nie zgłoszą swoich
  zdolności. Brak odtwarzacza nie może udawać działającego Enter, a brak prawa
  zapisu nie może pokazywać aktywnego usuwania lub tworzenia playlist.
- `Ctrl+F5` zarządza kontem w niemodalnym oknie. Powrót z logowania skupia
  „Sprawdź logowanie”; ponowne otwarcie nie rozpoczyna OAuth automatycznie.
- Listy przyjmują tylko jawne etykiety użytkowe. ID Apple, URL, obiekty Python,
  nazwy pól i rekordy API nigdy nie są mową NVDA.
- Pamięć playlist i celów jest dzielona według profilu, dostawcy i konta.
  Ponowne otwarcie używa migawki, lecz przed zapisem adapter ponownie sprawdza
  uprawnienie i możliwość edycji.

## Plan poleceń do potwierdzenia po działającym pilocie

- Enter: odtwórz wybrany utwór w webowym silniku Apple.
- Prawa strzałka: albumy/utwory/podobni wykonawcy oraz relacje utworu.
- `Ctrl+Shift+L`: dodaj lub usuń z Biblioteki, jeżeli typ jest obsługiwany.
- `Ctrl+Shift+U`: dodaj lub usuń z ulubionych, jeżeli API pilota to potwierdzi.
- Zarządzanie playlistą: wspólny mechanizm wyboru celu, bez surowych rekordów.
- Transport, kolejka, historia, wyszukiwanie i odczyt stanu: te same kontrakty
  co równoważne sesje, ale tylko dla faktycznie dostępnych możliwości Apple.

## Materiał do dokumentacji użytkownika

To jest projekt interfejsu, nie działająca funkcja. Ostateczne nazwy i skróty
zostaną pokazane dopiero razem z działającym zapleczem i testami parytetu.

## Testy i wynik

Brak kodu, testów, buildu, instalacji i NVDA. Kryteria obejmą początkowy fokus,
strzałki, Enter, Escape, anulowanie, powrót fokusu i ponowne otwarcie, a także
zgodność nazw i wykonania na wszystkich powierzchniach poleceń.

## Znane ograniczenia

Zakres zapisów do Biblioteki, ulubionych i playlist zależy od wyniku AM-001.
Transport i kolejka zależą od AM-002. AMC Public jest poza tym zadaniem.

## Wersja i commit

Brak zmian kodu i wersji. Commit: do uzupełnienia.
