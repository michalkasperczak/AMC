# AMC Test 0.1.0-alpha.467

W Plikach, Radiu i Podcastach `Shift+M` pozwala dodać drugie lokalne wyjście
dźwięku Windows lub odbiornik AirPlay audio. W tym samym oknie dostępne jest
dotychczasowe dublowanie na Google Cast. `Shift+A` wybiera główne wyjście
lokalne, a `Shift+C` przełącza sesję na Cast.

Poprawiono opisy opcji integracji z plikami Windows. Zaznaczenie typów plików
w AMC udostępnia program na liście wyboru; domyślne skojarzenia Windows
potwierdza się w ustawieniach systemu.

Urządzenia mogą grać z różnym opóźnieniem. AirPlay wymaga FFmpeg dostępnego
w systemie i wykrytego odbiornika RAOP. Automatyczne przejście do następnego
utworu kolejki oraz zmiany tempa i głośności AirPlay nie są jeszcze w pełni
obsługiwane. Dublowany jest dźwięk AMC, bez mowy NVDA i innych aplikacji.

Szczegóły testów i ograniczeń: `docs/zadania/REL-20261102-alpha467-AirPlay-mirroring.md`.
