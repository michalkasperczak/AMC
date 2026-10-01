# AMC 0.1.0-alpha.415 — Biblioteka i presety Sonosa

## Biblioteka i Ulubione

Uporządkowano sesję Sonosa. Ulubione pokazują materiały zapisane w Sonosie, a nie głośniki dodane do lokalnej kolekcji. Biblioteka udostępnia materiały Sonosa oraz własne stacje zapisane w AMC.

Pod Ctrl+F5 można wybrać cel odtwarzania, sprawdzić głośniki i zmienić skład grupy. Sam wybór składu nie uruchamia automatycznie muzyki. Program sprawdza stan urządzeń i informuje o konfliktach przed zmianą.

## Presety Ulubionych i własnych stacji

Ctrl+Alt+Shift+P otwiera przypisywanie zaznaczonego materiału. Ctrl+Alt+P pokazuje listę presetów, a Ctrl+Shift+cyfra uruchamia przypisany materiał.

Domyślnie preset korzysta z bieżącego celu Sonosa. Opcja „Zawsze w tym miejscu” pozwala zapamiętać konkretny zestaw głośników.

Gdy rozpoznany materiał już gra, powtórzenie presetu podaje tylko jego nazwę, bez ponownego ładowania. Po pauzie wznawia odtwarzanie. Przy niepewnym odczycie program odmawia zamiast przejmować inne źródło na podstawie samej nazwy.

## Własne stacje

W „Moich stacjach” można dodać, zmienić i usunąć nazwę oraz adres radia. Te wpisy są przechowywane w AMC; nie są automatycznie dopisywane do Ulubionych na koncie Sonosa.

Naprawiono odrzucanie identyfikatora sesji zawierającego znak @. Błąd blokował uruchomienie własnego adresu mimo przyjęcia utworzenia sesji przez Sonosa.

## Zakres sprawdzenia i ograniczenia

Nowa obsługa list, dialogów i skrótów została sprawdzona z żywym NVDA. Rzeczywisty Sonos przyjął adres testowej stacji, zgłosił odtwarzanie i zgodny identyfikator materiału, a następnie potwierdził zatrzymanie. Na prośbę użytkownika dalszy odsłuch własnych adresów pozostawiono do ręcznego sprawdzenia w gotowym AMC; nie deklarujemy potwierdzenia słyszalności dźwięku w tej próbie.

Podstawowa lista i uruchamianie natywnych playlist Sonosa pozostają dostępne. Rozpoznawanie ich ponownego uruchomienia bez przeładowania jest odłożone i nie jest objęte zapewnieniem o powtórzeniu presetu bez restartu. Nie dotyczy to playlist usług zapisanych w Ulubionych Sonosa, które mają rozpoznawalną tożsamość materiału.
