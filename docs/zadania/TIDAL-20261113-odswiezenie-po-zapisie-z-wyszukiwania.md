# TIDAL-20261113 — odświeżenie kolekcji po zapisie z wyszukiwania

- Status: **kod i testy gotowe; jeszcze bez nowej kompilacji, instalacji i
  publikacji**.
- Zgłoszenie użytkownika: 13 listopada 2026.
- Obszar: wyszukiwanie `Ctrl+F`, wyszukiwanie wszystkich usług `Ctrl+Alt+S`,
  Biblioteka TIDAL, fokus i pamięć podręczna list.

## Potwierdzony objaw i przyczyna

TIDAL potwierdzał dodanie albumu, a oryginalna aplikacja TIDAL od razu go
widziała. AMC zachowywał jednak w pamięci wcześniej wyrenderowane widoki
„Biblioteka” i „Albumy”. Zapis konta był poprawny; nieaktualna była wyłącznie
lista AMC pozostająca pod oknem wyszukiwania.

## Wdrożone zachowanie

- Po potwierdzonym zapisie z wyszukiwania AMC unieważnia tylko widoki, których
  zmiana dotyczy: album — Biblioteka i Albumy, wykonawca — Biblioteka i
  Wykonawcy, playlista — Biblioteka i Playlisty, ulubiony utwór — Ulubione i
  Utwory.
- Licznik wczytanych stron i sygnatura wyrenderowanej listy są usuwane razem z
  odpowiednią stroną. Niezwiązane kolekcje nie są przeładowywane.
- Okno wyszukiwania zachowuje fokus i własny komunikat o wyniku. Po jego
  zamknięciu widoczna pod spodem lista TIDAL jest odświeżana bez drugiej,
  konkurencyjnej wypowiedzi NVDA.
- Ten sam mechanizm działa dla zapisu z listy, kontenera i odtwarzacza. Przy
  usunięciu z otwartej kolekcji pozostaje wcześniejszy kontrakt sąsiedniego
  fokusu i końcowej mowy.

## Testy i wynik

- Unieważnienie wyłącznie `library` i `albums` po dodaniu albumu, z
  pozostawieniem `artists`: zaliczone.
- Usunięcie liczników stron i sygnatury renderowania zmienionego widoku:
  zaliczone.
- Ciche odświeżenie widocznej listy Albumy po zamknięciu wyszukiwarki, z
  zachowaniem poprzednio wskazanego elementu: zaliczone.
- Regresja wyszukiwania, nawigacji i stronicowania TIDAL oraz powierzchni
  poleceń Apple Music: **174/174 testy zaliczone**.
- Końcowa szeroka regresja: **2731 testów i 34 podtesty zaliczone, 117
  pominiętych**. Sześć wyłączeń było jawnych: dwa zastane długi kompletności
  tłumaczeń i cztery testy prawdziwego wxPython, które sprawdzono osobno w
  prywatnym runtime (3/3 GUI Radio Archiwisty oraz `MODELLESS_OK` Sonosa).
- Zapis na koncie został potwierdzony przez użytkownika w oryginalnej aplikacji
  TIDAL. Nowe automatyczne odświeżenie listy AMC wymaga jeszcze odbioru po
  zainstalowaniu kolejnej kompilacji.

## Stan dostarczenia

- Źródła: zatwierdzone w `8ef22969fed875e34f3759a0b3187bc93e4a38c6`;
  końcowy commit wydania `7ef8396849eff477e618920dce39392371381e03`.
- Build: paczka `cbf655af`, 3797 plików, `sourceDirty=false`; kontrolowany
  start zaliczony.
- Instalacja: AMC Test `0.1.0-alpha.499`, użytkowo `13.11.2026, 14:58`,
  uruchomiona z `D:\Projekty Codex\AMC Python`.
- GitHub: prerelease `test-2026.11.13.1458`, trzy zasoby `uploaded`; rozmiar i
  SHA-256 instalatora zgodne z lokalnym plikiem integralności.
- Automatyczne odświeżenie listy po zapisie wymaga jeszcze odbioru użytkownika
  w rzeczywistym koncie TIDAL.
