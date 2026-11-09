using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using AccessibleMediaController.ReleaseProfiles;

namespace AccessibleMediaController.PythonUpdater;

internal static class Program
{
    // Proces, skaner antywirusowy albo indeksator potrafi jeszcze przez ulamek
    // sekundy trzymac zamkniety plik EXE. Jedna natychmiastowa proba powodowala
    // poprawne wycofanie calej aktualizacji dokladnie na pliku aktualizatora.
    private static readonly TimeSpan FileReplacementTimeout = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan FileReplacementRetryDelay = TimeSpan.FromMilliseconds(200);
    private const int RescueTransactionRetention = 2;

    private const int SupportedBundleFormat = 1;
    private const int SupportedInterfaceVersion = 1;
    private const int SupportedHostProtocolVersion = 1;
    private const int Success = 0;
    private const int BadArguments = 2;
    private const int AppStillRunning = 3;
    private const int InvalidPackage = 4;
    private const int InstallFailed = 5;
    private const int UpdaterAlreadyRunning = 6;

    [STAThread]
    private static int Main(string[] args)
    {
        ReleaseProfileDescriptor? profile = null;
        var alertAfterClosure = false;
        var silentRequested = args.Any(argument =>
            string.Equals(argument, "--silent", StringComparison.OrdinalIgnoreCase));
        try
        {
            var options = Options.Parse(args);
            if (options is null)
            {
                if (!silentRequested)
                {
                    Show(null, "Podaj źródło aktualizacji i folder programu.\n\n"
                         + "Paczka lokalna: AMC-Python-Updater.exe --source KATALOG "
                         + "--target KATALOG [--cleanup-source]\n"
                         + "Pełny instalator: AMC-Python-Updater.exe --installer PLIK.EXE "
                         + "--installer-sha256 SUMA --target KATALOG "
                         + "[--cleanup-installer]\n"
                         + "Opcjonalnie: [--wait-pid NUMER] [--launch PLIK] [--silent]", true);
                }
                return BadArguments;
            }

            profile = LoadReleaseProfile(options.Source ?? options.Target);

            // Windows nie pozwala niezawodnie podmienić uruchomionego pliku
            // wykonywalnego. Gdy aktualizator startuje z katalogu AMC, najpierw
            // uruchamiamy jego roboczą kopię poza katalogiem instalacji. Dzięki
            // temu paczka może również bezpiecznie zaktualizować sam updater.
            if (RelaunchOutsideInstallation(options, args, profile))
                return Success;

            using var updaterInstance = UpdaterInstanceLease.TryAcquire(
                profile.UpdaterMutexName);
            if (!updaterInstance.IsOwner)
            {
                if (!options.Silent)
                    Show(profile, $"Aktualizator {profile.DisplayName} już działa.", true);
                return UpdaterAlreadyRunning;
            }

            // Kopia robocza nie może dotknąć pliku starego aktualizatora,
            // dopóki proces-rodzic nadal go wykonuje. Samo Process.Start i
            // natychmiastowy return powodowały na Windows wyścig: pomocnik
            // dochodził do AMC-Python-Updater.exe przed faktycznym wyjściem
            // rodzica, a następnie cała transakcja poprawnie się wycofywała.
            if (!WaitForApplication(options.WaitUpdaterPid))
            {
                Console.Error.WriteLine(
                    "Aktualizacja zatrzymana: nie zakończył się proces-rodzic aktualizatora.");
                if (!options.Silent)
                {
                    Show(profile, "Aktualizacja nie została rozpoczęta, ponieważ poprzedni "
                         + "proces aktualizatora nie zakończył się.", true);
                }
                return AppStillRunning;
            }

            // Najpierw sprawdzamy całą paczkę. Wadliwa albo niezgodna
            // aktualizacja nie może bez potrzeby zamknąć działającego AMC.
            Manifest? manifest = null;
            if (options.Source is not null)
                manifest = LoadAndVerify(options.Source, options.Target, profile);
            else
                VerifyInstaller(
                    options.Installer!,
                    options.InstallerSha256!,
                    options.Target,
                    profile);

            // Zwykłe WM_CLOSE uruchamia tę samą ścieżkę co Alt+F4. Interfejs
            // sprawdza żywy stan nagrywania i może bezpiecznie odmówić. Nigdy
            // nie zabijamy procesu i nie podmieniamy plików, dopóki choć jeden
            // składnik z katalogu instalacji nadal działa.
            if (!SafeApplicationStop.RequestAndWait(
                    options.Target,
                    profile,
                    options.WaitPid,
                    TimeSpan.FromSeconds(30)))
            {
                var blockers = InstalledProcessGuard.RunningComponents(
                    options.Target);
                Console.Error.WriteLine(
                    blockers.Count == 0
                        ? "Aktualizacja zatrzymana: wskazany proces AMC nadal istnieje."
                        : "Aktualizacja zatrzymana przez składniki instalacji: "
                          + string.Join(
                              "; ",
                              blockers.Select(item =>
                                  $"PID {item.ProcessId}: {item.Executable}")));
                if (!options.Silent)
                {
                    Show(profile, $"Aktualizacja nie została rozpoczęta, ponieważ {profile.DisplayName} nadal działa. "
                         + "Może trwać nagrywanie. Zatrzymaj je i spróbuj ponownie.", true);
                }
                return AppStillRunning;
            }
            // --wait-pid pilnuje procesu, który uruchomił aktualizację, ale
            // nie obejmuje osieroconego runtime\python.exe ani hosta audio.
            // Sprawdzamy cały katalog PRZED utworzeniem .aktualizacja-*.
            // Odmowa nie zostawia więc kopii transakcji i nie dochodzi do
            // częściowej podmiany działającej wersji.
            if (InstalledProcessGuard.HasRunningComponent(options.Target))
            {
                if (!options.Silent)
                {
                    Show(profile, $"Aktualizacja nie została rozpoczęta, ponieważ {profile.DisplayName} "
                         + "lub jeden z jego składników nadal działa. Może trwać "
                         + "nagrywanie. Zamknij program bezpiecznie i spróbuj ponownie.", true);
                }
                return AppStillRunning;
            }
            // Po bezpiecznym zamknięciu GUI nie ma już kto przeczytać
            // wyniku cichego pomocnika. Błąd musi mieć własny dostępny alert.
            alertAfterClosure = options.Silent
                && !string.IsNullOrWhiteSpace(options.Launch);
            PruneRescueTransactions(options.Target);
            if (options.Source is not null)
            {
                ApplyPackage(options.Source, options.Target, manifest!.Files);
                if (options.CleanupSource)
                    CleanupSourceBestEffort(options.Source, options.Target);
            }
            else
            {
                RunInstaller(options.Installer!, options.Target, profile);
                if (options.CleanupInstaller)
                    CleanupInstallerBestEffort(options.Installer!, options.Target);
            }

            if (!string.IsNullOrWhiteSpace(options.Launch))
            {
                // Launcher nie może startować, gdy nadal posiadamy mutex
                // aktualizacji. Pliki są już w tym miejscu zweryfikowane.
                updaterInstance.Dispose();
                var launch = ResolveInside(options.Target, options.Launch, mustExist: true);
                var start = new ProcessStartInfo
                {
                    FileName = launch,
                    WorkingDirectory = options.Target,
                    UseShellExecute = true,
                };
                start.ArgumentList.Add(PostUpdateLaunchContract.ActivateAfterUpdateSwitch);
                using var restarted = Process.Start(start);
                if (restarted is not null)
                    AllowForegroundBestEffort(restarted.Id);
            }
            return Success;
        }
        catch (InvalidDataException exception)
        {
            Console.Error.WriteLine("Pakiet aktualizacji został odrzucony: " + exception);
            if (!silentRequested)
                Show(profile, "Pakiet aktualizacji został odrzucony: " + exception.Message, true);
            return InvalidPackage;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(
                $"Nie udało się zainstalować aktualizacji {profile?.DisplayName ?? "AMC"}: "
                + exception);
            if (!silentRequested || alertAfterClosure)
                Show(
                    profile,
                    $"Nie udało się zainstalować aktualizacji {profile?.DisplayName ?? "AMC"}: "
                    + exception.Message,
                    true);
            return InstallFailed;
        }
    }

    private static void CleanupSourceBestEffort(string source, string target)
    {
        // Tylko jawny, zweryfikowany katalog źródłowy. Nigdy nie sprzątamy
        // katalogu instalacji ani jego rodzica. Niepowodzenie porządkowe nie
        // odwraca już poprawnie zastosowanej aktualizacji.
        try
        {
            var resolvedSource = Path.GetFullPath(source);
            var resolvedTarget = Path.GetFullPath(target);
            if (string.Equals(
                    resolvedSource.TrimEnd(Path.DirectorySeparatorChar),
                    resolvedTarget.TrimEnd(Path.DirectorySeparatorChar),
                    StringComparison.OrdinalIgnoreCase)
                || IsInside(resolvedSource, resolvedTarget)
                || IsInside(resolvedTarget, resolvedSource))
                return;
            Directory.Delete(resolvedSource, recursive: true);
        }
        catch (Exception exception) when (
            exception is IOException
                or UnauthorizedAccessException
                or DirectoryNotFoundException)
        {
            Console.Error.WriteLine(
                "Nie udało się usunąć katalogu przygotowanej aktualizacji: "
                + exception.Message);
        }
    }

    private static void CleanupInstallerBestEffort(string installer, string target)
    {
        // Usuwamy wyłącznie jeden, wcześniej zweryfikowany plik instalatora.
        // Katalogu pamięci podręcznej nie kasujemy rekurencyjnie: może zawierać
        // inną rozpoczętą aktualizację, której ten proces nie jest właścicielem.
        try
        {
            var resolvedInstaller = Path.GetFullPath(installer);
            if (IsInside(target, resolvedInstaller))
                return;
            File.Delete(resolvedInstaller);
            var parent = Path.GetDirectoryName(resolvedInstaller);
            if (!string.IsNullOrWhiteSpace(parent)
                && Directory.Exists(parent)
                && !Directory.EnumerateFileSystemEntries(parent).Any())
                Directory.Delete(parent);
        }
        catch (Exception exception) when (
            exception is IOException
                or UnauthorizedAccessException
                or DirectoryNotFoundException)
        {
            Console.Error.WriteLine(
                "Nie udało się usunąć użytego instalatora aktualizacji: "
                + exception.Message);
        }
    }

    private static bool RelaunchOutsideInstallation(
        Options options,
        string[] args,
        ReleaseProfileDescriptor profile)
    {
        var current = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(current) || !IsInside(options.Target, current))
            return false;

        var helperDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            profile.UpdaterHelperFolder.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(helperDirectory);
        // Stala nazwa pomocnika byla druga blokada: poprzedni alert albo
        // skaner mogl nadal trzymac ten jeden plik i kolejna proba konczyla
        // sie jeszcze przed transakcja. Stare kopie sprzatamy best effort, a
        // kazde uruchomienie dostaje wlasna nazwe.
        foreach (var oldHelper in Directory.EnumerateFiles(
                     helperDirectory,
                     profile.UpdaterHelperExecutablePrefix + "*.exe"))
        {
            try
            {
                File.Delete(oldHelper);
            }
            catch (Exception exception) when (
                exception is IOException or UnauthorizedAccessException)
            {
                // Dzialajacy stary pomocnik moze jeszcze konczyc poprzednia
                // probe. Unikalna nazwa ponizej nie musi na niego czekac.
            }
        }
        var helper = Path.Combine(
            helperDirectory,
            $"{profile.UpdaterHelperExecutablePrefix}-{Guid.NewGuid():N}.exe");
        File.Copy(current, helper, overwrite: true);

        var start = new ProcessStartInfo
        {
            FileName = helper,
            WorkingDirectory = helperDirectory,
            UseShellExecute = false,
        };
        foreach (var argument in args)
            start.ArgumentList.Add(argument);
        // Ten wewnętrzny identyfikator jest niezależny od --wait-pid
        // właściwej aplikacji. Pomocnik musi zaczekać na oba procesy.
        start.ArgumentList.Add("--wait-updater-pid");
        start.ArgumentList.Add(Environment.ProcessId.ToString());
        Process.Start(start);
        return true;
    }

    private static bool WaitForApplication(int? pid)
    {
        if (pid is null || pid <= 0) return true;
        try
        {
            using var process = Process.GetProcessById(pid.Value);
            return process.WaitForExit(30_000);
        }
        catch (ArgumentException)
        {
            return true;
        }
    }

    private static ReleaseProfileDescriptor LoadReleaseProfile(string source) =>
        ResolveReleaseProfile(ReadManifest(source));

    private static Manifest ReadManifest(string root)
    {
        root = Path.GetFullPath(root);
        if (!Directory.Exists(root))
            throw new InvalidDataException("nie ma folderu pakietu");
        var manifestPath = Path.Combine(root, "bundle-manifest.json");
        if (!File.Exists(manifestPath))
            throw new InvalidDataException("brak bundle-manifest.json");
        if (new FileInfo(manifestPath).Length > 8 * 1024 * 1024)
            throw new InvalidDataException("bundle-manifest.json jest zbyt duży");
        try
        {
            return JsonSerializer.Deserialize<Manifest>(
                File.ReadAllText(manifestPath),
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                ?? throw new InvalidDataException("pusty manifest");
        }
        catch (JsonException)
        {
            throw new InvalidDataException("bundle-manifest.json ma nieprawidłowy format");
        }
    }

    private static ReleaseProfileDescriptor ResolveReleaseProfile(Manifest manifest) =>
        ReleaseProfileContract.Validate(
            manifest.ReleaseProfileSchema,
            manifest.ReleaseProfile,
            manifest.ProductId,
            manifest.DisplayName,
            manifest.InstallId,
            manifest.UpdateChannel,
            manifest.UiStateFolder,
            manifest.RoamingProfileFolder,
            manifest.LocalProfileFolder,
            manifest.CacheFolder,
            manifest.LogFolder,
            manifest.CredentialNamespace,
            manifest.Sessions,
            manifest.Capabilities);

    internal static Manifest LoadAndVerify(
        string source,
        string target,
        ReleaseProfileDescriptor expectedProfile)
    {
        source = Path.GetFullPath(source);
        target = Path.GetFullPath(target);
        if (!Directory.Exists(source)) throw new InvalidDataException("nie ma folderu źródłowego");
        if (!Directory.Exists(target)) throw new InvalidDataException("nie ma folderu programu");
        if (string.Equals(source.TrimEnd('\\'), target.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("źródło i folder programu są tym samym miejscem");
        if (!File.Exists(Path.Combine(target, "AMC.exe")))
            throw new InvalidDataException(
                $"folder docelowy nie zawiera {expectedProfile.DisplayName}");

        var manifest = ReadManifest(source);
        var sourceProfile = ResolveReleaseProfile(manifest);
        if (!string.Equals(
                sourceProfile.InstallId,
                expectedProfile.InstallId,
                StringComparison.Ordinal))
            throw new InvalidDataException("profil pakietu zmienił się podczas uruchamiania aktualizacji");
        ValidateInstalledProfile(target, sourceProfile);
        if (manifest.Files is null || manifest.Files.Count == 0)
            throw new InvalidDataException("manifest nie zawiera plików");
        if (manifest.BundleFormatVersion != SupportedBundleFormat
            || manifest.InterfaceVersion != SupportedInterfaceVersion
            || manifest.HostProtocolVersion != SupportedHostProtocolVersion)
            throw new InvalidDataException("wersje składników aktualizacji nie są zgodne");
        if (!string.Equals(manifest.Application, "AMC Python", StringComparison.Ordinal)
            || !string.Equals(manifest.EntryPoint, "AMC.exe", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("manifest dotyczy innego programu");
        if (!manifest.Files.Contains("AMC.exe", StringComparer.OrdinalIgnoreCase))
            throw new InvalidDataException("aktualizacja nie zawiera AMC.exe");
        if (manifest.FileHashes is null || manifest.FileHashes.Count != manifest.Files.Count)
            throw new InvalidDataException("manifest nie zawiera sum wszystkich plików");
        if (manifest.FileSizes is not null
            && manifest.FileSizes.Count > 0
            && manifest.FileSizes.Count != manifest.Files.Count)
            throw new InvalidDataException("manifest nie zawiera rozmiarów wszystkich plików");

        foreach (var relative in manifest.Files)
        {
            var path = ResolveInside(source, relative, mustExist: true);
            if (manifest.FileSizes is { Count: > 0 }
                && (!manifest.FileSizes.TryGetValue(relative, out var expectedSize)
                    || expectedSize < 0
                    || new FileInfo(path).Length != expectedSize))
                throw new InvalidDataException($"niezgodny rozmiar pliku {relative}");
            if (!manifest.FileHashes.TryGetValue(relative, out var expected)
                || expected.Length != 64)
                throw new InvalidDataException($"brak sumy SHA-256 dla {relative}");
            var actual = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));
            if (!string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException($"niezgodna suma SHA-256 pliku {relative}");
            _ = ResolveInside(target, relative, mustExist: false);
        }
        return manifest;
    }

    internal static void VerifyInstaller(
        string installer,
        string expectedSha256,
        string target,
        ReleaseProfileDescriptor expectedProfile)
    {
        installer = Path.GetFullPath(installer);
        target = Path.GetFullPath(target);
        if (!Directory.Exists(target) || !File.Exists(Path.Combine(target, "AMC.exe")))
            throw new InvalidDataException(
                $"folder docelowy nie zawiera {expectedProfile.DisplayName}");
        if (!File.Exists(installer) || IsInside(target, installer))
            throw new InvalidDataException("brak bezpiecznego pliku instalatora");
        if (!string.Equals(Path.GetExtension(installer), ".exe", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("aktualizacja nie jest instalatorem EXE");

        var expectedPrefix = expectedProfile.ProfileId == "test"
            ? "AMC-Test-"
            : "AMC-Public-";
        if (!Path.GetFileName(installer).StartsWith(expectedPrefix, StringComparison.Ordinal))
            throw new InvalidDataException("instalator jest przeznaczony dla innego wydania AMC");
        if (expectedSha256.Length != 64
            || expectedSha256.Any(character => !Uri.IsHexDigit(character)))
            throw new InvalidDataException("nieprawidłowa suma SHA-256 instalatora");

        ValidateInstalledProfile(target, expectedProfile);
        using var input = new FileStream(
            installer,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read);
        var actual = Convert.ToHexString(SHA256.HashData(input));
        if (!string.Equals(actual, expectedSha256, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("niezgodna suma SHA-256 instalatora");
    }

    private static void RunInstaller(
        string installer, string target, ReleaseProfileDescriptor profile)
    {
        var start = new ProcessStartInfo
        {
            FileName = installer,
            WorkingDirectory = Path.GetDirectoryName(installer)!,
            UseShellExecute = false,
        };
        foreach (var argument in new[]
                 {
                     "/VERYSILENT",
                     "/SUPPRESSMSGBOXES",
                     "/NORESTART",
                     "/SP-",
                     "/LANG=polski",
                     "/DIR=" + target,
                 })
            start.ArgumentList.Add(argument);

        using var process = Process.Start(start)
            ?? throw new IOException("nie udało się uruchomić instalatora");
        if (!process.WaitForExit((int)TimeSpan.FromMinutes(15).TotalMilliseconds))
            throw new IOException("instalator nie zakończył się w wyznaczonym czasie");
        try
        {
            VerifyInstalledBundle(target, profile);
        }
        catch (Exception error) when (error is InvalidDataException or IOException)
        {
            throw new IOException(
                $"Instalacja jest niekompletna (kod instalatora {process.ExitCode}). "
                + "Zamknij AMC i uruchom ponownie ten sam instalator. "
                + error.Message, error);
        }
        if (process.ExitCode != 0)
            throw new IOException(
                $"instalator zakończył się kodem {process.ExitCode}; "
                + "poprzednia instalacja pozostała kompletna");
    }

    internal static void VerifyInstalledBundle(
        string target, ReleaseProfileDescriptor expectedProfile)
    {
        var manifest = ReadManifest(target);
        var installedProfile = ResolveReleaseProfile(manifest);
        if (!string.Equals(installedProfile.InstallId, expectedProfile.InstallId,
                StringComparison.Ordinal))
            throw new InvalidDataException("zainstalowano inny profil AMC");
        if (manifest.Files is null || manifest.Files.Count == 0
            || manifest.FileHashes is null
            || manifest.FileHashes.Count != manifest.Files.Count
            || manifest.FileSizes is null
            || manifest.FileSizes.Count != manifest.Files.Count)
            throw new InvalidDataException("manifest instalacji nie zawiera wszystkich plików");
        foreach (var relative in manifest.Files)
        {
            var path = ResolveInside(target, relative, mustExist: true);
            if (!manifest.FileSizes.TryGetValue(relative, out var size)
                || size < 0 || new FileInfo(path).Length != size)
                throw new InvalidDataException($"niezgodny rozmiar pliku {relative}");
            if (!manifest.FileHashes.TryGetValue(relative, out var expected)
                || expected.Length != 64 || expected.Any(character => !Uri.IsHexDigit(character)))
                throw new InvalidDataException($"brak sumy SHA-256 pliku {relative}");
            using var input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            var actual = Convert.ToHexString(SHA256.HashData(input));
            if (!string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException($"niezgodna suma SHA-256 pliku {relative}");
        }
    }

    private static void ValidateInstalledProfile(
        string target,
        ReleaseProfileDescriptor sourceProfile)
    {
        var installed = ReadManifest(target);
        if (installed.ReleaseProfileSchema == 0
            && string.IsNullOrWhiteSpace(installed.ReleaseProfile))
        {
            var folder = Path.GetFileName(
                Path.GetFullPath(target).TrimEnd(
                    Path.DirectorySeparatorChar,
                    Path.AltDirectorySeparatorChar));
            if (sourceProfile != ReleaseProfileContract.Test
                || !string.Equals(folder, "AMC Python", StringComparison.OrdinalIgnoreCase)
                || !string.Equals(installed.Application, "AMC Python", StringComparison.Ordinal)
                || !string.Equals(installed.EntryPoint, "AMC.exe", StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException(
                    "starsza instalacja nie ma zgodnej tożsamości profilu wydania");
            return;
        }

        ReleaseProfileContract.ValidateCompatibleInstalled(
            sourceProfile,
            installed.ReleaseProfileSchema,
            installed.ReleaseProfile,
            installed.ProductId,
            installed.DisplayName,
            installed.InstallId,
            installed.UpdateChannel,
            installed.UiStateFolder,
            installed.RoamingProfileFolder,
            installed.LocalProfileFolder,
            installed.CacheFolder,
            installed.LogFolder,
            installed.CredentialNamespace,
            installed.Sessions,
            installed.Capabilities);
    }

    private static void ApplyPackage(string source, string target, IReadOnlyList<string> files)
    {
        var transaction = Path.Combine(target, ".aktualizacja-" + Guid.NewGuid().ToString("N"));
        var staged = Path.Combine(transaction, "nowe");
        var backup = Path.Combine(transaction, "kopia");
        Directory.CreateDirectory(staged);
        var replaced = new List<(string Destination, string? Backup)>();
        try
        {
            foreach (var relative in files)
            {
                var input = ResolveInside(source, relative, mustExist: true);
                var prepared = ResolveInside(staged, relative, mustExist: false);
                Directory.CreateDirectory(Path.GetDirectoryName(prepared)!);
                File.Copy(input, prepared, overwrite: true);
            }

            foreach (var relative in files)
            {
                var prepared = ResolveInside(staged, relative, mustExist: true);
                var destination = ResolveInside(target, relative, mustExist: false);
                Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                // Nie ruszaj pliku, ktory juz ma dokladnie oczekiwana tresc.
                // W szczegolnosci niezmieniony AMC.exe moze byc chroniony
                // przez Windows albo skaner, mimo ze aplikacja juz nie
                // dziala. Dotychczas aktualizator bez potrzeby usuwal taki
                // plik i wycofywal cala poprawna aktualizacje interfejsu.
                if (File.Exists(destination)
                    && FilesHaveSameContent(prepared, destination))
                {
                    File.Delete(prepared);
                    continue;
                }
                string? saved = null;
                if (File.Exists(destination))
                {
                    saved = ResolveInside(backup, relative, mustExist: false);
                    Directory.CreateDirectory(Path.GetDirectoryName(saved)!);
                    File.Copy(destination, saved, overwrite: true);
                }
                replaced.Add((destination, saved));
                ReplacePreparedFile(prepared, destination, relative);
            }

            File.Copy(
                Path.Combine(source, "bundle-manifest.json"),
                Path.Combine(target, "bundle-manifest.json"),
                overwrite: true);
            Directory.Delete(transaction, recursive: true);
        }
        catch
        {
            var rollbackComplete = true;
            for (var index = replaced.Count - 1; index >= 0; index--)
            {
                var (destination, saved) = replaced[index];
                try
                {
                    if (saved is not null && File.Exists(saved))
                        File.Copy(saved, destination, overwrite: true);
                    else if (File.Exists(destination))
                        File.Delete(destination);
                }
                catch
                {
                    rollbackComplete = false;
                    // Pierwotny błąd jest ważniejszy; zachowana kopia zostaje
                    // w folderze .aktualizacja-* do ręcznego odzyskania.
                }
            }
            // Gdy kazdy plik wrocil na miejsce, katalog transakcji nie jest
            // juz kopia ratunkowa. Dotad pojedyncza chwilowa blokada zostawiala
            // ponad sto megabajtow po kazdej probie aktualizacji.
            if (rollbackComplete)
            {
                try
                {
                    Directory.Delete(transaction, recursive: true);
                }
                catch
                {
                    // Nie zaslaniamy pierwotnego bledu aktualizacji tylko tym,
                    // ze system jeszcze nie pozwolil posprzatac katalogu.
                }
            }
            PruneRescueTransactions(
                target,
                rollbackComplete ? null : transaction);
            throw;
        }
    }

    internal static int PruneRescueTransactions(
        string target,
        string? protectedTransaction = null)
    {
        target = Path.GetFullPath(target);
        if (!Directory.Exists(target)) return 0;

        var protectedPath = string.IsNullOrWhiteSpace(protectedTransaction)
            ? null
            : Path.GetFullPath(protectedTransaction);
        List<DirectoryInfo> candidates;
        try
        {
            candidates = Directory
                .EnumerateDirectories(
                    target,
                    ".aktualizacja-*",
                    SearchOption.TopDirectoryOnly)
                .Select(path => new DirectoryInfo(path))
                .Where(directory =>
                {
                    var suffix = directory.Name[".aktualizacja-".Length..];
                    return Guid.TryParseExact(suffix, "N", out _)
                           && directory.Parent is not null
                           && string.Equals(
                               Path.GetFullPath(directory.Parent.FullName)
                                   .TrimEnd(Path.DirectorySeparatorChar),
                               target.TrimEnd(Path.DirectorySeparatorChar),
                               StringComparison.OrdinalIgnoreCase)
                           && !directory.Attributes.HasFlag(FileAttributes.ReparsePoint);
                })
                .OrderByDescending(directory =>
                    protectedPath is not null
                    && string.Equals(
                        Path.GetFullPath(directory.FullName),
                        protectedPath,
                        StringComparison.OrdinalIgnoreCase))
                .ThenByDescending(directory => directory.LastWriteTimeUtc)
                .ThenByDescending(directory => directory.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
        catch (Exception exception) when (
            exception is IOException
                or UnauthorizedAccessException
                or DirectoryNotFoundException)
        {
            Console.Error.WriteLine(
                "Nie udało się sprawdzić kopii ratunkowych aktualizacji: "
                + exception.Message);
            return 0;
        }

        var removed = 0;
        foreach (var directory in candidates.Skip(RescueTransactionRetention))
        {
            try
            {
                Directory.Delete(directory.FullName, recursive: true);
                removed++;
            }
            catch (Exception exception) when (
                exception is IOException
                    or UnauthorizedAccessException
                    or DirectoryNotFoundException)
            {
                Console.Error.WriteLine(
                    "Nie udało się usunąć starej kopii ratunkowej aktualizacji: "
                    + exception.Message);
            }
        }
        return removed;
    }

    private static bool FilesHaveSameContent(string first, string second)
    {
        if (new FileInfo(first).Length != new FileInfo(second).Length)
            return false;

        using var firstStream = new FileStream(
            first,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete);
        using var secondStream = new FileStream(
            second,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete);
        var firstHash = SHA256.HashData(firstStream);
        var secondHash = SHA256.HashData(secondStream);
        return CryptographicOperations.FixedTimeEquals(firstHash, secondHash);
    }

    private static void ReplacePreparedFile(
        string prepared,
        string destination,
        string relative)
    {
        var deadline = DateTime.UtcNow + FileReplacementTimeout;
        while (true)
        {
            try
            {
                // File.Move(..., overwrite: true) na Windows potrafi odmowic
                // zastapienia pliku EXE. Kopia poprzedniej wersji juz istnieje,
                // wiec usuniecie i przeniesienie pozostaja transakcyjne.
                if (File.Exists(destination)) File.Delete(destination);
                File.Move(prepared, destination);
                return;
            }
            catch (Exception exception) when (
                exception is IOException or UnauthorizedAccessException)
            {
                if (DateTime.UtcNow >= deadline)
                {
                    throw new IOException(
                        $"nie można podmienić pliku {relative}: {exception.Message}",
                        exception);
                }
                Thread.Sleep(FileReplacementRetryDelay);
            }
        }
    }

    private static string ResolveInside(string root, string relative, bool mustExist)
    {
        if (string.IsNullOrWhiteSpace(relative) || Path.IsPathRooted(relative))
            throw new InvalidDataException("manifest zawiera nieprawidłową ścieżkę");
        var fullRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar)
                       + Path.DirectorySeparatorChar;
        var full = Path.GetFullPath(Path.Combine(fullRoot, relative.Replace('/', Path.DirectorySeparatorChar)));
        if (!full.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("manifest próbuje wyjść poza folder aktualizacji");
        if (mustExist && !File.Exists(full))
            throw new InvalidDataException($"brak pliku {relative}");
        return full;
    }

    private static bool IsInside(string root, string path)
    {
        var fullRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar)
                       + Path.DirectorySeparatorChar;
        var fullPath = Path.GetFullPath(path);
        return fullPath.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase);
    }

    private static void Show(
        ReleaseProfileDescriptor? profile,
        string text,
        bool error)
    {
        MessageBox(
            IntPtr.Zero,
            text,
            "Aktualizator " + (profile?.DisplayName ?? "AMC"),
            error ? 0x10u : 0x40u);
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int MessageBox(IntPtr owner, string text, string caption, uint type);

    private static void AllowForegroundBestEffort(int processId)
    {
        try
        {
            if (!AllowSetForegroundWindow((uint)processId))
            {
                Console.Error.WriteLine(
                    "Windows nie przekazał launcherowi uprawnienia pierwszego planu.");
            }
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(
                "Nie udało się przekazać launcherowi uprawnienia pierwszego planu: "
                + exception.Message);
        }
    }

    [DllImport("user32.dll")]
    private static extern bool AllowSetForegroundWindow(uint processId);

    internal sealed class Manifest
    {
        public int BundleFormatVersion { get; set; }
        public int InterfaceVersion { get; set; }
        public int HostProtocolVersion { get; set; }
        public string Application { get; set; } = "";
        public string EntryPoint { get; set; } = "";
        public int ReleaseProfileSchema { get; set; }
        public string ReleaseProfile { get; set; } = "";
        public string ProductId { get; set; } = "";
        public string DisplayName { get; set; } = "";
        public string InstallId { get; set; } = "";
        public string UpdateChannel { get; set; } = "";
        public string UiStateFolder { get; set; } = "";
        public string RoamingProfileFolder { get; set; } = "";
        public string LocalProfileFolder { get; set; } = "";
        public string CacheFolder { get; set; } = "";
        public string LogFolder { get; set; } = "";
        public string CredentialNamespace { get; set; } = "";
        public List<string> Sessions { get; set; } = [];
        public List<string> Capabilities { get; set; } = [];
        public List<string> Files { get; set; } = [];
        public Dictionary<string, string> FileHashes { get; set; } =
            new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, long> FileSizes { get; set; } =
            new(StringComparer.OrdinalIgnoreCase);
    }

    private sealed record Options(
        string? Source,
        string? Installer,
        string? InstallerSha256,
        string Target,
        int? WaitPid,
        int? WaitUpdaterPid,
        string? Launch,
        bool CleanupSource,
        bool CleanupInstaller,
        bool Silent)
    {
        public static Options? Parse(string[] args)
        {
            string? source = null;
            string? installer = null;
            string? installerSha256 = null;
            string? target = null;
            string? launch = null;
            int? waitPid = null;
            int? waitUpdaterPid = null;
            var cleanupSource = false;
            var cleanupInstaller = false;
            var silent = false;
            for (var index = 0; index < args.Length; index++)
            {
                switch (args[index].ToLowerInvariant())
                {
                    case "--source" when index + 1 < args.Length:
                        source = args[++index];
                        break;
                    case "--installer" when index + 1 < args.Length:
                        installer = args[++index];
                        break;
                    case "--installer-sha256" when index + 1 < args.Length:
                        installerSha256 = args[++index];
                        break;
                    case "--target" when index + 1 < args.Length:
                        target = args[++index];
                        break;
                    case "--wait-pid" when index + 1 < args.Length
                        && int.TryParse(args[++index], out var parsed):
                        waitPid = parsed;
                        break;
                    case "--wait-updater-pid" when index + 1 < args.Length
                        && int.TryParse(args[++index], out var parsedUpdater):
                        waitUpdaterPid = parsedUpdater;
                        break;
                    case "--launch" when index + 1 < args.Length:
                        launch = args[++index];
                        break;
                    case "--cleanup-source":
                        cleanupSource = true;
                        break;
                    case "--cleanup-installer":
                        cleanupInstaller = true;
                        break;
                    case "--silent":
                        silent = true;
                        break;
                    default:
                        return null;
                }
            }
            var packageMode = source is not null
                && installer is null
                && installerSha256 is null
                && !cleanupInstaller;
            var installerMode = source is null
                && installer is not null
                && installerSha256 is not null
                && !cleanupSource;
            return target is not null && (packageMode || installerMode)
                ? new Options(
                    source is null ? null : Path.GetFullPath(source),
                    installer is null ? null : Path.GetFullPath(installer),
                    installerSha256?.Trim(),
                    Path.GetFullPath(target),
                    waitPid,
                    waitUpdaterPid,
                    launch,
                    cleanupSource,
                    cleanupInstaller,
                    silent)
                : null;
        }
    }
}
