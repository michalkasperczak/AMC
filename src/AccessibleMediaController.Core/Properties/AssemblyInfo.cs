using System.Runtime.CompilerServices;

// Zestaw testow Core sprawdza KONTRAKT wewnetrznego StateSnapshotCopier:
// ktore ksztalty umie odlaczyc, a ktore ma jawnie odrzucic. Bez tego dostepu
// mozna by go sprawdzic tylko przez aktualny model, wiec kazda odmowa dla
// ksztaltu nieobecnego dzisiaj w modelu byla nietestowalna.
[assembly: InternalsVisibleTo("AccessibleMediaController.Core.SmokeTests")]
