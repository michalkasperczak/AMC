using System.Text.Json;
using AccessibleMediaController.ReleaseProfiles;

namespace AccessibleMediaController.PythonLauncher;

/// <summary>Detects a profile update before the launcher reads replaceable files.</summary>
internal static class UpdateInProgressGate
{
    internal const string InstallerFullCheck = "--sprawdz-pakiet-instalatora-pelny-cicho";
    internal const string InstallerStartCheck = "--sprawdz-start-instalatora-cicho";

    // Instalator uruchomiony przez aktualizator musi sprawdzić skopiowane pliki
    // przy zajętym mutexie aktualizacji. Tylko te dwie operacje diagnostyczne
    // mogą ominąć bramkę; normalny start nadal czeka na koniec aktualizacji.
    internal static bool IsInstallerCheck(IReadOnlyList<string> args) =>
        args.Count == 1 && (IsInstallerFullCheck(args) || IsInstallerStartCheck(args));

    internal static bool IsInstallerFullCheck(IReadOnlyList<string> args) =>
        args.Count == 1 && string.Equals(args[0], InstallerFullCheck,
            StringComparison.OrdinalIgnoreCase);

    internal static bool IsInstallerStartCheck(IReadOnlyList<string> args) =>
        args.Count == 1 && string.Equals(args[0], InstallerStartCheck,
            StringComparison.OrdinalIgnoreCase);

    internal static ReleaseProfileDescriptor? ActiveProfile(string installationRoot)
    {
        var selected = ReadProfile(installationRoot);
        if (selected is not null)
            return IsUpdaterRunning(selected.UpdaterMutexName) ? selected : null;

        // During replacement the manifest can be absent or incomplete. In
        // that short interval, a conservative check protects both profiles.
        foreach (var profile in new[] { ReleaseProfileContract.Test, ReleaseProfileContract.Public })
        {
            if (IsUpdaterRunning(profile.UpdaterMutexName))
                return profile;
        }
        return null;
    }

    private static ReleaseProfileDescriptor? ReadProfile(string root)
    {
        try
        {
            using var stream = File.OpenRead(Path.Combine(root, "bundle-manifest.json"));
            using var document = JsonDocument.Parse(stream);
            if (!document.RootElement.TryGetProperty("releaseProfile", out var value))
                return null;
            return value.GetString() switch
            {
                "test" => ReleaseProfileContract.Test,
                "public" => ReleaseProfileContract.Public,
                _ => null,
            };
        }
        catch (Exception error) when (error is IOException or JsonException or InvalidOperationException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    internal static bool IsUpdaterRunning(string mutexName)
    {
        if (!Mutex.TryOpenExisting(mutexName, out var mutex))
            return false;
        using (mutex)
        {
            try
            {
                if (!mutex.WaitOne(0))
                    return true;
            }
            catch (AbandonedMutexException)
            {
                // An abandoned mutex means there is no active installer.
            }
            mutex.ReleaseMutex();
            return false;
        }
    }
}
