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
            await Task.Delay(4000);
            IsUpdateNotificationVisible = false;
        }
    }

    [RelayCommand]
    public void DismissUpdateNotification()
    {
        IsUpdateNotificationVisible = false;
    }

    [RelayCommand]
    public async Task DownloadUpdateAsync()
    {
        ShowUpdateDownloadButton = false;
        UpdateNotificationMessage = "Downloading update...";
        try
        {
            string assetName = GetUpdateAssetName();
            string tempPath = Path.Combine(Path.GetTempPath(), assetName);

            // Download the update asset
            bool downloadSuccess = await _updateService.DownloadAsync(assetName, tempPath);
            if (!downloadSuccess)
            {
                UpdateNotificationMessage = "Failed to download update.";
                return;
            }

            // After download, prompt user to relaunch
            UpdateNotificationMessage = "Update downloaded. Relaunch application to install update.";
            ShowRelaunchButton = true;
            return;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to download update");
            UpdateNotificationMessage = "Failed to download update. See Log for details.";
        }
    }

    [RelayCommand]
    public async Task RelaunchAndInstallUpdateAsync()
    {
        ShowRelaunchButton = false;
        UpdateNotificationMessage = "Installing update...";
        try
        {
            string assetName = GetUpdateAssetName();
            string tempPath = Path.Combine(Path.GetTempPath(), assetName);
            await _updateService.InstallAsync(tempPath);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to install update");
            UpdateNotificationMessage = "Failed to install update. See Log for details.";
        }
    }

    // Returns the correct asset name for both download and install based on install type
    private string GetUpdateAssetName()
    {
        // return "WmiExplorer_2.0.0.2.zip"; //temporary hardcoded value for testing
        return IsPortable ? "WmiExplorer.Portable.exe" : "WmiExplorer.exe";
    }
}