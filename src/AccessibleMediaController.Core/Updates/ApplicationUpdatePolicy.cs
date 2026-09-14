using System.Text.RegularExpressions;

namespace AccessibleMediaController.Core.Updates;

/// <summary>Co aktualizator ma zrobic z wydaniem, ktore znalazl.</summary>
public enum ApplicationUpdateDecision
{
    /// <summary>Nic nowszego nie ma - konczymy jednym zdaniem.</summary>
    UpToDate,

    /// <summary>Jest nowsza paczka i wolno ja pobrac.</summary>
    UpdateAvailable,

    /// <summary>Wydanie jest starsze niz wersja uruchomiona (wersja robocza).</summary>
    LocalIsNewer,

    /// <summary>Kanal stabilny, a jedyne wydanie jest przedpremierowe.</summary>
    PrereleaseBlockedByChannel,

    /// <summary>Odpowiedz serwera nie da sie odczytac jako wydanie.</summary>
    NotUnderstood
}

/// <summary>
/// Wydanie odczytane z GitHuba, zredukowane do tego, co potrzebne do decyzji.
/// </summary>
public sealed record ApplicationRelease(
    string Tag,
    string? Notes,
    Uri? PackageUri,
    string? PackageName,
    long PackageBytes,
    bool IsPrerelease);

/// <summary>
/// Wynik oceny wydania. Zawiera gotowy komunikat po polsku, bo to samo zdanie
/// jest mowione czytnikiem i pisane do dziennika - dwie osobne redakcje tego
/// tekstu rozjechalyby sie przy pierwszej poprawce.
/// </summary>
public sealed record ApplicationUpdatePlan(
    ApplicationUpdateDecision Decision,
    string Message,
    ApplicationRelease? Release = null,
    string? ExpectedSha256 = null)
{
    /// <summary>
    /// Czy paczka ma opublikowana sume kontrolna. Gdy nie ma, pobranie jest
    /// nadal mozliwe, ale uzytkownik MUSI o tym uslyszec - milczenie kazaloby
    /// mu wierzyc, ze sprawdzenie bylo.
    /// </summary>
    public bool HasChecksum => ExpectedSha256 is { Length: 64 };
}

/// <summary>
/// Regula decyzyjna aktualizacji calej aplikacji. Osobno od pobierania, zeby
/// dala sie zmierzyc testem bez sieci - kazdy cykl budowania AMC to minuty.
/// </summary>
public static class ApplicationUpdatePolicy
{
    private static readonly Regex Sha256Pattern = new(
        @"\b([0-9a-fA-F]{64})\b",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    /// <summary>
    /// Nazwa kanalu, przy ktorym wolno instalowac wydania przedpremierowe.
    /// AMC jest dzisiaj wydawany wylacznie jako alfa, wiec kanal testowy jest
    /// tym, ktory naprawde cos znajduje.
    /// </summary>
    public const string PrereleaseChannel = "beta";

    public static ApplicationUpdatePlan Evaluate(
        string installedVersion,
        ApplicationRelease? release,
        string channel)
    {
        if (release is null || string.IsNullOrWhiteSpace(release.Tag))
        {
            return new ApplicationUpdatePlan(
                ApplicationUpdateDecision.NotUnderstood,
                "Nie udało się odczytać informacji o wydaniach AMC.");
        }

        if (!ApplicationVersion.TryParse(installedVersion, out var installed))
        {
            return new ApplicationUpdatePlan(
                ApplicationUpdateDecision.NotUnderstood,
                $"Nie udało się odczytać numeru uruchomionej wersji AMC ({installedVersion}), "
                + "więc nie ma z czym porównać wydania.");
        }

        if (!ApplicationVersion.TryParse(release.Tag, out var available))
        {
            return new ApplicationUpdatePlan(
                ApplicationUpdateDecision.NotUnderstood,
                $"Wydanie {release.Tag} ma numer, którego AMC nie rozumie, więc nie porównuje go z wersją "
                + $"uruchomioną ({installed.Text}).");
        }

        // Kanal stabilny odcinamy PRZED porownaniem numerow. Inaczej program
        // powiedzialby "jest nowsza wersja", a potem odmowil jej pobrania -
        // czyli oglosilby aktualizacje, ktorej sam nie zamierza zrobic.
        if (release.IsPrerelease && !IsPrereleaseAllowed(channel))
        {
            return new ApplicationUpdatePlan(
                ApplicationUpdateDecision.PrereleaseBlockedByChannel,
                $"Najnowsze wydanie AMC ({available.Text}) jest testowe, a w ustawieniach wybrany jest kanał "
                + "stabilny. Aby je pobierać, przełącz kanał aktualizacji na testowy.",
                release);
        }

        var comparison = available.CompareTo(installed);
        if (comparison == 0)
        {
            return new ApplicationUpdatePlan(
                ApplicationUpdateDecision.UpToDate,
                $"AMC {installed.Text} jest aktualne.",
                release);
        }

        if (comparison < 0)
        {
            return new ApplicationUpdatePlan(
                ApplicationUpdateDecision.LocalIsNewer,
                $"Uruchomiona wersja AMC ({installed.Text}) jest nowsza niż najnowsze wydanie ({available.Text}). "
                + "To wersja robocza, więc aktualizacja nie ma czego pobrać.",
                release);
        }

        if (release.PackageUri is null)
        {
            return new ApplicationUpdatePlan(
                ApplicationUpdateDecision.NotUnderstood,
                $"Wydanie {available.Text} nie zawiera paczki AMC dla Windows, więc nie ma czego pobrać.",
                release);
        }

        // Najpierw suma przypisana do NAZWY paczki, a dopiero potem jedyna suma
        // w opisie. Kolejnosc odwrotna brałaby sumę dodatku NVDA za sumę AMC w
        // każdym wydaniu, które publikuje oba pliki.
        var checksum = ReadChecksumFor(release.Notes, release.PackageName)
                       ?? ReadChecksum(release.Notes);

        return new ApplicationUpdatePlan(
            ApplicationUpdateDecision.UpdateAvailable,
            $"Dostępna jest nowsza wersja AMC. Uruchomiona: {installed.Text}. Do pobrania: {available.Text}.",
            release,
            checksum);
    }

    /// <summary>
    /// Suma kontrolna z opisu wydania. Bierzemy ja tylko wtedy, gdy w opisie
    /// jest DOKLADNIE JEDNA - dwie rozne sumy w jednym opisie znaczy, ze wydanie
    /// zawiera kilka plikow, i zgadywanie ktora nalezy do paczki AMC byloby
    /// gorsze niz uczciwe "sumy nie ma".
    /// </summary>
    public static string? ReadChecksum(string? notes)
    {
        if (string.IsNullOrWhiteSpace(notes)) return null;
        var matches = Sha256Pattern.Matches(notes);
        var unique = matches
            .Select(match => match.Groups[1].Value.ToLowerInvariant())
            .Distinct(StringComparer.Ordinal)
            .ToList();
        return unique.Count == 1 ? unique[0] : null;
    }

    /// <summary>
    /// Suma przypisana do KONKRETNEJ nazwy pliku. Opis wydania AMC wymienia
    /// paczkę i dodatek NVDA, więc para "nazwa pliku i suma" jest jedyną
    /// postacią, która pozostaje jednoznaczna po dołożeniu drugiego artefaktu.
    /// </summary>
    public static string? ReadChecksumFor(string? notes, string? packageName)
    {
        if (string.IsNullOrWhiteSpace(notes) || string.IsNullOrWhiteSpace(packageName)) return null;

        foreach (var line in notes.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            if (!line.Contains(packageName, StringComparison.OrdinalIgnoreCase)) continue;
            var match = Sha256Pattern.Match(line);
            if (match.Success) return match.Groups[1].Value.ToLowerInvariant();
        }

        // Postac dwuwierszowa: nazwa pliku, a suma w wierszu nastepnym.
        var lines = notes.Split(['\r', '\n'], StringSplitOptions.None);
        for (var index = 0; index < lines.Length - 1; index++)
        {
            if (!lines[index].Contains(packageName, StringComparison.OrdinalIgnoreCase)) continue;
            for (var next = index + 1; next < Math.Min(lines.Length, index + 4); next++)
            {
                var match = Sha256Pattern.Match(lines[next]);
                if (match.Success) return match.Groups[1].Value.ToLowerInvariant();
            }
        }

        return null;
    }

    public static bool IsPrereleaseAllowed(string? channel) =>
        string.Equals(channel?.Trim(), PrereleaseChannel, StringComparison.OrdinalIgnoreCase);
}
