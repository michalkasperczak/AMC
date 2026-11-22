# AMC alpha.513 — opublikowane Test i Public, przekazanie dla Hermesa

Stan: 22.11.2026. Ten raport **zastępuje status „nieopublikowane”** w `RELEASE-ALPHA513-20261122-kandydat.md` i w kolejce po alpha.512. Wydano oba profile na GitHubie z tego samego commitu źródeł `a07b11c0a8bbdf76e413ed695479d8b2ee4e757d`. Nie zainstalowano ich na komputerze Michała ani na Hermesie.

## Wydania odczytowo potwierdzone na GitHubie

| Profil | Wydanie | Instalator i bajty | SHA-256 GitHub i lokalne |
| --- | --- | --- | --- |
| Test, wstępne, kanał `test` | [test-2026.11.22.1851](https://github.com/michalkasperczak/AMC/releases/tag/test-2026.11.22.1851) | `AMC-Test-2026-11-22-1851.exe`, 214422916 | `51c7fcdd3225632d0d1c2966075c91765a4e7f8ef5e57e58480232c89a92c4a3` |
| Public, zwykłe, kanał `stable` | [public-2026.11.22.1853](https://github.com/michalkasperczak/AMC/releases/tag/public-2026.11.22.1853) | `AMC-Public-2026-11-22-1853.exe`, 133957142 | `63a9ec2f8c6bfb272894742375e0cad5a298f8450dafb19ab45e0131891c2bd7` |

Zdalne tagi obu wydań wskazują dokładnie commit `a07b11c0`. API GitHub potwierdziło `draft=false`, odpowiednio `prerelease=true/false`, wszystkie trzy załączniki każdego wydania w stanie `uploaded` oraz sumy `digest=sha256:...` obu EXE zgodne z lokalnymi plikami integralności. Obok instalatorów są na GitHubie `amc-update-test.json`/`amc-update-public.json` i odpowiednie `.integrity.json`. Lokalna gałąź źródeł została wysłana jako `codex/alpha513-release-20261122` bez force push; po publikacji odczytano ten sam pełny commit z API GitHub.

Lokalne egzemplarze pozostają w `D:\Projekty Codex\AMC - integracja wxPython\publish\installers-alpha513-a07b11c0` i `D:\Projekty Codex\AMC - integracja wxPython\publish\installers-alpha513-public-a07b11c0`. Nie usuwać ich przed potwierdzeniem użytkowej aktualizacji i przyjęciem wydania. Roboczy opis zmian oraz pełne wyniki budowy i testów są w `RELEASE-ALPHA513-20261122-kandydat.md`; jego wcześniejszy status publikacji jest nieaktualny, pozostałe ograniczenia testowe nadal obowiązują.

## Informacja dla Huberta

Po faktycznej publikacji wysłano i ponownie odczytano komentarze w zgłoszeniach [#32](https://github.com/michalkasperczak/AMC/issues/32#issuecomment-7085263303), [#25](https://github.com/michalkasperczak/AMC/issues/25#issuecomment-7085269604), [#22](https://github.com/michalkasperczak/AMC/issues/22#issuecomment-7085270636), [#28](https://github.com/michalkasperczak/AMC/issues/28#issuecomment-7085271686) i [#30](https://github.com/michalkasperczak/AMC/issues/30#issuecomment-7085272754). Każdy komentarz zawiera oba wydania i zaznacza brak odpowiedniego odbioru na prywatnym profilu, sprzęcie lub z NVDA. PR #29 i #31 zachowują autorstwo Huberta w commitach integracyjnych. Zgłoszeń nie zamknięto automatycznie.

## Korekta decyzji użytkownika z 22.11.2026

Michał wyjaśnił, że po skończonej i sprawdzonej pracy oba profile mają iść automatycznie na GitHub, bez pytania o termin. Sam zaktualizuje program u siebie. **Ta decyzja zastępuje wcześniejszy krok automatycznej instalacji na D po publikacji** w `AGENTS.md` z 13.11.2026. Nie daje powodu do zamykania działającego AMC ani przerywania nagrania. Powiadomienie Huberta następuje po faktycznym wydaniu, tak jak wykonano powyżej.

## Wyniki i ograniczenia

Kod i testy celowane, paczki, instalatory, publikacja źródeł oraz obu GitHub Releases: wykonane. Pełny zestaw Pythona nie jest zielony: ostatni ukończony przebieg miał 30 niepowodzeń obecnych także w porównywanym stanie bazowym, a dla końcowego commitu nie ukończono nowego pełnego przebiegu. Nie wykonano odbioru na prywatnej Bibliotece 79 869 plików, prawdziwej transmisji Hermanic, słuchawkach, kontach usług ani w NVDA. Instalacja Test/Public na komputerze Michała: nie; użytkownik zrobi ją sam. Nie przedstawiać publikacji jako potwierdzenia tych prób.

## Następny krok

Michał sam aktualizuje AMC. Po jego wyniku sprawdzić konkretnie #32, #25, #22, #28, #30, nagrywanie Hermanic, Radio Archiwistę i Kolejki. Ewentualne regresje naprawić w następnym przyroście; zgłoszenia zamykać po faktycznym potwierdzeniu działania. Nowe prace Apple Music oraz wycinanie nagrań Dwójki pozostają odłożone zgodnie z decyzją z 22.11.2026.

## Materiał do dokumentacji użytkownika

Wydania Test i Public są dostępne na GitHubie z datą w nazwie instalatora. W instalatorach dołączono manifest aktualizacji i plik sumy kontrolnej. Programu nie zaktualizowano automatycznie na komputerze Michała. Roboczy przewodnik `AMC-ALPHA513-lista-zmian-i-instrukcja-robocza.md` nadal czeka na korektę po odbiorze NVDA; nie publikowano go jako gotowej instrukcji.
