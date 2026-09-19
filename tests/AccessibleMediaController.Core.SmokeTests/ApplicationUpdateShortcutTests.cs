using AccessibleMediaController.Core.Commands;
using AccessibleMediaController.Core.Configuration;
using AccessibleMediaController.Core.Sessions;
using AccessibleMediaController.Core.Input;

internal static class ApplicationUpdateShortcutTests
{
    private const string CheckUpdatesCommand = "application.checkUpdates";

    public static void Run()
    {
        var profile = KeyboardProfile.CreateDefault();
        var actual = profile.Resolve(KeyChord.Parse("F11"));
        if (actual != CheckUpdatesCommand)
            throw new InvalidOperationException(
                $"F11 ma sprawdzać aktualizacje AMC; przypisano: {actual ?? "brak polecenia"}.");

        if (!CommandCatalog.GetAllCommandIds().Contains(CheckUpdatesCommand))
            throw new InvalidOperationException("Sprawdzanie aktualizacji musi być dostępne także w palecie poleceń.");
        if (CommandCatalog.GetDisplayName(CheckUpdatesCommand) != "Sprawdź aktualizacje AMC")
            throw new InvalidOperationException("Polecenie aktualizacji musi mieć czytelną nazwę.");

        var settings = new AppSettings();
        var actions = new FakeActions(new MediaItem());
        var router = new CommandRouter(new SessionManager(settings), settings, new FakeSink(), actions);
        var executed = router.Execute(CheckUpdatesCommand);
        if (!executed.Handled || actions.ApplicationUpdatesShown != 1)
            throw new Exception("Polecenie aktualizacji nie otwiera obsługi aktualizacji aplikacji.");

        foreach (var chord in new[] { "Shift+F11", "Ctrl+F11", "Alt+F11" })
            if (profile.Resolve(KeyChord.Parse(chord)) == CheckUpdatesCommand)
                throw new InvalidOperationException($"Nowy skrót nie może przejmować {chord}.");
    }
}
