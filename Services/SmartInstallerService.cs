using System.IO;
using System.IO.Compression;
using BeamNGModManager.Models;
using SharpCompress.Archives;

namespace BeamNGModManager.Services;

public class SmartInstallerService
{
    private const int MaxRecursionDepth = 3;
    private const int MaxEntriesPerArchive = 15_000;
    private const long MaxExtractedBytesLimit = 4L * 1024 * 1024 * 1024; // 4 GB реальных байтов

    private static readonly HashSet<string> ValidBeamNgDirs = new(StringComparer.OrdinalIgnoreCase)
    {
        "vehicles", "levels", "lua", "art", "mod_info", "scripts", "ui", "core", "gameplay", "campaigns"
    };

    private static readonly HashSet<string> DangerousOrAdExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".url", ".html", ".htm", ".lnk", ".exe", ".dll", ".bat", ".cmd", ".ps1", ".vbs", ".scr", ".msi", ".hta"
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
                    long totalBytesExtracted = 0;
                    bool installedAny = ProcessSingleArchive(path, targetModsDir, result.InstalledFiles, currentDepth: 0, ref totalBytesExtracted);
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

    private bool ProcessSingleArchive(
        string archivePath,
        string targetModsDir,
        List<string> installedCollector,
        int currentDepth,
        ref long totalBytesExtracted)
    {
        if (currentDepth > MaxRecursionDepth)
            return false;

        string ext = Path.GetExtension(archivePath).ToLowerInvariant();

        if (ext == ".zip" && IsCleanDirectZipMod(archivePath))
        {
            string destPath = GetUniqueDestinationPath(targetModsDir, Path.GetFileName(archivePath));
            File.Copy(archivePath, destPath, overwrite: false);
            installedCollector.Add(Path.GetFileName(destPath));
            return true;
        }

        string tempDir = Path.Combine(Path.GetTempPath(), "BeamNGModMgr_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        string fullTempRoot = Path.GetFullPath(tempDir) + Path.DirectorySeparatorChar;

        try
        {
            int entryCount = 0;
            byte[] buffer = new byte[81920];

            using (var archive = ArchiveFactory.OpenArchive(archivePath))
            {
                foreach (var entry in archive.Entries.Where(e => !e.IsDirectory))
                {
                    if (string.IsNullOrWhiteSpace(entry.Key)) continue;

                    // Защита от Symlink-атак внутри архивов
                    if (!string.IsNullOrEmpty(entry.LinkTarget))
                        continue;

                    if (++entryCount > MaxEntriesPerArchive)
                        throw new InvalidOperationException("Превышен лимит числа файлов в архиве (защита от archive bomb).");

                    // Строгая проверка Zip-Slip
                    string destFilePath = Path.GetFullPath(Path.Combine(tempDir, entry.Key));
                    if (!destFilePath.StartsWith(fullTempRoot, StringComparison.OrdinalIgnoreCase))
                        throw new InvalidOperationException("Заблокирована попытка выхода файла за пределы временной папки (Zip-Slip).");

                    string? destDir = Path.GetDirectoryName(destFilePath);
                    if (!string.IsNullOrEmpty(destDir))
                        Directory.CreateDirectory(destDir);

                    using var inStream = entry.OpenEntryStream();
                    using var outStream = File.Create(destFilePath);

                    // Считаем РЕАЛЬНО прочитанные байты (невозможно обмануть поддельным entry.Size)
                    int read;
                    while ((read = inStream.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        totalBytesExtracted += read;
                        if (totalBytesExtracted > MaxExtractedBytesLimit)
                            throw new InvalidOperationException("Превышен безопасный лимит реально распакованных байтов (4 ГБ).");
                        outStream.Write(buffer, 0, read);
                    }
                }
            }

            bool foundValidMod = false;

            var nestedZips = Directory.GetFiles(tempDir, "*.zip", SearchOption.AllDirectories);
            foreach (string nestedZip in nestedZips)
            {
                if (ValidateZipFile(nestedZip))
                {
                    string dest = GetUniqueDestinationPath(targetModsDir, Path.GetFileName(nestedZip));
                    File.Copy(nestedZip, dest, overwrite: false);
                    installedCollector.Add(Path.GetFileName(dest));
                    foundValidMod = true;
                }
                else
                {
                    if (ProcessSingleArchive(nestedZip, targetModsDir, installedCollector, currentDepth + 1, ref totalBytesExtracted))
                        foundValidMod = true;
                }
            }

            if (foundValidMod)
                return true;

            string? modRoot = FindBeamNgStructureRoot(tempDir);
            if (modRoot != null)
            {
                CleanAdAndDangerousFiles(modRoot);

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
            if (Directory.Exists(tempDir))
            {
                try { Directory.Delete(tempDir, recursive: true); } catch { }
            }
        }
    }

    private bool IsCleanDirectZipMod(string zipPath)
    {
        try
        {
            using var archive = ZipFile.OpenRead(zipPath);
            if (archive.Entries.Count > MaxEntriesPerArchive)
                return false;

            bool hasValidRootDir = false;

            foreach (var entry in archive.Entries)
            {
                string norm = entry.FullName.Replace('\\', '/').TrimStart('/');
                if (norm.Contains("..", StringComparison.Ordinal))
                    return false;

                string ext = Path.GetExtension(norm).ToLowerInvariant();
                if (ext is ".zip" or ".rar" or ".7z" || DangerousOrAdExtensions.Contains(ext))
                    return false;

                string topFolder = norm.Split('/')[0];
                if (ValidBeamNgDirs.Contains(topFolder))
                    hasValidRootDir = true;
            }

            return hasValidRootDir;
        }
        catch
        {
            return false;
        }
    }

    public bool ValidateZipFile(string zipPath)
    {
        try
        {
            using var archive = ZipFile.OpenRead(zipPath);
            foreach (var entry in archive.Entries)
            {
                string norm = entry.FullName.Replace('\\', '/').TrimStart('/');
                if (norm.Contains("..", StringComparison.Ordinal))
                    return false;

                string topFolder = norm.Split('/')[0];
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

    private void CleanAdAndDangerousFiles(string dirPath)
    {
        foreach (string file in Directory.GetFiles(dirPath, "*.*", SearchOption.AllDirectories))
        {
            if (DangerousOrAdExtensions.Contains(Path.GetExtension(file)))
            {
                try { File.Delete(file); } catch { }
            }
        }
    }

    private string GetUniqueDestinationPath(string targetModsDir, string fileName)
    {
        string cleanName = string.Join("_", fileName.Split(Path.GetInvalidFileNameChars()));
        if (string.IsNullOrWhiteSpace(cleanName))
            cleanName = "mod_" + Guid.NewGuid().ToString("N")[..6] + ".zip";

        string baseName = Path.GetFileNameWithoutExtension(cleanName);
        string ext = Path.GetExtension(cleanName);
        string disabledDir = Path.Combine(targetModsDir, "_disabled");

        string candidatePath = Path.Combine(targetModsDir, cleanName);
        string disabledCandidate = Path.Combine(disabledDir, cleanName);

        int counter = 1;
        while (File.Exists(candidatePath) || File.Exists(disabledCandidate))
        {
            string numberedName = $"{baseName} ({counter}){ext}";
            candidatePath = Path.Combine(targetModsDir, numberedName);
            disabledCandidate = Path.Combine(disabledDir, numberedName);
            counter++;
        }

        return candidatePath;
    }
}