using System.Net.Http;
using System.Reflection;
using System.Text.Json;

namespace BeamNGModManager.Services;

public class UpdateService
{
    private const string GithubLatestReleaseApi =
        "https://api.github.com/repos/Lancer10Evo/BeamNGModManager/releases/latest";

    public async Task<(bool IsUpdateAvailable, string LatestVersion, string DownloadUrl)> CheckForUpdatesAsync()
    {
        try
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(6) };
            client.DefaultRequestHeaders.UserAgent.ParseAdd("BeamModManagerUltimate/3.0");

            string json = await client.GetStringAsync(GithubLatestReleaseApi);
            using var doc = JsonDocument.Parse(json);

            string tag = doc.RootElement.GetProperty("tag_name").GetString()?.TrimStart('v', 'V') ?? "1.0.0";
            string htmlUrl = doc.RootElement.GetProperty("html_url").GetString() ?? string.Empty;

            // Строгая валидация ссылки (защита от подмены схемы URI)
            if (!Uri.TryCreate(htmlUrl, UriKind.Absolute, out var uri) ||
                uri.Scheme != Uri.UriSchemeHttps ||
                !uri.Host.Equals("github.com", StringComparison.OrdinalIgnoreCase))
            {
                htmlUrl = "https://github.com/Lancer10Evo/BeamNGModManager/releases/latest";
            }

            Version currentVersion = Assembly.GetExecutingAssembly().GetName().Version ?? new Version(1, 0, 0);
            if (Version.TryParse(tag, out var latestVersion) && latestVersion > currentVersion)
            {
                return (true, latestVersion.ToString(), htmlUrl);
            }
        }
        catch { }

        return (false, "3.0.0", string.Empty);
    }
}