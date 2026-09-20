# AMC 0.1.0-alpha.398 — konto Spotify

## Bezpośrednie logowanie biblioteki

W sesji Spotify naciśnij Ctrl+F5. Pierwsze okno to „Konto Spotify”, a przy zapisanym identyfikatorze aplikacji fokus trafia na „Zaloguj w przeglądarce”. Nie trzeba przechodzić przez okno parowania.

Jeśli logowanie działa i dodawanie do Biblioteki jest już możliwe, nie loguj się ponownie tylko na potrzeby tego sprawdzenia. Escape ma wrócić do listy lub odtwarzacza, z którego otwarto konto.

## Osobne parowanie odtwarzacza

Przy odtwarzaczu Librespot za przyciskiem logowania jest „Parowanie odtwarzacza…”. Otwórz je bez rozpoczynania nowego parowania. Escape albo „Konto katalogu i biblioteka…” wraca do tego samego okna konta. Zamknięcie konta wraca do AMC.

Przy oficjalnym odtwarzaczu SDK przycisk parowania Librespot nie jest pokazywany.

## Zgoda na zapis

Ponowne parowanie odtwarzacza nie rozszerza zgody na dodawanie i usuwanie pozycji biblioteki. Gdy brakuje tej zgody, komunikat wskazuje bezpośrednio Ctrl+F5 i „Zaloguj w przeglądarce”. Nie usuwa zapisanego konta i nie otwiera przeglądarki sam.

Logowanie oraz pobieranie biblioteki nadal są osobnymi czynnościami: po udanym logowaniu program automatycznie odświeża bibliotekę. Nie zmieniono tego mechanizmu ani zakresów wymaganych przez API.

## Zakres wydania

Zmiana dotyczy układu konta i instrukcji. Nie zmienia silników dźwięku, zapisanych poświadczeń, kolejki, TimeShift, dekoderów YouTube ani skrótów presetów. Uporządkowanie list podcastów Spotify i dalsze pomiary wydajności pozostają osobnymi zadaniami.
