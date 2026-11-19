# AUDIO-EDIT-20261119 — brak nagrania po cięciu, pozostawione nazwy robocze

## Status i zlecenie

**Potwierdzony incydent w wydanym AMC; odczyt logów i metadanych wykonany.
Osobne następne zadanie diagnostyczne, bez zmian plików użytkownika.**

Michał 19.11.2026 zgłosił: nagranie Dwójki rozpoczęte o 19:10, po usunięciu
początku i pozornie udanym zapisie Escape wraca do historii Alt+Shift+R,
która podaje brak pliku. Po następnym cięciu pojawia się kolejny taki wpis.
Następnie sam odnalazł dwa pliki o nazwach zaczynających się od kropki w
folderze iCloud/Castro/Sideloads; jeden przemianował na „Dwójka”, by go odsłuchać.
Polecił sprawdzić logi oraz zapis i wycinanie w odrębnym kolejnym zadaniu.
Pliki wynikowe/kopie przeznaczone dla użytkownika nie mają pozostawać pod
niezrozumiałymi nazwami z początkową kropką.

Nie oznacza to zgody na usuwanie kopii, odzyskiwanie przez nadpisanie,
zmianę nazw użytkownika, restart AMC, zmianę ustawień iCloud ani GUI/NVDA.

## Odczyt i tożsamość

Odczyt przez gotowy alias SSH `michal-glowny`; faktyczny komputer MICHAL,
stan plików odczytany 19.11 po 23:20. Instalacyjny manifest AMC Python wskazuje
`sourceRevision=1723786c4557ef68bb3922189a028099ce96db31`, `sourceDirty=false`.
To incydent wersji użytkowej, niezależny od obecnych niewydanych zmian audio.

## Potwierdzone fakty

1. Dziennik hosta rejestruje zakończenie dwóch nagrań Dwójki o 20:00 i 20:10,
   oba z początkiem 19:10. To dwa różne pliki: nazwa podstawowa i wariant `(2)`.
2. O 23:04:28–30 wariant `(2)` został poddany cięciu. Log zgłasza poprawną
   weryfikację wyniku i usunięcie kopii po sprawdzeniu pliku docelowego.
   O 23:04:59 próba otwarcia pierwotnej ścieżki daje FileNotFoundException.
3. O 23:06:23–25 analogiczna sekwencja występuje dla nazwy podstawowej.
   O 23:06:59 kolejna próba otwarcia także daje FileNotFoundException.
4. Metadane katalogu potwierdzają brak obu pierwotnych ścieżek.
5. Istnieje wynik `.amc-cut-...-result.mp3` dla wariantu `(2)` oraz drugi wynik
   już przemianowany przez Michała z początkiem „Dwójka”. Ich nazwy operacji
   są zgodne z nazwami wyniku w logu. Nie czytano audio, nie wykonywano
   haszowania nagrań, dekodowania, odtwarzania ani pobierania plików chmurowych.
6. W logu zapisano granice cięcia około 81,64 s i 81,62 s od początku,
   podczas gdy opis użytkownika wspomina pierwsze 10 s. Zachować obie
   informacje; sprawdzić niezależnie przekazanie zaznaczenia i czasu. Nie
   przypisywać tej rozbieżności błędowi użytkownika ani traktować jako
   wyjaśnienie zniknięcia plików.

To dowód incydentu po zapisie, nie ustalona przyczyna. Log sukcesu chwilę po
podmianie nie dowodzi trwałości nazwy po późniejszych zdarzeniach systemu plików.
Nie dowiedziono, że Escape sam usuwa plik, ani że winne jest iCloud. Nie jest to
wyłącznie błędna etykieta historii: pierwotnych nazw brakuje również na dysku.

## Hipotezy i najbliższy krok

1. Sprawdzić sekwencję `File.Replace`, usuwania kopii i cleanupu roboczych
   ścieżek oraz późniejsze zdarzenia zmiany nazwy. Pozostały wynik ma nazwę
   roboczą, choć log twierdzi, że plik docelowy został sprawdzony.
2. Osobno sprawdzić aktualizację historii nagrań i biblioteki po edycji i po
   zewnętrznym rename; nie przepinać ich na kopię ani plik roboczy.
3. Sprawdzić, czy kod/usługa synchronizacji odtwarza starą nazwę roboczą po
   pozornie udanym zastąpieniu; wymaga dowodu z kontrolowanych danych.

**Następny krok:** Codex ma na prywatnych, syntetycznych plikach Windows
odtworzyć `RemoveAsync → zapis → cleanup → lista nagrań → drugie cięcie`,
najpierw dla lokalnego NTFS. Sprawdzić wydany commit i bieżący kod. Gdy zwykły
NTFS nie odtworzy incydentu, nie uznawać tego za naprawę; oznaczyć granicę
rzeczywistego katalogu synchronizowanego i zaplanować bezpieczną sondę dopiero
po osobnym uzgodnieniu. Nie testować na nagraniach Michała ani w jego iCloud.

## Wymagania przyszłej poprawki i odbioru

- Funkcjonalny RED przed zmianą produktu, minimalny GREEN i sąsiednia regresja.
- Jeden oraz dwa kolejne zapisy: istnieje oczekiwana końcowa nazwa, poprawna
  oś czasu i dekodowalna zawartość, a historia i lista wskazują ten sam plik.
- Zachować istniejące ustawienie retencji kopii. Nie usuwać jedynego wyniku
  ani kopii przy niepewnym stanie; nie komunikować pełnego sukcesu przy
  brakującym docelowym pliku.
- Jeżeli trzeba zachować wynik awaryjny lub kopię dla użytkownika, ma mieć
  czytelną, zwykłą nazwę i informację w interfejsie. Nie rozwiązywać tego
  samą kosmetyczną zmianą prefiksu plików tymczasowych.
- Błąd replace, brak dostępu, anulowanie, zewnętrzny rename, opóźnione zdarzenia
  i podwójny zapis nie mogą pozbawić użytkownika dostępnej wersji nagrania.
- Zaznaczenie i faktyczna długość usuwanego fragmentu muszą być zgodne.
- Zakres interfejsu/klawiszy/NVDA odebrać osobno w uzgodnionym oknie; nie
  uznawać headless za odczyt żywego Alt+Shift+R/Escape.

## Dowody prywatne

Pełne nazwy nagrań, ścieżki, fragmenty dziennika i metadane pozostają lokalnie:
`/home/michal/.hermes/profiles/amc/cache/scratch/amc-recording-edit-20261119/`.
Najważniejsze: `focused.json` (wiersze hosta 12260–12380 i metadane katalogu),
`inspection.json` (manifest i wstępne wyszukanie). Wstępny skan zawiera też
historyczne dopasowania — nie używać ich do przypisywania dzisiejszego zdarzenia.
Żaden surowy log ani profil użytkownika nie został dodany do repozytorium.

W pamięci jest podobny przypadek starego WPF z września: File.Replace mógł
przepiąć historię na backup. To wskazówka porównawcza, nie dowód tej samej
przyczyny we współczesnym wxPython. Nie przenosić starej poprawki w ciemno.

## Materiał do dokumentacji użytkownika

Obecnie zgłoszenie nie jest naprawione. Wyniki cięcia pozostały na dysku pod
nazwami technicznymi, a lista nagrań może podawać brak pliku. Nie wykonywać
kolejnych zmian na jedynej znalezionej wersji ani usuwać tych plików w ramach
porządków. Przyszła poprawka musi zachować nagranie pod oczekiwaną nazwą,
prawidłowo odświeżyć Alt+Shift+R po Escape i jasno wskazać zachowaną wersję
awaryjną, jeżeli nie uda się zakończyć zapisu. Brak nowego wydania i instalacji.
