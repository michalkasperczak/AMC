# TIDAL w AMC — instrukcja pierwszego połączenia i testu

Instrukcja dotyczy wersji `0.1.0-alpha.337`. Integracja służy do bezpiecznego
logowania, odczytu i zmiany kolekcji, otwierania albumów, playlist oraz
wykonawców i wyszukiwania na prawdziwym koncie. Oficjalny Player SDK jest
już dołączony. Potwierdzono odtwarzanie próbek, nie pełnych utworów.
Ponowne logowanie do kolekcji nie jest rozwiązaniem ograniczenia do próbek.

Jeżeli konto jest już skonfigurowane, pomiń pierwszą konfigurację poniżej.
Nowy raport: Ctrl+F5 → Diagnostyka odtwarzania (Alt+D w oknie konta).
Krótki test bez ponownego logowania: `INSTRUKCJA_0.1.0-alpha.337_PL.md`.

## Co będzie potrzebne

- zwykłe konto TIDAL;
- dostęp do Internetu;
- zwykła przeglądarka internetowa;
- AMC `alpha.337`;
- jednorazowo utworzona aplikacja testowa w panelu TIDAL Developer.

Portal deweloperski i ekran zgody użytkownika pełnią dwie różne funkcje:

1. panel TIDAL Developer nadaje aplikacji AMC testowy **Client ID**;
2. późniejsze logowanie w przeglądarce pozwala użytkownikowi udzielić tej
   aplikacji ograniczonego dostępu do własnego konta.

W publicznym wydaniu AMC użytkownik nie powinien tworzyć własnej aplikacji
deweloperskiej. Obecna procedura jest potrzebna tylko w fazie rozwoju i testów.

## 1. Przygotowanie aplikacji w TIDAL Developer

1. W zwykłej przeglądarce otwórz:
   <https://developer.tidal.com/>
2. Wybierz logowanie i zaloguj się zwykłym kontem TIDAL.
3. Przy pierwszym wejściu zaakceptuj wytyczne dla programistów.
4. Otwórz **Dashboard** i wybierz utworzenie nowej aplikacji.
5. Nadaj jej jednoznaczną nazwę, na przykład `AMC Development`.
6. Jeżeli formularz wymaga opisu, opisz zgodnie z prawdą, że jest to prywatna,
   rozwijana aplikacja desktopowa do dostępnej obsługi własnego konta TIDAL.
   Jeżeli portal zażąda adresu witryny, polityki prywatności albo pola, którego
   znaczenie nie jest jasne, nie wpisuj przypadkowych danych. Zapisz nazwę pola
   i wróć z nią do rozmowy — dobierzemy prawidłową wartość.
7. Utwórz aplikację, a następnie otwórz jej ustawienia.
8. W sekcji adresów powrotu, przekierowań lub `Redirect URI` dodaj **dokładnie**:

   `http://127.0.0.1:43821/tidal/callback/`

   Ważne są protokół `http`, adres `127.0.0.1`, port `43821`, ścieżka oraz
   końcowy ukośnik.
9. Włącz tylko potrzebne uprawnienia:

   - `user.read`;
   - `collection.read`;
   - `collection.write`;
   - `playlists.read`;
   - `playlists.write`.

10. Zapisz ustawienia aplikacji.
11. Odszukaj **Client ID** i skopiuj go do schowka.

Panel pokaże także **Client Secret**. Nie wprowadzaj go do AMC, nie zapisuj w
repozytorium ani w pliku z testami i nie przesyłaj w rozmowie. AMC stosuje
logowanie OAuth 2.1 Authorization Code z PKCE i potrzebuje tylko Client ID.

## 2. Konfiguracja AMC

1. Zamknij wcześniejszą wersję AMC, aby nie pomylić okien i lokalnego portu
   logowania.
2. Uruchom:

   `D:\Projekty Codex\Accessible Multimedia Controller\publish\AccessibleMediaController-0.1.0-alpha.335\AccessibleMediaController-0.1.0-alpha.335.exe`

3. Przejdź do sesji TIDAL. Przy domyślnej kolejności jest to `Ctrl+3`. Jeżeli
   kolejność sesji została zmieniona, użyj `Ctrl+Shift+S` i wybierz TIDAL z
   listy.
4. Naciśnij `Ctrl+F5`. Otworzy się okno **Konto i synchronizacja TIDAL**.
5. W polu **Identyfikator aplikacji TIDAL** wklej Client ID.
6. Sprawdź pole **Adres powrotu logowania TIDAL**. Powinno zawierać:

   `http://127.0.0.1:43821/tidal/callback/`

7. W polu **Dwuliterowy kod kraju katalogu TIDAL** pozostaw `PL`.
8. Możesz wybrać **Zapisz ustawienia**. Przycisk logowania również sprawdza i
   zapisuje te trzy wartości, więc osobne zapisanie nie jest konieczne.
9. Wybierz **Zaloguj w przeglądarce**.

## 3. Udzielenie dostępu w przeglądarce

1. AMC otworzy oficjalną stronę logowania TIDAL w domyślnej przeglądarce.
2. Sprawdź adres strony przed wpisaniem hasła. Logowanie powinno odbywać się w
   domenie TIDAL, a nie w oknie AMC.
3. Zaloguj się i zaakceptuj wskazany, ograniczony zakres dostępu.
4. Po zatwierdzeniu przeglądarka przekieruje na lokalny adres `127.0.0.1`.
   Lokalny odbiornik działa tylko podczas tej operacji. Nie wysyła hasła ani
   tokenu do obcego serwera.
5. Jeżeli fokus sam nie wróci do programu, użyj `Alt+Tab`, aby przejść do AMC.
6. AMC powinien powiedzieć, że zalogowano konto, a następnie rozpoczęto
   synchronizację.

Nie uruchamiaj równocześnie drugiej kopii AMC podczas logowania. Mogłaby zająć
ten sam port lokalny `43821`.

## 4. Oczekiwany wynik synchronizacji

Po powodzeniu AMC powinien podać liczbę zsynchronizowanych elementów oraz,
jeżeli API ją udostępni, nazwę konta. Osobno pobierane są:

- polubione utwory;
- polubione albumy;
- polubieni wykonawcy;
- playlisty zapisane w kolekcji użytkownika;
- polubione materiały wideo.

Kolejka, historia, zakładki, presety, własne nazwy i ustawienia odtwarzania
pozostają lokalnymi danymi AMC. Nie są wysyłane do TIDAL.

## 5. Krótki test po połączeniu

1. Zamknij okno konta i pozostań w sesji TIDAL.
2. Otwórz Bibliotekę przez `Ctrl+L` i sprawdź nawigację strzałkami oraz
   literami. Elementy demonstracyjne powinny zostać zastąpione prawdziwymi
   danymi konta.
3. Otwórz Albumy przez `Ctrl+Shift+A` i porównaj kilka pozycji z aplikacją
   TIDAL.
4. Otwórz Ulubione przez `Ctrl+U` i sprawdź zapisane utwory. Porównaj zakres
   listy z Biblioteką, która obejmuje także pozostałe rodzaje kolekcji.
5. Naciśnij `Ctrl+F`, wpisz znanego wykonawcę albo album spoza własnej
   kolekcji i wykonaj wyszukiwanie.
6. Sprawdź także wynik, który już znajduje się w kolekcji. AMC powinien
   rozpoznać jego stan.
7. Naciśnij Enter na albumie, playliście i wykonawcy. Powinny otworzyć się
   odpowiednio utwory albumu, pozycje playlisty i albumy wykonawcy. Escape
   powinien wrócić do poprzedniej listy i elementu.
8. Naciśnij Enter na znalezionym utworze. Jeżeli TIDAL udostępni próbkę,
   AMC powinien odczytać jej powód i rzeczywisty czas, około 30 sekund.
   Po końcu próbki nie powinien usuwać pełnego utworu z Kolejki ani przeskakiwać
   po kolejnych utworach. Logowanie ponawiaj tylko po błędzie autoryzacji,
   nie z powodu samej próbki. Pełne odtwarzanie pozostaje niezweryfikowane.
9. Poleceniem `Ctrl+Shift+U` albo `Ctrl+Shift+L` dodaj element spoza kolekcji,
   porównaj go z aplikacją TIDAL, a następnie usuń i porównaj ponownie. W sesji
   TIDAL oba skróty zmieniają tę samą zdalną kolekcję.
10. Ponownie otwórz `Ctrl+F5` i wybierz **Synchronizuj teraz**. Nie powinno być
   konieczne ponowne wpisywanie hasła.
11. Zamknij AMC, uruchom je ponownie i sprawdź automatyczne odświeżenie sesji
    TIDAL.
12. Otwórz playlisty przez `Ctrl+P`. W otwartej playliście wybierz `Alt+3`,
    przenieś element przez `Alt+strzałkę`, a następnie sprawdź kolejność w
    oficjalnej aplikacji TIDAL. `Ctrl+X` i `Ctrl+V` powinny przenieść zaznaczony
    blok przed wybrany element. Album nie pozwala włączyć `Alt+3`.

## 6. Test dostępności menu

Sprawdź liczenie widocznych pozycji dynamicznego menu również w bieżącej wersji.

1. W sesji TIDAL otwórz menu **Plik**.
2. Przejdź po wszystkich pozycjach strzałkami.
3. NVDA powinien liczyć wyłącznie widoczne polecenia. Nie powinien mówić na
   przykład „1 z 19”, jeżeli w tej sesji dostępnych jest tylko kilka pozycji.
4. Powtórz sprawdzenie w Radiu, Podcastach i YouTube, WiiM oraz Plikach
   lokalnych.

## 7. Najczęstsze problemy

### Nieprawidłowy adres powrotu

Porównaj oba adresy znak po znaku. Najczęstszą przyczyną jest brak końcowego
ukośnika, inny port, `localhost` zamiast `127.0.0.1` albo `https` zamiast
`http`.

### TIDAL nie udzielił wymaganych uprawnień

Sprawdź w panelu aplikacji zakresy `user.read`, `collection.read`,
`collection.write`, `playlists.read` i `playlists.write`, a następnie zapisz ustawienia. Token ze
starszej wersji nie otrzyma nowego zakresu samoczynnie: wybierz w AMC
**Zaloguj w przeglądarce** i ponownie zaakceptuj dostęp.

### Port lokalny jest zajęty

Po zakończeniu nagrywania zamknij starsze kopie AMC i uruchom jedną bieżącą wersję. Nie zmieniaj portu w
jednym miejscu bez identycznej zmiany adresu w panelu TIDAL.

### Synchronizacja jest częściowa

AMC zachowuje poprzedni poprawny stan kategorii, której nie udało się pobrać.
Odczekaj chwilę i użyj **Synchronizuj teraz**. API może czasowo ograniczyć
liczbę zapytań.

### Po aktualizacji pojawia się prośba o logowanie

Wersje wcześniejsze niż `alpha.330` nie wysyłały kompletnego kontekstu podczas
odświeżania tokenu. Jeżeli TIDAL odrzuci zapisane wcześniej poświadczenie,
zaloguj się jeszcze raz z poziomu `Ctrl+F5`. Od tej chwili AMC zapisuje
odświeżane tokeny w Menedżerze poświadczeń Windows. Po pierwszej udanej
synchronizacji zachowuje też bezpieczną kopię kolekcji, dlatego późniejszy błąd
sieci lub autoryzacji nie powinien już powodować pustej Biblioteki.

### Przeglądarka zakończyła logowanie, ale AMC nic nie powiedział

Wróć do AMC przez `Alt+Tab` i odczekaj kilka sekund. Jeżeli nadal nic się nie
wydarzyło, zanotuj dokładny komunikat strony oraz programu. Nie przesyłaj
Client Secret, tokenu dostępu, tokenu odświeżania ani hasła.

## 8. Odłączenie konta

Odłączenie jest opcjonalne. Jeżeli chcesz zachować połączenie do następnych
testów, nie wykonuj tej części.

1. W sesji TIDAL naciśnij `Ctrl+F5`.
2. Wybierz **Odłącz konto** i potwierdź.
3. AMC usunie tokeny TIDAL z Menedżera poświadczeń Windows. Nie należy
   zastępować lokalnych kolejek elementami demonstracyjnymi.
4. Lokalne kolejki, historia, zakładki, presety i ustawienia pozostaną.

## Oficjalne materiały

- [Panel TIDAL Developer](https://developer.tidal.com/)
- [Zarządzanie aplikacjami](https://developer.tidal.com/documentation/api-sdk/api-sdk-manage-apps)
- [Autoryzacja OAuth](https://developer.tidal.com/documentation/api-sdk/api-sdk-authorization)
- [Dokumentacja TIDAL Web API](https://tidal-music.github.io/tidal-api-reference/)
- [Dokumentacja TIDAL Player SDK dla Web](https://tidal-music.github.io/tidal-sdk-web/modules/_tidal-music_player.html)
