using System.Windows;
using WmiExplorer.Common.Logging;

namespace WmiExplorer.Services;

/// <summary>
/// Implementation of the clipboard service
/// </summary>
public class ApplicationService : IApplicationService
{
    /// <summary>
    /// Copies text to the clipboard
    /// </summary>
    public void CopyToClipboard(string text)
    {
        if (string.IsNullOrEmpty(text))
            return;

        try
        {
            Clipboard.SetText(text);
        }
        catch (Exception ex)
        {
            // Log or handle the exception as needed
            Log.Warning(ex, "Error copying text '{Text}' to clipboard", text.Length > 50 ? text.Substring(0, 50) + "..." : text);
        }
    }
}