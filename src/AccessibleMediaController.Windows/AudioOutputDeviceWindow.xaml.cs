using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using AccessibleMediaController.Windows.Services;

namespace AccessibleMediaController.Windows;

public partial class AudioOutputDeviceWindow : Window
{
    private readonly IReadOnlyList<AudioOutputDeviceChoice> _choices;

    public AudioOutputDeviceWindow(
        string sessionName,
        string? selectedDeviceId)
    {
        InitializeComponent();
        HeadingText.Text = $"Urządzenie audio — {sessionName}";
        _choices = AudioOutputDeviceCatalog.Enumerate(selectedDeviceId);
        DeviceCombo.ItemsSource = _choices;
        DeviceCombo.SelectedItem = _choices.FirstOrDefault(choice => string.Equals(
            choice.Id,
            selectedDeviceId,
            StringComparison.Ordinal)) ?? _choices[0];
        Loaded += (_, _) => Dispatcher.BeginInvoke(() =>
        {
            DeviceCombo.Focus();
            Keyboard.Focus(DeviceCombo);
        }, DispatcherPriority.ContextIdle);
    }

    public string? SelectedDeviceId =>
        (DeviceCombo.SelectedItem as AudioOutputDeviceChoice)?.Id;

    public string SelectedDeviceLabel =>
        (DeviceCombo.SelectedItem as AudioOutputDeviceChoice)?.Label
        ?? "Domyślne urządzenie systemowe";

    public bool SelectedDeviceIsAvailable =>
        (DeviceCombo.SelectedItem as AudioOutputDeviceChoice)?.IsAvailable != false;

    private void Save_Click(object sender, RoutedEventArgs e) => DialogResult = true;
}
