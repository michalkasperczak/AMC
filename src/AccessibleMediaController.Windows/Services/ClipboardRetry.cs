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
        var dataDescription = DescribeData(data);
        for (var attempt = 0; attempt < RetryDelaysMilliseconds.Length; attempt++)
        {
            var delay = RetryDelaysMilliseconds[attempt];
            if (delay > 0) Thread.Sleep(delay);
            try
            {
                Clipboard.SetDataObject(data, true);
                DiagnosticLog.Info(
                    "clipboard",
                    attempt == 0
                        ? $"Zapisano schowek: {dataDescription}."
                        : $"Zapisano schowek po {attempt + 1} próbach: {dataDescription}.");
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
        DiagnosticLog.Error(
            "clipboard",
            $"Nie udało się zapisać schowka po {RetryDelaysMilliseconds.Length} próbach: {dataDescription}.",
            lastError);
        return false;
    }

    private static string DescribeData(object data)
    {
        if (data is not IDataObject dataObject) return data.GetType().Name;
        try
        {
            var formats = dataObject.GetFormats(false);
            return formats.Length == 0 ? "brak formatów" : string.Join(", ", formats);
        }
        catch (Exception exception) when (exception is ExternalException or InvalidOperationException)
        {
            return data.GetType().Name;
        }
    }
}
