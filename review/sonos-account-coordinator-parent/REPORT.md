# Koordynator Sonos — odbiór specyfikacji i trzy dalsze poprawki

Pierwszy szkic powstał bez testów, a wywołanie usługi wykonawcy przerwał timeout. Został zachowany w kopii przed testami. Następny wykonawca dodał testy i cztery naprawy w f5f1f01. Nie przedstawiamy pierwszego szkicu jako napisanego metodą TDD.

Rodzic sam odczytał oba pliki produkcyjne, diff wobec szkicu i testy. Własny build --no-incremental oraz istniejące zestawy: koordynator 18/18, logowanie36/36, refresh58/58, exit0.

## Dodatkowy RED rodzica

Osiem kolejnych testów uruchomiono PRZED nowymi zmianami produkcji: 23/26, exit1. Trzy odtworzone błędy:

1. Sukces odnowienia usuwał IsAwaitingBrowser niezależnej, wciąż czynnej próby logowania. Usunięto ten skutek z instalacji zestawu; zakończenie logowania samo czyści własną sesję.
2. Przejściowa odmowa odbioru (RateLimited) usuwała sesję logowania i uniemożliwiała ponowienie Fetch. Sesję kończą teraz Denied, Canceled i Expired; pozostałe błędy zachowują możliwość ponowienia bez dodatkowego Start.
3. Koordynator przechowywał nieznormalizowany origin, podczas gdy klient i magazyn używały istniejącej konfiguracji URI. Ten sam adres bez końcowego ukośnika powodował odrzucenie zapisu. Koordynator korzysta teraz z SonosLoginBrokerConfiguration i jej Origin.AbsoluteUri.

Nowa atrapa magazynu naprawdę wywołuje produkcyjny serializer oraz sprawdza origin. Nie jest to test natywnego DPAPI — ten magazyn ma oddzielne, wcześniejsze pomiary.

Dodatnie kontrole i pozostałe scenariusze: odtworzenie BrokerMismatch bez kasowania, odwrotna kolejność Start A/B, późny sukces anulowanego Fetch, stare401 po nowym logowaniu, Dispose w trakcie własnego refresh. Sesja jest produkowana przez rzeczywisty SonosLoginClient.StartAsync z syntetycznym handlerem, bez zmiany dostępności konstruktora.

## GREEN rodzica

Po minimalnych poprawkach: 26/26 unikalnych przypadków koordynatora,36/36logowania,58/58refresh, wszystkie exit0. Build0warnings/0errors. Hash źródeł przed i po wykonaniu zgodny. Polecenia i logi w amc_pomoc/sonos-account-coordinator-parent1 oraz sonos-account-coordinator-parent-fix1.

Nie przejęto zapewnienia wykonawcy, że brakowała tylko kombinacja Dispose+Disconnect: jego testy nie wykonywały też części innych wymienionych scenariuszy. Nie przyjęto komentarza przy późnym Pending jako dowodu odrębnej próby późnego Denied. Siedem wcześniejszych mutacji nie dowodzi każdej możliwej kombinacji stanów.

To odbiór specyfikacji lokalnego Core i poprawki w izolatce. Przegląd jakości oraz pomiar połączenia całego koordynatora z rzeczywistym DPAPI są jeszcze osobnymi krokami. Bez GUI, kont, publicznego serwera, publikacji i instalacji.
