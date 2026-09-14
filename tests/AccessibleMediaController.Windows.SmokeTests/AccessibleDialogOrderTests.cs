using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using AccessibleMediaController.Windows.Services;

/// <summary>
/// Pomiar kolejnosci odczytu w okienkach dialogowych.
///
/// SEDNO PROBLEMU: systemowy MessageBox.Show ustawia fokus startowy na
/// przycisku, a tresc jest tylko statycznym napisem. NVDA czyta wtedy
/// najpierw "Tak", a pytania uzytkownik nie slyszy wcale. Przy pytaniu
/// "przerwac nagrywanie?" znaczy to, ze niewidomy uzytkownik odpowiada na
/// pytanie, ktorego nie zna.
///
/// Te testy pilnuja trzech rzeczy naraz, bo kazda z osobna nie wystarcza:
/// 1) fokus startowy stoi na TRESCI, nie na przycisku,
/// 2) tresc ma NIZSZY numer tabulacji niz przyciski (inaczej Tab wraca
///    do przyciskow zamiast isc dalej po tresci),
/// 3) zamkniecie okna bez wyboru daje odpowiedz BEZPIECZNA, bo Alt+F4
///    i Escape to u niewidomego uzytkownika czesty odruch.
/// </summary>
internal static class AccessibleDialogOrderTests
{
    internal static void Run()
    {
        Exception? failure = null;
        var uiThread = new Thread(() =>
        {
            try
            {
                TestFocusOrderPutsContentFirst();
                TestContentIsReadableAndNamed();
                TestClosingWithoutChoiceIsSafe();
                TestEnterDoesNotTriggerUnwantedAction();
            }
            catch (Exception exception) { failure = exception; }
            finally
            {
                // Okna nigdy nie byly pokazane, wiec nie ma czego zamykac -
                // ale watek STA zostawia po sobie zywy Dispatcher. Bez tego
                // proces pada przy sprzataniu okien WPF.
                System.Windows.Threading.Dispatcher.CurrentDispatcher.InvokeShutdown();
            }
        });
        uiThread.SetApartmentState(ApartmentState.STA);
        uiThread.Start();
        uiThread.Join();
        if (failure is not null)
        {
            throw new InvalidOperationException("Kolejnosc odczytu w okienkach", failure);
        }

        Console.WriteLine("OK: kolejnosc odczytu w okienkach - tresc przed przyciskami, bezpieczna odpowiedz domyslna");
    }

    /// <summary>
    /// Warunek 1 i 2: tresc pierwsza w tabulacji. To jest cala roznica
    /// miedzy uslyszeniem "Tak" a "Trwa nagrywanie. Przerwac?".
    /// </summary>
    private static void TestFocusOrderPutsContentFirst()
    {
        const string message = "Trwa nagrywanie. Przerwac i zamknac program?";
        var window = AccessibleDialog.CreateForMeasurement(
            message, "Nagrywanie", MessageBoxButton.YesNo);

        var content = FindContentBox(window);
        Check(content is not null, "pole z trescia istnieje");
        Check(content!.Text == message, "pole nosi pelna tresc pytania");

        var buttons = FindButtons(window);
        Check(buttons.Count == 2, $"dwa przyciski dla YesNo, jest {buttons.Count}");

        foreach (var button in buttons)
        {
            Check(button.TabIndex > content.TabIndex,
                $"przycisk '{button.Content}' ma numer tabulacji WYZSZY niz tresc " +
                $"(tresc {content.TabIndex}, przycisk {button.TabIndex})");
        }

    }

    /// <summary>
    /// Tresc musi byc polem tekstowym, a nie napisem: tylko wtedy da sie ja
    /// przeczytac ponownie strzalkami i skopiowac, nie zamykajac okna.
    /// </summary>
    private static void TestContentIsReadableAndNamed()
    {
        var window = AccessibleDialog.CreateForMeasurement(
            "Zainstalowac aktualizacje teraz?", "Aktualizacja", MessageBoxButton.YesNo);

        var content = FindContentBox(window);
        Check(content is not null, "pole z trescia istnieje");
        Check(content!.IsReadOnly, "tresci nie da sie przypadkiem nadpisac");
        Check(content.IsReadOnlyCaretVisible,
            "kursor widoczny, wiec strzalki czytaja tresc ponownie");
        Check(AutomationProperties.GetName(content) == AccessibleDialog.ContentAutomationName,
            "pole ma nazwe dla czytnika");

        foreach (var button in FindButtons(window))
        {
            var name = AutomationProperties.GetName(button);
            Check(!string.IsNullOrWhiteSpace(name), "przycisk ma nazwe dla czytnika");
        }

    }

    /// <summary>
    /// Warunek 3: Alt+F4, Escape i krzyzyk nie moga znaczyc "tak".
    /// </summary>
    private static void TestClosingWithoutChoiceIsSafe()
    {
        var yesNo = AccessibleDialog.CreateForMeasurement(
            "Przerwac nagrywanie?", "Nagrywanie", MessageBoxButton.YesNo);
        Check(AccessibleDialog.PeekResult(yesNo) == MessageBoxResult.No,
            "zamkniecie okna YesNo bez wyboru = Nie");

        var yesNoCancel = AccessibleDialog.CreateForMeasurement(
            "Zapisac zmiany?", "Zapis", MessageBoxButton.YesNoCancel);
        Check(AccessibleDialog.PeekResult(yesNoCancel) == MessageBoxResult.Cancel,
            "zamkniecie okna YesNoCancel bez wyboru = Anuluj");

        var okCancel = AccessibleDialog.CreateForMeasurement(
            "Usunac plik?", "Usuwanie", MessageBoxButton.OKCancel);
        Check(AccessibleDialog.PeekResult(okCancel) == MessageBoxResult.Cancel,
            "zamkniecie okna OKCancel bez wyboru = Anuluj");
    }

    /// <summary>
    /// Pod Enterem stoi pierwszy przycisk. Gdy wywolujacy zaznaczy, ze
    /// domyslna odpowiedz to "nie", odruchowy Enter nie moze wykonac
    /// rzeczy, ktorej uzytkownik nie chcial.
    /// </summary>
    private static void TestEnterDoesNotTriggerUnwantedAction()
    {
        var window = AccessibleDialog.CreateForMeasurement(
            "Skasowac nagranie?", "Nagrywanie", MessageBoxButton.YesNo, MessageBoxResult.No);

        var buttons = FindButtons(window);
        Check(buttons.Count == 2, "dwa przyciski");

        var defaultButton = buttons.Find(b => b is Button { IsDefault: true });
        Check(defaultButton is not null, "jakis przycisk jest domyslny");
        Check(AutomationProperties.GetName(defaultButton!) == "Nie",
            "gdy domyslna odpowiedz to Nie, pod Enterem stoi Nie " +
            $"(stoi: '{AutomationProperties.GetName(defaultButton!)}')");

    }

    private static TextBox? FindContentBox(DependencyObject root)
    {
        foreach (var child in Descendants(root))
        {
            if (child is TextBox box) { return box; }
        }

        return null;
    }

    private static List<ButtonBase> FindButtons(DependencyObject root)
    {
        var found = new List<ButtonBase>();
        foreach (var child in Descendants(root))
        {
            if (child is ButtonBase button) { found.Add(button); }
        }

        return found;
    }

    /// <summary>
    /// Okno nie bylo pokazane, wiec drzewo wizualne jeszcze nie istnieje -
    /// chodzimy po drzewie LOGICZNYM, ktore jest gotowe od razu.
    /// </summary>
    private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
    {
        var pending = new Queue<DependencyObject>();
        pending.Enqueue(root);
        while (pending.Count > 0)
        {
            var current = pending.Dequeue();
            foreach (var child in LogicalTreeHelper.GetChildren(current))
            {
                if (child is DependencyObject node)
                {
                    yield return node;
                    pending.Enqueue(node);
                }
            }
        }
    }

    private static void Check(bool condition, string what)
    {
        if (!condition)
        {
            throw new InvalidOperationException($"Nie spelnione: {what}");
        }
    }
}
