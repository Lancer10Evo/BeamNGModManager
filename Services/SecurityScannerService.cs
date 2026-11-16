using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using SharpCompress.Archives;

namespace BeamNGModManager.Services;

public enum ScanVerdict
{
    VerifiedSafe,      // Все проверки (включая Defender ExitCode == 0) чистые
    UnverifiedWarning, // Критических угроз нет, но есть эвристические предупреждения или таймаут Defender
    DangerousThreat    // Найдены бинарники (.exe/.dll/.bat), вирус Defender или прямой эксплойт в Lua -> УСТАНОВКА БЛОКИРУЕТСЯ
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
    public List<string> CriticalLuaExploitsFound { get; set; } = new();
    public List<string> HeuristicWarnings { get; set; } = new();

    public ScanVerdict Verdict
    {
        get
        {
            if (DefenderDetectedThreat || DangerousBinariesFound.Count > 0 || CriticalLuaExploitsFound.Count > 0)
                return ScanVerdict.DangerousThreat;

            if (!DefenderConfirmedClean || HeuristicWarnings.Count > 0)
                return ScanVerdict.UnverifiedWarning;

            return ScanVerdict.VerifiedSafe;
        }
    }

    public string VirusTotalUrl => $"https://www.virustotal.com/gui/file/{Sha256Hash}";

    public string BuildFormattedSummary()
    {
        double mb = FileSizeBytes / 1024.0 / 1024.0;
        var sb = new StringBuilder();
        sb.AppendLine($"🛡️ ОТЧЁТ БЕЗОПАСНОСТИ (Security Audit v2.3)");
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
            : $"   ❌ БЛОКИРОВКА! Найдены исполняемые файлы: {string.Join(", ", DangerousBinariesFound.Take(5))}");
        sb.AppendLine();

        sb.AppendLine($"3. Аудит скриптов (Lua: {LuaFilesScanned}, Влож. архивов: {NestedArchivesScanned}, UI .js: {UiJsFilesCount}):");
        if (CriticalLuaExploitsFound.Count == 0)
            sb.AppendLine("   ✅ Критических эксплойтов (os.execute / io.popen / ffi.load) не найдено");
        else
            sb.AppendLine($"   🚨 БЛОКИРОВКА! ОПАСНЫЙ КОД В LUA:\n   • {string.Join("\n   • ", CriticalLuaExploitsFound.Take(5))}");

        if (HeuristicWarnings.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine($"4. Эвристические замечания (не блокируют установку):");
            sb.AppendLine($"   ⚠️ {string.Join("\n   ⚠️ ", HeuristicWarnings.Take(5))}");
        }

        sb.AppendLine($"━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
        switch (Verdict)
        {
            case ScanVerdict.VerifiedSafe:
                sb.AppendLine("✅ ВЕРДИКТ: Проверено Defender и анализатором кода. Угроз не найдено.");
                break;
            case ScanVerdict.UnverifiedWarning:
                sb.AppendLine("⚠️ ВЕРДИКТ: Прямых угроз нет, но есть эвристические замечания или антивирус не подтвердил статус. Рекомендуется сверить хеш на VirusTotal.");
                break;
            case ScanVerdict.DangerousThreat:
                sb.AppendLine("⛔ ВЕРДИКТ: УСТАНОВКА ЗАБЛОКИРОВАНА! В архиве обнаружено вредоносное содержимое.");
                break;
        }

        return sb.ToString();
    }
}

public class SecurityScannerService
{
    private const int MaxNestedScanDepth = 3;
    private const long MaxSingleNestedArchiveBytes = 256L * 1024 * 1024; // 256 MB лимит на вложенный архив
    private const long MaxTotalNestedQuotaBytes = 512L * 1024 * 1024;    // 512 MB общий бюджет на диск (0 МБ в RAM)

    private static readonly HashSet<string> ForbiddenBinaryExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".exe", ".dll", ".bat", ".cmd", ".ps1", ".vbs", ".vbe", ".jse", ".wsf", ".scr", ".msi", ".com", ".hta", ".pif", ".cpl"
    };

    // УРОВЕНЬ 1 (БЛОКИРУЮЩИЙ): Прямые эксплойты, выполнение команд ОС, загрузка DLL и скрытый байткод
    private static readonly (string Pattern, string Description)[] CriticalLuaExploitSignatures =
    {
        (@"os\.execute\s*\(", "os.execute() — прямой запуск системных команд ОС"),
        (@"io\.popen\s*\(", "io.popen() — скрытый запуск процессов ОС"),
        (@"package\.loadlib\s*\(", "package.loadlib() — подгрузка внешней DLL"),
        (@"ffi\.load\s*\(", "ffi.load() — загрузка нативной DLL через LuaJIT FFI"),
        (@"powershell(?:\.exe)?|cmd\.exe|wscript(?:\.exe)?|cscript(?:\.exe)?|mshta(?:\.exe)?|certutil", "Вызов системных утилит Windows из Lua"),
        (@"\\27Lua|\x1bLua", "Скрытый прекомпилированный байткод Lua")
    };

    // УРОВЕНЬ 2 (ПРЕДУПРЕЖДЕНИЕ): Динамические конструкции, которые встречаются и в сложных легальных модах, и при обфускации
    private static readonly (string Pattern, string Description)[] HeuristicLuaWarningSignatures =
    {
        (@"socket\.http|http\.request|luasec", "Сетевое обращение из Lua (проверьте, нужен ли моду интернет)"),
        (@"ffi\.C\.", "Обращение к структурам C (ffi.C)"),
        (@"loadstring\s*\(|(?<![a-zA-Z0-9_\.])load\s*\(", "Динамическая компиляция строк (load / loadstring)"),
        (@"_G\s*\[|rawget\s*\(\s*_G", "Динамическое обращение к глобальной таблице _G[...]"),
        (@"setfenv\s*\(|getfenv\s*\(", "Изменение окружения функций (setfenv / getfenv)"),
        (@"string\.char\s*\((?:\s*\d+\s*,){8,}", "Длинная цепочка string.char(...) (возможна обфускация)"),
        (@"(?:\\[0-9]{2,3}){12,}|(?:\\x[0-9a-fA-F]{2}){12,}", "Длинная экранированная байтовая/hex строка в Lua")
    };

    private static readonly (string Pattern, string Description)[] CriticalJsSignatures =
    {
        (@"ActiveXObject|WScript\.Shell", "Вызов системного COM/ActiveX из UI-скрипта"),
        (@"child_process", "Попытка запуска дочернего процесса из JS")
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
                report.HeuristicWarnings.Add(sourceWarning);

            RunWindowsDefenderScan(archivePath, report);

            try
            {
                long nestedBudgetUsed = 0;
                using var fs = File.OpenRead(archivePath);
                ScanArchiveStreamRecursive(fs, fi.Name, report, currentDepth: 0, ref nestedBudgetUsed);
            }
            catch (Exception ex)
            {
                report.HeuristicWarnings.Add($"Не удалось полностью прочитать структуру архива: {ex.Message}");
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
            return "🟡 Nexus Mods";

        if (domain.Contains("modsfire.com", StringComparison.OrdinalIgnoreCase) ||
            domain.Contains("sharemods.com", StringComparison.OrdinalIgnoreCase) ||
            domain.Contains("cloudflarestorage.com", StringComparison.OrdinalIgnoreCase))
        {
            warning = $"Файл загружен через файловое зеркало ({domain}). Сверьте хеш на VirusTotal.";
            return $"🟠 Файловое зеркало ({domain})";
        }

        warning = $"Сторонний портал модов ({domain}).";
        return $"🟡 Сторонний портал ({domain})";
    }

    private void ScanArchiveStreamRecursive(
        Stream archiveStream,
        string containerName,
        SecurityScanReport report,
        int currentDepth,
        ref long nestedBudgetUsed)
    {
        if (currentDepth > MaxNestedScanDepth)
        {
            report.HeuristicWarnings.Add($"Превышена глубина вложенности архивов (>{MaxNestedScanDepth}) в {containerName}.");
            return;
        }

        using var archive = ArchiveFactory.OpenArchive(archiveStream);
        foreach (var entry in archive.Entries.Where(e => !e.IsDirectory))
        {
            string entryName = entry.Key?.Replace('\\', '/') ?? "";
            string ext = Path.GetExtension(entryName).ToLowerInvariant();
            string displayPath = currentDepth == 0 ? entryName : $"{containerName} -> {entryName}";

            if (ForbiddenBinaryExtensions.Contains(ext))
            {
                report.DangerousBinariesFound.Add(displayPath);
                continue;
            }

            // Потоковая проверка вложенных архивов через временный файл на диске с FileOptions.DeleteOnClose (0 МБ нагрузки на RAM!)
            if (ext is ".zip" or ".rar" or ".7z")
            {
                if (entry.Size <= 0) continue;

                if (entry.Size > MaxSingleNestedArchiveBytes || (nestedBudgetUsed + entry.Size) > MaxTotalNestedQuotaBytes)
                {
                    report.HeuristicWarnings.Add($"Вложенный архив {displayPath} ({entry.Size / 1024 / 1024} MB) превышает квоту вложенной проверки.");
                    continue;
                }

                nestedBudgetUsed += entry.Size;
                report.NestedArchivesScanned++;

                string tempNestedFile = Path.Combine(Path.GetTempPath(), "BeamNG_NestedScan_" + Guid.NewGuid().ToString("N") + ext);
                try
                {
                    using var entryStream = entry.OpenEntryStream();
                    using var tempFs = new FileStream(
                        tempNestedFile,
                        FileMode.CreateNew,
                        FileAccess.ReadWrite,
                        FileShare.None,
                        bufferSize: 81920,
                        FileOptions.DeleteOnClose);

                    entryStream.CopyTo(tempFs);
                    tempFs.Position = 0;
                    ScanArchiveStreamRecursive(tempFs, Path.GetFileName(entryName), report, currentDepth + 1, ref nestedBudgetUsed);
                }
                catch (Exception ex)
                {
                    report.HeuristicWarnings.Add($"Не удалось просканировать вложенный архив {displayPath}: {ex.Message}");
                }
                continue;
            }

            // Двухуровневая проверка .lua скриптов с предварительной очисткой комментариев
            if (ext == ".lua" && entry.Size > 0)
            {
                report.LuaFilesScanned++;
                if (entry.Size > 2_000_000)
                {
                    report.HeuristicWarnings.Add($"Крупный Lua-скрипт ({entry.Size / 1024 / 1024.0:F1} MB): {displayPath}");
                }

                if (entry.Size <= 15_000_000)
                {
                    using var stream = entry.OpenEntryStream();
                    using var reader = new StreamReader(stream);
                    string rawLua = reader.ReadToEnd();
                    string cleanLua = StripLuaComments(rawLua);

                    foreach (var (pattern, desc) in CriticalLuaExploitSignatures)
                    {
                        if (Regex.IsMatch(cleanLua, pattern, RegexOptions.IgnoreCase))
                        {
                            report.CriticalLuaExploitsFound.Add($"{Path.GetFileName(entryName)}: {desc}");
                        }
                    }

                    foreach (var (pattern, desc) in HeuristicLuaWarningSignatures)
                    {
                        if (Regex.IsMatch(cleanLua, pattern, RegexOptions.IgnoreCase))
                        {
                            report.HeuristicWarnings.Add($"{Path.GetFileName(entryName)}: {desc}");
                        }
                    }
                }
                continue;
            }

            if (ext == ".js" && entry.Size > 0 && entry.Size <= 5_000_000)
            {
                report.UiJsFilesCount++;
                using var stream = entry.OpenEntryStream();
                using var reader = new StreamReader(stream);
                string jsCode = reader.ReadToEnd();

                foreach (var (pattern, desc) in CriticalJsSignatures)
                {
                    if (Regex.IsMatch(jsCode, pattern, RegexOptions.IgnoreCase))
                    {
                        report.CriticalLuaExploitsFound.Add($"{Path.GetFileName(entryName)} (JS): {desc}");
                    }
                }
            }
        }
    }

    private static string StripLuaComments(string luaCode)
    {
        // Удаляем блочные комментарии --[[ ... ]] и однострочные комментарии --...
        string noBlock = Regex.Replace(luaCode, @"--\[\[[\s\S]*?\]\]", "", RegexOptions.Multiline);
        return Regex.Replace(noBlock, @"--[^\r\n]*", "");
    }

    private static void RunWindowsDefenderScan(string filePath, SecurityScanReport report)
    {
        try
        {
            string? mpCmdRun = FindDefenderExecutable();
            if (mpCmdRun == null)
            {
                report.DefenderConfirmedClean = false;
                report.DefenderStatus = "⚠️ Сканер Microsoft Defender (MpCmdRun.exe) не найден — антивирусная проверка НЕ выполнялась.";
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

            bool exited = proc.WaitForExit(120_000);
            if (!exited)
            {
                try { proc.Kill(); } catch { }
                report.DefenderConfirmedClean = false;
                report.DefenderStatus = "⚠️ НЕ ЗАВЕРШЕНО: Сканирование Defender превысило 120 сек. Проверьте хеш на VirusTotal!";
                return;
            }

            int code = proc.ExitCode;
            if (code == 0)
            {
                report.DefenderConfirmedClean = true;
                report.DefenderStatus = "✅ Microsoft Defender: Проверка завершена, угроз нет (Код 0)";
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
                report.DefenderStatus = $"⚠️ Microsoft Defender вернул код ({code}) — безопасность НЕ подтверждена.";
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
