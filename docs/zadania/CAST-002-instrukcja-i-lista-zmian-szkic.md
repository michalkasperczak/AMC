# Google Cast i AirPlay — szkic do korekty, wydanie alpha.519 z 23.11.2026

Materiał do korekty użytkownika. Kod dostarczono w aktualizacjach Test i Public alpha.519. Ten tekst pozostaje szkicem instrukcji, a odsłuch nowych wariantów i odbiór NVDA nadal są otwarte.

## Wybór wyjścia

- **Shift+C — Google Cast bieżącej sesji.** Wybierz odbiornik strzałkami i zatwierdź Enterem. Escape anuluje. „Ten komputer” wraca do lokalnego odtwarzania. Cast przejmuje odtwarzanie materiału tej sesji.
- **Shift+M — AirPlay i dodatkowe wyjście audio.** Lista rozróżnia AirPlay, Google Cast i dodatkowe urządzenia lokalne. Główne wyjście nadal odtwarza; urządzenia mogą mieć słyszalne opóźnienie. Pierwsza pozycja wyłącza dodatkowe wyjście. Brak AirPlay na liście ma opisany powód, a odświeżanie ponawia wykrywanie.
- **Shift+A — Tor audio.** Nadal wybiera lokalny tor, niezależnie od listy Cast.

Polecenia są dostępne także w menu Dźwięk, palecie Ctrl+Shift+K i odpowiedniej pomocy. Po zamknięciu listy fokus powinien wracać do miejsca wywołania; odbiór tego zachowania z NVDA dla nowych sesji jest otwarty.

## Gdzie działa przygotowana obsługa

Pliki, Radio, Podcasty i Radio Archiwista używają dostępnych źródeł materiału. Dotyczy to również dodatkowego AirPlay. W Podcastach film/strona YouTube nie staje się przez to bezpośrednim strumieniem dla głośnika.

W **natywnym TIDAL** nie potrzeba TIDAL Connect. Dla Cast przygotowano przesłanie obsługiwanego nieszyfrowanego strumienia BTS FLAC/AAC/MP3. Jeśli utwór korzysta z DASH/MPD, Cast pozostaje niedostępny; AirPlay i drugie wyjście lokalne mogą korzystać z lokalnego źródła. Dostępność zależy od rzeczywistego bieżącego utworu. Spotify, Apple Music i odtwarzacze zewnętrzne wymagają osobnego rozwiązania; ta zmiana nie przechwytuje całego dźwięku Windows.

AirPlay jest dodatkowym odsłuchem, z możliwym przeliczeniem audio i opóźnieniem. Nie gwarantuje Lossless ani synchronizacji. Dawny problem braku dźwięku/timeoutu AirPlay na WiiM nie ma jeszcze potwierdzonej naprawy w odsłuchu.

## Projekt listy zmian

- Dodano Radio Archiwistę do Google Cast i dodatkowych wyjść, z poprawnym kierowaniem kolejnych materiałów oraz powrotem do tej samej sesji.
- Dodano warunkową obsługę natywnego TIDAL i powrót Cast z zachowaniem znanej pozycji. Głośnik otrzymuje osobny adres dostępu ograniczony do tego odbiornika.
- Nazwa polecenia Shift+M wskazuje AirPlay; lista wyjaśnia brak składników albo odbiorników.
- Spóźnione wykrywanie nie otwiera listy w innej sesji. Wyłączenie dodatkowego wyjścia pozostaje dostępne po zakończeniu materiału; brak potwierdzenia zatrzymania jest zgłaszany.

Wyniki i otwarte bramki: [karta CAST-002](CAST-002-20261123-zakres-sesji-i-AirPlay.md). Wersja techniczna: `0.1.0-alpha.519`; kod `dc000561d05ae7e0f2924c620e3d3f7599d943bd`. Instalację wykonuje użytkownik.
