using System.Windows;
using System.Windows.Automation;
using System.Windows.Input;
using AccessibleMediaController.Core.Commands;
using AccessibleMediaController.Core.Presentation;

namespace AccessibleMediaController.Windows;

public enum SeekInputMode
{
    Time,
    Percentage
}

public partial class SeekPositionWindow : Window
{
    private readonly SeekInputMode _mode;
    private readonly TimeSpan _duration;

    public SeekPositionWindow(SeekInputMode mode, TimeSpan duration)
    {
        InitializeComponent();
        _mode = mode;
        _duration = duration;

        if (mode == SeekInputMode.Time)
        {
            Title = "Skocz do czasu";
            ValueLabel.Content = "_Czas:";
            InstructionsText.Text =
                $"Wpisz minuty, minuty:sekundy albo godziny:minuty:sekundy. "
                + $"Sama liczba oznacza minuty. Czas całkowity: {CommandRouter.FormatTime(duration)}.";
            AutomationProperties.SetName(ValueBox, "Czas docelowy");
            AutomationProperties.SetHelpText(
                ValueBox,
                "Przykłady: 35 oznacza 35 minut, 1:35 oznacza minutę i 35 sekund, 1:02:30 oznacza godzinę, 2 minuty i 30 sekund.");
        }
        else
        {
            Title = "Skocz do procentu";
            ValueLabel.Content = "_Procent:";
            InstructionsText.Text = "Wpisz liczbę od 0 do 100. Znak procentu jest opcjonalny.";
            AutomationProperties.SetName(ValueBox, "Procent docelowy");
            AutomationProperties.SetHelpText(ValueBox, "Wpisz liczbę od 0 do 100, na przykład 35 albo 35%.");
        }
    }

    public TimeSpan Position { get; private set; }
    public int Percentage { get; private set; }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        ValueBox.Focus();
        Keyboard.Focus(ValueBox);
    }

    private void Confirm_Click(object sender, RoutedEventArgs e)
    {
        if (_mode == SeekInputMode.Time)
        {
            if (!SeekInputParser.TryParseTime(ValueBox.Text, out var position, out var error))
            {
                ShowError(error);
                return;
            }
            if (position > _duration)
            {
                ShowError($"Podany czas przekracza czas całkowity {CommandRouter.FormatTime(_duration)}.");
                return;
            }
            Position = position;
        }
        else
        {
            if (!SeekInputParser.TryParsePercentage(ValueBox.Text, out var percentage, out var error))
            {
                ShowError(error);
                return;
            }
            Percentage = percentage;
        }

        DialogResult = true;
    }

    private void ShowError(string message)
    {
        ErrorText.Announce(message);
        AutomationProperties.SetHelpText(ValueBox, message);
        ValueBox.Focus();
        Keyboard.Focus(ValueBox);
        ValueBox.SelectAll();
    }
}
