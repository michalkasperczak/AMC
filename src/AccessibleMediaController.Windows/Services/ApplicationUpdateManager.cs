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
/// AMC wydajemy jako instalator Inno Setup (a starsze wydania jako archiwum
/// ZIP), wiec obie drogi musza tu zyc. W obu wypadkach pliki programu wymienia
/// sie DOPIERO po zamknieciu AMC - dzialajacego pliku .exe Windows nadpisac nie
/// pozwoli.
///
/// Dwie reguly, ktorych nie wolno tu rozluznic:
/// - <c>ChecksumVerified</c> znaczy "sume POLICZONO z pliku i jest zgodna", a
///   nie "wydawca podal jakas sume w opisie". Obietnica wydawcy nie jest
///   sprawdzeniem, a uzytkownik slyszy z tego pola zdanie o bezpieczenstwie.
/// - Wpis o czekajacej aktualizacji usuwamy WYLACZNIE po udanej instalacji.
///   Usuniety przy samym uruchomieniu pomocnika odbiera mozliwosc powtorzenia,
///   gdy instalacja padnie - a wtedy program zostaje w starej wersji i nie ma
///   po czym wrocic.
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
    /// <param name="allowInstallOnExit">
    /// Prawda dla sprawdzenia W TLE: paczka przygotowana bez udzialu uzytkownika
    /// moze sie zainstalowac przy zamknieciu AMC. Falsz dla sprawdzenia RECZNEGO
    /// z okna aktualizacji - tam uzytkownik sam decyduje, kiedy instalowac, a
    /// cicha instalacja przy nastepnym zamknieciu byla dla niego zaskoczeniem.
    /// </param>
    internal static async Task<ApplicationUpdateStatus> CheckAsync(
        string channel,
        bool downloadAutomatically,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default,
        bool allowInstallOnExit = true)
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
            return await DecideAsync(
                    UpdateRoot,
                    InstalledVersion,
                    plan,
                    downloadAutomatically,
                    allowInstallOnExit,
                    progress,
                    cancellationToken)
                .ConfigureAwait(false);
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

    /// <summary>
    /// Sama decyzja o gotowym planie, z JAWNYM katalogiem roboczym i JAWNA
    /// wersja uruchomiona. Osobno od <see cref="CheckAsync"/>, zeby test mogl ja
    /// zmierzyc we wlasnym katalogu tymczasowym: test siegajacy do produkcyjnego
    /// %LOCALAPPDATA% kasowalby uzytkownikowi czekajaca aktualizacje. Celowo NIE
    /// ma tu zadnego globalnego przelacznika "katalog testowy" - taki przelacznik
    /// dalby sie zostawic wlaczony w wydaniu.
    /// </summary>
    internal static async Task<ApplicationUpdateStatus> DecideAsync(
        string updateRoot,
        string installedVersion,
        ApplicationUpdatePlan plan,
        bool downloadAutomatically,
        bool allowInstallOnExit,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (plan.Decision != ApplicationUpdateDecision.UpdateAvailable)
        {
            DiagnosticLog.Info("aktualizacja-amc", plan.Message);
            return new ApplicationUpdateStatus(plan.Decision, plan.Message, plan.Release?.Tag);
        }

        // Bez sumy kontrolnej NIE POBIERAMY NIC. Wczesniej wystarczylo dopisac
        // zdanie "zgodnosc nie zostala sprawdzona" i program mimo to przygotowywal
        // oraz uruchamial niesprawdzony plik .exe pobrany z sieci - ostrzezenie
        // nie jest zabezpieczeniem. Brak sumy to blad wydania, nie wybor
        // uzytkownika.
        if (!plan.HasChecksum)
        {
            var brak = $"Wydanie AMC {plan.Release!.Tag} nie podaje sumy kontrolnej SHA-256, "
                       + "więc AMC nie może sprawdzić, czy plik jest nienaruszony. "
                       + "Aktualizacja nie zostanie pobrana ani uruchomiona.";
            DiagnosticLog.Warning("aktualizacja-amc", brak);
            return new ApplicationUpdateStatus(
                ApplicationUpdateDecision.NotUnderstood,
                brak,
                plan.Release.Tag);
        }

        // Paczka moze byc JUZ pobrana i sprawdzona. Wtedy nie ma po co pobierac
        // jej drugi raz - liczymy sume z pliku na dysku i oddajemy gotowosc.
        // Wczesniej reczne sprawdzenie przy wylaczonym pobieraniu automatycznym
        // mowilo "dostepna nowsza wersja" o paczce lezacej gotowej obok.
        var gotowa = LoadPending(updateRoot);
        if (gotowa is not null
            && string.Equals(gotowa.Version, plan.Release!.Tag, StringComparison.OrdinalIgnoreCase)
            && gotowa.InstallerPath.Length > 0
            && File.Exists(gotowa.InstallerPath))
        {
            var suma = await ComputeSha256Async(gotowa.InstallerPath, cancellationToken).ConfigureAwait(false);
            if (string.Equals(suma, plan.ExpectedSha256, StringComparison.OrdinalIgnoreCase))
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!gotowa.ChecksumVerified || !string.Equals(gotowa.Sha256, suma, StringComparison.OrdinalIgnoreCase))
                {
                    gotowa.Sha256 = suma;
                    gotowa.ChecksumVerified = true;
                    SavePending(updateRoot, gotowa);
                }
                var gotowyKomunikat = $"AMC {plan.Release.Tag} jest już pobrane i sprawdzone. "
                                      + "Suma kontrolna zgodna.";
                DiagnosticLog.Info("aktualizacja-amc", gotowyKomunikat);
                return new ApplicationUpdateStatus(
                    ApplicationUpdateDecision.UpdateAvailable,
                    gotowyKomunikat,
                    plan.Release.Tag,
                    ChecksumVerified: true,
                    ReadyToInstall: true);
            }

            DiagnosticLog.Warning(
                "aktualizacja-amc",
                $"Przygotowana paczka AMC {gotowa.Version} ma inną sumę SHA-256 niż wydanie. Zostanie pobrana ponownie.");
        }

        if (!downloadAutomatically)
        {
            // NIC nie pobrano, wiec NIC nie sprawdzono. ChecksumVerified musi tu
            // zostac falszem: wcześniej oddawano tu samo "suma jest w opisie", a
            // uzytkownik slyszal z tego, ze plik zostal sprawdzony.
            DiagnosticLog.Info("aktualizacja-amc", plan.Message);
            return new ApplicationUpdateStatus(plan.Decision, plan.Message, plan.Release!.Tag);
        }

        return await PrepareAsync(updateRoot, plan, allowInstallOnExit, progress, cancellationToken)
            .ConfigureAwait(false);
    }

    private static async Task<ApplicationUpdateStatus> PrepareAsync(
        string updateRoot,
        ApplicationUpdatePlan plan,
        bool allowInstallOnExit,
        IProgress<double>? progress,
        CancellationToken cancellationToken)
    {
        var release = plan.Release!;
        Directory.CreateDirectory(updateRoot);
        var stagingRoot = Path.Combine(updateRoot, $".staging-{Guid.NewGuid():N}");
        Directory.CreateDirectory(stagingRoot);

        try
        {
            var archive = Path.Combine(stagingRoot, release.PackageName ?? "amc.zip");
            await DownloadAsync(release.PackageUri!, archive, progress, cancellationToken).ConfigureAwait(false);

            var actual = await ComputeSha256Async(archive, cancellationToken).ConfigureAwait(false);
            if (!string.Equals(actual, plan.ExpectedSha256, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException(
                    "Pobrana paczka AMC ma inną sumę SHA-256 niż podana w opisie wydania. Plik został usunięty.");
            }

            progress?.Report(0.9d);

            // Anulowanie MUSI zatrzymac zapis wpisu. Inaczej przerwane
            // przygotowanie zostawialo paczke oznaczona jako gotowa do
            // instalacji, a uzytkownik nie wiedzial, ze cos czeka.
            cancellationToken.ThrowIfCancellationRequested();

            // Instalator: nie ma czego rozpakowywac ani podmieniac. Plik
            // przenosimy poza katalog tymczasowy i uruchamiamy po zamknieciu AMC.
            if (release.PackageIsInstaller)
            {
                var readyInstaller = Path.Combine(updateRoot, release.PackageName ?? "amc-setup.exe");
                if (File.Exists(readyInstaller)) File.Delete(readyInstaller);
                File.Move(archive, readyInstaller);

                SavePending(updateRoot, new PendingUpdate
                {
                    Version = release.Tag,
                    Sha256 = actual,
                    ChecksumVerified = true,
                    InstallerPath = readyInstaller,
                    TargetDirectory = AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar),
                    ProgramPath = Path.Combine(
                        AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar),
                        "AccessibleMediaController.exe"),
                    AllowInstallOnExit = allowInstallOnExit,
                    PreparedAtUtc = DateTimeOffset.UtcNow
                });

                progress?.Report(1d);
                var installerMessage = $"AMC {release.Tag} jest pobrane i gotowe. Suma kontrolna zgodna. "
                                       + (allowInstallOnExit
                                           ? "Instalator uruchomi się po zamknięciu programu."
                                           : "Instalacja zacznie się, gdy ją potwierdzisz.");
                DiagnosticLog.Info("aktualizacja-amc", $"{installerMessage} SHA-256 {actual}.");
                return new ApplicationUpdateStatus(
                    ApplicationUpdateDecision.UpdateAvailable,
                    installerMessage,
                    release.Tag,
                    ChecksumVerified: true,
                    ReadyToInstall: true);
            }

            var unpacked = Path.Combine(stagingRoot, "rozpakowane");
            Directory.CreateDirectory(unpacked);
            ExtractSafely(archive, unpacked);

            var executable = FindExecutable(unpacked)
                ?? throw new InvalidDataException(
                    "Pobrana paczka nie zawiera pliku AccessibleMediaController.exe, więc nie jest wydaniem AMC.");

            cancellationToken.ThrowIfCancellationRequested();

            // Katalog gotowy do podmiany przenosimy POZA katalog tymczasowy,
            // zeby przetrwal zamkniecie programu.
            var readyRoot = Path.Combine(updateRoot, "gotowe");
            if (Directory.Exists(readyRoot)) Directory.Delete(readyRoot, recursive: true);
            Directory.Move(Path.GetDirectoryName(executable)!, readyRoot);

            SavePending(updateRoot, new PendingUpdate
            {
                Version = release.Tag,
                Sha256 = actual,
                ChecksumVerified = true,
                SourceDirectory = readyRoot,
                TargetDirectory = AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar),
                ProgramPath = Path.Combine(
                    AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar),
                    "AccessibleMediaController.exe"),
                AllowInstallOnExit = allowInstallOnExit,
                PreparedAtUtc = DateTimeOffset.UtcNow
            });

            progress?.Report(1d);
            var message = $"AMC {release.Tag} jest pobrane i gotowe. Suma kontrolna zgodna. "
                          + (allowInstallOnExit
                              ? "Zostanie zainstalowane po zamknięciu programu."
                              : "Instalacja zacznie się, gdy ją potwierdzisz.");
            DiagnosticLog.Info("aktualizacja-amc", $"{message} SHA-256 {actual}.");
            return new ApplicationUpdateStatus(
                ApplicationUpdateDecision.UpdateAvailable,
                message,
                release.Tag,
                ChecksumVerified: true,
                ReadyToInstall: true);
        }
        finally
        {
            TryDeleteDirectory(stagingRoot);
        }
    }

    /// <summary>
    /// Czy jest pobrana, SPRAWDZONA paczka czekajaca na instalacje.
    ///
    /// Sprawdzamy trzy rzeczy, ktore wczesniej nie byly sprawdzane wcale:
    /// numer wersji (stary wpis cofnalby AMC do wczesniejszej wersji), obecnosc
    /// pliku instalatora i jego sume SHA-256. Wpis w JSON nie jest dowodem na
    /// zawartosc pliku - plik mogl sie zmienic albo zostac podmieniony po
    /// pobraniu.
    /// </summary>
    /// <param name="includeManualRequests">
    /// Falsz przy zamykaniu programu: paczka przygotowana na WYRAZNE zyczenie
    /// uzytkownika w oknie aktualizacji nie moze sie zainstalowac sama, bo o
    /// momencie decyduje wtedy uzytkownik. Prawda w oknie aktualizacji, ktore
    /// ma widziec wszystko, co czeka.
    /// </param>
    internal static bool HasPendingUpdate(out string? version, bool includeManualRequests = true) =>
        HasPendingUpdate(UpdateRoot, InstalledVersion, out version, includeManualRequests);

    /// <summary>
    /// To samo z JAWNYM katalogiem i JAWNA wersja uruchomiona - do pomiaru
    /// testem bez dotykania produkcyjnego %LOCALAPPDATA%.
    /// </summary>
    internal static bool HasPendingUpdate(
        string updateRoot,
        string installedVersion,
        out string? version,
        bool includeManualRequests = true)
    {
        version = null;
        if (!TryResolveVerifiedPending(
                updateRoot, installedVersion, includeManualRequests, out var pending, out _, out _))
        {
            return false;
        }

        version = pending!.Version;
        return true;
    }

    /// <summary>
    /// JEDNA wspolna walidacja czekajacego wpisu: zgoda, numer wersji, suma
    /// SHA-256 policzona Z DYSKU i sciezki w zaufanym katalogu aktualizacji.
    ///
    /// Zarowno pytanie "czy cos czeka" (HasPendingUpdate), jak i samo
    /// uruchomienie instalacji ida TA SAMA droga i na TYM SAMYM, raz wczytanym
    /// obiekcie. Wczesniej byly to dwie rozjezdzajace sie kopie: pytanie
    /// sprawdzalo wszystko, a uruchomienie wczytywalo plik DRUGI RAZ i
    /// sprawdzalo wylacznie sciezki - wiec wpis podmieniony miedzy jednym a
    /// drugim odczytem mogl wykonac paczke bez zgody, bez weryfikacji sumy albo
    /// STARSZA od uruchomionej wersji.
    /// </summary>
    private static bool TryResolveVerifiedPending(
        string updateRoot,
        string installedVersion,
        bool includeManualRequests,
        out PendingUpdate? pending,
        out string installerPath,
        out string sourceDirectory)
    {
        pending = null;
        installerPath = "";
        sourceDirectory = "";

        var wpis = LoadPending(updateRoot);
        if (wpis is null || !wpis.ChecksumVerified) return false;
        if (!includeManualRequests && !wpis.AllowInstallOnExit) return false;

        // Wersja nie nowsza niz uruchomiona to nie aktualizacja, a cofniecie.
        // Taki wpis zostaje po wczesniejszej, juz wykonanej instalacji.
        if (!ApplicationVersion.TryParse(wpis.Version, out var czekajaca)) return false;
        if (!ApplicationVersion.TryParse(installedVersion, out var zainstalowana)) return false;
        if (czekajaca <= zainstalowana) return false;

        // Sciezka musi lezec w zaufanym katalogu aktualizacji, a nie
        // gdziekolwiek na dysku: uszkodzony albo zmieniony wpis nie ma
        // wyprowadzac AMC poza to, co samo pobralo.
        if (!TryResolvePendingPaths(wpis, updateRoot, out installerPath, out sourceDirectory)) return false;

        if (installerPath.Length > 0)
        {
            // Suma z wpisu nie dowodzi niczego o pliku. Liczymy ja z dysku.
            if (wpis.Sha256 is not { Length: 64 }) return false;
            try
            {
                if (!string.Equals(
                        ComputeSha256(installerPath),
                        wpis.Sha256,
                        StringComparison.OrdinalIgnoreCase))
                {
                    DiagnosticLog.Warning(
                        "aktualizacja-amc",
                        "Czekający instalator AMC ma inną sumę SHA-256 niż zapisana przy pobraniu. Pomijam go.");
                    return false;
                }
            }
            catch (Exception exception) when (exception is IOException
                or UnauthorizedAccessException)
            {
                DiagnosticLog.Warning(
                    "aktualizacja-amc",
                    $"Nie udało się policzyć sumy czekającego instalatora AMC: {exception.Message}");
                installerPath = "";
                return false;
            }
        }

        pending = wpis;
        return true;
    }

    /// <summary>
    /// Niezmienny snapshot wykonania: dokladnie te dane, na ktorych przeszla
    /// walidacja. Po jego zlozeniu plik pending.json nie jest juz czytany, wiec
    /// podmiana wpisu w trakcie uruchamiania nie zmienia ani wersji, ani sumy,
    /// ani sciezki instalatora. ProgramPath i InstallDirectory pochodza z
    /// BIEZACEJ instalacji, nie z wpisu.
    /// </summary>
    internal sealed record PendingInstallPlan(
        string Version,
        string Sha256,
        string InstallerPath,
        string SourceDirectory,
        bool AllowInstallOnExit,
        string ProgramPath,
        string InstallDirectory);

    /// <summary>
    /// Uruchamia podmiane plikow. Wolane przy zamykaniu programu.
    ///
    /// Podmiana idzie przez osobny proces PowerShell, ktory czeka na zniknięcie
    /// AMC z listy procesow, kopiuje pliki i uruchamia nowa wersje. Wlasny proces
    /// nie moze nadpisac swojego .exe, dopoki dziala.
    /// </summary>
    /// <param name="visible">
    /// Prawda, gdy uzytkownik SAM wybral "zainstaluj teraz". Wtedy instalator ma
    /// pokazac swoje okno na pierwszym planie - tak jak w EdSharpie - bo po
    /// kliknieciu "Tak" cisza i ukryte okno sa nieodroznialne od zawieszenia.
    /// Falsz to instalacja odlozona na zamkniecie programu: tam okno nie ma
    /// komu sie pokazac, wiec idzie trybem cichym.
    /// </param>
    internal static bool TryStartPendingInstall(bool relaunch, bool visible = false) =>
        TryStartPendingInstall(
            UpdateRoot,
            InstalledVersion,
            allowManualOnlyPackage: relaunch,
            relaunch: relaunch,
            visible: visible,
            start: null);

    /// <summary>
    /// To samo z JAWNYM katalogiem, JAWNA wersja uruchomiona i podmienialnym
    /// startem procesu - do pomiaru testem bez dotykania produkcyjnego
    /// %LOCALAPPDATA% i bez uruchamiania czegokolwiek naprawde.
    /// </summary>
    /// <param name="allowManualOnlyPackage">
    /// Prawda TYLKO dla jawnego zadania uzytkownika. Falsz to instalacja
    /// automatyczna przy zamykaniu: tam paczka z AllowInstallOnExit = false nie
    /// ma prawa ruszyc, bo o jej momencie decyduje uzytkownik.
    /// </param>
    internal static bool TryStartPendingInstall(
        string updateRoot,
        string installedVersion,
        bool allowManualOnlyPackage,
        bool relaunch,
        bool visible,
        Func<PendingInstallPlan, ProcessStartInfo, bool>? start)
    {
        try
        {
            // JEDEN odczyt, JEDNA walidacja, JEDEN obiekt. Wczesniej wolajacy
            // sprawdzal wpis A przez HasPendingUpdate, a uruchamiany byl wpis B
            // z drugiego odczytu - i B sprawdzano wylacznie pod katem sciezek.
            if (!TryResolveVerifiedPending(
                    updateRoot,
                    installedVersion,
                    includeManualRequests: allowManualOnlyPackage,
                    out var pending,
                    out var instalator,
                    out var zrodlo))
            {
                DiagnosticLog.Warning(
                    "aktualizacja-amc",
                    "Czekająca aktualizacja AMC nie przeszła sprawdzenia (zgoda, wersja, suma albo ścieżka). "
                    + "Nie uruchamiam instalacji.");
                return false;
            }

            // Od tej chwili pending.json nie jest juz czytany: wszystko, co
            // trafia do pomocnika, pochodzi z tego niezmiennego snapshotu.
            var plan = new PendingInstallPlan(
                pending!.Version ?? "",
                pending.Sha256 ?? "",
                instalator,
                zrodlo,
                pending.AllowInstallOnExit,
                CurrentProgramPath,
                CurrentInstallDirectory);

            return StartFromPlan(plan, updateRoot, relaunch, visible, start);
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

    private static bool StartFromPlan(
        PendingInstallPlan plan,
        string updateRoot,
        bool relaunch,
        bool visible,
        Func<PendingInstallPlan, ProcessStartInfo, bool>? start)
    {
        {
            // Droga instalatora. Instalatora NIE uruchamiamy stad wprost:
            // robi to pomocnik, ktory najpierw czeka na zniknięcie procesu AMC,
            // potem SPRAWDZA sume pliku ponownie, uruchamia instalator i - gdy
            // ten zakonczy sie sukcesem - sam startuje program w tej samej sesji.
            if (plan.InstallerPath.Length > 0)
            {
                var arguments = new List<string>();
                if (visible)
                {
                    // Instalator widoczny: pasek postepu i okno na wierzchu, bez
                    // pytan o katalog i skroty (te ustalono przy pierwszej
                    // instalacji). /SILENT, nie /VERYSILENT - /VERYSILENT ukrywa
                    // TAKZE pasek postepu, a wtedy nie ma czego pokazac.
                    arguments.Add("/SILENT");
                }
                else
                {
                    arguments.Add("/VERYSILENT");
                }
                arguments.Add("/SUPPRESSMSGBOXES");
                // /NORESTART: aktualizacja odtwarzacza NIE MA PRAWA restartowac
                // Windows. Przy czytniku ekranu niespodziewany restart systemu to
                // utrata calej pracy uzytkownika, nie niedogodnosc.
                arguments.Add("/NORESTART");
                // /CLOSEAPPLICATIONS pozwala instalatorowi domknac AMC, gdyby
                // proces wbrew oczekiwaniu jeszcze zyl.
                //
                // /RESTARTAPPLICATIONS CELOWO NIE MA. Restart Manager potrafi
                // wrocic tylko do programu, ktory JESZCZE ZYL, gdy instalator
                // startowal - a my startujemy go dopiero PO zniknięciu AMC.
                // Sekcja [Run] w .iss jest dodatkowo oznaczona skipifsilent,
                // wiec w trybie cichym tez nie uruchomi programu. Ponowne
                // uruchomienie robi wiec pomocnik, jawnie i sprawdzalnie.
                arguments.Add("/CLOSEAPPLICATIONS");
                arguments.Add("/NORESTARTAPPLICATIONS");
                arguments.Add($"/LOG={Path.Combine(updateRoot, "instalator.log")}");

                // Droga powrotu bierze sie z BIEZACEJ instalacji, nie z pol
                // ProgramPath / TargetDirectory we wpisie: po instalacji ma
                // wstac ten sam program, ktory teraz dziala. Wpis lezy w
                // katalogu konta uzytkownika i moze byc zmieniony, a uruchomienie
                // dowolnego pliku "bo tak stalo w JSON" jest niepotrzebne.

                // Wpisu o czekajacej aktualizacji NIE USUWAMY tutaj. Usuwa go
                // pomocnik i tylko po UDANEJ instalacji: gdy instalator padnie
                // albo nie doczeka sie zamkniecia AMC, aktualizacja musi dac sie
                // powtorzyc. Wczesniej wpis ginal natychmiast po odpaleniu
                // pomocnika, wiec kazdy blad instalacji zabieral droge powrotu.
                var script = Path.Combine(updateRoot, "uruchom-instalator.ps1");
                if (start is null) File.WriteAllText(script, BuildInstallerLauncherScript(), new UTF8Encoding(true));

                var launcher = BuildLauncherStartInfo(
                    script,
                    Environment.ProcessId,
                    plan.InstallerPath,
                    // Przelaczniki ida jako jeden ciag: PowerShell rozdzieli je sam,
                    // a my nie tracimy cudzyslowow ze sciezki dziennika ze spacjami.
                    string.Join(" ", arguments.Select(QuoteIfNeeded)),
                    plan.Sha256,
                    Path.Combine(updateRoot, "pending.json"),
                    plan.Version,
                    plan.ProgramPath,
                    relaunch);

                if (start is not null)
                {
                    if (!start(plan, launcher)) return false;
                }
                else
                {
                    Process.Start(launcher);
                }

                DiagnosticLog.Info(
                    "aktualizacja-amc",
                    $"Zlecono instalację AMC {plan.Version} po zamknięciu programu "
                    + $"(ponowne uruchomienie: {(relaunch ? "tak" : "nie")}).");
                return true;
            }

            if (plan.SourceDirectory.Length == 0) return false;

            // Cel kopiowania to KATALOG BIEZACEJ INSTALACJI, nie TargetDirectory
            // z wpisu. Zmieniony wpis nie ma kierowac kopiowania w dowolne
            // miejsce na dysku.
            var cel = plan.InstallDirectory;
            if (!Directory.Exists(cel)) return false;

            var copyScript = Path.Combine(updateRoot, "instaluj.ps1");
            if (start is null) File.WriteAllText(copyScript, BuildInstallScript(), new UTF8Encoding(true));

            var startInfo = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                UseShellExecute = false,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden
            };
            startInfo.ArgumentList.Add("-NoProfile");
            startInfo.ArgumentList.Add("-ExecutionPolicy");
            startInfo.ArgumentList.Add("Bypass");
            startInfo.ArgumentList.Add("-WindowStyle");
            startInfo.ArgumentList.Add("Hidden");
            startInfo.ArgumentList.Add("-File");
            startInfo.ArgumentList.Add(copyScript);
            startInfo.ArgumentList.Add("-ProcessId");
            startInfo.ArgumentList.Add(Environment.ProcessId.ToString());
            startInfo.ArgumentList.Add("-Source");
            startInfo.ArgumentList.Add(plan.SourceDirectory);
            startInfo.ArgumentList.Add("-Target");
            startInfo.ArgumentList.Add(cel);
            startInfo.ArgumentList.Add("-Version");
            startInfo.ArgumentList.Add(plan.Version);
            if (relaunch) startInfo.ArgumentList.Add("-Relaunch");

            if (start is not null)
            {
                if (!start(plan, startInfo)) return false;
            }
            else
            {
                Process.Start(startInfo);
            }

            DiagnosticLog.Info(
                "aktualizacja-amc",
                $"Rozpoczęto instalację AMC {plan.Version} po zamknięciu programu.");
            return true;
        }
    }

    /// <summary>Usuwa przygotowana paczke - np. gdy uzytkownik wylaczy aktualizacje.</summary>
    internal static void DiscardPending()
    {
        try
        {
            var pending = LoadPending(UpdateRoot);
            // Kasujemy TYLKO wewnatrz katalogu aktualizacji. Wpis wskazujacy
            // katalog gdzie indziej nie ma prawa niczego usunac.
            if (pending is not null
                && pending.SourceDirectory.Length > 0
                && TryResolveInsideUpdateRoot(
                    pending.SourceDirectory, UpdateRoot, "katalog rozpakowanej paczki", out var zrodlo))
            {
                TryDeleteDirectory(zrodlo);
            }

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
    /// Wiersz wywolania pomocnika. Wydzielony, zeby test mogl uruchomic ten sam
    /// skrypt z wlasnym procesem, wlasnym "instalatorem" i wlasnym wpisem, bez
    /// dotykania czegokolwiek w instalacji AMC.
    /// </summary>
    internal static ProcessStartInfo BuildLauncherStartInfo(
        string scriptPath,
        int processId,
        string installerPath,
        string arguments,
        string expectedSha256,
        string pendingPath,
        string version,
        string programPath,
        bool relaunch,
        int waitSeconds = 120)
    {
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
        start.ArgumentList.Add(scriptPath);
        start.ArgumentList.Add("-ProcessId");
        start.ArgumentList.Add(processId.ToString(System.Globalization.CultureInfo.InvariantCulture));
        start.ArgumentList.Add("-Installer");
        start.ArgumentList.Add(installerPath);
        start.ArgumentList.Add("-Arguments");
        start.ArgumentList.Add(arguments);
        start.ArgumentList.Add("-ExpectedSha256");
        start.ArgumentList.Add(expectedSha256);
        start.ArgumentList.Add("-PendingPath");
        start.ArgumentList.Add(pendingPath);
        start.ArgumentList.Add("-Version");
        start.ArgumentList.Add(version);
        start.ArgumentList.Add("-ProgramPath");
        start.ArgumentList.Add(programPath);
        start.ArgumentList.Add("-WaitSeconds");
        start.ArgumentList.Add(waitSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture));
        if (relaunch) start.ArgumentList.Add("-Relaunch");
        return start;
    }

    private static string QuoteIfNeeded(string argument) =>
        argument.Contains(' ', StringComparison.Ordinal) && !argument.StartsWith('"')
            ? $"\"{argument}\""
            : argument;

    /// <summary>
    /// Pomocnik: czekanie na zamkniecie AMC, ponowne sprawdzenie sumy, instalacja,
    /// ponowne uruchomienie programu, dopiero na koniec usuniecie wpisu.
    ///
    /// Kazdy krok pisze do osobnego dziennika, bo cicha instalacja nie zostawia
    /// sladu w interfejsie i bez tego nie da sie powiedziec, na czym stanela.
    /// </summary>
    internal static string BuildInstallerLauncherScript() => """
        param(
            [Parameter(Mandatory=$true)][int]$ProcessId,
            [Parameter(Mandatory=$true)][string]$Installer,
            [string]$Arguments = '',
            [string]$ExpectedSha256 = '',
            [string]$PendingPath = '',
            [string]$Version = '',
            [string]$ProgramPath = '',
            [int]$WaitSeconds = 120,
            [switch]$Relaunch
        )

        $ErrorActionPreference = 'Stop'
        $katalog = if ($PendingPath) { Split-Path -Parent $PendingPath }
                   else { Join-Path $env:LOCALAPPDATA 'AccessibleMediaController\updates' }
        if (-not (Test-Path -LiteralPath $katalog)) {
            New-Item -ItemType Directory -Force -Path $katalog | Out-Null
        }
        $dziennik = Join-Path $katalog 'uruchomienie-instalatora.log'

        function Zapisz($tekst) {
            $wiersz = "{0} {1}" -f (Get-Date -Format 'yyyy-MM-dd HH:mm:ss'), $tekst
            Add-Content -LiteralPath $dziennik -Value $wiersz -Encoding UTF8
        }

        try {
            # Czekamy, az AMC samo zniknie. NIE zabijamy procesu: uzytkownik moze
            # miec otwarte okno zapisu albo trwajace nagranie, a zabity program
            # nie zapisze ani jednego, ani drugiego.
            Zapisz "Czekam na zamkniecie AMC (proces $ProcessId), najwyzej $WaitSeconds s."
            $koniec = (Get-Date).AddSeconds($WaitSeconds)
            while ((Get-Date) -lt $koniec) {
                if (-not (Get-Process -Id $ProcessId -ErrorAction SilentlyContinue)) { break }
                Start-Sleep -Milliseconds 300
            }
            if (Get-Process -Id $ProcessId -ErrorAction SilentlyContinue) {
                Zapisz "AMC nadal dziala po $WaitSeconds s. NIE uruchamiam instalatora, wpis zostaje."
                exit 2
            }

            # Uchwyt jednej kopii ginie razem z procesem, ale system potrzebuje
            # chwili na zwolnienie plikow programu.
            Start-Sleep -Milliseconds 1500

            if (-not (Test-Path -LiteralPath $Installer)) {
                Zapisz "Brak pliku instalatora: $Installer. Wpis zostaje."
                exit 3
            }

            # Sume sprawdzamy PONOWNIE, tuz przed uruchomieniem. Miedzy pobraniem
            # a ta chwila minely minuty albo dni, a plik lezy w katalogu
            # zapisywalnym dla uzytkownika - pobranie go kiedys nie dowodzi, ze
            # to nadal ten sam plik.
            if ($ExpectedSha256) {
                $policzona = (Get-FileHash -LiteralPath $Installer -Algorithm SHA256).Hash
                if ($policzona -ne $ExpectedSha256.ToUpperInvariant()) {
                    Zapisz "Suma SHA-256 instalatora nie zgadza sie (jest $policzona, oczekiwano $ExpectedSha256). NIE uruchamiam."
                    exit 4
                }
                Zapisz 'Suma SHA-256 instalatora zgodna.'
            }
            else {
                Zapisz 'Brak oczekiwanej sumy SHA-256. NIE uruchamiam niesprawdzonego pliku.'
                exit 4
            }

            Zapisz "Uruchamiam instalator: $Installer $Arguments"
            if ($Arguments.Trim().Length -gt 0) {
                $proces = Start-Process -FilePath $Installer -ArgumentList $Arguments -PassThru -Wait
            }
            else {
                $proces = Start-Process -FilePath $Installer -PassThru -Wait
            }
            $kod = $proces.ExitCode
            Zapisz ("Instalator zakonczyl sie kodem {0}." -f $kod)
            if ($kod -ne 0) {
                Zapisz 'Instalacja nieudana: nie uruchamiam programu i zostawiam wpis do ponowienia.'
                exit $kod
            }

            # Program uruchamiamy SAMI i w tej samej sesji interaktywnej, w ktorej
            # dzialalo AMC. Na /RESTARTAPPLICATIONS nie ma co liczyc: Restart
            # Manager wraca tylko do programu, ktory zyl w chwili startu
            # instalatora, a sekcja [Run] w .iss ma skipifsilent.
            if ($Relaunch) {
                if ($ProgramPath -and (Test-Path -LiteralPath $ProgramPath)) {
                    Zapisz "Uruchamiam AMC po instalacji: $ProgramPath"
                    Start-Process -FilePath $ProgramPath -WorkingDirectory (Split-Path -Parent $ProgramPath) | Out-Null
                }
                else {
                    Zapisz "Nie znalazlem programu do uruchomienia: $ProgramPath"
                }
            }

            # Wpis kasujemy DOPIERO TERAZ i tylko wtedy, gdy dotyczy wersji,
            # ktora wlasnie zainstalowalismy. Cudzy, nowszy wpis zapisany w
            # miedzyczasie nie jest nasz.
            if ($PendingPath -and (Test-Path -LiteralPath $PendingPath)) {
                $usun = $true
                if ($Version) {
                    try {
                        $wpis = Get-Content -LiteralPath $PendingPath -Raw | ConvertFrom-Json
                        if ($wpis.Version -and $wpis.Version -ne $Version) {
                            $usun = $false
                            Zapisz ("Wpis dotyczy innej wersji ({0}), zostawiam go." -f $wpis.Version)
                        }
                    }
                    catch {
                        Zapisz 'Nie udalo sie odczytac wpisu o aktualizacji, zostawiam go nietkniety.'
                        $usun = $false
                    }
                }
                if ($usun) {
                    Remove-Item -LiteralPath $PendingPath -Force -ErrorAction SilentlyContinue
                    Zapisz 'Usunalem wpis o czekajacej aktualizacji.'
                }
            }

            Zapisz "Zainstalowano AMC $Version."
            exit 0
        }
        catch {
            Zapisz ("Blad uruchamiania instalatora: {0}" -f $_.Exception.Message)
            exit 1
        }
        """;

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

    /// <summary>
    /// Sprowadza sciezke z wpisu pending.json do postaci pelnej i sprawdza, czy
    /// lezy WEWNATRZ katalogu aktualizacji.
    ///
    /// Po co: wpis jest zwyklym plikiem konta uzytkownika i moze byc uszkodzony
    /// albo zmieniony. To NIE jest obrona przed programem dzialajacym na tym
    /// samym koncie - taki program i tak zrobi wszystko, co moze uzytkownik, a
    /// suma SHA sprawdza calosc pliku, nie jego pochodzenie. Chodzi o
    /// OGRANICZENIE SKUTKOW zlego wpisu: AMC ma uruchamiac i kopiowac tylko to,
    /// co samo pobralo do swojego katalogu, a nie dowolny plik ze sciezki
    /// wpisanej w JSON.
    ///
    /// Path.GetFullPath sam skleja '..', wiec 'updates\..\obcy\x.exe' wychodzi
    /// poza katalog i wypada. Doklejony separator w IsWithin pilnuje, by katalog
    /// 'updates-evil' nie uchodzil za wnetrze 'updates'.
    /// </summary>
    private static bool TryResolveInsideUpdateRoot(
        string? candidate,
        string updateRoot,
        string opis,
        out string resolved)
    {
        resolved = "";
        if (string.IsNullOrWhiteSpace(candidate))
        {
            return false;
        }

        string pelna;
        string pelnyRoot;
        try
        {
            // Sciezka wzgledna nie ma tu sensu: wpis powstaje z pelnych sciezek,
            // a wzgledna zalezy od katalogu biezacego procesu.
            if (!Path.IsPathFullyQualified(candidate))
            {
                DiagnosticLog.Warning(
                    "aktualizacja-amc",
                    $"Wpis o czekającej aktualizacji podaje względną ścieżkę ({opis}). Pomijam ją.");
                return false;
            }

            pelna = Path.GetFullPath(candidate);
            pelnyRoot = Path.GetFullPath(updateRoot);
        }
        catch (Exception exception) when (exception is ArgumentException
            or NotSupportedException
            or PathTooLongException
            or IOException
            or UnauthorizedAccessException
            or System.Security.SecurityException)
        {
            DiagnosticLog.Warning(
                "aktualizacja-amc",
                $"Wpisu o czekającej aktualizacji nie da się sprowadzić do poprawnej ścieżki "
                + $"({opis}): {exception.Message}");
            return false;
        }

        if (!IsWithin(pelna, pelnyRoot))
        {
            DiagnosticLog.Warning(
                "aktualizacja-amc",
                $"Wpis o czekającej aktualizacji wskazuje miejsce poza katalogiem aktualizacji AMC "
                + $"({opis}). Pomijam go.");
            return false;
        }

        resolved = pelna;
        return true;
    }

    /// <summary>
    /// Sprawdza sciezki JEDNEGO, juz wczytanego wpisu i zwraca postac, ktorej
    /// wolno uzyc. Celowo bierze obiekt, a nie katalog: walidacja i wykonanie
    /// musza dotyczyc TEGO SAMEGO odczytu, inaczej miedzy jednym a drugim
    /// wczytaniem plik moze sie zmienic.
    /// </summary>
    private static bool TryResolvePendingPaths(
        PendingUpdate pending,
        string updateRoot,
        out string installerPath,
        out string sourceDirectory)
    {
        installerPath = "";
        sourceDirectory = "";

        if (pending.InstallerPath.Length > 0)
        {
            if (!TryResolveInsideUpdateRoot(
                    pending.InstallerPath, updateRoot, "plik instalatora", out var instalator))
            {
                return false;
            }

            if (!File.Exists(instalator)) return false;
            installerPath = instalator;
            return true;
        }

        if (!TryResolveInsideUpdateRoot(
                pending.SourceDirectory, updateRoot, "katalog rozpakowanej paczki", out var zrodlo))
        {
            return false;
        }

        if (!Directory.Exists(zrodlo)) return false;
        sourceDirectory = zrodlo;
        return true;
    }

    /// <summary>
    /// Katalog biezacej instalacji AMC i plik programu w nim. Droga powrotu po
    /// instalacji bierze sie STAD, a nie z modyfikowalnych pol ProgramPath i
    /// TargetDirectory we wpisie: uruchamiamy ten sam program, ktory teraz
    /// dziala, nie ten, ktory ktos wpisal do JSON.
    /// </summary>
    private static string CurrentInstallDirectory =>
        AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar);

    private static string CurrentProgramPath =>
        Path.Combine(CurrentInstallDirectory, "AccessibleMediaController.exe");

    private static PendingUpdate? LoadPending(string updateRoot)
    {
        var path = Path.Combine(updateRoot, "pending.json");
        if (!File.Exists(path)) return null;
        try
        {
            var pending = JsonSerializer.Deserialize<PendingUpdate>(File.ReadAllText(path));
            if (pending is not null && (pending.InstallerPath is null || pending.SourceDirectory is null))
            {
                DiagnosticLog.Warning("aktualizacja-amc", "Wpis aktualizacji zawiera null zamiast ścieżki. Pomijam go.");
                return null;
            }
            return pending;
        }
        catch (JsonException exception)
        {
            DiagnosticLog.Warning(
                "aktualizacja-amc",
                $"Wpisu o czekającej aktualizacji nie da się odczytać: {exception.Message}");
            return null;
        }
    }

    /// <summary>
    /// Zapis wpisu do JAWNEGO katalogu. Publiczne tylko dla testu, ktory musi
    /// przygotowac stan bez dotykania produkcyjnego %LOCALAPPDATA%.
    /// </summary>
    internal static void SavePending(string updateRoot, PendingUpdate pending)
    {
        Directory.CreateDirectory(updateRoot);
        File.WriteAllText(
            Path.Combine(updateRoot, "pending.json"),
            JsonSerializer.Serialize(pending, new JsonSerializerOptions { WriteIndented = true }));
    }

    private static string ComputeSha256(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
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

    internal sealed class PendingUpdate
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

        /// <summary>
        /// Plik programu do uruchomienia PO instalacji. Zapisany jawnie, bo
        /// pomocnik dziala juz po zniknięciu AMC i nie ma skad go wywnioskowac.
        /// </summary>
        public string ProgramPath { get; set; } = "";

        /// <summary>
        /// Czy ta paczka moze sie zainstalowac sama przy zamknieciu AMC. Falsz
        /// dla paczki przygotowanej na wyrazne zyczenie uzytkownika w oknie
        /// aktualizacji: tam o momencie decyduje on, a nie zamkniecie okna.
        /// </summary>
        public bool AllowInstallOnExit { get; set; } = true;

        public DateTimeOffset PreparedAtUtc { get; set; }
    }
}
