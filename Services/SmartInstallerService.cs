using System.IO;
using System.IO.Compression;
using BeamNGModManager.Models;
using SharpCompress.Archives;

namespace BeamNGModManager.Services;

public class SmartInstallerService
{
    private const int MaxRecursionDepth = 3;
    private const long MaxExtractedBytesLimit = 4L * 1024 * 1024 * 1024; // 4 GB лимит

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
                    bool installedAny = ProcessSingleArchive(path, targetModsDir, result.InstalledFiles, currentDepth: 0);
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

    private bool ProcessSingleArchive(string archivePath, string targetModsDir, List<string> installedCollector, int currentDepth)
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
            long extractedBytes = 0;

            using (var archive = ArchiveFactory.OpenArchive(archivePath))
            {
                foreach (var entry in archive.Entries.Where(e => !e.IsDirectory))
                {
                    if (string.IsNullOrWhiteSpace(entry.Key)) continue;

                    extractedBytes += entry.Size;
                    if (extractedBytes > MaxExtractedBytesLimit)
                        throw new InvalidOperationException("Превышен безопасный лимит распаковки архива (4 ГБ).");

                    // Строгая защита от Zip-Slip (Path Traversal CVE-2026-44788)
                    string destFilePath = Path.GetFullPath(Path.Combine(tempDir, entry.Key));
                    if (!destFilePath.StartsWith(fullTempRoot, StringComparison.OrdinalIgnoreCase))
                        throw new InvalidOperationException("Заблокирована попытка выхода файла за пределы временной папки (Zip-Slip).");

                    string? destDir = Path.GetDirectoryName(destFilePath);
                    if (!string.IsNullOrEmpty(destDir))
                        Directory.CreateDirectory(destDir);

                    using var inStream = entry.OpenEntryStream();
                    using var outStream = File.Create(destFilePath);
                    inStream.CopyTo(outStream);
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
                    if (ProcessSingleArchive(nestedZip, targetModsDir, installedCollector, currentDepth + 1))
                        foundValidMod = true;
                }
            }

            if (foundValidMod)
                return true;

            string? modRoot = FindBeamNgStructureRoot(tempDir);
            if (modRoot != null)
            {
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
            if (ext is ".url" or ".html" or ".htm" or ".lnk" or ".exe" or ".bat" or ".cmd" or ".ps1")
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