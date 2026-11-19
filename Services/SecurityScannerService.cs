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
    VerifiedSafe,
    UnverifiedWarning,
    DangerousThreat
}

public enum LuaTokenType
{
    Identifier,
    StringLiteral,
    Number,
    Symbol
}

public readonly record struct LuaToken(LuaTokenType Type, string Value);

public class SecurityScanReport
{
    public string FileName { get; set; } = string.Empty;
    public string SourceOrigin { get; set; } = "Локальный файл с ПК";
    public long FileSizeBytes { get; set; }
    public string Sha256Hash { get; set; } = string.Empty;

    public string VirusTotalStatus { get; set; } = string.Empty;
    public bool VtConfirmedClean { get; set; }
    public bool VtDetectedHighThreat { get; set; }
    public bool VtHasLowDetections { get; set; }

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
        sb.AppendLine($"ОТЧЁТ БЕЗОПАСНОСТИ (Security Audit v3.0)");
        sb.AppendLine($"━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
        sb.AppendLine($"Файл: {FileName} ({mb:F2} MB)");
        sb.AppendLine($"Источник: {SourceOrigin}");
        if (Sha256Hash.Length >= 32)
            sb.AppendLine($"SHA-256: {Sha256Hash[..16]}...{Sha256Hash[^16..]}");
        sb.AppendLine();

        sb.AppendLine($"1. Поиск хеша архива в базе VirusTotal:");
        sb.AppendLine($"   {VirusTotalStatus}");
        sb.AppendLine();

        sb.AppendLine($"2. Локальная проверка файла (Microsoft Defender):");
        sb.AppendLine($"   {DefenderStatus}");
        sb.AppendLine();

        sb.AppendLine($"3. Проверка на исполняемые файлы (.exe/.dll/.bat/.ps1/.scr):");
        sb.AppendLine(DangerousBinariesFound.Count == 0
            ? "   Чисто (исполняемых файлов внутри архива не обнаружено)"
            : $"   БЛОКИРОВКА! Найдены исполняемые файлы: {string.Join(", ", DangerousBinariesFound.Take(5))}");
        sb.AppendLine();

        string amsiInfo = AmsiInitialized
            ? $"AMSI проверил буферов: {AmsiScriptsScanned}"
            : "AMSI не инициализирован";

        sb.AppendLine($"4. Потоковый токенизатор и лексер Lua (Lua: {LuaFilesScanned}, Влож.: {NestedArchivesScanned}, JS: {UiJsFilesCount} | {amsiInfo}):");
        if (CriticalLuaExploitsFound.Count == 0)
            sb.AppendLine("   Запрещённых вызовов в потоке токенов Lua/JS не обнаружено");
        else
            sb.AppendLine($"   БЛОКИРОВКА! ОПАСНЫЙ КОД В СКРИПТАХ:\n   • {string.Join("\n   • ", CriticalLuaExploitsFound.Take(5))}");

        if (HeuristicWarnings.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine($"5. Нестандартные конструкции и предупреждения:");
            sb.AppendLine($"   • {string.Join("\n   • ", HeuristicWarnings.Take(5))}");
        }

        sb.AppendLine($"━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
        switch (Verdict)
        {
            case ScanVerdict.VerifiedSafe:
                sb.AppendLine(LuaFilesScanned == 0 && UiJsFilesCount == 0
                    ? "ВЕРДИКТ: Чистый ресурсный мод (исполняемых скриптов .lua/.js и бинарников внутри нет, Defender: Код 0)."
                    : "ВЕРДИКТ: Defender проверил архив (Код 0), в потоке токенов Lua запрещённых вызовов не найдено.");
                break;
            case ScanVerdict.UnverifiedWarning:
                sb.AppendLine("ВЕРДИКТ: Статус НЕ подтверждён полностью (либо есть предупреждения анализатора токенов). Решение за вами.");
                break;
            case ScanVerdict.DangerousThreat:
                sb.AppendLine("ВЕРДИКТ: УСТАНОВКА ЗАБЛОКИРОВАНА! Обнаружены явные признаки угрозы.");
                break;
        }

        return sb.ToString();
    }
}

public class SecurityScannerService
{
    public static Task<string> ComputeSha256OnlyAsync(string filePath) => Task.Run(() => ComputeSha256(filePath));
    public static (bool allPassed, string summary) RunSelfTestSuite() => RunSelfTests();
    private const int MaxNestedScanDepth = 3;
    private const long MaxSingleNestedArchiveBytes = 256L * 1024 * 1024;
    private const long MaxTotalNestedQuotaBytes = 512L * 1024 * 1024;
    private const int VtHardBlockThreshold = 3;

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

    // Белый список абсолютно безопасных методов стандартных библиотек Lua, используемых в модах BeamNG (не вызывают шума!)
    private static readonly HashSet<string> SafeOsMembers = new(StringComparer.Ordinal)
    {
        "clock", "time", "date", "difftime"
    };

    private static readonly HashSet<string> SafePackageMembers = new(StringComparer.Ordinal)
    {
        "path", "cpath", "loaded", "preload", "config", "searchers", "loaders"
    };

    private static readonly HashSet<string> SafeDebugMembers = new(StringComparer.Ordinal)
    {
        "traceback", "getinfo"
    };

    // Критические методы, блокируемые при любом обращении
    private static readonly HashSet<string> CriticalBlockedMembers = new(StringComparer.OrdinalIgnoreCase)
    {
        "execute", "popen", "loadlib", "exit"
    };

    // Методы файлового ввода-вывода и отладки, дающие предупреждение
    private static readonly HashSet<string> WarningMembers = new(StringComparer.OrdinalIgnoreCase)
    {
        "remove", "rename", "getenv", "tmpname", "open", "write", "lines", "input", "output",
        "getregistry", "setupvalue", "getupvalue", "sethook", "setmetatable"
    };

    private static readonly (string Pattern, string Description)[] CriticalJsSignatures =
    {
        (@"ActiveXObject|WScript\.Shell", "Вызов системного COM/ActiveX из UI-скрипта"),
        (@"child_process", "Попытка запуска дочернего процесса из JS")
    };

    public async Task<(bool isValid, string message)> ValidateVtApiKeyAsync(string candidateKey)
    {
        string clean = candidateKey.Trim();
        if (clean.Length != 64 || !clean.All(Uri.IsHexDigit))
            return (false, "Ключ VirusTotal должен состоять ровно из 64 шестнадцатеричных символов.");

        try
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(8) };
            client.DefaultRequestHeaders.Add("x-apikey", clean);

            using var response = await client.GetAsync($"https://www.virustotal.com/api/v3/users/{clean}");
            if (response.StatusCode == HttpStatusCode.Unauthorized || response.StatusCode == HttpStatusCode.Forbidden)
                return (false, "Сервер VirusTotal отклонил этот ключ (401). Возможно, вы вставили SHA-256 хеш файла вместо API-ключа!");

            if (response.IsSuccessStatusCode)
                return (true, "Ключ проверен на сервере VirusTotal и зашифрован через Windows DPAPI!");

            return (false, $"Сервер VirusTotal вернул код {(int)response.StatusCode}.");
        }
        catch (Exception ex)
        {
            return (false, $"Ошибка соединения с VirusTotal: {ex.Message}");
        }
    }

    public static async Task<string> ComputeFileSha256OnlyAsync(string filePath)
    {
        return await Task.Run(() => ComputeSha256(filePath));
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
                int hr = AmsiInitialize("BeamModManagerUltimate", out amsiCtx);
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
            report.VirusTotalStatus = "Ключ VT API не задан (нажмите кнопку «VT API» в верхней панели или «Нет» для открытия в браузере)";
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
                report.VirusTotalStatus = "Хеш этого архива отсутствует в базе VirusTotal (404 — файл ранее не загружался на VT)";
                return;
            }

            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                report.VirusTotalStatus = "Ошибка 401: Недействительный ключ VirusTotal API";
                return;
            }

            if (!response.IsSuccessStatusCode)
            {
                report.VirusTotalStatus = $"VirusTotal API вернул статус {(int)response.StatusCode}";
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
                analysisDateStr = DateTimeOffset.FromUnixTimeSeconds(unixSec).LocalDateTime.ToString("dd.MM.yyyy");

            if (malicious >= VtHardBlockThreshold)
            {
                report.VtDetectedHighThreat = true;
                report.VirusTotalStatus = $"VirusTotal (анализ от {analysisDateStr}): ОБНАРУЖЕНА УГРОЗА ({malicious} из {totalEngines} движков)!";
            }
            else if (malicious > 0 || suspicious > 0)
            {
                report.VtHasLowDetections = true;
                report.HeuristicWarnings.Add($"На VirusTotal есть единичные срабатывания (malicious: {malicious}, suspicious: {suspicious} из {totalEngines}).");
                report.VirusTotalStatus = $"VirusTotal (анализ от {analysisDateStr}): Единичные срабатывания ({malicious} mal / {suspicious} susp из {totalEngines})";
            }
            else if (totalEngines > 0)
            {
                report.VtConfirmedClean = true;
                report.VirusTotalStatus = $"VirusTotal (кэш анализа от {analysisDateStr}): 0 срабатываний из {totalEngines} движков";
            }
            else
            {
                report.VirusTotalStatus = "VirusTotal: Статистика анализа пуста.";
            }
        }
        catch (Exception ex)
        {
            report.VirusTotalStatus = $"Ошибка запроса к VirusTotal API: {ex.Message}";
        }
    }

    private static string FormatSourceOrigin(string? domain, out string? warning)
    {
        warning = null;
        if (string.IsNullOrWhiteSpace(domain))
            return "Локальный файл с ПК";

        if (domain.Contains("beamng.com", StringComparison.OrdinalIgnoreCase))
            return "Официальный репозиторий BeamNG.com";

        if (domain.Contains("nexusmods.com", StringComparison.OrdinalIgnoreCase) ||
            domain.Contains("nexus-cdn.com", StringComparison.OrdinalIgnoreCase))
            return "Nexus Mods";

        if (domain.Contains("modsfire.com", StringComparison.OrdinalIgnoreCase) ||
            domain.Contains("sharemods.com", StringComparison.OrdinalIgnoreCase) ||
            domain.Contains("cloudflarestorage.com", StringComparison.OrdinalIgnoreCase))
        {
            warning = $"Файл загружен через файловое зеркало ({domain}).";
            return $"Файловое зеркало ({domain})";
        }

        warning = $"Сторонний портал модов ({domain}).";
        return $"Сторонний портал ({domain})";
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
            if (entryName.Contains("..", StringComparison.Ordinal) || Path.IsPathRooted(entryName) || !string.IsNullOrEmpty(entry.LinkTarget))
            {
                report.DangerousBinariesFound.Add($"[Zip-Slip / Symlink] {entryName}");
                continue;
            }
            string ext = Path.GetExtension(entryName).ToLowerInvariant();
            string displayPath = currentDepth == 0 ? entryName : $"{containerName} -> {entryName}";

            if (ForbiddenBinaryExtensions.Contains(ext))
            {
                report.DangerousBinariesFound.Add(displayPath);
                continue;
            }

            if (ext is ".zip" or ".rar" or ".7z")
            {
                if (entry.Size > MaxSingleNestedArchiveBytes || (nestedBudgetUsed + Math.Max(entry.Size, 0)) > MaxTotalNestedQuotaBytes)
                {
                    report.HeuristicWarnings.Add($"Вложенный архив {displayPath} превышает квоту вложенной проверки.");
                    continue;
                }

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

                    // Считаем реально прочитанные байты, а не только заголовок entry.Size
                    byte[] buffer = new byte[81920];
                    int read;
                    long actualNestedBytes = 0;
                    while ((read = entryStream.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        actualNestedBytes += read;
                        nestedBudgetUsed += read;
                        if (actualNestedBytes > MaxSingleNestedArchiveBytes || nestedBudgetUsed > MaxTotalNestedQuotaBytes)
                            throw new InvalidOperationException("Превышен лимит реально распакованных байтов вложенного архива.");
                        tempFs.Write(buffer, 0, read);
                    }

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
                    report.HeuristicWarnings.Add($"Крупный Lua-скрипт ({entry.Size / 1024 / 1024.0:F1} MB): {displayPath}");

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

                    var (crits, warns) = AnalyzeLuaCode(rawLua, Path.GetFileName(entryName));
                    report.CriticalLuaExploitsFound.AddRange(crits);
                    report.HeuristicWarnings.AddRange(warns);
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
                        report.CriticalLuaExploitsFound.Add($"{Path.GetFileName(entryName)} (JS): {desc}");
                }
            }
        }
    }

    /// <summary>
    /// Полноценный потоковый анализатор токенов Lua (Token-Stream Analyzer):
    /// 1. Удаляет комментарии лексером.
    /// 2. Декодирует эскейпы (\111\115 -> os), склеивает конкатенации ("exe".."cute" -> "execute") и нормализует tbl["key"] -> tbl.key.
    /// 3. Разбивает код на типизированные токены (LuaToken) и проверяет контекст использования таблиц os, io, package, ffi, debug, _G, _ENV.
    /// </summary>
    public static (List<string> criticals, List<string> warnings) AnalyzeLuaCode(string rawLua, string fileLabel = "script.lua")
    {
        var criticals = new List<string>();
        var warnings = new List<string>();

        if (rawLua.Contains(@"\27Lua", StringComparison.Ordinal) || rawLua.Contains("\x1bLua", StringComparison.Ordinal))
            criticals.Add($"{fileLabel}: Скрытый прекомпилированный байткод Lua");

        string uncommented = StripLuaCommentsWithLexer(rawLua);
        string normalized = NormalizeAndDeobfuscateLua(uncommented);
        List<LuaToken> tokens = TokenizeLua(normalized);

        for (int i = 0; i < tokens.Count; i++)
        {
            var tok = tokens[i];

            // Проверка строковых литералов на вызов командных интерпретаторов ОС или прямых имён эксплойтов
            if (tok.Type == LuaTokenType.StringLiteral)
            {
                if (Regex.IsMatch(tok.Value, @"powershell(?:\.exe)?|cmd\.exe|wscript(?:\.exe)?|cscript(?:\.exe)?|mshta(?:\.exe)?|certutil", RegexOptions.IgnoreCase))
                    criticals.Add($"{fileLabel}: Строка вызова системного интерпретатора Windows ({tok.Value})");

                if (CriticalBlockedMembers.Contains(tok.Value))
                    criticals.Add($"{fileLabel}: Строковая ссылка на запрещённый метод ({tok.Value})");

                continue;
            }

            if (tok.Type != LuaTokenType.Identifier)
                continue;

            string id = tok.Value;

            // 1. Обращение к системным библиотекам: os, io, package, ffi, debug
            if (id is "os" or "io" or "package" or "ffi" or "debug")
            {
                // Проверяем, не является ли этот токен полем другой таблицы (например, myTable.os)
                if (i > 0 && tokens[i - 1].Type == LuaTokenType.Symbol && tokens[i - 1].Value == ".")
                {
                    string prevObj = (i >= 2 && tokens[i - 2].Type == LuaTokenType.Identifier) ? tokens[i - 2].Value : "";
                    if (prevObj is not ("_G" or "_ENV"))
                        continue;
                }

                // Сценарий А: Вызов через точку (например, os.clock или os.execute)
                if (i + 2 < tokens.Count && tokens[i + 1].Type == LuaTokenType.Symbol && tokens[i + 1].Value == "." &&
                    tokens[i + 2].Type == LuaTokenType.Identifier)
                {
                    string member = tokens[i + 2].Value;

                    // Белый список безопасных методов BeamNG (0 шума!)
                    if (id == "os" && SafeOsMembers.Contains(member)) continue;
                    if (id == "package" && SafePackageMembers.Contains(member)) continue;
                    if (id == "debug" && SafeDebugMembers.Contains(member)) continue;

                    if (CriticalBlockedMembers.Contains(member) || (id == "ffi" && member.Equals("load", StringComparison.OrdinalIgnoreCase)))
                    {
                        criticals.Add($"{fileLabel}: Запрещённый системный вызов {id}.{member}");
                    }
                    else if (WarningMembers.Contains(member) || id == "ffi")
                    {
                        warnings.Add($"{fileLabel}: Обращение к системному методу {id}.{member}");
                    }
                }
                // Сценарий Б: Динамическая индексация системной таблицы через скобки (os[k], io[var])
                else if (i + 1 < tokens.Count && tokens[i + 1].Type == LuaTokenType.Symbol && tokens[i + 1].Value == "[")
                {
                    criticals.Add($"{fileLabel}: Динамическая индексация системной библиотеки {id}[...]");
                }
                // Сценарий В: Передача таблицы по ссылке / алиасинг / множественное присваивание (x = os; a, b = os, io; pairs(os); fn(os))
                else
                {
                    // Игнорируем, если os/io является именем локального параметра или поля (local os = ... или os = ...)
                    bool isLeftHandSideAssignment = (i + 1 < tokens.Count && tokens[i + 1].Type == LuaTokenType.Symbol && tokens[i + 1].Value == "=");
                    if (!isLeftHandSideAssignment)
                    {
                        warnings.Add($"{fileLabel}: Непрямое использование / алиасинг системной таблицы '{id}' (передача ссылки или перебор)");
                    }
                }
            }
            // 2. Динамическая кодогенерация и метатаблицы окружения
            else if (id == "require" && i + 2 < tokens.Count && tokens[i + 1].Value is "(" or "\"" or "'" || (id == "require" && i + 1 < tokens.Count && tokens[i + 1].Type == LuaTokenType.StringLiteral))
            {
                string reqArg = (i + 2 < tokens.Count && tokens[i + 2].Type == LuaTokenType.StringLiteral) ? tokens[i + 2].Value :
                                (i + 1 < tokens.Count && tokens[i + 1].Type == LuaTokenType.StringLiteral) ? tokens[i + 1].Value : "";
                if (reqArg.Equals("ffi", StringComparison.OrdinalIgnoreCase) || reqArg.Equals("os", StringComparison.OrdinalIgnoreCase) || reqArg.Equals("io", StringComparison.OrdinalIgnoreCase))
                    criticals.Add($"{fileLabel}: Загрузка системного модуля через require('{reqArg}')");
            }
            else if (id is "load" or "loadstring" or "dofile" or "loadfile" or "setfenv" or "getfenv" or "rawget")
            {
                bool isLhs = (i + 1 < tokens.Count && tokens[i + 1].Value == "="); if (!isLhs)
                {
                    if (id is "dofile" or "loadfile")
                        criticals.Add($"{fileLabel}: Загрузка внешнего скрипта с диска ({id})");
                    else
                        warnings.Add($"{fileLabel}: Динамическая компиляция или обход окружения ({id})");
                }
            }
            // 3. Обращение к _G или _ENV
            else if (id is "_G" or "_ENV")
            {
                if (i + 1 < tokens.Count && tokens[i + 1].Type == LuaTokenType.Symbol && tokens[i + 1].Value == "[")
                {
                    warnings.Add($"{fileLabel}: Динамическая индексация глобального окружения {id}[...]");
                }
            }
        }

        return (criticals.Distinct().ToList(), warnings.Distinct().ToList());
    }

    public static List<LuaToken> TokenizeLua(string code)
    {
        var tokens = new List<LuaToken>();
        int i = 0;
        int len = code.Length;

        while (i < len)
        {
            char c = code[i];
            if (char.IsWhiteSpace(c)) { i++; continue; }

            if (c == '"' || c == '\'')
            {
                char quote = c;
                var sb = new StringBuilder();
                i++;
                while (i < len)
                {
                    char sc = code[i++];
                    if (sc == '\\' && i < len)
                        sb.Append(code[i++]);
                    else if (sc == quote)
                        break;
                    else
                        sb.Append(sc);
                }
                tokens.Add(new LuaToken(LuaTokenType.StringLiteral, sb.ToString()));
                continue;
            }

            if (char.IsLetter(c) || c == '_')
            {
                int start = i;
                while (i < len && (char.IsLetterOrDigit(code[i]) || code[i] == '_')) i++;
                tokens.Add(new LuaToken(LuaTokenType.Identifier, code[start..i]));
                continue;
            }

            if (char.IsDigit(c))
            {
                int start = i;
                while (i < len && (char.IsLetterOrDigit(code[i]) || code[i] == '.')) i++;
                tokens.Add(new LuaToken(LuaTokenType.Number, code[start..i]));
                continue;
            }

            tokens.Add(new LuaToken(LuaTokenType.Symbol, c.ToString()));
            i++;
        }

        return tokens;
    }

    /// <summary>
    /// Расширенный набор из 8 тестов самодиагностики (для CI и --self-test)
    /// </summary>
    public static (bool allPassed, string summary) RunSelfTests()
    {
        // Тест 1: Комментарии внутри строк не скрывают код
        string t1 = "local s = \"--[[\"; os.execute(\"calc\"); local e = \"]]\"";
        var (c1, _) = AnalyzeLuaCode(t1);
        bool p1 = c1.Count > 0;

        // Тест 2: Деобфускация эскейпов, склейки и скобок
        string t2 = "_G[\"\\111\" .. \"s\"][\"exe\" .. \"cute\"](\"cmd\")";
        var (c2, _) = AnalyzeLuaCode(t2);
        bool p2 = c2.Count > 0;

        // Тест 3: Множественное присваивание алиасов (a, b = os, io)
        string t3 = "local a, b = os, io\na.execute('calc')";
        var (_, w3) = AnalyzeLuaCode(t3);
        bool p3 = w3.Count > 0;

        // Тест 4: Передача таблицы os в pairs(os) или функцию
        string t4 = "for k, v in pairs(os) do print(k) end";
        var (_, w4) = AnalyzeLuaCode(t4);
        bool p4 = w4.Count > 0;

        // Тест 5: Динамическая индексация os[var]
        string t5 = "local fn = 'exec' .. 'ute'; os[fn]('calc')";
        var (c5, _) = AnalyzeLuaCode(t5);
        bool p5 = c5.Count > 0;

        // Тест 6: Реалистичный многострочный контроллер автомобиля BeamNG (должен давать 0 угроз и 0 ложных предупреждений!)
        string benignBeamNgMod = @"
            -- BeamNG Vehicle Custom Controller
            local M = {}
            local math_abs = math.abs
            local lastTime = os.clock()
            local stamp = os.date('%Y-%m-%d', os.time())
            local pkgPath = package.path

            local function updateGFX(dt)
                local rpm = electrics.values.rpm or 0
                local throttle = electrics.values.throttle or 0
                if rpm > 7500 then
                    guihooks.message('Shift Up! -- not a comment', 1, 'info')
                end
                local elapsed = os.difftime(os.time(), 0)
            end

            local function onReset()
                debug.traceback()
            end

            M.updateGFX = updateGFX
            M.onReset = onReset
            return M
        ";
        var (c6, w6) = AnalyzeLuaCode(benignBeamNgMod);
        bool p6 = c6.Count == 0 && w6.Count == 0;

        bool all = p1 && p2 && p3 && p4 && p5 && p6;
        return (all, $"Self-Test Suite (6/6: Lexer, Deobfuscator, Multi-Assign Alias, pairs(os), Dynamic os[k], Benign BeamNG Controller Zero-Noise): {(all ? "PASSED" : "FAILED")}");
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
                report.DefenderStatus = "Сканер Microsoft Defender (MpCmdRun.exe) не найден — сам файл архива антивирусом НЕ проверялся.";
                return;
            }

            var psi = new ProcessStartInfo
            {
                FileName = mpCmdRun,
                Arguments = "",
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            psi.ArgumentList.Add("-Scan"); psi.ArgumentList.Add("-ScanType"); psi.ArgumentList.Add("3"); psi.ArgumentList.Add("-File"); psi.ArgumentList.Add(filePath); psi.ArgumentList.Add("-DisableRemediation"); using var proc = Process.Start(psi);
            if (proc == null)
            {
                report.DefenderConfirmedClean = false;
                report.DefenderStatus = "Не удалось запустить процесс MpCmdRun.exe.";
                return;
            }

            bool exited = proc.WaitForExit(120_000);
            if (!exited)
            {
                try { proc.Kill(); } catch { }
                report.DefenderConfirmedClean = false;
                report.DefenderStatus = "НЕ ЗАВЕРШЕНО: Сканирование Defender превысило 120 сек.";
                return;
            }

            int code = proc.ExitCode;
            if (code == 0)
            {
                report.DefenderConfirmedClean = true;
                report.DefenderStatus = "Microsoft Defender (MpCmdRun): Архив проверен, угроз не обнаружено (Код 0)";
            }
            else if (code == 2)
            {
                report.DefenderConfirmedClean = false;
                report.DefenderDetectedThreat = true;
                report.DefenderStatus = "Microsoft Defender: ОБНАРУЖЕНА ВРЕДОНОСНАЯ ПРОГРАММА (Код 2)!";
            }
            else
            {
                report.DefenderConfirmedClean = false;
                report.DefenderStatus = $"Microsoft Defender вернул код ({code}) — безопасность НЕ подтверждена.";
            }
        }
        catch (Exception ex)
        {
            report.DefenderConfirmedClean = false;
            report.DefenderStatus = $"Ошибка вызова Microsoft Defender ({ex.Message}).";
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