# PLAY-20261105 — kontynuacja albumu po wybraniu utworu

Status: AMC Test alpha.479 zbudowano, zainstalowano i uruchomiono 5 listopada 2026; kontynuacja albumu czeka na ręczny odbiór NVDA.

Instalator alpha.479: `D:\Projekty Codex\AMC - integracja wxPython\publish\installers\AMC-Test-2026-11-05-2211.exe`; SHA-256: `718B58684355BC2E1D0C8BF9A01EE45ADAE6871BF483FFC226248E2BC79B6EDF`. Widoczny tytuł uruchomionej aplikacji: `AMC Test — 05.11.2026, 22:11`. Kod tej poprawki: commit `b9fac4d7`. Instalacja i uruchomienie nie dowodzą jeszcze naturalnej kontynuacji albumu; wymaga to testu odsłuchowego.

## Korekta zgłoszona przez użytkownika

5 listopada 2026 użytkownik wskazał, że po uruchomieniu pojedynczego utworu z albumu odtwarzanie prawdopodobnie zatrzymuje się na nim. Oczekiwane zachowanie to automatyczne przejście do następnego utworu albumu lub bieżącej listy. Ta decyzja zastępuje wcześniejszą politykę, według której zwykły `PlayTrack` kończył odtwarzanie, a kontynuacja poza kolejką działała tylko przy losowaniu. W TIDAL Max wewnętrzny adapter również kończył po jednym utworze.

## Zachowanie do instrukcji użytkownika

- W lokalnych plikach Enter na utworze rozpoczyna od niego odtwarzanie kolejnych utworów bieżącej listy w jej kolejności. Page Up i Page Down nadal ręcznie przechodzą po tym samym źródle. Po ostatnim utworze odtwarzanie kończy się; nie zapętla listy.
- W Spotify Enter na utworze albumu lub listy rozpoczyna od niego dalsze odtwarzanie w tej liście. Kontynuacja działa również wtedy, gdy użytkownik przełączył się do innej sesji. Dedykowana kolejka Spotify nadal ma własny porządek.
- Polecenie „Odtwórz utwór TIDAL w AMC Test (eksperymentalnie)” uruchamia po końcu sąsiedni utwór załadowanej listy. Wewnętrzny odtwarzacz pozostaje otwarty podczas przygotowywania następnego utworu. Jego opis w jednym katalogu komend zasila menu, paletę, pomoc kontekstową i pełną pomoc. Enter w TIDAL nadal przekazuje utwór oryginalnej aplikacji, która prowadzi własne odtwarzanie.
- Odcinek podcastu otwarty ze skrzynki nie uruchamia automatycznie wszystkich następnych odcinków. Jawna kolejka hosta nadal sama prowadzi przejście; interfejs nie próbuje równolegle uruchamiać kolejnego utworu.

## Technika i uzasadnienie

Źródło odtwarzania było już przechowywane dla Page Up/Down. Zdarzenie naturalnego końca używa teraz tej samej sekwencji dla lokalnych utworów i Spotify, niezależnie od bieżącej sesji na ekranie. Id zakończonego utworu jest porównywany z oczekiwanym, aby opóźnione zdarzenie nie przeskoczyło kolejnego utworu. W TIDAL wewnętrzny adapter dopiero po zdarzeniu `ended` przygotowuje następny utwór i zachowuje źródło; `started` nadal potwierdza faktyczny start. Stop użytkownika pozostaje końcem odtwarzania.

## Weryfikacja i ograniczenia

226 testów Python: sekwencja lokalna, Spotify po przełączeniu sesji, odrzucenie starego końca, TIDAL Max, podcasty, kolejka, nawigacja i parytet komend — zaliczone. Jedyny starszy test Spotify wymagał dopisania obowiązkowego argumentu `fields`; sygnatura produkcyjna nie zmieniła się w tym zadaniu. Źródła usług zawierają tylko załadowane wiersze; w bardzo długim albumie/listach dalsze strony nie są automatycznie pobierane. Test prawdziwego końca utworu z dźwiękiem i mowy NVDA wymaga odbioru po instalacji. Wersja, commit, build, instalator i ewentualna publikacja zostaną zapisane oddzielnie.

## Szkic listy zmian

Wybranie utworu z albumu lub zwykłej listy nie zatrzymuje już odtwarzania po jego końcu, jeżeli w załadowanej liście jest następny utwór. Poprawka obejmuje lokalne pliki, Spotify i wewnętrzne odtwarzanie TIDAL Max. Szkic do korekty użytkownika, nie jest opublikowaną informacją o wydaniu.
