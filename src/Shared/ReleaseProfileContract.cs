namespace AccessibleMediaController.ReleaseProfiles;

internal sealed record ReleaseProfileDescriptor(
    string ProfileId,
    string ProductId,
    string DisplayName,
    string InstallId,
    string UpdateChannel,
    string UiStateFolder,
    string RoamingProfileFolder,
    string LocalProfileFolder,
    string CacheFolder,
    string LogFolder,
    string CredentialNamespace,
    IReadOnlyList<string> Sessions,
    IReadOnlyList<string> Capabilities,
    string LauncherMutexName,
    string UpdaterMutexName,
    string UpdaterHelperFolder,
    string UpdaterHelperExecutablePrefix,
    string PreferredInstallationFolder,
    IReadOnlyList<string> AcceptedWindowTitles);

/// <summary>
/// Closed, versioned release profile catalog shared by the launcher and
/// updater. A manifest may select a profile, but it may not redefine any of
/// its isolation or capability fields.
/// </summary>
internal static class ReleaseProfileContract
{
    internal const int SupportedSchema = 1;

    private static readonly string[] CommonCapabilities =
    [
        "session.files",
        "session.radio",
        "session.podcasts-youtube",
        "session.wiim",
        "session.sonos",
        "recording",
        "schedules",
        "bookmarks",
        "chapters",
        "command-palette",
        "keyboard-help",
        "problem-report-local",
        "experimental",
    ];

    private static readonly string[] PublicCapabilities = CommonCapabilities
        .Where(capability => capability != "session.sonos")
        .Append("session.frontier-smart")
        .ToArray();

    internal static readonly ReleaseProfileDescriptor Test = new(
        "test",
        "org.accessible-media-controller.test",
        "AMC Test",
        "AMC.Test.win-x64",
        "test",
        "AMC-wx-Lite",
        "AccessibleMediaController",
        "AccessibleMediaController",
        "AMC-wx-Lite/cache",
        "AMC-wx-Lite/logs",
        "AccessibleMediaController.Test",
        ["files", "radio", "podcasts", "tidal", "wiim", "sonos", "spotify", "frontierSmart", "radioarchiwista"],
        CommonCapabilities.Concat(
        [
            "session.spotify",
            "session.tidal",
            "session.frontier-smart",
            "session.radioarchiwista",
            "spotify.playlists",
            "youtube.account.readonly",
        ]).ToArray(),
        @"Local\AccessibleMultimediaController.PythonLauncher.SingleInstance",
        @"Local\AccessibleMultimediaController.PythonUpdater.Test.SingleInstance",
        "AMC-Python/Updater",
        "AMC-Python-Updater",
        "AMC Python",
        ["AMC Test", "AMC Python"]);

    internal static readonly ReleaseProfileDescriptor Public = new(
        "public",
        "org.accessible-media-controller.public",
        "AMC Public",
        "AMC.Public.win-x64",
        "stable",
        "AMC/ui",
        "AMC/profile",
        "AMC/profile",
        "AMC/cache",
        "AMC/logs",
        "AccessibleMediaController.Public",
        ["files", "radio", "podcasts", "wiim", "frontierSmart"],
        PublicCapabilities,
        @"Local\AccessibleMultimediaController.PythonLauncher.Public.SingleInstance",
        @"Local\AccessibleMultimediaController.PythonUpdater.Public.SingleInstance",
        "AMC/Updater",
        "AMC-Updater",
        "AMC",
        ["AMC Public", "AMC"]);

    internal static ReleaseProfileDescriptor Validate(
        int schema,
        string profileId,
        string productId,
        string displayName,
        string installId,
        string updateChannel,
        string uiStateFolder,
        string roamingProfileFolder,
        string localProfileFolder,
        string cacheFolder,
        string logFolder,
        string credentialNamespace,
        IReadOnlyList<string>? sessions,
        IReadOnlyList<string>? capabilities)
    {
        if (schema != SupportedSchema)
            throw new InvalidDataException("wersja profilu wydania nie jest obsługiwana");
        var expected = profileId switch
        {
            "test" => Test,
            "public" => Public,
            _ => throw new InvalidDataException("manifest wskazuje nieznany profil wydania"),
        };

        Require(expected.ProductId, productId, "identyfikator produktu");
        Require(expected.DisplayName, displayName, "nazwa wydania");
        Require(expected.InstallId, installId, "identyfikator instalacji");
        Require(expected.UpdateChannel, updateChannel, "kanał aktualizacji");
        Require(expected.UiStateFolder, uiStateFolder, "folder interfejsu");
        Require(expected.RoamingProfileFolder, roamingProfileFolder, "folder ustawień");
        Require(expected.LocalProfileFolder, localProfileFolder, "folder danych lokalnych");
        Require(expected.CacheFolder, cacheFolder, "folder pamięci podręcznej");
        Require(expected.LogFolder, logFolder, "folder dzienników");
        Require(expected.CredentialNamespace, credentialNamespace, "przestrzeń poświadczeń");
        RequireSequence(expected.Sessions, sessions, "lista sesji");
        RequireSequence(expected.Capabilities, capabilities, "lista możliwości");
        return expected;
    }

    internal static bool IsWindowTitle(ReleaseProfileDescriptor profile, string title) =>
        profile.AcceptedWindowTitles.Any(productName =>
            string.Equals(title, productName, StringComparison.Ordinal)
            || title.StartsWith(productName + " — ", StringComparison.Ordinal));

    /// <summary>
    /// Validates the identity of an already installed older bundle. Release
    /// capabilities may grow and their display order may change, so an older
    /// manifest is compatible when all of its sessions and capabilities are
    /// still known by the current profile. Isolation fields remain exact.
    /// </summary>
    internal static void ValidateCompatibleInstalled(
        ReleaseProfileDescriptor expected,
        int schema,
        string profileId,
        string productId,
        string displayName,
        string installId,
        string updateChannel,
        string uiStateFolder,
        string roamingProfileFolder,
        string localProfileFolder,
        string cacheFolder,
        string logFolder,
        string credentialNamespace,
        IReadOnlyList<string>? sessions,
        IReadOnlyList<string>? capabilities)
    {
        if (schema != SupportedSchema
            || !string.Equals(expected.ProfileId, profileId, StringComparison.Ordinal)
            || !string.Equals(expected.ProductId, productId, StringComparison.Ordinal)
            || !expected.AcceptedWindowTitles.Contains(displayName, StringComparer.Ordinal)
            || !string.Equals(expected.InstallId, installId, StringComparison.Ordinal)
            || !string.Equals(expected.UpdateChannel, updateChannel, StringComparison.Ordinal)
            || !string.Equals(expected.UiStateFolder, uiStateFolder, StringComparison.Ordinal)
            || !string.Equals(expected.RoamingProfileFolder, roamingProfileFolder, StringComparison.Ordinal)
            || !string.Equals(expected.LocalProfileFolder, localProfileFolder, StringComparison.Ordinal)
            || !string.Equals(expected.CacheFolder, cacheFolder, StringComparison.Ordinal)
            || !string.Equals(expected.LogFolder, logFolder, StringComparison.Ordinal)
            || !string.Equals(expected.CredentialNamespace, credentialNamespace, StringComparison.Ordinal))
            throw new InvalidDataException(
                "pakiet aktualizacji jest przeznaczony dla innego wydania AMC");

        var compatibleSessions = expected.ProfileId == "public"
            ? expected.Sessions.Concat(["sonos"]).ToArray()
            : expected.Sessions;
        RequireKnownSubset(compatibleSessions, sessions, "lista sesji");
        var compatibleCapabilities = expected.ProfileId == "public"
            ? expected.Capabilities.Concat(
                ["youtube.account.readonly", "session.sonos"]).ToArray()
            : expected.Capabilities;
        RequireKnownSubset(compatibleCapabilities, capabilities, "lista możliwości");
    }

    private static void Require(string expected, string actual, string field)
    {
        if (!string.Equals(expected, actual, StringComparison.Ordinal))
            throw new InvalidDataException($"profil wydania ma nieprawidłowe pole: {field}");
    }

    private static void RequireSequence(
        IReadOnlyList<string> expected,
        IReadOnlyList<string>? actual,
        string field)
    {
        if (actual is null || !expected.SequenceEqual(actual, StringComparer.Ordinal))
            throw new InvalidDataException($"profil wydania ma nieprawidłowe pole: {field}");
    }

    private static void RequireKnownSubset(
        IReadOnlyList<string> expected,
        IReadOnlyList<string>? actual,
        string field)
    {
        if (actual is null
            || actual.Count == 0
            || actual.Distinct(StringComparer.Ordinal).Count() != actual.Count
            || actual.Any(value =>
                string.IsNullOrWhiteSpace(value)
                || !expected.Contains(value, StringComparer.Ordinal)))
            throw new InvalidDataException(
                $"zainstalowany profil ma niezgodne pole: {field}");
    }
}
