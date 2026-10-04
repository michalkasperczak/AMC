using System.Text;
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

        // Protokol jest w UTF-8 i MUSI byc czytany jako UTF-8.
        //
        // Domyslnie Console.In na Windows dekoduje przekierowany stdin
        // kodowaniem strony kodowej konsoli (u Michala CP852/CP1250), wiec
        // polskie znaki docieraly uszkodzone. Zmierzone na zywym hoscie:
        // ten sam tytul wyslany surowym UTF-8 i jako \uXXXX dawal ROZNE
        // klucze sortowania, a zgadzaly sie tylko tytuly czysto ASCII
        // (1333 z 2596). Dotyczy to kazdej operacji z polskim tekstem --
        // takze sciezek plikow do odtwarzania, nie tylko kolejnosci listy.
        var utf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
        var protocolInput = new StreamReader(
            Console.OpenStandardInput(), utf8, detectEncodingFromByteOrderMarks: false);
        var protocolOutput = new StreamWriter(Console.OpenStandardOutput(), utf8)
        {
            AutoFlush = false // petla zadan flushuje sama, po calym wierszu
        };

        var timeshiftMinutes = ReadTimeshiftMinutes(args);

        using var handlers = new LiteEngineHandlers(timeshiftMinutes);
        var loop = new LiteDispatchLoop(handlers.Build());

        Console.Error.WriteLine(
            $"[amc-lite-host] start, bufor transmisji {timeshiftMinutes} min, protokol 1");

        try
        {
            loop.Run(protocolInput, protocolOutput);
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine("[amc-lite-host] awaria petli: " + exception);
            return 1;
        }
        finally
        {
            protocolOutput.Flush();
            Console.SetOut(standardOutput);
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
