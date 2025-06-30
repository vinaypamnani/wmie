using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Windows;
using WmiExplorer.Common.Logging;

namespace WmiExplorer.Services;

/// <summary>
/// Service to check for updates and fetch changelog from GitHub releases.
/// </summary>
public class UpdateService
{
    private const string GitHubApiLatestReleaseUrl = "https://api.github.com/repos/{0}/{1}/releases/latest";

    /// <summary>
    /// Indicates if the current executable is considered portable (file size over 25MB).
    /// </summary>
    public readonly bool IsPortable;

    private readonly TimeSpan _cacheDuration = TimeSpan.FromMinutes(5);
    private readonly HttpClient _httpClient;
    private DateTime _latestReleaseFetchedAt;

    // Helper to fetch and cache the latest release JSON
    private JsonDocument? _latestReleaseJson;

    private readonly string _owner;

    // Holds the latest release document for the current update workflow
    private JsonDocument? _releaseDoc;

    private readonly string _repo;

    public UpdateService(string owner, string repo)
    {
        _owner = owner;
        _repo = repo;
        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("WmiExplorerUpdateService");
        IsPortable = IsCurrentExePortable();
    }

    /// <summary>
    /// Checks if an update is available compared to the current version.
    /// </summary>
    public async Task<(bool isUpdateAvailable, string latestVersion, string changelog)> CheckForUpdateAsync(string currentVersion)
    {
        try
        {
            _releaseDoc = await GetLatestReleaseJsonAsync();
            if (_releaseDoc == null)
            {
                Log.Error("Failed to retrieve latest release info.");
                return (false, string.Empty, string.Empty);
            }

            // Extract the tag name and changelog from the JSON response
            var tag = _releaseDoc.RootElement.GetProperty("tag_name").GetString();
            // Remove leading 'v' if present in tag
            var latestVersion = !string.IsNullOrEmpty(tag) && tag.StartsWith("v", StringComparison.OrdinalIgnoreCase)
                ? tag.Substring(1)
                : tag;
            var changelog = _releaseDoc.RootElement.GetProperty("body").GetString();
            // Compare versions using Version.TryParse for proper semantic comparison
            bool isUpdateAvailable = false;
            if (!string.IsNullOrEmpty(currentVersion) && !string.IsNullOrEmpty(latestVersion))
            {
                if (Version.TryParse(currentVersion, out var current) && Version.TryParse(latestVersion, out var latest))
                {
                    isUpdateAvailable = latest > current;
                }
                else
                {
                    // Fallback to string comparison if parsing fails
                    Log.Warning($"Version parse failed, falling back to string comparison. currentVersion='{currentVersion}', latestVersion='{latestVersion}'");
                    isUpdateAvailable = !string.Equals(currentVersion, latestVersion, StringComparison.OrdinalIgnoreCase);
                }
            }
            return (isUpdateAvailable, latestVersion ?? string.Empty, changelog ?? string.Empty);
        }
        catch (HttpRequestException ex)
        {
            Log.Error(ex, "HTTP request failed during update check");
            return (false, string.Empty, string.Empty);
        }
        catch (JsonException ex)
        {
            Log.Error(ex, "JSON parsing failed during update check");
            return (false, string.Empty, string.Empty);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Exception occurred during update check");
            return (false, string.Empty, string.Empty);
        }
    }

    /// <summary>
    /// Downloads the specified asset from the latest GitHub release to the given destination path.
    /// </summary>
    public async Task<bool> DownloadAsync(string assetName, string destinationPath)
    {
        try
        {
            var doc = _releaseDoc ?? await GetLatestReleaseJsonAsync();
            if (doc == null)
            {
                Log.Error($"Failed to retrieve latest release info for asset '{assetName}'.");
                return false;
            }

            // Find the asset with the specified name
            var assets = doc.RootElement.GetProperty("assets");
            foreach (var asset in assets.EnumerateArray())
            {
                var name = asset.GetProperty("name").GetString();
                if (string.Equals(name, assetName, StringComparison.OrdinalIgnoreCase))
                {
                    var downloadUrl = asset.GetProperty("browser_download_url").GetString();
                    if (!string.IsNullOrEmpty(downloadUrl))
                    {
                        // Download the asset
                        using var assetResponse = await _httpClient.GetAsync(downloadUrl);
                        assetResponse.EnsureSuccessStatusCode();
                        using var fs = File.Create(destinationPath);
                        await assetResponse.Content.CopyToAsync(fs);
                        Log.Information($"Downloaded asset '{assetName}' to '{destinationPath}'.");
                        return true;
                    }
                }
            }
            Log.Error($"Asset '{assetName}' not found in the latest release.");
            return false;
        }
        catch (Exception ex)
        {
            Log.Error(ex, $"Failed to download asset '{assetName}'.");
            return false;
        }
    }

    /// <summary>
    /// Installs the downloaded executable by replacing the current running exe and relaunching the app.
    /// </summary>
    public async Task<bool> InstallAsync(string newExePath)
    {
        try
        {
            var currentExe = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName;
            if (string.IsNullOrEmpty(currentExe) || !File.Exists(newExePath))
            {
                Log.Error($"Current exe or new exe path invalid. currentExe='{currentExe}', newExePath='{newExePath}'");
                return false;
            }
            var backupExe = currentExe + ".remove";
            var batchPath = Path.Combine(Path.GetTempPath(), $"WmiExplorerUpdater_{Guid.NewGuid()}.bat");
            // Prepare batch file content
            var batchContent = $"@echo off\r\n" +
                ":loop\r\n" +
                $"tasklist | find /i \"{Path.GetFileName(currentExe)}\" >nul 2>&1\r\n" +
                "if not errorlevel 1 (\r\n" +
                "    timeout /t 1 >nul\r\n" +
                "    goto loop\r\n" +
                ")\r\n" +
                $"move /y \"{currentExe}\" \"{backupExe}\"\r\n" +
                $"move /y \"{newExePath}\" \"{currentExe}\"\r\n" +
                $"start \"\" \"{currentExe}\"\r\n" +
                $"del \"{backupExe}\"\r\n" +
                "del \"%~f0\"\r\n";
            await File.WriteAllTextAsync(batchPath, batchContent);

            // Launch the batch file and exit
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = batchPath,
                UseShellExecute = false,
                CreateNoWindow = true
            });
            Log.Information($"Update installed. Current exe will be replaced with '{newExePath}' and application will restart.");
            Application.Current.MainWindow?.Close(); // Close the main window to allow the batch file to run
            return true; // This line is not reached, but included for completeness
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to install update.");
            return false;
        }
    }

    /// <summary>
    /// Gets the latest release JSON from GitHub, with simple in-memory caching.
    /// </summary>
    private async Task<JsonDocument?> GetLatestReleaseJsonAsync()
    {
        // Use cached value if it's still fresh
        if (_latestReleaseJson != null && (DateTime.UtcNow - _latestReleaseFetchedAt) < _cacheDuration)
            return _latestReleaseJson;
        try
        {
            var url = string.Format(GitHubApiLatestReleaseUrl, _owner, _repo);
            var response = await _httpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadAsStringAsync();
            _latestReleaseJson?.Dispose();
            _latestReleaseJson = JsonDocument.Parse(json);
            _latestReleaseFetchedAt = DateTime.UtcNow;
            return _latestReleaseJson;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to fetch latest release JSON.");
            return null;
        }
    }

    /// <summary>
    /// Checks if the current executable is considered portable based on its file size.
    /// A portable executable contains full .NET runtime and is larger than 50MB.
    /// </summary>
    private bool IsCurrentExePortable()
    {
        var exePath = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName;
        if (!string.IsNullOrEmpty(exePath) && File.Exists(exePath))
        {
            long fileSize = new FileInfo(exePath).Length;
            return fileSize > 50 * 1024 * 1024; // 50MB in bytes
        }
        return false;
    }
}