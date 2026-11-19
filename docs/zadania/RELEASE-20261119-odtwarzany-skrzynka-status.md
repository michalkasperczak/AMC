# RELEASE-20261119 — skrzynka, oznaczenie odtwarzania i odczyt parametrów

Status: W trakcie — źródła w realizacji, brak nowej budowy i publikacji.

## Nadrzędne decyzje Michała — 19.11.2026

Po zakończeniu wszystkich poniższych poprawek przygotować i opublikować dwa
nowe wydania na GitHubie: AMC Public (stable) i AMC Test (test/prerelease).
Nie czekać na kolejne polecenie „kontynuuj”. Publikacja nie oznacza instalacji
na głównym komputerze Michała: nie zamykać, nie restartować ani nie podmieniać
tam AMC. Nie przywracać dawnych lokalnych procedur Windows/ISCC ani nie
pobierać dużych EXE na stanowisko Hermesa. Zachować ochronę antywirusową.

Oznaczanie odtwarzania nie jest zadaniem samego sygnału dźwiękowego. Ma to być
wspólna informacja o faktycznie odtwarzanym materiale: **tekst, dźwięk albo oba**
według ustawień, we wszystkich odpowiednich listach aplikacji. Dotyczy głównych
list każdej sesji/usługi, kolejek, zawartości playlist i innych widoków tego
samego materiału, nie tylko pierwotnie wyliczonych usług. Kontener nie staje się
odtwarzanym utworem. Nie zgadywać tożsamości po tytule ani po wysłaniu Play.
To doprecyzowanie zastępuje węższą interpretację „sygnał poza Radiem”.

## Zakres źródeł i kolejność

1. Pamięć skrzynki Podcastów: wybór odcinka → Ctrl+L → Ctrl+I przywraca ten
   odcinek po stabilnej tożsamości, niezależnie od pamięci Biblioteki.
   Respektować istniejącą opcję pamięci list; kolejne Ctrl+I wewnątrz otwartej
   skrzynki nadal doładowuje następną porcję. Zachować nawigację i filtry.
2. Wspólny stan odtwarzania i jego tekstowe/dźwiękowe oznaczenie we wszystkich
   właściwych listach. Potwierdzona sesja i tożsamość; poprawny pause/stop/error,
   brak fałszywego oznaczenia i podwójnego sygnału. Bez zmiany fokusu, sieci
   wykonywanej przy każdym ruchu strzałką i bez regresji Radia/nagrywania.
3. NVDA+End: najpierw rzeczywiste parametry audio, nazwa pliku/materiału na
   końcu; zachować bitrate i pozostałe dane oraz fallback przy braku parametrów.
   Istniejąca konfiguracja kolejności list dotyczy góra/dół i lewej strzałki,
   a nie tego gotowego tekstu statusu. Bez przebudowy dodatku NVDA i Alt+D.
4. Niezależny przegląd niezmiennych kandydatów, proporcjonalne testy granic i
   integracja z wcześniejszymi zaakceptowanymi poprawkami kopiowania Podcastów,
   fokusu po usuwaniu i wyszukiwania Radio Archiwisty/globalnego zakresu.
5. Zamrożenie jednego odebranego commita; nowa chroniona budowa obu profili
   na GitHubie i publikacja dopiero po właściwych bramkach.

## Stan początkowy i koordynacja

Główny checkout: `/home/michal/projekty/AMC`, gałąź
`codex/amc-test-public-20261109`, baza
`0b522c000335c7476e8e2cd6b4b35c2121e65d22`; odczyt Git: czysto.

Jeden wykonawca `gui.py` pracuje na `codex/amc-inbox-playing-20261119`.
Oddzielny wykonawca kolejności statusu C# pracuje na
`codex/amc-nvda-end-order-20261119`. Oba worktree w profilu AMC cache/scratch.
Root README i wspólny rejestr prowadzi rodzic, bez równoległej edycji.
Szczegółowe dowody należą do dokumentów INBOX-PLAYING-20261119 oraz
NVDA-END-20261119. Trwałe doprecyzowanie użytkownika: Hyperspace 1795045295.

## Bramki wydania

- Dotychczasowe Public `public-2026.11.18.0329` i Test
  `test-2026.11.18.0337` pochodzą z `1723786c4557ef68bb3922189a028099ce96db31`.
  Są zakończonym poprzednim wydaniem, nie artefaktami nowych poprawek.
- Nie ponawiać starych runów 44229055217/44292203528, nie podmieniać tagów,
  nie przepisywać ich sidecarów ani nie relabelować starych binariów.
- Obecny workflow budowy dopuszcza jednorazowe utworzenie dokładnej starej
  gałęzi r2; trzeba przygotować nowy ścisły ref oraz wszystkie powiązane guardy
  i celowane testy przed pojedynczym uruchomieniem. Nie osłabiać warunków.
- Zachować kanoniczne skrypty, integralność, realne skany Defendera na VM,
  kontrolę profili i sekretów oraz zgodność źródeł i manifestów obu wyjść.
- Próba instalacji/startu i żywy NVDA są osobnymi dowodami, nie wynikają z
  headless ani ze skanu/builda. Nie ustawiać acceptance=true bez pomiaru.
  Przed interaktywną próbą rozstrzygnąć zgodne warunki; nie wykonywać jej
  automatycznie pod pozorem testów bezokiennych.
- Historyczny alert RCX pozostaje nierozstrzygnięty; nowy czysty skan nie
  oznacza werdyktu false positive. Nie osłabiać AV, nie restartować Windows,
  nie odtwarzać próbek i nie wysyłać profili/logowania do Microsoftu.
- Publisher dopiero dla nowego źródła/runu/ID/digestów. Po publikacji odczytać
  oba dokładne wydania, tagi, assety i rzeczywiste manifesty aktualizatora.

## Testy i wynik

Na zapis tego planu nie ma ukończonych kandydatów trzech nowych poprawek,
nowego source SHA, wyników odbioru ani zbudowanych/publikowanych artefaktów.
Nie oznaczać planu jako zakończonego na podstawie wcześniejszych testów.

## Rekonesans drogi wydania — 19.11.2026

Odczytowy przegląd potwierdził model nowej, jednorazowej gałęzi chronionej
budowy, wspólny exact-ref workflow/PowerShell i konieczność nowego numeru
technicznego zamiast użytego alpha.511. Wykonawca rekonesansu uruchomił po
6 testów kontraktu budowy i publishera; nie są dowodem nowego buildu.
Rodzic odczytał rzeczywiste testy, guard PowerShell i pobrał aktualną gałąź:
`HEAD...origin/codex/amc-test-public-20261109` = `28 0` (ahead/behind).
Zdalny `86add1c` jest przodkiem lokalnego `bb3dc06`; nie ma brakującego
zdalnego commita do scalania. Ponowić odczyt przed zamrożeniem/pushem.

Chroniony workflow nie testuje instalacji przez EXE, tylko kontrole bundle.
Nie istnieje odebrana procedura instalacji nowego EXE ani żywego NVDA na
runnerze. Nie dopisywać pozornego zaliczenia. Przed etapem wymagającym
interaktywnego odbioru uzgodnić bezpieczne okno na Hermesie lub wskazać
konkretną blokadę; główny komputer i jego instalacja pozostają nietknięte.

Dozwolone przygotowanie bez uruchamiania: nowy ścisły ref oraz wersja w
Directory.Build.props/workflow/guardzie i ich powiązanych testach. Push
uruchamiający budowę nadal czeka na ukończenie i review wszystkich poprawek.
Publisher będzie miał nowe ID/hashe dopiero po rzeczywistej nowej budowie.

## Znane ograniczenia i następny krok

Następny krok: odebrać kandydatów wraz z RED/GREEN, sprawdzić ich zgodność
z powyższym rozszerzonym zakresem i przekazać niezmienne commity do review.
Równolegle tylko odczytowy rekonesans nowej bezpiecznej drogi wydania.

## Uzasadnienie i decyzje

Jedna warstwa stanu/tożsamości ma zasilać tekst i dźwięk w różnych widokach;
nie tworzyć osobnych oznaczeń kopiowanych dla każdej listy/usługi. Dostawcy
mają mapować potwierdzone dane, a nie zastępować brak informacji pozorem sukcesu.
Nowe wydania muszą rzeczywiście zawierać wszystkie odebrane poprawki.

## Materiał do dokumentacji użytkownika

Planowany rezultat, jeszcze nie deklaracja dostarczenia: po powrocie do skrzynki
Ctrl+I użytkownik kontynuuje od ostatniego odcinka. Odtwarzany element rozpoznaje
na listach, także w kolejce i playliście, przez wybrany tekst, dźwięk albo oba.
NVDA+End podaje najpierw parametry audio, a na końcu nazwę materiału. Nowe wersje
Public i Test mają być dostępne przez właściwe kanały GitHub/aktualizatora;
użytkownik aktualizuje główny komputer samodzielnie.

## Wersja i commit

Nowe źródła i artefakty: jeszcze nieustalone; dopisać rzeczywiste SHA, runy,
numery/etykiety, hashe i wyniki po wykonaniu odpowiednich etapów.
