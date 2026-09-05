using System.Windows;
using System.Windows.Input;
using AccessibleMediaController.Windows.Controls;

namespace AccessibleMediaController.Windows;

public partial class WiiMOptionWindow : AccessibleWindow
{
    private readonly IReadOnlyList<WiiMOptionChoice> choices;

    public WiiMOptionWindow(
        string title,
        string description,
        IReadOnlyList<WiiMOptionChoice> choices,
        string? selectedValue = null)
    {
        InitializeComponent();
        Title = title;
        DescriptionText.Text = description;
        this.choices = choices;
        OptionList.ItemsSource = choices;
        OptionList.SelectedItem = choices.FirstOrDefault(choice =>
            string.Equals(choice.Value, selectedValue, StringComparison.Ordinal));
        if (OptionList.SelectedItem is null && choices.Count > 0) OptionList.SelectedIndex = 0;
    }

    public WiiMOptionChoice? SelectedChoice { get; private set; }

    private void Window_ContentRendered(object? sender, EventArgs e) => FocusSelectedChoice();

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (Keyboard.Modifiers != ModifierKeys.None || e.Key is not (Key.Enter or Key.Space)) return;
        CommitSelection();
        e.Handled = true;
    }

    private void Apply_Click(object sender, RoutedEventArgs e) => CommitSelection();

    private void CommitSelection()
    {
        if (OptionList.SelectedItem is not WiiMOptionChoice choice)
        {
            OptionStatus.Announce("Najpierw wybierz opcję");
            return;
        }
        SelectedChoice = choice;
        DialogResult = true;
    }

    private void FocusSelectedChoice()
    {
        OptionList.UpdateLayout();
        if (OptionList.ItemContainerGenerator.ContainerFromItem(OptionList.SelectedItem)
            is System.Windows.Controls.ListBoxItem item)
        {
            item.Focus();
            Keyboard.Focus(item);
            return;
        }
        Keyboard.Focus(OptionList);
    }
}

public sealed record WiiMOptionChoice(string Value, string Label)
{
    public string NavigationText => Label;
    public override string ToString() => Label;
}
