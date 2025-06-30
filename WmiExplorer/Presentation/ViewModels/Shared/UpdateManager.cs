using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.IO;
using System.Windows.Threading;
using WmiExplorer.Common.Logging;
using WmiExplorer.Services;

namespace WmiExplorer.Presentation.ViewModels.Shared;

/// <summary>
/// Manages update state, commands, and UI notification for the application.
/// </summary>
public partial class UpdateManager : ObservableObject
{
    private const int DefaultDismissSeconds = 10;

    [ObservableProperty]
    private string _changelog = string.Empty;

    [ObservableProperty]
    private string _dismissButtonText = "Dismiss";

    private int _dismissCountdownSeconds;

    // Countdown timer for auto-dismiss
    private DispatcherTimer? _dismissTimer;

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
        try
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
                StopDismissTimer();
            }
            else
            {
                Log.Information($"No update available. Current version: {currentVersion}, Latest version: {latestVersion}");
                UpdateNotificationMessage = "No update available. You are up to date!";
                ShowUpdateDownloadButton = false;
                IsUpdateNotificationVisible = true;
                StartDismissTimer(DefaultDismissSeconds);
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to check for updates");
            UpdateNotificationMessage = "Failed to check for updates. See Log for details.";
            ShowUpdateDownloadButton = false;
            IsUpdateNotificationVisible = true;
            StartDismissTimer(DefaultDismissSeconds);
        }
    }

    [RelayCommand]
    public void DismissUpdateNotification()
    {
        IsUpdateNotificationVisible = false;
        StopDismissTimer();
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
    public void OpenReleaseUrl()
    {
        try
        {
            var url = _updateService.GitReleaseUrl;
            if (!string.IsNullOrEmpty(url))
            {
                // Open the release URL in the default browser
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true
                });
            }
            else
            {
                Log.Warning("GitReleaseUrl is null or empty. Cannot open release page.");
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to launch release URL.");
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

    [RelayCommand]
    public void ShowChangelog()
    {
        try
        {
            var url = _updateService.GitLatestReleaseUrl;
            if (!string.IsNullOrEmpty(url))
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true
                });
            }
            else
            {
                Log.Warning("GitReleaseUrl is null or empty. Cannot open changelog.");
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to launch changelog URL.");
        }
    }

    private void DismissTimer_Tick(object? sender, EventArgs e)
    {
        _dismissCountdownSeconds--;
        UpdateDismissButtonText();
        if (_dismissCountdownSeconds <= 0)
        {
            StopDismissTimer();
            IsUpdateNotificationVisible = false;
        }
    }

    // Returns the correct asset name and temp path for both download and install
    private (string assetName, string tempPath) GetUpdateAssetInfo()
    {
        string assetName = IsPortable ? "WmiExplorer.Portable.exe" : "WmiExplorer.exe";
        string tempPath = Path.Combine(Path.GetTempPath(), assetName);
        return (assetName, tempPath);
    }

    // Start the countdown timer for auto-dismiss
    private void StartDismissTimer(int seconds)
    {
        StopDismissTimer();
        _dismissCountdownSeconds = seconds;
        UpdateDismissButtonText();
        _dismissTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _dismissTimer.Tick += DismissTimer_Tick;
        _dismissTimer.Start();
    }

    private void StopDismissTimer()
    {
        if (_dismissTimer != null)
        {
            _dismissTimer.Stop();
            _dismissTimer.Tick -= DismissTimer_Tick;
            _dismissTimer = null;
        }
        DismissButtonText = "Dismiss";
    }

    private void UpdateDismissButtonText()
    {
        if (_dismissCountdownSeconds > 0)
            DismissButtonText = $"Dismiss ({_dismissCountdownSeconds})";
        else
            DismissButtonText = "Dismiss";
    }
}