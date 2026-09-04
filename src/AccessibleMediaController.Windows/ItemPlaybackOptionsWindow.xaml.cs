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
    PodcastEpisode
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
        string? podcastDownloadFolder = null)
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

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if ((PodcastDownloadFolderModeBox.SelectedItem as FolderChoice)?.Custom == true
            && (string.IsNullOrWhiteSpace(PodcastDownloadFolderBox.Text)
                || !Path.IsPathFullyQualified(PodcastDownloadFolderBox.Text)))
        {
            MessageBox.Show(
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

    private static BooleanChoice[] BooleanChoices(ItemPlaybackOptionsTarget target) =>
    [
        new(null, InheritedLabel(target)),
        new(true, "Włączone"),
        new(false, "Wyłączone")
    ];

    private static string InheritedLabel(ItemPlaybackOptionsTarget target) => target switch
    {
        ItemPlaybackOptionsTarget.LocalFolder => "Według folderu nadrzędnego lub ustawienia globalnego",
        ItemPlaybackOptionsTarget.LocalItem => "Według folderu lub ustawienia globalnego",
        ItemPlaybackOptionsTarget.PodcastEpisode => "Według podcastu lub ustawienia globalnego",
        _ => "Według ustawienia globalnego"
    };

    private static string ResumeInheritedLabel(ItemPlaybackOptionsTarget target) => target switch
    {
        ItemPlaybackOptionsTarget.LocalFolder => "Zgodnie z folderem nadrzędnym lub ustawieniem globalnym",
        ItemPlaybackOptionsTarget.LocalItem => "Zgodnie z ustawieniem folderu lub globalnym",
        ItemPlaybackOptionsTarget.PodcastEpisode => "Zgodnie z ustawieniem podcastu lub globalnym",
        _ => "Zgodnie z ustawieniem globalnym"
    };

    private static string InheritedRateLabel(ItemPlaybackOptionsTarget target) => target switch
    {
        ItemPlaybackOptionsTarget.LocalFolder => "Według folderu nadrzędnego lub prędkości sesji",
        ItemPlaybackOptionsTarget.LocalItem => "Według folderu lub prędkości sesji",
        ItemPlaybackOptionsTarget.PodcastEpisode => "Według podcastu lub prędkości sesji",
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
