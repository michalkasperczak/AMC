# AMC 0.1.0-alpha.412 — konto Sonos do próby logowania

W menu Plik dodano „Konto Sonos”. To samo okno można znaleźć w palecie poleceń, wpisując „Sonos”.

Okno prowadzi przez logowanie w przeglądarce i sprawdzenie jego wyniku. Użytkownik nie musi tworzyć aplikacji deweloperskiej ani wpisywać kluczy.

Dodano bezpieczny zapis dostępu dla bieżącego użytkownika Windows, odnowienie dostępu, ponowienie nieudanego zapisu oraz wylogowanie z potwierdzeniem. Zamknięcie samego okna konta nie oznacza wylogowania.

To pierwszy etap integracji Sonos: obsługa konta przeznaczona do rzeczywistej próby logowania. Ta wersja nie dodaje jeszcze listy głośników, osobnej sesji Sonos ani sterowania jego odtwarzaniem.

Zachowano opcję pozostawania na liście po uruchomieniu stacji radiowej Enterem z poprzedniego wydania. Nie zmieniono dotychczasowych skrótów sesji.

Przeszły pełne zestawy testów Core i Windows (168 testów Windows), dodatkowe testy logowania, odnawiania, koordynatora konta oraz podłączenia okna Sonos. Pakiet zawiera własne środowisko .NET i nie wymaga jego osobnego pobierania.

Instalator sprawdzono na Windows HERMES: nowa wersja uruchomiła się i zachowała poprzedni pusty stan sesji. Żywym NVDA sprawdzono otwieranie okna konta z menu i palety, instrukcję, przyciski oraz zamykanie. Logowanie do rzeczywistego konta Sonos pozostaje do wykonania przez użytkownika.

Dodatek NVDA 0.3.3 i źródła składnika Librespot są niezmienione względem alfa411.

## Rozmiary plików

- AMC-0.1.0-alpha.412.zip: 87843293 bajtów.
- AMC-Librespot-Sources-0.1.0-alpha.412.zip: 74392211 bajtów.
- AMC-NVDA-0.3.3.nvda-addon: 12099 bajtów.
- AMC-Setup-0.1.0-alpha.412.exe: 65280536 bajtów.

## Sumy kontrolne SHA-256

AMC-0.1.0-alpha.412.zip
dd1bcf0becb66e3bba4242a1fa038d21ee823fd9b819b2d3775f6e3d33853c86

AMC-Librespot-Sources-0.1.0-alpha.412.zip
127c6be67b4bf320ed2e47a4bb9a5a04cb71a823c67df49d154ed66228f4e114

AMC-NVDA-0.3.3.nvda-addon
c985a710330bec3e08d137306b202aaeb7371e1dfd1a1406e235f8047bbd7b4b

AMC-Setup-0.1.0-alpha.412.exe
ed0fdb14aadca7a96f5b206567a82b3d8d4e091d4e36b61360af94aee91daa91


## Uzupełnienie stanu po publikacji — 28 września 2026 r.

Pierwsze rzeczywiste logowanie do konta zostało potwierdzone po opublikowaniu tego wydania. Nie zmienia to zakresu instalatora alfa412: nie ma w nim jeszcze listy głośników, sesji Sonos ani sterowania odtwarzaniem. Aktualny kierunek i stan dalszych prac: [PROJEKT_SONOS_PL.md](PROJEKT_SONOS_PL.md).

Powyższy opis i sumy kontrolne pochodzą z [opublikowanego wydania](https://github.com/michalkasperczak/AMC/releases/tag/v0.1.0-alpha.412). Uzupełnienie dotyczy dokumentacji, nie nowej paczki programu.
