namespace AccessibleMediaController.Core.Updates;

/// <summary>Łączy jawną zgodę na instalację ze zwykłą ścieżką zamykania i zapisu AMC.</summary>
public sealed class ApplicationUpdateInstallFlow
{
    private bool _installLaunched;
    public bool IsRequested { get; private set; }

    public bool RequestClose(Func<bool> requestClose)
    {
        if (IsRequested || _installLaunched) return false;
        IsRequested = true;
        try
        {
            var accepted = requestClose();
            if (!accepted) IsRequested = false;
            return accepted;
        }
        catch
        {
            IsRequested = false;
            throw;
        }
    }

    public bool TryLaunchAfterSaving(bool stateSaved, bool automaticInstallEnabled, Func<bool, bool> launch)
    {
        if (_installLaunched || !stateSaved || (!IsRequested && !automaticInstallEnabled)) return false;
        var started = launch(IsRequested);
        if (started)
        {
            _installLaunched = true;
            IsRequested = false;
        }
        return started;
    }
}
