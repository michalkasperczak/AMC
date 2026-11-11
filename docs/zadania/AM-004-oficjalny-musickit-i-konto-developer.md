# AM-004 — oficjalny MusicKit i konto Apple Developer

- Status: `Oczekuje — nie kupować członkostwa przed wynikiem prywatnego pilota`
- Utworzono: 12 listopada 2026
- Obszar: oficjalne API, broker tokenów, AMC Public

## Cel

Zastąpić nieoficjalną warstwę konta oficjalnym Apple Music API/MusicKit, jeżeli
pilot potwierdzi wartość funkcji i zostanie wskazany formalny właściciel AMC
oraz zespołu Apple Developer.

## Uzasadnienie i decyzje

- Zwykłe Apple ID i abonament Apple Music nie wystarczają. Potrzebny jest
  aktywny zespół Apple Developer, Media ID i klucz Media Services.
- Organizacja może zaprosić członków zespołu; indywidualny właściciel nie daje
  pełnego członkostwa przez samo App Store Connect. Klucz należy do zespołu i
  nie jest plikiem przekazywanym użytkownikom AMC.
- Istnieje już `AccessibleMediaController.AppleMusicTokenBroker` z 12/12
  testów syntetycznych. Nie ma prawdziwego Team ID, Key ID, Media ID ani `.p8`.
- Uprawniona organizacja non-profit, edukacyjna lub publiczna może sprawdzić
  zwolnienie z opłaty. Osoba prywatna nie spełnia tych kryteriów.

## Bramka rozpoczęcia

1. AM-001/AM-002 potwierdzają użyteczną Bibliotekę i pełne odtwarzanie.
2. Użytkownik zatwierdza przejście z laboratorium do rozwiązania oficjalnego.
3. Formalny właściciel produktu zakłada lub wskazuje własny zespół.
4. Account Holder/Admin tworzy Media ID i klucz; sekret trafia tylko do brokera.
5. AMC otrzymuje krótkotrwały token dla jednego HTTPS originu i przechodzi
   pełny test logowania, odnowienia, wylogowania, DRM, NVDA oraz aktualizacji.

## Materiał do dokumentacji użytkownika

Konta deweloperskiego nie trzeba kupować do obecnego prywatnego pilota. Będzie
potrzebne dla wspieranej, oficjalnej i możliwej do publicznego utrzymania
integracji.

## Testy i wynik

Wcześniejszy broker: 12/12 testów syntetycznych. W tym zadaniu nie wykonano
nowego testu, wdrożenia HTTPS, logowania, buildu, instalacji ani publikacji.

## Znane ograniczenia

Koszt, umowa, właściciel zespołu i utrzymanie brokera pozostają nierozstrzygnięte.
Nie używać klucza znajomego ani nie wkładać `.p8` do AMC.

## Wersja i commit

Brak nowej wersji. Commit: do uzupełnienia.
