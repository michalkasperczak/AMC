# Delete w kolejce Podcastów i zapisanych filmów YouTube

Stan: 22.11.2026. Kandydat kodu w gałęzi `codex/alpha512-update-candidate-20261122`, commit `76b00d66`. Nie zainstalowano ani nie wydano.

## Zachowanie

- W sesji Podcasty, w otwartej Kolejce, `Delete` usuwa wybrane odcinki i zapisane filmy YouTube z członkostwa kolejki przez hosta. Nie usuwa plików ani wpisów Biblioteki.
- Po usunięciu kursor przechodzi na następny **widoczny** wiersz; przy końcu listy na poprzedni. Filtr listy jest uwzględniany.
- Gdy użytkownik zmieni zaznaczenie podczas zapisu, odświeżenie zachowuje nowszy wybór. Gdy przejdzie do innego widoku, spóźniona odpowiedź nie podmienia listy ani nie wypowiada potwierdzenia. Ponowne `Delete` podczas zapisu nie wysyła duplikatu.
- Po odświeżeniu program podaje jedno potwierdzenie „Usunięto z Kolejki podcastów: [nazwa/liczba]”, bez ponownego nagłówka widoku. Brak zmiany i błąd mają osobne komunikaty.
- Skrót i dostępność polecenia już istniały; nie dodano nowego polecenia do menu, palety ani pomocy.

## Dlaczego

Dotychczasowa ogólna ścieżka Delete liczyła sąsiada z pełnego modelu, bez filtra. W kolejce Podcastów odświeżenie korzystało z domyślnego ogłoszenia widoku, a potwierdzenie operacji mogło dotrzeć później. Nowa ścieżka używa tej samej reguły filtra co natywna lista i odświeża tylko wciąż otwartą Kolejkę.

Notatka zadania wskazywała commit `948c62ff` jako sprawdzony kandydat, ale tego obiektu nie ma w dostępnych lokalnych repozytoriach ani pod tym identyfikatorem na zdalnym GitHubie. Poprawkę wykonano na aktualnym kodzie, bez przypisywania sobie tamtego commitu.

## Sprawdzenia i ograniczenia

- Nowy `test_podcast_queue_delete.py`: **6 passed**. Obejmuje film YouTube w kolejce, filtr, koniec listy, nowszy wybór, zmianę widoku i duplikat żądania.
- `test_podcast_navigation.py` i `test_queue_membership.py`: **26 passed**.
- Szerszy zestaw z `test_profile_mutations.py` uruchamiał rzeczywistego hosta; po 32 początkowych testach nie zakończył się w rozsądnym czasie i został przerwany. Nie jest zaliczony. Nie uruchomiono pełnego zestawu, builda ani prawdziwego GUI/NVDA dla tego commitu.
- Do odbioru: Delete przy aktywnym filtrze na Windows z NVDA, wielokrotne zaznaczenie, rzeczywiste zapisane filmy YouTube, szybka zmiana widoku w trakcie odpowiedzi hosta. Sprawdzić też inne kolejki, szczególnie Spotify, TIDAL i Pliki.

## Szkic instrukcji i listy zmian do korekty Michała

W Kolejce Podcastów zaznacz odcinek lub zapisany film YouTube i naciśnij `Delete`. AMC usunie go z kolejki, zachowa widoczny sąsiedni wiersz i poda wynik. Biblioteka i pliki pozostaną na miejscu.

Lista zmian: poprawiono wybór wiersza i komunikat po usunięciu z Kolejki Podcastów, także przy filtrowaniu.
