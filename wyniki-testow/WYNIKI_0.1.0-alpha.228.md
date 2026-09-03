# Wyniki testów AMC 0.1.0-alpha.228

Wpisz poniżej zauważone zachowanie. Po dwukropku zachowaj spację.

AMC-228-01 — awaryjne wyjście audio: Uruchom dźwięk na urządzeniu zewnętrznym, odłącz je, a następnie przez Shift+A wybierz zwykłe głośniki. Odtwarzanie ma rzeczywiście wrócić na działające wyjście; fokus i bieżący element nie mogą zniknąć.

AMC-228-02 — wyjście domyślne: Gdy zapamiętane urządzenie jest niedostępne, uruchom plik, podcast i stację. Każda rzeczywista sesja ma użyć domyślnych głośników Windows zamiast pozostać bez dźwięku.

AMC-228-03 — trwałe wyciszenie sesji: Wycisz jedną sesję przez Ctrl+M, zamknij AMC, uruchom ponownie i sprawdź tę samą sesję. Ma pozostać wyciszona; ponowne Ctrl+M ma przywrócić dźwięk.

AMC-228-04 — trwałe wyciszenie globalne: Wycisz całe AMC przez Ctrl+Shift+M, uruchom program ponownie, a potem wyłącz wyciszenie globalne. Sesja wyciszona wcześniej indywidualnie ma nadal pozostać cicha, pozostałe mają odzyskać dźwięk.
