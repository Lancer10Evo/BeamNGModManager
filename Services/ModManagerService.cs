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

    public async Task<List<ModItem>> LoadModsAsync(string modsPath)
    {
        return await Task.Run(() =>
        {
            var list = new List<ModItem>();
            if (!Directory.Exists(modsPath)) return list;

            string disabledDir = Path.Combine(modsPath, "_disabled");

            // 1. Сканируем ВСЕ подпапки внутри mods/ (включая repo, multiplayer и т.д.), кроме _disabled
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
                list.Add(ParseModZip(file, isEnabled: true));

            // 2. Сканируем отключённые моды в папке _disabled
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

                    list.Add(ParseModZip(file, isEnabled: false));
                }
            }

            return list.OrderBy(m => m.DisplayName).ToList();
        });
    }

    private ModItem ParseModZip(string zipPath, bool isEnabled)
    {
        var fi = new FileInfo(zipPath);
        var item = new ModItem
        {
            FileName = fi.Name,
            FullPath = fi.FullName,
            DisplayName = Path.GetFileNameWithoutExtension(fi.Name),
            SizeBytes = fi.Length,
            IsEnabled = isEnabled
        };

        if (!fi.Extension.Equals(".zip", StringComparison.OrdinalIgnoreCase))
            return item;

        try
        {
            using var archive = ZipFile.OpenRead(zipPath);

            // 1. Сначала ищем mod_info/<id>/info.json, а затем vehicles/<car>/info.json или levels/<map>/info.json
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

            // 2. Ищем превью-картинку (в mod_info/, vehicles/*/default.png|jpg или levels/*)
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
                bitmap.DecodePixelWidth = 64;
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
            string targetActivePath = Path.Combine(modsPath, mod.FileName);
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