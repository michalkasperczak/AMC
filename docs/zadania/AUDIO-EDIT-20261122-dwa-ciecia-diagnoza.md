# AUDIO-EDIT-20261122 — dwa kolejne cięcia, diagnoza lokalna

## Status i źródło

22.11.2026, odrębna kontynuacja zgłoszenia `AUDIO-EDIT-20261119-znikajace-nagrania.md`.
Wydana alpha.513 nie jest tu uznana za poprawkę incydentu. Dwa lokalne cięcia
przeszły, lecz zniknięcia nazwy w katalogu synchronizowanym nie odtworzono.
Żadnego nagrania Michała, profilu iCloud ani działającej instalacji nie zmieniano.

## Uzasadnienie i decyzje

- Incydent Dwójki obejmował dwa różne nagrania, po jednym cięciu każdego,
  pozornie udany zapis, a potem brak pierwotnych nazw i wyniki pod nazwami
  `.amc-cut-...-result.mp3`. Brak dowodu na winę iCloud lub Escape.
- Obecny `AudioClipOriginalEditor.RemoveAsync` tworzy wynik obok źródła,
  sprawdza długość, zastępuje pierwotną ścieżkę z kopią, porównuje SHA-256
  i usuwa kopię przy wyłączonej retencji. `finally` usuwa pliki robocze.
  Historia nagrań przechowuje pierwotną ścieżkę; nie przepisuje jej na plik roboczy.
- Kontrola bezpośrednio po zastąpieniu nie gwarantuje, że inny proces później
  nie zmieni nazwy. Nie dodawano pozornej „naprawy” bez odtworzenia przyczyny.
- Nowy test sprawdza dwa kolejne cięcia na prywatnym NTFS dla WAV i MP3,
  przy wyłączonej retencji kopii. Nie zastępuje próby w osobnym katalogu
  synchronizowanym ani testu żywego Alt+Shift+R/NVDA.

## Dopowiedzenie Michała z 22.11.2026 — Escape po cięciu

Michał przypuszcza, że mógł szybko nacisnąć Escape; odnalazł plik wyniku
z kropką na początku. To nowa hipoteza czasowa, nie ustalona przyczyna.
Odczyt kodu pokazuje, że Escape z odtwarzacza prowadzi do powrotu na listę
i ewentualnej pauzy. Ta ścieżka nie woła `cancel_audio_clip`; jawne anulowanie
jest osobną komendą, a zamknięcie całej aplikacji anuluje trwającą operację.
Powrót na listę może jednak zbiec się ze spóźnionym zakończeniem wątku,
odświeżeniem historii albo zewnętrzną zmianą nazwy. Potrzebna jest osobna
próba z Escape przed i tuż po zakończeniu na sztucznym pliku, z rejestrem
faktycznych ścieżek, bez nagrań użytkownika.

## Kod i testy

- Zmieniono wyłącznie `tests/AccessibleMediaController.Windows.SmokeTests/Program.cs`:
  dodano `--audio-clip-double-cut`, który tworzy sztuczne trzysekundowe WAV
  i MP3, tnie je dwukrotnie, kontroluje pierwotną nazwę, możliwość odczytu,
  skrócenie i brak pozostałych `.amc-cut-...`.
- `dotnet run --no-restore --project tests/AccessibleMediaController.Windows.SmokeTests/AccessibleMediaController.Windows.SmokeTests.csproj -- --audio-clip-double-cut`:
  **exit 0**; „OK: dwa kolejne cięcia WAV i MP3 zachowały pierwotną nazwę na lokalnym dysku”.
- Istniejący `--audio-clip-original`: **exit 1** dla testowego MP3; czytnik Windows
  zgłosił 2,23 s po wycięciu 1 s z około 3 s, podczas gdy asercja oczekuje
  około 2 s (tolerancja 0,2 s). Nie wiadomo jeszcze, czy rozbieżność leży
  w nagłówku/czytniku czasu MP3, czy w zawartości wyniku. Nie zaliczać
  pełnego zestawu testów cięcia na podstawie nowej próby.
- Pierwsza próba kompilacji w ograniczonym sandboxie nie mogła zapisać `bin/obj`;
  po dopuszczeniu pracy w izolowanym worktree przywrócono zależności i testy
  uruchomiły się. To nie był błąd aplikacji.

## Następny krok

1. Na sztucznym MP3 porównać rzeczywistą oś ramek FFmpeg, czas odczytu Windows
   i odsłuch/dekodowanie po jednym oraz dwóch cięciach. W razie potwierdzonego
   błędu granic przygotować RED i małą poprawkę.
2. Odtworzyć kontrolowaną sekwencję zapisu w *nowym, testowym* katalogu
   synchronizowanym, gdy będzie dostępny bez ryzyka dla nagrań użytkownika;
   zapisać stan nazwy przed/po późniejszej synchronizacji i zachować wynik.
3. Po ustaleniu przyczyny sprawdzić Alt+Shift+R, Escape, drugą edycję i
   komunikaty z NVDA. Bez tego incydent pozostaje otwarty.

## Materiał do dokumentacji użytkownika

Na razie nie ma zmiany obsługi wycinania ani nowej wersji programu. W lokalnej
próbie dwa kolejne cięcia zachowały nazwę pliku, ale zgłoszenie znikających
nagrań pozostaje nierozwiązane. Nie wycinaj ponownie jedynej zachowanej kopii
tamtych nagrań. Wynik testu nie jest potwierdzeniem działania na iCloud.

## Wersja i commit

Baza testu: `7751dee7c0d417ce58e941745a0a81ebdfc03ada` (po publikacji
alpha.513). Test i pierwsza diagnoza: `2f93607b0bc6c6aad921bd95e96b815480a2d0c9`.
Kod produkcyjny bez zmian. Pakiet, instalacja, publikacja i żywy test NVDA:
niewykonane.
