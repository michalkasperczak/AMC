# AMC Test — dublowanie przez Google Cast i poprawka WiiM

- `Shift+M` w Plikach, Radiu i Podcastach pozwala dodać odbiornik Google Cast do lokalnego dźwięku AMC. W oknie można wybrać odbiornik lub wyłączyć dublowanie. `Shift+C` nadal przełącza sesję na samo urządzenie Cast, a `Shift+A` wybiera lokalne wyjście.
- Przy plikach i podcastach AMC próbuje wyrównać pozycję po rozpoczęciu odtwarzania przez Cast. Urządzenia mogą nadal grać z różnym opóźnieniem; nie ma gwarancji synchronizacji.
- W WiiM Enter na zapisanym strumieniu respektuje wybór „Pozostań na liście” z `Ctrl+Alt+Enter`. Natywne presety WiiM również respektują sesyjną opcję widoku po uruchomieniu presetu.
- Ustawienia programu (`Ctrl+,`) mają kartę **Integracja z Windows**. Można tam udostępnić AMC Test dla wybranych typów audio i wideo w „Otwórz za pomocą”, ustawieniach aplikacji domyślnych Windows oraz menu kontekstowym.

W tym wydaniu `Shift+M` obsługuje jeden dodatkowy odbiornik Google Cast. AirPlay i drugie lokalne wyjście audio pozostają w przygotowaniu. Próby sprzętowe potwierdziły odtwarzanie na komputerze i WiiM Sound oraz na komputerze i Denonie przez WiiM Pro Plus; ręczny odbiór okna Shift+M z NVDA i dokładny pomiar opóźnienia pozostają otwarte. Aktualizacja nie instaluje się automatycznie podczas budowania na GitHubie.
