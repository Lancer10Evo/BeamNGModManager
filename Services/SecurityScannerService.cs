using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using SharpCompress.Archives;

namespace BeamNGModManager.Services;

public enum ScanVerdict
{
    VerifiedSafe,      // Все проверки (включая Defender ExitCode == 0) прошли успешно
    UnverifiedWarning, // Угроз в Lua/файлах не найдено, но Defender не смог подтвердить (таймаут / нет Defender) или есть подозрения
    DangerousThreat    // Найдены исполняемые файлы, опасный/обфусцированный Lua-код или вирус по базе Defender
}

public class SecurityScanReport
{
    public string FileName { get; set; } = string.Empty;
    public string SourceOrigin { get; set; } = "Локальный файл с ПК";
    public long FileSizeBytes { get; set; }
    public string Sha256Hash { get; set; } = string.Empty;

    public string DefenderStatus { get; set; } = string.Empty;
    public bool DefenderConfirmedClean { get; set; }
    public bool DefenderDetectedThreat { get; set; }

    public int LuaFilesScanned { get; set; }
    public int NestedArchivesScanned { get; set; }
    public int UiJsFilesCount { get; set; }

    public List<string> DangerousBinariesFound { get; set; } = new();
    public List<string> SuspiciousLuaPatternsFound { get; set; } = new();
    public List<string> Warnings { get; set; } = new();

    public ScanVerdict Verdict
    {
        get
        {
            if (DefenderDetectedThreat || DangerousBinariesFound.Count > 0 || SuspiciousLuaPatternsFound.Count > 0)
                return ScanVerdict.DangerousThreat;

            if (!DefenderConfirmedClean || Warnings.Count > 0)
                return ScanVerdict.UnverifiedWarning;

            return ScanVerdict.VerifiedSafe;
        }
    }

    public string VirusTotalUrl => $"https://www.virustotal.com/gui/file/{Sha256Hash}";

    public string BuildFormattedSummary()
    {
        double mb = FileSizeBytes / 1024.0 / 1024.0;
        var sb = new StringBuilder();
        sb.AppendLine($"🛡️ ОТЧЁТ БЕЗОПАСНОСТИ (Security Audit v2.2)");
        sb.AppendLine($"━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
        sb.AppendLine($"📄 Файл: {FileName} ({mb:F2} MB)");
        sb.AppendLine($"🌐 Источник: {SourceOrigin}");
        if (Sha256Hash.Length >= 32)
            sb.AppendLine($"🔑 SHA-256: {Sha256Hash[..16]}...{Sha256Hash[^16..]}");
        sb.AppendLine();

        sb.AppendLine($"1. Антивирус Microsoft Defender:");
        sb.AppendLine($"   {DefenderStatus}");
        sb.AppendLine();

        sb.AppendLine($"2. Проверка на исполняемые файлы (.exe/.dll/.bat/.ps1/.scr):");
        sb.AppendLine(DangerousBinariesFound.Count == 0
            ? "   ✅ Чисто (включая проверку вложенных архивов)"
            : $"   ❌ ОПАСНО! Найдены бинарные файлы: {string.Join(", ", DangerousBinariesFound.Take(5))}");
        sb.AppendLine();

        sb.AppendLine($"3. Глубокий аудит Lua и UI-скриптов (Lua: {LuaFilesScanned}, Влож. архивов: {NestedArchivesScanned}, UI .js: {UiJsFilesCount}):");
        if (SuspiciousLuaPatternsFound.Count == 0)
            sb.AppendLine("   ✅ Опасных вызовов или обфускации в Lua не обнаружено");
        else
            sb.AppendLine($"   🚨 ОПАСНЫЙ ИЛИ ОБФУСЦИРОВАННЫЙ КОД:\n   • {string.Join("\n   • ", SuspiciousLuaPatternsFound.Take(6))}");

        if (Warnings.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine($"4. Предупреждения инспектора:");
            sb.AppendLine($"   • {string.Join("\n   • ", Warnings.Take(4))}");
        }

        sb.AppendLine($"━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
        switch (Verdict)
        {
            case ScanVerdict.VerifiedSafe:
                sb.AppendLine("✅ ВЕРДИКТ: Проверено Defender и статическим анализатором. Угроз не найдено.");
                break;
            case ScanVerdict.UnverifiedWarning:
                sb.AppendLine("⚠️ ВЕРДИКТ: Статический анализ пройден, но полная антивирусная проверка НЕ подтверждена или источник немодерируемый. Рекомендуется проверить хеш на VirusTotal (кнопка «Нет»).");
                break;
            case ScanVerdict.DangerousThreat:
                sb.AppendLine("🚨 ВЕРДИКТ: ОБНАРУЖЕНА УГРОЗА БЕЗОПАСНОСТИ! Не устанавливайте этот архив!");
                break;
        }

        return sb.ToString();
    }
}

public class SecurityScannerService
{
    private const int MaxNestedScanDepth = 3;

    // Убрано .js (легально используется в UI BeamNG), оставлены только реальные исполняемые форматы ОС
    private static readonly HashSet<string> ForbiddenBinaryExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".exe", ".dll", ".bat", ".cmd", ".ps1", ".vbs", ".vbe", ".jse", ".wsf", ".scr", ".msi", ".com", ".hta", ".pif", ".cpl"
    };

    // Прямые опасные вызовы + сигнатуры обфускации и динамического выполнения в Lua
    private static readonly (string Pattern, string Description)[] DangerousLuaSignatures =
    {
        (@"os\.execute\s*\(", "os.execute() — прямой запуск системных команд ОС"),
        (@"io\.popen\s*\(", "io.popen() — скрытый запуск процессов"),
        (@"package\.loadlib\s*\(", "package.loadlib() — загрузка внешней DLL"),
        (@"ffi\.load\s*\(", "ffi.load() — загрузка нативной DLL через LuaJIT FFI"),
        (@"ffi\.C\.", "ffi.C — прямой вызов функций Win32 API"),
        (@"powershell|cmd\.exe|wscript|cscript|mshta|curl\.exe|certutil", "Попытка вызова системных утилит Windows"),
        (@"socket\.http|http\.request|luasec", "Сетевой запрос из Lua к внешнему серверу"),
        (@"loadstring\s*\(|(?<![a-zA-Z0-9_])load\s*\(", "Динамическая компиляция скрытого кода (load / loadstring)"),
        (@"_G\s*\[|rawget\s*\(\s*_G", "Динамический обход глобальной таблицы _G[...] (признак обфускации)"),
        (@"setfenv\s*\(|getfenv\s*\(", "Манипуляция окружением функций (setfenv / getfenv)"),
        (@"string\.char\s*\((?:\s*\d+\s*,){5,}", "Сборка скрытых строк через string.char(...) (обфускация)"),
        (@"(?:\\[0-9]{2,3}){8,}|(?:\\x[0-9a-fA-F]{2}){8,}", "Обфусцированная байтовая/hex строка в коде Lua"),
        (@"\\27Lua|\x1bLua", "Скрытый прекомпилированный байткод Lua")
    };

    private static readonly (string Pattern, string Description)[] DangerousJsSignatures =
    {
        (@"ActiveXObject|WScript\.Shell", "Попытка вызова системного COM/ActiveX из JS"),
        (@"child_process|require\s*\(\s*['""]fs['""]", "Попытка доступа к файловой системе / процессам из JS")
    };

    public async Task<SecurityScanReport> ScanArchiveAsync(string archivePath, string? sourceDomain = null)
    {
        return await Task.Run(() =>
        {
            var fi = new FileInfo(archivePath);
            var report = new SecurityScanReport
            {
                FileName = fi.Name,
                FileSizeBytes = fi.Length,
                SourceOrigin = FormatSourceOrigin(sourceDomain, out string? sourceWarning),
                Sha256Hash = ComputeSha256(archivePath)
            };

            if (!string.IsNullOrEmpty(sourceWarning))
                report.Warnings.Add(sourceWarning);

            // 1. Честная проверка через антивирус Microsoft Defender (без тихого падения и без ложного "passed = true")
            RunWindowsDefenderScan(archivePath, report);

            // 2. Рекурсивный аудит архива (включая вложенные .zip/.rar/.7z "матрёшки"!)
            try
            {
                using var fs = File.OpenRead(archivePath);
                ScanArchiveStreamRecursive(fs, fi.Name, report, currentDepth: 0);
            }
            catch (Exception ex)
            {
                report.Warnings.Add($"Не удалось полностью прочитать структуру архива: {ex.Message}");
            }

            return report;
        });
    }

    private static string FormatSourceOrigin(string? domain, out string? warning)
    {
        warning = null;
        if (string.IsNullOrWhiteSpace(domain))
            return "📁 Локальный файл с ПК";

        if (domain.Contains("beamng.com", StringComparison.OrdinalIgnoreCase))
            return "🟢 Официальный репозиторий BeamNG.com";

        if (domain.Contains("nexusmods.com", StringComparison.OrdinalIgnoreCase) ||
            domain.Contains("nexus-cdn.com", StringComparison.OrdinalIgnoreCase))
            return "🟡 Nexus Mods (проверяйте описание автора)";

        if (domain.Contains("modsfire.com", StringComparison.OrdinalIgnoreCase) ||
            domain.Contains("sharemods.com", StringComparison.OrdinalIgnoreCase) ||
            domain.Contains("modsbase.com", StringComparison.OrdinalIgnoreCase) ||
            domain.Contains("modsup.com", StringComparison.OrdinalIgnoreCase))
        {
            warning = $"Файл скачан с немодерируемого файлообменника ({domain}). Рекомендуется сверить SHA-256 на VirusTotal.";
            return $"🟠 Внешний файлообменник ({domain})";
        }

        warning = $"Сторонний портал модов ({domain}) — архивы могут быть перезаливами.";
        return $"🟡 Сторонний портал ({domain})";
    }

    private void ScanArchiveStreamRecursive(Stream archiveStream, string containerName, SecurityScanReport report, int currentDepth)
    {
        if (currentDepth > MaxNestedScanDepth)
        {
            report.Warnings.Add($"Превышена глубина вложенности архивов (>{MaxNestedScanDepth}) внутри {containerName}.");
            return;
        }

        using var archive = ArchiveFactory.OpenArchive(archiveStream);
        foreach (var entry in archive.Entries.Where(e => !e.IsDirectory))
        {
            string entryName = entry.Key?.Replace('\\', '/') ?? "";
            string ext = Path.GetExtension(entryName).ToLowerInvariant();
            string displayPath = currentDepth == 0 ? entryName : $"{containerName} -> {entryName}";

            // 1. Проверка на запрещённые исполняемые файлы (.exe, .dll, .bat, .ps1 и т.д.)
            if (ForbiddenBinaryExtensions.Contains(ext))
            {
                report.DangerousBinariesFound.Add(displayPath);
                continue;
            }

            // 2. Рекурсивная проверка вложенных архивов ("матрёшек" .zip, .rar, .7z)
            if (ext is ".zip" or ".rar" or ".7z")
            {
                if (entry.Size > 0 && entry.Size < 1024L * 1024 * 1024) // до 1 ГБ в памяти/потоке
                {
                    report.NestedArchivesScanned++;
                    try
                    {
                        using var entryStream = entry.OpenEntryStream();
                        using var ms = new MemoryStream();
                        entryStream.CopyTo(ms);
                        ms.Position = 0;
                        ScanArchiveStreamRecursive(ms, Path.GetFileName(entryName), report, currentDepth + 1);
                    }
                    catch (Exception ex)
                    {
                        report.Warnings.Add($"Не удалось просканировать вложенный архив {displayPath}: {ex.Message}");
                    }
                }
                else if (entry.Size >= 1024L * 1024 * 1024)
                {
                    report.Warnings.Add($"Вложенный архив {displayPath} слишком велик для проверки в памяти (>1 ГБ).");
                }
                continue;
            }

            // 3. Проверка всех .lua файлов (включая файлы > 2 МБ!)
            if (ext == ".lua" && entry.Size > 0)
            {
                report.LuaFilesScanned++;
                if (entry.Size > 2_000_000)
                {
                    report.Warnings.Add($"Аномально большой Lua-скрипт ({entry.Size / 1024 / 1024.0:F1} MB): {displayPath}");
                }

                // Читаем и сканируем до 15 МБ текста Lua-скрипта
                if (entry.Size <= 15_000_000)
                {
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
                continue;
            }

            // 4. Спокойная проверка .js файлов (без ложных срабатываний на легальный UI BeamNG)
            if (ext == ".js" && entry.Size > 0 && entry.Size <= 5_000_000)
            {
                report.UiJsFilesCount++;
                using var stream = entry.OpenEntryStream();
                using var reader = new StreamReader(stream);
                string jsCode = reader.ReadToEnd();

                foreach (var (pattern, desc) in DangerousJsSignatures)
                {
                    if (Regex.IsMatch(jsCode, pattern, RegexOptions.IgnoreCase))
                    {
                        report.SuspiciousLuaPatternsFound.Add($"{Path.GetFileName(entryName)} (JS): {desc}");
                    }
                }
            }
        }
    }

    /// <summary>
    /// Честный запуск Microsoft Defender без сокрытия таймаутов и ошибок:
    /// DefenderConfirmedClean = true ставится ТОЛЬКО если процесс реально завершился с кодом 0!
    /// </summary>
    private static void RunWindowsDefenderScan(string filePath, SecurityScanReport report)
    {
        try
        {
            string? mpCmdRun = FindDefenderExecutable();
            if (mpCmdRun == null)
            {
                report.DefenderConfirmedClean = false;
                report.DefenderStatus = "⚠️ Сканер Microsoft Defender (MpCmdRun.exe) не найден на ПК — антивирусная проверка НЕ выполнялась.";
                return;
            }

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
            {
                report.DefenderConfirmedClean = false;
                report.DefenderStatus = "⚠️ Не удалось запустить процесс MpCmdRun.exe — статус не подтверждён.";
                return;
            }

            // Ждём до 120 секунд (для крупных архивов 350–500 МБ)
            bool exited = proc.WaitForExit(120_000);
            if (!exited)
            {
                try { proc.Kill(); } catch { }
                report.DefenderConfirmedClean = false;
                report.DefenderStatus = "⚠️ НЕ ЗАВЕРШЕНО: Сканирование Defender превысило 120 сек. (файл слишком большой). Проверьте хеш на VirusTotal!";
                return;
            }

            int code = proc.ExitCode;
            if (code == 0)
            {
                report.DefenderConfirmedClean = true;
                report.DefenderStatus = "✅ Microsoft Defender: Проверка успешно завершена, угроз нет (Код 0)";
            }
            else if (code == 2)
            {
                report.DefenderConfirmedClean = false;
                report.DefenderDetectedThreat = true;
                report.DefenderStatus = "🚨 Microsoft Defender: ОБНАРУЖЕНА ВРЕДОНОСНАЯ ПРОГРАММА (Код 2)!";
            }
            else
            {
                report.DefenderConfirmedClean = false;
                report.DefenderStatus = $"⚠️ Microsoft Defender вернул нестандартный код ({code}) — безопасность НЕ подтверждена.";
            }
        }
        catch (Exception ex)
        {
            report.DefenderConfirmedClean = false;
            report.DefenderStatus = $"⚠️ Ошибка вызова Microsoft Defender ({ex.Message}) — статус НЕ подтверждён.";
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