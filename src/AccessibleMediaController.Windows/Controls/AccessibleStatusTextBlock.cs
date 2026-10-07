using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;

namespace AccessibleMediaController.Windows.Controls;

public sealed class AccessibleStatusTextBlock : TextBlock
{
    protected override AutomationPeer OnCreateAutomationPeer() => new StatusAutomationPeer(this);

    /// <summary>
    /// RUTYNOWY POSTEP ("trwa odczyt", "wysyłam", "czekaj"): WIDOCZNY tekst
    /// statusu BEZ przerywania czytnikowi.
    ///
    /// ZGLOSZENIE UZYTKOWNIKA: zapowiedzi postepu Sonosa wchodzily mu w slowo
    /// przy KAZDYM wejsciu w sesje i przy KAZDYM uruchomieniu stacji, a czytnik
    /// urywal je w polowie, bo zaraz przychodzil nastepny komunikat. Sama
    /// informacja ma zostac: peer automatyzacji bierze nazwe Z TEKSTU, wiec
    /// status jest nadal DO ODCZYTANIA na zadanie - tylko nikt go nie wypycha.
    ///
    /// To NIE jest wyciszenie <see cref="Announce"/>: wyniki, bledy, odmowy,
    /// tytuly i liczniki ida dalej notyfikacja.
    /// </summary>
    public void ShowProgress(string message) => Text = message;

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
