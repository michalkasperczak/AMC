# Osobny import i eksport danych Radia AMC

## Cel i stan bieżący

Pełna kopia `*.amcbackup.json` już obejmuje całą Bibliotekę Radia, stan Ulubionych,
playlisty, presety i harmonogramy nagrywania. Służy do wiernego odtworzenia własnego
AMC, dlatego może zachowywać lokalne identyfikatory oraz pełne ścieżki folderów.

Osobne eksporty służą innemu celowi: wybraniu danych, przesłaniu ich innej
osobie i bezpiecznemu scaleniu z jej stanem. Alpha 149 ustaliła poniższe
zasady. Od `alpha.294` działa pierwszy szybki eksport, a od `alpha.296` ma
skrót `Ctrl+E`: Ulubione do przenośnej
playlisty M3U.

## Zakresy

- `*.m3u` — szybka, standardowa lista Ulubionych zawierająca wyłącznie nazwę
  użytkową i trwały publiczny adres stacji w aktualnej kolejności widoku;
- `*.amcradio.json` — wybrane albo wszystkie własne stacje, ich przyjazne nazwy,
  publiczne adresy, stan Ulubionych i opcjonalnie przynależność do playlist Radia;
- `*.amcschedules.json` — wybrane albo wszystkie harmonogramy nagrywania;
- eksport „Wszystkie dane Radia” może umieścić oba zakresy w jednej wersjonowanej
  kopercie, ale podczas importu nadal pozwala wybrać ich składniki.

Presety są prywatnymi skrótami interfejsu. Domyślnie nie wchodzą do pliku służącego
do przesyłania Ulubionych; mogą zostać dodane tylko jako jawnie wybrany składnik.

## Przenośny harmonogram

Rekord przenośny zachowuje nazwę stacji, jej normalizowany publiczny adres,
powtarzanie, dni tygodnia, lokalną datę i godzinę pierwszego wystąpienia, nazwę
strefy czasowej, długość, format nagrania oraz regułę wybudzania. Nie opiera się
wyłącznie na lokalnym identyfikatorze AMC.

Bezwzględna ścieżka folderu nagrań nie jest domyślnie udostępniana innej osobie.
Zaimportowany plan dziedziczy jej domyślny folder nagrywania, chyba że odbiorca
jawnie wskaże własny folder podczas importu. Każdy importowany plan jest początkowo
wyłączony. Użytkownik musi sprawdzić strefę czasową, datę, folder oraz wybudzanie
i dopiero potem go włączyć. Import nigdy sam nie rozpoczyna nagrania ani nie
wybudza komputera.

## Scalanie

1. Stację porównuje się po znormalizowanym adresie kanonicznym, nie po samej nazwie.
2. Zgodny adres łączy rekordy bez cichego nadpisywania lokalnej nazwy i bogatszych
   metadanych. Konflikt nazwy jest przedstawiany użytkownikowi.
3. Każdy nowy plan otrzymuje nowy lokalny identyfikator. Duplikaty wykrywa się po
   stacji, lokalnym czasie, strefie, powtarzaniu, dniach i długości.
4. Import jest transakcyjny: Anuluj nie zapisuje części danych, a błąd nie niszczy
   istniejących Ulubionych ani harmonogramów.
5. Podsumowanie podaje liczby dodanych, połączonych, pominiętych i konfliktowych
   wpisów, bez ujawniania adresów zawierających dane prywatne.

## Prywatność i bezpieczeństwo

- plik nie zawiera haseł, tokenów OAuth, ciasteczek, kluczy, nagłówków autoryzacji
  ani tymczasowych podpisanych adresów;
- eksportowany jest tylko adres HTTP lub HTTPS zatwierdzony jako trwałe źródło;
- parser ma numer schematu, limity rozmiaru i liczby rekordów oraz odrzuca kod,
  nieprawidłowe typy i niebezpieczne ścieżki;
- zapis używa pliku tymczasowego i atomowego zastąpienia pliku końcowego;
- starszy lub nowszy nieobsługiwany schemat daje czytelny komunikat, a nie częściowy
  import;
- data jest przenoszona wraz z semantycznym identyfikatorem strefy czasowej. Warstwa
  importu tłumaczy identyfikatory Windows i IANA, co jest konieczne dla przyszłego
  macOS.

## Interfejs docelowy

Polecenie **Eksportuj ulubione stacje do playlisty…** jest dostępne tylko w
sesji Radio internetowe, w menu Plik i pod `Ctrl+Shift+O`. Zapisuje rozszerzone
M3U w UTF-8 i publikuje plik dopiero po ukończeniu zapisu. `Ctrl+O` importuje
go ponownie bez dublowania identycznych adresów. Dla publicznego YouTube
zapisywany jest stabilny adres strony, nigdy krótkotrwały podpisany adres audio.

Docelowe polecenia **Eksportuj dane Radia…** i **Importuj dane Radia…** pozwolą
wybrać Ulubione, playlisty, presety i harmonogramy oraz pokażą konflikty przed
zapisem. Osobne **Eksportuj harmonogramy…** przekaże sam plan nagrań bez
Biblioteki i Ulubionych.
