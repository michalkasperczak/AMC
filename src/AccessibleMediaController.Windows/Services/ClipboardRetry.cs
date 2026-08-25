using System.Runtime.InteropServices;
using System.Windows;

namespace AccessibleMediaController.Windows.Services;

internal static class ClipboardRetry
{
    private static readonly int[] RetryDelaysMilliseconds = [0, 20, 40, 80, 160, 250];

    public static bool TrySetText(string text, out string errorMessage)
    {
        var data = new DataObject();
        data.SetData(DataFormats.UnicodeText, text);
        return TrySetDataObject(data, out errorMessage);
    }

    public static bool TrySetDataObject(object data, out string errorMessage)
    {
        ArgumentNullException.ThrowIfNull(data);
        Exception? lastError = null;
        foreach (var delay in RetryDelaysMilliseconds)
        {
            if (delay > 0) Thread.Sleep(delay);
            try
            {
                Clipboard.SetDataObject(data, true);
                errorMessage = string.Empty;
                return true;
            }
            catch (Exception exception) when (exception is ExternalException or InvalidOperationException)
            {
                lastError = exception;
            }
        }

        var code = lastError is ExternalException external
            ? $" Kod 0x{external.ErrorCode:X8}."
            : string.Empty;
        errorMessage = $"Schowek jest zajęty przez inną aplikację. Spróbuj ponownie.{code}";
        return false;
    }
}
