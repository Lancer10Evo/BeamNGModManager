using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;
using BeamNGModManager.Models;
using Microsoft.Win32;

namespace BeamNGModManager.Services;

public class PathResolverService
{
    private readonly string _configPath = Path.Combine(
        AppDomain.CurrentDomain.BaseDirectory, "modmanager_config.json");

    public AppConfig Config { get; private set; } = new();

    public void LoadConfig()
    {
        if (File.Exists(_configPath))
        {
            try
            {
                string json = File.ReadAllText(_configPath);
                Config = JsonSerializer.Deserialize<AppConfig>(json) ?? new AppConfig();
            }
            catch
            {
                Config = new AppConfig();
            }
        }

        // Всегда актуализируем список профилей и количество модов при старте
        DiscoverAllProfiles();
    }

    public void DiscoverAllProfiles()
    {
        string gameRoot = TryFindSteamGameRoot() ?? string.Empty;
        string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

        var candidateBasePaths = new List<(string path, string label)>();

        // 1. Ручные профили, которые пользователь добавлял сам
        foreach (var existing in Config.Profiles.Where(p => p.DetectionSource == "Manual"))
        {
            if (!string.IsNullOrEmpty(existing.UserPath) && Directory.Exists(existing.UserPath))
                candidateBasePaths.Add((existing.UserPath, existing.Name));
        }

        // 2. startup.ini в папке игры
        if (!string.IsNullOrEmpty(gameRoot))
        {
            string startupIni = Path.Combine(gameRoot, "startup.ini");
            if (File.Exists(startupIni))
            {
                string? iniPath = ParseKeyFromIni(startupIni, "UserPath", gameRoot);
                if (!string.IsNullOrEmpty(iniPath) && Directory.Exists(iniPath))
                    candidateBasePaths.Add((iniPath, "Portable INI"));
            }
        }

        // 3. Новый конфиг %LocalAppData%\BeamNG\BeamNG.drive.ini
        string modernBeamNgIni = Path.Combine(localAppData, "BeamNG", "BeamNG.drive.ini");
        if (File.Exists(modernBeamNgIni))
        {
            string? customFolder = ParseKeyFromIni(modernBeamNgIni, "userFolder", localAppData);
            if (!string.IsNullOrEmpty(customFolder) && Directory.Exists(customFolder))
                candidateBasePaths.Add((customFolder, "Custom UserFolder"));
        }

        // 4. Реестр Windows
        using (RegistryKey? key = Registry.CurrentUser.OpenSubKey(@"Software\BeamNG\BeamNG.drive"))
        {
            string? regUserPath = key?.GetValue("userpath_override")?.ToString()
                               ?? key?.GetValue("userpath")?.ToString();
            if (!string.IsNullOrWhiteSpace(regUserPath) && Directory.Exists(regUserPath))
                candidateBasePaths.Add((regUserPath, "Registry"));
        }

        // 5. Новая папка BeamNG (0.35+): %LocalAppData%\BeamNG\BeamNG.drive
        string modernDefaultPath = Path.Combine(localAppData, "BeamNG", "BeamNG.drive");
        if (Directory.Exists(modernDefaultPath))
            candidateBasePaths.Add((modernDefaultPath, "BeamNG.drive"));

        // 6. Старая папка BeamNG: %LocalAppData%\BeamNG.drive
        string legacyDefaultPath = Path.Combine(localAppData, "BeamNG.drive");
        if (Directory.Exists(legacyDefaultPath))
            candidateBasePaths.Add((legacyDefaultPath, "BeamNG Legacy"));

        var discovered = new List<(GameProfile profile, bool isCurrent)>();

        foreach (var (basePath, label) in candidateBasePaths.DistinctBy(x => Path.GetFullPath(x.path).TrimEnd('\\').ToLowerInvariant()))
        {
            // Проверяем прямую подпапку mods в корне
            string directMods = Path.Combine(basePath, "mods");
            if (Directory.Exists(directMods))
            {
                int count = CountModsInFolder(directMods);
                if (count > 0)
                {
                    discovered.Add((new GameProfile
                    {
                        Name = label,
                        GameRootPath = gameRoot,
                        UserPath = basePath,
                        ActiveVersion = "root",
                        ModsPath = directMods,
                        DetectionSource = "Auto",
                        ModsCount = count
                    }, false));
                }
            }

            // Проверяем подпапки версий (current, latest, 0.35, 0.34, 0.33...)
            foreach (string subDir in Directory.GetDirectories(basePath))
            {
                string folderName = Path.GetFileName(subDir);
                if (folderName.Equals("mods", StringComparison.OrdinalIgnoreCase) ||
                    folderName.Equals("temp", StringComparison.OrdinalIgnoreCase) ||
                    folderName.Equals("cache", StringComparison.OrdinalIgnoreCase))
                    continue;

                string subMods = Path.Combine(subDir, "mods");
                bool isCurrent = folderName.Equals("current", StringComparison.OrdinalIgnoreCase) ||
                                 folderName.Equals("latest", StringComparison.OrdinalIgnoreCase);
                bool isVersion = Regex.IsMatch(folderName, @"^\d+\.\d+");

                if (isCurrent || isVersion || Directory.Exists(subMods))
                {
                    Directory.CreateDirectory(subMods);
                    int count = CountModsInFolder(subMods);
                    discovered.Add((new GameProfile
                    {
                        Name = "BeamNG.drive",
                        GameRootPath = gameRoot,
                        UserPath = basePath,
                        ActiveVersion = folderName,
                        ModsPath = subMods,
                        DetectionSource = "Auto",
                        ModsCount = count
                    }, isCurrent));
                }
            }
        }

        if (discovered.Count > 0)
        {
            var sorted = discovered
                .DistinctBy(x => Path.GetFullPath(x.profile.ModsPath).TrimEnd('\\').ToLowerInvariant())
                .OrderByDescending(x => x.profile.ModsCount)
                .ThenByDescending(x => x.isCurrent)
                .ThenByDescending(x => x.profile.ActiveVersion)
                .Select(x => x.profile)
                .ToList();

            Config.Profiles = sorted;
            if (Config.SelectedProfileIndex >= Config.Profiles.Count)
                Config.SelectedProfileIndex = 0;
        }
        else if (Config.Profiles.Count == 0)
        {
            Config.Profiles.Add(ResolveByHierarchy(null));
        }

        SaveConfig();
    }

    public int CountModsInFolder(string modsDir)
    {
        if (!Directory.Exists(modsDir)) return 0;
        try
        {
            return Directory.GetFiles(modsDir, "*.*", SearchOption.AllDirectories)
                .Count(f =>
                {
                    string ext = Path.GetExtension(f).ToLowerInvariant();
                    return ext is ".zip" or ".rar" or ".7z";
                });
        }
        catch { return 0; }
    }

    public void SaveConfig()
    {
        var options = new JsonSerializerOptions { WriteIndented = true };
        File.WriteAllText(_configPath, JsonSerializer.Serialize(Config, options));
    }

    public GameProfile ResolveByHierarchy(
        string? manualGameRoot,
        string? manualUserPath = null,
        string profileName = "Custom Profile")
    {
        string gameRoot = manualGameRoot ?? TryFindSteamGameRoot() ?? string.Empty;

        if (!string.IsNullOrWhiteSpace(manualUserPath) && Directory.Exists(manualUserPath))
        {
            var (ver, modsDir) = ResolveVersionAndModsFolder(manualUserPath);
            int count = CountModsInFolder(modsDir);
            return new GameProfile
            {
                Name = profileName,
                GameRootPath = gameRoot,
                UserPath = manualUserPath,
                ActiveVersion = ver,
                ModsPath = modsDir,
                DetectionSource = "Manual",
                ModsCount = count
            };
        }

        string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        string modernPath = Path.Combine(localAppData, "BeamNG", "BeamNG.drive");
        string targetBase = Directory.Exists(modernPath)
            ? modernPath
            : Path.Combine(localAppData, "BeamNG.drive");

        Directory.CreateDirectory(targetBase);
        var (defaultVer, defaultModsDir) = ResolveVersionAndModsFolder(targetBase);
        int defaultCount = CountModsInFolder(defaultModsDir);

        return new GameProfile
        {
            Name = "BeamNG.drive",
            GameRootPath = gameRoot,
            UserPath = targetBase,
            ActiveVersion = defaultVer,
            ModsPath = defaultModsDir,
            DetectionSource = "Auto",
            ModsCount = defaultCount
        };
    }

    private string? ParseKeyFromIni(string iniPath, string keyName, string baseDirForRelative)
    {
        foreach (string rawLine in File.ReadAllLines(iniPath))
        {
            string line = rawLine.Trim();
            if (line.StartsWith(";") || line.StartsWith("#") || !line.Contains('='))
                continue;

            var parts = line.Split('=', 2);
            if (parts[0].Trim().Equals(keyName, StringComparison.OrdinalIgnoreCase))
            {
                string rawVal = parts[1].Trim().Trim('"', '\'');
                if (string.IsNullOrWhiteSpace(rawVal)) return null;

                return Path.IsPathRooted(rawVal)
                    ? Path.GetFullPath(rawVal)
                    : Path.GetFullPath(Path.Combine(baseDirForRelative, rawVal));
            }
        }
        return null;
    }

    public (string version, string modsPath) ResolveVersionAndModsFolder(string baseUserPath)
    {
        if (Path.GetFileName(baseUserPath).Equals("mods", StringComparison.OrdinalIgnoreCase))
            return ("custom", baseUserPath);

        string directMods = Path.Combine(baseUserPath, "mods");
        if (Directory.Exists(directMods) && CountModsInFolder(directMods) > 0)
            return ("root", directMods);

        if (Directory.Exists(baseUserPath))
        {
            var bestSubDir = Directory.GetDirectories(baseUserPath)
                .Select(d => new
                {
                    Name = Path.GetFileName(d),
                    ModsDir = Path.Combine(d, "mods"),
                    Count = CountModsInFolder(Path.Combine(d, "mods"))
                })
                .OrderByDescending(x => x.Count)
                .FirstOrDefault();

            if (bestSubDir != null && bestSubDir.Count > 0)
                return (bestSubDir.Name, bestSubDir.ModsDir);
        }

        string currentMods = Path.Combine(baseUserPath, "current", "mods");
        Directory.CreateDirectory(currentMods);
        return ("current", currentMods);
    }

    private string? TryFindSteamGameRoot()
    {
        using RegistryKey? steamKey = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\WOW6432Node\Valve\Steam")
                                   ?? Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam");
        string? steamPath = steamKey?.GetValue("InstallPath")?.ToString()
                         ?? steamKey?.GetValue("SteamPath")?.ToString();

        if (!string.IsNullOrEmpty(steamPath))
        {
            string defaultCandidate = Path.Combine(steamPath, "steamapps", "common", "BeamNG.drive");
            if (Directory.Exists(defaultCandidate)) return defaultCandidate;

            string vdfPath = Path.Combine(steamPath, "steamapps", "libraryfolders.vdf");
            if (File.Exists(vdfPath))
            {
                foreach (Match m in Regex.Matches(File.ReadAllText(vdfPath), "\"path\"\\s+\"([^\"]+)\""))
                {
                    string libPath = m.Groups[1].Value.Replace(@"\\", @"\");
                    string candidate = Path.Combine(libPath, "steamapps", "common", "BeamNG.drive");
                    if (Directory.Exists(candidate)) return candidate;
                }
            }
        }
        return null;
    }
}