# AUDIO-EDIT-20261122 — Tok, odzysk nazwy i zabezpieczenie cięcia

## Zgłoszenie i korekta wcześniejszej diagnozy

22.11.2026 Michał zgłosił, że po próbie usunięcia około sześciu minut z
pięciogodzinnego nagrania Tok AMC powiedział, że operacja się nie udała.
Po Escape nagranie zniknęło z listy „Nagrane”. Ta obserwacja zastępuje
hipotezę z raportu `AUDIO-EDIT-20261122-dwa-ciecia-diagnoza.md`, według której
pozostałe pliki z kropką mogły być wyłącznie skutkiem późniejszej
synchronizacji. Mamy teraz konkretny dziennik, stan plików i ścieżkę błędu.

## Dowody z komputera Michała — tylko odczyt

- Historia nagrań w `C:\Users\micha\AppData\Roaming\AMC-wx-Lite\state.json`
  nadal wskazuje `D:\iCloud\iCloudDrive\iCloud~co~supertop~castro\Sideloads\YouTubeAudio\Tok - 2026-11-22 16-06-33.mp3`.
  Ta pierwotna ścieżka nie istnieje w chwili badania.
- Zaznaczenie zapisane w profilu: 514,7636666–890,169414 sekundy, czyli
  około 8:35–14:50, długość 375,4057474 sekundy.
- W tym samym folderze pozostały `.Tok - 2026-11-22 16-06-33.amc-cut-64c076d1652446fb95237987cf73f10b-result.mp3`
  (317 732 514 bajtów) i analogiczny `-before.mp3` (8 217 402 bajty).
  Atrybuty obu: 0x80420 = Archive, ReparsePoint, Pinned. Nie znaleziono
  pierwotnej nazwy ani kopii `.amc-backup` dla tego pliku.
- `frontend.log` o 23:06:46 pokazuje, że silnik prawidłowo odrzucił próbę
  `pauseResume` podczas cięcia. O 23:06:58 `audio.clipRemoveOriginal` zgłosiło
  `FileNotFoundException` przy `FileInfo.get_Length()` dla pierwotnej ścieżki.
  W obecnym kodzie ten odczyt jest **po** powrocie z `RemoveAsync`, więc samo
  cięcie doszło do etapu zakończenia, lecz nazwy nie było przy późniejszej
  aktualizacji metadanych. Dziennik nie dowodzi, który proces zmienił nazwę;
  Escape nie jest w nim wskazany jako przyczyna.
- Kopia wyniku ma według FFprobe 19 903,921633 sekundy, czyli 5:31:43,9.
  Próbki z początku, około 10 000. sekundy i końca dekodują się bez błędu.
  To nie jest pełny odsłuch ani dowód dokładnych granic cięcia.

## Dopowiedzenie Michała z 22.11.2026 — Ctrl+Shift+T

Michał zapytał, czy skrót czasu całkowitego ponownie otwiera plik albo
spowodował końcowe sprawdzanie. W sesji Pliki/Podcasty `Action.TIME_TOTAL`
przechodzi przez `_announce_time`, która czyta wyłącznie `_last_status`
w interfejsie. `time_announcement` formatuje liczbę; skrót nie wywołuje hosta,
nie otwiera pliku i nie podmienia nazwy. Mógł jednak mówić **stary czas**,
gdy edycja trwała. Odczyt `FileInfo.Length` z błędu o 23:06:58 jest osobną,
końcową próbą odczytania metadanych po powrocie z edytora, a nie efektem
Ctrl+Shift+T. Ten odczyt nie otwiera strumienia audio ani nie usuwa pliku.
Próba `pauseResume` o 23:06:46 pasuje do wyjścia z odtwarzacza przez Escape
przy włączonej pauzie po powrocie na listę; host ją zablokował. Dziennik nie
identyfikuje klawisza, więc to powiązanie pozostaje wnioskiem z kodu i czasu.

## Odzysk — nie zmieniano plików w iCloud

Niezależne kopie zachowano w:

`D:\Projekty Codex\AMC - odzysk nagrań\2026-11-22 Tok`

- `Tok - 2026-11-22 16-06-33 - odzysk.mp3` — 317 732 514 bajtów,
  SHA-256 `6030A6C643147F2E598CFEED8B706F482B3BAD9FE6849D81F122D86FD7BB25F8`.
- `Tok - 2026-11-22 16-06-33 - fragment przed cięciem.mp3` — 8 217 402 bajty,
  SHA-256 `6EA8BAE9C243165D754452830A10C1BC76723F6CA8B3218F71ABEF0D6C6A609D`.

Rozmiar i SHA-256 obu kopii zgadzają się z ocalałymi plikami źródłowymi.
Nie przenoszono, nie usuwano ani nie zmieniano żadnego nagrania w iCloud.
Nie modyfikowano też historii nagrań, więc obecna lista może nadal nie
pokazywać odzyskanego pliku. Nie usuwać kopii ani technicznych źródeł.

## Zabezpieczenie w kodzie

W aktywnej gałęzi `codex/alpha512-update-candidate-20261122`, commit
`ee3be3eb8870c758e5630ef5119606285439ca70`, dodano osobną ocenę
nadpisywania pliku. Atrybut ReparsePoint razem z Pinned, Unpinned, Offline
lub Recall oznacza plik zarządzany przez synchronizację. Przy takim pliku
AMC odmawia cięcia nad oryginałem i dopisywania do niego **przed zmianą
danych**, zarówno w kontroli dostępności, jak i w samym edytorze. Mówi,
żeby skopiować plik poza folder synchronizowany i tam wykonać edycję.
Zwykły plik lokalny, także z samym Pinned albo nazwą folderu „iCloudDrive”,
nie jest z tego powodu blokowany. Poprzednia decyzja „pobrany plik chmurowy
może być edytowany” dotyczyła dostępności danych; po tym incydencie sama
dostępność nie wystarcza do bezpiecznej podmiany nazwy.

To ograniczenie chroni przed powtórką utraty nazwy, lecz nie przywraca
bezpośredniej edycji w folderze synchronizowanym. Mechanizm stabilnej edycji
takiego pliku i automatycznego powrotu odzyskanego pliku na listę pozostają
oddzielnymi zadaniami.

Commit `f7b2f61aeaa15dbec69101d571f8538e60d870dd` koryguje komunikację:
podczas cięcia Ctrl+Shift+T (także dwa pozostałe skróty czasu) mówi, że czas
będzie dostępny po edycji, zamiast podawać starą wartość. Po zakończonym
etapie zapisu host sprawdza długość pliku **przed** zmianą pamięci metadanych;
jeśli pierwotna nazwa jest niedostępna, zgłasza, że wynik nie został
potwierdzony, i prosi o zachowanie plików technicznych. Nie twierdzi wtedy,
że oryginał na pewno pozostał bez zmian.

## Testy i stan wydania

- Test `--cloud-overwrite-guard`: RED przed kodem (brak nowej funkcji), GREEN
  po kodzie, exit 0. Obejmuje rzeczywiście zaobserwowany zestaw atrybutów
  0x80420, zwykły lokalny plik, Pinned bez ReparsePoint oraz sam symlink.
- Sonda z nowo zbudowanej biblioteki Core na rzeczywistych ścieżkach, wyłącznie
  odczyt metadanych: ocalały plik iCloud daje `RequiresSafeCopyForOverwrite=True`,
  kopia w `D:\Projekty Codex` daje `False`. Nie wykonywano na nich edycji.
- `--audio-clip-double-cut` na sztucznych WAV/MP3: exit 0, obie pierwotne
  nazwy zachowane na lokalnym NTFS.
- Build projektu `AccessibleMediaController.LiteHost`: exit 0, zero ostrzeżeń
  i błędów. Użyto lokalnego cache NuGet, bez audytu sieciowego.
- Test ścieżki GUI Ctrl+Shift+T: RED przed komunikatem (stary czas), GREEN
  po zmianie. Wszystkie 26 bezpośrednio uruchomionych kontroli w
  `test_transport_parity_gui_wiring.py` przeszło; w tym brak wywołania hosta
  przez skrót czasu. Ponowny build LiteHost po korekcie końcowego błędu:
  exit 0, zero ostrzeżeń i błędów. Nie wykonywano żywego testu NVDA.
- Sąsiedni zestaw `--audio-clip-append`: 15/16 przechodzi; przypadek MP3 dwa
  razy kończy się „Za mało zdekodowanych próbek do sprawdzenia kolejności”
  przy odczycie końcówki krótkiego pliku. Nie zaliczamy go; brak dowodu, że
  nowe zabezpieczenie spowodowało ten problem. Wymaga osobnej diagnozy.
- Nie było testu rzeczywistego cięcia w iCloud, NVDA, instalacji, całego
  pakietu ani nowego GitHub Release. Kod źródłowy i build to osobne etapy.

## Kolejność dalszej pracy

1. Zachować obie kopie odzysku i sprawdzić odsłuch użytkownika.
2. Dodać bezpieczną drogę przygotowania lokalnej kopii oraz powrotu
   gotowego nagrania do Biblioteki lub historii „Nagrane”, bez nadpisania
   przez synchronizację.
3. Na osobnym sztucznym pliku w katalogu synchronizowanym przetestować
   odmowę wstępną i komunikat NVDA, bez użycia nagrań Michała.
4. Osobno zbadać test dopisywania MP3 i dokładność granicy cięcia MP3.
5. Po zaliczeniu bramki wydać Test/Public na GitHub z kodem i instalatorami;
   Michał chce aktualizacje co 2–3 dni, bez równoległej pracy Hermesa.

## Materiał do instrukcji użytkownika

Do czasu wydania poprawki nie usuwać fragmentów bezpośrednio z nagrań w
folderach iCloud/synchronizowanych. Po wydaniu AMC odmówi nadpisania pliku
zarządzanego przez synchronizację i poprosi o edycję lokalnej kopii.
Odzyskany plik Tok jest dostępny pod podaną wyżej ścieżką na D, ale jego
automatyczny powrót na listę „Nagrane” nie został jeszcze wykonany.
