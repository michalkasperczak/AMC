# AMC alpha.520 — wydanie Test i Public po konflikcie kanałów

**Korekta użytkownika 24.11.2026:** Cast ma teraz zostać połączony z pełnym zakresem alpha.520, opublikowany w spójnym wydaniu GitHub i automatycznie zainstalowany w Test. Gotowe funkcje obu profili mają być zgodne poza Apple Music. [Alpha.522](RELEASE-ALPHA522-20261124-Cast-instalacja.md) zastępuje poniższe wykluczenie Cast i ograniczenie odsłuchu nagrań tylko do Test; pozostałe wyniki alpha.520 są historyczne.

- Decyzja użytkownika z 23.11.2026: obie wersje mają zawierać ukończone poprawki wideo, wyszukiwania, kolejki, lewej strzałki, czasu i harmonogramu. Apple Music, HEOS i Google Cast pozostają w dalszych pracach; Apple Music eksperymentalnie może być w Test, nie w Public. Odsłuch trwającego nagrania z Test alpha.516 pozostaje tylko w Test.
- Poprzednia decyzja „wideo tylko Test” została zastąpiona zgodą na wideo także w Public. Zapis pełnego zachowania i wyników testów źródłowych znajduje się w [rejestrze alpha.519](RELEASE-ALPHA519-20261123.md), który jest częścią tej samej linii kodu.

## Materiał do dokumentacji użytkownika

- W Plikach i Podcastach/YouTube wybierz „Odtwarzaj wideo” z menu Odtwarzanie, menu kontekstowego albo palety `Ctrl+Shift+K`. Obraz działa w istniejącym odtwarzaczu. Dla pliku lokalnego jest także „Odtwarzaj tylko audio”; przy filmie YouTube online działa jawne „Otwórz w przeglądarce”. Spacja, strzałki, Page Up/Page Down i Escape zachowują zwykłą nawigację.
- `Ctrl+F` pokazuje wykonawców przed albumami i utworami. Lewa strzałka na albumie TIDAL podaje czas albo mówi „Sprawdzam czas albumu”, potem czas lub „Czas trwania nieznany”. Zwykły Enter na odcinku podcastu lub YouTube pobiera najnowszą pozycję z bazy. Poprawka kolejki Spotify Soloist nie dodaje polecenia.
- `Shift+Spacja` na liście harmonogramów pomija najbliższy termin, a ponowne naciśnięcie go przywraca. W Test `Alt+R`, Enter otwiera odsłuch aktywnego nagrania; `F2` zmienia nazwę docelową. Public nie pokazuje tych poleceń ani Apple Music. HEOS i Google Cast są poza obiema paczkami.

## Uzasadnienie i decyzje

- Równoległy Test `alpha.519` z Google Cast miał późniejszy czas commitu niż zweryfikowana paczka bez Cast. Aktualizator odczytywał go jako najnowszy. Nowy commit i paczki `alpha.520` mają rozwiązać ten konflikt bez zmiany znaczenia poleceń.
- Test `20:04` uruchomił się po instalacji, ale starszy aktualizator odrzucił brak możliwości odsłuchu nagrania w manifeście. Nowa linia zachowuje tę możliwość, a Public ma osobny origin YouTube i jej nie zawiera.

## Testy i wynik

- Integracja przed zwiększeniem numeru: 216 testów Python i 55 podtestów profili/wideo/nagrywania/TIDAL/Apple; 132 testy Python i 16 podtestów wznawiania; LiteHost Release bez błędów i ostrzeżeń. Pakiety Test/Public alpha.519 przeszły pełną kontrolę plików i kontrolowany start, instalatory miały `secretScan=passed`.
- Budowa, integralność, publikacja alpha.520 oraz lokalna instalacja: do uzupełnienia po wykonaniu. Odbiór NVDA z rzeczywistym filmem, prywatnym odsłuchem nagrania i sterowaniem Apple Music pozostaje otwarty.

## Znane ograniczenia

- Film YouTube może odmówić osadzenia; wtedy użyj jawnego polecenia otwarcia w przeglądarce. Wymagany jest Edge WebView2 Runtime i sieć.
- Apple Music w Test jest eksperymentalne; odtwarzanie wybranego utworu nie zostało potwierdzone. Wydanie Public nie zawiera Apple Music, HEOS, Google Cast ani odsłuchu aktywnego nagrania.

## Wersja i commit

- `0.1.0-alpha.520`; dokładny commit, identyfikatory pakietów, instalatorów, tagi i stan instalacji będą dopisane po weryfikacji. Kod, testy, build, publikacja i instalacja są osobnymi faktami.

## Szkic instrukcji i listy zmian do korekty

„Zaznacz film w Plikach albo Podcastach i YouTube, wybierz «Odtwarzaj wideo» i używaj zwykłych przycisków odtwarzacza. Wyniki wyszukiwania pokazują najpierw wykonawców, lewa strzałka na albumie TIDAL podaje czas, a Enter na podcaście wznawia od aktualnego zapisu. Test zachowuje dodatkowo odsłuch trwającego nagrania. Public nie ma Apple Music, HEOS ani Google Cast.”

Ten tekst jest szkicem do korekty, nie opublikowaną instrukcją.
