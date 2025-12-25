using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Windows;
using WmiExplorer.Common.Enums;
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

    /// <summary>
    /// Indicates the deployment type of the current application installation.
    /// </summary>
    public readonly DeploymentType DeploymentType;

    public readonly string GitApiReleaseUrl;
    public readonly string GitLatestReleaseUrl;
    public readonly string GitReleaseUrl;

    /// <summary>
    /// Path to the WmiExplorer temporary directory for update operations.
    /// </summary>
    public readonly string WmiExplorerTempDirectory;

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
        DeploymentType = DetectDeploymentType();

        // Initialize and create temp directory
        // Directory.CreateDirectory is idempotent - safe to call even if directory already exists
        WmiExplorerTempDirectory = Path.Combine(Path.GetTempPath(), "WmiExplorer");
        try
        {
            Directory.CreateDirectory(WmiExplorerTempDirectory);
        }
        catch (Exception ex)
        {
            Log.Error(ex, $"Failed to create temp directory '{WmiExplorerTempDirectory}'. Updates may not work correctly.");
            // Continue anyway - the directory might already exist or we'll fail later with a clearer error
        }
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
    /// Cleans up orphaned .delete and .stage files from previous update operations.
    /// This should be called on application startup to remove any leftover files.
    /// </summary>
    public static void CleanupOrphanedUpdateFiles()
    {
        try
        {
            var exePath = Environment.ProcessPath;
            if (string.IsNullOrEmpty(exePath) || !File.Exists(exePath))
            {
                Log.Debug("Could not determine exe path for orphaned file cleanup.");
                return;
            }

            // Look for .delete and .stage files matching the current executable name
            var deleteFile = exePath + ".delete";
            var stageFile = exePath + ".stage";

            // Clean up .delete file with retry logic
            if (File.Exists(deleteFile))
            {
                bool deleted = false;
                for (int i = 0; i < 5; i++)
                {
                    try
                    {
                        File.Delete(deleteFile);
                        Log.Information($"Cleaned up orphaned .delete file: '{deleteFile}'");
                        deleted = true;
                        break;
                    }
                    catch (IOException ex)
                    {
                        Log.Debug($"Could not delete orphaned .delete file, retry attempt [{i + 1}/5]... Error: {ex.Message}");
                        if (i < 4)
                        {
                            System.Threading.Thread.Sleep(500);
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Warning(ex, $"Failed to delete orphaned .delete file: '{deleteFile}'");
                        break;
                    }
                }

                if (!deleted)
                {
                    Log.Warning($"Could not delete orphaned .delete file after 5 attempts: '{deleteFile}'");
                }
            }

            // Clean up .stage file with retry logic
            if (File.Exists(stageFile))
            {
                bool deleted = false;
                for (int i = 0; i < 5; i++)
                {
                    try
                    {
                        File.Delete(stageFile);
                        Log.Information($"Cleaned up orphaned .stage file: '{stageFile}'");
                        deleted = true;
                        break;
                    }
                    catch (IOException ex)
                    {
                        Log.Debug($"Could not delete orphaned .stage file, retry attempt [{i + 1}/5]... Error: {ex.Message}");
                        if (i < 4)
                        {
                            System.Threading.Thread.Sleep(500);
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Warning(ex, $"Failed to delete orphaned .stage file: '{stageFile}'");
                        break;
                    }
                }

                if (!deleted)
                {
                    Log.Warning($"Could not delete orphaned .stage file after 5 attempts: '{stageFile}'");
                }
            }
        }
        catch (Exception ex)
        {
            // Don't throw exceptions - this is cleanup code that shouldn't break startup
            Log.Warning(ex, "Error during orphaned update file cleanup. Continuing startup.");
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
    /// Installs the downloaded update by extracting the ZIP and replacing files based on deployment type.
    /// </summary>
    public async Task<bool> InstallAsync(string zipPath)
    {
        string localCurrentFile = Environment.ProcessPath!;

        // Bail early if in a protected location
        if (IsProtectedLocation(localCurrentFile))
        {
            Log.Error($"Update cannot proceed: application is running from a protected location: '{localCurrentFile}'. Manually update the application by replacing the executable using '{zipPath}'.");
            return false;
        }

        // Extract ZIP to temporary directory
        string extractPath = Path.Combine(WmiExplorerTempDirectory, $"WmiExplorerUpdate_{Guid.NewGuid()}");
        bool isMultiFile = DeploymentType == DeploymentType.MultiFile;

        try
        {
            if (!ExtractZip(zipPath, extractPath))
            {
                Log.Error($"Failed to extract ZIP file: '{zipPath}'");
                return false;
            }

            // Route to appropriate installation method based on deployment type
            bool result = DeploymentType switch
            {
                DeploymentType.MultiFile => await InstallMultiFileAsync(extractPath, localCurrentFile),
                DeploymentType.SingleFile or DeploymentType.Standalone => await InstallSingleFileAsync(extractPath, localCurrentFile),
                _ => false
            };

            // For MultiFile, don't clean up immediately - batch script needs the files
            // The batch script will clean up after it's done, or we'll clean up on next startup
            if (!isMultiFile)
            {
                // Clean up extracted files after a delay (allow installation to complete)
                _ = Task.Run(async () =>
                {
                    await Task.Delay(5000); // Wait 5 seconds
                    try
                    {
                        if (Directory.Exists(extractPath))
                            Directory.Delete(extractPath, recursive: true);
                    }
                    catch (Exception ex)
                    {
                        Log.Warning(ex, $"Failed to clean up extracted files at '{extractPath}'");
                    }
                });
            }

            return result;
        }
        catch
        {
            // Only clean up on error - if successful, cleanup is handled above
            if (!isMultiFile)
            {
                try
                {
                    if (Directory.Exists(extractPath))
                        Directory.Delete(extractPath, recursive: true);
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, $"Failed to clean up extracted files after error at '{extractPath}'");
                }
            }
            throw;
        }
    }

    /// <summary>
    /// Detects the deployment type of the current application installation.
    /// </summary>
    private DeploymentType DetectDeploymentType()
    {
        var exePath = Environment.ProcessPath;
        if (string.IsNullOrEmpty(exePath) || !File.Exists(exePath))
        {
            Log.Warning("Could not determine exe path, defaulting to SingleFile deployment type.");
            return DeploymentType.SingleFile;
        }

        var exeDir = Path.GetDirectoryName(exePath);
        if (string.IsNullOrEmpty(exeDir))
        {
            Log.Warning("Could not determine exe directory, defaulting to SingleFile deployment type.");
            return DeploymentType.SingleFile;
        }

        var exeSize = new FileInfo(exePath).Length;

        // Check for DLLs in same directory
        bool hasDlls = Directory.GetFiles(exeDir, "*.dll", SearchOption.TopDirectoryOnly).Any();

        if (hasDlls)
        {
            Log.Information("Detected MultiFile deployment type (DLLs found in directory).");
            return DeploymentType.MultiFile;
        }
        else if (exeSize > 50 * 1024 * 1024) // 50MB
        {
            Log.Information("Detected Standalone deployment type (large exe > 50MB, no DLLs).");
            return DeploymentType.Standalone;
        }
        else
        {
            Log.Information("Detected SingleFile deployment type (small exe < 50MB, no DLLs).");
            return DeploymentType.SingleFile;
        }
    }

    /// <summary>
    /// Extracts a ZIP file to the specified destination directory.
    /// </summary>
    private bool ExtractZip(string zipPath, string extractPath)
    {
        try
        {
            if (!File.Exists(zipPath))
            {
                Log.Error($"ZIP file does not exist: '{zipPath}'");
                return false;
            }

            Directory.CreateDirectory(extractPath);
            ZipFile.ExtractToDirectory(zipPath, extractPath, overwriteFiles: true);

            // Validate that WmiExplorer.exe exists in extracted files
            var exeFile = Path.Combine(extractPath, "WmiExplorer.exe");
            if (!File.Exists(exeFile))
            {
                Log.Error($"WmiExplorer.exe not found in extracted ZIP at '{extractPath}'");
                return false;
            }

            Log.Information($"Successfully extracted ZIP to '{extractPath}'");
            return true;
        }
        catch (Exception ex)
        {
            Log.Error(ex, $"Failed to extract ZIP file '{zipPath}' to '{extractPath}'");
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
    /// Installs update for MultiFile deployment by replacing all binary files.
    /// </summary>
    private async Task<bool> InstallMultiFileAsync(string extractPath, string currentExePath)
    {
        try
        {
            var currentExeDir = Path.GetDirectoryName(currentExePath);
            if (string.IsNullOrEmpty(currentExeDir))
            {
                Log.Error("Could not determine current exe directory for MultiFile update.");
                return false;
            }

            // Get all files from extracted ZIP
            var extractedFiles = Directory.GetFiles(extractPath, "*", SearchOption.TopDirectoryOnly).ToList();
            if (!extractedFiles.Any())
            {
                Log.Error($"No files found in extracted ZIP at '{extractPath}'");
                return false;
            }

            // Use batch file approach for MultiFile since we need to replace multiple files
            var batchPath = Path.Combine(WmiExplorerTempDirectory, $"WmiExplorerUpdater_{Guid.NewGuid()}.bat");
            var currentExeName = Path.GetFileName(currentExePath);

            // Build batch script to replace all files
            var batchContent = new StringBuilder();
            batchContent.AppendLine("@echo off");
            batchContent.AppendLine(":loop");
            batchContent.AppendLine($"tasklist | find /i \"{currentExeName}\" >nul 2>&1");
            batchContent.AppendLine("if not errorlevel 1 (");
            batchContent.AppendLine("    timeout /t 1 >nul");
            batchContent.AppendLine("    goto loop");
            batchContent.AppendLine(")");

            // Replace each file from the ZIP
            foreach (var extractedFile in extractedFiles)
            {
                var fileName = Path.GetFileName(extractedFile);
                var targetPath = Path.Combine(currentExeDir, fileName);

                // Only replace binary files (exe, dll, etc.) - preserve config files
                if (fileName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ||
                    fileName.EndsWith(".dll", StringComparison.OrdinalIgnoreCase) ||
                    fileName.EndsWith(".pdb", StringComparison.OrdinalIgnoreCase))
                {
                    batchContent.AppendLine($"if exist \"{targetPath}\" del /f /q \"{targetPath}\"");
                    batchContent.AppendLine($"move /y \"{extractedFile}\" \"{targetPath}\"");
                }
            }

            // Restart application
            batchContent.AppendLine($"start \"\" \"{currentExePath}\"");

            // Clean up extracted files directory and batch file itself
            batchContent.AppendLine($"if exist \"{extractPath}\" rd /s /q \"{extractPath}\"");
            batchContent.AppendLine("del \"%~f0\"");

            await File.WriteAllTextAsync(batchPath, batchContent.ToString());

            // Launch the batch file (it will wait for this process to exit)
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = batchPath,
                UseShellExecute = false,
                CreateNoWindow = true
            });

            Log.Information($"MultiFile update: Batch script launched. Closing application to allow file replacement and restart.");
            // Close the application so the batch script can proceed with file replacement
            // The batch script will restart the application after replacing files
            Application.Current.MainWindow?.Close();
            return true;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to install MultiFile update.");
            return false;
        }
    }

    /// <summary>
    /// Installs update for SingleFile or Standalone deployment by replacing the single executable.
    /// </summary>
    private async Task<bool> InstallSingleFileAsync(string extractPath, string currentExePath)
    {
        var extractedExe = Path.Combine(extractPath, "WmiExplorer.exe");
        if (!File.Exists(extractedExe))
        {
            Log.Error($"WmiExplorer.exe not found in extracted files at '{extractPath}'");
            return false;
        }

        try
        {
            // Try direct file move approach
            bool directResult = await InstallUsingDirectFileMoveAsync(extractedExe, currentExePath);
            if (directResult)
                return true;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Direct file move update failed, falling back to batch file updater.");
        }
        // Fallback to batch file approach
        return await InstallUsingBatchAsync(extractedExe);
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
            var batchPath = Path.Combine(WmiExplorerTempDirectory, $"WmiExplorerUpdater_{Guid.NewGuid()}.bat");
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

        try
        {
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

            // Start the new process before closing to ensure it launches
            // The new process will use the updated exe file
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = localCurrentFile,
                UseShellExecute = false,
                CreateNoWindow = true
            });

            Log.Information($"Update installed using direct file move. Relaunching application.");
            // Close the current application - the new instance has already started
            // The .delete file will be cleaned up on next startup
            Application.Current.MainWindow?.Close();
            return Task.FromResult(true);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to install update using direct file move.");

            // Try to restore the original exe if it's missing (critical for recovery)
            try
            {
                if (!File.Exists(localCurrentFile) && File.Exists(localDeleteFile))
                {
                    File.Move(localDeleteFile, localCurrentFile, overwrite: true);
                    Log.Information($"Restored original executable from .delete file after update failure.");
                }
            }
            catch (Exception restoreEx)
            {
                Log.Warning(restoreEx, $"Failed to restore original executable from .delete file: '{localDeleteFile}'");
            }

            // Clean up .stage file on error (non-critical, will be cleaned up on startup if this fails)
            try
            {
                if (File.Exists(localStageFile))
                {
                    File.Delete(localStageFile);
                }
            }
            catch
            {
                // Ignore - will be cleaned up on startup
            }

            throw;
        }
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