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
