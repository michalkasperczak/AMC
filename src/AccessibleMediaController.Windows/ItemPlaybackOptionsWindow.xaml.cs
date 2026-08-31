using System.Globalization;
using System.Windows;
using System.Windows.Automation;
using AccessibleMediaController.Core.Configuration;

namespace AccessibleMediaController.Windows;

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
        bool folderTarget = false)
    {
        InitializeComponent();
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
        ItemTitleText.Text = itemTitle;

        ResumeChoice[] resumeChoices = folderTarget
            ?
            [
                new ResumeChoice(ResumePositionMode.Remember, "Pamiętaj pozycję odtwarzania"),
                new ResumeChoice(ResumePositionMode.StartFromBeginning, "Zawsze od początku"),
                new ResumeChoice(ResumePositionMode.Inherit, "Zgodnie z folderem nadrzędnym lub ustawieniem globalnym")
            ]
            :
            [
                new ResumeChoice(ResumePositionMode.Remember, "Pamiętaj pozycję odtwarzania"),
                new ResumeChoice(ResumePositionMode.StartFromBeginning, "Zawsze od początku"),
                new ResumeChoice(ResumePositionMode.Inherit, "Zgodnie z ustawieniem folderu lub globalnym")
            ];
        ResumeModeBox.ItemsSource = resumeChoices;
        ResumeModeBox.SelectedItem = resumeChoices.First(choice => choice.Value == resumePositionMode);

        var rateChoices = new List<RateChoice>
        {
            new(null, folderTarget
                ? "Według folderu nadrzędnego lub prędkości sesji"
                : "Według folderu lub prędkości sesji")
        };
        rateChoices.AddRange(PlaybackRates.Select(rate => new RateChoice(rate, RateLabel(rate))));
        PlaybackRateBox.ItemsSource = rateChoices;
        PlaybackRateBox.SelectedItem = playbackRateOverride.HasValue
            ? rateChoices
                .Where(choice => choice.Value.HasValue)
                .MinBy(choice => Math.Abs(choice.Value!.Value - playbackRateOverride.Value))
            : rateChoices[0];

        var loudnessChoices = BooleanChoices(folderTarget);
        LoudnessNormalizationBox.ItemsSource = loudnessChoices;
        LoudnessNormalizationBox.SelectedItem = loudnessChoices.First(choice =>
            choice.Value == loudnessNormalizationOverride);

        var transitionChoices = BooleanChoices(folderTarget);
        SmoothTransitionsBox.ItemsSource = transitionChoices;
        SmoothTransitionsBox.SelectedItem = transitionChoices.First(choice =>
            choice.Value == smoothTrackTransitionsOverride);

        var silenceChoices = new List<SilenceChoice>
        {
            new(null, InheritedLabel(folderTarget))
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

    private void Save_Click(object sender, RoutedEventArgs e) => DialogResult = true;

    private static BooleanChoice[] BooleanChoices(bool folderTarget) =>
    [
        new(null, InheritedLabel(folderTarget)),
        new(true, "Włączone"),
        new(false, "Wyłączone")
    ];

    private static string InheritedLabel(bool folderTarget) => folderTarget
        ? "Według folderu nadrzędnego lub ustawienia globalnego"
        : "Według folderu lub ustawienia globalnego";

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
}
