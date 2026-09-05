# Adapter WiiM — zakres i dalsze etapy

## Zasada architektoniczna

WiiM jest w AMC przede wszystkim **urządzeniem i celem odtwarzania**, a nie
zamiennikiem katalogów TIDAL, Spotify, Apple Music lub innych usług. Adapter
urządzenia steruje tym, co rzeczywiście udostępnia odtwarzacz. Katalogi,
logowanie, Ulubione i playlisty konta należą do osobnych adapterów usług.

AMC korzysta najpierw z udokumentowanego lokalnego API. Funkcje potwierdzone
tylko przez projekty społecznościowe mogą wejść wyłącznie jako jawnie
eksperymentalne, po sprawdzeniu możliwości konkretnego modelu i z bezpiecznym
wycofaniem operacji. Polecenia administracyjne, konfiguracja sieci, restart i
przywracanie ustawień fabrycznych nie należą do adaptera multimedialnego.

## Stan po alpha 273

- wykrywanie UPnP/SSDP i ręczne dodanie lokalnego adresu IP;
- wybór i zapamiętanie aktywnego urządzenia;
- stan odtwarzania i dostępne metadane;
- odtwarzanie, pauza, następny, poprzedni, stop, przewijanie, głośność i
  wyciszenie;
- wybór wejścia i fizycznego wyjścia, korektor graficzny, powtarzanie,
  losowanie i timer uśpienia;
- odczyt oraz uruchamianie natywnych presetów urządzenia;
- skróty `Ctrl+Shift+1–0/-/=` do miejsc 1–12 i
  `Alt+Page Up/Alt+Page Down` do sąsiedniego zajętego miejsca;
- po ponownym uruchomieniu AMC ustala bieżący preset przez zgodność adresu
  strumienia, a gdy urządzenie nie podaje adresu — przez ostatni preset
  uruchomiony w AMC. Zapamiętany numer jest używany tylko w trybie odtwarzania
  zarządzanym przez WiiM; przy Spotify Connect lub TIDAL Connect aplikacja nie
  zgaduje.
- stan odtwarzacza, `getMetaInfo` i UPnP `GetInfoEx` są łączone w jeden zestaw
  bieżących metadanych. Tytuł audycji lub utworu trafia do odtwarzacza, tytułu
  okna i paska stanu, a `Alt+D` odczytuje go na żądanie bez dialogu;
- nazwy `unknown`, `unknow` oraz techniczne nazwy playlist M3U/PLS nie są
  przedstawiane jako tytuł audycji. Zgodny adres może zostać powiązany z
  użytkową nazwą stacji w Bibliotece Radia AMC.

Lokalne API nie udostępnia zapisu ani zmiany kolejności natywnych presetów.
AMC nie zgłasza więc pozornego powodzenia; takie ustawienie nadal wykonuje się
w WiiM Home. Uniwersalne presety AMC są osobną funkcją i mogą później wskazywać
adres, album, playlistę lub inną treść niezależnie od sprzętowych miejsc WiiM.

## Kolejność dalszej integracji

1. **Otwieranie adresu na WiiM.** Radio, odcinek podcastu albo publiczna
   playlista M3U może zostać wysłana jako URL. Funkcja musi jasno ostrzegać, że
   zastępuje bieżące źródło. Lokalny plik wymaga kontrolowanego serwera HTTP lub
   DLNA w AMC; sama ścieżka `D:\...` nie jest dla urządzenia osiągalna.
2. **Aktualizacja stanu przez UPnP z odpytywaniem awaryjnym.** Zdarzenia UPnP
   mogą szybciej aktualizować tytuł, pozycję i głośność. Okresowe HTTP pozostaje
   mechanizmem awaryjnym, ponieważ zdarzenia bywają blokowane przez router,
   zaporę lub firmware.
3. **Grupy i multiroom.** Najpierw tylko odczyt ról oraz członków. Dołączanie i
   opuszczanie grupy dopiero po testach na co najmniej dwóch prawdziwych
   urządzeniach, z osobnym sterowaniem głośnością każdego pokoju. Nie wolno
   opierać stanu grupy na jednym polu, którego znaczenie różni się między
   generacjami firmware.
4. **Kolejka urządzenia.** Dostępna tylko, jeśli konkretne urządzenie ogłosi i
   potwierdzi obsługę. Przeglądanie kolejki jest ograniczone do części modeli i
   źródeł, dlatego nie zastąpi kolejki AMC ani kolejki Spotify/TIDAL.
5. **Korektor parametryczny i korekcja pomieszczenia.** PEQ może być
   eksperymentalny po wykryciu możliwości modelu. Korekcja pomieszczenia
   pozostaje tylko do odczytu, dopóki zapis nie zostanie szeroko potwierdzony na
   sprzęcie.
6. **Spotify Connect i TIDAL Connect.** WiiM pokazuje, że dana usługa gra, i
   pozwala na podstawowy transport, ale nie udostępnia jej pełnego katalogu.
   Wyszukiwanie, biblioteka i wybór albumu wymagają adaptera danej usługi.
   Dopiero taki adapter może przekazać odtwarzanie na wybrany WiiM oficjalną
   drogą Connect.

## Reguły zgodności i bezpieczeństwa

- Każda funkcja ma osobną flagę możliwości wykrytą na urządzeniu; odpowiedź
  „unknown command” wyłącza tylko tę funkcję.
- Awaria jednego odczytu nie usuwa urządzenia ani zapisanych ustawień.
- Żądania mają ograniczony czas, rozmiar odpowiedzi i są kierowane wyłącznie do
  lokalnych adresów IP bez przekierowań.
- Zapis stanu urządzenia nie może przejmować fokusu ani generować okresowych
  komunikatów NVDA.
- Nazwy usług, trybów i elementów są etykietami użytkowymi. Surowe kody,
  rekordy i obiekty API nie trafiają do UI Automation.
- Rozszerzenia społecznościowe wymagają testu na rzeczywistych modelach WiiM
  Pro Plus i WiiM Sound przed uznaniem ich za stabilne.

## Źródła techniczne

- Oficjalne: `HTTP API for WiiM Products`, dokumentacja pomocy WiiM oraz
  instrukcja WiiM Home.
- Społecznościowe do porównania zachowania: `mjcumming/pywiim`, integracja
  Home Assistant `mjcumming/wiim` oraz `gthibo/wiim-universal-remote`.
  Projekty te są źródłem testów i rozpoznania różnic firmware, nie powodem do
  automatycznego włączenia nieudokumentowanych poleceń.
