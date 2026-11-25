# AMC Test i Public alpha.531 — przygotowanie wydania GitHub

Data: 25.11.2026. Wersja źródeł `0.1.0-alpha.531`.

Zakres wspólny dla obu profili: odsłuch nagrania ma zwykły suwak i skróty
odtwarzacza; w Ustawieniach → Radio można wybrać start „Na żywo” (domyślnie)
lub „Od początku dostępnego fragmentu”. Ctrl+R w oknie zatrzymuje wskazane
nagranie. Ctrl+D w Podcastach zachowuje aktualny kursor podczas pobierania
i ponownego odczytu listy. Użytkownik potwierdził praktycznie działanie F2
na aktywnym nagraniu. Apple Music i HEOS są tylko w Test; Public ma pozostałe
wspólne poprawki z bieżącej gałęzi.

Dowody przed wydaniem: 292 testy Pythona zaliczone w dwóch zestawach; `dotnet build` LiteHost
bez ostrzeżeń; izolowany test C# `--recording-monitor-only` zaliczony po
uwzględnieniu 0,25 s bezpiecznego opóźnienia na żywo; `git diff --check`
bez błędów. Test UI i mowy NVDA, instalacja i odsłuch rzeczywistej stacji
pozostają do odbioru użytkownika. Źródła i kompilacja lokalna nie stanowią
dowodu opublikowania lub instalacji.

GitHub Actions ma zbudować oba profile z tego samego commitu. Po powodzeniu
należy sprawdzić SHA-256 instalatorów i manifestów, opublikować osobne wydania
Test (prerelease) i Public (najnowsze stabilne) oraz zweryfikować zdalne tagi,
zasoby, rozmiary i sumy. Numer commitu, uruchomienia i tagi zostaną uzupełnione
po rzeczywistym wykonaniu.
