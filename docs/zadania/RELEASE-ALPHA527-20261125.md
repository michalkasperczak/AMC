# AMC Test alpha.527 — GitHub i lokalna aktualizacja

**Zlecenie Michała, 25.11.2026:** poprawić odczyt po zapisie harmonogramu, wydać następną wersję AMC Test z kompilacją na GitHub, a potem zaktualizować działającą aplikację. [Kontrakt poprawki SCHED-004](SCHED-004-20261125-komunikat-po-zapisie-planu.md). Poprzednia alpha.526 była wydana na GitHub, ale nie zainstalowana; lokalną alpha.525 zastąpiła bezpośrednio alpha.527.

## Zachowanie użytkowe

Po Enter zapisującym plan Radia AMC przywraca fokus do listy harmonogramów, a dopiero potem wysyła wynik zapisu do NVDA. Nie zmienia to działania planu ani danych nagrania. Przy uruchomieniu NVDA może nadal odczytać tytuł okna: jego układ użytkownik wybiera w Ustawienia programu → Ogólne. Aktualny wybór „Przed nazwą AMC” pozostał zachowany.

Alpha.527 zawiera także źródła alpha.526: listę aktywnego odtwarzania `Shift+F6` ze Spacją do pauzy i wznowienia wskazanej pozycji, główne wyjścia Cast/AirPlay, skróty zmiany wyjścia, poprawki HEOS i ukrycie kolejki w zwykłym Radiu. Mirror z obcego strumienia Cast na komputer oraz przenoszenie i sortowanie Ulubionych HEOS nie są wdrożone.

## Oddzielnie potwierdzone etapy

- **Źródła:** commit `853504bcdcbbbbc9c31b1d93879900235f6f0263`, gałąź `codex/heos-next-test-20261123`, wysłana do lokalnego repozytorium i GitHub. Przed wydaniem śledzone źródła były czyste.
- **Testy:** 142 celowane testy harmonogramów, regionu aktywnego, ustawień, aktualizatora i tytułu okna zaliczone; `py_compile` i `git diff --check` zaliczone. Nie była to fizyczna próba mowy NVDA po Enter.
- **Kompilacja GitHub:** Actions run `45512450260` zakończył się `success` dla powyższego commitu. Zbudowany instalator `AMC-Test-2026-11-25-0046.exe` ma **214644598 bajtów**, SHA-256 `29f06e36c77cd38d5298dbc32f5705c68d372bb2af1931516cb7a5e32e8f8552`; manifest potwierdza profil Test, wersję `0.1.0-alpha.527`, etykietę `25.11.2026, 00:46`, czyste źródła i `secretScan=passed`.
- **Wydanie GitHub:** prerelease `test-2026.11.25.0046` ma `draft=false`, tag wskazuje dokładny commit źródeł. Trzy zasoby są w stanie `uploaded`: instalator, `.exe.integrity.json`, `amc-update-test.json`. Zdalny rozmiar i digest instalatora zgadzają się z pobranym artefaktem. Odczytowy aktualizator starej instalacji pokazał `available=true` i ten tag.
- **Preflight instalacji:** dokładnie trzy procesy AMC z `D:\Projekty Codex\AMC Python` (launcher, Python, host), bez procesu FFmpeg. Prywatny profil zawierał 59 włączonych planów; 0 w chronionym oknie i 0 w najbliższych 10 minutach. Najbliższy obliczony termin: `25.11.2026 06:45 UTC`. Updater ma dodatkową odmowę podmiany, jeśli AMC lub host nie zamkną się zwykłą drogą.
- **Instalacja:** uruchomiono kopię projektowego `AMC-Python-Updater.exe` poza katalogiem instalacji, z dokładnym instalatorem i SHA-256, wskazując działający proces UI do bezpiecznego zamknięcia. Kod wyjścia aktualizatora **0**. Nie usuwano pobranego instalatora.
- **Zainstalowane pliki:** `D:\Projekty Codex\AMC Python\bundle-manifest.json` wskazuje profil Test, wersję `25.11.2026, 00:46`, źródła `853504bc…` i `sourceDirty=false`. Wszystkie **3658** plików wymienionych w manifeście przeszły ponowną kontrolę rozmiaru i SHA-256, **0 niezgodności**.
- **Uruchomiona aplikacja:** jedna instancja launchera, prywatnego Pythona i hosta z katalogu instalacji. Okno Pythona pokazuje `AMC Test — 25.11.2026, 00:46` z aktualnie grającym tytułem przed nazwą zgodnie z ustawieniem. Odczytowy aktualizator nowej instalacji zwrócił `available=false` dla tego samego tagu.

Instalator pobrany z GitHub i lokalne dowody wydania: `D:\Projekty Codex\AMC - integracja wxPython\publish\release-alpha527-853504bc\artifacts`. [Wydanie AMC Test alpha.527](https://github.com/michalkasperczak/AMC/releases/tag/test-2026.11.25.0046).
https://github.com/michalkasperczak/AMC/releases/tag/test-2026.11.25.0046

## Znane granice i dalszy odbiór

Rzeczywisty odczyt NVDA po zapisaniu planu w zainstalowanej alpha.527 wymaga próby Michała. Test kolejności zdarzeń wx nie gwarantuje kolejności fizycznej mowy. Nowa komenda HEOS i główne AirPlay nadal wymagają prób na urządzeniach. Updater wypisał uwagę, że Windows nie przekazał launcherowi prawa pierwszego planu; mimo tego trzy składniki i okno nowej wersji uruchomiły się, a pliki są zgodne.

## Szkic instrukcji i listy zmian — do korekty Michała

Instrukcja: „W Radiu otwórz Harmonogram nagrywania. Po edycji planu naciśnij Enter na Zapisz. Fokus wróci do listy planów, a AMC poda wynik zapisu. Jeśli przy uruchomieniu najpierw słyszysz utwór, możesz wybrać kolejność w Ustawieniach programu, w sekcji Ogólne, przy opcji tytułu odtwarzanego utworu w tytule okna”.

Lista zmian: „Poprawiono odczyt wyniku zapisu harmonogramu w AMC Test alpha.527. Zachowano konfigurowalny układ tytułu okna. Wydanie obejmuje też funkcje alpha.526: aktywne odtwarzanie Shift+F6, wyjścia Cast/AirPlay i poprawki HEOS”. Ten szkic nie został opublikowany jako osobna instrukcja użytkownika.
