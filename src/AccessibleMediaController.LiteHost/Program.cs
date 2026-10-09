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

        // TRWALOSC kolejki. Dwie jawne decyzje, obie z wiersza polecen:
        //   --profile-dir <sciezka>  ktora KOPIA profilu,
        //   --queue-write            czy ten host jest wlascicielem zapisu.
        // Bez --queue-write host tylko ODCZYTUJE zapisana kolejke. Domyslnie
        // (bez --profile-dir) nie dotyka profilu w ogole.
        LiteQueueStore? store;
        try
        {
            store = OpenQueueStore(args);
        }
        catch (LiteQueueStoreDenied)
        {
            // Powod odmowy jest juz na stderr. Konczymy KODEM BLEDU, zeby
            // wolajacy nie wzial cichego startu za udana trwalosc.
            Console.SetOut(standardOutput);
            return 2;
        }

        var bookmarkStore = OpenBookmarkStore(args);
        using var handlers = new LiteEngineHandlers(timeshiftMinutes, store, bookmarkStore);
        // JAWNY opt-in: poza kolejke wychodza tylko operacje, ktore moga dlugo
        // czytac/dekodowac plik: informacja pod lewa strzalka oraz eksport
        // zaznaczonego fragmentu. Transport nadal pozostaje responsywny, a
        // wszystkie pozostale polecenia sa uporzadkowane serialnie.
        var loop = new LiteDispatchLoop(
            handlers.Build(),
            concurrentOperations:
            [
                LiteQuickInformation.Operation,
                // Eksport moze trwac dlugo. Odtwarzanie, pauza i status nie
                // moga na ten czas utknac za dekoderem/FFmpeg.
                LiteAudioClipOperations.ExportOperation
            ]);

        Console.Error.WriteLine(
            $"[amc-lite-host] start, bufor transmisji {timeshiftMinutes} min, protokol 1");
        Console.Error.WriteLine(store is null
            ? "[amc-lite-host] kolejka BEZ trwalosci (brak --profile-dir)"
            : $"[amc-lite-host] kolejka: profil {store.ProfileDirectory}, tryb {store.Mode}");

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

    /// <summary>
    /// Otwiera magazyn trwalosci kolejki wedlug argumentow.
    ///
    /// Zasada bezpieczenstwa: zapis jest WYLACZONY domyslnie i wlaczany
    /// wylacznie jawnym <c>--queue-write</c> razem z <c>--profile-dir</c>
    /// wskazujacym konkretna kopie. Odmowa (zajeta kopia, profil produkcyjny,
    /// brak prawa zapisu) konczy START HOSTA bledem -- host NIE startuje cicho
    /// bez trwalosci, o ktora go poproszono.
    /// </summary>
    private static LiteQueueStore? OpenQueueStore(string[] args)
    {
        string? profileDirectory = null;
        for (var index = 0; index < args.Length - 1; index++)
        {
            if (!string.Equals(args[index], "--profile-dir", StringComparison.Ordinal)) continue;
            profileDirectory = args[index + 1];
            break;
        }
        var writable = args.Contains("--queue-write", StringComparer.Ordinal);

        if (profileDirectory is null)
        {
            if (writable)
            {
                // Zadanie zapisu BEZ wskazania kopii to blad wywolania, nie
                // powod do cichego trybu odczytu.
                Console.Error.WriteLine(
                    "[amc-lite-host] --queue-write wymaga --profile-dir wskazujacego wlasna kopie profilu");
                throw new LiteQueueStoreDenied("--queue-write bez --profile-dir");
            }
            return null;
        }

        try
        {
            return LiteQueueStore.Open(profileDirectory, writable);
        }
        catch (LiteQueueStoreDenied denied)
        {
            Console.Error.WriteLine("[amc-lite-host] ODMOWA magazynu kolejki: " + denied.Message);
            throw;
        }
    }

    private static LiteBookmarkStore? OpenBookmarkStore(string[] args)
    {
        for (var index = 0; index < args.Length - 1; index++)
        {
            if (!string.Equals(args[index], "--library-db", StringComparison.Ordinal)) continue;
            var path = Path.GetFullPath(args[index + 1]);
            return File.Exists(path) ? new LiteBookmarkStore(path) : null;
        }
        return null;
    }
}
