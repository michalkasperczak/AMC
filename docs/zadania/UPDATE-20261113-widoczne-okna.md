# UPDATE-20261113 — widoczny wynik aktualizacji AMC

Data: 13.11.2026. Produkt: AMC Test wxPython. Stan: źródła i testy; brak
nowego buildu, instalacji i wydania w chwili zapisu.

## Zgłoszenie i korekta

Użytkownik ręcznie aktualizował AMC podczas wcześniejszego nagrywania.
Okno instalacji lub wyniku mogło być ukryte, a ponowne „Sprawdź aktualizację
AMC” nie dało widocznego rezultatu. Potwierdził zakończenie nagrywania.
Istniejący aktualizator dokończył instalację alpha.500: manifest zainstalowany
pod `D:\Projekty Codex\AMC Python` wskazuje wersję `13.11.2026, 19:52`
i źródło `16eb05b5`; działa nowe okno tej wersji. To nie potwierdza
odtwarzania Apple Music.

Decyzja z 13.11 zastępuje wcześniejszy opis wyłącznie mówionego błędu w
`UPDATE-20261101-ponowna-aktualizacja.md`. Ręczny wynik i błąd mają
pozostać widoczne do zamknięcia przez użytkownika, także gdy otwarte jest
inne okno AMC.

## Zachowanie użytkowe

- `F11`, menu Pomoc „Sprawdź aktualizację AMC” i paleta poleceń prowadzą do
  wspólnej akcji. Ręczna kontrola pokazuje okno wyniku na pierwszym planie.
  Powtórzenie podczas automatycznej kontroli przejmuje jej wynik i mówi:
  „Kończę sprawdzanie aktualizacji. Wynik pokażę na pierwszym planie”.
- Podczas pobierania lub instalacji kolejna kontrola nie uruchamia drugiej
  operacji; NVDA mówi: „Pobieranie lub instalacja aktualizacji już trwa”.
- Błąd kontroli, pobierania lub instalacji jest zarówno ogłaszany przez NVDA,
  jak i pokazany w modalnym oknie „Aktualizacja AMC”. Okno ma priorytet
  pierwszego planu; po zamknięciu fokus wraca do AMC.
- Automatyczna kontrola nadal czeka, aż inne okna AMC zostaną zamknięte,
  aby nie przerywać pracy użytkownika. Ręczne wywołanie ma pierwszeństwo.
- Nagrywanie i okno zaplanowanego nagrania nadal blokują instalację.

## Dowody i ograniczenia

`tests/test_application_updates.py` w przypiętym środowisku Python 3.14 /
wxPython 4.3.1: 23 testy zaliczone. Wspólny przebieg po zmianach Apple Music:
68 testów i 3 podtesty zaliczone. Zweryfikowano istnienie flagi
`wx.STAY_ON_TOP` i klasy `wx.MessageDialog` w rzeczywistym wxPython.
Ręczny odbiór NVDA, układ okna na pulpicie, build, instalacja i wydanie
alpha.501 pozostają do potwierdzenia. Nie uruchamiano drugiego instalatora
podczas pracy AMC.

## Wersja i commit

Kandydat źródłowy `0.1.0-alpha.501`; commit i identyfikator wydania należy
uzupełnić po zapisie i publikacji. Poprzednie wydanie alpha.500 jest
zainstalowane i uruchomione.
