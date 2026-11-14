# AMC Test i Public alpha.507 — szkic instrukcji i listy zmian

Szkic z 14.11.2026 dla wersji `0.1.0-alpha.507`. Test i Public powstały z
commitu `aafa2bc3`, zostały opublikowane i zainstalowane; Test działa z
kanonicznego katalogu D. Tekst pozostaje projektem instrukcji do korekty
użytkownika; nie został opublikowany jako oficjalny podręcznik.

## Co się zmienia

- W AMC Public dostępna jest lokalna sesja Sonos. W Menedżerze sesji można ją włączyć i wybrać głośnik w sieci lokalnej. Public nie wymaga konta deweloperskiego Sonos do lokalnego sterowania.
- W AMC Test i Public sprawdzono dodawanie kilku zaznaczonych pozycji do playlist w plikach, radiu, podcastach, TIDAL i Spotify. TIDAL podaje liczbę pozycji potwierdzonych przez usługę oraz zgłasza pominięcia.
- Playlista stacji radiowych nie podaje łącznego czasu trwania; stację nadal można uruchomić Enterem.
- Wspólne źródła zawierają poprawki kolejek z alpha.504 oraz nowsze zmiany odświeżania podcastów i YouTube. Automatyczne odświeżanie podcastów można wyłączyć lub zaplanować; ręczne odświeżanie pozostaje dostępne.
- AMC Test zachowuje odczyt Apple Music, ale wycofano niepotwierdzone odtwarzanie w osadzonym WebView. Apple Music nie przechodzi do Public w tym wydaniu.
- Pozycje usuwania z kolejki TIDAL i Radia Archiwisty w menu Biblioteka mają klawisz dostępu.

## Aktualizacja i odbiór

Test i Public mają instalatory z potwierdzoną integralnością i przeszły
kontrolowany start. Starszy proces Test z C: został zamknięty po potwierdzeniu
końca nagrywania; skróty i działający proces prowadzą do
`D:\Projekty Codex\AMC Python\AMC.exe`. Public jest zainstalowany w
`D:\Projekty Codex\AMC Public`. Po starcie Test pokazuje sesję Radio i
„Nic nie jest odtwarzane”; dźwięk nie został automatycznie wznowiony, bo
przed zamknięciem starsza aplikacja pokazywała inną stację niż wcześniej
wskazany Poznań. Nadal trzeba sprawdzić odczyt NVDA, działania na żywych
kontach i wydajność dużej Biblioteki podcastów.
