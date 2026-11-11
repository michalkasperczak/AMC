# BLUOS-001 — osobna sesja BluOS / Bluesound

- Status: `Oczekuje — opracowanie źródłowe jest gotowe; przed implementacją potrzebny jest pomiar na sprzęcie użytkownika`
- Utworzono: 23 października 2026
- Obszar: lokalne urządzenia sieciowe, usługi strumieniowe i dostępność
- Polecenie uruchamiające: `Uruchom zadanie BLUOS-001`
- Opracowanie źródłowe: `D:\Projekty Codex\Hermes\Nowe\BluOS-API-w-AMC.md`

## Cel

Przygotować BluOS jako osobną sesję AMC do lokalnego sterowania konkretnymi
Playerami Bluesound, grupami, kolejką, presetami oraz tą częścią katalogów
TIDAL-a i Apple Music, którą zalogowany Player rzeczywiście udostępnia przez
lokalne `/Browse`.

BluOS nie jest tu bezpośrednim API Apple Music ani TIDAL-a. Sesja ma odzwierciedlać
możliwości wybranego urządzenia i nie może obiecywać pełnej Biblioteki, playlist
ani ulubionych usługi bez potwierdzenia na sprzęcie użytkownika.

## Ustalenia obowiązujące

- Lokalny interfejs BluOS używa HTTP i XML na porcie `11000`. Wykrywanie może
  korzystać z mDNS (`_musc._tcp`, `_musp._tcp`) albo LSDP.
- Dostępne są między innymi: stan, transport, przewijanie, głośność, tryby
  losowania i powtarzania, odczyt i edycja kolejki, grupowanie, wejścia,
  przeglądanie katalogu oraz wyszukiwanie tylko wtedy, gdy urządzenie zwróci
  odpowiednią możliwość.
- Klucze, adresy i akcje zwracane przez `/Browse` są nieprzezroczyste. AMC ma
  wykonywać dokładnie zwrócone przejścia i `actionURL`, bez zgadywania ścieżek.
- Zmiany stanu należy odbierać przez długie odpytywanie `/Status` i
  `/SyncStatus` z `etag`; zwykłe odpytywanie nie może być częstsze niż raz na
  30 sekund.
- Presety są indywidualne dla każdego Playera. API pozwala je odczytać i
  uruchomić, ale nie daje podstaw do tworzenia ani kasowania presetów.
- W interfejsie trzeba jednoznacznie odróżniać „preset Playera BluOS” od skrótu
  lub ulubionego elementu AMC.
- Nie ma przesłanki, że lokalna sesja AMC wymaga ogólnego konta BluOS. Konta
  usług strumieniowych są jednak potrzebne na urządzeniu. W Apple Music Apple
  ID zapisuje się osobno na każdym Playerze, co trzeba uwzględnić przy grupach.
- Sesja BluOS może pokazać wyłącznie to, co zalogowany Bluesound wystawi przez
  `/Browse`. Zakres playlist i ulubionych Apple Music wymaga pomiaru na sprzęcie
  użytkownika.
- Integracja ma działać wyłącznie w zaufanej sieci lokalnej. Portu urządzenia
  nie wolno wystawiać do Internetu.

## Pierwszy etap po uruchomieniu zadania

Najpierw wykonać odczytowy eksperyment na jednym prawdziwym Playerze, bez
budowania docelowych widoków:

1. wykryć urządzenie i odczytać jego nazwę, model, wersję oraz stan;
2. sprawdzić transport, kolejkę, wejścia i listę presetów;
3. zapisać rzeczywiste drzewo `/Browse` dla TIDAL-a i Apple Music bez danych
   konta i bez wrażliwych identyfikatorów;
4. sprawdzić wyszukiwanie, playlisty i ulubione tylko tam, gdzie Player zwraca
   właściwe klucze lub akcje;
5. potwierdzić zachowanie Apple Music na dwóch Playerach przed obietnicą
   grupowania;
6. dopiero na podstawie wyników ustalić stabilne widoki, polecenia i zakres
   profilu testowego.

## Dostępność i wspólny model poleceń

Jeżeli zadanie przejdzie do implementacji, BluOS musi wejść do wspólnego modelu
sesji, poleceń i możliwości AMC. Jedno źródło prawdy ma zasilać wykonanie,
klawiaturę, menu, `Ctrl+F1`, pełną pomoc, paletę poleceń i dokumentację.

- `Ctrl+F5` w sesji BluOS powinno otwierać dostępny wybór lub konfigurację
  Playera, a nie pozorne okno logowania do BluOS.
- Wyszukiwanie, przewijanie, grupowanie i akcje katalogu mają być widoczne i
  aktywne wyłącznie wtedy, gdy bieżący Player faktycznie je udostępnia.
- Nazwy dostępne dla NVDA muszą zawierać tylko jawne etykiety użytkowe. Typy
  obiektów, identyfikatory, adresy IP, surowe klucze i `actionURL` nie mogą
  przeciekać do zwykłej mowy czytnika ekranu.
- Każda lista Playerów, grup, presetów i wyników `/Browse` wymaga jawnej nazwy
  dostępności, stabilnej reprezentacji tekstowej oraz testu początkowego
  fokusu, strzałek, anulowania, powrotu fokusu i ponownego otwarcia z NVDA.
- Testy parytetu mają porównać nazwę, skrót, opis, widoczność, stan aktywności
  i rzeczywiste wykonanie polecenia w reprezentatywnych stanach Playera.

## Granica kont i danych

- AMC nie przechowuje loginu ani hasła Apple ID lub TIDAL-a i nie udaje ich
  bezpośredniego klienta. Użytkownik loguje usługi w obsługiwanym interfejsie
  urządzenia BluOS.
- Sesja BluOS, bezpośrednia przyszła sesja Apple Music oraz bezpośrednia sesja
  TIDAL-a są trzema różnymi kontraktami i nie wolno mieszać ich możliwości.
- Diagnostyka może zapisać model, wersję i strukturę możliwości, ale nie tokeny,
  dane konta ani pełne techniczne adresy zwrócone dla prywatnej Biblioteki.

## Kryteria zakończenia przyszłej implementacji

1. Jeden Player jest wykrywany, wybierany i obsługiwany klawiaturą bez
   aplikacji producenta działającej w tle.
2. Stan i transport są aktualizowane zgodnie z kontraktem długiego odpytywania,
   bez nadmiernego ruchu w sieci.
3. Kolejka, presety, katalog, wyszukiwanie i grupy pokazują tylko możliwości
   rzeczywiście zgłoszone przez urządzenie.
4. Brak pełnej playlisty lub ulubionych Apple Music jest komunikowany uczciwie,
   bez sugerowania awarii bezpośredniego API Apple.
5. Pomoc, menu, paleta i dokumentacja są zgodne z wykonaniem, a automatyczne
   testy parytetu oraz ręczny odbiór NVDA są zapisane z rzeczywistymi wynikami.
6. Kod, testy, udana kompilacja, instalacja i publikacja są raportowane jako
   osobne etapy. Instalator powstaje dopiero po osobnej decyzji użytkownika.

## Zapis źródła i bieżący wynik

### Aktualizacja Apple Music — 12 listopada 2026

- BluOS ogłosił 20.10.2026 natywną obsługę Apple Music, dostarczoną w
  bezpłatnej aktualizacji platformy. To nowszy fakt niż pierwotne opracowanie
  BLUOS-001 i zastępuje ewentualne założenie, że Apple Music jest dostępne
  wyłącznie przez AirPlay.
- Oficjalna pomoc potwierdza logowanie Apple Music z przekierowaniem
  autoryzacyjnym; dokumentuje też, że konto trzeba połączyć osobno z każdym
  Playerem. Nie oznacza to jeszcze, że publiczne Custom Integration API
  udostępnia AMC pełne przeglądanie katalogu lub uruchomienie dowolnego
  katalogowego identyfikatora.
- Przy wznowieniu wykonać pomiar `/Browse`, wyszukiwania, kolejki, presetów i
  transportu po zalogowaniu Apple Music na jednym Playerze. W odpowiedzi
  diagnostycznej zapisać tylko strukturę możliwości i bezpieczne nazwy, bez
  tokenów, prywatnych kluczy, pełnych actionURL i danych konta.
- Źródła: https://bluos.io/2026/10/20/bluos-now-supports-apple-music/ oraz
  https://support.bluos.net/hc/en-us/articles/44634833939095-Why-Can-t-I-Log-into-Apple-Music .

- 23 października 2026 odczytano całe opracowanie
  `D:\Projekty Codex\Hermes\Nowe\BluOS-API-w-AMC.md`.
- Użytkownik potwierdził odczyt pliku i zgodność jego sumy SHA-256; wartość sumy
  nie została podana w rozmowie, dlatego nie jest tu wymyślana.
- Karta zachowuje wnioski i granice z opracowania, ale nie oznacza zgody na
  rozpoczęcie implementacji ani zmianę profilu wydania.
- Nie zmieniono kodu, nie uruchomiono testów ani kompilacji, nie zbudowano i nie
  zainstalowano żadnej wersji AMC.

## Znane ograniczenia

- Rzeczywisty zakres `/Browse` zależy od Playera, wersji BluOS, regionu i kont
  usług; nie da się go potwierdzić samą dokumentacją.
- Najważniejsza niewiadoma to widoczność pełnych playlist i ulubionych Apple
  Music na sprzęcie użytkownika.
- Dalsza praca czeka na jawne uruchomienie `BLUOS-001` i dostęp do co najmniej
  jednego Playera w tej samej sieci lokalnej.

## Wersja i commit

- Wersja źródeł AMC: bez zmiany.
- Commit zapisu karty i wpisu w rejestrze: `dded0666`
  (`docs(bluos): zapisz plan osobnej sesji`).
