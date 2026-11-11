# RADIO-TRACK-001 — implementacja ręcznego zapisu fragmentu

## Ręczny zapis dostępnego fragmentu — 11.11.2026

Po pomiarach ICY, HLS, Time Shift i prywatnego toru wdrożono funkcję **Sprawdź i zapisz fragment radia…** w historii rozpoznanych utworów. Nie nazywa pliku „całym utworem” i nie wyznacza granic z samego czasu odpowiedzi Shazam. To zastępuje wcześniejszą propozycję automatycznego wskazywania początku i końca na podstawie niezweryfikowanych metadanych. Użytkownik wybiera początek i koniec w sekundach względem początku 12-sekundowej próbki, odsłuchuje po pięć sekund przy obu punktach i wybiera plik WAV.

- Kotwica próbki pozostaje w hoscie C# jako krótkotrwały żeton powiązany z konkretnym obiektem toru i segmentem pierścienia. Przy powtórnym rozpoznaniu tej samej pozycji historia odświeża żeton. Nie zapisuje go na dysk; po ponownym uruchomieniu trzeba rozpoznać utwór jeszcze raz.
- Przed odsłuchem i zapisem host sprawdza identyfikator bufora, segment, dostępny zakres oraz wyrównanie do ramki PCM. Zapis wykonuje kopię w kawałkach 64 KiB pod krótkimi blokadami, bez przestawiania kursora odsłuchu i bez zatrzymywania nagrania. Jeśli pierścień nadpisze fragment w trakcie, plik częściowy jest usuwany. Gotowy plik powstaje przez przeniesienie pliku tymczasowego w tym samym folderze; istniejąca nazwa nie jest nadpisywana. WAV ma limit 2 GB.
- W historii: zaznaczenie rozpoznania, przycisk **Sprawdź fragment…** albo pozycja menu kontekstowego **Sprawdź i zapisz fragment radia…**. W oknie: pola **Początek** i **Koniec**, przyciski **Odtwórz początek**, **Odtwórz koniec**, **Zapisz fragment…**, **Anuluj**. Komunikaty NVDA/statusu obejmują niedostępną próbkę, błędną kolejność granic, rozpoczęcie i koniec odsłuchu, rozpoczęcie zapisu oraz jego wynik. Fokus wraca do listy historii po zamknięciu okna.
- Obie osie Time Shift pozostają rozdzielone: próbka odtwarzana wskazuje bufor głównego odtwarzacza, a próbka z aktywnego nagrania wskazuje jego prywatny tor. Zmiana stacji, reconnect i nadpisanie bufora unieważniają odpowiedni zakres.

**Sprawdzenie źródeł:** LiteHost Release i Windows Release kompilują się bez ostrzeżeń i błędów. Windows SmokeTests Release zbudowano z `-p:BuildProjectReferences=false` po osobnej poprawnej kompilacji Windows; `--radio-sample-anchors-only` kończy się kodem 0, sprawdzając długość WAV, niezmieniony kursor odsłuchu i utratę próbki po nadpisaniu. Zestaw Python dla historii rozpoznań, akcji, historii odtwarzania, palety, skrótów i wariantów YouTube: 127/127. Samą wersję GUI przygotowaną do commitu sprawdzono składniowo i w 18/18 testach. `compileall` dla zmienionych modułów: bez błędów. Pierwsza standardowa kompilacja SmokeTests zatrzymała się w rozwiązywaniu referencji projektu bez diagnostyki błędu; powyższa osobna kompilacja Windows i test usunęły tę przeszkodę środowiskową.

**Stan dostawy:** zaplecze zapisano w commicie `91d9b1db`, interfejs i szkic instrukcji w `06239e0b`, a sondy Time Shift i prywatnego toru w `f1a07e14`. Nie wykonano jeszcze testu NVDA na nowym oknie, instalacji ani publikacji. Test odsłuchu na rzeczywistym domyślnym urządzeniu i sprawdzenie słyszalnych granic pozostają konieczne przed obietnicą zapisu pełnej piosenki. Podgląd może mieszać się z nadal grającą stacją. Instrukcja i changelog są szkicem do korekty użytkownika. Wersja zainstalowana bez zmiany.

### Materiał do dokumentacji użytkownika

Ten etap nie dodaje polecenia do AMC. Pomiary sprawdzają, czy aplikacja potrafi wskazać odcinek dźwięku użyty do rozpoznania. Wynik nie jest jeszcze przyciskiem zapisu całego utworu: przed takim zapisem trzeba usłyszeć i zatwierdzić początek oraz koniec.

### Uzasadnienie i decyzje

Użyto jednego lokalnego narzędzia pomiarowego dla strumieni ICY i HLS oraz osobnej próby prawdziwego nagrania. Po błędzie 429 wyłączono kolejne wywołania Shazama w sondzie; to nie zmienia normalnego rozpoznawania w AMC. Metadane po komunikacie stacji pokazują, że pojedyncza zmiana tytułu nie wystarcza do automatycznego wycięcia piosenki.

### Testy i wynik

SmokeTests Release: dwie kompilacje po zmianach, każda z 0 błędów i ostrzeżeń. `--radio-sample-anchors-only` i `--radio-stream-title-only`: kod wyjścia 0. Trzy zakończone pomiary żywego strumienia oraz osobny test toru nagrania opisano wyżej. `git -c core.whitespace=cr-at-eol diff --check`: bez błędów; opcja odpowiada istniejącym końcom linii CRLF w `Program.cs`. Test NVDA i instalacja: nie wykonywano.

### Znane ograniczenia

Nie uchwycono i nie odsłuchano przejścia w samym dźwięku. Jedno wcześniejsze rozpoznanie dostało 429, a równoległy raport ICY/HLS opisuje pojedynczy wynik Shazama rozbieżny z metadanymi. Pomiar nie rozstrzyga, które nagranie w próbce jest właściwe, jeśli obejmuje przejście. Nie zmierzono jeszcze kosztu bezpiecznego eksportu zakresu z pełnego bufora ani odtwarzania po restarcie programu.

### Wersja i commit

Rozszerzenie sondy, prywatna próba i niniejsze uzupełnienie są niezatwierdzonymi zmianami roboczymi na bazie `91e2b99966572a0b68c9ba604507d58ab3b739fc`; commit tych zmian: brak. Zbudowano tylko testowy projekt SmokeTests, nie pakiet aplikacji. Instalacja AMC Test i opublikowane wydanie nie zmieniły się.
