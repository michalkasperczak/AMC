using System.ComponentModel;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Input;
using AccessibleMediaController.Windows.Controls;
using AccessibleMediaController.Windows.Services;

namespace AccessibleMediaController.Windows;

public partial class InformationWindow : AccessibleWindow
{
    private readonly string _information;
    private readonly IReadOnlyList<InformationLink> _links;
    private readonly System.Windows.Forms.RichTextBox _informationBox;

    public InformationWindow(
        string information,
        IReadOnlyList<InformationLink>? links = null,
        string? windowTitle = null,
        string? initialFocusName = null)
    {
        InitializeComponent();
        var accessibleTitle = string.IsNullOrWhiteSpace(windowTitle)
            ? "Właściwości i informacje"
            : windowTitle.Trim();
        Title = accessibleTitle;
        System.Windows.Automation.AutomationProperties.SetName(this, accessibleTitle);
        _information = information;
        _links = links ?? [];
        _informationBox = new System.Windows.Forms.RichTextBox
        {
            // For long-form content such as a podcast description, the focused
            // text field starts with the content itself. NVDA therefore does
            // not make the generic control role the first useful announcement.
            AccessibleName = string.IsNullOrWhiteSpace(initialFocusName)
                ? accessibleTitle
                : initialFocusName.Trim(),
            AccessibleDescription = _links.Count == 0
                ? "Tekst tylko do odczytu. Można poruszać się po znakach, słowach i wierszach oraz zaznaczać fragmenty."
                : "Tekst tylko do odczytu. Można poruszać się po znakach, słowach i wierszach oraz zaznaczać fragmenty. Tab przechodzi do listy aktywnych łączy.",
            BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle,
            DetectUrls = true,
            Dock = System.Windows.Forms.DockStyle.Fill,
            HideSelection = false,
            Multiline = true,
            ReadOnly = true,
            ScrollBars = System.Windows.Forms.RichTextBoxScrollBars.Vertical,
            ShortcutsEnabled = true,
            TabStop = true,
            Text = information,
            WordWrap = true
        };
        _informationBox.KeyDown += InformationBox_KeyDown;
        _informationBox.LinkClicked += InformationBox_LinkClicked;
        InformationHost.Child = _informationBox;
        if (_links.Count > 0)
        {
            LinksList.ItemsSource = _links;
            LinksList.SelectedIndex = 0;
            LinksList.Visibility = Visibility.Visible;
        }
    }

    private void Window_ContentRendered(object? sender, EventArgs e)
    {
        _informationBox.Select(0, 0);
        _informationBox.Focus();
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (Keyboard.Modifiers == ModifierKeys.None && e.Key == Key.Escape)
        {
            e.Handled = true;
            Close();
        }
    }

    private void InformationBox_KeyDown(object? sender, System.Windows.Forms.KeyEventArgs e)
    {
        if (e.Modifiers == System.Windows.Forms.Keys.None
            && e.KeyCode == System.Windows.Forms.Keys.Enter
            && TryGetUrlAtCaret(out var url))
        {
            e.Handled = true;
            e.SuppressKeyPress = true;
            OpenExternalUri(url);
            return;
        }
        if (e.Modifiers != System.Windows.Forms.Keys.None
            || e.KeyCode != System.Windows.Forms.Keys.Escape)
        {
            return;
        }

        e.Handled = true;
        e.SuppressKeyPress = true;
        Dispatcher.BeginInvoke(Close);
    }

    private void InformationBox_LinkClicked(
        object? sender,
        System.Windows.Forms.LinkClickedEventArgs e)
    {
        if (!string.IsNullOrWhiteSpace(e.LinkText)) OpenExternalUri(e.LinkText);
    }

    private bool TryGetUrlAtCaret(out string url)
    {
        var caret = _informationBox.SelectionStart;
        foreach (Match match in Regex.Matches(_information, @"https?://[^\s]+", RegexOptions.IgnoreCase))
        {
            if (caret < match.Index || caret > match.Index + match.Length) continue;
            url = match.Value.TrimEnd('.', ',', ';', ')', ']');
            return true;
        }
        url = string.Empty;
        return false;
    }

    private void LinksList_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter || Keyboard.Modifiers != ModifierKeys.None) return;
        OpenSelectedLink();
        e.Handled = true;
    }

    private void LinksList_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e) =>
        OpenSelectedLink();

    private void OpenSelectedLink()
    {
        if (LinksList.SelectedItem is InformationLink link) OpenExternalUri(link.Uri);
    }

    private void OpenExternalUri(string uri)
    {
        try
        {
            Process.Start(new ProcessStartInfo(uri) { UseShellExecute = true });
            CopyStatusText.Announce("Otwarto łącze w zewnętrznej aplikacji");
        }
        catch (Exception exception) when (exception is InvalidOperationException
            or Win32Exception
            or System.IO.IOException)
        {
            CopyStatusText.Announce($"Nie można otworzyć łącza: {exception.Message}");
        }
    }

    private void Copy_Click(object sender, RoutedEventArgs e)
    {
        if (ClipboardRetry.TrySetText(_information, out var errorMessage))
        {
            CopyStatusText.Announce("Skopiowano całą treść");
            return;
        }
        CopyStatusText.Announce(errorMessage);
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}

public sealed record InformationLink(string Label, string Uri)
{
    public override string ToString() => Label;
}
