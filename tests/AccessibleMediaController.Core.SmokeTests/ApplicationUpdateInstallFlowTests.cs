using AccessibleMediaController.Core.Updates;

internal static class ApplicationUpdateInstallFlowTests
{
    public static void Run()
    {
        var flow = new ApplicationUpdateInstallFlow();
        var order = new List<string>();
        var accepted = flow.RequestClose(() =>
        {
            order.Add("close");
            if (!flow.IsRequested) throw new Exception("Zamykanie nie zna celu aktualizacji.");
            if (!flow.TryLaunchAfterSaving(true, false, explicitRequest =>
            {
                if (!explicitRequest) throw new Exception("Jawna instalacja musi wznowić program.");
                order.Add("install");
                return true;
            })) throw new Exception("Po zapisie nie uruchomiono instalacji.");
            return true;
        });
        if (!accepted || !order.SequenceEqual(new[] { "close", "install" }))
            throw new Exception("Zgoda musi zamknąć AMC i dopiero po zapisie uruchomić instalację.");

        var duplicateCalls = 0;
        if (flow.TryLaunchAfterSaving(true, true, _ => { duplicateCalls++; return true; }) || duplicateCalls != 0)
            throw new Exception("Ten sam proces uruchomił instalator drugi raz.");
        if (flow.RequestClose(() => throw new Exception("Powtórne zamknięcie po instalacji")))
            throw new Exception("Ponowne żądanie zostało zaakceptowane.");

        var failedClose = new ApplicationUpdateInstallFlow();
        try { failedClose.RequestClose(() => throw new IOException("test zapisu")); }
        catch (IOException) { }
        if (failedClose.IsRequested)
            throw new Exception("Wyjątek zamykania pozostawił zgodę na późniejszą instalację.");

        var cancelled = new ApplicationUpdateInstallFlow();
        if (cancelled.RequestClose(() => false) || cancelled.IsRequested)
            throw new Exception("Anulowanie zamknięcia musi anulować żądanie instalacji.");
        if (cancelled.TryLaunchAfterSaving(true, false, _ => throw new Exception("Instalacja po anulowaniu")))
            throw new Exception("Anulowana instalacja została uruchomiona.");

        var notSaved = new ApplicationUpdateInstallFlow();
        notSaved.RequestClose(() =>
        {
            if (notSaved.TryLaunchAfterSaving(false, true, _ => throw new Exception("Instalacja bez zapisu")))
                throw new Exception("Błąd zapisu nie zatrzymał aktualizacji.");
            return false;
        });

        var automatic = new ApplicationUpdateInstallFlow();
        if (!automatic.TryLaunchAfterSaving(true, true, explicitRequest =>
            explicitRequest ? throw new Exception("Zwykłe zamknięcie udaje jawne żądanie instalacji") : true))
            throw new Exception("Zwykła dozwolona aktualizacja przy zamykaniu została pominięta.");
    }
}
