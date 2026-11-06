# AMC Test alpha.481 — publikacja i instalacja

Data: 6 listopada 2026. Korekta użytkownika z 6 listopada: Page Up/Page Down przewijają strony we wszystkich listach, a zmianę utworu wykonują tylko w odtwarzaczu; zastępuje wcześniejszą interpretację skrótów na listach. Użytkownik zlecił publikację na GitHubie, a następnie aktualizację z GitHuba po upewnieniu się, że AMC nie nagrywa.

## Zachowanie dla użytkownika

W sesji Podcasty i YouTube: Ustawienia → Wybierz źródła skrzynki podcastów… oraz Wybierz kanały do sprawdzania transmisji na żywo…. Ctrl+I otwiera skrzynkę, Ctrl+Shift+I sprawdza wybrane kanały transmisji. Właściwości podcastu pozostają pod Alt+Shift+Enter, ustawienia sesji pod Alt+Ctrl+Enter. W ustawieniach lokalnych i globalnych Alt+strzałka w górę / w dół zmienia kolejność informacji odczytywanych na listach. Page Up/Page Down przewijają listę o stronę. Menu, paleta i pomoc korzystają ze wspólnego modelu poleceń. Podczas aktualizacji AMC wypowiada „Pobieranie i sprawdzanie aktualizacji AMC”; przy nagrywaniu lub chronionym oknie harmonogramu odmawia instalacji.

## Rzeczywisty wynik

- Źródła: commit c872d0dee094ebc4896996222d07a4c9723bd29e, czysta gałąź codex/konfigurowalne-skroty-20261026; zdalny GitHub wskazywał ten sam commit.
- Testy: 308 testów Pythona w zintegrowanym drzewie zaliczonych; po naprawie czterech przestarzałych atrap sesji pełne test_gui_logic.py: 39 zaliczonych. Osiem scenariuszy --youtube-subscription-sync-apply i cztery zestawy protokołu LiteHost zaliczone. Trzy znane braki katalogu i18n poza tym zadaniem pozostały. Nie wykonano ręcznego testu mowy NVDA nowych pól wyboru.
- Build: pakiet AMC Test alpha.481 z czystego c872d0de przeszedł kontrolę integralności i kontrolowany start; instalator AMC-Test-2026-11-06-0144.exe miał 143852920 bajtów, SHA-256 aa584cbd0c0ae391d20d893d60d0a205fe9f3c5441bf676ecf3e7d34e72417b2, kontrola sekretów zaliczona.
- Publikacja: wstępne wydanie test-2026.11.06.0144 opublikowano i odczytowo zweryfikowano, razem z instalatorem, manifestem i sumą kontrolną.
- Instalacja: przed aktualizacją widok Alt+R pokazał zero aktywnych nagrań. Wbudowany aktualizator pobrał instalator z GitHuba, porównał rozmiar i SHA-256, sprawdził bezpośrednio stan nagrywania i harmonogramu, uruchomił instalator. Zainstalowany AMC.exe zgłasza ProductVersion 0.1.0-alpha.481, manifest zainstalowanej paczki wskazuje c872d0de i sourceDirty=false. Uruchomiono jedną instancję AMC Test z tytułem „AMC Test — 06.11.2026, 01:44” i hostem z katalogu tej instalacji. Instalator i pomocnik zakończyły pracę.

## Ograniczenia i dalszy odbiór

Nie potwierdzono jeszcze w NVDA kolejności odczytu i fokusu po Zapisz/Anuluj w nowych oknach ani działania Ctrl+I/Ctrl+Shift+I na realnych źródłach po instalacji. Zmiana skrótów ma testy automatyczne, lecz zgłoszona wcześniej przyczyna klawisza Ctrl+I pozostaje hipotezą do potwierdzenia w działającym AMC. Nie usuwać lokalnych artefaktów wydania przed potwierdzeniem przez użytkownika, że aktualizacja działa.