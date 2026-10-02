using AccessibleMediaController.LiteHost.Protocol;

namespace AccessibleMediaController.LiteHost;

/// <summary>
/// Punkt wejscia BEZOKIENNEGO hosta AMC-wx-Lite.
///
/// Uruchamia go frontend wxPython jako proces potomny i rozmawia z nim
/// przez stdin/stdout. Host NIE otwiera zadnego okna, NIE nasluchuje na
/// zadnym porcie i NIE wykonuje dowolnych polecen systemu.
/// </summary>
internal static class Program
{
    private static int Main(string[] args)
    {
        // stdout jest kanalem PROTOKOLU: nic poza wierszami JSON nie moze
        // sie tam znalezc, inaczej frontend straci synchronizacje.
        // Diagnostyka i wyjatki ida na stderr.
        var standardOutput = Console.Out;
        Console.SetOut(Console.Error);

        var timeshiftMinutes = ReadTimeshiftMinutes(args);

        using var handlers = new LiteEngineHandlers(timeshiftMinutes);
        var loop = new LiteDispatchLoop(handlers.Build());

        Console.Error.WriteLine(
            $"[amc-lite-host] start, bufor transmisji {timeshiftMinutes} min, protokol 1");

        try
        {
            loop.Run(Console.In, standardOutput);
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine("[amc-lite-host] awaria petli: " + exception);
            return 1;
        }

        Console.Error.WriteLine("[amc-lite-host] koniec wejscia, zamykam sie");
        return 0;
    }

    private static int ReadTimeshiftMinutes(string[] args)
    {
        for (var index = 0; index < args.Length - 1; index++)
        {
            if (!string.Equals(args[index], "--timeshift-minutes", StringComparison.Ordinal)) continue;
            if (int.TryParse(args[index + 1], out var value)) return Math.Clamp(value, 1, 720);
        }
        return 30;
    }
}
