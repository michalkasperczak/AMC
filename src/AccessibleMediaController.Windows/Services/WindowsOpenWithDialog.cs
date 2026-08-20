using System.Diagnostics;
using System.IO;

namespace AccessibleMediaController.Windows.Services;

/// <summary>
/// Opens the Windows application picker in a separate shell process. Running
/// it out of process lets Windows establish foreground focus independently of
/// the WPF input stack and gives screen readers a normal native focus event.
/// </summary>
internal static class WindowsOpenWithDialog
{
    public static void Show(string filePath)
    {
        var systemDirectory = Environment.GetFolderPath(Environment.SpecialFolder.System);
        var rundll32Path = Path.Combine(systemDirectory, "rundll32.exe");
        if (!File.Exists(rundll32Path))
        {
            throw new FileNotFoundException("Nie znaleziono systemowego programu rundll32.exe.", rundll32Path);
        }

        var startInfo = new ProcessStartInfo(rundll32Path)
        {
            UseShellExecute = false,
            CreateNoWindow = true
        };
        startInfo.ArgumentList.Add("shell32.dll,OpenAs_RunDLL");
        startInfo.ArgumentList.Add(filePath);
        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Windows nie uruchomił listy aplikacji.");
    }
}
