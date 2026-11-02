# MIRROR-001 — dublowanie dźwięku AMC przez Google Cast

## Status — 02.11.2026

Kod i testy przeniesione do gałęzi wydania AMC Test `codex/amc-test-mirror-wiim-20261102` na bazie alpha.465. Wydanie na GitHubie, instalacja i ręczny odbiór NVDA wymagają osobnych potwierdzeń. Dokument zastępuje w publicznym repozytorium prywatny dziennik prób zawierający lokalizacje nagrań użytkownika.

## Materiał do dokumentacji użytkownika

W sesjach **Pliki**, **Radio** i **Podcasty** skrót `Shift+M` otwiera okno dublowania dźwięku AMC. Wybór odbiornika Google Cast pozostawia odtwarzanie na lokalnym wyjściu sesji i dodaje drugi cel. Wybór **Wyłącz dublowanie** zatrzymuje drugi cel, pozostawiając odtwarzanie lokalne. `Shift+C` przełącza sesję na Cast, a `Shift+A` wybiera jedno lokalne wyjście. Nie da się używać bezpośredniego Cast i dublowania tej samej sesji jednocześnie.

Lista pokazuje czytelne nazwy urządzeń i wskazuje wybrany cel przy ponownym otwarciu. Enter lub przycisk **Zastosuj** zatwierdza, Escape anuluje, a **Odśwież urządzenia** ponawia wykrywanie. Po zamknięciu okna fokus wraca do AMC. Komunikaty po włączeniu i wyłączeniu informują o obu torach; utrata Cast ma pozostawić lokalne odtwarzanie i zostać oznajmiona. Ręczny odsłuch tych komunikatów w NVDA jest otwarty.

Dublowanie obejmuje dźwięk odtwarzany przez AMC, bez mowy NVDA i innych aplikacji. Przy plikach i podcastach AMC po rozpoczęciu odtwarzania przez Cast próbuje wyrównać pozycję. Dwa urządzenia mają niezależne zegary i mogą grać z różnym opóźnieniem. Radio jest przesyłane na żywo. AirPlay i drugie lokalne wyjście nie są częścią tej wersji.

## Uzasadnienie i decyzje

Lokalny host pozostaje właścicielem odtwarzania, a odbiornik Cast jest dodatkowym celem. Awaria celu nie może wyciszyć komputera. Korekta pozycji czeka najwyżej pięć sekund na stan PLAYING odbiornika, po czym w razie wyraźnego opóźnienia przewija plik lub podcast do aktualnej pozycji lokalnej. Nie obiecuje synchronizacji próbek. Wykrywanie Cast wysyła zapytanie mDNS na interfejsach IPv4, aby działać także przy aktywnym VPN.

## Testy i wynik

Celowany przebieg na bazie wydania: 222 zaliczone testy, 1 pominięty (wymaga jawnego hosta testowego). Obejmuje router dublowania, korektę po buforowaniu, wykrywanie Cast, skrót, pomoc, menu, paletę, obsługę WiiM, otwieranie lokalnych plików i profil instalatora. Analiza składni modułów przeszła. To wyniki kodu źródłowego, nie budowy na GitHubie ani instalacji.

W izolowanych próbach z prawdziwym hostem AMC i odbiornikami użytkownik usłyszał komputer oraz WiiM Sound, a osobno komputer i Denon podłączony do WiiM Pro Plus przez Cast. Wcześniejsze nagranie mikrofonowe wskazywało około 2,88 s odstępu; po poprawce nie wykazano już dwóch osobnych przebiegów oddalonych o tyle. Nagranie po poprawce nie pozwala wyznaczyć dokładnego opóźnienia akustycznego. Nie publikowano nagrań, ich lokalizacji ani danych użytkownika.

## Znane ograniczenia

Jeden dodatkowy odbiornik Google Cast w Plikach, Radiu lub Podcastach. Brak pełnej synchronizacji, AirPlay i dwóch lokalnych wyjść w interfejsie. Ręczny test okna i fokusu z NVDA oraz odsłuch wszystkich przejść transportu w zainstalowanym wydaniu pozostają otwarte.

## Wersja i commit

Planowana wersja AMC Test `0.1.0-alpha.466`; dokładny commit, wynik budowy, tag i stan instalacji zapisać po ich rzeczywistym potwierdzeniu. Główna gałąź robocza z innymi niezapisanymi zmianami pozostaje nienaruszona.

## Wynik wydania — 02.11.2026, po budowie GitHub Actions

Kod i celowane testy są zatwierdzone. Workflow GitHub Actions 41493428992 zakończył się powodzeniem: skompilował paczkę i instalator na Windows 2025 z .NET SDK 8.0.423, zweryfikował integralność oraz kontrolowany start programu i hosta, a następnie opublikował AMC Test jako prerelease. Użytkownik nie zlecił instalacji teraz; istniejąca instalacja pozostała bez zmian. Ręczny odbiór okna i komunikatów NVDA oraz dokładna synchronizacja pozostają otwarte.

Wersja `0.1.0-alpha.466`, commit źródeł `cab4357e61bb861a44f03c04bbd0170effbd7a22`, tag `test-2026.11.02.1743`. Instalator ma 133742949 bajtów, SHA-256 `b81a56c7e164561babb107fbc2a06244431fa4df09c2336e4323ffa7503df23b`. Zdalne API potwierdziło przesłanie wszystkich trzech zasobów i tag wskazujący dokładny commit. Lokalna kopia z GitHub została pobrana i porównana z sumą oraz manifestem: `lokalna kopia instalatora AMC-Test-2026-11-02-1743.exe`. Nie publikowano nagrań użytkownika ani ich ścieżek.
