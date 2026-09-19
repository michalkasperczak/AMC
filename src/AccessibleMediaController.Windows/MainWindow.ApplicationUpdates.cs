using System.IO;
using System.Windows;
using System.Windows.Input;
using AccessibleMediaController.Core.Updates;
using AccessibleMediaController.Windows.Services;

namespace AccessibleMediaController.Windows;

public partial class MainWindow
{
    private ApplicationUpdateWindow? _applicationUpdateWindow;
    private readonly ApplicationUpdateInstallFlow _applicationUpdateInstallFlow = new();
    private readonly CancellationTokenSource _applicationUpdateCancellation = new();

    /// <summary>
    /// Start instalacji. Podmienialny WYLACZNIE instancyjnie, przez test tego
    /// okna: pozwala zmierzyc odmowe i wyjatek startu bez czytania i bez
    /// dotykania produkcyjnego katalogu aktualizacji. W produkcji zawsze null.
    /// </summary>
    private Func<bool, bool>? _applicationUpdateStartOverride = null;

    private void ShowApplicationUpdateDialog()
    {
        if (_isClosing) return;
        if (_applicationUpdateWindow is { } existing)
        {
            existing.Activate();
            return;
        }

        var previousFocus = Keyboard.FocusedElement;
        var dialog = new ApplicationUpdateWindow(ApplicationUpdateManager.InstalledVersion,
            (download, progress, cancellation) => ApplicationUpdateManager.CheckAsync(
                _state.Settings.Updates.Channel,
                download,
                progress,
                cancellation,
                allowInstallOnExit: false))
        { Owner = this };
        _applicationUpdateWindow = dialog;
        try
        {
            dialog.ShowDialog();
            if (!dialog.InstallRequested)
            {
                AnnounceEssential("Zamknięto okno aktualizacji. Instalacja nie została uruchomiona.");
                return;
            }

            AnnounceEssential("Zamykanie AMC przed instalacją aktualizacji. Program uruchomi się ponownie po jej zakończeniu.");
            // Ochronny zapis PRZED nieodwracalnym zamykaniem. Window_Closing
            // zwalnia zasoby i dopiero na końcu zapisuje stan, więc awaria
            // zapisu wychodziłaby na jaw dopiero po zniszczeniu sesji — i
            // jedyną informacją byłby ulotny komunikat mowy w znikającym oknie.
            // Trwała awaria ma zostawić AMC w pełni działające.
            if (!TryPrepareExplicitUpdateStateSave()) return;

            // Zwykłe zamknięcie zachowuje ostrzeżenie o nagraniach i końcowy
            // zapis. Pomocnik jest uruchamiany dopiero w Window_Closing po zapisie.
            if (!_applicationUpdateInstallFlow.RequestClose(() =>
                {
                    Close();
                    return _isClosing;
                }))
                AnnounceEssential("Aktualizacja nie została rozpoczęta. AMC pozostaje otwarte.");
        }
        catch (Exception exception)
        {
            DiagnosticLog.Error("aktualizacja-amc", "Nie udało się zakończyć ręcznej aktualizacji AMC.", exception);
            AnnounceEssential("Nie udało się rozpocząć aktualizacji AMC. Szczegóły są w dzienniku.");
        }
        finally
        {
            _applicationUpdateWindow = null;
            if (!_isClosing)
            {
                if (previousFocus is UIElement element && element.IsVisible && element.IsEnabled)
                    Keyboard.Focus(element);
                else FocusMediaList();
            }
        }
    }

    /// <summary>
    /// Ochronny zapis stanu przed jawną aktualizacją. Uruchamiany, gdy okno
    /// jeszcze żyje, wszystkie zasoby są sprawne i wycofanie nic nie psuje.
    /// Trwała awaria zapisu wstrzymuje aktualizację i mówi o tym w oknie
    /// wymagającym potwierdzenia — nie w ulotnym komunikacie mowy.
    /// NIE jest gwarancją powodzenia końcowego zapisu: ten dalej decyduje
    /// o uruchomieniu instalatora.
    /// </summary>
    private bool TryPrepareExplicitUpdateStateSave()
    {
        Exception? failure;
        try
        {
            if (FlushStateSave(TimeSpan.FromSeconds(15), out failure)) return true;
        }
        catch (Exception exception)
        {
            failure = exception;
        }

        DiagnosticLog.Error(
            "aktualizacja-amc",
            "Ochronny zapis stanu przed jawną aktualizacją nie zakończył się prawidłowo.",
            failure ?? new IOException("Nieznany błąd zapisu stanu przed aktualizacją."));
        AccessibleDialog.Show(
            this,
            "Aktualizacja AMC została wstrzymana, ponieważ nie udało się zapisać stanu programu."
            + Environment.NewLine + Environment.NewLine
            + "Powód: " + (failure?.Message ?? "nieznany błąd zapisu.")
            + Environment.NewLine + Environment.NewLine
            + "AMC pozostaje otwarte. Nie udało się potwierdzić zapisu aktualnego stanu na dysku. "
            + "Instalacja nie została rozpoczęta. Sprawdź miejsce na dysku i uprawnienia do folderu "
            + "ustawień, potem spróbuj ponownie. Szczegóły są w dzienniku programu.",
            "Aktualizacja wstrzymana — nieudany zapis stanu",
            MessageBoxButton.OK,
            MessageBoxImage.Warning);
        return false;
    }

    private void LaunchUpdateAfterStateSave(bool stateSaved)
    {
        var explicitlyRequested = _applicationUpdateInstallFlow.IsRequested;
        try
        {
            var launched = _applicationUpdateInstallFlow.TryLaunchAfterSaving(
                stateSaved,
                _state.Settings.Updates.InstallOnExit,
                requested => _applicationUpdateStartOverride is { } start
                    ? start(requested)
                    // JEDNO wejscie: samo laduje wpis RAZ, sprawdza na nim zgode,
                    // numer wersji, sume SHA-256 i sciezki, i uruchamia dokladnie
                    // ten sprawdzony rekord. Zadnego wstepnego HasPendingUpdate:
                    // dwa osobne odczyty pozwalaly wykonac INNY wpis niz ten,
                    // ktory przeszedl sprawdzenie.
                    : ApplicationUpdateManager.TryStartPendingInstall(
                        relaunch: requested,
                        visible: requested));
            if (!launched && explicitlyRequested)
            {
                DiagnosticLog.Warning(
                    "aktualizacja-amc",
                    stateSaved
                        ? "Nie rozpoczęto żądanej aktualizacji po zamknięciu: brak sprawdzonej paczki albo nieudany start instalatora."
                        : "Nie rozpoczęto żądanej aktualizacji po zamknięciu: nieudany końcowy zapis stanu.");
                AnnounceEssential("Aktualizacja nie została rozpoczęta.");
                // Okno jest już zamykane i zasobów nie da się wznowić, więc
                // ulotny komunikat mowy przepadłby razem z nim. Jawnie
                // zamówiona aktualizacja nie może zniknąć bez śladu:
                // potrzebne jest potwierdzenie, że użytkownik to przeczytał —
                // TAKŻE gdy zapis stanu się udał, a zawiodło samo uruchomienie.
                ConfirmExplicitUpdateAbandoned(stateSaved
                    ? ExplicitUpdateFailure.NoVerifiedPackageOrStart
                    : ExplicitUpdateFailure.StateSaveFailed);
            }
        }
        catch (Exception exception)
        {
            DiagnosticLog.Error("aktualizacja-amc", "Nie uruchomiono instalatora po końcowym zapisie.", exception);
            if (explicitlyRequested)
            {
                AnnounceEssential("Nie udało się uruchomić instalatora aktualizacji AMC.");
                // Wyjątek startu też jest JAWNĄ odmową wykonania żądania i też
                // nie może zniknąć razem z oknem.
                ConfirmExplicitUpdateAbandoned(ExplicitUpdateFailure.StartThrew, exception.Message);
            }
        }
    }

    private enum ExplicitUpdateFailure
    {
        /// <summary>Końcowy zapis stanu zawiódł — instalacji nie zaczęto wcale.</summary>
        StateSaveFailed,

        /// <summary>Zapis był dobry; nie ma sprawdzonej paczki albo start instalatora odmówił.</summary>
        NoVerifiedPackageOrStart,

        /// <summary>Zapis był dobry; uruchomienie instalatora rzuciło wyjątek.</summary>
        StartThrew
    }

    /// <summary>
    /// Ostatni moment, w którym program może cokolwiek powiedzieć: zasoby są
    /// już zwolnione, więc okna NIE wznawiamy i nie cofamy zamykania. Modalny
    /// komunikat jedynie wymusza potwierdzenie, zanim AMC zniknie z ekranu.
    ///
    /// Treść ROZRÓŻNIA powody. Nie twierdzimy, że zapis stanu zawiódł, gdy był
    /// dobry, ani że pobrana paczka nadal czeka na dysku, gdy właśnie jej brak
    /// jest przyczyną.
    /// </summary>
    private void ConfirmExplicitUpdateAbandoned(ExplicitUpdateFailure failure, string? detail = null)
    {
        try
        {
            var (powod, skutek) = failure switch
            {
                ExplicitUpdateFailure.StateSaveFailed => (
                    "nie udało się zapisać stanu programu przy zamykaniu.",
                    "AMC zamknie się teraz bez instalowania aktualizacji, a pobrana paczka pozostaje do "
                    + "ponownego sprawdzenia. Część ostatnich zmian ustawień mogła się nie zapisać."
                    + Environment.NewLine + Environment.NewLine
                    + "Po ponownym uruchomieniu sprawdź miejsce na dysku i uprawnienia do folderu ustawień, "
                    + "potem powtórz aktualizację."),
                ExplicitUpdateFailure.NoVerifiedPackageOrStart => (
                    "nie ma sprawdzonej paczki gotowej do instalacji albo nie udało się uruchomić instalatora.",
                    "Stan programu został zapisany poprawnie. AMC zamknie się teraz bez instalowania "
                    + "aktualizacji."
                    + Environment.NewLine + Environment.NewLine
                    + "Po ponownym uruchomieniu otwórz okno aktualizacji i sprawdź jej stan. "
                    + "Jeśli paczka jest gotowa, możesz ponowić instalację bez ponownego pobierania."),
                _ => (
                    "uruchomienie instalatora zakończyło się błędem."
                    + (detail is { Length: > 0 } ? " Powód: " + detail : string.Empty),
                    "Stan programu został zapisany poprawnie. AMC zamknie się teraz bez instalowania "
                    + "aktualizacji."
                    + Environment.NewLine + Environment.NewLine
                    + "Po ponownym uruchomieniu otwórz okno aktualizacji i powtórz instalację.")
            };

            AccessibleDialog.Show(
                null,
                "Aktualizacja AMC nie zostanie zainstalowana, ponieważ " + powod
                + Environment.NewLine + Environment.NewLine
                + skutek
                + Environment.NewLine + Environment.NewLine
                + "Szczegóły są w dzienniku programu.",
                "Aktualizacja nie zostanie zainstalowana",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
        catch (Exception exception)
        {
            DiagnosticLog.Error(
                "aktualizacja-amc",
                "Nie udało się pokazać komunikatu o porzuconej aktualizacji przy zamykaniu.",
                exception);
        }
    }
}
