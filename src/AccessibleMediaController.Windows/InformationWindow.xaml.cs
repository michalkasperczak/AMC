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
    private readonly bool _preferTextMode;
    private bool _browseModeActive;
    private bool _switchingMode;
    private bool _browserConfigured;
    private bool _closed;

    public InformationWindow(
        string information,
        IReadOnlyList<InformationLink>? links = null,
        string? windowTitle = null,
        string? initialFocusName = null,
        bool preferTextMode = false)
    {
        InitializeComponent();
        _preferTextMode = preferTextMode;
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
        InformationBrowser.KeyDown += InformationBrowser_KeyDown;
        Closed += (_, _) => { _closed = true; InformationBrowser.Dispose(); _informationBox.Dispose(); };
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
            if (InformationBrowser.CoreWebView2 is null)
            {
                var environment = await CoreWebView2Environment.CreateAsync().ConfigureAwait(true);
                if (_closed) return false;
                await InformationBrowser.EnsureCoreWebView2Async(environment).ConfigureAwait(true);
            }
            var core = InformationBrowser.CoreWebView2;
            if (core is null || _closed) return false;

            // To okno pokazuje WYLACZNIE nasz wlasny tekst - zadnych stron z sieci,
            // zadnego menu przegladarki, zadnego pobierania.
            core.Settings.AreDefaultContextMenusEnabled = false;
            core.Settings.AreDevToolsEnabled = false;
            core.Settings.IsStatusBarEnabled = false;
            // Skroty przegladarki zostaja WLACZONE: to one daja szukanie w
            // tresci (Ctrl+F) i nawigacje, o ktora chodzilo w zgloszeniu.
            core.Settings.AreBrowserAcceleratorKeysEnabled = true;
            core.Settings.IsPasswordAutosaveEnabled = false;
            core.Settings.IsGeneralAutofillEnabled = false;

            // Escape musi zamykac okno takze wtedy, gdy ognisko jest w dokumencie.
            // Klawisze z wnetrza WebView2 nie docieraja do PreviewKeyDown okna.
            if (!_browserConfigured)
            {
            core.WebMessageReceived += (_, message) =>
            {
                var command = message.TryGetWebMessageAsString();
                if (command == "zamknij") Dispatcher.BeginInvoke(Close);
                else if (command == "mode") Dispatcher.BeginInvoke(async () => await SwitchModeAsync());
            };
            await core.AddScriptToExecuteOnDocumentCreatedAsync(
                "document.addEventListener('keydown', function (event) {"
                + "if (event.key === 'Escape' && !event.ctrlKey && !event.altKey && !event.shiftKey) {"
                + "event.preventDefault(); window.chrome.webview.postMessage('zamknij'); }"
                + "if (event.key === 'F6' && !event.ctrlKey && !event.altKey && !event.shiftKey) {"
                + "event.preventDefault(); window.chrome.webview.postMessage('mode'); } }, true);")
                .ConfigureAwait(true);
            _browserConfigured = true;
            }

            // Fokus dopiero PO wczytaniu dokumentu. Ustawiony wczesniej trafial
            // w pusty widok i czytnik nie mial czego czytac - kursor wygladal
            // na zablokowany (ZGLOSZENIE Michala 15.09.2026).
            var wczytane = new TaskCompletionSource<bool>();
            void Wczytany(object? _, CoreWebView2NavigationCompletedEventArgs __)
            {
                core.NavigationCompleted -= Wczytany;
                wczytane.TrySetResult(true);
            }
            core.NavigationCompleted += Wczytany;
            core.NavigateToString(_document);
            InformationHost.Visibility = Visibility.Collapsed;
            InformationBrowser.Visibility = Visibility.Visible;
            // Lacza sa juz W dokumencie jako prawdziwe lacza HTML - osobna lista
            // tylko dublowalaby je dla czytnika.
            LinksList.Visibility = Visibility.Collapsed;

            // Gdyby zdarzenie nie przyszlo, nie zawieszamy okna na zawsze.
            var skonczone = await Task.WhenAny(wczytane.Task, Task.Delay(5000)).ConfigureAwait(true);
            if (skonczone != wczytane.Task)
            {
                core.NavigationCompleted -= Wczytany;
                InformationBrowser.Visibility = Visibility.Collapsed;
                InformationHost.Visibility = Visibility.Visible;
                if (_links.Count > 0) LinksList.Visibility = Visibility.Visible;
                return false;
            }

            if (_closed) return false;
            InformationBrowser.Focus();
            // Zostaw naglowek widoczny przy wejsciu do dokumentu.
            await core.ExecuteScriptAsync(
                "(function(){var c=document.querySelector('h1');"
                + "if(c){c.setAttribute('tabindex','-1');c.focus({preventScroll:true});window.scrollTo(0,0);}})();")
                .ConfigureAwait(true);
            return true;
        }
        catch (Exception exception) when (exception is WebView2RuntimeNotFoundException
            or InvalidOperationException
            or System.ComponentModel.Win32Exception
            or System.IO.IOException
            or UnauthorizedAccessException
            or System.Runtime.InteropServices.COMException)
        {
            if (_closed) return false;
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
        if (!_preferTextMode)
        {
            await SwitchModeAsync();
            return;
        }
        ShowTextMode();
    }

    private void ShowTextMode()
    {
        if (_closed) return;
        _browseModeActive = false;
        InformationBrowser.Visibility = Visibility.Collapsed;
        InformationHost.Visibility = Visibility.Visible;
        LinksList.Visibility = _links.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        ViewModeButton.Content = "Widok _dokumentu (F6)";
        // Showing the WinForms host needs a layout pass before its HWND can take focus.
        Dispatcher.BeginInvoke(() =>
        {
            if (_closed || _browseModeActive) return;
            InformationHost.Focus();
            _informationBox.Select(0, 0);
            _informationBox.Focus();
        }, System.Windows.Threading.DispatcherPriority.Loaded);
    }

    private async Task SwitchModeAsync()
    {
        if (_switchingMode || _closed) return;
        _switchingMode = true;
        try
        {
            if (_browseModeActive) { ShowTextMode(); return; }
            if (await TryStartBrowseModeAsync())
            {
                _browseModeActive = true;
                ViewModeButton.Content = "Widok _tekstowy (F6)";
            }
            else ShowTextMode();
        }
        finally { _switchingMode = false; }
    }

    private async void ViewMode_Click(object sender, RoutedEventArgs e) => await SwitchModeAsync();

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (Keyboard.Modifiers == ModifierKeys.None && e.Key == Key.F6)
        {
            e.Handled = true;
            _ = SwitchModeAsync();
            return;
        }
        if (Keyboard.Modifiers == ModifierKeys.None && e.Key == Key.Escape)
        {
            e.Handled = true;
            Close();
        }
    }

    private void InformationBrowser_KeyDown(object sender, KeyEventArgs e)
    {
        // WebView2 raises accelerator KeyDown, not the normal preview route.
        // Defer view/focus changes until the browser's synchronous key callback ends.
        if (e.Key != Key.F6 || Keyboard.Modifiers != ModifierKeys.None) return;
        e.Handled = true;
        Dispatcher.BeginInvoke(async () => await SwitchModeAsync());
    }

    private void InformationBox_KeyDown(object? sender, System.Windows.Forms.KeyEventArgs e)
    {
        if (e.Modifiers == System.Windows.Forms.Keys.None && e.KeyCode == System.Windows.Forms.Keys.F6)
        {
            e.Handled = true;
            e.SuppressKeyPress = true;
            _ = SwitchModeAsync();
            return;
        }
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
