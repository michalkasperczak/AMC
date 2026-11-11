# RADIO-TRACK-001 — implementacja ręcznego zapisu fragmentu

## Korekta 11.11.2026 — także MP3

Użytkownik rozszerzył wcześniejszą decyzję o zapisie wyłącznie do WAV: w oknie
fragmentu wybiera się teraz **WAV — bezstratny, większy plik** (ustawienie
początkowe) lub **MP3 — 192 kb/s, mniejszy plik**. Zapis MP3 korzysta z tego
samego bufora i istniejącego systemowego kodera Media Foundation; kompresja
jest stratna. Rozszerzenie pliku musi odpowiadać wybranemu formatowi. Istniejący
plik nie jest zastępowany, a kursor radia i nagranie pozostają bez zmian.
Wcześniejsze stwierdzenia poniżej o wyborze samego WAV opisują pierwotny etap.

**Weryfikacja korekty:** host LiteHost Release, silnik Windows Release i zestaw
SmokeTests Release skompilowano bez ostrzeżeń i błędów. Test kotwic próbki
`--radio-sample-anchors-only` zakończył się kodem 0: odczytał plik WAV i nowo
zakodowany MP3 o wybranej długości oraz potwierdził niezmieniony kursor radia.
`wxlite/tests/test_radio_recognition.py`: 18/18 testów, w tym przekazanie
wybranego MP3 i nazwy do hosta. Testy skrótów, palety i menu: 106/106.
Zmodyfikowane moduły Pythona przeszły kontrolę składni. Aktualizację przygotowano w źródłach; nowej
wersji użytkowej nie zainstalowano ani nie opublikowano. Nawigacja nowego pola
formatu z NVDA wymaga odbioru po instalacji wersji testowej; nie wykonano jej
na dotychczasowej wersji aplikacji, która pola jeszcze nie zawiera.
Commit źródłowy: `f2797c07`. Wersja użytkowa pozostaje dotychczasowa do czasu
zbiorczej aktualizacji AMC Test.

## Ręczny zapis dostępnego fragmentu — 11.11.2026

Po pomiarach ICY, HLS, Time Shift i prywatnego toru wdrożono funkcję **Sprawdź i zapisz fragment radia…** w historii rozpoznanych utworów. Nie nazywa pliku „całym utworem” i nie wyznacza granic z samego czasu odpowiedzi Shazam. To zastępuje wcześniejszą propozycję automatycznego wskazywania początku i końca na podstawie niezweryfikowanych metadanych. Użytkownik wybiera początek i koniec w sekundach względem początku 12-sekundowej próbki, odsłuchuje po pięć sekund przy obu punktach i wybiera plik WAV.

- Kotwica próbki pozostaje w hoscie C# jako krótkotrwały żeton powiązany z konkretnym obiektem toru i segmentem pierścienia. Przy powtórnym rozpoznaniu tej samej pozycji historia odświeża żeton. Nie zapisuje go na dysk; po ponownym uruchomieniu trzeba rozpoznać utwór jeszcze raz.
- Przed odsłuchem i zapisem host sprawdza identyfikator bufora, segment, dostępny zakres oraz wyrównanie do ramki PCM. Zapis wykonuje kopię w kawałkach 64 KiB pod krótkimi blokadami, bez przestawiania kursora odsłuchu i bez zatrzymywania nagrania. Jeśli pierścień nadpisze fragment w trakcie, plik częściowy jest usuwany. Gotowy plik powstaje przez przeniesienie pliku tymczasowego w tym samym folderze; istniejąca nazwa nie jest nadpisywana. WAV ma limit 2 GB.
- W historii: zaznaczenie rozpoznania, przycisk **Sprawdź fragment…** albo pozycja menu kontekstowego **Sprawdź i zapisz fragment radia…**. W oknie: pola **Początek** i **Koniec**, przyciski **Odtwórz początek**, **Odtwórz koniec**, **Zapisz fragment…**, **Anuluj**. Komunikaty NVDA/statusu obejmują niedostępną próbkę, błędną kolejność granic, rozpoczęcie i koniec odsłuchu, rozpoczęcie zapisu oraz jego wynik. Fokus wraca do listy historii po zamknięciu okna.
- Obie osie Time Shift pozostają rozdzielone: próbka odtwarzana wskazuje bufor głównego odtwarzacza, a próbka z aktywnego nagrania wskazuje jego prywatny tor. Zmiana stacji, reconnect i nadpisanie bufora unieważniają odpowiedni zakres.

**Sprawdzenie źródeł:** LiteHost Release i Windows Release kompilują się bez ostrzeżeń i błędów. Windows SmokeTests Release zbudowano z `-p:BuildProjectReferences=false` po osobnej poprawnej kompilacji Windows; `--radio-sample-anchors-only` kończy się kodem 0, sprawdzając długość WAV, niezmieniony kursor odsłuchu i utratę próbki po nadpisaniu. Zestaw Python dla historii rozpoznań, akcji, historii odtwarzania, palety, skrótów i wariantów YouTube: 127/127. Samą wersję GUI przygotowaną do commitu sprawdzono składniowo i w 18/18 testach. `compileall` dla zmienionych modułów: bez błędów. Pierwsza standardowa kompilacja SmokeTests zatrzymała się w rozwiązywaniu referencji projektu bez diagnostyki błędu; powyższa osobna kompilacja Windows i test usunęły tę przeszkodę środowiskową.

**Stan dostawy:** zaplecze zapisano w commicie `91d9b1db`, interfejs i szkic instrukcji w `06239e0b`, a sondy Time Shift i prywatnego toru w `f1a07e14`. Nie wykonano jeszcze testu NVDA na nowym oknie, instalacji ani publikacji. Test odsłuchu na rzeczywistym domyślnym urządzeniu i sprawdzenie słyszalnych granic pozostają konieczne przed obietnicą zapisu pełnej piosenki. Podgląd może mieszać się z nadal grającą stacją. Instrukcja i changelog są szkicem do korekty użytkownika. Wersja zainstalowana bez zmiany.
