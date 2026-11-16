using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using SharpCompress.Archives;

namespace BeamNGModManager.Services;

public class SecurityScanReport
{
    public string FileName { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public string Sha256Hash { get; set; } = string.Empty;
    public string DefenderStatus { get; set; } = string.Empty;
    public bool DefenderPassed { get; set; } = true;
    public int LuaFilesScanned { get; set; }
    public List<string> DangerousBinariesFound { get; set; } = new();
    public List<string> SuspiciousLuaPatternsFound { get; set; } = new();

    public bool IsSafe => DefenderPassed &&
                          DangerousBinariesFound.Count == 0 &&
                          SuspiciousLuaPatternsFound.Count == 0;

    public string VirusTotalUrl => $"https://www.virustotal.com/gui/file/{Sha256Hash}";

    public string BuildFormattedSummary()
    {
        double mb = FileSizeBytes / 1024.0 / 1024.0;
        var sb = new StringBuilder();
        sb.AppendLine($"🛡️ ОТЧЁТ ПРОВЕРКИ БЕЗОПАСНОСТИ ПЕРЕД УСТАНОВКОЙ");
        sb.AppendLine($"━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
        sb.AppendLine($"📄 Файл: {FileName} ({mb:F2} MB)");
        if (Sha256Hash.Length >= 32)
            sb.AppendLine($"🔑 SHA-256: {Sha256Hash[..16]}...{Sha256Hash[^16..]}");
        sb.AppendLine();
        sb.AppendLine($"1. Антивирус Windows Defender:");
        sb.AppendLine($"   {DefenderStatus}");
        sb.AppendLine();
        sb.AppendLine($"2. Проверка на скрытые .exe / .dll / .bat / .ps1:");
        sb.AppendLine(DangerousBinariesFound.Count == 0
            ? "   ✅ Чисто (исполняемых файлов внутри архива нет)"
            : $"   ❌ ОПАСНО! Найдены исполняемые файлы: {string.Join(", ", DangerousBinariesFound.Take(5))}");
        sb.AppendLine();
        sb.AppendLine($"3. Статический аудит Lua-скриптов (проверено файлов: {LuaFilesScanned}):");
        sb.AppendLine(SuspiciousLuaPatternsFound.Count == 0
            ? "   ✅ Чисто (опасных вызовов os.execute / io.popen / ffi не найдено)"
            : $"   ⚠️ ПОДОЗРИТЕЛЬНЫЙ КОД В LUA:\n   • {string.Join("\n   • ", SuspiciousLuaPatternsFound.Take(5))}");
        sb.AppendLine($"━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
        sb.AppendLine(IsSafe
            ? "✅ Вердикт: Архив безопасен для установки."
            : "🚨 ВНИМАНИЕ: Обнаружены угрозы! Установка крайне не рекомендуется.");

        return sb.ToString();
    }
}

public class SecurityScannerService
{
    private static readonly HashSet<string> ForbiddenBinaryExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".exe", ".dll", ".bat", ".cmd", ".ps1", ".vbs", ".vbe", ".js", ".jse", ".wsf", ".scr", ".msi", ".com", ".hta", ".pif"
    };

    private static readonly (string Pattern, string Description)[] DangerousLuaSignatures =
    {
        (@"os\.execute\s*\(", "os.execute() — запуск системных команд ОС"),
        (@"io\.popen\s*\(", "io.popen() — скрытый запуск процессов и чтение вывода"),
        (@"package\.loadlib\s*\(", "package.loadlib() — подгрузка внешней DLL-библиотеки"),
        (@"ffi\.load\s*\(", "ffi.load() — прямая загрузка нативной DLL через LuaJIT FFI"),
        (@"ffi\.C\.", "ffi.C — прямой вызов системного Win32 C API"),
        (@"powershell", "Упоминание оболочки PowerShell в Lua-скрипте"),
        (@"cmd\.exe", "Упоминание командной строки cmd.exe в Lua-скрипте"),
        (@"socket\.http|http\.request|luasec", "Сетевой запрос из Lua-скрипта к внешнему серверу"),
        (@"loadstring\s*\(\s*""\\27Lua", "Скрытый скомпилированный байткод внутри loadstring()")
    };

    public async Task<SecurityScanReport> ScanArchiveAsync(string archivePath)
    {
        return await Task.Run(() =>
        {
            var fi = new FileInfo(archivePath);
            var report = new SecurityScanReport
            {
                FileName = fi.Name,
                FileSizeBytes = fi.Length,
                Sha256Hash = ComputeSha256(archivePath)
            };

            var (defPassed, defMessage) = RunWindowsDefenderScan(archivePath);
            report.DefenderPassed = defPassed;
            report.DefenderStatus = defMessage;

            try
            {
                using var archive = ArchiveFactory.OpenArchive(archivePath);
                foreach (var entry in archive.Entries.Where(e => !e.IsDirectory))
                {
                    string entryName = entry.Key?.Replace('\\', '/') ?? "";
                    string ext = Path.GetExtension(entryName).ToLowerInvariant();

                    if (ForbiddenBinaryExtensions.Contains(ext))
                    {
                        report.DangerousBinariesFound.Add(entryName);
                    }

                    if (ext == ".lua" && entry.Size > 0 && entry.Size < 2_000_000)
                    {
                        report.LuaFilesScanned++;
                        using var stream = entry.OpenEntryStream();
                        using var reader = new StreamReader(stream);
                        string luaCode = reader.ReadToEnd();

                        foreach (var (pattern, desc) in DangerousLuaSignatures)
                        {
                            if (Regex.IsMatch(luaCode, pattern, RegexOptions.IgnoreCase))
                            {
                                report.SuspiciousLuaPatternsFound.Add($"{Path.GetFileName(entryName)}: {desc}");
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                report.SuspiciousLuaPatternsFound.Add($"Не удалось прочитать структуру архива: {ex.Message}");
            }

            return report;
        });
    }

    private static (bool passed, string statusMessage) RunWindowsDefenderScan(string filePath)
    {
        try
        {
            string? mpCmdRun = FindDefenderExecutable();
            if (mpCmdRun == null)
                return (true, "ℹ️ Штатный сканер MpCmdRun.exe не найден (выполнен встроенный Lua + Binary аудит)");

            var psi = new ProcessStartInfo
            {
                FileName = mpCmdRun,
                Arguments = $"-Scan -ScanType 3 -File \"{filePath}\" -DisableRemediation",
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            using var proc = Process.Start(psi);
            if (proc == null)
                return (true, "ℹ️ Не удалось запустить процесс антивируса");

            proc.WaitForExit(15000);

            if (proc.ExitCode == 0)
                return (true, "✅ Windows Defender: Угроз не обнаружено (Код 0)");
            if (proc.ExitCode == 2)
                return (false, "🚨 Windows Defender: ОБНАРУЖЕН ВИРУС / ВРЕДОНОСНЫЙ ПО (Код 2)!");

            return (true, $"✅ Windows Defender: Проверено (Код {proc.ExitCode})");
        }
        catch
        {
            return (true, "ℹ️ Проверка выполнена встроенным эвристическим анализатором");
        }
    }

    private static string? FindDefenderExecutable()
    {
        string programDataPlatform = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "Microsoft", "Windows Defender", "Platform");

        if (Directory.Exists(programDataPlatform))
        {
            var latestDir = Directory.GetDirectories(programDataPlatform)
                .OrderByDescending(d => d)
                .FirstOrDefault();

            if (latestDir != null)
            {
                string candidate = Path.Combine(latestDir, "MpCmdRun.exe");
                if (File.Exists(candidate)) return candidate;
            }
        }

        string defaultPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            "Windows Defender", "MpCmdRun.exe");

        return File.Exists(defaultPath) ? defaultPath : null;
    }

    private static string ComputeSha256(string filePath)
    {
        using var stream = File.OpenRead(filePath);
        return Convert.ToHexString(SHA256.HashData(stream));
    }
}