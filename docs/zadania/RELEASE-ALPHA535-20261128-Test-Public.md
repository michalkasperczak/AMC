# AMC Test i Public alpha.535 — wydanie korekcyjne / corrective release

Data: 28.11.2026. Wersja źródeł 0.1.0-alpha.535, commit b1eb0799f838e7cce38cb7bdfca73f4f58186ca5, gałąź codex/heos-next-test-20261123. GitHub Actions 46091401482 zbudował oba profile z tego dokładnego commitu; oba zadania zakończyły się sukcesem. To odrębna poprawka po opublikowanej alpha.534, której zapis jest w RELEASE-ALPHA534-20261128-Test-Public.md.

## Zachowanie / Behavior

Ctrl+Shift+Enter został wycofany w obecnym AMC wxPython zgodnie z wcześniejszą decyzją użytkownika. Shift+Enter nadal jednorazowo ustawia zaznaczony materiał jako następny w Plikach, Podcastach i Radiu Archiwiście. Przełączenie członkostwa bloku „następne” pozostało dostępne przez menu, paletę Ctrl+Shift+K i pomoc, bez skrótu. W menu presetów poprawiono jednoznaczne klawisze dostępu. Aktualizator przyjmuje zgodne starsze manifesty instalacji, zachowując izolację Test/Public i odmowę obcego identyfikatora. Nie dodaje nowego polecenia użytkownika. Wspólne funkcje alpha.534 pozostają w obu profilach; Apple Music i HEOS są tylko w Test.

English: Ctrl+Shift+Enter is free in the current wxPython app. Shift+Enter still places the selection next; the next-block toggle remains in the menu, command palette, and help without a key. The updater accepts compatible older installed profiles while rejecting a foreign profile or install ID. Test includes Apple Music and HEOS; Public does not.

## Dowody źródła i kompilacji / Source and build evidence

Poprawka aktualizatora: 49/49 testów bezokienkowych, w tym starszy Public i odmowy niezgodnych profili. Korekta skrótu, menu, pomocy i presetów: 113/113 testów celowanych. Wcześniejsze testy funkcji alpha.534, build C# i testy protokołu opisuje jej rekord wydania. GitHub Actions sprawdził czystość źródła, profil, wersję, runtime, kompilację i potwierdzenie integralności instalatorów. Ostrzeżenie o Node.js 20 w przypiętych akcjach nie było błędem kompilacji.

- Test: tag test-2026.11.28.0208, instalator AMC-Test-2026-11-28-0208.exe, 214635685 bajtów, SHA-256 5a638818d2ab56ea303972093f83bf7789e6db2acff43107fae18b54c11f2f92.
- Public: tag public-2026.11.28.0208, instalator AMC-Public-2026-11-28-0208.exe, 134094832 bajty, SHA-256 c92113118b1d8b29f7a366efea2ae109e4b62f61c6d1d6d48b64f1520d640e95.

Pobrane artefakty obu zadań mają zgodny profil, wersję, commit źródła, wynik skanowania sekretów, kompilator exit 0, rozmiary i SHA-256 instalatorów. Oba manifesty amc-update-*.json przeszły rzeczywisty walidator aktualizacji AMC. Opisy PL/EN są w plikach release notes, a szkic instrukcji i listy zmian pozostaje do korekty użytkownika w ALPHA535-instrukcja-i-lista-zmian-szkic.md.

## Instalacja lokalna / Local installation

Przed podmianą Test skontrolowano interfejs: lista aktywnych nagrań była pusta, a najbliższy włączony plan przypada 28.11.2026 o 7:45. Projektowy aktualizator miał prawo odmówić zamknięcia przy nowym nagrywaniu. Public alpha.534 → alpha.535 i Test alpha.532 → alpha.535 zakończyły się kodem 0. Oba lokalne AMC.exe mają ProductVersion 0.1.0-alpha.535; ich manifesty wskazują właściwy profil, powyższy commit i sourceDirty=false. Test uruchomił się ponownie jako jedna instancja launcher/Python/host. Aktualizator zgłosił jedynie brak przyznania oknu pierwszego planu; instalacja i start zostały potwierdzone osobno.

Katalogi: D:\Projekty Codex\AMC Public i D:\Projekty Codex\AMC Python. Pobrane instalatory i potwierdzenia przechowywane są w D:\Projekty Codex\AMC - integracja wxPython\work\release-alpha535-20261128 do odbioru użytkownika.

## Publikacja GitHub / GitHub publication

Przesyłanie dużych EXE przez lokalne łącze było wolne; przerwano je bez usuwania szkiców. Jednorazowy GitHub Actions 46095109137, commit pomocniczego workflow eff491a59e20ae86276d9f103d2833858a1b64be, pobrał dokładnie sprawdzone artefakty builda 46091401482, ponownie zweryfikował ich rozmiary i SHA-256 oraz opublikował oba istniejące szkice. Oba zadania publikacji zakończyły się sukcesem. Commit pomocniczego workflow nie zmienia źródła aplikacji w wydaniu; oba tagi wskazują źródło b1eb0799f838e7cce38cb7bdfca73f4f58186ca5.

Zdalny odczyt GitHub potwierdził: Test jest opublikowany jako prerelease, Public jako stabilny release; żadnego szkicu nie pozostawiono. Każde wydanie ma cztery zasoby w stanie uploaded (EXE, plik integralności, manifest aktualizacji, dowód kompilacji). Rozmiar i SHA-256 wszystkich ośmiu zasobów odpowiadają lokalnym plikom. Endpoint releases/latest wskazuje public-2026.11.28.0208.

- Test: https://github.com/michalkasperczak/AMC/releases/tag/test-2026.11.28.0208
- Public: https://github.com/michalkasperczak/AMC/releases/tag/public-2026.11.28.0208

## Ograniczenia / Acceptance still open

Rzeczywisty odczyt NVDA i próba nowych zachowań w TIDAL, Podcastach i YouTube wymagają odbioru użytkownika w codziennej pracy. AirPlay odłożono do wspólnej próby za dnia; to wydanie nie jest potwierdzeniem działania odbiorników. Nie publikowano szkicu instrukcji jako zatwierdzonej dokumentacji.