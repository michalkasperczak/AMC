# AMC Test i AMC Public z jednym dodatkiem NVDA — szkic instrukcji

Status 10.11.2026: szkic do korekty i zatwierdzenia przez użytkownika. Wydania AMC 0.1.0-alpha.492 opublikowano; Public zainstalowano. Aktualizacja Test i dodatku NVDA 0.4.2 oraz odbiór na żywo pozostają do wykonania.

## Wybór profilu

Po zaktualizowaniu obu programów i dodatku NVDA, gdy fokus jest w AMC Test, skróty dodatku `Ctrl+Windows` sterują Testem. Gdy fokus jest w AMC Public, sterują Public. Dotyczy to także okien dialogowych otwartych przez właściwy program.

Po przejściu do innego programu skróty sterują ostatnio używanym profilem AMC. Dodatek pamięta wybór po ponownym uruchomieniu NVDA. Gdy działa tylko jeden profil i nie było wcześniejszego wyboru, dodatek wybiera ten profil.

Jeżeli oba profile działają, a dodatek nie zna jeszcze wyboru, przejdź do okna właściwego AMC i ponów skrót. Jeśli zapamiętany profil jest zamknięty, dodatek poprosi o wybranie okna; nie skieruje polecenia do drugiego profilu samoczynnie.

## Skróty i aktualizacja

Gesty pozostały takie same. `Ctrl+Windows+I` odczytuje stan wybranego programu, `Ctrl+Windows+P` steruje odtwarzaniem, a `Ctrl+Windows+Alt+R` steruje nagrywaniem bieżącej stacji. Pełną mapę można znaleźć w `NVDA > Ustawienia > Zdarzenia wejścia > AMC` oraz w pomocy dodatku.

Do równoczesnej pracy wymagane są nowe wersje AMC Test, AMC Public i dodatku NVDA. Starszy host AMC korzysta ze wspólnego kanału poleceń i nie obsłuży dodatku 0.4.2. Dane, ustawienia i nagrania obu profili pozostają oddzielne.

Nie należy aktualizować ani restartować AMC lub NVDA w czasie nagrywania. Przed instalacją trzeba sprawdzić nagrania bieżące i zaplanowane w obu profilach.

## Cicha aktualizacja — szkic dla następnego wydania

W następnym wydaniu cicha aktualizacja AMC zainstaluje także dołączony dodatek AMC do NVDA, jeżeli dotychczasowy jest starszy lub go brakuje, a potem uruchomi ponownie NVDA. Przed aktualizacją oba profile AMC muszą być zamknięte. Zakończ trwające nagrania, a następnie ponów aktualizację. Ta poprawka jest obecnie przetestowana w źródłach, lecz nie została jeszcze zbudowana ani zainstalowana.

Po aktualizacji `Ctrl+Windows+P` steruje odtwarzaniem, `Ctrl+Windows+Alt+P` otwiera presety, a `Ctrl+Windows+Shift+P` otwiera playlisty. Skróty są jednakowe w AMC Test i AMC Public.
