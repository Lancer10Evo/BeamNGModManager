using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Windows;
using System.Windows.Input;
using BeamNGModManager.Services;
using Microsoft.Web.WebView2.Core;

namespace BeamNGModManager.Views;

public partial class ModHubWindow : Window
{
    private readonly SmartInstallerService _installer;
    private readonly SecurityScannerService _securityScanner = new();
    private readonly string _targetModsDir;
    private readonly Func<Task> _onModInstalledCallback;

    // Строгий белый список разрешённых порталов модов и их официальных CDN
    private static readonly string[] AllowedDomains =
    {
        "beamng.com",
        "modland.net",
        "modsfire.com",
        "sharemods.com",
        "playground.ru",
        "nexusmods.com",
        "nexus-cdn.com"
    };

    public ModHubWindow(SmartInstallerService installer, string targetModsDir, Func<Task> onModInstalledCallback)
    {
        InitializeComponent();
        _installer = installer;
        _targetModsDir = targetModsDir;
        _onModInstalledCallback = onModInstalledCallback;

        InitializeWebViewAsync();
    }

    private async void InitializeWebViewAsync()
    {
        try
        {
            await Browser.EnsureCoreWebView2Async();

            // Защита приватности: отключаем автосохранение паролей и форм
            Browser.CoreWebView2.Settings.IsPasswordAutosaveEnabled = false;
            Browser.CoreWebView2.Settings.IsGeneralAutofillEnabled = false;

            // Контроль переходов по белому списку доменов
            Browser.CoreWebView2.NavigationStarting += CoreWebView2_NavigationStarting;

            Browser.CoreWebView2.SourceChanged += (_, _) =>
            {
                TxtUrl.Text = Browser.Source?.ToString() ?? "";
            };

            Browser.CoreWebView2.DownloadStarting += CoreWebView2_DownloadStarting;
        }
        catch (Exception ex)
        {
            LblHubStatus.Text = $"Ошибка инициализации WebView2: {ex.Message}";
        }
    }

    private void CoreWebView2_NavigationStarting(object? sender, CoreWebView2NavigationStartingEventArgs e)
    {
        if (Uri.TryCreate(e.Uri, UriKind.Absolute, out var uri))
        {
            if (!IsDomainAllowed(uri.Host))
            {
                e.Cancel = true;
                LblHubStatus.Text = $"🛡️ Заблокирован переход на посторонний сайт: {uri.Host}";
            }
        }
    }

    private static bool IsDomainAllowed(string host)
    {
        return AllowedDomains.Any(d =>
            host.Equals(d, StringComparison.OrdinalIgnoreCase) ||
            host.EndsWith("." + d, StringComparison.OrdinalIgnoreCase));
    }

    private void CoreWebView2_DownloadStarting(object? sender, CoreWebView2DownloadStartingEventArgs e)
    {
        string originalFileName = Path.GetFileName(e.ResultFilePath);
        string ext = Path.GetExtension(originalFileName).ToLowerInvariant();

        if (ext is not (".zip" or ".rar" or ".7z"))
        {
            e.Cancel = true;
            LblHubStatus.Text = $"🛡️ Заблокировано скачивание потенциально опасного файла: {originalFileName}";
            return;
        }

        e.Handled = true;
        string tempDownloadDir = Path.Combine(Path.GetTempPath(), "BeamNGModMgr_Quarantine");
        Directory.CreateDirectory(tempDownloadDir);

        string tempFilePath = Path.Combine(tempDownloadDir, Guid.NewGuid().ToString("N")[..6] + "_" + originalFileName);
        e.ResultFilePath = tempFilePath;

        var downloadOp = e.DownloadOperation;
        LblHubStatus.Text = $"⬇️ Скачивание в карантин для проверки: {originalFileName}...";
        PbDownload.IsIndeterminate = false;
        PbDownload.Value = 0;

        downloadOp.BytesReceivedChanged += (_, _) =>
        {
            Dispatcher.Invoke(() =>
            {
                long received = downloadOp.BytesReceived;
                long total = (long)(downloadOp.TotalBytesToReceive ?? 0);
                double recMb = received / 1024.0 / 1024.0;

                if (total > 0)
                {
                    double totMb = total / 1024.0 / 1024.0;
                    double percent = (double)received / total * 100.0;
                    PbDownload.Value = percent;
                    LblHubStatus.Text = $"⬇️ Скачивание {originalFileName}: {percent:F0}% ({recMb:F1} MB / {totMb:F1} MB)";
                }
                else
                {
                    PbDownload.IsIndeterminate = true;
                    LblHubStatus.Text = $"⬇️ Скачивание {originalFileName}: {recMb:F1} MB...";
                }
            });
        };

        downloadOp.StateChanged += async (_, _) =>
        {
            if (downloadOp.State == CoreWebView2DownloadState.Completed)
            {
                await Dispatcher.InvokeAsync(async () =>
                {
                    await ScanAndConfirmInstallAsync(tempFilePath, originalFileName);
                });
            }
            else if (downloadOp.State == CoreWebView2DownloadState.Interrupted)
            {
                Dispatcher.Invoke(() =>
                {
                    PbDownload.IsIndeterminate = false;
                    PbDownload.Value = 0;
                    LblHubStatus.Text = $"❌ Загрузка прервана: {originalFileName}";
                });
            }
        };
    }

    private async Task ScanAndConfirmInstallAsync(string quarantineFilePath, string displayFileName)
    {
        try
        {
            PbDownload.IsIndeterminate = true;
            LblHubStatus.Text = $"🛡️ Проверка антивирусом Windows Defender и анализ Lua-скриптов: {displayFileName}...";

            var scanReport = await _securityScanner.ScanArchiveAsync(quarantineFilePath);

            PbDownload.IsIndeterminate = false;
            PbDownload.Value = 100;

            string dialogText = scanReport.BuildFormattedSummary() +
                                "\n\nНажмите:\n" +
                                "• «Да» — Установить мод в игру\n" +
                                "• «Нет» — Открыть страницу хеша этого файла на VirusTotal\n" +
                                "• «Отмена» — Удалить скачанный архив из карантина";

            var choice = MessageBox.Show(
                dialogText,
                scanReport.IsSafe ? "🛡️ Подтверждение безопасности мода" : "🚨 ПРЕДУПРЕЖДЕНИЕ БЕЗОПАСНОСТИ",
                MessageBoxButton.YesNoCancel,
                scanReport.IsSafe ? MessageBoxImage.Information : MessageBoxImage.Warning);

            if (choice == MessageBoxResult.No)
            {
                Process.Start(new ProcessStartInfo(scanReport.VirusTotalUrl) { UseShellExecute = true });
                var secondChoice = MessageBox.Show(
                    $"Страница VirusTotal открыта в браузере.\n\nУстановить мод «{displayFileName}» в игру?",
                    "Подтверждение установки",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (secondChoice != MessageBoxResult.Yes)
                {
                    LblHubStatus.Text = $"🗑️ Установка отменена пользователем, файл {displayFileName} удалён.";
                    return;
                }
            }
            else if (choice != MessageBoxResult.Yes)
            {
                LblHubStatus.Text = $"🗑️ Установка отменена пользователем, файл {displayFileName} удалён.";
                return;
            }

            PbDownload.IsIndeterminate = true;
            LblHubStatus.Text = $"⚙️ Умная распаковка и установка: {displayFileName}...";

            var result = await _installer.InstallArchivesAsync(new[] { quarantineFilePath }, _targetModsDir);

            PbDownload.IsIndeterminate = false;
            if (result.InstalledFiles.Count > 0)
            {
                string installedNames = string.Join(", ", result.InstalledFiles);
                LblHubStatus.Text = $"✅ Проверено и установлено: {installedNames}!";
                await _onModInstalledCallback();
            }
            else
            {
                LblHubStatus.Text = $"⚠️ Архив {displayFileName} не содержит папок BeamNG.drive.";
            }
        }
        finally
        {
            if (File.Exists(quarantineFilePath))
            {
                try { File.Delete(quarantineFilePath); } catch { }
            }
        }
    }

    private void BtnPortalBeamNg_Click(object sender, RoutedEventArgs e) => NavigateTo("https://www.beamng.com/resources/");
    private void BtnPortalModLand_Click(object sender, RoutedEventArgs e) => NavigateTo("https://www.modland.net/beamng.drive-mods");
    private void BtnPortalPlayGround_Click(object sender, RoutedEventArgs e) => NavigateTo("https://www.playground.ru/beamng_drive/file");
    private void BtnPortalNexus_Click(object sender, RoutedEventArgs e) => NavigateTo("https://www.nexusmods.com/beamngdrive");

    private void NavigateTo(string url)
    {
        if (Browser.CoreWebView2 != null)
            Browser.CoreWebView2.Navigate(url);
    }

    private void BtnBack_Click(object sender, RoutedEventArgs e)
    {
        if (Browser.CanGoBack) Browser.GoBack();
    }

    private void BtnForward_Click(object sender, RoutedEventArgs e)
    {
        if (Browser.CanGoForward) Browser.GoForward();
    }

    private void BtnReload_Click(object sender, RoutedEventArgs e) => Browser.Reload();

    private void TxtUrl_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            string url = TxtUrl.Text.Trim();
            if (!url.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                url = "https://" + url;
            NavigateTo(url);
        }
    }

    private async void BtnDirectDownload_Click(object sender, RoutedEventArgs e)
    {
        string url = TxtUrl.Text.Trim();
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            MessageBox.Show("Вставьте корректную ссылку в адресную строку.", Title, MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (!IsDomainAllowed(uri.Host))
        {
            MessageBox.Show($"Домен «{uri.Host}» не входит в белый список разрешённых порталов модов.",
                Title, MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        string ext = Path.GetExtension(uri.AbsolutePath).ToLowerInvariant();
        if (ext is not (".zip" or ".rar" or ".7z"))
        {
            NavigateTo(url);
            return;
        }

        string fileName = Path.GetFileName(uri.AbsolutePath);
        string tempDir = Path.Combine(Path.GetTempPath(), "BeamNGModMgr_Quarantine");
        Directory.CreateDirectory(tempDir);
        string tempFile = Path.Combine(tempDir, Guid.NewGuid().ToString("N")[..6] + "_" + fileName);

        try
        {
            PbDownload.IsIndeterminate = false;
            PbDownload.Value = 0;
            LblHubStatus.Text = $"⬇️ Прямая загрузка в карантин: {fileName}...";

            using var client = new HttpClient();
            client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64)");

            using var response = await client.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();

            long totalBytes = response.Content.Headers.ContentLength ?? -1;
            await using var contentStream = await response.Content.ReadAsStreamAsync();
            await using var fileStream = new FileStream(tempFile, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true);

            byte[] buffer = new byte[81920];
            long totalRead = 0;
            int bytesRead;

            while ((bytesRead = await contentStream.ReadAsync(buffer)) > 0)
            {
                await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead));
                totalRead += bytesRead;
                if (totalBytes > 0)
                    PbDownload.Value = (double)totalRead / totalBytes * 100.0;
            }

            fileStream.Close();
            await ScanAndConfirmInstallAsync(tempFile, fileName);
        }
        catch (Exception ex)
        {
            PbDownload.IsIndeterminate = false;
            LblHubStatus.Text = $"❌ Ошибка прямой загрузки: {ex.Message}";
        }
    }
}