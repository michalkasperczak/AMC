# AMC 0.1.0-alpha.259 — wyniki testów

Data: 4 września 2026 r.

## Zakres poprawki

- odzyskiwanie znaczników czasu ze starszych adresów `new.tyflopodcast.pl`
  przez odpowiadającą stronę `tyflopodcast.net`;
- obsługa nagłówka rozdziałów umieszczonego wewnątrz komentarza autora;
- jawne, nieciągłe zaznaczanie rozdziałów przez `Ctrl+Spacja`;
- jednoznaczny Enter: zamyka listę, wraca do odtwarzacza i odtwarza wyłącznie
  zaznaczone rozdziały;
- niezależna nawigacja po wszystkich rozdziałach przez
  `Ctrl+Shift+strzałka w lewo/prawo`;
- zgodnościowe zachowanie dawnych `Ctrl+Alt+Page Up/Down`;
- obsługa skrótów rozdziałów także po chwilowym nieprawidłowym fokusie
  wewnątrz otwartego odtwarzacza.

## Testy automatyczne

- pełna kompilacja Release: zaliczona, 0 błędów, 0 ostrzeżeń;
- testy rdzenia: zaliczone;
- testy Windows i dostępności: zaliczone;
- parser Podcasting 2.0, opisów, stron i komentarzy: zaliczony;
- migracja adresu TyfloPodcastu do kanonicznej strony: zaliczona;
- wybór oraz odznaczenie nieprzyległych rozdziałów: zaliczone;
- mapowanie nowych skrótów i dawnych aliasów: zaliczone;
- kontrola technicznych etykiet w UI Automation: zaliczona.

## Test ręczny NVDA

1. Otwórz odcinek TyfloPrzeglądu, który rzeczywiście ma na stronie sekcję
   „Znaczniki czasu”, na przykład odcinek nr 300. Nie każdy starszy odcinek ma
   opublikowany spis.
2. Naciśnij `Ctrl+Alt+B`.
3. Przejdź `Ctrl+strzałką` do nieprzyległych rozdziałów i każdy przełącz
   `Ctrl+Spacją`. Program powinien powiedzieć „Zaznaczono” albo „Odznaczono”
   oraz liczbę wybranych pozycji.
4. Naciśnij Enter. Lista ma się zamknąć, fokus ma wrócić do odtwarzacza, a AMC
   ma odtworzyć tylko wybrane fragmenty w kolejności czasu i zatrzymać się po
   ostatnim.
5. Otwórz listę ponownie i naciśnij Escape. Nic nie powinno się uruchomić, a
   fokus powinien wrócić tam, skąd lista została otwarta.
6. W odtwarzaczu sprawdź `Ctrl+Shift+strzałka w lewo` oraz
   `Ctrl+Shift+strzałka w prawo`. Skróty mają przechodzić po całym spisie
   rozdziałów, także po rozdziałach, których wcześniej nie zaznaczono.

