# AMC Test i Public alpha.531 — wydanie GitHub

Data: 25.11.2026. Stan: oba wydania opublikowane i zdalnie zweryfikowane.
Wersja źródeł `0.1.0-alpha.531`, commit
`bc49032eb5ddf792403a0ca31cba4a9cdb176363`, gałąź
`codex/heos-next-test-20261123`.

## Zachowanie i decyzje użytkownika

W Radiu `Alt+R` otwiera listę Nagrywane. `Enter` na aktywnym nagraniu
uruchamia osobny odsłuch od początku wskazanego w Ustawieniach → Radio:
„Na żywo” (domyślnie) albo „Od początku dostępnego fragmentu”.
`Ctrl+Enter` uruchamia przeciwny tryb bez zmiany ustawienia. Ten późniejszy
wybór użytkownika z 25.11 zastępuje wcześniejszy bezwarunkowy start od
początku bufora, a następnie bezwarunkowy start na żywo. `F2` zmiany nazwy
aktywnego pliku użytkownik potwierdził dwukrotnie w praktyce.

Okno odsłuchu ma suwak czasu. `Home` prowadzi do początku dostępnego bufora,
`End` na żywo; lewo/prawo cofa lub przesuwa o 10 s, z Shift o 30 s,
z Ctrl o 60 s. Góra/dół reguluje głośność o 5 punktów, z Shift o 1.
`Shift+,` / `Shift+.` zmienia tempo, `Ctrl+.` przywraca normalne.
`Ctrl+Shift+E/R/T` czyta czas od początku dostępnego fragmentu, czas do końca
bufora i całkowitą długość dostępnego fragmentu. `Enter` na osi czasu mówi
„Odsłuch nagrania”; `F1` podaje skróty, Spacja pauzuje sam odsłuch.
`Escape` zamyka tylko odsłuch, a `Ctrl+R` wysyła zatrzymanie wskazanego
nagrania. Lokalny odsłuch nie zmienia radia grającego na Denonie ani kodera.

Po `Ctrl+D` w Podcastach zakończenie pobierania i drugi asynchroniczny odczyt
listy zachowują kursor na aktualnie wskazywanym odcinku. Wynik jest
ogłaszany bez powrotu do odcinka, na którym pobieranie się zaczęło.

Enter/Ctrl+Enter są w menu Radia, menu kontekstowym, pełnej pomocy,
`Ctrl+F1` i palecie `Ctrl+Shift+K` tylko w kontekście aktywnego nagrania.
Ustawienie ma jawne etykiety wyboru i jest zapisywane w profilu wxPython;
stary profil bez pola przyjmuje domyślne „Na żywo”.

Oba profile mają te wspólne zmiany. Apple Music i HEOS pozostają wyłącznie
w Test; Public nie udostępnia tych sesji.

## Weryfikacja

295 testów Pythona w łącznym zestawie zaliczonych po korekcie Ctrl+Enter.
Lokalny `dotnet build` LiteHost: zero ostrzeżeń i błędów. Izolowany test C#
`--recording-monitor-only`: zaliczony, w tym powrót na żywo z ochronnym
opóźnieniem do 0,25 s. `git diff --check`: bez błędów.

Pierwszy run GitHub Actions `45673578400` dla commitu `bd1781c2` anulowano
po nowej decyzji użytkownika o Ctrl+Enter, bez publikacji. Run
`45675400732` z commitu `bc49032e` zakończył oba zadania Test/Public
sukcesem. Pobrane artefakty miały profil, wersję, commit, rozmiar i SHA-256
zgodne z plikami integralności i potwierdzeniem kompilacji.

AMC Test: tag `test-2026.11.25.1929`, prerelease, instalator
`AMC-Test-2026-11-25-1929.exe`, 214660465 bajtów,
SHA-256 `c80eacda07e1f6f9d27b39272eb17a908d06ae222ba537e5bbbea188472a5c0e`.

AMC Public: tag `public-2026.11.25.1930`, najnowsze stabilne, instalator
`AMC-Public-2026-11-25-1930.exe`, 134086654 bajty,
SHA-256 `91920c73d485a8194f7e7d0e0286ecd02195e6c5df0fe61b635ac1ee5cd72e3c`.

Zdalny odczyt GitHub potwierdził oba tagi na dokładnym commicie źródłowym,
trzy zasoby każdego wydania w stanie `uploaded`, rozmiary i SHA-256 wszystkich
sześciu zasobów zgodne z lokalnymi. Endpoint `releases/latest` wskazuje
Public alpha.531. Instalacja na komputerze użytkownika i test NVDA nie
zostały wykonane.

## Ograniczenia i odbiór

„Od początku” oznacza początek nadal dostępnego bufora; po osiągnięciu jego
pojemności starsze próbki mogą być już nadpisane. „Na żywo” zachowuje około
0,25 s zapasu przed czołem strumienia. Odsłuch używa domyślnego lokalnego
wyjścia Windows; dźwięk z głośników laptopa wymaga ustawienia ich jako
wyjścia domyślnego. W formacie oryginalnego strumienia dekodowany odsłuch
może korzystać z osobnego połączenia radiowego i różnić się nieznacznie od
zapisanych bajtów. Rzeczywisty odsłuch stacji, mowa NVDA, fokus suwaka,
zmiany opcji i Ctrl+R podczas nagrywania wymagają odbioru w użyciu.

Kopie instalatorów, manifestów, notatek i potwierdzeń integralności są w
`D:\Projekty Codex\AMC - integracja wxPython\work\release-alpha531-20261125`.
Zachowano je do odbioru użytkownika. Szkic instrukcji i listy zmian:
`ALPHA531-instrukcja-i-lista-zmian-szkic.md`; nie publikowano go automatycznie
jako zatwierdzonej instrukcji.
