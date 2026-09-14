using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AccessibleMediaController.Core.Updates;

namespace AccessibleMediaController.Windows.Services;

internal sealed record ApplicationUpdateStatus(
    ApplicationUpdateDecision Decision,
    string Message,
    string? AvailableVersion = null,
    bool ChecksumVerified = false,
    bool ReadyToInstall = false);

/// <summary>
/// Aktualizacja calego AMC z wydan GitHuba.
///
/// AMC nie ma instalatora - jest rozpowszechniany jako archiwum ZIP, ktore
/// uzytkownik rozpakowuje sam. Dlatego "cicha instalacja" nie moze polegac na
/// uruchomieniu instalatora z przelacznikiem. Zamiast tego: paczka jest
/// pobierana i sprawdzana w tle, rozpakowywana do katalogu obok biezacej
/// instalacji, a podmiana plikow dzieje sie DOPIERO po zamknieciu programu -
/// bo dzialajacego pliku .exe Windows nie pozwoli nadpisac.
/// </summary>
internal static class ApplicationUpdateManager
{
    internal const string RepositoryOwner = "michalkasperczak";
    internal const string RepositoryName = "AMC";
    internal static readonly Uri RepositoryUri = new($"https://github.com/{RepositoryOwner}/{RepositoryName}");

    private static readonly Uri ReleasesUri = new(
        $"https://api.github.com/repos/{RepositoryOwner}/{RepositoryName}/releases?per_page=15");

    private const long MaximumPackageBytes = 400L * 1024 * 1024;
    private static readonly SemaphoreSlim UpdateGate = new(1, 1);
    private static readonly HttpClient Client = CreateClient();

    private static string UpdateRoot => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "AccessibleMediaController",
        "updates");

    private static string PendingPath => Path.Combine(UpdateRoot, "pending.json");

    /// <summary>Wersja uruchomionego AMC, czytana z atrybutu zestawu.</summary>
    internal static string InstalledVersion
    {
        get
        {
            var assembly = typeof(ApplicationUpdateManager).Assembly;
            var informational = assembly
                .GetCustomAttributes(typeof(System.Reflection.AssemblyInformationalVersionAttribute), false)
                .OfType<System.Reflection.AssemblyInformationalVersionAttribute>()
                .FirstOrDefault()?.InformationalVersion;
            if (!string.IsNullOrWhiteSpace(informational)) return informational!;
            return assembly.GetName().Version?.ToString() ?? "0.0.0";
        }
    }

    /// <summary>
    /// Sprawdza wydania i zwraca decyzje. Gdy <paramref name="downloadAutomatically"/>
    /// jest wlaczone, pobiera i przygotowuje paczke od razu - w tle, bez pytania.
    /// </summary>
    internal static async Task<ApplicationUpdateStatus> CheckAsync(
        string channel,
        bool downloadAutomatically,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (!await UpdateGate.WaitAsync(TimeSpan.FromSeconds(1), cancellationToken).ConfigureAwait(false))
        {
            return new ApplicationUpdateStatus(
                ApplicationUpdateDecision.NotUnderstood,
                "Sprawdzanie aktualizacji AMC już trwa.");
        }

        try
        {
            var release = await FindReleaseAsync(channel, cancellationToken).ConfigureAwait(false);
            var plan = ApplicationUpdatePolicy.Evaluate(InstalledVersion, release, channel);

            if (plan.Decision != ApplicationUpdateDecision.UpdateAvailable)
            {
                DiagnosticLog.Info("aktualizacja-amc", plan.Message);
                return new ApplicationUpdateStatus(plan.Decision, plan.Message, plan.Release?.Tag);
            }

            // Bez sumy kontrolnej mowimy wprost, ze jej nie ma. Zapewnienie
            // "sprawdzono" przy pominietej weryfikacji byloby klamstwem o
            // bezpieczenstwie, a to najgorszy rodzaj cichego bledu.
            var message = plan.HasChecksum
                ? plan.Message
                : plan.Message + " Wydanie nie zawiera sumy kontrolnej, więc AMC nie może sprawdzić, "
                  + "czy pobrany plik jest nienaruszony.";

            if (!downloadAutomatically)
            {
                DiagnosticLog.Info("aktualizacja-amc", message);
                return new ApplicationUpdateStatus(plan.Decision, message, plan.Release!.Tag, plan.HasChecksum);
            }

            var prepared = await PrepareAsync(plan, progress, cancellationToken).ConfigureAwait(false);
            return prepared;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception) when (exception is HttpRequestException
            or IOException
            or UnauthorizedAccessException
            or InvalidDataException
            or InvalidOperationException
            or JsonException
            or TimeoutException)
        {
            DiagnosticLog.Error("aktualizacja-amc", "Sprawdzanie aktualizacji AMC nie powiodło się.", exception);
            return new ApplicationUpdateStatus(
                ApplicationUpdateDecision.NotUnderstood,
                $"Nie udało się sprawdzić aktualizacji AMC: {exception.Message}");
        }
        finally
        {
            UpdateGate.Release();
        }
    }

    private static async Task<ApplicationUpdateStatus> PrepareAsync(
        ApplicationUpdatePlan plan,
        IProgress<double>? progress,
        CancellationToken cancellationToken)
    {
        var release = plan.Release!;
        Directory.CreateDirectory(UpdateRoot);
        var stagingRoot = Path.Combine(UpdateRoot, $".staging-{Guid.NewGuid():N}");
        Directory.CreateDirectory(stagingRoot);

        try
        {
            var archive = Path.Combine(stagingRoot, release.PackageName ?? "amc.zip");
            await DownloadAsync(release.PackageUri!, archive, progress, cancellationToken).ConfigureAwait(false);

            var actual = await ComputeSha256Async(archive, cancellationToken).ConfigureAwait(false);
            var verified = false;
            if (plan.HasChecksum)
            {
                if (!string.Equals(actual, plan.ExpectedSha256, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidDataException(
                        "Pobrana paczka AMC ma inną sumę SHA-256 niż podana w opisie wydania. Plik został usunięty.");
                }
                verified = true;
            }

            progress?.Report(0.9d);

            // Instalator: nie ma czego rozpakowywac ani podmieniac. Plik
            // przenosimy poza katalog tymczasowy i uruchamiamy przy zamykaniu
            // AMC - instalator sam wymienia pliki i dociaga srodowisko .NET.
            if (release.PackageIsInstaller)
            {
                var readyInstaller = Path.Combine(UpdateRoot, release.PackageName ?? "amc-setup.exe");
                if (File.Exists(readyInstaller)) File.Delete(readyInstaller);
                File.Move(archive, readyInstaller);

                SavePending(new PendingUpdate
                {
                    Version = release.Tag,
                    Sha256 = actual,
                    ChecksumVerified = verified,
                    InstallerPath = readyInstaller,
                    TargetDirectory = AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar),
                    PreparedAtUtc = DateTimeOffset.UtcNow
                });

                progress?.Report(1d);
                var installerNote = verified
                    ? "Suma kontrolna zgodna."
                    : "Wydanie nie podało sumy kontrolnej, więc zgodność pliku nie została sprawdzona.";
                var installerMessage = $"AMC {release.Tag} jest pobrane i gotowe. {installerNote} "
                                       + "Instalator uruchomi się po zamknięciu programu.";
                DiagnosticLog.Info("aktualizacja-amc", $"{installerMessage} SHA-256 {actual}.");
                return new ApplicationUpdateStatus(
                    ApplicationUpdateDecision.UpdateAvailable,
                    installerMessage,
                    release.Tag,
                    verified,
                    ReadyToInstall: true);
            }

            var unpacked = Path.Combine(stagingRoot, "rozpakowane");
            Directory.CreateDirectory(unpacked);
            ExtractSafely(archive, unpacked);

            var executable = FindExecutable(unpacked)
                ?? throw new InvalidDataException(
                    "Pobrana paczka nie zawiera pliku AccessibleMediaController.exe, więc nie jest wydaniem AMC.");

            // Katalog gotowy do podmiany przenosimy POZA katalog tymczasowy,
            // zeby przetrwal zamkniecie programu.
            var readyRoot = Path.Combine(UpdateRoot, "gotowe");
            if (Directory.Exists(readyRoot)) Directory.Delete(readyRoot, recursive: true);
            Directory.Move(Path.GetDirectoryName(executable)!, readyRoot);

            SavePending(new PendingUpdate
            {
                Version = release.Tag,
                Sha256 = actual,
                ChecksumVerified = verified,
                SourceDirectory = readyRoot,
                TargetDirectory = AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar),
                PreparedAtUtc = DateTimeOffset.UtcNow
            });

            progress?.Report(1d);
            var note = verified
                ? "Suma kontrolna zgodna."
                : "Wydanie nie podało sumy kontrolnej, więc zgodność pliku nie została sprawdzona.";
            var message = $"AMC {release.Tag} jest pobrane i gotowe. {note} "
                          + "Zostanie zainstalowane po zamknięciu programu.";
            DiagnosticLog.Info("aktualizacja-amc", $"{message} SHA-256 {actual}.");
            return new ApplicationUpdateStatus(
                ApplicationUpdateDecision.UpdateAvailable, message, release.Tag, verified, ReadyToInstall: true);
        }
        finally
        {
            TryDeleteDirectory(stagingRoot);
        }
    }

    /// <summary>Czy jest pobrana paczka czekajaca na podmiane.</summary>
    internal static bool HasPendingUpdate(out string? version)
    {
        version = null;
        var pending = LoadPending();
        if (pending is null) return false;
        var ready = (pending.InstallerPath.Length > 0 && File.Exists(pending.InstallerPath))
                    || Directory.Exists(pending.SourceDirectory);
        if (!ready) return false;
        version = pending.Version;
        return true;
    }

    /// <summary>
    /// Uruchamia podmiane plikow. Wolane przy zamykaniu programu.
    ///
    /// Podmiana idzie przez osobny proces PowerShell, ktory czeka na zniknięcie
    /// AMC z listy procesow, kopiuje pliki i uruchamia nowa wersje. Wlasny proces
    /// nie moze nadpisac swojego .exe, dopoki dziala.
    /// </summary>
    internal static bool TryStartPendingInstall(bool relaunch)
    {
        try
        {
            var pending = LoadPending();
            if (pending is null) return false;

            // Droga instalatora: nie kopiujemy nic sami. Instalator w trybie
            // cichym sam zamyka AMC, wymienia pliki i w razie potrzeby dociaga
            // srodowisko .NET. Uruchamiamy go i konczymy - reszta nalezy do niego.
            if (pending.InstallerPath.Length > 0 && File.Exists(pending.InstallerPath))
            {
                var installer = new ProcessStartInfo
                {
                    FileName = pending.InstallerPath,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden
                };
                installer.ArgumentList.Add("/VERYSILENT");
                installer.ArgumentList.Add("/SUPPRESSMSGBOXES");
                installer.ArgumentList.Add("/NORESTART");
                // /CLOSEAPPLICATIONS pozwala instalatorowi zamknac AMC, gdyby
                // proces jeszcze zyl; /RESTARTAPPLICATIONS wraca do programu po
                // wymianie plikow, gdy uzytkownik chcial ponownego uruchomienia.
                installer.ArgumentList.Add("/CLOSEAPPLICATIONS");
                if (relaunch) installer.ArgumentList.Add("/RESTARTAPPLICATIONS");
                else installer.ArgumentList.Add("/NORESTARTAPPLICATIONS");
                installer.ArgumentList.Add($"/LOG={Path.Combine(UpdateRoot, "instalator.log")}");

                Process.Start(installer);
                // Sam wpis "do zrobienia" usuwamy, ale pliku instalatora NIE -
                // wlasnie go uruchomilismy, a usuniecie wyrwaloby mu plik z rak.
                try
                {
                    if (File.Exists(PendingPath)) File.Delete(PendingPath);
                }
                catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
                {
                    DiagnosticLog.Warning(
                        "aktualizacja-amc",
                        $"Instalator uruchomiony, ale nie udało się usunąć wpisu o czekającej aktualizacji: {exception.Message}");
                }
                DiagnosticLog.Info(
                    "aktualizacja-amc",
                    $"Uruchomiono instalator AMC {pending.Version} po zamknięciu programu.");
                return true;
            }

            if (!Directory.Exists(pending.SourceDirectory)) return false;
            if (!Directory.Exists(pending.TargetDirectory)) return false;

            var script = Path.Combine(UpdateRoot, "instaluj.ps1");
            File.WriteAllText(script, BuildInstallScript(), new UTF8Encoding(true));

            var start = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                UseShellExecute = false,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden
            };
            start.ArgumentList.Add("-NoProfile");
            start.ArgumentList.Add("-ExecutionPolicy");
            start.ArgumentList.Add("Bypass");
            start.ArgumentList.Add("-WindowStyle");
            start.ArgumentList.Add("Hidden");
            start.ArgumentList.Add("-File");
            start.ArgumentList.Add(script);
            start.ArgumentList.Add("-ProcessId");
            start.ArgumentList.Add(Environment.ProcessId.ToString());
            start.ArgumentList.Add("-Source");
            start.ArgumentList.Add(pending.SourceDirectory);
            start.ArgumentList.Add("-Target");
            start.ArgumentList.Add(pending.TargetDirectory);
            start.ArgumentList.Add("-Version");
            start.ArgumentList.Add(pending.Version ?? "");
            if (relaunch) start.ArgumentList.Add("-Relaunch");

            Process.Start(start);
            DiagnosticLog.Info(
                "aktualizacja-amc",
                $"Rozpoczęto instalację AMC {pending.Version} po zamknięciu programu.");
            return true;
        }
        catch (Exception exception) when (exception is IOException
            or UnauthorizedAccessException
            or Win32Exception
            or JsonException
            or InvalidOperationException)
        {
            DiagnosticLog.Error("aktualizacja-amc", "Nie udało się rozpocząć instalacji aktualizacji.", exception);
            return false;
        }
    }

    /// <summary>Usuwa przygotowana paczke - np. gdy uzytkownik wylaczy aktualizacje.</summary>
    internal static void DiscardPending()
    {
        try
        {
            var pending = LoadPending();
            if (pending is not null) TryDeleteDirectory(pending.SourceDirectory);
            if (File.Exists(PendingPath)) File.Delete(PendingPath);
        }
        catch (Exception exception) when (exception is IOException
            or UnauthorizedAccessException
            or JsonException)
        {
            DiagnosticLog.Warning("aktualizacja-amc", $"Nie udało się usunąć paczki: {exception.Message}");
        }
    }

    /// <summary>
    /// Skrypt podmiany. Kopiuje pliki, ale NIE usuwa katalogu docelowego -
    /// gdyby AMC bylo rozpakowane obok cudzych plikow uzytkownika, usuniecie
    /// calego katalogu zabraloby mu je bez ostrzezenia.
    /// </summary>
    internal static string BuildInstallScript() => """
        param(
            [Parameter(Mandatory=$true)][int]$ProcessId,
            [Parameter(Mandatory=$true)][string]$Source,
            [Parameter(Mandatory=$true)][string]$Target,
            [string]$Version = '',
            [switch]$Relaunch
        )

        $ErrorActionPreference = 'Stop'
        $dziennik = Join-Path $env:LOCALAPPDATA 'AccessibleMediaController\updates\instalacja.log'

        function Zapisz($tekst) {
            $wiersz = "{0} {1}" -f (Get-Date -Format 'yyyy-MM-dd HH:mm:ss'), $tekst
            Add-Content -LiteralPath $dziennik -Value $wiersz -Encoding UTF8
        }

        try {
            Zapisz "Instalacja AMC $Version: czekam na zamkniecie procesu $ProcessId."
            for ($proba = 0; $proba -lt 60; $proba++) {
                $proces = Get-Process -Id $ProcessId -ErrorAction SilentlyContinue
                if (-not $proces) { break }
                Start-Sleep -Milliseconds 500
            }
            if (Get-Process -Id $ProcessId -ErrorAction SilentlyContinue) {
                Zapisz 'AMC nadal dziala po 30 sekundach. Przerywam - pliki zostaja nietkniete.'
                exit 1
            }

            if (-not (Test-Path -LiteralPath $Source)) {
                Zapisz "Brak katalogu zrodlowego $Source. Przerywam."
                exit 1
            }

            $kopia = Join-Path (Split-Path -Parent $Target) ('AMC-poprzednia-' + (Get-Date -Format 'yyyyMMdd-HHmmss'))
            Zapisz "Kopia poprzedniej wersji: $kopia"
            New-Item -ItemType Directory -Force -Path $kopia | Out-Null
            Copy-Item -LiteralPath (Join-Path $Target '*') -Destination $kopia -Recurse -Force -ErrorAction SilentlyContinue

            Zapisz 'Kopiuje nowe pliki.'
            Copy-Item -LiteralPath (Join-Path $Source '*') -Destination $Target -Recurse -Force

            $program = Join-Path $Target 'AccessibleMediaController.exe'
            if (-not (Test-Path -LiteralPath $program)) {
                Zapisz 'Po kopiowaniu brakuje AccessibleMediaController.exe. Przywracam poprzednia wersje.'
                Copy-Item -LiteralPath (Join-Path $kopia '*') -Destination $Target -Recurse -Force
                exit 1
            }

            Remove-Item -LiteralPath $Source -Recurse -Force -ErrorAction SilentlyContinue
            $stan = Join-Path $env:LOCALAPPDATA 'AccessibleMediaController\updates\pending.json'
            Remove-Item -LiteralPath $stan -Force -ErrorAction SilentlyContinue

            Zapisz "Zainstalowano AMC $Version."
            if ($Relaunch) {
                Zapisz 'Uruchamiam nowa wersje.'
                Start-Process -FilePath $program
            }
            exit 0
        }
        catch {
            Zapisz ("Blad instalacji: " + $_.Exception.Message)
            exit 1
        }
        """;

    private static async Task<ApplicationRelease?> FindReleaseAsync(
        string channel,
        CancellationToken cancellationToken)
    {
        using var response = await Client
            .GetAsync(ReleasesUri, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        if (document.RootElement.ValueKind != JsonValueKind.Array) return null;

        var allowPrerelease = ApplicationUpdatePolicy.IsPrereleaseAllowed(channel);
        ApplicationRelease? best = null;
        ApplicationVersion? bestVersion = null;
        ApplicationRelease? newestOverall = null;
        ApplicationVersion? newestOverallVersion = null;

        foreach (var element in document.RootElement.EnumerateArray())
        {
            if (element.TryGetProperty("draft", out var draft) && draft.ValueKind == JsonValueKind.True) continue;

            var tag = element.TryGetProperty("tag_name", out var tagValue) ? tagValue.GetString() : null;
            if (string.IsNullOrWhiteSpace(tag)) continue;
            var prerelease = element.TryGetProperty("prerelease", out var pre) && pre.ValueKind == JsonValueKind.True;
            var notes = element.TryGetProperty("body", out var body) ? body.GetString() : null;

            Uri? packageUri = null;
            string? packageName = null;
            long packageBytes = 0;
            var packageIsInstaller = false;
            if (element.TryGetProperty("assets", out var assets) && assets.ValueKind == JsonValueKind.Array)
            {
                // Wybor pliku trzyma polityka w Core, zeby dala sie sprawdzic
                // testem - tutaj tylko odnajdujemy wybrany plik po nazwie.
                var namesInRelease = new List<string>();
                foreach (var asset in assets.EnumerateArray())
                {
                    var name = asset.TryGetProperty("name", out var nameValue) ? nameValue.GetString() : null;
                    if (!string.IsNullOrWhiteSpace(name)) namesInRelease.Add(name!);
                }

                var chosen = ApplicationUpdatePolicy.ChoosePackageName(namesInRelease);
                if (chosen is not null)
                {
                    foreach (var asset in assets.EnumerateArray())
                    {
                        var name = asset.TryGetProperty("name", out var nameValue) ? nameValue.GetString() : null;
                        if (!string.Equals(name, chosen, StringComparison.Ordinal)) continue;
                        var url = asset.TryGetProperty("browser_download_url", out var urlValue)
                            ? urlValue.GetString()
                            : null;
                        if (string.IsNullOrWhiteSpace(url)) break;
                        if (!Uri.TryCreate(url, UriKind.Absolute, out var parsed)) break;
                        packageUri = parsed;
                        packageName = chosen;
                        packageIsInstaller = chosen.EndsWith(".exe", StringComparison.OrdinalIgnoreCase);
                        packageBytes = asset.TryGetProperty("size", out var size) && size.TryGetInt64(out var bytes)
                            ? bytes
                            : 0;
                        break;
                    }
                }
            }

            var candidate = new ApplicationRelease(
                tag!,
                notes,
                packageUri,
                packageName,
                packageBytes,
                prerelease,
                packageIsInstaller);

            if (!ApplicationVersion.TryParse(tag, out var version)) continue;

            // Najnowsze wydanie WOGOLE liczymy po numerze wersji, a nie po
            // kolejnosci z GitHuba: kolejnosc listy nie jest obiecana i zalezy
            // od daty utworzenia, ktora przy poprawianym wydaniu bywa starsza
            // niz przy wydaniu wczesniejszym.
            if (newestOverallVersion is null || version > newestOverallVersion)
            {
                newestOverall = candidate;
                newestOverallVersion = version;
            }

            if (prerelease && !allowPrerelease) continue;
            if (bestVersion is null || version > bestVersion)
            {
                best = candidate;
                bestVersion = version;
            }
        }

        // Gdy kanal stabilny odrzucil wszystko, oddajemy najnowsze wydanie mimo
        // wszystko - regula decyzyjna wyjasni uzytkownikowi, ze blokuje je kanal.
        // Milczace "brak aktualizacji" nie powiedzialoby mu, ze cos jest.
        return best ?? newestOverall;
    }

    private static async Task DownloadAsync(
        Uri uri,
        string destination,
        IProgress<double>? progress,
        CancellationToken cancellationToken)
    {
        using var response = await Client.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        var length = response.Content.Headers.ContentLength;
        if (length is > MaximumPackageBytes)
            throw new InvalidDataException("Paczka AMC przekracza bezpieczny limit rozmiaru.");

        await using var input = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        await using var output = new FileStream(
            destination,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            128 * 1024,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        var buffer = new byte[128 * 1024];
        long total = 0;
        int read;
        while ((read = await input.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
        {
            total += read;
            if (total > MaximumPackageBytes)
                throw new InvalidDataException("Paczka AMC przekracza bezpieczny limit rozmiaru.");
            await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
            if (length is > 0) progress?.Report(0.05d + 0.8d * total / length.Value);
        }
        await output.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Rozpakowanie z kontrola sciezek. Wpis w archiwum moze zawierac ".." i
    /// wskazac plik poza katalogiem docelowym - taka paczka nadpisalaby dowolny
    /// plik uzytkownika.
    /// </summary>
    internal static void ExtractSafely(string archivePath, string destinationRoot)
    {
        var root = Path.GetFullPath(destinationRoot);
        using var archive = ZipFile.OpenRead(archivePath);
        long total = 0;
        foreach (var entry in archive.Entries)
        {
            var target = Path.GetFullPath(Path.Combine(root, entry.FullName));
            if (!IsWithin(target, root))
                throw new InvalidDataException($"Paczka zawiera wpis wskazujący poza katalog docelowy: {entry.FullName}");

            if (entry.FullName.EndsWith('/') || entry.FullName.EndsWith('\\') || entry.Length == 0 && entry.Name.Length == 0)
            {
                Directory.CreateDirectory(target);
                continue;
            }

            total += entry.Length;
            if (total > MaximumPackageBytes)
                throw new InvalidDataException("Rozpakowana paczka AMC przekracza bezpieczny limit rozmiaru.");

            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            entry.ExtractToFile(target, overwrite: true);
        }
    }

    private static string? FindExecutable(string root)
    {
        const string name = "AccessibleMediaController.exe";
        var direct = Path.Combine(root, name);
        if (File.Exists(direct)) return direct;
        // Paczka czesto ma jeden katalog wierzchni - zagladamy o poziom glebiej.
        foreach (var directory in Directory.EnumerateDirectories(root))
        {
            var nested = Path.Combine(directory, name);
            if (File.Exists(nested)) return nested;
        }
        return null;
    }

    private static async Task<string> ComputeSha256Async(string path, CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(
            path, FileMode.Open, FileAccess.Read, FileShare.Read, 128 * 1024,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        var hash = await SHA256.HashDataAsync(stream, cancellationToken).ConfigureAwait(false);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static bool IsWithin(string candidate, string root)
    {
        var normalizedRoot = root.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        return candidate.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase);
    }

    private static PendingUpdate? LoadPending()
    {
        if (!File.Exists(PendingPath)) return null;
        return JsonSerializer.Deserialize<PendingUpdate>(File.ReadAllText(PendingPath));
    }

    private static void SavePending(PendingUpdate pending)
    {
        Directory.CreateDirectory(UpdateRoot);
        File.WriteAllText(PendingPath, JsonSerializer.Serialize(pending, new JsonSerializerOptions
        {
            WriteIndented = true
        }));
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path)) Directory.Delete(path, recursive: true);
        }
        catch (Exception)
        {
        }
    }

    private static HttpClient CreateClient()
    {
        var client = new HttpClient
        {
            Timeout = TimeSpan.FromMinutes(10)
        };
        // GitHub odrzuca zapytania bez nazwy programu.
        client.DefaultRequestHeaders.UserAgent.Add(
            new ProductInfoHeaderValue("AccessibleMediaController", "1.0"));
        client.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        return client;
    }

    private sealed class PendingUpdate
    {
        public string? Version { get; set; }
        public string? Sha256 { get; set; }
        public bool ChecksumVerified { get; set; }
        public string SourceDirectory { get; set; } = "";
        public string TargetDirectory { get; set; } = "";

        /// <summary>
        /// Sciezka do pobranego instalatora. Gdy jest ustawiona, aktualizacje
        /// wykonuje instalator, a nie kopiowanie plikow z SourceDirectory.
        /// </summary>
        public string InstallerPath { get; set; } = "";

        public DateTimeOffset PreparedAtUtc { get; set; }
    }
}
