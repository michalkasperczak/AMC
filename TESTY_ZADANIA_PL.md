# Zadania testowe AMC

- Numer zestawu: `AMC-TEST-143`
- Tytuł zestawu: Ustawienia nagrywania Radia, foldery, formaty i wybudzanie
- Wersja programu: `0.1.0-alpha.143`
- Utworzono: 2026-08-29, Europe/Warsaw
- Plik wyników: `wyniki-testow/WYNIKI_0.1.0-alpha.143.md`

Na początku pliku wyników wystarczy opisać zauważone zachowanie. Nie trzeba przed każdym zadaniem dopisywać osobnego wariantu „OK” lub „błąd”. Po dwukropku wpisuj spację.

## Nowości alpha 143

### AMC-143-01 — Dostępność ustawień Radia

Otwórz Ustawienia i kartę „Radio i nagrywanie”. Przejdź Tabem po wszystkich
polach, rozwiń pola formatu i bitrate'u, poruszaj się strzałkami, anuluj okno,
a następnie otwórz je ponownie i zapisz zmiany.

Oczekiwane: NVDA czyta tylko pełne, polskie etykiety. Nie pojawiają się nazwy
klas, rekordy w nawiasach klamrowych, identyfikatory enumów ani nazwy
właściwości. Początkowo wybrany element i każda zmiana są oznajmiane, a po
Zapisz lub Anuluj fokus wraca do AMC.

### AMC-143-02 — Ogólny folder nagrań

Najpierw wybierz domyślny folder programu, nagraj ręcznie kilkanaście sekund
klawiszem `R` i sprawdź położenie pliku. Potem wybierz własny folder, zapisz
ustawienia i wykonaj drugie nagranie.

Oczekiwane: pierwsze nagranie trafia do „Muzyka, AMC — Nagrania radia”, drugie
do wybranego folderu. Ścieżka pozostaje zapamiętana po ponownym uruchomieniu.

### AMC-143-03 — Folder ogólny i własny dla harmonogramu

Utwórz dwa krótkie plany `Shift+R`: pierwszy z opcją użycia ogólnego folderu,
drugi z własnym folderem. Otwórz każdy ponownie do edycji przed terminem.

Oczekiwane: wybrany wariant i ścieżka są zachowane, a pliki trafiają do
właściwych folderów. Pole własnej ścieżki jest nieaktywne przy wariancie
ogólnym. Sam wybór folderu nie zmienia ogólnego ustawienia programu.

### AMC-143-04 — MP3, M4A/AAC i WAV

W ustawieniach wybierz kolejno MP3 192 kb/s, M4A/AAC 128 lub 160 kb/s oraz
WAV. Za każdym razem nagraj tę samą stację przez kilkanaście sekund.

Oczekiwane: powstają odpowiednio pliki `.mp3`, `.m4a` i `.wav`; każdy daje się
odtworzyć, ma rozsądny czas i nie pozostaje plik `.amc-partial`. Przy WAV pole
bitrate'u jest nieaktywne. WAV jest wyraźnie większy, co jest prawidłowe.

### AMC-143-05 — HLS i nietypowe parametry stacji

Nagraj ręcznie oraz przez krótki plan stację HLS, na przykład Trójkę albo
transmisję `m3u8`. Powtórz próbę dla stacji podającej 24 kHz, jeśli jest
dostępna. Podczas planu słuchaj innej stacji.

Oczekiwane: AMC nagrywa zdekodowany dźwięk także z HLS do wybranego formatu;
plan działa w tle i nie zmienia odsłuchu. Format wejściowy nie jest bezmyślnie
kopiowany do pliku, więc zmiany segmentów HLS nie uszkadzają nagrania. Jeżeli
dany HLS wymaga FFmpeg, program podaje kontrolowany błąd, a nie zawiesza się.

### AMC-143-06 — Globalne i indywidualne wybudzanie

Włącz globalne wybudzanie w Ustawieniach. Utwórz trzy plany: zgodny z
ustawieniem ogólnym, zawsze wybudzający i nigdy niewybudzający. Otwórz je
ponownie i sprawdź odczyty pól. Faktyczną próbę uśpienia wykonaj na końcu,
pozostawiając AMC uruchomiony i ustawiając termin kilka minut później.

Oczekiwane: przy wariancie dziedziczonym edytor mówi również, jaki jest obecny
stan ogólny. Wszystkie trzy wybory są zachowane. Obsługiwany komputer budzi się
przed planem; blokada wybudzania przez sprzęt, baterię albo plan zasilania nie
powoduje uszkodzenia harmonogramu.

### AMC-143-07 — Niedostępny folder i bezpieczny zapis

Wskaż własny folder harmonogramu na odłączanym albo synchronizowanym dysku,
następnie przed terminem uczyń go niedostępnym. Powtórz z niedostępnym folderem
ogólnym.

Oczekiwane: AMC sprawdza rzeczywistą możliwość zapisu i wybiera kolejny
bezpieczny folder: planowy, ogólny, a na końcu systemowy. Program nie publikuje
pustego lub niedokończonego nagrania i nie ujawnia technicznego wyjątku NVDA.

### AMC-143-08 — Regresja nagrywania i presetów

Sprawdź `R`, `Shift+R`, `Ctrl+Alt+Shift+R`, `Ctrl+0` oraz `Ctrl+Shift+0` na
liście i w odtwarzaczu.

Oczekiwane: `R` steruje nagrywaniem słyszanej stacji, `Shift+R` otwiera plan,
`Ctrl+Alt+Shift+R` listę planów, `Ctrl+0` listę sesji, a `Ctrl+Shift+0` preset 0.
Żaden skrót nie przejmuje roli innego.

## Poprzedni zestaw alpha 142

## Nowości alpha 142

### AMC-142-01 — Preset 0 z NVDA

Przy uruchomionym NVDA ustaw preset pod klawiszem `0`. W aktywnym AMC naciśnij
`Ctrl+0`, wróć Escapem, a następnie naciśnij `Ctrl+Shift+0`. Powtórz próbę na
liście i w odtwarzaczu. Na koniec przejdź do innej aplikacji i użyj tam tej
samej kombinacji.

Oczekiwane: `Ctrl+0` otwiera listę sesji, a `Ctrl+Shift+0` uruchamia preset 0 i
nie milczy. AMC nie przejmuje skrótu, kiedy jego okno nie jest aktywne.

### AMC-142-02 — R nagrywa aktualnie słyszaną stację

Otwórz stację w odtwarzaczu. Sprawdź menu kontekstowe, naciśnij `R`, odczekaj
kilkanaście sekund i naciśnij `R` ponownie.

Oczekiwane: menu zawiera rozpoczęcie lub zakończenie nagrywania z opisem `R`.
Program mówi o początku i końcu, a powstały MP3 można odtworzyć.

### AMC-142-03 — Zmiana stacji podczas ręcznego nagrywania

Rozpocznij nagrywanie klawiszem `R`, a następnie spróbuj zmienić stację
Page Down albo presetem. Najpierw odpowiedz „Nie”, a przy drugiej próbie „Tak”.

Oczekiwane: „Nie” pozostawia bieżącą stację i nagrywanie. „Tak” finalizuje MP3
i dopiero potem przełącza stację.

### AMC-142-04 — Shift+R rozpoczyna nagranie czasowe

Na stacji w liście naciśnij `Shift+R`. Zostaw zaznaczone rozpoczęcie natychmiast,
ustaw jedną minutę i zapisz. Podczas nagrania słuchaj innej stacji.

Oczekiwane: nagranie rusza od razu w tle, nie zmienia odsłuchu i po minucie
powstaje poprawnie zakończony MP3.

### AMC-142-05 — Termin przyszły i menu kontekstowe

Ponownie użyj `Shift+R`, wyłącz rozpoczęcie natychmiast i ustaw przyszły termin.
Sprawdź też menu kontekstowe stacji oraz odtwarzacza.

Oczekiwane: można zapisać przyszły plan i powtarzanie. Menu listy zawiera
nagranie czasowe i harmonogram, a menu odtwarzacza dodatkowo nagrywanie `R`.

## Poprzedni zestaw alpha 141

## Nowości alpha 141

### AMC-141-01 — Ctrl+0 oraz Ctrl+Shift+0 są rozłączne

W Radiu przypisz Radio Emaus do miejsca oznaczonego klawiszem `0`. Naciśnij
`Ctrl+0`, zamknij listę sesji Escapem, a następnie naciśnij `Ctrl+Shift+0` na
liście Biblioteki, w Ulubionych, na playliście i w odtwarzaczu.

Oczekiwane: `Ctrl+0` zawsze otwiera listę sesji. `Ctrl+Shift+0` nigdy jej nie
otwiera, lecz od razu uruchamia Radio Emaus i mówi „Preset 0”. W innej sesji z
pustym miejscem mówi „Preset 0 pusty” zamiast milczeć albo otwierać sesje.

### AMC-141-02 — Dostępność listy i edytora harmonogramu

Na wybranej stacji naciśnij `Ctrl+Alt+Shift+R`. Na pustej lub istniejącej
liście użyj Insert, przejdź Tabem przez wszystkie pola, rozwiń każde pole kombi
i poruszaj się strzałkami. Anuluj edytor, otwórz go ponownie i zapisz wpis.

Oczekiwane: fokus zaczyna na liście planów, a w nowym wpisie na wyborze stacji.
NVDA czyta wyłącznie polskie etykiety. Nie pojawiają się nazwy klas, rekordy w
nawiasach klamrowych ani identyfikatory wartości. Anulowanie i zapis wracają na
właściwy wpis listy.

### AMC-141-03 — Jednorazowe nagranie w tle

Utwórz plan wybranej stacji na 2–3 minuty w przyszłości, o długości jednej
minuty. Przed terminem rozpocznij słuchanie innej stacji albo lokalnego pliku i
nie zamykaj AMC.

Oczekiwane: o terminie AMC oznajmia rozpoczęcie planu, lecz nie przełącza ani
nie słychać zaplanowanej stacji. Po minucie oznajmia zakończenie i powstaje
poprawnie zamknięty MP3 w folderze nagrań. Bieżące odtwarzanie działa dalej.

### AMC-141-04 — Stan, zatrzymanie i kilka planów

Podczas aktywnego planu ponownie otwórz harmonogram. Jeżeli to możliwe, ustaw
drugi, nakładający się plan. Usuń jeden aktywny wpis i potwierdź ostrzeżenie.

Oczekiwane: aktywny wiersz mówi „nagrywanie trwa”. Plany mogą nagrywać
równolegle, nie mieszając plików. Usunięcie aktywnego wpisu zatrzymuje tylko
jego zapis i finalizuje dotychczasową część; drugi plan i odsłuch pozostają.

### AMC-141-05 — Trwałość i powtarzanie

Zapisz plan codzienny oraz plan na wybrane dni tygodnia. Zamknij i uruchom AMC,
otwórz harmonogram, edytuj jeden wpis i zapisz go ponownie.

Oczekiwane: wpisy, stacje, terminy, długości, dni, foldery i ustawienia
wybudzania są zachowane. Po zakończeniu plan cykliczny przechodzi do najbliższego
właściwego dnia, a jednorazowy znika. Fokus po edycji wraca na ten sam wpis.

### AMC-141-06 — Folder zastępczy i bezpieczne pominięcie

Opcjonalnie wskaż dla planu folder, który przed terminem stanie się niedostępny.
Uruchom AMC również raz w trakcie nadal trwającego okna oraz raz dopiero po jego
całkowitym końcu.

Oczekiwane: niedostępny folder nie przerywa planu — MP3 trafia do ogólnego
folderu nagrań. Start w trakcie okna zapisuje tylko pozostały czas. Całkowicie
pominięty termin nie rozpoczyna spóźnionego pełnego nagrania; wpis cykliczny
przechodzi do następnego terminu.

## Poprzedni zestaw alpha 140

## Nowości alpha 140

### AMC-140-01 — Bezpośredni preset 0

W Radiu przypisz Radio Emaus do miejsca 10, oznaczonego klawiszem `0`.
Naciśnij `Ctrl+Shift+0` kolejno w Bibliotece, Ulubionych, playliście oraz
w otwartym odtwarzaczu.

Oczekiwane: za każdym razem zostaje uruchomione Radio Emaus, program mówi
„Preset 0” i nazwę stacji, nie milczy i nie przełącza sesji. W innej sesji,
w której to miejsce jest wolne, program mówi „Preset 0 pusty”.

### AMC-140-02 — Podsumowanie czasu playlisty

Otwórz listę playlist zawierającą „Biskup” oraz co najmniej jedną playlistę
stacji radiowych.

Oczekiwane: przy „Biskup” po liczbie elementów jest podany łączny czas.
Jeżeli tylko część plików ma znany czas, program mówi „znany czas” i „część
bez danych”. Playlista składająca się wyłącznie ze stacji mówi „transmisje na
żywo” i nie podaje fikcyjnej długości.

### AMC-140-03 — Otwieranie i odtwarzanie całej playlisty

Na nazwie lokalnej playlisty naciśnij Enter, wróć do listy playlist, a następnie
naciśnij `Ctrl+Enter`.

Oczekiwane: Enter tylko otwiera zawartość. `Ctrl+Enter` odtwarza pierwszy
dostępny element, a Page Up i Page Down przechodzą po elementach tej playlisty.
Escape wraca do otwartej zawartości playlisty.

### AMC-140-04 — Działania zbiorcze na nazwie playlisty

Otwórz menu kontekstowe na nazwie playlisty. Sprawdź właściwości `Alt+Enter`,
dodawanie zawartości do Ulubionych i Biblioteki oraz — w Plikach lokalnych —
do Kolejki i jako następne.

Oczekiwane: nazwy poleceń jednoznacznie mówią o „zawartości playlisty”.
Właściwości opisują playlistę, a nie przypadkowy aktualnie odtwarzany plik.
W Radiu polecenia Kolejki i odtwarzania jako następne są ukryte.

### AMC-140-05 — Widoki lokalne z wnętrza playlisty

Wejdź do lokalnej playlisty i naciśnij kolejno `Alt+1`, `Alt+2` i `Alt+3`.

Oczekiwane: skróty świadomie opuszczają playlistę i otwierają odpowiednio
Foldery, Wszystkie pliki oraz Kolejność użytkownika w tej samej sesji. W Radiu
te skróty nadal nie przechodzą do Plików lokalnych.

### AMC-140-06 — Parametry stacji na pasku

Odtwórz Tyflo oraz Radio Białystok i odczytaj pasek przez `NVDA+End`.

Oczekiwane: Tyflo może podać `44,1 kHz` bez bitrate'u, jeżeli wiarygodny bitrate
nie został rozpoznany. Radio Białystok może podać `160 kb/s, 24 kHz`: pierwsza
wartość pochodzi z danych katalogu, druga z faktycznego formatu dekodera.
„Za transmisją” i „bufor” są podawane tylko podczas aktywnego odtwarzania.

## Poprzedni zestaw alpha 139

## Nowości alpha 139

### AMC-139-01 — Te same miejsca są niezależne w każdej sesji

W Plikach lokalnych przypisz element do presetu 1. Przejdź kolejno do Radia,
TIDAL, Apple Music i WiiM. W każdej sesji sprawdź preset 1, a następnie przypisz
do niego inny dostępny element demonstracyjny lub stację.

Oczekiwane: każda sesja ma własny preset 1. `Ctrl+Shift+1` uruchamia albo
otwiera tylko cel aktywnej sesji i nigdy sam nie przełącza usługi.

### AMC-139-02 — Plik, folder, album i playlista

W Plikach lokalnych przypisz do różnych miejsc: zwykły plik, folder, album
rozpoznany z folderu i playlistę AMC. Wywołaj każdy cel skrótem bezpośrednim
oraz Enterem z listy `Ctrl+Alt+P`.

Oczekiwane: plik zaczyna się odtwarzać. Folder otwiera wskazany poziom,
a album i playlista otwierają swoją zawartość bez samoczynnego odtwarzania.
Escape wraca do prawidłowego miejsca i żaden wiersz nie ujawnia technicznej
nazwy klasy ani pól rekordu.

### AMC-139-03 — Preset z wyniku wyszukiwania

Wyszukaj jeden plik albo stację. Na wyniku użyj `Ctrl+Alt+Shift+P`, przypisz
wolne miejsce i uruchom je później w tej samej sesji.

Oczekiwane: okno wyszukiwania przekazuje dokładnie zaznaczony wynik, tryb
przypisania ma nazwę bieżącej sesji, a zapisane miejsce działa po ponownym
uruchomieniu AMC.

### AMC-139-04 — Zachowanie dotychczasowych presetów po aktualizacji

Po uruchomieniu alpha 139 sprawdź presety zapisane wcześniej w Radiu i Plikach
lokalnych, w szczególności miejsca 10–12.

Oczekiwane: wcześniejsze przypisania pozostają. Radio zostało jednorazowo
przeniesione do wspólnego magazynu bez nadpisania istniejących miejsc; lista
nadal mówi „Preset numer 10, klawisz 0”, a `Ctrl+Shift+0` używa nazwy
fizycznego klawisza.

### AMC-139-05 — Niedostępny cel nie niszczy presetu

Przypisz lokalny plik lub folder, zamknij AMC, a następnie czasowo odłącz jego
źródło. Uruchom program i wywołaj preset.

Oczekiwane: AMC krótko informuje o niedostępnym celu, nie zawiesza się, nie
usuwa przypisania i nie przełącza sesji. Po ponownym udostępnieniu źródła
to samo miejsce znów działa.

## Poprzedni zestaw alpha 138

## Nowości alpha 138

### AMC-138-01 — Pusty preset pod Ctrl+Shift+0

W Plikach lokalnych i w Radiu pozostaw miejsce przypisane do klawisza `0` puste, a następnie naciśnij `Ctrl+Shift+0` na liście i w odtwarzaczu.

Oczekiwane: program za każdym razem mówi „Preset 0 pusty” i podaje sposób przypisania. Nie milczy, nie uruchamia innego miejsca i nie przełącza sesji.

### AMC-138-02 — Cyfry na liście presetów

Otwórz listę `Ctrl+Alt+P`. Naciśnij kolejno `5`, `8`, `0`, minus i znak równości, nie zatwierdzając Enterem.

Oczekiwane: fokus przechodzi odpowiednio do presetów 5, 8, 10, 11 i 12, a NVDA odczytuje pełną etykietę wybranego miejsca. Sama cyfra lub znak niczego nie uruchamia, nie zapisuje ani nie nadpisuje.

### AMC-138-03 — Brak regresji zajętych presetów

Przypisz stacje lub pliki do miejsc 1, 9 i 10. Uruchom je skrótami `Ctrl+Shift+1`, `Ctrl+Shift+9` i `Ctrl+Shift+0`, a następnie Enterem z listy presetów.

Oczekiwane: każdy skrót oraz lista uruchamiają właściwy element w bieżącej sesji. Lista nadal nazywa dziesiąte miejsce „Preset numer 10, klawisz 0”.

## Poprzedni zestaw alpha 137

## Nowości alpha 137

### AMC-137-01 — Lista presetów w Plikach lokalnych

W sesji Pliki lokalne otwórz kolejno Foldery (`Alt+1`), Wszystkie pliki (`Alt+2`) i Ulubione (`Ctrl+U`). W każdym widoku naciśnij `Ctrl+Alt+P`, przejdź po liście, po czym zamknij ją Escape.

Oczekiwane: zawsze otwiera się lista „Presety — Pliki lokalne” z dwunastoma jednoznacznie nazwanymi miejscami. Nie pojawiają się określenia radiowe ani techniczne dane obiektu. Escape wraca na element, z którego lista została otwarta.

### AMC-137-02 — Preset folderu Biblioteki

W `Alt+1` wskaż folder na dowolnym poziomie i naciśnij `Ctrl+Alt+Shift+P`. Wybierz wolne miejsce, zatwierdź Enterem, przejdź do innego widoku lub folderu i wywołaj zapisane miejsce przez `Ctrl+Shift+cyfra`.

Oczekiwane: przypisanie podaje nazwę folderu. Wywołanie nie uruchamia dźwięku i nie zmienia sesji, lecz otwiera dokładnie zapisany folder w widoku Foldery. Fokus trafia na jego pierwszą pozycję albo na czytelną pustą listę.

### AMC-137-03 — Preset pliku z Biblioteki i Ulubionych

Przypisz jeden plik z płaskiej Biblioteki, a drugi z Ulubionych. Wywołaj je bezpośrednimi skrótami oraz Enterem z listy `Ctrl+Alt+P`; sprawdź również przypisanie aktualnego pliku z otwartego odtwarzacza.

Oczekiwane: plik od razu rozpoczyna odtwarzanie, a Page Up i Page Down korzystają z właściwego lokalnego kontekstu. Preset jest ten sam niezależnie od widoku, z którego przypisano plik. Skrót nie przełącza do Radia.

### AMC-137-04 — Trwałość i niedostępny cel

Zamknij i ponownie uruchom AMC, po czym sprawdź zapisane presety. Następnie czasowo odłącz jeden Folder Biblioteki albo usuń przypisany plik z Biblioteki i spróbuj wywołać jego miejsce.

Oczekiwane: po ponownym uruchomieniu przypisania pozostają. Niedostępny cel nie powoduje zawieszenia, przełączenia sesji ani usunięcia presetu; program przekazuje krótki komunikat, że pliku albo folderu nie ma już w Bibliotece.

## Poprzedni zestaw alpha 136

## Nowości alpha 136

### AMC-136-01 — Pasek stanu na liście i w odtwarzaczu

W Radiu sprawdź `NVDA+End` na liście, podczas odtwarzania oraz po powrocie Escape z odtwarzacza. Powtórz po zmianie stacji przez Page Up lub Page Down.

Oczekiwane: NVDA za każdym razem odczytuje jedną aktualną treść paska. Nie milczy, nie powtarza tekstu i nie dodaje samej nazwy „Pasek stanu odtwarzania”. Fokus oraz skróty głównego widoku pozostają aktywne.

### AMC-136-02 — Jednoznaczne presety 10–12

Otwórz w Radiu `Ctrl+Alt+P` i przejdź do ostatnich trzech miejsc. Sprawdź także bezpośrednie skróty zajętych miejsc `Ctrl+Shift+0`, `Ctrl+Shift+-` i `Ctrl+Shift+=`.

Oczekiwane: lista mówi odpowiednio „Preset numer 10, klawisz 0”, „Preset numer 11, klawisz minus” i „Preset numer 12, klawisz znak równości”, po czym nazwę stacji albo informację, że miejsce jest puste. Skrót zajętego miejsca uruchamia jego stację i nie przełącza sesji.

### AMC-136-03 — Granica playlisty i presetu

Utwórz playlistę kilku stacji przez `Ctrl+P` i przejdź po niej Enterem oraz Page Up/Page Down. Osobno przypisz jedną z tych stacji do presetu przez `Ctrl+Alt+Shift+P`.

Oczekiwane: playlista jest trwałym widokiem wielu stacji, natomiast obecny preset wskazuje dokładnie jedną stację. Wywołanie presetu nie usuwa playlisty, nie zmienia jej składu i nie przedstawia całej playlisty jako zajętego miejsca. Obsługa całej playlisty przez pojedynczy preset jest zaplanowana po zakończeniu testów playlist.

## Poprzedni zestaw alpha 135

## Nowości alpha 135

### AMC-135-01 — Playlisty stacji radiowych

W Radiu naciśnij `Ctrl+P`, utwórz playlistę Insertem, wróć do Biblioteki i zaznacz jedną albo kilka stacji. Naciśnij `Ctrl+Shift+P`, dodaj je do playlisty, ponownie otwórz `Ctrl+P` i wejdź do niej Enterem.

Oczekiwane: Radio korzysta ze zwykłych playlist AMC. Lista zawiera wyłącznie stacje przypisane do tej playlisty. Page Up, Page Down, Delete, ręczna kolejność i ponowne uruchomienie programu zachowują się tak samo jak w innych sesjach. Usunięcie wpisu albo playlisty nie usuwa stacji z Biblioteki.

### AMC-135-02 — Playlisty w wyszukiwaniu i odtwarzaczu

Wyszukaj stacje, zaznacz kilka wyników Shiftem i naciśnij `Ctrl+Shift+P`. Powtórz dla aktualnie odtwarzanej stacji w odtwarzaczu.

Oczekiwane: menedżer playlist przyjmuje zaznaczone stacje z jednej sesji. Po zapisaniu fokus wraca do właściwego okna, a preset nie zostaje zmieniony.

### AMC-135-03 — Presety mają tylko skróty uniwersalne

W Radiu sprawdź kolejno `Ctrl+P`, `Ctrl+Shift+P`, `Ctrl+Alt+P` oraz `Ctrl+Alt+Shift+P`. Powtórz na liście i w odtwarzaczu, a następnie sprawdź menu Widok, menu kontekstowe i paletę poleceń.

Oczekiwane: pierwsze dwa skróty zawsze dotyczą playlist. `Ctrl+Alt+P` otwiera listę presetów, a `Ctrl+Alt+Shift+P` bezpieczny tryb utworzenia lub przypisania presetu. Nazwy dostępnościowe podają dokładnie te same skróty i nie ujawniają nazw technicznych.

### AMC-135-04 — Pasek stanu po zmianie widoków

Przejdź kilka razy między Biblioteką, playlistą, presetami i odtwarzaczem, po czym na każdym z głównych widoków naciśnij `NVDA+End`.

Oczekiwane: pasek pozostaje dostępny i za każdym razem przekazuje jedną aktualną treść. Samo otwieranie playlist lub presetów nie usuwa go ani nie przenosi na niego fokusu.

## Poprzedni zestaw alpha 134

## Nowości alpha 134

### AMC-134-01 — Pasek stanu odnajdywany przez NVDA

Na liście i w odtwarzaczu naciśnij `NVDA+End`, następnie zmień stację lub utwór i powtórz test.

Oczekiwane: NVDA za każdym razem czyta jedną aktualną treść paska. Nie mówi „Pasek stanu odtwarzania”, nie powtarza całego tekstu i nie przenosi fokusu.

### AMC-134-02 — Fokus podąża za odtwarzaniem

W `Ustawienia > Ogólne > Odtwarzanie` pozostaw włączone „Po wyjściu z odtwarzacza ustaw fokus na aktualnie odtwarzanym elemencie”. Uruchom element z listy, zmień go kilka razy przez Page Up lub Page Down i naciśnij Escape. Następnie wyłącz opcję i powtórz.

Oczekiwane: przy włączonej opcji fokus wraca na element aktualnie wybrany w odtwarzaczu, jeżeli jest widoczny w bieżącym widoku. Przy wyłączonej wraca na element zaznaczony przed wejściem do odtwarzacza. Żaden wariant nie przełącza widoku ani sesji.

### AMC-134-03 — Uniwersalne skróty presetów w Radiu

W Radiu sprawdź `Ctrl+Alt+P` i `Ctrl+Alt+Shift+P`, również z otwartego odtwarzacza. Porównaj z `Ctrl+P` i `Ctrl+Shift+P`.

Oczekiwane: oba skróty listy otwierają te same presety, a oba skróty przypisania otwierają bezpieczny tryb tworzenia presetu. Fokus po zamknięciu wraca do właściwego miejsca. Skróty z Altem nie uruchamiają menu głównego.

## Poprzedni zestaw alpha 133

## Nowości alpha 133

### AMC-133-01 — Numery i skróty wszystkich presetów

W Radiu otwórz `Ctrl+P` i przejdź po pozycjach 9–12. Osobno otwórz `Ctrl+Shift+P` i wskaż klawiszami miejsca 10, 11 i 12.

Oczekiwane: NVDA mówi kolejno numer miejsca i skrót. Ostatnie pozycje są jednoznaczne: „Preset 10, skrót Ctrl+Shift+0”, „Preset 11, skrót Ctrl+Shift+minus” oraz „Preset 12, skrót Ctrl+Shift+znak równości”. Żaden znak ani numer nie ginie, nie pojawia się techniczna nazwa rekordu.

### AMC-133-02 — Zwięzła zmiana stacji

Uruchom stację Enterem, następnie zmień ją kilka razy przez Page Up i Page Down oraz przez zajęte presety.

Oczekiwane: przy każdym wyborze AMC mówi samą nazwę stacji. Nie dodaje słów „Łączenie”, „Odtwarzanie” ani „stacja”. Komunikat o błędzie albo przekroczeniu czasu nadal pojawia się, jeśli dźwięk rzeczywiście nie ruszy.

## Poprzedni zestaw alpha 132

## Nowości alpha 132

### AMC-132-01 — Pasek bez zbędnego wstępu

W głównym oknie, na liście i w odtwarzaczu naciśnij `NVDA+End`. Powtórz podczas odtwarzania pliku oraz radia.

Oczekiwane: NVDA czyta aktualny stan, czas i dostępne parametry. Nie poprzedza ich zdaniem „Pasek stanu odtwarzania” ani nie powtarza treści. Fokus i działanie klawiatury pozostają bez zmian.

### AMC-132-02 — Nazwa stacji i utwór w tytule okna

Uruchom bezpośrednią stację ICY/MP3 albo OGG, która podaje nazwę bieżącego utworu. Odczytaj tytuł okna przez `NVDA+T`, odczekaj na zmianę utworu i odczytaj ponownie.

Oczekiwane: tytuł zaczyna się od nazwy stacji, a następnie podaje bieżący utwór lub audycję. Aktualizacja nie wywołuje osobnego automatycznego komunikatu i nie zmienia nazwy stacji na liście.

### AMC-132-03 — Brak i czyszczenie metadanych

Przełącz ze stacji pokazującej utwór na stację bez takich danych, szybko zmień antenę jeszcze raz, a następnie zatrzymaj radio.

Oczekiwane: nowa stacja nigdy nie dziedziczy tytułu poprzedniego utworu. Gdy strumień lub użyty dekoder nie udostępnia metadanych, w tytule pozostaje sama nazwa stacji oraz zwykły kontekst okna. Stary tytuł nie wraca po zatrzymaniu ani błędzie połączenia.

## Poprzedni zestaw alpha 131

## Nowości alpha 131

### AMC-131-01 — Kopiowanie jednego i wielu presetów

Otwórz `Ctrl+P`. Zaznacz Shiftem kilka miejsc, w tym przynajmniej jedno puste, i sprawdź `Ctrl+C` oraz `Ctrl+Shift+C`.

Oczekiwane: pierwsze polecenie kopiuje po jednej nazwie stacji w wierszu. Drugie kopiuje naprzemiennie nazwę i adres. Puste miejsca są pomijane, a zawartość i przypisania presetów nie zmieniają się.

### AMC-131-02 — Plik MP4 odtwarzany jako audio

Przez `Ctrl+O` dodaj MP4 zawierający dźwięk i obraz. Odtwórz go, przewiń, zmień prędkość, sprawdź lewą strzałkę i `Alt+Enter`.

Oczekiwane: nie otwiera się obraz ani dodatkowe okno. Działają zwykłe funkcje odtwarzacza, a właściwości mówią „ścieżka audio z pliku wideo”. Rozmiar całego MP4 nie jest podawany jako bitrate audio.

### AMC-131-03 — Pozostałe kontenery wideo

Jeżeli masz dostępne pliki MOV, M4V, MKV, WebM, AVI, WMV, MPEG albo M2TS ze ścieżką audio, dodaj je pojedynczo lub przez Folder Biblioteki.

Oczekiwane: pliki są indeksowane i AMC próbuje odtworzyć wyłącznie ścieżkę audio. Nieobsługiwany przez Windows kodek kończy się zwykłym komunikatem bez zawieszenia. Program nie obiecuje dźwięku w kontenerze, który nie ma ścieżki audio.

### AMC-131-04 — Ostatnia sesja i jej kolejność

W Ustawieniach wybierz uruchamianie ostatniej używanej sesji. Na liście kolejności sesji przesuń wybraną pozycję przez `Alt+góra/dół`, zapisz, przejdź do Radia i uruchom AMC ponownie. Powtórz z ustawieniem „Lista sesji”.

Oczekiwane: ruch jest oznajmiany wraz z nowym `Ctrl+numer`. Pierwszy tryb wraca bezpośrednio do Radia i zapamiętanego widoku. Drugi otwiera listę sesji z fokusem na Radiu, a nie zawsze na pierwszej pozycji.

### AMC-131-05 — Precyzyjna częstotliwość 357

Na Radiu 357 użyj lewej strzałki i rozpocznij odtwarzanie.

Oczekiwane: bieżący wariant, który dekoder otwiera jako 22050 Hz, jest czytany jako `22,05 kHz`, nie `22,5 kHz` ani `44,1 kHz`. Bitrate pozostaje oddzielną wartością około 128 kb/s.

## Poprzedni zestaw alpha 130

## Nowości alpha 130

### AMC-130-01 — Bezpieczne przypisanie wolnego presetu

W Radiu wybierz stację spoza Biblioteki i naciśnij `Ctrl+Shift+P`. Posłuchaj informacji o pierwszym wolnym miejscu, naciśnij jego cyfrę lub znak i zatwierdź Enterem. Zamknij i ponownie uruchom AMC.

Oczekiwane: początkowo czytany jest pełny użytkowy wiersz, bez nazwy klasy i pól technicznych. Enter zapisuje preset, zachowuje stację w Bibliotece radia i po restarcie nadal działa. Escape zamiast Entera nie zapisuje stacji ani presetu.

### AMC-130-02 — Ochrona zajętego miejsca

Wybierz inną stację i otwórz `Ctrl+Shift+P`. Wskaż raz zajęte miejsce i naciśnij Enter. Następnie wskaż dwukrotnie tę samą cyfrę lub znak i dopiero naciśnij Enter.

Oczekiwane: pierwszy wariant nie nadpisuje presetu i wyjaśnia wymagane potwierdzenie. Dopiero powtórne wskazanie tego samego miejsca oraz Enter zastępuje stację. Wybranie innego miejsca zmienia cel, a Escape anuluje.

### AMC-130-03 — Lista presetów tylko do uruchamiania

Naciśnij `Ctrl+P`. Przejdź po wszystkich dwunastu pozycjach, uruchom zajętą Enterem i Spacją, a na pustej spróbuj obu klawiszy. Powtórz przez menu i paletę poleceń.

Oczekiwane: fokus zaczyna się na rzeczywistym wierszu presetu. Zajęta pozycja uruchamia stację, pusta tylko wyjaśnia sposób przypisania. Lista nie zapisuje, nie zastępuje i nie usuwa presetów. `Ctrl+Shift+1–0/-/=` uruchamia zajęte miejsce bez otwierania listy.

### AMC-130-04 — Usunięcie przypisania

Otwórz `Ctrl+Shift+P`, wybierz zajęty preset, naciśnij Delete, a następnie Enter. Sprawdź `Ctrl+P`, Bibliotekę i Ulubione.

Oczekiwane: usuwane jest wyłącznie przypisanie miejsca. Sama stacja pozostaje w Bibliotece i zachowuje stan Ulubionej. Escape po Delete anuluje usunięcie.

### AMC-130-05 — Bitrate 357, HLS i zwykłego MP3

Na liście wybierz kolejno Radio 357, Trójkę HLS, BBC HLS oraz zwykłą stację MP3 i naciśnij lewą strzałkę. Powtórz po rozpoczęciu odtwarzania.

Oczekiwane: jeżeli strumień lub katalog ujawnia parametry, AMC podaje kodek, bitrate oraz dostępną częstotliwość bez wartości `44100 kb/s`. BBC z wariantem `audio=128000` podaje 128 kb/s. Chronione 357 i znany HLS Trójki mogą podać odpowiednio około 128 i około 192 kb/s jako jawnie przybliżony profil awaryjny. Nieznana transmisja wideo bez osobnego pasma audio nie może podać bitrate całego obrazu.

### AMC-130-06 — Właściwości radia i aktywne łącza

Na stacji z adresem strony naciśnij `Alt+Enter`. Przeczytaj całość znakami, słowami i wierszami, przejdź Tabem do listy łączy i otwórz Enterem stronę stacji. Osobno sprawdź adres strumienia, lecz zamknij zewnętrzną aplikację, jeżeli zacznie go odtwarzać.

Oczekiwane: okno nie zawiera Kolejki ani „Odtwarzaj jako następne”. Pokazuje dane stacji, Bibliotekę, Ulubione, dźwięk i łącza. NVDA czyta na liście wyłącznie „Otwórz adres strumienia” i „Otwórz stronę stacji”, bez technicznych rekordów. Enter otwiera wybrane łącze, a kopiowanie całej treści pozostawia okno otwarte.

## Poprzedni zestaw alpha 129

## Nowości alpha 129

### AMC-129-01 — Trójka z podstawowego HLS

W Bibliotece radia wybierz `PR TRÓJKA` z adresem `https://stream13.polskieradio.pl/pr3/pr3.sdp/playlist.m3u8` i rozpocznij odtwarzanie. Po uzyskaniu dźwięku przejdź do innej stacji i wróć do Trójki.

Oczekiwane: AMC najpierw używa właściwego HLS i odbiera dźwięk AAC. Starszy MP3 na porcie 8904 jest wyłącznie próbą awaryjną. Szybka zmiana stacji nie przywraca anulowanego połączenia i nie blokuje okna.

### AMC-129-02 — Menu właściwe dla Radia

Na zwykłej liście radia oraz w odtwarzaczu otwórz kolejno menu główne Widok i Odtwarzanie, menu kontekstowe oraz paletę `Ctrl+Shift+K`. Powtórz po otwarciu wyników `Ctrl+F`.

Oczekiwane w `alpha.129`: w Radiu nie ma Kolejki, „Odtwórz jako następne”, playlist, Albumów, Zakładek, skoków procentowych ani prędkości. Od `alpha.130` `Ctrl+P` i `Ctrl+Shift+P` są świadomie ponownie użyte wyłącznie dla lokalnych presetów radiowych; `Ctrl+Q`, `Shift+Enter` i `Ctrl+Shift+Enter` nadal niczego nie zmieniają i podają zwięzły komunikat o niedostępności.

### AMC-129-03 — Zwięzła pozycja stacji

Przejdź strzałkami po Bibliotece i Ulubionych radia, zwracając uwagę na nazwę elementu i pozycję listy.

Oczekiwane: NVDA czyta nazwę oraz pozycję, np. `PR TRÓJKA, 8 z 48`. Nie dodaje zbędnego słowa `stacja` bezpośrednio przed pozycją. Właściwości `Alt+Enter` nadal mogą podać rodzaj elementu.

### AMC-129-04 — Przeniesienie jednej stacji przez Ctrl+X i Ctrl+V

Otwórz `Ctrl+U`, wybierz stację ze środka Ulubionych i naciśnij `Ctrl+X`. Przejdź do innej stacji zwykłymi strzałkami i naciśnij `Ctrl+V`.

Oczekiwane: pierwszy skrót tylko zapamiętuje stację i wyjaśnia następny krok. Drugi umieszcza ją bezpośrednio przed bieżącą stacją, odświeża listę raz, zachowuje fokus oraz zapisuje porządek po ponownym uruchomieniu. Nie zmienia systemowego schowka plików.

### AMC-129-05 — Przeniesienie zaznaczonej grupy

W Ulubionych zaznacz Shiftem kilka sąsiednich stacji, naciśnij `Ctrl+X`, wybierz pozycję docelową poza blokiem i naciśnij `Ctrl+V`. Powtórz z aktywnym filtrem oraz z celem należącym do przenoszonego bloku.

Oczekiwane: grupa zachowuje wzajemną kolejność i jest przenoszona jednym odświeżeniem. Aktywny filtr wymaga najpierw `Escape`, a wybór celu z przenoszonej grupy niczego nie zmienia i daje jasny komunikat. `Alt+góra/dół` nadal działa jako wariant pojedynczego kroku.

## Poprzedni zestaw alpha 123

## Nowości alpha 123

### AMC-123-01 — Stabilne stacje i szybka zmiana

Uruchom kolejno Jedynkę, Dwójkę, Czwórkę, PR24, Radio Chopin i Radio Emaus. Kilka razy zmień stację szybko, również zanim poprzednia zdąży zagrać.

Oczekiwane: prawidłowe stacje rozpoczynają odbiór po pojawieniu się dźwięku. Anulowane połączenie nie wraca, nie przejmuje odtwarzacza i nie tworzy dodatkowego komunikatu o błędzie. Okno i NVDA pozostają dostępne.

### AMC-123-02 — Chwilowe zerwanie

Podczas działania stacji na kilka sekund rozłącz sieć, po czym ją przywróć bez wybierania innej pozycji. Jeżeli nie chcesz zmieniać sieci całego komputera, ten test można pominąć.

Oczekiwane: AMC najwyżej dwukrotnie próbuje przywrócić odbiór. Po szybkim powrocie sieci radio ponownie gra bez zamykania odtwarzacza i bez utraty istniejącego timeshiftu. Po wyczerpaniu prób pojawia się jeden krótki komunikat, a aplikacja nadal działa.

### AMC-123-03 — Ponowne połączenie podczas nagrywania

Rozpocznij `Ctrl+Alt+R`, spowoduj krótką przerwę połączenia i przywróć sieć. Następnie zakończ nagrywanie.

Oczekiwane: jeżeli stacja wróci w tym samym formacie, powstaje jeden prawidłowo zakończony MP3, a przerwa może zawierać ciszę. Zmiana formatu lub brak powrotu kończy nagranie bez pozostawienia fałszywego gotowego pliku.

### AMC-123-04 — Niedostępna stacja

Spróbuj uruchomić Trójkę, jeżeli jej adres akurat nie odpowiada, a bezpośrednio potem wybierz działającą stację.

Oczekiwane: dwa niedostępne warianty nie są przedstawiane jako działające. AMC nie zapętla prób i nie blokuje przejścia do następnej stacji. Jeżeli nadawca przywrócił już serwer, Trójka może działać normalnie.

## Poprzedni zestaw alpha 114

## Nowości alpha 114

### AMC-114-01 — Placeholder niezależny od nazwy chmury

W Folderach Biblioteki użyj dostępnego pliku tylko online z OneDrive, iCloud, Google Drive albo innego klienta korzystającego z mechanizmu Cloud Files. Najpierw wybierz go i użyj lewej strzałki, potem świadomie rozpocznij odtwarzanie.

Oczekiwane: indeksowanie i krótka informacja nie pobierają zawartości. Program rozpoznaje stan pliku z metadanych Windows, nawet jeśli nazwa folderu nie zawiera nazwy dostawcy. Dopiero odtwarzanie może rozpocząć hydratację, a okno i NVDA pozostają dostępne.

### AMC-114-02 — Ponawianie po awarii usługi chmurowej

Podczas otwierania pliku tylko online chwilowo odłącz sieć albo zatrzymaj klienta chmury. Po błędzie spróbuj natychmiast kilka razy, potem przywróć dostęp i ponów po podanym czasie.

Oczekiwane: AMC nie tworzy wielu równoległych, zablokowanych prób. Krótki komunikat podaje czas do ponowienia, rosnący od około 10 sekund najwyżej do 5 minut. Po udanym otwarciu ograniczenie znika; restart nie jest wymagany.

### AMC-114-03 — MP3 z nietypowym początkiem

Na kopiach materiałów sprawdź MP3 z bardzo dużą okładką lub tagiem ID3, z dodatkowymi bajtami przed dźwiękiem oraz plik zapisany przez nietypowy rejestrator. Jeżeli masz MP3 free-format, dodaj go do próby.

Oczekiwane: aplikacja czyta tylko ograniczony fragment, odnajduje potwierdzone ramki i może ominąć wadliwy początek bez tworzenia nowej kopii pliku. Czas, pozycja i przewijanie pozostają prawidłowe. Ostrzeżenia techniczne nie są wypowiadane przez NVDA.

### AMC-114-04 — MP3 większy niż 512 MiB i 4 GiB

Jeżeli masz bardzo duży lokalny MP3, sprawdź start, kilka odległych skoków, zmianę prędkości oraz Page Up i Page Down. Nie kopiuj pliku specjalnie do katalogu testowego i nie używaj ważnego materiału do eksperymentalnego uszkadzania.

Oczekiwane: limit 512 MiB dotyczy wyłącznie ostatniego awaryjnego NLayer, a nie zwykłego odtwarzania. Ścieżka systemowa i oczyszczony podstrumień używają pozycji 64-bitowych, nie ładują całości do pamięci i nie tworzą tymczasowej kopii.

### AMC-114-05 — Uszkodzone deklaracje kontenerów

Na nieistotnych kopiach sprawdź ucięty WAV, AIFF, FLAC, OGG, M4A lub AAC. Po każdej próbie uruchom od razu prawidłowy plik innego formatu.

Oczekiwane: rozmiar zadeklarowany poza fizycznym końcem jest odnotowany w logu, ale okno nie blokuje się na analizie. Wadliwy tor zostaje odłączony, prawidłowy następny materiał działa, a program nie modyfikuje żadnego źródła.

## Poprzedni zestaw alpha 113

## Nowości alpha 113

### AMC-113-01 — Podstawowa macierz formatów

Odtwórz po jednym zwykłym pliku WAV, FLAC, OGG/Vorbis, M4A lub AAC, WMA, AIFF i MP3. Jeżeli masz, sprawdź również Ogg Opus, WebM albo MKA z dźwiękiem. Dla każdego użyj paska, przewijania, prędkości, pauzy, Page Up lub Page Down i wyjścia Escape.

Oczekiwane: każdy obsługiwany przez bieżący Windows format korzysta z tego samego stabilnego odtwarzacza, czasu, głośności i pamięci pozycji. Brak systemowego kodeka daje krótki błąd bez zamrożenia. NVDA nie odczytuje nazw klas, kontenerów technicznych ani wyjątków.

### AMC-113-02 — WAV, RF64 i długi materiał bez kompresji

Sprawdź dostępny większy WAV, a jeżeli masz nagranie RF64 lub BWF zapisane jako `.wav`, również ten plik. Wykonaj kilka odległych skoków i zmian prędkości.

Oczekiwane: prawidłowy plik otwiera się bez pełnego wczytywania do pamięci. Rozmiar większy niż klasyczna granica RIFF nie powoduje ujemnego czasu ani błędnego końca. Interfejs pozostaje dostępny podczas przygotowania.

### AMC-113-03 — FLAC i duże metadane

Otwórz FLAC z okładką lub dużą liczbą tagów oraz zwykły FLAC bez rozbudowanych metadanych. Przewiń w kilka miejsc, zatrzymaj i uruchom inny format.

Oczekiwane: preflight czyta tylko początek, a Media Foundation obsługuje właściwy strumień. Zmiana pliku odłącza poprzedni tor; późny wynik ani błąd FLAC nie przejmuje ponownie odtwarzacza.

### AMC-113-04 — OGG Vorbis i Ogg Opus

Odtwórz zwykły OGG/Vorbis, w tym wcześniejszy fragment o przesuniętej osi czasu. Następnie, jeżeli masz, otwórz plik Ogg Opus z rozszerzeniem `.opus`, `.oga` albo `.ogg`.

Oczekiwane: Vorbis korzysta z normalizowanej osi czasu i nie zapętla się za końcem. Ogg Opus nie jest błędnie otwierany czytnikiem Vorbis; jest przekazywany do systemu i albo gra, albo zostaje bezpiecznie odrzucony, zależnie od kodeków Windows.

### AMC-113-05 — Pliki ucięte i błędnie nazwane

Na kopiach nieistotnych materiałów przygotuj po jednym uciętym WAV, FLAC i OGG. Możesz też nadać kopii FLAC rozszerzenie `.wav` albo plikowi tekstowemu jedno z rozszerzeń multimedialnych. Nie modyfikuj ważnych oryginałów.

Oczekiwane: AMC może odrzucić plik przy otwieraniu albo po rozpoczęciu dekodowania, ale zawsze zwalnia tor, zachowuje działający fokus i pozwala natychmiast otworzyć inny plik. Ponowienie nie używa pozornie działającego, uszkodzonego dekodera.

### AMC-113-06 — Kontenery z obrazem i ścieżką audio

Jeżeli masz MP4, MOV, 3GP, MKV, WebM, AVI albo WMV ze ścieżką audio, dodaj go przez `Ctrl+O` lub Folder Biblioteki i rozpocznij odtwarzanie.

Oczekiwane: AMC odtwarza wyłącznie dźwięk i zachowuje semantykę elementu multimedialnego. Kontener bez obsługiwanej ścieżki audio kończy się zwięzłym błędem. Wideo nie otwiera dodatkowego okna i nie odbiera fokusu.

### AMC-113-07 — Regresja chmury i bardzo dużego pliku

Powtórz serię odległych skoków w wielogodzinnym pliku z Dysku Google, OneDrive albo iCloud. Następnie przejdź do krótkiego lokalnego WAV, OGG lub FLAC.

Oczekiwane: ograniczone rozpoznanie kontenera nie powoduje pobrania całej zawartości. Chmura nadal ma dłuższe limity, skoki łączą się do ostatniego celu, a lokalny plik uruchamia się na świeżym torze.

## Poprzedni zestaw alpha 112

## Nowości alpha 112

### AMC-112-01 — Wielogodzinny plik z chmury i seria skoków

Otwórz plik `Radio Centrum_23.lut.2026_11.27.42 AM.mp3` z Dysku Google albo jeszcze większy dostępny materiał. Skocz kolejno w kilka odległych miejsc cyframi i `Ctrl+J`, podczas doczytywania przejdź Tabem, odczytaj pasek `NVDA+End`, zmień głośność i wróć Escape.

Oczekiwane: okno, fokus i NVDA pozostają dostępne. Seria skoków kończy się w ostatnim żądanym miejscu. AMC nie wybiera przypadkowego pliku, nie wymaga odzyskiwania fokusu przez Escape i nie blokuje źródła na cały czas działania programu po chwilowym braku sieci.

### AMC-112-02 — Inny dostawca chmury albo udział sieciowy

Jeżeli masz możliwość, użyj pliku tylko online z OneDrive, iCloud, Dropbox, Box, Nextcloud albo udziału `\\serwer\udział`. Rozpocznij odtwarzanie, chwilowo odłącz sieć lub zatrzymaj klienta chmury, a potem przywróć dostęp i spróbuj ponownie.

Oczekiwane: skan Biblioteki nie pobiera całego folderu. Próba odtwarzania może czekać na dostawcę, ale interfejs działa. Po niepowodzeniu komunikat jest krótki i pozbawiony nazw klas lub kodów; po przywróceniu dostępu ten sam plik można ponowić bez restartu AMC.

### AMC-112-03 — Nietypowy lub częściowo uszkodzony MP3

Sprawdź dostępne pliki z bardzo dużym tagiem lub okładką ID3, dodatkowymi danymi przed pierwszą ramką, urwanym końcem albo materiał zapisany przez nietypowy rejestrator. Jeżeli masz plik, który wcześniej zawieszał lub był odrzucany przez system, użyj właśnie jego.

Oczekiwane: prawidłowy MP3 zaczyna grać, ewentualnie po jednej automatycznej próbie dekodera awaryjnego. Uszkodzony plik zostaje odrzucony bez zamrożenia i bez pętli ponowień. Po błędzie od razu można przejść do następnego pliku i używać całego okna.

### AMC-112-04 — Plik tylko udający MP3

Utwórz kopię małego pliku tekstowego albo innego nieaudio i nadaj jej rozszerzenie `.mp3`. Dodaj ją do Biblioteki i spróbuj odtworzyć. Nie używaj ważnego oryginału.

Oczekiwane: AMC nie przestaje odpowiadać, niczego nie modyfikuje i podaje zwięzłą informację o uszkodzonym albo nieobsługiwanym formacie. Szczegóły można znaleźć tylko w `%LocalAppData%\AccessibleMediaController\logs\amc.log`.

### AMC-112-05 — Bardzo długi plik lokalny

Na najdłuższym dostępnym pliku lokalnym, także dłuższym niż doba, sprawdź start, pasek, czas upłynięty, pozostały i całkowity, skok do czasu, cyfry procentowe, zakładki oraz Page Up i Page Down.

Oczekiwane: program akceptuje wiarygodne nagrania znacznie dłuższe niż wcześniejsza granica 30 dni, nie przepełnia obliczeń czasu i nie wczytuje całego pliku do pamięci. Nawigacja pozostaje spójna z kontekstem listy.

### AMC-112-06 — Regresja formatów i Kolejki

Odtwórz krótki zwykły MP3 oraz po jednym dostępnym WAV, FLAC, M4A lub AAC i OGG. Powtórz też dodanie folderu do Kolejki, odtworzenie lub usunięcie jednego pliku i usunięcie pozostałej zawartości folderu.

Oczekiwane: aktualizacja NAudio nie zmienia czasu, wysokości dźwięku, prędkości, paska ani kontynuacji. Częściowy stan folderu w Kolejce nadal działa zgodnie z alfą 110.

## Poprzedni zestaw alpha 111

## Nowości alpha 111

### AMC-111-01 — Wielogodzinny plik z Dysku Google

Otwórz plik `Radio Centrum_23.lut.2026_11.27.42 AM.mp3` o czasie około `8:46:27` z `G:\Dyski współdzielone\Archiwum M\Radio Centrum`. Skocz cyfrą albo `Ctrl+J` w okolice szóstej lub siódmej godziny. Podczas ewentualnego doczytywania użyj Tabu, odczytu paska `NVDA+End` oraz jednego polecenia czasu.

Oczekiwane: dźwięk może chwilę czekać na dane dostawcy, ale AMC, fokus i NVDA nie zamrażają się. Nie trzeba naciskać Escape, aby odzyskać sterowanie. Pasek od razu pokazuje żądany cel.

### AMC-111-02 — Seria szybkich skoków

W tym samym pliku naciśnij szybko kilka razy `Ctrl+strzałka w prawo`, potem `Shift+strzałka w prawo`, a podczas doczytywania wybierz inną cyfrę procentową.

Oczekiwane: interfejs reaguje na każde polecenie, ale dekoder nie tworzy długiej kolejki wszystkich pośrednich operacji. Ostatecznie trafia do ostatniej żądanej pozycji. Komunikaty i pasek nie cofają się kolejno przez stare cele.

### AMC-111-03 — Zmiana elementu podczas doczytywania

Rozpocznij odległy skok w pliku chmurowym, po czym przez Page Up albo Page Down uruchom inny dostępny plik. Możesz też wyjść z odtwarzacza zgodnie z bieżącym ustawieniem Escape.

Oczekiwane: nowy element działa od razu, a spóźnione zakończenie starego skoku nie zmienia jego pozycji, nie odbiera fokusu i nie uruchamia poprzedniego nagrania.

### AMC-111-04 — Zwykły plik lokalny

Na krótszym MP3 zapisanym poza chmurą sprawdź strzałki, cyfry, `Ctrl+J`, zakładki oraz odczyt czasu.

Oczekiwane: wszystkie skoki pozostają szybkie, ich pozycje są dokładne, a odtwarzanie i zakładki nie mają regresji.

### AMC-111-05 — Regresja folderu w Kolejce

Powtórz skrócony scenariusz alpha 110: dodaj folder do Kolejki, odtwórz lub usuń z niej jeden plik, wróć do folderu i usuń jego pozostałą zawartość `Shift+Enter`.

Oczekiwane: folder nadal proponuje usunięcie, dopóki choć jeden jego plik pozostaje w Kolejce albo „odtwarzaj jako następne”.

## Poprzedni zestaw alpha 110

## Nowości alpha 110

### AMC-110-01 — Folder po odtworzeniu jednego pliku z Kolejki

W Folderach dodaj cały folder do Kolejki przez `Shift+Enter`. Otwórz Kolejkę, uruchom jeden z jego plików i usuń ten plik albo inny pojedynczy plik z Kolejki. Wróć do tego folderu i naciśnij `Shift+Enter`.

Oczekiwane: dopóki choć jeden plik tego folderu pozostaje w Kolejce lub w grupie „odtwarzaj jako następne”, menu i polecenie folderu proponują usunięcie. `Shift+Enter` usuwa wszystkie pozostałe pliki folderu z obu grup. Nie dodaje ponownie plików już odtworzonych ani ręcznie usuniętych.

### AMC-110-02 — Ponowne dodanie całkowicie usuniętego folderu

Po wykonaniu poprzedniego zadania ponownie naciśnij `Shift+Enter` na tym samym folderze i otwórz Kolejkę.

Oczekiwane: kiedy żaden plik folderu nie należy już do Kolejki, polecenie ponownie dodaje całą dostępną zawartość folderu.

### AMC-110-03 — Menu kontekstowe przy stanie częściowym

Dodaj folder do Kolejki, usuń z Kolejki jeden jego plik, wróć do folderu i otwórz menu kontekstowe.

Oczekiwane: menu mówi „Usuń zawartość folderu z kolejki”, a nie „Dodaj…”. Analogiczna zasada obowiązuje dla częściowego stanu Ulubionych i „odtwarzaj jako następne”.

### AMC-110-04 — Cofnięcie częściowego usunięcia

W częściowym stanie Kolejki usuń pozostałą zawartość poleceniem folderu, a następnie naciśnij `Ctrl+Z`.

Oczekiwane: jedno cofnięcie odtwarza dokładnie stan sprzed polecenia folderu, bez przywracania wcześniej odtworzonego albo ręcznie usuniętego pliku.

## Poprzedni zestaw alpha 109

## Nowości alpha 109

### AMC-109-01 — Dodanie folderu do Kolejki

W widoku Foldery ustaw fokus na folderze zawierającym pliki i naciśnij `Shift+Enter`. Otwórz Kolejkę i sprawdź jej zawartość. Wróć do tego samego folderu i ponownie naciśnij `Shift+Enter`.

Oczekiwane: pierwsze polecenie dodaje wszystkie dostępne, zaindeksowane pliki z folderu i podfolderów, ale nie dodaje wiersza rodzaju „folder”. Komunikat podaje nazwę folderu i liczbę plików. Drugie polecenie usuwa tę zawartość z Kolejki i również podaje jednoznaczny komunikat.

### AMC-109-02 — Odtwarzaj zawartość jako następną

Na folderze naciśnij `Ctrl+Shift+Enter`, otwórz Kolejkę i sprawdź pierwszą grupę elementów. Powtórz polecenie na folderze.

Oczekiwane: rzeczywiste pliki są oznaczone jako odtwarzane następne w kolejności folderu. Powtórzenie usuwa ten stan. Sam folder nie występuje na liście.

### AMC-109-03 — Ulubione i cofanie całej zmiany

Na folderze naciśnij `Ctrl+Shift+U`, otwórz Ulubione, a następnie naciśnij `Ctrl+Z`.

Oczekiwane: do Ulubionych trafiają pliki, nie folder. Jedno `Ctrl+Z` cofa całą operację, niezależnie od liczby plików.

### AMC-109-04 — Playlisty folderu

Na folderze naciśnij `Ctrl+Shift+P`, wybierz lub utwórz playlistę i zapisz zmiany. Otwórz playlistę.

Oczekiwane: menedżer działa na wszystkich plikach folderu i podfolderów, podaje liczbę plików i nie pokazuje technicznego wiersza folderu jako utworu.

### AMC-109-05 — Folder pusty i Biblioteka

Jeżeli masz widoczny folder bez aktywnych plików, wywołaj na nim polecenie Kolejki. Na zwykłym folderze naciśnij `Ctrl+Shift+L`.

Oczekiwane: pusty folder niczego nie zmienia. `Ctrl+Shift+L` wyjaśnia, że folder już należy do Biblioteki i że przynależność zmienia się na plikach po otwarciu folderu.

### AMC-109-06 — Menu kontekstowe i fokus

Otwórz menu kontekstowe na folderze. Przejdź po poleceniach, wykonaj dodanie do Kolejki, a następnie je cofnij.

Oczekiwane: menu mówi „Otwórz folder” oraz jawnie nazywa działania na zawartości folderu. Po zamknięciu menu i po odświeżeniu fokus pozostaje na tym samym folderze.

## Krótka regresja alpha 109

- Enter otwiera folder, a Backspace wraca do folderu nadrzędnego;
- działanie folderu nie otwiera plików i nie pobiera placeholderów chmurowych;
- zwykły utwór nadal przełącza Kolejkę, Ulubione i „odtwarzaj jako następne” pojedynczo;
- album i kontener playlisty nadal wymagają otwarcia Enterem;
- ochrona dekoderów alpha 108 nadal działa.

## Poprzedni zestaw alpha 108

## Nowości alpha 108

### AMC-108-01 — OGG Emaus i brak zawieszenia interfejsu

Otwórz `Emaus - 2026-08-27 11-30.ogg`, pozostaw odtwarzanie przez co najmniej minutę, odczytuj pasek NVDA+End, przechodź Tabem po odtwarzaczu i użyj kilku skrótów czasu.

Oczekiwane: czas wynosi około `5:06`; AMC i NVDA odpowiadają przez cały test. W logu nie powstaje nowe ostrzeżenie `ui-watchdog` ani `decoder-watchdog` dla prawidłowo odtwarzanego pliku.

### AMC-108-02 — Różne formaty

Otwórz kolejno dostępne pliki MP3, WAV, AAC lub M4A, FLAC, WMA, zwykły OGG i AIFF. Dla każdego sprawdź start, pasek, przewijanie, zmianę prędkości, pauzę i przejście Page Up/Page Down.

Oczekiwane: format obsługiwany przez system zaczyna się bez blokowania okna, pozycja zmienia się prawidłowo i koniec nie tworzy pętli. Format bez zainstalowanego dekodera, w szczególności Opus na obecnej konfiguracji Windows, może zostać odrzucony krótkim błędem, ale AMC i NVDA nadal działają.

### AMC-108-03 — Lewa strzałka i brakujące dane

Na świeżo dodanym lokalnym pliku, którego AMC jeszcze nie odtwarzało, naciśnij lewą strzałkę. Podczas odczytu danych przechodź po innych programach i wróć do AMC.

Oczekiwane: okno nie zawiesza się. Informacja pojawia się po zakończeniu odczytu albo po pięciosekundowym limicie korzysta z już zapisanych danych. Jeżeli przed zakończeniem przejdziesz na inny element, spóźniony komunikat starego pliku nie jest wypowiadany.

### AMC-108-04 — Plik chmurowy

Wybierz niepobrany plik iCloud, OneDrive albo Dysku Google. Najpierw naciśnij lewą strzałkę, a następnie świadomie rozpocznij odtwarzanie.

Oczekiwane: lewa strzałka nie pobiera placeholdera i nie blokuje interfejsu. Dopiero odtwarzanie może rozpocząć pobieranie. Podczas oczekiwania AMC pozostaje dostępne; po niepowodzeniu podaje błąd chmury zamiast nieskończonego oczekiwania.

### AMC-108-05 — Logi watchdogów

Po zwykłym użyciu sprawdź plik `%LocalAppData%\AccessibleMediaController\logs\amc.log` albo przekaż go do analizy po zauważonym problemie.

Oczekiwane: zwykłe odtwarzanie nie tworzy ostrzeżeń. `ui-watchdog` oznacza brak odpowiedzi całego okna przez osiem sekund, `metadata-watchdog` przekroczenie limitu odczytu informacji, a `decoder-watchdog` zatrzymany odczyt konkretnego pliku. Żaden z tych mechanizmów nie usuwa ani nie modyfikuje materiału.

## Krótka regresja alpha 108

- pasek stanu, czas bieżący, pozostały i całkowity są nadal prawidłowe;
- przewijanie, zakładki i wznowienie zachowują rzeczywistą pozycję;
- regulacja prędkości 0,50–2,00 razy nie zmienia wysokości dźwięku;
- `Alt+F4`, Escape oraz F6 zachowują ustalone działanie;
- Pomoc `F1` i `Ctrl+F1` nadal używa zwięzłego kontekstu sesji.

## Poprzedni zestaw alpha 107

## Nowości alpha 107

### AMC-107-01 — Lewa strzałka w Plikach lokalnych

W sesji Pliki lokalne przejdź do Folderów, włącz `Ctrl+F1` i naciśnij strzałkę w lewo.

Oczekiwane: komunikat brzmi w rodzaju „Strzałka w lewo: oznajmia wielkość i bitrate pliku. Kontekst: Pliki lokalne”. Nie mówi ogólnie o krótkich informacjach i nie dodaje słowa „Foldery”.

### AMC-107-02 — Kontekst po zmianie sesji

Włączaj Pomoc klawiatury kolejno w Plikach lokalnych, WiiM, TIDAL i Apple Music. W każdej sesji sprawdź kilka skrótów na różnych widokach.

Oczekiwane: po słowie „Kontekst” występuje tylko użytkowa nazwa aktywnej sesji. Zmiana Biblioteki na Kolejkę, Ulubione, Foldery albo odtwarzacz nie dopisuje nazwy widoku.

### AMC-107-03 — Lewa strzałka poza sesją lokalną

Na liście demonstracyjnej WiiM, TIDAL albo Apple Music włącz `Ctrl+F1` i naciśnij strzałkę w lewo.

Oczekiwane: opis mówi o bitrate i innych dostępnych parametrach elementu. Nie obiecuje wielkości lokalnego pliku, a kontekst zawiera wyłącznie nazwę usługi.

### AMC-107-04 — Spis F1

Otwórz `F1`, wyszukaj „bitrate” i przejdź do wpisu lewej strzałki.

Oczekiwane: wpis opisuje wielkość, bitrate i dostępne parametry, nie zawiera technicznych identyfikatorów i pozostaje tylko opisem — Enter nie wykonuje odczytu ani innego działania.

## Krótka regresja alpha 107

- `Ctrl+F1` nadal przechwytuje i opisuje klawisz bez wykonywania polecenia.
- `Escape` oraz ponowne `Ctrl+F1` wyłączają Pomoc klawiatury.
- Po wyłączeniu Pomocy lewa strzałka rzeczywiście odczytuje dane zaznaczonego elementu.
- Plik Emaus nadal ma około `5:06` i nie zawiesza AMC.

## Poprzedni zestaw alpha 106

## Nowości alpha 106

### AMC-106-01 — Plik Emaus z folderu YouTubeAudio

1. Otwórz plik `Emaus - 2026-08-27 11-30.ogg` z folderu `Sideloads\YouTubeAudio`.
2. Sprawdź pasek odtwarzacza, informacje o czasie i szybką informację pod strzałką w lewo.
3. Pozostaw odtwarzanie przez co najmniej minutę i poruszaj się po przyciskach odtwarzacza.

Oczekiwane: czas wynosi około `5:06`, a nie setki dni. AMC i NVDA pozostają responsywne, nie znika główne okno i proces nie obciąża stale całego rdzenia procesora.

### AMC-106-02 — Przewijanie fragmentu transmisji

W pliku Emaus sprawdź cyfry `0`, `5` i `9`, `Ctrl+J` z wartością `4:30`, strzałki oraz powrót na początek klawiszem Home.

Oczekiwane: wszystkie pozycje liczą się od początku pięciominutowego fragmentu. Skok nie trafia w absolutny czas źródłowej transmisji, nie zawiesza programu i nie wychodzi poza koniec pliku.

### AMC-106-03 — Rzeczywisty koniec i następny element

Przejdź blisko końca pliku Emaus i pozwól mu dojść do końca w odtwarzalnym widoku, na przykład folderze albo playliście z następnym elementem.

Oczekiwane: koniec zostaje rozpoznany jeden raz. Nie powstaje pętla dekodera; dalsze zachowanie wynika z bieżącego kontekstu odtwarzania i zachowuje ustaloną kolejność.

### AMC-106-04 — Zwykłe pliki OGG i pozostałe formaty

Otwórz zwykły plik OGG, na przykład `Nextfest`, oraz po jednym MP3 i AAC. Sprawdź czas, przewijanie, tempo, pauzę i zmianę elementu.

Oczekiwane: zwykłe OGG nadal ma prawidłowy czas i przewijanie, a zmiana czytnika OGG nie powoduje regresji MP3 ani AAC.

### AMC-106-05 — Zamknięcie po odtwarzaniu OGG

Podczas odtwarzania Emaus zamknij AMC przez `Alt+F4`, po czym uruchom je ponownie.

Oczekiwane: pierwsza instancja kończy się, nie pozostawia procesu bez okna, a ponowne uruchomienie odtwarza stan bez automatycznego uruchamiania błędnej pętli.

## Krótka regresja alpha 106

- `F1`, `?` i `Ctrl+F1` nadal działają jak w alpha 105.
- Lista, odtwarzacz, Historia i playlisty zachowują fokus oraz ustalone skróty.
- Szybka informacja o OGG i `Alt+Enter` pokazują ten sam rzeczywisty czas.
- Plik tymczasowy z rozszerzeniem `.converting` nie staje się pozycją audio Biblioteki.

## Poprzedni zestaw alpha 105

## Nowości alpha 105

### AMC-105-01 — Pierwsze otwarcie spisu F1

1. Na zwykłej liście naciśnij `F1`.
2. Sprawdź pierwszą informację oraz fokus.
3. `Tab`, `Shift+Tab`, strzałki, Enter i Escape powinny prowadzić przewidywalnie między wyszukiwaniem, sekcjami, skrótami i przyciskami.
4. Zamknij okno, otwórz je ponownie i sprawdź, czy nie pojawiają się techniczne zapisy w rodzaju nazwy klasy, klamer albo nazw właściwości.

### AMC-105-02 — Sekcje i wyszukiwanie

1. Przejdź po wszystkich sekcjach spisu.
2. Wyszukaj kolejno: `ulubione`, `czas`, `kosz`, `ctrl f1` i tekst bez polskich znaków.
3. Sprawdź, czy wynik podaje kolejno nazwę polecenia, skrót i kontekst.
4. Wyczyść wyszukiwanie i sprawdź powrót pełnej listy sekcji.

### AMC-105-03 — Wykonanie polecenia z Pomocy

1. Wyszukaj bezpieczne polecenie, na przykład „Pokaż kolejkę”.
2. Naciśnij Enter na jego pozycji.
3. Pomoc powinna się zamknąć, a polecenie wykonać tak samo jak z klawiatury lub palety.
4. Otwórz F1 ponownie i naciśnij Enter na informacyjnym opisie, na przykład `Shift+Delete`; nic destrukcyjnego nie może zostać wykonane.

### AMC-105-04 — Pomoc klawiatury na liście

1. Naciśnij `Ctrl+F1`.
2. Sprawdź `Ctrl+U`, `Ctrl+Shift+C`, `Delete`, `Alt+F4` i dowolny nieprzypisany skrót.
3. Każdy klawisz ma zostać opisany wraz z bieżącym kontekstem, ale nie może wykonać działania ani zamknąć programu.
4. Naciśnij `Escape`; tryb ma się wyłączyć.

### AMC-105-05 — Pomoc klawiatury w odtwarzaczu

1. Otwórz odtwarzacz i włącz `Ctrl+F1`.
2. Sprawdź strzałki, `Shift+kropka`, cyfrę, `PageDown`, `B` i klawisz bez przypisania.
3. Upewnij się, że czas, głośność, prędkość, plik i Zakładki nie zmieniają się w czasie opisywania.
4. Wyłącz tryb ponownym `Ctrl+F1` i sprawdź, że zwykłe sterowanie znów działa.

### AMC-105-06 — Wyciszone komunikaty i prefiks

1. Wyłącz zwykłe komunikaty dostępności w Ustawieniach.
2. Włącz i wyłącz Pomoc klawiatury; oba stany nadal muszą być oznajmione.
3. W trybie Pomocy naciśnij skonfigurowany globalny prefiks. Ma zostać opisany, ale nie może otworzyć warstwy.
4. Po wyjściu z Pomocy sprawdź, czy globalny prefiks znowu działa.

### AMC-105-07 — Znak zapytania i pola tekstowe

1. Na liście naciśnij znak `?`; powinien otworzyć spis.
2. Zamknij spis, wejdź do filtra albo wyszukiwania i wpisz `?`.
3. W polu tekstowym znak ma zostać wpisany i nie może otworzyć Pomocy.

## Krótka regresja

- `Ctrl+Shift+K`: paleta nadal ma czytelne pozycje i wykonuje polecenia.
- `Ctrl+H`: Historia nadal działa jak w alpha 104.
- `Ctrl+P` i `Ctrl+Shift+P`: playlisty nadal działają.
- `F1` z odtwarzacza po zamknięciu przywraca fokus do odtwarzacza, a z listy do listy.
- Przyciski Anuluj i Escape w Pomocy niczego nie wykonują.

## Poprzedni zestaw alpha 104

## Nowości alpha 104

### AMC-104-01 — Trwała Historia w odwrotnej kolejności

W sesji Pliki lokalne odtwórz kolejno trzy różne pliki, po czym naciśnij `Ctrl+H`. Zamknij i ponownie uruchom AMC, wróć przez `Ctrl+1`, `Ctrl+H`.

Oczekiwane: Historia odtwarzania istnieje osobno dla tej sesji, jest trwała i pokazuje najpierw plik odtwarzany ostatnio. Ponowne odtworzenie wcześniejszego pliku przenosi go na początek bez tworzenia duplikatu.

### AMC-104-02 — Delete usuwa tylko z Historii

Dodaj plik do Biblioteki, Ulubionych i Kolejki, odtwórz go, a następnie w `Ctrl+H` zaznacz ten wpis i naciśnij `Delete`. Powtórz z kilkoma wpisami zaznaczonymi Shiftem.

Oczekiwane: komunikat mówi o usunięciu z Historii. Wpisy znikają wyłącznie z tego widoku. Pliki pozostają na dysku oraz nadal należą do Biblioteki, Ulubionych i Kolejki. Po ponownym uruchomieniu usunięte wpisy nie wracają. Menu kontekstowe nazywa polecenie **Usuń z Historii odtwarzania**.

### AMC-104-03 — Playlista i pozostałe działania z Historii

Zaznacz kilka wpisów Historii i naciśnij `Ctrl+Shift+P`. Utwórz nową playlistę, zapisz zmiany i sprawdź jej zawartość. Wyrywkowo sprawdź również `Ctrl+C`, `Ctrl+Shift+C`, `Shift+Enter` i zmianę Ulubionych.

Oczekiwane: nowa playlista zawiera wszystkie zaznaczone elementy, a pozostałe polecenia działają zbiorczo tak samo jak na zwykłej liście. `Alt+strzałka w górę/w dół` nie zmienia kolejności Historii, ponieważ pozostaje ona chronologiczna.

### AMC-104-04 — Shift+Delete pozostaje operacją na pliku

Tylko na niepotrzebnej kopii pliku lokalnego wybierz w Historii `Shift+Delete` i przeczytaj ostrzeżenie. Najpierw anuluj, a dopiero w osobnej próbie świadomie potwierdź.

Oczekiwane: program wyraźnie odróżnia usunięcie wpisu `Delete` od fizycznego przeniesienia pliku do Kosza przez `Shift+Delete`. Anulowanie niczego nie zmienia; potwierdzenie usuwa plik z dysku i jego rekordy zgodnie z dotychczasową regułą.

## Poprzedni zestaw alpha 103

## Nowości alpha 103

### AMC-103-01 — Dostępna lista niedostępnych plików

Otwórz `Ctrl+F5`, wybierz Folder Biblioteki mający co najmniej jeden rekord niedostępny i użyj przycisku **Przejrzyj niedostępne…**.

Oczekiwane: otwiera się osobne okno, a fokus i odczyt NVDA trafiają na pierwszy plik. Każdy wiersz zawiera czytelną nazwę i pełną ścieżkę, bez nazw klas, pól `Id`, `Label` ani innych technicznych etykiet. Lista obsługuje zwykłe zaznaczanie Shiftem oraz `Ctrl+A`.

### AMC-103-02 — Anulowanie jest bezpieczne

Zaznacz jeden lub kilka rekordów, naciśnij `Delete` albo przycisk **Zapomnij zaznaczone w AMC…**, a w ostrzeżeniu wybierz **Nie**.

Oczekiwane: komunikat wyjaśnia pełny zakres operacji i zaznacza, że dysk nie zostanie zmieniony. Po anulowaniu rekordy i wszystkie ich dane nadal istnieją.

### AMC-103-03 — Potwierdzone zapomnienie w AMC

Wykonaj próbę na niedostępnym rekordzie testowym, który można bezpiecznie usunąć z danych AMC. Potwierdź **Tak**, zamknij listę i ponownie odczytaj licznik wybranego folderu.

Oczekiwane: rekord znika z listy, a liczba niedostępnych maleje. AMC usuwa jego powiązania z Biblioteką, Ulubionymi, Kolejką, playlistami, Historią, Zakładkami i zapamiętaną pozycją. Nie usuwa ani nie przenosi żadnego pliku na dysku. Jeżeli ten sam plik później wróci do widocznej ścieżki, synchronizacja traktuje go jak nowy rekord.

### AMC-103-04 — Plik chmurowy tylko online

Jeżeli masz w iCloud, OneDrive albo Google Drive plik widoczny jako placeholder, lecz niepobrany lokalnie, odśwież folder przez `F5` i otwórz listę niedostępnych.

Oczekiwane: widoczny dla Windows placeholder pozostaje aktywnym plikiem Biblioteki i nie jest proponowany do zapomnienia. AMC nie pobiera całego folderu tylko po to, by utworzyć indeks.

## Poprzedni zestaw alpha 102

## Nowości alpha 102

### AMC-102-01 — Pierwszy folder odczytany po Ctrl+F5

Na lokalnej liście naciśnij `Ctrl+F5` i niczego więcej nie naciskaj.

Oczekiwane: fokus znajduje się na pierwszym zaznaczonym Folderze Biblioteki. NVDA odczytuje jego pełną etykietę i pozycję na liście, a nie tylko komunikat „Foldery Biblioteki, lista”. Strzałka w dół przechodzi od razu do drugiego folderu.

### AMC-102-02 — Czytelne znaczenie liczników

Przejdź po Folderach Biblioteki i przeczytaj tekst objaśniający oraz etykietę folderu.

Oczekiwane: stan korzenia brzmi jednoznacznie „folder dostępny” albo „folder niedostępny — rekordy zachowane”. Objaśnienie rozróżnia aktywne, niedostępne i wykluczone pliki. Plik online widoczny w systemie pozostaje aktywny nawet wtedy, gdy treść zostanie pobrana dopiero przy odtwarzaniu.

## Poprzedni zestaw alpha 101

## Nowości alpha 101

### AMC-101-01 — Foldery Biblioteki pod Ctrl+F5

Na lokalnej liście naciśnij `Ctrl+F5`, a następnie przejdź po oknie strzałkami i Tabem.

Oczekiwane: tytuł, nagłówek, lista i menu używają nazwy „Foldery Biblioteki”, bez określenia „źródła Biblioteki” i bez technicznych reprezentacji obiektów. Pierwszy fokus znajduje się na liście folderów, a Escape zamyka okno i wraca do głównej listy.

### AMC-101-02 — Trzy warianty dla wybranego folderu

W `Ctrl+F5` wybierz folder, przejdź do pola kombi **Dla wybranego folderu** i przeczytaj wszystkie pozycje. Zapisz kolejno każdą z nich.

Oczekiwane: pole zawiera „Pamiętaj pozycję odtwarzania”, „Zawsze od początku” oraz „Zgodnie z ustawieniem globalnym”. Po zapisie komunikat podaje nazwę folderu i wybraną wartość, fokus wraca do pola kombi, a ustawienie pozostaje wybrane po ponownym otwarciu okna.

### AMC-101-03 — Ustawienia globalne i Alt+Shift+Enter

Otwórz Ustawienia ogólne i znajdź opcję **Pamiętaj pozycję odtwarzania lokalnych plików**. Następnie anuluj Ustawienia i wywołaj `Alt+Shift+Enter` kolejno na pliku i folderze.

Oczekiwane: Ustawienia jasno określają opcję jako globalną. W opcjach elementu oraz folderu występują te same krótkie warianty „Pamiętaj pozycję odtwarzania” i „Zawsze od początku”. Wariant dziedziczenia dokładnie wskazuje ustawienie folderu, folderu nadrzędnego lub globalne, zależnie od wybranego obiektu; NVDA nie czyta nazw `ResumeChoice`, `Value` ani `Label`.

### AMC-101-04 — Paleta poleceń i F5

Otwórz `Ctrl+Shift+K`, wyszukaj „foldery biblioteki” i wykonaj znalezione polecenie. Potem zamknij okno i naciśnij `F5`.

Oczekiwane: paleta pokazuje „Foldery Biblioteki, Ctrl+F5”, a osobne polecenie nazywa się „Odśwież foldery Biblioteki, F5”. Odświeżenie mówi o folderach, nie o źródłach.

## Poprzedni zestaw alpha 100

## Nowości alpha 100

### AMC-100-01 — Plik przeniesiony z Kolejki

Dodaj do Kolejki co najmniej dwa pliki i uruchom pierwszy bezpośrednio z widoku `Ctrl+Q`. Przełącz się do Total Commandera, przenieś odtwarzany plik do innego folderu, wróć do AMC i naciśnij Spację.

Oczekiwane: AMC informuje o niedostępności pliku i wybiera następny element z Kolejki. Nie uruchamia niczego samoczynnie. Po Spacji gra następna pozycja Kolejki, a nie pierwszy plik folderu ani Biblioteki.

### AMC-100-02 — Jedyny plik Kolejki

Pozostaw w Kolejce tylko jeden plik, uruchom go z `Ctrl+Q`, a potem przenieś poza AMC w Total Commanderze. Wróć do programu i naciśnij Spację.

Oczekiwane: program mówi, że w bieżącym widoku nie ma następnego elementu. Spacja nie uruchamia przypadkowego pliku. Odtwarzacz wraca do listy, z której można świadomie wybrać inny materiał.

### AMC-100-03 — Plik uruchomiony z folderu

Otwórz folder zawierający co najmniej trzy pliki, uruchom środkowy, a potem przenieś go poza AMC. Wróć do programu i naciśnij Spację.

Oczekiwane: wybrany zostaje kolejny plik z tego samego widoku folderu. Nie jest wybierany pierwszy element płaskiej Biblioteki, Kolejki ani innego folderu.

### AMC-100-04 — Automatyczne wejście i powrót z Kolejki

Uruchom plik z folderu, mając co najmniej jedną pozycję w Kolejce. Po automatycznym wejściu do Kolejki przenieś bieżący plik poza AMC. Sprawdź zachowanie z jeszcze jedną pozycją Kolejki oraz bez niej.

Oczekiwane: najpierw wybierana jest kolejna dostępna pozycja Kolejki. Gdy Kolejka jest wyczerpana, AMC wraca do następnego elementu wcześniejszego widoku folderu. Program nie przechodzi na początek całej Biblioteki.

### AMC-100-05 — Delete i Shift+Delete w różnych widokach

Powtórz usunięcie bieżącego pliku przez Delete oraz fizyczne Shift+Delete po uruchomieniu go kolejno z Ulubionych, playlisty, albumu i płaskiej Biblioteki. Wystarczą krótkie próby na kopiach plików przeznaczonych do usunięcia.

Oczekiwane: każda metoda używa tej samej reguły co przeniesienie w Total Commanderze. Następca pochodzi dokładnie z widoku, z którego uruchomiono usunięty plik; przy braku następcy Spacja nie uruchamia elementu z innej listy.

### AMC-100-06 — Relacyjny komunikat ręcznego przenoszenia

W Kolejności własnej, Ulubionych, Kolejce i otwartej playliście wybierz pojedynczy element i użyj `Alt+strzałki w górę`, a następnie `Alt+strzałki w dół`. Powtórz z ciągłym blokiem dwóch lub trzech pozycji.

Oczekiwane: NVDA mówi odpowiednio „Przeniesiono w górę, nad [tytuł sąsiada]” albo „Przeniesiono w dół, pod [tytuł sąsiada]”, po czym odczytuje przeniesiony element. Dla bloku podawana jest również liczba przeniesionych elementów. Fokus i całe zaznaczenie pozostają na przeniesionych pozycjach.

## Poprzedni zestaw alpha 99

## Nowości alpha 99

### AMC-099-01 — Czytelne części jednej Kolejki

Oznacz dwa pliki przez `Ctrl+Shift+Enter` jako „Odtwórz jako następne”, a dwa inne dodaj przez `Shift+Enter` do zwykłej Kolejki. Naciśnij `Ctrl+Q` i przejdź po wszystkich wierszach.

Oczekiwane: pierwszy komunikat podaje liczbę pozycji jako następne i pozostałych. Każda pozycja priorytetowa zaczyna się od „Następny”, zwykłe pozycje nie otrzymują zbędnego powtórzenia słowa „Kolejka”. Wszystko pozostaje jedną listą.

### AMC-099-02 — Page Up i Page Down po jawnym otwarciu Kolejki

W `Ctrl+Q` uruchom pierwszą pozycję Enterem. W odtwarzaczu przejdź kilka razy przez Page Down, Page Up i ponownie Page Down.

Oczekiwane: oba klawisze poruszają się wyłącznie po kolejności widocznej wcześniej w Kolejce. Można wrócić do już odtworzonego elementu. Żaden pominięty ani wcześniej odtworzony plik nie uruchamia się później drugi raz samoczynnie.

### AMC-099-03 — Automatyczne wejście do Kolejki

Uruchom plik z Biblioteki, mając przygotowane pozycje „Odtwórz jako następne” i zwykłą Kolejkę. Pozwól pierwszemu plikowi się zakończyć, a po automatycznym rozpoczęciu pozycji Kolejki użyj Page Down i Page Up.

Oczekiwane: Page Down przechodzi do następnej pozycji Kolejki, a Page Up wraca do poprzedniej pozycji Kolejki — nie do sąsiada z wcześniejszej Biblioteki. Po wykorzystaniu Kolejki automatyczna kontynuacja wraca do elementu następującego po pierwotnym pliku w Bibliotece.

### AMC-099-04 — Element odtwarzany nie oczekuje drugi raz

Uruchom pozycję bezpośrednio z `Ctrl+Q`, wróć Escape do listy i ponownie otwórz Kolejkę.

Oczekiwane: bieżący element nie pozostaje jednocześnie na liście oczekujących. Pozostałe elementy zachowują porządek, a Page Up w odtwarzaczu nadal może wrócić do wcześniejszej pozycji dzięki historii chwilowego kontekstu.

## Poprzedni zestaw alpha 98

## Nowości alpha 98

### AMC-098-01 — Ręczna kolejność Kolejki

Dodaj do Kolejki co najmniej cztery różne pliki, otwórz ją przez `Ctrl+Q`, zaznacz środkowy element i użyj `Alt+strzałka w górę` oraz `Alt+strzałka w dół`. Powtórz z dwoma sąsiednimi elementami zaznaczonymi Shiftem.

Oczekiwane: pojedynczy element i cały blok zmieniają położenie bez utraty zaznaczenia. NVDA podaje nazwę elementu oraz nowe miejsce. Żaden plik nie znika z Kolejki ani z dysku.

### AMC-098-02 — Odtwórz jako następne i zwykła Kolejka

W jednej Kolejce przygotuj co najmniej dwa elementy przez `Ctrl+Shift+Enter` jako „Odtwórz jako następne” oraz dwa przez `Shift+Enter` jako zwykłą Kolejkę. Spróbuj przenosić elementy wewnątrz obu grup, a następnie zaznacz blok obejmujący obie grupy i użyj `Alt+strzałki`.

Oczekiwane: elementy „Odtwórz jako następne” są na początku. Można porządkować każdą grupę osobno. Dla bloku mieszanego program niczego nie zmienia i mówi, że obie grupy należy przenosić osobno.

### AMC-098-03 — Rzeczywista kolejność odtwarzania

Uruchom plik, który nie należy do przygotowanej Kolejki, a następnie doprowadź go do końca albo użyj krótkich plików testowych. Obserwuj przechodzenie przez wszystkie elementy Kolejki.

Oczekiwane: najpierw odtwarzają się pozycje „Odtwórz jako następne” w kolejności widocznej na liście, następnie zwykła Kolejka także w widocznej kolejności. Zużyty element znika z Kolejki i nie jest powtarzany.

### AMC-098-04 — Cofanie dokładnej pozycji

Przenieś element ze środka Kolejki, naciśnij `Ctrl+Z`, a potem dodaj albo usuń element przez `Shift+Enter` i ponownie użyj `Ctrl+Z`.

Oczekiwane: pierwsze cofnięcie odtwarza dokładne położenie. Drugie przywraca zarówno przynależność, jak i poprzednie miejsce elementu, a fokus pozostaje na właściwym wierszu.

### AMC-098-05 — Restart i odświeżenie Biblioteki

Ustaw własną Kolejkę, zamknij i ponownie uruchom AMC. Następnie naciśnij `F5` w Plikach lokalnych i ponownie otwórz `Ctrl+Q`.

Oczekiwane: układ Kolejki pozostaje identyczny po restarcie oraz po odświeżeniu folderów. Nie wraca do kolejności alfabetycznej ani do kolejności katalogu Biblioteki.

### AMC-098-06 — Osobna Kolejka każdej sesji

Ustaw inną kolejność w Plikach lokalnych i w jednej sesji demonstracyjnej, przełączając sesje przez `Ctrl+1–9` i otwierając `Ctrl+Q`.

Oczekiwane: każda sesja zachowuje własne elementy i własną kolejność. Przenoszenie w jednej usłudze nie zmienia drugiej.

## Poprzedni zestaw alpha 97

## Nowości alpha 97

### AMC-097-01 — Tworzenie i otwieranie playlisty

Naciśnij `Ctrl+P`. Na liście Playlisty użyj Insert, wpisz własną nazwę i zatwierdź. Sprawdź F2, Enter oraz powrót przez Escape i Backspace.

Oczekiwane: NVDA czyta zwykłą nazwę playlisty, liczbę dostępnych elementów i ewentualny czas, bez nazw klas ani technicznych identyfikatorów. F2 zmienia nazwę, Enter otwiera zawartość, a Escape lub Backspace wraca na tę samą playlistę.

### AMC-097-02 — Jedna pozycja, wiele pozycji i stan mieszany

Na zwykłej liście zaznacz jeden plik i użyj `Ctrl+Shift+P`. Utwórz playlistę lub zaznacz istniejącą Spacją i zapisz Enterem. Następnie zaznacz Shiftem kilka plików, z których tylko część już należy do tej playlisty, i ponownie użyj `Ctrl+Shift+P`.

Oczekiwane: lista oznajmia „zaznaczona”, „niezaznaczona” albo „stan mieszany”. Spacja ze stanu mieszanego dodaje cały blok, ponowna Spacja usuwa cały blok. Escape anuluje zmiany, a fokus wraca do wcześniejszego elementu listy.

### AMC-097-03 — Filtr i zarządzanie w oknie playlist

W menedżerze `Ctrl+Shift+P` naciśnij `Ctrl+K`, wpisz część nazwy, następnie Escape. Sprawdź też Insert lub `Ctrl+N`, F2 i Delete.

Oczekiwane: filtr zawęża wyłącznie listę playlist; pierwszy Escape czyści niepusty filtr i wraca na listę, następny anuluje okno. Tworzenie i zmiana nazwy nie gubią fokusu. Usunięcie playlisty wymaga potwierdzenia i jasno mówi, że pliki pozostają bez zmian.

### AMC-097-04 — Kolejność, odtwarzanie i usuwanie elementu

Otwórz playlistę mającą co najmniej trzy utwory. Przesuń środkowy element przez `Alt+strzałka w górę/w dół`, uruchom go i sprawdź Page Up, Page Down oraz naturalny koniec pliku. Wróć do listy i naciśnij Delete na jednym utworze.

Oczekiwane: kolejność jest trwała, a odtwarzanie pozostaje w playliście. Delete usuwa tylko odwołanie z playlisty, nie plik z Biblioteki ani z dysku.

### AMC-097-05 — Cofanie i trwałość

Kolejno zmień nazwę playlisty, jej kolejność albo przynależność elementu i po każdej czynności użyj `Ctrl+Z`. Następnie pozostaw playlistę ze zmianami, zamknij AMC i uruchom ponownie.

Oczekiwane: każde cofnięcie odtwarza właściwy poprzedni stan i pozycję. Po restarcie pozostają nazwy, członkostwo i kolejność. Pełny eksport `.amcbackup.json` zawiera playlisty.

### AMC-097-06 — Playlisty z wyników wyszukiwania

Wyszukaj lokalny plik przez `Ctrl+F` albo `Ctrl+Shift+F`, wybierz jeden wynik lub zaznacz kilka wyników tej samej sesji i naciśnij `Ctrl+Shift+P`.

Oczekiwane: okno wyszukiwania zamyka się, otwiera się menedżer playlist właściwej sesji, a zapis przypisuje wybrane wyniki. Zaznaczenie wyników z różnych sesji pozostawia wyszukiwanie otwarte i prosi o wybranie jednej usługi; nic nie zostaje omyłkowo dodane.

## Poprzedni zestaw alpha 96

## Nowości alpha 96

### AMC-096-01 — Dokładnie zgłoszony przypadek w Ulubionych

Otwórz Ulubione, wybierz element ze środka listy, zapamiętaj jego bezpośrednich sąsiadów, naciśnij `Delete`, a potem `Ctrl+Z`.

Oczekiwane: element wraca pomiędzy tych samych sąsiadów, jest zaznaczony i NVDA nie podaje go jako ostatniego elementu listy.

### AMC-096-02 — Kolejność własna Biblioteki

Otwórz `Alt+3`, wybierz element ze środka, naciśnij `Delete` i `Ctrl+Z`. Powtórz z dwoma sąsiednimi elementami zaznaczonymi Shiftem.

Oczekiwane: pojedynczy element albo cały blok wraca na dokładne wcześniejsze miejsce i zachowuje kolejność.

### AMC-096-03 — Polecenie dodaj lub usuń z ulubionych

Na elemencie należącym do Ulubionych użyj polecenia dodaj lub usuń z ulubionych, następnie `Ctrl+Z` i ponownie otwórz Ulubione.

Oczekiwane: cofnięcie przywraca nie tylko stan Ulubiony, lecz także poprzednią pozycję elementu.

## Poprzedni zestaw alpha 95

## Nowości alpha 95

### AMC-095-01 — Jedno usunięcie i Ctrl+Z

Otwórz `Alt+3`, wybierz plik ze środka Kolejności własnej, naciśnij `Delete`, a następnie `Ctrl+Z`.

Oczekiwane: plik wraca dokładnie pomiędzy tych samych sąsiadów, jest zaznaczony i nie ląduje na końcu listy.

### AMC-095-02 — Zaznaczony blok

W Kolejności własnej zaznacz Shiftem dwa lub trzy sąsiednie pliki, usuń je i cofnij jednym `Ctrl+Z`.

Oczekiwane: cały blok wraca na poprzednią pozycję i zachowuje wewnętrzną kolejność.

### AMC-095-03 — Foldery, alfabet i nowy plik

Powtórz usunięcie i cofnięcie w `Alt+1` oraz `Alt+2`. Następnie dodaj rzeczywiście nowy plik i przejdź do `Alt+3`.

Oczekiwane: w Folderach plik wraca do właściwego folderu, we Wszystkich plikach do miejsca alfabetycznego, a jedynie nowy plik pojawia się na końcu Kolejności własnej.

## Poprzedni zestaw alpha 94

## Nowości alpha 94

### AMC-094-01 — Ctrl+Shift+C na liście i w odtwarzaczu

Na lokalnej liście zaznacz jeden plik i naciśnij `Ctrl+Shift+C`. Powtórz próbę z kilkoma plikami, a następnie w otwartym odtwarzaczu. Wklej do edytora tekstowego i do pustego folderu w Eksploratorze albo Total Commanderze.

Oczekiwane: za każdym razem pojawia się komunikat o skopiowaniu, tekst zawiera pełne ścieżki, a menedżer plików otrzymuje fizyczne pliki. Skrót nie przestaje działać po szybkim powtórzeniu.

### AMC-094-02 — Rozróżnienie konfliktu schowka

Jeżeli kopiowanie znowu zawiedzie, niczego nie zamykaj i od razu zapisz, co powiedział NVDA. Log `%LocalAppData%\AccessibleMediaController\logs\amc.log` powinien zawierać sekcję `clipboard`.

Oczekiwane: wpis „Polecenie Ctrl+Shift+C” dowodzi, że klawisz dotarł do AMC. Kolejny wpis mówi o udanym zapisie albo podaje wyjątek Windows po sześciu próbach. Jeżeli nie ma pierwszego wpisu, kombinację przejęła zewnętrzna aplikacja lub globalny dodatek, zanim dotarła do okna AMC.

### AMC-094-03 — Pola tekstowe i regresja

W polu filtra albo wyszukiwania wpisz tekst i użyj standardowych poleceń schowka. Następnie sprawdź zwykłe `Ctrl+C`, szybkie informacje, odtwarzanie pliku lokalnego oraz Bibliotekę SQLite.

Oczekiwane: niski mechanizm skrótu nie przejmuje klawiszy w polach tekstowych. Pozostałe funkcje alpha 93 działają bez regresji.

## Poprzedni zestaw alpha 93

## Nowości alpha 93

### AMC-093-01 — Jednorazowa migracja Biblioteki do SQLite

Uruchom alpha 93 na dotychczasowych danych. Sprawdź liczbę plików i źródeł oraz kilka pozycji w Bibliotece, Ulubionych, Kolejce, Historii i Zakładkach. Zamknij AMC, uruchom ponownie i sprawdź te same elementy.

Oczekiwane: pierwsze uruchomienie może potrwać chwilę, lecz nic nie znika i nie powstają duplikaty. Drugie uruchomienie odczytuje ten sam stan. W `%LocalAppData%\AccessibleMediaController` istnieje `library.db`, a w `%AppData%\AccessibleMediaController` pozostaje jednorazowa kopia `state.pre-sqlite-migration.json`.

### AMC-093-02 — Indeksowanie chmury bez pobierania

W **Folderach Biblioteki** odśwież przez `F5` folder zawierający pliki dostępne tylko online. Przejdź po nim przez Foldery, Wszystkie pliki i Albumy; użyj lewej strzałki oraz `Alt+Enter`, ale nie uruchamiaj odtwarzania.

Oczekiwane: pliki są widoczne, interfejs i NVDA pozostają responsywne, a dostawca chmury nie rozpoczyna pobierania plików ani całego folderu. Szybka informacja i właściwości określają element jako plik w chmurze zamiast wymuszać odczyt nagłówka.

### AMC-093-03 — Jawne pobranie jednego pliku

Na pliku dostępnym tylko online naciśnij Enter. Podczas pobierania użyj Tabu, menu, odczytu tytułu okna i zwykłych skrótów; w osobnej próbie anuluj przez Escape albo wybierz inny lokalny utwór.

Oczekiwane: AMC mówi „Pobieranie z chmury” i pozostaje dostępny. Pobierany jest tylko świadomie wybrany plik. Po ukończeniu zaczyna się odtwarzanie; po anulowaniu lub zmianie elementu spóźnione poprzednie żądanie nie może rozpocząć dźwięku.

### AMC-093-04 — Brak sieci, timeout i powrót do lokalnego pliku

Jeżeli możesz bezpiecznie zasymulować niedostępną chmurę, spróbuj otworzyć placeholder iCloud, OneDrive albo Google Drive. Nie czekaj na wynik, jeśli nie chcesz wykonywać pełnej próby limitu dwóch minut; sprawdź od razu anulowanie i uruchom lokalny plik.

Oczekiwane: brak sieci lub przekroczenie czasu daje pojedynczy zrozumiały błąd, nie zawiesza okna i nie zamyka AMC. Lokalny plik można następnie odtworzyć normalnie.

### AMC-093-05 — Log diagnostyczny i regresja audio

Odtwórz kolejno dwa lub trzy lokalne pliki, użyj Page Up/Page Down, przewijania, zmiany prędkości, pauzy i zamknięcia programu. Sprawdź `%LocalAppData%\AccessibleMediaController\logs\amc.log`.

Oczekiwane: wszystkie dotychczasowe funkcje audio i komunikaty NVDA działają bez regresji. Log zawiera start programu oraz etapy żądania i rozpoczęcia odtwarzania, ale nie zawiera treści plików. Pojedynczy log nie rośnie powyżej około 5 MB, a archiwów jest najwyżej cztery oprócz bieżącego.

## Poprzedni zestaw alpha 92

## Nowości alpha 92

### AMC-092-01 — Reguła pozycji bez nazw technicznych

Naciśnij `Alt+Shift+Enter` kolejno na pliku, folderze i albumie. Rozwiń pole pozycji i przejdź strzałkami po wszystkich wariantach.

Oczekiwane: NVDA czyta wyłącznie pełne etykiety, np. „Zgodnie z folderem nadrzędnym lub ustawieniem globalnym” oraz „Pamiętaj pozycję odtwarzania”. Nie pojawiają się `ResumeChoice`, `Value`, `Label`, nawiasy klamrowe ani nazwy enumów.

### AMC-092-02 — Prędkość i fokus

W tym samym oknie przejdź do pola prędkości, odczytaj wszystkie wartości, zapisz jedną, ponownie otwórz okno i anuluj.

Oczekiwane: NVDA czyta tylko „Według…” albo wartość typu „1,25 razy”. Zapisana pozycja jest zaznaczona po ponownym otwarciu, a Zapisz i Anuluj przywracają fokus do właściwego elementu listy lub odtwarzacza.

## Poprzedni zestaw alpha 91

## Nowości alpha 91

### AMC-091-01 — Szybkie powtórzenie Ctrl+Shift+C

Na istniejącym pliku lokalnym naciśnij szybko dwa albo trzy razy `Ctrl+Shift+C`. Wklej wynik najpierw do edytora tekstowego, a potem do pustego folderu w Eksploratorze lub Total Commanderze.

Oczekiwane: AMC za każdym razem pozostaje responsywny, podaje sukces dopiero po zapisaniu schowka, tekst zawiera pełną ścieżkę, a menedżer plików otrzymuje prawdziwy plik. Fokus i zaznaczenie nie zmieniają się.

### AMC-091-02 — Nazwy i wiele zaznaczonych plików

Zaznacz Shiftem kilka plików. Naciśnij szybko dwukrotnie `Ctrl+C`, sprawdź wszystkie nazwy w edytorze, a następnie powtórz próbę z `Ctrl+Shift+C` i wklejeniem plików.

Oczekiwane: żaden drugi skrót nie wyłącza dalszego kopiowania. `Ctrl+C` daje wszystkie nazwy w osobnych wierszach, a `Ctrl+Shift+C` wszystkie ścieżki oraz wszystkie istniejące pliki.

### AMC-091-03 — Wyszukiwanie, właściwości i menedżer schowka

Powtórz kopiowanie z wyników `Ctrl+F`, z globalnych wyników oraz przyciskiem „Kopiuj wszystko” w `Alt+Enter`. Jeśli używasz Ditto lub podobnego programu, pozostaw go włączonego.

Oczekiwane: wszystkie miejsca mają tę samą odporność. Chwilowe zajęcie schowka powoduje krótkie ponowienie, a trwałe zajęcie daje komunikat „Schowek jest zajęty przez inną aplikację” z kodem, bez zawieszenia AMC.

## Poprzedni zestaw alpha 90

## Nowości alpha 90

### AMC-090-01 — Opcje pojedynczego pliku

W Folderach, Wszystkich plikach i Ulubionych wybierz ten sam plik lokalny i naciśnij `Alt+Shift+Enter`. Zmień pamiętanie pozycji lub prędkość, zapisz, ponownie otwórz opcje i uruchom plik.

Oczekiwane: za każdym razem otwiera się okno ustawień tego pliku; nie pojawia się komunikat „Nie można odnaleźć ustawień tego pliku w Bibliotece”. Wartość jest zachowana po zmianie widoku i ponownym uruchomieniu AMC.

### AMC-090-02 — Opcje folderu i albumu

W widoku Foldery zaznacz Folder Biblioteki, potem zwykły podfolder, a w widoku Albumy zaznacz album. Na każdym użyj `Alt+Shift+Enter`, ustaw inną prędkość albo regułę pozycji i zapisz.

Oczekiwane: otwiera się dostępne okno „Opcje odtwarzania folderu”. Ustawienie obejmuje pliki znajdujące się poniżej wybranego folderu lub w albumie, ale nie zmienia plików ani układu katalogów.

### AMC-090-03 — Hierarchia nadpisań

Ustaw prędkość dla folderu nadrzędnego, inną dla jego podfolderu i trzecią dla jednego pliku. Odtwórz kolejno plik z nadpisaniem, drugi plik z podfolderu oraz plik tylko z folderu nadrzędnego. Następnie wybierz „według…” na jednym z poziomów.

Oczekiwane: obowiązuje kolejność `plik > najbliższy folder > folder nadrzędny > sesja`. Wybranie dziedziczenia usuwa tylko bieżące nadpisanie i odsłania wartość wyższego poziomu.

### AMC-090-04 — Fokus, zapis i regresja Backspace

Anuluj i zapisz opcje z pliku oraz folderu, używając klawiatury i NVDA. Po zamknięciu okna sprawdź fokus. Następnie użyj `Backspace` i `Delete` na bezpiecznych elementach.

Oczekiwane: fokus wraca na ten sam wiersz listy, program nie zaczyna sam odtwarzać, `Backspace` przechodzi do rodzica i niczego nie usuwa, a `Delete` zachowuje swoje dotychczasowe znaczenie.

## Poprzedni zestaw alpha 89

## Nowości alpha 89

### AMC-089-01 — Ręczna kolejność Ulubionych

Otwórz `Ctrl+U`, zaznacz jeden element, a potem ciągły blok dwóch elementów i użyj `Alt+strzałka w górę/w dół`. Przejdź do innego widoku, wróć do Ulubionych i uruchom AMC ponownie.

Oczekiwane: element lub blok przesuwa się o jeden wiersz, fokus i zaznaczenie zostają zachowane, a kolejność przetrwa zmianę widoku i restart. Przy aktywnym filtrze przesuwanie jest zablokowane jasnym komunikatem.

### AMC-089-02 — Ulubione jako kontekst odtwarzania

W Ulubionych uruchom element, otwórz odtwarzacz i użyj `Page Up` oraz `Page Down`. Pozwól też jednemu krótkiemu plikowi zakończyć się naturalnie.

Oczekiwane: poprzedni, następny i automatycznie uruchomiony element pochodzą z kolejności Ulubionych, a nie z pierwotnego folderu ani płaskiej Biblioteki. Na początku i końcu lista nie zapętla się.

### AMC-089-03 — Kontekst albumu, folderu i kolejności własnej

Powtórz nawigację `Page Up/Page Down` po uruchomieniu elementu kolejno z otwartego Albumu, konkretnego Folderu i `Alt+3`. Po uruchomieniu utworu przejdź bez odtwarzania do innego widoku, wróć `F6` i wybierz następny plik.

Oczekiwane: każdy start ustanawia kolejność bieżącej nieprzefiltrowanej listy. Samo przeglądanie innego widoku nie zmienia kontekstu. Filtr `Ctrl+K` nie ogranicza odtwarzania do chwilowych wyników.

### AMC-089-04 — Kolejka ma pierwszeństwo i wraca do listy

Uruchom element ze środka Ulubionych albo albumu, dodaj inny element jako następny lub do Kolejki i pozwól obu zakończyć się kolejno.

Oczekiwane: najpierw odtwarza się element jawnie dodany do Kolejki, a potem AMC wraca do elementu następującego po pierwotnym utworze w zapamiętanym kontekście. Element kolejki nie jest odtwarzany drugi raz.

### AMC-089-05 — Opcje elementu Alt+Shift+Enter

Na lokalnym pliku naciśnij `Alt+Shift+Enter`. Ustaw „Zawsze od początku” oraz prędkość inną niż sesji, zapisz i odtwórz plik. Przejdź do innego pliku i wróć. Następnie zmień wariant na „Pamiętaj pozycję odtwarzania”, zatrzymaj materiał w środku i uruchom program ponownie.

Oczekiwane: okno ma zwykłe dostępne pola, Zapisz i Anuluj, a fokus wraca do listy lub odtwarzacza. Prędkość nadpisania działa tylko dla wybranego pliku, inny plik wraca do prędkości sesji. Reguła od początku nie wznawia pozycji; reguła pamiętania ją zachowuje.

### AMC-089-06 — Informacje i przyszłe wyjście audio

Po zapisaniu opcji naciśnij `Alt+Enter`, przeczytaj sekcję „W aplikacji”, a następnie ponownie otwórz `Alt+Shift+Enter`.

Oczekiwane: `Alt+Enter` pozostaje tekstem tylko do odczytu i podaje skuteczną regułę wznawiania, prędkość elementu oraz domyślne wyjście współdzielone. W opcjach urządzenie jest jednoznacznie nieaktywne, a okno nie udaje, że zmiana wyjścia lub EQ jest już zaimplementowana.

### AMC-089-07 — Alias tytułu i kolejność albumu

W albumie z plikami `01`, `02` i `03` zmień przez `F2` nazwy widoczne w Bibliotece tak, aby nie zaczynały się cyframi. Otwórz album ponownie i przejdź po nim także przez `Page Down`.

Oczekiwane: NVDA czyta własne aliasy, ale kolejność albumu nadal jest 01, 02, 03 według rzeczywistych nazw plików. `F2` nie zmienia pliku na dysku.

### AMC-089-08 — Przejdź do albumu i wykonawcy

Na utworze rozpoznanego albumu w Ulubionych, Kolejce albo odtwarzaczu otwórz menu kontekstowe. Wybierz kolejno „Przejdź do albumu” i „Przejdź do wykonawcy”.

Oczekiwane: pierwsze polecenie otwiera zawartość właściwego albumu, drugie — folder wykonawcy z fokusem na folderze albumu. Polecenia nie pojawiają się dla luźnego pliku bez rozpoznanej relacji. Obowiązuje zwykła reguła opuszczania odtwarzacza, w tym ustawienie pauzy po Escape lub przejściu do listy.

### AMC-089-09 — Widoki bez ręcznego sortowania

Spróbuj `Alt+strzałka w górę/w dół` w Folderach (`Alt+1`), Wszystkich plikach (`Alt+2`), Albumie, Historii i wynikach wyszukiwania.

Oczekiwane: żaden z tych widoków nie zmienia kolejności. Foldery odpowiadają dyskowi, Wszystkie pliki są alfabetyczne, Album respektuje numery ścieżek, a Historia i wyszukiwanie zachowują własną semantykę.

### AMC-089-10 — Backspace jest tylko poziomem nadrzędnym

Sprawdź `Backspace` kolejno: na pliku w Folderach, w podfolderze, wewnątrz Albumu, w otwartym odtwarzaczu, na najwyższym poziomie Ulubionych oraz podczas edycji filtra. Osobno sprawdź `Delete` na bezpiecznym elemencie.

Oczekiwane: `Backspace` nie usuwa żadnego elementu. W Folderach idzie do rodzica, z Albumu do listy Albumów, z odtwarzacza do poprzedniej listy, a na poziomie głównym podaje brak rodzica. W polu filtra usuwa znak. Dopiero `Delete` wykonuje właściwe usuwanie z bieżącego widoku.

## Poprzedni zestaw alpha 88

### AMC-088-01 — Album z numerowanych plików

W Folderze Biblioteki przygotuj lub znajdź układ `Wykonawca\Album\01…`, `02…`, `10…`. Naciśnij `Ctrl+Shift+A`.

Oczekiwane: lista Albumy zawiera nazwę folderu albumu, nazwę folderu wykonawcy i poprawną liczbę utworów. Nie pojawia się ścieżka techniczna ani nazwa klasy programu.

### AMC-088-02 — Wejście, kolejność i powrót

Naciśnij Enter na rozpoznanym albumie, przejdź po utworach, uruchom jeden z nich i wróć z odtwarzacza. Następnie naciśnij Escape na liście utworów albumu.

Oczekiwane: pliki są w kolejności 01, 02, …, 10, niezależnie od zwykłego porządku tekstowego. Powrót z odtwarzacza prowadzi do tego samego utworu, a Escape z listy utworów do wcześniej zaznaczonego albumu.

### AMC-088-03 — Brak fałszywych albumów

Sprawdź folder z jednym plikiem, folder kilku plików bez numerów oraz nagrania zaczynające się od roku, np. `2026-08-24…`.

Oczekiwane: żaden z tych folderów nie pojawia się samoczynnie jako Album. AMC nie otwiera plików tylko po to, aby przeprowadzić klasyfikację, i nie wymusza pobrania całego Folderu Biblioteki z chmury.

### AMC-088-04 — Filtr i nawigacja literowa

Na liście Albumy wpisz pierwsze litery nazwy albumu, następnie użyj `Ctrl+K`, wpisz fragment nazwy i naciśnij Escape. Powtórz filtr wewnątrz albumu.

Oczekiwane: nawigacja literowa korzysta z nazwy albumu. Filtr dotyczy tylko aktualnego poziomu; Escape najpierw czyści aktywny filtr, a dopiero następny Escape opuszcza zawartość albumu.

### AMC-088-05 — Menu kontenera i działania utworu

Otwórz menu kontekstowe na wierszu albumu, a potem na utworze wewnątrz. Na bezpiecznym utworze sprawdź `Ctrl+C`, `Ctrl+Shift+C`, F2, Delete i natychmiastowe `Ctrl+Z`.

Oczekiwane: sam album oferuje otwarcie i informacje, ale nie udaje pliku ani elementu możliwego do dodania do kolejki. Utwór zachowuje zwykłe działania Biblioteki. Delete pozostawia plik na dysku, a `Ctrl+Z` przywraca go do Albumu.

### AMC-088-06 — Restart i inne sesje

Zamknij AMC będąc wewnątrz albumu, uruchom program ponownie i naciśnij `Ctrl+Shift+A`. Sprawdź też Albumy w demonstracyjnej sesji streamingowej.

Oczekiwane: start nie otwiera osieroconego ani technicznie nazwanego poziomu; lokalnie pojawia się lista Albumów. Istniejący album demonstracyjny innej usługi nadal jest widoczny i nie podlega lokalnej heurystyce folderów.

## Poprzedni zestaw alpha 87

### AMC-087-01 — Trzy układy Biblioteki

Na lokalnej liście użyj kolejno `Alt+1`, `Alt+2` i `Alt+3`, a potem `Ctrl+L`. Sprawdź menu Widok i paletę `Ctrl+Shift+K`.

Oczekiwane: `Alt+1` otwiera Foldery Biblioteki, `Alt+2` — Wszystkie pliki alfabetycznie, a `Alt+3` — Kolejność własną. `Ctrl+L` wraca do ostatniego z tych układów. NVDA podaje nazwę układu bez technicznych identyfikatorów.

### AMC-087-02 — Przesuwanie jednego pliku

W Kolejności własnej zaznacz plik znajdujący się z dala od początku i końca. Naciśnij `Alt+strzałka w górę`, a następnie `Alt+strzałka w dół`.

Oczekiwane: plik przesuwa się dokładnie o jedną pozycję, pozostaje zaznaczony, a NVDA mówi krótko „Przeniesiono wyżej” albo „Przeniesiono niżej”. Nazwa i pełna ścieżka z `Ctrl+Shift+C` nie zmieniają się.

### AMC-087-03 — Zaznaczenie wielu elementów i granice

Zaznacz Shiftem dwa lub trzy sąsiadujące pliki i przesuń je w górę oraz w dół. Spróbuj także przejść poza początek lub koniec listy. Jeśli wygodnie, sprawdź nieciągłe zaznaczenie myszą lub klawiaturą wspomagającą.

Oczekiwane: ciągły blok zachowuje kolejność wewnętrzną i przesuwa się razem. Na granicy AMC mówi o początku albo końcu. Nieciągłe zaznaczenie nie jest przestawiane i otrzymuje jasny komunikat.

### AMC-087-04 — Trwałość i nowe pliki

Zmień kolejność kilku pozycji, przejdź do `Alt+2`, wróć przez `Alt+3`, uruchom AMC ponownie i ponownie wybierz `Alt+3`. Następnie dodaj bezpieczny plik przez `Ctrl+O` albo do Folderu Biblioteki i użyj `F5`.

Oczekiwane: ręczny porządek przetrwa zmianę widoku i restart. `Alt+2` pozostaje alfabetyczne i nie przejmuje ręcznych zmian. Nowy plik zostaje dopisany na końcu Kolejności własnej.

### AMC-087-05 — Filtr nie zostaje ukrytą pułapką

W `Alt+3` naciśnij `Ctrl+K`, wpisz fragment nazwy i spróbuj `Alt+strzałka w górę`. Następnie naciśnij Escape. Powtórz filtr i zamiast Escape przejdź do `Alt+2`, `Alt+1`, innego folderu oraz innej sesji; wróć za każdym razem do wcześniejszego miejsca. Na końcu zamknij AMC z aktywnym filtrem i uruchom ponownie.

Oczekiwane: przy filtrze przesuwanie jest zablokowane. Escape czyści filtr i od razu przenosi fokus na pełną listę. Zmiana widoku, folderu albo sesji również usuwa filtr; nie wraca on po powrocie ani po restarcie.

### AMC-087-06 — Delete, wklejanie i regresja

W Kolejności własnej sprawdź Delete na bezpiecznym rekordzie i natychmiastowe `Ctrl+Z`. Wklej lokalny plik przez `Ctrl+V`, sprawdź menu kontekstowe oraz wyrywkowo Enter, Escape, `Ctrl+C`, `Ctrl+Shift+C`, F2, Shift+F2, wyszukiwanie i odtwarzacz.

Oczekiwane: Delete usuwa wyłącznie przynależność do Biblioteki i pozostawia plik na dysku, a `Ctrl+Z` przywraca rekord. Wklejony nowy plik trafia na koniec Kolejności własnej. Wyszukiwanie nie pozwala przestawiać wyników, a dotychczasowe funkcje nie mają regresji.

## Poprzedni zestaw alpha 86

Do prób `Shift+F2` użyj kopii pliku, którego utrata nie będzie problemem. AMC nie nadpisuje istniejącego pliku, ale test dotyczy rzeczywistej nazwy na dysku.

### AMC-086-01 — F5 i Ctrl+F5

Na lokalnej liście naciśnij `F5`, a następnie `Ctrl+F5`. Sprawdź też polecenia „Odśwież foldery Biblioteki” i „Foldery Biblioteki” w palecie `Ctrl+Shift+K`.

Oczekiwane: `F5` skanuje Foldery Biblioteki, `Ctrl+F5` otwiera ich okno, a oba polecenia są czytelnie opisane wraz ze skrótami. Po zamknięciu okna fokus wraca do listy.

### AMC-086-02 — F2 zmienia tylko nazwę w AMC

Zaznacz jeden lokalny plik, zapamiętaj jego pełną ścieżkę przez `Ctrl+Shift+C`, naciśnij `F2` i wpisz własną nazwę. Sprawdź ten element w Bibliotece, Ulubionych, Kolejce, Historii i Zakładkach, jeśli występuje, a następnie uruchom AMC ponownie i użyj `F5`.

Oczekiwane: wszędzie pojawia się nowa nazwa, lecz ścieżka i nazwa pliku na dysku pozostają bez zmian. Nazwa przetrwa restart oraz skan źródeł. Wpisanie później nazwy pliku bez rozszerzenia wyłącza alias i przywraca zwykłe zachowanie.

### AMC-086-03 — Shift+F2 zmienia plik na dysku

Na bezpiecznej kopii naciśnij `Shift+F2`, podaj nową nazwę bez rozszerzenia i zatwierdź. Sprawdź `Ctrl+Shift+C`, odtwarzanie, zapamiętaną pozycję, Ulubione, Kolejkę i Zakładki. Powtórz próbę na pliku wcześniej załadowanym do odtwarzacza.

Oczekiwane: rozszerzenie nie zmienia się ani nie dubluje, plik ma nową ścieżkę, a jego stabilny rekord i powiązane dane pozostają. Jeśli plik był załadowany, AMC zatrzymuje odtwarzanie, zwalnia uchwyt i zachowuje pozycję do wznowienia.

### AMC-086-04 — Ochrona nazwy i fokus

Spróbuj wpisać pustą nazwę, nazwę z niedozwolonym znakiem, nazwę zarezerwowaną Windows, np. `CON`, oraz nazwę już istniejącego pliku. Anuluj oba okna klawiszem Escape. Sprawdź również F2 przy zaznaczeniu wielu elementów, na folderze oraz w innej sesji.

Oczekiwane: AMC niczego nie nadpisuje i podaje jasny powód odmowy. Escape nie zmienia danych. Zmiana nazwy wymaga jednego pliku lokalnego, a po każdym oknie fokus wraca na ten plik.

### AMC-086-05 — Menu i regresja list

Na pliku lokalnym otwórz menu **Edycja**, menu kontekstowe i paletę poleceń. Potem wyrywkowo sprawdź Enter, Escape, `Alt+1/2`, `Ctrl+C`, `Ctrl+Shift+C`, Delete, `Shift+Delete`, wyszukiwanie i odtwarzacz.

Oczekiwane: obie operacje zmiany nazwy są dostępne z opisanymi skrótami, natomiast dotychczasowe działania nie zmieniły znaczenia. `F2` i `Shift+F2` nie przejmują klawiszy w polach tekstowych ani w odtwarzaczu.

## Poprzedni zestaw alpha 85

## Poprawka alpha 85

### AMC-085-01 — Nazwy źródeł bez danych technicznych

Otwórz **Plik → Foldery Biblioteki** i przejdź strzałkami po wszystkich folderach.

Oczekiwane: NVDA czyta wyłącznie przygotowaną etykietę, na przykład nazwę `Sideloads`, dostępność, zasadę pamiętania pozycji, liczby plików i ścieżkę. Nie może czytać `LocalFolderSourceStatus`, `Id`, `DisplayName`, `IsReachable`, nazw innych pól programistycznych ani nawiasów technicznego rekordu.

### AMC-085-02 — Szczegóły, ustawienie i fokus

Na wybranym folderze przejdź Tabem do szczegółów oraz ustawienia pamiętania pozycji, zmień wartość i wybierz **Zapisz dla folderu**. Wróć Shift+Tabem do listy.

Oczekiwane: lista nadal ma krótką czytelną etykietę, szczegóły zawierają pełną ścieżkę, zapis nie gubi wyboru, a fokus można przewidywalnie przywrócić do listy.

## Poprzedni zestaw regresyjny alpha 84

## Nowości alpha 84

### AMC-084-01 — Domyślny Escape

Odtwórz lokalny plik, przejdź do odtwarzacza, odczekaj kilkanaście sekund i naciśnij `Escape`.

Oczekiwane: dźwięk zostaje wstrzymany, a fokus wraca na listę do logicznie tego samego elementu. Zwykłe strzałki listy nadal służą do nawigacji i nie sterują czasem ani głośnością.

### AMC-084-02 — Wznowienie zapamiętywanego pliku

Przy domyślnie zaznaczonej opcji **Pamiętaj pozycje lokalnych plików** odtwórz dłuższe nagranie, wyjdź `Escape`, wróć przez `F6` i uruchom odtwarzanie. Powtórz po ponownym uruchomieniu AMC.

Oczekiwane: `Escape` zatrzymuje dźwięk, lecz nie zeruje zapamiętanego miejsca. Wznowienie zaczyna się od ostatniej pozycji także po restarcie.

### AMC-084-03 — Folder muzyczny zawsze od początku

Otwórz **Plik → Foldery Biblioteki**, wybierz bezpieczny folder z muzyką, ustaw **Dla wybranego folderu** na **Zawsze od początku** i zapisz. Odtwórz plik z tego folderu, wyjdź `Escape`, wróć do niego i uruchom ponownie.

Oczekiwane: okno i jego lista czytelnie podają nową zasadę. Po `Escape` plik z tego folderu rozpoczyna się od `0:00`; stara pozycja nie wraca także po restarcie.

### AMC-084-04 — Nadpisanie ustawienia ogólnego

Wyłącz w Ustawieniach ogólnych **Pamiętaj pozycję odtwarzania lokalnych plików**. Dla jednego folderu ustaw jednak **Pamiętaj pozycję odtwarzania**, a dla drugiego pozostaw **Zgodnie z ustawieniem globalnym**. Sprawdź po jednym pliku z każdego folderu.

Oczekiwane: pierwszy folder wznawia miejsce mimo wyłączonej zasady ogólnej, drugi zaczyna od początku. Plik dodany pojedynczo przez `Ctrl+O` korzysta z zasady ogólnej.

### AMC-084-05 — Opcjonalne granie po wyjściu

Odznacz **Wstrzymuj odtwarzanie po wyjściu z odtwarzacza**, rozpocznij odtwarzanie i naciśnij `Escape`. Potem ponownie włącz tę opcję.

Oczekiwane: przy wyłączonej opcji lista się pojawia, ale dźwięk trwa. Po ponownym włączeniu `Escape`, `Shift+F6`, przycisk **Wróć do listy** oraz bezpośrednie przejście do widoku wstrzymują dźwięk.

### AMC-084-06 — Paleta, fokus i regresja

W palecie `Ctrl+Shift+K` wyszukaj obie nowe opcje ustawień. Sprawdź fokus po Zapisz i Anuluj, przełączanie sesji z otwartego odtwarzacza, `Page Up/Down`, przewijanie i głośność w odtwarzaczu oraz zwykłą nawigację listy.

Oczekiwane: paleta otwiera Ustawienia na właściwym polu wyboru. Opuszczana sesja zostaje wstrzymana zgodnie z opcją, NVDA nie traci fokusu, a dotychczasowe skróty listy i odtwarzacza nie mają regresji.

## Poprzedni zestaw regresyjny alpha 83

## Nowości alpha 83

Do prób odłączania wybierz folder, który można później bezpiecznie dodać ponownie. **Odłącz folder** nie może usuwać żadnego pliku z dysku.

### AMC-083-01 — Fokus i zawartość menedżera

Otwórz **Plik → Foldery Biblioteki**. Sprawdź pierwszą kontrolkę, nawigację strzałkami i Tabem oraz Escape.

Oczekiwane: fokus zaczyna na zwykłej liście źródeł. Każdy wpis podaje nazwę, dostępność, liczby aktywnych, niedostępnych i wykluczonych plików oraz ścieżkę. Tab prowadzi do przycisków, Escape zamyka tylko menedżer, a fokus wraca do głównej listy AMC.

### AMC-083-02 — Odśwież jeden i wszystkie Foldery Biblioteki

W oknie **Foldery Biblioteki** wybierz folder i użyj **Odśwież wybrane**, następnie **Odśwież wszystkie**.

Oczekiwane: operacje kończą się czytelnym komunikatem, lista zachowuje zaznaczenie, NVDA nie traci fokusu, a niedostępny folder nie powoduje skasowania zapisanych rekordów.

### AMC-083-03 — Ochrona przed nakładającymi się folderami

Mając dodany Folder Biblioteki, spróbuj dodać ponownie tę samą ścieżkę, jej podfolder, a następnie folder nadrzędny obejmujący istniejący Folder Biblioteki.

Oczekiwane: ta sama ścieżka jest jedynie ponownie skanowana. Podfolder i folder nadrzędny nie tworzą drugiego Folderu Biblioteki; AMC jednoznacznie podaje, z którym istniejącym folderem wystąpił konflikt.

### AMC-083-04 — Bezpieczne odłączenie

Zanotuj liczbę plików i stan przykładowego folderu, wybierz **Odłącz folder**, przeczytaj całe pytanie i zatwierdź. Sprawdź dysk, `Alt+1`, `Alt+2`, Ulubione, Kolejkę, Historię oraz Zakładki. Uruchom AMC ponownie.

Oczekiwane: folder znika tylko z listy automatycznej synchronizacji. Żaden plik na dysku nie zostaje usunięty. Rekordy i ich relacje pozostają dostępne jako pliki dodane pojedynczo także po restarcie. Jeśli bieżący poziom Folderów należał do odłączonego folderu, AMC bezpiecznie wraca na główny poziom.

### AMC-083-05 — Pełna kopia AMC

W menedżerze wybierz **Eksportuj pełną kopię AMC**. Zapisz plik `.amcbackup.json` w bezpiecznym miejscu. Sprawdź też opis w **Ustawienia → Import i eksport**.

Oczekiwane: komunikat potwierdza eksport katalogu Biblioteki, źródeł, wykluczeń, Ulubionych, kolejek, historii, zakładek, pozycji i ustawień. Opis w Ustawieniach wymienia te dane i zaznacza brak haseł oraz tokenów. Sam eksport niczego nie zmienia w Bibliotece.

### AMC-083-06 — Paleta i regresja chmur

Otwórz `Ctrl+Shift+K`, wyszukaj „foldery biblioteki” i uruchom polecenie. Potem wykonaj `F5`, `Alt+1`, `Alt+2` oraz wyrywkowy test Folderu Biblioteki z iCloud, OneDrive albo Google Drive.

Oczekiwane: paleta otwiera ten sam menedżer. Dotychczasowa nawigacja, bezpieczne skanowanie chmury, kopiowanie, odtwarzanie i kolejki działają bez regresji.

## Poprzedni zestaw regresyjny alpha 82

## Poprawki alpha 82

### AMC-082-01 — OneDrive bez masowego pobierania

Przez `Ctrl+Shift+O` dodaj niewielki folder OneDrive zawierający kilka plików audio dostępnych lokalnie i, jeśli masz takie dane testowe, kilka pozycji „tylko online”.

Oczekiwane: AMC pokazuje nazwy obu rodzajów plików, nie pomija folderu z atrybutem Cloud Files i nie rozpoczyna samoczynnego odtwarzania ani pobierania całej zawartości.

### AMC-082-02 — Google Drive Mirror

Jeżeli używasz trybu lustrzanego Google Drive, dodaj niewielki folder z plikami audio i wykonaj `F5`.

Oczekiwane: źródło zachowuje się jak zwykły folder lokalny, bez duplikatów i bez specjalnych komunikatów chmurowych.

### AMC-082-03 — Google Drive Stream albo niedostępny dysk

Jeżeli używasz trybu strumieniowanego, dodaj folder z wirtualnego dysku Google Drive, zamknij AMC, zatrzymaj Google Drive for desktop, uruchom AMC i użyj `F5`. Następnie ponownie uruchom Google Drive i odśwież źródła. Jeśli nie masz trybu Stream, test można pominąć.

Oczekiwane: podczas niedostępności AMC nie usuwa rekordów, historii, zakładek ani pozycji. Informuje o niedostępnym źródle. Po powrocie dysku te same rekordy stają się dostępne bez duplikatów.

### AMC-082-04 — Regresja iCloud i Alt+1

Powtórz test `Sideloads` oraz przejście z zaznaczonego pliku w `Alt+2` do `Alt+1`.

Oczekiwane: `Sideloads` nadal pokazuje 317 zapisanych plików audio, a `Alt+1` prowadzi do folderu zaznaczonego elementu.

## Poprzedni zestaw regresyjny alpha 81

## Poprawki alpha 81

### AMC-081-01 — Naprawa źródła Sideloads

Uruchom alfę 81 na dotychczasowym stanie i otwórz Foldery Biblioteki przez `Alt+1`. Wejdź do źródła `Sideloads`.

Oczekiwane: jednorazowa migracja usuwa błędne zbiorowe wykluczenie z alfy 80, źródło nie jest puste i pokazuje rozpoznane pliki oraz podfoldery. Samo indeksowanie nie powinno rozpoczynać odtwarzania ani pobierania wszystkich nagrań z iCloud.

### AMC-081-02 — Alt+2 do Alt+1

W `Alt+2` zaznacz plik znajdujący się kilka poziomów pod jednym ze źródeł, a następnie naciśnij `Alt+1`.

Oczekiwane: AMC otwiera folder nadrzędny tego pliku, pozostawia fokus na tym samym pliku i odczytuje położenie w Folderach. Dla pliku dodanego pojedynczo przez `Ctrl+O`, który nie leży w żadnym źródle, `Alt+1` przechodzi do głównego poziomu Folderów i zachowuje zaznaczenie na samodzielnym pliku.

### AMC-081-03 — Placeholder iCloud i F5

W źródle iCloud sprawdź plik dostępny tylko jako placeholder, następnie naciśnij `F5`.

Oczekiwane: plik pozostaje w Bibliotece, skanowanie nie uznaje `ReparsePoint` za dowiązanie i nie uruchamia pobierania treści. Prawdziwe dowiązania katalogów nie są przeszukiwane.

### AMC-081-04 — Kolejki poszczególnych sesji

Dodaj element do Kolejki w WiiM lub innej demonstracyjnej sesji. Otwórz lokalny folder przez `Ctrl+Shift+O`, sprawdź lokalną Kolejkę, a potem wróć do poprzedniej sesji i ponownie otwórz jej Kolejkę.

Oczekiwane: po otwarciu folderu aktywna jest sesja Pliki lokalne, której Kolejka może być pusta. Powrót do poprzedniej sesji pokazuje jej wcześniejszą Kolejkę; samo skanowanie lokalnego źródła jej nie czyści.

## Poprzedni zestaw regresyjny alpha 80

## Nowości alpha 80

Do prób z kasowaniem i zmianą nazwy przygotuj osobny folder oraz niepotrzebne kopie plików. Nie wykonuj `Shift+Delete` na jedynym egzemplarzu nagrania.

### AMC-080-01 — Kilka źródeł i dwa układy Biblioteki

Dodaj przez `Ctrl+Shift+O` co najmniej dwa różne foldery, najlepiej z podfolderami. Użyj `Alt+1`, `Alt+2`, a potem `Ctrl+L`.

Oczekiwane: `Alt+1` pokazuje **Foldery Biblioteki** z osobnymi źródłami, `Alt+2` jedną alfabetyczną listę **Wszystkie pliki**, a `Ctrl+L` wraca do ostatnio wybranego układu. Są to dwa widoki tych samych rekordów, bez duplikatów.

### AMC-080-02 — Automatyczne dodanie pliku

Przy uruchomionym AMC skopiuj z zewnątrz nowy rozpoznawany plik audio do jednego ze źródeł i odczekaj około dwóch sekund. Sprawdź oba układy.

Oczekiwane: plik pojawia się bez ponownego wybierania folderu i bez przejęcia fokusu. Jeśli obserwator danego nośnika nie działa, `F5` dodaje plik po pełnym skanowaniu.

### AMC-080-03 — Brak pliku i jego powrót

Przenieś testowy plik poza źródło bez używania AMC. Sprawdź listę, a następnie umieść go z powrotem dokładnie pod tą samą ścieżką i naciśnij `F5`, jeśli zmiana nie pojawi się samoczynnie.

Oczekiwane: brakujący plik znika z aktywnych list, lecz AMC nie usuwa jego historii, zakładek ani zapamiętanej pozycji. Po powrocie pojawia się jako ten sam rekord.

### AMC-080-04 — Trwałe wykluczenie przez Delete

Zaznacz testowy plik i naciśnij zwykły `Delete`. Potem użyj `F5`, przełącz `Alt+1` i `Alt+2`, zakończ AMC i uruchom je ponownie.

Oczekiwane: plik pozostaje na dysku, ale nie wraca do Biblioteki po skanowaniu ani restarcie. W osobnej próbie wykonanej bezpośrednio po Delete `Ctrl+Z` przywraca go od razu.

### AMC-080-05 — Niedostępne całe źródło

Jeśli masz bezpieczny testowy folder na nośniku odłączanym, zarejestruj go, zamknij AMC, odłącz nośnik i uruchom program. Alternatywnie tymczasowo zmień nazwę folderu przy zamkniętym AMC. Użyj `F5`.

Oczekiwane: AMC zgłasza jedno niedostępne źródło, ale nie uznaje go za pusty folder i nie kasuje jego danych. Po ponownym udostępnieniu źródła rekordy wracają.

### AMC-080-06 — Zmiana nazwy podczas działania

Przy uruchomionym AMC zmień poza programem nazwę testowego pliku, a potem nazwę jego folderu. Wcześniej można dodać zakładkę lub zapamiętać pozycję odtwarzania.

Oczekiwane: lista aktualizuje nazwę i ścieżkę, nie tworzy duplikatu, a stan rekordu pozostaje. Ruch wykonany przy zamkniętym AMC nie jest jeszcze gwarantowany jako ten sam rekord i stanowi znane ograniczenie tej wersji.

### AMC-080-07 — Plik aktualnie odtwarzany staje się niedostępny

Odtwórz niepotrzebną kopię, a następnie przenieś ją poza źródło w Eksploratorze lub innym menedżerze plików.

Oczekiwane: AMC zatrzymuje własny tor, informuje, który plik stał się niedostępny, nie blokuje NVDA i pozostawia pozostałe elementy aktywne.

### AMC-080-08 — Nawigacja po znakach specjalnych

Umieść w jednym widocznym poziomie kopie o nazwach zaczynających się np. od `!`, `@`, `#` albo cyfry. Sprawdź wpisywanie znaków oraz `Shift+cyfrę` właściwą dla polskiego układu klawiatury, a następnie `Alt+1` i `Alt+2`.

Oczekiwane: znaki specjalne uczestniczą w nawigacji po nazwach; `Alt+1/2` przełącza układ i nie przechwytuje `Shift+cyfr`.

### AMC-080-09 — Wyszukiwanie po synchronizacji

Wyklucz jeden plik Delete, przenieś drugi poza źródło, dodaj trzeci i wykonaj `Ctrl+F` dla ich nazw.

Oczekiwane: wyszukiwanie lokalne znajduje nowy aktywny plik, ale nie zwraca wykluczonego ani niedostępnego.

### AMC-080-10 — Regresja działań i fokusu

W obu układach sprawdź Enter, Escape, `Ctrl+C`, `Ctrl+Shift+C`, Ulubione, Kolejkę, zakładki, `Alt+Enter`, zaznaczanie Shiftem oraz menu kontekstowe. Uruchom też pełne `F5` z fokusem wewnątrz listy.

Oczekiwane: stan jest wspólny w obu układach, fokus pozostaje na logicznie tym samym elemencie, a odświeżenie nie otwiera filtra ani nie przenosi fokusu na pasek stanu.

## Poprzedni zestaw regresyjny alpha 79

## Poprawki alpha 79

Do testu fizycznego kasowania używaj wyłącznie niepotrzebnych kopii plików. Zwykły `Delete` nie powinien w tym zestawie usuwać niczego z dysku.

### AMC-079-01 — Folder jako źródło Biblioteki

W pustej albo dotychczasowej sesji Pliki lokalne naciśnij `Ctrl+Shift+O` i wybierz folder zawierający rozpoznane pliki w katalogu głównym oraz podfolderach. Po zakończeniu skanowania naciśnij `Ctrl+L`.

Oczekiwane: Foldery pokazują rzeczywistą hierarchię, natomiast Biblioteka pokazuje płaską alfabetyczną listę wszystkich wczytanych plików. Nie pojawia się dawna, niezależna biblioteka ani duplikaty.

### AMC-079-02 — Ponowne włączenie znanego pliku

W Bibliotece usuń zwykłym `Delete` jeden plik, upewnij się, że zniknął z `Ctrl+L`, a następnie ponownie wybierz jego źródło przez `Ctrl+Shift+O` i wróć do Biblioteki.

Oczekiwane: znany plik wraca do Biblioteki bez powstania drugiego rekordu. Komunikat rozróżnia przywrócenie od dodania nowego pliku.

### AMC-079-03 — Delete na pliku w Folderach

W widoku Foldery zaznacz plik należący do Biblioteki i naciśnij `Delete`. Następnie sprawdź `Ctrl+L`, wróć do Folderów i użyj `Ctrl+Z`.

Oczekiwane: program mówi, że usunął plik z Biblioteki i pozostawił go w folderze. Plik znika z płaskiej Biblioteki, ale nadal istnieje na dysku i pozostaje widoczny w swoim fizycznym folderze. `Ctrl+Z` przywraca jego przynależność do Biblioteki.

### AMC-079-04 — Delete na wierszu folderu

Zaznacz podfolder albo wiersz źródła i naciśnij `Delete`.

Oczekiwane: nic nie zostaje usunięte. AMC wyjaśnia, że Delete nie usuwa folderu ani źródła, Enter otwiera folder, a zarządzanie źródłami będzie osobnym poleceniem.

### AMC-079-05 — Rozdzielenie Delete i Shift+Delete

Na niepotrzebnej kopii pliku porównaj `Delete` oraz `Shift+Delete` z potwierdzeniem.

Oczekiwane: `Delete` zmienia tylko przynależność w AMC. Dopiero `Shift+Delete` pyta o potwierdzenie i przenosi fizyczny plik do systemowego Kosza.

## Poprzedni zestaw regresyjny alpha 78

## Poprawki alpha 78

### AMC-078-01 — Pusta sesja lokalna

Uruchom AMC bez dodawania plików i naciśnij `Ctrl+1`. Następnie spróbuj F6 albo polecenia odtwarzania.

Oczekiwane: `Ctrl+1` wybiera „Pliki lokalne” i pustą listę, a nie sesję nieprzypisaną ani listę demonstracyjną. Polecenie wymagające pliku mówi „Brak elementów w sesji Pliki lokalne” i nie otwiera fikcyjnego odtwarzacza.

### AMC-078-02 — Wczytywanie folderu z innej sesji

Przejdź do TIDAL, Apple Music albo WiiM, naciśnij `Ctrl+Shift+O` i wybierz folder zawierający pliki audio, najlepiej także podfoldery.

Oczekiwane: po zatwierdzeniu wyboru program natychmiast przechodzi do „Pliki lokalne — Foldery” i mówi nazwę wczytywanego folderu. Podczas skanowania nie pokazuje demonstracyjnych utworów poprzedniej usługi. Po zakończeniu wyświetla prawdziwe podfoldery i pliki.

### AMC-078-03 — Pusty folder i trwałość źródła

Wybierz `Ctrl+Shift+O` pusty katalog, a potem zakończ i ponownie uruchom AMC.

Oczekiwane: program informuje, że nie znaleziono obsługiwanych plików, ale pozostaje w lokalnym widoku i nie wraca do danych demonstracyjnych. Źródło folderu i przypisanie `Ctrl+1` pozostają zapisane.

## Poprzedni zestaw regresyjny alpha 77

## Nowości alpha 77

Do testu Folderów najlepiej wybrać katalog zawierający co najmniej dwa pliki audio w katalogu głównym i dwa różne podfoldery. Zwykłe otwieranie folderu niczego fizycznie nie przenosi ani nie usuwa.

### AMC-077-01 — Domyślne miejsca sesji

Po pierwszym uruchomieniu tej wersji sprawdź `Ctrl+1`, `Ctrl+2`, `Ctrl+3` i `Ctrl+4`.

Oczekiwane w bieżącej wersji: `Ctrl+1` zawsze wybiera Pliki lokalne, także z pustą listą; dalej są WiiM, TIDAL i Apple Music. Program podaje numer, usługę oraz zapamiętany widok tej sesji.

### AMC-077-02 — Zmiana kolejności sesji

Otwórz `Ctrl+,`, kartę Ogólne i listę „Kolejność sesji i skrótów Ctrl+1–9”. Przesuń wybraną sesję `Alt+strzałka w górę` albo przyciskiem, zapisz ustawienia i sprawdź odpowiednie `Ctrl+cyfra`.

Oczekiwane: po każdym przesunięciu NVDA podaje nowy numer. Po zapisaniu skrót wybiera właściwą sesję, lista `Ctrl+0` ma tę samą kolejność, a `Ctrl+Page Up/Page Down` przechodzi według niej.

### AMC-077-03 — Trwałość kolejności

Zakończ AMC po zmianie kolejności i uruchom je ponownie.

Oczekiwane: własna kolejność oraz znaczenie `Ctrl+1–9` pozostają zachowane. Przycisk „Przywróć domyślną” odtwarza kolejność: Pliki lokalne, WiiM, TIDAL, Apple Music.

### AMC-077-04 — Połączona karta Komunikaty

W Ustawieniach odszukaj kartę Komunikaty i sekcję „Odczytywanie elementów list”. Zmień kolejność pól `Alt+strzałka w górę/w dół`, zapisz i sprawdź zwykłą listę.

Oczekiwane: nie ma osobnej, dublującej karty „Listy i odczyt”. Kolejność pól, podgląd, przyciski oraz wszystkie dotychczasowe ustawienia komunikatów są dostępne i działają. Paleta poleceń nadal potrafi ustawić fokus bezpośrednio na kolejności odczytu i na kolejności sesji.

### AMC-077-05 — Rejestracja źródła folderu

Naciśnij `Ctrl+Shift+O` i wybierz przygotowany katalog.

Oczekiwane: program niczego automatycznie nie odtwarza, rejestruje źródło i pokazuje widok Foldery. Na jednym poziomie podfoldery są przed plikami, a NVDA czyta każdy podfolder jako „folder”.

### AMC-077-06 — Nawigacja po poziomach

W widoku Foldery otwórz podfolder Enterem, uruchom plik Enterem, wróć Escape z odtwarzacza, a następnie użyj Backspace.

Oczekiwane: Enter na folderze schodzi dokładnie o poziom, Enter na pliku otwiera wspólny odtwarzacz, Escape wraca do tego samego poziomu i elementu, a Backspace wraca do folderu nadrzędnego. Na liście źródeł Backspace jedynie informuje, że wyżej przejść nie można.

### AMC-077-07 — Litery, filtr i wyszukiwanie

W podfolderze użyj kilku liter, następnie `Ctrl+K`; na końcu wykonaj `Ctrl+F` dla nazwy pliku znajdującego się w innym podfolderze.

Oczekiwane: litery i filtr dotyczą tylko widocznego poziomu. Wyszukiwanie obejmuje całą sesję Pliki lokalne i może znaleźć rekord spoza bieżącego folderu. Strzałka w lewo na pliku nadal podaje dostępne parametry techniczne.

### AMC-077-08 — Pamięć Folderów i Biblioteki

Pozostaw fokus wewnątrz podfolderu, zakończ AMC i uruchom ponownie. Następnie otwórz płaską Bibliotekę przez `Ctrl+L` i wróć do Folderów z menu Widok albo palety.

Oczekiwane: źródło oraz poziom folderu są pamiętane. Biblioteka nadal zawiera wszystkie zaimportowane pliki w płaskim zestawieniu, a powrót do Folderów nie tworzy duplikatów.

### AMC-077-09 — Regresja lokalnych działań

Na pliku dostępnym przez Foldery sprawdź odtwarzanie, zakładkę, `Ctrl+C`, `Ctrl+Shift+C`, Ulubione, Kolejkę i informacje `Alt+Enter`.

Oczekiwane: rekord jest tym samym elementem co w Bibliotece, więc odtwarzanie, pozycja, zakładki i stany przynależności są wspólne. Kopiowanie nazwy i fizycznego pliku działa jak dotychczas.

## Poprzedni zestaw regresyjny alpha 76

## Nowości alpha 76

Do tych prób użyj wyłącznie niepotrzebnych kopii plików, które można później odzyskać z systemowego Kosza.

### AMC-076-01 — Rezygnacja z potwierdzenia

Zaznacz testowy plik lokalny, naciśnij `Shift+Delete`, a w pytaniu wybierz „Nie”.

Oczekiwane: plik pozostaje na dysku i w AMC, a fokus wraca do listy.

### AMC-076-02 — Plik iCloud

Zaznacz niepotrzebną kopię pliku znajdującą się w iCloud Drive i potwierdź `Shift+Delete`.

Oczekiwane: plik trafia do systemowego Kosza i znika z katalogu AMC. Nie pojawia się okno „Nieoczekiwany błąd”, a aplikacja pozostaje uruchomiona.

### AMC-076-03 — Plik aktualnie odtwarzany

Uruchom testowy plik, wróć do listy, zaznacz go i wykonaj `Shift+Delete`.

Oczekiwane: AMC najpierw zamyka odtwarzanie i zwalnia plik, a następnie przenosi go do Kosza. Lista przechodzi do następnego dostępnego elementu i podaje wynik.

### AMC-076-04 — Kilka plików

Zaznacz Shiftem dwie lub trzy niepotrzebne kopie, także z iCloud Drive, i potwierdź `Shift+Delete`.

Oczekiwane: wszystkie przeniesione pliki znikają z AMC i można je znaleźć w Koszu. Program nie zawiesza się podczas odświeżania listy.

### AMC-076-05 — Kontrolowany błąd

Jeśli któryś plik jest zablokowany albo niedostępny, spróbuj go przenieść do Kosza.

Oczekiwane: AMC pozostaje uruchomiony, nie usuwa błędnego rekordu i podaje nazwę pliku, opis oraz kod `0x...`. Inne prawidłowo przetworzone elementy mogą zostać usunięte.

### AMC-076-06 — Delete pozostawia plik

Na osobnej kopii użyj zwykłego `Delete`.

Oczekiwane: znika tylko rekord AMC, natomiast fizyczny plik nadal istnieje w swoim folderze. `Backspace` nie usuwa rekordu i przechodzi do poziomu nadrzędnego.

## Poprzedni zestaw regresyjny alpha 75

## Nowości alpha 75

### AMC-075-01 — Lokalny wynik wyszukiwania

W sesji Pliki lokalne wykonaj `Ctrl+F`, wybierz znaleziony plik i naciśnij strzałkę w lewo.

Oczekiwane: NVDA podaje krótką informację o wyniku, zawierającą wszystkie dostępne dane, w tym format, czas, bitrate w kb/s, częstotliwość w kHz i rozmiar. Fokus pozostaje na tym samym wyniku, a okno wyszukiwania nie zamyka się.

### AMC-075-02 — Spójność ze zwykłą listą

Otwórz ten sam plik na zwykłej liście i ponownie naciśnij strzałkę w lewo.

Oczekiwane: zakres i kolejność informacji są takie same jak w wyszukiwaniu. Dopuszczalne jest uzupełnienie wcześniej nieznanego parametru po pierwszym odczytaniu metadanych.

### AMC-075-03 — Wyszukiwanie globalne

Użyj `Ctrl+Shift+F`, znajdź plik lokalny i naciśnij strzałkę w lewo.

Oczekiwane: szybka informacja działa również w globalnych wynikach i nie przełącza sesji ani widoku.

### AMC-075-04 — Sesja streamingowa

Na zwykłej liście i w wynikach wyszukiwania sesji demonstracyjnej naciśnij strzałkę w lewo.

Oczekiwane: AMC podaje te dane, które posiada, w tym bitrate i kHz, jeśli adapter je zwrócił. Nie szacuje nieznanych parametrów streamingu. Jeśli nie ma żadnych danych technicznych, mówi o ich braku.

### AMC-075-05 — Regresja schowka

Na lokalnym pliku sprawdź `Ctrl+C` w edytorze tekstowym oraz `Ctrl+Shift+C` przez wklejenie do folderu i do edytora.

Oczekiwane: pierwszy skrót kopiuje nazwę. Drugi przekazuje prawdziwy plik oraz pełną ścieżkę tekstową.

## Poprzedni zestaw regresyjny alpha 74

## Nowości alpha 74

### AMC-074-01 — Shift+Page na zwykłej liście

Na liście Multimedia, Biblioteka albo Ulubione naciśnij `Shift+Page Down`, a następnie `Shift+Page Up`.

Oczekiwane: jest to standardowe rozszerzone zaznaczanie większego zakresu listy. Widok nie zmienia się na Zakładki, nie pojawia się lista zakładek i tytuł okna nie zawiera słowa „Zakładki”.

### AMC-074-02 — Jawne otwarcie Zakładek

Zapamiętaj sesję, widok i zaznaczony element, a następnie naciśnij `Ctrl+B`.

Oczekiwane: dopiero ten skrót otwiera globalny widok Zakładek. NVDA podaje kontekst Zakładek i aktywnej sesji.

### AMC-074-03 — Dwustopniowy powrót

Na liście Zakładek otwórz wybraną zakładkę Enterem. W odtwarzaczu naciśnij Escape, a potem ponownie Escape.

Oczekiwane: pierwszy Escape wraca do tej samej zakładki na liście Zakładek. Drugi wraca do zapamiętanej sesji, poprzedniego widoku i możliwie tego samego zaznaczonego elementu.

### AMC-074-04 — Powrót między sesjami

Otwórz `Ctrl+B` z sesji Pliki lokalne, ale wybierz zakładkę należącą do innej sesji. Po jej otwarciu wróć dwukrotnie przez Escape.

Oczekiwane: pierwszy powrót prowadzi do rekordu Zakładek, drugi do pierwotnej sesji Pliki lokalne i miejsca sprzed `Ctrl+B`.

### AMC-074-05 — Escape i filtr Zakładek

W widoku Zakładek wpisz tekst do filtra. Naciśnij Escape dwa razy.

Oczekiwane: pierwszy Escape jedynie czyści filtr i pozostawia Zakładki otwarte. Drugi zamyka przejściowy widok i przywraca listę źródłową.

### AMC-074-06 — Nawigacja zakładek w odtwarzaczu

Otwórz materiał zawierający kilka zakładek i użyj `Shift+Page Up/Down` w odtwarzaczu.

Oczekiwane: skróty przechodzą wyłącznie po zakładkach tego materiału. Nie otwierają globalnej listy Zakładek.

### AMC-074-07 — Ponowne uruchomienie

Zakończ AMC, będąc na globalnej liście Zakładek, i uruchom program ponownie. Osobno wykonaj próbę, kończąc pracę w otwartym odtwarzaczu.

Oczekiwane: sama lista Zakładek nie jest po starcie przywracana bez ostrzeżenia — pojawia się podstawowa lista sesji. Odtwarzacz może zostać przywrócony; Escape prowadzi wtedy do zwykłej listy, a nie do niespodziewanego widoku Zakładek.

## Poprzedni zestaw regresyjny alpha 73

## Nowości alpha 73

### AMC-073-01 — Zaznaczanie wyników wyszukiwania

Wykonaj `Ctrl+F` albo `Ctrl+Shift+F`, a na wynikach użyj `Shift+strzałka w dół` kilka razy.

Oczekiwane: lista zachowuje się jak standardowa lista wielokrotnego wyboru i zaznacza kolejne wyniki. Nawigacja bez Shifta nadal wybiera pojedynczy wynik.

### AMC-073-02 — Zbiorcze kopiowanie nazw

Zaznacz kilka wyników i naciśnij `Ctrl+C`, następnie wklej do edytora tekstu.

Oczekiwane: każda nazwa znajduje się w osobnym wierszu i zachowana jest kolejność widoczna na liście.

### AMC-073-03 — Zbiorcze kopiowanie plików

W wyszukiwaniu sesji Pliki lokalne zaznacz kilka wyników, naciśnij `Ctrl+Shift+C` i wklej w pustym folderze testowym.

Oczekiwane: system wkleja wszystkie zaznaczone prawdziwe pliki. W schowku tekstowym dostępne są również ich pełne ścieżki.

### AMC-073-04 — Wyniki z różnych usług

Wyszukaj we wszystkich usługach, zaznacz wyniki lokalne i usługowe, po czym użyj `Ctrl+Shift+C` i wklej tekst do edytora.

Oczekiwane: lokalne pozycje mają pełne ścieżki, a usługowe — łącza. Jeśli wklejasz do folderu, system przekazuje tylko rzeczywiste pliki lokalne.

### AMC-073-05 — Blokada wycinania w wyszukiwaniu

Na wynikach naciśnij `Ctrl+X`, a potem `Ctrl+V`.

Oczekiwane: program jednoznacznie informuje, że wycinanie i wklejanie nie działają na liście wyników. W polu wyszukiwania `Ctrl+X`, `Ctrl+C` i `Ctrl+V` nadal standardowo edytują tekst.

### AMC-073-06 — Wklejanie do lokalnej biblioteki

Skopiuj w Eksploratorze jeden lub kilka obsługiwanych plików audio. W AMC przejdź do sesji Pliki lokalne, do widoku Multimedia lub Biblioteka, i naciśnij `Ctrl+V`.

Oczekiwane: pliki pojawiają się w AMC i są zaznaczone. Fizycznie pozostają w swoich dotychczasowych folderach. Ponowne wklejenie nie tworzy duplikatów.

### AMC-073-07 — Wklejanie do Kolejki i Ulubionych

Skopiuj plik spoza katalogu AMC. Wklej go najpierw w widoku Kolejka, a inny w widoku Ulubione.

Oczekiwane: pierwszy zostaje dodany do katalogu i kolejki, drugi do katalogu i ulubionych. Oba są dostępne również w lokalnych Multimediach.

### AMC-073-08 — Niedozwolone cele

Spróbuj wkleić plik w Historii odtwarzania, Zakładkach, odtwarzaczu i sesji demonstracyjnej.

Oczekiwane: żaden plik nie jest dodawany, a komunikat wskazuje dozwolone lokalne widoki.

### AMC-073-09 — Wytnij, a następnie wklej wewnątrz AMC

Na zwykłej lokalnej liście naciśnij `Ctrl+X`, następnie przejdź do Kolejki lub Ulubionych i naciśnij `Ctrl+V`. Potem spróbuj wkleić ten sam schowek do folderu w Eksploratorze.

Oczekiwane: AMC dodaje przynależność, ale nie przenosi pliku. Późniejsze wklejenie w Eksploratorze działa jak kopiowanie, a nie przenoszenie źródła.

## Poprzedni zestaw regresyjny alpha 72

## Nowości alpha 72

### AMC-072-01 — Globalna kolejność informacji

Otwórz `Ctrl+B` i przejdź po kilku zakładkach należących do jednego oraz do różnych plików.

Oczekiwane: każdy wiersz zaczyna się od nazwy pliku lub materiału, następnie podaje datę utworzenia zakładki, pozycję w materiale, opcjonalną nazwę, sesję i słowo „zakładka”. Kilka zakładek tego samego pliku pozostaje ułożonych według pozycji w nagraniu, a nie według daty utworzenia.

### AMC-072-02 — Krótka nawigacja w odtwarzaczu

W otwartym materiale użyj `Shift+Page Up/Down` najpierw dla zwykłej, a potem nazwanej zakładki.

Oczekiwane: zwykła zakładka podaje tylko czas. Nazwana podaje nazwę i czas. Nie powtarza tytułu pliku ani daty utworzenia.

### AMC-072-03 — Kopiowanie jednej zakładki

Na liście `Ctrl+B` wybierz zakładkę i naciśnij `Ctrl+C`, po czym wklej zawartość do edytora tekstu.

Oczekiwane: skopiowany jest pełny opis widocznego wiersza — razem z datą i czasem — a nie sam tytuł pliku źródłowego.

### AMC-072-04 — Kopiowanie kilku zakładek

Zaznacz kilka sąsiednich zakładek przez `Shift+strzałka`, naciśnij `Ctrl+C` i wklej wynik.

Oczekiwane: każda zaznaczona zakładka znajduje się w osobnym wierszu, w tej samej kolejności co na liście.

### AMC-072-05 — Powrót i fokus

Z listy Zakładek otwórz rekord Enterem, a następnie wróć przez Escape.

Oczekiwane: fokus wraca do tego samego rekordu i strzałki od razu działają. Nie pojawia się dodatkowy widok ani filtr.

### AMC-072-06 — Dwa sposoby kopiowania w wyszukiwaniu

W `Ctrl+F` i `Ctrl+Shift+F` wybierz lokalny wynik. Naciśnij `Ctrl+C`, wklej do edytora, następnie naciśnij `Ctrl+Shift+C` i wklej w folderze testowym albo menedżerze plików.

Oczekiwane: `Ctrl+C` kopiuje samą nazwę. `Ctrl+Shift+C` kopiuje prawdziwy plik oraz pełną ścieżkę. Okno wyszukiwania pozostaje otwarte, a fokus wraca na ten sam wynik. Dla wyniku usługowego `Ctrl+Shift+C` kopiuje łącze, nie fikcyjny plik.

### AMC-072-07 — Wycinanie lokalnego pliku

Na dowolnej zwykłej liście sesji Pliki lokalne wybierz niepotrzebny plik testowy, naciśnij `Ctrl+X`, przejdź do pustego folderu i naciśnij tam `Ctrl+V`.

Oczekiwane: samo `Ctrl+X` niczego nie usuwa. `Ctrl+V` przenosi prawdziwy plik. Po powrocie do AMC program wykrywa brak starej ścieżki, usuwa nieaktualny wpis i podaje jednoznaczny komunikat.

### AMC-072-08 — Anulowane wycinanie

Na lokalnej liście naciśnij `Ctrl+X`, ale nie wklejaj pliku. Wróć do AMC i dalej nawiguj.

Oczekiwane: plik pozostaje na dysku i na liście. `Ctrl+X` bez późniejszego `Ctrl+V` nie powoduje utraty danych.

### AMC-072-09 — Wycinanie z wyszukiwania

Wyszukaj lokalny plik przez `Ctrl+F` albo `Ctrl+Shift+F`, naciśnij `Ctrl+X`, wklej go do folderu testowego, wróć do wyszukiwania i zamknij je Escape.

Oczekiwane: plik jest przeniesiony przez system. Po zamknięciu wyszukiwania AMC porządkuje swój katalog. `Ctrl+X` dla wyniku TIDAL-a, Apple Music lub WiiM nie tworzy fikcyjnego pliku i zgłasza, że funkcja dotyczy tylko plików lokalnych.

## Poprzedni zestaw regresyjny alpha 71

## Nowości alpha 71

### AMC-071-01 — Kolejność niezależna od dodawania

W jednym pliku dodaj zakładki nie po kolei, na przykład najpierw w `10:00`, potem w `2:00`, a na końcu w `6:00`. Otwórz `Ctrl+B`.

Oczekiwane: zakładki tego pliku występują jako `2:00`, `6:00`, `10:00`, niezależnie od kolejności ich utworzenia.

### AMC-071-02 — Kilka materiałów i sesji

Utwórz zakładki w dwóch plikach albo sesjach, wróć do jednego z nich i otwórz `Ctrl+B`.

Oczekiwane: bieżący materiał znajduje się pierwszy i ma rosnący czas. Pozostałe zakładki są pogrupowane według sesji i tytułu; czasy różnych plików nie są przemieszane w jedną wspólną oś.

### AMC-071-03 — Enter z listy do odtwarzacza

Na liście `Ctrl+B` wybierz zakładkę i naciśnij Enter. Od razu użyj NVDA+strzałka w górę albo Spacji.

Oczekiwane: fokus jest na głównym przycisku odtwarzacza. NVDA czyta obiekt odtwarzacza, a Spacja wstrzymuje lub wznawia; nic nie pozostaje zablokowane na ukrytej liście.

### AMC-071-04 — Escape do tej samej zakładki

Po otwarciu zakładki Enterem naciśnij Escape, a następnie strzałkę w dół i w górę.

Oczekiwane: fokus wraca do tej samej zakładki na liście `Ctrl+B`, a obie strzałki natychmiast czytają sąsiednie rekordy.

### AMC-071-05 — Krótka regresja nazw

Dodaj szybką zakładkę przez `B`, nazwaną przez `Ctrl+Shift+B`, przejdź po nich `Shift+Page Up/Down` i sprawdź wyciszenie komunikatów.

Oczekiwane: funkcje alpha.69–70 działają bez zmian, nazwy są trwałe i nie powstają duplikaty w tej samej sekundzie.

## Poprzedni zestaw regresyjny alpha 70

## Nowości alpha 70

### AMC-070-01 — Dodanie nazwanej zakładki

Otwórz plik w odtwarzaczu, przejdź do wybranego miejsca i naciśnij `Ctrl+Shift+B`. Wpisz nazwę, na przykład „Początek rozmowy”, i zatwierdź Enterem.

Oczekiwane: fokus trafia bezpośrednio do pola „Nazwa zakładki”. Enter zapisuje, Escape anuluje, a po zatwierdzeniu AMC podaje nazwę i czas bez zatrzymywania odtwarzania.

### AMC-070-02 — Nazwanie istniejącej szybkiej zakładki

Dodaj szybką zakładkę klawiszem `B`. Bez przewijania naciśnij `Ctrl+Shift+B`, wpisz nazwę i zatwierdź.

Oczekiwane: istniejąca zakładka otrzymuje nazwę. Na liście nie pojawia się drugi wpis z tym samym czasem.

### AMC-070-03 — Globalna lista nazw

Dodaj kilka nazwanych i kilka szybkich zakładek, po czym otwórz `Ctrl+B`. Nawiguj strzałkami, użyj pierwszej litery nazwy i filtra `Ctrl+K`.

Oczekiwane: nazwana pozycja jest czytana w kolejności: nazwa zakładki, tytuł materiału, czas, sesja, zakładka. Wpis bez nazwy zachowuje krótszy dotychczasowy format. Nazwa działa w nawigacji literowej i filtrze.

### AMC-070-04 — Nawigacja i wyciszenie

Przejdź po nazwanych zakładkach przez `Shift+Page Up/Down`, potem wyłącz „Oznajmiaj nawigację po zakładkach” i powtórz test.

Oczekiwane: przy włączonej opcji AMC podaje nazwę oraz czas osiągniętej zakładki. Przy wyłączonej przechodzi prawidłowo, lecz bez automatycznego komunikatu. `Ctrl+Shift+E` nadal podaje czas na żądanie.

### AMC-070-05 — Trwałość i pełna kopia

Zamknij i uruchom AMC ponownie, sprawdź nazwy przez `Ctrl+B`. Opcjonalnie wykonaj pełny eksport i import na danych testowych.

Oczekiwane: nazwy, czasy i powiązania z materiałami pozostają zachowane. Eksport samych ustawień nadal nie zawiera zakładek.

## Poprzedni zestaw regresyjny alpha 69

## Nowości alpha 69

### AMC-069-01 — Kilka poprzednich zakładek podczas odtwarzania

W jednym dłuższym pliku utwórz co najmniej cztery zakładki. Odtwarzaj plik za ostatnią z nich i kilka razy dość szybko naciśnij `Shift+Page Up`.

Oczekiwane: każde naciśnięcie przechodzi do wcześniejszej zakładki. Odtwarzanie nie powoduje ponownego wyboru tej samej pozycji i nie następuje przejście do innego pliku.

### AMC-069-02 — Kilka następnych zakładek

Po dojściu do pierwszej zakładki kilka razy naciśnij `Shift+Page Down`.

Oczekiwane: każde naciśnięcie przechodzi do następnej zakładki w prawidłowej kolejności. Za ostatnią słychać komunikat o braku następnej zakładki.

### AMC-069-03 — Powrót do rzeczywistej pozycji

Przejdź do zakładki, następnie użyj zwykłego przewijania, skoku cyfrą albo `Ctrl+J`, po czym ponownie naciśnij `Shift+Page Up` lub `Shift+Page Down`.

Oczekiwane: po innym poleceniu AMC wybiera zakładkę względem nowej, rzeczywistej pozycji, a nie względem starej sekwencji.

### AMC-069-04 — Cicha nawigacja

Otwórz Ustawienia → Komunikaty i wyłącz „Oznajmiaj nawigację po zakładkach”. Wróć do odtwarzacza i użyj `Shift+Page Up/Down`.

Oczekiwane: po udanym skoku nie pojawia się automatyczny komunikat „Zakładka” z czasem, ale pozycja naprawdę się zmienia. `Ctrl+Shift+E` nadal odczytuje czas na żądanie. Na krańcu pozostaje komunikat o braku dalszej zakładki.

### AMC-069-05 — Paleta i trwałość ustawienia

W palecie `Ctrl+Shift+K` wyszukaj „komunikaty nawigacji po zakładkach”, naciśnij Enter i sprawdź, czy fokus trafia na właściwy checkbox. Zapisz ustawienia i ponownie uruchom AMC.

Oczekiwane: paleta podaje bieżący stan opcji, ustawienie jest zachowane po restarcie, a `B` nadal dodaje zakładkę i `Ctrl+B` otwiera ich listę.

## Poprzedni zestaw regresyjny alpha 68

## Nowości alpha 68

### AMC-068-01 — Dodawanie szybkich zakładek

Otwórz dłuższy plik w odtwarzaczu. Przejdź mniej więcej do 2 minut i naciśnij `B`, potem przejdź do innego miejsca i ponownie naciśnij `B`.

Oczekiwane: program krótko mówi „Dodano zakładkę” oraz czas. Nie otwiera się żadne dodatkowe okno, odtwarzanie i fokus pozostają w odtwarzaczu.

### AMC-068-02 — Duplikat w tym samym miejscu

Bez zmiany pozycji naciśnij `B` ponownie.

Oczekiwane: program mówi, że zakładka już istnieje; lista nie otrzymuje drugiego wpisu w tej samej sekundzie.

### AMC-068-03 — Nawigacja wewnątrz jednego materiału

W tym samym pliku użyj kilka razy `Shift+Page Up` i `Shift+Page Down`, także przed pierwszą i za ostatnią zakładką.

Oczekiwane: skróty ustawiają dokładne zapisane miejsca i czytają czas. Nie otwierają innego pliku. Na krańcach pojawia się jednoznaczny komunikat o braku poprzedniej albo następnej zakładki.

### AMC-068-04 — Globalna lista Ctrl+B

Dodaj zakładki w co najmniej dwóch sesjach, następnie naciśnij `Ctrl+B`. Nawiguj strzałkami, wpisz początkową literę tytułu i sprawdź filtr `Ctrl+K`.

Oczekiwane: zwykła dostępna lista zawiera zakładki ze wszystkich sesji. Każdy wiersz podaje kolejno tytuł, czas, usługę i słowo „zakładka”. Nawigacja literowa oraz filtr działają jak na innych listach.

### AMC-068-05 — Otwarcie zakładki z innej sesji

Na globalnej liście wybierz zakładkę należącą do innej sesji i naciśnij Enter. Potem naciśnij `Escape`.

Oczekiwane: AMC przełącza właściwą sesję, otwiera materiał i ustawia zapisany czas. `Escape` wraca do globalnej listy na tej samej zakładce.

### AMC-068-06 — Bezpieczne usuwanie

Na liście Zakładek naciśnij najpierw `Shift+Delete`, a następnie zwykły `Delete`. Sprawdź, czy plik nadal istnieje na dysku.

Oczekiwane: `Shift+Delete` wyjaśnia, że z tego widoku nie usuwa pliku. `Delete` usuwa tylko wybraną zakładkę i ustawia fokus na sąsiednim wpisie. Źródłowy plik pozostaje bez zmian.

### AMC-068-07 — Zapis po ponownym uruchomieniu

Pozostaw kilka zakładek, zamknij AMC przez `Alt+F4`, uruchom ponownie i naciśnij `Ctrl+B`.

Oczekiwane: wszystkie pozostawione zakładki, ich czasy i sesje są zachowane. Program nie rozpoczyna odtwarzania samoczynnie.

### AMC-068-08 — B na zwykłej liście i krótka regresja

Wróć do zwykłej listy mediów i naciśnij `B`. Sprawdź też lewą strzałkę, wznowienie pozycji, `Page Up/Down`, `Alt+góra/dół`, OGG i `Delete` w odtwarzaczu.

Oczekiwane: na zwykłej liście `B` nadal przechodzi do tytułu zaczynającego się na B, zamiast tworzyć zakładkę. Pozostałe potwierdzone funkcje nie zmieniają się. `Ctrl+Shift+B` jest na razie celowo wolne i zarezerwowane dla zakładki nazwanej.

## Poprzedni zestaw regresyjny alpha 67

## Nowości alpha 67

### AMC-067-01 — Prawa strzałka i fokus listy

Na lokalnym pliku naciśnij prawą strzałkę, potem strzałkę w dół i w górę.

Oczekiwane: nie otwiera się żadne okno systemowe, fokus pozostaje na liście, a NVDA nadal czyta kolejne elementy.

### AMC-067-02 — Menu lokalnego pliku

Otwórz menu kontekstowe na liście i w odtwarzaczu.

Oczekiwane: nie ma pozycji „Otwórz w…”. Pozostaje „Otwórz w domyślnej aplikacji” oraz właściwe działania AMC.

### AMC-067-03 — Regresja

Sprawdź lewą strzałkę, wznowienie pozycji po restarcie, `Delete` w odtwarzaczu i `Shift+Delete` na liście.

Oczekiwane: pozostałe funkcje działają bez zmian.

## Poprzedni zestaw regresyjny alpha 66

## Nowości alpha 66

### AMC-066-01 — Osobny proces z menu

Na pliku lokalnym otwórz menu kontekstowe i wybierz „Otwórz w…”.

Oczekiwane: systemowy wybór aplikacji staje się aktywnym oknem i NVDA odczytuje jego kontrolki. Po `Escape` fokus wraca do AMC.

### AMC-066-02 — Osobny proces prawą strzałką

Na tym samym pliku naciśnij prawą strzałkę.

Oczekiwane: działanie i fokus są takie same jak z menu. Jeżeli ponownie wystąpi cisza lub konieczność użycia `Alt+Tab`, uznajemy mechanizm za niezgodny i usuwamy funkcję.

### AMC-066-03 — Regresja

Sprawdź lewą strzałkę, wznowienie pozycji po restarcie, `Delete` w odtwarzaczu oraz `Shift+Delete` na liście.

Oczekiwane: pozostałe funkcje nie zmieniają się.

## Poprzedni zestaw regresyjny alpha 65

## Nowości alpha 65

### AMC-065-01 — Otwórz w przez menu

Na pliku lokalnym otwórz menu kontekstowe, wybierz „Otwórz w…” i odczekaj chwilę.

Oczekiwane: systemowy wybór aplikacji otrzymuje fokus, NVDA odczytuje jego kontrolki i można wskazać program albo anulować. Po zamknięciu fokus wraca do tego samego pliku w AMC.

### AMC-065-02 — Otwórz w prawą strzałką

Na tym samym pliku naciśnij prawą strzałkę i odczekaj chwilę.

Oczekiwane: wynik jest taki sam jak z menu. Jeżeli działa menu, ale nie strzałka, zapisz dokładnie tę różnicę — wtedy zachowamy funkcję tylko w menu.

### AMC-065-03 — Anulowanie i regresja

Anuluj systemowe okno przez `Escape`, sprawdź nawigację listy, lewą strzałkę, pamiętanie pozycji i `Delete` w odtwarzaczu.

Oczekiwane: `Escape` nie zamyka AMC, fokus wraca na listę, a potwierdzone funkcje `alpha.63–64` pozostają bez zmian.

## Poprzedni zestaw regresyjny alpha 64

## Nowości alpha 64

### AMC-064-01 — Prawa strzałka nie gubi fokusu

Na pliku lokalnym naciśnij prawą strzałkę, potem strzałkę w dół i w górę.

Oczekiwane: nie pojawia się systemowe okno ani cisza spowodowana utratą fokusu. NVDA pozostaje na liście, a zwykła nawigacja nadal czyta elementy.

### AMC-064-02 — Menu pliku lokalnego

Otwórz menu kontekstowe pliku na liście i w odtwarzaczu.

Oczekiwane: nie ma pozycji „Otwórz w…”. „Otwórz w domyślnej aplikacji” pozostaje dostępne. `Delete` w odtwarzaczu nadal usuwa tylko wpis AMC, a `Shift+Delete` jest dostępne wyłącznie na liście.

### AMC-064-03 — Krótka regresja alpha 63

Sprawdź wznowienie pozycji po restarcie, lewą strzałkę z `kb/s`, `Delete` w odtwarzaczu i `Ctrl+Z`.

Oczekiwane: wszystkie potwierdzone funkcje `alpha.63` działają bez zmian.

## Poprzedni zestaw regresyjny alpha 63

## Nowości alpha 63

### AMC-063-01 — Pozycja ostatniego pliku po ponownym uruchomieniu

Odtwórz dłuższy plik, przejdź co najmniej minutę od początku, wstrzymaj i zamknij AMC przez `Alt+F4`. Uruchom ponownie, użyj `F6`, a następnie `Ctrl+Shift+E`. Wznów odtwarzanie.

Oczekiwane: AMC nie uruchamia dźwięku samoczynnie, ale od razu pokazuje i podaje zapisaną pozycję. Pierwsze wznowienie zaczyna się z tego miejsca, a nie od `0:00`.

### AMC-063-02 — Prawa strzałka bez skojarzenia pliku — historyczne, wycofane w alpha 64

Na lokalnym pliku naciśnij prawą strzałkę. Najlepiej sprawdzić także rozszerzenie, dla którego Windows nie ma poprawnej aplikacji domyślnej.

Ręczny test wykazał utratę czytelnego fokusu NVDA. Aktualne wymaganie opisuje `AMC-064-01`.

### AMC-063-03 — Delete w lokalnym odtwarzaczu

Otwórz kopię pliku testowego w odtwarzaczu i naciśnij `Delete`. Sprawdź dysk, wypowiedź NVDA i następny element. Następnie naciśnij `Ctrl+Z`.

Oczekiwane: plik pozostaje na dysku. AMC usuwa tylko swój wpis, wstrzymuje usuwany element, podaje następny element albo sesję, a `Ctrl+Z` przywraca wpis.

### AMC-063-04 — Fizyczne usuwanie tylko na liście

W odtwarzaczu naciśnij `Shift+Delete`, a potem otwórz jego menu kontekstowe. Następnie wróć na listę i użyj `Shift+Delete` na kopii pliku testowego.

Oczekiwane: w odtwarzaczu skrót niczego fizycznie nie usuwa i nie ma pozycji przenoszenia do Kosza. Na liście pozostaje dotychczasowe pytanie potwierdzające i systemowy Kosz.

### AMC-063-05 — Krótka regresja

Sprawdź lewą strzałkę z `kb/s`, `Alt+F4` z odtwarzacza, `Ctrl+Shift+E/R/T`, OGG i zmianę prędkości.

Oczekiwane: zachowanie `alpha.62` pozostaje bez zmian.

## Poprzedni zestaw regresyjny alpha 62

## Nowości alpha 62

### AMC-062-01 — Bitrate pliku wcześniej odtwarzanego

Na liście zaznacz odtwarzany wcześniej plik i naciśnij lewą strzałkę.

Oczekiwane: komunikat zawiera rozszerzenie, czas, wartość `kb/s`, dostępne `kHz` i rozmiar.

### AMC-062-02 — Bitrate pliku jeszcze nieodtwarzanego

Dodaj nowy plik, nie uruchamiaj go i od razu naciśnij lewą strzałkę. Powtórz ją drugi raz.

Oczekiwane: AMC odczytuje metadane tylko tego pliku i podaje średni bitrate w `kb/s`. Druga próba korzysta z zapisanego wyniku. Nie rozpoczyna się odtwarzanie ani skanowanie całej listy.

### AMC-062-03 — Alt+F4 z odtwarzacza

Otwórz odtwarzacz przez `F6` i naciśnij `Alt+F4`.

Oczekiwane: cała aplikacja zamyka się od razu i zapisuje stan; nie następuje najpierw powrót do listy. Po ponownym uruchomieniu `Escape` i `Shift+F6` nadal wracają tylko do listy.

## Poprzedni zestaw regresyjny alpha 61

## Nowości alpha 61

### AMC-061-01 — Prawa strzałka i systemowe Otwórz w

Na pliku lokalnym naciśnij prawą strzałkę w głównej liście, Bibliotece i Historii odtwarzania.

Oczekiwane: bez otwierania menu AMC pojawia się bezpośrednio systemowy wybór aplikacji. Można jednorazowo otworzyć plik np. w foobar2000; ewentualna opcja zmiany aplikacji domyślnej zależy od wersji Windows.

### AMC-061-02 — Shift+Delete w odtwarzaczu — historyczne, wycofane w alpha 63

Odtwórz kopię pliku testowego, naciśnij `Shift+Delete`, najpierw wybierz Nie, a przy drugiej próbie Tak.

To zachowanie było testowane w `alpha.61–62`, lecz zostało świadomie wycofane. Aktualne wymaganie opisuje `AMC-063-04`.

### AMC-061-03 — Usunięty plik a Historia odtwarzania

Odtwórz plik, usuń go przez `Shift+Delete`, otwórz `Ctrl+H`, uruchom AMC ponownie i sprawdź historię ponownie.

Oczekiwane: usunięty element nie jest widoczny i nie wraca po restarcie. `Alt+góra/dół` również go pomija.

### AMC-061-04 — Krótka regresja

Sprawdź lewą strzałkę, `Ctrl+Shift+E/R/T`, zwykły `Delete` z `Ctrl+Z`, kopiowanie i naturalne przejście do następnego pliku.

Oczekiwane: zachowanie alpha 60 pozostaje bez zmian.

## Poprzedni zestaw regresyjny alpha 60

## AMC-060-01 — Start, ostatnio odtwarzany i F6

Uruchom AMC po wcześniejszym odtworzeniu pliku, lecz bez jego ponownego włączania. Sprawdź listę i `F6`.

Oczekiwane:

- program nie zaczyna sam odtwarzać i nie przesuwa zaznaczenia;
- zapisany element ma początek „Wstrzymany” albo „Ostatnio odtwarzany”;
- `F6` otwiera ten element w odtwarzaczu.

## AMC-060-02 — Jawne pytania o czas

Sprawdź `Ctrl+Shift+E`, `Ctrl+Shift+R` i `Ctrl+Shift+T` najpierw na liście, potem po `F6`.

Oczekiwane:

- w obu miejscach słychać odpowiednio czas od początku, pozostały i całkowity;
- działanie nie zależy od wyciszenia automatycznych komunikatów transportu.

## AMC-060-03 — Dwie niezależne historie

Na liście otwórz kolejno Bibliotekę, Kolejkę i Ulubione, po czym użyj `Alt+lewo` oraz `Alt+prawo`. Następnie odtwórz trzy różne pliki A, B i C, otwórz odtwarzacz i użyj `Alt+dół`, `Alt+dół`, `Alt+góra`.

Oczekiwane:

- boczny Alt cofa i ponawia widoki bieżącej sesji;
- w odtwarzaczu `Alt+dół` wybiera B, potem A, a `Alt+góra` wraca do B;
- każdy plik zachowuje własną pozycję;
- po ponownym uruchomieniu historia odtwarzania pozostaje, natomiast stos Wstecz/Naprzód może być pusty.

## AMC-060-04 — Widok Historia i lista źródłowa

Naciśnij `Ctrl+H`, sprawdź kolejność wpisów, następnie wróć do odtwarzacza i użyj `Page Up/Down`.

Oczekiwane:

- Historia jest uporządkowana od najnowszego wpisu i nie zawiera powtórzeń;
- `Page Up/Down` wybiera sąsiada odtwarzanego pliku na liście źródłowej, nie sąsiada z historii.

## AMC-060-05 — Strzałki lokalne we wszystkich widokach

Na pliku lokalnym użyj lewej i prawej strzałki w głównym katalogu, Bibliotece, Kolejce i Ulubionych.

Oczekiwane:

- lewa podaje krótkie informacje, a prawa otwiera menu działań i ustawia w nim fokus w każdym z lokalnych widoków;
- w sesjach nielokalnych strzałki zachowują zwykłą semantykę listy.

## AMC-060-06 — Delete i Shift+Delete

Na kopiach testowych plików sprawdź zwykły `Delete`, `Ctrl+Z`, a następnie `Shift+Delete`: najpierw odpowiedź Nie, potem Tak. Powtórz z wielokrotnym zaznaczeniem i z aktualnie otwartym plikiem.

Oczekiwane:

- `Delete` usuwa wpis z katalogu AMC i daje się cofnąć bez dotykania dysku;
- `Shift+Delete` wymaga potwierdzenia, po odpowiedzi Nie niczego nie zmienia, a po Tak zwalnia plik, przenosi udane pliki do systemowego Kosza i usuwa je z AMC;
- `Ctrl+Z` nie przywraca fizycznie usuniętych plików.

## AMC-060-07 — Kopiowanie wielokrotnego zaznaczenia

Zaznacz kilka plików. Wklej wynik `Ctrl+C` do edytora, a wynik `Ctrl+Shift+C` do edytora i do pustego folderu w Total Commanderze lub Eksploratorze.

Oczekiwane:

- `Ctrl+C` daje wszystkie nazwy, po jednej w wierszu;
- `Ctrl+Shift+C` daje pełne ścieżki w tekście i pozwala wkleić wszystkie fizyczne pliki.

## AMC-060-08 — Przełączanie sesji i regresja

Przejdź w każdej sesji do innego widoku, po czym przełączaj ją przez `Ctrl+1–9`. Sprawdź wyrywkowo OGG, prędkość, pasek, `Alt+Enter`, Kolejkę i naturalne przejście do następnego pliku.

Oczekiwane: komunikat zaczyna się od numeru i nazwy sesji, potem podaje przywrócony widok i element, np. „4, Pliki lokalne, Biblioteka…”. Pozostałe funkcje nie mają regresji.
