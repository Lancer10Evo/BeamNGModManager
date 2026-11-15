using System.Net.Http;
using System.Reflection;
using System.Text.Json;

namespace BeamNGModManager.Services;

public class UpdateService
{
    // Укажите здесь ссылку на ваш GitHub-репозиторий для автопроверки релизов
    private const string GithubLatestReleaseApi =
        "https://api.github.com/repos/Lancer10Evo/BeamNGModManager/releases/latest";

    public async Task<(bool IsUpdateAvailable, string LatestVersion, string DownloadUrl)> CheckForUpdatesAsync()
    {
        try
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
            client.DefaultRequestHeaders.UserAgent.ParseAdd("BeamNGModManager/1.0");

            string json = await client.GetStringAsync(GithubLatestReleaseApi);
            using var doc = JsonDocument.Parse(json);

            // Читаем тег релиза (например, "v1.1.0" -> "1.1.0") и ссылку на страницу загрузки
            string tag = doc.RootElement.GetProperty("tag_name").GetString()?.TrimStart('v', 'V') ?? "1.0.0";
            string htmlUrl = doc.RootElement.GetProperty("html_url").GetString() ?? string.Empty;

            Version currentVersion = Assembly.GetExecutingAssembly().GetName().Version ?? new Version(1, 0, 0);
            if (Version.TryParse(tag, out var latestVersion) && latestVersion > currentVersion)
            {
                return (true, latestVersion.ToString(), htmlUrl);
            }
        }
        catch
        {
            // При отсутствии подключения к интернету тихо возвращаем false без сбоев
        }

        return (false, "1.0.0", string.Empty);
    }
}
