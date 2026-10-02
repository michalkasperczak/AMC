namespace AccessibleMediaController.LiteHost.ProtocolTests;

internal static class Program
{
    private static readonly (string Switch, Action Run)[] Suites =
    [
        ("--requests", RequestParsingTests.Run),
        ("--dispatch", DispatchLoopTests.Run),
        ("--args", ArgumentReadingTests.Run),
        ("--audio", AudioConfigurationTests.Run),
    ];

    public static int Main(string[] args)
    {
        // Tryb SERWERA PROTOKOLU: prawdziwa petla hosta na stdin/stdout.
        // Uzywa go test zgodnosci klienta Python w WSL.
        if (args.Contains("--wire-server", StringComparer.Ordinal))
        {
            return WireServer.Run();
        }

        var selected = args.Length == 0
            ? Suites
            : Suites.Where(suite => args.Contains(suite.Switch, StringComparer.Ordinal)).ToArray();

        if (selected.Length == 0)
        {
            Console.Error.WriteLine("Nie wskazano zadnego znanego zestawu testow.");
            Console.Error.WriteLine("Dostepne: " + string.Join(", ", Suites.Select(suite => suite.Switch)));
            return 2;
        }

        var failures = 0;
        foreach (var (name, run) in selected)
        {
            // Kazdy zestaw ma WLASNY blok bledow: awaria jednego nie ukrywa
            // wynikow pozostalych, a kod wyjscia pozostaje niezerowy.
            try
            {
                run();
            }
            catch (Exception exception)
            {
                failures++;
                Console.Error.WriteLine($"NIEPOWODZENIE {name}: {exception.Message}");
                Console.Error.WriteLine(exception.StackTrace);
            }
        }

        Console.WriteLine(failures == 0
            ? $"Wszystkie zestawy przeszly ({selected.Length})."
            : $"Niepowodzenia: {failures} z {selected.Length}.");
        return failures == 0 ? 0 : 1;
    }
}
