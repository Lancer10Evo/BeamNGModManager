using System.IO;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows.Media.Imaging;
using BeamNGModManager.Models;

namespace BeamNGModManager.Services;

public class ModManagerService
{
    [DllImport("Kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool CreateHardLink(string lpFileName, string lpExistingFileName, IntPtr lpSecurityAttributes);

    public async Task<List<ModItem>> LoadModsAsync(string modsPath, IEnumerable<string>? favoriteFileNames = null)
    {
        return await Task.Run(() =>
        {
            var list = new List<ModItem>();
            if (!Directory.Exists(modsPath)) return list;

            var favSet = new HashSet<string>(favoriteFileNames ?? Enumerable.Empty<string>(), StringComparer.OrdinalIgnoreCase);
            string disabledDir = Path.Combine(modsPath, "_disabled");

            var allFiles = Directory.GetFiles(modsPath, "*.*", SearchOption.AllDirectories)
                .Where(f =>
                {
                    string ext = Path.GetExtension(f).ToLowerInvariant();
                    return ext is ".zip" or ".rar" or ".7z";
                })
                .ToList();

            var activeFiles = allFiles
                .Where(f => !f.StartsWith(disabledDir, StringComparison.OrdinalIgnoreCase))
                .ToList();

            foreach (string file in activeFiles)
            {
                var item = ParseModZip(file, isEnabled: true);
                item.IsFavorite = favSet.Contains(item.FileName);
                list.Add(item);
            }

            if (Directory.Exists(disabledDir))
            {
                var activeNames = new HashSet<string>(activeFiles.Select(Path.GetFileName)!, StringComparer.OrdinalIgnoreCase);
                var disabledFiles = Directory.GetFiles(disabledDir, "*.*", SearchOption.AllDirectories)
                    .Where(f =>
                    {
                        string ext = Path.GetExtension(f).ToLowerInvariant();
                        return ext is ".zip" or ".rar" or ".7z";
                    });

                foreach (string file in disabledFiles)
                {
                    if (activeNames.Contains(Path.GetFileName(file)))
                        continue;

                    var item = ParseModZip(file, isEnabled: false);
                    item.IsFavorite = favSet.Contains(item.FileName);
                    list.Add(item);
                }
            }

            // Избранные моды всегда наверху, далее по алфавиту
            return list
                .OrderByDescending(m => m.IsFavorite)
                .ThenBy(m => m.DisplayName)
                .ToList();
        });
    }

    private ModItem ParseModZip(string zipPath, bool isEnabled)
    {
        var fi = new FileInfo(zipPath);
        string parentDirName = fi.Directory?.Name ?? "";

        string sourceTag = "📁 Local";
        if (parentDirName.Equals("multiplayer", StringComparison.OrdinalIgnoreCase) ||
            fi.Name.Contains("BeamMP", StringComparison.OrdinalIgnoreCase))
        {
            sourceTag = "🌐 BeamMP";
        }
        else if (parentDirName.Equals("repo", StringComparison.OrdinalIgnoreCase))
        {
            sourceTag = "📦 Repo";
        }

        var item = new ModItem
        {
            FileName = fi.Name,
            FullPath = fi.FullName,
            DisplayName = Path.GetFileNameWithoutExtension(fi.Name),
            SizeBytes = fi.Length,
            SourceTag = sourceTag,
            IsEnabled = isEnabled
        };

        if (!fi.Extension.Equals(".zip", StringComparison.OrdinalIgnoreCase))
        {
            item.Category = "⚠️ Не .ZIP";
            item.InternalStructure = "Требуется перепаковка в .zip";
            return item;
        }

        try
        {
            using var archive = ZipFile.OpenRead(zipPath);

            bool hasVehicles = false, hasLevels = false, hasLuaOrUi = false, hasSound = false;
            var rootFolders = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var entry in archive.Entries)
            {
                string path = entry.FullName.Replace('\\', '/').TrimStart('/');
                var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
                if (segments.Length > 1)
                    rootFolders.Add(segments[0] + "/" + segments[1]);
                else if (segments.Length == 1)
                    rootFolders.Add(segments[0]);

                if (path.StartsWith("vehicles/", StringComparison.OrdinalIgnoreCase)) hasVehicles = true;
                else if (path.StartsWith("levels/", StringComparison.OrdinalIgnoreCase)) hasLevels = true;
                else if (path.StartsWith("art/sound/", StringComparison.OrdinalIgnoreCase)) hasSound = true;
                else if (path.StartsWith("lua/", StringComparison.OrdinalIgnoreCase) ||
                         path.StartsWith("ui/", StringComparison.OrdinalIgnoreCase) ||
                         path.StartsWith("scripts/", StringComparison.OrdinalIgnoreCase)) hasLuaOrUi = true;
            }

            if (sourceTag == "🌐 BeamMP" && fi.Name.Equals("BeamMP.zip", StringComparison.OrdinalIgnoreCase))
                item.Category = "🌐 Клиент MP";
            else if (hasLevels) item.Category = "🗺️ Карта";
            else if (hasVehicles) item.Category = "🚗 Авто";
            else if (hasSound) item.Category = "🔊 Звук";
            else if (hasLuaOrUi) item.Category = "⚙️ Скрипт";
            else item.Category = "📦 Мод";

            item.InternalStructure = rootFolders.Count > 0
                ? string.Join(", ", rootFolders.Take(6))
                : "Корень архива";

            var infoEntries = archive.Entries
                .Where(e => e.Name.Equals("info.json", StringComparison.OrdinalIgnoreCase))
                .OrderBy(e => e.FullName.StartsWith("mod_info/", StringComparison.OrdinalIgnoreCase) ? 0 : 1)
                .ThenBy(e => e.FullName.Count(c => c == '/' || c == '\\'))
                .ToList();

            foreach (var infoEntry in infoEntries)
            {
                try
                {
                    using var stream = infoEntry.Open();
                    using var doc = JsonDocument.Parse(stream, new JsonDocumentOptions
                    {
                        AllowTrailingCommas = true,
                        CommentHandling = JsonCommentHandling.Skip
                    });
                    var root = doc.RootElement;

                    if (item.DisplayName == Path.GetFileNameWithoutExtension(fi.Name))
                    {
                        if (TryGetPropertyIgnoreCase(root, "title", out var titleProp) && !string.IsNullOrWhiteSpace(titleProp.ToString()))
                            item.DisplayName = titleProp.ToString();
                        else if (TryGetPropertyIgnoreCase(root, "Name", out var nameProp) && !string.IsNullOrWhiteSpace(nameProp.ToString()))
                            item.DisplayName = nameProp.ToString();
                    }

                    if (item.Author == "—")
                    {
                        if (TryGetPropertyIgnoreCase(root, "author", out var authProp) && !string.IsNullOrWhiteSpace(authProp.ToString()))
                            item.Author = authProp.ToString();
                        else if (TryGetPropertyIgnoreCase(root, "authors", out var authsProp) && !string.IsNullOrWhiteSpace(authsProp.ToString()))
                            item.Author = authsProp.ToString();
                    }

                    if (item.Version == "—")
                    {
                        if (TryGetPropertyIgnoreCase(root, "version_string", out var verStr) && !string.IsNullOrWhiteSpace(verStr.ToString()))
                            item.Version = verStr.ToString();
                        else if (TryGetPropertyIgnoreCase(root, "version", out var ver) && !string.IsNullOrWhiteSpace(ver.ToString()))
                            item.Version = ver.ToString();
                    }
                }
                catch { }
            }

            var iconEntry = archive.Entries
                .Where(e =>
                    (e.Name.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) ||
                     e.Name.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ||
                     e.Name.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase)) &&
                    e.Length > 0 && e.Length < 5_000_000)
                .OrderBy(e => e.FullName.StartsWith("mod_info/", StringComparison.OrdinalIgnoreCase) ? 0 :
                              e.Name.StartsWith("default.", StringComparison.OrdinalIgnoreCase) ? 1 : 2)
                .FirstOrDefault();

            if (iconEntry != null)
            {
                using var imgStream = iconEntry.Open();
                using var ms = new MemoryStream();
                imgStream.CopyTo(ms);
                ms.Position = 0;

                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.DecodePixelWidth = 180; // Увеличенное разрешение для боковой карточки Инспектора
                bitmap.StreamSource = ms;
                bitmap.EndInit();
                bitmap.Freeze();
                item.PreviewImage = bitmap;
            }
        }
        catch { }

        return item;
    }

    private static bool TryGetPropertyIgnoreCase(JsonElement element, string propName, out JsonElement value)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var prop in element.EnumerateObject())
            {
                if (prop.Name.Equals(propName, StringComparison.OrdinalIgnoreCase))
                {
                    value = prop.Value;
                    return true;
                }
            }
        }
        value = default;
        return false;
    }

    public void ToggleModState(ModItem mod, string modsPath, bool useHardlinks)
    {
        string disabledDir = Path.Combine(modsPath, "_disabled");
        Directory.CreateDirectory(disabledDir);

        var dirInfo = new DirectoryInfo(disabledDir);
        if ((dirInfo.Attributes & FileAttributes.Hidden) == 0)
            dirInfo.Attributes |= FileAttributes.Hidden;

        if (mod.IsEnabled)
        {
            // Если это мод BeamMP, возвращаем его именно в подпапку multiplayer
            string targetSubFolder = mod.SourceTag == "🌐 BeamMP"
                ? Path.Combine(modsPath, "multiplayer")
                : modsPath;
            Directory.CreateDirectory(targetSubFolder);

            string targetActivePath = Path.Combine(targetSubFolder, mod.FileName);
            if (File.Exists(mod.FullPath) && !mod.FullPath.Equals(targetActivePath, StringComparison.OrdinalIgnoreCase))
            {
                if (useHardlinks)
                {
                    if (!CreateHardLink(targetActivePath, mod.FullPath, IntPtr.Zero))
                    {
                        try { File.CreateSymbolicLink(targetActivePath, mod.FullPath); }
                        catch { File.Move(mod.FullPath, targetActivePath, overwrite: true); }
                    }
                }
                else
                {
                    File.Move(mod.FullPath, targetActivePath, overwrite: true);
                }
                mod.FullPath = targetActivePath;
            }
        }
        else
        {
            string targetDisabledPath = Path.Combine(disabledDir, mod.FileName);
            if (File.Exists(mod.FullPath))
            {
                if (useHardlinks && File.Exists(targetDisabledPath))
                    File.Delete(mod.FullPath);
                else
                    File.Move(mod.FullPath, targetDisabledPath, overwrite: true);

                mod.FullPath = targetDisabledPath;
            }
        }
    }

    public void DeleteMod(ModItem mod, string modsPath)
    {
        if (File.Exists(mod.FullPath))
            File.Delete(mod.FullPath);

        string disabledCopy = Path.Combine(modsPath, "_disabled", mod.FileName);
        if (File.Exists(disabledCopy))
            File.Delete(disabledCopy);
    }
}using System.IO;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows.Media.Imaging;
using BeamNGModManager.Models;

namespace BeamNGModManager.Services;

public class ModManagerService
{
    [DllImport("Kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool CreateHardLink(string lpFileName, string lpExistingFileName, IntPtr lpSecurityAttributes);

    public async Task<List<ModItem>> LoadModsAsync(string modsPath, IEnumerable<string>? favoriteFileNames = null)
    {
        return await Task.Run(() =>
        {
            var list = new List<ModItem>();
            if (!Directory.Exists(modsPath)) return list;

            var favSet = new HashSet<string>(favoriteFileNames ?? Enumerable.Empty<string>(), StringComparer.OrdinalIgnoreCase);
            string disabledDir = Path.Combine(modsPath, "_disabled");

            var allFiles = Directory.GetFiles(modsPath, "*.*", SearchOption.AllDirectories)
                .Where(f =>
                {
                    string ext = Path.GetExtension(f).ToLowerInvariant();
                    return ext is ".zip" or ".rar" or ".7z";
                })
                .ToList();

            var activeFiles = allFiles
                .Where(f => !f.StartsWith(disabledDir, StringComparison.OrdinalIgnoreCase))
                .ToList();

            foreach (string file in activeFiles)
            {
                var item = ParseModZip(file, isEnabled: true);
                item.IsFavorite = favSet.Contains(item.FileName);
                list.Add(item);
            }

            if (Directory.Exists(disabledDir))
            {
                var activeNames = new HashSet<string>(activeFiles.Select(Path.GetFileName)!, StringComparer.OrdinalIgnoreCase);
                var disabledFiles = Directory.GetFiles(disabledDir, "*.*", SearchOption.AllDirectories)
                    .Where(f =>
                    {
                        string ext = Path.GetExtension(f).ToLowerInvariant();
                        return ext is ".zip" or ".rar" or ".7z";
                    });

                foreach (string file in disabledFiles)
                {
                    if (activeNames.Contains(Path.GetFileName(file)))
                        continue;

                    var item = ParseModZip(file, isEnabled: false);
                    item.IsFavorite = favSet.Contains(item.FileName);
                    list.Add(item);
                }
            }

            return list
                .OrderByDescending(m => m.IsFavorite)
                .ThenBy(m => m.DisplayName)
                .ToList();
        });
    }

    private ModItem ParseModZip(string zipPath, bool isEnabled)
    {
        var fi = new FileInfo(zipPath);
        string parentDirName = fi.Directory?.Name ?? "";

        string sourceTag = "📁 Local";
        if (parentDirName.Equals("multiplayer", StringComparison.OrdinalIgnoreCase) ||
            fi.Name.Contains("BeamMP", StringComparison.OrdinalIgnoreCase))
        {
            sourceTag = "🌐 BeamMP";
        }
        else if (parentDirName.Equals("repo", StringComparison.OrdinalIgnoreCase))
        {
            sourceTag = "📦 Repo";
        }

        var item = new ModItem
        {
            FileName = fi.Name,
            FullPath = fi.FullName,
            DisplayName = Path.GetFileNameWithoutExtension(fi.Name),
            SizeBytes = fi.Length,
            SourceTag = sourceTag,
            InstallDate = fi.LastWriteTime.ToString("dd.MM.yyyy HH:mm"),
            IsEnabled = isEnabled
        };

        if (!fi.Extension.Equals(".zip", StringComparison.OrdinalIgnoreCase))
        {
            item.Category = "⚠️ Не .ZIP";
            item.GameId = "Требуется .zip";
            return item;
        }

        try
        {
            using var archive = ZipFile.OpenRead(zipPath);

            bool hasVehicles = false, hasLevels = false, hasLuaOrUi = false, hasSound = false;
            int pcConfigsCount = 0;
            var targetIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var entry in archive.Entries)
            {
                string path = entry.FullName.Replace('\\', '/').TrimStart('/');
                var seg = path.Split('/', StringSplitOptions.RemoveEmptyEntries);

                if (path.EndsWith(".pc", StringComparison.OrdinalIgnoreCase))
                    pcConfigsCount++;

                if (seg.Length >= 2 && seg[0].Equals("vehicles", StringComparison.OrdinalIgnoreCase))
                {
                    hasVehicles = true;
                    if (!seg[1].Equals("common", StringComparison.OrdinalIgnoreCase))
                        targetIds.Add($"vehicles/{seg[1]}");
                }
                else if (seg.Length >= 2 && seg[0].Equals("levels", StringComparison.OrdinalIgnoreCase))
                {
                    hasLevels = true;
                    targetIds.Add($"levels/{seg[1]}");
                }
                else if (path.StartsWith("art/sound/", StringComparison.OrdinalIgnoreCase))
                {
                    hasSound = true;
                }
                else if (path.StartsWith("lua/", StringComparison.OrdinalIgnoreCase) ||
                         path.StartsWith("ui/", StringComparison.OrdinalIgnoreCase) ||
                         path.StartsWith("scripts/", StringComparison.OrdinalIgnoreCase))
                {
                    hasLuaOrUi = true;
                }
            }

            if (sourceTag == "🌐 BeamMP" && fi.Name.Equals("BeamMP.zip", StringComparison.OrdinalIgnoreCase))
                item.Category = "🌐 Клиент MP";
            else if (hasLevels) item.Category = "🗺️ Карта";
            else if (hasVehicles) item.Category = "🚗 Авто";
            else if (hasSound) item.Category = "🔊 Звук";
            else if (hasLuaOrUi) item.Category = "⚙️ Скрипт";
            else item.Category = "📦 Мод";

            item.ConfigsCount = pcConfigsCount;
            item.GameId = targetIds.Count > 0 ? string.Join(", ", targetIds.Take(3)) : "Системный / Скрипт";

            var infoEntries = archive.Entries
                .Where(e => e.Name.Equals("info.json", StringComparison.OrdinalIgnoreCase))
                .OrderBy(e => e.FullName.StartsWith("mod_info/", StringComparison.OrdinalIgnoreCase) ? 0 : 1)
                .ThenBy(e => e.FullName.Count(c => c == '/' || c == '\\'))
                .ToList();

            foreach (var infoEntry in infoEntries)
            {
                try
                {
                    using var stream = infoEntry.Open();
                    using var doc = JsonDocument.Parse(stream, new JsonDocumentOptions
                    {
                        AllowTrailingCommas = true,
                        CommentHandling = JsonCommentHandling.Skip
                    });
                    var root = doc.RootElement;

                    if (item.DisplayName == Path.GetFileNameWithoutExtension(fi.Name))
                    {
                        if (TryGetPropertyIgnoreCase(root, "title", out var titleProp) && !string.IsNullOrWhiteSpace(titleProp.ToString()))
                            item.DisplayName = titleProp.ToString();
                        else if (TryGetPropertyIgnoreCase(root, "Name", out var nameProp) && !string.IsNullOrWhiteSpace(nameProp.ToString()))
                            item.DisplayName = nameProp.ToString();
                    }

                    if (item.Author == "—")
                    {
                        if (TryGetPropertyIgnoreCase(root, "author", out var authProp) && !string.IsNullOrWhiteSpace(authProp.ToString()))
                            item.Author = authProp.ToString();
                        else if (TryGetPropertyIgnoreCase(root, "authors", out var authsProp) && !string.IsNullOrWhiteSpace(authsProp.ToString()))
                            item.Author = authsProp.ToString();
                    }

                    if (item.Version == "—")
                    {
                        if (TryGetPropertyIgnoreCase(root, "version_string", out var verStr) && !string.IsNullOrWhiteSpace(verStr.ToString()))
                            item.Version = verStr.ToString();
                        else if (TryGetPropertyIgnoreCase(root, "version", out var ver) && !string.IsNullOrWhiteSpace(ver.ToString()))
                            item.Version = ver.ToString();
                    }
                }
                catch { }
            }

            var iconEntry = archive.Entries
                .Where(e =>
                    (e.Name.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) ||
                     e.Name.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ||
                     e.Name.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase)) &&
                    e.Length > 0 && e.Length < 8_000_000)
                .OrderBy(e => e.FullName.StartsWith("mod_info/", StringComparison.OrdinalIgnoreCase) ? 0 :
                              e.Name.StartsWith("default.", StringComparison.OrdinalIgnoreCase) ? 1 : 2)
                .FirstOrDefault();

            if (iconEntry != null)
            {
                using var imgStream = iconEntry.Open();
                using var ms = new MemoryStream();
                imgStream.CopyTo(ms);
                ms.Position = 0;

                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.DecodePixelWidth = 480; // Чёткое HD-качество для карточки Инспектора!
                bitmap.StreamSource = ms;
                bitmap.EndInit();
                bitmap.Freeze();
                item.PreviewImage = bitmap;
            }
        }
        catch { }

        return item;
    }

    private static bool TryGetPropertyIgnoreCase(JsonElement element, string propName, out JsonElement value)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var prop in element.EnumerateObject())
            {
                if (prop.Name.Equals(propName, StringComparison.OrdinalIgnoreCase))
                {
                    value = prop.Value;
                    return true;
                }
            }
        }
        value = default;
        return false;
    }

    public void ToggleModState(ModItem mod, string modsPath, bool useHardlinks)
    {
        string disabledDir = Path.Combine(modsPath, "_disabled");
        Directory.CreateDirectory(disabledDir);

        var dirInfo = new DirectoryInfo(disabledDir);
        if ((dirInfo.Attributes & FileAttributes.Hidden) == 0)
            dirInfo.Attributes |= FileAttributes.Hidden;

        if (mod.IsEnabled)
        {
            string targetSubFolder = mod.SourceTag == "🌐 BeamMP"
                ? Path.Combine(modsPath, "multiplayer")
                : modsPath;
            Directory.CreateDirectory(targetSubFolder);

            string targetActivePath = Path.Combine(targetSubFolder, mod.FileName);
            if (File.Exists(mod.FullPath) && !mod.FullPath.Equals(targetActivePath, StringComparison.OrdinalIgnoreCase))
            {
                if (useHardlinks)
                {
                    if (!CreateHardLink(targetActivePath, mod.FullPath, IntPtr.Zero))
                    {
                        try { File.CreateSymbolicLink(targetActivePath, mod.FullPath); }
                        catch { File.Move(mod.FullPath, targetActivePath, overwrite: true); }
                    }
                }
                else
                {
                    File.Move(mod.FullPath, targetActivePath, overwrite: true);
                }
                mod.FullPath = targetActivePath;
            }
        }
        else
        {
            string targetDisabledPath = Path.Combine(disabledDir, mod.FileName);
            if (File.Exists(mod.FullPath))
            {
                if (useHardlinks && File.Exists(targetDisabledPath))
                    File.Delete(mod.FullPath);
                else
                    File.Move(mod.FullPath, targetDisabledPath, overwrite: true);

                mod.FullPath = targetDisabledPath;
            }
        }
    }

    public void DeleteMod(ModItem mod, string modsPath)
    {
        if (File.Exists(mod.FullPath))
            File.Delete(mod.FullPath);

        string disabledCopy = Path.Combine(modsPath, "_disabled", mod.FileName);
        if (File.Exists(disabledCopy))
            File.Delete(disabledCopy);
    }
}