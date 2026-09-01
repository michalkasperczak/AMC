# Składniki zewnętrzne / Third-party components

AMC korzysta z następujących składników audio:

- **NAudio 2.3.0** — Copyright (c) Mark Heath i współtwórcy, licencja MIT. Pełny tekst: `licenses/NAudio-MIT.txt`.
- **NLayer 2.0.1** i **NLayer.NAudioSupport 2.0.1** — zarządzany dekoder awaryjny MPEG Layer I, II i III, Copyright (c) Andrew Ward, Mark Heath i współtwórcy, licencja MIT. Pełny tekst: `licenses/NLayer-MIT.txt`.
- **NAudio.Vorbis 1.5.0** i **NVorbis 0.10.4** — dekodowanie OGG/Vorbis, Copyright (c) Andrew Ward, licencja MIT. Pełne teksty: `licenses/Vorbis-components-MIT.txt`.
- **SoundTouch.Net 2.3.2** oraz **SoundTouch.Net.NAudioSupport.Core 2.3.2** — port biblioteki SoundTouch autorstwa Olafa Woudenberga, na podstawie SoundTouch Olliego Parviainena; licencja GNU LGPL 2.1 lub nowsza. Pełny tekst: `licenses/SoundTouch.Net-LGPL-2.1.txt`.
- **BASS 2.4** — własnościowa biblioteka Un4seen Developments używana jako opcjonalny, preferowany dekoder bezpośrednich strumieni radia internetowego. BASS jest bezpłatny wyłącznie do zastosowań niekomercyjnych. AMC nie jest sprzedawany, nie zawiera reklam, płatnych funkcji ani odsyłaczy do darowizn. Osoba wykorzystująca AMC lub jego fork komercyjnie musi usunąć BASS albo uzyskać właściwą licencję od Un4seen Developments. Pełne warunki dostarczone z biblioteką: `licenses/BASS-2.4.txt`; strona producenta: https://www.un4seen.com/bass.html.
- **ShazamIO** — natywny port C# algorytmu podpisu akustycznego z projektu ShazamIO, Copyright (c) 2021 dotX12, licencja MIT. Pełny tekst: `licenses/ShazamIO-MIT.txt`. Adapter rozpoznawania jest opcjonalny i odizolowany; Shazam jest znakiem towarowym Apple Inc., a AMC nie jest powiązany ani wspierany przez Apple lub Shazam. Do usługi wysyłany jest podpis akustyczny, nie nagranie ani adres stacji.
- **FFmpeg** — opcjonalny, zarządzany składnik multimedialny. AMC pobiera stabilny wariant Windows x64 LGPL shared z projektu [BtbN/FFmpeg-Builds](https://github.com/BtbN/FFmpeg-Builds), wskazanego na oficjalnej [stronie pobierania FFmpeg](https://ffmpeg.org/download.html). Pakiet pozostaje oddzielnym programem i zestawem bibliotek w lokalnych danych aplikacji. Przed uaktywnieniem AMC sprawdza opublikowaną sumę SHA-256, uruchomienie, wersję oraz brak opcji GPL i nonfree. FFmpeg jest dostępny na licencji GNU LGPL 2.1 lub nowszej; pobrane wydanie zawiera licencję i informacje o kompilacji, a odpowiadające źródła i podpisy wydań są dostępne na ffmpeg.org.

Kod źródłowy użytej wersji SoundTouch.Net jest dostępny pod adresem:
https://github.com/owoudenberg/soundtouch.net/tree/98e5b8fd2f8efed0ddf7c8f66b435bfb231659dc

Biblioteki `SoundTouch.Net.dll` i `SoundTouch.Net.NAudioSupport.dll` są publikowane jako oddzielne, wymienne pliki obok programu.

Biblioteka `bass.dll` jest publikowana jako oddzielny, wymienny plik obok programu i nie jest objęta licencją kodu źródłowego AMC. Jej brak lub odrzucenie strumienia nie wyłącza radia: AMC automatycznie przechodzi do dotychczasowych dekoderów systemowych i zarządzanych.

AMC korzysta również z następujących składników do lokalnego przechowywania Biblioteki:

- **Microsoft.Data.Sqlite 8.0.30** — Copyright (c) .NET Foundation and Contributors, licencja MIT. Pełny tekst: `licenses/Microsoft.Data.Sqlite-MIT.txt`.
- **SQLitePCLRaw 2.1.12** — Copyright Eric Sink, licencja Apache License 2.0. Pełny tekst: `licenses/SQLitePCLRaw-Apache-2.0.txt`.
- **SQLite** — silnik bazy danych przekazany do domeny publicznej przez autorów. Oświadczenie: `licenses/SQLite-public-domain.txt`.
