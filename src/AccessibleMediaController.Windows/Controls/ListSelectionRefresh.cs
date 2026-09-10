namespace AccessibleMediaController.Windows.Controls;

// ItemsSource replacement temporarily clears WPF selection. Treat a synchronous
// refresh as one change of logical selection, not as navigation away and back.
// Do not use this scope for user navigation or keep it open across an await.
internal sealed class ListSelectionRefresh
{
    private int depth;

    internal void SelectionChanged(Action invalidate)
    {
        if (depth == 0) invalidate();
    }

    internal void Run(Func<string?> selectedId, Action refresh, Action invalidate)
    {
        var previousId = selectedId();
        depth++;
        try
        {
            refresh();
        }
        finally
        {
            depth--;
            if (depth == 0 && !string.Equals(previousId, selectedId(), StringComparison.Ordinal))
                invalidate();
        }
    }
}
