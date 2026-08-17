# Zadania testowe AMC

- Numer zestawu: `AMC-TEST-033`
- Tytuł zestawu: Pierwsze rzeczywiste odtwarzanie lokalne
- Wersja programu: `0.1.0-alpha.33`
- Utworzono: 2026-08-17 00:38, Europe/Warsaw
- Plik wyników: `wyniki-testow/WYNIKI_2026-08-17_0038_0.1.0-alpha.33.md`

W pliku wyników po dwukropku wpisz krótko, co się wydarzyło. Jeżeli wszystko działa, wystarczy `OK`. Nie trzeba przed każdym zadaniem wybierać wariantu odpowiedzi.

Do próby wybierz jeden lub kilka zwykłych, krótkich plików audio, najlepiej MP3, WAV, M4A albo FLAC. Test nie zmienia ani nie usuwa wybranych plików.

## AMC-033-01 — Otwarcie bez samoczynnego odtwarzania

1. Uruchom AMC i naciśnij `Ctrl+O`.
2. Wybierz jeden plik audio i zatwierdź okno systemowe.

Oczekiwane:

- plik nie zaczyna grać samoczynnie;
- fokus wraca na jego pozycję w sesji `Lokalne multimedia`;
- NVDA podaje nazwę sesji, miejsce sesji, informację o dodaniu pliku oraz tytuł jako jedną wypowiedź fokusową.

## AMC-033-02 — Odtwarzanie, pauza i Spacja

1. Na pliku naciśnij Enter i sprawdź rzeczywisty dźwięk.
2. Naciśnij ponownie Enter, a następnie `Ctrl+Enter`.
3. Przejdź na inną pozycję, jeśli jest dostępna, i naciśnij Spację.

Oczekiwane:

- pierwsze użycie Entera uruchamia plik, drugie wstrzymuje, a `Ctrl+Enter` wznawia;
- Spacja steruje plikiem, który faktycznie gra, niezależnie od zaznaczenia;
- komunikaty `Odtwarzanie` i `Pauza` odpowiadają słyszanemu stanowi.

## AMC-033-03 — Czas, przewijanie i głośność

1. Podczas odtwarzania użyj prefiksu, a następnie `Ctrl+E`, `Ctrl+R` i `Ctrl+T`.
2. Użyj prefiksu i strzałki w prawo, a następnie sprawdź czas od początku.
3. Użyj prefiksu i strzałek w górę oraz w dół.

Oczekiwane:

- czasy odpowiadają prawdziwemu plikowi i zmieniają się podczas odtwarzania;
- strzałka w prawo rzeczywiście przesuwa dźwięk o około 10 sekund;
- zmiana głośności wpływa na plik, nie wycisza NVDA i jest potwierdzana wartością procentową.

## AMC-033-04 — Kilka plików i sesja 4

1. Ponownie naciśnij `Ctrl+O` i wybierz kilka plików, w tym jeden już wcześniej dodany.
2. Sprawdź listę sesji przez `Ctrl+0`, a następnie `Ctrl+4`.

Oczekiwane:

- nowe pliki są dopisywane, a ten sam plik nie tworzy duplikatu;
- `Lokalne multimedia` występują jako sesja 4, jeśli miejsce 4 było wolne;
- `Ctrl+4` wraca do listy lokalnej i nie uruchamia dźwięku samoczynnie.

## AMC-033-05 — Paleta i menu Plik

1. W palecie `Ctrl+Shift+K` wyszukaj `otwórz lokalne pliki audio`.
2. Sprawdź, czy pozycja podaje `Ctrl+O`, i zamknij paletę.
3. Otwórz menu Plik i odszukaj `Otwórz pliki audio`.

Oczekiwane:

- polecenie jest dostępne i czytelne w obu miejscach;
- samo przechodzenie po menu ani palecie nie otwiera okna plików.

## AMC-033-06 — Zakończenie i błąd formatu

1. Pozwól krótkiemu plikowi dojść do końca albo wybierz celowo nieobsługiwany plik przez wariant `Wszystkie pliki`.
2. Po komunikacie spróbuj ponownie otworzyć i odtworzyć prawidłowy plik.

Oczekiwane:

- po naturalnym końcu słychać krótki komunikat `Koniec`;
- błąd formatu jest komunikatem, a nie zawieszeniem lub zamknięciem AMC;
- po błędzie prawidłowy plik nadal można odtworzyć.

Uwaga: w tej wersji lista lokalnych plików jest tymczasowa i znika po zamknięciu AMC. To zachowanie zaplanowane dla pierwszego testu toru dźwięku.
