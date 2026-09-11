# AMC 0.1.0-alpha.342 i dodatek NVDA 0.2.1

Wydanie testowe dla Windows. Zawiera również zmiany pośredniej alpha.341.

- Dodatek NVDA udostępnia 65 poleceń; prefiks AMC pozostaje niezależny.
- Ctrl+Windows+A wybiera urządzenie audio bieżącej sesji.
- Ctrl+Windows+Tab / Ctrl+Windows+Shift+Tab przełącza sesje.
- Ctrl+Windows+przecinek / kropka zwalnia / przyspiesza;
  Ctrl+Windows+Shift+kropka przywraca normalną prędkość.
- Ctrl+Windows+Alt+1–9, 0, minus i równa się uruchamia 12 presetów bieżącej
  sesji. Puste miejsce jest oznajmiane, nigdy automatycznie zastępowane.
- Ctrl+Windows+Shift+P otwiera playlisty, Ctrl+Windows+Alt+P — presety.
- Listy playlist i presetów mają przyciski tworzenia. WiiM pozwala przypisać
  lokalny skrót AMC do gotowego presetu, bez zmiany konfiguracji urządzenia.
- Po zapisie lub anulowaniu wraca właściwa lista i zaznaczenie. Enter/Spacja
  na przyciskach dialogów nie uruchamiają przypadkiem presetu z listy.
- Sterowanie odtwarzaniem w tle jest oddzielone od jawnego otwierania widoków;
  działania dotyczą bieżącego nagrania, nie zaznaczenia ukrytej listy.
- Mostek lokalny ma ograniczoną kolejkę, terminy ważności poleceń i listę
  dozwolonych działań. Nie ponawia automatycznie operacji przełączających stan.

## Testy i dalszy audyt

Pełna kompilacja Release i testy Core/Windows zakończyły się powodzeniem;
osobne testy mostka oraz 7 testów Python również. Użytkownik zaakceptował
najnowszą wersję do publikacji 11 września 2026 r.

Mapa: INSTRUKCJA_NVDA_PL.md. Decyzje i obszary do dalszego sprawdzenia innymi
modelami: PROJEKT_NVDA_PL.md. Szczegółowy raport i granice weryfikacji:
wyniki-testow/WERYFIKACJA_NVDA_342_2026-09-11.md.

Wymagane NVDA 2026.1 lub nowsze. Nie instalować aktualizacji podczas nagrywania.
Własne gesty NVDA pozostają zachowane i mogą nadpisywać domyślną mapę.
TIDAL nadal udostępnia temu klientowi próbki; to wydanie nie rozwiązuje
ograniczenia do pełnych utworów ani nie dodaje TIDAL Connect.
