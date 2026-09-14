using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;

namespace AccessibleMediaController.Windows.Services;

/// <summary>
/// Wlasne okno komunikatu, ktore czytnik ekranu czyta w dobrej kolejnosci:
/// najpierw TRESC, potem przyciski.
///
/// Systemowy MessageBox ustawia fokus startowy NA PRZYCISKU, a tresc jest
/// statycznym napisem - dlatego NVDA mowi "Tak" albo "OK" przed trescia.
/// Nie da sie tego naprawic opoznieniem mowy: to wyscig z czytnikiem, ktory
/// przy innej szybkosci mowy wypada losowo. Jedyne pewne rozwiazanie to
/// BUDOWA okna: tresc jako pole tekstowe tylko do czytania, z fokusem
/// startowym i nizszym numerem tabulacji niz przyciski.
///
/// Skutek uboczny (dobry): tresc da sie przeczytac ponownie strzalkami bez
/// zamykania okna i skopiowac przez Ctrl+C.
/// </summary>
public static class AccessibleDialog
{
    /// <summary>
    /// Odpowiedz wybrana w danym oknie. Trzymana z boku, a nie w polu okna,
    /// zeby dalo sie ja odczytac po zamknieciu (i zmierzyc w testach).
    /// </summary>
    private static readonly ConditionalWeakTable<Window, AnswerBox> Answers = new();

    private sealed class AnswerBox
    {
        internal MessageBoxResult Value;
    }

    /// <summary>
    /// Ustaw na false, zeby wrocic do systemowych okien MessageBox.
    /// Droga awaryjna na wypadek, gdyby wlasne okno gdzies zawiodlo.
    /// </summary>
    public static bool UseCustomWindows { get; set; } = true;

    public static MessageBoxResult Show(string messageBoxText)
        => Show(null, messageBoxText, string.Empty, MessageBoxButton.OK, MessageBoxImage.None, MessageBoxResult.None);

    public static MessageBoxResult Show(string messageBoxText, string caption)
        => Show(null, messageBoxText, caption, MessageBoxButton.OK, MessageBoxImage.None, MessageBoxResult.None);

    public static MessageBoxResult Show(string messageBoxText, string caption, MessageBoxButton button)
        => Show(null, messageBoxText, caption, button, MessageBoxImage.None, MessageBoxResult.None);

    public static MessageBoxResult Show(string messageBoxText, string caption, MessageBoxButton button, MessageBoxImage icon)
        => Show(null, messageBoxText, caption, button, icon, MessageBoxResult.None);

    public static MessageBoxResult Show(string messageBoxText, string caption, MessageBoxButton button, MessageBoxImage icon, MessageBoxResult defaultResult)
        => Show(null, messageBoxText, caption, button, icon, defaultResult);

    public static MessageBoxResult Show(Window? owner, string messageBoxText)
        => Show(owner, messageBoxText, string.Empty, MessageBoxButton.OK, MessageBoxImage.None, MessageBoxResult.None);

    public static MessageBoxResult Show(Window? owner, string messageBoxText, string caption)
        => Show(owner, messageBoxText, caption, MessageBoxButton.OK, MessageBoxImage.None, MessageBoxResult.None);

    public static MessageBoxResult Show(Window? owner, string messageBoxText, string caption, MessageBoxButton button)
        => Show(owner, messageBoxText, caption, button, MessageBoxImage.None, MessageBoxResult.None);

    public static MessageBoxResult Show(Window? owner, string messageBoxText, string caption, MessageBoxButton button, MessageBoxImage icon)
        => Show(owner, messageBoxText, caption, button, icon, MessageBoxResult.None);

    public static MessageBoxResult Show(
        Window? owner,
        string messageBoxText,
        string caption,
        MessageBoxButton button,
        MessageBoxImage icon,
        MessageBoxResult defaultResult)
    {
        if (!UseCustomWindows)
        {
            return ShowSystemFallback(owner, messageBoxText, caption, button, icon, defaultResult);
        }

        try
        {
            return ShowCustom(owner, messageBoxText ?? string.Empty, caption ?? string.Empty, button, defaultResult);
        }
        catch (Exception)
        {
            // Komunikat MUSI sie pokazac. Ciche pominiecie znaczy decyzje
            // podjeta za uzytkownika, wiec spadamy na okno systemowe.
            return ShowSystemFallback(owner, messageBoxText, caption, button, icon, defaultResult);
        }
    }

    private static MessageBoxResult ShowSystemFallback(
        Window? owner,
        string messageBoxText,
        string caption,
        MessageBoxButton button,
        MessageBoxImage icon,
        MessageBoxResult defaultResult)
    {
        if (owner is not null)
        {
            return MessageBox.Show(owner, messageBoxText, caption, button, icon, defaultResult);
        }

        return MessageBox.Show(messageBoxText, caption, button, icon, defaultResult);
    }

    /// <summary>
    /// Kolejnosc przyciskow. Pod Enterem stoi PIERWSZY przycisk, wiec gdy
    /// domyslna odpowiedz to "nie", odmowa musi byc pierwsza - odruchowy
    /// Enter nie moze wykonac rzeczy, ktorej uzytkownik nie chcial.
    /// </summary>
    internal static IReadOnlyList<MessageBoxResult> BuildButtonOrder(MessageBoxButton button, MessageBoxResult defaultResult)
    {
        switch (button)
        {
            case MessageBoxButton.OK:
                return new[] { MessageBoxResult.OK };

            case MessageBoxButton.OKCancel:
                return defaultResult == MessageBoxResult.Cancel
                    ? new[] { MessageBoxResult.Cancel, MessageBoxResult.OK }
                    : new[] { MessageBoxResult.OK, MessageBoxResult.Cancel };

            case MessageBoxButton.YesNo:
                return defaultResult == MessageBoxResult.No
                    ? new[] { MessageBoxResult.No, MessageBoxResult.Yes }
                    : new[] { MessageBoxResult.Yes, MessageBoxResult.No };

            case MessageBoxButton.YesNoCancel:
                if (defaultResult == MessageBoxResult.No)
                {
                    return new[] { MessageBoxResult.No, MessageBoxResult.Yes, MessageBoxResult.Cancel };
                }

                if (defaultResult == MessageBoxResult.Cancel)
                {
                    return new[] { MessageBoxResult.Cancel, MessageBoxResult.Yes, MessageBoxResult.No };
                }

                return new[] { MessageBoxResult.Yes, MessageBoxResult.No, MessageBoxResult.Cancel };

            default:
                return new[] { MessageBoxResult.OK };
        }
    }

    internal static MessageBoxResult CancelResultFor(MessageBoxButton button)
        => button switch
        {
            MessageBoxButton.OK => MessageBoxResult.OK,
            MessageBoxButton.OKCancel => MessageBoxResult.Cancel,
            MessageBoxButton.YesNo => MessageBoxResult.No,
            MessageBoxButton.YesNoCancel => MessageBoxResult.Cancel,
            _ => MessageBoxResult.None,
        };

    internal static string LabelFor(MessageBoxResult result)
        => result switch
        {
            MessageBoxResult.OK => "OK",
            MessageBoxResult.Cancel => "Anuluj",
            MessageBoxResult.Yes => "Tak",
            MessageBoxResult.No => "Nie",
            _ => result.ToString(),
        };

    /// <summary>
    /// Litery Alt: OK i Anuluj ich NIE dostaja - maja Enter i Escape, wiec
    /// litera byla by zmarnowana.
    /// </summary>
    internal static string AccessKeyLabelFor(MessageBoxResult result)
        => result switch
        {
            MessageBoxResult.Yes => "_Tak",
            MessageBoxResult.No => "_Nie",
            MessageBoxResult.OK => "OK",
            MessageBoxResult.Cancel => "Anuluj",
            _ => result.ToString(),
        };

    /// <summary>
    /// Nazwa pola z trescia. Czytnik mowi ja przed trescia, wiec musi byc
    /// krotka - dluga nazwa opoznia to, na co uzytkownik czeka.
    /// </summary>
    public const string ContentAutomationName = "Tresc komunikatu";

    /// <summary>
    /// Zbuduj okno BEZ pokazywania go. Wylacznie do pomiaru w testach:
    /// kolejnosci fokusa i tabulacji nie da sie sprawdzic na oknie, ktore
    /// samo blokuje watek w ShowDialog.
    /// </summary>
    public static Window CreateForMeasurement(
        string messageBoxText,
        string caption,
        MessageBoxButton button,
        MessageBoxResult defaultResult = MessageBoxResult.None)
        => BuildWindow(null, messageBoxText ?? string.Empty, caption ?? string.Empty, button, defaultResult);

    /// <summary>
    /// Odpowiedz zapisana w oknie zbudowanym przez <see cref="CreateForMeasurement"/>.
    /// </summary>
    public static MessageBoxResult PeekResult(Window window)
        => Answers.TryGetValue(window, out var box) ? box.Value : MessageBoxResult.None;

    private static MessageBoxResult ShowCustom(
        Window? owner,
        string messageBoxText,
        string caption,
        MessageBoxButton button,
        MessageBoxResult defaultResult)
    {
        var window = BuildWindow(owner, messageBoxText, caption, button, defaultResult);
        window.ShowDialog();
        return PeekResult(window);
    }

    private static Window BuildWindow(
        Window? owner,
        string messageBoxText,
        string caption,
        MessageBoxButton button,
        MessageBoxResult defaultResult)
    {
        var order = BuildButtonOrder(button, defaultResult);

        var window = new Window
        {
            Title = string.IsNullOrWhiteSpace(caption) ? "Accessible Multimedia Controller" : caption,
            SizeToContent = SizeToContent.Height,
            Width = 520,
            MinHeight = 190,
            ResizeMode = ResizeMode.NoResize,
            ShowInTaskbar = false,
            WindowStartupLocation = owner is null
                ? WindowStartupLocation.CenterScreen
                : WindowStartupLocation.CenterOwner,
        };

        // Domyslna odpowiedz to ta BEZPIECZNA: zamkniecie okna krzyzykiem,
        // Escape albo Alt+F4 nie moze znaczyc "tak, przerwij nagrywanie".
        var box = new AnswerBox { Value = CancelResultFor(button) };
        Answers.Add(window, box);

        if (owner is not null && !ReferenceEquals(owner, window))
        {
            window.Owner = owner;
        }

        var root = new Grid { Margin = new Thickness(12) };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        // TRESC jako pole tylko do czytania. To ona ma fokus startowy, wiec
        // czytnik czyta ja pierwsza. Da sie po niej chodzic strzalkami.
        var lineCount = 1 + CountLines(messageBoxText);
        var content = new TextBox
        {
            Text = messageBoxText,
            IsReadOnly = true,
            IsReadOnlyCaretVisible = true,
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            BorderThickness = new Thickness(0),
            Background = System.Windows.SystemColors.WindowBrush,
            MinHeight = 72,
            MaxHeight = 320,
            TabIndex = 0,
            Margin = new Thickness(0, 0, 0, 12),
        };

        if (lineCount > 3)
        {
            content.Height = Math.Min(320, 24 + (lineCount * 19));
        }

        AutomationProperties.SetName(content, ContentAutomationName);
        Grid.SetRow(content, 0);
        root.Children.Add(content);

        var buttonRow = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
        };
        Grid.SetRow(buttonRow, 1);
        root.Children.Add(buttonRow);

        for (var i = 0; i < order.Count; i++)
        {
            var result = order[i];
            var item = new Button
            {
                Content = AccessKeyLabelFor(result),
                MinWidth = 92,
                Margin = new Thickness(8, 0, 0, 0),
                Padding = new Thickness(12, 4, 12, 4),
                // Numer tabulacji z pozycji LOGICZNEJ, nie z kolejnosci
                // dodawania - i zawsze WYZSZY niz tresc.
                TabIndex = 1 + i,
                IsDefault = i == 0,
                IsCancel = false,
            };

            AutomationProperties.SetName(item, LabelFor(result));

            var captured = result;
            item.Click += (_, _) =>
            {
                box.Value = captured;
                window.DialogResult = true;
            };

            buttonRow.Children.Add(item);
        }

        window.Content = root;

        window.PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape)
            {
                box.Value = CancelResultFor(button);
                window.DialogResult = true;
                e.Handled = true;
            }
        };

        // Fokus na TRESCI, nie na przycisku - to jest cala poprawka.
        window.Loaded += (_, _) =>
        {
            content.CaretIndex = 0;
            content.Focus();
            Keyboard.Focus(content);
        };

        return window;
    }

    private static int CountLines(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return 0;
        }

        var lines = 0;
        foreach (var character in text)
        {
            if (character == '\n')
            {
                lines++;
            }
        }

        // Grubo liczac zawijanie dlugich wierszy.
        return lines + (text.Length / 70);
    }
}
