using System.Windows.Forms;

namespace AccessibleMediaController.Windows.Controls;

/// <summary>
/// Keeps the current status text synchronized with the single text item that
/// NVDA expects to find inside a standard Windows status bar.
/// </summary>
public sealed class AccessiblePlaybackStatusStrip : StatusStrip
{
    private string _spokenText = string.Empty;

    public AccessiblePlaybackStatusStrip()
    {
        // The status bar is informational. It must remain available to NVDA+End,
        // but it must never become the keyboard target while playback updates it.
        SetStyle(ControlStyles.Selectable, false);
        TabStop = false;
    }

    public string SpokenText
    {
        get => _spokenText;
        set
        {
            var normalized = value ?? string.Empty;
            if (string.Equals(_spokenText, normalized, StringComparison.Ordinal)) return;
            _spokenText = normalized;
            AccessibleName = normalized;
            foreach (ToolStripStatusLabel label in Items.OfType<ToolStripStatusLabel>())
            {
                label.Text = normalized;
                label.AccessibleName = normalized;
                label.AccessibleRole = AccessibleRole.StaticText;
            }

            // NVDA+End reads the current name directly from the standard status
            // bar tree. Do not emit a NameChange event every second: for a native
            // control hosted inside WPF that event can move NVDA's navigator away
            // from the player even though WPF still reports logical focus there.
        }
    }
}
