# Osobny import i eksport danych Radia AMC

## Cel i stan bieżący

Pełna kopia `*.amcbackup.json` już obejmuje całą Bibliotekę Radia, stan Ulubionych,
playlisty, presety i harmonogramy nagrywania. Służy do wiernego odtworzenia własnego
AMC, dlatego może zachowywać lokalne identyfikatory oraz pełne ścieżki folderów.

Planowane osobne eksporty służą innemu celowi: wybraniu danych, przesłaniu ich
innej osobie i bezpiecznemu scaleniu z jej stanem. Alpha 149 ustala poniższe zasady,
ale nie udostępnia jeszcze osobnych poleceń w interfejsie.

## Zakresy

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

Polecenia **Eksportuj dane Radia…** i **Importuj dane Radia…** będą dostępne tylko
w sesji Radio internetowe. Eksport pozwoli zaznaczyć Ulubione, playlisty, presety
i harmonogramy. Import pokaże dostępne składniki, konflikty oraz podsumowanie przed
Zapisz. Osobne szybkie polecenie **Eksportuj harmonogramy…** pozwoli przekazać sam
plan nagrań bez Biblioteki i Ulubionych.
