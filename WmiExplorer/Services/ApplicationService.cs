using System.Windows;

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
            System.Diagnostics.Debug.WriteLine($"[ApplicationService] Error copying to clipboard: {ex.Message}");
        }
    }
}