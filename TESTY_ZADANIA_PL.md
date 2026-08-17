# Zadania testowe AMC

- Numer zestawu: `AMC-TEST-037`
- Tytuł zestawu: Sterowanie z listy i stan odtwarzanego elementu
- Wersja programu: `0.1.0-alpha.37`
- Utworzono: 2026-08-17 14:52, Europe/Warsaw
- Plik wyników: `wyniki-testow/WYNIKI_2026-08-17_1452_0.1.0-alpha.37.md`

W pliku wyników po dwukropku wpisz krótko, co się wydarzyło. Jeżeli wszystko działa, wystarczy `OK`. Nie trzeba przed każdym zadaniem wybierać wariantu odpowiedzi.

Do prób odtwarzania otwórz przez `Ctrl+O` co najmniej dwa pliki, w tym jeden trwający ponad dwie minuty.

## AMC-037-01 — Pierwsza zmiana sesji

1. Zaraz po uruchomieniu użyj kolejno dwóch przypisanych skrótów `Ctrl+cyfra`.
2. Powtórz przełączanie po kilku sekundach.

Oczekiwane:

- za pierwszym i kolejnym razem NVDA podaje numer sesji, usługę oraz element;
- nie pojawia się „Stan programu” ani „Stan programu, Stan programu”;
- fokus pozostaje na liście.

## AMC-037-02 — Odtwarzany i wstrzymany element

1. Na pierwszym pliku naciśnij `Ctrl+Enter`.
2. Przejdź strzałką na drugi plik, a następnie wróć na pierwszy.
3. Wstrzymaj odtwarzanie Spacją i ponownie odczytaj pierwszy plik.

Oczekiwane:

- aktywny plik jest oznaczony na liście słowem „Odtwarzany”;
- po pauzie ten sam plik jest oznaczony słowem „Wstrzymany”;
- zaznaczenie nie jest samoczynnie przenoszone do bieżącego pliku.

## AMC-037-03 — F6 i zapamiętane miejsce

1. Uruchom pierwszy plik, po czym przejdź zaznaczeniem na drugi bez jego odtwarzania.
2. Naciśnij F6, a potem Escape.

Oczekiwane:

- F6 pokazuje odtwarzacz pierwszego pliku;
- Escape wraca do drugiego, ostatnio przeglądanego pliku;
- pierwszy plik nadal gra i pozostaje oznaczony jako „Odtwarzany”.

## AMC-037-04 — Skróty czasu

1. Na liście sprawdź `Ctrl+Shift+E`, `Ctrl+Shift+R` i `Ctrl+Shift+T`.
2. Powtórz je w odtwarzaczu otwartym klawiszem F6.

Oczekiwane:

- skróty podają odpowiednio czas od początku, pozostały i całkowity;
- działają niezawodnie zarówno na liście, jak i w odtwarzaczu;
- dawne skróty `Ctrl+E/R/T` nie są już prezentowane w palecie jako skróty okna.

## AMC-037-05 — Trzy kroki przewijania

1. W odtwarzaczu sprawdź lewo/prawo.
2. Sprawdź `Shift+lewo/prawo`.
3. Sprawdź `Ctrl+lewo/prawo`.

Oczekiwane:

- bez modyfikatora pozycja zmienia się o około 10 sekund;
- z Shiftem — o około 30 sekund;
- z Ctrl — o około minutę;
- zwykłe strzałki na liście nadal nie sterują transportem.

## AMC-037-06 — Sterowanie bez otwierania odtwarzacza

1. Wróć Escape do listy i ustaw zaznaczenie na innym pliku niż odtwarzany.
2. Spacją wstrzymaj i wznów bieżące odtwarzanie.
3. `Ctrl+Enter` uruchom zaznaczony plik i sprawdź, czy lista pozostała otwarta.

Oczekiwane:

- Spacja steruje tym, co faktycznie gra, niezależnie od zaznaczenia;
- `Ctrl+Enter` steruje zaznaczeniem bez przejścia do odtwarzacza;
- dopiero Enter lub F6 otwiera odtwarzacz.

## AMC-037-07 — Przyciski odtwarzacza

1. Otwórz odtwarzacz i przejdź Tabem przez jego przyciski.
2. Użyj przycisku odtwarzania, przewijania, głośności i powrotu.

Oczekiwane:

- przyciski mają jednoznaczne nazwy i działają zgodnie z nazwami;
- przycisk „Wróć do listy” zachowuje się tak jak Escape;
- komunikaty nie nakładają się na fokus.

Uwaga: Alt+strzałki oraz Ctrl+Shift+strzałki w odtwarzaczu pozostają obecnie wolne. Konfiguracja lokalnych skrótów odtwarzacza jest zapisana jako późniejsze rozszerzenie ustawień.
