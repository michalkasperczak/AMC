# Zgłoszenia AMC na GitHub — przegląd po przygotowaniu alpha.513

Stan: 22.11.2026, odczyt zgłoszeń przez połączone konto GitHub. To rejestr kolejności dla Codexa i Hermesa, nie zamykanie Issues i nie potwierdzenie wydania. Źródło: `michalkasperczak/AMC` oraz raporty zadaniowe na D. Zgłoszenia Huberta i pozostałe otwarte pozycje pozostają na GitHubie w swoim dotychczasowym stanie.

**Późniejszy stan tego samego dnia:** oba profile alpha.513 są już wydane, a komentarze do #32, #25, #22, #28 i #30 wysłane. Szczegóły i linki w `RELEASE-ALPHA513-20261122-publikacja.md`. Poniższa tabela zachowuje zakres kodu i otwarte próby odbioru; jej wcześniejsze sformułowania o niewydanym kandydacie są historyczne.

## Gotowe w kodzie kandydata alpha.513, odbiór otwarty

| Issue | Zakres | Potwierdzone | Pozostało |
| --- | --- | --- | --- |
| #32 | Ctrl+F w Bibliotece ponad 50 000 tytułów | poprawka i celowane testy | rzeczywisty profil 79 869 plików, NVDA |
| #25 | RSS, nowości i transmisje YouTube | poprawka, protokół | prawdziwe 122 kanały, sprawdzenie scalania |
| #22 | szybkość Folderów Biblioteki | syntetyczne 60 000 rekordów i zgodność wyników | rzeczywisty profil, NVDA |
| #28 / PR #29 | wznowienie YouTube 2x | test Windows Smoke | odsłuch po pauzie |
| #30 / PR #31 | domyślne wyjście Windows | test Windows Smoke | słuchawki, odłączenie, radio i NVDA |

Osobno w tym samym kandydacie są nagranie Hermanic, Radio Archiwista, presety i Kolejki, opisane w `RELEASE-ALPHA513-20261122-kandydat.md`. Zgłoszenie nagrania dotyczy głównego komputera Michała, nie Hermesa. Po realnym wydaniu poinformować Huberta w odpowiednich Issues lub PR, podając wersję i zakres faktycznie odebrany. Nie wysłano jeszcze komentarzy sugerujących wydanie.

## Otwarte, już obecne we wcześniejszym wydaniu lub wymagające żywej próby

- #26: rozdziały YouTube są już w alpha.512 według rejestru projektu, lecz zgłoszenie pozostaje otwarte do próby na wskazanym filmie i NVDA.
- #21: kod komunikatów Folderów Biblioteki był już wydany; potrzebny odsłuch wyniku dodania i rozróżniania jednakowych nazw.
- #14 i #17: ochrony aktualizatora i opcjonalnego dodatku NVDA są już w źródłach wcześniejszego wydania; nadal trzeba wykonać kontrolowaną próbę cichej aktualizacji i odzyskiwania po błędzie. Nie testować tego na działającym nagraniu.
- #9–#11: Spotify wymaga prób na koncie i urządzeniu. #15 (przester MP3) wymaga odsłuchu wskazanego materiału. #19 (masowy import YouTube) ma szerszy zakres niż RSS-first z #25. #1 (pobieranie YouTube) wymaga dokładnej reprodukcji.

## Następne prace po odbiorze obecnego kandydata

1. Najpierw zakończyć odbiór alpha.513 dla Biblioteki, YouTube/Hermanic, Radio Archiwisty, Kolejek i wyjścia audio. Naprawić znalezione regresje przed publikacją.
2. #23: komunikaty NVDA giną przy powrocie fokusu po menu i oknach. To ważne dla odbioru obecnych zmian; jeśli objaw wystąpi, rozwiązać go przed uznaniem testu mowy za zaliczony. Nie ma jeszcze poprawki w alpha.513.
3. #24: jawna instalacja dodatku AMC do NVDA z menu Pomoc jest osobną funkcją. Obecny instalator zawiera dodatek, ale cicha aktualizacja nie instaluje go osobie bez dodatku; nie przedstawiać tej funkcji jako gotowej.
4. Nowe wyszukiwanie po nazwach folderów z dopisku #32 jest osobnym ulepszeniem; poprawka limitu 50 000 go nie realizuje.
5. Zgodnie z korektą Michała z 22.11.2026, wycinanie nagrań Dwójki i nowe prace Apple Music zostają na później.

Zamknięte #5 (dawna powolna lista Plików lokalnych) nie jest nowym zadaniem tylko dlatego, że nadal pojawia się w wyszukiwaniu tekstowym. Nie wznawiać go bez nowego objawu. Stan Issues może się zmienić; przed komentarzem lub zamknięciem sprawdzić go ponownie.

## Materiał do dokumentacji użytkownika

Zgłoszenia oznaczone jako gotowe w kodzie pojawią się w programie dopiero po zainstalowaniu odpowiedniej wersji. Otwarte Issue może czekać na sprawdzenie z NVDA, nawet gdy poprawka jest już w źródłach. Ten przegląd nie jest listą funkcji gotowych w bieżącej instalacji.
