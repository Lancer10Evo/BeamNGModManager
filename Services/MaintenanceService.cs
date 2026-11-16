using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
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
    /// Углублённый Health Center:
    /// 1. Настоящая проверка целостности структуры .ZIP архивов (Central Directory + чтение заголовка потока).
    /// 2. Поиск 100% побайтовых дубликатов по хэшу SHA-256.
    /// 3. Поиск семантических дубликатов (одинаковый GameId машины/карты в разных архивах).
    /// </summary>
    public async Task<string> RunHealthCheckAsync(IEnumerable<ModItem> allMods, string modsPath)
    {
        return await Task.Run(() =>
        {
            var mods = allMods.ToList();
            var nonZipMods = mods.Where(m => !m.FileName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)).ToList();
            var corruptedZips = new List<string>();

            // 1. Проверка целостности ZIP-архивов
            foreach (var mod in mods.Where(m => m.FileName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)))
            {
                if (mod.SizeBytes == 0 || !VerifyZipIntegrity(mod.FullPath))
                    corruptedZips.Add(mod.FileName);
            }

            // 2. Поиск точных дубликатов по содержимому (SHA-256 хэш для файлов одинакового размера)
            var exactDuplicates = new List<string>();
            var sameSizeGroups = mods
                .Where(m => m.SizeBytes > 0 && File.Exists(m.FullPath))
                .GroupBy(m => m.SizeBytes)
                .Where(g => g.Count() > 1);

            foreach (var sizeGroup in sameSizeGroups)
            {
                var hashGroups = sizeGroup
                    .GroupBy(m => ComputeSha256(m.FullPath))
                    .Where(hg => !string.IsNullOrEmpty(hg.Key) && hg.Count() > 1);

                foreach (var hg in hashGroups)
                    exactDuplicates.Add(string.Join(" == ", hg.Select(x => x.FileName)));
            }

            // 3. Поиск семантических дубликатов (два разных архива с одной и той же уникальной папкой машины/карты)
            var semanticDuplicates = mods
                .Where(m => m.GameId != "—" && m.GameId != "Системный / Скрипт" && !m.GameId.Contains(','))
                .GroupBy(m => m.GameId, StringComparer.OrdinalIgnoreCase)
                .Where(g => g.Count() > 1)
                .Select(g => $"{g.Key}: ({string.Join(", ", g.Select(x => x.FileName))})")
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

            return $"🩺 ОТЧЁТ ЦЕНТРА ЗДОРОВЬЯ (Health Center v1.4):\n\n" +
                   $"• Проверено архивов: {mods.Count}\n" +
                   $"• Проверка целостности ZIP: {(corruptedZips.Count == 0 ? "✅ Все архивы целые" : $"❌ Повреждены ({corruptedZips.Count}): {string.Join(", ", corruptedZips)}")}\n" +
                   $"• Побайтовые дубликаты (SHA-256): {(exactDuplicates.Count == 0 ? "✅ Не найдено" : $"⚠️ Найдено ({exactDuplicates.Count}):\n   " + string.Join("\n   ", exactDuplicates.Take(5)))}\n" +
                   $"• Повторы одной модели/карты: {(semanticDuplicates.Count == 0 ? "✅ Нет" : $"ℹ️ {semanticDuplicates.Count} шт.:\n   " + string.Join("\n   ", semanticDuplicates.Take(4)))}\n" +
                   $"• Архивы не в формате .ZIP: {(nonZipMods.Count == 0 ? "✅ Нет" : $"⚠️ {nonZipMods.Count} шт.")}\n\n" +
                   $"🌐 Кэш серверов BeamMP: {serverModsCount} мод. ({serverModsMb:F1} MB)";
        });
    }

    private static bool VerifyZipIntegrity(string zipPath)
    {
        try
        {
            using var archive = ZipFile.OpenRead(zipPath);
            if (archive.Entries.Count == 0)
                return false;

            // Пробуем открыть поток первого непустого файла, чтобы убедиться, что заголовок не битый
            var firstFile = archive.Entries.FirstOrDefault(e => e.Length > 0);
            if (firstFile != null)
            {
                using var stream = firstFile.Open();
                Span<byte> buffer = stackalloc byte[1];
                _ = stream.Read(buffer);
            }
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static string ComputeSha256(string filePath)
    {
        try
        {
            using var stream = File.OpenRead(filePath);
            byte[] hash = SHA256.HashData(stream);
            return Convert.ToHexString(hash);
        }
        catch
        {
            return string.Empty;
        }
    }

    /// <summary>
    /// Умный анализатор конфликтов с разделением на Критические (JBeam/Lua/PC) и Мягкие переопределения (Текстуры/Ресурсы)
    /// </summary>
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

            // Помечаем уровень серьёзности конфликта (🔴 КРИТИЧЕСКИЙ для физики/скриптов vs 🟡 РЕСУРС для текстур)
            var result = new Dictionary<string, List<string>>();
            var overlaps = fileToModsMap
                .Where(kv => kv.Value.Count > 1)
                .OrderByDescending(kv => IsCriticalConflictFile(kv.Key));

            foreach (var kv in overlaps)
            {
                string prefix = IsCriticalConflictFile(kv.Key) ? "🔴 [КРИТИЧНО: Физика/Код] " : "🟡 [Ресурс/Текстура] ";
                result[prefix + kv.Key] = kv.Value;
            }

            return result;
        });
    }

    private static bool IsCriticalConflictFile(string virtualPath)
    {
        string ext = Path.GetExtension(virtualPath).ToLowerInvariant();
        return ext is ".jbeam" or ".lua" or ".pc" or ".cs";
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