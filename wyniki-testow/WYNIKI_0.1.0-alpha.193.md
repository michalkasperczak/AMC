# Wyniki testów AMC 0.1.0-alpha.193

Na początku każdego punktu wpisz wynik po dwukropku. Nie trzeba dopisywać osobno „OK” ani „błąd”; wystarczy opis zaobserwowanego zachowania.

## Globalny prefiks

### AMC-193-01 — sam Plus numeryczny

W Ustawieniach jako globalny prefiks wybierz sam Plus numeryczny i zapisz. Przejdź do innej aplikacji. Naciśnij Plus numeryczny, a następnie `N`.

Oczekiwane: AMC otwiera albo przywołuje widok „Teraz odtwarzane”. NVDA nie milknie, Plus nie trafia do programu znajdującego się pod fokusem, a po wykonaniu polecenia pozostałe klawisze działają zwyczajnie.

Wynik: 

### AMC-193-02 — anulowanie warstwy

W innej aplikacji naciśnij Plus numeryczny, a następnie Escape. Wpisz kilka zwykłych znaków albo użyj strzałek.

Oczekiwane: Escape anuluje oczekiwanie AMC. Klawiatura i NVDA działają dalej, a następny klawisz nie wykonuje polecenia AMC.

Wynik: 

### AMC-193-03 — prefiks Pause

Zmień globalny prefiks na sam klawisz Pause, zapisz ustawienia i poza AMC naciśnij Pause, a następnie `N`.

Oczekiwane: ustawienie zapisuje się bez komunikatu o nieobsługiwanym klawiszu, a sekwencja wykonuje „Teraz odtwarzane”.

Wynik: 

### AMC-193-04 — ponowne uruchomienie i Pomoc klawiszy

Przy ustawionym Plusie numerycznym zamknij i ponownie uruchom AMC. Sprawdź sekwencję Plus numeryczny, `N`. Następnie dwukrotnie użyj `Ctrl+F1` i powtórz sekwencję.

Oczekiwane: prefiks działa po ponownym uruchomieniu oraz po włączeniu i wyłączeniu Pomocy klawiszy. NVDA nie traci mowy.

Wynik: 

## Regresja sterowania Radiem

### AMC-193-05 — Spacja, wyciszenie i nagrywanie

Podczas odtwarzania i nagrywania stacji sprawdź kolejno Spację, `Ctrl+M` i `Shift+Spacja`, za każdym razem używając skrótu ponownie.

Oczekiwane: Spacja steruje tylko odsłuchem, `Ctrl+M` tylko wyciszeniem sesji, a `Shift+Spacja` tylko zapisem nagrania. Zwykła Spacja ani wyciszenie nie tworzą przerwy lub ciszy w nagrywanym pliku.

Wynik: 

## Pakiet

- Pakiet: `publish/AccessibleMediaController-0.1.0-alpha.193`.
