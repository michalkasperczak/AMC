# AMC Test i Public alpha.534 — szkic instrukcji i listy zmian / draft guide and changelog

Szkic z 28.11.2026 do korekty i zatwierdzenia przez użytkownika. Nie jest automatycznie publikowaną instrukcją. Status kompilacji GitHub, wydania, instalacji i odbioru NVDA znajduje się w osobnym rekordzie wydania.

## Instrukcja po polsku

- Ustaw domyślne komunikaty w Ustawieniach programu → Dostępność i komunikaty. W wybranej sesji naciśnij Ctrl+Alt+Enter. Dostępne tam komunikaty odtwarzania, pauzy, przewijania, głośności i zakładek mogą dziedziczyć ustawienie ogólne lub mieć własną wartość. Widoczne są tylko opcje obsługiwane przez tę sesję.
- W Podcastach i YouTube zaznacz podcast, kanał, playlistę, odcinek albo film. Ctrl+Alt+Shift+cyfra przypisuje preset, Ctrl+Shift+cyfra go uruchamia. Podcast, kanał i playlista otwierają listę; odcinek i film rozpoczynają odtwarzanie w AMC. Wyboru miejsca można dokonać także przez Ctrl+Alt+Shift+P.
- Po zmianie Ulubionych w Podcastach i YouTube naciśnij Ctrl+Z, aby cofnąć ostatnią zmianę. Usunięty element wraca na poprzednie miejsce. Polecenie jest dostępne tylko dopóki nie nastąpi kolejna zmiana Ulubionych; edycja tego samego elementu poza AMC także może uniemożliwić bezpieczne cofnięcie.
- W Plikach, Podcastach i Radiu Archiwiście Shift+Enter dopisuje zaznaczony materiał do odtworzenia jako następny. Polecenie „Dodaj lub usuń z następnych” jest w menu bez skrótu, a Ctrl+Shift+Q zmienia zwykłą kolejkę.
- W TIDAL NVDA+End czyta znany format i bitrate bieżącego utworu po potwierdzeniu startu. Spacja odtwarza lub wstrzymuje i wypowiada tylko stan transportu według ustawień komunikatów.
- Ctrl+R w Radiu potwierdza „Nagrywanie rozpoczęte” dopiero po otwarciu zapisu. Po Ctrl+Shift+L w bibliotece Podcastów i Ctrl+D podczas pobierania odcinka lista zachowuje aktualnie wybrany element, nawet gdy odpowiedź przychodzi później.

## English guide draft

- Set default speech in Settings → Accessibility and messages. Press Ctrl+Alt+Enter in a session to inherit each supported playback message from the global setting or override it for that session. Only controls with a working action appear.
- In Podcasts and YouTube, save a selected podcast, channel, playlist, episode, or video with Ctrl+Alt+Shift+digit. Open a slot with Ctrl+Shift+digit. Containers open their lists; an episode or video starts playback in AMC. Ctrl+Alt+Shift+P opens the slot chooser.
- Ctrl+Z undoes the last Favorites change in Podcasts and YouTube, restoring a removed item to its previous position when the affected record has not changed again.
- Shift+Enter places a selected playable item next in Files, Podcasts, and Radio Archiwista. The “Add or remove from Play Next” menu command has no shortcut; Ctrl+Shift+Q toggles the ordinary queue.
- NVDA+End in TIDAL reads the known audio format and bitrate after playback is confirmed. Space announces only the transport state under the selected message policy.
- Ctrl+R in Radio confirms recording only after the file opens. Podcast library and download refreshes preserve the user's current selection.

## Różnice i granice / Profiles and limits

AMC Public ma wspólne poprawki. Apple Music i HEOS są wyłącznie w AMC Test. Rzeczywisty odczyt NVDA, działanie TIDAL i urządzeń oraz odsłuch na żywo wymagają osobnego odbioru po instalacji. Diagnostyka AirPlay pozostaje odłożona do próby za dnia z użytkownikiem.