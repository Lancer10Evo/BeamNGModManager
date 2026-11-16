using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using BeamNGModManager.Models;

namespace BeamNGModManager.Services;

public class MaintenanceService
{
    public int ClearCacheKeepingShaders(GameProfile profile)
    {
        string versionRoot = string.IsNullOrEmpty(profile.ActiveVersion) || profile.ActiveVersion == "root" || profile.ActiveVersion == "custom"
            ? profile.UserPath
            : Path.Combine(profile.UserPath, profile.ActiveVersion);

        string tempPath = Path.Combine(versionRoot, "temp");
        if (!Directory.Exists(tempPath))
            return 0;

        int deletedFilesCount = 0;

        foreach (string file in Directory.GetFiles(tempPath))
        {
            try { File.Delete(file); deletedFilesCount++; } catch { }
        }

        foreach (string dir in Directory.GetDirectories(tempPath))
        {
            string dirName = Path.GetFileName(dir);
            if (dirName.Equals("shaders", StringComparison.OrdinalIgnoreCase))
                continue;

            try
            {
                int count = Directory.GetFiles(dir, "*.*", SearchOption.AllDirectories).Length;
                Directory.Delete(dir, recursive: true);
                deletedFilesCount += count;
            }
            catch { }
        }

        return deletedFilesCount;
    }

    /// <summary>
    /// Очистка скачанных модов серверов BeamMP (из папки mods\multiplayer\), сохраняя сам клиент BeamMP.zip
    /// </summary>
    public (int removedCount, double freedMb) CleanBeamMpServerCache(string modsPath)
    {
        string mpDir = Path.Combine(modsPath, "multiplayer");
        if (!Directory.Exists(mpDir))
            return (0, 0);

        int count = 0;
        long bytes = 0;

        foreach (string file in Directory.GetFiles(mpDir, "*.*", SearchOption.AllDirectories))
        {
            string fileName = Path.GetFileName(file);
            // Никогда не удаляем сам клиентский мод BeamMP.zip!
            if (fileName.Equals("BeamMP.zip", StringComparison.OrdinalIgnoreCase))
                continue;

            try
            {
                var fi = new FileInfo(file);
                bytes += fi.Length;
                File.Delete(file);
                count++;
            }
            catch { }
        }

        return (count, bytes / 1024.0 / 1024.0);
    }

    /// <summary>
    /// Health Center (как в Mod Manager Pro и BMM): поиск дубликатов, битых архивов и не-ZIP файлов
    /// </summary>
    public async Task<string> RunHealthCheckAsync(IEnumerable<ModItem> allMods, string modsPath)
    {
        return await Task.Run(() =>
        {
            var mods = allMods.ToList();
            var nonZipMods = mods.Where(m => !m.FileName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)).ToList();
            var zeroByteMods = mods.Where(m => m.SizeBytes == 0).ToList();

            // Поиск дубликатов по совпадению отображаемого названия или точного размера в байтах (>100 КБ)
            var duplicateGroups = mods
                .Where(m => m.SizeBytes > 100_000)
                .GroupBy(m => m.SizeBytes)
                .Where(g => g.Count() > 1)
                .Select(g => string.Join(" == ", g.Select(x => x.FileName)))
                .ToList();

            string mpDir = Path.Combine(modsPath, "multiplayer");
            int serverModsCount = 0;
            double serverModsMb = 0;
            if (Directory.Exists(mpDir))
            {
                var serverFiles = Directory.GetFiles(mpDir, "*.zip")
                    .Where(f => !Path.GetFileName(f).Equals("BeamMP.zip", StringComparison.OrdinalIgnoreCase))
                    .Select(f => new FileInfo(f))
                    .ToList();
                serverModsCount = serverFiles.Count;
                serverModsMb = serverFiles.Sum(f => f.Length) / 1024.0 / 1024.0;
            }

            return $"🩺 ОТЧЁТ ЦЕНТРА ЗДОРОВЬЯ (Health Center):\n\n" +
                   $"• Всего проверено модов: {mods.Count}\n" +
                   $"• Дубликаты модов (одинаковый вес): {(duplicateGroups.Count == 0 ? "✅ Не найдено" : $"⚠️ Найдено ({duplicateGroups.Count}):\n   " + string.Join("\n   ", duplicateGroups.Take(5)))}\n\n" +
                   $"• Архивы не в формате .ZIP (.rar/.7z в папке mods): {(nonZipMods.Count == 0 ? "✅ Нет" : $"⚠️ {nonZipMods.Count} шт. ({string.Join(", ", nonZipMods.Select(x => x.FileName))})")}\n" +
                   $"• Повреждённые (пустые) файлы: {(zeroByteMods.Count == 0 ? "✅ Нет" : $"❌ {zeroByteMods.Count} шт.")}\n\n" +
                   $"🌐 Кэш серверов BeamMP (mods\\multiplayer):\n" +
                   $"• Чужих серверных модов: {serverModsCount} шт. ({serverModsMb:F1} MB)\n" +
                   $"  (Вы можете очистить их кнопкой «🌐 Кэш MP», сохранив клиент BeamMP.zip)";
        });
    }

    public async Task<Dictionary<string, List<string>>> DetectConflictsAsync(IEnumerable<ModItem> activeMods)
    {
        return await Task.Run(() =>
        {
            var fileToModsMap = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

            foreach (var mod in activeMods.Where(m => m.IsEnabled && File.Exists(m.FullPath) && m.FileName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)))
            {
                try
                {
                    using var archive = ZipFile.OpenRead(mod.FullPath);
                    foreach (var entry in archive.Entries)
                    {
                        if (string.IsNullOrEmpty(entry.Name))
                            continue;

                        string virtualPath = entry.FullName.Replace('\\', '/').TrimStart('/');
                        if (virtualPath.StartsWith("mod_info/", StringComparison.OrdinalIgnoreCase) ||
                            virtualPath.Equals("info.json", StringComparison.OrdinalIgnoreCase) ||
                            entry.Name.Equals("desktop.ini", StringComparison.OrdinalIgnoreCase) ||
                            entry.Name.Equals("thumbs.db", StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }

                        if (!fileToModsMap.TryGetValue(virtualPath, out var modList))
                        {
                            modList = new List<string>();
                            fileToModsMap[virtualPath] = modList;
                        }

                        if (!modList.Contains(mod.FileName))
                            modList.Add(mod.FileName);
                    }
                }
                catch { }
            }

            return fileToModsMap
                .Where(kv => kv.Value.Count > 1)
                .ToDictionary(kv => kv.Key, kv => kv.Value);
        });
    }

    public bool LaunchGame(GameProfile profile)
    {
        if (string.IsNullOrWhiteSpace(profile.GameRootPath))
            return false;

        string[] candidates =
        {
            Path.Combine(profile.GameRootPath, "BeamNG.drive.exe"),
            Path.Combine(profile.GameRootPath, "Bin64", "BeamNG.drive.x64.exe")
        };

        string? exePath = candidates.FirstOrDefault(File.Exists);
        if (exePath == null)
            return false;

        var startInfo = new ProcessStartInfo
        {
            FileName = exePath,
            WorkingDirectory = profile.GameRootPath,
            UseShellExecute = true
        };

        Process.Start(startInfo);
        return true;
    }

    /// <summary>
    /// Автопоиск и запуск лаунчера мультиплеера BeamMP-Launcher.exe
    /// </summary>
    public bool LaunchBeamMp(string? savedBeamMpPath, out string resolvedPath)
    {
        resolvedPath = string.Empty;
        var candidates = new List<string>();

        if (!string.IsNullOrWhiteSpace(savedBeamMpPath))
            candidates.Add(savedBeamMpPath);

        string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        string desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);

        candidates.Add(Path.Combine(appData, "BeamMP-Launcher", "BeamMP-Launcher.exe"));
        candidates.Add(Path.Combine(localAppData, "BeamMP-Launcher", "BeamMP-Launcher.exe"));
        candidates.Add(Path.Combine(desktop, "BeamMP-Launcher.exe"));
        candidates.Add(@"C:\Games\BeamMP-Launcher\BeamMP-Launcher.exe");

        string? found = candidates.FirstOrDefault(File.Exists);
        if (found == null)
            return false;

        resolvedPath = found;
        Process.Start(new ProcessStartInfo
        {
            FileName = found,
            WorkingDirectory = Path.GetDirectoryName(found) ?? "",
            UseShellExecute = true
        });
        return true;
    }
}