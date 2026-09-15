using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Automation;
using AccessibleMediaController.Core.Configuration;
using Microsoft.Win32;

namespace AccessibleMediaController.Windows;

public enum ItemPlaybackOptionsTarget
{
    LocalItem,
    LocalFolder,
    Podcast,
    PodcastEpisode,

    RadioStation,

    /// <summary>
    /// Ustawienia dla CALEJ sesji (calego TIDAL-a, calego radia, wszystkich
    /// plikow lokalnych). Poziom miedzy folderem a ustawieniem ogolnym.
    /// </summary>
    Session
}

public partial class ItemPlaybackOptionsWindow : Window
{
    private static readonly double[] PlaybackRates =
        [0.50d, 0.75d, 1.00d, 1.25d, 1.50d, 1.75d, 2.00d];

    public ItemPlaybackOptionsWindow(
        string itemTitle,
        ResumePositionMode resumePositionMode,
        double? playbackRateOverride,
        bool? loudnessNormalizationOverride,
        bool? smoothTrackTransitionsOverride,
        int? interTrackSilenceMillisecondsOverride,
        ItemPlaybackOptionsTarget target = ItemPlaybackOptionsTarget.LocalItem,
        int podcastRefreshIntervalMinutes = 0,
        string? podcastDownloadFolder = null,
        string? radioBackupStreamUrl = null,
        string? radioRecordingFolder = null)
    {
        InitializeComponent();
        var folderTarget = target == ItemPlaybackOptionsTarget.LocalFolder;
        if (folderTarget)
        {
            Title = "Opcje odtwarzania folderu";
            ResumeModeLabel.Content = "_Pozycja odtwarzania plików:";
            PlaybackRateLabel.Content = "_Prędkość plików w folderze:";
            LoudnessNormalizationLabel.Content = "_Normalizacja głośności plików w folderze:";
            SmoothTransitionsLabel.Content = "_Łagodne przejścia plików w folderze:";
            InterTrackSilenceLabel.Content = "_Cisza po plikach w folderze:";
            OutputDeviceLabel.Content = "_Urządzenie audio dla folderu:";
            AutomationProperties.SetName(ResumeModeBox, "Pozycja odtwarzania plików");
            AutomationProperties.SetName(PlaybackRateBox, "Prędkość plików w folderze");
            AutomationProperties.SetName(LoudnessNormalizationBox, "Normalizacja głośności plików w folderze");
            AutomationProperties.SetName(SmoothTransitionsBox, "Łagodne przejścia plików w folderze");
            AutomationProperties.SetName(InterTrackSilenceBox, "Cisza po plikach w folderze");
            AutomationProperties.SetName(OutputDeviceBox, "Urządzenie audio dla folderu");
        }
        else if (target == ItemPlaybackOptionsTarget.Session)
        {
            Title = "Opcje odtwarzania sesji";
            ResumeModeLabel.Content = "_Pozycja odtwarzania w tej sesji:";
            PlaybackRateLabel.Content = "_Prędkość w tej sesji:";
            LoudnessNormalizationLabel.Content = "_Normalizacja głośności w tej sesji:";
            SmoothTransitionsLabel.Content = "_Łagodne przejścia w tej sesji:";
            InterTrackSilenceLabel.Content = "_Cisza między nagraniami w tej sesji:";
            OutputDeviceLabel.Content = "_Urządzenie audio dla tej sesji:";
            AutomationProperties.SetName(ResumeModeBox, "Pozycja odtwarzania w tej sesji");
            AutomationProperties.SetName(PlaybackRateBox, "Prędkość w tej sesji");
            AutomationProperties.SetName(LoudnessNormalizationBox, "Normalizacja głośności w tej sesji");
            AutomationProperties.SetName(SmoothTransitionsBox, "Łagodne przejścia w tej sesji");
            AutomationProperties.SetName(InterTrackSilenceBox, "Cisza między nagraniami w tej sesji");
            AutomationProperties.SetName(OutputDeviceBox, "Urządzenie audio dla tej sesji");
            AutomationProperties.SetHelpText(
                ResumeModeBox,
                "Ustawienie obejmuje całą sesję. Pojedynczy plik i folder mogą je nadpisać.");
            AutomationProperties.SetHelpText(
                LoudnessNormalizationBox,
                "Obejmuje całą sesję. Może dziedziczyć ustawienie globalne albo zostać "
                + "włączona lub wyłączona dla tej sesji. Plik i folder to nadpisują.");
            AutomationProperties.SetHelpText(
                SmoothTransitionsBox,
                "Obejmuje całą sesję. Może dziedziczyć ustawienie globalne albo zostać "
                + "włączone lub wyłączone dla tej sesji. Plik i folder to nadpisują.");
            AutomationProperties.SetHelpText(
                InterTrackSilenceBox,
                "Określa dodatkową ciszę między nagraniami w całej tej sesji. "
                + "Plik i folder to nadpisują.");
            AutomationProperties.SetHelpText(
                PlaybackRateBox,
                "Prędkość dla całej tej sesji. 1,00 razy oznacza normalną prędkość; "
                + "mniejsze wartości są wolniejsze, a większe szybsze.");
        }
        else if (target == ItemPlaybackOptionsTarget.RadioStation)
        {
            // Opcje jednej stacji radiowej. ZGLOSZENIE Michala 15.09.2026:
            // zapasowy adres strumienia i wlasny folder nagran tej stacji.
            Title = "Opcje strumienia";
            RadioSettingsPanel.Visibility = Visibility.Visible;
            BackupStreamUrlBox.Text = radioBackupStreamUrl ?? string.Empty;
            var radioFolderChoices = new[]
            {
                new FolderChoice(false, "Zgodnie z ustawieniem nagrywania"),
                new FolderChoice(true, "Własny folder dla tej stacji")
            };
            RadioRecordingFolderModeBox.ItemsSource = radioFolderChoices;
            RadioRecordingFolderModeBox.SelectedItem =
                radioFolderChoices[string.IsNullOrWhiteSpace(radioRecordingFolder) ? 0 : 1];
            RadioRecordingFolderBox.Text = radioRecordingFolder ?? string.Empty;
            UpdateRadioFolderControls();

            // W radiu na zywo nie ma czego wznawiac ani wyciszac miedzy
            // utworami - te pozycje tylko myliłyby przy czytaniu okna.
            ResumeModeLabel.Visibility = Visibility.Collapsed;
            ResumeModeBox.Visibility = Visibility.Collapsed;
            InterTrackSilenceLabel.Visibility = Visibility.Collapsed;
            InterTrackSilenceBox.Visibility = Visibility.Collapsed;
            SmoothTransitionsLabel.Visibility = Visibility.Collapsed;
            SmoothTransitionsBox.Visibility = Visibility.Collapsed;
            // ZGLOSZENIE Michala 15.09.2026: predkosc odtwarzania nie ma sensu
            // przy transmisji na zywo. Te trzy pola i tak nic nie zapisywaly -
            // okno stacji zapisuje wylacznie zapasowy adres i folder nagran -
            // wiec myliłyby tylko przy czytaniu okna czytnikiem.
            PlaybackRateLabel.Visibility = Visibility.Collapsed;
            PlaybackRateBox.Visibility = Visibility.Collapsed;
            LoudnessNormalizationLabel.Visibility = Visibility.Collapsed;
            LoudnessNormalizationBox.Visibility = Visibility.Collapsed;
            OutputDeviceLabel.Visibility = Visibility.Collapsed;
            OutputDeviceBox.Visibility = Visibility.Collapsed;
        }
        else if (target is ItemPlaybackOptionsTarget.Podcast or ItemPlaybackOptionsTarget.PodcastEpisode)
        {
            var podcastTarget = target == ItemPlaybackOptionsTarget.Podcast;
            Title = podcastTarget ? "Opcje podcastu" : "Opcje odcinka podcastu";
            ResumeModeLabel.Content = podcastTarget
                ? "_Pozycja odtwarzania odcinków:"
                : "_Pozycja odtwarzania odcinka:";
            PlaybackRateLabel.Content = podcastTarget
                ? "_Prędkość odcinków podcastu:"
                : "_Prędkość tego odcinka:";
            InterTrackSilenceLabel.Content = podcastTarget
                ? "_Cisza po odcinkach podcastu:"
                : "_Cisza po tym odcinku:";
            AutomationProperties.SetName(ResumeModeBox, podcastTarget
                ? "Pozycja odtwarzania odcinków podcastu"
                : "Pozycja odtwarzania tego odcinka");
            AutomationProperties.SetName(PlaybackRateBox, podcastTarget
                ? "Prędkość odcinków podcastu"
                : "Prędkość tego odcinka");
            AutomationProperties.SetName(InterTrackSilenceBox, podcastTarget
                ? "Cisza po odcinkach podcastu"
                : "Cisza po tym odcinku");
            AutomationProperties.SetHelpText(
                ResumeModeBox,
                podcastTarget
                    ? "Wybierz ustawienie dla wszystkich odcinków tego podcastu albo ustawienie globalne."
                    : "Wybierz ustawienie dla tego odcinka albo dziedziczenie z podcastu i ustawienia globalnego.");
            AutomationProperties.SetHelpText(
                LoudnessNormalizationBox,
                podcastTarget
                    ? "Może dziedziczyć ustawienie globalne albo zostać ustawiona dla całego podcastu."
                    : "Może dziedziczyć ustawienie podcastu i globalne albo zostać ustawiona tylko dla tego odcinka.");
            if (podcastTarget)
            {
                PodcastSettingsPanel.Visibility = Visibility.Visible;
                var refreshChoices = new[]
                {
                    new RefreshChoice(0, "Tylko ręcznie"),
                    new RefreshChoice(15, "Co 15 minut"),
                    new RefreshChoice(30, "Co 30 minut"),
                    new RefreshChoice(60, "Co godzinę"),
                    new RefreshChoice(180, "Co 3 godziny"),
                    new RefreshChoice(360, "Co 6 godzin"),
                    new RefreshChoice(720, "Co 12 godzin"),
                    new RefreshChoice(1440, "Raz dziennie")
                };
                PodcastRefreshIntervalBox.ItemsSource = refreshChoices;
                PodcastRefreshIntervalBox.SelectedItem = refreshChoices.FirstOrDefault(choice =>
                    choice.Minutes == podcastRefreshIntervalMinutes) ?? refreshChoices[0];
                var folderChoices = new[]
                {
                    new FolderChoice(false, "Zgodnie z ustawieniem Podcastów"),
                    new FolderChoice(true, "Własny folder dla tego podcastu")
                };
                PodcastDownloadFolderModeBox.ItemsSource = folderChoices;
                PodcastDownloadFolderModeBox.SelectedItem = folderChoices[podcastDownloadFolder is null ? 0 : 1];
                PodcastDownloadFolderBox.Text = podcastDownloadFolder ?? string.Empty;
                UpdatePodcastFolderControls();
            }
        }
        ItemTitleText.Text = itemTitle;

        ResumeChoice[] resumeChoices =
        [
            new ResumeChoice(ResumePositionMode.Remember, "Pamiętaj pozycję odtwarzania"),
            new ResumeChoice(ResumePositionMode.StartFromBeginning, "Zawsze od początku"),
            new ResumeChoice(ResumePositionMode.Inherit, ResumeInheritedLabel(target))
        ];
        ResumeModeBox.ItemsSource = resumeChoices;
        ResumeModeBox.SelectedItem = resumeChoices.First(choice => choice.Value == resumePositionMode);

        var rateChoices = new List<RateChoice>
        {
            new(null, InheritedRateLabel(target))
        };
        rateChoices.AddRange(PlaybackRates.Select(rate => new RateChoice(rate, RateLabel(rate))));
        PlaybackRateBox.ItemsSource = rateChoices;
        PlaybackRateBox.SelectedItem = playbackRateOverride.HasValue
            ? rateChoices
                .Where(choice => choice.Value.HasValue)
                .MinBy(choice => Math.Abs(choice.Value!.Value - playbackRateOverride.Value))
            : rateChoices[0];

        var loudnessChoices = BooleanChoices(target);
        LoudnessNormalizationBox.ItemsSource = loudnessChoices;
        LoudnessNormalizationBox.SelectedItem = loudnessChoices.First(choice =>
            choice.Value == loudnessNormalizationOverride);

        var transitionChoices = BooleanChoices(target);
        SmoothTransitionsBox.ItemsSource = transitionChoices;
        SmoothTransitionsBox.SelectedItem = transitionChoices.First(choice =>
            choice.Value == smoothTrackTransitionsOverride);

        var silenceChoices = new List<SilenceChoice>
        {
            new(null, InheritedLabel(target))
        };
        silenceChoices.AddRange(
            PlaybackAudioSettingsRules.SupportedInterTrackSilenceMilliseconds.Select(milliseconds =>
                new SilenceChoice(
                    milliseconds,
                    milliseconds == 0
                        ? "Bez dodatkowej ciszy"
                        : $"Cisza: {PlaybackAudioSettingsRules.GetInterTrackSilenceLabel(milliseconds)}")));
        InterTrackSilenceBox.ItemsSource = silenceChoices;
        InterTrackSilenceBox.SelectedItem = silenceChoices.FirstOrDefault(choice =>
            choice.Value == interTrackSilenceMillisecondsOverride) ?? silenceChoices[0];

        OutputDeviceBox.Items.Add("Domyślne urządzenie systemowe — tryb współdzielony");
        OutputDeviceBox.SelectedIndex = 0;
        Loaded += (_, _) => ResumeModeBox.Focus();
    }

    public ResumePositionMode SelectedResumePositionMode =>
        (ResumeModeBox.SelectedItem as ResumeChoice)?.Value ?? ResumePositionMode.Inherit;

    public double? SelectedPlaybackRateOverride =>
        (PlaybackRateBox.SelectedItem as RateChoice)?.Value;

    public bool? SelectedLoudnessNormalizationOverride =>
        (LoudnessNormalizationBox.SelectedItem as BooleanChoice)?.Value;

    public bool? SelectedSmoothTrackTransitionsOverride =>
        (SmoothTransitionsBox.SelectedItem as BooleanChoice)?.Value;

    public int? SelectedInterTrackSilenceMillisecondsOverride =>
        (InterTrackSilenceBox.SelectedItem as SilenceChoice)?.Value;

    public int SelectedPodcastRefreshIntervalMinutes =>
        (PodcastRefreshIntervalBox.SelectedItem as RefreshChoice)?.Minutes ?? 0;

    public string? SelectedPodcastDownloadFolder =>
        (PodcastDownloadFolderModeBox.SelectedItem as FolderChoice)?.Custom == true
            ? PodcastDownloadFolderBox.Text.Trim()
            : null;

    public string? SelectedRadioBackupStreamUrl
    {
        get
        {
            var wpisane = BackupStreamUrlBox.Text.Trim();
            return wpisane.Length == 0 ? null : wpisane;
        }
    }

    public string? SelectedRadioRecordingFolder =>
        (RadioRecordingFolderModeBox.SelectedItem as FolderChoice)?.Custom == true
            ? RadioRecordingFolderBox.Text.Trim()
            : null;

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if ((RadioRecordingFolderModeBox.SelectedItem as FolderChoice)?.Custom == true
            && (string.IsNullOrWhiteSpace(RadioRecordingFolderBox.Text)
                || !Path.IsPathFullyQualified(RadioRecordingFolderBox.Text)))
        {
            AccessibleMediaController.Windows.Services.AccessibleDialog.Show(
                this,
                "Wybierz pełną ścieżkę własnego folderu nagrań albo użyj folderu ogólnego.",
                "Folder nagrań stacji",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            BrowseRadioRecordingFolderButton.Focus();
            return;
        }
        // Zapasowy adres musi byc adresem, inaczej cisza po awarii glownego
        // byłaby jeszcze trudniejsza do zrozumienia niz sama awaria.
        var zapasowy = BackupStreamUrlBox.Text.Trim();
        var adresPoprawny = zapasowy.Length == 0
            || (Uri.TryCreate(zapasowy, UriKind.Absolute, out var adres)
                && adres.Scheme is "http" or "https");
        if (!adresPoprawny)
        {
            AccessibleMediaController.Windows.Services.AccessibleDialog.Show(
                this,
                "Zapasowy adres strumienia musi zaczynać się od http albo https. "
                + "Zostaw pole puste, jeśli stacja ma tylko jeden adres.",
                "Zapasowy adres strumienia",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            BackupStreamUrlBox.Focus();
            BackupStreamUrlBox.SelectAll();
            return;
        }
        if ((PodcastDownloadFolderModeBox.SelectedItem as FolderChoice)?.Custom == true
            && (string.IsNullOrWhiteSpace(PodcastDownloadFolderBox.Text)
                || !Path.IsPathFullyQualified(PodcastDownloadFolderBox.Text)))
        {
            AccessibleMediaController.Windows.Services.AccessibleDialog.Show(
                this,
                "Wybierz pełną ścieżkę własnego folderu albo użyj folderu ogólnego Podcastów.",
                "Folder pobierania podcastu",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            BrowsePodcastDownloadFolderButton.Focus();
            return;
        }
        DialogResult = true;
    }

    private void PodcastDownloadFolderModeBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e) =>
        UpdatePodcastFolderControls();

    private void UpdatePodcastFolderControls()
    {
        if (PodcastDownloadFolderBox is null || BrowsePodcastDownloadFolderButton is null) return;
        var enabled = (PodcastDownloadFolderModeBox.SelectedItem as FolderChoice)?.Custom == true;
        PodcastDownloadFolderBox.IsEnabled = enabled;
        BrowsePodcastDownloadFolderButton.IsEnabled = enabled;
    }

    private void BrowsePodcastDownloadFolder_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog
        {
            Title = "Wybierz folder pobierania odcinków tego podcastu",
            Multiselect = false
        };
        if (Directory.Exists(PodcastDownloadFolderBox.Text))
            dialog.InitialDirectory = PodcastDownloadFolderBox.Text;
        if (dialog.ShowDialog(this) != true) return;
        PodcastDownloadFolderBox.Text = dialog.FolderName;
        BrowsePodcastDownloadFolderButton.Focus();
    }

    private void RadioRecordingFolderModeBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e) =>
        UpdateRadioFolderControls();

    private void UpdateRadioFolderControls()
    {
        if (RadioRecordingFolderBox is null || BrowseRadioRecordingFolderButton is null) return;
        var enabled = (RadioRecordingFolderModeBox.SelectedItem as FolderChoice)?.Custom == true;
        RadioRecordingFolderBox.IsEnabled = enabled;
        BrowseRadioRecordingFolderButton.IsEnabled = enabled;
    }

    private void BrowseRadioRecordingFolder_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog
        {
            Title = "Wybierz folder nagrań tej stacji",
            Multiselect = false
        };
        if (Directory.Exists(RadioRecordingFolderBox.Text))
            dialog.InitialDirectory = RadioRecordingFolderBox.Text;
        if (dialog.ShowDialog(this) != true) return;
        RadioRecordingFolderBox.Text = dialog.FolderName;
        BrowseRadioRecordingFolderButton.Focus();
    }

    private static BooleanChoice[] BooleanChoices(ItemPlaybackOptionsTarget target) =>
    [
        new(null, InheritedLabel(target)),
        new(true, "Włączone"),
        new(false, "Wyłączone")
    ];

    private static string InheritedLabel(ItemPlaybackOptionsTarget target) => target switch
    {
        ItemPlaybackOptionsTarget.LocalFolder => "Według folderu nadrzędnego, sesji lub ustawienia globalnego",
        ItemPlaybackOptionsTarget.LocalItem => "Według folderu, sesji lub ustawienia globalnego",
        ItemPlaybackOptionsTarget.PodcastEpisode => "Według podcastu, sesji lub ustawienia globalnego",
        ItemPlaybackOptionsTarget.Session => "Według ustawienia globalnego",
        _ => "Według ustawienia globalnego"
    };

    private static string ResumeInheritedLabel(ItemPlaybackOptionsTarget target) => target switch
    {
        ItemPlaybackOptionsTarget.LocalFolder => "Zgodnie z folderem nadrzędnym, sesją lub ustawieniem globalnym",
        ItemPlaybackOptionsTarget.LocalItem => "Zgodnie z ustawieniem folderu, sesji lub globalnym",
        ItemPlaybackOptionsTarget.PodcastEpisode => "Zgodnie z ustawieniem podcastu, sesji lub globalnym",
        ItemPlaybackOptionsTarget.Session => "Zgodnie z ustawieniem globalnym",
        _ => "Zgodnie z ustawieniem globalnym"
    };

    private static string InheritedRateLabel(ItemPlaybackOptionsTarget target) => target switch
    {
        ItemPlaybackOptionsTarget.LocalFolder => "Według folderu nadrzędnego lub prędkości sesji",
        ItemPlaybackOptionsTarget.LocalItem => "Według folderu lub prędkości sesji",
        ItemPlaybackOptionsTarget.PodcastEpisode => "Według podcastu lub prędkości sesji",
        ItemPlaybackOptionsTarget.Session => "Bez zmiany prędkości",
        _ => "Według prędkości sesji"
    };

    private static string RateLabel(double rate)
    {
        var value = rate.ToString("0.00", CultureInfo.GetCultureInfo("pl-PL"));
        return rate switch
        {
            < 1d => $"{value} razy — wolniej",
            > 1d => $"{value} razy — szybciej",
            _ => $"{value} razy — normalna prędkość"
        };
    }

    private sealed record ResumeChoice(ResumePositionMode Value, string Label)
    {
        public override string ToString() => Label;
    }

    private sealed record RateChoice(double? Value, string Label)
    {
        public override string ToString() => Label;
    }

    private sealed record BooleanChoice(bool? Value, string Label)
    {
        public override string ToString() => Label;
    }

    private sealed record SilenceChoice(int? Value, string Label)
    {
        public override string ToString() => Label;
    }

    private sealed record RefreshChoice(int Minutes, string Label)
    {
        public override string ToString() => Label;
    }

    private sealed record FolderChoice(bool Custom, string Label)
    {
        public override string ToString() => Label;
    }
}
