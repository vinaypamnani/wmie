using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Management;
using System.Security;
using System.Windows;
using WmiExplorer.Common.Logging;

namespace WmiExplorer.Presentation.ViewModels.Dialogs;

/// <summary>
/// ViewModel for the ConnectionOptionsDialog that allows users to specify WMI connection parameters.
/// </summary>
public partial class ConnectionOptionsDialogViewModel : ObservableObject
{
    [ObservableProperty]
    private AuthenticationLevel _authentication = AuthenticationLevel.Default;

    [ObservableProperty]
    private string _authority = string.Empty;

    [ObservableProperty]
    private string _computerName = Environment.MachineName;

    [ObservableProperty]
    private bool _editContext = false;

    [ObservableProperty]
    private bool _enablePrivileges = true;

    [ObservableProperty]
    private ImpersonationLevel _impersonation = ImpersonationLevel.Impersonate;

    [ObservableProperty]
    private string _locale = string.Empty;

    [ObservableProperty]
    private string _newContextKey = string.Empty;

    [ObservableProperty]
    private string _newContextValue = string.Empty;

    [ObservableProperty]
    private string _password = string.Empty;

    [ObservableProperty]
    private TimeSpan _timeout = TimeSpan.MaxValue;

    [ObservableProperty]
    private string _username = string.Empty;

    private readonly Window _window;

    /// <summary>
    /// Initializes a new instance of the ConnectionOptionsDialogViewModel.
    /// </summary>
    /// <param name="window">The dialog window instance</param>
    /// <param name="existingOptions">Existing connection options to pre-populate the dialog (optional)</param>
    /// <param name="computerName">The computer name to connect to (optional)</param>
    public ConnectionOptionsDialogViewModel(Window window, ConnectionOptions? existingOptions = null, string? computerName = null)
    {
        _window = window ?? throw new ArgumentNullException(nameof(window));

        // Set computer name if provided
        if (!string.IsNullOrWhiteSpace(computerName))
        {
            ComputerName = computerName;
        }

        // Pre-populate with existing options if provided
        if (existingOptions != null)
        {
            Authentication = existingOptions.Authentication;
            Authority = existingOptions.Authority ?? string.Empty;
            EnablePrivileges = existingOptions.EnablePrivileges;
            Impersonation = existingOptions.Impersonation;
            Locale = !string.IsNullOrWhiteSpace(existingOptions.Locale) ? existingOptions.Locale : string.Empty;
            Username = existingOptions.Username ?? string.Empty;
            Timeout = existingOptions.Timeout != TimeSpan.MaxValue ? existingOptions.Timeout : TimeSpan.MaxValue;

            // Copy context items if they exist
            if (existingOptions.Context != null)
            {
                foreach (string key in existingOptions.Context)
                {
                    ContextItems.Add(new KeyValuePair<string, object>(key, existingOptions.Context[key]));
                }
            }

            // Note: We cannot retrieve the password from existing ConnectionOptions for security reasons
        }
    }

    /// <summary>
    /// Gets the computer name result. Only available after OK is clicked.
    /// </summary>
    public string? ComputerNameResult { get; private set; }

    /// <summary>
    /// Simple observable dictionary for context key-value pairs
    /// </summary>
    public ObservableCollection<KeyValuePair<string, object>> ContextItems { get; } = new ObservableCollection<KeyValuePair<string, object>>();

    /// <summary>
    /// Gets the connection options result. Only available after OK is clicked.
    /// </summary>
    public ConnectionOptions? Result { get; private set; }

    [RelayCommand]
    private void AddContextItem()
    {
        // Use the values from the textboxes if provided, otherwise use defaults
        var key = string.IsNullOrWhiteSpace(NewContextKey) ? $"Key{ContextItems.Count + 1}" : NewContextKey.Trim();
        var value = NewContextValue ?? string.Empty;

        // Check if key already exists
        var existingItemIndex = -1;
        for (int i = 0; i < ContextItems.Count; i++)
        {
            if (ContextItems[i].Key.Equals(key, StringComparison.OrdinalIgnoreCase))
            {
                existingItemIndex = i;
                break;
            }
        }

        if (existingItemIndex >= 0)
        {
            // Replace existing item
            ContextItems[existingItemIndex] = new KeyValuePair<string, object>(key, value);
        }
        else
        {
            // Add new item
            ContextItems.Add(new KeyValuePair<string, object>(key, value));
        }

        // Clear the input textboxes
        NewContextKey = string.Empty;
        NewContextValue = string.Empty;
    }

    [RelayCommand]
    private void Cancel()
    {
        Result = null; _window.DialogResult = false;
        _window.Close();
    }

    [RelayCommand]
    private void ClearContext()
    {
        ContextItems.Clear();
    }

    [RelayCommand]
    private void Connect()
    {
        try
        {
            // Create the ConnectionOptions object with the specified values
            var connectionOptions = new ConnectionOptions
            {
                Authentication = Authentication,
                Authority = string.IsNullOrWhiteSpace(Authority) ? null : Authority.Trim(),
                EnablePrivileges = EnablePrivileges,
                Impersonation = Impersonation,
                Locale = string.IsNullOrWhiteSpace(Locale) ? null : Locale.Trim(),
                Username = string.IsNullOrWhiteSpace(Username) ? null : Username.Trim(),
                Timeout = Timeout != TimeSpan.MaxValue ? Timeout : TimeSpan.MaxValue
            };

            // Create context from ContextItems if any exist
            if (ContextItems.Count > 0)
            {
                var context = new ManagementNamedValueCollection();
                foreach (var item in ContextItems)
                {
                    if (!string.IsNullOrWhiteSpace(item.Key))
                    {
                        context.Add(item.Key, item.Value);
                    }
                }
                connectionOptions.Context = context;
            }

            // Set the password if provided
            if (!string.IsNullOrWhiteSpace(Password))
            {
                // Convert password to SecureString for better security
                var securePassword = new SecureString();
                foreach (char c in Password)
                {
                    securePassword.AppendChar(c);
                }
                securePassword.MakeReadOnly();
                connectionOptions.SecurePassword = securePassword;
            }

            Result = connectionOptions;
            ComputerNameResult = string.IsNullOrWhiteSpace(ComputerName) ? Environment.MachineName : ComputerName.Trim();
            _window.DialogResult = true;
            _window.Close();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error creating connection options in ConnectionOptionsDialogViewModel");
            MessageBox.Show($"Error creating connection options: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    private void RemoveContextItem(KeyValuePair<string, object> item)
    {
        if (ContextItems.Contains(item))
        {
            ContextItems.Remove(item);
        }
    }

    [RelayCommand]
    private void Reset()
    {
        // Reset to default values
        Authentication = AuthenticationLevel.Default;
        Authority = string.Empty;
        EnablePrivileges = true;
        Impersonation = ImpersonationLevel.Impersonate;
        Locale = string.Empty;
        Password = string.Empty;
        Username = string.Empty;
        Timeout = TimeSpan.MaxValue;
        ComputerName = Environment.MachineName;

        // Clear context items
        ContextItems.Clear();
        EditContext = false;
        NewContextKey = string.Empty;
        NewContextValue = string.Empty;
    }
}