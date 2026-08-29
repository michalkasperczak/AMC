using System.Windows.Forms;

namespace AccessibleMediaController.Windows.Controls;

/// <summary>
/// Keeps the current status text synchronized with the single text item that
/// NVDA expects to find inside a standard Windows status bar.
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
            foreach (ToolStripStatusLabel label in Items.OfType<ToolStripStatusLabel>())
            {
                label.Text = normalized;
                label.AccessibleName = normalized;
                label.AccessibleRole = AccessibleRole.StaticText;
            }

            // Child 0 is the one status label. Announcing a change on that
            // child preserves the standard StatusStrip accessibility tree,
            // which is used by NVDA+End.
            AccessibilityNotifyClients(AccessibleEvents.NameChange, 0);
        }
    }
}
