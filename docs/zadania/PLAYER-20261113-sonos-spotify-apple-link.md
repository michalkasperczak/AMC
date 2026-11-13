# PLAYER-20261113 — Sonos, Spotify i łącze Apple Music

- Status: **kod i testy celowane gotowe; wydanie w toku**.
- Utworzono: 13 listopada 2026.
- Obszar: wspólny odtwarzacz, fokus, głośność, NVDA, schowek.

## Wdrożone zachowanie

- Po `Strzałka w górę/dół` i `Shift+Strzałka w górę/dół` Sonos wypowiada
  wyłącznie potwierdzoną przez urządzenie głośność, np. `8%`. Pełny opis
  materiału, czasu, stanu i głośności nadal trafia do paska stanu i NVDA+End.
- Odtwarzacz Spotify zachowuje początkowy fokus na przycisku transportu, ale
  Tab przechodzi teraz także na działający suwak głośności. Wybór tempa
  pozostaje wyłączony, ponieważ bieżący silnik Spotify go nie obsługuje.
- `Ctrl+Shift+C` w Apple Music kopiuje bezpośredni link zwrócony przez Apple,
  a gdy takiego linku nie ma — nazwany link wyszukiwania z wykonawcą i tytułem.
  NVDA rozróżnia oba przypadki.

## Uzasadnienie i decyzje

Sonos stosował pełne pole `message` do każdego potwierdzenia transportu, więc
po zmianie głośności NVDA czytał także „Brak informacji, 3:57, zatrzymane”.
Spotify posiada działające zdalne `setVolume`, lecz stara flaga widoku
wyłączała suwak i zostawiała tylko przycisk „Anuluj/Wstrzymaj”. Apple Music
często nie zwraca publicznego URL dla rekordu bibliotecznego; bezpieczny link
wyszukiwania jest uczciwym działającym zastępstwem.

## Materiał do dokumentacji użytkownika

W Sonosie strzałki głośności podają tylko nową wartość. Pełny stan odtwarzania
można nadal odczytać przez NVDA+End. W Spotify po wejściu do odtwarzacza Tab
przechodzi z „Anuluj”, „Wstrzymaj” lub „Odtwórz” na suwak głośności; prędkość
nie jest dostępna. W Apple Music `Ctrl+Shift+C` zawsze kopiuje użyteczne łącze
i mówi, czy jest to link bezpośredni, czy link wyszukiwania.

## Testy i wynik

- Sonos: potwierdzona głośność, wyłączona rutynowa mowa i zachowanie pełnej
  migawki — testy zaliczone.
- Spotify: suwak włączony, tempo nadal wyłączone — test kontraktu zaliczony.
- Apple Music: bezpośredni/fallback schowka bez ujawnienia identyfikatorów —
  test zaliczony.
- Łączna izolowana regresja naprawionych powierzchni schowka, odtwarzaczy,
  fokusu i poleceń: 225/225 zaliczonych.

## Znane ograniczenia

- Odtwarzanie oraz `Shift+A` Apple Music są odłożone decyzją użytkownika z
  13 listopada 2026 do osobnej próby oficjalnej aplikacji Apple Music.
- Etykieta Spotify „Anuluj” podczas przygotowania nadal wymaga ręcznej próby,
  czy działanie odpowiada nazwie przy długim pobieraniu.
- Suwak natywnego TIDAL-a wymaga osobnego sprawdzenia, ponieważ wariant
  zewnętrzny nie może sterować lokalnym wyjściem AMC.

## Wersja i commit

Wersja docelowa: AMC Test `0.1.0-alpha.498`. Commit, paczka i wydanie zostaną
dopisane po bramce wydania.

