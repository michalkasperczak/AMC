# YT-006 — wybór konkretnego wariantu pobrania

Status: **Oczekuje**. Decyzja użytkownika: 10.11.2026. Plan rozpoczęcia: 11.11.2026.

## Ustalenie użytkownika

Przemyśleć dostępny z klawiatury wybór adekwatnego pliku lub strumienia do pobrania, inspirowany blindDL. Obecnych prostych ustawień pobierania nie usuwać bez decyzji po sprawdzeniu rzeczywistych wariantów.

## Stan i zakres pracy

- `YT-004` daje pobieranie wideo jako MP4 lub najlepszy format oryginalny; `Ctrl+S` otwiera Zapisz jako, a `Ctrl+Shift+D` pobiera zaznaczone filmy. Zweryfikować rzeczywisty przebieg i odbiór NVDA tego zadania przed rozszerzeniem.
- Przy pojedynczym materiale YouTube zbadać listę wariantów dostępną przez zarządzany yt-dlp: obraz, dźwięk lub połączony plik, kodek, rozdzielczość, bitrate i przybliżony rozmiar, tylko gdy źródło rzeczywiście podaje te dane. Nie wyświetlać technicznego identyfikatora formatu jako zwykłej etykiety NVDA.
- Zaprojektować jawne polecenie „Wybierz wariant pobrania…” w odpowiednim kontekście. Lista ma mieć jednoznaczne nazwy, początkowy fokus, obsługę strzałek, Enter/Escape i powrót fokusu po pobraniu lub anulowaniu. Zwykłe pobranie ma zachować dotychczasowy prosty przebieg.
- Sprawdzić wygaśnięcie adresów i zmianę oferty między wyświetleniem listy a pobraniem. Wybrany wariant musi być zweryfikowany ponownie, bez cichego przejścia na inny format lub niezamówionego przekodowania. Osobno rozstrzygnąć, czy funkcja dotyczy też audio `Ctrl+D` i pobrań wielokrotnych.
- Przy implementacji uaktualnić wspólny model poleceń, menu, paletę, `Ctrl+F1`, pełną pomoc i instrukcję oraz testy ich zgodności.

## Uzasadnienie i decyzje

Źródło inspiracji: [blindDL](https://projects.serrebiradio.com/apps/blindDL/), opis wyboru dokładnego strumienia. To pomysł na interakcję, bez kopiowania kodu i bez przejmowania całego zakresu programu do pobierania. Najpierw ograniczyć projekt do pojedynczego materiału, aby nie mylić wyboru pliku z ustawieniem dla całej grupy.

## Materiał do dokumentacji użytkownika

Projekt do przyszłej korekty: „Na pojedynczym filmie możesz otworzyć listę dostępnych wariantów pobrania i wybrać jakość oraz format. AMC poda rozdzielczość, dźwięk i rozmiar, jeśli serwis je udostępni. Zwykłe pobieranie nadal działa bez otwierania tej listy.” Dokładny skrót i miejsce w menu zostaną ustalone wraz z projektem polecenia.

## Testy i wynik

Nie uruchamiano testów w ramach tego zadania. Zaplanować przypadki pojedynczego i brakującego formatu, zmiany oferty, anulowania, etykiet NVDA, fokusu, zgodności poleceń oraz prawdziwego krótkiego pobrania. Źródła, build, instalacja i publikacja: bez zmian.

## Znane ograniczenia

Serwis może nie podać wielkości, kodeka lub osobnego połączonego wariantu. Format wideo i audio mogą wymagać technicznego połączenia bez ponownego kodowania; komunikat musi to wyjaśniać, gdy ma znaczenie dla wyboru.

## Wersja i commit

Nie dotyczy przed rozpoczęciem implementacji. Zadanie dokumentacyjne, bez zmiany programu.
