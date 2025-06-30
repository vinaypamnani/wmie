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
    private const string GitHubLatestReleaseUrl = "https://github.com/{0}/{1}/releases/latest";
    private const string GitHubReleaseUrl = "https://github.com/{0}/{1}/releases";

    public readonly string GitApiReleaseUrl;
    public readonly string GitLatestReleaseUrl;
    public readonly string GitReleaseUrl;

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

        GitApiReleaseUrl = string.Format(GitHubApiLatestReleaseUrl, _owner, _repo);
        GitLatestReleaseUrl = string.Format(GitHubLatestReleaseUrl, _owner, _repo);
        GitReleaseUrl = string.Format(GitHubReleaseUrl, _owner, _repo);
        IsPortable = IsCurrentExePortable();
    }

    /// <summary>
    /// Checks if an update is available compared to the current version.
    /// </summary>
    public async Task<(bool isUpdateAvailable, string latestVersion, string changelog)> CheckForUpdateAsync(string currentVersion)
    {
        // Let exceptions bubble up to the caller for proper error handling in the UI
        _releaseDoc = await GetLatestReleaseJsonAsync();
        if (_releaseDoc == null)
        {
            Log.Error("Failed to retrieve latest release info.");
            throw new InvalidOperationException("Failed to retrieve latest release info.");
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
    /// Uses a direct file move/rename approach, with batch file fallback.
    /// </summary>
    public async Task<bool> InstallAsync(string newExePath)
    {
        string localCurrentFile = Environment.ProcessPath!;

        // Bail early if in a protected location
        if (IsProtectedLocation(localCurrentFile))
        {
            Log.Error($"Update cannot proceed: application is running from a protected location: '{localCurrentFile}'. Manually update the application by replacing the executable using '{newExePath}'.");
            return false;
        }

        try
        {
            // Try direct file move approach
            bool directResult = await InstallUsingDirectFileMoveAsync(newExePath, localCurrentFile);
            if (directResult)
                return true;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Direct file move update failed, falling back to batch file updater.");
        }
        // Fallback to batch file approach
        return await InstallUsingBatchAsync(newExePath);
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
            var response = await _httpClient.GetAsync(GitApiReleaseUrl);
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
    /// Installs the update using a batch file as a backup method.
    /// </summary>
    private async Task<bool> InstallUsingBatchAsync(string newExePath)
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
            Log.Information($"Update installed using batch file. Current exe will be replaced with '{newExePath}' and application will restart.");
            Application.Current.MainWindow?.Close();
            return true;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to install update using batch file.");
            return false;
        }
    }

    /// <summary>
    /// Performs the direct file move/rename update logic.
    /// </summary>
    private Task<bool> InstallUsingDirectFileMoveAsync(string newExePath, string localCurrentFile)
    {
        string localStageFile = localCurrentFile + ".stage";
        string localDeleteFile = localCurrentFile + ".delete";

        // Clean up any leftover delete file
        if (File.Exists(localDeleteFile))
            File.Delete(localDeleteFile);

        // Clean up any leftover stage file
        if (File.Exists(localStageFile))
            File.Delete(localStageFile);

        // Move the new exe to stage file
        File.Move(newExePath, localStageFile, overwrite: true);

        // Rename running .exe to .exe.delete
        File.Move(localCurrentFile, localDeleteFile, overwrite: true);
        System.Threading.Thread.Sleep(200);

        // If for some reason the current exe still exists, try again
        if (File.Exists(localCurrentFile))
        {
            File.Move(localCurrentFile, localDeleteFile, overwrite: true);
            System.Threading.Thread.Sleep(200);
        }

        // Rename .exe.stage to .exe
        File.Move(localStageFile, localCurrentFile, overwrite: true);

        // If for some reason the stage file still exists, try again after a short wait
        if (File.Exists(localStageFile))
        {
            System.Threading.Thread.Sleep(1000);
            File.Move(localStageFile, localCurrentFile, overwrite: true);
        }

        // Relaunch the application
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = localCurrentFile,
            UseShellExecute = false,
            CreateNoWindow = true
        });

        Log.Information($"Update installed using direct file move. Relaunching application.");
        Application.Current.MainWindow?.Close();
        return Task.FromResult(true);
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

    /// <summary>
    /// Checks if the given path is in a protected system location by testing write access.
    /// </summary>
    private bool IsProtectedLocation(string exePath)
    {
        if (string.IsNullOrEmpty(exePath))
            return true;
        try
        {
            string dir = Path.GetDirectoryName(exePath)!;
            string testFile = Path.Combine(dir, $".__writetest_{Guid.NewGuid()}.tmp");
            using (FileStream fs = File.Create(testFile))
            {
                // Write a single byte to ensure write access
                fs.WriteByte(0);
            }
            File.Delete(testFile);
            return false; // Write succeeded
        }
        catch
        {
            return true; // Write failed, treat as protected
        }
    }
}