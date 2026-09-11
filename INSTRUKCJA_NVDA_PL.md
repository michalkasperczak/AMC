# AMC — dodatek NVDA 0.2.1, dla AMC alpha 342

Pakiet dodatku: `D:\Projekty Codex\Accessible Multimedia Controller\AMC-NVDA-0.2.1.nvda-addon`

Program: `D:\Projekty Codex\Accessible Multimedia Controller\publish\AccessibleMediaController-0.1.0-alpha.342\AccessibleMediaController-0.1.0-alpha.342.exe`

Najpierw, gdy nic nie nagrywasz, zamknij wcześniejsze AMC i uruchom alpha 342. Zainstaluj aktualizację dodatku, wykonując instrukcje NVDA. Dodatek ma 65 poleceń. Bezpośrednie presety wymagają AMC 342; pozostałe rozszerzenia wymagają co najmniej AMC 341. Wymagane NVDA 2026.1 lub nowsze. Nie instalowano dodatku ani nie uruchomiono nowej aplikacji automatycznie.

Własne przypisania w NVDA pozostają zachowane i mogą mieć pierwszeństwo. Zmienisz je w NVDA > Ustawienia > Zdarzenia wejścia > AMC. Prefiks AMC nadal jest osobnym sposobem sterowania. Free Radio powinno pozostać wyłączone albo mieć inne skróty.

## Dlaczego strzałki odtwarzają po pauzie i przechodzą po presetach?

Ctrl+Windows+P wstrzymuje lub wznawia bieżący odsłuch. Lewo/prawo, z Ctrl+Windows, wybiera i rozpoczyna inny element — tak samo jak Page Up/Page Down w odtwarzaczu AMC. Pauza nie zamienia tych klawiszy w samo przesuwanie kursora.

Zakres zależy od miejsca rozpoczęcia odtwarzania. Po uruchomieniu stacji z Ulubionych przechodzisz po Ulubionych; po uruchomieniu z presetu — po presetach. Samo otwarcie innej listy nie zmienia trwającej listy odtwarzania. Uruchom z niej wybrany element, aby wybrać nowy zakres. Ctrl+Windows+Alt+I odczytuje zakres i pozycję.

Ctrl+Windows+Alt+Page Up/Page Down jawnie wybiera poprzedni/następny zapisany preset, z pomijaniem pustych miejsc i zawijaniem na końcu. Folder lub playlista pod presetem wymaga wejścia przez jawną listę presetów; dodatek nie otwiera po cichu jej zawartości. W WiiM zwykłe lewo/prawo to transport urządzenia, natomiast Alt+Page Up/Page Down (z Ctrl+Windows) korzysta z dotychczasowej nawigacji po presetach lub zapisanych strumieniach.

## Mapa klawiszy

**Do każdej kombinacji w tabelach dodaj Ctrl+Windows.** Przykład: „Shift+R” oznacza Ctrl+Windows+Shift+R. Skróty działają od razu, bez prefiksu.

### Odsłuch i informacja

| Dodatkowe klawisze | Działanie |
| --- | --- |
| I | Bieżące nagranie, sesja i stan |
| P | Odtwórz lub wstrzymaj odsłuch |
| lewo | Odtwórz poprzedni element bieżącej listy odtwarzania |
| prawo | Odtwórz następny element bieżącej listy odtwarzania |
| góra | Głośniej o 5 procent |
| dół | Ciszej o 5 procent |
| M | Wycisz lub przywróć dźwięk sesji |
| J | Cofnij o 10 sekund |
| K | Przewiń o 10 sekund |
| E | Czas od początku / pozycja w buforze radia |
| R | Czas pozostały / opóźnienie względem transmisji radiowej |
| T | Czas całkowity / długość bufora radia |
| Alt+I | Odczytaj listę używaną przez następny i poprzedni element |
| Shift+M | Wycisz lub przywróć dźwięk wszystkich sesji AMC |
| Shift+J | Cofnij odtwarzanie o 30 sekund |
| Shift+K | Przewiń odtwarzanie o 30 sekund |
| Alt+J | Cofnij odtwarzanie o minutę |
| Alt+K | Przewiń odtwarzanie o minutę |
| przecinek | Zmniejsz prędkość odtwarzania bez zmiany wysokości dźwięku |
| kropka | Zwiększ prędkość odtwarzania bez zmiany wysokości dźwięku |
| Shift+kropka | Przywróć normalną prędkość odtwarzania |
| Home | Przejdź do początku nagrania lub bufora radia |
| End | Przejdź do końca nagrania lub radia na żywo |

### Sesje i presety

| Dodatkowe klawisze | Działanie |
| --- | --- |
| Shift+Tab | Poprzednia sesja AMC |
| Tab | Następna sesja AMC |
| Alt+Page Up | Odtwórz poprzedni preset; w WiiM preset lub zapisany strumień |
| Alt+Page Down | Odtwórz następny preset; w WiiM preset lub zapisany strumień |

Bezpośrednie wywołanie: **Ctrl+Windows+Alt+1–9, 0, minus, równa się** — odpowiednio presety 1–12 bieżącej sesji. 0 uruchamia preset 10, minus 11, równa się 12. Wywołanie pustego miejsca podaje komunikat; nigdy niczego nie zapisuje. Stacja lub nagranie rusza w tle. Folder, album lub playlista jawnie otwiera swoją zawartość w AMC (pod kontrolą zgody Windows na fokus). WiiM używa lokalnego przypisania do gotowego presetu urządzenia.

### Tworzenie bez wychodzenia z listy

- Na liście presetów (Ctrl+Windows+Alt+P lub Ctrl+Alt+P w AMC) jest przycisk **Utwórz nowy preset**. Dotyczy elementu, dla którego otwarto listę; jego nazwa jest podana w opisie. Nie wybiera przypadkowo innego aktualnie grającego utworu. Wybierz miejsce i zatwierdź Enterem. Zajęte miejsce wymaga dotychczasowego, dodatkowego potwierdzenia. Escape anuluje; po zapisie lub anulowaniu wracasz do listy presetów.
- W WiiM przycisk **Przypisz skrót AMC do wybranego presetu** działa na zaznaczonym, istniejącym presecie. Nie tworzy ani nie zmienia presetów urządzenia.
- Na widoku Playlisty (Ctrl+Windows+Shift+P albo Ctrl+P w AMC) jest **Utwórz nową playlistę**. Przycisk Nowa w oknie dodawania do playlist również pozostaje. Dla TIDAL utworzenie używa API konta; nie powoduje dodania przypadkowego utworu.
- Enter i Spacja na liście presetów nadal tylko uruchamiają. Na przycisku wykonują funkcję przycisku, nie ukrytej listy.

### Zakładki, rozdziały i kolekcje

| Dodatkowe klawisze | Działanie |
| --- | --- |
| B | Dodaj zakładkę w bieżącym nagraniu |
| Shift+Page Up | Przejdź do poprzedniej zakładki bieżącego nagrania |
| Shift+Page Down | Przejdź do następnej zakładki bieżącego nagrania |
| Alt+Shift+Page Up | Przejdź do poprzedniego rozdziału bieżącego nagrania |
| Alt+Shift+Page Down | Przejdź do następnego rozdziału bieżącego nagrania |
| Shift+U | Dodaj lub usuń bieżące nagranie z ulubionych |
| Shift+Q | Dodaj lub usuń bieżące nagranie z kolejki |

### Nagrywanie

| Dodatkowe klawisze | Działanie |
| --- | --- |
| Alt+R | Rozpocznij lub zakończ nagrywanie bieżącej stacji |
| Shift+R | Wstrzymaj lub wznów nagrywanie bieżącej stacji bez zmiany odsłuchu |
| Shift+T | Kontynuuj nagrywanie bieżącej stacji w nowym pliku |

### Otwieranie okna AMC

| Dodatkowe klawisze | Działanie |
| --- | --- |
| F6 | Otwórz okno AMC z bieżącym odtwarzaczem |
| L | Otwórz bibliotekę bieżącej sesji w oknie AMC |
| U | Otwórz ulubione bieżącej sesji w oknie AMC |
| Alt+Q | Otwórz kolejkę bieżącej sesji w oknie AMC |
| Shift+P | Otwórz playlisty bieżącej sesji w oknie AMC |
| H | Otwórz historię odtwarzania bieżącej sesji w oknie AMC |
| Alt+P | Otwórz presety bieżącej sesji w oknie AMC |
| Alt+B | Otwórz listę zakładek w oknie AMC |
| Alt+C | Otwórz listę rozdziałów w oknie AMC |
| Shift+S | Otwórz wybór sesji w oknie AMC |
| A | Otwórz wybór urządzenia audio bieżącej sesji w oknie AMC |
| Alt+F | Otwórz wyszukiwanie bieżącej sesji w oknie AMC |
| F2 | Otwórz paletę poleceń w oknie AMC |
| Alt+H | Otwórz listę nagrywanych stacji w oknie AMC |
| Shift+H | Otwórz harmonogramy nagrywania w oknie AMC |
| Alt+S | Otwórz listę rozpoznanych utworów w oknie AMC |

## Ważne rozróżnienia

- P — pauza odsłuchu; M — wyciszenie odsłuchu. Żadne z nich nie pauzuje nagrywania. W radiu pauza odsłuchu korzysta z dostępnego bufora timeshift.
- Shift+R — pauza/wznowienie nagrywania bieżącej stacji. Odsłuch jest niezależny. Zapis oryginalnego strumienia bez konwersji może nie obsługiwać pauzy; AMC poda komunikat.
- Alt+R — rozpoczęcie/zakończenie nagrywania bieżącej stacji. Dotyczy również istniejącego nagrania z harmonogramu tej stacji, zgodnie z zasadami AMC; nie zatrzymuje wszystkich stacji.
- Shift+T — ręczny podział bieżącego nagrania. Harmonogram zachowuje folder i czas zakończenia.
- Shift+U i Shift+Q dotyczą **otwartego nagrania**, nie zaznaczenia ukrytej listy. Kolejka nie jest dostępna w sesji Radio.
- Zmiana sesji nie wybiera urządzenia wyjściowego i sama nie wyłącza nagrywania.
- Polecenia odsłuchu pozostają w tle. Grupa „Otwieranie okna AMC” oraz bezpośredni preset folderu, albumu lub playlisty jawnie przywołują program. Po otwarciu działają zwykłe skróty okna AMC; Escape zamyka dialog zgodnie z jego dotychczasową logiką.
- Rozdziały i zakładki dotyczą bieżącego nagrania. Ich nawigacja jest dostępna także wtedy, gdy główne okno pokazuje listę. W radiu B dodaje zakładkę do trwającego nagrania, nie do ulotnego timeshiftu.
- Funkcja niedostępna w danej sesji nie przełącza jej na inną. WiiM i usługi zachowują ograniczenia swoich interfejsów; ta aktualizacja nie odblokowuje pełnych utworów TIDAL.
- Odpowiedź na jawny globalny skrót może być czytana poza AMC. Automatyczne komunikaty Shazam nie zmieniają swoich ustawień. Asynchroniczny błąd usługi może pojawić się normalną drogą AMC; przyjęcie polecenia nie oznacza potwierdzenia odtwarzania.

## Konflikty i bezpieczeństwo

Zachowane, wcześniej wybrane Ctrl+Windows+lewo/prawo zastępują przełączanie pulpitów Windows podczas działania dodatku. Nie przypisano Ctrl+Windows+Enter, cyfr, C, D, F, N, O, Q, S, V, Spacji, F4 ani Ctrl+Windows+Shift+B. Lista rezerwacji opiera się na [dokumentacji Microsoft](https://support.microsoft.com/en-us/accessibility/windows/keyboard-shortcuts-in-windows); nie gwarantuje braku skrótów innych zainstalowanych programów.

Ctrl+Windows+Shift+cyfry pozostawiamy Windows (uruchamianie aplikacji z paska jako administrator). Ctrl+Windows+A oraz Tab z tymi modyfikatorami nie figurują w cytowanej mapie jako osobne polecenia systemowe, ale należy sprawdzić dodatki i inne aplikacje. Zwykły Ctrl+Tab poza AMC nie jest przechwytywany. Przecinek, kropka, minus i równa się oznaczają klawisze bloku głównego, nie numerycznego.

Po braku odpowiedzi dodatek nie powtarza komendy. Najpierw sprawdź stan przez I, zwłaszcza przed ponowną pauzą lub przełącznikiem nagrywania. Kolejka ma 4 miejsca i usuwa niepodjęte polecenia starsze niż 1,5 sekundy. Otwarty dialog albo menu AMC blokuje globalne sterowanie do jego zamknięcia.

Jawne otwieranie okna wymaga zgody Windows na przekazanie fokusa. Jeśli system ją odrzuci, dodatek poprosi o Alt+Tab do AMC i ponowienie skrótu. Nie używa wymuszonego okna „zawsze na wierzchu” ani symulowania klawiszy. Nie uruchamia AMC, gdy program jest zamknięty, i nie działa na ekranie blokady.

## Krótka próba własna

1. W NVDA+1 sprawdź P, Alt+I, Alt+P, Shift+R i A, z Ctrl+Windows. Powinny być same opisy, bez wykonywania komend. Wyłącz pomoc.
2. Uruchom stację z Ulubionych. Przejdź do edytora. P, następnie prawo: ma ruszyć następna ulubiona, a fokus pozostać w edytorze. Alt+I podaje „Ulubione”. Uruchomienie z presetu zmienia zakres na „Presety”.
3. Alt+Page Up/Page Down przełącza presety. I odczytuje bieżący stan. M nie wstrzymuje odsłuchu, P go wstrzymuje. Sprawdź granice listy, pustą listę presetów i inny typ sesji.
4. Alt+P, L, U, Shift+S i A jawnie otwierają odpowiednie widoki/okna. Sprawdź pierwszą pozycję, Tab, strzałki, Escape, powtórne otwarcie oraz program zminimalizowany. Nie powinny otworzyć się dwa dialogi.
5. Dla pliku/podcastu: J/K, Shift+J/K, przecinek/kropka/Shift+kropka, B, Shift+Page Up/Page Down. Shift+kropka przywraca normalną prędkość 1,00 razy. Zakres i funkcje zależą od materiału i silnika.
6. Krótkie nagranie testowe radia: Alt+R rozpoczyna; Shift+R pauzuje/wznawia; P zmienia tylko odsłuch; Shift+T dzieli plik; Alt+R kończy. Sprawdź pliki i czas zakończenia harmonogramu, jeśli nagrywasz z niego. Nie przerywaj ważnego nagrania do tego testu.
7. W AMC zaznacz inną stację niż słuchana. W edytorze Shift+U ma zmienić Ulubione słuchanej stacji, nie tej ukrytej pod kursorem. Dwa naciśnięcia przywracają stan.
8. Sprawdź Tab/Shift+Tab z Ctrl+Windows w edytorze, A dla audio oraz wszystkie 12 bezpośrednich presetów: puste, zajęte, radio, plik, folder, WiiM. Sprawdź przyciski tworzenia, anulowanie i powtórne otwarcie; Enter/Spacja na Zamknij nie może uruchomić presetu.
9. Po zamknięciu AMC skrót ma podać brak połączenia, bez zawieszania NVDA. Sprawdź mowę i brajl oraz brak przejmowania fokusa przy szybkiej nawigacji i opóźnionej odpowiedzi sieci.

Testy automatyczne nie zastępują odsłuchu NVDA ani prób rzeczywistych urządzeń. Wyniki tej kompilacji są zapisane osobno. Użytkownik zaakceptował najnowszą wersję do publikacji 11 września 2026 r.; nie oznacza to wykonania wszystkich szczegółowych scenariuszy audytu.
