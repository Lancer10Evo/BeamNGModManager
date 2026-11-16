using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using SharpCompress.Archives;

namespace BeamNGModManager.Services;

public enum ScanVerdict
{
    VerifiedSafe,
    UnverifiedWarning,
    DangerousThreat
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
        sb.AppendLine($"🛡️ ОТЧЁТ БЕЗОПАСНОСТИ (Security Audit v2.4)");
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

        sb.AppendLine($"3. Лексический аудит и деобфускация Lua (Lua: {LuaFilesScanned}, Влож.: {NestedArchivesScanned}, JS: {UiJsFilesCount}):");
        if (CriticalLuaExploitsFound.Count == 0)
            sb.AppendLine("   ✅ Критических эксплойтов (включая скобочные вызовы и склейку строк) не найдено");
        else
            sb.AppendLine($"   🚨 БЛОКИРОВКА! ОПАСНЫЙ КОД В LUA:\n   • {string.Join("\n   • ", CriticalLuaExploitsFound.Take(5))}");

        if (HeuristicWarnings.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine($"4. Эвристические замечания:");
            sb.AppendLine($"   ⚠️ {string.Join("\n   ⚠️ ", HeuristicWarnings.Take(5))}");
        }

        sb.AppendLine($"━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
        switch (Verdict)
        {
            case ScanVerdict.VerifiedSafe:
                sb.AppendLine("✅ ВЕРДИКТ: Проверено Defender и лексическим анализатором. Угроз не найдено.");
                break;
            case ScanVerdict.UnverifiedWarning:
                sb.AppendLine("⚠️ ВЕРДИКТ: Прямых эксплойтов нет, но статус Defender не подтверждён или есть эвристические замечания. Рекомендуется проверить хеш на VirusTotal.");
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
    private const long MaxSingleNestedArchiveBytes = 256L * 1024 * 1024;
    private const long MaxTotalNestedQuotaBytes = 512L * 1024 * 1024;

    private static readonly HashSet<string> ForbiddenBinaryExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".exe", ".dll", ".bat", ".cmd", ".ps1", ".vbs", ".vbe", ".jse", ".wsf", ".scr", ".msi", ".com", ".hta", ".pif", ".cpl"
    };

    // УРОВЕНЬ 1 (БЛОКИРУЮЩИЙ): Работает поверх деобфусцированного и нормализованного кода
    private static readonly (string Pattern, string Description)[] CriticalLuaExploitSignatures =
    {
        (@"(?:os|_G\.os|rawget\s*\(\s*_G\s*,\s*[""']os[""']\s*\))\s*\.\s*execute", "Вызов os.execute (прямой или через таблицу/скобки)"),
        (@"(?:io|_G\.io|rawget\s*\(\s*_G\s*,\s*[""']io[""']\s*\))\s*\.\s*popen", "Вызов io.popen (скрытый запуск процессов)"),
        (@"(?:package|_G\.package)\s*\.\s*loadlib", "Вызов package.loadlib (загрузка внешней DLL)"),
        (@"ffi\s*\.\s*load\b", "Вызов ffi.load (загрузка нативной DLL через FFI)"),
        (@"[""'](?:execute|popen|loadlib)[""']\s*\]", "Обращение к запрещённому методу execute/popen/loadlib по ключу таблицы"),
        (@"powershell(?:\.exe)?|cmd\.exe|wscript(?:\.exe)?|cscript(?:\.exe)?|mshta(?:\.exe)?|certutil", "Вызов системных интерпретаторов Windows"),
        (@"\\27Lua|\x1bLua", "Скрытый прекомпилированный байткод Lua"),
        (@"(?:loadstring|(?<![a-zA-Z0-9_\.])load)\s*\([^)]*string\.char", "Динамическое выполнение обфусцированного массива string.char (...)")
    };

    // УРОВЕНЬ 2 (ПРЕДУПРЕЖДЕНИЕ)
    private static readonly (string Pattern, string Description)[] HeuristicLuaWarningSignatures =
    {
        (@"socket\.http|http\.request|luasec", "Сетевое обращение из Lua"),
        (@"ffi\.C\.", "Обращение к структурам C (ffi.C)"),
        (@"loadstring\s*\(|(?<![a-zA-Z0-9_\.])load\s*\(", "Динамическая компиляция строк (load / loadstring)"),
        (@"_G\s*\.|_G\s*\[|rawget\s*\(\s*_G", "Динамическое обращение к глобальной таблице _G"),
        (@"setfenv\s*\(|getfenv\s*\(", "Изменение окружения функций (setfenv / getfenv)"),
        (@"string\.char\s*\((?:\s*\d+\s*,){5,}", "Цепочка string.char(...)"),
        (@"os\s*\.\s*(?:remove|rename)\b", "Удаление или переименование файлов (os.remove / os.rename)")
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

                    // 1. Посимвольный лексер Lua (удаляет комментарии ТОЛЬКО вне строковых литералов)
                    string UncommentedLua = StripLuaCommentsWithLexer(rawLua);
                    // 2. Деобфускация эскейпов (\111\115 -> os), склейка конкатенаций ("exe".."cute" -> "execute") и скобок (os["execute"] -> os.execute)
                    string normalizedLua = NormalizeAndDeobfuscateLua(UncommentedLua);

                    foreach (var (pattern, desc) in CriticalLuaExploitSignatures)
                    {
                        if (Regex.IsMatch(normalizedLua, pattern, RegexOptions.IgnoreCase))
                        {
                            report.CriticalLuaExploitsFound.Add($"{Path.GetFileName(entryName)}: {desc}");
                        }
                    }

                    foreach (var (pattern, desc) in HeuristicLuaWarningSignatures)
                    {
                        if (Regex.IsMatch(normalizedLua, pattern, RegexOptions.IgnoreCase))
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

    /// <summary>
    /// Настоящий посимвольный лексер Lua (конечный автомат):
    /// распознаёт одинарные '...', двойные "..." и многострочные [=[...]=] строки,
    /// поэтому символы "--" или "--[[" внутри строк НИКОГДА не обрезают следующий за ними код!
    /// </summary>
    public static string StripLuaCommentsWithLexer(string code)
    {
        var sb = new StringBuilder(code.Length);
        int i = 0;
        int len = code.Length;

        while (i < len)
        {
            char c = code[i];

            // 1. Одинарная или двойная строка ('...' или "...")
            if (c == '"' || c == '\'')
            {
                char quote = c;
                sb.Append(c);
                i++;
                while (i < len)
                {
                    char sc = code[i];
                    sb.Append(sc);
                    i++;
                    if (sc == '\\' && i < len)
                    {
                        sb.Append(code[i]);
                        i++;
                    }
                    else if (sc == quote)
                    {
                        break;
                    }
                }
                continue;
            }

            // 2. Проверка на многострочный литерал [=[ ... ]=]
            if (c == '[')
            {
                int eqCount = MatchLongBracketOpening(code, i);
                if (eqCount >= 0)
                {
                    int openLen = eqCount + 2;
                    sb.Append(code, i, openLen);
                    i += openLen;
                    int closePos = FindLongBracketClosing(code, i, eqCount);
                    if (closePos >= 0)
                    {
                        int closeLen = eqCount + 2;
                        sb.Append(code, i, closePos + closeLen - i);
                        i = closePos + closeLen;
                    }
                    else
                    {
                        sb.Append(code, i, len - i);
                        break;
                    }
                    continue;
                }
            }

            // 3. Проверка на комментарий (-- или --[=[ ... ]=])
            if (c == '-' && i + 1 < len && code[i + 1] == '-')
            {
                i += 2;
                if (i < len && code[i] == '[')
                {
                    int eqCount = MatchLongBracketOpening(code, i);
                    if (eqCount >= 0)
                    {
                        i += eqCount + 2;
                        int closePos = FindLongBracketClosing(code, i, eqCount);
                        if (closePos >= 0)
                        {
                            i = closePos + eqCount + 2;
                            sb.Append(' ');
                            continue;
                        }
                        break;
                    }
                }

                // Однострочный комментарий до конца строки
                while (i < len && code[i] != '\n' && code[i] != '\r')
                    i++;

                continue;
            }

            sb.Append(c);
            i++;
        }

        return sb.ToString();
    }

    private static int MatchLongBracketOpening(string s, int pos)
    {
        if (pos >= s.Length || s[pos] != '[') return -1;
        int p = pos + 1;
        int eq = 0;
        while (p < s.Length && s[p] == '=') { eq++; p++; }
        return (p < s.Length && s[p] == '[') ? eq : -1;
    }

    private static int FindLongBracketClosing(string s, int startPos, int eqCount)
    {
        string target = "]" + new string('=', eqCount) + "]";
        return s.IndexOf(target, startPos, StringComparison.Ordinal);
    }

    /// <summary>
    /// Деобфускатор выражений Lua:
    /// 1. Декодирует \xXX и \ddd эскейпы внутри строковых литералов ("\111\115" -> "os").
    /// 2. Склеивает конкатенации строковых литералов ("exe" .. "cute" -> "execute").
    /// 3. Преобразует скобочный доступ к таблицам (os["execute"] или _G["os"]["execute"]) в точечный (_G.os.execute).
    /// </summary>
    public static string NormalizeAndDeobfuscateLua(string code)
    {
        // 1. Декодируем \xXX (hex) и \ddd (десятичные) эскейпы
        string decoded = Regex.Replace(code, @"\\x([0-9a-fA-F]{2})", m =>
        {
            int val = int.Parse(m.Groups[1].Value, NumberStyles.HexNumber);
            return val is >= 32 and <= 126 ? ((char)val).ToString() : m.Value;
        });

        decoded = Regex.Replace(decoded, @"\\([0-9]{1,3})", m =>
        {
            if (int.TryParse(m.Groups[1].Value, out int val) && val is >= 32 and <= 126)
                return ((char)val).ToString();
            return m.Value;
        });

        // 2. Склеиваем конкатенации соседних строковых литералов ("exe" .. "cute" -> "execute")
        for (int pass = 0; pass < 4; pass++)
        {
            string next = Regex.Replace(decoded, @"""([^""\\]*)""\s*\.\.\s*""([^""\\]*)""", "\"$1$2\"");
            next = Regex.Replace(next, @"'([^'\\]*)'\s*\.\.\s*'([^'\\]*)'", "'$1$2'");
            if (next == decoded) break;
            decoded = next;
        }

        // 3. Нормализуем обращения вида tbl["method"] или tbl['method'] -> tbl.method
        string normalized = Regex.Replace(decoded, @"\[\s*[""']([a-zA-Z_][a-zA-Z0-9_]*)[""']\s*\]", ".$1");
        return normalized;
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
