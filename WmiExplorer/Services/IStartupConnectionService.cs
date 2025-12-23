namespace WmiExplorer.Services;

/// <summary>
/// Service to handle automatic WMI connections based on command-line arguments.
/// </summary>
public interface IStartupConnectionService
{
    /// <summary>
    /// Handles command-line arguments for automatic WMI connection.
    /// </summary>
    /// <param name="args">Command-line arguments</param>
    /// <param name="mainWindow">The main window instance</param>
    void HandleCommandLineConnection(string[] args, System.Windows.Window mainWindow);
}