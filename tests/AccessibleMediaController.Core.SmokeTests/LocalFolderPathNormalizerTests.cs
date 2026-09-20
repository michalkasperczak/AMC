// Regresja KOSZTU + ZGODNOŚCI dla normalizacji korzeni folderów w obrębie jednego
// przebiegu. Test pilnuje DWÓCH własności naraz (inaczej przechodziłby też na kodzie
// bez poprawki): wynik identyczny z wariantem bez normalizatora ORAZ liczba rzeczywistych
// normalizacji mniejsza od liczby wywołań.
using AccessibleMediaController.Core.LocalMedia;

internal static class LocalFolderPathNormalizerTests
{
    internal static void Run()
    {
        SameVerdictAsWithoutNormalizer();
        NormalizesEachDistinctRootOnce();
        CaseAndSeparatorVariantsKeepOriginalSemantics();
        ThrowingPathPropagatesAndIsNotRemembered();
        MemoDoesNotLeakBetweenRuns();
        NestedRunsAreIndependent();
        Console.WriteLine("OK: normalizacja korzeni folderów w jednym przebiegu nie zmienia wyniku i nie powtarza pracy");
    }

    private static string Root(params string[] parts) =>
        Path.Combine(new[] { Path.GetTempPath(), "AMC-normalizer" }.Concat(parts).ToArray());

    private static void SameVerdictAsWithoutNormalizer()
    {
        var roots = new[]
        {
            Root("Muzyka"),
            Root("Muzyka") + Path.DirectorySeparatorChar,
            Root("muzyka", "Podcasty"),
            Root("Inne"),
            "",
            "   "
        };
        var candidates = new[]
        {
            Root("Muzyka", "album", "utwor.mp3"),
            Root("MUZYKA", "album", "utwor.mp3"),
            Root("Muzyka"),
            Root("Muzyka2", "utwor.mp3"),
            Root("muzyka", "Podcasty", "odc.mp3"),
            "",
            "relatywna.mp3"
        };

        var normalizer = new LocalFolderPathNormalizer();
        foreach (var candidate in candidates)
        {
            foreach (var root in roots)
            {
                var without = LocalFolderSourcePolicy.IsSameOrDescendant(candidate, root);
                var with = LocalFolderSourcePolicy.IsSameOrDescendant(candidate, root, normalizer);
                Check(
                    without == with,
                    $"Werdykt z normalizatorem musi być identyczny: „{candidate}” wobec „{root}” "
                    + $"({without} kontra {with}).");
            }
        }
    }

    private static void NormalizesEachDistinctRootOnce()
    {
        var normalizer = new LocalFolderPathNormalizer();
        var root = Root("Muzyka");
        var calls = 0;
        for (var i = 0; i < 50; i++)
        {
            LocalFolderSourcePolicy.IsSameOrDescendant(Root("Muzyka", $"{i}.mp3"), root, normalizer);
            calls++;
        }

        // Każde wywołanie normalizuje kandydata (50 różnych) ORAZ korzeń (zawsze ten
        // sam). Bez pamięci przebiegu byłoby 2*calls normalizacji; z pamięcią korzeń
        // liczy się raz, więc 50 kandydatów + 1 korzeń.
        Check(
            normalizer.ComputeCount == calls + 1,
            $"Powtarzany korzeń ma znormalizować się raz: {normalizer.ComputeCount}, "
            + $"oczekiwano {calls + 1}.");
        Check(
            normalizer.ComputeCount < 2 * calls,
            $"Pamięć przebiegu musi zmniejszyć liczbę normalizacji poniżej {2 * calls}: "
            + $"{normalizer.ComputeCount}.");
        Check(
            normalizer.HitCount >= calls - 1,
            $"Powtórzony korzeń ma trafiać w pamięć przebiegu: {normalizer.HitCount} trafień.");
    }

    private static void CaseAndSeparatorVariantsKeepOriginalSemantics()
    {
        var normalizer = new LocalFolderPathNormalizer();
        var withSeparator = Root("Muzyka") + Path.DirectorySeparatorChar;
        var upper = Root("MUZYKA");
        var file = Root("Muzyka", "a.mp3");

        Check(
            LocalFolderSourcePolicy.IsSameOrDescendant(file, withSeparator, normalizer)
            == LocalFolderSourcePolicy.IsSameOrDescendant(file, withSeparator),
            "Korzeń z końcowym separatorem musi dać ten sam wynik.");
        Check(
            LocalFolderSourcePolicy.IsSameOrDescendant(file, upper, normalizer)
            == LocalFolderSourcePolicy.IsSameOrDescendant(file, upper),
            "Inna wielkość liter w korzeniu musi dać ten sam wynik.");
        // Klucz pamięci jest Ordinal, więc dwa różne zapisy tego samego korzenia
        // są liczone osobno — ale oba muszą dać tę samą znormalizowaną wartość.
        Check(
            normalizer.Normalize(withSeparator) == normalizer.Normalize(Root("Muzyka")),
            "Zapis z separatorem i bez niego musi znormalizować się do tej samej ścieżki.");
    }

    private static void ThrowingPathPropagatesAndIsNotRemembered()
    {
        var normalizer = new LocalFolderPathNormalizer();
        var bad = "\0niepoprawna";
        var firstThrew = false;
        var secondThrew = false;
        try { normalizer.Normalize(bad); }
        catch (ArgumentException) { firstThrew = true; }
        try { normalizer.Normalize(bad); }
        catch (ArgumentException) { secondThrew = true; }

        Check(firstThrew && secondThrew,
            "Wyjątek normalizacji musi propagować się za każdym razem, nie zostać zapamiętany jako wynik.");
        Check(normalizer.HitCount == 0,
            "Nieudana normalizacja nie może zostać zaliczona jako trafienie pamięci.");
    }

    private static void MemoDoesNotLeakBetweenRuns()
    {
        var root = Root("Muzyka");
        var first = new LocalFolderPathNormalizer();
        LocalFolderSourcePolicy.IsSameOrDescendant(Root("Muzyka", "a.mp3"), root, first);
        Check(first.ComputeCount > 0, "Pierwszy przebieg musi realnie policzyć normalizację.");

        var second = new LocalFolderPathNormalizer();
        Check(second.ComputeCount == 0 && second.HitCount == 0,
            "Nowy przebieg startuje z pustą pamięcią — żadnego trwałego cache'u globalnego.");
        LocalFolderSourcePolicy.IsSameOrDescendant(Root("Muzyka", "a.mp3"), root, second);
        Check(second.ComputeCount > 0,
            "Drugi przebieg musi policzyć normalizację od nowa, nie odziedziczyć wyniku poprzedniego.");
    }

    private static void NestedRunsAreIndependent()
    {
        // Zagnieżdżenie: dwa żywe normalizatory jednocześnie. Każdy ma własną pamięć,
        // żaden nie zeruje drugiego (nie ma tu stanu statycznego ani ThreadStatic).
        var outer = new LocalFolderPathNormalizer();
        var root = Root("Muzyka");
        LocalFolderSourcePolicy.IsSameOrDescendant(Root("Muzyka", "a.mp3"), root, outer);
        var outerAfterFirst = outer.ComputeCount;

        var inner = new LocalFolderPathNormalizer();
        LocalFolderSourcePolicy.IsSameOrDescendant(Root("Muzyka", "b.mp3"), root, inner);
        Check(inner.ComputeCount > 0, "Zagnieżdżony przebieg liczy własną normalizację.");

        LocalFolderSourcePolicy.IsSameOrDescendant(Root("Muzyka", "c.mp3"), root, outer);
        // Nowy kandydat („c.mp3”) to jedna nowa normalizacja; korzeń przychodzi
        // z pamięci przebiegu zewnętrznego, więc nie jest liczony ponownie.
        Check(outer.ComputeCount == outerAfterFirst + 1,
            $"Przebieg zewnętrzny liczy tylko nowego kandydata: {outer.ComputeCount}, "
            + $"oczekiwano {outerAfterFirst + 1}.");
        Check(outer.HitCount > 0,
            "Przebieg zewnętrzny nadal korzysta z własnej pamięci korzenia, mimo zagnieżdżenia.");
        Check(inner.ComputeCount > 0 && inner.HitCount == 0,
            "Zagnieżdżony przebieg ma własną, niezależną pamięć.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }
}
