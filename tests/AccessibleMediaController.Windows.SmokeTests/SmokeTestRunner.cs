using System.IO;
using System.Text;

// Test-process boundary only. Never install this handler in the production player:
// reporting a failed test is not recovery from a failed playback operation.
internal static class SmokeTestRunner
{
    internal static int Run(Func<int> run, TextWriter? error = null)
    {
        try
        {
            // Keep redirected diagnostics readable on Windows with a legacy OEM
            // console code page, including in the isolated child-process checks.
            Console.OutputEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
            return run();
        }
        catch (Exception exception)
        {
            (error ?? Console.Error).WriteLine($"BŁĄD TESTU AMC (nie okna odtwarzacza): {exception}");
            return 1;
        }
    }
}
