# AMC Test i AMC Public z jednym dodatkiem NVDA — szkic instrukcji

Status: szkic do korekty i zatwierdzenia przez użytkownika. Dotyczy kandydata AMC 0.1.0-alpha.492 i dodatku NVDA 0.4.2. Instalacja i odbiór na żywo nie zostały jeszcze wykonane.

## Wybór profilu

Po zaktualizowaniu obu programów i dodatku NVDA, gdy fokus jest w AMC Test, skróty dodatku `Ctrl+Windows` sterują Testem. Gdy fokus jest w AMC Public, sterują Public. Dotyczy to także okien dialogowych otwartych przez właściwy program.

Po przejściu do innego programu skróty sterują ostatnio używanym profilem AMC. Dodatek pamięta wybór po ponownym uruchomieniu NVDA. Gdy działa tylko jeden profil i nie było wcześniejszego wyboru, dodatek wybiera ten profil.

Jeżeli oba profile działają, a dodatek nie zna jeszcze wyboru, przejdź do okna właściwego AMC i ponów skrót. Jeśli zapamiętany profil jest zamknięty, dodatek poprosi o wybranie okna; nie skieruje polecenia do drugiego profilu samoczynnie.

## Skróty i aktualizacja

Gesty pozostały takie same. `Ctrl+Windows+I` odczytuje stan wybranego programu, `Ctrl+Windows+P` steruje odtwarzaniem, a `Ctrl+Windows+Alt+R` steruje nagrywaniem bieżącej stacji. Pełną mapę można znaleźć w `NVDA > Ustawienia > Zdarzenia wejścia > AMC` oraz w pomocy dodatku.

Do równoczesnej pracy wymagane są nowe wersje AMC Test, AMC Public i dodatku NVDA. Starszy host AMC korzysta ze wspólnego kanału poleceń i nie obsłuży dodatku 0.4.2. Dane, ustawienia i nagrania obu profili pozostają oddzielne.

Nie należy aktualizować ani restartować AMC lub NVDA w czasie nagrywania. Przed instalacją trzeba sprawdzić nagrania bieżące i zaplanowane w obu profilach.
