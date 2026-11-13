using System.Text.Json;
using System.Text;

namespace AccessibleMediaController.AppleMusicWebHost;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        Console.InputEncoding = Encoding.UTF8;
        Console.OutputEncoding = Encoding.UTF8;
        ApplicationConfiguration.Initialize();
        using var window = new PlayerWindow();
        _ = window.Handle;
        _ = Task.Run(() => ReadCommandsAsync(window));
        Application.Run(window);
    }

    private static async Task ReadCommandsAsync(PlayerWindow window)
    {
        while (true)
        {
            string? line;
            try { line = await Console.In.ReadLineAsync(); }
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
                Console.Out.WriteLine(JsonSerializer.Serialize(new { id, result }));
                Console.Out.Flush();
            }
            catch { break; }
        }
        if (!window.IsDisposed)
            window.BeginInvoke(new Action(window.Close));
    }
}
