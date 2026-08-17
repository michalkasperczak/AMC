using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;

namespace AccessibleMediaController.Windows.Controls;

public sealed class AccessibleStatusTextBlock : TextBlock
{
    protected override AutomationPeer OnCreateAutomationPeer() => new StatusAutomationPeer(this);

    public void Announce(string message)
    {
        Text = message;
        var peer = UIElementAutomationPeer.FromElement(this) ?? UIElementAutomationPeer.CreatePeerForElement(this);
        // A notification carries the actual message. Raising LiveRegionChanged as
        // well makes some screen readers announce the region's static name first.
        peer?.RaiseNotificationEvent(
            AutomationNotificationKind.ActionCompleted,
            AutomationNotificationProcessing.ImportantMostRecent,
            message,
            "AccessibleMediaController.Status");
    }

    private sealed class StatusAutomationPeer(AccessibleStatusTextBlock owner) : FrameworkElementAutomationPeer(owner)
    {
        protected override string GetClassNameCore() => "StatusText";
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Text;

        protected override string GetNameCore()
        {
            var currentText = ((AccessibleStatusTextBlock)Owner).Text;
            return string.IsNullOrWhiteSpace(currentText) ? base.GetNameCore() : currentText;
        }
    }
}
