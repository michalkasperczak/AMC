# HIST-REL-20261109 — nazwy i kopiowanie celów z historii

Status: **kod i testy celowane gotowe; bez budowy, instalacji i odbioru NVDA**. Zgłoszenie i korekta użytkownika: 9.11.2026.

## Zachowanie widoczne dla użytkownika

- W historii TIDAL i Spotify (`Ctrl+H`) prawa strzałka pokazuje listę rzeczywiście dostępnych celów. Wiersze zawierają zapisane nazwy, np. „Wszystko się może zdarzyć, album, Anita Lipnicka” oraz „Anita Lipnicka, wykonawca”. Pojedynczy cel również najpierw pojawia się na liście; dopiero Enter go otwiera. Lewa strzałka i Escape wracają do zaznaczonego utworu.
- Dla odcinka Spotify lista wskazuje jego nazwany podcast. W historii Podcastów i YouTube wskazuje podcast lub kanał powiązany z odcinkiem, gdy źródło nadal istnieje w Bibliotece AMC. Prawa strzałka czyta wyłącznie lokalną bazę podcastów; Enter otwiera listę odcinków i zaznacza pierwotny odcinek.
- Na głównej liście historii `Ctrl+C` kopiuje tytuł wybranego wpisu. `Ctrl+Shift+C` kopiuje dokładny link do utworu Spotify lub TIDAL, jeżeli istnieje zapisany adres albo poprawna tożsamość katalogowa. W liście pod prawą strzałką `Ctrl+C` kopiuje nazwę wskazanego albumu, wykonawcy, podcastu lub kanału, a `Ctrl+Shift+C` dokładny link tego celu. Gdy linku nie ma, AMC mówi „Ten cel nie ma dokładnego linku” i nie zmienia schowka.
- Lista relacji ma nazwę „Dokąd przejść”. Natywna lista podaje pozycję, np. „1 z 2”. Identyfikatory katalogowe nie są wypowiadane ani kopiowane zamiast nazw.

## Uzasadnienie i decyzje

- Korekta z 9.11.2026 zastępuje wcześniejsze ogólne etykiety „Album” i „Wykonawca” oraz automatyczne wejście TIDAL do jedynego celu. Zaznaczony wpis historii już przechowuje relacje przekazane przez usługę, więc pokazanie listy i skopiowanie nazwy nie pobiera całego katalogu ani nie wymaga żądania sieciowego. Enter może pobrać zawartość wybranego celu.
- Link jest składany tylko z zapisanego, właściwego dla usługi adresu albo z identyfikatora o sprawdzonym formacie Spotify/TIDAL. Wspólna funkcja przyjmuje też adres HTTPS dostarczony przez przyszły adapter; nie zgaduje schematu nieznanej usługi. Dla Podcastów i YouTube używa zapisanego adresu źródła, który może być adresem kanału RSS, a nie stroną do przeglądania.
- Przejście w lokalnej sesji Plików pozostaje poza tym zadaniem zgodnie z uwagą użytkownika z 9.11.2026.

## Materiał do dokumentacji użytkownika

> Otwórz historię przez `Ctrl+H` i zaznacz utwór. `Ctrl+C` kopiuje jego nazwę, a `Ctrl+Shift+C` dokładny link, jeśli jest dostępny. Naciśnij prawą strzałkę, aby wybrać album albo wykonawcę. Lista podaje ich nazwy; strzałka w dół zmienia cel, Enter go otwiera, a lewa strzałka lub Escape wraca do utworu. Na tej liście `Ctrl+C` kopiuje nazwę wskazanego celu, a `Ctrl+Shift+C` jego dokładny link. W historii odcinków Podcastów i YouTube prawa strzałka wskazuje nazwany podcast lub kanał, jeśli nadal jest zapisany w Bibliotece AMC. Brak dokładnego linku jest oznajmiany i nie zastępuje go link wyszukiwania.

## Testy i wynik

- Testy bez widocznego GUI: zestaw historii, źródeł, nawigacji i wyszukiwania Spotify/TIDAL oraz kopiowania 187/187 zaliczonych. Osobny zestaw opisu i przejścia do podcastu, menu i istniejących skrótów kopiowania: 17/17 zaliczonych. Obejmują pojedynczy cel TIDAL, nazwany kanał YouTube, brak dokładnego linku, rozdzielenie usług, Enter dopiero po wyborze, skróty i pomoc.
- `python -m py_compile` dla zmienionych modułów: zaliczony. `git diff --check`: zaliczony.
- Ręczna próba klawiatury i NVDA, budowa, instalacja, publikacja: nie wykonano. Testy bez okna nie dowodzą dokładnej wypowiedzi NVDA ani aktywnej instalacji.

## Znane ograniczenia

- Starszy wpis historii bez identyfikatora relacji nie otrzyma wymyślonego albumu, wykonawcy ani linku. Brak źródła podcastu w Bibliotece uniemożliwia przejście do niego z odcinka.
- Adres źródła podcastu może prowadzić do kanału RSS lub listy filmów, a nie do strony usługodawcy. Brak adresu daje uczciwy komunikat.
- Nowa lista i kopiowanie nie zostały jeszcze sprawdzone z NVDA na zainstalowanej wersji. Nie uruchamiano programu ani nie ingerowano w trwające odtwarzanie lub nagrywanie.

## Wersja i commit

- Baza: gałąź `codex/amc-test-public-20261109`, commit `bb7b1cc945b7662c5e353e71c26bab8d672adf75`.
- Commit zadania: nie wykonano. W czasie pracy pojawiły się równoległe zmiany innych zadań także w `gui.py` i rejestrze; wspólny commit objąłby ich niezweryfikowany zakres. Nie powstała nowa wersja ani instalacja.
- Zmiany dotyczą `wxlite/amc_wx_lite/gui.py`, `service_links.py`, `shortcuts.py`, `menu_model.py` i testów historii, Spotify, podcastów, wyszukiwania oraz menu. W repozytorium pojawiły się równoległe, obce zmiany dotyczące MP3 i kolejki; nie należą do tego zadania.
