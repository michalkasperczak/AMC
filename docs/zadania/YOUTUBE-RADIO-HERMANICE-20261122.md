# YouTube Radio Hermanice — odświeżenie kanału i Ctrl+R w terminie planu

Stan 22.11.2026: zmiana źródeł i testów w kandydacie `codex/alpha512-update-candidate-20261122`, commit `cadc8ead`. Nie instalowano ani nie wydano aktualizacji. Nie wykonano próby GUI z NVDA ani nagrania na żywo nowym kodem.

## Zgłoszenie i korekta miejsca zdarzenia

Michał zgłosił, że ręczne odświeżenie stacji YouTube „Hermanice” nie znajdowało trwającej transmisji, a Ctrl+R podczas terminu harmonogramu dawało niejasny wynik. Aktualny adres transmisji podał jako `https://www.youtube.com/watch?v=mD6gpBK7r6E`. Korekta Michała z 22.11.2026 zastępuje wcześniejsze przypisanie incydentu Hermesowi: nagranie było na głównym komputerze. Lokalny dziennik `C:\Users\micha\AppData\Roaming\AMC-wx-Lite\logs\frontend.log` o 16:15:12 potwierdza błąd `youtube.radioBroadcasts`: „Nie udało się ustalić kanału YouTube tej stacji”. Historia zawiera cztery ręczne próby Hermanic po 16:00, wszystkie nieudane i bez zapisanego pliku. Zgłoszonego pięciosekundowego pliku nie udało się potwierdzić w tej historii. Dalsze szczegóły są w `RADIO-LIVE-REC-20261122-zmiana-adresu-i-start.md`.

## Przyczyna i zachowanie

Gdy stacja przechowywała adres pojedynczego filmu `watch?v=...`, odświeżenie każdorazowo próbowało odzyskać identyfikator kanału z tego filmu. Jeśli stary film nie oddawał metadanych, operacja kończyła się błędem jeszcze przed sprawdzeniem nowych transmisji kanału. Poprzednie udane odświeżenie ponownie zapisywało adres jednego filmu, więc problem mógł wrócić.

Po zmianie host dołącza do wyniku odświeżenia trwały adres `https://www.youtube.com/channel/{identyfikator}/live`, jeśli ustali kanał. AMC zapisuje go w tym samym wpisie stacji i powiązanych planach zamiast następnego adresu `watch?v=...`. Odświeżenie stacji z już zapisanym adresem kanału zachowuje ten adres. Ścieżka publiczna odczytuje identyfikator bez ponownego wywołania yt-dlp, gdy URL zawiera prawidłowe `/channel/{identyfikator}`. Przy braku ustalonego kanału pozostaje dotychczasowe zachowanie i komunikat błędu; aplikacja nie zgaduje kanału po nazwie stacji.

Ctrl+R podczas aktywnego nagrania zaplanowanego dla wybranej stacji wysyła żądanie zatrzymania bieżącego terminu. Plan jednorazowy zostanie wyłączony po zakończeniu, a plan powtarzalny wyznaczy następny termin. Ta sama naciśnięta kombinacja nie uruchamia jednocześnie nagrania ręcznego. Jeśli poprzednia próba jeszcze się zamyka, ponowny Ctrl+R informuje, że trzeba poczekać i ponowić skrót po zakończeniu. W sesji Radio wypowiadane są odpowiednio: „Zatrzymuję zaplanowane nagrywanie: [stacja]. Bieżący termin nie wznowi się automatycznie” oraz „Nagranie stacji [stacja] jest już zatrzymywane. Po zakończeniu ponów Ctrl+R, aby rozpocząć nowe”. Dla zwykłego zatrzymania ręcznego komunikat pozostaje „Zatrzymuję nagrywanie: [stacja]”. Dotyczy to zarówno planu z Biblioteki, jak i Ulubionych, gdy wskazują ten sam identyfikator stacji.

## Odczyt publicznej transmisji i ograniczenie

Zainstalowany składnik yt-dlp `2026.10.26` odczytał z podanego adresu identyfikator kanału `UCLQDRrFkBrwK6UbcUypMi5w`. Odczyt metadanych `/channel/.../live` wskazał ten sam film, stan `True`, dostępność `public`; lista `/streams` wskazała film jako `is_live`. Były to odczytowe sprawdzenia publicznych metadanych po zgłoszeniu, a nie test nowego AMC ani dowód stanu o 16:15.

Jeśli **pierwsze** odświeżenie nastąpi już po utracie metadanych zapisanego filmu, bez wcześniej zachowanego adresu kanału AMC nadal nie może ustalić właściwego kanału. Wtedy trzeba wpisać aktualny film lub, lepiej, znany adres kanału. Obecnego profilu i planów Michała nie zmieniono; ewentualna podmiana nastąpi dopiero przy uruchomieniu nowej wersji i poleceniu odświeżenia albo podczas świadomej edycji stacji.

## Zmiany, testy i stany wydania

- Źródła: `LiteYouTubeAccountCoordinator.cs`, `LiteYouTubePublicBroadcastBackend.cs`, `LiteYouTubePublicBroadcastCoordinator.cs`, `LiteYouTubeAccountContract.cs`, `gui.py`, `LiteRadioRecordingCoordinator.cs`.
- Testy: `test_youtube_live.py`, `test_radio_recording.py`, `YouTubePublicBroadcastCoordinatorTests.cs`.
- `PYTHONPATH=wxlite python -m pytest wxlite/tests/test_radio_recording.py wxlite/tests/test_youtube_live.py wxlite/tests/test_youtube_public_gui.py -q`: **67 passed, 2 subtests passed**.
- Protocol Tests `--youtube-public-broadcasts`: **4 scenariusze, PASS**; `--youtube-account`: **9 scenariuszy, PASS**.
- `dotnet build src/AccessibleMediaController.LiteHost/AccessibleMediaController.LiteHost.csproj -c Release --no-restore --nologo`: **sukces, 0 ostrzeżeń, 0 błędów**.
- Źródła i testy: tak. Build: tak. Instalacja: nie. Próba z nowym AMC i NVDA: nie. Publikacja GitHub: nie. Hubert nie otrzymał jeszcze informacji o aktualizacji, ponieważ jej nie wydano.

## Następny krok

Przed instalacją sprawdzić, czy na głównym komputerze nie trwa nagranie. Na odrębnej krótkiej próbie sprawdzić odświeżenie z adresu filmu i później z zapisanego adresu kanału, odtwarzanie Enter, synchronizację planu oraz dwa naciśnięcia Ctrl+R podczas aktywnego terminu. Po rzeczywistym wydaniu przekazać Hubertowi wynik w odpowiednim zgłoszeniu GitHub. Przed wydaniem rozstrzygnąć także istniejące niepowodzenia pełnego zestawu testów opisane w raporcie integracji.
