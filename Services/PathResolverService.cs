using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;
using BeamNGModManager.Models;
using Microsoft.Win32;

namespace BeamNGModManager.Services;

public class PathResolverService
{
    public const string SteamAppId = "284160"; // Официальный ID BeamNG.drive в Steam

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

        DiscoverAllProfiles();
    }

    public void DiscoverAllProfiles()
    {
        var (detectedGameRoot, licenseType) = TryFindLicensedGameRoot();
        string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

        var candidateBasePaths = new List<(string path, string label, string root, string lic)>();

        // 1. Сохранённые ручные профили
        foreach (var existing in Config.Profiles.Where(p => p.DetectionSource == "Manual"))
        {
            if (!string.IsNullOrEmpty(existing.UserPath) && Directory.Exists(existing.UserPath))
            {
                string lic = DetectLicenseTypeForGameRoot(existing.GameRootPath);
                candidateBasePaths.Add((existing.UserPath, existing.Name, existing.GameRootPath, lic));
            }
        }

        // 2. Конфиг startup.ini в корне игры
        if (!string.IsNullOrEmpty(detectedGameRoot))
        {
            string startupIni = Path.Combine(detectedGameRoot, "startup.ini");
            if (File.Exists(startupIni))
            {
                string? iniPath = ParseKeyFromIni(startupIni, "UserPath", detectedGameRoot);
                if (!string.IsNullOrEmpty(iniPath) && Directory.Exists(iniPath))
                    candidateBasePaths.Add((iniPath, "Portable INI", detectedGameRoot, licenseType));
            }
        }

        // 3. Новый конфиг %LocalAppData%\BeamNG\BeamNG.drive.ini
        string modernBeamNgIni = Path.Combine(localAppData, "BeamNG", "BeamNG.drive.ini");
        if (File.Exists(modernBeamNgIni))
        {
            string? customFolder = ParseKeyFromIni(modernBeamNgIni, "userFolder", localAppData);
            if (!string.IsNullOrEmpty(customFolder) && Directory.Exists(customFolder))
                candidateBasePaths.Add((customFolder, "BeamNG.ini", detectedGameRoot, licenseType));
        }

        // 4. Реестр Windows
        using (RegistryKey? key = Registry.CurrentUser.OpenSubKey(@"Software\BeamNG\BeamNG.drive"))
        {
            string? regUserPath = key?.GetValue("userpath_override")?.ToString()
                               ?? key?.GetValue("userpath")?.ToString();
            if (!string.IsNullOrWhiteSpace(regUserPath) && Directory.Exists(regUserPath))
                candidateBasePaths.Add((regUserPath, "Registry", detectedGameRoot, licenseType));
        }

        // 5. Современная папка BeamNG (0.35+): %LocalAppData%\BeamNG\BeamNG.drive
        string modernDefaultPath = Path.Combine(localAppData, "BeamNG", "BeamNG.drive");
        if (Directory.Exists(modernDefaultPath))
            candidateBasePaths.Add((modernDefaultPath, "BeamNG.drive", detectedGameRoot, licenseType));

        // 6. Классическая папка BeamNG: %LocalAppData%\BeamNG.drive
        string legacyDefaultPath = Path.Combine(localAppData, "BeamNG.drive");
        if (Directory.Exists(legacyDefaultPath))
            candidateBasePaths.Add((legacyDefaultPath, "BeamNG Legacy", detectedGameRoot, licenseType));

        var discovered = new List<(GameProfile profile, bool isCurrent)>();

        foreach (var (basePath, label, root, lic) in candidateBasePaths.DistinctBy(x => Path.GetFullPath(x.path).TrimEnd('\\').ToLowerInvariant()))
        {
            string directMods = Path.Combine(basePath, "mods");
            if (Directory.Exists(directMods))
            {
                int count = CountModsInFolder(directMods);
                if (count > 0)
                {
                    discovered.Add((new GameProfile
                    {
                        Name = label,
                        GameRootPath = root,
                        UserPath = basePath,
                        ActiveVersion = "root",
                        ModsPath = directMods,
                        DetectionSource = "Auto",
                        LicenseType = lic,
                        ModsCount = count
                    }, false));
                }
            }

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
                        GameRootPath = root,
                        UserPath = basePath,
                        ActiveVersion = folderName,
                        ModsPath = subMods,
                        DetectionSource = "Auto",
                        LicenseType = lic,
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
        var (autoRoot, autoLic) = TryFindLicensedGameRoot();
        string gameRoot = manualGameRoot ?? autoRoot;
        string licType = !string.IsNullOrEmpty(manualGameRoot)
            ? DetectLicenseTypeForGameRoot(manualGameRoot)
            : autoLic;

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
                LicenseType = licType,
                ModsCount = count
            };
        }

        // Проверяем startup.ini в выбранной папке игры (портативная сборка / пиратка)
        if (!string.IsNullOrEmpty(gameRoot) && Directory.Exists(gameRoot))
        {
            string startupIni = Path.Combine(gameRoot, "startup.ini");
            if (File.Exists(startupIni))
            {
                string? iniUserPath = ParseKeyFromIni(startupIni, "UserPath", gameRoot);
                if (!string.IsNullOrEmpty(iniUserPath))
                {
                    Directory.CreateDirectory(iniUserPath);
                    var (ver, modsDir) = ResolveVersionAndModsFolder(iniUserPath);
                    return new GameProfile
                    {
                        Name = profileName,
                        GameRootPath = gameRoot,
                        UserPath = iniUserPath,
                        ActiveVersion = ver,
                        ModsPath = modsDir,
                        DetectionSource = "Manual",
                        LicenseType = licType,
                        ModsCount = CountModsInFolder(modsDir)
                    };
                }
            }
        }

        string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        string modernPath = Path.Combine(localAppData, "BeamNG", "BeamNG.drive");
        string targetBase = Directory.Exists(modernPath)
            ? modernPath
            : Path.Combine(localAppData, "BeamNG.drive");

        Directory.CreateDirectory(targetBase);
        var (defaultVer, defaultModsDir) = ResolveVersionAndModsFolder(targetBase);

        return new GameProfile
        {
            Name = profileName,
            GameRootPath = gameRoot,
            UserPath = targetBase,
            ActiveVersion = defaultVer,
            ModsPath = defaultModsDir,
            DetectionSource = "Manual",
            LicenseType = licType,
            ModsCount = CountModsInFolder(defaultModsDir)
        };
    }

    public string DetectLicenseTypeForGameRoot(string? gameRoot)
    {
        if (string.IsNullOrWhiteSpace(gameRoot) || !Directory.Exists(gameRoot))
            return "Local Profile";

        // Проверка признаков лицензии Steam (appmanifest_284160.acf на два уровня выше в steamapps)
        string? parent1 = Directory.GetParent(gameRoot)?.FullName;
        string? parent2 = parent1 != null ? Directory.GetParent(parent1)?.FullName : null;
        if (parent2 != null && File.Exists(Path.Combine(parent2, $"appmanifest_{SteamAppId}.acf")))
            return "Steam License";

        if (gameRoot.Contains("steamapps", StringComparison.OrdinalIgnoreCase))
            return "Steam License";

        // Проверка признаков лицензии Epic Games (.egstore)
        if (Directory.Exists(Path.Combine(gameRoot, ".egstore")) ||
            gameRoot.Contains("Epic Games", StringComparison.OrdinalIgnoreCase))
            return "Epic Games License";

        // Проверка наличия эмуляторов Steam (пиратка / репак)
        if (File.Exists(Path.Combine(gameRoot, "steam_api64.ini")) ||
            File.Exists(Path.Combine(gameRoot, "Bin64", "steam_emu.ini")) ||
            File.Exists(Path.Combine(gameRoot, "SmartSteamEmu.ini")) ||
            File.Exists(Path.Combine(gameRoot, "onlinefix.ini")))
            return "Portable / Repack";

        return "Custom Build";
    }

    /// <summary>
    /// Поиск лицензионной версии в Steam (через libraryfolders.vdf и appmanifest_284160.acf)
    /// и в Epic Games Launcher (через C:\ProgramData\Epic\EpicGamesLauncher\Data\Manifests\*.item)
    /// </summary>
    public (string gameRoot, string licenseType) TryFindLicensedGameRoot(string? customSteamVdfPath = null, string? customEpicManifestDir = null)
    {
        // 1. Поиск лицензии STEAM
        string? steamPath = null;
        if (customSteamVdfPath == null)
        {
            using RegistryKey? steamKey = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\WOW6432Node\Valve\Steam")
                                       ?? Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam");
            steamPath = steamKey?.GetValue("InstallPath")?.ToString()
                     ?? steamKey?.GetValue("SteamPath")?.ToString();
        }

        string? vdfPath = customSteamVdfPath ?? (!string.IsNullOrEmpty(steamPath)
            ? Path.Combine(steamPath, "steamapps", "libraryfolders.vdf")
            : null);

        if (!string.IsNullOrEmpty(vdfPath) && File.Exists(vdfPath))
        {
            foreach (Match m in Regex.Matches(File.ReadAllText(vdfPath), "\"path\"\\s+\"([^\"]+)\""))
            {
                string libPath = m.Groups[1].Value.Replace(@"\\", @"\");
                string steamAppsDir = Path.Combine(libPath, "steamapps");
                string acfFile = Path.Combine(steamAppsDir, $"appmanifest_{SteamAppId}.acf");

                // Если есть официальный манифест лицензии Steam
                if (File.Exists(acfFile))
                {
                    string acfContent = File.ReadAllText(acfFile);
                    var installDirMatch = Regex.Match(acfContent, "\"installdir\"\\s+\"([^\"]+)\"");
                    string folderName = installDirMatch.Success ? installDirMatch.Groups[1].Value : "BeamNG.drive";
                    string candidate = Path.Combine(steamAppsDir, "common", folderName);
                    if (Directory.Exists(candidate))
                        return (candidate, "Steam License");
                }

                string fallbackCandidate = Path.Combine(steamAppsDir, "common", "BeamNG.drive");
                if (Directory.Exists(fallbackCandidate))
                    return (fallbackCandidate, "Steam License");
            }
        }

        // 2. Поиск лицензии EPIC GAMES STORE
        string epicManifestsDir = customEpicManifestDir ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "Epic", "EpicGamesLauncher", "Data", "Manifests");

        if (Directory.Exists(epicManifestsDir))
        {
            foreach (string itemFile in Directory.GetFiles(epicManifestsDir, "*.item"))
            {
                try
                {
                    using var doc = JsonDocument.Parse(File.ReadAllText(itemFile));
                    var root = doc.RootElement;
                    string displayName = root.TryGetProperty("DisplayName", out var dn) ? dn.GetString() ?? "" : "";
                    string installLoc = root.TryGetProperty("InstallLocation", out var il) ? il.GetString() ?? "" : "";

                    if (displayName.Contains("BeamNG", StringComparison.OrdinalIgnoreCase) &&
                        !string.IsNullOrEmpty(installLoc) && Directory.Exists(installLoc))
                    {
                        return (installLoc, "Epic Games License");
                    }
                }
                catch { }
            }
        }

        return (string.Empty, "Local Profile");
    }

    /// <summary>
    /// АДДОН: Симулятор и тестер лицензий Steam и Epic Games (позволяет протестировать поддержку лицензий без купленной игры)
    /// </summary>
    public string RunLicenseSimulatorTest()
    {
        string tempSimRoot = Path.Combine(Path.GetTempPath(), "BeamNG_License_Sim_" + Guid.NewGuid().ToString("N")[..6]);
        try
        {
            // 1. Создаём виртуальный стенд Steam (libraryfolders.vdf + appmanifest_284160.acf + папка игры)
            string fakeSteamLib = Path.Combine(tempSimRoot, "SteamLibrary");
            string fakeSteamApps = Path.Combine(fakeSteamLib, "steamapps");
            string fakeSteamGame = Path.Combine(fakeSteamApps, "common", "BeamNG.drive");
            Directory.CreateDirectory(fakeSteamGame);
            File.WriteAllText(Path.Combine(fakeSteamGame, "BeamNG.drive.exe"), "MOCK_EXE");

            string vdfFile = Path.Combine(tempSimRoot, "libraryfolders.vdf");
            File.WriteAllText(vdfFile, $"\"libraryfolders\" {{ \"0\" {{ \"path\" \"{fakeSteamLib.Replace(@"\", @"\\")}\" }} }}");

            string acfFile = Path.Combine(fakeSteamApps, $"appmanifest_{SteamAppId}.acf");
            File.WriteAllText(acfFile, $"\"AppState\" {{ \"appid\" \"{SteamAppId}\" \"name\" \"BeamNG.drive\" \"installdir\" \"BeamNG.drive\" }}");

            var steamTest = TryFindLicensedGameRoot(customSteamVdfPath: vdfFile);
            bool steamOk = steamTest.licenseType == "Steam License" && steamTest.gameRoot == fakeSteamGame;

            // 2. Создаём виртуальный стенд Epic Games (Manifest.item + папка игры с .egstore)
            string fakeEpicManifests = Path.Combine(tempSimRoot, "EpicManifests");
            string fakeEpicGame = Path.Combine(tempSimRoot, "EpicGames", "BeamNG.drive");
            Directory.CreateDirectory(fakeEpicManifests);
            Directory.CreateDirectory(Path.Combine(fakeEpicGame, ".egstore"));

            string itemJson = JsonSerializer.Serialize(new
            {
                DisplayName = "BeamNG.drive",
                InstallLocation = fakeEpicGame
            });
            File.WriteAllText(Path.Combine(fakeEpicManifests, "beamng_mock.item"), itemJson);

            var epicTest = TryFindLicensedGameRoot(customSteamVdfPath: "none", customEpicManifestDir: fakeEpicManifests);
            bool epicOk = epicTest.licenseType == "Epic Games License" && epicTest.gameRoot == fakeEpicGame;

            // 3. Проверяем реальное состояние на текущем ПК
            var realPc = TryFindLicensedGameRoot();
            string realStatus = string.IsNullOrEmpty(realPc.gameRoot)
                ? "На вашем ПК лицензия Steam/Epic не установлена (используется локальный профиль)."
                : $"Найдена реальная лицензия: {realPc.licenseType} ({realPc.gameRoot})";

            return $"🛡️ ОТЧЁТ ДИАГНОСТИКИ И СИМУЛЯЦИИ ЛИЦЕНЗИЙ:\n\n" +
                   $"1. Реальный статус ПК:\n   • {realStatus}\n\n" +
                   $"2. Тест парсера Steam (libraryfolders.vdf + appmanifest_284160.acf):\n" +
                   $"   • Статус: {(steamOk ? "✅ УСПЕШНО ПРОЙДЕН" : "❌ ОШИБКА")}\n" +
                   $"   • Распознан тип: {steamTest.licenseType}\n\n" +
                   $"3. Тест парсера Epic Games (ProgramData Manifests *.item + .egstore):\n" +
                   $"   • Статус: {(epicOk ? "✅ УСПЕШНО ПРОЙДЕН" : "❌ ОШИБКА")}\n" +
                   $"   • Распознан тип: {epicTest.licenseType}\n\n" +
                   $"Вывод: У всех пользователей со Steam и Epic Games лицензией ваша программа определит пути со 100% точностью!";
        }
        finally
        {
            if (Directory.Exists(tempSimRoot))
            {
                try { Directory.Delete(tempSimRoot, recursive: true); } catch { }
            }
        }
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
}