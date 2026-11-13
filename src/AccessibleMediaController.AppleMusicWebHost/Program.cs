using System.Text.Json;
using System.Text;

namespace AccessibleMediaController.AppleMusicWebHost;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        // WinExe has no console handle. Setting Console.InputEncoding crashes
        // before the redirected pipes can be used (IOException: invalid handle).
        using var input = new StreamReader(
            Console.OpenStandardInput(), new UTF8Encoding(false));
        using var output = new StreamWriter(
            Console.OpenStandardOutput(), new UTF8Encoding(false))
        {
            AutoFlush = true
        };
        if (args.Length == 1 && args[0] == "--pipe-probe")
        {
            output.WriteLine(input.ReadLine() == "ping" ? "pong" : "error");
            return;
        }
        ApplicationConfiguration.Initialize();
        using var window = new PlayerWindow();
        _ = window.Handle;
        _ = Task.Run(() => ReadCommandsAsync(window, input, output));
        Application.Run(window);
    }

    private static async Task ReadCommandsAsync(
        PlayerWindow window, StreamReader input, StreamWriter output)
    {
        while (true)
        {
            string? line;
            try { line = await input.ReadLineAsync(); }
            catch { break; }
            if (line is null) break;
            int id = -1;
            object result;
            try
            {
                var request = WebCommand.Parse(line);
                id = request.Id;
                var completion = new TaskCompletionSource<object>(
                    TaskCreationOptions.RunContinuationsAsynchronously);
                window.BeginInvoke(new Action(async () =>
                {
                    try { completion.SetResult(await window.HandleAsync(request)); }
                    catch (Exception) { completion.SetResult(new {
                        handled = false,
                        message = "Nie można sterować osadzonym Apple Music."
                    }); }
                }));
                result = await completion.Task.WaitAsync(TimeSpan.FromSeconds(30));
            }
            catch (Exception)
            {
                result = new { handled = false,
                    message = "Nieprawidłowe polecenie Apple Music." };
            }
            try
            {
                output.WriteLine(JsonSerializer.Serialize(new { id, result }));
            }
            catch { break; }
        }
        if (!window.IsDisposed)
            window.BeginInvoke(new Action(window.Close));
    }
}
