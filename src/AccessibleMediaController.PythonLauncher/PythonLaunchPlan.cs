using System.Diagnostics;

namespace AccessibleMediaController.PythonLauncher;

internal sealed record PythonLaunchPlan(ProcessStartInfo StartInfo, bool ShowCapturedOutput)
{
    internal static PythonLaunchPlan Create(
        VerifiedBundle bundle,
        IReadOnlyList<string> arguments)
    {
        var diagnostic = arguments.Any(argument =>
            string.Equals(argument, "--sprawdz", StringComparison.OrdinalIgnoreCase)
            || string.Equals(argument, "--check", StringComparison.OrdinalIgnoreCase));
        // Zawsze używamy wariantu konsolowego, ale chowamy jego okno i
        // przechwytujemy oba strumienie. pythonw.exe potrafi zamienić zapis
        // wykonany bardzo wcześnie przez interpreter w modalny alert „Python”.
        // Taki alert nie ma sensownej etykiety dla NVDA i blokuje interfejs.
        // python.exe z przekierowaniem zachowuje ten sam brak konsoli, a
        // diagnostyka trafia do kontrolowanego dziennika launchera.
        var runtime = Path.Combine(bundle.Root, "runtime", "python.exe");
        if (!File.Exists(runtime))
            throw new InvalidDataException("w pakiecie brakuje prywatnego środowiska Pythona");

        var start = new ProcessStartInfo
        {
            FileName = runtime,
            WorkingDirectory = bundle.Root,
            UseShellExecute = false,
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        start.ArgumentList.Add("-m");
        start.ArgumentList.Add(bundle.Manifest.PythonModule);
        foreach (var argument in arguments)
            start.ArgumentList.Add(argument);

        // Embeddable Python otrzymuje app z pliku python*._pth. Nie dziedziczy
        // przypadkowego interpretera ani PYTHONPATH z terminala użytkownika.
        start.Environment.Remove("PYTHONPATH");
        start.Environment.Remove("AMC_WX_LITE_PYTHON");
        start.Environment.Remove("AMC_POST_UPDATE_RESUME");
        start.Environment["AMC_LITE_HOST"] = bundle.HostExecutable;
        start.Environment["AMC_RELEASE_PROFILE"] = bundle.Profile.ProfileId;
        start.Environment["AMC_SHELL_REQUESTS"] = "1";
        start.Environment["PYTHONUTF8"] = "1";
        // Kod pakietu jest objęty manifestem i sumami. Interpreter nie może
        // dopisywać obok niego niepodpisanych __pycache__ podczas testu startu
        // ani zwykłego używania zainstalowanej aplikacji.
        start.Environment["PYTHONDONTWRITEBYTECODE"] = "1";
        return new PythonLaunchPlan(start, diagnostic);
    }
}
