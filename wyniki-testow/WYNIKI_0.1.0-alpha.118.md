# Wyniki testów AMC 0.1.0-alpha.118

Na początku opisz zauważone zachowanie. Nie trzeba przed każdym zadaniem dopisywać osobnego wariantu „OK” lub „błąd”. Po dwukropku wpisuj spację.

## AMC-118-01 — Menu Plik zależne od sesji

W sesji **Pliki lokalne** otwórz menu Plik. Powinny być widoczne: **Otwórz pliki audio**, **Otwórz folder z plikami audio** i **Foldery Biblioteki**. Przejdź do **Radia internetowego** i otwórz menu ponownie. Zamiast tych pozycji powinny być: **Importuj stacje z playlisty** oraz **Dodaj stację radiową**.

## AMC-118-02 — Import playlisty radia

W Radiu naciśnij `Ctrl+O` i wybierz lokalny plik M3U, M3U8, PLS, XSPF albo eksport VRadio JSON. Po zatwierdzeniu program powinien podać liczbę dodanych i ewentualnie pominiętych pozycji, wrócić do Biblioteki radia oraz ustawić fokus na pierwszej nowej stacji. Sam import nie powinien uruchomić dźwięku.

## AMC-118-03 — Duplikaty i wadliwy wpis

Zaimportuj tę samą playlistę drugi raz. Stacje nie powinny się powielić. Jeżeli eksport zawiera pusty, przypadkowy albo uszkodzony adres, AMC powinien go pominąć bez wyświetlania i bez zapisywania jego treści w komunikacie.

## AMC-118-04 — Szybkie przełączanie stacji

Na liście uruchom kolejno kilka różnych stacji, nie czekając za każdym razem na pełne połączenie. Ostatecznie ma grać wyłącznie ostatnio wybrana stacja. Fokus, klawiatura i NVDA mają pozostać dostępne; spóźniona odpowiedź wcześniejszej stacji nie może zastąpić ostatniego wyboru ani wywołać jego błędu.

## AMC-118-05 — MP3, AAC i nieudane połączenie

Sprawdź jedną działającą stację MP3, jedną AAC/AAC+ oraz jeden niedziałający adres. MP3 nie może mieć regresji. Jeżeli bieżący Windows nie zdekoduje AAC, program powinien zakończyć próbę jednym komunikatem o nieudanym odtworzeniu, bez fałszywego startu, przypadkowej częstotliwości próbkowania ani zablokowania następnej stacji.

## AMC-118-06 — Skrót Ctrl+O

W Radiu `Ctrl+O` powinien otworzyć wybór playlisty stacji. Po przejściu do Plików lokalnych ten sam skrót powinien znowu otworzyć wybór plików audio. `Ctrl+Shift+O` pozostaje wyłącznie otwieraniem folderu w sesji lokalnej.
