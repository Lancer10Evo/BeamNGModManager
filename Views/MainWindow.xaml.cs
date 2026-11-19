using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using BeamNGModManager.Models;
using BeamNGModManager.Services;
using Microsoft.Win32;

namespace BeamNGModManager.Views;

public partial class MainWindow : Window
{
    private readonly LocalizationService _loc = new();
    private readonly PathResolverService _pathResolver = new();
    private readonly SmartInstallerService _installer = new();
    private readonly SecurityScannerService _securityScanner = new();
    private readonly ModManagerService _modManager = new();
    private readonly MaintenanceService _maintenance = new();
    private readonly UpdateService _updater = new();

    private List<ModItem> _allMods = new();
    private GameProfile? ActiveProfile => CmbProfiles.SelectedItem as GameProfile;

        [System.Runtime.InteropServices.DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        try
        {
            IntPtr hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
            int dark = 1;
            _ = DwmSetWindowAttribute(hwnd, 20, ref dark, sizeof(int)); // DWMWA_USE_IMMERSIVE_DARK_MODE
            int captionColor = 0x00141111; // Чёрный цвет заголовка #111114
            _ = DwmSetWindowAttribute(hwnd, 35, ref captionColor, sizeof(int));
        }
        catch { }
    }

    private async void BtnScanSelectedMod_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (GridMods.SelectedItem is ModItem mod && File.Exists(mod.FullPath))
            {
                LblStatus.Text = $"Проверка мода: {mod.FileName}...";
                var report = await _securityScanner.ScanArchiveAsync(mod.FullPath, null, _pathResolver.Config.GetVtApiKey());
                LblStatus.Text = _loc.Get("drag_drop_hint");
                if (MessageBox.Show(report.BuildFormattedSummary() + "\n\nОткрыть страницу хеша этого мода на VirusTotal?",
                    "Отчёт проверки установленного мода", MessageBoxButton.YesNo, MessageBoxImage.Information) == MessageBoxResult.Yes)
                {
                    Process.Start(new ProcessStartInfo(report.VirusTotalUrl) { UseShellExecute = true });
                }
            }
        }
        catch (Exception ex) { MessageBox.Show($"Ошибка проверки: {ex.Message}", Title, MessageBoxButton.OK, MessageBoxImage.Error); }
    }

    private async void BtnCopySelectedModHash_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (GridMods.SelectedItem is ModItem mod && File.Exists(mod.FullPath))
            {
                string hash = await SecurityScannerService.ComputeSha256OnlyAsync(mod.FullPath);
                Clipboard.SetText(hash);
                LblStatus.Text = $"SHA-256 скопирован: {hash}";
            }
        }
        catch (Exception ex) { MessageBox.Show($"Ошибка вычисления хеша: {ex.Message}", Title, MessageBoxButton.OK, MessageBoxImage.Error); }
    }
    public MainWindow()
    {
                InitializeComponent();
        InitializeApp();
    }

    private async void InitializeApp()
    {
        _pathResolver.LoadConfig();

        foreach (string lang in _loc.AvailableLanguages)
            CmbLanguage.Items.Add(lang);

        CmbLanguage.SelectedItem = _pathResolver.Config.Language;
        ChkHardlinks.IsChecked = _pathResolver.Config.UseHardlinks;

        RefreshProfilesCombo();
        RefreshPresetsCombo();
        ApplyLocalization();
        await ReloadModsListAsync();
    }

    private void RefreshProfilesCombo()
    {
        CmbProfiles.ItemsSource = null;
        CmbProfiles.ItemsSource = _pathResolver.Config.Profiles;
        if (_pathResolver.Config.Profiles.Count > 0)
        {
            int idx = Math.Clamp(_pathResolver.Config.SelectedProfileIndex, 0, _pathResolver.Config.Profiles.Count - 1);
            CmbProfiles.SelectedIndex = idx;
        }
    }

    private void RefreshPresetsCombo()
    {
        CmbPresets.ItemsSource = null;
        CmbPresets.ItemsSource = _pathResolver.Config.Presets;
    }

    private void ApplyLocalization()
    {
        Title = _loc.Get("app_title");
        LblProfile.Text = _loc.Get("profile");
        BtnAddProfile.Content = _loc.Get("add_profile");
        BtnAutoDetect.Content = _loc.Get("auto_detect");
        LblStatus.Text = _loc.Get("drag_drop_hint");
        FilterAll.Content = _loc.Get("filter_all");
        FilterActive.Content = _loc.Get("filter_active");
        FilterDisabled.Content = _loc.Get("filter_disabled");
        ChkHardlinks.Content = _loc.Get("use_hardlinks");
        BtnLaunchGame.Content = $"▶ {_loc.Get("launch_game")}";

        ColActive.Header = _loc.Get("col_active");
        ColName.Header = _loc.Get("col_name");
        ColAuthor.Header = _loc.Get("col_author");
        ColVersion.Header = _loc.Get("col_version");
        ColSize.Header = _loc.Get("col_size");
    }

    private async Task ReloadModsListAsync()
    {
        if (ActiveProfile == null) return;
        _allMods = await _modManager.LoadModsAsync(ActiveProfile.ModsPath, _pathResolver.Config.FavoriteMods);
        ActiveProfile.ModsCount = _allMods.Count;
        UpdateDashboardStats();
        ApplySearchAndFilter();
    }

    private void UpdateDashboardStats()
    {
        int total = _allMods.Count;
        int active = _allMods.Count(m => m.IsEnabled);
        int disabled = total - active;
        long totalBytes = _allMods.Sum(m => m.SizeBytes);

        TxtStatTotal.Text = total.ToString();
        TxtStatActive.Text = active.ToString();
        TxtStatDisabled.Text = disabled.ToString();

        double mb = totalBytes / 1024.0 / 1024.0;
        TxtStatSize.Text = mb >= 1024 ? $"{mb / 1024.0:F2} GB" : $"{mb:F1} MB";
    }

    private void ApplySearchAndFilter()
    {
        if (GridMods == null) return;
        string query = TxtSearch.Text.Trim();
        int filterIndex = CmbFilter.SelectedIndex;

        var filtered = _allMods
            .Where(m =>
            {
                if (filterIndex == 1 && !m.IsEnabled) return false;
                if (filterIndex == 2 && m.IsEnabled) return false;
                if (filterIndex == 3 && !m.IsFavorite) return false;
                if (filterIndex == 4 && m.SourceTag != "🌐 BeamMP") return false;
                if (filterIndex == 5 && !m.Category.Contains("Авто")) return false;
                if (filterIndex == 6 && !m.Category.Contains("Карта")) return false;
                if (filterIndex == 7 && !m.Category.Contains("Скрипт")) return false;

                if (!string.IsNullOrEmpty(query))
                {
                    return m.DisplayName.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                           m.Author.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                           m.FileName.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                           m.Category.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                           m.SourceTag.Contains(query, StringComparison.OrdinalIgnoreCase);
                }
                return true;
            })
            .OrderByDescending(m => m.IsFavorite)
            .ThenBy(m => m.DisplayName)
            .ToList();

        GridMods.ItemsSource = filtered;
        if (GridMods.SelectedItem == null && filtered.Count > 0)
            GridMods.SelectedIndex = 0;
    }

    // --- Инспектор выбранного мода (правая панель) ---
    private void GridMods_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (GridMods.SelectedItem is ModItem mod)
        {
            ImgInspectorBrush.ImageSource = mod.PreviewImage;
            TxtInspectorTitle.Text = mod.DisplayName;
            TxtInspectorVersionBadge.Text = mod.Version == "—" ? "v1.0" : $"v{mod.Version}";

            BadgeCategory.Text = mod.Category;
            BadgeSource.Text = mod.SourceTag;
            BadgeStatus.Text = mod.IsEnabled ? "АКТИВЕН" : "ОТКЛЮЧЁН";
            BadgeStatus.Foreground = mod.IsEnabled ? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(16, 185, 129)) : new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(245, 158, 11));
            BadgeStatusBorder.BorderBrush = BadgeStatus.Foreground;

            TxtInspectorAuthor.Text = mod.Author;
            TxtInspectorGameId.Text = mod.GameId;
            TxtInspectorConfigs.Text = mod.ConfigsText;
            TxtInspectorSizeDate.Text = $"{mod.SizeFormatted} • {mod.InstallDate}";
            TxtInspectorFile.Text = mod.FileName;
        }
    }

    private void BtnRevealSelectedMod_Click(object sender, RoutedEventArgs e)
    {
        if (GridMods.SelectedItem is ModItem mod && File.Exists(mod.FullPath))
        {
            Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{mod.FullPath}\"") { UseShellExecute = true });
        }
    }

    private void BtnOpenZipArchive_Click(object sender, RoutedEventArgs e)
    {
        if (GridMods.SelectedItem is ModItem mod && File.Exists(mod.FullPath))
        {
            Process.Start(new ProcessStartInfo(mod.FullPath) { UseShellExecute = true });
        }
    }

    // --- Избранное (★) ---
    private void BtnFavoriteMod_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.DataContext is ModItem mod)
        {
            mod.IsFavorite = !mod.IsFavorite;
            if (mod.IsFavorite)
            {
                if (!_pathResolver.Config.FavoriteMods.Contains(mod.FileName, StringComparer.OrdinalIgnoreCase))
                    _pathResolver.Config.FavoriteMods.Add(mod.FileName);
            }
            else
            {
                _pathResolver.Config.FavoriteMods.RemoveAll(x => x.Equals(mod.FileName, StringComparison.OrdinalIgnoreCase));
            }

            _pathResolver.SaveConfig();
            ApplySearchAndFilter();
        }
    }

    // --- Установка модов (Файл с ПК, Drag-and-Drop и Мод-Хаб 2.2) ---
    private async void BtnInstallMods_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Multiselect = true,
            Filter = "BeamNG Mod Archives (*.zip;*.rar;*.7z)|*.zip;*.rar;*.7z"
        };

        if (dlg.ShowDialog() == true)
            await RunSmartInstallAsync(dlg.FileNames);
    }

    private void BtnOpenModHub_Click(object sender, RoutedEventArgs e)
    {
        if (ActiveProfile == null) return;
        var hubWindow = new ModHubWindow(_installer, ActiveProfile.ModsPath, ReloadModsListAsync, _pathResolver.Config.GetVtApiKey())
        {
            Owner = this
        };
        hubWindow.Show();
    }

    private void Window_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop)
            ? DragDropEffects.Copy
            : DragDropEffects.None;
        e.Handled = true;
    }

    private async void Window_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(DataFormats.FileDrop) is string[] files)
        {
            var validExt = files.Where(f =>
            {
                string ext = Path.GetExtension(f).ToLowerInvariant();
                return ext is ".zip" or ".rar" or ".7z";
            }).ToArray();

            if (validExt.Length > 0)
                await RunSmartInstallAsync(validExt);
        }
    }

    /// <summary>
    /// Единая защищённая точка установки для кнопки «＋ Файл с ПК» и перетаскивания Drag-and-Drop:
    /// каждый архив сначала проходит антивирусную и Lua-проверку с подтверждением пользователя.
    /// </summary>
    private async Task RunSmartInstallAsync(IEnumerable<string> files)
    {
        if (ActiveProfile == null) return;
        var verifiedFiles = new List<string>();

        foreach (string filePath in files)
        {
            if (!File.Exists(filePath)) continue;
            string name = Path.GetFileName(filePath);
            LblStatus.Text = $"🛡️ Проверка антивирусом и рекурсивный аудит Lua: {name}...";

            var report = await _securityScanner.ScanArchiveAsync(filePath, null, _pathResolver.Config.GetVtApiKey());
            if (report.Verdict == ScanVerdict.DangerousThreat)
            {
                if (MessageBox.Show(report.BuildFormattedSummary() + "\n\n⛔ Установка этого архива заблокирована!\nОткрыть отчёт хеша на VirusTotal?",
                    "⛔ УСТАНОВКА ЗАБЛОКИРОВАНА", MessageBoxButton.YesNo, MessageBoxImage.Error) == MessageBoxResult.Yes)
                {
                    Process.Start(new ProcessStartInfo(report.VirusTotalUrl) { UseShellExecute = true });
                }
                continue;
            }
            string dialogText = report.BuildFormattedSummary() +
                                "\n\nНажмите:\n" +
                                "• «Да» — Установить этот мод\n" +
                                "• «Нет» — Открыть страницу хеша этого файла на VirusTotal\n" +
                                "• «Отмена» — Пропустить и не устанавливать";

            string dialogTitle = report.Verdict switch
            {
                ScanVerdict.VerifiedSafe => "✅ Подтверждение установки (Проверено)",
                ScanVerdict.UnverifiedWarning => "⚠️ ВНИМАНИЕ: Статус не подтверждён антивирусом",
                _ => "🚨 КРИТИЧЕСКАЯ УГРОЗА БЕЗОПАСНОСТИ"
            };

            var choice = MessageBox.Show(
                dialogText,
                dialogTitle,
                MessageBoxButton.YesNoCancel,
                report.Verdict == ScanVerdict.VerifiedSafe ? MessageBoxImage.Information : MessageBoxImage.Warning);

            if (choice == MessageBoxResult.No)
            {
                Process.Start(new ProcessStartInfo(report.VirusTotalUrl) { UseShellExecute = true });
                if (MessageBox.Show($"Установить мод «{name}» после проверки VirusTotal?", Title,
                    MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                {
                    verifiedFiles.Add(filePath);
                }
            }
            else if (choice == MessageBoxResult.Yes)
            {
                verifiedFiles.Add(filePath);
            }
        }

        if (verifiedFiles.Count == 0)
        {
            LblStatus.Text = _loc.Get("drag_drop_hint");
            return;
        }

        LblStatus.Text = _loc.Get("installing_status");
        var result = await _installer.InstallArchivesAsync(verifiedFiles, ActiveProfile.ModsPath);
        await ReloadModsListAsync();

        LblStatus.Text = _loc.Get("drag_drop_hint");

        if (result.InvalidFiles.Count > 0)
        {
            MessageBox.Show(
                $"{_loc.Get("invalid_mod")}\n\n" + string.Join("\n", result.InvalidFiles),
                Title, MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    // --- Включение / Отключение ---
    private void ModCheckBox_Click(object sender, RoutedEventArgs e)
    {
        if (ActiveProfile == null) return;
        if (sender is CheckBox cb && cb.DataContext is ModItem mod)
        {
            _modManager.ToggleModState(mod, ActiveProfile.ModsPath, _pathResolver.Config.UseHardlinks);
            UpdateDashboardStats();
            GridMods_SelectionChanged(GridMods, null!);
            ApplySearchAndFilter();
        }
    }

    private void BtnEnableAll_Click(object sender, RoutedEventArgs e)
    {
        if (ActiveProfile == null) return;
        foreach (var mod in _allMods.Where(m => !m.IsEnabled))
        {
            mod.IsEnabled = true;
            _modManager.ToggleModState(mod, ActiveProfile.ModsPath, _pathResolver.Config.UseHardlinks);
        }
        UpdateDashboardStats();
        ApplySearchAndFilter();
    }

    private void BtnDisableAll_Click(object sender, RoutedEventArgs e)
    {
        if (ActiveProfile == null) return;
        foreach (var mod in _allMods.Where(m => m.IsEnabled))
        {
            mod.IsEnabled = false;
            _modManager.ToggleModState(mod, ActiveProfile.ModsPath, _pathResolver.Config.UseHardlinks);
        }
        UpdateDashboardStats();
        ApplySearchAndFilter();
    }

    private void BtnBeamMpOnlyMode_Click(object sender, RoutedEventArgs e)
    {
        if (ActiveProfile == null) return;
        foreach (var mod in _allMods)
        {
            bool shouldBeActive = mod.SourceTag == "🌐 BeamMP";
            if (mod.IsEnabled != shouldBeActive)
            {
                mod.IsEnabled = shouldBeActive;
                _modManager.ToggleModState(mod, ActiveProfile.ModsPath, _pathResolver.Config.UseHardlinks);
            }
        }
        UpdateDashboardStats();
        ApplySearchAndFilter();
        LblStatus.Text = "🌐 Включён режим BeamMP: активны только моды мультиплеера.";
    }

    private async void BtnDeleteMod_Click(object sender, RoutedEventArgs e)
    {
        if (ActiveProfile == null) return;
        if (sender is Button btn && btn.DataContext is ModItem mod)
        {
            var ans = MessageBox.Show(
                $"{_loc.Get("confirm_delete")}\n\n{mod.DisplayName} ({mod.FileName})",
                Title, MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (ans == MessageBoxResult.Yes)
            {
                _modManager.DeleteMod(mod, ActiveProfile.ModsPath);
                await ReloadModsListAsync();
            }
        }
    }

    // --- Именные Loadouts ---
    private void BtnSavePreset_Click(object sender, RoutedEventArgs e)
    {
        string customName = TxtPresetName.Text.Trim();
        if (string.IsNullOrEmpty(customName))
            customName = $"Loadout #{_pathResolver.Config.Presets.Count + 1}";

        var enabledFiles = _allMods.Where(m => m.IsEnabled).Select(m => m.FileName).ToList();

        var existing = _pathResolver.Config.Presets.FirstOrDefault(p => p.Name.Equals(customName, StringComparison.OrdinalIgnoreCase));
        if (existing != null)
        {
            existing.EnabledModFiles = enabledFiles;
            _pathResolver.SaveConfig();
            RefreshPresetsCombo();
            CmbPresets.SelectedItem = existing;
        }
        else
        {
            var preset = new ModPreset { Name = customName, EnabledModFiles = enabledFiles };
            _pathResolver.Config.Presets.Add(preset);
            _pathResolver.SaveConfig();
            RefreshPresetsCombo();
            CmbPresets.SelectedItem = preset;
        }

        MessageBox.Show($"🎛️ Loadout «{customName}» сохранён!\nАктивных модов в сборке: {enabledFiles.Count}",
            Title, MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void BtnExportPresets_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var dlg = new SaveFileDialog { Filter = "BeamModManager Loadouts (*.json)|*.json", FileName = "beamng_loadouts.json" };
            if (dlg.ShowDialog() == true)
            {
                string json = System.Text.Json.JsonSerializer.Serialize(_pathResolver.Config.Presets, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(dlg.FileName, json, System.Text.Encoding.UTF8);
                MessageBox.Show($"Экспортировано сборок: {_pathResolver.Config.Presets.Count}", Title, MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
        catch (Exception ex) { MessageBox.Show($"Ошибка экспорта: {ex.Message}", Title, MessageBoxButton.OK, MessageBoxImage.Error); }
    }

    private void BtnImportPresets_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var dlg = new OpenFileDialog { Filter = "BeamModManager Loadouts (*.json)|*.json" };
            if (dlg.ShowDialog() == true)
            {
                var imported = System.Text.Json.JsonSerializer.Deserialize<List<ModPreset>>(File.ReadAllText(dlg.FileName, System.Text.Encoding.UTF8));
                if (imported != null && imported.Count > 0)
                {
                    foreach (var p in imported)
                    {
                        if (!_pathResolver.Config.Presets.Any(x => x.Name.Equals(p.Name, StringComparison.OrdinalIgnoreCase)))
                            _pathResolver.Config.Presets.Add(p);
                    }
                    _pathResolver.SaveConfig();
                    RefreshPresetsCombo();
                    MessageBox.Show($"Импортировано сборок: {imported.Count}", Title, MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
        }
        catch (Exception ex) { MessageBox.Show($"Ошибка импорта: {ex.Message}", Title, MessageBoxButton.OK, MessageBoxImage.Error); }
    }

    private void BtnDeletePreset_Click(object sender, RoutedEventArgs e)
    {
        if (CmbPresets.SelectedItem is ModPreset preset)
        {
            _pathResolver.Config.Presets.Remove(preset);
            _pathResolver.SaveConfig();
            RefreshPresetsCombo();
        }
    }

    private void CmbPresets_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ActiveProfile == null) return;
        if (CmbPresets.SelectedItem is ModPreset preset)
        {
            TxtPresetName.Text = preset.Name;

            var existingOnDisk = new HashSet<string>(_allMods.Select(m => m.FileName), StringComparer.OrdinalIgnoreCase);
            preset.EnabledModFiles.RemoveAll(f => !existingOnDisk.Contains(f));
            _pathResolver.SaveConfig();

            var activeSet = new HashSet<string>(preset.EnabledModFiles, StringComparer.OrdinalIgnoreCase);
            foreach (var mod in _allMods)
            {
                bool shouldEnable = activeSet.Contains(mod.FileName);
                if (mod.IsEnabled != shouldEnable)
                {
                    mod.IsEnabled = shouldEnable;
                    _modManager.ToggleModState(mod, ActiveProfile.ModsPath, _pathResolver.Config.UseHardlinks);
                }
            }
            UpdateDashboardStats();
            ApplySearchAndFilter();
        }
    }

    // --- Диагностика, Здоровье и BeamMP ---
    private void BtnTestLicense_Click(object sender, RoutedEventArgs e)
    {
        string report = _pathResolver.RunLicenseSimulatorTest();
        MessageBox.Show(report, "Диагностика и Симулятор Лицензий (Steam / Epic)",
            MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void BtnVtApi_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new Window
        {
            Title = "🔑 Настройка VirusTotal API v3 (DPAPI Encrypted)",
            Width = 520,
            Height = 250,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Owner = this,
            ResizeMode = ResizeMode.NoResize,
            Background = (System.Windows.Media.Brush)FindResource("BgDark"),
            Foreground = (System.Windows.Media.Brush)FindResource("TextPrimary")
        };

        var stack = new StackPanel { Margin = new Thickness(18) };
        stack.Children.Add(new TextBlock
        {
            Text = "Введите ваш персональный API-ключ VirusTotal (64 символа):",
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 0, 0, 6)
        });

        var txtKey = new PasswordBox
        {
            Password = _pathResolver.Config.GetVtApiKey(),
            Height = 34,
            Margin = new Thickness(0, 0, 0, 8)
        };
        stack.Children.Add(txtKey);

        var lblHint = new TextBlock
        {
            Text = "🔒 Ключ проверяется на сервере VirusTotal (защита от случайной вставки SHA-256 хеша) и сохраняется в зашифрованном виде через Windows DPAPI.",
            FontSize = 11,
            TextWrapping = TextWrapping.Wrap,
            Foreground = (System.Windows.Media.Brush)FindResource("TextSecondary"),
            Margin = new Thickness(0, 0, 0, 14)
        };
        stack.Children.Add(lblHint);

        var btnPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        var btnGetKey = new Button { Content = "🌐 Получить ключ", Padding = new Thickness(12, 6, 12, 6), Margin = new Thickness(0, 0, 8, 0) };
        var btnClear = new Button { Content = "🗑️ Удалить ключ", Padding = new Thickness(12, 6, 12, 6), Margin = new Thickness(0, 0, 8, 0) };
        var btnSave = new Button { Content = "✅ Проверить и Сохранить", Padding = new Thickness(14, 6, 14, 6), FontWeight = FontWeights.Bold };

        btnGetKey.Click += (_, _) => Process.Start(new ProcessStartInfo("https://www.virustotal.com/gui/my-apikey") { UseShellExecute = true });
        btnClear.Click += (_, _) =>
        {
            _pathResolver.Config.SetVtApiKey("");
            _pathResolver.SaveConfig();
            txtKey.Password = "";
            MessageBox.Show("Ключ VirusTotal удалён из конфигурации.", dlg.Title, MessageBoxButton.OK, MessageBoxImage.Information);
            dlg.Close();
        };
        btnSave.Click += async (_, _) =>
        {
            btnSave.IsEnabled = false;
            var (isValid, msg) = await _securityScanner.ValidateVtApiKeyAsync(txtKey.Password);
            btnSave.IsEnabled = true;

            if (!isValid)
            {
                MessageBox.Show(msg, "Ошибка проверки ключа", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            _pathResolver.Config.SetVtApiKey(txtKey.Password);
            _pathResolver.SaveConfig();
            MessageBox.Show(msg, dlg.Title, MessageBoxButton.OK, MessageBoxImage.Information);
            dlg.Close();
        };

        btnPanel.Children.Add(btnGetKey);
        btnPanel.Children.Add(btnClear);
        btnPanel.Children.Add(btnSave);
        stack.Children.Add(btnPanel);
        dlg.Content = stack;
        dlg.ShowDialog();
    }

    private async void BtnHealthCheck_Click(object sender, RoutedEventArgs e)
    {
        if (ActiveProfile == null) return;
        string report = await _maintenance.RunHealthCheckAsync(_allMods, ActiveProfile.ModsPath);
        MessageBox.Show(report, "🩺 Health Center & Поиск Дубликатов", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private async void BtnCleanBeamMpCache_Click(object sender, RoutedEventArgs e)
    {
        if (ActiveProfile == null) return;
        var (count, mb) = _maintenance.CleanBeamMpServerCache(ActiveProfile.ModsPath);
        await ReloadModsListAsync();
        MessageBox.Show($"🌐 Кэш серверов BeamMP очищен!\nКлиентский мод BeamMP.zip сохранён.\nУдалено чужих модов: {count} шт. ({mb:F1} MB)",
            Title, MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void BtnLaunchBeamMp_Click(object sender, RoutedEventArgs e)
    {
        if (_maintenance.LaunchBeamMp(_pathResolver.Config.BeamMpExePath, out string resolved))
        {
            _pathResolver.Config.BeamMpExePath = resolved;
            _pathResolver.SaveConfig();
            return;
        }

        var dlg = new OpenFileDialog
        {
            Title = "Укажите путь к BeamMP-Launcher.exe (только один раз)",
            Filter = "BeamMP Launcher (BeamMP-Launcher.exe)|*.exe"
        };

        if (dlg.ShowDialog() == true)
        {
            _pathResolver.Config.BeamMpExePath = dlg.FileName;
            _pathResolver.SaveConfig();
            _maintenance.LaunchBeamMp(dlg.FileName, out _);
        }
    }

    private void BtnOpenFolder_Click(object sender, RoutedEventArgs e)
    {
        if (ActiveProfile == null || !Directory.Exists(ActiveProfile.ModsPath)) return;
        Process.Start(new ProcessStartInfo("explorer.exe", ActiveProfile.ModsPath) { UseShellExecute = true });
    }

    private async void CmbProfiles_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (CmbProfiles.SelectedIndex >= 0)
        {
            _pathResolver.Config.SelectedProfileIndex = CmbProfiles.SelectedIndex;
            _pathResolver.SaveConfig();
            await ReloadModsListAsync();
        }
    }

    private async void BtnAutoDetect_Click(object sender, RoutedEventArgs e)
    {
        _pathResolver.DiscoverAllProfiles();
        RefreshProfilesCombo();
        await ReloadModsListAsync();
    }

    private async void BtnAddProfile_Click(object sender, RoutedEventArgs e)
    {
        var gameDlg = new OpenFolderDialog
        {
            Title = "Выберите корневую папку игры BeamNG.drive (где лежит BeamNG.drive.exe или startup.ini)"
        };
        if (gameDlg.ShowDialog() != true) return;

        var userDlg = new OpenFolderDialog
        {
            Title = "Выберите папку UserPath или саму папку mods (или нажмите Отмена для автопоиска)"
        };
        string? manualUser = userDlg.ShowDialog() == true ? userDlg.FolderName : null;

        var profile = _pathResolver.ResolveByHierarchy(
            gameDlg.FolderName, manualUser, $"Profile #{_pathResolver.Config.Profiles.Count + 1}");

        _pathResolver.Config.Profiles.Add(profile);
        _pathResolver.Config.SelectedProfileIndex = _pathResolver.Config.Profiles.Count - 1;
        _pathResolver.SaveConfig();
        RefreshProfilesCombo();
        await ReloadModsListAsync();
    }

    private void BtnClearCache_Click(object sender, RoutedEventArgs e)
    {
        if (ActiveProfile == null) return;
        int removed = _maintenance.ClearCacheKeepingShaders(ActiveProfile);
        MessageBox.Show($"{_loc.Get("cache_cleared")} {removed}", Title, MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private async void BtnCheckConflicts_Click(object sender, RoutedEventArgs e)
    {
        var conflicts = await _maintenance.DetectConflictsAsync(_allMods);
        if (conflicts.Count == 0)
        {
            MessageBox.Show(_loc.Get("conflicts_none"), Title, MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var lines = conflicts.Take(25)
            .Select(kv => $"• {kv.Key}\n   ↳ {string.Join(", ", kv.Value)}");

        MessageBox.Show(
            $"{_loc.Get("conflicts_found")} ({conflicts.Count})\n\n" + string.Join("\n\n", lines),
            Title, MessageBoxButton.OK, MessageBoxImage.Warning);
    }

    private async void BtnCheckUpdates_Click(object sender, RoutedEventArgs e)
    {
        var (available, latestVer, url) = await _updater.CheckForUpdatesAsync();
        if (available)
        {
            if (MessageBox.Show($"{_loc.Get("update_available")} v{latestVer}",
                Title, MessageBoxButton.YesNo, MessageBoxImage.Information) == MessageBoxResult.Yes)
            {
                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            }
        }
        else
        {
            MessageBox.Show(_loc.Get("update_latest"), Title, MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    private void BtnLaunchGame_Click(object sender, RoutedEventArgs e)
    {
        if (ActiveProfile == null) return;
        if (!_maintenance.LaunchGame(ActiveProfile))
        {
            MessageBox.Show(_loc.Get("exe_not_found"), Title, MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void TxtSearch_TextChanged(object sender, TextChangedEventArgs e) => ApplySearchAndFilter();
    private void CmbFilter_SelectionChanged(object sender, SelectionChangedEventArgs e) => ApplySearchAndFilter();

    private void CmbLanguage_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (CmbLanguage.SelectedItem is string lang)
        {
            _loc.SetLanguage(lang);
            _pathResolver.Config.Language = lang;
            _pathResolver.SaveConfig();
            ApplyLocalization();
        }
    }

    private void ChkHardlinks_Changed(object sender, RoutedEventArgs e)
    {
        _pathResolver.Config.UseHardlinks = ChkHardlinks.IsChecked == true;
        _pathResolver.SaveConfig();
    }
}



