# AMC Test alpha.467 — AirPlay i drugie wyjście lokalne

## Status — 2 listopada 2026

To roboczy zapis kolejnej aktualizacji AMC Test. Bazą jest wydane alpha.466,
scalone z główną gałęzią roboczą. Kod alpha.467 i izolowana paczka kandydata
istnieją; instalacja na komputerze użytkownika i publikacja wymagają osobnych
wyników. Poprzednie stwierdzenie w MIRROR-001, że AirPlay i dwa wyjścia lokalne
nie są w interfejsie, opisuje wyłącznie alpha.466.

## Zachowanie użytkowe

W sesjach Pliki, Radio i Podcasty `Shift+M` otwiera okno „Dublowanie dźwięku”.
Lista zawiera „Wyłącz dublowanie” i dostępne cele z prefiksami „Wyjście
lokalne”, „Google Cast” i „AirPlay”. `Shift+A` nadal wybiera jedno podstawowe
wyjście lokalne, a `Shift+C` przełącza odtwarzanie na Cast. Wybór drugiego
lokalnego wyjścia uruchamia osobny proces hosta AMC i ten sam materiał na
wybranym urządzeniu Windows. AirPlay przesyła dźwięk do odbiornika RAOP przez
pyatv i FFmpeg. Wybór celu nie zatrzymuje podstawowego dźwięku AMC. Ponowne
otwarcie pokazuje wybrany cel; Escape anuluje, Enter zatwierdza, „Odśwież
urządzenia” ponawia listę, a po zamknięciu fokus wraca do AMC.

Dublowanie nie obejmuje dźwięku Windows, NVDA ani innych aplikacji. Urządzenia
mają niezależne zegary; opóźnienia i echo są możliwe. AirPlay jest dostępny,
gdy pyatv i FFmpeg są obecne i wykryty jest odbiornik RAOP. Osobny proces
lokalny nie zapisuje kolejki ani ustawień profilu.

Komunikat po wyborze: „Dublowanie włączone: ten komputer i [nazwa]. Możliwe
opóźnienie między urządzeniami”. Po wyłączeniu: „Dublowanie wyłączone”.
Awaria celu dodatkowego ma pozostawić podstawowe odtwarzanie i ogłosić
„Utracono AirPlay” albo „Utracono drugie wyjście audio”.

## Implementacja i uzasadnienie

Wspólne polecenie `audio.mirrorSession` dostarcza skrót, menu, pomoc kontekstową,
pełną pomoc i paletę. Lista oraz model celu przechowują osobno identyfikator
techniczny i etykietę użytkową. AirPlay uruchamia zadanie w osobnym wątku,
przekształca bieżący materiał do strumienia MP3 oraz restartuje od aktualnej
pozycji po pauzie lub przewinięciu. Błąd transkodowania jest zgłaszany.
Zmiana celu jest atomowa dla danego typu, a błędy nie zatrzymują hosta głównego.

## Wyniki sprawdzenia

- Źródła: 26 celowanych testów `test_mirror_*.py` zaliczonych 2 listopada 2026.
- Budowa: kandydat alpha.467 z izolowanym Pythonem, pyatv 0.18.0 i srptools
  1.0.1 zbudowany lokalnie; manifest integralności i kontrolowany start GUI
  oraz hosta przeszły. To była budowa z niezatwierdzonych źródeł.
- Odbiornik: wykryto WiiM Pro Plus przez RAOP z izolowanego runtime.
- Ręczny odsłuch nowego toru AirPlay, dwóch wyjść lokalnych oraz nawigacja NVDA
  po oknie wymagają jeszcze sprawdzenia.

## Ograniczenia i następne kroki

Automatyczne przejście do następnego utworu kolejki może nie przełączyć
dodatkowego celu. AirPlay może mieć przerwę po przewinięciu i inne opóźnienie
od lokalnego wyjścia; zmiana głośności i tempa w AMC nie jest jeszcze
odzwierciedlana na AirPlay. Uwierzytelniane odbiorniki i treści chronione nie
są potwierdzone. Przed wydaniem sprawdzić próbę sprzętową, nawigację NVDA,
licencje zależności, źródła po commicie, ponowić budowę z czystego commitu
i dopiero odnotować instalację i publikację.

## Porządkowanie gałęzi

116 pierwotnie zmienionych plików zapisano w zweryfikowanym archiwum ZIP i
stash Git. 79 plików miało odpowiedniki w historii wydania; scalono bazę
alpha.466 z gałęzią roboczą. Pozostałych 37 nie nadpisywano masowym scalaniem:
wersje zachowano w archiwum i stash. Dziewięć dawnych, niepowiązanych
dokumentów przeniesiono do osobnego katalogu `odlozone-dokumenty` z manifestem
SHA-256. Automatyczna recenzja odrzuciła heurystyczne rozwiązywanie 16
konfliktów; nie wykonano tej operacji. Wcześniejszą poprawkę słownictwa
integracji z Windows przywrócono z konkretnego commitu 9266fd4f.

## Wersja i commit

Planowana wersja `0.1.0-alpha.467`. Commit funkcji, instalator, zdalny workflow,
tag i stan instalacji trzeba uzupełnić po ich rzeczywistym potwierdzeniu.

## Wynik wydania — 02.11.2026, po GitHub Actions

Commit funkcji `a415e5066d0b04078ee1146a83d16c94dd14fd7d` został wysłany na gałąź testową GitHub. Workflow 41508760594 przeszedł: pobrał bazę alpha.466 o sprawdzonej sumie, dołożył zamknięty zestaw bibliotek AirPlay, zbudował paczkę i instalator, sprawdził integralność i kontrolowany start, po czym opublikował prerelease `test-2026.11.02.1921`. Zdalny tag wskazuje dokładnie commit funkcji. Zasób `AMC-Test-2026-11-02-1921.exe` ma 143412542 bajty i SHA-256 `77dc6721591e5d0d9d5f79bb23482d3fff09e1326b9edebe657714cd69b78286`. Pobrana do folderu projektu kopia ma identyczny rozmiar i sumę, a plik integralności podaje profil `test`, wersję `0.1.0-alpha.467`, ten sam commit źródeł, wynik skanowania sekretów `passed` i sumę dodatku NVDA.

Źródło, zaliczone testy, lokalna budowa, opublikowany instalator i instalacja użytkownika są odrębnymi stanami. Instalacji na komputerze użytkownika nie wykonano. Ręczny odsłuch AirPlay z nowego kodu, odsłuch dwóch lokalnych wyjść oraz odbiór NVDA w zainstalowanym alpha.467 nadal czekają na użytkownika. Dziewięć odłożonych dokumentów przeniesiono w obrębie tego samego dysku, więc fizycznie nie odzyskano miejsca; ich dokładne sumy zapisuje manifest archiwum. ZIP i stash pierwotnych 116 zmian pozostają zachowane.

## Próba AirPlay z paczki — 02.11.2026, po wydaniu

Po potwierdzeniu gotowości użytkownika uruchomiono z wydanej paczki 20-sekundowy sygnał testowy na wykrytym WiiM Pro Plus. Router AirPlay zgłosił rozpoczęcie i zakończenie bez błędu procesu, a po teście nie pozostał proces FFmpeg ani host AMC. Potwierdzenie dźwięku w kolumnach przez użytkownika pozostaje otwarte. Przy zamykaniu testowego interpretera pojawił się niekrytyczny komunikat `asyncio` o zamkniętym potoku Proactor; wymaga zbadania w kolejnej poprawce i nie wolno uznać tej próby za pełny odbiór.

Korekta decyzji o skrótach jest w rozmowie: rekomendowany układ to `Shift+A` dla jednego głównego celu lokalnego, Cast lub AirPlay, `Shift+M` dla celu dodatkowego, `Shift+C` jako szybki wybór Cast. Wydane alpha.467 oferuje AirPlay wyłącznie jako dodatkowy cel w `Shift+M`. Zmiana głównego toru AirPlay wymaga osobnej implementacji; nie przedstawiać rekomendacji jako już wydanej funkcji.

## Korekta po odsłuchu i diagnostyce — 02.11.2026

Użytkownik potwierdził dźwięk pierwszej 20-sekundowej próby w kolumnach Denona
przez WiiM Pro Plus. To zastępuje wcześniejsze zdanie „Potwierdzenie dźwięku
pozostaje otwarte”. Powtórka była niesłyszalna. Późniejsza próba zgłosiła
`mirror.failed` po ok. 10 sekundach: odbiornik nie odpowiedział na żądanie
rozpoczęcia strumienia RAOP. Próba starszym wariantem AirPlay zakończyła się
zamknięciem połączenia przez odbiornik. Następna próba wariantem automatycznym
nie dostała już odpowiedzi na wstępne zapytanie RAOP. Potwierdzenie pojedynczego
odsłuchu nie dowodzi stabilności AirPlay w alpha.467; powtarzalny odsłuch jest
otwarty. Lokalny dźwięk AMC pozostaje niezależny od awarii AirPlay.

Po tych wynikach źródło kandydata zmieniono: zwykły potok procesu FFmpeg
usunął ostrzeżenie zamykania potoku Proactor z próby diagnostycznej. Router
czeka na potwierdzenie odtwarzania przez RAOP przed zgłoszeniem włączenia
dublowania, podejmuje najwyżej jedną ponowną próbę, jeśli odbiornik nie
odpowiada przed rozpoczęciem, i zwraca czytelny komunikat z nazwą odbiornika
bez tekstu protokołu. Testy celowane: 20 zaliczonych, w tym nowe przypadki
niepowodzenia startu i ponawiania. To są zmiany źródła w następnej wersji;
instalator alpha.467 ich nie zawiera. Nie zbudowano, nie zainstalowano ani nie
opublikowano nowszego wydania po tej korekcie.

Użytkownik zatwierdził przyszły układ skrótów: `Shift+A` wybierze jeden główny
cel lokalny, Cast lub AirPlay; `Shift+M` drugi cel, a `Shift+C` zachowa szybki
wybór Cast. Tego nie ma jeszcze w alpha.467. Przed następnym testem sprzętowym
warto ponownie uruchomić usługę AirPlay na WiiM Pro Plus lub urządzenie,
ponieważ przestało odpowiadać nawet na wstępne zapytanie. To hipoteza
diagnostyczna, nie potwierdzona przyczyna usterki.

Poprawkę źródła zatwierdzono w commicie `807df16c` i wysłano na gałąź testową.
To uruchomiło starszy automatyczny workflow wydania. Bieg `41519618586`
anulowano; krok publikacji został pominięty. Wyzwalacz workflow zmieniono w
commicie `7a49f9a7` na uruchamianie ręczne. Opublikowany instalator alpha.467
pozostaje zbudowany z wcześniejszego commitu `a415e506`; poprawka startu
AirPlay jest obecnie wyłącznie w źródłach gałęzi testowej.

## Uzupełnienie od użytkownika — 02.11.2026

Użytkownik uruchomił stację radiową bez AirPlay i potwierdził, że WiiM Pro
Plus odtwarza ją prawidłowo. Koryguje to wcześniejszą ogólną interpretację
„odbiornik nie odpowiada”: brak odpowiedzi stwierdzono konkretnie w usłudze
AirPlay/RAOP podczas prób AMC; nie wykazano awarii zwykłego odtwarzania WiiM.
Ponowne uruchomienie urządzenia pozostaje jedynie możliwą próbą diagnostyczną,
nie wymogiem ani potwierdzoną naprawą. Dalszy odsłuch odłożono. Przy powrocie
należy najpierw odczytać faktycznie wykrywane odbiorniki AirPlay i porównać
WiiM z innym dostępnym odbiornikiem, np. zgodnym modelem Sonos; iPhone został
wskazany przez użytkownika jako kandydat do sprawdzenia, bez potwierdzenia,
że oferuje usługę odbiornika. Nie publikować kolejnego instalatora jako
naprawionego AirPlay bez powtarzalnej próby sprzętowej.

## Wskazówka o Zaporze Windows — 02.11.2026

Użytkownik zgłosił niedostępne dla niego okno zabezpieczeń Windows po próbach.
Podgląd ekranu pokazał alert Zapory Windows dla programu „Python” z wydawcą
„Python Software Foundation”. Nie potwierdzono ścieżki procesu ani tego, że
alert spowodował awarię RAOP. Może on dotyczyć procesu diagnostycznego,
dlatego przed następnym testem sprawdzić dokładny proces i stan uprawnień
sieciowych. Asystent nie zatwierdził ani nie odrzucił żądania Zapory; zgodnie
z regułami narzędzia Computer Use nie steruje oknami zabezpieczeń ani
żądaniami uprawnień. Brak nowej próby dźwiękowej i brak nowego wydania.

## Próba podczas odtwarzania radia na WiiM — 02.11.2026

Użytkownik poprosił o jeszcze jedną próbę AirPlay, gdy WiiM Pro Plus odtwarzał
stację radiową własnym sposobem. Adres zapamiętany z wcześniejszych prób
przestał przyjmować połączenia. Odczyt sieci i świeże wykrywanie RAOP
potwierdziły nowy adres urządzenia. Wstępne `GET /info` pod nowym adresem
zwróciło `200 OK`, również przez samą bibliotekę pyatv. Tym samym wcześniejsza
hipoteza o niedziałającej stale usłudze RAOP wymaga korekty: usługa odpowiadała
na zapytania kontrolne przed i po próbie.

Pełny 20-sekundowy test poprawionego źródła AMC nie potwierdził rozpoczęcia
strumienia. Po najwyżej jednej ponownej próbie router zgłosił brak odpowiedzi
na zapytanie wstępne. Kod nie zapisał błędu pierwszego podejścia oddzielnie,
więc nie przypisywać całego niepowodzenia do jednego etapu protokołu. Po
teście nie pozostał proces FFmpeg. Nie wykonano dalszych prób dźwiękowych,
nie zmieniono zgód zapory i nie zbudowano nowego wydania. Przy następnym
podejściu dodać rozróżnienie diagnostyczne pierwszego i drugiego połączenia
oraz sprawdzić wpływ istniejących alertów Zapory Windows bez automatycznego
zatwierdzania ich.
