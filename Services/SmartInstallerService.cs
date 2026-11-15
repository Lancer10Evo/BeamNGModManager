using System.IO;
using System.IO.Compression;
using BeamNGModManager.Models;
using SharpCompress.Archives;
using SharpCompress.Common;

namespace BeamNGModManager.Services;

public class SmartInstallerService
{
    // Список системных директорий BeamNG.drive для валидации структуры мода
    private static readonly HashSet<string> ValidBeamNgDirs = new(StringComparer.OrdinalIgnoreCase)
    {
        "vehicles", "levels", "lua", "art", "mod_info", "scripts", "ui", "core", "gameplay", "campaigns"
    };

    public async Task<InstallResult> InstallArchivesAsync(IEnumerable<string> archivePaths, string targetModsDir)
    {
        return await Task.Run(() =>
        {
            var result = new InstallResult();
            Directory.CreateDirectory(targetModsDir);

            foreach (string path in archivePaths)
            {
                if (!File.Exists(path)) continue;
                try
                {
                    bool installedAny = ProcessSingleArchive(path, targetModsDir, result.InstalledFiles);
                    if (!installedAny)
                        result.InvalidFiles.Add(Path.GetFileName(path));
                }
                catch
                {
                    result.InvalidFiles.Add(Path.GetFileName(path));
                }
            }
            return result;
        });
    }

    private bool ProcessSingleArchive(string archivePath, string targetModsDir, List<string> installedCollector)
    {
        string ext = Path.GetExtension(archivePath).ToLowerInvariant();

        // БЫСТРЫЙ ПУТЬ: если это уже корректный .zip мод без вложенных архивов-матрёшек
        if (ext == ".zip" && IsCleanDirectZipMod(archivePath))
        {
            string destPath = GetUniqueDestinationPath(targetModsDir, Path.GetFileName(archivePath));
            File.Copy(archivePath, destPath, overwrite: true);
            installedCollector.Add(Path.GetFileName(destPath));
            return true;
        }

        // Распаковка в %TEMP% для анализа "матрёшек" (.rar/.7z/.zip с рекламой) или кривой структуры
        string tempDir = Path.Combine(Path.GetTempPath(), "BeamNGModMgr_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            using (var archive = ArchiveFactory.Open(archivePath))
            {
                foreach (var entry in archive.Entries.Where(e => !e.IsDirectory))
                {
                    entry.WriteToDirectory(tempDir, new ExtractionOptions
                    {
                        ExtractFullPath = true,
                        Overwrite = true
                    });
                }
            }

            bool foundValidMod = false;

            // 1. ОБРАБОТКА «МАТРЁШЕК»: ищем целевые .zip архивы внутри распакованной папки
            var nestedZips = Directory.GetFiles(tempDir, "*.zip", SearchOption.AllDirectories);
            foreach (string nestedZip in nestedZips)
            {
                if (ValidateZipFile(nestedZip))
                {
                    string dest = GetUniqueDestinationPath(targetModsDir, Path.GetFileName(nestedZip));
                    File.Copy(nestedZip, dest, overwrite: true);
                    installedCollector.Add(Path.GetFileName(dest));
                    foundValidMod = true;
                }
                else
                {
                    // Если вложенный .zip сам запакован криво (например, с лишней папкой-обёрткой)
                    if (ProcessSingleArchive(nestedZip, targetModsDir, installedCollector))
                        foundValidMod = true;
                }
            }

            if (foundValidMod)
                return true;

            // 2. ЗАЩИТА ОТ КРИВОЙ РАСПАКОВКИ: внутри сразу лежат папки vehicles/, levels/, lua/, art/
            // (или они обёрнуты в лишнюю папку вида MyCarMod_v1/vehicles/...)
            string? modRoot = FindBeamNgStructureRoot(tempDir);
            if (modRoot != null)
            {
                // Очищаем рекламный мусор сайтов (.url, .html, .lnk) перед упаковкой
                CleanAdFiles(modRoot);

                string zipName = Path.GetFileNameWithoutExtension(archivePath) + ".zip";
                string destZip = GetUniqueDestinationPath(targetModsDir, zipName);

                ZipFile.CreateFromDirectory(modRoot, destZip, CompressionLevel.Optimal, includeBaseDirectory: false);
                installedCollector.Add(Path.GetFileName(destZip));
                return true;
            }

            return false;
        }
        finally
        {
            // Гарантированная очистка временной папки в %TEMP%
            if (Directory.Exists(tempDir))
            {
                try { Directory.Delete(tempDir, recursive: true); } catch { }
            }
        }
    }

    /// <summary>
    /// Проверяет, является ли .zip готовым валидным модом без вложенных архивов
    /// </summary>
    private bool IsCleanDirectZipMod(string zipPath)
    {
        try
        {
            using var archive = ZipFile.OpenRead(zipPath);
            bool hasValidRootDir = false;
            bool hasNestedArchives = false;

            foreach (var entry in archive.Entries)
            {
                string norm = entry.FullName.Replace('\\', '/').TrimStart('/');
                if (norm.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) ||
                    norm.EndsWith(".rar", StringComparison.OrdinalIgnoreCase) ||
                    norm.EndsWith(".7z", StringComparison.OrdinalIgnoreCase))
                {
                    hasNestedArchives = true;
                    break;
                }

                string topFolder = norm.Split('/')[0];
                if (ValidBeamNgDirs.Contains(topFolder))
                    hasValidRootDir = true;
            }

            return hasValidRootDir && !hasNestedArchives;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Валидация .zip архива на наличие хотя бы одной системной папки BeamNG
    /// </summary>
    public bool ValidateZipFile(string zipPath)
    {
        try
        {
            using var archive = ZipFile.OpenRead(zipPath);
            foreach (var entry in archive.Entries)
            {
                string topFolder = entry.FullName.Replace('\\', '/').TrimStart('/').Split('/')[0];
                if (ValidBeamNgDirs.Contains(topFolder))
                    return true;
            }
            return false;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Ищет папку, в которой находятся системные директории BeamNG (в корне или внутри обёртки)
    /// </summary>
    private string? FindBeamNgStructureRoot(string extractedRoot)
    {
        if (HasBeamNgSystemFolders(extractedRoot))
            return extractedRoot;

        foreach (string subDir in Directory.GetDirectories(extractedRoot, "*", SearchOption.AllDirectories))
        {
            if (HasBeamNgSystemFolders(subDir))
                return subDir;
        }

        return null;
    }

    private bool HasBeamNgSystemFolders(string dirPath)
    {
        return Directory.GetDirectories(dirPath)
            .Select(Path.GetFileName)
            .Any(name => name != null && ValidBeamNgDirs.Contains(name));
    }

    private void CleanAdFiles(string dirPath)
    {
        foreach (string file in Directory.GetFiles(dirPath, "*.*", SearchOption.TopDirectoryOnly))
        {
            string ext = Path.GetExtension(file).ToLowerInvariant();
            if (ext is ".url" or ".html" or ".htm" or ".lnk")
            {
                try { File.Delete(file); } catch { }
            }
        }
    }

    private string GetUniqueDestinationPath(string dir, string fileName)
    {
        string cleanName = string.Join("_", fileName.Split(Path.GetInvalidFileNameChars()));
        return Path.Combine(dir, cleanName);
    }
}