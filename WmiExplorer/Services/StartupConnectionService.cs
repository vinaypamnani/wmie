using Microsoft.Extensions.DependencyInjection;
using System.Management;
using System.Security.Principal;
using System.Windows;
using WmiExplorer.Common.Helpers;
using WmiExplorer.Common.Logging;
using WmiExplorer.Presentation.ViewModels.Coordinators;
using WmiExplorer.Presentation.Views.Dialogs;

namespace WmiExplorer.Services;

/// <summary>
/// Service to handle automatic WMI connections based on command-line arguments.
/// </summary>
public class StartupConnectionService : IStartupConnectionService
{
    private readonly IServiceProvider _serviceProvider;

    public StartupConnectionService(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
    }

    /// <summary>
    /// Handles command-line arguments for automatic WMI connection.
    /// </summary>
    public void HandleCommandLineConnection(string[] args, Window mainWindow)
    {
        var computerName = CommandLineArgumentsParser.GetValue(args, "computername");
        var userName = CommandLineArgumentsParser.GetValue(args, "username");

        Log.Debug($"Command-line connection args parsed: computername='{computerName}', username='{userName}'");

        // If no connection arguments provided, do nothing
        if (string.IsNullOrWhiteSpace(computerName))
        {
            Log.Debug("No computername provided in command-line arguments, skipping auto-connect");
            return;
        }

        Log.Information($"Auto-connect requested: computername='{computerName}', username='{userName ?? "(current user)"}'");

        // Execute connection logic after window is loaded
        if (mainWindow.IsLoaded)
        {
            Log.Debug("MainWindow already loaded, executing connection immediately");
            // Window is already loaded, execute immediately
            _ = ExecuteConnectionAsync(computerName, userName, mainWindow);
        }
        else
        {
            Log.Debug("MainWindow not yet loaded, waiting for Loaded event");
            // Window not loaded yet, wait for Loaded event
            mainWindow.Loaded += async (s, e) =>
            {
                Log.Debug("MainWindow Loaded event fired, executing connection");
                await ExecuteConnectionAsync(computerName, userName, mainWindow);
            };
        }
    }

    /// <summary>
    /// Executes the connection logic based on command-line arguments.
    /// </summary>
    private async Task ExecuteConnectionAsync(string computerName, string? userName, Window mainWindow)
    {
        try
        {
            // Small delay to ensure window is fully initialized
            await Task.Delay(100);

            var optionsViewModel = _serviceProvider.GetRequiredService<OptionsViewModel>();
            var namespacesViewModel = _serviceProvider.GetRequiredService<NamespacesViewModel>();

            // Case 1: Only computername provided - connect immediately with current user
            if (string.IsNullOrWhiteSpace(userName))
            {
                Log.Information($"Connecting to {computerName} using current user credentials (from command line)");

                // Create default connection options (uses current user)
                var connectionOptions = new ConnectionOptions
                {
                    EnablePrivileges = true,
                    Impersonation = ImpersonationLevel.Impersonate,
                    Authentication = AuthenticationLevel.Default,
                    Username = null,
                    SecurePassword = null
                };

                // Update OptionsViewModel
                optionsViewModel.ComputerName = computerName;
                optionsViewModel.ConnectionOptions = connectionOptions;

                // Connect
                await namespacesViewModel.ConnectAsync(computerName, connectionOptions);
            }
            // Case 2: Both computername and username provided
            else
            {
                var currentUser = GetCurrentUsername();

                // Check if username matches current user
                if (IsSameUser(userName, currentUser))
                {
                    Log.Information($"Connecting to {computerName} as {userName} (matches current user, from command line)");

                    // Create connection options with username (no password needed for same user)
                    var connectionOptions = new ConnectionOptions
                    {
                        EnablePrivileges = true,
                        Impersonation = ImpersonationLevel.Impersonate,
                        Authentication = AuthenticationLevel.Default,
                        Username = userName,
                        SecurePassword = null
                    };

                    // Update OptionsViewModel
                    optionsViewModel.ComputerName = computerName;
                    optionsViewModel.ConnectionOptions = connectionOptions;

                    // Connect
                    await namespacesViewModel.ConnectAsync(computerName, connectionOptions);
                }
                else
                {
                    // Different user - show connection dialog for password
                    Log.Information($"Showing connection dialog for {computerName} as {userName} (different from current user, from command line)");

                    // Create connection options with username pre-filled
                    var existingOptions = new ConnectionOptions
                    {
                        EnablePrivileges = true,
                        Impersonation = ImpersonationLevel.Impersonate,
                        Authentication = AuthenticationLevel.Default,
                        Username = userName,
                        SecurePassword = null
                    };

                    // Show dialog with pre-filled values
                    var dialog = new ConnectionOptionsDialog(mainWindow, existingOptions, computerName);
                    var result = dialog.ShowDialog();

                    if (result == true && dialog.Result != null)
                    {
                        // Update OptionsViewModel
                        optionsViewModel.ConnectionOptions = dialog.Result;
                        if (!string.IsNullOrWhiteSpace(dialog.ComputerNameResult))
                        {
                            optionsViewModel.ComputerName = dialog.ComputerNameResult;
                        }

                        // Connect using the provided credentials
                        await namespacesViewModel.ConnectAsync(
                            dialog.ComputerNameResult ?? computerName,
                            dialog.Result);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error handling command-line connection arguments");
            // Error is already logged, connection failures are handled by NamespacesViewModel
        }
    }

    /// <summary>
    /// Gets the current Windows username.
    /// </summary>
    private static string GetCurrentUsername()
    {
        try
        {
            using var identity = WindowsIdentity.GetCurrent();
            return identity.Name;
        }
        catch
        {
            // Fallback to Environment.UserName if WindowsIdentity fails
            return Environment.UserName;
        }
    }

    /// <summary>
    /// Compares two usernames, handling different formats (DOMAIN\user vs user).
    /// Returns true if they represent the same user.
    /// </summary>
    private static bool IsSameUser(string? username1, string? username2)
    {
        if (string.IsNullOrWhiteSpace(username1) && string.IsNullOrWhiteSpace(username2))
            return true;

        if (string.IsNullOrWhiteSpace(username1) || string.IsNullOrWhiteSpace(username2))
            return false;

        // Normalize usernames by extracting just the username portion
        string NormalizeUsername(string username)
        {
            // Remove domain prefix if present (DOMAIN\username -> username)
            var parts = username.Split('\\');
            return parts[parts.Length - 1].Trim();
        }

        var normalized1 = NormalizeUsername(username1);
        var normalized2 = NormalizeUsername(username2);

        // Case-insensitive comparison
        return string.Equals(normalized1, normalized2, StringComparison.OrdinalIgnoreCase);
    }
}