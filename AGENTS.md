# AMC — ustalenia projektu

- Kanoniczne repozytorium: `D:\Projekty Codex\Accessible Multimedia Controller`.
- Użytkownik 9 września 2026 r. udzielił stałej zgody na publikowanie zmian
  AMC i przygotowanych wydań w `michalkasperczak/AMC` na GitHubie. Kolejne
  zwykłe publikacje w tym repozytorium nie wymagają ponownego pytania.
- Przed publikacją sprawdzaj zakres zmian i zawartość pakietu. Nie publikuj
  poświadczeń, danych kont, lokalnych kolekcji, prywatnych nagrań ani logów
  użytkownika. Zgoda nie obejmuje innych repozytoriów ani kontaktowania
  zewnętrznych osób w imieniu użytkownika.
- Publikacja źródeł, publikacja ZIP w Releases i uruchomienie programu są
  osobnymi czynnościami. Raportuj ich rzeczywiste wyniki, także blokady.
  Nie zamykaj programu w trakcie nagrywania.
- Po publikacji wydania usuń jego lokalny ZIP z `publish` dopiero po
  zweryfikowaniu, że odpowiadający mu załącznik GitHub Releases został
  skutecznie wysłany i jest dostępny (nazwa, rozmiar i, gdy dostępny, SHA-256).
  Sam push źródeł nie oznacza wysłania ZIP-a. Niewysłane i niezweryfikowane
  paczki pozostaw; nie usuwaj przy tym rozpakowanej bieżącej wersji programu.
- Priorytet TIDAL: zweryfikowane pełne odtwarzanie, nie deklarowanie próbek
  jako pełnych utworów. Ustalenia i granice: `PROJEKT_TIDAL_PL.md`.
- Wynik zwykłego logowania do Embed i analiza jego kodu:
  `TIDAL_WERYFIKACJA_PELNEGO_ODTWARZANIA_2026-09-09.md`. Nie prosić ponownie
  o identyczne logowanie bez nowego dowodu. Za osobną zgodą użytkownika pytanie
  wysłano do TIDAL: https://github.com/orgs/tidal-music/discussions/384.
  Nie dublować zgłoszenia. Zgoda nie obejmuje podłączania tożsamości Nostr.
- Testy muszą kończyć się niezerowym kodem i diagnostyką po niepowodzeniu,
  nie pozostawionym oknem awarii CLR. Test negatywny nie jest zaliczeniem
  funkcji. Profile WebView2 testów są odizolowane od konta i stanu AMC.
- Etykiety dostępności list, pól kombi i menu muszą być tekstem użytkowym,
  nigdy domyślną reprezentacją obiektu, enuma ani identyfikatorem komendy.
  Testuj początkowy fokus, strzałki, ponowne otwarcie i powrót po zamknięciu.
