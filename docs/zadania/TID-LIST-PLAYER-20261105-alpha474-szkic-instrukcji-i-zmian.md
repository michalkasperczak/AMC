# Szkic instrukcji i zmian AMC Test alpha.474

**Projekt do korekty przez użytkownika, nie opublikowana instrukcja.**

## Odczyt list

W ustawieniach programu (`Ctrl+,`) na karcie „Komunikaty dostępności” można osobno wybrać informacje czytane podczas poruszania się po liście strzałkami góra/dół i po naciśnięciu lewej strzałki. Nazwa pozycji zawsze pozostaje na liście. Ustawienia danej sesji (`Ctrl+Alt+Enter`) pozwalają odziedziczyć wybór ogólny albo utworzyć własny. Dostępność pól zależy od danych, które dana sesja rzeczywiście podaje. Zmiana nie powoduje pobierania danych przy każdym ruchu góra/dół.

Lewa strzałka na pliku lokalnym zaczyna od długości, bitrate i rozmiaru, o ile te dane są dostępne. Dotyczy także plików nagrań. TIDAL odczytuje samą długość bez słowa „Czas” i nie wymienia brakującego formatu. Album na liście TIDAL jest czytany jako tytuł, wykonawca i długość bez słowa „album”. Radio Archiwista podaje samą długość. Dla dodanego strumienia Sonos albo WiiM lewa strzałka może podać zmierzony bitrate i format. Brakujący parametr jest pomijany.

## Odtwarzacz i jakość

`J` otwiera skok do czasu bieżącego utworu, `Shift+J` skok do procentu. Zakładki lokalnych plików już działały; TIDAL ma własne zakładki. `Ctrl+H` pokazuje historię w Spotify i TIDAL, a kolejka TIDAL działa z wewnętrznym odtwarzaczem. W TIDAL AMC Test prosi o najwyższą jakość Max; zgodne strumienie FLAC 24 bit są odtwarzane wewnętrznie, a przy niezgodnym wariancie następuje powrót do High. Dla TIDAL wyłączono łagodne narastanie początku po Page Up/Down. Spotify nadal używa oficjalnego Web Playback SDK; jego Lossless nie trafia do tego samego toru audio i wymaga osobnego zadania.

## Do sprawdzenia z NVDA

Po instalacji zbiorczej wersji testowej sprawdzić po kolei: lokalne pliki i nagrania, TIDAL, Spotify, Radio Archiwista, dodany strumień Sonos/WiiM, dziedziczenie i własne opcje sesji, pierwszą pozycję w kontrolkach, strzałki, Anuluj/Zapisz, ponowne otwarcie oraz początek utworu po Page Up/Down. Wyniki ręcznego odsłuchu dopisać do rekordu zadania.
