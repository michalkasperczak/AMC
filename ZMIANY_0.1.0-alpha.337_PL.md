# AMC 0.1.0-alpha.337

- Wznowienie TIDAL po pauzie przygotowuje aktualne poświadczenie tak jak
  otwarcie utworu; wygasły token może zostać odświeżony przez istniejący OAuth.
  Silnik nie przechodzi na odtwarzanie anonimowe, gdy logowanie jest niepełne.
- Brak poświadczenia podczas startowego sprawdzania przez SDK jest zgłaszany
  kodem oczekiwanym przez ten moduł, bez nieobsłużonego błędu inicjalizacji.
- Nowy przycisk „Diagnostyka odtwarzania” w Ctrl+F5 (Alt+D wewnątrz tego
  okna). Czytelny, kopiowalny raport odróżnia dostarczenie logowania, jego
  odczyt przez SDK, rozpoczęcie audio i odpowiedź o rodzaju materiału.
- Rodzaj materiału i powód próbki są sprawdzane także po starcie/wznowieniu,
  gdy zdarzenie przejścia zostało pominięte. Niezmienione dane nie zaśmiecają logu.
- Powrót z ustawień konta otwartych w odtwarzaczu nie kieruje już fokusa na
  ukrytą listę. Kopiowanie raportu nie zamyka okna.
- Zaktualizowano objaśnienia logowania i aktualnego stanu Playera w ustawieniach.
- Budowanie pozostawia cały folder uruchomionej starszej wersji; nie próbuje
  usuwać jej bibliotek, zanim napotka zablokowany EXE. Nie zamyka programu.

Pełne odtwarzanie TIDAL nadal nie jest potwierdzone. Nie zmieniano tożsamości
aplikacji, zakresów zgody, konta, chronionych adresów audio ani oficjalnego SDK.
Nie dodano automatycznego otwierania strony TIDAL i nie trzeba logować się ponownie
wyłącznie z powodu instalacji tej wersji.

Instrukcja testu: `INSTRUKCJA_0.1.0-alpha.337_PL.md`.
