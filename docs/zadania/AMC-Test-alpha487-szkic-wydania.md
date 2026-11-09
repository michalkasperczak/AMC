# AMC Test — szkic następnej aktualizacji

Stan na 8 listopada 2026, wieczorem. Numer `0.1.0-alpha.487` jest propozycją po opublikowanym `alpha.486`; przed budową trzeba sprawdzić, czy nie powstało nowsze wydanie. To szkic do korekty użytkownika, bez publikacji.

## Instrukcja użytkownika po instalacji

- **Zgłoszenia:** wybierz w menu Pomoc „Zgłoś błąd lub sugestię…” albo znajdź to polecenie w palecie `Ctrl+Shift+K`. Sprawdź pełny podgląd zredagowanego raportu. Log jest domyślnie wyłączony. W AMC Test z aktywnym adresem możesz wysłać raport albo skopiować i zapisać go lokalnie. Po sukcesie formularz pokazuje numer publicznego zgłoszenia.
- **Sonos:** przejdź do sesji `Ctrl+6`. AMC Test szuka grup w tej samej sieci lokalnej; `Ctrl+F5` ponawia wyszukiwanie. Wybierz grupę strzałkami i Enterem. `Ctrl+L` otwiera bibliotekę, `Ctrl+U` ulubione, `Ctrl+P` playlisty. Konto Sonos i logowanie w przeglądarce nie są potrzebne do lokalnego sterowania. Odbiór z prawdziwym głośnikiem i NVDA nastąpi po instalacji.
- **Spotify:** aktualizacja usuwa stary host LibreSpot z katalogu programu. Bieżący tor Spotify w AMC Test korzysta z Chrome. Ustawienia konta i dane użytkownika pozostają w profilu.

## Szkic listy zmian

- Dodano internetowe wysyłanie zredagowanych raportów z AMC Test przez testowego Workera. Prywatny pełny zapis i krótki publiczny wpis trafiają do oddzielnych repozytoriów GitHub. Jeden zatwierdzony raport próbny został już sprawdzony po obu stronach.
- Dodano lokalne wyszukiwanie i sterowanie grupami Sonos w AMC Test, bez konta i brokera. Zależności SoCo są przypięte i pakowane tylko w profil Test.
- Wycofano nieużywany host LibreSpot z nowej paczki i przewidziano usunięcie jego dawnych plików przez instalator. Spotify nadal korzysta z dotychczasowego toru Chrome.
- Zachowano poprawki playlist, TIDAL i presetów z poprzedniego wspólnego źródła `alpha.486`.

## Dowody i otwarte kroki

- Testowy Worker zwrócił HTTP 201; odczyt GitHuba potwierdził prywatne `amc-support-intake#1` i publiczne `AMC#12` dla jednego identyfikatora. Nie dołączono logu.
- Czysta gałąź źródeł: `codex/amc-test-sygnalista-sonos-20261108`, commit `066a61b33592de86fb1d9647cf0779e72826e6ab`. Testy: 94/94 pakowania, profili, Sonosa i Sygnalisty oraz 45/45 interfejsu Sonosa. Składnia skryptu budowy: poprawna.
- **Nie wykonano:** kompletnego builda, instalatora, GitHub Release, instalacji ani ręcznego odbioru NVDA. Użytkownik nagrywa dziś wieczorem i polecił wrócić do instalacji jutro. Bieżący AMC Test ma otwarte okno wyboru źródeł podcastów z niezapisanymi zaznaczeniami; zostawić je użytkownikowi.
- Przed budową sprawdzić źródła przypiętego runtime TIDAL i SoCo oraz aktualny numer wydania. Budować w czystym checkoutcie, z limitem obciążenia odpowiednim dla pracy komputera. Zweryfikować manifest, brak sekretu i LibreSpot, profil Test, kompletność plików oraz kontrolowany start.
- Przed instalacją uzyskać aktualny stan nagrywania i harmonogramu, poczekać na zakończenie pracy w otwartym oknie, wykonać bezpieczną kopię profilu, łagodnie zamknąć wyłącznie AMC Test i sprawdzić instalację. Nie ruszać uruchomionego AMC Public.
- Po instalacji sprawdzić z NVDA: polecenie w menu i palecie, otwarcie formularza, początkowy fokus, etykiety, podgląd, wybór logu, zapis/kopiowanie, przycisk wysyłania, powrót fokusu po Anuluj i ponownym otwarciu. Nie wysyłać kolejnego prawdziwego zgłoszenia bez świadomej decyzji użytkownika.
- Sonos w wydaniu wstępnym pozostaje bez próby na głośniku; użytkownik planuje ją za kilka dni. W opisie GitHub Release podać to ograniczenie. AMC Public `alpha.486` jest już wydany i zainstalowany, więc ta paczka go nie zastępuje.

## Lokalizacja pracy

Kanoniczne repozytorium: `D:\Projekty Codex\AMC - integracja wxPython`. Czysty checkout wydania: `C:\Users\micha\.codex\worktrees\amc-test-sygnalista-sonos\AMC - integracja wxPython`. Zapisy wyników: `docs/zadania/REL-001-github-aktualizacje-sygnalista.md`, `AMC10-logowanie-sonos-i-publiczny-broker.md` i `SPOTIFY-LIBRESPOT-20261108-wycofanie-z-pakietu.md` w kanonicznym repozytorium.
