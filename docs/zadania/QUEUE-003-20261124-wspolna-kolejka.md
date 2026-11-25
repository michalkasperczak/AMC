# QUEUE-003 — opcjonalna wspólna kolejka materiałów

Stan: wymaganie zapisane 24.11.2026; rozpoznanie architektury uzupełnione 25.11.2026. Nie zaimplementowano, nie budowano i nie wydano.

## Cel i zakres

Michał chce w ustawieniach możliwość wyboru, czy lokalne pliki, podcasty i ewentualnie inne odtwarzalne materiały mają trafiać do jednej wspólnej kolejki, czy pozostać w kolejkach sesji. Wspólna kolejka ma być przewidywalnym kontenerem, którego lista i polecenia pokazują tylko rzeczywiście obsługiwane pozycje. Zwykłe Radio na żywo nie ma kolejki; jest to osobna poprawka w `QUEUE-002-przeglad-kolejek-sesji-internetowych.md`. Kolejki sterowane przez zewnętrzne urządzenia lub konta nie mogą udawać, że należą do lokalnego hosta.

## Uzasadnienie i decyzje

- Ustawienie powinno jawnie wybierać tryb „kolejki osobne” albo „wspólna kolejka”, z domyślnym zachowaniem dotychczasowym dla istniejących profili. Zmiana ustawienia nie może bez pytania usuwać ani scalać zapisanych pozycji.
- Każdy wpis wspólnej kolejki przechowuje stabilną tożsamość, rodzaj materiału, sesję źródłową i wskazanie sposobu odtworzenia. Zwykły strumień Radia nie jest pozycją kolejki; odcinek podcastu i lokalny plik mogą być, jeśli istnieje działający adapter odtwarzania.
- Przejście między różnymi silnikami odtwarzania musi jawnie przełączyć kontekst, zachować fokus i ogłosić materiał. Wpis, którego źródło znikło, ma dać czytelny błąd i możliwość pominięcia lub usunięcia, bez zatrzymania całej kolejki. Nie wolno przenosić technicznych identyfikatorów do etykiet NVDA.
- Filtrowanie jest tylko widokiem. Przenoszenie, `Ctrl+X`, wklejanie, Delete, następny/poprzedni i zapis działają na stabilnych wpisach, a po operacji fokus zostaje na najbliższym widocznym wierszu. Sortowanie nie może po cichu zmienić trwałego ręcznego porządku.
- Menu, paleta, `Ctrl+F1`, pełna pomoc i skróty muszą czerpać dostępność z tego samego modelu możliwości: na żywym Radiu kolejka pozostaje ukryta, a nieobsługiwane źródło nie dostaje martwego „Dodaj do wspólnej kolejki”.

## Testy i wynik

Na razie brak kodu i testów tej opcji. Rozpoznano istniejące osobne kolejki Plików, Podcastów, TIDAL, Spotify, Radio Archiwisty i HEOS. Poprawka wyłączenia kolejki w zwykłym Radiu ma 118 zaliczonych testów celowanych we wspólnej gałęzi, lecz nie jest implementacją QUEUE-003.

## Materiał do dokumentacji użytkownika — szkic

Ustawienie „Sposób kolejki” wybierze kolejki osobne lub jedną wspólną kolejkę dla obsługiwanych plików i odcinków. Zwykłe stacje Radia nadal grają na żywo i nie są dopisywane. Pozycje z różnych źródeł będą miały czytelną nazwę i źródło, a AMC poinformuje, gdy któregoś materiału nie da się już odtworzyć. To jest szkic planowanej funkcji, nie opis obecnej wersji.

## Znane ograniczenia, migracja, wersja i commit

Nie ustalono jeszcze technicznego połączenia lokalnego hosta z odtwarzaniem usług zewnętrznych. W pierwszym etapie trzeba rozpoznać zdolność każdego adaptera i zaprojektować zapis bez utraty starych kolejek. Nie ma wersji ani commitu implementacyjnego. Następny krok: tabela możliwości źródeł i scenariusze migracji, po czym testy parytetu poleceń oraz przepływu mieszanego lokalny plik → podcast → niedostępny materiał.

## Rozpoznanie źródeł i zapisu, 25.11.2026

Ta analiza rozwija następny krok zapisany 24.11.2026; nie zmienia wcześniejszej decyzji o domyślnych osobnych kolejkach. `Ctrl+Q` w zwykłym Radiu jest już ukryte i zainstalowane w Test alpha.527, zgodnie z korektą `QUEUE-002` z 25.11.2026.

| Źródło | Obecny zapis i odtwarzanie | Wspólna kolejka |
| --- | --- | --- |
| Pliki lokalne | Trwała kolejka `LiteQueueCoordinator` i `LiteQueueStore`; host pilnuje następnego utworu. | Kandydat podstawowy, po sprawdzeniu dostępności pliku przy starcie. |
| Odcinki Podcastów i zapisane materiały YouTube | Członkostwo i kolejność w bazach podcastów i biblioteki; `media.play` obsługuje lokalny plik lub HTTP(S), z opcjami wznowienia i tempa odcinka. | Kandydat podstawowy, ale wspólny silnik musi zachować opcje i postęp odcinka. |
| Nagrania Radio Archiwisty | Osobna kolejka nagrań; odtwarzanie przez `media.play`. | Możliwy późniejszy adapter po teście przejścia między sesjami. |
| TIDAL i Spotify | Oddzielne kolejki i własne drogi sterowania odtwarzaniem. | Wymagają adapterów następnego/poprzedniego, autoryzacji i błędów usługi; nie wolno ich udawać przez wpis lokalnego hosta. |
| HEOS oraz kolejki odbiorników sieciowych | Kolejkę prowadzi urządzenie, nie lokalny host. | Pozostają kolejkami urządzeń, chyba że powstanie osobny, sprawdzony adapter przekazywania materiału. |
| Radio na żywo | Nie ma kolejki materiałów. | Wyłączone. |

Istniejące `queue.set` w hoście przyjmuje elementy jako lokalne utwory i tworzy dla nich jedną sesję silnika. Samo połączenie wierszy Plików i Podcastów w GUI nie zapewni więc przejścia na kolejny odcinek ani jego ustawień wznowienia, tempa i postępu. Implementacja wymaga wspólnego właściciela porządku i dyspozytora odtwarzania, który przekazuje każdy wpis do właściwego adaptera. Odczyt `Ctrl+Q` nie może sam uruchamiać dźwięku.

## Warunki wdrożenia i migracja

1. Nowy tryb jest opt-in. Pierwsze włączenie zakłada pustą wspólną kolejkę; osobne kolejki, ich porządek i aktualnie odtwarzany materiał pozostają zapisane. Przełączenie trybu nie kopiuje i nie usuwa wpisów. Ewentualny import ze starych kolejek będzie oddzielnym, jawnym poleceniem z podglądem.
2. Wpis ma trwały klucz złożony z sesji źródłowej i identyfikatora materiału, widoczny tytuł oraz typ adaptera. Przed odtworzeniem adapter ponownie rozwiązuje bieżące źródło; zapis nie może zależeć od starego adresu strumienia ani zawierać danych logowania. Zmiana tytułu w bibliotece może odświeżyć etykietę bez zmiany tożsamości.
3. Jedna trwała kolejność obejmuje tylko źródła z działającym adapterem. `Ctrl+Q` w kwalifikujących się sesjach pokazuje ten sam porządek, a `Ctrl+Shift+Q`, „Odtwórz jako następne”, `Alt+strzałki`, Delete, filtr i Enter używają tych samych kluczy. W Radiu polecenie pozostaje niedostępne. Usługi bez adaptera nadal pokazują własną kolejkę i jej własną nazwę.
4. Jeśli wpisu brakuje albo usługa odmówi startu, AMC podaje jego nazwę i przyczynę, zachowuje pozostałe wpisy i pozwala pominąć lub usunąć problematyczny wpis. Błąd nie może uruchomić nieograniczonej pętli prób. Filtr i sortowanie widoku nie zmieniają trwałej kolejności; tylko jawne przeniesienie ją zmienia.
5. Test odbiorczy obejmuje: odczyt bez uruchomienia dźwięku; Pliki → Podcasty i odwrotnie; odcinek HTTP(S) i pobrany; zachowanie wznowienia, tempa, postępu i wyjścia audio; brak źródła; koniec kolejki; zmianę trybu przy aktywnym odtwarzaniu; restart; osobne kolejki po powrocie; fokus, etykiety NVDA i parytet menu, palety, pomocy i skrótów.

Zakres pierwszej wersji obejmujący usługi zewnętrzne pozostaje do ustalenia z Michałem. Do tego czasu nie dodawać ustawienia, które obiecuje wspólną kolejkę bez działającego dyspozytora odtwarzania. Brak zmian w źródłach oraz brak testów implementacji na tym etapie.

Korekta 25.11.2026: osobne zadanie [QUEUE-004](QUEUE-004-20261125-shift-enter-nastepne.md) zapisuje przywrócenie `Shift+Enter` i kolejność pozycji „Odtwarzaj jako następne”. Skrót ma działać przewidywalnie zarówno przy obecnych osobnych kolejkach, jak i w przyszłym trybie wspólnym; ten zapis nie oznacza wdrożenia żadnego z nich.
