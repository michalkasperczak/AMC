using System.Runtime.CompilerServices;

// Zestaw testow Core sprawdza KONTRAKT wewnetrznego StateSnapshotCopier:
// ktore ksztalty umie odlaczyc, a ktore ma jawnie odrzucic. Jawny dostep
// pozwala sprawdzac takze ksztalty nieobecne w aktualnym modelu, bez
// refleksyjnego wywolywania wewnetrznych metod w testach.
[assembly: InternalsVisibleTo("AccessibleMediaController.Core.SmokeTests")]
