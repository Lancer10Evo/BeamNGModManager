using System.IO;
using System.Reflection;
using System.Text.Json;

namespace BeamNGModManager.Services;

public class LocalizationService
{
    private Dictionary<string, Dictionary<string, string>> _dictionaries = new();
    public string CurrentLanguage { get; private set; } = "ru";

    public IReadOnlyList<string> AvailableLanguages => _dictionaries.Keys.ToList();

    public LocalizationService()
    {
        LoadDictionaries();
    }

    private void LoadDictionaries()
    {
        try
        {
            // Если рядом с .exe положить внешний locales.json, программа прочитает его (удобно для кастомных переводов).
            // Иначе читаем вшитый внутрь .exe ресурс.
            string externalFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "locales.json");
            string json;

            if (File.Exists(externalFile))
            {
                json = File.ReadAllText(externalFile);
            }
            else
            {
                var assembly = Assembly.GetExecutingAssembly();
                using var stream = assembly.GetManifestResourceStream("BeamNGModManager.locales.json");
                if (stream == null) return;
                using var reader = new StreamReader(stream);
                json = reader.ReadToEnd();
            }

            _dictionaries = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, string>>>(json) ?? new();
        }
        catch
        {
            _dictionaries = new();
        }
    }

    public void SetLanguage(string langCode)
    {
        if (_dictionaries.ContainsKey(langCode))
            CurrentLanguage = langCode;
    }

    public string Get(string key)
    {
        if (_dictionaries.TryGetValue(CurrentLanguage, out var dict) && dict.TryGetValue(key, out var val))
            return val;

        // Резервный откат на английский, если ключ не найден в выбранном языке
        if (_dictionaries.TryGetValue("en", out var enDict) && enDict.TryGetValue(key, out var enVal))
            return enVal;

        return key;
    }
}