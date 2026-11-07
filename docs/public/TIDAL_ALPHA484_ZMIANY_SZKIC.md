# AMC Public alpha.484 — szkic zmian

Korekta po zgłoszeniu użytkownika z 7 listopada 2026. Alpha.483 nie otwierała okna Ctrl+Shift+P w profilu Public i pozostawiała komunikat „Zmiana playlist TIDAL już trwa”.

- Wspólne okno wyboru playlist jest dołączane do Public niezależnie od wyłączonej sesji Spotify.
- Nieudane otwarcie okna zwalnia blokadę, przywraca fokus i podaje zrozumiały komunikat. Można spróbować ponownie bez restartu.
- Test nowej poprawki uruchamia rzeczywiście odfiltrowany profil Public w nowym procesie. Sprawdza również brak prywatnych modułów Spotify.
- Prawdziwy konstruktor wx sprawdzono na takim pakiecie z fikcyjnymi playlistami, bez konta i bez zapisu do usługi.
- Nazwa nowej playlisty TIDAL również zaczyna wiersz, aby zachować przechodzenie literą.

Zachowano poprawki alpha.483: przeglądarkę z kodem, Enter i Spację w oknie playlist, pamięć celu, powrót do odtwarzacza i polski język systemowy. Odbiór właściwego Ctrl+Shift+P i NVDA wymaga sprawdzenia po aktualizacji. Dokument jest szkicem, a paczka pozostaje kandydatem lokalnym bez publikacji.
