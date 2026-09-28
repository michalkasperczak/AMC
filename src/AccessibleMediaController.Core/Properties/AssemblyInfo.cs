using System.Runtime.CompilerServices;

// Zestaw testow Core sprawdza KONTRAKT wewnetrznego StateSnapshotCopier:
// ktore ksztalty umie odlaczyc, a ktore ma jawnie odrzucic. Jawny dostep
// pozwala sprawdzac takze ksztalty nieobecne w aktualnym modelu, bez
// refleksyjnego wywolywania wewnetrznych metod w testach.
[assembly: InternalsVisibleTo("AccessibleMediaController.Core.SmokeTests")]

// Zestaw testow Windows mierzy sesje Sonos na PRAWDZIWYM MainWindow i musi
// zaplanowac UDANE odnowienie dostepu przez syntetyczna bramke. Fabryki wynikow
// odnowienia sa wewnetrzne, zeby produkcyjne warstwy nie tworzyly ich z niczego.
[assembly: InternalsVisibleTo("AccessibleMediaController.Windows.SmokeTests")]
