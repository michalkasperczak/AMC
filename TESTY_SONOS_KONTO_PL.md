# Testy okna "Konto Sonos" — instrukcja dla użytkownika

Dotyczy nowego okna **Konto Sonos** w AMC. Okno jest gotowe i zmierzone bez
pokazywania obrazu; test z żywym NVDA jest osobnym krokiem i wykonuje go
właściciel pulpitu.

## Co to okno robi

Logujesz się **zwykłym kontem Sonos**. Hasło wpisujesz wyłącznie w oficjalnej
przeglądarce na stronie Sonos — AMC go nie widzi i nie zapisuje.

W oknie **nie ma żadnych pól deweloperskich**: nie ma Client ID, sekretu ani
adresu powrotu. Te dane należą do serwera logowania AMC, więc nie masz tam czego
wpisywać. (Starsze okno konta TIDAL posłużyło tylko za wzór wyglądu — jego pola
deweloperskie NIE zostały tu przeniesione.)

## Układ okna i kolejność czytania

1. **Na początku pole tylko do odczytu** ze stanem konta i krótką instrukcją.
   Tam stoi kursor po otwarciu okna, więc czytnik przeczyta tę treść jako
   pierwszą. Możesz ją przejrzeć strzałkami i skopiować — to pole, nie sam napis.
2. Dalej **Tab** prowadzi po przyciskach w kolejności działań.
3. Na dole jest **pole ostatniego komunikatu**. Każdy komunikat zostaje w nim
   zapisany, więc możesz do niego wrócić i przeczytać go ponownie, gdy mowa
   zdąży ucichnąć.

Kolejność czytania wynika z budowy okna — nie z żadnego opóźnienia ani timera.

Nazwy przycisków dla czytnika nie zawierają dopisanego skrótu (np. „Alt+L”),
bo klawisz dostępu czytnik ogłasza sam, a dopisek brzmiałby jak podwojenie.

## Przyciski

Przyciski, które w danym momencie nic nie robią, **są ukryte** — nie trafiasz na
martwe klawisze podczas tabulacji.

| Przycisk | Kiedy się pokazuje | Co robi |
|---|---|---|
| Zaloguj w przeglądarce | zawsze | otwiera stronę logowania Sonos w Twojej zwykłej przeglądarce |
| Sprawdź logowanie | gdy trwa logowanie | **jedno** sprawdzenie, czy skończyłeś w przeglądarce |
| Anuluj logowanie | gdy trwa logowanie | przerywa rozpoczętą próbę |
| Odnów dostęp | gdy konto jest połączone i ma token odnawiania | odnawia dostęp |
| Ponów zapis logowania | gdy konto działa, ale nie zostało zapisane | ponawia **sam zapis**, bez łączenia się z siecią |
| Wyloguj z Sonos | gdy jest co wylogować | usuwa zapisane logowanie (wymaga potwierdzenia) |
| Zamknij | zawsze | zamyka okno; działa też Escape |

**Sprawdzanie logowania nie chodzi samo w tle.** Nie ma odpytywania w pętli ani
timera — sprawdzasz wtedy, kiedy sam wybierzesz „Sprawdź logowanie”.

**Wylogowanie nie jest domyślną akcją Enter** i wymaga potwierdzenia, bo jest
nieodwracalne.

## Co usłyszysz w trudnych sytuacjach

Komunikaty są dobrane tak, by nie kłamały o stanie konta:

- **Logowanie jeszcze trwa** → „Dokończ je w przeglądarce i sprawdź ponownie”.
  Przyciski **Sprawdź** i **Anuluj** zostają czynne.
- **Konto działa, ale nie udało się go zapisać** → usłyszysz dokładnie to,
  a nie „Wylogowano”. Pojawia się „Ponów zapis logowania”. Twoje logowanie żyje
  w pamięci programu; po ponownym zapisie przetrwa restart.
- **Nie udało się usunąć zapisu przy wylogowaniu** → „Wylogowanie nie zostało
  dokończone: zapisane logowanie mogło pozostać”. Nie udajemy sukcesu.
- **Błąd przejściowy** (brak sieci, przeciążony serwer) → opis przyczyny;
  konto **nie** jest kasowane i nie jest pokazane jako wylogowane.
- **Niezgodny dowód logowania** → usłyszysz przyczynę i propozycję rozpoczęcia
  logowania od nowa, a nie samo „czekaj”.

Żaden komunikat nie zawiera tokenu, adresu ani treści błędu technicznego.

## Czego okno NIE robi

- Nie zamyka i nie rozłącza wspólnego konta przy zamknięciu okna — po zamknięciu
  konto działa dalej w programie. Zamknięcie anuluje tylko **własną** próbę
  logowania tego okna.
- Nie zabiera fokusu przeglądarce, gdy wynik przyjdzie w trakcie Twojego
  logowania na stronie Sonos.
- Nie blokuje interfejsu czekaniem — Zamknij i Anuluj działają także wtedy, gdy
  operacja jest w toku.

## Co zostało zmierzone bez pokazywania okna

94 sprawdzenia, wszystkie zaliczone, na prawdziwym Windows (kod wyjścia 0):

- treść jako pole tylko do odczytu, fokusowalne, z fokusem startowym i najniższym
  numerem tabulacji; przyciski po niej,
- **zero pól do wpisywania** (jedyne pole tekstowe to instrukcja),
- nazwy przycisków bez skrótów i podkreślników,
- widoczność przycisków dla stanu: brak konta / konto połączone / niezapisane,
- prawdziwe ścieżki: start logowania, jedno sprawdzenie, Pending, wyjątek startu,
  spóźniona odpowiedź po zamknięciu okna,
- ponowienie zapisu **bez ani jednego zapytania HTTP**,
- wylogowanie udane i nieudane usunięcie,
- zamknięcie okna nie zwalnia wspólnego koordynatora i nie kasuje konta.

Pomiar sprawdzono też **od strony błędu**: po celowym zepsuciu okna (treść jako
edytowalne pole na końcu tabulacji, skrót dopisany do nazwy) te same testy dały
10 niezaliczonych i kod wyjścia 1. Bez tego nie byłoby wiadomo, czy w ogóle
odróżniają dobry układ od złego.

**Czego ten pomiar NIE dowodzi:** nie jest testem żywego NVDA. Okno nie było
pokazywane, więc pierwszej wypowiedzi czytnika i rzeczywistej kolejności mowy
nikt jeszcze nie słyszał. To osobny krok.

## Jak uruchomić

Pomiar bez GUI (nic nie pojawi się na ekranie, czytnik milczy):

```bash
tests/SonosAccountWindowHarness/run.sh
```

Okno próbne do testu żywym NVDA — **zabiera fokus i obudzi czytnik**, więc
uruchamiaj je świadomie:

```bash
tests/SonosAccountWindowHarness/run.sh --show-fixture --seconds 240
tests/SonosAccountWindowHarness/run.sh --show-fixture --with-account
```

Okno próbne ma jednoznaczny tytuł **„AMC PROBA A11Y - Konto Sonos (dane
probne)”**, widać je na pasku zadań, działa na danych próbnych i nie łączy się z
niczym: brak sieci, brak prawdziwego magazynu haseł, brak audio, brak
aktualizacji. „Zaloguj” tylko zapisuje adres do pliku, zamiast otwierać
przeglądarkę. Po ograniczonym czasie okno zamyka się samo — i zamyka wyłącznie
siebie.

Każde uruchomienie zakłada **nowy** katalog z kwitami (PID, HWND, liczniki
operacji, lista komunikatów) i nie usuwa poprzednich:
`/home/michal/projekty/amc_pomoc/sonos-account-window1/okno-<data>-pid<PID>/`.

## Czego jeszcze nie ma

Okno **nie jest jeszcze podłączone** do menu głównego, skrótów globalnych ani
ustawień produkcyjnych. To następny, osobny krok — po odbiorze samego okna.
