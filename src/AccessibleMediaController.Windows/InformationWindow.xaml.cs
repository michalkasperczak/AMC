using System.ComponentModel;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Input;
using AccessibleMediaController.Windows.Controls;
using AccessibleMediaController.Windows.Services;
using Microsoft.Web.WebView2.Core;

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
        _document = InformationDocument.Build(_information, _links, accessibleTitle);
    }

    /// <summary>
    /// Tresc jako dokument HTML. Czytnik ekranu dostaje wtedy tryb przegladania
    /// (naglowki, akapity, lacza, znajdowanie). ZGLOSZENIE Michala 15.09.2026.
    /// </summary>
    private readonly string _document;

    /// <summary>
    /// Wlacza tryb przegladania. Gdy WebView2 nie jest zainstalowany albo nie
    /// wstaje, zostaje stare pole tekstowe - okno musi dzialac zawsze.
    /// </summary>
    private async Task<bool> TryStartBrowseModeAsync()
    {
        try
        {
            var environment = await CoreWebView2Environment.CreateAsync().ConfigureAwait(true);
            await InformationBrowser.EnsureCoreWebView2Async(environment).ConfigureAwait(true);
            var core = InformationBrowser.CoreWebView2;
            if (core is null) return false;

            // To okno pokazuje WYLACZNIE nasz wlasny tekst - zadnych stron z sieci,
            // zadnego menu przegladarki, zadnego pobierania.
            core.Settings.AreDefaultContextMenusEnabled = false;
            core.Settings.AreDevToolsEnabled = false;
            core.Settings.IsStatusBarEnabled = false;
            core.Settings.AreBrowserAcceleratorKeysEnabled = false;
            core.Settings.IsPasswordAutosaveEnabled = false;
            core.Settings.IsGeneralAutofillEnabled = false;

            // Escape musi zamykac okno takze wtedy, gdy ognisko jest w dokumencie.
            // Klawisze z wnetrza WebView2 nie docieraja do PreviewKeyDown okna.
            core.WebMessageReceived += (_, message) =>
            {
                if (string.Equals(message.TryGetWebMessageAsString(), "zamknij", StringComparison.Ordinal))
                    Dispatcher.BeginInvoke(Close);
            };
            await core.AddScriptToExecuteOnDocumentCreatedAsync(
                "document.addEventListener('keydown', function (event) {"
                + "if (event.key === 'Escape' && !event.ctrlKey && !event.altKey && !event.shiftKey) {"
                + "event.preventDefault(); window.chrome.webview.postMessage('zamknij'); } }, true);")
                .ConfigureAwait(true);

            core.NavigateToString(_document);
            InformationHost.Visibility = Visibility.Collapsed;
            InformationBrowser.Visibility = Visibility.Visible;
            // Lacza sa juz W dokumencie jako prawdziwe lacza HTML - osobna lista
            // tylko dublowalaby je dla czytnika.
            LinksList.Visibility = Visibility.Collapsed;
            InformationBrowser.Focus();
            return true;
        }
        catch (Exception exception) when (exception is WebView2RuntimeNotFoundException
            or InvalidOperationException
            or System.ComponentModel.Win32Exception
            or System.IO.IOException
            or UnauthorizedAccessException)
        {
            InformationBrowser.Visibility = Visibility.Collapsed;
            InformationHost.Visibility = Visibility.Visible;
            if (_links.Count > 0) LinksList.Visibility = Visibility.Visible;
            return false;
        }
    }

    /// <summary>
    /// Lacza otwiera przegladarka systemowa, nie to okno. Bez tego klikniecie
    /// wciagnelo by cala strone w okno wlasciwosci.
    /// </summary>
    private void InformationBrowser_NavigationStarting(
        object? sender,
        CoreWebView2NavigationStartingEventArgs e)
    {
        if (e.Uri.StartsWith("data:", StringComparison.OrdinalIgnoreCase)
            || e.Uri.StartsWith("about:", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }
        e.Cancel = true;
        OpenExternalUri(e.Uri);
    }

    private async void Window_ContentRendered(object? sender, EventArgs e)
    {
        // Najpierw probujemy trybu przegladania. Dopiero gdy sie nie uda,
        // ognisko idzie do starego pola tekstowego.
        if (await TryStartBrowseModeAsync().ConfigureAwait(true)) return;
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
