# DirectCut RS — odczytowy wzorzec dla edycji nagrań AMC

## Decyzja i zakres

Michał wskazał DirectCut RS jako możliwy wzór albo źródło implementacji dla
edycji audio. Sprawdzono dokumentację i konkretne źródła, bez uruchamiania,
kompilowania, instalacji lub kopiowania produkcyjnego kodu do AMC.
**Warto inspirować się wybranymi mechanizmami; nie zastępować teraz edytora AMC
całą aplikacją ani zmieniać kolejności uzgodnionych zadań.** Bieżący cykl
przełączania wyjścia audio trwa niezależnie; incydent cięcia nagrań jest następny.

## Doprecyzowanie Michała — scenariusz na później (20.11.2026)

**Tylko zapisać przyszły scenariusz, nie rozpoczynać teraz implementacji.**
AMC ma sprawnie wyciąć wskazany fragment i bezpiecznie zapisać wynik,
w miarę możliwości bez dodatkowej utraty jakości. Nie budować pełnego edytora
wielościeżkowego ani przenosić całego interfejsu DirectCut RS. Jego sposób pracy
lub pojedynczy mechanizm może być wzorem, jeśli daje rzeczywistą korzyść.

MP3 jest ważnym scenariuszem testowym, ponieważ Michał przypuszcza, że wiele
osób nagrywa właśnie w tym formacie — to założenie użytkowe, nie pomiar udziału
formatów. MP3 jest już stratne, lecz cięcie bez ponownego kodowania pozwala
uniknąć kolejnej generacji strat; nie odzyskuje utraconej wcześniej jakości.
Nie obiecywać identycznych próbek przy granicy cięcia.

Przyszły, ograniczony test porównawczy na syntetycznych MP3 CBR/VBR:
- usunięcie początku, środka lub końca oraz dwa kolejne zapisy;
- zgodność zaznaczenia i rzeczywistego cięcia z jawną tolerancją ramek;
- dekodowalność, poprawne przewijanie i zachowanie pozostałego dźwięku;
- bezpieczny wynik, końcowa nazwa i zgodna historia, bez znikania nagrania;
- prosty, dostępny przebieg zaznacz → wytnij → zapisz, bez dodatkowych funkcji;
- pomiar przyrostu rozmiaru instalatora i instalacji dla tego samego profilu
  względem bazy oraz wykaz nowych zależności, zamiast zgadywania wagi Rust.

Najpierw sprawdzić, czy obecne FFmpeg i kod AMC wystarczą. Wybierać możliwie
mały mechanizm, nie dokładać całego programu ani drugiego dużego środowiska
wykonawczego tylko dla cięcia. Nie ustalono liczbowego limitu wielkości ani
zgody na powiększenie paczki. Tej próby jeszcze nie wykonano; nie zastępuje
ona pilnej diagnozy znikających plików i nie zmienia kolejności prac.

## Tożsamość materiału

- Repozytorium: https://github.com/Nick6489/DirectCutRS
- Zbadany HEAD: `94f86fd546b71abfd02afe04856b35bbfeba7ded`.
- Data commita z GitHub API: `2026-11-16T14:28:00Z`.
- Cargo.toml: pakiet `directcut-rs`, wersja `1.2.2`, Rust edition 2024.
- LICENSE: MIT, Copyright (c) 2026 Nick Giannak III. Zachowanie informacji
  o prawach autorskich i licencji wymagane przy wykorzystaniu kodu. Zależności
  wymagają własnej weryfikacji przed dystrybucją; MIT projektu nie rozstrzyga
  licencji wszystkich bibliotek.
- Kopia tylko do odczytu: scratch profilu AMC `directcutrs-reference-20261120`;
  rzeczywisty `git rev-parse HEAD` zgodny z SHA API. Nie uruchamiano build.rs,
  aplikacji ani jej mechanizmu aktualizacji.

## Co rzeczywiście robi

To przenośny edytor audio Windows w Rust/RunGUI, nie edytor dokumentów.
Edytuje ramki MP3, MP2 i ADTS AAC bez ponownego kodowania zachowanego dźwięku.
Nie oznacza to edytora FLAC ani obietnicy dokładności co do próbki PCM.
Cięcia mają granice ramek; stan dekodera w pobliżu łączenia może zmienić próbki.

## Wartość dla AMC, według priorytetu

### 1. Ochrona źródła, jawny plik wynikowy, kontrolowane zatwierdzenie

`src/editor.rs:2767` (`check_destination`) odrzuca zapis pod ścieżką otwartego
źródła i sprawdza konflikt oryginalnego CUE. `src/audio.rs:223–313` tworzy plik
roboczy w katalogu docelowym, weryfikuje pakiety przez dekoder dla obsługiwanych
profili, kończy zapis/flush/sync_all, dopiero potem wykonuje persist.
Wariant export_no_replace używa persist_noclobber i rozróżnia istniejący cel od
zapisanego wyniku. To wartościowe kontrakty dla zwykłych nazw i kolizji plików.

Nie jest to automatyczna naprawa incydentu AMC. DirectCut z założenia zapisuje
edytowaną kopię zamiast nadpisywać otwarte źródło, a AMC ma istniejący kontrakt
edycji oryginału z retencją kopii. Nie zmieniać tego kontraktu ani ustawień
użytkownika bez decyzji. Nie deklarować odporności tego kodu na iCloud,
hardlinki lub utratę zasilania na podstawie samej lektury. Walidacja pakietów
przed persist nie jest odczytem całego pliku po podmianie i późniejszych
zdarzeniach systemu plików.

### 2. Rzeczywiste łączenie MP3 bez dodatkowej stratnej kompresji

`src/mp3.rs:799–891`, `Packed::new/plan/xing_header`, przelicza rozmieszczenie
istniejących danych skal i Huffmana w rezerwuarze bitowym MP3 oraz buduje indeks
Xing. Kod nie ogranicza się do sklejenia dowolnych bajtów; może zwiększyć bitrate
transportowy ramki, a gdy nie ma miejsca, zwraca błąd wymagający innej granicy.
`audio::export_mp3` dekoduje przygotowane pakiety przed ich zatwierdzeniem.

W AMC `AudioClipOriginalEditor.cs` już używa FFmpeg `-c:a copy`, usuwa nagłówki
Xing z części i tworzy końcowy plik z walidacją, a WAV tnie w granicach bloków.
Nie przedstawiać kopiowania ramek jako całkowicie nowej brakującej funkcji AMC.
DirectCut może być porównaniem jakości granic/splice, nie powodem do wymiany
FFmpeg bez testu na syntetycznych fixture CBR/VBR i wielu kolejnych zapisach.
Nie portować teraz parsera MP3 ani całego silnika Rust do C#/wxPython.

### 3. Dostępny model edycji i kontrola zaznaczenia

`src/editor.rs` zawiera migawki Edit, undo/redo i śledzenie źródła ramek oraz
przenoszenie znaczników razem z edycją. README opisuje odsłuch zaznaczenia,
krótki odsłuch po przesunięciu granicy, tekstowy status F6 i jasny wynik zapisu.
`src/timeline.rs:49–75` usuwa WS_TABSTOP z wizualnego paska; te same informacje
są udostępnione tekstowo. `src/announce.rs` rzeczywiście używa Prismer/Prism,
wybiera bezpośredni backend czytnika, pomija backend UIA i nie włącza własnej
mowy przy braku czytnika. Nie dowodzi to zmierzonej dostępności aplikacji:
README deklaruje test NVDA, ale tej oceny nie weryfikowano żywym czytnikiem.

Nie kopiować skrótów i zachowania Spacji do AMC w ciemno. AMC ma własny dojrzały
kontrakt klawiatury i wspólny transport, a DirectCut np. rozróżnia Space od
Ctrl+Space i używa fizycznych pozycji klawiszy interpunkcji układu US.

## Ograniczenia istotne przed wykorzystaniem

README: wynik nie zachowuje ID3/APEv2, okładek, encoder delay/padding i części
pól CUE. AAC-LC mono/stereo ma dekodowanie/walidację; część innych ADTS profili
ma tylko walidację strukturalną. Format, częstotliwość i kanały wstawianych
fragmentów muszą być zgodne. Brak fades/gain/normalizacji i brak dokładności
co do próbki w formatach skompresowanych. Nie wolno utracić istniejących funkcji
AMC pod pozorem przyjęcia gotowego edytora. GUI RunGUI nie jest wkładanym
komponentem wxPython, a projekt nie udostępnia w Cargo.toml gotowej biblioteki
C ABI ani komendy batch służącej do edycji; opisane CLI dotyczy analizy MP3.

## Następny krok w istniejącym zadaniu

Przy diagnostyce AUDIO-EDIT-20261119 użyć powyższych kontraktów jako listy
porównawczej: zachowanie źródła/kopii, jednoznaczny finalny plik, konflikt nazw,
jeden i dwa zapisy, anulowanie/błąd commit oraz zgodna historia AMC. Najpierw
odtworzyć błąd AMC na prywatnym NTFS. Nie podmieniać implementacji tylko dlatego,
że druga ma podobny interfejs lub deklarację bezstratności. Nie używać jedynych
nagrań Michała ani jego iCloud. Ta notatka nie rozpoczyna nowego wdrożenia.

## Materiał do dokumentacji użytkownika

DirectCut RS jest wartościowym wzorem do bezpieczeństwa zapisu i wygodnego
wycinania nagrań z klawiatury. Zbadano kod, nie uruchamiano programu ani NVDA.
Na razie zapisano konkretne rozwiązania do porównania z AMC; niczego nie
zainstalowano i nie zastąpiono. „Bezstratnie” znaczy tu brak kolejnej kompresji
MP3/MP2/AAC, nie obsługę plików FLAC lub gwarancję identycznych próbek przy cięciu.
