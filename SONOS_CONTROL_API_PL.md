# Sonos Control API — podstawa dalszej integracji AMC

Weryfikacja dokumentacji: 28 września 2026 r. Są to możliwości opisane przez Sonosa, **nie wynik próby na koncie ani funkcje już wydane w AMC**. Bieżący stan produktu: [PROJEKT_SONOS_PL.md](PROJEKT_SONOS_PL.md).

Źródłem metod i pól są definicje OpenAPI z oficjalnych stron podanych poniżej. Baza adresów: `https://api.ws.sonos.com/control/api/v1`.

## Odtwarzanie w istniejącej grupie

Sterowanie już załadowanym materiałem nie wymaga tworzenia własnej kolejki ani własnej `playbackSession`.

- [Odczyt stanu](https://docs.sonos.com/reference/playback-getplaybackstatus-groupid): `GET /groups/{groupId}/playback`.
- [Odtwarzaj](https://docs.sonos.com/reference/playback-play-groupid): `POST /groups/{groupId}/playback/play`.
- [Wstrzymaj](https://docs.sonos.com/reference/playback-pause-groupid): `POST /groups/{groupId}/playback/pause`.
- [Przełącz odtwarzanie/wstrzymanie](https://docs.sonos.com/reference/playback-toggleplaypause-groupid): `POST /groups/{groupId}/playback/togglePlayPause`.
- [Następny](https://docs.sonos.com/reference/playback-skiptonexttrack-groupid): `POST /groups/{groupId}/playback/skipToNextTrack`.
- [Poprzedni](https://docs.sonos.com/reference/playback-skiptoprevioustrack-groupid): `POST /groups/{groupId}/playback/skipToPreviousTrack`.
- [Przejdź do pozycji](https://docs.sonos.com/reference/playback-seek-groupid): `POST /groups/{groupId}/playback/seek`, wymagane `positionMillis`.
- [Przesuń pozycję](https://docs.sonos.com/reference/playback-seekrelative-groupid): `POST /groups/{groupId}/playback/seekRelative`, wymagane `deltaMillis`.

Obie operacje przewijania dotyczą grupy. Opcjonalne `itemId` pozwala odrzucić polecenie, jeżeli utwór zdążył się zmienić. Pozycja poza końcem utworu może spowodować przejście do następnego — to nie jest obietnica zatrzymania na ostatniej sekundzie.

Zestaw możliwych operacji zależy od aktualnego materiału. [Stan odtwarzania](https://docs.sonos.com/reference/playback-playbackstatus) zawiera `availablePlaybackActions`, m.in. `canPlay`, `canPause`, `canStop`, `canSkip`, `canSeek` i `canSkipToPrevious`.

Starsze `canSkipBack` oznaczono w schemacie jako wycofywane na rzecz `canSkipToPrevious`. To nie jest ta sama semantyka co polecenie `skipBack`, które może cofać do początku utworu. Wdrażanie skrótów wymaga dopasowania konkretnej akcji, nie tylko podobnej nazwy.

Dla radia polecenie wstrzymania może dać `PLAYBACK_STATE_IDLE` zamiast `PLAYBACK_STATE_PAUSED`. Brak pozycji lub długości materiału nie oznacza wartości zero.

## Informacje o aktualnym materiale

[Odczyt metadanych](https://docs.sonos.com/reference/playbackmetadata-getmetadatastatus-groupid): `GET /groups/{groupId}/playbackMetadata`.

- `container` opisuje źródło, np. stację lub playlistę; może go nie być, gdy nic nie załadowano.
- `currentItem.track.name` to tytuł; `artist` i `album` są obiektami, nie zwykłymi napisami.
- `currentItem.track.durationMillis` dostarcza długość, jeżeli jest dostępna.
- `nextItem` występuje tylko wtedy, gdy znany jest następny element.
- `streamInfo` może zawierać opis radia bez strukturalnych danych utworu.

Sonos nie emituje zdarzenia dla każdej kolejnej sekundy normalnego odtwarzania. Dokumentacja zaleca lokalny licznik postępu oparty na ostatniej otrzymanej pozycji. Taki licznik jest przewidywaniem pozycji między odczytami, nie nowym pomiarem z urządzenia; przy nieaktualnym lub nieznanym stanie nie wolno przedstawiać go jako potwierdzonego czasu.

## Głośność grupy

- [Odczyt](https://docs.sonos.com/reference/groupvolume-getvolume-groupid): `GET /groups/{groupId}/groupVolume`.
- [Ustawienie](https://docs.sonos.com/reference/groupvolume-setvolume-groupid): `POST /groups/{groupId}/groupVolume`, wymagane `volume` w zakresie 0–100.
- [Wyciszenie](https://docs.sonos.com/reference/groupvolume-setmute-groupid): `POST /groups/{groupId}/groupVolume/mute`, wymagane `muted`.
- [Zmiana względna](https://docs.sonos.com/reference/groupvolume-setrelativevolume-groupid): `POST /groups/{groupId}/groupVolume/relative`, wymagane `volumeDelta` w zakresie od −100 do 100.

Przy `fixed: true` regulacja poziomu ma być niedostępna. Wyciszenie jest osobnym stanem, niezależnym od wartości głośności.

[Opis grupowej głośności](https://docs.sonos.com/reference/groupvolume-object) uprzedza, że jedno polecenie może wywołać kilka zdarzeń, zanim głośności poszczególnych urządzeń się ustabilizują. Pierwsza odpowiedź po poleceniu nie musi jeszcze przedstawiać wyniku końcowego.

## Potwierdzanie skutku i błędy

Definicje wymienionych operacji dokumentują sukces HTTP `200`, nie `204`. Samo potwierdzenie polecenia nie zastępuje odczytu nowego stanu ani sprawdzenia oczekiwanego skutku.

Dla AMC oznacza to osobne wyniki: polecenie przyjęte, skutek potwierdzony albo niepotwierdzony. Polecenia zmieniającego stan nie należy automatycznie ponawiać po niejednoznacznym błędzie sieciowym — w szczególności przełączenie, następny utwór i względna zmiana głośności mogły już zostać wykonane.

[Dokumentacja Control](https://docs.sonos.com/docs/control) wymienia ograniczanie liczby żądań (`429`). Ten odczyt dokumentacji nie ustalił liczbowej gwarancji dopuszczalnej częstotliwości. Nie należy przenosić bez pomiaru rytmu odpytywania lokalnego WiiM do chmurowego Sonosa.

## Zdarzenia wymagają osobnej usługi

[Subskrypcje Sonosa](https://docs.sonos.com/docs/subscribe) wymagają adresu odbioru zdarzeń HTTPS zarejestrowanego dla klucza integracji. Bez niego próba subskrypcji daje `403`.

Adres odbioru zdarzeń jest **inny niż adres powrotu z logowania**. Działające logowanie AMC nie dowodzi więc gotowości subskrypcji. Potrzebna byłaby obsługa serwerowa, weryfikacja podpisów i przekazywanie zdarzeń do właściwego klienta.

Do czasu wdrożenia takiej usługi możliwy jest odczyt na wejściu do widoku, po poleceniu i oszczędne odświeżanie w tle. Konkretny rytm oraz reakcja na ograniczenie żądań wymagają implementacji i testów; nie jest to potwierdzony limit Sonosa ani decyzja o usunięciu automatycznej aktualizacji.

## Dalsze możliwości i granice ustaleń

- [Ulubione](https://docs.sonos.com/reference/favorites-getfavorites-householdid): istnieje `GET /households/{householdId}/favorites`. Nie jest to jeszcze zaimplementowana obsługa presetów AMC.
- [Wejście liniowe](https://docs.sonos.com/reference/playback-loadlinein-groupid): istnieje `POST /groups/{groupId}/playback/lineIn`. Dostępność zależy od wyposażenia urządzenia; nie wolno ogólnie twierdzić, że Sonos nie ma obsługi wejść.
- Dostępności pełnej regulacji basów, sopranów i loudness w tym API **nie ustalono**. Opcje kina domowego nie są dowodem istnienia pełnego korektora.

## Zakres kolejnego działającego etapu

Po odbiorze listy urządzeń: aktywna grupa w sesji Sonos, widok odtwarzacza z faktycznie odczytanym stanem i opisem materiału, następnie sterowanie oraz głośność z bramkami możliwości. Nawigacja i skróty mają odpowiadać WiiM, a wyjście z widoku nie może samo zatrzymywać urządzenia.

Odbiór wymaga oddzielnie: testów protokołu i ochrony konta, klawiatury oraz żywego NVDA, a następnie rzeczywistej odpowiedzi i skutku na urządzeniu. Dokumentacja dostawcy rozstrzyga możliwość techniczną, ale nie zalicza żadnego z tych testów.
