# SCHED-002 — jednorazowe pominięcie terminu nagrywania

## Status

Kod w bieżącej aplikacji wxPython i testy celowane ukończone 23.11.2026.
Nie przygotowano instalatora ani wydania; obowiązuje decyzja użytkownika z
23.11.2026 o wstrzymaniu wydania alpha.514. Pozostaje ręczny odbiór NVDA.

## Cel i zakres

Użytkownik chce pominąć najbliższe nagranie cyklicznego harmonogramu, na
przykład dzisiejszy poniedziałek, bez wyłączania nagrania w następny
poniedziałek. `Spacja` nadal przełącza cały plan. `Shift+Spacja` przełącza
pominięcie wyłącznie najbliższego przyszłego terminu. Drugie naciśnięcie
przed terminem przywraca nagranie. Funkcja dotyczy obecnie Radia w profilach
AMC Test i AMC Public; pozostałe sesje nie mają wykonawczego harmonogramu.

## Materiał do dokumentacji użytkownika

Otwórz „Harmonogram nagrywania” przez `Ctrl+Shift+H` w sesji Radia. Zaznacz
włączony plan i naciśnij `Shift+Spacja`. AMC poda „Pominięto termin” z datą,
godziną i nazwą planu. Na liście pojawi się „ten termin pominięty”. Plan
pozostanie włączony, a po upływie pominiętego terminu wróci do zwykłego
cyklu. `Shift+Spacja` przed terminem przywraca nagranie; AMC poda
„Przywrócono termin”. Zwykła `Spacja` włącza lub wyłącza cały plan.
Te same działania są w menu `Shift+F10` i przyciskach dialogu. `Ctrl+F1`
odczytuje pomoc listy, a `Ctrl+Shift+K` otwiera jej polecenia. Po zapisie
fokus wraca do listy. Gdy plan jest wyłączony, trwa już nagrywanie lub
termin się rozpoczął, AMC wyjaśnia, dlaczego nie pomija terminu.

## Uzasadnienie i decyzje

- Korekta użytkownika z 23.11.2026 zastępuje starszą globalną instrukcję
  wskazującą WPF jako kanoniczne AMC. Praca dotyczy wyłącznie wxPython.
- Pominięcie zapisuje start konkretnego wystąpienia. Wykonawca nie uruchamia
  tego nagrania ani wybudzania komputera, także po restarcie w jego oknie.
  Po upływie terminu przesuwa plan do następnego wystąpienia i czyści znacznik.
- Polecenie odmawia zmiany, gdy termin się rozpoczął lub trwa nagrywanie.
  Pozwala to uniknąć zaskakującego ucięcia już zapisywanego pliku.
- Pierwotna decyzja SCHED-001 o zwykłej `Spacji` pozostaje ważna; nowe
  polecenie ma osobny gest i nie zmienia sposobu powtarzania planu.

## Zmiany źródeł

- `wxlite/amc_wx_lite/radio_schedule_dialogs.py`: klawisz, przycisk, menu,
  lokalna pomoc/paleta, komunikat i etykieta stanu.
- `src/AccessibleMediaController.LiteHost/LiteRadioRecordingCoordinator.Schedules.cs`:
  wykonanie pominięcia, trwałość po synchronizacji i ochrona wybudzania.
- `src/AccessibleMediaController.Core/Configuration/RadioScheduleCalculator.cs`
  oraz `Presentation/RadioSchedulePresentation.cs`: wspólna decyzja i etykieta.
- `wxlite/tests/test_radio_schedule_skip.py`,
  `wxlite/tests/test_radio_schedule_keyboard.py`,
  `tests/AccessibleMediaController.Core.SmokeTests/Program.cs`: testy.
- `AGENTS.md`, globalne `C:\Users\micha\.codex\AGENTS.md`,
  `wxlite/JAK_TESTOWAC.md`, ten plik i rejestr zadań: korekta wymagań
  oraz instrukcja odbioru.

## Testy i rzeczywisty wynik

- 40/40 testów harmonogramów Pythona, w tym izolowany test prawdziwego
  LiteHosta: po pominiętym terminie brak aktywnego nagrania i historii,
  a włączony plan wskazuje ten sam dzień następnego tygodnia.
- Celowany test Core `--radio-schedule`: zaliczony.
- Kompilacja LiteHost Release `--no-restore`: zaliczona, 0 ostrzeżeń,
  0 błędów.
- Pierwszy test uruchomiony prywatnym interpreterem bez pytest dał błąd
  środowiska; ponowiono poprawnym, dostępnym środowiskiem `uv` offline.
  Pośredni test wykrył błąd długości generatora menu, naprawiony przed
  wynikiem 40/40.

## Znane ograniczenia i następny krok

- Brak ręcznego odsłuchu NVDA. Odbiór: pierwszy fokus, strzałki, `Shift+Spacja`
  dwukrotnie, menu i pomoc, zamknięcie i ponowne otwarcie, powrót fokusu.
- Zmiana jest tylko w źródłach; nie jest częścią działającej instalacji ani
  opublikowanego wydania. Przy następnym uzgodnionym wydaniu dołączyć ją do
  obu profili po odpowiedniej bramce testów i integralności pakietów.

## Wersja i commit

- Baza źródeł na początku pracy: `0f58310d` na
  `codex/amc-test-public-20261109`.
- Zmiany tego zadania są jeszcze niezatwierdzone w Git; bez nowego commitu.
- Instalacja i publikacja: nie wykonano, zgodnie z wstrzymaniem alpha.514.
