using System.Diagnostics;
using System.Management;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Navigation;
using WmiExplorer.Presentation.ViewModels.Dialogs;

namespace WmiExplorer.Presentation.Views.Dialogs;

/// <summary>
/// Dialog for configuring WMI connection options including authentication and credentials.
/// </summary>
public partial class ConnectionOptionsDialog : Window
{
    /// Creates a new ConnectionOptionsDialog.
    /// </summary>
    /// <param name="owner">The owner window</param>
    /// <param name="existingOptions">Existing connection options to pre-populate (optional)</param>
    /// <param name="computerName">The computer name to connect to (optional)</param>
    public ConnectionOptionsDialog(Window owner, ConnectionOptions? existingOptions = null, string? computerName = null)
    {
        InitializeComponent();
        Owner = owner ?? throw new ArgumentNullException(nameof(owner));
        ViewModel = new ConnectionOptionsDialogViewModel(this, existingOptions, computerName);
        DataContext = ViewModel;
    }

    /// <summary>
    /// Gets the computer name result after the dialog closes with OK.
    /// </summary>
    public string? ComputerNameResult => ViewModel.ComputerNameResult;

    /// <summary>
    /// Gets the connection options result after the dialog closes with OK.
    /// </summary>
    public ConnectionOptions? Result => ViewModel.Result;

    /// <summary>
    /// Gets the ViewModel for this dialog.
    /// </summary>
    public ConnectionOptionsDialogViewModel ViewModel { get; }

    /// <summary>
    /// Handles hyperlink navigation to open URLs in the default browser.
    /// </summary>
    private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = e.Uri.AbsoluteUri,
                UseShellExecute = true
            });
            e.Handled = true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ConnectionOptionsDialog] Error opening URL: {ex.Message}");
            // Silently fail - don't show error to user for link clicks
        }
    }

    /// <summary>
    /// Handles password changes from the PasswordBox control.
    /// PasswordBox doesn't support data binding for security reasons.
    /// </summary>
    private void PasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
    {
        if (sender is PasswordBox passwordBox && ViewModel != null)
        {
            ViewModel.Password = passwordBox.Password;
        }
    }
}