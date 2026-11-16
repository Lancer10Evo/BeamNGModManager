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
    private readonly ModManagerService _modManager = new();
    private readonly MaintenanceService _maintenance = new();
    private readonly UpdateService _updater = new();

    private List<ModItem> _allMods = new();
    private GameProfile? ActiveProfile => CmbProfiles.SelectedItem as GameProfile;

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

    // --- Установка модов и Мод-Хаб 2.0 ---
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
        var hubWindow = new ModHubWindow(_installer, ActiveProfile.ModsPath, ReloadModsListAsync)
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

    private async Task RunSmartInstallAsync(IEnumerable<string> files)
    {
        if (ActiveProfile == null) return;
        LblStatus.Text = _loc.Get("installing_status");

        var result = await _installer.InstallArchivesAsync(files, ActiveProfile.ModsPath);
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