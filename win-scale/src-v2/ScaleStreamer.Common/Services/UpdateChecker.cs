using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ScaleStreamer.Common.Services;

/// <summary>
/// Checks GitHub Releases API for software updates
/// </summary>
public class UpdateChecker
{
    private static readonly HttpClient _httpClient = new HttpClient();
    private readonly string _currentVersion;
    private readonly string _repoOwner;
    private readonly string _repoName;
    private DateTime _lastCheck = DateTime.MinValue;
    private TimeSpan _checkInterval = TimeSpan.FromHours(24);

    public UpdateChecker(string currentVersion, string repoOwner = "cloud-scale-us", string repoName = "Cloud-Scale")
    {
        _currentVersion = currentVersion;
        _repoOwner = repoOwner;
        _repoName = repoName;

        // Set GitHub API headers
        _httpClient.DefaultRequestHeaders.Add("User-Agent", $"ScaleStreamer/{currentVersion}");
        _httpClient.DefaultRequestHeaders.Add("Accept", "application/vnd.github+json");
    }

    /// <summary>
    /// Check for updates (respects cache interval)
    /// </summary>
    public async Task<UpdateInfo?> CheckForUpdatesAsync(bool forceCheck = false)
    {
        // Check cache
        if (!forceCheck && DateTime.Now - _lastCheck < _checkInterval)
        {
            return null; // Too soon since last check
        }

        try
        {
            var apiUrl = $"https://api.github.com/repos/{_repoOwner}/{_repoName}/releases/latest";
            var response = await _httpClient.GetAsync(apiUrl);

            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            var json = await response.Content.ReadAsStringAsync();
            var release = JsonSerializer.Deserialize<GitHubRelease>(json);

            if (release == null)
                return null;

            _lastCheck = DateTime.Now;

            // Parse version from tag (e.g., "v2.5.0" -> "2.5.0")
            var latestVersion = release.TagName?.TrimStart('v') ?? "";

            // Compare versions
            if (IsNewerVersion(latestVersion, _currentVersion))
            {
                // Find MSI asset
                var msiAsset = release.Assets?.FirstOrDefault(a =>
                    a.Name?.EndsWith(".msi", StringComparison.OrdinalIgnoreCase) == true);

                return new UpdateInfo
                {
                    LatestVersion = latestVersion,
                    CurrentVersion = _currentVersion,
                    ReleaseNotes = release.Body ?? "",
                    DownloadUrl = msiAsset?.BrowserDownloadUrl ?? release.HtmlUrl ?? "",
                    ReleaseName = release.Name ?? $"Version {latestVersion}",
                    PublishedAt = release.PublishedAt,
                    FileSize = msiAsset?.Size ?? 0
                };
            }

            return null; // No update available
        }
        catch (Exception)
        {
            // Silently fail - don't block GUI startup
            return null;
        }
    }

    /// <summary>
    /// Compare semantic versions
    /// </summary>
    private bool IsNewerVersion(string latest, string current)
    {
        try
        {
            var latestParts = latest.Split('.').Select(int.Parse).ToArray();
            var currentParts = current.Split('.').Select(int.Parse).ToArray();

            // Ensure both have 3 parts (major.minor.patch)
            if (latestParts.Length < 3 || currentParts.Length < 3)
                return false;

            // Compare major
            if (latestParts[0] > currentParts[0]) return true;
            if (latestParts[0] < currentParts[0]) return false;

            // Compare minor
            if (latestParts[1] > currentParts[1]) return true;
            if (latestParts[1] < currentParts[1]) return false;

            // Compare patch
            if (latestParts[2] > currentParts[2]) return true;

            return false;
        }
        catch
        {
            return false;
        }
    }
}

/// <summary>
/// Update information returned to GUI
/// </summary>
public class UpdateInfo
{
    public string LatestVersion { get; set; } = "";
    public string CurrentVersion { get; set; } = "";
    public string ReleaseNotes { get; set; } = "";
    public string DownloadUrl { get; set; } = "";
    public string ReleaseName { get; set; } = "";
    public DateTime? PublishedAt { get; set; }
    public long FileSize { get; set; }

    public string FileSizeMB => $"{FileSize / 1024.0 / 1024.0:F1} MB";
}

/// <summary>
/// GitHub API response model
/// </summary>
internal class GitHubRelease
{
    [JsonPropertyName("tag_name")]
    public string? TagName { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("body")]
    public string? Body { get; set; }

    [JsonPropertyName("html_url")]
    public string? HtmlUrl { get; set; }

    [JsonPropertyName("published_at")]
    public DateTime? PublishedAt { get; set; }

    [JsonPropertyName("assets")]
    public List<GitHubAsset>? Assets { get; set; }
}

internal class GitHubAsset
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("browser_download_url")]
    public string? BrowserDownloadUrl { get; set; }

    [JsonPropertyName("size")]
    public long Size { get; set; }
}
