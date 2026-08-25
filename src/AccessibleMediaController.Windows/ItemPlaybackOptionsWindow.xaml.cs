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
        bool folderTarget = false)
    {
        InitializeComponent();
        if (folderTarget)
        {
            Title = "Opcje odtwarzania folderu";
            ResumeModeLabel.Content = "_Pozycja plików po ponownym otwarciu:";
            PlaybackRateLabel.Content = "_Prędkość plików w folderze:";
            OutputDeviceLabel.Content = "_Urządzenie audio dla folderu:";
            AutomationProperties.SetName(ResumeModeBox, "Pozycja plików po ponownym otwarciu");
            AutomationProperties.SetName(PlaybackRateBox, "Prędkość plików w folderze");
            AutomationProperties.SetName(OutputDeviceBox, "Urządzenie audio dla folderu");
        }
        ItemTitleText.Text = itemTitle;

        ResumeChoice[] resumeChoices = folderTarget
            ?
            [
                new ResumeChoice(ResumePositionMode.Inherit, "Według folderu nadrzędnego lub ustawienia ogólnego"),
                new ResumeChoice(ResumePositionMode.Remember, "Pamiętaj pozycję plików w tym folderze"),
                new ResumeChoice(ResumePositionMode.StartFromBeginning, "Pliki w tym folderze zawsze od początku")
            ]
            :
            [
                new ResumeChoice(ResumePositionMode.Inherit, "Według ustawienia folderu lub ustawienia ogólnego"),
                new ResumeChoice(ResumePositionMode.Remember, "Pamiętaj pozycję dla tego elementu"),
                new ResumeChoice(ResumePositionMode.StartFromBeginning, "Zawsze od początku dla tego elementu")
            ];
        ResumeModeBox.ItemsSource = resumeChoices;
        ResumeModeBox.SelectedItem = resumeChoices.First(choice => choice.Value == resumePositionMode);

        var rateChoices = new List<RateChoice>
        {
            new(null, folderTarget
                ? "Według folderu nadrzędnego lub prędkości sesji"
                : "Według folderu lub prędkości sesji")
        };
        rateChoices.AddRange(PlaybackRates.Select(rate => new RateChoice(
            rate,
            $"{rate.ToString("0.00", CultureInfo.GetCultureInfo("pl-PL"))} razy")));
        PlaybackRateBox.ItemsSource = rateChoices;
        PlaybackRateBox.SelectedItem = playbackRateOverride.HasValue
            ? rateChoices
                .Where(choice => choice.Value.HasValue)
                .MinBy(choice => Math.Abs(choice.Value!.Value - playbackRateOverride.Value))
            : rateChoices[0];

        OutputDeviceBox.Items.Add("Domyślne urządzenie systemowe — tryb współdzielony");
        OutputDeviceBox.SelectedIndex = 0;
        Loaded += (_, _) => ResumeModeBox.Focus();
    }

    public ResumePositionMode SelectedResumePositionMode =>
        (ResumeModeBox.SelectedItem as ResumeChoice)?.Value ?? ResumePositionMode.Inherit;

    public double? SelectedPlaybackRateOverride =>
        (PlaybackRateBox.SelectedItem as RateChoice)?.Value;

    private void Save_Click(object sender, RoutedEventArgs e) => DialogResult = true;

    private sealed record ResumeChoice(ResumePositionMode Value, string Label);
    private sealed record RateChoice(double? Value, string Label);
}
