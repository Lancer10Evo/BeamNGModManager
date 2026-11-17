using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media.Imaging;

namespace BeamNGModManager.Models;

public class GameProfile
{
    public string Name { get; set; } = "Default";
    public string GameRootPath { get; set; } = string.Empty;
    public string UserPath { get; set; } = string.Empty;
    public string ActiveVersion { get; set; } = string.Empty;
    public string ModsPath { get; set; } = string.Empty;
    public string DetectionSource { get; set; } = "Manual";
    public string LicenseType { get; set; } = "Portable / Custom";
    public int ModsCount { get; set; }

    public string ShortTitle => string.IsNullOrEmpty(ActiveVersion)
        ? $"{Name} [{LicenseType}]"
        : $"{Name} [{ActiveVersion} • {LicenseType}]";

    public string ModsCountBadge => $"{ModsCount} мод.";

    public override string ToString() => $"{ShortTitle} ({ModsCount} мод.) — {ModsPath}";
}

public class ModPreset
{
    public string Name { get; set; } = string.Empty;
    public List<string> EnabledModFiles { get; set; } = new();
    public override string ToString() => $"🎛️ {Name} ({EnabledModFiles.Count} мод.)";
}

public class AppConfig
{
    public string Language { get; set; } = "ru";
    public int SelectedProfileIndex { get; set; } = 0;
    public bool UseHardlinks { get; set; } = false;
    public string BeamMpExePath { get; set; } = string.Empty;
    public string VirusTotalApiKey { get; set; } = string.Empty;
    public List<string> FavoriteMods { get; set; } = new();
    public List<GameProfile> Profiles { get; set; } = new();
    public List<ModPreset> Presets { get; set; } = new();
}

public class ModItem : INotifyPropertyChanged
{
    private bool _isEnabled;
    private bool _isFavorite;

    public string FileName { get; set; } = string.Empty;
    public string FullPath { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Author { get; set; } = "—";
    public string Version { get; set; } = "—";
    public string Category { get; set; } = "📦 Мод";
    public string SourceTag { get; set; } = "📁 Local";
    public string GameId { get; set; } = "—";
    public int ConfigsCount { get; set; }
    public string ConfigsText => ConfigsCount > 0 ? $"{ConfigsCount} шт. (.pc)" : "Нет / Базовый";
    public string InstallDate { get; set; } = "—";
    public long SizeBytes { get; set; }
    public string SizeFormatted => $"{SizeBytes / 1024.0 / 1024.0:F2} MB";
    public BitmapImage? PreviewImage { get; set; }

    public bool IsEnabled
    {
        get => _isEnabled;
        set
        {
            if (_isEnabled != value)
            {
                _isEnabled = value;
                OnPropertyChanged();
            }
        }
    }

    public bool IsFavorite
    {
        get => _isFavorite;
        set
        {
            if (_isFavorite != value)
            {
                _isFavorite = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(FavoriteStar));
            }
        }
    }

    public string FavoriteStar => IsFavorite ? "★" : "☆";

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

public class InstallResult
{
    public List<string> InstalledFiles { get; set; } = new();
    public List<string> InvalidFiles { get; set; } = new();
}
