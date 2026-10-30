# PUBLIC-20261030 — uzupełnienie poprawek Huberta

Status: W trakcie. Data korekty: 30.10.2026.

## Materiał do dokumentacji użytkownika

- Plik otwarty z Eksploratora, Total Commandera lub okna „Otwórz plik” jest bieżącym plikiem AMC. `F6` pokazuje jego tytuł bez rozszerzenia. Po wczytaniu folderu `Page Up` i `Page Down` przechodzą po utworach tego folderu, jeżeli otwarty plik należy do listy.
- W odtwarzaczu suwak głośności i lista prędkości mają nazwy dla NVDA. W języku polskim `Alt+G` i `Alt+P` kierują do dostępnych kontrolek.
- Kandydat Public zawiera również PR #2 (radio), #3 (prędkość), #6 (mniejsze obciążenie dużych list), dzisiejsze odświeżanie historii rozpoznawania oraz opcje osobnych folderów podcastów i stacji radiowych.

## Uzasadnienie i decyzje

Wcześniejszy kandydat Public z commitu `fba01de343ba95c0aa4b26c46fd83a5792c15cbd` powstał bez PR #7 i #8, mimo że obydwa dotyczą Public. Ta korekta zastępuje decyzję o jego kompletności z 30.10.2026, 19:47. PR #7 (`03d4e83`) prowadzi otwarty plik przez `Navigator.play_local_file`, zamiast uruchamiać silnik poza stanem sesji. PR #8 (`431ad6d`) tworzy podpisy przed kontrolkami, by Windows i NVDA nadały im nazwy i właściwe skróty dostępu. Oba commity zastosowano bez konfliktów w odizolowanym drzewie roboczym. Numer techniczny podniesiono do `0.1.0-alpha.448`.

Zgłoszenie #5 o 42 866 plikach pozostaje odrębnym zadaniem: PR #6 usuwa skan całej listy co sekundę, ale pełne wstawienie wierszy i odbudowa UI Automation po Escape nadal mogą blokować AMC i NVDA. Zadanie ma trafić do następnej aktualizacji razem z Radioarchiwistą po opracowaniu i odbiorze. Zgłoszenia #9–#11 dotyczą Spotify w AMC Test, którego nie ma w profilu Public.

## Testy i realny wynik

- Po zastosowaniu PR #7 i #8: 79 celowanych testów bez GUI zaliczonych (`test_local_file_playback`, `test_player_labels`, preferencje audio, aktywność odtwarzania, natywna lista i plan zmian). Testy uruchomiono z kontrolowanym katalogiem TEMP, ponieważ domyślny katalog tymczasowy piaskownicy nie pozwalał tworzyć plików.
- Testy Huberta na zainstalowanym Public z podmienionymi plikami: F6 i Page Down dla pliku z Total Commandera oraz odczyt nazw i Alt+G/P w NVDA. Są to wyniki autora PR, nie odbiór tego nowego instalatora.
- Pełna paczka, instalator i kontrola integralności: do uzupełnienia po kompilacji.

## Znane ograniczenia

- PR #7 nie zmienia nawigacji Backspace w folderze po otwarciu z powłoki ani zachowania przy wyłączonej sesji Pliki lokalne.
- W sesjach, w których suwak lub prędkość są wyłączone, skróty podpisów mogą przenieść fokus dalej; wymaga osobnego odbioru NVDA.
- PR #6 nie usuwa długich pauz w bibliotece 42 866 utworów (#5).
- Nowy kandydat nie został zainstalowany ani sprawdzony ręcznie z NVDA.

## Wersja i commit

Podstawa: `fba01de343ba95c0aa4b26c46fd83a5792c15cbd`. Źródła: https://github.com/michalkasperczak/AMC/pull/7 oraz https://github.com/michalkasperczak/AMC/pull/8. Commit kandydata, identyfikator paczki, instalator i suma SHA-256: do uzupełnienia.
