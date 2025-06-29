using System.Net.Http;
using System.Text.Json;
using WmiExplorer.Common.Logging;

namespace WmiExplorer.Services;

/// <summary>
/// Service to check for updates and fetch changelog from GitHub releases.
/// </summary>
public class UpdateService
{
    private const string GitHubApiLatestReleaseUrl = "https://api.github.com/repos/{0}/{1}/releases/latest";

    private readonly HttpClient _httpClient;
    private readonly string _owner;
    private readonly string _repo;

    public UpdateService(string owner, string repo)
    {
        _owner = owner;
        _repo = repo;
        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("WmiExplorerUpdateService");
    }

    /// <summary>
    /// Checks if an update is available compared to the current version.
    /// </summary>
    public async Task<(bool isUpdateAvailable, string latestVersion, string changelog)> CheckForUpdateAsync(string currentVersion)
    {
        try
        {
            var url = string.Format(GitHubApiLatestReleaseUrl, _owner, _repo);
            Log.Debug($"Checking for updates at {url} with current version '{currentVersion}'");

            // Make the HTTP request to GitHub API
            var response = await _httpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();
            // Read and parse the JSON response
            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            // Get the root element of the JSON document
            var root = doc.RootElement;

            // Extract the tag name and changelog from the JSON response
            var tag = root.GetProperty("tag_name").GetString();
            // Remove leading 'v' if present in tag
            var latestVersion = !string.IsNullOrEmpty(tag) && tag.StartsWith("v", StringComparison.OrdinalIgnoreCase)
                ? tag.Substring(1)
                : tag;
            var changelog = root.GetProperty("body").GetString();

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

            // Log.Debug($"isUpdateAvailable={isUpdateAvailable}, latestVersion='{latestVersion}', changelog='{changelog}'");
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
}