# Google Cast w AMC — instrukcja i lista zmian do odbioru

Wersja robocza z 01.11.2026. Funkcja jest przygotowana w źródłach; ten dokument nie oznacza zainstalowania nowej wersji AMC. Użytkownik zaliczył odbiór listy urządzeń z NVDA w osobnym oknie próby.

## Wybór urządzenia

1. Włącz Google Cast w aplikacji WiiM Home, jeśli jest wyłączony. Komputer i głośnik muszą być w tej samej sieci lokalnej.
2. W AMC przejdź do Plików lokalnych, Radia albo Podcastów. Naciśnij **Shift+C** lub wybierz **Dźwięk → Google Cast bieżącej sesji**.
3. Strzałkami wybierz głośnik i zatwierdź Enterem. Escape anuluje. Wybór dotyczy bieżącej sesji; listy urządzeń nie są łączone z lokalnymi wyjściami audio.
4. Przy pierwszym otwarciu AMC wyszukuje urządzenia. Kolejne otwarcia korzystają z zapamiętanej listy. **Odśwież urządzenia** ponawia wyszukiwanie. Przy zatwierdzaniu AMC ponownie sprawdza dostępność wskazanego odbiornika.

Lista podaje nazwy urządzeń oraz pozycję **Ten komputer**. Po otwarciu fokus jest na dotychczasowym wyborze, a po zamknięciu wraca do miejsca wywołania. Potwierdzenie brzmi **Wyjście sesji: [nazwa]**. Brak urządzenia lub nieudane odtwarzanie daje komunikat o błędzie.

## Odtwarzanie i powrót

Wybrany głośnik przejmuje aktualny pojedynczy plik, stację lub odcinek. Jeżeli nic nie gra, następny materiał tej sesji zostanie wysłany na wybrane urządzenie. Pauza, wznowienie, głośność i wyciszenie sterują odbiornikiem. Pliki i zwykłe odcinki można przewijać.

Aby wrócić do lokalnego toru, naciśnij **Shift+C** i wybierz **Ten komputer**. AMC zachowuje znaną pozycję oraz pauzę. **Shift+A** nadal wybiera lokalne urządzenie audio.

## Ograniczenia tego etapu

- Radio jest przesyłane na żywo, bez bufora Time Shift AMC.
- Obsługiwane są pojedyncze materiały. Ciągłe kolejki, tempo, zakładki, rozdziały i edycja fragmentów wymagają lokalnego odtwarzania.
- Pozycja z Cast służy bieżącemu powrotowi do komputera. Jej trwały zapis po zamknięciu AMC pozostaje do wdrożenia.
- Treści chronione Spotify, TIDAL i źródłowe materiały YouTube wymagają własnej obsługi tych usług. Nie jest przesyłany cały dźwięk Windows.
- Format musi być obsługiwany przez odbiornik. Dla lokalnych plików dopuszczone są MP3, AAC/M4A, WAV, FLAC, Ogg/Opus i MP4. Nie każdy strumień lub serwer internetowy musi działać.
- Utrata sieci powoduje komunikat o utracie sterowania. Bez łączności AMC nie może potwierdzić zatrzymania głośnika.
- Mirroring oraz proponowane **Shift+M** są osobnym pomysłem.

## Robocza lista zmian

- Osobny wybór Google Cast bieżącej sesji pod Shift+C, w menu, palecie i pomocy.
- Wykrywanie urządzeń, pamięć listy i wybór po stabilnej tożsamości bez odczytywania identyfikatorów przez NVDA.
- Przesyłanie pojedynczych plików i dostępnych strumieni, sterowanie transportem oraz powrót do komputera.
- Ograniczone udostępnianie jednego lokalnego pliku, z obsługą przewijania i automatycznym zakończeniem serwera.
- Zapis potwierdzonych startów w historii właściwej sesji oraz blokowanie funkcji niedostępnych podczas Cast.

Próby sprzętowe wykonano na WiiM Sound; użytkownik potwierdził słyszalny sygnał. Kompilacja hosta przeszła bez błędów i ostrzeżeń, a celowana bramka dała 134 zaliczone testy. Końcowe 20 testów Cast oraz niewidoczna próba rzeczywistych kontrolek wxPython również przeszły. W osobnym oknie użytkownik potwierdził Shift+C, utrzymanie fokusu, poprawny odczyt listy, Escape i Alt+F4. Gotowa paczka, instalacja, odbiór w głównym AMC oraz publikacja pozostają osobnymi etapami.

[Instrukcja włączania Google Cast w WiiM](https://faq.wiimhome.com/en/support/solutions/articles/72000600942-how-to-enable-or-disable-google-cast-on-wiim-device)

https://faq.wiimhome.com/en/support/solutions/articles/72000600942-how-to-enable-or-disable-google-cast-on-wiim-device
