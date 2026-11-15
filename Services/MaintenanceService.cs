using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using BeamNGModManager.Models;

namespace BeamNGModManager.Services;

public class MaintenanceService
{
    /// <summary>
    /// 1. Безопасная очистка кэша: удаление содержимого <UserPath>\<версия>\temp,
    /// исключая папку shaders (чтобы избежать долгой перекомпиляции шейдеров).
    /// </summary>
    public int ClearCacheKeepingShaders(GameProfile profile)
    {
        string versionRoot = string.IsNullOrEmpty(profile.ActiveVersion)
            ? profile.UserPath
            : Path.Combine(profile.UserPath, profile.ActiveVersion);

        string tempPath = Path.Combine(versionRoot, "temp");
        if (!Directory.Exists(tempPath))
            return 0;

        int deletedFilesCount = 0;

        // Удаляем отдельные файлы в корне папки temp
        foreach (string file in Directory.GetFiles(tempPath))
        {
            try
            {
                File.Delete(file);
                deletedFilesCount++;
            }
            catch { }
        }

        // Удаляем все подпапки внутри temp, КРОМЕ папки shaders
        foreach (string dir in Directory.GetDirectories(tempPath))
        {
            string dirName = Path.GetFileName(dir);
            if (dirName.Equals("shaders", StringComparison.OrdinalIgnoreCase))
                continue; // Пропускаем кэш шейдеров!

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
    /// 2. Проверка конфликтов (базовая): поиск активных модов с одинаковыми путями файлов внутри .zip архивов
    /// </summary>
    public async Task<Dictionary<string, List<string>>> DetectConflictsAsync(IEnumerable<ModItem> activeMods)
    {
        return await Task.Run(() =>
        {
            var fileToModsMap = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

            foreach (var mod in activeMods.Where(m => m.IsEnabled && File.Exists(m.FullPath)))
            {
                try
                {
                    using var archive = ZipFile.OpenRead(mod.FullPath);
                    foreach (var entry in archive.Entries)
                    {
                        if (string.IsNullOrEmpty(entry.Name))
                            continue; // Пропускаем записи директорий

                        string virtualPath = entry.FullName.Replace('\\', '/').TrimStart('/');

                        // Игнорируем служебные метаданные репозитория и ОС
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

            // Оставляем только те файлы, которые перезаписываются сразу двумя или более модами
            return fileToModsMap
                .Where(kv => kv.Value.Count > 1)
                .ToDictionary(kv => kv.Key, kv => kv.Value);
        });
    }

    /// <summary>
    /// 3. Быстрый запуск: старт BeamNG.drive.exe выбранного профиля напрямую из менеджера
    /// </summary>
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

        // Если это кастомный/портативный профиль, передаём игре точный путь к UserPath
        if (!string.IsNullOrWhiteSpace(profile.UserPath) &&
            profile.DetectionSource.StartsWith("1:"))
        {
            startInfo.Arguments = $"-userpath \"{profile.UserPath}\"";
        }

        Process.Start(startInfo);
        return true;
    }
}