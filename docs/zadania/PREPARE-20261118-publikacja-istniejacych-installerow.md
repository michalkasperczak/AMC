# PREPARE-20261118 — wąska publikacja istniejących instalatorów Public/Test

## Status

Przygotowanie lokalne do niezależnego review. Ten dokument i workflow **nie
wykonały pushu, publikacji, instalacji ani uruchomienia EXE**.

## Zakres

- Jednorazowy workflow Linux GitHub-hosted uruchamiany wyłącznie przy pierwszym
  pushu nowej gałęzi `codex/amc-publish-existing-20261118`.
- Nie wykonuje buildu, instalacji, skanów lokalnego Defendera, pobierania EXE do
  WSL ani uruchamiania pobranych plików.
- Na efemerycznym runnerze pobiera wyłącznie dwa istniejące archive Actions:
  `14395074212` (instalatory) i `14395482588` (dowody), weryfikuje ich API,
  hash, zamknięty layout ZIP, sidecary, źródła, etapy kompilacji i dowody
  Defendera.
- Jeżeli wszystkie bramki przejdą, workflow tworzy osobne drafty, przesyła
  wyłącznie EXE, odpowiadający `.integrity.json` i wymagany przez aktualizator
  `amc-update-<profil>.json`, odczytuje assets, pobiera EXE z draftu i porównuje
  jego SHA-256. Dopiero potem ustawia `draft=false` i sprawdza faktyczny endpoint
  listy wydań używany przez aktualizator.

## Zamrożone wejście

- Źródło instalatorów i target obu tagów:
  `1723786c4557ef68bb3922189a028099ce96db31`.
- Build run: `44229055217`, attempt `1`, wynik `success`.
- Binary artifact: `14395074212`,
  `sha256:8c6a7d6daa5a621eb9231ea5204b6930006b55e9e535fdda4ec9191eeec1a5f3`.
- Evidence artifact: `14395482588`,
  `sha256:24f0f8ff72139f328d28e37a70ccd549eae24d865df790b0419c833cedc95682`.
- Tag Public: `public-2026.11.18.0329`, stable (`prerelease=false`).
- Tag Test: `test-2026.11.18.0337`, prerelease (`prerelease=true`).

Publisher commit nigdy nie jest targetem tagu ani źródłem instalatora.
Workflow odmawia istniejącego tagu/release oraz każdego innego source run,
attemptu, artefaktu, profilu, pliku, rozmiaru, sumy lub wyniku bramki.

## Opis użytkowy w release

Opis ogranicza się do zintegrowanego #21: komunikatów i nazw Folderów
Biblioteki. Nie przedstawia otwartych odbiorów YouTube jako zakończonych.
Test jawnie mówi, że Apple Music ma wyszukiwanie i dodawanie
do Biblioteki, ale bez odtwarzania; Public Apple Music nie zawiera.

Nie domniemywa odbioru instalacji lub NVDA. Zapisuje, że nowe instalatory
pochodzą z chronionej budowy GitHub bez wykryć oraz że wcześniejszy lokalny alert
RCX nie ma analizy historycznych próbek przez Microsoft. Opinia Huberta/Claude o
prawdopodobnym związku RCX z zasobami Inno i deklaracja prywatnej instalacji nie
są werdyktem Microsoftu ani własnym pomiarem i nie zostały podniesione do
odbioru.

## Pliki

- `.github/workflows/amc-publish-existing-installers.yml`
- `tools/publish_existing_installers.py`
- `wxlite/tests/test_publish_existing_installers.py`

## Lokalne testy fixture

- `python3 -m py_compile tools/publish_existing_installers.py wxlite/tests/test_publish_existing_installers.py`
- `python3 wxlite/tests/test_publish_existing_installers.py` → `Ran 4 tests ... OK`.
- `ruamel.yaml` odczytał workflow; test statyczny potwierdził Linux runner,
  brak `workflow_dispatch`, pinned checkout, warunek `before` zerowy/attempt 1,
  oba ID artefaktów, source SHA i tylko `actions: read` + `contents: write` w
  jobie.

Fixture to jawnie małe, syntetyczne EXE/ZIP i atrapy API; nie jest wynikiem
chmury ani testem rzeczywistych binariów.

## Przegląd rodzica przed wysłaniem

W pierwszej wersji cztery syntetyczne fixture przechodziły, ale rzeczywisty
mały evidence ZIP był odrzucany: zawiera także `protection-setup.json`.
Odtworzono RED na tym dokładnym ZIP-ie oraz na fixture z 13 raportami.
Po poprawce weryfikator przyjmuje dokładnie 13 plików i dodatkowo sprawdza
włączenie ochrony oraz brak wykluczeń po przygotowaniu runnera. Te same
prawdziwe raporty przechodzą bez modyfikowania ich treści lub sumy ZIP-a.

Drugi RED wykazał zły typ `make_latest` z rzeczywistego producenta JSON
w workflow. Poprawiono na wymagany string `true`/`false`, `draft=false`
w PATCH jest booleanem, a upload używa dokładnego release ID i jawnie
uwierzytelnionego endpointu uploads, bez zgadywania obsługi innego hosta
przez `gh api`. Dodano test producenta JSON i kontraktu PATCH.

Stan po korektach: **6 testów unittest PASS**, rzeczywisty evidence ZIP
**13 raportów PASS**, odczyt YAML oraz `bash -n` wszystkich bloków PASS,
`git diff --check` PASS. Opis release ograniczono do #21 i jawnego zakresu
Apple Music w Test, bez reklamowania otwartych odbiorów YouTube.
Niezależny read-only review aktualnych plików: `deleg_ecf8745b` — w toku.
Nie jest to jeszcze wynik publikacji.

## Znane ograniczenia i następny krok

Nie było rzeczywistej publikacji, instalacji, uruchomienia, odbioru Windows/NVDA
ani rozstrzygnięcia historycznego alertu RCX. Artefakty nie zostały pobrane do
WSL. Przed osobnym wykonaniem rodzic ma zreviewować diff i niezależnie sprawdzić
remote tag/release absence oraz bieżącą dostępność artefaktów.
