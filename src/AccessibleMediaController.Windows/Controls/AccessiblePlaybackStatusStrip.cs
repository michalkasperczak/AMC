using System.Windows.Forms;

namespace AccessibleMediaController.Windows.Controls;

/// <summary>
/// Exposes one status-bar object with one current spoken value. The visual
/// ToolStrip item is intentionally hidden from the accessibility tree so NVDA
/// does not read the same status twice when NVDA+End requests the status bar.
/// </summary>
public sealed class AccessiblePlaybackStatusStrip : StatusStrip
{
    private string _spokenText = string.Empty;

    public string SpokenText
    {
        get => _spokenText;
        set
        {
            var normalized = value ?? string.Empty;
            if (string.Equals(_spokenText, normalized, StringComparison.Ordinal)) return;
            _spokenText = normalized;
            Text = normalized;
            AccessibilityNotifyClients(AccessibleEvents.NameChange, -1);
        }
    }

    protected override AccessibleObject CreateAccessibilityInstance() =>
        new PlaybackStatusAccessibleObject(this);

    private sealed class PlaybackStatusAccessibleObject(AccessiblePlaybackStatusStrip owner)
        : ControlAccessibleObject(owner)
    {
        public override string? Name
        {
            get => owner.SpokenText;
            set { }
        }

        public override AccessibleRole Role => AccessibleRole.StatusBar;

        public override int GetChildCount() => 0;
    }
}
