using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.IO;
using WmiExplorer.Common.Logging;
using WmiExplorer.Services;

namespace WmiExplorer.Presentation.ViewModels.Shared;

/// <summary>
/// Manages update state, commands, and UI notification for the application.
/// </summary>
public partial class UpdateManager : ObservableObject
{
    [ObservableProperty]
    private string _changelog = string.Empty;

    [ObservableProperty]
    private bool _isUpdateNotificationVisible;

    [ObservableProperty]
    private string _latestVersion = string.Empty;

    [ObservableProperty]
    private bool _showRelaunchButton;

    [ObservableProperty]
    private bool _showUpdateDownloadButton;

    private bool _updateDownloaded = false;

    [ObservableProperty]
    private string _updateNotificationMessage = string.Empty;

    private readonly UpdateService _updateService;

    public UpdateManager(UpdateService updateService)
    {
        _updateService = updateService;
    }

    public bool IsPortable => _updateService.IsPortable;

    [RelayCommand]
    public async Task CheckForUpdatesAsync()
    {
        var currentVersion = WmiExplorer.VersionInfo.AppVersion;
        var (isUpdateAvailable, latestVersion, changelog) = await _updateService.CheckForUpdateAsync(currentVersion);
        LatestVersion = latestVersion;
        Changelog = changelog;
        if (isUpdateAvailable)
        {
            Log.Information($"Update available! Current: {currentVersion}, Latest: {latestVersion}");
            UpdateNotificationMessage = $"A new version ({latestVersion}) is available!";
            ShowUpdateDownloadButton = true;
            IsUpdateNotificationVisible = true;
        }
        else
        {
            Log.Information($"No update available. Current version: {currentVersion}, Latest version: {latestVersion}");
            UpdateNotificationMessage = "No update available. You are up to date.";
            ShowUpdateDownloadButton = false;
            IsUpdateNotificationVisible = true;
            await Task.Delay(5000);
            IsUpdateNotificationVisible = false;
        }
    }

    [RelayCommand]
    public void DismissUpdateNotification()
    {
        IsUpdateNotificationVisible = false;
        if (_updateDownloaded)
        {
            var (_, tempPath) = GetUpdateAssetInfo();
            Log.Information($"Update was downloaded to '{tempPath}' but notification was dismissed. Please replace the downloaded file manually if you wish to update.");
            _updateDownloaded = false;
        }
    }

    [RelayCommand]
    public async Task DownloadUpdateAsync()
    {
        ShowUpdateDownloadButton = false;
        UpdateNotificationMessage = "Downloading update...";
        try
        {
            var (assetName, tempPath) = GetUpdateAssetInfo();

            // Download the update asset
            bool downloadSuccess = await _updateService.DownloadAsync(assetName, tempPath);
            if (!downloadSuccess)
            {
                UpdateNotificationMessage = "Failed to download update.";
                _updateDownloaded = false;
                return;
            }

            // After download, prompt user to relaunch
            UpdateNotificationMessage = "Update downloaded. Relaunch application to install update.";
            ShowRelaunchButton = true;
            _updateDownloaded = true;
            return;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to download update");
            UpdateNotificationMessage = "Failed to download update. See Log for details.";
            _updateDownloaded = false;
        }
    }

    [RelayCommand]
    public async Task RelaunchAndInstallUpdateAsync()
    {
        ShowRelaunchButton = false;
        UpdateNotificationMessage = "Installing update...";
        try
        {
            var (_, tempPath) = GetUpdateAssetInfo();
            bool installed = await _updateService.InstallAsync(tempPath);
            if (!installed)
            {
                UpdateNotificationMessage = "Failed to install update. Review Logs.";
                return;
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to install update");
            UpdateNotificationMessage = "Failed to install update. See Log for details.";
        }
    }

    // Returns the correct asset name and temp path for both download and install
    private (string assetName, string tempPath) GetUpdateAssetInfo()
    {
        string assetName = IsPortable ? "WmiExplorer.Portable.exe" : "WmiExplorer_2.0.0.2.zip";
        string tempPath = Path.Combine(Path.GetTempPath(), assetName);
        return (assetName, tempPath);
    }
}