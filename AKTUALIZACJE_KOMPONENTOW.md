# Aktualizacje AMC, bibliotek multimedialnych i dodatków

Stan zasad: 2026-08-29. Dokument uzupełnia rozdział 13 specyfikacji
`MEDIA_CONTROLLER_PL.md`.

## 1. Znaczenie słowa „aktualne”

W tym projekcie chodzi o **FFmpeg**, a nie o format MPEG. AMC i powiązane
dodatki mają używać najnowszej wersji zatwierdzonej jako zgodna, bezpieczna
i licencyjnie dopuszczalna. Nie pobieramy automatycznie każdej nowej biblioteki
w dniu jej publikacji. Każda zmiana dekodera może zmienić obsługę uszkodzonych
plików, HLS, anulowania połączeń, czasu trwania albo urządzeń audio, dlatego
przechodzi testy przed udostępnieniem użytkownikom.

Nie instalujemy globalnych pakietów kodeków i nie podmieniamy bibliotek
systemowych. Składniki należą do konkretnej wersji AMC albo konkretnego dodatku.

## 2. Inwentarz wymagający stałej kontroli

- sam AMC i samowystarczalne środowisko .NET;
- Microsoft.Data.Sqlite oraz natywne SQLite;
- NAudio, NAudio.Vorbis i NLayer;
- SoundTouch.Net i warstwa NAudioSupport;
- BASS oraz ewentualne rozszerzenia BASS, z uwzględnieniem licencji
  niekomercyjnej;
- opcjonalny FFmpeg dla HLS i przyszłych izolowanych konwerterów;
- przyszłe adaptery usług, urządzeń, podcastów i YouTube;
- cienka wtyczka NVDA oraz osobne dodatki, w tym FreeRadio;
- dane zgodności stacji i katalogów, które mogą być aktualizowane bez kodu.

Każde wydanie zapisuje w informacjach diagnostycznych wersję aplikacji,
wersję środowiska, architekturę oraz wersje faktycznie załadowanych składników.
Brak składnika opcjonalnego jest odróżniany od jego awarii.

## 3. Manifest wydania

Serwer wydania przekazuje podpisany manifest. Każdy wpis zawiera:

- stabilny identyfikator komponentu, wersję, system i architekturę;
- minimalną oraz maksymalną zgodną wersję API hosta;
- adres paczki, rozmiar, SHA-256 i podpisane metadane;
- zależności i informację, czy element jest wymagany;
- licencję, źródło, konfigurację kompilacji i SBOM;
- kanał Stabilny albo Beta oraz poziom krytyczności;
- listę testów zgodności zaliczonych przez paczkę.

HTTPS nie zastępuje podpisu. Aplikacja nie uruchamia w komputerze użytkownika
`dotnet restore`, NuGet, pobranego skryptu ani polecenia powłoki.

## 4. Bezpieczna instalacja i cofanie

1. Metadane są sprawdzane w tle bez kradzieży fokusu i bez przerywania mowy.
2. Pobierany jest cały zgodny zestaw, nigdy przypadkowa mieszanina DLL.
3. Pliki trafiają do katalogu tymczasowego z limitem rozmiaru.
4. Przed użyciem sprawdzane są podpis, SHA-256, wersje, zależności i licencje.
5. Nowy zestaw powstaje obok bieżącego; działające pliki nie są nadpisywane.
6. Aktywacja następuje po bezpiecznym zamknięciu AMC lub NVDA, nigdy podczas
   odtwarzania albo nagrywania.
7. Po uruchomieniu wykonywany jest test zdrowia: host, baza, główne okno,
   dekoder i podstawowy test dostępności.
8. Brak znacznika zdrowia powoduje powrót do poprzedniej działającej wersji.
9. Dane użytkownika, SQLite, ustawienia, presety, playlisty i zakładki pozostają
   poza katalogiem programu i nie są cofane razem z plikami wykonywalnymi.

## 5. FFmpeg i kodeki

Docelowy FFmpeg jest opcjonalnym, osobno rozpoznawalnym komponentem AMC
o udokumentowanej konfiguracji zgodnej z LGPL, bez przypadkowego dołączenia
części GPL lub `nonfree`. Pakiet zawiera licencje i wymagane źródła lub
jednoznaczne odsyłacze zgodne z warunkami dystrybucji.

Przed zatwierdzeniem nowej wersji sprawdzamy co najmniej: MP3, AAC, OGG/Vorbis,
FLAC, WAV, MP4/M4A, HLS audio, HLS z obrazem, duży plik, uszkodzony plik,
anulowanie, szybkie przełączanie, przewijanie, nagrywanie oraz zamknięcie
procesu potomnego. Nowy komponent nie może zmienić aktywnego urządzenia audio
ani przejąć wyłącznego dostępu, którego potrzebuje NVDA.

## 6. Dodatki NVDA i FreeRadio

Dodatek NVDA jest publikowany jako kompletna, wersjonowana paczka. Aktualizator
nie modyfikuje plików zainstalowanego dodatku, gdy NVDA działa. Nowy pakiet
jest aktywowany dopiero po ponownym uruchomieniu czytnika.

Audyt z 2026-08-29 potwierdził aktywną instalację FreeRadio
`2026.23.98`. Ta wersja nadal zawiera własny FFmpeg z 2018 roku
(`N-92510-gfa08345e88`). Nie podmieniamy go po cichu wewnątrz paczki
`.98`. Aktualizacja wymaga następnego numeru dodatku, sprawdzenia rozmiaru,
licencji oraz testów odtwarzania, konwersji, HLS, anulowania i sprzątania
plików tymczasowych.

Docelowo każdy dodatek ma własny manifest lub korzysta ze wspólnego,
podpisanego menedżera komponentów. Nie wolno, aby dwa dodatki samodzielnie
nadpisywały ten sam plik wykonywalny w miejscu.

## 7. Kolejność wdrażania

- obecne wersje alpha pozostają przenośne i podają wersje komponentów
  diagnostycznie;
- CI otrzymuje przypięte zależności, pliki blokady, audyt podatności, licencje,
  SBOM oraz skróty SHA-256 publikacji;
- przed publiczną betą powstają podpisany manifest, pobieranie do stagingu,
  aktywacja po restarcie i automatyczny rollback;
- dopiero po przejściu testów aktualizacje mogą pobierać się automatycznie
  w tle; instalacja nadal czeka na bezpieczne zamknięcie.

Do czasu wdrożenia tego mechanizmu pozycja „Sprawdź aktualizacje” nie może
udawać działającego serwera. Informuje uczciwie, że usługa nie została jeszcze
skonfigurowana.
