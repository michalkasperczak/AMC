using System.Globalization;

namespace AccessibleMediaController.Core.Updates;

/// <summary>
/// Porownywanie numerow wersji AMC w postaci "0.1.0-alpha.354".
///
/// Wlasny porzadek jest tu konieczny, bo <see cref="Version"/> z platformy w
/// ogole nie zna czlonu przedwydawniczego: "0.1.0-alpha.354" i "0.1.0-alpha.342"
/// bylyby dla niego rowne. Aktualizator porownujacy wersje tak, ze kazda alfa
/// jest rowna kazdej innej, nigdy nie zauwazylby nowego wydania - a to jedyny
/// rodzaj wydan, jaki AMC dzisiaj publikuje.
/// </summary>
public sealed class ApplicationVersion : IComparable<ApplicationVersion>, IEquatable<ApplicationVersion>
{
    private readonly int[] _release;
    private readonly string[] _prerelease;

    private ApplicationVersion(string text, int[] release, string[] prerelease)
    {
        Text = text;
        _release = release;
        _prerelease = prerelease;
    }

    /// <summary>Numer w postaci, w jakiej przyszedl (bez wiodacego "v").</summary>
    public string Text { get; }

    /// <summary>Czy to wydanie przedpremierowe (alpha, beta, rc).</summary>
    public bool IsPrerelease => _prerelease.Length > 0;

    /// <summary>
    /// Przyjmuje "v0.1.0-alpha.354", "0.1.0-alpha.354", "0.1.0" oraz numer z
    /// doklejonym znacznikiem kompilacji ("0.1.0-alpha.354+abc123"), bo wlasnie
    /// taki oddaje <c>AssemblyInformationalVersion</c>.
    /// </summary>
    public static bool TryParse(string? value, out ApplicationVersion version)
    {
        version = null!;
        if (string.IsNullOrWhiteSpace(value)) return false;

        var text = value.Trim();
        if (text.StartsWith("v", StringComparison.OrdinalIgnoreCase)) text = text[1..];
        var build = text.IndexOf('+');
        if (build >= 0) text = text[..build];
        if (text.Length == 0) return false;

        var dash = text.IndexOf('-');
        var releasePart = dash < 0 ? text : text[..dash];
        var prereleasePart = dash < 0 ? string.Empty : text[(dash + 1)..];

        var releaseFields = releasePart.Split('.');
        if (releaseFields.Length is 0 or > 4) return false;
        var release = new int[releaseFields.Length];
        for (var index = 0; index < releaseFields.Length; index++)
        {
            if (!int.TryParse(releaseFields[index], NumberStyles.None, CultureInfo.InvariantCulture, out var number))
                return false;
            release[index] = number;
        }

        var prerelease = prereleasePart.Length == 0
            ? []
            : prereleasePart.Split('.', StringSplitOptions.RemoveEmptyEntries);

        version = new ApplicationVersion(text, release, prerelease);
        return true;
    }

    public int CompareTo(ApplicationVersion? other)
    {
        if (other is null) return 1;

        var fields = Math.Max(_release.Length, other._release.Length);
        for (var index = 0; index < fields; index++)
        {
            var mine = index < _release.Length ? _release[index] : 0;
            var theirs = index < other._release.Length ? other._release[index] : 0;
            if (mine != theirs) return mine.CompareTo(theirs);
        }

        // Wydanie zwykle jest STARSZE niz przedpremierowe o tym samym numerze
        // czlonu glownego - taka jest regula semver i taka jest prawda o
        // naszych paczkach: 0.1.0 wyjdzie po wszystkich 0.1.0-alpha.
        if (_prerelease.Length == 0 && other._prerelease.Length == 0) return 0;
        if (_prerelease.Length == 0) return 1;
        if (other._prerelease.Length == 0) return -1;

        var parts = Math.Max(_prerelease.Length, other._prerelease.Length);
        for (var index = 0; index < parts; index++)
        {
            if (index >= _prerelease.Length) return -1;
            if (index >= other._prerelease.Length) return 1;

            var mine = _prerelease[index];
            var theirs = other._prerelease[index];
            var mineNumeric = int.TryParse(mine, NumberStyles.None, CultureInfo.InvariantCulture, out var mineNumber);
            var theirsNumeric = int.TryParse(theirs, NumberStyles.None, CultureInfo.InvariantCulture, out var theirsNumber);

            // "alpha.354" kontra "alpha.342": czlon liczbowy porownujemy
            // LICZBOWO, nie tekstowo - inaczej "9" wyszloby wieksze od "354".
            if (mineNumeric && theirsNumeric)
            {
                if (mineNumber != theirsNumber) return mineNumber.CompareTo(theirsNumber);
                continue;
            }
            if (mineNumeric) return -1;
            if (theirsNumeric) return 1;

            var text = string.CompareOrdinal(mine, theirs);
            if (text != 0) return text < 0 ? -1 : 1;
        }

        return 0;
    }

    public bool Equals(ApplicationVersion? other) => CompareTo(other) == 0;

    public override bool Equals(object? obj) => obj is ApplicationVersion other && Equals(other);

    public override int GetHashCode() => Text.GetHashCode(StringComparison.Ordinal);

    public override string ToString() => Text;

    public static bool operator >(ApplicationVersion left, ApplicationVersion right) => left.CompareTo(right) > 0;

    public static bool operator <(ApplicationVersion left, ApplicationVersion right) => left.CompareTo(right) < 0;

    public static bool operator >=(ApplicationVersion left, ApplicationVersion right) => left.CompareTo(right) >= 0;

    public static bool operator <=(ApplicationVersion left, ApplicationVersion right) => left.CompareTo(right) <= 0;
}
