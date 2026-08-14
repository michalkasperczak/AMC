# Bezpieczny smoke test z NVDA

Ten folder zawiera opcjonalny klient testowy dla lokalnego mostu NVDA. Nie instaluje dodatku, nie uruchamia NVDA i nie steruje klawiaturą. Sprawdza jedynie aktywne okno, nazwę i rolę elementu z fokusem oraz obiekt nawigatora.

## Stan integracji

Publiczny projekt `nvda-mcp-bridge` w wersji 0.2.0 nie spełnia obecnie profilu bezpieczeństwa AMC. Dodatek automatycznie uruchamia serwer, używa publicznego tokenu domyślnego i razem z odczytem udostępnia operacje zmieniające stan NVDA. Skrypt celowo odmówi współpracy z takim profilem.

Do właściwego testu potrzebny jest oddzielnie zainstalowany, utwardzony wariant mostu:

- nasłuch wyłącznie na `127.0.0.1`;
- losowy token ustawiony przez użytkownika, bez wartości domyślnej;
- serwer domyślnie wyłączony i uruchamiany tylko na czas testu;
- wyłącznie akcje `get_current_focus`, `get_window_title` oraz `get_navigator_object`;
- brak przeładowania dodatków, restartu NVDA, przesuwania fokusu, mowy, komunikatów i odczytu logu;
- wyraźny wskaźnik, że tryb testowy działa, oraz możliwość natychmiastowego zatrzymania.

## Uruchomienie po przygotowaniu utwardzonego mostu

Otwórz AMC, ręcznie ustaw fokus na kontrolce przeznaczonej do sprawdzenia i w PowerShell ustaw token tylko dla bieżącego procesu:

```powershell
$env:NVDA_MCP_BRIDGE_TOKEN = "tu-losowy-dlugi-token"
powershell.exe -NoProfile -ExecutionPolicy Bypass -File ".\tests\accessibility\nvda\Invoke-NvdaSmoke.ps1"
```

Opcjonalne `-WriteReport` zapisuje zanonimizowany wynik w ignorowanym przez Git folderze `artifacts/accessibility`. Skrypt nie zapisuje wartości ani opisów pól, bieżącego wiersza, logu NVDA lub tokenu. Samo ręcznie wpisane polecenie ustawiające zmienną środowiskową może jednak pozostać w historii PowerShell; w stałej konfiguracji token powinien pochodzić z chronionego magazynu sekretów.

Test jest tylko dodatkowym smoke testem. Nie potwierdza kolejności Tab, wypowiedzi NVDA, działania skrótów, trybu przeglądania ani zgodności z JAWS, Narratorem i VoiceOver. Te obszary nadal wymagają testów UI Automation i testów ręcznych.
