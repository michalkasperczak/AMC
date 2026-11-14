# PLAYER-20261113 — Sonos, Spotify i łącze Apple Music

- Status: **wydane, zainstalowane i uruchomione w AMC Test alpha.498**.
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

- Wersja: AMC Test `0.1.0-alpha.498`, użytkowo `13.11.2026, 02:17`.
- Commit źródłowy: `6e34a3fedc78642ab86e83ab16202b707a1293b2`.
- Paczka: `8fce9c2a`, 4256 plików, `sourceDirty=false`.
- Instalator: `AMC-Test-2026-11-13-0217.exe`, 165 818 571 bajtów,
  SHA-256 `3f3c020db76f66bbbcf0c4f0df78032442a10f2bed839a534e63b88967d996ea`.
- Wydanie: `test-2026.11.13.0217`, prerelease, opublikowane i zweryfikowane.
- Instalacja i kontrolowany start w `D:\Projekty Codex\AMC Python`: zaliczone;
  uruchomione okno ma tytuł `AMC Test — 13.11.2026, 02:17`.

## Nowszy stan wydania 14.11.2026

Wspólne wydanie Test/Public `0.1.0-alpha.507`, commit źródłowy `aafa2bc3`,
zawiera te zmiany wraz z późniejszymi poprawkami. Oba profile opublikowano
i zainstalowano; Test działa z `D:\Projekty Codex\AMC Python`, tytuł
„AMC Test — 14.11.2026, 16:42”. Public nie ma sesji ani działań Apple Music.
Pełna kontrola obu instalacji i cichy start przeszły. Sprawdzenie NVDA
w odtwarzaczu, Sonosa na głośniku oraz linków Apple Music w Test pozostaje
otwarte. Ta sekcja aktualizuje powyższe dane alpha.498, nie zastępuje
historycznego wyniku tamtego wydania.

