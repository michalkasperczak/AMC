using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using AccessibleMediaController.Core.ShellIntegration;
using Microsoft.Win32;

namespace AccessibleMediaController.Windows.Services;

/// <summary>
/// Wpisuje i wypisuje AMC z rejestru Windows: menu kontekstowe plikow oraz
/// zgloszenie sie jako mozliwosc w oknie "Otwórz za pomocą".
///
/// WSZYSTKO IDZIE DO HKEY_CURRENT_USER
/// AMC instaluje sie w profilu uzytkownika, bez uprawnien administratora, wiec
/// i wpisy sa tylko dla tego uzytkownika. Zaden zapis w tej klasie nie wymaga
/// podniesienia uprawnien i zaden nie rusza innych kont na komputerze.
///
/// CZEGO TA KLASA NIE ROBI
/// Nie ustawia AMC programem domyslnym. Windows 10 i 11 pilnuja wyboru
/// domyslnego programu skrotem kontrolnym w kluczu UserChoice i cofaja zmiany
/// zapisane poza swoim okienkiem - proba skonczylaby sie tym, ze uzytkownik
/// dostaje powiadomienie o przywroceniu domyslnej aplikacji, a AMC nadal nie
/// otwiera plikow. Dlatego jest <see cref="OpenWindowsDefaultAppsSettings"/> i
/// <see cref="ShowOpenWithDialog"/>: decyzje podejmuje uzytkownik w oknie
/// systemu, a my tylko go tam prowadzimy.
/// </summary>
internal static class ShellIntegrationManager
{
    private const string ClassesRoot = @"Software\Classes";
    private const string ApplicationsKey = @"Software\Classes\Applications";
    private const string RegisteredApplicationsKey = @"Software\RegisteredApplications";
    private const string CapabilitiesKey = @"Software\AccessibleMediaController\Capabilities";
    private const string ExecutableName = "AccessibleMediaController.exe";

    private static string ExecutablePath => Path.Combine(AppContext.BaseDirectory, ExecutableName);

    /// <summary>
    /// Wpisuje menu kontekstowe dla podanych rozszerzen i zglasza AMC jako
    /// mozliwosc otwarcia. Rozszerzenia nieobecne na liscie zostaja WYPISANE,
    /// zeby odznaczenie grupy w ustawieniach naprawde ja usuwalo.
    /// </summary>
    internal static ShellIntegrationResult Apply(IEnumerable<string> extensions)
    {
        var wanted = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var extension in extensions ?? [])
        {
            var normalized = ShellIntegrationRules.NormalizeExtension(extension);
            if (normalized is not null) wanted.Add(normalized);
        }

        try
        {
            var executable = ExecutablePath;
            if (!File.Exists(executable))
            {
                return new ShellIntegrationResult(
                    false,
                    "Nie znaleziono pliku programu, więc wpisy w menu kontekstowym nie zostały zmienione.");
            }

            RegisterApplicationItself(executable);

            var added = 0;
            foreach (var extension in ShellIntegrationRules.AllExtensions)
            {
                if (wanted.Contains(extension))
                {
                    RegisterExtension(extension, executable);
                    added++;
                }
                else
                {
                    UnregisterExtension(extension);
                }
            }

            NotifyShell();
            var message = added == 0
                ? "Wpisy AMC w menu kontekstowym zostały usunięte."
                : $"AMC pojawi się w menu po kliknięciu prawym przyciskiem dla {added} rodzajów plików.";
            DiagnosticLog.Info("skojarzenia", message);
            return new ShellIntegrationResult(true, message);
        }
        catch (Exception exception) when (exception is UnauthorizedAccessException
            or System.Security.SecurityException
            or IOException)
        {
            DiagnosticLog.Error("skojarzenia", "Nie udało się zapisać wpisów w rejestrze.", exception);
            return new ShellIntegrationResult(
                false,
                $"Nie udało się zapisać wpisów w systemie: {exception.Message}");
        }
    }

    /// <summary>Ktore rozszerzenia sa teraz wpisane. Do zaznaczenia pol w ustawieniach.</summary>
    internal static IReadOnlyList<string> ReadRegisteredExtensions()
    {
        var found = new List<string>();
        try
        {
            foreach (var extension in ShellIntegrationRules.AllExtensions)
            {
                var className = ShellIntegrationRules.ClassNameFor(extension);
                using var key = Registry.CurrentUser.OpenSubKey($@"{ClassesRoot}\{className}\shell\otworz\command");
                if (key is null) continue;
                var command = key.GetValue(null) as string;
                if (string.IsNullOrWhiteSpace(command)) continue;
                // Wpis po starej instalacji, ktora zniknela z dysku, nie liczy
                // sie jako dzialajace skojarzenie.
                if (!command.Contains(ExecutableName, StringComparison.OrdinalIgnoreCase)) continue;
                found.Add(extension);
            }
        }
        catch (Exception exception) when (exception is UnauthorizedAccessException
            or System.Security.SecurityException
            or IOException)
        {
            DiagnosticLog.Warning("skojarzenia", $"Nie udało się odczytać wpisów: {exception.Message}");
        }

        return found;
    }

    /// <summary>Usuwa wszystkie wpisy AMC. Wolane przy odinstalowaniu i na zadanie.</summary>
    internal static ShellIntegrationResult RemoveAll() => Apply([]);

    private static void RegisterExtension(string extension, string executable)
    {
        var className = ShellIntegrationRules.ClassNameFor(extension);
        using (var classKey = Registry.CurrentUser.CreateSubKey($@"{ClassesRoot}\{className}"))
        {
            classKey.SetValue(null, ShellIntegrationRules.FileTypeDescriptionFor(extension));
            classKey.SetValue("FriendlyTypeName", ShellIntegrationRules.FileTypeDescriptionFor(extension));
            using var icon = classKey.CreateSubKey("DefaultIcon");
            icon.SetValue(null, $"\"{executable}\",0");
            foreach (var verb in ShellIntegrationRules.Verbs)
            {
                using var verbKey = classKey.CreateSubKey($@"shell\{verb.Key}");
                verbKey.SetValue(null, verb.Label);
                using var commandKey = verbKey.CreateSubKey("command");
                commandKey.SetValue(null, $"\"{executable}\" {verb.ArgumentTemplate}");
            }
        }

        // OpenWithProgids sprawia, ze AMC widac na liscie "Otwórz za pomocą"
        // BEZ odbierania komukolwiek roli programu domyslnego. To jedyna
        // droga, ktorej Windows nie cofa.
        using var extensionKey = Registry.CurrentUser.CreateSubKey($@"{ClassesRoot}\{extension}");
        using var openWith = extensionKey.CreateSubKey("OpenWithProgids");
        openWith.SetValue(className, Array.Empty<byte>(), RegistryValueKind.None);
    }

    private static void UnregisterExtension(string extension)
    {
        var className = ShellIntegrationRules.ClassNameFor(extension);
        try
        {
            Registry.CurrentUser.DeleteSubKeyTree($@"{ClassesRoot}\{className}", throwOnMissingSubKey: false);
            using var extensionKey = Registry.CurrentUser.OpenSubKey($@"{ClassesRoot}\{extension}\OpenWithProgids", writable: true);
            extensionKey?.DeleteValue(className, throwOnMissingValue: false);
        }
        catch (Exception exception) when (exception is UnauthorizedAccessException
            or System.Security.SecurityException
            or IOException)
        {
            DiagnosticLog.Warning(
                "skojarzenia",
                $"Nie udało się usunąć wpisu dla {extension}: {exception.Message}");
        }
    }

    /// <summary>
    /// Zglasza sam program: nazwe, ikone, obslugiwane typy. Bez tego AMC nie
    /// pojawi sie w Ustawieniach Windows w "Aplikacje domyślne", wiec
    /// uzytkownik nie mialby gdzie go wybrac.
    /// </summary>
    private static void RegisterApplicationItself(string executable)
    {
        using (var appKey = Registry.CurrentUser.CreateSubKey($@"{ApplicationsKey}\{ExecutableName}"))
        {
            appKey.SetValue("FriendlyAppName", ShellIntegrationRules.ApplicationDisplayName);
            using var command = appKey.CreateSubKey(@"shell\open\command");
            command.SetValue(null, $"\"{executable}\" \"%1\"");
            using var supported = appKey.CreateSubKey("SupportedTypes");
            foreach (var extension in ShellIntegrationRules.AllExtensions)
            {
                supported.SetValue(extension, string.Empty);
            }
        }

        using (var capabilities = Registry.CurrentUser.CreateSubKey(CapabilitiesKey))
        {
            capabilities.SetValue("ApplicationName", ShellIntegrationRules.ApplicationDisplayName);
            capabilities.SetValue(
                "ApplicationDescription",
                "Odtwarzacz i rejestrator multimedialny dostępny dla czytników ekranu.");
            using var associations = capabilities.CreateSubKey("FileAssociations");
            foreach (var extension in ShellIntegrationRules.AllExtensions)
            {
                associations.SetValue(extension, ShellIntegrationRules.ClassNameFor(extension));
            }
        }

        using var registered = Registry.CurrentUser.CreateSubKey(RegisteredApplicationsKey);
        registered.SetValue(ShellIntegrationRules.ApplicationDisplayName, CapabilitiesKey);
    }

    /// <summary>
    /// Otwiera systemowe okno "Otwórz za pomocą" dla wskazanego pliku. Tam - i
    /// tylko tam - uzytkownik moze zaznaczyc "Zawsze używaj tej aplikacji".
    /// </summary>
    internal static bool ShowOpenWithDialog(IntPtr owner, string filePath)
    {
        try
        {
            var info = new OpenAsInfo
            {
                FileName = filePath,
                ClassName = null,
                // 0x4 = OAIF_EXEC: po wybraniu program od razu otwiera plik.
                // 0x10 = OAIF_HIDE_REGISTRATION nie uzywamy - chcemy, zeby
                // uzytkownik mogl ustawic AMC na stale.
                Flags = 0x4
            };
            var result = SHOpenWithDialog(owner, ref info);
            // 0 = zrobione; 0x800704C7 = uzytkownik anulowal, co nie jest bledem.
            return result == 0;
        }
        catch (Exception exception) when (exception is DllNotFoundException
            or EntryPointNotFoundException
            or COMException)
        {
            DiagnosticLog.Warning("skojarzenia", $"Nie udało się otworzyć okna wyboru programu: {exception.Message}");
            return false;
        }
    }

    /// <summary>
    /// Otwiera Ustawienia Windows na stronie aplikacji domyslnych AMC. To
    /// miejsce, w ktorym uzytkownik legalnie ustawia program domyslny.
    /// </summary>
    internal static bool OpenWindowsDefaultAppsSettings()
    {
        // Windows 11 przyjmuje nazwe programu w adresie i otwiera od razu jego
        // strone; starsze wydania zignoruja ogonek i pokaza ogolna liste.
        var addresses = new[]
        {
            $"ms-settings:defaultapps?registeredAppMachine={ShellIntegrationRules.ApplicationDisplayName}",
            "ms-settings:defaultapps"
        };

        foreach (var address in addresses)
        {
            try
            {
                Process.Start(new ProcessStartInfo(address) { UseShellExecute = true });
                return true;
            }
            catch (Exception exception) when (exception is System.ComponentModel.Win32Exception
                or InvalidOperationException
                or PlatformNotSupportedException)
            {
                DiagnosticLog.Warning(
                    "skojarzenia",
                    $"Nie udało się otworzyć Ustawień Windows adresem {address}: {exception.Message}");
            }
        }

        return false;
    }

    /// <summary>
    /// Mowi Eksploratorowi, ze skojarzenia sie zmienily. Bez tego menu
    /// kontekstowe pokazuje stary stan do ponownego zalogowania.
    /// </summary>
    private static void NotifyShell()
    {
        try
        {
            // 0x08000000 = SHCNE_ASSOCCHANGED, 0x1000 = SHCNF_FLUSH.
            SHChangeNotify(0x08000000, 0x1000, IntPtr.Zero, IntPtr.Zero);
        }
        catch (Exception exception) when (exception is DllNotFoundException
            or EntryPointNotFoundException)
        {
            DiagnosticLog.Warning("skojarzenia", $"Nie udało się odświeżyć menu Eksploratora: {exception.Message}");
        }
    }

    [DllImport("shell32.dll", SetLastError = true)]
    private static extern void SHChangeNotify(uint eventId, uint flags, IntPtr item1, IntPtr item2);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern int SHOpenWithDialog(IntPtr parent, ref OpenAsInfo info);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct OpenAsInfo
    {
        [MarshalAs(UnmanagedType.LPWStr)] public string FileName;
        [MarshalAs(UnmanagedType.LPWStr)] public string? ClassName;
        public int Flags;
    }
}

/// <summary>Wynik zapisu wpisow, z gotowym komunikatem dla uzytkownika.</summary>
internal sealed record ShellIntegrationResult(bool Success, string Message);
