# RELEASE-20261119 — skrzynka, oznaczenie odtwarzania i odczyt parametrów

Status: W trakcie — źródła w realizacji, brak nowej budowy i publikacji.

## Nadrzędne decyzje Michała — 19.11.2026

Po zakończeniu wszystkich poniższych poprawek przygotować i opublikować dwa
nowe wydania na GitHubie: AMC Public (stable) i AMC Test (test/prerelease).
Nie czekać na kolejne polecenie „kontynuuj”. Publikacja nie oznacza instalacji
na głównym komputerze Michała: nie zamykać, nie restartować ani nie podmieniać
tam AMC. Nie przywracać dawnych lokalnych procedur Windows/ISCC ani nie
pobierać dużych EXE na stanowisko Hermesa. Zachować ochronę antywirusową.

Oznaczanie odtwarzania nie jest zadaniem samego sygnału dźwiękowego. Ma to być
wspólna informacja o faktycznie odtwarzanym materiale: **tekst, dźwięk albo oba**
według ustawień, we wszystkich odpowiednich listach aplikacji. Dotyczy głównych
list każdej sesji/usługi, kolejek, zawartości playlist i innych widoków tego
samego materiału, nie tylko pierwotnie wyliczonych usług. Kontener nie staje się
odtwarzanym utworem. Nie zgadywać tożsamości po tytule ani po wysłaniu Play.
To doprecyzowanie zastępuje węższą interpretację „sygnał poza Radiem”.

## Zakres źródeł i kolejność

1. Pamięć skrzynki Podcastów: wybór odcinka → Ctrl+L → Ctrl+I przywraca ten
   odcinek po stabilnej tożsamości, niezależnie od pamięci Biblioteki.
   Respektować istniejącą opcję pamięci list; kolejne Ctrl+I wewnątrz otwartej
   skrzynki nadal doładowuje następną porcję. Zachować nawigację i filtry.
2. Wspólny stan odtwarzania i jego tekstowe/dźwiękowe oznaczenie we wszystkich
   właściwych listach. Potwierdzona sesja i tożsamość; poprawny pause/stop/error,
   brak fałszywego oznaczenia i podwójnego sygnału. Bez zmiany fokusu, sieci
   wykonywanej przy każdym ruchu strzałką i bez regresji Radia/nagrywania.
3. NVDA+End: najpierw rzeczywiste parametry audio, nazwa pliku/materiału na
   końcu; zachować bitrate i pozostałe dane oraz fallback przy braku parametrów.
   Istniejąca konfiguracja kolejności list dotyczy góra/dół i lewej strzałki,
   a nie tego gotowego tekstu statusu. Bez przebudowy dodatku NVDA i Alt+D.
4. Niezależny przegląd niezmiennych kandydatów, proporcjonalne testy granic i
   integracja z wcześniejszymi zaakceptowanymi poprawkami kopiowania Podcastów,
   fokusu po usuwaniu i wyszukiwania Radio Archiwisty/globalnego zakresu.
5. Zamrożenie jednego odebranego commita; nowa chroniona budowa obu profili
   na GitHubie i publikacja dopiero po właściwych bramkach.

## Dodatkowe kryteria Radio Archiwisty — 19.11.2026

Przed zamrożeniem wspólnego kandydata odebrać również nowe polecenie Michała:
pełną historię Ctrl+H, zakładki, procentowe skoki w odtwarzaczu i pozostałe
wspólne funkcje materiału skończonego. Jeśli czegoś brakuje, minimalnie uzupełnić
istniejący mechanizm z funkcjonalnym RED/GREEN, bez odrębnego odtwarzacza.
[Szczegółowy zakres i faktyczne wyniki](RARCH-PLAYER-20261119-kontrakt.md).

Ograniczony odbiór kodu/offline zakończono: nie wykryto funkcjonalnego braku
wymagającego zmiany produkcji. Testy i raport przyjęto jako `8ff468c` po
niezależnym odczycie; rodzic po integracji 41 testów Python i 3 zestawy C# PASS.
Nie jest to odbiór GUI/NVDA/audio ani zgłoszenia o utracie wyjścia.

Na liście audycji ma działać zwykły wybór po nazwie literami i cyframi;
nie tworzyć selektora godzin ani aliasu 7 → 07. Cyfry przewijają procentowo
wyłącznie we właściwym kontekście odtwarzacza, nie podczas nawigacji listy.
[Korekta wcześniejszej diagnozy](RARCH-HOURS-20261119-diagnoza.md).
To rozszerza kryteria odbioru źródeł, nie zezwala na GUI/audio/instalację
na głównym PC ani nie oznacza gotowej publikacji.

## Dodatkowy zakres wyjść audio — późniejsze zlecenie 19.11.2026

Michał zlecił wdrożenie #30/PR #31 we wszystkich właściwych sesjach, w tym
TIDAL i Spotify, oraz odzyskanie po utracie zapamiętanego urządzenia:
ochronna pauza ma zostać, lecz jawne Spacja/Enter tej samej pozycji mają
odzyskać dźwięk bez obejścia przez inną stację. Sprawdzić odciszanie bez
bezwarunkowego naruszenia pauzy użytkownika. Zapisana preferencja urządzenia
jest odrębna od chwilowego wyjścia zastępczego.

[Kontrakt i granice adapterów](AUDIO-RECOVERY-20261119-kontrakt.md).
Otrzymano lokalny przyrost `0901f8d` i korektę odmowy wyboru Spotify
`17dcde5`, wspólny izolowany kandydat `e758ae4`. Niezależny `4a791c2`
potwierdził i naprawił ciszę przy wyłączonej ochronie, dodał test hostowego
wznowienia i asercje zachowania ręcznej preferencji Spotify (RED/GREEN).
Rodzic porównał 747 źródeł stagingu z tym commitem: bez różnic, lecz jego
uruchomienie testu hosta zwróciło exit 1 na `transport.pauseResume`.
Końcowy `3948aec` poprawił synchronizację testu, Debug u rodzica exit 0 po
porównaniu 747 źródeł. Jednak świeża wspólna kompilacja Release `aef4626`
ponownie daje test hosta exit 1 na wznowieniu. Próbne scalenie cofnięto w
`8d1da71`, przywracając wcześniejsze źródła i zachowując #26. Lokalnego audio
nie odebrano; poprzedni limit cykli wyczerpano bez ponowień w ciemno.
Michał ponownie polecił badać przełączanie audio. `deleg_12ac386e` dowiódł
w `6c70141`, że spóźniony PlaybackStarted nadpisuje ochronną pauzę hosta;
rodzic potwierdził kontrolowany RED i późniejszy GREEN kandydata `7b0a60b`,
po sprawdzeniu 749 źródeł. Przed akceptacją integracji trwa review
`deleg_9a50915b` nowej flagi hosta na granicach zwykłego startu i kolejki.
[Dowód przyczyny i bieżący etap](AUDIO-RECOVERY-20261120-late-start-odbior.md).
Polityka Spotify `7a26f3a` ma niezależny świeży Release i adapter/politykę offline
exit 0 u rodzica, ale nie została zintegrowana. Native callback/Denon/Chrome
pozostają niezmierzone; [dokładny odbiór](AUDIO-RECOVERY-20261119-spotify-parent-check.md).
Kandydaci nie rozstrzygają #28/PR #29; pozostaje sprawdzenie nakładających się
zmian WindowsMediaOutput. Pozostałe bramki i ograniczenia wydania bez zmian.

## Rozdziały YouTube — odbiór źródeł zakończony

#26/PR #27 przyjęto na bieżącej bazie jako `36ba1fed868aa655d38974ee93ec8af80932aeb7`,
z poprawką odbiorową `516e5b3`. Kontrolowany proces-fixture potwierdził RED/GREEN
zawieszenia po wyjściu rodzica; finalny runner obejmuje deadline również dla
stdout/stderr i kończy własne procesy przez Windows Job Object. Rodzic porównał
9 źródeł finalnego stagingu, całe `src/tests` po integracji i uruchomił celowany
Windows smoke: exit 0. Wcześniejszy niezależny zestaw koordynatora również PASS.
Nie wykonywano żywego YouTube, GUI ani audio.
[Szczegóły i granice](HUBERT26-CHAPTERS-20261119-plan.md).
Kolejny etap #28 dotyka wspólnego WindowsMediaOutput: uwzględnić kończony
odbiór lokalnego recovery i czas ochronnej pauzy. Nie jest to zamrożenie wspólnego
kandydata wydania ani usunięcie pozostałych bramek tego planu.

## Nowe osobne zadanie — zapis po wycinaniu nagrania

Michał zgłosił brak plików Dwójki po udanym z pozoru cięciu i pozostawione
wyniki pod technicznymi nazwami z kropką. Odczyt na głównym PC potwierdził
incydent w wydanej wersji `1723786`; nagrań nie modyfikowano. Dziennik podaje
weryfikację zapisu i usunięcie kopii, następnie FileNotFoundException, a wyniki
znajdują się nadal na dysku pod innymi nazwami. To odrębne następne zadanie,
nie zgoda na publikację lub naprawę plików użytkownika. Reprodukcja i poprawka
na fixture; do tego czasu nie nazywać ścieżki zapisu/cięcia naprawioną.
[Pełne zlecenie i dowody](AUDIO-EDIT-20261119-znikajace-nagrania.md).

## Stan początkowy i koordynacja

Główny checkout: `/home/michal/projekty/AMC`, gałąź
`codex/amc-test-public-20261109`, baza
`0b522c000335c7476e8e2cd6b4b35c2121e65d22`; odczyt Git: czysto.

Jeden wykonawca `gui.py` pracuje na `codex/amc-inbox-playing-20261119`.
Oddzielny wykonawca kolejności statusu C# pracuje na
`codex/amc-nvda-end-order-20261119`. Oba worktree w profilu AMC cache/scratch.
Root README i wspólny rejestr prowadzi rodzic, bez równoległej edycji.
Szczegółowe dowody należą do dokumentów INBOX-PLAYING-20261119 oraz
NVDA-END-20261119. Trwałe doprecyzowanie użytkownika: Hyperspace 1795045295.

## Bramki wydania

- Dotychczasowe Public `public-2026.11.18.0329` i Test
  `test-2026.11.18.0337` pochodzą z `1723786c4557ef68bb3922189a028099ce96db31`.
  Są zakończonym poprzednim wydaniem, nie artefaktami nowych poprawek.
- Nie ponawiać starych runów 44229055217/44292203528, nie podmieniać tagów,
  nie przepisywać ich sidecarów ani nie relabelować starych binariów.
- Obecny workflow budowy dopuszcza jednorazowe utworzenie dokładnej starej
  gałęzi r2; trzeba przygotować nowy ścisły ref oraz wszystkie powiązane guardy
  i celowane testy przed pojedynczym uruchomieniem. Nie osłabiać warunków.
- Zachować kanoniczne skrypty, integralność, realne skany Defendera na VM,
  kontrolę profili i sekretów oraz zgodność źródeł i manifestów obu wyjść.
- Próba instalacji/startu i żywy NVDA są osobnymi dowodami, nie wynikają z
  headless ani ze skanu/builda. Nie ustawiać acceptance=true bez pomiaru.
  Przed interaktywną próbą rozstrzygnąć zgodne warunki; nie wykonywać jej
  automatycznie pod pozorem testów bezokiennych.
- Historyczny alert RCX pozostaje nierozstrzygnięty; nowy czysty skan nie
  oznacza werdyktu false positive. Nie osłabiać AV, nie restartować Windows,
  nie odtwarzać próbek i nie wysyłać profili/logowania do Microsoftu.
- Publisher dopiero dla nowego źródła/runu/ID/digestów. Po publikacji odczytać
  oba dokładne wydania, tagi, assety i rzeczywiste manifesty aktualizatora.

## Testy i wynik

Odebrano dwa lokalne kandydaty; NVDA+End po niezależnym przeglądzie
zintegrowano, Skrzynka/oznaczanie pozostają osobnym kandydatem. Brak nowego wydania:

- Skrzynka/oznaczanie: `547b904a8d2113766e11cfbf45396a00c9827951`.
  Wykonawca zgłasza 82 celowane testy zaliczone; nie jest to pełny odbiór
  wszystkich usług i widoków. Podany `exit 4` próby TIDAL był błędem
  przygotowania testu, nie funkcjonalnym RED. Frontier/Apple pozostały bez
  potwierdzonego mapowania; nie zastępować go zgadywaniem. Niezależny
  przegląd odrzucił pełny odbiór tego kandydata. Potwierdzono pamięć ID
  Skrzynki w navigatorze i zachowanie doładowania Ctrl+I, ale wykazano:
  - `before/after/off` nie są respektowane spójnie: brak położenia w rendererze,
    opcja pominięta w cache, handler ustawień odświeża tylko Radio;
  - wyszukiwanie ignoruje `off` i nie ma ścieżki sygnału przy fokusie;
  - callbacki WiiM/Sonos zatwierdzają żądane ID mimo innego `itemId` statusu;
  - syntetyczna macierz `Row(material_id=...)` nie dowodzi podłączenia
    loaderów kolejki, playlist, historii i zakładek.
  Rodzic odczytał rzeczywiste callbacki i handler ustawień. Zlecono korektę
  przez jednego autora `gui.py` z funkcjonalnym RED/GREEN i produkcyjnymi
  ścieżkami. Osobny wykonawca tylko odczytowo bada pochodzenie ID hosta
  WiiM/Sonos/Frontier/Apple: samo pole statusu może być echem żądania,
  więc porównanie pola nie zastępuje dowodu obserwacji.
  Uwaga do trzeciego punktu review: brak `sonosPlaylist` w predykacie nie
  uzasadnia automatycznego dodania kontenera jako utworu. Potrzebny jest
  kontrakt rzeczywistego materiału/zawartości; nie oznaczać playlisty zamiast
  odtwarzanego utworu. Recenzent uruchomił reproduktory obserwacyjne,
  nie pełną suite; brak pytest w jego bezpośrednim Pythonie nie jest blokadą
  projektu, bo działa istniejące `uv run --offline --python 3.11 --with pytest`.
- NVDA+End: kod `fb6ee2e`, dokumentacja i HEAD
  `a51b69ed53b449f1a574f079f4be35110c5fe718`. Rodzic odczytał diff:
  wspólny formatter jest wywoływany przez generalny status, Spotify i TIDAL.
  Wykonawca zgłasza rzeczywisty RED TIDAL, kompilację testów protokołu bez
  błędów i ostrzeżeń oraz trzy zaliczone zestawy. Test helpera z etykietą
  Spotify nie jest testem całego koordynatora Spotify. Pełny LiteHost offline
  blokują brakujące Windows reference packs; nie pobierano ich. Niezależny
  przegląd zakończył się bez blokera źródłowego. Zintegrowano jako
  `d4a298c787d8225576045f7681717a0ac4afc177`; porównano identyczność kodu
  z kandydatem. Po integracji: 28 testów Python statusu/Radia zaliczonych
  oraz jeden zestaw C# TIDAL zaliczony. Bez lokalnego buildu Windows.

Nie ma jeszcze zintegrowanego i zamrożonego wspólnego source SHA, nowej
budowy, instalacji ani odbioru żywym NVDA. Nie oznaczać planu jako
zakończonego na podstawie testów headless lub raportów wykonawców.

## Rekonesans drogi wydania — 19.11.2026

Odczytowy przegląd potwierdził model nowej, jednorazowej gałęzi chronionej
budowy, wspólny exact-ref workflow/PowerShell i konieczność nowego numeru
technicznego zamiast użytego alpha.511. Wykonawca rekonesansu uruchomił po
6 testów kontraktu budowy i publishera; nie są dowodem nowego buildu.
Rodzic odczytał rzeczywiste testy, guard PowerShell i pobrał aktualną gałąź:
`HEAD...origin/codex/amc-test-public-20261109` = `28 0` (ahead/behind).
Zdalny `86add1c` jest przodkiem lokalnego `bb3dc06`; nie ma brakującego
zdalnego commita do scalania. Ponowić odczyt przed zamrożeniem/pushem.

Chroniony workflow nie testuje instalacji przez EXE, tylko kontrole bundle.
Nie istnieje odebrana procedura instalacji nowego EXE ani żywego NVDA na
runnerze. Nie dopisywać pozornego zaliczenia. Przed etapem wymagającym
interaktywnego odbioru uzgodnić bezpieczne okno na Hermesie lub wskazać
konkretną blokadę; główny komputer i jego instalacja pozostają nietknięte.

Dozwolone przygotowanie bez uruchamiania: nowy ścisły ref oraz wersja w
Directory.Build.props/workflow/guardzie i ich powiązanych testach. Push
uruchamiający budowę nadal czeka na ukończenie i review wszystkich poprawek.
Publisher będzie miał nowe ID/hashe dopiero po rzeczywistej nowej budowie.

Odebrano przygotowawczy commit `18452db257e7b1dd33fac11164176e9eb7b34811`
(baza `82db46c329702dce3cdf129912fb7fb9a05e830a`): tylko cztery pliki,
nowy exact ref `codex/amc-protected-public-test-20261119-playback-r1`,
wersja `0.1.0-alpha.512`, spójne trigger/job/guard/props i negatywne testy
rozjazdów. Rodzic niezależnie odczytał cały diff i uruchomił kontrakty:
**12 passed in 0.08s**. Istniejący lokalnie `ruamel.yaml` sparsował pełny YAML
oraz potwierdził pojedynczy dokładny push, uprawnienia tylko do odczytu,
GitHub-hosted Windows, first-create/first-attempt i cztery akcje przypięte
pełnym SHA. Guard zachował BOM; osiem bloków `run` pozostaje w workflow.
Brak `actionlint`; parser YAML nie weryfikuje całej semantyki Actions ani
rzeczywistego działania Defendera. Nie instalowano narzędzi. Wykonawca
raportuje parser PowerShell i izolowany test początkowego guarda, bez cmdletów AV.
To odbiór przygotowania źródeł workflow, nie uruchomienie budowy. Commit
nie zawiera jeszcze pozostałych nowych poprawek, więc nie wolno pushować go
jako wejścia nowej budowy. Przed docelowym pushem ponowić odczyt zdalnej
rezerwacji wersji i istnienia refu/runów; wyniki wykonawcy były chwilowym
odczytem, nie rezerwacją.

## Korekta prezentacji i obserwowalność urządzeń

Odebrano lokalną korektę `17e677aec46affe4868af0b732c7aaf1b9a8374b`
na gałęzi Skrzynki/oznaczania, bez integracji. Rodzic odczytał pełny diff
produkcji względem `547b904`: położenie tekstu trafia do renderera i cache,
handler ustawień odświeża wszystkie obsługiwane sesje, SearchDialog wiąże
fokus z callbackiem sygnału. Wykonawca raportuje **121 passed, 12 skipped**
w sześciu plikach; pominięć nie wliczać do zaliczeń. Niezależny, ograniczony
przegląd tej korekty trwa w oddzielnym, zamrożonym worktree.

Korekta usunęła przypisywanie żądanego materiału i jego historii w WiiM/Sonos.
To usuwa fałszywe potwierdzenie, ale samo wyłączenie oznaczeń nie realizuje
zlecenia. Odczyt hosta potwierdził konkretną, małą drogę do obserwowanego ID:

- WiiM: urządzenie zwraca ContentUri; host już porównuje je z zapisanymi
  strumieniami, ale późniejszy resolver może zachować pamiętany zamiar.
  Do oznaczania wolno użyć tylko jednoznacznego, dokładnego URI match,
  nie `rememberedStreamId`, przekierowania ani podobnego tytułu.
- Sonos: rzeczywisty odczyt playback zawiera ItemId; obecna migawka go
  odrzuca i wystawia ID grupy. Dla własnego strumienia istnieje tożsamość
  URL/ID wyliczana przez SonosOwnStreamIdentity. Potwierdzony odczyt można
  dopasować do własnego strumienia, nie automatycznie do ulubionej/playlisty.
- Frontier: dostępne metadane/stan nie zawierają stabilnego URI ani ID
  materiału. ID urządzenia i hash tytułu nie są tożsamością wiersza.
- Apple Music: SMTC potwierdza oryginalną aplikację, tytuł, autora i stan,
  ale nie catalog/library ID/URI. Nie oznaczać zaznaczonego utworu AMC
  na podstawie tych danych ani nie udawać własnego odtwarzacza AMC.

Zlecono minimalne rozszerzenie hosta i podłączenie GUI w rozdzielonych
zakresach autorów: opcjonalne `observedMaterialId` i `identityProvenance`.
WiiM: `uriMatch`; Sonos własne strumienie: `playbackItemId`. Brak
wiarygodnego dopasowania daje null i usuwa znacznik, nigdy fallback zamiaru.
Pole `itemId` urządzenia/grupy pozostaje kompatybilne. Każdy nowy odczyt
musi nadal należeć do właściwego urządzenia/grupy i sesji; idle/stop/error
nie mogą zostawiać oznaczenia. To rozszerzenie statusu, nie przebudowa silników,
kont ani logowania. Brak mapowania Sonos ulubionych/dzieci playlist oraz
Frontier/Apple pozostaje jawną luką, nie zaliczeniem pełnego zakresu.

## Wynik przeglądu korekty i wspólnego kontraktu

Niezależny przegląd `17e677a` zakończony: nadal dwa konkretne blockery.
SearchDialog zawsze umieszcza tekst stanu przed tytułem, także dla `after`.
Navigator przed wywołaniem hosta wpisuje żądane ID do current_material_id;
wczesne returny (brak hosta/grupy) omijają czyszczenie. Nie przyjęto jeszcze
tego kandydata do integracji. Recenzent potwierdził **35 passed, 12 skipped**
w test_library_activity; wszystkie 12 pominięć oznacza brak chronionego
fixture biblioteki, nie brak wx i nie zaliczone scenariusze.

Odebrano kolejne osobne źródła, nadal bez integracji:

- Host `bfc684d1b79ece44c80b31a7b3112fa9074a11d2`: opcjonalna tożsamość
  WiiM po URI oraz Sonos po odczytanym playback ItemId; wykonawca raportuje
  trzy zaliczone zestawy protokołu/koordynatora.
- GUI `c75f6e9ee8a5b5284f5a5d63a865236883944904`: parsery, użycie obserwacji
  do list/wyszukiwania/historii, czyszczenie błędnych statusów; wykonawca
  raportuje 101 zaliczonych testów celowanych. Nadal ma oba blockery review.

Rodzic odczytał pełne diffy obu warstw i wykrył rozjazd, którego oddzielne
syntetyczne testy nie ujawniły: produkcyjny loader hosta i status Sonosa
wystawiają `sonos-stream:<id>`, ale GUI akceptuje `sonos-ownStream:<id>`.
Poprawić konsumenta do rzeczywistego ID, nie zmieniać identyfikatora hosta
ani nie dodawać aliasów w ciemno. Ponadto host dla Sonos kasuje ID w pauzie,
a WiiM wystawia je także przy idle; ujednolicić confirmed playing/paused,
bez cue/historii przy pauzie i bez oznaczenia podczas idle/buffering.

Ostatnie ograniczone korekty wykonawców są gotowe: Python
`40a24341116386f6caccda38ea706c258deec276` i host
`f4747db4dbdc13aa2f250e5c98961ad507c40bcd`. Rodzic rzeczywiście uruchomił
cztery zestawy C# i ponownie wygenerował eksport dziesięciu przypadków
(pięć WiiM, pięć Sonos): playing, paused, idle, mismatch, ambiguous dla obu
sesji. Wyniki mapperów C# przeszły produkcyjne parsery/loadery/handlery
Python; dziewięć celowanych testów korekty zaliczono.

Rozszerzenie tego samego testu o zastosowanie dekoracji na głównej liście
wykryło jeszcze brak etykiety hostowego `pauza` i flagę sygnału przy pauzie
w dekoratorze natywnym. Rodzic uzyskał funkcjonalne RED dla obu granic,
minimalnie poprawił wspólne dekoratory i zapisał
`a5caacd9711fd4e38ca4c9941a900f19cec0085a`. Tekst pauzy pozostaje,
flaga sygnału jest wyłączona. Cztery celowane moduły: **51 passed in 0.50s**
z rzeczywistym eksportem C#, bez pominięcia kontraktu. Nie sumować tego
wyboru z wcześniejszymi nakładającymi się wynikami. Zlecono ostatni,
ograniczony odczyt zamknięcia konkretnych blockerów; nie nowy szeroki audyt.
Wejścia fixture są fikcyjne; wykonanie mappera nie jest testem żywego
urządzenia, GUI ani NVDA.

Przygotowanie chronionej budowy `18452db` zintegrowano osobno do głównej
gałęzi. Po integracji **18 passed in 0.22s** dla kontraktów workflow i
publishera, diff bez błędów, drzewo czyste. Nadal bez pushu, uruchomienia
Actions, Windows builda ani publikacji. Nie zamrożono jeszcze source SHA.

## Integracja po końcowym przeglądzie — 19.11.2026

Ograniczony, niezależny przegląd `a5caacd`/`f4747db` przyjął zmienione granice
bez nowego objawu; wykonał 10 testów korekty z rzeczywistym eksportem C#.
Werdykt nie obejmuje żywego GUI/NVDA ani usług bez stabilnego ID.

Zintegrowano do głównej gałęzi:

- Python: `0e24e4b` (kandydat `a5caacd9711fd4e38ca4c9941a900f19cec0085a`);
- host: `a045f7cbd54abbbc33e3861de075a54be3ed1d6b` (kandydat
  `f4747db4dbdc13aa2f250e5c98961ad507c40bcd`).

Jedyny konflikt dotyczył nagłówka rejestru zadań; zachowano nadrzędny plan
wydania, przyjęty status NVDA+End i wpis Skrzynki. Produkcyjne pliki Python
i hosta po integracji są bajtowo identyczne z odpowiednimi kandydatami
(`git diff --exit-code`). Rodzic uruchomił na głównym checkoutcie pięć
zestawów C# (WiiM, Sonos, koordynator obserwacji Sonos, kontrakt eksportu,
TIDAL) i nowy eksport mapperów do testu wspólnej granicy: zaliczone.

Po integracji siedem celowanych modułów Python: **94 passed in 5.02s**.
Wybór obejmuje test korekty i kontraktu C#→Python, wspólną dekorację,
historię streamerów, regresję Radia, nawigację Podcastów i status NVDA+End.
Nie jest to pełna suite ani żywy odczyt czytnikiem. Bez pominięcia testu
kontraktu, bez Windows builda, instalacji, pushu i publikacji.

## Znane ograniczenia i następny krok

Gotowy, odebrany przyrost źródeł nie realizuje jeszcze żądania oznaczenia
we wszystkich usługach: Sonos ulubione/zawartość playlist, Frontier i Apple
Music nie mają niezależnie obserwowanego, mapowalnego ID materiału w obecnym
kontrakcie. Dalsze zgadywanie po tytule/zaznaczeniu jest wykluczone.
Publikacja została uzgodniona po wszystkich zadaniach, więc rodzic nie może
sam zmienić jej na wydanie podzbioru. Przed budową/publikacją potrzebna jest
decyzja Michała, czy wydać odebrany przyrost z jawnymi ograniczeniami,
czy pozostawić oba wydania wstrzymane do rozwiązania reszty tożsamości.
Dotychczasowy zakaz instalacji na głównym PC oraz ograniczenia lokalnego
buildu/EXE/GUI i obowiązek uczciwego wykazania bramek pozostają w mocy.

## Uzasadnienie i decyzje

Jedna warstwa stanu/tożsamości ma zasilać tekst i dźwięk w różnych widokach;
nie tworzyć osobnych oznaczeń kopiowanych dla każdej listy/usługi. Dostawcy
mają mapować potwierdzone dane, a nie zastępować brak informacji pozorem sukcesu.
Nowe wydania muszą rzeczywiście zawierać wszystkie odebrane poprawki.

## Materiał do dokumentacji użytkownika

Planowany rezultat, jeszcze nie deklaracja dostarczenia: po powrocie do skrzynki
Ctrl+I użytkownik kontynuuje od ostatniego odcinka. Odtwarzany element rozpoznaje
na listach, także w kolejce i playliście, przez wybrany tekst, dźwięk albo oba.
NVDA+End podaje najpierw parametry audio, a na końcu nazwę materiału. Nowe wersje
Public i Test mają być dostępne przez właściwe kanały GitHub/aktualizatora;
użytkownik aktualizuje główny komputer samodzielnie.

## Wersja i commit

Nowe źródła i artefakty: jeszcze nieustalone; dopisać rzeczywiste SHA, runy,
numery/etykiety, hashe i wyniki po wykonaniu odpowiednich etapów.
