# ACCESS-MESSAGES — komunikaty odtwarzania w sesjach

## Korekta użytkownika, 28.11.2026

Ustawienia sesji otwiera **Ctrl+Alt+Enter**. Wcześniejsze omówienie wskazujące
Alt+Shift+Enter zastępuje ta decyzja. W Ustawieniach programu → Dostępność i
komunikaty użytkownik wybiera domyślne komunikaty odtwarzania. Każda sesja
dziedziczy je do chwili wyboru własnej wartości „Włączone” lub „Wyłączone”.
Powrót do „Jak ustawienie ogólne” usuwa własny wybór.

## Zachowanie i dostępność

W Ctrl+Alt+Enter kontrolki mają pełne polskie nazwy i jawne etykiety
„Jak ustawienie ogólne: włączone/wyłączone”. Wartości modelu i enumy nie
trafiają do nazw ani pozycji list. Potwierdzenia wyłączone z mowy pozostają
w statusie. Błędy transportu i odpowiedzi na jawne żądania użytkownika
pozostają słyszalne. Globalny wyłącznik wszystkich rutynowych komunikatów
pozostaje nadrzędny. Spacja nie dokleja parametrów technicznych materiału.

| Sesja | Własne ustawienia komunikatów | Różnica wykonawcy |
| --- | --- | --- |
| Pliki, Radio, Podcasty i YouTube, TIDAL, Spotify, Radio Archiwista | Główna bramka, strzałki/Home/End, cyfry, treść skoku cyfrą, głośność, pauza/odtwarzanie, zakładki | Wspólna polityka odtwarzacza; osobna automatyczna zapowiedź nowego utworu. |
| WiiM, HEOS, Frontier Smart | Główna bramka, głośność, pauza/odtwarzanie | Potwierdzenia po odpowiedzi urządzenia. |
| Sonos | Główna bramka, przewijanie, głośność, pauza/odtwarzanie | Potwierdzona głośność nie czyta całej migawki utworu. |
| Apple Music | Główna bramka, pauza/odtwarzanie | Zewnętrzne sterowanie oryginalną aplikacją; brak natywnego silnika materiału w tej sesji. |

Odczyt listy góra/dół i lewa strzałka oraz automatyczne zapowiedzi nowych
utworów zachowują wcześniejsze własne dziedziczenie. Zachowanie Enter i
otwarcie odtwarzacza nadal są oddzielnymi ustawieniami.

## Próby i status

- Model: zapis, odczyt, błędne dane, dziedziczenie, niezależność sesji.
- Seam GUI: konstrukcja natywnych pól wx na atrapach, etykiety, pierwszy fokus,
  wybór, ponowne otwarcie, powrót do dziedziczenia, Anuluj i przywrócenie fokusu.
- Testy źródłowe, sesyjne i skrótów: **202 przeszły, 3 wyłączone** z powodu
  istniejących atrap Sonos/Frontier niezwiązanych ze zmianą. Dodatkowy pakiet
  GUI/statusu: **101 przeszło, 7 wyłączono**, 1 istniejąca atrapa TIDAL
  wymaga `_youtube_playback_active`, którego nie dostarcza. Szersze testy
  TIDAL/Spotify mają także inne nieaktualne atrapy. Wynik końcowej próby
  integracyjnej należy potwierdzić po scaleniu.
- Kod źródłowy: zmieniony. Build GitHub: nie wykonano. Instalacja: nie
  wykonano. Release: nie opublikowano. Żywy odbiór NVDA: oczekuje.

Ograniczenie: test seam nie dowodzi głosu rzeczywistego NVDA ani zachowania
fokusu w otwartym wxWidgets. Wymaga próby na zainstalowanym AMC.

Commit kodu: `2742e0fe2872601a528c29960200889767118d08`.
Wersja wydania: nie nadano w tym worktree.

## English guide and changelog draft

Use **Ctrl+Alt+Enter** to open settings for the current session. Playback
announcements inherit the defaults in Program Settings → Accessibility and
messages. Choose On or Off for a session, or return to “Use global setting”.
Only controls backed by that session's transport are shown. Muted routine
confirmations remain available in status; transport errors still speak.
The shared player covers files, radio, podcasts/YouTube, TIDAL, Spotify and
Radio Archiwista. Device sessions expose the supported subset shown above.
The original Apple Music app has external transport confirmations only.

Changelog draft: Added per-session inheritance for playback, seek and volume
announcements and accessible labels in the session settings dialog. Updated
the shared command description. Live NVDA acceptance and release status must
be filled in after installation; this draft is not approved for publication.
