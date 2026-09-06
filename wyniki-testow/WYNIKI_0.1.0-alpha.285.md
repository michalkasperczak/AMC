# AMC 0.1.0-alpha.285 — nazwy strumieni WiiM

1. W sesji WiiM uruchom strumień „1”, a następnie przechodź `Alt+Page Down`
   przez „2”, „3”, „Poznań” i „357”. Oczekiwane: NVDA czyta właśnie te nazwy,
   nigdy adres HTTP/HTTPS ani podpisany link.

2. Wróć `Alt+Page Up` przez te same wpisy. Oczekiwane: nazwy i kolejność są
   identyczne jak na liście Strumieni sieciowych AMC.

3. Otwórz strumień, który przekierowuje na inny adres, i pozostaw odtwarzacz
   przez kilkanaście sekund, aby wykonało się kilka odświeżeń WiiM. Oczekiwane:
   nazwa zapisana w AMC pozostaje w odtwarzaczu, tytule i komunikatach; URL nie
   pojawia się jako nazwa stacji ani audycji.
