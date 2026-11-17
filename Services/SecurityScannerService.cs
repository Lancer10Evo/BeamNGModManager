using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using SharpCompress.Archives;

namespace BeamNGModManager.Services;

public enum ScanVerdict
{
    VerifiedSafe,      // Локальный Defender подтвердил чистоту (Код 0) + нет угроз/предупреждений в скриптах и VT
    UnverifiedWarning, // Прямых эксплойтов нет, но Defender не завершился, либо на VT 1-2 подозрения, либо сработала эвристика
    DangerousThreat    // Найдены бинарники (.exe/.dll/.bat), вирус Defender (Код 2), >=3 детектов на VT или прямой эксплойт -> БЛОКИРОВКА
}

public class SecurityScanReport
{
    public string FileName { get; set; } = string.Empty;
    public string SourceOrigin { get; set; } = "Локальный файл с ПК";
    public long FileSizeBytes { get; set; }
    public string Sha256Hash { get; set; } = string.Empty;

    public string VirusTotalStatus { get; set; } = string.Empty;
    public bool VtConfirmedClean { get; set; }
    public bool VtDetectedHighThreat { get; set; } // >= 3 детектов на VT -> блокировка
    public bool VtHasLowDetections { get; set; }   // 1-2 детекта или suspicious -> предупреждение

    public string DefenderStatus { get; set; } = string.Empty;
    public bool DefenderConfirmedClean { get; set; }
    public bool DefenderDetectedThreat { get; set; }

    public bool AmsiInitialized { get; set; }
    public int AmsiScriptsScanned { get; set; }
    public bool AmsiDetectedThreat { get; set; }

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
            if (VtDetectedHighThreat || DefenderDetectedThreat || AmsiDetectedThreat ||
                DangerousBinariesFound.Count > 0 || CriticalLuaExploitsFound.Count > 0)
            {
                return ScanVerdict.DangerousThreat;
            }

            // Для VerifiedSafe ОБЯЗАТЕЛЬНО требуется подтверждение локального сканера Defender (Код 0)
            // и отсутствие единичных срабатываний VT или эвристических предупреждений
            if (!DefenderConfirmedClean || VtHasLowDetections || HeuristicWarnings.Count > 0)
                return ScanVerdict.UnverifiedWarning;

            return ScanVerdict.VerifiedSafe;
        }
    }

    public string VirusTotalUrl => $"https://www.virustotal.com/gui/file/{Sha256Hash}";

    public string BuildFormattedSummary()
    {
        double mb = FileSizeBytes / 1024.0 / 1024.0;
        var sb = new StringBuilder();
        sb.AppendLine($"🛡️ ОТЧЁТ БЕЗОПАСНОСТИ (Security Audit v2.6)");
        sb.AppendLine($"━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
        sb.AppendLine($"📄 Файл: {FileName} ({mb:F2} MB)");
        sb.AppendLine($"🌐 Источник: {SourceOrigin}");
        if (Sha256Hash.Length >= 32)
            sb.AppendLine($"🔑 SHA-256: {Sha256Hash[..16]}...{Sha256Hash[^16..]}");
        sb.AppendLine();

        sb.AppendLine($"1. Поиск хеша архива в базе VirusTotal:");
        sb.AppendLine($"   {VirusTotalStatus}");
        sb.AppendLine();

        sb.AppendLine($"2. Локальная проверка файла (Microsoft Defender):");
        sb.AppendLine($"   {DefenderStatus}");
        sb.AppendLine();

        sb.AppendLine($"3. Проверка на исполняемые файлы (.exe/.dll/.bat/.ps1/.scr):");
        sb.AppendLine(DangerousBinariesFound.Count == 0
            ? "   ✅ Исполняемых файлов внутри архива не обнаружено"
            : $"   ❌ БЛОКИРОВКА! Найдены исполняемые файлы: {string.Join(", ", DangerousBinariesFound.Take(5))}");
        sb.AppendLine();

        string amsiInfo = AmsiInitialized
            ? $"AMSI проверил буферов: {AmsiScriptsScanned}"
            : "AMSI не инициализирован";

        sb.AppendLine($"4. Статический аудит скриптов (Lua: {LuaFilesScanned}, Влож.: {NestedArchivesScanned}, JS: {UiJsFilesCount} | {amsiInfo}):");
        if (CriticalLuaExploitsFound.Count == 0)
            sb.AppendLine("   ✅ Прямых эксплойтов в Lua/JS не обнаружено");
        else
            sb.AppendLine($"   🚨 БЛОКИРОВКА! ОПАСНЫЙ КОД В СКРИПТАХ:\n   • {string.Join("\n   • ", CriticalLuaExploitsFound.Take(5))}");

        if (HeuristicWarnings.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine($"5. Предупреждения и эвристика:");
            sb.AppendLine($"   ⚠️ {string.Join("\n   ⚠️ ", HeuristicWarnings.Take(5))}");
        }

        sb.AppendLine($"━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
        switch (Verdict)
        {
            case ScanVerdict.VerifiedSafe:
                sb.AppendLine("✅ ВЕРДИКТ: Локальная проверка Defender завершена (Код 0), опасных сигнатур в скриптах нет.");
                break;
            case ScanVerdict.UnverifiedWarning:
                sb.AppendLine("⚠️ ВЕРДИКТ: Статус НЕ подтверждён полностью (либо есть замечания эвристики / единичные срабатывания). Решение за вами.");
                break;
            case ScanVerdict.DangerousThreat:
                sb.AppendLine("⛔ ВЕРДИКТ: УСТАНОВКА ЗАБЛОКИРОВАНА! Обнаружены явные признаки угрозы.");
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
    private const int VtHardBlockThreshold = 3; // Блокируем намертво от 3 и более срабатываний (1-2 могут быть False Positive)

    [DllImport("amsi.dll", CharSet = CharSet.Unicode, CallingConvention = CallingConvention.StdCall)]
    private static extern int AmsiInitialize(string appName, out IntPtr amsiContext);

    [DllImport("amsi.dll", CallingConvention = CallingConvention.StdCall)]
    private static extern void AmsiUninitialize(IntPtr amsiContext);

    [DllImport("amsi.dll", CharSet = CharSet.Unicode, CallingConvention = CallingConvention.StdCall)]
    private static extern int AmsiScanBuffer(IntPtr amsiContext, byte[] buffer, uint length, string contentName, IntPtr session, out int result);

    private static readonly HashSet<string> ForbiddenBinaryExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".exe", ".dll", ".bat", ".cmd", ".ps1", ".vbs", ".vbe", ".jse", ".wsf", ".scr", ".msi", ".com", ".hta", ".pif", ".cpl"
    };

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

    /// <summary>
    /// Проверяет подлинность API-ключа VirusTotal перед сохранением (чтобы пользователь случайно не вставил SHA-256 хеш вместо ключа)
    /// </summary>
    public async Task<(bool isValid, string message)> ValidateVtApiKeyAsync(string candidateKey)
    {
        string clean = candidateKey.Trim();
        if (clean.Length != 64 || !clean.All(Uri.IsHexDigit))
            return (false, "Ключ VirusTotal должен состоять ровно из 64 шестнадцатеричных символов.");

        try
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(8) };
            client.DefaultRequestHeaders.Add("x-apikey", clean);

            // Проверяем ключ на эндпоинте пользователя VT API v3
            using var response = await client.GetAsync($"https://www.virustotal.com/api/v3/users/{clean}");
            if (response.StatusCode == HttpStatusCode.Unauthorized || response.StatusCode == HttpStatusCode.Forbidden)
            {
                return (false, "Сервер VirusTotal отклонил этот ключ (401 Unauthorized). Возможно, вы случайно скопировали SHA-256 хеш файла вместо API-ключа!");
            }

            if (response.IsSuccessStatusCode)
                return (true, "Ключ проверен на сервере VirusTotal и зашифрован через Windows DPAPI!");

            return (false, $"Сервер VirusTotal вернул код {(int)response.StatusCode}.");
        }
        catch (Exception ex)
        {
            return (false, $"Ошибка соединения с VirusTotal: {ex.Message}");
        }
    }

    public async Task<SecurityScanReport> ScanArchiveAsync(string archivePath, string? sourceDomain = null, string? virusTotalApiKey = null)
    {
        var fi = new FileInfo(archivePath);
        var report = new SecurityScanReport
        {
            FileName = fi.Name,
            FileSizeBytes = fi.Length,
            SourceOrigin = FormatSourceOrigin(sourceDomain, out string? sourceWarning),
            Sha256Hash = await Task.Run(() => ComputeSha256(archivePath))
        };

        if (!string.IsNullOrEmpty(sourceWarning))
            report.HeuristicWarnings.Add(sourceWarning);

        await QueryVirusTotalApiAsync(report, virusTotalApiKey);

        await Task.Run(() =>
        {
            RunWindowsDefenderScan(archivePath, report);

            IntPtr amsiCtx = IntPtr.Zero;
            try
            {
                int hr = AmsiInitialize("BeamNGModManagerPro", out amsiCtx);
                report.AmsiInitialized = (hr == 0 && amsiCtx != IntPtr.Zero);
            }
            catch
            {
                report.AmsiInitialized = false;
                amsiCtx = IntPtr.Zero;
            }

            try
            {
                long nestedBudgetUsed = 0;
                using var fs = File.OpenRead(archivePath);
                ScanArchiveStreamRecursive(fs, fi.Name, report, currentDepth: 0, ref nestedBudgetUsed, amsiCtx);
            }
            catch (Exception ex)
            {
                report.HeuristicWarnings.Add($"Не удалось полностью прочитать структуру архива: {ex.Message}");
            }
            finally
            {
                if (amsiCtx != IntPtr.Zero)
                {
                    try { AmsiUninitialize(amsiCtx); } catch { }
                }
            }
        });

        return report;
    }

    private static async Task QueryVirusTotalApiAsync(SecurityScanReport report, string? configuredApiKey)
    {
        string? apiKey = !string.IsNullOrWhiteSpace(configuredApiKey)
            ? configuredApiKey.Trim()
            : Environment.GetEnvironmentVariable("VT_API_KEY");

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            report.VirusTotalStatus = "ℹ️ Ключ VT API не задан (нажмите кнопку «🔑 VT API» в верхней панели или «Нет» для открытия в браузере)";
            return;
        }

        try
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
            client.DefaultRequestHeaders.Add("x-apikey", apiKey);

            string url = $"https://www.virustotal.com/api/v3/files/{report.Sha256Hash}";
            using var response = await client.GetAsync(url);

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                report.VirusTotalStatus = "ℹ️ Хеш этого архива ещё отсутствует в базе VirusTotal (404 — файл ранее не загружался на VT)";
                return;
            }

            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                report.VirusTotalStatus = "⚠️ Ошибка 401: Недействительный ключ VirusTotal API (проверьте ключ в «🔑 VT API»)";
                return;
            }

            if (!response.IsSuccessStatusCode)
            {
                report.VirusTotalStatus = $"⚠️ VirusTotal API вернул статус {(int)response.StatusCode}";
                return;
            }

            string json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            var attributes = doc.RootElement.GetProperty("data").GetProperty("attributes");
            var stats = attributes.GetProperty("last_analysis_stats");

            int malicious = stats.TryGetProperty("malicious", out var mProp) ? mProp.GetInt32() : 0;
            int suspicious = stats.TryGetProperty("suspicious", out var sProp) ? sProp.GetInt32() : 0;
            int harmless = stats.TryGetProperty("harmless", out var hProp) ? hProp.GetInt32() : 0;
            int undetected = stats.TryGetProperty("undetected", out var uProp) ? uProp.GetInt32() : 0;
            int totalEngines = malicious + suspicious + harmless + undetected;

            string analysisDateStr = "неизвестная дата";
            if (attributes.TryGetProperty("last_analysis_date", out var dateProp) && dateProp.TryGetInt64(out long unixSec))
            {
                analysisDateStr = DateTimeOffset.FromUnixTimeSeconds(unixSec).LocalDateTime.ToString("dd.MM.yyyy");
            }

            if (malicious >= VtHardBlockThreshold)
            {
                report.VtDetectedHighThreat = true;
                report.VirusTotalStatus = $"🚨 VirusTotal (анализ от {analysisDateStr}): ОБНАРУЖЕНА УГРОЗА ({malicious} из {totalEngines} движков)!";
            }
            else if (malicious > 0 || suspicious > 0)
            {
                report.VtHasLowDetections = true;
                report.HeuristicWarnings.Add($"На VirusTotal есть единичные срабатывания (malicious: {malicious}, suspicious: {suspicious} из {totalEngines}). Проверьте отчёт на сайте VT.");
                report.VirusTotalStatus = $"⚠️ VirusTotal (анализ от {analysisDateStr}): Единичные срабатывания ({malicious} mal / {suspicious} susp из {totalEngines}) — возможен False Positive";
            }
            else if (totalEngines > 0)
            {
                report.VtConfirmedClean = true;
                report.VirusTotalStatus = $"✅ VirusTotal (кэш анализа от {analysisDateStr}): 0 срабатываний из {totalEngines} движков";
            }
            else
            {
                report.VirusTotalStatus = "ℹ️ VirusTotal: Статистика анализа пуста.";
            }
        }
        catch (Exception ex)
        {
            report.VirusTotalStatus = $"⚠️ Ошибка запроса к VirusTotal API: {ex.Message}";
        }
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
            warning = $"Файл загружен через файловое зеркало ({domain}).";
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
        ref long nestedBudgetUsed,
        IntPtr amsiCtx)
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
                    ScanArchiveStreamRecursive(tempFs, Path.GetFileName(entryName), report, currentDepth + 1, ref nestedBudgetUsed, amsiCtx);
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

                    if (report.AmsiInitialized && amsiCtx != IntPtr.Zero)
                    {
                        report.AmsiScriptsScanned++;
                        if (ScanBufferWithAmsi(amsiCtx, rawLua, displayPath))
                        {
                            report.AmsiDetectedThreat = true;
                            report.CriticalLuaExploitsFound.Add($"{Path.GetFileName(entryName)}: Срабатывание антивируса через Windows AMSI!");
                        }
                    }

                    string uncommentedLua = StripLuaCommentsWithLexer(rawLua);
                    string normalizedLua = NormalizeAndDeobfuscateLua(uncommentedLua);

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

                if (report.AmsiInitialized && amsiCtx != IntPtr.Zero)
                {
                    report.AmsiScriptsScanned++;
                    if (ScanBufferWithAmsi(amsiCtx, jsCode, displayPath))
                    {
                        report.AmsiDetectedThreat = true;
                        report.CriticalLuaExploitsFound.Add($"{Path.GetFileName(entryName)} (JS): Срабатывание антивируса через Windows AMSI!");
                    }
                }

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

    private static bool ScanBufferWithAmsi(IntPtr amsiCtx, string content, string contentName)
    {
        try
        {
            byte[] bytes = Encoding.UTF8.GetBytes(content);
            if (bytes.Length == 0) return false;
            int hr = AmsiScanBuffer(amsiCtx, bytes, (uint)bytes.Length, contentName, IntPtr.Zero, out int amsiResult);
            return hr == 0 && amsiResult >= 32768;
        }
        catch
        {
            return false;
        }
    }

    public static string StripLuaCommentsWithLexer(string code)
    {
        var sb = new StringBuilder(code.Length);
        int i = 0;
        int len = code.Length;

        while (i < len)
        {
            char c = code[i];

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

    public static string NormalizeAndDeobfuscateLua(string code)
    {
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

        for (int pass = 0; pass < 4; pass++)
        {
            string next = Regex.Replace(decoded, @"""([^""\\]*)""\s*\.\.\s*""([^""\\]*)""", "\"$1$2\"");
            next = Regex.Replace(next, @"'([^'\\]*)'\s*\.\.\s*'([^'\\]*)'", "'$1$2'");
            if (next == decoded) break;
            decoded = next;
        }

        return Regex.Replace(decoded, @"\[\s*[""']([a-zA-Z_][a-zA-Z0-9_]*)[""']\s*\]", ".$1");
    }

    private static void RunWindowsDefenderScan(string filePath, SecurityScanReport report)
    {
        try
        {
            string? mpCmdRun = FindDefenderExecutable();
            if (mpCmdRun == null)
            {
                report.DefenderConfirmedClean = false;
                report.DefenderStatus = "⚠️ Сканер Microsoft Defender (MpCmdRun.exe) не найден — сам файл архива антивирусом НЕ проверялся.";
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
                report.DefenderStatus = "⚠️ Не удалось запустить процесс MpCmdRun.exe.";
                return;
            }

            bool exited = proc.WaitForExit(120_000);
            if (!exited)
            {
                try { proc.Kill(); } catch { }
                report.DefenderConfirmedClean = false;
                report.DefenderStatus = "⚠️ НЕ ЗАВЕРШЕНО: Сканирование Defender превысило 120 сек.";
                return;
            }

            int code = proc.ExitCode;
            if (code == 0)
            {
                report.DefenderConfirmedClean = true;
                report.DefenderStatus = "✅ Microsoft Defender (MpCmdRun): Архив проверен, угроз не обнаружено (Код 0)";
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
            report.DefenderStatus = $"⚠️ Ошибка вызова Microsoft Defender ({ex.Message}).";
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
